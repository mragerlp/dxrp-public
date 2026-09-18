using Dxura.RP.Shared;

namespace Dxura.RP.Game;

public sealed class PlayerSanctionHistorySystem : GameObjectSystem<PlayerSanctionHistorySystem>
{
	private const float CacheDurationSeconds = 60f;
	private readonly List<PlayerSanctionHistoryDto> _visibleSanctions = [];
	private readonly Dictionary<long, CachedSanctionsEntry> _cachedSanctions = [];

	public PlayerSanctionHistorySystem( Scene scene ) : base( scene )
	{
	}

	public IReadOnlyList<PlayerSanctionHistoryDto> VisibleSanctions => _visibleSanctions;
	public int ClientRevision { get; private set; }
	public long? CurrentPlayerId { get; private set; }
	public Guid CurrentRequestId { get; private set; }
	public bool IsLoading { get; private set; }
	public bool CurrentRequestFailed { get; private set; }
	public bool CurrentRequestRefused { get; private set; }
	public bool CurrentResultMayBeFiltered { get; private set; }

	public void InvalidateCachedSanctions( long playerId )
	{
		_cachedSanctions.Remove( playerId );
	}

	public void BeginLoadingClient( long playerId, Guid requestId )
	{
		CurrentPlayerId = playerId;
		CurrentRequestId = requestId;
		IsLoading = true;
		CurrentRequestFailed = false;
		CurrentRequestRefused = false;
		CurrentResultMayBeFiltered = false;
		_visibleSanctions.Clear();
		ClientRevision++;
	}

	public void ClearVisibleSanctionsClient()
	{
		if ( CurrentPlayerId == null && CurrentRequestId == Guid.Empty && !IsLoading && _visibleSanctions.Count == 0 )
		{
			return;
		}

		CurrentPlayerId = null;
		CurrentRequestId = Guid.Empty;
		IsLoading = false;
		CurrentRequestFailed = false;
		CurrentRequestRefused = false;
		CurrentResultMayBeFiltered = false;
		_visibleSanctions.Clear();
		ClientRevision++;
	}

	[Rpc.Host]
	public async void RequestSanctionsHost( long playerId, Guid requestId )
	{
		var callerConnection = Rpc.Caller;
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) )
		{
			SendSanctionsOutcome( callerConnection, playerId, requestId, refused: true, failed: false );
			return;
		}

		var caller = GetValidCaller( callerConnection, playerId );
		if ( caller == null || caller.Connection == null )
		{
			SendSanctionsOutcome( callerConnection, playerId, requestId, refused: true, failed: false );
			return;
		}

		var sanctions = GetCachedSanctions( playerId );
		if ( sanctions == null )
		{
			var fetched = await ServerApiClient.GetPlayerSanctions( playerId );
			if ( fetched == null )
			{
				await GameTask.MainThread();
				SendSanctionsOutcome( callerConnection, playerId, requestId, refused: false, failed: true );
				return;
			}

			sanctions = [.. fetched];
			_cachedSanctions[playerId] = new CachedSanctionsEntry( sanctions );
		}

		await GameTask.MainThread();

		if ( !caller.IsValid() || caller.Connection == null
		     || !CanViewSanctions( caller.SteamId, playerId, RankSystem.HasPermission ) )
		{
			SendSanctionsOutcome( callerConnection, playerId, requestId, refused: true, failed: false );
			return;
		}

		var visibleSanctions = BuildVisibleSanctions( caller.SteamId, sanctions );
		var mayBeFiltered = visibleSanctions.Length < sanctions.Length;

		using ( Rpc.FilterInclude( c => c.Id == caller.ConnectionId ) )
		{
			BroadcastVisibleSanctionsClient( playerId, requestId, visibleSanctions, mayBeFiltered );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void BroadcastVisibleSanctionsClient(
		long playerId,
		Guid requestId,
		PlayerSanctionHistoryDto[] sanctions,
		bool mayBeFiltered )
	{
		if ( requestId != CurrentRequestId )
		{
			return;
		}

		CurrentPlayerId = playerId;
		IsLoading = false;
		CurrentRequestFailed = false;
		CurrentRequestRefused = !CanViewRequestedSanctions( playerId );
		CurrentResultMayBeFiltered = !CurrentRequestRefused && mayBeFiltered;
		_visibleSanctions.Clear();
		if ( !CurrentRequestRefused )
		{
			_visibleSanctions.AddRange( sanctions );
		}
		ClientRevision++;
	}

	private void SendSanctionsOutcome(
		Connection caller,
		long playerId,
		Guid requestId,
		bool refused,
		bool failed )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			BroadcastSanctionsOutcomeClient( playerId, requestId, refused, failed );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void BroadcastSanctionsOutcomeClient(
		long playerId,
		Guid requestId,
		bool refused,
		bool failed )
	{
		if ( requestId != CurrentRequestId )
		{
			return;
		}

		CurrentPlayerId = playerId;
		IsLoading = false;
		CurrentRequestRefused = refused;
		CurrentRequestFailed = failed;
		CurrentResultMayBeFiltered = false;
		_visibleSanctions.Clear();
		ClientRevision++;
	}

	private PlayerSanctionHistoryDto[]? GetCachedSanctions( long playerId )
	{
		if ( !_cachedSanctions.TryGetValue( playerId, out var entry ) )
		{
			return null;
		}

		if ( entry.CachedSince.Relative > CacheDurationSeconds )
		{
			_cachedSanctions.Remove( playerId );
			return null;
		}

		return entry.Sanctions;
	}

	private static PlayerSanctionHistoryDto[] BuildVisibleSanctions( long callerId, IEnumerable<PlayerSanctionHistoryDto> sanctions )
	{
		var canViewPrivilegedSanctions =
			RankSystem.HasPermission( callerId, Permission.ViewSanctionNotes );

		return sanctions
			.Where( sanction => canViewPrivilegedSanctions || (sanction.Flags & SanctionFlags.Privileged) == 0 )
			.Select( sanction => new PlayerSanctionHistoryDto
			{
				Created = sanction.Created,
				Type = sanction.Type,
				IsGlobal = sanction.IsGlobal,
				State = sanction.State,
				Flags = sanction.Flags,
				Duration = sanction.Duration,
				Reason = sanction.Reason,
				Notes = canViewPrivilegedSanctions ? sanction.Notes : null
			} )
			.ToArray();
	}

	private static bool CanViewRequestedSanctions( long playerId )
	{
		return CanViewSanctions( Player.Local.SteamId, playerId, ( _, permission ) => RankSystem.HasLocalPermission( permission ) );
	}

	private static Player? GetValidCaller( Connection callerConnection, long playerId )
	{
		if ( !CanViewSanctions( callerConnection.SteamId, playerId, RankSystem.HasPermission ) )
		{
			return null;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerConnection.Id );
		return caller.IsValid() ? caller : null;
	}

	private static bool CanViewSanctions( long callerId, long playerId, Func<long, Permission, bool> hasPermission )
	{
		if ( callerId == playerId )
		{
			return hasPermission( callerId, Permission.ViewOwnSanctions ) ||
			       hasPermission( callerId, Permission.ViewOtherSanctions );
		}

		return hasPermission( callerId, Permission.ViewOtherSanctions );
	}

	private sealed class CachedSanctionsEntry( PlayerSanctionHistoryDto[] sanctions )
	{
		public TimeSince CachedSince { get; } = 0f;
		public PlayerSanctionHistoryDto[] Sanctions { get; } = sanctions;
	}
}
