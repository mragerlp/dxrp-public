"""Render evidence views of the separated Custom Pistol 9mm source.

The intake contains repeated presentation copies.  This keeps the assembled
copy at the source origin, colors the source-authored reload hierarchy, and
renders side/top views without changing the FBX or any game asset.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SLIDE_SUFFIXES = {"", ".006", ".007", ".009", ".012", ".018"}


def fail(message: str) -> None:
    raise RuntimeError(message)


def material(name: str, color: tuple[float, float, float, float]):
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    return result


def look_at(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    try:
        separator = sys.argv.index("--")
        source = Path(sys.argv[separator + 1]).resolve()
        output_dir = Path(sys.argv[separator + 2]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit("Expected SOURCE_FBX and OUTPUT_DIR after --") from exc

    if not source.is_file():
        raise SystemExit(f"Source FBX does not exist: {source}")
    output_dir.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source))

    selected = []
    for obj in list(bpy.context.scene.objects):
        keep = False
        if obj.type == "MESH":
            if obj.name == "CustomPistol9mm":
                keep = True
            elif obj.name.startswith("CustomPistol9mm."):
                suffix = obj.name[len("CustomPistol9mm") :]
                keep = suffix[1:].isdigit() and int(suffix[1:]) <= 22
            elif obj.name == "Magazine9mm" or obj.name in {
                "Magazine9mm.001",
                "Magazine9mm.002",
                "Magazine9mm.003",
            }:
                keep = True
        if keep:
            selected.append(obj)
        else:
            bpy.data.objects.remove(obj, do_unlink=True)

    if len(selected) != 27:
        fail(f"expected 27 assembled pistol meshes, found {len(selected)}")

    root_mat = material("semantic_root", (0.18, 0.20, 0.24, 1.0))
    slide_mat = material("semantic_slide", (0.08, 0.42, 0.90, 1.0))
    detail_mat = material("semantic_detail", (0.94, 0.55, 0.08, 1.0))
    magazine_mat = material("semantic_magazine", (0.10, 0.70, 0.30, 1.0))

    for obj in selected:
        if obj.name.startswith("Magazine9mm"):
            chosen = magazine_mat
        elif obj.name == "CustomPistol9mm.022":
            chosen = root_mat
        else:
            suffix = obj.name[len("CustomPistol9mm") :]
            chosen = slide_mat if suffix in SLIDE_SUFFIXES else detail_mat
        obj.data.materials.clear()
        obj.data.materials.append(chosen)

    mins = Vector((math.inf, math.inf, math.inf))
    maxs = Vector((-math.inf, -math.inf, -math.inf))
    for obj in selected:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            mins.x, mins.y, mins.z = min(mins.x, world.x), min(mins.y, world.y), min(mins.z, world.z)
            maxs.x, maxs.y, maxs.z = max(maxs.x, world.x), max(maxs.y, world.y), max(maxs.z, world.z)
    center = (mins + maxs) * 0.5

    camera_data = bpy.data.cameras.new("EvidenceCamera")
    camera = bpy.data.objects.new("EvidenceCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera
    camera_data.type = "ORTHO"

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "BOTH"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.035, 0.035, 0.045)

    views = {
        "side": (Vector((0.45, center.y, center.z)), max(maxs.y - mins.y, maxs.z - mins.z) * 1.25),
        "top": (Vector((center.x, center.y, 0.45)), max(maxs.x - mins.x, maxs.y - mins.y) * 1.25),
    }
    for name, (location, scale) in views.items():
        camera.location = location
        camera_data.ortho_scale = scale
        look_at(camera, center)
        scene.render.filepath = str(output_dir / f"custom_pistol_9mm_{name}.png")
        bpy.ops.render.render(write_still=True)

    print(f"DXRP_CUSTOM_PISTOL_RENDER={output_dir}")


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_CUSTOM_PISTOL_RENDER_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
