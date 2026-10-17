// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* - addon ident: lifepunch)
// Shell kit host (lifepunch.sample): dev-only. UNVERIFIED in the editor until the driving seat proves it.
// -----------------------------------------------------------------------------

using System;
using System.Linq;
using Sandbox;
using Sandbox.UI;

#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.ShellKit;

/// <summary>
/// Mount host for the shell kit. A port of <c>playerhub/code/ui/LpPlayerHubHost.cs</c>, the house
/// pattern for a LIFEPUNCH screen menu: the panel exists only while open, is destroyed on close,
/// and every open builds a fresh instance.
/// </summary>
public static class ShellKitHost
{
	private const string MenuObjectName = "LifePunchShellKit";

	private static ShellKitSample _instance;

	public static bool IsOpen => _instance.IsValid();

	/// <summary>Console entry point: <c>lp_shellkit</c> opens the kit if closed, else closes it.</summary>
	[ConCmd( "lp_shellkit" )]
	public static void ShellKitConCmd() => Toggle();

	/// <summary>
	/// Deep link: open the kit (if closed) straight onto a tab. <c>lp_shellkit_tab states</c>.
	/// Names are the <see cref="ShellKitSample.ShellKitTab"/> members, case-insensitive. Unknown name = default tab.
	/// </summary>
	[ConCmd( "lp_shellkit_tab" )]
	public static void ShellKitTabConCmd( string tab = "" )
	{
		if ( !IsOpen )
		{
			_instance = Mount();
			if ( !_instance.IsValid() )
			{
				Log.Warning( "[shellkit] lp_shellkit_tab: kit did not mount (see prior mount warnings)." );
				return;
			}

			SetCursorMode( true );
		}

		if ( Enum.TryParse<ShellKitSample.ShellKitTab>( tab, true, out var parsed ) )
			_instance.SelectTab( parsed );
		else if ( !string.IsNullOrWhiteSpace( tab ) )
			Log.Warning( $"[shellkit] lp_shellkit_tab: unknown tab '{tab}' (overview, readouts, states, help)." );
	}

	/// <summary>Open the kit if closed, else close it.</summary>
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

		Log.Warning( "[shellkit] Toggle failed - kit did not mount (see prior mount warnings)." );
	}

	/// <summary>Close and tear down the open kit, if any. Safe to call when nothing is open.</summary>
	public static void RequestClose()
	{
		if ( _instance.IsValid() )
		{
			Close( _instance );
		}

		_instance = null;
		SetCursorMode( false );
	}

	private static void SetCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
		{
			Player.Local.LockCamera = menuOpen;
		}
#endif
	}

	private static ShellKitSample Mount()
	{
		// Close any prior instance first; guards against a stale component surviving a hotload.
		var existing = Sandbox.Game.ActiveScene?.GetAllComponents<ShellKitSample>().FirstOrDefault();
		if ( existing.IsValid() )
		{
			Close( existing );
		}

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel();
#else
		var panel = GameManager.ShowUi<ShellKitSample>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( "[shellkit] GameManager.ShowUi returned null - falling back to ScreenPanel." );
		return MountOnScreenPanel();
#endif
	}

	private static ShellKitSample MountOnScreenPanel()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "[shellkit] ActiveScene is null - cannot mount kit." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = MenuObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<ShellKitSample>();
	}

	private static void Close( ShellKitSample kit )
	{
		if ( !kit.IsValid() )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		if ( kit.GameObject.IsValid() )
		{
			kit.GameObject.Destroy();
		}
		else
		{
			kit.Destroy();
		}
#else
		// ShowUi shares the HUD root; only destroy the object this host created.
		if ( kit.GameObject.IsValid() && kit.GameObject.Name == MenuObjectName )
			kit.GameObject.Destroy();
		else
			kit.Destroy();
#endif
	}
}
