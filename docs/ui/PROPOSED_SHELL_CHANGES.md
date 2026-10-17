# PROPOSED SHELL CHANGES - not made; each needs the principal's ruling and a live-tree writer

FROM: CC-SIDE-UI-1 [lane=LP-UI] -- side UI author (off-tree) -- Claude Code CLI -- Anthropic
AS-RUN: claude-opus-5-5 (self-declared, UNVERIFIED)
Dispatch: DISPATCH_CC-SIDE-UI-1_R1 (5704 B / 5AD8522C...8E4E), unit S2. Companion: `SHELL_CONTRACT.md`, `SHELL_AUDIT.md`.

**This seat changed none of the eight shared shell files.** They are byte-identical in this branch to the live tree
(ACT 0, 8/8 MATCH; the S5 diff names no file under them). Each row below is a proposal with `file:line` and a reason,
and it carries the ruling it waits on. Paths are relative to `game/Code/Addons/lifepunch/`.

| # | File : line | Proposed change | Reason | Waits on |
|---|---|---|---|---|
| P-1 | new file (e.g. `LpHouseShell.razor` + `.razor.scss`) | Extract the ULX top-tab grammar (SHELL_CONTRACT A1-A7) into one shared component: wordmark + subtitle, header-actions slot, tab list model, page head, tab host slot, footer. `_dev/shellkit/` in this branch is the executable draft of its markup and rules. | The house shell (R-2832-A) exists only as two standalone copies (`StaffMenu.razor:24-69`, `LpPlayerHubRoot.razor:18-88`); the BTC hub UI (DIRECTION sec. 4B) would make a third | R-1, R-3 |
| P-2 | `LpMenuShell.razor:225` | Default `AccentColor` from `"#e87d3e"` to `string.Empty`, and emit the inline `style` (`:66`, `:79`) only when it is set | The shared shell's default accent is the HASHD amber, a hex in razor; a new consumer inherits another product's colour | R-1 (moot if retired) |
| P-3 | `LpMenuShell.razor.scss:6-11` | Drop the six `$lp-shell-*` duplicates; `@import "LifePunchUiShell.scss"` and use `$lp-ui-*` | Same six values declared twice (`LifePunchUiShell.scss:14-19`); the shell markup uses `lp-ui-*` classes it never imports, so it depends on the consumer's import | R-1, R-3 |
| P-4 | `LifePunchUiShell.scss:169,234,321,372,380` | Replace the rule-body literals (`rgba(255,255,255,0.03)`, `#ffffff`, `#6b7280`, `#ef6b5f`) with `$lp-ui-*` tokens (new token lines for the dot colours) | Rule-body colour fails the hex gate on the next touch | R-3 |
| P-5 | `LifePunchCurrencyToken.razor.scss` (append after `:23`) | Define `.lp-money-btc`, `.lp-money-cash`, `.lp-money-amt`, `.lp-money-divider` once, from the house token surface | Sign colours are defined in 3 consumer sheets (`LpHashdPanel.razor.scss:6318`, `LpPlayerHubBtc.razor.scss:188-205`, `LpServerHubPrimitives.scss:16-21`); a consumer that defines none renders an uncoloured sign | R-10, R-3 |
| P-6 | `LifePunchUiFooter.razor.scss` (root copy) | Make `lifepunchcore/LifePunchUiFooter.razor.scss` the single source; repoint the two root-copy imports (`LpHashdPanel.razor.scss:42`, `LpBitcoinTerminalPanel.razor.scss:22`) at it, then delete the root copy | Two byte-identical copies today (3460 B / 85C5798A...FD58A); the next edit to one forks them | principal (deletion) |
| P-7 | `LifePunchUiFooter.razor.scss:27-28,69,77,83,93,105,109` | Move the footer literals (`#060606`, `#c8a45a`, `#8ec8ff`, white rgbas) to footer tokens | Rule-body colour; R-5 decides the mark colour | R-5, R-3 |
| P-8 | `LifePunchUiFooter.razor.scss:115-121` | Align `--rounded` to the house window radius (10px today) or retire it | 22px footer corners under a 10px ULX window (`StaffMenu.razor.scss:96`); UNVERIFIED visual | R-5, R-12 |
| P-9 | `LifePunchUiScale.scss:11` | Replace "s&box silently drops SCSS rules nested 4+ selectors deep." with a note that deep nesting is not an engine limit (editor-proven 2026-07-14) and shallow trees are style | A disproven engine claim in a shared file teaches the next reader a false law | none (comment only) |
| P-10 | `LpMenuShell.razor:65-68` | Two-part wordmark (`lifepunch` + `.<addon>`) and a subtitle line, if `LpMenuShell` survives | Brand slot of the house grammar (SHELL_CONTRACT A5) | R-1 |
| P-11 | `LpMenuShell.razor:324-336` | Document in a comment that `BuildHash` does not fold slot content, chip text or `DoorCard.Icon` | Readouts in `TopBarControls` (wallet, `LpHashdPanel.razor:117-120`) refresh only when the consumer rebuilds | none (comment only) |

No change is proposed for the compound selectors (`LpMenuShell.razor.scss:118`, `LifePunchUiShell.scss:270,335,362,375,379`, footer `:108`).
They render today, and the project record says compound class selectors match on this engine build. They should be revisited only if a
capture disagrees.
