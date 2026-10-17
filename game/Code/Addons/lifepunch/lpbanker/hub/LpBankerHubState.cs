// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-2 (DRIVE-AWAY-2-U1): the read-only view model of the Banker hub (pattern: _dev/btcui2
// ILpBitcoinHubState + LpBitcoinHubStateFixture). Every figure names the ruling it waits on (I1-I6 =
// DIRECTION_BANKER-HUB_R1 section 8 items 1-6) and the stub a later slice binds. No live source exists yet, so the
// only state is the fixture: no balance, no rail, no persistence.

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.Banker;

/// <summary>The rulings DIRECTION_BANKER-HUB_R1 section 8 says the principal still owes, plus the two non-I ids the hub cites.</summary>
public static class LpBankerRulings
{
	public const string I1 = "I1";
	public const string I2 = "I2";
	public const string I3 = "I3";
	public const string I4 = "I4";
	public const string I5 = "I5";
	public const string I6 = "I6";
	public const string Seq2 = "SEQ2";
	public const string M6Scale = "R-M6-1";

	public static string Describe( string id ) => id switch
	{
		I1 => "dividend and premium rates, tick and caps",
		I2 => "whether investments use lifepunch.wallet or a bank balance of their own",
		I3 => "persistence of the pile and what a raid takes",
		I4 => "the main hub model and its map placement",
		I5 => "vault security tiers",
		I6 => "what happens to the pile when no Banker is in play",
		Seq2 => "DIRECTION section 5 sequence 2: the crypto vault slice",
		M6Scale => "the money_stacked import scale (M6 recommendation, PROVISIONAL)",
		_ => "unnamed ruling",
	};
}

/// <summary>One figure on a Banker tab: what it shows, the ruling it waits on and the stub a later slice binds.</summary>
public readonly record struct LpBankerFigure( string Label, string Value, string Ruling, string Stub )
{
	public string Tooltip => $"{Stub}: waits on {Ruling} - {LpBankerRulings.Describe( Ruling )}";
}

/// <summary>Read-only state behind the Banker hub tabs. Nothing here moves money or writes state.</summary>
public interface ILpBankerHubState
{
	bool IsFixture { get; }

	IReadOnlyList<LpBankerFigure> Figures( LpBankerHubTab tab );
}

/// <summary>FIXTURE: invented figures that show the layout. Every value is a stub until its ruling and its own GO.</summary>
public sealed class LpBankerHubFixture : ILpBankerHubState
{
	public bool IsFixture => true;

	public IReadOnlyList<LpBankerFigure> Figures( LpBankerHubTab tab ) => tab switch
	{
		LpBankerHubTab.Overview => new LpBankerFigure[]
		{
			new( "Bank reserve (the pile)", "$25,000", LpBankerRulings.I3, "Pile.Reserve" ),
			new( "Open investments", "3", LpBankerRulings.I2, "Investments.OpenCount" ),
			new( "Dividends paid today", "$412", LpBankerRulings.I1, "Dividends.PaidToday" ),
			new( "Premiums collected today", "$180", LpBankerRulings.I1, "Premiums.CollectedToday" ),
			new( "Security tier", "Tier 1", LpBankerRulings.I5, "Vault.SecurityTier" ),
			new( "With no Banker in play", "pile idles", LpBankerRulings.I6, "Pile.NoBankerMode" ),
		},
		LpBankerHubTab.Investments => new LpBankerFigure[]
		{
			new( "Principal invested", "$12,500", LpBankerRulings.I2, "Investments.Principal" ),
			new( "Open positions", "3", LpBankerRulings.I2, "Investments.OpenCount" ),
			new( "Paid from", "wallet or bank balance", LpBankerRulings.I2, "Investments.Source" ),
			new( "Minimum stake", "$100", LpBankerRulings.I1, "Investments.MinStake" ),
			new( "Cap per player", "$10,000", LpBankerRulings.I1, "Investments.CapPerPlayer" ),
		},
		LpBankerHubTab.Dividends => new LpBankerFigure[]
		{
			new( "Dividend rate", "1.5% per tick", LpBankerRulings.I1, "Dividends.Rate" ),
			new( "Tick", "10 min", LpBankerRulings.I1, "Dividends.Tick" ),
			new( "Next payout in", "07:42", LpBankerRulings.I1, "Dividends.NextPayout" ),
			new( "Paid to date", "$412", LpBankerRulings.I1, "Dividends.PaidToDate" ),
			new( "Daily cap", "$2,000", LpBankerRulings.I1, "Dividends.DailyCap" ),
		},
		LpBankerHubTab.Premiums => new LpBankerFigure[]
		{
			new( "Premium rate", "2.0%", LpBankerRulings.I1, "Premiums.Rate" ),
			new( "Insured value", "$9,000", LpBankerRulings.I1, "Premiums.InsuredValue" ),
			new( "Collected to date", "$180", LpBankerRulings.I1, "Premiums.CollectedToDate" ),
			new( "Banker share", "100%", LpBankerRulings.I1, "Premiums.BankerShare" ),
		},
		LpBankerHubTab.Upgrades => new LpBankerFigure[]
		{
			new( "Pile tier", "1 of 3", LpBankerRulings.I1, "Upgrades.PileTier" ),
			new( "Pile cap", "$50,000", LpBankerRulings.I1, "Upgrades.PileCap" ),
			new( "Next tier cost", "$5,000", LpBankerRulings.I1, "Upgrades.NextTierCost" ),
			new( "Hub model", "money_stacked_01", LpBankerRulings.I4, "Pile.Model" ),
			new( "Import scale", "0.4082", LpBankerRulings.M6Scale, "Pile.ImportScale" ),
		},
		LpBankerHubTab.Vault => new LpBankerFigure[]
		{
			new( "Crypto vault", "rack model (stub)", LpBankerRulings.Seq2, "Vault.Crypto" ),
			new( "Security tier", "Tier 1", LpBankerRulings.I5, "Vault.SecurityTier" ),
			new( "What a raid takes", "stub", LpBankerRulings.I3, "Vault.RaidTake" ),
			new( "Pile persistence", "stub", LpBankerRulings.I3, "Pile.Persistence" ),
		},
		_ => System.Array.Empty<LpBankerFigure>(),
	};
}
