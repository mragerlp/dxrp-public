# Grok distill prompt — lifepunchulx style parity

**Route:** `GROK REQUIRED` (SCSS/layout distill)  
**Fire when:** Bloodwave says GO + this file is pinned on branch  
**Do not commit** monorepo changes from Grok output without owner GO.

---

## Paste to Grok

```text
TASK: LIFEPUNCH lifepunchulx UI style parity (styling ONLY — no behavior changes)

You are distilling a visual alignment pass. Read the reference and target paths below.
Output: a concrete SCSS change plan (selector-by-selector), not full rewritten files unless asked.

REFERENCE (DXRP — style source of truth):
- dxrp-public @ bounty/73-party-system @ 914967a
- game/Code/UI/HUD/Components/PartyMenu.razor.scss
- game/Code/UI/Menus/TabMenu/TabMenu.razor.scss
- game/Code/UI/styles.scss ($color-primary #191919, $color-accent #7170e6, $rounding 2px)

TARGET (LifePunch monorepo — read-only unless GO):
- lifepunch/addons/Code/Addons/lifepunch/adminmenu/StaffMenu.razor.scss
- lifepunch/addons/Code/Addons/lifepunch/adminmenu/StaffMenu.razor (layout classes only)

ALREADY ANALYZED DIFF (use as starting point):
- dxrp-public/dxrp-staffmenu/handoff/STYLE_DIFF.md

RULES:
- Preserve all StaffMenuHost / StaffMenuActions / permission / RPC behavior
- No CSS linear-gradient on panels (s&box rejects them)
- Keep .material-icons font-family fix under root
- Do not add features; do not rename command aliases
- Prefer SCSS-only pass first; flag Razor DOM changes separately

DELIVER:
1. Token remap table (ULX vars → DXRP Party tokens)
2. Top 10 selector patches (before/after SCSS snippets)
3. Player-row .selected fix (currently solid blue — too loud vs Party)
4. Tab strip vs sidebar recommendation with effort estimate (S/M/L)
5. ulx-v2 chrome simplification list (glow, badge, pill chips)
6. Risk list (what could break clicks/layout if Razor moves)
7. Red verify checklist (Sync adminmenu, staffmenu in play, no gradient logs)

Label claims: VERIFIED (from STYLE_DIFF.md) vs PROPOSED (your suggestions).
```

---

## After Grok returns

1. Bloodwave reviews accent color + sidebar vs tabs decision
2. Cornerman or Red applies SCSS on monorepo branch
3. Red proves on VENGEANCE — screenshot for Dimmer if upstream visibility needed
4. Dimmer uses `handoff/DIMMER_REVIEW_CHECKLIST.md` for visual consistency lens only (no DXRP code change required unless Party Menu itself needs tweak)
