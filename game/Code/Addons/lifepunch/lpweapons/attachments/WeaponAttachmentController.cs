using Dxura.RP.Game;
using Dxura.RP.Shared;
using System.Threading;
using System.Threading.Tasks;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

[Title( "Weapon Attachment Controller" )]
[Group( "LifePunch Weapons" )]
public sealed class WeaponAttachmentController : Component
{
	private static readonly SemaphoreSlim TransactionLock = new( 1, 1 );
	private static readonly WeaponAttachmentReconciliationQuarantine ReconciliationQuarantine = new();

	[Property]
	public string WeaponId { get; set; } = string.Empty;

	[Property]
	public bool InventoryTransactionsEnabled { get; set; }

	[Property]
	[RequireComponent]
	public required Equipment Equipment { get; set; }

	[Property]
	[RequireComponent]
	public required WeaponAttachmentState AttachmentState { get; set; }

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void AttachHost( WeaponAttachmentKind kind, Guid itemId )
	{
		if ( !TryGetCallerOwner( out var owner ) )
		{
			return;
		}

		_ = TryAttachForHost( owner, kind, itemId );
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void DetachHost( WeaponAttachmentSlot slot )
	{
		if ( !TryGetCallerOwner( out var owner ) )
		{
			return;
		}

		_ = TryDetachForHost( owner, slot );
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void SetLaserEnabledHost( bool enabled )
	{
		if ( !TryGetCallerOwner( out var owner ) )
		{
			return;
		}

		_ = TrySetLaserEnabledForHost( owner, enabled );
	}

	public Task<WeaponAttachmentTransactionResult> TryAttachForHost(
		Player owner,
		WeaponAttachmentKind kind,
		Guid itemId )
	{
		if ( IsReconciliationRequired( owner ) )
		{
			return Task.FromResult( ReconciliationRequiredResult() );
		}

		if ( !CanMutate( owner )
		     || itemId == Guid.Empty
		     || !WeaponAttachmentSlots.TryGetSlot( kind, out _ ) )
		{
			return Task.FromResult( WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest ) );
		}

		return AttachAsync( owner, kind, itemId );
	}

	public Task<WeaponAttachmentTransactionResult> TryDetachForHost(
		Player owner,
		WeaponAttachmentSlot slot )
	{
		if ( IsReconciliationRequired( owner ) )
		{
			return Task.FromResult( ReconciliationRequiredResult() );
		}

		if ( !CanMutate( owner )
		     || (slot != WeaponAttachmentSlot.Muzzle
		         && slot != WeaponAttachmentSlot.Optic
		         && slot != WeaponAttachmentSlot.Rail) )
		{
			return Task.FromResult( WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest ) );
		}

		return DetachAsync( owner, slot );
	}

	public Task<WeaponAttachmentTransactionResult> TrySetLaserEnabledForHost(
		Player owner,
		bool enabled )
	{
		if ( IsReconciliationRequired( owner ) )
		{
			return Task.FromResult( ReconciliationRequiredResult() );
		}

		if ( !CanMutate( owner ) )
		{
			return Task.FromResult( WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest ) );
		}

		return SetLaserEnabledAsync( owner, enabled );
	}

	private async Task<WeaponAttachmentTransactionResult> AttachAsync(
		Player owner,
		WeaponAttachmentKind kind,
		Guid itemId )
	{
		var ownerId = owner.SteamId;
		await TransactionLock.WaitAsync();
		try
		{
			await GameTask.MainThread();
			if ( IsReconciliationRequired( ownerId ) )
			{
				return ReconciliationRequiredResult();
			}

			if ( !Equipment.IsValid() )
			{
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.InvalidRequest );
			}

			var inventoryTaken = false;
			try
			{
				Equipment.IsDropLocked = true;
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				var inventory = await ServerApiClient.GetPlayerInventory( owner.SteamId );
				await GameTask.MainThread();
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				if ( inventory is null )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InventoryTakeFailed );
				}

				var inventoryItem = inventory.FirstOrDefault( item =>
					item.Definition.Id == itemId
						&& item.Quantity > 0
						&& item.Definition.Type == ItemType.Accessory
						&& WeaponAttachmentCatalog.MatchesGrantIdentifier(
							kind,
							item.Definition.GrantIdentifier ) );
				if ( inventoryItem is null )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InventoryTakeFailed );
				}

				var expected = AttachmentState.Current;
				var inventoryQuantity = InventoryQuantity( inventory, itemId );
				var stagedInventory = new BufferedInventoryGateway( itemId, inventoryQuantity );
				var stagedState = new BufferedStateGateway( expected );
				var stagedResult = WeaponAttachmentTransactionController.Attach(
					WeaponId,
					kind,
					itemId,
					stagedInventory,
					stagedState );
				if ( !stagedResult.Succeeded )
				{
					return stagedResult;
				}

				inventoryTaken = true;
				var takeSucceeded = await ServerApiClient.TakePlayerItem( owner.SteamId, new TakeItemDto
				{
					ItemId = itemId,
					Quantity = 1
				} );
				await GameTask.MainThread();
				if ( !takeSucceeded )
				{
					RequireReconciliation( ownerId );
					Log.Error(
						$"Weapon attachment take outcome was not acknowledged for {owner.SteamId}: " +
						$"attach {kind}. Inventory reconciliation is required before retrying." );
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.CompensationFailed,
						true );
				}

				var expectedQuantityAfterTake = Math.Max( 0, inventoryQuantity - 1 );
				if ( CanMutate( owner ) && AttachmentState.TryReplace( expected, stagedState.Current ) )
				{
					inventoryTaken = false;
					BroadcastInventoryRefreshBestEffort( owner, itemId, expectedQuantityAfterTake );
					return WeaponAttachmentTransactionResult.Success;
				}

				var compensation = await CompensateTakenItemAsync(
					owner.SteamId,
					itemId,
					$"state-rejected attach {kind}" );

				if ( compensation.Succeeded && compensation.ActualQuantity >= 0 )
				{
					inventoryTaken = false;
					BroadcastInventoryRefreshBestEffort( owner, itemId, compensation.ActualQuantity );

					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.StateCommitFailed );
				}

				RequireReconciliation( ownerId );
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.CompensationFailed,
					true );
			}
			catch ( Exception exception )
			{
				if ( inventoryTaken )
				{
					RequireReconciliation( ownerId );
				}

				await GameTask.MainThread();
				Log.Error( $"Weapon attachment attach transaction failed: {exception.Message}" );
				return WeaponAttachmentTransactionResult.Failed(
					inventoryTaken
						? WeaponAttachmentTransactionFailure.CompensationFailed
						: WeaponAttachmentTransactionFailure.InventoryTakeFailed,
					inventoryTaken );
			}
			finally
			{
				if ( Equipment.IsValid() )
				{
					Equipment.IsDropLocked = false;
				}
			}
		}
		finally
		{
			TransactionLock.Release();
		}
	}

	private async Task<WeaponAttachmentTransactionResult> DetachAsync(
		Player owner,
		WeaponAttachmentSlot slot )
	{
		var ownerId = owner.SteamId;
		await TransactionLock.WaitAsync();
		try
		{
			await GameTask.MainThread();
			if ( IsReconciliationRequired( ownerId ) )
			{
				return ReconciliationRequiredResult();
			}

			if ( !Equipment.IsValid() )
			{
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.InvalidRequest );
			}

			var stateDetached = false;
			try
			{
				Equipment.IsDropLocked = true;
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				var expected = AttachmentState.Current;
				var detached = expected.Get( slot );
				if ( !detached.IsPresent )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.SlotEmpty );
				}

				var definition = await ServerApiClient.GetItemDefinition( detached.ItemId );
				await GameTask.MainThread();
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				if ( definition is null )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InventoryGiveFailed );
				}

				if ( definition.Type != ItemType.Accessory
				     || !WeaponAttachmentCatalog.MatchesGrantIdentifier(
					     detached.Kind,
					     definition.GrantIdentifier ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.Incompatible );
				}

				var inventory = await ServerApiClient.GetPlayerInventory( owner.SteamId );
				await GameTask.MainThread();
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				if ( inventory is null )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InventoryGiveFailed );
				}

				var inventoryQuantity = InventoryQuantity( inventory, detached.ItemId );
				var stagedInventory = new BufferedInventoryGateway(
					detached.ItemId,
					inventoryQuantity );
				var stagedState = new BufferedStateGateway( expected );
				var stagedResult = WeaponAttachmentTransactionController.Detach(
					slot,
					stagedInventory,
					stagedState );
				if ( !stagedResult.Succeeded )
				{
					return stagedResult;
				}

				stateDetached = true;
				if ( !AttachmentState.TryReplace( expected, stagedState.Current ) )
				{
					stateDetached = false;
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.StateCommitFailed );
				}

				var givenItem = await ServerApiClient.GivePlayerItem( owner.SteamId, new GiveItemDto
				{
					ItemId = detached.ItemId,
					Quantity = 1
				} );
				await GameTask.MainThread();
				if ( givenItem is not null )
				{
					stateDetached = false;
					BroadcastInventoryRefreshBestEffort( owner, detached.ItemId, givenItem.Quantity );
					return WeaponAttachmentTransactionResult.Success;
				}

				RequireReconciliation( ownerId );
				Log.Error(
					$"Weapon attachment give outcome was not acknowledged for {owner.SteamId}: detach {slot}. " +
					"State remains detached to prevent duplicate ownership." );
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.CompensationFailed,
					true );
			}
			catch ( Exception exception )
			{
				if ( stateDetached )
				{
					RequireReconciliation( ownerId );
				}

				await GameTask.MainThread();
				Log.Error( $"Weapon attachment detach transaction failed: {exception.Message}" );
				return WeaponAttachmentTransactionResult.Failed(
					stateDetached
						? WeaponAttachmentTransactionFailure.CompensationFailed
						: WeaponAttachmentTransactionFailure.InventoryGiveFailed,
					stateDetached );
			}
			finally
			{
				if ( Equipment.IsValid() )
				{
					Equipment.IsDropLocked = false;
				}
			}
		}
		finally
		{
			TransactionLock.Release();
		}
	}

	private async Task<WeaponAttachmentTransactionResult> SetLaserEnabledAsync(
		Player owner,
		bool enabled )
	{
		var ownerId = owner.SteamId;
		await TransactionLock.WaitAsync();
		try
		{
			await GameTask.MainThread();
			if ( IsReconciliationRequired( ownerId ) )
			{
				return ReconciliationRequiredResult();
			}

			if ( !Equipment.IsValid() )
			{
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.InvalidRequest );
			}

			try
			{
				Equipment.IsDropLocked = true;
				if ( !CanMutate( owner ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.InvalidRequest );
				}

				var expected = AttachmentState.Current;
				if ( !expected.TrySetLaserEnabled( enabled, out var replacement ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.SlotEmpty );
				}

				if ( !AttachmentState.TryReplace( expected, replacement ) )
				{
					return WeaponAttachmentTransactionResult.Failed(
						WeaponAttachmentTransactionFailure.StateCommitFailed );
				}

				return WeaponAttachmentTransactionResult.Success;
			}
			catch ( Exception exception )
			{
				await GameTask.MainThread();
				Log.Error( $"Weapon attachment laser transaction failed: {exception.Message}" );
				return WeaponAttachmentTransactionResult.Failed(
					WeaponAttachmentTransactionFailure.StateCommitFailed );
			}
			finally
			{
				if ( Equipment.IsValid() )
				{
					Equipment.IsDropLocked = false;
				}
			}
		}
		finally
		{
			TransactionLock.Release();
		}
	}

	private static void BroadcastInventoryRefreshBestEffort( Player owner, Guid itemId, int quantity )
	{
		if ( !owner.IsValid() )
		{
			return;
		}

		try
		{
			owner.BroadcastInventoryRefresh( itemId, quantity );
		}
		catch ( Exception exception )
		{
			Log.Warning(
				$"Weapon attachment inventory refresh failed for item {itemId}: {exception.Message}" );
		}
	}

	private bool TryGetCallerOwner( out Player owner )
	{
		var caller = GameUtils.GetPlayerByConnectionId( Rpc.CallerId );
		owner = caller!;
		return CanMutate( caller );
	}

	private bool CanMutate( Player? owner )
	{
		return Networking.IsHost
			&& InventoryTransactionsEnabled
			&& !string.IsNullOrWhiteSpace( WeaponId )
			&& Equipment.IsValid()
			&& WeaponAttachmentCompatibility.MatchesEquipmentPrefab( WeaponId, Equipment.Resource.PrefabPath() )
			&& AttachmentState.IsValid()
			&& owner.IsValid()
			&& !owner.IsDead
			&& !SyntheticActorRegistry.IsSynthetic( owner.SteamId, owner.IsDebugPlayer )
			&& Equipment.Owner == owner
			&& Equipment.IsDeployed
			&& owner.CurrentEquipment == Equipment
			&& !IsReconciliationRequired( owner );
	}

	private static bool IsReconciliationRequired( Player? owner )
	{
		return owner.IsValid() && IsReconciliationRequired( owner.SteamId );
	}

	private static bool IsReconciliationRequired( long ownerId )
	{
		return ReconciliationQuarantine.IsRequired( ownerId );
	}

	private static WeaponAttachmentTransactionResult ReconciliationRequiredResult()
	{
		return WeaponAttachmentTransactionResult.Failed(
			WeaponAttachmentTransactionFailure.ReconciliationRequired,
			true );
	}

	private static void RequireReconciliation( long ownerId )
	{
		ReconciliationQuarantine.Require( ownerId );
	}

	private static int InventoryQuantity( IEnumerable<InventoryItemDto> inventory, Guid itemId )
	{
		return inventory
			.Where( item => item.Definition.Id == itemId )
			.Sum( item => item.Quantity );
	}

	private static async Task<InventoryCompensationResult> CompensateTakenItemAsync(
		long playerId,
		Guid itemId,
		string reason )
	{
		var refunded = await ServerApiClient.GivePlayerItem( playerId, new GiveItemDto
		{
			ItemId = itemId,
			Quantity = 1
		} );
		await GameTask.MainThread();
		if ( refunded is null )
		{
			Log.Error( $"Weapon attachment compensation API failed for {playerId}: {reason}." );
			return new InventoryCompensationResult( false, -1 );
		}

		return new InventoryCompensationResult( true, refunded.Quantity );
	}

	private readonly record struct InventoryCompensationResult(
		bool Succeeded,
		int ActualQuantity );

	private sealed class BufferedInventoryGateway : IWeaponAttachmentInventoryGateway
	{
		private readonly Guid _itemId;

		public BufferedInventoryGateway( Guid itemId, int quantity )
		{
			_itemId = itemId;
			Quantity = Math.Max( 0, quantity );
		}

		public int Quantity { get; private set; }

		public bool TryTake( Guid itemId, int quantity )
		{
			if ( itemId != _itemId || quantity != 1 || Quantity < quantity )
			{
				return false;
			}

			Quantity -= quantity;
			return true;
		}

		public bool TryGive( Guid itemId, int quantity )
		{
			if ( itemId != _itemId || quantity != 1 )
			{
				return false;
			}

			Quantity += quantity;
			return true;
		}
	}

	private sealed class BufferedStateGateway : IWeaponAttachmentStateGateway
	{
		public BufferedStateGateway( WeaponAttachmentLoadout current )
		{
			Current = current;
		}

		public WeaponAttachmentLoadout Current { get; private set; }

		public bool TryReplace( WeaponAttachmentLoadout expected, WeaponAttachmentLoadout replacement )
		{
			if ( Current != expected )
			{
				return false;
			}

			Current = replacement;
			return true;
		}
	}
}
