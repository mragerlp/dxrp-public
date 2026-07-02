# dxrp-staffmenu — UI style parity workspace (Dimmer / upstream review)

**Purpose:** Reference workspace for aligning **LIFEPUNCH lifepunchulx** admin menu visuals with the **DXRP Party Menu / TabMenu** language.  
**Lane:** Style/layout distill only — **no lifepunchulx gameplay code ships in this repo.**

## For Dimmer (quick orientation)

| Role | Path |
|------|------|
| **Style source of truth (this repo)** | `game/Code/UI/HUD/Components/PartyMenu.razor` + `.scss` |
| **Tab/sidebar language** | `game/Code/UI/Menus/TabMenu/TabMenu.razor.scss` |
| **Shared tokens** | `game/Code/UI/styles.scss` |
| **This workspace** | `dxrp-staffmenu/` (spec + diff + handoff — you are here) |
| **Implementation target (read-only)** | LifePunch monorepo — see `paths/CANONICAL_PATHS.md` |

**Branch pin (Party Menu reference):** `bounty/73-party-system` @ `914967a`  
**Issue context:** dxura/dxrp#73 (Party System) — visual language Dimmer approved for `/party` menu.

## What lives here vs what does not

| In `dxrp-staffmenu/` | In `lifepunch` monorepo only |
|----------------------|------------------------------|
| Style spec, path map, diff notes | `StaffMenu.razor` / `.scss` edits |
| Grok distill prompt (when fired) | Permission logic, RPC, aliases |
| Dimmer review checklist | Commits to `adminmenu` addon |

**Law:** lifepunchulx is LIFEPUNCH™ proprietary IP. This folder documents **visual alignment** with DXRP HUD patterns; it does not import or commit proprietary addon code into `dxrp-public`.

## Read order

1. `paths/CANONICAL_PATHS.md` — every path Dimmer and agents need
2. `handoff/STYLE_DIFF.md` — Party Menu vs lifepunchulx side-by-side
3. `STYLE_PARITY_SPEC.md` — owner brief (styling only)
4. `handoff/GROK_DISTILL_PROMPT.md` — when Bloodwave fires Grok
5. `MANIFEST.md` — branch pins + acceptance criteria

## Canonical game paths (mirrored under this folder)

```
dxrp-staffmenu/game/Code/UI/HUD/Components/   → PartyMenu reference (see README there)
dxrp-staffmenu/game/Code/UI/Menus/TabMenu/    → TabMenu reference (see README there)
```

These mirror the real DXRP tree so navigation matches what Dimmer already uses in PR review.
