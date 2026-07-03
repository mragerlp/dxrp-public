namespace Dxura.RP.Game;

/// <summary>
/// Per-member local party UI preferences. These are saved client-side and never host-synced.
/// </summary>
public static class PartyPreferences
{
	/// <summary>
	/// Local player preference for showing party member outlines.
	/// Default is off and can be toggled via /party outline or Party menu Settings.
	/// </summary>
	[ConVar( "party_member_outline", ConVarFlags.Saved )]
	public static bool MemberOutlineEnabled { get; set; } = false;

	public static void SetMemberOutlineEnabled( bool enabled )
	{
		MemberOutlineEnabled = enabled;
	}
}
