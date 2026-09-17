"""Build a deterministic, strict-TEMP-only AKS-74U first-person bolt candidate.

The tool consumes a byte-pinned AKS/MP5 fit candidate, asks a byte-pinned
Blender worker to extract only the authoritative ``tag_bolt`` rigid partition,
and emits promotion-ready FBXs, ModelDocs, one first-person prefab, and a
complete bundle manifest.  There is intentionally no game/product write mode.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import stat
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence


class ContractError(RuntimeError):
    pass


PURPOSE = "dxrp_aks74u_fp_bolt_split_candidate"
REQUIRED_INPUTS = {
    "authoritative_fbx",
    "authoritative_fbx_probe",
    "source_topology_evidence",
    "base_fit_candidate_prefab",
    "base_fit_report",
    "existing_fit_builder",
    "current_target_vm_prefab",
    "mp5_donor_vm_prefab",
    "current_body_modeldoc",
    "current_mag_modeldoc",
    "compiled_mp5_model",
    "compiled_mp5_data_probe",
    "source2viewer_parser",
    "blender_executable",
    "body_material",
    "body_basecolor",
    "body_normal",
    "body_ao",
    "body_metalness",
    "body_roughness",
}
EXPECTED_BLENDER_VERSION = "Blender 5.1.2"
EXPECTED_BIND = {
    "sensor": (
        "Source2Viewer-CLI 19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa "
        "-b DATA stdout"
    ),
    "bone": "bolt",
    "parent": "weapon_root_children",
    "transform": {
        "position": "4.098689,0.000002,2.750489",
        "rotation": "0,0,0,1",
        "scale": "1,1,1",
    },
}
EXPECTED_TOPOLOGY = {
    "source_object": "SK_Rif_SLR_AK47.001",
    "source_material": "SLR47_MI.001",
    "group": "tag_bolt",
    "source_vertices": 34_717,
    "source_faces": 21_831,
    "bolt_vertices": 1_285,
    "bolt_faces": 830,
    "body_minus_bolt_vertices": 33_432,
    "body_minus_bolt_faces": 21_001,
    "mixed_group_faces": 0,
    "weld_quantum_m": 1e-6,
    "welded_island_face_counts": [350, 291, 189],
}

ROOT_NAME = "vm_aks74u"
ASSEMBLY_PATH = "vm_aks74u/weapon_root/weapon_root_children"
BOLT_PARENT_PATH = ASSEMBLY_PATH + "/bolt"
MAGAZINE_PATH = ASSEMBLY_PATH + "/magazine/aks74u_magazine"
BODY_PATH = ASSEMBLY_PATH + "/aks74u_body"
SELECTOR_PATH = ASSEMBLY_PATH + "/mode_selector"
TRIGGER_PATH = ASSEMBLY_PATH + "/trigger"
STOCK_PATH = ASSEMBLY_PATH + "/stock"
DONOR_MODEL = "models/weapons/sbox_smg_mp5/v_mp5.vmdl"
CURRENT_BODY_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl"
CURRENT_MAG_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl"
BODY_MINUS_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl"
BOLT_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl"
BODY_FBX_ASSET = (
    "addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_body_minus_bolt.fbx"
)
BOLT_FBX_ASSET = "addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_bolt.fbx"
BODY_MATERIAL = "addons/lifepunch/lpweapons/aks74u/aks74u.vmat"
SOURCE_MATERIAL = "SLR47_MI.001.vmat"
BOLT_NODE_GUID = "b583651e-982a-57d4-bcbb-564696b46a23"
BOLT_RENDERER_GUID = "a6151211-0429-5ec4-8a36-acbcc6b40888"


@dataclass(frozen=True)
class Pin:
    path: Path
    bytes: int
    sha256: str


@dataclass(frozen=True)
class Transform:
    position: tuple[float, float, float]
    rotation: tuple[float, float, float, float]
    scale: tuple[float, float, float]


@dataclass(frozen=True)
class InputContract:
    manifest_path: Path
    manifest_sha256: str
    pins: Mapping[str, Pin]
    compiled_bind: Transform
    raw: Mapping[str, Any]


@dataclass(frozen=True)
class PrefabIndex:
    nodes: Mapping[str, dict[str, Any]]
    node_guids: Mapping[str, str]
    component_guids: Mapping[str, tuple[str, dict[str, Any]]]


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return (json.dumps(value, indent=2, ensure_ascii=True) + "\n").encode("utf-8")


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def _is_reparse(path: Path) -> bool:
    try:
        result = path.lstat()
    except FileNotFoundError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(getattr(result, "st_file_attributes", 0) & flag)


def _assert_no_reparse_components(path: Path) -> None:
    current = Path(os.path.abspath(os.fspath(path)))
    while True:
        if _is_reparse(current):
            raise ContractError(f"Reparse-point path is forbidden: {current}")
        parent = current.parent
        if parent == current:
            return
        current = parent


def assert_temp_output_directory(path: Path) -> Path:
    lexical = Path(os.path.abspath(os.fspath(path)))
    _assert_no_reparse_components(lexical)
    parent = lexical.parent.resolve(strict=True)
    candidate = parent / lexical.name
    temp_root = Path(tempfile.gettempdir()).resolve(strict=True)
    if candidate == temp_root or not _is_within(candidate, temp_root):
        raise ContractError(
            f"Output must be a strict child of system TEMP {temp_root}: {candidate}"
        )
    if candidate.exists():
        if not candidate.is_dir():
            raise ContractError(f"Output is not a directory: {candidate}")
        if any(candidate.iterdir()):
            raise ContractError(f"Output directory must be empty: {candidate}")
    return candidate


def _parse_pin(label: str, value: Any) -> Pin:
    if not isinstance(value, dict) or set(value) != {"path", "bytes", "sha256"}:
        raise ContractError(f"inputs.{label} must contain exactly path/bytes/sha256")
    path_text = value["path"]
    if not isinstance(path_text, str) or not Path(path_text).is_absolute():
        raise ContractError(f"inputs.{label}.path must be absolute")
    expected_bytes = value["bytes"]
    expected_sha = value["sha256"]
    if not isinstance(expected_bytes, int) or expected_bytes < 0:
        raise ContractError(f"inputs.{label}.bytes must be a nonnegative integer")
    if (
        not isinstance(expected_sha, str)
        or len(expected_sha) != 64
        or any(ch not in "0123456789abcdefABCDEF" for ch in expected_sha)
    ):
        raise ContractError(f"inputs.{label}.sha256 must be a full SHA-256")
    path = Path(path_text)
    _assert_no_reparse_components(path)
    path = path.resolve(strict=True)
    if not path.is_file():
        raise ContractError(f"Pinned input is not a file: {path}")
    raw = path.read_bytes()
    if len(raw) != expected_bytes:
        raise ContractError(
            f"Pinned input byte mismatch for {label}: expected {expected_bytes}, got {len(raw)}"
        )
    actual = _sha256(raw)
    if actual != expected_sha.lower():
        raise ContractError(
            f"Pinned input SHA-256 mismatch for {label}: expected {expected_sha.lower()}, got {actual}"
        )
    return Pin(path, expected_bytes, actual)


def _parse_numbers(value: Any, count: int, label: str) -> tuple[float, ...]:
    if not isinstance(value, str):
        raise ContractError(f"{label} must be a comma-separated string")
    fields = value.split(",")
    if len(fields) != count:
        raise ContractError(f"{label} must contain {count} values")
    try:
        result = tuple(float(field) for field in fields)
    except ValueError as exc:
        raise ContractError(f"{label} contains a non-number") from exc
    if not all(math.isfinite(number) for number in result):
        raise ContractError(f"{label} contains a non-finite number")
    return result


def parse_transform(value: Any, label: str) -> Transform:
    if not isinstance(value, dict) or set(value) != {"position", "rotation", "scale"}:
        raise ContractError(f"{label} must contain exactly position/rotation/scale")
    transform = Transform(
        _parse_numbers(value["position"], 3, label + ".position"),
        _parse_numbers(value["rotation"], 4, label + ".rotation"),
        _parse_numbers(value["scale"], 3, label + ".scale"),
    )
    if any(abs(number) < 1e-12 for number in transform.scale):
        raise ContractError(f"{label}.scale must be invertible")
    length = math.sqrt(sum(number * number for number in transform.rotation))
    if abs(length - 1.0) > 1e-5:
        raise ContractError(f"{label}.rotation must be a unit quaternion")
    return Transform(
        transform.position,
        tuple(number / length for number in transform.rotation),
        transform.scale,
    )


def load_input_contract(path: Path) -> InputContract:
    _assert_no_reparse_components(path)
    path = path.resolve(strict=True)
    raw_bytes = path.read_bytes()
    try:
        payload = json.loads(raw_bytes)
    except json.JSONDecodeError as exc:
        raise ContractError(f"Input manifest is not JSON: {path}") from exc
    if not isinstance(payload, dict) or set(payload) != {
        "version",
        "purpose",
        "inputs",
        "blender_version",
        "compiled_bolt_bind",
        "topology_contract",
    }:
        raise ContractError("Input manifest has missing or extra top-level fields")
    if payload["version"] != 1 or payload["purpose"] != PURPOSE:
        raise ContractError("Input manifest version/purpose mismatch")
    inputs = payload["inputs"]
    if not isinstance(inputs, dict) or set(inputs) != REQUIRED_INPUTS:
        raise ContractError(
            f"Input manifest labels mismatch: expected {sorted(REQUIRED_INPUTS)}"
        )
    pins = {label: _parse_pin(label, value) for label, value in inputs.items()}
    if payload["blender_version"] != EXPECTED_BLENDER_VERSION:
        raise ContractError("Input manifest Blender version mismatch")
    if payload["compiled_bolt_bind"] != EXPECTED_BIND:
        raise ContractError("Compiled MP5 bolt bind does not match the pinned DATA record")
    if payload["topology_contract"] != EXPECTED_TOPOLOGY:
        raise ContractError("Authoritative tag_bolt topology contract mismatch")
    bind = parse_transform(EXPECTED_BIND["transform"], "compiled_bolt_bind.transform")
    return InputContract(path, _sha256(raw_bytes), pins, bind, payload)


def _quat_multiply(
    a: tuple[float, float, float, float],
    b: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def _quat_conjugate(
    value: tuple[float, float, float, float]
) -> tuple[float, float, float, float]:
    return (-value[0], -value[1], -value[2], value[3])


def _quat_rotate(
    rotation: tuple[float, float, float, float],
    vector: tuple[float, float, float],
) -> tuple[float, float, float]:
    result = _quat_multiply(
        _quat_multiply(rotation, (vector[0], vector[1], vector[2], 0.0)),
        _quat_conjugate(rotation),
    )
    return result[0], result[1], result[2]


def compose(parent: Transform, local: Transform) -> Transform:
    scaled = tuple(local.position[i] * parent.scale[i] for i in range(3))
    rotated = _quat_rotate(parent.rotation, scaled)
    position = tuple(parent.position[i] + rotated[i] for i in range(3))
    rotation = _quat_multiply(parent.rotation, local.rotation)
    length = math.sqrt(sum(value * value for value in rotation))
    rotation = tuple(value / length for value in rotation)
    scale = tuple(parent.scale[i] * local.scale[i] for i in range(3))
    return Transform(position, rotation, scale)


def inverse_parent_compose(parent: Transform, desired: Transform) -> Transform:
    inverse_rotation = _quat_conjugate(parent.rotation)
    delta = tuple(desired.position[i] - parent.position[i] for i in range(3))
    unrotated = _quat_rotate(inverse_rotation, delta)
    position = tuple(unrotated[i] / parent.scale[i] for i in range(3))
    rotation = _quat_multiply(inverse_rotation, desired.rotation)
    length = math.sqrt(sum(value * value for value in rotation))
    rotation = tuple(value / length for value in rotation)
    scale = tuple(desired.scale[i] / parent.scale[i] for i in range(3))
    return Transform(position, rotation, scale)


def _almost_equal(a: Transform, b: Transform, tolerance: float = 1e-7) -> bool:
    direct_rotation = all(
        abs(a.rotation[index] - b.rotation[index]) <= tolerance for index in range(4)
    )
    negated_rotation = all(
        abs(a.rotation[index] + b.rotation[index]) <= tolerance for index in range(4)
    )
    return (
        all(abs(a.position[index] - b.position[index]) <= tolerance for index in range(3))
        and (direct_rotation or negated_rotation)
        and all(abs(a.scale[index] - b.scale[index]) <= tolerance for index in range(3))
    )


def _format_number(value: float) -> str:
    if abs(value) < 5e-10:
        return "0"
    return format(value, ".9g")


def transform_payload(value: Transform) -> dict[str, str]:
    return {
        "position": ",".join(_format_number(number) for number in value.position),
        "rotation": ",".join(_format_number(number) for number in value.rotation),
        "scale": ",".join(_format_number(number) for number in value.scale),
    }


def index_prefab(prefab: Mapping[str, Any], label: str) -> PrefabIndex:
    root = prefab.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError(f"{label} has no RootObject")
    nodes: dict[str, dict[str, Any]] = {}
    node_guids: dict[str, str] = {}
    component_guids: dict[str, tuple[str, dict[str, Any]]] = {}

    def visit(node: dict[str, Any], parent: str) -> None:
        name = node.get("Name")
        guid = node.get("__guid")
        children = node.get("Children")
        components = node.get("Components")
        if not isinstance(name, str) or not name or not isinstance(guid, str):
            raise ContractError(f"{label} contains a malformed node")
        if not isinstance(children, list) or not isinstance(components, list):
            raise ContractError(f"{label} node {name} has malformed children/components")
        path = parent + "/" + name if parent else name
        if path in nodes or guid in node_guids:
            raise ContractError(f"{label} contains duplicate path/GUID at {path}")
        nodes[path] = node
        node_guids[guid] = path
        for component in components:
            if not isinstance(component, dict) or not isinstance(component.get("__guid"), str):
                raise ContractError(f"{label} node {path} has a malformed component")
            component_guid = component["__guid"]
            if component_guid in component_guids or component_guid in node_guids:
                raise ContractError(f"{label} has duplicate component GUID {component_guid}")
            component_guids[component_guid] = path, component
        for child in children:
            if not isinstance(child, dict):
                raise ContractError(f"{label} node {path} has a non-object child")
            visit(child, path)

    visit(root, "")
    return PrefabIndex(nodes, node_guids, component_guids)


def _renderer(node: Mapping[str, Any], model: str, label: str) -> dict[str, Any]:
    matches = [
        component
        for component in node.get("Components", [])
        if component.get("__type") in {"Sandbox.ModelRenderer", "Sandbox.SkinnedModelRenderer"}
        and component.get("Model") == model
    ]
    if len(matches) != 1:
        raise ContractError(f"{label} expected exactly one renderer for {model}, got {len(matches)}")
    return matches[0]


def _unique_renderer(prefab: Mapping[str, Any], model: str, label: str) -> dict[str, Any]:
    index = index_prefab(prefab, label)
    matches = [
        component
        for _, component in index.component_guids.values()
        if component.get("__type") in {"Sandbox.ModelRenderer", "Sandbox.SkinnedModelRenderer"}
        and component.get("Model") == model
    ]
    if len(matches) != 1:
        raise ContractError(f"{label} expected one renderer for {model}, got {len(matches)}")
    return matches[0]


def _node_transform(node: Mapping[str, Any], label: str) -> Transform:
    return parse_transform(
        {key: node.get(key.capitalize()) for key in ("position", "rotation", "scale")},
        label,
    )


def _set_node_transform(node: dict[str, Any], value: Transform) -> None:
    payload = transform_payload(value)
    node["Position"] = payload["position"]
    node["Rotation"] = payload["rotation"]
    node["Scale"] = payload["scale"]


def audit_base_prefab(prefab: Mapping[str, Any]) -> dict[str, Any]:
    index = index_prefab(prefab, "base fit candidate")
    required = {
        ASSEMBLY_PATH,
        BOLT_PARENT_PATH,
        MAGAZINE_PATH,
        BODY_PATH,
        SELECTOR_PATH,
        TRIGGER_PATH,
        STOCK_PATH,
    }
    missing = sorted(required - set(index.nodes))
    if missing:
        raise ContractError(f"Base candidate is missing required nodes: {missing}")
    if prefab["RootObject"].get("Name") != ROOT_NAME:
        raise ContractError("Base candidate root name changed")
    donor = _unique_renderer(prefab, DONOR_MODEL, "base fit candidate")
    if donor.get("CreateBoneObjects") is not True or donor.get("UseAnimGraph") is not True:
        raise ContractError("Base candidate MP5 donor no longer owns bone objects/animation graph")
    body_renderer = _renderer(index.nodes[BODY_PATH], CURRENT_BODY_MODEL, "base body")
    _renderer(index.nodes[MAGAZINE_PATH], CURRENT_MAG_MODEL, "base magazine")
    if index.nodes[BOLT_PARENT_PATH]["Children"]:
        raise ContractError("Base MP5 bolt node is not empty")
    if any(node.get("Name") == "aks74u_bolt" for node in index.nodes.values()):
        raise ContractError("Base candidate already contains an AKS bolt renderer")
    for path in (SELECTOR_PATH, TRIGGER_PATH, STOCK_PATH):
        if index.nodes[path]["Children"]:
            raise ContractError(f"Out-of-scope generated node unexpectedly has children: {path}")
    return {
        "index": index,
        "body_renderer": body_renderer,
        "body_transform": _node_transform(index.nodes[BODY_PATH], "base body transform"),
        "magazine_snapshot": copy.deepcopy(index.nodes[MAGAZINE_PATH]),
        "node_count": len(index.nodes),
    }


def _bolt_node(renderer_template: Mapping[str, Any], local: Transform) -> dict[str, Any]:
    renderer = copy.deepcopy(renderer_template)
    renderer["__type"] = "Sandbox.ModelRenderer"
    renderer["__guid"] = BOLT_RENDERER_GUID
    renderer["Model"] = BOLT_MODEL
    renderer.pop("BoneMergeTarget", None)
    renderer.pop("CreateBoneObjects", None)
    renderer.pop("Morphs", None)
    renderer.pop("Parameters", None)
    renderer.pop("UseAnimGraph", None)
    node = {
        "__guid": BOLT_NODE_GUID,
        "__version": 1,
        "Flags": 0,
        "Name": "aks74u_bolt",
        "Position": "0,0,0",
        "Rotation": "0,0,0,1",
        "Scale": "1,1,1",
        "Tags": "",
        "Enabled": True,
        "NetworkMode": 2,
        "NetworkInterpolation": True,
        "NetworkOrphaned": 0,
        "OwnerTransfer": 1,
        "Components": [renderer],
        "Children": [],
    }
    _set_node_transform(node, local)
    return node


def build_candidate_prefab(
    base: Mapping[str, Any], bolt_parent_bind: Transform
) -> tuple[dict[str, Any], dict[str, Any]]:
    base_audit = audit_base_prefab(base)
    desired = base_audit["body_transform"]
    local = inverse_parent_compose(bolt_parent_bind, desired)
    round_trip = compose(bolt_parent_bind, local)
    if not _almost_equal(round_trip, desired, tolerance=1e-7):
        raise ContractError("W = inverse(P) * B failed its P * W = B round trip")

    candidate = copy.deepcopy(base)
    index = index_prefab(candidate, "candidate before mutation")
    body_renderer = _renderer(index.nodes[BODY_PATH], CURRENT_BODY_MODEL, "candidate body")
    renderer_template = copy.deepcopy(body_renderer)
    body_renderer["Model"] = BODY_MINUS_MODEL
    index.nodes[BOLT_PARENT_PATH]["Children"].append(_bolt_node(renderer_template, local))

    final_index = index_prefab(candidate, "final candidate")
    _renderer(final_index.nodes[BODY_PATH], BODY_MINUS_MODEL, "final body")
    _renderer(final_index.nodes[BOLT_PARENT_PATH + "/aks74u_bolt"], BOLT_MODEL, "final bolt")
    _renderer(final_index.nodes[MAGAZINE_PATH], CURRENT_MAG_MODEL, "final magazine")
    if final_index.nodes[MAGAZINE_PATH] != base_audit["magazine_snapshot"]:
        raise ContractError("Magazine mapping changed while adding the bolt")

    reverted = copy.deepcopy(candidate)
    reverted_index = index_prefab(reverted, "candidate graph-diff proof")
    reverted_index.nodes[BOLT_PARENT_PATH]["Children"] = []
    _renderer(reverted_index.nodes[BODY_PATH], BODY_MINUS_MODEL, "reverted body")["Model"] = CURRENT_BODY_MODEL
    if reverted != base:
        raise ContractError("Candidate graph changed outside the body-model swap and bolt child")

    return candidate, {
        "base_node_count": base_audit["node_count"],
        "candidate_node_count": len(final_index.nodes),
        "body_assembly_B": transform_payload(desired),
        "mp5_bolt_parent_bind_P": transform_payload(bolt_parent_bind),
        "bolt_renderer_local_W": transform_payload(local),
        "round_trip_P_times_W": transform_payload(round_trip),
        "transform_law": "W = inverse(P) * B; P * W = B",
        "body_model_swap": {"from": CURRENT_BODY_MODEL, "to": BODY_MINUS_MODEL},
        "bolt_parent": BOLT_PARENT_PATH,
        "bolt_node": BOLT_PARENT_PATH + "/aks74u_bolt",
        "bolt_model": BOLT_MODEL,
        "magazine_mapping_byte_semantics_preserved": True,
        "selector_trigger_stock_unchanged": True,
        "only_expected_graph_changes": True,
    }


def _modeldoc(filename: str, object_name: str) -> bytes:
    text = f'''<!-- kv3 encoding:text:version{{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}} format:modeldoc30:version{{8c2d7a91-9c42-4bf0-883a-5a3b1762d4f1}} -->
{{
\trootNode =
\t{{
\t\t_class = "RootNode"
\t\tchildren =
\t\t[
\t\t\t{{
\t\t\t\t_class = "MaterialGroupList"
\t\t\t\tchildren =
\t\t\t\t[
\t\t\t\t\t{{
\t\t\t\t\t\t_class = "DefaultMaterialGroup"
\t\t\t\t\t\tremaps =
\t\t\t\t\t\t[
\t\t\t\t\t\t\t{{
\t\t\t\t\t\t\t\tfrom = "{SOURCE_MATERIAL}"
\t\t\t\t\t\t\t\tto = "{BODY_MATERIAL}"
\t\t\t\t\t\t\t}},
\t\t\t\t\t\t]
\t\t\t\t\t\tuse_global_default = false
\t\t\t\t\t\tglobal_default_material = "materials/default.vmat"
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t\t{{
\t\t\t\t_class = "RenderMeshList"
\t\t\t\tchildren =
\t\t\t\t[
\t\t\t\t\t{{
\t\t\t\t\t\t_class = "RenderMeshFile"
\t\t\t\t\t\tfilename = "{filename}"
\t\t\t\t\t\timport_translation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_rotation = [ 0.0, 0.0, 0.0 ]
\t\t\t\t\t\timport_scale = 0.3937008
\t\t\t\t\t\talign_origin_x_type = "None"
\t\t\t\t\t\talign_origin_y_type = "None"
\t\t\t\t\t\talign_origin_z_type = "None"
\t\t\t\t\t\tparent_bone = ""
\t\t\t\t\t\timport_filter =
\t\t\t\t\t\t{{
\t\t\t\t\t\t\texclude_by_default = true
\t\t\t\t\t\t\texception_list =
\t\t\t\t\t\t\t[
\t\t\t\t\t\t\t\t"{object_name}",
\t\t\t\t\t\t\t]
\t\t\t\t\t\t}}
\t\t\t\t\t}},
\t\t\t\t]
\t\t\t}},
\t\t]
\t\tmodel_archetype = ""
\t\tprimary_associated_entity = ""
\t\tanim_graph_name = ""
\t\tbase_model_name = ""
\t}}
}}
'''
    return text.encode("utf-8")


def _verify_blender_version(executable: Path) -> str:
    result = subprocess.run(
        [str(executable), "--version"],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=30,
    )
    first = result.stdout.splitlines()[0].strip() if result.stdout else ""
    if result.returncode != 0 or first != EXPECTED_BLENDER_VERSION:
        raise ContractError(
            f"Blender version mismatch: expected {EXPECTED_BLENDER_VERSION!r}, got {first!r}"
        )
    return first


def _run_blender_worker(contract: InputContract, output: Path, worker: Path) -> str:
    source = contract.pins["authoritative_fbx"]
    blender = contract.pins["blender_executable"].path
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(worker),
        "--",
        "--source",
        str(source.path),
        "--output-dir",
        str(output),
        "--source-bytes",
        str(source.bytes),
        "--source-sha256",
        source.sha256,
    ]
    environment = os.environ.copy()
    environment["PYTHONHASHSEED"] = "0"
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    result = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=300,
        env=environment,
    )
    if result.returncode != 0:
        raise ContractError(
            "Blender split worker failed:\n" + result.stdout[-4000:] + result.stderr[-4000:]
        )
    marker = [line for line in result.stdout.splitlines() if line.startswith("DXRP_AKS74U_BOLT_SPLIT=")]
    if len(marker) != 1:
        raise ContractError("Blender split worker did not emit exactly one success marker")
    return marker[0]


def _write_exclusive(path: Path, data: bytes) -> None:
    _assert_no_reparse_components(path)
    temp_root = Path(tempfile.gettempdir()).resolve(strict=True)
    resolved_parent = path.parent.resolve(strict=True)
    destination = resolved_parent / path.name
    if not _is_within(destination, temp_root) or destination == temp_root:
        raise ContractError(f"Refusing non-TEMP output: {destination}")
    with destination.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def _pin_path(path: Path, relative_to: Path) -> dict[str, Any]:
    data = path.read_bytes()
    return {
        "relative_path": path.relative_to(relative_to).as_posix(),
        "bytes": len(data),
        "sha256": _sha256(data),
    }


def _validate_topology_report(output: Path) -> dict[str, Any]:
    path = output / "evidence" / "bolt_split_topology.json"
    try:
        report = json.loads(path.read_bytes())
    except (OSError, json.JSONDecodeError) as exc:
        raise ContractError("Blender topology report is missing or invalid") from exc
    partition = report.get("partition", {})
    expected = {
        "body_minus_bolt_vertices": EXPECTED_TOPOLOGY["body_minus_bolt_vertices"],
        "bolt_vertices": EXPECTED_TOPOLOGY["bolt_vertices"],
        "vertex_intersection": 0,
        "vertex_union": EXPECTED_TOPOLOGY["source_vertices"],
        "body_minus_bolt_faces": EXPECTED_TOPOLOGY["body_minus_bolt_faces"],
        "bolt_faces": EXPECTED_TOPOLOGY["bolt_faces"],
        "face_intersection": 0,
        "face_union": EXPECTED_TOPOLOGY["source_faces"],
        "welded_position_edge_islands_q1e-6m": 3,
        "welded_position_edge_island_face_counts": [350, 291, 189],
    }
    for key, value in expected.items():
        if partition.get(key) != value:
            raise ContractError(f"Topology proof mismatch for partition.{key}")
    post = report.get("post_export_reimport", {})
    required_true = {
        "each_part_matches_pre_export",
        "vertex_multiset_closure",
        "face_position_uv_material_multiset_closure",
    }
    if any(post.get(key) is not True for key in required_true):
        raise ContractError("FBX round-trip topology closure is not proven")
    if post.get("duplicate_bolt_in_body") is not False:
        raise ContractError("Topology proof did not exclude a duplicate bolt in the body")
    for label, relative in {
        "body_minus_bolt_fbx": "meshes/aks74u_body_minus_bolt.fbx",
        "bolt_fbx": "meshes/aks74u_bolt.fbx",
    }.items():
        pin = report.get("outputs", {}).get(label, {})
        path = output / relative
        actual = _pin_path(path, output)
        if pin != actual:
            raise ContractError(f"Blender output pin mismatch for {label}")
    return report


def build_bundle(
    manifest_path: Path,
    output_directory: Path,
    *,
    worker_path: Path | None = None,
) -> dict[str, Any]:
    contract = load_input_contract(manifest_path)
    output = assert_temp_output_directory(output_directory)
    if any(_is_within(pin.path, output) for pin in contract.pins.values()):
        raise ContractError("An input file overlaps the output directory")
    if output.exists():
        if any(output.iterdir()):
            raise ContractError("Output directory must remain empty before build")
    else:
        output.mkdir(parents=False, exist_ok=False)
    worker = worker_path or Path(__file__).with_name("blender_split_aks74u_bolt.py")
    _assert_no_reparse_components(worker)
    worker = worker.resolve(strict=True)
    if not worker.is_file():
        raise ContractError(f"Blender worker is missing: {worker}")
    blender_version = _verify_blender_version(contract.pins["blender_executable"].path)

    base_bytes = contract.pins["base_fit_candidate_prefab"].path.read_bytes()
    try:
        base_prefab = json.loads(base_bytes)
    except json.JSONDecodeError as exc:
        raise ContractError("Base fit candidate prefab is not JSON") from exc
    candidate, graph = build_candidate_prefab(base_prefab, contract.compiled_bind)

    _run_blender_worker(contract, output, worker)
    topology = _validate_topology_report(output)
    modeldocs = output / "modeldocs"
    prefabs = output / "prefabs"
    modeldocs.mkdir(exist_ok=False)
    prefabs.mkdir(exist_ok=False)
    _write_exclusive(
        modeldocs / "aks74u_body_minus_bolt.vmdl",
        _modeldoc(BODY_FBX_ASSET, "SK_Rif_SLR_AK47_body_minus_bolt"),
    )
    _write_exclusive(
        modeldocs / "aks74u_bolt.vmdl",
        _modeldoc(BOLT_FBX_ASSET, "SK_Rif_SLR_AK47_bolt"),
    )
    _write_exclusive(
        prefabs / "vm_aks74u.bolt-split-candidate.prefab",
        _json_bytes(candidate),
    )

    artifact_paths = sorted(
        path for path in output.rglob("*") if path.is_file()
    )
    artifacts = {
        path.relative_to(output).as_posix(): _pin_path(path, output)
        for path in artifact_paths
    }
    code_paths = [Path(__file__).resolve(), worker]
    sibling_test = Path(__file__).with_name("test_build_aks74u_bolt_split_candidate.py")
    if sibling_test.is_file():
        code_paths.append(sibling_test.resolve())
    code_pins = {
        path.name: {
            "path": str(path),
            "bytes": path.stat().st_size,
            "sha256": _sha256(path.read_bytes()),
        }
        for path in code_paths
    }
    input_pins = {
        label: {
            "path": str(pin.path),
            "bytes": pin.bytes,
            "sha256": pin.sha256,
        }
        for label, pin in sorted(contract.pins.items())
    }
    bundle_manifest = {
        "version": 1,
        "purpose": PURPOSE,
        "mode": "STRICT_SYSTEM_TEMP_ONLY_NO_PRODUCT_WRITE_SWITCH",
        "input_manifest": {
            "path": str(contract.manifest_path),
            "bytes": contract.manifest_path.stat().st_size,
            "sha256": contract.manifest_sha256,
        },
        "input_pins": input_pins,
        "pipeline_code_pins": code_pins,
        "blender": {
            "version": blender_version,
            "executable_pin": input_pins["blender_executable"],
        },
        "compiled_mp5_bolt_bind": contract.raw["compiled_bolt_bind"],
        "topology_contract": contract.raw["topology_contract"],
        "topology_result": {
            "source": topology["source"],
            "partition": topology["partition"],
            "pre_export": topology["pre_export"],
            "post_export_reimport": topology["post_export_reimport"],
        },
        "first_person_prefab_graph": graph,
        "materials": {
            "source_slot": SOURCE_MATERIAL,
            "modeldoc_remap": BODY_MATERIAL,
            "pinned_material_stack": {
                label: input_pins[label]
                for label in (
                    "body_material",
                    "body_basecolor",
                    "body_normal",
                    "body_ao",
                    "body_metalness",
                    "body_roughness",
                )
            },
        },
        "artifacts": artifacts,
        "promotion_map": {
            "meshes/aks74u_body_minus_bolt.fbx": "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_body_minus_bolt.fbx",
            "meshes/aks74u_bolt.fbx": "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_bolt.fbx",
            "modeldocs/aks74u_body_minus_bolt.vmdl": "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl",
            "modeldocs/aks74u_bolt.vmdl": "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl",
            "prefabs/vm_aks74u.bolt-split-candidate.prefab": "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab",
        },
        "scope": {
            "first_person_only": True,
            "split_only_tag_bolt": True,
            "selector_trigger_stock_split": False,
            "third_person_created_or_modified": False,
            "magazine_mapping_changed": False,
        },
        "proof_ceiling": [
            "PASS source byte custody",
            "PASS deterministic rigid tag_bolt partition and common-origin FBX re-import closure",
            "PASS static prefab graph and W = inverse(P) * B round trip",
            "PASS strict system-TEMP containment",
            "UNVERIFIED ModelDoc asset compile because definitions remain outside the game content tree",
            "UNVERIFIED code compile",
            "UNVERIFIED editor/hotload/runtime/visual bolt motion",
            "UNVERIFIED animation graph state selection and movement direction",
            "NO third-person motion candidate or claim",
        ],
    }
    manifest_output = output / "bundle_manifest.json"
    _write_exclusive(manifest_output, _json_bytes(bundle_manifest))
    return {
        "output_directory": str(output),
        "bundle_manifest": str(manifest_output),
        "manifest": bundle_manifest,
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build a byte-pinned AKS-74U first-person bolt split below strict system TEMP. "
            "There is intentionally no product-write switch."
        )
    )
    parser.add_argument("--input-manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        result = build_bundle(args.input_manifest, args.output_dir)
    except (ContractError, OSError, subprocess.SubprocessError) as exc:
        print(f"DXRP_AKS74U_BOLT_CANDIDATE_ERROR: {exc}", file=sys.stderr)
        return 2
    print(
        "DXRP_AKS74U_BOLT_CANDIDATE="
        + json.dumps(
            {
                "mode": result["manifest"]["mode"],
                "output_directory": result["output_directory"],
                "bundle_manifest": result["bundle_manifest"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
