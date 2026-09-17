"""Build the provenance-isolated, modular SR-25 FBX used by DXRP.

Run with Blender in background mode:
  blender --background --factory-startup --python build_sr25_native.py -- \
    SOURCE_BLEND OUTPUT_FBX OUTPUT_TEXTURE_DIR

The native Blender source is opened read-only in memory and is never saved.
Every semantic part is baked into one shared origin before export.  The build
also splits the source ORM textures into explicit AO, roughness, and metalness
PNG channels for Source 2 materials.
"""

from __future__ import annotations

import hashlib
import datetime
import importlib.util
import json
import math
import os
import sys
from collections import OrderedDict
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


GROUPS = OrderedDict(
    [
        (
            "sr25_body",
            [
                "SR25",
                "barrel",
                "handguard",
                "muzzle",
                "pistolgrip",
                "reciever",
                "stock_tube",
            ],
        ),
        ("sr25_stock", ["stock"]),
        ("sr25_trigger", ["trigger"]),
        (
            "sr25_magazine",
            [
                "mag_ar10",
                "ammo_762x51_m62",
                "ammo_762x51_m62.001",
                "ammo_762x51_m62.002",
            ],
        ),
        ("sr25_mode_selector", ["selector"]),
        ("sr25_bolt_flap", ["window"]),
        ("sr25_bolt", ["bolt"]),
        ("sr25_charging_handle", ["charging_handle"]),
        ("sr25_scope", ["scope_30mm_24x"]),
        ("sr25_scope_mount", ["scope_mount"]),
        ("sr25_suppressor", ["silencer"]),
    ]
)

MATERIAL_TEXTURES = OrderedDict(
    [
        ("ammo_762x51_m62", ("ammo_762x51_m62_diff.png", "ammo_762x51_nrm.png", "ammo_762x51_m62_ORM.png")),
        ("barrel_ar10", ("barrel_diff.png", "barrel_nrm.png", "barrel_ORM.png")),
        ("body", ("body_diff.png", "body_nrm.png", "body_ORM.png")),
        ("charging_handle", ("charge_diff.png", "charge_nrm.png", "charge_ORM.png")),
        ("handguard_ar10", ("handguard_diff.png", "handguard_nrm.png", "handguard_ORM.png")),
        ("mag_ar10", ("mag_ar10_diff.png", "mag_ar10_nrm.png", "mag_ar10_ORM.png")),
        ("muzzle_ar10", ("muzzle_diff.png", "muzzle_nrm.png", "muzzle_ORM.png")),
        ("pistolgrip_ar15", ("pistolgrip_diff.png", "pistolgrip_nrm.png", "pistolgrip_ORM.png")),
        ("reciever_ar10", ("reciever_ar10_diff.png", "reciever_ar10_nrm.png", "reciever_ar10_ORM.png")),
        ("scope_30mm_24x", ("scope_30mm_diff.png", "scope_30mm_nrm.png", "scope_30mm_ORM.png")),
        ("scope_mount", ("scope_mount_diff.png", "scope_mount_nrm.png", "scope_mount_ORM.png")),
        ("silencer_ar10", ("silencer_diff.png", "silencer_nrm.png", "silencer_ORM.png")),
        ("stock_ar15", ("stock_diff.png", "stock_nrm.png", "stock_ORM.png")),
        ("stock_tube_ar15", ("stock_tube_diff.png", "stock_tube_nrm.png", "stock_tube_ORM.png")),
    ]
)

EXPECTED_SOURCE_MESH_COUNT = 20
EXPECTED_UNIQUE_MESH_COUNT = 18
EXPECTED_UNIQUE_VERTEX_COUNT = 16991
EXPECTED_TRIANGLE_COUNT = 32710
EXPECTED_SOURCE_BYTES = 75815175
EXPECTED_SOURCE_SHA256 = (
    "9936B46A24EAB5018EB9AF9F77D3279AA6E6C68ECA4AD04ECE8285F615F5BD42"
)
EXPECTED_SOURCE_TEXTURE_COUNT = 42
EXPECTED_SOURCE_TEXTURE_BYTES = 68653083
EXPECTED_SOURCE_TEXTURE_MANIFEST_SHA256 = (
    "0B4E72C886AC0BDC61CBF00E94787AA4367F08BC5E4A1E54E746AEFA4AA7E3B1"
)
EXPECTED_SEMANTIC_SHA256 = (
    "E1E7854F7445E4B6288DD3EB9AA99D2EF8F53D61A66BD09707D8EB74A5C47304"
)

# Native Blender coordinates are metres, +Z up, and the rifle points down -Y.
# A +90 degree Z rotation puts the muzzle on Source's +X forward axis.
NORMALIZE_TO_SOURCE_AXES = Matrix.Rotation(math.radians(90.0), 4, "Z")


def parse_paths() -> tuple[Path, Path, Path]:
    try:
        separator = sys.argv.index("--")
        source, output, texture_output = sys.argv[separator + 1 : separator + 4]
    except (ValueError, IndexError):
        raise SystemExit(
            "Expected arguments after --: SOURCE_BLEND OUTPUT_FBX OUTPUT_TEXTURE_DIR"
        )

    source_path = Path(source).resolve()
    output_path = Path(output).resolve()
    texture_output_path = Path(texture_output).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Source Blender file does not exist: {source_path}")
    if source_path == output_path:
        raise SystemExit("Source and output paths must differ")
    return source_path, output_path, texture_output_path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def paths_alias(left: Path, right: Path) -> bool:
    if left == right:
        return True
    return left.exists() and right.exists() and left.samefile(right)


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


def ordinal_png_manifest(directory: Path) -> tuple[int, int, str]:
    files = sorted(
        (path for path in directory.iterdir() if path.is_file() and path.suffix == ".png"),
        key=lambda path: path.name,
    )
    rows = "".join(
        f"{path.name}\t{path.stat().st_size}\t{sha256(path)}\n" for path in files
    ).encode("utf-8")
    return (
        len(files),
        sum(path.stat().st_size for path in files),
        hashlib.sha256(rows).hexdigest().upper(),
    )


def validate_source_scene(source_path: Path) -> list[bpy.types.Object]:
    scene = bpy.context.scene
    if scene.unit_settings.system != "METRIC" or scene.unit_settings.scale_length != 1.0:
        raise RuntimeError(
            f"Expected metric scale_length=1.0, got "
            f"{scene.unit_settings.system} {scene.unit_settings.scale_length}"
        )

    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    expected_objects = {member for members in GROUPS.values() for member in members}
    actual_objects = {obj.name for obj in meshes}
    missing = sorted(expected_objects - actual_objects)
    unexpected = sorted(actual_objects - expected_objects)
    if missing or unexpected or len(meshes) != EXPECTED_SOURCE_MESH_COUNT:
        raise RuntimeError(
            json.dumps(
                {
                    "missing_expected_meshes": missing,
                    "unexpected_meshes": unexpected,
                    "mesh_count": len(meshes),
                },
                sort_keys=True,
            )
        )

    unique_meshes = {obj.data for obj in meshes}
    unique_vertices = sum(len(mesh.vertices) for mesh in unique_meshes)
    triangles = sum(
        len(poly.vertices) - 2 for mesh in unique_meshes for poly in mesh.polygons
    )
    if (
        len(unique_meshes) != EXPECTED_UNIQUE_MESH_COUNT
        or unique_vertices != EXPECTED_UNIQUE_VERTEX_COUNT
        or triangles != EXPECTED_TRIANGLE_COUNT
    ):
        raise RuntimeError(
            json.dumps(
                {
                    "unique_mesh_count": len(unique_meshes),
                    "unique_vertices": unique_vertices,
                    "triangles": triangles,
                },
                sort_keys=True,
            )
        )

    material_names = sorted(
        {
            material.name
            for obj in meshes
            for material in obj.data.materials
            if material is not None
        }
    )
    if material_names != sorted(MATERIAL_TEXTURES):
        raise RuntimeError(
            json.dumps(
                {
                    "expected_materials": sorted(MATERIAL_TEXTURES),
                    "actual_materials": material_names,
                },
                sort_keys=True,
            )
        )

    if any(obj.modifiers for obj in meshes):
        raise RuntimeError("Native SR-25 source unexpectedly contains modifiers")
    if any(obj.type == "ARMATURE" for obj in scene.objects):
        raise RuntimeError("Native SR-25 source unexpectedly contains an armature")
    if bpy.data.actions:
        raise RuntimeError("Native SR-25 source unexpectedly contains animation actions")

    return meshes


def bake_to_shared_origin(obj: bpy.types.Object, source_world: Matrix) -> None:
    obj.animation_data_clear()
    obj.data = obj.data.copy()
    obj.data.transform(NORMALIZE_TO_SOURCE_AXES @ source_world)
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)


def join_group(name: str, members: list[str]) -> bpy.types.Object:
    objects = [bpy.data.objects[member] for member in members]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    joined = objects[0]
    bpy.context.view_layer.objects.active = joined
    if len(objects) > 1:
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


def split_orm_channels(source_dir: Path, output_dir: Path) -> list[dict[str, object]]:
    generated: list[dict[str, object]] = []
    channel_names = ((0, "AO"), (1, "Roughness"), (2, "Metalness"))
    for family, (_, _, orm_filename) in MATERIAL_TEXTURES.items():
        orm_path = source_dir / orm_filename
        if not orm_path.is_file():
            raise RuntimeError(f"Missing ORM texture: {orm_path}")

        source_image = bpy.data.images.load(str(orm_path), check_existing=False)
        source_image.colorspace_settings.name = "Non-Color"
        width, height = source_image.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        source_image.pixels.foreach_get(pixels)
        pixels = pixels.reshape((-1, 4))

        for channel_index, channel_name in channel_names:
            output_path = output_dir / f"{family}_{channel_name}.png"
            channel = np.empty_like(pixels)
            channel[:, 0] = pixels[:, channel_index]
            channel[:, 1] = pixels[:, channel_index]
            channel[:, 2] = pixels[:, channel_index]
            channel[:, 3] = 1.0

            target = bpy.data.images.new(
                f"{family}_{channel_name}_derived",
                width=width,
                height=height,
                alpha=True,
                float_buffer=False,
                is_data=True,
            )
            target.colorspace_settings.name = "Non-Color"
            target.file_format = "PNG"
            target.filepath_raw = str(output_path)
            target.pixels.foreach_set(channel.ravel())
            target.save()
            bpy.data.images.remove(target)

            verification = bpy.data.images.load(str(output_path), check_existing=False)
            verification.colorspace_settings.name = "Non-Color"
            verified_pixels = np.empty(width * height * 4, dtype=np.float32)
            verification.pixels.foreach_get(verified_pixels)
            verified_pixels = verified_pixels.reshape((-1, 4))
            max_channel_error = float(
                np.max(np.abs(verified_pixels[:, 0] - pixels[:, channel_index]))
            )
            max_rgb_disagreement = float(
                np.max(np.abs(verified_pixels[:, :3] - verified_pixels[:, :1]))
            )
            bpy.data.images.remove(verification)
            if max_channel_error > (1.0 / 255.0 + 1e-6) or max_rgb_disagreement > 1e-6:
                raise RuntimeError(
                    f"Derived {family} {channel_name} channel verification failed: "
                    f"channel_error={max_channel_error} rgb_error={max_rgb_disagreement}"
                )

            generated.append(
                {
                    "family": family,
                    "channel": channel_name,
                    "path": str(output_path),
                    "bytes": output_path.stat().st_size,
                    "sha256": sha256(output_path),
                    "width": width,
                    "height": height,
                    "max_channel_error": max_channel_error,
                    "max_rgb_disagreement": max_rgb_disagreement,
                }
            )

        bpy.data.images.remove(source_image)
    return generated


def stabilize_fbx_object_ids() -> None:
    """Replace Blender's pointer-hash FBX IDs with encounter-order IDs.

    Blender's stock exporter hashes keys that contain bpy object identities.
    Those pointer-derived hashes change between background processes even with
    PYTHONHASHSEED fixed.  Export traversal is stable for this validated source,
    so sequential IDs make the binary FBX byte-reproducible.
    """
    from io_scene_fbx import export_fbx_bin, fbx_utils

    def deterministic_key_to_uuid(uuids: dict[object, object], key: object):
        if isinstance(key, int) and 0 <= key < 2**63:
            value = key
        else:
            value = 1_000_000_000 + len(uuids)
            while fbx_utils.UUID(value) in uuids:
                value += 1
        return fbx_utils.UUID(value)

    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    fbx_utils._key_to_uuid = deterministic_key_to_uuid

    class FixedDateTime(datetime.datetime):
        @classmethod
        def now(cls, tz=None):
            value = cls(1970, 1, 1, 10, 0, 0, 0)
            return value if tz is None else value.replace(tzinfo=tz)

    export_fbx_bin.datetime.datetime = FixedDateTime


def main() -> None:
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit(
            "Set PYTHONHASHSEED=0 before launching Blender so FBX object IDs are byte-stable"
        )
    source_path, output_path, texture_output_path = parse_paths()
    pipeline_io = load_pipeline_io()
    output_path = pipeline_io.assert_temp_destination(output_path)
    texture_output_path = pipeline_io.assert_temp_destination(texture_output_path)
    source_bytes = source_path.stat().st_size
    source_hash = sha256(source_path)
    if source_bytes != EXPECTED_SOURCE_BYTES or source_hash != EXPECTED_SOURCE_SHA256:
        raise RuntimeError(
            f"Native source custody mismatch: bytes={source_bytes} sha256={source_hash}"
        )
    source_texture_dir = source_path.parent / "textures"
    if not source_texture_dir.is_dir():
        raise RuntimeError(f"Missing native source texture directory: {source_texture_dir}")
    texture_manifest = ordinal_png_manifest(source_texture_dir)
    expected_texture_manifest = (
        EXPECTED_SOURCE_TEXTURE_COUNT,
        EXPECTED_SOURCE_TEXTURE_BYTES,
        EXPECTED_SOURCE_TEXTURE_MANIFEST_SHA256,
    )
    if texture_manifest != expected_texture_manifest:
        raise RuntimeError(
            f"Native source texture custody mismatch: {texture_manifest} != "
            f"{expected_texture_manifest}"
        )
    derived_texture_paths = [
        texture_output_path / f"{family}_{channel}.png"
        for family in MATERIAL_TEXTURES
        for channel in ("AO", "Roughness", "Metalness")
    ]
    guarded_outputs = [output_path, *derived_texture_paths]
    custody_sources = [
        source_path,
        *sorted(
            path
            for path in source_texture_dir.iterdir()
            if path.is_file() and path.suffix == ".png"
        ),
    ]
    aliases = [
        (output, source)
        for output in guarded_outputs
        for source in custody_sources
        if paths_alias(output, source)
    ]
    if aliases:
        raise RuntimeError(
            "SR-25 outputs alias custody sources: "
            + ", ".join(f"{output} == {source}" for output, source in aliases)
        )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    texture_output_path.mkdir(parents=True, exist_ok=True)
    staged_output = pipeline_io.staging_path(output_path)
    staged_texture_directory = pipeline_io.staging_directory(texture_output_path)
    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    source_meshes = validate_source_scene(source_path)
    source_materials = sorted(
        {
            material.name
            for obj in source_meshes
            for material in obj.data.materials
            if material is not None
        }
    )
    generated_textures = split_orm_channels(
        source_path.parent / "textures", staged_texture_directory
    )

    # Snapshot every source world transform before unparenting anything.  The
    # native hierarchy is nested, so mutating a parent first would otherwise
    # move children that belong to later semantic groups.
    source_world_matrices = {
        obj.name: obj.matrix_world.copy() for obj in source_meshes
    }
    for obj in source_meshes:
        bake_to_shared_origin(obj, source_world_matrices[obj.name])

    retained = [join_group(name, members) for name, members in GROUPS.items()]
    for obj in list(bpy.context.scene.objects):
        if obj not in retained:
            bpy.data.objects.remove(obj, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in retained:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = retained[0]

    stabilize_fbx_object_ids()
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
        # Let Source 2 generate tangents from the preserved normals/UVs.  The
        # Blender 5.1 FBX tangent writer is multi-thread-order dependent and
        # makes otherwise identical exports byte-unstable.
        use_tspace=False,
        use_triangles=True,
    )

    scene_result = {
        "groups": [obj.name for obj in retained],
        "mesh_count": len(retained),
        "vertices": sum(len(obj.data.vertices) for obj in retained),
        "polygons": sum(len(obj.data.polygons) for obj in retained),
        "triangles": sum(
            len(poly.vertices) - 2 for obj in retained for poly in obj.data.polygons
        ),
        "bounds_meters_before_fbx_export": aggregate_bounds(retained),
    }

    semantic_result = load_semantic_inspector().inspect(staged_output)
    if semantic_result["sha256"] != EXPECTED_SEMANTIC_SHA256:
        raise RuntimeError(
            f"Semantic FBX mismatch: expected {EXPECTED_SEMANTIC_SHA256}, "
            f"got {semantic_result['sha256']}"
        )

    texture_promotions = [
        (Path(item["path"]), texture_output_path / Path(item["path"]).name)
        for item in generated_textures
    ]
    pipeline_io.promote_files(
        [(staged_output, output_path), *texture_promotions]
    )
    semantic_result["path"] = str(output_path)
    for item in generated_textures:
        item["path"] = str(texture_output_path / Path(item["path"]).name)

    result = {
        "source": str(source_path),
        "source_sha256": sha256(source_path),
        "output": str(output_path),
        "output_bytes": output_path.stat().st_size,
        "output_sha256": sha256(output_path),
        "groups": scene_result["groups"],
        "mesh_count": scene_result["mesh_count"],
        "vertices": scene_result["vertices"],
        "polygons": scene_result["polygons"],
        "triangles": scene_result["triangles"],
        "materials": source_materials,
        "bounds_meters_before_fbx_export": scene_result["bounds_meters_before_fbx_export"],
        "derived_texture_count": len(generated_textures),
        "derived_texture_bytes": sum(item["bytes"] for item in generated_textures),
        "derived_textures": generated_textures,
        "source_texture_manifest": {
            "count": texture_manifest[0],
            "bytes": texture_manifest[1],
            "sha256": texture_manifest[2],
        },
        "semantic_result": semantic_result,
    }
    print("DXRP_SR25_BUILD=" + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BaseException as exc:
        print(f"DXRP_SR25_BUILD_FAILED: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
