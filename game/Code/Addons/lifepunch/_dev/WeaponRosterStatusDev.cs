// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// Read-only audit of the custom-weapon prefab, game-mode roster, and market
/// seams. This never creates placeholders, changes the roster, or equips a
/// weapon; it only reports the state inherited by the current editor session.
/// </summary>
public static class WeaponRosterStatusDev
{
	private const string Command = "lp_weapon_roster_status";

	private sealed record Candidate(
		string Name,
		string WorldPrefabPath,
		string ViewPrefabPath,
		Guid? ExpectedContentId = null,
		Guid? EditorContentId = null );

	private static readonly Candidate[] Candidates =
	[
		new(
			"AK-47",
			"addons/lifepunch/lpweapons/ak47/equipment/w_ak47/w_ak47.prefab",
			"addons/lifepunch/lpweapons/ak47/equipment/vm_ak47/vm_ak47.prefab",
			Ak47DevGive.AkEquipmentId ),
		new(
			"AKS-74U",
			"addons/lifepunch/lpweapons/aks74u/equipment/w_aks74u/w_aks74u.prefab",
			"addons/lifepunch/lpweapons/aks74u/equipment/vm_aks74u/vm_aks74u.prefab" ),
		new(
			"AKS-74U Original",
			"addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab",
			"addons/lifepunch/lpweapons/aks74ucovert/equipment/vm_aks74u_original/vm_aks74u_original.prefab",
			EditorContentId: Aks74uOriginalDevRoster.EditorContentId ),
		new(
			"AR-15",
			"addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab",
			"addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab" ),
		new(
			"Desert Eagle",
			"addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab",
			"addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab" ),
		new(
			"SR-25",
			"addons/lifepunch/lpweapons/sr25/equipment/w_sr25/w_sr25.prefab",
			"addons/lifepunch/lpweapons/sr25/equipment/vm_sr25/vm_sr25.prefab" ),
		new(
			"M1911",
			"addons/lifepunch/lpweapons/m1911/equipment/w_m1911/w_m1911.prefab",
			"addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/vm_m1911.prefab" ),
		new(
			"Glock",
			"addons/lifepunch/lpweapons/glock/equipment/w_glock/w_glock.prefab",
			"addons/lifepunch/lpweapons/glock/equipment/vm_glock/vm_glock.prefab" ),
		new(
			"M870",
			"addons/lifepunch/lpweapons/m870/equipment/w_m870/w_m870.prefab",
			"addons/lifepunch/lpweapons/m870/equipment/vm_m870/vm_m870.prefab" )
	];

	[ConCmd( Command )]
	public static void Report()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"{Command}: editor-only." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( $"{Command}: must be host (editor play)." );
			return;
		}

		var rosterReady = 0;
		var marketReady = 0;

		foreach ( var candidate in Candidates )
		{
			var worldAssetReady = GameObject.GetPrefab( candidate.WorldPrefabPath ).IsValid();
			var viewPrefab = GameObject.GetPrefab( candidate.ViewPrefabPath );
			var viewAssetReady = viewPrefab.IsValid() &&
				viewPrefab.Components.Get<ViewModel>().IsValid();
			var matchingEquipmentRows = GameModeEquipments.All
				.Where( row => string.Equals(
					row.PrefabPath(),
					candidate.WorldPrefabPath,
					StringComparison.OrdinalIgnoreCase ) )
				.ToArray();
			var rosterRowReady = matchingEquipmentRows.Length == 1;
			var equipment = rosterRowReady ? matchingEquipmentRows[0] : null;
			var matchingContentRows = rosterRowReady
				? GameModeEquipments.All
					.Where( row => row.GameModeAddonContentId ==
						equipment!.GameModeAddonContentId )
					.ToArray()
				: [];
			var contentIdUnique = rosterRowReady &&
				matchingContentRows.Length == 1 &&
				matchingContentRows[0].Id == equipment!.Id;
			var expectedContentIdReady = rosterRowReady &&
				(!candidate.ExpectedContentId.HasValue ||
				 equipment!.GameModeAddonContentId == candidate.ExpectedContentId.Value);
			var contentIdReady = contentIdUnique && expectedContentIdReady;
			var editorInjected = rosterRowReady &&
				candidate.EditorContentId.HasValue &&
				equipment!.GameModeAddonContentId == candidate.EditorContentId.Value;
			var secondaryReady = rosterRowReady && string.Equals(
				equipment!.SecondaryPrefabPath(),
				candidate.ViewPrefabPath,
				StringComparison.OrdinalIgnoreCase );

			var matchingEquipmentRowIds = matchingEquipmentRows
				.Select( row => row.Id )
				.ToHashSet();
			var marketRows = GameModeMarketItems.All
				.Where( item => item.Type == GameModeMarketItemType.Equipment &&
					item.ReferenceId.HasValue &&
					matchingEquipmentRowIds.Contains( item.ReferenceId.Value ) )
				.ToArray();
			var spawnableRows = marketRows.Count( GameModeMarketItems.IsSpawnable );
			var firstMissingRequirement =
				!worldAssetReady || !rosterRowReady ? "worldPrefab" :
				!viewAssetReady || !secondaryReady ? "viewPrefab" :
				!contentIdReady ? "uniqueContentMapping" :
				spawnableRows == 0 ? "marketRow" :
				"none";

			var readyForEquip = worldAssetReady && viewAssetReady && rosterRowReady &&
				contentIdReady && secondaryReady;
			var readyForMarket = readyForEquip && !editorInjected && spawnableRows > 0;
			if ( readyForEquip ) rosterReady++;
			if ( readyForMarket ) marketReady++;

			var contentId = rosterRowReady
				? equipment!.GameModeAddonContentId.ToString()
				: "none";
			var equipmentRowId = rosterRowReady ? equipment!.Id.ToString() : "none";
			var matchingRosterRowIds = matchingEquipmentRows.Length > 0
				? string.Join( ",", matchingEquipmentRows.Select( row => row.Id ) )
				: "none";
			var marketItemIds = marketRows.Length > 0
				? string.Join( ",", marketRows.Select( item => item.Id ) )
				: "none";
			Log.Info(
				$"LP_WEAPON_ROSTER weapon=\"{candidate.Name}\" " +
				$"worldAsset={worldAssetReady} viewAsset={viewAssetReady} " +
				$"matchingRosterRows={matchingEquipmentRows.Length} rosterRow={rosterRowReady} " +
				$"matchingContentRows={matchingContentRows.Length} " +
				$"contentIdUnique={contentIdUnique} " +
				$"expectedContentIdMatch={expectedContentIdReady} contentIdMatch={contentIdReady} " +
				$"editorInjected={editorInjected} " +
				$"secondaryMatch={secondaryReady} marketRows={marketRows.Length} " +
				$"spawnableMarketRows={spawnableRows} firstMissing={firstMissingRequirement} " +
				$"contentId={contentId} " +
				$"equipmentRowId={equipmentRowId} matchingRosterRowIds={matchingRosterRowIds} " +
				$"marketItemIds={marketItemIds}" );
		}

		Log.Info(
			$"LP_WEAPON_ROSTER_SUMMARY candidates={Candidates.Length} " +
			$"equipReady={rosterReady} marketReady={marketReady}" );
	}
}
