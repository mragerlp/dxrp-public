"""Validate the pinned TEMP weapon-promotion candidates without product writes.

This is the candidate-aware companion to ``Test-WeaponAnimationHierarchyContract.ps1``.
The PowerShell gate continues to describe the active product tree.  This module
describes the deliberately different renderer-wrapper and minimal-correction
shapes in a review bundle and never overlays them into ``game/``.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


class ContractError(RuntimeError):
    """Raised when the candidate bundle no longer satisfies its pinned contract."""


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSETS_ROOT = REPO_ROOT / "game" / "Assets"
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
REPORT_FILE = "weapon_promotion_bundle_report.json"
EXPECTED_MODE = "TEMP_ONLY_REVIEW_BUNDLE_NO_PRODUCT_WRITE_MODE"
DESERTEAGLE_WRAPPER_GUID = "832b4f45-d26e-5839-9d88-0c0481a31215"
DESERTEAGLE_WRAPPER_NAME = "desert_eagle_magazine_bind_wrapper"
DESERTEAGLE_VIEW_MODEL_GUID = "f1e6fcd8-0f8f-4757-b164-300ceeb9a2c5"
DESERTEAGLE_VISIBILITY_ROOT_GUID = "a69829af-af97-4da5-9c4f-584d43998bd5"
DESERTEAGLE_MAGAZINE_PARENT_GUID = "dff4e566-fa52-4130-bb5d-26c3e951206a"
DESERTEAGLE_MAGAZINE_RENDERER_GUID = "07c7131b-503b-47c3-8780-c3fe309c78d8"
DESERTEAGLE_WRAPPER_FIELDS: Mapping[str, Any] = {
    "__guid": DESERTEAGLE_WRAPPER_GUID,
    "__version": 2,
    "Flags": 0,
    "Name": DESERTEAGLE_WRAPPER_NAME,
    "Position": "-0.81244256471166221,0,-0.15842932801374754",
    "Rotation": "0,-0.79863533644524842,0,0.60181525352967336",
    "Scale": "1.0000000000001106,1,1.0000000000001104",
    "Tags": "",
    "Enabled": True,
    "NetworkMode": 2,
    "NetworkFlags": 0,
    "NetworkOrphaned": 0,
    "NetworkTransmit": True,
    "OwnerTransfer": 1,
    "Components": [],
}
DESERTEAGLE_MINIMAL_BUILDER = (
    REPO_ROOT
    / "tools/weapon_pipeline/build_deserteagle_minimal_correction_candidate.py"
)
DESERTEAGLE_MINIMAL_BUILDER_BYTES = 29_478
DESERTEAGLE_MINIMAL_BUILDER_SHA256 = (
    "786A843C7A68113B36475F55E0F2CF00909C091F2C5A9945AB85EE8F30398585"
)
AKS74U_BOLT_BUILDER = (
    REPO_ROOT / "tools/weapon_pipeline/build_aks74u_bolt_split_candidate.py"
)
AKS74U_BOLT_BUILDER_BYTES = 35_401
AKS74U_BOLT_BUILDER_SHA256 = (
    "5468DABC89C22A48921EA6DA65AFE1525858BA90CF4E5653DE18129721CA22E3"
)
AKS74U_BOLT_WORKER = REPO_ROOT / "tools/weapon_pipeline/blender_split_aks74u_bolt.py"
AKS74U_BOLT_WORKER_BYTES = 25_602
AKS74U_BOLT_WORKER_SHA256 = (
    "F3130E5459233DC7DC651E60C9A047BC89F03120BE2252A05D25AB66B4E9A8FE"
)
AKS74U_BOLT_NODE_GUID = "b583651e-982a-57d4-bcbb-564696b46a23"
AKS74U_BOLT_RENDERER_GUID = "a6151211-0429-5ec4-8a36-acbcc6b40888"
AKS74U_ASSEMBLY_PATH = "vm_aks74u/weapon_root/weapon_root_children"
AKS74U_BOLT_PARENT_PATH = AKS74U_ASSEMBLY_PATH + "/bolt"
AKS74U_BODY_PATH = AKS74U_ASSEMBLY_PATH + "/aks74u_body"
AKS74U_MAGAZINE_PATH = AKS74U_ASSEMBLY_PATH + "/magazine/aks74u_magazine"
AKS74U_SELECTOR_PATH = AKS74U_ASSEMBLY_PATH + "/mode_selector"
AKS74U_TRIGGER_PATH = AKS74U_ASSEMBLY_PATH + "/trigger"
AKS74U_STOCK_PATH = AKS74U_ASSEMBLY_PATH + "/stock"
AKS74U_BODY_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl"
AKS74U_BODY_MINUS_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl"
AKS74U_BOLT_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl"
AKS74U_MAG_MODEL = "addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl"


@dataclass(frozen=True)
class FilePin:
    bytes: int
    sha256: str


@dataclass(frozen=True)
class PartContract:
    model: str
    motion_owner: str
    shape: str = "direct"
    component_type: str = "Sandbox.ModelRenderer"


@dataclass(frozen=True)
class CandidateContract:
    relative_path: str
    destination: str
    root_name: str
    candidate_pin: FilePin
    destination_pin: FilePin
    view_model: bool
    donor_model: str
    donor_material: str
    parts: tuple[PartContract, ...]


@dataclass(frozen=True)
class SupportAssetContract:
    relative_path: str
    source_relative_path: str
    destination: str
    pin: FilePin


def _pin(size: int, sha256: str) -> FilePin:
    return FilePin(size, sha256.upper())


def _part(model: str, owner: str, shape: str = "direct", component_type: str = "Sandbox.ModelRenderer") -> PartContract:
    return PartContract(model, owner, shape, component_type)


CANDIDATES: Mapping[str, CandidateContract] = {
    "ak47_view": CandidateContract(
        "candidates/visibility_roots/vm_ak47.visibility-root.candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab",
        "vm_ak47",
        _pin(76978, "EC32DA9D4116A36B7D800A2731DB9821EDB7C17D0201620E6D5DECB76CA3383F"),
        _pin(76835, "F63B94EE0A127DCC45BED0F2167FD405B9101C2C675B9789DE3FBBDF87C73255"),
        True,
        "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl",
        "addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/invisible.vmat",
        (_part("addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl", "weapon_root"),),
    ),
    "aks74u_view": CandidateContract(
        "candidates/visibility_roots/vm_aks74u.visibility-root.candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab",
        "vm_aks74u",
        _pin(75309, "786D6EE46BF6A96CDA6336582B9B4040C30A0275262089D8CB27B7291A9F8A0F"),
        _pin(74777, "D125A7298CCC00CF10F2D6F817A009D7B807515BFA3953CE1C6784CF303F5551"),
        True,
        "models/weapons/sbox_smg_mp5/v_mp5.vmdl",
        "addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/invisible.vmat",
        (
            _part(AKS74U_BODY_MINUS_MODEL, "weapon_root_children"),
            _part("addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl", "magazine"),
            _part(AKS74U_BOLT_MODEL, "bolt"),
        ),
    ),
    "aks74u_world": CandidateContract(
        "candidates/aks74u/w_aks74u.fit-candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab",
        "w_aks74u",
        _pin(16544, "D8756E77BC50E713B436F1F750CC2FB1E55B8AE3164590364EADC1C914D3503D"),
        _pin(17021, "3D675DF533A4F107E0A4D591AF2DB81F86BF7CE422EBFD67E48D833AE459A99C"),
        False,
        "models/weapons/sbox_smg_mp5/w_mp5.vmdl",
        "addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/invisible.vmat",
        (_part("addons/lifepunch/lpweapons/aks74u/aks74u.vmdl", "Model", component_type="Sandbox.SkinnedModelRenderer"),),
    ),
    "ar15_view": CandidateContract(
        "candidates/m4_current_body/vm_ar15.wrapper_candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab",
        "vm_ar15",
        _pin(84627, "92BF6A0E7ECC94A1A8F0E1391253CF2B67724D3EFDE5EDD8FFC52EB96DEE5B82"),
        _pin(77843, "B573E6920D5C095FE09A5FD256BDBFE6FEFF6E03B927BD36E059AB5A02574043"),
        True,
        "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl",
        "addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/invisible.vmat",
        tuple(
            _part(f"addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_{name}.vmdl", owner, "wrapper")
            for name, owner in (
                ("body", "weapon_root_children"), ("stock", "stock"), ("trigger", "trigger"),
                ("magazine", "magazine"), ("bolt_flap", "bolt_flap"), ("bolt", "bolt"),
                ("charging_handle", "charging_handle"),
            )
        ),
    ),
    "deserteagle_view": CandidateContract(
        "candidates/visibility_roots/vm_desert_eagle.visibility-root.candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab",
        "vm_desert_eagle",
        _pin(74373, "27CDBFD846AF3A08637A05C8577428BC3C30B4420259DD5CDE5502C4E4FFB6C7"),
        _pin(73212, "77A891CD69990A868726B821DC7F52FD086F3594B48752E497809559765E4227"),
        True,
        "models/weapons/sbox_pistol_usp/v_usp.vmdl",
        "addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/invisible.vmat",
        (
            _part("addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl", "weapon_root_children"),
            _part("addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl", "slide"),
            _part(
                "addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl",
                "magazine",
                "bind_wrapper",
            ),
        ),
    ),
    "m1911_view": CandidateContract(
        "candidates/visibility_roots/vm_m1911.visibility-root.candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab",
        "vm_m1911",
        _pin(73340, "D3316F70750F9BBF355D73EF3885F8C712D59F472A415E169A754FC3841BFC46"),
        _pin(13794, "6F1FB9FD9024C439F3E5F8CF6448AF3C8FF68AE2D99F021EDC6339431F220ED3"),
        True,
        "models/weapons/sbox_pistol_usp/v_usp.vmdl",
        "addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/invisible.vmat",
        (
            _part("addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl", "weapon_root_children"),
            _part("addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl", "slide"),
            _part("addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl", "magazine"),
        ),
    ),
    "m870_view": CandidateContract(
        "candidates/visibility_roots/vm_m870.visibility-root.candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab",
        "vm_m870",
        _pin(70839, "F9860C726E466007C8624DE2F2BAC68C05034DFEFD826D90FE4BE91F2546703D"),
        _pin(70704, "7BFA941833E2F023194D30758524C4158CEC61980CEAD4AC895F0FED188E2885"),
        True,
        "models/weapons/sbox_shotgun_spaghellim4/v_spaghellim4.vmdl",
        "addons/lifepunch/lpweapons/m870/equipment/vm_m870/invisible.vmat",
        (
            _part("addons/lifepunch/lpweapons/m870/models/m870_body.vmdl", "weapon_root_children"),
            _part("addons/lifepunch/lpweapons/m870/models/m870_pump.vmdl", "weapon_root_children"),
        ),
    ),
    "sr25_view": CandidateContract(
        "candidates/m4_current_body/vm_sr25.wrapper_candidate.prefab",
        "game/Assets/addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab",
        "vm_sr25",
        _pin(94773, "9378D22F99F81BEDC83043B99C6FCCAA9C7C6E20D4EFFED0BD1137BCDCEE694D"),
        _pin(85430, "FFC3EB52516107EE9292821F1ECDE575E46EE85B04F1FC3B695EC21B40F6C8D4"),
        True,
        "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl",
        "addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/invisible.vmat",
        tuple(
            _part(f"addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_{name}.vmdl", owner, "wrapper")
            for name, owner in (
                ("body", "weapon_root_children"), ("stock", "stock"), ("trigger", "trigger"),
                ("magazine", "magazine"), ("mode_selector", "mode_selector"),
                ("bolt_flap", "bolt_flap"), ("bolt", "bolt"),
                ("charging_handle", "charging_handle"), ("scope", "sr25_scope"),
                ("scope_mount", "sr25_scope_mount"), ("suppressor", "sr25_suppressor"),
            )
        ),
    ),
}


SUPPORT_ASSETS: Mapping[str, SupportAssetContract] = {
    "aks74u_body_minus_bolt_fbx": SupportAssetContract(
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "source/bolt_split/aks74u_body_minus_bolt.fbx"
        ),
        "evidence/intermediates/aks74u_bolt_split/meshes/aks74u_body_minus_bolt.fbx",
        (
            "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/"
            "aks74u_body_minus_bolt.fbx"
        ),
        _pin(1_684_556, "427C43D4410B0330BDAAE9C3DC606B65898622B64530BDA74A80C10426234C0C"),
    ),
    "aks74u_bolt_fbx": SupportAssetContract(
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "source/bolt_split/aks74u_bolt.fbx"
        ),
        "evidence/intermediates/aks74u_bolt_split/meshes/aks74u_bolt.fbx",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_bolt.fbx",
        _pin(81_148, "06DE3C4729D039691804467EE698E1532175F429083DA8ECF443FB60EA48C95F"),
    ),
    "aks74u_body_minus_bolt_vmdl": SupportAssetContract(
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "aks74u_body_minus_bolt.vmdl"
        ),
        "evidence/intermediates/aks74u_bolt_split/modeldocs/aks74u_body_minus_bolt.vmdl",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl",
        _pin(1_259, "EF3A1285977F4D335D4D54B06510224E499D52A82D54B68F697B2A2F529730ED"),
    ),
    "aks74u_bolt_vmdl": SupportAssetContract(
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "aks74u_bolt.vmdl"
        ),
        "evidence/intermediates/aks74u_bolt_split/modeldocs/aks74u_bolt.vmdl",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl",
        _pin(1_237, "FDC8F6EE449FD4AC5F1BE79AC202F6A6ED0FE412CAAD416955FC089F42C8D93C"),
    ),
}


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _verify_pin(data: bytes, pin: FilePin, label: str) -> None:
    if len(data) != pin.bytes:
        raise ContractError(f"{label} byte mismatch: {len(data)} != {pin.bytes}")
    digest = _sha256(data)
    if digest != pin.sha256:
        raise ContractError(f"{label} SHA-256 mismatch: {digest} != {pin.sha256}")


def assert_temp_bundle_root(path: Path) -> Path:
    resolved = path.resolve()
    if resolved == SYSTEM_TEMP_ROOT or SYSTEM_TEMP_ROOT not in resolved.parents:
        raise ContractError(f"Bundle root must be a strict child of system TEMP: {resolved}")
    if not resolved.is_dir():
        raise ContractError(f"Bundle root is not a directory: {resolved}")
    return resolved


def _strict_child(root: Path, path: Path, label: str) -> Path:
    try:
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise ContractError(f"{label} cannot be resolved: {exc}") from exc
    if resolved == root or root not in resolved.parents:
        raise ContractError(f"{label} escaped bundle root: {resolved}")
    return resolved


def _walk_tree(root: Mapping[str, Any]) -> tuple[dict[str, dict[str, Any]], dict[str, dict[str, Any]]]:
    nodes: dict[str, dict[str, Any]] = {}
    components: dict[str, dict[str, Any]] = {}

    def visit(node: Mapping[str, Any], path: str, parent: Mapping[str, Any] | None) -> None:
        name = node.get("Name")
        guid = node.get("__guid")
        if not isinstance(name, str) or not isinstance(guid, str):
            raise ContractError(f"Malformed GameObject at {path or '<root>'}")
        current = f"{path}/{name}" if path else name
        if guid in nodes or guid in components:
            raise ContractError(f"Duplicate/colliding GUID {guid} at {current}")
        nodes[guid] = {"data": node, "path": current, "parent": parent}
        for component in node.get("Components") or []:
            component_guid = component.get("__guid")
            if not isinstance(component_guid, str):
                raise ContractError(f"Malformed component at {current}")
            if component_guid in nodes or component_guid in components:
                raise ContractError(f"Duplicate/colliding GUID {component_guid} at {current}")
            components[component_guid] = {
                "data": component,
                "owner": node,
                "owner_path": current,
                "parent": parent,
            }
        for child in node.get("Children") or []:
            visit(child, current, node)

    visit(root, "", None)
    return nodes, components


def _scan_references(
    value: Any,
    location: str = "$",
) -> Iterable[tuple[str, str, str, str | None]]:
    if isinstance(value, dict):
        if value.get("_type") == "gameobject" and isinstance(value.get("go"), str):
            yield "gameobject", value["go"], location, None
        if value.get("_type") == "component" and isinstance(value.get("component_id"), str):
            owner_guid = value.get("go")
            yield "component", value["component_id"], location, owner_guid if isinstance(owner_guid, str) else None
            if isinstance(owner_guid, str):
                yield "gameobject", owner_guid, location + "/go", None
        for key, child in value.items():
            yield from _scan_references(child, location + "/" + str(key))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from _scan_references(child, f"{location}/{index}")


def _is_descendant(path: str, ancestor: str) -> bool:
    return path == ancestor or path.startswith(ancestor + "/")


def _validate_candidate_structure(key: str, prefab: Mapping[str, Any]) -> dict[str, Any]:
    contract = CANDIDATES[key]
    root = prefab.get("RootObject")
    if not isinstance(root, dict) or root.get("Name") != contract.root_name:
        raise ContractError(f"{key} root name drifted from {contract.root_name}")
    nodes, components = _walk_tree(root)

    for reference_type, guid, location, expected_owner_guid in _scan_references(prefab):
        target = nodes if reference_type == "gameobject" else components
        if guid not in target:
            raise ContractError(f"{key} dangling {reference_type} reference {guid} at {location}")
        if reference_type == "component":
            if expected_owner_guid is None:
                raise ContractError(f"{key} component reference {guid} has no owner GameObject at {location}")
            actual_owner_guid = components[guid]["owner"].get("__guid")
            if actual_owner_guid != expected_owner_guid:
                raise ContractError(
                    f"{key} component owner mismatch for {guid} at {location}: "
                    f"{actual_owner_guid} != {expected_owner_guid}"
                )

    root_view_models = [
        component for component in root.get("Components") or []
        if component.get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    all_view_models = [
        record for record in components.values()
        if record["data"].get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    additional_root_path: str | None = None
    if contract.view_model:
        if len(root_view_models) != 1 or len(all_view_models) != 1:
            raise ContractError(f"{key} must contain exactly one root ViewModel")
        reference = root_view_models[0].get("AdditionalRendererRoot")
        additional_guid = reference.get("go") if isinstance(reference, dict) else None
        if additional_guid not in nodes:
            raise ContractError(f"{key} AdditionalRendererRoot does not resolve")
        additional_record = nodes[additional_guid]
        if additional_record["data"].get("Name") != "weapon_root":
            raise ContractError(f"{key} AdditionalRendererRoot is not weapon_root")
        additional_root_path = additional_record["path"]
    elif all_view_models:
        raise ContractError(f"{key} world candidate unexpectedly contains a ViewModel")

    custom_renderers: dict[str, dict[str, Any]] = {}
    donor_renderers: list[dict[str, Any]] = []
    for record in components.values():
        component = record["data"]
        model = component.get("Model")
        if not isinstance(model, str):
            continue
        if model.startswith("addons/lifepunch/"):
            if model in custom_renderers:
                raise ContractError(f"{key} duplicates custom renderer model {model}")
            custom_renderers[model] = record
        if model == contract.donor_model and component.get("MaterialOverride") == contract.donor_material:
            donor_renderers.append(record)

    expected_models = {part.model for part in contract.parts}
    if set(custom_renderers) != expected_models:
        raise ContractError(
            f"{key} custom renderer set drifted: {sorted(custom_renderers)} != {sorted(expected_models)}"
        )
    if len(donor_renderers) != 1:
        raise ContractError(f"{key} must contain exactly one hidden donor renderer")

    owner_rows: list[dict[str, str]] = []
    for part in contract.parts:
        record = custom_renderers[part.model]
        component = record["data"]
        owner = record["owner"]
        parent = record["parent"]
        if component.get("__type") != part.component_type:
            raise ContractError(f"{key} {part.model} renderer type drifted")
        if contract.view_model and not _is_descendant(record["owner_path"], additional_root_path or ""):
            raise ContractError(f"{key} {part.model} is outside AdditionalRendererRoot")
        motion_parent = parent
        if part.shape == "bind_wrapper":
            if (
                not isinstance(parent, dict)
                or parent.get("Name") != DESERTEAGLE_WRAPPER_NAME
                or parent.get("__guid") != DESERTEAGLE_WRAPPER_GUID
                or parent.get("Components") != []
                or [child.get("__guid") for child in parent.get("Children", [])]
                != [DESERTEAGLE_MAGAZINE_RENDERER_GUID]
            ):
                raise ContractError(f"{key} {part.model} lost its bind wrapper")
            wrapper_record = nodes.get(DESERTEAGLE_WRAPPER_GUID)
            motion_parent = (
                wrapper_record.get("parent")
                if isinstance(wrapper_record, dict)
                else None
            )
        if (
            not isinstance(motion_parent, dict)
            or motion_parent.get("Name") != part.motion_owner
        ):
            actual = (
                None
                if not isinstance(motion_parent, dict)
                else motion_parent.get("Name")
            )
            raise ContractError(
                f"{key} {part.model} motion owner {actual} != {part.motion_owner}"
            )
        if part.shape == "wrapper" and not str(owner.get("Name", "")).endswith("_renderer_wrapper"):
            raise ContractError(f"{key} {part.model} lost its renderer wrapper")
        if part.shape == "direct" and str(owner.get("Name", "")).endswith("_renderer_wrapper"):
            raise ContractError(f"{key} {part.model} unexpectedly gained a renderer wrapper")
        owner_rows.append({"model": part.model, "motion_owner": part.motion_owner, "shape": part.shape})

    return {
        "game_objects": len(nodes),
        "components": len(components),
        "references_closed": True,
        "root_view_model": contract.view_model,
        "additional_renderer_root": additional_root_path,
        "custom_renderers": len(custom_renderers),
        "motion_owners": owner_rows,
    }


def _validate_deserteagle_minimal_delta(
    candidate: Mapping[str, Any],
    destination_preimage: Mapping[str, Any],
) -> dict[str, Any]:
    """Prove the candidate is the active prefab plus exactly two narrow changes."""

    source_nodes, source_components = _walk_tree(destination_preimage["RootObject"])
    candidate_nodes, candidate_components = _walk_tree(candidate["RootObject"])
    source_guids = set(source_nodes) | set(source_components)
    candidate_guids = set(candidate_nodes) | set(candidate_components)
    if candidate_guids - source_guids != {DESERTEAGLE_WRAPPER_GUID}:
        raise ContractError("deserteagle_view added more than its one wrapper GUID")
    if source_guids - candidate_guids:
        raise ContractError("deserteagle_view removed an existing GUID")

    source_owners = {
        guid: record["owner"].get("__guid")
        for guid, record in source_components.items()
    }
    candidate_owners = {
        guid: candidate_components[guid]["owner"].get("__guid")
        for guid in source_components
    }
    if source_owners != candidate_owners:
        raise ContractError("deserteagle_view changed an existing component owner")

    stripped = copy.deepcopy(candidate)
    stripped_nodes, stripped_components = _walk_tree(stripped["RootObject"])
    view_model = stripped_components.get(DESERTEAGLE_VIEW_MODEL_GUID, {}).get(
        "data"
    )
    expected_visibility = {
        "_type": "gameobject",
        "go": DESERTEAGLE_VISIBILITY_ROOT_GUID,
    }
    if (
        not isinstance(view_model, dict)
        or view_model.pop("AdditionalRendererRoot", None) != expected_visibility
    ):
        raise ContractError("deserteagle_view visibility-root delta drifted")

    wrapper_record = stripped_nodes.get(DESERTEAGLE_WRAPPER_GUID)
    if not isinstance(wrapper_record, dict):
        raise ContractError("deserteagle_view wrapper disappeared")
    wrapper = wrapper_record["data"]
    wrapper_parent = wrapper_record.get("parent")
    if (
        not isinstance(wrapper_parent, dict)
        or wrapper_parent.get("__guid") != DESERTEAGLE_MAGAZINE_PARENT_GUID
        or {key: value for key, value in wrapper.items() if key != "Children"}
        != DESERTEAGLE_WRAPPER_FIELDS
        or [child.get("__guid") for child in wrapper.get("Children", [])]
        != [DESERTEAGLE_MAGAZINE_RENDERER_GUID]
    ):
        raise ContractError("deserteagle_view wrapper structure drifted")
    parent_children = wrapper_parent.get("Children")
    if not isinstance(parent_children, list) or len(parent_children) != 1:
        raise ContractError("deserteagle_view wrapper parent structure drifted")
    wrapper_parent["Children"] = wrapper["Children"]

    if stripped != destination_preimage:
        raise ContractError(
            "deserteagle_view changed existing locals, GUID graph, references, or data"
        )
    return {
        "added_guids": [DESERTEAGLE_WRAPPER_GUID],
        "removed_guids": [],
        "existing_guid_graph_preserved": True,
        "existing_component_owners_preserved": True,
        "existing_locals_preserved": True,
        "only_allowed_changes": [
            "one magazine bind wrapper",
            "AdditionalRendererRoot",
        ],
    }


def _validate_aks74u_bolt_delta(
    candidate: Mapping[str, Any],
    visibility_base: Mapping[str, Any],
) -> dict[str, Any]:
    base_nodes, base_components = _walk_tree(visibility_base["RootObject"])
    final_nodes, final_components = _walk_tree(candidate["RootObject"])
    base_guids = set(base_nodes) | set(base_components)
    final_guids = set(final_nodes) | set(final_components)
    if final_guids - base_guids != {
        AKS74U_BOLT_NODE_GUID,
        AKS74U_BOLT_RENDERER_GUID,
    }:
        raise ContractError("aks74u_view bolt split added an unexpected GUID")
    if base_guids - final_guids:
        raise ContractError("aks74u_view bolt split removed an existing GUID")

    base_by_path = {record["path"]: record["data"] for record in base_nodes.values()}
    final_by_path = {record["path"]: record["data"] for record in final_nodes.values()}
    for path in (
        AKS74U_MAGAZINE_PATH,
        AKS74U_SELECTOR_PATH,
        AKS74U_TRIGGER_PATH,
        AKS74U_STOCK_PATH,
    ):
        if final_by_path.get(path) != base_by_path.get(path):
            raise ContractError(f"aks74u_view changed out-of-scope node {path}")

    def view_model(prefab: Mapping[str, Any]) -> Mapping[str, Any]:
        matches = [
            component
            for component in prefab["RootObject"].get("Components") or []
            if component.get("__type") == "Dxura.RP.Game.ViewModel"
        ]
        if len(matches) != 1:
            raise ContractError("aks74u_view root ViewModel count drifted")
        return matches[0]

    if view_model(candidate).get("AdditionalRendererRoot") != view_model(
        visibility_base
    ).get("AdditionalRendererRoot"):
        raise ContractError("aks74u_view lost the visibility-root transform")

    bolt_parent = final_by_path.get(AKS74U_BOLT_PARENT_PATH)
    if (
        not isinstance(bolt_parent, dict)
        or [child.get("__guid") for child in bolt_parent.get("Children") or []]
        != [AKS74U_BOLT_NODE_GUID]
    ):
        raise ContractError("aks74u_view must contain one bolt renderer under MP5 bolt")

    reverted = copy.deepcopy(candidate)
    reverted_nodes, reverted_components = _walk_tree(reverted["RootObject"])
    reverted_by_path = {
        record["path"]: record["data"] for record in reverted_nodes.values()
    }
    reverted_by_path[AKS74U_BOLT_PARENT_PATH]["Children"] = []
    body_renderers = [
        component
        for component in reverted_by_path[AKS74U_BODY_PATH].get("Components") or []
        if component.get("Model") == AKS74U_BODY_MINUS_MODEL
    ]
    if len(body_renderers) != 1:
        raise ContractError("aks74u_view body-minus-bolt renderer drifted")
    body_renderers[0]["Model"] = AKS74U_BODY_MODEL
    if reverted != visibility_base:
        raise ContractError(
            "aks74u_view changed beyond body swap and one bolt renderer"
        )
    return {
        "added_guids": [AKS74U_BOLT_NODE_GUID, AKS74U_BOLT_RENDERER_GUID],
        "removed_guids": [],
        "additional_renderer_root_preserved": True,
        "magazine_unchanged": True,
        "selector_trigger_stock_unchanged": True,
        "one_bolt_renderer_under_mp5_bolt": True,
        "only_allowed_changes": ["body-minus-bolt model swap", "one bolt child"],
    }


def _validate_support_assets(
    bundle_root: Path, report: Mapping[str, Any]
) -> dict[str, dict[str, Any]]:
    reported = report.get("support_assets")
    destinations = report.get("create_only_destinations")
    if not isinstance(reported, dict) or set(reported) != set(SUPPORT_ASSETS):
        raise ContractError("Promotion report support asset set drifted")
    if not isinstance(destinations, dict) or set(destinations) != set(SUPPORT_ASSETS):
        raise ContractError("Promotion report create-only destination set drifted")

    rows: dict[str, dict[str, Any]] = {}
    expected_files: set[Path] = set()
    for name, contract in SUPPORT_ASSETS.items():
        metadata = reported[name]
        if (
            metadata.get("relative_path") != contract.relative_path
            or metadata.get("source_relative_path") != contract.source_relative_path
            or metadata.get("product_destination") != contract.destination
            or metadata.get("role") != "CREATE_ONLY_PROMOTION_SUPPORT_ASSET"
            or metadata.get("create_only") is not True
            or metadata.get("product_destination_exists") is not False
            or metadata.get("product_write_performed") is not False
        ):
            raise ContractError(f"{name} create-only support metadata drifted")
        path = _strict_child(
            bundle_root, bundle_root / contract.relative_path, f"{name} support asset"
        )
        source = _strict_child(
            bundle_root,
            bundle_root / contract.source_relative_path,
            f"{name} support source",
        )
        reported_path = metadata.get("path")
        if not isinstance(reported_path, str) or _strict_child(
            bundle_root, Path(reported_path), f"{name} reported support asset"
        ) != path:
            raise ContractError(f"{name} reported support path drifted")
        data = path.read_bytes()
        source_data = source.read_bytes()
        _verify_pin(data, contract.pin, name + " support asset")
        if source_data != data:
            raise ContractError(f"{name} support source closure drifted")
        if (
            metadata.get("bytes") != contract.pin.bytes
            or str(metadata.get("sha256", "")).upper() != contract.pin.sha256
        ):
            raise ContractError(f"{name} reported support pin drifted")
        product_destination = REPO_ROOT / contract.destination
        destination = destinations[name]
        if (
            destination
            != {
                "path": str(product_destination),
                "exists": False,
                "create_only": True,
            }
            or product_destination.exists()
        ):
            raise ContractError(f"{name} create-only destination is no longer absent")
        expected_files.add(path)
        rows[name] = {
            "bytes": len(data),
            "sha256": _sha256(data),
            "source_closed": True,
            "destination_absent": True,
        }

    actual_files = {
        path.resolve()
        for path in (bundle_root / "support_assets").rglob("*")
        if path.is_file()
    }
    if actual_files != expected_files:
        raise ContractError("Bundle support asset tree drifted")
    return rows


def _validate_aks74u_bolt_evidence(
    bundle_root: Path, report: Mapping[str, Any]
) -> dict[str, Any]:
    evidence = report.get("support_asset_evidence")
    if not isinstance(evidence, dict) or set(evidence) != {
        "bundle_manifest",
        "topology_report",
    }:
        raise ContractError("AKS-74U bolt evidence set drifted")
    relative_paths = {
        "bundle_manifest": (
            "evidence/intermediates/aks74u_bolt_split/bundle_manifest.json"
        ),
        "topology_report": (
            "evidence/intermediates/aks74u_bolt_split/evidence/"
            "bolt_split_topology.json"
        ),
    }
    evidence_payloads: dict[str, bytes] = {}
    for name, relative_path in relative_paths.items():
        metadata = evidence[name]
        if metadata.get("relative_path") != relative_path:
            raise ContractError(f"AKS-74U {name} relative path drifted")
        path = _strict_child(bundle_root, bundle_root / relative_path, name)
        if _strict_child(bundle_root, Path(str(metadata.get("path"))), name) != path:
            raise ContractError(f"AKS-74U {name} reported path drifted")
        data = path.read_bytes()
        if (
            metadata.get("bytes") != len(data)
            or str(metadata.get("sha256", "")).upper() != _sha256(data)
        ):
            raise ContractError(f"AKS-74U {name} reported pin drifted")
        evidence_payloads[name] = data
    _verify_pin(
        evidence_payloads["topology_report"],
        _pin(
            8_678,
            "5AE32EC8589EF38669C863B668B3A8722743F8706820554033370732F1283EDC",
        ),
        "AKS-74U topology report",
    )

    manifest = json.loads(evidence_payloads["bundle_manifest"])
    topology = json.loads(evidence_payloads["topology_report"])
    generated_inputs = report.get("generated_aks74u_bolt_inputs")
    if not isinstance(generated_inputs, dict) or set(generated_inputs) != {
        "authoritative_fbx_probe",
        "source_topology_evidence",
        "compiled_mp5_data_probe",
        "input_manifest",
    }:
        raise ContractError("AKS-74U generated bolt input set drifted")
    generated_input_payloads: dict[str, bytes] = {}
    for name, metadata in generated_inputs.items():
        path = _strict_child(
            bundle_root, Path(str(metadata.get("path"))), f"AKS-74U {name}"
        )
        data = path.read_bytes()
        if (
            metadata.get("bytes") != len(data)
            or str(metadata.get("sha256", "")).upper() != _sha256(data)
        ):
            raise ContractError(f"AKS-74U generated input pin drifted: {name}")
        generated_input_payloads[name] = data
    input_manifest_metadata = generated_inputs["input_manifest"]
    nested_input_manifest = manifest.get("input_manifest")
    if (
        not isinstance(nested_input_manifest, dict)
        or Path(str(nested_input_manifest.get("path"))).resolve()
        != Path(str(input_manifest_metadata.get("path"))).resolve()
        or nested_input_manifest.get("bytes") != input_manifest_metadata.get("bytes")
        or str(nested_input_manifest.get("sha256", "")).upper()
        != str(input_manifest_metadata.get("sha256", "")).upper()
    ):
        raise ContractError("AKS-74U generated input manifest closure drifted")
    input_manifest = json.loads(generated_input_payloads["input_manifest"])
    if (
        input_manifest.get("purpose") != "dxrp_aks74u_fp_bolt_split_candidate"
        or set(input_manifest.get("inputs", {}))
        != set(manifest.get("input_pins", {}))
    ):
        raise ContractError("AKS-74U deterministic input manifest labels drifted")
    expected_scope = {
        "first_person_only": True,
        "split_only_tag_bolt": True,
        "selector_trigger_stock_split": False,
        "third_person_created_or_modified": False,
        "magazine_mapping_changed": False,
    }
    if (
        manifest.get("purpose") != "dxrp_aks74u_fp_bolt_split_candidate"
        or manifest.get("mode") != "STRICT_SYSTEM_TEMP_ONLY_NO_PRODUCT_WRITE_SWITCH"
        or manifest.get("scope") != expected_scope
    ):
        raise ContractError("AKS-74U bolt manifest scope drifted")
    expected_promotion_map = {
        contract.source_relative_path.split(
            "evidence/intermediates/aks74u_bolt_split/", 1
        )[1]: contract.destination
        for contract in SUPPORT_ASSETS.values()
    }
    expected_promotion_map[
        "prefabs/vm_aks74u.bolt-split-candidate.prefab"
    ] = CANDIDATES["aks74u_view"].destination
    if manifest.get("promotion_map") != expected_promotion_map:
        raise ContractError("AKS-74U bolt promotion map drifted")

    intermediate = report.get("intermediates", {}).get("aks74u_visibility_base")
    base_pin = manifest.get("input_pins", {}).get("base_fit_candidate_prefab")
    if (
        not isinstance(intermediate, dict)
        or not isinstance(base_pin, dict)
        or Path(str(base_pin.get("path"))).resolve()
        != Path(str(intermediate.get("path"))).resolve()
        or base_pin.get("bytes") != intermediate.get("bytes")
        or str(base_pin.get("sha256", "")).upper()
        != str(intermediate.get("sha256", "")).upper()
    ):
        raise ContractError("AKS-74U bolt visibility-base input closure drifted")

    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, dict):
        raise ContractError("AKS-74U bolt artifact pins are missing")
    for name, contract in SUPPORT_ASSETS.items():
        artifact_name = contract.source_relative_path.split(
            "evidence/intermediates/aks74u_bolt_split/", 1
        )[1]
        pin = artifacts.get(artifact_name)
        if (
            not isinstance(pin, dict)
            or pin.get("relative_path") != artifact_name
            or pin.get("bytes") != contract.pin.bytes
            or str(pin.get("sha256", "")).upper() != contract.pin.sha256
        ):
            raise ContractError(f"{name} bolt artifact closure drifted")
    candidate_artifact = artifacts.get(
        "prefabs/vm_aks74u.bolt-split-candidate.prefab"
    )
    candidate_pin = CANDIDATES["aks74u_view"].candidate_pin
    if (
        not isinstance(candidate_artifact, dict)
        or candidate_artifact.get("bytes") != candidate_pin.bytes
        or str(candidate_artifact.get("sha256", "")).upper()
        != candidate_pin.sha256
    ):
        raise ContractError("AKS-74U final candidate artifact closure drifted")

    partition = topology.get("partition", {})
    expected_partition = {
        "body_minus_bolt_vertices": 33_432,
        "bolt_vertices": 1_285,
        "vertex_intersection": 0,
        "vertex_union": 34_717,
        "body_minus_bolt_faces": 21_001,
        "bolt_faces": 830,
        "face_intersection": 0,
        "face_union": 21_831,
        "welded_position_edge_islands_q1e-6m": 3,
        "welded_position_edge_island_face_counts": [350, 291, 189],
    }
    if any(partition.get(key) != value for key, value in expected_partition.items()):
        raise ContractError("AKS-74U topology partition closure drifted")
    post = topology.get("post_export_reimport", {})
    if (
        post.get("each_part_matches_pre_export") is not True
        or post.get("vertex_multiset_closure") is not True
        or post.get("face_position_uv_material_multiset_closure") is not True
        or post.get("duplicate_bolt_in_body") is not False
    ):
        raise ContractError("AKS-74U topology round-trip closure drifted")
    output_to_support = {
        "body_minus_bolt_fbx": SUPPORT_ASSETS["aks74u_body_minus_bolt_fbx"],
        "bolt_fbx": SUPPORT_ASSETS["aks74u_bolt_fbx"],
    }
    for output_name, contract in output_to_support.items():
        pin = topology.get("outputs", {}).get(output_name)
        artifact_name = contract.source_relative_path.split(
            "evidence/intermediates/aks74u_bolt_split/", 1
        )[1]
        if (
            not isinstance(pin, dict)
            or pin.get("relative_path") != artifact_name
            or pin.get("bytes") != contract.pin.bytes
            or str(pin.get("sha256", "")).upper() != contract.pin.sha256
        ):
            raise ContractError(f"AKS-74U topology output {output_name} drifted")

    body_modeldoc = (
        bundle_root / SUPPORT_ASSETS["aks74u_body_minus_bolt_vmdl"].relative_path
    ).read_text(encoding="utf-8")
    bolt_modeldoc = (
        bundle_root / SUPPORT_ASSETS["aks74u_bolt_vmdl"].relative_path
    ).read_text(encoding="utf-8")
    if (
        'filename = "addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_body_minus_bolt.fbx"'
        not in body_modeldoc
        or '"SK_Rif_SLR_AK47_body_minus_bolt"' not in body_modeldoc
        or 'filename = "addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_bolt.fbx"'
        not in bolt_modeldoc
        or '"SK_Rif_SLR_AK47_bolt"' not in bolt_modeldoc
    ):
        raise ContractError("AKS-74U body-minus/bolt ModelDoc source closure drifted")
    return {
        "scope": expected_scope,
        "topology_report_pin_match": True,
        "source_partition_closed": True,
        "fbx_round_trip_closed": True,
        "body_minus_and_bolt_modeldocs_closed": True,
    }


def validate_bundle(bundle_root: Path) -> dict[str, Any]:
    bundle_root = assert_temp_bundle_root(bundle_root)
    report_path = _strict_child(bundle_root, bundle_root / REPORT_FILE, "Promotion report")
    try:
        report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Cannot read promotion report: {exc}") from exc
    if report.get("mode") != EXPECTED_MODE or report.get("result") != "PASS":
        raise ContractError("Promotion report mode/result is not the pinned review contract")
    if report.get("version") != 1:
        raise ContractError("Promotion report version drifted")
    expected_acceptance = {
        "measured": False,
        "portal": False,
        "runtime": False,
        "visual": False,
    }
    if report.get("acceptance") != expected_acceptance:
        raise ContractError("Promotion report acceptance contract drifted")
    expected_world_ik_exclusion = {
        "accepted_transforms_supplied": False,
        "candidate_count": 0,
        "included": False,
        "reason": (
            "Generic WorldWeaponLeftHandIk candidates are not included absent accepted "
            "per-weapon transforms."
        ),
        "transforms_generated_or_invented": False,
    }
    if report.get("world_weapon_left_hand_ik_candidates") != expected_world_ik_exclusion:
        raise ContractError("Promotion report world-weapon IK exclusion contract drifted")
    reported_output = report.get("output_directory")
    if not isinstance(reported_output, str) or Path(reported_output).resolve() != bundle_root:
        raise ContractError("Promotion report output_directory does not match bundle root")
    expected_writes = {
        "docs": False,
        "game_tree": False,
        "git": False,
        "portal": False,
        "temp_only": True,
    }
    if report.get("writes") != expected_writes:
        raise ContractError("Promotion report writes contract drifted")
    builders = report.get("builders")
    if not isinstance(builders, dict):
        raise ContractError("Promotion report builder custody is missing")
    deagle_builder = builders.get("deserteagle")
    expected_deagle_builder_pin = FilePin(
        DESERTEAGLE_MINIMAL_BUILDER_BYTES,
        DESERTEAGLE_MINIMAL_BUILDER_SHA256,
    )
    _verify_pin(
        DESERTEAGLE_MINIMAL_BUILDER.read_bytes(),
        expected_deagle_builder_pin,
        "Desert Eagle minimal builder",
    )
    if (
        not isinstance(deagle_builder, dict)
        or Path(str(deagle_builder.get("path", ""))).resolve()
        != DESERTEAGLE_MINIMAL_BUILDER.resolve()
        or deagle_builder.get("bytes") != DESERTEAGLE_MINIMAL_BUILDER_BYTES
        or str(deagle_builder.get("sha256", "")).upper()
        != DESERTEAGLE_MINIMAL_BUILDER_SHA256
    ):
        raise ContractError("Promotion report does not pin the minimal Desert Eagle builder")
    for key, path, expected in (
        (
            "aks74u_bolt_split",
            AKS74U_BOLT_BUILDER,
            _pin(AKS74U_BOLT_BUILDER_BYTES, AKS74U_BOLT_BUILDER_SHA256),
        ),
        (
            "aks74u_bolt_worker",
            AKS74U_BOLT_WORKER,
            _pin(AKS74U_BOLT_WORKER_BYTES, AKS74U_BOLT_WORKER_SHA256),
        ),
    ):
        _verify_pin(path.read_bytes(), expected, key)
        metadata = builders.get(key)
        if (
            not isinstance(metadata, dict)
            or Path(str(metadata.get("path", ""))).resolve() != path.resolve()
            or metadata.get("bytes") != expected.bytes
            or str(metadata.get("sha256", "")).upper() != expected.sha256
        ):
            raise ContractError(f"Promotion report does not pin {key}")
    nested_evidence = report.get("nested_builder_evidence")
    visibility_evidence = (
        nested_evidence.get("visibility_roots")
        if isinstance(nested_evidence, dict)
        else None
    )
    expected_visibility_scope = {
        "included_families": ["ak47", "aks74u", "m1911", "m870"],
        "excluded_families": ["deserteagle"],
        "reason": (
            "Desert Eagle uses the pinned minimal correction builder directly; "
            "the older broad/common-origin visibility input is not generated or consumed."
        ),
    }
    if (
        not isinstance(visibility_evidence, dict)
        or visibility_evidence.get("promotion_bundle_scope")
        != expected_visibility_scope
    ):
        raise ContractError("Desert Eagle visibility-builder exclusion drifted")
    report_candidates = report.get("candidates")
    report_preimages = report.get("destination_preimages")
    if not isinstance(report_candidates, dict) or set(report_candidates) != set(CANDIDATES):
        raise ContractError("Promotion report candidate set drifted")
    if not isinstance(report_preimages, dict) or set(report_preimages) != set(CANDIDATES):
        raise ContractError("Promotion report destination-preimage set drifted")

    support_rows = _validate_support_assets(bundle_root, report)
    bolt_evidence = _validate_aks74u_bolt_evidence(bundle_root, report)
    visibility_base_metadata = report.get("intermediates", {}).get(
        "aks74u_visibility_base"
    )
    if not isinstance(visibility_base_metadata, dict):
        raise ContractError("AKS-74U visibility-base evidence is missing")
    visibility_base_path = _strict_child(
        bundle_root,
        Path(str(visibility_base_metadata.get("path"))),
        "AKS-74U visibility-base evidence",
    )
    visibility_base_data = visibility_base_path.read_bytes()
    if (
        visibility_base_metadata.get("bytes") != len(visibility_base_data)
        or str(visibility_base_metadata.get("sha256", "")).upper()
        != _sha256(visibility_base_data)
    ):
        raise ContractError("AKS-74U visibility-base evidence pin drifted")
    try:
        aks74u_visibility_base = json.loads(visibility_base_data.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ContractError("AKS-74U visibility-base evidence is invalid JSON") from exc
    support_models = {
        contract.destination.split("game/Assets/", 1)[1]
        for contract in SUPPORT_ASSETS.values()
        if contract.destination.endswith(".vmdl")
    }

    rows: dict[str, Any] = {}
    for key, contract in CANDIDATES.items():
        metadata = report_candidates[key]
        if metadata.get("relative_path") != contract.relative_path:
            raise ContractError(f"{key} relative path drifted")
        if metadata.get("destination_preimage") != key:
            raise ContractError(f"{key} destination-preimage identity drifted")
        if metadata.get("promotable") is not True or metadata.get("role") != "PROMOTABLE_REVIEW_CANDIDATE":
            raise ContractError(f"{key} is no longer an explicit review candidate")
        if any(metadata.get(field) is not False for field in (
            "measured_and_accepted", "runtime_accepted", "visual_accepted", "portal_accepted"
        )):
            raise ContractError(f"{key} acceptance flags must remain false")
        candidate_path = _strict_child(
            bundle_root,
            bundle_root / contract.relative_path,
            f"{key} candidate",
        )
        reported_candidate_path = metadata.get("path")
        if not isinstance(reported_candidate_path, str):
            raise ContractError(f"{key} report candidate path drifted")
        try:
            reported_candidate_resolved = _strict_child(
                bundle_root,
                Path(reported_candidate_path),
                f"{key} reported candidate",
            )
        except ContractError as exc:
            raise ContractError(f"{key} report candidate path drifted: {exc}") from exc
        if reported_candidate_resolved != candidate_path:
            raise ContractError(f"{key} report candidate path drifted")
        data = candidate_path.read_bytes()
        _verify_pin(data, contract.candidate_pin, key + " candidate")
        if metadata.get("bytes") != contract.candidate_pin.bytes or str(metadata.get("sha256", "")).upper() != contract.candidate_pin.sha256:
            raise ContractError(f"{key} report candidate pin drifted")

        preimage = report_preimages[key]
        destination_path = REPO_ROOT / contract.destination
        destination_data = destination_path.read_bytes()
        _verify_pin(destination_data, contract.destination_pin, key + " destination preimage")
        if Path(preimage.get("path", "")).resolve() != destination_path.resolve():
            raise ContractError(f"{key} destination path drifted")
        if preimage.get("bytes") != contract.destination_pin.bytes or str(preimage.get("sha256", "")).upper() != contract.destination_pin.sha256:
            raise ContractError(f"{key} report destination pin drifted")

        try:
            prefab = json.loads(data.decode("utf-8-sig"))
        except (UnicodeError, json.JSONDecodeError) as exc:
            raise ContractError(f"{key} candidate is invalid JSON: {exc}") from exc
        row = _validate_candidate_structure(key, prefab)
        if key == "aks74u_view":
            row["bolt_delta"] = _validate_aks74u_bolt_delta(
                prefab, aks74u_visibility_base
            )
        if key == "deserteagle_view":
            try:
                destination_prefab = json.loads(destination_data.decode("utf-8-sig"))
            except (UnicodeError, json.JSONDecodeError) as exc:
                raise ContractError(
                    "deserteagle_view destination preimage is invalid JSON"
                ) from exc
            row["minimal_delta"] = _validate_deserteagle_minimal_delta(
                prefab,
                destination_prefab,
            )
        for part in contract.parts:
            if part.model in support_models:
                continue
            asset_path = ASSETS_ROOT.joinpath(*part.model.split("/"))
            if not asset_path.is_file():
                raise ContractError(f"{key} missing local asset {part.model}")
        row.update({"bytes": len(data), "sha256": _sha256(data), "destination_pin_match": True})
        rows[key] = row

    return {
        "result": "PASS",
        "mode": "TEMP_ONLY_READ_ONLY_CANDIDATE_VALIDATION",
        "bundle_root": str(bundle_root),
        "candidate_count": len(rows),
        "candidate_pins": len(rows),
        "destination_preimage_pins": len(rows),
        "support_asset_count": len(support_rows),
        "support_asset_pins": len(support_rows),
        "candidates": rows,
        "support_assets": support_rows,
        "aks74u_bolt_evidence": bolt_evidence,
        "writes": {"game": False, "docs": False, "editor": False, "portal": False, "git": False},
        "proof_ceiling": [
            "exact candidate and destination-preimage byte custody",
            "static prefab GUID/reference/visibility-root closure",
            "candidate-aware donor motion-owner ancestry",
            "AKS-74U create-only support-asset and topology closure",
            "UNVERIFIED editor compile, runtime, visual fit, ADS, reload, IK, audio, roster, and Portal delivery",
        ],
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Validate one pinned TEMP DXRP weapon-promotion bundle.")
    parser.add_argument("--bundle-root", required=True, type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        result = validate_bundle(args.bundle_root)
    except (ContractError, OSError, ValueError) as exc:
        print(f"DXRP_WEAPON_CANDIDATE_VALIDATION_ERROR: {exc}")
        return 2
    print("DXRP_WEAPON_CANDIDATE_VALIDATION=" + json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
