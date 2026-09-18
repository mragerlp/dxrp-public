// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Server Hub for DXRP" (s&box ident: lifepunch.serverhub - addon ident: serverhub)
// Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave
// -----------------------------------------------------------------------------

using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.ServerHub;

/// <summary>
/// The Server Hub's mount host. This follows the house pattern for a LIFEPUNCH screen menu
/// (StaffMenuHost, and playerhub's LpPlayerHubHost after it). It is not a new mechanism.
/// </summary>
/// <remarks>
/// <para>
/// THE PANEL DOES NOT EXIST UNTIL IT IS OPENED, AND IS DESTROYED WHEN IT IS CLOSED. That is
/// the entire pattern. The failure it prevents is on record in this repo: a panel that carried
/// its own in-component "_open" visibility flag, with nothing to create it and nothing to set
/// the flag back, rendered for nobody and could never have been reopened even if it had.
/// Re-openability here is BY CONSTRUCTION - <see cref="Toggle"/> destroys the instance on
/// close and builds a fresh one on the next open, so there is no long-lived flag to forget
/// to reset, because there is no long-lived flag.
/// </para>
/// <para>
/// COMMAND NAMES. This registers <c>serverhub</c> and <c>lp_serverhub</c>. It deliberately
/// does NOT register <c>hub</c>, <c>playerhub</c>, or <c>menu</c>: all three are already
/// live registrations elsewhere in the tree, and a duplicate would be a silent collision
/// rather than a build error. The Server Hub and the Player Hub are different surfaces -
/// this one is server-scoped (jobs, info, banker), that one is player-scoped.
/// </para>
/// <para>
/// SELF-CONTAINMENT. This addon takes no dependency on any other LIFEPUNCH addon directory.
/// Its only outward references are the sensed DXRP seams, each behind
/// <c>#if !LIFEPUNCH_LOCAL</c>, so it also builds in a standalone LIFEPUNCH configuration.
/// </para>
/// </remarks>
public static class LpServerHubHost
{
	/// <summary>Positive code-string ID for the mount slice. Exists nowhere else in the tree.</summary>
	public const string MountMark = "LP_SERVERHUB_MOUNT_20260827";

	/// <summary>
	/// Names the GameObject this host creates. Close() destroys ONLY an object with this name,
	/// which is what keeps a shared HUD root from being torn down along with the menu.
	/// </summary>
	private const string MenuObjectName = "LifePunchServerHub";

	private static LpServerHubRoot _instance;

	public static bool IsOpen => _instance.IsValid();

	// --- Entry points ------------------------------------------------------

	/// <summary>
	/// Console + chat entry point. A player binds any key to <c>serverhub</c>
	/// (e.g. <c>bind f4 serverhub</c>). Chat: <c>/serverhub</c>.
	/// </summary>
	[ConCmd( "serverhub" )]
	public static void ServerHubConCmd() => Toggle();

	/// <summary>Namespaced alias, matching the lp_-prefixed console convention in this tree.</summary>
	[ConCmd( "lp_serverhub" )]
	public static void LpServerHubConCmd() => Toggle();

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

		Log.Warning( "[serverhub] Toggle failed - hub did not mount (see prior mount warnings)." );
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
	/// While the hub is open we release the local player's look controls so the cursor frees up
	/// and the panel becomes clickable.
	/// </summary>
	/// <remarks>
	/// This reuses the engine hook the native staff menu uses (<c>Player.LockCamera</c> drives
	/// <c>Controller.UseLookControls</c>) rather than touching the cursor directly. No-op in a
	/// standalone build, where there is no DXRP player to ask.
	/// </remarks>
	private static void SetCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
		{
			Player.Local.LockCamera = menuOpen;
		}
#endif
	}

	private static LpServerHubRoot Mount()
	{
		// Close any prior instance first - guards against a stale component surviving a hotload.
		var existing = Sandbox.Game.ActiveScene?.GetAllComponents<LpServerHubRoot>().FirstOrDefault();
		if ( existing.IsValid() )
		{
			Close( existing );
		}

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel();
#else
		// Prefer the DXRP HUD root: it is the proven-clickable path. Dedicated servers and early
		// joins sometimes have no HUD root yet, so the ScreenPanel fallback is NOT optional -
		// it is the path that works on join.
		var panel = GameManager.ShowUi<LpServerHubRoot>();
		if ( panel.IsValid() )
		{
			return panel;
		}

		Log.Warning( "[serverhub] GameManager.ShowUi returned null - falling back to ScreenPanel." );
		return MountOnScreenPanel();
#endif
	}

	private static LpServerHubRoot MountOnScreenPanel()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "[serverhub] ActiveScene is null - cannot mount hub." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = MenuObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<LpServerHubRoot>();
	}

	private static void Close( LpServerHubRoot hub )
	{
		if ( !hub.IsValid() )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		if ( hub.GameObject.IsValid() )
		{
			hub.GameObject.Destroy();
		}
		else
		{
			hub.Destroy();
		}
#else
		// ShowUi shares the HUD root; the ScreenPanel fallback uses MenuObjectName. Destroying
		// the shared HUD root would take the whole HUD with it - so destroy ONLY the object we
		// created ourselves, and otherwise remove just the component.
		if ( hub.GameObject.IsValid() && hub.GameObject.Name == MenuObjectName )
		{
			hub.GameObject.Destroy();
		}
		else
		{
			hub.Destroy();
		}
#endif
	}
}
