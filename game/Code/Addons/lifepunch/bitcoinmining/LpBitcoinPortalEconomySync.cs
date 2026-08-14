// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Dxura.RP.Game;
using Dxura.RP.Game.Addons;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Host bridge — mirrors portal $BTC backing into <see cref="LpBitcoinEconomy"/> for every peer.
/// Reads portal inventory item <see cref="LpBitcoinIdent.PortalBtcInventoryItemId"/> for optional JSON +
/// token-scoped store overrides. Hub mined BTC rate and portal stack redeem rate are independent.
/// </summary>
[AddonService]
public sealed class LpBitcoinPortalEconomySync : SingletonComponent<LpBitcoinPortalEconomySync>
{
	/// <summary>Portal store — hub wallet $ per 1 mined BTC (wins over item description when set).</summary>
	public const string StoreCashUsdPerBtcKey = "lifepunch:bitcoin:cash_usd_per_btc";

	/// <summary>Portal store — event multiplier on hub cash rate (default 1).</summary>
	public const string StoreCashRateMultiplierKey = "lifepunch:bitcoin:cash_rate_multiplier";

	/// <summary>Portal store — $ paid when player Uses one portal $BTC inventory stack.</summary>
	public const string StorePortalRedeemCashUsdKey = "lifepunch:bitcoin:portal_redeem_cash_usd";

	/// <summary>Portal store — atomic config bundle (BLOCK-0 v1.1). Present + valid fields WIN per-field
	/// over the three legacy scalar keys above; absent → fall back to them + a one-line migration audit.
	/// Read as raw JSON via GetStore (never GetStoreJson — it swallows malformed JSON to default(T), a
	/// silent $0 economy; Packet O FLAG 3).</summary>
	public const string StoreConfigSettingsKey = "lifepunch:bitcoin:config:settings";

	private const float RefreshIntervalSeconds = 300f;

	private RealTimeSince _sinceRefresh;
	private bool _hadAuthorizationKey;
	private bool _refreshInFlight;
	private int _lastAppliedHubBaseUsd = LpBitcoinEconomy.DefaultBitcoinCashUsd;
	private float _lastAppliedHubMultiplier = 1f;
	private int _lastAppliedRedeemUsd = LpBitcoinEconomy.DefaultPortalRedeemCashUsd;

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
		{
			RequestPortalEconomyHost();
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		var hasAuth = ServerApiLink.HasAuthorizationKey;
		if ( hasAuth && !_hadAuthorizationKey )
		{
			_sinceRefresh = RefreshIntervalSeconds;
			_hadAuthorizationKey = true;
			TryScheduleRefresh( "portal auth ready" );
		}
		else if ( !hasAuth )
		{
			_hadAuthorizationKey = false;
		}

		if ( !hasAuth || _refreshInFlight )
		{
			return;
		}

		if ( _sinceRefresh < RefreshIntervalSeconds )
		{
			return;
		}

		_sinceRefresh = 0;
		TryScheduleRefresh( "periodic" );
	}

	[ConCmd( "lp_bitcoin_portal_sync" )]
	public static void ForcePortalSync()
	{
		ForcePortalSyncAfterAuth( "console" );
	}

	/// <summary>Immediate refresh after <c>lp_authorize</c> or manual console sync.</summary>
	public static void ForcePortalSyncAfterAuth( string reason = "auth" )
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			return;
		}

		var sync = Instance;
		if ( !sync.IsValid() )
		{
			return;
		}

		sync._sinceRefresh = RefreshIntervalSeconds;
		sync.TryScheduleRefresh( reason );
	}

	/// <summary>Reload the economy from the portal store WITHOUT writing (BLOCK-0 v1.1) — re-reads the
	/// atomic config bundle + legacy keys and re-applies + broadcasts. The live-tune path: no portal
	/// restart, no store write. Host/server-console only. Packet O note: a [ConCmd] executes host-side
	/// only from the server console (operator trust ≥ economy.manage). The ruled ManageEconomy ||
	/// EditServer RankSystem gate binds a FUTURE in-game /-chat dispatch (which carries a caller
	/// SteamId); the verified command mechanism here is ConCmd — no addon ICommand discovery exists.</summary>
	[ConCmd( "lpbitcoinreloadconfig" )]
	public static void ReloadConfig()
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "lpbitcoinreloadconfig: host only (run from the server console)." );
			return;
		}

		if ( !ServerApiLink.HasAuthorizationKey )
		{
			Log.Warning( "lpbitcoinreloadconfig: portal not authorized (run lp_authorize first)." );
			return;
		}

		var sync = Instance;
		if ( !sync.IsValid() )
		{
			Log.Warning( "lpbitcoinreloadconfig: no sync service instance." );
			return;
		}

		sync._sinceRefresh = RefreshIntervalSeconds;
		sync.TryScheduleRefresh( "reload command" );
		Log.Info( "lpbitcoinreloadconfig: re-reading portal store (atomic config + legacy) and re-applying — no write." );
	}

	/// <summary>Dev override — sets economy locally and broadcasts (does not write portal store).</summary>
	[ConCmd( "lp_bitcoin_portal_rate" )]
	public static void SetPortalRateDev( string args = "" )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_bitcoin_portal_rate: host only." );
			return;
		}

		var parts = (args ?? string.Empty).Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 )
		{
			Log.Info(
				$"lp_bitcoin_portal_rate: hub=${LpBitcoinEconomy.PortalBaseCashUsdPerBtc:N0}/BTC " +
				$"mult={LpBitcoinEconomy.CashRateMultiplier:0.##} effective=${LpBitcoinEconomy.CashUsdPerBtc:N0}/BTC | " +
				$"portal redeem=${LpBitcoinEconomy.PortalRedeemCashUsdPerStack:N0}/stack" );
			return;
		}

		if ( !int.TryParse( parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var baseUsd ) || baseUsd < 0 )
		{
			Log.Warning( "lp_bitcoin_portal_rate <cashUsdPerBtc> [eventMultiplier]" );
			return;
		}

		var multiplier = 1f;
		if ( parts.Length > 1 &&
		     !float.TryParse( parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier ) )
		{
			Log.Warning( "lp_bitcoin_portal_rate <cashUsdPerBtc> [eventMultiplier]" );
			return;
		}

		var sync = Instance;
		if ( !sync.IsValid() )
		{
			LpBitcoinEconomy.ApplyPortalBacking( baseUsd, multiplier );
			Log.Info( $"lp_bitcoin_portal_rate: applied locally (no sync service) — ${baseUsd:N0}/BTC × {multiplier:0.##}" );
			return;
		}

		sync.ApplyAndBroadcastHub( baseUsd, multiplier, "dev override" );
	}

	[ConCmd( "lp_bitcoin_portal_redeem_rate" )]
	public static void SetPortalRedeemRateDev( string args = "" )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_bitcoin_portal_redeem_rate: host only." );
			return;
		}

		var parts = (args ?? string.Empty).Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 )
		{
			Log.Info( $"lp_bitcoin_portal_redeem_rate: ${LpBitcoinEconomy.PortalRedeemCashUsdPerStack:N0} per portal BTC stack" );
			return;
		}

		if ( !int.TryParse( parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var redeemUsd ) || redeemUsd < 0 )
		{
			Log.Warning( "lp_bitcoin_portal_redeem_rate <cashUsdPerStack>" );
			return;
		}

		var sync = Instance;
		if ( !sync.IsValid() )
		{
			LpBitcoinEconomy.ApplyPortalRedeemBacking( redeemUsd );
			Log.Info( $"lp_bitcoin_portal_redeem_rate: applied locally — ${redeemUsd:N0}/stack" );
			return;
		}

		sync.ApplyAndBroadcastRedeem( redeemUsd, "dev override" );
	}

	private void TryScheduleRefresh( string reason )
	{
		if ( _refreshInFlight || !ServerApiLink.HasAuthorizationKey )
		{
			return;
		}

		_refreshInFlight = true;
		_ = RefreshFromPortalAsync( reason );
	}

	private async Task RefreshFromPortalAsync( string reason )
	{
		try
		{
			// Start from last-known-good, not hard defaults, so a failed/malformed store read holds
			// the last applied economy rather than resetting it (Packet O FLAG 3). On the first
			// refresh these equal the shipped defaults (field initializers).
			var hubBaseUsd = _lastAppliedHubBaseUsd;
			var hubMultiplier = _lastAppliedHubMultiplier;
			var redeemUsd = _lastAppliedRedeemUsd;
			var hubBaseFromStore = false;
			var hubMultiplierFromStore = false;
			var redeemFromStore = false;
			var bundlePresent = false;

			// Atomic config bundle (BLOCK-0 v1.1) — wins per-field over the legacy scalar keys. Raw
			// GetStore + JsonDocument; each field positive-validated (a zero/negative/missing field is
			// NOT adopted and falls through to the legacy path). Packet O FLAG 3.
			try
			{
				var storeBundle = await ServerApiClient.GetStore( StoreConfigSettingsKey );
				if ( TryParseConfigBundle( storeBundle, out var bCash, out var bCashOk,
					     out var bMult, out var bMultOk, out var bRedeem, out var bRedeemOk ) )
				{
					bundlePresent = true;
					if ( bCashOk )
					{
						hubBaseUsd = bCash;
						hubBaseFromStore = true;
					}

					if ( bMultOk )
					{
						hubMultiplier = bMult;
						hubMultiplierFromStore = true;
					}

					if ( bRedeemOk )
					{
						redeemUsd = bRedeem;
						redeemFromStore = true;
					}
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[lifepunch.bitcoin] portal store read failed ({StoreConfigSettingsKey}): {ex.Message}" );
			}

			try
			{
				var storeBase = await ServerApiClient.GetStore( StoreCashUsdPerBtcKey );
				if ( !hubBaseFromStore && TryParsePositiveInt( storeBase, out var parsedBase ) )
				{
					hubBaseUsd = parsedBase;
					hubBaseFromStore = true;
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[lifepunch.bitcoin] portal store read failed ({StoreCashUsdPerBtcKey}): {ex.Message}" );
			}

			try
			{
				var storeMult = await ServerApiClient.GetStore( StoreCashRateMultiplierKey );
				if ( !hubMultiplierFromStore && TryParsePositiveFloat( storeMult, out var parsedMult ) )
				{
					hubMultiplier = parsedMult;
					hubMultiplierFromStore = true;
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[lifepunch.bitcoin] portal store read failed ({StoreCashRateMultiplierKey}): {ex.Message}" );
			}

			try
			{
				var storeRedeem = await ServerApiClient.GetStore( StorePortalRedeemCashUsdKey );
				if ( !redeemFromStore && TryParsePositiveInt( storeRedeem, out var parsedRedeem ) )
				{
					redeemUsd = parsedRedeem;
					redeemFromStore = true;
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[lifepunch.bitcoin] portal store read failed ({StorePortalRedeemCashUsdKey}): {ex.Message}" );
			}

			if ( !bundlePresent && ( hubBaseFromStore || hubMultiplierFromStore || redeemFromStore ) )
			{
				Log.Info(
					$"[lifepunch.bitcoin] MIGRATION — using legacy scalar store keys; atomic " +
					$"'{StoreConfigSettingsKey}' absent. Set it (then lpbitcoinreloadconfig) to consolidate; " +
					$"legacy keys retire in a follow-up." );
			}

			ItemDefinitionDto? item = null;
			if ( !hubBaseFromStore || !hubMultiplierFromStore || !redeemFromStore )
			{
				if ( Guid.TryParse( LpBitcoinIdent.PortalBtcInventoryItemId, out var itemId ) )
				{
					try
					{
						item = await ServerApiClient.GetItemDefinition( itemId );
					}
					catch ( Exception ex )
					{
						Log.Warning( $"[lifepunch.bitcoin] portal item fetch failed ({itemId}): {ex.Message}" );
					}
				}
			}

			if ( !hubBaseFromStore && item != null && TryParseHubCashUsdFromItem( item, out var itemHubBase ) )
			{
				hubBaseUsd = itemHubBase;
			}

			if ( !hubMultiplierFromStore && item != null && TryParseHubMultiplierFromItem( item, out var itemHubMult ) )
			{
				hubMultiplier = itemHubMult;
			}

			if ( !redeemFromStore && item != null && TryParseRedeemUsdFromItem( item, out var itemRedeem ) )
			{
				redeemUsd = itemRedeem;
			}

			await GameTask.MainThread();

			if ( !Networking.IsHost )
			{
				return;
			}

			ApplyAndBroadcastHub( hubBaseUsd, hubMultiplier, reason );
			ApplyAndBroadcastRedeem( redeemUsd, reason );
		}
		finally
		{
			_refreshInFlight = false;
		}
	}

	private void ApplyAndBroadcastHub( int baseUsd, float multiplier, string reason )
	{
		var changed = baseUsd != _lastAppliedHubBaseUsd ||
		              MathF.Abs( multiplier - _lastAppliedHubMultiplier ) > 0.0001f;

		_lastAppliedHubBaseUsd = baseUsd;
		_lastAppliedHubMultiplier = multiplier;

		LpBitcoinEconomy.ApplyPortalBacking( baseUsd, multiplier );
		BroadcastPortalEconomyClient( _lastAppliedHubBaseUsd, _lastAppliedHubMultiplier, _lastAppliedRedeemUsd );

		if ( !changed )
		{
			return;
		}

		Log.Info(
			$"[lifepunch.bitcoin] hub economy ({reason}) — ${baseUsd:N0}/BTC × {multiplier:0.##} " +
			$"(effective ${LpBitcoinEconomy.CashUsdPerBtc:N0}/BTC)" );
	}

	private void ApplyAndBroadcastRedeem( int redeemUsd, string reason )
	{
		var changed = redeemUsd != _lastAppliedRedeemUsd;

		_lastAppliedRedeemUsd = redeemUsd;
		LpBitcoinEconomy.ApplyPortalRedeemBacking( redeemUsd );
		BroadcastPortalEconomyClient( _lastAppliedHubBaseUsd, _lastAppliedHubMultiplier, _lastAppliedRedeemUsd );

		if ( !changed )
		{
			return;
		}

		Log.Info(
			$"[lifepunch.bitcoin] portal redeem ({reason}) — ${redeemUsd:N0}/stack " +
			$"(grant {LpBitcoinIdent.PortalBtcRedeemGrantName}) item={LpBitcoinIdent.PortalBtcInventoryItemId}" );
	}

	[Rpc.Host]
	public void RequestPortalEconomyHost()
	{
		BroadcastPortalEconomyClient( _lastAppliedHubBaseUsd, _lastAppliedHubMultiplier, _lastAppliedRedeemUsd );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void BroadcastPortalEconomyClient( int hubBaseUsd, float hubMultiplier, int redeemUsd )
	{
		LpBitcoinEconomy.ApplyPortalBacking( hubBaseUsd, hubMultiplier );
		LpBitcoinEconomy.ApplyPortalRedeemBacking( redeemUsd );
	}

	private static bool TryParseHubCashUsdFromItem( ItemDefinitionDto item, out int cashUsdPerBtc )
	{
		cashUsdPerBtc = 0;

		if ( TryParseEconomyJson( item.Description, out cashUsdPerBtc, out _, out _ ) && cashUsdPerBtc > 0 )
		{
			return true;
		}

		return TryParseCashUsdFromPlainText( item.Description, out cashUsdPerBtc );
	}

	private static bool TryParseHubMultiplierFromItem( ItemDefinitionDto item, out float multiplier )
	{
		multiplier = 1f;

		if ( TryParseEconomyJson( item.Description, out _, out multiplier, out _ ) && multiplier > 0f )
		{
			return true;
		}

		return false;
	}

	private static bool TryParseRedeemUsdFromItem( ItemDefinitionDto item, out int redeemCashUsd )
	{
		redeemCashUsd = 0;

		if ( TryParseEconomyJson( item.Description, out _, out _, out redeemCashUsd ) && redeemCashUsd > 0 )
		{
			return true;
		}

		return TryParseCashUsdFromPlainText( item.GrantIdentifier, out redeemCashUsd );
	}

	private static bool TryParseEconomyJson(
		string? text,
		out int cashUsdPerBtc,
		out float eventMultiplier,
		out int redeemCashUsd )
	{
		cashUsdPerBtc = 0;
		eventMultiplier = 1f;
		redeemCashUsd = 0;

		if ( string.IsNullOrWhiteSpace( text ) )
		{
			return false;
		}

		var trimmed = text.Trim();
		if ( !trimmed.StartsWith( "{", StringComparison.Ordinal ) )
		{
			return false;
		}

		try
		{
			using var doc = JsonDocument.Parse( trimmed );
			var root = doc.RootElement;
			if ( root.ValueKind != JsonValueKind.Object )
			{
				return false;
			}

			if ( TryReadIntProperty( root, "cashUsdPerBtc", out cashUsdPerBtc ) ||
			     TryReadIntProperty( root, "cash_usd_per_btc", out cashUsdPerBtc ) ||
			     TryReadIntProperty( root, "bitcoinCashUsd", out cashUsdPerBtc ) ||
			     TryReadIntProperty( root, "valueUsd", out cashUsdPerBtc ) )
			{
				// parsed base
			}

			if ( TryReadFloatProperty( root, "eventMultiplier", out eventMultiplier ) ||
			     TryReadFloatProperty( root, "cashRateMultiplier", out eventMultiplier ) ||
			     TryReadFloatProperty( root, "cash_rate_multiplier", out eventMultiplier ) )
			{
				// parsed multiplier
			}

			if ( TryReadIntProperty( root, "redeemCashUsd", out redeemCashUsd ) ||
			     TryReadIntProperty( root, "portalRedeemCashUsd", out redeemCashUsd ) ||
			     TryReadIntProperty( root, "portal_redeem_cash_usd", out redeemCashUsd ) )
			{
				// parsed redeem
			}

			return cashUsdPerBtc > 0 ||
			       redeemCashUsd > 0 ||
			       MathF.Abs( eventMultiplier - 1f ) > 0.0001f;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryReadIntProperty( JsonElement root, string name, out int value )
	{
		value = 0;
		if ( !root.TryGetProperty( name, out var prop ) )
		{
			return false;
		}

		if ( prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32( out value ) )
		{
			return true;
		}

		return prop.ValueKind == JsonValueKind.String &&
		       int.TryParse( prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value );
	}

	private static bool TryReadFloatProperty( JsonElement root, string name, out float value )
	{
		value = 0f;
		if ( !root.TryGetProperty( name, out var prop ) )
		{
			return false;
		}

		if ( prop.ValueKind == JsonValueKind.Number && prop.TryGetSingle( out value ) )
		{
			return true;
		}

		return prop.ValueKind == JsonValueKind.String &&
		       float.TryParse( prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value );
	}

	private static bool TryParseCashUsdFromPlainText( string? text, out int cashUsdPerBtc )
	{
		cashUsdPerBtc = 0;
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			return false;
		}

		var span = text.AsSpan();
		for ( var i = 0; i < span.Length; i++ )
		{
			if ( span[i] != '$' )
			{
				continue;
			}

			var start = i + 1;
			var end = start;
			while ( end < span.Length && (char.IsDigit( span[end] ) || span[end] == ',') )
			{
				end++;
			}

			if ( end <= start )
			{
				continue;
			}

			var digits = span[start..end].ToString().Replace( ",", string.Empty, StringComparison.Ordinal );
			if ( int.TryParse( digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out cashUsdPerBtc ) &&
			     cashUsdPerBtc > 0 )
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Parse the atomic config bundle (BLOCK-0 v1.1). Returns false when absent/blank/not a
	/// JSON object (→ caller falls back to legacy keys). Each field's found-flag is true ONLY when
	/// present AND valid (int &gt; 0 for cash/redeem; finite float &gt; 0 for the multiplier) — a
	/// zero/negative/NaN/missing field is REJECTED, never adopted as a silent default (Packet O
	/// FLAG 3). Accepts camelCase or snake_case field names.</summary>
	private static bool TryParseConfigBundle(
		string? raw,
		out int cashUsdPerBtc, out bool cashFound,
		out float cashRateMultiplier, out bool multFound,
		out int portalRedeemCashUsd, out bool redeemFound )
	{
		cashUsdPerBtc = 0;
		cashFound = false;
		cashRateMultiplier = 1f;
		multFound = false;
		portalRedeemCashUsd = 0;
		redeemFound = false;

		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			return false;
		}

		var trimmed = raw.Trim();
		if ( !trimmed.StartsWith( "{", StringComparison.Ordinal ) )
		{
			return false;
		}

		try
		{
			using var doc = JsonDocument.Parse( trimmed );
			var root = doc.RootElement;
			if ( root.ValueKind != JsonValueKind.Object )
			{
				return false;
			}

			if ( ( TryReadIntProperty( root, "cashUsdPerBtc", out var cash ) ||
			       TryReadIntProperty( root, "cash_usd_per_btc", out cash ) ) && cash > 0 )
			{
				cashUsdPerBtc = cash;
				cashFound = true;
			}

			if ( ( TryReadFloatProperty( root, "cashRateMultiplier", out var mult ) ||
			       TryReadFloatProperty( root, "cash_rate_multiplier", out mult ) ) &&
			     mult > 0f && !float.IsNaN( mult ) && !float.IsInfinity( mult ) )
			{
				cashRateMultiplier = mult;
				multFound = true;
			}

			if ( ( TryReadIntProperty( root, "portalRedeemCashUsd", out var redeem ) ||
			       TryReadIntProperty( root, "portal_redeem_cash_usd", out redeem ) ) && redeem > 0 )
			{
				portalRedeemCashUsd = redeem;
				redeemFound = true;
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryParsePositiveInt( string? raw, out int value )
	{
		value = 0;
		return !string.IsNullOrWhiteSpace( raw ) &&
		       int.TryParse( raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value ) &&
		       value > 0;
	}

	private static bool TryParsePositiveFloat( string? raw, out float value )
	{
		value = 0f;
		return !string.IsNullOrWhiteSpace( raw ) &&
		       float.TryParse( raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value ) &&
		       value > 0f;
	}
}
#endif
