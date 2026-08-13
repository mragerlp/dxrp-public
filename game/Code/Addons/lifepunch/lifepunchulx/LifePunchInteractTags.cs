// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Shared interaction tags for LifePunch menus layered on DXRP <c>HandsEquipment</c> / <c>PocketSystem</c>.
/// </summary>
public static class LifePunchInteractTags
{
	/// <summary>Raid deny — omit <c>pocket_item</c>; tag on miners/terminals so Hands never pockets them.</summary>
	public const string NoPocket = "lifepunch_nopocket";
}
