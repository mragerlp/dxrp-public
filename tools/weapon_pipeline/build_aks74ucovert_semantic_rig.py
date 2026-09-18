"""Build and verify the first semantic rig for the AKS-74U Covert intake.

Run with Blender so the same FBX importer/exporter is used for both sides of
the bind-pose comparison:

  blender --background --factory-startup \
    --python build_aks74ucovert_semantic_rig.py -- SOURCE_FBX OUTPUT_FBX

The input archive contains several presentation groups.  This builder keeps
only the complete suppressed weapon under the exact ``Empty`` parent.  It
does not rescale, reposition, merge, or deform the source meshes.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


EXPECTED_SOURCE_SHA256 = "2066a418f4c12c65c3d2d243a6981afdb7a1b285c25082fbf72cff145cc48b26"
SOURCE_PARENT = "Empty"
ROUND_DIGITS = 5
POSITION_TOLERANCE = 0.0001

SOURCE_PARTS = {
    "barrel": "barrel",
    "bolt carrier": "bolt",
    "dust cover rear sight": "dust_cover",
    "hand guard bottom": "handguard_bottom",
    "hand guard top": "handguard_top",
    "magazine.001": "magazine",
    "pbs04 suppressor": "suppressor",
    "pistol grip": "pistol_grip",
    "receiver": "receiver",
    "safety switch lever": "safety",
    "stock": "stock",
    "trigger": "trigger",
}

PART_BONES = {
    "barrel": "weapon_root",
    "bolt": "bolt",
    "dust_cover": "weapon_root",
    "handguard_bottom": "weapon_root",
    "handguard_top": "weapon_root",
    "magazine": "magazine",
    "suppressor": "suppressor",
    "pistol_grip": "weapon_root",
    "receiver": "weapon_root",
    "safety": "safety",
    "stock": "stock",
    "trigger": "trigger",
}

CHILD_BONES = ("bolt", "magazine", "suppressor", "safety", "stock", "trigger")
EXPECTED_BONES = ("weapon_root", *CHILD_BONES)


def fail(message: str) -> None:
    raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.armatures,
        bpy.data.actions,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            collection.remove(block)


def rounded_vector(value) -> tuple[float, ...]:
    return tuple(round(float(component), ROUND_DIGITS) for component in value)


def world_vertices(obj: bpy.types.Object) -> list[tuple[float, float, float]]:
    return sorted(rounded_vector(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)


def mesh_snapshot(objects: list[bpy.types.Object]) -> dict[str, dict]:
    result: dict[str, dict] = {}
    for obj in sorted(objects, key=lambda item: item.name):
        mesh = obj.data
        mesh.update()
        result[obj.name] = {
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "loops": len(mesh.loops),
            "world_vertices": world_vertices(obj),
            "materials": [material.name if material else "" for material in mesh.materials],
            "uv_layers": {layer.name: len(layer.data) for layer in mesh.uv_layers},
        }
    return result


def validate_source(source: Path) -> list[bpy.types.Object]:
    actual_hash = sha256(source)
    if actual_hash != EXPECTED_SOURCE_SHA256:
        fail(f"source hash mismatch: expected {EXPECTED_SOURCE_SHA256}, got {actual_hash}")

    bpy.ops.import_scene.fbx(filepath=str(source))
    parent = bpy.data.objects.get(SOURCE_PARENT)
    if parent is None or parent.type != "EMPTY":
        fail(f"missing exact source parent '{SOURCE_PARENT}'")

    direct_mesh_children = {child.name: child for child in parent.children if child.type == "MESH"}
    expected_names = set(SOURCE_PARTS)
    if set(direct_mesh_children) != expected_names:
        missing = sorted(expected_names - set(direct_mesh_children))
        extra = sorted(set(direct_mesh_children) - expected_names)
        fail(f"source part set changed; missing={missing}, extra={extra}")

    selected = [direct_mesh_children[name] for name in SOURCE_PARTS]
    if any(len(obj.data.vertices) == 0 for obj in selected):
        fail("a selected source part has no vertices")
    return selected


def isolate_and_rename(selected: list[bpy.types.Object]) -> list[bpy.types.Object]:
    selected_set = set(selected)
    for obj in list(bpy.context.scene.objects):
        if obj not in selected_set:
            bpy.data.objects.remove(obj, do_unlink=True)

    renamed = []
    for obj in selected:
        world = obj.matrix_world.copy()
        source_name = obj.name
        obj.parent = None
        obj.matrix_world = world
        obj.name = SOURCE_PARTS[source_name]
        obj.data.name = f"{obj.name}_mesh"
        renamed.append(obj)

    if {obj.name for obj in renamed} != set(PART_BONES):
        fail("isolated mesh names do not match the semantic part map")
    return renamed


def create_armature(parts: list[bpy.types.Object]) -> bpy.types.Object:
    by_name = {obj.name: obj for obj in parts}
    root_head = by_name["receiver"].matrix_world.translation.copy()
    forward = (
        by_name["suppressor"].matrix_world.translation
        - by_name["stock"].matrix_world.translation
    ).normalized()
    if not all(math.isfinite(component) for component in forward) or forward.length < 0.99:
        fail("could not derive a finite weapon forward axis")

    armature_data = bpy.data.armatures.new("aks74ucovert_semantic_skeleton")
    armature = bpy.data.objects.new("aks74ucovert_semantic_rig", armature_data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    root = armature_data.edit_bones.new("weapon_root")
    root.head = root_head
    root.tail = root_head + forward * 0.5
    root.use_deform = True

    for name in CHILD_BONES:
        child = armature_data.edit_bones.new(name)
        child.head = by_name[name].matrix_world.translation.copy()
        child.tail = child.head + forward * 0.2
        child.parent = root
        child.use_connect = False
        child.use_deform = True

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def bind_parts(parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    for obj in parts:
        target_bone = PART_BONES[obj.name]
        obj.vertex_groups.clear()
        group = obj.vertex_groups.new(name=target_bone)
        group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")

        for modifier in list(obj.modifiers):
            obj.modifiers.remove(modifier)
        modifier = obj.modifiers.new(name="Semantic Rig", type="ARMATURE")
        modifier.object = armature
        modifier.use_vertex_groups = True
        modifier.use_bone_envelopes = False

        world = obj.matrix_world.copy()
        obj.parent = armature
        obj.parent_type = "OBJECT"
        obj.matrix_parent_inverse = armature.matrix_world.inverted()
        obj.matrix_world = world


def export_fbx(output: Path, parts: list[bpy.types.Object], armature: bpy.types.Object) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature

    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="ROOT",
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    if "FINISHED" not in result or not output.is_file() or output.stat().st_size == 0:
        fail(f"FBX export failed: {sorted(result)}")


def directed_vertex_delta(
    source: list[tuple[float, ...]],
    target: list[tuple[float, ...]],
) -> float:
    tree = KDTree(len(target))
    for index, point in enumerate(target):
        tree.insert(Vector(point), index)
    tree.balance()
    return max((tree.find(Vector(point))[2] for point in source), default=0.0)


def max_vertex_delta(before: list[tuple[float, ...]], after: list[tuple[float, ...]]) -> float:
    if len(before) != len(after):
        return math.inf
    return max(
        directed_vertex_delta(before, after),
        directed_vertex_delta(after, before),
    )


def verify_round_trip(output: Path, expected_snapshot: dict[str, dict]) -> dict:
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(output))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    extras = [obj.name for obj in bpy.context.scene.objects if obj.type not in {"ARMATURE", "MESH"}]
    if len(armatures) != 1:
        fail(f"round trip expected one armature, found {len(armatures)}")
    if extras:
        fail(f"round trip contains non-rig objects: {sorted(extras)}")

    armature = armatures[0]
    bones = {bone.name: bone for bone in armature.data.bones}
    if set(bones) != set(EXPECTED_BONES):
        fail(f"round-trip bones changed: {sorted(bones)}")
    if bones["weapon_root"].parent is not None:
        fail("weapon_root is not the unique skeleton root")
    for name in CHILD_BONES:
        parent = bones[name].parent
        if parent is None or parent.name != "weapon_root":
            fail(f"bone '{name}' is not a direct child of weapon_root")

    after_snapshot = mesh_snapshot(meshes)
    if set(after_snapshot) != set(expected_snapshot):
        fail(
            "round-trip mesh names changed; "
            f"expected={sorted(expected_snapshot)}, got={sorted(after_snapshot)}"
        )

    largest_delta = 0.0
    for obj in meshes:
        expected_bone = PART_BONES[obj.name]
        group_names = {group.name for group in obj.vertex_groups}
        if group_names != {expected_bone}:
            fail(f"mesh '{obj.name}' has vertex groups {sorted(group_names)}")
        group_index = obj.vertex_groups[expected_bone].index
        for vertex in obj.data.vertices:
            weights = [element.weight for element in vertex.groups if element.group == group_index]
            if len(weights) != 1 or abs(weights[0] - 1.0) > 0.000001:
                fail(f"mesh '{obj.name}' has a vertex not rigidly weighted to '{expected_bone}'")

        before = expected_snapshot[obj.name]
        after = after_snapshot[obj.name]
        for field in ("vertices", "polygons", "loops", "materials", "uv_layers"):
            if before[field] != after[field]:
                fail(f"mesh '{obj.name}' changed {field} across the FBX round trip")
        delta = max_vertex_delta(before["world_vertices"], after["world_vertices"])
        if not math.isfinite(delta) or delta > POSITION_TOLERANCE:
            fail(f"mesh '{obj.name}' moved by {delta:.8f} across the FBX round trip")
        largest_delta = max(largest_delta, delta)

    return {
        "armature": armature.name,
        "bones": sorted(bones),
        "meshes": sorted(after_snapshot),
        "materials": sorted(
            {
                material
                for snapshot in after_snapshot.values()
                for material in snapshot["materials"]
                if material
            }
        ),
        "max_bind_pose_vertex_delta": largest_delta,
    }


def main() -> None:
    try:
        separator = sys.argv.index("--")
        source = Path(sys.argv[separator + 1]).resolve()
        output = Path(sys.argv[separator + 2]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit("Expected SOURCE_FBX and OUTPUT_FBX after --") from exc

    if not source.is_file():
        raise SystemExit(f"Source FBX does not exist: {source}")
    if source == output:
        raise SystemExit("Output FBX must not overwrite the source FBX")

    clear_scene()
    selected = validate_source(source)
    parts = isolate_and_rename(selected)
    armature = create_armature(parts)
    bind_parts(parts, armature)
    before = mesh_snapshot(parts)
    export_fbx(output, parts, armature)
    verification = verify_round_trip(output, before)

    payload = {
        "source": str(source),
        "source_sha256": sha256(source),
        "output": str(output),
        "output_sha256": sha256(output),
        "output_bytes": output.stat().st_size,
        **verification,
    }
    print("DXRP_AKS74UCOVERT_RIG=" + json.dumps(payload, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_AKS74UCOVERT_RIG_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
