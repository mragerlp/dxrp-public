// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// KEYPAD-1 U2 (cc-lp-2): the fading door. A component put on a door or prop object. When its linked keypad accepts a
// code, the host marks the door faded for N seconds; every peer then turns the object's colliders off and its model
// tint translucent, and turns them back when the host clears the flag. NO MONEY (Law B).
//
// Link: owner-only. LinkedKeypad + LinkOwner are written by the keypad's host core (LpKeypadEntity.LinkNearestCore),
// which has already checked the caller is the keypad's owner and standing in reach (CanOpenHostInRange). A fade is
// requested only by a keypad whose id matches LinkedKeypad and whose owner matches LinkOwner.
//
// Keep the keypad OUT of this object's hierarchy: the fade walks self and descendants, so a child keypad would fade too.

using System;
using System.Collections.Generic;
using Sandbox;

namespace LifePunch.DXRP.Addons.Keypad;

[Title( "Fading Door" ), Category( "LifePunch" )]
public sealed class LpFadingDoor : Component
{
	/// <summary>GameObject id of the keypad this door answers to; Guid.Empty = unlinked.</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public Guid LinkedKeypad { get; set; }

	/// <summary>Steam ID (long, like BaseEntity.Owner) of whoever linked the door; 0 = unlinked.</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public long LinkOwner { get; set; }

	/// <summary>How long the door stays faded. Clamped to LpKeypadRules.MinFadeSeconds..MaxFadeSeconds when used.</summary>
	[Property] public float FadeSeconds { get; set; } = LpKeypadRules.DefaultFadeSeconds;

	/// <summary>True while the door is faded. Written on the host only; read by every peer.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsFaded { get; set; }

	// Host-only: when the fade ends.
	private float _restoreAt;

	// Per peer: what the last Apply left in place, and the parts it acts on.
	private bool _applied;
	private readonly List<Collider> _colliders = new();
	private readonly List<(ModelRenderer Renderer, Color Tint)> _renderers = new();

	protected override void OnStart()
	{
		CacheParts();
	}

	private void CacheParts()
	{
		_colliders.Clear();
		_renderers.Clear();

		foreach ( var collider in GameObject.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			_colliders.Add( collider );

		// The original tint is kept per renderer so a restore puts back exactly what was there.
		foreach ( var renderer in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			_renderers.Add( (renderer, renderer.Tint) );
	}

	/// <summary>Host only. Links the door to a keypad and records who linked it.</summary>
	public void Link( Guid keypadId, long owner )
	{
		if ( !Networking.IsHost )
			return;

		LinkedKeypad = keypadId;
		LinkOwner = owner;
	}

	/// <summary>Host only. Clears the link and restores the door at once.</summary>
	public void Unlink()
	{
		if ( !Networking.IsHost )
			return;

		LinkedKeypad = Guid.Empty;
		LinkOwner = 0;
		if ( IsFaded )
			IsFaded = false;
	}

	/// <summary>
	/// Host only. Starts (or extends) the fade. Returns false when this is not the host. A second correct code during a
	/// fade restarts the timer; it does not stack.
	/// </summary>
	public bool FadeHost()
	{
		if ( !Networking.IsHost )
			return false;

		var seconds = LpKeypadRules.ClampFade( FadeSeconds );
		_restoreAt = Time.Now + seconds;
		IsFaded = true;
		Log.Info( $"[lpkeypad] {LpKeypadIdent.FadeMark} door={GameObject.Id} keypad={LinkedKeypad} seconds={seconds:F1} colliders={_colliders.Count} renderers={_renderers.Count}" );
		return true;
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost && IsFaded && Time.Now >= _restoreAt )
		{
			IsFaded = false;
			Log.Info( $"[lpkeypad] {LpKeypadIdent.RestoreMark} door={GameObject.Id} keypad={LinkedKeypad}" );
		}

		if ( IsFaded != _applied )
			Apply( IsFaded );
	}

	/// <summary>
	/// Turns the object non-solid and translucent, or back. Runs on every peer from the synced flag. A model whose
	/// material cannot blend alpha will stay opaque while faded (NEEDS EDITOR PROOF); the collision half does not
	/// depend on the material.
	/// </summary>
	private void Apply( bool faded )
	{
		foreach ( var collider in _colliders )
		{
			if ( collider.IsValid() )
				collider.Enabled = !faded;
		}

		foreach ( var (renderer, tint) in _renderers )
		{
			if ( renderer.IsValid() )
				renderer.Tint = faded ? tint.WithAlpha( LpKeypadRules.FadedAlpha ) : tint;
		}

		_applied = faded;
	}
}
