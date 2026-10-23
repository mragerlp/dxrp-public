# BTC-UI-2 pre-build: Bitcoin Hub UI skeleton (slice SHELL-1b)

PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.

**BTC-HUB-OVERHAUL-1 U2 (2026-10-18):** moved here from `_dev/btcui2/` (ruling BH-Q9). A hub press
now opens this UI through `components/LpBtcHubUiHost.cs` while the server convar
`lp_btc_hub_native_ui` is 1 (the default, ruling BH-Q7); 0 restores the Bitcoin Ops app
(`ui/LpHashdPanel.razor`, kept, not deleted). The panel binds the pressed hub. The PIN keypad now
asks the host: unlock through `UnlockAccessPinHost`, and set / re-enroll through `SetAccessPinHost`.
**HUB-UPGRADE-1 (2026-10-23):** the Racks tab's Upgrade control now asks the one existing purchase
rail, the same call HASHD makes (`rack.RequestPurchaseComputeTier()`, no arguments): the shell
(`RequestUpgrade`) sends it, the host's `NotifyPurchaseResult` envelope comes back through
`LpBtcHubUiHost.OnPurchaseResult`, and the control stays disabled for a viewer the host would refuse and
while a request is in flight. Price, debit and ledger stay on the host; nothing here keeps a balance.
Every other action control is still inert until U3. The dev ConCmds (`lp_btcui2_*`) stay in
`_dev/btcui2/LpBtcHubDev.cs`. The notes below describe the pre-build and are kept as written.

Seat CC-SIDE-UI-1, off-tree. Source: docs/superpowers/specs/2026-10-17-btc-ui-design.md on
cc-drive-1/DRIVE-BTC-UI-1-20261017 (commit 0ae06924, blob 6433be9f, 56417 B). Shell: the house
top-tab grammar (R-2832-A), ported from playerhub/code/ui/LpPlayerHubRoot.razor through
_dev/shellkit/ (that kit ships on PR 15, branch cc-side-ui-1/SHELL-1-20261017; nothing here needs
it to compile).

**UNVERIFIED in the editor.** Nothing here was compiled by s&box, linted, rendered or clicked.
An offline Roslyn build covered the three .cs files and the eight razor `@code` blocks against
hand-written engine stubs; razor markup and SCSS are not covered by it.

## What it is, and is not

- Seven tabs in spec order: Overview, Racks, Terminal, Wallet, Transfers, Logs, Settings, plus the
  PIN view that replaces the tab host (spec 6.1).
- `ILpBitcoinHubState` / `ILpBitcoinRackState`: one read-only view model carrying every readout of
  spec section 5, mapped to the H/K rows below.
- `ILpBitcoinHubFormat`: the one text seam. `LpBitcoinHubFormatUnruled` prints the invariant
  default form of each number because spec gate G2 forbids a format constant before R-BTCUI-1..4.
- `LpBitcoinHubStateFixture`: literal figures; it reads no component, field, economy or wallet.
- NO rails, NO money paths. The spec section 5 "Actions" row is left out. Every action control is
  an inert `btchub-action-inert` button whose tooltip names the rail the build slice binds. The
  only live controls are local: tab clicks, the wallet chip (opens Wallet), the rack slot cards
  (open detail), the PIN keypad (fills dots locally; any 4 digits continue, nothing is sent), the
  amount field (local bad-input text), copy-to-clipboard, close and Esc.
- No ConCmd and no chat command (spec 8 and 11). No edit to LpHashdPanel, LpMenuShell, the Player
  Hub or any rail file. Gates G2 and G3 of the spec are unruled, so this lives under `_dev/`.

## Files

| File | Role |
|---|---|
| ILpBitcoinHubState.cs | view-model contract, alert row, last result, format seam |
| LpBitcoinHubStateFixture.cs | fixture state + unruled formatter |
| LpBtcHubTab.cs | tab enum, labels, blurbs |
| LpBtcHubSkeleton.razor (+ .scss) | PanelComponent root: header, pills, tabs, page head, PIN view, footer, Esc |
| LpBtcHubTabParts.scss | shared tab body parts (tokens only) |
| LpBtcHub{Overview,Racks,Terminal,Wallet,Transfers,Logs,Settings}.razor (+ .scss) | the seven tabs |

## Readout coverage (spec 2.3 / 3 -> view model)

| Spec rows | Member(s) | Tab |
|---|---|---|
| H1 H6 H40 K19 | IsPowered | header switch, Overview, Settings |
| H2 H11 H16 H29 H36 K14 | WalletBtc | header chip, Overview, Wallet, Transfers |
| H11 H31 | WalletUsd (wallet only, R-3) | Overview, Wallet |
| H29 H38 K17 | PendingBtc, PendingUsd | Overview, Wallet, Transfers |
| H32 H37 K16 | UsdPerBtc, RateEventActive, RateEventMultiplier | Wallet, Transfers |
| H33 K11 | LocalCashUsd | Wallet |
| H34 K12 | (local amount text) x UsdPerBtc, "est. before bonuses" (R-14) | Wallet |
| H3 H28 H4 | LastResult (Kind, Ok, Message, PaidUsd) | reserved for the build slice |
| H4 K28 | PinSet, PinNeedsReEnroll | PIN view, header, Terminal, Settings |
| H7 H14 H18 H30 K21 | TerminalLinked, TerminalInRange | Overview, Terminal, Wallet, Settings |
| H8 H10 H13 K2 K35 | RackCount, RackMax | header, Overview |
| H9 | derived: IsPowered && TerminalLinked && racks-by-token | Overview |
| H12 H39 H40 K18 K22 | Alerts, UnreadAlerts | Logs, Settings |
| H13 H15 H19 | Racks[] by Token | Racks |
| H20 H22 K4 | Rack.Tier, Rack.ClockGhz (synced) | Racks |
| H20 H23 K4 | Rack.RateBtcPerMin (no TH/s) | Racks |
| H21 K3 | Rack.IsMining, Rack.Full, Rack.Yield, Rack.InvestedBtc | Racks |
| H24 K4 | Rack.Btc, Rack.CapBtc, Rack.FillPct | Racks |
| H25 K5 K7 | Rack.Tier, Rack.MaxTier + 5 static rows | Racks |
| H26 K6 | Rack.NextQuoteSats, Rack.NextQuoteUsd | Racks |
| H27 K6 | Rack.NextClockGhz, Rack.PaybackMinutes | Racks |
| H17 K9 K10 | static tracks; derived Online / Secured | Terminal |
| H35 K13 | OwnerSteamId | Transfers |
| K15 | send grammar (TC:249) + example | Transfers |
| K23 | recipe line | Settings |
| H41 | LifePunchUiFooter Product "Bitcoin Hub" | footer |
| 6.3 R-13 | OwnedHubCount, HubId | (Player Hub, UI-3) |
| sec. 5 | StateHash | every BuildHash |

Not carried, by design: H5 (stale "Upgrades" callout), H20 TH/s, H19 positional names.

## Rulings this skeleton waits on

R-BTCUI-1/2/3/4 (formats; G2), R-BTCUI-5 (power writer), R-BTCUI-6 (keypad style, clr, unlock
lifetime; the skeleton backspaces one digit as recommended, recorded open), R-BTCUI-8 (brand;
superseded 2026-10-18 by ruling BH-Q1: .btc brand core #FF7B00, see BH-BRAND-1), R-BTCUI-9 (seam; G3), R-BTCUI-10 (shell
partial and token gaps: the root sheet copies the shell's literal sizes, and declares
`$pin-dot-size` / `$pin-key-w` once each), R-BTCUI-11 (deposit / mining controls shown inert),
R-BTCUI-14 (preview label), R-BTCUI-15 (tab names). Shell rulings R-1..R-13 of
docs/ui/SHELL_CONTRACT.md still stand, except the hub accent: $btc-core replaces $lp-blue here.

## How the driving seat proves it

1. Sync this one folder (`_dev/btcui2/`) with kit-sync, `-WhatIf` first.
2. razor_lint each of the eight `.razor` files.
3. Mount without a ConCmd: in the editor scene, a GameObject with a ScreenPanel plus the
   `LpBtcHubSkeleton` component (component_add, DRIVE granted), then Play.
4. Positive ID: sweep sbox-dev.log for `[btcui2] LP_BTCUI2_SKELETON_20261017 shell=house tabs=7 rails=none`,
   postdating the sync. The string is defined once (LpBtcHubSkeleton.razor).
5. Captures, each read back: PIN view; each of the 7 tabs; Racks detail for gpurack-1; Wallet with
   `abc` typed (bad-input line) and with `9` typed (Insufficient funds).
6. ui_panel_dump: 7 tab nodes, exactly 1 tab-sel, no LpMenuShell node, header pills present.
7. His click: Esc in rack detail returns to slots; Esc again closes; reopen starts at the PIN view.
8. Confirm no rail fired: no host log line from any hub rail while clicking every inert button.

## QoL (by reading, UNVERIFIED)

- Discover: no open path by design (no ConCmd); the build slice opens it from the hub press.
  Overview teaches the order; every tab has a blurb; Settings has the recipe line.
- Close: header close and Esc; Esc closes rack detail first (spec 11), consumed like LpTagsMenu.
- Bad input: PIN (digit cap, short-entry message); amount (unparseable, over wallet).
- Help: tab blurbs, empty slot hint, empty Logs state, send grammar.
- Conflicts: no ConCmd or chat name registered; positive ID unique; the class names
  `lp-switch*` and `lp-money-*` repeat the Player Hub's on purpose (same grammar), scoped by sheet.
