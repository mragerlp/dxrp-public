// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

internal static class LpBitcoinTerminalUiHost
{
	private const string PanelObjectName = "LpBitcoinTerminalPanel";

	public static bool IsOpen =>
		Game.ActiveScene?.GetAllComponents<LpBitcoinTerminalPanel>().FirstOrDefault().IsValid() ?? false;

	public static LpBitcoinTerminalPanel Open( LpBitcoinHubEntity hub, LpBitcoinRackEntity focusRack = null )
	{
		LpHashdUiHost.CloseOpen();
		CloseOpen();

		var panel = MountPanel();
		panel?.Bind( hub, focusRack );
		return panel;
	}

	private static LpBitcoinTerminalPanel MountPanel()
	{
#if LIFEPUNCH_LOCAL
		return OpenOnScreenPanel();
#else
		var panel = GameManager.ShowUi<LpBitcoinTerminalPanel>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( "LpBitcoinTerminalUiHost: GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return OpenOnScreenPanel();
#endif
	}

	private static LpBitcoinTerminalPanel OpenOnScreenPanel()
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		var go = scene.CreateObject();
		go.Name = PanelObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<LpBitcoinTerminalPanel>();
	}

	public static void CloseOpen()
	{
		var panel = Game.ActiveScene?.GetAllComponents<LpBitcoinTerminalPanel>().FirstOrDefault();
		Close( panel );
	}

	private static void Close( LpBitcoinTerminalPanel panel )
	{
		if ( !panel.IsValid() )
			return;

		if ( panel.GameObject.IsValid() && panel.GameObject.Name == PanelObjectName )
			panel.GameObject.Destroy();
		else
			panel.Destroy();
	}
}
