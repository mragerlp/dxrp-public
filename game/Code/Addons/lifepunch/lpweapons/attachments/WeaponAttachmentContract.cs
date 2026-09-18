using System;
using System.Collections.Generic;
using Dxura.RP.Game;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

public enum WeaponAttachmentKind : byte
{
	None = 0,
	PistolSuppressor,
	PbsSuppressor,
	RedDot,
	Laser
}

public enum WeaponAttachmentSlot : byte
{
	Muzzle = 0,
	Optic,
	Rail
}

public enum WeaponAttachmentPerspective : byte
{
	FirstPerson = 0,
	ThirdPerson,
	Dropped
}

public enum WeaponAttachmentTransactionFailure : byte
{
	None = 0,
	InvalidRequest,
	Incompatible,
	SlotOccupied,
	SlotEmpty,
	InventoryTakeFailed,
	InventoryGiveFailed,
	StateCommitFailed,
	CompensationFailed,
	ReconciliationRequired
}

public readonly record struct WeaponAttachmentIdentity( WeaponAttachmentKind Kind, Guid ItemId )
{
	public static WeaponAttachmentIdentity Empty => default;

	public bool IsEmpty => Kind == WeaponAttachmentKind.None && ItemId == Guid.Empty;

	public bool IsPresent => Kind != WeaponAttachmentKind.None && ItemId != Guid.Empty;
}

public readonly record struct WeaponAttachmentLoadout(
	WeaponAttachmentIdentity Muzzle,
	WeaponAttachmentIdentity Optic,
	WeaponAttachmentIdentity Rail,
	bool LaserEnabled )
{
	public static WeaponAttachmentLoadout Empty => default;

	public bool HasItemIdentity => Muzzle.ItemId != Guid.Empty
		|| Optic.ItemId != Guid.Empty
		|| Rail.ItemId != Guid.Empty;

	public bool TryAttach(
		WeaponAttachmentKind kind,
		Guid itemId,
		out WeaponAttachmentLoadout replacement )
	{
		replacement = this;
		if ( itemId == Guid.Empty || !WeaponAttachmentSlots.TryGetSlot( kind, out var slot ) )
		{
			return false;
		}

		if ( !Get( slot ).IsEmpty )
		{
			return false;
		}

		var identity = new WeaponAttachmentIdentity( kind, itemId );
		replacement = slot switch
		{
			WeaponAttachmentSlot.Muzzle => this with { Muzzle = identity },
			WeaponAttachmentSlot.Optic => this with { Optic = identity },
			WeaponAttachmentSlot.Rail => this with { Rail = identity },
			_ => this
		};

		return replacement != this;
	}

	public bool TryDetach(
		WeaponAttachmentSlot slot,
		out WeaponAttachmentIdentity detached,
		out WeaponAttachmentLoadout replacement )
	{
		detached = Get( slot );
		replacement = this;
		if ( detached.ItemId == Guid.Empty )
		{
			return false;
		}

		replacement = slot switch
		{
			WeaponAttachmentSlot.Muzzle => this with { Muzzle = WeaponAttachmentIdentity.Empty },
			WeaponAttachmentSlot.Optic => this with { Optic = WeaponAttachmentIdentity.Empty },
			WeaponAttachmentSlot.Rail => this with
			{
				Rail = WeaponAttachmentIdentity.Empty,
				LaserEnabled = false
			},
			_ => this
		};

		return replacement != this;
	}

	public bool TrySetLaserEnabled( bool enabled, out WeaponAttachmentLoadout replacement )
	{
		replacement = this;
		if ( enabled && (Rail.Kind != WeaponAttachmentKind.Laser || !Rail.IsPresent) )
		{
			return false;
		}

		replacement = this with { LaserEnabled = enabled };
		return true;
	}

	public WeaponAttachmentIdentity Get( WeaponAttachmentSlot slot )
	{
		return slot switch
		{
			WeaponAttachmentSlot.Muzzle => Muzzle,
			WeaponAttachmentSlot.Optic => Optic,
			WeaponAttachmentSlot.Rail => Rail,
			_ => WeaponAttachmentIdentity.Empty
		};
	}
}

public static class WeaponAttachmentSnapshotCodec
{
	public const string SnapshotKey = "lifepunch.weapon-attachments/v1";

	private const string Version = "1";
	private const int FieldCount = 8;
	private const int MaxPayloadLength = 512;

	public static bool TrySerialize( WeaponAttachmentLoadout loadout, out string payload )
	{
		payload = string.Empty;
		if ( !IsValid( loadout ) )
		{
			return false;
		}

		payload = string.Join(
			'|',
			Version,
			((byte)loadout.Muzzle.Kind).ToString(),
			loadout.Muzzle.ItemId.ToString( "N" ),
			((byte)loadout.Optic.Kind).ToString(),
			loadout.Optic.ItemId.ToString( "N" ),
			((byte)loadout.Rail.Kind).ToString(),
			loadout.Rail.ItemId.ToString( "N" ),
			loadout.LaserEnabled ? "1" : "0" );
		return true;
	}

	public static bool TryDeserialize( string payload, out WeaponAttachmentLoadout loadout )
	{
		loadout = default;
		if ( string.IsNullOrWhiteSpace( payload ) || payload.Length > MaxPayloadLength )
		{
			return false;
		}

		var fields = payload.Split( '|', StringSplitOptions.None );
		if ( fields.Length != FieldCount || fields[0] != Version )
		{
			return false;
		}

		if ( !TryDeserializeIdentity( fields[1], fields[2], WeaponAttachmentSlot.Muzzle, out var muzzle )
		     || !TryDeserializeIdentity( fields[3], fields[4], WeaponAttachmentSlot.Optic, out var optic )
		     || !TryDeserializeIdentity( fields[5], fields[6], WeaponAttachmentSlot.Rail, out var rail )
		     || (fields[7] != "0" && fields[7] != "1") )
		{
			return false;
		}

		var candidate = new WeaponAttachmentLoadout( muzzle, optic, rail, fields[7] == "1" );
		if ( !IsValid( candidate ) )
		{
			return false;
		}

		loadout = candidate;
		return true;
	}

	public static bool IsValid( WeaponAttachmentLoadout loadout )
	{
		return IsValidIdentity( loadout.Muzzle, WeaponAttachmentSlot.Muzzle )
		       && IsValidIdentity( loadout.Optic, WeaponAttachmentSlot.Optic )
		       && IsValidIdentity( loadout.Rail, WeaponAttachmentSlot.Rail )
		       && (!loadout.LaserEnabled
		           || (loadout.Rail.Kind == WeaponAttachmentKind.Laser && loadout.Rail.IsPresent));
	}

	private static bool TryDeserializeIdentity(
		string kindValue,
		string itemIdValue,
		WeaponAttachmentSlot expectedSlot,
		out WeaponAttachmentIdentity identity )
	{
		identity = default;
		if ( !byte.TryParse( kindValue, out var rawKind )
		     || !Guid.TryParseExact( itemIdValue, "N", out var itemId ) )
		{
			return false;
		}

		identity = new WeaponAttachmentIdentity( (WeaponAttachmentKind)rawKind, itemId );
		return IsValidIdentity( identity, expectedSlot );
	}

	private static bool IsValidIdentity(
		WeaponAttachmentIdentity identity,
		WeaponAttachmentSlot expectedSlot )
	{
		if ( identity.IsEmpty )
		{
			return true;
		}

		return identity.IsPresent
		       && Enum.IsDefined( typeof( WeaponAttachmentKind ), identity.Kind )
		       && WeaponAttachmentSlots.TryGetSlot( identity.Kind, out var actualSlot )
		       && actualSlot == expectedSlot;
	}
}

public static class WeaponAttachmentSlots
{
	public static WeaponAttachmentSlot GetSlot( WeaponAttachmentKind kind )
	{
		if ( TryGetSlot( kind, out var slot ) )
		{
			return slot;
		}

		throw new ArgumentOutOfRangeException( nameof(kind), kind, "Unknown attachment kind." );
	}

	public static bool TryGetSlot( WeaponAttachmentKind kind, out WeaponAttachmentSlot slot )
	{
		switch ( kind )
		{
			case WeaponAttachmentKind.PistolSuppressor:
			case WeaponAttachmentKind.PbsSuppressor:
				slot = WeaponAttachmentSlot.Muzzle;
				return true;
			case WeaponAttachmentKind.RedDot:
				slot = WeaponAttachmentSlot.Optic;
				return true;
			case WeaponAttachmentKind.Laser:
				slot = WeaponAttachmentSlot.Rail;
				return true;
			default:
				slot = default;
				return false;
		}
	}
}

public static class WeaponAttachmentCompatibility
{
	private const string GlockWorldPrefab =
		"addons/lifepunch/lpweapons/glock/equipment/w_glock/w_glock.prefab";
	private const string Aks74uOriginalWorldPrefab =
		"addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab";
	private const string Ak47WorldPrefab =
		"addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab";

	public static bool CanAttach( string weaponId, WeaponAttachmentKind kind )
	{
		if ( string.IsNullOrWhiteSpace( weaponId ) )
		{
			return false;
		}

		return kind switch
		{
			WeaponAttachmentKind.PistolSuppressor => IsWeapon( weaponId, "glock" ),
			WeaponAttachmentKind.RedDot => IsWeapon( weaponId, "glock" ),
			WeaponAttachmentKind.Laser => IsWeapon( weaponId, "glock" ),
			WeaponAttachmentKind.PbsSuppressor => IsWeapon( weaponId, "aks74u_original" )
				|| IsWeapon( weaponId, "ak47" ),
			_ => false
		};
	}

	public static bool MatchesEquipmentPrefab( string weaponId, string prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( weaponId ) || string.IsNullOrWhiteSpace( prefabPath ) )
		{
			return false;
		}

		var expectedPrefab = IsWeapon( weaponId, "glock" )
			? GlockWorldPrefab
			: IsWeapon( weaponId, "aks74u_original" )
				? Aks74uOriginalWorldPrefab
				: IsWeapon( weaponId, "ak47" )
					? Ak47WorldPrefab
					: string.Empty;
		if ( string.IsNullOrEmpty( expectedPrefab ) )
		{
			return false;
		}

		var normalizedPrefab = prefabPath.Trim().Replace( '\\', '/' );
		return string.Equals( normalizedPrefab, expectedPrefab, StringComparison.OrdinalIgnoreCase );
	}

	public static bool CanRestoreLoadout( string weaponId, WeaponAttachmentLoadout loadout )
	{
		if ( !WeaponAttachmentSnapshotCodec.IsValid( loadout ) )
		{
			return false;
		}

		return CanRestoreIdentity( weaponId, loadout.Muzzle )
		       && CanRestoreIdentity( weaponId, loadout.Optic )
		       && CanRestoreIdentity( weaponId, loadout.Rail );
	}

	private static bool CanRestoreIdentity( string weaponId, WeaponAttachmentIdentity identity )
	{
		return identity.IsEmpty || (identity.IsPresent && CanAttach( weaponId, identity.Kind ));
	}

	private static bool IsWeapon( string actual, string expected )
	{
		return string.Equals( actual.Trim(), expected, StringComparison.OrdinalIgnoreCase );
	}
}

public interface IWeaponAttachmentInventoryGateway
{
	bool TryTake( Guid itemId, int quantity );
	bool TryGive( Guid itemId, int quantity );
}

public interface IWeaponAttachmentStateGateway
{
	WeaponAttachmentLoadout Current { get; }
	bool TryReplace( WeaponAttachmentLoadout expected, WeaponAttachmentLoadout replacement );
}

public readonly record struct WeaponAttachmentTransactionResult(
	bool Succeeded,
	WeaponAttachmentTransactionFailure Failure,
	bool CompensationRequired )
{
	public static WeaponAttachmentTransactionResult Success => new(
		true,
		WeaponAttachmentTransactionFailure.None,
		false );

	public static WeaponAttachmentTransactionResult Failed(
		WeaponAttachmentTransactionFailure failure,
		bool compensationRequired = false )
	{
		return new WeaponAttachmentTransactionResult( false, failure, compensationRequired );
	}
}

public sealed class WeaponAttachmentReconciliationQuarantine
{
	private readonly HashSet<long> _owners = [];

	public bool IsRequired( long ownerId )
	{
		if ( ownerId <= 0 )
		{
			return false;
		}

		lock ( _owners )
		{
			return _owners.Contains( ownerId );
		}
	}

	public bool Require( long ownerId )
	{
		if ( ownerId <= 0 )
		{
			return false;
		}

		lock ( _owners )
		{
			return _owners.Add( ownerId );
		}
	}
}

public static class WeaponAttachmentTransactionController
{
	public static WeaponAttachmentTransactionResult Attach(
		string weaponId,
		WeaponAttachmentKind kind,
		Guid itemId,
		IWeaponAttachmentInventoryGateway inventory,
		IWeaponAttachmentStateGateway state )
	{
		if ( inventory is null || state is null || itemId == Guid.Empty
			|| !WeaponAttachmentSlots.TryGetSlot( kind, out var slot ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest );
		}

		var current = state.Current;
		if ( !current.Get( slot ).IsEmpty )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.SlotOccupied );
		}

		if ( !WeaponAttachmentCompatibility.CanAttach( weaponId, kind ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.Incompatible );
		}

		if ( !current.TryAttach( kind, itemId, out var replacement ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest );
		}

		if ( !inventory.TryTake( itemId, 1 ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InventoryTakeFailed );
		}

		if ( state.TryReplace( current, replacement ) )
		{
			return WeaponAttachmentTransactionResult.Success;
		}

		if ( inventory.TryGive( itemId, 1 ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.StateCommitFailed );
		}

		return WeaponAttachmentTransactionResult.Failed(
			WeaponAttachmentTransactionFailure.CompensationFailed,
			true );
	}

	public static WeaponAttachmentTransactionResult Detach(
		WeaponAttachmentSlot slot,
		IWeaponAttachmentInventoryGateway inventory,
		IWeaponAttachmentStateGateway state )
	{
		if ( inventory is null || state is null || !IsKnownSlot( slot ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InvalidRequest );
		}

		var current = state.Current;
		if ( !current.TryDetach( slot, out var detached, out var replacement ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.SlotEmpty );
		}

		if ( !state.TryReplace( current, replacement ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.StateCommitFailed );
		}

		if ( inventory.TryGive( detached.ItemId, 1 ) )
		{
			return WeaponAttachmentTransactionResult.Success;
		}

		if ( state.TryReplace( replacement, current ) )
		{
			return WeaponAttachmentTransactionResult.Failed(
				WeaponAttachmentTransactionFailure.InventoryGiveFailed );
		}

		return WeaponAttachmentTransactionResult.Failed(
			WeaponAttachmentTransactionFailure.CompensationFailed,
			true );
	}

	private static bool IsKnownSlot( WeaponAttachmentSlot slot )
	{
		return slot is WeaponAttachmentSlot.Muzzle
			or WeaponAttachmentSlot.Optic
			or WeaponAttachmentSlot.Rail;
	}
}

public static class WeaponAttachmentDropPolicy
{
	public static bool CanMergeDuplicate( WeaponAttachmentLoadout loadout )
	{
		return !loadout.HasItemIdentity;
	}
}

public static class WeaponAttachmentCatalog
{
	public static string GetGrantIdentifier( WeaponAttachmentKind kind )
	{
		return kind switch
		{
			WeaponAttachmentKind.PistolSuppressor => "lifepunch.weapon_attachment.pistol_suppressor",
			WeaponAttachmentKind.PbsSuppressor => "lifepunch.weapon_attachment.pbs_suppressor",
			WeaponAttachmentKind.RedDot => "lifepunch.weapon_attachment.red_dot",
			WeaponAttachmentKind.Laser => "lifepunch.weapon_attachment.laser",
			_ => throw new ArgumentOutOfRangeException( nameof(kind), kind, "Unknown attachment kind." )
		};
	}

	public static bool MatchesGrantIdentifier( WeaponAttachmentKind kind, string grantIdentifier )
	{
		if ( string.IsNullOrWhiteSpace( grantIdentifier )
			|| !WeaponAttachmentSlots.TryGetSlot( kind, out _ ) )
		{
			return false;
		}

		return string.Equals(
			GetGrantIdentifier( kind ),
			grantIdentifier.Trim(),
			StringComparison.OrdinalIgnoreCase );
	}
}

public static class WeaponAttachmentPresentation
{
	public static bool IsAttachmentRendererVisible(
		WeaponAttachmentLoadout loadout,
		WeaponAttachmentKind kind )
	{
		if ( !WeaponAttachmentSlots.TryGetSlot( kind, out var slot ) )
		{
			return false;
		}

		var identity = loadout.Get( slot );
		return identity.IsPresent && identity.Kind == kind;
	}

	public static byte ResolveFirePresentation( WeaponAttachmentLoadout loadout )
	{
		if ( loadout.Muzzle.IsPresent
			&& loadout.Muzzle.Kind is WeaponAttachmentKind.PistolSuppressor
				or WeaponAttachmentKind.PbsSuppressor )
		{
			return (byte)(WeaponFirePresentation.SuppressMuzzleFlash
				| WeaponFirePresentation.UseSuppressedSound);
		}

		return (byte)WeaponFirePresentation.None;
	}

	public static bool IsLaserVisible(
		WeaponAttachmentLoadout loadout,
		WeaponAttachmentPerspective perspective )
	{
		_ = perspective;
		return loadout.LaserEnabled
			&& loadout.Rail.IsPresent
			&& loadout.Rail.Kind == WeaponAttachmentKind.Laser;
	}
}
