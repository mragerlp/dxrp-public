// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U4b (cc-opus-4): the bank alarm. A pressable box that watches the vaults within its radius.
// NO MONEY (Law B): no balance, no price, no value-moving RPC. It holds a state and makes noise.
//
// A plain Component, NOT a BaseEntity: it is not an ISnapshot, so the host's world save never writes it (F49).
//
// State (LpBankAlarmRules.Decide) is host-authoritative and synced as an int.
//   - Arm / disarm / reset: a Banker or a Bank Guard (LpBankJobGate), by a press or from the Banker hub's Vault tab.
//   - Trip: an Armed alarm trips when a watched vault's door opens (ILpBankVaultEvent, posted by the vault on the
//     host) or when that vault's linked keypad locks a caller out after wrong codes (ILpKeypadEvent, posted by the
//     keypad on the host).
//   - While tripped every peer plays the siren at the alarm; the host sends one HUD notice that only Bank Guards
//     and police show (LpBankJobGate.IsAlarmAudience).

using System;
using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Keypad;

namespace LifePunch.DXRP.Addons.Banker;

[Title( "Bank Alarm" ), Category( "LifePunch" )]
public sealed class LpBankAlarmEntity : Component, Component.IPressable, ILpBankVaultEvent, ILpKeypadEvent
{
	/// <summary>Reach within which this alarm watches vaults.</summary>
	[Property] public float WatchRadius { get; set; } = LpBankAlarmRules.DefaultWatchRadius;

	/// <summary>The alarm state as its wire value (LpBankAlarmState). Written on the host only.</summary>
	[Sync( SyncFlags.FromHost )] public int StateRaw { get; set; }

	public LpBankAlarmState State => LpBankAlarmRules.FromRaw( StateRaw );

	// Per peer: whether the siren is playing here, and its handle.
	private bool _sirenOn;
	private SoundHandle _siren;

	// ---- Body -------------------------------------------------------------------------------------------------------

	protected override void OnStart()
	{
		EnsureBody();
	}

	/// <summary>Adds the PLACEHOLDER body (the core keypad box; the alarm's own model is owed) and a static box collider.</summary>
	private void EnsureBody()
	{
		var renderer = GameObject.GetComponent<ModelRenderer>();
		if ( renderer is null )
			renderer = GameObject.AddComponent<ModelRenderer>();

		if ( renderer.Model is null || renderer.Model.IsError )
			renderer.Model = Model.Load( LpKeypadIdent.PlaceholderModelPath );

		var model = renderer.Model;
		if ( model is null || model.IsError || GameObject.GetComponent<Collider>() is not null )
			return;

		var collider = GameObject.AddComponent<BoxCollider>();
		collider.Center = model.Bounds.Center;
		collider.Scale = model.Bounds.Size;
		collider.Static = true;
	}

	// ---- Press ------------------------------------------------------------------------------------------------------

	// Anyone in reach may press, so a player who is not bank staff is told why nothing happened (the host answers).
	public bool CanPress( IPressable.Event e ) => LpHubAccess.CanPress( GameObject );

	public bool Press( IPressable.Event e )
	{
		if ( !LpHubAccess.CanPress( GameObject ) )
			return false;

		PressHost();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	[Rpc.Host]
	private void PressHost()
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		var isStaff = LpBankJobGate.IsStaff( GameUtils.GetPlayerByConnectionId( callerId ) );
		var decision = RunCommandCore( LpBankAlarmRules.PressCommand( State ), isStaff, "press" );
		ReplyToCaller( callerId, decision.Accepted, decision.Reason );
	}

	// ---- Requests from the Banker hub's Vault tab --------------------------------------------------------------------

	/// <summary>Client side: asks the host for one command. The host decides; nothing changes here.</summary>
	public void RequestCommand( LpBankAlarmCommand command ) => CommandHost( (int)command );

	[Rpc.Host]
	private void CommandHost( int rawCommand )
	{
		// The two trip signals are host-internal. A client that sends one, or any other number, is dropped here.
		if ( !LpBankAlarmRules.IsClientCommand( rawCommand ) )
			return;

		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		var isStaff = LpBankJobGate.IsStaff( GameUtils.GetPlayerByConnectionId( callerId ) );
		var decision = RunCommandCore( (LpBankAlarmCommand)rawCommand, isStaff, "hub" );
		ReplyToCaller( callerId, decision.Accepted, decision.Reason );
	}

	/// <summary>Host to the one asking caller: the result line, on the HUD.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReplyToCaller( Guid callerId, bool accepted, string message )
	{
		if ( Connection.Local is null || Connection.Local.Id != callerId )
			return;

		if ( accepted )
			Notify.Info( message );
		else
			Notify.Warn( message );
	}

	/// <summary>Host to everyone; only a Bank Guard or an officer shows it. Sent once per trip.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void NoticeAudience( string message )
	{
		if ( !LpBankJobGate.IsAlarmAudience( Player.Local ) )
			return;

		Notify.Warn( message, LpBankAlarmRules.NoticeSeconds );
	}

	// ---- Trip signals (host only) ------------------------------------------------------------------------------------

	public void OnBankVaultDoorOpened( Guid vaultId, bool byKeypad )
	{
		if ( !Networking.IsHost || !Watches( vaultId ) )
			return;

		RunCommandCore( LpBankAlarmCommand.VaultDoorOpened, false, byKeypad ? "vault-keypad" : "vault" );
	}

	public void OnKeypadLockout( Guid keypadId, Guid callerId )
	{
		if ( !Networking.IsHost || keypadId == Guid.Empty )
			return;

		foreach ( var vault in Scene.GetAllComponents<LpBankVaultEntity>() )
		{
			if ( !vault.IsValid() || vault.LinkedKeypadIdHost != keypadId || !InRadius( vault ) )
				continue;

			RunCommandCore( LpBankAlarmCommand.KeypadLockout, false, "keypad-lockout" );
			return;
		}
	}

	private bool InRadius( LpBankVaultEntity vault )
		=> ( vault.GameObject.WorldPosition - GameObject.WorldPosition ).Length <= WatchRadius;

	private bool Watches( Guid vaultId )
	{
		foreach ( var vault in Scene.GetAllComponents<LpBankVaultEntity>() )
		{
			if ( vault.IsValid() && vault.GameObject.Id == vaultId )
				return InRadius( vault );
		}

		return false;
	}

	private bool AnyWatchedVaultOpen()
	{
		foreach ( var vault in Scene.GetAllComponents<LpBankVaultEntity>() )
		{
			if ( vault.IsValid() && vault.State == LpBankVaultState.Open && InRadius( vault ) )
				return true;
		}

		return false;
	}

	// ---- Core: host logic, no transport. The RPCs, the trip signals and the editor dev commands all run this. --------

	/// <summary>Runs one command through the rules and applies an accepted result. Host only.</summary>
	internal LpBankAlarmDecision RunCommandCore( LpBankAlarmCommand command, bool isStaff, string source )
	{
		var before = State;
		if ( !Networking.IsHost )
			return new LpBankAlarmDecision( false, before, string.Empty, false );

		var decision = LpBankAlarmRules.Decide( before, command, isStaff, AnyWatchedVaultOpen() );
		if ( decision.Accepted )
			StateRaw = (int)decision.State;

		Log.Info( $"[lpbanker] {LpBankVaultIdent.AlarmStateMark} alarm={GameObject.Id} source={source} command={command} staff={isStaff} accepted={decision.Accepted} from={before} to={decision.State} money=none" );

		if ( decision.Accepted && decision.TrippedNow )
		{
			Log.Info( $"[lpbanker] {LpBankVaultIdent.AlarmNoticeMark} alarm={GameObject.Id} cause={command} audience=bankguard+police" );
			NoticeAudience( decision.Reason );
		}

		return decision;
	}

	// ---- Siren (every peer, from the synced state) --------------------------------------------------------------------

	protected override void OnUpdate()
	{
		var tripped = State == LpBankAlarmState.Tripped;
		if ( tripped == _sirenOn )
			return;

		if ( tripped )
			StartSiren();
		else
			StopSiren();
	}

	protected override void OnDisabled()
	{
		StopSiren();
	}

	// World sound at the alarm (the Sound.Play( path, position, fade ) shape, LpBitcoinHubVisuals.cs:286). Whether the
	// placeholder event loops is not known from the tree: NEEDS EDITOR PROOF.
	private void StartSiren()
	{
		_sirenOn = true;
		_siren = Sound.Play( LpBankVaultIdent.AlarmSoundPath, WorldPosition, 0f );
	}

	private void StopSiren()
	{
		_sirenOn = false;
		if ( _siren.IsValid() )
			_siren.Stop( 0.2f );

		_siren = null;
	}
}
