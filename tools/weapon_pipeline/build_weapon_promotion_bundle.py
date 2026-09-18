#!/usr/bin/env python3
"""Build one fail-closed, review-only DXRP weapon promotion bundle in TEMP.

The bundle regenerates eight current deterministic candidates for AK-47,
AKS-74U, AR-15, Desert Eagle, M1911, M870, and SR-25.  It verifies byte
custody before loading any builder, writes candidates and structural JSON
diffs only below the operating-system temporary directory, and deliberately
exposes no product promotion mode.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import stat
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any, Iterable, Mapping, Sequence


class ContractError(RuntimeError):
    """Raised when custody, TEMP containment, or candidate identity drifts."""


REPO_ROOT = Path(__file__).resolve().parents[2]
TOOL_ROOT = Path(__file__).resolve().parent
ORCHESTRATOR_PATH = Path(__file__).resolve()
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve(strict=True)
REPORT_FILE = "weapon_promotion_bundle_report.json"
CHANGE_CATEGORIES = (
    "guid_reference",
    "transform",
    "metadata",
    "schema_version",
    "structure",
    "other",
)


@dataclass(frozen=True)
class FilePin:
    path: Path
    bytes: int
    sha256: str


@dataclass(frozen=True)
class CandidatePin:
    file: str
    bytes: int
    sha256: str
    destination: str


@dataclass(frozen=True)
class SupportAssetPin:
    source_file: str
    relative_path: str
    bytes: int
    sha256: str
    destination: str


def _repo(relative: str) -> Path:
    return REPO_ROOT / relative


BUILDER_PINS: Mapping[str, FilePin] = {
    "pipeline_io": FilePin(
        TOOL_ROOT / "pipeline_io.py",
        12_537,
        "B74664DA4D63248A6475990BF99BAF9D9EFF6C0209717CFC6A7B9BE481E22FBA",
    ),
    "aks74u": FilePin(
        TOOL_ROOT / "build_aks74u_mp5_fit_candidate.py",
        54_643,
        "9B671E23CE8436F659580A407BA3C888804094AFDCB86A62FA51ADE0D34BCAB0",
    ),
    "aks74u_bolt_split": FilePin(
        TOOL_ROOT / "build_aks74u_bolt_split_candidate.py",
        35_401,
        "5468DABC89C22A48921EA6DA65AFE1525858BA90CF4E5653DE18129721CA22E3",
    ),
    "aks74u_bolt_worker": FilePin(
        TOOL_ROOT / "blender_split_aks74u_bolt.py",
        25_602,
        "F3130E5459233DC7DC651E60C9A047BC89F03120BE2252A05D25AB66B4E9A8FE",
    ),
    "deserteagle": FilePin(
        TOOL_ROOT / "build_deserteagle_minimal_correction_candidate.py",
        29_478,
        "786A843C7A68113B36475F55E0F2CF00909C091F2C5A9945AB85EE8F30398585",
    ),
    "deserteagle_common_origin_helper": FilePin(
        TOOL_ROOT / "build_deserteagle_common_origin_candidate.py",
        50_237,
        "3C2DBD5574C7433422A74EC345BC0CA3B985C374BB2741138EABDD9CFD425446",
    ),
    "m1911_materializer": FilePin(
        TOOL_ROOT / "build_m1911_view_prefab.py",
        18_239,
        "6FA2DCED4DBF971FC8A1E08BBF4D3964EA749A8D258E641EC7A943B0B35A00AA",
    ),
    "m1911": FilePin(
        TOOL_ROOT / "build_m1911_common_origin_candidate.py",
        34_852,
        "18DDA266F38E2FE7797B777E696E28A9AD41E878A72B5C1E9C10571DC9E31389",
    ),
    "m4_current_body": FilePin(
        TOOL_ROOT / "build_m4_current_body_wrapper_candidates.py",
        61_780,
        "9276DD243FF27EB87E7F757EB700198F27A36AFC335232EBE6AA51702FDA6663",
    ),
    "renderer_wrappers": FilePin(
        TOOL_ROOT / "build_viewmodel_renderer_wrappers.py",
        39_979,
        "7A8B36BE2CE24E57C00A5195DED5AD6E905C6BB9DB6B50EE9BC9F7DBEF7002DE",
    ),
    "visibility_roots": FilePin(
        TOOL_ROOT / "build_viewmodel_visibility_root_candidates.py",
        36_221,
        "25005FD121C050F55CBD79C36A8093D00592C3AC09B902978809006488F0882C",
    ),
}


SOURCE_PINS: Mapping[str, FilePin] = {
    "source2viewer_parser": FilePin(
        Path(r"C:\Tools\Source2Viewer\Source2Viewer-CLI.exe"),
        108_603_232,
        "36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0",
    ),
    "compiled_mp5_viewmodel": FilePin(
        Path(
            r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
            r"\sbox_smg_mp5\v_mp5.bb3ccbb95b323f14.vmdl_c"
        ),
        1_602_903,
        "87B8E0757BB7AE6B8B784A261449DABCD9A37125A0AC8B420C115244F65B4F24",
    ),
    "compiled_usp_viewmodel": FilePin(
        Path(
            r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
            r"\sbox_pistol_usp\v_usp.c9a4333387e69880.vmdl_c"
        ),
        1_394_438,
        "439F8B2B676F310318EC4080D577DC16EA3549249564BADDDCD0B0CDE4A520F5",
    ),
    "compiled_m4_viewmodel": FilePin(
        Path(
            r"D:\Steam\steamapps\common\sbox\download\assets\models\weapons"
            r"\sbox_assault_m4a1\v_m4a1.3912fd337e9d3488.vmdl_c"
        ),
        2_019_792,
        "8726559C336098469AAA7C9A9A5018FE4825D3C6375EF406AB849C3EC82AABE4",
    ),
    "m4_view_prefab": FilePin(
        _repo("game/Assets/gameplay/equipment/weapons/m4a1/vm_m4a1.prefab"),
        74_922,
        "D1FBDB400EFE51583BB95267BBBB44A80B67952E5DFB55B6369C33D971029767",
    ),
    "usp_view_prefab": FilePin(
        _repo("game/Assets/gameplay/equipment/weapons/usp/vm_usp.prefab"),
        68_573,
        "C5621B02C9CE1D15104718DF8DA954696921886DFB16ABEFAD9D46AC3AE83C3C",
    ),
    "mp5_view_prefab": FilePin(
        _repo("game/Assets/gameplay/equipment/weapons/mp5/vm_mp5.prefab"),
        70_863,
        "CEFD224B7B8FA4325090691AFA66A5EE429566FF372AD526C8AFD98C20585BD2",
    ),
    "mp5_world_prefab": FilePin(
        _repo("game/Assets/gameplay/equipment/weapons/mp5/w_mp5.prefab"),
        14_998,
        "AE8C567EF8BC3CCB4462DB4D22F80061334030C6E813153FA58274CBC6927AC0",
    ),
    "aks74u_source_fbx": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/source/fab_original/aks74u.fbx"),
        2_020_924,
        "6EDAB0636B1D43744A0344FF9168766229D3D0DD68E845E7C21EDB9971B34780",
    ),
    "aks74u_body_vmdl": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_body.vmdl"),
        1_240,
        "81D746F3527C59C7C08A1B5D23634D0AC826E67019713AB4EB4016E518980A06",
    ),
    "aks74u_mag_vmdl": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl"),
        1_258,
        "2F2D3BF405BDD0CB18FE79B7A4252CBC8F3D828FC44FC8CE5C223A4A4B253089",
    ),
    "aks74u_combined_vmdl": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.vmdl"),
        1_279,
        "BE4FAEE555C240CC16257CF5D180543555A05E1A3581E874459C0DF74A51B49D",
    ),
    "aks74u_invisible_vmat": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/"
            "vm_aks74u/invisible.vmat"
        ),
        387,
        "19E2CB707EFD42A88F0C826DEE12B90E4AD7365CCB92A72D92CA6A589B3C2746",
    ),
    "aks74u_wepanim": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.wepanim"),
        16_647,
        "2440C19B1BC4BA5C21E4EFCA008823FD70D4AAE90110F43D8B5072189EFBC1DA",
    ),
    "blender_executable": FilePin(
        Path(r"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe"),
        108_687_824,
        "1E6624AF112B3C936F4B038B025EBD2BF00AE72C4B62881A6787166D71C58FA5",
    ),
    "aks74u_body_material": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u.vmat"),
        1_056,
        "F66AD9E9F82EFF1660FA319949F5233E462EBF754B27334E512841348E61A022",
    ),
    "aks74u_body_basecolor": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/source/fab_original/"
            "aks74u_body_basecolor.png"
        ),
        3_000_584,
        "84093C797D1A6E0897B42CB9151FF2D6ADEF495A07A733B5BD58C51B052C874D",
    ),
    "aks74u_body_normal": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/source/fab_original/"
            "aks74u_body_normal.png"
        ),
        1_054_317,
        "AF1A65F3812C09A349B30D7285894F990F6C2364A6830D96E768D10D9CA8ECAB",
    ),
    "aks74u_body_ao": FilePin(
        _repo("game/Assets/addons/lifepunch/lpweapons/aks74u/textures/aks74u_body_ao.png"),
        897_626,
        "FDB983857308AB9FD46F04136D2D719FCDD3C05D3CC7F7F496E37B5F0DCEA116",
    ),
    "aks74u_body_metalness": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/textures/"
            "aks74u_body_metalness.png"
        ),
        1_010_661,
        "07DE2A7B3310A456603A9A4A979B5C4F5E7175AEC75C8BD0332A2E1A6F017A2F",
    ),
    "aks74u_body_roughness": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/textures/"
            "aks74u_body_roughness.png"
        ),
        1_665_640,
        "A7F6B98B939D218D1C02EBB7477C93976E8ADDCE3AD678A70FC09E065B379FB6",
    ),
}


DESTINATION_PREIMAGE_PINS: Mapping[str, FilePin] = {
    "ak47_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/ak47/equipment/"
            "vm_ak47/vm_ak47.prefab"
        ),
        76_835,
        "F63B94EE0A127DCC45BED0F2167FD405B9101C2C675B9789DE3FBBDF87C73255",
    ),
    "aks74u_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/"
            "vm_aks74u/vm_aks74u.prefab"
        ),
        74_777,
        "D125A7298CCC00CF10F2D6F817A009D7B807515BFA3953CE1C6784CF303F5551",
    ),
    "aks74u_world": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/aks74u/equipment/"
            "w_aks74u/w_aks74u.prefab"
        ),
        17_021,
        "3D675DF533A4F107E0A4D591AF2DB81F86BF7CE422EBFD67E48D833AE459A99C",
    ),
    "deserteagle_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/deserteagle/equipment/"
            "vm_desert_eagle/vm_desert_eagle.prefab"
        ),
        73_212,
        "77A891CD69990A868726B821DC7F52FD086F3594B48752E497809559765E4227",
    ),
    "m1911_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/m1911/equipment/"
            "vm_m1911/vm_m1911.prefab"
        ),
        13_794,
        "6F1FB9FD9024C439F3E5F8CF6448AF3C8FF68AE2D99F021EDC6339431F220ED3",
    ),
    "m870_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/m870/equipment/"
            "vm_m870/vm_m870.prefab"
        ),
        70_704,
        "7BFA941833E2F023194D30758524C4158CEC61980CEAD4AC895F0FED188E2885",
    ),
    "ar15_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/ar15/equipment/"
            "vm_ar15/vm_ar15.prefab"
        ),
        77_843,
        "B573E6920D5C095FE09A5FD256BDBFE6FEFF6E03B927BD36E059AB5A02574043",
    ),
    "sr25_view": FilePin(
        _repo(
            "game/Assets/addons/lifepunch/lpweapons/sr25/equipment/"
            "vm_sr25/vm_sr25.prefab"
        ),
        85_430,
        "FFC3EB52516107EE9292821F1ECDE575E46EE85B04F1FC3B695EC21B40F6C8D4",
    ),
}


CANDIDATE_PINS: Mapping[str, CandidatePin] = {
    "ak47_view": CandidatePin(
        "vm_ak47.visibility-root.candidate.prefab",
        76_978,
        "EC32DA9D4116A36B7D800A2731DB9821EDB7C17D0201620E6D5DECB76CA3383F",
        "ak47_view",
    ),
    "aks74u_view": CandidatePin(
        "vm_aks74u.visibility-root.candidate.prefab",
        75_309,
        "786D6EE46BF6A96CDA6336582B9B4040C30A0275262089D8CB27B7291A9F8A0F",
        "aks74u_view",
    ),
    "aks74u_world": CandidatePin(
        "w_aks74u.fit-candidate.prefab",
        16_544,
        "D8756E77BC50E713B436F1F750CC2FB1E55B8AE3164590364EADC1C914D3503D",
        "aks74u_world",
    ),
    "deserteagle_view": CandidatePin(
        "vm_desert_eagle.visibility-root.candidate.prefab",
        74_373,
        "27CDBFD846AF3A08637A05C8577428BC3C30B4420259DD5CDE5502C4E4FFB6C7",
        "deserteagle_view",
    ),
    "m1911_view": CandidatePin(
        "vm_m1911.visibility-root.candidate.prefab",
        73_340,
        "D3316F70750F9BBF355D73EF3885F8C712D59F472A415E169A754FC3841BFC46",
        "m1911_view",
    ),
    "m870_view": CandidatePin(
        "vm_m870.visibility-root.candidate.prefab",
        70_839,
        "F9860C726E466007C8624DE2F2BAC68C05034DFEFD826D90FE4BE91F2546703D",
        "m870_view",
    ),
    "ar15_view": CandidatePin(
        "vm_ar15.wrapper_candidate.prefab",
        84_627,
        "92BF6A0E7ECC94A1A8F0E1391253CF2B67724D3EFDE5EDD8FFC52EB96DEE5B82",
        "ar15_view",
    ),
    "sr25_view": CandidatePin(
        "vm_sr25.wrapper_candidate.prefab",
        94_773,
        "9378D22F99F81BEDC83043B99C6FCCAA9C7C6E20D4EFFED0BD1137BCDCEE694D",
        "sr25_view",
    ),
}


SUPPORT_ASSET_PINS: Mapping[str, SupportAssetPin] = {
    "aks74u_body_minus_bolt_fbx": SupportAssetPin(
        "meshes/aks74u_body_minus_bolt.fbx",
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "source/bolt_split/aks74u_body_minus_bolt.fbx"
        ),
        1_684_556,
        "427C43D4410B0330BDAAE9C3DC606B65898622B64530BDA74A80C10426234C0C",
        (
            "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/"
            "aks74u_body_minus_bolt.fbx"
        ),
    ),
    "aks74u_bolt_fbx": SupportAssetPin(
        "meshes/aks74u_bolt.fbx",
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "source/bolt_split/aks74u_bolt.fbx"
        ),
        81_148,
        "06DE3C4729D039691804467EE698E1532175F429083DA8ECF443FB60EA48C95F",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/source/bolt_split/aks74u_bolt.fbx",
    ),
    "aks74u_body_minus_bolt_vmdl": SupportAssetPin(
        "modeldocs/aks74u_body_minus_bolt.vmdl",
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "aks74u_body_minus_bolt.vmdl"
        ),
        1_259,
        "EF3A1285977F4D335D4D54B06510224E499D52A82D54B68F697B2A2F529730ED",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl",
    ),
    "aks74u_bolt_vmdl": SupportAssetPin(
        "modeldocs/aks74u_bolt.vmdl",
        (
            "support_assets/create_only/game/Assets/addons/lifepunch/lpweapons/aks74u/"
            "aks74u_bolt.vmdl"
        ),
        1_237,
        "FDC8F6EE449FD4AC5F1BE79AC202F6A6ED0FE412CAAD416955FC089F42C8D93C",
        "game/Assets/addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl",
    ),
}

AKS_BOLT_TOPOLOGY_PIN = (
    8_678,
    "5AE32EC8589EF38669C863B668B3A8722743F8706820554033370732F1283EDC",
)
AKS_VISIBILITY_BASE_PIN = (
    73_295,
    "C2F22A4460B72DE8A27312A75B59D09CB3C5F274C3ED87048B20A24A54898090",
)


MANIFEST_PINS: Mapping[str, tuple[int, str]] = {
    "aks74u": (
        1_463,
        "3650D345D64393C89261628930F64F8F6D4B0159DAD019156E6BC385D70E2DF0",
    ),
    "m1911": (
        1_986,
        "2271B467C02423158352714FD1E53D0F489E2CF59D17D2F1ED6D6869754E6FAC",
    ),
}


VISIBILITY_INPUT_PINS: Mapping[str, tuple[int, str]] = {
    "aks74u": (
        73_160,
        "EE8F4D82DE3B12257E3EB78B8D523EB4A1D0A28907485C2A12B7248C2A6DC5FE",
    ),
    "m1911": (
        73_205,
        "099EF7340861DB3305E045238BADD0918BC59A518C6E67FC7CC8CCC64732C451",
    ),
}


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def _is_reparse(path: Path, result: os.stat_result) -> bool:
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    return path.is_symlink() or bool(
        getattr(result, "st_file_attributes", 0) & flag
    )


def _assert_no_reparse_components(path: Path) -> None:
    current = Path(os.path.abspath(os.fspath(path)))
    while True:
        try:
            result = current.lstat()
        except FileNotFoundError:
            pass
        else:
            if _is_reparse(current, result):
                raise ContractError(f"Reparse-point output is forbidden: {current}")
        parent = current.parent
        if parent == current:
            return
        current = parent


def assert_temp_output_directory(path: Path) -> Path:
    lexical = Path(os.path.abspath(os.fspath(path.expanduser())))
    _assert_no_reparse_components(lexical)
    resolved = lexical.resolve(strict=False)
    if _is_within(resolved, REPO_ROOT.resolve(strict=True)):
        raise ContractError(f"Repository output is forbidden: {resolved}")
    if resolved == SYSTEM_TEMP_ROOT or not _is_within(resolved, SYSTEM_TEMP_ROOT):
        raise ContractError(
            f"TEMP-only bundle requires a strict child of {SYSTEM_TEMP_ROOT}: {resolved}"
        )
    if resolved.exists():
        if not resolved.is_dir():
            raise ContractError(f"Output path is not a directory: {resolved}")
        if any(resolved.iterdir()):
            raise ContractError(f"Output directory must be empty: {resolved}")
    return resolved


def _pin_payload(path: Path, data: bytes) -> dict[str, object]:
    return {
        "path": str(path.resolve()),
        "bytes": len(data),
        "sha256": _sha256(data),
    }


def _verify_file_pin(pin: FilePin, label: str) -> bytes:
    path = pin.path.resolve()
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ContractError(f"Cannot read pinned {label} {path}: {exc}") from exc
    actual_sha = _sha256(data)
    if len(data) != pin.bytes or actual_sha != pin.sha256.upper():
        raise ContractError(
            f"Pinned {label} drifted at {path}: expected {pin.bytes} bytes / "
            f"{pin.sha256.upper()}, got {len(data)} / {actual_sha}"
        )
    return data


def _verify_group(
    pins: Mapping[str, FilePin], kind: str
) -> tuple[dict[str, bytes], dict[str, dict[str, object]]]:
    payloads: dict[str, bytes] = {}
    reports: dict[str, dict[str, object]] = {}
    for name, pin in pins.items():
        data = _verify_file_pin(pin, f"{kind} {name}")
        payloads[name] = data
        reports[name] = _pin_payload(pin.path, data)
    return payloads, reports


def _load_module(name: str, pin: FilePin, public_name: str | None = None) -> ModuleType:
    _verify_file_pin(pin, f"builder module {name}")
    module_name = public_name or f"_dxrp_promotion_{name}"
    spec = importlib.util.spec_from_file_location(module_name, pin.path)
    if spec is None or spec.loader is None:
        raise ContractError(f"Cannot create import spec for pinned builder {pin.path}")
    module = importlib.util.module_from_spec(spec)
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        spec.loader.exec_module(module)
    except Exception:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    return module


def _serialize_json(value: object) -> bytes:
    return (
        json.dumps(value, ensure_ascii=True, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def _serialize_manifest(value: object) -> bytes:
    return (
        json.dumps(value, ensure_ascii=True, separators=(",", ":"), sort_keys=True)
        + "\n"
    ).encode("utf-8")


def _verify_generated_pin(
    path: Path, expected_bytes: int, expected_sha256: str, label: str
) -> bytes:
    data = path.read_bytes()
    actual = _sha256(data)
    if len(data) != expected_bytes or actual != expected_sha256.upper():
        raise ContractError(
            f"Generated {label} drifted: expected {expected_bytes} / "
            f"{expected_sha256.upper()}, got {len(data)} / {actual}"
        )
    return data


def _assert_create_only_destinations_absent() -> dict[str, dict[str, object]]:
    rows: dict[str, dict[str, object]] = {}
    for name, contract in SUPPORT_ASSET_PINS.items():
        destination = REPO_ROOT / contract.destination
        _assert_no_reparse_components(destination)
        if destination.exists():
            raise ContractError(
                f"Create-only support destination already exists: {destination}"
            )
        rows[name] = {
            "path": str(destination),
            "exists": False,
            "create_only": True,
        }
    return rows


def _write_aks74u_bolt_input_manifest(
    bolt: ModuleType,
    pipeline_io: ModuleType,
    output_directory: Path,
    base_candidate: Path,
    base_report: Path,
    builder_reports: Mapping[str, Mapping[str, object]],
    source_reports: Mapping[str, Mapping[str, object]],
    destination_reports: Mapping[str, Mapping[str, object]],
) -> tuple[Path, dict[str, dict[str, object]]]:
    evidence_directory = output_directory / "manifests" / "aks74u_bolt_evidence"
    evidence_directory.mkdir()
    record_payloads = {
        "authoritative_fbx_probe": {
            "version": 1,
            "kind": "PINNED_SOURCE_CONTRACT_RECORD",
            "source": source_reports["aks74u_source_fbx"],
            "topology_contract": bolt.EXPECTED_TOPOLOGY,
            "proof_ceiling": "worker re-probes the pinned FBX during this bundle run",
        },
        "source_topology_evidence": {
            "version": 1,
            "kind": "PINNED_TOPOLOGY_EXPECTATION_RECORD",
            "source": source_reports["aks74u_source_fbx"],
            "topology_contract": bolt.EXPECTED_TOPOLOGY,
            "proof_ceiling": "closed by the generated Blender topology report",
        },
        "compiled_mp5_data_probe": {
            "version": 1,
            "kind": "PINNED_COMPILED_BIND_RECORD",
            "compiled_model": source_reports["compiled_mp5_viewmodel"],
            "parser": source_reports["source2viewer_parser"],
            "compiled_bolt_bind": bolt.EXPECTED_BIND,
        },
    }
    evidence_reports: dict[str, dict[str, object]] = {}
    for name, payload in record_payloads.items():
        path = evidence_directory / f"{name}.json"
        data = _serialize_json(payload)
        pipeline_io.write_bytes_atomic(path, data)
        if path.read_bytes() != data:
            raise ContractError(f"Generated AKS bolt input evidence changed: {name}")
        evidence_reports[name] = _pin_payload(path, data)

    base_candidate_data = base_candidate.read_bytes()
    base_report_data = base_report.read_bytes()
    manifest = {
        "version": 1,
        "purpose": bolt.PURPOSE,
        "inputs": {
            "authoritative_fbx": source_reports["aks74u_source_fbx"],
            "authoritative_fbx_probe": evidence_reports[
                "authoritative_fbx_probe"
            ],
            "source_topology_evidence": evidence_reports[
                "source_topology_evidence"
            ],
            "base_fit_candidate_prefab": _pin_payload(
                base_candidate, base_candidate_data
            ),
            "base_fit_report": _pin_payload(base_report, base_report_data),
            "existing_fit_builder": builder_reports["aks74u"],
            "current_target_vm_prefab": destination_reports["aks74u_view"],
            "mp5_donor_vm_prefab": source_reports["mp5_view_prefab"],
            "current_body_modeldoc": source_reports["aks74u_body_vmdl"],
            "current_mag_modeldoc": source_reports["aks74u_mag_vmdl"],
            "compiled_mp5_model": source_reports["compiled_mp5_viewmodel"],
            "compiled_mp5_data_probe": evidence_reports[
                "compiled_mp5_data_probe"
            ],
            "source2viewer_parser": source_reports["source2viewer_parser"],
            "blender_executable": source_reports["blender_executable"],
            "body_material": source_reports["aks74u_body_material"],
            "body_basecolor": source_reports["aks74u_body_basecolor"],
            "body_normal": source_reports["aks74u_body_normal"],
            "body_ao": source_reports["aks74u_body_ao"],
            "body_metalness": source_reports["aks74u_body_metalness"],
            "body_roughness": source_reports["aks74u_body_roughness"],
        },
        "blender_version": bolt.EXPECTED_BLENDER_VERSION,
        "compiled_bolt_bind": bolt.EXPECTED_BIND,
        "topology_contract": bolt.EXPECTED_TOPOLOGY,
    }
    manifest_path = output_directory / "manifests" / "aks74u_bolt_input.json"
    manifest_data = bolt._json_bytes(manifest)
    pipeline_io.write_bytes_atomic(manifest_path, manifest_data)
    if manifest_path.read_bytes() != manifest_data:
        raise ContractError("Generated AKS bolt input manifest changed")
    bolt.load_input_contract(manifest_path)
    evidence_reports["input_manifest"] = _pin_payload(
        manifest_path, manifest_data
    )
    return manifest_path, evidence_reports


def _json_pointer(path: str, key: object) -> str:
    token = str(key).replace("~", "~0").replace("/", "~1")
    return f"{path}/{token}"


def _canonical_subtree(value: object) -> bytes:
    return json.dumps(
        value, ensure_ascii=True, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")


def _value_summary(value: object) -> object:
    if isinstance(value, dict):
        raw = _canonical_subtree(value)
        return {
            "kind": "object",
            "keys": len(value),
            "canonical_bytes": len(raw),
            "canonical_sha256": _sha256(raw),
        }
    if isinstance(value, list):
        raw = _canonical_subtree(value)
        return {
            "kind": "array",
            "items": len(value),
            "canonical_bytes": len(raw),
            "canonical_sha256": _sha256(raw),
        }
    return value


def structural_diff(before: object, after: object, path: str = "") -> list[dict[str, object]]:
    """Return a deterministic JSON-Pointer structural diff without dumping subtrees."""

    changes: list[dict[str, object]] = []
    if type(before) is not type(after):
        return [
            {
                "op": "replace",
                "path": path,
                "before": _value_summary(before),
                "after": _value_summary(after),
            }
        ]
    if isinstance(before, dict):
        before_keys = set(before)
        after_keys = set(after)
        for key in sorted(before_keys - after_keys):
            changes.append(
                {
                    "op": "remove",
                    "path": _json_pointer(path, key),
                    "before": _value_summary(before[key]),
                }
            )
        for key in sorted(after_keys - before_keys):
            changes.append(
                {
                    "op": "add",
                    "path": _json_pointer(path, key),
                    "after": _value_summary(after[key]),
                }
            )
        for key in sorted(before_keys & after_keys):
            changes.extend(
                structural_diff(before[key], after[key], _json_pointer(path, key))
            )
        return changes
    if isinstance(before, list):
        common = min(len(before), len(after))
        for index in range(common):
            changes.extend(
                structural_diff(
                    before[index], after[index], _json_pointer(path, index)
                )
            )
        for index in range(len(before) - 1, common - 1, -1):
            changes.append(
                {
                    "op": "remove",
                    "path": _json_pointer(path, index),
                    "before": _value_summary(before[index]),
                }
            )
        for index in range(common, len(after)):
            changes.append(
                {
                    "op": "add",
                    "path": _json_pointer(path, index),
                    "after": _value_summary(after[index]),
                }
            )
        return changes
    if before != after:
        changes.append(
            {"op": "replace", "path": path, "before": before, "after": after}
        )
    return changes


def classify_change(change: Mapping[str, object]) -> str:
    """Classify one raw JSON delta without hiding it from the review report."""

    path = change.get("path")
    operation = change.get("op")
    if not isinstance(path, str) or operation not in {"add", "remove", "replace"}:
        raise ContractError(f"Malformed structural change record: {change!r}")
    if path.endswith(("/__guid", "/component_id", "/go")):
        return "guid_reference"
    if path.endswith(("/Position", "/Rotation", "/Scale")):
        return "transform"
    if path == "/RootObject/Name":
        return "metadata"
    if path.endswith("/__version"):
        return "schema_version"
    if operation in {"add", "remove"}:
        return "structure"
    return "other"


def summarize_change_categories(
    changes: Iterable[Mapping[str, object]],
) -> dict[str, int]:
    counts = {category: 0 for category in CHANGE_CATEGORIES}
    for change in changes:
        counts[classify_change(change)] += 1
    return counts


def _decode_json(data: bytes, label: str) -> object:
    try:
        return json.loads(data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ContractError(f"Pinned {label} is not valid UTF-8 JSON: {exc}") from exc


def _candidate_locations(
    aks_world_path: Path,
    aks74u_view_path: Path,
    deserteagle_view_path: Path,
    m4_result: Mapping[str, Any],
    visibility_report: Mapping[str, Any],
) -> dict[str, Path]:
    rows = visibility_report.get("candidates")
    if not isinstance(rows, list):
        raise ContractError("Visibility-root report omitted its candidate list")
    visibility_locations: dict[str, Path] = {}
    for row in rows:
        if not isinstance(row, Mapping):
            raise ContractError("Visibility-root report contains a malformed row")
        family = row.get("family")
        output = row.get("output")
        if not isinstance(family, str) or not isinstance(output, Mapping):
            raise ContractError("Visibility-root report row omitted family or output")
        path = output.get("path")
        if not isinstance(path, str):
            raise ContractError(f"Visibility-root output path is missing for {family}")
        if family in visibility_locations:
            raise ContractError(f"Duplicate visibility-root family: {family}")
        visibility_locations[family] = Path(path)
    expected_visibility_families = {
        "ak47",
        "aks74u",
        "m1911",
        "m870",
    }
    if set(visibility_locations) != expected_visibility_families:
        raise ContractError(
            "Visibility-root output family set drifted: "
            f"{sorted(visibility_locations)}"
        )
    return {
        "ak47_view": visibility_locations["ak47"],
        "aks74u_view": aks74u_view_path,
        "aks74u_world": aks_world_path,
        "deserteagle_view": deserteagle_view_path,
        "m1911_view": visibility_locations["m1911"],
        "m870_view": visibility_locations["m870"],
        "ar15_view": Path(m4_result["output_directory"])
        / str(m4_result["files"]["ar15"]),
        "sr25_view": Path(m4_result["output_directory"])
        / str(m4_result["files"]["sr25"]),
    }


def _build_visibility_candidates_without_deserteagle(
    visibility: ModuleType,
    generated_inputs: Mapping[str, Any],
    output_directory: Path,
) -> dict[str, object]:
    """Run the shared visibility builder for the four still-rootless families.

    Desert Eagle is deliberately excluded because its narrow correction builder
    already adds ``AdditionalRendererRoot`` together with the one required
    magazine bind wrapper.  The shared builder's older Desert Eagle contract is
    the broad/common-origin shape and must not feed this promotion bundle.
    """

    original_contracts = visibility.WEAPON_CONTRACTS
    original_generated = visibility.GENERATED_FAMILIES
    original_by_family = visibility.CONTRACTS_BY_FAMILY
    scoped_contracts = tuple(
        contract
        for contract in original_contracts
        if contract.family != "deserteagle"
    )
    expected_families = {"ak47", "aks74u", "m1911", "m870"}
    if {contract.family for contract in scoped_contracts} != expected_families:
        raise ContractError("Scoped visibility-root family set drifted")
    if set(generated_inputs) != {"aks74u", "m1911"}:
        raise ContractError("Scoped visibility generated-input set drifted")

    try:
        visibility.WEAPON_CONTRACTS = scoped_contracts
        visibility.GENERATED_FAMILIES = frozenset({"aks74u", "m1911"})
        visibility.CONTRACTS_BY_FAMILY = {
            contract.family: contract for contract in scoped_contracts
        }
        report = visibility.build_candidates(
            generated_inputs,
            output_directory=output_directory,
        )
    finally:
        visibility.WEAPON_CONTRACTS = original_contracts
        visibility.GENERATED_FAMILIES = original_generated
        visibility.CONTRACTS_BY_FAMILY = original_by_family

    if report.get("candidate_count") != 4:
        raise ContractError("Scoped visibility-root builder did not emit four files")
    report["promotion_bundle_scope"] = {
        "included_families": sorted(expected_families),
        "excluded_families": ["deserteagle"],
        "reason": (
            "Desert Eagle uses the pinned minimal correction builder directly; "
            "the older broad/common-origin visibility input is not generated or consumed."
        ),
    }
    return report


def _prefab_set(root: Path) -> set[Path]:
    return {path.resolve() for path in root.rglob("*.prefab") if path.is_file()}


def build_temp_bundle(output_directory: Path | None = None) -> dict[str, object]:
    """Regenerate all pinned candidates and emit a TEMP-only review bundle."""

    orchestrator_payload = ORCHESTRATOR_PATH.read_bytes()
    orchestrator_report = _pin_payload(ORCHESTRATOR_PATH, orchestrator_payload)
    builder_payloads, builder_reports = _verify_group(BUILDER_PINS, "builder")
    source_payloads, source_reports = _verify_group(SOURCE_PINS, "source")
    destination_payloads, destination_reports = _verify_group(
        DESTINATION_PREIMAGE_PINS, "destination preimage"
    )
    create_only_destinations = _assert_create_only_destinations_absent()

    if output_directory is None:
        output_directory = Path(tempfile.mkdtemp(prefix="dxrp_weapon_promotion_bundle_"))
        output_directory = assert_temp_output_directory(output_directory)
    else:
        output_directory = assert_temp_output_directory(output_directory)
        output_directory.mkdir(parents=False, exist_ok=True)

    pipeline_io = _load_module(
        "pipeline_io", BUILDER_PINS["pipeline_io"], public_name="pipeline_io"
    )
    _load_module(
        "renderer_wrappers",
        BUILDER_PINS["renderer_wrappers"],
        public_name="build_viewmodel_renderer_wrappers",
    )
    aks = _load_module("aks74u", BUILDER_PINS["aks74u"])
    bolt = _load_module(
        "aks74u_bolt_split",
        BUILDER_PINS["aks74u_bolt_split"],
        public_name="build_aks74u_bolt_split_candidate",
    )
    _load_module(
        "deserteagle_common_origin_helper",
        BUILDER_PINS["deserteagle_common_origin_helper"],
        public_name="build_deserteagle_common_origin_candidate",
    )
    deagle = _load_module(
        "deserteagle",
        BUILDER_PINS["deserteagle"],
        public_name="build_deserteagle_minimal_correction_candidate",
    )
    _load_module(
        "m1911_materializer",
        BUILDER_PINS["m1911_materializer"],
        public_name="build_m1911_view_prefab",
    )
    m1911 = _load_module("m1911", BUILDER_PINS["m1911"])
    m4 = _load_module("m4_current_body", BUILDER_PINS["m4_current_body"])
    visibility = _load_module(
        "visibility_roots",
        BUILDER_PINS["visibility_roots"],
        public_name="build_viewmodel_visibility_root_candidates",
    )

    manifests_dir = output_directory / "manifests"
    manifests_dir.mkdir()
    aks_manifest = manifests_dir / "aks74u_compiled_bind.json"
    m1911_manifest = manifests_dir / "m1911_compiled_bind.json"
    manifest_payloads = {
        "aks74u": _serialize_manifest(aks.compiled_bind_manifest_template()),
        "m1911": _serialize_manifest(m1911.compiled_bind_manifest_template()),
    }
    generated_manifest_reports: dict[str, dict[str, object]] = {}
    for name, path in (
        ("aks74u", aks_manifest),
        ("m1911", m1911_manifest),
    ):
        expected_bytes, expected_sha = MANIFEST_PINS[name]
        data = manifest_payloads[name]
        if len(data) != expected_bytes or _sha256(data) != expected_sha:
            raise ContractError(f"Pinned generated {name} manifest template drifted")
        pipeline_io.write_bytes_atomic(path, data)
        written = _verify_generated_pin(
            path, expected_bytes, expected_sha, f"{name} bind manifest"
        )
        generated_manifest_reports[name] = _pin_payload(path, written)

    candidates_root = output_directory / "candidates"
    candidates_root.mkdir()
    intermediates_root = output_directory / "evidence" / "intermediates"
    intermediates_root.mkdir(parents=True)
    aks_result = aks.build_temp_candidates(
        aks_manifest, intermediates_root / "aks74u"
    )
    aks_world_source = Path(aks_result["output_directory"]) / str(
        aks_result["files"]["world"]
    )
    aks_world_path = candidates_root / "aks74u" / str(
        aks_result["files"]["world"]
    )
    aks_world_payload = aks_world_source.read_bytes()
    pipeline_io.write_bytes_atomic(aks_world_path, aks_world_payload)
    if aks_world_path.read_bytes() != aks_world_payload:
        raise ContractError("AKS-74U world candidate changed during placement")
    aks_world_source.unlink()
    deagle_report = deagle.build_candidate(
        output_directory=intermediates_root / "deserteagle",
    )
    deagle_output = Path(str(deagle_report["output"]["path"]))
    deagle_payload = _verify_generated_pin(
        deagle_output,
        CANDIDATE_PINS["deserteagle_view"].bytes,
        CANDIDATE_PINS["deserteagle_view"].sha256,
        "Desert Eagle minimal correction",
    )

    m1911_dir = intermediates_root / "m1911"
    m1911_report = m1911.build_candidate(
        output_directory=m1911_dir,
        compiled_bind_path=m1911_manifest,
        compiled_bind_sha256=_sha256(manifest_payloads["m1911"]),
    )
    m1911_output = Path(str(m1911_report["output"]["path"]))

    m4_result = m4.build_temp_candidates(candidates_root / "m4_current_body")
    generated_visibility_paths = {
        "aks74u": Path(aks_result["output_directory"])
        / str(aks_result["files"]["viewmodel"]),
        "m1911": m1911_output,
    }
    visibility_inputs: dict[str, Any] = {}
    for family, path in generated_visibility_paths.items():
        expected_bytes, expected_sha = VISIBILITY_INPUT_PINS[family]
        _verify_generated_pin(
            path,
            expected_bytes,
            expected_sha,
            f"{family} pre-visibility candidate",
        )
        visibility_inputs[family] = visibility.InputPin(
            path=path,
            expected_bytes=expected_bytes,
            expected_sha256=expected_sha,
        )
    visibility_dir = candidates_root / "visibility_roots"
    visibility_dir.mkdir()
    visibility_report = _build_visibility_candidates_without_deserteagle(
        visibility,
        visibility_inputs,
        visibility_dir,
    )
    aks_visibility_rows = [
        row
        for row in visibility_report["candidates"]
        if row.get("family") == "aks74u"
    ]
    if len(aks_visibility_rows) != 1:
        raise ContractError("Visibility report did not contain one AKS-74U row")
    aks_visibility_row = aks_visibility_rows[0]
    aks_visibility_path = Path(str(aks_visibility_row["output"]["path"]))
    visibility_bytes, visibility_sha = AKS_VISIBILITY_BASE_PIN
    aks_visibility_payload = _verify_generated_pin(
        aks_visibility_path,
        visibility_bytes,
        visibility_sha,
        "AKS-74U visibility-root base",
    )
    aks_visibility_evidence_path = (
        intermediates_root
        / "aks74u_visibility_base"
        / CANDIDATE_PINS["aks74u_view"].file
    )
    pipeline_io.write_bytes_atomic(
        aks_visibility_evidence_path, aks_visibility_payload
    )
    if aks_visibility_evidence_path.read_bytes() != aks_visibility_payload:
        raise ContractError("AKS-74U visibility-root evidence changed during placement")
    aks_visibility_row["output"] = {
        **aks_visibility_row["output"],
        "path": str(aks_visibility_evidence_path),
    }

    aks_base_report = Path(aks_result["output_directory"]) / str(
        aks_result["files"]["report"]
    )
    bolt_input_manifest, bolt_input_evidence = _write_aks74u_bolt_input_manifest(
        bolt,
        pipeline_io,
        output_directory,
        aks_visibility_evidence_path,
        aks_base_report,
        builder_reports,
        source_reports,
        destination_reports,
    )
    bolt_directory = intermediates_root / "aks74u_bolt_split"
    bolt_result = bolt.build_bundle(
        bolt_input_manifest,
        bolt_directory,
        worker_path=BUILDER_PINS["aks74u_bolt_worker"].path,
    )
    bolt_manifest_path = Path(str(bolt_result["bundle_manifest"]))
    bolt_manifest_payload = bolt_manifest_path.read_bytes()
    topology_path = bolt_directory / "evidence" / "bolt_split_topology.json"
    topology_bytes, topology_sha = AKS_BOLT_TOPOLOGY_PIN
    topology_payload = _verify_generated_pin(
        topology_path,
        topology_bytes,
        topology_sha,
        "AKS-74U bolt topology report",
    )
    bolt_evidence_reports = {
        "bundle_manifest": {
            **_pin_payload(bolt_manifest_path, bolt_manifest_payload),
            "relative_path": bolt_manifest_path.relative_to(
                output_directory
            ).as_posix(),
        },
        "topology_report": {
            **_pin_payload(topology_path, topology_payload),
            "relative_path": topology_path.relative_to(
                output_directory
            ).as_posix(),
        },
    }

    support_asset_reports: dict[str, dict[str, object]] = {}
    for name, expected in SUPPORT_ASSET_PINS.items():
        source = bolt_directory / expected.source_file
        data = _verify_generated_pin(
            source, expected.bytes, expected.sha256, f"{name} support source"
        )
        destination = output_directory / expected.relative_path
        pipeline_io.write_bytes_atomic(destination, data)
        written = _verify_generated_pin(
            destination, expected.bytes, expected.sha256, f"{name} support asset"
        )
        support_asset_reports[name] = {
            **_pin_payload(destination, written),
            "relative_path": expected.relative_path,
            "source_relative_path": source.relative_to(
                output_directory
            ).as_posix(),
            "product_destination": expected.destination,
            "role": "CREATE_ONLY_PROMOTION_SUPPORT_ASSET",
            "create_only": True,
            "product_destination_exists": False,
            "product_write_performed": False,
        }
    expected_support_assets = {
        (output_directory / contract.relative_path).resolve()
        for contract in SUPPORT_ASSET_PINS.values()
    }
    actual_support_assets = {
        path.resolve()
        for path in (output_directory / "support_assets").rglob("*")
        if path.is_file()
    }
    if actual_support_assets != expected_support_assets:
        raise ContractError("Create-only support asset set drifted")

    bolt_candidate_source = (
        bolt_directory / "prefabs" / "vm_aks74u.bolt-split-candidate.prefab"
    )
    bolt_candidate_payload = bolt_candidate_source.read_bytes()
    aks74u_final_path = visibility_dir / CANDIDATE_PINS["aks74u_view"].file
    pipeline_io.write_bytes_atomic(aks74u_final_path, bolt_candidate_payload)
    if aks74u_final_path.read_bytes() != bolt_candidate_payload:
        raise ContractError("AKS-74U bolt candidate changed during final placement")

    deagle_final_path = visibility_dir / CANDIDATE_PINS["deserteagle_view"].file
    pipeline_io.write_bytes_atomic(deagle_final_path, deagle_payload)
    deagle_final_payload = _verify_generated_pin(
        deagle_final_path,
        CANDIDATE_PINS["deserteagle_view"].bytes,
        CANDIDATE_PINS["deserteagle_view"].sha256,
        "Desert Eagle final minimal correction",
    )
    if deagle_final_payload != deagle_payload:
        raise ContractError("Desert Eagle minimal correction changed during placement")
    candidate_locations = _candidate_locations(
        aks_world_path,
        aks74u_final_path,
        deagle_final_path,
        m4_result,
        visibility_report,
    )

    intermediate_reports: dict[str, dict[str, object]] = {}
    for family, path in generated_visibility_paths.items():
        data = path.read_bytes()
        intermediate_reports[family] = {
            **_pin_payload(path, data),
            "relative_path": path.relative_to(output_directory).as_posix(),
            "role": "NON_PROMOTABLE_VISIBILITY_INPUT_EVIDENCE",
            "promotable": False,
            "feeds_candidate": f"{family}_view",
        }
    intermediate_reports["deserteagle"] = {
        **_pin_payload(deagle_output, deagle_payload),
        "relative_path": deagle_output.relative_to(output_directory).as_posix(),
        "role": "NON_PROMOTABLE_MINIMAL_BUILDER_EVIDENCE",
        "promotable": False,
        "feeds_candidate": "deserteagle_view",
    }
    intermediate_reports["aks74u_visibility_base"] = {
        **_pin_payload(aks_visibility_evidence_path, aks_visibility_payload),
        "relative_path": aks_visibility_evidence_path.relative_to(
            output_directory
        ).as_posix(),
        "role": "NON_PROMOTABLE_VISIBILITY_ROOT_BASE_EVIDENCE",
        "promotable": False,
        "feeds_candidate": "aks74u_view",
    }
    intermediate_reports["aks74u_bolt_split"] = {
        **_pin_payload(bolt_candidate_source, bolt_candidate_payload),
        "relative_path": bolt_candidate_source.relative_to(
            output_directory
        ).as_posix(),
        "role": "NON_PROMOTABLE_BOLT_BUILDER_EVIDENCE",
        "promotable": False,
        "feeds_candidate": "aks74u_view",
    }

    expected_candidates = {path.resolve() for path in candidate_locations.values()}
    actual_candidates = _prefab_set(candidates_root)
    if actual_candidates != expected_candidates or len(actual_candidates) != 8:
        raise ContractError(
            "Candidate prefab set drifted: expected exactly eight promotable files; "
            f"got {sorted(str(path) for path in actual_candidates)}"
        )
    expected_intermediates = {
        path.resolve() for path in generated_visibility_paths.values()
    } | {
        deagle_output.resolve(),
        aks_visibility_evidence_path.resolve(),
        bolt_candidate_source.resolve(),
    }
    actual_intermediates = _prefab_set(intermediates_root)
    if actual_intermediates != expected_intermediates or len(actual_intermediates) != 5:
        raise ContractError(
            "Intermediate prefab set drifted: expected exactly five evidence files; "
            f"got {sorted(str(path) for path in actual_intermediates)}"
        )

    candidate_payloads: dict[str, bytes] = {}
    candidate_reports: dict[str, dict[str, object]] = {}
    for name, expected in CANDIDATE_PINS.items():
        path = candidate_locations[name]
        data = _verify_generated_pin(
            path, expected.bytes, expected.sha256, f"{name} candidate"
        )
        candidate_payloads[name] = data
        candidate_reports[name] = {
            **_pin_payload(path, data),
            "relative_path": path.relative_to(output_directory).as_posix(),
            "destination_preimage": expected.destination,
            "role": "PROMOTABLE_REVIEW_CANDIDATE",
            "promotable": True,
            "measured_and_accepted": False,
            "visual_accepted": False,
            "runtime_accepted": False,
            "portal_accepted": False,
        }

    diffs_dir = output_directory / "structural_diffs"
    diffs_dir.mkdir()
    diff_reports: dict[str, dict[str, object]] = {}
    for name, candidate_data in candidate_payloads.items():
        destination_key = CANDIDATE_PINS[name].destination
        before_data = destination_payloads[destination_key]
        changes = structural_diff(
            _decode_json(before_data, f"{name} destination preimage"),
            _decode_json(candidate_data, f"{name} candidate"),
        )
        operation_counts = {
            operation: sum(1 for change in changes if change["op"] == operation)
            for operation in ("add", "remove", "replace")
        }
        change_categories = summarize_change_categories(changes)
        diff_document = {
            "version": 1,
            "kind": "unified_json_structural_diff_summary",
            "candidate": name,
            "destination_preimage": destination_reports[destination_key],
            "candidate_output": {
                "relative_path": candidate_reports[name]["relative_path"],
                "bytes": candidate_reports[name]["bytes"],
                "sha256": candidate_reports[name]["sha256"],
                "destination_preimage": destination_key,
                "measured_and_accepted": False,
                "visual_accepted": False,
                "runtime_accepted": False,
                "portal_accepted": False,
            },
            "change_count": len(changes),
            "operation_counts": operation_counts,
            "change_categories": change_categories,
            "changes": changes,
            "acceptance": {
                "measured": False,
                "visual": False,
                "runtime": False,
                "portal": False,
            },
        }
        diff_bytes = _serialize_json(diff_document)
        diff_path = diffs_dir / f"{name}.json"
        pipeline_io.write_bytes_atomic(diff_path, diff_bytes)
        if diff_path.read_bytes() != diff_bytes:
            raise ContractError(f"Structural diff bytes changed for {name}")
        diff_reports[name] = {
            **_pin_payload(diff_path, diff_bytes),
            "relative_path": diff_path.relative_to(output_directory).as_posix(),
            "change_count": len(changes),
            "operation_counts": operation_counts,
            "change_categories": change_categories,
        }

    # Re-read all authoritative inputs after generation.  This detects a drift
    # or accidental product mutation during the bundle run before PASS exists.
    for name, pin in BUILDER_PINS.items():
        if _verify_file_pin(pin, f"post-build builder {name}") != builder_payloads[name]:
            raise ContractError(f"Pinned builder changed during generation: {name}")
    for name, pin in SOURCE_PINS.items():
        if _verify_file_pin(pin, f"post-build source {name}") != source_payloads[name]:
            raise ContractError(f"Pinned source changed during generation: {name}")
    for name, pin in DESTINATION_PREIMAGE_PINS.items():
        if (
            _verify_file_pin(pin, f"post-build destination {name}")
            != destination_payloads[name]
        ):
            raise ContractError(f"Destination preimage changed during generation: {name}")
    if _assert_create_only_destinations_absent() != create_only_destinations:
        raise ContractError("Create-only support destination state changed during generation")
    if ORCHESTRATOR_PATH.read_bytes() != orchestrator_payload:
        raise ContractError("Executing promotion orchestrator changed during generation")

    report = {
        "version": 1,
        "result": "PASS",
        "mode": "TEMP_ONLY_REVIEW_BUNDLE_NO_PRODUCT_WRITE_MODE",
        "output_directory": str(output_directory),
        "orchestrator": orchestrator_report,
        "builders": builder_reports,
        "sources": source_reports,
        "generated_bind_manifests": generated_manifest_reports,
        "generated_aks74u_bolt_inputs": bolt_input_evidence,
        "destination_preimages": destination_reports,
        "create_only_destinations": create_only_destinations,
        "candidates": candidate_reports,
        "support_assets": support_asset_reports,
        "support_asset_evidence": bolt_evidence_reports,
        "intermediates": intermediate_reports,
        "structural_diffs": diff_reports,
        "world_weapon_left_hand_ik_candidates": {
            "included": False,
            "candidate_count": 0,
            "accepted_transforms_supplied": False,
            "transforms_generated_or_invented": False,
            "reason": (
                "Generic WorldWeaponLeftHandIk candidates are not included absent "
                "accepted per-weapon transforms."
            ),
        },
        "nested_builder_evidence": {
            "aks74u": aks_result["report"],
            "aks74u_bolt_split": bolt_result["manifest"],
            "deserteagle": deagle_report,
            "m1911": m1911_report,
            "m4_current_body": m4_result["report"],
            "visibility_roots": visibility_report,
        },
        "acceptance": {
            "measured": False,
            "visual": False,
            "runtime": False,
            "portal": False,
        },
        "writes": {
            "temp_only": True,
            "game_tree": False,
            "docs": False,
            "portal": False,
            "git": False,
        },
        "proof_ceiling": [
            "exact byte/SHA custody for the executing promotion orchestrator",
            "byte custody for every invoked builder and support module",
            "byte custody for every authoritative source and destination preimage",
            "exact regeneration of eight deterministic TEMP candidate files",
            "four exact-pinned create-only AKS-74U bolt support assets",
            "AKS-74U visibility-root preservation followed by tag_bolt split",
            "reviewable JSON-Pointer structural diff summaries",
            "destination preimages unchanged after generation",
            "NO invented transforms",
            "NO game, documentation, Portal, or Git write path",
            "UNVERIFIED editor asset compile and code compile",
            "UNVERIFIED rendered first-person and third-person fit",
            "UNVERIFIED ADS, reload, moving-part, IK, and audio behavior",
            "NO selector, trigger, stock, or third-person AKS-74U motion claim",
            "UNVERIFIED dev roster, spawn, market, and private Portal delivery",
        ],
    }
    report_bytes = _serialize_json(report)
    report_path = output_directory / REPORT_FILE
    pipeline_io.write_bytes_atomic(report_path, report_bytes)
    if report_path.read_bytes() != report_bytes:
        raise ContractError("Consolidated report bytes changed after write")

    return {
        "output_directory": str(output_directory),
        "report": report,
        "report_pin": {
            **_pin_payload(report_path, report_bytes),
            "relative_path": REPORT_FILE,
        },
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Regenerate the pinned DXRP weapon candidates and structural diffs "
            "below system TEMP only. There is no product-write mode."
        )
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Empty strict child of system TEMP; default is a new TEMP directory.",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        result = build_temp_bundle(args.output_dir)
    except (ContractError, OSError, RuntimeError, ValueError) as exc:
        print(f"DXRP_WEAPON_PROMOTION_BUNDLE_ERROR: {exc}", file=sys.stderr)
        return 2
    print(
        "DXRP_WEAPON_PROMOTION_BUNDLE="
        + json.dumps(
            {
                "output_directory": result["output_directory"],
                "report_pin": result["report_pin"],
                "mode": result["report"]["mode"],
                "candidate_count": len(result["report"]["candidates"]),
                "game_tree_written": False,
                "docs_written": False,
                "portal_written": False,
                "git_written": False,
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
