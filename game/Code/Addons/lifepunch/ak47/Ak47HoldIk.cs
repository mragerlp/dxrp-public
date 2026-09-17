// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "AK-47" (s&box ident: lifepunch.ak47 · addon ident: ak47) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using Dxura.RP.Game;
using Sandbox;

namespace LifePunch.DXRP.Addons.AK47;

/// <summary>
/// Third-person hold IK for the AK world prefab. Uses the house
/// <see cref="AnimationHelper"/> door (same slots as surrender / kneel / typing).
/// Holster does not disable this GameObject — release also runs from OnUpdate.
/// LEFT HAND ONLY: the right hand is already placed by hold_R parenting plus
/// HoldType, so it needs no IK. Driving it from a grip on this weapon is
/// circular — see <see cref="RightGrip"/>.
/// </summary>
public sealed class Ak47HoldIk : Component
{
	[Property] public GameObject LeftGrip { get; set; }

	/// <summary>
	/// Dormant anchor. Kept on the prefab and documented, but deliberately NOT assigned
	/// to <see cref="AnimationHelper.IkRightHand"/>: this GameObject is a descendant of
	/// hold_R, so driving the right hand toward it makes the hand chase a goal carried by
	/// that same hand — a closed loop that walks the arm out to its reach limit and pins
	/// it there (measured 2026-08-21: hand_R to RightGrip 27.17u, matching hold_R to
	/// w_ak47 26.92u). No grip position fixes a constant offset. Release stays defensive
	/// for this slot so any stale assignment from an older build is still cleared.
	/// </summary>
	[Property] public GameObject RightGrip { get; set; }

	protected override void OnUpdate()
	{
		var equipment = Components.Get<Equipment>();
		if ( !equipment.IsValid() )
		{
			return;
		}

		var owner = equipment.Owner;
		if ( !owner.IsValid() )
		{
			return;
		}

		var helper = owner.AnimationHelper;
		if ( !helper.IsValid() )
		{
			return;
		}

		if ( owner.CurrentEquipment == equipment )
		{
			// Left hand only — the right hand rides hold_R + HoldType natively.
			TryAssign( owner, helper, LeftGrip, left: true );
			if ( helper.IkLeftHand == LeftGrip )
			{
				// The weapon root is attached after spawn. Refresh the target transform
				// so IK follows the settled hold_R pose instead of its spawn-time pose.
				helper.SetIk( "hand_left", LeftGrip.Transform.World );
			}
			return;
		}

		ReleaseIfOurs( helper );
	}

	protected override void OnDisabled()
	{
		ReleaseFromOwner();
	}

	protected override void OnDestroy()
	{
		ReleaseFromOwner();
	}

	private void ReleaseFromOwner()
	{
		var equipment = Components.Get<Equipment>();
		if ( !equipment.IsValid() )
		{
			return;
		}

		var helper = equipment.Owner?.AnimationHelper;
		if ( helper.IsValid() )
		{
			ReleaseIfOurs( helper );
		}
	}

	private void TryAssign( Player owner, AnimationHelper helper, GameObject grip, bool left )
	{
		if ( !grip.IsValid() )
		{
			return;
		}

		var slot = left ? helper.IkLeftHand : helper.IkRightHand;
		if ( slot.IsValid() && slot != grip && IsForeignOwner( owner, slot ) )
		{
			return;
		}

		if ( left )
		{
			helper.IkLeftHand = grip;
		}
		else
		{
			helper.IkRightHand = grip;
		}
	}

	private static bool IsForeignOwner( Player owner, GameObject slot )
	{
		if ( owner.TypingHandTarget.IsValid() && slot == owner.TypingHandTarget )
		{
			return true;
		}

		return slot.Name is "IK_LeftHand_Surrender" or "IK_RightHand_Surrender"
			or "IK_LeftHand_CPR" or "IK_RightHand_CPR";
	}

	private void ReleaseIfOurs( AnimationHelper helper )
	{
		if ( LeftGrip.IsValid() && helper.IkLeftHand == LeftGrip )
		{
			helper.IkLeftHand = null;
		}

		if ( RightGrip.IsValid() && helper.IkRightHand == RightGrip )
		{
			helper.IkRightHand = null;
		}
	}
}
