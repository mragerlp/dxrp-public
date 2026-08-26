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
	public static readonly Guid AkEquipmentId = new( "baf2ccb8-0ae5-48bd-a763-171064e08d4e" );

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

		var player = Player.Local;
		if ( !player.IsValid() || !player.WeaponGameObject.IsValid() )
		{
			Log.Warning( "lp_give_ak: no local player / weapon holder." );
			return;
		}

		var resource = GameModeEquipments.FindById( AkEquipmentId );
		if ( resource == null || !resource.IsValid() )
		{
			Log.Error( $"lp_give_ak: roster miss for EquipmentId={AkEquipmentId}." );
			return;
		}

		var prefabPath = Ak47Weapon.WorldPrefabPath;
		var prefab = GameObject.GetPrefab( prefabPath );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"lp_give_ak: prefab could not load: {prefabPath}" );
			return;
		}

		RemoveExisting( player );

		var go = prefab.Clone( new CloneConfig
		{
			Transform = new Transform(),
			Parent = player.WeaponGameObject
		} );

		var equipment = go.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants );
		if ( !equipment.IsValid() )
		{
			Log.Error( $"lp_give_ak: prefab has no Equipment component: {prefabPath}" );
			go.Destroy();
			return;
		}

		equipment.EquipmentId = AkEquipmentId;
		equipment.OwnerId = player.Id;
		equipment.CanDrop = true;
		go.NetworkSpawn( player.Network.Owner );

		if ( !player.CantSwitch )
		{
			player.SetCurrentEquipment( equipment );
		}

		var vm = equipment.Resource?.SecondaryPrefabPath();
		Log.Info( $"lp_give_ak: equipped AK-47 EquipmentId={equipment.EquipmentId} resourceValid={equipment.Resource.IsValid()} vm={vm} from {prefabPath}" );
	}

	private static void RemoveExisting( Player player )
	{
		foreach ( var weapon in player.Equipment.ToList() )
		{
			if ( !weapon.IsValid() )
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
