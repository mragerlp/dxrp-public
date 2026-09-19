// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.

using System;
using System.Collections.Generic;
#if !LIFEPUNCH_LOCAL
using Sandbox;
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.StaffMenu;

internal readonly record struct StaffChatContext(
	object? Source, string Scope, bool Available, bool Allowed, int MaxLength, string SendBlockedReason )
{
	public bool CanRead => Available && Allowed;
	public bool CanSend => CanRead && string.IsNullOrEmpty( SendBlockedReason );
	public bool SameSession( StaffChatContext other ) => ReferenceEquals( Source, other.Source ) && Scope == other.Scope;
}

internal readonly record struct StaffChatMessage(
	Guid Id, string Author, string AuthorColor, string Role, string RoleColor, string Text );

internal readonly record struct StaffChatSnapshot( IReadOnlyList<StaffChatMessage> Messages, int Available )
{
	public static StaffChatSnapshot Empty => new( Array.Empty<StaffChatMessage>(), 0 );
}

/// <summary>Filter before capping, preserving the native newest-first enumeration order.</summary>
internal static class StaffChatRecent
{
	// Match Observe's display limit; the denominator is only this received buffer's staff rows.
	public const int DisplayCap = 40;

	public static StaffChatSnapshot Select<T>( IEnumerable<T> entries, Func<T, bool> isStaffChat,
		Func<T, StaffChatMessage> project )
	{
		var messages = new List<StaffChatMessage>( DisplayCap );
		var available = 0;
		foreach ( var entry in entries )
		{
			if ( !isStaffChat( entry ) ) continue;
			available++;
			if ( messages.Count < DisplayCap ) messages.Add( project( entry ) );
		}
		return new( messages, available );
	}
}

/// <summary>
/// Reads only the local native chat buffer. No additional archive, RPC, or recipient list.
/// Permission regrant may reveal still-buffered messages originally delivered by DXRP.
/// </summary>
internal static class StaffChatHost
{
	public static StaffChatContext ReadContext()
	{
#if LIFEPUNCH_LOCAL
		return new( null, string.Empty, false, false, 0, "Staff chat is unavailable in this build." );
#else
		var chat = Chat.Current;
		var player = Player.Local;
		var scope = $"{Game.ActiveScene?.Id}|{Connection.Host?.Id}|{player?.ConnectionId}|{Game.SteamId}";
		if ( chat is null || player is null || !player.IsValid() || Config.Current?.Game is not { } configuration )
			return new( chat, scope, false, false, 0, "Staff chat is unavailable in this session." );

		var allowed = RankSystem.HasLocalPermission( Permission.StaffChat );
		var blocked = !allowed ? "Requires permission: chat.staff"
			: player.HasStatus( Constants.GaggedStatus ) || (Status.Current?.HasStatus( player.SteamId, Constants.GaggedStatus ) ?? false)
				? "Chat is unavailable while gagged."
			: configuration.ChatMaxLength <= 0 ? "Staff chat is unavailable in this session."
			: Cooldown.Current == null ? "Staff chat is unavailable in this session."
			: Cooldown.Current.IsOnCooldown( "chat" ) ? "Wait for the chat cooldown."
			: string.Empty;
		return new( chat, scope, true, allowed, configuration.ChatMaxLength, blocked );
#endif
	}

	public static Guid LatestMessageId
	{
		get
		{
#if LIFEPUNCH_LOCAL
			return Guid.Empty;
#else
			return Chat.Current?.LatestMessageId ?? Guid.Empty;
#endif
		}
	}

	public static int MessageCount
	{
		get
		{
#if LIFEPUNCH_LOCAL
			return 0;
#else
			return Chat.Current?.Entries.Count ?? 0;
#endif
		}
	}

	public static StaffChatSnapshot ReadMessages( StaffChatContext expected )
	{
#if LIFEPUNCH_LOCAL
		return StaffChatSnapshot.Empty;
#else
		var current = ReadContext();
		if ( !current.CanRead || !current.SameSession( expected ) )
			return StaffChatSnapshot.Empty;

		// Exact channel match; Role and RoleColor come from native authorized delivery.
		// Do not infer a hidden role from the player's current rank or retain evicted entries.
		return StaffChatRecent.Select( Chat.Current.Entries, entry => entry.Type == MessageType.StaffChat,
			entry => new StaffChatMessage( entry.MessageId, entry.Author, entry.Color.Hex,
				entry.Role ?? string.Empty, entry.RoleColor?.Hex ?? string.Empty, entry.Message ) );
#endif
	}

	/// <summary>True means submitted to the native path, not acknowledged delivery.</summary>
	public static bool TrySubmit( StaffChatContext expected, string draft, out string reason )
	{
		var current = ReadContext();
		if ( !current.SameSession( expected ) || !current.CanSend )
		{
			reason = !current.SameSession( expected ) ? "The chat session changed. Enter a new message." : current.SendBlockedReason;
			return false;
		}

		if ( !StaffChatInput.TryPrepare( draft, current.MaxLength, out var text, out reason ) )
			return false;
#if LIFEPUNCH_LOCAL
		reason = "Staff chat is unavailable in this build.";
		return false;
#else
		// Fixed channel, no /staff prefix. Native submission retains host permission, gag,
		// length and cooldown enforcement. No optimistic row or delivery-success toast.
		Chat.Current.SubmitPlayerChat( text, MessageType.StaffChat );
		return true;
#endif
	}
}
