# lifepunch-assets - layout and manifest (prep only)

Status: PREP. `mragerlp/lifepunch-assets` does not exist yet. The owner creates it. Nothing is pushed there until it exists and the owner approves the first push. This page and `tools/packaging/New-AssetRepoManifest.ps1` are the prep.

## Why a second repo

Weapon assets live in a separate private LFS repo, and the fork (`mragerlp/dxrp-public`, public) stays code plus small assets. The fork is the single source for what the workbench compiles. The asset repo holds what the fork does not carry:

- `lpweapons/`: the whole `game/Assets/addons/lifepunch/lpweapons` tree.
- `lpbitcoin/`: the lpbitcoin files the fork excludes. These are any folder named `_archive`, `audit`, `source` or `provenance`, plus any file over 50,000,000 B. This is the CP2 rule; see `CP2_SCREENS.md` on the CP2 branch.

## Layout

Paths are relative to `game/Assets/addons/lifepunch/`. The repo mirrors that relative tree, so a file's repo path is its workbench path minus that prefix:

```
lifepunch-assets/
  README.md                 what the repo is, who owns it, the never-list rule (no names)
  .gitattributes            LFS patterns (below)
  MANIFEST.txt              one row per file: repo_path, bytes, sha256, licence
  lpweapons/<weapon>/...    admingun1 ak47 aks74u aks74ucovert ar15 deserteagle glock m1911 m870 mp7a1 sr25 tacticalshotgun
  lpbitcoin/<unit>/...      _archive audit bitcoinhub gpurack hashdterminal (excluded set only)
```

## MANIFEST.txt

The generator writes the file as tab-separated rows, LF line endings, UTF-8 without BOM:

`repo_path <TAB> bytes <TAB> sha256 <TAB> licence`

The licence column comes from the nearest `ATTRIBUTION*` / `LICENSE*` / `PROVENANCE*` file walking up to the unit root:
- If that file has a table row naming this file, its Licence column is used.
- If the file is a "left blank" stub, the value is `BLANK`.
- If the file exists but has no row for this name, the value is `SEE <path>`.
- If no such file exists, the value is `NONE`.

ATTRIBUTION and MANIFEST content authored by others is never edited. The licence column only reports what those files say.

Never-list: files named in a local never-list file (`-NeverListPath`, kept outside every repo) are not opened, not hashed and not named. Each becomes a `WITHHELD-<set>-<n>` row with an empty size and hash. AGENTS.md: the sr25 AD source pair is sole copy, and is never touched, opened, listed or compared. Hashing means opening the file, so the pair is withheld without a sha, by that house rule.

## Dry-run (2026-10-17, live workbench tree, read only)

```
lpweapons: 1657 files, 2,962,580,247 B (2,962.6 MB); LFS-eligible 229 files 2,147,791,349 B; withheld 2
lpbitcoin-excl: 144 files, 325,469,523 B (325.5 MB); LFS-eligible 21 files 302,860,955 B; withheld 0
Licence values: NONE=1210, SEE=430, BLANK=140, Unverified=14, CC=7
Over 50000000 B: 13
LFS extensions with a file over 1048576 B: .blend .fbx .png .tga .zip
  .fbx: 56 files, 30 over 1048576 B
  .blend: 3 files, 2 over 1048576 B
  .zip: 4 files, 4 over 1048576 B
  .png: 361 files, 209 over 1048576 B
  .tga: 5 files, 5 over 1048576 B
  (.psd: 0 files)
```

Top level, in MB: lpweapons glock 823.9, mp7a1 896.8, ar15 461.5, sr25 213.9, aks74ucovert 188.0, deserteagle 127.1, aks74u 86.6, m870 66.9, ak47 46.0, tacticalshotgun 43.0, admingun1 5.0, m1911 3.7. lpbitcoin hashdterminal 252.0, _archive 33.8, bitcoinhub 30.5, gpurack 9.2, audit 0.0.

13 files are over 50,000,000 B. The largest is `lpweapons/mp7a1/provenance/mp7a1.zip` at 296,561,087 B. All 13 match an LFS extension. GitHub LFS takes files of this size; the plain-git limit of 100 MB does not apply under LFS.

## LFS patterns (proposed .gitattributes)

```
*.blend filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.zip filter=lfs diff=lfs merge=lfs -text
```

`.gitattributes` cannot match on size. The patterns cover every file of these extensions, including the ones under 1 MB (152 .png, 26 .fbx, 1 .blend). That is the simple and safe default. Add `*.psd` when the first .psd arrives.

## Running the generator

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\packaging\New-AssetRepoManifest.ps1 `
  -GameRoot D:\Cavelux\dxrp-workbench\game -OutDir <a folder outside the game tree> -NeverListPath <local never-list>
```

It writes `MANIFEST.txt`, `gitattributes.proposed` and `DRYRUN_SUMMARY.txt` to `-OutDir`, and refuses an `-OutDir` inside `-GameRoot`. It never copies, moves, deletes or runs git. The full dry-run MANIFEST is not committed here, because this fork is public. It lives with the local release record and belongs in the private repo.

## Populating (after the owner approves, not before)

1. The owner creates `mragerlp/lifepunch-assets` (private) and enables LFS.
2. Clone it; commit `.gitattributes` first, so every later add goes through LFS.
3. Re-run the generator (a fresh sense; the dry-run above is a photograph of 2026-10-17).
4. Copy by explicit path from the MANIFEST rows. Re-hash each copy against its row.
5. Commit `MANIFEST.txt` with the files. Withheld rows stay withheld.
