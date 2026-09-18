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

namespace LifePunch.DXRP.Addons.Morpheus;

/// <summary>
/// What a readout is. Kind is decided by the civic flag, never by line text.
/// </summary>
public enum LpMorpheusReadoutKind
{
	Hidden,
	File,
	Civic
}

/// <summary>
/// One line of Morpheus readout, ready for the readout consumer.
/// Speaker is always MORPHEUS. Line arrives pre-sanitized and is not re-scanned.
/// </summary>
public readonly record struct LpMorpheusReadout( string Speaker, string Line, LpMorpheusReadoutKind Kind )
{
	/// <summary>
	/// The only speaker label this chamber ever shows. Single source so the label
	/// cannot drift between the readout and the panel that renders it.
	/// </summary>
	public const string SpeakerName = "MORPHEUS";

	/// <summary>
	/// Empty readout: nothing to say.
	/// </summary>
	public static LpMorpheusReadout Hidden { get; } = new( SpeakerName, string.Empty, LpMorpheusReadoutKind.Hidden );

	/// <summary>
	/// Builds a readout from an already-sanitized briefing line.
	/// Kind comes only from the civic flag: empty or whitespace line is Hidden,
	/// civic true is Civic, civic false is File. Line text is never scanned.
	/// </summary>
	public static LpMorpheusReadout FromBriefing( string sanitizedLine, bool civic )
	{
		if ( string.IsNullOrWhiteSpace( sanitizedLine ) )
			return Hidden;

		return new LpMorpheusReadout( SpeakerName, sanitizedLine, civic ? LpMorpheusReadoutKind.Civic : LpMorpheusReadoutKind.File );
	}
}
