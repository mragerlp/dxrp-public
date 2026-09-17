// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Server Hub for DXRP" (s&box ident: lifepunch.serverhub - addon ident: serverhub)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity - including DXRP
// and LifePunch staff, contributors, or community - EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.ServerHub;

// --- View models -------------------------------------------------------------
//
// The UI layer never sees a Dxura type. Everything below is a plain projection, so the
// Razor components stay compilable in a standalone LIFEPUNCH build, and so the read seam
// has exactly ONE crossing point (LpServerHubData) that an auditor can check.

/// <summary>One job, already formatted for display. Every string is render-ready.</summary>
public sealed record LpServerHubJobRowVm(
	string Id,
	string Name,
	string Description,
	string SalaryAmount,
	string OccupancyText,
	string RequirementText,
	string AccentHex,
	bool Selectable,
	bool AtCapacity );

/// <summary>A job group with its jobs. Ungrouped jobs land in a synthetic trailing group.</summary>
public sealed record LpServerHubJobGroupVm(
	string Id,
	string Name,
	string AccentHex,
	IReadOnlyList<LpServerHubJobRowVm> Jobs );

/// <summary>The Jobs tab payload.</summary>
/// <param name="IsLive">
/// False means the gamemode config carried no jobs, so the tab shows an honest empty state
/// instead of inventing rows. This flag is the whole difference between "this server has no
/// jobs configured" and "these numbers are made up" - the hub must never blur the two.
/// </param>
public sealed record LpServerHubJobsVm(
	IReadOnlyList<LpServerHubJobGroupVm> Groups,
	int JobCount,
	int GroupCount,
	bool IsLive );

/// <summary>Local player money, read-only. Formatted once, here, so precision cannot drift.</summary>
public sealed record LpServerHubMoneyVm(
	string WalletAmount,
	string BankAmount,
	bool IsAvailable );

/// <summary>One market item, formatted for display.</summary>
/// <param name="ItemId">
/// The market item's id. This is the ONLY thing the hub ever sends outward when a player buys:
/// the host looks the item up itself and prices it itself. The hub never transmits a price, a
/// quantity, or an item body, so there is nothing for a client to tamper with.
/// </param>
/// <param name="PriceAmount">
/// ADVISORY display price only. It is computed with the same expression the host uses, so the
/// two agree, but the host recomputes it and is the only authority on what is charged.
/// </param>
/// <param name="Affordable">
/// A courtesy check against the local player's wallet+bank, mirroring what the native market
/// does. It is NOT a gate - the host performs the real funds check when it debits.
/// </param>
public sealed record LpServerHubShopItemVm(
	Guid ItemId,
	string Name,
	string Description,
	string PriceAmount,
	string QuantityText,
	string OwnedText,
	bool Purchasable,
	bool Affordable );

/// <summary>A market grouping with its items. Ungrouped items land in a trailing bucket.</summary>
public sealed record LpServerHubShopGroupVm(
	string Id,
	string Name,
	IReadOnlyList<LpServerHubShopItemVm> Items );

/// <summary>The Shop tab payload.</summary>
/// <param name="IsLive">
/// False means the gamemode config carried no market items. Same rule as the Jobs tab: an
/// unconfigured server gets an honest empty state, never invented rows.
/// </param>
/// <param name="MoneyEnabled">
/// False on gamemodes that run without money at all. The tab then shows the catalog without
/// prices rather than showing everything as costing nothing.
/// </param>
public sealed record LpServerHubShopVm(
	IReadOnlyList<LpServerHubShopGroupVm> Groups,
	int ItemCount,
	int GroupCount,
	bool IsLive,
	bool MoneyEnabled );

// --- The read seam -----------------------------------------------------------

/// <summary>
/// The ONLY place this addon touches DXRP data. Everything here is a read: no assignment,
/// no RPC, no command dispatch, no mutation of any kind.
/// </summary>
/// <remarks>
/// <para>
/// Job rows come from <c>GameModeJobs.All</c>, which resolves to the ACTIVE GAMEMODE CONFIG -
/// each server's own portal-authored job list. It is not a fixture and not a hardcoded table.
/// Grouping mirrors the native tab menu (group by <c>GameModeJobGroupId</c>, resolve through
/// <c>GameModeJobs.FindGroupById</c>, trail the ungrouped bucket) so the hub and the native
/// menu cannot disagree about the same underlying data.
/// </para>
/// <para>
/// Display text routes through <c>DisplayName()</c> / <c>DisplayDescription()</c> rather than
/// raw <c>.Name</c>, because those extensions run the label through <c>LabelResolver</c> and
/// therefore honour localization. Reading <c>.Name</c> directly compiles fine and silently
/// breaks every non-English locale, which is exactly the kind of bug that ships.
/// </para>
/// </remarks>
public static class LpServerHubData
{
	/// <summary>Positive code-string ID for the read seam. Exists nowhere else in the tree.</summary>
	public const string ReadMark = "LP_SERVERHUB_READ_SEAM_20260827";

	/// <summary>Shown when a job carries no group.</summary>
	private const string UngroupedName = "General";

	/// <summary>Sorts after any real group name - the native menu uses this same trailing key.</summary>
	private const string UngroupedSortKey = "zzzz";

	/// <summary>
	/// House separator (U+00B7), a literal in this .cs file. That is safe here: the ASCII-only
	/// constraint belongs to Razor <c>@code</c> blocks, which the s&amp;box transpiler scans, and
	/// plain C# files in this tree already carry non-ASCII glyphs. Every .razor file in this
	/// addon is pure ASCII and escapes its display glyphs as HTML entities instead.
	/// </summary>
	private const string Separator = " · ";

	private static readonly LpServerHubJobsVm EmptyJobs =
		new( Array.Empty<LpServerHubJobGroupVm>(), 0, 0, false );

	private static readonly LpServerHubMoneyVm NoMoney =
		new( "0", "0", false );

	/// <summary>Display bucket for market items that carry no grouping.</summary>
	/// <remarks>
	/// The native market groups on the same value and falls back to a literal "other" key. We
	/// group on the same field so the hub and the native market cannot disagree about which
	/// items belong together; only the label shown to the player differs.
	/// </remarks>
	private const string UngroupedShopName = "Other";

	private static readonly LpServerHubShopVm EmptyShop =
		new( Array.Empty<LpServerHubShopGroupVm>(), 0, 0, false, false );

	/// <summary>Read the live job roster. Never throws; an unavailable seam reads as empty.</summary>
	public static LpServerHubJobsVm ReadJobs()
	{
#if LIFEPUNCH_LOCAL
		return EmptyJobs;
#else
		var all = GameModeJobs.All;
		if ( all is null || all.Count == 0 )
		{
			return EmptyJobs;
		}

		var groups = all
			.GroupBy( job => job.GameModeJobGroupId )
			.Select( bucket =>
			{
				var group = GameModeJobs.FindGroupById( bucket.Key );
				var groupName = group is null ? UngroupedName : group.DisplayName();

				return new
				{
					SortKey = group is null ? UngroupedSortKey : groupName,
					Vm = new LpServerHubJobGroupVm(
						Id: bucket.Key?.ToString() ?? UngroupedSortKey,
						Name: groupName,
						AccentHex: AccentOf( group?.Color ?? 0u ),
						Jobs: bucket
							.OrderBy( job => job.DisplayName(), StringComparer.OrdinalIgnoreCase )
							.Select( ToRow )
							.ToList() )
				};
			} )
			.OrderBy( entry => entry.SortKey, StringComparer.OrdinalIgnoreCase )
			.Select( entry => entry.Vm )
			.ToList();

		return new LpServerHubJobsVm(
			Groups: groups,
			JobCount: all.Count,
			GroupCount: groups.Count,
			IsLive: true );
#endif
	}

	/// <summary>Read the local player's wallet and bank. Read-only by construction.</summary>
	/// <remarks>
	/// Both balances are <c>private set</c> on <c>Player</c>, so there is no write path to reach
	/// from here even by accident. Formatting is "N0" to match the native HUD balance readout
	/// exactly - the same number must never render at two precisions in one client.
	/// </remarks>
	public static LpServerHubMoneyVm ReadMoney()
	{
#if LIFEPUNCH_LOCAL
		return NoMoney;
#else
		var player = Player.Local;
		if ( !player.IsValid() )
		{
			return NoMoney;
		}

		return new LpServerHubMoneyVm(
			WalletAmount: player.WalletBalance.ToString( "N0" ),
			BankAmount: player.BankBalance.ToString( "N0" ),
			IsAvailable: true );
#endif
	}

	/// <summary>Read the live market catalog. Never throws; an unavailable seam reads as empty.</summary>
	/// <remarks>
	/// <para>
	/// Catalog and every permission decision come from <c>GameModeMarketItems</c> - the same
	/// helpers the native market screen uses. Grouping, ordering, display names, owned counts and
	/// purchasability are all resolved by that class rather than re-implemented here, so the two
	/// surfaces cannot drift apart in what they show or in who is allowed to buy what.
	/// </para>
	/// <para>
	/// THE PRICE HERE IS FOR DISPLAY ONLY. It is deliberately the same expression the host uses
	/// when it charges, so the number a player reads is the number they pay - but this method
	/// computes nothing that is ever spent. The host recomputes the price and debits it in the
	/// same call. Nothing in this addon prices an item and then grants it.
	/// </para>
	/// </remarks>
	public static LpServerHubShopVm ReadShop()
	{
#if LIFEPUNCH_LOCAL
		return EmptyShop;
#else
		var all = GameModeMarketItems.All;
		if ( all is null || all.Count == 0 )
		{
			return EmptyShop;
		}

		var player = Player.Local;
		var moneyEnabled = Config.Current.Game.MoneyEnabled;
		var spendable = player.IsValid() ? (ulong)player.WalletBalance + player.BankBalance : 0UL;

		var groups = GameModeMarketItems.OrderForDisplay( all )
			.GroupBy( item =>
			{
				var grouping = GameModeMarketItems.Grouping( item );
				return string.IsNullOrWhiteSpace( grouping ) ? string.Empty : grouping;
			} )
			.Select( bucket => new
			{
				// Ungrouped items trail, matching the native market's "other" bucket. Ordering on
				// a flag rather than a sentinel string keeps this readable and avoids depending on
				// where an exotic character happens to sort.
				IsUngrouped = bucket.Key.Length == 0,
				SortKey = bucket.Key,
				Vm = new LpServerHubShopGroupVm(
					Id: bucket.Key.Length == 0 ? UngroupedShopName : bucket.Key,
					Name: bucket.Key.Length == 0 ? UngroupedShopName : bucket.Key,
					Items: bucket.Select( item => ToShopItem( item, player, moneyEnabled, spendable ) ).ToList() )
			} )
			.OrderBy( entry => entry.IsUngrouped )
			.ThenBy( entry => entry.SortKey, StringComparer.OrdinalIgnoreCase )
			.Select( entry => entry.Vm )
			.ToList();

		return new LpServerHubShopVm(
			Groups: groups,
			ItemCount: all.Count,
			GroupCount: groups.Count,
			IsLive: true,
			MoneyEnabled: moneyEnabled );
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static LpServerHubJobRowVm ToRow( GameModeJobDto job )
	{
		var taken = GameUtils.GetPlayersByJob( job ).Count();
		var capped = job.MaxCount > 0;

		return new LpServerHubJobRowVm(
			Id: job.Id.ToString(),
			Name: job.DisplayName(),
			Description: job.DisplayDescription(),
			SalaryAmount: job.Salary.ToString( "N0" ),
			OccupancyText: capped ? $"{taken}/{job.MaxCount}" : taken.ToString(),
			RequirementText: BuildRequirementText( job ),
			AccentHex: AccentOf( job.Color ),
			Selectable: job.Selectable,
			AtCapacity: capped && taken >= job.MaxCount );
	}

	/// <summary>Packed colour to a CSS hex, or empty when the entity has no colour set.</summary>
	/// <remarks>
	/// An unset colour packs to 0, which is opaque black. Rendering that literally would put a
	/// black dot on a near-black shell - an invisible marker that reads as a rendering fault
	/// rather than as "no colour assigned". Returning empty lets the view fall back to a token
	/// colour instead, so the absence of a colour looks deliberate.
	/// </remarks>
	private static string AccentOf( uint packed )
	{
		return packed == 0u ? string.Empty : packed.ToColor().Hex;
	}

	/// <summary>Projects one market item for display. Reads only; grants nothing, charges nothing.</summary>
	/// <remarks>
	/// The price expression is deliberately character-identical to the one the host uses when it
	/// charges. That is the point: a hub that rounded differently would quote a price the player
	/// does not actually pay. It is still only a label - the host recomputes and debits.
	/// </remarks>
	private static LpServerHubShopItemVm ToShopItem( GameModeMarketItemDto item, Player player, bool moneyEnabled, ulong spendable )
	{
		// Falls back to the field's own default when the manager is not up yet, so an early paint
		// shows the unmultiplied cost rather than throwing inside a render pass.
		var multiplier = GameManager.Instance.IsValid() ? GameManager.Instance.EntityPriceMultiplier : 1f;
		var price = (uint)Math.Max( 0, (int)MathF.Ceiling( item.Cost * multiplier ) );
		var owned = player.IsValid() ? GameModeMarketItems.GetOwnedCount( player, item ) : 0;

		return new LpServerHubShopItemVm(
			ItemId: item.Id,
			Name: GameModeMarketItems.DisplayName( item ),
			Description: GameModeMarketItems.DisplayDescription( item ),
			PriceAmount: price.ToString( "N0" ),
			QuantityText: item.Quantity > 1 ? $"x{item.Quantity}" : string.Empty,
			OwnedText: owned > 0 ? $"{owned} owned" : string.Empty,
			Purchasable: player.IsValid() && GameModeMarketItems.CanPurchase( player, item ),
			Affordable: !moneyEnabled || spendable >= price );
	}

	/// <summary>Composes the requirement line C#-side and returns it as ONE string.</summary>
	/// <remarks>
	/// This is deliberate, not stylistic. A separator written as a bare literal between two
	/// Razor expressions becomes its own text node and the renderer collapses the surrounding
	/// whitespace, producing run-on text and stray marks. Building the whole line here and
	/// interpolating it as a single node is the only shape that renders predictably.
	/// </remarks>
	private static string BuildRequirementText( GameModeJobDto job )
	{
		var parts = new List<string>();

		if ( job.PlayTime.HasValue && job.PlayTime.Value > 0 )
		{
			parts.Add( $"{job.PlayTime.Value:N0} min playtime" );
		}

		var prerequisites = GameModeJobs.GetPrerequisiteJobs( job );
		if ( prerequisites.Length > 0 )
		{
			parts.Add( "After " + string.Join( ", ", prerequisites.Select( p => p.DisplayName() ) ) );
		}

		// Election supersedes vote: a seat that is elected is never merely voted for, and
		// showing both would read as two separate gates on one job.
		if ( job.ElectionRequired )
		{
			parts.Add( "Election" );
		}
		else if ( job.VoteRequired )
		{
			parts.Add( "Vote" );
		}

		if ( !job.Selectable )
		{
			parts.Add( "Assigned only" );
		}

		return parts.Count == 0 ? "Open to all" : string.Join( Separator, parts );
	}
#endif
}
