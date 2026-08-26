# Advanced GPU Rack (stacked) — ModelDoc foundation pass

**Source:** `assets/source/fbx/gpu-rack-stacked-anim.fbx`  
**vmdl:** `assets/models/gpu-rack-stacked.vmdl`  
**Materials:** Reuses `lpbitcoin/gpurack/assets/models/materials/*.vmat` (textures not duplicated here)

## ModelDoc settings (v1)

| Field | Value |
|-------|--------|
| import_scale | 0.72 |
| import_translation | -1.389, -0.208, 27.682 (legacy seed) |
| import_rotation | 0, 90, 0 |
| Animations | `power_on`, `Mining_Rig_Stacked` from same FBX |
| root bone | `rack_root` |

## Sign-off gate (Phase C — large rack)

- [ ] Mesh + anims compile clean
- [ ] Mining_Rig_Stacked loop obvious vs idle
- [ ] Hero kit: hub + 3 small + 1 stacked on flatgrass
