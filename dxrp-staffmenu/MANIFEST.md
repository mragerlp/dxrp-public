# dxrp-staffmenu manifest

| Field | Value |
|-------|--------|
| **Created** | 2026-07-02 |
| **Owner** | Bloodwave / LIFEPUNCH™ |
| **Upstream repo** | `mragerlp/dxrp-public` |
| **Party reference branch** | `bounty/73-party-system` |
| **Party reference commit** | `914967a` |
| **Monorepo target** | `mragerlp/lifepunch` → `lifepunch/addons/Code/Addons/lifepunch/adminmenu/` |
| **Task** | lifepunchulx visual parity with Party Menu / TabMenu |
| **Lane** | Style only — proprietary implementation stays in monorepo |

## Issue / review context

- **dxura/dxrp#73** — Party System (Dimmer-reviewed HUD)
- lifepunchulx is **not** an upstream DXRP ship item — Dimmer review is **visual consistency**, not merge to `dxura/dxrp:develop`

## Acceptance (from owner brief)

- [ ] Functional admin menu unchanged (permissions, actions, aliases)
- [ ] Visual language aligned with Party Menu direction
- [ ] No proprietary LP code committed into `dxrp-public` game tree
- [ ] Red flatgrass proof captured before portal push
- [ ] Commits: `mragerlp` author, no AI trailers, Bloodwave GO

## Folder layout

```
dxrp-staffmenu/
├── README.md
├── MANIFEST.md
├── STYLE_PARITY_SPEC.md
├── paths/
│   └── CANONICAL_PATHS.md
├── handoff/
│   ├── STYLE_DIFF.md
│   ├── GROK_DISTILL_PROMPT.md
│   └── DIMMER_REVIEW_CHECKLIST.md
└── game/Code/UI/          ← mirrors DXRP tree for Dimmer navigation
    ├── HUD/Components/README.md
    └── Menus/TabMenu/README.md
```
