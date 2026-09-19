# Staff chat plain-text regression checks

Run `dotnet run --project tests/ulx-staff-chat/StaffChatInput.Tests.csproj` from this checkout.
This SDK-only console harness links the actual shipped `StaffChatInput.cs`; it does not start
s&box, send chat, or call the game API. It tests command/channel prefix rejection after Trim,
Unicode whitespace/control/format prefixes, embedded newlines/control characters, native
length limits, ordinary exclamation text, and benign slash/at-sign text inside a message.

It also links `StaffChatHost.cs` under `LIFEPUNCH_LOCAL` to exercise the actual pure recent-feed
selector: filter before the 40-message cap, preserve native newest-first order, count only
matching entries in the current received buffer, and discard evicted entries on the next read.
The local-build adapter returns an empty, unavailable feed rather than invented fixtures.

The supplemental route classifier documents the inspected parser boundary in
`game/Code/Chat/Chat.Handler.cs` and `Chat.Command.cs`. It does not prove host enforcement,
transport delivery, UI clipping, or runtime permission-revocation behavior. Those need live QA.

The UI holds only a current projection of native received StaffChat entries. Permission loss,
scene/host/local-connection/local-account changes clear its draft and rows. On permission
regrant, messages still in the native buffer can reappear: no separate archive is retained.
Only native Role/RoleColor fields render; absent native role labels remain absent.

Submission uses native `SubmitPlayerChat(text, MessageType.StaffChat)` after a fresh permission
and session check. The native method returns no receipt. The panel adds no optimistic message
and reports no delivery success; a row appears only when native authorized delivery adds it.
The native buffer is capped at 80 messages across all channels, so this is recent received
staff chat, not a full server or Portal history.
The displayed list uses Observe's 40-row cap. Its visible shown/available count refers only to
StaffChat messages currently in that native received buffer, never a historical server total.
