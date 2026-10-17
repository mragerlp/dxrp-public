// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). DRIVE-AWAY-1-D (BANKER-HUB-1): deep links for the Banker
// hub skeleton proof (OS-synthesised clicks do not reach panels) and one cash-pile spawn beside the pawn.
// DRIVE-AWAY-2-U1 (BANKER-HUB-2): the pile builds its own body; a press link drives the pile's own Press, and a gate
// probe logs what the job gate would decide for the local player.

#if !LIFEPUNCH_LOCAL

using System.Linq;
using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Banker;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY. Play-scene objects only; game.scene is never written.</summary>
public static class LpBankerHubDev
{
	/// <summary>Positive code-string ID for the pile spawn. Exists nowhere else in the tree.</summary>
	public const string PileSpawnMark = "LP_BANKER_CASHPILE_SPAWN_20261017";

	/// <summary>Positive code-string ID for the press proof. Exists nowhere else in the tree.</summary>
	public const string PilePressMark = "LP_BANKER_PILE_PRESS_20261017";

	/// <summary>Positive code-string ID for the gate probe. Exists nowhere else in the tree.</summary>
	public const string GateProbeMark = "LP_BANKER_GATE_PROBE_20261017";

	// Reason: deep link that opens the Banker hub on a tab for the proof. Who: the editor only.
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

		if ( tab < 0 || tab > 6 )
		{
			Log.Warning( $"lp_banker_hub_tab: tab {tab} is out of range - use 0 (Overview) to 6 (Logs)." );
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
		go.Components.Create<LpBankerCashPileEntity>();

		var bounds = model.Bounds;
		Log.Info( $"[lpbanker] {PileSpawnMark} pile={go.Id} bounds={bounds.Size.x:F2}x{bounds.Size.y:F2}x{bounds.Size.z:F2} scale={LpBankerIdent.PileScaleNote} body=self pressable=yes money=none" );
	}

	// Reason: proof that the pile's own Press opens the hub (OS clicks and use keys cannot be synthesised). Who: the editor only.
	[ConCmd( "lp_banker_press_pile" )]
	public static void PressPile()
	{
		if ( !Application.IsEditor )
			return;

		var pile = Game.ActiveScene?.GetAllComponents<LpBankerCashPileEntity>().FirstOrDefault();
		if ( !pile.IsValid() )
		{
			Log.Warning( "lp_banker_press_pile: no cash pile in the scene - run lp_banker_spawn_pile first." );
			return;
		}

		var canPress = pile.CanPress( default );
		var pressed = canPress && pile.Press( default );
		var hasBody = pile.GameObject.GetComponent<ModelRenderer>() is { Model: not null } && pile.GameObject.GetComponent<Collider>() is not null;
		Log.Info( $"[lpbanker] {PilePressMark} pile={pile.GameObject.Id} body={hasBody} canPress={canPress} pressed={pressed} hubOpen={LpBankerHubUi.Current.IsValid()}" );
	}

	// Reason: what the Banker job gate would decide for the local player while the editor seam is in force. Who: anyone, read-only.
	[ConCmd( "lp_banker_gate_probe" )]
	public static void GateProbe()
	{
		var player = GameUtils.GetPlayerByConnectionId( Connection.Local.Id );
		var who = player.IsValid() ? player.DisplayName : "none";
		var job = player.IsValid() ? player.Job?.Id.ToString() : "none";
		Log.Info( $"[lpbanker] {GateProbeMark} jobGate={LpBankerAccess.JobGateEnabled} editorSeam={Application.IsEditor} tag={LpBankerAccess.BankerJobTag} player={who} job={job} isBanker={LpBankerAccess.IsBanker( player )} canUse={LpBankerAccess.CanUse( player )}" );
	}
}

#endif
