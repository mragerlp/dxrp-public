#!/usr/bin/env python3
"""Build one deterministic TEMP-only M1911 common-origin view candidate.

The active M1911 view prefab is a USP prefab-instance patch.  The pinned
``build_m1911_view_prefab.py`` materializer expands that patch in memory so the
result owns one direct root ``ViewModel`` component.  This builder then uses an
external, full-SHA-pinned compiled-USP bind manifest to correct the three rigid
common-origin overlays.

The existing authored body local is retained only as a non-final structural
seed.  Assembly ``A`` is ``B_root * L_body`` and every part receives
``L = inverse(B_part) * A``.  The result is not a hand-fit, ADS, animation, or
visual acceptance.  Output is possible only below system TEMP; no repository,
game-tree, product-write, or acceptance switch exists.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import re
import sys
import tempfile
from dataclasses import dataclass
from itertools import combinations
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

import build_deserteagle_common_origin_candidate as common_origin
import build_m1911_view_prefab as materializer
import pipeline_io


WORKBENCH_ROOT = Path(__file__).resolve().parents[2]
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
DEFAULT_DONOR = materializer.DEFAULT_DONOR.resolve()
DEFAULT_SOURCE = materializer.DEFAULT_PATCH.resolve()
MATERIALIZER_PATH = (
    WORKBENCH_ROOT / "tools/weapon_pipeline/build_m1911_view_prefab.py"
).resolve()
COMMON_ORIGIN_HELPERS_PATH = (
    WORKBENCH_ROOT
    / "tools/weapon_pipeline/build_deserteagle_common_origin_candidate.py"
).resolve()
OUTPUT_NAME = "vm_m1911.common_origin_candidate.prefab"

EXPECTED_DONOR_BYTES = 68_573
EXPECTED_DONOR_SHA256 = (
    "C5621B02C9CE1D15104718DF8DA954696921886DFB16ABEFAD9D46AC3AE83C3C"
)
EXPECTED_SOURCE_BYTES = 13_794
EXPECTED_SOURCE_SHA256 = (
    "6F1FB9FD9024C439F3E5F8CF6448AF3C8FF68AE2D99F021EDC6339431F220ED3"
)
EXPECTED_MATERIALIZER_BYTES = 18_239
EXPECTED_MATERIALIZER_SHA256 = (
    "6FA2DCED4DBF971FC8A1E08BBF4D3964EA749A8D258E641EC7A943B0B35A00AA"
)
EXPECTED_COMMON_ORIGIN_HELPERS_BYTES = 50_237
EXPECTED_COMMON_ORIGIN_HELPERS_SHA256 = (
    "3C2DBD5574C7433422A74EC345BC0CA3B985C374BB2741138EABDD9CFD425446"
)
EXPECTED_MATERIALIZED_BYTES = 73_057
EXPECTED_MATERIALIZED_SHA256 = materializer.EXPECTED_MATERIALIZED_SHA256
EXPECTED_CANDIDATE_BYTES = 73_205
EXPECTED_CANDIDATE_SHA256 = (
    "099EF7340861DB3305E045238BADD0918BC59A518C6E67FC7CC8CCC64732C451"
)

EXPECTED_ROOT_GUID = "190485e7-560a-5317-8cfe-234a3c5a4ea4"
EXPECTED_VIEW_MODEL_GUID = "becae687-6ef6-57de-a240-5bccff1b9a0f"
EXPECTED_DRIVER_GUID = "26f8f728-ca2d-5360-b500-a04a0b455ff4"
EXPECTED_ARMS_GUID = "7a5ace8d-0b51-5834-8c0a-8692de91dc49"
EXPECTED_MUZZLE_GUID = "2f1018d0-3a7c-56b9-808c-f4fb95c26146"
EXPECTED_EJECTION_GUID = "3df5bcb7-967a-54fd-8d9a-31dce27f441a"

BODY_SEED = {
    "Position": "-0.8824501,0.07494241,3.4594364",
    "Rotation": "0,0,0,1",
    "Scale": "1,1,1",
}
BODY_SEED_ACCEPTANCE = {"accepted": False, "visual_accepted": False}
PIN_RE = re.compile(r"^[0-9a-f]{64}$", re.IGNORECASE)


@dataclass(frozen=True)
class PartSpec:
    role: str
    bind_key: str
    name: str
    object_guid: str
    renderer_guid: str
    parent_source_guid: str
    parent_instance_guid: str
    model: str


PART_SPECS = (
    PartSpec(
        role="body",
        bind_key="B_root",
        name="m1911_body",
        object_guid="f3a5a630-0d36-55ef-89ae-30db6d72c007",
        renderer_guid="a2c32a8c-21d4-5dd9-80f9-0806834b5faf",
        parent_source_guid="2f9a4399-5075-4c53-a4f7-6722110fa2e2",
        parent_instance_guid="be655d9a-d1a6-5a37-86ef-693690409455",
        model="addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl",
    ),
    PartSpec(
        role="slide",
        bind_key="B_slide",
        name="m1911_slide",
        object_guid="6a1eb4d5-fd63-5899-b973-3b628482aca4",
        renderer_guid="d330855c-40eb-5f97-b652-c1f0abbb5547",
        parent_source_guid="38b6ec59-6293-4ece-901a-0868678dcd2c",
        parent_instance_guid="3df5bcb7-967a-54fd-8d9a-31dce27f441a",
        model="addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl",
    ),
    PartSpec(
        role="magazine",
        bind_key="B_mag",
        name="m1911_magazine",
        object_guid="ad1196a1-5216-5d92-a93a-be54233bec30",
        renderer_guid="51ca9973-8792-529d-b1e7-21b9f7b50fa5",
        parent_source_guid="56d6853a-544c-46a5-b70a-380bd2a745c8",
        parent_instance_guid="6204a3dd-86f0-5750-8069-8feccc8fe446",
        model="addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl",
    ),
)


class ContractError(RuntimeError):
    """Raised before output when a custody or transform contract drifts."""


@dataclass
class PrefabIndex:
    nodes_by_guid: dict[str, dict[str, Any]]
    components_by_guid: dict[str, dict[str, Any]]
    component_owners: dict[str, str]
    parent_by_guid: dict[str, str | None]

    @property
    def all_guids(self) -> set[str]:
        return set(self.nodes_by_guid) | set(self.components_by_guid)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _pin_payload(path: Path, data: bytes) -> dict[str, object]:
    return {
        "path": str(path.resolve()),
        "bytes": len(data),
        "sha256": _sha256(data),
    }


def _normalized_pin(value: object, label: str) -> str:
    if not isinstance(value, str) or not PIN_RE.fullmatch(value):
        raise ContractError(f"{label} must be one full SHA-256 value")
    return value.upper()


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


def _manifest_pin(bytes_count: int, sha256: str) -> dict[str, object]:
    return {"bytes": bytes_count, "sha256": sha256}


def compiled_bind_manifest_template() -> dict[str, object]:
    """Return the exact external manifest accepted by this builder."""

    return {
        "version": 1,
        "pose": "compiled-bind",
        "coordinate_space": "donor-view",
        "acceptance": copy.deepcopy(BODY_SEED_ACCEPTANCE),
        "body_seed": copy.deepcopy(BODY_SEED),
        "pins": {
            "donor": _manifest_pin(EXPECTED_DONOR_BYTES, EXPECTED_DONOR_SHA256),
            "source": _manifest_pin(EXPECTED_SOURCE_BYTES, EXPECTED_SOURCE_SHA256),
            "materializer": _manifest_pin(
                EXPECTED_MATERIALIZER_BYTES, EXPECTED_MATERIALIZER_SHA256
            ),
            "common_origin_helpers": _manifest_pin(
                EXPECTED_COMMON_ORIGIN_HELPERS_BYTES,
                EXPECTED_COMMON_ORIGIN_HELPERS_SHA256,
            ),
        },
        "evidence": common_origin.compiled_bind_evidence_template(),
        "transforms": copy.deepcopy(common_origin.COMPILED_BIND_TRANSFORMS),
    }


def compiled_bind_manifest_bytes() -> bytes:
    return (
        json.dumps(
            compiled_bind_manifest_template(),
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")


def _validate_manifest_pin(
    value: object,
    *,
    label: str,
    expected_bytes: int,
    expected_sha256: str,
) -> None:
    expected = _manifest_pin(expected_bytes, expected_sha256)
    if value != expected:
        raise ContractError(
            f"compiled-bind pins.{label} changed: expected {expected}, got {value}"
        )


def load_compiled_bind_manifest(
    path: Path,
    expected_sha256: str,
    *,
    donor_bytes: bytes,
    source_bytes: bytes,
    materializer_bytes: bytes,
    helper_bytes: bytes,
) -> tuple[dict[str, common_origin.Matrix4], dict[str, object], bytes]:
    expected = _normalized_pin(expected_sha256, "compiled-bind JSON SHA-256")
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"cannot read compiled-bind JSON {path}: {exc}") from exc
    actual = _sha256(raw)
    if actual != expected:
        raise ContractError(
            f"compiled-bind JSON SHA-256 mismatch: expected {expected}, got {actual}"
        )
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"compiled-bind JSON is invalid: {exc}") from exc

    required_top = {
        "version",
        "pose",
        "coordinate_space",
        "acceptance",
        "body_seed",
        "pins",
        "evidence",
        "transforms",
    }
    if not isinstance(payload, dict) or set(payload) != required_top:
        raise ContractError("compiled-bind JSON contains missing or extra fields")
    if payload["version"] != 1 or payload["pose"] != "compiled-bind":
        raise ContractError("compiled-bind JSON must be version 1 and compiled-bind pose")
    if payload["coordinate_space"] != "donor-view":
        raise ContractError("compiled-bind coordinate_space must be donor-view")
    if payload["acceptance"] != BODY_SEED_ACCEPTANCE:
        raise ContractError("body seed must remain explicitly unaccepted and visual false")
    if payload["body_seed"] != BODY_SEED:
        raise ContractError("compiled-bind body seed changed")

    pins = payload["pins"]
    if not isinstance(pins, dict) or set(pins) != {
        "donor",
        "source",
        "materializer",
        "common_origin_helpers",
    }:
        raise ContractError("compiled-bind pins set changed")
    _validate_manifest_pin(
        pins["donor"],
        label="donor",
        expected_bytes=len(donor_bytes),
        expected_sha256=_sha256(donor_bytes),
    )
    _validate_manifest_pin(
        pins["source"],
        label="source",
        expected_bytes=len(source_bytes),
        expected_sha256=_sha256(source_bytes),
    )
    _validate_manifest_pin(
        pins["materializer"],
        label="materializer",
        expected_bytes=len(materializer_bytes),
        expected_sha256=_sha256(materializer_bytes),
    )
    _validate_manifest_pin(
        pins["common_origin_helpers"],
        label="common_origin_helpers",
        expected_bytes=len(helper_bytes),
        expected_sha256=_sha256(helper_bytes),
    )

    expected_evidence = common_origin.compiled_bind_evidence_template()
    if payload["evidence"] != expected_evidence:
        raise ContractError("compiled-bind parser/model evidence changed")
    if payload["transforms"] != common_origin.COMPILED_BIND_TRANSFORMS:
        raise ContractError("compiled-bind transforms changed from pinned USP DATA")
    common_origin._verify_external_pin(
        common_origin.COMPILED_BIND_PARSER_PATH,
        common_origin.COMPILED_BIND_PARSER_BYTES,
        common_origin.COMPILED_BIND_PARSER_SHA256,
        "parser",
    )
    common_origin._verify_external_pin(
        common_origin.COMPILED_BIND_MODEL_PATH,
        common_origin.COMPILED_BIND_MODEL_BYTES,
        common_origin.COMPILED_BIND_MODEL_SHA256,
        "compiled USP model",
    )

    required = {spec.bind_key for spec in PART_SPECS}
    transforms = payload["transforms"]
    if not isinstance(transforms, dict) or set(transforms) != required:
        raise ContractError("compiled-bind JSON requires B_root/B_slide/B_mag")
    matrices = {
        key: common_origin._parse_manifest_transform(transforms[key], key)
        for key in sorted(required)
    }
    return matrices, _pin_payload(path, raw), raw


def _walk_nodes(
    root: dict[str, Any], parent_guid: str | None = None
) -> Iterable[tuple[dict[str, Any], str | None]]:
    yield root, parent_guid
    current_guid = root.get("__guid")
    if not isinstance(current_guid, str):
        raise ContractError("prefab object is missing a GUID")
    for child in root.get("Children", []) or []:
        if not isinstance(child, dict):
            raise ContractError(f"invalid child below {current_guid}")
        yield from _walk_nodes(child, current_guid)


def index_prefab(document: dict[str, Any]) -> PrefabIndex:
    root = document.get("RootObject")
    if not isinstance(root, dict):
        raise ContractError("prefab document has no RootObject")
    nodes: dict[str, dict[str, Any]] = {}
    components: dict[str, dict[str, Any]] = {}
    component_owners: dict[str, str] = {}
    parents: dict[str, str | None] = {}
    for node, parent_guid in _walk_nodes(root):
        guid = node.get("__guid")
        if not isinstance(guid, str) or guid in nodes or guid in components:
            raise ContractError(f"duplicate or invalid object GUID: {guid}")
        nodes[guid] = node
        parents[guid] = parent_guid
        for component in node.get("Components", []) or []:
            if not isinstance(component, dict):
                raise ContractError(f"invalid component on {guid}")
            component_guid = component.get("__guid")
            if (
                not isinstance(component_guid, str)
                or component_guid in nodes
                or component_guid in components
            ):
                raise ContractError(
                    f"duplicate or invalid component GUID: {component_guid}"
                )
            components[component_guid] = component
            component_owners[component_guid] = guid
    return PrefabIndex(nodes, components, component_owners, parents)


def _typed_references(value: Any) -> Iterable[tuple[str, str]]:
    if isinstance(value, dict):
        reference_type = value.get("_type")
        if reference_type == "gameobject" and isinstance(value.get("go"), str):
            yield "gameobject", value["go"]
        elif reference_type == "component":
            if isinstance(value.get("go"), str):
                yield "gameobject", value["go"]
            if isinstance(value.get("component_id"), str):
                yield "component", value["component_id"]
        for item in value.values():
            yield from _typed_references(item)
    elif isinstance(value, list):
        for item in value:
            yield from _typed_references(item)


def assert_references_valid(document: dict[str, Any], index: PrefabIndex) -> int:
    count = 0
    for kind, guid in _typed_references(document):
        definitions = (
            index.nodes_by_guid if kind == "gameobject" else index.components_by_guid
        )
        if guid not in definitions:
            raise ContractError(f"dangling typed {kind} reference: {guid}")
        count += 1
    if count == 0:
        raise ContractError("prefab contains no typed object/component references")
    return count


def _component_reference(value: object, key: str) -> str | None:
    if not isinstance(value, dict):
        return None
    target = value.get(key)
    return target if isinstance(target, str) else None


def _part_renderer(
    index: PrefabIndex, spec: PartSpec, node: dict[str, Any]
) -> dict[str, Any]:
    renderers = [
        component
        for component in node.get("Components", []) or []
        if component.get("__type") == "Sandbox.ModelRenderer"
    ]
    if len(renderers) != 1:
        raise ContractError(f"{spec.name} must own exactly one ModelRenderer")
    renderer = renderers[0]
    if renderer.get("__guid") != spec.renderer_guid:
        raise ContractError(f"{spec.name} renderer GUID changed")
    if renderer.get("Model") != spec.model:
        raise ContractError(f"{spec.name} model changed")
    if index.component_owners.get(spec.renderer_guid) != spec.object_guid:
        raise ContractError(f"{spec.name} renderer owner changed")
    return renderer


def validate_materialized_contract(
    document: dict[str, Any], *, require_authored_seed: bool
) -> tuple[PrefabIndex, dict[str, Any]]:
    if list(document) != materializer.EXPECTED_DOCUMENT_KEYS:
        raise ContractError("materialized M1911 document envelope keys changed")
    envelope = {
        key: document[key]
        for key in materializer.EXPECTED_DOCUMENT_KEYS
        if key != "RootObject"
    }
    if envelope != materializer.EXPECTED_DOCUMENT_ENVELOPE:
        raise ContractError("materialized M1911 resource envelope changed")

    root = document["RootObject"]
    if root.get("__guid") != EXPECTED_ROOT_GUID or root.get("Name") != "vm_m1911":
        raise ContractError("materialized M1911 root identity changed")
    if any(
        key in root
        for key in ("__Prefab", "__PrefabInstancePatch", "__PrefabIdToInstanceId")
    ):
        raise ContractError("materialized M1911 still contains prefab-instance metadata")

    index = index_prefab(document)
    view_models = [
        component
        for component in root.get("Components", []) or []
        if component.get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    if len(view_models) != 1 or view_models[0].get("__guid") != EXPECTED_VIEW_MODEL_GUID:
        raise ContractError("materialized root must own the exact single ViewModel")
    view_model = view_models[0]
    if index.component_owners.get(EXPECTED_VIEW_MODEL_GUID) != EXPECTED_ROOT_GUID:
        raise ContractError("ViewModel is not owned by the materialized root")
    expected_references = {
        "Muzzle": EXPECTED_MUZZLE_GUID,
        "EjectionPort": EXPECTED_EJECTION_GUID,
        "ModelRenderer": EXPECTED_DRIVER_GUID,
        "Arms": EXPECTED_ARMS_GUID,
    }
    for field, expected_guid in expected_references.items():
        key = "go" if field in {"Muzzle", "EjectionPort"} else "component_id"
        if _component_reference(view_model.get(field), key) != expected_guid:
            raise ContractError(f"ViewModel {field} reference changed")
    if index.component_owners.get(EXPECTED_DRIVER_GUID) != EXPECTED_ROOT_GUID:
        raise ContractError("USP driver is not owned by the materialized root")
    if index.component_owners.get(EXPECTED_ARMS_GUID) != EXPECTED_ROOT_GUID:
        raise ContractError("first-person arms are not owned by the materialized root")

    rows: dict[str, Any] = {}
    for spec in PART_SPECS:
        materializer_spec = materializer.EXPECTED_PARTS.get(spec.name)
        if materializer_spec != {
            "guid": spec.object_guid,
            "parent_source_guid": spec.parent_source_guid,
            "parent_instance_guid": spec.parent_instance_guid,
            "model": spec.model,
        }:
            raise ContractError(f"materializer contract drifted for {spec.name}")
        node = index.nodes_by_guid.get(spec.object_guid)
        if node is None or node.get("Name") != spec.name:
            raise ContractError(f"missing exact object GUID/name for {spec.name}")
        if index.parent_by_guid.get(spec.object_guid) != spec.parent_instance_guid:
            raise ContractError(f"{spec.name} parent GUID changed")
        _part_renderer(index, spec, node)
        if require_authored_seed and any(
            node.get(field) != value for field, value in BODY_SEED.items()
        ):
            raise ContractError(f"{spec.name} authored common-origin seed changed")
        rows[spec.role] = {
            "name": spec.name,
            "object_guid": spec.object_guid,
            "renderer_guid": spec.renderer_guid,
            "parent_source_guid": spec.parent_source_guid,
            "parent_instance_guid": spec.parent_instance_guid,
            "model": spec.model,
        }

    references = assert_references_valid(document, index)
    return index, {
        "root_guid": EXPECTED_ROOT_GUID,
        "view_model_guid": EXPECTED_VIEW_MODEL_GUID,
        "view_model_components": len(view_models),
        "valid_typed_references": references,
        "parts": rows,
    }


def _body_seed_transform() -> common_origin.Transform:
    return common_origin.Transform(
        position=common_origin._parse_sbox_vector(
            BODY_SEED["Position"], 3, "body seed Position"
        ),  # type: ignore[arg-type]
        quaternion=common_origin._parse_sbox_vector(
            BODY_SEED["Rotation"], 4, "body seed Rotation"
        ),  # type: ignore[arg-type]
        scale=common_origin._parse_sbox_vector(
            BODY_SEED["Scale"], 3, "body seed Scale"
        ),  # type: ignore[arg-type]
    )


def derive_assembly(
    bind_matrices: Mapping[str, common_origin.Matrix4]
) -> common_origin.Transform:
    body_matrix = common_origin.transform_matrix(_body_seed_transform())
    assembly_matrix = common_origin.matrix_multiply(
        bind_matrices["B_root"], body_matrix
    )
    return common_origin.decompose_trs(assembly_matrix, "M1911 assembly A")


def _assert_distinct_moving_parent_locals(
    bind_matrices: Mapping[str, common_origin.Matrix4],
    locals_by_bind: Mapping[str, common_origin.Transform],
) -> None:
    matrices = {
        key: common_origin.transform_matrix(value)
        for key, value in locals_by_bind.items()
    }
    for first, second in combinations(sorted(bind_matrices), 2):
        bind_delta = common_origin.matrix_max_delta(
            bind_matrices[first], bind_matrices[second]
        )
        local_delta = common_origin.matrix_max_delta(matrices[first], matrices[second])
        if (
            bind_delta > common_origin.TRS_TOLERANCE
            and local_delta <= common_origin.TRS_TOLERANCE
        ):
            raise ContractError(
                "copied local transform rejected across distinct moving parents: "
                f"{first}/{second}"
            )


def transform_document(
    document: dict[str, Any],
    bind_matrices: Mapping[str, common_origin.Matrix4],
) -> tuple[dict[str, Any], dict[str, Any]]:
    validate_materialized_contract(document, require_authored_seed=True)
    assembly = derive_assembly(bind_matrices)
    locals_by_bind, reconstruction_errors = common_origin.compute_locals(
        bind_matrices, assembly
    )
    _assert_distinct_moving_parent_locals(bind_matrices, locals_by_bind)

    candidate = copy.deepcopy(document)
    index = index_prefab(candidate)
    part_rows: dict[str, Any] = {}
    for spec in PART_SPECS:
        node = index.nodes_by_guid[spec.object_guid]
        local = locals_by_bind[spec.bind_key]
        serialized = common_origin._serialized_transform(local)
        node.update(serialized)
        part_rows[spec.role] = {
            "name": spec.name,
            "bind": spec.bind_key,
            "object_guid": spec.object_guid,
            "renderer_guid": spec.renderer_guid,
            "parent_source_guid": spec.parent_source_guid,
            "parent_instance_guid": spec.parent_instance_guid,
            "model": spec.model,
            "local": serialized,
            "reconstruction_max_delta": reconstruction_errors[spec.bind_key],
        }

    _, contract = validate_materialized_contract(
        candidate, require_authored_seed=False
    )
    assembly_matrix = common_origin.transform_matrix(assembly)
    serialized_locals = {
        spec.bind_key: common_origin.transform_matrix(
            common_origin.Transform(
                position=common_origin._parse_sbox_vector(
                    part_rows[spec.role]["local"]["Position"], 3, "Position"
                ),  # type: ignore[arg-type]
                quaternion=common_origin._parse_sbox_vector(
                    part_rows[spec.role]["local"]["Rotation"], 4, "Rotation"
                ),  # type: ignore[arg-type]
                scale=common_origin._parse_sbox_vector(
                    part_rows[spec.role]["local"]["Scale"], 3, "Scale"
                ),  # type: ignore[arg-type]
            )
        )
        for spec in PART_SPECS
    }
    reconstruction = common_origin.assert_local_reconstruction(
        bind_matrices, serialized_locals, assembly_matrix
    )
    return candidate, {
        "assembly_A": common_origin._serialized_transform(assembly),
        "assembly_derivation": "B_root * existing non-final M1911 body seed",
        "body_seed": {
            **copy.deepcopy(BODY_SEED),
            **copy.deepcopy(BODY_SEED_ACCEPTANCE),
        },
        "parts": part_rows,
        "reconstruction": "PASS",
        "reconstruction_max_delta": max(reconstruction.values()),
        "copied_local_guard": "PASS",
        "contract": contract,
    }


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def assert_temp_output_directory(output_directory: Path) -> Path:
    resolved = output_directory.resolve(strict=False)
    if _is_within(resolved, WORKBENCH_ROOT):
        raise ContractError("repository output is impossible for this TEMP-only builder")
    if resolved == SYSTEM_TEMP_ROOT or not _is_within(resolved, SYSTEM_TEMP_ROOT):
        raise ContractError("TEMP-only output must be a strict child of system TEMP")
    if resolved.exists():
        if not resolved.is_dir():
            raise ContractError("TEMP-only output path exists and is not a directory")
        if any(resolved.iterdir()):
            raise ContractError("TEMP-only output directory must be empty")
    return resolved


def write_candidate(path: Path, data: bytes) -> Path:
    """Create one candidate through a missing-destination promotion snapshot."""

    resolved = pipeline_io.assert_temp_destination(path)
    if _is_within(resolved, WORKBENCH_ROOT):
        raise ContractError("repository candidate path is impossible")
    if resolved.parent.exists() and any(resolved.parent.iterdir()):
        raise ContractError("TEMP-only output directory must be empty")
    resolved.parent.mkdir(parents=True, exist_ok=True)
    staged = pipeline_io.staging_path(resolved)
    stage_created = False
    try:
        # staging_path snapshots the final leaf.  Checking for an existing leaf
        # after that snapshot closes the create-only gap: an arrival before this
        # check is rejected here, and a later arrival is rejected by
        # promote_files because it differs from the registered missing snapshot.
        if resolved.exists() or resolved.is_symlink():
            raise ContractError(
                "candidate destination appeared during create-only staging"
            )
        if any(resolved.parent.iterdir()):
            raise ContractError(
                "TEMP-only output directory became nonempty during staging"
            )
        with staged.open("xb") as stream:
            stage_created = True
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        if staged.read_bytes() != data:
            raise ContractError("staged candidate bytes changed before promotion")
        pipeline_io.promote_files([(staged, resolved)])
    finally:
        cleanup_complete = not (staged.exists() or staged.is_symlink())
        if stage_created and (staged.exists() or staged.is_symlink()):
            try:
                staged.unlink()
            except OSError as exc:
                print(
                    "DXRP_M1911_COMMON_ORIGIN_CLEANUP_WARNING: staged output "
                    f"remains at {staged}: {exc}",
                    file=sys.stderr,
                )
            else:
                cleanup_complete = True
        if cleanup_complete:
            pipeline_io._staged_files.discard(staged)
            pipeline_io._file_destinations.pop(staged, None)
    return resolved


def build_candidate(
    *,
    output_directory: Path,
    compiled_bind_path: Path,
    compiled_bind_sha256: str,
) -> dict[str, object]:
    output_directory = assert_temp_output_directory(output_directory)
    if Path(materializer.__file__).resolve() != MATERIALIZER_PATH:
        raise ContractError("imported M1911 materializer path changed")
    if Path(common_origin.__file__).resolve() != COMMON_ORIGIN_HELPERS_PATH:
        raise ContractError("imported common-origin helper path changed")
    donor_bytes = _verify_file_pin(
        DEFAULT_DONOR, EXPECTED_DONOR_BYTES, EXPECTED_DONOR_SHA256, "USP donor"
    )
    source_bytes = _verify_file_pin(
        DEFAULT_SOURCE, EXPECTED_SOURCE_BYTES, EXPECTED_SOURCE_SHA256, "M1911 source"
    )
    materializer_bytes = _verify_file_pin(
        MATERIALIZER_PATH,
        EXPECTED_MATERIALIZER_BYTES,
        EXPECTED_MATERIALIZER_SHA256,
        "M1911 materializer",
    )
    helper_bytes = _verify_file_pin(
        COMMON_ORIGIN_HELPERS_PATH,
        EXPECTED_COMMON_ORIGIN_HELPERS_BYTES,
        EXPECTED_COMMON_ORIGIN_HELPERS_SHA256,
        "common-origin helpers",
    )
    bind_matrices, manifest_pin, manifest_bytes = load_compiled_bind_manifest(
        compiled_bind_path,
        compiled_bind_sha256,
        donor_bytes=donor_bytes,
        source_bytes=source_bytes,
        materializer_bytes=materializer_bytes,
        helper_bytes=helper_bytes,
    )

    materialized, materialized_report = materializer.materialize(
        DEFAULT_DONOR, DEFAULT_SOURCE
    )
    materialized_bytes = materializer.serialize_document(materialized)
    if len(materialized_bytes) != EXPECTED_MATERIALIZED_BYTES:
        raise ContractError("in-memory materialized M1911 byte count changed")
    if _sha256(materialized_bytes) != EXPECTED_MATERIALIZED_SHA256:
        raise ContractError("in-memory materialized M1911 SHA-256 changed")

    candidate, transform_report = transform_document(materialized, bind_matrices)
    candidate_bytes = materializer.serialize_document(candidate)
    if len(candidate_bytes) != EXPECTED_CANDIDATE_BYTES:
        raise ContractError(
            "M1911 common-origin candidate byte count changed: "
            f"expected {EXPECTED_CANDIDATE_BYTES}, got {len(candidate_bytes)}"
        )
    candidate_sha = _sha256(candidate_bytes)
    if candidate_sha != EXPECTED_CANDIDATE_SHA256:
        raise ContractError(
            "M1911 common-origin candidate SHA-256 changed: "
            f"expected {EXPECTED_CANDIDATE_SHA256}, got {candidate_sha}"
        )

    output_directory = assert_temp_output_directory(output_directory)
    output = output_directory / OUTPUT_NAME
    write_candidate(output, candidate_bytes)
    if output.read_bytes() != candidate_bytes:
        raise ContractError("TEMP candidate bytes changed during atomic write")
    written = json.loads(output.read_text(encoding="utf-8-sig"))
    validate_materialized_contract(written, require_authored_seed=False)

    source_snapshots = {
        DEFAULT_DONOR: donor_bytes,
        DEFAULT_SOURCE: source_bytes,
        MATERIALIZER_PATH: materializer_bytes,
        COMMON_ORIGIN_HELPERS_PATH: helper_bytes,
        compiled_bind_path: manifest_bytes,
    }
    changed = [
        str(path)
        for path, before in source_snapshots.items()
        if path.read_bytes() != before
    ]
    if changed:
        raise ContractError("source nonmutation check failed: " + ", ".join(changed))

    return {
        "result": "PASS",
        "mode": "TEMP_ONLY",
        "output": _pin_payload(output, candidate_bytes),
        "inputs": {
            "donor": _pin_payload(DEFAULT_DONOR, donor_bytes),
            "source": _pin_payload(DEFAULT_SOURCE, source_bytes),
            "materializer": _pin_payload(MATERIALIZER_PATH, materializer_bytes),
            "common_origin_helpers": _pin_payload(
                COMMON_ORIGIN_HELPERS_PATH, helper_bytes
            ),
            "compiled_bind": manifest_pin,
            "parser": {
                "path": str(common_origin.COMPILED_BIND_PARSER_PATH),
                "bytes": common_origin.COMPILED_BIND_PARSER_BYTES,
                "sha256": common_origin.COMPILED_BIND_PARSER_SHA256,
                "version": common_origin.COMPILED_BIND_PARSER_VERSION,
            },
            "compiled_model": {
                "path": str(common_origin.COMPILED_BIND_MODEL_PATH),
                "bytes": common_origin.COMPILED_BIND_MODEL_BYTES,
                "sha256": common_origin.COMPILED_BIND_MODEL_SHA256,
            },
        },
        "materializer_report": materialized_report,
        "transform_report": transform_report,
        "acceptance": copy.deepcopy(BODY_SEED_ACCEPTANCE),
        "source_nonmutation": "PASS",
        "game_tree_written": False,
        "proof_ceiling": (
            "deterministic TEMP structural candidate from compiled USP bind; "
            "the retained body seed is explicitly unaccepted and visual false; "
            "observed IdlePose, asset compile, editor, runtime, animation, hand fit, "
            "ADS, reload, muzzle/ejection alignment, and visual acceptance are UNVERIFIED"
        ),
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build one deterministic TEMP-only M1911 common-origin candidate. "
            "No output path, game-tree write, or acceptance mode exists."
        )
    )
    parser.add_argument(
        "--compiled-bind-json",
        type=Path,
        help="External version-1 compiled-USP bind manifest (read only).",
    )
    parser.add_argument(
        "--compiled-bind-sha256",
        help="Expected full SHA-256 of the compiled-bind manifest bytes.",
    )
    parser.add_argument(
        "--print-compiled-bind-manifest",
        action="store_true",
        help="Print the exact accepted manifest template and exit without writing.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    if args.print_compiled_bind_manifest:
        if args.compiled_bind_json is not None or args.compiled_bind_sha256 is not None:
            parser.error(
                "--print-compiled-bind-manifest cannot be combined with build options"
            )
        print(json.dumps(compiled_bind_manifest_template(), indent=2))
        return 0
    if args.compiled_bind_json is None or args.compiled_bind_sha256 is None:
        parser.error(
            "--compiled-bind-json and --compiled-bind-sha256 are required to build"
        )

    output_directory = Path(tempfile.mkdtemp(prefix="dxrp_m1911_common_origin_"))
    try:
        report = build_candidate(
            output_directory=output_directory,
            compiled_bind_path=args.compiled_bind_json,
            compiled_bind_sha256=args.compiled_bind_sha256,
        )
    except (ContractError, common_origin.ContractError, OSError, ValueError) as exc:
        parser.exit(2, f"DXRP_M1911_COMMON_ORIGIN_ERROR: {exc}\n")
    print("DXRP_M1911_COMMON_ORIGIN=" + json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
