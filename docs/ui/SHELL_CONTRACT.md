# SHELL CONTRACT - the LIFEPUNCH menu shell, as the bytes define it

FROM: CC-SIDE-UI-1 [lane=LP-UI] -- side UI author (off-tree) -- Claude Code CLI -- Anthropic
AS-RUN: claude-opus-5-5 (self-declared, UNVERIFIED)
Dispatch: DISPATCH_CC-SIDE-UI-1_R1 (5704 B / 5AD8522CDF5328A016CA8D3EB01A25FAF8D1C2F18BE07B23CC417F796C668E4E), unit S2.
Companion files: `SHELL_AUDIT.md` (S1, evidence), `PROPOSED_SHELL_CHANGES.md` (changes to the eight shared files; none made).
**Nothing in this contract has been rendered. Every visual statement is UNVERIFIED in the editor.**
Paths are relative to `game/Code/Addons/lifepunch/`.

## 0. Which shell this contract describes (judgement, disclosed)

The dispatch asks for "the LpMenuShell usage contract from the bytes" and lists, as its parts, a tab grammar with
ids, order, active state and deep links, header pills, a footer line, and a `lifepunch.<addon>` branding slot with
a subtitle. `LpMenuShell` has none of the top-tab, two-part-wordmark, subtitle or pill parts (SHELL_AUDIT sec. 0-1).
Those parts exist in the bytes of the **ULX top-tab grammar**: `lifepunchulx/StaffMenu.razor:24-69` and its
token-driven port `playerhub/code/ui/LpPlayerHubRoot.razor:18-88`. The dispatch's LAW line (R-2832-A) names that
grammar as the house shell, and `DIRECTION_BTC-UI_R1` sec. 3 W1 names the `LpMenuShell` sidebar app as the
divergence to retire ("the hub entity opens an orange Poppins app with a sidebar").

So this contract has two parts:
- **Part A, the house shell (R-2832-A):** the grammar new menus must speak. It is derived from the two
  in-tree carriers. **No shared file holds it today**, so a new menu ports it, the way PlayerHub did. Extracting it
  into one shared component is proposal P-1 and ruling R-1.
- **Part B, the shared files as they are:** what `LpMenuShell`, the footer, the currency token, the shell SCSS and
  the scale offer to a consumer today, including the legacy sidebar API.

If the principal reads R-2832-A differently, Part A is the part that changes. Part B is a description of bytes and
holds either way.

---

## PART A - the house shell (ULX top-tab grammar)

### A1. Structure (one tree, in this order)

```
root.<menu-root> (+ is-open)                    full-screen backdrop, centres the window
  .window                                       1160 x 700, max 94% x 92%
    .<p>-shell-header                           brand left, header actions right
      .<p>-shell-title > .title-stack
        span.title-primary.brand-wordmark
          span.wordmark-life  "lifepunch"
          span.wordmark-accent ".<addon>"       accent suffix (ULX names it .ulx)
        span.title-sub  "<MENU NAME>"           uppercase eyebrow
      .header-actions
        .header-chip  x n                       pills: icon or sign + short value, tooltip
        button.lp-ui-chrome-btn.chrome-close    close, always last
    .<p>-shell-tabs                             one row of text tabs
      button.tab | button.tab.tab-sel  x n
    .<p>-workspace
      .hub-page-head  .hub-title + .hub-blurb [+ status chip]
      .hub-tabhost    the active tab's component; one scroll region inside it
    LifePunchUiFooter Product="<Menu name>"
```
Sources: `StaffMenu.razor:22-69,1581-1582`; `LpPlayerHubRoot.razor:17-90`. `<p>` is the menu prefix (`ulx`, `hub`).
Both carriers use the class names above, apart from the prefix. A new menu uses `hub-shell-*` (the PlayerHub
spelling) unless R-1 rules a shared component with its own names.

### A2. Geometry and type (values from the bytes)

| Part | Value | Source |
|---|---|---|
| Backdrop | full screen, `rgba($canvas, 0.86)`, mono font | `LpPlayerHubRoot.razor.scss:11-25`; `StaffMenu.razor.scss:64-75` |
| Window | 1160 x 700, max 94% / 92%, 1px `$border`, radius `$radius-card` (10px), shadow `0 18px 44px rgba($canvas,.64)` | `LpPlayerHubRoot.razor.scss:38-51`; `StaffMenu.razor.scss:88-99` |
| Header | min-height 82px (`$header-h`), padding 15px 20px, bottom hairline | `:54-63`; `StaffMenu.razor.scss:101-110` |
| Wordmark | 24px / 700, letter-spacing 0.02em; suffix `$lp-blue` with `text-shadow 0 0 12px rgba($lp-blue,.58)` | `:77-96`; `StaffMenu.razor.scss:191-209` |
| Subtitle | 13px, uppercase, 0.08em, `$text-dim` | `:98-103`; `StaffMenu.razor.scss:211-223` |
| Header pill | min-height 32px, padding 0 10px, gap 6px, 1px `$border`, radius `$radius-chip` (7px), transparent, `$text-dim`, 13px | `:111-123`; `StaffMenu.razor.scss:267-277` |
| Chrome button | 32 x 32, radius 7px, `rgba($text-dim,.28)` edge, close glyph 22px in `rgba($bad,.82)` | `:145-176`; `StaffMenu.razor.scss:286-356` |
| Tab strip | min-height 43px (`$tabs-h`), gap 18px, padding 0 20px, bottom hairline | `:179-188`; `StaffMenu.razor.scss:375-383` |
| Tab | height 43px, 13px, `$text-dim`, 2px transparent underline; hover `$text-main` | `:190-206`; `StaffMenu.razor.scss:385-400` |
| Active tab | standalone `tab-sel`: `$text-main`, 700, underline `$lp-blue`, no fill | `:208-213`; `StaffMenu.razor.scss:402-407` |
| Workspace | padding 14px 20px 12px, gap 10px (`$gap-dense`) | `:216-224` |
| Page head | title 18px / 700; blurb 11px `$text-dim` behind a left hairline | `:226-245` |
| Tab host | `$surface` card, 1px `$border`, radius 10px, `overflow: hidden` | `:247-256` |
| Footer | `LifePunchUiFooter`, 32px, three zones | `lifepunchcore/LifePunchUiFooter.razor:19-34`; `LifePunchUiFooter.razor.scss:12-32` |

### A3. Tab grammar

- **Ids:** an enum per menu (`LpPlayerHubTab`, `playerhub/code/data/LpPlayerHubModels.cs:14`). Labels and blurbs
  come from one static info class (`LpPlayerHubTabInfo.Label` / `.Blurb`, `:611-633`). ULX uses string categories
  (`StaffMenuActions.Categories`) plus two named tabs (`StaffMenu.razor:50-68`).
- **Order:** a display-order array that is independent of enum order (`LpServerHubRoot.razor:116-124` states the reason).
  It must be an **instance** field, not `static readonly`: hotload keeps a static readonly field's old value
  (`LpPlayerHubRoot.razor:97-107`, a defect seen on 2026-10-16).
- **Active state:** one private field; the razor emits `"tab tab-sel"` or `"tab"` (`LpPlayerHubRoot.razor:183`,
  `StaffMenu.razor:1640`). Selecting the active tab again is a no-op; any other selection sets the field and calls `StateHasChanged()` (`:188-195`).
- **Deep link:** `<menu>_tab <name>` ConCmd, which opens the menu if it is closed and then selects the tab through the
  same path as a click. Names parse case-insensitively against the enum; an unknown name logs a warning that lists the valid
  names and leaves the default tab (`LpPlayerHubHost.cs:58-81`). Only PlayerHub has one today (R-8).
- **Visibility:** every tab renders for every viewer; actionability is decided elsewhere and shown as disabled state
  (`StaffMenu.razor:57-60`, R-2639-A / R-2640-A).

### A4. Header pills

A pill is a `.header-chip`: an icon or a currency sign, a short value and a `tooltip`. A pill **reads**; it does not
drive the menu. A clickable pill (ULX copy-link, `StaffMenu.razor:32-37`) states its action in the tooltip and shows
feedback (`header-chip-copied`). A status pill (LIVE / PREVIEW) sits in the page head, not the header
(`LpPlayerHubRoot.razor:54-61`). A money pill uses the currency token (sec. A6); `$LP` stays the temporary blue
`lp-mark` (`LpPlayerHubRoot.razor:31-34`, `.razor.scss:125-128`) until its sign and colour are ruled.

### A5. Branding slot

`lifepunch` + `.<addon>`: the s&box ident suffix, lowercase, in two spans; then the subtitle line in uppercase
(`LpPlayerHubRoot.razor:21-24`). The footer `Product` is the human menu name ("Player Hub", "ULX Console").
DIRECTION_BTC-UI_R1 sec. 4B fixes `lifepunch.btc` for the Bitcoin Hub UI. Per-menu accent colour on the suffix is R-4.

### A6. Money (Law 17 and Law 15 as the bytes implement them)

- Use `LifePunchCurrencyToken` (`Sign`, `Amount`, `Label`, `SignClass`, `TokenClass`; `LifePunchCurrencyToken.razor:27-31`).
  The sign carries the colour (`lp-money-btc` for the BTC sign, `lp-money-cash` for `$`); the amount is `lp-money-amt` (white).
- Dual price: two tokens with a `<span class="lp-money-divider">|</span>` between them, each side coloured.
- The sign colours are **not** defined by the token; the menu's sheet must define `.lp-money-*` from its tokens
  (PlayerHub: `Btc/LpPlayerHubBtc.razor.scss:188-205`). Shared home: P-5 / R-10.
- A composite string (separator, unit) is built C#-side and rendered as one node (`LpServerHubRoot.razor:145-152`).

### A7. Behaviour every house menu owes (the QoL pass)

| Duty | How, with the exemplar |
|---|---|
| Open / close / reopen | static host: mount on open, destroy on close, fresh instance on every open; `GameManager.ShowUi<T>()`, then a `ScreenPanel` fallback; `Player.Local.LockCamera` while open (`playerhub/code/ui/LpPlayerHubHost.cs:35-196`) |
| Discover | a ConCmd to toggle the menu (`playerhub`, `hub`, `LpPlayerHubHost.cs:52-56`), plus a deep link (A3) |
| Close with Esc | consume `Input.EscapePressed` in `OnUpdate` and request close (`lptags/tags/code/ui/LpTagsMenu.razor:311-319`). **Missing in ULX and PlayerHub** (SHELL_AUDIT sec. 5; R-9) |
| Input block | `LifePunchMenuInputBlock.NotifyMenuOpened/Closed` in `OnEnabled` / `OnDisabled` (`LpPlayerHubRoot.razor:251-259`) |
| Scroll | `LifePunchUiScrollPolicy.Apply( Panel )` in `OnStart` and `OnUpdate` (`:236-249`); one scroll region per tab |
| Rebuild | `BuildHash` folds every readout the panel shows (`:261-273`); live data folds a live hash (`LpPlayerHubBtcSource.LiveHash()`) |
| Proof hook | a positive code-string constant and one `Log.Info` on first open (`:94`, `:239-243`) |

### A8. Do / do not

Do:
- Speak the A1 tree with these class names. State is a standalone class emitted by the razor (`tab-sel`), never a new compound selector.
- Take every colour, radius and size from one imported token surface; `$name:` lines live only in that token file.
- Give every readout **one state source**: two widgets that show the same figure read the same view-model property, with the same
  precision and unit (DIRECTION_BTC-UI_R1 W2, W3, W4, W7).
- Use `LifePunchUiFooter` and `LifePunchCurrencyToken`; do not restyle them locally beyond the footer mark colour (A6, R-5).
- Keep `@code` ASCII and escape display glyphs as C# unicode escapes (the BTC sign is code point U+20BF); state `flex-direction` on every layout class.

Do not:
- Build a second shell: a new sidebar menu, a new header grammar, or a fork of `LpMenuShell`.
- Write a colour in a rule body or in razor (`AccentColor="#e87d3e"`, `LpHashdPanel.razor:92`, is the counter-example).
- Use `static readonly` for anything a hotload must refresh; use `@media`, grid, `inline-flex`, or gradients; put two adjacent `@(...)` in one attribute.
- Move money from the shell. The shell calls existing host rails only; a new rail is the principal's GO, never the shell's.

---

## PART B - the shared files as they are

### B1. `LpMenuShell` (legacy sidebar grammar; 1 consumer: `lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor:91-934`)

Parameters (`[Property]`, `LpMenuShell.razor:223-231`):

| Name | Type | Default | Use |
|---|---|---|---|
| `Mark` | `RenderFragment?` | null | sidebar identity block (`:29-31`) |
| `Wordmark` | `string` | `""` | header title, footer `Product`, dashboard eyebrow (`:67`, `:79`, `:216`) |
| `AccentColor` | `string` | `"#e87d3e"` | inline `style="color: ..."` on the title and eyebrow; passed to the footer (`:66,79,216`) |
| `TopBarControls` | `RenderFragment?` | null | header widget slot (`:69-71`) |
| `ChildContent` | `RenderFragment?` | null | page body for every non-dashboard item (`:205-208`) |
| `NavItems` | `List<NavItem>` | empty | sidebar; `IsSettings` items go to the foot (`:34`, `:51`) |
| `DoorCards` | `List<DoorCard>` | empty | dashboard "Getting started" rows (`:96-143`) |
| `SnapshotStats` | `List<StatRow>` | empty | dashboard snapshot grid (`:157-182`) |
| `QuickButtons` | `List<QuickButton>` | empty | dashboard action row (`:192-200`) |

Models: `NavItem` (`:237-252`), `DoorCard` (`:254-269`), `StatusChip` + `StatusChipVariant { Info, Success, Locked, Count }`
(`:235`, `:271-275`), `StatRow` (`:277-290`), `QuickButton` (`:292-300`). Behaviour:
- The active page is the first `NavItem` with `IsActive` (`:302`). The consumer owns active state; the shell only reads it.
- If that item has `IsDashboard`, the shell renders the dashboard from the three lists; otherwise it renders `ChildContent` (`:75-208`).
- Door status dot: `StatusClass` if set, else Success/Count -> `on`, Locked/other -> `off` (`:304-316`).
- The footer note is the first non-empty `FooterNote` in the list (`:318-322`).
- `BuildHash` folds the listed fields only (`:324-336`). It does **not** fold chip text, `DoorCard.Icon`, or anything inside `Mark` /
  `TopBarControls` / `ChildContent`; a readout in those slots relies on the consumer rebuilding.
- `Route` fields on `NavItem`, `DoorCard`, `QuickButton` are data only. The shell never reads them.
- Styling: frame and sidebar in `LpMenuShell.razor.scss:13-255` with its own `$lp-shell-*` values (`:6-11`). The `lp-ui-*`
  classes the markup uses come from `LifePunchUiShell.scss`, which **the consumer must import** (SHELL_AUDIT sec. 1).

### B2. `LifePunchUiFooter` (`lifepunchcore/LifePunchUiFooter.razor`, outside the eight; styles: two byte-identical sheets)

Parameters: `Product` (string), `AccentColor` (string, which recolours the mark when set) (`:37-38`). Three zones: identity
(`LifePunchSourceMark.UiFooterMark` + product), legal "proprietary IP", copy-URL button with a 1.8 s "Link copied!" revert (`:19-55`).
Wrappers: `lp-window-watermark--rounded` (22px bottom radius) and `--boxed` (square) (`LifePunchUiFooter.razor.scss:114-130`).

### B3. `LifePunchCurrencyToken` (`LifePunchCurrencyToken.razor`)

Plain properties `Sign`, `Amount`, `Label`, `SignClass` (default `lp-money-btc`), `TokenClass` (`:27-31`). Renders sign, amount, and
an optional unit label with a 4px left margin (`:17-24`; `.razor.scss:12-23`). It defines no colours (A6).

### B4. `LifePunchUiShell.scss` (website-mirror primitives)

Layout utilities `lp-ui-workspace`, `lp-ui-chrome-slot`, `lp-ui-scroll-region`, `lp-ui-scroll` (`:33-54`, `:120-125`) carry no colour and
are safe in any grammar. The panel, list, info, perk, button and status-dot primitives (`:56-382`) and the `lp-ui-accent-hashd` overrides
(`:384-427`) carry the website-mirror palette (`:14-29`) and belong to that family (R-3).

### B5. Scale (`LifePunchUiScale.scss`; C# twins `lifepunchcore/LifePunchUiScale.cs`, `bitcoinmining/LpUiScale.cs`)

S / M / L / XL classes `lp-ui-size-s|m|l|xl` (`LifePunchUiScale.cs:28-34`). The SCSS sizes only `.lp-ops-crt.lp-ui-size-* .shell`, which
is CRT (`LifePunchUiScale.scss:28-52`). House menus are fixed at 1160 x 700 (ULX freezes XL, `StaffMenu.razor:3636`). Scale is
**not part of the house shell** unless R-11 rules otherwise.

---

## RULINGS OWED TO THE PRINCIPAL (none settled here)

| # | Question | Evidence | Options as the bytes allow (no pick) |
|---|---|---|---|
| R-1 | Is the shared shell `LpMenuShell` (sidebar) or the ULX top-tab grammar? | SHELL_AUDIT sec. 0; R-2832-A; DIRECTION W1 | extract the ULX grammar into a new shared component (P-1) and retire `LpMenuShell` with its one consumer; rework `LpMenuShell` in place (breaks `LpHashdPanel`); keep both and scope `LpMenuShell` |
| R-2 | **`$lp-blue`: ULX `#1689ff` or standard `#017AEF`** (OPEN ruling, recorded only) | `StaffMenu.razor.scss:29`; `LpPlayerHubTokens.scss:9,20`; `LifePunchUiShell.scss:20`; 2026-10-16 spec sec. 3 FLAG | - |
| R-3 | Which surface is the house token set? | 6 families (SHELL_AUDIT sec. 4); PlayerHub moved to ULX values with approval (2026-10-16 spec) | ULX neutrals; website mirror; native DXRP mirror (ServerHub) |
| R-4 | Wordmark accent: always `$lp-blue`, or a per-menu or per-user colour? | ULX user accent `StaffMenu.razor:27,3639`; PlayerHub fixed `$lp-blue`; Tags colours each letter | - |
| R-5 | Footer wrapper and mark colour | `--rounded` 22px vs window 10px (`LifePunchUiFooter.razor.scss:115-121`, `StaffMenu.razor.scss:96`); mark `#c8a45a` (`:69`) vs PlayerHub override to `$lp-blue` (`LpPlayerHubRoot.razor.scss:258-260`) | - |
| R-6 | `$LP` sign and colour in pills | temporary blue `lp-mark` (`LpPlayerHubRoot.razor:32`); PENDING per the design-tokens canon | - |
| R-7 | House font: Roboto Mono for the whole shell (ULX, PlayerHub) or Poppins + Inter (token lock; `LpMenuShell`)? | `StaffMenu.razor.scss:74`; `LpPlayerHubRoot.razor.scss:21`; `LpMenuShell.razor.scss:124,164` | - |
| R-8 | Deep-link name form: `<menu>_tab` (PlayerHub) or `lp_<menu>_tab` (house ConCmd form `lp_*`)? Is a deep link owed by every menu? | `LpPlayerHubHost.cs:62`; `LpServerHubHost.cs:65-70` | - |
| R-9 | Esc to close in every house menu (ULX and PlayerHub lack it) | SHELL_AUDIT sec. 5 | fix slice per menu; a shared host helper |
| R-10 | One shared home for `.lp-money-*` sign colours | 3 definitions (SHELL_AUDIT sec. 4) | in `LifePunchCurrencyToken.razor.scss` (P-5); per token surface |
| R-11 | Is scale (S-XL) part of the house shell? | sec. B5 | - |
| R-12 | Radius scale: tokens skill 6 / 12, razor-ui skill 2 / 4, ULX bytes 7 / 10 | `LpPlayerHubTokens.scss:40-43`; the two skills disagree with each other | - |
| R-13 | `LIFEPUNCH_UI_STANDARD.md` is cited as canon but absent from this tree | SHELL_AUDIT sec. 5 | point the skills at the file of record, or restore it |
