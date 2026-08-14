# gpurack — GPU Rack

Fresh art lives under `assets/textures/{Wires,GPU_Rack,Power_Supply,Motherboard,GPU_GraphicsCard}/`.

## Two ship tiers (Jun 2026)

| Tier | Prefab | ModelDoc | FBX source |
|------|--------|----------|------------|
| **Single rack** (baseline) | `assets/entities/gpurack.prefab` | **`assets/models/gpurack.vmdl`** | `assets/source/fbx/gpurack.fbx` |
| **Stacked farm** (upgrade) | `assets/entities/advancedgpurack.prefab` | **`assets/models/advancedgpurack.vmdl`** | `assets/source/fbx/advancedgpurack.fbx` |

Shared: five `gpu-rack-*.vmat` · `material-map.json` · `LpBitcoinRackEntity` gameplay.

**Retired:** `gpu-rack.vmdl`, `gpu-rack-stacked.vmdl`, `gpu-rack.prefab`, `gpu-rack-stacked.prefab` — do not compile or publish.

**Do not open:** `assets/source/gpu_crypto_farm.fbx` — Blender master export with artist scale-reference human; not used by ship vmdls.

**Spawn / ident:** `LpBitcoinIdent.RackPrefabPath` → **`gpurack.prefab`**. Stacked via **`advancedgpurack.prefab`**.
