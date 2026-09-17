// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "Lockpick" (addon ident: lplockpick) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace Dxura.RP.Game;

/// <summary>
/// P-1 OPTION A: addon-lane partial. One Lp-named host method wrapping
/// <c>BroadcastLocked</c>. Serves unlock entry and re-lock exit. Zero vanilla file edits.
/// </summary>
public partial class Door
{
	public void LpSetLockedHost( bool locked )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		BroadcastLocked( locked );
	}
}
