"""Build and round-trip verify the assembled Custom Pistol 9mm semantic rig.

The source FBX is a presentation board containing repeated pistol copies and
loose attachments.  This builder keeps one source-authored assembled copy,
its inserted magazine, and the aligned first suppressor.  It does not rescale,
reposition, merge, or deform source geometry.
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


EXPECTED_SOURCE_SHA256 = "999224a780adc82891abebcf13a33b3a6ff6e3887dfcbf06e51e1fc7dc3bcb3d"
ROUND_DIGITS = 5
POSITION_TOLERANCE = 0.0001

PISTOL_NAMES = ["CustomPistol9mm", *[f"CustomPistol9mm.{index:03d}" for index in range(1, 23)]]
SLIDE_NAMES = {
    "CustomPistol9mm",
    "CustomPistol9mm.006",
    "CustomPistol9mm.007",
    "CustomPistol9mm.009",
    "CustomPistol9mm.012",
    "CustomPistol9mm.018",
}
TRIGGER_NAMES = {"CustomPistol9mm.016", "CustomPistol9mm.017"}
MAGAZINE_NAMES = {"Magazine9mm", "Magazine9mm.001", "Magazine9mm.002", "Magazine9mm.003"}
SUPPRESSOR_NAMES = {"Supressor1", "Supressor1.001"}
EXPECTED_SOURCE_NAMES = set(PISTOL_NAMES) | MAGAZINE_NAMES | SUPPRESSOR_NAMES
EXPECTED_BONES = {"weapon_root", "slide", "trigger", "magazine", "suppressor"}
MATERIAL_RENAMES = {
    "Magazine9mm": "Magazin_Bullet_Magazine",
    "Supressor1": "SupressorVer1_Material",
}


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
    result = {}
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


def source_bone(name: str) -> str:
    if name in SLIDE_NAMES:
        return "slide"
    if name in TRIGGER_NAMES:
        return "trigger"
    if name in MAGAZINE_NAMES:
        return "magazine"
    if name in SUPPRESSOR_NAMES:
        return "suppressor"
    return "weapon_root"


def validate_source(source: Path) -> list[bpy.types.Object]:
    actual_hash = sha256(source)
    if actual_hash != EXPECTED_SOURCE_SHA256:
        fail(f"source hash mismatch: expected {EXPECTED_SOURCE_SHA256}, got {actual_hash}")

    bpy.ops.import_scene.fbx(filepath=str(source))
    objects = {obj.name: obj for obj in bpy.context.scene.objects if obj.type == "MESH"}
    missing = sorted(EXPECTED_SOURCE_NAMES - set(objects))
    if missing:
        fail(f"assembled source parts are missing: {missing}")

    selected = [objects[name] for name in sorted(EXPECTED_SOURCE_NAMES)]
    if len(selected) != 29 or any(len(obj.data.vertices) == 0 for obj in selected):
        fail("assembled source selection is incomplete")
    if {obj.data.materials[0].name for obj in selected if obj.data.materials} != {
        "CustomPistol9mm",
        "Magazine9mm",
        "Supressor1",
    }:
        fail("assembled source material set changed")
    return selected


def isolate_and_rename(selected: list[bpy.types.Object]) -> tuple[list[bpy.types.Object], dict[str, str]]:
    selected_set = set(selected)
    for obj in list(bpy.context.scene.objects):
        if obj not in selected_set:
            bpy.data.objects.remove(obj, do_unlink=True)

    counters = {name: 0 for name in EXPECTED_BONES}
    part_bones = {}
    renamed = []
    for obj in sorted(selected, key=lambda item: item.name):
        world = obj.matrix_world.copy()
        bone = source_bone(obj.name)
        index = counters[bone]
        counters[bone] += 1
        new_name = bone if index == 0 else f"{bone}_detail_{index:02d}"
        obj.parent = None
        obj.matrix_world = world
        obj.name = new_name
        obj.data.name = f"{new_name}_mesh"
        part_bones[new_name] = bone
        renamed.append(obj)

    for source_name, output_name in MATERIAL_RENAMES.items():
        source_material = bpy.data.materials.get(source_name)
        if source_material is None:
            fail(f"missing source material '{source_name}'")
        source_material.name = output_name
    return renamed, part_bones


def average_origin(parts: list[bpy.types.Object]) -> Vector:
    return sum((obj.matrix_world.translation for obj in parts), Vector()) / len(parts)


def create_armature(parts: list[bpy.types.Object], part_bones: dict[str, str]) -> bpy.types.Object:
    by_bone = {
        bone: [obj for obj in parts if part_bones[obj.name] == bone]
        for bone in EXPECTED_BONES
    }
    forward = average_origin(by_bone["suppressor"]) - average_origin(by_bone["weapon_root"])
    if forward.length == 0 or not all(math.isfinite(component) for component in forward):
        fail("could not derive a finite weapon forward axis")
    forward.normalize()

    armature_data = bpy.data.armatures.new("custom_pistol_9mm_semantic_skeleton")
    armature = bpy.data.objects.new("custom_pistol_9mm_semantic_rig", armature_data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    root = armature_data.edit_bones.new("weapon_root")
    root.head = average_origin(by_bone["weapon_root"])
    root.tail = root.head + forward * 0.02
    root.use_deform = True
    for name in ("slide", "trigger", "magazine", "suppressor"):
        child = armature_data.edit_bones.new(name)
        child.head = average_origin(by_bone[name])
        child.tail = child.head + forward * 0.01
        child.parent = root
        child.use_connect = False
        child.use_deform = True

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def bind_parts(parts: list[bpy.types.Object], armature: bpy.types.Object, part_bones: dict[str, str]) -> None:
    for obj in parts:
        bone = part_bones[obj.name]
        obj.vertex_groups.clear()
        group = obj.vertex_groups.new(name=bone)
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


def directed_vertex_delta(source, target) -> float:
    tree = KDTree(len(target))
    for index, point in enumerate(target):
        tree.insert(Vector(point), index)
    tree.balance()
    return max((tree.find(Vector(point))[2] for point in source), default=0.0)


def max_vertex_delta(before, after) -> float:
    if len(before) != len(after):
        return math.inf
    return max(directed_vertex_delta(before, after), directed_vertex_delta(after, before))


def verify_round_trip(output: Path, expected_snapshot: dict[str, dict], part_bones: dict[str, str]) -> dict:
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(output))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    extras = [obj.name for obj in bpy.context.scene.objects if obj.type not in {"ARMATURE", "MESH"}]
    if len(armatures) != 1 or extras:
        fail(f"round trip structure changed: armatures={len(armatures)}, extras={sorted(extras)}")

    bones = {bone.name: bone for bone in armatures[0].data.bones}
    if set(bones) != EXPECTED_BONES or bones["weapon_root"].parent is not None:
        fail(f"round-trip skeleton changed: {sorted(bones)}")
    for name in EXPECTED_BONES - {"weapon_root"}:
        if bones[name].parent is None or bones[name].parent.name != "weapon_root":
            fail(f"bone '{name}' is not a direct child of weapon_root")

    after_snapshot = mesh_snapshot(meshes)
    if set(after_snapshot) != set(expected_snapshot):
        fail("round-trip mesh names changed")

    largest_delta = 0.0
    for obj in meshes:
        expected_bone = part_bones[obj.name]
        groups = {group.name for group in obj.vertex_groups}
        if groups != {expected_bone}:
            fail(f"mesh '{obj.name}' has vertex groups {sorted(groups)}")
        group_index = obj.vertex_groups[expected_bone].index
        for vertex in obj.data.vertices:
            weights = [element.weight for element in vertex.groups if element.group == group_index]
            if len(weights) != 1 or abs(weights[0] - 1.0) > 0.000001:
                fail(f"mesh '{obj.name}' is not rigidly weighted")

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
        "bones": sorted(bones),
        "mesh_count": len(meshes),
        "materials": sorted({material for row in after_snapshot.values() for material in row["materials"] if material}),
        "max_bind_pose_vertex_delta": largest_delta,
    }


def main() -> None:
    try:
        separator = sys.argv.index("--")
        source = Path(sys.argv[separator + 1]).resolve()
        output = Path(sys.argv[separator + 2]).resolve()
    except (ValueError, IndexError) as exc:
        raise SystemExit("Expected SOURCE_FBX and OUTPUT_FBX after --") from exc
    if not source.is_file() or source == output:
        raise SystemExit("Source must exist and output must not overwrite it")

    clear_scene()
    selected = validate_source(source)
    parts, part_bones = isolate_and_rename(selected)
    armature = create_armature(parts, part_bones)
    bind_parts(parts, armature, part_bones)
    before = mesh_snapshot(parts)
    export_fbx(output, parts, armature)
    verification = verify_round_trip(output, before, part_bones)
    print(
        "DXRP_CUSTOM_PISTOL_9MM_RIG="
        + json.dumps(
            {
                "source": str(source),
                "source_sha256": sha256(source),
                "output": str(output),
                "output_sha256": sha256(output),
                "output_bytes": output.stat().st_size,
                **verification,
            },
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_CUSTOM_PISTOL_9MM_RIG_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
