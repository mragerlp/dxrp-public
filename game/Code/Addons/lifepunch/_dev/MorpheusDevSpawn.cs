// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LifePunch editor dev lane" (Code/_dev — NOT shipped; lifepunchulx publishes six staff-menu files only) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL

using System.Linq;
using Dxura.RP.Game;
using LifePunch.DXRP.Addons.StaffMenu;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// Play-only Morpheus clone at the downtown <c>mayor_spawn</c> pin. Does not write <c>game.scene</c>.
/// </summary>
public static class MorpheusDevSpawn
{
	public const string PrefabPath = "addons/lifepunch/lpmorpheus/morpheus.prefab";
	public const string TownHallSpawnName = "mayor_spawn";
	public const string SessionTag = "lp_morpheus_dev";

	[ConCmd( "lp_spawn_morpheus" )]
	public static void SpawnMorpheus()
	{
		TrySpawnMorpheus( out _ );
	}

	/// <summary>
	/// Sit Morpheus at town hall and seat GROK within press range for a chat walkup.
	/// Does not possess Bloodwave. Drive GROK by GUID after spawn.
	/// </summary>
	[ConCmd( "lp_morpheus_walkup" )]
	public static void MorpheusWalkup()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_morpheus_walkup: editor-only." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_morpheus_walkup: must be host (editor play)." );
			return;
		}

		if ( !TrySpawnMorpheus( out var morpheus ) )
		{
			return;
		}

		var approach = morpheus.WorldPosition
			+ morpheus.WorldRotation.Forward * 72f
			+ Vector3.Up * 10f;
		var facing = Rotation.LookAt( (morpheus.WorldPosition - approach).WithZ( 0f ).Normal, Vector3.Up );

		var grok = StaffMenuTestBots.SpawnOrReplaceNamedBot(
			IntellibotSession.GrokName,
			IntellibotSession.GrokSteamId,
			approach );
		if ( !grok.IsValid() )
		{
			Log.Warning( "lp_morpheus_walkup: GROK spawn failed." );
			return;
		}

		grok.TeleportHost( new Transform( approach, facing ) );
		var ranked = StaffMenuTestBots.TryAssignNamedBotRank(
			IntellibotSession.GrokName,
			IntellibotSession.GrokSteamId,
			IntellibotSession.GrokRank );

		Log.Info(
			$"lp_morpheus_walkup: Morpheus={morpheus.Id} GROK={grok.GameObject.Id} " +
			$"superadmin={ranked} — drive_player GROK GUID, walk into press, hear visitor file / civic briefing." );
		if ( !ranked )
		{
			Log.Warning( "lp_morpheus_walkup: Super Admin not assigned. Run lp_authorize, then lp_rank_grok." );
		}
	}

	private static bool TrySpawnMorpheus( out GameObject go )
	{
		go = null;

		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_spawn_morpheus: editor-only." );
			return false;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_spawn_morpheus: must be host (editor play)." );
			return false;
		}

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			Log.Warning( "lp_spawn_morpheus: no active scene." );
			return false;
		}

		var prefab = ResourceLibrary.Get<PrefabFile>( PrefabPath );
		if ( prefab is null )
		{
			Log.Warning( $"lp_spawn_morpheus: prefab not found '{PrefabPath}'." );
			return false;
		}

		var spawn = scene.GetAllObjects( true )
			.FirstOrDefault( o => o.IsValid() && o.Name == TownHallSpawnName );

		Transform seat;
		string seatLabel;
		if ( spawn.IsValid() )
		{
			seat = spawn.WorldTransform;
			seatLabel = TownHallSpawnName;
		}
		else
		{
			var local = Player.Local;
			if ( !local.IsValid() )
			{
				Log.Warning( $"lp_spawn_morpheus: '{TownHallSpawnName}' missing and no Player.Local for fallback." );
				return false;
			}

			seat = new Transform(
				local.WorldPosition + local.WorldRotation.Forward * 120f,
				local.WorldRotation );
			seatLabel = "local-forward-fallback";
			Log.Warning( $"lp_spawn_morpheus: '{TownHallSpawnName}' missing — seating near Player.Local." );
		}

		foreach ( var previous in scene.GetAllObjects( true )
			         .Where( o => o.IsValid() && o.Tags.Has( SessionTag ) )
			         .ToArray() )
		{
			previous.Destroy();
		}

		go = SceneUtility.GetPrefabScene( prefab ).Clone( seat );
		go.Tags.Add( SessionTag );

		var bounds = go.GetBounds();
		if ( bounds.Size.z > 1f )
		{
			var sink = seat.Position.z + 1f - bounds.Mins.z;
			if ( sink > 0f )
			{
				go.WorldPosition += Vector3.Up * sink;
			}
		}

		if ( go.NetworkMode == NetworkMode.Object )
		{
			go.NetworkSpawn();
		}

		Log.Info( $"lp_spawn_morpheus: seated at {seatLabel} pos={go.WorldPosition}." );
		return true;
	}
}

#endif
