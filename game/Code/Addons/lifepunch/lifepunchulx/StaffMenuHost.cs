// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.ulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// A player row the menu can render and target. Define-free so it flows through the
/// Dxura-free <see cref="StaffMenu"/> razor. <see cref="GroupName"/> buckets the roster by staff tier
/// (the player's rank name) with all non-staff collapsed into "Players"; <see cref="GroupOrder"/>
/// sorts the groups (highest rank first, "Players" last). <see cref="Role"/> is the player's real,
/// sanitised rank name for the detail pane (distinct from <see cref="GroupName"/>, which buckets
/// non-staff under "Players"); <see cref="RankColorHex"/> is the rank colour as "#RRGGBB";
/// <see cref="PlayTimeMinutes"/> is DXRP playtime in minutes (display as <c>/ 60</c> hours).
/// </summary>
public readonly record struct StaffMenuPlayer(
	long SteamId,
	string Name,
	bool CanTarget,
	string GroupName,
	int GroupOrder,
	string Role,
	string RankColorHex,
	int PlayTimeMinutes );

/// <summary>
/// Richer, live per-player info for the selected-player detail pane. Define-free so it flows through the
/// Dxura-free razor. Computed on demand for the selected player only (not every row every frame).
/// <see cref="Found"/> is false for an off-roster / disconnected target (only <see cref="SteamId"/> is
/// then meaningful).
/// </summary>
public readonly record struct StaffPlayerDetail(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	int PlayTimeMinutes,
	string Job,
	string JobColorHex,
	int Wallet,
	int Bank,
	int Health,
	int MaxHealth,
	int Armor,
	int Kills,
	int Deaths,
	bool Found );

/// <summary>
/// One row of the DXRP portal Audit log, define-free so it flows through the Dxura-free razor.
/// Mirrors the portal Audit page columns 1:1 (<c>When</c> / <c>Action</c> / <c>Player</c> /
/// <c>Entity</c> / <c>Description</c>). <see cref="When"/> is a pre-formatted, display-ready string
/// (the host owns timestamp formatting so the razor stays logic-light). <see cref="PlayerSteamId"/>
/// is the raw SteamID64 (0 when the actor is the server/system) so the UI can resolve an avatar and
/// match the Player-ID filter without re-parsing <see cref="Player"/>.
/// </summary>
/// <summary>
/// One sanction row, projected free of Dxura types so it flows through the Dxura-free razor.
/// <see cref="TypeClass"/> is the lowercased type ("ban", "warning") used as a CSS class;
/// <see cref="IsActive"/> drives the prominent red treatment an in-force sanction requires.
/// </summary>
/// <summary>
/// One live state flag on a player. <see cref="Illegitimate"/> is the point of the section: the
/// flag is ON but the player does not hold the portal permission that grants it, which means
/// someone is using a command they were never given. A flag a staff member legitimately holds
/// reads as ordinary state.
/// </summary>
public readonly record struct StaffStateFlag( string Label, bool Illegitimate );

public readonly record struct StaffSanction(
	string Type,
	string TypeClass,
	bool IsActive,
	string Reason,
	string Duration,
	string Created );

public readonly record struct StaffAuditEntry(
	string When,
	string Action,
	string Player,
	long PlayerSteamId,
	string Entity,
	string Description );

/// <summary>
/// One gamemode job row for the Set Job picker. Define-free so the razor compiles in the editor build.
/// <see cref="Token"/> is dispatched to DXRP's <c>/job</c> command (internal job name).
/// </summary>
public readonly record struct StaffJobOption( string Token, string Label, string ColorHex );

/// <summary>
/// Dual-build host bindings for LIFEPUNCH ULX (<c>lifepunchulx</c>).
///
/// All DXRP coupling lives here behind <c>#if !LIFEPUNCH_LOCAL</c> so <c>StaffMenu.razor</c> and
/// <c>StaffMenuActions.cs</c> stay define-free and compile in the standalone s&amp;box editor. On the
/// dxrp.net build the real branch binds to <c>RankSystem</c> (client-side permission reads),
/// <c>AdminSystem</c> host RPCs, and <c>Chat.ExecuteCommandHost</c>. The local branch returns
/// permissive stubs and a dummy roster so the UI renders and clicks log instead of dispatching.
///
/// This uses a HUD-mounted host pattern: the engine define
/// <c>LIFEPUNCH_LOCAL</c> reliably reaches plain .cs files even when the editor's Razor pass
/// does not honour it.
/// </summary>
internal static class StaffMenuHost
{
	private static StaffMenu? _instance;

	/// <summary>Whether the menu is currently mounted/open.</summary>
	public static bool IsOpen => _instance.IsValid();

	/// <summary>Editor dev: the live menu instance (for scroll probes / test bots).</summary>
	internal static StaffMenu? DevMenu => _instance;

	/// <summary>The local viewer's Steam ID. Define-free: valid in both builds.</summary>
	public static long LocalSteamId => Sandbox.Game.SteamId;

	/// <summary>
	/// The local viewer's rank order on the live ladder (Mod=4, Admin=5, Super Admin=10, Owner=69).
	/// Drives the configurable ban-duration cap. In the editor build we pretend Super Admin so the
	/// full catalog renders.
	/// </summary>
	public static int LocalRankOrder
	{
#if LIFEPUNCH_LOCAL
		get => 10;
#else
		get => RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankOrder( LocalSteamId ) : 0;
#endif
	}

	/// <summary>
	/// Client-local UI scale preference — survives menu close/reopen for the session (not portal-persisted).
	/// </summary>
	public static LifePunchUiScaleSize SavedUiScale { get; set; } = LifePunchUiScaleSize.ExtraLarge;

	// --- Open / close ------------------------------------------------------

	/// <summary>
	/// Console + chat entry point. Staff bind any key to <c>lifepunchulx</c>, <c>menu</c>, or <c>ulx</c>
	/// (e.g. <c>bind f4 lifepunchulx</c>). Chat: <c>/lifepunchulx</c>, <c>/menu</c>, <c>/ulx</c>.
	/// </summary>
	[ConCmd( "lifepunchulx" )]
	public static void LifepunchUlxConCmd() => Toggle();

	[ConCmd( "menu" )]
	public static void MenuConCmd() => Toggle();

	[ConCmd( "ulx" )]
	public static void UlxConCmd() => Toggle();

	/// <summary>
	/// Open the menu if closed, else close it. Open-for-all by design: any player may open it via the
	/// command; non-staff simply see an empty catalog (every action is permission-gated per-row). This
	/// matches the portal model where access is decided purely by rank grants on the viewer's Steam ID.
	/// </summary>
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

		Log.Warning( "[lifepunchulx] Toggle failed — menu did not mount (see prior mount warnings)." );
	}

	/// <summary>Close and tear down the open menu, if any.</summary>
	public static void RequestClose()
	{
		if ( _instance.IsValid() )
		{
			Close( _instance );
		}

		_instance = null;
		SetCursorMode( false );
	}

	/// <summary>
	/// While the menu is open we release the local player's look controls so the cursor frees up and the
	/// panel becomes clickable. DXRP's <c>Player.LockCamera</c> drives <c>Controller.UseLookControls</c>
	/// each frame (see <c>Player.Camera.cs</c>) — the same hook the command wheel and camera zoom use, so
	/// we reuse the engine mechanism rather than touching the cursor directly. No-op in the editor build.
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

	/// <summary>
	/// True when a self-toggle action is ACTIVE on the local player right now. Read from live DXRP
	/// state -- the networked status dictionary for god/cloak/incognito, the controller's noclip mode
	/// for flight -- so a lit menu card and the HUD indicator can never disagree. A local click flag
	/// would desync the moment anything else changed the state (death, respawn, another admin,
	/// reconnect), which is exactly what this exists to avoid. Always false in the editor build.
	/// </summary>
	public static bool IsSelfToggleOn( string actionKey )
	{
#if !LIFEPUNCH_LOCAL
		if ( !Player.Local.IsValid() )
		{
			return false;
		}

		if ( actionKey == "noclip" )
		{
			if ( !Player.Local.Controller.IsValid() )
			{
				return false;
			}

			var noclip = Player.Local.Controller.Components.Get<MoveModeNoClip>();
			return noclip.IsValid() && noclip.IsNoclipping;
		}

		// God mode is NOT a status -- it is a property on HealthComponent, and it is the exact
		// value DXRP's own HUD reads (Vitals.razor: HealthComponent?.IsGodMode). Reading the same
		// field is what makes drift between the card and the HUD impossible.
		if ( actionKey == "god" )
		{
			return Player.Local.HealthComponent.IsValid() && Player.Local.HealthComponent.IsGodMode;
		}

		// Cloak and incognito ARE statuses. The live Statuses dictionary is keyed by the same short
		// lowercase id as the chat command that sets them ("afk" observed in session).
		if ( actionKey is "cloak" or "incognito" )
		{
			return Player.Local.HasStatus( actionKey );
		}
#endif
		return false;
	}

	/// <summary>
	/// Health colour taken from DXRP's OWN HUD function -- <c>UiUtils.HealthColorHex</c>, the one
	/// <c>PlayerInfo.razor</c> and <c>PartyMemberCard.razor</c> both call. Calling it rather than
	/// copying its constants is what makes console/HUD drift impossible: if DXRP retunes the ramp,
	/// this follows. Ramp of record: #f87171 at <=15%, #facc15 mid, #22c55e high, lerped.
	/// Empty string in the editor build, where the razor falls back to its own token.
	/// </summary>
	public static string HealthColorHex( int health, int maxHealth )
	{
#if !LIFEPUNCH_LOCAL
		var percent = maxHealth > 0
			? System.Math.Clamp( (float)health / maxHealth * 100f, 0f, 100f )
			: 0f;
		return Dxura.RP.Game.UI.UiUtils.HealthColorHex( percent );
#else
		return "";
#endif
	}

	// --- Sanction history (real DXRP source, not a stub) ------------------
	// Backed by PlayerSanctionHistorySystem, the same system DXRP's own
	// UI/Menus/TabMenu/Sections/Components/PlayerSanctionHistory.razor consumes.

	/// <summary>Bumped when a sanction request is issued, so BuildHash re-renders on arrival.</summary>
	public static int SanctionsVersion { get; private set; }

	public static bool CanViewSanctions() => CanView( "player.sanctions.view.other" );

	/// <summary>
	/// Ask the host for a player's sanction history. Idempotent per player: the system already
	/// tracks CurrentPlayerId, so re-requesting the same player is a no-op and this is safe to
	/// call from a render path.
	/// </summary>
	public static void RequestSanctions( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		if ( steamId == 0 || !CanViewSanctions() )
		{
			return;
		}

		var system = PlayerSanctionHistorySystem.Current;
		if ( system is null || system.CurrentPlayerId == steamId )
		{
			return;
		}

		var requestId = System.Guid.NewGuid();
		system.BeginLoadingClient( steamId, requestId );
		system.RequestSanctionsHost( steamId, requestId );
		SanctionsVersion++;
#endif
	}

	public static bool SanctionsLoading( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var system = PlayerSanctionHistorySystem.Current;
		return system is not null && system.IsLoading && system.CurrentPlayerId == steamId;
#else
		return false;
#endif
	}

	/// <summary>
	/// This player's sanctions, projected Dxura-free so the razor stays clean. Empty when the
	/// caller lacks the portal permission or the system has not answered for this player yet --
	/// the razor renders an explicit "no sanctions" row rather than a blank void.
	/// </summary>
	public static IReadOnlyList<StaffSanction> GetSanctions( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var system = PlayerSanctionHistorySystem.Current;
		if ( system is null || !CanViewSanctions() || system.CurrentPlayerId != steamId )
		{
			return System.Array.Empty<StaffSanction>();
		}

		var rows = new List<StaffSanction>();
		foreach ( var entry in system.VisibleSanctions )
		{
			var type = entry.Type.ToString();
			rows.Add( new StaffSanction(
				SplitPascalCase( type ),
				type.ToLowerInvariant(),
				entry.State.ToString() == "Active",
				string.IsNullOrWhiteSpace( entry.Reason ) ? "No reason recorded" : entry.Reason,
				entry.Duration is null ? "Permanent" : entry.Duration.Value.ToString(),
				entry.Created.ToString( "yyyy-MM-dd HH:mm" ) ) );
		}

		return rows;
#else
		return System.Array.Empty<StaffSanction>();
#endif
	}

	/// <summary>
	/// The player's job CATEGORY, resolved through DXRP's own taxonomy rather than a list we
	/// invented: every job carries a GameModeJobGroupId, and the group's Name is the category the
	/// tenant configured. Empty when the job or its group is not resolvable.
	/// </summary>
	public static string GetJobCategory( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() || player.Job is null )
		{
			return "";
		}

		return GameModeJobs.FindGroupById( player.Job.GameModeJobGroupId )?.Name ?? "";
#else
		return "";
#endif
	}

	/// <summary>
	/// Live state flags for a player, read from the same sources the commands write to. Only ACTIVE
	/// flags are returned. Powers (god/cloak/incognito/noclip) are checked against the permission
	/// that grants them, so an operator can tell a staff member's own toggle from a player running
	/// a command they should not have. Conditions (frozen/jailed/gagged) are done TO a player
	/// rather than wielded by one, so they are never flagged illegitimate.
	/// </summary>
	public static IReadOnlyList<StaffStateFlag> GetStateFlags( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return System.Array.Empty<StaffStateFlag>();
		}

		var flags = new List<StaffStateFlag>();

		void Power( bool active, string label, string permissionId )
		{
			if ( active )
			{
				flags.Add( new StaffStateFlag( label, !RankSystem.HasPermission( steamId, permissionId ) ) );
			}
		}

		// God mode is a HealthComponent property, not a status -- same field Vitals.razor reads.
		Power( player.HealthComponent.IsValid() && player.HealthComponent.IsGodMode, "God mode", "command.god" );
		Power( player.HasStatus( "cloak" ), "Cloaked", "command.cloak" );
		Power( player.HasStatus( "incognito" ), "Incognito", "command.incognito" );

		var noclip = player.Controller.IsValid()
			? player.Controller.Components.Get<MoveModeNoClip>()
			: null;
		Power( noclip.IsValid() && noclip.IsNoclipping, "Noclip", "ability.noclip" );

		// Status ids are DXRP's own constants (Constants.FreezeStatus / PrisonerStatus / GaggedStatus).
		if ( player.HasStatus( "freeze" ) )
		{
			flags.Add( new StaffStateFlag( "Frozen", false ) );
		}

		if ( player.HasStatus( "prisoner" ) )
		{
			flags.Add( new StaffStateFlag( "Jailed", false ) );
		}

		if ( player.HasStatus( "gagged" ) )
		{
			flags.Add( new StaffStateFlag( "Gagged", false ) );
		}

		return flags;
#else
		return System.Array.Empty<StaffStateFlag>();
#endif
	}

	/// <summary>"AutomaticBan" -> "Automatic Ban", matching DXRP's own sanction label rendering.</summary>
	private static string SplitPascalCase( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return "";
		}

		var builder = new System.Text.StringBuilder( value.Length + 8 );
		for ( var i = 0; i < value.Length; i++ )
		{
			if ( i > 0 && char.IsUpper( value[i] ) )
			{
				builder.Append( ' ' );
			}

			builder.Append( value[i] );
		}

		return builder.ToString();
	}

	private static StaffMenu? Mount()
	{
		// Close any prior instance first (guards against a stale component surviving a hotload/reload).
		var existing = Sandbox.Game.ActiveScene?.GetAllComponents<StaffMenu>().FirstOrDefault();
		if ( existing.IsValid() )
		{
			Close( existing );
		}

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel();
#else
		// Prefer DXRP HUD root (proven clickable path). Dedicated / early join sometimes has no HUD root yet.
		var panel = GameManager.ShowUi<StaffMenu>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( "[lifepunchulx] GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return MountOnScreenPanel();
#endif
	}

	private static StaffMenu? MountOnScreenPanel()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "[lifepunchulx] ActiveScene is null — cannot mount menu." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = MenuObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<StaffMenu>();
	}

	private const string MenuObjectName = "LifePunchUlx";

	private static void Close( StaffMenu menu )
	{
		if ( !menu.IsValid() )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		// Local build hosts the panel on its own object, so destroy the whole object.
		if ( menu.GameObject.IsValid() )
		{
			menu.GameObject.Destroy();
		}
		else
		{
			menu.Destroy();
		}
#else
		// ShowUi shares the HUD root; ScreenPanel fallback uses MenuObjectName — match LpHashdUiHost teardown.
		if ( menu.GameObject.IsValid() && menu.GameObject.Name == MenuObjectName )
			menu.GameObject.Destroy();
		else
			menu.Destroy();
#endif
	}

	// --- Permission / roster reads (client-side, for UX gating only) -------

	/// <summary>
	/// True if the local viewer's ranks grant <paramref name="permissionId"/>. UX gating only —
	/// the host re-checks on dispatch.
	/// </summary>
	public static bool CanView( string permissionId )
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		return RankSystem.HasLocalPermission( permissionId );
#endif
	}

	/// <summary>Label for the "non-staff" roster bucket.</summary>
	public const string NonStaffGroup = "Players";

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// DXRP stores cumulative playtime in <see cref="Player.PlayTime"/> as elapsed seconds
	/// (<see cref="TimeSince"/>); divide by 60 for portal-style minutes (matches VoteSystem checks).
	/// </summary>
	static int PlayTimeMinutesFrom( Player player ) => (int)( player.PlayTime / 60f );
#endif

	/// <summary>The online players the menu can list, grouped by staff tier, targetability resolved.</summary>
	public static IReadOnlyList<StaffMenuPlayer> OnlinePlayers()
	{
#if LIFEPUNCH_LOCAL
		return new List<StaffMenuPlayer>
		{
			new( 5L, "Owner Olivia", false, "Owner", 100, "Owner", "#E74C3C", 10980 ),
			new( 4L, "Super Sam", false, "Super Admin", 10, "Super Admin", "#3498DB", 5400 ),
			new( 3L, "Admin Andy", false, "Admin", 5, "Admin", "#2ECC71", 2400 ),
			new( 6L, "Mod Maddie", true, "Mod", 4, "Mod", "#9B59B6", 900 ),
			new( 1L, "Regular Rick", true, NonStaffGroup, int.MinValue, "Member", "#FFFFFF", 300 ),
			new( 2L, "Suspicious Sammy", true, NonStaffGroup, int.MinValue, "VIP", "#F1C40F", 120 )
		};
#else
		return GameUtils.Players
			.Where( player => player.IsValid() )
			.Select( player =>
			{
				var staff = IsStaff( player.SteamId );
				var group = staff ? RankName( player.SteamId ) : NonStaffGroup;
				var order = staff ? RankOrder( player.SteamId ) : int.MinValue;
				return new StaffMenuPlayer(
					player.SteamId,
					player.DisplayName,
					RankSystem.CanLocalTarget( player.SteamId ),
					group,
					order,
					RealRole( player.SteamId ),
					RankColorHex( player.SteamId ),
					PlayTimeMinutesFrom( player ) );
			} )
			.ToList();
#endif
	}

	/// <summary>Live detail for the selected player's profile pane. Found=false if they aren't connected.</summary>
	public static StaffPlayerDetail GetPlayerDetail( long steamId )
	{
#if LIFEPUNCH_LOCAL
		var p = OnlinePlayers().FirstOrDefault( x => x.SteamId == steamId );
		if ( p.SteamId == 0 )
		{
			return new StaffPlayerDetail( steamId, "", "—", "#ffffff", 0, "—", "#ffffff", 0, 0, 0, 0, 0, 0, 0, false );
		}

		return new StaffPlayerDetail( p.SteamId, p.Name, p.Role, p.RankColorHex, p.PlayTimeMinutes,
			"Citizen", "#5DA9E9", 1240, 540323, 100, 100, 25, 12, 4, true );
#else
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return new StaffPlayerDetail( steamId, "", "—", "#ffffff", 0, "—", "#ffffff", 0, 0, 0, 0, 0, 0, 0, false );
		}

		var health = player.HealthComponent.IsValid() ? (int)player.HealthComponent.Health : 0;
		var maxHealth = player.HealthComponent.IsValid() ? (int)player.HealthComponent.MaxHealth : 0;
		var armor = player.ArmorComponent.IsValid() ? (int)player.ArmorComponent.Armor : 0;

		// JobDisplayName is CustomJob ?? Job.DisplayName(); guard the rare pre-init state where both are null.
		var job = "—";
		var jobColor = "#ffffff";
		if ( player.CustomJob != null || player.Job != null )
		{
			var name = player.JobDisplayName;
			if ( !string.IsNullOrWhiteSpace( name ) )
			{
				job = name;
			}
		}

		if ( player.Job != null )
		{
			jobColor = $"#{player.Job.Color & 0xFFFFFFu:X6}";
		}

		return new StaffPlayerDetail(
			player.SteamId,
			player.DisplayName,
			RealRole( player.SteamId ),
			RankColorHex( player.SteamId ),
			PlayTimeMinutesFrom( player ),
			job,
			jobColor,
			(int)player.WalletBalance,
			(int)player.BankBalance,
			health,
			maxHealth,
			armor,
			player.Kills,
			player.Deaths,
			true );
#endif
	}

	// --- Audit log (read-side, portal-mirrored) ---------------------------

	/// <summary>
	/// Permission Id that gates the in-menu Audit viewer. Hardcoded string (define-free editor build,
	/// see TECH_DEBT STAFF-01) — reconciles with the portal's audit-visibility grant. The portal's
	/// recommended tiers (Mod: own-action only → Super Admin / Community Manager: broad) are enforced
	/// server-side when the real read API is wired (TECH_DEBT STAFF-07).
	/// </summary>
	public const string AuditPermissionId = "audit.view";

	/// <summary>True if the local viewer may open the Audit log. UX gating only; host re-checks the fetch.</summary>
	public static bool CanViewAudit() => CanView( AuditPermissionId );

	/// <summary>
	/// Audit entries for the viewer, newest first, pre-filtered by the portal's two live filters:
	/// free-text <paramref name="playerId"/> (matches SteamID64 or actor name; <c>system</c> for
	/// server/automated entries) and free-text <paramref name="entityId"/>. The live portal Audit page
	/// has exactly these two filters — there is no Action dropdown — so the menu mirrors it 1:1.
	///
	/// Mirrors the portal's <c>GET /v1/audit/events</c> (pageIndex/pageSize, Bearer, tenant-scoped).
	/// Editor build returns a representative stub set so the whole UX renders and filters live. The real
	/// dxrp.net read path is portal/HTTP-backed and async; the in-game <c>ServerApiClient</c> doesn't yet
	/// expose the audit read, so the server branch returns empty until that lands (TECH_DEBT STAFF-07) —
	/// the UI degrades to a clean "no entries" state rather than blocking a per-frame read.
	/// </summary>
	public static IReadOnlyList<StaffAuditEntry> GetAuditEntries( string playerId, string entityId )
	{
#if LIFEPUNCH_LOCAL
		var source = AuditStub();
#else
		// TODO(STAFF-07): bind to the DXRP portal audit read API — GET /v1/audit/events
		// (pageIndex/pageSize, Bearer, tenant-scoped; player/entity filter params) via ServerApiClient,
		// async + cached by filter with a short TTL, with the portal's per-rank visibility server-side.
		var source = (IReadOnlyList<StaffAuditEntry>)System.Array.Empty<StaffAuditEntry>();
#endif
		return FilterAudit( source, playerId, entityId );
	}

	private static IReadOnlyList<StaffAuditEntry> FilterAudit(
		IReadOnlyList<StaffAuditEntry> source, string playerId, string entityId )
	{
		var player = playerId?.Trim() ?? "";
		var entity = entityId?.Trim() ?? "";

		return source.Where( e =>
		{
			if ( player.Length > 0
			     && !e.PlayerSteamId.ToString().Contains( player, System.StringComparison.OrdinalIgnoreCase )
			     && !e.Player.Contains( player, System.StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}

			if ( entity.Length > 0
			     && !e.Entity.Contains( entity, System.StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}

			return true;
		} ).ToList();
	}

#if LIFEPUNCH_LOCAL
	// Representative editor-only audit rows mirroring the live portal Audit page: real action types
	// (Chat / ModifyBalance / DispatchAction / Update / GenerateToken) shown as coloured pills, the
	// "system" actor (SteamId 0) for server/automated entries, and Server/Player entities. Lets the
	// viewer, filters and empty-states all be designed without the live backend.
	private static IReadOnlyList<StaffAuditEntry> AuditStub() => new List<StaffAuditEntry>
	{
		new( "just now", "Chat", "system", 0L, "Server", "[System] #system.automessage.rulebreakers" ),
		new( "just now", "ModifyBalance", "Regular Rick", 1L, "Player", "$6 for Salary" ),
		new( "2m ago", "DispatchAction", "Mod Maddie", 6L, "ServerAction", "Kicked Suspicious Sammy — reason: RDM" ),
		new( "14m ago", "DispatchAction", "Admin Andy", 3L, "ServerAction", "Banned Regular Rick — 3d, reason: cheating" ),
		new( "38m ago", "Chat", "Suspicious Sammy", 2L, "Server", "/advert WTS printers cheap" ),
		new( "1h ago", "Update", "Super Sam", 4L, "Player", "Changed Regular Rick rank → VIP" ),
		new( "2h ago", "ModifyBalance", "Regular Rick", 1L, "Player", "$12 for Salary" ),
		new( "3h ago", "GenerateToken", "Owner Olivia", 5L, "Server", "Generated server automation token" ),
		new( "Yesterday", "Update", "Owner Olivia", 5L, "Server", "Pinned gamemode revision dxura.rp@latest" )
	};
#endif

	// --- Waypoints (admin teleport bookmarks) -----------------------------

	/// <summary>
	/// Permission Ids gating the in-menu Waypoints panel, matching DXRP's
	/// <c>Dxura.RP.Game.Commands.WaypointCommand</c>: <c>command.waypoint.use</c> lists + teleports,
	/// <c>command.waypoint.edit</c> sets + clears. UX gating only — <c>/waypoint</c> re-checks host-side.
	/// </summary>
	public const string WaypointUsePermissionId = "command.waypoint.use";

	public const string WaypointEditPermissionId = "command.waypoint.edit";

	public static bool CanUseWaypoints() => CanView( WaypointUsePermissionId );

	public static bool CanEditWaypoints() => CanView( WaypointEditPermissionId );

	// The registered chat command every op routes through (mirrors WaypointCommand.Command). Going
	// through /waypoint keeps the host owning validation, the portal store write and the audit entry —
	// the menu stays a thin dispatch layer and never touches the store directly.
	private const string WaypointCommandName = "waypoint";

	/// <summary>
	/// Bumped whenever the cached waypoint list changes, so the razor's <c>BuildHash</c> re-renders the
	/// panel after a (editor) set/clear.
	/// </summary>
	public static int WaypointVersion { get; private set; }

#if LIFEPUNCH_LOCAL
	// Editor build: a live in-memory list so set/clear visibly update the panel with no backend.
	private static readonly List<string> _waypoints = new() { "bank", "nlr cave", "pd", "spawn" };
#else
	// dxrp.net build: host-synced. The waypoint store is host/token-scoped (ServerApiClient needs the
	// server authorization key), so the client can't read it directly — RefreshWaypoints asks the host
	// (via the addon-owned StaffMenuBridgeService) to read its own per-server store and push the names back
	// (OnWaypointsReceived). Self-contained in the addon: no DXRP core changes needed to drop it in.
	private static readonly List<string> _waypoints = new();
#endif

	/// <summary>Saved waypoint names (alphabetical). Editor: live stub; server: host-synced via RefreshWaypoints.</summary>
	public static IReadOnlyList<string> GetWaypoints() => _waypoints;

	/// <summary>
	/// Refresh the cached waypoint list. Editor build is already in-memory (no-op). Server build asks the
	/// host to read its token-scoped store and push the real names back, so the panel shows each server's
	/// own waypoints with zero per-server config (retires the STAFF-08 read limitation).
	/// </summary>
	public static void RefreshWaypoints()
	{
#if !LIFEPUNCH_LOCAL
		// Routes through the addon bridge (StaffMenuBridgeService), not DXRP core.
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.RequestWaypointsHost();
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Host→client callback (invoked by <see cref="StaffMenuBridgeService"/> filtered RPC):
	/// replace the cached list with the server's real waypoint names and bump the version so the razor's
	/// <c>BuildHash</c> re-renders.
	/// </summary>
	internal static void OnWaypointsReceived( string[] names )
	{
		_waypoints.Clear();
		if ( names != null )
		{
			_waypoints.AddRange( names );
		}

		WaypointVersion++;
	}

	// A /waypoint set|clear store write is async host-side; wait briefly, then re-read so the panel
	// reflects the change. The tab also refreshes on open, so this is a best-effort immediate update.
	private static async System.Threading.Tasks.Task RefreshAfterWrite()
	{
		await GameTask.DelayRealtimeSeconds( 0.4f );
		RefreshWaypoints();
	}
#endif

	/// <summary>Teleport the caller to a saved waypoint (host re-checks <c>command.waypoint.use</c>).</summary>
	public static void GoToWaypoint( string name ) => DispatchWaypoint( "go", name );

	/// <summary>Save a waypoint at the caller's position + aim (host re-checks <c>command.waypoint.edit</c>).</summary>
	public static void SetWaypoint( string name ) => DispatchWaypoint( "set", name );

	/// <summary>Delete a saved waypoint (host re-checks <c>command.waypoint.edit</c>).</summary>
	public static void ClearWaypoint( string name ) => DispatchWaypoint( "clear", name );

	private static void DispatchWaypoint( string op, string name )
	{
		name = name?.Trim() ?? "";
		if ( name.Length == 0 )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		var norm = name.ToLowerInvariant();
		switch ( op )
		{
			case "set" when !_waypoints.Contains( norm ):
				_waypoints.Add( norm );
				_waypoints.Sort( System.StringComparer.OrdinalIgnoreCase );
				WaypointVersion++;
				break;
			case "clear" when _waypoints.Remove( norm ):
				WaypointVersion++;
				break;
		}

		Log.Info( $"[lifepunchulx] (local stub) waypoint {op} '{name}'" );
#else
		// /waypoint grammar: "set <name>", "clear <name>", or a bare <name> to teleport.
		var argv = op switch
		{
			"set" => new[] { "set", name },
			"clear" => new[] { "clear", name },
			_ => new[] { name }
		};

		Chat.Current?.ExecuteCommandHost( WaypointCommandName, argv );

		if ( op is "set" or "clear" )
		{
			_ = RefreshAfterWrite();
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	// A player counts as "staff" (and gets their own rank-named group) only if their rank grants at
	// least one action in our catalog. Everyone else collapses into the "Players" bucket — so donor
	// ranks (VIP/EVIP) and members land under "Players", while Mod/Admin/Super Admin/Owner each group
	// by their portal rank name. Reads host-synced rank dictionaries, so it resolves client-side.
	private static bool IsStaff( long steamId )
		=> StaffMenuActions.All.Any( action => RankSystem.HasPermission( steamId, action.PermissionId ) );

	private static string RankName( long steamId )
	{
		var name = RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankName( steamId ) : "";
		name = SanitizeRankName( name );
		return string.IsNullOrWhiteSpace( name ) ? NonStaffGroup : name;
	}

	/// <summary>
	/// DXRP stores backend rank names with a leading colour/markup control character (e.g. the real
	/// "Owner" arrives as a 6-char string). That junk breaks both clean display and our reference-tier
	/// matching, so we strip control / format / private-use / surrogate code points and trim. Letters,
	/// digits, spaces and ordinary punctuation are preserved, so "Super Admin" stays intact.
	/// </summary>
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

	private static int RankOrder( long steamId )
		=> RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankOrder( steamId ) : 0;

	/// <summary>The player's real, sanitised rank name for the detail pane ("—" when they have none).</summary>
	private static string RealRole( long steamId )
	{
		var name = RankSystem.Instance.IsValid() ? SanitizeRankName( RankSystem.Instance.GetRankName( steamId ) ) : "";
		return string.IsNullOrWhiteSpace( name ) ? "—" : name;
	}

	/// <summary>The player's rank colour as a "#RRGGBB" string for the detail pane.</summary>
	private static string RankColorHex( long steamId )
	{
		var color = RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankColor( steamId ) : 0xFFFFFFu;
		return $"#{color & 0xFFFFFFu:X6}";
	}
#endif

	// --- Settings (owner customizations) ----------------------------------

	/// <summary>
	/// Owner-grant permission gating edits to the menu's owner customizations (currently the network
	/// website link). The Owner rank's <c>"*"</c> wildcard satisfies it automatically; an owner may also
	/// grant <c>lifepunchulx.settings.edit</c> to other ranks in the portal. UX gating only — the host
	/// (<see cref="StaffMenuBridgeService"/>) re-checks every write.
	/// </summary>
	public const string SettingsEditPermissionId = "lifepunchulx.settings.edit";

	/// <summary>True if the local viewer may edit owner settings. UX gating only; host re-checks the write.</summary>
	public static bool CanEditSettings() => CanView( SettingsEditPermissionId );

	/// <summary>Bumped whenever cached settings change, so the razor's <c>BuildHash</c> re-renders.</summary>
	public static int SettingsVersion { get; private set; }

#if LIFEPUNCH_LOCAL
	// Editor build: a live in-memory value so the input + click-to-copy work with no backend.
	private static string _websiteUrl = "https://lifepunch.co";
#else
	// dxrp.net build: host-synced from the token-scoped store via StaffMenuBridgeService (RefreshSettings).
	private static string _websiteUrl = string.Empty;
#endif

	/// <summary>The owner-configured network website URL ("" when unset). Editor: stub; server: host-synced.</summary>
	public static string WebsiteUrl => _websiteUrl;

	/// <summary>True when a website URL is configured — the network tag then becomes a click-to-copy link.</summary>
	public static bool HasWebsite => !string.IsNullOrWhiteSpace( _websiteUrl );

	/// <summary>
	/// Lowercase network slug shown in the header chip — derived from <see cref="WebsiteUrl"/>
	/// (e.g. <c>https://dxrp.net/</c> → <c>dxrp</c>). Neutral <c>network</c> when unset.
	/// </summary>
	public static string NetworkIdentifier => DeriveNetworkIdentifier( _websiteUrl );

	static string DeriveNetworkIdentifier( string url )
	{
		if ( string.IsNullOrWhiteSpace( url ) )
		{
			return "network";
		}

		var trimmed = url.Trim();
		if ( !trimmed.Contains( "://" ) )
		{
			trimmed = "https://" + trimmed;
		}

		var host = ParseUrlHost( trimmed );
		if ( string.IsNullOrWhiteSpace( host ) )
		{
			return "network";
		}

		host = host.ToLowerInvariant();
		if ( host.StartsWith( "www." ) )
		{
			host = host[4..];
		}

		var dot = host.IndexOf( '.' );
		var slug = dot > 0 ? host[..dot] : host;
		return string.IsNullOrWhiteSpace( slug ) ? "network" : slug;
	}

	static string ParseUrlHost( string url )
	{
		var schemeEnd = url.IndexOf( "://" );
		if ( schemeEnd < 0 )
			return null;

		var rest = url[(schemeEnd + 3)..];
		var pathStart = rest.IndexOfAny( new[] { '/', '?', '#', ':' } );
		var hostPort = pathStart >= 0 ? rest[..pathStart] : rest;
		if ( string.IsNullOrWhiteSpace( hostPort ) )
			return null;

		var colon = hostPort.LastIndexOf( ':' );
		if ( colon > 0 && !hostPort.StartsWith( "[" ) )
			hostPort = hostPort[..colon];

		return hostPort;
	}

	/// <summary>Ask the host for the current owner settings (mirrors <see cref="RefreshWaypoints"/>). No-op in editor.</summary>
	public static void RefreshSettings()
	{
#if !LIFEPUNCH_LOCAL
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.RequestSettingsHost();
		}
#endif
	}

	/// <summary>Save the network website URL (host re-checks the owner grant). An empty value clears it.</summary>
	public static void SaveWebsite( string url )
	{
#if LIFEPUNCH_LOCAL
		_websiteUrl = ( url ?? string.Empty ).Trim();
		SettingsVersion++;
		Log.Info( $"[lifepunchulx] (local stub) website set '{_websiteUrl}'" );
#else
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.SetWebsiteHost( url ?? string.Empty );
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Host→client callback (invoked by <see cref="StaffMenuBridgeService"/>): replace the
	/// cached website with the server's stored value and bump the version so the razor re-renders.
	/// </summary>
	internal static void OnSettingsReceived( string website )
	{
		_websiteUrl = website ?? string.Empty;
		SettingsVersion++;
	}
#endif

	// --- Jobs (force-set via DXRP /job — menu picker reads live gamemode config) ----

	/// <summary>Portal permission for force-setting jobs (<c>/job &lt;player&gt; &lt;job&gt;</c>).</summary>
	public const string JobManagePermissionId = "command.job.manage";

	/// <summary>
	/// Jobs from the active gamemode config — each server's custom job list, sorted for the Set Job picker.
	/// Force-set dispatches to DXRP's native <c>/job</c> command (host re-validates permission).
	/// </summary>
	public static IReadOnlyList<StaffJobOption> AssignableJobs()
	{
#if LIFEPUNCH_LOCAL
		return new List<StaffJobOption>
		{
			new( "Citizen", "Citizen", "#FFFFFF" ),
			new( "Police", "Police Officer", "#3498DB" ),
			new( "Mayor", "Mayor", "#E74C3C" ),
			new( "Gun Dealer", "Gun Dealer", "#F39C12" ),
			new( "Bitcoin Miner", "Bitcoin Miner", "#F1C40F" )
		};
#else
		return GameModeJobs.All
			.OrderBy( job => job.DisplayName() )
			.Select( job => new StaffJobOption(
				job.Name,
				job.DisplayName(),
				$"#{job.Color & 0xFFFFFFu:X6}" ) )
			.ToList();
#endif
	}

	// --- Dispatch ----------------------------------------------------------

	/// <summary>
	/// Dispatch an action to DXRP's backend. AdminSystem RPC where one exists, else the registered
	/// chat command. No-op-safe in the local build (logs instead).
	/// </summary>
	public static void Dispatch( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
#if LIFEPUNCH_LOCAL
		var argText = string.Join( ", ", args.Select( kv => $"{kv.Key}={kv.Value}" ) );
		Log.Info( $"[lifepunchulx] (local stub) {action.Key} target={targetSteamId} [{argText}]" );
#else
		switch ( action.Dispatch )
		{
			case StaffDispatchKind.AdminRpc:
				DispatchAdminRpc( action, targetSteamId, args );
				break;
			case StaffDispatchKind.ChatCommand:
				DispatchChatCommand( action, targetSteamId, args );
				break;
			case StaffDispatchKind.LocalToggle:
				DispatchLocalToggle( action );
				break;
			case StaffDispatchKind.GiveMoney:
				DispatchGiveMoney( targetSteamId, args );
				break;
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static void DispatchAdminRpc( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
		if ( !AdminSystem.Instance.IsValid() )
		{
			return;
		}

		switch ( action.DispatchTarget )
		{
			case "kick":
				var reason = args.TryGetValue( "reason", out var supplied ) && !string.IsNullOrWhiteSpace( supplied )
					? supplied
					: "Kicked by staff";
				AdminSystem.Instance.KickPlayerHost( targetSteamId, reason );
				break;
			case "screenshot":
				AdminSystem.Instance.ForceScreenshotHost( targetSteamId );
				break;
		}
	}

	/// <summary>
	/// Client-only affordances DXRP exposes via keybind rather than a command. Noclip is a synced
	/// MoveMode toggled on the local player's own controller (same path the "Noclip" bind drives),
	/// gated by <c>ability.noclip</c> — we re-use the engine mechanism rather than reimplementing it.
	/// </summary>
	private static void DispatchLocalToggle( StaffAction action )
	{
		switch ( action.DispatchTarget )
		{
			case "noclip":
				if ( !Player.Local.IsValid() || !Player.Local.Controller.IsValid() )
				{
					return;
				}

				var noclip = Player.Local.Controller.Components.Get<MoveModeNoClip>();
				if ( noclip.IsValid() )
				{
					noclip.IsNoclipping = !noclip.IsNoclipping;
				}

				break;
		}
	}

	/// <summary>
	/// Hand a currency grant to the host. The client parses its own form here, but every value is
	/// re-validated host-side -- this parse is for the UI's benefit, never for authority.
	/// </summary>
	private static void DispatchGiveMoney( long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
#if !LIFEPUNCH_LOCAL
		args.TryGetValue( "amount", out var rawAmount );
		args.TryGetValue( "reason", out var reason );
		args.TryGetValue( "destination", out var destination );

		if ( !uint.TryParse( rawAmount?.Trim(), out var amount ) || amount == 0 )
		{
			return;
		}

		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.GiveMoneyHost(
				targetSteamId, amount, destination == "bank", reason ?? "" );
		}
#endif
	}

	private static void DispatchChatCommand( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
		var argv = new List<string>();

		// Target identifier first, as a Steam ID string (CommandHelper.ResolvePlayer accepts it,
		// avoiding name-collision ambiguity). Self-only commands carry no target.
		if ( action.TargetMode == StaffActionTarget.OtherPlayer )
		{
			argv.Add( targetSteamId.ToString() );
		}

		foreach ( var arg in action.Args )
		{
			if ( args.TryGetValue( arg.Name, out var value ) && !string.IsNullOrWhiteSpace( value ) )
			{
				argv.Add( value );
			}
		}

		Chat.Current?.ExecuteCommandHost( action.DispatchTarget, argv.ToArray() );
	}
#endif
}

#if !LIFEPUNCH_LOCAL
/// <summary>
/// Registers <c>/lifepunchulx</c>, <c>/menu</c>, and <c>/ulx</c> as in-game chat commands.
/// <see cref="ExecuteLocal"/> opens the menu client-side and consumes the command, so it never round-trips to the host.
/// Discovered automatically via TypeLibrary on the dxrp.net gamemode build.
/// </summary>
public sealed class StaffMenuChatCommand : ICommand
{
	public string Command => "lifepunchulx";
	public string[] Aliases => ["menu", "ulx"];
	public string Help => "Open the staff admin menu.";
	public bool IsUsableWhileDead => true;

	public bool ExecuteLocal( string[] args, string raw )
	{
		StaffMenuHost.Toggle();
		return true;
	}

	public bool ExecuteHost( Player caller, string[] args, string raw ) => true;
}
#endif
