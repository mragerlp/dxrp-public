# SHELL AUDIT - who consumes the LIFEPUNCH menu shell, and where they diverge

FROM: CC-SIDE-UI-1 [lane=LP-UI] -- side UI author (off-tree) -- Claude Code CLI -- Anthropic
AS-RUN: claude-opus-5-5 (self-declared, UNVERIFIED)
Dispatch: DISPATCH_CC-SIDE-UI-1_R1 (5704 B / 5AD8522CDF5328A016CA8D3EB01A25FAF8D1C2F18BE07B23CC417F796C668E4E), unit S1.
Tree read: worktree `.claude/worktrees/side-ui-1`, branch `cc-side-ui-1/SHELL-1-20261017`, base `origin/develop` b3d3b525.
The eight shell files in this tree are byte-identical to the live tree (ACT 0, 8/8 MATCH, both trees hashed).
Method: read-only. Sensors are named per row: `Get-FileHash`, `Select-String` / ripgrep over
`game/Code/Addons/lifepunch` (`*.razor`, `*.scss`, `*.cs`), and whole-file reads of the shell files.
Nothing here was rendered. **Every visual consequence below is UNVERIFIED in the editor.**

All paths are relative to `game/Code/Addons/lifepunch/` unless they start with `docs/`.

---

## 0. The headline - two shells, and the law names the other one

| | `LpMenuShell` (the eight shared files) | ULX top-tab grammar (`lifepunchulx/StaffMenu.razor`) |
|---|---|---|
| Navigation | **left sidebar** (`LpMenuShell.razor:28-62`) | **top tab strip** (`StaffMenu.razor:49-69`, `.razor.scss:375-407`) |
| Brand | `Wordmark` string in the header, uppercase, coloured by `AccentColor` (`LpMenuShell.razor:65-68`) | `lifepunch` + `.ulx` two-span wordmark + `title-sub` line (`StaffMenu.razor:26-29`) |
| Header widgets | one `TopBarControls` render slot (`LpMenuShell.razor:69-71`) | `header-chip`, `lp-ui-chrome-btn` cog, `chrome-close` (`StaffMenu.razor:31-46`) |
| Palette | website mirror: `#000000` / `#1a1d23` / `#374151`, accent `#017aef` (`LifePunchUiShell.scss:14-24`) | ULX: `#17181a` / `#1e1f21` / `#343536`, blue `#1689ff` (`StaffMenu.razor.scss:14-30`) |
| Consumers | **1**: `LpHashdPanel` | ULX itself, plus a renamed **copy** in PlayerHub |

R-2832-A (dispatch LAW) names "the ULX top-tab grammar" as the house shell for job menus. The shared shell
files implement a **different** grammar (sidebar), and nothing in them renders a top tab strip, a
`lifepunch.<addon>` two-part wordmark, a subtitle line or header pills. **The house shell exists only as
copies inside two menus. No shared file holds it.** This is ruling R-1 in `SHELL_CONTRACT.md`.

---

## 1. Shell parts - what exists in the shared files

| Part | File : line | Notes |
|---|---|---|
| Frame 1160 x 700, max 96%, radius 12 | `LpMenuShell.razor.scss:13-28` | ULX frame is 1160 x 700, max 94% / 92%, radius 10 (`StaffMenu.razor.scss:88-99`); Tags 1160 x 700, 94vw / 92vh (`LpTagsTokens.scss`) |
| Sidebar nav + settings foot | `LpMenuShell.razor:28-62`, `.razor.scss:60-130` | `NavItem` model `:237-252` |
| Header: wordmark + widget slot | `LpMenuShell.razor:65-72`, `.razor.scss:142-179` | wordmark is `text-transform: uppercase` 22px/900 (`.razor.scss:156-171`) |
| Dashboard page (door cards, snapshot, quick buttons) | `LpMenuShell.razor:75-204` | rendered only when the active `NavItem.IsDashboard` |
| Child content slot | `LpMenuShell.razor:205-208` | every non-dashboard page |
| Footer | `LpMenuShell.razor:215-217` -> `lifepunchcore/LifePunchUiFooter.razor:19-34` | params `Product`, `AccentColor` (`:37-38`) |
| Panel primitives (`lp-ui-*`) | `LifePunchUiShell.scss:33-427` | **not imported by `LpMenuShell.razor.scss`** although the shell markup uses `lp-ui-panel`, `lp-ui-panel-title`, `lp-ui-intro`, `lp-ui-status-dot`, `lp-ui-btn-row`, `lp-ui-btn-ghost` (`LpMenuShell.razor:66,80,86,90-91,105,151-152,191,297`) |
| Scale S/M/L/XL | `LifePunchUiScale.scss:28-52` | rules target only `.lp-ops-crt.lp-ui-size-* .shell` (CRT). Two C# twins: `lifepunchcore/LifePunchUiScale.cs:16-47` and `bitcoinmining/LpUiScale.cs:22-27` |
| Currency token | `LifePunchCurrencyToken.razor:17-35`, `.razor.scss:12-23` | defines **no** sign colours; `lp-money-btc` / `-cash` / `-amt` are defined only in consumer sheets (sec. 4) |
| Footer chrome | `LifePunchUiFooter.razor.scss:12-130` | two byte-identical copies: root and `lifepunchcore/` (both 3460 B / 85C5798A...FD58A) |
| Tab deep link | none in the shell | PlayerHub owns `playerhub_tab` (`playerhub/code/ui/LpPlayerHubHost.cs:62-81`) |
| Esc to close | none in the shell | see sec. 5 |

---

## 2. Consumers - part by part

### 2.1 `lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor` (Bitcoin Ops) - the only `LpMenuShell` consumer
- Uses `LpMenuShell` with `Wordmark`, `AccentColor`, `NavItems`, `DoorCards`, `SnapshotStats`, `QuickButtons`, `Mark`, `TopBarControls`, `ChildContent` (`:91-934`); builders `:1037-1180`.
- Wordmark is `"Bitcoin Ops"` (`bitcoinmining/LpBitcoinIdent.cs:20`), not `lifepunch.<addon>`.
- **Hard-coded colour in razor:** `AccentColor="#e87d3e"` at `:92`, and again on the pin-gate footer `:85`.
- **Second shell copy:** the PIN gate (`:17-87`) renders its own `.shell` / `.shell-body` and its own footer outside `LpMenuShell`.
- Imports `LifePunchUiShell.scss` (`.razor.scss:43`), root footer scss (`:42`), `bitcoinmining/LpUiChrome.scss` (`:40`), `LpUiScale.scss` (`:41`), `LpUiMenuLayout.scss` (`:44`).
- 30 local colour variables in `LpHashdPanel.razor.scss` (token-surface census, sec. 4); defines `.lp-money-btc` at `.razor.scss:6318`.
- Root class `lp-bitcoin-ops lp-ui-accent-hashd <LpUiScale>` (`:2427`). Scale uses the `bitcoinmining` twin.
- Header pills: `header-chip wallet-chip` and `cashout-all-chip` as raw buttons inside `TopBarControls` (`:117-124`), styled locally (`.razor.scss:597`).

### 2.2 `lifepunchulx/StaffMenu.razor` (ULX Console) - owner of the house grammar, uses no `LpMenuShell`
- Header / wordmark / sub / chips / chrome buttons `:24-46`; top tabs `:49-69`, `TabClass` -> `"tab tab-sel"` `:1640`; styles `.razor.scss:88-407`.
- Uses footer only: `<LifePunchUiFooter Product="ULX Console" />` inside `lp-window-watermark lp-window-watermark--rounded` (`:1581-1582`); imports the `lifepunchcore` footer copy (`.razor.scss:62`).
- **Radius mismatch (UNVERIFIED visual):** `--rounded` rounds the footer `0 0 22px 22px` (`LifePunchUiFooter.razor.scss:115-121`); the ULX window radius is 10px (`StaffMenu.razor.scss:96`).
- Scale: `LifePunchUiScale.CssClass( FrozenUiScale )` (`:3636`, `:4032`) - the `lifepunchcore` twin.
- Own palette, 29 colour variables (`.razor.scss:14-60`); `$blue: #1689ff` (`:29`); `DefaultAccentHex = "#1689ff"` (`StaffMenu.razor:3639`); per-tab accent through inline `style` (`TopTabAccentStyle`, `:52,61,65`).
- No tab deep link (ConCmds `menu` / `ulx` only, `StaffMenuHost.cs:598,601`).

### 2.3 `playerhub/code/ui/LpPlayerHubRoot.razor` (Player Hub) - a renamed copy of the ULX grammar
- `hub-shell-header` / `title-stack` / `lifepunch` + `.hub` / `title-sub "PLAYER HUB"` / `header-chip` x2 / `chrome-close` (`:19-39`); `hub-shell-tabs` + `tab` / `tab-sel` (`:41-48`, `:183`).
- Styles duplicate the ULX rules under new names (`LpPlayerHubRoot.razor.scss:38-209`); tokens restated "moved to the ULX grammar" (`LpPlayerHubTokens.scss:7-10`), `$lp-blue: #1689ff` with an in-file FLAG (`:9`, `:20`).
- Footer: `<LifePunchUiFooter Product="Player Hub" />` with **no** `lp-window-watermark` wrapper (`:88`).
- Deep link: `playerhub_tab <tab>` (`LpPlayerHubHost.cs:62-81`); open: `playerhub`, `hub` (`:52-56`). The only tab deep link in the tree.
- Header `$LP` pill writes the sign as a raw span `<span class="lp-mark">$LP</span>` (`:32`), not the currency token.
- Does not use `LpMenuShell`, `LifePunchUiShell.scss` or `LifePunchCurrencyToken`.

### 2.4 `serverhub/code/ui/LpServerHubRoot.razor` (Server Hub) - a third grammar
- Brand `LIFEPUNCH` + `Server hub` (`:20-23`), not `lifepunch.<addon>`.
- **Sidebar** nav `hub-nav-item` / `is-active` (`:47-62`, `:154-158`), own footer `hub-foot` (`:102-106`) - **does not use `LifePunchUiFooter`**.
- Money pills as raw sign + amount spans (`:28-36`) - Law 17 shape, but duplicates the currency token.
- Own token surface mirroring native DXRP (`LpServerHubTokens.scss`: `$bg #111111`, `$accent #7170e6`); `.lp-money-cash` / `-amt` defined in `LpServerHubPrimitives.scss:16-21`.
- Banker is an empty tab socket (`Banker/LpServerHubBanker.razor`, `LpServerHubRoot.razor:92-96`). Open: `serverhub`, `lp_serverhub` (`LpServerHubHost.cs:65-70`).

### 2.5 `lptags/tags/code/ui/LpTagsMenu.razor` (Tags) - a fourth grammar
- BEM classes `lp-tags-menu__*`; wordmark `lifepunch` `.` `t` `a` `g` `s`, one span per letter (`:28-31`), title `NAME APPEARANCE` (`:32`).
- **Rail** (sidebar) nav (`:39-47`), own footer (`:221-249`) - no `LifePunchUiFooter`.
- Own token surface `LpTagsTokens.scss` (30 colour variables; sizes incl. `calc(25% - 7.5px)`).
- **Esc to close:** consumes `Input.EscapePressed` and calls `RequestClose()` (`:314-318`) - the in-tree exemplar for the kit.
- `TAGS_RUNTIME_GATES.md:47`: "Reuse-first comparison against the standing LifePunchUiShell token surface remains open."
- R-2832-A names "Tags + Aura as one menu"; no Aura menu exists in this tree (razor census below).

### 2.6 `bitcoinmining/LpBitcoinTerminalPanel.razor` (HASHD terminal) - CRT, exempt
- CRT frame `shell` / `crt-frame` / `crt-header` (`:17-21`); imports `LifePunchUiShell.scss` and the root footer scss (`.razor.scss:22-23`); `<LifePunchUiFooter Product="Bitcoin Terminal" />` (`:155`).
- The only consumer the `LifePunchUiScale.scss` CRT rules are written for. Exempt from the panel standard (HASHD CRT exemption).

### 2.7 Not found
- `lifepunch.banker`, `lifepunch.chemist`: **no menu exists.** Sensor: `*.razor` census over `game/Code/Addons/lifepunch` (25 files, listed in checkpoint CP1 of the RETURN; menu roots among them: the six in sec. 2.1-2.6, plus `lifepunchulx/observe/*`, `lplockpick`, `lpmorpheus` and `lptags/.../LpTagName.razor`, which are overlays or readouts, not menus). Banker exists only as the empty Server Hub tab.
- Aura menu: none.

---

## 3. Divergence summary (consumer x part)

| Consumer | Shell frame | Brand slot | Nav | Header pills | Footer | Scale | Tokens | Currency token |
|---|---|---|---|---|---|---|---|---|
| LpHashdPanel | LpMenuShell (+ own PIN-gate copy) | "Bitcoin Ops" | LpMenuShell sidebar | raw chips in slot | shared (via shell + own copy) | `LpUiScale` twin | 30 local + `lp-ui-*` | yes (6 sites) |
| StaffMenu (ULX) | own | `lifepunch.ulx` + sub | **top tabs** (house) | own `header-chip` | shared, `--rounded` | `LifePunchUiScale` twin | own 29 | no |
| PlayerHub | own copy of ULX | `lifepunch.hub` + sub | copy of ULX tabs | own copy of `header-chip` | shared, no wrapper | none | `LpPlayerHubTokens` (ULX values) | no (raw `$LP` span) |
| ServerHub | own | `LIFEPUNCH` / `Server hub` | own sidebar | own money spans | **own** | none | `LpServerHubTokens` (native) | no (raw spans) |
| Tags | own | per-letter `lifepunch.tags` | own rail | none | **own** | none | `LpTagsTokens` | no |
| HASHD terminal | CRT | "HASHD TERMINAL" | n/a | n/a | shared | CRT scale | 9 local + `lp-ui-*` | no |

Counts: 6 menu roots audited; **1/6** uses `LpMenuShell`; **4/6** use `LifePunchUiFooter`; **2/6** carry the
house top-tab grammar, and both of those are standalone copies; **0/6** take their palette from a single shared surface.

---

## 4. Token surfaces and hard-coded colour

Sensor: `Select-String '^\s*\$[A-Za-z0-9_-]+\s*:\s*(#[0-9A-Fa-f]{3,8}|rgba?\()'` over every `*.scss` under
`game/Code/Addons/lifepunch`, counted per file (11 files define colour variables):

| File | colour vars | family |
|---|---|---|
| `LifePunchUiShell.scss` | 15 | website mirror (`#000` / `#1a1d23` / `#374151`, `#017aef`) |
| `LpMenuShell.razor.scss` | 6 | **same six values renamed** `$lp-shell-*` (`:6-11`) - a duplicate surface |
| `lifepunchulx/StaffMenu.razor.scss` | 29 | ULX |
| `playerhub/code/ui/LpPlayerHubTokens.scss` | 22 | ULX values |
| `serverhub/code/ui/LpServerHubTokens.scss` | 24 | native DXRP mirror |
| `lptags/tags/code/ui/LpTagsTokens.scss` | 30 | tags |
| `lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor.scss` | 30 | local (a `.razor.scss` defining colour vars is a finding, not a surface) |
| `bitcoinmining/LpBitcoinTerminalPanel.razor.scss` | 9 | local (CRT) |
| `bitcoinmining/LpUiChrome.scss` | 2 | chrome |
| `lifepunchulx/observe/StaffObserve.razor.scss`, `StaffChatPanel.razor.scss` | 1 each | local |

**`$lp-blue` split (OPEN ruling, not settled here):** `#1689ff` at `StaffMenu.razor.scss:29`, `StaffMenu.razor:3639`,
`LpPlayerHubTokens.scss:20` (+ rgba twins `:22-24`), and as rule-body literals in `StaffObserve.razor.scss:139,160-162,408,427,660`;
`#017aef` at `LifePunchUiShell.scss:20-23`. Sensor: `Select-String '1689ff|017aef|1,\s*122,\s*239|22,\s*137,\s*255'` over lifepunch `*.scss` / `*.razor`.

**Raw colour in the shell's own rule bodies** (would fail the hex gate on a touched line):
`LifePunchUiShell.scss:169,234` (`rgba(255,255,255,0.03)`), `:321` (`#ffffff`), `:372` (`#6b7280`), `:380` (`#ef6b5f`);
`LifePunchUiFooter.razor.scss:27-28,69,77,83,93,105,109`. **Raw colour in razor:** `LpMenuShell.razor:225`
(default `AccentColor = "#e87d3e"` - the shell's default accent is HASHD amber, not a brand token), inline `style="color: @AccentColor"`
at `:66` and `:79`; `LpHashdPanel.razor:85,92`.

**Sign colours have no shared home:** `.lp-money-btc` is defined in `LpHashdPanel.razor.scss:6318` and
`playerhub/code/ui/Btc/LpPlayerHubBtc.razor.scss:188`; `.lp-money-cash` / `-amt` in `LpPlayerHubBtc.razor.scss:193-202` and
`serverhub/code/ui/LpServerHubPrimitives.scss:16-21`. `LifePunchCurrencyToken` and `LpMenuShell` emit these classes
(`LpMenuShell.razor:285,288`) but define none, so a consumer that defines none renders an uncoloured sign (UNVERIFIED visual).

---

## 5. Other findings

- **Stale engine claim in a shell file:** `LifePunchUiScale.scss:11` says "s&box silently drops SCSS rules nested 4+ selectors deep".
  The lifepunch-design-tokens skill records that claim as DISPROVEN (2026-07-14, seven levels applied). Proposed comment fix: `PROPOSED_SHELL_CHANGES.md` P-7.
- **Compound selectors in the shell** (`.sidebar-btn.sidebar-btn-sel` `LpMenuShell.razor.scss:118`; `&.accent` `LifePunchUiShell.scss:270`;
  `&.disabled` `:335,:362`; `&.on` / `&.off` `:375,:379`; `&.copied` `LifePunchUiFooter.razor.scss:108`). The razor-ui skill treats compounds as suspects;
  the project memory records compound class selectors capture-proven to match in 26.08.19. Reported, not judged.
- **Uppercase chrome:** shell wordmark (`LpMenuShell.razor.scss:167`), `lp-ui-panel-title` (`LifePunchUiShell.scss:103`), `lp-ui-btn-primary` (`:325`).
  The razor-ui skill says sentence case except tiny eyebrows. Canon file not resolvable in this tree (next bullet).
- **Canon pointers do not resolve here:** `LIFEPUNCH_UI_STANDARD.md`, `SBOX_RAZOR_SCSS_RULES.md`, `SBOX_CONTEXT.md` are absent from this worktree
  (sensor: recursive filename search over the worktree, `.git` excluded; the positive control is `docs/superpowers/specs/2026-10-16-playerhub-ulx-btc-design.md`, which the same search found).
  Every token or law the skills cite is UNVERIFIED product canon in this tree.
- **Esc to close:** a consumer exists only in `LpTagsMenu.razor:314-318` (and `lplockpick/LockpickMinigame.razor:167-169`). No hit in StaffMenu, PlayerHub, ServerHub or LpHashdPanel.
  Sensor: `Select-String 'Escape|EscapePressed'` over lifepunch `*.cs` / `*.razor`; the Tags hit is the positive control. A handler under another name would be missed: the negative is UNVERIFIED.
- **Two binding idioms:** `LpMenuShell` declares `[Property]` parameters (`:223-231`); `LifePunchCurrencyToken` (`:27-31`) and `LifePunchUiFooter` (`:37-38`) use plain public properties. Both compile in-tree today (consumers exist); the kit follows each component's own idiom.
- **Shell depends on its consumer's imports:** `LpMenuShell` markup uses `lp-ui-*` classes it does not import (sec. 1). A new consumer that forgets `@import "LifePunchUiShell.scss"` gets an unstyled dashboard (UNVERIFIED visual). The kit imports it explicitly.
