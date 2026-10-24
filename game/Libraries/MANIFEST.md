# game/Libraries - what the editor expects

This folder is not tracked in git except for this file and `.gitkeep`; see `docs/developers/LIBRARIES.md` for why and for how a fresh clone gets its libraries. This page lists names and metadata only. No library file is in this repository.

Evidence: read from a developer tree on 2026-10-23. The folder held 40 library folders; the editor loaded all 40. A library loads by its folder being present under `game/Libraries/` (`rp.sbproj` lists no package references), so a missing folder is a missing library, not a build error. Versions are the `.version` file the editor library manager writes (the sbox.game package revision), where one exists.

| Library folder | Version | Licence file | Kind | Loaded by editor |
| --- | --- | --- | --- | --- |
| `ali3nsystems.lib-atmokinetic_assets` | 1.0.338612 | yes (MIT) | third-party | yes |
| `ali3nsystems.lib-metrify` | 1.0.338943 | yes (MIT) | third-party | yes |
| `ali3nsystems.lib-shader_graph_extras` | 1.0.336415 | yes (MIT) | third-party | yes |
| `bluedock.modelpro` | 1.0.340317 | no | third-party | yes |
| `brax.unrealimporter` | 1.0.423542 | no | third-party | yes |
| `facepunch.libstates` | 1.0.335846 | yes (MIT) | third-party | yes |
| `fbx-clip-extractor-master` | - | no | third-party | yes |
| `fieldguide.placement` | 1.0.337877 | yes (MIT) | third-party | yes |
| `fieldguide.tips` | 1.0.337872 | yes (MIT) | third-party | yes |
| `fieldguide.vehiclephysics` | 1.0.337869 | yes (MIT) | third-party | yes |
| `fobiat.sbox_mcp_server` | 1.0.342130 | no | third-party | yes |
| `fwotsoft.lightinghelpers` | 1.0.355266 | no | third-party | yes |
| `fwotsoft.rotsoftlighting` | 1.0.355262 | no | third-party | yes |
| `kamishell.blender_bridge` | 1.0.337656 | no | third-party | yes |
| `kitsupanic.sbox_mcp_plus` | 1.0.423404 | yes (MIT) | third-party (editor bridge the house tooling uses) | yes |
| `local.cvlxobservatory` | - | no | house (authored in this workbench) | yes |
| `mikekotys.assetdoctor` | 1.0.337738 | no | third-party | yes |
| `notpointless.chomnr_adaptive_director` | 1.0.337987 | no | third-party | yes |
| `notpointless.chomnr_better_ik` | 1.0.311785 | yes (MIT) | third-party | yes |
| `notpointless.chomnr_humanoid_hand_retargeter` | 1.0.394704 | no | third-party | yes |
| `notpointless.chomnr_humanoid_retargeter` | 1.0.426088 | no | third-party | yes |
| `notpointless.chomnr_humanoid_rigger` | 1.0.421644 | no | third-party | yes |
| `notpointless.chomnr_mcp` | 1.0.367408 | yes (MIT) | third-party (editor bridge the house tooling uses) | yes |
| `notpointless.chomnr_sbox_superuser` | 1.0.426518 | no | third-party | yes |
| `pooh.geppetto` | 1.0.411088 | yes (MIT) | third-party | yes |
| `redsnail.enginebankslicer` | 1.0.344082 | no | third-party | yes |
| `sbox-adaptive-director-main` | - | no | third-party | yes |
| `sbox-motionbricks-weights-main` | - | no | third-party | yes |
| `sbox-navmeshplus-main` | - | no | third-party | yes |
| `sbox-two-brain-director-main` | - | no | third-party | yes |
| `sboxcool.network-storage` | 1.0.427072 | yes (MIT) | third-party | yes |
| `sboxskinsgg.claudebridge` | 1.0.382152 | no | house organ (package from sbox.game) | yes |
| `scene.canadian_pocketknife` | 1.0.422385 | no | third-party | yes |
| `sonac.sbox-animator` | 1.0.337289 | no | third-party | yes |
| `stellawisps.fxbox` | 1.0.379493 | no | third-party | yes |
| `sturnus.imagetomaterial` | 1.0.379602 | no | third-party | yes |
| `xaz.goo` | 1.0.366815 | no | third-party | yes |
| `yugi.hierarchychanges` | 1.0.381874 | no | third-party | yes |

Two further libraries exist in the developer tree and are deliberately not itemised here. They are counted in the 40 above.

## Not libraries

- `_bak-sbox-ff-20260820-1405`: a local backup folder (dated 2026-08-20 in its name). It is not a library root and is not loaded.

## Notes

- 26 of the 38 itemised libraries carry no licence file and 12 carry an MIT licence file. That is the reason no library bytes are published from this repository.
- `sbox-adaptive-director-main` and `notpointless.chomnr_adaptive_director` declare the same organisation and ident (an unpacked repository zip beside the package install). Both folders are present and loaded in the tree this list was read from.
- Folders whose Version is `-` have no `.version` file: `local.cvlxobservatory` is authored here, the five `*-main` / `*-master` folders are unpacked repository zips.
- No file under `game/Code` references a type or namespace from these libraries (swept 2026-10-23, 961 files on develop). Use from assets, scenes or shaders was not swept.
