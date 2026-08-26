# AK47 Assets

**Fix first person / shipment:** [`../../../../docs/AK47_FIX_CHECKLIST.md`](../../../../docs/AK47_FIX_CHECKLIST.md)

This is the clean asset lane for the LifePunch AK47 addon package.

Package:

```text
lifepunch.ak47
```

Planned DXRP content references:

```text
addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab
addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab
addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl
```

Core rule:

- Do not copy old scratch files here without review.
- Keep world and viewmodel prefabs separated.
- Keep model source/output paths under `models/lifepunch/ak47/`.
- The gamemode should reference mounted paths without the leading `Assets/`.

Imported source assets:

- FBX model sources live under `models/lifepunch/ak47/w_ak47/source/`.
- Active ready FBX: `addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/source/ak47.fbx`.
- Original unclean FBX files are not kept in the LifePunch addon lane.
- PBR texture source lives under `models/lifepunch/ak47/w_ak47/textures/`.
- Raw WAV sound sources live under `sounds/source/`.
- Normalized first-pass sound resources live under `sounds/`.

Model attribution is tracked in `ATTRIBUTION.md`. Preserve attribution before publishing this addon publicly.

Deferred resources:

- `w_ak47.vmdl` has been created from the imported FBX/textures.
- `w_ak47.prefab` has been created with a `Sandbox.SkinnedModelRenderer`.
- `vm_ak47.prefab` has been created with a `Sandbox.SkinnedModelRenderer`.
- `vm_ak47.prefab` currently uses the same `w_ak47.vmdl` model as a first pass until a DXRP-specific viewmodel pattern is confirmed.
- DXRP-specific weapon components still need confirmation before runtime integration.

Model/material build scaffold:

```text
models/lifepunch/ak47/w_ak47/MODEL_BUILD.md
models/lifepunch/ak47/w_ak47/material-map.json
```
