#!/usr/bin/env python3
"""Prove selected stock weapon-donor animation channels from pinned bytes.

This tool is deliberately read-only with respect to the repository, game tree,
editor, and Portal.  It pins Source2Viewer and the four compiled donor models,
exports only the named animations to a fresh strict child of the operating-
system TEMP directory, parses the resulting glTF accessors, and removes the
temporary export before printing its report.  It has no product-output mode.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence


class ContractError(RuntimeError):
    """Raised when custody, TEMP containment, or motion evidence drifts."""


SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve(strict=True)
TEMP_PREFIX = "dxrp_weapon_donor_animation_audit_"
PARSER_VERSION = "19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa"
TRANSLATION_EPSILON = 1.0e-4
ROTATION_EPSILON_DEGREES = 1.0e-3


@dataclass(frozen=True)
class FilePin:
    path: Path
    bytes: int
    sha256: str


@dataclass(frozen=True)
class ChannelRequirement:
    animation: str
    node: str
    path: str
    motion: str


@dataclass(frozen=True)
class DonorSpec:
    key: str
    model: FilePin
    animations: tuple[str, ...]
    requirements: tuple[ChannelRequirement, ...]
    inventory_nodes: tuple[str, ...] = ()
    allow_known_physics_sidecar_exception: bool = False


PARSER_PIN = FilePin(
    Path(r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe"),
    108_603_232,
    "36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0",
)

MP5_FIRE_DELTAS = tuple(f"Fire_{index:02d}_delta" for index in range(1, 10))
USP_RELOADS = (
    "1H_Reload",
    "1H_Reload_Empty",
    "2H_Reload",
    "2H_Reload_Empty",
)
USP_FIRES = (
    "1H_Fire_01",
    "1H_Fire_02",
    "2H_Fire_01",
    "2H_Fire_02",
    "Fire_GoesEmpty",
)

DONORS: tuple[DonorSpec, ...] = (
    DonorSpec(
        key="m4",
        model=FilePin(
            Path(
                r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
                r"\sbox_assault_m4a1\v_m4a1.3912fd337e9d3488.vmdl_c"
            ),
            2_019_792,
            "8726559C336098469AAA7C9A9A5018FE4825D3C6375EF406AB849C3EC82AABE4",
        ),
        animations=(
            "Deploy",
            "Reload_Throw",
            "Reload_Pull",
            "Reload_Empty",
            "Trigger_Fire_delta",
            "Fire_Hold_delta",
        ),
        requirements=(
            ChannelRequirement(
                "Reload_Throw", "magazine", "translation", "translation"
            ),
            ChannelRequirement(
                "Reload_Pull", "magazine", "translation", "translation"
            ),
            ChannelRequirement(
                "Reload_Empty", "magazine", "translation", "translation"
            ),
            ChannelRequirement("Reload_Empty", "bolt", "translation", "translation"),
            ChannelRequirement(
                "Trigger_Fire_delta", "bolt", "translation", "translation"
            ),
            ChannelRequirement(
                "Trigger_Fire_delta", "trigger", "rotation", "rotation"
            ),
        ),
        inventory_nodes=(
            "magazine",
            "bolt",
            "bolt_flap",
            "charging_handle",
            "trigger",
            "mode_selector",
            "stock",
        ),
    ),
    DonorSpec(
        key="mp5",
        model=FilePin(
            Path(
                r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
                r"\sbox_smg_mp5\v_mp5.bb3ccbb95b323f14.vmdl_c"
            ),
            1_602_903,
            "87B8E0757BB7AE6B8B784A261449DABCD9A37125A0AC8B420C115244F65B4F24",
        ),
        animations=("Reload", "Reload_Empty", *MP5_FIRE_DELTAS),
        requirements=(
            ChannelRequirement("Reload", "magazine", "translation", "translation"),
            ChannelRequirement(
                "Reload_Empty", "magazine", "translation", "translation"
            ),
            *(
                ChannelRequirement(name, "bolt", "translation", "translation")
                for name in MP5_FIRE_DELTAS
            ),
        ),
    ),
    DonorSpec(
        key="usp",
        model=FilePin(
            Path(
                r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
                r"\sbox_pistol_usp\v_usp.c9a4333387e69880.vmdl_c"
            ),
            1_394_438,
            "439F8B2B676F310318EC4080D577DC16EA3549249564BADDDCD0B0CDE4A520F5",
        ),
        animations=(*USP_RELOADS, *USP_FIRES),
        requirements=(
            *(
                ChannelRequirement(name, "magazine", "translation", "translation")
                for name in USP_RELOADS
            ),
            ChannelRequirement(
                "1H_Reload_Empty", "slide", "translation", "translation"
            ),
            ChannelRequirement(
                "2H_Reload_Empty", "slide", "translation", "translation"
            ),
            *(
                ChannelRequirement(name, "slide", "translation", "translation")
                for name in USP_FIRES
            ),
        ),
        # Source2Viewer 19.2 writes the valid requested glTF, then reports a
        # known failure while attempting the unrelated USP physics sidecar.
        # Only that exact diagnostic is tolerated; all other exceptions fail.
        allow_known_physics_sidecar_exception=True,
    ),
    DonorSpec(
        key="spaghelli",
        model=FilePin(
            Path(
                r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
                r"\sbox_shotgun_spaghellim4"
                r"\v_spaghellim4.60dd375c241f403a.vmdl_c"
            ),
            1_176_409,
            "F3FA60FA7D9F3DB7D544D66925F91641B5BA488A41C658FF811D5013D5F6B7BE",
        ),
        animations=("Reload_FirstShell", "Reload_Shell"),
        requirements=(
            ChannelRequirement(
                "Reload_FirstShell", "bolt", "translation", "translation"
            ),
            ChannelRequirement("Reload_Shell", "carrier", "rotation", "rotation"),
        ),
    ),
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def _verify_pin(pin: FilePin, label: str) -> dict[str, Any]:
    try:
        resolved = pin.path.resolve(strict=True)
    except OSError as exc:
        raise ContractError(f"{label} is unavailable: {pin.path}") from exc
    stat = resolved.stat()
    if not resolved.is_file():
        raise ContractError(f"{label} is not a file: {resolved}")
    if stat.st_size != pin.bytes:
        raise ContractError(
            f"{label} byte drift: expected {pin.bytes}, observed {stat.st_size}"
        )
    observed_sha = _sha256(resolved)
    if observed_sha != pin.sha256:
        raise ContractError(
            f"{label} SHA-256 drift: expected {pin.sha256}, observed {observed_sha}"
        )
    return {"path": str(resolved), "bytes": stat.st_size, "sha256": observed_sha}


def _strict_child(root: Path, path: Path, label: str, *, must_exist: bool) -> Path:
    try:
        root_resolved = root.resolve(strict=True)
        path_resolved = path.resolve(strict=must_exist)
    except OSError as exc:
        raise ContractError(f"{label} cannot be resolved: {path}") from exc
    if root_resolved != SYSTEM_TEMP_ROOT and SYSTEM_TEMP_ROOT not in root_resolved.parents:
        raise ContractError(f"TEMP root escaped system TEMP: {root_resolved}")
    if path_resolved == root_resolved or root_resolved not in path_resolved.parents:
        raise ContractError(f"{label} is not a strict TEMP child: {path_resolved}")
    return path_resolved


def _directory_snapshot(path: Path) -> tuple[tuple[str, bool, int, int], ...]:
    rows: list[tuple[str, bool, int, int]] = []
    for child in path.iterdir():
        stat = child.stat()
        rows.append((child.name, child.is_file(), stat.st_size, stat.st_mtime_ns))
    return tuple(sorted(rows))


def _verify_parser_version() -> str:
    result = subprocess.run(
        [str(PARSER_PIN.path), "--version"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="strict",
        timeout=30,
        check=False,
    )
    if result.returncode != 0:
        raise ContractError(f"Source2Viewer --version failed: exit {result.returncode}")
    first_line = result.stdout.splitlines()[0] if result.stdout.splitlines() else ""
    expected = f"Version: {PARSER_VERSION}"
    if first_line != expected:
        raise ContractError(
            f"Source2Viewer version drift: expected {expected!r}, observed {first_line!r}"
        )
    if result.stderr:
        raise ContractError("Source2Viewer --version emitted stderr")
    return PARSER_VERSION


def _validate_export_diagnostics(spec: DonorSpec, result: subprocess.CompletedProcess[str]) -> bool:
    if result.returncode != 0:
        raise ContractError(f"{spec.key} export failed: exit {result.returncode}")
    diagnostics = "\n".join(part for part in (result.stdout, result.stderr) if part)
    exception_lines = [
        line.strip() for line in diagnostics.splitlines() if "Exception" in line
    ]
    if not exception_lines:
        return False
    expected = (
        spec.allow_known_physics_sidecar_exception
        and exception_lines
        == [
            "System.NullReferenceException: Object reference not set to an instance of an object."
        ]
        and "GltfModelExporter.LoadPhysicsMeshes" in diagnostics
        and "GltfModelExporter.ExportToFile" in diagnostics
    )
    if not expected:
        raise ContractError(f"{spec.key} export emitted an unapproved exception")
    return True


def _export_donor(spec: DonorSpec, output_root: Path) -> tuple[Path, dict[str, Any]]:
    donor_dir = _strict_child(
        output_root, output_root / spec.key, f"{spec.key} export directory", must_exist=False
    )
    donor_dir.mkdir()
    donor_dir = _strict_child(
        output_root, donor_dir, f"{spec.key} export directory", must_exist=True
    )
    output_path = _strict_child(
        output_root,
        donor_dir / f"{spec.key}.gltf",
        f"{spec.key} glTF output",
        must_exist=False,
    )
    source_parent = spec.model.path.resolve(strict=True).parent
    before = _directory_snapshot(source_parent)
    command = [
        str(PARSER_PIN.path),
        "-i",
        str(spec.model.path),
        "-o",
        str(output_path),
        "-d",
        "--gltf_export_format",
        "gltf",
        "--gltf_export_animations",
        "--gltf_animation_list",
        ",".join(spec.animations),
    ]
    result = subprocess.run(
        command,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="strict",
        timeout=120,
        check=False,
    )
    known_physics_exception = _validate_export_diagnostics(spec, result)
    after = _directory_snapshot(source_parent)
    if after != before:
        raise ContractError(f"{spec.key} donor directory changed during export")
    output_path = _strict_child(
        output_root, output_path, f"{spec.key} glTF output", must_exist=True
    )
    artifact_pins: list[dict[str, Any]] = []
    for artifact in sorted(donor_dir.iterdir(), key=lambda item: item.name):
        artifact = _strict_child(
            output_root, artifact, f"{spec.key} generated artifact", must_exist=True
        )
        if not artifact.is_file():
            raise ContractError(f"{spec.key} generated a non-file artifact: {artifact}")
        artifact_pins.append(
            {
                "name": artifact.name,
                "bytes": artifact.stat().st_size,
                "sha256": _sha256(artifact),
            }
        )
    return output_path, {
        "animations_requested": list(spec.animations),
        "generated_artifacts": artifact_pins,
        "known_usp_physics_sidecar_exception": known_physics_exception,
        "source_directory_unchanged": True,
    }


def _require_list(document: Mapping[str, Any], key: str) -> list[Any]:
    value = document.get(key)
    if not isinstance(value, list):
        raise ContractError(f"glTF {key!r} is not a list")
    return value


def _index(items: Sequence[Any], index: Any, label: str) -> Mapping[str, Any]:
    if not isinstance(index, int) or isinstance(index, bool):
        raise ContractError(f"{label} index is not an integer: {index!r}")
    if index < 0 or index >= len(items) or not isinstance(items[index], Mapping):
        raise ContractError(f"{label} index is out of range: {index}")
    return items[index]


def _read_accessor(
    document: Mapping[str, Any], accessor_index: int, gltf_path: Path, output_root: Path
) -> tuple[tuple[float, ...], ...]:
    accessors = _require_list(document, "accessors")
    views = _require_list(document, "bufferViews")
    buffers = _require_list(document, "buffers")
    accessor = _index(accessors, accessor_index, "accessor")
    if accessor.get("sparse") is not None:
        raise ContractError("sparse glTF accessors are outside this narrow audit")
    if accessor.get("componentType") != 5126:
        raise ContractError("animation accessor is not FLOAT")
    component_counts = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}
    accessor_type = accessor.get("type")
    if accessor_type not in component_counts:
        raise ContractError(f"unsupported animation accessor type: {accessor_type!r}")
    count = accessor.get("count")
    if not isinstance(count, int) or isinstance(count, bool) or count < 1:
        raise ContractError(f"invalid animation accessor count: {count!r}")
    view = _index(views, accessor.get("bufferView"), "bufferView")
    buffer = _index(buffers, view.get("buffer"), "buffer")
    uri = buffer.get("uri")
    if not isinstance(uri, str) or not uri or uri.startswith("data:"):
        raise ContractError("animation buffer URI must be a nonempty external file")
    uri_path = Path(uri)
    if uri_path.is_absolute() or ".." in uri_path.parts:
        raise ContractError(f"animation buffer URI escapes its glTF directory: {uri!r}")
    buffer_path = _strict_child(
        output_root,
        gltf_path.parent / uri_path,
        "glTF animation buffer",
        must_exist=True,
    )
    component_count = component_counts[accessor_type]
    element_size = component_count * 4
    stride = view.get("byteStride", element_size)
    if not isinstance(stride, int) or stride < element_size or stride % 4:
        raise ContractError(f"invalid animation accessor byte stride: {stride!r}")
    view_offset = view.get("byteOffset", 0)
    accessor_offset = accessor.get("byteOffset", 0)
    view_length = view.get("byteLength")
    declared_buffer_length = buffer.get("byteLength")
    if not all(
        isinstance(value, int) and not isinstance(value, bool) and value >= 0
        for value in (view_offset, accessor_offset, view_length, declared_buffer_length)
    ):
        raise ContractError("invalid glTF byte offset or length")
    required_in_view = accessor_offset + (count - 1) * stride + element_size
    if required_in_view > view_length:
        raise ContractError("animation accessor exceeds its bufferView")
    payload = buffer_path.read_bytes()
    if len(payload) != declared_buffer_length:
        raise ContractError("animation buffer byte length differs from glTF declaration")
    start = view_offset + accessor_offset
    if start + (count - 1) * stride + element_size > len(payload):
        raise ContractError("animation accessor exceeds its buffer")
    rows: list[tuple[float, ...]] = []
    for sample in range(count):
        offset = start + sample * stride
        values = struct.unpack_from(f"<{component_count}f", payload, offset)
        if not all(math.isfinite(value) for value in values):
            raise ContractError("animation accessor contains a nonfinite value")
        rows.append(tuple(float(value) for value in values))
    return tuple(rows)


def _channel_samples(
    document: Mapping[str, Any],
    gltf_path: Path,
    output_root: Path,
    requirement: ChannelRequirement,
) -> tuple[tuple[float, ...], ...]:
    nodes = _require_list(document, "nodes")
    node_indices = [
        index
        for index, node in enumerate(nodes)
        if isinstance(node, Mapping) and node.get("name") == requirement.node
    ]
    if len(node_indices) != 1:
        raise ContractError(
            f"expected exactly one node {requirement.node!r}, observed {len(node_indices)}"
        )
    animations = _require_list(document, "animations")
    matches = [
        animation
        for animation in animations
        if isinstance(animation, Mapping) and animation.get("name") == requirement.animation
    ]
    if len(matches) != 1:
        raise ContractError(
            f"expected exactly one animation {requirement.animation!r}, observed {len(matches)}"
        )
    animation = matches[0]
    channels = animation.get("channels")
    samplers = animation.get("samplers")
    if not isinstance(channels, list) or not isinstance(samplers, list):
        raise ContractError(f"animation {requirement.animation!r} is malformed")
    channel_matches: list[Mapping[str, Any]] = []
    for channel in channels:
        if not isinstance(channel, Mapping) or not isinstance(channel.get("target"), Mapping):
            raise ContractError(f"animation {requirement.animation!r} has a malformed channel")
        target = channel["target"]
        if target.get("node") == node_indices[0] and target.get("path") == requirement.path:
            channel_matches.append(channel)
    if len(channel_matches) != 1:
        raise ContractError(
            f"expected one {requirement.animation}/{requirement.node}/{requirement.path} "
            f"channel, observed {len(channel_matches)}"
        )
    sampler = _index(samplers, channel_matches[0].get("sampler"), "animation sampler")
    interpolation = sampler.get("interpolation", "LINEAR")
    if interpolation not in ("LINEAR", "STEP"):
        raise ContractError(f"unsupported animation interpolation: {interpolation!r}")
    times = _read_accessor(document, sampler.get("input"), gltf_path, output_root)
    values = _read_accessor(document, sampler.get("output"), gltf_path, output_root)
    if len(times) != len(values):
        raise ContractError("animation input/output sample counts differ")
    time_values = [row[0] for row in times]
    if any(right < left for left, right in zip(time_values, time_values[1:])):
        raise ContractError("animation sample times are not monotonic")
    return values


def _translation_displacement(samples: Sequence[Sequence[float]]) -> float:
    if not samples or any(len(sample) != 3 for sample in samples):
        raise ContractError("translation channel is not a nonempty VEC3 stream")
    origin = samples[0]
    return max(math.dist(origin, sample) for sample in samples)


def _rotation_displacement_degrees(samples: Sequence[Sequence[float]]) -> float:
    if not samples or any(len(sample) != 4 for sample in samples):
        raise ContractError("rotation channel is not a nonempty VEC4 stream")

    def normalize(sample: Sequence[float]) -> tuple[float, float, float, float]:
        length = math.sqrt(sum(value * value for value in sample))
        if length <= 1.0e-12:
            raise ContractError("rotation channel contains a zero quaternion")
        return tuple(value / length for value in sample)  # type: ignore[return-value]

    origin = normalize(samples[0])
    peak = 0.0
    for sample in samples:
        current = normalize(sample)
        dot = abs(sum(left * right for left, right in zip(origin, current)))
        angle = math.degrees(2.0 * math.acos(max(-1.0, min(1.0, dot))))
        peak = max(peak, angle)
    return peak


def _prove_requirement(
    document: Mapping[str, Any],
    gltf_path: Path,
    output_root: Path,
    requirement: ChannelRequirement,
) -> dict[str, Any]:
    samples = _channel_samples(document, gltf_path, output_root, requirement)
    if requirement.motion == "translation":
        displacement = _translation_displacement(samples)
        if displacement <= TRANSLATION_EPSILON:
            raise ContractError(
                f"{requirement.animation}/{requirement.node} translation is constant"
            )
        metric = "peak_displacement"
        threshold = TRANSLATION_EPSILON
    elif requirement.motion == "rotation":
        displacement = _rotation_displacement_degrees(samples)
        if displacement <= ROTATION_EPSILON_DEGREES:
            raise ContractError(
                f"{requirement.animation}/{requirement.node} rotation is constant"
            )
        metric = "peak_rotation_degrees"
        threshold = ROTATION_EPSILON_DEGREES
    else:
        raise ContractError(f"unknown motion proof type: {requirement.motion!r}")
    return {
        "animation": requirement.animation,
        "node": requirement.node,
        "channel": requirement.path,
        "samples": len(samples),
        "metric": metric,
        "observed": displacement,
        "required_greater_than": threshold,
        "nonconstant": True,
    }


def _observe_requirement(
    document: Mapping[str, Any],
    gltf_path: Path,
    output_root: Path,
    requirement: ChannelRequirement,
) -> dict[str, Any]:
    samples = _channel_samples(document, gltf_path, output_root, requirement)
    if requirement.path == "translation":
        observed = _translation_displacement(samples)
        metric = "peak_displacement"
        threshold = TRANSLATION_EPSILON
    elif requirement.path == "rotation":
        observed = _rotation_displacement_degrees(samples)
        metric = "peak_rotation_degrees"
        threshold = ROTATION_EPSILON_DEGREES
    else:
        raise ContractError(f"unsupported observed channel: {requirement.path!r}")
    return {
        "animation": requirement.animation,
        "node": requirement.node,
        "channel": requirement.path,
        "samples": len(samples),
        "metric": metric,
        "observed": observed,
        "nonconstant": observed > threshold,
    }


def _normalized_node_name(name: str) -> str:
    return "".join(character for character in name.casefold() if character.isalnum())


def _prove_no_spaghelli_action_node(document: Mapping[str, Any]) -> dict[str, Any]:
    nodes = _require_list(document, "nodes")
    names = [
        node.get("name")
        for node in nodes
        if isinstance(node, Mapping) and isinstance(node.get("name"), str)
    ]
    forbidden_tokens = ("pump", "foreend", "forend", "action")
    matches = [
        name
        for name in names
        if any(token in _normalized_node_name(name) for token in forbidden_tokens)
    ]
    if matches:
        raise ContractError(f"Spaghelli exposes a dedicated action node: {matches!r}")
    return {
        "node_names_scanned": len(names),
        "forbidden_normalized_tokens": list(forbidden_tokens),
        "matches": [],
        "dedicated_pump_foreend_action_node": False,
    }


def _load_gltf(path: Path) -> Mapping[str, Any]:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"invalid glTF JSON: {path}") from exc
    if not isinstance(document, Mapping):
        raise ContractError("glTF root is not an object")
    asset = document.get("asset")
    if not isinstance(asset, Mapping) or asset.get("version") != "2.0":
        raise ContractError("export is not glTF 2.0")
    return document


def audit_to_temp(output_root: Path) -> dict[str, Any]:
    output_root = output_root.resolve(strict=True)
    if output_root == SYSTEM_TEMP_ROOT or SYSTEM_TEMP_ROOT not in output_root.parents:
        raise ContractError(f"audit output is not a strict system TEMP child: {output_root}")
    parser_pin = _verify_pin(PARSER_PIN, "Source2Viewer CLI")
    parser_version = _verify_parser_version()
    donor_reports: dict[str, Any] = {}
    for spec in DONORS:
        model_pin = _verify_pin(spec.model, f"{spec.key} compiled donor")
        gltf_path, export_report = _export_donor(spec, output_root)
        document = _load_gltf(gltf_path)
        exported_names = {
            animation.get("name")
            for animation in _require_list(document, "animations")
            if isinstance(animation, Mapping)
        }
        if exported_names != set(spec.animations):
            raise ContractError(
                f"{spec.key} exported animation set drift: "
                f"expected {sorted(spec.animations)!r}, observed {sorted(exported_names)!r}"
            )
        proofs = [
            _prove_requirement(document, gltf_path, output_root, requirement)
            for requirement in spec.requirements
        ]
        report: dict[str, Any] = {
            "compiled_model": model_pin,
            "export": export_report,
            "channel_proofs": proofs,
        }
        if spec.inventory_nodes:
            report["target_channel_inventory"] = [
                _observe_requirement(
                    document,
                    gltf_path,
                    output_root,
                    ChannelRequirement(animation, node, path, path),
                )
                for animation in spec.animations
                for node in spec.inventory_nodes
                for path in ("translation", "rotation")
            ]
        if spec.key == "spaghelli":
            report["action_node_absence"] = _prove_no_spaghelli_action_node(document)
        donor_reports[spec.key] = report
        _verify_pin(spec.model, f"{spec.key} compiled donor after export")
    _verify_pin(PARSER_PIN, "Source2Viewer CLI after exports")
    return {
        "version": 1,
        "status": "PASS",
        "writes": {
            "game_tree": False,
            "repository": False,
            "editor": False,
            "portal": False,
            "temp_only": True,
        },
        "source2viewer": {**parser_pin, "version": parser_version},
        "donors": donor_reports,
        "proof_ceiling": (
            "Pinned compiled-donor glTF channel data only; this does not prove "
            "custom replacement geometry wiring, runtime animation, visual fit, "
            "reload state-machine timing, IK, audio, editor health, or Portal delivery."
        ),
    }


def main(argv: Sequence[str] | None = None) -> int:
    arguments = list(sys.argv[1:] if argv is None else argv)
    if arguments:
        raise ContractError(
            "this sealed auditor accepts no output or promotion arguments"
        )
    temporary_path: Path | None = None
    with tempfile.TemporaryDirectory(prefix=TEMP_PREFIX, dir=SYSTEM_TEMP_ROOT) as raw:
        temporary_path = Path(raw).resolve(strict=True)
        report = audit_to_temp(temporary_path)
    assert temporary_path is not None
    if temporary_path.exists():
        raise ContractError(f"temporary audit output was not removed: {temporary_path}")
    report["temporary_output"] = {
        "system_temp_root": str(SYSTEM_TEMP_ROOT),
        "prefix": TEMP_PREFIX,
        "removed_before_report": True,
    }
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ContractError as exc:
        print(f"DONOR_ANIMATION_AUDIT_FAIL: {exc}", file=sys.stderr)
        raise SystemExit(1)
