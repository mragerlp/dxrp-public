# Steam Machine hub — fan setup (ModelDoc)

**Status: PARKED (June 2026)** — `fan_spin_hub` disabled on `bitcoinhub.prefab` until Phase A hub sign-off. Status LED + static chassis remain active.

**Phase 2 (when resumed):** Child-GO fan spin — chassis stays static; only `fan_spin_hub` rotates when powered.

## Single FBX source (alignment law)

Both vmdls import from **`steam-machine.fbx`** (static, no armature) with identical `import_rotation` / `import_scale` / bottom Z align:

| vmdl | import filter |
|------|----------------|
| `bitcoinhub.vmdl` | `base_body`, `front_panel`, `back_body` |
| `bitcoinhub-fan.vmdl` | `fan` |

Re-export from owner blend (snaps fan mesh to `front_panel` before export):

```powershell
powershell -File lifepunchaddons\scripts\Export-BitcoinMinerSteamMachineFbx.ps1
```

## Spin (code)

Hub fan spins on **`Vector3.Up`** (matches Blender `fanAction` axis), counter-clockwise from the grill. Wrong axis (`Forward`) causes blades to orbit out of the cage.

## Prefab

1. Child GO `fan_spin_hub` → `bitcoinhub-fan.vmdl` at **`0,0,0`** (aligned when FBX is unified)
2. Optional: `LpBitcoinHubVisuals.AutoAlignFanToGrille` + `FanManualOffset` if a nudge is still needed
3. `lp_bitcoin_fan_tune` logs body/fan bounds after prefab edits

Compile both vmdls after any FBX or vmdl edit.
