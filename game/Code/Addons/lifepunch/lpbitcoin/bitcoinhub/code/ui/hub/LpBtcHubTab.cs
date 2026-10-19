// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// BTC-UI-2 pre-build (slice SHELL-1b): the seven hub UI tabs, in the order of spec section 6.1.
// Labels and blurbs live here once (pattern: playerhub LpPlayerHubTabInfo). Names "Terminal" and
// "Racks" wait on R-BTCUI-15. UNVERIFIED in the editor until the driving seat proves it.
// -----------------------------------------------------------------------------

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

public enum LpBtcHubTab
{
	Overview,
	Racks,
	Terminal,
	Wallet,
	Transfers,
	Logs,
	Settings,
}

public static class LpBtcHubTabInfo
{
	public static string Label( LpBtcHubTab tab ) => tab switch
	{
		LpBtcHubTab.Overview => "Overview",
		LpBtcHubTab.Racks => "Racks",
		LpBtcHubTab.Terminal => "Terminal",
		LpBtcHubTab.Wallet => "Wallet",
		LpBtcHubTab.Transfers => "Transfers",
		LpBtcHubTab.Logs => "Logs",
		LpBtcHubTab.Settings => "Settings",
		_ => tab.ToString(),
	};

	public static string Blurb( LpBtcHubTab tab ) => tab switch
	{
		LpBtcHubTab.Overview => "Setup checklist and a snapshot of the hub",
		LpBtcHubTab.Racks => "Three slots by token; pick one for its detail and upgrades",
		LpBtcHubTab.Terminal => "The linked HASHD terminal: link, access and power",
		LpBtcHubTab.Wallet => "Hub wallet, pending on racks, exchange rate and cash out",
		LpBtcHubTab.Transfers => "Your receive id, the send grammar and the live rate table",
		LpBtcHubTab.Logs => "Commands, capacity, intrusion and transfer alerts",
		LpBtcHubTab.Settings => "Power, PIN, terminal link and notifications",
		_ => string.Empty,
	};
}
