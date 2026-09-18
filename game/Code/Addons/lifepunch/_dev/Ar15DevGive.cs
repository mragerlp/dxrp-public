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
/// The command deliberately fails closed until the current game-mode roster
/// contains the real Portal row for the AR-15 world prefab.
/// </summary>
public static class Ar15DevGive
{
	private const string Command = "lp_give_ar15";
	private const string WorldPrefabPath = "addons/lifepunch/lpweapons/ar15/equipment/w_ar15/w_ar15.prefab";
	private const string ViewPrefabPath = "addons/lifepunch/lpweapons/ar15/equipment/vm_ar15/vm_ar15.prefab";

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

		var resource = ResolveResource();
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

	private static GameModeEquipmentDto? ResolveResource()
	{
		var pathRows = GameModeEquipments.All
			.Where( row => string.Equals(
				row.PrefabPath(), WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
		if ( pathRows.Length != 1 ) return null;

		var resource = pathRows[0];
		var contentRows = GameModeEquipments.All
			.Where( row => row.GameModeAddonContentId == resource.GameModeAddonContentId )
			.ToArray();
		return contentRows.Length == 1 && contentRows[0].Id == resource.Id ? resource : null;
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
