// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using LifePunch.DXRP.Addons;
using Sandbox.UI;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Hub logs scrollback — imperative rows (Razor must not re-emit alert markup every tick).
/// </summary>
public sealed class LpBitcoinHubLogsScrollPanel : LifePunchScrollRegionPanel
{
	public LpBitcoinHubLogsScrollPanel()
	{
		CanDragScroll = false;
		PreferScrollToBottom = false;
	}

	protected override float GetContentHeight()
	{
		var list = Children.FirstOrDefault( child => child.HasClass( "notif-list" ) );
		if ( !list.IsValid() )
			return base.GetContentHeight();

		var rowCount = 0;
		var total = 0f;
		const float fallbackRowHeight = 56f;

		foreach ( var row in list.Children )
		{
			if ( !row.IsValid() )
				continue;

			rowCount++;
			var height = row.Box.Rect.Height;
			if ( height <= 1f )
				height = fallbackRowHeight;

			total += height;
		}

		if ( rowCount <= 0 )
			return 0f;

		if ( rowCount > 1 )
			total += ( rowCount - 1 ) * 8f;

		return total;
	}

	public override float GetMaxScrollY()
	{
		if ( !IsValid )
			return 0f;

		var viewHeight = Box.Rect.Height;
		if ( viewHeight <= 1f )
			return 0f;

		return System.Math.Max( 0f, GetContentHeight() - viewHeight );
	}
}
