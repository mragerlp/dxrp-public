// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Server Hub for DXRP" (s&box ident: lifepunch.serverhub - addon ident: serverhub)
// Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave
// -----------------------------------------------------------------------------

namespace LifePunch.DXRP.Addons.ServerHub;

/// <summary>The Server Hub's tab set.</summary>
/// <remarks>
/// <para>
/// Shop was absent from v1 by scope, not by design, and has now been added. The prediction the
/// v1 note made held: adding it took an entry here and a branch in the tab host, and nothing
/// else in the shell changed.
/// </para>
/// <para>
/// Shop is APPENDED rather than inserted between Jobs and Info, so the underlying values of
/// Info and Banker do not shift. Display order is set by the array in the root component, not
/// by declaration order here, so the tab still reads second in the nav without renumbering
/// anything.
/// </para>
/// </remarks>
public enum LpServerHubTab
{
	Jobs,
	Info,
	Banker,
	Shop
}

/// <summary>
/// Presentation metadata for each tab, kept out of the markup so the nav, the page header,
/// and any future breadcrumb all read the same strings from one place.
/// </summary>
public static class LpServerHubTabInfo
{
	public static string Label( LpServerHubTab tab ) => tab switch
	{
		LpServerHubTab.Jobs => "Jobs",
		LpServerHubTab.Shop => "Shop",
		LpServerHubTab.Info => "Server info",
		LpServerHubTab.Banker => "Banker",
		_ => string.Empty
	};

	/// <summary>Material icon ligature. Glyphs sit inline and bare - never in an icon tile.</summary>
	public static string Icon( LpServerHubTab tab ) => tab switch
	{
		LpServerHubTab.Jobs => "badge",
		LpServerHubTab.Shop => "storefront",
		LpServerHubTab.Info => "info",
		LpServerHubTab.Banker => "account_balance",
		_ => "circle"
	};

	public static string Blurb( LpServerHubTab tab ) => tab switch
	{
		LpServerHubTab.Jobs =>
			"Every role this server runs, read live from its own configuration.",
		LpServerHubTab.Shop =>
			"What this server sells, priced and charged by the server itself.",
		LpServerHubTab.Info =>
			"How this server is set up, and where to go next.",
		LpServerHubTab.Banker =>
			"Accounts, transfers and interest. Reserved - not yet operating.",
		_ => string.Empty
	};

	/// <summary>
	/// True when the tab shows no live server data in v1. Drives the honest "not live yet"
	/// marker in the nav, so a placeholder can never be mistaken for a working feature.
	/// </summary>
	public static bool IsReserved( LpServerHubTab tab ) => tab == LpServerHubTab.Banker;
}
