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

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// One sentinel/anticheat violation event (reserved). Shape mirrors the dormant
/// SentinelDto (game/Code/Api/Dtos/SentinelDto.cs) plus a display timestamp -- the fields
/// Sentinel.RecordViolation already has in hand when it fires.
/// </summary>
public readonly record struct ObserveSentinelEventVm(
	string When,
	long SteamId,
	string SteamName,
	string Exploit,
	string Detail );

/// <summary>One balance-delta event (reserved). All strings pre-formatted on the data side
/// so the view cannot invent a precision or a currency sign; IsCredit drives tone only.</summary>
public readonly record struct ObserveBalanceDeltaVm(
	string When,
	long SteamId,
	string Name,
	string Amount,
	bool IsCredit,
	string Source );

/// <summary>One offline staff member with portal-side activity (reserved).</summary>
public readonly record struct ObserveOfflineStaffVm(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	string LastSeen,
	string TotalPlaytime );

/// <summary>
/// Optional display-only data providers. Null means no provider is connected, not an empty feed.
/// These contracts expose no mutation, RPC or command.
/// Sentinel.RecordViolation (game/Code/Sentinel/Sentinel.cs) does not retain a local violation feed.
/// EconomyDeltas requires numeric transaction history; current balances and free-text audit rows
/// are insufficient. OfflineStaff requires offline activity data such as last-seen time,
/// which an online rank projection does not provide.
/// </summary>
internal static class StaffObserveSockets
{
	/// <summary>
	/// Sentinel violation feed. Null until a provider exposes recorded violations or portal sanctions.
	/// </summary>
	public static IReadOnlyList<ObserveSentinelEventVm>? SentinelEvents { get; set; }

	/// <summary>
	/// Balance-delta feed. Null until a provider supplies numeric transaction history.
	/// </summary>
	public static IReadOnlyList<ObserveBalanceDeltaVm>? EconomyDeltas { get; set; }

	/// <summary>
	/// Offline staff and activity. Null until a provider supplies roster and activity records.
	/// </summary>
	public static IReadOnlyList<ObserveOfflineStaffVm>? OfflineStaff { get; set; }
}
