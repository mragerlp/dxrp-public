// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// KEYPAD-1 (cc-lp-2): the keypad's pure rules. No engine type is used in this file, so it compiles and runs outside
// the editor (the offline gate). It holds the code format, the salted digest, the wrong-try lockout, the fade clamp,
// and the result vocabulary + copy that the entity and the panel share.
//
// Every tunable below is a PROVISIONAL const in this one place. None is read from T1/T2/T3 config: extending the
// config surface needs a ruling (lifepunch-config). The rulings owed are listed in the KEYPAD-1 return (WAITS).

using System;
using System.Security.Cryptography;
using System.Text;

namespace LifePunch.DXRP.Addons.Keypad;

public static class LpKeypadRules
{
	// ---- Code format -------------------------------------------------------------------------------------------

	public const int MinDigits = 4;
	public const int MaxDigits = 8;

	// ---- PROVISIONAL policy (WAIT: lockout policy, max fade seconds) -------------------------------------------

	/// <summary>Wrong codes from one caller before that caller is locked out of this keypad.</summary>
	public const int MaxFailsBeforeLockout = 5;

	/// <summary>How long a locked-out caller waits.</summary>
	public const float LockoutSeconds = 30f;

	public const float MinFadeSeconds = 1f;
	public const float DefaultFadeSeconds = 5f;
	public const float MaxFadeSeconds = 30f;

	/// <summary>Reach, in world units from the keypad, within which "link nearest door" looks for a door.</summary>
	public const float LinkRadius = 160f;

	/// <summary>Tint alpha of a faded door (still drawn, so the owner and raiders see where it was).</summary>
	public const float FadedAlpha = 0.25f;

	// ---- Result vocabulary (the ILpHubResult kind and the payload outcome) ----------------------------------------

	public const string KindTry = "try";
	public const string KindSet = "set";
	public const string KindLink = "link";
	public const string KindUnlink = "unlink";

	public const string Ok = "ok";
	public const string Wrong = "wrong";
	public const string Locked = "locked";
	public const string NoCode = "nocode";
	public const string BadFormat = "badformat";
	public const string Denied = "denied";
	public const string NoDoor = "nodoor";

	// ---- Code format and digest ----------------------------------------------------------------------------------

	/// <summary>
	/// True for 4 to 8 ASCII digits. Not char.IsDigit: that accepts other scripts' digits, which no keypad can type.
	/// </summary>
	public static bool IsValidFormat( string code )
	{
		if ( string.IsNullOrEmpty( code ) || code.Length < MinDigits || code.Length > MaxDigits )
			return false;

		foreach ( var c in code )
		{
			if ( c < '0' || c > '9' )
				return false;
		}

		return true;
	}

	/// <summary>Per-keypad random salt. Host-side, never replicated.</summary>
	public static string NewSalt() => Guid.NewGuid().ToString( "N" );

	/// <summary>
	/// Salted SHA-256 hex digest. It is a fiction gate, not banking crypto: a 4-digit code is a 10,000-choice online
	/// oracle even with the digest host-only, so the lockout below is the real fence and the digest never leaves the
	/// host (no [Sync], no client message carries it).
	/// </summary>
	public static string Digest( string code, string salt )
	{
		var bytes = SHA256.HashData( Encoding.UTF8.GetBytes( ( salt ?? string.Empty ) + ":" + ( code ?? string.Empty ) ) );
		return Convert.ToHexString( bytes );
	}

	/// <summary>A malformed code is rejected before any hashing, so a huge client string is dropped at the door.</summary>
	public static bool Matches( string code, string salt, string storedDigest )
		=> IsValidFormat( code )
		   && !string.IsNullOrEmpty( storedDigest )
		   && Digest( code, salt ) == storedDigest;

	// ---- Wrong-try lockout ------------------------------------------------------------------------------------------

	/// <summary>Per-caller attempt state. Host-only, transient: never persisted, never synced.</summary>
	public struct AttemptState
	{
		public int Fails;
		public float LockedUntil;
	}

	public static bool IsLockedOut( AttemptState state, float now ) => now < state.LockedUntil;

	/// <summary>Whole seconds left on a lockout; 0 when not locked out.</summary>
	public static int LockoutRemaining( AttemptState state, float now )
		=> IsLockedOut( state, now ) ? (int)MathF.Ceiling( state.LockedUntil - now ) : 0;

	public static int FailsLeft( AttemptState state ) => Math.Max( 0, MaxFailsBeforeLockout - state.Fails );

	/// <summary>Counts one wrong code. The fail that reaches the limit starts the lockout and zeroes the count.</summary>
	public static AttemptState RegisterFail( AttemptState state, float now, out bool lockedNow )
	{
		state.Fails++;
		lockedNow = state.Fails >= MaxFailsBeforeLockout;
		if ( lockedNow )
		{
			state.LockedUntil = now + LockoutSeconds;
			state.Fails = 0;
		}

		return state;
	}

	// ---- Fade ----------------------------------------------------------------------------------------------------------

	/// <summary>The fade length actually used: a NaN falls back to the default; anything else is clamped to the range.</summary>
	public static float ClampFade( float seconds )
		=> float.IsNaN( seconds ) ? DefaultFadeSeconds : Math.Clamp( seconds, MinFadeSeconds, MaxFadeSeconds );

	// ---- Payload and copy ----------------------------------------------------------------------------------------------

	/// <summary>Builds "outcome" or "outcome:number" (the number is a count or a seconds figure the outcome defines).</summary>
	public static string Compose( string outcome, int number ) => number > 0 ? outcome + ":" + number : outcome;

	public static void Parse( string payload, out string outcome, out int number )
	{
		outcome = string.Empty;
		number = 0;
		if ( string.IsNullOrEmpty( payload ) )
			return;

		var split = payload.IndexOf( ':' );
		if ( split < 0 )
		{
			outcome = payload;
			return;
		}

		outcome = payload.Substring( 0, split );
		int.TryParse( payload.Substring( split + 1 ), out number );
	}

	/// <summary>True when the result is a success the panel shows in the good tone.</summary>
	public static bool IsGood( string kind, string payload )
	{
		Parse( payload, out var outcome, out _ );
		return outcome == Ok;
	}

	/// <summary>The one line the panel shows for a result. ASCII only.</summary>
	public static string Message( string kind, string payload )
	{
		Parse( payload, out var outcome, out var number );
		switch ( outcome )
		{
			case Ok:
				switch ( kind )
				{
					case KindTry: return number > 0 ? "Code accepted." : "Code accepted. No door is linked to this keypad.";
					case KindSet: return "Code saved (" + number + " digits).";
					case KindLink: return "Linked to the nearest door.";
					case KindUnlink: return number > 0 ? "Unlinked " + number + " door(s)." : "No door was linked.";
					default: return "Done.";
				}
			case Wrong:
				return number > 0 ? "Wrong code. " + number + " tries left." : "Wrong code.";
			case Locked:
				return "Too many wrong codes. Try again in " + Math.Max( 1, number ) + "s.";
			case NoCode:
				return "No code is set on this keypad.";
			case BadFormat:
				return "A code is " + MinDigits + " to " + MaxDigits + " digits.";
			case Denied:
				return "Only the owner can do that.";
			case NoDoor:
				return "No door of yours within reach to link.";
			default:
				return string.Empty;
		}
	}
}
