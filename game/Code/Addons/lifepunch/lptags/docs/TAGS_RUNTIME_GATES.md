<!--
PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.

"LIFEPUNCH™ Tags for DXRP" (s&box ident: lifepunch.tags · addon ident: lptags) is the sole-owned
intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
sublicensing, copying, or reuse by ANY person or entity — including DXRP and
LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
Third-party material identified by an accompanying notice remains under its stated license.
Presence in this repository or on the DXRP portal grants no rights to anyone else.

Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
-->

# LIFEPUNCH™ Tags — deferred runtime and design gates

Status: **UNVERIFIED**. Lane 4 provides static worktree evidence only and makes no runtime claim.
A trusted-seat editor/runtime pass remains mandatory before any compile, visual, performance,
replication, package, or shipping assertion.

## DEV-FENCE

Static source inspection shows that the `LIFEPUNCH_LOCAL` boundary encloses the diagnostic
entry point, while supporting diagnostic code in `code/dev/LpTagDevChecks.cs` remains outside
that boundary. Moving the bulk helper surface is an M-class structural change and was not
silently folded into this lane. Named follow-up work must fence the complete developer-only
surface, then compile-check both symbol profiles through the trusted editor path.

## SCHEDULER-CAPS

The scheduler and Unicode-segmentation paths have no trusted stress, frame-budget, player-cap,
or replication evidence in this lane. Named follow-up work must define the contractually
allowed caps, test boundary and over-boundary cases, and collect trusted editor/runtime sensors.

## FONT-SHIPPING

The staged `RobotoMono-Regular.ttf` has SHA-256 `AF0BFF7599C3DF3831755C16E39B3C496DF74B8C8D8A1161B14DC8461BE17CB4` and sfnt magic
`00010000`; those sensors prove only the staged bytes and TrueType container, not font identity
or engine consumption. Its OFL-1.1 license must equal upstream `googlefonts/robotomono/OFL.txt`
at SHA-256 `50AB8DD54680D3473F649C9DB86FECE88434D097C7834475C1C72D2F8C429215` / 4395 B;
substring matching is not an admissible license gate. Named follow-up work must prove the
engine-consumed font resource, package inclusion, SCSS lookup,
fallback behavior, and rendered Roboto Mono Regular result through trusted editor/runtime and
visual sensors.

## UI-SHELL-TOKENS

Reuse-first comparison against the standing `LifePunchUiShell` token surface remains open.
Lane 4 does not substitute local tokens without a field-level compatibility contract. Named
follow-up work must inventory matching semantic tokens, preserve Tags-specific values where no
shared contract exists, and submit any reuse diff through the Razor/SCSS static gate before a
trusted visual pass.

## Chat-command boundary

No `/tags` `ICommand` was added. The unsupported README claim that `tags` is an exact alias was
removed as the authorized alternative; the existing `[ConCmd( "tags" )]` source remains
undocumented as a slash-chat route. Any chat-command addition requires a separate engine-truth
contract and trusted runtime proof.

## Lane update -- OPUS-TAGS-MENU-2 (2026-08-28)

Route B ruled: finish lptags in place. Two gates moved. The gate text above is left exactly as
written; this section records what changed and does not rewrite the record.

### DEV-FENCE -- CLOSED

The fence now opens immediately after the file header of `code/dev/LpTagDevChecks.cs` and
encloses the entire diagnostic surface, so `LpTagPureChecks` and its nested helpers no longer
compile into production builds. The previously separate inner fence is redundant and is noted
as such in place, keeping the directive balance at one `#if` / one `#endif`.

Sensor: offline Roslyn parse of the file under both preprocessor symbol profiles, counting
type declarations that survive preprocessing.

| Profile | Before | After |
|---|---:|---:|
| no LIFEPUNCH_LOCAL (production) | 5 | 0 |
| LIFEPUNCH_LOCAL (dev) | 7 | 7 |

Diagnostics are fully preserved under the dev profile and absent from production. Both profiles
parse with 0 syntax errors, as do all 24 lptags C# files. The parse gate was validated with a
negative control -- a deliberately malformed file reports CS1525 / CS1513 -- so the zero is a
real result rather than a dead sensor.

NOT claimed: this is a Roslyn PARSE, not a build. No editor, runtime, or visual proof exists
for this lane, and the gates doc's standing UNVERIFIED status is unchanged by it.

### FONT-SHIPPING -- licence half CLOSED, engine half still OPEN

The licence requirement is met, with the proof recorded in the sibling
`Assets/addons/lifepunch/lptags/tags/ui/fonts/ATTRIBUTION.md`. The staged OFL text is
byte-identical to the pin of record once CRLF is normalised to LF: 4488 B as stored, 93 lines,
exactly 93 CR bytes; LF-normalised it is 4395 B with SHA-256
50AB8DD54680D3473F649C9DB86FECE88434D097C7834475C1C72D2F8C429215. That is an exact whole-file
equality with the required pin, not the substring match this document rules inadmissible. The
93-byte delta equals the CR count, which is the signature of serialization drift rather than an
altered document. The staged TTF matches its pin as stored (125748 B /
AF0BFF7599C3DF3831755C16E39B3C496DF74B8C8D8A1161B14DC8461BE17CB4).

That ATTRIBUTION table previously read "Origin and licence left blank"; Origin and Licence are
now filled for both files, and the licence file itself has been added as a row.

STILL OPEN -- engine consumption. `code/ui/LpTagsMenu.razor.scss:32` and `:87` request
`font-family: "Roboto Mono", "Courier New", monospace`, and `game/rp.sbproj` Resources already
covers `addons/lifepunch/lptags/**`, so the bytes do ship. But there is no `@font-face` rule
anywhere in the tree (sensor: `grep -rn '@font-face' game --include=*.scss --include=*.css`
returns 0 hits, with `font-family` matches present in the same sheets as a positive control),
so no in-tree precedent exists for registering a custom font family and none was invented here.
Expect fallback to Courier New / monospace until an editor pass proves the face resolves.

### Untouched by this lane

SCHEDULER-CAPS, UI-SHELL-TOKENS and the chat-command boundary are unchanged. The five successor
integration gates in `README.md` are likewise unchanged. Gate 1 cannot be closed from the menu
side alone: the DXRP portal key/value store is host- and authorization-gated, and the house
pattern for reaching it is a host RPC service, so a client-side production profile client is
not possible without the networking that gate 1 itself names. Gates 2-4 require the vanilla
chat, scoreboard and nameplate surfaces, which this lane's ceiling forbids modifying.
