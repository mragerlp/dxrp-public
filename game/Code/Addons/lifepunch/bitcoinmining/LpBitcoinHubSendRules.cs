// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Host-authoritative hub-to-hub send gates. Parser and RPC share these so
/// <c>float.TryParse("NaN")</c> cannot skip <c>amount &lt;= 0</c> / <c>amount &gt; wallet</c>.
/// </summary>
internal enum LpBitcoinHubSendReject
{
	Accepted = 0,
	Unauthorized,
	NoTerminal,
	InvalidTarget,
	NonFiniteAmount,
	NonPositiveAmount,
	InsufficientFunds,
	SelfTransfer,
	MissingRecipient
}

internal static class LpBitcoinHubSendRules
{
	internal const float ParserWalletEpsilon = 0.000001f;

	internal static bool IsFiniteBtc( float value ) =>
		!float.IsNaN( value ) && !float.IsInfinity( value );

	internal static bool TryParseSendAmount(
		string token,
		float hubWalletBtc,
		out float amount,
		out string error )
	{
		amount = 0f;
		error = string.Empty;

		if ( token.Equals( "all", StringComparison.OrdinalIgnoreCase ) )
		{
			amount = hubWalletBtc;
			if ( !IsFiniteBtc( amount ) )
			{
				error = "ERR invalid amount — use a positive BTC value or all";
				amount = 0f;
				return false;
			}

			if ( amount <= 0f )
			{
				error = "ERR hub wallet empty";
				return false;
			}

			return true;
		}

		if ( !float.TryParse( token, out amount ) || !IsFiniteBtc( amount ) || amount <= 0f )
		{
			error = "ERR invalid amount — use a positive BTC value or all";
			amount = 0f;
			return false;
		}

		if ( !IsFiniteBtc( hubWalletBtc ) || amount > hubWalletBtc + ParserWalletEpsilon )
		{
			error = $"ERR insufficient hub wallet ({hubWalletBtc:F6} BTC available)";
			amount = 0f;
			return false;
		}

		return true;
	}

	internal static LpBitcoinHubSendReject EvaluateHost(
		bool canOperate,
		bool hasLinkedTerminal,
		long targetSteamId,
		float amount,
		float hubWalletBtc,
		long ownerSteamId,
		bool recipientHubFound )
	{
		if ( !canOperate )
			return LpBitcoinHubSendReject.Unauthorized;

		if ( !hasLinkedTerminal )
			return LpBitcoinHubSendReject.NoTerminal;

		if ( targetSteamId <= 0 )
			return LpBitcoinHubSendReject.InvalidTarget;

		if ( !IsFiniteBtc( amount ) || !IsFiniteBtc( hubWalletBtc ) )
			return LpBitcoinHubSendReject.NonFiniteAmount;

		if ( amount <= 0f )
			return LpBitcoinHubSendReject.NonPositiveAmount;

		if ( amount > hubWalletBtc )
			return LpBitcoinHubSendReject.InsufficientFunds;

		if ( ownerSteamId != 0 && ownerSteamId == targetSteamId )
			return LpBitcoinHubSendReject.SelfTransfer;

		if ( !recipientHubFound )
			return LpBitcoinHubSendReject.MissingRecipient;

		return LpBitcoinHubSendReject.Accepted;
	}

	/// <summary>
	/// Fail-closed mutation. Rejects non-finite amount/wallets and non-finite
	/// computed results. Neither balance is written unless both next values pass.
	/// </summary>
	internal static bool TryApplyAcceptedTransfer(
		ref float senderWalletBtc,
		ref float recipientWalletBtc,
		float amount )
	{
		if ( !IsFiniteBtc( amount ) || amount <= 0f )
			return false;

		if ( !IsFiniteBtc( senderWalletBtc ) || !IsFiniteBtc( recipientWalletBtc ) )
			return false;

		if ( amount > senderWalletBtc )
			return false;

		var nextSender = senderWalletBtc - amount;
		var nextRecipient = recipientWalletBtc + amount;
		if ( !IsFiniteBtc( nextSender ) || !IsFiniteBtc( nextRecipient ) || nextSender < 0f )
			return false;

		senderWalletBtc = nextSender;
		recipientWalletBtc = nextRecipient;
		return true;
	}
}
