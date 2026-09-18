#!/usr/bin/env python3
"""Stage pinned CS:GO weapon-audio candidates without touching the game tree.

The default invocation reads the exact pinned archive and writes a deterministic
candidate tree below the system TEMP directory. This helper is deliberately
TEMP-only and exposes no product-write switch because redistribution rights are
not established by the archive.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import struct
import sys
import tempfile
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Iterable, Mapping

from pipeline_io import promote_files, staging_path


ARCHIVE_EXPECTED_BYTES = 1_764_941_503
ARCHIVE_EXPECTED_SHA256 = (
    "E8452275A5007670D367B4E870A8FC3E057B41560B3E7C93C5886BD96DE9BEB7"
)
DEFAULT_ARCHIVE = Path(r"C:\Users\jared\Downloads\csgo-master.zip")
DEFAULT_STAGE_NAME = "dxrp-csgo-weapon-audio-e8452275a5007670-v1"
EQUIPMENT_MIXER_ID = "cbe1a5ff-7ef1-420c-abf4-06d6e0e7d736"
LICENSE_NAMES = {
    "copying",
    "copyright",
    "license",
    "licenses",
    "licence",
    "licences",
    "notice",
}

REPO_ROOT = Path(__file__).resolve().parents[2]


@dataclass(frozen=True)
class WaveSpec:
    weapon: str
    role: str
    source_entry: str
    output_path: str
    expected_bytes: int
    expected_sha256: str


@dataclass(frozen=True)
class WaveMetadata:
    audio_format: int
    channels: int
    sample_rate: int
    byte_rate: int
    block_align: int
    bits_per_sample: int
    data_bytes: int


@dataclass(frozen=True)
class EventSpec:
    weapon: str
    role: str
    output_path: str
    sounds: tuple[str, ...]
    decibels: int
    distance: int
    occlusion_radius: int
    falloff: str
    wiring_status: str


WAVES = (
    WaveSpec(
        weapon="m870",
        role="nova_shot",
        source_entry="csgo-master/sound/weapons/nova/nova-1.wav",
        output_path="game/Assets/addons/lifepunch/lpweapons/m870/sounds/m870_shot.wav",
        expected_bytes=505_264,
        expected_sha256=(
            "D30659B4CB5AA1A6F72D292F9A4A568B59671EE9B96BAC3AFA63D4A600C7B6B6"
        ),
    ),
    WaveSpec(
        weapon="m870",
        role="nova_pump_unwired",
        source_entry="csgo-master/sound/weapons/nova/nova_pump.wav",
        output_path="game/Assets/addons/lifepunch/lpweapons/m870/sounds/m870_pump.wav",
        expected_bytes=35_914,
        expected_sha256=(
            "AE8B0F14117978612839BE08CE3BF6C241604B6F04057714A6E527B5C263B9BA"
        ),
    ),
    WaveSpec(
        weapon="glock",
        role="glock_normal_01",
        source_entry="csgo-master/sound/weapons/glock18/glock_01.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_shot_01.wav"
        ),
        expected_bytes=292_492,
        expected_sha256=(
            "3B865D45D9B8461FD571FA8AF1A24C7C5B3BC66C22057729CD471F4190DAA87E"
        ),
    ),
    WaveSpec(
        weapon="glock",
        role="glock_normal_02",
        source_entry="csgo-master/sound/weapons/glock18/glock_02.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_shot_02.wav"
        ),
        expected_bytes=294_130,
        expected_sha256=(
            "BF206B187625E09EF4C68CAE609C1E6B96770F0F1259D9F015F2C59B09D8266C"
        ),
    ),
    WaveSpec(
        weapon="glock",
        role="usp_suppressed_01",
        source_entry="csgo-master/sound/weapons/usp/usp_01.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_suppressed_shot_01.wav"
        ),
        expected_bytes=286_390,
        expected_sha256=(
            "39779E6C80FBD1ECBBCC2A9BF2CF379A72917538C4C41BEB9CC4DE64CBA89F6E"
        ),
    ),
    WaveSpec(
        weapon="glock",
        role="usp_suppressed_02",
        source_entry="csgo-master/sound/weapons/usp/usp_02.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_suppressed_shot_02.wav"
        ),
        expected_bytes=286_466,
        expected_sha256=(
            "C289655BA36406B64021115C1CDD1F9A5E43F1DC7F042AAD831F833A82479FD6"
        ),
    ),
    WaveSpec(
        weapon="glock",
        role="usp_suppressed_03",
        source_entry="csgo-master/sound/weapons/usp/usp_03.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_suppressed_shot_03.wav"
        ),
        expected_bytes=286_466,
        expected_sha256=(
            "B7DB2B7D8CC0EFEEBB12951DF86EE4BF8842B7A5ABD31369F1BA6E021BD07A39"
        ),
    ),
    WaveSpec(
        weapon="m1911",
        role="fiveseven_01",
        source_entry="csgo-master/sound/weapons/fiveseven/fiveseven_01.wav",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/m1911/sounds/m1911_shot_01.wav"
        ),
        expected_bytes=283_124,
        expected_sha256=(
            "FAAC507C225475A5B49889B3FB40503F9546FED2602CBCDAF9FE1FD5FB7538C1"
        ),
    ),
)


EVENTS = (
    EventSpec(
        weapon="m870",
        role="nova_shot",
        output_path="game/Assets/addons/lifepunch/lpweapons/m870/sounds/m870_shot.sound",
        sounds=("addons/lifepunch/lpweapons/m870/sounds/m870_shot.vsnd",),
        decibels=70,
        distance=2_500,
        occlusion_radius=64,
        falloff="shot",
        wiring_status="candidate_only_not_wired",
    ),
    EventSpec(
        weapon="m870",
        role="nova_pump_unwired",
        output_path="game/Assets/addons/lifepunch/lpweapons/m870/sounds/m870_pump.sound",
        sounds=("addons/lifepunch/lpweapons/m870/sounds/m870_pump.vsnd",),
        decibels=60,
        distance=1_500,
        occlusion_radius=64,
        falloff="mechanical",
        wiring_status="deliberately_unwired",
    ),
    EventSpec(
        weapon="glock",
        role="glock_normal_random_01_02",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_shot.sound"
        ),
        sounds=(
            "addons/lifepunch/lpweapons/glock/sounds/glock_shot_01.vsnd",
            "addons/lifepunch/lpweapons/glock/sounds/glock_shot_02.vsnd",
        ),
        decibels=70,
        distance=2_500,
        occlusion_radius=64,
        falloff="shot",
        wiring_status="candidate_only_not_wired",
    ),
    EventSpec(
        weapon="glock",
        role="usp_suppressed_random_01_02_03",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/glock/sounds/"
            "glock_suppressed_shot.sound"
        ),
        sounds=(
            "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_01.vsnd",
            "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_02.vsnd",
            "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot_03.vsnd",
        ),
        decibels=70,
        distance=2_500,
        occlusion_radius=64,
        falloff="shot",
        wiring_status="candidate_only_not_wired",
    ),
    EventSpec(
        weapon="m1911",
        role="fiveseven_01",
        output_path=(
            "game/Assets/addons/lifepunch/lpweapons/m1911/sounds/m1911_shot.sound"
        ),
        sounds=("addons/lifepunch/lpweapons/m1911/sounds/m1911_shot_01.vsnd",),
        decibels=70,
        distance=2_500,
        occlusion_radius=64,
        falloff="shot",
        wiring_status="candidate_only_not_wired",
    ),
)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True


def is_reparse_point(path: Path) -> bool:
    result = path.lstat()
    attributes = getattr(result, "st_file_attributes", 0)
    reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(attributes & reparse_flag)


def assert_regular_archive(path: Path) -> Path:
    if not path.exists():
        raise RuntimeError(f"Archive does not exist: {path}")
    if is_reparse_point(path):
        raise RuntimeError(f"Archive path may not be a symlink or reparse point: {path}")
    if not path.is_file():
        raise RuntimeError(f"Archive is not a regular file: {path}")
    return path.resolve(strict=True)


def assert_regular_source_root(path: Path) -> Path:
    if not path.exists():
        raise RuntimeError(f"Source root does not exist: {path}")
    if is_reparse_point(path):
        raise RuntimeError(
            f"Source root may not be a symlink or reparse point: {path}"
        )
    if not path.is_dir():
        raise RuntimeError(f"Source root is not a directory: {path}")
    return path.resolve(strict=True)


def validate_source_root(
    source_root: Path,
) -> tuple[dict[WaveSpec, bytes], dict[WaveSpec, WaveMetadata], dict[str, object]]:
    source_root = assert_regular_source_root(source_root)
    contents: dict[WaveSpec, bytes] = {}
    metadata: dict[WaveSpec, WaveMetadata] = {}

    for spec in WAVES:
        relative = PurePosixPath(spec.source_entry)
        if relative.is_absolute() or any(part in {"", ".", ".."} for part in relative.parts):
            raise RuntimeError(f"Unsafe selected source path: {spec.source_entry}")

        source_path = source_root.joinpath(*relative.parts)
        if not source_path.exists():
            raise RuntimeError(f"Selected source does not exist: {source_path}")
        if is_reparse_point(source_path):
            raise RuntimeError(
                f"Selected source may not be a symlink or reparse point: {source_path}"
            )
        if not source_path.is_file():
            raise RuntimeError(f"Selected source is not a regular file: {source_path}")

        resolved = source_path.resolve(strict=True)
        if not is_relative_to(resolved, source_root):
            raise RuntimeError(
                f"Selected source escapes the verified root: {source_path}"
            )

        initial = source_path.stat()
        data = source_path.read_bytes()
        final = source_path.stat()
        before_identity = (
            initial.st_dev,
            initial.st_ino,
            initial.st_size,
            initial.st_mtime_ns,
        )
        after_identity = (
            final.st_dev,
            final.st_ino,
            final.st_size,
            final.st_mtime_ns,
        )
        actual_sha = sha256_bytes(data)
        if before_identity != after_identity:
            raise RuntimeError(
                f"Selected source identity changed during validation: {source_path}"
            )
        if len(data) != spec.expected_bytes or actual_sha != spec.expected_sha256:
            raise RuntimeError(
                f"{spec.source_entry} failed expected byte/SHA custody: "
                f"bytes={len(data)}, sha256={actual_sha}"
            )

        contents[spec] = data
        metadata[spec] = validate_wave(data, spec.source_entry)

    source_record: dict[str, object] = {
        "kind": "extracted_source_root",
        "path": str(source_root),
        "selected_files": len(contents),
        "preserved": True,
    }
    return contents, metadata, source_record


def validate_wave(data: bytes, source_entry: str) -> WaveMetadata:
    if len(data) < 12 or data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise RuntimeError(f"{source_entry} is not a RIFF/WAVE file")

    riff_size = struct.unpack_from("<I", data, 4)[0]
    container_end = riff_size + 8
    if container_end != len(data):
        raise RuntimeError(
            f"{source_entry} RIFF size is {container_end}, actual bytes are {len(data)}"
        )

    cursor = 12
    format_fields: tuple[int, int, int, int, int, int] | None = None
    data_bytes = 0
    while cursor < container_end:
        if cursor + 8 > container_end:
            raise RuntimeError(f"{source_entry} has a truncated RIFF chunk header")
        chunk_id = data[cursor : cursor + 4]
        chunk_size = struct.unpack_from("<I", data, cursor + 4)[0]
        payload_start = cursor + 8
        payload_end = payload_start + chunk_size
        padded_end = payload_end + (chunk_size & 1)
        if payload_end > container_end or padded_end > container_end:
            raise RuntimeError(f"{source_entry} has an out-of-bounds RIFF chunk")

        if chunk_id == b"fmt ":
            if format_fields is not None:
                raise RuntimeError(f"{source_entry} has multiple fmt chunks")
            if chunk_size < 16:
                raise RuntimeError(f"{source_entry} has a short fmt chunk")
            format_fields = struct.unpack_from("<HHIIHH", data, payload_start)
        elif chunk_id == b"data":
            data_bytes += chunk_size

        cursor = padded_end

    if format_fields is None:
        raise RuntimeError(f"{source_entry} has no fmt chunk")
    if data_bytes <= 0:
        raise RuntimeError(f"{source_entry} has no non-empty data chunk")

    (
        audio_format,
        channels,
        sample_rate,
        byte_rate,
        block_align,
        bits_per_sample,
    ) = format_fields
    if audio_format != 1:
        raise RuntimeError(
            f"{source_entry} is WAVE format {audio_format}, expected PCM format 1"
        )
    if channels <= 0 or sample_rate <= 0 or bits_per_sample <= 0:
        raise RuntimeError(f"{source_entry} has invalid PCM format fields")
    if bits_per_sample % 8 != 0:
        raise RuntimeError(f"{source_entry} PCM bit depth is not byte-aligned")
    expected_block_align = channels * (bits_per_sample // 8)
    if block_align != expected_block_align:
        raise RuntimeError(
            f"{source_entry} block alignment is {block_align}, "
            f"expected {expected_block_align}"
        )
    if byte_rate != sample_rate * block_align:
        raise RuntimeError(f"{source_entry} has inconsistent PCM byte rate")
    if data_bytes % block_align != 0:
        raise RuntimeError(f"{source_entry} PCM data is not frame-aligned")

    return WaveMetadata(
        audio_format=audio_format,
        channels=channels,
        sample_rate=sample_rate,
        byte_rate=byte_rate,
        block_align=block_align,
        bits_per_sample=bits_per_sample,
        data_bytes=data_bytes,
    )


def common_license_entries(names: Iterable[str]) -> list[str]:
    matches: list[str] = []
    for name in names:
        tokens = {
            token
            for component in PurePosixPath(name).parts
            for token in re.split(r"[^a-z]+", component.lower())
            if token
        }
        if tokens & LICENSE_NAMES:
            matches.append(name)
    return sorted(matches)


def validate_archive(
    archive_path: Path,
) -> tuple[dict[WaveSpec, bytes], dict[WaveSpec, WaveMetadata], list[str], dict[str, object]]:
    archive_path = assert_regular_archive(archive_path)
    initial = archive_path.stat()
    if initial.st_size != ARCHIVE_EXPECTED_BYTES:
        raise RuntimeError(
            f"Archive byte count is {initial.st_size}, expected {ARCHIVE_EXPECTED_BYTES}"
        )

    before_sha = sha256_file(archive_path)
    if before_sha != ARCHIVE_EXPECTED_SHA256:
        raise RuntimeError(
            f"Archive SHA-256 is {before_sha}, expected {ARCHIVE_EXPECTED_SHA256}"
        )

    contents: dict[WaveSpec, bytes] = {}
    metadata: dict[WaveSpec, WaveMetadata] = {}
    with zipfile.ZipFile(archive_path, mode="r") as archive:
        infos_by_name: dict[str, list[zipfile.ZipInfo]] = {}
        for info in archive.infolist():
            infos_by_name.setdefault(info.filename, []).append(info)
        license_entries = common_license_entries(infos_by_name)

        for spec in WAVES:
            matches = infos_by_name.get(spec.source_entry, [])
            if len(matches) != 1:
                raise RuntimeError(
                    f"Expected exactly one archive entry {spec.source_entry}, "
                    f"found {len(matches)}"
                )
            info = matches[0]
            if info.is_dir():
                raise RuntimeError(f"Selected archive entry is a directory: {spec.source_entry}")
            if info.file_size != spec.expected_bytes:
                raise RuntimeError(
                    f"{spec.source_entry} has {info.file_size} bytes, "
                    f"expected {spec.expected_bytes}"
                )
            data = archive.read(info)
            actual_sha = sha256_bytes(data)
            if len(data) != spec.expected_bytes or actual_sha != spec.expected_sha256:
                raise RuntimeError(
                    f"{spec.source_entry} failed expected byte/SHA custody: "
                    f"bytes={len(data)}, sha256={actual_sha}"
                )
            contents[spec] = data
            metadata[spec] = validate_wave(data, spec.source_entry)

    final = archive_path.stat()
    after_sha = sha256_file(archive_path)
    before_identity = (initial.st_dev, initial.st_ino, initial.st_size, initial.st_mtime_ns)
    after_identity = (final.st_dev, final.st_ino, final.st_size, final.st_mtime_ns)
    if before_identity != after_identity or before_sha != after_sha:
        raise RuntimeError("Archive identity or SHA-256 changed during validation")

    archive_record: dict[str, object] = {
        "path": str(archive_path),
        "expected_bytes": ARCHIVE_EXPECTED_BYTES,
        "actual_bytes": final.st_size,
        "expected_sha256": ARCHIVE_EXPECTED_SHA256,
        "before_sha256": before_sha,
        "after_sha256": after_sha,
        "preserved": True,
    }
    return contents, metadata, license_entries, archive_record


def event_document(event: EventSpec) -> dict[str, object]:
    if event.falloff == "shot":
        falloff: list[dict[str, object]] = [
            {
                "x": 0,
                "y": 1,
                "in": 3.1415927,
                "out": -3.1415927,
                "mode": "Mirrored",
            },
            {"x": 1, "y": 0, "in": 0, "out": 0, "mode": "Mirrored"},
        ]
    elif event.falloff == "mechanical":
        falloff = [
            {"x": 0, "y": 1, "in": 0, "out": -1.8, "mode": "Mirrored"},
            {
                "x": 0.05,
                "y": 0.22,
                "in": 3.5,
                "out": -3.5,
                "mode": "Mirrored",
            },
            {
                "x": 0.2,
                "y": 0.04,
                "in": 0.16,
                "out": -0.16,
                "mode": "Mirrored",
            },
            {"x": 1, "y": 0, "in": 0, "out": 0, "mode": "Mirrored"},
        ]
    else:
        raise RuntimeError(f"Unknown event falloff profile: {event.falloff}")

    return {
        "UI": False,
        "Volume": "1",
        "Pitch": "1",
        "Decibels": event.decibels,
        "SelectionMode": "Random",
        "Sounds": list(event.sounds),
        "Occlusion": True,
        "AirAbsorption": True,
        "Transmission": True,
        "OcclusionRadius": event.occlusion_radius,
        "DistanceAttenuation": True,
        "Distance": event.distance,
        "Falloff": falloff,
        "DefaultMixer": {
            "Name": "equipment",
            "Id": EQUIPMENT_MIXER_ID,
        },
        "__references": [],
        "__version": 1,
    }


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, indent=2, ensure_ascii=True) + "\n").encode("utf-8")


def provenance_bytes(
    archive_record: Mapping[str, object],
    metadata: Mapping[WaveSpec, WaveMetadata],
    license_entries: list[str],
) -> bytes:
    if license_entries:
        license_notice = (
            "> CAUTION: Common license-filename matches exist in this archive, but "
            "this stage does not interpret their terms or establish redistribution "
            "or product-use rights. License, upstream source, and redistribution "
            "clearance remain unresolved."
        )
    else:
        license_notice = (
            "> WARNING: This pinned archive has no entry whose filename identifies a "
            "license, licence, copying, copyright, or notice file. This filename scan "
            "does not establish redistribution or product-use rights. License, upstream "
            "source, and redistribution clearance remain unresolved."
        )
    lines = [
        "# DXRP CS:GO weapon-audio candidate provenance",
        "",
        "Status: TEMP-STAGED CANDIDATE ONLY. No product prefab or code is edited or wired.",
        "",
        "## Archive custody",
        "",
        f"- Source: `{archive_record['path']}`",
        f"- Bytes (expected/actual): `{archive_record['expected_bytes']}` / "
        f"`{archive_record['actual_bytes']}`",
        f"- SHA-256 (expected/before/after): `{archive_record['expected_sha256']}` / "
        f"`{archive_record['before_sha256']}` / `{archive_record['after_sha256']}`",
        "- Open mode: read-only ZIP; before/after identity and SHA-256 matched.",
        f"- Common license-filename matches: `{len(license_entries)}`.",
        "",
        license_notice,
        "",
        "## Exact selected WAVs",
        "",
        "| Weapon | Role | Exact archive entry | Bytes | SHA-256 | PCM | Candidate path |",
        "| --- | --- | --- | ---: | --- | --- | --- |",
    ]
    for spec in WAVES:
        wave = metadata[spec]
        pcm = f"{wave.sample_rate} Hz, {wave.bits_per_sample}-bit, {wave.channels} ch"
        lines.append(
            f"| {spec.weapon.upper()} | {spec.role} | `{spec.source_entry}` | "
            f"{spec.expected_bytes} | `{spec.expected_sha256}` | {pcm} | "
            f"`{spec.output_path}` |"
        )

    lines.extend(
        [
            "",
            "## Deterministic event candidates",
            "",
        ]
    )
    for event in EVENTS:
        lines.append(
            f"- `{event.output_path}`: `{event.role}`, SelectionMode `Random`, "
            f"wiring status `{event.wiring_status}`."
        )
    lines.extend(
        [
            "",
            "The M870 Nova pump WAV and `.sound` candidate are staged for custody and "
            "review but deliberately remain unwired. No prefab, code, scene, project, "
            "Portal, Git, or deployment action is part of this stage.",
            "",
            "Proof ceiling: archive/entry byte custody, RIFF/WAVE PCM structure, and "
            "deterministic candidate serialization only. Runtime playback, perceptual "
            "fit, editor compilation, redistribution rights, and product wiring are "
            "UNVERIFIED.",
            "",
        ]
    )
    return "\n".join(lines).encode("utf-8")


def candidate_files(
    archive_record: Mapping[str, object],
    wave_contents: Mapping[WaveSpec, bytes],
    metadata: Mapping[WaveSpec, WaveMetadata],
    license_entries: list[str],
) -> dict[str, bytes]:
    files: dict[str, bytes] = {
        spec.output_path: wave_contents[spec] for spec in WAVES
    }
    for event in EVENTS:
        files[event.output_path] = json_bytes(event_document(event))

    provenance = provenance_bytes(archive_record, metadata, license_entries)
    files["PROVENANCE.md"] = provenance

    manifest = {
        "schema_version": 1,
        "status": "TEMP_STAGED_CANDIDATE_ONLY",
        "archive": dict(archive_record),
        "license_entry_scan": {
            "method": "common license filename tokens in ZIP entry paths",
            "matches": license_entries,
            "warning": (
                "No license entry is present; redistribution and product-use rights "
                "remain unresolved."
                if not license_entries
                else "Filename matches exist but their terms were not adjudicated."
            ),
        },
        "selection": [
            {
                "weapon": spec.weapon,
                "role": spec.role,
                "source_entry": spec.source_entry,
                "output_path": spec.output_path,
                "expected_bytes": spec.expected_bytes,
                "actual_bytes": len(wave_contents[spec]),
                "expected_sha256": spec.expected_sha256,
                "actual_sha256": sha256_bytes(wave_contents[spec]),
                "wave": asdict(metadata[spec]),
            }
            for spec in WAVES
        ],
        "events": [
            {
                "weapon": event.weapon,
                "role": event.role,
                "output_path": event.output_path,
                "selection_mode": "Random",
                "sounds": list(event.sounds),
                "wiring_status": event.wiring_status,
            }
            for event in EVENTS
        ],
        "game_write": {
            "requested": False,
            "target": None,
            "manifest_is_not_promotion_proof": True,
        },
        "outputs": [
            {
                "path": path,
                "bytes": len(data),
                "sha256": sha256_bytes(data),
            }
            for path, data in sorted(files.items())
        ],
        "proof_ceiling": [
            "archive_sha256_and_byte_count",
            "selected_entry_sha256_and_byte_count",
            "riff_wave_pcm_structure",
            "deterministic_sound_event_serialization",
        ],
        "unverified": [
            "license_and_redistribution_rights",
            "editor_compile",
            "runtime_playback",
            "perceptual_fit",
            "product_wiring",
        ],
    }
    files["manifest.json"] = json_bytes(manifest)
    checksum_lines = [
        f"{sha256_bytes(data)}  {path}" for path, data in sorted(files.items())
    ]
    files["SHA256SUMS"] = ("\n".join(checksum_lines) + "\n").encode("ascii")
    return files


def validate_stage_root(output_root: Path) -> Path:
    temp_root = Path(tempfile.gettempdir()).resolve(strict=True)
    lexical = Path(os.path.abspath(os.fspath(output_root)))
    resolved = lexical.resolve(strict=False)
    if resolved == temp_root or not is_relative_to(resolved, temp_root):
        raise RuntimeError(
            f"Staging output must be a strict child of system TEMP ({temp_root}): "
            f"{resolved}"
        )
    if resolved.exists() and is_reparse_point(resolved):
        raise RuntimeError(f"Staging root may not be a symlink or reparse point: {resolved}")
    return resolved


def write_exact_temp_files(root: Path, files: Mapping[str, bytes]) -> tuple[int, int]:
    root = validate_stage_root(root)
    root.mkdir(parents=True, exist_ok=True)
    pairs: list[tuple[Path, Path]] = []
    unchanged = 0
    for relative, data in sorted(files.items()):
        destination = root.joinpath(*PurePosixPath(relative).parts)
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists() and destination.is_file():
            if destination.read_bytes() == data:
                unchanged += 1
                continue
        staged = staging_path(destination)
        staged.write_bytes(data)
        if staged.stat().st_size != len(data) or sha256_file(staged) != sha256_bytes(data):
            raise RuntimeError(f"Staged output failed write verification: {staged}")
        pairs.append((staged, destination))
    if pairs:
        promote_files(pairs)
    return len(pairs), unchanged


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate and TEMP-stage the pinned M870/Glock/M1911 CS:GO WAV "
            "selection plus deterministic s&box .sound candidates."
        ),
        allow_abbrev=False,
    )
    source = parser.add_mutually_exclusive_group()
    source.add_argument(
        "--archive",
        type=Path,
        default=None,
        help=f"Pinned input ZIP (default: {DEFAULT_ARCHIVE})",
    )
    source.add_argument(
        "--source-root",
        type=Path,
        help=(
            "Organized extracted source root. This mode is verification-only and "
            "must be combined with --verify-only."
        ),
    )
    parser.add_argument(
        "--output-root",
        type=Path,
        default=Path(tempfile.gettempdir()) / DEFAULT_STAGE_NAME,
        help="TEMP child directory for the candidate tree.",
    )
    parser.add_argument(
        "--verify-only",
        action="store_true",
        help="Validate archive, SHA, selected entries, and PCM structure without writing.",
    )
    args = parser.parse_args(argv)
    return args


def run(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if args.source_root is not None:
        if not args.verify_only:
            raise RuntimeError(
                "--source-root is read-only verification only; pass --verify-only"
            )
        _, metadata, source_record = validate_source_root(args.source_root)
        print(
            json.dumps(
                {
                    "result": "PASS",
                    "mode": "verify_only_extracted_source_root",
                    "source": source_record,
                    "selected_waves": len(WAVES),
                    "riff_pcm_validated": len(metadata),
                    "license_scan": "not_performed",
                    "writes": 0,
                },
                separators=(",", ":"),
            )
        )
        return 0

    archive = assert_regular_archive(args.archive or DEFAULT_ARCHIVE)
    wave_contents, metadata, license_entries, archive_record = validate_archive(archive)

    if args.verify_only:
        print(
            json.dumps(
                {
                    "result": "PASS",
                    "mode": "verify_only",
                    "archive_sha256": archive_record["after_sha256"],
                    "archive_preserved": archive_record["preserved"],
                    "license_entry_matches": license_entries,
                    "selected_waves": len(WAVES),
                    "riff_pcm_validated": len(metadata),
                    "writes": 0,
                },
                separators=(",", ":"),
            )
        )
        return 0

    output_root = validate_stage_root(args.output_root)
    files = candidate_files(
        archive_record,
        wave_contents,
        metadata,
        license_entries,
    )
    staged_written, staged_unchanged = write_exact_temp_files(output_root, files)

    print(
        json.dumps(
            {
                "result": "PASS",
                "mode": "temp_stage_only",
                "archive_sha256": archive_record["after_sha256"],
                "archive_preserved": archive_record["preserved"],
                "license_entry_matches": license_entries,
                "selected_waves": len(WAVES),
                "sound_event_candidates": len(EVENTS),
                "stage_root": str(output_root),
                "stage_files_written": staged_written,
                "stage_files_unchanged": staged_unchanged,
                "game_files_written": 0,
                "game_files_unchanged": 0,
            },
            separators=(",", ":"),
        )
    )
    return 0


def main() -> int:
    try:
        return run()
    except (OSError, RuntimeError, ValueError, zipfile.BadZipFile) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
