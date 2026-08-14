// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH Bitcoin Miner for DXRP" (s&box ident: lifepunch.bitcoin · addon ident: bitcoinmining)
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Bitcoin;

/// <summary>
/// Hub world visuals — fence emissive status LED (green ON / red OFF) on the vmdl mesh only.
/// Phase 0: fan mesh is part of <c>bitcoinhub.vmdl</c> (no child GO spin). Phase 2: optional fan child.
/// H4/H5: optional power glow + startup / fan loop / fan down audio at the hub origin.
/// </summary>
public sealed class LpBitcoinHubVisuals : Component
{
	private const float FanMaxSpeed = 900f;
	private const float FanRampSeconds = 6f;
	private const float SoundFadeSeconds = 0.25f;
	private const float MaxFanLoopVolume = 0.3f;

	private static readonly Vector3 FanSpinAxis = Vector3.Up;
	private const float FanSpinSign = -1f;

	private static readonly Color PowerGlowOnColor = new( 0.15f, 1f, 0.45f, 1f );

	[Property] public LpBitcoinHubEntity Hub { get; set; }

	[Property] public bool EnablePowerGlow { get; set; } = true;

	/// <summary>Phase 2 only — fan is baked into body vmdl for Phase 0.</summary>
	[Property] public bool AutoAlignFanToGrille { get; set; } = false;

	/// <summary>Fine-tune after auto-align (prefab editor values stack on top).</summary>
	[Property] public Vector3 FanManualOffset { get; set; }

	private GameObject _fanChild;
	private GameObject _powerGlowGo;
	private PointLight _powerGlow;
	private Rotation _fanBaseLocalRotation = Rotation.Identity;
	private float _fanSpeed;
	private float _fanAngle;
	private bool _lastPowered;
	private bool _fanAlignPending = true;
	private TimeSince _sinceStart;
	private int _materialRefreshPasses;

	private SoundHandle? _fanLoopHandle;

	private ModelRenderer _bodyRenderer;

	protected override void OnStart()
	{
		if ( !Hub.IsValid() )
			Hub = Components.Get<LpBitcoinHubEntity>( FindMode.EverythingInSelf );

		_bodyRenderer = GameObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
		                 ?? GameObject.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;

		RemoveLegacyStatusLightChildren();
		CacheFanChild();
		EnsurePowerGlow();

		_lastPowered = Hub is { IsPowered: true };
		_sinceStart = 0;
		_materialRefreshPasses = 0;
		ApplyPowerVisuals( _lastPowered );
	}

	protected override void OnDestroy()
	{
		StopFanLoop( 0f );
		base.OnDestroy();
	}

	/// <summary>Called from <see cref="LpBitcoinHubEntity"/> when power toggles.</summary>
	public void ApplyPowerVisuals( bool powered )
	{
		if ( !_bodyRenderer.IsValid() )
		{
			_bodyRenderer = GameObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelf )
			                 ?? GameObject.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf ) as ModelRenderer;
		}

		LpBitcoinPowerLeds.ApplyHubStatusLed( _bodyRenderer, powered );
		UpdatePowerGlow( powered );
		UpdateHubFanSounds( powered );
		_lastPowered = powered;

		if ( !powered )
		{
			_fanSpeed = 0f;
			_fanAngle = 0f;
		}
	}

	private static void RemoveLegacyStatusLightChildren( GameObject root )
	{
		foreach ( var child in root.Children.ToList() )
		{
			if ( !child.IsValid() )
				continue;

			if ( child.Name.Equals( "status_led", System.StringComparison.OrdinalIgnoreCase )
			     || child.Name.Equals( "hub_status_glow", System.StringComparison.OrdinalIgnoreCase ) )
				child.Destroy();
		}
	}

	private void RemoveLegacyStatusLightChildren()
		=> RemoveLegacyStatusLightChildren( GameObject );

	private void CacheFanChild()
	{
		_fanChild = GameObject.Children
			.FirstOrDefault( child => child.IsValid()
			                          && child.Name.StartsWith( "fan_spin", System.StringComparison.OrdinalIgnoreCase ) );

		if ( !_fanChild.IsValid() )
			return;

		_fanChild.Flags |= GameObjectFlags.NoInterpolation;
		_fanBaseLocalRotation = Rotation.Identity;
		_fanChild.LocalRotation = _fanBaseLocalRotation;
		_fanAngle = 0f;
	}

	private void EnsurePowerGlow()
	{
		if ( !EnablePowerGlow )
			return;

		_powerGlowGo = GameObject.Children
			.FirstOrDefault( child => child.IsValid()
			                          && child.Name.Equals( "hub_power_glow", System.StringComparison.OrdinalIgnoreCase ) );

		if ( !_powerGlowGo.IsValid() )
		{
			_powerGlowGo = new GameObject( GameObject, false, "hub_power_glow" );
			_powerGlow = _powerGlowGo.Components.Create<PointLight>();
			_powerGlow.Radius = 140f;
			_powerGlow.Attenuation = 0.75f;
			_powerGlow.LightColor = PowerGlowOnColor;
			_powerGlow.Shadows = false;
			_powerGlow.Enabled = false;
		}
		else
		{
			_powerGlow = _powerGlowGo.Components.Get<PointLight>( FindMode.EverythingInSelf );
		}

		AlignPowerGlowToBody();
	}

	private void AlignPowerGlowToBody()
	{
		if ( !_powerGlowGo.IsValid() || !_bodyRenderer.IsValid() )
			return;

		var bounds = _bodyRenderer.LocalBounds;
		if ( bounds.Size.Length < 0.01f )
			return;

		_powerGlowGo.LocalPosition = new Vector3(
			bounds.Maxs.x * 0.55f,
			bounds.Center.y,
			bounds.Center.z + bounds.Size.z * 0.08f );
	}

	private void UpdatePowerGlow( bool powered )
	{
		if ( !EnablePowerGlow )
		{
			if ( _powerGlow.IsValid() )
				_powerGlow.Enabled = false;

			return;
		}

		EnsurePowerGlow();
		if ( !_powerGlow.IsValid() )
			return;

		_powerGlow.Enabled = powered;
		if ( powered )
			_powerGlow.LightColor = PowerGlowOnColor;
	}

	private void TryAlignFanToGrille()
	{
		if ( !AutoAlignFanToGrille || !_fanChild.IsValid() || !_bodyRenderer.IsValid() )
			return;

		var fanRenderer = _fanChild.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( !fanRenderer.IsValid() )
			return;

		var bodyBounds = _bodyRenderer.LocalBounds;
		var fanBounds = fanRenderer.LocalBounds;
		if ( bodyBounds.Size.Length < 0.01f || fanBounds.Size.Length < 0.01f )
			return;

		var fanExtent = MathF.Max( fanBounds.Size.x, MathF.Max( fanBounds.Size.y, fanBounds.Size.z ) );
		var targetCenter = new Vector3(
			bodyBounds.Maxs.x - fanExtent * 0.35f,
			bodyBounds.Center.y,
			bodyBounds.Center.z ) + FanManualOffset;

		_fanChild.LocalPosition = targetCenter - fanBounds.Center;
		_fanBaseLocalRotation = Rotation.Identity;
		_fanChild.LocalRotation = Rotation.FromAxis( FanSpinAxis, _fanAngle ) * _fanBaseLocalRotation;
	}

	protected override void OnUpdate()
	{
		if ( !Hub.IsValid() )
			return;

		if ( _fanAlignPending )
		{
			TryAlignFanToGrille();
			AlignPowerGlowToBody();
			_fanAlignPending = false;
		}

		if ( Hub.IsPowered != _lastPowered )
			ApplyPowerVisuals( Hub.IsPowered );

		if ( _materialRefreshPasses < 6 && _sinceStart > _materialRefreshPasses * 0.35f )
		{
			_materialRefreshPasses++;
			ApplyPowerVisuals( Hub.IsPowered );
		}

		UpdateFanRamp();
		UpdateFanLoopVolume();

		if ( !_fanChild.IsValid() || !_fanChild.Enabled || _fanSpeed <= 0f )
			return;

		_fanAngle += FanSpinSign * _fanSpeed * Time.Delta;
		_fanChild.LocalRotation = Rotation.FromAxis( FanSpinAxis, _fanAngle ) * _fanBaseLocalRotation;
	}

	private void UpdateFanRamp()
	{
		if ( !_fanChild.IsValid() || !_fanChild.Enabled )
			return;

		var target = Hub.IsPowered ? FanMaxSpeed : 0f;
		var step = ( FanMaxSpeed / FanRampSeconds ) * Time.Delta;

		if ( _fanSpeed < target )
			_fanSpeed = MathF.Min( _fanSpeed + step, target );
		else if ( _fanSpeed > target )
			_fanSpeed = MathF.Max( _fanSpeed - step, target );
	}

	private void UpdateHubFanSounds( bool powered )
	{
		if ( !ShouldPlayWorldAudio() )
			return;

		var wasPowered = _lastPowered;
		if ( powered && !wasPowered )
		{
			PlayOneShot( LpBitcoinIdent.HubStartupSoundPath );
			StartFanLoop();
		}
		else if ( !powered && wasPowered )
		{
			PlayOneShot( LpBitcoinIdent.HubFanDownSoundPath );
			StopFanLoop( FanRampSeconds );
		}
	}

	private void StartFanLoop()
	{
		StopFanLoop( 0f );

		_fanLoopHandle = Sound.Play( LpBitcoinIdent.HubFanLoopSoundPath, WorldPosition, 0f );
		if ( !_fanLoopHandle.IsValid() )
			return;

		_fanLoopHandle.Volume = 0f;
		_fanLoopHandle.FollowParent = true;
		_fanLoopHandle.Parent = GameObject;
	}

	private void PlayOneShot( string soundPath )
	{
		var handle = Sound.Play( soundPath, WorldPosition, SoundFadeSeconds );
		if ( !handle.IsValid() )
			return;

		handle.FollowParent = true;
		handle.Parent = GameObject;
	}

	private void UpdateFanLoopVolume()
	{
		if ( !_fanLoopHandle.IsValid() )
			return;

		_fanLoopHandle.Position = WorldPosition;

		if ( !Hub.IsPowered )
		{
			if ( _fanSpeed <= 0f )
				StopFanLoop( 0.1f );

			return;
		}

		var ramp = FanMaxSpeed <= 0f ? 1f : _fanSpeed / FanMaxSpeed;
		_fanLoopHandle.Volume = MaxFanLoopVolume * Math.Clamp( ramp, 0f, 1f );
	}

	private void StopFanLoop( float fadeSeconds )
	{
		if ( !_fanLoopHandle.IsValid() )
			return;

		_fanLoopHandle.Stop( fadeSeconds );
		_fanLoopHandle = null;
	}

	private static bool ShouldPlayWorldAudio()
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		return !GameManager.IsHeadless;
#endif
	}
}
