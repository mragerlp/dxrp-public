// -----------------------------------------------------------------------------
// PROPRIETARY & CONFIDENTIAL - Copyright 2026 lifepunch.co. All rights reserved.
//
// "Lockpick" (addon ident: lplockpick) is the sole-owned intellectual property
// of lifepunch.co. It is not licensed for resale, redistribution, sublicensing,
// copying, or reuse except by the owner (lifepunch.co).
// Author account: mrragerlp. Public alias: Bloodwave.
// Presence in this repository or on the DXRP portal grants no additional rights.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Dxura.RP.Game;
using Dxura.RP.Game.UI;
using Sandbox;
using Sandbox.Services;
using Sandbox.UI;

namespace Dxura.RP.Game
{
	/// <summary>Flat tuning keys for the lockpick pin-tumbler.</summary>
	public abstract partial class GameConfig
	{
		public virtual float LockpickSweetSpotWidth { get; set; } = 0.12f;
		public virtual float LockpickPinFallSpeed { get; set; } = 0.5f;
		public virtual int LockpickPinCount { get; set; } = 5;
	}
}

namespace LifePunch.DXRP.Addons.Lockpick
{
	// PURE-RULES-BEGIN
	public enum LockpickPinResult
	{
		None,
		NoTension,
		Loose,
		Binding,
		MissedBinding,
		Caught,
		Complete
	}

	/// <summary>
	/// Deterministic, engine-free pin state. The attempt seed randomizes both the
	/// binding order and each pin's sweet spot; only the current binding pin can catch.
	/// </summary>
	public sealed class LockpickMinigameRules
	{
		private readonly int[] _bindingOrder;
		private readonly float[] _pinHeights;
		private readonly float[] _sweetSpotCenters;
		private readonly bool[] _caught;
		private int _bindingIndex;

		public LockpickMinigameRules( int pinCount, int seed )
		{
			PinCount = ClampInt( pinCount, 1, 8 );
			_bindingOrder = new int[PinCount];
			_pinHeights = new float[PinCount];
			_sweetSpotCenters = new float[PinCount];
			_caught = new bool[PinCount];

			var random = new Random( seed );
			for ( var index = 0; index < PinCount; index++ )
			{
				_bindingOrder[index] = index;
				_sweetSpotCenters[index] = 0.58f + (float)random.NextDouble() * 0.3f;
			}

			for ( var index = PinCount - 1; index > 0; index-- )
			{
				var swapIndex = random.Next( index + 1 );
				var value = _bindingOrder[index];
				_bindingOrder[index] = _bindingOrder[swapIndex];
				_bindingOrder[swapIndex] = value;
			}
		}

		public int PinCount { get; private set; }
		public IReadOnlyList<int> BindingOrder { get { return _bindingOrder; } }
		public int CaughtCount { get { return _bindingIndex; } }
		public bool IsComplete { get { return _bindingIndex >= PinCount; } }
		public int CurrentBindingPin { get { return IsComplete ? -1 : _bindingOrder[_bindingIndex]; } }
		public int Revision { get; private set; }

		public float GetPinHeight( int pinIndex )
		{
			return IsPinIndexValid( pinIndex ) ? _pinHeights[pinIndex] : 0f;
		}

		public bool IsCaught( int pinIndex )
		{
			return IsPinIndexValid( pinIndex ) && _caught[pinIndex];
		}

		public float GetSweetSpotStart( int pinIndex, float width )
		{
			if ( !IsPinIndexValid( pinIndex ) )
			{
				return 0f;
			}

			var safeWidth = ClampFloat( width, 0.02f, 0.5f );
			return ClampFloat( _sweetSpotCenters[pinIndex] - safeWidth * 0.5f, 0f, 1f );
		}

		public float GetSweetSpotEnd( int pinIndex, float width )
		{
			if ( !IsPinIndexValid( pinIndex ) )
			{
				return 0f;
			}

			var safeWidth = ClampFloat( width, 0.02f, 0.5f );
			return ClampFloat( _sweetSpotCenters[pinIndex] + safeWidth * 0.5f, 0f, 1f );
		}

		public LockpickPinResult LiftPin( int pinIndex, float amount, bool hasTension, float sweetSpotWidth )
		{
			if ( !IsPinIndexValid( pinIndex ) || IsComplete || _caught[pinIndex] )
			{
				return LockpickPinResult.None;
			}

			var previousHeight = _pinHeights[pinIndex];
			var nextHeight = ClampFloat( previousHeight + Math.Max( 0f, amount ), 0f, 1f );
			if ( Math.Abs( nextHeight - previousHeight ) > 0.0001f )
			{
				_pinHeights[pinIndex] = nextHeight;
				Revision++;
			}

			if ( !hasTension )
			{
				return LockpickPinResult.NoTension;
			}

			if ( pinIndex != CurrentBindingPin )
			{
				return LockpickPinResult.Loose;
			}

			var sweetStart = GetSweetSpotStart( pinIndex, sweetSpotWidth );
			var sweetEnd = GetSweetSpotEnd( pinIndex, sweetSpotWidth );
			if ( previousHeight <= sweetEnd && nextHeight >= sweetStart )
			{
				_caught[pinIndex] = true;
				_pinHeights[pinIndex] = (sweetStart + sweetEnd) * 0.5f;
				_bindingIndex++;
				Revision++;
				return IsComplete ? LockpickPinResult.Complete : LockpickPinResult.Caught;
			}

			return previousHeight > sweetEnd
				? LockpickPinResult.MissedBinding
				: LockpickPinResult.Binding;
		}

		public void UpdateTension( bool hasTension, float deltaSeconds, float fallSpeed )
		{
			if ( hasTension )
			{
				return;
			}

			var fallAmount = Math.Max( 0f, deltaSeconds ) * Math.Max( 0f, fallSpeed );
			if ( fallAmount <= 0f )
			{
				return;
			}

			var changed = false;
			for ( var index = 0; index < PinCount; index++ )
			{
				if ( _caught[index] || _pinHeights[index] <= 0f )
				{
					continue;
				}

				_pinHeights[index] = Math.Max( 0f, _pinHeights[index] - fallAmount );
				changed = true;
			}

			if ( changed )
			{
				Revision++;
			}
		}

		/// <summary>
		/// HOST-VERIFY: apply a precomputed fall amount to every uncaught lifted pin.
		/// Same semantics as UpdateTension(false, dt, fallSpeed) with the product
		/// already taken -- the shared client/replay path, so determinism is
		/// single-sourced. Quantized ops mean no dt ever reaches the rules.
		/// </summary>
		public void ApplyFall( float amount )
		{
			if ( amount <= 0f )
			{
				return;
			}

			var changed = false;
			for ( var index = 0; index < PinCount; index++ )
			{
				if ( _caught[index] || _pinHeights[index] <= 0f )
				{
					continue;
				}

				_pinHeights[index] = Math.Max( 0f, _pinHeights[index] - amount );
				changed = true;
			}

			if ( changed )
			{
				Revision++;
			}
		}

		private bool IsPinIndexValid( int pinIndex )
		{
			return pinIndex >= 0 && pinIndex < PinCount;
		}

		private static int ClampInt( int value, int minimum, int maximum )
		{
			return Math.Min( maximum, Math.Max( minimum, value ) );
		}

		private static float ClampFloat( float value, float minimum, float maximum )
		{
			return Math.Min( maximum, Math.Max( minimum, value ) );
		}
	}

	/// <summary>
	/// HOST-VERIFY V1 wire format. One op per int:
	/// bits [26:24] kind, [18:16] pin, [15:0] amountQ (heights in 1/256 units).
	/// The client records ops and applies them; the host replays the identical
	/// ops through a fresh rules instance built from its own begin-time capture.
	/// Structural bounds only -- no op carries or implies wall time (R3).
	/// </summary>
	public static class LockpickTranscript
	{
		public const int MaxOps = 4096;
		public const float Quantum = 1f / 256f;
		public const int KindLiftTensioned = 0;
		public const int KindLiftFree = 1;
		public const int KindFall = 2;
		public const int MaxLiftQuanta = 64;
		public const int MaxFallQuanta = 16384;

		public static int Pack( int kind, int pin, int amountQ )
		{
			return ((kind & 0x7) << 24) | ((pin & 0x7) << 16) | (amountQ & 0xFFFF);
		}

		public static bool Replay(
			int[]? ops,
			int seed,
			int pinCount,
			float sweetSpotWidth,
			out string failReason )
		{
			failReason = "";
			if ( ops is null || ops.Length == 0 )
			{
				failReason = "empty";
				return false;
			}

			if ( ops.Length > MaxOps )
			{
				failReason = $"opcount={ops.Length}";
				return false;
			}

			var rules = new LockpickMinigameRules( pinCount, seed );
			foreach ( var packed in ops )
			{
				var kind = (packed >> 24) & 0x7;
				var pin = (packed >> 16) & 0x7;
				var amountQ = packed & 0xFFFF;

				// SPEC1 s.2.3 order -- kind, then pin, then the kind's amount bound --
				// applied to EVERY kind BEFORE any apply. No FALL exemption.
				if ( kind != KindFall && kind != KindLiftTensioned && kind != KindLiftFree )
				{
					failReason = "kind";
					return false;
				}

				if ( pin >= rules.PinCount )
				{
					failReason = "pin";
					return false;
				}

				if ( kind == KindFall )
				{
					if ( amountQ < 1 || amountQ > MaxFallQuanta )
					{
						failReason = "fallq";
						return false;
					}

					rules.ApplyFall( amountQ * Quantum );
					continue;
				}

				if ( amountQ < 1 || amountQ > MaxLiftQuanta )
				{
					failReason = "liftq";
					return false;
				}

				rules.LiftPin( pin, amountQ * Quantum, kind == KindLiftTensioned, sweetSpotWidth );
			}

			if ( !rules.IsComplete )
			{
				failReason = "incomplete";
				return false;
			}

			return true;
		}
	}
	// PURE-RULES-END

	/// <summary>
	/// Async pin-tumbler replacement for BUILD 1's timed hold lifecycle.
	/// The inherited entry override is <see cref="LockpickEquipment.OnInputDown"/>.
	/// </summary>
	public sealed class LockpickMinigameEquipment : LockpickEquipment, IEquipmentEvents, IInputHints
	{
		private const float HostDistanceTolerance = 50f;
		private const float ClientRequestTimeout = 3f;
		private const float HostBeginThrottle = 1f;

		private bool _clientAwaitingHost;
		private bool _clientAttemptActive;
		private int _clientRequestSerial;
		private Guid _clientAttemptToken;
		private GameObject? _clientTarget;
		private TimeSince _clientRequestAge;

		private Guid _hostAttemptToken;
		private Guid _hostCallerId;
		private long _hostSteamId;
		private GameObject? _hostTarget;
		private string _hostTargetName = "";
		private string _hostDoorName = "";
		private float _hostAttemptStartedAt;
		private int _hostSeed;
		private int _hostPinCount;
		private float _hostSweetSpotWidth;
		private int _hostVerifyOps;
		private string _hostVerifyText = "absent";

		protected override bool ResolveLockpickOutcome( Player player, GameObject target ) => false;

		protected override void OnInputDown()
		{
			if ( _clientAwaitingHost || _clientAttemptActive || LockpickMinigameUiHost.IsOpen )
			{
				return;
			}

			var player = Player.Local;
			if ( !player.IsValid() || player.IsDead || !Equipment.IsValid() || !Equipment.IsDeployed )
			{
				return;
			}

			var trace = GetTrace( Config.Current.Game.ReachDistance );
			if ( trace is not { Hit: true } || !trace.Value.GameObject.IsValid() )
			{
				return;
			}

			var target = trace.Value.GameObject.Root;
			if ( !TryGetLockedDoor( target, out _, out var reason ) )
			{
				if ( !string.IsNullOrWhiteSpace( reason ) )
				{
					Notify.Error( reason );
				}

				return;
			}

			_clientRequestSerial++;
			_clientAwaitingHost = true;
			_clientRequestAge = 0;
			BeginAttemptHost( target, _clientRequestSerial );
		}

		protected override void OnInputUp()
		{
			// BUILD 1 cancels its hold here. The minigame intentionally does not.
		}

		protected override void OnInputFixedUpdate()
		{
			if ( _clientAwaitingHost && _clientRequestAge > ClientRequestTimeout )
			{
				_clientAwaitingHost = false;
				Notify.Error( "#notify.lockpick.begin_failed" );
			}

			if ( !_clientAttemptActive )
			{
				return;
			}

			var player = Player.Local;
			if ( !player.IsValid() || player.IsDead || !Equipment.IsValid() || !Equipment.IsDeployed ||
			     !_clientTarget.IsValid() || !TryGetLockedDoor( _clientTarget, out _, out _ ) )
			{
				CancelClientAttempt( true );
				return;
			}

			if ( CalculateDistance( player, _clientTarget ) > Config.Current.Game.LockpickMaxDistance )
			{
				Notify.Warn( "#notify.prybar.toofar" );
				CancelClientAttempt( true );
			}
		}

		protected override void OnFixedUpdate()
		{
			base.OnFixedUpdate();

			if ( !Networking.IsHost || _hostAttemptToken == Guid.Empty )
			{
				return;
			}

			var player = GameUtils.GetPlayerByConnectionId( _hostCallerId );
			if ( !player.IsValid() || player.IsDead || !Equipment.IsValid() || !Equipment.IsDeployed ||
			     Equipment.Owner != player || !_hostTarget.IsValid() ||
			     !TryGetLockedDoor( _hostTarget, out _, out _ ) ||
			     CalculateDistance( player, _hostTarget ) > Config.Current.Game.LockpickMaxDistance + HostDistanceTolerance )
			{
				ResolveHostAttempt( false, false );
			}
		}

		protected override void OnDisabled()
		{
			StopReplacementLifecycle();
		}

		void IEquipmentEvents.OnEquipmentHolstered( Equipment equipment )
		{
			StopReplacementLifecycle();
		}

		void IEquipmentEvents.OnEquipmentDestroyed( Equipment equipment )
		{
			StopReplacementLifecycle();
		}

		IEnumerable<(string Action, string Label)> IInputHints.GetInputHints()
		{
			if ( !_clientAwaitingHost && !_clientAttemptActive )
			{
				yield return ("attack1", Language.GetPhrase( "lockpick.minigame.input.pick_lock" ));
			}
		}

		int IInputHints.GetInputHintsHash()
		{
			return HashCode.Combine( _clientAwaitingHost, _clientAttemptActive );
		}

		internal void CompleteFromPanel( int[] transcriptOps )
		{
			if ( !_clientAttemptActive || _clientAttemptToken == Guid.Empty )
			{
				return;
			}

			var token = _clientAttemptToken;
			ClearClientState();
			CompleteAttemptHost( token, transcriptOps );
		}

		internal void CancelFromPanel()
		{
			CancelClientAttempt( true );
		}

		[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
		private void BeginAttemptHost( GameObject target, int requestSerial )
		{
			var callerId = Rpc.CallerId;
			if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:lockpick:begin", HostBeginThrottle ) )
			{
				RejectAttemptOwner( requestSerial );
				return;
			}

			if ( _hostAttemptToken != Guid.Empty )
			{
				RejectAttemptOwner( requestSerial );
				return;
			}

			var player = GameUtils.GetPlayerByConnectionId( callerId );
			if ( !player.IsValid() || player.IsDead || !Equipment.IsValid() || !Equipment.IsDeployed ||
			     Equipment.Owner != player || !TryGetLockedDoor( target, out _, out _ ) ||
			     CalculateDistance( player, target ) > Config.Current.Game.LockpickMaxDistance )
			{
				RejectAttemptOwner( requestSerial );
				return;
			}

			_hostAttemptToken = Guid.NewGuid();
			_hostCallerId = callerId;
			_hostSteamId = player.SteamId;
			_hostTarget = target;
			_hostTargetName = Language.GetPhrase( "roleplay.door.name" );
			_hostDoorName = target.Name;
			_hostAttemptStartedAt = RealTime.Now;

			GameManager.Instance.BroadcastTagHost( target, true, Constants.PryingTag );

			_hostSeed = _hostAttemptToken.GetHashCode();
			_hostPinCount = SanitizedPinCount();
			_hostSweetSpotWidth = SanitizedSweetSpotWidth();
			BeginAttemptOwner(
				target,
				requestSerial,
				_hostAttemptToken,
				_hostSeed,
				_hostPinCount,
				_hostSweetSpotWidth,
				SanitizedPinFallSpeed() );
		}

		[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
		private void BeginAttemptOwner(
			GameObject target,
			int requestSerial,
			Guid attemptToken,
			int seed,
			int pinCount,
			float sweetSpotWidth,
			float pinFallSpeed )
		{
			if ( !_clientAwaitingHost || requestSerial != _clientRequestSerial )
			{
				CancelAttemptHost( attemptToken );
				return;
			}

			_clientAwaitingHost = false;
			_clientAttemptActive = true;
			_clientAttemptToken = attemptToken;
			_clientTarget = target;

			if ( !LockpickMinigameUiHost.Open( this, seed, pinCount, sweetSpotWidth, pinFallSpeed ) )
			{
				CancelClientAttempt( true );
			}
		}

		[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
		private void RejectAttemptOwner( int requestSerial )
		{
			if ( _clientAwaitingHost && requestSerial == _clientRequestSerial )
			{
				_clientAwaitingHost = false;
				Notify.Error( "#notify.lockpick.begin_failed" );
			}
		}

		[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
		private void CompleteAttemptHost( Guid attemptToken, int[] transcriptOps )
		{
			if ( !IsMatchingHostAttempt( attemptToken, Rpc.CallerId ) )
			{
				return;
			}

			var player = GameUtils.GetPlayerByConnectionId( _hostCallerId );
			if ( !player.IsValid() || player.IsDead || !Equipment.IsValid() || !Equipment.IsDeployed ||
			     Equipment.Owner != player || !_hostTarget.IsValid() ||
			     !TryGetLockedDoor( _hostTarget, out _, out _ ) ||
			     CalculateDistance( player, _hostTarget ) > Config.Current.Game.LockpickMaxDistance + HostDistanceTolerance )
			{
				ResolveHostAttempt( false, true );
				return;
			}

			_hostVerifyOps = transcriptOps?.Length ?? 0;
			if ( !LockpickTranscript.Replay(
				     transcriptOps, _hostSeed, _hostPinCount, _hostSweetSpotWidth, out var failReason ) )
			{
				// MUST precede the resolve: ResolveHostAttempt captures _hostVerifyText
				// into its audit locals (2f).
				_hostVerifyText = "fail";

				// MUST be captured before the resolve: ClearHostState blanks both (2c).
				var forgeryOps = _hostVerifyOps;
				var forgeryDoorName = _hostDoorName;

				// State-clear-before-side-effects (SPEC1 s.2.3). The resolve arms the
				// re-entrancy guard, lowers PryingTag, closes the panel, AND FILES THE
				// AUDIT ROW. Sentinel is reported only once that record is safe: a throw
				// inside ReportViolation must never be able to suppress the evidence of
				// the forgery that triggered it.
				ResolveHostAttempt( false, true );
				ReportTranscriptForgery( player, failReason, forgeryOps, forgeryDoorName );
				return;
			}

			_hostVerifyText = "pass";
			// Five caught pins PROVEN BY REPLAY; still no elapsed-time floor (R3):
			// verification is of play, never of clock.
			ResolveHostAttempt( true, true );
		}

		[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
		private void CancelAttemptHost( Guid attemptToken )
		{
			if ( IsMatchingHostAttempt( attemptToken, Rpc.CallerId ) )
			{
				ResolveHostAttempt( false, false );
			}
		}

		[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
		private void CloseAttemptOwner( Guid attemptToken )
		{
			if ( _clientAttemptActive && _clientAttemptToken == attemptToken )
			{
				ClearClientState();
			}
		}

		private void ResolveHostAttempt( bool success, bool auditFailure )
		{
			if ( _hostAttemptToken == Guid.Empty )
			{
				return;
			}

			var attemptToken = _hostAttemptToken;
			var target = _hostTarget;
			var player = GameUtils.GetPlayerByConnectionId( _hostCallerId );
			var steamId = _hostSteamId;
			var targetName = _hostTargetName;
			var doorName = _hostDoorName;
			var elapsedSeconds = Math.Max( 0f, RealTime.Now - _hostAttemptStartedAt );
			var verifyOps = _hostVerifyOps;
			var verifyText = _hostVerifyText;
			var door = target.IsValid() ? target.GetComponentInParent<Door>() : null;

			ClearHostState();
			CloseAttemptOwner( attemptToken );

			if ( success && door.IsValid() )
			{
				door.LpSetLockedHost( false );
				LockpickRelockTimer.Schedule( door, Config.Current.Game.LockpickDuration );

				if ( player.IsValid() )
				{
					player.IncrementStat( "lockpick", 1 );
					player.IncrementStat( "lockpick-door", 1 );
					player.Success( "#notify.prybar.success_door" );
				}
			}

			if ( success || auditFailure )
			{
				AuditAttempt( steamId, targetName, doorName, success && door.IsValid(), elapsedSeconds, verifyOps, verifyText );
			}
		}

		private void ClearHostState()
		{
			if ( _hostTarget.IsValid() )
			{
				GameManager.Instance.BroadcastTagHost( _hostTarget, false, Constants.PryingTag );
			}

			_hostAttemptToken = Guid.Empty;
			_hostCallerId = Guid.Empty;
			_hostSteamId = 0;
			_hostTarget = null;
			_hostTargetName = "";
			_hostDoorName = "";
			_hostAttemptStartedAt = 0f;
			_hostSeed = 0;
			_hostPinCount = 0;
			_hostSweetSpotWidth = 0f;
			_hostVerifyOps = 0;
			_hostVerifyText = "absent";
		}

		private void CancelClientAttempt( bool tellHost )
		{
			var attemptToken = _clientAttemptToken;
			var wasActive = _clientAttemptActive;
			ClearClientState();

			if ( tellHost && wasActive && attemptToken != Guid.Empty )
			{
				CancelAttemptHost( attemptToken );
			}
		}

		private void ClearClientState()
		{
			_clientAwaitingHost = false;
			_clientAttemptActive = false;
			_clientAttemptToken = Guid.Empty;
			_clientTarget = null;
			LockpickMinigameUiHost.CloseOpen( this );
		}

		private void StopReplacementLifecycle()
		{
			if ( Networking.IsHost && _hostAttemptToken != Guid.Empty )
			{
				ResolveHostAttempt( false, false );
			}

			if ( _clientAwaitingHost || _clientAttemptActive )
			{
				var canNotifyHost = !Networking.IsHost && this.IsValid() && GameObject.IsValid();
				CancelClientAttempt( canNotifyHost );
			}
		}

		private bool IsMatchingHostAttempt( Guid attemptToken, Guid callerId )
		{
			return attemptToken != Guid.Empty &&
			       attemptToken == _hostAttemptToken &&
			       callerId == _hostCallerId;
		}

		private void ReportTranscriptForgery( Player player, string failReason, int verifyOps, string doorName )
		{
			Dxura.RP.Game.Sentinel.Sentinel.ReportViolation(
				player,
				"Lockpick Transcript Forgery",
				$"Replay failed ({failReason}) ops={verifyOps} door={doorName}" );
		}

		private static bool TryGetLockedDoor( GameObject target, out Door? door, out string reason )
		{
			door = null;
			reason = "";
			if ( !target.IsValid() )
			{
				return false;
			}

			door = target.GetComponentInParent<Door>();
			if ( !door.IsValid() )
			{
				return false;
			}

			if ( string.Equals( door.OwnerGroupIdentifier, "public", StringComparison.OrdinalIgnoreCase ) )
			{
				reason = "#notify.prybar.door_public";
				return false;
			}

			if ( string.IsNullOrWhiteSpace( door.OwnerJobIdentifier ) &&
			     string.IsNullOrWhiteSpace( door.OwnerGroupIdentifier ) &&
			     door.Owner == 0 )
			{
				reason = "#notify.prybar.door_unowned";
				return false;
			}

			if ( !door.Locked )
			{
				reason = "#notify.prybar.notlocked";
				return false;
			}

			return true;
		}

		private static float CalculateDistance( Player player, GameObject target )
		{
			var renderer = target.GetComponent<ModelRenderer>();
			if ( renderer.IsValid() && renderer.Model.IsValid() )
			{
				return player.WorldPosition.Distance( renderer.Bounds.ClosestPoint( player.WorldPosition ) );
			}

			return player.WorldPosition.Distance( target.WorldPosition );
		}

		private static int SanitizedPinCount()
		{
			return Math.Clamp( Config.Current.Game.LockpickPinCount, 1, 8 );
		}

		private static float SanitizedSweetSpotWidth()
		{
			return Math.Clamp( Config.Current.Game.LockpickSweetSpotWidth, 0.02f, 0.5f );
		}

		private static float SanitizedPinFallSpeed()
		{
			return Math.Clamp( Config.Current.Game.LockpickPinFallSpeed, 0.05f, 3f );
		}

		private static void AuditAttempt( long steamId, string targetName, string doorName, bool result, float elapsedSeconds, int verifyOps, string verifyText )
		{
			ServerApiClient.Audit(
				"Lockpick",
				$"actor={steamId} target={targetName} door={doorName} result={result} duration={elapsedSeconds:F3} ops={verifyOps} verify={verifyText}",
				steamId );
		}
	}

	internal static class LockpickMinigameUiHost
	{
		private const string PanelObjectName = "LpLockpickMinigame";
		private static LockpickMinigame? _openPanel;
		private static LockpickMinigameEquipment? _ownerEquipment;

		public static bool IsOpen => _openPanel.IsValid();

		public static bool Open(
			LockpickMinigameEquipment equipment,
			int seed,
			int pinCount,
			float sweetSpotWidth,
			float pinFallSpeed )
		{
			if ( IsOpen )
			{
				return false;
			}

			_openPanel = null;
			_ownerEquipment = null;

			var scene = Game.ActiveScene;
			if ( scene is null )
			{
				return false;
			}

			var panelObject = scene.CreateObject();
			panelObject.Name = PanelObjectName;
			panelObject.AddComponent<ScreenPanel>();
			_openPanel = panelObject.AddComponent<LockpickMinigame>();
			_ownerEquipment = equipment;
			_openPanel.Bind( equipment, seed, pinCount, sweetSpotWidth, pinFallSpeed );
			return true;
		}

		public static void CloseOpen( LockpickMinigameEquipment equipment )
		{
			if ( !ReferenceEquals( _ownerEquipment, equipment ) )
			{
				return;
			}

			var panel = _openPanel;
			_openPanel = null;
			_ownerEquipment = null;
			if ( !panel.IsValid() )
			{
				return;
			}

			DestroyPanel( panel );
		}

		public static void CloseOrphan( LockpickMinigame panel )
		{
			if ( ReferenceEquals( _openPanel, panel ) )
			{
				_openPanel = null;
				_ownerEquipment = null;
			}

			if ( panel.IsValid() )
			{
				DestroyPanel( panel );
			}
		}

		private static void DestroyPanel( LockpickMinigame panel )
		{
			panel.PrepareForClose();
			if ( panel.GameObject.IsValid() && panel.GameObject.Name == PanelObjectName )
			{
				panel.GameObject.Destroy();
			}
			else
			{
				panel.Destroy();
			}
		}
	}
}
