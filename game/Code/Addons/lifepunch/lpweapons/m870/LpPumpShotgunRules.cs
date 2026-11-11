// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────
using System;

namespace LifePunch.DXRP.Addons.Weapons.M870;

/// <summary>What the pump shotgun is doing, as far as the pump is concerned.</summary>
public enum LpPumpActivity
{
	/// <summary>Pump forward, ready to fire or reload.</summary>
	Ready,

	/// <summary>The pump cycle after a shot: the spent hull leaves on the back stroke.</summary>
	Cycling,

	/// <summary>Upstream shell-by-shell reload is running. The pump stays forward.</summary>
	Reloading,

	/// <summary>The closing pump after a reload: chambers a shell, throws no hull.</summary>
	Racking
}

/// <summary>Where the pump is inside one stroke.</summary>
public enum LpPumpPhase
{
	None,
	Lead,
	Back,
	Hold,
	Forward
}

/// <summary>What the component must do this tick.</summary>
[Flags]
public enum LpPumpEffect
{
	None = 0,
	PumpSound = 1,
	EjectHull = 2,
	StrokeDone = 4
}

/// <summary>Seconds of each part of one stroke.</summary>
public struct LpPumpTimings
{
	public float Lead;
	public float Back;
	public float Hold;
	public float Forward;

	public LpPumpTimings( float lead, float back, float hold, float forward )
	{
		Lead = Clean( lead );
		Back = Clean( back );
		Hold = Clean( hold );
		Forward = Clean( forward );
	}

	public float Total => Lead + Back + Hold + Forward;

	private static float Clean( float seconds )
	{
		return float.IsNaN( seconds ) || float.IsInfinity( seconds ) || seconds < 0f ? 0f : seconds;
	}
}

/// <summary>The whole pump state. A value: every rule returns a new one.</summary>
public struct LpPumpState
{
	public LpPumpActivity Activity;

	/// <summary>Seconds into the current stroke (Cycling or Racking).</summary>
	public float Elapsed;

	/// <summary>The stroke's sound (and hull) has been issued.</summary>
	public bool StrokeIssued;

	/// <summary>The running reload began with no shell in the gun.</summary>
	public bool ReloadStartedEmpty;
}

/// <summary>
/// The pump shotgun rules, with no engine types so they run in an offline harness. Upstream owns the
/// authority (ShootWeaponComponent decides whether a shot happens, ReloadWeaponComponent loads shells);
/// these rules decide only what the pump does about it and when firing is held (the "bolting" tag).
/// </summary>
public static class LpPumpShotgunRules
{
	/// <summary>A shot was fired. Ready starts the cycle; any other activity ignores it.</summary>
	public static LpPumpState OnShot( LpPumpState state )
	{
		if ( state.Activity != LpPumpActivity.Ready )
		{
			return state;
		}

		return new LpPumpState { Activity = LpPumpActivity.Cycling };
	}

	/// <summary>
	/// Upstream reload began. Ammo is the count at that moment: zero means the chamber is empty, because
	/// the pump after the last shot threw the hull. A stroke in progress is dropped (pump returns forward).
	/// </summary>
	public static LpPumpState OnReloadStarted( LpPumpState state, int ammo )
	{
		if ( state.Activity == LpPumpActivity.Reloading )
		{
			return state;
		}

		return new LpPumpState { Activity = LpPumpActivity.Reloading, ReloadStartedEmpty = ammo <= 0 };
	}

	/// <summary>
	/// Upstream reload ended (full, out of reserve, interrupted by fire, or holstered). With at least one
	/// shell in the gun, the pump closes the reload when it began empty (or always, when rackOnlyFromEmpty
	/// is off). With no shell there is nothing to chamber.
	/// </summary>
	public static LpPumpState OnReloadEnded( LpPumpState state, int ammo, bool rackAfterReload, bool rackOnlyFromEmpty )
	{
		if ( state.Activity != LpPumpActivity.Reloading )
		{
			return state;
		}

		var wantsRack = rackAfterReload && ammo > 0 && (state.ReloadStartedEmpty || !rackOnlyFromEmpty);
		return wantsRack ? BeginRack( state ) : default;
	}

	/// <summary>Start the closing pump. Used for the owner's broadcast so every client shows the same stroke.</summary>
	public static LpPumpState BeginRack( LpPumpState state )
	{
		return new LpPumpState { Activity = LpPumpActivity.Racking };
	}

	/// <summary>The weapon was holstered or dropped: pump forward, nothing pending.</summary>
	public static LpPumpState OnHolstered( LpPumpState state )
	{
		return default;
	}

	/// <summary>Advance time. Issues the stroke's sound once (and the hull, for a post-shot cycle) at the end of the lead.</summary>
	public static LpPumpState Tick( LpPumpState state, float deltaSeconds, LpPumpTimings timings, out LpPumpEffect effects )
	{
		effects = LpPumpEffect.None;

		if ( !IsStroking( state ) )
		{
			return state;
		}

		if ( float.IsNaN( deltaSeconds ) || deltaSeconds < 0f )
		{
			deltaSeconds = 0f;
		}

		state.Elapsed += deltaSeconds;

		if ( !state.StrokeIssued && state.Elapsed >= timings.Lead )
		{
			state.StrokeIssued = true;
			effects |= LpPumpEffect.PumpSound;

			if ( state.Activity == LpPumpActivity.Cycling )
			{
				effects |= LpPumpEffect.EjectHull;
			}
		}

		if ( state.Elapsed >= timings.Total )
		{
			effects |= LpPumpEffect.StrokeDone;
			return default;
		}

		return state;
	}

	/// <summary>True while a stroke runs: firing and reload start are held (the "bolting" tag).</summary>
	public static bool BlocksFire( LpPumpState state )
	{
		return IsStroking( state );
	}

	public static bool IsStroking( LpPumpState state )
	{
		return state.Activity == LpPumpActivity.Cycling || state.Activity == LpPumpActivity.Racking;
	}

	public static LpPumpPhase PhaseOf( LpPumpState state, LpPumpTimings timings )
	{
		if ( !IsStroking( state ) )
		{
			return LpPumpPhase.None;
		}

		var t = state.Elapsed;
		if ( t < timings.Lead )
		{
			return LpPumpPhase.Lead;
		}

		t -= timings.Lead;
		if ( t < timings.Back )
		{
			return LpPumpPhase.Back;
		}

		t -= timings.Back;
		if ( t < timings.Hold )
		{
			return LpPumpPhase.Hold;
		}

		return LpPumpPhase.Forward;
	}

	/// <summary>How far back the pump is: 0 forward, 1 fully back.</summary>
	public static float SlideFraction( LpPumpState state, LpPumpTimings timings )
	{
		if ( !IsStroking( state ) )
		{
			return 0f;
		}

		var t = state.Elapsed - timings.Lead;
		if ( t <= 0f )
		{
			return 0f;
		}

		if ( t < timings.Back )
		{
			return t / timings.Back;
		}

		t -= timings.Back;
		if ( t < timings.Hold )
		{
			return 1f;
		}

		t -= timings.Hold;
		if ( timings.Forward <= 0f || t >= timings.Forward )
		{
			return 0f;
		}

		return 1f - t / timings.Forward;
	}

	/// <summary>
	/// The longest stroke the host fire floor covers: a semi-only weapon's next shot is accepted after
	/// fireDelay * LpPumpShotgunSpec.HostFireFloorScale. A stroke longer than this could be shot through.
	/// </summary>
	public static bool StrokeFitsFireDelay( LpPumpTimings timings, float fireDelay )
	{
		return timings.Total <= fireDelay * LpPumpShotgunSpec.HostFireFloorScale + 0.0001f;
	}

	/// <summary>
	/// Upstream fact, stated for the harness: firing during a single reload queues a cancel and the shell
	/// in progress still lands (ReloadWeaponComponent.cs:82-85, :233-241), so an interrupted reload always
	/// ends with at least one shell whenever reserve was available.
	/// </summary>
	public static int AmmoAfterInterruptedInsert( int ammoBeforeInsert, int reserve, int capacity )
	{
		if ( reserve <= 0 || ammoBeforeInsert >= capacity )
		{
			return ammoBeforeInsert;
		}

		return ammoBeforeInsert + 1;
	}
}
