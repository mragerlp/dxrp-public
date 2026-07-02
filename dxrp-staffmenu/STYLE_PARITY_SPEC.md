# STYLE PARITY SPEC — lifepunchulx → Party Menu / DXRP tab language

**Owner:** Bloodwave / LIFEPUNCH™  
**Task type:** UI styling overhaul only  
**Target addon:** lifepunchulx (`lifepunch/addons/.../adminmenu/`)  
**Reference:** Dimmer Party Menu + DXRP TabMenu (paths in `paths/CANONICAL_PATHS.md`)

---

## Goal

Restyle the existing **functional** lifepunchulx admin menu so it visually mirrors the simple, clean Party Menu direction — native to DXRP, not a custom cyberpunk dashboard.

## Primary rule

**Preserve functionality.** No rewrites of permissions, RPC, aliases, server actions, or routing.

## Safe to change

- `StaffMenu.razor.scss` — colors, spacing, borders, selected states, typography
- `StaffMenu.razor` — layout wrappers and CSS classnames only (header, tabs, roster, panels)

## Do not change

- `StaffMenuHost.cs`, `StaffMenuActions.cs`, `StaffMenuBridgeService.cs`
- Permission gates, command behavior, waypoint/audit logic
- Package ident, addon identity, DXRP official lane code

## s&box CSS law

- **No** `linear-gradient` on panel backgrounds (proven log spam on open)
- Solid colors + optional `box-shadow` only
- Material Icons: keep `.material-icons { font-family: Material Icons; }` under root (Poppins cascade bug)

## Acceptance

- Menu opens from existing aliases
- All admin actions still work (kick, freeze, waypoints, settings, audit)
- Visual direction matches Party Menu: flat panels, 2px radius, opacity-based tab select, subtle accent wash
- No new features; no commits without Bloodwave GO

See `handoff/STYLE_DIFF.md` for concrete token mapping.
