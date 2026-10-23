// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Dxura.RP.Game;
using Ak47Weapon = LifePunch.DXRP.Addons.AK47.AK47;
using LockpickConst = LifePunch.DXRP.Addons.Lockpick.Lockpick;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// One weapon the editor-only roster inject may add as an in-memory Equipment row.
/// <see cref="Enabled"/> is the per-weapon flag from <see cref="WeaponRosterInjectTable"/>.
/// </summary>
public sealed record WeaponRosterInjectEntry(
	string Key,
	string Name,
	string WorldPrefabPath,
	string ViewPrefabPath,
	EquipmentSlot Slot,
	Guid ContentId,
	Guid EquipmentRowId,
	bool Enabled,
	bool ContentIdPinned );

/// <summary>
/// Table for <see cref="WeaponRosterEditorInject"/>: the six non-protected weapons and the lockpick.
/// The inject adds Equipment rows (plus their in-memory addon content) only; this table has no
/// market data and the inject has no market code path.
/// </summary>
public static class WeaponRosterInjectTable
{
	// Each flag is a const bool, one per weapon, and is the owner's to flip (licence, WEAPON-ROSTER-1 Q7).

	// His to flip (licence, Q7): default false, the content id is a fresh editor-only id, not a Portal row.
	public const bool InjectAks74u = false;

	// His to flip (licence, Q7): default false, the content id is a fresh editor-only id, not a Portal row.
	public const bool InjectAr15 = false;

	// His to flip (licence, Q7): default false, the content id is a fresh editor-only id, not a Portal row.
	public const bool InjectDesertEagle = false;

	// His to flip (licence, Q7): default false, the content id is a fresh editor-only id, not a Portal row.
	public const bool InjectM1911 = false;

	// His to flip (licence, Q7): default false, the content id is a fresh editor-only id, not a Portal row.
	public const bool InjectM870 = false;

	// His to flip (licence, Q7): default true only because the content id is the one pinned in code (Ak47DevGive.AkEquipmentId), documented as his own Portal row.
	public const bool InjectAk47 = true;

	// His to flip (licence, Q7): default true only because the content id is the one pinned in code (LockpickDevGive.LockpickEquipmentId), documented as his own Portal row.
	public const bool InjectLockpick = true;

	public static readonly WeaponRosterInjectEntry[] Entries =
	[
		new(
			"ak47",
			"AK-47",
			Ak47Weapon.WorldPrefabPath,
			Ak47Weapon.ViewModelPrefabPath,
			EquipmentSlot.Primary,
			Ak47DevGive.AkEquipmentId,
			new Guid( "82e52f28-1ef2-4dae-88c4-bba0e1c5b4b8" ),
			InjectAk47,
			ContentIdPinned: true ),
		new(
			"aks74u",
			"AKS-74U",
			"addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab",
			"addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab",
			EquipmentSlot.Primary,
			new Guid( "2c0bbbfb-3091-4b7d-a326-e52df098f7f9" ),
			new Guid( "265994e0-8c9e-4df8-80dd-ccdd827f5f47" ),
			InjectAks74u,
			ContentIdPinned: false ),
		new(
			"ar15",
			"AR-15",
			"addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab",
			"addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab",
			EquipmentSlot.Primary,
			new Guid( "eef46616-a741-41d8-afb8-7984ae69a924" ),
			new Guid( "9eeb7f78-a2cd-46a7-94ee-90221310a625" ),
			InjectAr15,
			ContentIdPinned: false ),
		new(
			"deagle",
			"Desert Eagle",
			"addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab",
			"addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab",
			EquipmentSlot.Secondary,
			new Guid( "7d7d47ad-61a4-4a6b-8a61-2753532d8bb4" ),
			new Guid( "f327f01b-0dc3-4292-a2ee-b84c210e4de4" ),
			InjectDesertEagle,
			ContentIdPinned: false ),
		new(
			"m1911",
			"M1911",
			"addons/lifepunch/lpweapons/m1911/equipment/w_m1911/w_m1911.prefab",
			"addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab",
			EquipmentSlot.Secondary,
			new Guid( "fc318f13-ca3f-4986-bb32-578d88437fe4" ),
			new Guid( "4ce025a4-7567-4bdc-90b9-8463106927bb" ),
			InjectM1911,
			ContentIdPinned: false ),
		new(
			"m870",
			"M870",
			"addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab",
			"addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab",
			EquipmentSlot.Primary,
			new Guid( "d09b129e-b28c-4fb2-a531-6d5baa4f9231" ),
			new Guid( "20b61045-2669-47b3-bce7-d0b6f6037ab7" ),
			InjectM870,
			ContentIdPinned: false ),
		new(
			"lockpick",
			"Lockpick",
			LockpickConst.WorldPrefabPath,
			LockpickConst.ViewModelPrefabPath,
			EquipmentSlot.Utility,
			LockpickDevGive.LockpickEquipmentId,
			new Guid( "0c29a5fa-0e43-44eb-b293-0037337a832b" ),
			InjectLockpick,
			ContentIdPinned: true )
	];
}
