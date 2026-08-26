// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if !LIFEPUNCH_LOCAL
using System.Threading.Tasks;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Instant host-side payout for portal inventory consumable <c>$BTC</c>
/// (<see cref="LpBitcoinIdent.PortalBtcInventoryItemId"/>) when grant =
/// <see cref="LpBitcoinIdent.PortalBtcRedeemGrantName"/>.
/// Stock DXRP <c>/useitem</c> consumes the stack then spawns this prefab; we PayHost and despawn.
/// </summary>
[Title( "LIFEPUNCH BTC Cash Redeem" )]
[Category( "LifePunch/Bitcoin" )]
public sealed class LpBitcoinCashRedeemEntity : BaseEntity
{
	protected override void OnStart()
	{
		base.OnStart();

		if ( !Networking.IsHost )
		{
			return;
		}

		_ = RedeemHostAsync();
	}

	private async Task RedeemHostAsync()
	{
		var player = GameUtils.GetPlayerById( Owner );
		var payout = LpBitcoinEconomy.PortalRedeemCashPayout();

		if ( !player.IsValid() )
		{
			Log.Warning( "[lifepunch.bitcoin] portal BTC redeem — owner player not found." );
			await DestroySelfAsync();
			return;
		}

		if ( payout == 0 )
		{
			player.Error( "Portal BTC redeem is not configured on this server." );
			Log.Warning( "[lifepunch.bitcoin] portal BTC redeem — payout is $0 (configure portal_redeem_cash_usd)." );
			await DestroySelfAsync();
			return;
		}

		if ( !await LpBitcoinWallet.TryPayBank( player.ConnectionId, payout, "LIFEPUNCH portal BTC redeem" ) )
		{
			player.Error( "Portal BTC redeem failed — contact staff." );
			Log.Warning( $"[lifepunch.bitcoin] portal BTC redeem PayHost(bank) failed for {player.DisplayName} (${payout})." );
			await DestroySelfAsync();
			return;
		}

		player.SendMessage( $"Redeemed 1 portal BTC for ${payout:N0} to your bank." );
		Log.Info( $"[lifepunch.bitcoin] portal BTC redeem — {player.DisplayName} +${payout:N0} (stack consumed)." );

		await DestroySelfAsync();
	}

	private async Task DestroySelfAsync()
	{
		await GameTask.MainThread();

		if ( GameObject.IsValid() && !GameObject.IsDestroyed )
		{
			GameObject.Destroy();
		}
	}
}
#endif
