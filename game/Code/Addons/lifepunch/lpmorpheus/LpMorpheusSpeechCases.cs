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
using System.Linq;

namespace LifePunch.DXRP.Addons.Morpheus;

/// <summary>
/// Read-for case table freezing the speech builder contract. Original LifePunch
/// strings only. Nothing here runs in a session; a reviewer or later harness
/// compares Actual() with Expected. A row with ExpectPass=false documents a
/// known residual a later slice must flip to passing.
/// </summary>
public static class LpMorpheusSpeechCases
{
	public sealed record SpeechCase( string Name, string Detail, string Expected, Func<string> Actual, bool ExpectPass )
	{
		public bool Passes => string.Equals( Actual(), Expected, StringComparison.Ordinal );

		/// <summary>True when observed behavior matches what the row documents.</summary>
		public bool Agrees => Passes == ExpectPass;
	}

	// Overflow probe: 7-letter word plus space repeated to 400 chars. The cap (300)
	// is not a multiple of 8, so a naive hard cut would land inside a word.
	private static string OverflowInput => string.Concat( Enumerable.Repeat( "chamber ", 50 ) );

	// Whole "chamber " blocks that fit inside the cap, joined back word-safe.
	private static string OverflowWordSafe
		=> string.Join( " ", Enumerable.Repeat( "chamber", ( LpMorpheusSpeech.MaxLength + 1 ) / 8 ) );

	// Removal-loop expectation at cap 300: with laws absent and names dropped, a
	// composed line with k names runs 10k + 82 chars, so the largest fitting k is 21.
	private static string CivicShrinkExpected
		=> "Morpheus. City briefing. Wanted: "
			+ string.Join( ", ", Enumerable.Repeat( "resident", 21 ) )
			+ ". No laws on the board. Remaining warrants omitted.";

	public static readonly IReadOnlyList<SpeechCase> Rows = new SpeechCase[]
	{
		new( "empty input", "Sanitize(\"\")",
			string.Empty,
			() => LpMorpheusSpeech.Sanitize( string.Empty ), true ),
		new( "whitespace input", "Sanitize(\"   \\t  \")",
			string.Empty,
			() => LpMorpheusSpeech.Sanitize( "   \t  " ), true ),
		new( "ascii fold", "Normalize collapses whitespace, drops non-printable-ASCII",
			"Chamber of the city",
			() => LpMorpheusSpeech.Normalize( "  Chamber —\t of  the\ncity  " ), true ),
		new( "civic short line", "BuildCivicBriefing: one name, one law, fits the cap",
			"Morpheus. City briefing. Wanted: Avery Cole. Posted laws: Curfew at midnight.",
			() => LpMorpheusSpeech.BuildCivicBriefing( new[] { "Avery Cole" }, new[] { "Curfew at midnight" } ), true ),
		new( "civic empty boards", "BuildCivicBriefing: no warrants, no laws",
			"Morpheus. City briefing. No warrants posted. No laws on the board.",
			() => LpMorpheusSpeech.BuildCivicBriefing( Array.Empty<string>(), Array.Empty<string>() ), true ),
		new( "file greeting wanted", "BuildFileGreeting: Avery Cole, Courier, wanted",
			"Morpheus. Visitor file. Name Avery Cole. Occupation Courier. Status wanted.",
			() => LpMorpheusSpeech.BuildFileGreeting( "Avery Cole", "Courier", wanted: true, government: false ), true ),
		new( "file greeting government", "BuildFileGreeting: Avery Cole, City Clerk, government",
			"Morpheus. Visitor file. Name Avery Cole. Duty City Clerk. Status credentialed.",
			() => LpMorpheusSpeech.BuildFileGreeting( "Avery Cole", "City Clerk", wanted: false, government: true ), true ),
		new( "file greeting clear", "BuildFileGreeting: Avery Cole, Courier, clear",
			"Morpheus. Visitor file. Name Avery Cole. Occupation Courier. Status clear.",
			() => LpMorpheusSpeech.BuildFileGreeting( "Avery Cole", "Courier", wanted: false, government: false ), true ),
		new( "local editor file", "BuildLocalEditorFile()",
			"Morpheus. Visitor file. Local editor session. Status unlisted.",
			() => LpMorpheusSpeech.BuildLocalEditorFile(), true ),
		new( "RESIDUAL (flipped Slice 3): overflow is word-safe", "Sanitize(400-char splittable blob) cuts at a word boundary",
			OverflowWordSafe,
			() => LpMorpheusSpeech.Sanitize( OverflowInput ), true ),
		new( "unsplittable overflow fails closed", "Sanitize(400 x's, no spaces): no word boundary to cut at",
			string.Empty,
			() => LpMorpheusSpeech.Sanitize( new string( 'x', 400 ) ), true ),
		new( "civic removal loop shrinks", "BuildCivicBriefing: 30 wanted names, no laws; loop drops to 21 at cap 300",
			CivicShrinkExpected,
			() => LpMorpheusSpeech.BuildCivicBriefing( Enumerable.Repeat( "resident", 30 ).ToArray(), null ), true ),
		new( "token word-safe cut", "BuildFileGreeting: 43-char display name; Token cuts word-safe under 40",
			"Morpheus. Visitor file. Name Metropolitan Records Adjudicator. Occupation Courier. Status clear.",
			() => LpMorpheusSpeech.BuildFileGreeting( "Metropolitan Records Adjudicator Generalist", "Courier", wanted: false, government: false ), true ),
	};

	/// <summary>Rows whose observed behavior disagrees with what they document.</summary>
	public static IEnumerable<string> Disagreements()
		=> Rows.Where( row => !row.Agrees ).Select( row => row.Name );
}
