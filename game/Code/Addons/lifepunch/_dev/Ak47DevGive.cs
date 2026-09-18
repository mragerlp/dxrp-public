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
using Ak47Weapon = LifePunch.DXRP.Addons.AK47.AK47;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// AK-only give. Stamps <see cref="Equipment.EquipmentId"/> from the live
/// <c>lifepunch.dxrpdev</c> roster row (lifted 2026-08-21).
/// </summary>
public static class Ak47DevGive
{
	/// <summary>
	/// Live <c>GameModeAddonContentId</c> / content <c>Id</c> for AK-47 on
	/// <c>lifepunch.dxrpdev</c>. <see cref="GameModeEquipments.FindById"/> key.
	/// </summary>
	public static readonly Guid AkEquipmentId = new( "c029bcc7-bcec-4197-a7a4-8558cc3d90e7" );

	[ConCmd( "lp_give_ak" )]
	public static void GiveAk()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_give_ak: editor-only." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_give_ak: must be host (editor play)." );
			return;
		}

		GiveAkTo( Player.Local, "lp_give_ak" );
	}

	/// <summary>Host give onto any valid pawn. Used by the GROK bot seat so Bloodwave stays untouched.</summary>
	public static bool GiveAkTo( Player player, string logPrefix = "lp_give_ak" )
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

		var prefabPath = Ak47Weapon.WorldPrefabPath;
		var viewPrefabPath = Ak47Weapon.ViewModelPrefabPath;
		var contentRows = GameModeEquipments.All
			.Where( row => row.GameModeAddonContentId == AkEquipmentId )
			.ToArray();
		var pathRows = GameModeEquipments.All
			.Where( row => string.Equals(
				row.PrefabPath(), prefabPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
		var resource = contentRows.Length == 1 ? contentRows[0] : null;
		var pathResource = pathRows.Length == 1 ? pathRows[0] : null;
		if ( !resource.IsValid()
			|| !pathResource.IsValid()
			|| pathResource!.GameModeAddonContentId != AkEquipmentId
			|| pathResource.Id != resource!.Id
			|| !string.Equals( resource.SecondaryPrefabPath(), viewPrefabPath, StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Error( $"{logPrefix}: roster miss or ID/world/view mismatch for " +
				$"EquipmentId={AkEquipmentId} world={prefabPath} view={viewPrefabPath} " +
				$"contentRows={contentRows.Length} pathRows={pathRows.Length}." );
			return false;
		}

		var prefab = GameObject.GetPrefab( prefabPath );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab could not load: {prefabPath}" );
			return false;
		}

		var viewPrefab = GameObject.GetPrefab( viewPrefabPath );
		if ( !viewPrefab.IsValid() )
		{
			Log.Error( $"{logPrefix}: viewmodel prefab could not load: {viewPrefabPath}" );
			return false;
		}
		if ( !viewPrefab.Components.Get<ViewModel>().IsValid() )
		{
			Log.Error( $"{logPrefix}: viewmodel prefab has no root ViewModel component: {viewPrefabPath}" );
			return false;
		}

		var go = prefab.Clone( new CloneConfig
		{
			Transform = new Transform(),
			Parent = player.WeaponGameObject
		} );
		if ( !go.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab clone failed: {prefabPath}" );
			return false;
		}

		var equipment = go.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants );
		if ( !equipment.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab has no Equipment component: {prefabPath}" );
			go.Destroy();
			return false;
		}

		equipment.EquipmentId = AkEquipmentId;
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

		RemoveExisting( player, equipment );

		if ( !player.CantSwitch )
		{
			player.SetCurrentEquipment( equipment );
		}

		var vm = equipment.Resource?.SecondaryPrefabPath();
		var active = player.CurrentEquipment == equipment;
		Log.Info( $"{logPrefix}: created AK-47 on '{player.SteamName}' active={active} EquipmentId={equipment.EquipmentId} resourceValid={equipment.Resource.IsValid()} vm={vm} from {prefabPath}" );
		return true;
	}

	private static void RemoveExisting( Player player, Equipment except )
	{
		foreach ( var weapon in player.Equipment.ToList() )
		{
			if ( !weapon.IsValid() || ReferenceEquals( weapon, except ) )
			{
				continue;
			}

			if ( weapon.EquipmentId == AkEquipmentId
				|| string.Equals( weapon.GameObject.Name, "w_ak47", StringComparison.OrdinalIgnoreCase ) )
			{
				player.RemoveEquipment( weapon );
			}
		}
	}
}
