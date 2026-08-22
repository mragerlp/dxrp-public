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

using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// The moon test space: a sparse fitting with exactly ONE of each DXRP native
/// world prefab (doors, atm, drug drop, bins, boards, ladder, elevator) plus
/// spawn points, laid out on sensed moon ground. Play-only; never writes
/// <c>game.scene</c>. Enable with <c>lp_test_space 1</c> (or launch arg
/// <c>+lp_test_space true</c> on the test-space shortcut); while enabled the
/// session holds the map on the moon (defeating the API's per-init map push),
/// keeps fitting suppressed, and applies the catalog once ground is physical.
/// <c>lp_fit_test</c> enables and applies immediately; <c>lp_fit_clear</c>
/// wipes the pad (it re-applies within a second unless lp_test_space is 0).
/// </summary>
public static class TestSpaceDev
{
	public const string TestTag = "lp_test_space";

	/// <summary>One of each DXRP native world prefab.</summary>
	public static readonly string[] Catalog =
	[
		"prefabs/world/doors/door_single.prefab",
		"prefabs/world/doors/door_double.prefab",
		"prefabs/world/doors/door_roller.prefab",
		"prefabs/world/doors/door_manhole.prefab",
		"prefabs/world/atm.prefab",
		"prefabs/world/drug_drop.prefab",
		"prefabs/world/bin.prefab",
		"prefabs/helpers/garbage_point.prefab",
		"prefabs/world/stat_board.prefab",
		"prefabs/world/law_board_world.prefab",
		"prefabs/world/motd.prefab",
		"prefabs/world/panic.prefab",
		"prefabs/world/ladder.prefab",
		"prefabs/world/elevator.prefab"
	];

	private const float SlotSpacing = 180f;
	private const int SlotsPerRow = 5;
	private const string SpawnPointPrefab = "prefabs/world/world_spawn.prefab";
	private const int SpawnPointCount = 4;

	[ConVar( "lp_test_space" )]
	public static bool Enabled { get; set; }

	[ConCmd( "lp_fit_test" )]
	public static void FitTest()
	{
		Enabled = true;

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || scene.IsEditor || !Networking.IsHost )
		{
			Log.Info( "lp_fit_test: enabled — applies when a hosted play session is up." );
			return;
		}

		Tick( scene, verbose: true );
	}

	[ConCmd( "lp_fit_clear" )]
	public static void FitClear()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			return;
		}

		var spawned = scene.FindAllWithTag( TestTag ).ToArray();
		foreach ( var go in spawned )
		{
			go.Destroy();
		}

		Log.Info( $"lp_fit_clear: removed {spawned.Length} test-space object(s). " +
		          $"lp_test_space={(Enabled ? "1 (pad re-applies in ~1s)" : "0")}" );
	}

	/// <summary>
	/// One pass of the test-space discipline: suppress fitting, hold the moon,
	/// apply the catalog when ground is physical. Idempotent — catalog presence
	/// is keyed on the <see cref="TestTag"/> tag, not on latched state.
	/// </summary>
	public static void Tick( Scene scene, bool verbose = false )
	{
		if ( !Enabled || !Application.IsEditor || !Networking.IsHost || scene.IsEditor )
		{
			return;
		}

		var map = scene.Components.GetAll<MapInstance>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault();
		if ( !map.IsValid() )
		{
			if ( verbose )
			{
				Log.Warning( "lp_test_space: no MapInstance in the active scene." );
			}

			return;
		}

		// Idempotent suppression: inerts the downtown fitter and un-gates
		// RespawnerSystem. Re-checked every tick — no cross-session latch.
		if ( Config.Current?.Game is not null && Config.Current.Game.MapFittingEnabled )
		{
			Config.Current.Game.MapFittingEnabled = false;
			Log.Info( "lp_test_space: MapFittingEnabled off for this play session." );
		}

		// Watchdog: hold the moon even when the API init pushes the portal map.
		if ( map.MapName != MapWorkspaceDev.MoonIdent )
		{
			Log.Info( $"lp_test_space: map '{map.MapName}' -> '{MapWorkspaceDev.MoonIdent}'." );
			map.MapName = MapWorkspaceDev.MoonIdent;
			return;
		}

		if ( !map.IsLoaded )
		{
			if ( verbose )
			{
				Log.Info( "lp_test_space: moon still loading — catalog lands on the next tick." );
			}

			return;
		}

		if ( scene.FindAllWithTag( TestTag ).Any() )
		{
			if ( verbose )
			{
				Log.Info( "lp_test_space: pad already present (lp_fit_clear to rebuild)." );
			}

			return;
		}

		// Wait until the moon is physical where the pad goes.
		var center = TraceGround( scene, Vector3.Zero );
		if ( center is null )
		{
			if ( verbose )
			{
				Log.Info( "lp_test_space: ground not physical yet — catalog lands on the next tick." );
			}

			return;
		}

		ApplyCatalog( scene, center.Value );
	}

	private static Vector3? TraceGround( Scene scene, Vector3 at )
	{
		var tr = scene.Trace
			.Ray( at.WithZ( 2000f ), at.WithZ( -4000f ) )
			.WithoutTags( Constants.PlayerTag, TestTag )
			.Run();

		return tr.Hit ? tr.HitPosition : null;
	}

	private static void ApplyCatalog( Scene scene, Vector3 center )
	{
		var placed = 0;

		for ( var i = 0; i < Catalog.Length; i++ )
		{
			var row = i / SlotsPerRow;
			var col = i % SlotsPerRow;
			var slot = center + new Vector3( row * SlotSpacing, (col - SlotsPerRow / 2) * SlotSpacing, 0f );

			if ( SpawnCatalogItem( scene, Catalog[i], slot ) )
			{
				placed++;
			}
		}

		// Spawn points ring the pad so respawns land on the moon.
		for ( var i = 0; i < SpawnPointCount; i++ )
		{
			var angle = i * (360f / SpawnPointCount);
			var offset = Rotation.FromYaw( angle ).Forward * (SlotSpacing * 1.5f);
			if ( SpawnCatalogItem( scene, SpawnPointPrefab, center - new Vector3( SlotSpacing, 0f, 0f ) + offset, liftAboveGround: 8f ) )
			{
				placed++;
			}
		}

		// Registers the new spawn points and un-gates any pending respawns.
		IGameEvents.Post( x => x.OnMapFitted() );

		Log.Info( $"lp_test_space: catalog applied — {placed} object(s) placed at {center}. lp_fit_clear removes them." );
	}

	private static bool SpawnCatalogItem( Scene scene, string prefabPath, Vector3 slot, float liftAboveGround = 2f )
	{
		var prefab = ResourceLibrary.Get<PrefabFile>( prefabPath );
		if ( prefab is null )
		{
			Log.Warning( $"lp_test_space: prefab not found '{prefabPath}'." );
			return false;
		}

		var ground = TraceGround( scene, slot ) ?? slot;
		var go = SceneUtility.GetPrefabScene( prefab ).Clone( new Transform( ground + Vector3.Up * liftAboveGround ) );
		go.Tags.Add( TestTag );

		// Prefab origins usually sit mid-model; seat the item so its bounds
		// rest on the ground instead of burying its lower half.
		var bounds = go.GetBounds();
		if ( bounds.Size.z > 1f )
		{
			var sink = ground.z + 1f - bounds.Mins.z;
			if ( sink > 0f )
			{
				go.WorldPosition += Vector3.Up * sink;
			}
		}

		if ( go.NetworkMode == NetworkMode.Object )
		{
			go.NetworkSpawn();
		}

		return true;
	}
}

/// <summary>
/// Per-session driver: runs the test-space tick once a second so fresh play
/// sessions launched with <c>+lp_test_space true</c> assemble themselves with
/// no console input. Dormant in the session where the class first hotloaded
/// (systems register at scene start) — <c>lp_fit_test</c> covers that case.
/// </summary>
public sealed class TestSpaceSystem( Scene scene ) : GameObjectSystem<TestSpaceSystem>( scene ), IGameEvents
{
	public void OnSecondlyUpdate()
	{
		if ( !Networking.IsActive )
		{
			return;
		}

		TestSpaceDev.Tick( Scene );
	}
}
