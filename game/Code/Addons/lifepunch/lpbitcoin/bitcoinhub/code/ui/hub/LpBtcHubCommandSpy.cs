// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// BH-SHOWCASE-1: the hub UI's command spy. Every inert control reports here, and every host request
// the hub UI makes passes through Send. With a showcase attached Send refuses and counts, so the
// showcase walkthrough can show zero mutation calls; on the live hub path Send calls straight
// through, unchanged. Counters are local and reset on hotload. UNVERIFIED in the editor until the
// driving seat proves it.
// -----------------------------------------------------------------------------

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin.HubUi;

public static class LpBtcHubCommandSpy
{
	/// <summary>Inert controls pressed since the last reset (nothing was sent for any of them).</summary>
	public static int BlockedCount { get; private set; }

	/// <summary>Host requests attempted while a showcase was attached; each one was refused.</summary>
	public static int ShowcaseMutationAttempts { get; private set; }

	/// <summary>
	/// Why an inert control does nothing, in player terms, and where the player can do it today
	/// (terminal verbs: bitcoinmining/LpBitcoinTerminalCommands.cs:37-144, where link/unlink take a rack id
	/// and mining needs start|stop; terminal link switch: lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor:906;
	/// Player Hub BTC tab callbacks: playerhub/code/data/LpPlayerHubBtcSource.cs:102-143). One table, so a tooltip and the note a
	/// press shows always read the same. if/else, not a switch expression (razor_lint, LpBtcHubLogs.razor:51).
	/// </summary>
	public static string Reason( string action )
	{
		if ( action == "power" ) return "Not in this view yet. Switch hub power from the BTC tab of your Player Hub.";
		if ( action == "link" ) return "Not in this view yet. Stand near a HASHD terminal and use the Bitcoin terminal switch in Settings on the classic hub screen (hub must be on).";
		if ( action == "unlink" ) return "Not in this view yet. Use the Bitcoin terminal switch in Settings on the classic hub screen.";
		if ( action == "mining" ) return "Not in this view yet. Type mining start all (or mining stop all) at the linked HASHD terminal.";
		if ( action == "upgrade" ) return "Upgrades can't be bought in this view yet.";
		if ( action == "cashout" ) return "Not in this view yet. The BTC tab of your Player Hub can cash out the whole hub wallet.";
		if ( action == "cashoutall" ) return "Not in this view yet. Use Cash out all on the BTC tab of your Player Hub.";
		if ( action == "deposit" ) return "Not in this view yet. Type deposit all at the linked HASHD terminal.";
		if ( action == "deposit-nolink" ) return "Link a HASHD terminal first, then type deposit all at it.";
		if ( action == "pin" ) return "Changing the PIN isn't in this view yet.";
		if ( action == "clearalerts" ) return "Clearing alerts isn't in this view yet.";
		return "Not available in this view yet.";
	}

	public static void Reset()
	{
		BlockedCount = 0;
		ShowcaseMutationAttempts = 0;
	}

	/// <summary>An inert control was pressed. Nothing is sent; the panel shows the reason.</summary>
	public static void Blocked( string action )
	{
		BlockedCount++;
		Log.Info( "[btcui2] BH-SHOWCASE-1 spy blocked action=" + action + " blocked=" + BlockedCount );
	}

	/// <summary>
	/// The one door from the hub UI to the host. Showcase attached: refused, counted, logged, nothing
	/// sent, returns false. Otherwise the request runs as it did before the spy existed.
	/// </summary>
	public static bool Send( bool showcase, string action, Action send )
	{
		if ( showcase )
		{
			ShowcaseMutationAttempts++;
			Log.Warning( "[btcui2] BH-SHOWCASE-1 spy refused mutation action=" + action + " attempts=" + ShowcaseMutationAttempts );
			return false;
		}

		send();
		return true;
	}
}
