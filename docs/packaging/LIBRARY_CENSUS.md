# LIBRARY CENSUS - game/Libraries on the live branch, for the mirror (Phase B re-seed input)

Census date: 2026-10-17 (16:10Z). This file decides nothing; it is the input for the decision on what the mirror re-seeds.

## Sensors

- **Tracked set**: the live workbench index file `.git/index` (DIRC v2, 6168 entries), read as a file by a scratch parser. No git verb ran in the live tree. The parser was checked against two known paths: a staged add was present and a staged delete was absent. "Tracked on the live branch" here means the index at 2026-10-17 11:07 local: HEAD d9c08031 (codex/ulx-polish-20260914) plus the staged changes. HEAD alone was not read.
- **Size**: `index_size_bytes` is the size recorded in the index. `disk_size_bytes` is os.path.getsize of the live file, and is blank when the file is absent from disk.
- **Develop tracks**: `git ls-tree -r origin/develop -- game/Libraries` at 4cc698949df40ac8b85ed0eb5568f5c9fdb38f9b, run in a clone of this repo.
- **Develop ignores**: `git check-ignore --no-index -v -n --stdin` in a clone of this repo checked out at develop 4cc69894, over every tracked path. Positive control: synthetic `game/Libraries/x/foo.meta`, `x/a.bak` and `x/obj/a.cs` all came back ignored (.gitignore:103, :505, :58).
- **Package markers**: the `<name>.sbproj` JSON on disk (Org + Ident + Type), plus `.bin/package.<org.ident>.xml` in the tracked set. The revision is the `.version` file on disk.
- **Fetchable**: inferred from the markers only. No network call was made to the s&box package service, so fetchability is **UNVERIFIED** for every row.

## Classification rule

- **published**: the folder name equals the sbproj `Org.Ident`, and `.bin/package.<ident>.xml` is tracked. Fetchable by ident (UNVERIFIED).
- **local**: a `local.*` ident, a GitHub archive folder (`*-main`), or a `_bak*` folder. JUDGEMENT DISCLOSED: `fbx-clip-extractor-master` (`*-master`) is filed with `*-main`.
- **unknown**: anything else. All 7 unknowns have a matching `Org.Ident` and a tracked `.version` revision but no `.bin/package` xml, either tracked or on disk.

## Totals

| Class | Folders | Files | Index bytes | Files absent on disk |
|---|---:|---:|---:|---:|
| published | 23 | 1883 | 100.2 MB (100223115 B) | 181 |
| unknown | 7 | 279 | 57.2 MB (57204106 B) | 35 |
| local | 8 | 651 | 7.5 MB (7473696 B) | 0 |
| root-file | 1 | 1 | 0.0 MB (0 B) | 0 |
| **total** | 39 | 2814 | 164.9 MB (164900917 B) | 216 |

- Develop tracks **1** of the 2814 paths (`game/Libraries/.gitkeep`) and ignores **0** of them. Every library file is live-branch-only.
- K1 (50 MB decimal): **0** tracked Libraries files exceed 50,000,000 B. The largest is `scene.canadian_pocketknife/data/mountiemaple-seed.db.gz` at 41,135,826 B (index size), and it is absent on disk.
- 216 tracked paths are absent from disk (deleted on disk but not staged): humanoid_retargeter 134, humanoid_hand_retargeter 46, humanoid_rigger 21, canadian_pocketknife 14, geppetto 1.
- Two folders on disk are not tracked at all and are outside the census rows: `local.cvlxobservatory` (sbproj local.cvlxobservatory) and `yugi.hierarchychanges` (two sbproj: local.hierarchychanges and yugi.hierarchychanges).
- Develop's compiled-output ignores (`*.vmat_c`, `*.vtex_c` and the rest, .gitignore:483-490) are scoped to `game/Assets/**`. So the `_c` files under Libraries are **not** ignored on develop and would be tracked if added.

## Per library

| Folder | Class | Files | Index bytes | Disk bytes | Absent | Rev (.version) | Licence file | Develop ignores / tracks | Basis |
|---|---|---:|---:|---:|---:|---|---|---|---|
| `(root)` | root-file | 1 | 0 | 0 | 0 | - | - | 0 / 1 | game/Libraries/.gitkeep |
| `_bak-sbox-ff-20260820-1405` | local | 165 | 3641900 | 3641900 | 0 | - | - | 0 / 0 | _bak backup folder |
| `ali3nsystems.lib-atmokinetic_assets` | published | 72 | 56175628 | 45890572 | 0 | 1.0.338612 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.ali3nsystems.lib-atmokinetic_assets.xml tracked |
| `ali3nsystems.lib-metrify` | published | 16 | 92881 | 92881 | 0 | 1.0.338943 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.ali3nsystems.lib-metrify.xml tracked |
| `ali3nsystems.lib-shader_graph_extras` | published | 221 | 22792148 | 22792148 | 0 | 1.0.336415 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.ali3nsystems.lib-shader_graph_extras.xml tracked |
| `auto-rigger-main` | local | 134 | 1303974 | 1303974 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `bluedock.modelpro` | published | 27 | 87748 | 87748 | 0 | 1.0.340317 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.bluedock.modelpro.xml tracked |
| `brax.unrealimporter` | unknown | 19 | 216717 | 204943 | 0 | 1.0.423542 | - | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `facepunch.libstates` | published | 26 | 97986 | 97986 | 0 | 1.0.335846 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.facepunch.libstates.xml tracked |
| `fbx-clip-extractor-master` | local | 2 | 36376 | 36376 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `fieldguide.placement` | published | 34 | 228052 | 228052 | 0 | 1.0.337877 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.fieldguide.placement.xml tracked |
| `fieldguide.tips` | published | 49 | 539419 | 539419 | 0 | 1.0.337872 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.fieldguide.tips.xml tracked |
| `fieldguide.vehiclephysics` | published | 86 | 1286149 | 1291480 | 0 | 1.0.337869 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.fieldguide.vehiclephysics.xml tracked |
| `fobiat.sbox_mcp_server` | unknown | 9 | 62310 | 62310 | 0 | 1.0.342130 | - | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `fwotsoft.lightinghelpers` | published | 17 | 32415 | 32415 | 0 | 1.0.355266 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.fwotsoft.lightinghelpers.xml tracked |
| `fwotsoft.rotsoftlighting` | published | 17 | 33125 | 33125 | 0 | 1.0.355262 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.fwotsoft.rotsoftlighting.xml tracked |
| `kamishell.blender_bridge` | unknown | 14 | 177892 | 177892 | 0 | 1.0.337656 | - | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `kitsupanic.sbox_mcp_plus` | unknown | 14 | 1013709 | 1019576 | 0 | 1.0.423404 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `local.auto_rig` | local | 134 | 1330222 | 1330222 | 0 | - | - | 0 / 0 | local.* ident |
| `mikekotys.assetdoctor` | published | 18 | 59149 | 59149 | 0 | 1.0.337738 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.mikekotys.assetdoctor.xml tracked |
| `notpointless.chomnr_adaptive_director` | published | 62 | 401628 | 401628 | 0 | 1.0.337987 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.notpointless.chomnr_adaptive_director.xml tracked |
| `notpointless.chomnr_better_ik` | published | 55 | 221155 | 221155 | 0 | 1.0.311785 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.notpointless.chomnr_better_ik.xml tracked |
| `notpointless.chomnr_humanoid_hand_retargeter` | published | 113 | 841274 | 723829 | 46 | 1.0.394704 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.notpointless.chomnr_humanoid_hand_retargeter.xml tracked |
| `notpointless.chomnr_humanoid_retargeter` | published | 143 | 3727558 | 868887 | 134 | 1.0.422539 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.notpointless.chomnr_humanoid_retargeter.xml tracked |
| `notpointless.chomnr_humanoid_rigger` | unknown | 124 | 1054275 | 1004981 | 21 | 1.0.421644 | - | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `notpointless.chomnr_mcp` | unknown | 57 | 404689 | 404689 | 0 | 1.0.367408 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `pooh.geppetto` | published | 445 | 7300896 | 8535667 | 1 | 1.0.411088 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.pooh.geppetto.xml tracked |
| `redsnail.enginebankslicer` | published | 9 | 57284 | 57284 | 0 | 1.0.344082 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.redsnail.enginebankslicer.xml tracked |
| `sbox-adaptive-director-main` | local | 60 | 347055 | 347055 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `sbox-motionbricks-weights-main` | local | 1 | 501 | 501 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `sbox-navmeshplus-main` | local | 18 | 97205 | 97205 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `sbox-two-brain-director-main` | local | 137 | 716463 | 716463 | 0 | - | - | 0 / 0 | GitHub archive folder (-main/-master) |
| `sboxcool.network-storage` | published | 122 | 1111581 | 1127846 | 0 | 1.0.423774 | LICENSE | 0 / 0 | sbproj Org.Ident = folder; .bin/package.sboxcool.network-storage.xml tracked |
| `sboxskinsgg.claudebridge` | published | 83 | 2183341 | 2182882 | 0 | 1.0.382152 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.sboxskinsgg.claudebridge.xml tracked |
| `scene.canadian_pocketknife` | unknown | 42 | 54274514 | 2473138 | 14 | 1.0.422385 | - | 0 / 0 | sbproj Org.Ident = folder; no .bin/package xml (tracked or disk) |
| `sonac.sbox-animator` | published | 46 | 1017996 | 1017996 | 0 | 1.0.337289 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.sonac.sbox-animator.xml tracked |
| `stellawisps.fxbox` | published | 34 | 588755 | 589516 | 0 | 1.0.379493 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.stellawisps.fxbox.xml tracked |
| `sturnus.imagetomaterial` | published | 9 | 21334 | 21334 | 0 | 1.0.379602 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.sturnus.imagetomaterial.xml tracked |
| `xaz.goo` | published | 179 | 1325613 | 1397277 | 0 | 1.0.366815 | - | 0 / 0 | sbproj Org.Ident = folder; .bin/package.xaz.goo.xml tracked |

Some rows differ between index bytes and disk bytes (for example atmokinetic_assets 56.2 MB vs 45.9 MB, geppetto 7.30 MB vs 8.54 MB). Those files were modified on disk after they were staged. The mirror must pick one side.

## What this means for the mirror re-seed (recommendation; the owner decides)

1. **published (23 folders, 1883 files, 100.2 MB)**: re-seed by ident at the recorded `.version` revision instead of vendoring the bytes into the fork. Fetchability is UNVERIFIED until a package-service lookup is run.
2. **unknown (7 folders, 279 files, 57.2 MB)**: each has an ident and a revision but no compiled package marker. Each needs a fetch test (does the ident resolve at that revision?) before it is treated as published. `scene.canadian_pocketknife` carries 54.3 MB in the index, mostly two data files that are absent on disk.
3. **local (8 folders, 651 files, 7.5 MB)**: no package service can re-seed these. Each needs a decision: vendor into the fork or lifepunch-assets, keep as a machine-local layer, or drop. `_bak-sbox-ff-20260820-1405` holds older copies of two notpointless packages and is a candidate to drop.
4. The two untracked folders (`local.cvlxobservatory`, `yugi.hierarchychanges`) are outside the mirror unless the owner adds them.

## Per-file appendix

`docs/packaging/LIBRARY_CENSUS.csv`: one row per tracked path (2814 rows + header). Columns: path, folder, class, index_size_bytes, disk_size_bytes, index_blob_sha1, develop_ignore, develop_tracks. 535407 B / SHA-256 ED300C418EF23F87E97492F8C955ADD42E270BEB5E921532A68E7E1C2BDAC0EB.
