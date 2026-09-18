using System;
using System.IO;
using System.Reflection;

namespace Dxura.RP.Game;

internal static class Program
{
	private static int _failed;

	private static int Main()
	{
		Rules_RejectUnauthorizedOrInactiveWeapons();
		Rules_RejectInvalidAim();
		Rules_RejectInvalidFireTiming();
		Rules_RejectInvalidWeaponConfiguration();
		Rules_RejectMissingAndEmptyAmmo();
		Rules_AcceptOneValidShot();
		Source_UsesHostOwnedHitDataOnly();

		if ( _failed != 0 )
		{
			Console.Error.WriteLine( $"LpWeaponHostFireRules: {_failed} failed assertion(s)" );
			return 1;
		}

		Console.WriteLine( "LpWeaponHostFireRules: all focused host-fire cases passed" );
		return 0;
	}

	private static void Rules_RejectUnauthorizedOrInactiveWeapons()
	{
		ExpectReject( "missing equipment", WeaponHostFireReject.MissingEquipment, equipmentValid: false );
		ExpectReject( "missing owner", WeaponHostFireReject.MissingOwner, ownerValid: false );
		ExpectReject( "caller/player mismatch", WeaponHostFireReject.CallerMismatch, callerOwnsPlayer: false );
		ExpectReject( "caller/equipment mismatch", WeaponHostFireReject.CallerMismatch, callerOwnsEquipment: false );
		ExpectReject( "holstered equipment", WeaponHostFireReject.NotDeployed, isDeployed: false );
		ExpectReject( "non-current equipment", WeaponHostFireReject.NotCurrentEquipment, isCurrentEquipment: false );
		ExpectReject( "dead owner", WeaponHostFireReject.OwnerDead, ownerIsDead: true );
		ExpectReject( "running owner", WeaponHostFireReject.OwnerRunning, ownerIsRunning: true );
		ExpectReject( "blocked equipment", WeaponHostFireReject.Blocked, isBlocked: true );
		ExpectReject( "deploy delay", WeaponHostFireReject.DeployDelay, deployDelayElapsed: false );
	}

	private static void Rules_RejectInvalidAim()
	{
		Expect( "finite vector accepts ordinary components", WeaponHostFireRules.IsFiniteVector( 1f, -2f, 3f ) );
		Expect( "finite vector rejects NaN", !WeaponHostFireRules.IsFiniteVector( float.NaN, 0f, 0f ) );
		Expect( "finite vector rejects +infinity", !WeaponHostFireRules.IsFiniteVector( 0f, float.PositiveInfinity, 0f ) );
		Expect( "finite vector rejects -infinity", !WeaponHostFireRules.IsFiniteVector( 0f, 0f, float.NegativeInfinity ) );
		Expect( "aim direction rejects zero vector", !WeaponHostFireRules.IsFiniteNonZeroVector( 0f, 0f, 0f ) );
		ExpectReject( "non-finite aim position", WeaponHostFireReject.InvalidAim, aimPositionValid: false );
		ExpectReject( "invalid aim forward", WeaponHostFireReject.InvalidAim, aimForwardValid: false );
		ExpectReject( "NaN aim distance", WeaponHostFireReject.InvalidAim, aimOriginDistance: float.NaN );
		ExpectReject( "+infinite aim distance", WeaponHostFireReject.InvalidAim, aimOriginDistance: float.PositiveInfinity );
		ExpectReject( "negative aim distance", WeaponHostFireReject.InvalidAim, aimOriginDistance: -1f );
		ExpectReject( "remote aim origin", WeaponHostFireReject.AimOriginTooFar,
			aimOriginDistance: WeaponHostFireRules.MaximumAimOriginOffset + 0.01f );
	}

	private static void Rules_RejectInvalidWeaponConfiguration()
	{
		Expect( "valid weapon configuration", ValidWeaponConfiguration() );
		Expect( "configuration rejects NaN cooldown", !ValidWeaponConfiguration( fireCooldown: float.NaN ) );
		Expect( "configuration rejects infinite range", !ValidWeaponConfiguration( maxRange: float.PositiveInfinity ) );
		Expect( "configuration rejects negative bullet size", !ValidWeaponConfiguration( bulletSize: -1f ) );
		Expect( "configuration rejects NaN base damage", !ValidWeaponConfiguration( baseDamage: float.NaN ) );
		Expect( "configuration rejects infinite headshot multiplier", !ValidWeaponConfiguration( headshotMultiplier: float.PositiveInfinity ) );
		Expect( "configuration rejects negative spread", !ValidWeaponConfiguration( combinedSpread: -0.01f ) );
		Expect( "configuration rejects zero bullets", !ValidWeaponConfiguration( bulletCount: 0 ) );
		Expect( "configuration rejects excessive bullets", !ValidWeaponConfiguration( bulletCount: WeaponHostFireRules.MaximumBulletsPerShot + 1 ) );
		ExpectReject( "invalid weapon configuration", WeaponHostFireReject.InvalidWeaponConfiguration,
			weaponConfigurationValid: false );
	}

	private static void Rules_RejectInvalidFireTiming()
	{
		Expect( "valid automatic timing", ValidFireTiming( 0.1f, 0.5f, 0.05f, 0.085f ) );
		Expect( "valid zero deploy delay", ValidFireTiming( 0.1f, 0f, 0.05f, 0.085f ) );
		Expect( "timing rejects zero cadence", !ValidFireTiming( 0f, 0.5f, 0.05f, 0.05f ) );
		Expect( "timing rejects negative cadence", !ValidFireTiming( -0.1f, 0.5f, 0.05f, 0.05f ) );
		Expect( "timing rejects NaN cadence", !ValidFireTiming( float.NaN, 0.5f, 0.05f, 0.05f ) );
		Expect( "timing rejects +infinite cadence", !ValidFireTiming( float.PositiveInfinity, 0.5f, 0.05f, 0.05f ) );
		Expect( "timing rejects -infinite cadence", !ValidFireTiming( float.NegativeInfinity, 0.5f, 0.05f, 0.05f ) );
		Expect( "timing rejects negative deploy delay", !ValidFireTiming( 0.1f, -0.5f, 0.05f, 0.085f ) );
		Expect( "timing rejects NaN deploy delay", !ValidFireTiming( 0.1f, float.NaN, 0.05f, 0.085f ) );
		Expect( "timing rejects infinite deploy delay", !ValidFireTiming( 0.1f, float.PositiveInfinity, 0.05f, 0.085f ) );
		Expect( "timing rejects negative damage cooldown", !ValidFireTiming( 0.1f, 0.5f, -0.05f, 0.085f ) );
		Expect( "timing rejects non-finite derived cooldown", !ValidFireTiming( 0.1f, 0.5f, 0.05f, float.NaN ) );
	}

	private static void Rules_RejectMissingAndEmptyAmmo()
	{
		ExpectReject( "missing required ammo component", WeaponHostFireReject.MissingAmmo,
			requiresAmmo: true, ammoComponentValid: false );
		ExpectReject( "empty required ammo component", WeaponHostFireReject.EmptyAmmo,
			requiresAmmo: true, ammo: 0 );
		ExpectReject( "negative required ammo", WeaponHostFireReject.EmptyAmmo,
			requiresAmmo: true, ammo: -1 );
	}

	private static void Rules_AcceptOneValidShot()
	{
		Expect( "valid host shot", Evaluate() == WeaponHostFireReject.Accepted );
		Expect(
			"ammo-optional weapon remains valid without a container",
			Evaluate( requiresAmmo: false, ammoComponentValid: false, ammo: 0 ) == WeaponHostFireReject.Accepted );
	}

	private static void Source_UsesHostOwnedHitDataOnly()
	{
		var source = File.ReadAllText( Path.Combine( AppContext.BaseDirectory, "ShootWeaponComponent.source.cs" ) );
		var hostMethod = SliceIncludingPreviousAttribute(
			source,
			"[Rpc.Host(",
			"private void DoShootHost()",
			"private float CalculateDamageFalloff" );
		var pelletLoop = SliceBraceBlock( hostMethod, "for ( var i = 0; i < BulletCount; i++ )" );
		Expect( "host RPC accepts fire intent only", source.Contains( "private void DoShootHost()" ) );
		Expect( "host RPC remains owner-only and reliable",
			hostMethod.TrimStart().StartsWith( "[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]", StringComparison.Ordinal ) );
		Expect( "client hit payload type removed", !source.Contains( "ShootDamageTrace" ) );
		Expect( "client hit collection removed", !source.Contains( "clientHits" ) && !source.Contains( "clientShootTrace" ) );
		Expect( "host authorization table is used", hostMethod.Contains( "WeaponHostFireRules.Evaluate(" ) );
		Expect( "caller/player ownership is checked", source.Contains( "owner.Connection == caller" ) );
		Expect( "caller/equipment ownership is checked", source.Contains( "Equipment.GameObject.Network.Owner" ) );
		Expect( "active-equipment equality is checked", source.Contains( "owner.CurrentEquipment == Equipment" ) );
		Expect( "deployed state is checked", source.Contains( "Equipment.IsDeployed" ) );
		Expect( "blocked weapon state is checked", source.Contains( "Equipment.Tags.Has( \"reloading\" )" ) );
		Expect( "host reconstructs aim position from controller", hostMethod.Contains( "owner.Controller.EyePosition" ) );
		Expect( "host reconstructs aim direction from controller", hostMethod.Contains( "owner.Controller.EyeAngles.ToRotation().Forward" ) );
		Expect( "host does not read synchronized owner AimRay", !hostMethod.Contains( "owner.AimRay" ) );
		Expect( "component-wise aim validation is used", hostMethod.Contains( "WeaponHostFireRules.IsFiniteVector(" ) );
		Expect( "nonzero aim direction validation is used", hostMethod.Contains( "WeaponHostFireRules.IsFiniteNonZeroVector(" ) );
		Expect( "raw fire timing validation is used", hostMethod.Contains( "WeaponHostFireRules.IsValidFireTiming(" ) );
		Expect( "weapon configuration validation is used", hostMethod.Contains( "WeaponHostFireRules.IsValidWeaponConfiguration(" ) );
		Expect( "host aim ray is passed to every damage trace", hostMethod.Contains( "GetShootTrace( hostAimRay )" ) );
		Expect( "damage trace comes from the host trace", hostMethod.Contains( "var shootTrace = serverTrace.Value;" ) );
		Expect( "host trace target is validated", hostMethod.Contains( "shootTrace.GameObject.IsValid()" ) );
		Expect( "host trace distance is validated", hostMethod.Contains( "WeaponHostFireRules.IsPositiveFinite( shootTrace.Distance )" ) );
		Expect( "damage target comes from the host trace", hostMethod.Contains( "shootTrace.GameObject.TakeDamageHost" ) );
		Expect( "ammo is debited exactly once in the host method",
			Count( hostMethod, "AmmoComponent.Ammo =" ) == 1
			&& Count( hostMethod, "AmmoComponent.Ammo = Math.Max( AmmoComponent.Ammo - 1, 0 );" ) == 1 );
		Expect( "host traces exactly the configured pellet count",
			pelletLoop.Length > 0
			&& Count( pelletLoop, "GetShootTrace( hostAimRay )" ) == 1
			&& Count( pelletLoop, "shootTrace.GameObject.TakeDamageHost" ) == 1 );

		var authorizationIndex = hostMethod.IndexOf( "var authorization = WeaponHostFireRules.Evaluate(", StringComparison.Ordinal );
		var rejectionIndex = hostMethod.IndexOf( "if ( authorization != WeaponHostFireReject.Accepted )", StringComparison.Ordinal );
		var cooldownIndex = hostMethod.IndexOf( "CheckAndStartCooldown", StringComparison.Ordinal );
		var ammoIndex = hostMethod.IndexOf( "AmmoComponent.Ammo =", StringComparison.Ordinal );
		var effectsIndex = hostMethod.IndexOf( "BroadcastShootEffects( presentation )", StringComparison.Ordinal );
		var traceIndex = hostMethod.IndexOf( "GetShootTrace( hostAimRay )", StringComparison.Ordinal );
		Expect( "authorization rejection precedes every mutation and effect",
			authorizationIndex >= 0
			&& rejectionIndex > authorizationIndex
			&& cooldownIndex > rejectionIndex
			&& ammoIndex > cooldownIndex
			&& effectsIndex > ammoIndex
			&& traceIndex > effectsIndex );

		Expect( "mutation probe rejects owner-only attribute removal",
			!HostSourceContractValid( ReplaceHostRpcAttribute( source, "[Rpc.Host]" ) ) );
		Expect( "mutation probe rejects ammo increment",
			!HostSourceContractValid( source.Replace(
				"AmmoComponent.Ammo = Math.Max( AmmoComponent.Ammo - 1, 0 );",
				"AmmoComponent.Ammo = Math.Max( AmmoComponent.Ammo + 1, 0 );",
				StringComparison.Ordinal ) ) );
		Expect( "mutation probe rejects trace moved outside pellet loop",
			!HostSourceContractValid( MoveTraceOutsidePelletLoop( source ) ) );
	}

	private static bool HostSourceContractValid( string source )
	{
		var hostMethod = SliceIncludingPreviousAttribute(
			source,
			"[Rpc.Host(",
			"private void DoShootHost()",
			"private float CalculateDamageFalloff" );
		var pelletLoop = SliceBraceBlock( hostMethod, "for ( var i = 0; i < BulletCount; i++ )" );

		return hostMethod.TrimStart().StartsWith(
			       "[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]",
			       StringComparison.Ordinal )
		       && Count( hostMethod, "AmmoComponent.Ammo =" ) == 1
		       && Count( hostMethod, "AmmoComponent.Ammo = Math.Max( AmmoComponent.Ammo - 1, 0 );" ) == 1
		       && Count( pelletLoop, "GetShootTrace( hostAimRay )" ) == 1
		       && Count( pelletLoop, "shootTrace.GameObject.TakeDamageHost" ) == 1;
	}

	private static string MoveTraceOutsidePelletLoop( string source )
	{
		const string traceStatement = "var serverTrace = GetShootTrace( hostAimRay );";
		const string loopMarker = "for ( var i = 0; i < BulletCount; i++ )";
		var withoutTrace = source.Replace( traceStatement, "var serverTrace = movedServerTrace;", StringComparison.Ordinal );
		return withoutTrace.Replace(
			loopMarker,
			$"var movedServerTrace = GetShootTrace( hostAimRay );{Environment.NewLine}{Environment.NewLine}\t\t{loopMarker}",
			StringComparison.Ordinal );
	}

	private static string ReplaceHostRpcAttribute( string source, string replacement )
	{
		var method = source.IndexOf( "private void DoShootHost()", StringComparison.Ordinal );
		var start = method < 0 ? -1 : source.LastIndexOf( "[Rpc.Host(", method, StringComparison.Ordinal );
		var end = start < 0 ? -1 : source.IndexOf( ']', start );
		if ( start < 0 || end < start )
		{
			return source;
		}

		return string.Concat( source.AsSpan( 0, start ), replacement, source.AsSpan( end + 1 ) );
	}

	private static bool ValidFireTiming( float rawFireInterval, float deployDelay, float damageCooldown, float fireCooldown )
	{
		var method = typeof( WeaponHostFireRules ).GetMethod(
			"IsValidFireTiming",
			BindingFlags.Public | BindingFlags.Static,
			binder: null,
			types: new[] { typeof( float ), typeof( float ), typeof( float ), typeof( float ) },
			modifiers: null );
		if ( method is null )
		{
			return false;
		}

		return method.Invoke( null, new object[] { rawFireInterval, deployDelay, damageCooldown, fireCooldown } ) is true;
	}

	private static string Slice( string source, string startMarker, string endMarker )
	{
		var start = source.IndexOf( startMarker, StringComparison.Ordinal );
		var end = source.IndexOf( endMarker, start + startMarker.Length, StringComparison.Ordinal );
		if ( start < 0 || end <= start )
		{
			return string.Empty;
		}

		return source[start..end];
	}

	private static string SliceIncludingPreviousAttribute(
		string source,
		string attributeMarker,
		string methodMarker,
		string endMarker )
	{
		var method = source.IndexOf( methodMarker, StringComparison.Ordinal );
		if ( method < 0 )
		{
			return string.Empty;
		}

		var start = source.LastIndexOf( attributeMarker, method, StringComparison.Ordinal );
		var end = source.IndexOf( endMarker, method + methodMarker.Length, StringComparison.Ordinal );
		if ( start < 0 || end <= start )
		{
			return string.Empty;
		}

		return source[start..end];
	}

	private static string SliceBraceBlock( string source, string marker )
	{
		var markerIndex = source.IndexOf( marker, StringComparison.Ordinal );
		var openBrace = markerIndex < 0 ? -1 : source.IndexOf( '{', markerIndex + marker.Length );
		if ( openBrace < 0 )
		{
			return string.Empty;
		}

		var depth = 0;
		for ( var i = openBrace; i < source.Length; i++ )
		{
			if ( source[i] == '{' )
			{
				depth++;
			}
			else if ( source[i] == '}' )
			{
				depth--;
				if ( depth == 0 )
				{
					return source[markerIndex..(i + 1)];
				}
			}
		}

		return string.Empty;
	}

	private static int Count( string source, string value )
	{
		var count = 0;
		var offset = 0;
		while ( (offset = source.IndexOf( value, offset, StringComparison.Ordinal )) >= 0 )
		{
			count++;
			offset += value.Length;
		}

		return count;
	}

	private static bool ValidWeaponConfiguration(
		float fireCooldown = 0.1f,
		float maxRange = 12000f,
		float bulletSize = 1f,
		float baseDamage = 25f,
		float headshotMultiplier = 2f,
		float combinedSpread = 0.1f,
		int bulletCount = 1 )
	{
		return WeaponHostFireRules.IsValidWeaponConfiguration(
			fireCooldown,
			maxRange,
			bulletSize,
			baseDamage,
			headshotMultiplier,
			combinedSpread,
			bulletCount );
	}

	private static WeaponHostFireReject Evaluate(
		bool equipmentValid = true,
		bool ownerValid = true,
		bool callerOwnsPlayer = true,
		bool callerOwnsEquipment = true,
		bool isDeployed = true,
		bool isCurrentEquipment = true,
		bool ownerIsDead = false,
		bool ownerIsRunning = false,
		bool isBlocked = false,
		bool deployDelayElapsed = true,
		bool aimPositionValid = true,
		bool aimForwardValid = true,
		float aimOriginDistance = 16f,
		bool weaponConfigurationValid = true,
		bool requiresAmmo = true,
		bool ammoComponentValid = true,
		int ammo = 1 )
	{
		return WeaponHostFireRules.Evaluate(
			equipmentValid,
			ownerValid,
			callerOwnsPlayer,
			callerOwnsEquipment,
			isDeployed,
			isCurrentEquipment,
			ownerIsDead,
			ownerIsRunning,
			isBlocked,
			deployDelayElapsed,
			aimPositionValid,
			aimForwardValid,
			aimOriginDistance,
			weaponConfigurationValid,
			requiresAmmo,
			ammoComponentValid,
			ammo );
	}

	private static void ExpectReject(
		string name,
		WeaponHostFireReject expected,
		bool equipmentValid = true,
		bool ownerValid = true,
		bool callerOwnsPlayer = true,
		bool callerOwnsEquipment = true,
		bool isDeployed = true,
		bool isCurrentEquipment = true,
		bool ownerIsDead = false,
		bool ownerIsRunning = false,
		bool isBlocked = false,
		bool deployDelayElapsed = true,
		bool aimPositionValid = true,
		bool aimForwardValid = true,
		float aimOriginDistance = 16f,
		bool weaponConfigurationValid = true,
		bool requiresAmmo = true,
		bool ammoComponentValid = true,
		int ammo = 1 )
	{
		Expect(
			$"rejects {name}",
			Evaluate(
				equipmentValid,
				ownerValid,
				callerOwnsPlayer,
				callerOwnsEquipment,
				isDeployed,
				isCurrentEquipment,
				ownerIsDead,
				ownerIsRunning,
				isBlocked,
				deployDelayElapsed,
				aimPositionValid,
				aimForwardValid,
				aimOriginDistance,
				weaponConfigurationValid,
				requiresAmmo,
				ammoComponentValid,
				ammo ) == expected );
	}

	private static void Expect( string name, bool ok )
	{
		if ( ok )
		{
			Console.WriteLine( $"PASS {name}" );
			return;
		}

		_failed++;
		Console.Error.WriteLine( $"FAIL {name}" );
	}
}
