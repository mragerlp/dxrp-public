// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using Dxura.RP.Game;
using LifePunch.DXRP.Addons.Weapons.M870;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// Editor-only driver commands for the pump shotgun's cycle component, so each part can be proven on
/// its own without firing. All three act on the local player's current weapon and refuse outside the
/// editor. None changes ammo, damage, money or saved state.
/// </summary>
public static class M870PumpDev
{
	/// <summary>Why: prove the pump stroke, its sound and its hull without spending a shell. Editor only.</summary>
	[ConCmd( "lp_m870_pump" )]
	public static void Pump()
	{
		var cycle = Resolve( "lp_m870_pump" );
		if ( cycle is null )
		{
			return;
		}

		cycle.DevForceCycle();
		Log.Info( $"lp_m870_pump: one cycle started; {cycle.DevDescribe()}" );
	}

	/// <summary>Why: prove the hull effect and the ejection port position on their own. Editor only.</summary>
	[ConCmd( "lp_m870_eject" )]
	public static void Eject()
	{
		var cycle = Resolve( "lp_m870_eject" );
		if ( cycle is null )
		{
			return;
		}

		cycle.DevEjectHull();
		Log.Info( $"lp_m870_eject: one {LpPumpShotgunSpec.GaugeLabel} hull requested; {cycle.DevDescribe()}" );
	}

	/// <summary>Why: read the pump state machine while firing and reloading. Editor only, read-only.</summary>
	[ConCmd( "lp_m870_reload_state" )]
	public static void ReloadState()
	{
		var cycle = Resolve( "lp_m870_reload_state" );
		if ( cycle is null )
		{
			return;
		}

		Log.Info( $"lp_m870_reload_state: {LpPumpShotgunSpec.DisplayName} {cycle.DevDescribe()}" );
	}

	private static LpPumpShotgunCycle? Resolve( string command )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"{command}: editor-only." );
			return null;
		}

		var player = Player.Local;
		if ( !player.IsValid() || !player.CurrentEquipment.IsValid() )
		{
			Log.Warning( $"{command}: no local player or no weapon in hand (lp_give_m870 first)." );
			return null;
		}

		var cycle = player.CurrentEquipment.Components.Get<LpPumpShotgunCycle>( FindMode.EnabledInSelfAndDescendants );
		if ( !cycle.IsValid() )
		{
			Log.Warning( $"{command}: the weapon in hand has no LpPumpShotgunCycle." );
			return null;
		}

		return cycle;
	}
}
