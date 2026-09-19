// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.

using System;
using System.Globalization;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>Plain-text boundary before DXRP's shared chat/command parser.</summary>
internal static class StaffChatInput
{
	public static bool TryPrepare( string? draft, int maxLength, out string text, out string reason )
	{
		text = (draft ?? string.Empty).Trim();
		reason = string.Empty;
		if ( text.Length == 0 )
		{
			reason = "Enter a staff message.";
			return false;
		}

		// Native submission checks // and /; the host trims before checking / and @.
		// Validate the exact normalized text that will be submitted, never a prefix-stripped copy.
		if ( text[0] == '/' || text[0] == '@' )
		{
			reason = "Plain text only. Commands and channel shortcuts are unavailable here.";
			return false;
		}

		// Format characters are not removed by native Trim, but reject a hidden leading
		// prefix anyway. Keep interior format characters so ordinary joined emoji still work.
		if ( char.GetUnicodeCategory( text[0] ) == UnicodeCategory.Format )
		{
			reason = "Start the message with visible text.";
			return false;
		}

		foreach ( var character in text )
		{
			if ( char.IsControl( character ) || character == '\u2028' || character == '\u2029' )
			{
				reason = "Use a single line of plain text.";
				return false;
			}
		}

		if ( maxLength <= 0 || text.Length > maxLength )
		{
			reason = maxLength > 0 ? $"Use {maxLength} characters or fewer." : "Chat is unavailable in this session.";
			return false;
		}

		return true;
	}
}
