// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

internal static class LpHashdUiHost
{
	private const string PanelObjectName = "LpHashdPanel";

	public static bool IsOpen =>
		Game.ActiveScene?.GetAllComponents<LpHashdPanel>().FirstOrDefault().IsValid() ?? false;

	public static LpHashdPanel Open( LpBitcoinHubEntity hub )
	{
		LpBitcoinUi.CloseSuiPreviews();
		LpBitcoinTerminalUiHost.CloseOpen();
		CloseOpen();

		var panel = MountPanel();
		panel?.BindHub( hub );
		return panel;
	}

	private static LpHashdPanel MountPanel()
	{
#if LIFEPUNCH_LOCAL
		return OpenOnScreenPanel();
#else
		var panel = GameManager.ShowUi<LpHashdPanel>();
		if ( panel.IsValid() )
			return panel;

		// Editor / solo play without HUD root — dev preview ConCmds still need a visible panel.
		Log.Warning( "LpHashdUiHost: GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return OpenOnScreenPanel();
#endif
	}

	private static LpHashdPanel OpenOnScreenPanel()
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		var go = scene.CreateObject();
		go.Name = PanelObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<LpHashdPanel>();
	}

	public static void CloseOpen()
	{
		var panel = Game.ActiveScene?.GetAllComponents<LpHashdPanel>().FirstOrDefault();
		Close( panel );
	}

	private static void Close( LpHashdPanel panel )
	{
		if ( !panel.IsValid() )
			return;

		if ( panel.GameObject.IsValid() && panel.GameObject.Name == PanelObjectName )
			panel.GameObject.Destroy();
		else
			panel.Destroy();
	}

	public static int GetLocalWalletCash() => LpBitcoinWallet.GetLocalCash();
}
