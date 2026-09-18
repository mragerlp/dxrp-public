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
/// Canonical LIFEPUNCH source identifier strings — use in UI footers, About tabs, and boot lines.
/// Full legal terms live in source headers + website TOS; in-game copy stays readable.
/// </summary>
public static class LifePunchSourceMark
{
	public const string Mark = "LIFEPUNCH™";
	public const string Publisher = "LIFEPUNCH";
	public const string PublisherUrl = "lifepunch.co";
	public const string CopyrightLine = "© 2026 lifepunch.co. All rights reserved.";

	public const string UiFooterMark = "LIFEPUNCH™";
	public const string UiFooterUrl = "https://lifepunch.co/";

	/// <summary>Screen anchor for LifePunch terminal menus — bottom-right, parallel to DXRP player HUD.</summary>
	public const int UiMenuScreenBottomPx = 48;

	/// <summary>Right inset for LifePunch terminal menus — stacks beside the player HUD.</summary>
	public const int UiMenuScreenRightPx = 24;

	public const string UiFooterPublisher = "Published by LIFEPUNCH — lifepunch.co";
	public const string UiFooterLegal = "Proprietary software · no copy, resale, or redistribution";

	public const string ProprietaryShort = "Proprietary software. All rights reserved.";
	public const string UseRestrictionShort = "Use on your server only. No redistribution or resale.";

	public const string ProprietaryFull =
		"NOT licensed for resale, redistribution, sublicensing, copying, or reuse by ANY person or entity — " +
		"including DXRP and LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).";

	public static string PublishedByLine => $"Published by {Publisher} — {PublisherUrl}";

	public static string[] AboutBlock( string productTitle )
	{
		return new[]
		{
			productTitle,
			PublishedByLine,
			CopyrightLine,
			ProprietaryShort,
			UseRestrictionShort,
			ProprietaryFull
		};
	}
}
