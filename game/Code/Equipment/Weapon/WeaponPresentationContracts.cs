using System;

namespace Dxura.RP.Game;

[Flags]
public enum WeaponFirePresentation : byte
{
	None = 0,
	SuppressMuzzleFlash = 1 << 0,
	UseSuppressedSound = 1 << 1
}

public static class WeaponFirePresentationRules
{
	public static bool ShouldSuppressMuzzleFlash( byte presentation )
	{
		return ((WeaponFirePresentation)presentation & WeaponFirePresentation.SuppressMuzzleFlash) != 0;
	}

	public static bool ShouldUseSuppressedSound( byte presentation, bool suppressedSoundAvailable )
	{
		return suppressedSoundAvailable
			&& ((WeaponFirePresentation)presentation & WeaponFirePresentation.UseSuppressedSound) != 0;
	}
}

public interface IWeaponFirePresentationProvider
{
	byte GetFirePresentation();
}

public interface IDroppedWeaponMergePolicy
{
	bool CanMergeDuplicate();
}

public interface IDroppedWeaponPresentationSource
{
	bool TryCopyPresentationTo( DroppedEquipment dropped );
}
