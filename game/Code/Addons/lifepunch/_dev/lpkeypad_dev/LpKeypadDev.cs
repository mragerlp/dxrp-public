// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). KEYPAD-1 U4 (cc-lp-2): editor-only commands for the driver,
// shaped on lpbanker's (_dev/lpbanker_dev/LpBankerHubDev.cs): spawn a keypad + a linked sample door beside the pawn, set
// the keypad's code, try a code, and (a fourth, so the panel can be proven) open the panel. The set and try commands run
// the entity's host cores directly (the same code the RPCs run, minus the transport and the reach gate), as the local
// player. Play-scene objects only; game.scene is never written.
// No money anywhere in this file (Law B). A code is never written to a log line.

#if !LIFEPUNCH_LOCAL

using System.Linq;
using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Keypad;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY.</summary>
public static class LpKeypadDev
{
	// Reason: one keypad and one linked sample door beside the pawn, for the set / try / fade proof. Who: the editor host only.
	[ConCmd( "lp_keypad_spawn" )]
	public static void Spawn()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_keypad_spawn: editor host only." );
			return;
		}

		if ( !TryInFrontOfLocal( out var position, out var facing ) )
		{
			Log.Warning( "lp_keypad_spawn: no local player - play from game.scene first." );
			return;
		}

		var doorModel = Model.Load( LpKeypadIdent.SampleDoorModelPath );
		if ( doorModel is null || doorModel.IsError )
		{
			Log.Warning( $"lp_keypad_spawn: sample door model missing ({LpKeypadIdent.SampleDoorModelPath})." );
			return;
		}

		var keypadGo = new GameObject( true, "lp_keypad" );
		keypadGo.Tags.Add( "lp_keypad_dev" );
		keypadGo.WorldPosition = position;
		keypadGo.WorldRotation = facing;
		var keypad = keypadGo.Components.Create<LpKeypadEntity>();
		keypad.BindOwnerFromLocalViewer();

		// The door stands a little beyond the keypad, inside the keypad's link radius, and is NOT a child of it.
		var doorGo = new GameObject( true, "lp_keypad_sample_door" );
		doorGo.Tags.Add( "lp_keypad_dev" );
		doorGo.WorldPosition = position + facing.Forward * 64f;
		doorGo.WorldRotation = facing;
		doorGo.Components.Create<ModelRenderer>().Model = doorModel;
		var collider = doorGo.Components.Create<BoxCollider>();
		collider.Center = doorModel.Bounds.Center;
		collider.Scale = doorModel.Bounds.Size;
		var fade = doorGo.Components.Create<LpFadingDoor>();

		keypadGo.NetworkSpawn();
		doorGo.NetworkSpawn();

		// Link as the owner would (the same call the Link door button's host core makes).
		var linkResult = keypad.LinkNearestCore( keypad.HubOwner );

		Log.Info( $"[lpkeypad] {LpKeypadIdent.SpawnMark} keypad={keypadGo.Id} door={doorGo.Id} owner={keypad.Owner} link={linkResult} fadeSeconds={fade.FadeSeconds:F1} codeSet={keypad.CodeIsSet} money=none" );
	}

	// Reason: set the keypad's code without the panel, to drive the proof. Who: the editor host only. The code is never logged.
	[ConCmd( "lp_keypad_set" )]
	public static void Set( string code = "" )
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_keypad_set: editor host only." );
			return;
		}

		var keypad = NearestKeypad();
		if ( !keypad.IsValid() )
		{
			Log.Warning( "lp_keypad_set: no keypad in the scene - run lp_keypad_spawn first." );
			return;
		}

		var callerId = Connection.Local.Id;
		var payload = keypad.SetCodeCore( callerId, SteamIdOf( callerId ), code );
		Log.Info( $"[lpkeypad] {LpKeypadIdent.SetMark} dev=lp_keypad_set payload={payload} message=\"{LpKeypadRules.Message( LpKeypadRules.KindSet, payload )}\"" );
	}

	// Reason: try a code without the panel, to drive the proof (correct, wrong x5, locked, fade, restore). Who: the editor host only.
	[ConCmd( "lp_keypad_try" )]
	public static void Try( string code = "" )
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_keypad_try: editor host only." );
			return;
		}

		var keypad = NearestKeypad();
		if ( !keypad.IsValid() )
		{
			Log.Warning( "lp_keypad_try: no keypad in the scene - run lp_keypad_spawn first." );
			return;
		}

		var payload = keypad.TryCodeCore( Connection.Local.Id, code );
		Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} dev=lp_keypad_try payload={payload} message=\"{LpKeypadRules.Message( LpKeypadRules.KindTry, payload )}\"" );
	}

	// Reason: deep link that opens the keypad panel for the proof (OS-synthesised clicks and use keys cannot reach panels).
	// A fourth command beyond the three the dispatch names; without it the panel cannot be proven. Who: the editor only.
	[ConCmd( "lp_keypad_open" )]
	public static void Open()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_keypad_open: editor only." );
			return;
		}

		var keypad = NearestKeypad();
		if ( !keypad.IsValid() )
		{
			Log.Warning( "lp_keypad_open: no keypad in the scene - run lp_keypad_spawn first." );
			return;
		}

		var canManage = keypad.CanManageHub( SteamIdOf( Connection.Local.Id ) );
		var panel = LpKeypadUi.Open( keypad, canManage );
		Log.Info( $"[lpkeypad] {LpKeypadIdent.UiMark} dev=lp_keypad_open keypad={keypad.GameObject.Id} canManage={canManage} panel={( panel.IsValid() ? panel.GameObject.Id.ToString() : "none" )}" );
	}

	/// <summary>The keypad nearest the local player; the first one when there is no local player.</summary>
	private static LpKeypadEntity NearestKeypad()
	{
		var keypads = Sandbox.Game.ActiveScene?.GetAllComponents<LpKeypadEntity>().Where( k => k.IsValid() ).ToList();
		if ( keypads is null || keypads.Count == 0 )
			return null;

		var player = Player.Local;
		if ( !player.IsValid() )
			return keypads[0];

		return keypads.OrderBy( k => ( k.GameObject.WorldPosition - player.WorldPosition ).Length ).First();
	}

	private static ulong SteamIdOf( System.Guid connectionId )
	{
		var player = GameUtils.GetPlayerByConnectionId( connectionId );
		return player.IsValid() ? (ulong)player.SteamId : 0UL;
	}

	/// <summary>A point in front of where the local player aims, and the aim's yaw (the house placement, as lpbanker's dev lane does it).</summary>
	private static bool TryInFrontOfLocal( out Vector3 position, out Rotation facing )
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
}

#endif
