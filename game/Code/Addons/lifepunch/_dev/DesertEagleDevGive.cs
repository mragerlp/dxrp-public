// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LifePunch editor dev lane" (Code/_dev — NOT shipped; lifepunchulx publishes six staff-menu files only) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — equips the custom Desert Eagle prefab after the
/// current game-mode roster contains its real Portal row.
/// </summary>
public static class DesertEagleDevGive
{
	private const string Command = "lp_give_deagle";
	private const string WorldPrefabPath = "addons/lifepunch/lpweapons/deserteagle/equipment/w_desert_eagle/w_desert_eagle.prefab";
	private const string ViewPrefabPath = "addons/lifepunch/lpweapons/deserteagle/equipment/vm_desert_eagle/vm_desert_eagle.prefab";

	[ConCmd( Command )]
	public static void GiveDesertEagle()
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

		GiveDesertEagleTo( Player.Local );
	}

	public static bool GiveDesertEagleTo( Player player, string logPrefix = Command )
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
			Log.Error( $"{logPrefix}: roster miss or viewmodel mismatch for the Desert Eagle Portal row; " +
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
		Log.Info( $"{logPrefix}: created Desert Eagle on '{player.SteamName}' active={active} " +
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
				|| string.Equals( weapon.GameObject.Name, "w_desert_eagle", StringComparison.OrdinalIgnoreCase ) )
			{
				player.RemoveEquipment( weapon );
			}
		}
	}
}
