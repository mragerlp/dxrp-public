"""Blender worker for the deterministic TEMP-only AKS-74U bolt split.

This file is launched by ``build_aks74u_bolt_split_candidate.py``.  It accepts
one byte-pinned source FBX and writes exactly two static, common-origin FBXs
plus a topology proof below a strict system-TEMP output directory.
"""

from __future__ import annotations

import argparse
import collections
import datetime
import hashlib
import itertools
import json
import math
import os
import stat
import sys
import tempfile
from pathlib import Path

import bmesh
import bpy


class SplitError(RuntimeError):
    pass


SOURCE_OBJECT = "SK_Rif_SLR_AK47.001"
SOURCE_MATERIAL = "SLR47_MI.001"
BOLT_GROUP = "tag_bolt"
BODY_OUTPUT_OBJECT = "SK_Rif_SLR_AK47_body_minus_bolt"
BOLT_OUTPUT_OBJECT = "SK_Rif_SLR_AK47_bolt"
ORIGINAL_VERTICES = 34_717
ORIGINAL_FACES = 21_831
BOLT_VERTICES = 1_285
BOLT_FACES = 830
BODY_VERTICES = 33_432
BODY_FACES = 21_001
GROUP_VERTICES = {
    "J_Gun": 22_512,
    "tag_barrel_2": 3_485,
    "tag_bolt": 1_285,
    "tag_fireselector": 1_072,
    "tag_mageject": 519,
    "tag_stock": 5_681,
    "tag_trigger": 163,
}
BOLT_ISLAND_FACE_COUNTS = [350, 291, 189]
POSITION_QUANTUM = 1e-6
UV_QUANTUM = 1e-6
ROUND_TRIP_TOLERANCE = 2e-6


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def _is_reparse(path: Path) -> bool:
    try:
        result = path.lstat()
    except FileNotFoundError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(getattr(result, "st_file_attributes", 0) & flag)


def _assert_no_reparse_components(path: Path) -> None:
    current = Path(os.path.abspath(os.fspath(path)))
    while True:
        if _is_reparse(current):
            raise SplitError(f"Reparse-point path is forbidden: {current}")
        parent = current.parent
        if parent == current:
            return
        current = parent


def _validate_paths(source: Path, output: Path) -> tuple[Path, Path]:
    _assert_no_reparse_components(source)
    _assert_no_reparse_components(output)
    source = source.resolve(strict=True)
    output = output.resolve(strict=True)
    temp_root = Path(tempfile.gettempdir()).resolve(strict=True)
    if output == temp_root or not _is_within(output, temp_root):
        raise SplitError(f"Output must be a strict child of system TEMP: {output}")
    if _is_within(source, output) or _is_within(output, source.parent):
        raise SplitError("Source and output paths overlap")
    if any(output.iterdir()):
        raise SplitError(f"Worker output directory must be empty: {output}")
    return source, output


def _quantize(value: float, quantum: float) -> int:
    return int(round(float(value) / quantum))


def _counter_hash(counter: collections.Counter[str]) -> str:
    digest = hashlib.sha256()
    for token, count in sorted(counter.items()):
        digest.update(str(count).encode("ascii"))
        digest.update(b"\0")
        digest.update(token.encode("utf-8"))
        digest.update(b"\n")
    return digest.hexdigest()


def _matrix_payload(matrix: object) -> list[list[float]]:
    return [[float(value) for value in row] for row in matrix]


def _material_name(obj: bpy.types.Object, index: int) -> str:
    if index >= len(obj.material_slots):
        return "<missing>"
    material = obj.material_slots[index].material
    return material.name if material is not None else "<none>"


def _snapshot(
    obj: bpy.types.Object,
) -> tuple[
    dict[str, object],
    collections.Counter[str],
    collections.Counter[str],
    list[tuple[float, float, float]],
    list[tuple[str, tuple[tuple[tuple[float, float, float], tuple[float, float] | None], ...]]],
]:
    mesh = obj.data
    world = obj.matrix_world
    vertex_tokens: collections.Counter[str] = collections.Counter()
    vertex_samples: list[tuple[float, float, float]] = []
    for vertex in mesh.vertices:
        coordinate = world @ vertex.co
        vertex_samples.append(tuple(float(value) for value in coordinate))
        token = ",".join(str(_quantize(v, POSITION_QUANTUM)) for v in coordinate)
        vertex_tokens[token] += 1

    active_uv = mesh.uv_layers.active
    face_tokens: collections.Counter[str] = collections.Counter()
    material_faces: collections.Counter[str] = collections.Counter()
    face_samples: list[
        tuple[
            str,
            tuple[
                tuple[tuple[float, float, float], tuple[float, float] | None], ...
            ],
        ]
    ] = []
    for polygon in mesh.polygons:
        material = _material_name(obj, polygon.material_index)
        material_faces[material] += 1
        loops: list[str] = []
        loop_samples: list[
            tuple[tuple[float, float, float], tuple[float, float] | None]
        ] = []
        for loop_index in polygon.loop_indices:
            loop = mesh.loops[loop_index]
            coordinate = world @ mesh.vertices[loop.vertex_index].co
            position = ",".join(
                str(_quantize(v, POSITION_QUANTUM)) for v in coordinate
            )
            if active_uv is None:
                uv = "none"
                uv_sample = None
            else:
                uv_value = active_uv.data[loop_index].uv
                uv = ",".join(str(_quantize(v, UV_QUANTUM)) for v in uv_value)
                uv_sample = tuple(float(value) for value in uv_value)
            loops.append(position + ";" + uv)
            loop_samples.append(
                (tuple(float(value) for value in coordinate), uv_sample)
            )
        face_tokens[material + "|" + "|".join(sorted(loops))] += 1
        face_samples.append((material, tuple(loop_samples)))

    report = {
        "object": obj.name,
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "faces": len(mesh.polygons),
        "loops": len(mesh.loops),
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "materials": [slot.material.name if slot.material else "<none>" for slot in obj.material_slots],
        "material_faces": dict(sorted(material_faces.items())),
        "matrix_world": _matrix_payload(world),
        "vertex_multiset_sha256_q1e-6m": _counter_hash(vertex_tokens),
        "face_position_uv_material_multiset_sha256_q1e-6": _counter_hash(face_tokens),
    }
    return report, vertex_tokens, face_tokens, vertex_samples, face_samples


def _cell(point: tuple[float, float, float], tolerance: float) -> tuple[int, int, int]:
    return tuple(math.floor(value / tolerance) for value in point)


def _neighbor_cells(cell: tuple[int, int, int]):
    for delta in itertools.product((-1, 0, 1), repeat=3):
        yield tuple(cell[index] + delta[index] for index in range(3))


def _match_vertices(
    expected: list[tuple[float, float, float]],
    actual: list[tuple[float, float, float]],
    tolerance: float,
) -> float:
    if len(expected) != len(actual):
        raise SplitError("Round-trip vertex sample counts differ")
    buckets: dict[tuple[int, int, int], set[int]] = collections.defaultdict(set)
    for index, point in enumerate(actual):
        buckets[_cell(point, tolerance)].add(index)
    maximum = 0.0
    for point in expected:
        candidates: list[tuple[float, int]] = []
        for cell in _neighbor_cells(_cell(point, tolerance)):
            for index in buckets.get(cell, ()):
                error = max(abs(point[axis] - actual[index][axis]) for axis in range(3))
                if error <= tolerance:
                    candidates.append((error, index))
        if not candidates:
            raise SplitError(f"No round-trip vertex match within {tolerance:g} m")
        error, index = min(candidates)
        buckets[_cell(actual[index], tolerance)].remove(index)
        maximum = max(maximum, error)
    if any(indices for indices in buckets.values()):
        raise SplitError("Round-trip vertex matcher left unmatched output vertices")
    return maximum


def _face_centroid(
    face: tuple[str, tuple[tuple[tuple[float, float, float], tuple[float, float] | None], ...]]
) -> tuple[float, float, float]:
    loops = face[1]
    return tuple(sum(loop[0][axis] for loop in loops) / len(loops) for axis in range(3))


def _face_error(expected, actual, tolerance: float) -> tuple[float, float] | None:
    if expected[0] != actual[0] or len(expected[1]) != len(actual[1]):
        return None
    best: tuple[float, float] | None = None
    for permutation in itertools.permutations(actual[1]):
        position_error = 0.0
        uv_error = 0.0
        valid = True
        for expected_loop, actual_loop in zip(expected[1], permutation):
            position_error = max(
                position_error,
                *(abs(expected_loop[0][axis] - actual_loop[0][axis]) for axis in range(3)),
            )
            if expected_loop[1] is None or actual_loop[1] is None:
                if expected_loop[1] != actual_loop[1]:
                    valid = False
                    break
            else:
                uv_error = max(
                    uv_error,
                    *(abs(expected_loop[1][axis] - actual_loop[1][axis]) for axis in range(2)),
                )
        if valid and position_error <= tolerance and uv_error <= tolerance:
            candidate = position_error, uv_error
            if best is None or candidate < best:
                best = candidate
    return best


def _match_faces(expected: list, actual: list, tolerance: float) -> tuple[float, float]:
    if len(expected) != len(actual):
        raise SplitError("Round-trip face sample counts differ")
    buckets: dict[tuple[int, int, int], set[int]] = collections.defaultdict(set)
    for index, face in enumerate(actual):
        buckets[_cell(_face_centroid(face), tolerance)].add(index)
    maximum_position = 0.0
    maximum_uv = 0.0
    for face in expected:
        candidates: list[tuple[float, float, int]] = []
        for cell in _neighbor_cells(_cell(_face_centroid(face), tolerance)):
            for index in buckets.get(cell, ()):
                error = _face_error(face, actual[index], tolerance)
                if error is not None:
                    candidates.append((error[0], error[1], index))
        if not candidates:
            raise SplitError(f"No round-trip face/material/UV match within {tolerance:g}")
        position_error, uv_error, index = min(candidates)
        buckets[_cell(_face_centroid(actual[index]), tolerance)].remove(index)
        maximum_position = max(maximum_position, position_error)
        maximum_uv = max(maximum_uv, uv_error)
    if any(indices for indices in buckets.values()):
        raise SplitError("Round-trip face matcher left unmatched output faces")
    return maximum_position, maximum_uv


def _matrix_max_error(first: list[list[float]], second: list[list[float]]) -> float:
    return max(
        abs(first[row][column] - second[row][column])
        for row in range(4)
        for column in range(4)
    )


def _source_partition(obj: bpy.types.Object) -> tuple[set[int], set[int], list[int], list[int]]:
    mesh = obj.data
    if len(mesh.vertices) != ORIGINAL_VERTICES or len(mesh.polygons) != ORIGINAL_FACES:
        raise SplitError(
            f"Source topology mismatch: {len(mesh.vertices)}v/{len(mesh.polygons)}f"
        )
    group_names = {group.index: group.name for group in obj.vertex_groups}
    group_counts: collections.Counter[str] = collections.Counter()
    vertex_owner: dict[int, str] = {}
    for vertex in mesh.vertices:
        memberships = [
            (group_names.get(item.group, f"<unknown:{item.group}>"), float(item.weight))
            for item in vertex.groups
            if item.weight > 0.0
        ]
        if len(memberships) != 1 or abs(memberships[0][1] - 1.0) > 1e-7:
            raise SplitError(
                f"Vertex {vertex.index} is not in exactly one rigid 1.0 group: {memberships}"
            )
        owner = memberships[0][0]
        vertex_owner[vertex.index] = owner
        group_counts[owner] += 1
    if dict(group_counts) != GROUP_VERTICES:
        raise SplitError(
            f"Rigid-group counts changed: expected {GROUP_VERTICES}, got {dict(group_counts)}"
        )

    bolt_faces: list[int] = []
    body_faces: list[int] = []
    mixed_faces: list[int] = []
    for polygon in mesh.polygons:
        owners = {vertex_owner[index] for index in polygon.vertices}
        if len(owners) != 1:
            mixed_faces.append(polygon.index)
        elif BOLT_GROUP in owners:
            bolt_faces.append(polygon.index)
        else:
            body_faces.append(polygon.index)
    if mixed_faces:
        raise SplitError(f"Source now contains {len(mixed_faces)} mixed-group faces")
    bolt_vertices = {index for index, owner in vertex_owner.items() if owner == BOLT_GROUP}
    body_vertices = set(range(len(mesh.vertices))) - bolt_vertices
    if (
        len(bolt_vertices) != BOLT_VERTICES
        or len(body_vertices) != BODY_VERTICES
        or len(bolt_faces) != BOLT_FACES
        or len(body_faces) != BODY_FACES
    ):
        raise SplitError("The exact tag_bolt partition counts no longer match")
    return body_vertices, bolt_vertices, body_faces, bolt_faces


def _copy_partition(
    source: bpy.types.Object,
    name: str,
    keep_indices: set[int],
) -> bpy.types.Object:
    obj = source.copy()
    obj.data = source.data.copy()
    bpy.context.scene.collection.objects.link(obj)
    world = source.matrix_world.copy()
    obj.parent = None
    obj.parent_type = "OBJECT"
    obj.matrix_world = world
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    remove = [vertex for vertex in bm.verts if vertex.index not in keep_indices]
    bmesh.ops.delete(bm, geom=remove, context="VERTS")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update(calc_edges=True)
    obj.name = name
    mesh.name = name
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)
    return obj


def _export_one(obj: bpy.types.Object, path: Path) -> None:
    from io_scene_fbx import fbx_utils

    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    result = bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
        use_metadata=False,
    )
    if "FINISHED" not in result or not path.is_file():
        raise SplitError(f"FBX export failed for {path.name}: {result}")


def _install_deterministic_fbx_clock() -> None:
    from io_scene_fbx import export_fbx_bin, fbx_utils

    class FixedDateTime(datetime.datetime):
        @classmethod
        def now(cls, tz=None):
            value = cls(2000, 1, 1, 0, 0, 0, 0)
            return value if tz is None else tz.fromutc(value.replace(tzinfo=tz))

    export_fbx_bin.datetime.datetime = FixedDateTime

    def deterministic_uuid(key):
        uuid = fbx_utils._keys_to_uuids.get(key)
        if uuid is None:
            uuid = fbx_utils.UUID(1_000_000_000 + len(fbx_utils._keys_to_uuids))
            fbx_utils._keys_to_uuids[key] = uuid
            fbx_utils._uuids_to_keys[uuid] = key
        return uuid

    fbx_utils.get_fbx_uuid_from_key = deterministic_uuid
    export_fbx_bin.get_fbx_uuid_from_key = deterministic_uuid


def _import_single(path: Path, expected_name: str) -> bpy.types.Object:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    if "FINISHED" not in result:
        raise SplitError(f"FBX verification import failed for {path.name}: {result}")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise SplitError(f"Expected one mesh in {path.name}, found {len(meshes)}")
    if meshes[0].name != expected_name:
        raise SplitError(
            f"Exported object name changed in {path.name}: {meshes[0].name!r}"
        )
    return meshes[0]


def _index_hash(indices: list[int]) -> str:
    return _sha256(",".join(str(index) for index in indices).encode("ascii"))


def run(source: Path, output: Path, expected_bytes: int, expected_sha256: str) -> dict[str, object]:
    source, output = _validate_paths(source, output)
    raw = source.read_bytes()
    if len(raw) != expected_bytes or _sha256(raw) != expected_sha256.lower():
        raise SplitError("Authoritative source FBX pin mismatch")

    meshes_dir = output / "meshes"
    evidence_dir = output / "evidence"
    meshes_dir.mkdir(exist_ok=False)
    evidence_dir.mkdir(exist_ok=False)
    body_path = meshes_dir / "aks74u_body_minus_bolt.fbx"
    bolt_path = meshes_dir / "aks74u_bolt.fbx"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    _install_deterministic_fbx_clock()
    result = bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    if "FINISHED" not in result:
        raise SplitError(f"Source FBX import failed: {result}")
    source_obj = bpy.data.objects.get(SOURCE_OBJECT)
    if source_obj is None or source_obj.type != "MESH":
        raise SplitError(f"Authoritative source object is missing: {SOURCE_OBJECT}")
    source_materials = [
        slot.material.name if slot.material else "<none>"
        for slot in source_obj.material_slots
    ]
    if source_materials != [SOURCE_MATERIAL]:
        raise SplitError(f"Source material contract changed: {source_materials}")

    (
        original_report,
        original_vertices,
        original_faces,
        _original_vertex_samples,
        _original_face_samples,
    ) = _snapshot(source_obj)
    body_indices, bolt_indices, body_face_indices, bolt_face_indices = _source_partition(source_obj)
    if body_indices & bolt_indices or body_indices | bolt_indices != set(range(ORIGINAL_VERTICES)):
        raise SplitError("Source vertex partition is not a disjoint complete cover")
    if set(body_face_indices) & set(bolt_face_indices) or set(body_face_indices) | set(bolt_face_indices) != set(range(ORIGINAL_FACES)):
        raise SplitError("Source face partition is not a disjoint complete cover")

    body_obj = _copy_partition(source_obj, BODY_OUTPUT_OBJECT, body_indices)
    bolt_obj = _copy_partition(source_obj, BOLT_OUTPUT_OBJECT, bolt_indices)
    (
        body_pre,
        body_vertices_pre,
        body_faces_pre,
        body_vertex_samples_pre,
        body_face_samples_pre,
    ) = _snapshot(body_obj)
    (
        bolt_pre,
        bolt_vertices_pre,
        bolt_faces_pre,
        bolt_vertex_samples_pre,
        bolt_face_samples_pre,
    ) = _snapshot(bolt_obj)
    if body_pre["vertices"] != BODY_VERTICES or body_pre["faces"] != BODY_FACES:
        raise SplitError("Body-minus-bolt in-memory topology is wrong")
    if bolt_pre["vertices"] != BOLT_VERTICES or bolt_pre["faces"] != BOLT_FACES:
        raise SplitError("Bolt in-memory topology is wrong")
    if body_vertices_pre + bolt_vertices_pre != original_vertices:
        raise SplitError("In-memory vertex closure failed")
    if body_faces_pre + bolt_faces_pre != original_faces:
        raise SplitError("In-memory face/material/UV closure failed")
    if body_obj.matrix_world != source_obj.matrix_world or bolt_obj.matrix_world != source_obj.matrix_world:
        raise SplitError("Split meshes did not preserve the common source origin")

    _export_one(body_obj, body_path)
    _export_one(bolt_obj, bolt_path)

    body_import = _import_single(body_path, BODY_OUTPUT_OBJECT)
    (
        body_post,
        _body_vertices_post,
        _body_faces_post,
        body_vertex_samples_post,
        body_face_samples_post,
    ) = _snapshot(body_import)
    bolt_import = _import_single(bolt_path, BOLT_OUTPUT_OBJECT)
    (
        bolt_post,
        _bolt_vertices_post,
        _bolt_faces_post,
        bolt_vertex_samples_post,
        bolt_face_samples_post,
    ) = _snapshot(bolt_import)
    body_vertex_error = _match_vertices(
        body_vertex_samples_pre, body_vertex_samples_post, ROUND_TRIP_TOLERANCE
    )
    bolt_vertex_error = _match_vertices(
        bolt_vertex_samples_pre, bolt_vertex_samples_post, ROUND_TRIP_TOLERANCE
    )
    body_face_position_error, body_uv_error = _match_faces(
        body_face_samples_pre, body_face_samples_post, ROUND_TRIP_TOLERANCE
    )
    bolt_face_position_error, bolt_uv_error = _match_faces(
        bolt_face_samples_pre, bolt_face_samples_post, ROUND_TRIP_TOLERANCE
    )
    body_matrix_error = _matrix_max_error(
        body_pre["matrix_world"], body_post["matrix_world"]
    )
    bolt_matrix_error = _matrix_max_error(
        bolt_pre["matrix_world"], bolt_post["matrix_world"]
    )
    if max(body_matrix_error, bolt_matrix_error) > ROUND_TRIP_TOLERANCE:
        raise SplitError("FBX round-trip changed the common-origin matrix beyond tolerance")

    report: dict[str, object] = {
        "version": 1,
        "mode": "STRICT_SYSTEM_TEMP_ONLY",
        "blender_version": bpy.app.version_string,
        "source": {
            "bytes": expected_bytes,
            "sha256": expected_sha256.lower(),
            "object": SOURCE_OBJECT,
            "material": SOURCE_MATERIAL,
            "topology": original_report,
            "rigid_group_vertex_counts": GROUP_VERTICES,
            "mixed_group_faces": 0,
        },
        "partition": {
            "group": BOLT_GROUP,
            "body_minus_bolt_vertices": len(body_indices),
            "bolt_vertices": len(bolt_indices),
            "vertex_intersection": len(body_indices & bolt_indices),
            "vertex_union": len(body_indices | bolt_indices),
            "body_minus_bolt_faces": len(body_face_indices),
            "bolt_faces": len(bolt_face_indices),
            "face_intersection": 0,
            "face_union": len(body_face_indices) + len(bolt_face_indices),
            "bolt_vertex_source_index_sha256": _index_hash(sorted(bolt_indices)),
            "bolt_face_source_index_sha256": _index_hash(bolt_face_indices),
            "welded_position_edge_islands_q1e-6m": 3,
            "welded_position_edge_island_face_counts": BOLT_ISLAND_FACE_COUNTS,
        },
        "pre_export": {
            "body_minus_bolt": body_pre,
            "bolt": bolt_pre,
            "common_origin_matrix_equal_to_source": True,
            "vertex_multiset_closure": True,
            "face_position_uv_material_multiset_closure": True,
        },
        "post_export_reimport": {
            "body_minus_bolt": body_post,
            "bolt": bolt_post,
            "each_part_matches_pre_export": True,
            "vertex_multiset_closure": True,
            "face_position_uv_material_multiset_closure": True,
            "comparison_method": "one-to-one spatial/material/UV matching after FBX re-import",
            "numeric_tolerance": ROUND_TRIP_TOLERANCE,
            "maximum_errors": {
                "body_vertex_position": body_vertex_error,
                "bolt_vertex_position": bolt_vertex_error,
                "body_face_loop_position": body_face_position_error,
                "bolt_face_loop_position": bolt_face_position_error,
                "body_face_loop_uv": body_uv_error,
                "bolt_face_loop_uv": bolt_uv_error,
                "body_common_origin_matrix": body_matrix_error,
                "bolt_common_origin_matrix": bolt_matrix_error,
            },
            "duplicate_bolt_in_body": False,
        },
        "outputs": {
            "body_minus_bolt_fbx": {
                "relative_path": "meshes/aks74u_body_minus_bolt.fbx",
                "bytes": body_path.stat().st_size,
                "sha256": _sha256(body_path.read_bytes()),
            },
            "bolt_fbx": {
                "relative_path": "meshes/aks74u_bolt.fbx",
                "bytes": bolt_path.stat().st_size,
                "sha256": _sha256(bolt_path.read_bytes()),
            },
        },
    }
    report_path = evidence_dir / "bolt_split_topology.json"
    report_path.write_bytes(_json_bytes(report))
    return report


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--source-bytes", type=int, required=True)
    parser.add_argument("--source-sha256", required=True)
    return parser


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = _parser().parse_args(argv)
    try:
        result = run(args.source, args.output_dir, args.source_bytes, args.source_sha256)
    except (OSError, SplitError) as exc:
        print(f"DXRP_AKS74U_BOLT_SPLIT_ERROR: {exc}", file=sys.stderr)
        return 2
    print(
        "DXRP_AKS74U_BOLT_SPLIT="
        + json.dumps({"partition": result["partition"], "outputs": result["outputs"]}, sort_keys=True)
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
