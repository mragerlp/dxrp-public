# Style diff — Party Menu vs lifepunchulx (StaffMenu)

**Reference pin:** `dxrp-public` @ `914967a` (`bounty/73-party-system`)  
**Target:** `lifepunch/.../adminmenu/StaffMenu.razor.scss` (monorepo, read-only from this repo)

---

## 1. Design tokens

| Token | Party Menu / DXRP (`styles.scss`) | lifepunchulx (current) | Parity action |
|-------|-------------------------------------|-------------------------|---------------|
| Panel bg | `$color-primary` **#191919** | `$bg` **#14161c** | Move toward #191919 / #111111 stack |
| Sidebar bg | `$color-secondary` **#111111** | horizontal `.tabs` strip rgba(0,0,0,0.22) | Consider sidebar model OR flatten tab strip to TabMenu language |
| Accent | `$color-accent` **#7170e6** (purple) | `$accent` **#4f8cff** (blue) + `$dxrp-cyan` | **Pick one accent** — Party uses purple; ULX uses blue. Owner call: match Party (#7170e6) OR keep LP blue at lower saturation |
| Text primary | `$color-tertiary` **#F6F9FF** | `$text` **#e8eaed** | Align to #F6F9FF / near-white labels |
| Text dim | `$color-gray-400` **#888b91** | `$text-dim` **#9aa0ab** | Match gray-400 |
| Border | **0.5px** rgba(white, 0.04) | **1px** `$border` rgba(255,255,255,0.07) | Thinner, subtler borders |
| Radius | `$rounding` **2px** | `$shell-radius` **22px** window, tabs **9px**, rows **7px** | **Major delta** — Party is nearly square; ULX is rounded dashboard. Reduce window radius toward 2–4px |
| Font | **Mina** (DXRP `$main-font`) | **Poppins** on root | Consider Mina for menu chrome to match Party/TabMenu (keep Material Icons fix) |
| Overlay | rgba(black, **0.55**) | rgba(4,6,12, **0.62**) | Slightly lighter overlay |

---

## 2. Layout architecture

| Area | Party Menu | lifepunchulx | Parity action |
|------|------------|--------------|---------------|
| Shell size | **680×460** compact | **1160×700** large dashboard | Optional size trim — do not break roster+actions layout |
| Navigation | **Vertical sidebar** 200px, `.menuButton` | **Horizontal tab strip** `.tabs .tab` | **Largest structural question.** Full parity = sidebar refactor in Razor (layout-only). Lighter pass = restyle horizontal tabs to *look* like Party selected state without moving DOM |
| Title bar | Simple `.title-bar` 10px pad, uppercase 14px label | Badge + title stack + chips + chrome buttons | Simplify header: drop glow badge (`ulx-v2 .title-badge` box-shadow), flatten to Party title-bar |
| Content | `.content` + `.pane` 16px pad | `.ulx-workspace` multi-pane (roster + actions + profile) | Keep 3-pane logic; restyle panels only |

---

## 3. Selected / hover states

| Element | Party Menu | lifepunchulx | Parity action |
|---------|------------|--------------|---------------|
| Nav item default | `opacity: 0.7` | tab: dim text, transparent bg | Adopt opacity model on tabs/sidebar |
| Nav item hover | `opacity: 1`, `rgba($color-accent, 0.04)` | tab hover: white 5% bg + blue icon | Replace with Party hover wash |
| Nav item active | `.active-btn`: opacity 1, `rgba($color-accent, 0.08)` | `.tab-sel`: blue ring + `$accent-soft` fill | **Replace** heavy ring with subtle 8% accent wash |
| Player row hover | (via PartyMemberCard) | solid `$bg-row` **#252932** | Keep subtle; avoid loud fills |
| Player row selected | card highlight | **full `$accent` blue fill** — very loud | **High priority:** change to `rgba($color-accent, 0.08)` + opacity like Party, not solid blue |
| Action tiles | compact `.primary-btn` 7×13 pad | `.action-btn` **90px tall** tiles with icon badges | Shrink tiles toward Party button scale OR keep grid but flatten chrome (remove icon badge background) |

---

## 4. ulx-v2 chrome to tame (keep class, simplify CSS)

Current `&.ulx-v2` adds:
- 42px badge with `$accent-glow` box-shadow → **remove glow**
- 18px title-primary + uppercase sub → align to Party 14px uppercase single title
- Pill chips in header → simplify to Party icon-only chrome (visibility/close)

---

## 5. Files to edit (monorepo — not this repo)

| Priority | File | Scope |
|----------|------|-------|
| P0 | `StaffMenu.razor.scss` | Tokens, tabs/sidebar, player-row selected, action-btn, header |
| P1 | `StaffMenu.razor` | Only if moving tabs → sidebar (class/DOM wrappers) |
| P0 | Verify zero `linear-gradient` | Already clean — keep it |

---

## 6. Proof (Red only)

```powershell
lifepunch\scripts\Sync-LifePunchAddonsToDxrp.ps1 -Addon adminmenu
# Play → staffmenu → screenshot; log filter gradient → empty
```

---

## 7. Open decisions for Bloodwave

1. **Accent color:** match Party purple (#7170e6) vs keep LP blue (#4f8cff)?
2. **Layout:** full sidebar refactor vs horizontal tabs with Party styling?
3. **Window size:** shrink toward 680×460 or keep large ops console width?
