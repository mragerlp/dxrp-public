// PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
//
// BANK-VAULT-1 U3 (cc-opus-4): what the Banker hub's Vault tab shows and can ask for. Read-only rows over the nearest
// vault's synced door state, the FIXTURE contents, and the door commands (open, close, lock down, unlock). A command
// here only asks the host (LpBankVaultEntity.RequestCommand); the host decides and answers on the HUD.
//
// NO MONEY (Law B): nothing here reads or moves value. The contents rows are fixture text.
// The alarm adds its own rows and commands through the partial methods below (LpBankAlarmTabState.cs).

using System;
using System.Collections.Generic;
using System.Linq;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.Banker;

/// <summary>One live button on the Vault tab: the id the tab hands back, the label and the tooltip.</summary>
public readonly record struct LpBankVaultAction( string Id, string Label, string Tooltip );

public static partial class LpBankVaultTabState
{
	public const string ActionOpen = "vault.open";
	public const string ActionClose = "vault.close";
	public const string ActionLock = "vault.lock";
	public const string ActionUnlock = "vault.unlock";

	/// <summary>The vault nearest the local player; the first one when there is no local player; null when none exists.</summary>
	public static LpBankVaultEntity NearestVault()
	{
		var vaults = Sandbox.Game.ActiveScene?.GetAllComponents<LpBankVaultEntity>().Where( v => v.IsValid() ).ToList();
		if ( vaults is null || vaults.Count == 0 )
			return null;

		var player = Player.Local;
		if ( !player.IsValid() )
			return vaults[0];

		return vaults.OrderBy( v => ( v.GameObject.WorldPosition - player.WorldPosition ).Length ).First();
	}

	/// <summary>Changes whenever a row on the tab would read differently; the hub's BuildHash folds it in.</summary>
	public static int Version()
	{
		var vault = NearestVault();
		var version = vault.IsValid() ? HashCode.Combine( 1, vault.StateRaw, vault.KeypadLinked ) : 0;
		AlarmVersion( ref version );
		return version;
	}

	public static IReadOnlyList<LpBankerFigure> Rows()
	{
		var rows = new List<LpBankerFigure>();
		var vault = NearestVault();

		if ( vault.IsValid() )
		{
			rows.Add( new LpBankerFigure( "Vault door", LpBankVaultRules.Label( vault.State ), LpBankerRulings.VaultRule, "LpBankVaultEntity.State" ) );
			rows.Add( new LpBankerFigure( "Keypad", vault.KeypadLinked ? "linked" : "not linked", LpBankerRulings.VaultRule, "LpBankVaultEntity.KeypadLinked" ) );
		}
		else
		{
			rows.Add( new LpBankerFigure( "Vault door", "no vault in the world", LpBankerRulings.VaultRule, "LpBankVaultEntity.State" ) );
		}

		AppendAlarmRows( rows );

		foreach ( var item in LpBankVaultRules.FixtureContents )
			rows.Add( new LpBankerFigure( item.Key, item.Value, LpBankerRulings.Rail, "Vault.Contents" ) );

		return rows;
	}

	/// <summary>The live buttons. Every one is shown while a vault exists; the host refuses with a reason line.</summary>
	public static IReadOnlyList<LpBankVaultAction> Actions()
	{
		var actions = new List<LpBankVaultAction>();
		if ( NearestVault().IsValid() )
		{
			actions.Add( new LpBankVaultAction( ActionOpen, "Open", "Banker only. Opens a closed vault. No money moves." ) );
			actions.Add( new LpBankVaultAction( ActionClose, "Close", "Banker or Bank Guard. Closes an open vault." ) );
			actions.Add( new LpBankVaultAction( ActionLock, "Lock down", "Banker or Bank Guard. Seals the vault: no press and no keypad code opens it." ) );
			actions.Add( new LpBankVaultAction( ActionUnlock, "Unlock", "Banker only. Ends a lock down; the vault stays closed." ) );
		}

		AppendAlarmActions( actions );
		return actions;
	}

	/// <summary>Sends one button's request to the host. Stand in reach of the vault: the host checks it.</summary>
	public static void Run( string id )
	{
		var handled = false;
		RunAlarmAction( id, ref handled );
		if ( handled )
			return;

		var vault = NearestVault();
		if ( !vault.IsValid() )
			return;

		switch ( id )
		{
			case ActionOpen: vault.RequestCommand( LpBankVaultCommand.Open ); break;
			case ActionClose: vault.RequestCommand( LpBankVaultCommand.Close ); break;
			case ActionLock: vault.RequestCommand( LpBankVaultCommand.Lock ); break;
			case ActionUnlock: vault.RequestCommand( LpBankVaultCommand.Unlock ); break;
		}
	}

	static partial void AlarmVersion( ref int version );
	static partial void AppendAlarmRows( List<LpBankerFigure> rows );
	static partial void AppendAlarmActions( List<LpBankVaultAction> actions );
	static partial void RunAlarmAction( string id, ref bool handled );
}

/// <summary>
/// The hub's state once a vault can exist: the fixture for every money figure, with the vault's live rows placed
/// first on the Vault tab. Still reported as fixture: no figure here is a real balance.
/// </summary>
public sealed class LpBankerHubLive : ILpBankerHubState
{
	private readonly LpBankerHubFixture _fixture = new();

	public bool IsFixture => true;

	public int Version => LpBankVaultTabState.Version();

	public IReadOnlyList<LpBankerFigure> Figures( LpBankerHubTab tab )
	{
		if ( tab != LpBankerHubTab.Vault )
			return _fixture.Figures( tab );

		var rows = new List<LpBankerFigure>( LpBankVaultTabState.Rows() );
		rows.AddRange( _fixture.Figures( tab ) );
		return rows;
	}
}
