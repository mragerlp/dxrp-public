// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch shared world LCD placement — HASHD terminal monitor glass (model bounds).
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons;

/// <summary>World-space <see cref="TextRenderer"/> alignment for CRT terminal props.</summary>
public static class LifePunchTerminalLcd
{
	/// <summary>
	/// Bounds-based placement when <see cref="LpBitcoinTerminalEntity.ManualLcdPlacement"/> is false.
	/// Prefer prefab tuning + <c>lp_bitcoin_lcd_tune</c> when monitor import axis differs.
	/// </summary>
	public static bool TryAlignHashdScreen( GameObject terminalRoot, TextRenderer screen )
	{
		if ( !terminalRoot.IsValid() || !screen.IsValid() )
			return false;

		var renderer = terminalRoot.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( !renderer.IsValid() || renderer.Model is null )
			return false;

		var modelPath = renderer.Model.ResourcePath ?? string.Empty;
		if ( !modelPath.Contains( "hashdterminal", StringComparison.OrdinalIgnoreCase )
		     && !modelPath.Contains( "hashd-terminal", StringComparison.OrdinalIgnoreCase ) )
			return false;

		var bounds = renderer.Model.Bounds;
		var lcdGo = screen.GameObject;

		lcdGo.LocalPosition = new Vector3(
			bounds.Center.x,
			bounds.Maxs.y - bounds.Size.y * 0.04f,
			bounds.Mins.z + bounds.Size.z * 0.76f );

		// HASHD import: text plane faces +Y after yaw — tune in prefab when ManualLcdPlacement is on.
		lcdGo.LocalRotation = Rotation.From( 0f, 180f, 0f );
		lcdGo.LocalScale = Vector3.One;
		screen.Scale = 0.045f;

		return true;
	}
}
