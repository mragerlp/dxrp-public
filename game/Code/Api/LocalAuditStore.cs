using System;
using System.Collections.Generic;

namespace Dxura.RP.Game;

/// <summary>
/// Host-side bounded audit ring. Fed from <see cref="ServerApiClient.Audit"/> so every
/// existing callsite lands a local row. Oldest-out at <see cref="Capacity"/>. The remote
/// POST remains the durable copy and is not owned here.
/// </summary>
public static class LocalAuditStore
{
	public const int Capacity = 500;

	public readonly record struct Row(
		DateTimeOffset WhenUtc,
		string Action,
		long ActorSteamId,
		string ActorName,
		string Description );

	private static readonly object Gate = new();
	private static readonly List<Row> Rows = new();

	public static void Record( string action, string description, long? cause )
	{
		var actorId = cause ?? 0L;
		var row = new Row(
			DateTimeOffset.UtcNow,
			action ?? string.Empty,
			actorId,
			ResolveActorName( actorId ),
			description ?? string.Empty );

		lock ( Gate )
		{
			if ( Rows.Count >= Capacity )
			{
				Rows.RemoveAt( 0 );
			}

			Rows.Add( row );
		}
	}

	public static IReadOnlyList<Row> SnapshotNewestFirst()
	{
		lock ( Gate )
		{
			var copy = new List<Row>( Rows.Count );
			for ( var i = Rows.Count - 1; i >= 0; i-- )
			{
				copy.Add( Rows[i] );
			}

			return copy;
		}
	}

	private static string ResolveActorName( long steamId )
	{
		if ( steamId == 0L )
		{
			return "system";
		}

		if ( !GameNetworkManager.Instance.IsValid() )
		{
			return steamId.ToString();
		}

		var player = GameUtils.GetPlayerById( steamId );
		if ( !player.IsValid() )
		{
			return steamId.ToString();
		}

		if ( !string.IsNullOrWhiteSpace( player.SteamName ) )
		{
			return player.SteamName;
		}

		return player.DisplayName;
	}
}
