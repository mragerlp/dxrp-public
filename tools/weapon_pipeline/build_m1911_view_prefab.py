#!/usr/bin/env python3
"""Materialize the M1911 view-model prefab from its USP prefab-instance patch.

The current M1911 source prefab is a prefab-instance patch. s&box compiles the
file, but ``GameObject.GetPrefab`` does not expose the inherited root
``ViewModel`` component. This builder expands the pinned USP donor with the
instance GUID map already stored in the M1911 source, applies the existing
property overrides, and inserts the three authored M1911 overlay objects.

The output path is required and must be a new file beneath the operating-system
temporary directory.  This helper is deliberately TEMP-only and exposes no
product-write switch, so it cannot replace the active prefab while Play is
live.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import tempfile
from pathlib import Path
from typing import Any, Iterable

import pipeline_io


WORKBENCH_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_DONOR = (
    WORKBENCH_ROOT
    / "game/Assets/gameplay/equipment/weapons/usp/vm_usp.prefab"
)
DEFAULT_PATCH = (
    WORKBENCH_ROOT
    / "game/Assets/addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab"
)
SYSTEM_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()
EXPECTED_DONOR_SHA256 = (
    "C5621B02C9CE1D15104718DF8DA954696921886DFB16ABEFAD9D46AC3AE83C3C"
)
EXPECTED_SOURCE_PATCH_SHA256 = (
    "6F1FB9FD9024C439F3E5F8CF6448AF3C8FF68AE2D99F021EDC6339431F220ED3"
)
EXPECTED_MATERIALIZED_SHA256 = (
    "5A0BACA5D93D4F3D51D5E3956D9E1C2597B4E517B1F044EB46A41353550743A8"
)
EXPECTED_DOCUMENT_KEYS = [
    "RootObject",
    "ResourceVersion",
    "ShowInMenu",
    "MenuPath",
    "MenuIcon",
    "DontBreakAsTemplate",
    "__references",
    "__version",
]
EXPECTED_DOCUMENT_ENVELOPE = {
    "ResourceVersion": 2,
    "ShowInMenu": False,
    "MenuPath": None,
    "MenuIcon": None,
    "DontBreakAsTemplate": False,
    "__references": [
        "facepunch.v_first_person_arms_human#205798",
        "facepunch.v_usp#278998",
    ],
    "__version": 2,
}
GUID_RE = re.compile(
    r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
    re.IGNORECASE,
)

EXPECTED_PARTS = {
    "m1911_body": {
        "guid": "f3a5a630-0d36-55ef-89ae-30db6d72c007",
        "parent_source_guid": "2f9a4399-5075-4c53-a4f7-6722110fa2e2",
        "parent_instance_guid": "be655d9a-d1a6-5a37-86ef-693690409455",
        "model": "addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl",
    },
    "m1911_slide": {
        "guid": "6a1eb4d5-fd63-5899-b973-3b628482aca4",
        "parent_source_guid": "38b6ec59-6293-4ece-901a-0868678dcd2c",
        "parent_instance_guid": "3df5bcb7-967a-54fd-8d9a-31dce27f441a",
        "model": "addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl",
    },
    "m1911_magazine": {
        "guid": "ad1196a1-5216-5d92-a93a-be54233bec30",
        "parent_source_guid": "56d6853a-544c-46a5-b70a-380bd2a745c8",
        "parent_instance_guid": "6204a3dd-86f0-5750-8069-8feccc8fe446",
        "model": "addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl",
    },
}
EXPECTED_POSITION = "-0.8824501,0.07494241,3.4594364"
EXPECTED_INVISIBLE_MATERIAL = (
    "addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/invisible.vmat"
)


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def remap_strings(value: Any, mapping: dict[str, str]) -> Any:
    if isinstance(value, dict):
        return {key: remap_strings(item, mapping) for key, item in value.items()}
    if isinstance(value, list):
        return [remap_strings(item, mapping) for item in value]
    if isinstance(value, str):
        return mapping.get(value, value)
    return value


def walk_objects(root: dict[str, Any]) -> Iterable[dict[str, Any]]:
    yield root
    for child in root.get("Children", []) or []:
        yield from walk_objects(child)


def walk_components(root: dict[str, Any]) -> Iterable[dict[str, Any]]:
    for node in walk_objects(root):
        yield from node.get("Components", []) or []


def index_definitions(root: dict[str, Any]) -> dict[str, dict[str, Any]]:
    definitions: dict[str, dict[str, Any]] = {}
    for value in [*walk_objects(root), *walk_components(root)]:
        guid = value.get("__guid")
        if isinstance(guid, str):
            if guid in definitions:
                raise ValueError(f"duplicate definition GUID: {guid}")
            definitions[guid] = value
    return definitions


def guid_strings(value: Any) -> set[str]:
    found: set[str] = set()
    if isinstance(value, dict):
        for item in value.values():
            found.update(guid_strings(item))
    elif isinstance(value, list):
        for item in value:
            found.update(guid_strings(item))
    elif isinstance(value, str) and GUID_RE.match(value):
        found.add(value)
    return found


def apply_property_overrides(
    root: dict[str, Any], patch: dict[str, Any], mapping: dict[str, str]
) -> None:
    definitions = index_definitions(root)
    for override in patch.get("PropertyOverrides", []) or []:
        target = override.get("Target", {})
        source_guid = target.get("IdValue")
        mapped_guid = mapping.get(source_guid, source_guid)
        destination = definitions.get(mapped_guid)
        if destination is None:
            raise ValueError(
                f"property override target is absent after remap: {source_guid}"
            )
        destination[override["Property"]] = copy.deepcopy(override.get("Value"))


def insert_added_objects(
    root: dict[str, Any], patch: dict[str, Any], mapping: dict[str, str]
) -> None:
    definitions = index_definitions(root)
    for added in patch.get("AddedObjects", []) or []:
        data = copy.deepcopy(added["Data"])
        parent_source_guid = added["Parent"]["IdValue"]
        parent_guid = mapping.get(parent_source_guid, parent_source_guid)
        parent = definitions.get(parent_guid)
        if parent is None:
            raise ValueError(
                f"added-object parent is absent after remap: {parent_source_guid}"
            )

        children = parent.setdefault("Children", [])
        previous = added.get("PreviousElement")
        previous_source_guid = previous.get("IdValue") if previous else None
        previous_guid = mapping.get(previous_source_guid, previous_source_guid)
        if previous_guid:
            previous_index = next(
                (
                    index
                    for index, child in enumerate(children)
                    if child.get("__guid") == previous_guid
                ),
                None,
            )
            if previous_index is not None:
                children.insert(previous_index + 1, data)
            else:
                children.append(data)
        else:
            children.insert(0, data)
        definitions = index_definitions(root)


def validate_materialized(root: dict[str, Any]) -> dict[str, Any]:
    definitions = index_definitions(root)
    view_models = [
        component
        for component in root.get("Components", []) or []
        if component.get("__type") == "Dxura.RP.Game.ViewModel"
    ]
    if len(view_models) != 1:
        raise ValueError(
            f"materialized root has {len(view_models)} ViewModel components; expected 1"
        )

    if any(key in root for key in ("__Prefab", "__PrefabInstancePatch", "__PrefabIdToInstanceId")):
        raise ValueError("materialized root still contains prefab-instance metadata")

    weapon_renderers = [
        component
        for component in root.get("Components", []) or []
        if component.get("__type") == "Sandbox.SkinnedModelRenderer"
        and component.get("Model") == "models/weapons/sbox_pistol_usp/v_usp.vmdl"
    ]
    if len(weapon_renderers) != 1:
        raise ValueError(
            f"materialized root has {len(weapon_renderers)} USP driver renderers; expected 1"
        )
    if weapon_renderers[0].get("MaterialOverride") != EXPECTED_INVISIBLE_MATERIAL:
        raise ValueError("USP driver renderer does not use the M1911 invisible material")

    part_rows: list[dict[str, str]] = []
    for name, expected in EXPECTED_PARTS.items():
        node = definitions.get(expected["guid"])
        if node is None or node.get("Name") != name:
            raise ValueError(f"missing preserved overlay node: {name}")
        if (
            node.get("Position") != EXPECTED_POSITION
            or node.get("Rotation") != "0,0,0,1"
            or node.get("Scale") != "1,1,1"
        ):
            raise ValueError(f"{name} alignment transform changed")
        renderers = [
            component
            for component in node.get("Components", []) or []
            if component.get("__type") == "Sandbox.ModelRenderer"
            and component.get("Model") == expected["model"]
        ]
        if len(renderers) != 1:
            raise ValueError(f"{name} model renderer mapping changed")

        expected_parent_guid = expected["parent_instance_guid"]
        parent_guid = next(
            (
                candidate.get("__guid")
                for candidate in walk_objects(root)
                if node in (candidate.get("Children", []) or [])
            ),
            None,
        )
        if parent_guid != expected_parent_guid:
            raise ValueError(
                f"{name} parent is {parent_guid}; expected {expected_parent_guid}"
            )
        part_rows.append(
            {"name": name, "guid": expected["guid"], "parent": parent_guid}
        )

    defined_guids = set(definitions)
    all_guids = guid_strings(root)
    dangling_guids = sorted(
        guid
        for guid in all_guids
        if guid not in defined_guids
        and guid not in {
            # Event/asset GUID values are not local object references. None are
            # expected today; keep the exception list explicit if one appears.
        }
    )
    # Prefabs legitimately contain non-object GUID values in serialized events.
    # Report them for custody but do not reject them as dangling without type
    # metadata proving they are object/component references.

    return {
        "rootGuid": root.get("__guid"),
        "rootName": root.get("Name"),
        "viewModelComponents": len(view_models),
        "definedGuids": len(defined_guids),
        "guidValuesWithoutLocalDefinition": dangling_guids,
        "parts": part_rows,
    }


def validate_document(document: dict[str, Any]) -> dict[str, Any]:
    """Validate both the materialized root and the prefab resource envelope."""

    if list(document) != EXPECTED_DOCUMENT_KEYS:
        raise ValueError(
            "M1911 materialized document keys changed: "
            f"{list(document)}; expected {EXPECTED_DOCUMENT_KEYS}"
        )

    envelope = {
        key: document.get(key)
        for key in EXPECTED_DOCUMENT_KEYS
        if key != "RootObject"
    }
    if envelope != EXPECTED_DOCUMENT_ENVELOPE:
        raise ValueError(
            "M1911 materialized document envelope changed: "
            f"{envelope}; expected {EXPECTED_DOCUMENT_ENVELOPE}"
        )

    summary = validate_materialized(document["RootObject"])
    summary["documentEnvelopePreserved"] = True
    summary["externalPackageReferences"] = list(document["__references"])
    return summary


def materialize(donor_path: Path, patch_path: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    donor_document = read_json(donor_path)
    patch_document = read_json(patch_path)
    patch_root = patch_document["RootObject"]
    instance_patch = patch_root.get("__PrefabInstancePatch")
    mapping = patch_root.get("__PrefabIdToInstanceId")
    if not isinstance(instance_patch, dict) or not isinstance(mapping, dict):
        raise ValueError("M1911 source is not the expected prefab-instance patch")

    donor_root = donor_document["RootObject"]
    donor_guid_values = guid_strings(donor_root)
    unmapped = sorted(guid for guid in donor_guid_values if guid not in mapping)
    if unmapped:
        raise ValueError(
            "M1911 instance map does not cover donor GUIDs: " + ", ".join(unmapped)
        )

    root = remap_strings(copy.deepcopy(donor_root), mapping)
    root["Name"] = "vm_m1911"
    apply_property_overrides(root, instance_patch, mapping)
    insert_added_objects(root, instance_patch, mapping)

    document = {"RootObject": root}
    for key, value in patch_document.items():
        if key != "RootObject":
            document[key] = copy.deepcopy(value)
    for expected in EXPECTED_PARTS.values():
        mapped_parent = mapping.get(expected["parent_source_guid"])
        if mapped_parent != expected["parent_instance_guid"]:
            raise ValueError(
                "M1911 parent GUID mapping changed: "
                f"{expected['parent_source_guid']} -> {mapped_parent}; "
                f"expected {expected['parent_instance_guid']}"
            )

    summary = validate_document(document)
    summary.update(
        {
            "donor": str(donor_path),
            "patch": str(patch_path),
            "mappedDonorGuids": len(mapping),
        }
    )
    return document, summary


def serialize_document(document: dict[str, Any]) -> bytes:
    text = json.dumps(document, indent=2, ensure_ascii=False) + "\n"
    return text.encode("utf-8")


def write_bytes_atomic(path: Path, data: bytes, staging_dir: Path) -> None:
    path = pipeline_io.assert_temp_destination(path)
    staging_probe = pipeline_io.assert_temp_destination(
        staging_dir / f".{path.name}.staging-guard"
    )
    staging_dir = staging_probe.parent
    if staging_dir != path.parent:
        raise ValueError("M1911 atomic staging directory must match the output parent")
    pipeline_io.write_bytes_atomic(path, data)


def build_document(
    donor_path: Path, patch_path: Path
) -> tuple[dict[str, Any], dict[str, Any]]:
    donor_sha = sha256_file(donor_path)
    if donor_sha != EXPECTED_DONOR_SHA256:
        raise ValueError(
            f"M1911 USP donor SHA changed: {donor_sha}; "
            f"expected {EXPECTED_DONOR_SHA256}"
        )

    patch_sha = sha256_file(patch_path)
    patch_document = read_json(patch_path)
    patch_root = patch_document["RootObject"]
    if isinstance(patch_root.get("__PrefabInstancePatch"), dict):
        if patch_sha != EXPECTED_SOURCE_PATCH_SHA256:
            raise ValueError(
                f"M1911 source patch SHA changed: {patch_sha}; "
                f"expected {EXPECTED_SOURCE_PATCH_SHA256}"
            )
        document, summary = materialize(donor_path, patch_path)
        summary["sourceMode"] = "prefab-instance-patch"
    else:
        document = copy.deepcopy(patch_document)
        summary = validate_document(document)
        summary.update(
            {
                "donor": str(donor_path),
                "patch": str(patch_path),
                "mappedDonorGuids": None,
                "sourceMode": "already-materialized",
            }
        )

    summary["donorSha256"] = donor_sha
    summary["sourceSha256"] = patch_sha
    return document, summary


def is_within(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent)
        return True
    except ValueError:
        return False


def assert_temp_output_path(output: Path) -> Path:
    resolved = output.resolve(strict=False)
    if is_within(resolved, WORKBENCH_ROOT):
        raise ValueError(
            "TEMP-only M1911 builder refuses every repository output: "
            + str(resolved)
        )
    if not is_within(resolved, SYSTEM_TEMP_ROOT):
        raise ValueError(
            "TEMP-only M1911 builder requires output beneath the system temp "
            "directory: "
            + str(resolved)
        )
    if resolved.exists():
        raise ValueError("TEMP-only M1911 output must not already exist: " + str(resolved))
    if resolved.parent.exists() and any(resolved.parent.iterdir()):
        raise ValueError(
            "TEMP-only M1911 output directory must be empty: " + str(resolved.parent)
        )
    return resolved


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--donor", type=Path, default=DEFAULT_DONOR)
    parser.add_argument("--patch", type=Path, default=DEFAULT_PATCH)
    parser.add_argument("--output", type=Path, required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)

    output = assert_temp_output_path(args.output)

    donor = args.donor.resolve()
    patch = args.patch.resolve()
    output_sha_before = sha256_file(output) if output.exists() else None
    document, summary = build_document(donor, patch)
    data = serialize_document(document)
    materialized_sha = sha256_bytes(data)
    if materialized_sha != EXPECTED_MATERIALIZED_SHA256:
        raise ValueError(
            "M1911 materialized candidate SHA changed: "
            f"{materialized_sha}; expected {EXPECTED_MATERIALIZED_SHA256}"
        )
    staged_document = json.loads(data.decode("utf-8"))
    validate_document(staged_document)

    if output_sha_before is not None:
        raise ValueError("TEMP-only M1911 output appeared during generation")
    write_bytes_atomic(output, data, output.parent)
    written_document = read_json(output)
    validate_document(written_document)
    if output.read_bytes() != data:
        raise ValueError("M1911 output bytes changed after atomic replacement")

    summary["output"] = str(output)
    summary["outputBytes"] = output.stat().st_size
    summary["outputSha256"] = sha256_file(output)
    summary["gameTreeWritten"] = False
    summary["proofCeiling"] = (
        "TEMP structural candidate only; editor compile, runtime equip, animation, "
        "hand fit, ADS, reload, audio, and Portal delivery are unverified"
    )
    print(json.dumps({"result": "PASS", **summary}, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
