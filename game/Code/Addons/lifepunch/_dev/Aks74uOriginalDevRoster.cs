// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// Isolated editor-session roster seam for the Original AKS-74U. It prefers an
/// exact live row and otherwise creates one in-memory row only after both
/// product prefabs exist. It never creates Portal or market state.
/// </summary>
public static class Aks74uOriginalDevRoster
{
	public static readonly Guid EditorContentId = new( "409a0940-5db7-4e7c-b8be-9413f17739e6" );
	public static readonly Guid EditorEquipmentRowId = new( "fca594b9-efea-4d8e-b80a-67c5b7160b14" );

	public const string WorldPrefabPath =
		"addons/lifepunch/lpweapons/aks74ucovert/equipment/w_aks74u_original/w_aks74u_original.prefab";
	public const string ViewPrefabPath =
		"addons/lifepunch/lpweapons/aks74ucovert/equipment/vm_aks74u_original/vm_aks74u_original.prefab";
	private const bool ProductSourceCleared = false;

	public static GameModeEquipmentDto? Ensure()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			return null;
		}

		if ( !ProductSourceCleared )
		{
			Log.Warning(
				"Aks74uOriginalDevRoster: product source license clearance is not pinned; " +
				"refusing prefab access and editor roster mutation." );
			return null;
		}

		var gameMode = Config.Current?.GameMode;
		if ( gameMode?.Equipments is null )
		{
			Log.Error( "Aks74uOriginalDevRoster: Config.GameMode.Equipments unavailable." );
			return null;
		}

		var worldPrefab = GameObject.GetPrefab( WorldPrefabPath );
		if ( !worldPrefab.IsValid() )
		{
			Log.Warning( $"Aks74uOriginalDevRoster: product world prefab unavailable: {WorldPrefabPath}" );
			return null;
		}

		var viewPrefab = GameObject.GetPrefab( ViewPrefabPath );
		if ( !viewPrefab.IsValid() || !viewPrefab.Components.Get<ViewModel>().IsValid() )
		{
			Log.Warning( $"Aks74uOriginalDevRoster: product view prefab unavailable or missing root ViewModel: {ViewPrefabPath}" );
			return null;
		}

		var exactRow = ResolveExactPathRow();
		if ( exactRow.IsValid() )
		{
			return exactRow;
		}

		if ( HasConflictingRows( gameMode.Equipments ) || HasConflictingContent() )
		{
			Log.Error( "Aks74uOriginalDevRoster: conflicting Original AKS-74U row/content identity; refusing editor injection." );
			return null;
		}

		GameModeAddonContents.EnsureEditorContent(
			EditorContentId,
			"AKS-74U Original (editor)",
			WorldPrefabPath,
			ViewPrefabPath,
			nameof( EquipmentSlot.Secondary ) );

		var row = new GameModeEquipmentDto
		{
			Id = EditorEquipmentRowId,
			GameModeAddonContentId = EditorContentId,
			NameOverride = "AKS-74U Original",
			DescriptionOverride = "Editor inject — not Portal or market.",
			Limit = -1
		};
		gameMode.Equipments.Add( row );

		var resolved = ResolveExactPathRow();
		if ( !resolved.IsValid() || resolved!.Id != EditorEquipmentRowId )
		{
			Log.Error( "Aks74uOriginalDevRoster: injected row did not resolve uniquely." );
			return null;
		}

		Log.Info( $"LP_AKS74U_ORIGINAL_DEV_ROSTER_READY " +
			$"EquipmentRowId={EditorEquipmentRowId} ContentId={EditorContentId}." );
		return resolved;
	}

	private static GameModeEquipmentDto? ResolveExactPathRow()
	{
		var pathRows = GameModeEquipments.All
			.Where( row => string.Equals(
				row.PrefabPath(), WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
		if ( pathRows.Length != 1 )
		{
			return null;
		}

		var resource = pathRows[0];
		if ( !string.Equals(
			resource.SecondaryPrefabPath(), ViewPrefabPath, StringComparison.OrdinalIgnoreCase ) )
		{
			return null;
		}

		var contentRows = GameModeEquipments.All
			.Where( row => row.GameModeAddonContentId == resource.GameModeAddonContentId )
			.ToArray();
		return contentRows.Length == 1 && contentRows[0].Id == resource.Id ? resource : null;
	}

	private static bool HasConflictingRows( IEnumerable<GameModeEquipmentDto> rows )
	{
		var snapshot = rows.ToArray();
		return snapshot.Any( row =>
			row.Id == EditorEquipmentRowId ||
			row.GameModeAddonContentId == EditorContentId ||
			string.Equals( row.PrefabPath(), WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) ||
			string.Equals( row.SecondaryPrefabPath(), ViewPrefabPath, StringComparison.OrdinalIgnoreCase ) );
	}

	private static bool HasConflictingContent()
	{
		if ( GameModeAddonContents.All.Any( content =>
			content.Id != EditorContentId &&
			(string.Equals( content.PrimaryReference, WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) ||
			 string.Equals( content.SecondaryReference, ViewPrefabPath, StringComparison.OrdinalIgnoreCase )) ) )
		{
			return true;
		}

		var content = GameModeAddonContents.FindById( EditorContentId );
		return content is not null &&
			(!string.Equals( content.PrimaryReference, WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) ||
			 !string.Equals( content.SecondaryReference, ViewPrefabPath, StringComparison.OrdinalIgnoreCase ));
	}
}
