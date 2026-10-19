// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). DRIVE-AWAY-2-U2 (BTC-UI-3): mounts the btcui2 hub skeleton
// in the Play scene from game code and drives its proof seams (OS-synthesised clicks do not reach panels). Earlier
// proofs mounted it as a temporary editor object; editor-scene tools write the editor document, so this lane
// creates the panel in Game.ActiveScene and never touches game.scene.

#if !LIFEPUNCH_LOCAL

using System.Linq;
using Sandbox;
using LifePunch.DXRP.Addons.Bitcoin;
using LifePunch.DXRP.Addons.Bitcoin.HubUi;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY. Play-scene objects only; game.scene is never written.</summary>
public static class LpBtcHubDev
{
	/// <summary>Positive code-string ID for the Play-scene mount. Exists nowhere else in the tree.</summary>
	public const string MountMark = "LP_BTCUI3_MOUNT_20261017";

	// U2: the host's name, so LpBtcHubUiHost.Close takes this dev object with the panel.
	private const string PanelObjectName = LpBtcHubUiHost.PanelObjectName;

	private static LpBtcHubSkeleton Current => Game.ActiveScene?.GetAllComponents<LpBtcHubSkeleton>().FirstOrDefault();

	// Reason: mount the btcui2 hub in the Play scene for the proof (it reads the viewer's own hub). Who: the editor only.
	[ConCmd( "lp_btcui2_open" )]
	public static void Open()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_btcui2_open: editor only." );
			return;
		}

		CloseAll();
		var scene = Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "lp_btcui2_open: no active scene - start Play first." );
			return;
		}

		var go = scene.CreateObject();
		go.Name = PanelObjectName;
		go.AddComponent<ScreenPanel>();
		go.AddComponent<LpBtcHubSkeleton>();
		Log.Info( $"[btcui2] {MountMark} root={go.Id} scene={scene.Name}" );
	}

	// Reason: unlock through the keypad path (ProofUnlock). Who: the editor only.
	[ConCmd( "lp_btcui2_unlock" )]
	public static void Unlock()
	{
		var hub = Current;
		if ( !Application.IsEditor || !hub.IsValid() )
		{
			Log.Warning( "lp_btcui2_unlock: no btcui2 hub open - run lp_btcui2_open first." );
			return;
		}

		hub.ProofUnlock();
		Log.Info( "lp_btcui2_unlock: unlocked" );
	}

	// Reason: select a tab (0 = Overview .. 6 = Settings) through the tab path. Who: the editor only.
	[ConCmd( "lp_btcui2_tab" )]
	public static void Tab( int tab )
	{
		var hub = Current;
		if ( !Application.IsEditor || !hub.IsValid() )
		{
			Log.Warning( "lp_btcui2_tab: no btcui2 hub open - run lp_btcui2_open first." );
			return;
		}

		if ( tab < 0 || tab > 6 )
		{
			Log.Warning( $"lp_btcui2_tab: tab {tab} is out of range - use 0 (Overview) to 6 (Settings)." );
			return;
		}

		hub.ProofSelectTabIndex( tab );
		Log.Info( $"lp_btcui2_tab: tab={tab}" );
	}

	// Reason: close every mounted dev copy. Who: the editor only.
	[ConCmd( "lp_btcui2_close" )]
	// U2: through the host, which never destroys the shared HUD object a pressed hub mounts on.
	public static void CloseAll() => LpBtcHubUiHost.CloseOpen();
}

#endif
