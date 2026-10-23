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

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons;

/// <summary>
/// SPINE-1 kit piece C5: the scene event a hub result reaches the asking caller's open panel through. Native
/// s&box event-interface idiom, the same shape as <see cref="ILifePunchPurchaseEvent"/>
/// (ILifePunchPurchaseEvent.cs:25). A panel opts in by implementing it; NOTHING DOES YET.
/// </summary>
public interface ILpHubResult : ISceneEvent<ILpHubResult>
{
	/// <param name="hubKey">The product key of the hub that answered (<see cref="ILpHub.HubKey"/>).</param>
	/// <param name="callerId">Connection id of the caller the result is for.</param>
	/// <param name="kind">A short result tag the product defines; the closed vocabulary is not fixed in this slice.</param>
	/// <param name="payload">The product's result body as text; opaque to the relay.</param>
	void OnHubResult( string hubKey, Guid callerId, string kind, string payload );
}

/// <summary>
/// SPINE-1 kit piece C5: the host-to-caller broadcast, filtered by caller id. The same filter the Bitcoin hub
/// writes by hand on each of its result broadcasts (LpBitcoinHubEntity.cs:1281-1358: the callerId parameter and
/// the Connection.Local.Id check). The broadcast carries NetFlags.HostOnly, the flags Notify.cs:107 uses for its
/// static broadcasts, so only the host sends. Nothing calls <see cref="SendToCaller"/> in this slice and nothing
/// subscribes to <see cref="ILpHubResult"/>, so the relay never fires.
/// </summary>
public sealed class LpHubResultRelay
{
	private LpHubResultRelay() { }

	/// <summary>Host to caller: delivers one result to the one connection whose id matches <paramref name="callerId"/>.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	public static void SendToCaller( string hubKey, Guid callerId, string kind, string payload )
	{
		if ( Connection.Local is null || Connection.Local.Id != callerId )
			return;

		ILpHubResult.Post( x => x.OnHubResult( hubKey, callerId, kind, payload ) );
	}
}
