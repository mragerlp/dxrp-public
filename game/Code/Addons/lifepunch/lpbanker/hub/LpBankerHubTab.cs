// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 tabs (DRIVE-AWAY-1-D). Labels, blurbs and placeholder rows live here once (pattern: _dev/btcui2
// LpBtcHubTab.cs). Every row is a PLACEHOLDER; every action is inert. Rulings cite DIRECTION_BANKER-HUB_R1 section 8.

using System;

namespace LifePunch.DXRP.Addons.Banker;

public enum LpBankerHubTab
{
	Overview,
	Vault,
	Investments,
	Premiums,
	Upgrades,
	Security,
	Logs,
}

public static class LpBankerHubTabInfo
{
	public static string Label( LpBankerHubTab tab ) => tab.ToString();

	public static string Blurb( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => "The bank at a glance: reserve, investments, premiums and security",
		LpBankerHubTab.Vault => "The Banker's cash pile: what it holds and what a raid can take",
		LpBankerHubTab.Investments => "Player investments and their dividends",
		LpBankerHubTab.Premiums => "Insurance premiums the bank collects",
		LpBankerHubTab.Upgrades => "Vault and branch upgrades",
		LpBankerHubTab.Security => "Vault security tiers, guards and alarms",
		LpBankerHubTab.Logs => "Deposits, withdrawals, raids and payouts",
		_ => string.Empty,
	};

	public static string Ruling( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => "waits on rulings 1, 2, 3, 6",
		LpBankerHubTab.Vault => "waits on rulings 3, 6",
		LpBankerHubTab.Investments => "waits on rulings 1, 2",
		LpBankerHubTab.Premiums => "waits on ruling 1",
		LpBankerHubTab.Upgrades => "waits on ruling 5",
		LpBankerHubTab.Security => "waits on ruling 5",
		LpBankerHubTab.Logs => "waits on the rails",
		_ => string.Empty,
	};

	public static string[] Rows( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => new[] { "Vault reserve", "Open investments", "Premiums due", "Security tier" },
		LpBankerHubTab.Vault => new[] { "Cash pile", "Pile cap", "Last raid" },
		LpBankerHubTab.Investments => new[] { "Dividend rate", "Tick", "Cap" },
		LpBankerHubTab.Premiums => new[] { "Premium rate", "Insured value" },
		LpBankerHubTab.Upgrades => new[] { "Vault tier", "Next tier" },
		LpBankerHubTab.Security => new[] { "Security tier", "Guards", "Alarm" },
		_ => Array.Empty<string>(),
	};

	public static string[] Actions( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Vault => new[] { "Deposit to pile", "Withdraw" },
		LpBankerHubTab.Investments => new[] { "Invest", "Withdraw" },
		LpBankerHubTab.Premiums => new[] { "Set premium" },
		LpBankerHubTab.Upgrades => new[] { "Upgrade" },
		LpBankerHubTab.Security => new[] { "Raise tier" },
		_ => Array.Empty<string>(),
	};

	public static string Empty( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Logs => "No log rows: deposits, raids and payouts log here once their rails exist.",
		_ => "Skeleton: no balance, no rail, no money path. Each row binds after its ruling and its own GO.",
	};
}
