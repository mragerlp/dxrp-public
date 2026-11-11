// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). BANK-VAULT-1 U4 (cc-opus-4): editor-only commands for the
// driver: spawn a bank alarm beside the pawn, trip it, reset it. The trip and reset commands run the alarm's host
// core directly (the same code the RPCs and the trip signals run, minus the transport and the reach gate).
// Play-scene objects only; game.scene is never written. No money anywhere in this file (Law B).
// F49: the alarm is a plain Component, not a BaseEntity, and is never written to the host's world save.

#if !LIFEPUNCH_LOCAL

using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Banker;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY.</summary>
public static class LpBankAlarmDev
{
	// Reason: one bank alarm beside the pawn, for the arm / trip / reset proof. Who: the editor host only.
	[ConCmd( "lp_bank_alarm_spawn" )]
	public static void Spawn()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_alarm_spawn: editor host only." );
			return;
		}

		if ( !LpBankerHubDev.TryInFrontOfLocal( out var position, out var facing ) )
		{
			Log.Warning( "lp_bank_alarm_spawn: no local player - play from game.scene first." );
			return;
		}

		var alarmGo = new GameObject( true, "lp_bank_alarm" );
		alarmGo.Tags.Add( LpBankerAssetDevSpawn.SessionTag );
		alarmGo.WorldPosition = position;
		alarmGo.WorldRotation = facing;
		var alarm = alarmGo.Components.Create<LpBankAlarmEntity>();
		alarmGo.NetworkSpawn();

		Log.Info( $"[lpbanker] {LpBankVaultIdent.AlarmSpawnMark} alarm={alarmGo.Id} state={alarm.State} watchRadius={alarm.WatchRadius:F0} persistent=no money=none" );
	}

	// Reason: trip the nearest alarm without opening a vault, to drive the siren and notice proof. It arms a disarmed
	// alarm first, then sends the same door-opened signal a watched vault sends. Who: the editor host only.
	[ConCmd( "lp_bank_alarm_trip" )]
	public static void Trip()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_alarm_trip: editor host only." );
			return;
		}

		var alarm = LpBankVaultTabState.NearestAlarm();
		if ( !alarm.IsValid() )
		{
			Log.Warning( "lp_bank_alarm_trip: no alarm in the scene - run lp_bank_alarm_spawn first." );
			return;
		}

		if ( alarm.State == LpBankAlarmState.Disarmed )
			alarm.RunCommandCore( LpBankAlarmCommand.Arm, true, "lp_bank_alarm_trip" );

		var decision = alarm.RunCommandCore( LpBankAlarmCommand.VaultDoorOpened, false, "lp_bank_alarm_trip" );
		Log.Info( $"[lpbanker] {LpBankVaultIdent.AlarmStateMark} dev=lp_bank_alarm_trip accepted={decision.Accepted} state={decision.State} tripped={decision.TrippedNow} message=\"{decision.Reason}\"" );
	}

	// Reason: reset the nearest alarm as bank staff, to drive the proof. Who: the editor host only.
	[ConCmd( "lp_bank_alarm_reset" )]
	public static void Reset()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_alarm_reset: editor host only." );
			return;
		}

		var alarm = LpBankVaultTabState.NearestAlarm();
		if ( !alarm.IsValid() )
		{
			Log.Warning( "lp_bank_alarm_reset: no alarm in the scene - run lp_bank_alarm_spawn first." );
			return;
		}

		var decision = alarm.RunCommandCore( LpBankAlarmCommand.Reset, true, "lp_bank_alarm_reset" );
		Log.Info( $"[lpbanker] {LpBankVaultIdent.AlarmStateMark} dev=lp_bank_alarm_reset accepted={decision.Accepted} state={decision.State} message=\"{decision.Reason}\"" );
	}
}

#endif
