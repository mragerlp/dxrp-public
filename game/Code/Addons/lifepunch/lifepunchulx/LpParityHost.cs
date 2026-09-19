// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Read-only views over synced config, server identity and online player data.
/// No socket, RPC or ServerApiClient request is issued from this projection.
/// <c>GameNetworkManager.ServerName</c> and <c>MaxPlayers</c> are unsynced host fields;
/// clients must receive HostOnly availability rather than their compile-time defaults.
/// Link state uses synced <c>TenantId</c>, following PauseMenu.razor and DashboardTabMenuSection.razor.
/// <c>HasAuthorizationKey</c> reads the local authorize convar and cannot describe a remote host.
/// Do not sample <c>HostStatsTracker.GetStats()</c> here: it resets the minimum FPS used by the portal pulse.
/// </summary>
#if LIFEPUNCH_PACKAGE
internal static class LpParityHost
{
	public const string RankViewPermissionId = "portal.rank.view";

	/// <summary>
	/// The current published parent predates the synced rank DTO surface used by the workbench.
	/// Fail closed instead of substituting fixtures or claiming that an empty list is a tenant roster.
	/// </summary>
	public static bool RanksAreAvailable => false;
	public static bool CanViewRanks() => StaffMenuHost.CanView( RankViewPermissionId );
	public static bool RanksAreComplete => false;
	public const string RanksScopeNote =
		"Rank observation is unavailable from the currently published parent package. No tenant-rank claim is made.";
	public static IReadOnlyList<LpParityRank> ObservedRanks() => System.Array.Empty<LpParityRank>();
}
#else
internal static class LpParityHost
{
	/// <summary>Positive code-string ID for this slice. Exists nowhere else in the tree.</summary>
	public const string ParityMark = "LP_ULX_PARITY_READ_20260828";

	// Portal scopes this surface mirrors. Hardcoded id strings for the same reason StaffMenuHost
	// hardcodes AuditPermissionId: the define-free editor build cannot see the Dxura enum.
	public const string ServerViewPermissionId = "portal.server.view";
	public const string GameModeViewPermissionId = "portal.gamemode.view";
	public const string RankViewPermissionId = "portal.rank.view";
	public const string AddonViewPermissionId = "portal.addon.view";

	// REACHABILITY, sensed 2026-09-18 (R9) and recorded so the next reader is not misled: of the four
	// gates below, only CanViewRanks() is reachable. CanViewServer() and CanViewGameMode() have NO callers
	// anywhere in game/. CanViewAddons() has exactly one caller -- GameModeOverview() -- and that method
	// itself has no callers, so the gate is transitively dead. ServerStatus() and GameModeOverview() are
	// likewise never invoked. They are LEFT AS THEY ARE on purpose: under R-2639-A the fix for a gate that
	// hides a section is to stop it hiding, and a gate that gates nothing hides nothing. Wiring these up so
	// they could then be inverted would ADD gating that does not exist today, which is the opposite of the
	// ruling. If these projections are ever mounted, they must render for every rank from the start.

	/// <summary>UX gating only — mirrors <c>portal.server.view</c>. Currently has no callers.</summary>
	public static bool CanViewServer() => StaffMenuHost.CanView( ServerViewPermissionId );

	/// <summary>UX gating only — mirrors <c>portal.gamemode.view</c>. Currently has no callers.</summary>
	public static bool CanViewGameMode() => StaffMenuHost.CanView( GameModeViewPermissionId );

	/// <summary>
	/// UX gating only — mirrors <c>portal.rank.view</c>. The one reachable gate of the four: read by
	/// StaffObserve.razor, where it decides whether rank ROWS are supplied, never whether the view renders.
	/// </summary>
	public static bool CanViewRanks() => StaffMenuHost.CanView( RankViewPermissionId );

	/// <summary>
	/// UX gating only — mirrors <c>portal.addon.view</c>. Reached only from <c>GameModeOverview()</c>,
	/// which has no callers, so this gate is transitively dead. See the reachability note above.
	/// </summary>
	public static bool CanViewAddons() => StaffMenuHost.CanView( AddonViewPermissionId );

	private const string HostOnlyText = "host only";
	private const string UnlinkedText = "not linked to a portal network";
	private const string PendingText = "waiting for the portal";
	private const string NoneText = "—";

	private static LpParityField Live( string label, string? value )
		=> new( label, string.IsNullOrWhiteSpace( value ) ? NoneText : value, LpParityAvailability.Live );

	private static LpParityField HostOnly( string label, string why = HostOnlyText )
		=> new( label, why, LpParityAvailability.HostOnly );

	private static LpParityField Unlinked( string label )
		=> new( label, UnlinkedText, LpParityAvailability.Unlinked );

	private static LpParityField Pending( string label )
		=> new( label, PendingText, LpParityAvailability.Pending );

	private static LpParitySection Section( string title, params LpParityField[] fields )
		=> new( title, fields );

	// --- Link state --------------------------------------------------------

	/// <summary>
	/// Whether this server is bound to a portal network, read the client-safe way (synced tenant id)
	/// rather than via the host-local <c>authorize</c> ConVar. See the class remarks.
	/// </summary>
	public static bool IsLinked
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => !string.IsNullOrEmpty( ServerApiLink.Current?.TenantId );
#endif
	}

	/// <summary>True once the portal has answered and config overrides have been applied.</summary>
	public static bool IsReady
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => Config.Current?.IsReady ?? false;
#endif
	}

	/// <summary>True when this viewer is the authoritative host and may see host-only values.</summary>
	public static bool IsHostViewer
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => Networking.IsHost;
#endif
	}

	/// <summary>
	/// One-line state for a header chip: what this viewer can expect the parity views to contain.
	/// </summary>
	public static LpParityAvailability LinkState
	{
		get
		{
			if ( !IsLinked )
			{
				return LpParityAvailability.Unlinked;
			}

			return IsReady ? LpParityAvailability.Live : LpParityAvailability.Pending;
		}
	}

	/// <summary>
	/// Change signal for parity views: link state, readiness, roster size and game mode identity.
	/// </summary>
	public static int ParityVersion
	{
		get
		{
			var hash = IsLinked ? 17 : 3;
			hash = hash * 31 + ( IsReady ? 1 : 0 );
			hash = hash * 31 + PlayersOnline;
			hash = hash * 31 + GameModeName.GetHashCode();
			return hash;
		}
	}

	private static int PlayersOnline
	{
#if LIFEPUNCH_LOCAL
		get => 6;
#else
		get => GameUtils.Players.Count( player => player.IsValid() );
#endif
	}

	private static string GameModeName
	{
#if LIFEPUNCH_LOCAL
		get => "LIFEPUNCH RP (editor stub)";
#else
		get => Config.Current?.GameMode?.Name ?? "";
#endif
	}

	// --- Server status view (portal "Servers" page, this server only) ------

	/// <summary>
	/// Identity, link state and available health fields for this server.
	/// Unlinked servers retain the field list with <see cref="LpParityAvailability.Unlinked"/> availability.
	/// </summary>
	public static IReadOnlyList<LpParitySection> ServerStatus()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParitySection>
		{
			Section( "Identity",
				Live( "Server", "LIFEPUNCH #1 (editor stub)" ),
				Live( "Tenant", "11111111-1111-1111-1111-111111111111" ),
				Live( "Server id", "00000000-0000-4000-8000-000000000001" ),
				Live( "Ruleset", "00000000-0000-4000-8000-0000000000ff" ) ),
			Section( "Link",
				Live( "Portal", "linked · production" ),
				Live( "Config", "ready" ),
				Live( "Pulse interval", "10s" ) ),
			Section( "Population",
				Live( "Players online", "6 / 128" ),
				Live( "Rank whitelist", "none" ) ),
			Section( "Host performance",
				HostOnly( "Reported to portal", "FPS and bandwidth are published each pulse; not sampled here" ) )
		};
#else
		var link = ServerApiLink.Current;
		var linked = IsLinked;

		var identity = new List<LpParityField>();
		if ( linked )
		{
			identity.Add( IsHostViewer
				? Live( "Server", GameNetworkManager.ServerName )
				: HostOnly( "Server" ) );
			identity.Add( Live( "Tenant", link?.TenantId ) );
			identity.Add( Live( "Server id", link is null || link.ServerId == System.Guid.Empty
				? ""
				: link.ServerId.ToString() ) );
			identity.Add( Live( "Ruleset", link?.RulesetId is null ? "" : link.RulesetId.Value.ToString() ) );
			identity.Add( Live( "Network",
				string.Equals( link?.TenantId, Constants.OfficialTenantId, System.StringComparison.OrdinalIgnoreCase )
					? "official"
					: "community" ) );
		}
		else
		{
			identity.Add( Unlinked( "Server" ) );
			identity.Add( Unlinked( "Tenant" ) );
		}

		var linkFields = new List<LpParityField>
		{
			linked
				? ( IsHostViewer ? Live( "Portal", $"linked · {EndpointLabel()}" ) : Live( "Portal", "linked" ) )
				: Unlinked( "Portal" ),
			linked
				? ( IsReady ? Live( "Config", "ready" ) : Pending( "Config" ) )
				: Unlinked( "Config" ),
			Live( "Pulse interval", $"{Constants.ApiServerSyncInterval}s" ),
			IsHostViewer
				? Live( "Portal address", Constants.BaseWebsiteUrl )
				: HostOnly( "Portal address" ),
			Live( "Build", Application.Version )
		};

		var population = new List<LpParityField>
		{
			IsHostViewer
				? Live( "Players online", $"{PlayersOnline} / {GameNetworkManager.MaxPlayers}" )
				: Live( "Players online", PlayersOnline.ToString() ),
			IsHostViewer
				? Live( "Max players", GameNetworkManager.MaxPlayers.ToString() )
				: HostOnly( "Max players" ),
			IsHostViewer
				? Live( "Rank whitelist", GameNetworkManager.WhitelistRankIds.Length == 0
					? "none"
					: $"{GameNetworkManager.WhitelistRankIds.Length} rank(s)" )
				: HostOnly( "Rank whitelist" )
		};

		// GetStats() resets the minimum FPS; reading it here would alter the portal pulse metric.
		var performance = new List<LpParityField>
		{
			HostOnly( "Reported to portal", "FPS and bandwidth are published each pulse; not sampled here" ),
			IsHostViewer
				? Live( "Audit ring", $"{LocalAuditStore.SnapshotNewestFirst().Count} / {LocalAuditStore.Capacity} rows this session" )
				: HostOnly( "Audit ring" )
		};

		return new List<LpParitySection>
		{
			new( "Identity", identity ),
			new( "Link", linkFields ),
			new( "Population", population ),
			new( "Host performance", performance )
		};
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static string EndpointLabel() => ServerApiLink.Endpoint switch
	{
		ApiEndpoint.Local => "local",
		ApiEndpoint.Staging => "staging",
		_ => "production"
	};
#endif

	// --- Game mode view (portal "Game Modes" + "Addons" pages) -------------

	/// <summary>
	/// Game mode data read directly from the synced <c>GameModeDto</c>, including
	/// jobs, equipment, entities, market items and installed addon revisions.
	/// </summary>
	public static IReadOnlyList<LpParitySection> GameModeOverview()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParitySection>
		{
			Section( "Game mode",
				Live( "Name", "LIFEPUNCH RP (editor stub)" ),
				Live( "Visibility", "Public" ),
				Live( "Starting balance", "$2,500" ) ),
			Section( "Content",
				Live( "Jobs", "24 in 5 groups" ),
				Live( "Equipment", "61" ),
				Live( "Entities", "38" ),
				Live( "Market items", "77" ) ),
			Section( "Addons",
				Live( "lifepunchulx", "rev 12 · sbox 1.0" ),
				Live( "serverhub", "rev 4 · sbox 1.0" ) )
		};
#else
		var mode = Config.Current?.GameMode;
		if ( mode is null || string.IsNullOrWhiteSpace( mode.Name ) || mode.Name == "None" )
		{
			// Distinguish an unlinked server from one still waiting for portal initialization.
			var why = IsLinked ? Pending( "Game mode" ) : Unlinked( "Game mode" );
			return new List<LpParitySection> { new( "Game mode", new List<LpParityField> { why } ) };
		}

		var summary = new List<LpParityField>
		{
			Live( "Name", mode.Name ),
			Live( "Description", mode.Description ),
			Live( "Visibility", mode.Visibility.ToString() ),
			Live( "Starting balance", mode.StartingBalance.ToString() ),
			Live( "Last modified", mode.LastModified.ToLocalTime().ToString( "yyyy-MM-dd HH:mm" ) )
		};

		// Read straight off the DTO rather than through GameModeEquipments.All / GameModeMarketItems.All:
		// those two helpers dereference Config.Current.GameMode without a null guard, so they throw in
		// exactly the pre-init window this view is most likely to be opened in.
		var jobs = mode.Jobs?.Count ?? 0;
		var groups = mode.JobGroups?.Count ?? 0;
		var content = new List<LpParityField>
		{
			Live( "Jobs", groups > 0 ? $"{jobs} in {groups} groups" : jobs.ToString() ),
			Live( "Equipment", ( mode.Equipments?.Count ?? 0 ).ToString() ),
			Live( "Entities", ( mode.Entities?.Count ?? 0 ).ToString() ),
			Live( "Market items", ( mode.MarketItems?.Count ?? 0 ).ToString() ),
			Live( "Building props", DescribeAllowlist( mode.BuildingProps?.Count ?? 0 ) ),
			Live( "Building materials", DescribeAllowlist( mode.BuildingMaterials?.Count ?? 0 ) )
		};

		var sections = new List<LpParitySection>
		{
			new( "Game mode", summary ),
			new( "Content", content )
		};

		if ( CanViewAddons() )
		{
			sections.Add( new LpParitySection( "Addons", AddonFields( mode ) ) );
		}

		return sections;
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>An empty allowlist is not "zero props" — it means the legacy fallback rule applies.</summary>
	private static string DescribeAllowlist( int count )
		=> count == 0 ? "no explicit list · legacy fallback applies" : $"{count} explicitly allowed";

	private static IReadOnlyList<LpParityField> AddonFields( GameModeDto mode )
	{
		var addons = mode.Addons;
		if ( addons is null || addons.Count == 0 )
		{
			return new List<LpParityField> { Live( "Installed", "none" ) };
		}

		var fields = new List<LpParityField>( addons.Count );
		foreach ( var addon in addons.OrderBy( a => a.AddonName ?? "", System.StringComparer.OrdinalIgnoreCase ) )
		{
			var name = string.IsNullOrWhiteSpace( addon.AddonName ) ? addon.AddonId.ToString() : addon.AddonName;
			var detail = $"rev {addon.RevisionNumber}";
			if ( !string.IsNullOrWhiteSpace( addon.SboxVersion ) )
			{
				detail += $" · sbox {addon.SboxVersion}";
			}

			var contents = addon.Contents?.Count ?? 0;
			if ( contents > 0 )
			{
				detail += $" · {contents} content item(s)";
			}

			fields.Add( Live( name, detail ) );
		}

		return fields;
	}
#endif

	// --- Ranks view (portal "Ranks" page, observed subset) -----------------

	/// <summary>
	/// Distinct display ranks returned by <c>GetPlayerRank(steamId)</c> for online players,
	/// including each rank's grants. This projection does not enumerate the full tenant roster;
	/// see <see cref="RanksAreComplete"/>.
	/// </summary>
	public static IReadOnlyList<LpParityRank> ObservedRanks()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParityRank>
		{
			new( "Owner", 69, "#E74C3C", false, 1, 1, true ),
			new( "Super Admin", 10, "#3498DB", false, 74, 1, false ),
			new( "Admin", 5, "#2ECC71", false, 41, 1, false ),
			new( "Mod", 4, "#9B59B6", false, 18, 1, false ),
			new( "Member", 0, "#FFFFFF", true, 6, 2, false )
		};
#else
		var system = RankSystem.Instance;
		if ( !system.IsValid() )
		{
			return System.Array.Empty<LpParityRank>();
		}

		var byName = new Dictionary<string, LpParityRank>( System.StringComparer.OrdinalIgnoreCase );

		foreach ( var player in GameUtils.Players.Where( p => p.IsValid() ) )
		{
			var rank = system.GetPlayerRank( player.SteamId );
			if ( rank is null )
			{
				continue;
			}

			var name = SanitizeRankName( rank.Name );
			if ( string.IsNullOrWhiteSpace( name ) )
			{
				continue;
			}

			if ( byName.TryGetValue( name, out var existing ) )
			{
				byName[name] = existing with { HoldersOnline = existing.HoldersOnline + 1 };
				continue;
			}

			var permissions = rank.Permissions ?? new List<string>();
			byName[name] = new LpParityRank(
				name,
				rank.Order,
				$"#{rank.Color & 0xFFFFFFu:X6}",
				rank.IsDefault,
				permissions.Count,
				1,
				permissions.Contains( "*" ) );
		}

		return byName.Values
			.OrderByDescending( r => r.Order )
			.ThenBy( r => r.Name, System.StringComparer.OrdinalIgnoreCase )
			.ToList();
#endif
	}

	/// <summary>
	/// False because this projection includes only display ranks observed on online players.
	/// </summary>
	public static bool RanksAreComplete => false;
	public static bool RanksAreAvailable => true;

	/// <summary>The sentence a parity Ranks panel should show under its heading.</summary>
	public const string RanksScopeNote =
		"Display ranks observed for players currently online. Secondary assignments and the full tenant roster are not included in this view.";

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Strip control and markup characters from backend rank names.
	/// Keep this normalization consistent with <c>StaffMenuHost.SanitizeRankName</c>.
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
#endif
}
#endif
