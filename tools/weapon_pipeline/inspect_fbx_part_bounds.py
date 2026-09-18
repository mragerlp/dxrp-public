"""Print compact per-mesh bounds for an FBX without modifying it.

Run with Blender:

  blender --background --factory-startup \
    --python inspect_fbx_part_bounds.py -- SOURCE_FBX
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def rounded(vector) -> list[float]:
    return [round(float(value), 6) for value in vector]


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    mins = Vector(tuple(min(corner[index] for corner in corners) for index in range(3)))
    maxs = Vector(tuple(max(corner[index] for corner in corners) for index in range(3)))
    return mins, maxs


def main() -> None:
    try:
        separator = sys.argv.index("--")
        source = Path(sys.argv[separator + 1]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit("Expected SOURCE_FBX after --") from exc

    if not source.is_file():
        raise SystemExit(f"Source FBX does not exist: {source}")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source))

    rows = []
    for obj in sorted(
        (candidate for candidate in bpy.context.scene.objects if candidate.type == "MESH"),
        key=lambda candidate: candidate.name,
    ):
        mins, maxs = world_bounds(obj)
        rows.append(
            {
                "name": obj.name,
                "parent": obj.parent.name if obj.parent else None,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "center": rounded((mins + maxs) * 0.5),
                "size": rounded(maxs - mins),
                "mins": rounded(mins),
                "maxs": rounded(maxs),
                "materials": [
                    material.name if material else None for material in obj.data.materials
                ],
            }
        )

    print("DXRP_FBX_PART_BOUNDS=" + json.dumps(rows, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_FBX_PART_BOUNDS_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
