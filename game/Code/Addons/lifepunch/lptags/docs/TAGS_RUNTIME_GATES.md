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
