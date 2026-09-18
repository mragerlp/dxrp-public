using Dxura.RP.Game;

namespace LifePunch.DXRP.Addons.Weapons.Attachments;

[Title( "Weapon Attachment State" )]
[Group( "LifePunch Weapons" )]
public sealed class WeaponAttachmentState : Component,
	IDroppedWeaponState<WeaponAttachmentState>,
	IDroppedWeaponMergePolicy,
	IEquipmentSnapshotState,
	IWeaponAttachmentStateGateway,
	IWeaponFirePresentationProvider
{
	[Property]
	[Sync( SyncFlags.FromHost )]
	public WeaponAttachmentLoadout ReplicatedLoadout { get; set; }

	public WeaponAttachmentLoadout Current => ReplicatedLoadout;

	public bool TryReplace( WeaponAttachmentLoadout expected, WeaponAttachmentLoadout replacement )
	{
		if ( !Networking.IsHost
		     || Current != expected
		     || !WeaponAttachmentSnapshotCodec.IsValid( replacement ) )
		{
			return false;
		}

		Apply( replacement );
		return true;
	}

	public byte GetFirePresentation()
	{
		return WeaponAttachmentPresentation.ResolveFirePresentation( Current );
	}

	public bool CanMergeDuplicate()
	{
		return WeaponAttachmentDropPolicy.CanMergeDuplicate( Current );
	}

	string IEquipmentSnapshotState.SnapshotKey => WeaponAttachmentSnapshotCodec.SnapshotKey;

	bool IEquipmentSnapshotState.TryCaptureSnapshotState( out string payload )
	{
		return WeaponAttachmentSnapshotCodec.TrySerialize( Current, out payload );
	}

	bool IEquipmentSnapshotState.TryRestoreSnapshotState( string payload )
	{
		if ( !Networking.IsHost
		     || !WeaponAttachmentSnapshotCodec.TryDeserialize( payload, out var restored ) )
		{
			return false;
		}

		var controller = Components.Get<WeaponAttachmentController>();
		var equipment = Components.Get<Equipment>( FindMode.EverythingInSelfAndAncestors );
		if ( !controller.IsValid()
		     || !equipment.IsValid()
		     || !WeaponAttachmentCompatibility.MatchesEquipmentPrefab( controller.WeaponId, equipment.Resource.PrefabPath() )
		     || !WeaponAttachmentCompatibility.CanRestoreLoadout( controller.WeaponId, restored ) )
		{
			return false;
		}

		return TryReplace( Current, restored );
	}

	private void Apply( WeaponAttachmentLoadout loadout )
	{
		ReplicatedLoadout = loadout;
	}
}
