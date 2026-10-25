// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LIFEPUNCH Player Hub for DXRP
// s&box ident: lifepunch.playerhub
// addon ident: playerhub
//
// The $BTC tab's ONE live read seam. Reads the viewer's Bitcoin Hub (found by owner SteamID) and
// its linked GPU racks, formats the display strings, and binds the switch/button actions to the
// EXISTING host rails on the entities. No money code lives here: SetPowered, RequestSetMining,
// RequestDepositRacksToHub and RequestCashOutAllHub are the hub/rack entities' own [Rpc.Host] paths
// (debit-before-await exemplars). World-side power animation and sound keep hanging off entity
// state (ApplyHubPowerVisual / ApplyRackMiningVisual / LpBitcoinPowerAnim); this file never touches
// a renderer.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using LifePunch.DXRP.Addons.Bitcoin;
using LifePunch.DXRP.Addons.Bitcoin.HubUi;

namespace LifePunch.DXRP.Addons.PlayerHub;

public static class LpPlayerHubBtcSource
{
	/// <summary>Positive code-string ID for the live seam. Exists nowhere else in the tree.</summary>
	public const string SeamMark = "LP_PLAYERHUB_BTC_LIVE_SEAM_20261016";

	private const string BtcFormat = "F6";

	// Parity with HASHD (LpHashdPanel): a rack's BTC is shown at F4 and its rate as TH/s via the cosmetic x18.5
	// display factor. HASHD is the in-world authority for these units until the principal rules otherwise.
	private const string RackBtcFormat = "F4";
	private const float HashdRateFactor = 18.5f;

	public static LpBitcoinHubEntity FindLocalHub()
	{
		var steamId = Game.SteamId;
		return steamId == 0 ? null : LpBitcoinHubEntity.FindHubByOwnerSteamId( steamId );
	}

	/// <summary>
	/// Folds the replicated state the tab renders into one hash, so the Root's BuildHash re-renders
	/// the panel when the host changes power, wallet or a rack. Cheap: a scene component walk per frame
	/// only while the $BTC tab is active.
	/// </summary>
	public static int LiveHash()
	{
		var hub = FindLocalHub();
		if ( !hub.IsValid() )
			return 0;

		var hash = new HashCode();
		hash.Add( hub.IsPowered );
		hash.Add( hub.AccessPinIsSet );
		hash.Add( hub.HasLinkedTerminal() );
		hash.Add( LpBitcoinEconomy.PortalEconomyRevision );
		hash.Add( (long)(hub.HubWalletBtc * 100_000_000f) );
		foreach ( var rack in hub.GetLinkedRacks() )
		{
			hash.Add( rack.GameObject.Id );
			hash.Add( rack.IsMining );
			hash.Add( rack.ComputeTier );
			hash.Add( (long)(rack.BitcoinAmount * 100_000_000f) );
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// HUB-RACKS-1 read fact: the rack is at the top of the compute ladder (the track's own MaxTier, read from the
	/// registered track exactly as the hub UI reads it; no price is asked). The host's purchase rail refuses a
	/// request past the top (PreconditionTier), so the control says so ahead of the press.
	/// </summary>
	public static bool IsTopTier( LpBitcoinRackEntity rack )
		=> rack.ComputeTier >= LpBitcoinHubStateLive.TrackTopTier( out _ );

	/// <summary>The ladder label of the next tier ("TIER II"), or an empty string at the top.</summary>
	public static string NextTierLabelFor( LpBitcoinRackEntity rack )
		=> IsTopTier( rack ) ? string.Empty : "TIER " + LpBitcoinIdent.RomanTier( rack.ComputeTier + 1 );

	// --- HUB-RACKS-1: a rack upgrade through the ONE existing purchase rail ---------------------------------------
	// The request is the same host member HASHD calls (rack.RequestPurchaseComputeTier(), no arguments). The host does
	// every check, the quote, the debit and the ledger commit; this file prices nothing and writes no balance. The
	// answer comes back through the hub result relay (LpHubResultRelay -> ILpHubResult on the open Player Hub).

	public const string UpgradeDeniedNote = "Only the hub owner can buy upgrades.";

	/// <summary>Display mirror of the host's own manage test (the host still decides on every request).</summary>
	public static bool CanUpgradeHub( LpBitcoinHubEntity hub )
		=> hub.IsValid() && hub.CanManageHub( Connection.Local.Id );

	public static void RequestRackUpgrade( LpBitcoinHubEntity hub, Guid rackId, LpBtcUpgradeState upgrade, Action onChanged )
	{
		if ( upgrade is null || upgrade.Pending || !hub.IsValid() )
			return;

		if ( !CanUpgradeHub( hub ) )
		{
			Say( upgrade, false, UpgradeDeniedNote, onChanged );
			return;
		}

		var rack = hub.GetLinkedRacks().FirstOrDefault( r => r.IsValid() && r.GameObject.Id == rackId );
		if ( !rack.IsValid() )
		{
			Say( upgrade, false, "That rack is no longer linked to this hub.", onChanged );
			return;
		}

		if ( IsTopTier( rack ) )
		{
			Say( upgrade, false, "This rack is already at the top tier.", onChanged );
			return;
		}

		upgrade.Note = string.Empty;
		// Pending first: on a listen host the answer can arrive inside the call and must win.
		upgrade.RackId = rackId;
		Click();
		rack.RequestPurchaseComputeTier();
		onChanged?.Invoke();
	}

	/// <summary>
	/// The host's answer, delivered by the hub result relay to the caller that asked. Consumed only while this panel
	/// is waiting for one, only for the Bitcoin hub's compute-tier result. Payload: code;newTier;costSats;shortfallSats
	/// (the existing purchase envelope; the figures are not shown or used here).
	/// </summary>
	public static void OnRelayResult( LpBtcUpgradeState upgrade, string hubKey, Guid callerId, string kind, string payload, Action onChanged )
	{
		if ( upgrade is null || !upgrade.Pending )
			return;

		if ( hubKey != LpBitcoinHubEntity.RelayHubKey || kind != LpBitcoinHubEntity.RelayKindComputeTier )
			return;

		if ( callerId != Connection.Local.Id )
			return;

		upgrade.RackId = Guid.Empty;
		var parts = ( payload ?? string.Empty ).Split( ';' );
		if ( parts.Length < 2 || !int.TryParse( parts[0], out var code ) || !int.TryParse( parts[1], out var newTier ) )
		{
			Say( upgrade, false, "The hub did not accept the upgrade.", onChanged );
			return;
		}

		var result = (LpBitcoinPurchaseResultCode)code;
		if ( result == LpBitcoinPurchaseResultCode.Ok )
			Say( upgrade, true, "Compute TIER " + LpBitcoinIdent.RomanTier( newTier ) + " is online.", onChanged );
		else if ( result == LpBitcoinPurchaseResultCode.InsufficientFunds )
			Say( upgrade, false, "The hub wallet does not have enough for this upgrade. Nothing was charged.", onChanged );
		else if ( result == LpBitcoinPurchaseResultCode.PreconditionTier )
			Say( upgrade, false, "That upgrade is not available. Nothing was charged.", onChanged );
		else if ( result == LpBitcoinPurchaseResultCode.HubGuard )
			Say( upgrade, false, "Hub access is required to buy upgrades.", onChanged );
		else
			Say( upgrade, false, "The hub did not accept the upgrade.", onChanged );
	}

	private static void Say( LpBtcUpgradeState upgrade, bool good, string note, Action onChanged )
	{
		upgrade.NoteGood = good;
		upgrade.Note = note;
		onChanged?.Invoke();
	}

	public static LpBtcVm Build( Action onChanged, LpBtcUpgradeState upgrade )
	{
		upgrade ??= new LpBtcUpgradeState();
		var hub = FindLocalHub();
		if ( !hub.IsValid() )
		{
			return new LpBtcVm(
				HasHub: false,
				IsPowered: false,
				PinSet: false,
				CanManage: false,
				TerminalLinked: false,
				WalletBtc: 0f.ToString( BtcFormat ),
				WalletUsd: "0",
				PendingBtc: 0f.ToString( BtcFormat ),
				RackCount: 0,
				Racks: Array.Empty<LpBtcRackVm>(),
				TogglePower: null,
				DepositAll: null,
				CashOutAll: null,
				Note: "No Bitcoin Hub is owned by this player. Place a hub to mine.",
				CanUpgrade: false,
				UpgradeHint: UpgradeDeniedNote,
				UpgradeRackId: string.Empty,
				UpgradeNote: string.Empty,
				UpgradeNoteGood: false);
		}

		var linked = hub.GetLinkedRacks();
		var racks = new List<LpBtcRackVm>( linked.Count );
		foreach ( var rack in linked )
		{
			if ( !rack.IsValid() )
				continue;

			var r = rack;
			racks.Add( new LpBtcRackVm(
				Id: r.GameObject.Id.ToString(),
				Label: LpBitcoinIdent.FormatRackSlotDisplayName( r, linked ),
				TierLabel: r.ComputeTier > 0 ? "TIER " + LpBitcoinIdent.RomanTier( r.ComputeTier ) : "STOCK",
				ClockLabel: $"{r.ClockGhz:0.0} GHz x {r.CoreCount}",
				RateLabel: $"{r.MiningRatePerMinute * HashdRateFactor:F1} TH/s",
				MinedBtc: r.BitcoinAmount.ToString( RackBtcFormat ),
				MinedUsd: r.UsdValue.ToString( "N0" ),
				IsMining: r.IsMining,
				Advanced: r.AdvancedRack,
				ToggleMining: () =>
				{
					if ( !r.IsValid() ) return;
					Click();
					r.RequestSetMining( !r.IsMining );
					onChanged?.Invoke();
				},
				AtTopTier: IsTopTier( r ),
				NextTierLabel: NextTierLabelFor( r ),
				Upgrade: () => RequestRackUpgrade( hub, r.GameObject.Id, upgrade, onChanged ) ) );
		}

		var pending = hub.GetRackPendingBtc();
		var h = hub;
		var terminalLinked = h.HasLinkedTerminal();
		var note = h.IsPowered
			? "Hub powered. Racks mine while switched on; deposit moves mined BTC into the hub wallet."
			: "Hub is off. Racks stop when the hub powers down; switch it on to mine.";
		if ( !terminalLinked )
			note += " No terminal is linked to this hub, so Deposit all is off until you link your terminal at the hub.";

		return new LpBtcVm(
			HasHub: true,
			IsPowered: h.IsPowered,
			PinSet: h.AccessPinIsSet,
			CanManage: true,
			TerminalLinked: terminalLinked,
			WalletBtc: h.HubWalletBtc.ToString( BtcFormat ),
			WalletUsd: LpBitcoinEconomy.BtcToCashUsd( h.HubWalletBtc ).ToString( "N0" ),
			PendingBtc: pending.ToString( BtcFormat ),
			RackCount: racks.Count,
			Racks: racks,
			TogglePower: () =>
			{
				if ( !h.IsValid() ) return;
				Click();
				h.SetPowered( !h.IsPowered );
				onChanged?.Invoke();
			},
			DepositAll: () =>
			{
				if ( !h.IsValid() ) return;
				// The host drops a deposit when no terminal is linked; do not click-and-wait on a no-op.
				if ( !h.HasLinkedTerminal() ) return;
				Click();
				h.RequestDepositRacksToHub();
				onChanged?.Invoke();
			},
			CashOutAll: () =>
			{
				if ( !h.IsValid() ) return;
				Click();
				h.RequestCashOutAllHub();
				onChanged?.Invoke();
			},
			Note: note,
			CanUpgrade: CanUpgradeHub( h ),
			UpgradeHint: UpgradeDeniedNote,
			UpgradeRackId: upgrade.Pending ? upgrade.RackId.ToString() : string.Empty,
			UpgradeNote: upgrade.Note,
			UpgradeNoteGood: upgrade.NoteGood );
	}

	// UI click until CC-ASSETS-1 ships a dedicated switch sound; the world-side on/off sounds hang off entity state.
	private static void Click()
	{
		try { LpBitcoinUiSounds.PlayKeyboardClick(); }
		catch ( Exception e ) { Log.Warning( "[playerhub] switch click sound failed: " + e.Message ); }
	}
}
