# Shell kit - `lifepunch.sample`

**STATUS: UNVERIFIED in the editor.** Authored off-tree by CC-SIDE-UI-1 (AS-RUN claude-opus-5-5, self-declared,
UNVERIFIED) under DISPATCH_CC-SIDE-UI-1_R1 unit S3. It was written to compile by reading. No editor, no
`razor_lint`, no compiler and no capture has seen it. Nothing here is proven until the driving seat (CC-DRIVE-1)
syncs it and reads the proof below.

## What it is

A dev-only menu that exercises every part of the house shell in `docs/ui/SHELL_CONTRACT.md` Part A (the ULX
top-tab grammar, R-2832-A). It is the executable draft for proposal P-1 (one shared house-shell component).

| File | Role | Reference artifact it ports |
|---|---|---|
| `ShellKitSample.razor` | the menu (PanelComponent) | `playerhub/code/ui/LpPlayerHubRoot.razor:17-90` (grammar), `lptags/tags/code/ui/LpTagsMenu.razor:311-319` (Esc) |
| `ShellKitSample.razor.scss` | styles, no colour declared | `playerhub/code/ui/LpPlayerHubRoot.razor.scss:11-261`, `Btc/LpPlayerHubBtc.razor.scss:188-205` |
| `ShellKitHost.cs` | ConCmd stub + mount host | `playerhub/code/ui/LpPlayerHubHost.cs:35-196` |

Parts exercised: brand slot (`lifepunch` + `.sample`, subtitle), two header pills (one a currency token),
chrome close, a four-tab strip with `tab-sel`, page head with a chip, a tab host with one scroll region,
`LifePunchCurrencyToken` (BTC, cash, dual price, a rate with a unit), bad-input feedback (stepper bounds 0-5),
a disabled action, an empty state, `LifePunchUiFooter`, Esc to close, a toggle ConCmd and a deep link.
Every figure is a constant. The kit reads no game state, calls no RPC and moves no money.

Tokens come from `playerhub/code/ui/LpPlayerHubTokens.scss`, imported, not copied. It is the only importable
surface that carries the ULX values. This is a cross-addon import, and when R-1 / R-3 are ruled it moves to the
house token file. `$lp-blue` (R-2) is read through the token, so a ruling changes the kit with no edit.

## Commands

| Command | Effect |
|---|---|
| `lp_shellkit` | open or close |
| `lp_shellkit_tab <name>` | open on a tab: `overview`, `readouts`, `states` or `help` (case-insensitive; an unknown name logs a warning and keeps the default) |
| Esc / close button | close |

## Positive code-string ID

`[shellkit] LP_SHELLKIT_20261017`. It is logged once per session by `OnStart` on the first open, as
`[shellkit] LP_SHELLKIT_20261017 shell=house tabs=4`. Grep the worktree before syncing: the ID exists only in
`ShellKitSample.razor` (the constant `KitMark`) and in this README.

## How the driving seat proves it (in order; each step is the driving seat's act under its own GO)

1. **Sync, one folder only:** copy `game/Code/Addons/lifepunch/_dev/shellkit/` (3 code files + this README) from
   branch `cc-side-ui-1/SHELL-1-20261017` into the same path in the live tree, through `/kit-sync` (S3 of the
   driving seat's slice). Nothing else in this branch is code. The `docs/ui/*.md` files never sync.
2. **Compile sensor:** `razor_lint` on `ShellKitSample.razor` (the engine-host route), then a log sweep from the
   sync time: `log-sweep.ps1 -Since <local sync time> -Id LP_SHELLKIT_20261017`. Grep for `] Error |`,
   `Compile of '`, and `Broken Reference`, never `error CS` alone (engine error lines carry no CS code).
3. **FRESH proof (two facts):** run `lp_shellkit` in the console. The editor log must show
   `[shellkit] LP_SHELLKIT_20261017` with a stamp after the sync.
4. **Captures:** one per tab, via `lp_shellkit_tab overview`, `readouts`, `states`, `help`, plus `ui_panel_dump` of
   the root. Read each PNG: wordmark in two colours, subtitle, two pills, close button, underline on the active tab,
   coloured BTC and `$` signs with white amounts, footer present.
5. **Behaviour:** Esc closes; `lp_shellkit` reopens fresh on Overview; `lp_shellkit_tab bogus` logs the warning.
   The stepper's bad-input message needs a real click (synthetic clicks do not reach s&box panels) or an
   `execute_csharp` driver. Press + at 5 and expect "The sample allows at most 5."
6. **Fail branch:** any compile error, a missing ID, or a capture that disagrees with the contract means the kit is
   NOT proven. Report it to CC-SIDE-UI-1's RETURN by path; do not patch it live.

## Flagged before use (sbox-engine-truth LAW 0)

- `Input.EscapePressed`: in-tree exemplar `LpTagsMenu.razor:314-317` (the component-side Esc idiom). Not used in a
  `GameManager.ShowUi` menu before; UNVERIFIED there.
- A `_dev` folder under `Code/Addons/lifepunch`: no in-tree precedent for an underscore-prefixed code folder.
  UNVERIFIED that the compiler includes it (expected, since it compiles every `Code/` file).
- Nested enum `ShellKitSample.ShellKitTab` used from `ShellKitHost.cs`: the nested-type idiom is in-tree
  (`LpMenuShell.NavItem` from `LpHashdPanel.razor:1037`).
- **Ships with the game:** code under `Code/` compiles into the game assembly, so `lp_shellkit` would exist for players
  if this reached the Portal. Removing or gating the kit before publish is the principal's call.

## Remove

Delete `game/Code/Addons/lifepunch/_dev/shellkit/`. Nothing references it.
