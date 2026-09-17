// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "Morpheus" (addon ident: lpmorpheus) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Text;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Morpheus;

/// <summary>
/// Original LifePunch chamber lines. Cap owned by this addon.
/// No Ion Storm transcript — do not paste Deus Ex dialogue here.
/// </summary>
public static class LpMorpheusSpeech
{
	public const string VoiceName = "Microsoft David Desktop";

	public const int MaxLength = 300;

	public static string Sanitize( string message )
	{
		if ( MaxLength <= 0 )
			return string.Empty;

		var normalized = Normalize( message );
		if ( normalized.Length <= MaxLength )
			return normalized;

		return CutWordSafe( normalized, MaxLength );
	}

	/// <summary>
	/// Charset and whitespace fold only — no TTS cap. Civic briefing measures this
	/// so the removal loop can still run.
	/// </summary>
	public static string Normalize( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return string.Empty;

		var sanitized = new StringBuilder( message.Length );
		var lastWasSpace = true;

		foreach ( var c in message )
		{
			if ( char.IsWhiteSpace( c ) )
			{
				if ( lastWasSpace )
					continue;

				sanitized.Append( ' ' );
				lastWasSpace = true;
			}
			else if ( c is >= ' ' and <= '~' )
			{
				sanitized.Append( c );
				lastWasSpace = false;
			}
		}

		return sanitized.ToString().Trim();
	}

	public static string BuildFileGreeting( string displayName, string jobTitle, bool wanted, bool government )
	{
		var name = Token( displayName );
		var job = Token( jobTitle );

		if ( wanted )
			return Sanitize( $"Morpheus. Visitor file. Name {name}. Occupation {job}. Status wanted." );

		if ( government )
			return Sanitize( $"Morpheus. Visitor file. Name {name}. Duty {job}. Status credentialed." );

		return Sanitize( $"Morpheus. Visitor file. Name {name}. Occupation {job}. Status clear." );
	}

	public static string BuildLocalEditorFile()
		=> Sanitize( "Morpheus. Visitor file. Local editor session. Status unlisted." );

	public static string BuildCivicBriefing( IReadOnlyList<string> wantedNames, IReadOnlyList<string> laws )
	{
		var names = NormalizeList( wantedNames );
		var posted = NormalizeList( laws );
		var originalNameCount = wantedNames?.Count ?? 0;
		var originalLawCount = laws?.Count ?? 0;

		while ( true )
		{
			var line = ComposeCivic( names, posted, originalNameCount, originalLawCount );
			var normalized = Normalize( line );
			if ( normalized.Length <= MaxLength )
				return normalized;

			if ( posted.Count > 0 )
			{
				posted.RemoveAt( posted.Count - 1 );
				continue;
			}

			if ( names.Count > 0 )
			{
				names.RemoveAt( names.Count - 1 );
				continue;
			}

			// Exhaust: both lists dropped and the line still exceeds the cap. Fall
			// back to the existing omit sentences, never a mid-word hard cut.
			return Sanitize( "Morpheus. City briefing. Wanted names omitted. Posted laws omitted." );
		}
	}

#if !LIFEPUNCH_LOCAL
	public static string ResolveLaw( string raw )
	{
		if ( string.IsNullOrWhiteSpace( raw ) )
			return string.Empty;

		var phrase = Language.GetPhrase( raw );
		if ( phrase == raw && raw.StartsWith( '#' ) )
			phrase = Language.GetPhrase( raw[1..] );

		return phrase;
	}
#else
	public static string ResolveLaw( string raw ) => raw ?? string.Empty;
#endif

	private static string ComposeCivic(
		IReadOnlyList<string> names,
		IReadOnlyList<string> laws,
		int originalNameCount,
		int originalLawCount )
	{
		var wantedPart = originalNameCount == 0
			? "No warrants posted."
			: names.Count == 0
				? "Wanted names omitted."
				: "Wanted: " + string.Join( ", ", names ) + ".";

		var lawPart = originalLawCount == 0
			? "No laws on the board."
			: laws.Count == 0
				? "Posted laws omitted."
				: "Posted laws: " + string.Join( "; ", laws ) + ".";

		var droppedNames = originalNameCount - names.Count;
		var droppedLaws = originalLawCount - laws.Count;
		var omit = string.Empty;
		if ( droppedNames > 0 && droppedLaws > 0 && names.Count > 0 && laws.Count > 0 )
			omit = " Remaining warrants and laws omitted.";
		else if ( droppedNames > 0 && names.Count > 0 )
			omit = " Remaining warrants omitted.";
		else if ( droppedLaws > 0 && laws.Count > 0 )
			omit = " Remaining laws omitted.";

		return $"Morpheus. City briefing. {wantedPart} {lawPart}{omit}";
	}

	private static List<string> NormalizeList( IReadOnlyList<string> values )
	{
		var list = new List<string>();
		if ( values is null )
			return list;

		foreach ( var value in values )
		{
			var token = Token( value );
			if ( token.Length > 0 && token != "unlisted" )
				list.Add( token );
		}

		return list;
	}

	private static string Token( string value )
	{
		var sanitized = Sanitize( value ?? string.Empty );
		if ( string.IsNullOrWhiteSpace( sanitized ) )
			return "unlisted";

		if ( sanitized.Length <= 40 )
			return sanitized;

		var cut = CutWordSafe( sanitized, 40 );
		return cut.Length > 0 ? cut : "unlisted";
	}

	/// <summary>
	/// Overflow never ends mid-word: cut at the last space at or before the cap,
	/// or fail closed to empty when there is no space to cut at.
	/// </summary>
	private static string CutWordSafe( string text, int max )
	{
		var cut = text.LastIndexOf( ' ', max );
		if ( cut <= 0 )
			return string.Empty;

		return text[..cut].TrimEnd();
	}
}
