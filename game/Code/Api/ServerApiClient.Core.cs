using Dxura.RP.Shared;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

internal enum StrictMoneyMutationResult
{
	Rejected,
	Applied,
	Unknown
}

public static partial class ServerApiClient
{
	public static async Task<GameModeDto?> FetchDefaultGameMode()
	{
		return await ApiClientBase.SafeApiCall(
			() => ApiClientBase.RequestJsonAsync<GameModeDto>( $"{Constants.ApiBaseUrl}/v1/public/gamemode/default" ),
			"Failed to fetch default game mode" );
	}

	public static async Task<InitalizeServerResponseDto?> InitializeServer( InitalizeServerDto initalize )
	{
		return await SafeApiCall<InitalizeServerResponseDto?>( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<InitalizeServerResponseDto>(
					$"{Constants.ApiBaseUrl}/v1/server/initialize",
					"POST", Http.CreateJsonContent( initalize ), headers );

				return response;
			},
			"Failed to initialize server with API" );
	}

	public static async Task<InitalizePlayerResponseDto?> InitializePlayer( InitalizePlayerDto initalize )
	{
		return await SafeApiCall<InitalizePlayerResponseDto?>( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<InitalizePlayerResponseDto>(
					$"{Constants.ApiBaseUrl}/v1/server/player/initialize",
					"POST", Http.CreateJsonContent( initalize ), headers );

				return response;
			},
			$"Failed to initialize player (Name: {initalize.Name})" );
	}

	public static async Task<ServerPulseResponseDto?> Pulse( ServerPulseDto pulse )
	{
		return await SafeApiCall<ServerPulseResponseDto?>( async headers =>
			{
				var httpResponse = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/pulse", "POST", Http.CreateJsonContent( pulse ), headers );

				httpResponse.EnsureSuccessStatusCode();
				var body = await httpResponse.Content.ReadAsStringAsync();
				
				// HACK: Fixes unrecognized type discriminator id 'set_level'. etc when new actions are added without restart.
				return ParsePulseResponse( body );
			},
			"Failed to pulse server" );
	}

	private static readonly JsonSerializerOptions PulseOptions = new() { PropertyNameCaseInsensitive = true };

	private static ServerPulseResponseDto ParsePulseResponse( string body )
	{
		var node = JsonNode.Parse( body )!.AsObject();

		var safeActions = node["pendingActions"]!.AsArray()
			.Select( e => { try { return e.Deserialize<ServerActionDto>( PulseOptions ); } catch ( JsonException ) { return null; } } )
			.OfType<ServerActionDto>()
			.ToList();

		node["pendingActions"] = JsonSerializer.SerializeToNode( safeActions, PulseOptions );

		return node.Deserialize<ServerPulseResponseDto>( PulseOptions )!;
	}

	// Preserve the original CLR signature for automatic/system callers and already-compiled addons.
	// Those call sites attribute the violation to the subject itself.
	public static Task<bool> SanctionPlayer( long playerId, CreateSanctionDto sanction )
		=> SanctionPlayer( playerId, sanction, playerId );

	public static async Task<bool> SanctionPlayer( long playerId, CreateSanctionDto sanction, long actorSteamId )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC SUBJECT OR ACTOR IN HOST PERSISTENCE.
		// The subject owns the durable record; the actor is the identity represented in its notes.
		// Either one borrowing a public Steam identity would create a false portal record.
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) || SyntheticActorRegistry.IsSynthetic( actorSteamId ) )
		{
			return false;
		}

		var succeeded = await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/moderation/sanction/{playerId}",
					"POST",
					Http.CreateJsonContent( sanction ),
					headers );
				response.EnsureSuccessStatusCode();

				return true;
			},
			$"Failed to sanction player {playerId} for {sanction.Reason}" );

		if ( succeeded )
		{
			PlayerSanctionHistorySystem.Current?.InvalidateCachedSanctions( playerId );
		}

		return succeeded;
	}

	public static bool Audit( string action, string description, long? cause = null )
	{
		LocalAuditStore.Record( action, description, cause );

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// The remote POST below is the durable copy (see LocalAuditStore's summary). When the ACTOR
		// is a synthetic actor, suppress it so a non-person's conduct is never written to the portal
		// audit trail as though a human performed it. The in-memory ring above still records the row,
		// so in-session staff/dev visibility is unchanged.
		// RESIDUAL, DELIBERATELY NOT FIXED HERE: `description` is composed by ~86 call sites and can
		// embed a synthetic TARGET's name and id even when `cause` is a human (e.g. a staff member
		// teleporting to a test bot). Scrubbing that requires editing every call site and is outside
		// this change's scope — it is reported in the return, not silently absorbed.
		if ( cause.HasValue && SyntheticActorRegistry.IsSynthetic( cause.Value ) )
		{
			return false;
		}

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			Log.Info( $"[{action}] {description}" );
			return false;
		}
		
		return TryQueueApiCall( async headers =>
			{
				Log.Info( $"[{action}] {description}" );

				var url = $"{Constants.ApiBaseUrl}/v1/server/audit?action={action.UrlEncode()}&description={description.UrlEncode()}";
				if ( cause.HasValue )
				{
					url += $"&cause={cause.Value}";
				}

				var response = await ApiClientBase.RequestAsync( url, "POST", headers: headers );
				response.EnsureSuccessStatusCode();
			},
			$"Failed to audit (Action {action} Description {description})" );
	}

	internal static bool TryQueueAuditStrict( string action, string description, long cause )
	{
		if ( SyntheticActorRegistry.IsSynthetic( cause ) || !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		var queued = TryQueueApiCall( async headers =>
			{
				Log.Info( $"[{action}] {description}" );
				var url = $"{Constants.ApiBaseUrl}/v1/server/audit?action={action.UrlEncode()}&description={description.UrlEncode()}&cause={cause}";
				var response = await ApiClientBase.RequestAsync( url, "POST", headers: headers );
				response.EnsureSuccessStatusCode();
			},
			$"Failed to audit (Action {action} Description {description})" );

		if ( queued )
		{
			LocalAuditStore.Record( action, description, cause );
		}

		return queued;
	}

	// Preserve the self-service/compiled-addon signature; self-service acts as its own subject.
	public static Task<bool> SetRpName( long steamId, string? rpName )
		=> SetRpName( steamId, rpName, steamId );

	public static async Task<bool> SetRpName( long steamId, string? rpName, long actorSteamId )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC SUBJECT OR ACTOR IN HOST PERSISTENCE.
		// RP-name commands promise durable portal state, so an intentionally skipped synthetic write is
		// reported as failure rather than being presented to staff as a successful persistence action.
		if ( SyntheticActorRegistry.IsSynthetic( steamId ) || SyntheticActorRegistry.IsSynthetic( actorSteamId ) )
		{
			return false;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<bool>(
					$"{Constants.ApiBaseUrl}/v1/server/players/{steamId}/rpname",
					"POST",
					Http.CreateJsonContent( rpName ),
					headers );

				return response;
			},
			$"Failed to set RP name for player ({steamId})" );
	}

	public static Task<bool> ModifyPlayerBalance( long steamId, int amount, string reason )
		=> ModifyPlayerBalanceCore( steamId, amount, reason, allowNeutralLocalBookkeeping: true );

	internal static async Task<StrictMoneyMutationResult> ModifyPlayerBalanceStrict( long steamId, int amount, string reason )
	{
		if ( !ServerApiLink.HasAuthorizationKey
		     || !Config.Current.Game.MoneyEnabled
		     || SyntheticActorRegistry.IsSynthetic( steamId ) )
		{
			return StrictMoneyMutationResult.Rejected;
		}

		var headers = GetAuthHeaders();
		try
		{
			var applied = await GameTask.RunInThreadAsync( () => ApiClientBase.RequestJsonAsync<bool>(
				$"{Constants.ApiBaseUrl}/v1/server/players/{steamId}/balance?amount={amount}&reason={reason.UrlEncode()}",
				"POST",
				headers: headers ) );
			return applied ? StrictMoneyMutationResult.Applied : StrictMoneyMutationResult.Rejected;
		}
		catch ( Exception e )
		{
			Log.Error( $"Failed to modify player ({steamId}) balance (Amount: {amount}): {e.Message}" );
			return StrictMoneyMutationResult.Unknown;
		}
	}

	private static async Task<bool> ModifyPlayerBalanceCore(
		long steamId,
		int amount,
		string reason,
		bool allowNeutralLocalBookkeeping )
	{
		if ( !ServerApiLink.HasAuthorizationKey || !Config.Current.Game.MoneyEnabled )
		{
			return allowNeutralLocalBookkeeping;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// MONEY-INVARIANT: DEBIT-FIRST; ADDITIVE-RESTORE; NEVER-NEGATIVE — UNAFFECTED.
		// Returning true matches the neutral shape of the guard above: the caller's LOCAL bookkeeping
		// (Player.ChargeHost / PayHost adjusting BankBalance) proceeds exactly as it does today with
		// money disabled, so no currency is minted or destroyed — the transient pawn's balance simply
		// never reaches a real account's durable ledger.
		if ( SyntheticActorRegistry.IsSynthetic( steamId ) )
		{
			return allowNeutralLocalBookkeeping;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<bool>(
					$"{Constants.ApiBaseUrl}/v1/server/players/{steamId}/balance?amount={amount}&reason={reason.UrlEncode()}",
					"POST",
					headers: headers );

				return response;
			},
			$"Failed to modify player ({steamId}) balance (Amount: {amount})" );
	}

	public static async Task<List<PlayerSanctionHistoryDto>?> GetPlayerSanctions( long playerId )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return null;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// A READ, so nothing leaks outward — but without this a staff member inspecting a test bot
		// would be shown the real moderation history of the third party whose account id the bot wears.
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) )
		{
			return null;
		}

		var result = await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<List<PlayerSanctionHistoryDto>>(
					$"{Constants.ApiBaseUrl}/v1/server/players/{playerId}/sanctions",
					"GET",
					headers: headers );

				return response;
			},
			$"Failed to get sanctions for player {playerId}" );

		return result;
	}
}
