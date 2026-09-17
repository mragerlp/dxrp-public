"""Build deterministic, temporary-only world-weapon left-hand IK candidates.

This tool deliberately has no product-write mode.  It reads an exact DXRP
world prefab, verifies the caller's SHA-256 pin and accepted per-weapon
manifest, then writes an inspectable candidate beneath the operating system's
temporary directory.  The source prefab is never modified.

The manifest is intentionally strict.  There are no default transforms:

    {
      "version": 1,
      "weapons": [
        {
          "weapon_id": "aks74u",
          "accepted": true,
          "source_sha256": "64 hexadecimal characters",
          "world_prefab_path":
            "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab",
          "hold_type": "Rifle",
          "left_grip": {
            "parent_guid": "existing source GameObject GUID",
            "position": "x,y,z",
            "rotation": "x,y,z,w",
            "scale": "x,y,z"
          }
        }
      ]
    }

The parent GUID is required because a local transform is meaningless without
its exact local basis.  Candidate GUIDs are UUIDv5 values derived from the
weapon identity and pinned source bytes.  Manifest order and temporary output
location therefore cannot change candidate bytes.

The literal accepted marker is a fail-closed input gate, not authentication of
who accepted the transform.  That provenance remains external evidence.

Only AKS-74U, AR-15, SR-25, and M870 are in the initial allowlist.  Pistol
weapons are rejected instead of receiving a rifle-style support-hand target.
The generated component is Dxura.RP.Game.WorldWeaponLeftHandIk and has only an
Equipment reference plus LeftGrip.  HoldType and Handedness remain untouched;
the firing hand remains owned by hold_R, HoldType, and the player animation
graph.  No RightGrip, IkRightHand, or hand_right assignment is generated.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import shutil
import sys
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterator, Mapping


WORKSPACE_ROOT = Path(__file__).resolve().parents[2]
TEMP_PREFIX = "dxrp-world-hold-ik-"
COMPONENT_TYPE = "Dxura.RP.Game.WorldWeaponLeftHandIk"
EQUIPMENT_TYPE = "Dxura.RP.Game.Equipment"
GUID_NAMESPACE = uuid.uuid5(
    uuid.NAMESPACE_URL,
    "https://lifepunch.co/dxrp/world-hold-ik-candidate/v1",
)
PISTOL_WEAPON_IDS = frozenset(
    {
        "deserteagle",
        "desert_eagle",
        "m1911",
        "pistol",
        "usp",
    }
)


class ContractError(RuntimeError):
    """Raised when an input violates the temp-candidate contract."""


@dataclass(frozen=True)
class WeaponSpec:
    weapon_id: str
    world_prefab_path: str
    root_name: str
    hold_type: str
    output_name: str


SUPPORTED_WEAPONS: tuple[WeaponSpec, ...] = (
    WeaponSpec(
        weapon_id="aks74u",
        world_prefab_path=(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/"
            "w_aks74u/w_aks74u.prefab"
        ),
        root_name="w_aks74u",
        hold_type="Rifle",
        output_name="w_aks74u.world_hold_ik.candidate.prefab",
    ),
    WeaponSpec(
        weapon_id="ar15",
        world_prefab_path=(
            "game/Assets/addons/lifepunch/lpweapons/ar15/equipment/"
            "w_ar15/w_ar15.prefab"
        ),
        root_name="w_ar15",
        hold_type="Rifle",
        output_name="w_ar15.world_hold_ik.candidate.prefab",
    ),
    WeaponSpec(
        weapon_id="sr25",
        world_prefab_path=(
            "game/Assets/addons/lifepunch/lpweapons/sr25/equipment/"
            "w_sr25/w_sr25.prefab"
        ),
        root_name="w_sr25",
        hold_type="Rifle",
        output_name="w_sr25.world_hold_ik.candidate.prefab",
    ),
    WeaponSpec(
        weapon_id="m870",
        world_prefab_path=(
            "game/Assets/addons/lifepunch/lpweapons/m870/equipment/"
            "w_m870/w_m870.prefab"
        ),
        root_name="w_m870",
        hold_type="Shotgun",
        output_name="w_m870.world_hold_ik.candidate.prefab",
    ),
)
SPECS_BY_ID: Mapping[str, WeaponSpec] = {
    spec.weapon_id: spec for spec in SUPPORTED_WEAPONS
}


@dataclass(frozen=True)
class GripTransform:
    parent_guid: str
    position: str
    rotation: str
    scale: str


@dataclass(frozen=True)
class ManifestEntry:
    weapon_id: str
    accepted: bool
    source_sha256: str
    world_prefab_path: str
    hold_type: str
    left_grip: GripTransform


@dataclass(frozen=True)
class NodeRecord:
    node: dict[str, Any]
    path: str


@dataclass(frozen=True)
class PrefabIndex:
    nodes_by_guid: Mapping[str, NodeRecord]
    components_by_guid: Mapping[str, dict[str, Any]]
    component_owners: Mapping[str, str]
    all_guids: frozenset[str]


@dataclass(frozen=True)
class SourcePrefab:
    path: Path
    raw: bytes
    payload: dict[str, Any]
    index: PrefabIndex
    equipment: dict[str, Any]
    equipment_guid: str
    root_guid: str


@dataclass(frozen=True)
class RenderedCandidate:
    entry: ManifestEntry
    spec: WeaponSpec
    source: SourcePrefab
    output_bytes: bytes
    output_sha256: str
    component_guid: str
    left_grip_guid: str


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


def _load_json_bytes(raw: bytes, context: str) -> object:
    try:
        return json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=_reject_duplicate_keys,
        )
    except ContractError:
        raise
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Cannot parse {context} as UTF-8 JSON: {exc}") from exc


def _require_exact_keys(
    value: object,
    expected: set[str],
    context: str,
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
    if not isinstance(value, str):
        raise ContractError(f"{context} must be a SHA-256 string")
    normalized = value.strip().upper()
    if len(normalized) != 64 or any(
        character not in "0123456789ABCDEF" for character in normalized
    ):
        raise ContractError(f"{context} must be exactly 64 hexadecimal characters")
    return normalized


def _parse_guid(value: object, context: str) -> str:
    if not isinstance(value, str) or value != value.strip():
        raise ContractError(f"{context} must be a canonical GUID string")
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError) as exc:
        raise ContractError(f"{context} is not a valid GUID: {value!r}") from exc
    canonical = str(parsed)
    if value.lower() != canonical:
        raise ContractError(f"{context} must use canonical GUID form: {value!r}")
    return canonical


def _parse_vector(
    value: object,
    length: int,
    context: str,
) -> tuple[str, tuple[float, ...]]:
    if not isinstance(value, str) or value != value.strip() or not value:
        raise ContractError(
            f"{context} must be an exact, non-empty comma-separated string"
        )
    tokens = value.split(",")
    if len(tokens) != length or any(token != token.strip() or not token for token in tokens):
        raise ContractError(
            f"{context} must contain exactly {length} comma-separated values "
            "without surrounding whitespace"
        )
    try:
        parsed = tuple(float(token) for token in tokens)
    except ValueError as exc:
        raise ContractError(f"{context} contains a non-numeric value") from exc
    if not all(math.isfinite(item) for item in parsed):
        raise ContractError(f"{context} must contain only finite values")
    return value, parsed


def _parse_grip(value: object, context: str) -> GripTransform:
    payload = _require_exact_keys(
        value,
        {"parent_guid", "position", "rotation", "scale"},
        context,
    )
    position, _ = _parse_vector(payload["position"], 3, f"{context}.position")
    rotation, parsed_rotation = _parse_vector(
        payload["rotation"], 4, f"{context}.rotation"
    )
    scale, parsed_scale = _parse_vector(payload["scale"], 3, f"{context}.scale")
    rotation_norm = math.sqrt(sum(item * item for item in parsed_rotation))
    if not math.isclose(rotation_norm, 1.0, rel_tol=0.0, abs_tol=1e-5):
        raise ContractError(
            f"{context}.rotation must be a normalized quaternion; "
            f"norm is {rotation_norm:.12g}"
        )
    if any(item <= 0.0 for item in parsed_scale):
        raise ContractError(f"{context}.scale must contain only positive values")
    return GripTransform(
        parent_guid=_parse_guid(payload["parent_guid"], f"{context}.parent_guid"),
        position=position,
        rotation=rotation,
        scale=scale,
    )


def _parse_entry(value: object, index: int) -> ManifestEntry:
    context = f"weapons[{index}]"
    payload = _require_exact_keys(
        value,
        {
            "weapon_id",
            "accepted",
            "source_sha256",
            "world_prefab_path",
            "hold_type",
            "left_grip",
        },
        context,
    )
    weapon_id = payload["weapon_id"]
    if not isinstance(weapon_id, str) or weapon_id != weapon_id.strip().lower():
        raise ContractError(f"{context}.weapon_id must be a lowercase identifier")
    if weapon_id in PISTOL_WEAPON_IDS:
        raise ContractError(
            f"{context}.weapon_id {weapon_id!r} is a pistol; "
            "no world support-hand IK candidate is emitted"
        )
    spec = SPECS_BY_ID.get(weapon_id)
    if spec is None:
        raise ContractError(
            f"{context}.weapon_id {weapon_id!r} is unsupported; "
            f"allowed values are {sorted(SPECS_BY_ID)}"
        )
    if type(payload["accepted"]) is not bool or payload["accepted"] is not True:
        raise ContractError(
            f"{context}.accepted must be the JSON boolean true; "
            "unaccepted transforms cannot produce a candidate"
        )
    world_prefab_path = payload["world_prefab_path"]
    if not isinstance(world_prefab_path, str):
        raise ContractError(f"{context}.world_prefab_path must be a string")
    if world_prefab_path != spec.world_prefab_path:
        raise ContractError(
            f"{context}.world_prefab_path must be exactly "
            f"{spec.world_prefab_path!r}"
        )
    hold_type = payload["hold_type"]
    if hold_type != spec.hold_type:
        raise ContractError(
            f"{context}.hold_type must be {spec.hold_type!r} for {weapon_id}"
        )
    return ManifestEntry(
        weapon_id=weapon_id,
        accepted=True,
        source_sha256=_parse_sha256(
            payload["source_sha256"], f"{context}.source_sha256"
        ),
        world_prefab_path=world_prefab_path,
        hold_type=hold_type,
        left_grip=_parse_grip(payload["left_grip"], f"{context}.left_grip"),
    )


def _canonical_manifest_bytes(entries: tuple[ManifestEntry, ...]) -> bytes:
    payload = {
        "version": 1,
        "weapons": [
            {
                "weapon_id": entry.weapon_id,
                "accepted": entry.accepted,
                "source_sha256": entry.source_sha256,
                "world_prefab_path": entry.world_prefab_path,
                "hold_type": entry.hold_type,
                "left_grip": {
                    "parent_guid": entry.left_grip.parent_guid,
                    "position": entry.left_grip.position,
                    "rotation": entry.left_grip.rotation,
                    "scale": entry.left_grip.scale,
                },
            }
            for entry in sorted(entries, key=lambda item: item.weapon_id)
        ],
    }
    return (
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def load_manifest(path: Path) -> tuple[tuple[ManifestEntry, ...], str]:
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"Cannot read manifest {path}: {exc}") from exc
    payload = _require_exact_keys(
        _load_json_bytes(raw, f"manifest {path}"),
        {"version", "weapons"},
        "manifest",
    )
    if type(payload["version"]) is not int or payload["version"] != 1:
        raise ContractError("manifest.version must be the integer 1")
    weapons = payload["weapons"]
    if not isinstance(weapons, list) or not weapons:
        raise ContractError("manifest.weapons must be a non-empty array")
    entries = tuple(_parse_entry(value, index) for index, value in enumerate(weapons))
    weapon_ids = [entry.weapon_id for entry in entries]
    duplicates = sorted(
        weapon_id for weapon_id in set(weapon_ids) if weapon_ids.count(weapon_id) > 1
    )
    if duplicates:
        raise ContractError(f"manifest contains duplicate weapons: {duplicates}")
    canonical = _canonical_manifest_bytes(entries)
    return entries, _sha256(canonical)


def _iter_nodes(node: object, parent_path: str = "") -> Iterator[NodeRecord]:
    if not isinstance(node, dict):
        raise ContractError("Every prefab GameObject must be a JSON object")
    name = node.get("Name")
    if not isinstance(name, str) or not name:
        raise ContractError("Every prefab GameObject must have a non-empty Name")
    path = f"{parent_path}/{name}" if parent_path else name
    yield NodeRecord(node=node, path=path)
    children = node.get("Children")
    if not isinstance(children, list):
        raise ContractError(f"GameObject {path} must have a Children array")
    for child in children:
        yield from _iter_nodes(child, path)


def _add_guid(
    value: object,
    owner: str,
    seen: dict[str, str],
) -> str:
    guid = _parse_guid(value, owner)
    previous = seen.get(guid)
    if previous is not None:
        raise ContractError(f"Duplicate GUID {guid}: {previous} and {owner}")
    seen[guid] = owner
    return guid


def _walk_json(value: object) -> Iterator[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_json(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_json(child)


def validate_references(prefab: object, index: PrefabIndex) -> None:
    for value in _walk_json(prefab):
        reference_type = value.get("_type")
        if reference_type == "gameobject" and "go" in value:
            target = value.get("go")
            if target not in index.nodes_by_guid:
                raise ContractError(f"Dangling GameObject reference: {target!r}")
        elif reference_type == "component":
            component_id = value.get("component_id")
            target_go = value.get("go")
            if component_id not in index.components_by_guid:
                raise ContractError(f"Dangling component reference: {component_id!r}")
            actual_owner = index.component_owners[component_id]
            if target_go != actual_owner:
                raise ContractError(
                    f"Component reference {component_id} names owner {target_go!r}; "
                    f"actual owner is {actual_owner}"
                )


def index_prefab(prefab: object) -> PrefabIndex:
    if not isinstance(prefab, dict) or "RootObject" not in prefab:
        raise ContractError("Prefab must contain a RootObject")
    seen: dict[str, str] = {}
    nodes: dict[str, NodeRecord] = {}
    components: dict[str, dict[str, Any]] = {}
    component_owners: dict[str, str] = {}
    for record in _iter_nodes(prefab["RootObject"]):
        node_guid = _add_guid(
            record.node.get("__guid"),
            f"GameObject {record.path}",
            seen,
        )
        nodes[node_guid] = record
        node_components = record.node.get("Components")
        if not isinstance(node_components, list):
            raise ContractError(f"GameObject {record.path} must have a Components array")
        for component in node_components:
            if not isinstance(component, dict):
                raise ContractError(
                    f"GameObject {record.path} contains a non-object component"
                )
            component_type = component.get("__type")
            if not isinstance(component_type, str) or not component_type:
                raise ContractError(
                    f"GameObject {record.path} contains a component without __type"
                )
            component_guid = _add_guid(
                component.get("__guid"),
                f"Component {record.path}:{component_type}",
                seen,
            )
            components[component_guid] = component
            component_owners[component_guid] = node_guid
    result = PrefabIndex(
        nodes_by_guid=nodes,
        components_by_guid=components,
        component_owners=component_owners,
        all_guids=frozenset(seen),
    )
    validate_references(prefab, result)
    return result


def _source_path(repo_root: Path, spec: WeaponSpec) -> Path:
    pure = PurePosixPath(spec.world_prefab_path)
    if pure.is_absolute() or ".." in pure.parts:
        raise ContractError(f"Unsafe configured prefab path: {spec.world_prefab_path}")
    candidate = _absolute(repo_root.joinpath(*pure.parts))
    resolved_root = repo_root.resolve(strict=True)
    try:
        resolved = candidate.resolve(strict=True)
    except OSError as exc:
        raise ContractError(
            f"World prefab does not exist for {spec.weapon_id}: {candidate}"
        ) from exc
    if not _is_within(resolved, resolved_root):
        raise ContractError(f"World prefab escapes the repository: {resolved}")
    if not resolved.is_file():
        raise ContractError(f"World prefab is not a file: {resolved}")
    return resolved


def load_source_prefab(repo_root: Path, spec: WeaponSpec) -> SourcePrefab:
    path = _source_path(repo_root, spec)
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"Cannot read world prefab {path}: {exc}") from exc
    payload = _load_json_bytes(raw, f"world prefab {path}")
    if not isinstance(payload, dict):
        raise ContractError(f"World prefab {path} must be a JSON object")
    prefab_index = index_prefab(payload)
    root = payload["RootObject"]
    if root.get("Name") != spec.root_name:
        raise ContractError(
            f"{spec.weapon_id} root must be {spec.root_name!r}, "
            f"got {root.get('Name')!r}"
        )
    root_guid = root["__guid"]
    equipment_matches = [
        (guid, component)
        for guid, component in prefab_index.components_by_guid.items()
        if component.get("__type") == EQUIPMENT_TYPE
    ]
    if len(equipment_matches) != 1:
        raise ContractError(
            f"{spec.weapon_id} must contain exactly one Equipment component; "
            f"found {len(equipment_matches)}"
        )
    equipment_guid, equipment = equipment_matches[0]
    if prefab_index.component_owners[equipment_guid] != root_guid:
        raise ContractError(f"{spec.weapon_id} Equipment must be on the prefab root")
    if equipment.get("HoldType") != spec.hold_type:
        raise ContractError(
            f"{spec.weapon_id} source HoldType must be {spec.hold_type!r}, "
            f"got {equipment.get('HoldType')!r}"
        )
    shared_components = [
        component
        for component in prefab_index.components_by_guid.values()
        if component.get("__type") == COMPONENT_TYPE
    ]
    if shared_components:
        raise ContractError(
            f"{spec.weapon_id} source already contains {COMPONENT_TYPE}; "
            "refusing to stack a candidate"
        )
    existing_grips = [
        record.path
        for record in prefab_index.nodes_by_guid.values()
        if record.node.get("Name") == "LeftGrip"
    ]
    if existing_grips:
        raise ContractError(
            f"{spec.weapon_id} source already contains LeftGrip at {existing_grips}"
        )
    return SourcePrefab(
        path=path,
        raw=raw,
        payload=payload,
        index=prefab_index,
        equipment=equipment,
        equipment_guid=equipment_guid,
        root_guid=root_guid,
    )


def inspect_supported_sources(repo_root: Path = WORKSPACE_ROOT) -> list[dict[str, object]]:
    root = _absolute(repo_root)
    reports = []
    for spec in SUPPORTED_WEAPONS:
        source = load_source_prefab(root, spec)
        reports.append(
            {
                "weapon_id": spec.weapon_id,
                "world_prefab_path": spec.world_prefab_path,
                "source_sha256": _sha256(source.raw),
                "hold_type": source.equipment["HoldType"],
                "guid_count": len(source.index.all_guids),
                "reference_closure": True,
            }
        )
    return reports


def _candidate_guids(entry: ManifestEntry) -> tuple[str, str]:
    seed = (
        f"{entry.weapon_id}|{entry.world_prefab_path}|"
        f"{entry.source_sha256}"
    )
    component_guid = str(uuid.uuid5(GUID_NAMESPACE, f"{seed}|component"))
    left_grip_guid = str(uuid.uuid5(GUID_NAMESPACE, f"{seed}|left-grip"))
    return component_guid, left_grip_guid


def _left_grip_node(guid: str, transform: GripTransform) -> dict[str, object]:
    return {
        "__guid": guid,
        "__version": 2,
        "Flags": 0,
        "Name": "LeftGrip",
        "Position": transform.position,
        "Rotation": transform.rotation,
        "Scale": transform.scale,
        "Tags": "",
        "Enabled": True,
        "NetworkMode": 2,
        "NetworkFlags": 0,
        "NetworkOrphaned": 0,
        "NetworkTransmit": True,
        "OwnerTransfer": 1,
        "Components": [],
        "Children": [],
    }


def _ik_component(
    guid: str,
    root_guid: str,
    equipment_guid: str,
    left_grip_guid: str,
) -> dict[str, object]:
    return {
        "__type": COMPONENT_TYPE,
        "__guid": guid,
        "__enabled": True,
        "Flags": 0,
        "Equipment": {
            "_type": "component",
            "component_id": equipment_guid,
            "go": root_guid,
            "component_type": "Equipment",
        },
        "LeftGrip": {
            "_type": "gameobject",
            "go": left_grip_guid,
        },
        "OnComponentDestroy": None,
        "OnComponentDisabled": None,
        "OnComponentEnabled": None,
        "OnComponentFixedUpdate": None,
        "OnComponentStart": None,
        "OnComponentUpdate": None,
    }


def _serialize_prefab(prefab: dict[str, Any]) -> bytes:
    return (json.dumps(prefab, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def render_candidate(
    entry: ManifestEntry,
    spec: WeaponSpec,
    source: SourcePrefab,
) -> RenderedCandidate:
    actual_sha = _sha256(source.raw)
    if actual_sha != entry.source_sha256:
        raise ContractError(
            f"{entry.weapon_id} source SHA-256 mismatch: "
            f"expected {entry.source_sha256}, got {actual_sha}"
        )
    if entry.world_prefab_path != spec.world_prefab_path:
        raise ContractError(
            f"{entry.weapon_id} world prefab path changed after manifest validation"
        )
    if entry.hold_type != source.equipment.get("HoldType"):
        raise ContractError(
            f"{entry.weapon_id} manifest/source HoldType mismatch: "
            f"{entry.hold_type!r} vs {source.equipment.get('HoldType')!r}"
        )
    if entry.left_grip.parent_guid not in source.index.nodes_by_guid:
        raise ContractError(
            f"{entry.weapon_id} left_grip.parent_guid does not exist in the "
            f"pinned source: {entry.left_grip.parent_guid}"
        )

    component_guid, left_grip_guid = _candidate_guids(entry)
    for generated_guid in (component_guid, left_grip_guid):
        if generated_guid in source.index.all_guids:
            raise ContractError(
                f"{entry.weapon_id} deterministic candidate GUID collides with "
                f"the source: {generated_guid}"
            )

    candidate = copy.deepcopy(source.payload)
    candidate_index = index_prefab(candidate)
    parent = candidate_index.nodes_by_guid[entry.left_grip.parent_guid].node
    parent["Children"].append(_left_grip_node(left_grip_guid, entry.left_grip))
    root = candidate["RootObject"]
    root["Components"].append(
        _ik_component(
            component_guid,
            source.root_guid,
            source.equipment_guid,
            left_grip_guid,
        )
    )

    final_index = index_prefab(candidate)
    generated_components = [
        component
        for component in final_index.components_by_guid.values()
        if component.get("__type") == COMPONENT_TYPE
    ]
    if len(generated_components) != 1:
        raise ContractError(
            f"{entry.weapon_id} candidate must contain exactly one "
            f"{COMPONENT_TYPE}; found {len(generated_components)}"
        )
    component = generated_components[0]
    forbidden_tokens = {"RightGrip", "IkRightHand", "hand_right"}
    serialized_component = json.dumps(component, sort_keys=True)
    present_forbidden = sorted(
        token for token in forbidden_tokens if token in serialized_component
    )
    if present_forbidden:
        raise ContractError(
            f"{entry.weapon_id} candidate generated forbidden right-hand tokens: "
            f"{present_forbidden}"
        )
    final_equipment = final_index.components_by_guid[source.equipment_guid]
    if final_equipment.get("HoldType") != source.equipment.get("HoldType"):
        raise ContractError(f"{entry.weapon_id} candidate changed HoldType")
    if final_equipment.get("Handedness") != source.equipment.get("Handedness"):
        raise ContractError(f"{entry.weapon_id} candidate changed Handedness")

    output_bytes = _serialize_prefab(candidate)
    return RenderedCandidate(
        entry=entry,
        spec=spec,
        source=source,
        output_bytes=output_bytes,
        output_sha256=_sha256(output_bytes),
        component_guid=component_guid,
        left_grip_guid=left_grip_guid,
    )


def render_candidate_set(
    manifest_path: Path,
    repo_root: Path = WORKSPACE_ROOT,
) -> tuple[list[RenderedCandidate], bytes, str]:
    entries, manifest_contract_sha256 = load_manifest(manifest_path)
    root = _absolute(repo_root)
    rendered: list[RenderedCandidate] = []
    for entry in sorted(entries, key=lambda item: item.weapon_id):
        spec = SPECS_BY_ID[entry.weapon_id]
        source = load_source_prefab(root, spec)
        rendered.append(render_candidate(entry, spec, source))

    index_payload = {
        "version": 1,
        "temporary_only": True,
        "manifest_contract_sha256": manifest_contract_sha256,
        "runtime_component": COMPONENT_TYPE,
        "right_hand_policy": (
            "unchanged: hold_R plus HoldType; no right-hand IK generated"
        ),
        "candidates": [
            {
                "weapon_id": candidate.entry.weapon_id,
                "world_prefab_path": candidate.entry.world_prefab_path,
                "source_sha256": candidate.entry.source_sha256,
                "hold_type": candidate.entry.hold_type,
                "left_grip_parent_guid": candidate.entry.left_grip.parent_guid,
                "left_grip_guid": candidate.left_grip_guid,
                "component_guid": candidate.component_guid,
                "output_file": candidate.spec.output_name,
                "output_sha256": candidate.output_sha256,
                "reference_closure": True,
            }
            for candidate in rendered
        ],
    }
    index_bytes = (
        json.dumps(index_payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    return rendered, index_bytes, manifest_contract_sha256


def _verified_temp_parent(repo_root: Path, temp_parent: Path | None) -> Path:
    parent = _absolute(temp_parent or Path(tempfile.gettempdir()))
    try:
        resolved_parent = parent.resolve(strict=True)
        resolved_repo = repo_root.resolve(strict=True)
        resolved_system_temp = Path(tempfile.gettempdir()).resolve(strict=True)
    except OSError as exc:
        raise ContractError(f"Cannot resolve temp/repository roots: {exc}") from exc
    if not resolved_parent.is_dir():
        raise ContractError(f"Temporary parent is not a directory: {resolved_parent}")
    if not _is_within(resolved_parent, resolved_system_temp):
        raise ContractError(
            "Temporary output parent must be beneath the operating system temp "
            f"directory {resolved_system_temp}: {resolved_parent}"
        )
    protected_roots = {resolved_repo, WORKSPACE_ROOT.resolve(strict=True)}
    for protected_root in protected_roots:
        if _is_within(resolved_parent, protected_root):
            raise ContractError(
                "Temporary output parent must be outside the repository/workspace: "
                f"{resolved_parent}"
            )
    return resolved_parent


def generate_temp_candidates(
    manifest_path: Path,
    repo_root: Path = WORKSPACE_ROOT,
    *,
    temp_parent: Path | None = None,
) -> dict[str, object]:
    root = _absolute(repo_root)
    rendered, index_bytes, manifest_contract_sha256 = render_candidate_set(
        manifest_path,
        root,
    )
    for candidate in rendered:
        try:
            current = candidate.source.path.read_bytes()
        except OSError as exc:
            raise ContractError(
                f"Cannot re-read pinned source {candidate.source.path}: {exc}"
            ) from exc
        if current != candidate.source.raw:
            raise ContractError(
                f"Pinned source changed during candidate rendering: "
                f"{candidate.source.path}"
            )

    parent = _verified_temp_parent(root, temp_parent)
    output_directory = Path(tempfile.mkdtemp(prefix=TEMP_PREFIX, dir=parent))
    try:
        for candidate in rendered:
            (output_directory / candidate.spec.output_name).write_bytes(
                candidate.output_bytes
            )
        (output_directory / "candidate-index.json").write_bytes(index_bytes)
    except BaseException:
        if output_directory.is_dir() and output_directory.parent == parent:
            shutil.rmtree(output_directory)
        raise

    return {
        "temporary_only": True,
        "output_directory": str(output_directory),
        "manifest_contract_sha256": manifest_contract_sha256,
        "candidate_count": len(rendered),
        "candidates": [
            {
                "weapon_id": candidate.entry.weapon_id,
                "output_file": candidate.spec.output_name,
                "output_sha256": candidate.output_sha256,
            }
            for candidate in rendered
        ],
        "index_sha256": _sha256(index_bytes),
    }


def _check_report(
    rendered: list[RenderedCandidate],
    index_bytes: bytes,
    manifest_contract_sha256: str,
) -> dict[str, object]:
    return {
        "temporary_only": True,
        "wrote_files": False,
        "manifest_contract_sha256": manifest_contract_sha256,
        "candidate_count": len(rendered),
        "candidates": [
            {
                "weapon_id": candidate.entry.weapon_id,
                "output_file": candidate.spec.output_name,
                "output_sha256": candidate.output_sha256,
            }
            for candidate in rendered
        ],
        "index_sha256": _sha256(index_bytes),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Render deterministic DXRP world-hold IK candidates only beneath "
            "the system temporary directory."
        )
    )
    parser.add_argument("manifest", type=Path)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=WORKSPACE_ROOT,
        help="read-only repository root containing the exact manifest paths",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="validate and hash candidates in memory without writing temp files",
    )
    args = parser.parse_args(argv)

    try:
        if args.check_only:
            rendered, index_bytes, manifest_sha = render_candidate_set(
                args.manifest,
                args.repo_root,
            )
            report = _check_report(rendered, index_bytes, manifest_sha)
            prefix = "DXRP_WORLD_HOLD_IK_CHECK="
        else:
            report = generate_temp_candidates(args.manifest, args.repo_root)
            prefix = "DXRP_WORLD_HOLD_IK_TEMP="
    except ContractError as exc:
        print(f"DXRP_WORLD_HOLD_IK_FAILED: {exc}", file=sys.stderr)
        return 1
    print(prefix + json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
