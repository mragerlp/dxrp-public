namespace Dxura.RP.Game;

public class RespawnerSystem : SingletonComponent<RespawnerSystem>, IGameEvents
{
	private List<Transform> SpawnPoints { get; } = new();
	private Dictionary<GameModeJobDto, List<Transform>> JobSpawnPoints { get; } = new();
	private Dictionary<GameModeJobGroupDto, List<Transform>> GroupSpawnPoints { get; } = new();
	private List<(string[] Tags, Transform Transform)> TagSpawnPoints { get; } = new();

	private bool _mapFitted;

	protected override void OnStart()
	{
		// Without a server authorization key, there's no API-provided map fitting - allow spawning immediately.
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			_mapFitted = true;
		}

		RefreshSpawnPoints();
	}

	public Transform GetSpawnPoint( Player? player = null )
	{
		if ( player is not null )
		{
			// Restricted players spawn at jail-tagged spawn points
			if ( player.Restricted )
			{
				var jailSpawns = TagSpawnPoints
					.Where( e => e.Tags.Contains( WorldSpawnPoint.JailTag, StringComparer.OrdinalIgnoreCase ) )
					.Select( e => e.Transform )
					.ToList();
				if ( jailSpawns.Count > 0 )
				{
					var jailSpawnPoint = Sandbox.Game.Random.FromList( jailSpawns );
					return new Transform( jailSpawnPoint.Position, jailSpawnPoint.Rotation );
				}
			}

			// Try job-specific spawn
			var jobSpecificSpawns = JobSpawnPoints.GetValueOrDefault( player.Job );
			if ( jobSpecificSpawns is { Count: > 0 } )
			{
				var jobSpawnPoint = Sandbox.Game.Random.FromList( jobSpecificSpawns );
				return new Transform( jobSpawnPoint.Position, jobSpawnPoint.Rotation );
			}

			// Try job group spawn
			var group = player.Job.GetGroup();
			if ( group is not null )
			{
				var groupSpawns = GroupSpawnPoints.GetValueOrDefault( group );
				if ( groupSpawns is { Count: > 0 } )
				{
					var groupSpawnPoint = Sandbox.Game.Random.FromList( groupSpawns );
					return new Transform( groupSpawnPoint.Position, groupSpawnPoint.Rotation );
				}
			}

			// Try tag-based spawn
			var tagMatches = TagSpawnPoints
				.Where( e => e.Tags.All( t => JobTags.HasNamedTag( player.Job, t ) ) )
				.Select( e => e.Transform )
				.ToList();
			if ( tagMatches.Count > 0 )
			{
				var tagSpawnPoint = Sandbox.Game.Random.FromList( tagMatches );
				return new Transform( tagSpawnPoint.Position, tagSpawnPoint.Rotation );
			}
		}

		var spawnPoint = Sandbox.Game.Random.FromList( SpawnPoints );
		return new Transform( spawnPoint.Position, spawnPoint.Rotation );
	}

	public void OnMapFitted()
	{
		_mapFitted = true;
		RefreshSpawnPoints();
		MovePlayersOffPreFitSpawns();
	}

	private bool _movedAfterFirstFit;

	/// <summary>
	/// Players created before the first map fit stand on pre-fit spawn points (in the editor the host is created about
	/// 11 s before the fit and dies of the fall). At the first fit only, the host moves every living player onto a
	/// fitted spawn point; later refits never move anyone.
	/// </summary>
	private void MovePlayersOffPreFitSpawns()
	{
		if ( _movedAfterFirstFit || !Networking.IsHost )
		{
			return;
		}

		_movedAfterFirstFit = true;

		foreach ( var player in GameUtils.Players.ToList() )
		{
			if ( !player.IsValid() || player.IsDead )
			{
				continue;
			}

			player.TeleportHost( GetSpawnPoint( player ) );
			Log.Info( $"[Respawner] SPAWN_SAFE_20261017 moved {player.DisplayName} to a fitted spawn point after the first map fit" );
		}
	}

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		RefreshSpawnPoints();
	}

	private void RefreshSpawnPoints()
	{
		SpawnPoints.Clear();
		JobSpawnPoints.Clear();
		GroupSpawnPoints.Clear();
		TagSpawnPoints.Clear();

		var officialSpawns = Scene.GetAllComponents<WorldSpawnPoint>().ToList();

		// Prioritize official WorldSpawnPoint components if available
		if ( officialSpawns.Count > 0 )
		{
			foreach ( var spawnPoint in officialSpawns )
			{
				var transform = new Transform( spawnPoint.WorldPosition, spawnPoint.WorldRotation );

				switch ( spawnPoint.Type )
				{
					case SpawnType.Job:
						if ( spawnPoint.Job is not null )
						{
							JobSpawnPoints.TryAdd( spawnPoint.Job, new List<Transform>() );
							JobSpawnPoints[spawnPoint.Job].Add( transform );
						}
						break;

					case SpawnType.JobGroup:
						if ( spawnPoint.Group is not null )
						{
							GroupSpawnPoints.TryAdd( spawnPoint.Group, new List<Transform>() );
							GroupSpawnPoints[spawnPoint.Group].Add( transform );
						}
						break;

					case SpawnType.Tag:
						if ( spawnPoint.Tags.Length > 0 )
							TagSpawnPoints.Add( (spawnPoint.Tags, transform) );
						break;
					default:
						SpawnPoints.Add( transform );
						break;
				}
			}
		}

		// Fallback to legacy spawn points if no official ones found
		if ( SpawnPoints.Count == 0 )
		{
			var backupSpawns = Scene.GetAllComponents<SpawnPoint>().ToList();
			foreach ( var spawnPoint in backupSpawns )
			{
				SpawnPoints.Add( new Transform( spawnPoint.WorldPosition, spawnPoint.WorldRotation ) );
			}
		}

		// Last resort fallback if no spawn points found at all
		if ( SpawnPoints.Count == 0 )
		{
			Log.Warning( "No spawn points found in the scene!" );
			SpawnPoints.Add( new Transform( WorldPosition, WorldRotation ) );
		}
	}

	public void OnSecondlyUpdate()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		foreach ( var player in GameUtils.Players.ToList() )
		{
			switch ( player.RespawnState )
			{
				case RespawnState.Requested:
					player.RespawnState = RespawnState.Delayed;
					break;

				case RespawnState.Delayed:
					if ( player.GetEffectiveRespawnElapsed() > Config.Current.Game.RespawnTime )
					{
						var newState = player.IsDebugPlayer ? RespawnState.Immediate : RespawnState.Approved;

						HandleDemotions( player );

						player.RespawnState = newState;
					}

					break;

				case RespawnState.Immediate:
					if ( Config.Current.Game.MapFittingEnabled && !_mapFitted )
						break;
					player.SpawnHost();
					break;
			}
		}

	}

	[Rpc.Host]
	public void RequestRespawn()
	{
		var callerId = Rpc.CallerId;

		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:respawn:request", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );

		if ( !player.IsValid() )
		{
			return;
		}

		if ( player.RespawnState != RespawnState.Approved )
		{
			player.Warn( "You can't respawn yet..." );
			return;
		}

		player.RespawnState = RespawnState.Immediate;
	}


	private void HandleDemotions( Player player )
	{
		if ( !player.IsValid() || !player.Job.DemoteOnRespawn )
		{
			return;
		}

		Log.Info( $"Demoting player {player.DisplayName} due to death" );

		var demotedFromJob = player.Job.Id;

		// Demote to citizen
		player.Job = GameModeJobs.GetByTagOrFallback( JobTag.Citizen, "Citizen" );

		Cooldown.Current.StartCooldown(
			$"{player.SteamId}:job:{demotedFromJob}",
			Config.Current.Game.DemoteJobReapplyCooldown );
	}
}
