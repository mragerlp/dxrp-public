namespace Dxura.RP.Game;

public enum WeaponHostFireReject : byte
{
	Accepted,
	MissingEquipment,
	MissingOwner,
	CallerMismatch,
	NotDeployed,
	NotCurrentEquipment,
	OwnerDead,
	OwnerRunning,
	Blocked,
	DeployDelay,
	InvalidAim,
	AimOriginTooFar,
	InvalidWeaponConfiguration,
	MissingAmmo,
	EmptyAmmo
}

public static class WeaponHostFireRules
{
	public const float MaximumAimOriginOffset = 150f;
	public const int MaximumBulletsPerShot = 64;

	public static WeaponHostFireReject Evaluate(
		bool equipmentValid,
		bool ownerValid,
		bool callerOwnsPlayer,
		bool callerOwnsEquipment,
		bool isDeployed,
		bool isCurrentEquipment,
		bool ownerIsDead,
		bool ownerIsRunning,
		bool isBlocked,
		bool deployDelayElapsed,
		bool aimPositionValid,
		bool aimForwardValid,
		float aimOriginDistance,
		bool weaponConfigurationValid,
		bool requiresAmmo,
		bool ammoComponentValid,
		int ammo )
	{
		if ( !equipmentValid )
		{
			return WeaponHostFireReject.MissingEquipment;
		}

		if ( !ownerValid )
		{
			return WeaponHostFireReject.MissingOwner;
		}

		if ( !callerOwnsPlayer || !callerOwnsEquipment )
		{
			return WeaponHostFireReject.CallerMismatch;
		}

		if ( !isDeployed )
		{
			return WeaponHostFireReject.NotDeployed;
		}

		if ( !isCurrentEquipment )
		{
			return WeaponHostFireReject.NotCurrentEquipment;
		}

		if ( ownerIsDead )
		{
			return WeaponHostFireReject.OwnerDead;
		}

		if ( ownerIsRunning )
		{
			return WeaponHostFireReject.OwnerRunning;
		}

		if ( isBlocked )
		{
			return WeaponHostFireReject.Blocked;
		}

		if ( !deployDelayElapsed )
		{
			return WeaponHostFireReject.DeployDelay;
		}

		if ( !aimPositionValid || !aimForwardValid || !float.IsFinite( aimOriginDistance ) || aimOriginDistance < 0f )
		{
			return WeaponHostFireReject.InvalidAim;
		}

		if ( aimOriginDistance > MaximumAimOriginOffset )
		{
			return WeaponHostFireReject.AimOriginTooFar;
		}

		if ( !weaponConfigurationValid )
		{
			return WeaponHostFireReject.InvalidWeaponConfiguration;
		}

		if ( requiresAmmo && !ammoComponentValid )
		{
			return WeaponHostFireReject.MissingAmmo;
		}

		if ( requiresAmmo && ammo <= 0 )
		{
			return WeaponHostFireReject.EmptyAmmo;
		}

		return WeaponHostFireReject.Accepted;
	}

	public static bool IsFiniteVector( float x, float y, float z )
	{
		return float.IsFinite( x ) && float.IsFinite( y ) && float.IsFinite( z );
	}

	public static bool IsFiniteNonZeroVector( float x, float y, float z )
	{
		return IsFiniteVector( x, y, z ) && (x != 0f || y != 0f || z != 0f);
	}

	public static bool IsPositiveFinite( float value )
	{
		return float.IsFinite( value ) && value > 0f;
	}

	public static bool IsValidFireTiming(
		float rawFireInterval,
		float deployDelay,
		float damageCooldown,
		float fireCooldown )
	{
		return IsPositiveFinite( rawFireInterval )
		       && float.IsFinite( deployDelay )
		       && deployDelay >= 0f
		       && float.IsFinite( damageCooldown )
		       && damageCooldown >= 0f
		       && IsPositiveFinite( fireCooldown )
		       && fireCooldown >= damageCooldown
		       && fireCooldown >= rawFireInterval * 0.85f;
	}

	public static bool IsValidWeaponConfiguration(
		float fireCooldown,
		float maxRange,
		float bulletSize,
		float baseDamage,
		float headshotMultiplier,
		float combinedSpread,
		int bulletCount )
	{
		return IsPositiveFinite( fireCooldown )
		       && IsPositiveFinite( maxRange )
		       && float.IsFinite( bulletSize )
		       && bulletSize >= 0f
		       && float.IsFinite( baseDamage )
		       && baseDamage >= 0f
		       && float.IsFinite( headshotMultiplier )
		       && headshotMultiplier >= 0f
		       && float.IsFinite( combinedSpread )
		       && combinedSpread >= 0f
		       && bulletCount is > 0 and <= MaximumBulletsPerShot;
	}
}
