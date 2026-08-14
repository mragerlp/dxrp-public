// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using LifePunch.DXRP.Addons;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using DamageInfo = Dxura.RP.Game.DamageInfo;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Hub — PIN, power, linking, upgrades (purchased here). No mine/sell on hub UI.</summary>
[Title( "LIFEPUNCH Bitcoin Hub (v2)" )]
[Category( "LifePunch/Bitcoin" )]
#if LIFEPUNCH_LOCAL
public sealed class LpBitcoinHubEntity : Component, Component.IPressable
#else
public sealed class LpBitcoinHubEntity : BaseEntity, Component.IPressable, IAreaDamageReceiver
#endif
{
#if !LIFEPUNCH_LOCAL
	[Property]
	[Group( "Effects" )]
	public GameObject Explosion { get; set; }

	bool _isExploding;
#endif
	// [Property, ReadOnly] + [Sync] = snapshot persistence (BaseEntity.Owner-proven combo;
	// UPGRADE_ARC_DESIGN decision 9). Power / security / wallet are snapshot-sole-truth.
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public bool IsPowered { get; set; }
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public bool AccessPinIsSet { get; set; }
	// HOST-ONLY persisted: [Property] serializes into the snapshot (GameObject.Serialize, empirically
	// proven, red\0082); NO [Sync] = the PIN-derived value never replicates to clients — closes the P2
	// keyspace leak. Splits persistence from replication (Green AE2). Salted SHA256 digest (LpBitcoinHubPin)
	// is stable across process restarts — fixes P1 (GetHashCode was randomized per process).
	[Property, ReadOnly] public string AccessPinDigest { get; set; } = string.Empty;
	[Property, ReadOnly] public string AccessPinSalt { get; set; } = string.Empty;
	// H2 (codex\0080): a pre-#182 hub restores with AccessPinIsSet=true but an empty digest (the old
	// int-hash key is orphaned). It must FAIL CLOSED (no PIN unlocks) yet let the OWNER re-enroll a new
	// PIN once, wallet untouched. This synced flag is safe (reveals only "legacy, needs re-enroll", no
	// PIN material); it is re-derived on the first host tick after restore, not persisted.
	[Sync( SyncFlags.FromHost )] public bool AccessPinNeedsReEnrollment { get; set; }
#if LIFEPUNCH_LOCAL
	[Sync( SyncFlags.FromHost )] public long Owner { get; set; }
#endif
	/// <summary>PIN-gated hub wallet — rack BTC deposits here via terminal; cash out from hub admin.</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public float HubWalletBtc { get; set; }
	// Alerts deliberately NOT persisted — audit_retention's base tier is "Volatile Logs";
	// survival beyond restart becomes a purchased effect (ECONOMY_DOCTRINE house pattern).
	[Sync( SyncFlags.FromHost )] public string AlertFeed { get; private set; } = string.Empty;
	[Sync( SyncFlags.FromHost )] public int UnreadAlertCount { get; private set; }

#if !LIFEPUNCH_LOCAL
	public override string DisplayName => LpBitcoinIdent.HubDisplayName;
#endif

	/// <summary>Dev spawn (<see cref="LpBitcoinDevSpawn"/>) — feet on ground, frozen collider (no printer drop).</summary>
	internal bool DevSpawnAsWorldMachine { get; set; }

	private ModelRenderer _modelRenderer;
	private bool _lastPoweredVisual;
#if !LIFEPUNCH_LOCAL
	/// <summary>Skip collider sync / ground hacks until renderer bounds are live (printer-style drop).</summary>
	int _spawnDropGraceTicks;
	bool _colliderSyncedFromModel;
#endif

	protected override void OnAwake()
	{
		// Collider sync after ground align in OnStart (avoid self-hit trace).
	}

	protected override void OnStart()
	{
		base.OnStart();
#if !LIFEPUNCH_LOCAL
		this.TryBindSpawnOwnerHost();
		if ( Networking.IsHost )
		{
			if ( DevSpawnAsWorldMachine )
			{
				ApplyVirginSpawnDefaultsHost();
				if ( HealthComponent.IsValid() )
				{
					HealthComponent.MaxHealth = LpBitcoinIdent.HubMaxHealth;
					if ( HealthComponent.Health <= 0f || HealthComponent.Health > HealthComponent.MaxHealth )
						HealthComponent.Health = HealthComponent.MaxHealth;
				}

				// Holdable-hub law (3.5 item E): the hub is a NORMAL HANDS ENTITY — never a
				// world machine, regardless of power or claim state.
				LifePunchGroundContact.AlignMeshBottom( GameObject );
				LifePunchPropPhysics.BeginGrabbablePrinterDrop( GameObject, syncColliderFromModel: true );
				_colliderSyncedFromModel = true;
				Log.Info( $"HUB_SPAWN_PHYSICS pos={GameObject.WorldPosition} mode=dev-grabbable" );
			}
			else
			{
				ApplyVirginSpawnDefaultsHost();
				if ( HealthComponent.IsValid() )
				{
					HealthComponent.MaxHealth = LpBitcoinIdent.HubMaxHealth;
					if ( HealthComponent.Health <= 0f || HealthComponent.Health > HealthComponent.MaxHealth )
						HealthComponent.Health = HealthComponent.MaxHealth;
				}

				// Printer drop — prefab collider first frame; no AlignMeshBottom teleport.
				_spawnDropGraceTicks = 45;
				_colliderSyncedFromModel = false;
				LifePunchPropPhysics.BeginGrabbablePrinterDrop( GameObject, syncColliderFromModel: false );
				Log.Info( $"HUB_SPAWN_PHYSICS pos={GameObject.WorldPosition} gravity=on (printer drop, no ground snap)" );
			}
		}
#endif
		_modelRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
		                 ?? Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;
		EnsureHubVisuals();
		ApplyHubPowerVisual( IsPowered );
	}

	private void EnsureHubVisuals()
	{
		var visuals = Components.Get<LpBitcoinHubVisuals>( FindMode.EverythingInSelf );
		if ( !visuals.IsValid() )
		{
			visuals = GameObject.AddComponent<LpBitcoinHubVisuals>();
			visuals.Hub = this;
		}
		else if ( !visuals.Hub.IsValid() )
		{
			visuals.Hub = this;
		}
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

		if ( _spawnDropGraceTicks > 0 )
		{
			LifePunchPropPhysics.MaintainGrabbablePlaceableProp( GameObject );
			_spawnDropGraceTicks--;
			return;
		}

		// COLLIDER: prefab-authored box is the source of truth (defect 2, r3). The hub's authored
		// box already MATCHED its mesh AABB (verified by eye), so removing the sync is a no-op for
		// the hub — but it removes the mechanism that was masking the rack/terminal errors.

		// One-shot rehydrate sweep: pre-fix snapshots carry racks with no slot binding —
		// bind + reconcile once on first settled host tick (leak fix migration path).
		if ( !_slotSweepDone )
		{
			_slotSweepDone = true;
			ReconcileLinkedRacksHost();

			// H2 legacy-PIN migration: a pre-#182 hub restores AccessPinIsSet with no digest -> fail
			// closed and flag the owner for a one-time re-enrollment (wallet state untouched).
			if ( AccessPinIsSet && string.IsNullOrEmpty( AccessPinDigest ) )
			{
				AccessPinNeedsReEnrollment = true;
				PushAlertHost( LpBitcoinHubAlertKind.HackAttack, "Legacy PIN unreadable after update — owner must re-enroll a new PIN." );
			}
		}

		// Amended Holdable-Hub Law (3.5 item E, amended 2026-07-12): the hub is a HANDS
		// ENTITY only while UNPLACED — the spawn drop, still in motion. Once it settles at
		// rest (or is powered) it converts to a FIXED MACHINE: hands_interact dropped + RB
		// frozen, so USE opens the hub menu at natural console distance and the DXRP Hands
		// grab no longer wins the E key. Racks are unchanged (no menu, stay fully holdable).
		// HOLDABLE-WHILE-UNPLACED, NEVER POCKETABLE: pocket_item is stripped at spawn by
		// BeginGrabbablePrinterDrop and nothing re-adds it. The held-diff reach band still
		// governs the brief pre-placement window.
		if ( !_placedAsWorldMachine )
			MaybePlaceAsWorldMachineHost();
#endif
	}

	private bool _slotSweepDone;
#if !LIFEPUNCH_LOCAL
	// Amended Holdable-Hub Law placement state (2026-07-12): grabbable while unplaced, fixed once settled.
	private const float WorldMachineRestSpeed = 4f;
	private const int WorldMachineRestConfirmTicks = 12;
	private const int WorldMachineSettleCapTicks = 300;
	private bool _placedAsWorldMachine;
	private int _settleRestTicks;
	private int _settleElapsedTicks;

	/// <summary>Once the spawn-dropped hub settles at rest (or is powered) it converts to an
	/// immovable world machine — hands_interact dropped so USE opens the menu at natural distance
	/// instead of the DXRP Hands grab winning the E key. One-shot per placement (host only).</summary>
	private void MaybePlaceAsWorldMachineHost()
	{
		var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelf );
		var atRest = !rb.IsValid() || rb.Velocity.Length <= WorldMachineRestSpeed;
		_settleRestTicks = atRest ? _settleRestTicks + 1 : 0;
		_settleElapsedTicks++;

		// Wait for a short at-rest confirm window, unless powered (fix now) or the hard cap hits
		// (a hub that never fully rests still converts rather than staying grabbable forever).
		if ( !IsPowered
		     && _settleRestTicks < WorldMachineRestConfirmTicks
		     && _settleElapsedTicks < WorldMachineSettleCapTicks )
			return;

		if ( !LifePunchPropPhysics.EnforceWorldMachine( GameObject ) )
			return; // collider bounds not live yet — retry next host tick

		_placedAsWorldMachine = true;
		Log.Info(
			$"LP_HUB_PLACE placed=world-machine powered={IsPowered} rest={_settleRestTicks} " +
			$"elapsed={_settleElapsedTicks} hands_interact={GameObject.Tags.Has( "hands_interact" )} pos={GameObject.WorldPosition}" );
	}
#endif

	protected override void OnUpdate()
	{
		if ( IsPowered == _lastPoweredVisual )
			return;

		ApplyHubPowerVisual( IsPowered );
	}

	protected override void OnDestroy()
	{
		// Hub wallet is session/entity-bound — do not leave BTC on a destroyed hub.
		HubWalletBtc = 0f;
#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost )
			GetLinkedTerminal()?.UnlinkFromHub();
#endif
	}

#if !LIFEPUNCH_LOCAL
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
#endif

	// ROTATION SACRED (holdable-hub law clause 3): rotate is USE-while-holding in DXRP
	// hands — a grabbed hub must never answer Press, or rotation opens the menu.
	public bool CanPress( IPressable.Event e )
		=> !GameObject.Tags.Has( LifePunchPropPhysics.GrabbedTag )
		   && LifePunchMenuInteractGate.CanPressHubMenu( GameObject );

	public bool Press( IPressable.Event e )
	{
		RequestOpenHub();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	public void RequestOpenHub() => OpenHubHost();

	[Rpc.Host]
	private void OpenHubHost()
	{
#if !LIFEPUNCH_LOCAL
		if ( !LifePunchMenuInteractGate.IsCallerAllowedHub( Rpc.Caller, GameObject ) )
			return;
#else
		if ( !LifePunchMenuInteractGate.IsCallerAllowedHub( null, GameObject ) )
			return;
#endif

		OpenHub( Rpc.CallerId );
	}

	[Rpc.Broadcast]
	private void OpenHub( Guid callerId )
	{
		if ( Connection.Local.Id != callerId )
			return;

		LpHashdUiHost.Open( this );
	}

	public void SetPowered( bool on ) => SetPoweredHost( on );

	/// <summary>Host-side power apply without RPC caller (preview hub, editor).</summary>
	public void ApplyPoweredState( bool on )
	{
		IsPowered = on;
		ApplyHubPowerVisual( on );
		if ( !on )
		{
			foreach ( var rack in GetLinkedRacks() )
				rack.StopMiningHost();
		}

		RefreshLinkedTerminalScreens();
	}

	[Rpc.Host]
	private void SetPoweredHost( bool on )
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		IsPowered = on;
		ApplyHubPowerVisual( on );
		if ( !on )
		{
			foreach ( var rack in GetLinkedRacks() )
				rack.StopMiningHost();
		}

		RefreshLinkedTerminalScreens();
	}

	public void RefreshLinkedTerminalScreens() => LpBitcoinTerminalEntity.RefreshForHub( this );

	public IReadOnlyList<LpBitcoinRackEntity> GetLinkedRacks()
	{
		var scene = GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return Array.Empty<LpBitcoinRackEntity>();

		return LpBitcoinIdent.OrderLinkedRacks(
			scene.GetAllComponents<LpBitcoinRackEntity>()
				.Where( r => r.IsValid() && r.LinkedHubId == GameObject.Id ) );
	}

	public LpBitcoinRackEntity FindRackByIndex( int index )
	{
		var racks = GetLinkedRacks();
		return index >= 0 && index < racks.Count ? racks[index] : null;
	}

	/// <summary>Membership-change sweep (leak fix, GO 2026-07-09): (re)binds slot tokens —
	/// first-come keeps a valid unique token, blanks/dupes get the lowest free slot — then
	/// re-reconciles EVERY linked rack so none keeps a tier from a slot it no longer holds.
	/// Idempotent. Runs on link, rack death, the first host tick (pre-fix snapshot
	/// migration), and any future move/unclaim transition.</summary>
	internal void ReconcileLinkedRacksHost()
	{
		if ( !Networking.IsHost )
			return;

		var linked = GetLinkedRacks();
		var takenStandard = new HashSet<string>( StringComparer.Ordinal );

		// Pass 1 — first-come keeps: a valid, unique standard token survives; blanks,
		// duplicates, and garbage get cleared for reassignment. (OrderLinkedRacks is a
		// stable sort, so "first-come" is deterministic.)
		foreach ( var rack in linked )
		{
			if ( rack.AdvancedRack )
			{
				rack.AssignedSlotToken = LpBitcoinIdent.AdvancedRackTerminalToken;
				continue;
			}

			var token = rack.AssignedSlotToken ?? string.Empty;
			var valid = LpBitcoinIdent.TryParseLinkRackSlotToken( token, out var advanced, out _ ) && !advanced;
			if ( !valid || !takenStandard.Add( token ) )
				rack.AssignedSlotToken = string.Empty;
		}

		// Pass 2 — bind blanks to the lowest free slot.
		foreach ( var rack in linked )
		{
			if ( rack.AdvancedRack || !string.IsNullOrEmpty( rack.AssignedSlotToken ) )
				continue;

			for ( var n = 1; n <= LpBitcoinIdent.PortalMaxStandardRacksPerHub; n++ )
			{
				var candidate = LpBitcoinIdent.FormatDeclaredLinkSlotToken( false, n );
				if ( takenStandard.Add( candidate ) )
				{
					rack.AssignedSlotToken = candidate;
					break;
				}
			}
		}

		// Pass 3 — ledger-wins reconcile against each rack's OWN binding.
		foreach ( var rack in linked )
			rack.ReconcileComputeTierHost();
	}

	/// <summary>Purchase the next COMPUTE tier for a linked rack (Guid.Empty = first).
	/// Funnels to the ONE purchase path — slice 2 replaces the legacy CPU/core RPCs.</summary>
	public void RequestPurchaseComputeTier( Guid rackId ) => PurchaseComputeTierHost( rackId );

	public void RequestSetAccessPin( string pin, string confirm ) => SetAccessPinHost( pin, confirm );

	public void RequestUnlockAccessPin( string pin ) => UnlockAccessPinHost( pin );

	public void RequestChangeAccessPin( string currentPin, string newPin, string confirm )
		=> ChangeAccessPinHost( currentPin, newPin, confirm );

	public void RequestDepositRacksToHub() => DepositRacksToHubHost();

	public void RequestDepositRack( int index ) => DepositRackHost( index );

	public void RequestCashOutHub( float amount ) => CashOutHubHost( amount, false );

	public void RequestCashOutAllHub() => CashOutHubHost( HubWalletBtc, true );

	public void RequestSendHubWallet( long targetSteamId, float amount ) => SendHubWalletHost( targetSteamId, amount );

	/// <summary>First hub in the scene owned by <paramref name="steamId"/> (excludes <paramref name="exclude"/>).</summary>
	public static LpBitcoinHubEntity FindHubByOwnerSteamId( long steamId, LpBitcoinHubEntity exclude = null )
	{
		if ( steamId == 0 )
			return null;

		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		foreach ( var hub in scene.GetAllComponents<LpBitcoinHubEntity>() )
		{
			if ( !hub.IsValid() || hub == exclude )
				continue;

			if ( hub.Owner == steamId )
				return hub;
		}

		return null;
	}

	/// <summary>Legacy alias — cash out entire hub wallet.</summary>
	public void RequestSellAllRacks() => RequestCashOutAllHub();

	/// <summary>Resolve hub by spawned <see cref="GameObject.Id"/> — scene component scan, not Directory.FindByGuid.</summary>
	public static LpBitcoinHubEntity FindByGameObjectId( Guid hubId, Scene scene = null )
	{
		if ( hubId == Guid.Empty )
			return null;

		scene ??= Game.ActiveScene;
		if ( scene is null )
			return null;

		foreach ( var hub in scene.GetAllComponents<LpBitcoinHubEntity>() )
		{
			if ( hub.IsValid() && hub.GameObject.Id == hubId )
				return hub;
		}

		return null;
	}

	public bool HasLinkedTerminal() => GetLinkedTerminal().IsValid();

	public LpBitcoinTerminalEntity GetLinkedTerminal()
	{
		var scene = GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return null;

		foreach ( var terminal in scene.GetAllComponents<LpBitcoinTerminalEntity>() )
		{
			if ( !terminal.IsValid() )
				continue;

			if ( terminal.LinkedHubId == GameObject.Id )
				return terminal;
		}

		return null;
	}

	public bool HasNearbyUnlinkedTerminal()
		=> LpBitcoinTerminalEntity.FindNearestUnlinked( this ).IsValid();

	/// <summary>Max distance from hub for terminal Settings link and rig0 <c>link</c> rack registration.</summary>
	[Property] public float RackLinkRange { get; set; } = 512f;

	public bool HasNearbyUnlinkedRack()
		=> LpBitcoinRackEntity.FindNearestUnlinked( this, RackLinkRange ).IsValid();

	public void RequestLinkNearbyRack() => LinkNearbyRackHost();

	public void RequestLinkRack( string slotToken ) => LinkRackByTokenHost( slotToken );

	public void RequestUnlinkRack( string rackToken ) => UnlinkRackByTokenHost( rackToken );


	// Deliberate release verb (rig0 `unlink <rack>`): frees the slot binding and fires the
	// membership sweep. The slot's LEDGER RECORDS PERSIST — a rack later bound into this
	// freed slot inherits its ladder (R1 survival property).
	[Rpc.Host]
	private void UnlinkRackByTokenHost( string rackToken )
	{
		if ( !CanOperateTerminal( Rpc.CallerId ) )
			return;

		var racks = GetLinkedRacks();
		if ( !LpBitcoinIdent.TryResolveLinkedRackIndex( rackToken, racks, out var idx ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"usage: unlink <rackId>" );
			return;
		}

		var rack = racks[idx];
		var label = LpBitcoinIdent.FormatRackSlotDisplayName( rack, racks );
		rack.ReleaseFromHubHost();
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
			$"{label} unlinked — slot freed, ledger history kept." );
		RefreshLinkedTerminalScreens();
	}

	public void RequestLinkNearbyTerminal() => LinkNearbyTerminalHost();

	public void RequestUnlinkTerminal() => UnlinkTerminalHost();

	[Rpc.Host]
	private void LinkNearbyTerminalHost()
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		if ( !IsPowered )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Hub power is off — use the power switch before linking equipment." );
			return;
		}

		if ( Owner == 0 && !TryBindOwner( Rpc.CallerId ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Claim this hub first (secure boot / PIN), then link your terminal." );
			return;
		}

		var existing = GetLinkedTerminal();
		if ( existing.IsValid() )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Terminal already linked — unlink first to register another." );
			return;
		}

		var terminal = LpBitcoinTerminalEntity.FindNearestUnlinked( this );
		if ( !terminal.IsValid() )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"No unlinked terminal in range that belongs to you — place your terminal near this hub." );
			return;
		}

		if ( !this.TryClaimLinkableEquipment( terminal, Rpc.CallerId, out var linkError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, linkError );
			return;
		}

		LinkTerminalHost( terminal );
	}

	[Rpc.Host]
	private void UnlinkTerminalHost()
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		var terminal = GetLinkedTerminal();
		if ( !terminal.IsValid() )
			return;

		terminal.UnlinkFromHub();
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, "Bitcoin terminal unlinked from hub." );
	}

	[Rpc.Host]
	private void LinkNearbyRackHost()
	{
		if ( !CanOperateTerminal( Rpc.CallerId ) )
			return;

		if ( !IsPowered )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Hub power is off — power on before linking GPU racks." );
			return;
		}

		if ( !HasLinkedTerminal() )
			return;

		if ( GetLinkedRacks().Count >= LpBitcoinIdent.PortalMaxRacksPerHub )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"This hub supports 2 GPU racks + 1 Advanced GPU Rack — unlink a slot first." );
			return;
		}

		if ( Owner == 0 && !TryBindOwner( Rpc.CallerId ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Claim this hub first (secure boot / PIN), then link your racks." );
			return;
		}

		if ( RefuseIfOwnerHasAnotherActiveHub() )
			return;

		var rack = LpBitcoinRackEntity.FindNearestUnlinked( this, RackLinkRange );
		if ( !rack.IsValid() )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"No unlinked GPU rack in range that belongs to you — place your rack near this hub, then type link at rig0." );
			return;
		}

		if ( !this.TryClaimLinkableEquipment( rack, Rpc.CallerId, out var linkError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, linkError );
			return;
		}

		if ( !LpBitcoinIdent.CanLinkRackToHub( rack, GetLinkedRacks(), out var rackSlotError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, rackSlotError );
			return;
		}

		rack.LinkToHub( this );
		var label = LpBitcoinIdent.FormatRackSlotDisplayName( rack, GetLinkedRacks() );
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, $"{label} linked — type racks to confirm." );
		RefreshLinkedTerminalScreens();
	}

	[Rpc.Host]
	private void LinkRackByTokenHost( string slotToken )
	{
		if ( !CanOperateTerminal( Rpc.CallerId ) )
			return;

		if ( !IsPowered )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Hub power is off — power on before linking GPU racks." );
			return;
		}

		if ( !HasLinkedTerminal() )
			return;

		if ( !LpBitcoinIdent.TryParseLinkRackSlotToken( slotToken, out var advanced, out var standardSlot ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"usage: link gpurack-1|gpurack-2|advancedgpurack" );
			return;
		}

		var linked = GetLinkedRacks();
		if ( !LpBitcoinIdent.CanLinkToDeclaredSlot( advanced, standardSlot, linked, out var slotError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, slotError );
			return;
		}

		if ( GetLinkedRacks().Count >= LpBitcoinIdent.PortalMaxRacksPerHub )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"This hub supports 2 GPU racks + 1 Advanced GPU Rack — unlink a slot first." );
			return;
		}

		if ( Owner == 0 && !TryBindOwner( Rpc.CallerId ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Claim this hub first (secure boot / PIN), then link your racks." );
			return;
		}

		if ( RefuseIfOwnerHasAnotherActiveHub() )
			return;

		var rack = LpBitcoinRackEntity.FindNearestUnlinked( this, RackLinkRange, advancedOnly: advanced );
		if ( !rack.IsValid() )
		{
			var kind = advanced ? "Advanced GPU" : "GPU";
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"No unlinked {kind} rack in range that belongs to you — place your rack near this hub." );
			return;
		}

		if ( advanced != rack.AdvancedRack )
		{
			var expected = advanced ? "advancedgpurack" : $"gpurack-{standardSlot}";
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				$"ERR nearest rack in range is not {expected} — move the correct rack closer or unlink a mismatch." );
			return;
		}

		if ( !this.TryClaimLinkableEquipment( rack, Rpc.CallerId, out var linkError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, linkError );
			return;
		}

		if ( !LpBitcoinIdent.CanLinkRackToHub( rack, linked, out var rackSlotError ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, rackSlotError );
			return;
		}

		rack.LinkToHub( this );
		var declared = LpBitcoinIdent.FormatDeclaredLinkSlotToken( advanced, standardSlot );
		var label = LpBitcoinIdent.FormatRackSlotDisplayName( rack, GetLinkedRacks() );
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, $"{label} linked as {declared} — type racks to confirm." );
		RefreshLinkedTerminalScreens();
	}

	internal void LinkTerminalHost( LpBitcoinTerminalEntity terminal )
	{
		if ( !Networking.IsHost || !terminal.IsValid() )
			return;

		if ( !IsPowered )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Hub power is off — turn the hub on before linking the terminal." );
			return;
		}

		if ( terminal.LinkedHubId != Guid.Empty && terminal.LinkedHubId != GameObject.Id )
			return;

		if ( !terminal.IsWithinLinkRange( this ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"Terminal out of link range — move it closer to the hub." );
			return;
		}

#if !LIFEPUNCH_LOCAL
		if ( Owner != 0 && !LifePunchEntityOwnership.SharesOperator( Owner, terminal.Owner ) )
		{
			PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
				"That terminal belongs to another operator — only your terminal can link here." );
			return;
		}
#endif

		terminal.LinkToHub( this );
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, "Bitcoin terminal linked — rig0 ops enabled." );
		RefreshLinkedTerminalScreens();
	}

	public float GetRackPendingBtc() => GetLinkedRacks().Sum( r => r.BitcoinAmount );

	public IReadOnlyList<LpBitcoinHubAlert> GetAlerts()
		=> LpBitcoinHubAlertCodec.Deserialize( AlertFeed );

	public int GetRackIndex( LpBitcoinRackEntity rack )
	{
		if ( !rack.IsValid() )
			return -1;

		var racks = GetLinkedRacks();
		for ( var i = 0; i < racks.Count; i++ )
		{
			if ( racks[i].GameObject.Id == rack.GameObject.Id )
				return i;
		}

		return -1;
	}

	public void RequestMarkAlertsRead() => MarkAlertsReadHost();

	public void RequestClearAlerts() => ClearAlertsHost();

	/// <summary>Dev only — seed varied sample alerts so the Hub logs page can be verified in flatgrass.</summary>
	internal void DevSeedSampleAlerts()
	{
		if ( !Networking.IsHost )
			return;

		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, "rig0> link gpurack-01 — registered" );
		PushAlertHost( LpBitcoinHubAlertKind.HubTransfer, "Swept 0.004210 BTC from gpurack-01 to hub wallet" );
		PushAlertHost( LpBitcoinHubAlertKind.RackCapacity, "gpurack-02 at capacity (0.010000 / 0.010000 BTC) — deposit at terminal" );
		PushAlertHost( LpBitcoinHubAlertKind.HackAttack, "HASHD intrusion attempt — unknown operator" );
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, "rig0> racks — 2 linked, 1 mining" );
		PushAlertHost( LpBitcoinHubAlertKind.HubTransfer, "Cashed out 0.025000 BTC at terminal" );
	}

	public void RequestPushTerminalCommandAlert( string command, string result )
		=> PushTerminalCommandAlertHost( command, result );

	public void ReportHackAttempt( string attackerLabel ) => ReportHackAttemptHostRpc( attackerLabel );

	[Rpc.Host]
	private void ReportHackAttemptHostRpc( string attackerLabel )
	{
		var label = LpBitcoinHubAlertCodec.Sanitize( attackerLabel );
		if ( string.IsNullOrWhiteSpace( label ) )
			label = "unknown operator";

		PushHackAttackAlertHost( $"HASHD intrusion attempt — {label}" );
	}

	internal void PushRackCapacityAlertHost( int rackIndex, float amount, float capacity )
	{
		var racks = GetLinkedRacks();
		var rack = FindRackByIndex( rackIndex );
		var label = rack.IsValid() ? LpBitcoinIdent.FormatRackSlotTerminalToken( rack, racks ) : "gpurack";
		PushAlertHost(
			LpBitcoinHubAlertKind.RackCapacity,
			$"{label} at capacity ({amount:F6} / {capacity:F6} BTC) — deposit at terminal" );
	}

	[Rpc.Host]
	private void MarkAlertsReadHost()
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		UnreadAlertCount = 0;
	}

	[Rpc.Host]
	private void ClearAlertsHost()
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		AlertFeed = string.Empty;
		UnreadAlertCount = 0;
	}

	[Rpc.Host]
	private void PushTerminalCommandAlertHost( string command, string result )
	{
		if ( !CanOperateTerminal( Rpc.CallerId ) )
			return;

		var cmd = LpBitcoinHubAlertCodec.Sanitize( command );
		if ( string.IsNullOrWhiteSpace( cmd ) )
			return;

		var line = LpBitcoinHubAlertCodec.Sanitize( result );
		var message = string.IsNullOrWhiteSpace( line ) ? cmd : $"{cmd} → {line}";
		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand, message );
	}

	internal void PushHackAttackAlertHost( string detail )
	{
		var message = LpBitcoinHubAlertCodec.Sanitize( detail );
		if ( string.IsNullOrWhiteSpace( message ) )
			message = "Intrusion attempt detected on HASHD hub";

		PushAlertHost( LpBitcoinHubAlertKind.HackAttack, message );
	}

	internal void PushAlertHost( LpBitcoinHubAlertKind kind, string message )
	{
		if ( !Networking.IsHost )
			return;

		message = LpBitcoinHubAlertCodec.Sanitize( message );
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		var alerts = GetAlerts().ToList();
		alerts.Insert( 0, new LpBitcoinHubAlert
		{
			Kind = kind,
			Message = message,
			Timestamp = Time.Now,
		} );

		while ( alerts.Count > 20 )
			alerts.RemoveAt( alerts.Count - 1 );

		AlertFeed = LpBitcoinHubAlertCodec.Serialize( alerts );
		UnreadAlertCount = Math.Min( UnreadAlertCount + 1, 99 );
	}

	[Rpc.Host]
	private void SetAccessPinHost( string pin, string confirm )
	{
		// H2: a legacy hub (AccessPinIsSet, no digest -> AccessPinNeedsReEnrollment) is allowed a
		// one-time owner re-enrollment; a normally-configured hub still rejects a re-set.
		if ( AccessPinIsSet && !AccessPinNeedsReEnrollment )
		{
			SendPinResultToCaller( false, "PIN already configured." );
			return;
		}

		if ( !LpBitcoinHubPin.IsValidFormat( pin ) || pin != confirm )
		{
			SendPinResultToCaller( false, "PIN must be 4 digits and match confirmation." );
			return;
		}

		if ( Owner == 0 && !TryBindOwner( Rpc.CallerId ) )
		{
			SendPinResultToCaller( false, "Could not claim hub ownership." );
			return;
		}

		if ( Owner != 0 && !CallerIsOwner( Rpc.CallerId ) )
		{
			SendPinResultToCaller( false, "Only the hub owner can set the PIN." );
			return;
		}

		AccessPinSalt = LpBitcoinHubPin.NewSalt();
		AccessPinDigest = LpBitcoinHubPin.Hash( pin, AccessPinSalt );
		AccessPinIsSet = true;
		AccessPinNeedsReEnrollment = false;
		SendPinResultToCaller( true, "Secure boot enabled." );
	}

	// --- PIN brute-force throttle (H3, codex\0080): host-only, transient (never persisted, never synced).
	// A 4-digit PIN is a 10,000-choice ONLINE oracle even with the digest host-only, so every failed
	// attempt is tracked per caller with a temporary lockout and an auditable HackAttack alert. ---
	private const int PinMaxFailsBeforeLockout = 5;
	private const float PinLockoutSeconds = 30f;
	private struct PinAttemptState { public int Fails; public float LockedUntil; }
	private readonly System.Collections.Generic.Dictionary<Guid, PinAttemptState> _pinAttempts = new();

	private bool PinAttemptLockedOut( Guid caller )
		=> _pinAttempts.TryGetValue( caller, out var s ) && Time.Now < s.LockedUntil;

	private bool RegisterFailedPinAttempt( Guid caller, string surface )
	{
		var s = _pinAttempts.TryGetValue( caller, out var cur ) ? cur : new PinAttemptState();
		s.Fails++;
		if ( s.Fails >= PinMaxFailsBeforeLockout )
		{
			s.LockedUntil = Time.Now + PinLockoutSeconds;
			s.Fails = 0;
			_pinAttempts[caller] = s;
			PushAlertHost( LpBitcoinHubAlertKind.HackAttack, $"{surface} PIN lockout — too many failed attempts" );
			return true;
		}

		_pinAttempts[caller] = s;
		PushAlertHost( LpBitcoinHubAlertKind.HackAttack, $"{surface} PIN failed attempt {s.Fails}/{PinMaxFailsBeforeLockout}" );
		return false;
	}

	private void ClearPinAttempts( Guid caller ) => _pinAttempts.Remove( caller );

	[Rpc.Host]
	private void UnlockAccessPinHost( string pin )
	{
		if ( !AccessPinIsSet )
		{
			SendPinResultToCaller( true, string.Empty );
			return;
		}

		var caller = Rpc.CallerId;
		// Locked-out callers are rejected silently: the lockout was already audited at onset (below), so
		// re-alerting on every rejected RPC while locked would only spam the feed. Checked first so a
		// locked-out prober cannot generate fresh audit lines.
		if ( PinAttemptLockedOut( caller ) )
		{
			SendPinResultToCaller( false, "Too many failed attempts — locked out briefly." );
			return;
		}

		// #184 REVISE (codex\0082): an unauthorized attempt is a COUNTED, AUDITED failure (was silent).
		if ( !CallerIsOwner( caller ) )
		{
			RegisterFailedPinAttempt( caller, "Hub admin (unauthorized)" );
			SendPinResultToCaller( false, "Access denied — hub belongs to another operator." );
			return;
		}

		if ( !LpBitcoinHubPin.Matches( pin, AccessPinSalt, AccessPinDigest ) )
		{
			RegisterFailedPinAttempt( caller, "Hub admin" );
			SendPinResultToCaller( false, "Incorrect PIN." );
			return;
		}

		ClearPinAttempts( caller );
		SendPinResultToCaller( true, string.Empty );
	}

	/// <summary>Terminal (rig0>) uses the exact same PIN secret as the hub admin panel.</summary>
	public void RequestTerminalUnlock( string pin ) => RequestTerminalUnlockHost( pin );

	[Rpc.Host]
	private void RequestTerminalUnlockHost( string pin )
	{
		if ( !AccessPinIsSet )
		{
			SendTerminalUnlockResultToCaller( true );
			return;
		}

		var caller = Rpc.CallerId;
		// Locked-out callers rejected silently (the lockout was already audited at onset).
		if ( PinAttemptLockedOut( caller ) )
		{
			SendTerminalUnlockResultToCaller( false );
			return;
		}

		// H3 (codex\0080): was an ungated 10k online oracle — any client could brute-force. Gate to the
		// terminal-operate authority so a non-authorized caller never reaches the compare, AND (codex\0082
		// #184 revise) audit that unauthorized attempt as a counted failure (was silent).
		if ( !CanOperateTerminal( caller ) )
		{
			RegisterFailedPinAttempt( caller, "Terminal (unauthorized)" );
			SendTerminalUnlockResultToCaller( false );
			return;
		}

		if ( !LpBitcoinHubPin.Matches( pin, AccessPinSalt, AccessPinDigest ) )
		{
			RegisterFailedPinAttempt( caller, "Terminal" );
			SendTerminalUnlockResultToCaller( false );
			return;
		}

		ClearPinAttempts( caller );
		SendTerminalUnlockResultToCaller( true );
	}

	private void SendTerminalUnlockResultToCaller( bool ok )
		=> NotifyTerminalUnlockResult( Rpc.CallerId, ok );

	[Rpc.Broadcast]
	private void NotifyTerminalUnlockResult( Guid callerId, bool ok )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var terminalPanel = Game.ActiveScene?.GetAllComponents<LpBitcoinTerminalPanel>()
			.FirstOrDefault( p => p.IsValid() && p.Hub == this )
			?? Game.ActiveScene?.GetAllComponents<LpBitcoinTerminalPanel>().FirstOrDefault( p => p.IsValid() );

		if ( terminalPanel.IsValid() )
			terminalPanel.OnTerminalPinResult( ok );
	}

	[Rpc.Host]
	private void ChangeAccessPinHost( string currentPin, string newPin, string confirm )
	{
		if ( !AccessPinIsSet )
		{
			SendPinChangeResultToCaller( false, "Hub PIN is not configured yet." );
			return;
		}

		if ( !CallerIsOwner( Rpc.CallerId ) )
		{
			SendPinChangeResultToCaller( false, "Only the hub owner can change the PIN." );
			return;
		}

		if ( !LpBitcoinHubPin.Matches( currentPin, AccessPinSalt, AccessPinDigest ) )
		{
			SendPinChangeResultToCaller( false, "Current PIN is incorrect." );
			return;
		}

		if ( !LpBitcoinHubPin.IsValidFormat( newPin ) || newPin != confirm )
		{
			SendPinChangeResultToCaller( false, "New PIN must be 4 digits and match confirmation." );
			return;
		}

		if ( currentPin == newPin )
		{
			SendPinChangeResultToCaller( false, "New PIN must differ from the current PIN." );
			return;
		}

		AccessPinSalt = LpBitcoinHubPin.NewSalt();
		AccessPinDigest = LpBitcoinHubPin.Hash( newPin, AccessPinSalt );
		SendPinChangeResultToCaller( true, "Hub PIN updated." );
	}

	[Rpc.Host]
	private void DepositRackHost( int index )
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		if ( !HasLinkedTerminal() )
			return;

		var rack = FindRackByIndex( index );
		if ( rack is null || rack.BitcoinAmount <= 0f )
			return;

		HubWalletBtc += rack.BitcoinAmount;
		rack.ClearBalanceHost();
		RefreshLinkedTerminalScreens();
	}

	[Rpc.Host]
	private void DepositRacksToHubHost()
	{
		if ( !CanManageHub( Rpc.CallerId ) )
			return;

		if ( !HasLinkedTerminal() )
			return;

		foreach ( var rack in GetLinkedRacks() )
		{
			if ( rack.BitcoinAmount <= 0f )
				continue;

			HubWalletBtc += rack.BitcoinAmount;
			rack.ClearBalanceHost();
		}

		RefreshLinkedTerminalScreens();
	}

	[Rpc.Host]
	private async void CashOutHubHost( float amount, bool soldAll )
	{
		var callerId = Rpc.CallerId;
		if ( !CanManageHub( callerId ) )
			return;

		if ( amount <= 0f || amount > HubWalletBtc )
			return;

		var payout = LpBitcoinEconomy.BtcToCashPayout( amount, callerId );
		if ( payout.FinalUsd == 0 )
			return;

		// Debit BEFORE the TryPayBank await, so the balance itself serialises concurrent cash-outs:
		// a second call entering during the await reads the already-reduced wallet and its own
		// `amount > HubWalletBtc` check limits it. This mirrors PurchaseFlow's debit-then-restore
		// (LpBitcoinPurchaseFlow.cs:106/112). The one difference: our restore straddles the await,
		// so it must be ADDITIVE (+= amount) — a snapshot restore would clobber a deposit that
		// landed during the await. Without this, two cash-outs both paid the bank and drove the
		// wallet negative (repro: handoff/gate-toctou-repro-2026-07-09.log).
		HubWalletBtc -= amount;
		ClampWalletNonNegativeHost( callerId, "cashout-debit" );
		Log.Info( $"LP_CASHOUT_SENSOR caller={callerId} amountReq={amount:F8} walletAfter={HubWalletBtc:F8}" );

		if ( !await LpBitcoinWallet.TryPayBank(
			callerId,
			payout.FinalUsd,
			payout.BuildLedgerReason( "LIFEPUNCH hub BTC cashout" ) ) )
		{
			HubWalletBtc += amount; // payment failed — give back exactly what we took
			return;
		}

		LpBitcoinPayoutAudit.RecordSuccessful( callerId, "hub-cashout", payout );
		NotifyCashOutSuccess( callerId, amount, payout.FinalUsd, soldAll );
		RefreshLinkedTerminalScreens();
	}

	/// <summary>Backstop invariant: the hub wallet is host-authoritative money and must never be
	/// negative. If a debit ever drives it below zero a balance check was raced — clamp to zero and
	/// log an ERROR naming the caller, so the sentinel is loud in the feed rather than silent
	/// corruption. In normal flow the pre-debit check keeps this from firing.</summary>
	private void ClampWalletNonNegativeHost( Guid callerId, string op )
	{
		if ( HubWalletBtc >= 0f )
			return;

		Log.Error( $"LP_CASHOUT_INVARIANT hub wallet went negative ({HubWalletBtc:F8}) after {op} caller={callerId} — clamped to 0; a balance check was raced." );
		HubWalletBtc = 0f;
	}

	[Rpc.Host]
	private void SendHubWalletHost( long targetSteamId, float amount )
	{
		if ( !CanOperateTerminal( Rpc.CallerId ) )
			return;

		if ( !HasLinkedTerminal() )
			return;

		if ( targetSteamId <= 0 || amount <= 0f || amount > HubWalletBtc )
			return;

		if ( Owner != 0 && Owner == targetSteamId )
			return;

		var target = FindHubByOwnerSteamId( targetSteamId, exclude: this );
		if ( target is null || !target.IsValid() )
			return;

		HubWalletBtc -= amount;
		target.HubWalletBtc += amount;

		var recipientLabel = LifePunchEntityOwnership.GetOwnerLabel( targetSteamId );
		if ( string.IsNullOrWhiteSpace( recipientLabel ) )
			recipientLabel = targetSteamId.ToString();

		var senderLabel = LifePunchEntityOwnership.GetOwnerLabel( Owner );
		if ( string.IsNullOrWhiteSpace( senderLabel ) )
			senderLabel = Owner == 0 ? "unknown operator" : Owner.ToString();

		PushAlertHost( LpBitcoinHubAlertKind.HubTransfer,
			$"Sent {amount:F6} BTC to hub {recipientLabel}" );
		target.PushAlertHost( LpBitcoinHubAlertKind.HubTransfer,
			$"Received {amount:F6} BTC from hub {senderLabel}" );

		RefreshLinkedTerminalScreens();
		target.RefreshLinkedTerminalScreens();
	}

	[Rpc.Broadcast]
	private void NotifyCashOutSuccess( Guid callerId, float btcAmount, uint usdPayout, bool soldAll )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var panel = Game.ActiveScene?.GetAllComponents<LpHashdPanel>().FirstOrDefault();
		if ( panel.IsValid() )
			panel.OnCashOutSuccess( btcAmount, usdPayout, soldAll );
	}

	private void SendPinResultToCaller( bool ok, string message )
		=> NotifyPinResult( Rpc.CallerId, ok, message );

	[Rpc.Broadcast]
	private void NotifyPinResult( Guid callerId, bool ok, string message )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var panel = FindHashdPanelForCaller();
		if ( panel.IsValid() )
			panel.OnPinGateResult( ok, message );
	}

	private void SendPinChangeResultToCaller( bool ok, string message )
		=> NotifyPinChangeResult( Rpc.CallerId, ok, message );

	[Rpc.Broadcast]
	private void NotifyPinChangeResult( Guid callerId, bool ok, string message )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var panel = FindHashdPanelForCaller();
		if ( panel.IsValid() )
			panel.OnPinChangeResult( ok, message );
	}

	private LpHashdPanel FindHashdPanelForCaller()
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		return scene.GetAllComponents<LpHashdPanel>()
			.FirstOrDefault( p => p.IsValid() && p.Hub == this )
			?? scene.GetAllComponents<LpHashdPanel>().FirstOrDefault( p => p.IsValid() );
	}

	[Rpc.Host]
	private void PurchaseComputeTierHost( Guid rackId )
	{
		var result = LpBitcoinPurchaseFlow.PurchaseComputeTierHost( this, ResolveRack( rackId ), Rpc.CallerId );
		NotifyPurchaseResultToCaller( Rpc.CallerId, result );
	}

	/// <summary>Mirror the purchase envelope back to the caller client (slice 3) — the
	/// stepper renders its states (working → success / rejection) from this, never
	/// optimistically. Same idiom as <see cref="NotifyCashOutSuccess"/>.</summary>
	internal void NotifyPurchaseResultToCaller( Guid callerId, LpBitcoinPurchaseResult result )
		=> NotifyPurchaseResult( callerId, (int)result.Code, result.NewTier, result.CostPaidSats,
			result.NewClockGhz, result.NewBufferCap, result.ShortfallSats );

	[Rpc.Broadcast]
	private void NotifyPurchaseResult(
		Guid callerId, int code, int newTier, long costSats, float newClockGhz, float newBufferCap, long shortfallSats )
	{
		if ( Connection.Local.Id != callerId )
			return;

		var panel = Game.ActiveScene?.GetAllComponents<LpHashdPanel>().FirstOrDefault( p => p.IsValid() && p.Hub == this )
			?? Game.ActiveScene?.GetAllComponents<LpHashdPanel>().FirstOrDefault( p => p.IsValid() );
		if ( panel.IsValid() )
			panel.OnPurchaseResult( code, newTier, costSats, newClockGhz, newBufferCap, shortfallSats );
	}

	private LpBitcoinRackEntity ResolveRack( Guid rackId )
	{
		if ( rackId == Guid.Empty )
			return GetLinkedRacks().FirstOrDefault();

		return GetLinkedRacks().FirstOrDefault( r => r.GameObject.Id == rackId );
	}

	/// <summary>GO ruling C (slice 2): ONE active HASHD hub per operator, enforced at
	/// rack-link time — keeps the ledger's owner+slot subject key unambiguous. Scoped
	/// to lpbitcoin hubs only (future Banker HUB / data center are separate systems).</summary>
	private bool RefuseIfOwnerHasAnotherActiveHub()
	{
		if ( Owner == 0 )
			return false;

		var other = FindHubByOwnerSteamId( Owner, exclude: this );
		if ( other is null )
			return false;

		PushAlertHost( LpBitcoinHubAlertKind.TerminalCommand,
			"One active HASHD hub per operator — unlink or remove your other hub before registering racks here." );
		return true;
	}

	public bool CanManageHub( Guid callerId )
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		if ( !AccessPinIsSet )
			return CallerIsOwner( callerId ) || Owner == 0;

		return CallerIsOwner( callerId );
#endif
	}

	public bool CanOperateTerminal( Guid callerId )
	{
		if ( !IsPowered )
			return false;

		return CanManageHub( callerId );
	}

	public void BindOwnerFromLocalViewer()
	{
#if LIFEPUNCH_LOCAL
		if ( Owner != 0 )
			return;
		Owner = 1;
#else
		LifePunchEntityOwnership.BindOwnerFromLocalViewer( this );
#endif
	}

	/// <summary>Market spawn baseline — OFF until operator sets PIN and powers on at hub admin.</summary>
	private void ApplyVirginSpawnDefaultsHost()
	{
		if ( AccessPinIsSet || HubWalletBtc > 0f )
			return;

		IsPowered = false;
	}

	private bool CallerIsOwner( Guid callerId )
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		return LifePunchEntityOwnership.CallerIsOwner( Owner, callerId );
#endif
	}

	private bool TryBindOwner( Guid callerId )
	{
#if LIFEPUNCH_LOCAL
		Owner = 1;
		return true;
#else
		return this.TryBindOwnerFromCaller( callerId );
#endif
	}

#if !LIFEPUNCH_LOCAL
	private bool TryClaimLinkableEquipment( BaseEntity equipment, Guid callerId, out string error )
	{
		error = string.Empty;
		if ( !equipment.IsValid() )
		{
			error = "Equipment missing — re-place and try again.";
			return false;
		}

		if ( LifePunchEntityOwnership.SharesOperator( Owner, equipment.Owner ) )
			return true;

		if ( equipment.Owner != 0 )
		{
			error = "That equipment belongs to another operator — only your gear can link to this hub.";
			return false;
		}

		if ( !LifePunchEntityOwnership.TryClaimEquipmentForHub( equipment, Owner, callerId ) )
		{
			error = "Could not claim equipment ownership — only gear you placed can link here.";
			return false;
		}

		return true;
	}
#else
	private bool TryClaimLinkableEquipment( Component equipment, Guid callerId, out string error )
	{
		error = string.Empty;
		return equipment.IsValid();
	}
#endif

	private void ApplyHubPowerVisual( bool powered )
	{
		_lastPoweredVisual = powered;

		var visuals = Components.Get<LpBitcoinHubVisuals>( FindMode.EverythingInSelf );
		if ( !visuals.IsValid() || !visuals.Enabled )
			return;

		if ( !_modelRenderer.IsValid() )
			_modelRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
			                 ?? Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;

		visuals.ApplyPowerVisuals( powered );
	}
}
