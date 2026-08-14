// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Player Hub for DXRP" (s&box ident: lifepunch.playerhub · addon ident: playerhub)
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.PlayerHub;

/// <summary>
/// The Player Hub's mount host. Follows <c>StaffMenuHost</c> exactly — this is the house pattern for a
/// LIFEPUNCH screen menu, not a new mechanism.
/// </summary>
/// <remarks>
/// <para>
/// THE PANEL DOES NOT EXIST UNTIL IT IS OPENED, AND IS DESTROYED WHEN IT IS CLOSED. That is the whole
/// pattern, and getting it wrong is what shipped Slice 1 unreachable: the panel carried an in-component
/// <c>_open</c> visibility flag with NOTHING TO CREATE IT and nothing to set the flag back, so it rendered
/// for nobody and — had it rendered — could never have been reopened. Both defects were the same missing
/// piece: THIS FILE.
/// </para>
/// <para>
/// RE-OPENABILITY IS BY CONSTRUCTION, NOT BY A RESET. <see cref="Toggle"/> destroys the instance on close
/// and builds a FRESH one on the next open, so every open starts from a clean component with clean state.
/// There is no "reset the flag" path to forget, because there is no long-lived flag to reset.
/// </para>
/// </remarks>
public static class LpPlayerHubHost
{
	/// <summary>Positive code-string ID for the mount slice. Exists nowhere else in the tree.</summary>
	public const string MountMark = "LP_PLAYERHUB_MOUNT_20260714";

	private const string MenuObjectName = "LifePunchPlayerHub";

	private static LpPlayerHubRoot _instance;

	public static bool IsOpen => _instance.IsValid();

	// --- Entry points ------------------------------------------------------

	/// <summary>
	/// Console + chat entry point. A player binds any key to <c>playerhub</c> or <c>hub</c>
	/// (e.g. <c>bind h playerhub</c>). Chat: <c>/playerhub</c>, <c>/hub</c>.
	/// </summary>
	[ConCmd( "playerhub" )]
	public static void PlayerHubConCmd() => Toggle();

	[ConCmd( "hub" )]
	public static void HubConCmd() => Toggle();

	/// <summary>Open the hub if closed, else close it.</summary>
	public static void Toggle()
	{
		if ( IsOpen )
		{
			RequestClose();
			return;
		}

		_instance = Mount();
		if ( _instance.IsValid() )
		{
			SetCursorMode( true );
			return;
		}

		Log.Warning( "[playerhub] Toggle failed — hub did not mount (see prior mount warnings)." );
	}

	/// <summary>Close and tear down the open hub, if any. Safe to call when nothing is open.</summary>
	public static void RequestClose()
	{
		if ( _instance.IsValid() )
		{
			Close( _instance );
		}

		_instance = null;
		SetCursorMode( false );
	}

	// --- Mount / teardown --------------------------------------------------

	/// <summary>
	/// While the hub is open we release the local player's look controls so the cursor frees up and the
	/// panel becomes clickable. Same engine hook StaffMenu uses (<c>Player.LockCamera</c> drives
	/// <c>Controller.UseLookControls</c>) — we reuse the engine mechanism rather than touching the cursor
	/// directly. No-op in the editor build.
	/// </summary>
	private static void SetCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
		{
			Player.Local.LockCamera = menuOpen;
		}
#endif
	}

	private static LpPlayerHubRoot Mount()
	{
		// Close any prior instance first — guards against a stale component surviving a hotload/reload.
		var existing = Sandbox.Game.ActiveScene?.GetAllComponents<LpPlayerHubRoot>().FirstOrDefault();
		if ( existing.IsValid() )
		{
			Close( existing );
		}

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel();
#else
		// Prefer the DXRP HUD root (proven clickable path). Dedicated / early join sometimes has no HUD
		// root yet, so the ScreenPanel fallback is not optional — it is the path that works on join.
		var panel = GameManager.ShowUi<LpPlayerHubRoot>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( "[playerhub] GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return MountOnScreenPanel();
#endif
	}

	private static LpPlayerHubRoot MountOnScreenPanel()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "[playerhub] ActiveScene is null — cannot mount hub." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = MenuObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<LpPlayerHubRoot>();
	}

	private static void Close( LpPlayerHubRoot hub )
	{
		if ( !hub.IsValid() )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		// Local build hosts the panel on its own object, so destroy the whole object.
		if ( hub.GameObject.IsValid() )
		{
			hub.GameObject.Destroy();
		}
		else
		{
			hub.Destroy();
		}
#else
		// ShowUi shares the HUD root; the ScreenPanel fallback uses MenuObjectName. Destroying the shared
		// HUD root would take the whole HUD with it — so only destroy the object WE created.
		if ( hub.GameObject.IsValid() && hub.GameObject.Name == MenuObjectName )
			hub.GameObject.Destroy();
		else
			hub.Destroy();
#endif
	}
}
