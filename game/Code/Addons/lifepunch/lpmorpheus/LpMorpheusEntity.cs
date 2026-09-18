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
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Audio;
using Sandbox.Speech;
using LifePunch.DXRP.Addons;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.Morpheus;

/// <summary>
/// Town-hall chamber: public press, host-built file or civic briefing, one parented TTS voice.
/// </summary>
[Title( "LIFEPUNCH Morpheus" )]
[Category( "LifePunch/Morpheus" )]
#if LIFEPUNCH_LOCAL
public sealed class LpMorpheusEntity : Component, Component.IPressable
#else
public sealed class LpMorpheusEntity : BaseEntity, Component.IPressable
#endif
{
	private const float DefaultHearRange = 320f;
	private const float DefaultPitch = 0.82f;
	private const float DefaultCaptionHold = 12f;
	private const float PressCooldownSeconds = 0.75f;

	[Property] public TextRenderer Caption { get; set; }

	[Property] public float HearRange { get; set; } = DefaultHearRange;

	[Property] public float VoicePitch { get; set; } = DefaultPitch;

	[Property] public float CaptionHoldSeconds { get; set; } = DefaultCaptionHold;

#if !LIFEPUNCH_LOCAL
	public override string DisplayName => "Morpheus";
	public override Color Color => new Color( 0.2f, 1f, 0.75f );
#endif

	private readonly Dictionary<long, bool> _nextCivic = new();
	private readonly Dictionary<long, TimeSince> _pressAge = new();
	private readonly MixerHandle _ttsMixer = Mixer.FindMixerByName( "TTS" );

	private SoundHandle? _voice;
	private TimeSince _captionAge;

	protected override void OnStart()
	{
		base.OnStart();

		if ( !Caption.IsValid() )
			Caption = GameObject.Children.FirstOrDefault( child => child.Name == "caption" )?.GetComponent<TextRenderer>();
	}

	protected override void OnUpdate()
	{
		var hold = CaptionHoldSeconds > 0f ? CaptionHoldSeconds : DefaultCaptionHold;
		if ( _captionAge < hold )
			return;

		if ( _voice.IsValid() && _voice.IsPlaying )
			return;

		if ( Caption.IsValid() && !string.IsNullOrEmpty( Caption.Text ) )
			Caption.Text = string.Empty;

		LpMorpheusHud.Clear();
	}

	protected override void OnDestroy()
	{
		StopVoice();
		LpMorpheusHud.Clear();
		base.OnDestroy();
	}

	public bool CanPress( IPressable.Event e )
		=> LifePunchMenuInteractGate.CanPressMenu( GameObject );

	public bool Press( IPressable.Event e )
	{
		RequestBriefingHost();
		return true;
	}

	public void Hover( IPressable.Event e ) { }
	public void Blur( IPressable.Event e ) { }

	[Rpc.Host]
	private void RequestBriefingHost()
	{
#if !LIFEPUNCH_LOCAL
		if ( !LifePunchMenuInteractGate.IsCallerAllowed( Rpc.Caller, GameObject ) )
			return;

		var player = GameUtils.GetPlayerByConnectionId( Rpc.CallerId );
		if ( !player.IsValid() )
			return;

		if ( _pressAge.TryGetValue( player.SteamId, out var since ) && since < PressCooldownSeconds )
			return;

		_pressAge[player.SteamId] = 0;

		var civic = false;
		if ( _nextCivic.TryGetValue( player.SteamId, out var nextCivic ) )
			civic = nextCivic;
		_nextCivic[player.SteamId] = !civic;

		var line = civic ? BuildCivicLineHost() : BuildFileLineHost( player );
#else
		if ( !LifePunchMenuInteractGate.IsCallerAllowed( null, GameObject ) )
			return;

		var civic = false;
		var line = LpMorpheusSpeech.BuildLocalEditorFile();
#endif

		line = LpMorpheusSpeech.Sanitize( line );
		if ( string.IsNullOrWhiteSpace( line ) )
			return;

		Log.Info( $"LP_MORPHEUS_BRIEFING civic={civic} chars={line.Length}" );
		PlayBriefing( line, civic );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void PlayBriefing( string text, bool civic )
	{
		var line = LpMorpheusSpeech.Sanitize( text );
		if ( string.IsNullOrWhiteSpace( line ) )
			return;

		if ( !IsLocalListenerInRange() )
			return;

		// Start the hold here rather than in ShowCaption: the readout has to expire on
		// this same clock even when the chamber has no caption child to write to.
		_captionAge = 0;
		ShowCaption( line );
		LpMorpheusHud.Show( LpMorpheusReadout.FromBriefing( line, civic ) );
		SpeakLocal( line );
	}

#if !LIFEPUNCH_LOCAL
	private static string BuildFileLineHost( Player player )
	{
		var wanted = player.HasStatus( Constants.WantedStatus );
		var government = player.Job.IsGovernmentRole();
		return LpMorpheusSpeech.BuildFileGreeting(
			player.DisplayName,
			player.JobDisplayName,
			wanted,
			government );
	}

	private static string BuildCivicLineHost()
	{
		var wantedNames = new List<string>();
		if ( GameNetworkManager.Instance is not null )
		{
			foreach ( var other in GameUtils.Players )
			{
				if ( !other.IsValid() || !other.HasStatus( Constants.WantedStatus ) )
					continue;

				wantedNames.Add( other.DisplayName ?? string.Empty );
			}
		}

		var laws = new List<string>();
		var governance = Governance.Current;
		if ( governance is not null )
		{
			foreach ( var raw in governance.GetAllLaws() )
				laws.Add( LpMorpheusSpeech.ResolveLaw( raw ) );
		}

		return LpMorpheusSpeech.BuildCivicBriefing( wantedNames, laws );
	}
#endif

	private bool IsLocalListenerInRange()
	{
#if !LIFEPUNCH_LOCAL
		if ( GameManager.IsHeadless )
			return false;
#endif

		var viewer = LifePunchMenuInteractGate.GetLocalViewerPosition( GameObject.Scene );
		if ( !viewer.HasValue )
			return false;

		var range = HearRange > 0f ? HearRange : DefaultHearRange;
		return viewer.Value.Distance( WorldPosition ) <= range;
	}

	private void ShowCaption( string line )
	{
		if ( !Caption.IsValid() )
			return;

		Caption.Text = line;
		_captionAge = 0;
	}

	private void SpeakLocal( string line )
	{
#if !LIFEPUNCH_LOCAL
		if ( GameManager.IsHeadless )
			return;
#endif

		StopVoice();

		try
		{
			var synth = new Synthesizer();
			synth.TrySetVoice( LpMorpheusSpeech.VoiceName );
			if ( string.IsNullOrWhiteSpace( synth.CurrentVoice ) )
				return;

			synth.WithText( line );
			var sound = synth.Play();
			if ( !sound.IsValid() )
				return;

			sound.Parent = GameObject;
			sound.FollowParent = true;
			sound.Pitch = VoicePitch > 0f ? VoicePitch : DefaultPitch;
#if !LIFEPUNCH_LOCAL
			sound.Volume = DxSound.TtsVolume;
#endif
			sound.TargetMixer = _ttsMixer.GetOrDefault();
			_voice = sound;
		}
		catch
		{
			// Fail closed: caption stays, no crash when the Windows voice is missing.
		}
	}

	private void StopVoice()
	{
		if ( _voice.IsValid() )
			_voice.Stop();

		_voice = null;
	}
}
