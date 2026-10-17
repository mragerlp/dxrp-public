# CP2 asset checkpoint - size and licence screens

Source: the live workbench tree `D:\Cavelux\dxrp-workbench\game`, read only, sensed 2026-10-17 ~14:30Z. Every row is in `COPY_MANIFEST_CP2.csv` (this folder): path, bytes, action, reason, develop status, git disposition, SHA-256 live at plan, SHA-256 live at copy, SHA-256 in the clone, verdict. 482 of 482 copied files read MATCH (live = clone).

## Scope

| Unit | Live files | Copied | Excluded | Staged on this branch |
|---|---:|---:|---:|---:|
| lplockpick | 35 | 35 | 0 | 12 (11 new + source/crowbar.vmdl modified) |
| lpmorpheus | 37 | 37 | 0 | 16 new |
| lptags extras | 4 (2 already on develop) | 2 | 2 (tracked on develop) | 1 new (ATTRIBUTION.md) |
| lpbitcoin runtime subset | 552 | 408 | 144 | 15 new (12 ATTRIBUTION.md + 3 gpurack/assets/textures/gpu_farm_*.vmdl) |

"Copied" means copied into the clone working tree. A copied file is staged only if develop's `.gitignore` allows it and it differs from develop. 162 copied files equal develop modulo CRLF and produce no change. 276 copied files are ignored by develop's `.gitignore` and are not force-added:

| Rule | Files |
|---|---:|
| `.gitignore:103 *.meta` | 178 |
| `.gitignore:502 game/Assets/**/*.generated.vtex_c` | 48 |
| `.gitignore:483 game/Assets/**/*.vmat_c` | 18 |
| `.gitignore:486 game/Assets/**/*.vmdl_c` | 11 |
| `.gitignore:499 *.sound_c` / `:487 *.vsnd_c` | 7 + 7 |
| `.gitignore:500 game/Assets/**/*.prefab_c` | 6 |
| `.gitignore:505 *.bak` | 1 |

One of the 178 is the lptags extra `tags/ui/fonts/robotomono-license-ofl-1.1.txt.meta`, which the dispatch named. It is not force-added; that is a ruling owed.

## Exclusion rule (lpbitcoin only)

A file is excluded if any folder in its path is `_archive`, `audit`, `source` or `provenance`, or contains `extract` (no folder of that name exists in lpbitcoin), or if it is over 50 MB. 50 MB is read as 50,000,000 bytes (decimal).

- 139 files excluded by folder (70.4 MB).
- 5 files excluded by size: `lpbitcoin/hashdterminal/assets/textures/{Keyboard_mause_Color, Keyboard_mause_Normal, Monitor_Color, Monitor_Emissive, Monitor_Normal}.tga`, 50,331,692 B each. They are under 50 MiB (52,428,800 B) and over 50 MB decimal. They are already tracked on develop.
- 57 of the 144 excluded files (304.5 MB) are already tracked on develop. This branch does not remove them; removal is not in CP2's scope.

## Size screen

- Nothing over 50,000,000 B is added by this branch. The largest staged file is 8,182,553 B. The staged set is 44 asset files, 25,251,321 B.
- The lpbitcoin runtime subset (408 files) is 322.8 MB. That is OVER the 300 MB Portal asset bar. PKG-1 estimated ~181 MB; the gap comes mostly from the thirteen hashdterminal TGAs (each about 50 MB; 5 of them are excluded here only by the strict 50 MB reading).
- Live lpbitcoin is 648.3 MB (552 files).

## Licence screen

ATTRIBUTION files are copied byte for byte and never edited. 21 ATTRIBUTION files are in scope: 1 lptags, and 20 lpbitcoin. All 20 lpbitcoin files read "Origin and licence left blank."; 16 of them are outside `_archive`.

Blank, in the copied runtime subset (12):
- lpbitcoin/bitcoinhub/assets/textures/ATTRIBUTION.md
- lpbitcoin/bitcoinhub/assets/ui/hashd/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/GPURack/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/GPU_GraphicsCard/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/GPU_Rack/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/GraphicsCard/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/Motherboard/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/PowerSupply/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/Power_Supply/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/textures/Wires/ATTRIBUTION.md
- lpbitcoin/hashdterminal/assets/textures/ATTRIBUTION.md

Blank, excluded by folder (4 outside `_archive` + 4 inside):
- lpbitcoin/bitcoinhub/assets/source/fbx/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/source/fbx/ATTRIBUTION.md
- lpbitcoin/gpurack/assets/source/fbx/_reference_anim/ATTRIBUTION.md
- lpbitcoin/hashdterminal/assets/source/fbx/ATTRIBUTION.md
- lpbitcoin/_archive/advancedgpurack-intake/assets/source/fbx/ATTRIBUTION.md
- lpbitcoin/_archive/advancedgpurack-intake/assets/textures/ATTRIBUTION.md
- lpbitcoin/bitcoinhub/_archive/sketchfab-generic-pc/assets/source/fbx/ATTRIBUTION.md
- lpbitcoin/bitcoinhub/_archive/sketchfab-generic-pc/assets/textures/ATTRIBUTION.md

lptags/tags/ui/fonts/ATTRIBUTION.md carries origin and licence (OFL 1.1, RobotoMono). The licence text file itself is already on develop.
