// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Shared action-name sets for audit filters. These are emitted audit names, not command keys:
/// movement commands emit "Teleport", while the command catalogue uses goto, bring, return and tpall.
/// Matching is exact and case-insensitive. The sets overlap intentionally because each filter
/// answers a different question; check every emitter's authorization before adding a staff-only name.
/// </summary>
public static class StaffActionTaxonomy
{
	/// <summary>
	/// Actions whose game emitters all require staff permissions or run inside staff systems.
	/// Shared civilian and government-role actions are excluded; Arrest remains in enforcement.
	/// Waypoint is excluded because its grant does not establish staff-only use. ModifyBalance
	/// is retained for portal catalogue and local fixture rows. Recheck every emitter when updating this set.
	/// </summary>
	public static readonly string[] StaffActionNames =
	{
		"Warn", "Kick", "Ban", "Jail", "Gag", "Freeze", "Unarrest", "Status",
		"Spectate", "Fake Disconnect", "Teleport", "SetHealth", "SetArmor", "ForceRpName",
		"ForceSellDoor", "ClearEntities", "ClearAllEntities", "ClearAllProps",
		"SpawnItem", "StaffSpawnEntity", "StaffSpawnMarket", "StaffAnnounce",
		"CancelDemote", "JobForce", "PocketView", "StaffTicketClaimed",
		"StaffTicketResolved", "ModifyBalance"
	};

	/// <summary>
	/// Staff or governance actions directed at a player, used by the profile sanction feed.
	/// Jail and Arrest are distinct actions. This set overlaps staff actions but also includes
	/// government-role enforcement; workflow and ordinary player actions are excluded.
	/// </summary>
	public static readonly string[] SanctionActionNames =
	{
		"Warn", "Kick", "Ban", "Jail", "Gag", "Freeze", "Arrest", "Unarrest", "Sanction",
		"Demote", "CancelDemote", "VoteDemote", "PoliticalPrisoner", "Wanted", "Warrant",
		"Fake Disconnect", "SetHealth", "SetArmor", "ForceRpName", "JobForce", "PocketView",
		"Spectate", "Teleport"
	};

	/// <summary>
	/// Movement events: Teleport is shared by goto, bring, return and tpall; Waypoint covers
	/// waypoint operations. Waypoint is not classified as staff-only because its grant may be broader.
	/// </summary>
	public static readonly string[] TeleportActionNames = { "Teleport", "Waypoint" };
}
