// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "Morpheus" (addon ident: lpmorpheus) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Morpheus;

/// <summary>
/// Seats the lower-third readout for the local viewer only: one screen panel, found
/// or created on demand, never networked and never parented to the chamber.
/// It carries no authority — it only shows a line the host already sanitized.
/// </summary>
public static class LpMorpheusHud
{
	private const string HostName = "lp_morpheus_hud";

	private static MorpheusReadout _readout;

	public static void Show( LpMorpheusReadout readout )
	{
		if ( readout.Kind == LpMorpheusReadoutKind.Hidden )
		{
			Clear();
			return;
		}

		var panel = EnsureReadout();
		if ( !panel.IsValid() )
			return;

		panel.Speaker = readout.Speaker;
		panel.Line = readout.Line;
		panel.Kind = readout.Kind;
		panel.StateHasChanged();
	}

	public static void Clear()
	{
		if ( !_readout.IsValid() || _readout.Kind == LpMorpheusReadoutKind.Hidden )
			return;

		_readout.Line = string.Empty;
		_readout.Kind = LpMorpheusReadoutKind.Hidden;
		_readout.StateHasChanged();
	}

	/// <summary>
	/// Fail closed: if no panel can be seated, the caption stays the only readout
	/// rather than throwing inside the briefing path.
	/// </summary>
	private static MorpheusReadout EnsureReadout()
	{
#if !LIFEPUNCH_LOCAL
		if ( GameManager.IsHeadless )
			return null;
#endif

		if ( _readout.IsValid() )
			return _readout;

		try
		{
			var scene = Game.ActiveScene;
			if ( scene is null )
				return null;

			var host = scene.CreateObject();
			host.Name = HostName;
			host.NetworkMode = NetworkMode.Never;
			host.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;

			host.Components.GetOrCreate<ScreenPanel>();
			_readout = host.Components.GetOrCreate<MorpheusReadout>();
			return _readout;
		}
		catch ( Exception e )
		{
			Log.Warning( $"LP_MORPHEUS_HUD unavailable: {e.Message}" );
			return null;
		}
	}
}
