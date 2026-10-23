// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D): mounts the Banker hub on its own ScreenPanel (the LpHashdUiHost
// fallback path, UH:54-63). One open at a time.
// SPINE-3 (SPINE-1 R1 U2): mounts through the shared LpHubMount - the DXRP HUD root first, the ScreenPanel
// fallback when there is none - and closes only the object it created.

using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public static class LpBankerHubUi
{
	private const string PanelObjectName = "LpBankerHubRoot";

	public static LpBankerHubRoot Open( int tabIndex )
	{
		var root = LpHubMount.Mount<LpBankerHubRoot>( PanelObjectName, "lpbanker" );
		if ( root.IsValid() )
			root.InitialTabIndex = tabIndex;

		return root;
	}

	public static LpBankerHubRoot Current => Game.ActiveScene?.GetAllComponents<LpBankerHubRoot>().FirstOrDefault();

	public static void CloseOpen() => LpHubMount.CloseAll<LpBankerHubRoot>();
}
