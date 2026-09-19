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

/// <summary>Operator-impact classification carried by every catalog action.</summary>
public enum StaffActionSeverity
{
	Light,
	Medium,
	Severe
}

/// <summary>Optional catalog-tile display override; severity remains authoritative.</summary>
public enum StaffActionDisplayTone
{
	Severity,
	Money,
	Armor,
	Cloak,
	Freeze,
	Health
}

/// <summary>
/// Which DXRP backend an action dispatches to. The menu prefers a direct <c>AdminSystem</c> host
/// RPC where one exists, else routes through the registered chat <c>ICommand</c> via
/// <c>Chat.ExecuteCommandHost</c>. <see cref="LocalToggle"/> handles client-only affordances that
/// DXRP exposes via keybind rather than a command (e.g. noclip move-mode). Every path is re-checked
/// at the execution boundary; client-only commands retain their native local permission checks.
/// </summary>
public enum StaffDispatchKind
{
	AdminRpc,
	ChatCommand,
	LocalToggle,

	/// <summary>
	/// Currency grant through a host request; the host validates permission and executes it.
	/// </summary>
	GiveMoney,
	LocalCommand
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
/// A staff command definition using permission IDs and dispatch names instead of Dxura types.
/// Built-in IDs follow DXRP; addon commands define separate IDs that require their own rank grants.
/// </summary>
public sealed record StaffAction(
	string Key,
	string Label,
	string Category,
	string PermissionId,
	StaffDispatchKind Dispatch,
	string DispatchTarget,
	StaffActionTarget TargetMode,
	StaffActionSeverity Severity,
	IReadOnlyList<StaffActionArg> Args,
	string Icon = "bolt",
	string Tooltip = "",
	StaffActionDisplayTone DisplayTone = StaffActionDisplayTone.Severity );

/// <summary>
/// Staff command catalogue and categories following the DXRP portal permission taxonomy.
/// Chat-only commands are excluded; Set Job dispatches to DXRP /job using command.job.manage.
/// Built-in catalogue reconciled against the portal Super Admin permission set in 2026-06.
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

	private static StaffActionArg Duration( bool required, bool allowPermanent = false ) =>
		new( "duration", "Duration", StaffArgKind.Duration, required,
			allowPermanent ? "e.g. 1h, 1d, 7d, perm" : "e.g. 10m, 1h, 1d" );

	// Rebuild catalog data from source during hotload instead of migrating old records.
	[Sandbox.SkipHotload]
	public static readonly IReadOnlyList<StaffAction> All = new List<StaffAction>
	{
		// ---- Moderation ----
		new( "kick", "Kick", CategoryModeration, "player.kick",
			StaffDispatchKind.AdminRpc, "kick", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			new[] { Reason( required: true ) }, "logout", "Kick player" ),

		new( "ban", "Ban", CategoryModeration, "player.ban",
			StaffDispatchKind.ChatCommand, "ban", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			new[] { Duration( required: true, allowPermanent: true ), Reason( required: true ) }, "gavel", "Ban player" ),

		new( "jail", "Jail", CategoryModeration, "player.jail",
			StaffDispatchKind.ChatCommand, "jail", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			new[] { Duration( required: true ), Reason( required: true ) }, "lock", "Jail player" ),

		new( "gag", "Gag", CategoryModeration, "player.gag",
			StaffDispatchKind.ChatCommand, "gag", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			new[] { Duration( required: true ), Reason( required: true ) }, "mic_off", "Mute player's voice" ),

		new( "warn", "Warn", CategoryModeration, "player.warn",
			StaffDispatchKind.ChatCommand, "warn", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			new[] { Reason( required: true ) }, "warning", "Warn player" ),

		new( "spectate", "Spectate", CategoryModeration, "player.spectate",
			StaffDispatchKind.ChatCommand, "spectate", StaffActionTarget.OtherPlayer, StaffActionSeverity.Light,
			NoArgs, "visibility", "Spectate player" ),

		new( "screenshot", "Screenshot", CategoryModeration, "player.screenshot",
			StaffDispatchKind.AdminRpc, "screenshot", StaffActionTarget.OtherPlayer, StaffActionSeverity.Light,
			NoArgs, "photo_camera", "Capture player's screen" ),

		// ---- Commands ----
		new( "god", "God Mode", CategoryCommands, "command.god",
			StaffDispatchKind.ChatCommand, "god", StaffActionTarget.SelfOnly, StaffActionSeverity.Severe,
			NoArgs, "volunteer_activism", "Toggle god mode" ),

		new( "cloak", "Cloak", CategoryCommands, "command.cloak",
			StaffDispatchKind.ChatCommand, "cloak", StaffActionTarget.SelfOnly, StaffActionSeverity.Light,
			NoArgs, "visibility_off", "Go invisible", StaffActionDisplayTone.Cloak ),

		new( "incognito", "Incognito", CategoryCommands, "command.incognito",
			StaffDispatchKind.ChatCommand, "incognito", StaffActionTarget.SelfOnly, StaffActionSeverity.Light,
			NoArgs, "do_not_disturb_on_total_silence", "Hide from player list" ),

		new( "fakedisconnect", "Fake Disconnect", CategoryCommands, "command.fakedisconnect",
			StaffDispatchKind.ChatCommand, "fakedisconnect", StaffActionTarget.SelfOnly, StaffActionSeverity.Light,
			NoArgs, "wifi_off", "Fake a disconnect" ),

		new( "freeze", "Freeze", CategoryCommands, "command.freeze",
			StaffDispatchKind.ChatCommand, "freeze", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			NoArgs, "ac_unit", "Freeze player in place", StaffActionDisplayTone.Freeze ),

		new( "sethealth", "Set Health", CategoryCommands, "command.sethealth",
			StaffDispatchKind.ChatCommand, "sethealth", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			new[] { new StaffActionArg( "amount", "Health", StaffArgKind.Number, true, "e.g. 100" ) }, "favorite", "Set player's health", StaffActionDisplayTone.Health ),

		// Addon-defined permission; granting Set Health does not grant Set Armor.
		new( "setarmor", "Set Armor", CategoryCommands, "command.setarmor",
			StaffDispatchKind.ChatCommand, "setarmor", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			new[] { new StaffActionArg( "amount", "Armor", StaffArgKind.Number, true, "e.g. 100; 0 clears armor" ) }, "shield", "Set player's armor within the server limit", StaffActionDisplayTone.Armor ),

		new( "setjob", "Set Job", CategoryCommands, "command.job.manage",
			StaffDispatchKind.ChatCommand, "job", StaffActionTarget.OtherPlayer, StaffActionSeverity.Light,
			new[] { new StaffActionArg( "job", "Job", StaffArgKind.Job, true, "Search or pick a job" ) },
			"work", "Force-set the player's job (gamemode job list)" ),

		new( "arrest", "Arrest", CategoryCommands, "command.arrest",
			StaffDispatchKind.ChatCommand, "arrest", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			new[] { Duration( required: false ) }, "local_police", "Arrest player" ),

		new( "unarrest", "Unarrest", CategoryCommands, "command.unarrest",
			StaffDispatchKind.ChatCommand, "unarrest", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			NoArgs, "no_accounts", "Unarrest player" ),

		// Uses economy.manage for refunds and event payouts. The host rechecks the grant
		// regardless of client visibility or rank label.
		new( "givemoney", "Give Money", CategoryCommands, "economy.manage",
			StaffDispatchKind.GiveMoney, "givemoney", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			new[]
			{
				new StaffActionArg( "destination", "Destination", StaffArgKind.Destination, true, "Cash or bank" ),
				new StaffActionArg( "amount", "Amount", StaffArgKind.Number, true, "e.g. 5000" ),
				new StaffActionArg( "reason", "Reason", StaffArgKind.Text, true, "Refund for lost printer, event payout..." )
			},
			"payments", "Grant currency to this player (audited)", StaffActionDisplayTone.Money ),

		new( "forcerpname", "Force RP Name", CategoryCommands, "command.forcerpname",
			StaffDispatchKind.ChatCommand, "forcerpname", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			new[] { new StaffActionArg( "name", "RP Name", StaffArgKind.Text, false, "Leave blank to clear" ) }, "badge", "Force player's RP name" ),

		new( "canceldemote", "Cancel Demote", CategoryCommands, "command.canceldemote",
			StaffDispatchKind.ChatCommand, "canceldemote", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			NoArgs, "how_to_vote", "Cancel player's demotion" ),

		new( "clearprops", "Clear Props", CategoryCommands, "command.clearprops",
			StaffDispatchKind.ChatCommand, "clearprops", StaffActionTarget.OtherPlayer, StaffActionSeverity.Severe,
			NoArgs, "cleaning_services", "Clear player's props" ),

		new( "forceselldoor", "Force Sell Door", CategoryCommands, "command.forceselldoor",
			StaffDispatchKind.ChatCommand, "forceselldoor", StaffActionTarget.SelfOnly, StaffActionSeverity.Medium,
			NoArgs, "sensor_door", "Force-sell the door you're looking at" ),

		// ---- Ability ----
		new( "goto", "Teleport To", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "goto", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			NoArgs, "my_location", "Teleport to player" ),

		new( "bring", "Bring", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "bring", StaffActionTarget.OtherPlayer, StaffActionSeverity.Medium,
			NoArgs, "pan_tool", "Bring player to you" ),

		new( "return", "Return", CategoryAbility, "ability.teleport",
			StaffDispatchKind.ChatCommand, "return", StaffActionTarget.OtherPlayer, StaffActionSeverity.Light,
			NoArgs, "undo", "Return player to their position" ),

		new( "tpall", "Teleport All", CategoryAbility, "ability.teleportall",
			StaffDispatchKind.ChatCommand, "tpall", StaffActionTarget.Global, StaffActionSeverity.Severe,
			NoArgs, "groups", "Teleport everyone to you" ),

		new( "noclip", "Noclip", CategoryAbility, "ability.noclip",
			StaffDispatchKind.LocalToggle, "noclip", StaffActionTarget.SelfOnly, StaffActionSeverity.Severe,
			NoArgs, "flight", "Toggle noclip flight" ),

		// Each server owner grants this capability to their trusted staff ranks.
		// X-ray shares the Frozen palette; this display tone does not imply a native status.
		new( "xray", "X-ray", CategoryAbility, "command.xray",
			StaffDispatchKind.LocalCommand, "xray", StaffActionTarget.SelfOnly, StaffActionSeverity.Severe,
			NoArgs, "xray", "Toggle X-ray for yourself", StaffActionDisplayTone.Freeze )
	};
}

/// <summary>
	/// Tunable policy the DXRP portal can't express. The portal only toggles WHETHER a rank may ban —
	/// not for how long. This addon therefore limits the durations its own UI will dispatch. DXRP's
	/// native command permission remains the host security boundary; this table is never presented as
	/// a replacement for a server-side tenant policy.
/// </summary>
public static class StaffMenuConfig
{
	/// <summary>
	/// Optional empty rank groups under Staff. By default, groups come from online players
	/// with staff-menu permissions. These labels do not grant permissions or control action visibility.
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

	/// <summary>Only Ban accepts an indefinite sanction; other commands require a finite duration.</summary>
	public static IEnumerable<(string Label, string Token, int Hours)> DurationPresets( string actionKey )
	{
		foreach ( var preset in BanDurations )
		{
			if ( actionKey == "ban" || preset.Token != "perm" )
			{
				yield return preset;
			}
		}
	}

	/// <summary>
	/// Normalize the token sent to DXRP and enforce its integer and TimeSpan parser bounds.
	/// Blank optional values are handled by the form; only Ban may use a permanent token.
	/// </summary>
	public static bool TryNormalizeDurationToken( string token, bool allowPermanent, out string normalized )
	{
		normalized = "";
		token = ( token ?? string.Empty ).Trim().ToLowerInvariant();
		if ( token is "perm" or "permanent" )
		{
			if ( !allowPermanent ) return false;
			normalized = "perm";
			return true;
		}

		if ( token.Length < 2 || !int.TryParse( token[..^1], out var value ) || value <= 0 )
		{
			return false;
		}

		TimeSpan? duration;
		try
		{
			duration = token[^1] switch
			{
				'm' => TimeSpan.FromMinutes( value ),
				'h' => TimeSpan.FromHours( value ),
				'd' => TimeSpan.FromDays( value ),
				_ => null
			};
		}
		catch ( OverflowException )
		{
			return false;
		}
		catch ( ArgumentOutOfRangeException )
		{
			return false;
		}

		if ( duration is null ) return false;
		normalized = value.ToString( System.Globalization.CultureInfo.InvariantCulture ) + token[^1];
		return true;
	}

	/// <summary>
	/// Validate native duration syntax. Portal permission and host targeting authorize a ban;
	/// server-specific rank ordinals do not impose an additional ULX duration policy.
	/// </summary>
	public static bool IsBanDurationTokenAllowed( string token )
		=> TryNormalizeDurationToken( token, allowPermanent: true, out _ );

}
