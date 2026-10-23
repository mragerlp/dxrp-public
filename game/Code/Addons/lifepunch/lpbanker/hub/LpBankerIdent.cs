// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D), deepened in BANKER-HUB-2 (DRIVE-AWAY-2-U1). Identity, paths and the
// Banker-only gate. No money path.

using Dxura.RP.Game;
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

	/// <summary>The key the shared hub kit (LpHubAccess, LpHubResultRelay) knows the Banker hub by (SPINE-3).</summary>
	public const string HubKey = "banker";

	/// <summary>Positive code-string ID for the menu skeleton. Exists nowhere else in the tree.</summary>
	public const string SkeletonMark = "LP_BANKER_HUB_SKELETON_20261017";

	/// <summary>Positive code-string ID for the BANKER-HUB-2 view model. Exists nowhere else in the tree.</summary>
	public const string HubVmMark = "LP_BANKER_HUB2_VM_20261017";

	/// <summary>PLACEHOLDER pile model (I4, the hub model choice, is owed): the landed money stack (BANK-ASSETS-2).</summary>
	public const string PileModelPath = "addons/lifepunch/lpbanker/bankreserve/assets/models/money_stacked_01.vmdl";

	/// <summary>The pile model's import scale as landed (M6 recommendation; R-M6-1 is owed, so PROVISIONAL).</summary>
	public const string PileScaleNote = "import_scale 0.4082 (R-M6-1 PROVISIONAL)";
}

/// <summary>
/// The Banker-only gate. DIRECTION_BANKER-HUB_R1 section 7: the job system, not the UI, enforces the one Banker slot.
/// Design (exemplar: the market whitelist, GameModeMarketItems.cs:187-192, JobTags.HasNamedTag at JobTags.cs:92):
/// the host resolves the caller (GameUtils.GetPlayerByConnectionId, GameUtils.cs:40) and asks whether the caller's
/// job carries the named tag <see cref="BankerJobTag"/>; the one slot is that job's MaxCount in the job config.
/// No Banker job exists in this tree, so <see cref="JobGateEnabled"/> stays false and the gate is the editor seam:
/// it fails closed outside the editor and opens for nobody in a real game.
/// </summary>
public static class LpBankerAccess
{
	/// <summary>The named job tag a Banker job will carry (PLACEHOLDER until the job exists).</summary>
	public const string BankerJobTag = "banker";

	/// <summary>Flip to true in the slice that lands the Banker job; until then the editor seam decides.</summary>
	public static bool JobGateEnabled { get; } = false;

	public static bool IsBanker( Player player ) => player.IsValid() && JobTags.HasNamedTag( player.Job, BankerJobTag );

	public static bool CanUse( Player player ) => JobGateEnabled ? IsBanker( player ) : Application.IsEditor;

	public static bool CanUseLocal() => CanUse( GameUtils.GetPlayerByConnectionId( Connection.Local.Id ) );
}
