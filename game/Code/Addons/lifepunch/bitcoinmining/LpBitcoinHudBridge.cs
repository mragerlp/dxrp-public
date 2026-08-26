// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// DXRP HUD coupling — keep <c>#if</c> out of Razor markup (renders as visible text).
/// Uses <see cref="Player.LockCamera"/> (StaffMenu pattern); bitcoin panels are <c>PanelComponent</c>, not <c>Panel</c>.
/// </summary>
internal static class LpBitcoinHudBridge
{
	public static void SetMenuCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
			Player.Local.LockCamera = menuOpen;
#endif
	}
}
