# TabMenu — style reference (sidebar / tab language)

**Canonical file (this repo):**

```
../../../../../game/Code/UI/Menus/TabMenu/TabMenu.razor.scss
```

From repo root: `game/Code/UI/Menus/TabMenu/TabMenu.razor.scss`

---

## Why this path

Party Menu sidebar navigation **mirrors TabMenu** explicitly (see comments in `PartyMenu.razor.scss` lines 65–68):

- `.menuButton` — opacity 0.7 default
- `.active-btn` — opacity 1 + `rgba($color-accent, 0.05–0.08)` wash
- No per-row dividers; left-aligned icon + label

lifepunchulx horizontal tabs should adopt this **state language** even if layout stays horizontal.

## Shared tokens

Import chain: `@import "../../styles.scss"` — tokens at `game/Code/UI/styles.scss`

See: `dxrp-staffmenu/handoff/STYLE_DIFF.md` § Selected / hover states
