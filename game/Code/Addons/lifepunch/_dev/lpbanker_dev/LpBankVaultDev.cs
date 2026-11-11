// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane (Code/_dev - NOT shipped). BANK-VAULT-1 U3 (cc-opus-4): editor-only commands for the
// driver, shaped on LpBankerHubDev.cs and LpKeypadDev.cs: spawn a vault with a linked keypad beside the pawn, and
// open / close or lock / unlock the nearest vault. The open and lock commands run the vault's host core directly
// (the same code the RPCs run, minus the transport and the reach gate), as a Banker.
// Play-scene objects only; game.scene is never written. No money anywhere in this file (Law B).
//
// F49, said plainly: the VAULT is a plain Component and is never written to the host's world save. The KEYPAD this
// command spawns beside it is an LpKeypadEntity, a BaseEntity, and a BaseEntity IS an ISnapshot: a host auto-save
// that fires during Play can write that keypad into the save. Spawn right after a save and Stop before the next.

#if !LIFEPUNCH_LOCAL

using System.Linq;
using Dxura.RP.Game;
using Sandbox;
using LifePunch.DXRP.Addons.Banker;
using LifePunch.DXRP.Addons.Keypad;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>DEV / EDITOR-TEST ONLY.</summary>
public static class LpBankVaultDev
{
	// Reason: one vault and one linked keypad beside the pawn, for the open / lock / keypad / alarm proof.
	// Who: the editor host only. The keypad it spawns is a persistent-class BaseEntity (F49, see the file header).
	[ConCmd( "lp_bank_vault_spawn" )]
	public static void Spawn()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_vault_spawn: editor host only." );
			return;
		}

		if ( !LpBankerHubDev.TryInFrontOfLocal( out var position, out var facing ) )
		{
			Log.Warning( "lp_bank_vault_spawn: no local player - play from game.scene first." );
			return;
		}

		var model = Model.Load( LpBankVaultIdent.BodyModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"lp_bank_vault_spawn: vault model missing ({LpBankVaultIdent.BodyModelPath}) - the bank source files are not in this tree." );
			return;
		}

		var vaultGo = new GameObject( true, "lp_bank_vault" );
		vaultGo.Tags.Add( LpBankerAssetDevSpawn.SessionTag );
		vaultGo.WorldPosition = position;
		vaultGo.WorldRotation = facing;
		vaultGo.Components.Create<ModelRenderer>().Model = model;
		var vault = vaultGo.Components.Create<LpBankVaultEntity>();
		var grounded = LifePunchGroundContact.AlignMeshBottom( vaultGo );
		vaultGo.NetworkSpawn();

		// The link target must exist before the keypad looks for a door (OnStart may not have run yet).
		var link = vault.EnsureKeypadLinkHost();

		// The keypad stands beside the vault, inside its own link radius, and is NOT a child of it.
		var keypadGo = new GameObject( true, "lp_bank_vault_keypad" );
		keypadGo.Tags.Add( LpBankerAssetDevSpawn.SessionTag );
		keypadGo.WorldPosition = vaultGo.WorldPosition + facing.Right * 48f;
		keypadGo.WorldRotation = facing;
		var keypad = keypadGo.Components.Create<LpKeypadEntity>();
		keypad.BindOwnerFromLocalViewer();
		keypadGo.NetworkSpawn();

		// Link as the keypad's owner would (the same call the keypad panel's Link door button makes on the host).
		var linkResult = keypad.LinkNearestCore( keypad.HubOwner );
		var bounds = model.Bounds;

		Log.Info( $"[lpbanker] {LpBankVaultIdent.SpawnMark} vault={vaultGo.Id} keypad={keypadGo.Id} link={linkResult} linkTarget={( link.IsValid() ? link.GameObject.Id.ToString() : "none" )} state={vault.State} grounded={grounded} bounds={bounds.Size.x:F2}x{bounds.Size.y:F2}x{bounds.Size.z:F2} vaultPersistent=no keypadPersistent=yes(BaseEntity,F49) money=none" );
	}

	// Reason: open the nearest vault, or close it when it is open, without the press, to drive the proof.
	// Who: the editor host only. Runs as a Banker.
	[ConCmd( "lp_bank_vault_open" )]
	public static void Open()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_vault_open: editor host only." );
			return;
		}

		var vault = LpBankVaultTabState.NearestVault();
		if ( !vault.IsValid() )
		{
			Log.Warning( "lp_bank_vault_open: no vault in the scene - run lp_bank_vault_spawn first." );
			return;
		}

		var decision = vault.RunCommandCore( LpBankVaultRules.PressCommand( vault.State ), true, false, "lp_bank_vault_open" );
		Log.Info( $"[lpbanker] {LpBankVaultIdent.StateMark} dev=lp_bank_vault_open accepted={decision.Accepted} state={decision.State} message=\"{decision.Reason}\"" );
	}

	// Reason: lock the nearest vault down, or unlock it when it is locked, to drive the proof.
	// Who: the editor host only. Runs as a Banker.
	[ConCmd( "lp_bank_vault_lock" )]
	public static void Lock()
	{
		if ( !Application.IsEditor || !Networking.IsHost )
		{
			Log.Warning( "lp_bank_vault_lock: editor host only." );
			return;
		}

		var vault = LpBankVaultTabState.NearestVault();
		if ( !vault.IsValid() )
		{
			Log.Warning( "lp_bank_vault_lock: no vault in the scene - run lp_bank_vault_spawn first." );
			return;
		}

		var command = vault.State == LpBankVaultState.Locked ? LpBankVaultCommand.Unlock : LpBankVaultCommand.Lock;
		var decision = vault.RunCommandCore( command, true, false, "lp_bank_vault_lock" );
		Log.Info( $"[lpbanker] {LpBankVaultIdent.StateMark} dev=lp_bank_vault_lock command={command} accepted={decision.Accepted} state={decision.State} message=\"{decision.Reason}\"" );
	}
}

#endif
