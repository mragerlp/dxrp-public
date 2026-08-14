# Bitcoin Hub — where to work (Phase 1 static chassis)

**Package:** `lpbitcoin` · **Entity:** `bitcoinhub`

## Open in ModelDoc (double-click the `.vmdl`, not `.vmdl_c`)

| File | Asset Browser path |
|------|-------------------|
| **Hub chassis** | `addons/lifepunch/lpbitcoin/bitcoinhub/assets/models/bitcoinhub.vmdl` |
| Fan mesh (Phase 2 only) | `.../bitcoinhub-fan.vmdl` |

## Open in Prefab editor

| File | Path |
|------|------|
| Spawn entity | `.../bitcoinhub/assets/entities/bitcoinhub.prefab` |

**Phase 1 prefab:** static chassis only — no `fan_spin_hub` child (fan wiring is Phase 2; see `HUB_FAN_SETUP.md`).

## Source art

| File | Path |
|------|------|
| FBX | `assets/source/fbx/bitcoinhub.fbx` |
| Blender archive | `assets/source/blend/bitcoinminer.blend` |
| Textures (18 PNG) | `assets/textures/sm_*` |
| Materials | `assets/models/materials/bitcoinhub-sm-*.vmat` |

## ModelDoc won't open?

1. Run: `powershell -File lifepunchaddons\scripts\Invalidate-BitcoinHubCompiled.ps1 -IncludeDxrp`
2. **Restart s&box editor** (Resources mount changed recently).
3. Open **`bitcoinhub.vmdl`** — not `bitcoin-hub.vmdl` (that lives in `_archive/sketchfab-generic-pc/` only).
4. ModelDoc → **Compile** (or F5). Wait for vmats + FBX to resolve.

Stale `_c` files copied from the old `bitcoin-miner` tree block ModelDoc — invalidate forces a clean compile.

## Docs

- `MODEL_BUILD.md` — scale, import_rotation, market spawn law
- `HUB_FAN_SETUP.md` — Phase 2 fan child (deferred)
- `material-map.json` — slot → vmat map

## Do not use

- `bitcoinmining/models/.../bitcoin-miner/` — MOVED.md + archive only
- `_archive/sketchfab-generic-pc/` — parked reference mesh
