# lpbitcoin - issues

**Priority:** P0 | **Health score:** 85/100 | **Size:** 799 MB

See ASSET_CLASSIFICATION_LAW.md - do not merge this package with visually similar folders.

## Slot status

### advancedgpurack
- **Stacked farm tier** — `gpurack/assets/models/advancedgpurack.vmdl` + `gpurack/assets/entities/advancedgpurack.prefab`
- Compile target: **`advancedgpurack.vmdl_c`** only (not `gpu-rack-stacked.vmdl_c`)

### bitcoinhub
- Role: Bitcoin HUB (admin capstone)
- Primary: assets/source/fbx/generic-pc-desktop.fbx
- Texture: assets/textures/generic-pc-desktop_basecolor.png
- vmdl: assets/models/bitcoin-hub.vmdl
- FBX/OBJ/Blend/Tex: 1/0/0/1 | ~2.3k verts (Sketchfab CC BY Bryan)
- Retired: cpu_gamer.fbx (Fab CPU GAMER — do not use for hub)
- Note: Fan spin = child GO Phase 2

### gpurack
- Role: GPU Rack (standard single + stacked farm in same slot folder)
- Primary: `assets/models/gpurack.vmdl` (single) · `assets/models/advancedgpurack.vmdl` (stacked)
- Prefabs: `assets/entities/gpurack.prefab` · `assets/entities/advancedgpurack.prefab`
- Compile `_c`: **`gpurack.vmdl_c`** + **`advancedgpurack.vmdl_c`** only
- FBX/OBJ/Blend/Tex: 0/1/1/32 | 34.61 MB
- Note: Anim FBX under `assets/source/fbx/` for ModelDoc import

### hashdterminal
- Role: HASHD Terminal
- Primary: `assets/models/hashdterminal.vmdl` · `assets/entities/hashdterminal.prefab`
- Source: `assets/source/fbx/hashdterminal.fbx`
- Compile `_c`: **`hashdterminal.vmdl_c`** only (not `hashd-terminal.vmdl_c`)
- FBX/OBJ/Blend/Tex: 1/0/1/22 | 585.64 MB

## Pending ModelDoc metrics
- Triangle counts: run after vmdl compile (not available from filesystem scan)
- Scale consistency: compare spawned bounds in lifepunch-modeldoc.scene

