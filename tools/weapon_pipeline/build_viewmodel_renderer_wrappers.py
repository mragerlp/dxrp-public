"""Build temporary AR-15 and SR-25 first-person renderer-wrapper candidates.

The native weapon parts were exported around one shared weapon origin.  A
custom ``ModelRenderer`` therefore cannot safely live directly on an animated
donor-bone GameObject: the bone transform is applied to common-origin geometry.
This builder moves each custom renderer, unchanged, to a new child GameObject.

With no transform manifest, every wrapper is identity.  That mode is useful
only for temporary hierarchy/reference proof; it does not fix the visible
common-origin animation fault.  A manifest value is the *complete wrapper-local
transform* ``W``, not merely the inverse donor transform.  For a donor idle
local transform ``P`` and an accepted custom-weapon fit ``B``, including its
uniform body scale, product values must be composed as ``W = inverse(P) * B``.
The builder validates manifest shape and coverage, but cannot prove that the
supplied numbers came from measured ``P`` and accepted ``B`` values.

Manifest schema (paths are exact prefab GameObject paths)::

    {
      "version": 1,
      "measured_and_accepted": true,
      "pins": {
        "ar15": {
          "source_path": "D:/.../vm_ar15.prefab",
          "source_sha256": "64 hex characters",
          "output_path": "D:/.../vm_ar15.wrapper_candidate.prefab",
          "output_sha256": "64 hex characters"
        }
      },
      "families": {
        "ar15": {
          "vm_ar15/weapon_root/weapon_root_children/magazine": {
            "position": "x,y,z",
            "quaternion": "x,y,z,w",
            "scale": "x,y,z"
          }
        },
        "sr25": {}
      }
    }

An external manifest used for temporary output must cover every common-origin
moving part.  Static parts may be omitted and then receive identity wrappers.
Output defaults to a fresh system-temporary directory.  This helper is
deliberately TEMP-only: every output must remain beneath the operating-system
temporary directory and outside the workbench.  It exposes no product-write
switch.  A measured/accepted marker and pins can travel with a manifest as
evidence, but they never authorize this tool to alter ``game/Assets``.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterator, Mapping, Sequence

import pipeline_io


WORKSPACE_ROOT = Path(__file__).resolve().parents[2]
GAME_ASSETS_ROOT = WORKSPACE_ROOT / "game" / "Assets"
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
WRAPPER_NAMESPACE = uuid.UUID("a91788dd-4fe3-5a51-b691-c5c928e5531f")


class ContractError(RuntimeError):
    """Raised when an input or requested output violates the narrow contract."""


@dataclass(frozen=True)
class RendererNodeSpec:
    path: str
    model: str
    moving_part: bool


@dataclass(frozen=True)
class PrefabSpec:
    family: str
    root_name: str
    default_source: Path
    output_name: str
    renderer_nodes: tuple[RendererNodeSpec, ...]

    @property
    def model_prefix(self) -> str:
        return f"addons/lifepunch/lpweapons/{self.family}/"

    @property
    def moving_paths(self) -> frozenset[str]:
        return frozenset(node.path for node in self.renderer_nodes if node.moving_part)


def _node(
    family: str, root: str, name: str, *, moving: bool = True
) -> RendererNodeSpec:
    base = f"{root}/weapon_root/weapon_root_children"
    path = base if not name else f"{base}/{name}"
    model_name = "body" if not name else name.removeprefix(f"{family}_")
    return RendererNodeSpec(
        path=path,
        model=f"addons/lifepunch/lpweapons/{family}/models/native_candidate/{family}_{model_name}.vmdl",
        moving_part=moving,
    )


PREFAB_SPECS: tuple[PrefabSpec, ...] = (
    PrefabSpec(
        family="ar15",
        root_name="vm_ar15",
        default_source=(
            WORKSPACE_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab"
        ),
        output_name="vm_ar15.wrapper_candidate.prefab",
        renderer_nodes=(
            _node("ar15", "vm_ar15", "", moving=False),
            _node("ar15", "vm_ar15", "stock"),
            _node("ar15", "vm_ar15", "trigger"),
            _node("ar15", "vm_ar15", "magazine"),
            _node("ar15", "vm_ar15", "bolt_flap"),
            _node("ar15", "vm_ar15", "bolt"),
            _node("ar15", "vm_ar15", "charging_handle"),
        ),
    ),
    PrefabSpec(
        family="sr25",
        root_name="vm_sr25",
        default_source=(
            WORKSPACE_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab"
        ),
        output_name="vm_sr25.wrapper_candidate.prefab",
        renderer_nodes=(
            _node("sr25", "vm_sr25", "", moving=False),
            _node("sr25", "vm_sr25", "stock"),
            _node("sr25", "vm_sr25", "trigger"),
            _node("sr25", "vm_sr25", "magazine"),
            _node("sr25", "vm_sr25", "mode_selector"),
            _node("sr25", "vm_sr25", "bolt_flap"),
            _node("sr25", "vm_sr25", "bolt"),
            _node("sr25", "vm_sr25", "charging_handle"),
            _node("sr25", "vm_sr25", "sr25_scope", moving=False),
            _node("sr25", "vm_sr25", "sr25_scope_mount", moving=False),
            _node("sr25", "vm_sr25", "sr25_suppressor", moving=False),
        ),
    ),
)


@dataclass(frozen=True)
class WrapperTransform:
    position: str
    quaternion: str
    scale: str

    @property
    def is_identity(self) -> bool:
        position = _parse_vector(self.position, 3, "position")
        quaternion = _parse_vector(self.quaternion, 4, "quaternion")
        scale = _parse_vector(self.scale, 3, "scale")
        return (
            _vector_close(position, (0.0, 0.0, 0.0))
            and _quaternion_equivalent(quaternion, (0.0, 0.0, 0.0, 1.0))
            and _vector_close(scale, (1.0, 1.0, 1.0))
        )


IDENTITY_TRANSFORM = WrapperTransform(
    position="0,0,0", quaternion="0,0,0,1", scale="1,1,1"
)


@dataclass(frozen=True)
class ProductPin:
    source_path: str
    source_sha256: str
    output_path: str
    output_sha256: str


@dataclass(frozen=True)
class TransformManifest:
    families: Mapping[str, Mapping[str, WrapperTransform]]
    measured_and_accepted: bool | None
    pins: Mapping[str, ProductPin]
    path: Path | None
    sha256: str | None


@dataclass(frozen=True)
class NodeRecord:
    node: dict[str, Any]
    path: str
    parent_guid: str | None


@dataclass(frozen=True)
class PrefabIndex:
    nodes_by_guid: Mapping[str, NodeRecord]
    nodes_by_path: Mapping[str, tuple[NodeRecord, ...]]
    components_by_guid: Mapping[str, dict[str, Any]]
    component_owners: Mapping[str, str]
    all_guids: frozenset[str]


@dataclass(frozen=True)
class RendererMatch:
    contract: RendererNodeSpec
    donor: NodeRecord
    renderer: dict[str, Any]


def _parse_vector(value: object, length: int, label: str) -> tuple[float, ...]:
    if not isinstance(value, str) or not value.strip():
        raise ContractError(f"Manifest {label} must be a non-empty comma string")
    parts = [part.strip() for part in value.split(",")]
    if len(parts) != length:
        raise ContractError(
            f"Manifest {label} must have {length} comma-separated values: {value!r}"
        )
    try:
        parsed = tuple(float(part) for part in parts)
    except ValueError as exc:
        raise ContractError(f"Manifest {label} is not numeric: {value!r}") from exc
    if not all(math.isfinite(item) for item in parsed):
        raise ContractError(f"Manifest {label} must contain only finite values: {value!r}")
    return parsed


def _vector_close(left: Sequence[float], right: Sequence[float]) -> bool:
    return all(math.isclose(a, b, abs_tol=1e-9) for a, b in zip(left, right))


def _quaternion_equivalent(left: Sequence[float], right: Sequence[float]) -> bool:
    return _vector_close(left, right) or _vector_close(left, tuple(-item for item in right))


def _parse_transform(value: object, context: str) -> WrapperTransform:
    if not isinstance(value, dict) or not value:
        raise ContractError(f"Manifest transform for {context} must be a non-empty object")
    expected = {"position", "quaternion", "scale"}
    actual = set(value)
    if actual != expected:
        raise ContractError(
            f"Manifest transform for {context} must contain exactly "
            f"{sorted(expected)}, got {sorted(actual)}"
        )
    transform = WrapperTransform(
        position=str(value["position"]).strip(),
        quaternion=str(value["quaternion"]).strip(),
        scale=str(value["scale"]).strip(),
    )
    _parse_vector(transform.position, 3, f"{context}.position")
    quaternion = _parse_vector(transform.quaternion, 4, f"{context}.quaternion")
    scale = _parse_vector(transform.scale, 3, f"{context}.scale")
    quaternion_norm = math.sqrt(sum(item * item for item in quaternion))
    if not math.isclose(quaternion_norm, 1.0, rel_tol=0.0, abs_tol=1e-6):
        raise ContractError(
            f"Manifest quaternion for {context} must be normalized; "
            f"norm is {quaternion_norm:.12g}"
        )
    if any(item <= 0.0 for item in scale):
        raise ContractError(f"Manifest scale for {context} must be positive")
    if not all(
        math.isclose(item, scale[0], rel_tol=0.0, abs_tol=1e-9)
        for item in scale[1:]
    ):
        raise ContractError(f"Manifest scale for {context} must be uniform")
    return transform


def _parse_sha256(value: object, context: str) -> str:
    if not isinstance(value, str):
        raise ContractError(f"{context} must be a SHA-256 string")
    normalized = value.strip().upper()
    if len(normalized) != 64 or any(
        character not in "0123456789ABCDEF" for character in normalized
    ):
        raise ContractError(f"{context} must be exactly 64 hexadecimal characters")
    return normalized


def _parse_product_pin(value: object, context: str) -> ProductPin:
    if not isinstance(value, dict):
        raise ContractError(f"{context} must be an object")
    expected = {"source_path", "source_sha256", "output_path", "output_sha256"}
    if set(value) != expected:
        raise ContractError(
            f"{context} must contain exactly {sorted(expected)}, got {sorted(value)}"
        )
    source_path = value["source_path"]
    output_path = value["output_path"]
    if not isinstance(source_path, str) or not source_path.strip():
        raise ContractError(f"{context}.source_path must be a non-empty string")
    if not isinstance(output_path, str) or not output_path.strip():
        raise ContractError(f"{context}.output_path must be a non-empty string")
    if not Path(source_path).is_absolute() or not Path(output_path).is_absolute():
        raise ContractError(f"{context} source_path and output_path must be absolute")
    return ProductPin(
        source_path=source_path.strip(),
        source_sha256=_parse_sha256(value["source_sha256"], f"{context}.source_sha256"),
        output_path=output_path.strip(),
        output_sha256=_parse_sha256(value["output_sha256"], f"{context}.output_sha256"),
    )


def load_transform_manifest(path: Path | None) -> TransformManifest:
    if path is None:
        return TransformManifest(
            families={}, measured_and_accepted=None, pins={}, path=None, sha256=None
        )
    try:
        manifest_bytes = path.read_bytes()
        payload = json.loads(manifest_bytes.decode("utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Cannot read transform manifest {path}: {exc}") from exc
    if not isinstance(payload, dict) or payload.get("version") != 1:
        raise ContractError("Transform manifest must be an object with version 1")
    expected_top_level = {"version", "families", "measured_and_accepted", "pins"}
    unknown_top_level = set(payload) - expected_top_level
    if unknown_top_level:
        raise ContractError(
            f"Transform manifest has unknown top-level fields: {sorted(unknown_top_level)}"
        )
    families = payload.get("families")
    if not isinstance(families, dict):
        raise ContractError("Transform manifest must contain a families object")
    known_families = {spec.family for spec in PREFAB_SPECS}
    unknown_families = set(families) - known_families
    if unknown_families:
        raise ContractError(
            f"Transform manifest has unknown families: {sorted(unknown_families)}"
        )
    parsed_families: dict[str, dict[str, WrapperTransform]] = {}
    for family, values in families.items():
        if not isinstance(values, dict):
            raise ContractError(f"Manifest family {family} must be an object")
        parsed_families[family] = {
            node_path: _parse_transform(transform, f"{family}:{node_path}")
            for node_path, transform in values.items()
        }

    marker = payload.get("measured_and_accepted")
    if marker is not None and not isinstance(marker, bool):
        raise ContractError("measured_and_accepted must be a JSON boolean when present")
    raw_pins = payload.get("pins", {})
    if not isinstance(raw_pins, dict):
        raise ContractError("Transform manifest pins must be an object")
    unknown_pin_families = set(raw_pins) - known_families
    if unknown_pin_families:
        raise ContractError(
            f"Transform manifest has pins for unknown families: {sorted(unknown_pin_families)}"
        )
    parsed_pins = {
        family: _parse_product_pin(value, f"pins.{family}")
        for family, value in raw_pins.items()
    }
    return TransformManifest(
        families=parsed_families,
        measured_and_accepted=marker,
        pins=parsed_pins,
        path=_normalized_absolute(path),
        sha256=hashlib.sha256(manifest_bytes).hexdigest().upper(),
    )


def _iter_nodes(
    node: object, path: str = "", parent_guid: str | None = None
) -> Iterator[NodeRecord]:
    if not isinstance(node, dict):
        raise ContractError("Every prefab GameObject must be a JSON object")
    name = node.get("Name")
    guid = node.get("__guid")
    if not isinstance(name, str) or not name:
        raise ContractError("Every prefab GameObject must have a non-empty Name")
    if not isinstance(guid, str) or not guid:
        raise ContractError(f"GameObject {name!r} has no valid __guid")
    node_path = f"{path}/{name}" if path else name
    record = NodeRecord(node=node, path=node_path, parent_guid=parent_guid)
    yield record
    children = node.get("Children")
    if not isinstance(children, list):
        raise ContractError(f"GameObject {node_path} must have a Children array")
    for child in children:
        yield from _iter_nodes(child, node_path, guid)


def _add_guid(
    guid: object, owner: str, seen: dict[str, str], *, expected_kind: str
) -> str:
    if not isinstance(guid, str) or not guid:
        raise ContractError(f"{expected_kind} {owner} has no valid __guid")
    try:
        uuid.UUID(guid)
    except ValueError as exc:
        raise ContractError(f"{expected_kind} {owner} has invalid GUID {guid!r}") from exc
    previous = seen.get(guid)
    if previous is not None:
        raise ContractError(f"Duplicate GUID {guid}: {previous} and {owner}")
    seen[guid] = owner
    return guid


def index_prefab(prefab: object) -> PrefabIndex:
    if not isinstance(prefab, dict) or "RootObject" not in prefab:
        raise ContractError("Prefab must be an object containing RootObject")
    seen: dict[str, str] = {}
    nodes_by_guid: dict[str, NodeRecord] = {}
    paths: dict[str, list[NodeRecord]] = {}
    components: dict[str, dict[str, Any]] = {}
    owners: dict[str, str] = {}
    for record in _iter_nodes(prefab["RootObject"]):
        guid = _add_guid(record.node.get("__guid"), record.path, seen, expected_kind="GameObject")
        nodes_by_guid[guid] = record
        paths.setdefault(record.path, []).append(record)
        node_components = record.node.get("Components")
        if not isinstance(node_components, list):
            raise ContractError(f"GameObject {record.path} must have a Components array")
        for component in node_components:
            if not isinstance(component, dict):
                raise ContractError(f"GameObject {record.path} contains a non-object component")
            component_type = component.get("__type", "<unknown>")
            component_guid = _add_guid(
                component.get("__guid"),
                f"{record.path}:{component_type}",
                seen,
                expected_kind="Component",
            )
            components[component_guid] = component
            owners[component_guid] = guid
    index = PrefabIndex(
        nodes_by_guid=nodes_by_guid,
        nodes_by_path={key: tuple(value) for key, value in paths.items()},
        components_by_guid=components,
        component_owners=owners,
        all_guids=frozenset(seen),
    )
    validate_references(prefab, index)
    return index


def _walk_json(value: object) -> Iterator[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_json(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_json(child)


def validate_references(prefab: object, index: PrefabIndex | None = None) -> None:
    index = index or index_prefab(prefab)
    for value in _walk_json(prefab):
        reference_type = value.get("_type")
        if reference_type == "gameobject":
            target = value.get("go")
            if target not in index.nodes_by_guid:
                raise ContractError(f"Dangling GameObject reference: {target!r}")
        elif reference_type == "component":
            component_id = value.get("component_id")
            target_go = value.get("go")
            if component_id not in index.components_by_guid:
                raise ContractError(f"Dangling component reference: {component_id!r}")
            expected_go = index.component_owners[component_id]
            if target_go != expected_go:
                raise ContractError(
                    f"Component reference {component_id} names owner {target_go!r}; "
                    f"actual owner is {expected_go}"
                )


def _view_model_contract(prefab: object) -> dict[str, Any]:
    index = index_prefab(prefab)
    matches = [
        component
        for component in index.components_by_guid.values()
        if component.get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    if len(matches) != 1:
        raise ContractError(f"Expected exactly one ViewModel component, found {len(matches)}")
    view_model = matches[0]
    return {
        "CanADS_present": "CanADS" in view_model,
        "CanADS": copy.deepcopy(view_model.get("CanADS")),
        "AdditionalRendererRoot_present": "AdditionalRendererRoot" in view_model,
        "AdditionalRendererRoot": copy.deepcopy(view_model.get("AdditionalRendererRoot")),
    }


def _find_renderer_matches(prefab: object, spec: PrefabSpec) -> list[RendererMatch]:
    index = index_prefab(prefab)
    root = prefab["RootObject"]
    if root.get("Name") != spec.root_name:
        raise ContractError(
            f"{spec.family} root must be {spec.root_name!r}, got {root.get('Name')!r}"
        )
    custom_renderers: list[tuple[NodeRecord, dict[str, Any]]] = []
    for record in index.nodes_by_guid.values():
        for component in record.node["Components"]:
            model = component.get("Model")
            if (
                component.get("__type") == "Sandbox.ModelRenderer"
                and isinstance(model, str)
                and model.startswith(spec.model_prefix)
            ):
                custom_renderers.append((record, component))

    expected_paths = {contract.path for contract in spec.renderer_nodes}
    observed_paths = {record.path for record, _ in custom_renderers}
    if observed_paths != expected_paths or len(custom_renderers) != len(spec.renderer_nodes):
        raise ContractError(
            f"{spec.family} custom-renderer paths changed: expected "
            f"{sorted(expected_paths)}, got {sorted(observed_paths)}"
        )

    by_path = {record.path: (record, component) for record, component in custom_renderers}
    matches: list[RendererMatch] = []
    for contract in spec.renderer_nodes:
        donor, renderer = by_path[contract.path]
        if renderer.get("Model") != contract.model:
            raise ContractError(
                f"{spec.family} renderer at {contract.path} changed model: "
                f"expected {contract.model}, got {renderer.get('Model')!r}"
            )
        matches.append(RendererMatch(contract=contract, donor=donor, renderer=renderer))
    return matches


def _hierarchy_snapshot(index: PrefabIndex) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for guid, record in index.nodes_by_guid.items():
        result[guid] = {
            "parent_guid": record.parent_guid,
            "children": tuple(child["__guid"] for child in record.node["Children"]),
            "Position": copy.deepcopy(record.node.get("Position")),
            "Rotation": copy.deepcopy(record.node.get("Rotation")),
            "Scale": copy.deepcopy(record.node.get("Scale")),
            "Name": record.node.get("Name"),
        }
    return result


def _next_wrapper_guid(
    family: str,
    root_guid: str,
    donor_guid: str,
    renderer_guid: str,
    used_guids: set[str],
) -> str:
    identity = f"{family}\0{root_guid}\0{donor_guid}\0{renderer_guid}\0renderer-wrapper-v1"
    attempt = 0
    while True:
        suffix = identity if attempt == 0 else f"{identity}\0{attempt}"
        candidate = str(uuid.uuid5(WRAPPER_NAMESPACE, suffix))
        if candidate not in used_guids:
            used_guids.add(candidate)
            return candidate
        attempt += 1


def _make_wrapper(
    donor: dict[str, Any],
    renderer: dict[str, Any],
    wrapper_guid: str,
    transform: WrapperTransform,
) -> dict[str, Any]:
    model = renderer.get("Model")
    model_stem = PurePosixPath(model).stem if isinstance(model, str) else "custom"
    return {
        "__guid": wrapper_guid,
        "__version": donor.get("__version", 2),
        "Flags": donor.get("Flags", 0),
        "Name": f"{model_stem}_renderer_wrapper",
        "Position": transform.position,
        "Rotation": transform.quaternion,
        "Scale": transform.scale,
        "Tags": donor.get("Tags", ""),
        "Enabled": donor.get("Enabled", True),
        "NetworkMode": donor.get("NetworkMode", 2),
        "NetworkFlags": donor.get("NetworkFlags", 0),
        "NetworkOrphaned": donor.get("NetworkOrphaned", 0),
        "NetworkTransmit": donor.get("NetworkTransmit", True),
        "OwnerTransfer": donor.get("OwnerTransfer", 1),
        "Components": [renderer],
        "Children": [],
    }


def _update_component_reference_owners(
    value: object, moved_owners: Mapping[str, tuple[str, str]]
) -> int:
    updates = 0
    for item in _walk_json(value):
        if item.get("_type") != "component":
            continue
        component_id = item.get("component_id")
        if component_id not in moved_owners:
            continue
        old_owner, new_owner = moved_owners[component_id]
        current_owner = item.get("go")
        if current_owner not in {old_owner, new_owner}:
            raise ContractError(
                f"Reference to moved component {component_id} has unexpected owner "
                f"{current_owner!r}"
            )
        if current_owner != new_owner:
            item["go"] = new_owner
            updates += 1
    return updates


def _resolve_transforms(
    spec: PrefabSpec,
    manifest: Mapping[str, Mapping[str, WrapperTransform]],
    *,
    external_manifest: bool,
) -> dict[str, WrapperTransform]:
    family_values = dict(manifest.get(spec.family, {}))
    known_paths = {node.path for node in spec.renderer_nodes}
    unknown_paths = set(family_values) - known_paths
    if unknown_paths:
        raise ContractError(
            f"{spec.family} manifest has unknown renderer paths: {sorted(unknown_paths)}"
        )
    if external_manifest:
        missing_moving = spec.moving_paths - set(family_values)
        if missing_moving:
            raise ContractError(
                f"{spec.family} manifest is missing common-origin moving parts: "
                f"{sorted(missing_moving)}"
            )
    return {
        node.path: family_values.get(node.path, IDENTITY_TRANSFORM)
        for node in spec.renderer_nodes
    }


def transform_prefab(
    source_prefab: object,
    spec: PrefabSpec,
    transforms: Mapping[str, WrapperTransform],
) -> tuple[dict[str, Any], dict[str, Any]]:
    candidate = copy.deepcopy(source_prefab)
    before_index = index_prefab(candidate)
    before_hierarchy = _hierarchy_snapshot(before_index)
    view_model_before = _view_model_contract(candidate)
    matches = _find_renderer_matches(candidate, spec)
    root_guid = candidate["RootObject"]["__guid"]
    used_guids = set(before_index.all_guids)
    new_wrapper_guids: set[str] = set()
    moved_owners: dict[str, tuple[str, str]] = {}
    expected_renderers: dict[str, dict[str, Any]] = {}
    wrapper_rows: list[dict[str, Any]] = []

    for match in matches:
        donor = match.donor.node
        renderer = match.renderer
        renderer_guid = renderer["__guid"]
        donor_guid = donor["__guid"]
        transform = transforms.get(match.contract.path)
        if transform is None:
            raise ContractError(f"No wrapper transform resolved for {match.contract.path}")
        wrapper_guid = _next_wrapper_guid(
            spec.family, root_guid, donor_guid, renderer_guid, used_guids
        )
        retained = [component for component in donor["Components"] if component is not renderer]
        if len(retained) != len(donor["Components"]) - 1:
            raise ContractError(f"Could not isolate renderer {renderer_guid} at {match.contract.path}")
        donor["Components"] = retained
        wrapper = _make_wrapper(donor, renderer, wrapper_guid, transform)
        donor["Children"] = list(donor["Children"]) + [wrapper]
        new_wrapper_guids.add(wrapper_guid)
        moved_owners[renderer_guid] = (donor_guid, wrapper_guid)
        expected_renderers[renderer_guid] = copy.deepcopy(renderer)
        wrapper_rows.append(
            {
                "donor_path": match.contract.path,
                "donor_guid": donor_guid,
                "renderer_guid": renderer_guid,
                "model": renderer["Model"],
                "wrapper_guid": wrapper_guid,
                "position": transform.position,
                "quaternion": transform.quaternion,
                "scale": transform.scale,
                "moving_part": match.contract.moving_part,
                "identity_transform": transform.is_identity,
            }
        )

    reference_updates = _update_component_reference_owners(candidate, moved_owners)
    for expected in expected_renderers.values():
        _update_component_reference_owners(expected, moved_owners)

    after_index = index_prefab(candidate)
    after_hierarchy = _hierarchy_snapshot(after_index)
    if view_model_before != _view_model_contract(candidate):
        raise ContractError(f"{spec.family} ViewModel CanADS/AdditionalRendererRoot drifted")
    if not before_index.all_guids.issubset(after_index.all_guids):
        raise ContractError(f"{spec.family} lost an existing GameObject or component GUID")
    if len(after_index.all_guids) != len(before_index.all_guids) + len(matches):
        raise ContractError(f"{spec.family} GUID cardinality did not grow by wrapper count")

    for guid, snapshot in before_hierarchy.items():
        current = after_hierarchy.get(guid)
        if current is None:
            raise ContractError(f"{spec.family} lost original GameObject {guid}")
        for field in ("parent_guid", "Position", "Rotation", "Scale", "Name"):
            if current[field] != snapshot[field]:
                raise ContractError(
                    f"{spec.family} original GameObject {guid} changed {field}: "
                    f"{snapshot[field]!r} -> {current[field]!r}"
                )
        original_children = snapshot["children"]
        retained_children = tuple(
            child for child in current["children"] if child not in new_wrapper_guids
        )
        if retained_children != original_children:
            raise ContractError(f"{spec.family} original child structure drifted at {guid}")

    for renderer_guid, expected in expected_renderers.items():
        actual = after_index.components_by_guid.get(renderer_guid)
        if actual != expected:
            raise ContractError(f"{spec.family} renderer {renderer_guid} changed while moving")
        expected_owner = moved_owners[renderer_guid][1]
        if after_index.component_owners.get(renderer_guid) != expected_owner:
            raise ContractError(f"{spec.family} renderer {renderer_guid} has wrong new owner")

    scope_guids = {
        guid
        for guid, snapshot in before_hierarchy.items()
        if "scope" in str(snapshot["Name"]).casefold()
    }
    report = {
        "family": spec.family,
        "renderers_moved": len(matches),
        "new_wrapper_guids": sorted(new_wrapper_guids),
        "reference_owner_updates": reference_updates,
        "existing_guid_count": len(before_index.all_guids),
        "candidate_guid_count": len(after_index.all_guids),
        "original_hierarchy_preserved": True,
        "donor_transforms_preserved": True,
        "renderer_components_preserved": True,
        "references_valid": True,
        "scope_nodes_preserved": len(scope_guids),
        "view_model_contract": view_model_before,
        "identity_moving_parts": sorted(
            row["donor_path"]
            for row in wrapper_rows
            if row["moving_part"] and row["identity_transform"]
        ),
        "wrappers": wrapper_rows,
    }
    return candidate, report


def _normalized_absolute(path: Path) -> Path:
    return path.expanduser().resolve(strict=False)


def _is_within(path: Path, root: Path) -> bool:
    candidate = os.path.normcase(os.fspath(_normalized_absolute(path)))
    boundary = os.path.normcase(os.fspath(_normalized_absolute(root)))
    try:
        return os.path.commonpath((candidate, boundary)) == boundary
    except ValueError:
        return False


def assert_output_paths_allowed(outputs: Sequence[Path]) -> None:
    for path in outputs:
        resolved = _normalized_absolute(path)
        if _is_within(resolved, WORKSPACE_ROOT):
            raise ContractError(
                "TEMP-only builder refuses every repository output: " + str(resolved)
            )
        if not _is_within(resolved, SYSTEM_TEMP_ROOT):
            raise ContractError(
                "TEMP-only builder requires output beneath the system temp directory: "
                + str(resolved)
            )


def _serialize_prefab(prefab: object, newline: str) -> bytes:
    text = json.dumps(prefab, ensure_ascii=False, indent=2) + "\n"
    if newline != "\n":
        text = text.replace("\n", newline)
    return text.encode("utf-8")


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _validate_manifest_pins(
    manifest: TransformManifest,
    *,
    sources: Mapping[str, Path],
    source_bytes: Mapping[str, bytes],
    outputs: Mapping[str, Path],
    output_bytes: Mapping[str, bytes],
) -> None:
    if not manifest.pins:
        if manifest.measured_and_accepted is True:
            raise ContractError(
                "measured_and_accepted manifest requires complete source/output pins"
            )
        return

    known_families = {spec.family for spec in PREFAB_SPECS}
    if set(manifest.pins) != known_families:
        missing = sorted(known_families - set(manifest.pins))
        extra = sorted(set(manifest.pins) - known_families)
        raise ContractError(
            f"Manifest pins must cover every family; missing={missing}, extra={extra}"
        )

    for spec in PREFAB_SPECS:
        family = spec.family
        pin = manifest.pins[family]
        actual_source = _normalized_absolute(sources[family])
        if _normalized_absolute(Path(pin.source_path)) != actual_source:
            raise ContractError(f"{family} source path pin mismatch")
        actual_source_sha = _sha256(source_bytes[family])
        if pin.source_sha256 != actual_source_sha:
            raise ContractError(
                f"{family} source SHA-256 pin mismatch: expected "
                f"{pin.source_sha256}, got {actual_source_sha}"
            )
        actual_output = _normalized_absolute(outputs[family])
        if _normalized_absolute(Path(pin.output_path)) != actual_output:
            raise ContractError(f"{family} output path pin mismatch")
        actual_output_sha = _sha256(output_bytes[family])
        if pin.output_sha256 != actual_output_sha:
            raise ContractError(
                f"{family} output SHA-256 pin mismatch: expected "
                f"{pin.output_sha256}, got {actual_output_sha}"
            )


def build_candidates(
    *,
    output_directory: Path,
    source_paths: Mapping[str, Path] | None = None,
    transform_manifest_path: Path | None = None,
) -> list[dict[str, Any]]:
    sources = {
        spec.family: (source_paths or {}).get(spec.family, spec.default_source)
        for spec in PREFAB_SPECS
    }
    output_directory = _normalized_absolute(output_directory)
    outputs = [output_directory / spec.output_name for spec in PREFAB_SPECS]
    assert_output_paths_allowed(outputs)
    if output_directory.exists() and any(output_directory.iterdir()):
        raise ContractError(
            "TEMP-only output directory must be empty: " + str(output_directory)
        )
    for spec, output in zip(PREFAB_SPECS, outputs):
        source = _normalized_absolute(sources[spec.family])
        if source == _normalized_absolute(output):
            raise ContractError(f"Source and output must differ: {source}")

    manifest = load_transform_manifest(transform_manifest_path)
    external_manifest = transform_manifest_path is not None
    resolved = {
        spec.family: _resolve_transforms(
            spec, manifest.families, external_manifest=external_manifest
        )
        for spec in PREFAB_SPECS
    }

    source_payloads: dict[str, tuple[Path, bytes, object, str]] = {}
    for spec in PREFAB_SPECS:
        source = _normalized_absolute(sources[spec.family])
        try:
            source_data = source.read_bytes()
            source_prefab = json.loads(source_data.decode("utf-8-sig"))
        except (OSError, UnicodeError, json.JSONDecodeError) as exc:
            raise ContractError(f"Cannot read source prefab {source}: {exc}") from exc
        newline = "\r\n" if b"\r\n" in source_data else "\n"
        source_payloads[spec.family] = (source, source_data, source_prefab, newline)

    built_outputs: list[tuple[PrefabSpec, Path, Path, bytes, bytes, dict[str, Any]]] = []
    reports: list[dict[str, Any]] = []
    for spec, output in zip(PREFAB_SPECS, outputs):
        source, source_data, source_prefab, newline = source_payloads[spec.family]
        candidate, report = transform_prefab(
            source_prefab, spec, resolved[spec.family]
        )
        output_bytes = _serialize_prefab(candidate, newline)
        json.loads(output_bytes.decode("utf-8"))
        report.update(
            {
                "source": str(source),
                "source_bytes": len(source_data),
                "source_sha256": _sha256(source_data),
                "output": str(output),
                "output_bytes": len(output_bytes),
                "output_sha256": _sha256(output_bytes),
                "manifest_mode": (
                    "external" if external_manifest else "builtin_identity_structural_only"
                ),
                "transform_manifest": (
                    str(manifest.path) if manifest.path is not None else None
                ),
                "transform_manifest_sha256": manifest.sha256,
                "measured_and_accepted": manifest.measured_and_accepted is True,
                "transform_semantics": (
                    "wrapper_local W = inverse(donor_idle_local P) * "
                    "accepted_weapon_fit B, including B scale"
                ),
                "proof_ceiling": (
                    "TEMP candidate only; W = inverse(P) * B composition, accepted B, "
                    "and rendered behavior are unverified"
                ),
            }
        )
        built_outputs.append((spec, output, source, source_data, output_bytes, report))
        reports.append(report)

    _validate_manifest_pins(
        manifest,
        sources={family: payload[0] for family, payload in source_payloads.items()},
        source_bytes={family: payload[1] for family, payload in source_payloads.items()},
        outputs={spec.family: output for spec, output in zip(PREFAB_SPECS, outputs)},
        output_bytes={
            spec.family: candidate_bytes
            for spec, _, _, _, candidate_bytes, _ in built_outputs
        },
    )

    output_directory.mkdir(parents=True, exist_ok=True)
    staged_pairs: list[tuple[Path, Path]] = []
    for _, output, _, _, output_bytes, _ in built_outputs:
        staged = pipeline_io.staging_path(output)
        staged.write_bytes(output_bytes)
        json.loads(staged.read_text(encoding="utf-8"))
        staged_pairs.append((staged, output))

    pipeline_io.promote_files(staged_pairs)
    return reports


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Move AR-15 and SR-25 custom first-person renderers onto child wrappers. "
            "Manifest values are full local W = inverse(P) * B transforms. Default "
            "output is temporary and identity-transform structural proof only."
        )
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Output directory; defaults to a new system-temporary directory.",
    )
    parser.add_argument(
        "--transform-manifest",
        type=Path,
        help="Version-1 JSON manifest of explicit per-node wrapper transforms.",
    )
    parser.add_argument(
        "--ar15-source",
        type=Path,
        default=PREFAB_SPECS[0].default_source,
        help="AR-15 first-person source prefab (read only).",
    )
    parser.add_argument(
        "--sr25-source",
        type=Path,
        default=PREFAB_SPECS[1].default_source,
        help="SR-25 first-person source prefab (read only).",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    output_directory = args.output_dir
    if output_directory is None:
        output_directory = Path(tempfile.mkdtemp(prefix="dxrp_viewmodel_wrappers_"))
    try:
        reports = build_candidates(
            output_directory=output_directory,
            source_paths={"ar15": args.ar15_source, "sr25": args.sr25_source},
            transform_manifest_path=args.transform_manifest,
        )
    except (ContractError, OSError) as exc:
        parser.exit(2, f"DXRP_VIEWMODEL_WRAPPER_BUILD_ERROR: {exc}\n")
    print(
        "DXRP_VIEWMODEL_WRAPPER_BUILD="
        + json.dumps(
            {
                "result": "PASS",
                "output_directory": str(_normalized_absolute(output_directory)),
                "game_tree_written": False,
                "reports": reports,
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
