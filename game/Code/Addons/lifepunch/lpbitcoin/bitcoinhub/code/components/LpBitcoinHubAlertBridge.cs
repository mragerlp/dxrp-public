// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Cross-lane entry for hacker vengeance ops — surfaces intrusion attempts on the HASHD hub alert feed.</summary>
public static class LpBitcoinHubAlertBridge
{
	public const string ScanIdPrefix = "hashd-";

	public static string ToScanId( LpBitcoinHubEntity hub )
		=> hub.IsValid() ? $"{ScanIdPrefix}{hub.GameObject.Id}" : string.Empty;

	public static LpBitcoinHubEntity ResolveFromScanId( string scanId )
	{
		if ( string.IsNullOrWhiteSpace( scanId ) || !scanId.StartsWith( ScanIdPrefix, StringComparison.OrdinalIgnoreCase ) )
			return null;

		if ( !Guid.TryParse( scanId.AsSpan( ScanIdPrefix.Length ), out var hubId ) )
			return null;

		var scene = Game.ActiveScene;
		if ( scene is null )
			return null;

		return LpBitcoinHubEntity.FindByGameObjectId( hubId, scene );
	}

	public static void NotifyHubUnderAttack( LpBitcoinHubEntity hub, string attackerLabel )
	{
		if ( !hub.IsValid() )
			return;

		var label = string.IsNullOrWhiteSpace( attackerLabel ) ? "unknown operator" : attackerLabel.Trim();
		hub.ReportHackAttempt( label );
	}
}
