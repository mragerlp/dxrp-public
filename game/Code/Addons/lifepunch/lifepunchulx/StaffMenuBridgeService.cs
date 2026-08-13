// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.ulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using System;
using System.Linq;
using System.Threading.Tasks;
using Dxura.RP.Game;
using Dxura.RP.Game.Addons;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Single host bridge for lifepunchulx client-only reads/writes that need the server token store.
/// On-demand RPC round-trips only — no <c>[Sync]</c> state. Replaces separate waypoint + settings services.
/// </summary>
[AddonService]
public sealed class StaffMenuBridgeService : SingletonComponent<StaffMenuBridgeService>
{
	private const string WaypointStorePrefix = "commands:waypoint:";
	private const string WebsiteStoreKey = "lifepunchulx:settings:website";
	private const string SettingsEditPermission = "lifepunchulx.settings.edit";

	[Rpc.Host]
	public void RequestWaypointsHost()
	{
		var caller = Rpc.Caller;
		if ( !RankSystem.HasPermission( caller.SteamId, Permission.CommandWaypointUse ) )
		{
			return;
		}

		_ = SendWaypointsToCaller( caller );
	}

	/// <summary>
	/// Execute a staff currency grant. HOST-AUTHORITATIVE by construction: the client sends a
	/// request and nothing more. It does not decide whether the grant is allowed, and a modified
	/// client sending a forged request is rejected on the permission check alone.
	///
	/// Every guard here is a hard return, not a clamp: a grant that fails validation does not
	/// execute in a reduced form. Amount is <c>uint</c>, so a deduction is not merely disallowed
	/// but unrepresentable.
	///
	/// The grant runs through <c>Player.PayHost</c> -- DXRP's own economy path, the one paydays and
	/// purchases use -- so MoneyEnabled, the overflow guard, the transaction lock and the audit
	/// write all still apply. PayHost audits both destinations: ModifyPlayerBalance for bank,
	/// Audit("WalletDeposit") for cash. Nothing here writes a balance field directly.
	/// </summary>
	[Rpc.Host]
	public void GiveMoneyHost( long targetSteamId, uint amount, bool inBank, string reason )
	{
		var caller = Rpc.Caller;

		if ( !RankSystem.HasPermission( caller.SteamId, Permission.ManageEconomy ) )
		{
			return;
		}

		if ( amount == 0 )
		{
			return;
		}

		// No blank-reason grants, ever -- the reason is the audit entry's only human content.
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			return;
		}

		var target = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == targetSteamId );
		if ( !target.IsValid() )
		{
			return;
		}

		// The issuer is recorded in the reason string because that is what reaches the portal
		// audit; without it the entry would say what happened but not who did it.
		var audited = $"Staff grant by {caller.DisplayName} ({caller.SteamId}): {reason.Trim()}";
		_ = target.PayHost( amount, audited, inBank );
	}

	[Rpc.Host]
	public void RequestSettingsHost()
	{
		_ = SendSettingsToCaller( Rpc.Caller );
	}

	[Rpc.Host]
	public void SetWebsiteHost( string url )
	{
		var caller = Rpc.Caller;
		if ( !RankSystem.HasPermission( caller.SteamId, SettingsEditPermission ) )
		{
			return;
		}

		_ = SaveWebsite( url );
	}

	private async Task SendWaypointsToCaller( Connection connection )
	{
		var entries = await ServerApiClient.ListStore( WaypointStorePrefix );

		var names = entries
			.Select( entry => entry.Key.StartsWith( WaypointStorePrefix, StringComparison.OrdinalIgnoreCase )
				? entry.Key[WaypointStorePrefix.Length..]
				: null )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Select( name => name! )
			.OrderBy( name => name, StringComparer.OrdinalIgnoreCase )
			.ToArray();

		await GameTask.MainThread();

		using ( Rpc.FilterInclude( c => c == connection ) )
		{
			ReceiveWaypointsClient( names );
		}
	}

	private async Task SendSettingsToCaller( Connection connection )
	{
		var website = string.Empty;
		try
		{
			website = await ServerApiClient.GetStore( WebsiteStoreKey ) ?? string.Empty;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] website read failed (offline?): {e.Message}" );
		}

		await GameTask.MainThread();

		using ( Rpc.FilterInclude( c => c == connection ) )
		{
			ReceiveSettingsClient( website );
		}
	}

	private async Task SaveWebsite( string url )
	{
		url = ( url ?? string.Empty ).Trim();

		try
		{
			if ( url.Length == 0 )
			{
				await ServerApiClient.DeleteStore( WebsiteStoreKey );
			}
			else
			{
				await ServerApiClient.SetStore( WebsiteStoreKey, url );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] website persist failed (offline?): {e.Message}" );
		}

		await GameTask.MainThread();
		ReceiveSettingsClient( url );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveWaypointsClient( string[] names )
	{
		StaffMenuHost.OnWaypointsReceived( names );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveSettingsClient( string website )
	{
		StaffMenuHost.OnSettingsReceived( website );
	}
}
#endif
