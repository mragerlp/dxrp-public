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

using System;
using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>Who an action operates on.</summary>
public enum StaffActionTarget
{
	/// <summary>Applies to the caller (no target picker needed).</summary>
	SelfOnly,

	/// <summary>Requires a selected, targetable player.</summary>
	OtherPlayer,

	/// <summary>Affects everyone / the server at once (e.g. teleport-all).</summary>
	Global
}

/// <summary>
/// Which DXRP backend an action dispatches to. The menu prefers a direct <c>AdminSystem</c> host
/// RPC where one exists, else routes through the registered chat <c>ICommand</c> via
/// <c>Chat.ExecuteCommandHost</c>. <see cref="LocalToggle"/> handles client-only affordances that
/// DXRP exposes via keybind rather than a command (e.g. noclip move-mode). Every path is re-checked
/// host-side; this catalog never carries authority.
/// </summary>
public enum StaffDispatchKind
{
	AdminRpc,
	ChatCommand,
	LocalToggle,

	/// <summary>
	/// Currency grant. Its own kind rather than a chat command because DXRP exposes none: the
	/// client sends a request and the HOST validates the caller's permission and executes.
	/// </summary>
	GiveMoney
}

/// <summary>How the UI should render/validate an argument input.</summary>
public enum StaffArgKind
{
	Text,
	Duration,
	Number,
	Job,

	/// <summary>Cash or bank. Rendered as a two-chip picker, not free text.</summary>
	Destination
}

/// <summary>
/// A user-supplied argument, mirroring the DXRP chat-command grammar
/// (e.g. <c>/ban &lt;player&gt; &lt;duration&gt; &lt;reason&gt;</c>). Arg order in
/// <see cref="StaffAction.Args"/> must match the command's positional argument order.
/// </summary>
public sealed record StaffActionArg(
	string Name,
	string Label,
	StaffArgKind Kind = StaffArgKind.Text,
	bool Required = true,
	string? Placeholder = null );

/// <summary>
/// One entry in the staff command catalog (ULX-style, built clean as data). References DXRP only by
/// stable permission Id string + dispatch target name — never a Dxura type — so it compiles in the
/// standalone editor build. Permission Ids reconcile 1:1 with the live DXRP portal and
/// <c>admin-panel/permissions/*.json</c>.
/// </summary>
public sealed record StaffAction(
	string Key,
	string Label,
	string Category,
	string PermissionId,
	StaffDispatchKind Dispatch,
	string DispatchTarget,
	StaffActionTarget TargetMode,
	IReadOnlyList<StaffActionArg> Args,
	string Icon = "bolt",
	string Tooltip = "" );

/// <summary>
/// The LifePunch staff command catalog + categories, mirroring the DXRP portal permission taxonomy
/// (Moderation / Commands / Ability). Growing the menu = adding rows here.
/// Chat-only commands stay out of <see cref="All"/>; job force-set is menu-only via Set Job below
/// (still dispatches to DXRP <c>/job</c> — portal <c>command.job.manage</c>).
/// Catalog reconciled against the live portal Super Admin permission set (2026-06).
/// </summary>
public static class StaffMenuActions
{
	public const string CategoryModeration = "Moderation";
	public const string CategoryCommands = "Commands";
	public const string CategoryAbility = "Ability";

	/// <summary>Category display order for the menu tabs.</summary>
	public static readonly IReadOnlyList<string> Categories = new[]
	{
		CategoryModeration, CategoryCommands, CategoryAbility
	};

	private static readonly StaffActionArg[] NoArgs = Array.Empty<StaffActionArg>();

	private static StaffActionArg Reason( bool required ) =>
		new( "reason", "Reason", StaffArgKind.Text, required, "Reason" );

	private static StaffActionArg Duration( bool required ) =>
		new( "duration", "Duration", StaffArgKind.Duration, required, "e.g. 1h, 1d, 7d, perm" );

	public static readonly IReadOnlyList<StaffAction> All = new List<StaffAction>
	{
		// ---- Moderation ----
		new( "kick", "Kick", CategoryModeration, "player.kick",
			StaffDispatchKind.AdminRpc, "kick", StaffActionTarget.OtherPlayer,
			new[] { Reason( required: false ) }, "logout", "Kick player" ),

		new( "ban", "Ban", CategoryModeration, "player.ban",
			StaffDispatchKind.ChatCommand, "ban", StaffActionTarget.OtherPlayer,
			new[] { Duration( required: true ), Reason( required: true ) }, "gavel", "Ban player" ),

		new( "jail", "Jail", CategoryModeration, "player.jail",
			StaffDispatchKind.ChatCommand, "jail", StaffActionTarget.OtherPlayer,
			new[] { Duration( required: true ), Reason( required: true ) }, "lock", "Jail player" ),

		new( "gag", "Gag", CategoryModeration, "player.gag",
			StaffDispatchKind.ChatCommand, "gag", StaffActionTarget.OtherPlayer,
			new[] { Duration( required: true ), Reason( required: true ) }, "mic_off", "Mute player's voice" ),

		new( "warn", "Warn", CategoryModeration, "player.warn",
			StaffDispatchKind.ChatCommand, "warn", StaffActionTarget.OtherPlayer,
			new[] { Reason( required: true ) }, "warning", "Warn player" ),

		new( "spectate", "Spectate", CategoryModeration, "player.spectate",
			StaffDispatchKind.ChatCommand, "spectate", StaffActionTarget.OtherPlayer,
			NoArgs, "visibility", "Spectate player" ),

		new( "screenshot", "Screenshot", CategoryModeration, "player.screenshot",
			StaffDispatchKind.AdminRpc, "screenshot", StaffActionTarget.OtherPlayer,
			NoArgs, "photo_camera", "Capture player's screen" ),

		// ---- Commands ----
		new( "god", "God Mode", CategoryCommands, "command.god",
			StaffDispatchKind.ChatCommand, "god", StaffActionTarget.SelfOnly, NoArgs, "shield", "Toggle god mode" ),

		new( "cloak", "Cloak", CategoryCommands, "command.cloak",
			StaffDispatchKind.ChatCommand, "cloak", StaffActionTarget.SelfOnly, NoArgs, "visibility_off", "Go invisible" ),

		new( "incognito", "Incognito", CategoryCommands, "command.incognito",
			StaffDispatchKind.ChatCommand, "incognito", StaffActionTarget.SelfOnly, NoArgs, "person_off", "Hide from player list" ),

		new( "fakedisconnect", "Fake Disconnect", CategoryCommands, "command.fakedisconnect",
			StaffDispatchKind.ChatCommand, "fakedisconnect", StaffActionTarget.SelfOnly, NoArgs, "wifi_off", "Fake a disconnect" ),

		new( "freeze", "Freeze", CategoryCommands, "command.freeze",
			StaffDispatchKind.ChatCommand, "freeze", StaffActionTarget.OtherPlayer, NoArgs, "ac_unit", "Freeze player in place" ),

		new( "sethealth", "Set Health", CategoryCommands, "command.sethealth",
			StaffDispatchKind.ChatCommand, "sethealth", StaffActionTarget.OtherPlayer,
			new[] { new StaffActionArg( "amount", "Health", StaffArgKind.Number, true, "e.g. 100" ) }, "favorite", "Set player's health" ),

		new( "setjob", "Set Job", CategoryCommands, "command.job.manage",
			StaffDispatchKind.ChatCommand, "job", StaffActionTarget.OtherPlayer,
			new[] { new StaffActionArg( "job", "Job", StaffArgKind.Job, true, "Search or pick a job" ) },
			"work", "Force-set the player's job (gamemode job list)" ),

		new( "arrest", "Arrest", CategoryCommands, "command.arrest",
			StaffDispatchKind.ChatCommand, "arrest", StaffActionTarget.OtherPlayer,
			new[] { Duration( required: false ) }, "local_police", "Arrest player" ),

		new( "unarrest", "Unarrest", CategoryCommands, "command.unarrest",
			StaffDispatchKind.ChatCommand, "unarrest", StaffActionTarget.OtherPlayer, NoArgs, "no_accounts", "Unarrest player" ),

		// Staff currency grant, for refunds and event payouts. Gated on the portal's own
		// economy.manage permission -- the tier the portal assigns to Owner and Super Admin --
		// rather than on a rank name we invented here. The grid hides actions the caller lacks
		// permission for, and the host re-checks regardless of what the client believes.
		new( "givemoney", "Give Money", CategoryCommands, "economy.manage",
			StaffDispatchKind.GiveMoney, "givemoney", StaffActionTarget.OtherPlayer,
			new[]
			{
				new StaffActionArg( "amount", "Amount", StaffArgKind.Number, true, "e.g. 5000" ),
				new StaffActionArg( "destination", "Destination", StaffArgKind.Destination, true, "Cash or bank" ),
				new StaffActionArg( "reason", "Reason", StaffArgKind.Text, true, "Refund for lost printer, event payout..." )
			},
			"payments", "Grant currency to this player (audited)" ),

		new( "forcerpname", "Force RP Name", CategoryCommands, "command.forcerpname",
			StaffDispatchKind.ChatCommand, "forcerpname", StaffActionTarget.OtherPlayer,
			new[] { new StaffActionArg( "name", "RP Name", StaffArgKind.Text, false, "Leave blank to clear" ) }, "badge", "Force player's RP name" ),

		new( "canceldemote", "Cancel Demote", CategoryCommands, "command.canceldemote",
			StaffDispatchKind.ChatCommand, "canceldemote", StaffActionTarget.OtherPlayer, NoArgs, "how_to_vote", "Cancel player's demotion" ),

		new( "clearprops", "Clear Props", CategoryCommands, "command.clearprops",
			StaffDispatchKind.ChatCommand, "clearprops", StaffActionTarget.OtherPlayer, NoArgs, "cleaning_services", "Clear player's props" ),

		new( "forceselldoor", "Force Sell Door", CategoryCommands, "command.forceselldoor",
			StaffDispatchKind.ChatCommand, "forceselldoor", StaffActionTarget.SelfOnly, NoArgs, "sensor_door", "Force-sell the door you're looking at" ),

		// ---- Ability ----
		new( "goto", "Teleport To", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "goto", StaffActionTarget.OtherPlayer, NoArgs, "my_location", "Teleport to player" ),

		new( "bring", "Bring", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "bring", StaffActionTarget.OtherPlayer, NoArgs, "pan_tool", "Bring player to you" ),

		new( "return", "Return", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "return", StaffActionTarget.OtherPlayer, NoArgs, "undo", "Return player to their position" ),

		new( "tpall", "Teleport All", CategoryAbility, "ability.teleportall",
			StaffDispatchKind.ChatCommand, "tpall", StaffActionTarget.Global, NoArgs, "groups", "Teleport everyone to you" ),

		new( "noclip", "Noclip", CategoryAbility, "ability.noclip",
			StaffDispatchKind.LocalToggle, "noclip", StaffActionTarget.SelfOnly, NoArgs, "flight", "Toggle noclip flight" )
	};
}

/// <summary>
/// Tunable, server-agnostic policy the DXRP portal can't express. The portal only toggles WHETHER a
/// rank may ban — not for how long. This adds a per-rank ban-duration ceiling (the headline value-add
/// for reselling the menu to other servers). It is a client-side guardrail for UX; DXRP's own
/// permission checks remain the security boundary (see TECH_DEBT STAFF-03 for the enforceable endgame).
/// </summary>
public static class StaffMenuConfig
{
	/// <summary>
	/// Optional reference ladder placeholders under "Staff" (the only per-server config in the addon).
	///
	/// Default is empty — fully server-agnostic: the sidebar lists only portal ranks that have someone
	/// online with real staff-menu permissions, grouped by their live portal rank name. No hardcoded
	/// LifePunch empty "(0)" rows on other servers.
	///
	/// Always automatic: action visibility and staff vs Players come from portal permissions, not this list.
	/// Optional override: populate tiers here if an owner wants always-visible empty rows merged by rank name.
	/// </summary>
	public static readonly IReadOnlyList<(string Name, int Order)> ReferenceTiers = Array.Empty<(string Name, int Order)>();

	/// <summary>Quick-pick ban durations. Tokens use DXRP's m/h/d grammar, or "perm".</summary>
	public static readonly IReadOnlyList<(string Label, string Token, int Hours)> BanDurations = new[]
	{
		("1 hour", "1h", 1),
		("6 hours", "6h", 6),
		("1 day", "1d", 24),
		("3 days", "3d", 72),
		("1 week", "7d", 168),
		("30 days", "30d", 720),
		("Permanent", "perm", int.MaxValue)
	};

	/// <summary>
	/// Max ban length (in hours) the caller's rank ORDER may issue; null = unlimited (permanent allowed).
	/// Defaults match the live LifePunch ladder (Mod=4, Admin=5, Super Admin=10, Owner=69).
	/// </summary>
	public static int? MaxBanHoursForRankOrder( int order ) => order switch
	{
		>= 10 => null,    // Super Admin / Owner — unlimited
		>= 5 => 168,      // Admin — up to 1 week
		>= 4 => 24,       // Mod — up to 1 day (only if granted player.ban at all)
		_ => 0            // below staff — none
	};

	/// <summary>True if a rank order may issue the given quick-pick duration (in hours).</summary>
	public static bool IsBanDurationAllowed( int rankOrder, int hours )
	{
		var cap = MaxBanHoursForRankOrder( rankOrder );
		return cap is null || hours <= cap.Value;
	}
}
