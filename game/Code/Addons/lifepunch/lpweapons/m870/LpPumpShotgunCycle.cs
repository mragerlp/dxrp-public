// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Weapons.M870;

/// <summary>
/// The pump cycle of a pump-action shotgun. Sits beside ShootWeaponComponent, ReloadWeaponComponent and
/// AmmoComponent on the weapon's Functions object and changes none of them.
/// After each shot the pump runs one stroke: a short lead, back, hold, forward. At the start of the
/// back stroke the pump sound plays and the spent hull leaves the ejection port (the weapon's
/// ShootWeaponComponent must have no EjectionPrefab of its own, or the hull leaves twice).
/// After a reload that began empty the pump runs one closing stroke with no hull.
/// While a stroke runs the house "bolting" tag is up, which upstream already honours: no fire
/// (ShootWeaponComponent.cs:699, host :426) and no reload start (ReloadWeaponComponent.cs:101).
/// The host-side floor is ShootWeaponComponent.Delay (semi-only weapons, :415-418): keep the stroke
/// no longer than Delay * 0.85.
/// Cosmetic and local on every client: the owner broadcasts the two stroke starts, each client runs its
/// own timer, and nothing here touches ammo, damage, money or saved state. The hull is the upstream
/// casing effect (TimedDestroyComponent, no entity, no snapshot).
/// Shapes followed: BoltPusher.cs (tag bind :23, OnWeaponShot :57), ShootWeaponComponent.cs:180-191
/// (Effector) and :252-258 (casing clone). ReloadWeaponComponent raises no event, so the owner watches
/// its "reloading" tag (:57).
/// </summary>
[Title( "Pump Shotgun - Cycle" )]
[Icon( "pending" )]
[Group( "Weapon Components" )]
public sealed class LpPumpShotgunCycle : WeaponComponent, IGameEvents, IEquipmentEvents
{
	/// <summary>Seconds after the shot before the pump starts back.</summary>
	[Property]
	[Group( "Stroke" )]
	public float LeadSeconds { get; set; } = 0.10f;

	/// <summary>Seconds the pump takes to travel back.</summary>
	[Property]
	[Group( "Stroke" )]
	public float BackSeconds { get; set; } = 0.18f;

	/// <summary>Seconds the pump rests at the back.</summary>
	[Property]
	[Group( "Stroke" )]
	public float HoldSeconds { get; set; } = 0.04f;

	/// <summary>Seconds the pump takes to travel forward.</summary>
	[Property]
	[Group( "Stroke" )]
	public float ForwardSeconds { get; set; } = 0.18f;

	/// <summary>The pump sound, played at the start of the back stroke.</summary>
	[Property]
	[Group( "Effects" )]
	public SoundEvent? PumpSound { get; set; }

	/// <summary>The spent hull thrown on the back stroke (prefabs/weapon_effects/12g_casing.prefab).</summary>
	[Property]
	[Group( "Effects" )]
	public GameObject? EjectionPrefab { get; set; }

	/// <summary>Run one closing stroke when a reload ends.</summary>
	[Property]
	[Group( "Reload" )]
	public bool RackAfterReload { get; set; } = true;

	/// <summary>Only close with a stroke when the reload began with an empty gun.</summary>
	[Property]
	[Group( "Reload" )]
	public bool RackOnlyFromEmpty { get; set; } = true;

	private LpPumpState _state;
	private bool _wasReloading;
	private ViewModel? _slideViewModel;
	private LpPumpSlideVisual? _viewSlide;
	private LpPumpSlideVisual? _worldSlide;
	private bool _worldSlideSearched;

	private LpPumpTimings Timings => new( LeadSeconds, BackSeconds, HoldSeconds, ForwardSeconds );

	/// <summary>The viewmodel for the shooter in first person, the world model for everyone else.</summary>
	private IEquipment Effector => IsProxy || !Equipment.ViewModel.IsValid() ? Equipment : Equipment.ViewModel;

	protected override void OnStart()
	{
		BindTag( "bolting", () => LpPumpShotgunRules.BlocksFire( _state ) );
	}

	protected override void OnUpdate()
	{
		if ( !Equipment.IsValid() )
		{
			return;
		}

		if ( !IsProxy )
		{
			WatchReload();
		}

		_state = LpPumpShotgunRules.Tick( _state, Time.Delta, Timings, out var effects );

		// ShootWeaponComponent.cs:224 - no effects on a dedicated server.
		if ( Application.IsDedicatedServer )
		{
			return;
		}

		if ( (effects & LpPumpEffect.PumpSound) != 0 )
		{
			PumpSound?.Play( Equipment.WorldPosition );
		}

		if ( (effects & LpPumpEffect.EjectHull) != 0 )
		{
			SpawnHull();
		}

		ApplySlide( LpPumpShotgunRules.SlideFraction( _state, Timings ) );
	}

	void IGameEvents.OnWeaponShot()
	{
		// Owner only (posted from ShootWeaponComponent.Shoot). Every shot is pumped, the last one too:
		// the spent hull always leaves.
		BroadcastShotCycle();
	}

	void IEquipmentEvents.OnEquipmentHolstered( Equipment equipment )
	{
		if ( equipment != Equipment )
		{
			return;
		}

		_state = LpPumpShotgunRules.OnHolstered( _state );
		_wasReloading = false;
		ApplySlide( 0f );
	}

	[Rpc.Broadcast( NetFlags.Unreliable )]
	private void BroadcastShotCycle()
	{
		_state = LpPumpShotgunRules.OnShot( _state );
	}

	[Rpc.Broadcast( NetFlags.Unreliable )]
	private void BroadcastRack()
	{
		_state = LpPumpShotgunRules.BeginRack( _state );
	}

	/// <summary>Owner only: turn the "reloading" tag's edges into reload start and end.</summary>
	private void WatchReload()
	{
		var reloading = Equipment.Tags.Has( "reloading" );
		if ( reloading == _wasReloading )
		{
			return;
		}

		_wasReloading = reloading;

		if ( reloading )
		{
			_state = LpPumpShotgunRules.OnReloadStarted( _state, CurrentAmmo() );
			return;
		}

		var next = LpPumpShotgunRules.OnReloadEnded( _state, CurrentAmmo(), RackAfterReload, RackOnlyFromEmpty );
		if ( next.Activity == LpPumpActivity.Racking && Equipment.IsDeployed )
		{
			// Every client starts the same closing stroke, the owner included.
			BroadcastRack();
			return;
		}

		_state = LpPumpShotgunRules.OnHolstered( next );
	}

	private void SpawnHull()
	{
		var port = Effector.EjectionPort;
		if ( !EjectionPrefab.IsValid() || !port.IsValid() )
		{
			return;
		}

		EjectionPrefab.Clone( new CloneConfig
		{
			Parent = port, Transform = new Transform(), StartEnabled = true, Name = $"Ejection: {Equipment.GameObject}"
		} );
	}

	/// <summary>Move the pump mesh: the viewmodel's for the owner in first person, the world model's when it has one.</summary>
	private void ApplySlide( float fraction )
	{
		var viewModel = Equipment.ViewModel;
		if ( viewModel.IsValid() )
		{
			if ( _slideViewModel != viewModel )
			{
				_slideViewModel = viewModel;
				_viewSlide = viewModel.GameObject.Components.Get<LpPumpSlideVisual>( FindMode.EnabledInSelfAndDescendants );
			}

			if ( _viewSlide.IsValid() )
			{
				_viewSlide.SetFraction( fraction );
			}
		}

		if ( !_worldSlideSearched )
		{
			_worldSlideSearched = true;
			_worldSlide = Equipment.GameObject.Components.Get<LpPumpSlideVisual>( FindMode.EnabledInSelfAndDescendants );
		}

		if ( _worldSlide.IsValid() )
		{
			_worldSlide.SetFraction( fraction );
		}
	}

	private int CurrentAmmo()
	{
		// BoltPusher.cs:54 idiom: AmmoComponent sits on this same GameObject.
		return Components.TryGet<AmmoComponent>( out var ammo ) ? ammo.Ammo : 0;
	}

	/// <summary>Dev lane (lp_m870_pump): run one post-shot cycle on every client without firing.</summary>
	public void DevForceCycle()
	{
		BroadcastShotCycle();
	}

	/// <summary>Dev lane (lp_m870_eject): throw one hull now, on this client only.</summary>
	public void DevEjectHull()
	{
		SpawnHull();
	}

	/// <summary>Dev lane (lp_m870_reload_state): the state in one line.</summary>
	public string DevDescribe()
	{
		var timings = Timings;
		return $"activity={_state.Activity} phase={LpPumpShotgunRules.PhaseOf( _state, timings )} elapsed={_state.Elapsed:0.000} " +
			$"stroke={timings.Total:0.000}s slide={LpPumpShotgunRules.SlideFraction( _state, timings ):0.00} " +
			$"bolting={LpPumpShotgunRules.BlocksFire( _state )} reloadingTag={Equipment.Tags.Has( "reloading" )} " +
			$"reloadStartedEmpty={_state.ReloadStartedEmpty} ammo={CurrentAmmo()}/{LpPumpShotgunSpec.LoadedCapacity} " +
			$"viewSlide={_viewSlide.IsValid()} worldSlide={_worldSlide.IsValid()} hullPrefab={EjectionPrefab.IsValid()} pumpSound={PumpSound is not null}";
	}
}
