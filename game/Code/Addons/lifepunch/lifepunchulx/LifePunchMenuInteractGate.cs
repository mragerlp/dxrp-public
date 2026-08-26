// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Game.Equipments;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Client + host gates for LifePunch menu USE — close range; yields when DXRP Hands grab/rotate or pocket-pickup wins.
/// </summary>
public static class LifePunchMenuInteractGate
{
	public static bool CanPressMenu( GameObject target )
	{
		if ( !target.IsValid() )
			return false;

		var viewerPos = GetLocalViewerPosition( target.Scene );
		if ( !viewerPos.HasValue )
			return false;

		if ( !LifePunchMenuInteractRange.IsInOpenRange( viewerPos.Value, target.WorldPosition ) )
			return false;

		if ( WouldHandsManipulationTakePriority( viewerPos.Value, target ) )
			return false;

		return !WouldHandsPickupTakePriority( viewerPos.Value, target );
	}

	public static bool CanPressHubMenu( GameObject target )
	{
		if ( !target.IsValid() )
			return false;

		var viewerPos = GetLocalViewerPosition( target.Scene );
		if ( !viewerPos.HasValue )
			return false;

		if ( !IsHubTargetInOpenRange( viewerPos.Value, target ) )
			return false;

		if ( WouldHandsManipulationTakePriority( viewerPos.Value, target ) )
			return false;

		return !WouldHandsPickupTakePriority( viewerPos.Value, target );
	}

	/// <summary>Hub reach measured to the model's world bounding box so it scales with model size
	/// (2026-07-10 fix). Falls back to the pivot-based check only if the hub has no renderer.</summary>
	private static bool IsHubTargetInOpenRange( Vector3 viewerPos, GameObject target )
	{
		var renderer = target.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( renderer.IsValid() )
			return LifePunchMenuInteractRange.IsHubInOpenRange( viewerPos, renderer.Bounds );

		return LifePunchMenuInteractRange.IsHubInOpenRange( viewerPos, target.WorldPosition );
	}

#if !LIFEPUNCH_LOCAL
	public static bool IsCallerAllowed( Connection caller, GameObject target )
	{
		if ( !target.IsValid() || caller is null )
			return false;

		if ( !GameUtils.HasPermission( caller, target ) )
			return false;

		var player = GameUtils.GetPlayerByConnectionId( caller.Id );
		if ( !player.IsValid() )
			return false;

		if ( !LifePunchMenuInteractRange.IsInOpenRange( player.WorldPosition, target.WorldPosition ) )
			return false;

		if ( WouldHandsManipulationTakePriority( player, target ) )
			return false;

		return !WouldHandsPickupTakePriority( player, target );
	}

	public static bool IsCallerAllowedHub( Connection caller, GameObject target )
	{
		if ( !target.IsValid() || caller is null )
			return false;

		if ( !GameUtils.HasPermission( caller, target ) )
			return false;

		var player = GameUtils.GetPlayerByConnectionId( caller.Id );
		if ( !player.IsValid() )
			return false;

		if ( !IsHubTargetInOpenRange( player.WorldPosition, target ) )
			return false;

		if ( WouldHandsManipulationTakePriority( player, target ) )
			return false;

		return !WouldHandsPickupTakePriority( player, target );
	}
#else
	public static bool IsCallerAllowed( Connection caller, GameObject target )
	{
		if ( !target.IsValid() )
			return false;

		var viewerPos = GetLocalViewerPosition( target.Scene );
		if ( !viewerPos.HasValue )
			return false;

		return LifePunchMenuInteractRange.IsInOpenRange( viewerPos.Value, target.WorldPosition );
	}

	public static bool IsCallerAllowedHub( Connection caller, GameObject target )
	{
		if ( !target.IsValid() )
			return false;

		var viewerPos = GetLocalViewerPosition( target.Scene );
		if ( !viewerPos.HasValue )
			return false;

		return IsHubTargetInOpenRange( viewerPos.Value, target );
	}
#endif

	public static Vector3? GetLocalViewerPosition( Scene scene )
	{
		if ( scene is null )
			return null;

#if LIFEPUNCH_LOCAL
		var camera = scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		return camera.IsValid() ? camera.WorldPosition : null;
#else
		var player = Player.Local;
		return player.IsValid() ? player.WorldPosition : null;
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static bool WouldHandsManipulationTakePriority( Player player, GameObject menuTarget )
	{
		if ( !player.IsValid() || !menuTarget.IsValid() )
			return false;

		var root = menuTarget.Root;
		if ( !root.IsValid() )
			return false;

		if ( root.Tags.Has( Constants.GrabbedTag ) )
			return true;

		var equipment = player.CurrentEquipment;
		if ( !equipment.IsValid() || equipment.EquipmentId != Constants.HandsEquipmentId )
			return false;

		var hands = equipment.Components.Get<HandsEquipment>( FindMode.EverythingInSelf );
		if ( hands.IsValid() && hands.IsHolding( root ) )
			return true;

		if ( !root.Tags.Has( Constants.HandsInteractTag ) )
			return false;

		// Hands grab/rotate uses attack1 or use (E) — do not open LifePunch menu while manipulating.
		if ( !Input.Down( "attack1" ) && !Input.Down( "use" ) )
			return false;

		var trace = player.Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( Constants.TraceIgnoreTags )
			.Run();

		return trace.Hit && trace.GameObject.IsValid() && trace.GameObject.Root == root;
	}
#endif

	private static bool WouldHandsManipulationTakePriority( Vector3 viewerPos, GameObject menuTarget )
	{
#if LIFEPUNCH_LOCAL
		return false;
#else
		var player = Player.Local;
		return player.IsValid() && WouldHandsManipulationTakePriority( player, menuTarget );
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static bool WouldHandsPickupTakePriority( Player player, GameObject menuTarget )
	{
		if ( !player.IsValid() || !menuTarget.IsValid() || player.Scene is null )
			return false;

		if ( !Config.Current.Game.PocketEnabled )
			return false;

		var trace = player.Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.UseHitboxes()
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( Constants.TraceIgnoreTags )
			.Run();

		return WouldHandsPickupHit( trace, menuTarget );
	}
#endif

	private static bool WouldHandsPickupTakePriority( Vector3 viewerPos, GameObject menuTarget )
	{
#if LIFEPUNCH_LOCAL
		return false;
#else
		var player = Player.Local;
		return player.IsValid() && WouldHandsPickupTakePriority( player, menuTarget );
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static bool WouldHandsPickupHit( SceneTraceResult trace, GameObject menuTarget )
	{
		if ( !trace.Hit || !trace.GameObject.IsValid() || !menuTarget.IsValid() )
			return false;

		var aimed = trace.GameObject.Root;
		if ( !aimed.IsValid() || aimed == menuTarget.Root )
			return false;

		if ( aimed.Tags.Has( LifePunchInteractTags.NoPocket ) )
			return false;

		// Mirror HandsEquipment RMB pocket path: pocket_item and not already pocketed.
		return aimed.Tags.Has( Constants.PocketItemTag ) && !aimed.Tags.Has( Constants.PocketTag );
	}
#endif
}
