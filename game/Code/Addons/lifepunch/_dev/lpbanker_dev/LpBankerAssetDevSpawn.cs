// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). DRIVE-AWAY-1-C (BANK-ASSETS-2): lays the landed
// lpbanker meshes beside the local viewer for the scale proof and the collider audit. The vmdls carry no
// physics node, so the collider here is a BoxCollider sized from Model.Bounds, the bitcoin prefab pattern
// (bitcoinhub.prefab BoxCollider; LpBitcoinDevSpawn lp_bitcoin_scale_audit logs the same pair).

#if !LIFEPUNCH_LOCAL

using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY. Play-scene objects only; game.scene is never written.</summary>
public static class LpBankerAssetDevSpawn
{
	/// <summary>Positive code-string ID for the lineup. Exists nowhere else in the tree.</summary>
	public const string LineupMark = "LP_BANKER_ASSETS_LINEUP_20261017";

	public const string SessionTag = "lp_banker_dev";

	// The pawn's own mesh first, as the scale reference, then the bank set.
	private const string PawnReference = "models/citizen/citizen.vmdl";

	private static readonly string[] BankModels =
	{
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_01.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_02.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_03.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_04.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_05.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_06.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_bag_closed_01.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_bag_open_01.vmdl",
		"addons/lifepunch/lpbanker/bankreserve/assets/models/money_bag_open_full_01.vmdl",
		"addons/lifepunch/lpbanker/bankvault/assets/models/safe_vault.vmdl",
		"addons/lifepunch/lpbanker/bankvault/assets/models/safe_vault_door.vmdl",
	};

	// Reason: the bank asset scale proof needs every landed mesh in front of the pawn in one frame.
	// Who: the editor host only (dev lane); it spawns Play-scene props tagged lp_banker_dev and nothing else.
	[ConCmd( "lp_banker_spawn_lineup" )]
	public static void SpawnLineup()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_banker_spawn_lineup: editor host only." );
			return;
		}

		if ( !LifePunchMarketSpawn.TryGetIdentitySpawnTransform( out var transform ) )
		{
			Log.Warning( "lp_banker_spawn_lineup: no local viewer - play from game.scene first." );
			return;
		}

		var rot = transform.Rotation;
		var start = transform.Position + rot.Forward * 140f + rot.Left * 250f;
		var spawned = 0;

		Spawn( PawnReference, start, rot, "pawn_reference" );

		for ( var i = 0; i < BankModels.Length; i++ )
		{
			var position = start + rot.Right * ( 50f * ( i + 1 ) );
			if ( Spawn( BankModels[i], position, rot, System.IO.Path.GetFileNameWithoutExtension( BankModels[i] ) ) )
				spawned++;
		}

		Log.Info( $"[lpbanker] {LineupMark} models={spawned}/{BankModels.Length} reference=citizen" );
	}

	private static bool Spawn( string path, Vector3 position, Rotation rotation, string label )
	{
		var model = Model.Load( path );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[lpbanker] lineup {label}: model missing or error ({path})" );
			return false;
		}

		var go = new GameObject( true, "lp_banker_lineup_" + label );
		go.Tags.Add( SessionTag );
		go.WorldPosition = position;
		go.WorldRotation = rotation;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;

		var bounds = model.Bounds;
		var collider = go.Components.Create<BoxCollider>();
		collider.Center = bounds.Center;
		collider.Scale = bounds.Size;
		collider.Static = true;

		Log.Info( $"[lpbanker] lineup {label} mesh={bounds.Size.x:F2}x{bounds.Size.y:F2}x{bounds.Size.z:F2} center={bounds.Center.x:F2},{bounds.Center.y:F2},{bounds.Center.z:F2} collider={collider.Scale.x:F2}x{collider.Scale.y:F2}x{collider.Scale.z:F2}" );
		return true;
	}
}

#endif
