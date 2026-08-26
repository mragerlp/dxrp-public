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
using System.Linq;
using Sandbox;
using Sandbox.UI;
using LifePunch.DXRP.Addons;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// HASHD CRT scrollback — manual wheel scroll + drag-select on log labels (PowerShell-style).
/// </summary>
public sealed class LpBitcoinTerminalLogPanel : LifePunchScrollRegionPanel
{
	private const float LineGap = 2f;
	private const float FallbackLineHeight = 16f;
	private const float WheelStep = 48f;
	private const float ContentBottomPad = 48f;
	private const float WrapLineHeight = FallbackLineHeight * 1.4f;
	private const int WrapCharsPerLine = 72;
	private const float ClickDragThreshold = 4f;

	private static readonly Color SelectionTint = (Color.Parse( "#f0a500" ) ?? new Color( 0.941f, 0.647f, 0f )).WithAlpha( 0.35f );

	private Vector2 _mouseDownLocal;
	private bool _leftMouseDown;

	public Func<string> CopyFallback { get; set; }

	public LpBitcoinTerminalLogPanel()
	{
		CanDragScroll = false;
		AcceptsFocus = true;
		AllowChildSelection = true;
		PreferScrollToBottom = true;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		if ( e.MouseButton == MouseButtons.Left )
		{
			_leftMouseDown = true;
			_mouseDownLocal = e.LocalPosition;
		}

		base.OnMouseDown( e );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		var leftUp = e.MouseButton == MouseButtons.Left && _leftMouseDown;
		var downPos = _mouseDownLocal;

		if ( e.MouseButton == MouseButtons.Left )
			_leftMouseDown = false;

		base.OnMouseUp( e );

		if ( !leftUp )
			return;

		var moved = ( downPos - e.LocalPosition ).Length > ClickDragThreshold;
		if ( !moved )
			ClearLineSelection();
	}

	public override void OnMouseWheel( Vector2 value )
	{
		if ( System.Math.Abs( value.y ) < 0.01f )
			return;

		if ( HasScrollY )
		{
			base.OnMouseWheel( value );
			var max = GetMaxScrollY();
			PreferScrollToBottom = max <= 0f || ScrollOffset.y >= max - 4f;
			return;
		}

		var maxManual = GetMaxScrollY();
		if ( maxManual <= 0f )
			return;

		var next = System.Math.Clamp( ScrollOffset.y - value.y * WheelStep, 0f, maxManual );
		ScrollOffset = new Vector2( 0f, next );
		PreferScrollToBottom = next >= maxManual - 4f;
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Pressed && e.Button == "escape" )
		{
			ClearLineSelection();
			e.StopPropagation = true;
			return;
		}

		if ( e.Pressed && IsEndButton( e.Button ) )
		{
			PreferScrollToBottom = true;
			ScrollToBottom( force: true );
			e.StopPropagation = true;
			return;
		}

		if ( e.Pressed && e.HasCtrl && e.Button is "c" or "C" )
		{
			if ( TryCopySelection() )
			{
				e.StopPropagation = true;
				return;
			}

			var text = CopyFallback?.Invoke();
			if ( !string.IsNullOrEmpty( text ) )
			{
				Clipboard.SetText( text );
				e.StopPropagation = true;
				return;
			}
		}

		base.OnButtonEvent( e );
	}

	public void ApplyLineSelectionChrome()
	{
		foreach ( var label in Descendants.OfType<Label>() )
		{
			label.Selectable = true;
			label.ShouldDrawSelection = true;
			label.SelectionColor = SelectionTint;
		}
	}

	/// <summary>Collapse drag-select highlight — click log background or press Esc.</summary>
	public void ClearLineSelection()
	{
		var hadSelection = false;
		foreach ( var label in Descendants.OfType<Label>() )
		{
			if ( !label.HasSelection() )
				continue;

			label.Selectable = false;
			hadSelection = true;
		}

		if ( !hadSelection )
			return;

		foreach ( var label in Descendants.OfType<Label>() )
			label.Selectable = true;

		ApplyLineSelectionChrome();
	}

	private bool TryCopySelection()
	{
		foreach ( var label in Descendants.OfType<Label>() )
		{
			if ( !label.HasSelection() )
				continue;

			var text = label.GetClipboardValue( false );
			if ( string.IsNullOrEmpty( text ) )
				continue;

			Clipboard.SetText( text );
			return true;
		}

		return false;
	}

	private static bool IsEndButton( string button )
		=> !string.IsNullOrEmpty( button )
		   && button.Equals( "end", StringComparison.OrdinalIgnoreCase );

	protected override float GetContentHeight()
	{
		var stack = Children.FirstOrDefault( child => child.HasClass( "log-stack" ) );
		if ( !stack.IsValid() )
			return base.GetContentHeight();

		var lineCount = 0;
		var total = 0f;

		foreach ( var line in stack.Children )
		{
			if ( !line.IsValid() )
				continue;

			lineCount++;
			total += GetLogLineHeight( line );
		}

		if ( lineCount <= 0 )
			return 0f;

		if ( lineCount > 1 )
			total += ( lineCount - 1 ) * LineGap;

		// Wrapped labels: flex stack height beats per-child sum when layout lags a frame.
		var stackHeight = stack.Box.Rect.Height;
		if ( stackHeight > total + 2f )
			total = stackHeight;

		return total + ContentBottomPad;
	}

	private float GetLogLineHeight( Panel line )
	{
		var height = line.Box.Rect.Height;
		if ( height > 1f )
			return height;

		if ( line is Label label && !string.IsNullOrWhiteSpace( label.Text ) )
			return EstimateWrappedLabelHeight( label.Text );

		return FallbackLineHeight;
	}

	private static float EstimateWrappedLabelHeight( string text )
	{
		var rows = 0;
		foreach ( var segment in text.Split( '\n' ) )
		{
			var len = segment.TrimEnd().Length;
			rows += System.Math.Max( 1, (int)System.Math.Ceiling( len / (float)WrapCharsPerLine ) );
		}

		return rows * WrapLineHeight;
	}

	public override void SyncTerminalScroll( bool forceFollowBottom = false )
	{
		var max = GetMaxScrollY();
		if ( max <= 0f )
		{
			ScrollToTop();
			PreferScrollToBottom = true;
			return;
		}

		if ( forceFollowBottom || PreferScrollToBottom )
			ScrollToBottom( forceFollowBottom );
	}

	public override float GetMaxScrollY()
	{
		if ( !IsValid )
			return 0f;

		var viewHeight = Box.Rect.Height;
		if ( viewHeight <= 1f )
			return 0f;

		var measured = System.Math.Max( 0f, GetContentHeight() - viewHeight );

		if ( HasScrollY && ScrollSize.y > viewHeight )
		{
			var engine = System.Math.Max( 0f, ScrollSize.y - viewHeight );
			return System.Math.Max( measured, engine );
		}

		return measured;
	}
}
