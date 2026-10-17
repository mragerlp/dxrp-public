// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANKER-HUB-1 skeleton (DRIVE-AWAY-1-D): mounts the Banker hub on its own ScreenPanel (the LpHashdUiHost
// fallback path, UH:54-63). One open at a time.

using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public static class LpBankerHubUi
{
	private const string PanelObjectName = "LpBankerHubRoot";

	public static LpBankerHubRoot Open( int tabIndex )
	{
		CloseOpen();
		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		var go = scene.CreateObject();
		go.Name = PanelObjectName;
		go.AddComponent<ScreenPanel>();
		var root = go.AddComponent<LpBankerHubRoot>();
		root.InitialTabIndex = tabIndex;
		return root;
	}

	public static LpBankerHubRoot Current => Game.ActiveScene?.GetAllComponents<LpBankerHubRoot>().FirstOrDefault();

	public static void CloseOpen()
	{
		var open = Game.ActiveScene?.GetAllComponents<LpBankerHubRoot>().ToList();
		if ( open is null )
			return;

		foreach ( var root in open )
		{
			if ( root.IsValid() )
				root.GameObject.Destroy();
		}
	}
}
