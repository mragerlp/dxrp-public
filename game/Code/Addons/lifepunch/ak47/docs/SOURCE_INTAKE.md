# AK47 Source Intake

This is Step 1 for rebuilding AK47 from the core LifePunch addon structure.

Do not copy old scratch files directly. Use this record to decide what is safe to import and where it should go.

## Source Paths Reviewed

Scratch project:

```text
<local downloads>/ak-classic/ak47
```

Model ZIPs:

```text
<local downloads>/ak47 (1).zip
<local downloads>/ak47.zip
<local downloads>/rifle-ak-47-weapon-model-cs2.zip
```

Sound ZIP:

```text
<local downloads>/ak47_weapon_sounds.zip
```

Current AK47 local reference folder:

```text
<local reference>/addons/weapons/ak47
```

Current AK47 foundation source folder:

```text
<local reference>/addons/weapons/ak47
```

This is the current local reference source of truth for AK47 testing files unless replaced by a newer owner-provided folder.

Note: `<local downloads>/ak47 (1)` was not an extracted folder during intake. The matching item was `ak47 (1).zip`.

## Scratch Project Finding

The extracted scratch project appears to be a starter S&box addon template, not a weapon implementation.

Observed files:

- `Code\MyEntity.cs`
- `Assets\entity\my_entity.prefab`
- `ak47.sbproj`
- `Code\ak47.csproj`
- `.sbox` and `.vscode` project/editor metadata

Decision:

- `Code\MyEntity.cs`: `reject` for AK47 runtime code. It is a default moving entity component.
- `Assets\entity\my_entity.prefab`: `reject` for AK47. It references a watermelon model and default entity components.
- `.sbox`, `.vscode`, project files, localization, cloud/editor state: `reference-only` or `reject`. Do not import into the LifePunch addon lane.

## Model Sources

Model source page:

```text
https://sketchfab.com/3d-models/ak47-831519a097d84e079fd8bc4b15e5b57d
```

Observed Sketchfab metadata:

- Title: `Ak47`
- Creator: `wburton` / `@wburton95`
- License: `CC Attribution`
- Published: `9 years ago`
- Description: `A game ready ak47 model. Fully textured and animated for an fps game.`
- Triangles: `3.4k`
- Vertices: `1.8k`

Attribution requirement:

- Preserve creator/license attribution in LifePunch docs and release notes before public release.
- Do not publish outside LifePunch/DXRP testing until attribution text is included wherever DXRP addon publication expects credits.
- The planned model cleanup in Blender should remove loose bullets and the extra magazine while preserving the usable AK47 mesh, textures, and animation data.

The local AK47 reference folder contains:

```text
<local reference>/addons/weapons/ak47/source/raw/ak47-animated.fbx
<local reference>/addons/weapons/ak47/source/cleaned/ak47.fbx
<local reference>/addons/weapons/ak47/textures/ak47_BaseColor.tga.png
<local reference>/addons/weapons/ak47/textures/ak47_Normal.tga.png
<local reference>/addons/weapons/ak47/textures/ak47_Metalness.tga.png
<local reference>/addons/weapons/ak47/textures/ak47_Roughness.tga.png
```

Decision:

- `ak47.fbx`: active cleaned Blender export for the LifePunch AK47.
- `ak47-animated.fbx`: original source/provenance file outside the repo, superseded by the cleaned export.
- Import status: `imported-to-lifepunch-addon`.
- Cleanup completed: loose bullets, extra magazine, cube, and loose shard pieces removed.
- Keep only cleaned, ready model source files under the LifePunch addon lane before creating `w_ak47.vmdl`.

Imported model sources:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/source/ak47.fbx
```

Active model source:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/source/ak47.fbx
```

`ak47 (1).zip` and `ak47.zip` appear to contain the same source set:

```text
source/ak47-animated.fbx
textures/ak47_BaseColor.tga.png
textures/ak47_Normal.tga.png
textures/ak47_Metalness.tga.png
textures/ak47_Roughness.tga.png
```

Decision:

- `reference-only`: superseded by the current owner-provided reference folder.
- Use only one of the duplicate ZIPs.

`rifle-ak-47-weapon-model-cs2.zip` contains:

```text
source/AK47.glb
textures/ak47_default_color_psd_5b66a23b_2.png
textures/ak47_default_normal_png_c8c5793e_0.png
textures/ak47_default_ao_png_b00f6e2e_orm_1322888879_1@channels=G.png
textures/ak47_default_ao_png_b00f6e2e_orm_1322888879_1@channels=B.png
textures/ak47_default_ao_png_b00f6e2e_orm_1322888879_1@channels=R.png
```

Decision:

- `pending-review`: alternate model candidate. Review source/permission and size before importing.

## Sound Sources

The local AK47 reference folder contains raw WAV sources:

```text
<local reference>/addons/weapons/ak47/sounds/raw/ak47-1.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47-1-distant.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_01.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_boltpull.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_clipin.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_clipout.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_distant.wav
<local reference>/addons/weapons/ak47/sounds/raw/ak47_draw.wav
```

Observed mapping candidates:

- `ak47-1.wav`, `ak47_01.wav`: close shot candidates.
- `ak47-1-distant.wav`, `ak47_distant.wav`: distant shot candidates.
- `ak47_boltpull.wav`: bolt/cock candidate.
- `ak47_clipout.wav`, `ak47_clipin.wav`, `ak47_boltpull.wav`: reload sequence candidates.
- `ak47_draw.wav`: draw/equip candidate.

Decision:

- `use`: current raw audio source candidates for the LifePunch AK47.
- Import status: `imported-to-lifepunch-addon`.
- First pass `.sound` resources created for confirmed events.
- Do not commit `desktop.ini`.

Imported raw sound sources:

```text
Assets/addons/lifepunch/ak47/sounds/source/ak47-1.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47-1-distant.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_01.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_boltpull.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_clipin.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_clipout.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_distant.wav
Assets/addons/lifepunch/ak47/sounds/source/ak47_draw.wav
```

Normalized sound source copies:

```text
Assets/addons/lifepunch/ak47/sounds/ak47_shot.wav
Assets/addons/lifepunch/ak47/sounds/ak47_shot_distant.wav
Assets/addons/lifepunch/ak47/sounds/ak47_reload_clipout.wav
Assets/addons/lifepunch/ak47/sounds/ak47_reload_clipin.wav
Assets/addons/lifepunch/ak47/sounds/ak47_cock.wav
Assets/addons/lifepunch/ak47/sounds/ak47_draw.wav
```

Created sound resources:

```text
Assets/addons/lifepunch/ak47/sounds/ak47_shot.sound
Assets/addons/lifepunch/ak47/sounds/ak47_shot_distant.sound
Assets/addons/lifepunch/ak47/sounds/ak47_reload_clipout.sound
Assets/addons/lifepunch/ak47/sounds/ak47_reload_clipin.sound
Assets/addons/lifepunch/ak47/sounds/ak47_cock.sound
Assets/addons/lifepunch/ak47/sounds/ak47_draw.sound
```

Deferred sound decisions:

- `ak47_01.wav` and `ak47_distant.wav` remain imported as raw source candidates.
- Do not create silent-fire or last-shot sound resources until the source mapping is confirmed.

`ak47_weapon_sounds.zip` contains:

```text
ak47_single_round_fired.wav
ak47_final_round_fired.wav
ak47_magazine_reload.wav
ak47_gun_cocked.wav
```

Decision:

- `reference-only`: superseded by the current owner-provided reference folder.
- If the WAV source ZIP is used later, normalize the source into the same event names:

```text
ak47_shot
ak47_lastshot
ak47_reload
ak47_cock
ak47_draw
```

## Clean LifePunch Targets

Model source target:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/source/
```

Texture source target:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/textures/
```

Compiled/model resource target:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl
```

Model/material scaffold:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/MODEL_BUILD.md
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/material-map.json
```

World prefab target:

```text
Assets/addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab
```

Viewmodel prefab target:

```text
Assets/addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab
```

Sound target:

```text
Assets/addons/lifepunch/ak47/sounds/
```

Planned mounted sound references:

```text
addons/lifepunch/ak47/sounds/ak47_shot.sound
addons/lifepunch/ak47/sounds/ak47_shot_distant.sound
addons/lifepunch/ak47/sounds/ak47_reload_clipout.sound
addons/lifepunch/ak47/sounds/ak47_reload_clipin.sound
addons/lifepunch/ak47/sounds/ak47_cock.sound
addons/lifepunch/ak47/sounds/ak47_draw.sound
```

Code target:

```text
Code/Addons/lifepunch/ak47/AK47.cs
```

## CS2 reference intake (study only)

- Mesh: `weapon_rif_ak47`
- Intake: `C:/lifepunch/reference-intake/cs2-weapons/ak47/`
- Animation catalog: `addons/docs/reference/CS2_AK47_STUDY.md`
- M4 class map: `addons/docs/reference/M4A1_CLASS_ANIM_MAP.md`
- Ship boundary: no CS2 assets in publish tree; FP via M4A1 class kit (`vm_m4a1`)

## Next Step

Create the S&box model resource/materials from the imported FBX and textures using `MODEL_BUILD.md` and `material-map.json`, then create `w_ak47.prefab` and `vm_ak47.prefab` around the confirmed DXRP weapon prefab/component pattern.
