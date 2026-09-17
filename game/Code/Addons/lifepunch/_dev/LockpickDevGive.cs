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
using LockpickConst = LifePunch.DXRP.Addons.Lockpick.Lockpick;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// DEV / EDITOR-TEST ONLY — lives in <c>Code/_dev/</c>.
/// Lockpick-only give. Separate from <see cref="Ak47DevGive"/>; that file is never re-pointed.
/// Portal roster row is a NAMED PREREQUISITE — roster miss is EXPECTED until minted.
/// </summary>
public static class LockpickDevGive
{
	/// <summary>
	/// New placeholder <c>GameModeAddonContentId</c> for the lockpick row.
	/// SOL + principal portal hands mint the live row; until then FindById misses.
	/// </summary>
	public static readonly Guid LockpickEquipmentId = new( "5b688632-fc46-4c56-a8ab-19cba2fc0db3" );

	[ConCmd( "lp_give_lockpick" )]
	public static void GiveLockpick()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( "lp_give_lockpick: editor-only." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( "lp_give_lockpick: must be host (editor play)." );
			return;
		}

		GiveLockpickTo( Player.Local, "lp_give_lockpick" );
	}

	public static bool GiveLockpickTo( Player player, string logPrefix = "lp_give_lockpick" )
	{
		if ( !player.IsValid() || !player.WeaponGameObject.IsValid() )
		{
			Log.Warning( $"{logPrefix}: no player / weapon holder." );
			return false;
		}

		var resource = GameModeEquipments.FindById( LockpickEquipmentId );
		if ( resource == null || !resource.IsValid() )
		{
			Log.Error( $"{logPrefix}: roster miss for EquipmentId={LockpickEquipmentId}." );
			return false;
		}

		var prefabPath = LockpickConst.WorldPrefabPath;
		var prefab = GameObject.GetPrefab( prefabPath );
		if ( !prefab.IsValid() )
		{
			Log.Error( $"{logPrefix}: prefab could not load: {prefabPath}" );
			return false;
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
			Log.Error( $"{logPrefix}: prefab has no Equipment component: {prefabPath}" );
			go.Destroy();
			return false;
		}

		equipment.EquipmentId = LockpickEquipmentId;
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

		if ( !player.CantSwitch )
		{
			player.SetCurrentEquipment( equipment );
		}

		var vm = equipment.Resource?.SecondaryPrefabPath();
		Log.Info( $"{logPrefix}: equipped Lockpick on '{player.SteamName}' EquipmentId={equipment.EquipmentId} resourceValid={equipment.Resource.IsValid()} vm={vm} from {prefabPath}" );
		return true;
	}

	private static void RemoveExisting( Player player )
	{
		foreach ( var weapon in player.Equipment.ToList() )
		{
			if ( !weapon.IsValid() )
			{
				continue;
			}

			if ( weapon.EquipmentId == LockpickEquipmentId
				|| string.Equals( weapon.GameObject.Name, "w_lockpick", StringComparison.OrdinalIgnoreCase ) )
			{
				player.RemoveEquipment( weapon );
			}
		}
	}
}
