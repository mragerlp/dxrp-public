using Dxura.RP.Game.UI;
using Dxura.RP.Shared;
using Sandbox.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public partial class Player
{
	/// <summary>
	///     Players current cash balance
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	[Order( -100 )]
	[Property]
	[Group( "State" )]
	public uint WalletBalance { get; private set; }

	[Sync( SyncFlags.FromHost )]
	[Order( -100 )]
	[Property]
	[Group( "State" )]
	public uint BankBalance { get; private set; }

	[Sync( SyncFlags.FromHost )]
	[Property]
	[Group( "State" )]
	public int Level { get; set; }

	[Sync( SyncFlags.FromHost )]
	[Property]
	[Group( "State" )]
	public string? CustomJob { get; set; }

	[Sync( SyncFlags.FromHost )]
	[Property]
	[Group( "State" )]
	public bool Restricted { get; set; }

	[Sync( SyncFlags.FromHost )]
	[Property]
	[Group( "State" )]
	private SitType? Sit { get; set; }
	public bool Sitting => Sit != null;
	public bool Kneeling => Sit == SitType.Kneel;
	public bool KneelingRevive => Sit == SitType.KneelRevive;

	private Vector3 _sitReturnPosition = Vector3.Zero;

	private readonly SemaphoreSlim _transactionLock = new( 1, 1 );
	public readonly SemaphoreSlim PurchaseLock = new( 1, 1 );

	private void OnUpdateRoleplay()
	{
		if ( Input.Pressed( "Sit" ) )
		{
			if ( Sitting )
			{
				SetSit( null );
				return;
			}

			if ( Cooldown.Current.CheckAndStartCooldown( $"{SteamId}:sit", Config.Current.Game.PlayerSitCooldown, true ) )
			{
				return;
			}
			

			SetSit( SitType.Sit );
		}

		if ( Sitting && Input.Pressed( "Duck" ) )
		{
			SetSit( null );
			return;
		}

		// Press Shift while sitting to toggle kneeling
		if ( Sitting && Input.Pressed( "Run" ) )
		{
			SetSit( SitType.Kneel );
		}
	}


	// Preserve the original CLR signature for already-compiled economy callers.
	public Task<bool> ChargeHost( uint amount, string reason, bool useBank = false )
		=> ChargeHost( amount, reason, useBank, null );

	public async Task<bool> ChargeHost( uint amount, string reason, bool useBank, long? auditActorSteamId )
	{
		Assert.True( Networking.IsHost );

		if ( !Config.Current.Game.MoneyEnabled )
		{
			return !auditActorSteamId.HasValue;
		}

		// Every mutation below eventually converts the unsigned amount (or one of its portions)
		// to int. Reject values outside that domain before any balance or API mutation; otherwise
		// the cast can wrap and turn a charge into a credit.
		if ( amount == 0 || amount > int.MaxValue )
		{
			return amount == 0;
		}

		await _transactionLock.WaitAsync();
		try
		{
			if ( !Config.Current.Game.MoneyEnabled )
			{
				return !auditActorSteamId.HasValue;
			}

			if ( auditActorSteamId is long actorSteamId
			     && (SyntheticActorRegistry.IsSynthetic( SteamId )
			         || SyntheticActorRegistry.IsSynthetic( actorSteamId )
			         || !RankSystem.HasPermission( actorSteamId, Permission.ManageEconomy )
			         || !RankSystem.CanTarget( actorSteamId, SteamId )) )
			{
				return false;
			}

			bool didCharge;

			if ( useBank )
			{
				if ( amount <= BankBalance )
				{
					didCharge = await ServerApiClient.ModifyPlayerBalance( SteamId, -(int)amount, reason );
					if ( didCharge )
					{
						BankBalance -= amount;
						this.Money( -(int)amount, true );
					}
				}
				else
				{
					if ( (ulong)BankBalance + WalletBalance < amount )
					{
						this.Error( "#notify.cash.poor" );
						return false;
					}

					var bankPortion = BankBalance;
					var walletPortion = amount - BankBalance;

					didCharge = await ServerApiClient.ModifyPlayerBalance( SteamId, -(int)bankPortion, $"{reason} (${amount} total, ${walletPortion} from wallet)" );
					if ( didCharge )
					{
						if ( WalletBalance < walletPortion )
						{
							_ = ServerApiClient.ModifyPlayerBalance( SteamId, (int)bankPortion, $"Refund: wallet balance changed during charge ({reason})" );
							return false;
						}

						BankBalance -= bankPortion;
						WalletBalance -= walletPortion;
						this.Money( -(int)bankPortion, true );
						this.Money( -(int)walletPortion );
					}
				}
			}
			else
			{
				if ( WalletBalance < amount )
				{
					this.Error( "#notify.cash.poor" );
					return false;
				}

				WalletBalance -= amount;
				didCharge = true;
				this.Money( -(int)amount );
				_ = ServerApiClient.Audit( "WalletCharge", $"{SteamName} ({SteamId}) charged ${amount} from wallet: {reason}", auditActorSteamId ?? SteamId );
			}

			if ( !didCharge )
			{
				this.Error( "#notify.cash.poor" );
				return false;
			}

			return true;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	internal async Task<bool> RestoreWalletAfterFailedBankAll( uint amount, long auditActorSteamId )
	{
		Assert.True( Networking.IsHost );

		if ( amount == 0 || amount > int.MaxValue )
		{
			return amount == 0;
		}

		await _transactionLock.WaitAsync();
		try
		{
			if ( uint.MaxValue - WalletBalance < amount )
			{
				return false;
			}

			WalletBalance += amount;
			this.Money( (int)amount );
			_ = ServerApiClient.Audit(
				"BankAllRollback",
				$"{SteamName} ({SteamId}) had ${amount} restored to wallet after BankAll could not complete.",
				auditActorSteamId );
			return true;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	// Keep the original three-argument CLR signature for already-compiled addons. Optional
	// parameters are only source-compatible; replacing this method would break them at runtime.
	public Task<bool> PayHost( uint amount, string reason, bool inBank = false )
		=> PayHost( amount, reason, inBank, null );

	public async Task<bool> PayHost( uint amount, string reason, bool inBank, long? auditActorSteamId )
	{
		Assert.True( Networking.IsHost );

		// Every durable balance and notification path below crosses an Int32 boundary. Reject values
		// that cannot be represented instead of allowing the cast to wrap into a negative mutation.
		if ( amount > int.MaxValue )
		{
			return false;
		}

		if ( !Config.Current.Game.MoneyEnabled )
		{
			return true;
		}

		if ( amount == 0 )
		{
			return true;
		}

		await _transactionLock.WaitAsync();
		try
		{
			var didPay = false;

			if ( inBank ) // Deposit directly into bank
			{
				if ( uint.MaxValue - BankBalance < amount )
				{
					this.Error( "#generic.error" );
					return false;
				}

				if ( await ServerApiClient.ModifyPlayerBalance( SteamId, (int)amount, reason ) )
				{
					didPay = true;
					BankBalance += amount;
					this.Money( (int)amount, true );
					_ = ServerApiClient.Audit( "BankDeposit", $"{SteamName} ({SteamId}) received ${amount} into bank: {reason}", auditActorSteamId ?? SteamId );
				}
			}
			else // Pay into wallet
			{
				if ( uint.MaxValue - WalletBalance < amount )
				{
					this.Error( "#generic.error" );
					return false;
				}

				WalletBalance += amount;
				didPay = true;
				this.Money( (int)amount );
				_ = ServerApiClient.Audit( "WalletDeposit", $"{SteamName} ({SteamId}) received ${amount} into wallet: {reason}", auditActorSteamId ?? SteamId );
			}

			if ( !didPay )
			{
				this.Error( "#generic.error" );
				return false;
			}

			return true;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	/// <summary>
	/// Staff/admin grant path whose success means the linked server rail was still available after
	/// entering the target's transaction lock. Ordinary gameplay PayHost keeps its historical neutral
	/// offline semantics; this stricter seam is what UI confirmations and bulk staff banking require.
	/// </summary>
	internal async Task<StrictMoneyMutationResult> PayHostStrictAudited( uint amount, string reason, bool inBank, long auditActorSteamId )
	{
		Assert.True( Networking.IsHost );

		if ( amount == 0 || amount > int.MaxValue )
		{
			return StrictMoneyMutationResult.Rejected;
		}

		await _transactionLock.WaitAsync();
		try
		{
			if ( !Config.Current.Game.MoneyEnabled
			     || !ServerApiLink.HasAuthorizationKey
			     || SyntheticActorRegistry.IsSynthetic( SteamId )
			     || SyntheticActorRegistry.IsSynthetic( auditActorSteamId )
			     || !RankSystem.HasPermission( auditActorSteamId, Permission.ManageEconomy )
			     || !RankSystem.CanTarget( auditActorSteamId, SteamId ) )
			{
				return StrictMoneyMutationResult.Rejected;
			}

			if ( inBank )
			{
				if ( uint.MaxValue - BankBalance < amount )
				{
					return StrictMoneyMutationResult.Rejected;
				}

				var bankResult = await ServerApiClient.ModifyPlayerBalanceStrict( SteamId, (int)amount, reason );
				if ( bankResult != StrictMoneyMutationResult.Applied )
				{
					return bankResult;
				}

				BankBalance += amount;
				this.Money( (int)amount, true );
				_ = ServerApiClient.Audit( "BankDeposit", $"{SteamName} ({SteamId}) received ${amount} into bank: {reason}", auditActorSteamId );
				return StrictMoneyMutationResult.Applied;
			}

			if ( uint.MaxValue - WalletBalance < amount )
			{
				return StrictMoneyMutationResult.Rejected;
			}

			var auditDescription = $"{SteamName} ({SteamId}) received ${amount} into wallet: {reason}";
			if ( !ServerApiClient.TryQueueAuditStrict( "WalletDeposit", auditDescription, auditActorSteamId ) )
			{
				return StrictMoneyMutationResult.Rejected;
			}

			WalletBalance += amount;
			this.Money( (int)amount );
			return StrictMoneyMutationResult.Applied;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Strict money mutation became indeterminate for {SteamName} ({SteamId}): {e.Message}" );
			return StrictMoneyMutationResult.Unknown;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	public void ForceApiRefresh()
	{
		var callerId = Rpc.CallerId;

		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:api:refresh", Config.Current.Game.ActionLongCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			if ( !Config.Current.Game.MoneyEnabled )
			{
				return;
			}

			var initResponse = await ServerApiClient.InitializePlayer( new InitalizePlayerDto
			{
				Id = player.SteamId, Name = player.DisplayName
			} );

			if ( initResponse == null )
			{
				this.Error( "#generic.error" );
				return;
			}

			await GameTask.MainThread();

			await _transactionLock.WaitAsync();
			try
			{
				BankBalance = initResponse.Balance;
			}
			finally
			{
				_transactionLock.Release();
			}

			Level = initResponse.Level;
			PlayTime = initResponse.Playtime * 60f;
			FactionId = initResponse.FactionId;
			FactionRoleId = initResponse.FactionRoleId;
		} );
	}

	public async Task<uint> ClearWalletHost()
	{
		Assert.True( Networking.IsHost );
		await _transactionLock.WaitAsync();
		try
		{
			var amount = WalletBalance;
			var remaining = amount;
			while ( remaining > 0 )
			{
				var chunk = (int)Math.Min( remaining, (uint)int.MaxValue );
				this.Money( -chunk );
				remaining -= (uint)chunk;
			}
			WalletBalance = 0;
			return amount;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	public async Task ClearWalletAndDropHost( Vector3 position, string reason )
	{
		var amount = await ClearWalletHost();
		if ( amount > 0 )
		{
			GameManager.Instance.DropMoneyHost( amount, position, reason );
		}
	}

	public async Task SetBankBalanceHost( uint balance )
	{
		Assert.True( Networking.IsHost );
		await _transactionLock.WaitAsync();
		try
		{
			BankBalance = balance;
		}
		finally
		{
			_transactionLock.Release();
		}
	}

	private bool CanSit()
	{
		if ( Restricted )
		{
			return false;
		}

		if ( HasStatus( Constants.SurrenderStatus ) )
		{
			return false;
		}

		return true;
	}

	private void ClearSitStateHost( bool returnToSavedPosition )
	{
		Assert.True( Networking.IsHost );

		if ( !Sitting )
		{
			_sitReturnPosition = Vector3.Zero;
			return;
		}

		var previousState = Sit;
		Sit = null;

		if ( !returnToSavedPosition || _sitReturnPosition == Vector3.Zero )
		{
			_sitReturnPosition = Vector3.Zero;
			return;
		}

		var distanceToReturnPosition = WorldPosition.Distance( _sitReturnPosition );
		if ( distanceToReturnPosition <= Config.Current.Game.ReachDistance * 2 )
		{
			var returnRotation = previousState == SitType.Sit ? Rotation.FromYaw( WorldRotation.Yaw() + 180 ) : WorldRotation;
			Teleport( new Transform( _sitReturnPosition, returnRotation ) );
		}

		_sitReturnPosition = Vector3.Zero;
	}

	[Rpc.Host]
	public void SetSit( SitType? newState )
	{
		var callerId = Rpc.CallerId;
		var ignoreCooldowns = Rpc.Caller.IsHost;

		// Ensure the caller is the owner or host
		if ( !GameObject.IsValid() || Rpc.Caller != Network.Owner && !Rpc.Caller.IsHost )
		{
			return;
		}
		
		if ( newState == Sit )
		{
			return; // No state change
		}

		if ( !ignoreCooldowns )
		{
			// Handle cooldowns based on state transition
			switch ( newState )
			{
				case SitType.Sit when !Sitting:
					if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:sit", Config.Current.Game.PlayerSitCooldown ) )
					{
						return;
					}
					break;
				case SitType.Kneel when !Kneeling:
					if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:kneel", Config.Current.Game.PlayerSitCooldown ) )
					{
						return;
					}
					break;
				case SitType.KneelRevive:
					if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:kneeling_revive", Config.Current.Game.ActionCooldown ) )
					{
						return;
					}
					break;
				case null:
					if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:stand_position", Config.Current.Game.ActionCooldown ) )
					{
						return;
					}
					break;
				default:
					if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:sit:default", Config.Current.Game.ActionCooldown ) )
					{
						return;
					}
					break;
			}
		}
		
		// Check if we're allowed to sit (based on our state)
		if ( !Sitting && !CanSit() )
		{
			return;
		}

		switch ( newState )
		{
			// Validate sitting conditions
			case SitType.Sit when !Sitting:
				{
					// On headless server, controller is disabled so skip its state checks.
					if ( !GameManager.IsHeadless && ( Controller.IsDucking || !Controller.IsOnGround ) )
					{
						this.Warn( "#generic.sit.invalid" );
						return;
					}

					if ( AimRay.Forward.z > 0.25f )
					{
						this.Warn( "#generic.sit.invalid" );
						return;
					}

					var hit = Scene.Trace.Ray( AimRay, Config.Current.Game.ReachDistance )
						.IgnoreGameObjectHierarchy( GameObject )
						.WithoutTags( Constants.RagdollTag, "movement" )
						.Size( 5f )
						.Run();

					if ( !hit.Hit )
					{
						this.Warn( "#generic.sit.invalid" );
						return;
					}

					const float radius = 10f;
					const float height = 40f;
					const float bottomHeightBuffer = 12f;
					var box = new BBox(
						hit.HitPosition + Vector3.Up * bottomHeightBuffer - Vector3.One * radius,
						hit.HitPosition + Vector3.Up * height + Vector3.One * radius
					);

					var colliders = Scene.FindInPhysics( box ).Where( x => x != GameObject );
					if ( colliders.Any() )
					{
						this.Warn( "#generic.sit.invalid" );
						return;
					}

					_sitReturnPosition = WorldPosition;
					Sit = SitType.Sit;
					Teleport( new Transform( hit.HitPosition + Vector3.Up * -16f, Rotation.FromYaw( WorldRotation.Yaw() + 180 ) ) );
					return;
				}
			case SitType.Kneel or SitType.KneelRevive when !Sitting:
				_sitReturnPosition = WorldPosition;
				Sit = newState;
				return;
		}

		var previousState = Sit;
		Sit = newState;

		// Handle position adjustments
		switch (previousState, newState)
		{
			case (SitType.Sit, SitType.Kneel):
			case (SitType.Sit, SitType.KneelRevive):
				Teleport( new Transform( WorldPosition + Vector3.Up * 15f, WorldRotation ) );
				break;
			case (SitType.Kneel, SitType.Sit):
			case (SitType.KneelRevive, SitType.Sit):
				Teleport( new Transform( WorldPosition + Vector3.Up * -15f, WorldRotation ) );
				break;
			case (_, null):
				Sit = previousState;
				ClearSitStateHost( returnToSavedPosition: true );
				break;
		}
	}



}
