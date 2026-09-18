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

using Sandbox;
using Sandbox.UI;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>Original skull artwork, tinted by the same styles as the other command icons.</summary>
public sealed class StaffXrayIcon : Image
{
	private static readonly Texture Skull = Sandbox.Texture.CreateFromSvgSource(
		"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">" +
		"<path fill=\"white\" fill-rule=\"evenodd\" d=\"M12 2C6.5 2 3 5.8 3 10.5c0 3.2 1.6 5.5 4 6.5v4a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1v-4c2.4-1 4-3.3 4-6.5C21 5.8 17.5 2 12 2Z M6 10a2 2 0 1 0 4 0a2 2 0 1 0-4 0Z M14 10a2 2 0 1 0 4 0a2 2 0 1 0-4 0Z M12 13l-1.5 3h3Z M9 18h1v4H9Z M14 18h1v4h-1Z\"/>" +
		"</svg>", 52, 52, Color.White );

	public StaffXrayIcon()
	{
		Texture = Skull;
	}
}
