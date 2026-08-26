# Bitcoin mining props — collision-first baseline (Jun 2026)

**Do not start over on code.** Hub/rack/terminal entities, economy, terminal UI, and networking stay.
Reset only the **prop layer** (vmdl + prefab + fan child GOs) using this order.

## Why it looks horrible

Animation work (rigged FBX, bone spin, vmdl `power_on` sequences) ran **before** collision and origin were locked.
That stacked broken mesh orientation, guessed BoxColliders, and detached fan parts. The game code is fine; the visuals/physics baseline is not.

## Canonical pattern — Phase 0 (Jun 2026 reset)

| Layer | Rule |
|-------|------|
| **Body vmdl** | **One static mesh** — fans **included** in import (baked blades). No `AnimationList` / no bone spin on body. |
| **Prefab** | `ModelRenderer` on root only. **No** `fan_spin_*` child GOs until Phase 2 sign-off. |
| **Code** | `LpBitcoinHubVisuals` / `LpBitcoinRackVisuals` — status LEDs only; fan spin parked. |
| **Collider** | `BoxCollider` **Center (X,Y,Z)** + **Scale (X,Y,Z)** = `model.Bounds` — must match the **white** mesh wireframe on **all three axes**. In the prefab editor the Box gizmo shows **red / green / blue** edges for the collider volume; that entire box must sit on the white box (not just one axis). `OnAwake` → `LifePunchPropPhysics.SyncBoxColliderFromModel`. |
| **Ground** | Host spawn → `LifePunchGroundContact.AlignMeshBottom` (mesh `Bounds.Mins.z` → surface). |
| **Scale** | Prefab root **`1,1,1`**. Tune **`import_scale`** in vmdl only. |

### Phase 2 — separate spinning fans (after Phase 0 + collision sign-off)

Evo Bitminer pattern (study only): fan blades **excluded** from body vmdl; small `*-fan.vmdl` on child GOs; code spins `LocalRotation`.

## Phase checklist (do in order — skip phase 2 until phase 0 + collision are green)

### Phase 0 — Unified static models (current)

1. **GPU rack:** `gpu-rack.vmdl` → `source/gpu-rack-static.obj` (scale 0.395, Z translation 21.382).
2. **Advanced rack:** `gpu-rack-stacked.vmdl` → `source/gpu-rack-stacked-anim.fbx` (static mesh, no anim nodes).
3. **Hub:** `bitcoinhub.vmdl` import_filter includes **`fan`** with body meshes.
4. Prefabs: `ModelRenderer` only; **empty** `Children` on rack prefabs.
5. ModelDoc compile all three vmdls → sync → flatgrass spawn kit.

### Phase 1 — Collision + feet (all props)

1. ModelDoc: `import_translation = [0,0,0]` unless bridge proves otherwise. Align **Center / Center / Bottom** (hub + rack).
2. Compile body vmdl. Open prefab tab → **white wireframe** = compiled mesh bounds. Select **BoxCollider** → **Center X,Y,Z** and **Scale X,Y,Z** must match `model.Bounds` so the **red/green/blue** collider box fully overlaps white (no offset on any axis).
3. `OnAwake` sync on entity (hub, rack, terminal) — already wired Jun 2026.
4. Play flatgrass → `lp_bitcoin_spawn_hub` / `lp_bitcoin_spawn_kit` → props sit on ground, no fall-through.
5. `lp_bitcoin_scale_audit` → log line `modelBounds center=… size=…` matches BoxCollider fields.

| Prop | Prefab | Body vmdl |
|------|--------|-----------|
| Hub | `lpbitcoin/bitcoinhub/assets/entities/bitcoinhub.prefab` | `lpbitcoin/bitcoinhub/assets/models/bitcoinhub.vmdl` |
| GPU rack | `entities/gpurack/gpu-rack.prefab` | `gpu-rack/gpu-rack.vmdl` |
| GPU Rack (farm) | `entities/advancedgpurack/advanced-gpu-rack.prefab` | `gpu-rack/gpu-rack-stacked.vmdl` |
| Terminal | `entities/bitcoin-terminal/bitcoin-terminal.prefab` | *(owner replacing model — pause)* |

### Phase 2 — Separate spinning fans (after phase 0 + 1 sign-off)

1. Enable **`fan_spin_hub`** (hub) — nudge in prefab editor until blade sits in cage.
2. Enable **`fan_spin_rack_1`** first (rack) — clone offsets to siblings.
3. Play → `lp_bitcoin_playtest_mining` → only blades spin; chassis stays rigid.
4. Clone Evo pattern to **advanced-gpu-rack** (still on legacy placeholders).

### Phase 3 — Polish (defer)

- Fence LEDs, hub admin UI chrome, RGB gpu vmats, terminal LCD tune.
- New terminal model intake when owner ships replacement FBX.

## What we are NOT doing again

- Rigged fan FBX as body `RenderMeshFile`
- `LpBitcoinSkinnedFanSpin` / `SetBoneTransform` fan hacks
- Hand-authored BoxCollider guesses (`25×20×36`, `0,0,9`, etc.)
- vmdl `power_on` / `power_off` sequences on rack body until static baseline ships

## Dev commands

| Command | When |
|---------|------|
| `lp_bitcoin_scale_audit` | After vmdl compile — copy `modelBounds` to prefab if needed |
| `lp_bitcoin_fan_tune` | After nudging `fan_spin_*` in prefab editor |
| `lp_bitcoin_playtest_mining` | Fan spin + mining smoke test |
| `lp_bitcoin_spawn_kit` | Hub + terminal + racks |

## Start over?

**No full restart.** Keep all C# under `Code/Addons/lifepunch/bitcoinmining/`.
If a prefab is beyond repair, delete **only** that prefab + body vmdl filter nodes and rebuild from this doc + Evo reference — reattach the same entity components.

---

## Model intake queue (owner drops — Jun 2026)

**Full folder map:** `addons/docs/MODEL_INTAKE_DROP_MAP.md`

Drop FBX packs under OneDrive `LIFEPUNCH*\addons\` (or send any path + entity name). Colors irrelevant. Code + prefab entity components stay; Phase 1 collision on all axes before fans/LEDs.

**GPU racks:** no new mesh — collision-only on existing art.

