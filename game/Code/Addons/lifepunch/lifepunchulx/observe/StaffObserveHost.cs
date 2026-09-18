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

using System;
using System.Collections.Generic;
using System.Linq;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// An online player with active state flags from <see cref="StaffMenuHost.GetStateFlags"/>.
/// This is a current snapshot, not a history.
/// </summary>
public readonly record struct ObserveFlaggedRow(
	StaffMenuPlayer Player,
	IReadOnlyList<StaffStateFlag> Flags,
	bool AnyIllegitimate );

/// <summary>
/// An online player's current host-synced wallet and bank balances, not transaction history.
/// </summary>
public readonly record struct ObserveHoldingRow(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	uint Wallet,
	uint Bank,
	ulong Total );

/// <summary>
/// One online staff member with an activity summary (Observe "Online staff" row).
/// RingEventCount counts this actor's rows in the host audit ring -- newest 500, current
/// boot, host process only -- so the view labels it as ring events, never as history.
/// </summary>
public readonly record struct ObserveStaffRow(
	StaffMenuPlayer Player,
	int RingEventCount,
	IReadOnlyList<StaffStateFlag> Flags );

/// <summary>
/// Read-only Observe projections through StaffMenuHost, Sandbox and the BCL.
/// The same code supports the local fixture and live builds without direct Dxura dependencies.
/// Views without a data provider use <see cref="StaffObserveSockets"/>.
/// </summary>
internal static class StaffObserveHost
{
	// ── Superadmin+ gate ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Minimum rank order for Observe, matching the default unlimited-ban threshold.
	/// DXRP ranks use configured numeric Order values, not a fixed tier enum.
	/// </summary>
	public const int SuperadminPlusMinRankOrder = 10;

	/// <summary>
	/// Observe visibility requires portal.audit.view and the rank-order floor.
	/// This is a UI gate for read-only views, not authority to execute an action.
	/// The local fixture build supplies permission and rank order 10 for preview.
	/// </summary>
	public static bool CanViewObserve() =>
		StaffMenuHost.CanView( StaffMenuHost.AuditPermissionId )
		&& StaffMenuHost.LocalRankOrder >= SuperadminPlusMinRankOrder;

	// ── Audit-ring action sets (existing names only) ─────────────────────────────────

	/// <summary>
	/// Enforcement action names emitted to the host audit ring.
	/// GetAuditEntries matches these exact names case-insensitively.
	/// </summary>
	private static readonly string[] ModerationActionNames =
	{
		"Warn", "Kick", "Ban", "Jail", "Gag", "Freeze", "Arrest", "Unarrest", "Status",
		"Wanted", "Warrant", "Spectate", "Fake Disconnect", "Demote",
		"PoliticalPrisoner", "StaffTicketClaimed", "StaffTicketResolved"
	};

	/// <summary>
	/// Money-movement action names emitted by the wallet, bank-deposit and gameplay rails.
	/// BankDeposit covers deposits audited by those payment paths; this filtered event feed
	/// is not a complete transaction ledger or the numeric history used by EconomyDeltas.
	/// "ModifyBalance" exists only in the portal catalog and the LIFEPUNCH_LOCAL stub rows,
	/// and is included so those rows filter correctly where they exist.
	/// </summary>
	private static readonly string[] EconomyActionNames =
	{
		"WalletCharge", "WalletDeposit", "BankDeposit", "ATM", "MoneySpawn", "Recycler", "CoinFlip",
		"SlotMachineCashOut", "VoteBet", "Hit", "MayorTown", "LifePunchBtcPayout",
		"MysteryBoxWin", "ModifyBalance"
	};

	/// <summary>
	/// Audit rows remain in the host process; remote clients cannot read the ring.
	/// An unavailable feed must not be presented as an empty history.
	/// </summary>
	public static bool RingLivesHere => StaffMenuHost.AuditFeedIsReadableHere;

	/// <summary>
	/// Reason ring-backed Observe views are unavailable to this viewer.
	/// </summary>
	public static string RingUnavailableText => StaffMenuHost.AuditFeedUnavailableText;

	// ── View reads (existing sources only) ───────────────────────────────────────────

	/// <summary>Newest-first enforcement events from the host audit ring.</summary>
	public static IReadOnlyList<StaffAuditEntry> ModerationEvents() =>
		StaffMenuHost.GetAuditEntries( "", "", false, ModerationActionNames );

	/// <summary>Newest-first money events from the host audit ring. Amounts live inside the
	/// free-text descriptions (the ring has no numeric field); callers must render them
	/// verbatim and compute nothing from them.</summary>
	public static IReadOnlyList<StaffAuditEntry> EconomyEvents() =>
		StaffMenuHost.GetAuditEntries( "", "", false, EconomyActionNames );

	/// <summary>
	/// Newest-first staff actions, optionally restricted to an exact actor SteamID.
	/// Targets are embedded in Description rather than stored in a separate ring field.
	/// </summary>
	public static IReadOnlyList<StaffAuditEntry> StaffActionLog( long actorSteamId = 0 ) =>
		StaffMenuHost.GetAuditEntries(
			actorSteamId == 0 ? "" : actorSteamId.ToString(), "", false, StaffActionTaxonomy.StaffActionNames );

	/// <summary>
	/// Online players with active flags, with flags lacking their required permission first.
	/// This scan reads current state and records no history.
	/// </summary>
	public static IReadOnlyList<ObserveFlaggedRow> FlaggedOnlinePlayers()
	{
		var rows = new List<ObserveFlaggedRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			var flags = StaffMenuHost.GetStateFlags( player.SteamId );
			if ( flags.Count == 0 )
			{
				continue;
			}

			rows.Add( new ObserveFlaggedRow( player, flags, flags.Any( f => f.Illegitimate ) ) );
		}

		return rows
			.OrderByDescending( r => r.AnyIllegitimate )
			.ThenBy( r => r.Player.Name )
			.ToList();
	}

	/// <summary>
	/// Online players ordered by current wallet plus bank balance, capped at <paramref name="max"/>.
	/// Spike detection requires the separate history source represented by EconomyDeltas.
	/// </summary>
	public static IReadOnlyList<ObserveHoldingRow> TopHoldings( int max )
	{
		var rows = new List<ObserveHoldingRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			if ( StaffMenuHost.IsSyntheticPlayer( player.SteamId ) )
			{
				continue;
			}

			var detail = StaffMenuHost.GetPlayerDetail( player.SteamId );
			if ( !detail.Found )
			{
				continue;
			}

			rows.Add( new ObserveHoldingRow(
				player.SteamId, player.Name, player.Role, player.RankColorHex,
				detail.Wallet, detail.Bank, (ulong)detail.Wallet + detail.Bank ) );
		}

		return rows
			.OrderByDescending( r => r.Total )
			.ThenBy( r => r.Name )
			.Take( max )
			.ToList();
	}

	/// <summary>
	/// Online staff grouped by rank with playtime, current-boot ring counts and current flags.
	/// Offline activity requires a separate source; see <see cref="StaffObserveSockets.OfflineStaff"/>.
	/// </summary>
	public static IReadOnlyList<ObserveStaffRow> StaffActivity()
	{
		var counts = new Dictionary<long, int>();
		foreach ( var entry in StaffMenuHost.GetAuditEntries( "", "" ) )
		{
			if ( entry.PlayerSteamId == 0 )
			{
				continue;
			}

			counts[entry.PlayerSteamId] = counts.TryGetValue( entry.PlayerSteamId, out var n ) ? n + 1 : 1;
		}

		var rows = new List<ObserveStaffRow>();
		foreach ( var player in StaffMenuHost.OnlinePlayers() )
		{
			if ( player.GroupName == StaffMenuHost.NonStaffGroup )
			{
				continue;
			}

			rows.Add( new ObserveStaffRow(
				player,
				counts.TryGetValue( player.SteamId, out var n ) ? n : 0,
				StaffMenuHost.GetStateFlags( player.SteamId ) ) );
		}

		return rows
			.OrderByDescending( r => r.Player.GroupOrder )
			.ThenBy( r => r.Player.Name )
			.ToList();
	}
}
