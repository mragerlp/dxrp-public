# AK47 Model Build

This folder is the model build area for the LifePunch AK47 world model.

## Active Source

Cleaned FBX:

```text
source/ak47.fbx
```

The cleaned FBX is the only active model source in the LifePunch repo. Raw/original exports stay in the local reference mirror.

## Texture Inputs

```text
textures/ak47_BaseColor.tga.png
textures/ak47_Normal.tga.png
textures/ak47_Metalness.tga.png
textures/ak47_Roughness.tga.png
```

## Target Outputs

Model resource:

```text
w_ak47.vmdl
```

Material resource:

```text
materials/ak47_body.vmat
```

## Current Status

- `w_ak47.vmdl` has been created by S&box.
- The model references `source/ak47.fbx`.
- `materials/ak47_body.vmat` has been created from the PBR texture set.
- `w_ak47.vmdl` is assigned to `materials/ak47_body.vmat`.
- `equipment/w_ak47/w_ak47.prefab` has been created with `Sandbox.SkinnedModelRenderer`.
- `equipment/w_ak47/w_ak47.prefab` now has first-pass DXRP `Equipment`, `TagBinder`, `Muzzle`, `EjectionPort`, `Functions`, ammo, shoot, reload, and recoil component wiring.
- `equipment/w_ak47/w_ak47.prefab` was reopened in S&box after wiring and saved with adjusted `Muzzle` and `EjectionPort` positions.
- `equipment/vm_ak47/vm_ak47.prefab` has been created with `Sandbox.SkinnedModelRenderer` using the same model as a first pass.
- `equipment/vm_ak47/vm_ak47.prefab` now has first-pass DXRP `ViewModel`, `Muzzle`, and `EjectionPort` wiring.
- Material references should stay mounted/relative, not absolute local filesystem paths.

## Build Notes

- Create the S&box model resource from `source/ak47.fbx`.
- Create the material from the imported PBR texture channels.
- Keep the world model mounted path stable: `addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl`.
- Do not add raw Blender exports, loose bullet variants, or old model names here.
- Do not mark this model ready until S&box opens/compiles the resource cleanly.

## Viewmodel (required before next publish)

First-person work is **not** part of this world-model doc. Follow:

```text
../v_ak47/VIEWMODEL_BUILD.md
```

Do not publish another DXRP revision until `v_ak47.vmdl` exists and `vm_ak47.prefab` is rebuilt in S&box to match the M4A1 viewmodel pattern.

## Shipment height (crate display)

See also [`lifepunchaddons/docs/M4A1_REFERENCE.md`](../../../../../../../docs/M4A1_REFERENCE.md) (M4A1 shipment vs first-person split).

Shipments spawn `worldModelPath` directly (`w_ak47.vmdl`), not the world prefab child offsets.

- Tune **`import_translation` Z** on the `ak47.fbx` node in `w_ak47.vmdl` until the rifle lines up with official M4A1 shipment height in a gun-dealer crate test.
- Current repo value: **`[0, 0, 8]`** (raise AK in crate; was sitting low vs M4A1).
- Do not rely on `w_ak47.prefab` → `Model` child position for shipment height.

## World model verification

1. Open `w_ak47.vmdl` in S&box and confirm it compiles cleanly.
2. Compare AK-47 vs M4A1 shipment crates on the dev server (same crate type).
3. Publish only after the viewmodel checklist in `VIEWMODEL_BUILD.md` is complete.
