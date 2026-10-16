# Player Hub: ULX reshell + live $BTC tab - design spec

Date: 2026-10-16. Seat: CC-SBOX-KIT-2 (lane SBOX-OBS), AS-RUN claude-fable-5-1 (self-declared, UNVERIFIED).
Approved in chat by Bloodwave (principal), 2026-10-16: "reskin the whole Player Hub to the ULX grammar and add
the live $BTC tab as designed. Write the spec and build." That approval is also the ruling on the sibling-tab
composition left open by `lifepunch/docs/cvl/PLAYER_HUB_BTC_STORE_RULING_2026-07-15.md` sec 3.

## 1. Goal

The Player Hub (`game/Code/Addons/lifepunch/playerhub`) becomes the ULX-grammar shell and gains its first LIVE
tab, `$BTC`, which reads the player's Bitcoin Hub and GPU racks and drives their power/mining switches through
the existing host RPCs. Cosmetic fixtures in the other four tabs are unchanged inside; only the shell around
them changes. World-side animation and sound keep hanging off entity state (`LpBitcoinPowerAnim`,
`ApplyHubPowerVisual`, `ApplyRackMiningVisual`); CC-ASSETS-1's new sequences and `.sound` files plug into the
existing candidate lists later with no UI change.

## 2. Reference artifacts (lifepunch-razor-ui sec 1: reference first)

- Shell grammar + tokens: `lifepunchulx/StaffMenu.razor` and `StaffMenu.razor.scss` at workbench HEAD
  `d9c08031` (NOT the working copy: that file is another session's in-flight edit). Rules copied: `.window`,
  `.ulx-shell-header`, `.title-stack` / `.title-primary` / `.brand-wordmark` / `.wordmark-life` / accent
  suffix, `.title-sub`, `.header-actions`, `.header-chip`, `.lp-ui-chrome-btn` / `.chrome-close`,
  `.ulx-shell-tabs`, `.tab` / `.tab-sel`, `.ulx-workspace`.
- Money grammar: `LpHashdPanel.razor` `.lp-money-btc` + `.lp-money-amt` (Law 17), `$ops-orange #f7931a`,
  `$ops-cash #5fd38d`.
- Entity data: `LpBitcoinHubEntity` (`IsPowered`, `HubWalletBtc`, `AccessPinIsSet`, `Owner`, `GetLinkedRacks()`,
  `GetRackPendingBtc()`, `FindHubByOwnerSteamId`, `SetPowered`, `RequestDepositRacksToHub`,
  `RequestCashOutAllHub`, `CanManageHub`), `LpBitcoinRackEntity` (`IsMining`, `BitcoinAmount`, `ClockGhz`,
  `CoreCount`, `ComputeTier`, `MiningRatePerMinute`, `UsdValue`, `AdvancedRack`, `RequestSetMining`),
  `LpBitcoinEconomy.BtcToCashUsd`.
- Footer: `lifepunchcore/LifePunchUiFooter.razor` (unchanged).

## 3. Tokens (`LpPlayerHubTokens.scss`)

Variable NAMES stay so the four fixture tabs compile untouched; VALUES move to the ULX neutrals:

| token | old | new (ULX @ d9c08031) |
|---|---|---|
| `$bg` | #000000 | #17181a (`$shell`) |
| `$surface` | #1a1d23 | #1e1f21 |
| `$surface-inset` | #12151a | #1e1f21 |
| `$card-header` | #22262e | #272829 (`$surface-raised`) |
| `$border` | #374151 | #343536 |
| `$text-main` | #ffffff | #e4e6e8 |
| `$text-dim` | #9ca3af | #8d8f90 |
| `$lp-blue` | #017AEF | #1689ff |
| `$lp-blue-hover` | #33A0FF | #55aaff |
| `$good` | #199c3b | #199c3b |
| `$bad` | #931010 | #c05f5f |
| `$radius-card` | 12px | 10px |

New: `$canvas #0f1011`, `$amber #d99a3f`, `$btc #f7931a`, `$cash #5fd38d`, `$font-mono "Roboto Mono"`.
FLAG (canon question, not decided here): `LIFEPUNCH_UI_STANDARD.md` fixes `$lp-blue` at `#017AEF`; ULX uses
`#1689ff`. This spec follows ULX because the principal named ULX as the reference. The standard needs a
changelog line if this sticks.

## 4. Shell (`LpPlayerHubRoot.razor` + `.razor.scss`)

```
root.lp-playerhub (backdrop rgba($canvas,.86), mono font)
  .window (1160x700, $bg, 1px $border, 10px radius, shadow)
    .hub-shell-header   wordmark "lifepunch" + ".hub" accent | title-sub "PLAYER HUB"
                        right: header-chip LEVEL n | header-chip $LP n | chrome-close
    .hub-shell-tabs     .tab x5: Overview | $BTC | $LP Store | Skills | Stats  (.tab-sel = active)
    .hub-workspace      .hub-page-head (title + blurb) then the tab component
    LifePunchUiFooter
```
Sidebar, identity block, nav badges and the fixture-scope note are removed. The PREVIEW tag moves into the
page head as a `.header-chip` only on fixture tabs. State classes are standalone (`tab-sel`), never compound.

## 5. $BTC tab (`Btc/LpPlayerHubBtc.razor` + `.razor.scss`, VMs in `LpPlayerHubModels.cs`, live read in
`data/LpPlayerHubBtcSource.cs`)

VMs (display strings pre-formatted on the data side, like the serverhub Banker socket):
- `LpBtcVm(bool HasHub, bool IsPowered, bool PinSet, bool CanManage, string WalletBtc, string WalletUsd,
  string PendingBtc, int RackCount, IReadOnlyList<LpBtcRackVm> Racks, Action TogglePower, Action DepositAll,
  Action CashOutAll, string Note)`
- `LpBtcRackVm(string Id, string Label, string TierLabel, string ClockLabel, string RateLabel, string MinedBtc,
  string MinedUsd, bool IsMining, bool Advanced, Action ToggleMining)`

Source: `LpPlayerHubBtcSource.Build(...)` finds the hub by `Game.SteamId`, builds the VM, wires actions to
`hub.SetPowered(!IsPowered)`, `rack.RequestSetMining(!IsMining)`, `hub.RequestDepositRacksToHub()`,
`hub.RequestCashOutAllHub()`. Every action also calls `LpBitcoinUiSounds.PlayKeyboardClick()` (the switch
click sound until CC-ASSETS-1 ships a dedicated one). `LiveHash()` folds powered/wallet-sats/rack states into
the Root's `BuildHash` so the panel re-renders on replicated change.

Layout: hub plate (power switch, ฿ wallet + $ quote, pending ฿, PIN chip, rack count) ; actions row
(Deposit all, Cash out all; `$good` solid buttons, disabled when nothing to move or no manage right) ; rack
grid of cards (tier, clock/cores, rate /min, mined ฿ | $, mining switch). Switch = `.lp-switch` track + knob,
`.lp-switch-on` standalone state class, 140 ms transition. Empty state = `LpPlayerHubEmpty` with
"Place a Bitcoin Hub" copy. Law 17: `฿` sign `$btc`, `$` sign `$cash`, amounts `$text-main`.

Money law: NO new money code. The tab only calls existing host rails (debit-before-await exemplars). No
purchase, no send/receive (slice 2 per `DUAL_CURRENCY_IDENTITY_RULING_2026-07-15.md` sec 2).

## 6. Not in this slice

Send/receive, $LP purchase, PIN entry, rack linking, HASHD terminal restyle, any edit under `lifepunchulx`
or `lpbitcoin`/`bitcoinmining`, new sounds or model sequences (CC-ASSETS-1).

## 7. Proof

1. `razor_lint` (engine host, read-only) on the new/changed razor before sync.
2. Positive ID `LP_PLAYERHUB_ULX_BTC_20261016` in `LpPlayerHubRoot` + a `Log.Info` on first open; the editor
   log line postdating the sync is the FRESH proof (two facts).
3. Bridge screenshots read back by the seat: hub open on the game scene (empty state if no hub owned), then
   with a hub + rack dev-spawned (`LpBitcoinDevSpawn`; scene-mutating, needs the principal's DRIVE word).
4. lifepunch-razor-ui pre-flight: hex/radius diff grep empty outside `$name:` lines; no `$var-<digit>`;
   no compound state selectors; `@code` ASCII; PROPRIETARY header on every new file.

## 8. Files

Changed: `playerhub/code/ui/LpPlayerHubTokens.scss`, `LpPlayerHubPrimitives.scss`, `LpPlayerHubRoot.razor`,
`LpPlayerHubRoot.razor.scss`, `playerhub/code/data/LpPlayerHubModels.cs`.
New: `playerhub/code/ui/Btc/LpPlayerHubBtc.razor`, `LpPlayerHubBtc.razor.scss`,
`playerhub/code/data/LpPlayerHubBtcSource.cs`.
Branch: `claude/sbox-kit-2-dispatch-b4a9e2` (this worktree, off `bacab67c`). Live proof by robocopy of the
`playerhub` folder only into `D:\Cavelux\dxrp-workbench\game\Code\Addons\lifepunch\playerhub`.
