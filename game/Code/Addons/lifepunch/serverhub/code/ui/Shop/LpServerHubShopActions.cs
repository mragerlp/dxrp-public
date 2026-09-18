// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Server Hub for DXRP" (s&box ident: lifepunch.serverhub - addon ident: serverhub)
// Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave
// -----------------------------------------------------------------------------

using System;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.ServerHub;

/// <summary>
/// THE ONLY OUTBOUND ACTION IN THIS ADDON. Everything else the Server Hub does is a read.
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file, apart from <see cref="LpServerHubData"/>, on purpose. That class
/// documents itself as a pure read seam and it stays true; putting a purchase call inside it
/// would have quietly falsified its own contract. Anyone auditing what this addon can do to the
/// economy has exactly one file to read, and it is this one.
/// </para>
/// <para>
/// WHAT THIS SENDS: a single <c>Guid</c>. Not a price, not a quantity, not an item body. The host
/// looks the item up itself, prices it itself, and debits before it grants. There is nothing in
/// the payload for a client to tamper with.
/// </para>
/// <para>
/// WHY <c>PurchaseMarketItemHost</c> AND NOT <c>PurchaseEntityHost</c>. Both are
/// <c>[Rpc.Host]</c> methods on <c>GameManager</c>, 65 lines apart, with near-identical names.
/// They are not equivalent, and the difference is money:
/// </para>
/// <para>
/// <c>PurchaseMarketItemHost( Guid )</c> resolves the caller from <c>Rpc.CallerId</c>, serialises
/// through a per-player purchase lock, re-checks <c>CanPurchase</c>, computes the price host-side
/// and then <b>returns early unless <c>ChargeHost</c> succeeds</b> - the debit gates the grant.
/// That is the correct rail and it is the one this addon rides.
/// </para>
/// <para>
/// <c>PurchaseEntityHost( GameModeEntityDto )</c> takes a client-supplied item body, computes a
/// price into a local that is never read again, and spawns the entity with no debit at all. As of
/// this run it is still that way. Riding it would have handed players free items through a shop
/// button. This addon must never call it.
/// </para>
/// <para>
/// The staff free-spawn rail <c>SpawnMarketItemHost</c> is likewise not wired here. Staff already
/// have it behind the native market's admin mode; a player-facing hub is the wrong place for it.
/// </para>
/// </remarks>
public static class LpServerHubShopActions
{
	/// <summary>Positive code-string ID for the purchase slice. Exists nowhere else in the tree.</summary>
	public const string PurchaseMark = "LP_SERVERHUB_SHOP_PURCHASE_20260828";

	/// <summary>
	/// Ask the host to sell the local player one market item. Returns immediately; the outcome
	/// arrives as the host's own notification and world state, never as a local grant.
	/// </summary>
	/// <remarks>
	/// The checks below are COURTESY checks. They exist so a player gets an immediate, legible
	/// reason instead of a silent no-op, and they mirror what the native market screen does before
	/// the same call. **None of them is a gate.** The host re-runs every one of them and is the
	/// only authority. In particular the price computed here is never spent, never stored, and
	/// never sent - it decides only whether it is worth troubling the host.
	/// </remarks>
	public static void RequestPurchase( Guid itemId )
	{
#if !LIFEPUNCH_LOCAL
		var manager = GameManager.Instance;
		if ( !manager.IsValid() )
		{
			return;
		}

		var player = Player.Local;
		var item = GameModeMarketItems.FindById( itemId );

		// Re-resolved from the id rather than trusted from the panel: the row that was rendered
		// may be a frame or two stale, and the roster can change underneath an open menu.
		if ( !player.IsValid() || item is null )
		{
			return;
		}

		if ( !GameModeMarketItems.CanPurchase( player, item ) )
		{
			Notify.Error( "#generic.forbidden" );
			return;
		}

		if ( Config.Current.Game.MoneyEnabled )
		{
			var price = (uint)Math.Max( 0, (int)MathF.Ceiling( item.Cost * manager.EntityPriceMultiplier ) );
			if ( (ulong)player.WalletBalance + player.BankBalance < price )
			{
				Notify.Error( "#notify.cash.poor" );
				return;
			}
		}

		// Shares the native market's cooldown key deliberately. A separate key would let a player
		// open both surfaces and spend at twice the intended rate on the client side.
		if ( Cooldown.Current.CheckAndStartCooldown( Constants.EntityTag, Config.Current.Game.EntityCooldown, true ) )
		{
			return;
		}

		// The id, and only the id.
		manager.PurchaseMarketItemHost( item.Id );
#endif
	}
}
