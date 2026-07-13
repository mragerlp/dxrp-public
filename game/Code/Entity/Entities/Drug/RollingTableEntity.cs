namespace Dxura.RP.Game.Entities;

public sealed class RollingTableEntityConfig
{
	public float AutoProcessInterval { get; init; } = 30f;
	public uint AutoProcessCost { get; init; } = 2000;
	public uint UnprocessedWeedCapacity { get; init; } = 6;
	public uint ProcessedWeedCapacity { get; init; } = 6;
	public uint JointOutputCount { get; init; } = 3;
	public uint BundleOutputCount { get; init; } = 1;
}

[Title( "Rolling Table" )]
[Category( "Entities" )]
public class RollingTableEntity : BaseEntity, Component.ITriggerListener, IGameEvents
{
	[Property]
	[Sync( SyncFlags.FromHost )]
	public uint UnprocessedWeed { get; set; }

	[Property]
	[Sync( SyncFlags.FromHost )]
	public uint ProcessedWeed { get; set; }

	[Property]
	[Sync( SyncFlags.FromHost )]
	public TimeUntil? AutoProcessTime { get; set; }

	public RollingTableEntityConfig RollingTableConfig { get; private set; } = new();

	[Property]
	public required GameObject JointGameObject { get; set; }

	[Property]
	public required GameObject PackedGameObject { get; set; }

	[Property]
	public SoundEvent? AutoProcessSound { get; set; }

	private readonly object _makeLock = new();

	protected override void OnStart()
	{
		base.OnStart();

		RollingTableConfig = GetConfig( new RollingTableEntityConfig() );
	}

	[Rpc.Host]
	public void OnProcess()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:rollingtable:process", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		lock ( _makeLock )
		{
			if ( UnprocessedWeed == 0 || ProcessedWeed >= RollingTableConfig.ProcessedWeedCapacity )
			{
				return;
			}

			UnprocessedWeed--;
			ProcessedWeed++;
		}
	}

	[Rpc.Host]
	public void OnMakeProduct( bool isJoint )
	{
		var caller = Rpc.Caller;
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:rollingtable:make", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );

		if ( !player.IsValid() )
		{
			return;
		}

		lock ( _makeLock )
		{
			if ( ProcessedWeed == 0 )
			{
				return;
			}

			ProcessedWeed--;

			var spawnGameObject = isJoint ? JointGameObject : PackedGameObject;
			var count = isJoint ? RollingTableConfig.JointOutputCount : RollingTableConfig.BundleOutputCount;

			for ( var i = 0u; i < count; i++ )
			{
				var toSpawn = spawnGameObject.Clone();

				toSpawn.WorldPosition = WorldPosition + Vector3.Up * 50f;
				toSpawn.WorldRotation = WorldRotation;

				var baseEntity = toSpawn.GetComponent<BaseEntity>();

				if ( baseEntity.IsValid() )
				{
					baseEntity.Owner = player.SteamId;
				}

				toSpawn.NetworkSpawn( caller );
			}

		}
	}

	public void OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		// Add any dried weed to the rolling table
		var weed = other.GetComponent<WeedHarvestEntity>();

		lock ( _makeLock )
		{
			if ( weed.IsValid() && weed.Dried && UnprocessedWeed < RollingTableConfig.UnprocessedWeedCapacity )
			{
				weed.GameObject.Destroy();
				UnprocessedWeed++;
			}
		}
	}

	[Rpc.Host]
	public async void OnStartAutoProcessBuy()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:rollingtable:autoprocess", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		if ( AutoProcessTime.HasValue )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			return;
		}

		// Charge the player for auto-plucking
		if ( !await player.ChargeHost( RollingTableConfig.AutoProcessCost, "Auto Processing (Rolling table)" ) )
		{
			return;
		}

		AutoProcessTime = MathF.Max( 1f, RollingTableConfig.AutoProcessInterval );
		GameManager.Instance.PurchaseSound.BroadcastHost( WorldPosition );
	}

	public void OnSecondlyUpdate()
	{
		if ( !Networking.IsHost || !AutoProcessTime.HasValue )
		{
			return;
		}

		if ( AutoProcessTime.Value <= 0 )
		{
			lock ( _makeLock )
			{
				if ( UnprocessedWeed > 0 && ProcessedWeed < RollingTableConfig.ProcessedWeedCapacity )
				{
					UnprocessedWeed--;
					ProcessedWeed++;
					AutoProcessSound.BroadcastHost( WorldPosition );
				}
			}

			AutoProcessTime = MathF.Max( 1f, RollingTableConfig.AutoProcessInterval );
		}
	}

}
