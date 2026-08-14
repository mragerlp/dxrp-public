// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>Shared LifePunch menu UI scale steps (S / M / L / XL).</summary>
public enum LpUiScaleSize
{
	Small = 0,
	Medium = 1,
	Large = 2,
	ExtraLarge = 3
}

/// <summary>CSS class helpers for hub and CRT terminal scale modifiers.</summary>
public static class LpUiScale
{
	public static readonly LpUiScaleSize Default = LpUiScaleSize.ExtraLarge;

	public static string CssClass( LpUiScaleSize size ) => size switch
	{
		LpUiScaleSize.Small => "lp-ui-size-s",
		LpUiScaleSize.Medium => "lp-ui-size-m",
		LpUiScaleSize.Large => "lp-ui-size-l",
		LpUiScaleSize.ExtraLarge => "lp-ui-size-xl",
		_ => "lp-ui-size-xl"
	};

	public static string ShortLabel( LpUiScaleSize size ) => size switch
	{
		LpUiScaleSize.Small => "S",
		LpUiScaleSize.Medium => "M",
		LpUiScaleSize.Large => "L",
		LpUiScaleSize.ExtraLarge => "XL",
		_ => "XL"
	};

	public static LpUiScaleSize Clamp( int value ) =>
		(LpUiScaleSize)Math.Clamp( value, 0, 3 );
}
