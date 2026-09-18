"""Print a path-independent semantic fingerprint for one or more FBX files.

Run through Blender so its FBX importer is the parser under test:

  blender --background --factory-startup --python inspect_fbx_semantics.py -- FILE [FILE ...]

The byte-level FBX container may change between exports because exporter metadata
is not stable. This fingerprint covers the imported mesh geometry, transforms,
material assignments, normals, and UVs instead.
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy


ROUND_DIGITS = 8


def rounded(values) -> list[float]:
    return [round(float(value), ROUND_DIGITS) for value in values]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.armatures,
        bpy.data.actions,
        bpy.data.images,
    ):
        for block in list(collection):
            collection.remove(block)


def mesh_payload(obj: bpy.types.Object) -> dict:
    mesh = obj.data
    mesh.update()
    uv_layers = []
    for layer in mesh.uv_layers:
        uv_layers.append(
            {
                "name": layer.name,
                "data": [rounded(loop.uv) for loop in layer.data],
            }
        )

    payload = {
        "name": obj.name,
        "matrix_world": [rounded(row) for row in obj.matrix_world],
        "materials": [material.name if material else None for material in mesh.materials],
        "vertices": [rounded(vertex.co) for vertex in mesh.vertices],
        "loops": [
            {
                "vertex": loop.vertex_index,
                "normal": rounded(loop.normal),
            }
            for loop in mesh.loops
        ],
        "polygons": [
            {
                "vertices": list(polygon.vertices),
                "material": polygon.material_index,
                "smooth": polygon.use_smooth,
            }
            for polygon in mesh.polygons
        ],
        "uv_layers": uv_layers,
    }
    if obj.vertex_groups:
        group_names = {group.index: group.name for group in obj.vertex_groups}
        payload["vertex_groups"] = [
            {
                "name": group.name,
                "weights": [
                    [vertex.index, round(float(element.weight), ROUND_DIGITS)]
                    for vertex in mesh.vertices
                    for element in vertex.groups
                    if element.group == group.index
                ],
            }
            for group in sorted(obj.vertex_groups, key=lambda item: item.name)
        ]
        payload["unresolved_vertex_group_indices"] = sorted(
            {
                element.group
                for vertex in mesh.vertices
                for element in vertex.groups
                if element.group not in group_names
            }
        )

    armature_modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
    if armature_modifiers:
        payload["armature_modifiers"] = [
            {
                "name": modifier.name,
                "object": modifier.object.name if modifier.object else None,
                "use_deform_preserve_volume": modifier.use_deform_preserve_volume,
                "use_vertex_groups": modifier.use_vertex_groups,
            }
            for modifier in armature_modifiers
        ]
    return payload


def armature_payload(obj: bpy.types.Object) -> dict:
    bones = []
    for bone in sorted(obj.data.bones, key=lambda item: item.name):
        bones.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "matrix_local": [rounded(row) for row in bone.matrix_local],
                "head_local": rounded(bone.head_local),
                "tail_local": rounded(bone.tail_local),
                "use_connect": bone.use_connect,
                "use_deform": bone.use_deform,
                "inherit_scale": bone.inherit_scale,
            }
        )
    return {
        "name": obj.name,
        "matrix_world": [rounded(row) for row in obj.matrix_world],
        "bones": bones,
    }


def fcurve_payload(curve) -> dict:
    """Return deterministic animation data for both legacy and layered actions."""
    return {
        "data_path": curve.data_path,
        "array_index": curve.array_index,
        "extrapolation": curve.extrapolation,
        "group": curve.group.name if curve.group else None,
        "keyframes": [
            {
                "co": rounded(point.co),
                "handle_left": rounded(point.handle_left),
                "handle_right": rounded(point.handle_right),
                "handle_left_type": point.handle_left_type,
                "handle_right_type": point.handle_right_type,
                "interpolation": point.interpolation,
                "easing": point.easing,
            }
            for point in curve.keyframe_points
        ],
    }


def action_payload(action: bpy.types.Action) -> dict:
    curves = []
    if hasattr(action, "fcurves"):
        curves.extend(fcurve_payload(curve) for curve in action.fcurves)

    layers = []
    for layer in getattr(action, "layers", []):
        strips = []
        for strip in layer.strips:
            channelbags = []
            for channelbag in getattr(strip, "channelbags", []):
                channelbags.append(
                    {
                        "slot": channelbag.slot.identifier if channelbag.slot else None,
                        "fcurves": sorted(
                            (fcurve_payload(curve) for curve in channelbag.fcurves),
                            key=lambda item: (item["data_path"], item["array_index"]),
                        ),
                    }
                )
            strips.append({"type": strip.type, "channelbags": channelbags})
        layers.append({"name": layer.name, "strips": strips})

    return {
        "name": action.name,
        "frame_range": rounded(action.frame_range),
        "use_cyclic": action.use_cyclic,
        "slots": [
            {
                "identifier": slot.identifier,
                "target_id_type": slot.target_id_type,
            }
            for slot in getattr(action, "slots", [])
        ],
        "pose_markers": [
            {"name": marker.name, "frame": marker.frame}
            for marker in action.pose_markers
        ],
        "legacy_fcurves": sorted(
            curves, key=lambda item: (item["data_path"], item["array_index"])
        ),
        "layers": layers,
    }


def inspect(path: Path) -> dict:
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(path))
    mesh_objects = sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "MESH"),
        key=lambda obj: obj.name,
    )
    armature_objects = sorted(
        (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"),
        key=lambda obj: obj.name,
    )
    armatures = [obj.name for obj in armature_objects]
    action_payloads = [
        action_payload(action) for action in sorted(bpy.data.actions, key=lambda item: item.name)
    ]
    payload = {
        "meshes": [mesh_payload(obj) for obj in mesh_objects],
        "armatures": [armature_payload(obj) for obj in armature_objects],
        "actions": action_payloads,
    }
    canonical = json.dumps(
        payload,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return {
        "path": str(path),
        "sha256": hashlib.sha256(canonical).hexdigest().upper(),
        "mesh_count": len(mesh_objects),
        "vertices": sum(len(obj.data.vertices) for obj in mesh_objects),
        "polygons": sum(len(obj.data.polygons) for obj in mesh_objects),
        "materials": sorted(
            {
                material.name
                for obj in mesh_objects
                for material in obj.data.materials
                if material is not None
            }
        ),
        "armatures": armatures,
        "actions": [action["name"] for action in action_payloads],
    }


def main() -> None:
    try:
        separator = sys.argv.index("--")
        paths = [Path(arg).resolve() for arg in sys.argv[separator + 1 :]]
    except ValueError:
        paths = []
    if not paths:
        raise SystemExit("Expected one or more FBX paths after --")
    missing = [str(path) for path in paths if not path.is_file()]
    if missing:
        raise SystemExit("Missing FBX files: " + ", ".join(missing))

    results = [inspect(path) for path in paths]
    print("DXRP_FBX_SEMANTICS=" + json.dumps(results, sort_keys=True))

    hashes = {result["sha256"] for result in results}
    if len(paths) > 1 and len(hashes) != 1:
        raise SystemExit("Semantic FBX fingerprints differ")


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_FBX_SEMANTICS_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
