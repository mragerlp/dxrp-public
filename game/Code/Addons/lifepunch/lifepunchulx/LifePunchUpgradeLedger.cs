// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// One committed purchase. Append-only — the record is never updated or deleted;
/// integer money only (long satoshis). Float or an update path here = doctrine violation
/// (UPGRADE_ARC_DESIGN decision 7; slice 1 GO ruling).
/// </summary>
public sealed class LifePunchPurchaseRecord
{
	/// <summary>Purchase owner (SteamID64) — the ledger key. Never an object ref.</summary>
	public long OwnerSteamId { get; set; }

	public string TrackId { get; set; }

	/// <summary>Slot token for Slot-kind tracks (e.g. "gpurack-1"); empty for Global.</summary>
	public string SubjectId { get; set; }

	/// <summary>Subject class guard — apply honors tier ONLY on class match (GO ruling R1,
	/// closes the standard→advanced occupant swap). Empty = classless track.</summary>
	public string SubjectClass { get; set; }

	public int Tier { get; set; }
	public long CostSats { get; set; }
	public long CommittedUtcTicks { get; set; }
	public int Seq { get; set; }
}

/// <summary>
/// The purchase ledger — landlord-grade, trackId-scoped, SteamID-keyed, host-only,
/// append-only. THE source of truth for upgrade ownership; component tier state is a
/// rehydrated projection that reconciles against this ledger (ledger wins,
/// UPGRADE_ARC_DESIGN decision 9). Storage is host FileSystem.Data, flushed at every
/// commit — deliberately independent of the snapshot cadence (crash-dupe firewall).
/// Contains ZERO track-specific logic: rack_compute is tenant #1, not owner.
/// </summary>
public static class LifePunchUpgradeLedger
{
	// One JSON array document, rewritten+flushed whole at every commit. NOT line-per-record
	// JSONL: s&box Json.Serialize has no compact mode (gate-1 finding, 2026-07-08 — pretty-
	// printed records made the old .jsonl unparseable on the first true disk reload).
	private const string LedgerFile = "lifepunch-upgrade-ledger.json";

	private static List<LifePunchPurchaseRecord> _records;

	/// <summary>Scene identity the cache was loaded under. FileSystem.Data resolves
	/// differently per context (editor menu vs game session) — a load latched in one
	/// context must never serve another (gate-1 finding, 2026-07-08: a pre-play read
	/// cached an empty ledger into the play session). Guid, never a Scene ref.</summary>
	private static Guid _loadedSceneId;

	/// <summary>Highest committed tier for (owner, track, subject, class); 0 = none.</summary>
	public static int MaxTier( long ownerSteamId, string trackId, string subjectId, string subjectClass )
	{
		if ( !Networking.IsHost || ownerSteamId == 0 || string.IsNullOrWhiteSpace( trackId ) )
			return 0;

		EnsureLoaded();

		var max = 0;
		foreach ( var record in _records )
		{
			if ( record.OwnerSteamId != ownerSteamId )
				continue;
			if ( !string.Equals( record.TrackId, trackId, StringComparison.Ordinal ) )
				continue;
			if ( !string.Equals( record.SubjectId ?? string.Empty, subjectId ?? string.Empty, StringComparison.Ordinal ) )
				continue;
			if ( !string.Equals( record.SubjectClass ?? string.Empty, subjectClass ?? string.Empty, StringComparison.Ordinal ) )
				continue;

			if ( record.Tier > max )
				max = record.Tier;
		}

		return max;
	}

	/// <summary>
	/// Validate → append+flush → raise OnPurchase (commit-then-raise; the event announces
	/// a fact). Idempotency = the sequential-tier precondition: only currentMax+1 commits;
	/// re-fired requests and tier-skips both reject. Charging is NOT done here — and
	/// neither is PRICING: <paramref name="costSats"/> is the cost the caller actually
	/// charged (quote-time yield multipliers included). The ledger records and announces
	/// what was PAID (gate-2 finding 2026-07-09: internal base-ladder pricing under-
	/// recorded Advanced purchases by half).
	/// </summary>
	public static bool TryCommitPurchase(
		long ownerSteamId,
		string trackId,
		string subjectId,
		string subjectClass,
		int tier,
		long costSats,
		Guid purchaserConnectionId,
		out string error )
	{
		error = string.Empty;

		if ( !Networking.IsHost )
		{
			error = "ledger is host-only";
			return false;
		}

		if ( ownerSteamId == 0 )
		{
			error = "no owner SteamID";
			return false;
		}

		var track = LifePunchUpgradeTracks.Get( trackId );
		if ( track is null )
		{
			error = $"unknown track '{trackId}'";
			return false;
		}

		if ( track.SubjectKind == LifePunchTrackSubjectKind.Slot && string.IsNullOrWhiteSpace( subjectId ) )
		{
			error = $"track '{trackId}' requires a subject slot";
			return false;
		}

		if ( tier < 1 || tier > track.MaxTier )
		{
			error = $"tier {tier} outside 1..{track.MaxTier}";
			return false;
		}

		var current = MaxTier( ownerSteamId, trackId, subjectId, subjectClass );
		if ( tier != current + 1 )
		{
			error = $"sequential precondition: current tier {current}, requested {tier}";
			return false;
		}

		if ( costSats < 0 )
		{
			error = $"invalid cost {costSats}";
			return false;
		}

		EnsureLoaded();

		var record = new LifePunchPurchaseRecord
		{
			OwnerSteamId = ownerSteamId,
			TrackId = trackId,
			SubjectId = subjectId ?? string.Empty,
			SubjectClass = subjectClass ?? string.Empty,
			Tier = tier,
			CostSats = costSats,
			CommittedUtcTicks = DateTime.UtcNow.Ticks,
			Seq = _records.Count,
		};

		// Transactional order: persist first; only a flushed record is a fact.
		_records.Add( record );
		if ( !TrySaveAll() )
		{
			_records.RemoveAt( _records.Count - 1 );
			error = "ledger flush failed — purchase not committed";
			return false;
		}

		RaisePurchaseEvent( record, purchaserConnectionId );
		return true;
	}

	/// <summary>
	/// Ledger-wins reconcile for a rehydrated tier projection. Returns the ledger tier;
	/// logs a discrepancy line when the persisted projection disagrees (deliberate
	/// corruption, stale snapshot, or class mismatch all clamp here).
	/// </summary>
	public static int ReconcileTier( int persistedTier, int ledgerTier, string contextLabel )
	{
		if ( persistedTier != ledgerTier )
		{
			Log.Warning(
				$"LP_UPGRADE_RECONCILE {contextLabel} projection={persistedTier} ledger={ledgerTier} — clamped to ledger" );
		}

		return ledgerTier;
	}

	/// <summary>Debug/proof view — copy, not the live list.</summary>
	public static IReadOnlyList<LifePunchPurchaseRecord> GetRecords()
	{
		if ( !Networking.IsHost )
			return Array.Empty<LifePunchPurchaseRecord>();

		EnsureLoaded();
		return _records.ToList();
	}

	private static void RaisePurchaseEvent( LifePunchPurchaseRecord record, Guid purchaserConnectionId )
	{
		var costBtc = record.CostSats / 100_000_000f;
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.GetPlayerByConnectionId( purchaserConnectionId );
		ILifePunchPurchaseEvent.Post( x => x.OnPurchase( player, record.TrackId, record.Tier, costBtc ) );
#else
		ILifePunchPurchaseEvent.Post( x => x.OnPurchase( null, record.TrackId, record.Tier, costBtc ) );
#endif
	}

	private static void EnsureLoaded()
	{
		var sceneId = Game.ActiveScene?.Id ?? Guid.Empty;
		if ( _records is not null && _loadedSceneId == sceneId )
			return;

		_loadedSceneId = sceneId;
		_records = new List<LifePunchPurchaseRecord>();

		try
		{
			if ( !FileSystem.Data.FileExists( LedgerFile ) )
				return;

			var raw = FileSystem.Data.ReadAllText( LedgerFile );
			if ( string.IsNullOrWhiteSpace( raw ) )
				return;

			var loaded = Json.Deserialize<List<LifePunchPurchaseRecord>>( raw );
			if ( loaded is not null )
				_records.AddRange( loaded.Where( r => r is not null ) );

			Log.Info( $"LP_UPGRADE_LEDGER loaded {_records.Count} record(s)" );
		}
		catch ( Exception e )
		{
			Log.Error( $"LP_UPGRADE_LEDGER load failed: {e.Message}" );
		}
	}

	private static bool TrySaveAll()
	{
		try
		{
			FileSystem.Data.WriteAllText( LedgerFile, Json.Serialize( _records ) );
			return true;
		}
		catch ( Exception e )
		{
			Log.Error( $"LP_UPGRADE_LEDGER flush failed: {e.Message}" );
			return false;
		}
	}
}
