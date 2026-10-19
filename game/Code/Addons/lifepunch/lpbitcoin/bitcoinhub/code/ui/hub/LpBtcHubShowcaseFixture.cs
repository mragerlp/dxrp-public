// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// BH-SHOWCASE-1: a deterministic showcase of the Bitcoin Hub UI. The fixture is a view-model with
// two stages (EMPTY, SEEDED) whose every figure is a literal picked to show one screen state; none
// is computed by the economy and none is a balance default. The driver attaches it to ONE open
// panel instance (the editor bridge calls it; no ConCmd), so a hub a player presses never reads it.
// Walkthrough: Attach -> Report -> Seed -> Tab / Rack / Back -> Press -> close -> reopen -> Attach.
// UNVERIFIED in the editor until the driving seat proves it.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

public enum LpBtcHubShowcaseStage
{
	Empty,
	Seeded,
}

/// <summary>
/// Showcase hub. EMPTY: powered, PIN set, terminal linked, no racks, no alerts, empty wallet.
/// SEEDED: three racks - gpurack-1 can afford its next Compute tier from the hub wallet, gpurack-2 is
/// short of it, advancedgpurack is at the top tier - three alerts (one unread), and a wallet that
/// differs from what is still pending on the racks. Illustrative only, labelled PREVIEW on screen.
/// </summary>
public sealed class LpBtcHubShowcaseFixture : ILpBitcoinHubState
{
	private const float UsdRate = 5000f;

	public LpBtcHubShowcaseStage Stage { get; set; } = LpBtcHubShowcaseStage.Empty;

	private bool Seeded => Stage == LpBtcHubShowcaseStage.Seeded;

	public bool HasHub => true;
	// Fixed: the same hub reads the same after a close and reopen.
	public Guid HubId { get; } = new( "5c0a5e00-0000-4000-8000-0000000b5c01" );
	public int OwnedHubCount => 1;
	public bool IsPowered => true;
	public bool PinSet => true;
	public bool PinNeedsReEnroll => false;
	public long OwnerSteamId => 76561190000000002L;
	public float WalletBtc => Seeded ? 0.0125f : 0f;
	public float WalletUsd => WalletBtc * UsdRate;
	public float PendingBtc => Racks.Sum( r => r.Btc );
	public float PendingUsd => PendingBtc * UsdRate;
	public float UsdPerBtc => UsdRate;
	public bool RateEventActive => false;
	public float RateEventMultiplier => 1f;
	public bool TerminalLinked => true;
	public bool TerminalInRange => true;
	public int RackCount => Racks.Count;
	public int RackMax => 3;
	public int LocalCashUsd => 1840;
	public int UnreadAlerts => Seeded ? 1 : 0;
	public LpBitcoinHubLastResult LastResult => default;

	public IReadOnlyList<ILpBitcoinRackState> Racks => Seeded ? SeededRacks : Array.Empty<ILpBitcoinRackState>();

	public IReadOnlyList<LpBitcoinHubAlertRow> Alerts => Seeded ? SeededAlerts : Array.Empty<LpBitcoinHubAlertRow>();

	public int StateHash => HashCode.Combine( HubId, (int)Stage );

	// Wallet 0.0125 BTC = 1,250,000 sats: 1,000,000 is affordable, 5,000,000 is short by 3,750,000.
	private readonly ILpBitcoinRackState[] SeededRacks =
	{
		new ShowcaseRack
		{
			Id = new Guid( "5c0a5e00-0000-4000-8000-0000000005a1" ),
			Token = "gpurack-1",
			Name = "GPU Rack 1",
			IsMining = true,
			Btc = 0.1802f,
			CapBtc = 0.5f,
			InvestedBtc = 0.005f,
			Tier = 1,
			ClockGhz = 4.88f,
			RateBtcPerMin = 0.016267f,
			Yield = 1f,
			NextQuoteSats = 1_000_000L,
			NextClockGhz = 9.76f,
			PaybackMinutes = 12.3f,
		},
		new ShowcaseRack
		{
			Id = new Guid( "5c0a5e00-0000-4000-8000-0000000005a2" ),
			Token = "gpurack-2",
			Name = "GPU Rack 2",
			IsMining = false,
			Btc = 0.16f,
			CapBtc = 0.5f,
			InvestedBtc = 0.015f,
			Tier = 2,
			ClockGhz = 9.76f,
			RateBtcPerMin = 0.032533f,
			Yield = 1f,
			NextQuoteSats = 5_000_000L,
			NextClockGhz = 19.52f,
			PaybackMinutes = 30.7f,
		},
		new ShowcaseRack
		{
			Id = new Guid( "5c0a5e00-0000-4000-8000-0000000005a3" ),
			Token = "advancedgpurack",
			Name = "Advanced GPU Rack",
			Advanced = true,
			IsMining = true,
			Btc = 1f,
			CapBtc = 1f,
			InvestedBtc = 0.31f,
			Tier = 5,
			ClockGhz = 78.08f,
			RateBtcPerMin = 0.39f,
			Yield = 1.5f,
			NextQuoteSats = 0L,
			NextClockGhz = 78.08f,
			PaybackMinutes = 0f,
		},
	};

	private readonly LpBitcoinHubAlertRow[] SeededAlerts =
	{
		new( LpBitcoinHubAlertKind.RackCapacity, "Advanced GPU Rack buffer is full", 40f ),
		new( LpBitcoinHubAlertKind.TerminalCommand, "Terminal linked", 620f ),
		new( LpBitcoinHubAlertKind.HubTransfer, "Received 0.01 BTC from another hub", 4000f ),
	};

	private sealed class ShowcaseRack : ILpBitcoinRackState
	{
		public Guid Id { get; init; }
		public string Token { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public bool Advanced { get; init; }
		public bool IsMining { get; init; }
		public float Btc { get; init; }
		public float CapBtc { get; init; }
		public int FillPct => CapBtc > 0f ? (int)MathF.Round( 100f * Math.Clamp( Btc / CapBtc, 0f, 1f ) ) : 0;
		public bool Full => CapBtc > 0f && Btc >= CapBtc;
		public float InvestedBtc { get; init; }
		public int Tier { get; init; }
		public int MaxTier => 5;
		public float ClockGhz { get; init; }
		public float RateBtcPerMin { get; init; }
		public float Yield { get; init; }
		public long NextQuoteSats { get; init; }
		public float NextQuoteUsd => NextQuoteSats / 100_000_000f * UsdRate;
		public float NextClockGhz { get; init; }
		public float PaybackMinutes { get; init; }
	}
}

/// <summary>
/// EDITOR ONLY. Drives the showcase on the open hub panel through its own proof seams; the editor
/// bridge calls these (invoke_static). Every step logs one line, so the walkthrough reads from the log.
/// </summary>
public static class LpBtcHubShowcase
{
	/// <summary>Positive code-string ID for the showcase driver. Exists nowhere else in the tree.</summary>
	public const string ShowcaseMark = "LP_BH_SHOWCASE1_20261019";

	// Static so close and reopen keep the same hub identity and stage; reset by hotload.
	private static LpBtcHubShowcaseFixture _fixture = new();

	private static LpBtcHubSkeleton? Open => Game.ActiveScene?.GetAllComponents<LpBtcHubSkeleton>().FirstOrDefault();

	private static LpBtcHubSkeleton? Ready( string step )
	{
		if ( !Application.IsEditor )
			return null;

		var panel = Open;
		if ( !panel.IsValid() )
			Log.Warning( "[btcui2] " + ShowcaseMark + " " + step + ": no hub panel open - run lp_btcui2_open first." );
		return panel.IsValid() ? panel : null;
	}

	/// <summary>Attach the showcase to the open panel. A first attach (or fresh = true) starts EMPTY with clean spy counts.</summary>
	public static void Attach( bool fresh )
	{
		var panel = Ready( "attach" );
		if ( panel is null )
			return;

		if ( fresh )
		{
			_fixture = new LpBtcHubShowcaseFixture();
			LpBtcHubCommandSpy.Reset();
		}

		panel.AttachShowcase( _fixture );
		Report( "attach" );
	}

	public static void Seed()
	{
		if ( Ready( "seed" ) is null )
			return;

		_fixture.Stage = LpBtcHubShowcaseStage.Seeded;
		Report( "seed" );
	}

	public static void Tab( int index )
	{
		Ready( "tab" )?.ProofSelectTabIndex( index );
		Report( "tab" );
	}

	public static void Rack( string token )
	{
		Ready( "rack" )?.ProofSelectRack( token );
		Report( "rack" );
	}

	public static void Back()
	{
		Ready( "back" )?.ProofSelectRack( string.Empty );
		Report( "back" );
	}

	/// <summary>Press an inert control by its action name, the same path a click takes.</summary>
	public static void Press( string action )
	{
		Ready( "press" )?.ProofPressInert( action );
		Report( "press" );
	}

	/// <summary>Positive control for the spy: one PIN request pushed through it with the showcase attached must be refused.</summary>
	public static void Probe()
	{
		Ready( "probe" )?.ProofMutationProbe();
		Report( "probe" );
	}

	public static void Report( string step )
	{
		var panel = Ready( "report" );
		if ( panel is null )
			return;

		Log.Info( "[btcui2] " + ShowcaseMark + " step=" + step
			+ " hub=" + panel.ViewHubId + " source=" + panel.ProvenanceText + " stage=" + _fixture.Stage
			+ " tab=" + panel.ActiveTabName + " rack=" + ( string.IsNullOrEmpty( panel.SelectedRackToken ) ? "(none)" : panel.SelectedRackToken )
			+ " racks=" + _fixture.RackCount + " wallet=" + _fixture.WalletBtc + " pending=" + _fixture.PendingBtc
			+ " unread=" + _fixture.UnreadAlerts + " blocked=" + LpBtcHubCommandSpy.BlockedCount
			+ " mutations=" + LpBtcHubCommandSpy.ShowcaseMutationAttempts );
	}
}
