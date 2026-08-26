# Bitcoin hub (`bitcoinhub`) — start here

**Package:** `lpbitcoin` · **Entity slug:** `bitcoinhub` · **Portal:** `lifepunchbitcoin`

## Open in ModelDoc

| File | Asset Browser path |
|------|-------------------|
| **Hub chassis** | `addons/lifepunch/lpbitcoin/bitcoinhub/assets/models/bitcoinhub.vmdl` |
| **Fan mesh** | `.../bitcoinhub-fan.vmdl` |

## Prefab (gameplay)

| File | Path |
|------|------|
| Spawn entity | `addons/lifepunch/lpbitcoin/bitcoinhub/assets/entities/bitcoinhub.prefab` |

## Source art

| File | Path |
|------|------|
| FBX | `assets/source/fbx/bitcoinhub.fbx` |
| Blender archive | `assets/source/blend/bitcoinminer.blend` |
| Textures (18 PNG) | `assets/textures/sm_*` |
| Materials | `assets/models/materials/bitcoinhub-sm-*.vmat` |

## Docs (this folder)

- `assets/models/MODEL_BUILD.md` — scale, import_rotation, market spawn law
- `assets/models/HUB_FAN_SETUP.md` — fan child wiring
- `audit/manifest.json` — slot inventory

## Legacy path (do not use)

`bitcoinmining/models/.../bitcoin-miner/` → see `MOVED.md` there.

## Sync to DXRP

```powershell
powershell -File lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon bitcoinmining
```

**Phase 1 prefab:** static chassis only — no `fan_spin_hub` child (fan = Phase 2; see `assets/models/HUB_FAN_SETUP.md`).

## ModelDoc won't open?

1. `powershell -File lifepunchaddons\scripts\Invalidate-BitcoinHubCompiled.ps1 -IncludeDxrp`
2. **Restart s&box editor**
3. Open **`assets/models/bitcoinhub.vmdl`** — not `bitcoin-hub.vmdl` (`_archive/` only)
4. Compile in ModelDoc (F5)

Stale `_c` copied from old `bitcoin-miner` paths blocks ModelDoc until invalidated.
