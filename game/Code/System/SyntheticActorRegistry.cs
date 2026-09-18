using System.Collections.Generic;
using System.Linq;

namespace Dxura.RP.Game;

/// <summary>
/// Host-side contract identifying TRANSIENT SYNTHETIC ACTORS — pawns that occupy a
/// <see cref="Player"/> slot but represent no human being: staff test bots, debug pawns, and any
/// future automated operator. Membership here is the SOLE structural boundary consulted by the
/// persistence paths; it exists so identity/state belonging to a non-person can never be written
/// into host persistence as though a person had played.
///
/// WHY A REGISTRY AND NOT A FLAG: a fake name, a fake SteamId, or reading IsDebugPlayer at each
/// call site are all insufficient boundaries. IsDebugPlayer already existed and was simply never
/// read by SnapshotSystem — the flag was present and meaningless downstream. A single registry with
/// one predicate gives every sink the same answer and makes a missed call site visible.
///
/// WHY LIVE-SCOPED AND NEVER APPEND-ONLY: StaffMenuTestBots draws its rank-preview identities from
/// REAL PUBLIC STEAM ACCOUNTS (the 7656119… range) so the bots resolve real avatars. An id that is
/// synthetic right now may therefore belong to a genuine human who joins later. <see cref="Observed"/>
/// is consequently REBUILT FROM LIVE TRUTH on every sweep rather than accumulated, so a departed
/// bot's id stops being excluded the instant its pawn is gone. An append-only set would silently
/// drop that real player's crash-recovery save — trading a privacy bug for a data-loss bug.
///
/// This type is deliberately free of engine types so it can be compiled and exercised verbatim by
/// an offline harness; the predicate under test is then the shipped predicate, not a copy of it.
/// </summary>
public static class SyntheticActorRegistry
{
	private static readonly object Gate = new();

	/// <summary>
	/// Ids registered by an explicit caller. This is the forward path for a synthetic actor that does
	/// NOT surface as a debug pawn (a future IntelliBot Operator holding a real connection). Explicit
	/// entries survive sweeps and are removed only by <see cref="Unregister"/> or <see cref="Clear"/>.
	/// </summary>
	private static readonly HashSet<long> Explicit = new();

	/// <summary>
	/// Ids observed as live synthetic pawns during the most recent <see cref="ObserveLive"/> sweep.
	/// Rebuilt wholesale each sweep — see the live-scoping rationale on the type.
	/// </summary>
	private static readonly HashSet<long> Observed = new();

	/// <summary>Register a synthetic actor explicitly. Idempotent.</summary>
	public static void Register( long steamId )
	{
		lock ( Gate )
		{
			Explicit.Add( steamId );
		}
	}

	/// <summary>Drop an explicit registration. Returns true if the id was registered.</summary>
	public static bool Unregister( long steamId )
	{
		lock ( Gate )
		{
			return Explicit.Remove( steamId );
		}
	}

	/// <summary>
	/// Replace the observed set with the ids of the synthetic pawns that are live RIGHT NOW.
	/// Callers pass the full live set; anything absent is dropped. This is what makes a departed
	/// bot's id become non-synthetic again, which matters because those ids can be real accounts.
	/// </summary>
	public static void ObserveLive( IEnumerable<long> liveSyntheticIds )
	{
		lock ( Gate )
		{
			Observed.Clear();

			if ( liveSyntheticIds == null )
			{
				return;
			}

			foreach ( var id in liveSyntheticIds )
			{
				Observed.Add( id );
			}
		}
	}

	/// <summary>
	/// THE predicate. True if this SteamId currently belongs to a transient synthetic actor and must
	/// therefore be kept out of every persistence sink.
	/// </summary>
	public static bool IsSynthetic( long steamId )
	{
		lock ( Gate )
		{
			return Explicit.Contains( steamId ) || Observed.Contains( steamId );
		}
	}

	/// <summary>
	/// Predicate for call sites that hold the live pawn. The live <paramref name="isDebugPlayer"/>
	/// flag is authoritative on its own so a pawn is excluded even on the very first tick, before any
	/// sweep has run — closing the ordering window in which a snapshot could fire against an
	/// unswept bot.
	/// </summary>
	public static bool IsSynthetic( long steamId, bool isDebugPlayer )
	{
		if ( isDebugPlayer )
		{
			return true;
		}

		return IsSynthetic( steamId );
	}

	/// <summary>Count of ids currently treated as synthetic. Diagnostics only.</summary>
	public static int Count
	{
		get
		{
			lock ( Gate )
			{
				return Explicit.Union( Observed ).Count();
			}
		}
	}

	/// <summary>Forget everything. Used between sessions and by tests.</summary>
	public static void Clear()
	{
		lock ( Gate )
		{
			Explicit.Clear();
			Observed.Clear();
		}
	}
}
