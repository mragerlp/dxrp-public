# Observe and staff-chat validation

## Behavior

- Chat is a single right-aligned entry in Observe navigation. Back to Observe restores the previous section.
- The chat view projects only received native StaffChat entries, newest first, with a 40-message display cap and the count still present in the native buffer. It is not a historical archive.
- Sending uses the native StaffChat channel. Leading slash/at-sign commands and channel shortcuts are rejected before dispatch. The host retains permission, gag, length and cooldown enforcement.
- Loss of permission or a change of local account/session clears the draft and projected messages and removes the editable input. Regrant can show messages still held by native chat.
- Staff counts and the action log use the same action taxonomy and exact numeric actor identity. Online role colors use the configured rank color; the unused offline placeholder is removed.
- The workbench rank directory includes every definition in the server-received snapshot, keyed by full GUID. Its parent accessor returns detached DTOs and lists. Rank-view permission gates the projection; a received empty directory differs from an unavailable source.

## Executable checks

From the repository root:

```powershell
dotnet run --project tests/ulx-staff-chat/StaffChatInput.Tests.csproj --configuration Release
pwsh -NoProfile -File tests/ulx-staff-activity/Run.ps1
pwsh -NoProfile -File tests/ulx-rank-snapshot/Run.ps1
```

The chat checks link the actual plain-text and bounded selection helpers. Activity and rank checks extract actual production methods into a stubbed environment. These validate algorithms, not engine transport or UI behavior.

## Results and limits

On September 19, 2026, the input helper passed 38 explicit cases and 508 Unicode prefix cases, bounded message selection checks passed, staff activity passed 8 checks, and rank snapshots passed 21 checks. Independent source review found no concrete defect in the final chat/navigation/authentication changes.

The isolated addon build against the cached DXRP parent reported no new errors from these changes, but failed because `XrayCommand.IsActive` is absent from that parent. This is a failed package build, not release certification.

The cached published parent also lacks the new rank snapshot accessor. Do not copy the parent RankSystem class into the addon. The package rank view remains explicitly unavailable until its parent integration is supplied. Existing package audit, pocket, money and website adapters have separate unavailable/fail-closed branches; this change does not certify them or nested host-RPC attribution.

## Required live checks

After installation while the editor is closed, use one clean editor restart and the existing development server authorization.

1. Confirm the mounted project, successful engine compile and absence of a new hotload error.
2. Check Online Staff top-row visibility, role colors, action-count agreement, and absence of the old offline placeholder.
3. Check one Chat entry at the navigation's far right; test opening from each Observe section and returning to that section.
4. Send ordinary staff text from each authorized client and confirm native delivery to the other. Confirm rejected prefixes create neither a command nor a message.
5. Revoke and restore chat permission while the input is focused; confirm draft/rows disappear, editing stops, and no stale submission succeeds. Check a viewer without audit access can still reach chat with its separate permission.
6. Confirm the rank directory includes ranks without online holders and remains gated by rank-view permission. Repeat against the exact eventual release parent.
7. Recheck sanctions cache invalidation and an armed command after a live permission change. Exercise remote commands only on disposable test actors within the authorized runtime scope.

Publication remains held until the exact package builds and the required remote checks pass.
