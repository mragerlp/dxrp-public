from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import tempfile
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LIVE_PREFAB = (
    REPO_ROOT
    / "game/Assets/addons/lifepunch/lpweapons/glock/equipment/vm_glock/vm_glock.prefab"
)
DEFAULT_GENERATED_PREFAB = (
    REPO_ROOT
    / "game/Assets/addons/lifepunch/lpweapons/glock/generated/custom_pistol_9mm/"
    "v_custom_pistol_9mm.prefab"
)
GENERATED_MODEL = (
    "addons/lifepunch/lpweapons/glock/generated/custom_pistol_9mm/"
    "custom_pistol_9mm_vm.vmdl"
)
ARMS_MODEL = "models/first_person/v_first_person_arms_human.vmdl"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def components_of_type(node: dict[str, Any], type_name: str) -> list[dict[str, Any]]:
    return [
        component
        for component in node.get("Components", [])
        if component.get("__type") == type_name
    ]


def direct_child(node: dict[str, Any], name: str) -> dict[str, Any]:
    matches = [child for child in node.get("Children", []) if child.get("Name") == name]
    if len(matches) != 1:
        raise ValueError(f"Expected exactly one direct child named {name}; found {len(matches)}")
    return matches[0]


def component_reference(game_object: dict[str, Any], component: dict[str, Any]) -> dict[str, str]:
    return {
        "_type": "component",
        "component_id": component["__guid"],
        "go": game_object["__guid"],
        "component_type": "SkinnedModelRenderer",
    }


def game_object_reference(game_object: dict[str, Any]) -> dict[str, str]:
    return {"_type": "gameobject", "go": game_object["__guid"]}


def set_first_person_render_layer(renderer: dict[str, Any]) -> None:
    renderer["RenderType"] = "On"
    renderer["UseAnimGraph"] = True
    options = renderer.setdefault("RenderOptions", {})
    options["GameLayer"] = False
    options["OverlayLayer"] = True
    options.setdefault("BloomLayer", False)
    options.setdefault("AfterUILayer", False)


def validate_generated_manifest(generated_prefab: Path) -> None:
    manifest_path = generated_prefab.parent / "weaponanim.manifest.json"
    manifest = load_json(manifest_path)
    relative_path = generated_prefab.relative_to(manifest_path.parent).as_posix()
    matches = [entry for entry in manifest.get("Files", []) if entry.get("RelativePath") == relative_path]
    if len(matches) != 1:
        raise ValueError(f"Manifest does not own exactly one {relative_path} entry")
    if matches[0].get("Sha256", "").lower() != sha256(generated_prefab):
        raise ValueError("Generated prefab hash does not match weaponanim.manifest.json")


def build_adapter(live: dict[str, Any], generated: dict[str, Any]) -> dict[str, Any]:
    live_root = live["RootObject"]
    generated_root = generated["RootObject"]

    view_models = components_of_type(live_root, "Dxura.RP.Game.ViewModel")
    if len(view_models) != 1:
        raise ValueError(f"Expected one live ViewModel component; found {len(view_models)}")

    generated_renderers = [
        component
        for component in components_of_type(generated_root, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == GENERATED_MODEL
    ]
    if len(generated_renderers) != 1:
        raise ValueError(
            f"Expected one generated Glock renderer; found {len(generated_renderers)}"
        )

    arms = copy.deepcopy(direct_child(generated_root, "v_first_person_arms_human"))
    arms_renderers = [
        component
        for component in components_of_type(arms, "Sandbox.SkinnedModelRenderer")
        if component.get("Model") == ARMS_MODEL
    ]
    if len(arms_renderers) != 1:
        raise ValueError(f"Expected one generated arms renderer; found {len(arms_renderers)}")

    camera = copy.deepcopy(direct_child(generated_root, "camera"))
    muzzle = copy.deepcopy(direct_child(generated_root, "muzzle"))
    eject = copy.deepcopy(direct_child(generated_root, "eject"))
    generated_renderer = copy.deepcopy(generated_renderers[0])
    arms_renderer = arms_renderers[0]

    set_first_person_render_layer(generated_renderer)
    set_first_person_render_layer(arms_renderer)

    root = copy.deepcopy(live_root)
    view_model = copy.deepcopy(view_models[0])
    root["Components"] = [view_model, generated_renderer]
    root["Children"] = [arms, camera, muzzle, eject]

    arms_renderer["BoneMergeTarget"] = component_reference(root, generated_renderer)
    view_model["ModelRenderer"] = component_reference(root, generated_renderer)
    view_model["Arms"] = component_reference(arms, arms_renderer)
    view_model["Muzzle"] = game_object_reference(muzzle)
    view_model["EjectionPort"] = game_object_reference(eject)
    view_model["AdditionalRendererRoot"] = None

    result = copy.deepcopy(live)
    result["RootObject"] = root
    result["__references"] = copy.deepcopy(generated.get("__references", []))
    return result


def write_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        temporary_path.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build the live Glock ViewModel shell from Weapon Animator output."
    )
    parser.add_argument("--live", type=Path, default=DEFAULT_LIVE_PREFAB)
    parser.add_argument("--generated", type=Path, default=DEFAULT_GENERATED_PREFAB)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--expected-live-sha256", required=True)
    parser.add_argument("--expected-generated-sha256", required=True)
    args = parser.parse_args()

    live_path = args.live.resolve()
    generated_path = args.generated.resolve()
    output_path = args.output.resolve()
    expected_live_hash = args.expected_live_sha256.lower()
    expected_generated_hash = args.expected_generated_sha256.lower()

    actual_live_hash = sha256(live_path)
    actual_generated_hash = sha256(generated_path)
    if actual_live_hash != expected_live_hash:
        raise ValueError(
            f"Live Glock prefab pin mismatch: {actual_live_hash} != {expected_live_hash}"
        )
    if actual_generated_hash != expected_generated_hash:
        raise ValueError(
            "Generated Glock prefab pin mismatch: "
            f"{actual_generated_hash} != {expected_generated_hash}"
        )

    validate_generated_manifest(generated_path)
    result = build_adapter(load_json(live_path), load_json(generated_path))
    serialized = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    write_atomic(output_path, serialized)

    print(
        json.dumps(
            {
                "result": "PASS",
                "liveSha256": actual_live_hash,
                "generatedSha256": actual_generated_hash,
                "output": str(output_path),
                "outputSha256": sha256(output_path),
            },
            separators=(",", ":"),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
