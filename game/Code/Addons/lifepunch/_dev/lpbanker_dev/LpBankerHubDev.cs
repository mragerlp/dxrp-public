// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). DRIVE-AWAY-1-D (BANKER-HUB-1): deep links for the Banker
// hub skeleton proof (OS-synthesised clicks do not reach panels) and one cash-pile spawn beside the pawn.
// DRIVE-AWAY-2-U1 (BANKER-HUB-2): the pile builds its own body; a press link drives the pile's own Press, and a gate
// probe logs what the job gate would decide for the local player.
// SPINE-3 (SPINE-1 R1 U2): the probe is editor-only and also logs the shared hub gate verdicts for the spawned pile.

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

	/// <summary>Positive code-string ID for the aim-and-floor placement (DRIVE-AWAY-2-U4). Exists nowhere else in the tree.</summary>
	public const string PlacementMark = "LP_BANKER_DEV_PLACEMENT_20261017";

	/// <summary>
	/// QoL (DRIVE-AWAY-2-U4): a point in front of where the local player aims, and the aim's yaw. The market transform
	/// (LifePunchMarketSpawn.TryGetIdentitySpawnTransform) carries Rotation.Identity, so its offsets ran along world +X,
	/// out of view. The floor drop is the house LifePunchGroundContact.AlignMeshBottom.
	/// </summary>
	public static bool TryInFrontOfLocal( out Vector3 position, out Rotation facing )
	{
		var player = Player.Local;
		if ( !player.IsValid() )
		{
			position = default;
			facing = Rotation.Identity;
			return false;
		}

		var aim = player.AimRay;
		var flat = aim.Forward.WithZ( 0f );
		facing = flat.Length > 0.01f ? Rotation.LookAt( flat.Normal ) : Rotation.Identity;
		position = LifePunchMarketSpawn.GetSpawnPosition( aim );
		return true;
	}

	/// <summary>Positive code-string ID for the gate probe. Exists nowhere else in the tree.</summary>
	public const string GateProbeMark = "LP_BANKER_GATE_PROBE_20261017";

	/// <summary>Positive code-string ID for the shared-gate verdict line (SPINE-3). Exists nowhere else in the tree.</summary>
	public const string SharedGateProbeMark = "LP_BANKER_SHARED_GATE_PROBE_20261023";

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

		if ( !TryInFrontOfLocal( out var position, out var facing ) )
		{
			Log.Warning( "lp_banker_spawn_pile: no local player - play from game.scene first." );
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
		go.WorldPosition = position;
		go.WorldRotation = facing;
		go.Components.Create<ModelRenderer>().Model = model;
		go.Components.Create<LpBankerCashPileEntity>();
		var grounded = LifePunchGroundContact.AlignMeshBottom( go );

		var bounds = model.Bounds;
		Log.Info( $"[lpbanker] {PileSpawnMark} pile={go.Id} bounds={bounds.Size.x:F2}x{bounds.Size.y:F2}x{bounds.Size.z:F2} scale={LpBankerIdent.PileScaleNote} body=self pressable=yes money=none" );
		Log.Info( $"[lpbanker] {PlacementMark} pile placement=aim grounded={grounded} at={go.WorldPosition}" );
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

	// Reason: what the Banker job gate would decide for the local player while the editor seam is in force, and what the
	// shared hub gate decides for the spawned pile. Who: the editor only, read-only.
	[ConCmd( "lp_banker_gate_probe" )]
	public static void GateProbe()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_banker_gate_probe: editor only." );
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( Connection.Local.Id );
		var who = player.IsValid() ? player.DisplayName : "none";
		var job = player.IsValid() ? player.Job?.Id.ToString() : "none";
		Log.Info( $"[lpbanker] {GateProbeMark} jobGate={LpBankerAccess.JobGateEnabled} editorSeam={Application.IsEditor} tag={LpBankerAccess.BankerJobTag} player={who} job={job} isBanker={LpBankerAccess.IsBanker( player )} canUse={LpBankerAccess.CanUse( player )}" );

		// SPINE-3: the shared hub gate on the pile lp_banker_spawn_pile makes. pressGate is the client press check;
		// hostRange is the host reach tier the pile now uses; hostWhole is the whole gate, whose ownership permission
		// is expected to refuse this pile (no entity tag, no owner) - that reading is what this line is for.
		var pileObject = Game.ActiveScene?.GetAllObjects( true ).FirstOrDefault( g => g.Name == "lp_banker_cash_pile" );
		if ( !pileObject.IsValid() )
		{
			Log.Info( $"[lpbanker] {SharedGateProbeMark} pile=none - run lp_banker_spawn_pile first" );
			return;
		}

		var pressGate = LpHubAccess.CanPress( pileObject );
		var hostRange = LpHubAccess.CanOpenHostInRange( Connection.Local, pileObject );
		var hostWhole = LpHubAccess.CanOpenHost( Connection.Local, pileObject );
		Log.Info( $"[lpbanker] {SharedGateProbeMark} hubKey={LpBankerIdent.HubKey} pile={pileObject.Id} pressGate={pressGate} hostRange={hostRange} hostWhole={hostWhole}" );
	}
}

#endif
