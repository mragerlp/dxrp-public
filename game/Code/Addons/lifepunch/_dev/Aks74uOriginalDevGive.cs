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
/// Editor-only equip command for the Original AKS-74U. This remains inert until
/// its exact licensed product prefabs exist and an unambiguous roster row can
/// be resolved by <see cref="Aks74uOriginalDevRoster"/>.
/// </summary>
public static class Aks74uOriginalDevGive
{
	private const string Command = "lp_give_aks74u_original";
	private const string WorldPrefabPath = Aks74uOriginalDevRoster.WorldPrefabPath;
	private const string ViewPrefabPath = Aks74uOriginalDevRoster.ViewPrefabPath;

	[ConCmd( Command )]
	public static void GiveAks74uOriginal()
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

		GiveAks74uOriginalTo( Player.Local );
	}

	public static bool GiveAks74uOriginalTo( Player player, string logPrefix = Command )
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

		var resource = Aks74uOriginalDevRoster.Ensure();
		if ( !resource.IsValid() || !string.Equals(
			resource!.SecondaryPrefabPath(), ViewPrefabPath, StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Error( $"{logPrefix}: exact Original AKS-74U roster row unavailable; " +
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
		if ( !viewPrefab.IsValid() || !viewPrefab.Components.Get<ViewModel>().IsValid() )
		{
			Log.Error( $"{logPrefix}: viewmodel prefab unavailable or missing root ViewModel: {ViewPrefabPath}" );
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
		Log.Info( $"LP_AKS74U_ORIGINAL_DEV_GIVE_READY player='{player.SteamName}' active={active} " +
			$"EquipmentId={equipment.EquipmentId} resourceValid={equipment.Resource.IsValid()} " +
			$"world={WorldPrefabPath}" );
		return true;
	}

	private static void RemoveExisting( Player player, Guid equipmentId, Equipment except )
	{
		foreach ( var weapon in player.Equipment.ToList() )
		{
			if ( !weapon.IsValid() || ReferenceEquals( weapon, except ) )
			{
				continue;
			}

			if ( weapon.EquipmentId == equipmentId ||
				string.Equals( weapon.GameObject.Name, "w_aks74u_original", StringComparison.OrdinalIgnoreCase ) )
			{
				player.RemoveEquipment( weapon );
			}
		}
	}
}
