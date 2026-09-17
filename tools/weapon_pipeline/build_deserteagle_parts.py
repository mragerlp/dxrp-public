"""Split the licensed Desert Eagle source into rigid common-origin parts.

Run with Blender in background mode:

  blender --background --factory-startup --python build_deserteagle_parts.py -- \
    SOURCE_FBX OUTPUT_DIRECTORY

The archived source is read-only.  The output directory receives body, slide,
magazine, and complete-world FBXs.  Every export preserves the source object's
world transform, so the parts retain one shared origin for donor-driven prefab
assembly.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
from collections import OrderedDict
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


EXPECTED_MESHES = {f"{letter}_low" for letter in "abcdefghijklmnopqrs"}
EXPECTED_SOURCE_SHA256 = "08CB76FE16ED41BE8C4F710BBF4E95834EC6762FBFEE2C618480F0432DCDEC99"
EXPECTED_SEMANTIC_SHA256 = {
    "desert_eagle_body": "08C12ED8CEA2E7F613ED68548733C086E96761CC7E0DED7F3DFBF3AE39018999",
    "desert_eagle_slide": "F7AAAC64EF6B772434AA87691547C5D89D1B3B79FDD6930B2FC91D234E7630BC",
    "desert_eagle_magazine": "06A2EE14E320DDEEF03910921BBC1DCACF42C607B2E19E61532C3F59FDD1863E",
    "desert_eagle_world": "07AFBA72859AFD391EF20804CC084512ED7EADA70D344AFE59A79399DD1FEAD4",
}
SLIDE = {"b_low"}
MAGAZINE = {"i_low"}
BODY = EXPECTED_MESHES - SLIDE - MAGAZINE
GROUPS = OrderedDict(
    [
        ("desert_eagle_body", BODY),
        ("desert_eagle_slide", SLIDE),
        ("desert_eagle_magazine", MAGAZINE),
        ("desert_eagle_world", EXPECTED_MESHES),
    ]
)


def parse_paths() -> tuple[Path, Path]:
    try:
        separator = sys.argv.index("--")
        source, output_directory = sys.argv[separator + 1 : separator + 3]
    except (ValueError, IndexError):
        raise SystemExit("Expected arguments after --: SOURCE_FBX OUTPUT_DIRECTORY")

    source_path = Path(source).resolve()
    output_path = Path(output_directory).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Source FBX does not exist: {source_path}")
    source_sha256 = hashlib.sha256(source_path.read_bytes()).hexdigest().upper()
    if source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit(
            f"Source FBX SHA-256 mismatch: expected {EXPECTED_SOURCE_SHA256}, "
            f"got {source_sha256}"
        )
    derived_outputs = [output_path / f"{group_name}.fbx" for group_name in GROUPS]
    if any(
        source_path == candidate
        or (candidate.exists() and source_path.samefile(candidate))
        for candidate in derived_outputs
    ):
        raise SystemExit("Source and derived output paths must differ")
    return source_path, output_path


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


def bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(min(point[index] for point in points) for index in range(3))
    maximum = Vector(max(point[index] for point in points) for index in range(3))
    return minimum, maximum


def connected_component_count(obj: bpy.types.Object) -> int:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    remaining = set(mesh.verts)
    count = 0
    while remaining:
        count += 1
        pending = [remaining.pop()]
        while pending:
            vertex = pending.pop()
            for edge in vertex.link_edges:
                neighbour = edge.other_vert(vertex)
                if neighbour in remaining:
                    remaining.remove(neighbour)
                    pending.append(neighbour)
    mesh.free()
    return count


def validate_magazine(meshes: dict[str, bpy.types.Object]) -> dict[str, object]:
    magazine = meshes["i_low"]
    grip = meshes["h_low"]

    mesh = bmesh.new()
    mesh.from_mesh(magazine.data)
    non_manifold_edges = sum(1 for edge in mesh.edges if not edge.is_manifold)
    mesh.free()

    component_count = connected_component_count(magazine)
    magazine_min, magazine_max = bounds(magazine)
    grip_min, grip_max = bounds(grip)

    nested_in_grip_width = (
        magazine_min.x >= grip_min.x and magazine_max.x <= grip_max.x
    )
    spans_magazine_well = (
        magazine_min.z < grip_min.z and magazine_max.z > grip_max.z
    )
    if non_manifold_edges or component_count != 1:
        raise RuntimeError(
            "i_low is no longer one closed manifold mesh; refusing the magazine split"
        )
    if not nested_in_grip_width or not spans_magazine_well:
        raise RuntimeError(
            "i_low no longer has the source magazine relationship to h_low; refusing split"
        )

    return {
        "object": magazine.name,
        "vertices": len(magazine.data.vertices),
        "polygons": len(magazine.data.polygons),
        "connected_components": component_count,
        "non_manifold_edges": non_manifold_edges,
        "bounds": {
            "min": list(magazine_min),
            "max": list(magazine_max),
        },
        "nested_in_grip_width": nested_in_grip_width,
        "spans_magazine_well": spans_magazine_well,
    }


def export_group(
    output: Path,
    group_name: str,
    members: set[str],
    meshes: dict[str, bpy.types.Object],
) -> Path:
    bpy.ops.object.select_all(action="DESELECT")
    selected = [meshes[name] for name in sorted(members)]
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = selected[0]

    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=False,
        add_leaf_bones=False,
        use_tspace=True,
        use_triangles=False,
    )
    return output


def main() -> None:
    source_path, output_directory = parse_paths()
    pipeline_io = load_pipeline_io()
    output_directory = pipeline_io.assert_temp_destination(output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)
    outputs = {
        name: output_directory / f"{name}.fbx" for name in GROUPS
    }
    staged_outputs = {
        name: pipeline_io.staging_path(path) for name, path in outputs.items()
    }
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source_path))
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    meshes = {
        obj.name: obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
    }
    imported = set(meshes)
    if imported != EXPECTED_MESHES:
        raise RuntimeError(
            json.dumps(
                {
                    "missing_expected_meshes": sorted(EXPECTED_MESHES - imported),
                    "unexpected_meshes": sorted(imported - EXPECTED_MESHES),
                }
            )
        )

    source_parents = {
        obj.parent.name if obj.parent is not None else None for obj in meshes.values()
    }
    if source_parents != {"desert_eagle_low"}:
        raise RuntimeError(
            "Source meshes no longer share the desert_eagle_low parent coordinate space"
        )

    magazine_evidence = validate_magazine(meshes)
    exported = {
        name: export_group(staged_outputs[name], name, members, meshes)
        for name, members in GROUPS.items()
    }
    inspector = load_semantic_inspector()
    semantic_results = {}
    for name, path in exported.items():
        inspected = inspector.inspect(path)
        expected = EXPECTED_SEMANTIC_SHA256[name]
        if inspected["sha256"] != expected:
            raise RuntimeError(
                f"Semantic FBX mismatch for {name}: expected {expected}, "
                f"got {inspected['sha256']}"
            )
        semantic_results[name] = inspected

    pipeline_io.promote_files(
        (staged_outputs[name], outputs[name]) for name in GROUPS
    )
    for name, result in semantic_results.items():
        result["path"] = str(outputs[name])

    result = {
        "source": str(source_path),
        "source_sha256": EXPECTED_SOURCE_SHA256,
        "output_directory": str(output_directory),
        "groups": {name: sorted(members) for name, members in GROUPS.items()},
        "outputs": {name: str(path) for name, path in outputs.items()},
        "magazine_evidence": magazine_evidence,
        "semantic_results": semantic_results,
        "shared_source_coordinate_space": True,
    }
    print("DXRP_DESERTEAGLE_BUILD=" + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_DESERTEAGLE_BUILD_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
