# Bitcoin mining — session handoff (2026-06-15)

**You do NOT need to restart from scratch tomorrow.** The hacker terminal UI is still the quality bar — bitcoin hub is catching up on world mesh + admin panel, not a greenfield rewrite.

## What was broken (root cause)

| Symptom | Cause |
|---------|--------|
| Building-sized hub + parts on the ground | `Export-BitcoinMinerSteamMachineFbx.py` exported **every** mesh/empty in the blend + applied transforms **before** parenting |
| Black VALVE cubes / missing materials | Giant scale + stale Ophion collider (`30×15.5×20`) on Steam Machine mesh |
| Hub UI black box / vertical text | ScreenPanel/HUD root had no reliable size; negative-margin centering collapsed flex |
| Preview hub NRE on power | `CallerIsOwner` null when no player connection in solo play |

## What was fixed tonight

### World mesh (H1 — major progress)

- **FBX export** now ships only `Steam_Machine` collection; parents `fan`, `front_panel`, `back_body` under `base_body` **before** export (no per-mesh transform apply).
- **`import_scale = 0.152`** in `bitcoin-miner.vmdl` (bridge-measured @ prefab `1,1,1`).
- **Prefab collider** retuned: `32×20×28`, center `0,0,14`.
- **Verified bounds** (flatgrass play): mesh **~32×30×29** vs collider **32×20×28**; ground contact OK.
- Proof screenshot: `addons/docs/proof/2026-06-15-hub/H1-mesh-assembled.png`

### UI (H7 — mockup dashboard pass)

- Hub + CRT roots use **full-viewport overlay** + fixed `.shell` dimensions (StaffMenu pattern).
- `LpHashdPanel` redesigned toward **Bitcoin Mining Dashboard** mockup: sidebar nav, orange hero cards, miners table, server status rail, Wallet/Racks/Settings tabs.
- `LifePunchUiFooter` SCSS → lowercase `.lp-source-footer` (UI-ENGINE-01 fix).
- Preview hub uses `ApplyPoweredState()`; null-safe `CallerIsOwner`.
- Scale audit skips `LpBitcoinPreview*` ghosts.
- Default hub UI scale: **Large**.

### Terminal (T3 — CRT repair pass)

- **Layout:** `.log` scroll (`overflow-y: scroll`, `min-height: 0`); `.crt-frame` flex chain fixed; shell column + footer.
- **Input:** `TextEntry` `@ref` + click-to-focus on log/prompt row; sidebar commands click-to-prefill.
- **Prompt:** `rig@hub>` with amber `#f0a500` accent (gray CRT shell per doctrine).
- **Boot:** Ophion copy removed; click-to-skip splash; faster boot timing.
- **Chrome:** header status row (power / rack count / selection); `LifePunchUiFooter`; default scale **Large**.

### Re-test commands (2 minutes)

```text
game.scene → Host Play → wait for cold load
lp_map_flatgrass
lp_bitcoin_spawn_hub          # world mesh — should be ~citizen-waist size, assembled
lp_bitcoin_scale_audit        # expect mesh ~32×30×29
lp_bitcoin_preview_hub        # amber admin dashboard
lp_bitcoin_preview_terminal   # CRT rig@hub>
```

## Phase checklist — honest status

| ID | Status | Notes |
|----|--------|-------|
| **H1** | **~90%** | Scale + assembly fixed; owner citizen comparison + sign-off pending |
| **H2** | Open | Material slot audit on flatgrass (vmats compile; verify no flat black) |
| **H3–H6** | Open | Power emissive, audio, USE, night shot |
| **H7** | **~80%** | Mockup dashboard pass; owner verify tabs + miners table scroll |
| **H8–H10** | Open | Live metrics, PIN, hero sign-off |
| **T3** | **~80%** | CRT layout/input/boot fixed; owner verify `lp_bitcoin_preview_terminal` |
| **Phase B+** | Partial | Terminal UI repaired; rack/world terminal phases (T1–T2, T4–T6) still open |

## Tomorrow — 30-minute win path (no restart)

1. **Host Play** on `game.scene` — run commands above.
2. If hub mesh looks right next to citizen → check **H1** in polish checklist.
3. If `lp_bitcoin_preview_hub` dashboard renders → check **H7** partial.
4. If `lp_bitcoin_preview_terminal` scrolls + accepts typed commands → check **T3** partial.
5. Next single task: **H2** material audit OR **H4** power on/off screenshot pair.
6. ModelDoc only when ready: **star-add** `fanAction` animation (BITCOINMINING-05).

## If something regresses

```powershell
# Re-export FBX from repo blend (fixed pipeline)
powershell -File lifepunchaddons\scripts\Export-BitcoinMinerSteamMachineFbx.ps1

# Sync + compile in editor
powershell -File lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon bitcoinmining
# ModelDoc: compile bitcoin-miner.vmdl
powershell -File lifepunch\scripts\Pull-DxrpCompiledAssetsToRepo.ps1 -Addon bitcoinmining
```

**Do not** revert to Ophion FBX or `import_scale 0.77` — that was a different mesh.

## Reference quality (unchanged)

Hacker `HackerServerRackMenu` + advanced terminal = shipped UI bar. Bitcoin hub admin should feel like that family (bottom-weighted ops chrome, amber not green) — world mesh is now on track to match.
