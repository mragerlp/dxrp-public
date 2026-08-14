# GPU Rack — ModelDoc

**Two ship tiers** — entity slug names match vmdl filenames (no hyphenated legacy names).

| Tier | vmdl (compile `_c` here) | prefab | FBX |
|------|--------------------------|--------|-----|
| Single (baseline) | **`gpurack.vmdl`** | `gpurack.prefab` | `source/fbx/gpurack.fbx` |
| Stacked farm | **`advancedgpurack.vmdl`** | `advancedgpurack.prefab` | `source/fbx/advancedgpurack.fbx` |

**Retired (do not compile):** `gpu-rack.vmdl`, `gpu-rack-stacked.vmdl`, `gpu-rack.prefab`, `gpu-rack-stacked.prefab` — removed Jun 2026.

**Texture roots:** `assets/textures/{Wires,GPU_Rack,Power_Supply,Motherboard,GPU_GraphicsCard}/`

## ModelDoc P0

| Step | Action |
|------|--------|
| 1 | Single: open **`gpurack.vmdl`** — FBX = **`gpurack.fbx`**. Stacked: open **`advancedgpurack.vmdl`** — FBX = **`advancedgpurack.fbx`** |
| 2 | Stacked import filter must include **`Mining_Rig_Stacked`**. Single-rack names (`FanBox_*`, `Rack_Frame`, …) do not exist in the stacked FBX. |
| 3 | Confirm remaps + five vmats compile clean (subfolder texture paths) |

## ModelDoc settings

| Field | Value |
|-------|--------|
| import_scale | **0.72** baseline — tune on flatgrass |
| import_rotation | `[0, 90, 0]` |
| Materials | See `material-map.json` + five `gpu-rack-*.vmat` |
| **use_global_default** | **`true` in KV3 for compile** (full body). Per-slot remaps bind the five ship vmats. |
| Physics | HullPerElement (ModelDoc) + BoxCollider on prefab (gameplay baseline) |

## After compile

Only these `_c` files belong in this folder:

- `gpurack.vmdl_c`
- `advancedgpurack.vmdl_c`

Pull from DXRP after ModelDoc:

```powershell
powershell -File lifepunch\scripts\Pull-DxrpCompiledAssetsToRepo.ps1 -Addon lpbitcoin
```

## Sign-off gate (Phase C)

- [ ] Stacked mesh compiles clean (full body, not floating fans)
- [ ] PSU / GPU branding readable (EV3X, RAZX-A9, orange wires)
- [ ] Scale vs hub on flatgrass kit
- [ ] Collider matches visible mesh
