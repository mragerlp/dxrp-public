// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────
namespace LifePunch.DXRP.Addons.Weapons.M870;

/// <summary>
/// The named facts of the pump shotgun. Pure constants: no engine types, so the offline rules harness
/// reads the same values the game does. The prefab carries the numbers the engine uses (AmmoComponent,
/// ShootWeaponComponent); the harness checks the prefab against these constants.
/// </summary>
public static class LpPumpShotgunSpec
{
	/// <summary>Display name. One constant so a rename is one line (ruling W-SG-1: M870 or another name).</summary>
	public const string DisplayName = "M870";

	/// <summary>Shell gauge label (ruling W-SG-2). The hull effect is the upstream 12 gauge casing.</summary>
	public const string GaugeLabel = "12 gauge";

	/// <summary>Nominal hull length of a 2 3/4 inch 12 gauge shell, in inches.</summary>
	public const float HullLengthInches = 2.75f;

	/// <summary>Nominal hull body diameter of a 12 gauge shell, in inches.</summary>
	public const float HullDiameterInches = 0.80f;

	/// <summary>Shells the tube magazine holds (ruling W-SG-3).</summary>
	public const int TubeCapacity = 7;

	/// <summary>Shells the chamber holds.</summary>
	public const int ChamberCapacity = 1;

	/// <summary>
	/// Loaded capacity. Upstream AmmoComponent is one pool with no separate chamber, so tube and chamber
	/// are one number: AmmoComponent.MaxAmmo on the world prefab must equal this.
	/// </summary>
	public const int LoadedCapacity = TubeCapacity + ChamberCapacity;

	/// <summary>
	/// The host accepts a semi-only weapon's next shot after Delay * this (ShootWeaponComponent.cs:418).
	/// The pump cycle must be no longer than that, or the host would accept a shot mid-cycle.
	/// </summary>
	public const float HostFireFloorScale = 0.85f;

	/// <summary>The upstream casing prefab thrown on the pump stroke.</summary>
	public const string HullPrefabPath = "prefabs/weapon_effects/12g_casing.prefab";
}
