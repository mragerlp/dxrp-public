// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Player Hub for DXRP" (s&box ident: lifepunch.playerhub · addon ident: playerhub)
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.PlayerHub;

#if !LIFEPUNCH_LOCAL
/// <summary>
/// Registers <c>/playerhub</c> and <c>/hub</c> as in-game chat commands. Mirrors <c>StaffMenuChatCommand</c>
/// (lifepunchulx/StaffMenuHost.cs): <see cref="ExecuteLocal"/> toggles the hub client-side and consumes the
/// command, so it never round-trips to the host. Discovered via TypeLibrary.
/// </summary>
/// <remarks>
/// The chat registry is built at <c>Stage.SceneLoaded</c> (Chat.cs), so a newly added command appears on the
/// next scene load, not on hotload.
/// </remarks>
public sealed class LpPlayerHubChatCommand : ICommand
{
	/// <summary>Positive code-string ID for the chat-command slice. Exists nowhere else in the tree.</summary>
	public const string ChatMark = "LP_PLAYERHUB_CHAT_20261017";

	public string Command => "playerhub";
	public string[] Aliases => ["hub"];
	public string Help => "Open the Player Hub.";
	public bool IsUsableWhileDead => true;

	public bool ExecuteLocal( string[] args, string raw )
	{
		LpPlayerHubHost.Toggle();
		return true;
	}

	public bool ExecuteHost( Player caller, string[] args, string raw ) => true;
}
#endif
