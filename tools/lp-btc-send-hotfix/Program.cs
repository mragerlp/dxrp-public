using System;
using System.Globalization;
using LifePunch.DXRP.Addons.Bitcoin;

internal static class Program
{
	private static int _failed;

	public static int Main()
	{
		// Fresh proof that the .NET parse hole still exists on this runtime.
		var parsed = float.TryParse( "NaN", NumberStyles.Float, CultureInfo.InvariantCulture, out var nan );
		Expect( "legacy-probe parsed NaN", parsed && float.IsNaN( nan ) );
		Expect( "legacy-probe amount <= 0 is false for NaN", !( nan <= 0f ) );
		Expect( "legacy-probe amount > balance is false for NaN", !( nan > 1f ) );

		Parser_RejectsNonFiniteTokens();
		Parser_RejectsZeroNegativeAndInsufficient();
		Parser_AcceptsAllAndValidAmount();
		Host_RejectsNonFiniteZeroNegativeAndInsufficient();
		Host_RejectsAuthSelfAndMissingRecipient();
		Host_AcceptsOneValidTransfer();
		Apply_RejectsPoisonedRecipientOverflowAndNaN();
		Apply_AllChainMatchesManualProof();

		if ( _failed > 0 )
		{
			Console.Error.WriteLine( $"FAILED {_failed} assertion(s)" );
			return 1;
		}

		Console.WriteLine( "LpBitcoinHubSendRules: all focused send-hotfix cases passed" );
		return 0;
	}

	private static void Parser_RejectsNonFiniteTokens()
	{
		ExpectRejectParse( "NaN", 1f, "NaN token" );
		ExpectRejectParse( "Infinity", 1f, "+Infinity token" );
		ExpectRejectParse( "+Infinity", 1f, "explicit +Infinity token" );
		ExpectRejectParse( "-Infinity", 1f, "-Infinity token" );
		ExpectRejectParse( "all", float.NaN, "all against NaN wallet" );
		ExpectRejectParse( "all", float.PositiveInfinity, "all against +Inf wallet" );
	}

	private static void Parser_RejectsZeroNegativeAndInsufficient()
	{
		ExpectRejectParse( "0", 1f, "zero amount" );
		ExpectRejectParse( "0.0", 1f, "zero.0 amount" );
		ExpectRejectParse( "-0.5", 1f, "negative amount" );
		ExpectRejectParse( "2", 1f, "insufficient funds" );
		ExpectRejectParse( "all", 0f, "all against empty wallet" );
	}

	private static void Parser_AcceptsAllAndValidAmount()
	{
		Expect(
			"parser accepts 0.25",
			LpBitcoinHubSendRules.TryParseSendAmount( "0.25", 1f, out var amount, out var error )
			&& amount == 0.25f
			&& error.Length == 0 );
		Expect(
			"parser accepts all",
			LpBitcoinHubSendRules.TryParseSendAmount( "all", 1.5f, out amount, out error )
			&& amount == 1.5f
			&& error.Length == 0 );
		Expect(
			"parser accepts ALL",
			LpBitcoinHubSendRules.TryParseSendAmount( "ALL", 0.1f, out amount, out error )
			&& amount == 0.1f
			&& error.Length == 0 );
	}

	private static void Host_RejectsNonFiniteZeroNegativeAndInsufficient()
	{
		ExpectHost( "host rejects NaN amount",
			LpBitcoinHubSendReject.NonFiniteAmount,
			amount: float.NaN, wallet: 2f );
		ExpectHost( "host rejects +Inf amount",
			LpBitcoinHubSendReject.NonFiniteAmount,
			amount: float.PositiveInfinity, wallet: 2f );
		ExpectHost( "host rejects -Inf amount",
			LpBitcoinHubSendReject.NonFiniteAmount,
			amount: float.NegativeInfinity, wallet: 2f );
		ExpectHost( "host rejects NaN wallet",
			LpBitcoinHubSendReject.NonFiniteAmount,
			amount: 0.5f, wallet: float.NaN );
		ExpectHost( "host rejects zero",
			LpBitcoinHubSendReject.NonPositiveAmount,
			amount: 0f, wallet: 2f );
		ExpectHost( "host rejects negative",
			LpBitcoinHubSendReject.NonPositiveAmount,
			amount: -1f, wallet: 2f );
		ExpectHost( "host rejects insufficient",
			LpBitcoinHubSendReject.InsufficientFunds,
			amount: 3f, wallet: 2f );
	}

	private static void Host_RejectsAuthSelfAndMissingRecipient()
	{
		Expect(
			"host rejects unauthorized caller",
			LpBitcoinHubSendRules.EvaluateHost(
				canOperate: false,
				hasLinkedTerminal: true,
				targetSteamId: 2,
				amount: 0.5f,
				hubWalletBtc: 1f,
				ownerSteamId: 1,
				recipientHubFound: true ) == LpBitcoinHubSendReject.Unauthorized );

		Expect(
			"host rejects missing terminal",
			LpBitcoinHubSendRules.EvaluateHost(
				canOperate: true,
				hasLinkedTerminal: false,
				targetSteamId: 2,
				amount: 0.5f,
				hubWalletBtc: 1f,
				ownerSteamId: 1,
				recipientHubFound: true ) == LpBitcoinHubSendReject.NoTerminal );

		Expect(
			"host rejects self-transfer",
			LpBitcoinHubSendRules.EvaluateHost(
				canOperate: true,
				hasLinkedTerminal: true,
				targetSteamId: 42,
				amount: 0.5f,
				hubWalletBtc: 1f,
				ownerSteamId: 42,
				recipientHubFound: true ) == LpBitcoinHubSendReject.SelfTransfer );

		Expect(
			"host rejects missing recipient hub",
			LpBitcoinHubSendRules.EvaluateHost(
				canOperate: true,
				hasLinkedTerminal: true,
				targetSteamId: 99,
				amount: 0.5f,
				hubWalletBtc: 1f,
				ownerSteamId: 1,
				recipientHubFound: false ) == LpBitcoinHubSendReject.MissingRecipient );

		Expect(
			"host rejects invalid steam id",
			LpBitcoinHubSendRules.EvaluateHost(
				canOperate: true,
				hasLinkedTerminal: true,
				targetSteamId: 0,
				amount: 0.5f,
				hubWalletBtc: 1f,
				ownerSteamId: 1,
				recipientHubFound: true ) == LpBitcoinHubSendReject.InvalidTarget );
	}

	private static void Host_AcceptsOneValidTransfer()
	{
		var verdict = LpBitcoinHubSendRules.EvaluateHost(
			canOperate: true,
			hasLinkedTerminal: true,
			targetSteamId: 2,
			amount: 0.25f,
			hubWalletBtc: 1f,
			ownerSteamId: 1,
			recipientHubFound: true );
		Expect( "host accepts one valid transfer", verdict == LpBitcoinHubSendReject.Accepted );

		var sender = 1f;
		var recipient = 0.5f;
		Expect(
			"valid transfer apply succeeds",
			LpBitcoinHubSendRules.TryApplyAcceptedTransfer( ref sender, ref recipient, 0.25f ) );
		Expect( "valid transfer debits sender", sender == 0.75f );
		Expect( "valid transfer credits recipient", recipient == 0.75f );

		var nanVerdict = LpBitcoinHubSendRules.EvaluateHost(
			canOperate: true,
			hasLinkedTerminal: true,
			targetSteamId: 2,
			amount: float.NaN,
			hubWalletBtc: 1f,
			ownerSteamId: 1,
			recipientHubFound: true );
		Expect( "NaN never reaches apply", nanVerdict != LpBitcoinHubSendReject.Accepted );
	}

	private static void Apply_RejectsPoisonedRecipientOverflowAndNaN()
	{
		ExpectUnchanged( "NaN recipient", 1f, float.NaN, 0.5f );
		ExpectUnchanged( "+Inf recipient", 1f, float.PositiveInfinity, 0.5f );
		ExpectUnchanged( "-Inf recipient", 1f, float.NegativeInfinity, 0.5f );
		ExpectUnchanged( "NaN sender", float.NaN, 1f, 0.5f );
		ExpectUnchanged( "NaN amount", 1f, 1f, float.NaN );
		ExpectUnchanged( "+Inf amount", 1f, 1f, float.PositiveInfinity );
		ExpectUnchanged( "MaxValue+MaxValue overflow", float.MaxValue, float.MaxValue, float.MaxValue );
		ExpectUnchanged( "zero amount at sink", 1f, 1f, 0f );
		ExpectUnchanged( "amount exceeds sender at sink", 0.25f, 1f, 0.5f );
	}

	private static void Apply_AllChainMatchesManualProof()
	{
		Expect(
			"all-chain parse 1.5",
			LpBitcoinHubSendRules.TryParseSendAmount( "all", 1.5f, out var amount, out var error )
			&& amount == 1.5f
			&& error.Length == 0 );

		var verdict = LpBitcoinHubSendRules.EvaluateHost(
			canOperate: true,
			hasLinkedTerminal: true,
			targetSteamId: 2,
			amount: amount,
			hubWalletBtc: 1.5f,
			ownerSteamId: 1,
			recipientHubFound: true );
		Expect( "all-chain host accepted", verdict == LpBitcoinHubSendReject.Accepted );

		var sender = 1.5f;
		var recipient = 0.25f;
		Expect(
			"all-chain apply succeeds",
			LpBitcoinHubSendRules.TryApplyAcceptedTransfer( ref sender, ref recipient, amount ) );
		Expect( "all-chain sender became 0", sender == 0f );
		Expect( "all-chain recipient became 1.75", recipient == 1.75f );
	}

	private static void ExpectUnchanged( string name, float sender, float recipient, float amount )
	{
		var beforeSender = sender;
		var beforeRecipient = recipient;
		var applied = LpBitcoinHubSendRules.TryApplyAcceptedTransfer( ref sender, ref recipient, amount );
		var senderSame = float.IsNaN( beforeSender ) ? float.IsNaN( sender ) : sender == beforeSender;
		var recipientSame = float.IsNaN( beforeRecipient ) ? float.IsNaN( recipient ) : recipient == beforeRecipient;
		Expect( $"sink rejects {name}", !applied && senderSame && recipientSame );
	}

	private static void ExpectRejectParse( string token, float wallet, string name )
	{
		var ok = LpBitcoinHubSendRules.TryParseSendAmount( token, wallet, out var amount, out var error );
		Expect( $"parser rejects {name}", !ok && amount == 0f && error.Length > 0 );
	}

	private static void ExpectHost( string name, LpBitcoinHubSendReject expected, float amount, float wallet )
	{
		var actual = LpBitcoinHubSendRules.EvaluateHost(
			canOperate: true,
			hasLinkedTerminal: true,
			targetSteamId: 2,
			amount: amount,
			hubWalletBtc: wallet,
			ownerSteamId: 1,
			recipientHubFound: true );
		Expect( name, actual == expected );
	}

	private static void Expect( string name, bool ok )
	{
		if ( ok )
		{
			Console.WriteLine( $"PASS {name}" );
			return;
		}

		_failed++;
		Console.Error.WriteLine( $"FAIL {name}" );
	}
}
