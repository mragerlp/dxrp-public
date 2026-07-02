# Canonical paths — dxrp-staffmenu / Dimmer map

All paths relative to **`dxrp-public` repo root** unless noted.

---

## DXRP upstream (this repo) — style references

| Asset | Path | Notes |
|-------|------|-------|
| **Party Menu (primary visual reference)** | `game/Code/UI/HUD/Components/PartyMenu.razor` | Sidebar nav, title bar, roster rows, buttons |
| Party Menu styles | `game/Code/UI/HUD/Components/PartyMenu.razor.scss` | Dimmer spacing comments inline |
| **TabMenu (sidebar/tab language)** | `game/Code/UI/Menus/TabMenu/TabMenu.razor.scss` | `.menuButton`, `.active-btn`, opacity model |
| Shared UI tokens | `game/Code/UI/styles.scss` | `$color-primary`, `$color-accent`, `$rounding`, `$space-*`, `$text-*` |
| HUD mount | `game/Code/UI/HUD/HUD.razor` | `<PartyMenu/>` sibling context |
| Party commands | `game/Code/Chat/Commands/PartyCommand.cs` | `/party` opens menu — behavior reference only |

**Pinned branch:** `bounty/73-party-system`  
**Pinned commit:** `914967a` — `fix(party): Alt-gated HUD drag + collapse, equal roster card widths (#73)`

---

## LifePunch monorepo (read-only implementation target)

| Asset | Path (in `mragerlp/lifepunch`) | Notes |
|-------|--------------------------------|-------|
| **lifepunchulx UI (edit target)** | `lifepunch/addons/Code/Addons/lifepunch/adminmenu/StaffMenu.razor` | Layout/classnames only in style pass |
| Staff menu styles | `lifepunch/addons/Code/Addons/lifepunch/adminmenu/StaffMenu.razor.scss` | Primary SCSS work |
| Host / actions (do not restyle logic) | `StaffMenuHost.cs`, `StaffMenuActions.cs`, `StaffMenuBridgeService.cs` | Behavior frozen |
| Shared LP footer import | `lifepunch/addons/Code/Addons/lifepunch/LifePunchUiFooter.razor.scss` | May tune import only |
| Validate | `lifepunch/addons/scripts/validate-layout.ps1` | Pre-commit gate |
| Sync to editor | `lifepunch/scripts/Sync-LifePunchAddonsToDxrp.ps1 -Addon adminmenu` | Red/VENGEANCE only |
| Prior art brief | `lifepunch/addons/docs/briefs/CORNERMAN_STAFF_MENU_TASK.md` | Gradient removal precedent |
| DXRP party manifest | `lifepunch/docs/handoff/dxrp/DXRP_73_BRANCH_MANIFEST.txt` | Read-only distill input |

**Active branch (monorepo):** `checkpoint-lpbitcoin-pre-sleep-20260701` (or owner-assigned feature branch)

---

## This workspace (dxrp-staffmenu)

| File | Purpose |
|------|---------|
| `dxrp-staffmenu/README.md` | Dimmer entry |
| `dxrp-staffmenu/paths/CANONICAL_PATHS.md` | This file |
| `dxrp-staffmenu/STYLE_PARITY_SPEC.md` | Owner styling brief |
| `dxrp-staffmenu/handoff/STYLE_DIFF.md` | Concrete diff notes |
| `dxrp-staffmenu/handoff/GROK_DISTILL_PROMPT.md` | Grok route prompt |
| `dxrp-staffmenu/handoff/DIMMER_REVIEW_CHECKLIST.md` | Upstream review lens |
| `dxrp-staffmenu/game/Code/UI/HUD/Components/README.md` | Pointer → real PartyMenu |
| `dxrp-staffmenu/game/Code/UI/Menus/TabMenu/README.md` | Pointer → real TabMenu |

---

## s&box / editor paths (Red only — not committed)

| Machine | DXRP editor checkout |
|---------|----------------------|
| VENGEANCE | `D:\Steam\steamapps\common\sbox\dxrp` |
| Sync source | LifePunch monorepo → DXRP via `Sync-LifePunchAddonsToDxrp.ps1` |

**Proof command in play:** `staffmenu` / `adminmenu` on flatgrass — screenshot from Red only.
