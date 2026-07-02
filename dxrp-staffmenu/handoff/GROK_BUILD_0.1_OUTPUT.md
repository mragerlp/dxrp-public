# Grok Build 0.1 distill output — lifepunchulx style parity

| Field | Value |
|-------|--------|
| **Model** | `grok-build-0.1` |
| **Response ID** | `ecdde515-7937-9ae3-8c50-89f54410455f` |
| **Generated** | 2026-07-02 |
| **Task** | SCSS-only parity with PartyMenu / TabMenu |

## Agent correction note (before applying)

Grok used generic selectors (`.staff-menu`, `.player-name`). **Real targets in monorepo:**

| Grok wrote | Actual selector in `StaffMenu.razor.scss` |
|------------|---------------------------------------------|
| `.staff-menu` | `.lifepunchulx` + `.window` |
| `.player-name` | `.player-row .name` |
| `.tab.active` | `.tab-sel` (class on tab button) |
| `.action-btn .icon` | `.action-btn .material-icons` |

Map all patches under `.lifepunchulx { ... }` nesting.

---

<!-- GROK OUTPUT START -->

**1. Token remap table**

| ULX Variable       | Current Value       | DXRP-aligned Token   | Target Value              | Status    |
|--------------------|---------------------|----------------------|---------------------------|-----------|
| Background         | `$bg`               | `$color-primary`     | `#191919`                 | PROPOSED  |
| Accent             | `$accent`           | `$color-accent`      | `#7170e6`                 | PROPOSED  |
| Radius             | `$shell-radius`     | `$rounding`          | `2px`                     | PROPOSED  |
| Text               | (default)           | `$color-tertiary`    | `#F6F9FF`                 | PROPOSED  |
| Border             | (none / thick)      | —                    | `0.5px rgba(white, 0.04)` | VERIFIED  |
| Sidebar width      | (horizontal tabs)   | —                    | `200px`                   | VERIFIED  |
| Menu button idle   | —                   | —                    | `opacity: 0.7`            | VERIFIED  |
| Menu button hover  | —                   | —                    | `rgba($color-accent, 0.04)` | VERIFIED |
| Menu button active | —                   | —                    | `opacity: 1` + `rgba($color-accent, 0.08)` | VERIFIED |

**2. Top 10 SCSS selector patches**

```scss
// 1. Main shell
.staff-menu {
  background: #191919;           // was $bg
  border-radius: 2px;            // was $shell-radius
}

// 2. General borders
.staff-menu,
.player-row,
.action-btn {
  border: 0.5px rgba(white, 0.04);
}

// 3. Tab / menu button base (horizontal tabs)
.tabs .tab {
  opacity: 0.7;
  border: none;                  // remove blue ring
}

// 4. Tab hover
.tabs .tab:hover {
  background: rgba(#7170e6, 0.04);
}

// 5. Tab active
.tabs .tab-sel,
.tabs .tab.active {
  opacity: 1;
  background: rgba(#7170e6, 0.08);
  border: none;
}

// 6. Content area text
.staff-menu .content,
.player-name {
  color: #F6F9FF;
}

// 7. Action buttons
.action-btn {
  height: 48px;                  // was 90px
  background: transparent;
}

// 8. Action button icon container
.action-btn .icon {
  background: rgba(#7170e6, 0.1);
  color: #7170e6;
}

// 9. Remove heavy ulx-v2 effects
&.ulx-v2 .title-badge {
  box-shadow: none;
  background: transparent;
}

// 10. Sidebar-equivalent container (if using vertical)
.sidebar,
.tabs {
  width: 200px;
  flex-direction: column;
}
```

**3. .player-row.selected fix**

```scss
// Before (current)
.player-row.selected {
  background-color: $accent;
  color: #fff;
}

// After
.player-row.selected {
  background: rgba(#7170e6, 0.08);
  color: #F6F9FF;
}
```

**4. Horizontal tabs vs vertical sidebar recommendation**

**Recommendation: Vertical sidebar**

- **Effort: Medium**
- Matches PartyMenu reference structure and interaction model.
- Requires changing `.tabs` from `flex-direction: row` to `column` + width constraint.
- Horizontal tabs will never achieve true parity without heavy visual hacks.

**5. ulx-v2 simplification list**

- Remove `box-shadow: 0 0 18px $accent-glow` on `.title-badge`
- Remove all blue ring borders on `.tab`/`.tab-sel`
- Reduce `.action-btn` height (90px → 48px)
- Remove any `background-color: $accent` on selected states
- Strip `font-family: Poppins` from root (let global UI handle it)
- Remove any accent-soft badge styling that creates visual weight

**6. Risks**

- Window size reduction (if applied) may clip existing content or require layout changes in Razor.
- Purple accent (`#7170e6`) is a significant departure from current LP blue (`#4f8cff`).
- 2px radius is very sharp — may feel inconsistent with other lifepunchulx surfaces.
- If `.tabs` structure cannot be made vertical without Razor changes, parity will be limited.
- Low opacity buttons (`0.7`) may reduce readability on the darker `#191919` background.

**7. Red verify checklist**

- [ ] No linear gradients introduced anywhere
- [ ] `.player-row.selected` uses only `rgba()` (no solid $accent)
- [ ] All borders are exactly `0.5px rgba(white, 0.04)`
- [ ] `.action-btn` height changed and icon badge simplified
- [ ] `&.ulx-v2 .title-badge` has no glow/shadow
- [ ] Tab active state uses `rgba(#7170e6, 0.08)` only
- [ ] Material icons still visible after color changes
- [ ] Text remains `#F6F9FF` on all primary surfaces

---

**Open decisions (owner flag)**

- Accent: `#7170e6` (DXRP purple) or keep LP blue (`#4f8cff`)?
- Window size: shrink to `680x460` or keep current `1160x700`?

<!-- GROK OUTPUT END -->
