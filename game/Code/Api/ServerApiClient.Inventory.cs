using Dxura.RP.Shared;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public static partial class ServerApiClient
{
	public static async Task<ItemDefinitionDto?> GetItemDefinition( Guid itemId )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return null;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<ItemDefinitionDto>(
					$"{Constants.ApiBaseUrl}/v1/server/inventory/items/{itemId}",
					"GET", headers: headers );

				return response;
			},
			$"Failed to get item definition ({itemId})" );
	}

	public static async Task<InventoryItemDto?> GivePlayerItem( long playerId, GiveItemDto dto )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return null;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// Inventory rows are durable and keyed by SteamId. Returning null is the same neutral shape as
		// the guard above and is already handled by every caller (e.g. ItemEntity.cs:167 null-checks
		// and shows "#notify.inventory.pickup_failed"), so no caller learns a new failure mode.
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) )
		{
			return null;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<InventoryItemDto>(
					$"{Constants.ApiBaseUrl}/v1/server/inventory/{playerId}/give",
					"POST", Http.CreateJsonContent( dto ), headers );

				return response;
			},
			$"Failed to give item to player {playerId}" );
	}

	public static async Task<bool> TakePlayerItem( long playerId, TakeItemDto dto )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// A take against a synthetic actor must not mutate a real account's durable inventory.
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) )
		{
			return false;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/inventory/{playerId}/take",
					"POST", Http.CreateJsonContent( dto ), headers );

				response.EnsureSuccessStatusCode();

				return true;
			},
			$"Failed to take item from player {playerId}" );
	}

	public static async Task<List<InventoryItemDto>?> GetPlayerInventory( long playerId )
	{
		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return null;
		}

		// PRIVACY-INVARIANT: NO SYNTHETIC ACTOR IN HOST PERSISTENCE.
		// This is a READ, so it leaks nothing outward — but it would surface a real third party's
		// private inventory inside the game as though it belonged to the bot wearing their id.
		if ( SyntheticActorRegistry.IsSynthetic( playerId ) )
		{
			return null;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestJsonAsync<List<InventoryItemDto>>(
					$"{Constants.ApiBaseUrl}/v1/server/inventory/{playerId}",
					"GET", headers: headers );

				return response;
			},
			$"Failed to get inventory for player {playerId}" );
	}

}
