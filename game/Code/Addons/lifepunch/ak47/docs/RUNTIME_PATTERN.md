# AK47 Runtime Pattern

This document tracks the AK47 code/runtime step after both prefabs have been created.

## Current Asset State

Created:

```text
Assets/addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab
Assets/addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/materials/ak47_body.vmat
Code/Addons/lifepunch/ak47/AK47Weapon.cs
```

Blocking note (2026-05-26):

- `vm_ak47.prefab` still uses `w_ak47.vmdl` as a temporary stand-in. DXRP first person requires a dedicated viewmodel with a `camera` bone (see M4A1 `v_m4a1.vmdl` + `vm_m4a1.prefab`).
- Do **not** publish another revision for prefab offset tweaks. Complete `Assets/addons/lifepunch/ak47/models/lifepunch/ak47/v_ak47/VIEWMODEL_BUILD.md` in S&box first.

## Observed Local S&box API

The local S&box editor generated API metadata for these weapon-related types:

- `BaseCarryable`
- `BaseWeapon`
- `BaseBulletWeapon`
- `IronSightsWeapon`
- `AmmoResource`
- `TraceAttackInfo`

Useful observed members:

- `BaseCarryable.ItemPrefab`
- `BaseCarryable.MuzzleGameObject`
- `BaseCarryable.WeaponModel`
- `BaseCarryable.MuzzleTransform`
- `BaseWeapon.UsesAmmo`
- `BaseWeapon.UsesClips`
- `BaseWeapon.ClipMaxSize`
- `BaseWeapon.ClipContents`
- `BaseWeapon.MaxReserveAmmo`
- `BaseWeapon.StartingAmmo`
- `BaseWeapon.ReloadTime`
- `BaseWeapon.DeployTime`
- `BaseWeapon.PrimaryAttack()`
- `BaseWeapon.GetPrimaryFireRate()`
- `BaseBulletWeapon.ShootBullet(...)`
- `BaseBulletWeapon.GetAimConeAmount(...)`

## Observed Official DXRP Weapon Pattern

The source-available `dxura/dxrp` repository exposes the official DXRP equipment and weapon component structure. This is the primary reference for AK47 runtime wiring.

Official source paths reviewed:

```text
game/Code/Equipment/Equipment.cs
game/Code/Equipment/Weapon/WeaponComponent.cs
game/Code/Equipment/Weapon/WeaponInputComponent.cs
game/Code/Equipment/Weapon/ShootWeaponComponent.cs
game/Code/Equipment/Weapon/AmmoComponent.cs
game/Code/Equipment/Weapon/ReloadWeaponComponent.cs
game/Code/Equipment/Weapon/RecoilWeaponComponent.cs
game/Code/Equipment/ViewModel.cs
game/Assets/gameplay/equipment/weapons/m4a1/w_m4a1.prefab
game/Assets/gameplay/equipment/weapons/m4a1/vm_m4a1.prefab
```

Official M4A1 content is published as an equipment content row in the DXRP Base Content addon:

```text
name: #equipment.m4a1.name
type: 1
description: #equipment.m4a1.description
```

The official `w_m4a1.prefab` uses a component-composition model instead of a per-gun class like `M4A1 : BaseBulletWeapon`:

- Root object has `Dxura.RP.Game.Equipment`.
- `Equipment.Resource` points to `gameplay/equipment/weapons/m4a1/m4a1.equip`.
- `Equipment.ViewModelPrefab` / content secondary reference resolves the first-person prefab.
- `Equipment.ModelRenderer`, `Muzzle`, and `EjectionPort` are explicit prefab references.
- A `Functions` child contains weapon behavior components.
- `Dxura.RP.Game.AmmoComponent` stores magazine and reserve ammo.
- `Dxura.RP.Game.ShootWeaponComponent` stores bullet damage, fire rate, fire modes, spread, muzzle flash, dry-fire sound, shoot sound, and ammo component reference.
- `Dxura.RP.Game.ReloadWeaponComponent` stores reload timings, reload input, timed reload sounds, and ammo component reference.
- `Dxura.RP.Game.RecoilWeaponComponent` stores recoil pattern or standard recoil values.
- Other viewmodel effects such as `ManualOffset`, `ViewPunch`, and `FovOffset` are optional tuning helpers.

Official M4A1 tuning observed from `w_m4a1.prefab`:

- `BaseDamage`: `20`
- `FireRate`: `680`
- `Ammo`: `30`
- `MaxAmmo`: `30`
- `ReserveAmmo`: `30`
- `MaxReserveAmmo`: `120`
- `SupportedFireModes`: `Automatic`, `Semi`
- `RequiresAmmoComponent`: `true`
- `ShootSound`: `gameplay/equipment/weapons/m4a1/sounds/m4_shot.sound`
- `DryFireSound`: `sounds/equipment/gun_dryfire.sound`
- `MuzzleFlashPrefab`: `prefabs/weapon_effects/rifle_muzzleflash.prefab`
- `EjectionPrefab`: `prefabs/weapon_effects/556_casing.prefab`
- `ReloadTime`: `1.5`
- `EmptyReloadTime`: `2`
- `HoldType`: `Rifle`
- `SpeedPenalty`: `40`

LifePunch AK47 first-pass official target:

- Keep content type `1`, primary world prefab, secondary viewmodel prefab, and grouping `Primary`.
- Make `w_ak47.prefab` match the official `Equipment` composition pattern once DXRP component types are available in the S&box editor.
- Add `Equipment`, `TagBinder`, `AmmoComponent`, `ShootWeaponComponent`, `ReloadWeaponComponent`, and `RecoilWeaponComponent` through the editor/prefab, not by copying official source into LifePunch.
- Use LifePunch-owned asset paths and package identity only.
- Configure AK47 values from `AK47.cs`: `28` damage, `600` RPM, `30` magazine, `90` reserve, `2.4s` reload, automatic fire.
- Use LifePunch sounds under `addons/lifepunch/ak47/sounds/`.
- Add `Muzzle` and `EjectionPort` child game objects to `w_ak47.prefab` before testing firing effects.

Important correction:

- Do not chase a hidden official `M4A1.cs` class for normal gun behavior. The official public M4A1 is prefab/resource-driven using shared DXRP equipment components.
- The earlier `BaseBulletWeapon` metadata is lower-priority until DXRP shows that addon weapons should inherit it directly. The official M4A1 evidence points to prefab component composition.

## LifePunch Identity Rule

LifePunch code must stay LifePunch-owned.

Do not copy public-addon branding, namespaces, groups, categories, or identifiers such as:

- `SWB`
- `SWE`
- `BeCreativeRP`
- any other public server/community package identity

Public addon pages and Code Explorer files are reference material for patterns only. When implementing AK47 runtime code, use LifePunch-owned names such as:

```text
LifePunch.DXRP.Addons.AK47
lifepunch.ak47
AK-47
```

If a public example uses a group/category marker, translate the idea into a LifePunch-specific equivalent instead of reusing their label.

## Observed Public Weapon Reference

The public `Simple weapons`/weapon addon example appears to use a modular weapon pattern:

- `Weapon.cs` as the central weapon component.
- `BulletInfo` / physical bullet components for projectile behavior.
- `ShootInfo` for shot data.
- Attachments and particles as separate component/resource areas.

This is useful for understanding structure, but it is not LifePunch source and should not be copied directly.

Additional observed public reference sections:

- `Weapon_Shoot.cs`: shooting gate/check logic, including reload state, owner/input checks, ammo checks, dry-fire behavior, and shot timing.
- `Weapon_UI.cs`: first-person screen/crosshair UI setup and teardown.
- `Weapon_Vars.cs`: model/viewmodel fields, display/class/category metadata, slot, hold type, reload timing, clip/ammo values, offsets, recoil/spread/animation settings, and sound references.
- `WeaponRegistry.cs`: runtime registry that collects weapon components and indexes them by class name.
- `WeaponSettings.cs`: component for weapon settings/helpers attached near the scene root.
- `WeaponParticleManager.cs`: scene-level particle/decal tracking and cleanup helper.
- `Weapon_Extras.cs`: burst-fire and trace/damage helper behavior.

Observed `WeaponSettings.cs` details:

- The public example uses a scene-root singleton settings component.
- The settings component exposes synchronized global toggles such as friendly-fire and auto-reload style behavior.
- It protects against duplicate instances and clears the singleton reference on destroy.

LifePunch AK47 first-pass decision:

- Do not copy the public settings component or its package identity.
- Do not add a shared LifePunch weapon settings singleton until more than one LifePunch weapon needs it.
- Keep AK47 defaults local to `AK47.cs` and `AK47Weapon.cs` for now.
- Revisit a shared settings component later for cross-weapon policies such as friendly fire, auto reload, or server-wide debug toggles.
- `Weapon_Getters.cs`: helper methods for effective attachment data and shoot animation/left-hand state.
- `Weapon_Reload.cs`: reload state machine, clip/ammo transfer, reload start/finish/cancel hooks.
- `Weapon_Scoping.cs`: scope state, viewmodel visibility/offset behavior, and scope component handling.
- `Commands.cs`: console command helpers that toggle global weapon settings.

LifePunch takeaway:

- AK47 may need a single LifePunch class first, but the final shape should allow separation of concerns if the code grows.
- Do not reuse `SWB`, `SWE`, or the public example namespace/group names.
- Translate concepts into LifePunch terms only after confirming they are needed for DXRP.
- Keep our first runtime pass narrow: weapon identity, prefabs, ammo/clip settings, fire rate, primary attack, reload timing, and sound hooks.
- Keep reusable framework guidance in `docs/REUSABLE_ADDON_FRAMEWORK.md`; AK47-specific tuning should not become accidental defaults for every future addon.

Observed scene-level/reference helper details:

- `WeaponSettings.cs` is a scene-root/global settings component with a static singleton, host-spawned network object, and synced booleans for holster-style defaults.
- `WeaponRegistry.cs` is a scene-root prefab registry/cache. It exposes configured weapon prefabs, instantiates them under the registry/root, disables the spawned weapon objects, extracts weapon components, indexes them by class/name identifier, and provides a lookup helper for class names.
- `WeaponParticleManager.cs` is a scene-root/global particle and decal cleanup manager. It tracks current particles/decals, exposes host-synced limits for count and lifetime, parents tracked decals under the manager when needed, and destroys/removes oldest entries when limits are exceeded.
- `Weapon_UI.cs` is a client-side UI builder for weapon HUD/crosshair state. It creates screen/root panels for the local player, parents existing UI children under the root panel, clears/rebuilds custom crosshair state when the root changes, and removes its panels during cleanup.
- `Commands.cs` contains console command helpers that check the local player and toggle global weapon setting values such as holster/customization or auto-reload-style behavior. These are convenience/admin/user controls over settings, not core weapon runtime.

LifePunch decision:

- Do not implement global weapon settings, a weapon registry, a particle/decal manager, weapon HUD/crosshair helpers, or console command toggles for the AK47 first pass unless DXRP requires them.
- If DXRP later needs these patterns, translate them into LifePunch-owned gamemode/framework helpers such as `LifePunchWeaponSettings`, `LifePunchWeaponRegistry`, `LifePunchWeaponParticleManager`, or a LifePunch weapon HUD helper.
- If LifePunch later needs command helpers, use LifePunch-owned command names and route them through confirmed DXRP/admin-panel permission policy instead of copying public reference command names.
- Keep AK47 focused on confirmed DXRP base-class requirements, LifePunch-owned identity, prefab references, ammo/fire/reload behavior, and LifePunch sound resources.

Observed `Weapon_Vars.cs` details:

- Core metadata fields include a weapon class name, category, display title, icon, and slot.
- Model fields separate world model and viewmodel references.
- General weapon fields include primary/secondary clip sizes, ammo values, hold type, reload timing, deploy timing, and automatic/semi-auto style flags.
- Positioning fields include viewmodel/main offsets, aim offsets, sprint offsets, customization offsets, and first-person transform values.
- Animation fields include draw, reload, attack, idle, empty, and shell reload animation names/timings.
- Sound fields include draw, reload, and shot-related sound references.
- State fields track reload/scoping/attack timing, active scope state, shot timing, burst state, and recoil/spread status.

LifePunch AK47 should translate these into a smaller first-pass config:

- `DisplayName`: `AK-47`
- `ClassName` / stable code id: LifePunch-owned AK47 identifier.
- `Category`: LifePunch weapon/equipment category, not SWB/SWE.
- `WorldPrefabPath`: `addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab`
- `ViewModelPrefabPath`: `addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab`
- `WorldModelPath`: `addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl`
- Clip/ammo/reload/fire-rate values from `AK47.cs`.
- Fire/distant/reload/cock/draw sounds from LifePunch sound resources.

Do not implement attachments, scope UI, burst fire, alternate ammo, or complex animation offsets until the simple AK47 weapon fires reliably in DXRP.

Observed `Weapon_Shoot.cs` details:

- Defines shared bullet tracer ignore tags such as player/NPC/weapon/sky-style tags.
- Uses a configurable impact particle mapping for surface tags.
- Has a `CanShoot(...)` gate that checks reload state, owner/input state, sprint/interact constraints, ammo/clip state, and shot timing.
- Performs dry-fire behavior when empty.
- Has separate primary and secondary attack entry points that call a shared shoot function with a `ShootInfo`.
- The shoot function creates/configures shot info, decreases ammo when appropriate, uses owner eye transform or muzzle transform, applies recoil/spread, and calls bullet logic.
- It plays shot sounds and can pick delayed/distant variants.
- It creates muzzle flash and other shoot particles/effects.
- It has helpers for straight bullet traces and impact particle/sound effects.

LifePunch AK47 first-pass shooting loop should only borrow the shape:

1. Check not reloading.
2. Check clip has ammo.
3. Enforce fire-rate delay.
4. Spend one round.
5. Play `ak47_shot.sound`.
6. Fire a bullet/trace using the confirmed DXRP base weapon API.
7. Apply simple recoil/spread values from `AK47.cs`.
8. Trigger reload when empty only after the base reload path is confirmed.

Defer:

- Secondary attack.
- Attachments.
- Surface-specific impact particles.
- Distant shot scheduling.
- Scope-specific behavior.
- Complex muzzle particles beyond what DXRP requires.

Observed `Weapon.cs` lifecycle/model details:

- Central `Weapon` component is split across partial files.
- Startup checks for primary/secondary attack input setup.
- Startup initializes cursor/style behavior, owner/player references, attachments, and world position.
- Destroy/disable paths clean up viewmodel renderer, screen panels, attachments, and owner hold type.
- Deploy logic clears state, sets draw timings, plays draw animation/sound, and handles fallback deploy sound.
- Holster/deploy state tracks owner validity, active weapon state, reload/scoping status, and visibility.
- Per-frame logic handles owner changes, attachment changes, scoping transitions, sprint/lower state, attack inputs, reload inputs, and animation transitions.
- World model creation uses a model renderer and disables shadows/transforms for held state.
- Viewmodel creation uses a separate model renderer/camera setup and can hide the world renderer while first-person viewmodel is active.
- Sound playback helper supports local UI/non-positional playback and world-position playback with optional volume, pitch, and distance.

LifePunch AK47 first-pass lifecycle should stay smaller:

- Configure item/world prefab reference.
- Spawn/use world prefab and first-pass viewmodel prefab.
- Set draw/deploy timing.
- Add primary attack and reload state.
- Play LifePunch sound resources.
- Avoid custom UI, registry, attachment handling, scoping, sprint/lower animations, and complex particle systems until the base weapon works in DXRP.

Observed `Weapon_Reload.cs` details:

- Reload is skipped if the weapon is already reloading, attacking, or scoping.
- Reload checks whether the clip is already full.
- Reload checks whether reserve ammo is available.
- Reload start sets `IsReloading`, reload timing, and a shell/reload flag.
- Reload start can play reload animation and reload sound.
- Reload finish transfers ammo from reserve into the clip up to max clip size.
- Reload finish clears reload state and can restart reload logic for shell-by-shell behavior.
- Cancel reload clears reload state and can stop reload animation.
- Shell reload has a separate start/finish/cancel flow and shell-specific sound timing.

LifePunch AK47 first-pass reload loop:

1. Do not start reload while firing.
2. Do not reload when the 30-round magazine is full.
3. Do not reload when reserve ammo is empty.
4. Set a `ReloadSeconds` timer from `AK47.Stats.ReloadSeconds`.
5. Play simple reload sounds from `ak47_reload_clipout.sound`, `ak47_reload_clipin.sound`, and `ak47_cock.sound` if the final runtime API supports timed sound hooks.
6. On finish, transfer ammo from reserve to magazine.
7. Clear reload state.

Defer:

- Shell-by-shell reload.
- Reload animation names and timing curves.
- Reload cancel behavior beyond basic interruption.
- Attachment-specific reload modifiers.

Observed `Weapon_Scoping.cs` details:

- Scope start changes camera/render behavior and may hide the weapon viewmodel.
- Scope start can play a scope-in sound.
- Scope end restores scope counters/state and can play a scope-out sound.
- Scope behavior is guarded by `IsScoping` and reload state.

LifePunch AK47 decision:

- Do not implement scoping for the first AK47 pass.
- Keep AK47 as a simple primary automatic rifle until base fire/reload behavior works in DXRP.

Observed `Weapon_Getters.cs` details:

- Contains helper methods for resolving active attachments and attachment-driven overrides.
- Resolves muzzle/effective attachment data from sight/muzzle attachments or default weapon data.
- Selects shoot animation from reload/shot state and whether primary/secondary fire is active.
- Checks ammo state for primary/secondary ammo.
- Computes shoot delay from fire rate.
- Computes spread from base spread plus movement/ducking/airborne/shooting/scoping-style modifiers.
- Computes recoil angles from pitch/yaw/randomness and movement state.

LifePunch AK47 first-pass decision:

- Use static AK47 spread/recoil values from `AK47.cs`.
- Do not implement attachment-driven spread/recoil overrides yet.
- Do not implement scoped spread modifiers yet.
- Treat movement/airborne/ducking modifiers as later tuning after the basic weapon fires.

Observed `Weapon_Extras.cs` details:

- Contains burst-fire counters/checks.
- Contains a helper for trace distance from camera/eye direction and possible tuck range.
- Contains a `ShootBlank(...)` style helper that can consume/spend ammo without full bullet behavior.

LifePunch AK47 first-pass decision:

- Do not implement burst fire.
- Do not implement tuck-range/distance correction unless basic firing traces are clearly wrong in DXRP.
- Do not add blank-fire behavior beyond a simple dry-fire/no-ammo path if needed.

## Current Runtime Code

`AK47Weapon.cs` now provides a compile-safe LifePunch-owned runtime contract:

- Tracks clip ammo, reserve ammo, reload state, and shot timing.
- Initializes from `AK47.Stats`.
- Exposes first-pass `TrySpendRound()`, `TryStartReload()`, `FinishReload()`, and `CancelReload()` gates.
- Compiles as a `Sandbox.Component` while the DXRP/base weapon class is not available to the local `addons.csproj` compile references.

Attempted direct inheritance from `BaseBulletWeapon` failed in local `dotnet build` because the type is present in mounted package metadata but not available to the C# project as a compile reference.

After reviewing official DXRP public source, `BaseBulletWeapon` is no longer the main target for the first pass. The official M4A1 uses `Equipment` plus child weapon components in the prefab. Keep `AK47Weapon.cs` as a compile-safe LifePunch contract/reference while the prefab is migrated to official DXRP `Equipment` composition in the S&box editor.

## AK47 Runtime Targets

Initial tuning from `AK47.cs`:

- Damage: `28`
- RPM: `600`
- Magazine: `30`
- Reserve ammo: `90`
- Reload: `2.4s`
- Range: `120m`
- Spread: `1.8`
- Recoil pitch: `2.2`
- Recoil yaw: `0.85`

Sound references:

```text
addons/lifepunch/ak47/sounds/ak47_shot.sound
addons/lifepunch/ak47/sounds/ak47_shot_distant.sound
addons/lifepunch/ak47/sounds/ak47_reload_clipout.sound
addons/lifepunch/ak47/sounds/ak47_reload_clipin.sound
addons/lifepunch/ak47/sounds/ak47_cock.sound
addons/lifepunch/ak47/sounds/ak47_draw.sound
```

## Current Prefab Wiring

`w_ak47.prefab` now has the first-pass official DXRP component layout:

- Root `Equipment` and `TagBinder`.
- Root `Equipment.ModelRenderer` points to the AK47 `SkinnedModelRenderer`.
- `Muzzle` and `EjectionPort` child objects are wired to `Equipment`.
- `Functions` child object contains `AmmoComponent`, `ShootWeaponComponent`, `ReloadWeaponComponent`, and `RecoilWeaponComponent`.
- AK47 values are configured from `AK47.cs` where possible.
- The prefab was reopened in S&box after wiring and saved with adjusted `Muzzle` and `EjectionPort` positions.

The `Muzzle` and `EjectionPort` positions are good enough for first-pass testing and can be tuned further after observing muzzle flash, traces, and casing/ejection effects in-game.

`vm_ak47.prefab` now has first-pass DXRP viewmodel wiring:

- Root `ViewModel`.
- `ViewModel.ModelRenderer` points to the AK47 `SkinnedModelRenderer`.
- `Muzzle` and `EjectionPort` child objects are wired to `ViewModel`.
- No root `Equipment` component is present on the viewmodel prefab.

This is still a temporary viewmodel because it uses the same world AK47 model. Replace or tune it later with a proper first-person viewmodel/arms setup.

## Next Required Step

The next AK47 implementation step is to reopen `vm_ak47.prefab` in S&box and verify the first-pass DXRP viewmodel component structure:

1. Confirm the prefab opens without missing-type or broken-reference errors.
2. Confirm `ViewModel.ModelRenderer` is assigned.
3. Confirm `Muzzle` and `EjectionPort` appear as children.
4. Save once from the editor.
5. After viewmodel wiring is stable, test the AK47 content row in a DXRP server/editor context.

Do not publish or attach the addon to a gamemode until the prefab component wiring saves cleanly and the LifePunch validators still pass.
