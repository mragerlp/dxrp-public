// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U4b (cc-opus-4): the alarm's half of the Banker hub's Vault tab. It fills the partial methods that
// LpBankVaultTabState.cs declares: one live row (the alarm state) and three buttons that only ask the host.
// NO MONEY (Law B).

using System;
using System.Collections.Generic;
using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

public static partial class LpBankVaultTabState
{
	public const string ActionAlarmArm = "alarm.arm";
	public const string ActionAlarmDisarm = "alarm.disarm";
	public const string ActionAlarmReset = "alarm.reset";

	/// <summary>The alarm nearest the local player; the first one when there is no local player; null when none exists.</summary>
	public static LpBankAlarmEntity NearestAlarm()
	{
		var alarms = Sandbox.Game.ActiveScene?.GetAllComponents<LpBankAlarmEntity>().Where( a => a.IsValid() ).ToList();
		if ( alarms is null || alarms.Count == 0 )
			return null;

		var player = Player.Local;
		if ( !player.IsValid() )
			return alarms[0];

		return alarms.OrderBy( a => ( a.GameObject.WorldPosition - player.WorldPosition ).Length ).First();
	}

	static partial void AlarmVersion( ref int version )
	{
		var alarm = NearestAlarm();
		version = HashCode.Combine( version, alarm.IsValid() ? alarm.StateRaw + 1 : 0 );
	}

	static partial void AppendAlarmRows( List<LpBankerFigure> rows )
	{
		var alarm = NearestAlarm();
		var value = alarm.IsValid() ? LpBankAlarmRules.Label( alarm.State ) : "no alarm in the world";
		rows.Add( new LpBankerFigure( "Bank alarm", value, LpBankerRulings.AlarmRule, "LpBankAlarmEntity.State" ) );
	}

	static partial void AppendAlarmActions( List<LpBankVaultAction> actions )
	{
		if ( !NearestAlarm().IsValid() )
			return;

		actions.Add( new LpBankVaultAction( ActionAlarmArm, "Arm alarm", "Banker or Bank Guard. An armed alarm trips when the vault opens or its keypad locks someone out." ) );
		actions.Add( new LpBankVaultAction( ActionAlarmDisarm, "Disarm alarm", "Banker or Bank Guard. Disarm before opening the vault." ) );
		actions.Add( new LpBankVaultAction( ActionAlarmReset, "Reset alarm", "Banker or Bank Guard. Silences a sounding alarm." ) );
	}

	static partial void RunAlarmAction( string id, ref bool handled )
	{
		LpBankAlarmCommand command;
		switch ( id )
		{
			case ActionAlarmArm: command = LpBankAlarmCommand.Arm; break;
			case ActionAlarmDisarm: command = LpBankAlarmCommand.Disarm; break;
			case ActionAlarmReset: command = LpBankAlarmCommand.Reset; break;
			default: return;
		}

		handled = true;
		var alarm = NearestAlarm();
		if ( alarm.IsValid() )
			alarm.RequestCommand( command );
	}
}
