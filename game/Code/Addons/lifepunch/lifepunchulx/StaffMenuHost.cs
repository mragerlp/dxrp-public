// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// A player row the menu can render and target. Define-free so it flows through the
/// Dxura-free <see cref="StaffMenu"/> razor. <see cref="GroupName"/> buckets the roster by staff tier
/// (the player's rank name) with all non-staff collapsed into "Players"; <see cref="GroupOrder"/>
/// sorts the groups (highest rank first, "Players" last). <see cref="Role"/> is the player's real,
/// sanitised rank name for the detail pane (distinct from <see cref="GroupName"/>, which buckets
/// non-staff under "Players"); <see cref="RankColorHex"/> is the rank colour as "#RRGGBB";
/// <see cref="IsFrozen"/> reads the canonical DXRP freeze status; <see cref="PlayTimeMinutes"/> is
/// DXRP playtime in minutes (display as <c>/ 60</c> hours).
/// </summary>
public readonly record struct StaffMenuPlayer(
	long SteamId,
	string Name,
	bool CanTarget,
	bool IsFrozen,
	string GroupName,
	int GroupOrder,
	string Role,
	string RankColorHex,
	int PlayTimeMinutes );

/// <summary>
/// Richer, live per-player info for the selected-player detail pane. Define-free so it flows through the
/// Dxura-free razor. Computed on demand for the selected player only (not every row every frame).
/// <see cref="Found"/> is false for an off-roster / disconnected target (only <see cref="SteamId"/> is
/// then meaningful).
/// </summary>
public readonly record struct StaffPlayerDetail(
	long SteamId,
	string Name,
	string Role,
	string RankColorHex,
	int PlayTimeMinutes,
	int Level,
	string Job,
	string JobColorHex,
	int BaseSalary,
	uint Wallet,
	uint Bank,
	int Health,
	int MaxHealth,
	int Armor,
	int MaxArmor,
	int Kills,
	int Deaths,
	bool Found );

/// <summary>
/// One display-safe row from a player's durable Portal inventory. IDs, grant identifiers and
/// mutation controls deliberately never cross the ULX bridge.
/// </summary>
public readonly record struct StaffInventoryItem( string Name, string Type, string Rarity, int Quantity );

/// <summary>The truthful client-side state of a selected-player inventory read.</summary>
public enum StaffInventoryReadState
{
	Idle,
	Loading,
	Unavailable,
	CompletedEmpty,
	Populated
}

/// <summary>
/// One active player flag. <see cref="Illegitimate"/> means the player currently lacks
/// the permission required for that active power, including when permission was revoked.
/// </summary>
public readonly record struct StaffStateFlag( string Label, bool Illegitimate );

public readonly record struct StaffSanction(
	string Type,
	string TypeClass,
	bool IsActive,
	string Reason,
	string Duration,
	string Created,
	string Scope,
	string State,
	string Flags,
	string Notes );

/// <summary>
/// Distinct sanction-read outcomes. Empty, unavailable and failed reads must remain separate.
/// A missing or discarded response is indistinguishable from a delayed response and remains
/// <see cref="Loading"/> until a response or reset changes the client state.
/// </summary>
public enum SanctionsReadState
{
	/// <summary>Local viewer lacks the portal permission. Nothing was ever asked.</summary>
	PermissionDenied,

	/// <summary>The backing system is absent in this realm, so nothing can be read at all.</summary>
	Unavailable,

	/// <summary>Asked for this subject and no answer has landed yet. Also how a silently dropped request presents.</summary>
	Loading,

	/// <summary>The host ANSWERED by clearing: it declined to disclose this subject's record.</summary>
	Refused,

	/// <summary>The system holds a different subject; our answer never landed. This is not a clean record.</summary>
	Unanswered,

	/// <summary>Answered for this subject with zero rows visible to this caller.</summary>
	CompletedEmpty,

	/// <summary>The host reached the sanction service, but the read failed. No record claim is possible.</summary>
	Failed,

	/// <summary>The host answered under a permission-limited view; only a visible subset can be claimed.</summary>
	Filtered,

	/// <summary>Answered for this subject, with rows to show.</summary>
	Populated
}

/// <summary>Correlated host result for the staff money-grant form.</summary>
public enum StaffMoneyGrantState
{
	Idle,
	Pending,
	Succeeded,
	Rejected,
	Unknown
}

/// <summary>
/// One audit row as the menu renders it. <see cref="When"/> is the portal-style relative label;
/// <see cref="WhenUtc"/> is the raw stamp the label was rendered from and is what the time-window
/// filter compares against — the label alone is unfilterable ("Yesterday" has no ordering).
/// <see cref="WhenUtc"/> defaults to <c>default</c> for a row whose time is unknown; a row with an
/// unknown stamp is never hidden by a window, because we cannot prove it falls outside one.
/// </summary>
public readonly record struct StaffAuditEntry(
	string When,
	string Action,
	string Player,
	long PlayerSteamId,
	string Entity,
	string Description,
	System.DateTimeOffset WhenUtc = default );

/// <summary>
/// Which side of a row the Audit tab's Player ID box matches.
///
/// The feed only ever stores the ACTOR as a field (<see cref="StaffAuditEntry.PlayerSteamId"/> /
/// <see cref="StaffAuditEntry.Player"/>, both derived from <c>ServerApiClient.Audit</c>'s
/// <c>cause</c>). A TARGET is never a field — it survives only as interpolated text inside
/// <see cref="StaffAuditEntry.Description"/>. So <see cref="Target"/> is necessarily a description
/// substring match, not a field match, and it is only as good as the call site's wording.
/// </summary>
public enum AuditPlayerScope
{
	/// <summary>Rows this player performed. Field-exact; the default and the old behaviour.</summary>
	Actor,

	/// <summary>Rows that name this player in the description — usually rows performed ON them.</summary>
	Target,

	/// <summary>Either side. Matches what a player's profile "Recent actions" card already shows.</summary>
	Both
}

/// <summary>
/// One gamemode job row for the Set Job picker. Define-free so the razor compiles in the editor build.
/// <see cref="Token"/> is dispatched to DXRP's <c>/job</c> command (internal job name).
/// </summary>
public readonly record struct StaffJobOption( string Token, string Label, string ColorHex );

/// <summary>
/// DXRP host bindings behind <c>#if !LIFEPUNCH_LOCAL</c>, keeping Razor free of Dxura types.
/// The live branch reads ranks and dispatches through native host or chat interfaces;
/// the local branch supplies fixtures and logs clicks without dispatching.
/// The HUD host carries the build split because the editor's Razor pass does not reliably
/// honor LIFEPUNCH_LOCAL even when plain C# files receive it.
/// </summary>
internal static class StaffMenuHost
{
	public static string Localize( string key )
	{
#if LIFEPUNCH_LOCAL
		return key ?? string.Empty;
#else
		return Language.GetPhrase( key ?? string.Empty );
#endif
	}

	private static StaffMenu? _instance;

	/// <summary>Whether the menu is currently mounted/open.</summary>
	public static bool IsOpen => _instance.IsValid();

	/// <summary>Editor dev: the live menu instance (for scroll probes / test bots).</summary>
	internal static StaffMenu? DevMenu => _instance;

	/// <summary>The local viewer's Steam ID. Define-free: valid in both builds.</summary>
	public static long LocalSteamId => Sandbox.Game.SteamId;

	/// <summary>
	/// The viewer's server-configured rank order. Permission grants do not depend on its numeric value.
	/// The local fixture build returns order 10.
	/// </summary>
	public static int LocalRankOrder
	{
#if LIFEPUNCH_LOCAL
		get => 10;
#else
		get => RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankOrder( LocalSteamId ) : 0;
#endif
	}

	/// <summary>
	/// Configured pocket capacity for the read-only player profile. The local UI harness mirrors DXRP's
	/// default; the live build reads the authoritative game config and guards invalid non-positive values.
	/// </summary>
	public static int GetPocketSlotCount()
	{
#if LIFEPUNCH_LOCAL
		return 6;
#else
		return System.Math.Max( 1, Config.Current.Game.MaxPocketItems );
#endif
	}

	/// <summary>Whether the local viewer may request this online player's read-only pocket.</summary>
	public static bool CanViewPocket( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return steamId != 0;
#elif LIFEPUNCH_PACKAGE
		return false;
#else
		if ( steamId == 0 || !RankSystem.HasLocalPermission( Dxura.RP.Shared.Permission.ViewPocket ) )
		{
			return false;
		}

		var target = GameUtils.GetPlayerById( steamId );
		return target.IsValid() && RankSystem.CanLocalTarget( steamId );
#endif
	}

	/// <summary>Start a correlated read through DXRP's existing pocket system.</summary>
	public static void RequestPocket( long steamId )
	{
#if !LIFEPUNCH_LOCAL && !LIFEPUNCH_PACKAGE
		var system = PocketSystem.Instance;
		if ( !system.IsValid() || !CanViewPocket( steamId ) )
		{
			return;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( "pocket:view", Config.Current.Game.ActionCooldown ) )
		{
			Notify.Cooldown( "pocket:view" );
			return;
		}

		var requestId = System.Guid.NewGuid();
		system.BeginPocketViewClient( steamId, requestId );
		system.RequestPocketContentsHost( steamId, requestId );
#endif
	}

	public static IReadOnlyList<string> GetPocketItems( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return System.Array.Empty<string>();
#elif LIFEPUNCH_PACKAGE
		return System.Array.Empty<string>();
#else
		var system = PocketSystem.Instance;
		return system.IsValid() && system.AdminViewPlayerId == steamId
		       && !system.AdminViewIsLoading && !system.AdminViewIsUnavailable
			? system.AdminViewItems
			: System.Array.Empty<string>();
#endif
	}

	/// <summary>Display icon key for a slot in the current correlated pocket response.</summary>
	public static string GetPocketItemIcon( long steamId, int slot )
	{
#if !LIFEPUNCH_LOCAL && !LIFEPUNCH_PACKAGE
		var system = PocketSystem.Instance;
		if ( system.IsValid() && system.AdminViewPlayerId == steamId
		     && !system.AdminViewIsLoading && !system.AdminViewIsUnavailable
		     && slot >= 0 && slot < system.AdminViewItems.Count && slot < system.AdminViewItemKinds.Count )
		{
			return system.AdminViewItemKinds[slot] switch
			{
				PocketItemKind.Printer => "attach_money",
				PocketItemKind.Shipment => "inventory_2",
				PocketItemKind.Firearm => GetPocketFirearmIcon( system.AdminViewRequestId ),
				PocketItemKind.Plant => "local_florist",
				PocketItemKind.Equipment => "build",
				PocketItemKind.Medical => "local_hospital",
				_ => "category"
			};
		}
#endif
		return "category";
	}

	private static Guid? _pocketFirearmIconRequest;
	private static bool _pocketFirearmIconAvailable;

	private static string GetPocketFirearmIcon( Guid requestId )
	{
		// Retry on each new pocket snapshot, but avoid loading a texture per slot or frame.
		if ( _pocketFirearmIconRequest != requestId )
		{
			_pocketFirearmIconRequest = requestId;
			_pocketFirearmIconAvailable = false;
			try
			{
				var texture = Texture.LoadFromFileSystem( "ui/weapons/guns/M4_01.png", FileSystem.Mounted );
				_pocketFirearmIconAvailable = texture is not null && !texture.IsError;
			}
			catch ( Exception )
			{
				// Missing optional art must not prevent a permitted pocket read.
			}
		}

		return _pocketFirearmIconAvailable ? "firearm" : "category";
	}

	public static bool PocketIsLoading( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return false;
#elif LIFEPUNCH_PACKAGE
		return false;
#else
		var system = PocketSystem.Instance;
		return system.IsValid() && system.AdminViewPlayerId == steamId && system.AdminViewIsLoading;
#endif
	}

	public static bool PocketIsUnavailable( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return false;
#elif LIFEPUNCH_PACKAGE
		return true;
#else
		var system = PocketSystem.Instance;
		return !system.IsValid() || system.AdminViewPlayerId != steamId || system.AdminViewIsUnavailable;
#endif
	}

	public static int PocketClientRevision
	{
#if LIFEPUNCH_LOCAL
		get => 0;
#elif LIFEPUNCH_PACKAGE
		get => 0;
#else
		get => PocketSystem.Instance.IsValid() ? PocketSystem.Instance.AdminViewRevision : 0;
#endif
	}

	public static long PocketAnsweredFor
	{
#if LIFEPUNCH_LOCAL
		get => 0;
#elif LIFEPUNCH_PACKAGE
		get => 0;
#else
		get => PocketSystem.Instance.IsValid() ? PocketSystem.Instance.AdminViewPlayerId ?? 0 : 0;
#endif
	}

	public static void ClearPocketView()
	{
#if !LIFEPUNCH_LOCAL && !LIFEPUNCH_PACKAGE
		PocketSystem.Instance?.ClearPocketViewClient();
#endif
	}

	// --- Durable account inventory (read-only, host/API-backed) -----------

	private static readonly List<StaffInventoryItem> _inventoryItems = new();
	private static System.Guid _inventoryRequestId;
	private static long _inventorySubjectSteamId;
	private static long _inventoryAnsweredFor;
	private static StaffInventoryReadState _inventoryReadState;
	private static string _inventoryReadMessage = string.Empty;

	/// <summary>Bumped on every inventory transition so the open modal repaints when the host answers.</summary>
	public static int InventoryClientRevision { get; private set; }

	/// <summary>The subject attached to the most recent correlated host answer, or 0 while unanswered.</summary>
	public static long InventoryAnsweredFor => _inventoryAnsweredFor;

	/// <summary>Whether the local viewer may request this online player's durable account inventory.</summary>
	public static bool CanViewInventory( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return steamId != 0;
#else
		if ( steamId == 0 || !RankSystem.HasLocalPermission( Dxura.RP.Shared.Permission.ViewInventory ) )
		{
			return false;
		}

		var target = GameUtils.GetPlayerById( steamId );
		return target.IsValid() && !IsSyntheticPlayer( steamId ) && RankSystem.CanLocalTarget( steamId );
#endif
	}

	/// <summary>Begin a correlated, read-only host request for the selected player's Portal inventory.</summary>
	public static void RequestInventory( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		if ( _inventorySubjectSteamId == steamId && _inventoryReadState == StaffInventoryReadState.Loading )
		{
			return;
		}

		ClearInventoryView();
		_inventorySubjectSteamId = steamId;

		if ( !CanViewInventory( steamId ) )
		{
			_inventoryReadState = StaffInventoryReadState.Unavailable;
			_inventoryReadMessage = "Inventory unavailable: permission or targetability denied.";
			InventoryClientRevision++;
			return;
		}

		_inventoryRequestId = System.Guid.NewGuid();
		_inventoryReadState = StaffInventoryReadState.Loading;
		_inventoryReadMessage = "Loading the player's durable inventory through the server API...";
		InventoryClientRevision++;

#if LIFEPUNCH_LOCAL
		_inventoryAnsweredFor = steamId;
		_inventoryReadState = StaffInventoryReadState.CompletedEmpty;
		_inventoryReadMessage = "No durable inventory items in the local editor preview.";
		InventoryClientRevision++;
#else
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.RequestInventoryHost( steamId, _inventoryRequestId );
		}
		else
		{
			_inventoryReadState = StaffInventoryReadState.Unavailable;
			_inventoryReadMessage = "Inventory unavailable: the host bridge is not mounted.";
			InventoryClientRevision++;
		}
#endif
	}

	public static IReadOnlyList<StaffInventoryItem> GetInventoryItems( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		return _inventorySubjectSteamId == steamId && _inventoryReadState == StaffInventoryReadState.Populated
			? _inventoryItems
			: System.Array.Empty<StaffInventoryItem>();
	}

	public static StaffInventoryReadState GetInventoryState( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		return _inventorySubjectSteamId == steamId ? _inventoryReadState : StaffInventoryReadState.Idle;
	}

	public static string GetInventoryMessage( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		return _inventorySubjectSteamId == steamId ? _inventoryReadMessage : string.Empty;
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>Accept one caller-filtered host result only when it matches the active subject/request.</summary>
	internal static void OnInventoryReceived(
		System.Guid requestId,
		long steamId,
		string[] names,
		int[] quantities,
		string[] types,
		string[] rarities,
		bool succeeded,
		string message )
	{
		EnsureServerScope();
		if ( requestId == System.Guid.Empty || requestId != _inventoryRequestId || steamId != _inventorySubjectSteamId )
		{
			return;
		}

		_inventoryItems.Clear();
		_inventoryAnsweredFor = steamId;
		if ( !succeeded || !CanViewInventory( steamId ) )
		{
			_inventoryReadState = StaffInventoryReadState.Unavailable;
			_inventoryReadMessage = string.IsNullOrWhiteSpace( message )
				? "Inventory unavailable: the host could not confirm this read."
				: message.Trim();
			InventoryClientRevision++;
			return;
		}

		names ??= System.Array.Empty<string>();
		quantities ??= System.Array.Empty<int>();
		types ??= System.Array.Empty<string>();
		rarities ??= System.Array.Empty<string>();
		var count = System.Math.Min( System.Math.Min( names.Length, quantities.Length ),
			System.Math.Min( types.Length, rarities.Length ) );
		for ( var i = 0; i < count; i++ )
		{
			var name = names[i]?.Trim() ?? string.Empty;
			if ( name.Length == 0 || quantities[i] <= 0 )
			{
				continue;
			}

			_inventoryItems.Add( new StaffInventoryItem(
				name,
				string.IsNullOrWhiteSpace( types[i] ) ? "Item" : types[i].Trim(),
				string.IsNullOrWhiteSpace( rarities[i] ) ? "Standard" : rarities[i].Trim(),
				quantities[i] ) );
		}

		_inventoryReadState = _inventoryItems.Count == 0
			? StaffInventoryReadState.CompletedEmpty
			: StaffInventoryReadState.Populated;
		_inventoryReadMessage = _inventoryItems.Count == 0
			? "No durable inventory items are recorded for this player."
			: string.Empty;
		InventoryClientRevision++;
	}
#endif

	public static void ClearInventoryView()
	{
		if ( _inventoryRequestId == System.Guid.Empty && _inventorySubjectSteamId == 0
		     && _inventoryAnsweredFor == 0 && _inventoryItems.Count == 0
		     && _inventoryReadState == StaffInventoryReadState.Idle && _inventoryReadMessage.Length == 0 )
		{
			return;
		}

		_inventoryRequestId = System.Guid.Empty;
		_inventorySubjectSteamId = 0;
		_inventoryAnsweredFor = 0;
		_inventoryItems.Clear();
		_inventoryReadState = StaffInventoryReadState.Idle;
		_inventoryReadMessage = string.Empty;
		InventoryClientRevision++;
	}

	/// <summary>
	/// Client-local UI scale preference — survives menu close/reopen for the session (not portal-persisted).
	/// </summary>
	public static LifePunchUiScaleSize SavedUiScale { get; set; } = LifePunchUiScaleSize.ExtraLarge;

	// --- Open / close ------------------------------------------------------

	/// <summary>
	/// Console + chat entry point. Staff bind any key to <c>lifepunchulx</c>, <c>menu</c>, or <c>ulx</c>
	/// (e.g. <c>bind f4 lifepunchulx</c>). Chat: <c>/lifepunchulx</c>, <c>/menu</c>, <c>/ulx</c>.
	/// </summary>
	[ConCmd( "lifepunchulx" )]
	public static void LifepunchUlxConCmd() => Toggle();

	[ConCmd( "menu" )]
	public static void MenuConCmd() => Toggle();

	[ConCmd( "ulx" )]
	public static void UlxConCmd() => Toggle();

	/// <summary>
	/// Open the menu if closed, else close it. Open-for-all by design: any player may open it via the
	/// command; non-staff simply see an empty catalog (every action is permission-gated per-row). This
	/// matches the portal model where access is decided purely by rank grants on the viewer's Steam ID.
	/// </summary>
	public static void Toggle()
	{
		if ( IsOpen )
		{
			RequestClose();
			return;
		}

		_instance = Mount();
		if ( _instance.IsValid() )
		{
			SetCursorMode( true );
			return;
		}

		Log.Warning( "[lifepunchulx] Toggle failed — menu did not mount (see prior mount warnings)." );
	}

	/// <summary>Close and tear down the open menu, if any.</summary>
	public static void RequestClose()
	{
		if ( MoneyGrantBlocksMenuClose )
		{
			return;
		}

		if ( _instance.IsValid() )
		{
			Close( _instance );
		}

		_instance = null;
		SetCursorMode( false );
	}

	/// <summary>
	/// Release look controls while the menu is open through <c>Player.LockCamera</c>,
	/// which drives Controller.UseLookControls in Player.Camera.cs. No-op in the local fixture build.
	/// </summary>
	private static void SetCursorMode( bool menuOpen )
	{
#if !LIFEPUNCH_LOCAL
		if ( Player.Local.IsValid() )
		{
			Player.Local.LockCamera = menuOpen;
		}
#endif
	}

	public static bool IsGuardedStatusToggle( string actionKey ) =>
		actionKey is "freeze" or "god" or "cloak" or "incognito";

	/// <summary>Whole-number form limits; native commands remain authoritative at execution.</summary>
	public static bool TryGetNumberInputBounds( string actionKey, long targetSteamId, out uint minimum, out uint maximum )
	{
		minimum = 0;
		maximum = 0;
		if ( actionKey == "sethealth" )
		{
			minimum = 1;
			maximum = 1_000_000; // Native SetHealthCommand.MaximumHealth.
			return true;
		}

		if ( actionKey != "setarmor" ) return false;
#if !LIFEPUNCH_LOCAL
		var target = GameUtils.GetPlayerById( targetSteamId );
		if ( !target.IsValid() || !target.ArmorComponent.IsValid() ) return false;
		var limit = target.ArmorComponent.MaxArmor;
		if ( !float.IsFinite( limit ) || limit < 0f ) return false;
		maximum = (uint)System.Math.Floor( System.Math.Min( (double)limit, int.MaxValue ) );
		return true;
#else
		return false;
#endif
	}

	/// <summary>
	/// Observed state used to describe a toggle's next operation. Match the source the native
	/// command tests; null means unavailable, not OFF. X-ray is observable for the local player only.
	/// </summary>
	public static bool? GetCommandToggleState( string actionKey, long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return null;
		}

		switch ( actionKey )
		{
			case "xray":
				return player == Player.Local ? GetLocalXrayState() : null;
			case "god":
				return player.HasStatus( Constants.GodStatus );
			case "cloak":
				return player.HasStatus( "cloak" );
			case "incognito":
				return player.HasStatus( "incognito" );
			case "freeze":
				return player.HasStatus( Constants.FreezeStatus );
			case "noclip":
				if ( !player.Controller.IsValid() )
				{
					return null;
				}
				var noclip = player.Controller.Components.Get<MoveModeNoClip>();
				return noclip.IsValid() ? noclip.IsNoclipping : null;
		}
#endif
		return null;
	}

	/// <summary>
	/// Read local toggle state from native health, status and movement components.
	/// This reflects state changed through other command routes; the local fixture build returns false.
	/// </summary>
	public static bool IsSelfToggleOn( string actionKey )
	{
#if !LIFEPUNCH_LOCAL
		if ( !Player.Local.IsValid() )
		{
			return false;
		}

		if ( actionKey == "xray" )
		{
			return GetLocalXrayState() == true;
		}

		if ( actionKey == "noclip" )
		{
			if ( !Player.Local.Controller.IsValid() )
			{
				return false;
			}

			var noclip = Player.Local.Controller.Components.Get<MoveModeNoClip>();
			return noclip.IsValid() && noclip.IsNoclipping;
		}

		// God mode is NOT a status -- it is a property on HealthComponent, and it is the exact
		// value DXRP's own HUD reads (Vitals.razor: HealthComponent?.IsGodMode). Reading the same
		// field is what makes drift between the card and the HUD impossible.
		if ( actionKey == "god" )
		{
			return Player.Local.HealthComponent.IsValid() && Player.Local.HealthComponent.IsGodMode;
		}

		// Cloak and incognito ARE statuses. The live Statuses dictionary is keyed by the same short
		// lowercase id as the chat command that sets them ("afk" observed in session).
		if ( actionKey is "cloak" or "incognito" )
		{
			return Player.Local.HasStatus( actionKey );
		}
#endif
		return false;
	}

	/// <summary>
	/// Health colour taken from DXRP's OWN HUD function -- <c>UiUtils.HealthColorHex</c>, the one
	/// <c>PlayerInfo.razor</c> and <c>PartyMemberCard.razor</c> both call. Calling it rather than
	/// copying its constants is what makes console/HUD drift impossible: if DXRP retunes the ramp,
	/// this follows. Ramp of record: #f87171 at &lt;=15%, #facc15 mid, #22c55e high, lerped.
	/// Empty string in the editor build, where the razor falls back to its own token.
	/// </summary>
	public static string HealthColorHex( int health, int maxHealth )
	{
#if !LIFEPUNCH_LOCAL
		var percent = maxHealth > 0
			? System.Math.Clamp( (float)health / maxHealth * 100f, 0f, 100f )
			: 0f;
		return Dxura.RP.Game.UI.UiUtils.HealthColorHex( percent );
#else
		return "";
#endif
	}


	/// <summary>Selected player's native HUD health color, including the HUD's upward rounding.</summary>
	public static string PlayerHealthColorHex( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( player.IsValid() && player.HealthComponent.IsValid() )
		{
			return HealthColorHex( player.HealthComponent.Health.CeilToInt(), player.HealthComponent.MaxHealth.CeilToInt() );
		}
#endif
		return "";
	}

	// --- Sanction history (caller-filtered host bridge) ------------------
	// Both runtime builds use the same host answer, including its explicit read scope.
	// Native Tab-menu reads cannot replace or relabel this menu's cached answer.

	/// <summary>Sanction request counter; response and scope changes update the client revision.</summary>
	public static int SanctionsVersion { get; private set; }

#if !LIFEPUNCH_LOCAL
	private static readonly List<StaffSanction> _sanctions = new();
	private static System.Guid _sanctionsRequestId;
	private static long _sanctionsSubjectId;
	private static long _sanctionsAnsweredFor;
	private static bool _sanctionsCanView;
	private static bool _sanctionsCanViewNotes;
	private static bool _sanctionsRefreshPending;
	private static bool _sanctionsHasRequested;
	private static RealTimeSince _sanctionsRequestAge;
	private const float SanctionsRequestInterval = 1f;
	private const float SanctionsScopeRetryInterval = 5f;
	private static float _sanctionsRetryInterval = SanctionsRequestInterval;
	private static SanctionsReadState _sanctionsState = SanctionsReadState.Unanswered;
	private static int _sanctionsClientRevision;
#endif

	/// <summary>Client revision for loading, delivered answers, and permission-scope changes.</summary>
	public static int SanctionsClientRevision
	{
		get
		{
#if !LIFEPUNCH_LOCAL
			EnsureSanctionsScope();
			return _sanctionsClientRevision;
#else
			return 0;
#endif
		}
	}

	/// <summary>Subject of the current bridge answer, or zero while no answer is readable.</summary>
	public static long SanctionsAnsweredFor
	{
		get
		{
#if !LIFEPUNCH_LOCAL
			EnsureSanctionsScope();
			return _sanctionsAnsweredFor;
#else
			return 0;
#endif
		}
	}

	public static bool CanViewSanctions( long subjectSteamId )
	{
		if ( subjectSteamId == 0 )
		{
			return false;
		}

		return subjectSteamId == LocalSteamId
			? CanView( "player.sanctions.view.self" ) || CanView( "player.sanctions.view.other" )
			: CanView( "player.sanctions.view.other" );
	}

	/// <summary>
	/// Select a subject and ask the host for history. A denied selection is remembered without
	/// sending a request, so a later grant can fetch a new answer instead of relabelling old data.
	/// </summary>
	public static void RequestSanctions( long steamId, bool forceRetry = false )
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
		if ( steamId == 0 )
		{
			return;
		}

		var canView = CanViewSanctions( steamId );
		var canViewNotes = canView && CanView( "player.sanctions.view.notes" );
		if ( _sanctionsSubjectId != steamId || _sanctionsCanView != canView
		     || _sanctionsCanViewNotes != canViewNotes || forceRetry )
		{
			_sanctionsSubjectId = steamId;
			_sanctionsCanView = canView;
			_sanctionsCanViewNotes = canViewNotes;
			InvalidateSanctionsAnswer();
		}

		TryRequestSanctions();
#endif
	}

	public static bool SanctionsLoading( long steamId ) =>
		GetSanctionsState( steamId ) == SanctionsReadState.Loading;

	/// <summary>
	/// The stored host outcome is never upgraded using a later grant. An empty list alone
	/// does not establish a clean record, and limited answers remain explicitly Filtered.
	/// </summary>
	public static SanctionsReadState GetSanctionsState( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		EnsureSanctionsScope();
#endif
		if ( !CanViewSanctions( steamId ) )
		{
			return SanctionsReadState.PermissionDenied;
		}

#if !LIFEPUNCH_LOCAL
		return _sanctionsSubjectId == steamId
			? _sanctionsState
			: SanctionsReadState.Unanswered;
#else
		return SanctionsReadState.Unavailable;
#endif
	}

	/// <summary>Only the active subject's answer under its unchanged permission scope is readable.</summary>
	public static IReadOnlyList<StaffSanction> GetSanctions( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var state = GetSanctionsState( steamId );
		return state is SanctionsReadState.Populated or SanctionsReadState.Filtered
			? _sanctions
			: System.Array.Empty<StaffSanction>();
#else
		return System.Array.Empty<StaffSanction>();
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static void EnsureSanctionsScope()
	{
		EnsureServerScope();
		if ( _sanctionsSubjectId == 0 )
		{
			return;
		}

		var canView = CanViewSanctions( _sanctionsSubjectId );
		var canViewNotes = canView && CanView( "player.sanctions.view.notes" );
		if ( _sanctionsCanView != canView || _sanctionsCanViewNotes != canViewNotes )
		{
			_sanctionsCanView = canView;
			_sanctionsCanViewNotes = canViewNotes;
			InvalidateSanctionsAnswer();
		}

		TryRequestSanctions();
	}

	private static void InvalidateSanctionsAnswer()
	{
		// Invalidate correlation immediately; neither an old answer nor its in-flight reply
		// can survive a permission transition. Keep the last-issued time across invalidation.
		_sanctionsRequestId = System.Guid.Empty;
		_sanctionsAnsweredFor = 0;
		_sanctions.Clear();
		_sanctionsRefreshPending = _sanctionsCanView;
		_sanctionsRetryInterval = SanctionsRequestInterval;
		_sanctionsState = _sanctionsCanView ? SanctionsReadState.Loading : SanctionsReadState.PermissionDenied;
		_sanctionsClientRevision++;
	}

	private static void TryRequestSanctions()
	{
		if ( !_sanctionsRefreshPending || !_sanctionsCanView
		     || (_sanctionsHasRequested && _sanctionsRequestAge < _sanctionsRetryInterval) )
		{
			return;
		}

		_sanctionsRefreshPending = false;
		_sanctions.Clear();
		_sanctionsAnsweredFor = 0;
		_sanctionsState = SanctionsReadState.Loading;
		_sanctionsClientRevision++;
		_sanctionsRequestId = System.Guid.NewGuid();
		_sanctionsRequestAge = 0;
		_sanctionsHasRequested = true;
		SanctionsVersion++;
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			StaffMenuBridgeService.Instance.RequestSanctionsHost( _sanctionsSubjectId, _sanctionsRequestId );
		}
		else
		{
			_sanctionsState = SanctionsReadState.Unavailable;
			_sanctionsClientRevision++;
		}
	}

	/// <summary>Accept one caller-filtered bridge answer for the active subject and request scope.</summary>
	internal static void OnSanctionsReceived(
		System.Guid requestId,
		long steamId,
		int outcome,
		string[] types,
		bool[] active,
		string[] reasons,
		string[] durations,
		string[] created,
		string[] scopes,
		string[] states,
		string[] flags,
		string[] notes )
	{
		EnsureSanctionsScope();
		if ( requestId == System.Guid.Empty || requestId != _sanctionsRequestId
		     || steamId != _sanctionsSubjectId || !_sanctionsCanView
		     || _sanctionsState != SanctionsReadState.Loading )
		{
			return;
		}

		_sanctions.Clear();
		_sanctionsAnsweredFor = steamId;
		var nextState = (SanctionsReadState)outcome;
		if ( nextState is not (SanctionsReadState.Populated or SanctionsReadState.CompletedEmpty
			or SanctionsReadState.Filtered or SanctionsReadState.Refused or SanctionsReadState.Failed) )
		{
			nextState = SanctionsReadState.Unavailable;
		}

		// Outcome records the host's answered scope. A local grant cannot promote Filtered,
		// and a host still seeing an older grant cannot expose notes to a now-limited viewer.
		var canReadNotes = _sanctionsCanViewNotes
			&& nextState is SanctionsReadState.Populated or SanctionsReadState.CompletedEmpty;
		if ( !_sanctionsCanViewNotes
		     && nextState is SanctionsReadState.Populated or SanctionsReadState.CompletedEmpty )
		{
			nextState = SanctionsReadState.Filtered;
		}

		types ??= System.Array.Empty<string>();
		active ??= System.Array.Empty<bool>();
		reasons ??= System.Array.Empty<string>();
		durations ??= System.Array.Empty<string>();
		created ??= System.Array.Empty<string>();
		scopes ??= System.Array.Empty<string>();
		states ??= System.Array.Empty<string>();
		flags ??= System.Array.Empty<string>();
		notes ??= System.Array.Empty<string>();
		var lengths = new[]
		{
			types.Length, active.Length, reasons.Length, durations.Length, created.Length,
			scopes.Length, states.Length, flags.Length, notes.Length
		};
		var count = types.Length;
		if ( lengths.Any( length => length != count )
		     || (nextState == SanctionsReadState.CompletedEmpty && count != 0)
		     || (nextState == SanctionsReadState.Populated && count == 0) )
		{
			// A malformed projection is unknown, never proof of an empty record.
			nextState = SanctionsReadState.Failed;
		}

		if ( nextState is SanctionsReadState.Populated or SanctionsReadState.Filtered )
		{
			for ( var i = 0; i < count; i++ )
			{
				if ( !canReadNotes
				     && (!System.Enum.TryParse<Dxura.RP.Shared.SanctionFlags>( flags[i], true, out var sanctionFlags )
				         || (sanctionFlags & Dxura.RP.Shared.SanctionFlags.Privileged) != 0) )
				{
					continue;
				}

				var rawType = string.IsNullOrWhiteSpace( types[i] ) ? "Unknown" : types[i].Trim();
				_sanctions.Add( new StaffSanction(
					SplitPascalCase( rawType ),
					rawType.ToLowerInvariant(),
					active[i],
					string.IsNullOrWhiteSpace( reasons[i] ) ? "No reason recorded" : reasons[i].Trim(),
					string.IsNullOrWhiteSpace( durations[i] ) ? "Permanent" : durations[i].Trim(),
					created[i]?.Trim() ?? string.Empty,
					scopes[i]?.Trim() ?? string.Empty,
					states[i]?.Trim() ?? string.Empty,
					FormatSanctionFlags( flags[i] ),
					canReadNotes ? notes[i]?.Trim() ?? string.Empty : string.Empty ) );
			}
		}

		_sanctionsState = nextState;
		// Permission sync can lag on either end. Keep the answered scope honest while
		// allowing a bounded new read to recover once the host observes the same grant.
		_sanctionsRefreshPending = nextState == SanctionsReadState.Refused
			|| (nextState == SanctionsReadState.Filtered && _sanctionsCanViewNotes);
		_sanctionsRetryInterval = SanctionsScopeRetryInterval;
		_sanctionsClientRevision++;
	}

	private static void ClearSanctionsView()
	{
		_sanctionsRequestId = System.Guid.Empty;
		_sanctionsSubjectId = 0;
		_sanctionsAnsweredFor = 0;
		_sanctionsCanView = false;
		_sanctionsCanViewNotes = false;
		_sanctionsRefreshPending = false;
		_sanctionsHasRequested = false;
		_sanctionsState = SanctionsReadState.Unanswered;
		_sanctions.Clear();
		_sanctionsClientRevision++;
	}
#endif

	/// <summary>
	/// The tenant-configured job group name, resolved through GameModeJobGroupId.
	/// Empty when the job or group cannot be resolved.
	/// </summary>
	public static string GetJobCategory( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() || player.Job is null )
		{
			return "";
		}

		return GameModeJobs.FindGroupById( player.Job.GameModeJobGroupId )?.Name ?? "";
#else
		return "";
#endif
	}

	/// <summary>
	/// Live state flags for a player, read from the same sources the commands write to. Only ACTIVE
	/// flags are returned. Powers (god/cloak/incognito/noclip) are checked against the permission
	/// that grants them, so an operator can tell a staff member's own toggle from a player running
	/// a command they should not have. Conditions (frozen/jailed/gagged) are done TO a player
	/// rather than wielded by one, so they are never flagged illegitimate.
	/// X-ray is included only for the local player; remote X-ray state is unavailable.
	/// </summary>
	public static IReadOnlyList<StaffStateFlag> GetStateFlags( long steamId )
	{
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return System.Array.Empty<StaffStateFlag>();
		}

		var flags = new List<StaffStateFlag>();

		void Power( bool active, string label, string permissionId )
		{
			if ( active )
			{
				flags.Add( new StaffStateFlag( label, !RankSystem.HasPermission( steamId, permissionId ) ) );
			}
		}

		// God mode is a HealthComponent property, not a status -- same field Vitals.razor reads.
		Power( player.HealthComponent.IsValid() && player.HealthComponent.IsGodMode, "God mode", "command.god" );
		Power( player.HasStatus( "cloak" ), "Cloaked", "command.cloak" );
		Power( player.HasStatus( "incognito" ), "Incognito", "command.incognito" );

		var noclip = player.Controller.IsValid()
			? player.Controller.Components.Get<MoveModeNoClip>()
			: null;
		if ( noclip.IsValid() && noclip.IsNoclipping )
		{
			var allowed = Config.Current.Game.NoClip || RankSystem.HasPermission( steamId, "ability.noclip" );
			flags.Add( new StaffStateFlag( "Noclip", !allowed ) );
		}

		// Native X-ray belongs to this client; never attach its state to a remote player.
		Power( player == Player.Local && GetLocalXrayState() == true, "X-ray", "command.xray" );

		// Status ids are DXRP's own constants (Constants.FreezeStatus / PrisonerStatus / GaggedStatus).
		if ( player.HasStatus( Constants.FreezeStatus ) )
		{
			flags.Add( new StaffStateFlag( "Frozen", false ) );
		}

		if ( player.HasStatus( "prisoner" ) )
		{
			flags.Add( new StaffStateFlag( "Jailed", false ) );
		}

		if ( player.HasStatus( "gagged" ) )
		{
			flags.Add( new StaffStateFlag( "Gagged", false ) );
		}

		return flags;
#else
		return System.Array.Empty<StaffStateFlag>();
#endif
	}

	/// <summary>"AutomaticBan" -> "Automatic Ban", matching DXRP's own sanction label rendering.</summary>
	private static string SplitPascalCase( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			return "";
		}

		var builder = new System.Text.StringBuilder( value.Length + 8 );
		for ( var i = 0; i < value.Length; i++ )
		{
			if ( i > 0 && char.IsUpper( value[i] ) )
			{
				builder.Append( ' ' );
			}

			builder.Append( value[i] );
		}

		return builder.ToString();
	}

	/// <summary>Turn the permission-filtered sanction flag enum into portal-style display text.</summary>
	private static string FormatSanctionFlags( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) || string.Equals( value, "None", System.StringComparison.OrdinalIgnoreCase ) )
		{
			return "None";
		}

		return string.Join( ", ", value.Split( ',' )
			.Select( part => SplitPascalCase( part.Trim() ) )
			.Where( part => !string.IsNullOrWhiteSpace( part ) ) );
	}

	private static StaffMenu? Mount()
	{
		// Close any prior instance first (guards against a stale component surviving a hotload/reload).
		var existing = Sandbox.Game.ActiveScene?.GetAllComponents<StaffMenu>().FirstOrDefault();
		if ( existing.IsValid() )
		{
			Close( existing );
		}

#if LIFEPUNCH_LOCAL
		return MountOnScreenPanel();
#else
		// Prefer DXRP HUD root (proven clickable path). Dedicated / early join sometimes has no HUD root yet.
		var panel = GameManager.ShowUi<StaffMenu>();
		if ( panel.IsValid() )
			return panel;

		Log.Warning( "[lifepunchulx] GameManager.ShowUi returned null — falling back to ScreenPanel." );
		return MountOnScreenPanel();
#endif
	}

	private static StaffMenu? MountOnScreenPanel()
	{
		var scene = Sandbox.Game.ActiveScene;
		if ( scene is null )
		{
			Log.Warning( "[lifepunchulx] ActiveScene is null — cannot mount menu." );
			return null;
		}

		var go = scene.CreateObject();
		go.Name = MenuObjectName;
		go.AddComponent<ScreenPanel>();
		return go.AddComponent<StaffMenu>();
	}

	private const string MenuObjectName = "LifePunchUlx";

	private static void Close( StaffMenu menu )
	{
		if ( !menu.IsValid() )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		// Local build hosts the panel on its own object, so destroy the whole object.
		if ( menu.GameObject.IsValid() )
		{
			menu.GameObject.Destroy();
		}
		else
		{
			menu.Destroy();
		}
#else
		// ShowUi shares the HUD root; ScreenPanel fallback uses MenuObjectName — match LpHashdUiHost teardown.
		if ( menu.GameObject.IsValid() && menu.GameObject.Name == MenuObjectName )
			menu.GameObject.Destroy();
		else
			menu.Destroy();
#endif
	}

	// --- Permission / roster reads (client-side, for UX gating only) -------

	/// <summary>
	/// True if the local viewer's ranks grant <paramref name="permissionId"/>. UX gating only —
	/// the host re-checks on dispatch.
	/// </summary>
	public static bool CanView( string permissionId )
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		return RankSystem.HasLocalPermission( permissionId );
#endif
	}

	/// <summary>Label for the "non-staff" roster bucket.</summary>
	public const string NonStaffGroup = "Players";

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// DXRP stores cumulative playtime in <see cref="Player.PlayTime"/> as elapsed seconds
	/// (<see cref="TimeSince"/>); divide by 60 for portal-style minutes (matches VoteSystem checks).
	/// </summary>
	static int PlayTimeMinutesFrom( Player player ) => (int)( player.PlayTime / 60f );
#endif

	/// <summary>
	/// True when the live player is synthetic. Package builds fail closed on the engine's
	/// <c>IsDebugPlayer</c> flag without reaching workbench-only registry types.
	/// </summary>
	public static bool IsSyntheticPlayer( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return false;
#else
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return false;
		}

#if LIFEPUNCH_PACKAGE
		return player.IsDebugPlayer;
#else
		return SyntheticActorRegistry.IsSynthetic( steamId, player.IsDebugPlayer );
#endif
#endif
	}

	/// <summary>The online players the menu can list, grouped by staff tier, targetability resolved.</summary>
	public static IReadOnlyList<StaffMenuPlayer> OnlinePlayers()
	{
#if LIFEPUNCH_LOCAL
		return new List<StaffMenuPlayer>
		{
			new( 5L, "Owner Olivia", false, false, "Owner", 100, "Owner", "#E74C3C", 10980 ),
			new( 4L, "Super Sam", false, false, "Super Admin", 10, "Super Admin", "#3498DB", 5400 ),
			new( 3L, "Admin Andy", false, false, "Admin", 5, "Admin", "#2ECC71", 2400 ),
			new( 6L, "Mod Maddie", true, false, "Mod", 4, "Mod", "#9B59B6", 900 ),
			new( 1L, "Regular Rick", true, false, NonStaffGroup, int.MinValue, "Member", "#FFFFFF", 300 ),
			new( 2L, "Suspicious Sammy", true, false, NonStaffGroup, int.MinValue, "VIP", "#F1C40F", 120 )
		};
#else
		return GameUtils.Players
			.Where( player => player.IsValid() )
			.Select( player =>
			{
				var staff = IsStaff( player.SteamId );
				var group = staff ? RankName( player.SteamId ) : NonStaffGroup;
				var order = staff ? RankOrder( player.SteamId ) : int.MinValue;
				return new StaffMenuPlayer(
					player.SteamId,
					player.DisplayName,
					RankSystem.CanLocalTarget( player.SteamId ),
					player.HasStatus( Constants.FreezeStatus ),
					group,
					order,
					RealRole( player.SteamId ),
					RankColorHex( player.SteamId ),
					PlayTimeMinutesFrom( player ) );
			} )
			.ToList();
#endif
	}

	/// <summary>Live detail for the selected player's profile pane. Found=false if they aren't connected.</summary>
	public static StaffPlayerDetail GetPlayerDetail( long steamId )
	{
#if LIFEPUNCH_LOCAL
		var p = OnlinePlayers().FirstOrDefault( x => x.SteamId == steamId );
		if ( p.SteamId == 0 )
		{
			return new StaffPlayerDetail( steamId, "", "—", "#ffffff", 0, 0, "—", "#ffffff", 0, 0, 0, 0, 0, 0, 0, 0, 0, false );
		}

		return new StaffPlayerDetail( p.SteamId, p.Name, p.Role, p.RankColorHex, p.PlayTimeMinutes,
			2, "Citizen", "#5DA9E9", 50, 1240, 540323, 100, 100, 25, 100, 12, 4, true );
#else
		var player = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == steamId );
		if ( !player.IsValid() )
		{
			return new StaffPlayerDetail( steamId, "", "—", "#ffffff", 0, 0, "—", "#ffffff", 0, 0, 0, 0, 0, 0, 0, 0, 0, false );
		}

		var health = player.HealthComponent.IsValid() ? (int)player.HealthComponent.Health : 0;
		var maxHealth = player.HealthComponent.IsValid() ? (int)player.HealthComponent.MaxHealth : 0;
		var armor = player.ArmorComponent.IsValid() ? (int)player.ArmorComponent.Armor : 0;
		var maxArmor = player.ArmorComponent.IsValid() ? (int)player.ArmorComponent.MaxArmor : 0;

		// JobDisplayName is CustomJob ?? Job.DisplayName(); guard the rare pre-init state where both are null.
		var job = "—";
		var jobColor = "#ffffff";
		if ( player.CustomJob != null || player.Job != null )
		{
			var name = player.JobDisplayName;
			if ( !string.IsNullOrWhiteSpace( name ) )
			{
				job = name;
			}
		}

		if ( player.Job != null )
		{
			jobColor = $"#{player.Job.Color & 0xFFFFFFu:X6}";
		}

		return new StaffPlayerDetail(
			player.SteamId,
			player.DisplayName,
			RealRole( player.SteamId ),
			RankColorHex( player.SteamId ),
			PlayTimeMinutesFrom( player ),
			player.Level,
			job,
			jobColor,
			player.Job?.Salary ?? 0,
			player.WalletBalance,
			player.BankBalance,
			health,
			maxHealth,
			armor,
			maxArmor,
			player.Kills,
			player.Deaths,
			true );
#endif
	}

	// --- Audit log (read-side, portal-mirrored) ---------------------------

	/// <summary>
	/// Audit-view permission ID, matching Permission.ViewAudit in game/Code/Api/Enums/Permission.cs.
	/// A string keeps the local fixture build independent of the Dxura enum.
	/// </summary>
	public const string AuditPermissionId = "portal.audit.view";

	/// <summary>
	/// Whether the local viewer may open the Audit view. This is a UI permission check.
	/// </summary>
	public static bool CanViewAudit() => CanView( AuditPermissionId );

	private static readonly List<StaffAuditEntry> _remoteAuditRows = new();
	private static System.Guid _auditRequestId;
	private static RealTimeSince _auditRequestAge = 10f;
	private static string _auditReadState = "idle";
	private static string _auditCoverage = string.Empty;
	private static int _remoteAuditVersion;

	private static void ClearAuditView()
	{
		if ( _remoteAuditRows.Count != 0 || _auditRequestId != System.Guid.Empty
			|| _auditReadState != "idle" || _auditCoverage.Length != 0 )
			_remoteAuditVersion++;
		_remoteAuditRows.Clear();
		_auditRequestId = System.Guid.Empty;
		_auditReadState = "idle";
		_auditCoverage = string.Empty;
		_auditRequestAge = 10f;
	}

	/// <summary>Invalidate disclosed data as soon as the local grant or server scope changes.</summary>
	private static bool EnsureAuditAccess()
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		if ( CanViewAudit() ) return true;
		ClearAuditView();
		return false;
	}

	/// <summary>Request a bounded host snapshot; repeated consumers share one in-flight read.</summary>
	public static void RefreshAuditSnapshot( bool force = false )
	{
		if ( !EnsureAuditAccess() ) return;
#if !LIFEPUNCH_LOCAL
		if ( Networking.IsHost ) return;
		if ( _auditRequestId != System.Guid.Empty )
		{
			if ( _auditRequestAge < 10f ) return;
			ClearAuditView();
			_auditReadState = "failed";
			_auditRequestAge = 0f;
			_remoteAuditVersion++;
			return;
		}
		// Even an explicit refresh respects the host's one-second request cooldown.
		if ( _auditRequestAge < 1f || (!force && _auditRequestAge < 5f) ) return;
		if ( !StaffMenuBridgeService.Instance.IsValid() )
		{
			if ( _auditReadState != "unavailable" ) _remoteAuditVersion++;
			_remoteAuditRows.Clear();
			_auditCoverage = string.Empty;
			_auditReadState = "unavailable";
			return;
		}
		_auditRequestId = System.Guid.NewGuid();
		_auditRequestAge = 0f;
		if ( _auditReadState != "ok" )
		{
			_auditReadState = "loading";
			_remoteAuditVersion++;
		}
		StaffMenuBridgeService.Instance.RequestAuditHost( _auditRequestId );
#endif
	}

#if !LIFEPUNCH_LOCAL
	internal static void OnAuditReceived( System.Guid requestId, string outcome,
		StaffAuditWireRow[] rows, string coverage )
	{
		if ( !EnsureAuditAccess() || requestId == System.Guid.Empty || requestId != _auditRequestId ) return;
		_auditRequestId = System.Guid.Empty;
		_auditRequestAge = 0f;
		_remoteAuditRows.Clear();
		_auditCoverage = string.Empty;
		_auditReadState = outcome is "ok" or "refused" or "unavailable" or "failed" ? outcome : "failed";
		if ( _auditReadState == "ok" )
		{
			try
			{
				if ( rows is null || rows.Length > 500 ) throw new System.ArgumentException( "Invalid audit snapshot" );
				foreach ( var row in rows )
				{
					var when = System.DateTimeOffset.FromUnixTimeMilliseconds( row.WhenUnixMilliseconds );
					_remoteAuditRows.Add( new StaffAuditEntry( FormatAuditWhen( when ), row.Action ?? string.Empty,
						row.ActorName ?? string.Empty, row.ActorSteamId, row.ActorSteamId == 0L ? "Server" : "Player",
						row.Description ?? string.Empty, when ) );
				}
				_auditCoverage = coverage ?? string.Empty;
			}
			catch ( System.Exception )
			{
				_remoteAuditRows.Clear();
				_auditReadState = "failed";
			}
		}
		_remoteAuditVersion++;
	}
#endif

	public static int AuditVersion
	{
		get
		{
			if ( !EnsureAuditAccess() ) return 0;
#if LIFEPUNCH_LOCAL
			return 0;
#elif !LIFEPUNCH_PACKAGE
			if ( Networking.IsHost ) return LocalAuditStore.Version;
#endif
			return _remoteAuditVersion;
		}
	}

	public static string AuditCoverageText => EnsureAuditAccess() ? _auditCoverage : string.Empty;

	// Portal Audit action catalog (dxrp.net/portal/audit Actions dropdown) plus staff writers
	// that already land in the local ring. Unioned with live row actions so a new name appears.
	private static readonly string[] PortalAuditActions =
	{
		"AddLaw", "Advert", "Arrest", "ATM", "Ban", "BulkGiveItems", "BulkRevokeItems",
		"CancelDemote", "Chat", "CoinFlip", "Create", "CustomJob", "Death", "Delete",
		"Demote", "DispatchAction", "DropItem", "Expire", "ForceSellDoor", "ForceRpName",
		"Frame", "Freeze", "Gag", "GenerateToken", "GiveItem", "Hit", "Job", "JobForce",
		"Kick", "Kill", "MayorAnnounce", "MayorTown", "Me", "Minigame", "ModifyBalance",
		"MoneySpawn", "MysteryBoxWin", "PickupItem", "PocketDrop", "PocketPickup",
		"PoliticalPrisoner", "PrivateMessage", "Recycler", "RemoveLaw", "RpName",
		"Sanction", "SetHealth", "Spectate", "Teleport", "Unarrest", "Update",
		"WalletCharge", "WalletDeposit", "Warn", "Waypoint"
	};

	/// <summary>
	/// Locally emitted audit action names missing from the portal catalogue.
	/// To update, inspect the first argument of every ServerApiClient.Audit call, including
	/// conditional names, then subtract <see cref="PortalAuditActions"/>.
	/// Portal names remain available even when this game build has no corresponding emitter.
	/// </summary>
	private static readonly string[] LocalAuditActions =
	{
		"ClearAllEntities", "ClearAllProps", "ClearEntities", "Fake Disconnect",
		"LifePunchBtcPayout", "Lockpick", "PocketView", "SlotMachineCashOut", "SpawnItem",
		"StaffAnnounce", "StaffRequestCreated", "StaffRequestUpdated", "StaffSpawnEntity",
		"StaffSpawnMarket", "StaffTicketClaimed", "StaffTicketResolved", "Status", "TV",
		"UseItem", "Vote", "VoteBet", "VoteDemote", "Wanted", "Warrant"
	};

	/// <summary>
	/// Audit pill tones follow the portal palette: ModifyBalance teal, WalletDeposit maroon
	/// and Chat purple. Other names use a stable hash into the same 12-tone set.
	/// </summary>
	private static readonly string[] AuditActionTones =
	{
		"action-pill-teal", "action-pill-maroon", "action-pill-purple", "action-pill-green",
		"action-pill-orange", "action-pill-blue", "action-pill-rose", "action-pill-olive",
		"action-pill-indigo", "action-pill-rust", "action-pill-slate", "action-pill-gold"
	};

	public static string AuditActionTone( string action )
	{
		if ( string.IsNullOrEmpty( action ) )
		{
			return "action-pill-teal";
		}

		if ( action.Equals( "ModifyBalance", System.StringComparison.OrdinalIgnoreCase ) ) return "action-pill-teal";
		if ( action.Equals( "WalletDeposit", System.StringComparison.OrdinalIgnoreCase ) ) return "action-pill-maroon";
		if ( action.Equals( "Chat", System.StringComparison.OrdinalIgnoreCase ) ) return "action-pill-purple";
		if ( action.Equals( "SetHealth", System.StringComparison.OrdinalIgnoreCase ) ) return "action-pill-blue";
		if ( action.Equals( "Ban", System.StringComparison.OrdinalIgnoreCase ) ) return "action-pill-rose";

		var hash = 0;
		foreach ( var ch in action )
		{
			hash = unchecked( ch + ( hash << 5 ) - hash );
		}

		// Mask, never Math.Abs: the hash above is unchecked, so int.MinValue is reachable and
		// Math.Abs( int.MinValue ) throws OverflowException. This runs once per rendered row AND
		// once per catalog pill, inside the render tree — a throw here takes the whole staff menu
		// down, not just the Audit tab. Masking the sign bit is total and allocation-free.
		return AuditActionTones[( hash & 0x7FFFFFFF ) % AuditActionTones.Length];
	}

	/// <summary>Portal-style player cell: SteamID64, or <c>system</c> when the actor is the server.</summary>
	public static string AuditPlayerLabel( StaffAuditEntry entry )
		=> entry.PlayerSteamId == 0L ? "system" : entry.PlayerSteamId.ToString();

	/// <summary>
	/// Cached newest-first feed shared by the grid, action catalogue and facet counts.
	/// Sharing the snapshot avoids copying the locked ring for each consumer on a rebuild.
	/// </summary>
	private static IReadOnlyList<StaffAuditEntry> AuditSource()
	{
		if ( !EnsureAuditAccess() ) return System.Array.Empty<StaffAuditEntry>();
#if LIFEPUNCH_LOCAL
		return AuditStub();
#else
#if !LIFEPUNCH_PACKAGE
		if ( Networking.IsHost ) return ReadLocalAuditEntries();
#endif
		RefreshAuditSnapshot();
		return _auditReadState == "ok" ? _remoteAuditRows : System.Array.Empty<StaffAuditEntry>();
#endif
	}

	/// <summary>
	/// Action names for the Audit Actions dropdown: the portal catalog, union this build's own
	/// emitters (<see cref="LocalAuditActions"/>), union whatever is actually in the feed, sorted.
	/// The third term keeps a brand-new action name selectable the moment its first row lands.
	/// </summary>
	public static IReadOnlyList<string> AuditActionCatalog()
	{
		var set = new SortedSet<string>( PortalAuditActions, System.StringComparer.OrdinalIgnoreCase );
		foreach ( var name in LocalAuditActions )
		{
			set.Add( name );
		}

		foreach ( var row in AuditSource() )
		{
			if ( !string.IsNullOrWhiteSpace( row.Action ) )
			{
				set.Add( row.Action );
			}
		}

		return set.ToList();
	}

	/// <summary>
	/// Row counts for each action under the other active filters.
	/// Exclude the action filter itself so counts show what selecting each action would return.
	/// </summary>
	public static IReadOnlyDictionary<string, int> AuditActionCounts(
		string playerId, string entityId,
		AuditPlayerScope scope = AuditPlayerScope.Actor, int windowMinutes = 0 )
	{
		var counts = new Dictionary<string, int>( System.StringComparer.OrdinalIgnoreCase );
		foreach ( var entry in FilterAudit( AuditSource(), playerId, entityId, false, null, scope, windowMinutes ) )
		{
			if ( string.IsNullOrWhiteSpace( entry.Action ) )
			{
				continue;
			}

			counts.TryGetValue( entry.Action, out var seen );
			counts[entry.Action] = seen + 1;
		}

		return counts;
	}

	/// <summary>
	/// Rows in the feed before ANY filter. Lets the empty state tell "nothing has happened yet" apart
	/// from "your filters excluded everything", which are the same blank grid but opposite fixes.
	/// </summary>
	public static int AuditFeedTotal() => AuditSource().Count;

	/// <summary>
	/// Oldest stamped row still in the ring, or null when the feed is empty or carries no stamps.
	/// The ring is bounded (<c>LocalAuditStore.Capacity</c>), so a time window wider than this is
	/// answered only as far back as this row — the grid says so rather than implying full coverage.
	/// </summary>
	public static System.DateTimeOffset? AuditOldestUtc()
	{
		System.DateTimeOffset? oldest = null;
		foreach ( var entry in AuditSource() )
		{
			if ( entry.WhenUtc == default )
			{
				continue;
			}

			if ( oldest is null || entry.WhenUtc < oldest.Value )
			{
				oldest = entry.WhenUtc;
			}
		}

		return oldest;
	}

	/// <summary>True only for an authorized local source or a completed remote snapshot.</summary>
	public static bool AuditFeedIsReadableHere
	{
		get
		{
			if ( !EnsureAuditAccess() ) return false;
#if LIFEPUNCH_LOCAL
			return true;
#elif !LIFEPUNCH_PACKAGE
			if ( Networking.IsHost ) return true;
#endif
			return _auditReadState == "ok";
		}
	}

	public static string AuditFeedUnavailableText
	{
		get
		{
			if ( !EnsureAuditAccess() ) return "Requires permission: portal.audit.view.";
#if LIFEPUNCH_PACKAGE
			if ( Networking.IsHost || _auditReadState == "unavailable" )
				return "Audit records are unavailable from this server's current DXRP version.";
#endif
			return _auditReadState switch
			{
				"refused" => "The server denied access to audit records. Requires permission: portal.audit.view.",
				"failed" => "Audit records could not be loaded. Refresh to try again.",
				"unavailable" => "Audit records are unavailable from this server.",
				_ => "Loading audit records from the server..."
			};
		}
	}

	/// <summary>Relative "when" label for a raw stamp, in the same vocabulary the grid's When column uses.</summary>
	public static string AuditWhenLabel( System.DateTimeOffset whenUtc ) => FormatAuditWhen( whenUtc );

	/// <summary>
	/// Newest-first local audit rows filtered by player, action and entity. Player matches
	/// SteamID64 or actor name; populated action sets match exactly and case-insensitively.
	/// The live branch reads LocalAuditStore, fed by ServerApiClient.Audit; no remote audit GET
	/// is connected. The local fixture branch supplies sample rows for UI development.
	/// </summary>
	public static IReadOnlyList<StaffAuditEntry> GetAuditEntries(
		string playerId, string entityId, bool matchDescription = false,
		IReadOnlyCollection<string> actions = null,
		AuditPlayerScope scope = AuditPlayerScope.Actor,
		int windowMinutes = 0 )
		=> FilterAudit( AuditSource(), playerId, entityId, matchDescription, actions, scope, windowMinutes );

#if !LIFEPUNCH_LOCAL && !LIFEPUNCH_PACKAGE
	private static IReadOnlyList<StaffAuditEntry> ReadLocalAuditEntries()
	{
		var rows = LocalAuditStore.SnapshotNewestFirst();
		var mapped = new List<StaffAuditEntry>( rows.Count );
		foreach ( var row in rows )
		{
			mapped.Add( new StaffAuditEntry(
				FormatAuditWhen( row.WhenUtc ),
				row.Action,
				row.ActorName,
				row.ActorSteamId,
				InferAuditEntity( row.ActorSteamId ),
				row.Description,
				row.WhenUtc ) );
		}

		return mapped;
	}

	private static string InferAuditEntity( long actorSteamId )
		=> actorSteamId == 0L ? "Server" : "Player";
#endif

	// Deliberately OUTSIDE the define: the editor stub now renders its When column through the SAME
	// formatter as the live build, so the two cannot drift (the stub used to say "2m ago" where live
	// says "2 minutes ago"). Fully qualified so it needs no import in either build.
	private static string FormatAuditWhen( System.DateTimeOffset whenUtc )
	{
		if ( whenUtc == default ) return "unknown";
		var elapsed = System.DateTimeOffset.UtcNow - whenUtc;
		if ( elapsed.TotalSeconds < 60 ) return "just now";
		var minutes = (int)elapsed.TotalMinutes;
		if ( minutes < 60 ) return minutes <= 1 ? "1 minute ago" : $"{minutes} minutes ago";
		var hours = (int)elapsed.TotalHours;
		if ( hours < 24 ) return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
		if ( elapsed.TotalDays < 2 ) return "Yesterday";
		return whenUtc.ToLocalTime().ToString( "yyyy-MM-dd HH:mm" );
	}

	private static IReadOnlyList<StaffAuditEntry> FilterAudit(
		IReadOnlyList<StaffAuditEntry> source,
		string playerId,
		string entityId,
		bool matchDescription = false,
		IReadOnlyCollection<string> actions = null,
		AuditPlayerScope scope = AuditPlayerScope.Actor,
		int windowMinutes = 0 )
	{
		var player = playerId?.Trim() ?? "";
		var entity = entityId?.Trim() ?? "";
		var selectedActions = new HashSet<string>( System.StringComparer.OrdinalIgnoreCase );
		if ( actions is not null )
		{
			foreach ( var action in actions )
			{
				var normalized = action?.Trim() ?? "";
				if ( normalized.Length > 0 )
				{
					selectedActions.Add( normalized );
				}
			}
		}

		// matchDescription is the profile card's older, narrower spelling of AuditPlayerScope.Both.
		// Keep honouring it so that caller's results are byte-for-byte what they were, while the tab
		// drives the explicit scope. Actor-only remains the default, i.e. the previous tab behaviour.
		var matchActor = scope != AuditPlayerScope.Target;
		var matchTarget = matchDescription || scope != AuditPlayerScope.Actor;

		var cutoff = windowMinutes > 0
			? System.DateTimeOffset.UtcNow.AddMinutes( -windowMinutes )
			: (System.DateTimeOffset?)null;

		return source.Where( e =>
		{
			if ( player.Length > 0 )
			{
				var actorHit = matchActor
				               && ( e.PlayerSteamId.ToString().Contains( player, System.StringComparison.OrdinalIgnoreCase )
				                    || e.Player.Contains( player, System.StringComparison.OrdinalIgnoreCase ) );
				var targetHit = matchTarget
				                && e.Description.Contains( player, System.StringComparison.OrdinalIgnoreCase );
				if ( !actorHit && !targetHit )
				{
					return false;
				}
			}

			// The Entity column is SYNTHESISED ("Server" / "Player"), so any identifier an operator
			// would actually paste — a SteamID64, an item id, a door or waypoint name — exists only
			// inside the description. Matching either is what lets the box do what its label promises;
			// matching Entity alone made it a two-value toggle wearing an id field's placeholder.
			if ( entity.Length > 0
			     && !e.Entity.Contains( entity, System.StringComparison.OrdinalIgnoreCase )
			     && !e.Description.Contains( entity, System.StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}

			if ( selectedActions.Count > 0
			     && !selectedActions.Contains( e.Action ) )
			{
				return false;
			}

			// A row with no stamp is never hidden by a window: we cannot prove it falls outside one.
			if ( cutoff is not null && e.WhenUtc != default && e.WhenUtc < cutoff.Value )
			{
				return false;
			}

			return true;
		} ).ToList();
	}

#if LIFEPUNCH_LOCAL
	// Editor-only audit fixtures cover action types, system actors and Server/Player entities.
	// Relative timestamps are recalculated per call and span every window preset and formatter branch.
	private static IReadOnlyList<StaffAuditEntry> AuditStub()
	{
		var now = System.DateTimeOffset.UtcNow;
		return new List<StaffAuditEntry>
		{
			StubAuditRow( now.AddSeconds( -30 ), "Chat", "system", 0L, "Server", "[System] #system.automessage.rulebreakers" ),
			StubAuditRow( now.AddSeconds( -45 ), "ModifyBalance", "Regular Rick", 1L, "Player", "$6 for Salary" ),
			StubAuditRow( now.AddMinutes( -2 ), "DispatchAction", "Mod Maddie", 6L, "Player", "Kicked Suspicious Sammy — reason: RDM" ),
			StubAuditRow( now.AddMinutes( -14 ), "DispatchAction", "Admin Andy", 3L, "Player", "Banned Regular Rick — 3d, reason: cheating" ),
			StubAuditRow( now.AddMinutes( -38 ), "Chat", "Suspicious Sammy", 2L, "Server", "/advert WTS printers cheap" ),
			StubAuditRow( now.AddHours( -1 ), "Update", "Super Sam", 4L, "Player", "Changed Regular Rick rank → VIP" ),
			StubAuditRow( now.AddHours( -2 ), "ModifyBalance", "Regular Rick", 1L, "Player", "$12 for Salary" ),
			StubAuditRow( now.AddHours( -3 ), "GenerateToken", "Owner Olivia", 5L, "Server", "Generated server automation token" ),
			StubAuditRow( now.AddHours( -30 ), "Update", "Owner Olivia", 5L, "Server", "Pinned gamemode revision dxura.rp@latest" )
		};
	}

	private static StaffAuditEntry StubAuditRow(
		System.DateTimeOffset whenUtc, string action, string player, long steamId, string entity, string description )
		=> new( FormatAuditWhen( whenUtc ), action, player, steamId, entity, description, whenUtc );
#endif

	// --- Waypoints (admin teleport bookmarks) -----------------------------

	/// <summary>
	/// Permission Ids gating the in-menu Waypoints panel, matching DXRP's
	/// <c>Dxura.RP.Game.Commands.WaypointCommand</c>: <c>command.waypoint.use</c> lists + teleports,
	/// <c>command.waypoint.edit</c> sets + clears. UX gating only — <c>/waypoint</c> re-checks host-side.
	/// </summary>
	public const string WaypointUsePermissionId = "command.waypoint.use";

	public const string WaypointEditPermissionId = "command.waypoint.edit";

	public static bool CanUseWaypoints() => CanView( WaypointUsePermissionId );

	public static bool CanEditWaypoints() => CanView( WaypointEditPermissionId );

	// The registered chat command every op routes through (mirrors WaypointCommand.Command). Going
	// through /waypoint keeps the host owning validation, the portal store write and the audit entry —
	// the menu stays a thin dispatch layer and never touches the store directly.
	private const string WaypointCommandName = "waypoint";

	/// <summary>
	/// Waypoint cache revision used by Razor BuildHash.
	/// </summary>
	public static int WaypointVersion { get; private set; }

#if LIFEPUNCH_LOCAL
	// Editor build: a live in-memory list so set/clear visibly update the panel with no backend.
	private static readonly List<string> _waypoints = new() { "bank", "nlr cave", "pd", "spawn" };
	public static bool WaypointsReadIsUncertain => false;
#else
	// Waypoint reads go through StaffMenuBridgeService because the store uses the host
	// server token. Responses update the client cache through OnWaypointsReceived.
	private static readonly List<string> _waypoints = new();
	public static bool WaypointsReadIsUncertain { get; private set; } = true;
	private static System.Guid _waypointRequestId;
#endif

	/// <summary>Saved waypoint names (alphabetical). Editor: live stub; server: host-synced via RefreshWaypoints.</summary>
	public static IReadOnlyList<string> GetWaypoints()
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
#endif
		if ( !CanUseWaypoints() )
		{
#if !LIFEPUNCH_LOCAL
			if ( _waypoints.Count != 0 ) WaypointVersion++;
			_waypoints.Clear();
			_waypointRequestId = System.Guid.Empty;
			WaypointsReadIsUncertain = true;
#endif
			return System.Array.Empty<string>();
		}
		return _waypoints;
	}

	/// <summary>
	/// Request the host's token-scoped waypoint list. The local fixture build is already in memory.
	/// </summary>
	public static void RefreshWaypoints()
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
		if ( !CanUseWaypoints() ) { GetWaypoints(); return; }
		// Routes through the addon bridge (StaffMenuBridgeService), not DXRP core.
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			_waypointRequestId = System.Guid.NewGuid();
			StaffMenuBridgeService.Instance.RequestWaypointsHost( _waypointRequestId );
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Apply a correlated waypoint response and bump the cache revision for Razor.
	/// </summary>
	internal static void OnWaypointsReceived( System.Guid requestId, string[] names, bool authoritative )
	{
		EnsureServerScope();
		if ( !CanUseWaypoints() ) { GetWaypoints(); return; }
		if ( requestId == System.Guid.Empty || requestId != _waypointRequestId )
		{
			return;
		}

		if ( !authoritative )
		{
			WaypointsReadIsUncertain = true;
			WaypointVersion++;
			return;
		}

		_waypoints.Clear();
		if ( names != null )
		{
			_waypoints.AddRange( names );
		}

		WaypointsReadIsUncertain = false;
		WaypointVersion++;
	}

	// A /waypoint set|clear store write is async host-side; wait briefly, then re-read so the panel
	// reflects the change. The tab also refreshes on open, so this is a best-effort immediate update.
	private static async System.Threading.Tasks.Task RefreshAfterWrite()
	{
		await GameTask.DelayRealtimeSeconds( 0.4f );
		RefreshWaypoints();
	}
#endif

	/// <summary>Teleport the caller to a saved waypoint (host re-checks <c>command.waypoint.use</c>).</summary>
	public static void GoToWaypoint( string name ) => DispatchWaypoint( "go", name );

	/// <summary>Save a waypoint at the caller's position + aim (host re-checks <c>command.waypoint.edit</c>).</summary>
	public static void SetWaypoint( string name ) => DispatchWaypoint( "set", name );

	/// <summary>Delete a saved waypoint (host re-checks <c>command.waypoint.edit</c>).</summary>
	public static void ClearWaypoint( string name ) => DispatchWaypoint( "clear", name );

	private static void DispatchWaypoint( string op, string name )
	{
		if ( op == "go" ? !CanUseWaypoints() : !CanEditWaypoints() ) return;
		name = name?.Trim() ?? "";
		if ( name.Length == 0 )
		{
			return;
		}

#if LIFEPUNCH_LOCAL
		var norm = name.ToLowerInvariant();
		switch ( op )
		{
			case "set" when !_waypoints.Contains( norm ):
				_waypoints.Add( norm );
				_waypoints.Sort( System.StringComparer.OrdinalIgnoreCase );
				WaypointVersion++;
				break;
			case "clear" when _waypoints.Remove( norm ):
				WaypointVersion++;
				break;
		}

		Log.Info( $"[lifepunchulx] (local stub) waypoint {op} '{name}'" );
#else
		// /waypoint grammar: "set <name>", "clear <name>", or a bare <name> to teleport.
		var argv = op switch
		{
			"set" => new[] { "set", name },
			"clear" => new[] { "clear", name },
			_ => new[] { name }
		};

		Chat.Current?.ExecuteCommandHost( WaypointCommandName, argv );

		if ( op is "set" or "clear" )
		{
			_ = RefreshAfterWrite();
		}
#endif
	}

	public static bool IsStaffMember( long steamId )
	{
#if LIFEPUNCH_LOCAL
		return OnlinePlayers().Any( player => player.SteamId == steamId && player.GroupName != NonStaffGroup );
#else
		return IsStaff( steamId );
#endif
	}

#if !LIFEPUNCH_LOCAL
	// Group players as staff when they hold a permission used by a catalogue action.
	// Other ranks, including donor ranks without staff permissions, remain under Players.
	// Groups use the host-synced portal rank name.
	private static bool IsStaff( long steamId )
		=> StaffMenuActions.All.Any( action => RankSystem.HasPermission( steamId, action.PermissionId ) );

	private static string RankName( long steamId )
	{
		var name = RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankName( steamId ) : "";
		name = SanitizeRankName( name );
		return string.IsNullOrWhiteSpace( name ) ? NonStaffGroup : name;
	}

	/// <summary>
	/// Remove control, format, private-use and surrogate characters from backend rank names,
	/// then trim. Preserve ordinary letters, digits, spaces and punctuation for display and group matching.
	/// </summary>
	private static string SanitizeRankName( string raw )
	{
		if ( string.IsNullOrEmpty( raw ) )
		{
			return "";
		}

		var sb = new System.Text.StringBuilder( raw.Length );
		foreach ( var c in raw )
		{
			if ( char.IsControl( c ) || char.IsSurrogate( c ) )
			{
				continue;
			}

			var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory( c );
			if ( category is System.Globalization.UnicodeCategory.Format
			    or System.Globalization.UnicodeCategory.PrivateUse
			    or System.Globalization.UnicodeCategory.OtherNotAssigned )
			{
				continue;
			}

			sb.Append( c );
		}

		return sb.ToString().Trim();
	}

	private static int RankOrder( long steamId )
		=> RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankOrder( steamId ) : 0;

	/// <summary>The player's real, sanitised rank name for the detail pane ("—" when they have none).</summary>
	private static string RealRole( long steamId )
	{
		var name = RankSystem.Instance.IsValid() ? SanitizeRankName( RankSystem.Instance.GetRankName( steamId ) ) : "";
		return string.IsNullOrWhiteSpace( name ) ? "—" : name;
	}

	/// <summary>The player's rank colour as a "#RRGGBB" string for the detail pane.</summary>
	private static string RankColorHex( long steamId )
	{
		var color = RankSystem.Instance.IsValid() ? RankSystem.Instance.GetRankColor( steamId ) : 0xFFFFFFu;
		return $"#{color & 0xFFFFFFu:X6}";
	}
#endif

	// --- Settings (owner customizations) ----------------------------------

	/// <summary>
	/// Owner-grant permission gating edits to the menu's owner customizations (currently the network
	/// website link). The Owner rank's <c>"*"</c> wildcard satisfies it automatically; an owner may also
	/// grant <c>lifepunchulx.settings.edit</c> to other ranks in the portal. UX gating only — the host
	/// (<see cref="StaffMenuBridgeService"/>) re-checks every write.
	/// </summary>
	public const string SettingsEditPermissionId = "lifepunchulx.settings.edit";

	/// <summary>True if the local viewer may edit owner settings. UX gating only; host re-checks the write.</summary>
	public static bool CanEditSettings() => CanView( SettingsEditPermissionId );

	/// <summary>Bumped whenever cached settings change, so the razor's <c>BuildHash</c> re-renders.</summary>
	public static int SettingsVersion { get; private set; }
	public static bool WebsiteWriteIsPending { get; private set; }
	public static bool WebsiteWriteSucceeded { get; private set; }
	public static string WebsiteWriteMessage { get; private set; } = string.Empty;
	public static int WebsiteWriteVersion { get; private set; }

#if LIFEPUNCH_LOCAL
	// Editor build: a live in-memory value so the input + click-to-copy work with no backend.
	private static string _websiteUrl = "https://lifepunch.co";
	public static bool WebsiteReadIsUncertain => false;
#else
	// dxrp.net build: host-synced from the token-scoped store via StaffMenuBridgeService (RefreshSettings).
	private static string _websiteUrl = string.Empty;
	public static bool WebsiteReadIsUncertain { get; private set; } = true;
	private static System.Guid _settingsRequestId;
	private static System.Guid _websiteWriteRequestId;
	private static string _serverScopeKey = string.Empty;
#endif

	/// <summary>The owner-configured network website URL ("" when unset). Editor: stub; server: host-synced.</summary>
	public static string WebsiteUrl
	{
		get
		{
#if !LIFEPUNCH_LOCAL
			EnsureServerScope();
#endif
			return _websiteUrl;
		}
	}

	/// <summary>True when a website URL is configured — the network tag then becomes a click-to-copy link.</summary>
	public static bool HasWebsite => !string.IsNullOrWhiteSpace( WebsiteUrl );

#if !LIFEPUNCH_LOCAL
	private static void EnsureServerScope()
	{
		var sceneId = Game.ActiveScene?.Id ?? System.Guid.Empty;
		var hostConnectionId = Connection.Host?.Id.ToString() ?? "none";
		var scope = $"{sceneId:N}|{hostConnectionId}|{LocalSteamId}|{Networking.ServerName ?? string.Empty}";
		if ( string.Equals( scope, _serverScopeKey, System.StringComparison.Ordinal ) )
		{
			return;
		}

		_serverScopeKey = scope;
		ClearAuditView();
		_waypoints.Clear();
		_websiteUrl = string.Empty;
		_waypointRequestId = System.Guid.Empty;
		_settingsRequestId = System.Guid.Empty;
		_websiteWriteRequestId = System.Guid.Empty;
		ClearInventoryView();
		ClearSanctionsView();
		WaypointsReadIsUncertain = true;
		WebsiteReadIsUncertain = true;
		WebsiteWriteIsPending = false;
		WebsiteWriteSucceeded = false;
		WebsiteWriteMessage = string.Empty;
		WaypointVersion++;
		SettingsVersion++;
		WebsiteWriteVersion++;
	}
#endif

	/// <summary>
	/// Lowercase network slug shown in the header chip — derived from <see cref="WebsiteUrl"/>
	/// (e.g. <c>https://dxrp.net/</c> → <c>dxrp</c>). Neutral <c>network</c> when unset.
	/// </summary>
	public static string NetworkIdentifier => DeriveNetworkIdentifier( WebsiteUrl );

	static string DeriveNetworkIdentifier( string url )
	{
		if ( string.IsNullOrWhiteSpace( url ) )
		{
			return "network";
		}

		var trimmed = url.Trim();
		if ( !trimmed.Contains( "://" ) )
		{
			trimmed = "https://" + trimmed;
		}

		var host = ParseUrlHost( trimmed );
		if ( string.IsNullOrWhiteSpace( host ) )
		{
			return "network";
		}

		host = host.ToLowerInvariant();
		if ( host.StartsWith( "www." ) )
		{
			host = host[4..];
		}

		var dot = host.IndexOf( '.' );
		var slug = dot > 0 ? host[..dot] : host;
		return string.IsNullOrWhiteSpace( slug ) ? "network" : slug;
	}

	static string ParseUrlHost( string url )
	{
		var schemeEnd = url.IndexOf( "://" );
		if ( schemeEnd < 0 )
			return null;

		var rest = url[(schemeEnd + 3)..];
		var pathStart = rest.IndexOfAny( new[] { '/', '?', '#', ':' } );
		var hostPort = pathStart >= 0 ? rest[..pathStart] : rest;
		if ( string.IsNullOrWhiteSpace( hostPort ) )
			return null;

		var colon = hostPort.LastIndexOf( ':' );
		if ( colon > 0 && !hostPort.StartsWith( "[" ) )
			hostPort = hostPort[..colon];

		return hostPort;
	}

	/// <summary>Ask the host for the current owner settings (mirrors <see cref="RefreshWaypoints"/>). No-op in editor.</summary>
	public static void RefreshSettings()
	{
#if !LIFEPUNCH_LOCAL
		EnsureServerScope();
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			_settingsRequestId = System.Guid.NewGuid();
			StaffMenuBridgeService.Instance.RequestSettingsHost( _settingsRequestId );
		}
#endif
	}

	/// <summary>Save the network website URL (host re-checks the owner grant). An empty value clears it.</summary>
	public static void SaveWebsite( string url )
	{
#if LIFEPUNCH_LOCAL
		_websiteUrl = ( url ?? string.Empty ).Trim();
		WebsiteWriteIsPending = false;
		WebsiteWriteSucceeded = true;
		WebsiteWriteMessage = "Applied to the local editor preview only.";
		SettingsVersion++;
		WebsiteWriteVersion++;
		Log.Info( $"[lifepunchulx] (local stub) website set '{_websiteUrl}'" );
#else
		EnsureServerScope();
		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			_websiteWriteRequestId = System.Guid.NewGuid();
			WebsiteWriteIsPending = true;
			WebsiteWriteSucceeded = false;
			WebsiteWriteMessage = "Saving through the server API...";
			WebsiteWriteVersion++;
			StaffMenuBridgeService.Instance.SetWebsiteHost( _websiteWriteRequestId, url ?? string.Empty );
		}
		else
		{
			WebsiteWriteIsPending = false;
			WebsiteWriteSucceeded = false;
			WebsiteWriteMessage = "Not saved: the host settings bridge is unavailable.";
			WebsiteWriteVersion++;
		}
#endif
	}

	private static StaffMoneyGrantState _moneyGrantState;
	private static TimeSince _moneyGrantPendingAge;
	public static StaffMoneyGrantState MoneyGrantState
	{
		get
		{
			if ( _moneyGrantState == StaffMoneyGrantState.Pending && _moneyGrantPendingAge > 15f )
			{
				_moneyGrantState = StaffMoneyGrantState.Unknown;
				MoneyGrantMessage = "Host confirmation is delayed. Do not retry; a late result will still reconcile here.";
				MoneyGrantVersion++;
			}

			return _moneyGrantState;
		}
		private set
		{
			_moneyGrantState = value;
			if ( value == StaffMoneyGrantState.Pending )
			{
				_moneyGrantPendingAge = 0;
			}
		}
	}

	/// <summary>
	/// The panel owns the immutable subject/form snapshot for an in-flight money request. Keep it
	/// mounted until the host returns a terminal result. An unknown outcome cannot be unlocked locally.
	/// </summary>
	public static bool MoneyGrantBlocksMenuClose =>
		MoneyGrantState is StaffMoneyGrantState.Pending or StaffMoneyGrantState.Unknown;
	public static string MoneyGrantMessage { get; private set; } = string.Empty;
	public static int MoneyGrantVersion { get; private set; }
	private static System.Guid _moneyGrantRequestId;

	public static void ClearMoneyGrantResult()
	{
		if ( MoneyGrantState is StaffMoneyGrantState.Pending or StaffMoneyGrantState.Unknown )
		{
			return;
		}

		_moneyGrantRequestId = System.Guid.Empty;
		MoneyGrantState = StaffMoneyGrantState.Idle;
		MoneyGrantMessage = string.Empty;
		MoneyGrantVersion++;
	}

	/// <summary>
	/// Query the host's replay cache after a delayed result. This never re-executes a missing
	/// request, so an uncertain prior grant cannot become a duplicate grant.
	/// </summary>
	public static void ReconcileMoneyGrant()
	{
		if ( MoneyGrantState != StaffMoneyGrantState.Unknown
		     || _moneyGrantRequestId == System.Guid.Empty )
		{
			return;
		}

#if !LIFEPUNCH_LOCAL
		if ( !StaffMenuBridgeService.Instance.IsValid() )
		{
			MoneyGrantMessage = "The host bridge is unavailable. The original request remains locked for safety.";
			MoneyGrantVersion++;
			return;
		}

		MoneyGrantMessage = "Checking the original host result…";
		MoneyGrantVersion++;
		StaffMenuBridgeService.Instance.QueryMoneyGrantResultHost( _moneyGrantRequestId );
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Host→client callback (invoked by <see cref="StaffMenuBridgeService"/>): replace the
	/// cached website with the server's stored value and bump the version so the razor re-renders.
	/// </summary>
	internal static void OnSettingsReceived( System.Guid requestId, string website, bool authoritative )
	{
		EnsureServerScope();
		if ( requestId == System.Guid.Empty )
		{
			if ( !authoritative )
			{
				return;
			}

			// A confirmed write supersedes every older read still in flight.
			_settingsRequestId = System.Guid.NewGuid();
		}
		else if ( requestId != _settingsRequestId )
		{
			return;
		}

		if ( authoritative )
		{
			_websiteUrl = website ?? string.Empty;
			WebsiteReadIsUncertain = false;
		}
		else
		{
			WebsiteReadIsUncertain = true;
		}

		SettingsVersion++;
	}

	internal static void OnWebsiteWriteResult( System.Guid requestId, bool succeeded, string message )
	{
		if ( requestId == System.Guid.Empty || requestId != _websiteWriteRequestId )
		{
			return;
		}

		WebsiteWriteIsPending = false;
		WebsiteWriteSucceeded = succeeded;
		WebsiteWriteMessage = message ?? string.Empty;
		WebsiteWriteVersion++;
	}

	internal static void OnMoneyGrantResult( System.Guid requestId, bool succeeded, string message )
	{
		if ( requestId == System.Guid.Empty || requestId != _moneyGrantRequestId )
		{
			return;
		}

		MoneyGrantState = succeeded ? StaffMoneyGrantState.Succeeded : StaffMoneyGrantState.Rejected;
		MoneyGrantMessage = message ?? string.Empty;
		MoneyGrantVersion++;
	}

	internal static void OnMoneyGrantReconcileState( System.Guid requestId, bool inFlight, string message )
	{
		if ( requestId == System.Guid.Empty || requestId != _moneyGrantRequestId )
		{
			return;
		}

		MoneyGrantState = inFlight ? StaffMoneyGrantState.Pending : StaffMoneyGrantState.Unknown;
		MoneyGrantMessage = message ?? string.Empty;
		MoneyGrantVersion++;
	}
#endif

	// --- Jobs (force-set via DXRP /job — menu picker reads live gamemode config) ----

	/// <summary>Portal permission for force-setting jobs (<c>/job &lt;player&gt; &lt;job&gt;</c>).</summary>
	public const string JobManagePermissionId = "command.job.manage";

	/// <summary>
	/// Jobs from the active gamemode config — each server's custom job list, sorted for the Set Job picker.
	/// Force-set dispatches to DXRP's native <c>/job</c> command (host re-validates permission).
	/// </summary>
	public static IReadOnlyList<StaffJobOption> AssignableJobs()
	{
#if LIFEPUNCH_LOCAL
		return new List<StaffJobOption>
		{
			new( "Citizen", "Citizen", "#FFFFFF" ),
			new( "Police", "Police Officer", "#3498DB" ),
			new( "Mayor", "Mayor", "#E74C3C" ),
			new( "Gun Dealer", "Gun Dealer", "#F39C12" ),
			new( "Bitcoin Miner", "Bitcoin Miner", "#F1C40F" )
		};
#else
		return GameModeJobs.All
			.OrderBy( job => job.DisplayName() )
			.Select( job => new StaffJobOption(
				job.Name,
				job.DisplayName(),
				$"#{job.Color & 0xFFFFFFu:X6}" ) )
			.ToList();
#endif
	}

	/// <summary>Read the native local command instance; null means this session has no supported reader.</summary>
	private static bool? GetLocalXrayState()
	{
#if LIFEPUNCH_LOCAL
		return null;
#else
		var chat = Chat.Current;
		if ( !Player.Local.IsValid() || chat is null
			|| !chat.TryGetCommand( "xray", out var command )
			|| command is not Dxura.RP.Game.Commands.XrayCommand xray )
		{
			return null;
		}

		return xray.IsActive;
#endif
	}

	/// <summary>Use the registered local command and the server's grants, never a rank-name threshold.</summary>
	public static bool CanUseXray()
	{
#if LIFEPUNCH_LOCAL
		return true;
#else
		var chat = Chat.Current;
		var player = Player.Local;
		return player.IsValid() && chat is not null && CanView( "command.xray" )
			&& chat.TryGetCommand( "xray", out var command ) && command is not null
			&& chat.CanAccessCommand( player, command );
#endif
	}

	// --- Dispatch ----------------------------------------------------------

	/// <summary>
	/// Dispatch an action to DXRP's backend. AdminSystem RPC where one exists, else the registered
	/// chat command. No-op-safe in the local build (logs instead).
	/// </summary>
	public static void Dispatch( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args,
		bool? expectedToggleState = null )
	{
#if LIFEPUNCH_LOCAL
		var argText = string.Join( ", ", args.Select( kv => $"{kv.Key}={kv.Value}" ) );
		Log.Info( $"[lifepunchulx] (local stub) {action.Key} target={targetSteamId} [{argText}]" );
#else
		switch ( action.Dispatch )
		{
			case StaffDispatchKind.AdminRpc:
				DispatchAdminRpc( action, targetSteamId, args );
				break;
			case StaffDispatchKind.ChatCommand:
				if ( IsGuardedStatusToggle( action.Key ) )
				{
					if ( !expectedToggleState.HasValue || !StaffMenuBridgeService.Instance.IsValid() )
					{
						if ( Player.Local.IsValid() )
							Player.Local.SendMessage( "Toggle unavailable: current state or host bridge is unavailable. No change was made." );
						break;
					}

					StaffMenuBridgeService.Instance.SetStatusToggleHost( action.Key, targetSteamId,
						expectedToggleState.Value, !expectedToggleState.Value );
					break;
				}
				DispatchChatCommand( action, targetSteamId, args );
				break;
			case StaffDispatchKind.LocalToggle:
				DispatchLocalToggle( action );
				break;
			case StaffDispatchKind.LocalCommand:
				if ( action.Key == "xray" && action.DispatchTarget == "xray" && CanUseXray() )
				{
					// Consumed is not an enabled-state receipt; the native command owns state and feedback.
					if ( !Chat.Current.TryExecuteLocalCommand( "/xray" ) )
					{
						Player.Local.SendMessage( "X-ray is unavailable in this session." );
					}
				}
				break;
			case StaffDispatchKind.GiveMoney:
				DispatchGiveMoney( targetSteamId, args );
				break;
		}
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static void DispatchAdminRpc( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
		if ( !AdminSystem.Instance.IsValid() )
		{
			return;
		}

		switch ( action.DispatchTarget )
		{
			case "kick":
				if ( !args.TryGetValue( "reason", out var reason ) || string.IsNullOrWhiteSpace( reason ) )
				{
					return;
				}

				AdminSystem.Instance.KickPlayerHost( targetSteamId, reason );
				break;
			case "screenshot":
				AdminSystem.Instance.ForceScreenshotHost( targetSteamId );
				break;
		}
	}

	/// <summary>
	/// Client-only affordances DXRP exposes via keybind rather than a command. Noclip is a synced
	/// MoveMode toggled on the local player's own controller (same path the "Noclip" bind drives),
	/// gated by <c>ability.noclip</c> — we re-use the engine mechanism rather than reimplementing it.
	/// </summary>
	private static void DispatchLocalToggle( StaffAction action )
	{
		switch ( action.DispatchTarget )
		{
			case "noclip":
				if ( !Config.Current.Game.NoClip && !RankSystem.HasLocalPermission( Dxura.RP.Shared.Permission.Noclip ) )
				{
					return;
				}

				if ( !Player.Local.IsValid() || !Player.Local.Controller.IsValid() )
				{
					return;
				}

				var noclip = Player.Local.Controller.Components.Get<MoveModeNoClip>();
				if ( noclip.IsValid() )
				{
					noclip.IsNoclipping = !noclip.IsNoclipping;
				}

				break;
		}
	}

	/// <summary>
	/// Hand a currency grant to the host. The client parses its own form here, but every value is
	/// re-validated host-side -- this parse is for the UI's benefit, never for authority.
	/// </summary>
	private static void DispatchGiveMoney( long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
		if ( MoneyGrantState is StaffMoneyGrantState.Pending or StaffMoneyGrantState.Succeeded or StaffMoneyGrantState.Unknown )
		{
			return;
		}

#if !LIFEPUNCH_LOCAL
		args.TryGetValue( "amount", out var rawAmount );
		args.TryGetValue( "reason", out var reason );
		args.TryGetValue( "destination", out var destination );

		if ( !uint.TryParse( rawAmount?.Trim(), out var amount ) || amount == 0 || amount > int.MaxValue
		     || destination is not ("cash" or "bank") || string.IsNullOrWhiteSpace( reason ) )
		{
			_moneyGrantRequestId = System.Guid.Empty;
			MoneyGrantState = StaffMoneyGrantState.Rejected;
			MoneyGrantMessage = "Grant rejected: check the amount, destination, and audit reason.";
			MoneyGrantVersion++;
			return;
		}

		if ( StaffMenuBridgeService.Instance.IsValid() )
		{
			_moneyGrantRequestId = System.Guid.NewGuid();
			MoneyGrantState = StaffMoneyGrantState.Pending;
			MoneyGrantMessage = "Awaiting host confirmation…";
			MoneyGrantVersion++;
			StaffMenuBridgeService.Instance.GiveMoneyHost(
				_moneyGrantRequestId, targetSteamId, amount, destination == "bank", reason.Trim() );
		}
		else
		{
			_moneyGrantRequestId = System.Guid.Empty;
			MoneyGrantState = StaffMoneyGrantState.Rejected;
			MoneyGrantMessage = "Grant rejected: the host bridge is unavailable.";
			MoneyGrantVersion++;
		}
#else
		_moneyGrantRequestId = System.Guid.Empty;
		MoneyGrantState = StaffMoneyGrantState.Rejected;
		MoneyGrantMessage = "Preview only: no live host grant was sent.";
		MoneyGrantVersion++;
#endif
	}

	private static void DispatchChatCommand( StaffAction action, long targetSteamId, IReadOnlyDictionary<string, string> args )
	{
		var argv = new List<string>();

		// Target identifier first, as a Steam ID string (CommandHelper.ResolvePlayer accepts it,
		// avoiding name-collision ambiguity). Self-only commands carry no target.
		if ( action.TargetMode == StaffActionTarget.OtherPlayer )
		{
			argv.Add( targetSteamId.ToString() );
		}

		foreach ( var arg in action.Args )
		{
			if ( args.TryGetValue( arg.Name, out var value ) && !string.IsNullOrWhiteSpace( value ) )
			{
				argv.Add( value );
			}
		}

		Chat.Current?.ExecuteCommandHost( action.DispatchTarget, argv.ToArray() );
	}
#endif
}

#if !LIFEPUNCH_LOCAL
/// <summary>
/// Registers <c>/lifepunchulx</c>, <c>/menu</c>, and <c>/ulx</c> as in-game chat commands.
/// <see cref="ExecuteLocal"/> opens the menu client-side and consumes the command, so it never round-trips to the host.
/// Discovered automatically via TypeLibrary on the dxrp.net gamemode build.
/// </summary>
public sealed class StaffMenuChatCommand : ICommand
{
	public string Command => "lifepunchulx";
	public string[] Aliases => ["menu", "ulx"];
	public string Help => "Open the staff admin menu.";
	public bool IsUsableWhileDead => true;

	public bool ExecuteLocal( string[] args, string raw )
	{
		StaffMenuHost.Toggle();
		return true;
	}

	public bool ExecuteHost( Player caller, string[] args, string raw ) => true;
}
#endif
