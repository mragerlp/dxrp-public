// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch)
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;

namespace LifePunch.DXRP.Addons;

public delegate LifePunchScrollRegionPanel LifePunchScrollPanelFactory( string slotClass );

/// <summary>
/// Manual wheel scroll for flex-bound <c>lp-ui-scroll-region</c> panels — s&amp;box often leaves
/// <see cref="Panel.HasScrollY"/> false until layout catches up; pair with <see cref="LifePunchUiScrollPolicy"/>.
/// </summary>
public class LifePunchScrollRegionPanel : Panel
{
	private const float WheelStep = 48f;

	/// <summary>When true, new log lines stick to the bottom; wheel-up clears until user returns near bottom.</summary>
	public new bool PreferScrollToBottom { get; set; } = true;

	public LifePunchScrollRegionPanel()
	{
		CanDragScroll = false;
	}

	public override void OnMouseWheel( Vector2 value )
	{
		if ( System.Math.Abs( value.y ) < 0.01f )
			return;

		if ( HasScrollY )
		{
			base.OnMouseWheel( value );
			UpdatePreferScrollToBottom();
			return;
		}

		var max = GetMaxScrollY();
		if ( max <= 0f )
			return;

		var next = System.Math.Clamp( ScrollOffset.y - value.y * WheelStep, 0f, max );
		ScrollOffset = new Vector2( 0f, next );
		PreferScrollToBottom = next >= max - 4f;
	}

	public virtual float GetMaxScrollY()
	{
		if ( !IsValid )
			return 0f;

		var viewHeight = Box.Rect.Height;
		if ( viewHeight <= 1f )
			return 0f;

		if ( HasScrollY && ScrollSize.y > 0f )
			return System.Math.Max( 0f, ScrollSize.y - viewHeight );

		return System.Math.Max( 0f, GetContentHeight() - viewHeight );
	}

	public void ScrollToTop()
	{
		if ( ScrollOffset != Vector2.Zero )
			ScrollOffset = Vector2.Zero;
	}

	public void ScrollToBottom( bool force = false )
	{
		var max = GetMaxScrollY();
		if ( max <= 0f )
		{
			ScrollToTop();
			PreferScrollToBottom = true;
			return;
		}

		if ( !force && !PreferScrollToBottom && ScrollOffset.y < max - 28f )
			return;

		ScrollOffset = new Vector2( 0f, max );
		PreferScrollToBottom = true;
	}

	/// <summary>CMD-style: top-aligned when content fits; follow bottom only when overflowing.</summary>
	public virtual void SyncTerminalScroll( bool forceFollowBottom = false )
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

	private void UpdatePreferScrollToBottom()
	{
		var max = GetMaxScrollY();
		PreferScrollToBottom = max <= 0f || ScrollOffset.y >= max - 4f;
	}

	protected virtual float GetContentHeight()
		=> LifePunchScrollLayout.GetStackedContentHeight( this );
}

/// <summary>
/// s&amp;box Razor cannot nest markup inside custom C# <see cref="Panel"/> tags — use
/// <c>&lt;div class="lp-ui-scroll-region"&gt;</c> in markup, then upgrade after the tree builds.
/// </summary>
public static class LifePunchScrollRegionBootstrap
{
	private static LifePunchScrollPanelFactory _customFactory;

	/// <summary>Register slot-specific scroll panels (e.g. HASHD terminal log with copy support).</summary>
	public static void SetCustomFactory( LifePunchScrollPanelFactory factory )
		=> _customFactory = factory;

	public static void UpgradeScrollRegions( Panel root )
	{
		if ( root is null || !root.IsValid )
			return;

		var sources = root.Descendants
			.Where( panel => panel is not LifePunchScrollRegionPanel && panel.HasClass( "lp-ui-scroll-region" ) )
			.ToList();

		foreach ( var source in sources )
		{
			if ( !source.IsValid )
				continue;

			var parent = source.Parent;
			if ( parent is null || !parent.IsValid )
				continue;

			var slotClass = GetScrollSlotClass( source );
			if ( slotClass is not null )
			{
				var existing = parent.Children
					.OfType<LifePunchScrollRegionPanel>()
					.FirstOrDefault( panel => panel.HasClass( slotClass ) );

				if ( existing.IsValid() )
				{
					// Imperative scroll bodies (HASHD CRT log, hub logs) — Razor re-emits empty shells on
					// unrelated StateHasChanged; merging would duplicate rows and flicker on hover.
					if ( slotClass is not ("log" or "logs-scroll") && source.Children.Any() )
						MergeChildren( source, existing );

					source.Delete();
					continue;
				}
			}

			// Razor may emit the scroll shell a frame before its body — wait for content.
			// Hub logs + CRT log populate rows imperatively after upgrade.
			if ( !source.Children.Any() && slotClass is not ("log" or "logs-scroll") )
				continue;

			var scroll = CreateScrollPanel( slotClass );

			CopyPanelClasses( source, scroll );

			scroll.ScrollOffset = source.ScrollOffset;
			scroll.PreferScrollToBottom = source.PreferScrollToBottom;

			MergeChildren( source, scroll );

			var index = parent.GetChildIndex( source );
			source.Delete();
			scroll.Parent = parent;

			if ( index >= 0 )
				parent.SetChildIndex( scroll, index );
		}
	}

	private static LifePunchScrollRegionPanel CreateScrollPanel( string slotClass )
	{
		var custom = _customFactory?.Invoke( slotClass );
		return custom ?? new LifePunchScrollRegionPanel();
	}

	private static void MergeChildren( Panel from, Panel to )
	{
		foreach ( var child in from.Children.ToList() )
			child.Parent = to;
	}

	private static string GetScrollSlotClass( Panel panel )
		=> LifePunchScrollLayout.GetScrollSlotClass( panel );

	public static LifePunchScrollRegionPanel FindFirst( Panel root, string className )
	{
		if ( root is null || !root.IsValid )
			return null;

		foreach ( var panel in root.Descendants )
		{
			if ( panel is LifePunchScrollRegionPanel scroll && scroll.HasClass( className ) )
				return scroll;
		}

		return null;
	}

	/// <summary>Razor-safe entry — pass <see cref="PanelComponent"/>; avoids <c>Panel</c> type/name clashes in generated @code.</summary>
	public static void UpgradeScrollRegionsFromHost( PanelComponent host )
		=> UpgradeScrollRegions( host?.Panel );

	/// <summary>Razor-safe lookup — pass <see cref="PanelComponent"/> instead of raw <see cref="Panel"/>.</summary>
	public static LifePunchScrollRegionPanel FindFirstFromHost( PanelComponent host, string className )
		=> FindFirst( host?.Panel, className );

	private static void CopyPanelClasses( Panel from, Panel to )
	{
		// Panel.Classes.Split(...) is not string.Split — it returns the class string, so foreach yields char.
		var classText = from.Classes.ToString();
		if ( string.IsNullOrWhiteSpace( classText ) )
			return;

		var classNames = classText.Split( ' ', System.StringSplitOptions.RemoveEmptyEntries );
		for ( var i = 0; i < classNames.Length; i++ )
			to.AddClass( classNames[i] );
	}
}
