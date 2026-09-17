using Dxura.RP.Game;
using System.Threading.Tasks;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

public sealed class WeaponAttachmentCommand : ICommand
{
	public string Command => "weaponattachment";
	public string[] Aliases => ["attachment"];
	public string Help =>
		"/attachment attach <pistol_suppressor|pbs_suppressor|red_dot|laser> <item id> | " +
		"detach <muzzle|optic|rail> | laser <on|off>";
	public bool IsUsableWhileDead => false;

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		_ = raw;
		if ( !caller.IsValid() )
		{
			return false;
		}

		if ( !TryResolveController( caller, out var controller ) )
		{
			caller.Error( "Your deployed weapon does not support attachments." );
			return true;
		}

		if ( args.Length == 3
		     && string.Equals( args[0], "attach", StringComparison.OrdinalIgnoreCase )
		     && TryParseKind( args[1], out var kind )
		     && Guid.TryParse( args[2], out var itemId ) )
		{
			_ = ReportTransactionResultAsync(
				caller,
				controller.TryAttachForHost( caller, kind, itemId ),
				"Attachment installed." );

			return true;
		}

		if ( args.Length == 2
		     && string.Equals( args[0], "detach", StringComparison.OrdinalIgnoreCase )
		     && TryParseSlot( args[1], out var slot ) )
		{
			_ = ReportTransactionResultAsync(
				caller,
				controller.TryDetachForHost( caller, slot ),
				"Attachment removed." );

			return true;
		}

		if ( args.Length == 2
		     && string.Equals( args[0], "laser", StringComparison.OrdinalIgnoreCase )
		     && TryParseEnabled( args[1], out var enabled ) )
		{
			_ = ReportTransactionResultAsync(
				caller,
				controller.TrySetLaserEnabledForHost( caller, enabled ),
				enabled ? "Laser enabled." : "Laser disabled." );

			return true;
		}

		caller.SendMessage( Help );
		return true;
	}

	private static async Task ReportTransactionResultAsync(
		Player caller,
		Task<WeaponAttachmentTransactionResult> operation,
		string successMessage )
	{
		try
		{
			var result = await operation;
			await GameTask.MainThread();
			if ( !caller.IsValid() )
			{
				return;
			}

			if ( result.Succeeded )
			{
				caller.Success( successMessage );
				return;
			}

			if ( result.CompensationRequired )
			{
				caller.Error(
					"Attachment inventory requires reconciliation. Do not retry this action yet." );
				return;
			}

			caller.Error( FailureMessage( result.Failure ) );
		}
		catch ( Exception exception )
		{
			Log.Error( $"Weapon attachment command failed: {exception.Message}" );
			await GameTask.MainThread();
			if ( caller.IsValid() )
			{
				caller.Error(
					"Attachment outcome could not be confirmed. Check your inventory before retrying." );
			}
		}
	}

	private static string FailureMessage( WeaponAttachmentTransactionFailure failure )
	{
		return failure switch
		{
			WeaponAttachmentTransactionFailure.Incompatible =>
				"That attachment is not compatible with this weapon.",
			WeaponAttachmentTransactionFailure.SlotOccupied =>
				"That attachment slot is already occupied.",
			WeaponAttachmentTransactionFailure.SlotEmpty =>
				"That attachment slot is empty.",
			WeaponAttachmentTransactionFailure.InventoryTakeFailed =>
				"The attachment item could not be removed from your inventory.",
			WeaponAttachmentTransactionFailure.InventoryGiveFailed =>
				"The attachment item could not be returned to your inventory.",
			WeaponAttachmentTransactionFailure.StateCommitFailed =>
				"The weapon changed before the attachment action completed. Try again.",
			WeaponAttachmentTransactionFailure.CompensationFailed =>
				"The attachment inventory could not be reconciled.",
			_ => "Attachment request rejected."
		};
	}

	private static bool TryResolveController(
		Player caller,
		out WeaponAttachmentController controller )
	{
		controller = null!;
		var equipment = caller.CurrentEquipment;
		if ( !equipment.IsValid() )
		{
			return false;
		}

		var candidate = equipment.Components.Get<WeaponAttachmentController>(
			FindMode.EverythingInSelfAndDescendants );
		if ( !candidate.IsValid() )
		{
			return false;
		}

		controller = candidate;
		return true;
	}

	private static bool TryParseKind( string value, out WeaponAttachmentKind kind )
	{
		kind = value.Trim().ToLowerInvariant() switch
		{
			"pistol_suppressor" or "pistol-suppressor" or "pistolsuppressor" or "suppressor"
				=> WeaponAttachmentKind.PistolSuppressor,
			"pbs_suppressor" or "pbs-suppressor" or "pbssuppressor" or "pbs"
				=> WeaponAttachmentKind.PbsSuppressor,
			"red_dot" or "red-dot" or "reddot" => WeaponAttachmentKind.RedDot,
			"laser" => WeaponAttachmentKind.Laser,
			_ => WeaponAttachmentKind.None
		};

		return kind != WeaponAttachmentKind.None;
	}

	private static bool TryParseSlot( string value, out WeaponAttachmentSlot slot )
	{
		switch ( value.Trim().ToLowerInvariant() )
		{
			case "muzzle":
				slot = WeaponAttachmentSlot.Muzzle;
				return true;
			case "optic":
				slot = WeaponAttachmentSlot.Optic;
				return true;
			case "rail":
				slot = WeaponAttachmentSlot.Rail;
				return true;
			default:
				slot = default;
				return false;
		}
	}

	private static bool TryParseEnabled( string value, out bool enabled )
	{
		switch ( value.Trim().ToLowerInvariant() )
		{
			case "on":
			case "true":
			case "1":
				enabled = true;
				return true;
			case "off":
			case "false":
			case "0":
				enabled = false;
				return true;
			default:
				enabled = false;
				return false;
		}
	}
}
