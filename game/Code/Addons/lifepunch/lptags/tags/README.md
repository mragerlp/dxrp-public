<!--
PROPRIETARY & CONFIDENTIAL â€” Â© 2026 lifepunch.co. All rights reserved.

"LIFEPUNCHâ„¢ Tags for DXRP" (s&box ident: lifepunch.tags Â· addon ident: lptags) is the sole-owned
intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
sublicensing, copying, or reuse by ANY person or entity â€” including DXRP and
LifePunch staff, contributors, or community â€” EXCEPT the owner (lifepunch.co).
Third-party material identified below remains governed exclusively by its stated license;
this notice does not relicense that third-party material.
Presence in this repository or on the DXRP portal grants no rights to anyone else.

Author account: mrragerlp Â· Public alias (in-game Â· Steam Â· Discord): Bloodwave
-->
# lifepunch.tags foundation

`lifepunch.tags` is the standalone foundation for deterministic player-name
cosmetics on Chat, Scoreboard, and Nameplate surfaces. Its code uses the
`LifePunch.DXRP.Addons.Tags` namespace. The package includes stable catalog and
profile contracts, validation/repair rules, the shared scheduler and production
name presenter, a menu, local viewer preferences, and narrow surface-adapter
interfaces.

This repository does not include concrete live Chat, Scoreboard, or Nameplate
adapters, an authoritative account store, or production network wiring.
No production global persistence exists yet.

## Effects, surfaces, and local viewing

The eleven selectable effects are:

1. Solid Red
2. Solid Green
3. Solid Yellow
4. Solid Blue
5. Solid Cyan
6. Color Wipe
7. Rainbow Wave
8. Red Scanner
9. Slide
10. Bouncing Dot
11. Bouncing Plus

Chat, Scoreboard, and Nameplate each retain an independent selected effect and
independent on/off state. Selecting an effect does not enable a surface, and
turning a surface off does not erase its remembered selection. Stored choices
use stable keys; effective public state uses allowlisted IDs only.

The three local viewer settings are `Reduce Motion`, `Disable Animations`, and
`Hide Dot/Plus Decorations`. They change only what the current player sees and
do not change another player's stored selection.

The evaluator and presenter retain the authoritative filtered display name.
They do not accept client-authored CSS, HTML, markup, raw colors, rank, role,
staff identity, or other identity metadata.

## Menu commands and runtime modes

- `lifepunchtags` toggles the Tags menu.
- `lp_tags_test` is the local diagnostic command compiled only when the
  `LIFEPUNCH_LOCAL` build symbol is defined.

With `LIFEPUNCH_LOCAL`, the menu uses the session-only local-preview client and
labels its state `LOCAL PREVIEW`; it never labels that state global or
production. Local preview does not create global persistence.

In a live build without the still-missing production client integration, the
menu opens through the same commands in read-only unavailable mode, reports
`Integration unavailable`, disables outgoing profile changes, and leaves
public surfaces plain. It does not silently replace the missing backend with
local memory.

## Surface adapter contracts

The `code/adapters` directory publishes interfaces only:

- Chat receives one immutable row-time effect snapshot. System messages are
  always plain, and old rows expose no effect-update path.
- Scoreboard receives authoritative roster identity/effect updates and board
  visibility from its eventual owner.
- Nameplate receives authoritative identity plus explicit host and geometric
  visibility; hidden or unknown host state always wins.

All three contracts require viewer-preference subscription and deterministic
teardown. They deliberately name no speculative DXRP Chat, roster, entity,
WorldPanel, store, or RPC API.

## Successor integration gates

The canonical plan excludes production integration until these five separately
named gates activate:

1. **Global profile and network integration** — requires the real account
   store, conditional-write API, connection lifecycle, RPC filtering, registry
   spawn point, and synchronized collection support.
2. **Chat adapter** — requires the authoritative receive pipeline,
   stock-overlay policy, channel/block/gag/mute behavior, and row lifecycle.
3. **Scoreboard adapter** — requires the authoritative roster, row identity,
   open/close, badge, and update lifecycle.
4. **Nameplate adapter** — requires the player prefab/spawn path, WorldPanel
   setup, local/spectator policy, distance/occlusion, and
   cloak/incognito/fake-disconnect hooks.
5. **Cross-surface production acceptance** — requires all three adapters and
   the real global backend in the same editor project.

## Validation status

Validation status: **STATIC SOURCE PREPARED — EDITOR VALIDATION PENDING**.

Static preparation is not s&box editor validation. No standalone compilation,
editor execution, console-command execution, live adapter behavior, global
persistence, or production integration validation is claimed here.

## Provenance and attribution

Effect behavior is adapted from
[`mragerlp/lifepunchwawcolortags`](https://github.com/mragerlp/lifepunchwawcolortags)
at pinned commit
[`dba9dca76d008ac6b59f62fcd8bc3bce9bcac9bf`](https://github.com/mragerlp/lifepunchwawcolortags/commit/dba9dca76d008ac6b59f62fcd8bc3bce9bcac9bf).
See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the complete MIT notice,
source mapping, and LifePunch modifications.
