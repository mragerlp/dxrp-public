// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D). Identity, paths and the Banker-only gate seam. No money path.

using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public static class LpBankerIdent
{
	/// <summary>s&amp;box package ident (lifepunch repo lpbanker/bankerhub LpBankVaultEntity.cs:4).</summary>
	public const string SboxPackage = "lifepunch.banker";

	/// <summary>Addon ident in the same declaration.</summary>
	public const string AddonIdent = "bankerjob";

	public const string Wordmark = "lifepunch";
	public const string WordmarkAccent = ".banker";
	public const string ProductName = "Banker Hub";

	/// <summary>Positive code-string ID for the menu skeleton. Exists nowhere else in the tree.</summary>
	public const string SkeletonMark = "LP_BANKER_HUB_SKELETON_20261017";

	/// <summary>PLACEHOLDER pile model (ruling 4, the hub model choice, is owed): the landed money stack (BANK-ASSETS-2).</summary>
	public const string PileModelPath = "addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_01.vmdl";
}

/// <summary>
/// The Banker-only gate seam. PLACEHOLDER: no Banker job exists in this tree, and DIRECTION_BANKER-HUB_R1
/// says the job system (not the UI) enforces the one Banker slot. Until the job lands this fails closed
/// outside the editor, so the skeleton can be proven in the editor and opens for nobody in a real game.
/// </summary>
public static class LpBankerAccess
{
	public static bool CanUse() => Application.IsEditor;
}
