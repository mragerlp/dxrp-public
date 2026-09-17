using Dxura.RP.Shared;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public sealed record StoreListReadResult( bool Succeeded, IReadOnlyList<StoreEntryDto> Entries );
public sealed record StoreValueReadResult( bool Succeeded, bool Found, string? Value );

public static partial class ServerApiClient
{
	private static readonly Dictionary<string, (string Value, DateTimeOffset? ExpiresAt)> _mockStore = new();

	public static async Task<List<StoreEntryDto>> ListStore( string? prefix = null )
	{
		prefix = string.IsNullOrWhiteSpace( prefix ) ? null : NormalizeKey( prefix );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			var now = DateTimeOffset.UtcNow;
			var entries = _mockStore
				.Where( entry => entry.Value.ExpiresAt == null || entry.Value.ExpiresAt > now )
				.Where( entry => prefix == null || entry.Key.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
				.Select( entry => new StoreEntryDto { Key = entry.Key, Value = entry.Value.Value, ExpiresAt = entry.Value.ExpiresAt } )
				.OrderBy( entry => entry.Key )
				.ToList();

			return entries;
		}

		return await SafeApiCall( async headers =>
			{
				var url = $"{Constants.ApiBaseUrl}/v1/server/store";
				if ( prefix != null )
				{
					url += $"?prefix={Uri.EscapeDataString( prefix )}";
				}

				var response = await ApiClientBase.RequestAsync(
					url, headers: headers );

				response.EnsureSuccessStatusCode();

				var content = await response.Content.ReadAsStringAsync();
				return JsonSerializer.Deserialize<List<StoreEntryDto>>(
					content,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true } ) ?? [];
			},
			"Failed to list store keys" ) ?? [];
	}

	/// <summary>
	/// Truth-preserving store-list read for callers that must distinguish a confirmed empty list
	/// from SafeApiCall's failure fallback. Existing ListStore behavior remains unchanged.
	/// </summary>
	public static async Task<StoreListReadResult> ReadStoreList( string? prefix = null )
	{
		prefix = string.IsNullOrWhiteSpace( prefix ) ? null : NormalizeKey( prefix );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			var now = DateTimeOffset.UtcNow;
			var entries = _mockStore
				.Where( entry => entry.Value.ExpiresAt == null || entry.Value.ExpiresAt > now )
				.Where( entry => prefix == null || entry.Key.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
				.Select( entry => new StoreEntryDto { Key = entry.Key, Value = entry.Value.Value, ExpiresAt = entry.Value.ExpiresAt } )
				.OrderBy( entry => entry.Key )
				.ToList();
			return new StoreListReadResult( false, entries );
		}

		var result = await SafeApiCall( async headers =>
			{
				var url = $"{Constants.ApiBaseUrl}/v1/server/store";
				if ( prefix != null )
				{
					url += $"?prefix={Uri.EscapeDataString( prefix )}";
				}

				var response = await ApiClientBase.RequestAsync( url, headers: headers );
				response.EnsureSuccessStatusCode();
				var content = await response.Content.ReadAsStringAsync();
				var entries = JsonSerializer.Deserialize<List<StoreEntryDto>>(
					content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );
				return entries == null
					? new StoreListReadResult( false, Array.Empty<StoreEntryDto>() )
					: new StoreListReadResult( true, entries );
			},
			"Failed to list store keys" );

		return result ?? new StoreListReadResult( false, Array.Empty<StoreEntryDto>() );
	}

	public static async Task<string?> GetStore( string key )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			if ( !_mockStore.TryGetValue( key, out var entry ) ) return null;
			if ( entry.ExpiresAt.HasValue && entry.ExpiresAt <= DateTimeOffset.UtcNow ) return null;
			return entry.Value;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}", headers: headers );

				if ( response.StatusCode == HttpStatusCode.NotFound )
				{
					return null;
				}

				response.EnsureSuccessStatusCode();

				var content = await response.Content.ReadAsStringAsync();
				var entry = JsonSerializer.Deserialize<StoreEntryDto>(
					content,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

				return entry?.Value;
			},
			$"Failed to get store key '{key}'" );
	}

	/// <summary>
	/// Truth-preserving single-value read. Succeeded/Found separates a confirmed 404 from a
	/// transport or authorization failure, which GetStore intentionally collapses to null.
	/// </summary>
	public static async Task<StoreValueReadResult> ReadStoreValue( string key )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			if ( !_mockStore.TryGetValue( key, out var entry )
			     || (entry.ExpiresAt.HasValue && entry.ExpiresAt <= DateTimeOffset.UtcNow) )
			{
				return new StoreValueReadResult( false, false, null );
			}

			return new StoreValueReadResult( false, true, entry.Value );
		}

		var result = await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}", headers: headers );
				if ( response.StatusCode == HttpStatusCode.NotFound )
				{
					return new StoreValueReadResult( true, false, null );
				}

				response.EnsureSuccessStatusCode();
				var content = await response.Content.ReadAsStringAsync();
				var value = JsonSerializer.Deserialize<StoreEntryDto>(
					content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );
				return value == null
					? new StoreValueReadResult( false, false, null )
					: new StoreValueReadResult( true, true, value.Value );
			},
			$"Failed to get store key '{key}'" );

		return result ?? new StoreValueReadResult( false, false, null );
	}

	public static async Task SetStore( string key, string value, DateTimeOffset? expiresAt = null )
	{
		await TrySetStore( key, value, expiresAt );
	}

	public static async Task<bool> TrySetStore( string key, string value, DateTimeOffset? expiresAt = null )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			_mockStore[key] = (value, expiresAt);
			return true;
		}

		return await SafeApiCall( async headers =>
			{
				var url = $"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}";
				if ( expiresAt.HasValue )
					url += $"?expiresAt={Uri.EscapeDataString( expiresAt.Value.ToString( "O" ) )}";

				var response = await ApiClientBase.RequestAsync( url, "PUT", Http.CreateJsonContent( value ), headers );
				response.EnsureSuccessStatusCode();
				return true;
			},
			$"Failed to set store key '{key}'" );
	}

	internal static async Task<bool> TrySetStoreStrict( string key, string value, DateTimeOffset? expiresAt = null )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		return await SafeApiCall( async headers =>
			{
				var url = $"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}";
				if ( expiresAt.HasValue )
					url += $"?expiresAt={Uri.EscapeDataString( expiresAt.Value.ToString( "O" ) )}";

				var response = await ApiClientBase.RequestAsync( url, "PUT", Http.CreateJsonContent( value ), headers );
				response.EnsureSuccessStatusCode();
				return true;
			},
			$"Failed to set store key '{key}'" );
	}

	public static async Task DeleteStore( string key )
	{
		await TryDeleteStore( key );
	}

	public static async Task<bool> TryDeleteStore( string key )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			_mockStore.Remove( key );
			return true;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}",
					"DELETE", headers: headers );

				if ( response.StatusCode == HttpStatusCode.NotFound )
				{
					return true;
				}

				response.EnsureSuccessStatusCode();

				return true;
			},
			$"Failed to delete store key '{key}'" );
	}

	internal static async Task<bool> TryDeleteStoreStrict( string key )
	{
		key = NormalizeKey( key );

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return false;
		}

		return await SafeApiCall( async headers =>
			{
				var response = await ApiClientBase.RequestAsync(
					$"{Constants.ApiBaseUrl}/v1/server/store/{Uri.EscapeDataString( key )}",
					"DELETE", headers: headers );

				if ( response.StatusCode == HttpStatusCode.NotFound )
				{
					return true;
				}

				response.EnsureSuccessStatusCode();
				return true;
			},
			$"Failed to delete store key '{key}'" );
	}

	private static string NormalizeKey( string key ) => key.Trim().ToLowerInvariant();

	public static async Task<T?> GetStoreJson<T>( string key )
	{
		var raw = await GetStore( key );
		if ( raw is null ) return default;

		try { return JsonSerializer.Deserialize<T>( raw ); }
		catch { return default; }
	}

	public static async Task SetStoreJson<T>( string key, T value, DateTimeOffset? expiresAt = null )
	{
		await SetStore( key, JsonSerializer.Serialize( value ), expiresAt );
	}

}
