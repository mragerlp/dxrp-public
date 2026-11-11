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
/// Editor-only equip command for the AR-15 private Portal candidate.
/// When the current game-mode roster has no row for the AR-15 world prefab, the command adds ONE
/// in-memory, editor-only Equipment row (the ids the editor roster inject table reserves for the
/// AR-15; never a Portal row, never a market row, gone when Play stops) so the weapon can be tested
/// without a Portal row. With a real Portal row present it uses that row and adds nothing.
/// Why: the AR-15 could not be given in an editor without Portal authorization, so none of its
/// runtime behaviour could be proven (DRIVE-AR15-1). Who may run it: the editor host only.
/// Shape followed: WeaponDevRosterInject.cs:50 (EnsureEditorContent + one in-memory row).
/// </summary>
public static class Ar15DevGive
{
	private const string Command = "lp_give_ar15";
	private const string WorldPrefabPath = "addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab";
	private const string ViewPrefabPath = "addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab";
	private const string DisplayName = "AR-15";

	// The ids WeaponRosterInjectTable reserves for the AR-15 (its inject flag stays off; this command
	// only adds the same row for itself).
	private static readonly Guid ContentId = new( "eef46616-a741-41d8-afb8-7984ae69a924" );
	private static readonly Guid RowId = new( "9eeb7f78-a2cd-46a7-94ee-90221310a625" );

	[ConCmd( Command )]
	public static void GiveAr15()
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

		GiveAr15To( Player.Local );
	}

	public static bool GiveAr15To( Player player, string logPrefix = Command )
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( $"{logPrefix}: editor host only." );
			return false;
		}

		if ( !player.IsValid() || !player.WeaponGameObject.IsValid() )
		{
			Log.Warning( $"{logPrefix}: no player / weapon holder." );
			return false;
		}

		var resource = EnsureRow( logPrefix );
		if ( !resource.IsValid() || !string.Equals(
			resource!.SecondaryPrefabPath(), ViewPrefabPath, StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Error( $"{logPrefix}: roster miss or viewmodel mismatch for the AR-15 Portal row; " +
				$"world={WorldPrefabPath} view={ViewPrefabPath}." );
			return false;
		}

		var prefab = GameObject.GetPrefab( WorldPrefabPath );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab could not load: {WorldPrefabPath}" );
			return false;
		}

		var viewPrefab = GameObject.GetPrefab( ViewPrefabPath );
		if ( !viewPrefab.IsValid() )
		{
			Log.Error( $"{logPrefix}: viewmodel prefab could not load: {ViewPrefabPath}" );
			return false;
		}
		if ( !viewPrefab.Components.Get<ViewModel>().IsValid() )
		{
			Log.Error( $"{logPrefix}: viewmodel prefab has no root ViewModel component: {ViewPrefabPath}" );
			return false;
		}

		var go = prefab.Clone( new CloneConfig
		{
			Transform = new Transform(),
			Parent = player.WeaponGameObject
		} );
		if ( !go.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab clone failed: {WorldPrefabPath}" );
			return false;
		}

		var equipment = go.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants );
		if ( !equipment.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab has no Equipment component: {WorldPrefabPath}" );
			go.Destroy();
			return false;
		}

		equipment.EquipmentId = resource.GameModeAddonContentId;
		equipment.ViewModelPrefab = viewPrefab;
		equipment.OwnerId = player.Id;
		equipment.CanDrop = true;

		if ( player.Network.Owner is { } owner )
		{
			go.NetworkSpawn( owner );
		}
		else
		{
			go.NetworkSpawn();
		}

		RemoveExisting( player, resource.GameModeAddonContentId, equipment );

		if ( !player.CantSwitch )
		{
			player.SetCurrentEquipment( equipment );
		}

		var active = player.CurrentEquipment == equipment;
		Log.Info( $"{logPrefix}: created AR-15 on '{player.SteamName}' active={active} " +
			$"EquipmentId={equipment.EquipmentId} resourceValid={equipment.Resource.IsValid()} from {WorldPrefabPath}" );
		return true;
	}

	/// <summary>The row for the world prefab, adding one in-memory editor-only row when none exists.</summary>
	private static GameModeEquipmentDto? EnsureRow( string logPrefix )
	{
		var gameMode = Config.Current?.GameMode;
		if ( gameMode?.Equipments is null )
		{
			Log.Error( $"{logPrefix}: Config.GameMode.Equipments unavailable." );
			return null;
		}

		var equipments = gameMode.Equipments;
		var pathRows = PathRows( equipments );
		if ( pathRows.Length == 0 )
		{
			if ( equipments.Any( row => row.Id == RowId || row.GameModeAddonContentId == ContentId ) )
			{
				Log.Error( $"{logPrefix}: editor ids already claimed by another row." );
				return null;
			}

			GameModeAddonContents.EnsureEditorContent(
				ContentId, $"{DisplayName} (editor)", WorldPrefabPath, ViewPrefabPath, EquipmentSlot.Primary.ToString() );

			equipments.Add( new GameModeEquipmentDto
			{
				Id = RowId,
				GameModeAddonContentId = ContentId,
				NameOverride = DisplayName,
				DescriptionOverride = "Editor inject, not Portal or market.",
				Limit = -1
			} );
			Log.Info( $"{logPrefix}: added in-memory editor row {RowId} gameMode='{gameMode.Name}' isReady={Config.Current!.IsReady}" );
			pathRows = PathRows( equipments );
		}

		if ( pathRows.Length != 1 )
		{
			return null;
		}

		var resource = pathRows[0];
		var contentRows = equipments.Where( row => row.GameModeAddonContentId == resource.GameModeAddonContentId ).ToArray();
		return contentRows.Length == 1 && contentRows[0].Id == resource.Id ? resource : null;
	}

	private static GameModeEquipmentDto[] PathRows( System.Collections.Generic.IEnumerable<GameModeEquipmentDto> rows )
	{
		return rows
			.Where( row => string.Equals( row.PrefabPath(), WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
	}

	private static void RemoveExisting( Player player, Guid equipmentId, Equipment except )
	{
		foreach ( var weapon in player.Equipment.ToList() )
		{
			if ( !weapon.IsValid() || ReferenceEquals( weapon, except ) )
			{
				continue;
			}

			if ( weapon.EquipmentId == equipmentId
				|| string.Equals( weapon.GameObject.Name, "w_ar15", StringComparison.OrdinalIgnoreCase ) )
			{
				player.RemoveEquipment( weapon );
			}
		}
	}
}
