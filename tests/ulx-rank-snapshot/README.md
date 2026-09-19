# Rank snapshot regression checks

Requires PowerShell 7 and the .NET 10 SDK. From the repository root:

```powershell
pwsh -NoProfile -File ./tests/ulx-rank-snapshot/Run.ps1
```

The runner extracts the current `RankSystem.GetRanksSnapshot` method and the rank-directory projection/availability branches from `LpParityHost.cs`. It copies the current rank DTO, flags, and ULX rank model into an ignored `generated` directory. Extraction accepts LF or CRLF sources and fails if its source anchors no longer match. Generated files are replaced on each run; `generated`, `bin`, and `obj` are ignored.

The 21 checks cover:

- Detached snapshot objects and nested lists, complete DTO fields, and read-only collection behavior.
- Every rank definition, duplicate display names, zero holders, secondary assignments, and duplicate player/assignment inputs.
- Color normalization, permission-entry metadata, deterministic ordering, and updated definitions.
- Permission revocation/regrant and unavailable, pending, missing-system, and empty-snapshot states.
- Explicit unavailability when compiled against the older published-parent contract.

`Stubs.cs` supplies an in-memory rank map, assignments, player roster, permission grant, and readiness state. These tests exercise extracted production algorithms; they do not validate s&box synchronization, RPCs, editor hotload, UI rendering, the published parent assembly, or real remote clients. The parent snapshot API remains part of the separately reviewable DXRP change.