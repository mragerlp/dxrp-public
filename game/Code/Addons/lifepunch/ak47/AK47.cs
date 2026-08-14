// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "AK-47" (s&box ident: lifepunch.ak47 · addon ident: ak47) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.AK47;

public static class AK47
{
	public const string Package = "lifepunch.ak47";
	public const string Ident = "ak47";
	public const string DisplayName = "AK-47";
	public const string Grouping = "Secondary";
	public const string WeaponClass = "assault-rifle";
	public const string DxrpClassReference = "m4a1";
	public const string Cs2ReferenceMesh = "weapon_rif_ak47";

	public const string WorldPrefabPath = "addons/lifepunch/ak47/equipment/w_ak47/w_ak47.prefab";
	public const string ViewModelPrefabPath = "addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab";
	public const string WorldModelPath = "addons/lifepunch/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl";
	public const string ClassWorldPrefabPlaceholder = "gameplay/equipment/weapons/m4a1/w_m4a1.prefab";
	public const string ClassViewModelPlaceholder = "gameplay/equipment/weapons/m4a1/vm_m4a1.prefab";
	public const string FireSoundPath = "addons/lifepunch/ak47/sounds/ak47_shot.sound";
	public const string FireDistantSoundPath = "addons/lifepunch/ak47/sounds/ak47_shot_distant.sound";
	public const string ReloadClipOutSoundPath = "addons/lifepunch/ak47/sounds/ak47_reload_clipout.sound";
	public const string ReloadClipInSoundPath = "addons/lifepunch/ak47/sounds/ak47_reload_clipin.sound";
	public const string CockSoundPath = "addons/lifepunch/ak47/sounds/ak47_cock.sound";
	public const string DrawSoundPath = "addons/lifepunch/ak47/sounds/ak47_draw.sound";
	public const string IconPath = "addons/lifepunch/ak47/ui/ak47_killfeed.png";
	public const string DevGiveCommand = "lp_give_ak_class";

	public static AK47WeaponStats Stats { get; } = new()
	{
		Damage = 28,
		RoundsPerMinute = 600,
		MagazineSize = 30,
		ReserveAmmo = 90,
		ReloadSeconds = 2.4f,
		RangeMeters = 120,
		SpreadDegrees = 1.8f,
		RecoilPitch = 2.2f,
		RecoilYaw = 0.85f,
		Automatic = true
	};
}

public sealed class AK47WeaponStats
{
	public int Damage { get; init; }
	public int RoundsPerMinute { get; init; }
	public int MagazineSize { get; init; }
	public int ReserveAmmo { get; init; }
	public float ReloadSeconds { get; init; }
	public int RangeMeters { get; init; }
	public float SpreadDegrees { get; init; }
	public float RecoilPitch { get; init; }
	public float RecoilYaw { get; init; }
	public bool Automatic { get; init; }

	public float SecondsBetweenShots => 60f / RoundsPerMinute;
}
