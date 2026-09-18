"""Build deterministic, temp-only weapon audio wiring candidates.

This tool changes the serialized ``ShootSound`` value and, where explicitly
requested, ``SuppressedShootSound`` in a selected world-weapon prefab.  It
deliberately has no product-write switch: the staged WAV/.sound assets, editor
compile, playback, and redistribution rights are separate acceptance gates.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import tempfile
from dataclasses import dataclass
from pathlib import Path
import re
from typing import Iterable

import pipeline_io


REPO_ROOT = Path(__file__).resolve().parents[2]
GAME_ROOT = REPO_ROOT / "game"
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()


@dataclass(frozen=True)
class WeaponAudioSpec:
    name: str
    source: str
    source_sha256: str
    event: str
    suppressed_event: str | None = None


SPECS: dict[str, WeaponAudioSpec] = {
    "m870": WeaponAudioSpec(
        name="m870",
        source=(
            "game/Assets/addons/lifepunch/lpweapons/m870/equipment/"
            "w_m870/w_m870.prefab"
        ),
        source_sha256=(
            "39B6FB0D54373DD77847BA6301DCD9582E309F2BFB4928A39062BA0F6C26F2E3"
        ),
        event="addons/lifepunch/lpweapons/m870/sounds/m870_shot.sound",
    ),
    "glock": WeaponAudioSpec(
        name="glock",
        source=(
            "game/Assets/addons/lifepunch/lpweapons/glock/equipment/"
            "w_glock/w_glock.prefab"
        ),
        source_sha256=(
            "7000AF72BB387813077CDB4CF3247C35E294CB77EF0B42A11F9A1C7BE516A376"
        ),
        event="addons/lifepunch/lpweapons/glock/sounds/glock_shot.sound",
        suppressed_event=(
            "addons/lifepunch/lpweapons/glock/sounds/glock_suppressed_shot.sound"
        ),
    ),
    "m1911": WeaponAudioSpec(
        name="m1911",
        source=(
            "game/Assets/addons/lifepunch/lpweapons/m1911/equipment/"
            "w_m1911/w_m1911.prefab"
        ),
        source_sha256=(
            "7186E086ACFE0A8BDA1949A84289575820D4D257AA115BA270BFEF9680E13D69"
        ),
        event="addons/lifepunch/lpweapons/m1911/sounds/m1911_shot.sound",
    ),
}


SHOOT_COMPONENT = "Dxura.RP.Game.ShootWeaponComponent"
SHOOT_SOUND_PATTERN = re.compile(
    rb'("__type"\s*:\s*"Dxura\.RP\.Game\.ShootWeaponComponent"'
    rb'.*?"ShootSound"\s*:\s*)(null|"(?:\\.|[^"\\])*")',
    re.DOTALL,
)
SUPPRESSED_SHOOT_SOUND_PATTERN = re.compile(
    rb'("SuppressedShootSound"\s*:\s*)(null|"(?:\\.|[^"\\])*")'
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def require_temp_output(output_dir: Path) -> Path:
    resolved = output_dir.resolve(strict=False)
    if _is_relative_to(resolved, REPO_ROOT.resolve()):
        raise ValueError("Audio candidates must stay outside the repository")
    if not _is_relative_to(resolved, SYSTEM_TEMP_ROOT):
        raise ValueError("Audio candidates must stay beneath the system temp directory")
    return resolved


def _walk(value: object) -> Iterable[dict[str, object]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk(child)


def inspect_candidate(
    candidate: bytes,
    expected_event: str,
    expected_suppressed_event: str | None = None,
) -> None:
    document = json.loads(candidate.decode("utf-8-sig"))
    components = [
        node
        for node in _walk(document)
        if node.get("__type") == SHOOT_COMPONENT
    ]
    if len(components) != 1:
        raise ValueError(
            f"Expected exactly one {SHOOT_COMPONENT}; found {len(components)}"
        )
    if components[0].get("ShootSound") != expected_event:
        raise ValueError("Candidate ShootSound does not match the requested event")
    if (
        expected_suppressed_event is not None
        and components[0].get("SuppressedShootSound") != expected_suppressed_event
    ):
        raise ValueError(
            "Candidate SuppressedShootSound does not match the requested event"
        )


def build_candidate_bytes(
    source: bytes,
    event: str,
    suppressed_event: str | None = None,
) -> tuple[bytes, object]:
    matches = list(SHOOT_SOUND_PATTERN.finditer(source))
    if len(matches) != 1:
        raise ValueError(
            "Expected exactly one serialized ShootWeaponComponent/ShootSound pair; "
            f"found {len(matches)}"
        )

    match = matches[0]
    old_literal = match.group(2)
    old_value = json.loads(old_literal.decode("utf-8"))
    new_literal = json.dumps(event, ensure_ascii=False).encode("utf-8")
    candidate = source[: match.start(2)] + new_literal + source[match.end(2) :]

    if suppressed_event is not None:
        suppressed_matches = list(SUPPRESSED_SHOOT_SOUND_PATTERN.finditer(candidate))
        suppressed_literal = json.dumps(
            suppressed_event, ensure_ascii=False
        ).encode("utf-8")
        if len(suppressed_matches) > 1:
            raise ValueError(
                "Expected at most one serialized SuppressedShootSound; "
                f"found {len(suppressed_matches)}"
            )
        if suppressed_matches:
            suppressed_match = suppressed_matches[0]
            candidate = (
                candidate[: suppressed_match.start(2)]
                + suppressed_literal
                + candidate[suppressed_match.end(2) :]
            )
        else:
            updated_matches = list(SHOOT_SOUND_PATTERN.finditer(candidate))
            if len(updated_matches) != 1:
                raise ValueError(
                    "Candidate lost its unique ShootSound insertion seam"
                )
            updated_match = updated_matches[0]
            newline = b"\r\n" if b"\r\n" in candidate else b"\n"
            property_start = candidate.rfind(
                b'"ShootSound"', 0, updated_match.start(2)
            )
            line_start = candidate.rfind(newline, 0, property_start)
            indent = candidate[line_start + len(newline) : property_start]
            insertion = (
                b"," + newline + indent + b'"SuppressedShootSound": '
                + suppressed_literal
            )
            candidate = (
                candidate[: updated_match.end(2)]
                + insertion
                + candidate[updated_match.end(2) :]
            )

    if candidate == source:
        raise ValueError("Requested event is already present; no candidate was built")

    inspect_candidate(candidate, event, suppressed_event)
    return candidate, old_value


def write_atomic(path: Path, data: bytes) -> None:
    path = pipeline_io.assert_temp_destination(path)
    pipeline_io.write_bytes_atomic(path, data)


def build_one(spec: WeaponAudioSpec, output_dir: Path) -> dict[str, object]:
    output_dir = require_temp_output(output_dir)
    source_path = REPO_ROOT / spec.source
    source = source_path.read_bytes()
    actual_source_sha = sha256(source)
    if actual_source_sha != spec.source_sha256:
        raise ValueError(
            f"{spec.name} source SHA mismatch: expected {spec.source_sha256}, "
            f"got {actual_source_sha}"
        )

    candidate, old_value = build_candidate_bytes(
        source,
        spec.event,
        spec.suppressed_event,
    )
    candidate_sha = sha256(candidate)
    output = output_dir / f"w_{spec.name}.audio_candidate.prefab"
    write_atomic(output, candidate)
    if sha256(output.read_bytes()) != candidate_sha:
        raise ValueError(f"{spec.name} candidate changed after atomic write")

    return {
        "weapon": spec.name,
        "source": spec.source,
        "source_sha256": actual_source_sha,
        "candidate": str(output),
        "candidate_sha256": candidate_sha,
        "previous_shoot_sound": old_value,
        "requested_shoot_sound": spec.event,
        "requested_suppressed_shoot_sound": spec.suppressed_event,
        "pump_sound": (
            "addons/lifepunch/lpweapons/m870/sounds/m870_pump.sound"
            if spec.name == "m870"
            else None
        ),
        "pump_wiring": (
            "staged_unwired_no_verified_pump_driver"
            if spec.name == "m870"
            else None
        ),
        "accepted": False,
        "proof_ceiling": "temp candidate only; no editor/runtime/audio/rights proof",
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--weapon",
        action="append",
        choices=sorted(SPECS),
        help="Repeat to select weapons; defaults to all three.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)

    output_dir = require_temp_output(args.output_dir)
    if output_dir.exists() and any(output_dir.iterdir()):
        raise ValueError("Audio candidate output directory must be empty")
    selected = args.weapon or sorted(SPECS)
    records = [build_one(SPECS[name], output_dir) for name in selected]

    manifest = {
        "schema": "dxrp.weapon-audio-candidate.v1",
        "accepted": False,
        "records": records,
    }
    manifest_path = output_dir / "weapon_audio_candidates.json"
    write_atomic(
        manifest_path,
        (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )
    print(json.dumps(manifest, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
