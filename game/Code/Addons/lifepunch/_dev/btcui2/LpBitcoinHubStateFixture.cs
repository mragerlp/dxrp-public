// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// BTC-UI-2 pre-build (slice SHELL-1b): a constant fixture for ILpBitcoinHubState and the UNRULED
// text seam. Nothing here reads a component, a [Sync] field, the economy or a wallet; every figure
// is a literal chosen to exercise a layout state. UNVERIFIED in the editor until the driving seat
// proves it.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

/// <summary>
/// Fixture hub: powered, PIN set, terminal linked, two of three slots filled (gpurack-1 mining,
/// advancedgpurack full), gpurack-2 empty so the empty-slot hint renders. Illustrative only: the
/// figures are not computed by LpBitcoinEconomy and must never be read as live state.
/// </summary>
public sealed class LpBitcoinHubStateFixture : ILpBitcoinHubState
{
	public bool HasHub => true;
	public Guid HubId { get; } = new( "6b7f2a10-0000-4000-8000-00000000b7c1" );
	public int OwnedHubCount => 1;
	public bool IsPowered => true;
	public bool PinSet => true;
	public bool PinNeedsReEnroll => false;
	public long OwnerSteamId => 76561190000000001L;
	public float WalletBtc => 0.0125f;
	public float WalletUsd => 62.5f;
	public float PendingBtc => 0.5002f;
	public float PendingUsd => 2501f;
	public float UsdPerBtc => 5000f;
	public bool RateEventActive => false;
	public float RateEventMultiplier => 1f;
	public bool TerminalLinked => true;
	public bool TerminalInRange => true;
	public int RackCount => Racks.Count;
	public int RackMax => 3;
	public int LocalCashUsd => 1840;

	public IReadOnlyList<ILpBitcoinRackState> Racks { get; } = new ILpBitcoinRackState[]
	{
		new FixtureRack(
			id: new Guid( "6b7f2a10-0000-4000-8000-0000000000a1" ),
			token: "gpurack-1",
			name: "GPU Rack 1",
			advanced: false,
			isMining: true,
			btc: 0.1802f,
			capBtc: 0.5f,
			investedBtc: 0.25f,
			tier: 2,
			clockGhz: 9.76f,
			rateBtcPerMin: 0.032533f,
			yield: 1f,
			nextQuoteSats: 50_000_000L,
			nextQuoteUsd: 2500f,
			nextClockGhz: 19.52f,
			paybackMinutes: 15.4f ),
		new FixtureRack(
			id: new Guid( "6b7f2a10-0000-4000-8000-0000000000a3" ),
			token: "advancedgpurack",
			name: "Advanced GPU Rack",
			advanced: true,
			isMining: false,
			btc: 1f,
			capBtc: 1f,
			investedBtc: 0f,
			tier: 0,
			clockGhz: 2.44f,
			rateBtcPerMin: 0.008133f,
			yield: 1.5f,
			nextQuoteSats: 10_000_000L,
			nextQuoteUsd: 500f,
			nextClockGhz: 4.88f,
			paybackMinutes: 12.3f ),
	};

	public IReadOnlyList<LpBitcoinHubAlertRow> Alerts { get; } = new[]
	{
		new LpBitcoinHubAlertRow( LpBitcoinHubAlertKind.RackCapacity, "Advanced GPU Rack buffer is full", 40f ),
		new LpBitcoinHubAlertRow( LpBitcoinHubAlertKind.TerminalCommand, "Terminal linked", 620f ),
		new LpBitcoinHubAlertRow( LpBitcoinHubAlertKind.HubTransfer, "Received 0.010000 from another hub", 4000f ),
	};

	public int UnreadAlerts => 1;

	public LpBitcoinHubLastResult LastResult { get; } = new( LpBitcoinHubResultKind.None, false, string.Empty, 0f );

	public int StateHash
	{
		get
		{
			var hash = new HashCode();
			hash.Add( HubId );
			hash.Add( IsPowered );
			hash.Add( PinSet );
			hash.Add( WalletBtc );
			hash.Add( PendingBtc );
			hash.Add( UsdPerBtc );
			hash.Add( TerminalLinked );
			hash.Add( UnreadAlerts );
			hash.Add( LocalCashUsd );
			foreach ( var rack in Racks )
			{
				hash.Add( rack.Id );
				hash.Add( rack.Token );
				hash.Add( rack.IsMining );
				hash.Add( rack.Btc );
				hash.Add( rack.Tier );
				hash.Add( rack.ClockGhz );
			}
			return hash.ToHashCode();
		}
	}

	private sealed class FixtureRack : ILpBitcoinRackState
	{
		public FixtureRack( Guid id, string token, string name, bool advanced, bool isMining, float btc, float capBtc,
			float investedBtc, int tier, float clockGhz, float rateBtcPerMin, float yield, long nextQuoteSats,
			float nextQuoteUsd, float nextClockGhz, float paybackMinutes )
		{
			Id = id;
			Token = token;
			Name = name;
			Advanced = advanced;
			IsMining = isMining;
			Btc = btc;
			CapBtc = capBtc;
			InvestedBtc = investedBtc;
			Tier = tier;
			ClockGhz = clockGhz;
			RateBtcPerMin = rateBtcPerMin;
			Yield = yield;
			NextQuoteSats = nextQuoteSats;
			NextQuoteUsd = nextQuoteUsd;
			NextClockGhz = nextClockGhz;
			PaybackMinutes = paybackMinutes;
		}

		public Guid Id { get; }
		public string Token { get; }
		public string Name { get; }
		public bool Advanced { get; }
		public bool IsMining { get; }
		public float Btc { get; }
		public float CapBtc { get; }
		public int FillPct => CapBtc > 0f ? (int)(Btc / CapBtc * 100f) : 0;
		public bool Full => Btc >= CapBtc;
		public float InvestedBtc { get; }
		public int Tier { get; }
		public int MaxTier => 5;
		public float ClockGhz { get; }
		public float RateBtcPerMin { get; }
		public float Yield { get; }
		public long NextQuoteSats { get; }
		public float NextQuoteUsd { get; }
		public float NextClockGhz { get; }
		public float PaybackMinutes { get; }
	}
}

/// <summary>
/// The text seam before R-BTCUI-1..4 are ruled. Spec gate G2 forbids writing a format constant
/// first, so every method prints the invariant default form of the number with no format string.
/// The ruled formatter replaces this class; no tab changes.
/// </summary>
public sealed class LpBitcoinHubFormatUnruled : ILpBitcoinHubFormat
{
	public static readonly LpBitcoinHubFormatUnruled Instance = new();

	private const float SatsPerBtc = 100_000_000f;

	public string Btc( float btc ) => btc.ToString( CultureInfo.InvariantCulture );
	public string BtcPrice( long sats ) => (sats / SatsPerBtc).ToString( CultureInfo.InvariantCulture );
	public string Usd( float usd ) => usd.ToString( CultureInfo.InvariantCulture );
	public string RatePerMin( float btcPerMin ) => btcPerMin.ToString( CultureInfo.InvariantCulture );
	public string Clock( float ghz ) => ghz.ToString( CultureInfo.InvariantCulture ) + " GHz";
	public string Tier( int tier ) => tier <= 0 ? "STOCK" : "tier " + tier.ToString( CultureInfo.InvariantCulture );
	public string Yield( float factor ) => factor.ToString( CultureInfo.InvariantCulture ) + "x";
	public string Payback( float minutes ) => minutes.ToString( CultureInfo.InvariantCulture ) + " min";
	public string Age( float seconds ) => seconds.ToString( CultureInfo.InvariantCulture ) + " s ago";
}
