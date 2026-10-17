// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 tabs (DRIVE-AWAY-1-D), reshaped in BANKER-HUB-2 (DRIVE-AWAY-2-U1) to DIRECTION_BANKER-HUB_R1 section 2:
// the cash side (Investments -> Dividends; Premiums -> the Banker), hub upgrades (the pile itself) and the vault
// (crypto vault = sequence 2, security = sequence 3, both stubs). Labels, blurbs and inert actions live here once
// (pattern: _dev/btcui2 LpBtcHubTab.cs); the figures live in the view model (LpBankerHubState.cs).

using System;

namespace LifePunch.DXRP.Addons.Banker;

public enum LpBankerHubTab
{
	Overview,
	Investments,
	Dividends,
	Premiums,
	Upgrades,
	Vault,
	Logs,
}

public static class LpBankerHubTabInfo
{
	public static string Label( LpBankerHubTab tab ) => tab.ToString();

	public static string Blurb( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => "The bank at a glance: reserve, investments, dividends, premiums and security",
		LpBankerHubTab.Investments => "Players put money in; dividends pay it back over time",
		LpBankerHubTab.Dividends => "What the bank pays investors, tick by tick",
		LpBankerHubTab.Premiums => "What the Banker collects",
		LpBankerHubTab.Upgrades => "Hub upgrades: the pile itself",
		LpBankerHubTab.Vault => "The crypto vault (sequence 2) and vault security (sequence 3): stubs",
		LpBankerHubTab.Logs => "Investments, payouts, premiums and raids",
		_ => string.Empty,
	};

	public static string Ruling( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => "waits on I1, I2, I3, I5, I6",
		LpBankerHubTab.Investments => "waits on I1, I2",
		LpBankerHubTab.Dividends => "waits on I1",
		LpBankerHubTab.Premiums => "waits on I1",
		LpBankerHubTab.Upgrades => "waits on I1, I4, R-M6-1",
		LpBankerHubTab.Vault => "sequence 2 and 3; waits on I3, I5",
		LpBankerHubTab.Logs => "waits on the rails",
		_ => string.Empty,
	};

	public static string[] Actions( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Investments => new[] { "Invest", "Withdraw" },
		LpBankerHubTab.Dividends => new[] { "Claim dividends" },
		LpBankerHubTab.Premiums => new[] { "Set premium" },
		LpBankerHubTab.Upgrades => new[] { "Upgrade pile" },
		LpBankerHubTab.Vault => new[] { "Open crypto vault" },
		_ => Array.Empty<string>(),
	};

	public static string Empty( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Logs => "No log rows: investments, payouts and raids log here once their rails exist.",
		_ => "Fixture data: no balance, no rail, no money path. Each figure binds after its ruling and its own GO.",
	};
}
