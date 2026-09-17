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

#if !LIFEPUNCH_LOCAL
using System.Globalization;
using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Host-only armor command discovered through DXRP's ICommand registry.
/// The return value means consumed, never a client success or durable-audit receipt.
/// </summary>
public sealed class SetArmorCommand : ICommand
{
	public const string PermissionId = "command.setarmor";

	public string Command => "setarmor";
	public string Help => "/setarmor <player or Steam ID> <amount> - set armor from 0 to the server limit";
	public bool IsUsableWhileDead => true;
	public string[] RequiredPermissionIds => [PermissionId];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( !caller.IsValid() )
		{
			Log.Warning( "Set Armor rejected: caller is unavailable." );
			return true;
		}

		if ( !Networking.IsHost )
		{
			caller.SendMessage( "Set Armor rejected: this command must run on the host." );
			return true;
		}

		// Chat checks this too; retain the guard if a host-side caller invokes the command directly.
		if ( !RankSystem.HasPermission( caller.SteamId, PermissionId ) )
		{
			caller.SendMessage( "Set Armor rejected: missing permission command.setarmor." );
			return true;
		}

		if ( args is null || args.Length != 2 || string.IsNullOrWhiteSpace( args[0] ) )
		{
			caller.SendMessage( Help );
			return true;
		}

		if ( !float.TryParse( args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount )
		     || !float.IsFinite( amount ) )
		{
			caller.SendMessage( "Set Armor rejected: enter a finite number using a decimal point." );
			return true;
		}

		var target = CommandHelper.ResolvePlayer( caller, args[0] );
		if ( target is null || !target.IsValid() )
		{
			// ResolvePlayer sends the not-found or ambiguous-target explanation.
			return true;
		}

		if ( target.SteamId != caller.SteamId && !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			caller.SendMessage( "#command.errors.higher_rank" );
			return true;
		}

		if ( !target.HealthComponent.IsValid() || target.HealthComponent.State != LifeState.Alive )
		{
			caller.SendMessage( "Set Armor rejected: the target must be alive and have a valid health component." );
			return true;
		}

		var armor = target.ArmorComponent;
		if ( !armor.IsValid() )
		{
			caller.SendMessage( "Set Armor rejected: the target has no valid armor component." );
			return true;
		}

		var maximum = armor.MaxArmor;
		if ( !float.IsFinite( maximum ) || maximum < 0f )
		{
			caller.SendMessage( "Set Armor rejected: the server armor limit is invalid." );
			return true;
		}

		if ( amount < 0f || amount > maximum )
		{
			caller.SendMessage( $"Set Armor rejected: amount must be from 0 to {maximum.ToString( CultureInfo.InvariantCulture )}." );
			return true;
		}

		// Existing host-synced state; armor changes do not grant a helmet or revive the target.
		armor.Armor = amount;
		var amountText = amount.ToString( CultureInfo.InvariantCulture );
		caller.SendMessage( $"Set {target.DisplayName}'s armor to {amountText}." );
		if ( target != caller )
		{
			target.SendMessage( $"Your armor was set to {amountText} by {caller.DisplayName}." );
		}

		Log.Info( $"[COMMAND] {caller.DisplayName} ({caller.SteamId}) set {target.DisplayName} ({target.SteamId})'s armor to {amountText}" );
		// Audit records locally and may queue a remote copy; its bool is not durable-delivery proof.
		_ = ServerApiClient.Audit( "SetArmor", $"{caller.SteamName} ({caller.SteamId}) set {target.SteamName} ({target.SteamId})'s armor to {amountText}", caller.SteamId );
		return true;
	}
}
#endif
