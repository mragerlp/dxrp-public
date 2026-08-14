// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Sandbox;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

internal readonly struct LpBitcoinDonorRate
{
	public LpBitcoinDonorRate( string tier, float multiplier )
	{
		Tier = tier;
		Multiplier = multiplier;
	}

	public string Tier { get; }
	public float Multiplier { get; }
}

/// <summary>A complete, immutable mined-BTC payout calculation. The final integer is floored once.</summary>
public readonly struct LpBitcoinPayoutQuote
{
	internal LpBitcoinPayoutQuote(
		float bitcoinAmount,
		int baseUsdPerBtc,
		float eventMultiplier,
		string donorTier,
		float donorMultiplier,
		float unroundedUsd,
		uint finalUsd )
	{
		BitcoinAmount = bitcoinAmount;
		BaseUsdPerBtc = baseUsdPerBtc;
		EventMultiplier = eventMultiplier;
		DonorTier = donorTier;
		DonorMultiplier = donorMultiplier;
		UnroundedUsd = unroundedUsd;
		FinalUsd = finalUsd;
	}

	public float BitcoinAmount { get; }
	public int BaseUsdPerBtc { get; }
	public float EventMultiplier { get; }
	public string DonorTier { get; }
	public float DonorMultiplier { get; }
	public float UnroundedUsd { get; }
	public uint FinalUsd { get; }

	public string BuildLedgerReason( string baseReason ) => FormattableString.Invariant(
		$"{baseReason} | btc={BitcoinAmount:0.########} baseUsdPerBtc={BaseUsdPerBtc} eventMultiplier={EventMultiplier:0.####} donorTier={DonorTier} donorMultiplier={DonorMultiplier:0.####} preFloorUsd={UnroundedUsd:0.####} finalUsd={FinalUsd}" );

	public string BuildAuditDescription( string rail ) => FormattableString.Invariant(
		$"rail={rail} btc={BitcoinAmount:0.########} baseUsdPerBtc={BaseUsdPerBtc} eventMultiplier={EventMultiplier:0.####} donorTier={DonorTier} donorMultiplier={DonorMultiplier:0.####} preFloorUsd={UnroundedUsd:0.####} finalUsd={FinalUsd}" );
}

internal static class LpBitcoinDonorPolicy
{
	internal const float VipMultiplier = 1.5f;
	internal const float EvipMultiplier = 2f;

	// Current portal identities recorded in lifepunch/portal/ranks/README.md. When the existing
	// ranks become OG ranks, their identity remains entitled; exact-name fallback covers new tiers.
	internal static readonly Guid VipRankId = new( "019db2da-c303-7c94-99e9-f3dbb6efb525" );
	internal static readonly Guid EvipRankId = new( "019db2db-0656-7893-ae2b-3ae1be47c186" );

	private static readonly LpBitcoinDonorRate None = new( "None", 1f );
	private static readonly LpBitcoinDonorRate Vip = new( "VIP", VipMultiplier );
	private static readonly LpBitcoinDonorRate Evip = new( "EVIP", EvipMultiplier );

	internal static LpBitcoinDonorRate ResolveRankName( string? rankName )
	{
		if ( string.Equals( rankName, "EVIP", StringComparison.OrdinalIgnoreCase ) ||
			string.Equals( rankName, "EVIP (OG)", StringComparison.OrdinalIgnoreCase ) )
		{
			return Evip;
		}

		if ( string.Equals( rankName, "VIP", StringComparison.OrdinalIgnoreCase ) ||
			string.Equals( rankName, "VIP (OG)", StringComparison.OrdinalIgnoreCase ) )
		{
			return Vip;
		}

		return None;
	}

	internal static LpBitcoinDonorRate ResolveAssignedRanks(
		IEnumerable<Guid> assignedRankIds,
		string? displayRankName )
	{
		var hasVip = false;
		foreach ( var rankId in assignedRankIds )
		{
			if ( rankId == EvipRankId )
				return Evip;

			if ( rankId == VipRankId )
				hasVip = true;
		}

		return hasVip ? Vip : ResolveRankName( displayRankName );
	}

#if !LIFEPUNCH_LOCAL
	internal static LpBitcoinDonorRate ResolveSteamId( long steamId )
	{
		if ( !RankSystem.Instance.IsValid() )
			return None;

		return ResolveAssignedRanks(
			RankSystem.Instance.GetPlayerRankIds( steamId ),
			RankSystem.Instance.GetRankName( steamId ) );
	}

	internal static LpBitcoinDonorRate ResolveCaller( Guid callerId )
	{
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
			return None;

		return ResolveSteamId( player.SteamId );
	}
#endif
}

internal static class LpBitcoinPayoutMath
{
	internal static LpBitcoinPayoutQuote CreateQuote(
		float bitcoinAmount,
		int baseUsdPerBtc,
		float eventMultiplier,
		LpBitcoinDonorRate donorRate )
	{
		var safeBitcoin = MathF.Max( 0f, bitcoinAmount );
		var safeBaseUsd = Math.Max( 0, baseUsdPerBtc );
		var safeEvent = MathF.Max( 0f, eventMultiplier );
		var safeDonor = MathF.Max( 0f, donorRate.Multiplier );
		var unroundedUsd = safeBitcoin * safeBaseUsd * safeEvent * safeDonor;
		var finalUsd = unroundedUsd <= 0f ? 0u : (uint)MathF.Floor( unroundedUsd );

		return new LpBitcoinPayoutQuote(
			safeBitcoin,
			safeBaseUsd,
			safeEvent,
			donorRate.Tier,
			safeDonor,
			unroundedUsd,
			finalUsd );
	}
}

#if !LIFEPUNCH_LOCAL
internal static class LpBitcoinPayoutAudit
{
	internal static void RecordSuccessful( Guid callerId, string rail, LpBitcoinPayoutQuote quote )
	{
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			Log.Warning( $"LP_DONOR_PAYOUT_AUDIT missing player caller={callerId} rail={rail}" );
			return;
		}

		var auditQueued = ServerApiClient.Audit(
			"LifePunchBtcPayout",
			quote.BuildAuditDescription( rail ),
			player.SteamId );

		Log.Info( $"LP_DONOR_PAYOUT_SENSOR caller={callerId} steamId={player.SteamId} " +
			$"rail={rail} tier={quote.DonorTier} donorMultiplier={quote.DonorMultiplier:0.####} " +
			$"eventMultiplier={quote.EventMultiplier:0.####} finalUsd={quote.FinalUsd} auditQueued={auditQueued}" );
	}
}
#endif
