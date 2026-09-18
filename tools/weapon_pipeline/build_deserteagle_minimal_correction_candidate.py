#!/usr/bin/env python3
"""Build one narrow, deterministic Desert Eagle correction in system TEMP.

The byte-pinned active ``vm_desert_eagle`` prefab is the complete baseline.  It
is cloned without remapping any existing GUID or changing any existing
reference, local transform, component, or root name.  The only candidate
changes are:

* insert one deterministic transform-only wrapper between the compiled USP
  ``magazine`` bone GameObject and the existing Desert Eagle magazine node;
* register the existing custom ``weapon_root`` as
  ``ViewModel.AdditionalRendererRoot``.

For the compiled-USP magazine bind ``P``, existing magazine renderer local
``R``, and existing body assembly ``A``, the wrapper is derived as
``W = inverse(P) * A * inverse(R)``.  The serialized candidate is accepted only
when ``P * W * R`` reconstructs ``A`` within the pinned numerical tolerance.

There is deliberately no product-write mode.  Output is restricted to a
strict child of the operating system TEMP directory.  This is static
structural evidence, not compile, editor, runtime, animation, or visual proof.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import os
import sys
import tempfile
import uuid
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

import build_deserteagle_common_origin_candidate as common_origin
import pipeline_io


class ContractError(RuntimeError):
    """Raised when source custody or the minimal correction contract drifts."""


WORKBENCH_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
ACTIVE_PREFAB_PATH = (
    WORKBENCH_ROOT
    / "game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/"
    "vm_desert_eagle/vm_desert_eagle.prefab"
)
OUTPUT_NAME = "vm_desert_eagle.minimal-correction.candidate.prefab"

EXPECTED_ACTIVE_BYTES = 73_212
EXPECTED_ACTIVE_SHA256 = (
    "77A891CD69990A868726B821DC7F52FD086F3594B48752E497809559765E4227"
)

COMMON_ORIGIN_HELPER_PATH = Path(common_origin.__file__).resolve()
EXPECTED_COMMON_ORIGIN_HELPER_BYTES = 50_237
EXPECTED_COMMON_ORIGIN_HELPER_SHA256 = (
    "3C2DBD5574C7433422A74EC345BC0CA3B985C374BB2741138EABDD9CFD425446"
)
PIPELINE_IO_PATH = Path(pipeline_io.__file__).resolve()
EXPECTED_PIPELINE_IO_BYTES = 12_537
EXPECTED_PIPELINE_IO_SHA256 = (
    "B74664DA4D63248A6475990BF99BAF9D9EFF6C0209717CFC6A7B9BE481E22FBA"
)

COMPILED_BIND_PARSER_PATH = Path(r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe")
COMPILED_BIND_PARSER_BYTES = 108_603_232
COMPILED_BIND_PARSER_SHA256 = (
    "36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0"
)
COMPILED_BIND_PARSER_VERSION = (
    "19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa"
)
COMPILED_BIND_MODEL_PATH = Path(
    r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons\sbox_pistol_usp\v_usp.c9a4333387e69880.vmdl_c"
)
COMPILED_BIND_MODEL_BYTES = 1_394_438
COMPILED_BIND_MODEL_SHA256 = (
    "439F8B2B676F310318EC4080D577DC16EA3549249564BADDDCD0B0CDE4A520F5"
)


@dataclass(frozen=True)
class SourcePin:
    relative_path: str
    bytes: int
    sha256: str


SOURCE_PINS: Mapping[str, SourcePin] = {
    "body_model": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl",
        1_054,
        "7E987BA4509C45CBC1717B39E3F3BE8264E02059B7B82B3326DD415E27685DF3",
    ),
    "slide_model": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl",
        1_055,
        "16F801A943B3B3E94E1A74C18A774538874F4C40A9D02E6706D81B8B1392EAAA",
    ),
    "magazine_model": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl",
        1_058,
        "3204A463BF0B0F08DDF847546F8B26A7AE2E624D2C5125BA4042CD0C5375163E",
    ),
    "body_mesh": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_body.fbx",
        256_396,
        "E9061EEEA5D623A8BCEE9D68F8E22E22C82B9E4C26B4396AB8EFA036BAA1A2AE",
    ),
    "slide_mesh": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_slide.fbx",
        39_836,
        "53EBB3124644686F91F9FB3DF3406DEAA1ADD66DB2B2DB9D19F08D5D6DB5D595",
    ),
    "magazine_mesh": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_magazine.fbx",
        25_244,
        "88C3124612D2E88E88F7A7B8F91EB4E919C36F7B36FAD7EFF7B00AE94EC377F5",
    ),
    "visible_material": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/mat_desert_eagle.vmat",
        1_036,
        "25AB9EE9DD5BF8C4F0DB811B5F0B7080D3D5BD266522AAB7DF089EA20701A8B5",
    ),
    "invisible_material": SourcePin(
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/"
        "vm_desert_eagle/invisible.vmat",
        463,
        "A9D0BA2E36D8C3F719465BF95F8C738636507D3B32FAC12F61FAB1B3A0D3D7B7",
    ),
}

ROOT_NAME = "vm_desert_eagle"
ROOT_GUID = "647c7b32-1ba6-473a-899a-29c5da805480"
VIEW_MODEL_GUID = "f1e6fcd8-0f8f-4757-b164-300ceeb9a2c5"
CUSTOM_ROOT_PATH = "weapon_root"
CUSTOM_ROOT_GUID = "a69829af-af97-4da5-9c4f-584d43998bd5"
MAGAZINE_PARENT_PATH = "weapon_root/weapon_root_children/magazine"
MAGAZINE_PARENT_GUID = "dff4e566-fa52-4130-bb5d-26c3e951206a"
MAGAZINE_RENDERER_PATH = MAGAZINE_PARENT_PATH + "/desert_eagle_magazine"
MAGAZINE_RENDERER_GUID = "07c7131b-503b-47c3-8780-c3fe309c78d8"
BODY_RENDERER_PATH = "weapon_root/weapon_root_children/desert_eagle_body"
SLIDE_RENDERER_PATH = "weapon_root/weapon_root_children/slide/desert_eagle_slide"

BODY_MODEL = "addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl"
SLIDE_MODEL = "addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl"
MAGAZINE_MODEL = "addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl"
VISIBLE_MATERIAL = "addons/lifepunch/lpweapons/deserteagle/mat_desert_eagle.vmat"
INVISIBLE_MATERIAL = (
    "addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/invisible.vmat"
)
USP_DRIVER_MODEL = "models/weapons/sbox_pistol_usp/v_usp.vmdl"

CUSTOM_RENDERERS: Mapping[str, tuple[str, str]] = {
    BODY_RENDERER_PATH: (
        "87e02586-4d69-4829-a45b-11be950a16a9",
        BODY_MODEL,
    ),
    SLIDE_RENDERER_PATH: (
        "b312a440-4eee-40b3-a3b6-149dac8479b9",
        SLIDE_MODEL,
    ),
    MAGAZINE_RENDERER_PATH: (
        "f5a510e8-628a-4dd6-a660-06180a870ef2",
        MAGAZINE_MODEL,
    ),
}

EXPECTED_BODY_TRANSFORM = {
    "Position": "2.8425531,-0.029227138,-3.1230555",
    "Rotation": "0,0,0,1",
    "Scale": "0.037891492,0.037891492,0.037891492",
}
EXPECTED_SLIDE_TRANSFORM = {
    "Position": "1.5202713,-0.029129624,-4.7529078",
    "Rotation": "0,0,0,1",
    "Scale": "0.037891492,0.037891492,0.037891492",
}
EXPECTED_MAGAZINE_TRANSFORM = dict(EXPECTED_BODY_TRANSFORM)

WRAPPER_NAMESPACE = uuid.UUID("c60e1874-13af-527f-a3a1-19f38ae9627c")
WRAPPER_GUID = str(
    uuid.uuid5(
        WRAPPER_NAMESPACE,
        EXPECTED_ACTIVE_SHA256
        + ":"
        + COMPILED_BIND_MODEL_SHA256
        + ":desert-eagle-magazine-bind-wrapper-v1",
    )
)
WRAPPER_NAME = "desert_eagle_magazine_bind_wrapper"
RECONSTRUCTION_TOLERANCE = 1e-12


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _verify_file_pin(
    path: Path, expected_bytes: int, expected_sha256: str, label: str
) -> bytes:
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"cannot read pinned {label} {path}: {exc}") from exc
    if len(data) != expected_bytes:
        raise ContractError(
            f"{label} byte pin mismatch: expected {expected_bytes}, got {len(data)}"
        )
    actual = _sha256(data)
    if actual != expected_sha256:
        raise ContractError(
            f"{label} SHA-256 pin mismatch: expected {expected_sha256}, got {actual}"
        )
    return data


def _source_pin_report() -> dict[str, dict[str, object]]:
    return {
        name: {
            "path": str((WORKBENCH_ROOT / pin.relative_path).resolve()),
            "bytes": pin.bytes,
            "sha256": pin.sha256,
        }
        for name, pin in sorted(SOURCE_PINS.items())
    }


def validate_source_pins() -> dict[str, bytes]:
    _verify_file_pin(
        COMMON_ORIGIN_HELPER_PATH,
        EXPECTED_COMMON_ORIGIN_HELPER_BYTES,
        EXPECTED_COMMON_ORIGIN_HELPER_SHA256,
        "common-origin helper",
    )
    _verify_file_pin(
        PIPELINE_IO_PATH,
        EXPECTED_PIPELINE_IO_BYTES,
        EXPECTED_PIPELINE_IO_SHA256,
        "pipeline I/O helper",
    )
    _verify_file_pin(
        COMPILED_BIND_PARSER_PATH,
        COMPILED_BIND_PARSER_BYTES,
        COMPILED_BIND_PARSER_SHA256,
        "compiled-bind parser",
    )
    _verify_file_pin(
        COMPILED_BIND_MODEL_PATH,
        COMPILED_BIND_MODEL_BYTES,
        COMPILED_BIND_MODEL_SHA256,
        "compiled USP model",
    )

    payloads: dict[str, bytes] = {}
    for name, pin in SOURCE_PINS.items():
        payloads[name] = _verify_file_pin(
            WORKBENCH_ROOT / pin.relative_path,
            pin.bytes,
            pin.sha256,
            name.replace("_", " "),
        )

    model_contracts = {
        "body_model": (
            "addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_body.fbx",
            VISIBLE_MATERIAL,
        ),
        "slide_model": (
            "addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_slide.fbx",
            VISIBLE_MATERIAL,
        ),
        "magazine_model": (
            "addons/lifepunch/lpweapons/deserteagle/source/desert_eagle_magazine.fbx",
            VISIBLE_MATERIAL,
        ),
    }
    for name, (mesh, material) in model_contracts.items():
        try:
            text = payloads[name].decode("utf-8")
        except UnicodeError as exc:
            raise ContractError(f"pinned {name} is not UTF-8 text") from exc
        if f'filename = "{mesh}"' not in text:
            raise ContractError(f"pinned {name} source-mesh reference drifted")
        if f'global_default_material = "{material}"' not in text:
            raise ContractError(f"pinned {name} material reference drifted")
    return payloads


def _node_transform_payload(node: Mapping[str, Any]) -> dict[str, object]:
    return {
        "Position": node.get("Position"),
        "Rotation": node.get("Rotation"),
        "Scale": node.get("Scale"),
    }


def _components_of_type(
    node: Mapping[str, Any], component_type: str
) -> list[dict[str, Any]]:
    return [
        component
        for component in node.get("Components", []) or []
        if component.get("__type") == component_type
    ]


def _is_descendant_or_self(
    index: common_origin.PrefabIndex, node_guid: str, ancestor_guid: str
) -> bool:
    current: str | None = node_guid
    seen: set[str] = set()
    while current is not None:
        if current == ancestor_guid:
            return True
        if current in seen:
            raise ContractError(f"cycle detected in prefab parent graph at {current}")
        seen.add(current)
        current = index.parent_by_guid.get(current)
    return False


def _reference_counter(value: object) -> Counter[tuple[str, str, str | None]]:
    counter: Counter[tuple[str, str, str | None]] = Counter()
    for reference in common_origin._reference_objects(value):
        kind = reference.get("_type")
        owner = reference.get("go")
        component = reference.get("component_id")
        if isinstance(kind, str) and isinstance(owner, str):
            counter[(kind, owner, component if isinstance(component, str) else None)] += 1
    return counter


def validate_active_prefab(
    prefab: dict[str, Any], raw: bytes
) -> tuple[common_origin.PrefabIndex, dict[str, Any]]:
    if common_origin._serialize_prefab(prefab, raw) != raw:
        raise ContractError("active prefab is not byte-stable through pinned serialization")
    root = prefab.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError("active prefab omitted RootObject")
    if root.get("Name") != ROOT_NAME or root.get("__guid") != ROOT_GUID:
        raise ContractError("active Desert Eagle root name/GUID drifted")

    index = common_origin.index_prefab(prefab)
    common_origin.assert_references_valid(prefab, index)
    if WRAPPER_GUID in index.all_guids:
        raise ContractError("deterministic wrapper GUID already exists")
    if any(node.get("Name") == WRAPPER_NAME for node in index.nodes_by_guid.values()):
        raise ContractError("magazine bind wrapper already exists")

    view_models = _components_of_type(root, "Dxura.RP.Game.ViewModel")
    if len(view_models) != 1 or view_models[0].get("__guid") != VIEW_MODEL_GUID:
        raise ContractError("active ViewModel component count/GUID drifted")
    view_model = view_models[0]
    if "AdditionalRendererRoot" in view_model:
        raise ContractError("active ViewModel already has AdditionalRendererRoot")

    custom_root = index.nodes_by_path.get(CUSTOM_ROOT_PATH)
    if custom_root is None or custom_root.get("__guid") != CUSTOM_ROOT_GUID:
        raise ContractError("active custom renderer root path/GUID drifted")
    magazine_parent = index.nodes_by_path.get(MAGAZINE_PARENT_PATH)
    if magazine_parent is None or magazine_parent.get("__guid") != MAGAZINE_PARENT_GUID:
        raise ContractError("active magazine parent path/GUID drifted")
    children = magazine_parent.get("Children")
    if (
        not isinstance(children, list)
        or len(children) != 1
        or children[0].get("__guid") != MAGAZINE_RENDERER_GUID
    ):
        raise ContractError("active magazine parent must contain exactly the pinned renderer")

    expected_transforms = {
        BODY_RENDERER_PATH: EXPECTED_BODY_TRANSFORM,
        SLIDE_RENDERER_PATH: EXPECTED_SLIDE_TRANSFORM,
        MAGAZINE_RENDERER_PATH: EXPECTED_MAGAZINE_TRANSFORM,
    }
    for path, expected in expected_transforms.items():
        node = index.nodes_by_path.get(path)
        if node is None or _node_transform_payload(node) != expected:
            raise ContractError(f"active local transform drifted at {path}")

    observed_custom: set[str] = set()
    for path, (renderer_guid, model) in CUSTOM_RENDERERS.items():
        node = index.nodes_by_path.get(path)
        if node is None:
            raise ContractError(f"active custom renderer path disappeared: {path}")
        renderers = _components_of_type(node, "Sandbox.ModelRenderer")
        if (
            len(renderers) != 1
            or renderers[0].get("__guid") != renderer_guid
            or renderers[0].get("Model") != model
        ):
            raise ContractError(f"active custom renderer contract drifted at {path}")
        observed_custom.add(renderer_guid)
        if not _is_descendant_or_self(index, node["__guid"], CUSTOM_ROOT_GUID):
            raise ContractError(f"custom renderer escaped weapon_root at {path}")
    all_custom = {
        guid
        for guid, component in index.components_by_guid.items()
        if isinstance(component.get("Model"), str)
        and component["Model"].startswith("addons/lifepunch/lpweapons/deserteagle/")
        and component.get("__type") == "Sandbox.ModelRenderer"
    }
    if all_custom != observed_custom:
        raise ContractError("active custom renderer set drifted")

    drivers = [
        component
        for component in _components_of_type(root, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == USP_DRIVER_MODEL
    ]
    if (
        len(drivers) != 1
        or drivers[0].get("MaterialOverride") != INVISIBLE_MATERIAL
        or drivers[0].get("CreateBoneObjects") is not True
        or drivers[0].get("UseAnimGraph") is not True
    ):
        raise ContractError("active USP animation driver contract drifted")
    return index, view_model


def _canonical_wrapper_transform(
    transform: common_origin.Transform,
) -> common_origin.Transform:
    def clean(value: float) -> float:
        if abs(value) <= 1e-15:
            return 0.0
        return value

    return common_origin.Transform(
        position=tuple(clean(value) for value in transform.position),  # type: ignore[arg-type]
        quaternion=tuple(clean(value) for value in transform.quaternion),  # type: ignore[arg-type]
        scale=tuple(clean(value) for value in transform.scale),  # type: ignore[arg-type]
    )


def derive_magazine_wrapper(
    index: common_origin.PrefabIndex,
) -> tuple[common_origin.Transform, dict[str, object]]:
    body = index.nodes_by_path[BODY_RENDERER_PATH]
    slide = index.nodes_by_path[SLIDE_RENDERER_PATH]
    magazine = index.nodes_by_path[MAGAZINE_RENDERER_PATH]
    body_local = common_origin.transform_matrix(
        common_origin._transform_from_node(body, "body renderer R_body")
    )
    slide_local = common_origin.transform_matrix(
        common_origin._transform_from_node(slide, "slide renderer R_slide")
    )
    magazine_local = common_origin.transform_matrix(
        common_origin._transform_from_node(magazine, "magazine renderer R")
    )
    root_bind = common_origin._parse_manifest_transform(
        common_origin.COMPILED_BIND_TRANSFORMS["B_root"], "compiled B_root"
    )
    slide_bind = common_origin._parse_manifest_transform(
        common_origin.COMPILED_BIND_TRANSFORMS["B_slide"], "compiled B_slide"
    )
    magazine_bind = common_origin._parse_manifest_transform(
        common_origin.COMPILED_BIND_TRANSFORMS["B_mag"], "compiled B_mag"
    )

    assembly = common_origin.matrix_multiply(root_bind, body_local)
    exact_wrapper = common_origin.matrix_multiply(
        common_origin.matrix_multiply(
            common_origin.matrix_inverse(magazine_bind), assembly
        ),
        common_origin.matrix_inverse(magazine_local),
    )
    wrapper = _canonical_wrapper_transform(
        common_origin.decompose_trs(exact_wrapper, "magazine wrapper W")
    )
    serialized = common_origin._serialized_transform(wrapper)
    reparsed = common_origin._transform_from_node(serialized, "serialized wrapper W")
    serialized_wrapper = common_origin.transform_matrix(reparsed)
    reconstructed = common_origin.matrix_multiply(
        common_origin.matrix_multiply(magazine_bind, serialized_wrapper),
        magazine_local,
    )
    reconstruction_delta = common_origin.matrix_max_delta(reconstructed, assembly)
    if not math.isfinite(reconstruction_delta) or reconstruction_delta > RECONSTRUCTION_TOLERANCE:
        raise ContractError(
            "serialized P * W * R did not reconstruct the existing body assembly; "
            f"delta={reconstruction_delta}"
        )
    slide_delta = common_origin.matrix_max_delta(
        common_origin.matrix_multiply(slide_bind, slide_local), assembly
    )
    return reparsed, {
        "law": "P * W * R = A; W = inverse(P) * A * inverse(R)",
        "coordinate_space": "compiled USP donor-view",
        "P_compiled_magazine_bind": common_origin._serialized_transform(
            common_origin.decompose_trs(magazine_bind, "compiled magazine bind P")
        ),
        "W_wrapper_local": serialized,
        "R_existing_magazine_local": dict(EXPECTED_MAGAZINE_TRANSFORM),
        "A_existing_body_assembly": common_origin._serialized_transform(
            common_origin.decompose_trs(assembly, "existing body assembly A")
        ),
        "reconstructed_A": common_origin._serialized_transform(
            common_origin.decompose_trs(reconstructed, "reconstructed assembly A")
        ),
        "reconstruction_max_delta": reconstruction_delta,
        "tolerance": RECONSTRUCTION_TOLERANCE,
        "preserved_slide_compiled_bind_to_body_max_delta": slide_delta,
    }


def _make_wrapper(
    magazine_renderer: dict[str, Any], transform: common_origin.Transform
) -> dict[str, Any]:
    wrapper = copy.deepcopy(magazine_renderer)
    wrapper["__guid"] = WRAPPER_GUID
    wrapper["Name"] = WRAPPER_NAME
    wrapper.update(common_origin._serialized_transform(transform))
    wrapper["Components"] = []
    wrapper["Children"] = [magazine_renderer]
    return wrapper


def transform_prefab(
    source: dict[str, Any], source_bytes: bytes
) -> tuple[dict[str, Any], dict[str, object]]:
    before_index, _ = validate_active_prefab(source, source_bytes)
    before_guids = set(before_index.all_guids)
    before_references = _reference_counter(source)
    wrapper_transform, transform_report = derive_magazine_wrapper(before_index)

    candidate = copy.deepcopy(source)
    candidate_index = common_origin.index_prefab(candidate)
    root = candidate["RootObject"]
    view_model = next(
        component
        for component in root["Components"]
        if component.get("__guid") == VIEW_MODEL_GUID
    )
    magazine_parent = candidate_index.nodes_by_path[MAGAZINE_PARENT_PATH]
    magazine_renderer = magazine_parent["Children"][0]
    magazine_parent["Children"] = [
        _make_wrapper(magazine_renderer, wrapper_transform)
    ]
    view_model["AdditionalRendererRoot"] = {
        "_type": "gameobject",
        "go": CUSTOM_ROOT_GUID,
    }

    after_index = common_origin.index_prefab(candidate)
    common_origin.assert_references_valid(candidate, after_index)
    if after_index.all_guids - before_guids != {WRAPPER_GUID}:
        raise ContractError("candidate GUID delta is not exactly the wrapper GUID")
    if before_guids - after_index.all_guids:
        raise ContractError("candidate removed an existing GUID")
    if after_index.component_owners != before_index.component_owners:
        raise ContractError("candidate changed an existing component owner")
    expected_references = before_references.copy()
    expected_references[("gameobject", CUSTOM_ROOT_GUID, None)] += 1
    if _reference_counter(candidate) != expected_references:
        raise ContractError("candidate existing-reference graph drifted")

    wrapper = after_index.nodes_by_guid[WRAPPER_GUID]
    if after_index.parent_by_guid[WRAPPER_GUID] != MAGAZINE_PARENT_GUID:
        raise ContractError("wrapper parent is not the pinned USP magazine node")
    if after_index.parent_by_guid[MAGAZINE_RENDERER_GUID] != WRAPPER_GUID:
        raise ContractError("existing magazine renderer is not the wrapper child")
    if wrapper.get("Components") != [] or len(wrapper.get("Children", [])) != 1:
        raise ContractError("wrapper must be transform-only with one existing child")

    stripped = copy.deepcopy(candidate)
    stripped_index = common_origin.index_prefab(stripped)
    stripped_view_model = stripped_index.components_by_guid[VIEW_MODEL_GUID]
    removed_reference = stripped_view_model.pop("AdditionalRendererRoot", None)
    stripped_parent = stripped_index.nodes_by_guid[MAGAZINE_PARENT_GUID]
    stripped_wrapper = stripped_parent["Children"][0]
    stripped_parent["Children"] = stripped_wrapper["Children"]
    if removed_reference != {"_type": "gameobject", "go": CUSTOM_ROOT_GUID}:
        raise ContractError("candidate visibility-root property drifted")
    if stripped != source:
        raise ContractError("candidate changed more than wrapper structure/property")

    return candidate, {
        "semantic_change": (
            "insert one magazine bind wrapper and add AdditionalRendererRoot only"
        ),
        "wrapper_guid": WRAPPER_GUID,
        "wrapper_name": WRAPPER_NAME,
        "wrapper_parent_guid": MAGAZINE_PARENT_GUID,
        "wrapper_child_existing_guid": MAGAZINE_RENDERER_GUID,
        "additional_renderer_root": CUSTOM_ROOT_GUID,
        "existing_guid_count": len(before_guids),
        "candidate_guid_count": len(after_index.all_guids),
        "existing_guids_preserved": True,
        "existing_component_owners_preserved": True,
        "existing_references_preserved": True,
        "body_transform_preserved": dict(EXPECTED_BODY_TRANSFORM),
        "slide_transform_preserved": dict(EXPECTED_SLIDE_TRANSFORM),
        "magazine_renderer_transform_preserved": dict(
            EXPECTED_MAGAZINE_TRANSFORM
        ),
        "transform": transform_report,
    }


def _prepare_output_directory(output_directory: Path | None) -> Path:
    if output_directory is None:
        created = Path(tempfile.mkdtemp(prefix="dxrp_deserteagle_minimal_"))
        pipeline_io.assert_temp_destination(created / OUTPUT_NAME)
        return created.resolve(strict=True)

    lexical = Path(os.path.abspath(os.fspath(output_directory)))
    destination = pipeline_io.assert_temp_destination(lexical / OUTPUT_NAME)
    resolved_directory = destination.parent
    if resolved_directory == SYSTEM_TEMP_ROOT:
        raise ContractError("output directory must be a strict child of system TEMP")
    if resolved_directory.exists():
        if not resolved_directory.is_dir():
            raise ContractError(f"output path is not a directory: {resolved_directory}")
        if any(resolved_directory.iterdir()):
            raise ContractError(f"output directory must be empty: {resolved_directory}")
    else:
        try:
            resolved_directory.mkdir(parents=False, exist_ok=False)
        except OSError as exc:
            raise ContractError(
                f"cannot create TEMP output directory {resolved_directory}: {exc}"
            ) from exc
    pipeline_io.assert_temp_destination(resolved_directory / OUTPUT_NAME)
    return resolved_directory


def build_candidate(output_directory: Path | None = None) -> dict[str, object]:
    validate_source_pins()
    active_bytes = _verify_file_pin(
        ACTIVE_PREFAB_PATH,
        EXPECTED_ACTIVE_BYTES,
        EXPECTED_ACTIVE_SHA256,
        "active Desert Eagle prefab",
    )
    source = common_origin._decode_prefab(active_bytes, "active Desert Eagle")
    candidate, proof = transform_prefab(source, active_bytes)
    candidate_bytes = common_origin._serialize_prefab(candidate, active_bytes)
    reparsed = common_origin._decode_prefab(candidate_bytes, "serialized candidate")
    if reparsed != candidate:
        raise ContractError("serialized candidate changed after JSON round trip")
    reparsed_index = common_origin.index_prefab(reparsed)
    common_origin.assert_references_valid(reparsed, reparsed_index)
    if WRAPPER_GUID not in reparsed_index.nodes_by_guid:
        raise ContractError("serialized candidate lost the magazine wrapper")

    output_directory = _prepare_output_directory(output_directory)
    output = pipeline_io.write_bytes_atomic(
        output_directory / OUTPUT_NAME, candidate_bytes
    )
    if output.read_bytes() != candidate_bytes:
        raise ContractError("TEMP candidate bytes changed after atomic write")

    return {
        "result": "PASS",
        "mode": "TEMP_ONLY_NO_PRODUCT_WRITE_MODE",
        "output": {
            "path": str(output),
            "bytes": len(candidate_bytes),
            "sha256": _sha256(candidate_bytes),
        },
        "inputs": {
            "active_prefab": {
                "path": str(ACTIVE_PREFAB_PATH.resolve()),
                "bytes": EXPECTED_ACTIVE_BYTES,
                "sha256": EXPECTED_ACTIVE_SHA256,
            },
            "compiled_usp": {
                "path": str(COMPILED_BIND_MODEL_PATH),
                "bytes": COMPILED_BIND_MODEL_BYTES,
                "sha256": COMPILED_BIND_MODEL_SHA256,
            },
            "parser": {
                "path": str(COMPILED_BIND_PARSER_PATH),
                "bytes": COMPILED_BIND_PARSER_BYTES,
                "sha256": COMPILED_BIND_PARSER_SHA256,
                "version": COMPILED_BIND_PARSER_VERSION,
                "command_mode": "-b DATA",
            },
            "source_models_and_materials": _source_pin_report(),
            "helpers": {
                "common_origin": {
                    "path": str(COMMON_ORIGIN_HELPER_PATH),
                    "bytes": EXPECTED_COMMON_ORIGIN_HELPER_BYTES,
                    "sha256": EXPECTED_COMMON_ORIGIN_HELPER_SHA256,
                },
                "pipeline_io": {
                    "path": str(PIPELINE_IO_PATH),
                    "bytes": EXPECTED_PIPELINE_IO_BYTES,
                    "sha256": EXPECTED_PIPELINE_IO_SHA256,
                },
            },
        },
        "proof": proof,
        "writes": {
            "system_temp": True,
            "game": False,
            "docs": False,
            "editor": False,
            "portal": False,
            "git": False,
        },
        "proof_ceiling": (
            "exact byte custody plus deterministic TEMP serialization and static "
            "P*W*R reconstruction only; product write, asset/code compile, editor, "
            "runtime, animation, visual fit, Portal, and deployment are UNVERIFIED"
        ),
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build one exact-pin Desert Eagle minimal correction below system "
            "TEMP. No product-write mode exists."
        )
    )
    parser.add_argument(
        "--output-directory",
        type=Path,
        help=(
            "Empty strict-system-TEMP directory; omitted to create a fresh one."
        ),
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        report = build_candidate(args.output_directory)
    except (ContractError, OSError, RuntimeError) as exc:
        print(f"DXRP_DESERTEAGLE_MINIMAL_ERROR: {exc}", file=sys.stderr)
        return 2
    print("DXRP_DESERTEAGLE_MINIMAL=" + json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
