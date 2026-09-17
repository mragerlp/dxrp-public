namespace Dxura.RP.Game;

/// <summary>
/// Drives only the support hand toward a measured target on an equipped world
/// weapon. The firing hand remains owned by hold_R, the equipment hold type,
/// and the player animation graph.
/// </summary>
[Icon( "back_hand" )]
[Title( "World Weapon Left-Hand IK" )]
[Group( "Weapon Components" )]
public sealed class WorldWeaponLeftHandIk : Component
{
	[Property]
	[RequireComponent]
	public required Equipment Equipment { get; set; }

	/// <summary>
	/// A per-weapon, principal-approved grip target inside the moving weapon
	/// hierarchy. Do not copy this transform between weapon models.
	/// </summary>
	[Property]
	public GameObject? LeftGrip { get; set; }

	private AnimationHelper? _assignedHelper;
	private GameObject? _assignedGrip;

	protected override void OnUpdate()
	{
		if ( !Equipment.IsValid() )
		{
			ReleaseCached();
			return;
		}

		var owner = Equipment.Owner;
		var helper = owner?.AnimationHelper;
		if ( !owner.IsValid() || !helper.IsValid() )
		{
			ReleaseCached();
			return;
		}

		if ( _assignedHelper.IsValid() &&
			(_assignedHelper != helper || _assignedGrip != LeftGrip) )
		{
			ReleaseCached();
		}

		if ( owner.CurrentEquipment != Equipment ||
			owner.IsTyping ||
			!LeftGrip.IsValid() ||
			!LeftGrip.Active )
		{
			ReleaseCached();
			return;
		}

		// Yield to every active foreign target. Surrender, CPR, and future
		// animation systems remain authoritative without a name allowlist. An
		// inactive stale pointer is not an applied IK target and must not starve
		// this grip forever.
		if ( helper.IkLeftHand.IsValid() &&
			helper.IkLeftHand.Active &&
			helper.IkLeftHand != LeftGrip )
		{
			return;
		}

		helper.IkLeftHand = LeftGrip;
		helper.SetIk( "hand_left", LeftGrip.Transform.World );
		_assignedHelper = helper;
		_assignedGrip = LeftGrip;
	}

	protected override void OnDisabled()
	{
		ReleaseCached();
	}

	protected override void OnDestroy()
	{
		ReleaseCached();
	}

	private void ReleaseCached()
	{
		if ( _assignedHelper.IsValid() &&
			_assignedGrip is not null &&
			_assignedHelper.IkLeftHand == _assignedGrip )
		{
			_assignedHelper.IkLeftHand = null;
		}

		_assignedHelper = null;
		_assignedGrip = null;
	}
}
