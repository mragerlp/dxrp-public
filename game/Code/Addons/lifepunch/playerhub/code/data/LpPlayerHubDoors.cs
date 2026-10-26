// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Player Hub for DXRP" (s&box ident: lifepunch.playerhub - addon ident: playerhub) is the
// sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity - including DXRP and LifePunch staff,
// contributors, or community - EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp - Public alias (in-game - Steam - Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Sandbox;

namespace LifePunch.DXRP.Addons.PlayerHub;

/// <summary>
/// SPINE-2: one door on the Player Hub tab strip. A door is either a tab INSIDE the hub (<see cref="Tab"/> is set,
/// the Root renders that tab's page) or a launcher that hands over to another hub's own menu
/// (<see cref="Tab"/> is null, <see cref="Open"/> is set). The five built-in tabs are in-hub doors.
/// </summary>
/// <param name="Key">Stable lower-case key. For an in-hub tab it is the <see cref="LpPlayerHubTab"/> member name lower-cased.</param>
/// <param name="Label">Text on the strip button.</param>
/// <param name="Icon">Material icon name. Read only by <see cref="LpPlayerHubTabInfo.Icon"/> today.</param>
/// <param name="Blurb">Line under the page title (in-hub tabs only; empty for a launcher).</param>
/// <param name="Accent">Reserved. No built-in tab has a per-tab accent (one shared strip style), so the Root does not read it yet.</param>
/// <param name="Tab">The enum member for an in-hub tab; null for a launcher.</param>
/// <param name="LiveHash">Change token mixed into the panel's BuildHash while this tab is the active one; null when nothing live backs the tab.</param>
/// <param name="Open">Launcher action (null for an in-hub tab).</param>
public sealed record LpPlayerHubDoor(
	string Key,
	string Label,
	string Icon,
	string Blurb,
	string? Accent,
	LpPlayerHubTab? Tab,
	Func<int>? LiveHash,
	Action? Open );

/// <summary>
/// SPINE-2: the Player Hub door registry. Static and deterministic: the five built-in doors are a fixed table in
/// <see cref="Builtin"/>, in strip order; nothing is discovered by reflection and nothing runs at startup. Another
/// hub adds a launcher door with one <see cref="RegisterLauncher"/> call.
/// </summary>
/// <remarks>
/// The enum <see cref="LpPlayerHubTab"/> stays the key type for the five built-ins (the Root keeps its selection as
/// the enum, the deep link and the BuildHash use it). Launcher doors have no enum member, so they are keyed by string
/// only and cannot be selected as a tab.
///
/// The built-ins are NOT cached in a static field. The Root's own comment records that hotload keeps a static
/// readonly field's OLD value (a stale 4-tab strip rendered on 2026-10-16), so the table is rebuilt on every read.
/// Only the launcher registrations live in a static list, because those are meant to survive.
/// </remarks>
public static class LpPlayerHubDoors
{
	private static readonly List<LpPlayerHubDoor> Launchers = new();

	/// <summary>The built-in tabs in strip order. Same order the Root's tab array held before SPINE-2.</summary>
	private static LpPlayerHubTab[] BuiltinOrder() => new[]
	{
		LpPlayerHubTab.Overview,
		LpPlayerHubTab.Btc,
		LpPlayerHubTab.Store,
		LpPlayerHubTab.Skills,
		LpPlayerHubTab.Stats,
	};

	/// <summary>The fixed definition of one built-in tab, or null for an enum value that is not a built-in.</summary>
	private static LpPlayerHubDoor? Builtin( LpPlayerHubTab tab ) => tab switch
	{
		LpPlayerHubTab.Overview => new LpPlayerHubDoor(
			"overview", "Overview", "dashboard",
			"Fixture-backed progression, currency and route-readiness placeholders.",
			null, LpPlayerHubTab.Overview, null, null ),
		LpPlayerHubTab.Btc => new LpPlayerHubDoor(
			"btc", "$BTC", "currency_bitcoin",
			"Your Bitcoin Hub and GPU racks. Power, mining, deposit and cash-out on the live rails.",
			null, LpPlayerHubTab.Btc, LpPlayerHubBtcSource.LiveHash, null ),
		LpPlayerHubTab.Store => new LpPlayerHubDoor(
			"store", "$LP Store", "storefront",
			"Browse the $LP preview catalog. Purchasing is not open.",
			null, LpPlayerHubTab.Store, null, null ),
		LpPlayerHubTab.Skills => new LpPlayerHubDoor(
			"skills", "Skills", "account_tree",
			"Browse five tracks, independent skill ranks and mastery status.",
			null, LpPlayerHubTab.Skills, null, null ),
		LpPlayerHubTab.Stats => new LpPlayerHubDoor(
			"stats", "Stats", "insights",
			"Read-only fixture activity. Unknown lifetime data stays unknown.",
			null, LpPlayerHubTab.Stats, null, null ),
		_ => null,
	};

	/// <summary>Every door in strip order: the five built-in tabs, then launchers in registration order. A fresh list each call.</summary>
	public static IReadOnlyList<LpPlayerHubDoor> Ordered()
	{
		var list = new List<LpPlayerHubDoor>();
		foreach ( var tab in BuiltinOrder() )
			list.Add( Builtin( tab ) );

		list.AddRange( Launchers );
		return list;
	}

	/// <summary>Lookup by key (case-insensitive). False when no door has that key.</summary>
	public static bool TryGet( string key, out LpPlayerHubDoor? door )
	{
		door = null;
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		foreach ( var candidate in Ordered() )
		{
			if ( string.Equals( candidate.Key, key, StringComparison.OrdinalIgnoreCase ) )
			{
				door = candidate;
				return true;
			}
		}

		return false;
	}

	/// <summary>Lookup of a built-in tab by its enum member. False for a value that is not a built-in.</summary>
	public static bool TryGetTab( LpPlayerHubTab tab, out LpPlayerHubDoor? door )
	{
		door = Builtin( tab );
		return door is not null;
	}

	/// <summary>
	/// Adds a launcher door that hands over to another hub's own menu. The Root closes the Player Hub first, then
	/// runs <paramref name="open"/>. Returns false (and warns) for an empty key, a missing action or a key that is already taken.
	/// </summary>
	public static bool RegisterLauncher( string key, string label, Action open, string? icon = null, string? accent = null )
	{
		if ( string.IsNullOrWhiteSpace( key ) || string.IsNullOrWhiteSpace( label ) || open is null )
		{
			Log.Warning( "[playerhub] RegisterLauncher: key, label and open are required." );
			return false;
		}

		if ( TryGet( key, out _ ) )
		{
			Log.Warning( $"[playerhub] RegisterLauncher: door '{key}' already exists." );
			return false;
		}

		Launchers.Add( new LpPlayerHubDoor( key.ToLowerInvariant(), label, icon ?? "open_in_new", string.Empty, accent, null, null, open ) );
		return true;
	}

	/// <summary>
	/// The live change token of the active in-hub tab, or null when that tab has no live source. The Root mixes it into
	/// its BuildHash only when it is non-null, exactly as the $BTC tab did before SPINE-2.
	/// </summary>
	public static int? LiveHashOf( IReadOnlyList<LpPlayerHubDoor> doors, LpPlayerHubTab tab )
	{
		foreach ( var door in doors )
		{
			if ( door.Tab == tab && door.LiveHash is not null )
				return door.LiveHash();
		}

		return null;
	}

	/// <summary>The built-in tab keys in strip order, comma separated (the playerhub_tab usage hint).</summary>
	public static string TabNames()
	{
		var keys = new List<string>();
		foreach ( var tab in BuiltinOrder() )
			keys.Add( Builtin( tab )!.Key );

		return string.Join( ", ", keys );
	}
}
