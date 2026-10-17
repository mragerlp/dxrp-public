# lifepunch.core: one shared library for the LifePunch packages (proposal)

LifePunch internal working doc (not a dxrp.net/docs page). Written 2026-10-17 from `../packages/CODE_CENSUS.md`. This page proposes; the owner decides. Source keys are those in `../packages/PACKAGE_SHAPE.md`.

## The proposal

Publish `game/Code/Addons/lifepunch/lifepunchcore` once, as an s&box library package `lifepunch.core` under the `lifepunch` org. Every LifePunch package depends on it instead of carrying a private copy. The current doctrine copies shared primitives into each package (`DXRP_ADDON_PUBLISH_DOCTRINE.md:169`, cited in `PACKAGE_SHAPE.md` section 8). This proposal replaces that rule for `lifepunchcore`.

Proposed `packages.json` row: slug `lifepunchcore`, s&box ident `lifepunch.core` (`packages.proposed.json`).

## What goes in

`lifepunchcore` today: 19 files, 85,574 B (17 `.cs`, 1 `.razor`, 1 `.scss`; `CODE_CENSUS.md` row `lifepunchcore`):

`ILifePunchPurchaseEvent.cs`, `LifePunchEntityOwnership.cs`, `LifePunchGroundContact.cs`, `LifePunchInteractTags.cs`, `LifePunchMachineDestroyFx.cs`, `LifePunchMenuInputBlock.cs`, `LifePunchMenuInteractGate.cs`, `LifePunchMenuInteractRange.cs`, `LifePunchPropPhysics.cs`, `LifePunchScrollLayout.cs`, `LifePunchScrollRegionPanel.cs`, `LifePunchSourceMark.cs`, `LifePunchTerminalLcd.cs`, `LifePunchUiFooter.razor`, `LifePunchUiFooter.razor.scss`, `LifePunchUiScale.cs`, `LifePunchUiScrollPolicy.cs`, `LifePunchUpgradeLedger.cs`, `LifePunchUpgradeTracks.cs`.

No ConCmd, no RPC. Namespace `LifePunch.DXRP.Addons`. It reads `Config.Current.Game` (4 sites) and persists one host file, `LifePunchUpgradeLedger` in `FileSystem.Data` (`CODE_CENSUS.md`, section `lifepunchcore`).

Open: whether the shared UI shell joins it. The 8 loose files under `game/Code/Addons/lifepunch/` (`LpMenuShell.razor`, `LifePunchUiShell.scss`, `LifePunchCurrencyToken.razor`, `LifePunchMarketSpawn`, ...; 40,542 B) are not in `lifepunchcore`. `LpMenuShell` has one consumer today (`lpbitcoin`). It uses `LifePunchCurrencyToken` and `LifePunchUiFooter`.

## What it depends on

| Dependency | Kind | Evidence |
|---|---|---|
| DXRP core (`Dxura.RP.*`, `package.dxura.rp`) | compile | graph edge `core --> dxrp` (`CODE_CENSUS.md`, dependency graph) |
| `bitcoinmining` | none at compile time: a doc-comment `<see cref>` only | `lifepunchcore/LifePunchTerminalLcd.cs:16` |

It depends on no other LifePunch unit. That is what makes it publishable first.

## Who depends on it

| Package (unit) | Reference lines into core | Evidence |
|---|---:|---|
| `lifepunch.btc` (`bitcoinmining`) | 40 | graph edge `btcm --> core` |
| `lifepunch.btc` (`lpbitcoin`) | 33 | graph edge `lpb --> core`; e.g. `LpBitcoinHubLogsScrollPanel : LifePunchScrollRegionPanel` |
| `lifepunch.ulx` (`lifepunchulx`) | 15 | graph edge `ulx --> core` |
| `playerhub` | 6 | graph edge `ph --> core`; `LifePunchUiFooter` at `LpPlayerHubRoot.razor:88` |
| `lpmorpheus` | 4 | `lpmorpheus/LpMorpheusEntity.cs:92` |
| `_dev` | dev only, never ships | dotted edge `dev -.-> core` |

Units that do not use it: `serverhub`, `lptags`, `lplockpick`, `lpweapons`, `ak47`.

## Why one library and not a copy per package

- **One ledger.** `LifePunchUpgradeLedger` calls itself "THE source of truth for upgrade ownership" (`LifePunchUpgradeLedger.cs:51-58`). A copy per package gives each package its own type with the same name in a different assembly. Whether s&box then keeps one ledger file or two is UNVERIFIED. With one library there is one type and one file.
- **One footer, one input guard.** `LifePunchUiFooter` has 5 consumers, and `LifePunchMenuInputBlock` carries an internal `LifePunchMenuInputGuard : Component`. Copied components exist twice under the same name. Whether the TypeLibrary tolerates that is UNVERIFIED.
- **One place to fix.** A fix to an interact gate or a scroll region ships once.

## Risks and the order of work

1. `lifepunch.ulx` is published with `lifepunchcore` copied in (`PACKAGE_SHAPE.md` section 7, item 1). If `lifepunch.core` loads in the same game mode, the same type names exist in two assemblies. Recommended: publish `lifepunch.core`, then a ULX revision that drops its copy and declares the dependency, then `lifepunch.btc` built against it. Until the ULX revision lands, do not load `lifepunch.core` next to it on the Dev game mode without a proof run.
2. The package's own assembly must compile against `package.dxura.rp.dll` with no `partial` DXRP types and no `internal` DXRP members (`PACKAGE_SHAPE.md` section 8). This pass did not check `lifepunchcore` for either. A package-mode build is the proof, and it has not been run.
3. How a package declares a dependency on another s&box library package (sbproj field, version pin) is UNKNOWN here: neither `ulx.sbproj` nor `libstates.sbproj` read in this pass declares one. Settle that from the s&box docs or a published example before the first build.
4. Every source file carries the proprietary header (`AGENT_ONBOARDING.md:274-276`). Check `lifepunchcore` before publishing.
