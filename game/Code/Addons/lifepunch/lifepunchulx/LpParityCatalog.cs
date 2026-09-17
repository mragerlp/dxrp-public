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

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Classifies the DXRP portal capabilities by their available game interfaces.
/// Reconciled by hand on 2026-08-29 against all 38 Portal-category rows in
/// <c>game/Code/Api/Enums/Permission.cs</c>, in source order. That enum is mirrored from
/// the backend; <c>command.xray</c> belongs to Commands and is outside this count.
/// This catalogue is maintained manually and does not detect newly added backend scopes.
/// Billing, monetization and addon-purchase scopes are listed for completeness only;
/// <see cref="LpParityCatalogRow.PrincipalDomain"/> marks operations reserved for the owner.
/// </summary>
public static class LpParityCatalog
{
	/// <summary>
	/// The Portal-scope count recorded by the manual reconciliation on 2026-08-29.
	/// This is a recorded baseline, not an automatic check.
	/// </summary>
	public const int ScopeCount = 38;

	private static LpParityCatalogRow Read( string scope, string capability, string sensor, bool shipped = false )
		=> new( scope, capability, LpParityClass.InGameRead, shipped, false, sensor );

	private static LpParityCatalogRow Act( string scope, string capability, string sensor, bool shipped = false )
		=> new( scope, capability, LpParityClass.InGameAction, shipped, false, sensor );

	private static LpParityCatalogRow Portal( string scope, string capability, string sensor, bool principal = false )
		=> new( scope, capability, LpParityClass.PortalOnly, false, principal, sensor );

	/// <summary>
	/// Capabilities in mirrored <c>Permission</c> enum order, for direct comparison with that source.
	/// </summary>
	public static readonly IReadOnlyList<LpParityCatalogRow> All = new List<LpParityCatalogRow>
	{
		Portal( "portal.access", "Access the web portal",
			"Permission.cs — a gate on the browser, not a capability; the in-game menu opens for all and gates per row" ),

		Read( "portal.server.view", "View this server's identity and health",
			"ServerApiLink.TenantId/ServerId/RulesetId [Sync]; HostStatsTracker.GetStats (host); the multi-server LIST has no endpoint" ),

		Portal( "portal.server.edit", "Manage server settings",
			"UpdateServerInfoActionHandler is inbound only — no writer exists in ServerApiClient" ),

		Read( "portal.rank.view", "View ranks and what they grant",
			"RankSystem.GetPlayerRank returns a full RankDto incl. Permissions; the rank dictionary itself is private, so coverage is ranks held by online players" ),

		Portal( "portal.rank.edit", "Create and edit ranks",
			"no rank writer in ServerApiClient" ),

		Portal( "portal.map.view", "View map rotation",
			"only the current MapSboxIdentifier arrives at init and is passed straight to MapFitter.Fit; no rotation type exists" ),

		Portal( "portal.map.edit", "Manage map rotation",
			"as portal.map.view — nothing retained, nothing writable" ),

		Portal( "portal.ruleset.view", "View server rulesets",
			"only the bare RulesetId GUID reaches the game; there is no RulesetDto anywhere in Api/Dtos" ),

		Portal( "portal.ruleset.edit", "Manage rulesets",
			"as portal.ruleset.view" ),

		Read( "portal.faction.view", "View factions, roles and members",
			"ServerApiClient.GetAllFactions/GetFaction (host); FactionSystem already models FactionInfo/RoleInfo/MemberInfo" ),

		Read( "portal.sanction.view", "View a player's sanctions",
			"PlayerSanctionHistorySystem via StaffMenuHost.GetSanctions", shipped: true ),

		Portal( "portal.sanction.issue", "Issue sanctions from the portal",
			"portal.sanction.issue is distinct from the in-game player.* command permissions; no outbound writer is authorized by this portal scope" ),

		Portal( "portal.sanction.pardon", "Pardon or expire sanctions",
			"UpdateSanctionFlagsDto is defined and referenced NOWHERE in game code — no pardon writer exists" ),

		Read( "portal.audit.view", "View audit events",
			"SPLIT: LocalAuditStore is this host session's 500-row ring (shipped). The historical portal GET /v1/audit/events is unbound", shipped: true ),

		Portal( "portal.network.edit", "Manage tenant network settings",
			"no seam — the scope string occurs only in Permission.cs" ),

		Portal( "portal.announcement.edit", "Manage portal-persisted announcements",
			"the in-game /staffannounce command uses a separate command permission; this portal scope has no outbound writer" ),

		Read( "portal.items.view", "View items and inventories",
			"PlayerApiClient.GetInventory (client, own) and ServerApiClient.GetPlayerInventory (host, anyone); GetItemDefinition on both" ),

		Portal( "portal.items.manage", "Manage portal item definitions",
			"player inventory mutation is governed separately by inventory.manage; item-definition CRUD has no outbound writer" ),

		Read( "portal.addon.view", "View installed addons and revisions",
			"GameModeDto.Addons is [Sync]'d to clients with AddonName, AddonDescription, RevisionNumber, SboxVersion and Contents" ),

		Portal( "portal.addon.edit", "Manage addons and revisions",
			"inbound update_game_mode only" ),

		Portal( "portal.addon.entitlements.manage", "Grant addon access to other networks",
			"cross-tenant; no seam" ),

		Read( "portal.gamemode.view", "View the live game mode",
			"the whole GameModeDto is [Sync]'d: jobs, job groups, equipment, entities, market items, starting balance, building allowlists" ),

		Portal( "portal.gamemode.edit", "Manage game modes",
			"UpdateGameModeActionHandler is inbound; no outbound writer" ),

		Act( "portal.server.snapshot", "Save a server snapshot",
			"ServerApiClient.SaveSnapshot/GetSnapshot; SnapshotSystem.SaveSnapshot and SnapshotManualHost are public" ),

		Read( "portal.players.view", "View the player list",
			"StaffMenuHost.OnlinePlayers", shipped: true ),

		Read( "portal.players.view.incognito", "See incognito players in the list",
			"HasStatus(Constants.IncognitoStatus) — already an in-game scope by its own description" ),

		Read( "portal.player.view", "View a player's details",
			"StaffMenuHost.GetPlayerDetail — job, wallet, bank, health, armour, kills, deaths, playtime, rank", shipped: true ),

		Portal( "portal.player.view.alt", "View a player's alternate accounts",
			"hwid is sent OUTBOUND on the player pulse and never returned — PlayerPulseResponseDto has one field, Permitted" ),

		Portal( "portal.player.notes.manage", "Internal staff notes on players",
			"no seam — the scope string occurs only in Permission.cs" ),

		// RankSystem.SetPlayerRanks changes synced state without persisting it to the portal.
		// A later rank_snapshot or restart replaces it, so rank assignment remains portal-only.
		Portal( "portal.rank.assign", "Assign ranks to players",
			"HAZARD: RankSystem.SetPlayerRanks mutates local synced state only; no portal writeback, so an in-game assign silently reverts" ),

		Read( "portal.store.manage", "Read and write the tenant data store",
			"ServerApiClient.ListStore/GetStore/SetStore/DeleteStore — already in production use here for waypoints and owner settings", shipped: true ),

		Portal( "portal.backups.manage", "View and restore network backups",
			"backup_restored is a notification the portal sends after the fact; listing and restoring have no writer" ),

		Portal( "portal.apikeys.manage", "Create, list and revoke API keys",
			"no seam — occurs only in Permission.cs; also a credential surface that does not belong in a game HUD" ),

		Portal( "portal.billing.manage", "Payment onboarding and billing status",
			"no billing seam of any kind exists in this tree — the string occurs only in Permission.cs", principal: true ),

		Portal( "portal.addon.purchase", "Purchase marketplace addons",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.products.manage", "Create, edit and delete products",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.coupons.manage", "Create, edit and delete coupons",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.view", "Monetization breakdown and sales history",
			"no seam; billing-adjacent", principal: true )
	};

	/// <summary>
	/// Capabilities with a game interface, in whole or in part.
	/// </summary>
	public static IEnumerable<LpParityCatalogRow> Feasible =>
		All.Where( row => row.Class != LpParityClass.PortalOnly );

	/// <summary>
	/// Capabilities with no game interface.
	/// </summary>
	public static IEnumerable<LpParityCatalogRow> PortalOnly =>
		All.Where( row => row.Class == LpParityClass.PortalOnly );

	/// <summary>Rows already live in this addon.</summary>
	public static IEnumerable<LpParityCatalogRow> Shipped =>
		All.Where( row => row.Shipped );

	/// <summary>Short display label for a class, for a pill or column header.</summary>
	public static string ClassLabel( LpParityClass value ) => value switch
	{
		LpParityClass.InGameRead => "In-game read",
		LpParityClass.InGameAction => "In-game action",
		_ => "Portal only"
	};

	/// <summary>CSS tone suffix for a class pill, mirroring the audit pill convention already in the menu.</summary>
	public static string ClassTone( LpParityClass value ) => value switch
	{
		LpParityClass.InGameRead => "parity-pill-green",
		LpParityClass.InGameAction => "parity-pill-blue",
		_ => "parity-pill-slate"
	};
}
