"""Build fail-closed TEMP-only AR-15/SR-25 M4 wrapper candidates.

The custom AR-15 and SR-25 parts were exported around one common origin, while
their moving renderers currently live directly on animated M4 donor nodes.  A
renderer wrapper local ``W`` must therefore satisfy::

    P * W = B
    W = inverse(P) * B

``P`` is re-read from the exact byte-pinned compiled M4 model with the exact
byte-pinned Source2Viewer CLI.  ``B`` is not an accepted fit: it is only the
current, byte-pinned body renderer baseline, authored at identity on
``weapon_root_children`` in each source prefab.  This orchestrator always
reports ``measured_and_accepted=false``.

The existing renderer-wrapper builder performs the prefab rewrite.  All
outputs remain strict children of the operating-system TEMP directory.  There
is deliberately no product-write mode or switch.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import importlib
import json
import math
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


class ContractError(RuntimeError):
    """Raised when pinned evidence or the TEMP-only contract does not hold."""


REPO_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()


@dataclass(frozen=True)
class FilePin:
    path: Path
    bytes: int
    sha256: str


@dataclass(frozen=True)
class Transform:
    position: tuple[float, float, float]
    quaternion: tuple[float, float, float, float]
    scale: tuple[float, float, float]


@dataclass(frozen=True)
class BoneRecord:
    bone: str
    parent: str | None
    transform: Transform


@dataclass(frozen=True)
class CompiledBindSample:
    records: Mapping[str, BoneRecord]
    evidence: Mapping[str, Any]
    data_stdout: bytes


@dataclass(frozen=True)
class BodyBaseline:
    family: str
    node_path: str
    body_model: str
    transform: Transform
    evidence: Mapping[str, Any]


IDENTITY = Transform(
    position=(0.0, 0.0, 0.0),
    quaternion=(0.0, 0.0, 0.0, 1.0),
    scale=(1.0, 1.0, 1.0),
)

PARSER_VERSION = "19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa"
PARSER_PIN = FilePin(
    path=Path(r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe"),
    bytes=108_603_232,
    sha256="36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0",
)
M4_MODEL_PIN = FilePin(
    path=Path(
        r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
        r"\sbox_assault_m4a1\v_m4a1.3912fd337e9d3488.vmdl_c"
    ),
    bytes=2_019_792,
    sha256="8726559C336098469AAA7C9A9A5018FE4825D3C6375EF406AB849C3EC82AABE4",
)
M4_RESOURCE_NAME = "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl"
EXPECTED_BONE_COUNT = 80
EXPECTED_DATA_STDOUT_BYTES = 22_429
EXPECTED_DATA_STDOUT_SHA256 = (
    "12338DBDE712B9ED754CD192538F7F180DF1E35864353E241EEBF946C780D7B2"
)
EXPECTED_REQUIRED_RECORDS_SHA256 = (
    "CD1965EF7E5EC9717E5B0DA1109529FC2507AD8CBB44E886A1219104B9E8E2C9"
)

WRAPPER_BUILDER_PIN = FilePin(
    path=Path(__file__).with_name("build_viewmodel_renderer_wrappers.py"),
    bytes=39_979,
    sha256="7A8B36BE2CE24E57C00A5195DED5AD6E905C6BB9DB6B50EE9BC9F7DBEF7002DE",
)

M4_SOURCE_PREFAB_PIN = FilePin(
    path=(
        REPO_ROOT
        / "game/Assets/gameplay/equipment/weapons/m4a1/vm_m4a1.prefab"
    ),
    bytes=74_922,
    sha256="D1FBDB400EFE51583BB95267BBBB44A80B67952E5DFB55B6369C33D971029767",
)

SOURCE_PREFAB_PINS: Mapping[str, FilePin] = {
    "ar15": FilePin(
        path=(
            REPO_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab"
        ),
        bytes=77_843,
        sha256="B573E6920D5C095FE09A5FD256BDBFE6FEFF6E03B927BD36E059AB5A02574043",
    ),
    "sr25": FilePin(
        path=(
            REPO_ROOT
            / "game/Assets/addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab"
        ),
        bytes=85_430,
        sha256="FFC3EB52516107EE9292821F1ECDE575E46EE85B04F1FC3B695EC21B40F6C8D4",
    ),
}

BODY_NODE_PATHS: Mapping[str, str] = {
    "ar15": "vm_ar15/weapon_root/weapon_root_children",
    "sr25": "vm_sr25/weapon_root/weapon_root_children",
}
BODY_MODELS: Mapping[str, str] = {
    "ar15": "addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_body.vmdl",
    "sr25": "addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_body.vmdl",
}

AR15_ADDITIONAL_RENDERER_ROOT_PATH = "vm_ar15/weapon_root"
AR15_ADDITIONAL_RENDERER_ROOT_GUID = "a436e623-2f6b-4cff-8639-1ad59beec5d5"
AR15_CUSTOM_MODEL_RENDERER_GUIDS = frozenset(
    {
        "45f5ca26-f233-4c12-929b-3e44444f60bc",
        "68ff4e70-374a-484e-afa3-27bce99f6252",
        "91c5a0d0-c18e-471f-a138-a022b0b2cc78",
        "b5a13ce8-484f-4e74-bcf2-3943e01a9808",
        "cdc94a87-0979-4f5c-bb38-50757cdf24fd",
        "dbee7b0b-308d-4bd9-8d15-d959f15fca9b",
        "f888005b-4ec8-4922-bf0f-d2ed9ccc3adb",
    }
)

SR25_VIEW_MODEL_COMPONENT_GUID = "57250000-5a25-4000-8000-000000000047"
SR25_EJECTION_PORT_GUID = "57250000-5a25-4000-8000-000000000066"
SR25_EJECTION_PORT_PATH = (
    "vm_sr25/weapon_root/weapon_root_children/sr25_ejection_port"
)
SR25_EJECTION_PORT_BASELINE = Transform(
    position=(-5.898, -0.953, 0.263),
    quaternion=(0.0, 0.0, 0.0, 1.0),
    scale=(1.0, 1.0, 1.0),
)
SR25_WEAPON_ROOT_CHILDREN_GUID = "57250000-5a25-4000-8000-00000000004c"
SR25_BOLT_FLAP_GUID = "57250000-5a25-4000-8000-000000000036"
SR25_BOLT_FLAP_PATH = "vm_sr25/weapon_root/weapon_root_children/bolt_flap"
SR25_BOLT_FLAP_WRAPPER_GUID = "7aeb6592-09a1-5efd-9d16-098e25c98d48"
M4_VIEW_MODEL_COMPONENT_GUID = "2dd95a44-30f3-41fd-ac16-dd967ea5d163"
M4_BOLT_FLAP_GUID = "60019c0b-5443-4e23-9154-37648ba90882"

FAMILY_MOVING_BONES: Mapping[str, Mapping[str, str]] = {
    "ar15": {
        "vm_ar15/weapon_root/weapon_root_children/stock": "stock",
        "vm_ar15/weapon_root/weapon_root_children/trigger": "trigger",
        "vm_ar15/weapon_root/weapon_root_children/magazine": "magazine",
        "vm_ar15/weapon_root/weapon_root_children/bolt_flap": "bolt_flap",
        "vm_ar15/weapon_root/weapon_root_children/bolt": "bolt",
        "vm_ar15/weapon_root/weapon_root_children/charging_handle": "charging_handle",
    },
    "sr25": {
        "vm_sr25/weapon_root/weapon_root_children/stock": "stock",
        "vm_sr25/weapon_root/weapon_root_children/trigger": "trigger",
        "vm_sr25/weapon_root/weapon_root_children/magazine": "magazine",
        "vm_sr25/weapon_root/weapon_root_children/mode_selector": "mode_selector",
        "vm_sr25/weapon_root/weapon_root_children/bolt_flap": "bolt_flap",
        "vm_sr25/weapon_root/weapon_root_children/bolt": "bolt",
        "vm_sr25/weapon_root/weapon_root_children/charging_handle": "charging_handle",
    },
}

EXPECTED_DATA_RECORDS: Mapping[str, BoneRecord] = {
    "weapon_root_children": BoneRecord(
        bone="weapon_root_children",
        parent="weapon_root",
        transform=Transform(
            (0.0, 0.0, 0.0),
            (0.026089, -0.046191, 0.0, 0.998592),
            (1.0, 1.0, 1.0),
        ),
    ),
    "stock": BoneRecord(
        bone="stock",
        parent="weapon_root_children",
        transform=Transform(
            (-5.538097, 0.051171, 1.236977),
            (0.046192, 0.026089, 0.998592, 0.0),
            (1.0, 1.0, 1.0),
        ),
    ),
    "trigger": BoneRecord(
        bone="trigger",
        parent="weapon_root_children",
        transform=Transform(
            (-0.296543, -0.038442, -0.72308),
            (-0.018448, 0.738773, -0.018447, 0.673449),
            (1.0, 1.0, 1.0),
        ),
    ),
    "magazine": BoneRecord(
        bone="magazine",
        parent="weapon_root_children",
        transform=Transform(
            (2.447816, -0.129532, -2.595881),
            (-0.018448, 0.738773, -0.018448, 0.67345),
            (1.0, 1.0, 1.0),
        ),
    ),
    "mode_selector": BoneRecord(
        bone="mode_selector",
        parent="weapon_root_children",
        transform=Transform(
            (-1.348378, 0.536545, -0.458675),
            (0.046192, 0.026089, 0.998592, 0.0),
            (1.0, 1.0, 1.0),
        ),
    ),
    "bolt_flap": BoneRecord(
        bone="bolt_flap",
        parent="weapon_root_children",
        transform=Transform(
            (2.401474, -0.757396, 0.453261),
            (-0.535813, 0.57736, 0.460112, 0.409703),
            (1.0, 1.0, 1.0),
        ),
    ),
    "bolt": BoneRecord(
        bone="bolt",
        parent="weapon_root_children",
        transform=Transform(
            (1.574773, 0.033958, 0.57799),
            (-0.02609, 0.046191, 0.0, 0.998592),
            (1.0, 1.0, 1.0),
        ),
    ),
    "charging_handle": BoneRecord(
        bone="charging_handle",
        parent="weapon_root_children",
        transform=Transform(
            (-2.70557, 0.095134, 1.948513),
            (-0.026089, 0.046191, 0.0, 0.998592),
            (1.0, 1.0, 1.0),
        ),
    ),
}


@dataclass(frozen=True)
class CandidatePin:
    file: str
    bytes: int
    sha256: str


EXPECTED_CANDIDATE_PINS: Mapping[str, CandidatePin] = {
    "ar15": CandidatePin(
        "vm_ar15.wrapper_candidate.prefab",
        84_627,
        "92BF6A0E7ECC94A1A8F0E1391253CF2B67724D3EFDE5EDD8FFC52EB96DEE5B82",
    ),
    "sr25": CandidatePin(
        "vm_sr25.wrapper_candidate.prefab",
        94_773,
        "9378D22F99F81BEDC83043B99C6FCCAA9C7C6E20D4EFFED0BD1137BCDCEE694D",
    ),
}

WRAPPER_CANDIDATE_PINS: Mapping[str, CandidatePin] = {
    "ar15": CandidatePin(
        "vm_ar15.wrapper_candidate.prefab",
        84_492,
        "7BF9FD0E53D5FF6617C304F5864DD76E95C8A14FADFD8FFD4BB330AC46AA256B",
    ),
    "sr25": CandidatePin(
        "vm_sr25.wrapper_candidate.prefab",
        94_650,
        "D312DD3AC96B34F93DB6F6C01B1CED9742EFF7AEA4749CF4B3A1F482F20CE519",
    ),
}

REPORT_FILE = "m4_current_body_wrapper_report.json"


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _pin_payload(pin: FilePin) -> dict[str, object]:
    return {
        "path": str(pin.path.resolve()),
        "bytes": pin.bytes,
        "sha256": pin.sha256,
    }


def _verify_file_pin(pin: FilePin, label: str) -> bytes:
    path = pin.path.resolve(strict=False)
    if not pin.path.is_absolute():
        raise ContractError(f"Pinned {label} path must be absolute: {pin.path}")
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"Pinned {label} is unreadable: {path}: {exc}") from exc
    if len(data) != pin.bytes:
        raise ContractError(
            f"Pinned {label} byte mismatch: expected {pin.bytes}, got {len(data)}"
        )
    actual = _sha256(data)
    if actual != pin.sha256.upper():
        raise ContractError(
            f"Pinned {label} SHA-256 mismatch: expected {pin.sha256.upper()}, got {actual}"
        )
    return data


def _run_process(arguments: Sequence[str], label: str) -> tuple[bytes, bytes]:
    try:
        result = subprocess.run(
            list(arguments),
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise ContractError(f"Pinned parser {label} failed to run: {exc}") from exc
    if result.returncode != 0:
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise ContractError(
            f"Pinned parser {label} exited {result.returncode}: {stderr or '<no stderr>'}"
        )
    if result.stderr:
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise ContractError(f"Pinned parser {label} emitted stderr: {stderr}")
    if not result.stdout:
        raise ContractError(f"Pinned parser {label} emitted empty stdout")
    return result.stdout, result.stderr


def _validate_parser_version(stdout: bytes) -> str:
    try:
        text = stdout.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ContractError("Pinned parser --version stdout is not UTF-8") from exc
    match = re.search(r"(?m)^Version:\s*([^\r\n]+)\s*$", text)
    if match is None:
        raise ContractError("Pinned parser --version stdout lacks a Version line")
    actual = match.group(1).strip()
    if actual != PARSER_VERSION:
        raise ContractError(
            f"Pinned parser version mismatch: expected {PARSER_VERSION}, got {actual}"
        )
    return actual


def _extract_array(text: str, key: str) -> str:
    matches = list(re.finditer(rf"\b{re.escape(key)}\s*=\s*\[", text))
    if len(matches) != 1:
        raise ContractError(
            f"Pinned DATA must contain exactly one {key} array; got {len(matches)}"
        )
    start = text.find("[", matches[0].start())
    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(text)):
        character = text[index]
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                in_string = False
            continue
        if character == '"':
            in_string = True
        elif character == "[":
            depth += 1
        elif character == "]":
            depth -= 1
            if depth == 0:
                return text[start + 1 : index]
    raise ContractError(f"Pinned DATA {key} array is unterminated")


def _parse_vector_rows(body: str, width: int, label: str) -> list[tuple[float, ...]]:
    rows = re.findall(r"\[\s*([^\[\]]+?)\s*\]", body)
    parsed: list[tuple[float, ...]] = []
    for row in rows:
        fields = [field.strip() for field in row.split(",") if field.strip()]
        if len(fields) != width:
            raise ContractError(f"Pinned DATA {label} row has {len(fields)} values")
        try:
            values = tuple(float(field) for field in fields)
        except ValueError as exc:
            raise ContractError(f"Pinned DATA {label} row is non-numeric") from exc
        if not all(math.isfinite(value) for value in values):
            raise ContractError(f"Pinned DATA {label} contains a non-finite value")
        parsed.append(values)
    return parsed


def parse_compiled_data_records(data_stdout: bytes) -> dict[str, BoneRecord]:
    try:
        text = data_stdout.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ContractError("Pinned parser DATA stdout is not UTF-8") from exc
    if f'm_name = "{M4_RESOURCE_NAME}"' not in text:
        raise ContractError("Pinned DATA resource name does not identify the M4 viewmodel")

    names_body = _extract_array(text, "m_boneName")
    parents_body = _extract_array(text, "m_nParent")
    positions_body = _extract_array(text, "m_bonePosParent")
    rotations_body = _extract_array(text, "m_boneRotParent")

    names = re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"', names_body)
    parents = [int(value) for value in re.findall(r"(?<![.\w])-?\d+(?![.\w])", parents_body)]
    positions = _parse_vector_rows(positions_body, 3, "m_bonePosParent")
    rotations = _parse_vector_rows(rotations_body, 4, "m_boneRotParent")
    lengths = {len(names), len(parents), len(positions), len(rotations)}
    if lengths != {EXPECTED_BONE_COUNT}:
        raise ContractError(
            "Pinned DATA skeleton arrays must all contain "
            f"{EXPECTED_BONE_COUNT} records; got names={len(names)}, "
            f"parents={len(parents)}, positions={len(positions)}, "
            f"rotations={len(rotations)}"
        )
    if len(set(names)) != len(names):
        raise ContractError("Pinned DATA skeleton contains duplicate bone names")

    records: dict[str, BoneRecord] = {}
    for index, name in enumerate(names):
        parent_index = parents[index]
        if parent_index < -1 or parent_index >= len(names):
            raise ContractError(
                f"Pinned DATA bone {name} has invalid parent index {parent_index}"
            )
        parent = None if parent_index == -1 else names[parent_index]
        records[name] = BoneRecord(
            bone=name,
            parent=parent,
            transform=Transform(
                position=positions[index],  # type: ignore[arg-type]
                quaternion=rotations[index],  # type: ignore[arg-type]
                scale=(1.0, 1.0, 1.0),
            ),
        )
    return records


def _record_payload(record: BoneRecord) -> dict[str, object]:
    return {
        "bone": record.bone,
        "parent": record.parent,
        "position": format_vector(record.transform.position),
        "quaternion": format_vector(record.transform.quaternion),
        "scale": format_vector(record.transform.scale),
    }


def _records_payload(records: Mapping[str, BoneRecord]) -> dict[str, object]:
    return {
        bone: _record_payload(records[bone])
        for bone in EXPECTED_DATA_RECORDS
    }


def validate_required_records(records: Mapping[str, BoneRecord]) -> None:
    for bone, expected in EXPECTED_DATA_RECORDS.items():
        actual = records.get(bone)
        if actual is None:
            raise ContractError(f"Pinned DATA lacks required bone {bone}")
        if actual != expected:
            raise ContractError(
                f"Pinned DATA record mismatch for {bone}: expected "
                f"{_record_payload(expected)}, got {_record_payload(actual)}"
            )


def collect_compiled_bind_sample() -> CompiledBindSample:
    _verify_file_pin(PARSER_PIN, "Source2Viewer parser")
    _verify_file_pin(M4_MODEL_PIN, "compiled M4 viewmodel")

    version_stdout, _ = _run_process(
        (str(PARSER_PIN.path), "--version"), "version probe"
    )
    version = _validate_parser_version(version_stdout)
    command = (
        str(PARSER_PIN.path),
        "-i",
        str(M4_MODEL_PIN.path),
        "-b",
        "DATA",
    )
    data_stdout, _ = _run_process(command, "-b DATA extraction")
    if len(data_stdout) != EXPECTED_DATA_STDOUT_BYTES:
        raise ContractError(
            "Pinned parser DATA stdout byte mismatch: expected "
            f"{EXPECTED_DATA_STDOUT_BYTES}, got {len(data_stdout)}"
        )
    data_stdout_sha256 = _sha256(data_stdout)
    if data_stdout_sha256 != EXPECTED_DATA_STDOUT_SHA256:
        raise ContractError(
            "Pinned parser DATA stdout SHA-256 mismatch: expected "
            f"{EXPECTED_DATA_STDOUT_SHA256}, got {data_stdout_sha256}"
        )
    records = parse_compiled_data_records(data_stdout)
    validate_required_records(records)
    required_payload = _records_payload(records)
    required_bytes = json.dumps(
        required_payload, sort_keys=True, separators=(",", ":"), ensure_ascii=True
    ).encode("ascii")
    required_records_sha256 = _sha256(required_bytes)
    if required_records_sha256 != EXPECTED_REQUIRED_RECORDS_SHA256:
        raise ContractError(
            "Pinned required-record payload SHA-256 mismatch: expected "
            f"{EXPECTED_REQUIRED_RECORDS_SHA256}, got {required_records_sha256}"
        )
    _verify_file_pin(PARSER_PIN, "Source2Viewer parser after DATA extraction")
    _verify_file_pin(M4_MODEL_PIN, "compiled M4 viewmodel after DATA extraction")
    evidence = {
        "sample_kind": "compiled_model_bind",
        "sensor": f"Source2Viewer-CLI {version} -b DATA stdout",
        "parser": {**_pin_payload(PARSER_PIN), "version": version},
        "compiled_model": _pin_payload(M4_MODEL_PIN),
        "command": list(command),
        "data_stdout": {
            "bytes": len(data_stdout),
            "sha256": data_stdout_sha256,
        },
        "bone_count": len(records),
        "scale_basis": (
            "m_modelSkeleton DATA supplies parent-local position/quaternion; "
            "wrapper composition uses unit scale"
        ),
        "required_records_sha256": required_records_sha256,
        "required_parent_local_records": required_payload,
    }
    return CompiledBindSample(records=records, evidence=evidence, data_stdout=data_stdout)


def _parse_number_vector(value: object, width: int, label: str) -> tuple[float, ...]:
    if not isinstance(value, str):
        raise ContractError(f"{label} must be a comma-separated string")
    fields = [field.strip() for field in value.split(",")]
    if len(fields) != width:
        raise ContractError(f"{label} must contain exactly {width} values")
    try:
        result = tuple(float(field) for field in fields)
    except ValueError as exc:
        raise ContractError(f"{label} contains a non-numeric value") from exc
    if not all(math.isfinite(item) for item in result):
        raise ContractError(f"{label} contains a non-finite value")
    return result


def _node_transform(node: Mapping[str, Any], label: str) -> Transform:
    position = _parse_number_vector(node.get("Position"), 3, f"{label}.Position")
    quaternion = _parse_number_vector(node.get("Rotation"), 4, f"{label}.Rotation")
    scale = _parse_number_vector(node.get("Scale"), 3, f"{label}.Scale")
    return Transform(
        position=position,  # type: ignore[arg-type]
        quaternion=quaternion,  # type: ignore[arg-type]
        scale=scale,  # type: ignore[arg-type]
    )


def _transform_is_identity(transform: Transform, tolerance: float = 1e-9) -> bool:
    position_ok = all(abs(value) <= tolerance for value in transform.position)
    scale_ok = all(abs(value - 1.0) <= tolerance for value in transform.scale)
    direct = all(
        abs(left - right) <= tolerance
        for left, right in zip(transform.quaternion, IDENTITY.quaternion)
    )
    negated = all(
        abs(left + right) <= tolerance
        for left, right in zip(transform.quaternion, IDENTITY.quaternion)
    )
    return position_ok and scale_ok and (direct or negated)


def _index_nodes(prefab: Mapping[str, Any], label: str) -> dict[str, dict[str, Any]]:
    root = prefab.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError(f"Pinned {label} prefab lacks RootObject")
    indexed: dict[str, dict[str, Any]] = {}

    def visit(node: object, parent: str) -> None:
        if not isinstance(node, dict):
            raise ContractError(f"Pinned {label} prefab has a non-object GameObject")
        name = node.get("Name")
        if not isinstance(name, str) or not name:
            raise ContractError(f"Pinned {label} prefab has an unnamed GameObject")
        path = f"{parent}/{name}" if parent else name
        if path in indexed:
            raise ContractError(f"Pinned {label} prefab has duplicate path {path}")
        indexed[path] = node
        children = node.get("Children")
        if not isinstance(children, list):
            raise ContractError(f"Pinned {label} prefab node {path} lacks Children")
        for child in children:
            visit(child, path)

    visit(root, "")
    return indexed


def verify_body_baseline(
    family: str, prefab: Mapping[str, Any], source_pin: FilePin
) -> BodyBaseline:
    node_path = BODY_NODE_PATHS[family]
    nodes = _index_nodes(prefab, family)
    node = nodes.get(node_path)
    if node is None:
        raise ContractError(f"Pinned {family} prefab lacks body node {node_path}")
    transform = _node_transform(node, f"{family}:{node_path}")
    if not _transform_is_identity(transform):
        raise ContractError(
            f"Pinned {family} current-body baseline B is not identity: "
            f"{transform_payload(transform)}"
        )
    components = node.get("Components")
    if not isinstance(components, list):
        raise ContractError(f"Pinned {family} body node has invalid Components")
    renderers = [
        component
        for component in components
        if isinstance(component, dict)
        and component.get("__type") == "Sandbox.ModelRenderer"
    ]
    expected_model = BODY_MODELS[family]
    if len(renderers) != 1 or renderers[0].get("Model") != expected_model:
        models = [renderer.get("Model") for renderer in renderers]
        raise ContractError(
            f"Pinned {family} body renderer mismatch at {node_path}: {models}"
        )
    evidence = {
        "baseline_label": "current-body baseline",
        "measured_and_accepted": False,
        "geometry_derived": False,
        "visually_accepted": False,
        "source_prefab": _pin_payload(source_pin),
        "node_path": node_path,
        "body_model": expected_model,
        "transform_B": transform_payload(transform),
        "basis": (
            "byte-pinned current prefab: the custom common-origin body renderer "
            "is directly on identity weapon_root_children"
        ),
    }
    return BodyBaseline(
        family=family,
        node_path=node_path,
        body_model=expected_model,
        transform=transform,
        evidence=evidence,
    )


def collect_body_baselines() -> tuple[dict[str, bytes], dict[str, BodyBaseline]]:
    payloads: dict[str, bytes] = {}
    baselines: dict[str, BodyBaseline] = {}
    for family, pin in SOURCE_PREFAB_PINS.items():
        data = _verify_file_pin(pin, f"{family} source prefab")
        try:
            prefab = json.loads(data.decode("utf-8-sig"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ContractError(f"Pinned {family} source prefab is invalid JSON") from exc
        if not isinstance(prefab, dict):
            raise ContractError(f"Pinned {family} source prefab root is not an object")
        payloads[family] = data
        baselines[family] = verify_body_baseline(family, prefab, pin)
    return payloads, baselines


def _normalize_quaternion(
    quaternion: tuple[float, float, float, float], label: str
) -> tuple[float, float, float, float]:
    length = math.sqrt(sum(value * value for value in quaternion))
    if length <= 1e-12:
        raise ContractError(f"{label} quaternion is zero")
    normalized = tuple(value / length for value in quaternion)
    if normalized[3] < 0.0:
        normalized = tuple(-value for value in normalized)
    return normalized  # type: ignore[return-value]


def _quat_conjugate(
    quaternion: tuple[float, float, float, float]
) -> tuple[float, float, float, float]:
    x, y, z, w = quaternion
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
    return _normalize_quaternion(result, "composed")


def _quat_rotate(
    quaternion: tuple[float, float, float, float],
    point: tuple[float, float, float],
) -> tuple[float, float, float]:
    x, y, z, w = quaternion
    px, py, pz = point
    tx = 2.0 * (y * pz - z * py)
    ty = 2.0 * (z * px - x * pz)
    tz = 2.0 * (x * py - y * px)
    return (
        px + w * tx + (y * tz - z * ty),
        py + w * ty + (z * tx - x * tz),
        pz + w * tz + (x * ty - y * tx),
    )


def _normalized_transform(transform: Transform, label: str) -> Transform:
    if min(transform.scale) <= 0.0:
        raise ContractError(f"{label} scale must be positive")
    return Transform(
        transform.position,
        _normalize_quaternion(transform.quaternion, label),
        transform.scale,
    )


def compose(parent: Transform, child: Transform) -> Transform:
    parent = _normalized_transform(parent, "parent")
    child = _normalized_transform(child, "child")
    scaled_child = tuple(
        child.position[index] * parent.scale[index] for index in range(3)
    )
    rotated_child = _quat_rotate(parent.quaternion, scaled_child)
    return Transform(
        position=tuple(
            parent.position[index] + rotated_child[index] for index in range(3)
        ),  # type: ignore[arg-type]
        quaternion=_quat_multiply(parent.quaternion, child.quaternion),
        scale=tuple(
            parent.scale[index] * child.scale[index] for index in range(3)
        ),  # type: ignore[arg-type]
    )


def inverse_parent_compose(parent: Transform, desired: Transform) -> Transform:
    """Return local W such that ``compose(parent, W) == desired``."""

    parent = _normalized_transform(parent, "parent P")
    desired = _normalized_transform(desired, "desired B")
    inverse_rotation = _quat_conjugate(parent.quaternion)
    delta = tuple(
        desired.position[index] - parent.position[index] for index in range(3)
    )
    unrotated = _quat_rotate(inverse_rotation, delta)  # type: ignore[arg-type]
    return Transform(
        position=tuple(
            unrotated[index] / parent.scale[index] for index in range(3)
        ),  # type: ignore[arg-type]
        quaternion=_quat_multiply(inverse_rotation, desired.quaternion),
        scale=tuple(
            desired.scale[index] / parent.scale[index] for index in range(3)
        ),  # type: ignore[arg-type]
    )


def _transform_max_delta(left: Transform, right: Transform) -> float:
    left = _normalized_transform(left, "left")
    right = _normalized_transform(right, "right")
    direct_quaternion = max(
        abs(a - b) for a, b in zip(left.quaternion, right.quaternion)
    )
    negated_quaternion = max(
        abs(a + b) for a, b in zip(left.quaternion, right.quaternion)
    )
    return max(
        max(abs(a - b) for a, b in zip(left.position, right.position)),
        min(direct_quaternion, negated_quaternion),
        max(abs(a - b) for a, b in zip(left.scale, right.scale)),
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
        "quaternion": format_vector(transform.quaternion),
        "scale": format_vector(transform.scale),
    }


def parse_transform_payload(payload: Mapping[str, object], label: str) -> Transform:
    expected = {"position", "quaternion", "scale"}
    if set(payload) != expected:
        raise ContractError(
            f"{label} must contain exactly {sorted(expected)}; got {sorted(payload)}"
        )
    return Transform(
        position=_parse_number_vector(
            payload["position"], 3, f"{label}.position"
        ),  # type: ignore[arg-type]
        quaternion=_parse_number_vector(
            payload["quaternion"], 4, f"{label}.quaternion"
        ),  # type: ignore[arg-type]
        scale=_parse_number_vector(
            payload["scale"], 3, f"{label}.scale"
        ),  # type: ignore[arg-type]
    )


def derive_wrapper_manifest(
    records: Mapping[str, BoneRecord],
    baselines: Mapping[str, BodyBaseline],
) -> tuple[dict[str, dict[str, dict[str, str]]], dict[str, Any]]:
    validate_required_records(records)
    families: dict[str, dict[str, dict[str, str]]] = {}
    derivations: dict[str, Any] = {}
    for family, path_to_bone in FAMILY_MOVING_BONES.items():
        baseline = baselines.get(family)
        if baseline is None or not _transform_is_identity(baseline.transform):
            raise ContractError(f"{family} current-body baseline B is not proven identity")
        family_manifest: dict[str, dict[str, str]] = {}
        family_rows: dict[str, Any] = {}
        for path, bone in path_to_bone.items():
            record = records[bone]
            if record.parent != "weapon_root_children":
                raise ContractError(
                    f"Pinned DATA moving bone {bone} parent is {record.parent!r}, "
                    "not weapon_root_children"
                )
            parent = _normalized_transform(record.transform, f"DATA bone {bone}")
            wrapper = inverse_parent_compose(parent, baseline.transform)
            round_trip = compose(parent, wrapper)
            delta = _transform_max_delta(round_trip, baseline.transform)
            if delta > 1e-7:
                raise ContractError(
                    f"{family}:{path} P * W round-trip delta {delta:.12g} exceeds tolerance"
                )
            wrapper_payload = transform_payload(wrapper)
            serialized_wrapper = parse_transform_payload(
                wrapper_payload, f"{family}:{path} serialized wrapper W"
            )
            serialized_round_trip = compose(parent, serialized_wrapper)
            serialized_delta = _transform_max_delta(
                serialized_round_trip, baseline.transform
            )
            if serialized_delta > 1e-7:
                raise ContractError(
                    f"{family}:{path} P * W_serialized round-trip delta "
                    f"{serialized_delta:.12g} exceeds tolerance"
                )
            family_manifest[path] = wrapper_payload
            family_rows[path] = {
                "bone": bone,
                "parent_local_P": transform_payload(parent),
                "current_body_baseline_B": transform_payload(baseline.transform),
                "wrapper_local_W": wrapper_payload,
                "round_trip_P_times_W": transform_payload(round_trip),
                "round_trip_max_delta": delta,
                "serialized_round_trip_P_times_W": transform_payload(
                    serialized_round_trip
                ),
                "serialized_round_trip_max_delta": serialized_delta,
            }
        families[family] = family_manifest
        derivations[family] = family_rows
    return families, derivations


def _load_pinned_wrapper_builder() -> tuple[Any, dict[str, object]]:
    _verify_file_pin(WRAPPER_BUILDER_PIN, "renderer-wrapper builder")
    try:
        module = importlib.import_module("build_viewmodel_renderer_wrappers")
    except ImportError as exc:
        raise ContractError(f"Cannot import pinned renderer-wrapper builder: {exc}") from exc
    module_path = Path(module.__file__).resolve()
    if module_path != WRAPPER_BUILDER_PIN.path.resolve():
        raise ContractError(
            "Imported renderer-wrapper builder path mismatch: "
            f"expected {WRAPPER_BUILDER_PIN.path.resolve()}, got {module_path}"
        )
    return module, _pin_payload(WRAPPER_BUILDER_PIN)


def _validate_wrapper_builder_contract(module: Any) -> None:
    specs = {spec.family: spec for spec in module.PREFAB_SPECS}
    if set(specs) != set(FAMILY_MOVING_BONES):
        raise ContractError("Pinned renderer-wrapper builder family set drifted")
    for family, expected_paths in FAMILY_MOVING_BONES.items():
        spec = specs[family]
        if set(spec.moving_paths) != set(expected_paths):
            raise ContractError(
                f"Pinned renderer-wrapper builder moving-path contract drifted for {family}"
            )
        if Path(spec.default_source).resolve() != SOURCE_PREFAB_PINS[family].path.resolve():
            raise ContractError(
                f"Pinned renderer-wrapper builder source path drifted for {family}"
            )
        if spec.output_name != EXPECTED_CANDIDATE_PINS[family].file:
            raise ContractError(
                f"Pinned renderer-wrapper builder output name drifted for {family}"
            )


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def assert_temp_output_directory(path: Path) -> Path:
    resolved = path.expanduser().resolve(strict=False)
    if _is_within(resolved, REPO_ROOT.resolve()):
        raise ContractError(
            "TEMP-only orchestrator refuses every repository output: " + str(resolved)
        )
    if resolved == SYSTEM_TEMP_ROOT or not _is_within(resolved, SYSTEM_TEMP_ROOT):
        raise ContractError(
            "TEMP-only orchestrator requires a strict child of system TEMP: "
            + str(resolved)
        )
    if resolved.exists():
        if not resolved.is_dir():
            raise ContractError("TEMP-only output path is not a directory: " + str(resolved))
        if any(resolved.iterdir()):
            raise ContractError("TEMP-only output directory must be empty: " + str(resolved))
    return resolved


def _serialize_json(value: object) -> bytes:
    return (
        json.dumps(value, ensure_ascii=True, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def _bind_ar15_additional_renderer_root(
    candidate_path: Path, wrapper_builder: Any
) -> dict[str, object]:
    try:
        prefab = json.loads(candidate_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"AR-15 wrapper candidate is invalid JSON: {exc}") from exc
    if not isinstance(prefab, dict):
        raise ContractError("AR-15 wrapper candidate root is not an object")

    nodes = _index_nodes(prefab, "ar15 wrapper candidate")
    renderer_root = nodes.get(AR15_ADDITIONAL_RENDERER_ROOT_PATH)
    if renderer_root is None:
        raise ContractError("AR-15 wrapper candidate lacks its renderer root")
    if renderer_root.get("__guid") != AR15_ADDITIONAL_RENDERER_ROOT_GUID:
        raise ContractError("AR-15 wrapper candidate renderer-root GUID drifted")

    view_models = [
        component
        for node in nodes.values()
        for component in node.get("Components", [])
        if isinstance(component, dict)
        and component.get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    if len(view_models) != 1:
        raise ContractError(
            f"AR-15 wrapper candidate must contain one ViewModel; got {len(view_models)}"
        )
    renderer_reference = {
        "_type": "gameobject",
        "go": AR15_ADDITIONAL_RENDERER_ROOT_GUID,
    }
    existing_reference = view_models[0].get("AdditionalRendererRoot")
    if existing_reference not in (None, renderer_reference):
        raise ContractError("AR-15 AdditionalRendererRoot has an unexpected owner")
    view_models[0]["AdditionalRendererRoot"] = renderer_reference

    renderer_paths: dict[str, str] = {}
    for path, node in nodes.items():
        for component in node.get("Components", []):
            if not isinstance(component, dict):
                continue
            if component.get("__type") != "Sandbox.ModelRenderer":
                continue
            guid = component.get("__guid")
            if not isinstance(guid, str) or guid in renderer_paths:
                raise ContractError("AR-15 custom ModelRenderer GUIDs are invalid")
            renderer_paths[guid] = path
    if set(renderer_paths) != AR15_CUSTOM_MODEL_RENDERER_GUIDS:
        raise ContractError("AR-15 custom ModelRenderer GUID set drifted")
    descendant_prefix = AR15_ADDITIONAL_RENDERER_ROOT_PATH + "/"
    outside_root = {
        guid: path
        for guid, path in renderer_paths.items()
        if not path.startswith(descendant_prefix)
    }
    if outside_root:
        raise ContractError(
            f"AR-15 custom ModelRenderers escaped AdditionalRendererRoot: {outside_root}"
        )

    data = _serialize_json(prefab)
    try:
        wrapper_builder.pipeline_io.write_bytes_atomic(candidate_path, data)
    except (OSError, RuntimeError) as exc:
        raise ContractError(f"Cannot bind AR-15 AdditionalRendererRoot: {exc}") from exc
    if candidate_path.read_bytes() != data:
        raise ContractError("AR-15 AdditionalRendererRoot bytes changed after promotion")
    return {
        "reference": renderer_reference,
        "root_path": AR15_ADDITIONAL_RENDERER_ROOT_PATH,
        "custom_model_renderer_count": len(renderer_paths),
        "custom_model_renderer_paths": {
            guid: renderer_paths[guid] for guid in sorted(renderer_paths)
        },
    }


def _single_component_by_guid(
    prefab_index: Any, component_guid: str, label: str
) -> dict[str, Any]:
    component = prefab_index.components_by_guid.get(component_guid)
    if not isinstance(component, dict):
        raise ContractError(f"{label} component {component_guid} is missing")
    return component


def _shallow_node_snapshot(prefab_index: Any) -> dict[str, dict[str, Any]]:
    return {
        guid: {
            key: copy.deepcopy(value)
            for key, value in record.node.items()
            if key != "Children"
        }
        for guid, record in prefab_index.nodes_by_guid.items()
    }


def _child_guid_tuple(node: Mapping[str, Any], label: str) -> tuple[str, ...]:
    children = node.get("Children")
    if not isinstance(children, list):
        raise ContractError(f"{label} lacks a Children array")
    guids: list[str] = []
    for child in children:
        if not isinstance(child, dict) or not isinstance(child.get("__guid"), str):
            raise ContractError(f"{label} contains an invalid child")
        guids.append(child["__guid"])
    if len(guids) != len(set(guids)):
        raise ContractError(f"{label} contains duplicate child GUIDs")
    return tuple(guids)


def _bind_sr25_ejection_owner(
    candidate_path: Path,
    wrapper_builder: Any,
    compiled_records: Mapping[str, BoneRecord],
) -> dict[str, object]:
    """Move the SR-25 ejection marker under the animated bolt-flap donor.

    The marker's existing parent-local transform is the desired baseline ``B``.
    Its new local is therefore derived as ``W = inverse(P) * B`` from the exact
    compiled M4 bolt-flap parent-local record.  This is a TEMP-only structural
    candidate; it is not a rendered-placement acceptance claim.
    """

    raw_pin = WRAPPER_CANDIDATE_PINS["sr25"]
    raw_data = _verify_file_pin(
        FilePin(candidate_path, raw_pin.bytes, raw_pin.sha256),
        "sr25 renderer-wrapper intermediate",
    )
    if b"\r\n" in raw_data:
        if b"\n" in raw_data.replace(b"\r\n", b""):
            raise ContractError("SR-25 wrapper candidate has mixed newlines")
        newline = "\r\n"
    else:
        newline = "\n"
    try:
        prefab = json.loads(raw_data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"SR-25 wrapper candidate is invalid JSON: {exc}") from exc
    if not isinstance(prefab, dict):
        raise ContractError("SR-25 wrapper candidate root is not an object")

    native_m4_data = _verify_file_pin(M4_SOURCE_PREFAB_PIN, "native M4 source prefab")
    try:
        native_m4 = json.loads(native_m4_data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Native M4 source prefab is invalid JSON: {exc}") from exc

    try:
        before_index = wrapper_builder.index_prefab(prefab)
        native_m4_index = wrapper_builder.index_prefab(native_m4)
    except wrapper_builder.ContractError as exc:
        raise ContractError(f"Pinned prefab indexing failed: {exc}") from exc

    native_view_model = _single_component_by_guid(
        native_m4_index, M4_VIEW_MODEL_COMPONENT_GUID, "native M4 ViewModel"
    )
    native_owner = native_view_model.get("EjectionPort")
    if native_owner != {"_type": "gameobject", "go": M4_BOLT_FLAP_GUID}:
        raise ContractError("Native M4 EjectionPort no longer points to bolt_flap")
    native_bolt_flap = native_m4_index.nodes_by_guid.get(M4_BOLT_FLAP_GUID)
    if native_bolt_flap is None or native_bolt_flap.node.get("Name") != "bolt_flap":
        raise ContractError("Native M4 bolt_flap ownership node drifted")

    marker_record = before_index.nodes_by_guid.get(SR25_EJECTION_PORT_GUID)
    donor_record = before_index.nodes_by_guid.get(SR25_BOLT_FLAP_GUID)
    parent_record = before_index.nodes_by_guid.get(SR25_WEAPON_ROOT_CHILDREN_GUID)
    if marker_record is None or marker_record.path != SR25_EJECTION_PORT_PATH:
        raise ContractError("SR-25 ejection marker path or GUID drifted")
    if donor_record is None or donor_record.path != SR25_BOLT_FLAP_PATH:
        raise ContractError("SR-25 bolt_flap donor path or GUID drifted")
    if parent_record is None:
        raise ContractError("SR-25 weapon_root_children owner is missing")
    if marker_record.parent_guid != SR25_WEAPON_ROOT_CHILDREN_GUID:
        raise ContractError("SR-25 ejection marker has an unexpected current parent")
    if donor_record.parent_guid != SR25_WEAPON_ROOT_CHILDREN_GUID:
        raise ContractError("SR-25 bolt_flap donor has an unexpected current parent")

    marker = marker_record.node
    donor = donor_record.node
    parent = parent_record.node
    if marker.get("Name") != "sr25_ejection_port":
        raise ContractError("SR-25 ejection marker name drifted")
    if marker.get("Components") != [] or marker.get("Children") != []:
        raise ContractError("SR-25 ejection marker must remain an empty anchor")
    marker_baseline = _node_transform(marker, "SR-25 ejection marker baseline B")
    if marker_baseline != SR25_EJECTION_PORT_BASELINE:
        raise ContractError("SR-25 ejection marker baseline transform drifted")

    view_model = _single_component_by_guid(
        before_index, SR25_VIEW_MODEL_COMPONENT_GUID, "SR-25 ViewModel"
    )
    ejection_reference = {"_type": "gameobject", "go": SR25_EJECTION_PORT_GUID}
    if view_model.get("EjectionPort") != ejection_reference:
        raise ContractError("SR-25 ViewModel EjectionPort reference drifted")

    parent_children_before = _child_guid_tuple(parent, "SR-25 weapon_root_children")
    donor_children_before = _child_guid_tuple(donor, "SR-25 bolt_flap")
    if parent_children_before.count(SR25_EJECTION_PORT_GUID) != 1:
        raise ContractError("SR-25 ejection marker is not a unique direct child")
    if donor_children_before != (SR25_BOLT_FLAP_WRAPPER_GUID,):
        raise ContractError("SR-25 bolt_flap wrapper-child contract drifted")

    bolt_flap_record = compiled_records.get("bolt_flap")
    if bolt_flap_record is None or bolt_flap_record.parent != "weapon_root_children":
        raise ContractError("Pinned compiled bolt_flap parent record drifted")
    parent_transform = _normalized_transform(
        bolt_flap_record.transform, "compiled M4 bolt_flap P"
    )
    marker_local = inverse_parent_compose(parent_transform, marker_baseline)
    serialized_marker_local = parse_transform_payload(
        transform_payload(marker_local), "SR-25 serialized ejection marker W"
    )
    round_trip = compose(parent_transform, serialized_marker_local)
    round_trip_delta = _transform_max_delta(round_trip, marker_baseline)
    if round_trip_delta > 1e-7:
        raise ContractError(
            "SR-25 ejection marker P * W round-trip delta "
            f"{round_trip_delta:.12g} exceeds tolerance"
        )

    before_guids = before_index.all_guids
    before_component_owners = dict(before_index.component_owners)
    before_shallow = _shallow_node_snapshot(before_index)
    marker_snapshot = copy.deepcopy(marker)

    parent["Children"] = [
        child
        for child in parent["Children"]
        if child.get("__guid") != SR25_EJECTION_PORT_GUID
    ]
    marker["Position"] = format_vector(serialized_marker_local.position)
    marker["Rotation"] = format_vector(serialized_marker_local.quaternion)
    marker["Scale"] = format_vector(serialized_marker_local.scale)
    donor["Children"] = list(donor["Children"]) + [marker]

    try:
        after_index = wrapper_builder.index_prefab(prefab)
    except wrapper_builder.ContractError as exc:
        raise ContractError(f"SR-25 ejection reparent validation failed: {exc}") from exc
    if after_index.all_guids != before_guids:
        raise ContractError("SR-25 ejection reparent changed the GUID set")
    if after_index.component_owners != before_component_owners:
        raise ContractError("SR-25 ejection reparent changed component ownership")
    after_marker = after_index.nodes_by_guid[SR25_EJECTION_PORT_GUID]
    if after_marker.parent_guid != SR25_BOLT_FLAP_GUID:
        raise ContractError("SR-25 ejection marker did not move under bolt_flap")
    if after_marker.path != SR25_BOLT_FLAP_PATH + "/sr25_ejection_port":
        raise ContractError("SR-25 ejection marker final path drifted")
    if view_model.get("EjectionPort") != ejection_reference:
        raise ContractError("SR-25 ViewModel EjectionPort reference changed")

    after_parent = after_index.nodes_by_guid[SR25_WEAPON_ROOT_CHILDREN_GUID].node
    after_donor = after_index.nodes_by_guid[SR25_BOLT_FLAP_GUID].node
    expected_parent_children = tuple(
        guid for guid in parent_children_before if guid != SR25_EJECTION_PORT_GUID
    )
    if _child_guid_tuple(after_parent, "final SR-25 weapon_root_children") != expected_parent_children:
        raise ContractError("SR-25 weapon_root_children changed beyond marker removal")
    if _child_guid_tuple(after_donor, "final SR-25 bolt_flap") != (
        *donor_children_before,
        SR25_EJECTION_PORT_GUID,
    ):
        raise ContractError("SR-25 bolt_flap changed beyond marker append")

    after_shallow = _shallow_node_snapshot(after_index)
    expected_marker = copy.deepcopy(marker_snapshot)
    expected_marker["Position"] = format_vector(serialized_marker_local.position)
    expected_marker["Rotation"] = format_vector(serialized_marker_local.quaternion)
    expected_marker["Scale"] = format_vector(serialized_marker_local.scale)
    expected_marker.pop("Children", None)
    for guid, before_node in before_shallow.items():
        expected_node = expected_marker if guid == SR25_EJECTION_PORT_GUID else before_node
        if after_shallow.get(guid) != expected_node:
            raise ContractError(
                f"SR-25 ejection reparent changed unexpected node fields at {guid}"
            )

    serializer = getattr(wrapper_builder, "_serialize_prefab", None)
    if not callable(serializer):
        raise ContractError("Pinned renderer-wrapper serializer is unavailable")
    data = serializer(prefab, newline)
    try:
        wrapper_builder.pipeline_io.write_bytes_atomic(candidate_path, data)
    except (OSError, RuntimeError) as exc:
        raise ContractError(f"Cannot bind SR-25 ejection owner: {exc}") from exc
    if candidate_path.read_bytes() != data:
        raise ContractError("SR-25 ejection-owner bytes changed after promotion")
    if (
        _verify_file_pin(M4_SOURCE_PREFAB_PIN, "native M4 source prefab after build")
        != native_m4_data
    ):
        raise ContractError("Native M4 source prefab changed during the TEMP build")
    return {
        "native_m4_source_prefab": _pin_payload(M4_SOURCE_PREFAB_PIN),
        "native_m4_ejection_owner_guid": M4_BOLT_FLAP_GUID,
        "marker_guid": SR25_EJECTION_PORT_GUID,
        "marker_reference": ejection_reference,
        "old_parent_guid": SR25_WEAPON_ROOT_CHILDREN_GUID,
        "new_parent_guid": SR25_BOLT_FLAP_GUID,
        "compiled_parent_local_P": transform_payload(parent_transform),
        "marker_baseline_B": transform_payload(marker_baseline),
        "marker_local_W": transform_payload(serialized_marker_local),
        "serialized_round_trip_P_times_W": transform_payload(round_trip),
        "serialized_round_trip_max_delta": round_trip_delta,
        "guid_set_preserved": True,
        "component_owners_preserved": True,
        "reference_preserved": True,
        "native_m4_source_unchanged": True,
        "renderer_wrapper_intermediate_original_hierarchy_preserved": True,
        "final_candidate_original_hierarchy_preserved": False,
    }


def _runtime_manifest(
    output_directory: Path,
    families: Mapping[str, Mapping[str, Mapping[str, str]]],
) -> dict[str, Any]:
    return {
        "version": 1,
        "measured_and_accepted": False,
        "pins": {
            family: {
                "source_path": str(SOURCE_PREFAB_PINS[family].path.resolve()),
                "source_sha256": SOURCE_PREFAB_PINS[family].sha256,
                "output_path": str(
                    (output_directory / EXPECTED_CANDIDATE_PINS[family].file).resolve()
                ),
                "output_sha256": WRAPPER_CANDIDATE_PINS[family].sha256,
            }
            for family in FAMILY_MOVING_BONES
        },
        "families": families,
    }


def _normalized_builder_report(report: Mapping[str, Any]) -> dict[str, Any]:
    fields = (
        "family",
        "renderers_moved",
        "new_wrapper_guids",
        "reference_owner_updates",
        "existing_guid_count",
        "candidate_guid_count",
        "original_hierarchy_preserved",
        "donor_transforms_preserved",
        "renderer_components_preserved",
        "references_valid",
        "scope_nodes_preserved",
        "view_model_contract",
        "identity_moving_parts",
        "wrappers",
        "manifest_mode",
        "measured_and_accepted",
        "transform_semantics",
    )
    missing = [field for field in fields if field not in report]
    if missing:
        raise ContractError(f"Pinned renderer-wrapper report lacks fields: {missing}")
    if report["manifest_mode"] != "external":
        raise ContractError("Pinned renderer-wrapper builder did not use external manifest")
    if report["measured_and_accepted"] is not False:
        raise ContractError("Pinned renderer-wrapper builder reported accepted transforms")
    if report["identity_moving_parts"]:
        raise ContractError(
            "Pinned renderer-wrapper builder left identity moving wrappers: "
            f"{report['identity_moving_parts']}"
        )
    return {field: report[field] for field in fields}


def build_temp_candidates(output_directory: Path | None = None) -> dict[str, Any]:
    sample = collect_compiled_bind_sample()
    source_payloads, baselines = collect_body_baselines()
    families, derivations = derive_wrapper_manifest(sample.records, baselines)
    wrapper_builder, wrapper_builder_evidence = _load_pinned_wrapper_builder()
    _validate_wrapper_builder_contract(wrapper_builder)

    if output_directory is None:
        output_directory = Path(tempfile.mkdtemp(prefix="dxrp_m4_body_wrappers_"))
    output_directory = assert_temp_output_directory(output_directory)
    output_paths = [
        output_directory / pin.file for pin in EXPECTED_CANDIDATE_PINS.values()
    ] + [output_directory / REPORT_FILE]
    try:
        wrapper_builder.assert_output_paths_allowed(output_paths)
    except wrapper_builder.ContractError as exc:
        raise ContractError(str(exc)) from exc

    runtime_manifest = _runtime_manifest(output_directory, families)
    with tempfile.TemporaryDirectory(prefix="dxrp_m4_wrapper_manifest_") as directory:
        manifest_path = Path(directory) / "m4-current-body-wrapper-manifest.json"
        manifest_path.write_bytes(_serialize_json(runtime_manifest))
        try:
            raw_reports = wrapper_builder.build_candidates(
                output_directory=output_directory,
                source_paths={
                    family: pin.path for family, pin in SOURCE_PREFAB_PINS.items()
                },
                transform_manifest_path=manifest_path,
            )
        except (wrapper_builder.ContractError, OSError, RuntimeError) as exc:
            raise ContractError(f"Pinned renderer-wrapper build failed: {exc}") from exc

    reports_by_family = {report["family"]: report for report in raw_reports}
    if set(reports_by_family) != set(FAMILY_MOVING_BONES):
        raise ContractError("Pinned renderer-wrapper builder returned wrong family reports")

    ar15_renderer_root = _bind_ar15_additional_renderer_root(
        output_directory / EXPECTED_CANDIDATE_PINS["ar15"].file,
        wrapper_builder,
    )
    sr25_ejection_owner = _bind_sr25_ejection_owner(
        output_directory / EXPECTED_CANDIDATE_PINS["sr25"].file,
        wrapper_builder,
        sample.records,
    )

    outputs: dict[str, Any] = {}
    normalized_reports: dict[str, Any] = {}
    for family, expected in EXPECTED_CANDIDATE_PINS.items():
        output_pin = FilePin(
            output_directory / expected.file,
            expected.bytes,
            expected.sha256,
        )
        output_data = _verify_file_pin(output_pin, f"{family} TEMP candidate")
        raw_report = reports_by_family[family]
        if raw_report.get("source_sha256") != SOURCE_PREFAB_PINS[family].sha256:
            raise ContractError(f"Pinned renderer-wrapper source report drifted for {family}")
        if raw_report.get("output_sha256") != WRAPPER_CANDIDATE_PINS[family].sha256:
            raise ContractError(f"Pinned renderer-wrapper output report drifted for {family}")
        outputs[family] = {
            "file": expected.file,
            "bytes": len(output_data),
            "sha256": _sha256(output_data),
        }
        normalized_reports[family] = _normalized_builder_report(raw_report)

    report = {
        "version": 1,
        "mode": "TEMP_ONLY_NO_PRODUCT_WRITE_MODE",
        "game_tree_written": False,
        "sample_kind": "compiled_model_bind_plus_current_body_baseline",
        "measured_and_accepted": False,
        "compiled_bind_evidence": sample.evidence,
        "current_body_assemblies_B": {
            family: baseline.evidence for family, baseline in baselines.items()
        },
        "transform_law": "W = inverse(P) * B; P * W = B",
        "wrapper_manifest": {
            "version": 1,
            "measured_and_accepted": False,
            "families": families,
            "pins": {
                family: {
                    "source_prefab": _pin_payload(SOURCE_PREFAB_PINS[family]),
                    "output": {
                        "file": EXPECTED_CANDIDATE_PINS[family].file,
                        "bytes": EXPECTED_CANDIDATE_PINS[family].bytes,
                        "sha256": EXPECTED_CANDIDATE_PINS[family].sha256,
                    },
                }
                for family in FAMILY_MOVING_BONES
            },
        },
        "derivations": derivations,
        "wrapper_builder": wrapper_builder_evidence,
        "wrapper_builder_reports": normalized_reports,
        "ar15_additional_renderer_root": ar15_renderer_root,
        "sr25_ejection_owner": sr25_ejection_owner,
        "outputs": outputs,
        "source_inputs_unchanged": all(
            source_payloads[family] == SOURCE_PREFAB_PINS[family].path.read_bytes()
            for family in SOURCE_PREFAB_PINS
        ),
        "proof_ceiling": [
            "byte custody for parser, compiled M4 model, wrapper builder, and source prefabs",
            "compiled M4 skeleton parent-local DATA records for the required nodes",
            "byte-pinned current body renderer identity baselines",
            "static W = inverse(P) * B composition and P * W round-trip",
            "TEMP candidate serialization and wrapper structural validation",
            "SR-25 ejection marker ownership derived from native M4 and compiled bolt_flap bind",
            "compiled skeleton bind is not an observed IdlePose sample",
            "current-body baseline is not geometry-derived or visually accepted",
            "UNVERIFIED asset compile",
            "UNVERIFIED code compile",
            "UNVERIFIED editor/runtime animation behavior",
            "UNVERIFIED visual hand fit, ADS, muzzle, and ejection placement",
            "NO product-tree write",
        ],
    }
    if report["source_inputs_unchanged"] is not True:
        raise ContractError("A pinned source prefab changed during the TEMP build")
    report_bytes = _serialize_json(report)
    report_path = output_directory / REPORT_FILE
    try:
        wrapper_builder.pipeline_io.write_bytes_atomic(report_path, report_bytes)
    except (OSError, RuntimeError) as exc:
        raise ContractError(f"Cannot write TEMP evidence report: {exc}") from exc
    if report_path.read_bytes() != report_bytes:
        raise ContractError("TEMP evidence report bytes changed after promotion")

    return {
        "output_directory": str(output_directory),
        "files": {
            **{
                family: expected.file
                for family, expected in EXPECTED_CANDIDATE_PINS.items()
            },
            "report": REPORT_FILE,
        },
        "report": report,
        "report_pin": {
            "file": REPORT_FILE,
            "bytes": len(report_bytes),
            "sha256": _sha256(report_bytes),
        },
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build byte-pinned M4/current-body AR-15 and SR-25 renderer-wrapper "
            "candidates below system TEMP only. There is no product-write mode."
        )
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Empty strict child of system TEMP; default is a new TEMP directory.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    try:
        result = build_temp_candidates(args.output_dir)
    except (ContractError, OSError) as exc:
        print(f"DXRP_M4_BODY_WRAPPER_ERROR: {exc}", file=sys.stderr)
        return 2
    print(
        "DXRP_M4_BODY_WRAPPER="
        + json.dumps(
            {
                "output_directory": result["output_directory"],
                "files": result["files"],
                "mode": result["report"]["mode"],
                "game_tree_written": False,
                "measured_and_accepted": False,
                "report_pin": result["report_pin"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
