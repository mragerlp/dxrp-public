// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// The universal purchase event (ECONOMY_DOCTRINE House Pattern) — raised by
/// <see cref="LifePunchUpgradeLedger"/> AFTER a purchase record is committed, never before
/// (commit-then-raise: the event announces a fact; UPGRADE_ARC_DESIGN decision 8).
/// Native s&box event-interface idiom per LIFEPUNCH_ADDON_ARCHITECTURE Rule 1 — the stat
/// ledger and future consumers listen here; vanilla-observed events join the same family later.
/// </summary>
public interface ILifePunchPurchaseEvent : ISceneEvent<ILifePunchPurchaseEvent>
{
#if !LIFEPUNCH_LOCAL
	/// <param name="player">Purchaser (may be invalid if disconnected between commit and raise).</param>
	void OnPurchase( Player player, string trackId, int tier, float costBtc );
#else
	/// <param name="player">Local lane has no DXRP Player — always null.</param>
	void OnPurchase( object player, string trackId, int tier, float costBtc );
#endif
}
