// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
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

namespace LifePunch.DXRP.Addons;

/// <summary>
/// SPINE-1 kit piece C2: mounts and closes a hub panel the house way. Shaped from the Player Hub host
/// (LpPlayerHubHost.cs:132-195) and the Bitcoin hub host (LpBtcHubUiHost.cs:43-114): try the DXRP HUD root
/// first (GameManager.ShowUi), fall back to a ScreenPanel object when there is no HUD root.
/// Pure statics: nothing registered, no state, and no existing host is switched to call it in this slice.
/// </summary>
public static class LpHubMount
{
	/// <summary>
	/// Tag put on every object this helper creates. <see cref="Close"/> destroys a panel's whole object only
	/// when it carries this tag; a panel on the shared HUD root is removed alone. The tag survives a hotload,
	/// which a static list of created objects would not.
	/// </summary>
	public const string OwnedTag = "lp_hub_mount";

	/// <summary>
	/// Mounts one panel of type <typeparamref name="T"/>. Any open instance is closed first (one open at a time,
	/// and a stale component cannot survive a hotload). The panel does not exist until this is called.
	/// </summary>
	/// <param name="objectName">Name of the fallback ScreenPanel object.</param>
	/// <param name="logSource">Log prefix for the warnings this raises.</param>
	public static T Mount<T>( string objectName, string logSource = "lphub" ) where T : PanelComponent, new()
	{
		CloseAll<T>();

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel<T>( objectName, logSource );
#else
		// Prefer the DXRP HUD root (the proven clickable path). Dedicated or early join can have no HUD root yet,
		// so the ScreenPanel fallback is not optional.
		var panel = GameManager.ShowUi<T>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( $"[{logSource}] GameManager.ShowUi returned null - falling back to ScreenPanel." );
		return MountOnScreenPanel<T>( objectName, logSource );
#endif
	}

	private static T MountOnScreenPanel<T>( string objectName, string logSource ) where T : PanelComponent, new()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( $"[{logSource}] ActiveScene is null - cannot mount." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = objectName;
		go.Tags.Add( OwnedTag );
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<T>();
	}

	/// <summary>
	/// Removes a mounted panel. An object this helper created goes whole; a panel on the shared HUD root is
	/// removed alone, so the HUD is never destroyed with it. Safe on an invalid panel.
	/// </summary>
	public static void Close( Component panel )
	{
		if ( !panel.IsValid() )
			return;

		var go = panel.GameObject;
		if ( go.IsValid() && go.Tags.Has( OwnedTag ) && !IsSharedHud( go ) )
			go.Destroy();
		else
			panel.Destroy();
	}

	/// <summary>Closes every open instance of <typeparamref name="T"/> in the active scene.</summary>
	public static void CloseAll<T>() where T : PanelComponent
	{
		var open = Sandbox.Game.ActiveScene?.GetAllComponents<T>().ToList();
		if ( open is null )
			return;

		foreach ( var panel in open )
			Close( panel );
	}

	private static bool IsSharedHud( GameObject go )
	{
#if LIFEPUNCH_LOCAL
		return false;
#else
		return GameManager.Instance.IsValid() && GameManager.Instance.HudGameObject == go;
#endif
	}

	/// <summary>
	/// Cursor mode for a hub the caller opens and closes itself: release the local player's look controls so the
	/// cursor frees and the panel is clickable (the engine hook StaffMenu and the Player Hub use,
	/// Player.LockCamera). No-op in the local lane. Do not combine with <see cref="OnPanelEnabled"/>, whose
	/// menu-open call already locks and releases the camera.
	/// </summary>
	public static void SetCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
			Player.Local.LockCamera = menuOpen;
#endif
	}

	/// <summary>
	/// Panel lifecycle, for OnEnabled: the menu-open notice (input block, combat lock) and, optionally, the Esc
	/// claim. Same two calls and order as the Banker hub root (LpBankerHubRoot.razor OnEnabled).
	/// </summary>
	public static void OnPanelEnabled( bool claimEscape = true )
	{
		LifePunchMenuInputBlock.NotifyMenuOpened();
		if ( claimEscape )
			LifePunchMenuInputBlock.ClaimEscape();
	}

	/// <summary>Panel lifecycle, for OnDisabled: the reverse of <see cref="OnPanelEnabled"/>, in reverse order.</summary>
	public static void OnPanelDisabled( bool claimEscape = true )
	{
		if ( claimEscape )
			LifePunchMenuInputBlock.ReleaseEscape();

		LifePunchMenuInputBlock.NotifyMenuClosed();
	}

	/// <summary>
	/// Panel lifecycle, for OnUpdate: applies the wheel-scroll-only policy to the panel tree. The Esc close stays
	/// the panel's own (LifePunchMenuInputBlock, as the Banker hub root does in OnUpdate).
	/// </summary>
	public static void OnPanelUpdate( Panel root ) => LifePunchUiScrollPolicy.Apply( root );
}
