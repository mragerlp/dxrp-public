// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Runtime emissive on hub/rack vmdl materials (<c>F_SELF_ILLUM</c> slots only).
/// Hub status: green when powered, red when off — always readable at a glance.
/// </summary>
public static class LpBitcoinPowerLeds
{
	private const string SelfIllumScaleAttr = "g_flSelfIllumScale";
	private const string SelfIllumTintAttr = "g_vSelfIllumTint";

	public const float HubStatusOnScale = 2.5f;
	public const float HubStatusOffScale = 1.75f;
	public const float RackGpuLedOn = 1.5f;

	private static readonly Vector4 HubStatusOnTint = new( 0.15f, 1f, 0.45f, 0f );
	private static readonly Vector4 HubStatusOffTint = new( 1f, 0.12f, 0.05f, 0f );

	private static readonly string[] HubStatusMaterialTokens =
	[
		"fence-led",
		"fence_led",
		"sm_fence_led",
		"bitcoinhub-sm-fence-led"
	];

	/// <summary>Hub fence LED — green ON, red OFF (static chassis, no anims).</summary>
	public static void ApplyHubStatusLed( ModelRenderer renderer, bool powered )
	{
		if ( !renderer.IsValid() )
			return;

		var materials = renderer.Materials;
		if ( materials is null || materials.Count <= 0 )
			return;

		var tint = powered ? HubStatusOnTint : HubStatusOffTint;
		var scale = powered ? HubStatusOnScale : HubStatusOffScale;
		var changed = false;

		for ( var i = 0; i < materials.Count; i++ )
		{
			var original = materials.GetOriginal( i );
			if ( original is null || !IsHubStatusLedMaterial( original ) )
				continue;

			var copy = materials.GetOverride( i );
			if ( !copy.IsValid() )
				copy = original.CreateCopy();

			copy.Set( SelfIllumTintAttr, tint );
			copy.Set( SelfIllumScaleAttr, scale );
			materials.SetOverride( i, copy );
			changed = true;
		}

		if ( changed )
			materials.Apply();
	}

	/// <summary>Legacy name — routes to <see cref="ApplyHubStatusLed"/>.</summary>
	public static void ApplyHubFenceLeds( ModelRenderer renderer, bool powered )
		=> ApplyHubStatusLed( renderer, powered );

	public static void ApplyRackGpuLeds( ModelRenderer renderer, bool active, float intensity01 = 1f )
	{
		var scale = active ? RackGpuLedOn * Math.Clamp( intensity01, 0f, 1f ) : 0f;
		ApplySelfIllum( renderer, scale );
	}

	public static void ApplySelfIllum( ModelRenderer renderer, float scale )
	{
		if ( !renderer.IsValid() )
			return;

		var sceneObject = renderer.SceneObject;
		if ( sceneObject is null || !sceneObject.IsValid() )
			return;

		sceneObject.Attributes.Set( SelfIllumScaleAttr, scale );
	}

	private static bool IsHubStatusLedMaterial( Material material )
	{
		var path = material.ResourcePath ?? string.Empty;
		foreach ( var token in HubStatusMaterialTokens )
		{
			if ( path.Contains( token, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return material.GetFeature( "F_SELF_ILLUM" ) > 0
		       && path.Contains( "fence", StringComparison.OrdinalIgnoreCase );
	}

}
