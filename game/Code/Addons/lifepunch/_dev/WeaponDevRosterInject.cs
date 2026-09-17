// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// Editor-only synthetic game-mode rows so fail-closed give helpers can equip
/// weapons that exist on disk but are not yet Portal-minted. Cleared whenever
/// <see cref="Config.SetGameMode"/> replaces the snapshot.
/// </summary>
public static class WeaponDevRosterInject
{
	/// <summary>Stable editor-only content id for the Glock candidate.</summary>
	public static readonly Guid GlockContentId = new( "23b45d88-aff6-4be2-bf5d-0af655a39d01" );

	/// <summary>Stable editor-only equipment-row id for the Glock candidate.</summary>
	public static readonly Guid GlockEquipmentRowId = new( "dd19aaf6-c525-48dc-8d15-b67b09b447f4" );

	private const string GlockWorldPrefabPath =
		"addons/lifepunch/lpweapons/glock/equipment/w_glock/w_glock.prefab";
	private const string GlockViewPrefabPath =
		"addons/lifepunch/lpweapons/glock/equipment/vm_glock/vm_glock.prefab";

	/// <summary>
	/// Ensures exactly one Glock equipment row + content mapping for this
	/// editor session. Prefers a live Portal row when present.
	/// </summary>
	public static GameModeEquipmentDto? EnsureGlock()
	{
		if ( !Application.IsEditor )
		{
			return null;
		}

		var portal = ResolveExactPathRow( GlockWorldPrefabPath, GlockViewPrefabPath );
		if ( portal.IsValid() )
		{
			return portal;
		}

		GameModeAddonContents.EnsureEditorContent(
			GlockContentId,
			"Glock (editor)",
			GlockWorldPrefabPath,
			GlockViewPrefabPath,
			nameof( EquipmentSlot.Secondary ) );

		var gm = Config.Current?.GameMode;
		if ( gm?.Equipments is null )
		{
			Log.Error( "WeaponDevRosterInject: Config.GameMode.Equipments unavailable." );
			return null;
		}

		// Drop prior editor injects for this content so uniqueness stays exact-1.
		gm.Equipments.RemoveAll( row =>
			row.Id == GlockEquipmentRowId
			|| row.GameModeAddonContentId == GlockContentId
			|| string.Equals( row.PrefabPath(), GlockWorldPrefabPath, StringComparison.OrdinalIgnoreCase ) );

		var row = new GameModeEquipmentDto
		{
			Id = GlockEquipmentRowId,
			GameModeAddonContentId = GlockContentId,
			NameOverride = "Glock",
			DescriptionOverride = "Editor inject — not Portal.",
			Limit = -1
		};
		gm.Equipments.Add( row );

		Log.Info( $"WeaponDevRosterInject: editor Glock row ready " +
			$"EquipmentRowId={GlockEquipmentRowId} ContentId={GlockContentId}." );
		return row;
	}

	private static GameModeEquipmentDto? ResolveExactPathRow( string worldPath, string viewPath )
	{
		var pathRows = GameModeEquipments.All
			.Where( row => string.Equals(
				row.PrefabPath(), worldPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
		if ( pathRows.Length != 1 )
		{
			return null;
		}

		var resource = pathRows[0];
		if ( !string.Equals( resource.SecondaryPrefabPath(), viewPath, StringComparison.OrdinalIgnoreCase ) )
		{
			return null;
		}

		var contentRows = GameModeEquipments.All
			.Where( row => row.GameModeAddonContentId == resource.GameModeAddonContentId )
			.ToArray();
		return contentRows.Length == 1 && contentRows[0].Id == resource.Id ? resource : null;
	}
}
