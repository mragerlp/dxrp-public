// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>DXRP money-printer style death VFX — clone explosion prefab + brief area damage.</summary>
public static class LifePunchMachineDestroyFx
{
	public const string DxrpExplosionPrefabPath = "prefabs/helpers/explosion.prefab";
	public const float DefaultExplosionDamage = 60f;

	/// <summary>Spawn printer-style explosion at <paramref name="position"/> (host only).</summary>
	public static void SpawnPrinterStyleExplosion( Component attacker, GameObject explosionPrefab, Vector3 position )
	{
#if LIFEPUNCH_LOCAL
		_ = attacker;
		_ = explosionPrefab;
		_ = position;
#else
		if ( !Networking.IsHost )
			return;

		var prefab = explosionPrefab;
		if ( !prefab.IsValid() )
			prefab = GameObject.GetPrefab( DxrpExplosionPrefabPath );

		if ( !prefab.IsValid() )
		{
			Log.Warning( $"LifePunchMachineDestroyFx: missing explosion prefab (wire property or '{DxrpExplosionPrefabPath}')." );
			return;
		}

		var explosion = prefab.Clone( position );
		explosion.NetworkSpawn();

		var areaDamage = explosion.AddComponent<AreaDamage>();
		areaDamage.Damage = DefaultExplosionDamage;
		areaDamage.Attacker = attacker;
		areaDamage.TimeLimit = 0.1f;
		areaDamage.DamageFlags = DamageFlags.Explosion;
#endif
	}
}
