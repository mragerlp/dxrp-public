// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;
using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Structured purchase outcome (feedback amendment) — the socket the
/// slice-3 UI plugs into. Success carries the post-purchase facts; failures carry
/// a typed reason. Alert copy is emitted alongside, never instead.</summary>
internal enum LpBitcoinPurchaseResultCode
{
	Ok,
	/// <summary>Wallet short — <see cref="LpBitcoinPurchaseResult.ShortfallSats"/> set.</summary>
	InsufficientFunds,
	/// <summary>Sequential precondition / beyond max tier.</summary>
	PreconditionTier,
	/// <summary>Permission, claim, or target failures (hub access / no hub / no rack).</summary>
	HubGuard,
}

internal sealed class LpBitcoinPurchaseResult
{
	public LpBitcoinPurchaseResultCode Code { get; init; }
	public int NewTier { get; init; }
	public float NewClockGhz { get; init; }
	public long CostPaidSats { get; init; }
	public float NewBufferCap { get; init; }
	public long ShortfallSats { get; init; }
	public string Error { get; init; } = string.Empty;
}

/// <summary>
/// The rack_compute purchase flow — the ONE host path where money moves (slice 2).
/// Commit-order invariant: funds check → debit hub wallet → ledger append+flush
/// (which raises OnPurchase internally, commit-then-raise) → apply effects. The
/// debit and the append run in one synchronous host call with NO awaits between
/// them; a ledger rejection restores the debit exactly. Insufficient funds is a
/// clean rejection — nothing is written, no event fires. Every outcome returns
/// the structured envelope above.
/// </summary>
internal static class LpBitcoinPurchaseFlow
{
	/// <summary>
	/// Purchase a COMPUTE tier for a linked rack. Default: next sequential tier.
	/// <paramref name="tierOverride"/> (dev harness only) submits that tier AS-IS so
	/// gate proofs can exercise the ledger's sequential rejection AFTER a real debit
	/// (the debit-restore leg).
	/// </summary>
	internal static LpBitcoinPurchaseResult PurchaseComputeTierHost(
		LpBitcoinHubEntity hub,
		LpBitcoinRackEntity rack,
		Guid callerId,
		int tierOverride = 0 )
	{
		if ( !Networking.IsHost || hub is null || !hub.IsValid() || rack is null || !rack.IsValid() )
			return new LpBitcoinPurchaseResult { Code = LpBitcoinPurchaseResultCode.HubGuard, Error = "no hub/rack target" };

		if ( !hub.CanManageHub( callerId ) || hub.Owner == 0 )
		{
			hub.PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"COMPUTE upgrade denied — hub access required (secure boot / PIN)." );
			return new LpBitcoinPurchaseResult { Code = LpBitcoinPurchaseResultCode.HubGuard, Error = "hub access required" };
		}

		LpBitcoinComputeTrack.EnsureRegistered();

		var slot = LpBitcoinIdent.FormatRackSlotTerminalToken( rack, hub.GetLinkedRacks() );
		var subjectClass = LpBitcoinComputeTrack.ClassOf( rack );
		var current = LifePunchUpgradeLedger.MaxTier(
			hub.Owner, LpBitcoinComputeTrack.TrackId, slot, subjectClass );

		var requested = tierOverride > 0 ? tierOverride : current + 1;

		var quoteSats = LpBitcoinComputeTrack.QuoteSats( rack, requested );
		if ( quoteSats < 0 )
		{
			hub.PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"COMPUTE is at max tier on {slot} — no further upgrade." );
			return new LpBitcoinPurchaseResult { Code = LpBitcoinPurchaseResultCode.PreconditionTier, Error = "beyond max tier" };
		}

		var costBtc = quoteSats / 100_000_000f;
		if ( hub.HubWalletBtc < costBtc )
		{
			// Clean rejection: nothing written, no event, wallet untouched.
			var shortfallSats = quoteSats - (long)Math.Round( hub.HubWalletBtc * 100_000_000d );
			hub.PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"COMPUTE {LpBitcoinIdent.RomanTier( requested )} on {slot} costs {costBtc:F8} BTC — hub wallet has {hub.HubWalletBtc:F8}." );
			Log.Info( $"LP_PURCHASE rejected slot={slot} tier={requested} insufficient funds wallet={hub.HubWalletBtc:F8} cost={costBtc:F8} shortfallSats={shortfallSats}" );
			return new LpBitcoinPurchaseResult
			{
				Code = LpBitcoinPurchaseResultCode.InsufficientFunds,
				ShortfallSats = shortfallSats,
				Error = "insufficient funds",
			};
		}

		// ── Atomic money segment: debit → append+flush(+raise). No awaits in here. ──
		var walletBefore = hub.HubWalletBtc;
		hub.HubWalletBtc -= costBtc;

		if ( !LifePunchUpgradeLedger.TryCommitPurchase(
			hub.Owner, LpBitcoinComputeTrack.TrackId, slot, subjectClass,
			requested, quoteSats, callerId, out var error ) )
		{
			hub.HubWalletBtc = walletBefore; // restore the debit exactly
			hub.PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"COMPUTE upgrade rejected on {slot}: {error}" );
			Log.Info( $"LP_PURCHASE rejected slot={slot} tier={requested} {error} — debit restored wallet={hub.HubWalletBtc:F8}" );
			return new LpBitcoinPurchaseResult { Code = LpBitcoinPurchaseResultCode.PreconditionTier, Error = error };
		}

		rack.ComputeTier = requested;
		LpBitcoinComputeTrack.Apply( rack, requested );

		hub.PushAlertHost( LpBitcoinHubAlertKind.HubTransfer,
			$"COMPUTE {LpBitcoinIdent.RomanTier( requested )} online on {slot} — rate ×{LpBitcoinComputeTrack.EffectMultiplierFor( requested )} · -{costBtc:F8} BTC" );
		Log.Info(
			$"LP_PURCHASE ok slot={slot} class={subjectClass} tier={requested} costSats={quoteSats} wallet={walletBefore:F8}->{hub.HubWalletBtc:F8}" );
		hub.RefreshLinkedTerminalScreens();

		return new LpBitcoinPurchaseResult
		{
			Code = LpBitcoinPurchaseResultCode.Ok,
			NewTier = requested,
			NewClockGhz = rack.ClockGhz,
			CostPaidSats = quoteSats,
			NewBufferCap = LpBitcoinEconomy.RackBtcCapacityFor( rack ),
		};
	}
}
