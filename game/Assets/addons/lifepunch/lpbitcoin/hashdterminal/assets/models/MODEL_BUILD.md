# HASHD Terminal — ModelDoc foundation pass

**Source:** `assets/source/fbx/hashdterminal.fbx`  
**vmdl (compile `_c` here):** **`assets/models/hashdterminal.vmdl`**  
**prefab:** `assets/entities/hashdterminal.prefab`  
**Phase:** B — Terminal (Model Foundation)

**Retired:** `hashd-terminal.vmdl`, `hashd-terminal.prefab` — do not compile or publish.

## ModelDoc settings (v1)

| Field | Value |
|-------|--------|
| import_scale | **0.286** (PC.fbx units — ~legacy terminal height vs citizen) |
| import_rotation | 0,0,0 |
| align | Center / Center / Bottom |
| Materials | Monitor + Keyboard_mause remaps (audit extra slots in ModelDoc) |
| CRT glow | `hashd-terminal-monitor.vmat` — amber self-illum baseline |

## Textures (4K TGA — ship lowercase)

**Source drop:** `%USERPROFILE%\Downloads\textures\textures4k` (pack originals).

Import into staging:

```powershell
powershell -File lifepunchaddons\scripts\Import-HashdTerminalTextures.ps1
```

vmats reference split **lowercase `.tga`** maps under `assets/textures/`. Height maps are skipped (not wired). **Compile in ModelDoc** — do not rely on runtime on-demand compile for 4K maps.

If console loops after a sync: stop play → `Clean-HashdTerminalAssetCache.ps1 -IncludeRepo` → sync → recompile vmats + vmdl in ModelDoc → copy `_c` to repo.

### `Skipping texture streaming: … reloaded while in the streaming list`

One-shot **RenderSystem** warnings on spawn — not the old vmat recompile crash loop. Cause: stale compiled `_c` / `.generated.vtex*` still referencing **PNG-era** bakes (`*_png_*`) while live vmats point at **TGA**. Fix: stop play, run `Clean-HashdTerminalAssetCache.ps1 -IncludeRepo`, recompile monitor + keyboard vmats then vmdl, copy fresh `_c` to repo. After a clean compile, generated vtex names should be `*_tga_*.generated.vtex_c`. A single editor restart clears any in-memory streaming list leftovers.

## ModelDoc material editor warning

Saving vmats from the **Material Editor UI** can rewrite the file as `// THIS FILE IS AUTO-GENERATED` and replace your TGA paths with `materials/default/*` — keyboard/mouse will look flat gray while monitor may still look OK from stale `_c`.

**Law:** edit `.vmat` as text in repo (or Import script), **sync**, then **Compile** in ModelDoc — do **not** Save from the material Variables panel unless paths still show `hashdterminal/assets/textures/*.tga`.

## Compile order

1. Open **`hashdterminal.vmdl`** in ModelDoc
2. Recompile vmats first, then vmdl
3. Fix slot remaps if ModelDoc shows unmapped materials
4. Pull compiled output:

```powershell
powershell -File lifepunch\scripts\Pull-DxrpCompiledAssetsToRepo.ps1 -Addon lpbitcoin
```

5. Scene proof: `_dev/scenes/lifepunch-modeldoc.scene`

## Sign-off gate (Phase B)

- [ ] Compiles with zero ERROR
- [ ] Monitor glow readable at night
- [ ] Scale vs hub + citizen reference
- [ ] Owner OK before prefab wire
