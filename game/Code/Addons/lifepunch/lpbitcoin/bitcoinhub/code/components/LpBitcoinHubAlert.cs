// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;

namespace LifePunch.DXRP.Addons.Bitcoin;

public enum LpBitcoinHubAlertKind
{
	TerminalCommand = 0,
	RackCapacity = 1,
	HackAttack = 2,
	HubTransfer = 3,
}

public readonly struct LpBitcoinHubAlert
{
	public LpBitcoinHubAlertKind Kind { get; init; }
	public string Message { get; init; }
	public double Timestamp { get; init; }
}

internal static class LpBitcoinHubAlertCodec
{
	private const char FieldSep = '\x1f';
	private const char RecordSep = '\x1e';
	private const int MaxAlerts = 20;
	private const int MaxMessageLength = 160;

	public static string Serialize( IReadOnlyList<LpBitcoinHubAlert> alerts )
	{
		if ( alerts is null || alerts.Count == 0 )
			return string.Empty;

		var parts = new List<string>( alerts.Count );
		foreach ( var alert in alerts.Take( MaxAlerts ) )
		{
			var message = Sanitize( alert.Message );
			if ( string.IsNullOrWhiteSpace( message ) )
				continue;

			parts.Add( $"{(int)alert.Kind}{FieldSep}{alert.Timestamp:F3}{FieldSep}{message}" );
		}

		return string.Join( RecordSep, parts );
	}

	public static List<LpBitcoinHubAlert> Deserialize( string feed )
	{
		var results = new List<LpBitcoinHubAlert>();
		if ( string.IsNullOrWhiteSpace( feed ) )
			return results;

		foreach ( var record in feed.Split( RecordSep, StringSplitOptions.RemoveEmptyEntries ) )
		{
			var fields = record.Split( FieldSep );
			if ( fields.Length < 3 )
				continue;

			if ( !int.TryParse( fields[0], out var kindValue ) )
				continue;

			if ( !double.TryParse( fields[1], out var timestamp ) )
				continue;

			if ( !Enum.IsDefined( typeof( LpBitcoinHubAlertKind ), kindValue ) )
				continue;

			results.Add( new LpBitcoinHubAlert
			{
				Kind = (LpBitcoinHubAlertKind)kindValue,
				Timestamp = timestamp,
				Message = fields[2],
			} );
		}

		return results;
	}

	public static string Sanitize( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return string.Empty;

		var trimmed = message.Trim()
			.Replace( RecordSep, ' ' )
			.Replace( FieldSep, ' ' )
			.Replace( '\n', ' ' )
			.Replace( '\r', ' ' );

		return trimmed.Length <= MaxMessageLength
			? trimmed
			: trimmed.Substring( 0, MaxMessageLength );
	}
}
