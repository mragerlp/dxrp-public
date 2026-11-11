// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U3 (cc-opus-4): the bank vault's pure rules. No engine type is used in this file, so it compiles and
// runs outside the editor (the offline gate and the rules harness), the LpKeypadRules shape.
// It holds the door state machine, the reason copy, the identity strings and the FIXTURE contents.
//
// NO MONEY (Law B): the vault holds no balance and nothing here moves value. The contents list is fixture text.
// Every policy below is PROVISIONAL and listed under WAITS in the BANK-1 return (vault open rule).

using System.Collections.Generic;

namespace LifePunch.DXRP.Addons.Banker;

/// <summary>Door state. The int values are the synced wire values; do not reorder.</summary>
public enum LpBankVaultState
{
	/// <summary>Shut. A Banker's press or the linked keypad's code opens it.</summary>
	Closed = 0,

	/// <summary>Open.</summary>
	Open = 1,

	/// <summary>Shut and sealed: neither a press nor the keypad code opens it until a Banker unlocks it.</summary>
	Locked = 2,
}

public enum LpBankVaultCommand
{
	Open = 0,
	Close = 1,
	Lock = 2,
	Unlock = 3,

	/// <summary>The linked keypad accepted a code. Host-internal: never accepted from a client request.</summary>
	KeypadAccepted = 4,
}

/// <summary>What one command does: whether it was accepted, the state after it, the line shown, and whether the door just opened.</summary>
public readonly record struct LpBankVaultDecision( bool Accepted, LpBankVaultState State, string Reason, bool DoorOpened );

public static class LpBankVaultIdent
{
	/// <summary>The key the hub kit (ILpHub.HubKey) knows the vault by.</summary>
	public const string HubKey = "bankvault";

	public const string ProductName = "Bank Vault";

	/// <summary>The landed vault body and door (BANK-ASSETS-2; import_scale 0.8567, R-M6-3 owed, so PROVISIONAL).</summary>
	public const string BodyModelPath = "addons/lifepunch/lpbanker/bankvault/assets/models/safe_vault.vmdl";
	public const string DoorModelPath = "addons/lifepunch/lpbanker/bankvault/assets/models/safe_vault_door.vmdl";

	/// <summary>PLACEHOLDER siren: a core DXRP sound event. The alarm's own sound is owed.</summary>
	public const string AlarmSoundPath = "sounds/lockdown/siren.sound";

	public const string DoorObjectName = "lp_bank_vault_door";
	public const string KeypadLinkObjectName = "lp_bank_vault_keypad_link";

	// Positive code-string IDs. Each exists nowhere else in the tree; a log line carrying one proves the new bytes ran.
	public const string StateMark = "LP_BANK_VAULT_STATE_20261111";
	public const string SpawnMark = "LP_BANK_VAULT_SPAWN_20261111";
	public const string DoorMark = "LP_BANK_VAULT_DOOR_20261111";
	public const string TabMark = "LP_BANK_VAULT_TAB_20261111";
	public const string AlarmStateMark = "LP_BANK_ALARM_STATE_20261111";
	public const string AlarmSpawnMark = "LP_BANK_ALARM_SPAWN_20261111";
	public const string AlarmNoticeMark = "LP_BANK_ALARM_NOTICE_20261111";
}

public static class LpBankVaultRules
{
	// ---- Reason copy (ASCII only; the entity shows the line to the asking player) ---------------------------------

	public const string Opened = "Vault opened.";
	public const string OpenedByCode = "Code accepted. Vault opened.";
	public const string Closed = "Vault closed.";
	public const string LockedNow = "Vault locked down.";
	public const string Unlocked = "Vault unlocked. It is closed.";
	public const string AlreadyOpen = "The vault is already open.";
	public const string AlreadyClosed = "The vault is already closed.";
	public const string AlreadyLocked = "The vault is already locked down.";
	public const string NotLocked = "The vault is not locked down.";
	public const string LockedDown = "Locked down. A Banker unlocks it from the Banker hub (Vault tab).";
	public const string LockedDownCode = "Locked down. The keypad code does not open it until a Banker unlocks it.";
	public const string BankerOnly = "Only the Banker can do that. The keypad code opens a closed vault.";
	public const string StaffOnly = "Only the Banker or a Bank Guard can do that.";
	public const string Unknown = "That is not a vault command.";

	/// <summary>A wire value back to a state; anything out of range reads as Closed.</summary>
	public static LpBankVaultState FromRaw( int raw )
		=> raw == (int)LpBankVaultState.Open ? LpBankVaultState.Open
			: raw == (int)LpBankVaultState.Locked ? LpBankVaultState.Locked
			: LpBankVaultState.Closed;

	public static string Label( LpBankVaultState state ) => state switch
	{
		LpBankVaultState.Open => "Open",
		LpBankVaultState.Locked => "Locked down",
		_ => "Closed",
	};

	/// <summary>True for a command a client may ask for. The keypad signal is host-internal.</summary>
	public static bool IsClientCommand( int raw )
		=> raw >= (int)LpBankVaultCommand.Open && raw <= (int)LpBankVaultCommand.Unlock;

	/// <summary>What a press on the vault asks for: open a shut vault, close an open one.</summary>
	public static LpBankVaultCommand PressCommand( LpBankVaultState state )
		=> state == LpBankVaultState.Open ? LpBankVaultCommand.Close : LpBankVaultCommand.Open;

	/// <summary>
	/// The whole door state machine. Open and Unlock are the Banker's; Close and Lock are the Banker's or a Bank
	/// Guard's (a guard can secure the vault, never open it). The keypad signal carries no role: the code is the key,
	/// and it opens a Closed vault only.
	/// </summary>
	public static LpBankVaultDecision Decide( LpBankVaultState state, LpBankVaultCommand command, bool isBanker, bool isGuard )
	{
		switch ( command )
		{
			case LpBankVaultCommand.KeypadAccepted:
				if ( state == LpBankVaultState.Closed )
					return new LpBankVaultDecision( true, LpBankVaultState.Open, OpenedByCode, true );

				return Refuse( state, state == LpBankVaultState.Locked ? LockedDownCode : AlreadyOpen );

			case LpBankVaultCommand.Open:
				if ( !isBanker )
					return Refuse( state, BankerOnly );

				if ( state == LpBankVaultState.Closed )
					return new LpBankVaultDecision( true, LpBankVaultState.Open, Opened, true );

				return Refuse( state, state == LpBankVaultState.Locked ? LockedDown : AlreadyOpen );

			case LpBankVaultCommand.Close:
				if ( !isBanker && !isGuard )
					return Refuse( state, StaffOnly );

				if ( state == LpBankVaultState.Open )
					return new LpBankVaultDecision( true, LpBankVaultState.Closed, Closed, false );

				return Refuse( state, state == LpBankVaultState.Locked ? AlreadyLocked : AlreadyClosed );

			case LpBankVaultCommand.Lock:
				if ( !isBanker && !isGuard )
					return Refuse( state, StaffOnly );

				if ( state == LpBankVaultState.Locked )
					return Refuse( state, AlreadyLocked );

				// Locking an open vault shuts it too.
				return new LpBankVaultDecision( true, LpBankVaultState.Locked, LockedNow, false );

			case LpBankVaultCommand.Unlock:
				if ( !isBanker )
					return Refuse( state, BankerOnly );

				if ( state == LpBankVaultState.Locked )
					return new LpBankVaultDecision( true, LpBankVaultState.Closed, Unlocked, false );

				return Refuse( state, NotLocked );

			default:
				return Refuse( state, Unknown );
		}
	}

	private static LpBankVaultDecision Refuse( LpBankVaultState state, string reason )
		=> new LpBankVaultDecision( false, state, reason, false );

	// ---- FIXTURE contents ------------------------------------------------------------------------------------------

	/// <summary>
	/// FIXTURE: what the Vault tab lists as contents. Invented text that shows the layout. The vault holds NO value:
	/// no balance, no synced amount, no rail. Each row binds only after the Law B rail ruling (BANK-RAILS.md).
	/// </summary>
	public static IReadOnlyList<KeyValuePair<string, string>> FixtureContents { get; } = new[]
	{
		new KeyValuePair<string, string>( "Cash reserve", "$0 (fixture)" ),
		new KeyValuePair<string, string>( "Bullion", "0 bars (fixture)" ),
		new KeyValuePair<string, string>( "Heist take", "nothing to take (fixture)" ),
	};
}
