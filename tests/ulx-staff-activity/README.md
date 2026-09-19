# Staff activity regressions

Run from PowerShell with .NET SDK 10:

```powershell
pwsh -NoProfile -File .\tests\ulx-staff-activity\Run.ps1
```

The runner extracts `CanViewObserve`, `StaffActionLog`, `StaffActivity`, and the
`ObserveStaffRow` declaration verbatim from this checkout's production source.
It copies the actual staff-action taxonomy unchanged. The generated source
manifest records SHA-256 pins; source must remain unchanged across the test run.
No engine, game files, Portal, network connection, or persistent game data is used.

Eight cases cover ordinary Chat/salary BankDeposit exclusion, taxonomy membership,
exact numeric actor identity despite spoofed names or ID prefixes, target/actor
separation, denied access, per-staff count/log agreement, no hidden display cap,
and preservation of zero-activity staff and current flags.

## Boundary

This is an executable source-projection harness, not engine or remote-client QA.
Permission, roster, flags and audit storage are deterministic stubs. The audit
stub deliberately retains generic name/ID substring search and case-insensitive
action filtering so the production method must enforce exact actor identity.
It does not test native audit transport, live revocation timing, rendering,
package compatibility, or completeness of the bounded audit snapshot. The
taxonomy is tested as configured; this suite does not certify every audit emitter.

All generated source and build output stay under this test directory.
