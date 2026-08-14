// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Client-side GPU rack visuals — Phase 0: static unified vmdl (fans baked in mesh). LEDs only.
/// Phase 2+: separate fan child GOs or vmdl <c>power_on</c> when ModelDoc-signed-off.
/// </summary>
public sealed class LpBitcoinRackVisuals : Component
{
	/// <summary>
	/// Phase 0 — static body only (gpu-rack-static.obj / stacked static FBX). No child fan GOs in prefab.
	/// </summary>
	private const bool RackFanVisualsParked = true;
	private const float FanMaxSpeed = 1200f;
	private const float FanRampSeconds = 4f;
	private const float VisualTickSeconds = 0.05f;
	private const float FanMeshTiltDegrees = 10f;
	private const float FanHideSpeedThreshold = 1f;

	[Property] public LpBitcoinRackEntity Rack { get; set; }

	private GameObject[] _fanChildren = Array.Empty<GameObject>();
	private Rotation[] _fanBaseLocalRotation = Array.Empty<Rotation>();
	private float[] _fanAngles = Array.Empty<float>();
	private ModelRenderer _modelRenderer;
	private float _fanSpeed;
	private bool _occluded;
	private bool _vmdlAnimActive;
	private bool _lastAppliedMiningVisual;

	protected override void OnStart()
	{
		if ( !Rack.IsValid() )
			Rack = Components.Get<LpBitcoinRackEntity>( FindMode.EverythingInSelf );

		CacheFanChildren();
		_modelRenderer = ResolveBodyRenderer();
#if !LIFEPUNCH_LOCAL
		if ( !RackFanVisualsParked )
			ApplyMiningPowerAnim( force: true );
#endif
	}

	private ModelRenderer ResolveBodyRenderer()
	{
		return Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
		       ?? Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;
	}

	private void CacheFanChildren()
	{
		if ( RackFanVisualsParked || !GameObject.IsValid() )
		{
			_fanChildren = Array.Empty<GameObject>();
			_fanBaseLocalRotation = Array.Empty<Rotation>();
			_fanAngles = Array.Empty<float>();
			return;
		}

		_fanChildren = GameObject.Children
			.Where( child => child.IsValid()
			                   && ( child.Name.StartsWith( "fan_spin_", StringComparison.OrdinalIgnoreCase )
			                        || child.Name.StartsWith( "fan_placeholder", StringComparison.OrdinalIgnoreCase ) )
			                   && HasFanRenderer( child ) )
			.ToArray();

		_fanBaseLocalRotation = new Rotation[_fanChildren.Length];
		_fanAngles = new float[_fanChildren.Length];
		var tilt = Rotation.FromAxis( Vector3.Right, FanMeshTiltDegrees );

		for ( var i = 0; i < _fanChildren.Length; i++ )
		{
			var fan = _fanChildren[i];
			fan.Flags |= GameObjectFlags.NoInterpolation;
			_fanBaseLocalRotation[i] = tilt;
			fan.LocalRotation = tilt;
			fan.Enabled = false;
		}
	}

	private static bool HasFanRenderer( GameObject fan )
	{
		var renderer = fan.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		return renderer.IsValid() && renderer.Model.IsValid();
	}

#if !LIFEPUNCH_LOCAL
	public void OnOcclusionChanged( bool occlude ) => _occluded = occlude;

	/// <summary>Host/client sync hook — refresh fan visibility immediately when mining toggles.</summary>
	internal void SyncMiningVisualState()
	{
		if ( GameManager.IsHeadless || !Rack.IsValid() )
			return;

		if ( !RackFanVisualsParked )
		{
			ApplyMiningPowerAnim( force: true );
			UpdateFanRamp();
			UpdateFanChildEnabled();
		}

		UpdateMiningLeds();
	}
#endif

	protected override void OnUpdate()
	{
#if LIFEPUNCH_LOCAL
		return;
#else
		if ( !Rack.IsValid() || _occluded || GameManager.IsHeadless )
			return;

		if ( Cooldown.Current.CheckAndStartCooldown( $"{GameObject.Id}:rack-vis", VisualTickSeconds ) )
			return;

		if ( !RackFanVisualsParked )
		{
			ApplyMiningPowerAnim();
			UpdateFanRamp();
			UpdateFanChildEnabled();
			SpinFans();
		}

		UpdateMiningLeds();
#endif
	}

	private bool IsMiningVisualActive()
	{
		var hub = Rack.GetLinkedHub();
		return Rack.IsMining && hub is { IsPowered: true };
	}

	/// <summary>
	/// v1 dual path: play vmdl sequence when compiled; child-GO spin remains fallback (standard rack).
	/// </summary>
	private void ApplyMiningPowerAnim( bool force = false )
	{
#if LIFEPUNCH_LOCAL
		return;
#else
		if ( RackFanVisualsParked )
		{
			_vmdlAnimActive = false;
			return;
		}
		var miningActive = IsMiningVisualActive();
		if ( !force && miningActive == _lastAppliedMiningVisual )
			return;

		_lastAppliedMiningVisual = miningActive;

		if ( !_modelRenderer.IsValid() )
			_modelRenderer = ResolveBodyRenderer();

		if ( !_modelRenderer.IsValid() )
		{
			_vmdlAnimActive = false;
			return;
		}

		_vmdlAnimActive = LpBitcoinPowerAnim.ApplyRackPower( _modelRenderer, miningActive, out _ );

		if ( _vmdlAnimActive && miningActive )
			_fanSpeed = FanMaxSpeed;
		else if ( _vmdlAnimActive )
			_fanSpeed = 0f;
#endif
	}

	private void UpdateFanRamp()
	{
		if ( _vmdlAnimActive )
			return;

		var target = IsMiningVisualActive() ? FanMaxSpeed : 0f;
		var step = ( FanMaxSpeed / FanRampSeconds ) * Time.Delta;

		if ( _fanSpeed < target )
			_fanSpeed = MathF.Min( _fanSpeed + step, target );
		else if ( _fanSpeed > target )
			_fanSpeed = MathF.Max( _fanSpeed - step, target );
	}

	private void UpdateFanChildEnabled()
	{
		if ( _vmdlAnimActive )
		{
			for ( var i = 0; i < _fanChildren.Length; i++ )
			{
				var fan = _fanChildren[i];
				if ( !fan.IsValid() )
					continue;

				if ( fan.Enabled )
					fan.Enabled = false;
			}

			return;
		}

		var miningActive = IsMiningVisualActive();
		var fansVisible = miningActive || _fanSpeed > FanHideSpeedThreshold;

		for ( var i = 0; i < _fanChildren.Length; i++ )
		{
			var fan = _fanChildren[i];
			if ( !fan.IsValid() )
				continue;

			if ( fan.Enabled != fansVisible )
				fan.Enabled = fansVisible;

			if ( !fansVisible )
			{
				_fanAngles[i] = 0f;
				fan.LocalRotation = _fanBaseLocalRotation[i];
			}
		}
	}

	private void SpinFans()
	{
		if ( _vmdlAnimActive || _fanSpeed <= 0f || _fanChildren.Length == 0 )
			return;

		var delta = _fanSpeed * Time.Delta;

		for ( var i = 0; i < _fanChildren.Length; i++ )
		{
			var fan = _fanChildren[i];
			if ( !fan.IsValid() || !fan.Enabled )
				continue;

			_fanAngles[i] += delta;
			fan.LocalRotation = Rotation.FromAxis( Vector3.Forward, _fanAngles[i] ) * _fanBaseLocalRotation[i];
		}
	}

	private void UpdateMiningLeds()
	{
		if ( !_modelRenderer.IsValid() )
			_modelRenderer = ResolveBodyRenderer();

		if ( !_modelRenderer.IsValid() )
			return;

		var mining = IsMiningVisualActive();
		var ledIntensity = mining && FanMaxSpeed > 0f ? Math.Clamp( _fanSpeed / FanMaxSpeed, 0f, 1f ) : 0f;
		LpBitcoinPowerLeds.ApplyRackGpuLeds( _modelRenderer, mining, ledIntensity );
	}
}
