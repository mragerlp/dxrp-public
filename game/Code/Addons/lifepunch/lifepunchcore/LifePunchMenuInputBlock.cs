// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch)
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// While a LifePunch menu is open, block all combat / equipment / hands input so UI clicks
/// cannot fire weapons, grab props, or USE world entities. Panels call
/// <see cref="NotifyMenuOpened"/> / <see cref="NotifyMenuClosed"/> from <c>OnEnabled</c> /
/// <c>OnDisabled</c>. A scene guard clears DXRP input actions every Update + FixedUpdate and
/// holsters the local player's weapon on first open.
/// </summary>
public static class LifePunchMenuInputBlock
{
	private static int _openDepth;

#if !LIFEPUNCH_LOCAL
	private static bool _playerCombatLocked;
	private static bool _savedCantSwitch;
#endif

	// DXRP mixes PascalCase (InputWeaponComponent) and lowercase (HandsEquipment, ShootWeapon).
	private static readonly string[] SuppressedActions =
	{
		"Attack1",
		"attack1",
		"Attack2",
		"attack2",
		"Attack3",
		"attack3",
		"Use",
		"use",
		"Reload",
		"reload",
		"Pocket",
		"pocket",
		"Drop",
		"drop",
		"Slot1",
		"slot1",
		"Slot2",
		"slot2",
		"Slot3",
		"slot3",
		"Slot4",
		"slot4",
		"Slot5",
		"slot5",
		"SlotNext",
		"SlotPrev",
		"slotnext",
		"slotprev"
	};

	public static bool IsAnyMenuOpen => _openDepth > 0;

	private static int _escapeClaims;
	private static bool _escapePending;

	// ESC-FIX-2 Phase 1: the Esc trace. One Esc that closes a LifePunch panel can still open DXRP's pause
	// menu (ESC-PROOF-1 P2), in two shapes: routed-then-pause, and never routed at all. The trace logs every
	// frame the router sees Esc, every pause menu open/close, and every claim and menu count change, so the
	// fix is chosen from data. Reason: diagnose the Esc / pause menu leak. Who: anyone, on their own client;
	// it only writes log lines. Off by default.
	[ConVar( "lp_esc_trace" )]
	public static bool EscTrace { get; set; }

	/// <summary>Positive code-string ID for the Esc trace. Exists nowhere else in the tree.</summary>
	public const string EscTraceMark = "LP_ESC_TRACE_20261019";

	private static int _frame;
	private static bool _routerSawEsc;
#if !LIFEPUNCH_LOCAL
	private static bool _pauseWasOpen;
#endif

	private static void Trace( string what )
	{
		if ( !EscTrace )
			return;

		Log.Info( "[lpcore] " + EscTraceMark + " f=" + _frame + " t=" + RealTime.Now.ToString( "0.000" ) + " " + what
			+ " claims=" + _escapeClaims + " depth=" + _openDepth + " pending=" + _escapePending );
	}

	/// <summary>True while at least one open panel closes itself on Esc.</summary>
	public static bool IsEscapeClaimed => _escapeClaims > 0;

	/// <summary>
	/// A panel that closes on Esc calls this from <c>OnEnabled</c>, paired with
	/// <see cref="ReleaseEscape"/> from <c>OnDisabled</c>. While a claim stands,
	/// <see cref="LifePunchEscapeRouter"/> takes the Esc press at the start of the scene
	/// update, before DXRP's PauseMenu reads it, and holds it for <see cref="TakeEscape"/>.
	/// Panels that do not claim keep the engine default (Esc opens the pause menu).
	/// </summary>
	public static void ClaimEscape()
	{
		_escapeClaims++;
		Trace( "claim" );
	}

	public static void ReleaseEscape()
	{
		if ( _escapeClaims > 0 )
			_escapeClaims--;

		if ( _escapeClaims == 0 )
			_escapePending = false;

		Trace( "release" );
	}

	/// <summary>True once per routed Esc press: the first claiming panel to ask consumes it.</summary>
	public static bool TakeEscape()
	{
		if ( !_escapePending )
			return false;

		_escapePending = false;
		Trace( "taken by a panel" );
		return true;
	}

	internal static void RouteEscape()
	{
		_frame++;
		_routerSawEsc = Input.EscapePressed;
		if ( _routerSawEsc )
			Trace( "esc raised at StartUpdate" );

		if ( _escapeClaims <= 0 || !Input.EscapePressed )
			return;

		Input.EscapePressed = false;
		_escapePending = true;
		Log.Info( "[lpcore] ESC-CONFLICT-1 esc routed to a LifePunch panel claims=" + _escapeClaims );
	}

	/// <summary>
	/// End of frame, before the drop: logs a pause menu open/close and whether the router saw an Esc this
	/// frame (opened with routerSawEsc=False = a press raised after StartUpdate), and an Esc still raised.
	/// </summary>
	internal static void TraceFrameEnd()
	{
#if !LIFEPUNCH_LOCAL
		var pauseOpen = Dxura.RP.Game.UI.PauseMenu.IsOpen;
		if ( pauseOpen != _pauseWasOpen )
		{
			_pauseWasOpen = pauseOpen;
			Trace( "pause menu " + ( pauseOpen ? "OPENED" : "closed" ) + " routerSawEsc=" + _routerSawEsc );
		}
#endif

		if ( Input.EscapePressed )
			Trace( "esc still raised at FinishUpdate" );
	}

	internal static void DropUntakenEscape()
	{
		if ( _escapePending )
			Trace( "routed esc dropped, no panel took it" );

		_escapePending = false;
	}

	public static void NotifyMenuOpened()
	{
		_openDepth++;
		if ( _openDepth == 1 )
			AcquirePlayerCombatLock();

		LifePunchMenuInputGuard.Ensure( Game.ActiveScene );
		Apply();
		Trace( "menu opened" );
	}

	public static void NotifyMenuClosed()
	{
		if ( _openDepth <= 0 )
		{
			Trace( "menu closed with depth already 0" );
			return;
		}

		_openDepth--;
		if ( _openDepth == 0 )
			ReleasePlayerCombatLock();

		Trace( "menu closed" );
	}

	public static void Apply()
	{
		if ( !IsAnyMenuOpen )
			return;

		SuppressGameplayInput();
		ReassertPlayerCombatLock();
	}

	private static void SuppressGameplayInput()
	{
		foreach ( var action in SuppressedActions )
		{
			Input.SetAction( action, false );
			Input.Clear( action );
			Input.ReleaseAction( action );
		}
	}

	private static void AcquirePlayerCombatLock()
	{
#if !LIFEPUNCH_LOCAL
		var player = Player.Local;
		if ( !player.IsValid() || _playerCombatLocked )
			return;

		_savedCantSwitch = player.CantSwitch;
		player.CantSwitch = true;
		player.Holster();
		player.LockCamera = true;
		_playerCombatLocked = true;
#endif
	}

	private static void ReleasePlayerCombatLock()
	{
#if !LIFEPUNCH_LOCAL
		if ( !_playerCombatLocked )
			return;

		var player = Player.Local;
		if ( player.IsValid() )
		{
			player.CantSwitch = _savedCantSwitch;
			player.LockCamera = false;
		}

		_playerCombatLocked = false;
#endif
	}

	private static void ReassertPlayerCombatLock()
	{
#if !LIFEPUNCH_LOCAL
		if ( !_playerCombatLocked )
			return;

		var player = Player.Local;
		if ( !player.IsValid() )
			return;

		player.CantSwitch = true;
		player.LockCamera = true;
#endif
	}
}

/// <summary>
/// Runs input suppression before equipment reads attack / use each frame.
/// </summary>
internal sealed class LifePunchMenuInputGuard : Component
{
	private const string GuardObjectName = "LifePunchMenuInputGuard";

	protected override void OnUpdate()
	{
		LifePunchMenuInputBlock.Apply();
	}

	protected override void OnFixedUpdate()
	{
		LifePunchMenuInputBlock.Apply();
	}

	internal static void Ensure( Scene scene )
	{
		if ( scene is null )
			return;

		var existing = scene.GetAllComponents<LifePunchMenuInputGuard>().FirstOrDefault();
		if ( existing.IsValid() )
			return;

		var go = scene.CreateObject();
		go.Name = GuardObjectName;
		go.AddComponent<LifePunchMenuInputGuard>();
	}
}

/// <summary>
/// Hands Esc to a claiming LifePunch panel before any component update runs, so DXRP's
/// PauseMenu (which reads Input.EscapePressed in its own OnUpdate) never sees that press.
/// A press no panel took is dropped at the end of the frame.
/// </summary>
public sealed class LifePunchEscapeRouter : GameObjectSystem<LifePunchEscapeRouter>
{
	public LifePunchEscapeRouter( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, -1, LifePunchMenuInputBlock.RouteEscape, "LifePunch Esc Router" );
		Listen( Stage.FinishUpdate, 999, LifePunchMenuInputBlock.TraceFrameEnd, "LifePunch Esc Trace" );
		Listen( Stage.FinishUpdate, 1000, LifePunchMenuInputBlock.DropUntakenEscape, "LifePunch Esc Drop" );
	}
}
