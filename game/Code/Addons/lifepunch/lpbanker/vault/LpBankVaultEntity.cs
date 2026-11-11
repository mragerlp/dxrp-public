// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U3 (cc-opus-4): the bank vault. Pressable, and an implementer of the hub kit's ILpHub
// (lifepunchcore/LpHubAccess.cs). It carries the landed safe_vault body and safe_vault_door door (BANK-ASSETS-2).
//
// NO MONEY (Law B): no balance, no synced amount, no price, no value-moving RPC. The door has a state and that is
// all. What the vault holds is fixture text on the Banker hub's Vault tab (LpBankVaultRules.FixtureContents).
//
// A plain Component, NOT a BaseEntity: it is not an ISnapshot, so the host's world save never writes it (F49). A
// vault that must persist is a later slice and a ruling.
//
// Door state (LpBankVaultRules.Decide) is host-authoritative and synced as an int. It changes three ways:
//   - a press: anyone in reach may press; the host opens or closes only for a Banker (LpBankJobGate);
//   - a request from the Banker hub's Vault tab (open, close, lock, unlock), gated the same way on the host;
//   - the linked keypad: the vault carries a hidden LpFadingDoor child as the link target. The keypad's owner links
//     it with the keypad's own "Link door" (LpKeypadEntity.LinkNearestCore); a correct code fades that child
//     (LpFadingDoor.FadeHost), and the host reads the rising edge here. lpkeypad is not edited for this.
//
// Open = the door goes non-solid and translucent on every peer (the LpFadingDoor presentation), or swings by
// OpenYawDegrees when that is set. The model's pivot is the safe's origin, not the hinge, so the swing is off by
// default until a hinge offset is measured in the editor.

using System;
using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Keypad;

namespace LifePunch.DXRP.Addons.Banker;

[Title( "Bank Vault" ), Category( "LifePunch" )]
public sealed class LpBankVaultEntity : Component, Component.IPressable, ILpHub
{
	/// <summary>Body model; empty = the landed safe_vault.</summary>
	[Property] public Model BodyModel { get; set; }

	/// <summary>Door model; empty = the landed safe_vault_door.</summary>
	[Property] public Model DoorModel { get; set; }

	/// <summary>Yaw the open door swings to, in degrees. 0 = no swing: the door fades instead (the default).</summary>
	[Property] public float OpenYawDegrees { get; set; }

	/// <summary>The door state as its wire value (LpBankVaultState). Written on the host only.</summary>
	[Sync( SyncFlags.FromHost )] public int StateRaw { get; set; }

	/// <summary>Whether a keypad is linked to this vault. Written on the host only; the Vault tab reads it.</summary>
	[Sync( SyncFlags.FromHost )] public bool KeypadLinked { get; set; }

	public LpBankVaultState State => LpBankVaultRules.FromRaw( StateRaw );

	// Host-only: the hidden link target and the last fade reading.
	private LpFadingDoor _keypadLink;
	private bool _linkWasFaded;

	// Per peer: the door parts and what the last ApplyDoor left in place.
	private GameObject _door;
	private ModelRenderer _doorRenderer;
	private Collider _doorCollider;
	private Color _doorTint;
	private Rotation _doorClosedRotation;
	private bool _doorCached;
	private bool _appliedOpen;

	// ---- ILpHub -----------------------------------------------------------------------------------------------------

	public string HubKey => LpBankVaultIdent.HubKey;

	/// <summary>The bank is an institution: the vault has no owning player.</summary>
	public ulong HubOwner => 0UL;

	/// <summary>Managing the vault is the Banker's. A caller that did not resolve to a player counts as the host.</summary>
	public bool CanManageHub( ulong callerSteamId )
		=> callerSteamId == 0 ? Networking.IsHost : LpBankJobGate.IsBanker( GameUtils.GetPlayerById( (long)callerSteamId ) );

	// ---- Body -------------------------------------------------------------------------------------------------------

	protected override void OnStart()
	{
		EnsureBody();
		if ( Networking.IsHost )
			EnsureKeypadLinkHost();
	}

	/// <summary>Adds the body model, a static box collider and the door child when the object does not carry them.</summary>
	private void EnsureBody()
	{
		var renderer = GameObject.GetComponent<ModelRenderer>();
		if ( renderer is null )
			renderer = GameObject.AddComponent<ModelRenderer>();

		if ( renderer.Model is null || renderer.Model.IsError )
			renderer.Model = BodyModel ?? Model.Load( LpBankVaultIdent.BodyModelPath );

		var model = renderer.Model;
		if ( model is not null && !model.IsError && GameObject.GetComponent<Collider>() is null )
		{
			var collider = GameObject.AddComponent<BoxCollider>();
			collider.Center = model.Bounds.Center;
			collider.Scale = model.Bounds.Size;
			collider.Static = true;
		}

		EnsureDoor();
	}

	private void EnsureDoor()
	{
		_door = FindChild( LpBankVaultIdent.DoorObjectName );
		if ( !_door.IsValid() )
		{
			// Child-object shape: WeaponAttachmentRenderController.cs:235 (new GameObject( parent, true, name )).
			_door = new GameObject( GameObject, true, LpBankVaultIdent.DoorObjectName );
			_door.LocalPosition = Vector3.Zero;
			_door.LocalRotation = Rotation.Identity;
		}

		_doorRenderer = _door.GetComponent<ModelRenderer>();
		if ( _doorRenderer is null )
			_doorRenderer = _door.AddComponent<ModelRenderer>();

		if ( _doorRenderer.Model is null || _doorRenderer.Model.IsError )
			_doorRenderer.Model = DoorModel ?? Model.Load( LpBankVaultIdent.DoorModelPath );

		_doorCollider = _door.GetComponent<Collider>();
		var doorModel = _doorRenderer.Model;
		if ( _doorCollider is null && doorModel is not null && !doorModel.IsError )
		{
			var box = _door.AddComponent<BoxCollider>();
			box.Center = doorModel.Bounds.Center;
			box.Scale = doorModel.Bounds.Size;
			box.Static = true;
			_doorCollider = box;
		}

		_doorTint = _doorRenderer.Tint;
		_doorClosedRotation = _door.LocalRotation;
		_doorCached = true;
	}

	/// <summary>
	/// Host only, idempotent. Makes the hidden child the keypad links to: an LpFadingDoor on an object with no model
	/// and no collider, so its own fade changes nothing that can be seen or walked through. The dev spawn calls this
	/// before it links, in case OnStart has not run yet.
	/// </summary>
	internal LpFadingDoor EnsureKeypadLinkHost()
	{
		if ( !Networking.IsHost )
			return null;

		if ( _keypadLink.IsValid() )
			return _keypadLink;

		var linkObject = FindChild( LpBankVaultIdent.KeypadLinkObjectName );
		if ( !linkObject.IsValid() )
		{
			linkObject = new GameObject( GameObject, true, LpBankVaultIdent.KeypadLinkObjectName );
			linkObject.LocalPosition = Vector3.Zero;
		}

		_keypadLink = linkObject.GetComponent<LpFadingDoor>();
		if ( _keypadLink is null )
			_keypadLink = linkObject.AddComponent<LpFadingDoor>();

		return _keypadLink;
	}

	private GameObject FindChild( string name )
	{
		foreach ( var child in GameObject.Children )
		{
			if ( child.IsValid() && child.Name == name )
				return child;
		}

		return null;
	}

	/// <summary>Host only. GameObject id of the keypad linked to this vault; Guid.Empty when none is.</summary>
	internal Guid LinkedKeypadIdHost => _keypadLink.IsValid() ? _keypadLink.LinkedKeypad : Guid.Empty;

	// ---- Press ------------------------------------------------------------------------------------------------------

	// Anyone in reach may press, so a player who is not the Banker is told why nothing happened (the host answers).
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
		// The in-range tier, not CanOpenHost: the whole gate's ownership permission refuses an object with no entity
		// tag and no owner, and the vault is one (LpHubAccess.cs:86-121; the cash pile makes the same choice).
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		var decision = RunCommandCore( LpBankVaultRules.PressCommand( State ), LpBankJobGate.IsBanker( player ), LpBankJobGate.IsGuard( player ), "press" );
		ReplyToCaller( callerId, decision.Accepted, decision.Reason );
	}

	// ---- Requests from the Banker hub's Vault tab --------------------------------------------------------------------

	/// <summary>Client side: asks the host for one command. The host decides; nothing changes here.</summary>
	public void RequestCommand( LpBankVaultCommand command ) => CommandHost( (int)command );

	[Rpc.Host]
	private void CommandHost( int rawCommand )
	{
		// The keypad signal is host-internal. A client that sends it, or any other number, is dropped here.
		if ( !LpBankVaultRules.IsClientCommand( rawCommand ) )
			return;

		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		var decision = RunCommandCore( (LpBankVaultCommand)rawCommand, LpBankJobGate.IsBanker( player ), LpBankJobGate.IsGuard( player ), "hub" );
		ReplyToCaller( callerId, decision.Accepted, decision.Reason );
	}

	/// <summary>Host to the one asking caller: the result line, on the HUD (the OpenForCaller filter shape, LpKeypadEntity.cs:106-113).</summary>
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

	// ---- Core: host logic, no transport. The RPCs above, the keypad edge and the editor dev commands all run this. ----

	/// <summary>Runs one command through the rules and applies an accepted result. Host only.</summary>
	internal LpBankVaultDecision RunCommandCore( LpBankVaultCommand command, bool isBanker, bool isGuard, string source )
	{
		var before = State;
		if ( !Networking.IsHost )
			return new LpBankVaultDecision( false, before, string.Empty, false );

		var decision = LpBankVaultRules.Decide( before, command, isBanker, isGuard );
		if ( decision.Accepted )
			StateRaw = (int)decision.State;

		Log.Info( $"[lpbanker] {LpBankVaultIdent.StateMark} vault={GameObject.Id} source={source} command={command} banker={isBanker} guard={isGuard} accepted={decision.Accepted} from={before} to={decision.State} money=none" );

		if ( decision.Accepted && decision.DoorOpened )
		{
			var vaultId = GameObject.Id;
			var byKeypad = command == LpBankVaultCommand.KeypadAccepted;
			ILpBankVaultEvent.Post( x => x.OnBankVaultDoorOpened( vaultId, byKeypad ) );
		}

		return decision;
	}

	// ---- Update -----------------------------------------------------------------------------------------------------

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			ReadKeypadLinkHost();

		var open = State == LpBankVaultState.Open;
		if ( _doorCached && open != _appliedOpen )
			ApplyDoor( open );
	}

	/// <summary>
	/// Host only. The linked keypad accepted a code when the link child's fade flag rises. A second correct code
	/// inside the same fade window restarts the keypad's timer and raises no new edge, so it is not a second signal.
	/// </summary>
	private void ReadKeypadLinkHost()
	{
		if ( !_keypadLink.IsValid() )
			return;

		var linked = _keypadLink.LinkedKeypad != Guid.Empty;
		if ( linked != KeypadLinked )
			KeypadLinked = linked;

		var faded = _keypadLink.IsFaded;
		if ( faded && !_linkWasFaded )
			RunCommandCore( LpBankVaultCommand.KeypadAccepted, false, false, "keypad" );

		_linkWasFaded = faded;
	}

	/// <summary>
	/// Runs on every peer from the synced state: the open door is non-solid, and either translucent or swung. A model
	/// whose material cannot blend alpha stays opaque while open (NEEDS EDITOR PROOF); the collision half does not
	/// depend on the material.
	/// </summary>
	private void ApplyDoor( bool open )
	{
		if ( _doorCollider.IsValid() )
			_doorCollider.Enabled = !open;

		if ( OpenYawDegrees != 0f )
		{
			if ( _door.IsValid() )
				_door.LocalRotation = open ? _doorClosedRotation * Rotation.FromYaw( OpenYawDegrees ) : _doorClosedRotation;
		}
		else if ( _doorRenderer.IsValid() )
		{
			_doorRenderer.Tint = open ? _doorTint.WithAlpha( LpKeypadRules.FadedAlpha ) : _doorTint;
		}

		_appliedOpen = open;
		Log.Info( $"[lpbanker] {LpBankVaultIdent.DoorMark} vault={GameObject.Id} open={open} style={( OpenYawDegrees != 0f ? "swing" : "fade" )}" );
	}
}
