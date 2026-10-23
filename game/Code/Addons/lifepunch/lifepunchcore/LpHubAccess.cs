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

using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// SPINE-1 kit piece C1: the identity and manage-access seam every LifePunch hub offers.
/// NOTHING IMPLEMENTS THIS YET. It is the shape a hub opts into in a later slice; adding it here
/// changes no behaviour (no registration, no subscriber, no consumer).
/// </summary>
public interface ILpHub
{
	/// <summary>Stable product key of the hub (for example the key a PlayerHub door and a result relay use).</summary>
	string HubKey { get; }

	/// <summary>Operator Steam ID that owns the hub; 0 when the hub is unowned.</summary>
	ulong HubOwner { get; }

	/// <summary>Whether the caller (by Steam ID; 0 = the caller did not resolve to a player) may manage the hub.</summary>
	bool CanManageHub( ulong callerSteamId );
}

/// <summary>
/// SPINE-1 kit piece C1: the shared access policy and the open-gate wrappers. Pure statics over the
/// existing gate; no state, nothing registered, and no existing file is switched to call it in this slice.
/// </summary>
public static class LpHubAccess
{
	/// <summary>
	/// The default manage policy, lifted in meaning from the Bitcoin hub's CanManageHub
	/// (LpBitcoinHubEntity.cs:1386-1396): the owner may manage; an unowned hub may be managed by anyone while
	/// no PIN is set; with a PIN set only the owner may. The pin-set state is a parameter so this never reaches
	/// into a hub.
	/// </summary>
	/// <param name="hubOwner">Steam ID of the hub's owner; 0 = unowned.</param>
	/// <param name="callerSteamId">Steam ID of the caller; 0 = the caller did not resolve to a player.</param>
	/// <param name="pinIsSet">Whether an access PIN is set on the hub.</param>
	/// <param name="unresolvedCallerIsHost">
	/// What an unresolved caller counts as. The Bitcoin hub's owner check answers Networking.IsHost for a caller
	/// it cannot resolve to a player (LifePunchEntityOwnership.CallerIsOwner); pass that value here.
	/// </param>
	public static bool DefaultCanManage( ulong hubOwner, ulong callerSteamId, bool pinIsSet, bool unresolvedCallerIsHost )
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		var callerIsOwner = callerSteamId == 0
			? unresolvedCallerIsHost
			: hubOwner == 0 || callerSteamId == hubOwner;

		if ( !pinIsSet )
			return callerIsOwner || hubOwner == 0;

		return callerIsOwner;
#endif
	}

	/// <summary>
	/// Client press gate for a hub: a hub the hands are holding never answers a press (rotate is USE while
	/// holding), and the existing hub press gate decides range and hands priority. Same two checks as
	/// LpBitcoinHubEntity.CanPress (LpBitcoinHubEntity.cs:294-296).
	/// </summary>
	public static bool CanPress( GameObject hub )
		=> hub.IsValid()
		   && !hub.Tags.Has( LifePunchPropPhysics.GrabbedTag )
		   && LifePunchMenuInteractGate.CanPressHubMenu( hub );

	/// <summary>
	/// Host open gate for a hub whose access is a DXRP ownership permission: the existing hub gate, whole
	/// (permission, reach to the model's bounds, hands manipulation, hands pickup). Pass Rpc.Caller from the
	/// host RPC (null in the local lane). Same call as LpBitcoinHubEntity.OpenHubHost (LpBitcoinHubEntity.cs:309-318).
	/// </summary>
	/// <remarks>
	/// The gate's permission step (GameUtils.HasPermission) refuses an object that carries neither the entity
	/// nor the construct tag and is not owned. A job-gated hub that is not a DXRP entity must use
	/// <see cref="CanOpenHostInRange"/> instead, or this call refuses every press.
	/// </remarks>
	public static bool CanOpenHost( Connection caller, GameObject hub )
		=> LifePunchMenuInteractGate.IsCallerAllowedHub( caller, hub );

	/// <summary>
	/// Host open gate for a job-gated hub that is not a DXRP entity (its access is decided by its own job
	/// gate, not by ownership): the caller must stand in hub reach, measured to the model's bounds like the
	/// existing gate does, and the hub must not be held by the hands. Hands pickup priority stays a client
	/// press check for this tier (<see cref="CanPress"/>): the existing gate has no host call that skips the
	/// ownership permission, and this slice does not edit it. Local lane: the existing gate's own answer.
	/// </summary>
	public static bool CanOpenHostInRange( Connection caller, GameObject hub )
	{
#if LIFEPUNCH_LOCAL
		return LifePunchMenuInteractGate.IsCallerAllowedHub( caller, hub );
#else
		if ( !hub.IsValid() || caller is null )
			return false;

		if ( hub.Root.Tags.Has( LifePunchPropPhysics.GrabbedTag ) )
			return false;

		var player = GameUtils.GetPlayerByConnectionId( caller.Id );
		if ( !player.IsValid() )
			return false;

		var renderer = hub.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		return renderer.IsValid()
			? LifePunchMenuInteractRange.IsHubInOpenRange( player.WorldPosition, renderer.Bounds )
			: LifePunchMenuInteractRange.IsHubInOpenRange( player.WorldPosition, hub.WorldPosition );
#endif
	}
}
