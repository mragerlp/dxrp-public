// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Weapons.M870;

/// <summary>
/// Slides the pump mesh. Put it on the object that renders the pump (vm_m870: m870_pump).
/// LpPumpShotgunCycle finds it and feeds it a fraction each frame: 0 = forward (the authored rest
/// position), 1 = fully back. Visual only; it moves one local transform on this client.
/// </summary>
[Title( "Pump Shotgun - Slide Visual" )]
[Icon( "swap_horiz" )]
[Group( "Weapon Components" )]
public sealed class LpPumpSlideVisual : Component
{
	/// <summary>
	/// Where the pump sits when fully back, as an offset from its rest position in the parent's space.
	/// The m870 mesh points +X at the muzzle, so back is -X. Units are Source inches.
	/// </summary>
	[Property]
	public Vector3 StrokeOffset { get; set; } = new( -3.5f, 0f, 0f );

	private Vector3 _rest;
	private bool _hasRest;

	protected override void OnStart()
	{
		CaptureRest();
	}

	public void SetFraction( float fraction )
	{
		CaptureRest();
		LocalPosition = _rest + StrokeOffset * Math.Clamp( fraction, 0f, 1f );
	}

	private void CaptureRest()
	{
		if ( _hasRest )
		{
			return;
		}

		_rest = LocalPosition;
		_hasRest = true;
	}
}
