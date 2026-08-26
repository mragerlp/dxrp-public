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

	public static void NotifyMenuOpened()
	{
		_openDepth++;
		if ( _openDepth == 1 )
			AcquirePlayerCombatLock();

		LifePunchMenuInputGuard.Ensure( Game.ActiveScene );
		Apply();
	}

	public static void NotifyMenuClosed()
	{
		if ( _openDepth <= 0 )
			return;

		_openDepth--;
		if ( _openDepth == 0 )
			ReleasePlayerCombatLock();
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
