// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Single scroll-panel factory for HASHD terminal + hub ops (last register wins if both open).</summary>
public static class LpBitcoinScrollRegionFactory
{
	public static void Register()
		=> LifePunchScrollRegionBootstrap.SetCustomFactory( Create );

	public static LifePunchScrollRegionPanel Create( string slotClass )
		=> slotClass switch
		{
			"log" => new LpBitcoinTerminalLogPanel(),
			"logs-scroll" => new LpBitcoinHubLogsScrollPanel(),
			_ => null
		};
}
