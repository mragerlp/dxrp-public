using System.Threading.Tasks;
using System.Text.Json;
using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class WaypointCommand : ICommand
{
	private const string StorePrefix = "commands:waypoint:";

	public string Command => "waypoint";
	public string[] Aliases => ["wp"];
	public string Help => Language.GetPhrase( "command.waypoint.help" );

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( !caller.IsValid() )
		{
			return false;
		}

		if ( args.Length == 0 )
		{
			caller.SendMessage( Help );
			return true;
		}

		var action = args[0].ToLowerInvariant();
		switch ( action )
		{
			case "set" when !HasPermission( caller, Permission.CommandWaypointEdit ):
				return true;
			case "set":
				{
					var name = ParseName( args.Skip( 1 ) );
					if ( name == null )
					{
						caller.SendMessage( Language.GetPhrase( "command.waypoint.usage_set" ) );
						return true;
					}

					var waypoint = WaypointData.FromTransform( caller.WorldPosition, Rotation.LookAt( caller.AimRay.Forward ));
					_ = SetWaypointAsync( caller, name, waypoint );
					return true;
				}
			case "list" when !HasPermission( caller, Permission.CommandWaypointUse ):
				return true;
			case "list":
				_ = ListWaypointsAsync( caller );
				return true;
			case "clear" when !HasPermission( caller, Permission.CommandWaypointEdit ):
				return true;
			case "clear":
				{
					var name = ParseName( args.Skip( 1 ) );
					if ( name == null )
					{
						caller.SendMessage( Language.GetPhrase( "command.waypoint.usage_clear" ) );
						return true;
					}

					_ = ClearWaypointAsync( caller, name );
					return true;
				}
		}

		if ( !HasPermission( caller, Permission.CommandWaypointUse ) )
		{
			return true;
		}

		var waypointName = ParseName( args );
		if ( waypointName == null )
		{
			caller.SendMessage( Help );
			return true;
		}

		_ = GoToWaypointAsync( caller, waypointName );
		return true;
	}

	private static async Task SetWaypointAsync( Player caller, string name, WaypointData waypoint )
	{
		var callerSteamId = caller.SteamId;
		var callerConnectionId = caller.ConnectionId;
		var callerSteamName = caller.SteamName;
		if ( !CanPersistWaypoint( callerSteamId ) )
		{
			caller.Error( "#generic.error" );
			return;
		}

		var persisted = await ServerApiClient.TrySetStoreStrict( GetWaypointKey( name ), JsonSerializer.Serialize( waypoint ) );

		await GameTask.MainThread();
		var callerSessionIsCurrent = IsCurrentCallerSession( caller, callerSteamId, callerConnectionId );
		if ( !persisted )
		{
			if ( callerSessionIsCurrent && RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointEdit ) )
			{
				caller.Error( "#generic.error" );
			}
			return;
		}

		if ( callerSessionIsCurrent && RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointEdit ) )
		{
			caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.saved" ), name ) );
		}
		_ = ServerApiClient.Audit( "Waypoint", $"{callerSteamName} ({callerSteamId}) set waypoint '{name}'", callerSteamId );
	}

	private static async Task ClearWaypointAsync( Player caller, string name )
	{
		var callerSteamId = caller.SteamId;
		var callerConnectionId = caller.ConnectionId;
		var callerSteamName = caller.SteamName;
		if ( !CanPersistWaypoint( callerSteamId ) )
		{
			caller.Error( "#generic.error" );
			return;
		}

		var waypointRead = await LoadWaypoint( name );
		await GameTask.MainThread();
		if ( !IsCurrentCallerSession( caller, callerSteamId, callerConnectionId ) )
		{
			return;
		}

		if ( !CanPersistWaypoint( callerSteamId ) )
		{
			caller.Error( "#generic.error" );
			return;
		}

		if ( !waypointRead.Succeeded )
		{
			caller.Error( "#generic.error" );
			return;
		}

		if ( !waypointRead.Found || waypointRead.Value == null )
		{
			caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.not_found" ), name ) );
			return;
		}

		var persisted = await ServerApiClient.TryDeleteStoreStrict( GetWaypointKey( name ) );

		await GameTask.MainThread();
		var callerSessionIsCurrent = IsCurrentCallerSession( caller, callerSteamId, callerConnectionId );
		if ( !persisted )
		{
			if ( callerSessionIsCurrent && RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointEdit ) )
			{
				caller.Error( "#generic.error" );
			}
			return;
		}

		if ( callerSessionIsCurrent && RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointEdit ) )
		{
			caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.cleared" ), name ) );
		}
		_ = ServerApiClient.Audit( "Waypoint", $"{callerSteamName} ({callerSteamId}) cleared waypoint '{name}'", callerSteamId );
	}

	private static async Task ListWaypointsAsync( Player caller )
	{
		var callerSteamId = caller.SteamId;
		var callerConnectionId = caller.ConnectionId;
		var waypointRead = await LoadWaypointNames();

		await GameTask.MainThread();
		if ( !IsCurrentCallerSession( caller, callerSteamId, callerConnectionId ) )
		{
			return;
		}

		if ( !HasPermission( caller, Permission.CommandWaypointUse ) )
		{
			return;
		}

		if ( !waypointRead.Succeeded )
		{
			caller.Error( "#generic.error" );
			return;
		}

		if ( waypointRead.Names.Count == 0 )
		{
			caller.SendMessage( Language.GetPhrase( "command.waypoint.none" ) );
			return;
		}

		var names = waypointRead.Names.OrderBy( name => name, StringComparer.OrdinalIgnoreCase );

		caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.list" ), string.Join( ", ", names ) ) );
	}

	private static async Task GoToWaypointAsync( Player caller, string name )
	{
		var callerSteamId = caller.SteamId;
		var callerConnectionId = caller.ConnectionId;
		var waypointRead = await LoadWaypoint( name );
		await GameTask.MainThread();
		if ( !IsCurrentCallerSession( caller, callerSteamId, callerConnectionId )
		     || !RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointUse ) )
		{
			return;
		}

		if ( !waypointRead.Succeeded )
		{
			caller.Error( "#generic.error" );
			return;
		}

		if ( !waypointRead.Found || waypointRead.Value == null )
		{
			caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.not_found" ), name ) );
			return;
		}

		var oldPosition = caller.WorldPosition;
		AdminSystem.Instance.PlayerReturnPositions[caller.SteamId] = (oldPosition, Rotation.LookAt( caller.AimRay.Forward ));

		var transform = new Transform( waypointRead.Value.ToPosition(), waypointRead.Value.ToRotation() );
		caller.TeleportHost( transform );
		OcclusionSystem.Current?.BroadcastForceCheckHost( caller.Connection );
		AdminSystem.Instance?.BroadcastTeleportEffect( caller, oldPosition, transform.Position );

		caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.teleported" ), NormalizeName( name ) ) );
		_ = ServerApiClient.Audit( "Waypoint", $"{caller.SteamName} ({caller.SteamId}) teleported to waypoint '{NormalizeName( name )}'", caller.SteamId );
	}

	private static async Task<(bool Succeeded, bool Found, WaypointData? Value)> LoadWaypoint( string name )
	{
		var read = await ServerApiClient.ReadStoreValue( GetWaypointKey( name ) );
		if ( !read.Succeeded || !read.Found )
		{
			return (read.Succeeded, read.Found, null);
		}

		try
		{
			var waypoint = JsonSerializer.Deserialize<WaypointData>( read.Value ?? string.Empty );
			return waypoint == null ? (false, true, null) : (true, true, waypoint);
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to deserialize waypoint '{NormalizeName( name )}': {e.Message}" );
			return (false, true, null);
		}
	}

	private static async Task<(bool Succeeded, List<string> Names)> LoadWaypointNames()
	{
		var read = await ServerApiClient.ReadStoreList( StorePrefix );
		var names = read.Entries
			.Select( entry => TryGetWaypointName( entry.Key ) )
			.Where( name => !string.IsNullOrWhiteSpace( name ) )
			.Select( name => name! )
			.ToList();
		return (read.Succeeded, names);
	}

	private static bool HasPermission( Player caller, Permission permission )
	{
		if ( RankSystem.HasPermission( caller.SteamId, permission ) )
		{
			return true;
		}

		caller.SendMessage( string.Format( Language.GetPhrase( "command.waypoint.missing_permission" ), permission.ToId() ) );
		return false;
	}

	private static bool IsCurrentCallerSession( Player caller, long steamId, Guid connectionId ) =>
		caller.IsValid()
		&& caller.IsConnected
		&& caller.SteamId == steamId
		&& caller.ConnectionId == connectionId;

	private static bool CanPersistWaypoint( long callerSteamId ) =>
		ServerApiLink.HasAuthorizationKey
		&& !SyntheticActorRegistry.IsSynthetic( callerSteamId )
		&& RankSystem.HasPermission( callerSteamId, Permission.CommandWaypointEdit );

	private static string? ParseName( IEnumerable<string> parts )
	{
		var name = string.Join( ' ', parts ).Trim();
		if ( string.IsNullOrWhiteSpace( name ) )
		{
			return null;
		}

		return name.Length <= 64 ? name : name[..64];
	}

	private static string NormalizeName( string name ) => name.Trim().ToLowerInvariant();

	private static string GetWaypointKey( string name ) => $"{StorePrefix}{NormalizeName( name )}";

	private static string? TryGetWaypointName( string key ) =>
		key.StartsWith( StorePrefix, StringComparison.OrdinalIgnoreCase )
			? key[StorePrefix.Length..]
			: null;

	public sealed class WaypointData
	{
		public float X { get; init; }
		public float Y { get; init; }
		public float Z { get; init; }
		public float Pitch { get; init; }
		public float Yaw { get; init; }
		public float Roll { get; init; }

		public static WaypointData FromTransform( Vector3 position, Rotation rotation )
		{
			var angles = rotation.Angles();
			return new WaypointData
			{
				X = position.x,
				Y = position.y,
				Z = position.z,
				Pitch = angles.pitch,
				Yaw = angles.yaw,
				Roll = angles.roll
			};
		}

		public Vector3 ToPosition() => new( X, Y, Z );

		public Rotation ToRotation() => Rotation.From( new Angles( Pitch, Yaw, Roll ) );
	}
}
