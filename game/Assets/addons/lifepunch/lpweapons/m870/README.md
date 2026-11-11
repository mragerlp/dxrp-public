# M870 pump shotgun (lpweapons/m870)

A 12 gauge pump-action shotgun for DXRP. The mesh is the CC BY 4.0 "Tactical Shotgun"
(see `ATTRIBUTION.md` and `SOURCE.md`); it is a generic tactical pump gun with wood
furniture, not a model of one named firearm. The display name is one constant
(`LpPumpShotgunSpec.DisplayName`), so a rename is one line.

It rides the upstream DXRP weapon components for everything they already do (fire,
damage, recoil, ammo, shell-by-shell reload, sounds, muzzle flash) and adds only what
upstream lacks: the pump cycle.

## What it does

- **Fire:** semi-only. One shot per trigger pull; holding the trigger does not repeat.
- **Pump cycle:** after every shot the pump runs one stroke (short lead, back, hold,
  forward: 0.50 s by default). Firing and reload start are held until it finishes.
- **Hull ejection:** the spent 12 gauge hull leaves the ejection port at the start of the
  back stroke, not at the shot. It is the upstream casing effect and removes itself.
- **Reload:** shell by shell from the reserve, one sound per shell. Pressing fire during a
  reload stops it after the shell in progress. If the reload began with an empty gun,
  one closing pump (no hull) follows before the gun can fire.
- **Capacity:** 7 in the tube + 1 chambered, held as one pool of 8.

## Files

| Path | What it is |
|---|---|
| `equipment/w_m870/w_m870.prefab` | World / equipment prefab: upstream components plus `LpPumpShotgunCycle` |
| `equipment/vm_m870/vm_m870.prefab` | First-person viewmodel: Spaghelli donor (hidden), `m870_body`, `m870_pump` with `LpPumpSlideVisual`, `LpDonorShellVisibility` |
| `models/`, `materials/`, `textures/`, `source/generated/` | The mesh, split into body and pump |
| `game/Code/Addons/lifepunch/lpweapons/m870/` | The weapon's code (five files) |
| `game/Code/Addons/lifepunch/_dev/M870DevGive.cs`, `M870PumpDev.cs` | Editor-only test commands; not part of the weapon |

## Components

| Component | Where | Job |
|---|---|---|
| `LpPumpShotgunCycle` | `w_m870` Functions | Runs the pump state, raises the `bolting` tag, plays the pump sound, throws the hull, moves the pump, adds the closing pump after an empty reload |
| `LpPumpSlideVisual` | `vm_m870` `m870_pump` | Slides the pump mesh between its rest position and `StrokeOffset` |
| `LpDonorShellVisibility` | `vm_m870` root | Shows the donor's animated shells during reload while the donor gun stays hidden |
| `LpPumpShotgunRules` | code only | The pump rules as plain functions (no engine types), covered by an offline test |
| `LpPumpShotgunSpec` | code only | Named constants: display name, gauge, hull size, capacity |

All of it is visual and local to each client. Nothing here changes ammo, damage, money
or saved state, and the hull is not a networked or saved object.

## Tunables

| Setting | Where | Default | Note |
|---|---|---|---|
| `LeadSeconds` / `BackSeconds` / `HoldSeconds` / `ForwardSeconds` | `LpPumpShotgunCycle` | 0.10 / 0.18 / 0.04 / 0.18 | Stroke total 0.50 s |
| `Delay` | `ShootWeaponComponent` | 0.6 | Time between shots. Keep the stroke total at or below `Delay * 0.85`, which is when the host accepts the next shot |
| `PumpSound` | `LpPumpShotgunCycle` | the Spaghelli shell-load sound | Placeholder: no pump sound asset exists yet |
| `EjectionPrefab` | `LpPumpShotgunCycle` | `prefabs/weapon_effects/12g_casing.prefab` | Leave `ShootWeaponComponent.EjectionPrefab` empty or the hull leaves twice |
| `RackAfterReload` / `RackOnlyFromEmpty` | `LpPumpShotgunCycle` | on / on | Closing pump after a reload that began empty |
| `StrokeOffset` | `LpPumpSlideVisual` | `-3.5,0,0` | Pump travel in inches; back is -X |
| `MaxAmmo` | `AmmoComponent` | 8 | Must equal `LpPumpShotgunSpec.LoadedCapacity` |
| `ReloadTime` / `EmptyReloadTime` | `ReloadWeaponComponent` | 1 / 2 | Seconds per shell; first shell into an empty gun |

## Adding it to a server

The weapon is added as one equipment row on the dxrp.net Portal. Nothing in this
folder writes to the Portal.

| Row field | Value |
|---|---|
| Name | `M870` (or the ruled display name) |
| World prefab path | `addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab` |
| Viewmodel (secondary) prefab path | `addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab` |
| Slot | Primary |
| Ammo | 12 gauge; 8 loaded, 6 reserve on spawn, 24 reserve maximum (set on the prefab) |
| Equipment row id / content id | Assigned by the Portal when the row is created |
| Price | *(left blank: the server owner's decision)* |
| Limit | *(left blank)* |

Editor test without a Portal row: `lp_give_m870` gives the weapon only when the current
game-mode roster already holds this exact row; the editor-only roster inject for it is
off by default (`WeaponRosterInjectTable.InjectM870`).

## Editor test commands (editor only)

| Command | What it does |
|---|---|
| `lp_give_m870` | Gives the weapon to the local player |
| `lp_m870_pump` | Runs one pump cycle without firing |
| `lp_m870_eject` | Throws one hull from the ejection port |
| `lp_m870_reload_state` | Prints the pump state in one line |

## Not proven

This addon was built without the editor. None of the following has been seen running:

- that the code compiles in the engine (it passes an offline compile against stand-in
  types, which is not the engine);
- the pump stroke: its direction, length and timing on screen;
- the hull: where it leaves the gun and how it flies;
- the shells shown during reload, and whether they line up with the loading port;
- the closing pump after an empty reload;
- what other players see and hear;
- every sound, including the placeholder pump sound.

Known gaps: the first-person left hand does not follow the pump (no authored animation
exists); the third-person pump does not move (the world model is one mesh); there is no
dedicated pump sound.
