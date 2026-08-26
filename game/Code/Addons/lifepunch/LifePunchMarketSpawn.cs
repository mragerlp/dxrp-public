// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch shared market spawn law — single source for DXRP PurchaseMarketItemHost parity.
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// DXRP market placement law: <c>GameUtils.GetSpawnPosition( player.AimRay )</c> + prefab
/// <c>Rotation.Identity</c>. Facing comes from vmdl <c>import_rotation</c> only — never dev yaw hacks.
/// </summary>
public static class LifePunchMarketSpawn
{
	/// <summary>Fallback when aim trace misses (ModelDoc / local-only).</summary>
	public const float FallbackSpawnDistanceUnits = 140f;

	/// <summary>Documented buffer on surface hits — matches DXRP market trace offset.</summary>
	public const float SurfaceBufferUnits = 30f;

	/// <summary>Canonical position for market purchases and LifePunch dev spawns on DXRP.</summary>
	public static Vector3 GetSpawnPosition( Ray aimRay )
	{
#if !LIFEPUNCH_LOCAL
		return GameUtils.GetSpawnPosition( aimRay );
#else
		return GetSpawnPositionLocalTrace( aimRay );
#endif
	}

	/// <summary>Market transform: resolved position + identity rotation (no prefab yaw).</summary>
	public static bool TryGetIdentitySpawnTransform( out Transform transform )
	{
#if !LIFEPUNCH_LOCAL
		var player = Player.Local;
		if ( !player.IsValid() )
		{
			transform = default;
			return false;
		}

		transform = new Transform( GetSpawnPosition( player.AimRay ), Rotation.Identity );
		return true;
#else
		var scene = Game.ActiveScene;
		var camera = scene?.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( !camera.IsValid() )
		{
			transform = default;
			return false;
		}

		var aimRay = new Ray( camera.WorldPosition, camera.WorldRotation.Forward );
		transform = new Transform( GetSpawnPosition( aimRay ), Rotation.Identity );
		return true;
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>Play-mode audit — proves dev spawn used the same aim ray + GameUtils path as market.</summary>
	public static void LogMarketSpawnAudit( string label )
	{
		var player = Player.Local;
		if ( !player.IsValid() )
		{
			Log.Warning( $"{label}: no local player — cannot audit market spawn." );
			return;
		}

		var aimRay = player.AimRay;
		var resolved = GetSpawnPosition( aimRay );
		var dist = Vector3.DistanceBetween( player.WorldPosition, resolved );
		Log.Info(
			$"{label} MARKET_SPAWN_AUDIT player={player.WorldPosition} aimOrigin={aimRay.Position} aimForward={aimRay.Forward} resolved={resolved} dist={dist:F1} rot=Identity (GameUtils.GetSpawnPosition)" );
	}
#endif

	static Vector3 GetSpawnPositionLocalTrace( Ray aimRay )
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return aimRay.Position + aimRay.Forward * FallbackSpawnDistanceUnits;

		var trace = scene.Trace.Ray( aimRay, 2048f )
			.WithoutTags( "player" )
			.Run();

		if ( trace.Hit )
			return trace.EndPosition + trace.Normal * SurfaceBufferUnits;

		return aimRay.Position + aimRay.Forward * FallbackSpawnDistanceUnits;
	}
}
