// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dxura.RP.Game;
using Dxura.RP.Game.Addons;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Host bridge for ULX reads and writes that need host authority or the server token store.
/// Uses on-demand RPC responses without replicated [Sync] state.
/// </summary>
[AddonService]
public sealed class StaffMenuBridgeService : SingletonComponent<StaffMenuBridgeService>
{
	private const string WaypointStorePrefix = "commands:waypoint:";
	private const string WebsiteStoreKey = "lifepunchulx:settings:website";
	private const string SettingsEditPermission = "lifepunchulx.settings.edit";
	private const float WaypointReadCooldownSeconds = 0.5f;
	private const float SettingsReadCooldownSeconds = 0.5f;
	private const float InventoryReadCooldownSeconds = 0.5f;
	private const float SanctionsReadCooldownSeconds = 0.5f;
	private const float SettingsWriteCooldownSeconds = 0.25f;
	private const int MoneyGrantReplayCapacity = 128;
	private enum MoneyGrantHostState { Succeeded, Rejected, Unknown }
	private readonly Dictionary<long, TimeSince> _settingsReadAge = new();
	private readonly HashSet<long> _settingsReadWorkers = new();
	private readonly Dictionary<long, Guid> _settingsLatestRequestIds = new();
	private readonly Dictionary<long, Connection> _settingsLatestConnections = new();
	private readonly Dictionary<long, long> _settingsReadGenerations = new();
	private readonly Dictionary<long, TimeSince> _waypointReadAge = new();
	private readonly HashSet<long> _waypointReadWorkers = new();
	private readonly Dictionary<long, Guid> _waypointLatestRequestIds = new();
	private readonly Dictionary<long, Connection> _waypointLatestConnections = new();
	private readonly Dictionary<long, long> _waypointReadGenerations = new();
	private readonly Dictionary<long, TimeSince> _settingsWriteAge = new();
	private readonly Dictionary<long, TimeSince> _inventoryReadAge = new();
	private readonly Dictionary<long, TimeSince> _sanctionsReadAge = new();
	private readonly SemaphoreSlim _websiteWriteGate = new( 1, 1 );
	private readonly HashSet<string> _moneyGrantInFlight = new( StringComparer.Ordinal );
	// Unknown outcomes retain this host-lifetime lock; a new request ID is not a safe retry.
	private readonly Dictionary<long, Guid> _moneyGrantUnresolvedByCaller = new();
	private readonly Dictionary<string, (MoneyGrantHostState State, string Message)> _moneyGrantResults = new( StringComparer.Ordinal );
	private readonly Queue<string> _moneyGrantResultOrder = new();

	/// <summary>Compare the confirmed state on the host, then run the existing native command without yielding.</summary>
	[Rpc.Host]
	public void SetStatusToggleHost( string actionKey, long targetSteamId, bool expectedState, bool desiredState )
	{
		var connection = Rpc.Caller;
		if ( !Networking.IsHost || connection is null || ( !connection.IsActive && !connection.IsHost ) )
			return;
		var caller = GameUtils.GetPlayerByConnectionId( connection.Id );
		if ( !caller.IsValid() || caller.Connection is null || caller.Connection.Id != connection.Id )
			return;

		var chat = Chat.Current;
		if ( expectedState == desiredState || string.IsNullOrWhiteSpace( actionKey ) || chat is null
			|| !chat.TryGetCommand( actionKey, out var command ) || command is null )
		{
			caller.SendMessage( "Toggle unavailable: the request or native command is invalid. No change was made." );
			return;
		}

		var statusId = actionKey switch
		{
			"freeze" when command is Dxura.RP.Game.Commands.FreezeCommand => Constants.FreezeStatus,
			"god" when command is Dxura.RP.Game.Commands.GodCommand => Constants.GodStatus,
			"cloak" when command is Dxura.RP.Game.Commands.CloakCommand => Constants.CloakStatus,
			"incognito" when command is Dxura.RP.Game.Commands.IncognitoCommand => Constants.IncognitoStatus,
			_ => null
		};
		if ( statusId is null )
		{
			caller.SendMessage( "Toggle unavailable: this command does not support guarded state changes." );
			return;
		}

		// Reuse the native command's cooldown and access rules, including parent-specific restrictions.
		var cooldown = command.CooldownOverride ?? Config.Current.Game.CommandCooldown;
		if ( cooldown > 0f && ( Cooldown.Current is null
			|| Cooldown.Current.CheckAndStartCooldown( $"{connection.Id}:command", cooldown ) ) )
			return;
		if ( !chat.CanAccessCommand( caller, command ) )
		{
			caller.SendMessage( "Toggle rejected: your permissions or current player state do not allow this command." );
			return;
		}

		var target = actionKey == "freeze" ? GameUtils.GetPlayerById( targetSteamId ) : caller;
		if ( !target.IsValid() || ( actionKey != "freeze" && targetSteamId != caller.SteamId ) )
		{
			caller.SendMessage( "Toggle rejected: the target is unavailable or this command is self-only." );
			return;
		}
		if ( actionKey == "freeze" && !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			caller.SendMessage( "#command.errors.higher_rank" );
			return;
		}

		if ( target.HasStatus( statusId ) != expectedState )
		{
			caller.SendMessage( "Toggle state changed before the request reached the host. No change was made; check the current state and confirm again." );
			return;
		}

		var nativeArgs = actionKey == "freeze"
			? new[] { targetSteamId.ToString( System.Globalization.CultureInfo.InvariantCulture ) }
			: Array.Empty<string>();
		var raw = $"/{actionKey} {string.Join( ' ', nativeArgs )}".TrimEnd();
		command.ExecuteHost( caller, nativeArgs, raw );
	}

	[Rpc.Host]
	public void RequestWaypointsHost( Guid requestId )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty || !RankSystem.HasPermission( caller.SteamId, Permission.CommandWaypointUse ) )
		{
			return;
		}

		_waypointLatestRequestIds[caller.SteamId] = requestId;
		_waypointLatestConnections[caller.SteamId] = caller;
		_waypointReadGenerations[caller.SteamId] =
			_waypointReadGenerations.TryGetValue( caller.SteamId, out var generation ) ? generation + 1 : 1;
		if ( _waypointReadWorkers.Add( caller.SteamId ) )
		{
			_ = SendWaypointsToCaller( caller.SteamId );
		}
	}

	/// <summary>
	/// Validate and execute a staff currency grant on the host. Invalid grants are rejected,
	/// not clamped; the amount must fit the signed API boundary despite the uint request field.
	/// Payment uses <c>Player.PayHostStrictAudited</c>, preserving native economy guards and audit handling.
	/// No balance field is written directly. An unknown outcome retains the caller's host-lifetime lock.
	/// </summary>
	[Rpc.Host]
	public void GiveMoneyHost( Guid requestId, long targetSteamId, uint amount, bool inBank, string reason )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty )
		{
			return;
		}

		var replayKey = MoneyGrantReplayKey( caller, requestId );
		if ( _moneyGrantResults.TryGetValue( replayKey, out var prior ) )
		{
			SendCachedMoneyGrantResult( caller, requestId, prior );
			return;
		}

		if ( _moneyGrantInFlight.Contains( replayKey ) )
		{
			return;
		}

		// An evicted receipt is still unresolved; validation/replay must not invent a terminal result.
		if ( _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
		     && unresolvedRequestId == requestId )
		{
			SendMoneyGrantReconcileState( caller, requestId, false,
				"The original grant remains unresolved. Do not retry; verify the balance and audit with the server operator." );
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( caller.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				"Grant rejected: synthetic test actors cannot perform durable economy actions." );
			return;
		}

		if ( !RankSystem.HasPermission( caller.SteamId, Permission.ManageEconomy ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: permission denied." );
			return;
		}

		// The published parent does not expose authorization state to addons, so its package build
		// cannot prove the audit rail is linked. Fail closed until a compatible parent is published.
#if LIFEPUNCH_PACKAGE
		CompleteMoneyGrant( caller, requestId, replayKey, false,
			"Grant unavailable: publish a compatible DXRP parent with server-link verification first." );
		return;
#else
		if ( _moneyGrantUnresolvedByCaller.ContainsKey( caller.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				"Grant rejected: your original money grant is unresolved. Recheck that request; a new grant is blocked." );
			return;
		}

		// The confirmation promises an audited staff action. Without the server credential the
		// bank path reports a neutral success and a wallet audit stays local-only, so neither can
		// truthfully satisfy that promise. Refuse the action until the host is linked.
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: authorize the server API key first." );
			return;
		}

		if ( !Config.Current.Game.MoneyEnabled )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: this server has money disabled." );
			return;
		}

		if ( amount == 0 || amount > int.MaxValue )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, $"Grant rejected: amount must be between 1 and {int.MaxValue}." );
			return;
		}

		// Require a nonblank reason for the audit entry.
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: an audit reason is required." );
			return;
		}

		var target = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == targetSteamId );
		if ( !target.IsValid() )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: the target is no longer available." );
			return;
		}

		if ( !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: the target is protected by rank hierarchy." );
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( targetSteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: synthetic test players cannot receive durable money." );
			return;
		}

		// Include the issuer in the reason because that text reaches the portal audit.
		var audited = $"Staff grant by {caller.DisplayName} ({caller.SteamId}): {reason.Trim()}";
		_moneyGrantUnresolvedByCaller[caller.SteamId] = requestId;
		_moneyGrantInFlight.Add( replayKey );
		_ = ExecuteGiveMoneyAsync( caller, requestId, replayKey, target, amount, inBank, audited, caller.SteamId );
#endif
	}

#if !LIFEPUNCH_PACKAGE
	private async Task ExecuteGiveMoneyAsync(
		Connection caller,
		Guid requestId,
		string replayKey,
		Player target,
		uint amount,
		bool inBank,
		string auditedReason,
		long auditActorSteamId )
	{
		var result = StrictMoneyMutationResult.Unknown;
		try
		{
			result = await target.PayHostStrictAudited( amount, auditedReason, inBank, auditActorSteamId );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] money grant failed: {e.Message}" );
		}

		await GameTask.MainThread();
		_moneyGrantInFlight.Remove( replayKey );
		if ( result == StrictMoneyMutationResult.Unknown )
		{
			CompleteMoneyGrantUnknown( caller, requestId, replayKey,
				"The economy response was lost or indeterminate. Do not retry; verify the portal balance and audit first." );
			return;
		}

		// Only the terminal result for the original request releases this caller's lock.
		if ( _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
		     && unresolvedRequestId == requestId )
		{
			_moneyGrantUnresolvedByCaller.Remove( caller.SteamId );
		}

		var succeeded = result == StrictMoneyMutationResult.Applied;
		CompleteMoneyGrant( caller, requestId, replayKey, succeeded,
			succeeded
				? ( inBank ? "Bank grant confirmed by the economy service." : "Wallet grant applied; check the portal audit separately." )
				: "Grant was rejected before any confirmed balance change." );
	}
#endif

	private static string MoneyGrantReplayKey( Connection caller, Guid requestId )
		=> $"{caller.SteamId}:{requestId:N}";

	private void CompleteMoneyGrant(
		Connection caller,
		Guid requestId,
		string replayKey,
		bool succeeded,
		string message )
	{
		if ( !_moneyGrantResults.ContainsKey( replayKey ) )
		{
			_moneyGrantResults[replayKey] = (succeeded ? MoneyGrantHostState.Succeeded : MoneyGrantHostState.Rejected, message);
			_moneyGrantResultOrder.Enqueue( replayKey );
			while ( _moneyGrantResultOrder.Count > MoneyGrantReplayCapacity )
			{
				_moneyGrantResults.Remove( _moneyGrantResultOrder.Dequeue() );
			}
		}

		SendMoneyGrantResult( caller, requestId, succeeded, message );
	}

	private void CompleteMoneyGrantUnknown( Connection caller, Guid requestId, string replayKey, string message )
	{
		if ( !_moneyGrantResults.ContainsKey( replayKey ) )
		{
			_moneyGrantResults[replayKey] = (MoneyGrantHostState.Unknown, message);
			_moneyGrantResultOrder.Enqueue( replayKey );
			while ( _moneyGrantResultOrder.Count > MoneyGrantReplayCapacity )
			{
				_moneyGrantResults.Remove( _moneyGrantResultOrder.Dequeue() );
			}
		}

		SendMoneyGrantReconcileState( caller, requestId, false, message );
	}

	private void SendCachedMoneyGrantResult(
		Connection caller,
		Guid requestId,
		(MoneyGrantHostState State, string Message) result )
	{
		if ( result.State == MoneyGrantHostState.Unknown )
		{
			SendMoneyGrantReconcileState( caller, requestId, false, result.Message );
			return;
		}

		SendMoneyGrantResult( caller, requestId, result.State == MoneyGrantHostState.Succeeded, result.Message );
	}

	private void SendMoneyGrantResult( Connection caller, Guid requestId, bool succeeded, string message )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			ReceiveMoneyGrantResultClient( requestId, succeeded, message );
		}
	}

	/// <summary>
	/// Query-only reconciliation for a delayed result. A cache miss never executes a payment.
	/// </summary>
	[Rpc.Host]
	public void QueryMoneyGrantResultHost( Guid requestId )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty )
		{
			return;
		}

		var replayKey = MoneyGrantReplayKey( caller, requestId );
		if ( _moneyGrantResults.TryGetValue( replayKey, out var prior ) )
		{
			SendCachedMoneyGrantResult( caller, requestId, prior );
			return;
		}

		var inFlight = _moneyGrantInFlight.Contains( replayKey );
		var remainsUnresolved = _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
			&& unresolvedRequestId == requestId;
		SendMoneyGrantReconcileState(
			caller,
			requestId,
			inFlight,
			inFlight
				? "The original grant is still processing on the host."
				: remainsUnresolved
					? "The original grant remains unresolved. New grants are blocked; verify the balance and audit with the server operator."
					: "The host has no result record for this request. Do not retry; verify the balance and audit with the server operator." );
	}

	private void SendMoneyGrantReconcileState( Connection caller, Guid requestId, bool inFlight, string message )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			ReceiveMoneyGrantReconcileStateClient( requestId, inFlight, message );
		}
	}

	[Rpc.Host]
	public void RequestSettingsHost( Guid requestId )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty )
		{
			return;
		}

		// All players may read the public network settings, but one client must not be able to
		// turn a forged RPC flood into unbounded authenticated store requests. Keep only the latest
		// correlation id for that Steam identity and let its single worker answer it.
		_settingsLatestRequestIds[caller.SteamId] = requestId;
		_settingsLatestConnections[caller.SteamId] = caller;
		_settingsReadGenerations[caller.SteamId] =
			_settingsReadGenerations.TryGetValue( caller.SteamId, out var generation ) ? generation + 1 : 1;
		if ( _settingsReadWorkers.Add( caller.SteamId ) )
		{
			_ = SendSettingsToCaller( caller.SteamId );
		}
	}

	[Rpc.Host]
	public void SetWebsiteHost( Guid requestId, string url )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty )
		{
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( caller.SteamId ) )
		{
			SendWebsiteWriteResult( caller, requestId, false,
				"Not saved: synthetic test actors cannot change durable server settings." );
			return;
		}

		if ( !RankSystem.HasPermission( caller.SteamId, SettingsEditPermission ) )
		{
			SendWebsiteWriteResult( caller, requestId, false, "Not saved: the host rejected this settings permission." );
			return;
		}

		if ( _settingsWriteAge.TryGetValue( caller.SteamId, out var since ) && since < SettingsWriteCooldownSeconds )
		{
			SendWebsiteWriteResult( caller, requestId, false, "Not saved: wait briefly before another settings change." );
			return;
		}

		_settingsWriteAge[caller.SteamId] = 0;
		_ = SaveWebsite( caller, requestId, url );
	}

	/// <summary>
	/// Read one selected player's durable Portal inventory. The host re-resolves the caller and target,
	/// enforces the dedicated permission and rank hierarchy, rejects synthetic identities, and returns
	/// only display-safe fields to the requesting connection. No inventory mutation API is reachable here.
	/// </summary>
	[Rpc.Host]
	public async void RequestInventoryHost( long targetSteamId, Guid requestId )
	{
		var callerConnection = Rpc.Caller;
		if ( requestId == Guid.Empty || targetSteamId == 0 )
		{
			return;
		}

		if ( _inventoryReadAge.TryGetValue( callerConnection.SteamId, out var since )
		     && since < InventoryReadCooldownSeconds )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: wait briefly before retrying." );
			return;
		}

		_inventoryReadAge[callerConnection.SteamId] = 0;
		if ( StaffMenuHost.IsSyntheticPlayer( callerConnection.SteamId )
		     || !RankSystem.HasPermission( callerConnection.SteamId, Permission.ViewInventory ) )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: permission denied." );
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerConnection.Id );
		var target = GameUtils.GetPlayerById( targetSteamId );
		if ( !caller.IsValid() || caller.Connection == null || !target.IsValid() )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: the selected player is no longer online." );
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( targetSteamId ) )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: synthetic test players have no durable account inventory." );
			return;
		}

		if ( !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: the target is protected by rank hierarchy." );
			return;
		}

#if !LIFEPUNCH_PACKAGE
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Server API authorization required. Authorize the key in the host console, then retry." );
			return;
		}
#endif

		List<InventoryItemDto>? inventory = null;
		try
		{
			inventory = await ServerApiClient.GetPlayerInventory( targetSteamId );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] inventory read failed: {e.Message}" );
		}

		await GameTask.MainThread();
		var connectionIsCurrent = (callerConnection.IsActive || callerConnection.IsHost)
			&& Connection.All.Any( current => current.Id == callerConnection.Id
				&& current.SteamId == callerConnection.SteamId );
		if ( !connectionIsCurrent )
		{
			return;
		}

		caller = GameUtils.GetPlayerByConnectionId( callerConnection.Id );
		target = GameUtils.GetPlayerById( targetSteamId );
		if ( !caller.IsValid() || caller.Connection == null || !target.IsValid()
		     || StaffMenuHost.IsSyntheticPlayer( caller.SteamId )
		     || StaffMenuHost.IsSyntheticPlayer( targetSteamId )
		     || !RankSystem.HasPermission( caller.SteamId, Permission.ViewInventory )
		     || !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
				"Inventory unavailable: permission, target, or session state changed during the read." );
			return;
		}

		if ( inventory is null )
		{
			SendInventoryResult( callerConnection, requestId, targetSteamId, false,
#if LIFEPUNCH_PACKAGE
				"Inventory unavailable: server API authorization or the inventory service could not be confirmed." );
#else
				"Inventory unavailable: the server API did not return a confirmed result." );
#endif
			return;
		}

		var rows = inventory
			.Where( item => item.Definition is not null
				&& !string.IsNullOrWhiteSpace( item.Definition.Name ) && item.Quantity > 0 )
			.OrderBy( item => item.Definition.Type.ToString(), StringComparer.OrdinalIgnoreCase )
			.ThenBy( item => item.Definition.Name, StringComparer.OrdinalIgnoreCase )
			.ToArray();
		SendInventoryResult(
			callerConnection,
			requestId,
			targetSteamId,
			true,
			string.Empty,
			rows.Select( item => item.Definition.Name ).ToArray(),
			rows.Select( item => item.Quantity ).ToArray(),
			rows.Select( item => item.Definition.Type.ToString() ).ToArray(),
			rows.Select( item => item.Definition.Rarity.ToString() ).ToArray() );
	}

	private void SendInventoryResult(
		Connection caller,
		Guid requestId,
		long targetSteamId,
		bool succeeded,
		string message,
		string[]? names = null,
		int[]? quantities = null,
		string[]? types = null,
		string[]? rarities = null )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id && c.SteamId == caller.SteamId ) )
		{
			ReceiveInventoryClient(
				requestId,
				targetSteamId,
				names ?? Array.Empty<string>(),
				quantities ?? Array.Empty<int>(),
				types ?? Array.Empty<string>(),
				rarities ?? Array.Empty<string>(),
				succeeded,
				message ?? string.Empty );
		}
	}

	/// <summary>
	/// Package-safe, read-only sanctions history bridge. It mirrors the parent history system's
	/// authorization and privacy rules but returns only display fields to the requesting connection.
	/// Limited viewers are always labelled as receiving a permission-limited subset; the response
	/// never reveals whether this particular subject has hidden privileged rows.
	/// </summary>
	[Rpc.Host]
	public async void RequestSanctionsHost( long targetSteamId, Guid requestId )
	{
		var callerConnection = Rpc.Caller;
		if ( requestId == Guid.Empty || targetSteamId == 0 )
		{
			return;
		}

		if ( _sanctionsReadAge.TryGetValue( callerConnection.SteamId, out var since )
		     && since < SanctionsReadCooldownSeconds )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Failed );
			return;
		}

		_sanctionsReadAge[callerConnection.SteamId] = 0;
		if ( StaffMenuHost.IsSyntheticPlayer( callerConnection.SteamId )
		     || StaffMenuHost.IsSyntheticPlayer( targetSteamId )
		     || !CanReadSanctions( callerConnection.SteamId, targetSteamId ) )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Refused );
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerConnection.Id );
		if ( !caller.IsValid() || caller.Connection == null )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Refused );
			return;
		}

#if !LIFEPUNCH_PACKAGE
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Failed );
			return;
		}
#endif

		List<PlayerSanctionHistoryDto>? sanctions = null;
		try
		{
			sanctions = await ServerApiClient.GetPlayerSanctions( targetSteamId );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] sanctions read failed: {e.Message}" );
		}

		await GameTask.MainThread();
		caller = GameUtils.GetPlayerByConnectionId( callerConnection.Id );
		var connectionIsCurrent = (callerConnection.IsActive || callerConnection.IsHost)
			&& Connection.All.Any( current => ReferenceEquals( current, callerConnection ) );
		if ( !connectionIsCurrent || !caller.IsValid() || caller.Connection == null
		     || StaffMenuHost.IsSyntheticPlayer( caller.SteamId )
		     || StaffMenuHost.IsSyntheticPlayer( targetSteamId )
		     || !CanReadSanctions( caller.SteamId, targetSteamId ) )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Refused );
			return;
		}

		if ( sanctions is null )
		{
			SendSanctionsResult( callerConnection, requestId, targetSteamId, SanctionsReadState.Failed );
			return;
		}

		var canViewNotes = RankSystem.HasPermission( caller.SteamId, Permission.ViewSanctionNotes );
		var rows = sanctions
			.Where( sanction => canViewNotes || (sanction.Flags & SanctionFlags.Privileged) == 0 )
			.OrderByDescending( sanction => sanction.Created )
			.ToArray();
		var outcome = canViewNotes
			? (rows.Length == 0 ? SanctionsReadState.CompletedEmpty : SanctionsReadState.Populated)
			: SanctionsReadState.Filtered;
		SendSanctionsResult(
			callerConnection,
			requestId,
			targetSteamId,
			outcome,
			rows.Select( sanction => sanction.Type.ToString() ).ToArray(),
			rows.Select( sanction => sanction.State.ToString() == "Active" ).ToArray(),
			rows.Select( sanction => sanction.Reason ?? string.Empty ).ToArray(),
			rows.Select( sanction => sanction.Duration is null ? "Permanent" : sanction.Duration.Value.ToString() ).ToArray(),
			rows.Select( sanction => sanction.Created.ToString( "yyyy-MM-dd HH:mm" ) ).ToArray(),
			rows.Select( sanction => sanction.IsGlobal ? "Global" : "Server" ).ToArray(),
			rows.Select( sanction => sanction.State.ToString() ).ToArray(),
			rows.Select( sanction => sanction.Flags.ToString() ).ToArray(),
			rows.Select( sanction => canViewNotes ? sanction.Notes ?? string.Empty : string.Empty ).ToArray() );
	}

	private static bool CanReadSanctions( long callerSteamId, long targetSteamId )
	{
		return callerSteamId == targetSteamId
			? RankSystem.HasPermission( callerSteamId, Permission.ViewOwnSanctions )
			  || RankSystem.HasPermission( callerSteamId, Permission.ViewOtherSanctions )
			: RankSystem.HasPermission( callerSteamId, Permission.ViewOtherSanctions );
	}

	private void SendSanctionsResult(
		Connection caller,
		Guid requestId,
		long targetSteamId,
		SanctionsReadState outcome,
		string[]? types = null,
		bool[]? active = null,
		string[]? reasons = null,
		string[]? durations = null,
		string[]? created = null,
		string[]? scopes = null,
		string[]? states = null,
		string[]? flags = null,
		string[]? notes = null )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id && c.SteamId == caller.SteamId ) )
		{
			ReceiveSanctionsClient(
				requestId,
				targetSteamId,
				(int)outcome,
				types ?? Array.Empty<string>(),
				active ?? Array.Empty<bool>(),
				reasons ?? Array.Empty<string>(),
				durations ?? Array.Empty<string>(),
				created ?? Array.Empty<string>(),
				scopes ?? Array.Empty<string>(),
				states ?? Array.Empty<string>(),
				flags ?? Array.Empty<string>(),
				notes ?? Array.Empty<string>() );
		}
	}

	private async Task SendWaypointsToCaller( long steamId )
	{
		while ( true )
		{
			var readGeneration = _waypointReadGenerations.TryGetValue( steamId, out var generation ) ? generation : 0;
			IReadOnlyList<StoreEntryDto> entries = Array.Empty<StoreEntryDto>();
			var authoritative = false;
			try
			{
				if ( _waypointReadAge.TryGetValue( steamId, out var since ) && since < WaypointReadCooldownSeconds )
				{
					await GameTask.DelayRealtimeSeconds( WaypointReadCooldownSeconds - (float)since );
				}

				if ( StoreReadsMayRun )
				{
#if LIFEPUNCH_PACKAGE
					entries = await ServerApiClient.ListStore( WaypointStorePrefix );
					// The published parent can return its process-local fallback here and exposes no
					// provenance bit. Values remain useful as a preview, but never claim they are authoritative.
					authoritative = false;
#else
					var read = await ServerApiClient.ReadStoreList( WaypointStorePrefix );
					entries = read.Entries;
					authoritative = read.Succeeded;
#endif
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[lifepunchulx] waypoint read failed: {e.Message}" );
			}

			var names = entries
				.Select( entry => entry.Key.StartsWith( WaypointStorePrefix, StringComparison.OrdinalIgnoreCase )
					? entry.Key[WaypointStorePrefix.Length..]
					: null )
				.Where( name => !string.IsNullOrWhiteSpace( name ) )
				.Select( name => name! )
				.OrderBy( name => name, StringComparer.OrdinalIgnoreCase )
				.ToArray();

			await GameTask.MainThread();
			_waypointReadAge[steamId] = 0;
			if ( _waypointReadGenerations.TryGetValue( steamId, out var latestGeneration )
			     && latestGeneration != readGeneration )
			{
				continue;
			}

			_waypointReadWorkers.Remove( steamId );
			_waypointReadGenerations.Remove( steamId );
			if ( !_waypointLatestRequestIds.TryGetValue( steamId, out var requestId )
			     || !_waypointLatestConnections.TryGetValue( steamId, out var connection ) )
			{
				_waypointLatestRequestIds.Remove( steamId );
				_waypointLatestConnections.Remove( steamId );
				return;
			}

			_waypointLatestRequestIds.Remove( steamId );
			_waypointLatestConnections.Remove( steamId );
			var connectionIsCurrent = (connection.IsActive || connection.IsHost)
				&& connection.SteamId == steamId
				&& Connection.All.Any( current => ReferenceEquals( current, connection ) );
			if ( !connectionIsCurrent || !RankSystem.HasPermission( steamId, Permission.CommandWaypointUse ) )
			{
				return;
			}

			using ( Rpc.FilterInclude( c => (c.IsActive || c.IsHost) && c.Id == connection.Id && c.SteamId == steamId ) )
			{
				ReceiveWaypointsClient( requestId, names, authoritative );
			}

			return;
		}
	}

	private async Task SendSettingsToCaller( long steamId )
	{
		while ( true )
		{
			var readGeneration = _settingsReadGenerations.TryGetValue( steamId, out var generation ) ? generation : 0;
			var website = string.Empty;
			var authoritative = false;
			try
			{
				if ( _settingsReadAge.TryGetValue( steamId, out var since ) && since < SettingsReadCooldownSeconds )
				{
					await GameTask.DelayRealtimeSeconds( SettingsReadCooldownSeconds - (float)since );
				}

				if ( StoreReadsMayRun )
				{
#if LIFEPUNCH_PACKAGE
					website = await ServerApiClient.GetStore( WebsiteStoreKey ) ?? string.Empty;
					// The published parent does not expose whether this came from its authenticated
					// store or its process-local fallback, so package reads are always explicitly uncertain.
					authoritative = false;
#else
					var read = await ServerApiClient.ReadStoreValue( WebsiteStoreKey );
					website = read.Found ? read.Value ?? string.Empty : string.Empty;
					authoritative = read.Succeeded;
#endif
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[lifepunchulx] settings read failed: {e.Message}" );
			}

			await GameTask.MainThread();
			_settingsReadAge[steamId] = 0;
			if ( _settingsReadGenerations.TryGetValue( steamId, out var latestGeneration )
			     && latestGeneration != readGeneration )
			{
				continue;
			}

			_settingsReadWorkers.Remove( steamId );
			_settingsReadGenerations.Remove( steamId );
			if ( !_settingsLatestRequestIds.TryGetValue( steamId, out var requestId )
			     || !_settingsLatestConnections.TryGetValue( steamId, out var connection ) )
			{
				return;
			}

			_settingsLatestRequestIds.Remove( steamId );
			_settingsLatestConnections.Remove( steamId );
			using ( Rpc.FilterInclude( c => c.Id == connection.Id ) )
			{
				ReceiveSettingsClient( requestId, website, authoritative );
			}

			return;
		}
	}

	private async Task SaveWebsite( Connection caller, Guid requestId, string url )
	{
		url = ( url ?? string.Empty ).Trim();

#if LIFEPUNCH_PACKAGE
		// The currently published parent exposes neither authorization state nor a durable write result.
		// A package-only set/read round trip can hit the parent's process-local fallback, so it is not
		// evidence that the portal persisted anything. Fail closed until that parent contract exists.
		await Task.CompletedTask;
		Log.Warning( "[lifepunchulx] website changes require a parent with authenticated, confirmed store writes." );
		SendWebsiteWriteResult( caller, requestId, false,
			"Not saved: this server build cannot prove an authenticated durable settings write." );
		return;
#else
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			Log.Warning( "[lifepunchulx] website change rejected: no server API authorization key." );
			SendWebsiteWriteResult( caller, requestId, false, "Not saved: authorize the server API first." );
			return;
		}

		var persisted = false;

		await _websiteWriteGate.WaitAsync();
		try
		{
			// Authorization and grants can change while this request waits behind another write.
			// Revalidate inside the serialized critical section before any API/fallback call.
			await GameTask.MainThread();
			if ( !ServerApiLink.HasAuthorizationKey
			     || StaffMenuHost.IsSyntheticPlayer( caller.SteamId )
			     || !RankSystem.HasPermission( caller.SteamId, SettingsEditPermission ) )
			{
				SendWebsiteWriteResult( caller, requestId, false,
					"Not saved: server authorization or settings permission changed before the write." );
				return;
			}

			if ( url.Length == 0 )
			{
				persisted = await ServerApiClient.TryDeleteStoreStrict( WebsiteStoreKey );
			}
			else
			{
				persisted = await ServerApiClient.TrySetStoreStrict( WebsiteStoreKey, url );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] website persist failed (offline?): {e.Message}" );
			await GameTask.MainThread();
			SendWebsiteWriteResult( caller, requestId, false, "Not saved: the durable settings write failed." );
			return;
		}
		finally
		{
			_websiteWriteGate.Release();
		}

		if ( !persisted )
		{
			await GameTask.MainThread();
			SendWebsiteWriteResult( caller, requestId, false, "Not saved: the server did not confirm the durable settings write." );
			return;
		}

		await GameTask.MainThread();
		ReceiveSettingsClient( Guid.Empty, url, true );
		SendWebsiteWriteResult( caller, requestId, true, "Saved and confirmed by the server API." );
#endif
	}

	private void SendWebsiteWriteResult( Connection caller, Guid requestId, bool succeeded, string message )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			ReceiveWebsiteWriteResultClient( requestId, succeeded, message );
		}
	}

	private static bool StoreReadsMayRun
	{
		get
		{
#if LIFEPUNCH_PACKAGE
			// The parent owns token injection but does not expose its state. Reads are allowed;
			// empty package results remain non-authoritative at their call sites.
			return true;
#else
			return ServerApiLink.HasAuthorizationKey;
#endif
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveWaypointsClient( Guid requestId, string[] names, bool authoritative )
	{
		StaffMenuHost.OnWaypointsReceived( requestId, names, authoritative );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveSettingsClient( Guid requestId, string website, bool authoritative )
	{
		StaffMenuHost.OnSettingsReceived( requestId, website, authoritative );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveWebsiteWriteResultClient( Guid requestId, bool succeeded, string message )
	{
		StaffMenuHost.OnWebsiteWriteResult( requestId, succeeded, message );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveMoneyGrantResultClient( Guid requestId, bool succeeded, string message )
	{
		StaffMenuHost.OnMoneyGrantResult( requestId, succeeded, message );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveMoneyGrantReconcileStateClient( Guid requestId, bool inFlight, string message )
	{
		StaffMenuHost.OnMoneyGrantReconcileState( requestId, inFlight, message );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveInventoryClient(
		Guid requestId,
		long targetSteamId,
		string[] names,
		int[] quantities,
		string[] types,
		string[] rarities,
		bool succeeded,
		string message )
	{
		StaffMenuHost.OnInventoryReceived(
			requestId, targetSteamId, names, quantities, types, rarities, succeeded, message );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveSanctionsClient(
		Guid requestId,
		long targetSteamId,
		int outcome,
		string[] types,
		bool[] active,
		string[] reasons,
		string[] durations,
		string[] created,
		string[] scopes,
		string[] states,
		string[] flags,
		string[] notes )
	{
#if LIFEPUNCH_PACKAGE
		StaffMenuHost.OnPackageSanctionsReceived(
			requestId, targetSteamId, outcome, types, active, reasons, durations, created, scopes, states, flags, notes );
#endif
	}
}
#endif
