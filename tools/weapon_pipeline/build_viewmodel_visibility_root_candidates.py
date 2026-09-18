"""Build deterministic TEMP-only ViewModel visibility-root candidates.

The supported DXRP view prefabs already place their custom renderers beneath a
single prefab-local weapon root, but omit ``ViewModel.AdditionalRendererRoot``.
That omission prevents the normal view-model visibility path from hiding the
replacement weapon as one unit.  This tool performs exactly one structural
change per input prefab::

    "AdditionalRendererRoot": {
      "_type": "gameobject",
      "go": "<pinned prefab-local weapon-root GUID>"
    }

AK-47 and M870 are read from exact byte-pinned active prefabs.  AKS-74U,
Desert Eagle, and M1911 must be supplied as exact caller-pinned generated
candidates through a manifest.  Generated inputs and every output must remain
beneath the operating system TEMP directory.  There is no product-write,
promotion-to-game, Portal, Git, or scene switch.

Generated-input manifest schema::

    {
      "version": 1,
      "generated_inputs": {
        "aks74u": {"path": "C:/...", "bytes": 1, "sha256": "64 hex"},
        "deserteagle": {"path": "C:/...", "bytes": 1, "sha256": "64 hex"},
        "m1911": {"path": "C:/...", "bytes": 1, "sha256": "64 hex"}
      }
    }

All JSON keys, GUIDs, component types, owner paths, renderer paths/models, and
byte pins are contracts.  The builder fails if ``AdditionalRendererRoot`` is
already present, even when it happens to contain the desired value.  Outputs
are create-only and use a missing-destination snapshot so an arrival race
cannot be overwritten.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import stat
import sys
import tempfile
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterator, Mapping, Sequence

import pipeline_io


WORKSPACE_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
TEMP_PREFIX = "dxrp-viewmodel-visibility-root-"
VIEW_MODEL_TYPE = "Dxura.RP.Game.ViewModel"
MODEL_RENDERER_TYPE = "Sandbox.ModelRenderer"
GENERATED_FAMILIES = frozenset({"aks74u", "deserteagle", "m1911"})


class ContractError(RuntimeError):
    """Raised when an input or destination violates the narrow contract."""


@dataclass(frozen=True)
class RendererContract:
    guid: str
    owner_path: str
    model: str
    component_type: str = MODEL_RENDERER_TYPE


@dataclass(frozen=True)
class WeaponContract:
    family: str
    source_kind: str
    root_name: str
    view_model_guid: str
    view_model_owner_path: str
    visibility_root_guid: str
    visibility_root_path: str
    renderer_prefix: str
    renderers: tuple[RendererContract, ...]
    output_name: str
    active_path: Path | None = None
    active_bytes: int | None = None
    active_sha256: str | None = None


@dataclass(frozen=True)
class InputPin:
    path: Path
    expected_bytes: int
    expected_sha256: str


@dataclass(frozen=True)
class NodeRecord:
    node: dict[str, Any]
    path: str
    parent_guid: str | None


@dataclass(frozen=True)
class PrefabIndex:
    nodes_by_guid: Mapping[str, NodeRecord]
    components_by_guid: Mapping[str, dict[str, Any]]
    component_owners: Mapping[str, str]
    all_guids: frozenset[str]


@dataclass(frozen=True)
class RenderedCandidate:
    contract: WeaponContract
    source_path: Path
    source_bytes: bytes
    output_bytes: bytes
    output_sha256: str
    original_reference_count: int


def _renderer(guid: str, owner_path: str, model: str) -> RendererContract:
    return RendererContract(guid=guid, owner_path=owner_path, model=model)


WEAPON_CONTRACTS: tuple[WeaponContract, ...] = (
    WeaponContract(
        family="ak47",
        source_kind="active",
        root_name="vm_ak47",
        view_model_guid="c8ecd6fd-8bad-4322-b9de-6de7a2bc5b50",
        view_model_owner_path="vm_ak47",
        visibility_root_guid="e7d10f28-79cd-4b00-b5ec-6ce242c66e91",
        visibility_root_path="vm_ak47/weapon_root",
        renderer_prefix="addons/lifepunch/lpweapons/ak47/",
        renderers=(
            _renderer(
                "a32f8062-802a-4118-8b3b-41cf43097c6e",
                "vm_ak47/weapon_root/ak47_mesh",
                "addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl",
            ),
        ),
        output_name="vm_ak47.visibility-root.candidate.prefab",
        active_path=(
            WORKSPACE_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab"
        ),
        active_bytes=76835,
        active_sha256=(
            "F63B94EE0A127DCC45BED0F2167FD405"
            "B9101C2C675B9789DE3FBBDF87C73255"
        ),
    ),
    WeaponContract(
        family="aks74u",
        source_kind="generated",
        root_name="vm_aks74u",
        view_model_guid="d932f165-a0b7-40e8-b66d-7df267e0a15f",
        view_model_owner_path="vm_aks74u",
        visibility_root_guid="3264d9c2-1e39-4837-b697-6555ffd9c51e",
        visibility_root_path="vm_aks74u/weapon_root",
        renderer_prefix="addons/lifepunch/lpweapons/aks74u/",
        renderers=(
            _renderer(
                "6dbed7b5-5dc5-4514-a59d-a91413c92223",
                "vm_aks74u/weapon_root/weapon_root_children/aks74u_body",
                "addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl",
            ),
            _renderer(
                "25629190-9d09-4165-8143-5801df1c1d86",
                "vm_aks74u/weapon_root/weapon_root_children/magazine/aks74u_magazine",
                "addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl",
            ),
        ),
        output_name="vm_aks74u.visibility-root.candidate.prefab",
    ),
    WeaponContract(
        family="deserteagle",
        source_kind="generated",
        root_name="vm_desert_eagle_common_origin_candidate",
        view_model_guid="dd4b78a6-22a0-538e-a67c-aa19df6ac458",
        view_model_owner_path="vm_desert_eagle_common_origin_candidate",
        visibility_root_guid="a25b319e-c265-5afc-bfc0-d1e9a369fd4c",
        visibility_root_path="vm_desert_eagle_common_origin_candidate/weapon_root",
        renderer_prefix="addons/lifepunch/lpweapons/deserteagle/",
        renderers=(
            _renderer(
                "6dc5e375-76bf-51ac-a163-d8ce28b33004",
                (
                    "vm_desert_eagle_common_origin_candidate/weapon_root/"
                    "weapon_root_children/desert_eagle_body"
                ),
                "addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl",
            ),
            _renderer(
                "8993a81a-ff7f-5da3-9e3b-09b5bc79b9f4",
                (
                    "vm_desert_eagle_common_origin_candidate/weapon_root/"
                    "weapon_root_children/slide/desert_eagle_slide"
                ),
                "addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl",
            ),
            _renderer(
                "ddb4b9de-58d8-5f43-9324-75be3fc9dc77",
                (
                    "vm_desert_eagle_common_origin_candidate/weapon_root/"
                    "weapon_root_children/magazine/desert_eagle_magazine"
                ),
                "addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl",
            ),
        ),
        output_name="vm_desert_eagle.visibility-root.candidate.prefab",
    ),
    WeaponContract(
        family="m1911",
        source_kind="generated",
        root_name="vm_m1911",
        view_model_guid="becae687-6ef6-57de-a240-5bccff1b9a0f",
        view_model_owner_path="vm_m1911",
        visibility_root_guid="994ee179-fd62-56a8-9c0a-077d01b34ec9",
        visibility_root_path="vm_m1911/weapon_root",
        renderer_prefix="addons/lifepunch/lpweapons/m1911/",
        renderers=(
            _renderer(
                "a2c32a8c-21d4-5dd9-80f9-0806834b5faf",
                "vm_m1911/weapon_root/weapon_root_children/m1911_body",
                "addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl",
            ),
            _renderer(
                "d330855c-40eb-5f97-b652-c1f0abbb5547",
                "vm_m1911/weapon_root/weapon_root_children/slide/m1911_slide",
                "addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl",
            ),
            _renderer(
                "51ca9973-8792-529d-b1e7-21b9f7b50fa5",
                "vm_m1911/weapon_root/weapon_root_children/magazine/m1911_magazine",
                "addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl",
            ),
        ),
        output_name="vm_m1911.visibility-root.candidate.prefab",
    ),
    WeaponContract(
        family="m870",
        source_kind="active",
        root_name="vm_m870",
        view_model_guid="43284320-056f-4a3e-b125-7948a56879b5",
        view_model_owner_path="vm_m870",
        visibility_root_guid="b9add72d-312b-4d90-8ef5-bc00c594443c",
        visibility_root_path="vm_m870/weapon_root",
        renderer_prefix="addons/lifepunch/lpweapons/m870/",
        renderers=(
            _renderer(
                "20b38c12-b055-4434-93a5-65b2d0f2039a",
                "vm_m870/weapon_root/weapon_root_children/m870_body",
                "addons/lifepunch/lpweapons/m870/models/m870_body.vmdl",
            ),
            _renderer(
                "5187b64e-db32-494d-abe2-7ce324af9d04",
                "vm_m870/weapon_root/weapon_root_children/m870_pump",
                "addons/lifepunch/lpweapons/m870/models/m870_pump.vmdl",
            ),
        ),
        output_name="vm_m870.visibility-root.candidate.prefab",
        active_path=(
            WORKSPACE_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab"
        ),
        active_bytes=70704,
        active_sha256=(
            "7BFA941833E2F023194D30758524C415"
            "8CEC61980CEAD4AC895F0FED188E2885"
        ),
    ),
)
CONTRACTS_BY_FAMILY: Mapping[str, WeaponContract] = {
    contract.family: contract for contract in WEAPON_CONTRACTS
}


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _absolute(path: Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True


def _reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ContractError(f"JSON object contains duplicate key {key!r}")
        result[key] = value
    return result


def _decode_json(raw: bytes, context: str) -> dict[str, Any]:
    try:
        value = json.loads(
            raw.decode("utf-8-sig"), object_pairs_hook=_reject_duplicate_keys
        )
    except ContractError:
        raise
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Cannot parse {context} as UTF-8 JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise ContractError(f"{context} root must be a JSON object")
    return value


def _require_exact_keys(
    value: object, expected: set[str], context: str
) -> dict[str, object]:
    if not isinstance(value, dict):
        raise ContractError(f"{context} must be a JSON object")
    actual = set(value)
    if actual != expected:
        raise ContractError(
            f"{context} must contain exactly {sorted(expected)}, got {sorted(actual)}"
        )
    return value


def _parse_sha256(value: object, context: str) -> str:
    if not isinstance(value, str) or value != value.strip():
        raise ContractError(f"{context} must be an exact SHA-256 string")
    normalized = value.upper()
    if len(normalized) != 64 or any(
        character not in "0123456789ABCDEF" for character in normalized
    ):
        raise ContractError(f"{context} must be exactly 64 hexadecimal characters")
    return normalized


def _parse_positive_bytes(value: object, context: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ContractError(f"{context} must be a positive integer")
    return value


def _walk_json(value: object) -> Iterator[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_json(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_json(child)


def _index_prefab(prefab: Mapping[str, object]) -> PrefabIndex:
    root = prefab.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError("Prefab RootObject must be a GameObject")
    nodes: dict[str, NodeRecord] = {}
    components: dict[str, dict[str, Any]] = {}
    owners: dict[str, str] = {}
    seen: set[str] = set()

    def add_guid(value: object, context: str) -> str:
        if not isinstance(value, str) or not value:
            raise ContractError(f"{context} has no GUID")
        if value in seen:
            raise ContractError(f"Duplicate GUID {value} at {context}")
        seen.add(value)
        return value

    def visit(node: object, parent_guid: str | None, parent_path: str) -> None:
        if not isinstance(node, dict):
            raise ContractError(f"Child beneath {parent_path or '<root>'} is not an object")
        name = node.get("Name")
        if not isinstance(name, str) or not name:
            raise ContractError("Every GameObject must have a nonempty Name")
        path = name if not parent_path else f"{parent_path}/{name}"
        guid = add_guid(node.get("__guid"), f"GameObject {path}")
        node_components = node.get("Components")
        children = node.get("Children")
        if not isinstance(node_components, list) or not isinstance(children, list):
            raise ContractError(f"GameObject {path} must have component and child arrays")
        nodes[guid] = NodeRecord(node=node, path=path, parent_guid=parent_guid)
        for component in node_components:
            if not isinstance(component, dict):
                raise ContractError(f"Component on {path} is not an object")
            component_guid = add_guid(
                component.get("__guid"), f"component on {path}"
            )
            components[component_guid] = component
            owners[component_guid] = guid
        for child in children:
            visit(child, guid, path)

    visit(root, None, "")
    index = PrefabIndex(
        nodes_by_guid=nodes,
        components_by_guid=components,
        component_owners=owners,
        all_guids=frozenset(seen),
    )
    _validate_references(prefab, index)
    return index


def _reference_counter(value: object) -> Counter[tuple[object, ...]]:
    references: Counter[tuple[object, ...]] = Counter()
    for item in _walk_json(value):
        if item.get("_type") == "gameobject":
            references[("gameobject", item.get("go"))] += 1
        elif item.get("_type") == "component":
            references[
                (
                    "component",
                    item.get("component_id"),
                    item.get("go"),
                    item.get("component_type"),
                )
            ] += 1
    return references


def _validate_references(prefab: object, index: PrefabIndex) -> None:
    for item in _walk_json(prefab):
        if item.get("_type") == "gameobject":
            target = item.get("go")
            if target not in index.nodes_by_guid:
                raise ContractError(f"Dangling GameObject reference: {target!r}")
        elif item.get("_type") == "component":
            component_id = item.get("component_id")
            owner = item.get("go")
            if component_id not in index.components_by_guid:
                raise ContractError(f"Dangling component reference: {component_id!r}")
            if owner != index.component_owners[component_id]:
                raise ContractError(
                    f"Component reference {component_id!r} owner drifted: "
                    f"expected {index.component_owners[component_id]!r}, got {owner!r}"
                )


def _is_descendant_or_self(
    index: PrefabIndex, candidate_guid: str, ancestor_guid: str
) -> bool:
    current: str | None = candidate_guid
    visited: set[str] = set()
    while current is not None:
        if current == ancestor_guid:
            return True
        if current in visited:
            raise ContractError(f"GameObject parent cycle includes {current}")
        visited.add(current)
        record = index.nodes_by_guid.get(current)
        if record is None:
            raise ContractError(f"Missing GameObject while walking ancestry: {current}")
        current = record.parent_guid
    return False


def _validate_source_contract(
    prefab: dict[str, Any], contract: WeaponContract
) -> tuple[PrefabIndex, dict[str, Any], int]:
    index = _index_prefab(prefab)
    root_object = prefab.get("RootObject")
    if not isinstance(root_object, dict) or root_object.get("Name") != contract.root_name:
        raise ContractError(
            f"{contract.family} root path changed: expected {contract.root_name!r}"
        )

    view_model = index.components_by_guid.get(contract.view_model_guid)
    if view_model is None:
        raise ContractError(f"{contract.family} ViewModel GUID changed")
    if view_model.get("__type") != VIEW_MODEL_TYPE:
        raise ContractError(f"{contract.family} ViewModel component type changed")
    vm_owner_guid = index.component_owners[contract.view_model_guid]
    if index.nodes_by_guid[vm_owner_guid].path != contract.view_model_owner_path:
        raise ContractError(f"{contract.family} ViewModel owner path changed")
    all_view_models = [
        value
        for value in index.components_by_guid.values()
        if value.get("__type") == VIEW_MODEL_TYPE
    ]
    if len(all_view_models) != 1:
        raise ContractError(
            f"{contract.family} must contain exactly one ViewModel component"
        )
    if "AdditionalRendererRoot" in view_model:
        raise ContractError(
            f"{contract.family} AdditionalRendererRoot already exists or drifted"
        )

    visibility_root = index.nodes_by_guid.get(contract.visibility_root_guid)
    if visibility_root is None or visibility_root.path != contract.visibility_root_path:
        raise ContractError(f"{contract.family} visibility-root path/GUID changed")

    observed_custom = {
        guid
        for guid, component in index.components_by_guid.items()
        if isinstance(component.get("Model"), str)
        and component["Model"].startswith(contract.renderer_prefix)
    }
    expected_custom = {renderer.guid for renderer in contract.renderers}
    if observed_custom != expected_custom:
        raise ContractError(
            f"{contract.family} custom-renderer GUID set changed: "
            f"expected {sorted(expected_custom)}, got {sorted(observed_custom)}"
        )
    for renderer in contract.renderers:
        component = index.components_by_guid.get(renderer.guid)
        if component is None:
            raise ContractError(f"{contract.family} renderer {renderer.guid} is missing")
        owner_guid = index.component_owners[renderer.guid]
        owner_path = index.nodes_by_guid[owner_guid].path
        if (
            component.get("__type") != renderer.component_type
            or component.get("Model") != renderer.model
            or owner_path != renderer.owner_path
        ):
            raise ContractError(
                f"{contract.family} renderer contract drifted for {renderer.guid}"
            )
        if not _is_descendant_or_self(
            index, owner_guid, contract.visibility_root_guid
        ):
            raise ContractError(
                f"{contract.family} renderer {renderer.guid} escaped visibility root"
            )
    return index, view_model, sum(_reference_counter(prefab).values())


def _serialize_prefab(prefab: dict[str, Any], source_bytes: bytes) -> bytes:
    newline = "\r\n" if b"\r\n" in source_bytes else "\n"
    text = json.dumps(prefab, ensure_ascii=False, indent=2) + "\n"
    if newline != "\n":
        text = text.replace("\n", newline)
    output = text.encode("utf-8")
    _decode_json(output, "rendered candidate")
    return output


def _read_pinned_input(pin: InputPin, contract: WeaponContract) -> tuple[Path, bytes]:
    path = _absolute(pin.path)
    if contract.source_kind == "active":
        if contract.active_path is None:
            raise ContractError(f"{contract.family} active path contract is incomplete")
        expected_path = contract.active_path.resolve(strict=True)
        try:
            actual_path = path.resolve(strict=True)
        except OSError as exc:
            raise ContractError(f"Cannot resolve {contract.family} input: {exc}") from exc
        if actual_path != expected_path:
            raise ContractError(f"{contract.family} active input path changed")
    else:
        try:
            actual_path = path.resolve(strict=True)
        except OSError as exc:
            raise ContractError(f"Cannot resolve {contract.family} input: {exc}") from exc
        if actual_path == SYSTEM_TEMP_ROOT or not _is_within(
            actual_path, SYSTEM_TEMP_ROOT
        ):
            raise ContractError(
                f"{contract.family} generated input must be a strict child of system TEMP"
            )
        try:
            pipeline_io._assert_no_reparse_components(actual_path)
        except RuntimeError as exc:
            raise ContractError(str(exc)) from exc
    result = actual_path.lstat()
    if not stat.S_ISREG(result.st_mode):
        raise ContractError(f"{contract.family} input is not a regular file")
    raw = actual_path.read_bytes()
    actual_sha = _sha256(raw)
    if len(raw) != pin.expected_bytes or actual_sha != pin.expected_sha256:
        raise ContractError(
            f"{contract.family} input byte pin mismatch: expected "
            f"{pin.expected_bytes}/{pin.expected_sha256}, got {len(raw)}/{actual_sha}"
        )
    return actual_path, raw


def _active_pin(contract: WeaponContract) -> InputPin:
    if (
        contract.active_path is None
        or contract.active_bytes is None
        or contract.active_sha256 is None
    ):
        raise ContractError(f"{contract.family} active pin contract is incomplete")
    return InputPin(
        path=contract.active_path,
        expected_bytes=contract.active_bytes,
        expected_sha256=contract.active_sha256,
    )


def load_generated_input_manifest(path: Path) -> dict[str, InputPin]:
    manifest_path = _absolute(path)
    raw = manifest_path.read_bytes()
    root = _require_exact_keys(
        _decode_json(raw, "generated-input manifest"),
        {"version", "generated_inputs"},
        "generated-input manifest",
    )
    if root["version"] != 1:
        raise ContractError("generated-input manifest version must be 1")
    inputs = _require_exact_keys(
        root["generated_inputs"],
        set(GENERATED_FAMILIES),
        "generated_inputs",
    )
    result: dict[str, InputPin] = {}
    for family in sorted(GENERATED_FAMILIES):
        item = _require_exact_keys(
            inputs[family], {"path", "bytes", "sha256"}, f"{family} input pin"
        )
        value_path = item["path"]
        if not isinstance(value_path, str) or value_path != value_path.strip():
            raise ContractError(f"{family} input path must be an exact string")
        parsed_path = Path(value_path)
        if not parsed_path.is_absolute():
            raise ContractError(f"{family} input path must be absolute")
        result[family] = InputPin(
            path=parsed_path,
            expected_bytes=_parse_positive_bytes(item["bytes"], f"{family} bytes"),
            expected_sha256=_parse_sha256(item["sha256"], f"{family} SHA-256"),
        )
    return result


def _pins_for_build(generated_inputs: Mapping[str, InputPin]) -> dict[str, InputPin]:
    if set(generated_inputs) != set(GENERATED_FAMILIES):
        raise ContractError(
            "generated inputs must contain exactly "
            f"{sorted(GENERATED_FAMILIES)}"
        )
    pins: dict[str, InputPin] = {}
    for contract in WEAPON_CONTRACTS:
        if contract.source_kind == "active":
            pins[contract.family] = _active_pin(contract)
        else:
            pin = generated_inputs[contract.family]
            pins[contract.family] = InputPin(
                path=Path(pin.path),
                expected_bytes=_parse_positive_bytes(
                    pin.expected_bytes, f"{contract.family} bytes"
                ),
                expected_sha256=_parse_sha256(
                    pin.expected_sha256, f"{contract.family} SHA-256"
                ),
            )
    return pins


def render_candidates(
    generated_inputs: Mapping[str, InputPin],
) -> tuple[RenderedCandidate, ...]:
    pins = _pins_for_build(generated_inputs)
    rendered: list[RenderedCandidate] = []
    for contract in WEAPON_CONTRACTS:
        source_path, source_bytes = _read_pinned_input(pins[contract.family], contract)
        source = _decode_json(source_bytes, f"{contract.family} prefab")
        before_index, _, original_reference_count = _validate_source_contract(
            source, contract
        )
        candidate = copy.deepcopy(source)
        candidate_index, candidate_view_model, _ = _validate_source_contract(
            candidate, contract
        )
        if candidate_index.all_guids != before_index.all_guids:
            raise ContractError(f"{contract.family} GUID set drifted before patch")
        candidate_view_model["AdditionalRendererRoot"] = {
            "_type": "gameobject",
            "go": contract.visibility_root_guid,
        }

        after_index = _index_prefab(candidate)
        if after_index.all_guids != before_index.all_guids:
            raise ContractError(f"{contract.family} GUID set changed during patch")
        stripped = copy.deepcopy(candidate)
        stripped_index = _index_prefab(stripped)
        stripped_vm = stripped_index.components_by_guid[contract.view_model_guid]
        removed = stripped_vm.pop("AdditionalRendererRoot", None)
        expected_reference = {
            "_type": "gameobject",
            "go": contract.visibility_root_guid,
        }
        if removed != expected_reference or stripped != source:
            raise ContractError(
                f"{contract.family} candidate changed more than AdditionalRendererRoot"
            )
        expected_references = _reference_counter(source)
        expected_references[("gameobject", contract.visibility_root_guid)] += 1
        if _reference_counter(candidate) != expected_references:
            raise ContractError(f"{contract.family} existing references drifted")

        output_bytes = _serialize_prefab(candidate, source_bytes)
        reparsed = _decode_json(output_bytes, f"{contract.family} serialized candidate")
        reparsed_index = _index_prefab(reparsed)
        reparsed_vm = reparsed_index.components_by_guid[contract.view_model_guid]
        if reparsed_vm.get("AdditionalRendererRoot") != expected_reference:
            raise ContractError(
                f"{contract.family} serialized visibility-root reference drifted"
            )
        for renderer in contract.renderers:
            owner = reparsed_index.component_owners[renderer.guid]
            if not _is_descendant_or_self(
                reparsed_index, owner, contract.visibility_root_guid
            ):
                raise ContractError(
                    f"{contract.family} serialized renderer escaped visibility root"
                )
        rendered.append(
            RenderedCandidate(
                contract=contract,
                source_path=source_path,
                source_bytes=source_bytes,
                output_bytes=output_bytes,
                output_sha256=_sha256(output_bytes),
                original_reference_count=original_reference_count,
            )
        )
    return tuple(rendered)


def _verified_output_directory(output_directory: Path | None) -> Path:
    if output_directory is None:
        created = Path(tempfile.mkdtemp(prefix=TEMP_PREFIX)).resolve(strict=True)
        return created
    requested = _absolute(output_directory)
    if not requested.exists():
        raise ContractError("caller-supplied output directory must already exist")
    if not requested.is_dir():
        raise ContractError("output path exists and is not a directory")
    resolved = requested.resolve(strict=True)
    try:
        pipeline_io.assert_temp_destination(resolved / ".visibility-root-probe")
    except RuntimeError as exc:
        raise ContractError(str(exc)) from exc
    if any(resolved.iterdir()):
        raise ContractError("TEMP-only output directory must be empty")
    return resolved


def _write_create_only(
    output_directory: Path,
    rendered: Sequence[RenderedCandidate],
) -> tuple[Path, ...]:
    finals = [output_directory / item.contract.output_name for item in rendered]
    if len(finals) != len(set(finals)):
        raise ContractError("duplicate visibility-root output destination")
    if any(final.exists() or final.is_symlink() for final in finals):
        raise ContractError("visibility-root output already exists")

    pairs: list[tuple[Path, Path]] = []
    try:
        for item, final in zip(rendered, finals):
            try:
                final = pipeline_io.assert_temp_destination(final)
                staged = pipeline_io.staging_path(final)
            except RuntimeError as exc:
                raise ContractError(str(exc)) from exc
            # staging_path captured a missing final.  This immediate check
            # closes the pre-registration arrival gap; promote_files closes
            # every later arrival race against the registered snapshot.
            if final.exists() or final.is_symlink():
                raise ContractError(
                    "visibility-root output appeared during create-only staging"
                )
            with staged.open("xb") as stream:
                stream.write(item.output_bytes)
                stream.flush()
                os.fsync(stream.fileno())
            if staged.read_bytes() != item.output_bytes:
                raise ContractError("staged visibility-root bytes changed")
            pairs.append((staged, final))
        try:
            pipeline_io.promote_files(pairs)
        except RuntimeError as exc:
            raise ContractError(str(exc)) from exc
    finally:
        for staged, _ in pairs:
            if staged.exists() and not staged.is_symlink():
                try:
                    staged.unlink()
                except OSError as exc:
                    print(
                        "DXRP_VISIBILITY_ROOT_CLEANUP_WARNING: staged output remains "
                        f"at {staged}: {exc}",
                        file=sys.stderr,
                    )
            if not staged.exists() and not staged.is_symlink():
                pipeline_io._staged_files.discard(staged)
                pipeline_io._file_destinations.pop(staged, None)
    for item, final in zip(rendered, finals):
        if final.read_bytes() != item.output_bytes:
            raise ContractError(f"promoted bytes changed for {item.contract.family}")
    return tuple(finals)


def _source_snapshots_unchanged(rendered: Sequence[RenderedCandidate]) -> None:
    for item in rendered:
        if item.source_path.read_bytes() != item.source_bytes:
            raise ContractError(
                f"{item.contract.family} input changed while building candidates"
            )


def _report(
    rendered: Sequence[RenderedCandidate],
    outputs: Sequence[Path] | None,
) -> dict[str, object]:
    output_by_family = (
        {item.contract.family: path for item, path in zip(rendered, outputs)}
        if outputs is not None
        else {}
    )
    return {
        "result": "PASS",
        "mode": "TEMP_ONLY_CREATE_ONLY_NO_PRODUCT_WRITE_MODE",
        "wrote_files": outputs is not None,
        "candidate_count": len(rendered),
        "candidates": [
            {
                "family": item.contract.family,
                "source_kind": item.contract.source_kind,
                "source": {
                    "path": str(item.source_path),
                    "bytes": len(item.source_bytes),
                    "sha256": _sha256(item.source_bytes),
                },
                "output": {
                    "path": str(output_by_family[item.contract.family])
                    if outputs is not None
                    else None,
                    "file": item.contract.output_name,
                    "bytes": len(item.output_bytes),
                    "sha256": item.output_sha256,
                },
                "view_model_guid": item.contract.view_model_guid,
                "additional_renderer_root": item.contract.visibility_root_guid,
                "custom_renderer_guids": [
                    renderer.guid for renderer in item.contract.renderers
                ],
                "all_custom_renderers_descend_from_root": True,
                "original_reference_count": item.original_reference_count,
                "semantic_change": "add AdditionalRendererRoot only",
            }
            for item in rendered
        ],
        "writes": {
            "temp_only": outputs is not None,
            "game": False,
            "docs": False,
            "portal": False,
            "git": False,
            "scene": False,
        },
        "proof_ceiling": [
            "exact input byte custody",
            "static GUID/type/path/renderer/reference closure",
            "deterministic TEMP candidate serialization",
            "UNVERIFIED editor asset compile",
            "UNVERIFIED runtime visibility",
            "UNVERIFIED rendered first-person and ADS behavior",
            "UNVERIFIED Portal delivery",
        ],
    }


def build_candidates(
    generated_inputs: Mapping[str, InputPin],
    *,
    output_directory: Path | None = None,
) -> dict[str, object]:
    rendered = render_candidates(generated_inputs)
    _source_snapshots_unchanged(rendered)
    output = _verified_output_directory(output_directory)
    try:
        paths = _write_create_only(output, rendered)
        _source_snapshots_unchanged(rendered)
    except BaseException:
        if output_directory is None and output.exists() and not any(output.iterdir()):
            output.rmdir()
        raise
    return _report(rendered, paths)


def check_candidates(generated_inputs: Mapping[str, InputPin]) -> dict[str, object]:
    rendered = render_candidates(generated_inputs)
    _source_snapshots_unchanged(rendered)
    return _report(rendered, None)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build five exact, create-only TEMP ViewModel visibility-root candidates. "
            "No product-write mode exists."
        )
    )
    parser.add_argument(
        "--generated-inputs-json",
        type=Path,
        required=True,
        help="Exact pins for the generated AKS-74U, Desert Eagle, and M1911 inputs.",
    )
    parser.add_argument(
        "--output-directory",
        type=Path,
        help="Existing empty strict-system-TEMP directory; omitted for a fresh one.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Render and report hashes without creating any output directory.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if args.check and args.output_directory is not None:
        raise ContractError("--check cannot be combined with --output-directory")
    pins = load_generated_input_manifest(args.generated_inputs_json)
    report = (
        check_candidates(pins)
        if args.check
        else build_candidates(pins, output_directory=args.output_directory)
    )
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ContractError as exc:
        print(f"VISIBILITY_ROOT_CONTRACT_ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
