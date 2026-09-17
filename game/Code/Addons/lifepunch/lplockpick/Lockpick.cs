// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "Lockpick" (addon ident: lplockpick) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.Lockpick;

public static class Lockpick
{
	public const string Package = "lifepunch.lplockpick";
	public const string Ident = "lplockpick";
	public const string DisplayName = "Lockpick";
	public const string Grouping = "Utility";

	public const string WorldPrefabPath = "gameplay/equipment/job/lockpick/w_lockpick.prefab";
	public const string ViewModelPrefabPath = "gameplay/equipment/job/lockpick/vm_lockpick.prefab";
	public const string WorldModelPath = "addons/lifepunch/lplockpick/lockpick/source/crowbar.vmdl";

	/// <summary>REUSE-FIRST: the approved pry sound set, verbatim.</summary>
	public const string PickSoundPath = "gameplay/equipment/job/pry_bar/sounds/pry.sound";

	/// <summary>P-2: pry_bar icon for v1. Zero new icon bytes.</summary>
	public const string IconPath = "ui/equipment/pry_bar.png";

	public const string DevGiveCommand = "lp_give_lockpick";
}
