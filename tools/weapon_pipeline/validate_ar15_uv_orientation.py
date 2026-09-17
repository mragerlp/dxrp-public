"""Fail closed when the generated AR-15 FBX regresses to mirrored UVs.

Run with Blender so its FBX importer is the parser under test:

  blender --background --factory-startup \
    --python validate_ar15_uv_orientation.py -- GENERATED_FBX

The expected fingerprint was derived from the intact Fab viewer model and then
confirmed visually against the publisher render.  It covers every imported
loop's vertex association and UV coordinate across all seven semantic meshes.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy


ROUND_DIGITS = 8
EXPECTED_CORRECTED_UV_SHA256 = (
    "9D2376E0A47E432B5DF60E41D4C2508B76E21506819AF0F40262722F2A737D33"
)
KNOWN_MIRRORED_UV_SHA256 = (
    "5AAAFA73A04E5F89484EF744A07DA0D549390F1DF504366DA469678F6AE2EAD7"
)


def parse_path() -> Path:
    try:
        separator = sys.argv.index("--")
        path = Path(sys.argv[separator + 1]).resolve()
    except (ValueError, IndexError):
        raise SystemExit("Expected one generated FBX path after --")
    if not path.is_file():
        raise SystemExit(f"Generated FBX does not exist: {path}")
    return path


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def uv_fingerprint(path: Path) -> tuple[str, int, int]:
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(path))
    mesh_objects = sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    payload = []
    loop_count = 0
    for obj in mesh_objects:
        uv_layer = obj.data.uv_layers.active
        if uv_layer is None:
            raise RuntimeError(f"Generated mesh has no active UV layer: {obj.name}")
        loops = []
        for loop in obj.data.loops:
            uv = uv_layer.data[loop.index].uv
            loops.append(
                {
                    "vertex": loop.vertex_index,
                    "uv": [
                        round(float(uv.x), ROUND_DIGITS),
                        round(float(uv.y), ROUND_DIGITS),
                    ],
                }
            )
        loop_count += len(loops)
        payload.append({"name": obj.name, "loops": loops})

    canonical = json.dumps(
        payload,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest().upper(), len(mesh_objects), loop_count


def main() -> None:
    path = parse_path()
    actual, mesh_count, loop_count = uv_fingerprint(path)
    result = {
        "path": str(path),
        "mesh_count": mesh_count,
        "loop_count": loop_count,
        "uv_sha256": actual,
        "expected_uv_sha256": EXPECTED_CORRECTED_UV_SHA256,
        "known_mirrored_uv_sha256": KNOWN_MIRRORED_UV_SHA256,
        "accepted": actual == EXPECTED_CORRECTED_UV_SHA256,
    }
    print("DXRP_AR15_UV_ORIENTATION=" + json.dumps(result, sort_keys=True))
    if actual != EXPECTED_CORRECTED_UV_SHA256:
        shape = "known mirrored orientation" if actual == KNOWN_MIRRORED_UV_SHA256 else "unknown drift"
        raise SystemExit(f"AR-15 UV orientation rejected: {shape}")


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_AR15_UV_ORIENTATION_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
