// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BTC-UI-2 read-only half (DRIVE-AWAY-1 B): ILpBitcoinHubState over one live LpBitcoinHubEntity.
// Reads [Sync] fields and pure statics only. No rail, no host call, no money path, no write.
// Rack figures use the same members and maths as the Bitcoin Ops panel (LpHashdPanel), so one
// fact reads one way: next quote HP:1232-1233, projected clock HP:1354-1355, payback
// HP:1376-1407, invested receipt HP:1463-1470, buffer percent HP:1476-1480.
// Placeholders, by design: LastResult (only the host rail envelopes carry it, HE:1254-1329) and
// Rack.MaxTier (no named constant; the ladder clamps at 5, CT:131).

using System;
using System.Collections.Generic;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

public sealed class LpBitcoinHubStateLive : ILpBitcoinHubState
{
	/// <summary>Positive code-string ID for the live binding. Exists nowhere else in the tree.</summary>
	public const string LiveMark = "LP_BTCUI2_LIVE_STATE_20261017";

	/// <summary>Positive code-string ID for the BTC-UI-3 polish (live top tier, unread dot, unclipped panels).</summary>
	public const string PolishMark = "LP_BTCUI3_POLISH_20261017";

	// Fallback only: the ladder clamps at 5 (CT:131, HP:1233) when the compute track is not registered on this client.
	private const int LadderTopTier = 5;

	/// <summary>BTC-UI-3: the ladder's top tier from the registered compute track (CT:53-56), else the clamp.</summary>
	public static int TrackTopTier( out bool fromTrack )
	{
		var def = LifePunchUpgradeTracks.Get( LpBitcoinComputeTrack.TrackId );
		fromTrack = def is not null;
		return def is not null ? def.MaxTier : LadderTopTier;
	}

	private readonly LpBitcoinHubEntity? _hub;

	private IReadOnlyList<ILpBitcoinRackState> _racks = Array.Empty<ILpBitcoinRackState>();
	private IReadOnlyList<LpBitcoinHubAlertRow> _alerts = Array.Empty<LpBitcoinHubAlertRow>();
	private bool _built;
	private int _builtHash;
	private float _hashAt = -1f;
	private int _hashValue;

	/// <summary>Read one hub. A null or destroyed hub reads as HasHub = false with every member default.</summary>
	public LpBitcoinHubStateLive( LpBitcoinHubEntity? hub )
	{
		_hub = hub;
	}

	public LpBitcoinHubEntity? Hub => _hub;

	public bool HasHub => _hub.IsValid();
	public Guid HubId => HasHub ? _hub.GameObject.Id : Guid.Empty;
	// Derived from the one-hub cap (ID:45); a scene-wide count is not implemented here.
	public int OwnedHubCount => HasHub && _hub.Owner != 0 && _hub.Owner == LocalSteamId() ? 1 : 0;
	public bool IsPowered => HasHub && _hub.IsPowered;
	public bool PinSet => HasHub && _hub.AccessPinIsSet;
	public bool PinNeedsReEnroll => HasHub && _hub.AccessPinNeedsReEnrollment;
	public long OwnerSteamId => HasHub ? _hub.Owner : 0;
	public float WalletBtc => HasHub ? _hub.HubWalletBtc : 0f;
	// Display rate (EC:77), not the payout quote: shown as "est. before bonuses" (R-BTCUI-14).
	public float WalletUsd => LpBitcoinEconomy.BtcToCashUsd( WalletBtc );
	public float PendingBtc => HasHub ? _hub.GetRackPendingBtc() : 0f;
	public float PendingUsd => LpBitcoinEconomy.BtcToCashUsd( PendingBtc );
	public float UsdPerBtc => LpBitcoinEconomy.CashUsdPerBtc;
	public bool RateEventActive => MathF.Abs( LpBitcoinEconomy.CashRateMultiplier - 1f ) >= 0.001f;
	public float RateEventMultiplier => LpBitcoinEconomy.CashRateMultiplier;
	public bool TerminalLinked => HasHub && _hub.HasLinkedTerminal();
	// HE:510-511: an UNLINKED terminal within link range (the link prompt), position-based.
	public bool TerminalInRange => HasHub && _hub.HasNearbyUnlinkedTerminal();
	public int RackCount => HasHub ? _hub.GetLinkedRacks().Count : 0;
	public int RackMax => LpBitcoinIdent.PortalMaxRacksPerHub;
	public int UnreadAlerts => HasHub ? _hub.UnreadAlertCount : 0;
	public int LocalCashUsd => LpBitcoinWallet.GetLocalCash();

	// PLACEHOLDER: no client-readable source outside the host rail envelopes.
	public LpBitcoinHubLastResult LastResult => default;

	public IReadOnlyList<ILpBitcoinRackState> Racks
	{
		get
		{
			Refresh();
			return _racks;
		}
	}

	public IReadOnlyList<LpBitcoinHubAlertRow> Alerts
	{
		get
		{
			Refresh();
			return _alerts;
		}
	}

	/// <summary>One hash per frame over every input above (adds the four BS:42-61 misses).</summary>
	public int StateHash
	{
		get
		{
			if ( _hashAt == Time.Now )
				return _hashValue;

			_hashAt = Time.Now;
			_hashValue = ComputeHash();
			return _hashValue;
		}
	}

	private int ComputeHash()
	{
		var hash = new HashCode();
		hash.Add( HasHub );
		hash.Add( LpBitcoinEconomy.PortalEconomyRevision );
		hash.Add( LpBitcoinEconomy.CashRateMultiplier );
		hash.Add( LpBitcoinWallet.GetLocalCash() );

		if ( !HasHub )
			return hash.ToHashCode();

		hash.Add( _hub.GameObject.Id );
		hash.Add( _hub.Owner );
		hash.Add( _hub.IsPowered );
		hash.Add( _hub.AccessPinIsSet );
		hash.Add( _hub.AccessPinNeedsReEnrollment );
		hash.Add( _hub.HubWalletBtc );
		hash.Add( _hub.UnreadAlertCount );
		hash.Add( _hub.AlertFeed );
		hash.Add( _hub.HasLinkedTerminal() );
		hash.Add( _hub.HasNearbyUnlinkedTerminal() );

		foreach ( var rack in _hub.GetLinkedRacks() )
		{
			if ( !rack.IsValid() )
				continue;

			hash.Add( rack.GameObject.Id );
			hash.Add( rack.AssignedSlotToken );
			hash.Add( rack.AdvancedRack );
			hash.Add( rack.IsMining );
			hash.Add( rack.BitcoinAmount );
			hash.Add( rack.ClockGhz );
			hash.Add( rack.CoreCount );
			hash.Add( rack.ComputeTier );
		}

		// Alert ages move with time: re-read every ten seconds while the feed has rows.
		if ( !string.IsNullOrEmpty( _hub.AlertFeed ) )
			hash.Add( (int)(Time.Now / 10f) );

		return hash.ToHashCode();
	}

	private void Refresh()
	{
		var hash = StateHash;
		if ( _built && hash == _builtHash )
			return;

		_built = true;
		_builtHash = hash;

		var racks = new List<ILpBitcoinRackState>();
		var alerts = new List<LpBitcoinHubAlertRow>();

		if ( HasHub )
		{
			var linked = _hub.GetLinkedRacks();
			foreach ( var rack in linked )
			{
				if ( rack.IsValid() )
					racks.Add( LiveRack.Read( rack, linked ) );
			}

			foreach ( var alert in _hub.GetAlerts() )
			{
				// Host Time.Now stamps (HE:912): exact on the host, skewed on a remote client (UNVERIFIED).
				var age = (float)Math.Max( 0.0, Time.Now - alert.Timestamp );
				alerts.Add( new LpBitcoinHubAlertRow( alert.Kind, alert.Message ?? string.Empty, age ) );
			}
		}

		_racks = racks;
		_alerts = alerts;
	}

	private static long LocalSteamId()
	{
		long steamId = Game.SteamId;
		return steamId;
	}

	private sealed class LiveRack : ILpBitcoinRackState
	{
		public Guid Id { get; private init; }
		public string Token { get; private init; } = string.Empty;
		public string Name { get; private init; } = string.Empty;
		public bool Advanced { get; private init; }
		public bool IsMining { get; private init; }
		public float Btc { get; private init; }
		public float CapBtc { get; private init; }
		public int FillPct { get; private init; }
		public bool Full { get; private init; }
		public float InvestedBtc { get; private init; }
		public int Tier { get; private init; }
		public int MaxTier { get; private init; }
		public float ClockGhz { get; private init; }
		public float RateBtcPerMin { get; private init; }
		public float Yield { get; private init; }
		public long NextQuoteSats { get; private init; }
		public float NextQuoteUsd { get; private init; }
		public float NextClockGhz { get; private init; }
		public float PaybackMinutes { get; private init; }

		public static LiveRack Read( LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linked )
		{
			var token = !string.IsNullOrEmpty( rack.AssignedSlotToken )
				? rack.AssignedSlotToken
				: LpBitcoinIdent.FormatRackSlotTerminalToken( rack, linked );

			// AdvancedRack is [Property], not [Sync] (RE:32): the slot token is the client-safe signal.
			var advanced = rack.AdvancedRack
				|| string.Equals( token, LpBitcoinIdent.AdvancedRackTerminalToken, StringComparison.OrdinalIgnoreCase );

			var tier = rack.ComputeTier;
			var top = TrackTopTier( out _ );
			var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
			var btc = rack.BitcoinAmount;

			long invested = 0;
			for ( var t = 1; t <= tier; t++ )
			{
				var quote = LpBitcoinComputeTrack.QuoteSats( rack, t );
				if ( quote > 0 )
					invested += quote;
			}

			var nextQuote = tier >= top ? 0L : Math.Max( 0L, LpBitcoinComputeTrack.QuoteSats( rack, tier + 1 ) );
			var nextClock = tier >= top ? rack.ClockGhz : ProjectedClockGhz( tier + 1 );
			var delta = tier >= top
				? 0f
				: LpBitcoinEconomy.MiningRatePerMinute( ProjectedClockGhz( tier + 1 ), rack.CoreCount, rack.YieldMultiplier )
					- LpBitcoinEconomy.MiningRatePerMinute( ProjectedClockGhz( tier ), rack.CoreCount, rack.YieldMultiplier );

			return new LiveRack
			{
				Id = rack.GameObject.Id,
				Token = token ?? string.Empty,
				Name = LpBitcoinIdent.FormatRackSlotDisplayName( rack, linked ) ?? string.Empty,
				Advanced = advanced,
				IsMining = rack.IsMining,
				Btc = btc,
				CapBtc = cap,
				FillPct = cap > 0f ? (int)Math.Round( 100f * Math.Clamp( btc / cap, 0f, 1f ) ) : 0,
				Full = cap > 0f && btc >= cap,
				InvestedBtc = invested / 100_000_000f,
				Tier = tier,
				MaxTier = top,
				ClockGhz = rack.ClockGhz,
				RateBtcPerMin = rack.MiningRatePerMinute,
				Yield = rack.YieldMultiplier,
				NextQuoteSats = nextQuote,
				NextQuoteUsd = LpBitcoinEconomy.BtcToCashUsd( nextQuote / 100_000_000f ),
				NextClockGhz = nextClock,
				PaybackMinutes = delta > 0f && nextQuote > 0 ? (nextQuote / 100_000_000f) / delta : 0f,
			};
		}

		private static float ProjectedClockGhz( int tier )
			=> LpBitcoinEconomy.StartClockGhz * LpBitcoinComputeTrack.EffectMultiplierFor( tier );
	}
}
