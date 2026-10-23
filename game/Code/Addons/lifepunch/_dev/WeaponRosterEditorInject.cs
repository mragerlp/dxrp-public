// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// LifePunch editor dev lane — editor test only; not shipped.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.Dev;

/// <summary>
/// Editor-session roster inject for the six non-protected weapons and the lockpick, so the
/// fail-closed <c>lp_give_*</c> helpers can equip them in Play before the Portal rows exist.
/// Same mechanism as <see cref="Aks74uOriginalDevRoster"/>: in-memory addon content through
/// <see cref="GameModeAddonContents.EnsureEditorContent"/> plus one in-memory Equipment row
/// (Limit -1). It never adds a MarketItem row, never touches Portal or the network, and never
/// persists: <see cref="Config.SetGameMode"/> replaces the rows, so re-run the command after a
/// game-mode update. Each weapon sits behind its own flag in <see cref="WeaponRosterInjectTable"/>.
/// </summary>
public static class WeaponRosterEditorInject
{
	private const string ApplyCommand = "lp_weapon_roster_inject";
	private const string StatusCommand = "lp_weapon_roster_inject_status";
	private const string NoGameModeName = "None";
	private const string InjectDescription = "Editor inject, not Portal or market.";

	private enum Outcome
	{
		Added,
		AlreadyInjected,
		AlreadyPresent,
		Refused
	}

	[ConCmd( ApplyCommand )]
	public static void Apply()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"{ApplyCommand}: editor-only." );
			return;
		}

		if ( !Networking.IsHost )
		{
			Log.Warning( $"{ApplyCommand}: must be host (editor play)." );
			return;
		}

		var config = Config.Current;
		var gameMode = config?.GameMode;
		if ( gameMode?.Equipments is null )
		{
			Log.Error( $"{ApplyCommand}: Config.GameMode.Equipments unavailable." );
			return;
		}

		var loaded = IsLoadedGameMode( config!, gameMode );
		if ( !loaded )
		{
			Log.Warning(
				$"{ApplyCommand}: game mode is not loaded (name=\"{gameMode.Name}\" isReady={config!.IsReady}); " +
				"rows go onto the placeholder and lp_weapon_roster_status will refuse to read them." );
		}

		var added = 0;
		var present = 0;
		var refused = 0;
		var flagsOn = 0;

		foreach ( var entry in WeaponRosterInjectTable.Entries )
		{
			if ( !entry.Enabled )
			{
				Log.Info( $"LP_WEAPON_ROSTER_INJECT weapon={entry.Key} flag=off result=skipped" );
				continue;
			}

			flagsOn++;
			var outcome = Ensure( gameMode, entry, out var reason );
			switch ( outcome )
			{
				case Outcome.Added:
					added++;
					break;
				case Outcome.AlreadyInjected:
				case Outcome.AlreadyPresent:
					present++;
					break;
				default:
					refused++;
					break;
			}

			Log.Info(
				$"LP_WEAPON_ROSTER_INJECT weapon={entry.Key} flag=on result={outcome} reason={reason} " +
				$"equipmentRowId={entry.EquipmentRowId} contentId={entry.ContentId}" );
		}

		Log.Info(
			$"LP_WEAPON_ROSTER_INJECT_SUMMARY candidates={WeaponRosterInjectTable.Entries.Length} flagsOn={flagsOn} " +
			$"added={added} alreadyPresent={present} refused={refused} marketRowsAdded=0 " +
			$"gameModeName=\"{gameMode.Name}\" isReady={config!.IsReady} loaded={loaded}" );
	}

	[ConCmd( StatusCommand )]
	public static void Status()
	{
		if ( !Application.IsEditor )
		{
			Log.Warning( $"{StatusCommand}: editor-only." );
			return;
		}

		var config = Config.Current;
		var gameMode = config?.GameMode;
		if ( gameMode?.Equipments is null )
		{
			Log.Warning( $"{StatusCommand}: Config.GameMode.Equipments unavailable." );
			return;
		}

		foreach ( var entry in WeaponRosterInjectTable.Entries )
		{
			var worldPrefab = GameObject.GetPrefab( entry.WorldPrefabPath ).IsValid();
			var viewPrefab = GameObject.GetPrefab( entry.ViewPrefabPath );
			var viewAsset = viewPrefab.IsValid() && viewPrefab.Components.Get<ViewModel>().IsValid();
			var pathRows = PathRows( gameMode.Equipments, entry );
			var source = pathRows.Length switch
			{
				0 => "none",
				1 => pathRows[0].Id == entry.EquipmentRowId ? "injected" : "other",
				_ => "duplicate"
			};

			Log.Info(
				$"LP_WEAPON_ROSTER_INJECT_STATUS weapon={entry.Key} flag={(entry.Enabled ? "on" : "off")} " +
				$"contentIdPinned={entry.ContentIdPinned} worldAsset={worldPrefab} viewAsset={viewAsset} " +
				$"pathRows={pathRows.Length} rowSource={source} slot={entry.Slot}" );
		}

		Log.Info(
			$"LP_WEAPON_ROSTER_INJECT_STATUS_SUMMARY candidates={WeaponRosterInjectTable.Entries.Length} " +
			$"equipmentRows={gameMode.Equipments.Count} marketRows={gameMode.MarketItems?.Count ?? 0} " +
			$"gameModeName=\"{gameMode.Name}\" isReady={config!.IsReady} loaded={IsLoadedGameMode( config, gameMode )}" );
	}

	/// <summary>True for a row this inject added (its row id and content id both match a table entry); never true for a Portal row.</summary>
	public static bool IsInjectedRow( GameModeEquipmentDto row )
	{
		return WeaponRosterInjectTable.Entries.Any( entry =>
			entry.EquipmentRowId == row.Id && entry.ContentId == row.GameModeAddonContentId );
	}

	private static bool IsLoadedGameMode( Config config, GameModeDto gameMode )
	{
		return config.IsReady && !string.Equals( gameMode.Name, NoGameModeName, StringComparison.Ordinal );
	}

	private static GameModeEquipmentDto[] PathRows(
		IEnumerable<GameModeEquipmentDto> rows, WeaponRosterInjectEntry entry )
	{
		return rows
			.Where( row => string.Equals(
				row.PrefabPath(), entry.WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) )
			.ToArray();
	}

	private static Outcome Ensure( GameModeDto gameMode, WeaponRosterInjectEntry entry, out string reason )
	{
		var equipments = gameMode.Equipments;

		if ( !GameObject.GetPrefab( entry.WorldPrefabPath ).IsValid() )
		{
			reason = "worldPrefabMissing";
			return Outcome.Refused;
		}

		var viewPrefab = GameObject.GetPrefab( entry.ViewPrefabPath );
		if ( !viewPrefab.IsValid() || !viewPrefab.Components.Get<ViewModel>().IsValid() )
		{
			reason = "viewPrefabMissingOrNoRootViewModel";
			return Outcome.Refused;
		}

		// Idempotent: an exact row for this world prefab (ours or a Portal row) is never doubled.
		var pathRows = PathRows( equipments, entry );
		if ( pathRows.Length > 1 )
		{
			reason = "duplicatePathRows";
			return Outcome.Refused;
		}

		if ( pathRows.Length == 1 )
		{
			var existing = pathRows[0];
			var sameView = string.Equals(
				existing.SecondaryPrefabPath(), entry.ViewPrefabPath, StringComparison.OrdinalIgnoreCase );
			var contentRows = equipments
				.Where( row => row.GameModeAddonContentId == existing.GameModeAddonContentId )
				.ToArray();
			if ( !sameView || contentRows.Length != 1 || contentRows[0].Id != existing.Id )
			{
				reason = "conflictingExistingRow";
				return Outcome.Refused;
			}

			// A pinned entry's give helper looks the row up by the pinned content id, so a row with another id would still fail closed there.
			if ( entry.ContentIdPinned && existing.GameModeAddonContentId != entry.ContentId )
			{
				reason = "existingRowHasDifferentContentIdThanPinned";
				return Outcome.Refused;
			}

			reason = existing.Id == entry.EquipmentRowId ? "rowExists" : "otherRowExists";
			return existing.Id == entry.EquipmentRowId ? Outcome.AlreadyInjected : Outcome.AlreadyPresent;
		}

		// No row for this world prefab: refuse if our ids or prefab refs are already claimed elsewhere.
		if ( equipments.Any( row =>
			row.Id == entry.EquipmentRowId || row.GameModeAddonContentId == entry.ContentId ) )
		{
			reason = "idClaimedByAnotherRow";
			return Outcome.Refused;
		}

		if ( GameModeAddonContents.All.Any( content =>
			content.Id != entry.ContentId &&
			(string.Equals( content.PrimaryReference, entry.WorldPrefabPath, StringComparison.OrdinalIgnoreCase ) ||
			 string.Equals( content.SecondaryReference, entry.ViewPrefabPath, StringComparison.OrdinalIgnoreCase )) ) )
		{
			reason = "prefabClaimedByOtherContent";
			return Outcome.Refused;
		}

		GameModeAddonContents.EnsureEditorContent(
			entry.ContentId,
			$"{entry.Name} (editor)",
			entry.WorldPrefabPath,
			entry.ViewPrefabPath,
			entry.Slot.ToString() );

		equipments.Add( new GameModeEquipmentDto
		{
			Id = entry.EquipmentRowId,
			GameModeAddonContentId = entry.ContentId,
			NameOverride = entry.Name,
			DescriptionOverride = InjectDescription,
			Limit = -1
		} );

		var resolved = PathRows( equipments, entry );
		if ( resolved.Length != 1 || resolved[0].Id != entry.EquipmentRowId ||
			!string.Equals( resolved[0].SecondaryPrefabPath(), entry.ViewPrefabPath, StringComparison.OrdinalIgnoreCase ) )
		{
			reason = "injectedRowDidNotResolveUniquely";
			return Outcome.Refused;
		}

		reason = "rowAdded";
		return Outcome.Added;
	}
}
