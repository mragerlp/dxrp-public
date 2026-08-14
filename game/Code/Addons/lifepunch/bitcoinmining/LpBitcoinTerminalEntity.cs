// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Sandbox;
using LifePunch.DXRP.Addons;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
using DamageInfo = Dxura.RP.Game.DamageInfo;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>CRT terminal prop — world LCD telemetry + USE opens command console.</summary>
[Title( "LIFEPUNCH Bitcoin Terminal (v2)" )]
[Category( "LifePunch/Bitcoin" )]
#if LIFEPUNCH_LOCAL
public sealed class LpBitcoinTerminalEntity : Component, Component.IPressable
#else
public sealed class LpBitcoinTerminalEntity : BaseEntity, Component.IPressable, IAreaDamageReceiver
#endif
{
	private const float ScreenRefreshSeconds = 0.25f;

	[Property] public float LinkRange { get; set; } = 512f;
	[Property] public TextRenderer ScreenText { get; set; }

	/// <summary>When true, prefab <c>lcd_screen</c> transform is authoritative — no bounds auto-align on spawn.</summary>
	[Property] public bool ManualLcdPlacement { get; set; }

	/// <summary>Dev spawn (<see cref="LpBitcoinDevSpawn"/>) — feet on ground, frozen collider (no printer drop).</summary>
	internal bool DevSpawnAsWorldMachine { get; set; }

	/// <summary>Hub that registered this terminal via admin Settings — not proximity auto-link.
	/// [Property, ReadOnly] + [Sync] = snapshot persistence (link state; UPGRADE_ARC_DESIGN decision 9).</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public Guid LinkedHubId { get; set; }

#if !LIFEPUNCH_LOCAL
	[Property]
	[Group( "Effects" )]
	public GameObject Explosion { get; set; }

	bool _isExploding;
	int _spawnDropGraceTicks;

	/// <summary>Last grab-gate edge (R1). Nullable so the FIRST host tick always applies the gate
	/// rather than inheriting a default that happens to match.</summary>
	bool? _grabGateMenuGoverns;
	bool _colliderSyncedFromModel;
#endif

#if !LIFEPUNCH_LOCAL
	public override string DisplayName => LpBitcoinIdent.TerminalDisplayName;
#endif

	private string _cachedScreenText;
	private bool _occluded;

	protected override void OnAwake()
	{
		// Collider sync after spawn grace in OnFixedUpdate (printer-style drop).
	}

	protected override void OnStart()
	{
		base.OnStart();
#if !LIFEPUNCH_LOCAL
		this.TryBindSpawnOwnerHost();
		if ( Networking.IsHost )
		{
			if ( HealthComponent.IsValid() )
			{
				HealthComponent.MaxHealth = LpBitcoinIdent.TerminalMaxHealth;
				if ( HealthComponent.Health <= 0f || HealthComponent.Health > HealthComponent.MaxHealth )
					HealthComponent.Health = HealthComponent.MaxHealth;
			}

			if ( DevSpawnAsWorldMachine )
			{
				LifePunchPropPhysics.SetupWorldMachine( GameObject, alignGround: true );
				_colliderSyncedFromModel = true;
				Log.Info( $"TERMINAL_SPAWN_PHYSICS pos={GameObject.WorldPosition} mode=dev-world-machine" );
			}
			else
			{
				_spawnDropGraceTicks = 45;
				_colliderSyncedFromModel = false;
				LifePunchPropPhysics.BeginGrabbablePrinterDrop( GameObject, syncColliderFromModel: false );
				Log.Info( $"TERMINAL_SPAWN_PHYSICS pos={GameObject.WorldPosition} gravity=on (printer drop, no ground snap)" );
			}
		}
#endif

		if ( !ScreenText.IsValid() )
			ScreenText = GameObject.Children.FirstOrDefault( c => c.Name == "lcd_screen" )?.GetComponent<TextRenderer>();

		if ( ScreenText.IsValid() && !ManualLcdPlacement )
			LifePunchTerminalLcd.TryAlignHashdScreen( GameObject, ScreenText );

		RefreshScreenIdle();
	}

	/// <summary>Re-align feet after dev recall / reposition.</summary>
	public void RestartPrinterSettle()
	{
#if !LIFEPUNCH_LOCAL
		if ( !Networking.IsHost )
			return;

		_spawnDropGraceTicks = 45;
		_colliderSyncedFromModel = false;
		LifePunchPropPhysics.BeginGrabbablePrinterDrop( GameObject, syncColliderFromModel: false );
#endif
	}

	protected override void OnFixedUpdate()
	{
#if !LIFEPUNCH_LOCAL
		if ( !Networking.IsHost )
			return;

		LifePunchPropPhysics.MaintainGrabbablePlaceableProp( GameObject );

		if ( _spawnDropGraceTicks > 0 )
		{
			_spawnDropGraceTicks--;
			return;
		}

		// COLLIDER: prefab-authored box is the source of truth (defect 2, r3). The terminal's
		// prefab previously carried an IDENTITY STUB (1,1,1) and relied entirely on the runtime
		// sync; its measured box is now baked in, so the sync is gone. See LpBitcoinRackEntity.

		ApplyGrabGateHost();
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// UNPOWERED-ONLY GRAB (R1/R2, ratified 2026-07-12). USE and the DXRP Hands grab both bind E, and
	/// on a menu-bearing machine the menu wins — which is why the terminal kept <c>hands_interact</c>
	/// and still could not be picked up (r3 runtime confirm: tag PRESENT, grab refused).
	///
	/// The gate is the exact complement of the menu's own precondition: OpenTerminalHost refuses
	/// unless the terminal is LINKED and the linked hub IS POWERED. So the menu can only govern E in
	/// that same state, and grab is allowed precisely when it cannot:
	///
	///   unlinked ................................ grabbable (no menu to lose E to)
	///   linked + hub UNPOWERED .................. grabbable (power-down-to-move, the intended flow)
	///   linked + hub POWERED .................... menu wins E, grab denied
	///
	/// Governing power source for the terminal is the LINKED HUB's IsPowered (R1) — the terminal has
	/// no power state of its own. Both tag calls host-broadcast, so clients see the change.
	/// </summary>
	private void ApplyGrabGateHost()
	{
		var hub = FindLinkedHub();
		var menuGoverns = hub.IsValid() && hub.IsPowered;

		if ( _grabGateMenuGoverns == menuGoverns )
			return; // no edge — do not re-broadcast tags every tick

		_grabGateMenuGoverns = menuGoverns;

		if ( menuGoverns )
			LifePunchPropPhysics.DenyHandsGrabTags( GameObject );
		else
			LifePunchPropPhysics.AllowHandsGrabTags( GameObject );

		Log.Info(
			$"LP_TERMINAL_GRAB_GATE menuGoverns={menuGoverns} linked={hub.IsValid()} " +
			$"hubPowered={( hub.IsValid() ? hub.IsPowered.ToString() : "n/a" )} " +
			$"hands_interact={GameObject.Tags.Has( "hands_interact" )}" );
	}

	public void ApplyAreaDamage( AreaDamage component )
	{
		var dmg = new DamageInfo(
			component.Attacker,
			component.Damage,
			component.Inflictor,
			component.WorldPosition,
			Flags: component.DamageFlags );

		HealthComponent?.TakeDamageHost( dmg );
	}

	protected override void OnDestroyed()
	{
		if ( Networking.IsHost && !_isExploding )
		{
			_isExploding = true;
			LifePunchMachineDestroyFx.SpawnPrinterStyleExplosion( this, Explosion, WorldPosition );
		}

		base.OnDestroyed();
	}

	public override void OnOcclusionChanged( bool occlude )
	{
		base.OnOcclusionChanged( occlude );
		_occluded = occlude;
	}
#endif

	protected override void OnUpdate()
	{
#if !LIFEPUNCH_LOCAL
		if ( _occluded || GameManager.IsHeadless )
			return;

		if ( Cooldown.Current.CheckAndStartCooldown( $"{GameObject.Id}:lcd", ScreenRefreshSeconds ) )
			return;
#endif

		RefreshScreenIdle();
	}

	/// <summary>Push hub telemetry to every terminal linked to this hub.</summary>
	public static void RefreshForHub( LpBitcoinHubEntity hub )
	{
		if ( hub is null || !hub.IsValid() )
			return;

		var scene = hub.GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return;

		foreach ( var terminal in scene.GetAllComponents<LpBitcoinTerminalEntity>() )
		{
			if ( !terminal.IsValid() )
				continue;

			var linked = terminal.FindLinkedHub();
			if ( linked.IsValid() && linked.GameObject.Id == hub.GameObject.Id )
				terminal.RefreshScreenIdle();
		}
	}

	public bool IsLinkedToHub() => LinkedHubId != Guid.Empty && FindLinkedHub().IsValid();

	public void LinkToHub( LpBitcoinHubEntity hub )
	{
		if ( !hub.IsValid() )
			return;

		if ( !IsWithinLinkRange( hub ) )
			return;

		LinkedHubId = hub.GameObject.Id;
		RefreshScreenIdle();
	}

	public void UnlinkFromHub()
	{
		LinkedHubId = Guid.Empty;
		RefreshScreenIdle();
	}

	public bool IsWithinLinkRange( LpBitcoinHubEntity hub )
		=> hub.IsValid() && hub.WorldPosition.Distance( WorldPosition ) <= LinkRange;

	internal LpBitcoinHubEntity FindLinkedHub()
		=> LpBitcoinHubEntity.FindByGameObjectId( LinkedHubId, GameObject.Scene ?? Game.ActiveScene );

	/// <summary>Nearest unlinked terminal within range (hub Settings link action).</summary>
	internal static LpBitcoinTerminalEntity FindNearestUnlinked( LpBitcoinHubEntity hub )
	{
		if ( !hub.IsValid() )
			return null;

		var scene = hub.GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return null;

		LpBitcoinTerminalEntity best = null;
		var bestDist = float.MaxValue;

		foreach ( var terminal in scene.GetAllComponents<LpBitcoinTerminalEntity>() )
		{
			if ( !terminal.IsValid() || terminal.LinkedHubId != Guid.Empty )
				continue;

			if ( !terminal.IsWithinLinkRange( hub ) )
				continue;

#if !LIFEPUNCH_LOCAL
			if ( !LifePunchEntityOwnership.SharesOperator( hub.Owner, terminal.Owner ) )
				continue;
#endif

			var dist = hub.WorldPosition.Distance( terminal.WorldPosition );
			if ( dist >= bestDist )
				continue;

			bestDist = dist;
			best = terminal;
		}

		return best;
	}

	public void RefreshScreenIdle()
	{
		if ( !ScreenText.IsValid() )
			return;

		var text = LpBitcoinTerminalScreen.Build( FindLinkedHub() );
		if ( string.Equals( text, _cachedScreenText, StringComparison.Ordinal ) )
			return;

		_cachedScreenText = text;
		ScreenText.Text = text;
	}

	public bool CanPress( IPressable.Event e )
		=> IsLinkedToHub() && LifePunchMenuInteractGate.CanPressMenu( GameObject );

	public bool Press( IPressable.Event e )
	{
		RequestOpenTerminal();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	public void RequestOpenTerminal() => OpenTerminalHost();

	[Rpc.Host]
	private void OpenTerminalHost()
	{
#if !LIFEPUNCH_LOCAL
		if ( !LifePunchMenuInteractGate.IsCallerAllowed( Rpc.Caller, GameObject ) )
			return;
#else
		if ( !LifePunchMenuInteractGate.IsCallerAllowed( null, GameObject ) )
			return;
#endif

		var hub = FindLinkedHub();
		if ( hub is null )
		{
			Log.Warning( "[lifepunch.bitcoin] Terminal not linked — register at hub admin → Settings." );
			return;
		}

		if ( !hub.IsPowered )
		{
			Log.Warning( "[lifepunch.bitcoin] Hub power off — enable at hub admin panel." );
			return;
		}

		OpenTerminal( Rpc.CallerId, hub.GameObject.Id );
	}

	[Rpc.Broadcast]
	private void OpenTerminal( Guid callerId, Guid hubId )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var hub = ResolveHub( hubId );
		if ( hub is null )
			return;

		LpBitcoinTerminalUiHost.Open( hub );
	}

	private static LpBitcoinHubEntity ResolveHub( Guid hubId )
		=> LpBitcoinHubEntity.FindByGameObjectId( hubId );
}
