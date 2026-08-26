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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>; never in lifepunchulx publish tree.
///
/// Spawns host-owned dummy "players" so staff-menu features that need a SECOND player can be tested
/// solo in editor play: the profile-pane Goto/Bring/Return box, target selection, CanTarget gating,
/// and the Waypoints "Return a player" list. The dummy is cloned from
/// <see cref="GameNetworkManager.PlayerPrefab"/>, given a fake SteamId, host-spawned (no real
/// connection — it simply stands there), and registered in <see cref="GameNetworkManager.Players"/>
/// so the menu roster shows it as a non-staff (targetable) player.
///
/// Usage (in-game console during play):
///   lifepunch_spawn_testbot          → spawns "Test Dummy N" near the local player (fake id, no rank)
///   lifepunch_spawn_testbot Greg     → spawns a bot named "Greg"
///   lifepunch_spawn_rankbots         → spawns one bot per rank (Regular/VIP/EVIP/Mod/Admin/Super Admin)
///                                      plus "Greg" as an Owner-mirror, each with a real public avatar.
///                                      Pass `lifepunch_spawn_rankbots false` to skip Greg (targetable only).
///   lifepunch_spawn_all_testbots     → alias for lifepunch_spawn_rankbots (full roster incl. Greg)
///   lifepunch_spawn_scroll_testbots  → rank roster + 18 regular fillers (sidebar scroll proof)
///   lifepunch_spawn_staff_tier_bots   → Mod + Admin + Super Admin only (staff sidebar preview)
///   ulx_staff_bots                    → alias for lifepunch_spawn_staff_tier_bots
///   lifepunch_auto_spawn_testbots 1  → on editor host play, auto-spawn after portal API init (default 1)
///   lifepunch_auto_spawn_testbots_fill 18 → extra regular bots when auto-spawn runs (default 18)
///   lifepunch_list_ranks             → logs which rank names resolve (confirms the live portal strings)
///   lifepunch_botsay "Greg hi there" → makes a spawned bot talk in chat (first token = bot, rest = msg)
///   lifepunch_clear_testbots         → removes all spawned bots and clears their rank assignments
/// </summary>
public static class StaffMenuTestBots
{
	// Obviously-fake SteamId base (not a real account), incremented per spawn so each is unique.
	private const long FakeSteamIdBase = 76500000000000000L;

	private static int _spawnCount;
	private static readonly List<long> _spawned = new();

	// RankSystem.Ranks is host-private — portal init fills this cache (lp_authorize / DxrpPortalDevAuth).
	private static readonly Dictionary<string, Guid> RankIdByName = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Filled after <c>lp_authorize</c> so rank bots can resolve portal rank names.</summary>
	public static void CacheRankDefinitions( IEnumerable<RankDto> definitions )
	{
		RankIdByName.Clear();
		foreach ( var def in definitions )
		{
			if ( def == null || def.Id == Guid.Empty )
			{
				continue;
			}

			RegisterRankName( def.Name, def.Id );
		}
	}

	/// <summary>
	/// Pulls the live portal rank table into <see cref="RankSystem"/> and the name cache.
	/// Editor play must use this — <see cref="Game.ActiveScene.IsEditor"/> is false while playing.
	/// </summary>
	public static async Task<int> SyncPortalRankTableFromApiAsync()
	{
		if ( !ServerApiLink.HasAuthorizationKey || !Networking.IsHost )
		{
			return 0;
		}

		InitalizeServerResponseDto? initResponse;
		try
		{
			initResponse = await ServerApiClient.InitializeServer( new InitalizeServerDto
			{
				Version = Application.Version,
				DefaultConfig = Json.Serialize( Config.Current.Game )
			} );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"portal ranks: InitializeServer failed — {ex.Message}" );
			return 0;
		}

		await GameTask.MainThread();

		if ( initResponse?.Ranks == null )
		{
			Log.Warning( "portal ranks: InitializeServer returned no rank table." );
			return 0;
		}

		EnsureEditorRankSystem();

		var ranks = RankSystem.Instance;
		if ( ranks.IsValid() )
		{
			ranks.SetRanks( initResponse.Ranks );
			if ( initResponse.RankAssignments != null )
			{
				ranks.SetRankAssignments( initResponse.RankAssignments );
			}
		}
		else
		{
			Log.Warning( "portal ranks: RankSystem unavailable — cached names only (scoreboard ROLE may stay empty)." );
		}

		CacheRankDefinitions( initResponse.Ranks );
		var count = RankIdByName.Count;
		Log.Info( $"portal ranks: cached {initResponse.Ranks.Count()} definition(s) ({count} lookup key(s))." );

		if ( Application.IsEditor && !Config.Current.IsReady )
		{
			Config.Current.MarkReady();
		}

		ReapplySpawnedRankAssignments();
		return count;
	}

	private static void EnsureEditorRankSystem()
	{
		if ( RankSystem.Instance.IsValid() )
		{
			return;
		}

		var anchor = GameNetworkManager.Instance;
		if ( anchor.IsValid() )
		{
			anchor.GameObject.Components.Create<RankSystem>();
			Log.Info( "portal ranks: created RankSystem on GameNetworkManager for editor dev play." );
			return;
		}

		var go = Game.ActiveScene?.CreateObject();
		if ( go != null )
		{
			go.Name = "RankSystem (editor dev)";
			go.Components.Create<RankSystem>();
			Log.Info( "portal ranks: created standalone RankSystem for editor dev play." );
		}
	}

	public static Guid? FindRankIdByName( string rankName )
	{
		if ( string.IsNullOrWhiteSpace( rankName ) )
		{
			return null;
		}

		if ( RankIdByName.TryGetValue( rankName.Trim(), out var id ) )
		{
			return id;
		}

		var sanitized = SanitizeRankName( rankName );
		return sanitized.Length > 0 && RankIdByName.TryGetValue( sanitized, out id ) ? id : null;
	}

	/// <summary>Portal rank labels vary — try canonical bot names plus common portal aliases.</summary>
	public static Guid? ResolveRankId( string canonicalRank )
	{
		foreach ( var candidate in GetRankNameCandidates( canonicalRank ) )
		{
			var id = FindRankIdByName( candidate );
			if ( id.HasValue )
			{
				return id;
			}
		}

		return null;
	}

	private static IEnumerable<string> GetRankNameCandidates( string canonicalRank )
	{
		yield return canonicalRank;

		switch ( canonicalRank.Trim().ToLowerInvariant() )
		{
			case "mod":
				yield return "Moderator";
				yield return "Trial Mod";
				break;
			case "admin":
				yield return "Administrator";
				break;
			case "super admin":
				yield return "SuperAdmin";
				yield return "Community Manager";
				break;
			case "vip":
				yield return "Donator";
				yield return "Donor";
				break;
			case "evip":
				yield return "Extreme VIP";
				yield return "Elite VIP";
				break;
		}
	}

	[ConCmd( "lifepunch_sync_portal_ranks" )]
	public static void SyncPortalRanksCmd()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lifepunch_sync_portal_ranks: editor host-only." );
			return;
		}

		_ = SyncPortalRankTableFromApiAsync();
	}

	/// <summary>After <c>lp_authorize</c> reloads the portal rank table, re-apply ranks to spawned bots.</summary>
	[ConCmd( "lifepunch_reapply_testbot_ranks" )]
	public static void ReapplySpawnedRankAssignments()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			return;
		}

		if ( _spawned.Count == 0 )
		{
			Log.Info( "lifepunch_reapply_testbot_ranks: no spawned test bots to update." );
			return;
		}

		var applied = 0;
		foreach ( var def in RankBots )
		{
			if ( string.IsNullOrEmpty( def.Rank ) || !_spawned.Contains( def.SteamId ) )
			{
				continue;
			}

			if ( TryAssignBotRank( def.Name, def.SteamId, def.Rank ) )
			{
				applied++;
			}
		}

		Log.Info( $"lifepunch_reapply_testbot_ranks: applied portal ranks to {applied} bot(s)." );
	}

	private static bool TryAssignBotRank( string botName, long steamId, string canonicalRank )
	{
		var ranks = RankSystem.Instance;
		if ( !ranks.IsValid() )
		{
			Log.Warning( $"testbot ranks: RankSystem unavailable for '{botName}'." );
			return false;
		}

		var rankId = ResolveRankId( canonicalRank );
		if ( !rankId.HasValue )
		{
			Log.Warning( $"testbot ranks: no portal rank matched '{canonicalRank}' for '{botName}' (run lp_authorize first; cached: {string.Join( ", ", RankIdByName.Keys.Take( 12 ) )})." );
			return false;
		}

		ranks.SetPlayerRanks( steamId, new List<Guid> { rankId.Value } );
		var resolved = ranks.GetRankName( steamId );
		Log.Info( $"testbot ranks: '{botName}' -> '{resolved}' ({canonicalRank})." );
		return !string.IsNullOrWhiteSpace( resolved );
	}

	private static void RegisterRankName( string raw, Guid id )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return;
		}

		var trimmed = raw.Trim();
		RankIdByName[trimmed] = id;
		RankIdByName[trimmed.ToLowerInvariant()] = id;

		var sanitized = SanitizeRankName( raw );
		if ( sanitized.Length > 0 )
		{
			RankIdByName[sanitized] = id;
			RankIdByName[sanitized.ToLowerInvariant()] = id;
		}
	}

	private static string SanitizeRankName( string raw )
	{
		if ( string.IsNullOrEmpty( raw ) )
		{
			return "";
		}

		var sb = new System.Text.StringBuilder( raw.Length );
		foreach ( var c in raw )
		{
			if ( char.IsControl( c ) || char.IsSurrogate( c ) )
			{
				continue;
			}

			var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory( c );
			if ( category is System.Globalization.UnicodeCategory.Format
			    or System.Globalization.UnicodeCategory.PrivateUse
			    or System.Globalization.UnicodeCategory.OtherNotAssigned )
			{
				continue;
			}

			sb.Append( c );
		}

		return sb.ToString().Trim();
	}

	[ConCmd( "lifepunch_spawn_testbot" )]
	public static void SpawnTestBot( string name = "" )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lifepunch_spawn_testbot: editor-only dev command." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lifepunch_spawn_testbot: must be host (editor play)." );
			return;
		}

		var fakeId = FakeSteamIdBase + ++_spawnCount;
		var botName = string.IsNullOrWhiteSpace( name ) ? $"Test Dummy {_spawnCount}" : name;

		var player = SpawnBot( botName, fakeId, SpawnNearLocal() );
		if ( player.IsValid() )
		{
			Log.Info( $"lifepunch_spawn_testbot: spawned '{botName}' (fake SteamId {fakeId})." );
		}
	}

	// Shared spawn path for every bot variant. Mirrors DXRP's own DebugPlayerSpawner: clone the player
	// prefab, give it the supplied identity, mark it a debug player, kill its controller/physics so it
	// just stands there, network-spawn it, then DROP OWNERSHIP. Dropping ownership is the critical bit —
	// a host-owned pawn would share the host's ConnectionId and clobber the host's entry in
	// GameNetworkManager.PlayersByConnectionIdCache, which made the host resolve OUR command caller as
	// the bot (the "Missing permission for /goto" error). Unowned ⇒ ConnectionId = Guid.Empty ⇒ no
	// collision with the real host connection. Returns the spawned player, or null on failure.
	private static Player SpawnBot( string name, long steamId, Vector3 position )
	{
		var manager = GameNetworkManager.Instance;
		if ( !manager.IsValid() || !manager.PlayerPrefab.IsValid() )
		{
			Log.Error( "testbot: GameNetworkManager / PlayerPrefab unavailable." );
			return null;
		}

		if ( manager.Players.ContainsKey( steamId ) )
		{
			Log.Warning( $"testbot: a player with SteamId {steamId} already exists; skipping '{name}'." );
			return null;
		}

		var go = manager.PlayerPrefab.Clone();
		if ( !go.IsValid() )
		{
			Log.Error( "testbot: failed to clone PlayerPrefab." );
			return null;
		}

		go.Name = $"TestBot ({name})";

		var player = go.GetComponent<Player>();
		if ( !player.IsValid() )
		{
			Log.Error( "testbot: cloned PlayerPrefab has no Player component." );
			go.Destroy();
			return null;
		}

		player.SteamId = steamId;
		player.SteamName = name;
		player.Job = GameModeJobs.Default;
		player.IsDebugPlayer = true;

		if ( player.Controller.IsValid() )
		{
			player.Controller.Enabled = false;
		}

		var rb = player.GetComponent<Rigidbody>();
		if ( rb.IsValid() )
		{
			rb.MotionEnabled = false;
		}

		go.NetworkSpawn( NetworkSpawnOptions.Default );
		go.Network.DropOwnership();

		manager.Players[steamId] = player;

		// Sane stats so the profile pane renders (bank balance, playtime, level, rp name).
		player.InitalizeHost( 50000, 6000, 5, name );

		// Proper body init + position (SpawnHost sets up the visible pawn).
		player.SpawnHost();
		player.TeleportHost( new Transform( position, Rotation.Identity ) );

		_spawned.Add( steamId );
		return player;
	}

	/// <summary>Drop a prior-session bot occupying a fixed SteamId so rank preview bots can respawn.</summary>
	private static void EnsureBotSlot( long steamId )
	{
		var manager = GameNetworkManager.Instance;
		if ( !manager.IsValid() )
		{
			return;
		}

		if ( manager.Players.TryGetValue( steamId, out var player ) && player.IsValid() )
		{
			player.GameObject.Destroy();
		}

		manager.Players.Remove( steamId );
		_spawned.Remove( steamId );

		var ranks = RankSystem.Instance;
		if ( ranks.IsValid() )
		{
			ranks.SetPlayerRanks( steamId, new List<Guid>() );
		}
	}

	[ConCmd( "lifepunch_clear_testbots" )]
	public static void ClearTestBots()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			return;
		}

		var manager = GameNetworkManager.Instance;
		var ranks = RankSystem.Instance;
		var removed = 0;

		foreach ( var id in _spawned )
		{
			if ( manager.IsValid() && manager.Players.TryGetValue( id, out var player ) && player.IsValid() )
			{
				player.GameObject.Destroy();
			}

			manager?.Players.Remove( id );

			// Drop any rank we assigned so placeholder ids don't linger in RankSystem between runs.
			if ( ranks.IsValid() )
			{
				ranks.SetPlayerRanks( id, new List<Guid>() );
			}

			removed++;
		}

		_spawned.Clear();
		Log.Info( $"lifepunch_clear_testbots: removed {removed} dummy player(s)." );
	}

	// One bot per rank, in display order. Rank "" means no assignment — a regular/default player who
	// holds no explicit rank (so GetPlayerRank falls back to the portal's default "None"). Named ranks
	// resolve against the live portal via RankSystem.FindRankIdByName (lifepunch_list_ranks confirms the
	// strings). The SteamIds are PUBLIC placeholder accounts so each bot resolves a real Steam avatar in
	// the menus/chat — swap freely; they only need to be valid, public, and NOT the dev's own id (a bot
	// sharing the dev's id would share rank/CanTarget state). "Greg" mirrors Owner for killswitch tests.
	private static readonly (string Name, string Rank, long SteamId)[] RankBots =
	{
		( "Regular Bot", "", 76561198172576363L ),
		( "VIP Bot", "VIP", 76561197960287930L ),
		( "EVIP Bot", "EVIP", 76561198200329058L ),
		( "Mod Bot", "Mod", 76561198163939993L ),
		( "Admin Bot", "Admin", 76561198042858602L ),
		( "Super Admin Bot", "Super Admin", 76561198822683862L ),
		( "Greg", "Owner", 76561198010565263L ),
	// Extra regular players to push the roster count up for admin-menu layout/density testing.
		( "Player Bot 1", "", 76561197964781654L ),
		( "Player Bot 2", "", 76561198005079964L ),
		( "Player Bot 3", "", 76561198012345678L ),
		( "Player Bot 4", "", 76561198023456789L ),
		( "Player Bot 5", "", 76561198034567890L )
	};

	public const int DefaultScrollFillCount = 18;

	[ConCmd( "lifepunch_spawn_scroll_testbots" )]
	public static void SpawnScrollTestBots()
	{
		SpawnScrollTestBotsInternal();
	}

	[ConCmd( "ulx_bots" )]
	public static void SpawnScrollTestBotsAlias()
	{
		SpawnScrollTestBotsInternal();
	}

	private static void SpawnScrollTestBotsInternal()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lifepunch_spawn_scroll_testbots: editor-only dev command." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lifepunch_spawn_scroll_testbots: must be host (editor play)." );
			return;
		}

		ClearTestBots();
		SpawnRankBots( false );
		var filled = SpawnScrollFillBots( DefaultScrollFillCount );
		Log.Info( $"lifepunch_spawn_scroll_testbots: rank roster + {filled} scroll fillers — open /lifepunchulx and wheel the sidebar." );

		if ( !StaffMenuHost.IsOpen )
		{
			StaffMenuHost.Toggle();
		}

		LogScrollMetricsSoon();
	}

	private static async void LogScrollMetricsSoon()
	{
		await GameTask.DelaySeconds( 0.35f );
		LogScrollMetrics();
	}

	private static void LogScrollMetrics()
	{
		if ( StaffMenuHost.DevMenu?.Panel is not { IsValid: true } root )
		{
			Log.Warning( "ulx scroll metrics: menu panel not ready." );
			return;
		}

		LifePunchUiScrollPolicy.Apply( root );

		foreach ( var panel in root.Descendants.Where( p => p.HasClass( "player-scroll" )
		                                                 || p.HasClass( "profile-fields" )
		                                                 || p.HasClass( "wp-list" )
		                                                 || p.HasClass( "audit-scroll" ) ) )
		{
			var viewH = panel.Box.Rect.Height;
			var contentH = LifePunchScrollLayout.GetStackedContentHeight( panel );
			var maxY = LifePunchScrollLayout.GetManualScrollMaxY( panel );

			Log.Info( $"ulx scroll metrics: {panel.GetType().Name} view={viewH:F0} content={contentH:F0} maxY={maxY:F0} offset={panel.ScrollOffset.y:F0} hasScrollY={panel.HasScrollY}" );

			if ( maxY > 8f )
			{
				panel.ScrollOffset = new Vector2( 0f, 64f );
				LifePunchUiScrollPolicy.Apply( root );
				Log.Info( $"ulx scroll metrics: test offset -> {panel.ScrollOffset.y:F0}" );
			}
		}
	}

	/// <summary>Regular (no-rank) bots with fake SteamIds — pushes <see cref="StaffMenu"/> sidebar past scroll height.</summary>
	public static int SpawnScrollFillBots( int count )
	{
		if ( count <= 0 || !Application.IsEditor || !Networking.IsHost )
		{
			return 0;
		}

		var spawned = 0;
		for ( var i = 0; i < count; i++ )
		{
			var fakeId = FakeSteamIdBase + ++_spawnCount;
			var name = $"Scroll Fill {i + 1}";
			if ( SpawnBot( name, fakeId, SpawnFannedOut( RankBots.Length + i ) ).IsValid() )
			{
				spawned++;
			}
		}

		return spawned;
	}

	[ConCmd( "lifepunch_spawn_all_testbots" )]
	public static void SpawnAllTestBots()
	{
		SpawnRankBots( true );
	}

	[ConCmd( "lifepunch_spawn_rankbots" )]
	public static void SpawnRankBots( bool includeOwner = true )
	{
		SpawnRankBotsInternal( includeOwner, null );
	}

	[ConCmd( "lifepunch_spawn_staff_tier_bots" )]
	public static void SpawnStaffTierBots()
	{
		SpawnStaffTierBotsInternal();
	}

	[ConCmd( "ulx_staff_bots" )]
	public static void SpawnStaffTierBotsAlias()
	{
		SpawnStaffTierBotsInternal();
	}

	private static void SpawnStaffTierBotsInternal()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lifepunch_spawn_staff_tier_bots: editor-only dev command." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lifepunch_spawn_staff_tier_bots: must be host (editor play)." );
			return;
		}

		SpawnStaffTierBotsSoon();
	}

	private static async void SpawnStaffTierBotsSoon()
	{
		var rankFilter = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
		{
			"Mod",
			"Admin",
			"Super Admin"
		};

		ClearTestBots();
		foreach ( var def in RankBots )
		{
			if ( rankFilter.Contains( def.Rank ) )
			{
				EnsureBotSlot( def.SteamId );
			}
		}

		var spawned = SpawnRankBotsInternal( false, rankFilter );
		if ( spawned == 0 )
		{
			Log.Warning( "lifepunch_spawn_staff_tier_bots: no bots spawned — is the player prefab ready?" );
			return;
		}

		if ( !await WaitForRankCacheAsync( "Mod", 45f ) )
		{
			Log.Warning( "lifepunch_spawn_staff_tier_bots: portal ranks not ready — run lp_authorize, then ulx_staff_bots again to assign Mod/Admin/Super Admin tiers." );
		}
		else
		{
			ApplyRankAssignments( rankFilter );
		}

		Log.Info( $"lifepunch_spawn_staff_tier_bots: {spawned} bot(s) ready — open /lifepunchulx and check the Staff section." );

		if ( !StaffMenuHost.IsOpen )
		{
			StaffMenuHost.Toggle();
		}
	}

	private static async Task<bool> WaitForRankCacheAsync( string probeRank, float timeoutSeconds )
	{
		var deadline = DateTime.UtcNow.AddSeconds( timeoutSeconds );
		while ( DateTime.UtcNow < deadline )
		{
			if ( ServerApiLink.HasAuthorizationKey )
			{
				if ( !ResolveRankId( probeRank ).HasValue && RankIdByName.Count == 0 )
				{
					await SyncPortalRankTableFromApiAsync();
				}

				if ( ResolveRankId( probeRank ).HasValue )
				{
					return true;
				}
			}

			await GameTask.DelaySeconds( 0.5f );
		}

		return ResolveRankId( probeRank ).HasValue;
	}

	private static void ApplyRankAssignments( HashSet<string> rankFilter )
	{
		foreach ( var def in RankBots )
		{
			if ( string.IsNullOrEmpty( def.Rank ) || !rankFilter.Contains( def.Rank ) )
			{
				continue;
			}

			TryAssignBotRank( def.Name, def.SteamId, def.Rank );
		}
	}

	private static int SpawnRankBotsInternal( bool includeOwner, HashSet<string> rankFilter )
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lifepunch_spawn_rankbots: editor-only dev command." );
			return 0;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lifepunch_spawn_rankbots: must be host (editor play)." );
			return 0;
		}

		var ranks = RankSystem.Instance;
		if ( !ranks.IsValid() )
		{
			Log.Error( "lifepunch_spawn_rankbots: RankSystem unavailable (not connected to the portal yet?)." );
			return 0;
		}

		var index = 0;
		var spawned = 0;
		foreach ( var def in RankBots )
		{
			if ( !includeOwner && def.Rank == "Owner" )
			{
				continue;
			}

			if ( rankFilter != null )
			{
				if ( string.IsNullOrEmpty( def.Rank ) || !rankFilter.Contains( def.Rank ) )
				{
					continue;
				}
			}

			var player = SpawnBot( def.Name, def.SteamId, SpawnFannedOut( index++ ) );
			if ( !player.IsValid() )
			{
				continue;
			}

			spawned++;

			if ( string.IsNullOrEmpty( def.Rank ) )
			{
				Log.Info( $"lifepunch_spawn_rankbots: '{def.Name}' spawned as regular (no rank)." );
				continue;
			}

			TryAssignBotRank( def.Name, def.SteamId, def.Rank );
		}

		return spawned;
	}

	[ConCmd( "lifepunch_list_ranks" )]
	public static void ListRanks()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lifepunch_list_ranks: editor host-only dev command." );
			return;
		}

		var ranks = RankSystem.Instance;
		if ( !ranks.IsValid() )
		{
			Log.Error( "lifepunch_list_ranks: RankSystem unavailable (not connected to the portal yet?)." );
			return;
		}

		// RankSystem.Ranks is private and the editor bridge can't read the dictionary contents, so we
		// probe the expected ladder plus common variants by name to confirm which strings the portal
		// actually loaded. Anything that resolves to a Guid is a real rank we can assign to a bot.
		var candidates = new[]
		{
			"Owner", "Super Admin", "Admin", "Mod", "Moderator", "Trial Mod",
			"EVIP", "Extreme VIP", "Elite VIP", "VIP", "Donator", "Donor",
			"Member", "User", "Default", "None"
		};

		Log.Info( "[lifepunch_list_ranks] probing rank names (name -> rank Guid):" );
		foreach ( var name in candidates )
		{
			var id = FindRankIdByName( name );
			Log.Info( $"  {name,-14} -> {(id.HasValue ? id.Value.ToString() : "(not found)")}" );
		}

		// Ground truth straight from the live data: the local player's resolved rank name + order.
		var localId = Sandbox.Game.SteamId;
		Log.Info( $"[lifepunch_list_ranks] local ({localId}) resolves to rank '{ranks.GetRankName( localId )}' (order {ranks.GetRankOrder( localId )})." );
	}

	// One quoted string arg, e.g.  lifepunch_botsay "Greg hello world". The ConCmd binder rejects a
	// string[] param (ToType can't build one from console input) and multi-arg binding is unreliable, so
	// we take the whole thing and split the first token off as the bot, leaving the rest as the message.
	[ConCmd( "lifepunch_botsay" )]
	public static void BotSay( string args = "" )
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lifepunch_botsay: editor host-only dev command." );
			return;
		}

		var trimmed = ( args ?? "" ).Trim();
		var split = trimmed.IndexOf( ' ' );
		var message = split > 0 ? trimmed[(split + 1)..].Trim() : "";
		if ( split <= 0 || string.IsNullOrWhiteSpace( message ) )
		{
			Log.Warning( "lifepunch_botsay: usage  lifepunch_botsay \"<bot name|steamId> <message>\"" );
			return;
		}

		var botToken = trimmed[..split];
		var steamId = ResolveBotSteamId( botToken );
		if ( steamId == 0 )
		{
			Log.Warning( $"lifepunch_botsay: no spawned bot matches '{botToken}'. Spawn some with lifepunch_spawn_rankbots first." );
			return;
		}

		// Chat.Current is a GameObjectSystem (not a Component), so it has no IsValid() — use a null check.
		var chat = Chat.Current;
		if ( chat == null )
		{
			Log.Error( "lifepunch_botsay: Chat unavailable." );
			return;
		}

		var player = GameUtils.Players.FirstOrDefault( p => p.IsValid() && p.SteamId == steamId );
		var label = player.IsValid() ? player.DisplayName : botToken;
		chat.BroadcastChat( $"{label}: {message}", MessageType.GlobalChat );
	}

	// Resolve a spawned bot by raw SteamId or by a case-insensitive substring of its display name.
	private static long ResolveBotSteamId( string token )
	{
		if ( long.TryParse( token, out var rawId ) && _spawned.Contains( rawId ) )
		{
			return rawId;
		}

		var manager = GameNetworkManager.Instance;
		if ( !manager.IsValid() )
		{
			return 0;
		}

		foreach ( var id in _spawned )
		{
			if ( manager.Players.TryGetValue( id, out var player ) && player.IsValid() &&
			     player.DisplayName.Contains( token, StringComparison.OrdinalIgnoreCase ) )
			{
				return id;
			}
		}

		return 0;
	}

	// A spot a short distance in front of the local player so the dummy is easy to see and teleport-test.
	private static Vector3 SpawnNearLocal()
	{
		var local = Player.Local;
		if ( local.IsValid() )
		{
			return local.WorldPosition + local.WorldRotation.Forward * 80f + Vector3.Up * 10f;
		}

		return Vector3.Zero;
	}

	// Spread rank bots along a row in front of the local player so they don't stack on one spot.
	private static Vector3 SpawnFannedOut( int index )
	{
		var local = Player.Local;
		if ( !local.IsValid() )
		{
			return Vector3.Zero;
		}

		var rot = local.WorldRotation;
		var lateral = ( index - 3 ) * 50f; // centre the row on the forward axis
		return local.WorldPosition + rot.Forward * 110f + rot.Right * lateral + Vector3.Up * 10f;
	}
}

#endif
