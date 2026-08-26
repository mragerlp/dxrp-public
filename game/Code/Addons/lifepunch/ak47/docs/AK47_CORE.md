# AK47 Core Build Notes

The AK47 starts from the LifePunch foundation, not the old scratch package.

## Package Contract

- Addon package: `lifepunch.ak47`
- Manifest ident: `ak47`
- Archetype: `weapon`
- DXRP content type: `1`
- Grouping: `Secondary`

## Planned References

```text
primaryReference: addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab
secondaryReference: addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab
worldModelPath: addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl
```

## Core Code

Initial LifePunch-owned code:

```text
Code/Addons/lifepunch/ak47/AK47.cs
```

This file intentionally starts as a neutral definition for identity, mounted paths, and baseline weapon stats. Add DXRP-specific inheritance only after the official weapon base API is confirmed.

Runtime pattern notes:

```text
Code/Addons/lifepunch/ak47/docs/RUNTIME_PATTERN.md
```

## Source Intake

Step 1 source review lives in:

```text
Code/Addons/lifepunch/ak47/docs/SOURCE_INTAKE.md
```

The scratch S&box project was reviewed as a template/reference only. The current AK47 reference source is an owner-provided local folder outside this repository. Approved source files have been copied into the clean LifePunch addon lane.

Active cleaned model source:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/source/ak47.fbx
```

Model/material scaffold:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/MODEL_BUILD.md
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/material-map.json
```

Model attribution is tracked in:

```text
Assets/addons/lifepunch/ak47/ATTRIBUTION.md
```

## Build Order

1. Use the public DXRP addon list and Kevlar addon page as the published-page reference.
2. Confirm the official DXRP/M4A1 weapon code and prefab shape.
3. Create S&box model/material resources from the imported FBX and textures using the model scaffold.
   - `w_ak47.vmdl` created.
   - `materials/ak47_body.vmat` created and assigned.
4. Create `w_ak47.prefab` and `vm_ak47.prefab` from the confirmed DXRP weapon prefab pattern.
   - `w_ak47.prefab` created with `Sandbox.SkinnedModelRenderer`.
   - `vm_ak47.prefab` created with `Sandbox.SkinnedModelRenderer`.
   - `vm_ak47.prefab` uses `w_ak47.vmdl` as a first pass until the DXRP viewmodel pattern is confirmed.
5. Add AK47 runtime code using the same weapon base pattern.
   - Local S&box API metadata confirms `BaseWeapon` and `BaseBulletWeapon` exist.
   - `AK47Weapon.cs` now exists as a compile-safe LifePunch runtime contract.
   - Official DXRP/M4A1 wiring and package compile references still need confirmation before inheriting runtime code is finalized.
   - Public `Simple Weapon Base` examples are useful for modular concepts only, not names or implementation ownership.
6. Validate the addon lane.
7. Generate publish staging for review.
8. Attach the addon only to the Development gamemode/server first.

## Published Addon Reference

Official/public DXRP addon list:

```text
https://dxrp.net/addons
```

Kevlar example page:

```text
https://dxrp.net/addons/019e4013-08a5-77d0-975c-df132345045e
```

Observed page structure to mirror for AK47:

- Public addon detail page with title, publisher, media, metadata, and `Add to Server` action.
- Metadata showing content item count, servers using the addon, published time, updated time, package identifier, and revision/source links.
- Tabs for `About`, `Contents`, and `Code Explorer`.
- Code Explorer separates addon behavior files, such as `KevlarEntity.cs` and `KevlarService.cs`.

Use this page as the publish/presentation reference only. AK47 runtime behavior should still follow the official DXRP weapon/M4A1 pattern.

## Naming Boundary

The AK47 addon is LifePunch-owned.

- Use `lifepunch` package identity.
- Use `LifePunch.DXRP.Addons.AK47` namespace for LifePunch code.
- Do not copy `SWB`, `SWE`, `BeCreativeRP`, or other public-addon branding from reference pages.
- Treat public code examples as patterns to study, not identifiers to reuse.

## Safety Rules

- No old scratch code or assets without review.
- No server sync until local validation passes.
- No gamemode row changes until the addon package references are stable.
