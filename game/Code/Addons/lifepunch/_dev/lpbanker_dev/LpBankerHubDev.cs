// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). DRIVE-AWAY-1-D (BANKER-HUB-1): deep links for the Banker
// hub skeleton proof (OS-synthesised clicks do not reach panels) and one cash-pile spawn beside the pawn.

#if !LIFEPUNCH_LOCAL

using Sandbox;
using LifePunch.DXRP.Addons.Banker;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY. Play-scene objects only; game.scene is never written.</summary>
public static class LpBankerHubDev
{
	/// <summary>Positive code-string ID for the pile spawn. Exists nowhere else in the tree.</summary>
	public const string PileSpawnMark = "LP_BANKER_CASHPILE_SPAWN_20261017";

	// Reason: deep link that opens the Banker hub skeleton on a tab for the proof. Who: the editor only.
	[ConCmd( "lp_banker_hub" )]
	public static void OpenHub( int tab = 0 )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_banker_hub: editor only." );
			return;
		}

		var root = LpBankerHubUi.Open( tab );
		Log.Info( $"lp_banker_hub: open tab={tab} root={( root.IsValid() ? root.GameObject.Id.ToString() : "none" )}" );
	}

	// Reason: deep link that switches the open Banker hub to a tab (0 = Overview .. 6 = Logs). Who: the editor only.
	[ConCmd( "lp_banker_hub_tab" )]
	public static void HubTab( int tab )
	{
		if ( !Application.IsEditor )
			return;

		var root = LpBankerHubUi.Current;
		if ( !root.IsValid() )
		{
			Log.Warning( "lp_banker_hub_tab: no Banker hub open - run lp_banker_hub first." );
			return;
		}

		root.SelectTabIndex( tab );
		Log.Info( $"lp_banker_hub_tab: tab={tab}" );
	}

	// Reason: one Banker cash pile beside the pawn for the press and scale proof. Who: the editor host only.
	[ConCmd( "lp_banker_spawn_pile" )]
	public static void SpawnPile()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_banker_spawn_pile: editor host only." );
			return;
		}

		if ( !LifePunchMarketSpawn.TryGetIdentitySpawnTransform( out var transform ) )
		{
			Log.Warning( "lp_banker_spawn_pile: no local viewer - play from game.scene first." );
			return;
		}

		var model = Model.Load( LpBankerIdent.PileModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"lp_banker_spawn_pile: pile model missing ({LpBankerIdent.PileModelPath}) - land BANK-ASSETS-2 first." );
			return;
		}

		var go = new GameObject( true, "lp_banker_cash_pile" );
		go.Tags.Add( "lp_banker_dev" );
		go.WorldPosition = transform.Position + transform.Rotation.Forward * 60f + Vector3.Up * 40f;
		go.WorldRotation = transform.Rotation;

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		var bounds = model.Bounds;
		var collider = go.Components.Create<BoxCollider>();
		collider.Center = bounds.Center;
		collider.Scale = bounds.Size;
		collider.Static = true;
		go.Components.Create<LpBankerCashPileEntity>();

		Log.Info( $"[lpbanker] {PileSpawnMark} pile={go.Id} bounds={bounds.Size.x:F2}x{bounds.Size.y:F2}x{bounds.Size.z:F2} pressable=yes money=none" );
	}
}

#endif
