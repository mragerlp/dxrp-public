"""Build deterministic, local-only M1911 mechanical graybox FBXs for DXRP.

Run with Blender in background mode:
  blender --background --factory-startup --python build_m1911_graybox.py -- SOURCE OUTPUT_DIR

The licensed/provenance-pending source is never modified.  The script reads the
rigged FBX only as a geometry manifest, separates rigid parts by disconnected
islands plus their existing material/weight labels, bakes real-world inch scale
and Source 2 axes, removes skinning/animation, and preserves one shared origin.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import sys
import datetime
from collections import Counter, defaultdict
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from io_scene_fbx import export_fbx_bin, fbx_utils


EXPECTED_SOURCE_SHA256 = (
    "0F4F3E1746ED5F77945F0A571A66511A1125CEF650B12707D7D967EF6FC99B5F"
)
EXPECTED_MATERIALS = ["Grip", "Slide", "Frame", "Barrel", "Ammo", "Mag"]
EXPECTED_VERTEX_GROUPS = [
    "Gun",
    "Trigger",
    "Hammer",
    "MagRelease",
    "SlideRelease",
    "Safety",
    "Mag",
    "Bullet",
    "Follower",
]
EXPECTED_SOURCE_COUNTS = (6089, 13474, 7412)
EXPECTED_OUTPUT = {
    "m1911_body": (3938, 4556),
    "m1911_slide": (1058, 1412),
    "m1911_magazine": (1093, 1444),
    "m1911_world": (6089, 7412),
}
EXPECTED_SEMANTIC_SHA256 = {
    "m1911_body": "231EAF3A394828225F609D9CD2DD21D0178A3C54690B48560574E71F1ABDD1AD",
    "m1911_slide": "842E15F4775A7836AB723ADE25C263714049E4A2BD72074D4F70DC2A50592AB4",
    "m1911_magazine": "05940AA43F1C009C792547AAFE3F29928BF77662DD7F4385651D608EE13A6769",
    "m1911_world": "E395E2AA2B4279333AB05F5C8AF37C9A813A6515FFA8023C8863131B1899FEA2",
}

# Blender source is in metres.  FBX -> Source 2 currently maps one metre to
# 100 Source units.  This multiplier makes one Source unit equal one inch.
METRES_TO_SOURCE_INCH_SCALE = 0.3937008

# Source's FBX import maps Blender -Y to Source +X.  Rotating the source +90
# around Blender Z puts the pistol's original -X muzzle direction on -Y, so the
# compiled weapon points down Source +X.  Uniform scale is baked into geometry.
BAKE_TO_SOURCE = Matrix.Rotation(math.radians(90.0), 4, "Z") @ Matrix.Scale(
    METRES_TO_SOURCE_INCH_SCALE, 4
)

# Blender's FBX writer normally uses Python's process-randomized hash() for
# object IDs and the wall clock for the FBX header.  Neither affects geometry,
# but both make custody hashes change between identical builds.  Pin both at
# the exporter boundary so repeated builds of the same source are byte-stable.
FIXED_FBX_TIME = datetime.datetime(2026, 8, 25, 0, 0, 0)
_ORIGINAL_HEADER_ELEMENTS = export_fbx_bin.fbx_header_elements


def _stable_key_to_uuid(uuids: dict, key: object) -> fbx_utils.UUID:
    if isinstance(key, int) and 0 <= key < 2**63:
        uuid = key
    else:
        digest = hashlib.sha256(repr(key).encode("utf-8")).digest()
        uuid = int.from_bytes(digest[:8], "little") & ((1 << 63) - 1)

    if uuid > int(1e9):
        shorter = uuid % int(1e9)
        if shorter not in uuids:
            uuid = shorter
    if uuid in uuids:
        increment = 1 if uuid < 2**62 else -1
        while uuid in uuids:
            uuid += increment
    return fbx_utils.UUID(uuid)


def _fixed_header_elements(root, scene_data, time=None):
    return _ORIGINAL_HEADER_ELEMENTS(root, scene_data, FIXED_FBX_TIME)


fbx_utils._key_to_uuid = _stable_key_to_uuid
export_fbx_bin.fbx_header_elements = _fixed_header_elements


def parse_paths() -> tuple[Path, Path]:
    try:
        separator = sys.argv.index("--")
        source, output_dir = sys.argv[separator + 1 : separator + 3]
    except (ValueError, IndexError):
        raise SystemExit("Expected arguments after --: SOURCE_FBX OUTPUT_DIR")

    source_path = Path(source).resolve()
    output_path = Path(output_dir).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Source FBX does not exist: {source_path}")
    return source_path, output_path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def load_semantic_inspector():
    inspector_path = Path(__file__).with_name("inspect_fbx_semantics.py")
    spec = importlib.util.spec_from_file_location("dxrp_fbx_semantics", inspector_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load semantic inspector: {inspector_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_pipeline_io():
    module_path = Path(__file__).with_name("pipeline_io.py")
    spec = importlib.util.spec_from_file_location("dxrp_pipeline_io", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load pipeline I/O helpers: {module_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def component_manifest(source: bpy.types.Object) -> dict[str, set[int]]:
    mesh = source.data
    parent = list(range(len(mesh.vertices)))

    def find(index: int) -> int:
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(left: int, right: int) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for edge in mesh.edges:
        union(edge.vertices[0], edge.vertices[1])

    vertices_by_root: dict[int, list[int]] = defaultdict(list)
    faces_by_root: dict[int, list[int]] = defaultdict(list)
    for vertex in mesh.vertices:
        vertices_by_root[find(vertex.index)].append(vertex.index)
    for polygon in mesh.polygons:
        faces_by_root[find(polygon.vertices[0])].append(polygon.index)

    group_names = {group.index: group.name for group in source.vertex_groups}
    material_names = [material.name for material in mesh.materials]
    output: dict[str, set[int]] = {
        "m1911_body": set(),
        "m1911_slide": set(),
        "m1911_magazine": set(),
        "m1911_world": set(range(len(mesh.polygons))),
    }

    for root, vertex_indices in vertices_by_root.items():
        weights = {
            group_names[element.group]
            for vertex_index in vertex_indices
            for element in mesh.vertices[vertex_index].groups
            if element.weight > 0.999
        }
        materials = {
            material_names[mesh.polygons[face_index].material_index]
            for face_index in faces_by_root[root]
        }

        if weights <= {"Mag", "Bullet", "Follower"}:
            target = "m1911_magazine"
        elif materials == {"Slide"} and weights == {"Gun"}:
            target = "m1911_slide"
        else:
            target = "m1911_body"
        output[target].update(faces_by_root[root])

    return output


def build_part(
    source: bpy.types.Object, name: str, keep_faces: set[int]
) -> bpy.types.Object:
    mesh = source.data.copy()
    mesh.name = f"{name}_mesh"

    editable = bmesh.new()
    editable.from_mesh(mesh)
    editable.faces.ensure_lookup_table()
    removable_faces = [face for face in editable.faces if face.index not in keep_faces]
    if removable_faces:
        bmesh.ops.delete(editable, geom=removable_faces, context="FACES")

    used_vertices = {vertex for face in editable.faces for vertex in face.verts}
    removable_vertices = [vertex for vertex in editable.verts if vertex not in used_vertices]
    if removable_vertices:
        bmesh.ops.delete(editable, geom=removable_vertices, context="VERTS")

    editable.to_mesh(mesh)
    editable.free()
    mesh.transform(BAKE_TO_SOURCE @ source.matrix_world)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.matrix_world = Matrix.Identity(4)
    obj.animation_data_clear()

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.material_slot_remove_unused()
    obj.select_set(False)

    actual = (len(mesh.vertices), len(mesh.polygons))
    if actual != EXPECTED_OUTPUT[name]:
        raise RuntimeError(f"Unexpected {name} counts: {actual} != {EXPECTED_OUTPUT[name]}")
    return obj


def bounds(obj: bpy.types.Object) -> dict[str, list[float]]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    return {
        "min": minimum,
        "max": maximum,
        "size": [maximum[index] - minimum[index] for index in range(3)],
    }


def export_part(obj: bpy.types.Object, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        # Keep Blender's metre-to-FBX-centimetre conversion in the payload.
        # Source 2 then imports the baked real-inch geometry at vmdl scale 1.
        apply_unit_scale=False,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=False,
        add_leaf_bones=False,
        # Source 2 derives tangents during compile. Blender's MikkTSpace export
        # can reorder equal tangent records on this multi-part world mesh, which
        # breaks byte-for-byte custody without changing geometry.
        use_tspace=False,
        use_triangles=True,
    )


def main() -> None:
    source_path, output_dir = parse_paths()
    pipeline_io = load_pipeline_io()
    output_dir = pipeline_io.assert_temp_destination(output_dir)
    source_hash = sha256(source_path)
    if source_hash != EXPECTED_SOURCE_SHA256:
        raise RuntimeError(
            f"M1911 source custody mismatch: {source_hash} != {EXPECTED_SOURCE_SHA256}"
        )
    derived_paths = {
        name: output_dir / f"{name}.fbx" for name in EXPECTED_OUTPUT
    }
    if any(
        source_path == path or (path.exists() and source_path.samefile(path))
        for path in derived_paths.values()
    ):
        raise RuntimeError("M1911 source and derived output paths must differ")
    output_dir.mkdir(parents=True, exist_ok=True)
    staged_paths = {
        name: pipeline_io.staging_path(path) for name, path in derived_paths.items()
    }

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source_path))
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1 or len(armatures) != 1:
        raise RuntimeError(
            f"Expected one mesh and one armature, got {len(meshes)} / {len(armatures)}"
        )

    source = meshes[0]
    source_counts = (
        len(source.data.vertices),
        len(source.data.edges),
        len(source.data.polygons),
    )
    if source_counts != EXPECTED_SOURCE_COUNTS:
        raise RuntimeError(
            f"Unexpected source mesh counts: {source_counts} != {EXPECTED_SOURCE_COUNTS}"
        )
    if [material.name for material in source.data.materials] != EXPECTED_MATERIALS:
        raise RuntimeError("M1911 material manifest changed")
    if [group.name for group in source.vertex_groups] != EXPECTED_VERTEX_GROUPS:
        raise RuntimeError("M1911 vertex-group manifest changed")
    if bpy.data.actions:
        raise RuntimeError("M1911 source unexpectedly contains animation actions")

    face_manifest = component_manifest(source)
    if set().union(
        face_manifest["m1911_body"],
        face_manifest["m1911_slide"],
        face_manifest["m1911_magazine"],
    ) != face_manifest["m1911_world"]:
        raise RuntimeError("M1911 semantic split does not cover the world mesh")
    if (
        face_manifest["m1911_body"] & face_manifest["m1911_slide"]
        or face_manifest["m1911_body"] & face_manifest["m1911_magazine"]
        or face_manifest["m1911_slide"] & face_manifest["m1911_magazine"]
    ):
        raise RuntimeError("M1911 semantic split overlaps")

    parts = {
        name: build_part(source, name, faces)
        for name, faces in face_manifest.items()
    }
    for original in list(bpy.context.scene.objects):
        if original not in parts.values():
            bpy.data.objects.remove(original, do_unlink=True)

    for name, obj in parts.items():
        export_part(obj, staged_paths[name])

    part_results = {
        name: {
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "materials": [
                material.name for material in obj.data.materials if material is not None
            ],
            "bounds_meters_before_fbx_export": bounds(obj),
            "sha256": sha256(staged_paths[name]),
        }
        for name, obj in parts.items()
    }

    inspector = load_semantic_inspector()
    semantic_results = {}
    for name, path in staged_paths.items():
        inspected = inspector.inspect(path)
        expected = EXPECTED_SEMANTIC_SHA256[name]
        if inspected["sha256"] != expected:
            raise RuntimeError(
                f"Semantic FBX mismatch for {name}: expected {expected}, "
                f"got {inspected['sha256']}"
            )
        semantic_results[name] = inspected

    pipeline_io.promote_files(
        (staged_paths[name], derived_paths[name]) for name in EXPECTED_OUTPUT
    )
    for name, result in semantic_results.items():
        result["path"] = str(derived_paths[name])

    result = {
        "source": str(source_path),
        "source_sha256": source_hash,
        "output_dir": str(output_dir),
        "orientation": "muzzle +X in Source 2",
        "scale": "one Source unit per real-world inch",
        "parts": part_results,
        "semantic_results": semantic_results,
    }
    print("DXRP_M1911_BUILD=" + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_M1911_BUILD_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
