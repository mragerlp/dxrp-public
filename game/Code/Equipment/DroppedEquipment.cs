using Dxura.RP.Shared;
using Sandbox.Diagnostics;

namespace Dxura.RP.Game;

public class DroppedEquipment : Component, Component.IPressable
{
	[Property]
	public GameModeEquipmentDto? Resource { get; private set; }
	
	public Guid EquipmentId { get; private set; }
	
	public string PrefabPath { get; private set; } = "";
	public Guid MarketItemId { get; private set; }
	public Rigidbody Rigidbody { get; private set; } = null!;

	private bool _worldModelApplied;

	protected override void OnUpdate()
	{
		if ( _worldModelApplied )
		{
			return;
		}

		ApplyWorldModelIfNeeded();
	}

	private void ApplyWorldModelIfNeeded()
	{
		var renderer = Components.Get<ModelRenderer>();
		if ( !renderer.IsValid() )
		{
			return;
		}

		if ( IsUsableWorldModel( renderer.Model ) )
		{
			_worldModelApplied = true;
			return;
		}

		if ( Resource is null )
		{
			return;
		}

		var model = Resource.GetWorldModel();
		if ( !IsUsableWorldModel( model ) )
		{
			return;
		}

		renderer.Model = model;
		var collider = Components.Get<BoxCollider>();
		if ( collider.IsValid() && model.Bounds.Size.Length > 0.1f )
		{
			collider.Scale = model.Bounds.Size;
			collider.Center = model.Bounds.Center;
		}

		_worldModelApplied = true;
	}

	private static bool IsUsableWorldModel( Model? model )
	{
		return model is not null && model.IsValid() && !model.IsError;
	}

	public bool CanPress( IPressable.Event e )
	{
		if ( !e.Source.IsValid() || e.Source is not PlayerController playerController )
		{
			return false;
		}

		var player = playerController.GameObject.GetComponent<Player>();

		if ( Resource == null || player.CanTake( Resource ) == Player.PickupResult.None )
		{
			return false;
		}

		return true;
	}

	public bool Press( IPressable.Event e )
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "action:quick", Config.Current.Game.ActionQuickCooldown, true ) )
		{
			return false;
		}

		DoPickupHost();
		return true;
	}

	[Rpc.Host]
	private void DoPickupHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:action:quick", Config.Current.Game.ActionQuickCooldown ) )
		{
			return;
		}

		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			return;
		}

		// Server-side distance check to prevent remote pickup exploits
		var distance = player.WorldPosition.Distance( WorldPosition );
		if ( distance > Config.Current.Game.ReachDistance )
		{
			return;
		}

		var resource = Resource;
		if ( resource == null )
		{
			GameObject.Destroy();
			return;
		}

		var existingWeapon = player.Equipment.FirstOrDefault( w => w.EquipmentId == EquipmentId );

		if ( existingWeapon != null )
		{
			var mergePolicies = Components.GetAll<IDroppedWeaponMergePolicy>();
			if ( mergePolicies.Any( policy => !policy.CanMergeDuplicate() ) )
			{
				return;
			}

			if ( existingWeapon.GameObject.Network.Owner != player.Connection )
			{
				existingWeapon.GameObject.Network.AssignOwnership( player.Connection );
			}

			// Player already has this weapon - just add fixed ammo
			var existingAmmo = existingWeapon.Components.Get<AmmoComponent>( FindMode.EverythingInSelfAndDescendants );

			if ( existingAmmo != null )
			{
				var magazineSpace = existingAmmo.MaxAmmo - existingAmmo.Ammo;
				var magazineAmmoAdded = Math.Min( existingAmmo.MaxAmmo, magazineSpace );

				var reserveSpace = existingAmmo.MaxReserveAmmo - existingAmmo.ReserveAmmo;
				var reserveAmmoAdded = Math.Min( existingAmmo.MaxAmmo, reserveSpace );

				var newAmmo = existingAmmo.Ammo + magazineAmmoAdded;
				var newReserve = existingAmmo.ReserveAmmo + reserveAmmoAdded;

				// Ensure the weapon is network owned by the server for RPC call
				if ( existingAmmo.GameObject.Network.Owner != player.Connection &&
				     existingAmmo.GameObject.Network.Owner != null )
				{
					existingAmmo.GameObject.Network.AssignOwnership( null ); // Server ownership
				}

				// Update values and broadcast to all clients
				existingAmmo.UpdateAmmoValues( newAmmo, newReserve );

				player.SetCurrentEquipment( existingWeapon );
				GameManager.Instance.AmmoSound?.Broadcast( WorldPosition );

				GameObject.Destroy();
				return;
			}
		}

		var weapon = player.GiveHost( resource );

		if ( !weapon.IsValid() )
		{
			GameObject.Destroy();
			return;
		}

		weapon.MarketItemId = MarketItemId;

		foreach ( var state in weapon.Components.GetAll<IDroppedWeaponState>() )
		{
			state.CopyFromDroppedWeapon( this );
		}

		IEquipmentEvents.Post( x => x.OnEquipmentPickedUp( player, this, weapon ) );

		GameObject.Destroy();
	}

	public static DroppedEquipment CreateHost( GameModeEquipmentDto dto, Vector3 position, Rotation? rotation = null,
		Equipment? heldWeapon = null, bool networkSpawn = true, Guid marketItemId = default )
	{
		Assert.True( Networking.IsHost );
		var worldModelScale = dto.WorldModelScale();

		var go = new GameObject
		{
			WorldPosition = position, WorldRotation = rotation ?? Rotation.Identity, Name = dto.DisplayName()
		};
		go.WorldScale = worldModelScale;
		go.Tags.Add( Constants.HandsInteractTag, Constants.OccludableTag, Constants.PocketItemTag, Constants.EntityTag );

		var droppedWeapon = go.Components.Create<DroppedEquipment>();
		droppedWeapon.Resource = dto;
		droppedWeapon.EquipmentId = dto.GameModeAddonContentId;
		droppedWeapon.PrefabPath = dto.PrefabPath();
		droppedWeapon.MarketItemId = marketItemId != Guid.Empty ? marketItemId : heldWeapon?.MarketItemId ?? Guid.Empty;

		var model = dto.GetWorldModel();
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;

		var collider = go.Components.Create<BoxCollider>();
		if ( model.IsValid() && model.Bounds.Size.Length > 0.1f )
		{
			collider.Scale = model.Bounds.Size;
			collider.Center = model.Bounds.Center;
		}
		else
		{
			collider.Scale = heldWeapon?.DroppedSize ?? new Vector3( 8, 2, 8 );
			collider.Center = heldWeapon?.DroppedCenter ?? default;
		}

		droppedWeapon.Rigidbody = go.Components.Create<Rigidbody>();

		if ( heldWeapon is not null )
		{
			foreach ( var state in heldWeapon.Components.GetAll<IDroppedWeaponState>() )
			{
				state.CopyToDroppedWeapon( droppedWeapon );
			}

			foreach ( var presentationSource in heldWeapon.Components.GetAll<IDroppedWeaponPresentationSource>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( presentationSource.TryCopyPresentationTo( droppedWeapon ) )
				{
					break;
				}
			}
		}

		IEquipmentEvents.Post( x => x.OnEquipmentDropped( droppedWeapon, heldWeapon?.Owner ) );

		// Destroy the dropped weapon after a certain time
		go.DestroyAsync( Config.Current.Game.DroppedEquipmentDestroyTime, true );

		if ( networkSpawn )
		{
			go.NetworkSpawn();
		}

		return droppedWeapon;
	}
}
