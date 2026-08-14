# Bitcoin Miner — play-test checklist

**Editor:** `Start-SboxDxrpEditor.ps1` (opens DXRP normally) · **Sync:** `Sync-LifePunchAddonsToDxrp.ps1 -Addon bitcoinmining`

**API key (owner manual):** after Host Play — **owner** runs `lp_authorize <dxrp.net server token>` in console before spawn/proof work (`authorize` is a launch ConVar only: `+authorize` at startup). Agents do not run this or request the token.

---

## 0. Enter play mode (DXRP is DedicatedServerOnly)

### Root cause (confirmed 2026-06-11)

**Play fails when the active tab is a prefab stage** (`Prefab: GPU Rack`, `Prefab: Advanced GPU Rack`, etc.). Bridge reports `sceneName: "gpu-rack"` — that is **not** DXRP play mode. Hosting from there always yields `Unable to create a lobby outside of a game`.

**Play works from `scenes/game.scene`** — bridge reports `sceneName: "Game"`, log shows `[Fitter] Fitting thieves.rpdowntown3t` and `Bloodwave has joined the game`.

### Steps (every session)

1. **Close all prefab tabs** (save/discard the `*` on Advanced GPU Rack if prompted).
2. Asset Browser → **scenes** → double-click **`game.scene`** — tab title must say **Game**, not `Prefab: …`.
3. Press **Play** (green arrow). First cold load: wait **2–5 minutes** while downtown compiles; early Stop = `Couldn't load map (A task was canceled.)`.
4. Log/console shows player join + map fit → you're in. **Owner:** `lp_authorize <token>` in console (manual — before agent spawn/proof).
5. Then: `lp_hashd_preview` or `lp_spawn_gpu_rack` / `lp_map_flatgrass` + bitcoin spawn commands.
6. **Start Hosting** is optional and only **after** step 3 succeeds (not from prefab edit mode).
7. Launch alternative: `+authorize` / `Start-SboxDxrpEditor.ps1 -WithAuthorize` (owner still prefers manual console entry each session).

**Stall on cold start:** broken `advanceddrugprocessing` models on disk can spam recompiles — local DXRP install may rename that folder to `advanceddrugprocessing._disabled` (not in `rp.sbproj` Resources).

### Log triage (`D:\Steam\steamapps\common\sbox\logs\sbox-dev.log`)

| Log line | Severity | Cause | Fix (owner/agent) |
|----------|----------|-------|-------------------|
| `lifepunch_rgb_fan_led.shader` + `Feature combo not found` | **P0 block** | Custom GPU vmat shader not compiled | **Fixed baseline:** `gpu-rack-gpu.vmat` → `complex.shader` (BITCOINMINING-03). Endgame: compile shader in editor, restore RGB vmat. |
| `gpu-rack-gpu.vmat_c` / `gpu_basecolor...vtex_c` not found | **P0 block** | Downstream of failed vmat compile | Recompile vmat + vmdl after vmat fix; `Pull-DxrpCompiledAssetsToRepo.ps1`. |
| `Couldn't load map (A task was canceled.)` | **P0 block** | Map load aborted (Stop clicked too soon, prefab tab active, or compile stall) | Open **`scenes/game.scene`** (not prefab); Play once and **wait** 2–5 min; disable broken `advanceddrugprocessing._disabled` folder locally. |
| `Unable to create a lobby outside of a game` | User flow | Start Hosting clicked before Play | Play first, then Start Hosting (§0 above). |
| `ToolsStallMonitor Stall detected` | Watch | Long on-demand recompiles (bitcoinmining vmdl/vmat) | Fix P0 shader; avoid leaving `gpu-rack-test` in startup scene. |
| `FanBlades.00x` / `GPU_Fan_x.00x` incorrect extension | **P0 loop** | Stacked FBX material slots not remapped | **Fixed:** remaps in `gpu-rack-stacked.vmdl` → rack/gpu vmats. |
| `bassm\Desktop\PSU...` content-relative error | Warning | Stacked FBX embeds artist absolute paths | Ignored after remap; PSU uses our `gpu-rack-psu.vmat`. |
| `dark green.vmat_c` / `lime green.vmat_c` on terminal | **P1 loop** | CRT FBX `Lime Green` / `Dark Green` slots | **Fixed:** remap → `bitcoin-terminal-monitor.vmat`. |
| `bitcoin-miner/*.sound_c` not found | **P1 loop** | Prefab refs sounds that do not exist yet | **Fixed:** prefab sound props nulled until owned audio ships. |
| `Skipping texture streaming` (gpu textures) | Watch | Recompile storm settling | Stops after vmat/vmdl `_c` stable; restart editor if it persists >30s. |
| `repeating-linear-gradient` / `linear-gradient` invalid `background-image` | **P1 loop** | s&box UI panel SCSS rejects CSS gradients | **Fixed:** solid colors in `HashdTerminal.razor.scss`, `HackerTerminal.razor.scss`, `HackerServerRackMenu.razor.scss` (no gradients). Run `Validate-SboxRazorScss.ps1` before ship. |
| `Failed to get player inventory` **403** | Expected offline | No portal API token | `lp_authorize <dxrp.net token>` after host play — not required for `lp_spawn_gpu_rack` / hashd UI. |
| `Unable to load prefab improved_atm` (×11) | DXRP map | Map fitting references missing SPL ATM addon | Noise only; downtown still loads. |
| `Couldn't find Input Action called "Pocket"` | DXRP | Pocket bind not in project InputSettings | Ignore for bitcoinmining playtest. |
| `returning error texture` metal036 / door vmdl | DXRP map | Downtown fitting props missing `_c` on cold compile | Cosmetic checkerboard on some doors; map playable. |
| `lp_spawn_gpu_rack: gpu-rack + bitcoin-terminal placed` | **OK** | Dev spawn succeeded | Run `hashd` near rig or `lp_hashd_preview` from play mode. |
| FPS tanks right after `lp_spawn_gpu_rack` | **P0 perf** | `ModelCollider` on full `gpu-rack*.vmdl` with `PhysicsHullFromRender` / `HullPerElement` + per-frame `TextRenderer` rebuild | **Fixed:** prefabs use tuned `BoxCollider` + `StartAsleep`; `GpuRackEntity` throttles screen text + RGB attribute writes. |

### SCSS triage (all `.razor.scss` — Cornerman audit 2026-06-13)

| File | Gradients | Undefined vars | `transform:` (non text-) | Red verify |
|------|-----------|----------------|--------------------------|------------|
| `adminmenu/StaffMenu.razor.scss` | 0 | 0 | 0 | `lifepunchulx` open — no gradient log spam |
| `bitcoinmining/HashdTerminal.razor.scss` | 0 | 0 | 1 (`scale(0.98)` active state) | `lp_hashd_preview` — UI paints |
| `hackerjob/HackerTerminal.razor.scss` | 0 | 0 | 0 | `hacker` terminal open — no compile fail |
| `hackerjob/HackerServerRackMenu.razor.scss` | 0 | 0 | 0 | rack menu if wired |
| `visiblepocket/VisiblePocketHud.razor.scss` | 0 | 0 | 1 (`translateX(-50%)` centering) | `lp_pocket_preview` — bar centered |

Validator: `lifepunchaddons/scripts/Validate-SboxRazorScss.ps1` — run before ship; 0 forbidden gradients required.

**MCP check:** `read_log` filter `bitcoinmining`, `error`, `spawn`, `403`. **Do not** leave test rigs in `game.scene` — use `lp_spawn_gpu_rack` in play mode instead.

## 1. Physical Bitcoin Miner hub (real-time DXRP)

**Market tab:** not live yet — `addons.json` has no `dxrpAddonId` / server market row. Use dev spawn until portal ship.

**Tonight’s path (VENGEANCE / DXRP play mode):**

1. Sync code + assets into DXRP:
   ```powershell
   powershell -File lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon bitcoinmining
   ```
2. Open **`scenes/game.scene`** (not a prefab tab) → **Play** → wait for map fit.
3. Console:
   ```text
   lp_spawn_bitcoin_miner_hub
   ```
   Spawns **Ophion hub** (owner = your Steam ID) + 3 small racks + 1 large rack nearby.
4. Optional skip boot: `lp_hub_power 1`
5. **USE** the Ophion hub (E / interact) → click **SECURE BOOT** → numpad PIN (owner, twice) → power on → `rig0>`.

**Ghost-console PIN preview (instant):**

```text
lp_hashd_pin_preview
lp_hashd_pin_preview unlock
lp_hashd_pin_preview setup
lp_hashd_pin_preview blocked
```

Shows ghost telemetry + clickable Secure Boot CTA → graphical numpad (no `auth>` typing).

**Two-player PIN abuse test:** second client walks to the same hub before owner sets PIN → should see **ACCESS LOCKED**, not PIN setup. After owner sets PIN, second client only gets past gate with the PIN (or stays locked).

**When Market ships:** buying **Bitcoin Miner Hub** sets `Owner` to buyer Steam ID — same PIN rules apply.

---

## 2. See the hashd console (fastest)

Enter play mode (hosted), then:

```text
lp_hashd_preview
```

Spawns small rack + CRT kit and **opens the hashd overlay immediately**. The CRT world mesh can still show ERROR until `bitcoin-terminal.vmdl` is compiled in ModelDoc — the console UI does not depend on the CRT mesh.

**Three entities (owner canon):** Bitcoin Terminal = control · GPU Rack = small · Advanced GPU Rack = stacked (2× yield). See `docs/reference/BITCOINMINING_THREE_ENTITY_ARCH.md`.

---

## 3. Spawn a rig

**Fast (dev console):**

```text
lp_spawn_gpu_rack
```

Spawns **GPU Rack** (`gpu-rack.prefab`) + **Bitcoin Terminal** (`bitcoin-terminal.prefab`) as linked pair. Works with a DXRP pawn **or** editor camera.

```text
lp_spawn_bitcoin_terminal
lp_spawn_advanced_gpu_rack
lp_spawn_bitcoinmining_full_kit
```

CRT only · stacked rack only · all three entities (terminal + small + advanced).

**Manual:** Asset Browser → DXRP → `addons/lifepunch/bitcoinmining/entities/gpu-rack/gpu-rack.prefab` → drag into map → **save scene**.

**Verify scene (dev console):**

```text
lp_gpu_rack_count
lp_map_flatgrass
lp_bitcoinmining_scale_audit
```

Logs `BITCOINMINING_TEST rigs=N pos=...` for every `GpuRackEntity` in the active scene.

---

## 4. Open terminal

Within **8 m horizontal / 4 m vertical** of the rig:

| Input | Action |
|-------|--------|
| `hashd` | Open LIFEPUNCH hashd CLI |
| `mine` | Same as `hashd` |
| `hashd close` | Close terminal |
| **Use key** on rig | `IPressable` → same as `hashd` |

---

## 4. CLI smoke test

```text
help
status
info
mining start
status
mining stop
bitcoin
upgrade
clear
```

**Scroll:** mouse-wheel the terminal output area (history keeps up to 200 lines).

**UI:** **HASHD RIG CONTROL** — amber telemetry rail (left) + command log (right). Rail **START/STOP** toggles mining without typing.

**Upgrade panel:** `upgrade` or `menu` opens **INSTALL** buttons for CPU Clock and CPU Cores (still accepts `upgrade cpu` / `upgrade cores`).

Expected: green terminal UI, LCD on `lcd_screen` (on `bitcoin-terminal.prefab`), fans spin while mining (placeholder fan GOs until vmdl anim ships).

**LCD tune:** nudge `lcd_screen` transform on `entities/bitcoin-terminal/bitcoin-terminal.prefab` — not on the rack prefab.

**World scale** — see `addons/docs/MODEL_SCALE_DOCTRINE.md`. **Prefab root = `1,1,1` only.** Tune `import_scale` in vmdl until **mesh bounds** match intent; `BoxCollider.Scale` is the gameplay footprint target.

**Visual size hierarchy (what players should see):**

```text
Advanced GPU Rack  >>  GPU Rack (standing crypto farm frame)  >>  Bitcoin Miner hub (Ophion gaming PC)
   stacked farm         single open-frame mining rig              hashd control tower
```

| Entity | Prefab | Root scale | BoxCollider (gameplay hammer) | vmdl `import_scale` (repo) | Mesh bounds @ flatgrass |
|--------|--------|------------|-------------------------------|----------------------------|-------------------------|
| **GPU Rack** | `gpurack/gpu-rack.prefab` | 1,1,1 | **25 × 20 × 36** | **0.395** | translation Z **21.382** · mesh bottom on ground |
| **Advanced GPU Rack** | `advancedgpurack/advanced-gpu-rack.prefab` | 1,1,1 | ~52 × 27 × **47** | **0.72** | ~1.8× single scale · translation Z **27.682** |
| **Bitcoin Miner hub** | `bitcoinminer/bitcoin-miner.prefab` | 1,1,1 | **10 × 8 × 15** | **0.385** + **Z 13.3** trans, rot 0 | Re-verify after vmdl recompile (was wrong @ trans 0 + pitch 90°) |
| **Bitcoin Terminal (CRT)** | `bitcoin-terminal/bitcoin-terminal.prefab` | 1,1,1 | **10 × 4 × 9** | **0.0272** | ~20×17×20 mesh @ flatgrass · **`import_rotation [-90,0,0]`** · **HP 100** |

**Verified read:** single GPU rack is a **standing crypto farm frame** (Sketchfab ref) — taller than the Ophion hub, smaller than the stacked farm unit.

**Scale audit:** `lp_map_flatgrass` → Play → `lp_bitcoinmining_scale_audit` (logs `BITCOINMINING_SCALE_AUDIT` lines).

**If something looks giant again:** delete old instances, Stop/Play, respawn `lp_spawn_bitcoin_miner_hub` / `lp_spawn_gpu_rack`. Never fudge prefab root scale (no `0.41` / `0.5`).

---

## 5. Known gaps (not blockers for Phase 1)

- Sounds missing (`sounds/bitcoin-miner/` — hum disabled on prefab)
- Power anim not wired (`gpu-rack-anim.fbx` → `power_on` / `power_off`)
- Economy RPCs need host + wallet — verify sell/upgrade on live DXRP server, not editor-only stub
- Remove `BitcoinMiningDevSpawn.cs` before portal publish

**Publish:** `*DevSpawn.cs` is excluded automatically by `prepare-publish.ps1` (local playtest keeps ConCmds).

---

## 6. After editor tweaks

Copy compiled outputs from DXRP `game/Assets/addons/lifepunch/bitcoinmining/` back into monorepo `lifepunchaddons/Assets/...`.
