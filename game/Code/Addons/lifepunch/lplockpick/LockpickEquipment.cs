// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "Lockpick" (addon ident: lplockpick) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using Dxura.RP.Game;
using Dxura.RP.Game.UI;
using Sandbox;
using Sandbox.Services;

namespace LifePunch.DXRP.Addons.Lockpick;

/// <summary>
/// Timed hold-to-pick door tool. Structural mirror of
/// <c>Dxura.RP.Game.Equipments.PryBarEquipment</c> with verbs changed.
/// Anti-cheat timing (0.5s) and distance (+50u) copied verbatim from
/// <c>PryBarEquipment.cs:266</c> and <c>:275</c>.
/// </summary>
public class LockpickEquipment : InputWeaponComponent, IEquipmentEvents, IInputHints
{
	[Property] [Group( "Effects" )] private SoundEvent? PickingSound { get; set; }

	[Sync( SyncFlags.FromHost )]
	private bool IsPicking { get; set; }

	[Sync( SyncFlags.FromHost )]
	private TimeSince PickStartTime { get; set; }

	[Sync( SyncFlags.FromHost )]
	private GameObject? PickTarget { get; set; }

	private string _targetName = "";
	private float _localPickStartTime;

	IEnumerable<(string Action, string Label)> IInputHints.GetInputHints()
	{
		yield return ("attack1", "#input.pry_bar.pry");
	}

	protected override void OnInputDown()
	{
		if ( !Player.Local.IsValid() )
		{
			return;
		}

		var trace = GetTrace( Config.Current.Game.ReachDistance );

		if ( trace is not { Hit: true } || !trace.Value.GameObject.IsValid() )
		{
			return;
		}

		var target = trace.Value.GameObject.Root;
		var (isValid, targetName, reason) = ValidateTarget( target );

		if ( targetName == "" )
		{
			return;
		}

		if ( Cooldown.Current?.CheckAndStartCooldown( "lockpick", Config.Current.Game.LockpickCooldown, true ) == true )
		{
			return;
		}

		if ( !isValid )
		{
			Notify.Error( reason );
			return;
		}

		_localPickStartTime = Time.Now;
		StartPickHost( target );

		_targetName = targetName;

		if ( EquipmentOverlay.Instance.IsValid() )
		{
			EquipmentOverlay.Instance.Status = string.Format( Language.GetPhrase( "equipment.prybar.status.prying" ), targetName );
			EquipmentOverlay.Instance.Progress = 0;
			EquipmentOverlay.Instance.IsActive = true;
		}
	}

	protected override void OnInputUp()
	{
		if ( IsPicking )
		{
			CancelPicking();
		}
		else
		{
			if ( EquipmentOverlay.Instance.IsValid() )
			{
				EquipmentOverlay.Instance.IsActive = false;
			}
		}
	}

	protected override void OnInputFixedUpdate()
	{
		if ( !IsPicking || !PickTarget.IsValid() )
		{
			return;
		}

		var player = Player.Local;
		if ( !player.IsValid() || player.IsDead )
		{
			CancelPicking();
			return;
		}

		var distanceToTarget = CalculateDistance( player, PickTarget );

		if ( distanceToTarget > Config.Current.Game.LockpickMaxDistance )
		{
			if ( !Cooldown.Current.IsOnCooldown( "lockpick:toofar" ) )
			{
				Notify.Warn( "#notify.prybar.toofar" );
				Cooldown.Current.StartCooldown( "lockpick:toofar", 1.5f );
			}

			CancelPicking();
			return;
		}

		if ( !Cooldown.Current.CheckAndStartCooldown( "lockpick:effects", Config.Current.Game.LockpickEffectsCooldown ) )
		{
			DoPickEffectsHost();
		}

		var duration = Config.Current.Game.LockpickDuration;
		var elapsed = Time.Now - _localPickStartTime;
		var progress = Math.Clamp( elapsed / duration, 0, 1 );

		if ( EquipmentOverlay.Instance.IsValid() )
		{
			EquipmentOverlay.Instance.Status = string.Format( Language.GetPhrase( "equipment.prybar.status.prying" ), _targetName );
			EquipmentOverlay.Instance.Progress = progress;
			EquipmentOverlay.Instance.IsActive = true;
		}

		if ( PickStartTime >= duration )
		{
			CompletePickHost();
			CompletePicking();
		}
	}

	private float CalculateDistance( Player player, GameObject target )
	{
		var renderer = target.GetComponent<ModelRenderer>();

		if ( renderer.IsValid() && renderer.Model.IsValid() )
		{
			var bounds = renderer.Bounds;
			var closestPoint = bounds.ClosestPoint( player.WorldPosition );
			return player.WorldPosition.Distance( closestPoint );
		}

		return player.WorldPosition.Distance( target.WorldPosition );
	}

	private (bool isValid, string targetName, string reason) ValidateTarget( GameObject target )
	{
		if ( !target.IsValid() )
		{
			return (false, "", "");
		}

		var door = target.GetComponentInParent<Door>();
		if ( door.IsValid() )
		{
			if ( string.Equals( door.OwnerGroupIdentifier, "public", StringComparison.OrdinalIgnoreCase ) )
			{
				return (false, Language.GetPhrase( "roleplay.door.name" ), "#notify.prybar.door_public");
			}

			if ( string.IsNullOrWhiteSpace( door.OwnerJobIdentifier ) &&
			     string.IsNullOrWhiteSpace( door.OwnerGroupIdentifier ) &&
			     door.Owner == 0 )
			{
				return (false, Language.GetPhrase( "roleplay.door.name" ), "#notify.prybar.door_unowned");
			}

			if ( !door.Locked )
			{
				return (false, Language.GetPhrase( "roleplay.door.name" ), "#notify.prybar.notlocked");
			}

			return (true, Language.GetPhrase( "roleplay.door.name" ), "");
		}

		var prop = target.GetComponentInParent<Dxura.RP.Game.Prop>();
		if ( prop.IsValid() && prop.FadingDoor )
		{
			return (true, Language.GetPhrase( "tool.fadingdoor.name" ), "");
		}

		return (false, "", "");
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void StartPickHost( GameObject target )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:lockpick", Config.Current.Game.LockpickCooldown ) )
		{
			return;
		}

		if ( !target.IsValid() )
		{
			return;
		}

		var door = target.GetComponentInParent<Door>();
		var prop = target.GetComponentInParent<Dxura.RP.Game.Prop>();

		if ( !door.IsValid() && !(prop.IsValid() && prop.FadingDoor) )
		{
			return;
		}

		IsPicking = true;
		PickStartTime = 0;
		PickTarget = target;

		GameManager.Instance.BroadcastTagHost( target, true, Constants.PryingTag );
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void CompletePickHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:action", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );

		if ( !IsPicking )
		{
			return;
		}

		if ( !player.IsValid() || player.IsDead )
		{
			ClearPickState();
			return;
		}

		var target = PickTarget;

		if ( !target.IsValid() )
		{
			ClearPickState();
			return;
		}

		var targetName = Language.GetPhrase( "roleplay.door.name" );
		var doorName = target.Name;

		// Anti-cheat: Validate timing
		var expectedDuration = Config.Current.Game.LockpickDuration;
		if ( PickStartTime < expectedDuration - 0.5f ) // Allow 0.5s tolerance for network latency
		{
			Log.Warning( $"Player {player.SteamId} attempted to complete lockpick too early ({PickStartTime}s < {expectedDuration}s)" );
			AuditPick( player, targetName, doorName, false );
			ClearPickState();
			return;
		}

		// Anti-cheat: Validate distance
		var distance = CalculateDistance( player, target );
		if ( distance > Config.Current.Game.LockpickMaxDistance + 50f ) // Allow 50 units tolerance
		{
			Log.Warning( $"Player {player.SteamId} attempted to complete lockpick from too far away ({distance} > {Config.Current.Game.LockpickMaxDistance})" );
			AuditPick( player, targetName, doorName, false );
			ClearPickState();
			return;
		}

		if ( !ResolveLockpickOutcome( player, target ) )
		{
			AuditPick( player, targetName, doorName, false );
			ClearPickState();
			return;
		}

		var door = target.GetComponentInParent<Door>();
		if ( door.IsValid() )
		{
			door.LpSetLockedHost( false );
			LockpickRelockTimer.Schedule( door, Config.Current.Game.LockpickDuration );

			player.IncrementStat( "lockpick", 1 );
			player.IncrementStat( "lockpick-door", 1 );
			player.Success( "#notify.prybar.success_door" );
			AuditPick( player, targetName, doorName, true );
			ClearPickState();
			return;
		}

		var prop = target.GetComponentInParent<Dxura.RP.Game.Prop>();
		if ( prop.IsValid() && prop.FadingDoor )
		{
			if ( !BreachSystem.Instance.IsValid() )
			{
				AuditPick( player, Language.GetPhrase( "tool.fadingdoor.name" ), doorName, false );
				ClearPickState();
				return;
			}

			BreachSystem.Instance.Breach( prop, player.WorldPosition );
			player.IncrementStat( "lockpick", 1 );
			player.IncrementStat( "lockpick-fade", 1 );
			player.Success( "#notify.prybar.success_fading" );
			AuditPick( player, Language.GetPhrase( "tool.fadingdoor.name" ), doorName, true );
		}

		ClearPickState();
	}

	/// <summary>
	/// D1 minigame seam. v1 is a deterministic timed hold.
	/// A future override replaces this body and nothing else.
	/// </summary>
	protected virtual bool ResolveLockpickOutcome( Player player, GameObject target )
	{
		return true;
	}

	private static void AuditPick( Player player, string targetName, string doorName, bool result )
	{
		ServerApiClient.Audit(
			"Lockpick",
			$"actor={player.SteamId} target={targetName} door={doorName} result={result} duration={Config.Current.Game.LockpickDuration}",
			player.SteamId );
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void DoPickEffectsHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:lockpick:effects", Config.Current.Game.LockpickEffectsCooldown ) )
		{
			return;
		}

		BroadcastPickEffects();
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void BroadcastPickEffects()
	{
		if ( !PickTarget.IsValid() )
		{
			return;
		}

		PickingSound?.Play( PickTarget.WorldPosition );
		Equipment?.Owner?.Renderer?.Set( "b_attack", true );
		Equipment?.ViewModel?.ModelRenderer?.Set( "b_attack", true );
	}

	private void CancelPicking()
	{
		if ( IsProxy && !Networking.IsHost )
		{
			return;
		}

		if ( Networking.IsHost )
		{
			ClearPickState();
		}
		else
		{
			CancelPickHost();
		}

		CompletePicking();
	}

	[Rpc.Host( NetFlags.OwnerOnly | NetFlags.Reliable )]
	private void CancelPickHost()
	{
		ClearPickState();
	}

	private void ClearPickState()
	{
		if ( PickTarget.IsValid() )
		{
			GameManager.Instance.BroadcastTagHost( PickTarget, false, Constants.PryingTag );
		}

		IsPicking = false;
		PickStartTime = 0;
		PickTarget = null;
	}

	private void CompletePicking()
	{
		if ( EquipmentOverlay.Instance.IsValid() )
		{
			EquipmentOverlay.Instance.Progress = 0;
			EquipmentOverlay.Instance.IsActive = false;
		}

		_targetName = "";
		_localPickStartTime = 0f;
	}

	protected override void OnDisabled()
	{
		CancelPicking();
	}

	public new void OnEquipmentHolstered( Equipment equipment )
	{
		CancelPicking();
	}

	public void OnEquipmentDestroyed( Equipment equipment )
	{
		CancelPicking();
	}
}

/// <summary>
/// Host-only re-lock scheduler (P-4). Attached to the door so holstering
/// or destroying the lockpick cannot cancel the re-lock.
/// </summary>
public sealed class LockpickRelockTimer : Component
{
	private TimeUntil _relockAt;

	public static void Schedule( Door door, float seconds )
	{
		if ( !Networking.IsHost || !door.IsValid() )
		{
			return;
		}

		var timer = door.GameObject.GetOrAddComponent<LockpickRelockTimer>();
		timer._relockAt = seconds;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
		{
			return;
		}

		if ( !_relockAt )
		{
			return;
		}

		var door = GameObject.GetComponent<Door>();
		if ( door.IsValid() )
		{
			door.LpSetLockedHost( true );
		}

		Destroy();
	}
}
