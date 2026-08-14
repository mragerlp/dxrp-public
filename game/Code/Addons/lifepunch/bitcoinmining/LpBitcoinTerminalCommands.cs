// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.Bitcoin;

internal readonly record struct LpBitcoinCommandResult( bool Ok, string Line );

internal static class LpBitcoinTerminalCommands
{
	public static LpBitcoinCommandResult Execute(
		LpBitcoinHubEntity hub,
		ref int selectedIndex,
		string raw )
	{
		if ( !hub.IsValid() )
			return new LpBitcoinCommandResult( false, "ERR no hub" );

		if ( !hub.IsPowered )
			return new LpBitcoinCommandResult( false, "ERR hub power off — enable at hub admin panel" );

		var parts = ( raw ?? string.Empty ).Trim().Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 )
			return new LpBitcoinCommandResult( true, string.Empty );

		var cmd = parts[0].ToLowerInvariant();
		var racks = hub.GetLinkedRacks();

		switch ( cmd )
		{
			case "help":
			case "?":
				return new LpBitcoinCommandResult( true, HelpText() );

			case "racks":
			case "rigs":
				return ListRacks( racks );

			case "select":
				if ( parts.Length < 2
				     || !LpBitcoinIdent.TryResolveLinkedRackIndex( parts[1], racks, out var pick ) )
				{
					var usage = racks.Count == 0
						? "usage: select <rackId> — no linked racks"
						: "usage: select <rackId> — copy it from the hub Servers page";
					return new LpBitcoinCommandResult( false, usage );
				}

				selectedIndex = pick;
				return new LpBitcoinCommandResult( true, $"selected {LpBitcoinIdent.FormatRackSlotTerminalToken( racks[pick], racks )}" );

			case "status":
				return StatusSelected( hub, selectedIndex );

			case "info":
				return InfoSelected( hub, selectedIndex );

			case "wallet":
			case "hub":
				return WalletSummary( hub );

			case "mining":
				return ExecuteMining( hub, parts, ref selectedIndex );

			case "deposit":
				if ( parts.Length > 1 && parts[1].Equals( "all", StringComparison.OrdinalIgnoreCase ) )
					return DepositAll( hub );

				if ( parts.Length > 1 )
				{
					if ( !LpBitcoinIdent.TryResolveLinkedRackIndex( parts[1], hub.GetLinkedRacks(), out var depositPick ) )
					{
						return new LpBitcoinCommandResult( false,
							"ERR rack slot not found — use deposit <rackId> or deposit all" );
					}

					return DepositRack( hub, depositPick );
				}

				return DepositRack( hub, selectedIndex );

			case "send":
			case "transfer":
				return ExecuteSend( hub, parts );

			case "sell":
				return new LpBitcoinCommandResult( false, "ERR sell moved to hub admin — use deposit here, then cash out from hub wallet" );

			case "bitcoin":
				if ( parts.Length > 1 && parts[1].Equals( "sell", StringComparison.OrdinalIgnoreCase ) )
					return new LpBitcoinCommandResult( false, "ERR use hub admin wallet to cash out — type wallet" );

				return new LpBitcoinCommandResult( false, "usage: deposit | deposit all | deposit <index> | wallet" );

			case "link":
				if ( !hub.HasLinkedTerminal() )
					return new LpBitcoinCommandResult( false, "ERR link terminal at hub admin first (hub must be powered on)" );

				if ( parts.Length < 2 )
					return new LpBitcoinCommandResult( false, "usage: link <rackId> — copy it from the hub Servers page" );

				if ( !LpBitcoinIdent.TryParseLinkRackSlotToken( parts[1], out var linkAdvanced, out var linkStandardSlot ) )
					return new LpBitcoinCommandResult( false, "usage: link <rackId> — copy it from the hub Servers page" );

				if ( !LpBitcoinIdent.CanLinkToDeclaredSlot( linkAdvanced, linkStandardSlot, racks, out var declaredSlotError ) )
					return new LpBitcoinCommandResult( false, declaredSlotError );

				if ( racks.Count >= LpBitcoinIdent.PortalMaxRacksPerHub )
					return new LpBitcoinCommandResult( false,
						"ERR rack limit — this hub supports 2 GPU racks + 1 Advanced GPU Rack" );

				var slotToken = LpBitcoinIdent.FormatDeclaredLinkSlotToken( linkAdvanced, linkStandardSlot );
				hub.RequestLinkRack( parts[1] );
				return new LpBitcoinCommandResult( true, $"linking {slotToken} — hub confirms when rack in range" );

			case "unlink":
				if ( parts.Length < 2 )
					return new LpBitcoinCommandResult( false, "usage: unlink <rackId>" );

				if ( !LpBitcoinIdent.TryResolveLinkedRackIndex( parts[1], racks, out var unlinkPick ) )
					return new LpBitcoinCommandResult( false,
						racks.Count == 0
							? "ERR no linked racks to unlink"
							: "usage: unlink <rackId> — type racks to see what's linked" );

				var unlinkToken = LpBitcoinIdent.FormatRackSlotTerminalToken( racks[unlinkPick], racks );
				hub.RequestUnlinkRack( parts[1] );
				return new LpBitcoinCommandResult( true, $"unlinking {unlinkToken} — slot freed, ledger history kept" );

			case "about":
				return new LpBitcoinCommandResult( true,
					"LIFEPUNCH HASHD rig console\n(c) 2026 lifepunch.co — cash out at hub admin wallet" );

			default:
				return new LpBitcoinCommandResult( false,
					$"ERR unknown command '{cmd}' — commands are space-separated (type help)" );
		}
	}

	private static LpBitcoinCommandResult ExecuteMining(
		LpBitcoinHubEntity hub,
		string[] parts,
		ref int selectedIndex )
	{
		if ( parts.Length < 2 )
			return new LpBitcoinCommandResult( false, "usage: mining start|stop|all-start|all-stop" );

		var action = parts[1].ToLowerInvariant();
		var scope = parts.Length > 2 ? parts[2].ToLowerInvariant() : string.Empty;

		if ( action is "start" or "on" )
		{
			if ( scope is "all" )
				return MiningAll( hub, true );

			return MiningStart( hub, selectedIndex );
		}

		if ( action is "stop" or "off" )
		{
			if ( scope is "all" )
				return MiningAll( hub, false );

			return MiningStop( hub, selectedIndex );
		}

		return action switch
		{
			"all-start" or "all-on" => MiningAll( hub, true ),
			"all-stop" or "all-off" => MiningAll( hub, false ),
			_ => new LpBitcoinCommandResult( false, "usage: mining start|stop|all-start|all-stop" )
		};
	}

	private static LpBitcoinCommandResult ListRacks( IReadOnlyList<LpBitcoinRackEntity> racks )
	{
		if ( racks.Count == 0 )
			return new LpBitcoinCommandResult( true, "No linked GPU racks." );

		var lines = new StringBuilder();
		for ( var i = 0; i < racks.Count; i++ )
		{
			var rack = racks[i];
			if ( !rack.IsValid() )
				continue;

			lines.AppendLine( FormatRackLine( rack, racks ) );
		}

		return new LpBitcoinCommandResult( true, lines.ToString().TrimEnd() );
	}

	private static LpBitcoinCommandResult StatusSelected( LpBitcoinHubEntity hub, int index )
	{
		var racks = hub.GetLinkedRacks();
		var rack = hub.FindRackByIndex( index );
		if ( rack is null )
			return new LpBitcoinCommandResult( false, "ERR no rack selected — type racks / select <n>" );

		var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
		var fill = cap > 0f ? (int)Math.Round( 100f * rack.BitcoinAmount / cap ) : 0;
		var state = rack.IsMining ? "MINING" : "IDLE";
		if ( rack.IsMining && rack.BitcoinAmount >= cap )
			state = "CAP FULL";

		return new LpBitcoinCommandResult( true,
			$"{LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks )} | {state} | {rack.BitcoinAmount:F6}/{cap:F6} BTC ({fill}%)\n" +
			$"rate {rack.MiningRatePerMinute:F6} BTC/min | tick {( rack.MiningProgress * 100f ):F0}%\n" +
			$"hub wallet {hub.HubWalletBtc:F6} BTC | pending {hub.GetRackPendingBtc():F6} BTC" );
	}

	private static LpBitcoinCommandResult InfoSelected( LpBitcoinHubEntity hub, int index )
	{
		var racks = hub.GetLinkedRacks();
		var rack = hub.FindRackByIndex( index );
		if ( rack is null )
			return new LpBitcoinCommandResult( false, "ERR no rack selected — type racks / select <n>" );

		var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
		return new LpBitcoinCommandResult( true,
			$"{LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks )}\n" +
			// "cores x1 (locked)" is CRT lore, kept by ruling R2 — fiction surface, not stats surface.
			$"CPU {rack.ClockGhz:F2} GHz | cores x{rack.CoreCount} (locked) | COMPUTE {LpBitcoinIdent.RomanTier( rack.ComputeTier )} (×{LpBitcoinComputeTrack.EffectMultiplierFor( rack.ComputeTier )})\n" +
			$"{rack.ClockGhz:F2} GHz · {rack.CoreCount} core(s) | cap {cap:F6} BTC | ${rack.UsdValue} rack value" );
	}

	private static LpBitcoinCommandResult WalletSummary( LpBitcoinHubEntity hub )
	{
		var pending = hub.GetRackPendingBtc();
		var wallet = hub.HubWalletBtc;
		return new LpBitcoinCommandResult( true,
			$"hub wallet {wallet:F6} BTC (${LpBitcoinEconomy.BtcToCashUsd( wallet ):N0})\n" +
			$"on racks {pending:F6} BTC (${LpBitcoinEconomy.BtcToCashUsd( pending ):N0})\n" +
			"deposit rack BTC here — send to other hubs: send <steamid> <amount>\n" +
			"cash out to bank from hub admin wallet tab" );
	}

	private static LpBitcoinCommandResult ExecuteSend( LpBitcoinHubEntity hub, string[] parts )
	{
		if ( parts.Length < 3 )
			return new LpBitcoinCommandResult( false, "usage: send <steamid> <amount|all>" );

		if ( !long.TryParse( parts[1], out var steamId ) || steamId <= 0 )
			return new LpBitcoinCommandResult( false, "ERR invalid steam id — use recipient hub owner's numeric Steam ID" );

		if ( !TryParseSendAmount( hub, parts[2], out var amount, out var amountError ) )
			return new LpBitcoinCommandResult( false, amountError );

		if ( hub.Owner != 0 && hub.Owner == steamId )
			return new LpBitcoinCommandResult( false, "ERR cannot send to your own hub wallet" );

		if ( !hub.HasLinkedTerminal() )
			return new LpBitcoinCommandResult( false, "ERR no bitcoin terminal linked to hub" );

		var target = LpBitcoinHubEntity.FindHubByOwnerSteamId( steamId, exclude: hub );
		if ( target is null || !target.IsValid() )
			return new LpBitcoinCommandResult( false, $"ERR no hub found for steam id {steamId}" );

		hub.RequestSendHubWallet( steamId, amount );
		var label = LifePunchEntityOwnership.GetOwnerLabel( steamId );
		if ( string.IsNullOrWhiteSpace( label ) )
			label = steamId.ToString();

		return new LpBitcoinCommandResult( true, $"sent {amount:F6} BTC to hub {label} ({steamId})" );
	}

	private static bool TryParseSendAmount(
		LpBitcoinHubEntity hub,
		string token,
		out float amount,
		out string error )
	{
		amount = 0f;
		error = string.Empty;

		if ( token.Equals( "all", StringComparison.OrdinalIgnoreCase ) )
		{
			amount = hub.HubWalletBtc;
			if ( amount <= 0f )
			{
				error = "ERR hub wallet empty";
				return false;
			}

			return true;
		}

		if ( !float.TryParse( token, out amount ) || amount <= 0f )
		{
			error = "ERR invalid amount — use a positive BTC value or all";
			return false;
		}

		if ( amount > hub.HubWalletBtc + 0.000001f )
		{
			error = $"ERR insufficient hub wallet ({hub.HubWalletBtc:F6} BTC available)";
			return false;
		}

		return true;
	}

	private static LpBitcoinCommandResult MiningStart( LpBitcoinHubEntity hub, int index )
	{
		var racks = hub.GetLinkedRacks();
		var rack = hub.FindRackByIndex( index );
		if ( rack is null )
			return new LpBitcoinCommandResult( false, "ERR no rack selected — type select <n>" );

		var slotId = LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks );
		var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
		if ( rack.BitcoinAmount >= cap )
			return new LpBitcoinCommandResult( false, $"ERR {slotId} at capacity — deposit before mining" );

		if ( rack.IsMining )
			return new LpBitcoinCommandResult( true, $"{slotId} already mining" );

		rack.RequestSetMining( true );
		return new LpBitcoinCommandResult( true, $"mining started on {slotId}" );
	}

	private static LpBitcoinCommandResult MiningStop( LpBitcoinHubEntity hub, int index )
	{
		var racks = hub.GetLinkedRacks();
		var rack = hub.FindRackByIndex( index );
		if ( rack is null )
			return new LpBitcoinCommandResult( false, "ERR no rack selected — type select <n>" );

		var slotId = LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks );
		if ( !rack.IsMining )
			return new LpBitcoinCommandResult( true, $"{slotId} already idle" );

		rack.RequestSetMining( false );
		return new LpBitcoinCommandResult( true, $"mining stopped on {slotId}" );
	}

	private static LpBitcoinCommandResult MiningAll( LpBitcoinHubEntity hub, bool on )
	{
		var racks = hub.GetLinkedRacks();
		if ( racks.Count == 0 )
			return new LpBitcoinCommandResult( false, "ERR no linked racks" );

		var started = 0;
		var skipped = 0;
		foreach ( var rack in racks )
		{
			if ( !rack.IsValid() )
				continue;

			if ( on )
			{
				var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
				if ( rack.BitcoinAmount >= cap || rack.IsMining )
				{
					skipped++;
					continue;
				}

				rack.RequestSetMining( true );
				started++;
				continue;
			}

			if ( rack.IsMining )
			{
				rack.RequestSetMining( false );
				started++;
			}
			else
			{
				skipped++;
			}
		}

		if ( on )
		{
			return started == 0
				? new LpBitcoinCommandResult( false, "ERR no racks started — all at capacity or already mining" )
				: new LpBitcoinCommandResult( true, $"mining started on {started} rack(s)" + ( skipped > 0 ? $" ({skipped} skipped)" : "" ) );
		}

		return started == 0
			? new LpBitcoinCommandResult( true, "all racks already idle" )
			: new LpBitcoinCommandResult( true, $"mining stopped on {started} rack(s)" );
	}

	private static LpBitcoinCommandResult DepositRack( LpBitcoinHubEntity hub, int index )
	{
		if ( !hub.HasLinkedTerminal() )
			return new LpBitcoinCommandResult( false, "ERR no bitcoin terminal linked to hub" );

		var rack = hub.FindRackByIndex( index );
		if ( rack is null )
			return new LpBitcoinCommandResult( false, "ERR no rack selected — type select <n>" );

		if ( rack.BitcoinAmount <= 0f )
			return new LpBitcoinCommandResult( false, "ERR rack balance empty" );

		var amount = rack.BitcoinAmount;
		hub.RequestDepositRack( index );
		var racks = hub.GetLinkedRacks();
		var slotId = rack.IsValid() ? LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks ) : $"rack #{LpBitcoinIdent.DisplayRackNumber( index )}";
		return new LpBitcoinCommandResult( true, $"deposited {amount:F6} BTC from {slotId} to hub wallet" );
	}

	private static LpBitcoinCommandResult DepositAll( LpBitcoinHubEntity hub )
	{
		if ( !hub.HasLinkedTerminal() )
			return new LpBitcoinCommandResult( false, "ERR no bitcoin terminal linked to hub" );

		var pending = hub.GetRackPendingBtc();
		if ( pending <= 0f )
			return new LpBitcoinCommandResult( false, "ERR no rack balances to deposit" );

		hub.RequestDepositRacksToHub();
		return new LpBitcoinCommandResult( true, $"deposited {pending:F6} BTC from all racks to hub wallet" );
	}

	private static string FormatRackLine( LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> racks )
	{
		var cap = LpBitcoinEconomy.RackBtcCapacityFor( rack );
		var state = rack.IsMining ? "MINING" : "IDLE";
		if ( rack.BitcoinAmount >= cap )
			state = "FULL";

		return $"{LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks )} | {rack.BitcoinAmount:F6}/{cap:F6} BTC | {state}";
	}

	// Command list is GRAMMAR, never instances (reference-not-shortcuts law): verbs take
	// <rackId>, players fetch rackIds from the hub Servers page (COPY) and TYPE them here —
	// deliberate retro friction. Numeric index parses as a silent legacy alias, undocumented.
	private static string HelpText() =>
		"── HASHD rig0 commands (space-separated) ──\n" +
		"help · clear (or header CLEAR) · link <rackId> · unlink <rackId> · racks · select <rackId> · status · info · wallet\n" +
		"mining start|stop · mining start all|stop all · mining all-start|all-stop\n" +
		"deposit · deposit all · deposit <rackId>\n" +
		"send <steamid> <amount|all> — transfer hub wallet BTC to another operator's hub\n" +
		"rackIds: copy from the hub Servers page — cash out to bank at hub admin wallet tab (not on this CRT)";
}
