namespace Dxura.RP.Game;

public enum FireMode
{
	Semi,
	Automatic,
	Burst
}

[Icon( "track_changes" )]
[Title( "Bullet" )]
[Group( "Weapon Components" )]
public class ShootWeaponComponent : InputWeaponComponent, IEquipmentEvents
{
	/// <summary>
	///     Store a reference to the blood impact sound so we don't have to grab it every time.
	/// </summary>
	private static SoundEvent? _bloodImpactSound;

	[Property]
	[Group( "Bullet" )]
	[EquipmentResourceProperty]
	public float BaseDamage { get; set; } = 25.0f;

	[Property]
	[Group( "Bullet" )]
	[EquipmentResourceProperty]
	public float FireRate { get; set; } = 0.2f;

	[Property] [Group( "Bullet" )] public float Delay { get; set; } = 0.2f;

	[Property] [Group( "Bullet" )] public float DryShootDelay { get; set; } = 0.15f;

	[Property] [Group( "Bullet" )] public float DeployDelay { get; set; } = 0.5f;
	[Property] [Group( "Bullet" )] public float BulletSize { get; set; } = 1.0f;

	[Property]
	[Group( "Bullet" )]
	[EquipmentResourceProperty]
	public int BulletCount { get; set; } = 1;

	[Property]
	[Group( "Bullet Falloff" )]
	[EquipmentResourceProperty]
	public Curve BaseDamageFalloff { get; set; } = new( new List<Curve.Frame>
	{
		new( 0, 1 ), new( 1, 0 )
	} );

	[Property]
	[Group( "Bullet Falloff" )]
	[EquipmentResourceProperty]
	public float MaxRange { get; set; } = 1024000;

	[Property] [Group( "Bullet Spread" )] public float BulletSpread { get; set; } = 0;
	[Property] [Group( "Bullet Spread" )] public float PlayerVelocityLimit { get; set; } = 300f;
	[Property] [Group( "Bullet Spread" )] public float VelocitySpreadScale { get; set; } = 0.25f;
	[Property] [Group( "Bullet Spread" )] public float InAirSpreadMultiplier { get; set; } = 2f;

	[Property] [Group( "Penetration" )] public float PenetrationThickness { get; set; } = 32f;

	[Property] [Group( "Effects" )] public GameObject? MuzzleFlashPrefab { get; set; }

	[Property] [Group( "Effects" )] public GameObject? EjectionPrefab { get; set; }

	[Property]
	[ToggleGroup( "Headshot" )] public bool HeadshotEnabled { get; set; } = true;

	[Property]
	[Group( "Headshot" )] public float HeadshotDamageMultiplier { get; set; } = 2.0f;

	/// <summary>
	///     What sound should we play when we fire?
	/// </summary>
	[Property]
	[Group( "Effects" )]
	public SoundEvent? ShootSound { get; set; }

	[Property]
	[Group( "Effects" )]
	public SoundEvent? SuppressedShootSound { get; set; }

	/// <summary>
	///     What sound should we play when we dry fire?
	/// </summary>
	[Property]
	[Group( "Effects" )]
	public SoundEvent? DryFireSound { get; set; }

	/// <summary>
	///     The current weapon's ammo container.
	/// </summary>
	[Property]
	[Category( "Ammo" )]
	public AmmoComponent? AmmoComponent { get; set; }

	/// <summary>
	///     Does this weapon require an ammo container to fire its bullets?
	/// </summary>
	[Property]
	[Category( "Ammo" )]
	public bool RequiresAmmoComponent { get; set; } = false;

	/// <summary>
	///     Anything past 2048 units won't produce effects,
	///     This is squared.
	/// </summary>
	[Property]
	[Group( "Effects" )]
	public float MaxEffectsPlayDistance { get; set; } = 4194304f;

	/// <summary>
	///     How far will we trace away from a gunshot wound, to make blood splatters?
	/// </summary>
	[Property]
	[Group( "Effects" )]
	public float BloodEjectDistance { get; set; } = 512f;

	/// <summary>
	///     How quickly can we switch fire mode?
	/// </summary>
	[Property]
	[Group( "Fire Modes" )]
	public float FireModeSwitchDelay { get; set; } = 0.3f;

	/// <summary>
	///     What fire modes do we support?
	/// </summary>
	[Property]
	[Group( "Fire Modes" )]
	public List<FireMode> SupportedFireModes { get; set; } = new()
	{
		FireMode.Automatic
	};

	/// <summary>
	///     What's our current fire mode? (Or Default)
	/// </summary>
	[Property]
	[Sync]
	[Group( "Fire Modes" )]
	public FireMode CurrentFireMode { get; set; } = FireMode.Automatic;

	/// <summary>
	///     How many bullets describes a burst?
	/// </summary>
	[Property]
	[Group( "Fire Modes" )]
	public int BurstAmount { get; set; } = 3;

	/// <summary>
	///     How long after we finish a burst until we can shoot again?
	/// </summary>
	[Property]
	[Group( "Fire Modes" )]
	public float BurstEndDelay { get; set; } = 0.2f;

	public TimeSince TimeSinceFireModeSwitch { get; set; }
	public TimeSince TimeSinceBurstFinished { get; set; }
	public bool IsBurstFiring { get; set; }

	/// <summary>
	///     Accessor for the aim ray.
	/// </summary>
	protected Ray? WeaponRay => Equipment.Owner?.AimRay;

	/// <summary>
	///     How long since we shot?
	/// </summary>
	public TimeSince TimeSinceShoot { get; private set; }

	/// <summary>
	///     How long since we deployed this weapon?
	/// </summary>
	public TimeSince TimeSinceDeployed { get; private set; }

	/// <summary>
	///     Fetches the desired model renderer that we'll focus effects on like trail effects, muzzle flashes, etc.
	/// </summary>
	protected IEquipment Effector
	{
		get
		{
			if ( IsProxy || !Equipment.ViewModel.IsValid() )
			{
				return Equipment;
			}

			return Equipment.ViewModel;
		}
	}

	[Sync] public int BurstCount { get; set; }

	public new void OnEquipmentDeployed( Equipment equipment )
	{
		TimeSinceDeployed = 0;
		base.OnEquipmentDeployed( equipment );
	}

	public new void OnEquipmentHolstered( Equipment equipment )
	{
		ClearBurst();
		base.OnEquipmentHolstered( equipment );
	}

	protected override void OnStart()
	{
		if ( _bloodImpactSound is not null )
		{
			return;
		}

		_bloodImpactSound = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-flesh.sound" );
	}


	/// <summary>
	///     Play any particle effects such as muzzle flashes.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void BroadcastShootEffects( byte presentation )
	{
		if ( Application.IsDedicatedServer )
		{
			return;
		}

		DoShootEffects( presentation );
	}

	private void DoShootEffects( byte presentation )
	{
		if ( !Effector.ModelRenderer.IsValid() )
		{
			return;
		}

		// Create a muzzle flash from a GameObject / prefab
		if ( !WeaponFirePresentationRules.ShouldSuppressMuzzleFlash( presentation )
		     && MuzzleFlashPrefab.IsValid() )
		{
			if ( Effector.Muzzle.IsValid() )
			{
				MuzzleFlashPrefab.Clone( new CloneConfig
				{
					Parent = Effector.Muzzle, Transform = new Transform(), StartEnabled = true, Name = $"Muzzle flash: {Equipment.GameObject}"
				} );
			}
		}

		if ( EjectionPrefab.IsValid() && Effector.EjectionPort.IsValid() )
		{
			EjectionPrefab.Clone( new CloneConfig
			{
				Parent = Effector.EjectionPort, Transform = new Transform(), StartEnabled = true, Name = $"Ejection: {Equipment.GameObject}"
			} );
		}

		var shootSound = WeaponFirePresentationRules.ShouldUseSuppressedSound(
			presentation,
			SuppressedShootSound is not null )
			? SuppressedShootSound
			: ShootSound;
		shootSound.Play( Equipment.WorldPosition );

		// Third person
		if ( Equipment.Owner.IsValid() && Equipment.Owner.Renderer.IsValid() )
		{
			Equipment.Owner.Renderer.Set( "b_attack", true );
		}

		// First person
		if ( Equipment.ViewModel.IsValid() && Equipment.ViewModel.Enabled )
		{
			Equipment.ViewModel.ModelRenderer.Set( "b_attack", true );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void BroadcastBloodEffects( Vector3 pos, Vector3 direction )
	{
		if ( GameManager.IsHeadless )
		{
			return;
		}

		DoBloodEffects( pos, direction );
	}

	private void DoBloodEffects( Vector3 pos, Vector3 direction )
	{
		if ( !IsNearby( pos ) )
		{
			return;
		}

		var tr = Scene.Trace.Ray( pos, pos + direction * BloodEjectDistance )
			.WithoutTags( Constants.PlayerTag )
			.Run();

		if ( tr.Hit )
		{
			CreateBloodDecal( Random.Shared.FromList( GameManager.Instance.BloodDecals! )!, tr.HitPosition - tr.Direction * 2, tr.Normal, Sandbox.Game.Random.Float( 0, 360 ),
				Sandbox.Game.Random.Int( 32, 96 ), 10f, 30f );
		}
	}

	private void CreateBloodDecal( DecalDefinition decalDefinition, Vector3 pos, Vector3 normal, float rotation, float size,
		float depth, float destroyTime = 3f )
	{
		var gameObject = Scene.CreateObject();
		gameObject.Name = $"Blood decal: {Equipment.GameObject}";
		gameObject.WorldPosition = pos;
		gameObject.WorldRotation = Rotation.LookAt( -normal );

		// Random rotation
		gameObject.WorldRotation *= Rotation.FromAxis( Vector3.Forward, rotation );

		var decal = gameObject.Components.Create<Decal>();
		decal.Decals.Add( decalDefinition );

		// Creates a destruction component to destroy the gameobject after a while
		gameObject.DestroyAsync( destroyTime );
	}

	private void CreateImpactEffects( GameObject hitObject, Surface surface, Vector3 pos, Vector3 normal )
	{
		var bulletImpactPrefab = surface.PrefabCollection.BulletImpact;
		if ( !bulletImpactPrefab.IsValid() )
		{
			return;
		}

		var impact = bulletImpactPrefab.Clone( new CloneConfig
		{
			Parent = Scene, Transform = new Transform( pos, Rotation.LookAt( -normal ) ), StartEnabled = true, Name = $"Bullet impact: {Equipment.GameObject}"
		} );

		if ( impact.IsValid() )
		{
			impact.DestroyAsync( 3f );
		}

		surface.SoundCollection.Bullet.Play( pos );
	}

	/// <summary>
	///     Shoot the gun!
	/// </summary>
	///
	private void Shoot()
	{
		TimeSinceShoot = 0;

		if ( CurrentFireMode == FireMode.Burst )
		{
			IsBurstFiring = true;
		}

		var presentationProvider = Equipment.Components.Get<IWeaponFirePresentationProvider>(
			FindMode.EnabledInSelfAndDescendants );
		if ( presentationProvider is null )
		{
			DoShootEffects( (byte)WeaponFirePresentation.None );
		}

		IGameEvents.PostToGameObject( GameObject, x => x.OnWeaponShot() );

		for ( var i = 0; i < BulletCount; i++ )
		{
			var trace = GetShootTrace();
			if ( !trace.HasValue )
			{
				continue;
			}

			var tr = trace.Value;

			if ( !tr.Hit || tr.Distance == 0 )
			{
				continue;
			}

			CreateImpactEffects( tr.GameObject, tr.Surface, tr.EndPosition, tr.Normal );

			if ( tr.GameObject?.Root.Components.Get<Player>( FindMode.EnabledInSelfAndDescendants ) is null )
			{
				continue;
			}

			DoBloodEffects( tr.HitPosition, tr.Direction );
		}

		DoShootHost();


		// If we have a recoil function, let it know.
		Equipment.Components.Get<RecoilWeaponComponent>( FindMode.EnabledInSelfAndDescendants )?.Shoot();
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void DoShootHost()
	{
		var caller = Rpc.Caller;
		var callerId = Rpc.CallerId;
		var equipmentValid = Equipment.IsValid();
		var owner = equipmentValid ? Equipment.Owner : null;
		var ownerValid = owner.IsValid();
		var controllerValid = ownerValid && owner.Controller.IsValid();
		var hostAimRay = controllerValid
			? new Ray( owner.Controller.EyePosition, owner.Controller.EyeAngles.ToRotation().Forward )
			: default;
		var ammoComponentValid = AmmoComponent.IsValid();
		var isSemiAuto = SupportedFireModes.Count == 1 && SupportedFireModes[0] == FireMode.Semi;
		var minFireInterval = isSemiAuto ? Delay : RpmToSeconds();
		var damageCooldown = Config.Current.Game.DamageCooldown;
		var fireCooldown = MathF.Max( minFireInterval * 0.85f, damageCooldown );
		var equipmentNetworkOwner = equipmentValid ? Equipment.GameObject.Network.Owner : null;
		var callerOwnsDebugPlayer = ownerValid && owner.IsDebugPlayer && caller.IsHost;
		var callerOwnsPlayer = ownerValid && (owner.Connection == caller || owner.Connection is null && callerOwnsDebugPlayer);
		var callerOwnsEquipment = equipmentValid
		                           && (equipmentNetworkOwner == caller || equipmentNetworkOwner is null && callerOwnsDebugPlayer);
		var combinedSpread = ownerValid ? BulletSpread + owner.Spread : float.NaN;
		var isBlocked = equipmentValid
		                && (Equipment.Tags.Has( "reloading" ) || Equipment.Tags.Has( "bolting" ) || Equipment.Tags.Has( "no_shooting" ));
		var authorization = WeaponHostFireRules.Evaluate(
			equipmentValid,
			ownerValid,
			callerOwnsPlayer,
			callerOwnsEquipment,
			equipmentValid && Equipment.IsDeployed,
			ownerValid && owner.CurrentEquipment == Equipment,
			ownerValid && owner.IsDead,
			ownerValid && owner.IsRunning,
			isBlocked,
			TimeSinceDeployed >= DeployDelay,
			controllerValid && WeaponHostFireRules.IsFiniteVector( hostAimRay.Position.x, hostAimRay.Position.y, hostAimRay.Position.z ),
			controllerValid && WeaponHostFireRules.IsFiniteNonZeroVector( hostAimRay.Forward.x, hostAimRay.Forward.y, hostAimRay.Forward.z ),
			controllerValid ? hostAimRay.Position.Distance( owner.WorldPosition ) : float.NaN,
			WeaponHostFireRules.IsValidFireTiming( minFireInterval, DeployDelay, damageCooldown, fireCooldown )
			&& WeaponHostFireRules.IsValidWeaponConfiguration(
				fireCooldown,
				MaxRange,
				BulletSize,
				BaseDamage,
				HeadshotEnabled ? HeadshotDamageMultiplier : 1f,
				combinedSpread,
				BulletCount ),
			RequiresAmmoComponent,
			ammoComponentValid,
			ammoComponentValid ? AmmoComponent.Ammo : 0 );
		if ( authorization != WeaponHostFireReject.Accepted )
		{
			return;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:damage", fireCooldown ) )
		{
			return;
		}

		if ( AmmoComponent.IsValid() )
		{
			AmmoComponent.Ammo = Math.Max( AmmoComponent.Ammo - 1, 0 );
		}

		var presentationProvider = Equipment.Components.Get<IWeaponFirePresentationProvider>(
			FindMode.EnabledInSelfAndDescendants );
		var presentation = presentationProvider?.GetFirePresentation()
			?? (byte)WeaponFirePresentation.None;
		if ( presentationProvider is not null )
		{
			BroadcastShootEffects( presentation );
		}
		else
		{
			using ( Rpc.FilterExclude( caller ) )
			{
				BroadcastShootEffects( presentation );
			}
		}

		for ( var i = 0; i < BulletCount; i++ )
		{
			var serverTrace = GetShootTrace( hostAimRay );

			if ( !serverTrace.HasValue )
			{
				continue;
			}

			var shootTrace = serverTrace.Value;
			if ( !shootTrace.Hit
			     || !shootTrace.GameObject.IsValid()
			     || !WeaponHostFireRules.IsPositiveFinite( shootTrace.Distance )
			     || !WeaponHostFireRules.IsFiniteVector( shootTrace.StartPosition.x, shootTrace.StartPosition.y, shootTrace.StartPosition.z )
			     || !WeaponHostFireRules.IsFiniteVector( shootTrace.HitPosition.x, shootTrace.HitPosition.y, shootTrace.HitPosition.z )
			     || !WeaponHostFireRules.IsFiniteNonZeroVector( shootTrace.Direction.x, shootTrace.Direction.y, shootTrace.Direction.z )
			     || !WeaponHostFireRules.IsFiniteVector( shootTrace.Normal.x, shootTrace.Normal.y, shootTrace.Normal.z ) )
			{
				continue;
			}

			// Require target to be alive
			var targetPlayer = shootTrace.GameObject.Root.Components.Get<Player>( FindMode.EnabledInSelfAndDescendants );
			if ( targetPlayer.IsValid() && (targetPlayer == owner || targetPlayer.HealthComponent.State != LifeState.Alive) )
			{
				continue;
			}

			if ( targetPlayer.IsValid() )
			{
				using ( Rpc.FilterExclude( caller ) )
				{
					BroadcastBloodEffects( shootTrace.HitPosition, shootTrace.Direction );
				}
			}

			if ( shootTrace.HitPosition.Distance( hostAimRay.Position ) > MaxRange * 1.1f )
			{
				continue;
			}

			var toHit = (shootTrace.HitPosition - hostAimRay.Position).Normal;
			if ( Vector3.Dot( toHit, hostAimRay.Forward ) < 0f )
			{
				continue;
			}

			var hitbox = shootTrace.GetHitboxTags();
			var headShot = HeadshotEnabled && hitbox.HasFlag( HitboxTags.Head );
			var headShotMult = headShot ? HeadshotDamageMultiplier : 1.0f;

			var damage = CalculateDamageFalloff( BaseDamage, shootTrace.Distance ).CeilToInt() * headShotMult;
			if ( !WeaponHostFireRules.IsPositiveFinite( damage ) )
			{
				continue;
			}

			var damageFlags = DamageFlags.None;

			if ( Player.IsValid() && Player.Controller.IsOnGround )
			{
				damageFlags |= DamageFlags.AirShot;
			}

			shootTrace.GameObject.TakeDamageHost( new DamageInfo(
				owner,
				damage,
				Equipment,
				shootTrace.HitPosition,
				shootTrace.Direction * damage * 80f,
				hitbox,
				damageFlags ) );
		}
	}

	private float CalculateDamageFalloff( float damage, float distance )
	{
		var distDelta = distance / MaxRange;
		var damageMultiplier = BaseDamageFalloff.Evaluate( distDelta );

		return damage * damageMultiplier;
	}

	/// <summary>
	///     Are we nearby a position? Used for FX
	/// </summary>
	/// <param name="position"></param>
	/// <returns></returns>
	private bool IsNearby( Vector3 position )
	{
		if ( !Scene.Camera.IsValid() )
		{
			return false;
		}

		return position.DistanceSquared( Scene.Camera.Transform.World.Position ) < MaxEffectsPlayDistance;
	}

	private void DryShoot()
	{
		TimeSinceShoot = 0f;

		DoDryShootEffects();
		BroadcastDryShootEffectsHost();
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void BroadcastDryShootEffectsHost()
	{
		var caller = Rpc.Caller;
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:shoot:dry:effects",
			Config.Current.Game.ShootDryEffectsCooldown ) )
		{
			return;
		}

		using ( Rpc.FilterExclude( caller ) )
		{
			BroadcastDryShootEffects();
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void BroadcastDryShootEffects()
	{
		DoDryShootEffects();
	}

	private void DoDryShootEffects()
	{
		if ( DryFireSound is not null )
		{
			DryFireSound.Play( Equipment.Transform.World.Position );
		}

		// First person
		Equipment.ViewModel?.ModelRenderer.Set( "b_attack_dry", true );
	}

	private SceneTraceResult DoTraceBullet( Vector3 start, Vector3 end, float radius )
	{
		return Scene.Trace.Ray( start, end )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( Constants.TraceIgnoreTags )
			.Size( radius )
			.Run();
	}

	/// <summary>
	///     Runs a trace with all the data we have supplied it, and returns the result
	/// </summary>
	/// <returns></returns>
	protected virtual SceneTraceResult? GetShootTrace()
	{
		if ( !WeaponRay.HasValue )
		{
			return null;
		}

		return GetShootTrace( WeaponRay.Value );
	}

	private SceneTraceResult? GetShootTrace( Ray weaponRay )
	{

		var start = weaponRay.Position;
		var rot = Rotation.LookAt( weaponRay.Forward );

		var forward = rot.Forward;
		forward += (Vector3.Random + Vector3.Random + Vector3.Random + Vector3.Random) *
		           (BulletSpread + Equipment.Owner?.Spread ?? 0) * 0.25f;
		forward = forward.Normal;

		// Just do a single trace and return the first hit
		var result = DoTraceBullet( start, weaponRay.Position + forward * MaxRange, BulletSize );

		return result;
	}

	protected float RpmToSeconds()
	{
		return 60 / FireRate;
	}

	/// <summary>
	///     Can we shoot this gun right now?
	/// </summary>
	/// <returns></returns>
	public bool CanShoot()
	{

		var cooldownTime = RpmToSeconds();
		var isSingleShot = SupportedFireModes.Count == 1 && SupportedFireModes[0] == FireMode.Semi;
		var isShotgun = BulletCount > 1;

		// Do we still have a weapon?
		if ( !Equipment.IsValid() )
		{
			return false;
		}

		if ( !Equipment.Owner.IsValid() )
		{
			return false;
		}

		// Check if weapon was just deployed
		if ( TimeSinceDeployed < DeployDelay )
		{
			return false;
		}

		// Weapon
		if ( Equipment.Tags.Has( "reloading" ) || Equipment.Tags.Has( "bolting" ) || Equipment.Tags.Has( "no_shooting" ) )
		{
			return false;
		}

		if ( isSingleShot )
		{
			cooldownTime = Delay;
		}
		else if ( isShotgun )
		{
			cooldownTime = Delay * 2;
		}

		// Delay checks
		if ( TimeSinceShoot < cooldownTime )
		{
			return false;
		}

		// Ammo checks
		if ( RequiresAmmoComponent && (AmmoComponent == null || !AmmoComponent.HasAmmo) )
		{
			return false;
		}

		return true;
	}

	private void ClearBurst()
	{
		TimeSinceBurstFinished = 0;
		IsBurstFiring = false;
		BurstCount = 0;
	}

	protected override void OnInputFixedUpdate()
	{
		if ( Input.Pressed( "FireMode" ) )
		{
			CycleFireMode();
			return;
		}

		// Don't allow shooting when running
		if ( Player?.IsRunning ?? false )
		{
			return;
		}


		if ( IsBurstFiring && BurstCount >= BurstAmount - 1 || Tags.Has( "reloading" ) && IsBurstFiring )
		{
			ClearBurst();
		}

		if ( CurrentFireMode == FireMode.Burst && IsBurstFiring && CanShoot() )
		{
			BurstCount++;
			Shoot();
		}

		var wantsToShoot = IsDown();

		// HACK
		if ( CurrentFireMode == FireMode.Semi )
		{
			wantsToShoot = Input.Pressed( "attack1" );
		}

		if ( wantsToShoot )
		{
			if ( !CanShoot() )
			{
				// Dry fire
				if ( !AmmoComponent?.HasAmmo ?? false )
				{
					if ( TimeSinceShoot < DryShootDelay )
					{
						return;
					}

					if ( Tags.Has( "reloading" ) )
					{
						return;
					}

					DryShoot();
				}
			}
			else
			{
				if ( IsBurstFiring )
				{
					return;
				}

				if ( TimeSinceBurstFinished < BurstEndDelay )
				{
					return;
				}

				Shoot();
			}
		}
	}

	protected int GetFireModeIndex( FireMode fireMode )
	{
		var i = 0;
		foreach ( var mode in SupportedFireModes )
		{
			if ( mode == fireMode )
			{
				return i;
			}

			i++;
		}

		return 0;
	}

	public void CycleFireMode()
	{
		if ( TimeSinceFireModeSwitch < FireModeSwitchDelay )
		{
			return;
		}

		if ( IsBurstFiring )
		{
			return;
		}

		if ( IsDown() )
		{
			return;
		}

		var curIndex = GetFireModeIndex( CurrentFireMode );
		var length = SupportedFireModes.Count;
		var newIndex = (curIndex + 1 + length) % length;

		// We didn't change anything
		if ( newIndex == curIndex )
		{
			return;
		}

		CurrentFireMode = SupportedFireModes[newIndex];

		Equipment.ViewModel?.OnFireMode( CurrentFireMode );

		TimeSinceFireModeSwitch = 0;
	}
}
