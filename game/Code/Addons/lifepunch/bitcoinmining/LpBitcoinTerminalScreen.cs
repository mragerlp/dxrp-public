// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>World LCD — minimal live status on CRT glass; detail in hashd UI on USE.</summary>
public static class LpBitcoinTerminalScreen
{
	// R1 world-display ruling (2026-07-09): chrome diet reaches the world — no brand line
	// on the prop readout; $ formatted whole-dollar. Link count reads L/3 (capacity), the
	// element the Law-13 state colors bind to when the TextScope color pass lands (3.5):
	// 0/3 red · partial gold · 3/3 green · parens white · ₿ icon orange + amount white ·
	// $ green.
	public static string Build( LpBitcoinHubEntity hub )
	{
		if ( hub is null || !hub.IsValid() )
			return "NOT LINKED";

		if ( !hub.IsPowered )
			return "POWER OFF";

		var racks = hub.GetLinkedRacks();
		if ( racks.Count == 0 )
			return "STANDBY\nrig0> link";

		var miningRacks = racks.Where( r => r.IsMining ).ToList();
		var miningCount = miningRacks.Count;
		var mix = FormatRackMix( racks );
		var totalBtc = racks.Sum( r => r.BitcoinAmount ) + hub.HubWalletBtc;
		var rate = miningRacks.Sum( r => r.MiningRatePerMinute );
		var pct = miningCount > 0
			? (int)( miningRacks.Average( r => r.MiningProgress ) * 100 )
			: 0;
		var usd = LpBitcoinEconomy.BtcToCashUsd( totalBtc );

		if ( miningCount == 0 )
			return $"IDLE · {mix}\n₿ {totalBtc:0.00000000}\n${usd:N0}";

		return $"MINING {miningCount}/{racks.Count} · {mix} · {pct}%\n₿ {totalBtc:0.00000000}\n{rate:0.00000}/m · ${usd:N0}";
	}

	/// <summary>Link count over hub capacity — e.g. <c>2/3</c> (the state-colored element).</summary>
	static string FormatRackMix( IEnumerable<LpBitcoinRackEntity> racks )
	{
		var count = racks.Count( r => r.IsValid() );
		return $"{count}/{LpBitcoinIdent.PortalMaxRacksPerHub}";
	}
}
