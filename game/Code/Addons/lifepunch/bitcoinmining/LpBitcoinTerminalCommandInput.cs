// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// is the sole-owned intellectual property of lifepunch.co. It is NOT licensed for resale,
// redistribution, sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;
using Sandbox.UI;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>rig@hub&gt; prompt — history navigation, drag-select, ctrl+c/x/v, amber selection tint.</summary>
[Library( "lp_bitcoin_terminal_cmd_input" )]
public sealed class LpBitcoinTerminalCommandInput : TextEntry
{
	private static readonly Color SelectionTint = (Color.Parse( "#f0a500" ) ?? new Color( 0.941f, 0.647f, 0f )).WithAlpha( 0.35f );

	public Action HistoryNavigateUp { get; set; }
	public Action HistoryNavigateDown { get; set; }

	public LpBitcoinTerminalCommandInput()
	{
		SelectionColor = SelectionTint;
		AcceptsFocus = true;
		AddClass( "cmd-input" );
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Pressed && HasFocus && TryHandleHistoryNavigation( e ) )
			return;

		base.OnButtonEvent( e );
	}

	public override void OnPaste( string text )
	{
		if ( string.IsNullOrEmpty( text ) )
			return;

		base.OnPaste( text );
	}

	private bool TryHandleHistoryNavigation( ButtonEvent e )
	{
		if ( IsHistoryButton( e.Button, "up" ) )
		{
			HistoryNavigateUp?.Invoke();
			e.StopPropagation = true;
			return true;
		}

		if ( IsHistoryButton( e.Button, "down" ) )
		{
			HistoryNavigateDown?.Invoke();
			e.StopPropagation = true;
			return true;
		}

		return false;
	}

	private static bool IsHistoryButton( string button, string direction )
	{
		if ( string.IsNullOrEmpty( button ) )
			return false;

		if ( button.Equals( direction, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return direction switch
		{
			"up" => button.Equals( "arrowup", StringComparison.OrdinalIgnoreCase ),
			"down" => button.Equals( "arrowdown", StringComparison.OrdinalIgnoreCase ),
			_ => false
		};
	}
}
