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
using Dxura.RP.Shared;
using DamageInfo = Dxura.RP.Game.DamageInfo;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>GPU rack — idle BTC printer; deposit to hub wallet via bitcoin terminal only.</summary>
[Title( "LIFEPUNCH Bitcoin Rack (v2)" )]
[Category( "LifePunch/Bitcoin" )]
#if LIFEPUNCH_LOCAL
public sealed class LpBitcoinRackEntity : Component, Component.IPressable
#else
public sealed class LpBitcoinRackEntity : BaseEntity, Component.IPressable, IAreaDamageReceiver
#endif
{
	/// <summary>GPU rack farm — stacked mesh; the COMPUTE tier (rack_compute) drives mining rate.
	/// Standard by default: every construction site sets this explicitly and both prefabs
	/// serialize it, so a bare AddComponent must never silently mint a 2× yield rack.</summary>
	[Property] public bool AdvancedRack { get; set; }

	/// <summary>Dev spawn (<see cref="LpBitcoinDevSpawn"/>) — feet on ground, frozen collider (no printer drop).</summary>
	internal bool DevSpawnAsWorldMachine { get; set; }

	// [Property, ReadOnly] + [Sync] = snapshot persistence (BaseEntity.Owner-proven combo;
	// UPGRADE_ARC_DESIGN decision 9). World-only state below is snapshot-sole-truth.
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public Guid LinkedHubId { get; set; }

	/// <summary>Ledger subject binding (leak fix, GO 2026-07-09): the slot token this rack
	/// OWNS while linked — assigned by the hub sweep at link time (lowest unoccupied),
	/// persisted + synced like <see cref="LinkedHubId"/>, cleared on unlink/destroy.
	/// Reconcile reads ONLY this; slot identity is never re-derived from link order.</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public string AssignedSlotToken { get; set; } = "";
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public bool IsMining { get; set; }
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public float BitcoinAmount { get; set; }
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public float ClockGhz { get; set; } = LpBitcoinEconomy.StartClockGhz;
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public int CoreCount { get; set; } = LpBitcoinEconomy.StartCores;
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public float MiningProgress { get; set; }

	/// <summary>rack_compute tier projection (0 = stock, I–V purchased). The ledger is
	/// ownership truth; this reconciles ledger-wins on host start and relink, and the
	/// reconcile re-derives the rate (Apply is absolute — decision 2).</summary>
	[Property, ReadOnly] [Sync( SyncFlags.FromHost )] public int ComputeTier { get; set; }

	/// <summary>Advanced racks yield 2× per slot for 2× capital (decision 5).</summary>
	public float YieldMultiplier => AdvancedRack
		? LpBitcoinEconomy.AdvancedRackYieldMultiplier
		: LpBitcoinIdent.BaseRackYieldMultiplier;
	public float MiningRatePerMinute => LpBitcoinEconomy.MiningRatePerMinute( ClockGhz, CoreCount, YieldMultiplier );
	public int UsdValue => (int)LpBitcoinEconomy.BtcToCashUsd( BitcoinAmount );

#if !LIFEPUNCH_LOCAL
	public override string DisplayName => AdvancedRack
		? LpBitcoinIdent.AdvancedRackDisplayName
		: LpBitcoinIdent.RackDisplayName;
#endif

	private TimeSince _sincePayout;
	private ModelRenderer _modelRenderer;
	private LpBitcoinRackVisuals _visuals;
	private bool _lastMiningVisual;
	private bool _capacityAlertSent;
	private bool _computeTierReconciled;
#if !LIFEPUNCH_LOCAL
	[Property]
	[Group( "Effects" )]
	public GameObject Explosion { get; set; }

	bool _isExploding;
	int _spawnDropGraceTicks;
	bool _colliderSyncedFromModel;
#endif

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
			if ( DevSpawnAsWorldMachine )
			{
				ApplyVirginSpawnDefaultsHost();
				if ( HealthComponent.IsValid() )
				{
					HealthComponent.MaxHealth = LpBitcoinIdent.RackMaxHealth;
					if ( HealthComponent.Health <= 0f || HealthComponent.Health > HealthComponent.MaxHealth )
						HealthComponent.Health = HealthComponent.MaxHealth;
				}

				LifePunchPropPhysics.SetupWorldMachine( GameObject, alignGround: true );
				_colliderSyncedFromModel = true;
				Log.Info( $"RACK_SPAWN_PHYSICS advanced={AdvancedRack} pos={GameObject.WorldPosition} mode=dev-world-machine" );
			}
			else
			{
				ApplyVirginSpawnDefaultsHost();
				if ( HealthComponent.IsValid() )
				{
					HealthComponent.MaxHealth = LpBitcoinIdent.RackMaxHealth;
					if ( HealthComponent.Health <= 0f || HealthComponent.Health > HealthComponent.MaxHealth )
						HealthComponent.Health = HealthComponent.MaxHealth;
				}

				_spawnDropGraceTicks = 45;
				_colliderSyncedFromModel = false;
				LifePunchPropPhysics.BeginGrabbablePrinterDrop( GameObject, syncColliderFromModel: false );
				Log.Info( $"RACK_SPAWN_PHYSICS advanced={AdvancedRack} pos={GameObject.WorldPosition} gravity=on (printer drop)" );
			}
		}
#endif
		_modelRenderer = Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
		                 ?? Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;
		_visuals = Components.Get<LpBitcoinRackVisuals>( FindMode.EverythingInSelf );
		if ( !_visuals.IsValid() )
		{
			_visuals = GameObject.AddComponent<LpBitcoinRackVisuals>();
			_visuals.Rack = this;
		}

		ApplyRackMiningVisual( IsMining );
	}

#if !LIFEPUNCH_LOCAL
	public override void OnOcclusionChanged( bool occlude )
	{
		base.OnOcclusionChanged( occlude );
		_visuals?.OnOcclusionChanged( occlude );
	}
#endif

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

		// COLLIDER: the prefab's authored BoxCollider is now the source of truth (defect 2, r3).
		// The runtime SyncBoxColliderFromModel call that used to live here overwrote it with the
		// model's render-bounds AABB every spawn. The measured boxes are baked into the prefabs,
		// so the sync is redundant — and keeping it would silently mask any future authoring error.
#endif
	}

	protected override void OnUpdate()
	{
		if ( IsMining != _lastMiningVisual )
			ApplyRackMiningVisual( IsMining );

		if ( !Networking.IsHost )
			return;

		if ( !_computeTierReconciled )
		{
			_computeTierReconciled = true;
			ReconcileComputeTierHost();
		}

		var hub = GetLinkedHub();
		if ( hub is null || !hub.IsPowered || !IsMining )
			return;

		MiningProgress = Math.Clamp( _sincePayout.Relative / LpBitcoinEconomy.PayoutIntervalSeconds, 0f, 1f );

		if ( _sincePayout < LpBitcoinEconomy.PayoutIntervalSeconds )
			return;

		_sincePayout = 0;
		BitcoinAmount += LpBitcoinEconomy.TickPayout( ClockGhz, CoreCount, YieldMultiplier );
		TryHandleCapacityHost();
		RefreshLinkedTerminalScreens();
	}

	private void TryHandleCapacityHost()
	{
		var capacity = LpBitcoinEconomy.RackBtcCapacityFor( this );
		if ( BitcoinAmount < capacity )
		{
			_capacityAlertSent = false;
			return;
		}

		BitcoinAmount = capacity;

		if ( IsMining )
			StopMiningHost();

		if ( _capacityAlertSent )
			return;

		_capacityAlertSent = true;
		var hub = GetLinkedHub();
		if ( hub is null )
			return;

		hub.PushRackCapacityAlertHost( hub.GetRackIndex( this ), BitcoinAmount, capacity );
	}

	public bool Press( IPressable.Event e ) => false;

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	public void LinkToHub( LpBitcoinHubEntity hub )
	{
		if ( !hub.IsValid() )
			return;

		if ( Networking.IsHost && !hub.IsPowered )
			return;

		LinkedHubId = hub.GameObject.Id;
#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost )
			StopMiningHost();
#endif
		// Link is a membership-change event: the hub sweep binds this rack's slot token
		// (lowest unoccupied) and re-reconciles EVERY linked rack (leak fix, GO 2026-07-09).
		if ( Networking.IsHost )
			hub.ReconcileLinkedRacksHost();

		hub.RefreshLinkedTerminalScreens();
	}

	/// <summary>Deliberate release (rig0 <c>unlink</c>): clear the hub link + slot binding,
	/// drop to stock, and sweep the hub. The slot's ledger records persist — this rack (or
	/// a replacement) re-binding the slot re-reads its ladder. Host-only; no explosion.</summary>
	internal void ReleaseFromHubHost()
	{
		if ( !Networking.IsHost )
			return;

		var hub = GetLinkedHub();
		StopMiningHost();
		LinkedHubId = Guid.Empty;
		AssignedSlotToken = "";

		// Unbound rack reads stock immediately (the hub sweep below only touches racks still
		// linked to it — this one just left that set, so reset its projection here).
		ComputeTier = 0;
		LpBitcoinComputeTrack.Apply( this, 0 );

		hub?.ReconcileLinkedRacksHost();
		hub?.RefreshLinkedTerminalScreens();
	}

	/// <summary>Ledger-wins rehydrate for the rack_compute projection (UPGRADE_ARC_DESIGN
	/// decision 9). Subject = the rack's OWN persisted <see cref="AssignedSlotToken"/>,
	/// guarded by subject class (GO ruling R1) — an unbound, class-mismatched, or unlinked
	/// occupant reads tier 0. Runs on first host tick after rehydrate and on every
	/// membership sweep; never derives the slot positionally (leak fix).</summary>
	internal void ReconcileComputeTierHost()
	{
		if ( !Networking.IsHost )
			return;

		// T3 latch (Packet O FLAG 2): feed this rack's content-config into the global one-shot
		// ladder latch. GetConfig is a BaseEntity read (absent on the LOCAL Component stub), so
		// under LIFEPUNCH_LOCAL we pass null and the latch uses shipped defaults.
		LpBitcoinRackConfig rackConfig = null;
#if !LIFEPUNCH_LOCAL
		rackConfig = GetConfig( new LpBitcoinRackConfig() );
#endif
		LpBitcoinComputeTrack.EnsureRegistered( rackConfig );

		var ledgerTier = 0;
		var hub = GetLinkedHub();
		if ( hub is not null && hub.Owner != 0 && !string.IsNullOrEmpty( AssignedSlotToken ) )
		{
			ledgerTier = LifePunchUpgradeLedger.MaxTier(
				hub.Owner, LpBitcoinComputeTrack.TrackId, AssignedSlotToken, LpBitcoinComputeTrack.ClassOf( this ) );
		}

		ComputeTier = LifePunchUpgradeLedger.ReconcileTier(
			ComputeTier, ledgerTier, $"{LpBitcoinComputeTrack.TrackId} rack={GameObject?.Name}" );

		// Effects are absolute — a rehydrated/relinked rack re-derives its rate from
		// tier alone, never from persisted floats (decision 2).
		LpBitcoinComputeTrack.Apply( this, ComputeTier );
	}

	public LpBitcoinHubEntity GetLinkedHub()
		=> LpBitcoinHubEntity.FindByGameObjectId( LinkedHubId, GameObject.Scene ?? Game.ActiveScene );

	public void RequestSetMining( bool on ) => SetMiningHost( on );

	[Rpc.Host]
	private void SetMiningHost( bool on )
	{
		var hub = GetLinkedHub();
		if ( hub is null || !hub.IsPowered || !hub.CanOperateTerminal( Rpc.CallerId ) )
		{
			IsMining = false;
			ApplyRackMiningVisual( false );
			RefreshLinkedTerminalScreens();
			return;
		}

		if ( on && BitcoinAmount >= LpBitcoinEconomy.RackBtcCapacityFor( this ) )
			on = false;

		IsMining = on;
		if ( IsMining )
			_sincePayout = 0;

		ApplyRackMiningVisual( IsMining );
		RefreshLinkedTerminalScreens();
	}

	public void RequestSell() => SellHost();

	[Rpc.Host]
	private async void SellHost() => await SellForCallerHost( Rpc.CallerId );

	internal async System.Threading.Tasks.Task SellForCallerHost( Guid callerId )
	{
		if ( BitcoinAmount <= 0f )
			return;

		var hub = GetLinkedHub();
		if ( hub is null || !hub.CanOperateTerminal( callerId ) )
			return;

		var soldBtc = BitcoinAmount;
		var payout = LpBitcoinEconomy.BtcToCashPayout( soldBtc, callerId );
		if ( payout.FinalUsd == 0 )
			return;

		// Zero the rack balance BEFORE the TryPayBank await, so a second sell in the same window
		// sees nothing to sell — the same debit-before-await fix as CashOutHubHost. Restore
		// ADDITIVELY on payment failure: mining may have added to the buffer during the await.
		BitcoinAmount = 0f;
		if ( !await LpBitcoinWallet.TryPayBank(
			callerId,
			payout.FinalUsd,
			payout.BuildLedgerReason( "LIFEPUNCH bitcoin sell" ) ) )
		{
			BitcoinAmount += soldBtc;
			Log.Info( $"LP_SELL_SENSOR restore caller={callerId} soldBtc={soldBtc:F8} — payment failed, rack balance now {BitcoinAmount:F8}" );
			return;
		}

		LpBitcoinPayoutAudit.RecordSuccessful( callerId, "rack-sell", payout );
		Log.Info( $"LP_SELL_SENSOR ok caller={callerId} soldBtc={soldBtc:F8} value={payout.FinalUsd} rackBalance={BitcoinAmount:F8}" );
	}

	internal void StopMiningHost()
	{
		IsMining = false;
		MiningProgress = 0f;
		ApplyRackMiningVisual( false );
		RefreshLinkedTerminalScreens();
	}

	internal void ClearBalanceHost()
	{
		BitcoinAmount = 0f;
		_capacityAlertSent = false;
		RefreshLinkedTerminalScreens();
	}

	/// <summary>Purchase the next COMPUTE tier for this rack — funnels to the ONE
	/// purchase path (LpBitcoinPurchaseFlow; slice 2, replaces the legacy CPU/core
	/// upgrade RPCs and the dual-entry problem with them).</summary>
	public void RequestPurchaseComputeTier() => PurchaseComputeTierHost();

	[Rpc.Host]
	private void PurchaseComputeTierHost()
	{
		var hub = GetLinkedHub();
		var result = LpBitcoinPurchaseFlow.PurchaseComputeTierHost( hub, this, Rpc.CallerId );
		hub?.NotifyPurchaseResultToCaller( Rpc.CallerId, result );
	}

	private void ApplyRackMiningVisual( bool mining )
	{
		_lastMiningVisual = mining;
#if !LIFEPUNCH_LOCAL
		_visuals?.SyncMiningVisualState();
#endif
	}

	private void RefreshLinkedTerminalScreens()
	{
		GetLinkedHub()?.RefreshLinkedTerminalScreens();
	}

	/// <summary>Market spawn baseline — unlinked racks stay idle until registered at rig0.</summary>
	private void ApplyVirginSpawnDefaultsHost()
	{
		if ( LinkedHubId != Guid.Empty )
			return;

		IsMining = false;
		MiningProgress = 0f;
	}

	internal static LpBitcoinRackEntity FindNearestUnlinked( LpBitcoinHubEntity hub, float maxRange )
		=> FindNearestUnlinked( hub, maxRange, advancedOnly: null );

	/// <param name="advancedOnly">null = any rack; true = advanced only; false = standard GPU rack only.</param>
	internal static LpBitcoinRackEntity FindNearestUnlinked(
		LpBitcoinHubEntity hub,
		float maxRange,
		bool? advancedOnly )
	{
		if ( !hub.IsValid() )
			return null;

		var scene = hub.GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return null;

		LpBitcoinRackEntity best = null;
		var bestDist = float.MaxValue;
		var hubPos = hub.WorldPosition;

		foreach ( var rack in scene.GetAllComponents<LpBitcoinRackEntity>() )
		{
			if ( !rack.IsValid() || rack.LinkedHubId != Guid.Empty )
				continue;

			if ( advancedOnly.HasValue && rack.AdvancedRack != advancedOnly.Value )
				continue;

#if !LIFEPUNCH_LOCAL
			if ( hub.Owner != 0 && !LifePunchEntityOwnership.SharesOperator( hub.Owner, rack.Owner ) )
				continue;
#endif

			var dist = hubPos.Distance( rack.WorldPosition );
			if ( dist > maxRange || dist >= bestDist )
				continue;

			bestDist = dist;
			best = rack;
		}

		return best;
	}

	internal static IReadOnlyList<LpBitcoinRackEntity> FindAllUnlinked( LpBitcoinHubEntity hub, float maxRange )
	{
		if ( !hub.IsValid() )
			return Array.Empty<LpBitcoinRackEntity>();

		var scene = hub.GameObject.Scene ?? Game.ActiveScene;
		if ( scene is null )
			return Array.Empty<LpBitcoinRackEntity>();

		var matches = new List<(LpBitcoinRackEntity Rack, float Distance)>();
		var hubPos = hub.WorldPosition;

		foreach ( var rack in scene.GetAllComponents<LpBitcoinRackEntity>() )
		{
			if ( !rack.IsValid() || rack.LinkedHubId != Guid.Empty )
				continue;

#if !LIFEPUNCH_LOCAL
			if ( hub.Owner != 0 && !LifePunchEntityOwnership.SharesOperator( hub.Owner, rack.Owner ) )
				continue;
#endif

			var dist = hubPos.Distance( rack.WorldPosition );
			if ( dist > maxRange )
				continue;

			matches.Add( (rack, dist) );
		}

		if ( matches.Count == 0 )
			return Array.Empty<LpBitcoinRackEntity>();

		matches.Sort( ( left, right ) => left.Distance.CompareTo( right.Distance ) );
		return matches.ConvertAll( match => match.Rack );
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
			StopMiningHost();
			BitcoinAmount = 0f;
			LifePunchMachineDestroyFx.SpawnPrinterStyleExplosion( this, Explosion, WorldPosition );
		}

		// Death frees the slot (membership-change event): the slot's ledger records
		// survive — a replacement rack bound into the freed slot inherits its ladder
		// (R1 survival property). Clear the binding FIRST so the sweep's scan excludes us.
		if ( Networking.IsHost && LinkedHubId != Guid.Empty )
		{
			var hub = GetLinkedHub();
			LinkedHubId = Guid.Empty;
			AssignedSlotToken = "";
			hub?.ReconcileLinkedRacksHost();
		}

		base.OnDestroyed();
	}
#endif
}
