// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// KEYPAD-1 U3 (cc-lp-2): mounts and closes the keypad panel through the hub kit's mount helper (LpHubMount, which tries
// the DXRP HUD root first and falls back to a ScreenPanel). One open at a time: Mount closes any open instance first.

using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.Keypad;

public static class LpKeypadUi
{
	private const string PanelObjectName = "LpKeypadPanel";

	/// <summary>Opens the panel for one keypad. <paramref name="canManage"/> shows the owner row; the host still gates every act.</summary>
	public static LpKeypadPanel Open( LpKeypadEntity keypad, bool canManage )
	{
		var panel = LpHubMount.Mount<LpKeypadPanel>( PanelObjectName, "lpkeypad" );
		if ( !panel.IsValid() )
			return null;

		panel.Keypad = keypad;
		panel.CanManage = canManage;
		return panel;
	}

	public static LpKeypadPanel Current => Sandbox.Game.ActiveScene?.GetAllComponents<LpKeypadPanel>().FirstOrDefault();

	public static void Close() => LpHubMount.CloseAll<LpKeypadPanel>();
}
