// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Greenfield v2 package identity — lifepunch.bitcoin.</summary>
public static class LpBitcoinIdent
{
	public const string SboxPackage = "lifepunch.bitcoin";
	public const string RepoIdent = "bitcoinmining";
	public const string ProductTitle = "LIFEPUNCH™ Bitcoin Miner for DXRP";
	public const string OpsTitle = "Bitcoin Ops";
	public const string HashdProgram = "hashd";
	public const string UiFooter = "lifepunch.bitcoin v2 — lifepunch.co";

	public const string HubEntitySlug = "bitcoinhub";
	public const string HubPrefabPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/entities/bitcoinhub.prefab";
	public const string HubModelPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/models/bitcoinhub.vmdl";
	public const string HubFanModelPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/models/bitcoinhub-fan.vmdl";
	public const string HubStartupSoundPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/sounds/bitcoinminer/hub-startup.sound";
	public const string HubFanLoopSoundPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/sounds/bitcoinminer/hub-fan-loop.sound";
	public const string HubFanDownSoundPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/sounds/bitcoinminer/hub-fan-down.sound";
	/// <summary>Mechanical click — rig0 CRT command line only (same asset as hacker terminal).</summary>
	public const string KeyboardSoundPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/sounds/bitcoinminer/keyboard.sound";
	public const string HubDisplayName = "Bitcoin Hub";

	/// <summary>Hub durability — heavier than DXRP money printer (100).</summary>
	public const float HubMaxHealth = 250f;

	/// <summary>GPU rack durability — single + stacked share baseline HP.</summary>
	public const float RackMaxHealth = 500f;

	/// <summary>Baseline rack yield multiplier — CPU/core upgrades drive effective rate (no small vs stacked tier).</summary>
	public const float BaseRackYieldMultiplier = 1f;

	/// <summary>DXRP Market caps per operator (portal content rows — enforced by gamemode at purchase/spawn).</summary>
	public const int PortalMaxHubsPerOperator = 1;
	public const int PortalMaxTerminalsPerOperator = 1;

	/// <summary>HASHD / rig0 slot prefix — GPURack-1 … GPURack-2 (standard racks only).</summary>
	public const string RackSlotPrefix = "GPURack";

	/// <summary>Standard GPU racks per hub (small farm mesh).</summary>
	public const int PortalMaxStandardRacksPerHub = 2;

	/// <summary>Advanced stacked GPU rack per hub.</summary>
	public const int PortalMaxAdvancedRacksPerHub = 1;

	/// <summary>DXRP portal cap per hub — 2× GPU Rack + 1× Advanced GPU Rack.</summary>
	public const int PortalMaxRacksPerHub = PortalMaxStandardRacksPerHub + PortalMaxAdvancedRacksPerHub;

	public const string AdvancedRackDisplayName = "Advanced GPU Rack";

	/// <summary>rig0 / CRT copy token for the advanced slot — e.g. advancedgpurack.</summary>
	public const string AdvancedRackTerminalToken = "advancedgpurack";

	/// <summary>Hub docs / slot id for the advanced rack.</summary>
	public const string AdvancedRackSlotId = "AdvancedGPURack";

	/// <summary>
	/// Owner portal inventory item — stackable $BTC token (drop/trade/redeem).
	/// Manage at dxrp.net/portal/inventory (not in-game mined float BTC).
	///
	/// Portal setup:
	/// - Type: <b>Consumable</b>, stackable, Grant Identifier = <see cref="PortalBtcRedeemGrantName"/>
	/// - Gamemode entity row on content <see cref="PortalBtcRedeemGrantName"/> → <see cref="PortalBtcRedeemPrefabPath"/>
	/// - Redeem $/stack: store <c>lifepunch:bitcoin:portal_redeem_cash_usd</c> or item description JSON
	///   <c>{"redeemCashUsd":5000}</c> (independent of hub mined BTC rate).
	/// </summary>
	public const string PortalBtcInventoryItemId = "019e4e7f-ab9c-7db8-9a3a-81ee6995bcb0";

	public const string PortalBtcInventoryUrl =
		"https://dxrp.net/portal/inventory/019e4e7f-ab9c-7db8-9a3a-81ee6995bcb0";

	/// <summary>Consumable grant + gamemode content name — must match portal rows exactly.</summary>
	public const string PortalBtcRedeemGrantName = "BTC Cash Redeem";

	/// <summary>Parked — prefab not in Rev 3 ship; rail 2 redeem deferred. <see cref="LpBitcoinCashRedeemEntity"/> kept for later.</summary>
	public const string PortalBtcRedeemPrefabPath =
		"addons/lifepunch/lpbitcoin/bitcoinhub/assets/entities/btccashredeem.prefab";

	public const string RackSlug = "gpu-rack";
	public const string RackDisplayName = "GPU Rack";
	public const string RackModelPath =
		"addons/lifepunch/lpbitcoin/gpurack/assets/models/gpurack.vmdl";

	public const string RackPrefabPath =
		"addons/lifepunch/lpbitcoin/gpurack/assets/entities/gpurack.prefab";

	/// <summary>Four-tier farm mesh — upgrade tier; prefab has AdvancedRack.</summary>
	public const string StackedRackModelPath =
		"addons/lifepunch/lpbitcoin/gpurack/assets/models/advancedgpurack.vmdl";

	public const string StackedRackPrefabPath =
		"addons/lifepunch/lpbitcoin/gpurack/assets/entities/advancedgpurack.prefab";

	/// <summary>Operator-facing rack slot (1-based). Internal APIs stay 0-based.</summary>
	public static int DisplayRackNumber( int zeroBasedIndex ) => zeroBasedIndex + 1;

	/// <summary>Operator-facing rack ID — GPURack-1 … GPURack-2 or AdvancedGPURack.</summary>
	public static string FormatRackSlotId( LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
	{
		if ( !rack.IsValid() )
			return $"{RackSlotPrefix}-?";

		if ( rack.AdvancedRack )
			return AdvancedRackSlotId;

		if ( !TryGetStandardRackSlotNumber( rack, linkedRacks, out var slot ) )
			return $"{RackSlotPrefix}-?";

		return $"{RackSlotPrefix}-{slot}";
	}

	/// <summary>Hub UI label — GPU Rack 1 … 2 or Advanced GPU Rack.</summary>
	public static string FormatRackSlotDisplayName( LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
	{
		if ( !rack.IsValid() )
			return $"{RackDisplayName} ?";

		if ( rack.AdvancedRack )
			return AdvancedRackDisplayName;

		if ( !TryGetStandardRackSlotNumber( rack, linkedRacks, out var slot ) )
			return $"{RackDisplayName} ?";

		return $"{RackDisplayName} {slot}";
	}

	/// <summary>Upgrade tier numeral for player-facing copy (0 = STOCK, 1..5 = I..V).</summary>
	public static string RomanTier( int tier )
		=> tier switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "STOCK" };

	/// <summary>rig0 / CRT copy token — gpurack-1 … gpurack-2 or advancedgpurack.</summary>
	public static string FormatRackSlotTerminalToken( LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
	{
		if ( !rack.IsValid() )
			return "gpurack-?";

		if ( rack.AdvancedRack )
			return AdvancedRackTerminalToken;

		if ( !TryGetStandardRackSlotNumber( rack, linkedRacks, out var slot ) )
			return "gpurack-?";

		return $"{RackSlotPrefix}-{slot}".ToLowerInvariant();
	}

	public static int CountLinkedStandardRacks( IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
		=> linkedRacks.Count( rack => rack.IsValid() && !rack.AdvancedRack );

	public static int CountLinkedAdvancedRacks( IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
		=> linkedRacks.Count( rack => rack.IsValid() && rack.AdvancedRack );

	public static List<LpBitcoinRackEntity> OrderLinkedRacks( IEnumerable<LpBitcoinRackEntity> racks )
		=> racks
			.Where( rack => rack.IsValid() )
			.OrderBy( rack => rack.AdvancedRack )
			.ThenBy( rack => rack.GameObject.Name, StringComparer.Ordinal )
			.ThenBy( rack => rack.GameObject.Id )
			.ToList();

	public static bool CanLinkRackToHub(
		LpBitcoinRackEntity rack,
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		out string error )
	{
		error = string.Empty;
		if ( !rack.IsValid() )
		{
			error = "Invalid rack.";
			return false;
		}

		if ( rack.AdvancedRack )
		{
			if ( CountLinkedAdvancedRacks( linkedRacks ) >= PortalMaxAdvancedRacksPerHub )
			{
				error = "Advanced GPU rack slot full — unlink the existing Advanced GPU Rack first.";
				return false;
			}

			return true;
		}

		if ( CountLinkedStandardRacks( linkedRacks ) >= PortalMaxStandardRacksPerHub )
		{
			error = "GPU rack slots full (2 max) — unlink a GPU Rack first.";
			return false;
		}

		return true;
	}

	/// <summary>rig0 <c>link</c> token before the rack is registered — gpurack-1, gpurack-2, advancedgpurack.</summary>
	public static bool TryParseLinkRackSlotToken(
		string token,
		out bool advanced,
		out int standardSlotNumber )
	{
		advanced = false;
		standardSlotNumber = 0;
		if ( string.IsNullOrWhiteSpace( token ) )
			return false;

		var normalized = token.Trim().ToLowerInvariant();
		if ( normalized == AdvancedRackTerminalToken
		     || normalized == AdvancedRackSlotId.ToLowerInvariant() )
		{
			advanced = true;
			return true;
		}

		var prefix = $"{RackSlotPrefix}-".ToLowerInvariant();
		if ( !normalized.StartsWith( prefix, StringComparison.Ordinal )
		     || !int.TryParse( normalized[prefix.Length..], out var slot )
		     || slot < 1
		     || slot > PortalMaxStandardRacksPerHub )
			return false;

		standardSlotNumber = slot;
		return true;
	}

	/// <summary>Lowest standard slot number with no linked rack BOUND to its token (leak
	/// fix — occupancy is token-based, not count-based, so a freed slot 1 refills before
	/// slot 2 even while slot 2's rack survives). 0 = no free standard slot.</summary>
	public static int LowestFreeStandardSlot( IReadOnlyList<LpBitcoinRackEntity> linkedRacks )
	{
		for ( var n = 1; n <= PortalMaxStandardRacksPerHub; n++ )
		{
			var token = FormatDeclaredLinkSlotToken( false, n );
			if ( !linkedRacks.Any( r =>
				     r.IsValid() && !r.AdvancedRack
				     && string.Equals( r.AssignedSlotToken, token, StringComparison.Ordinal ) ) )
				return n;
		}

		return 0;
	}

	/// <summary>Declared slot must be the lowest FREE slot (token-based occupancy —
	/// leak fix; formerly count-based fill-in-order).</summary>
	public static bool CanLinkToDeclaredSlot(
		bool advanced,
		int standardSlotNumber,
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		out string error )
	{
		error = string.Empty;
		if ( advanced )
		{
			if ( CountLinkedAdvancedRacks( linkedRacks ) >= PortalMaxAdvancedRacksPerHub )
			{
				error = "ERR advancedgpurack slot full — unlink the Advanced GPU Rack first";
				return false;
			}

			return true;
		}

		var nextSlot = LowestFreeStandardSlot( linkedRacks );
		if ( nextSlot == 0 )
		{
			error = "ERR GPU rack slots full (2 max) — unlink a GPU Rack first";
			return false;
		}

		if ( standardSlotNumber == nextSlot )
			return true;

		if ( standardSlotNumber < nextSlot )
		{
			error = $"ERR gpurack-{standardSlotNumber} already linked — unlink that slot first";
			return false;
		}

		error = $"ERR link gpurack-{nextSlot} next (lowest free slot first)";
		return false;
	}

	public static string FormatDeclaredLinkSlotToken( bool advanced, int standardSlotNumber )
		=> advanced ? AdvancedRackTerminalToken : $"{RackSlotPrefix}-{standardSlotNumber}".ToLowerInvariant();

	public static bool TryGetStandardRackSlotNumber(
		LpBitcoinRackEntity rack,
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		out int slot )
	{
		slot = 0;
		if ( !rack.IsValid() || rack.AdvancedRack )
			return false;

		// Bound racks answer from their OWN persisted token (leak fix) — position in the
		// linked list is display-fallback only (unlinked/pre-bind racks).
		if ( TryParseLinkRackSlotToken( rack.AssignedSlotToken, out var advanced, out var bound )
		     && !advanced )
		{
			slot = bound;
			return true;
		}

		foreach ( var candidate in OrderLinkedRacks( linkedRacks ) )
		{
			if ( candidate.AdvancedRack )
				continue;

			slot++;
			if ( candidate.GameObject.Id == rack.GameObject.Id )
				return true;
		}

		return false;
	}

	/// <summary>Legacy alias — standard slot number only (advanced racks return false).</summary>
	public static bool TryGetRackSlotNumber(
		LpBitcoinRackEntity rack,
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		out int slot )
		=> TryGetStandardRackSlotNumber( rack, linkedRacks, out slot );

	public static bool TryResolveLinkedRackIndex(
		string token,
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		out int zeroBasedIndex )
	{
		zeroBasedIndex = -1;
		if ( string.IsNullOrWhiteSpace( token ) )
			return false;

		var normalized = token.Trim().ToLowerInvariant();
		if ( normalized == AdvancedRackTerminalToken
		     || normalized == AdvancedRackSlotId.ToLowerInvariant() )
		{
			for ( var i = 0; i < linkedRacks.Count; i++ )
			{
				if ( linkedRacks[i].IsValid() && linkedRacks[i].AdvancedRack )
				{
					zeroBasedIndex = i;
					return true;
				}
			}

			return false;
		}

		var prefix = $"{RackSlotPrefix}-".ToLowerInvariant();
		if ( normalized.StartsWith( prefix, StringComparison.Ordinal )
		     && int.TryParse( normalized[prefix.Length..], out var tokenSlot )
		     && tokenSlot >= 1
		     && tokenSlot <= PortalMaxStandardRacksPerHub )
			return TryFindStandardRackBySlot( linkedRacks, tokenSlot, out zeroBasedIndex );

		if ( int.TryParse( normalized, out var numericSlot ) )
		{
			if ( numericSlot >= 1 && numericSlot <= PortalMaxStandardRacksPerHub )
				return TryFindStandardRackBySlot( linkedRacks, numericSlot, out zeroBasedIndex );

			return TryParseRackSlot( normalized, linkedRacks.Count, out zeroBasedIndex );
		}

		return false;
	}

	private static bool TryFindStandardRackBySlot(
		IReadOnlyList<LpBitcoinRackEntity> linkedRacks,
		int slot,
		out int zeroBasedIndex )
	{
		zeroBasedIndex = -1;

		// The address IS the binding (leak fix): a slot resolves to the rack BOUND to its
		// token, never to a list position — `gpurack-1` on a survivor-holds-gpurack-2 board
		// correctly resolves to nothing.
		var token = FormatDeclaredLinkSlotToken( false, slot );
		for ( var i = 0; i < linkedRacks.Count; i++ )
		{
			var rack = linkedRacks[i];
			if ( rack.IsValid() && !rack.AdvancedRack
			     && string.Equals( rack.AssignedSlotToken, token, StringComparison.Ordinal ) )
			{
				zeroBasedIndex = i;
				return true;
			}
		}

		return false;
	}

	/// <summary>Parse operator rack slot (1..linkedRackCount) to internal 0-based index.</summary>
	public static bool TryParseRackSlot( string token, int linkedRackCount, out int zeroBasedIndex )
	{
		zeroBasedIndex = -1;
		if ( !int.TryParse( token, out var slot ) )
			return false;

		if ( slot < 1 || slot > linkedRackCount )
			return false;

		zeroBasedIndex = slot - 1;
		return true;
	}

	public const string TerminalDisplayName = "HASHD Terminal";
	public const string TerminalModelPath =
		"addons/lifepunch/lpbitcoin/hashdterminal/assets/models/hashdterminal.vmdl";

	/// <summary>Terminal durability — matches DXRP money printer baseline (100).</summary>
	public const float TerminalMaxHealth = 100f;
	public const string TerminalPrefabPath = "addons/lifepunch/lpbitcoin/hashdterminal/assets/entities/hashdterminal.prefab";

	/// <summary>HASHD hub + terminal UI — transparent Bitcoin mark (sidebar / PIN gate).</summary>
	public const string BtcMarkPath = "addons/lifepunch/lpbitcoin/bitcoinhub/assets/ui/hashd/btc.png";
	public const string BtcMarkUrl = "/addons/lifepunch/lpbitcoin/bitcoinhub/assets/ui/hashd/btc.png";

	/// <summary>Canonical Bitcoin glyph for UI copy, server titles, and docs (Unicode U+20BF).</summary>
	public const string BtcEmoji = "₿";

	// Legacy aliases — remove when prefab paths and portal rows finish rename (BITCOINMINING-07).
	[Obsolete( "Use RackPrefabPath — single GPU rack farm entity." )]
	public const string AdvancedRackPrefabPath = RackPrefabPath;

	[Obsolete( "Use RackSlug." )]
	public const string AdvancedRackSlug = RackSlug;

	[Obsolete( "Use BaseRackYieldMultiplier — tier yield removed." )]
	public const float AdvancedRackYield = BaseRackYieldMultiplier;

	[Obsolete( "Use BaseRackYieldMultiplier." )]
	public const float StandardRackYield = BaseRackYieldMultiplier;

	[Obsolete( "Use RackMaxHealth." )]
	public const float AdvancedRackMaxHealth = RackMaxHealth;

	[Obsolete( "Single rack tier shipped." )]
	public const bool StandardRackShipParked = false;
}
