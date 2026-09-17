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

#if !LIFEPUNCH_LOCAL

using Dxura.RP.Game;
using LifePunch.DXRP.Addons.StaffMenu;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// s&amp;box Intellibot session seats inside THIS DXRP editor play:
/// GROK (SuperAdmin, agent-driven) and Fred (idle regular dummy).
/// Does not possess <see cref="Player.Local"/> and does not charge Bloodwave.
/// Learning notes live in the sibling <c>sbox-intellibot/</c> repo — not a second s&amp;box project.
/// </summary>
public static class IntellibotSession
{
	public const string GrokName = "GROK";
	public const string FredName = "Fred";
	public const string GrokRank = "Super Admin";

	/// <summary>Stable fake SteamId for the GROK SuperAdmin seat. Not a real account.</summary>
	public const long GrokSteamId = 76500000000000901L;

	/// <summary>Stable fake SteamId for the Fred regular dummy. Not a real account.</summary>
	public const long FredSteamId = 76500000000000902L;

	[ConCmd( "lp_spawn_intellibot" )]
	public static void SpawnBoth()
	{
		SpawnGrok();
		SpawnFred();
	}

	[ConCmd( "lp_spawn_grok" )]
	public static void SpawnGrok()
	{
		if ( !GuardHost( "lp_spawn_grok" ) )
		{
			return;
		}

		var grok = StaffMenuTestBots.SpawnOrReplaceNamedBot( GrokName, GrokSteamId, SeatOffset( 0 ) );
		if ( !grok.IsValid() )
		{
			Log.Warning( "lp_spawn_grok: spawn failed." );
			return;
		}

		var ranked = StaffMenuTestBots.TryAssignNamedBotRank( GrokName, GrokSteamId, GrokRank );
		Log.Info( $"lp_spawn_grok: '{grok.GameObject.Name}' id={grok.GameObject.Id} steam={GrokSteamId} superadmin={ranked} — drive_player this GUID, not Local." );
		if ( !ranked )
		{
			Log.Warning( "lp_spawn_grok: Super Admin not assigned. Run lp_authorize, then lp_rank_grok." );
		}
	}

	[ConCmd( "lp_spawn_fred" )]
	public static void SpawnFred()
	{
		if ( !GuardHost( "lp_spawn_fred" ) )
		{
			return;
		}

		var fred = StaffMenuTestBots.SpawnOrReplaceNamedBot( FredName, FredSteamId, SeatOffset( 1 ) );
		if ( !fred.IsValid() )
		{
			Log.Warning( "lp_spawn_fred: spawn failed." );
			return;
		}

		Log.Info( $"lp_spawn_fred: '{fred.GameObject.Name}' id={fred.GameObject.Id} steam={FredSteamId} (idle regular)." );
	}

	[ConCmd( "lp_rank_grok" )]
	public static void RankGrok()
	{
		if ( !GuardHost( "lp_rank_grok" ) )
		{
			return;
		}

		var grok = TryFindGrok();
		if ( !grok.IsValid() )
		{
			Log.Warning( "lp_rank_grok: exact spawned GROK identity not proven. Run lp_spawn_grok first." );
			return;
		}

		var ranked = StaffMenuTestBots.TryAssignNamedBotRank( GrokName, grok.SteamId, GrokRank );
		if ( !ranked )
		{
			Log.Warning( "lp_rank_grok: Super Admin not assigned. Run lp_authorize first." );
		}
	}

	[ConCmd( "lp_give_ak_grok" )]
	public static void GiveAkGrok()
	{
		GiveWeaponToGrok( "lp_give_ak_grok", Ak47DevGive.GiveAkTo );
	}

	[ConCmd( "lp_give_aks74u_grok" )]
	public static void GiveAks74uGrok()
	{
		GiveWeaponToGrok( "lp_give_aks74u_grok", Aks74uDevGive.GiveAks74uTo );
	}

	[ConCmd( "lp_give_aks74u_original_grok" )]
	public static void GiveAks74uOriginalGrok()
	{
		GiveWeaponToGrok( "lp_give_aks74u_original_grok", Aks74uOriginalDevGive.GiveAks74uOriginalTo );
	}

	[ConCmd( "lp_give_ar15_grok" )]
	public static void GiveAr15Grok()
	{
		GiveWeaponToGrok( "lp_give_ar15_grok", Ar15DevGive.GiveAr15To );
	}

	[ConCmd( "lp_give_deagle_grok" )]
	public static void GiveDesertEagleGrok()
	{
		GiveWeaponToGrok( "lp_give_deagle_grok", DesertEagleDevGive.GiveDesertEagleTo );
	}

	[ConCmd( "lp_give_m1911_grok" )]
	public static void GiveM1911Grok()
	{
		GiveWeaponToGrok( "lp_give_m1911_grok", M1911DevGive.GiveM1911To );
	}

	[ConCmd( "lp_give_m870_grok" )]
	public static void GiveM870Grok()
	{
		GiveWeaponToGrok( "lp_give_m870_grok", M870DevGive.GiveM870To );
	}

	[ConCmd( "lp_give_sr25_grok" )]
	public static void GiveSr25Grok()
	{
		GiveWeaponToGrok( "lp_give_sr25_grok", Sr25DevGive.GiveSr25To );
	}

	[ConCmd( "lp_grok_here" )]
	public static void MoveGrokHere()
	{
		MoveSeatHere( "lp_grok_here", GrokSteamId, GrokName, 0 );
	}

	[ConCmd( "lp_fred_here" )]
	public static void MoveFredHere()
	{
		MoveSeatHere( "lp_fred_here", FredSteamId, FredName, 1 );
	}

	[ConCmd( "lp_clear_grok" )]
	public static void ClearGrok()
	{
		ClearSeat( "lp_clear_grok", GrokSteamId, GrokName );
	}

	[ConCmd( "lp_clear_fred" )]
	public static void ClearFred()
	{
		ClearSeat( "lp_clear_fred", FredSteamId, FredName );
	}

	[ConCmd( "lp_clear_intellibot" )]
	public static void ClearBoth()
	{
		ClearGrok();
		ClearFred();
	}

	public static Player TryFindGrok()
	{
		return StaffMenuTestBots.TryGetSpawnedNamedBot( GrokSteamId, GrokName, out var grok ) ? grok : null;
	}

	public static Player TryFindFred()
	{
		return StaffMenuTestBots.TryGetSpawnedNamedBot( FredSteamId, FredName, out var fred ) ? fred : null;
	}

	private static void GiveWeaponToGrok( string cmd, System.Func<Player, string, bool> give )
	{
		if ( !GuardHost( cmd ) )
		{
			return;
		}

		if ( !Config.Current.IsReady )
		{
			Log.Warning( $"{cmd}: game-mode config is not ready." );
			return;
		}

		if ( !TryRequireSpawnedGrok( cmd, out var grok ) )
		{
			return;
		}

		give( grok, cmd );
	}

	private static bool TryRequireSpawnedGrok( string cmd, out Player grok )
	{
		grok = TryFindGrok();
		if ( !grok.IsValid() )
		{
			Log.Warning( $"{cmd}: exact spawned GROK identity not proven. Run lp_spawn_grok first." );
			return false;
		}

		var local = Player.Local;
		if ( local.IsValid() && local.GameObject.Id == grok.GameObject.Id )
		{
			Log.Error( $"{cmd}: GROK resolved to Player.Local; refusing." );
			grok = null;
			return false;
		}

		if ( !grok.WeaponGameObject.IsValid() )
		{
			Log.Warning( $"{cmd}: GROK weapon holder is unavailable." );
			grok = null;
			return false;
		}

		return true;
	}

	private static void MoveSeatHere( string cmd, long steamId, string name, int seat )
	{
		if ( !GuardHost( cmd ) )
		{
			return;
		}

		var hasPawn = StaffMenuTestBots.TryGetSpawnedNamedBot( steamId, name, out var pawn );
		var local = Player.Local;
		if ( !hasPawn || !pawn.IsValid() || !local.IsValid() )
		{
			Log.Warning( $"{cmd}: exact spawned {name} identity and Player.Local are required." );
			return;
		}

		var pos = SeatOffset( seat );
		var facing = Rotation.LookAt( (local.WorldPosition - pos).WithZ( 0f ).Normal, Vector3.Up );
		pawn.TeleportHost( new Transform( pos, facing ) );
		Log.Info( $"{cmd}: {name} at {pos}" );
	}

	private static void ClearSeat( string cmd, long steamId, string name )
	{
		if ( !GuardHost( cmd ) )
		{
			return;
		}

		StaffMenuTestBots.RemoveSpawnedNamedBot( steamId, name, cmd );
	}

	private static Vector3 SeatOffset( int seat )
	{
		var local = Player.Local;
		if ( !local.IsValid() )
		{
			return Vector3.Zero;
		}

		var rot = local.WorldRotation;
		return local.WorldPosition + rot.Forward * 80f + rot.Right * (seat * 56f) + Vector3.Up * 10f;
	}

	private static bool GuardHost( string cmd )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"{cmd}: editor-only." );
			return false;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( $"{cmd}: must be host (editor play)." );
			return false;
		}

		return true;
	}
}

#endif
