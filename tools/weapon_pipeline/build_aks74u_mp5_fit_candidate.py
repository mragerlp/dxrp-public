"""Build a TEMP-ONLY AKS-74U-on-MP5 structural fit candidate.

This tool never writes below the repository.  It reads the byte-pinned current
AKS-74U and MP5 prefabs, validates their animation ownership, and writes two
temporary candidate prefabs plus a deterministic report below the operating
system's temporary directory.

The AKS-74U body and magazine are filtered from one common-origin FBX but live
under different first-person parents.  The body assembly transform is ``B``.
The MP5 magazine parent's explicit bind transform is ``P``.  The
magazine renderer local must therefore be:

    W = inverse(P) * B

An explicit bind manifest is mandatory.  It may carry a directly observed idle
sample or byte-pinned compiled-model bind evidence.  Serialized generated bone
objects use identity placeholders, so this tool deliberately refuses to infer
``P`` from prefab JSON or from the current custom renderer transforms.  A
compiled bind remains a static candidate input, never visual IdlePose proof.

Muzzle, ejection, and support-hand positions are reported as source-semantic
seeds only.  They are not wired into either candidate because their rotations,
rendered fit, and third-person hand pose have not been accepted.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import stat
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


class ContractError(RuntimeError):
    """Raised when source custody or the candidate contract is not satisfied."""


REPO_ROOT = Path(__file__).resolve().parents[2]
GAME_ASSETS_ROOT = REPO_ROOT / "game" / "Assets"

DONOR_VM_REL = "game/Assets/gameplay/equipment/weapons/mp5/vm_mp5.prefab"
DONOR_W_REL = "game/Assets/gameplay/equipment/weapons/mp5/w_mp5.prefab"
TARGET_VM_REL = (
    "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab"
)
TARGET_W_REL = (
    "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab"
)
SOURCE_FBX_REL = (
    "game/Assets/addons/lifepunch/lpweapons/aks74u/source/fab_original/aks74u.fbx"
)
BODY_VMDL_REL = "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl"
MAG_VMDL_REL = "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl"
COMBINED_VMDL_REL = "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.vmdl"
INVISIBLE_VMAT_REL = (
    "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/invisible.vmat"
)
WEPANIM_REL = "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.wepanim"

DONOR_VM_MODEL = "models/weapons/sbox_smg_mp5/v_mp5.vmdl"
DONOR_W_MODEL = "models/weapons/sbox_smg_mp5/w_mp5.vmdl"
BODY_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl"
MAG_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl"
COMBINED_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u.vmdl"
INVISIBLE_VMAT = "addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/invisible.vmat"
SOURCE_FBX_ASSET = "addons/lifepunch/lpweapons/aks74u/source/fab_original/aks74u.fbx"

COMPILED_BIND_PARSER_PATH = Path(
    r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe"
)
COMPILED_BIND_PARSER_BYTES = 108_603_232
COMPILED_BIND_PARSER_SHA256 = (
    "36d8c9208eefa61dd695bd577e49618bb161569941318f629294a4e4af00edc0"
)
COMPILED_BIND_PARSER_VERSION = (
    "19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa"
)
COMPILED_BIND_MODEL_PATH = Path(
    r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons\sbox_smg_mp5\v_mp5.bb3ccbb95b323f14.vmdl_c"
)
COMPILED_BIND_MODEL_BYTES = 1_602_903
COMPILED_BIND_MODEL_SHA256 = (
    "87b8e0757bb7ae6b8b784a261449dabcd9a37125a0ac8b420c115244f65b4f24"
)
COMPILED_BIND_SENSOR = (
    "Source2Viewer-CLI 19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa "
    "-b DATA stdout"
)

VM_BODY_PATH = "weapon_root/weapon_root_children/aks74u_body"
VM_MAG_PARENT_PATH = "weapon_root/weapon_root_children/magazine"
VM_MAG_PATH = VM_MAG_PARENT_PATH + "/aks74u_magazine"
VM_ASSEMBLY_PARENT_PATH = "weapon_root/weapon_root_children"
W_MODEL_PATH = "Model"
W_CUSTOM_PATH = "Model/aks74u_mesh"


@dataclass(frozen=True)
class SourcePin:
    relative_path: str
    bytes: int
    sha256: str


SOURCE_PINS: Mapping[str, SourcePin] = {
    "aks74u_source_fbx": SourcePin(
        SOURCE_FBX_REL,
        2_020_924,
        "6edab0636b1d43744a0344ff9168766229d3d0dd68e845e7c21edb9971b34780",
    ),
    "aks74u_body_vmdl": SourcePin(
        BODY_VMDL_REL,
        1_240,
        "81d746f3527c59c7c08a1b5d23634d0ac826e67019713ab4eb4016e518980a06",
    ),
    "aks74u_mag_vmdl": SourcePin(
        MAG_VMDL_REL,
        1_258,
        "2f2d3bf405bdd0cb18fe79b7a4252cbc8f3d828fc44fc8ce5c223a4a4b253089",
    ),
    "aks74u_combined_vmdl": SourcePin(
        COMBINED_VMDL_REL,
        1_279,
        "be4faee555c240cc16257cf5d180543555a05e1a3581e874459c0df74a51b49d",
    ),
    "aks74u_vm_prefab": SourcePin(
        TARGET_VM_REL,
        74_777,
        "d125a7298ccc00cf10f2d6f817a009d7b807515bfa3953ce1c6784cf303f5551",
    ),
    "aks74u_w_prefab": SourcePin(
        TARGET_W_REL,
        17_021,
        "3d675df533a4f107e0a4d591af2db81f86bf7ce422ebfd67e48d833ae459a99c",
    ),
    "mp5_vm_prefab": SourcePin(
        DONOR_VM_REL,
        70_863,
        "cefd224b7b8fa4325090691afa66a5ee429566ff372ad526c8afd98c20585bd2",
    ),
    "mp5_w_prefab": SourcePin(
        DONOR_W_REL,
        14_998,
        "ae8c567ef8bc3ccb4462db4d22f80061334030c6e813153fa58274cbc6927ac0",
    ),
    "aks74u_invisible_vmat": SourcePin(
        INVISIBLE_VMAT_REL,
        387,
        "19e2cb707efd42a88f0c826dee12b90e4ad7365ccb92a72d92ca6a589b3c2746",
    ),
    "aks74u_wepanim": SourcePin(
        WEPANIM_REL,
        16_647,
        "2440c19b1bc4ba5c21e4efca008823fd70d4aae90110f43d8b5072189efbc1da",
    ),
}


@dataclass(frozen=True)
class Bounds:
    mins: tuple[float, float, float]
    maxs: tuple[float, float, float]

    @property
    def center(self) -> tuple[float, float, float]:
        return tuple((low + high) * 0.5 for low, high in zip(self.mins, self.maxs))

    @property
    def size(self) -> tuple[float, float, float]:
        return tuple(high - low for low, high in zip(self.mins, self.maxs))


# Fresh read-only inspect_model_geometry evidence, engine 26.08.19, 2026-08-25.
# These are model-local/default-pose render bounds; no GameObject scale is applied.
AKS_BODY_BOUNDS = Bounds(
    (-11.815158, -1.4113433, -4.5465417),
    (13.380316, 0.89958227, 2.726789),
)
AKS_MAG_BOUNDS = Bounds(
    (1.9912974, -0.46748447, -6.609273),
    (7.3049474, 0.42559403, 0.32358548),
)
AKS_COMBINED_BOUNDS = Bounds(
    (-11.815158, -1.4113433, -6.609273),
    (13.380316, 0.89958227, 2.726789),
)
MP5_VM_BOUNDS = Bounds(
    (-9.569484, -1.4709549, 0.05037793),
    (12.998839, 1.2833948, 10.777188),
)
MP5_W_BOUNDS = Bounds(
    (-9.58773, -1.4709423, 0.036378384),
    (12.998868, 1.2833805, 10.855452),
)


# Source-engine coordinates reconstructed from the pinned FBX using Blender
# 5.1.2.  Positions are bone heads in model-local inches after the same axis and
# unit conversion used by the pinned ModelDocs.  Orientation remains unaccepted.
SOURCE_ANCHORS: Mapping[str, tuple[float, float, float]] = {
    "muzzle_tag_muzzle": (13.435203, 0.0, 0.523395),
    "ejection_tag_brass": (4.082232, -0.417947, 1.28325),
    "left_grip_seed_tag_shroud": (6.451639, 0.0, 0.523393),
}


@dataclass(frozen=True)
class Transform:
    position: tuple[float, float, float]
    rotation: tuple[float, float, float, float]  # x, y, z, w
    scale: tuple[float, float, float]


IDENTITY = Transform((0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0), (1.0, 1.0, 1.0))

COMPILED_BIND_TRANSFORM_PAYLOAD: Mapping[str, str] = {
    "position": "5.020371,0.000001,0.410289",
    "rotation": "0,0.707106,0,0.707107",
    "scale": "1,1,1",
}


def compiled_bind_evidence_template() -> dict[str, Any]:
    return {
        "parser": {
            "path": str(COMPILED_BIND_PARSER_PATH),
            "bytes": COMPILED_BIND_PARSER_BYTES,
            "sha256": COMPILED_BIND_PARSER_SHA256,
            "version": COMPILED_BIND_PARSER_VERSION,
        },
        "compiled_model": {
            "path": str(COMPILED_BIND_MODEL_PATH),
            "bytes": COMPILED_BIND_MODEL_BYTES,
            "sha256": COMPILED_BIND_MODEL_SHA256,
        },
        "command_mode": "-b DATA",
        "record": {
            "bone": "magazine",
            "parent": "weapon_root_children",
            "transform": dict(COMPILED_BIND_TRANSFORM_PAYLOAD),
        },
    }


def _verify_external_pin(
    path: Path, expected_bytes: int, expected_sha256: str, label: str
) -> None:
    if not path.is_file():
        raise ContractError(f"Pinned compiled-bind {label} is missing: {path}")
    data = path.read_bytes()
    if len(data) != expected_bytes:
        raise ContractError(
            f"Pinned compiled-bind {label} byte mismatch: "
            f"expected {expected_bytes}, got {len(data)}"
        )
    actual_sha256 = _sha256(data)
    if actual_sha256 != expected_sha256:
        raise ContractError(
            f"Pinned compiled-bind {label} SHA-256 mismatch: "
            f"expected {expected_sha256}, got {actual_sha256}"
        )


@dataclass(frozen=True)
class IdleBindManifest:
    transform: Transform
    sensor: str
    sha256: str
    sample_kind: str
    evidence: Mapping[str, Any] | None


@dataclass(frozen=True)
class PrefabIndex:
    nodes: Mapping[str, dict[str, Any]]
    nodes_by_guid: Mapping[str, str]
    components_by_guid: Mapping[str, tuple[str, dict[str, Any]]]


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _source_path(pin: SourcePin) -> Path:
    return REPO_ROOT / Path(pin.relative_path)


def validate_source_pins() -> dict[str, bytes]:
    payloads: dict[str, bytes] = {}
    for name, pin in SOURCE_PINS.items():
        path = _source_path(pin)
        if not path.is_file():
            raise ContractError(f"Pinned source is missing: {pin.relative_path}")
        data = path.read_bytes()
        if len(data) != pin.bytes:
            raise ContractError(
                f"Pinned source byte mismatch for {pin.relative_path}: "
                f"expected {pin.bytes}, got {len(data)}"
            )
        actual = _sha256(data)
        if actual != pin.sha256:
            raise ContractError(
                f"Pinned source SHA-256 mismatch for {pin.relative_path}: "
                f"expected {pin.sha256}, got {actual}"
            )
        payloads[name] = data
    return payloads


def _parse_numbers(value: Any, count: int, label: str) -> tuple[float, ...]:
    if not isinstance(value, str):
        raise ContractError(f"{label} must be a comma-separated string")
    fields = value.split(",")
    if len(fields) != count:
        raise ContractError(f"{label} must contain exactly {count} values")
    try:
        result = tuple(float(field.strip()) for field in fields)
    except ValueError as exc:
        raise ContractError(f"{label} contains a non-numeric value") from exc
    if not all(math.isfinite(item) for item in result):
        raise ContractError(f"{label} must contain only finite values")
    return result


def parse_transform(payload: Any, label: str) -> Transform:
    if not isinstance(payload, dict):
        raise ContractError(f"{label} must be an object")
    expected = {"position", "rotation", "scale"}
    if set(payload) != expected:
        raise ContractError(
            f"{label} keys must be exactly {sorted(expected)}; got {sorted(payload)}"
        )
    position = _parse_numbers(payload["position"], 3, f"{label}.position")
    rotation = _parse_numbers(payload["rotation"], 4, f"{label}.rotation")
    scale = _parse_numbers(payload["scale"], 3, f"{label}.scale")
    q_length = math.sqrt(sum(value * value for value in rotation))
    if not math.isclose(q_length, 1.0, abs_tol=1e-5):
        raise ContractError(f"{label}.rotation must be a normalized quaternion")
    if min(scale) <= 0.0:
        raise ContractError(f"{label}.scale must be positive")
    if max(scale) - min(scale) > 1e-5:
        raise ContractError(f"{label}.scale must be uniform")
    return Transform(
        tuple(position),
        _canonical_quaternion(tuple(value / q_length for value in rotation)),
        tuple(scale),
    )


def _canonical_quaternion(
    value: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    if value[3] < 0.0:
        return tuple(-part for part in value)
    return value


def _quat_conjugate(
    value: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    x, y, z, w = value
    return (-x, -y, -z, w)


def _quat_multiply(
    left: tuple[float, float, float, float],
    right: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    lx, ly, lz, lw = left
    rx, ry, rz, rw = right
    result = (
        lw * rx + lx * rw + ly * rz - lz * ry,
        lw * ry - lx * rz + ly * rw + lz * rx,
        lw * rz + lx * ry - ly * rx + lz * rw,
        lw * rw - lx * rx - ly * ry - lz * rz,
    )
    length = math.sqrt(sum(part * part for part in result))
    if length == 0.0:
        raise ContractError("Quaternion composition produced zero length")
    return _canonical_quaternion(tuple(part / length for part in result))


def _quat_rotate(
    rotation: tuple[float, float, float, float],
    point: tuple[float, float, float],
) -> tuple[float, float, float]:
    x, y, z, w = rotation
    px, py, pz = point
    # Optimized q * p * conjugate(q).
    tx = 2.0 * (y * pz - z * py)
    ty = 2.0 * (z * px - x * pz)
    tz = 2.0 * (x * py - y * px)
    return (
        px + w * tx + (y * tz - z * ty),
        py + w * ty + (z * tx - x * tz),
        pz + w * tz + (x * ty - y * tx),
    )


def compose(parent: Transform, child: Transform) -> Transform:
    scaled_child = tuple(
        child.position[index] * parent.scale[index] for index in range(3)
    )
    rotated_child = _quat_rotate(parent.rotation, scaled_child)
    position = tuple(
        parent.position[index] + rotated_child[index] for index in range(3)
    )
    rotation = _quat_multiply(parent.rotation, child.rotation)
    scale = tuple(
        parent.scale[index] * child.scale[index] for index in range(3)
    )
    return Transform(position, rotation, scale)


def inverse_parent_compose(parent: Transform, desired: Transform) -> Transform:
    """Return local W such that ``compose(parent, W) == desired``."""

    inverse_rotation = _quat_conjugate(parent.rotation)
    delta = tuple(
        desired.position[index] - parent.position[index] for index in range(3)
    )
    unrotated = _quat_rotate(inverse_rotation, delta)
    position = tuple(unrotated[index] / parent.scale[index] for index in range(3))
    rotation = _quat_multiply(inverse_rotation, desired.rotation)
    scale = tuple(
        desired.scale[index] / parent.scale[index] for index in range(3)
    )
    return Transform(position, rotation, scale)


def transform_point(
    transform: Transform, point: tuple[float, float, float]
) -> tuple[float, float, float]:
    scaled = tuple(point[index] * transform.scale[index] for index in range(3))
    rotated = _quat_rotate(transform.rotation, scaled)
    return tuple(transform.position[index] + rotated[index] for index in range(3))


def derive_centered_length_fit(replacement: Bounds, donor: Bounds) -> Transform:
    replacement_size = replacement.size
    donor_size = donor.size
    if replacement_size[0] <= 0.0 or donor_size[0] <= 0.0:
        raise ContractError("Bounds must have a positive +X length")
    uniform_scale = donor_size[0] / replacement_size[0]
    position = tuple(
        donor.center[index] - replacement.center[index] * uniform_scale
        for index in range(3)
    )
    return Transform(
        position,
        IDENTITY.rotation,
        (uniform_scale, uniform_scale, uniform_scale),
    )


def _format_number(value: float) -> str:
    if abs(value) < 0.5e-8:
        value = 0.0
    rendered = f"{value:.8f}".rstrip("0").rstrip(".")
    return "0" if rendered in {"", "-0"} else rendered


def format_vector(values: Iterable[float]) -> str:
    return ",".join(_format_number(value) for value in values)


def transform_payload(transform: Transform) -> dict[str, str]:
    return {
        "position": format_vector(transform.position),
        "rotation": format_vector(transform.rotation),
        "scale": format_vector(transform.scale),
    }


def _transform_from_node(node: Mapping[str, Any], label: str) -> Transform:
    return parse_transform(
        {
            "position": node.get("Position"),
            "rotation": node.get("Rotation"),
            "scale": node.get("Scale"),
        },
        label,
    )


def _set_node_transform(node: dict[str, Any], transform: Transform) -> None:
    payload = transform_payload(transform)
    node["Position"] = payload["position"]
    node["Rotation"] = payload["rotation"]
    node["Scale"] = payload["scale"]


def _almost_equal_transform(
    left: Transform, right: Transform, *, tolerance: float = 2e-5
) -> bool:
    for left_values, right_values in (
        (left.position, right.position),
        (left.rotation, right.rotation),
        (left.scale, right.scale),
    ):
        if any(
            not math.isclose(a, b, abs_tol=tolerance)
            for a, b in zip(left_values, right_values)
        ):
            return False
    return True


def _read_json_bytes(data: bytes, label: str) -> dict[str, Any]:
    try:
        value = json.loads(data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"{label} is not valid UTF-8 JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise ContractError(f"{label} root must be an object")
    return value


def index_prefab(prefab: Mapping[str, Any], label: str) -> PrefabIndex:
    root = prefab.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError(f"{label} has no RootObject")
    nodes: dict[str, dict[str, Any]] = {}
    nodes_by_guid: dict[str, str] = {}
    components_by_guid: dict[str, tuple[str, dict[str, Any]]] = {}

    def visit(node: dict[str, Any], relative_path: str) -> None:
        if relative_path in nodes:
            raise ContractError(f"{label} has duplicate node path {relative_path!r}")
        nodes[relative_path] = node
        guid = node.get("__guid")
        if not isinstance(guid, str) or not guid:
            raise ContractError(f"{label} node {relative_path!r} lacks a GUID")
        if guid in nodes_by_guid:
            raise ContractError(f"{label} has duplicate GameObject GUID {guid}")
        nodes_by_guid[guid] = relative_path
        components = node.get("Components")
        if not isinstance(components, list):
            raise ContractError(f"{label} node {relative_path!r} has invalid Components")
        for component in components:
            if not isinstance(component, dict):
                raise ContractError(f"{label} node {relative_path!r} has invalid component")
            component_guid = component.get("__guid")
            if not isinstance(component_guid, str) or not component_guid:
                raise ContractError(
                    f"{label} component on {relative_path!r} lacks a GUID"
                )
            if component_guid in components_by_guid:
                raise ContractError(f"{label} has duplicate component GUID {component_guid}")
            components_by_guid[component_guid] = (relative_path, component)
        children = node.get("Children")
        if not isinstance(children, list):
            raise ContractError(f"{label} node {relative_path!r} has invalid Children")
        sibling_names: set[str] = set()
        for child in children:
            if not isinstance(child, dict) or not isinstance(child.get("Name"), str):
                raise ContractError(f"{label} node {relative_path!r} has invalid child")
            name = child["Name"]
            if name in sibling_names:
                raise ContractError(
                    f"{label} has duplicate sibling name {name!r} below {relative_path!r}"
                )
            sibling_names.add(name)
            child_path = f"{relative_path}/{name}" if relative_path else name
            visit(child, child_path)

    visit(root, "")
    return PrefabIndex(nodes, nodes_by_guid, components_by_guid)


def _component_by_type(
    index: PrefabIndex, owner_path: str, component_type: str
) -> dict[str, Any]:
    matches = [
        component
        for path, component in index.components_by_guid.values()
        if path == owner_path and component.get("__type") == component_type
    ]
    if len(matches) != 1:
        raise ContractError(
            f"Expected exactly one {component_type} on {owner_path or '<root>'}; "
            f"found {len(matches)}"
        )
    return matches[0]


def _renderer_by_model(
    index: PrefabIndex, owner_path: str, model: str
) -> dict[str, Any]:
    matches = [
        component
        for path, component in index.components_by_guid.values()
        if path == owner_path
        and component.get("__type")
        in {"Sandbox.ModelRenderer", "Sandbox.SkinnedModelRenderer"}
        and component.get("Model") == model
    ]
    if len(matches) != 1:
        raise ContractError(
            f"Expected one renderer for {model} on {owner_path or '<root>'}; "
            f"found {len(matches)}"
        )
    return matches[0]


def _resolve_gameobject_ref(
    index: PrefabIndex, value: Any, label: str
) -> str:
    if not isinstance(value, dict) or value.get("_type") != "gameobject":
        raise ContractError(f"{label} is not a GameObject reference")
    guid = value.get("go")
    if guid not in index.nodes_by_guid:
        raise ContractError(f"{label} points to missing GameObject {guid}")
    return index.nodes_by_guid[guid]


def _resolve_component_ref(
    index: PrefabIndex, value: Any, label: str
) -> tuple[str, str]:
    if not isinstance(value, dict) or value.get("_type") != "component":
        raise ContractError(f"{label} is not a component reference")
    guid = value.get("component_id")
    if guid not in index.components_by_guid:
        raise ContractError(f"{label} points to missing component {guid}")
    owner_path, component = index.components_by_guid[guid]
    if value.get("go") not in index.nodes_by_guid:
        raise ContractError(f"{label} has a missing owner GameObject")
    if index.nodes_by_guid[value["go"]] != owner_path:
        raise ContractError(f"{label} owner does not match the referenced component")
    actual_type = component.get("__type")
    declared_type = value.get("component_type")
    if not isinstance(actual_type, str) or not actual_type.endswith(str(declared_type)):
        raise ContractError(
            f"{label} component type mismatch: declared {declared_type}, got {actual_type}"
        )
    return owner_path, str(actual_type)


def _assert_donor_nodes_preserved(
    donor: PrefabIndex,
    target: PrefabIndex,
    expected_extra: set[str],
    label: str,
) -> None:
    donor_paths = set(donor.nodes)
    target_paths = set(target.nodes)
    if donor_paths - target_paths:
        raise ContractError(
            f"{label} is missing donor nodes: {sorted(donor_paths - target_paths)}"
        )
    if target_paths - donor_paths != expected_extra:
        raise ContractError(
            f"{label} custom-node set changed: expected {sorted(expected_extra)}, "
            f"got {sorted(target_paths - donor_paths)}"
        )
    fields = (
        "__version",
        "Flags",
        "Position",
        "Rotation",
        "Scale",
        "Tags",
        "Enabled",
        "NetworkMode",
        "NetworkInterpolation",
        "NetworkOrphaned",
        "OwnerTransfer",
    )
    for path in sorted(donor_paths):
        donor_node = donor.nodes[path]
        target_node = target.nodes[path]
        for field in fields:
            if donor_node.get(field) != target_node.get(field):
                raise ContractError(
                    f"{label} donor node {path or '<root>'} changed {field}: "
                    f"{donor_node.get(field)!r} -> {target_node.get(field)!r}"
                )


def _assert_renderer_driver_preserved(
    donor_index: PrefabIndex, target_index: PrefabIndex
) -> None:
    donor = _renderer_by_model(donor_index, "", DONOR_VM_MODEL)
    target = _renderer_by_model(target_index, "", DONOR_VM_MODEL)
    fields = (
        "AnimationGraph",
        "BodyGroups",
        "BoneMergeTarget",
        "CreateAttachments",
        "CreateBoneObjects",
        "LodOverride",
        "MaterialGroup",
        "Materials",
        "Model",
        "Morphs",
        "Parameters",
        "PlaybackRate",
        "RenderOptions",
        "RenderType",
        "Sequence",
        "Tint",
        "UseAnimGraph",
    )
    for field in fields:
        if donor.get(field) != target.get(field):
            raise ContractError(f"MP5 animation driver field changed: {field}")
    if target.get("CreateBoneObjects") is not True:
        raise ContractError("AKS-74U MP5 driver must create donor bone objects")
    if target.get("UseAnimGraph") is not True:
        raise ContractError("AKS-74U MP5 driver must retain the donor animation graph")
    if donor.get("MaterialOverride") is not None:
        raise ContractError("Pinned MP5 donor unexpectedly has a material override")
    if target.get("MaterialOverride") != INVISIBLE_VMAT:
        raise ContractError("AKS-74U MP5 driver is not hidden by the pinned material")


def _assert_viewmodel_contract(
    donor_index: PrefabIndex, target_index: PrefabIndex
) -> None:
    donor = _component_by_type(donor_index, "", "Dxura.RP.Game.ViewModel")
    target = _component_by_type(target_index, "", "Dxura.RP.Game.ViewModel")
    scalar_fields = (
        "CanADS",
        "IronsightsFireScale",
        "renderingEnabled",
        "UseMovementInertia",
    )
    for field in scalar_fields:
        if donor.get(field) != target.get(field):
            raise ContractError(f"AKS-74U ViewModel changed MP5 field {field}")
    expected_refs = {
        "Muzzle": "weapon_root/weapon_root_children/muzzle",
        "EjectionPort": "weapon_root/weapon_root_children/bolt",
    }
    for field, expected_path in expected_refs.items():
        donor_path = _resolve_gameobject_ref(donor_index, donor.get(field), f"donor.{field}")
        target_path = _resolve_gameobject_ref(target_index, target.get(field), f"target.{field}")
        if donor_path != expected_path or target_path != expected_path:
            raise ContractError(
                f"{field} donor ownership changed: donor={donor_path}, target={target_path}"
            )
    for field in ("ModelRenderer", "Arms"):
        donor_owner, donor_type = _resolve_component_ref(
            donor_index, donor.get(field), f"donor.{field}"
        )
        target_owner, target_type = _resolve_component_ref(
            target_index, target.get(field), f"target.{field}"
        )
        if (donor_owner, donor_type) != (target_owner, target_type):
            raise ContractError(f"{field} owner/type drifted from MP5 donor")


def _assert_modeldoc_contract(payloads: Mapping[str, bytes]) -> None:
    body = payloads["aks74u_body_vmdl"].decode("utf-8-sig")
    magazine = payloads["aks74u_mag_vmdl"].decode("utf-8-sig")
    combined = payloads["aks74u_combined_vmdl"].decode("utf-8-sig")
    common_fragments = (
        f'filename = "{SOURCE_FBX_ASSET}"',
        "import_translation = [ 0.0, 0.0, 0.0 ]",
        "import_rotation = [ 0.0, 0.0, 0.0 ]",
        "import_scale = 0.3937008",
    )
    for label, text in (("body", body), ("magazine", magazine), ("combined", combined)):
        for fragment in common_fragments:
            if fragment not in text:
                raise ContractError(f"AKS-74U {label} ModelDoc lost {fragment!r}")
        if "autorig" in text.lower():
            raise ContractError(f"AKS-74U {label} ModelDoc reaches Auto Rigger")
    if '"SK_Rif_SLR_AK47.001"' not in body:
        raise ContractError("AKS-74U body ModelDoc lost its pinned mesh filter")
    if '"SM_Rif_SLR_AK47_mag_static.001"' not in magazine:
        raise ContractError("AKS-74U magazine ModelDoc lost its pinned mesh filter")
    if "exclude_by_default = false" not in combined:
        raise ContractError("AKS-74U combined ModelDoc no longer includes the full assembly")


def _assert_wepanim_is_empty(payloads: Mapping[str, bytes]) -> None:
    document = _read_json_bytes(payloads["aks74u_wepanim"], WEPANIM_REL).get("Document")
    if not isinstance(document, dict):
        raise ContractError("AKS-74U Weapon Animator file has no Document")
    source = document.get("Source")
    rig = document.get("Rig")
    calibration = document.get("Calibration")
    clips = document.get("Clips")
    if not isinstance(source, dict) or any(
        source.get(field) for field in ("SourcePath", "CompiledModelPath", "SourceHash")
    ):
        raise ContractError("AKS-74U Weapon Animator source is no longer empty")
    if not isinstance(rig, dict) or rig.get("Bones") != []:
        raise ContractError("AKS-74U Weapon Animator rig is no longer empty")
    if not isinstance(calibration, dict) or calibration.get("Confirmed") is not False:
        raise ContractError("AKS-74U Weapon Animator calibration state changed")
    if not isinstance(clips, list) or any(
        clip.get("Tracks") or clip.get("ImportedSequence") for clip in clips
    ):
        raise ContractError("AKS-74U Weapon Animator now contains authored clip data")


def _bounds_union(left: Bounds, right: Bounds) -> Bounds:
    return Bounds(
        tuple(min(a, b) for a, b in zip(left.mins, right.mins)),
        tuple(max(a, b) for a, b in zip(left.maxs, right.maxs)),
    )


def _component_types(index: PrefabIndex) -> set[str]:
    return {
        str(component.get("__type"))
        for _, component in index.components_by_guid.values()
    }


def audit_current_sources(
    payloads: Mapping[str, bytes] | None = None,
) -> dict[str, Any]:
    payloads = dict(payloads or validate_source_pins())
    donor_vm = _read_json_bytes(payloads["mp5_vm_prefab"], DONOR_VM_REL)
    target_vm = _read_json_bytes(payloads["aks74u_vm_prefab"], TARGET_VM_REL)
    donor_w = _read_json_bytes(payloads["mp5_w_prefab"], DONOR_W_REL)
    target_w = _read_json_bytes(payloads["aks74u_w_prefab"], TARGET_W_REL)
    donor_vm_index = index_prefab(donor_vm, "MP5 VM")
    target_vm_index = index_prefab(target_vm, "AKS-74U VM")
    donor_w_index = index_prefab(donor_w, "MP5 world")
    target_w_index = index_prefab(target_w, "AKS-74U world")

    _assert_donor_nodes_preserved(
        donor_vm_index,
        target_vm_index,
        {VM_BODY_PATH, VM_MAG_PATH},
        "AKS-74U VM",
    )
    _assert_donor_nodes_preserved(
        donor_w_index,
        target_w_index,
        {W_CUSTOM_PATH},
        "AKS-74U world",
    )
    _assert_renderer_driver_preserved(donor_vm_index, target_vm_index)
    _assert_viewmodel_contract(donor_vm_index, target_vm_index)
    _assert_modeldoc_contract(payloads)
    _assert_wepanim_is_empty(payloads)

    body_renderer = _renderer_by_model(target_vm_index, VM_BODY_PATH, BODY_MODEL)
    magazine_renderer = _renderer_by_model(target_vm_index, VM_MAG_PATH, MAG_MODEL)
    if body_renderer.get("__type") != "Sandbox.ModelRenderer":
        raise ContractError("AKS-74U VM body renderer type changed")
    if magazine_renderer.get("__type") != "Sandbox.ModelRenderer":
        raise ContractError("AKS-74U VM magazine renderer type changed")
    if target_vm_index.nodes[VM_MAG_PARENT_PATH].get("Flags") != 4:
        raise ContractError("AKS-74U magazine parent is not a generated MP5 bone object")

    world_donor_renderer = _renderer_by_model(target_w_index, W_MODEL_PATH, DONOR_W_MODEL)
    if world_donor_renderer.get("MaterialOverride") != INVISIBLE_VMAT:
        raise ContractError("AKS-74U world donor renderer is not hidden")
    world_custom_renderer = _renderer_by_model(
        target_w_index, W_CUSTOM_PATH, COMBINED_MODEL
    )
    equipment = _component_by_type(target_w_index, "", "Dxura.RP.Game.Equipment")
    equipment_owner, equipment_renderer_type = _resolve_component_ref(
        target_w_index, equipment.get("ModelRenderer"), "Equipment.ModelRenderer"
    )
    if equipment_owner != W_CUSTOM_PATH:
        raise ContractError("AKS-74U world visibility is not owned by the custom renderer")
    if world_custom_renderer.get("__type") != equipment_renderer_type:
        raise ContractError("AKS-74U world renderer reference type changed")

    if _bounds_union(AKS_BODY_BOUNDS, AKS_MAG_BOUNDS) != AKS_COMBINED_BOUNDS:
        raise ContractError("Pinned AKS-74U filtered bounds no longer reassemble")

    fp_fit = derive_centered_length_fit(AKS_COMBINED_BOUNDS, MP5_VM_BOUNDS)
    tp_fit = derive_centered_length_fit(AKS_COMBINED_BOUNDS, MP5_W_BOUNDS)
    current_fp_body = _transform_from_node(
        target_vm_index.nodes[VM_BODY_PATH], "current AKS-74U VM body"
    )
    current_fp_mag = _transform_from_node(
        target_vm_index.nodes[VM_MAG_PATH], "current AKS-74U VM magazine"
    )
    current_tp = _transform_from_node(
        target_w_index.nodes[W_CUSTOM_PATH], "current AKS-74U world model"
    )

    viewmodel = _component_by_type(target_vm_index, "", "Dxura.RP.Game.ViewModel")
    fp_anchor_owners = {
        "muzzle": _resolve_gameobject_ref(
            target_vm_index, viewmodel.get("Muzzle"), "ViewModel.Muzzle"
        ),
        "ejection": _resolve_gameobject_ref(
            target_vm_index, viewmodel.get("EjectionPort"), "ViewModel.EjectionPort"
        ),
    }
    world_anchor_owners = {
        "muzzle": _resolve_gameobject_ref(
            target_w_index, equipment.get("Muzzle"), "Equipment.Muzzle"
        ),
        "ejection": _resolve_gameobject_ref(
            target_w_index, equipment.get("EjectionPort"), "Equipment.EjectionPort"
        ),
    }
    left_grip_nodes = sorted(
        path for path in target_w_index.nodes if path.rsplit("/", 1)[-1].lower() == "leftgrip"
    )
    ik_components = sorted(
        component_type
        for component_type in _component_types(target_w_index)
        if "WorldWeaponLeftHandIk" in component_type
    )

    all_text = b"\n".join(payloads.values()).decode("utf-8", errors="ignore").lower()
    if "autorig" in all_text:
        raise ContractError("Pinned active AKS-74U/MP5 source closure reaches Auto Rigger")

    return {
        "donor_vm": donor_vm,
        "target_vm": target_vm,
        "donor_w": donor_w,
        "target_w": target_w,
        "donor_vm_index": donor_vm_index,
        "target_vm_index": target_vm_index,
        "donor_w_index": donor_w_index,
        "target_w_index": target_w_index,
        "fp_fit": fp_fit,
        "tp_fit": tp_fit,
        "current_fp_body": current_fp_body,
        "current_fp_mag": current_fp_mag,
        "current_tp": current_tp,
        "fp_anchor_owners": fp_anchor_owners,
        "world_anchor_owners": world_anchor_owners,
        "left_grip_nodes": left_grip_nodes,
        "ik_components": ik_components,
        "node_counts": {
            "donor_vm": len(donor_vm_index.nodes),
            "target_vm": len(target_vm_index.nodes),
            "donor_world": len(donor_w_index.nodes),
            "target_world": len(target_w_index.nodes),
        },
    }


def manifest_template() -> dict[str, Any]:
    return {
        "version": 1,
        "purpose": "dxrp_aks74u_mp5_idle_bind",
        "pins": {
            "donor_vm_prefab": {
                "bytes": SOURCE_PINS["mp5_vm_prefab"].bytes,
                "sha256": SOURCE_PINS["mp5_vm_prefab"].sha256,
            },
            "target_vm_prefab": {
                "bytes": SOURCE_PINS["aks74u_vm_prefab"].bytes,
                "sha256": SOURCE_PINS["aks74u_vm_prefab"].sha256,
            },
        },
        "sample": {
            "kind": "observed_idle_bind",
            "observed": False,
            "state": "IdlePose",
            "time_seconds": 0,
            "coordinate_space": "source_inches:+X_forward,+Y_left,+Z_up",
            "relative_to": VM_ASSEMBLY_PARENT_PATH,
            "node": VM_MAG_PARENT_PATH,
            "donor_model": DONOR_VM_MODEL,
            "sensor": "REPLACE WITH THE SENSOR THAT OBSERVED THE IDLE BIND",
            "transform": {
                "position": "REPLACE",
                "rotation": "REPLACE",
                "scale": "REPLACE",
            },
        },
    }


def compiled_bind_manifest_template() -> dict[str, Any]:
    payload = manifest_template()
    payload["sample"].update(
        {
            "kind": "compiled_model_bind",
            "observed": False,
            "state": "ModelSkeletonBind",
            "sensor": COMPILED_BIND_SENSOR,
            "transform": dict(COMPILED_BIND_TRANSFORM_PAYLOAD),
            "evidence": compiled_bind_evidence_template(),
        }
    )
    return payload


def load_idle_bind_manifest(path: Path) -> IdleBindManifest:
    if not path.is_file():
        raise ContractError(f"Idle-bind manifest is missing: {path}")
    raw = path.read_bytes()
    payload = _read_json_bytes(raw, str(path))
    if set(payload) != {"version", "purpose", "pins", "sample"}:
        raise ContractError(
            "Idle-bind manifest keys must be exactly version, purpose, pins, sample"
        )
    if payload.get("version") != 1:
        raise ContractError("Idle-bind manifest version must be 1")
    if payload.get("purpose") != "dxrp_aks74u_mp5_idle_bind":
        raise ContractError("Idle-bind manifest purpose is wrong")
    pins = payload.get("pins")
    expected_pins = manifest_template()["pins"]
    if pins != expected_pins:
        raise ContractError("Idle-bind manifest donor/target source pins do not match")
    sample = payload.get("sample")
    if not isinstance(sample, dict):
        raise ContractError("Idle-bind manifest sample must be an object")
    required = {
        "coordinate_space": "source_inches:+X_forward,+Y_left,+Z_up",
        "relative_to": VM_ASSEMBLY_PARENT_PATH,
        "node": VM_MAG_PARENT_PATH,
        "donor_model": DONOR_VM_MODEL,
    }
    for field, expected in required.items():
        if sample.get(field) != expected:
            raise ContractError(
                f"Idle-bind manifest sample.{field} must be {expected!r}"
            )
    sample_kind = sample.get("kind")
    common_keys = {
        "kind",
        "observed",
        "state",
        "time_seconds",
        "coordinate_space",
        "relative_to",
        "node",
        "donor_model",
        "sensor",
        "transform",
    }
    evidence: Mapping[str, Any] | None = None
    if sample_kind == "observed_idle_bind":
        if set(sample) != common_keys:
            raise ContractError(
                "Observed idle-bind sample contains missing or extra fields"
            )
        kind_required = {
            "observed": True,
            "state": "IdlePose",
            "time_seconds": 0,
        }
    elif sample_kind == "compiled_model_bind":
        if set(sample) != common_keys | {"evidence"}:
            raise ContractError(
                "Compiled-model bind sample must contain exactly the bind fields "
                "plus evidence"
            )
        kind_required = {
            "observed": False,
            "state": "ModelSkeletonBind",
            "time_seconds": 0,
        }
        expected_evidence = compiled_bind_evidence_template()
        if sample.get("evidence") != expected_evidence:
            raise ContractError(
                "Compiled-model bind evidence does not match the pinned parser/model record"
            )
        if sample.get("sensor") != COMPILED_BIND_SENSOR:
            raise ContractError("Compiled-model bind sensor does not match the pinned sensor")
        if sample.get("transform") != COMPILED_BIND_TRANSFORM_PAYLOAD:
            raise ContractError(
                "Compiled-model bind transform does not match the pinned DATA record"
            )
        _verify_external_pin(
            COMPILED_BIND_PARSER_PATH,
            COMPILED_BIND_PARSER_BYTES,
            COMPILED_BIND_PARSER_SHA256,
            "parser",
        )
        _verify_external_pin(
            COMPILED_BIND_MODEL_PATH,
            COMPILED_BIND_MODEL_BYTES,
            COMPILED_BIND_MODEL_SHA256,
            "model",
        )
        evidence = copy.deepcopy(expected_evidence)
    else:
        raise ContractError(
            "Idle-bind manifest sample.kind must be observed_idle_bind or "
            "compiled_model_bind"
        )
    for field, expected in kind_required.items():
        if sample.get(field) != expected:
            raise ContractError(
                f"Idle-bind manifest sample.{field} must be {expected!r}"
            )
    sensor = sample.get("sensor")
    if (
        not isinstance(sensor, str)
        or not sensor.strip()
        or "replace" in sensor.lower()
        or "placeholder" in sensor.lower()
    ):
        raise ContractError("Idle-bind manifest requires a non-placeholder sensor")
    transform = parse_transform(sample.get("transform"), "sample.transform")
    return IdleBindManifest(
        transform, sensor.strip(), _sha256(raw), sample_kind, evidence
    )


def _is_reparse(path: Path, result: os.stat_result) -> bool:
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(getattr(result, "st_file_attributes", 0) & flag)


def _assert_no_reparse_components(path: Path) -> None:
    current = Path(os.path.abspath(os.fspath(path)))
    while True:
        try:
            result = current.lstat()
        except FileNotFoundError:
            pass
        else:
            if _is_reparse(current, result):
                raise ContractError(f"Reparse-point output path is not allowed: {current}")
        parent = current.parent
        if parent == current:
            return
        current = parent


def _is_within(path: Path, directory: Path) -> bool:
    try:
        path.relative_to(directory)
    except ValueError:
        return False
    return True


def assert_temp_output_directory(path: Path) -> Path:
    temp_root = Path(tempfile.gettempdir()).resolve(strict=True)
    lexical = Path(os.path.abspath(os.fspath(path)))
    _assert_no_reparse_components(lexical)
    resolved_parent = lexical.parent.resolve(strict=True)
    candidate = resolved_parent / lexical.name
    if candidate == temp_root or not _is_within(candidate, temp_root):
        raise ContractError(
            f"TEMP-ONLY output must be a child of {temp_root}; got {candidate}"
        )
    repo = REPO_ROOT.resolve(strict=True)
    if _is_within(candidate, repo):
        raise ContractError(f"Repository output is forbidden: {candidate}")
    if candidate.exists():
        if not candidate.is_dir():
            raise ContractError(f"Output path is not a directory: {candidate}")
        if any(candidate.iterdir()):
            raise ContractError(f"Output directory must be empty: {candidate}")
    return candidate


def _serialize_json(value: Any) -> bytes:
    return (json.dumps(value, indent=2, ensure_ascii=True) + "\n").encode("utf-8")


def _report_bounds(bounds: Bounds) -> dict[str, str]:
    return {
        "mins": format_vector(bounds.mins),
        "maxs": format_vector(bounds.maxs),
        "center": format_vector(bounds.center),
        "size": format_vector(bounds.size),
    }


def _anchor_report(fit: Transform) -> dict[str, dict[str, Any]]:
    return {
        name: {
            "source_model_position": format_vector(position),
            "candidate_parent_local_position": format_vector(
                transform_point(fit, position)
            ),
            "rotation": "UNVERIFIED_NOT_EMITTED",
            "wiring": "UNVERIFIED_NOT_MODIFIED",
        }
        for name, position in SOURCE_ANCHORS.items()
    }


def _source_pin_report() -> dict[str, dict[str, Any]]:
    return {
        name: {
            "path": pin.relative_path,
            "bytes": pin.bytes,
            "sha256": pin.sha256,
        }
        for name, pin in sorted(SOURCE_PINS.items())
    }


def build_temp_candidates(
    manifest_path: Path,
    output_directory: Path | None = None,
) -> dict[str, Any]:
    payloads = validate_source_pins()
    audit = audit_current_sources(payloads)
    manifest = load_idle_bind_manifest(manifest_path)
    fp_fit: Transform = audit["fp_fit"]
    tp_fit: Transform = audit["tp_fit"]
    magazine_local = inverse_parent_compose(manifest.transform, fp_fit)
    reassembled = compose(manifest.transform, magazine_local)
    current_magazine_bind_world = compose(
        manifest.transform, audit["current_fp_mag"]
    )
    if not _almost_equal_transform(reassembled, fp_fit, tolerance=1e-7):
        raise ContractError("inverse-bind round trip did not reproduce the body assembly")

    vm_candidate = copy.deepcopy(audit["target_vm"])
    w_candidate = copy.deepcopy(audit["target_w"])
    vm_index = index_prefab(vm_candidate, "AKS-74U VM candidate")
    w_index = index_prefab(w_candidate, "AKS-74U world candidate")
    _set_node_transform(vm_index.nodes[VM_BODY_PATH], fp_fit)
    _set_node_transform(vm_index.nodes[VM_MAG_PATH], magazine_local)
    _set_node_transform(w_index.nodes[W_CUSTOM_PATH], tp_fit)

    # Re-index after mutation and re-prove the two custom model mappings.
    vm_index = index_prefab(vm_candidate, "AKS-74U VM candidate")
    w_index = index_prefab(w_candidate, "AKS-74U world candidate")
    _renderer_by_model(vm_index, VM_BODY_PATH, BODY_MODEL)
    _renderer_by_model(vm_index, VM_MAG_PATH, MAG_MODEL)
    _renderer_by_model(w_index, W_CUSTOM_PATH, COMBINED_MODEL)
    if _component_types(w_index) & {"Dxura.RP.Game.WorldWeaponLeftHandIk"}:
        raise ContractError("TEMP candidate unexpectedly wired third-person IK")

    vm_bytes = _serialize_json(vm_candidate)
    w_bytes = _serialize_json(w_candidate)
    output_names = {
        "viewmodel": "vm_aks74u.fit-candidate.prefab",
        "world": "w_aks74u.fit-candidate.prefab",
        "report": "aks74u.fit-report.json",
    }
    report = {
        "version": 1,
        "mode": "TEMP_ONLY_NO_PRODUCT_WRITE_MODE",
        "source_pins": _source_pin_report(),
        "idle_bind_manifest": {
            "sha256": manifest.sha256,
            "sensor": manifest.sensor,
            "sample_kind": manifest.sample_kind,
            "compiled_evidence": manifest.evidence,
            "transform_P": transform_payload(manifest.transform),
        },
        "bounds_evidence": {
            "sensor": "sbox-native inspect_model_geometry; engine 26.08.19; model-local default pose",
            "replacement_body": _report_bounds(AKS_BODY_BOUNDS),
            "replacement_magazine": _report_bounds(AKS_MAG_BOUNDS),
            "replacement_combined": _report_bounds(AKS_COMBINED_BOUNDS),
            "mp5_first_person": _report_bounds(MP5_VM_BOUNDS),
            "mp5_third_person": _report_bounds(MP5_W_BOUNDS),
            "derivation": "uniform scale matches +X length; translation aligns bounds centers",
        },
        "first_person": {
            "ownership": {
                "animation_and_arms": DONOR_VM_MODEL,
                "visible_body": BODY_MODEL,
                "visible_magazine": MAG_MODEL,
                "magazine_parent": VM_MAG_PARENT_PATH,
            },
            "body_assembly_B": transform_payload(fp_fit),
            "magazine_parent_bind_P": transform_payload(manifest.transform),
            "parent_bind_source": manifest.sample_kind,
            "magazine_local_W": transform_payload(magazine_local),
            "transform_law": "W = inverse(P) * B; P * W = B",
            "round_trip": transform_payload(reassembled),
            "current_body_matches_bounds_seed": _almost_equal_transform(
                audit["current_fp_body"], fp_fit
            ),
            "current_magazine_local": transform_payload(audit["current_fp_mag"]),
            "current_magazine_bind_world": transform_payload(
                current_magazine_bind_world
            ),
            "current_magazine_aligns_with_current_body_at_bind": _almost_equal_transform(
                current_magazine_bind_world, audit["current_fp_body"]
            ),
            "current_magazine_aligns_with_bounds_seed_at_bind": _almost_equal_transform(
                current_magazine_bind_world, fp_fit
            ),
            "static_magazine_motion_compatibility": {
                "compatible": True,
                "basis": (
                    "target retains the byte-pinned MP5 renderer with CreateBoneObjects=true "
                    "and UseAnimGraph=true; the custom magazine is a child of the generated "
                    "MP5 magazine node"
                ),
                "rendered_motion_verified": False,
            },
            "existing_anchor_owners": audit["fp_anchor_owners"],
            "candidate_anchor_positions": _anchor_report(fp_fit),
            "anchors_rewired": False,
        },
        "third_person": {
            "ownership": {
                "equipment_visible_renderer": COMBINED_MODEL,
                "combined_static_geometry": True,
                "separate_magazine_animation": False,
            },
            "combined_assembly_B": transform_payload(tp_fit),
            "current_combined_matches_bounds_seed": _almost_equal_transform(
                audit["current_tp"], tp_fit
            ),
            "existing_anchor_owners": audit["world_anchor_owners"],
            "candidate_anchor_positions": _anchor_report(tp_fit),
            "left_grip_nodes": audit["left_grip_nodes"],
            "world_left_hand_ik_components": audit["ik_components"],
            "anchors_rewired": False,
            "left_hand_ik_wired": False,
        },
        "structural_audit": {
            "donor_nodes_preserved": True,
            "node_counts": audit["node_counts"],
            "filtered_parts_reassemble_to_combined_bounds": True,
            "weapon_animator_source_rig_tracks_empty": True,
            "auto_rigger_references": 0,
        },
        "outputs": {
            "viewmodel": {
                "file": output_names["viewmodel"],
                "bytes": len(vm_bytes),
                "sha256": _sha256(vm_bytes),
            },
            "world": {
                "file": output_names["world"],
                "bytes": len(w_bytes),
                "sha256": _sha256(w_bytes),
            },
        },
        "proof_ceiling": [
            "source-byte custody",
            "static hierarchy and transform composition",
            "TEMP candidate serialization",
            "UNVERIFIED asset compile",
            "UNVERIFIED code compile",
            "UNVERIFIED runtime",
            "UNVERIFIED rendered magazine motion",
            "compiled skeleton bind is not observed IdlePose when sample_kind is compiled_model_bind",
            "UNVERIFIED first-person hand fit and ADS",
            "UNVERIFIED third-person hold and left-hand IK",
            "UNVERIFIED muzzle/ejection orientation and effect placement",
            "UNVERIFIED Portal and multiplayer",
        ],
    }
    report_bytes = _serialize_json(report)

    if output_directory is None:
        output_directory = Path(tempfile.mkdtemp(prefix="dxrp_aks74u_fit_"))
        output_directory = assert_temp_output_directory(output_directory)
    else:
        output_directory = assert_temp_output_directory(output_directory)
        output_directory.mkdir(parents=False, exist_ok=True)

    for name, data in (
        (output_names["viewmodel"], vm_bytes),
        (output_names["world"], w_bytes),
        (output_names["report"], report_bytes),
    ):
        destination = output_directory / name
        with destination.open("xb") as stream:
            stream.write(data)

    return {
        "output_directory": str(output_directory),
        "files": output_names,
        "report": report,
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build byte-pinned AKS-74U/MP5 fit candidates below the OS temp "
            "directory only. There is intentionally no product-write mode."
        )
    )
    parser.add_argument(
        "--idle-bind-manifest",
        type=Path,
        help=(
            "Explicit version-1 magazine-parent bind manifest: observed IdlePose "
            "or byte-pinned compiled-model bind."
        ),
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Empty output directory below the OS temp directory; default is a new temp dir.",
    )
    parser.add_argument(
        "--print-manifest-template",
        action="store_true",
        help="Print an intentionally incomplete manifest template and exit without writing.",
    )
    parser.add_argument(
        "--print-compiled-bind-manifest",
        action="store_true",
        help=(
            "Print the exact byte-pinned compiled-model bind manifest and exit "
            "without writing."
        ),
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    if args.print_manifest_template or args.print_compiled_bind_manifest:
        if args.idle_bind_manifest is not None or args.output_dir is not None:
            parser.error("manifest print modes cannot be combined with build options")
        if args.print_manifest_template and args.print_compiled_bind_manifest:
            parser.error("choose exactly one manifest print mode")
        payload = (
            manifest_template()
            if args.print_manifest_template
            else compiled_bind_manifest_template()
        )
        print(json.dumps(payload, indent=2, ensure_ascii=True))
        return 0
    if args.idle_bind_manifest is None:
        parser.error("--idle-bind-manifest is required to build a candidate")
    try:
        result = build_temp_candidates(args.idle_bind_manifest, args.output_dir)
    except (ContractError, OSError) as exc:
        print(f"DXRP_AKS74U_TEMP_CANDIDATE_ERROR: {exc}", file=sys.stderr)
        return 2
    print(
        "DXRP_AKS74U_TEMP_CANDIDATE="
        + json.dumps(
            {
                "output_directory": result["output_directory"],
                "files": result["files"],
                "mode": result["report"]["mode"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
