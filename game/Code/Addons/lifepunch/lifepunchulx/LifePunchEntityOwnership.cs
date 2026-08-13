// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Binds <see cref="BaseEntity.Owner"/> (Steam ID) for DXRP Waila + future PIN/hack gates.
/// Waila shows the owner's in-game name while connected and the numeric Steam ID when offline.
/// </summary>
public static class LifePunchEntityOwnership
{
#if !LIFEPUNCH_LOCAL
	public static void TryBindSpawnOwnerHost( this BaseEntity entity )
	{
		if ( !entity.IsValid() || !Networking.IsHost || entity.Owner != 0 )
			return;

		var networkOwner = entity.GameObject.Network.Owner;
		if ( networkOwner == null )
			return;

		var player = GameUtils.GetPlayerByConnectionId( networkOwner.Id );
		if ( player.IsValid() )
			entity.Owner = player.SteamId;
	}

	public static void BindOwnerFromLocalViewer( this BaseEntity entity )
	{
		if ( !Networking.IsHost || !entity.IsValid() || entity.Owner != 0 )
			return;

		if ( Player.Local.IsValid() )
			entity.Owner = Player.Local.SteamId;
	}

	public static void BindOwnerFromPlayer( this BaseEntity entity, Player player )
	{
		if ( !entity.IsValid() || !player.IsValid() )
			return;

		entity.Owner = player.SteamId;
	}

	public static bool TryBindOwnerFromCaller( this BaseEntity entity, Guid callerId )
	{
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
			return false;

		entity.Owner = player.SteamId;
		return true;
	}

	public static bool CallerIsOwner( long ownerSteamId, Guid callerId )
	{
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
			return Networking.IsHost;

		return ownerSteamId == 0 || player.SteamId == ownerSteamId;
	}

	public static string GetOwnerLabel( long ownerSteamId )
	{
		if ( ownerSteamId == 0 )
			return string.Empty;

		var player = GameUtils.GetPlayerById( ownerSteamId );
		return player.IsValid() ? player.DisplayName : ownerSteamId.ToString();
	}

	/// <summary>Hub↔terminal/rack may link only when both share the same non-zero operator Steam ID.</summary>
	public static bool SharesOperator( long hubOwner, long equipmentOwner )
		=> hubOwner != 0 && equipmentOwner != 0 && hubOwner == equipmentOwner;

	/// <summary>Recover spawn bind — caller must already match <paramref name="hubOwner"/>.</summary>
	public static bool TryClaimEquipmentForHub( this BaseEntity equipment, long hubOwner, Guid callerId )
	{
		if ( !equipment.IsValid() || hubOwner == 0 )
			return false;

		if ( equipment.Owner != 0 )
			return SharesOperator( hubOwner, equipment.Owner );

		if ( !CallerIsOwner( hubOwner, callerId ) )
			return false;

		return equipment.TryBindOwnerFromCaller( callerId ) && SharesOperator( hubOwner, equipment.Owner );
	}
#else
	public static void TryBindSpawnOwnerHost( this Component entity ) { }

	public static void BindOwnerFromLocalViewer( this Component entity )
	{
		if ( !entity.IsValid() )
			return;
	}

	public static bool CallerIsOwner( long ownerSteamId, Guid callerId ) => true;

	public static bool TryBindOwnerFromCaller( this Component entity, Guid callerId ) => true;

	public static string GetOwnerLabel( long ownerSteamId )
		=> ownerSteamId == 0 ? string.Empty : ownerSteamId.ToString();

	public static bool SharesOperator( long hubOwner, long equipmentOwner ) => true;

	public static bool TryClaimEquipmentForHub( this Component equipment, long hubOwner, Guid callerId ) => true;
#endif
}
