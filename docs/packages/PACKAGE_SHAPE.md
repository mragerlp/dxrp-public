# Package shape: what a LifePunch addon package is

LifePunch internal working doc. It is not a dxrp.net/docs page (it is not listed in `docs/manifest.json`), the same as `docs/superpowers/`.
Sensed 2026-10-17. Every claim cites a source. **UNKNOWN** means no source read in this pass settles it. **UNVERIFIED** means a source says it but this pass did not re-sense it.
This page recommends and records. It rules on nothing: identity, publishing and ordering are the project owner's calls.

## Source keys

| Key | What it is |
|---|---|
| `dev:` | this repo, `develop` at `b3d3b525`, repo-relative paths |
| `live:` | the workbench live tree, read-only. Under `game/Code/Addons/lifepunch` it is content-identical to `dev:` (220/220 files equal after LF normalization, SHA-256, 2026-10-17). `game/Libraries` and most of `game/Assets` exist only in `live:` |
| `lp:` | the private `mragerlp/lifepunch` repo (read-only checkout) |
| `sb:` | the private `mragerlp/cavelux-switchboard` repo at `83f4891b` (head of PR #21, merged) |
| `ulx:` | the ULX package harness, a git checkout with remote `mragerlp/lifepunch.ulx`, HEAD `42bc387` |
| `rec:` | local release and acceptance records (not in git) |

## 1. There are two distribution systems. Do not mix them up.

**A. The DXRP Portal addon** (dxrp.net). An addon has two parts, packaged separately (`dev:docs/developers/creating-addons.md:9-12`):
- **Code**: extracted to `game/Code/Addons/<networkIdentifier>/<addonIdentifier>/` and compiled as part of the game project.
- **Assets**: extracted to `game/Assets/addons/<networkIdentifier>/<addonIdentifier>/`.
- Both identifiers are lowercase letters, numbers and hyphens, up to 64 characters. The addon identifier is unique across the whole platform and cannot be changed after creation (`creating-addons.md:14`, `:23`).
- A revision uploads the *contents* of the selected `<addonIdentifier>` folder. Selecting the parent folder makes the code extract one level too deep, and it then silently fails to compile (`creating-addons.md:40-41`).
- Size limits: code packages up to **50 MB**, asset packages up to **300 MB** (`creating-addons.md:45`). `lp:lifepunchaddons/docs/DXRP_ADDON_PUBLISH_DOCTRINE.md:135` says "<= 300 MB combined". That is older house wording; the upstream doc is `creating-addons.md`.

**B. The s&box package** (sbox.game). It is a project file plus a compiled payload.
- The project file is an `.sbproj` with `Title`, `Type`, `Org`, `Ident` (for example `live:game/Libraries/facepunch.libstates/libstates.sbproj:2-5`: Type `library`, Org `facepunch`, Ident `libstates`).
- The installed payload is `.bin/package.<org>.<ident>.cll` + `.xml`, plus `.bin/manifest.json` listing `package.base` and `package.<org>.<ident>` (`live:game/Libraries/facepunch.libstates/.bin/manifest.json:1-4`; the same shape appears for `ali3nsystems.lib-metrify`).
- All 40 `.sbproj` files under `live:game/Libraries` (searched to depth 2) are Type `library` (e.g. `libstates.sbproj:3`, `Metrify.sbproj:3`). None is a LifePunch addon.

**ULX uses both systems.** The s&box package is `lifepunch.ulx`. Its project file is `ulx:ulx.sbproj:2-18`: Type `addon`, Org `lifepunch`, Ident `ulx`, `Metadata.ParentPackage` `dxura.rp`, RootNamespace `LifePunch.DXRP.Addons.StaffMenu`, DefineConstants `SANDBOX;ADDON;LIFEPUNCH_PACKAGE`. The Portal addon carries no code of its own. Its revision **pins an s&box package revision in "Package" mode**: Portal Rev 18 changelog reads "Pin lifepunch.ulx s&box revision 394876 for Package-mode mounting" (`rec:CODEX_ULX_DEV_ACCEPTANCE_RECORD_20260928_W1.md:95-98`, a 2026-09-26 reading; the mode is inferred from changelog text only, `:132`).

`sb:extensions/sbox-kit/docs/FORK_WORKFLOW.md:46` describes the chain: local project -> published s&box package (LIFEPUNCH org; the org page listing is the Portal-link gate) -> DXRP Portal addon -> revision -> game-mode mapping -> running server.

## 2. Identity

| Layer | Rule | Source |
|---|---|---|
| package slug | `lifepunch{product}`, lowercase ASCII, no dots | `lp:lifepunchaddons/config/packages.json:5`, `lp:lifepunchaddons/docs/PACKAGE_NAMING_STANDARD.md:13-19` |
| s&box org | `lifepunch` | `packages.json:6`; `ulx:ulx.sbproj:4` |
| s&box ident | `lifepunch.{slug minus leading "lifepunch"}`, e.g. `lifepunch.bitcoin` | `packages.json:7-8`, `PACKAGE_NAMING_STANDARD.md:21` |
| Portal addon identifier | lowercase/digits/hyphens, <=64, platform-unique, immutable | `creating-addons.md:14`, `:23` |
| network folder on disk | the tree uses `lifepunch` (`game/Code/Addons/lifepunch/`, `game/Assets/addons/lifepunch/`) | `dev:` tree. That `lifepunch` equals the Portal network identifier is UNVERIFIED |
| display name | LIFEPUNCH™ leads; DXRP is named nominatively only; ™ not ® | `lp:lifepunch/docs/AGENT_ONBOARDING.md:42-48` |
| code namespace | `LifePunch.DXRP.Addons.*`; package `lifepunch.*` | `AGENT_ONBOARDING.md:373-374` |

**Open conflict: ULX's identifiers.** The records disagree, and this page does not pick one:

| Record | Says |
|---|---|
| `lp:lifepunchaddons/config/packages.json:15-16` | slug `lifepunchulx`, s&box `lifepunch.lifepunchulx` |
| `lp:lifepunch/docs/PUBLISH_REPO_LANE.md:13` | s&box `lifepunch.ulx` |
| Portal reading 2026-09-26 (`rec:W1.md:95`, the ULX acceptance record) | "Addon Identifier lifepunchulx; S&box Identifier lifepunch.ulx" |
| owner decision, 2026-10-01 | `lifepunch.lifepunchulx` retired and replaced by `lifepunch.ulx` |
| `sb:FORK_WORKFLOW.md:46` (2026-10-17) | Portal addon `lifepunchulx`, "collection shell `lifepunch.lifepunchulx`; the hidden `lifepunch.ulx` stays untouched" |

Settled: `lifepunch.ulx` is the live s&box ident and `lifepunch.lifepunchulx` is retired. The next package uses the same `lifepunch.<product>` form.

## 3. Dependencies on DXRP

- A package declares `Metadata.ParentPackage: dxura.rp` (`ulx:ulx.sbproj:14`). Both listed `lifepunch.ulx` revisions declare it, with no numeric parent revision (`rec:...W1.md:27`).
- The harness compiles the addon as **its own assembly** against `package.dxura.rp.dll` (`ulx:Code/lifepunchulx.csproj`: AssemblyName `lifepunchulx`, a Reference to `.sbox/bin/package.dxura.rp.dll` in the last ItemGroup). In Portal Code mode, the same folder compiles **inside** the game project (`creating-addons.md:11`). Three consequences follow for package mode:
  1. **No `partial` extension of DXRP types.** C# partial classes cannot span assemblies. `dev:game/Code/Addons/lifepunch/lplockpick/Door.Lockpick.cs:12-18` declares `namespace Dxura.RP.Game; public partial class Door`, the second half of DXRP's own `partial class Door` (`dev:game/Code/World/Door/Door.cs:5` and six sibling files). This works in Code mode and cannot work in Package mode.
  2. **No `internal` DXRP members.** ULX's in-game money grant calls `Player.PayHostStrictAudited` (`lifepunchulx/StaffMenuBridgeService.cs:433`), which is `internal` (`dev:game/Code/Player/Player.Roleplay.cs:304`) and was added on this fork (`git log -S PayHostStrictAudited` -> `5eeb6bf`). ULX fences that path with `#if !LIFEPUNCH_PACKAGE` (`StaffMenuBridgeService.cs:419`). This pass did not read the package-side branch.
  3. **Package builds take a different code path.** `LIFEPUNCH_PACKAGE` appears on 26 preprocessor lines (18 `#if` + 8 `#elif`), all in `lifepunchulx` (Select-String over the compile tree). No other addon has a package branch yet. A second symbol, `LIFEPUNCH_LOCAL` (255 `#if`/`#elif` lines across 9 addon folders and `LifePunchMarketSpawn.cs`), swaps entity bases, e.g. `LpBitcoinHubEntity : Component` vs `: BaseEntity` (`lpbitcoin/bitcoinhub/code/components/LpBitcoinHubEntity.cs:22-26`). Neither symbol is defined in `dev:game/rp.sbproj:29`, and no `#define` was found. Where `LIFEPUNCH_LOCAL` gets defined is UNKNOWN.

## 4. Code and asset layout

- **Code**: one folder per addon under `game/Code/Addons/lifepunch/<addon>/` (`creating-addons.md:31`). The house target layout puts per-entity `code/` and `assets/` under a package folder `lp{product}/`, with a per-package `.sbproj` planned for "Phase 6" (`lp:lifepunchaddons/docs/DXRP_ADDON_PUBLISH_DOCTRINE.md:29-39`, `PACKAGE_NAMING_STANDARD.md:26`, `:69`).
- **Shared primitives ship inside each package.** Files at the lifepunch code root are primitives only, and each published package is its own assembly; the publish bundle copies shared primitives into the package folder (`DXRP_ADDON_PUBLISH_DOCTRINE.md:154-170`). The ULX harness does this: its `Code/Addons/lifepunch/` holds `lifepunchcore/` (19 files) next to `lifepunchulx/` (file listing of `ulx:Code`).
- **Never ships**: dev helpers (`*DevSpawn*`, `*TestBots*`, `*DevGive*`, `_dev/`) (`lp:lifepunch/docs/AGENT_ONBOARDING.md:174`, `:517-519`); quarantined idents; `reference/`; third-party-modeled work (`AGENT_ONBOARDING.md:378`).
- **Every source file** (`.cs`/`.razor`/`.scss`) opens with the proprietary header block (`AGENT_ONBOARDING.md:274-276`).
- **Assets ship compiled `_c` files**, because the dedicated server does not compile, and must be self-contained with no cloud references (`RestrictCloudOrg="facepunch"`) (`AGENT_ONBOARDING.md:426-427`). Exclude source files (`source/`, `_archive/`, `provenance/`, `.fbx/.blend/.zip/.tga` sources) from the asset package. Section 6 of `PACKAGE_READINESS.md` measures what that leaves.
- **The game's resource whitelist** names the addon asset trees: `dev:game/rp.sbproj:8` (`addons/lifepunch/{playerhub,lpbitcoin,bitcoinmining,lpweapons,lifepunchulx,lptags,lplockpick,lpmorpheus}/**`, with excludes). This applies to the game project. Whether a Package-mode addon needs its own `Resources` line is UNKNOWN; `ulx.sbproj:8` has `Resources: null`.

## 5. Icons and listing art

UNKNOWN for the s&box package: neither `ulx.sbproj` nor `libstates.sbproj` carries an icon field. The Portal addon takes screenshots, a video, an about page, price and visibility from its manage page (`creating-addons.md:26`, `:55`). Brand marks follow `AGENT_ONBOARDING.md:42-48`. The bitcoin UI notes that "the HASHD mark does not exist yet" (`lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor:25`).

## 6. Versioning

- **Portal**: numbered revisions. Only the latest revision accepts uploads; older ones lock (`creating-addons.md:47`). Each revision takes a changelog and an optional target s&box version (`:42`).
- **s&box package**: numeric revision ids. `lifepunch.ulx` lists 394876 (created 2026-09-23, 7 files, 328434 B) and 397048 (created 2026-09-26, 7 files, 337625 B), both EngineVersion 29 (`rec:...W1.md:24-26`, `:70-74`, from the public `versions/2/lifepunch.ulx` endpoint as read 2026-09-28).
- **Coupling**: in Package mode the Portal revision names the s&box revision it mounts (`W1.md:96`). Rollback = point the Portal revision back at the previous s&box revision (`W1.md:57`).
- **Source**: the harness keeps a baseline commit per published revision (`ulx:` commit `069878b` "baseline of published lifepunch.ulx#394876 (43 files)") and a release branch `release/ulx-394876` (`W1.md:59`).

## 7. How lifepunch.ulx was published (the record, 2026-09-23 .. 2026-09-28)

1. A dedicated harness project holds `ulx.sbproj` (Type addon, ParentPackage `dxura.rp`), `Code/Addons/lifepunch/{lifepunchcore,lifepunchulx}`, an empty `Assets/`, tests, and `release-candidates/` (`ulx:` listing).
2. The s&box package `lifepunch.ulx` was published under the `lifepunch` org: revision 394876 on 2026-09-23, then 397048 on 2026-09-26 with changes "Revision 2: package-mode Store reads and website saves" (`W1.md:70-74`). Per a relayed screenshot, the upload wizard's pending files were `package.lifepunch.ulx.cll` + `.xml`, with four SCSS files and `manifest.json` uploaded or reused (`W1.md:77`). That item is relayed, not sensed; this pass inspected no screenshot.
3. The Portal addon (identifier `lifepunchulx`, Visibility Public) Rev 18 pins s&box revision 394876 in Package mode (`W1.md:95-98`).
4. Game-mode mapping: the Dev game mode `lifepunch.dxrpdev` lists `lifepunch.ulx` at Rev 18; the Official game mode `lifepunch.dxrp` did not carry ULX at that reading (`W1.md:91-105`).
5. Publishing is the owner's act; contributors prepare the candidate and the record, and only the owner publishes to sbox.game (`sb:FORK_WORKFLOW.md:46`).

What has changed since 2026-09-28 (current revision, Official mapping, whether 397048 was accepted) is UNVERIFIED: this pass made no Portal or sbox.game reading.

## 8. Checklist a next addon must satisfy

- [ ] **Identity ruled**: Portal addon identifier (immutable) and s&box ident under org `lifepunch`, per `packages.json:4-11`. ULX's live ident is `lifepunch.ulx` (section 2).
- [ ] **Mode chosen**: Portal Code mode (folder compiled into the game) or Package mode (s&box package + Portal pin). The rest of this list is for Package mode, the ULX road.
- [ ] **Own assembly compiles** against `package.dxura.rp.dll`: no `partial` extension of DXRP types, no `internal` DXRP members, a `LIFEPUNCH_PACKAGE` branch where the in-tree code relies on either.
- [ ] **No cross-addon compile dependency** that does not ship inside the same package. Shared primitives (`lifepunchcore`, loose root files the addon uses) are copied in (`DXRP_ADDON_PUBLISH_DOCTRINE.md:169`). See the graph in `CODE_CENSUS.md`.
- [ ] **Dev surfaces removed**: no `_dev/`, `*DevSpawn*`, `*TestBots*`, `*DevGive*`, no dev-only ConCmds.
- [ ] **Headers** on every `.cs`/`.razor`/`.scss`; LIFEPUNCH™-led title.
- [ ] **Assets**: compiled `_c` present, no cloud references, sources excluded, asset package <= 300 MB, code package <= 50 MB.
- [ ] **Attribution/licence fields** recorded for every third-party file that ships. The house default is that assets are ours unless the owner flags them (`AGENT_ONBOARDING.md:262-264`); `PACKAGE_READINESS.md` lists what the files say today.
- [ ] **Proof** before "done": runtime proof on a low-demand map (`lp:lifepunch/docs/START_HERE_AGENTS.md:65`), on the Dev game mode before Official (the ULX precedent, `W1.md:60`).
- [ ] **Record**: a baseline commit per published revision in the package harness; the revision ids written down; a rollback target named.
- [ ] **Publish by the owner's hand**: s&box package -> Portal addon -> New Revision (Package pin or Code/Assets upload, changelog, target s&box version) -> content entries if any -> visibility -> game-mode mapping (`creating-addons.md:16-57`, `FORK_WORKFLOW.md:46`).

## 9. Unknowns this page leaves open

- Which ULX s&box ident is live (section 2).
- Where `LIFEPUNCH_LOCAL` is defined; whether any build defines it.
- Whether a Package-mode addon needs its own `Resources` globs for its assets, and how its assets ship (`ulx:` has an empty `Assets/`; ULX ships no assets).
- s&box package icon/thumbnail requirements.
- The Portal network identifier string.
