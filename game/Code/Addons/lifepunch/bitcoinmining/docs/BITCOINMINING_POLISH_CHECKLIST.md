# lifepunch.bitcoin — polish checklist (hub → terminal → rack)

**Package:** `lifepunchbitcoin` · **s&box:** `lifepunch.bitcoin` · **repo:** `bitcoinmining`  
**Order:** Bitcoin Miner Hub → Bitcoin Terminal → GPU Rack → *(later)* Hacker Server Rack → Hacker Terminal  
**Law:** one item at a time → play proof on flatgrass → owner sign-off → check box → next.

**Production gate (read first every session):** `addons/docs/ACTIVE_WORKSTREAM.md`  
**Production laws:** `addons/docs/CYBER_REFERENCE_LAWS.md`  
**Bible (on sign-off):** `addons/docs/BITCOIN_REFERENCE_IMPLEMENTATION.md`  
**Parking lot (blocked ideas):** `addons/docs/BACKLOG_PARKING_LOT.md`  
**Owner tracker (plain text, second screen):** `addons/docs/OWNER_PROGRESS_TRACKER.txt`

**Visual canon:** `lifepunchaddons/docs/briefs/BITCOIN_OPHION_VISUAL_PASS_BRIEF.md`  
**Play law:** `BITCOINMINING_PLAYTEST.md` §0 — always from **`scenes/game.scene`**, never prefab tabs.

---

## Session setup (every sitting)

```text
game.scene → Play (wait 2–5 min cold) → lp_authorize <token> (owner manual) → lp_map_flatgrass → lp_bitcoin_spawn_kit
```

| Purpose | Command |
|---------|---------|
| Hub only (scale doc) | `lp_bitcoin_spawn_hub` |
| Hub UI only | `lp_bitcoin_preview_hub` |
| Terminal UI only | `lp_bitcoin_preview_terminal` |
| Hub scale audit log | `lp_bitcoin_scale_audit` |

**Sync after asset/code changes:**

```powershell
powershell -File lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon bitcoinmining
powershell -File lifepunch\scripts\Pull-DxrpCompiledAssetsToRepo.ps1 -Addon bitcoinmining
```

---

## Progress summary

| Phase | Scope | Status |
|-------|--------|--------|
| **A** | Bitcoin Miner Hub (Steam Machine) | **In progress** — H1 mesh assembly + scale verified |
| **B** | Bitcoin Terminal (CRT) | Not started |
| **C** | GPU Rack (small + large) | Not started |
| **D** | Hacker lane (deferred) | Not started |

**Last updated:** 2026-06-15 (H1 export pipeline + `import_scale 0.152` + UI overlay pass — see `BITCOIN_SESSION_HANDOFF.md`)

---

## Phase A — Bitcoin Miner Hub (Ophion)

*Room story first: silhouette → materials → power state → USE → admin UI.*

### Phase A sign-off gate (hard — no Phase B until complete)

**DONE WHEN (all required):**

```text
☐ Correct scale          (H1)
☐ Correct collider       (H1, H6)
☐ Idle state readable    (H4)
☐ Active state readable  (H4)
☐ USE feedback readable  (H6)
☐ Night visibility       (H4 — night screenshot)
☐ Branding verified      (H7 — BTC mark + amber HASHD)
☐ Screenshot proof       (H10 — proof package below)
```

**Proof package (status = NOT DONE if any missing):**

```text
- Screenshot #1: Day (flatgrass, citizen scale)
- Screenshot #2: Night (power on/off readable)
- Screenshot #3: USE state (hub interaction)
- Screenshot #4: Citizen comparison (waist/chest vs hub)
- 30-second gameplay clip (place → power → UI)
```

| ID | Done | Task | Done when | Proof |
|----|:----:|------|-----------|-------|
| **H1** | ☐ | **Scale + ground contact** — `bitcoin-miner.vmdl` import `0.77`, translation Z tuned for `mins.z ≈ 0`; prefab root `1,1,1`; `BoxCollider` matches mesh | Citizen ~waist-to-chest vs hub; feet on ground on flatgrass; not dollhouse | `lp_bitcoin_spawn_hub` + `lp_bitcoin_scale_audit` + orbit screenshot |
| **H2** | ☐ | **Material remap audit** — all Ophion FBX slots mapped (`AsusRog`, wire weave, plates, acrylic — not flat chassis) | GPU face, cables, glass read separately; no checkerboard spam | ModelDoc compile clean; flatgrass 10m screenshot |
| **H3** | ☐ | **Acrylic / glass sides** — `Side Panels` → acrylic vmat (translucent) | Sides read as glass at angle | Powered hub screenshot |
| **H4** | ☐ | **Powered vs off (world)** — emissive/audio reflect `IsPowered` | Off = dark; on = unmistakable live state | Toggle power in UI; before/after screenshot |
| **H5** | ☐ | **Hub audio baseline** — `hub-startup`, `hub-fan-loop`, `hub-fan-down` wired or nulled cleanly | No missing `sound_c` spam on power cycle | `read_log` filter `bitcoin-miner` |
| **H6** | ☐ | **Prefab integrity** — `bitcoin-miner.prefab`: `LpBitcoinHubEntity`, `ModelRenderer`, tuned `BoxCollider`, `Rigidbody` | USE works; no perf tank; compile clean | USE hub in play |
| **H7** | ☐ | **HASHD admin panel (UI)** — `LpHashdPanel`: amber ops (`#f0a500` / `#12100c`), Overview/Racks/Settings, scale S/M/L/XL, red X | No hacker green / gov cyan bleed | `lp_bitcoin_preview_hub` |
| **H8** | ☐ | **Hub power + rack link UI** — Overview metrics live; Racks tab; power off stops mining | Linked count matches spawned racks | Full kit → toggle power |
| **H9** | ☐ | **PIN gatekeeper** — graphical PIN / Secure Boot per `BITCOINMINING_TERMINAL_DOCTRINE.md` or explicit defer doc | Two-player abuse test passes when wired | Doctrine § PIN flow |
| **H10** | ☐ | **Hub-only hero sign-off** | Owner visual OK on silhouette + materials + power + UI | `lp_bitcoin_spawn_hub` screenshot |

### H1 session notes (2026-06-15)

- **Found:** old FBX export shipped camera/lights + unparented meshes → building-scale bounds (485u) + fan/panel on ground.
- **Fixed:** `Export-BitcoinMinerSteamMachineFbx.py` — `Steam_Machine` only, parent-under-`base_body`, no pre-parent transform apply.
- **Fixed:** `import_scale` `1.0` → **`0.152`**; collider `32×20×28` center `0,0,14`.
- **Verified:** `lp_bitcoin_scale_audit` mesh **31.97×30.4×29.36** @ flatgrass; proof `addons/docs/proof/2026-06-15-hub/H1-mesh-assembled.png`.
- **Pending owner sign-off:** citizen waist comparison + night shot (H4).

### Phase A — out of scope (this pass)

- vmdl fan/LED animation (BITCOINMINING-05)
- Encryption tiers / market purchase row
- `lifepunch_rgb_fan_led.shader` (BITCOINMINING-03) — post–hub sign-off

---

## Phase B — Bitcoin Terminal (CRT / rig0)

*Control surface — mine/stop/sell commands live here, not on hub.*

| ID | Done | Task | Done when | Proof |
|----|:----:|------|-----------|-------|
| **T1** | ☐ | **World mesh** — `bitcoin-terminal.vmdl` compiles; monitor vmats remapped | CRT visible; no ERROR texture on monitor | Spawn kit; walk to terminal |
| **T2** | ☐ | **Terminal placement** — prefab position/aim on kit reads intentional | USE range feels right | USE from player POV |
| **T3** | ☐ | **CRT UI** — `LpBitcoinTerminalPanel`: amber `rig@hub>` prompt, typed commands, ESC, cog → scale | Matches ops CRT theme; no SCSS gradients | `lp_bitcoin_preview_terminal` |
| **T4** | ☐ | **Command loop** — `mine` / `stop` / `sell` / `status`; host-validated; hub powered | Mining ticks; sell credits wallet | Full kit → terminal commands |
| **T5** | ☐ | **Hub ↔ terminal authority** — terminal refuses when hub off / wrong owner | Powered-off = blocked | Power off + try `mine` |
| **T6** | ☐ | **Terminal sign-off** | Owner OK on CRT read + command flow | Screenshot + 30s mining clip |

### Phase B — out of scope

- Shipped `hashd`/`mine` ConCmds in publish builds (dev-only per doctrine)

---

## Phase C — GPU Rack (small + large)

*Idle printer — mine/stop/sell only via terminal.*

| ID | Done | Task | Done when | Proof |
|----|:----:|------|-----------|-------|
| **R1** | ☐ | **Small rack mesh** — `gpu-rack.vmdl` five-slot materials; `complex.shader` GPU vmat baseline | No P0 compile errors; emissive readable when mining | `lp_bitcoin_spawn_kit` |
| **R2** | ☐ | **Large rack mesh** — `gpu-rack-stacked.vmdl` remaps clean; 2× yield in code | Stacked reads taller/denser | Side-by-side screenshot |
| **R3** | ☐ | **Collider + perf** — tuned `BoxCollider`; no hull storm; throttled text/RGB | FPS stable after 4-rack spawn | Spawn kit; watch FPS |
| **R4** | ☐ | **Mining visuals** — emissive/screen when `IsMining`; idle when stopped | On/off obvious per rack | `mine` / `stop` on terminal |
| **R5** | ☐ | **Hub link radius** — racks link to hub; unlink beyond range correct | `GetLinkedRacks()` matches layout | Kit spawn layout |
| **R6** | ☐ | **Fan motion** — ModelDoc `power_on`/`power_off` OR remove legacy child-fan spin (BITCOINMINING-01) | One clean path | Power cycle rack |
| **R7** | ☐ | **RGB LED endgame** *(post-P0)* — compile `lifepunch_rgb_fan_led.shader`; restore vmat (BITCOINMINING-03) | Rainbow ramp on START | After R1–R6 signed |
| **R8** | ☐ | **Full kit hero sign-off** | Hub + 3 small + 1 large = criminal-infra fantasy | MCP orbit on flatgrass |

---

## Phase D — Other cyber lanes (deferred)

Full checklists live in **`addons/docs/CYBER_JOBS_POLISH_CHECKLIST.md`**:

| Lane | Phase | Order |
|------|-------|-------|
| Hacker (criminal) | E | Server rack → terminal → advanced rack → advanced terminal |
| Government / FBI (protagonist) | F | Tax miner hub → police terminal |
| Banker | G | Vault hub → teller → security → bank miners |
| Black Market Dealer | H | BTC payment hub → shop terminal |

**Cross-lane rule:** bitcoin = **amber HASHD defense**; hacker = **green offense**; gov = **cyan**; banker = **navy/gold**; dealer = **black shell** — never swap palettes.

---

## Sign-off log

| Date | ID | Owner | Notes |
|------|-----|-------|-------|
| — | H1 | — | Import/collider pass committed in repo; flatgrass proof pending |

---

## How to use this doc

1. Say the next ID in chat (e.g. **H1**, **H2**).
2. Agent does **only that item**.
3. Play proof on flatgrass.
4. Owner replies **OK** or steers.
5. Agent checks the box here and adds a row to **Sign-off log**.
