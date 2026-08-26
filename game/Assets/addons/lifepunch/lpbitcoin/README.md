# lpbitcoin — LIFEPUNCH Bitcoin package (editor + publish staging)

**Package slug:** `lifepunchbitcoin` · **Code mount (repo):** `Code/Addons/lifepunch/bitcoinmining/`  
**Editor Asset Browser:** `Assets/addons/lifepunch/lpbitcoin/` only — no `bitcoinmining` assets folder.

## Addon content (four entities — complete set)

Everything shipped / playtested under this package is one of these:

| # | Entity | Folder slug | Prefab |
|---|--------|-------------|--------|
| 1 | **Bitcoin Hub** | `bitcoinhub/` | `bitcoinhub.prefab` |
| 2 | **HASHD Terminal** | `hashdterminal/` | `hashdterminal.prefab` |
| 3 | **GPU Rack** | `gpurack/` | `gpurack.prefab` |
| 4 | **Advanced GPU Rack** | `gpurack/` | `advancedgpurack.prefab` |

Per-operator cap (portal): **1 hub · 1 terminal · 2× GPU Rack · 1× Advanced GPU Rack** — see `LpBitcoinIdent.cs`.

## Entity slots (folder name = slug)

| Folder | What you edit / spawn | ModelDoc vmdl | Prefab |
|--------|----------------------|---------------|--------|
| `bitcoinhub/` | Bitcoin Hub (ops panel, power, wallet) | `assets/models/bitcoinhub.vmdl` | `assets/entities/bitcoinhub.prefab` |
| `hashdterminal/` | HASHD Terminal (rig0 CRT) | **`assets/models/hashdterminal.vmdl`** | **`assets/entities/hashdterminal.prefab`** |
| `gpurack/` | GPU Rack + Advanced GPU Rack | **`gpurack.vmdl`** · **`advancedgpurack.vmdl`** | **`gpurack.prefab`** · **`advancedgpurack.prefab`** |

Shared sounds + HASHD UI mark: `bitcoinhub/assets/sounds/` · `bitcoinhub/assets/ui/hashd/`

## Dev spawn (flatgrass)

```text
lp_bitcoin_clear_spawns
lp_bitcoin_spawn_five_prefabs   # all four entities: hub + terminal + 2× GPU rack + advanced rack
lp_bitcoin_dual_tester          # same full set per connected player (host, multiplayer)
```

Paths resolve via `LpBitcoinIdent.cs` — all under `lpbitcoin/…`.

## Archive (docs only — no compile artifacts)

s&box compiles **every** `.vmdl` / `.vmat` under `Assets/`. Archive folders must keep **MD/JSON/source FBX only** — never duplicate ModelDoc files.

- `_archive/advancedgpurack-intake/` — retired intake; canonical stacked mesh is **`advancedgpurack.vmdl`**
- `bitcoinhub/_archive/` — Sketchfab hub + phase2 fan experiments (retired)
- `gpurack/_archive/small-open-frame/` — parked single-unit mesh experiments (not shipped)

**Retired Jun 2026:** hyphenated `gpu-rack*.vmdl` / `gpu-rack*.prefab` — use **`gpurack`** / **`advancedgpurack`** names only.

Sync script strips any stray `_archive` compile artifacts from DXRP after mirror.

## Sync

```powershell
powershell -File lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon lpbitcoin
```

Mirrors repo → **DXRP editor game** (compile + playtest truth):

```text
D:\Steam\steamapps\common\sbox\dxrp\game\Assets\addons\lifepunch\lpbitcoin\
D:\Steam\steamapps\common\sbox\dxrp\game\Code\Addons\lifepunch\bitcoinmining\
```

## Publish (Rev 3+ — when owner says ship)

After ModelDoc compile + Stop → Play on DXRP game:

```powershell
powershell -File lifepunch\scripts\Prepare-LpBitcoinPublish.ps1 -OpenFolder
```

Upload **`.dxrp-publish/upload/lifepunch/lpbitcoin/Assets`** and **`.../Code`** to dxrp.net (≤ 300 MB combined). See `addons/docs/DXRP_ADDON_PUBLISH_DOCTRINE.md`.

After ModelDoc compile (backport *_c to git):

```powershell
powershell -File lifepunch\scripts\Pull-DxrpCompiledAssetsToRepo.ps1 -Addon lpbitcoin
```
