// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U4b (cc-opus-4): the bank alarm's pure rules. No engine type is used in this file, so it compiles and
// runs outside the editor (the offline gate and the rules harness), the LpKeypadRules shape.
//
// NO MONEY (Law B). Every policy below is PROVISIONAL and listed under WAITS in the BANK-1 return (alarm rule,
// alarm audience).

namespace LifePunch.DXRP.Addons.Banker;

/// <summary>Alarm state. The int values are the synced wire values; do not reorder.</summary>
public enum LpBankAlarmState
{
	Disarmed = 0,
	Armed = 1,
	Tripped = 2,
}

public enum LpBankAlarmCommand
{
	Arm = 0,
	Disarm = 1,
	Reset = 2,

	/// <summary>A watched vault's door opened. Host-internal: never accepted from a client request.</summary>
	VaultDoorOpened = 3,

	/// <summary>A watched vault's keypad locked a caller out after wrong codes. Host-internal.</summary>
	KeypadLockout = 4,
}

/// <summary>What one command does: whether it was accepted, the state after it, the line shown, and whether the alarm just tripped.</summary>
public readonly record struct LpBankAlarmDecision( bool Accepted, LpBankAlarmState State, string Reason, bool TrippedNow );

public static class LpBankAlarmRules
{
	/// <summary>Reach, in world units from the alarm, within which it watches vaults. PROVISIONAL.</summary>
	public const float DefaultWatchRadius = 600f;

	/// <summary>How long the trip notice stays on the HUD of a guard or an officer.</summary>
	public const float NoticeSeconds = 8f;

	// ---- Reason copy (ASCII only) ------------------------------------------------------------------------------------

	public const string ArmedNow = "Bank alarm armed.";
	public const string DisarmedNow = "Bank alarm disarmed.";
	public const string ResetArmed = "Bank alarm reset. It is armed again.";
	public const string ResetDisarmed = "Bank alarm reset. The vault is still open, so it is left disarmed.";
	public const string AlreadyArmed = "The bank alarm is already armed.";
	public const string AlreadyDisarmed = "The bank alarm is already disarmed.";
	public const string ResetFirst = "The bank alarm is sounding. Reset it first.";
	public const string NotTripped = "The bank alarm is not sounding.";
	public const string StaffOnly = "Only the Banker or a Bank Guard can work the bank alarm.";
	public const string Unknown = "That is not an alarm command.";

	public const string NoticeVaultOpened = "BANK ALARM: the vault was opened while the alarm was armed.";
	public const string NoticeKeypadLockout = "BANK ALARM: too many wrong codes on the vault keypad.";

	/// <summary>A wire value back to a state; anything out of range reads as Disarmed.</summary>
	public static LpBankAlarmState FromRaw( int raw )
		=> raw == (int)LpBankAlarmState.Armed ? LpBankAlarmState.Armed
			: raw == (int)LpBankAlarmState.Tripped ? LpBankAlarmState.Tripped
			: LpBankAlarmState.Disarmed;

	public static string Label( LpBankAlarmState state ) => state switch
	{
		LpBankAlarmState.Armed => "Armed",
		LpBankAlarmState.Tripped => "SOUNDING",
		_ => "Disarmed",
	};

	/// <summary>True for a command a client may ask for. The two trip signals are host-internal.</summary>
	public static bool IsClientCommand( int raw )
		=> raw >= (int)LpBankAlarmCommand.Arm && raw <= (int)LpBankAlarmCommand.Reset;

	/// <summary>What a press on the alarm asks for: arm it, disarm it, or reset it while it sounds.</summary>
	public static LpBankAlarmCommand PressCommand( LpBankAlarmState state ) => state switch
	{
		LpBankAlarmState.Armed => LpBankAlarmCommand.Disarm,
		LpBankAlarmState.Tripped => LpBankAlarmCommand.Reset,
		_ => LpBankAlarmCommand.Arm,
	};

	/// <summary>The notice a trip sends to guards and police.</summary>
	public static string TripNotice( LpBankAlarmCommand cause )
		=> cause == LpBankAlarmCommand.KeypadLockout ? NoticeKeypadLockout : NoticeVaultOpened;

	/// <summary>
	/// The whole alarm state machine. Arm, Disarm and Reset are the Banker's or a Bank Guard's. The two trip signals
	/// carry no role and trip an Armed alarm only: a door opened by anyone, the Banker included, trips it, so a Banker
	/// disarms before opening. A reset re-arms unless a watched vault is still open.
	/// </summary>
	public static LpBankAlarmDecision Decide( LpBankAlarmState state, LpBankAlarmCommand command, bool isStaff, bool vaultIsOpen )
	{
		switch ( command )
		{
			case LpBankAlarmCommand.VaultDoorOpened:
			case LpBankAlarmCommand.KeypadLockout:
				if ( state == LpBankAlarmState.Armed )
					return new LpBankAlarmDecision( true, LpBankAlarmState.Tripped, TripNotice( command ), true );

				return Refuse( state, string.Empty );

			case LpBankAlarmCommand.Arm:
				if ( !isStaff )
					return Refuse( state, StaffOnly );

				if ( state == LpBankAlarmState.Disarmed )
					return new LpBankAlarmDecision( true, LpBankAlarmState.Armed, ArmedNow, false );

				return Refuse( state, state == LpBankAlarmState.Tripped ? ResetFirst : AlreadyArmed );

			case LpBankAlarmCommand.Disarm:
				if ( !isStaff )
					return Refuse( state, StaffOnly );

				if ( state == LpBankAlarmState.Armed )
					return new LpBankAlarmDecision( true, LpBankAlarmState.Disarmed, DisarmedNow, false );

				return Refuse( state, state == LpBankAlarmState.Tripped ? ResetFirst : AlreadyDisarmed );

			case LpBankAlarmCommand.Reset:
				if ( !isStaff )
					return Refuse( state, StaffOnly );

				if ( state != LpBankAlarmState.Tripped )
					return Refuse( state, NotTripped );

				return vaultIsOpen
					? new LpBankAlarmDecision( true, LpBankAlarmState.Disarmed, ResetDisarmed, false )
					: new LpBankAlarmDecision( true, LpBankAlarmState.Armed, ResetArmed, false );

			default:
				return Refuse( state, Unknown );
		}
	}

	private static LpBankAlarmDecision Refuse( LpBankAlarmState state, string reason )
		=> new LpBankAlarmDecision( false, state, reason, false );
}
