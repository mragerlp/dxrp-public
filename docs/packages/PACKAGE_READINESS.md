# Package readiness: the addons after lifepunch.ulx

LifePunch internal working doc (not a dxrp.net/docs page). Sensed 2026-10-17 by seat CC-SIDE-PKG-1 (dispatch CC-SIDE-PKG-1 R1), read-only.
**This page recommends. The principal rules** on every identity, order and publish act.

Read with `PACKAGE_SHAPE.md`, which defines the two systems (Portal Code mode vs s&box Package mode) and the checklist, and `CODE_CENSUS.md`, which has the dependency graph and rails. Source keys are those in `PACKAGE_SHAPE.md`.

## Shared facts behind every row

| # | Fact | Sensor / source |
|---|---|---|
| S1 | Portal limits: code package <= 50 MB, asset package <= 300 MB | `dev:docs/developers/creating-addons.md:45` |
| S2 | Addon code on `develop` equals the live compile tree, 220/220 files, LF-normalized | per-file SHA-256, 2026-10-17 |
| S3 | `develop` carries addon **assets** only for `ak47` (49 files, 11,867,810 B), `deserteagle` (2, 2,993 B), `lpbitcoin` (216, 532,643,087 B), `lplockpick` (2, 2,680 B), `lptags` (2, 130,236 B) | `Get-ChildItem dev:game/Assets/addons/lifepunch` |
| S4 | **R-f identity drift**: the live `game/rp.sbproj` (1936 B, SHA-256 `48B583D4...EF9F`) says Title `LifePunch`, Org `lifepunch`, `Metadata.ParentPackage: dxura.rp`, and has an `AI` block. `develop`'s (1840 B, `243626EB...0601`) says Title `DXRP`, Org `dxura`, with no ParentPackage. Resources are the same | own-sensed pins; full hashes in the seat RETURN |
| S5 | Only `lifepunchulx` has a `LIFEPUNCH_PACKAGE` code path (26 preprocessor lines). Every other candidate has never been compiled as its own assembly, as far as the tree shows | `CODE_CENSUS.md` method notes |
| S6 | The house rule is that shared primitives (`lifepunchcore`, loose root shell files) are **copied into each package** | `lp:lifepunchaddons/docs/DXRP_ADDON_PUBLISH_DOCTRINE.md:169`; the ULX harness bundles `lifepunchcore` |
| S7 | House asset posture: ownership defaults to ours unless the owner flags external work | `lp:lifepunch/docs/AGENT_ONBOARDING.md:262-264`. The licence column below reports what the files say; it is not a provenance ruling |

## Per addon

### playerhub (Player Hub)

| Item | State |
|---|---|
| Code size | 120,480 B (live). S1: ok |
| Assets | none exist (no `Assets/addons/lifepunch/playerhub` in either tree); `dev:game/rp.sbproj:8` still lists `addons/lifepunch/playerhub/**` |
| Cross-addon coupling | **Hard** compile references to `lpbitcoin` (`LpBitcoinHubEntity`, `code/data/LpPlayerHubBtcSource.cs:31-34`) and `bitcoinmining` (`LpBitcoinIdent`, `:94-95`); `lifepunchcore` (6 lines). Cannot compile alone |
| Package-mode blockers | no package branch (S5); the coupling above |
| Licence lines | n/a (code only) |
| Identity | not in `lp:lifepunchaddons/config/packages.json` (no row) |
| Verdict | **Not ready alone.** Ships inside or after the bitcoin package. Option for the principal: put the $BTC tab behind an interface so the hub can ship without bitcoin |

### lifepunch.btc candidate (lpbitcoin + bitcoinmining)

| Item | State |
|---|---|
| Code size | 448,683 + 283,355 B. S1: ok |
| Assets (live) | 648,299,060 B / 552 files. Without `source/`, `_archive/` and source formats: **181,059,156 B / 401 files**. Compiled `_c` alone: 106,843,792 B / 91 files. Per entity: `hashdterminal` 432,370,996 B, `bitcoinhub` 118,743,276 B, `gpurack` 63,383,453 B, `_archive` 33,793,095 B |
| Assets (develop) | 532,643,087 B / 216 files, sources included. The stripped set fits S1; the raw tree does not |
| Cross-addon coupling | `lpbitcoin` and `bitcoinmining` reference each other (47 / 127 non-dev lines) in one namespace, so they **must ship as one package**. Both need `lifepunchcore`. `lpbitcoin` needs the loose `LpMenuShell.razor` (16 lines) and the shell SCSS (`LpHashdPanel.razor.scss:43`). All must be copied in (S6) |
| Dev surfaces in shipping folders | `lpbitcoin/bitcoinhub/code/components/LpBitcoinDevSpawn.cs`: 52 ConCmds. `bitcoinmining/LpBitcoinLedgerDevSpawn.cs`: 5 dev ConCmds + `LpBitcoinPurchaseDebugListener`. Exclude or gate |
| Package-mode blockers | no package branch (S5); entity bases split on `LIFEPUNCH_LOCAL` (`LpBitcoinHubEntity.cs:22-26`); Portal economy depends on a Portal inventory item and `ServerApiClient` (`LpBitcoinPortalEconomySync.cs:30`), so the Portal content must exist on the target game mode |
| Licence lines | 16 non-archive `ATTRIBUTION.md` files under `Assets/addons/lifepunch/lpbitcoin` say "Origin and licence left blank." (bitcoinhub 3, gpurack 11, hashdterminal 2) |
| Identity | `packages.json:23-24` names `lifepunchbitcoin` / **`lifepunch.bitcoin`**; the dispatch candidate name is `lifepunch.btc`. Needs a ruling |
| Stale procedure | `DXRP_ADDON_PUBLISH_DOCTRINE.md:118-136` describes a Code-mode bitcoin publish from the Steam tree (`D:\Steam\...\sbox\dxrp\game`). The editor now mounts the workbench; treat that section as historical |
| Verdict | **Highest value, most blockers.** Ready after: identity ruling, dev-surface exclusion, source strip (to ~181 MB), the attribution fields, a package build pass |

### lplockpick

| Item | State |
|---|---|
| Code size | 61,749 B. S1: ok |
| Assets | live 33,989,138 B / 35 files (ship subset 33,730,097 B; `_c` 10,543,596 B / 8). **develop: 2 files, 2,680 B.** The models are not on develop |
| Cross-addon coupling | none to other LifePunch units |
| Package-mode blockers | **`public partial class Door` in `namespace Dxura.RP.Game`** (`Door.Lockpick.cs:12-18`) extends DXRP's own partial `Door` (`dev:game/Code/World/Door/Door.cs:5`). That cannot cross an assembly boundary. Portal Code mode, where code compiles inside the game project, is not affected |
| Licence lines | no `ATTRIBUTION*`/`LICENSE*` file under `Assets/addons/lifepunch/lplockpick` (3 `.fbx` sources present) |
| Identity | not in `packages.json` |
| Verdict | **Code mode only, as written.** Package mode needs the `Door` extension redesigned (e.g. a component on the door instead of a partial). Assets must land on develop first |

### lptags

| Item | State |
|---|---|
| Code size | 404,769 B. S1: ok |
| Assets | live 132,719 B / 4 files; develop 130,236 B / 2 files (`RobotoMono-Regular.ttf`, `RobotoMono-LICENSE-OFL-1.1.txt`). The live-only files are `ATTRIBUTION.md` and one `.meta`, so **the attribution record is not on develop** |
| Cross-addon coupling | **none** (zero non-comment references to other LifePunch units) |
| Dev surfaces | `tags/code/dev/LpTagDevChecks.cs` (internal checks). Exclude or gate |
| Package-mode blockers | no package branch (S5); 12 `LIFEPUNCH_LOCAL` lines |
| Licence lines | Roboto Mono under OFL-1.1, licence file shipped, hash-checked against upstream on 2026-08-28 per its own record (`Assets/addons/lifepunch/lptags/tags/ui/fonts/ATTRIBUTION.md:7-15`). The same file says engine consumption is UNVERIFIED: no `@font-face` rule in the tree (`:36`) |
| Identity | README declares s&box ident `lifepunch.tags`, addon ident `lptags` (`lptags/tags/README.md:4`). No `packages.json` row yet (rows are owner-added, `PACKAGE_NAMING_STANDARD.md:47`) |
| Verdict | **Most ready.** Small, self-contained, licence evidenced, ident already declared |

### serverhub (Server Hub)

| Item | State |
|---|---|
| Code size | 92,543 B. S1: ok |
| Assets | none |
| Cross-addon coupling | **none** (comment mentions only) |
| Rails | the public DXRP host rail `GameManager.PurchaseMarketItemHost` (`public`, `dev:game/Code/GameManager.cs:336`), not the unsafe `PurchaseEntityHost` (`LpServerHubShopActions.cs:44-47`) |
| Package-mode blockers | no package branch (S5); 11 `LIFEPUNCH_LOCAL` lines. Whether it touches any `internal` DXRP member was not checked: UNVERIFIED |
| Product state | "v1 is a shell with one live tab" (`serverhub/README.md:6-7`). The banker socket is a VM only (`LpServerHubBankerSocket.cs:51`, `:62`) |
| Identity | not in `packages.json` |
| Verdict | **Packaging-ready, product-early.** Same packaging work as lptags; ship timing is a product call |

### keypadcracker, safe

| Item | State |
|---|---|
| Code | none. Each folder holds only `README_DESIGN.md` ("No code copied") |
| Identity | "Folder and portal identifier: OPEN (Q1)" (`keypadcracker/README_DESIGN.md`, `safe/README_DESIGN.md`) |
| Verdict | **Not packageable.** No code exists and the identifiers are open |

## Rows outside the candidate list (for completeness)

| Unit | Readiness note |
|---|---|
| `lifepunchcore` | Shared library; ships copied into each package (S6). **Open design question**: if two packages each bundle `LifePunchUpgradeLedger` and both mount in one game, two copies of the same `FileSystem.Data` ledger writer exist, and `ILifePunchPurchaseEvent` becomes two distinct types. Option: a single `lifepunch.core` library package that the others reference. How s&box resolves the same namespace across mounted packages is UNVERIFIED |
| `lpmorpheus` | Code 29,629 B; depends on `lifepunchcore`; **no assets on develop** (live 2,331,443 B / 37 files) |
| `lpweapons` | Code 51,650 B, no LifePunch coupling. **No assets on develop** (R-b pending). Live 3,107,563,422 B / 1,659 files; 10 files over 50 MB. Even outside `source/`/`provenance/`, `mp7a1/textures/textures/` holds three normal maps of 64-74 MB each |
| `ak47` | Quarantined in `lp:lifepunchaddons/config/portfolio.json:19` |
| `_dev` | Never ships (`AGENT_ONBOARDING.md:174`, `:517-519`) |

## Recommended order after lifepunch.ulx

A recommendation by packaging readiness only; product readiness and value are the principal's call.

1. **lptags** (`lifepunch.tags`): self-contained, about 0.4 MB of code plus a font, licence evidenced, ident already declared. Work: a `packages.json` row, bring the font `ATTRIBUTION.md` onto develop, exclude `LpTagDevChecks.cs`, a first package-mode build, Dev game mode proof.
2. **serverhub**: self-contained, no assets, a public DXRP rail. Its own README calls it a v1 shell, so it ships when the product is wanted.
3. **lifepunch.btc** (`lpbitcoin` + `bitcoinmining`, with `lifepunchcore` and the shell files copied in): rule the ident (`lifepunch.bitcoin` per `packages.json` vs `lifepunch.btc`), strip dev ConCmds and sources (to ~181 MB), fill the 16 blank attribution fields, decide the core-bundling question above, then a package build.
4. **playerhub**: with or after the btc package, unless it is decoupled.
5. **lplockpick**: Portal Code mode only, unless the `Door` partial is redesigned; its assets reach develop first.
6. **keypadcracker, safe**: not before code exists and Q1 is ruled.

## Rulings this page needs from the principal

- ULX's live s&box ident (the conflict table in `PACKAGE_SHAPE.md` section 2), before the next package copies its naming.
- R-f: which `rp.sbproj` identity (live `lifepunch`/`LifePunch` + ParentPackage, or develop `dxura`/`DXRP`) is the compile of record for package builds.
- The bitcoin package ident.
- Bundle `lifepunchcore` per package (current doctrine) or publish one `lifepunch.core` library.
- `packages.json` rows for `lptags`, `serverhub`, `playerhub`, `lplockpick`.
