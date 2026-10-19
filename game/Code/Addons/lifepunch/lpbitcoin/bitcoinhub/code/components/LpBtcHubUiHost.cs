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
using LifePunch.DXRP.Addons.Bitcoin.HubUi;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// BTC-HUB-OVERHAUL-1 U2: mounts the native-shell Bitcoin Hub UI (ui/hub, LpBtcHubSkeleton) for a hub
/// press. Mirrors LpHashdUiHost; the hub entity picks one host per press from <see cref="NativeUi"/>.
/// </summary>
internal static class LpBtcHubUiHost
{
	/// <summary>Positive code-string ID for the U2 hub press. Exists nowhere else in the tree.</summary>
	public const string PressMark = "LP_BTCHUB_U2_PRESS_20261018";

	/// <summary>Object name of a panel this host (or the dev lane) mounts on its own ScreenPanel.</summary>
	public const string PanelObjectName = "LpBtcHubSkeletonDev";

	// Reason: switch the hub press between the native-shell hub UI (1, default, ruled BH-Q7) and the
	// retiring Bitcoin Ops app (0). Read on the host only (LpBitcoinHubEntity.OpenHubHost), so a
	// client's own value never changes what the host sends. Who: the server operator.
	[ConVar( "lp_btc_hub_native_ui" )]
	public static bool NativeUi { get; set; } = true;

	public static bool IsOpen =>
		Game.ActiveScene?.GetAllComponents<LpBtcHubSkeleton>().FirstOrDefault().IsValid() ?? false;

	public static LpBtcHubSkeleton Open( LpBitcoinHubEntity hub )
	{
		LpBitcoinUi.CloseSuiPreviews();
		LpBitcoinTerminalUiHost.CloseOpen();
		LpHashdUiHost.CloseOpen();
		CloseOpen();

		var panel = MountPanel();
		panel?.BindHub( hub );
		Log.Info( "[btcui2] " + PressMark + " hub=" + ( hub.IsValid() ? hub.GameObject.Id.ToString() : "none" ) + " mounted=" + panel.IsValid() );
		return panel;
	}

	/// <summary>Routes the host's PIN unlock result (LpBitcoinHubEntity.NotifyPinResult) to the open hub UI.</summary>
	public static void OnPinResult( LpBitcoinHubEntity hub, bool ok, string message )
	{
		var panel = Game.ActiveScene?.GetAllComponents<LpBtcHubSkeleton>().FirstOrDefault( p => p.IsValid() && p.BoundHub == hub );
		if ( panel.IsValid() )
			panel.OnPinGateResult( ok, message );
	}

	private static LpBtcHubSkeleton MountPanel()
	{
#if LIFEPUNCH_LOCAL
		return OpenOnScreenPanel();
#else
		var panel = GameManager.ShowUi<LpBtcHubSkeleton>();
		if ( panel.IsValid() )
			return panel;

		// Editor / solo play without HUD root (LpHashdUiHost.cs:48-50).
		Log.Warning( "LpBtcHubUiHost: GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return OpenOnScreenPanel();
#endif
	}

	private static LpBtcHubSkeleton OpenOnScreenPanel()
	{
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		var go = scene.CreateObject();
		go.Name = PanelObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<LpBtcHubSkeleton>();
	}

	public static void CloseOpen()
	{
		var open = Game.ActiveScene?.GetAllComponents<LpBtcHubSkeleton>().ToList();
		if ( open is null )
			return;

		foreach ( var panel in open )
			Close( panel );
	}

	/// <summary>
	/// Removes the panel without touching the shared HUD object: a panel on its own ScreenPanel object
	/// takes the object with it; a panel mounted on the HUD (GameManager.ShowUi) is removed alone.
	/// </summary>
	public static void Close( LpBtcHubSkeleton panel )
	{
		if ( !panel.IsValid() )
			return;

		if ( panel.GameObject.IsValid() && panel.GameObject.Name == PanelObjectName )
			panel.GameObject.Destroy();
		else
			panel.Destroy();
	}
}
