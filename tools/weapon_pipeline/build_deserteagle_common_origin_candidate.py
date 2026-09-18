#!/usr/bin/env python3
"""Build a deterministic, temporary Desert Eagle common-origin candidate.

The pinned USP prefab is the animation donor.  The pinned current Desert Eagle
prefab supplies only the invisible donor material and the three renderer
templates.  It is not used as transform truth.

Every donor object/component GUID is deterministically remapped for the
candidate and every matching internal reference follows that remap.  The six
custom definitions use the same collision-checked UUIDv5 namespace.

An external, SHA-256-pinned idle-bind manifest must provide ``B_root``,
``B_slide``, and ``B_mag`` in donor-view coordinates.  One geometry-envelope
assembly candidate ``A`` is derived from the audited model bounds.  Each part's
local transform is then computed as ``L = inverse(B) * A`` and rechecked after
serialization by asserting ``B * L == A``.

There is deliberately no product-write or acceptance mode.  Output is allowed
only beneath the operating system's temporary directory and never beneath
``game/Assets``.  The result is structural evidence, not runtime or visual
acceptance.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import re
import tempfile
import uuid
from dataclasses import dataclass
from itertools import combinations
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

import pipeline_io


WORKBENCH_ROOT = Path(__file__).resolve().parents[2]
GAME_ASSETS_ROOT = (WORKBENCH_ROOT / "game/Assets").resolve()
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
DEFAULT_DONOR = (
    WORKBENCH_ROOT / "game/Assets/gameplay/equipment/weapons/usp/vm_usp.prefab"
)
DEFAULT_SOURCE = (
    WORKBENCH_ROOT
    / "game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/"
    "vm_desert_eagle/vm_desert_eagle.prefab"
)
OUTPUT_NAME = "vm_desert_eagle_common_origin_candidate.prefab"

EXPECTED_DONOR_BYTES = 68_573
EXPECTED_DONOR_SHA256 = (
    "C5621B02C9CE1D15104718DF8DA954696921886DFB16ABEFAD9D46AC3AE83C3C"
)
EXPECTED_SOURCE_BYTES = 73_212
EXPECTED_SOURCE_SHA256 = (
    "77A891CD69990A868726B821DC7F52FD086F3594B48752E497809559765E4227"
)

COMPILED_BIND_PARSER_PATH = Path(
    r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe"
)
COMPILED_BIND_PARSER_BYTES = 108_603_232
COMPILED_BIND_PARSER_SHA256 = (
    "36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0"
)
COMPILED_BIND_PARSER_VERSION = (
    "19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa"
)
COMPILED_BIND_MODEL_PATH = Path(
    r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons\sbox_pistol_usp\v_usp.c9a4333387e69880.vmdl_c"
)
COMPILED_BIND_MODEL_BYTES = 1_394_438
COMPILED_BIND_MODEL_SHA256 = (
    "439F8B2B676F310318EC4080D577DC16EA3549249564BADDDCD0B0CDE4A520F5"
)
COMPILED_BIND_TRANSFORMS: Mapping[str, object] = {
    "B_root": {
        "matrix": [
            [1, 0, 0, -2.16535],
            [0, 1, 0, 0],
            [0, 0, 1, 3.1496],
            [0, 0, 0, 1],
        ]
    },
    "B_slide": {
        "matrix": [
            [1, 0, 0, -0.84248],
            [0, 1, 0, 0],
            [0, 0, 1, 4.780133],
            [0, 0, 0, 1],
        ]
    },
    "B_mag": {
        "matrix": [
            [-0.275636801238, 0, 0.961261854961, -2.236997],
            [0, 1, 0, 0],
            [-0.961261854961, 0, -0.275636801238, 2.324961],
            [0, 0, 0, 1],
        ]
    },
}


def compiled_bind_evidence_template() -> dict[str, object]:
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
        "records": {
            "root": {
                "bone": "weapon_root",
                "parent": None,
                "position": [-2.16535, 0, 3.1496],
                "quaternion": [0, 0, 0, 1],
                "scale": [1, 1, 1],
            },
            "root_children": {
                "bone": "weapon_root_children",
                "parent": "weapon_root",
                "position": [0, 0, 0],
                "quaternion": [0, 0, 0, 1],
                "scale": [1, 1, 1],
            },
            "slide": {
                "bone": "slide",
                "parent": "weapon_root_children",
                "position": [1.32287, 0, 1.630533],
                "quaternion": [0, 0, 0, 1],
                "scale": [1, 1, 1],
            },
            "magazine": {
                "bone": "magazine",
                "parent": "weapon_root_children",
                "position": [-0.071647, 0, -0.824639],
                "quaternion": [0, 0.798635, 0, 0.601815],
                "scale": [1, 1, 1],
            },
        },
    }


def compiled_bind_manifest_template() -> dict[str, object]:
    return {
        "version": 1,
        "pose": "compiled-bind",
        "coordinate_space": "donor-view",
        "pins": {
            "donor": {
                "bytes": EXPECTED_DONOR_BYTES,
                "sha256": EXPECTED_DONOR_SHA256,
            },
            "source": {
                "bytes": EXPECTED_SOURCE_BYTES,
                "sha256": EXPECTED_SOURCE_SHA256,
            },
        },
        "evidence": compiled_bind_evidence_template(),
        "transforms": copy.deepcopy(COMPILED_BIND_TRANSFORMS),
    }

# The exact bytes below pin the numeric geometry record used to derive A.  The
# record is a candidate from model bounds, not a rendered-fit acceptance.
GEOMETRY_BOUNDS_RECORD = (
    b'{"custom":{"center":[-5.588684,1.1444092E-05,76.449905],'
    b'"x_length":261.08582},"donor":{"center":'
    b'[0.6741452,0.09895921,3.1780028],"x_length":9.068356}}'
)
EXPECTED_GEOMETRY_BOUNDS_SHA256 = (
    "00B21438680AE7AF9501C45E9F34126B433B6E92D6B712EF03EFB9943CDECB2F"
)
CUSTOM_FULL_CENTER = (-5.588684, 0.000011444092, 76.449905)
CUSTOM_FULL_X_LENGTH = 261.08582
DONOR_VIEW_CENTER = (0.6741452, 0.09895921, 3.1780028)
DONOR_VIEW_X_LENGTH = 9.068356
EXPECTED_ASSEMBLY_POSITION = (
    0.868258293095228,
    0.098958812509626,
    0.522650032445715,
)
EXPECTED_ASSEMBLY_SCALE = 0.0347332382892338

PIN_RE = re.compile(r"^[0-9a-f]{64}$", re.IGNORECASE)
GUID_RE = re.compile(
    r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
    re.IGNORECASE,
)
MATRIX_TOLERANCE = 1e-9
TRS_TOLERANCE = 1e-8
GUID_NAMESPACE = uuid.UUID("ebd7c801-533a-54ef-b960-3b0cb66d9150")


class ContractError(RuntimeError):
    """Raised when candidate custody or transform contracts are incomplete."""


Vector3 = tuple[float, float, float]
Quaternion = tuple[float, float, float, float]
Matrix4 = tuple[
    tuple[float, float, float, float],
    tuple[float, float, float, float],
    tuple[float, float, float, float],
    tuple[float, float, float, float],
]


@dataclass(frozen=True)
class Transform:
    position: Vector3
    quaternion: Quaternion
    scale: Vector3


@dataclass(frozen=True)
class PartSpec:
    role: str
    bind_key: str
    node_name: str
    model: str
    donor_parent_path: str


PART_SPECS = (
    PartSpec(
        role="root",
        bind_key="B_root",
        node_name="desert_eagle_body",
        model="addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl",
        donor_parent_path="weapon_root/weapon_root_children",
    ),
    PartSpec(
        role="slide",
        bind_key="B_slide",
        node_name="desert_eagle_slide",
        model="addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl",
        donor_parent_path="weapon_root/weapon_root_children/slide",
    ),
    PartSpec(
        role="magazine",
        bind_key="B_mag",
        node_name="desert_eagle_magazine",
        model="addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl",
        donor_parent_path="weapon_root/weapon_root_children/magazine",
    ),
)


@dataclass
class PrefabIndex:
    nodes_by_guid: dict[str, dict[str, Any]]
    nodes_by_path: dict[str, dict[str, Any]]
    components_by_guid: dict[str, dict[str, Any]]
    component_owners: dict[str, str]
    parent_by_guid: dict[str, str | None]

    @property
    def all_guids(self) -> set[str]:
        return set(self.nodes_by_guid) | set(self.components_by_guid)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


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


def _normalized_pin(value: object, label: str) -> str:
    if not isinstance(value, str) or not PIN_RE.fullmatch(value):
        raise ContractError(f"{label} must be one full SHA-256 value")
    return value.upper()


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.resolve(strict=False).relative_to(parent.resolve(strict=False))
    except ValueError:
        return False
    return True


def assert_temp_output_directory(output_directory: Path) -> Path:
    """Return a resolved temp output directory or fail closed."""

    resolved = output_directory.resolve(strict=False)
    if _is_within(resolved, GAME_ASSETS_ROOT):
        raise ContractError("game/Assets output is impossible for this TEMP-ONLY tool")
    if not _is_within(resolved, SYSTEM_TEMP_ROOT):
        raise ContractError(
            f"TEMP-ONLY output must remain beneath {SYSTEM_TEMP_ROOT}, got {resolved}"
        )
    return resolved


def _finite_float(value: object, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ContractError(f"{label} must contain JSON numbers")
    result = float(value)
    if not math.isfinite(result):
        raise ContractError(f"{label} must contain finite values")
    return result


def _numeric_vector(
    value: object, length: int, label: str
) -> tuple[float, ...]:
    if not isinstance(value, list) or len(value) != length:
        raise ContractError(f"{label} must be an array of {length} numbers")
    return tuple(_finite_float(item, label) for item in value)


def _identity_matrix() -> Matrix4:
    return (
        (1.0, 0.0, 0.0, 0.0),
        (0.0, 1.0, 0.0, 0.0),
        (0.0, 0.0, 1.0, 0.0),
        (0.0, 0.0, 0.0, 1.0),
    )


def matrix_multiply(left: Matrix4, right: Matrix4) -> Matrix4:
    return tuple(
        tuple(
            sum(left[row][axis] * right[axis][column] for axis in range(4))
            for column in range(4)
        )
        for row in range(4)
    )  # type: ignore[return-value]


def matrix_inverse(matrix: Matrix4) -> Matrix4:
    augmented = [
        [*matrix[row], *(_identity_matrix()[row])]
        for row in range(4)
    ]
    for column in range(4):
        pivot = max(range(column, 4), key=lambda row: abs(augmented[row][column]))
        if abs(augmented[pivot][column]) <= MATRIX_TOLERANCE:
            raise ContractError("idle-bind transform is singular")
        augmented[column], augmented[pivot] = augmented[pivot], augmented[column]
        divisor = augmented[column][column]
        augmented[column] = [value / divisor for value in augmented[column]]
        for row in range(4):
            if row == column:
                continue
            factor = augmented[row][column]
            augmented[row] = [
                augmented[row][index] - factor * augmented[column][index]
                for index in range(8)
            ]
    return tuple(
        tuple(augmented[row][column] for column in range(4, 8))
        for row in range(4)
    )  # type: ignore[return-value]


def matrix_max_delta(left: Matrix4, right: Matrix4) -> float:
    return max(
        abs(left[row][column] - right[row][column])
        for row in range(4)
        for column in range(4)
    )


def _normalize_quaternion(quaternion: Quaternion, label: str) -> Quaternion:
    length = math.sqrt(sum(value * value for value in quaternion))
    if length <= MATRIX_TOLERANCE:
        raise ContractError(f"{label} quaternion is zero")
    if abs(length - 1.0) > 1e-5:
        raise ContractError(f"{label} quaternion must be normalized")
    normalized = tuple(value / length for value in quaternion)
    if normalized[3] < 0.0:
        normalized = tuple(-value for value in normalized)
    return normalized  # type: ignore[return-value]


def transform_matrix(transform: Transform) -> Matrix4:
    x, y, z, w = _normalize_quaternion(transform.quaternion, "transform")
    sx, sy, sz = transform.scale
    if min(sx, sy, sz) <= MATRIX_TOLERANCE:
        raise ContractError("transform scale must be positive and non-zero")

    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    rotation = (
        (1.0 - 2.0 * (yy + zz), 2.0 * (xy - wz), 2.0 * (xz + wy)),
        (2.0 * (xy + wz), 1.0 - 2.0 * (xx + zz), 2.0 * (yz - wx)),
        (2.0 * (xz - wy), 2.0 * (yz + wx), 1.0 - 2.0 * (xx + yy)),
    )
    px, py, pz = transform.position
    return (
        (rotation[0][0] * sx, rotation[0][1] * sy, rotation[0][2] * sz, px),
        (rotation[1][0] * sx, rotation[1][1] * sy, rotation[1][2] * sz, py),
        (rotation[2][0] * sx, rotation[2][1] * sy, rotation[2][2] * sz, pz),
        (0.0, 0.0, 0.0, 1.0),
    )


def _determinant3(matrix: tuple[tuple[float, float, float], ...]) -> float:
    return (
        matrix[0][0]
        * (matrix[1][1] * matrix[2][2] - matrix[1][2] * matrix[2][1])
        - matrix[0][1]
        * (matrix[1][0] * matrix[2][2] - matrix[1][2] * matrix[2][0])
        + matrix[0][2]
        * (matrix[1][0] * matrix[2][1] - matrix[1][1] * matrix[2][0])
    )


def _quaternion_from_rotation(
    rotation: tuple[tuple[float, float, float], ...]
) -> Quaternion:
    trace = rotation[0][0] + rotation[1][1] + rotation[2][2]
    if trace > 0.0:
        root = math.sqrt(trace + 1.0) * 2.0
        quaternion = (
            (rotation[2][1] - rotation[1][2]) / root,
            (rotation[0][2] - rotation[2][0]) / root,
            (rotation[1][0] - rotation[0][1]) / root,
            0.25 * root,
        )
    elif rotation[0][0] > rotation[1][1] and rotation[0][0] > rotation[2][2]:
        root = math.sqrt(1.0 + rotation[0][0] - rotation[1][1] - rotation[2][2]) * 2.0
        quaternion = (
            0.25 * root,
            (rotation[0][1] + rotation[1][0]) / root,
            (rotation[0][2] + rotation[2][0]) / root,
            (rotation[2][1] - rotation[1][2]) / root,
        )
    elif rotation[1][1] > rotation[2][2]:
        root = math.sqrt(1.0 + rotation[1][1] - rotation[0][0] - rotation[2][2]) * 2.0
        quaternion = (
            (rotation[0][1] + rotation[1][0]) / root,
            0.25 * root,
            (rotation[1][2] + rotation[2][1]) / root,
            (rotation[0][2] - rotation[2][0]) / root,
        )
    else:
        root = math.sqrt(1.0 + rotation[2][2] - rotation[0][0] - rotation[1][1]) * 2.0
        quaternion = (
            (rotation[0][2] + rotation[2][0]) / root,
            (rotation[1][2] + rotation[2][1]) / root,
            0.25 * root,
            (rotation[1][0] - rotation[0][1]) / root,
        )
    return _normalize_quaternion(quaternion, "decomposed transform")


def decompose_trs(matrix: Matrix4, label: str) -> Transform:
    if max(abs(matrix[3][axis]) for axis in range(3)) > TRS_TOLERANCE or abs(
        matrix[3][3] - 1.0
    ) > TRS_TOLERANCE:
        raise ContractError(f"{label} is not an affine transform")

    columns = [tuple(matrix[row][column] for row in range(3)) for column in range(3)]
    scales = tuple(math.sqrt(sum(value * value for value in column)) for column in columns)
    if min(scales) <= MATRIX_TOLERANCE:
        raise ContractError(f"{label} has zero scale")
    normalized_columns = [
        tuple(value / scales[index] for value in column)
        for index, column in enumerate(columns)
    ]
    for first, second in combinations(range(3), 2):
        dot = sum(
            normalized_columns[first][axis] * normalized_columns[second][axis]
            for axis in range(3)
        )
        if abs(dot) > TRS_TOLERANCE:
            raise ContractError(f"{label} contains shear and cannot serialize as TRS")
    rotation = tuple(
        tuple(normalized_columns[column][row] for column in range(3))
        for row in range(3)
    )
    if _determinant3(rotation) <= 0.0:
        raise ContractError(f"{label} contains reflection or negative scale")
    transform = Transform(
        position=(matrix[0][3], matrix[1][3], matrix[2][3]),
        quaternion=_quaternion_from_rotation(rotation),
        scale=scales,  # type: ignore[arg-type]
    )
    if matrix_max_delta(transform_matrix(transform), matrix) > TRS_TOLERANCE:
        raise ContractError(f"{label} TRS decomposition did not reconstruct")
    return transform


def _parse_manifest_transform(value: object, label: str) -> Matrix4:
    if not isinstance(value, dict):
        raise ContractError(f"{label} must be a transform object")
    keys = set(value)
    if keys == {"matrix"}:
        rows = value["matrix"]
        if not isinstance(rows, list) or len(rows) != 4:
            raise ContractError(f"{label}.matrix must be a 4x4 array")
        matrix = tuple(
            _numeric_vector(row, 4, f"{label}.matrix") for row in rows
        )
        decompose_trs(matrix, label)  # validates affine, non-reflective TRS input
        return matrix  # type: ignore[return-value]
    if keys != {"position", "quaternion", "scale"}:
        raise ContractError(
            f"{label} must contain exactly matrix or position/quaternion/scale"
        )
    position = _numeric_vector(value["position"], 3, f"{label}.position")
    quaternion = _numeric_vector(value["quaternion"], 4, f"{label}.quaternion")
    scale = _numeric_vector(value["scale"], 3, f"{label}.scale")
    return transform_matrix(
        Transform(
            position=position,  # type: ignore[arg-type]
            quaternion=quaternion,  # type: ignore[arg-type]
            scale=scale,  # type: ignore[arg-type]
        )
    )


def derive_geometry_assembly() -> Transform:
    if _sha256(GEOMETRY_BOUNDS_RECORD) != EXPECTED_GEOMETRY_BOUNDS_SHA256:
        raise ContractError("geometry bounds record pin mismatch")
    scale = DONOR_VIEW_X_LENGTH / CUSTOM_FULL_X_LENGTH
    position = tuple(
        DONOR_VIEW_CENTER[axis] - CUSTOM_FULL_CENTER[axis] * scale
        for axis in range(3)
    )
    transform = Transform(
        position=position,  # type: ignore[arg-type]
        quaternion=(0.0, 0.0, 0.0, 1.0),
        scale=(scale, scale, scale),
    )
    if max(
        abs(position[axis] - EXPECTED_ASSEMBLY_POSITION[axis]) for axis in range(3)
    ) > 1e-14 or abs(scale - EXPECTED_ASSEMBLY_SCALE) > 1e-15:
        raise ContractError("geometry-envelope A no longer matches its audited values")
    return transform


def _pin_payload(path: Path, data: bytes) -> dict[str, object]:
    return {"path": str(path.resolve()), "bytes": len(data), "sha256": _sha256(data)}


def _verify_prefab_pin(
    path: Path, expected_bytes: int, expected_sha256: str, label: str
) -> bytes:
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"cannot read pinned {label} prefab {path}: {exc}") from exc
    if len(data) != expected_bytes:
        raise ContractError(
            f"{label} byte pin mismatch: expected {expected_bytes}, got {len(data)}"
        )
    actual = _sha256(data)
    if actual != expected_sha256:
        raise ContractError(
            f"{label} SHA-256 pin mismatch: expected {expected_sha256}, got {actual}"
        )
    return data


def _validate_manifest_pin(
    value: object,
    *,
    label: str,
    actual_bytes: int,
    actual_sha256: str,
) -> None:
    if not isinstance(value, dict) or set(value) != {"bytes", "sha256"}:
        raise ContractError(f"idle-bind pins.{label} must contain bytes and sha256")
    if value["bytes"] != actual_bytes:
        raise ContractError(f"idle-bind pins.{label} byte pin mismatch")
    if _normalized_pin(value["sha256"], f"pins.{label}.sha256") != actual_sha256:
        raise ContractError(f"idle-bind pins.{label} SHA-256 pin mismatch")


def load_idle_bind_manifest(
    path: Path,
    expected_sha256: str,
    *,
    donor_bytes: bytes,
    source_bytes: bytes,
) -> tuple[dict[str, Matrix4], dict[str, object]]:
    expected = _normalized_pin(expected_sha256, "idle-bind JSON SHA-256")
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"cannot read idle-bind JSON {path}: {exc}") from exc
    actual = _sha256(raw)
    if actual != expected:
        raise ContractError(
            f"idle-bind JSON SHA-256 mismatch: expected {expected}, got {actual}"
        )
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"idle-bind JSON is invalid: {exc}") from exc
    required_top = {"version", "pose", "coordinate_space", "pins", "transforms"}
    if not isinstance(payload, dict):
        raise ContractError("idle-bind JSON root must be an object")
    pose = payload.get("pose")
    if pose not in {"idle-bind", "compiled-bind"}:
        raise ContractError("idle-bind JSON pose must be idle-bind or compiled-bind")
    if pose == "compiled-bind":
        required_top = required_top | {"evidence"}
    if set(payload) != required_top:
        raise ContractError(
            "bind JSON contains missing or extra top-level fields for its pose"
        )
    if payload["version"] != 1:
        raise ContractError("idle-bind JSON version must be 1")
    if payload["coordinate_space"] != "donor-view":
        raise ContractError("idle-bind JSON coordinate_space must be donor-view")
    pins = payload["pins"]
    if not isinstance(pins, dict) or set(pins) != {"donor", "source"}:
        raise ContractError("idle-bind JSON pins must contain donor and source")
    _validate_manifest_pin(
        pins["donor"],
        label="donor",
        actual_bytes=len(donor_bytes),
        actual_sha256=_sha256(donor_bytes),
    )
    _validate_manifest_pin(
        pins["source"],
        label="source",
        actual_bytes=len(source_bytes),
        actual_sha256=_sha256(source_bytes),
    )
    transforms = payload["transforms"]
    compiled_evidence: Mapping[str, object] | None = None
    if pose == "compiled-bind":
        expected_evidence = compiled_bind_evidence_template()
        if payload.get("evidence") != expected_evidence:
            raise ContractError(
                "compiled-bind evidence does not match the pinned parser/model record"
            )
        if transforms != COMPILED_BIND_TRANSFORMS:
            raise ContractError(
                "compiled-bind transforms do not match the pinned DATA records"
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
        compiled_evidence = copy.deepcopy(expected_evidence)
    required_transforms = {spec.bind_key for spec in PART_SPECS}
    if not isinstance(transforms, dict) or set(transforms) != required_transforms:
        missing = sorted(required_transforms - set(transforms or {}))
        extra = sorted(set(transforms or {}) - required_transforms)
        raise ContractError(
            f"idle-bind JSON requires B_root/B_slide/B_mag; missing={missing}, extra={extra}"
        )
    matrices = {
        key: _parse_manifest_transform(transforms[key], key)
        for key in sorted(required_transforms)
    }
    manifest_pin = _pin_payload(path, raw)
    manifest_pin["pose"] = pose
    manifest_pin["compiled_evidence"] = compiled_evidence
    return matrices, manifest_pin


def _walk_nodes(root: dict[str, Any]) -> Iterable[tuple[dict[str, Any], str, str | None]]:
    root_name = root.get("Name")
    if not isinstance(root_name, str) or not root_name:
        raise ContractError("prefab root has no Name")

    def walk(
        node: dict[str, Any], relative_path: str, parent_guid: str | None
    ) -> Iterable[tuple[dict[str, Any], str, str | None]]:
        yield node, relative_path, parent_guid
        for child in node.get("Children", []) or []:
            if not isinstance(child, dict) or not isinstance(child.get("Name"), str):
                raise ContractError(f"invalid child below {relative_path or root_name}")
            child_path = (
                f"{relative_path}/{child['Name']}" if relative_path else child["Name"]
            )
            yield from walk(child, child_path, node.get("__guid"))

    yield from walk(root, "", None)


def index_prefab(prefab: object) -> PrefabIndex:
    if not isinstance(prefab, dict) or not isinstance(prefab.get("RootObject"), dict):
        raise ContractError("prefab must contain a RootObject")
    nodes_by_guid: dict[str, dict[str, Any]] = {}
    nodes_by_path: dict[str, dict[str, Any]] = {}
    components_by_guid: dict[str, dict[str, Any]] = {}
    component_owners: dict[str, str] = {}
    parent_by_guid: dict[str, str | None] = {}
    for node, path, parent_guid in _walk_nodes(prefab["RootObject"]):
        guid = node.get("__guid")
        if not isinstance(guid, str) or not GUID_RE.fullmatch(guid):
            raise ContractError(f"node at {path or '<root>'} has invalid GUID")
        if guid in nodes_by_guid or guid in components_by_guid:
            raise ContractError(f"duplicate GUID: {guid}")
        if path in nodes_by_path:
            raise ContractError(f"duplicate hierarchy path: {path}")
        nodes_by_guid[guid] = node
        nodes_by_path[path] = node
        parent_by_guid[guid] = parent_guid
        for component in node.get("Components", []) or []:
            component_guid = component.get("__guid")
            if not isinstance(component_guid, str) or not GUID_RE.fullmatch(component_guid):
                raise ContractError(f"component below {path or '<root>'} has invalid GUID")
            if component_guid in nodes_by_guid or component_guid in components_by_guid:
                raise ContractError(f"duplicate GUID: {component_guid}")
            components_by_guid[component_guid] = component
            component_owners[component_guid] = guid
    return PrefabIndex(
        nodes_by_guid=nodes_by_guid,
        nodes_by_path=nodes_by_path,
        components_by_guid=components_by_guid,
        component_owners=component_owners,
        parent_by_guid=parent_by_guid,
    )


def _reference_objects(value: object) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        if value.get("_type") in {"gameobject", "component"}:
            yield value
        for child in value.values():
            yield from _reference_objects(child)
    elif isinstance(value, list):
        for child in value:
            yield from _reference_objects(child)


def assert_references_valid(prefab: object, index: PrefabIndex) -> int:
    count = 0
    for reference in _reference_objects(prefab):
        count += 1
        owner = reference.get("go")
        if owner not in index.nodes_by_guid:
            raise ContractError(f"dangling gameobject reference: {owner}")
        if reference.get("_type") == "component":
            component = reference.get("component_id")
            if component not in index.components_by_guid:
                raise ContractError(f"dangling component reference: {component}")
            if index.component_owners[component] != owner:
                raise ContractError(
                    f"component reference owner mismatch: {component} is not on {owner}"
                )
    return count


def _components_of_type(node: Mapping[str, Any], component_type: str) -> list[dict[str, Any]]:
    return [
        component
        for component in node.get("Components", []) or []
        if component.get("__type") == component_type
    ]


def _source_inputs(
    source: dict[str, Any], source_index: PrefabIndex
) -> tuple[str, dict[str, dict[str, Any]]]:
    root = source["RootObject"]
    drivers = [
        component
        for component in _components_of_type(root, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == "models/weapons/sbox_pistol_usp/v_usp.vmdl"
    ]
    if len(drivers) != 1 or not isinstance(drivers[0].get("MaterialOverride"), str):
        raise ContractError("source must carry one invisibly material-overridden USP driver")
    invisible_material = drivers[0]["MaterialOverride"]

    templates: dict[str, dict[str, Any]] = {}
    for spec in PART_SPECS:
        matches = []
        for node in source_index.nodes_by_guid.values():
            renderers = [
                component
                for component in _components_of_type(node, "Sandbox.ModelRenderer")
                if component.get("Model") == spec.model
            ]
            if node.get("Name") == spec.node_name and len(renderers) == 1:
                matches.append(node)
        if len(matches) != 1:
            raise ContractError(
                f"source must contain exactly one {spec.node_name} renderer template"
            )
        template = matches[0]
        if template.get("Children") not in (None, []):
            raise ContractError(f"source renderer template has children: {spec.node_name}")
        templates[spec.role] = template
    return invisible_material, templates


def _deterministic_guid(role: str, kind: str) -> str:
    seed = (
        f"{EXPECTED_DONOR_SHA256}:{EXPECTED_SOURCE_SHA256}:overlay:{role}:{kind}"
    )
    return str(uuid.uuid5(GUID_NAMESPACE, seed))


def _deterministic_donor_guid(original_guid: str) -> str:
    seed = (
        f"{EXPECTED_DONOR_SHA256}:{EXPECTED_SOURCE_SHA256}:donor:{original_guid.lower()}"
    )
    return str(uuid.uuid5(GUID_NAMESPACE, seed))


def _remap_strings(value: Any, mapping: Mapping[str, str]) -> Any:
    if isinstance(value, dict):
        return {key: _remap_strings(item, mapping) for key, item in value.items()}
    if isinstance(value, list):
        return [_remap_strings(item, mapping) for item in value]
    if isinstance(value, str):
        return mapping.get(value, value)
    return value


def remap_donor_prefab(
    donor: dict[str, Any],
) -> tuple[dict[str, Any], dict[str, str]]:
    """Give every donor definition a deterministic candidate-only GUID."""

    donor_index = index_prefab(donor)
    mapping = {
        guid: _deterministic_donor_guid(guid) for guid in sorted(donor_index.all_guids)
    }
    mapped = set(mapping.values())
    if len(mapped) != len(mapping):
        raise ContractError("deterministic donor GUID remap collided internally")
    if mapped & donor_index.all_guids:
        raise ContractError("deterministic donor GUID remap reused an input GUID")
    candidate = _remap_strings(copy.deepcopy(donor), mapping)
    return candidate, mapping


def _format_scalar(value: float) -> str:
    if abs(value) < 5e-16:
        value = 0.0
    return format(value, ".17g")


def _format_vector(values: Sequence[float]) -> str:
    return ",".join(_format_scalar(value) for value in values)


def _parse_sbox_vector(value: object, length: int, label: str) -> tuple[float, ...]:
    if not isinstance(value, str):
        raise ContractError(f"{label} must be an s&box vector string")
    pieces = value.split(",")
    if len(pieces) != length:
        raise ContractError(f"{label} must contain {length} comma-separated values")
    try:
        result = tuple(float(piece) for piece in pieces)
    except ValueError as exc:
        raise ContractError(f"{label} contains an invalid number") from exc
    if not all(math.isfinite(item) for item in result):
        raise ContractError(f"{label} contains a non-finite number")
    return result


def _transform_from_node(node: Mapping[str, Any], label: str) -> Transform:
    return Transform(
        position=_parse_sbox_vector(node.get("Position"), 3, f"{label}.Position"),  # type: ignore[arg-type]
        quaternion=_parse_sbox_vector(node.get("Rotation"), 4, f"{label}.Rotation"),  # type: ignore[arg-type]
        scale=_parse_sbox_vector(node.get("Scale"), 3, f"{label}.Scale"),  # type: ignore[arg-type]
    )


def _serialized_transform(transform: Transform) -> dict[str, str]:
    return {
        "Position": _format_vector(transform.position),
        "Rotation": _format_vector(transform.quaternion),
        "Scale": _format_vector(transform.scale),
    }


def assert_local_reconstruction(
    bind_matrices: Mapping[str, Matrix4],
    local_matrices: Mapping[str, Matrix4],
    assembly_matrix: Matrix4,
) -> dict[str, float]:
    required = {spec.bind_key for spec in PART_SPECS}
    if set(bind_matrices) != required or set(local_matrices) != required:
        raise ContractError("reconstruction requires B_root/B_slide/B_mag")

    for first, second in combinations(sorted(required), 2):
        bind_delta = matrix_max_delta(bind_matrices[first], bind_matrices[second])
        local_delta = matrix_max_delta(local_matrices[first], local_matrices[second])
        if bind_delta > TRS_TOLERANCE and local_delta <= TRS_TOLERANCE:
            raise ContractError(
                f"copied local transform rejected across distinct moving parents: "
                f"{first}/{second}"
            )

    errors: dict[str, float] = {}
    for key in sorted(required):
        reconstructed = matrix_multiply(bind_matrices[key], local_matrices[key])
        error = matrix_max_delta(reconstructed, assembly_matrix)
        if error > TRS_TOLERANCE:
            raise ContractError(
                f"{key} local does not reconstruct geometry assembly A; delta={error}"
            )
        errors[key] = error
    return errors


def compute_locals(
    bind_matrices: Mapping[str, Matrix4], assembly: Transform
) -> tuple[dict[str, Transform], dict[str, float]]:
    assembly_matrix = transform_matrix(assembly)
    local_transforms: dict[str, Transform] = {}
    serialized_local_matrices: dict[str, Matrix4] = {}
    for spec in PART_SPECS:
        bind = bind_matrices[spec.bind_key]
        exact_local = matrix_multiply(matrix_inverse(bind), assembly_matrix)
        local = decompose_trs(exact_local, f"{spec.bind_key} local")
        serialized = _serialized_transform(local)
        reparsed = Transform(
            position=_parse_sbox_vector(serialized["Position"], 3, "Position"),  # type: ignore[arg-type]
            quaternion=_parse_sbox_vector(serialized["Rotation"], 4, "Rotation"),  # type: ignore[arg-type]
            scale=_parse_sbox_vector(serialized["Scale"], 3, "Scale"),  # type: ignore[arg-type]
        )
        local_transforms[spec.bind_key] = reparsed
        serialized_local_matrices[spec.bind_key] = transform_matrix(reparsed)
    errors = assert_local_reconstruction(
        bind_matrices, serialized_local_matrices, assembly_matrix
    )
    return local_transforms, errors


def _clone_part_template(
    template: dict[str, Any], spec: PartSpec, local: Transform
) -> dict[str, Any]:
    node = copy.deepcopy(template)
    node["__guid"] = _deterministic_guid(spec.role, "node")
    node.update(_serialized_transform(local))
    components = node.get("Components", []) or []
    if len(components) != 1:
        raise ContractError(f"{spec.node_name} template component count changed")
    components[0]["__guid"] = _deterministic_guid(spec.role, "renderer")
    node["Children"] = []
    return node


def _component_by_guid(index: PrefabIndex, guid: str) -> dict[str, Any]:
    try:
        return index.components_by_guid[guid]
    except KeyError as exc:
        raise ContractError(f"preserved donor component disappeared: {guid}") from exc


def _validate_donor_preserved(
    donor: dict[str, Any],
    candidate: dict[str, Any],
    *,
    donor_guid_mapping: Mapping[str, str],
    source_guids: set[str],
    invisible_material: str,
    added_node_guids: set[str],
    added_component_guids: set[str],
) -> dict[str, int]:
    donor_index = index_prefab(donor)
    candidate_index = index_prefab(candidate)
    mapped_donor_guids = set(donor_guid_mapping.values())
    missing = mapped_donor_guids - candidate_index.all_guids
    if missing:
        raise ContractError(f"candidate lost remapped donor definitions: {sorted(missing)}")
    if candidate_index.all_guids - mapped_donor_guids != (
        added_node_guids | added_component_guids
    ):
        raise ContractError("candidate GUID delta is not exactly the six deterministic GUIDs")
    if candidate_index.all_guids & donor_index.all_guids:
        raise ContractError("candidate GUIDs intersect the pinned donor GUID set")
    if candidate_index.all_guids & source_guids:
        raise ContractError("candidate GUIDs intersect the pinned source GUID set")

    donor_root_guid = donor["RootObject"]["__guid"]
    for guid, donor_node in donor_index.nodes_by_guid.items():
        candidate_guid = donor_guid_mapping[guid]
        candidate_node = candidate_index.nodes_by_guid[candidate_guid]
        expected_node = _remap_strings(donor_node, donor_guid_mapping)
        donor_scalars = {
            key: value
            for key, value in expected_node.items()
            if key not in {"Children", "Components", "Name"}
        }
        candidate_scalars = {
            key: value
            for key, value in candidate_node.items()
            if key not in {"Children", "Components", "Name"}
        }
        if candidate_scalars != donor_scalars:
            raise ContractError(f"donor node fields changed: {guid}")
        if guid != donor_root_guid and candidate_node.get("Name") != donor_node.get("Name"):
            raise ContractError(f"donor node name changed: {guid}")
        donor_children = [
            donor_guid_mapping[child["__guid"]]
            for child in donor_node.get("Children", []) or []
        ]
        candidate_children = [
            child["__guid"]
            for child in candidate_node.get("Children", []) or []
            if child.get("__guid") not in added_node_guids
        ]
        if candidate_children != donor_children:
            raise ContractError(f"donor hierarchy changed: {guid}")

    root = donor["RootObject"]
    view_models = _components_of_type(root, "Dxura.RP.Game.ViewModel")
    drivers = [
        component
        for component in _components_of_type(root, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == "models/weapons/sbox_pistol_usp/v_usp.vmdl"
    ]
    arms = [
        component
        for component in _components_of_type(root, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == "models/first_person/v_first_person_arms_human.vmdl"
    ]
    if len(view_models) != 1 or len(drivers) != 1 or len(arms) != 1:
        raise ContractError("pinned donor root component contract changed")
    protected_guids = {
        donor_guid_mapping[view_models[0]["__guid"]],
        donor_guid_mapping[arms[0]["__guid"]],
    }
    driver_guid = donor_guid_mapping[drivers[0]["__guid"]]

    for guid, component in donor_index.components_by_guid.items():
        candidate_guid = donor_guid_mapping[guid]
        candidate_component = _component_by_guid(candidate_index, candidate_guid)
        expected = _remap_strings(component, donor_guid_mapping)
        if candidate_guid == driver_guid:
            expected["MaterialOverride"] = invisible_material
            if candidate_component != expected:
                raise ContractError("USP donor driver changed beyond invisible material")
        elif candidate_component != expected:
            raise ContractError(f"donor component changed: {guid}")
    if not protected_guids <= candidate_index.components_by_guid.keys():
        raise ContractError("ViewModel or arms component was not preserved")

    donor_vm = _remap_strings(view_models[0], donor_guid_mapping)
    candidate_vm = _component_by_guid(candidate_index, donor_vm["__guid"])
    if candidate_vm.get("Muzzle") != donor_vm.get("Muzzle") or candidate_vm.get(
        "EjectionPort"
    ) != donor_vm.get("EjectionPort"):
        raise ContractError("donor muzzle/ejection references changed")
    references = assert_references_valid(candidate, candidate_index)
    return {
        "donor_nodes": len(donor_index.nodes_by_guid),
        "donor_components": len(donor_index.components_by_guid),
        "candidate_nodes": len(candidate_index.nodes_by_guid),
        "candidate_components": len(candidate_index.components_by_guid),
        "donor_guids_remapped": len(donor_guid_mapping),
        "input_guid_intersections": 0,
        "valid_references": references,
    }


def transform_prefab(
    donor: dict[str, Any],
    source: dict[str, Any],
    bind_matrices: Mapping[str, Matrix4],
) -> tuple[dict[str, Any], dict[str, object]]:
    donor_index = index_prefab(donor)
    source_index = index_prefab(source)
    assert_references_valid(donor, donor_index)
    assert_references_valid(source, source_index)
    invisible_material, templates = _source_inputs(source, source_index)
    assembly = derive_geometry_assembly()
    locals_by_bind, reconstruction_errors = compute_locals(bind_matrices, assembly)

    candidate, donor_guid_mapping = remap_donor_prefab(donor)
    candidate["RootObject"]["Name"] = "vm_desert_eagle_common_origin_candidate"
    candidate_index = index_prefab(candidate)
    drivers = [
        component
        for component in _components_of_type(
            candidate["RootObject"], "Sandbox.SkinnedModelRenderer"
        )
        if component.get("Model") == "models/weapons/sbox_pistol_usp/v_usp.vmdl"
    ]
    if len(drivers) != 1:
        raise ContractError("pinned donor USP driver count changed")
    drivers[0]["MaterialOverride"] = invisible_material

    added_nodes: set[str] = set()
    added_components: set[str] = set()
    part_reports: dict[str, object] = {}
    for spec in PART_SPECS:
        parent = candidate_index.nodes_by_path.get(spec.donor_parent_path)
        if parent is None:
            raise ContractError(f"donor parent path disappeared: {spec.donor_parent_path}")
        local = locals_by_bind[spec.bind_key]
        part = _clone_part_template(templates[spec.role], spec, local)
        node_guid = part["__guid"]
        component_guid = part["Components"][0]["__guid"]
        occupied = candidate_index.all_guids | added_nodes | added_components
        if node_guid in occupied or component_guid in occupied or node_guid == component_guid:
            raise ContractError(f"deterministic GUID collision for {spec.role}")
        parent.setdefault("Children", []).append(part)
        added_nodes.add(node_guid)
        added_components.add(component_guid)
        part_reports[spec.role] = {
            "bind": spec.bind_key,
            "parent_path": spec.donor_parent_path,
            "node_guid": node_guid,
            "renderer_guid": component_guid,
            "model": spec.model,
            "local": _serialized_transform(local),
            "reconstruction_max_delta": reconstruction_errors[spec.bind_key],
        }

    preservation = _validate_donor_preserved(
        donor,
        candidate,
        donor_guid_mapping=donor_guid_mapping,
        source_guids=source_index.all_guids,
        invisible_material=invisible_material,
        added_node_guids=added_nodes,
        added_component_guids=added_components,
    )
    return candidate, {
        "assembly_A": _serialized_transform(assembly),
        "assembly_derivation": "geometry-envelope candidate from pinned bounds record",
        "parts": part_reports,
        "preservation": preservation,
        "guid_uniqueness": "PASS",
        "guid_strategy": "deterministic full donor remap plus six overlay GUIDs",
        "reconstruction": "PASS",
        "copied_local_guard": "PASS",
    }


def _decode_prefab(data: bytes, label: str) -> dict[str, Any]:
    try:
        value = json.loads(data.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"pinned {label} prefab is not valid JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise ContractError(f"pinned {label} prefab root is not an object")
    return value


def _serialize_prefab(prefab: dict[str, Any], donor_bytes: bytes) -> bytes:
    newline = "\r\n" if b"\r\n" in donor_bytes else "\n"
    text = json.dumps(prefab, ensure_ascii=False, indent=2) + "\n"
    if newline != "\n":
        text = text.replace("\n", newline)
    data = text.encode("utf-8")
    json.loads(data.decode("utf-8"))
    return data


def build_candidate(
    *,
    output_directory: Path,
    idle_bind_path: Path,
    idle_bind_sha256: str,
    donor_path: Path = DEFAULT_DONOR,
    source_path: Path = DEFAULT_SOURCE,
) -> dict[str, object]:
    output_directory = assert_temp_output_directory(output_directory)
    donor_path = donor_path.resolve()
    source_path = source_path.resolve()
    donor_bytes = _verify_prefab_pin(
        donor_path, EXPECTED_DONOR_BYTES, EXPECTED_DONOR_SHA256, "donor"
    )
    source_bytes = _verify_prefab_pin(
        source_path, EXPECTED_SOURCE_BYTES, EXPECTED_SOURCE_SHA256, "source"
    )
    bind_matrices, idle_bind_pin = load_idle_bind_manifest(
        idle_bind_path,
        idle_bind_sha256,
        donor_bytes=donor_bytes,
        source_bytes=source_bytes,
    )
    donor = _decode_prefab(donor_bytes, "donor")
    source = _decode_prefab(source_bytes, "source")
    candidate, transform_report = transform_prefab(donor, source, bind_matrices)
    output_bytes = _serialize_prefab(candidate, donor_bytes)

    output_directory.mkdir(parents=True, exist_ok=True)
    output = output_directory / OUTPUT_NAME
    if _is_within(output, GAME_ASSETS_ROOT):
        raise ContractError("game/Assets output is impossible for this TEMP-ONLY tool")
    staged = pipeline_io.staging_path(output)
    staged.write_bytes(output_bytes)
    if staged.read_bytes() != output_bytes:
        raise ContractError("staged candidate bytes changed after write")
    pipeline_io.promote_files([(staged, output)])
    if output.read_bytes() != output_bytes:
        raise ContractError("candidate bytes changed during temporary promotion")

    return {
        "result": "PASS",
        "mode": "TEMP_ONLY",
        "output": _pin_payload(output, output_bytes),
        "inputs": {
            "donor": _pin_payload(donor_path, donor_bytes),
            "source": _pin_payload(source_path, source_bytes),
            "idle_bind": idle_bind_pin,
            "geometry_bounds": {
                "bytes": len(GEOMETRY_BOUNDS_RECORD),
                "sha256": EXPECTED_GEOMETRY_BOUNDS_SHA256,
            },
        },
        "transform_report": transform_report,
        "game_tree_written": False,
        "proof_ceiling": (
            "deterministic TEMP structural candidate; compiled skeleton bind is "
            "not observed IdlePose or rendered animation proof; compile, editor, "
            "runtime, animation, and visual fit are UNVERIFIED"
            if idle_bind_pin["pose"] == "compiled-bind"
            else "deterministic TEMP structural candidate; compile, editor, runtime, "
            "animation, and visual fit are UNVERIFIED"
        ),
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build one deterministic temporary Desert Eagle common-origin candidate. "
            "No game-tree output or acceptance mode exists."
        )
    )
    parser.add_argument(
        "--idle-bind-json",
        type=Path,
        help=(
            "Version-1 B_root/B_slide/B_mag bind JSON (observed idle-bind or "
            "byte-pinned compiled-bind; read only)."
        ),
    )
    parser.add_argument(
        "--idle-bind-sha256",
        help="Expected full SHA-256 of the idle-bind JSON bytes.",
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
    if args.print_compiled_bind_manifest:
        if args.idle_bind_json is not None or args.idle_bind_sha256 is not None:
            parser.error(
                "--print-compiled-bind-manifest cannot be combined with build options"
            )
        print(json.dumps(compiled_bind_manifest_template(), indent=2))
        return 0
    if args.idle_bind_json is None or args.idle_bind_sha256 is None:
        parser.error(
            "--idle-bind-json and --idle-bind-sha256 are required to build a candidate"
        )
    output_directory = Path(
        tempfile.mkdtemp(prefix="dxrp_deserteagle_common_origin_")
    )
    try:
        report = build_candidate(
            output_directory=output_directory,
            idle_bind_path=args.idle_bind_json,
            idle_bind_sha256=args.idle_bind_sha256,
        )
    except (ContractError, OSError) as exc:
        parser.exit(2, f"DXRP_DESERTEAGLE_COMMON_ORIGIN_ERROR: {exc}\n")
    print(
        "DXRP_DESERTEAGLE_COMMON_ORIGIN="
        + json.dumps(report, sort_keys=True)
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
