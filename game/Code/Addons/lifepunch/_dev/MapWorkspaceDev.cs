// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LifePunch editor dev lane" (Code/_dev — NOT shipped; lifepunchulx publishes six staff-menu files only) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// Slaps a low-demand map onto the live MapInstance. Play-only so
/// <c>game.scene</c> is never dirtied. Fitting is suppressed for the rest of
/// the play session so downtown prefabs are not cloned or lost.
/// </summary>
public static class MapWorkspaceDev
{
	public const string DowntownIdent = "dxura.rp_downtown_scuffed";
	public const string MoonIdent = "brothelteam.moon";
	public const string FlatgrassIdent = "facepunch.flatgrass";

	private static string? _sessionOrigin;
	private static bool _fittingSuppressed;

	[ConCmd( "lp_map" )]
	public static void Slap( string alias = "" )
	{
		if ( string.IsNullOrWhiteSpace( alias ) )
		{
			LogHelp();
			return;
		}

		if ( !TryResolveIdent( alias, out var ident, out var blank ) )
		{
			return;
		}

		Apply( ident, blank, alias.Trim() );
	}

	[ConCmd( "lp_map_moon" )]
	public static void SlapMoon() => Apply( MoonIdent, blank: false, "moon" );

	[ConCmd( "lp_map_flatgrass" )]
	public static void SlapFlatgrass() => Apply( FlatgrassIdent, blank: false, "flatgrass" );

	[ConCmd( "lp_map_blank" )]
	public static void SlapBlank() => Apply( "", blank: true, "blank" );

	[ConCmd( "lp_map_default" )]
	public static void SlapDefault()
	{
		var ident = string.IsNullOrWhiteSpace( _sessionOrigin ) ? DowntownIdent : _sessionOrigin;
		Apply( ident, blank: false, "default" );
	}

	private static void Apply( string ident, bool blank, string label )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"lp_map: editor-only ({label})." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( $"lp_map: must be host ({label})." );
			return;
		}

		if ( !Player.Local.IsValid() )
		{
			Log.Warning( "lp_map: play-only. Refusing so game.scene is not dirtied. Host Play, then slap." );
			return;
		}

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			Log.Warning( "lp_map: no active scene." );
			return;
		}

		var map = scene.Components.GetAll<MapInstance>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault();
		if ( !map.IsValid() )
		{
			Log.Warning( "lp_map: no MapInstance. Open game.scene, not a prefab tab." );
			return;
		}

		LatchOrigin( map );
		SuppressFitting();

		if ( blank )
		{
			map.MapName = "";
			map.UnloadMap();
			Log.Info( $"lp_map: blank. Fittings left in place. Origin={_sessionOrigin}. Scene not written." );
			return;
		}

		map.MapName = ident;
		Log.Info( $"lp_map: {label} -> '{ident}'. Fittings left in place. Origin={_sessionOrigin}. Scene not written." );
	}

	private static bool TryResolveIdent( string alias, out string ident, out bool blank )
	{
		ident = "";
		blank = false;
		var key = alias.Trim().ToLowerInvariant();
		switch ( key )
		{
			case "moon":
			case "brothelmoon":
			case "brothelteam.moon":
				ident = MoonIdent;
				return true;
			case "flatgrass":
			case "grass":
			case "facepunch.flatgrass":
				ident = FlatgrassIdent;
				return true;
			case "blank":
			case "void":
			case "none":
				blank = true;
				return true;
			case "default":
			case "downtown":
			case "dxrp":
				ident = string.IsNullOrWhiteSpace( _sessionOrigin ) ? DowntownIdent : _sessionOrigin;
				return true;
			default:
				if ( key.Contains( '.', StringComparison.Ordinal ) )
				{
					ident = alias.Trim();
					return true;
				}

				Log.Warning( $"lp_map: unknown '{alias}'." );
				LogHelp();
				return false;
		}
	}

	private static void LatchOrigin( MapInstance map )
	{
		if ( !string.IsNullOrWhiteSpace( _sessionOrigin ) )
		{
			return;
		}

		_sessionOrigin = string.IsNullOrWhiteSpace( map.MapName ) ? DowntownIdent : map.MapName;
	}

	private static void SuppressFitting()
	{
		if ( _fittingSuppressed )
		{
			return;
		}

		if ( Config.Current is null || Config.Current.Game is null )
		{
			return;
		}

		Config.Current.Game.MapFittingEnabled = false;
		_fittingSuppressed = true;
		Log.Info( "lp_map: MapFittingEnabled off for this play session. Stop Play restores the default." );
	}

	private static void LogHelp()
	{
		var current = "?";
		var scene = Game.ActiveScene;
		if ( scene.IsValid() )
		{
			var map = scene.Components.GetAll<MapInstance>( FindMode.EverythingInSelfAndDescendants ).FirstOrDefault();
			if ( map.IsValid() )
			{
				current = string.IsNullOrWhiteSpace( map.MapName ) ? "(blank)" : map.MapName;
			}
		}

		Log.Info( $"lp_map: current={current} origin={_sessionOrigin ?? "(unlatched)"} fittingSuppressed={_fittingSuppressed}" );
		Log.Info( "lp_map moon | flatgrass | blank | default" );
		Log.Info( "or lp_map_moon / lp_map_flatgrass / lp_map_blank / lp_map_default" );
		Log.Info( "or lp_map <package.ident> — play-only, never writes game.scene" );
	}
}
