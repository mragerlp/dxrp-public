"""Build the provenance-isolated, modular AR-15 FBX used by DXRP.

Run with Blender in background mode:
  blender --background --factory-startup --python build_ar15_native.py -- SOURCE OUTPUT

The source is never modified.  Every retained part is baked into one shared
origin before semantic groups are joined, which keeps the assembled weapon
coincident while allowing ModelDoc to import moving parts independently.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import sys
from collections import OrderedDict
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


GROUPS = OrderedDict(
    [
        (
            "ar15_body",
            [
                "ar15_17_low001",
                "ar15_01_low001",
                "ar15_02_low001",
                "ar15_08_low001",
                "ar15_44_low001",
                "ar15_49_low001",
                "ar15_11_low001",
                "ar15_64_low001",
                "ar15_66_low001",
                "dd_03_low001",
            ],
        ),
        ("ar15_stock", ["ar15_20_low001"]),
        ("ar15_trigger", ["ar15_14_low001"]),
        ("ar15_magazine", ["ar15_30_low001", "ar15_29_low002"]),
        ("ar15_bolt_flap", ["ar15_25_low001"]),
        ("ar15_bolt", ["ar15_58_low001"]),
        (
            "ar15_charging_handle",
            ["ar15_50_low001", "ar15_51_low001", "ar15_52_low001"],
        ),
    ]
)

EXCLUDED_LOOSE_ROUNDS = {
    "Bullet_01_low001",
    "Bullet_02_low001",
    "Bullet_01_low002",
    "Bullet_02_low002",
}

NORMALIZE_TO_SOURCE_AXES = Matrix.Rotation(math.radians(-90.0), 4, "Z")
EXPECTED_SOURCE_SHA256 = "A48236F6B53F742F4F8031701FFE0CDE5787A4D7556AACB08D02708DDA6B51B8"
EXPECTED_SEMANTIC_SHA256 = "AC858B89980BB379EBD3CE045B55FB47D90909B7BA6C9094A3D91345F97760DE"
EXPECTED_SOURCE_UV_LAYER = "UVChannel_1"


def parse_paths() -> tuple[Path, Path]:
    try:
        separator = sys.argv.index("--")
        source, output = sys.argv[separator + 1 : separator + 3]
    except (ValueError, IndexError):
        raise SystemExit("Expected arguments after --: SOURCE_FBX OUTPUT_FBX")

    source_path = Path(source).resolve()
    output_path = Path(output).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Source FBX does not exist: {source_path}")
    source_sha256 = hashlib.sha256(source_path.read_bytes()).hexdigest().upper()
    if source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit(
            f"Source FBX SHA-256 mismatch: expected {EXPECTED_SOURCE_SHA256}, "
            f"got {source_sha256}"
        )
    if source_path == output_path or (
        output_path.exists() and source_path.samefile(output_path)
    ):
        raise SystemExit("Source and output paths must differ")
    return source_path, output_path


def load_semantic_inspector():
    inspector_path = Path(__file__).with_name("inspect_fbx_semantics.py")
    spec = importlib.util.spec_from_file_location("dxrp_fbx_semantics", inspector_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load semantic inspector: {inspector_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_uv_validator():
    validator_path = Path(__file__).with_name("validate_ar15_uv_orientation.py")
    spec = importlib.util.spec_from_file_location("dxrp_ar15_uv_orientation", validator_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load AR-15 UV validator: {validator_path}")
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


def bake_to_shared_origin(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        raise RuntimeError(f"Expected mesh object, got {obj.name}: {obj.type}")
    obj.animation_data_clear()
    obj.data = obj.data.copy()
    obj.data.transform(NORMALIZE_TO_SOURCE_AXES @ obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    obj.parent = None


def correct_source_uv_orientation(obj: bpy.types.Object) -> int:
    """Restore the vertical UV orientation shown by the intact Fab model."""

    layers = list(obj.data.uv_layers)
    if len(layers) != 1 or layers[0].name != EXPECTED_SOURCE_UV_LAYER:
        raise RuntimeError(
            f"Unexpected UV layers on {obj.name}: "
            f"expected [{EXPECTED_SOURCE_UV_LAYER!r}], got {[layer.name for layer in layers]!r}"
        )
    for datum in layers[0].data:
        datum.uv.y = 1.0 - datum.uv.y
    return len(layers[0].data)


def join_group(name: str, members: list[str]) -> bpy.types.Object:
    objects = [bpy.data.objects[member] for member in members]
    for obj in objects:
        bake_to_shared_origin(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = f"{name}_mesh"
    joined.matrix_world = Matrix.Identity(4)
    return joined


def aggregate_bounds(objects: list[bpy.types.Object]) -> dict[str, list[float]]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = [min(point[i] for point in points) for i in range(3)]
    maximum = [max(point[i] for point in points) for i in range(3)]
    return {
        "min": minimum,
        "max": maximum,
        "size": [maximum[i] - minimum[i] for i in range(3)],
    }


def main() -> None:
    source_path, output_path = parse_paths()
    pipeline_io = load_pipeline_io()
    output_path = pipeline_io.assert_temp_destination(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    staged_output = pipeline_io.staging_path(output_path)
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source_path))
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    imported_meshes = {obj.name for obj in bpy.context.scene.objects if obj.type == "MESH"}
    expected = {member for members in GROUPS.values() for member in members}
    missing = sorted(expected - imported_meshes)
    unexpected = sorted(imported_meshes - expected - EXCLUDED_LOOSE_ROUNDS)
    if missing or unexpected:
        raise RuntimeError(
            json.dumps({"missing_expected_meshes": missing, "unexpected_meshes": unexpected})
        )

    corrected_uv_loops = sum(
        correct_source_uv_orientation(bpy.data.objects[name]) for name in sorted(expected)
    )

    retained = [join_group(name, members) for name, members in GROUPS.items()]

    for obj in list(bpy.context.scene.objects):
        if obj not in retained:
            bpy.data.objects.remove(obj, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in retained:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = retained[0]

    bpy.ops.export_scene.fbx(
        filepath=str(staged_output),
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="STRIP",
        embed_textures=False,
        bake_anim=False,
        add_leaf_bones=False,
        use_tspace=True,
        use_triangles=True,
    )

    result = {
        "source": str(source_path),
        "source_sha256": EXPECTED_SOURCE_SHA256,
        "output": str(output_path),
        "groups": [obj.name for obj in retained],
        "mesh_count": len(retained),
        "vertices": sum(len(obj.data.vertices) for obj in retained),
        "polygons": sum(len(obj.data.polygons) for obj in retained),
        "materials": sorted(
            {
                material.name
                for obj in retained
                for material in obj.data.materials
                if material is not None
            }
        ),
        "bounds_meters_before_fbx_export": aggregate_bounds(retained),
        "excluded_loose_rounds": sorted(EXCLUDED_LOOSE_ROUNDS),
        "uv_orientation_correction": {
            "axis": "V",
            "operation": "v = 1 - v",
            "source_layer": EXPECTED_SOURCE_UV_LAYER,
            "corrected_loop_count": corrected_uv_loops,
        },
    }

    inspected = load_semantic_inspector().inspect(staged_output)
    if inspected["sha256"] != EXPECTED_SEMANTIC_SHA256:
        raise RuntimeError(
            f"Semantic FBX mismatch: expected {EXPECTED_SEMANTIC_SHA256}, "
            f"got {inspected['sha256']}"
        )
    uv_validator = load_uv_validator()
    uv_sha256, uv_mesh_count, uv_loop_count = uv_validator.uv_fingerprint(staged_output)
    expected_uv_sha256 = uv_validator.EXPECTED_CORRECTED_UV_SHA256
    if uv_sha256 != expected_uv_sha256:
        raise RuntimeError(
            f"AR-15 UV orientation mismatch: expected {expected_uv_sha256}, "
            f"got {uv_sha256}"
        )
    result["uv_orientation_result"] = {
        "sha256": uv_sha256,
        "mesh_count": uv_mesh_count,
        "loop_count": uv_loop_count,
    }
    pipeline_io.promote_files([(staged_output, output_path)])
    inspected["path"] = str(output_path)
    result["semantic_result"] = inspected
    print("DXRP_AR15_BUILD=" + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_AR15_BUILD_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
