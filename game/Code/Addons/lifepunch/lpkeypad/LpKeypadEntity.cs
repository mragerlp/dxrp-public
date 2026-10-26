// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// KEYPAD-1 U1 (cc-lp-2): the keypad. A DXRP entity (BaseEntity, so Owner binds and persists) that is pressable and is
// the first implementer of the hub kit's ILpHub (lifepunchcore/LpHubAccess.cs). NO MONEY: no wallet, no price, no
// value-moving RPC (Law B).
//
// Access, in two tiers (the dispatch's U1):
//   - Press / try a code: anyone in reach. Gate = LpHubAccess.CanOpenHostInRange (reach + not held), NOT CanOpenHost,
//     which needs ownership permission and would refuse every non-owner (LpHubAccess.cs:91-121).
//   - Set the code, link or unlink a door: the owner only. Gate = ILpHub.CanManageHub (LpHubAccess.DefaultCanManage),
//     and link/unlink additionally require a non-zero owner.
//
// The code is stored as a salted SHA-256 digest that is HOST-ONLY: [Property, ReadOnly] without [Sync] persists it in
// the snapshot and never replicates it (the hub-PIN shape: salted digest, host only; exemplar in the KEYPAD-1 return, F5).
// Wrong tries are throttled per caller on the host (LpKeypadRules; the throttle is keyed by connection id, so a
// reconnect resets it, as the hub-PIN throttle does). Results go back to the asking caller's open panel through
// LpHubResultRelay.

using System;
using System.Collections.Generic;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Keypad;

[Title( "Keypad" ), Category( "LifePunch" )]
public sealed class LpKeypadEntity : BaseEntity, Component.IPressable, ILpHub
{
	// A code exists. This synced flag reveals only "set / not set", never the digest, the salt or the length.
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public bool CodeIsSet { get; set; }

	// HOST-ONLY persisted: [Property] serialises into the snapshot; no [Sync], so it never replicates.
	[Property, ReadOnly] public string CodeDigest { get; set; } = string.Empty;
	[Property, ReadOnly] public string CodeSalt { get; set; } = string.Empty;

	// Host-only, transient (never persisted, never synced): wrong-try state per calling connection.
	private readonly Dictionary<Guid, LpKeypadRules.AttemptState> _attempts = new();

	public override string DisplayName => LpKeypadIdent.ProductName;

	// ---- ILpHub -----------------------------------------------------------------------------------------------------

	public string HubKey => LpKeypadIdent.HubKey;

	/// <summary>BaseEntity.Owner is a long Steam ID (0 = unowned); the hub kit's owner is a ulong.</summary>
	public ulong HubOwner => Owner > 0 ? (ulong)Owner : 0UL;

	/// <summary>The default hub policy: the owner; an unowned keypad with no code set by anyone; with a code set, only the owner.</summary>
	public bool CanManageHub( ulong callerSteamId )
		=> LpHubAccess.DefaultCanManage( HubOwner, callerSteamId, CodeIsSet, Networking.IsHost );

	// ---- Body -------------------------------------------------------------------------------------------------------

	protected override void OnStart()
	{
		base.OnStart();
		EnsureBody();
	}

	/// <summary>Adds the placeholder model and a static box collider when the object does not carry them already.</summary>
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

	public bool CanPress( IPressable.Event e ) => LpHubAccess.CanPress( GameObject );

	public bool Press( IPressable.Event e )
	{
		if ( !LpHubAccess.CanPress( GameObject ) )
			return false;

		OpenHost();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	[Rpc.Host]
	private void OpenHost()
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		OpenForCaller( callerId, CanManageHub( SteamIdOf( callerId ) ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void OpenForCaller( Guid callerId, bool canManage )
	{
		if ( Connection.Local is null || Connection.Local.Id != callerId )
			return;

		LpKeypadUi.Open( this, canManage );
	}

	// ---- Client requests (the panel calls these) ---------------------------------------------------------------------

	public void RequestTryCode( string code ) => TryCodeHost( code );
	public void RequestSetCode( string newCode ) => SetCodeHost( newCode );
	public void RequestLinkNearest() => LinkNearestHost();
	public void RequestUnlinkAll() => UnlinkAllHost();

	// ---- Host RPCs: gate, run the core, answer the asking caller ------------------------------------------------------

	[Rpc.Host]
	private void TryCodeHost( string code )
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		Reply( callerId, LpKeypadRules.KindTry, TryCodeCore( callerId, code ) );
	}

	[Rpc.Host]
	private void SetCodeHost( string newCode )
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		Reply( callerId, LpKeypadRules.KindSet, SetCodeCore( callerId, SteamIdOf( callerId ), newCode ) );
	}

	[Rpc.Host]
	private void LinkNearestHost()
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		Reply( callerId, LpKeypadRules.KindLink, LinkNearestCore( SteamIdOf( callerId ) ) );
	}

	[Rpc.Host]
	private void UnlinkAllHost()
	{
		if ( !LpHubAccess.CanOpenHostInRange( Rpc.Caller, GameObject ) )
			return;

		var callerId = Rpc.CallerId;
		Reply( callerId, LpKeypadRules.KindUnlink, UnlinkAllCore( SteamIdOf( callerId ) ) );
	}

	private void Reply( Guid callerId, string kind, string payload )
		=> LpHubResultRelay.SendToCaller( HubKey, callerId, kind, payload );

	// ---- Cores: host logic, no transport. The RPCs above and the editor dev commands both run these. ------------------

	/// <summary>Tries a code for one caller. Returns the payload (see LpKeypadRules.Compose). Host only.</summary>
	internal string TryCodeCore( Guid callerId, string code )
	{
		if ( !Networking.IsHost )
			return string.Empty;

		var now = Time.Now;
		var state = _attempts.TryGetValue( callerId, out var known ) ? known : default;

		if ( LpKeypadRules.IsLockedOut( state, now ) )
		{
			var remaining = LpKeypadRules.LockoutRemaining( state, now );
			Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} keypad={GameObject.Id} result=locked caller={callerId} remaining={remaining}" );
			return LpKeypadRules.Compose( LpKeypadRules.Locked, remaining );
		}

		if ( !CodeIsSet )
		{
			Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} keypad={GameObject.Id} result=nocode caller={callerId}" );
			return LpKeypadRules.Compose( LpKeypadRules.NoCode, 0 );
		}

		if ( LpKeypadRules.Matches( code, CodeSalt, CodeDigest ) )
		{
			_attempts.Remove( callerId );
			var doors = FadeLinkedDoors();
			Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} keypad={GameObject.Id} result=ok caller={callerId} doors={doors}" );
			return LpKeypadRules.Compose( LpKeypadRules.Ok, doors );
		}

		state = LpKeypadRules.RegisterFail( state, now, out var lockedNow );
		_attempts[callerId] = state;

		if ( lockedNow )
		{
			Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} keypad={GameObject.Id} result=locked caller={callerId} remaining={(int)LpKeypadRules.LockoutSeconds} onset=true" );
			return LpKeypadRules.Compose( LpKeypadRules.Locked, (int)LpKeypadRules.LockoutSeconds );
		}

		var left = LpKeypadRules.FailsLeft( state );
		Log.Info( $"[lpkeypad] {LpKeypadIdent.TryMark} keypad={GameObject.Id} result=wrong caller={callerId} failsLeft={left}" );
		return LpKeypadRules.Compose( LpKeypadRules.Wrong, left );
	}

	/// <summary>
	/// Sets the code. The owner may; an unowned keypad with no code may be set by anyone, and that first setter
	/// becomes its owner (WAIT: who may own). The code itself is never logged. Host only.
	/// </summary>
	internal string SetCodeCore( Guid callerId, ulong callerSteamId, string newCode )
	{
		if ( !Networking.IsHost )
			return string.Empty;

		if ( !CanManageHub( callerSteamId ) )
		{
			Log.Info( $"[lpkeypad] {LpKeypadIdent.SetMark} keypad={GameObject.Id} result=denied caller={callerId}" );
			return LpKeypadRules.Compose( LpKeypadRules.Denied, 0 );
		}

		if ( !LpKeypadRules.IsValidFormat( newCode ) )
		{
			Log.Info( $"[lpkeypad] {LpKeypadIdent.SetMark} keypad={GameObject.Id} result=badformat caller={callerId}" );
			return LpKeypadRules.Compose( LpKeypadRules.BadFormat, 0 );
		}

		if ( Owner == 0 && callerSteamId != 0 )
			Owner = (long)callerSteamId;

		CodeSalt = LpKeypadRules.NewSalt();
		CodeDigest = LpKeypadRules.Digest( newCode, CodeSalt );
		CodeIsSet = true;
		_attempts.Clear();

		Log.Info( $"[lpkeypad] {LpKeypadIdent.SetMark} keypad={GameObject.Id} result=ok digits={newCode.Length} owner={Owner} codeSet={CodeIsSet} code=hidden" );
		return LpKeypadRules.Compose( LpKeypadRules.Ok, newCode.Length );
	}

	/// <summary>Links the nearest door within reach to this keypad. Owner only. Host only.</summary>
	internal string LinkNearestCore( ulong callerSteamId )
	{
		if ( !Networking.IsHost )
			return string.Empty;

		if ( HubOwner == 0 || callerSteamId != HubOwner )
			return LpKeypadRules.Compose( LpKeypadRules.Denied, 0 );

		LpFadingDoor nearest = null;
		var nearestDistance = LpKeypadRules.LinkRadius;
		foreach ( var door in Scene.GetAllComponents<LpFadingDoor>() )
		{
			if ( !door.IsValid() )
				continue;

			// A door already claimed by someone else is not linkable.
			if ( door.LinkOwner != 0 && (ulong)door.LinkOwner != callerSteamId )
				continue;

			var distance = ( door.GameObject.WorldPosition - GameObject.WorldPosition ).Length;
			if ( distance > nearestDistance )
				continue;

			nearest = door;
			nearestDistance = distance;
		}

		if ( !nearest.IsValid() )
		{
			Log.Info( $"[lpkeypad] {LpKeypadIdent.LinkMark} keypad={GameObject.Id} result=nodoor radius={LpKeypadRules.LinkRadius}" );
			return LpKeypadRules.Compose( LpKeypadRules.NoDoor, 0 );
		}

		nearest.Link( GameObject.Id, (long)callerSteamId );
		Log.Info( $"[lpkeypad] {LpKeypadIdent.LinkMark} keypad={GameObject.Id} result=ok door={nearest.GameObject.Id} distance={nearestDistance:F1}" );
		return LpKeypadRules.Compose( LpKeypadRules.Ok, 0 );
	}

	/// <summary>Unlinks every door linked to this keypad. Owner only. Host only.</summary>
	internal string UnlinkAllCore( ulong callerSteamId )
	{
		if ( !Networking.IsHost )
			return string.Empty;

		if ( HubOwner == 0 || callerSteamId != HubOwner )
			return LpKeypadRules.Compose( LpKeypadRules.Denied, 0 );

		var count = 0;
		foreach ( var door in Scene.GetAllComponents<LpFadingDoor>() )
		{
			if ( !door.IsValid() || door.LinkedKeypad != GameObject.Id )
				continue;

			door.Unlink();
			count++;
		}

		Log.Info( $"[lpkeypad] {LpKeypadIdent.LinkMark} keypad={GameObject.Id} result=unlinked count={count}" );
		return LpKeypadRules.Compose( LpKeypadRules.Ok, count );
	}

	/// <summary>
	/// Fades every door linked to this keypad. A link only counts while the door's link owner is still this keypad's
	/// owner, so a stale link from before an ownership change does nothing.
	/// </summary>
	private int FadeLinkedDoors()
	{
		var faded = 0;
		foreach ( var door in Scene.GetAllComponents<LpFadingDoor>() )
		{
			if ( !door.IsValid() || door.LinkedKeypad != GameObject.Id || door.LinkOwner != Owner )
				continue;

			if ( door.FadeHost() )
				faded++;
		}

		return faded;
	}

	private static ulong SteamIdOf( Guid connectionId )
	{
		var player = GameUtils.GetPlayerByConnectionId( connectionId );
		return player.IsValid() ? (ulong)player.SteamId : 0UL;
	}
}
