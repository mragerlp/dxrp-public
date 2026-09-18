// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox.UI;

namespace LifePunch.DXRP.Addons;

/// <summary>
/// LifePunch menus use wheel scroll only — never drag-scroll (<see cref="Panel.CanDragScroll"/>).
/// Call <see cref="Apply"/> every frame while a menu is open; pass <c>resetOffset: true</c> after UI scale changes.
/// </summary>
public static class LifePunchUiScrollPolicy
{
	public static void Apply( Panel root, bool resetOffset = false )
	{
		if ( root is null || !root.IsValid )
			return;

		ApplyToPanel( root, resetOffset );

		foreach ( var panel in root.Descendants )
			ApplyToPanel( panel, resetOffset );
	}

	public static void ResetOffsets( Panel root ) => Apply( root, resetOffset: true );

	private static void ApplyToPanel( Panel panel, bool resetOffset )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.CanDragScroll = false;

		if ( resetOffset )
		{
			panel.ScrollOffset = Vector2.Zero;

			if ( panel is LifePunchScrollRegionPanel scrollRegion )
				scrollRegion.PreferScrollToBottom = true;

			return;
		}

		// LifePunchScrollRegionPanel owns wheel scroll + stick-to-bottom; clamp only.
		if ( panel is LifePunchScrollRegionPanel )
		{
			ClampManualScrollRegion( panel );
			return;
		}

		ClampScrollOffset( panel );
	}

	private static void ClampScrollOffset( Panel panel )
	{
		if ( IsManualScrollShell( panel ) )
		{
			ClampManualScrollRegion( panel );
			return;
		}

		if ( !panel.HasScrollY && !panel.HasScrollX )
		{
			if ( panel.ScrollOffset != Vector2.Zero )
				panel.ScrollOffset = Vector2.Zero;

			return;
		}

		var offset = panel.ScrollOffset;
		var maxX = 0f;
		var maxY = 0f;

		if ( panel.HasScrollX )
		{
			var viewWidth = panel.Box.Right - panel.Box.Left;
			maxX = Math.Max( 0f, panel.ScrollSize.x - viewWidth );
		}

		if ( panel.HasScrollY )
		{
			var viewHeight = panel.Box.Bottom - panel.Box.Top;
			maxY = Math.Max( 0f, panel.ScrollSize.y - viewHeight );
		}

		var x = Math.Clamp( offset.x, 0f, maxX );
		var y = Math.Clamp( offset.y, 0f, maxY );

		if ( Math.Abs( offset.x - x ) > 0.01f || Math.Abs( offset.y - y ) > 0.01f )
			panel.ScrollOffset = new Vector2( x, y );
	}

	private static void ClampManualScrollRegion( Panel panel )
	{
		var maxY = panel is LifePunchScrollRegionPanel scrollRegion
			? scrollRegion.GetMaxScrollY()
			: GetManualScrollMaxY( panel );

		if ( maxY <= 0f )
		{
			if ( panel.ScrollOffset != Vector2.Zero )
				panel.ScrollOffset = Vector2.Zero;

			return;
		}

		var y = Math.Clamp( panel.ScrollOffset.y, 0f, maxY );

		if ( Math.Abs( panel.ScrollOffset.y - y ) > 0.01f )
			panel.ScrollOffset = new Vector2( 0f, y );
	}

	/// <summary>
	/// Native Razor scroll range measured from laid-out child edges. The span includes row gaps
	/// and remains stable when the children translate during scrolling. Opted-in lists use the
	/// measured screen-space travel directly instead of the legacy ScaleFromScreen conversion.
	/// </summary>
	public static float GetMeasuredScrollMaxY( Panel panel )
	{
		if ( panel is null || !panel.IsValid || panel.Box.Rect.Height <= 1f )
			return 0f;

		var hasContent = false;
		var top = 0f;
		var bottom = 0f;

		foreach ( var child in panel.Children )
		{
			if ( !child.IsValid || child.Box.Rect.Height <= 0f )
				continue;

			top = hasContent ? Math.Min( top, child.Box.Top ) : child.Box.Top;
			bottom = hasContent ? Math.Max( bottom, child.Box.Bottom ) : child.Box.Bottom;
			hasContent = true;
		}

		return hasContent ? Math.Max( 0f, bottom - top - panel.Box.Rect.Height ) : 0f;
	}

	private static float GetManualScrollMaxY( Panel panel )
	{
		if ( panel.HasClass( "lp-ui-measured-scroll" ) )
			return GetMeasuredScrollMaxY( panel );

		var viewHeight = panel.Box.Rect.Height;
		if ( viewHeight <= 1f )
			return 0f;

		var contentHeight = LifePunchScrollLayout.GetStackedContentHeight( panel );

		// Box heights are screen px; ScrollOffset is panel-local. At non-1 UI scale the
		// unconverted difference over- or under-clamps (engine ScrollSize is no better —
		// it disagrees with stacked Box heights on these flex stacks).
		return Math.Max( 0f, (contentHeight - viewHeight) * panel.ScaleFromScreen );
	}

	private static bool IsManualScrollShell( Panel panel )
		=> LifePunchScrollLayout.IsManualScrollShell( panel );
}
