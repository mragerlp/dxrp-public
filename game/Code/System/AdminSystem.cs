using Dxura.RP.Game.System.Events;
using Dxura.RP.Game.UI;
using Dxura.RP.Shared;
using Sandbox.Diagnostics;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public class AdminSystem : SingletonComponent<AdminSystem>
{
	[Property] [Group( "Spawns" )] public required GameObject NpcPrefab { get; set; }
	internal readonly Dictionary<long, (Vector3 Position, Rotation Rotation)> PlayerReturnPositions = new();

	[Rpc.Host]
	public void KickPlayerHost( long steamId, string reason )
	{
		var callerId = Rpc.CallerId;
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.PlayerKick ) )
		{
			return;
		}

		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );
		var kickPlayer = GameUtils.GetPlayerById( steamId );

		if ( !player.IsValid() || !kickPlayer.IsValid() )
		{
			return;
		}

		if ( callerSteamId == kickPlayer.SteamId || !RankSystem.CanTarget( callerSteamId, kickPlayer.SteamId ) )
		{
			return;
		}

		_ = ApplyKick( player, kickPlayer, reason );
	}

	private static async Task ApplyKick( Player caller, Player target, string reason )
	{
		var callerSteamId = caller.SteamId;
		var callerSteamName = caller.SteamName;
		var callerDisplayName = caller.DisplayName;
		var targetSteamId = target.SteamId;
		var targetName = target.SteamName;
		var succeeded = await ServerApiClient.SanctionPlayer( targetSteamId, new CreateSanctionDto
		{
			Reason = reason,
			Notes = $"Kicked by {callerSteamName} ({callerSteamId}) in-game.",
			Type = SanctionType.Kick
		}, callerSteamId );

		await GameTask.MainThread();
		if ( !succeeded )
		{
			if ( caller.IsValid() )
			{
				caller.Error( "#generic.error" );
			}
			return;
		}

		// The durable sanction can succeed after the player disconnects. That is still a successful
		// moderation action and must still be audited; only the live disconnect step becomes unnecessary.
		var liveTarget = GameUtils.GetPlayerById( targetSteamId );
		if ( liveTarget.IsValid() && liveTarget.Connection != null )
		{
			GameNetworkManager.Instance.KickPlayer( liveTarget.Connection, reason );
		}

		Log.Info( $"[ADMIN] {callerDisplayName} ({callerSteamId}) kicked {targetName} ({targetSteamId}): {reason}" );
		_ = ServerApiClient.Audit( "Kick", $"{callerSteamName} ({callerSteamId}) has kicked {targetName} ({targetSteamId}) for {reason}", callerSteamId );
	}

	[Rpc.Host( NetFlags.Unreliable )]
	public void MovePlayerHost( long steamId, Vector3 targetPosition )
	{
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.PlayerGrab ) )
		{
			return;
		}

		var targetPlayer = GameUtils.GetPlayerById( steamId );

		if ( !targetPlayer.IsValid() )
		{
			return;
		}

		if ( !RankSystem.CanTarget( callerSteamId, targetPlayer.SteamId ) )
		{
			return;
		}

		targetPlayer.TeleportHost( new Transform( targetPosition, targetPlayer.WorldRotation ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	internal void BroadcastTeleportEffect( Player player, Vector3 fromPosition, Vector3 toPosition )
	{
		if ( !player.IsValid() || player.HasStatus( Constants.CloakStatus ) )
		{
			return;
		}

		Sound.Play( "teleport", fromPosition );
		Sound.Play( "teleport", toPosition );
	}

	[Rpc.Host]
	public void SpawnNpcHost()
	{
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.DebugFull ) )
		{
			return;
		}

		var callerPlayer = GameUtils.GetPlayerById( callerSteamId );
		if ( !callerPlayer.IsValid() )
		{
			return;
		}

		var npcClone = NpcPrefab.Clone( callerPlayer.WorldPosition + Vector3.Forward * 50, callerPlayer.WorldRotation );

		npcClone.NetworkSpawn();
	}

	[Rpc.Host]
	public void BankAllHost()
	{
		var auditActorSteamId = Rpc.Caller.SteamId;
		if ( SyntheticActorRegistry.IsSynthetic( auditActorSteamId )
		     || !RankSystem.HasPermission( auditActorSteamId, Permission.ManageEconomy ) )
		{
			return;
		}

		_ = BankAll( auditActorSteamId );
	}

	[Rpc.Host]
	public void SetWireTick( float? tick )
	{
		if ( !RankSystem.HasPermission( Rpc.Caller.SteamId, Permission.DebugFull ) )
		{
			return;
		}

		Wire.Wire.Current.WireTickOverride = tick;
	}

	[Rpc.Host]
	public void ForceScreenshotHost( long steamId )
	{
		var canExecute = Rpc.Caller.IsHost || RankSystem.HasPermission( Rpc.Caller.SteamId, Permission.ForceScreenshot );
		if ( !canExecute || !ServerApiLink.HasAuthorizationKey )
		{
			return;
		}

		var player = GameUtils.GetPlayerById( steamId );
		if ( !player.IsValid() )
		{
			return;
		}

		if ( !RankSystem.CanTarget( Rpc.Caller.SteamId, player.SteamId ) )
		{
			return;
		}

		using ( Rpc.FilterInclude( c => c == player.Connection ) )
		{
			BroadcastForceScreenshot();
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void BroadcastForceScreenshot()
	{
		_ = GameTask.RunInThreadAsync( async () =>
		{
			var texture = Texture.CreateRenderTarget().WithSize(  1920,1080 ).Create();

			try
			{
				await GameTask.MainThread();
				Scene.Camera.RenderToTexture( texture );
				await GameTask.WorkerThread();

				var bitmap = texture.GetBitmap( 0 );
				var payload = bitmap.ToPng();

				// Send screenshot to server API
				await PlayerApiClient.ShareScreenshot( payload, true );
			}
			finally
			{
				texture.Dispose();
			}
		} );
	}

	private async Task BankAll( long auditActorSteamId )
	{
		Assert.True( Networking.IsHost );

		long total = 0;
		var attempted = 0;
		var banked = 0;
		var failed = 0;
		var unknown = 0;
		var restoreFailed = 0;
		foreach ( var player in GameUtils.Players.ToList() )
		{
			var amount = player.WalletBalance;
			if ( amount == 0 )
			{
				continue;
			}

			attempted++;
			try
			{
				var didTakeout = await player.ChargeHost( amount, "BankAll", false, auditActorSteamId );

				if ( !didTakeout )
				{
					failed++;
					continue;
				}

				var bankResult = await player.PayHostStrictAudited( amount, "BankAll", true, auditActorSteamId );

				if ( bankResult == StrictMoneyMutationResult.Unknown )
				{
					unknown++;
					Log.Warning( $"[ADMIN] BankAll balance outcome is unknown for {player.SteamName} ({player.SteamId}); wallet remains debited pending portal verification." );
					continue;
				}

				if ( bankResult == StrictMoneyMutationResult.Rejected )
				{
					failed++;
					if ( !await player.RestoreWalletAfterFailedBankAll( amount, auditActorSteamId ) )
					{
						restoreFailed++;
						Log.Warning( $"[ADMIN] BankAll could not restore {amount:C0} to {player.SteamName} ({player.SteamId}); manual balance review required." );
					}
					continue;
				}

				total += amount;
				banked++;
			}
			catch ( Exception e )
			{
				unknown++;
				Log.Warning( $"[ADMIN] BankAll threw during {player.SteamName} ({player.SteamId}) for {amount:C0}; outcome is unknown and requires portal verification: {e.Message}" );
			}
		}

		if ( attempted == 0 )
		{
			Chat.Current?.BroadcastSystemText( "No non-empty wallets needed banking." );
		}
		else if ( failed == 0 && unknown == 0 )
		{
			Chat.Current?.BroadcastSystemText( $"All {banked} non-empty wallets have been banked (totalling {total:C0})." );
		}
		else
		{
			var restoreWarning = restoreFailed > 0
				? $" {restoreFailed} balance restore(s) require manual review."
				: failed > 0 ? " Rejected transfers kept their wallet balance." : string.Empty;
			var unknownWarning = unknown > 0
				? $" {unknown} transfer outcome(s) are unknown; do not retry or restore before portal verification."
				: string.Empty;
			Chat.Current?.BroadcastSystemText(
				$"Wallet banking was partial: {banked} of {attempted} non-empty wallets banked (totalling {total:C0}); {failed} rejected.{restoreWarning}{unknownWarning}" );
		}
	}

	[Rpc.Host]
	public void RestartHost( string reason, bool snapshot, float delaySeconds = 120 )
	{
		var caller = Rpc.Caller;
		var rpcCaller = Rpc.Caller;

		if ( !RankSystem.HasPermission( Rpc.Caller.SteamId, Permission.ServerRestart ) )
		{
			return;
		}

		Chat.Current?.BroadcastSystemText( $"{caller.DisplayName} has initiated a server restart" );

		_ = Restart( reason, snapshot, delaySeconds );
	}

	public async Task Restart( string reason, bool snapshot, float delaySeconds = 5 )
	{
		Assert.True( Networking.IsHost );

		Chat.Current?.BroadcastSystemText( $"Server is restarting ({reason}) in {delaySeconds} seconds." );

		if ( snapshot )
		{
			await SnapshotSystem.Current.SaveSnapshot();
		}

		// Wait for the specified delay, announcing every 10 seconds
		var remainingSeconds = delaySeconds;
		while ( remainingSeconds > 0 )
		{
			await GameTask.DelayRealtimeSeconds( Math.Min( 10, remainingSeconds ) );
			remainingSeconds -= Math.Min( 10, remainingSeconds );

			if ( remainingSeconds > 0 )
			{
				Chat.Current?.BroadcastSystemText( $"Server is restarting in {remainingSeconds} seconds..." );
			}
		}

		Chat.Current?.BroadcastSystemText( "Server is restarting now..." );

		using ( Rpc.FilterExclude( c => c.IsHost ) )
		{
			BroadcastEjectToWaitingRoom();
		}

		await GameTask.DelayRealtimeSeconds( 5 );

		Sandbox.Game.Close();
	}

	[Rpc.Host]
	public void NukeHost()
	{
		var rpcCaller = Rpc.Caller;

		if ( !RankSystem.HasPermission( Rpc.Caller.SteamId, Permission.DebugFull ) )
		{
			return;
		}

		Log.Warning( "Nuke from " + rpcCaller.DisplayName + " (" + rpcCaller.SteamId + ")" );

		Sandbox.Game.Close();
	}

	[Rpc.Host]
	public void FreezeConstructs()
	{
		var rpcCaller = Rpc.Caller;

		if ( !RankSystem.HasPermission( Rpc.Caller.SteamId, Permission.ServerRestart ) )
		{
			return;
		}

		Log.Warning( "Construct freeze from " + rpcCaller.DisplayName + " (" + rpcCaller.SteamId + ")" );

		var freezeCount = 0;
		var constructs = Sandbox.Game.ActiveScene.Components.GetAll<IConstruct>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var construct in constructs )
		{
			freezeCount += construct.IsFrozen ? 0 : 1;
			construct.Freeze( construct.GameObject.WorldPosition, construct.GameObject.WorldRotation );
		}

		rpcCaller.SendLog( LogLevel.Info, $"Froze {freezeCount} constructs." );
	}

	[Rpc.Host]
	public void ToggleEventHost( string eventIdentifier )
	{
		var caller = Rpc.Caller;
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.DebugFull ) )
		{
			return;
		}

		var eventSystem = EventSystem.Instance;
		if ( !eventSystem.IsValid() )
		{
			Log.Warning( "EventSystem not found" );
			return;
		}

		if ( string.IsNullOrEmpty( eventIdentifier ) )
		{
			Log.Warning( "Invalid event identifier" );
			return;
		}

		eventSystem.Toggle( eventIdentifier );
	}

	[Rpc.Host]
	public void AddStatusHost( string playerName, string statusId, float? duration )
	{
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.DebugAccess ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( Rpc.CallerId );
		if ( !caller.IsValid() )
		{
			return;
		}

		statusId = statusId?.Trim() ?? string.Empty;
		if ( statusId.Length == 0 || Status.Current.GetCachedInstance( statusId ) is null )
		{
			caller.Error( $"Status '{statusId}' not found" );
			return;
		}

		if ( duration.HasValue && (float.IsNaN( duration.Value ) || float.IsInfinity( duration.Value ) || duration.Value <= 0) )
		{
			caller.Error( "Status duration must be greater than zero" );
			return;
		}

		var matchingPlayers = GameUtils.GetPlayersByName( playerName );

		if ( matchingPlayers.Count == 0 )
		{
			if ( caller.IsValid() )
			{
				caller.Error( $"Player '{playerName}' not found" );
			}
			return;
		}

		if ( matchingPlayers.Count > 1 )
		{
			if ( caller.IsValid() )
			{
				var playerNames = string.Join( ", ", matchingPlayers.Select( p => p.DisplayName ) );
				caller.SendMessage( $"Multiple players found matching '{playerName}': {playerNames}" );
			}
			return;
		}

		var target = matchingPlayers[0];
		if ( target.SteamId != callerSteamId && !RankSystem.CanTarget( callerSteamId, target.SteamId ) )
		{
			caller.Error( "#command.errors.higher_rank" );
			return;
		}

		Status.Current.AddStatus( target, statusId, duration );
		if ( !Status.Current.HasStatus( target.SteamId, statusId ) )
		{
			caller.Error( "#generic.error" );
			return;
		}

		// Notify the caller
		var durationText = duration.HasValue ? $" for {duration.Value}s" : "";
		caller.SendMessage( $"Added status '{statusId}' to {target.DisplayName}{durationText}" );

		// Log the action
		var logDurationText = duration.HasValue ? $" for {duration.Value}s" : "";
		Log.Info( $"[ADMIN] {caller.DisplayName} ({callerSteamId}) added status '{statusId}' to {target.DisplayName} ({target.SteamId}){logDurationText}" );
		_ = ServerApiClient.Audit( "Status", $"{caller.SteamName} ({callerSteamId}) added status '{statusId}' to {target.SteamName} ({target.SteamId}){logDurationText}", caller.SteamId );
	}

	[Rpc.Host]
	public void RemoveStatusHost( string playerName, string statusId )
	{
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.DebugAccess ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( Rpc.CallerId );
		if ( !caller.IsValid() )
		{
			return;
		}

		statusId = statusId?.Trim() ?? string.Empty;
		if ( statusId.Length == 0 || Status.Current.GetCachedInstance( statusId ) is null )
		{
			caller.Error( $"Status '{statusId}' not found" );
			return;
		}

		var matchingPlayers = GameUtils.GetPlayersByName( playerName );

		if ( matchingPlayers.Count == 0 )
		{
			if ( caller.IsValid() )
			{
				caller.Error( $"Player '{playerName}' not found" );
			}
			return;
		}

		if ( matchingPlayers.Count > 1 )
		{
			if ( caller.IsValid() )
			{
				var playerNames = string.Join( ", ", matchingPlayers.Select( p => p.DisplayName ) );
				caller.SendMessage( $"Multiple players found matching '{playerName}': {playerNames}" );
			}
			return;
		}

		var target = matchingPlayers[0];
		if ( target.SteamId != callerSteamId && !RankSystem.CanTarget( callerSteamId, target.SteamId ) )
		{
			caller.Error( "#command.errors.higher_rank" );
			return;
		}

		if ( !Status.Current.HasStatus( target.SteamId, statusId ) )
		{
			caller.Error( $"Status '{statusId}' is not active on {target.DisplayName}" );
			return;
		}

		Status.Current.RemoveStatus( target, statusId );
		if ( Status.Current.HasStatus( target.SteamId, statusId ) )
		{
			caller.Error( "#generic.error" );
			return;
		}

		// Notify the caller
		caller.SendMessage( $"Removed status '{statusId}' from {target.DisplayName}" );

		// Log the action
		Log.Info( $"[ADMIN] {caller.DisplayName} ({callerSteamId}) removed status '{statusId}' from {target.DisplayName} ({target.SteamId})" );
		_ = ServerApiClient.Audit( "Status", $"{caller.SteamName} ({callerSteamId}) removed status '{statusId}' from {target.SteamName} ({target.SteamId})", caller.SteamId );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private static void BroadcastEjectToWaitingRoom()
	{
		if ( !GameManager.Instance.IsValid() )
		{
			return;
		}

		GameManager.Instance.EjectToWaitingRoom();
	}

	[Rpc.Host]
	public void RequestConnectionStatsHost()
	{
		var caller = Rpc.Caller;
		var callerSteamId = Rpc.Caller.SteamId;

		if ( !RankSystem.HasPermission( callerSteamId, Permission.DebugFull ) )
		{
			return;
		}

		foreach ( var connection in Connection.All )
		{
			caller.SendLog( LogLevel.Info, $"""
			                                			Connection ({connection.DisplayName}):
			                                			  ID: {connection.Id}
			                                			  SteamID: {connection.SteamId}
			                                			  Address: {connection.Address}
			                                			  Ping: {connection.Ping}
			                                			  Latency: {connection.Latency}
			                                			  Quality: {connection.Stats.ConnectionQuality}
			                                			  InBytesPerSecond: {connection.Stats.InBytesPerSecond}
			                                			  InPacketsPerSecond: {connection.Stats.InPacketsPerSecond}
			                                			  OutBytesPerSecond: {connection.Stats.OutBytesPerSecond}
			                                			  OutPacketsPerSecond: {connection.Stats.OutPacketsPerSecond}
			                                			  SendRateBytesPerSecond: {connection.Stats.SendRateBytesPerSecond}
			                                			  MessagesReceived: {connection.MessagesRecieved}
			                                			  MessagesSent: {connection.MessagesSent}
			                                			  Connection Time: {connection.ConnectionTime}
			                                			-----------------------------
			                                """ );
		}
	}
}
