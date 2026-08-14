// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// rack_compute — tenant #1 of the purchase ledger and the House Pattern reference
/// implementation (ECONOMY_DOCTRINE · UPGRADE_ARC_DESIGN decision 1). Unified COMPUTE
/// track, tiers I–V; the legacy CpuUpgradeLevel/CoreUpgradeLevel pair is deleted
/// (slice 2, GO ruling R2). Base price ladder 0.25/0.75/2/6/16 BTC (decision 4);
/// Advanced pays ladder × yield multiplier read at quote time (decision 5). Effects
/// are ABSOLUTE: Apply(tier) sets ClockGhz + CoreCount from tier alone, rate vector
/// ×2/4/8/16/32 (decision 2).
/// </summary>
internal static class LpBitcoinComputeTrack
{
	public const string TrackId = "rack_compute";

	/// <summary>Subject classes — apply honors tier ONLY on class match (GO ruling R1).</summary>
	public const string ClassStandard = "gpurack";
	public const string ClassAdvanced = "advancedgpurack";

	private static bool _registered;
	private static bool _mismatchWarned;
	private static int[] _effectLadder;   // [0..5], index 0 = T0 (×1). Latched global-once (Packet O FLAG 2).
	private static long[] _priceLadder;   // [0..4] = tiers I..V. Latched global-once (Packet O FLAG 2).

	/// <summary>Latch the process-wide rack_compute ladder from a rack's T3 config. ONE-SHOT (Packet O
	/// FLAG 2): the ladder is a single global registry entry, so the FIRST rack to reconcile wins and
	/// a later rack whose config would latch DIFFERENT values is ignored — and logged once as a
	/// mismatch (e.g. a separately-tuned advancedgpurack content type). Passing no config latches the
	/// shipped defaults; the reconcile call site (LpBitcoinRackEntity.ReconcileComputeTierHost) is the
	/// canonical latch point and always precedes any QuoteSats.</summary>
	public static void EnsureRegistered( LpBitcoinRackConfig config = null )
	{
		if ( _registered )
		{
			if ( config is not null )
				WarnOnLadderMismatch( config );
			return;
		}

		config ??= new LpBitcoinRackConfig();
		_effectLadder = BuildEffectLadder( config );
		_priceLadder = BuildPriceLadder( config );
		_registered = true;

		LifePunchUpgradeTracks.Register( new LifePunchTrackDef
		{
			Id = TrackId,
			MaxTier = System.Math.Clamp( config.MaxTier, 1, 5 ),
			PriceLadderSats = _priceLadder,
			SubjectKind = LifePunchTrackSubjectKind.Slot,
		} );
	}

	private static int[] BuildEffectLadder( LpBitcoinRackConfig c ) => new[]
	{
		1,                        // T0 — stock
		c.Tier1EffectMultiplier,  // I
		c.Tier2EffectMultiplier,  // II
		c.Tier3EffectMultiplier,  // III
		c.Tier4EffectMultiplier,  // IV
		c.Tier5EffectMultiplier,  // V
	};

	private static long[] BuildPriceLadder( LpBitcoinRackConfig c ) => new[]
	{
		c.Tier1CostSats, // I
		c.Tier2CostSats, // II
		c.Tier3CostSats, // III
		c.Tier4CostSats, // IV
		c.Tier5CostSats, // V
	};

	/// <summary>Surface a divergent later config that the one-shot latch discards (Packet O FLAG 2).
	/// Warns once — ReconcileComputeTierHost re-runs every membership sweep.</summary>
	private static void WarnOnLadderMismatch( LpBitcoinRackConfig config )
	{
		if ( _mismatchWarned )
			return;

		var candidateEffect = BuildEffectLadder( config );
		var candidatePrice = BuildPriceLadder( config );
		if ( LaddersEqual( candidateEffect, _effectLadder ) && LaddersEqual( candidatePrice, _priceLadder ) )
			return;

		_mismatchWarned = true;
#if !LIFEPUNCH_LOCAL
		Log.Warning(
			$"[lifepunch.bitcoin] rack_compute ladder already latched; a later rack config diverges and is " +
			$"IGNORED (one global ladder). latched effect=[{string.Join( ",", _effectLadder )}] config effect=" +
			$"[{string.Join( ",", candidateEffect )}] latched price=[{string.Join( ",", _priceLadder )}] config " +
			$"price=[{string.Join( ",", candidatePrice )}]." );
#endif
	}

	private static bool LaddersEqual( int[] a, int[] b )
	{
		if ( a is null || b is null || a.Length != b.Length )
			return false;
		for ( var i = 0; i < a.Length; i++ )
			if ( a[i] != b[i] )
				return false;
		return true;
	}

	private static bool LaddersEqual( long[] a, long[] b )
	{
		if ( a is null || b is null || a.Length != b.Length )
			return false;
		for ( var i = 0; i < a.Length; i++ )
			if ( a[i] != b[i] )
				return false;
		return true;
	}

	public static string ClassOf( LpBitcoinRackEntity rack )
		=> rack.AdvancedRack ? ClassAdvanced : ClassStandard;

	/// <summary>Rate multiplier vs stock for a tier: ×1 at T0, config ladder at I–V (shipped
	/// ×2/4/8/16/32). Reads the global latched ladder; falls back to the stock 1&lt;&lt;tier shape
	/// before the latch or out of range.</summary>
	public static int EffectMultiplierFor( int tier )
	{
		var t = System.Math.Clamp( tier, 0, 5 );
		if ( _effectLadder is not null && t < _effectLadder.Length )
			return _effectLadder[t];
		return 1 << t;
	}

	/// <summary>Absolute effect apply — ClockGhz + CoreCount derive from tier ALONE
	/// (decision 2). One knob: the clock carries the whole vector; cores stay stock.
	/// Idempotent; also runs at reconcile so a rehydrated rack re-derives its rate.</summary>
	public static void Apply( LpBitcoinRackEntity rack, int tier )
	{
		rack.ClockGhz = LpBitcoinEconomy.StartClockGhz * EffectMultiplierFor( tier );
		rack.CoreCount = LpBitcoinEconomy.StartCores;
	}

	/// <summary>Quote for a tier on THIS rack — base ladder × the rack's yield
	/// multiplier, read at quote time (decision 5: Advanced pays 2× for 2× throughput).
	/// Returns -1 when the tier has no price.</summary>
	public static long QuoteSats( LpBitcoinRackEntity rack, int tier )
	{
		EnsureRegistered();
		var baseSats = LifePunchUpgradeTracks.Get( TrackId )?.PriceSatsForTier( tier ) ?? -1;
		if ( baseSats < 0 )
			return -1;

		return (long)System.Math.Round( baseSats * (double)rack.YieldMultiplier );
	}
}
