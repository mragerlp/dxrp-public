// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// KEYPAD-1 (cc-lp-2): identity and positive code-string IDs for the keypad + fading door addon. No money anywhere
// in this addon (Law B): no wallet, no price, no value-moving RPC.

namespace LifePunch.DXRP.Addons.Keypad;

public static class LpKeypadIdent
{
	/// <summary>s&amp;box package ident for this addon.</summary>
	public const string SboxPackage = "lifepunch.keypad";

	/// <summary>Addon ident (folder name).</summary>
	public const string AddonIdent = "lpkeypad";

	/// <summary>The hub key (<see cref="ILpHub.HubKey"/>) the result relay and a future PlayerHub door use.</summary>
	public const string HubKey = "lifepunch.keypad";

	public const string Wordmark = "lifepunch";
	public const string WordmarkAccent = ".keypad";
	public const string ProductName = "Keypad";

	/// <summary>
	/// PLACEHOLDER body model: the in-tree core keypad model (game/Assets/models/wire/keypad/keypad.vmdl). The
	/// keypad's own model choice is owed; this keeps the entity pressable and visible until then.
	/// </summary>
	public const string PlaceholderModelPath = "models/wire/keypad/keypad.vmdl";

	/// <summary>PLACEHOLDER sample door model for the dev spawn only (a core DXRP jail cell door).</summary>
	public const string SampleDoorModelPath = "models/gameplay/jail/cell_door/cell_door.vmdl";

	// Positive code-string IDs. Each exists nowhere else in the tree; a log line carrying one proves the new bytes ran.
	public const string SpawnMark = "LP_KEYPAD_SPAWN_20261026";
	public const string SetMark = "LP_KEYPAD_SET_20261026";
	public const string TryMark = "LP_KEYPAD_TRY_20261026";
	public const string LinkMark = "LP_KEYPAD_LINK_20261026";
	public const string FadeMark = "LP_KEYPAD_FADE_20261026";
	public const string RestoreMark = "LP_KEYPAD_RESTORE_20261026";
	public const string UiMark = "LP_KEYPAD_UI_20261026";
}
