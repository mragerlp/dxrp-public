# HUD Components — style reference (Party Menu)

**Canonical file (this repo):**

```
../../../../../game/Code/UI/HUD/Components/PartyMenu.razor
../../../../../game/Code/UI/HUD/Components/PartyMenu.razor.scss
```

From repo root: `game/Code/UI/HUD/Components/PartyMenu.*`

---

## Why this path

Dimmer and DXRP upstream reviewers already know **`game/Code/UI/HUD/Components/`** as the HUD component layer. Party Menu is the **primary visual reference** for the lifepunchulx styling pass.

## Related components (same folder)

| Component | Path |
|-----------|------|
| Party HUD | `PartyHud.razor` + `.scss` |
| Party member card | `PartyMemberCard.razor` + `.scss` |
| HUD root | `../HUD.razor` mounts `<PartyMenu/>` |

## Implementation note

**lifepunchulx (`StaffMenu`) does not live here.** It ships from the LifePunch monorepo addon path documented in `dxrp-staffmenu/paths/CANONICAL_PATHS.md`. This folder entry exists so reviewers can navigate the same tree shape Dimmer uses in PR diffs.

See: `dxrp-staffmenu/handoff/STYLE_DIFF.md`
