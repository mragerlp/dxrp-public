using Dxura.RP.Game.Equipments;
using Sandbox.Diagnostics;

namespace Dxura.RP.Game.Entities;

public class ShipmentEntity : BaseEntity, IWireUsable, Component.IPressable
{
	[Property]
	[ReadOnly]
	[Sync( SyncFlags.FromHost )]
	public Guid MarketItemId { get; set; }

	[Property]
	[Change( nameof( OnQuantityChange ) )]
	[Sync( SyncFlags.FromHost )]
	public int Quantity { get; private set; }

	[Property]
	public Guid EquipmentId { get; set; }

	[Property]
	public int MaxQuantity { get; set; } = 10;

	[Property] public required GameObject EquipmentPreview { get; set; }
	[Property] public required ModelRenderer EquipmentRenderer { get; set; }

	[Property] public required TextRenderer TypeText { get; set; }
	[Property] public required TextRenderer QuantityText { get; set; }

	private float _totalAnimationTime;
	private Vector3 _originalPreviewPosition;
	private bool _previewPositionSaved;
	private Vector3 _authoredPreviewPosition;
	private bool _authoredPreviewSaved;

	public override bool DestroyOnJobChange => false;
	public override bool AllowOwnershipTransfer => true;

	public override string DisplayName => $"{TypeText.Text} ({QuantityText.Text})";

	private bool _occluded;

	protected override void OnStart()
	{
		base.OnStart();

		RememberAuthoredPreview();
		UpdateState();

		// Save the original position of the preview
		_originalPreviewPosition = EquipmentPreview.LocalPosition;
		_previewPositionSaved = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var previewRenderer = ResolveEquipmentRenderer();
		if ( previewRenderer.IsValid() && !IsUsablePreviewModel( previewRenderer.Model ) )
		{
			UpdateState();
		}

		if ( !_occluded && _previewPositionSaved && !GameManager.IsHeadless )
		{
			AnimatePreview();
		}
	}

	public override void OnOcclusionChanged( bool occlude )
	{
		base.OnOcclusionChanged( occlude );

		_occluded = occlude;
	}

	private ModelRenderer? ResolveEquipmentRenderer()
	{
		if ( EquipmentPreview.IsValid() )
		{
			var fromPreview = EquipmentPreview.Components.Get<ModelRenderer>();
			if ( fromPreview.IsValid() )
			{
				EquipmentRenderer = fromPreview;
				return fromPreview;
			}
		}

		return EquipmentRenderer.IsValid() ? EquipmentRenderer : null;
	}

	private void UpdateState()
	{
		var previewRenderer = ResolveEquipmentRenderer();
		if ( !previewRenderer.IsValid() || !TypeText.IsValid() || !QuantityText.IsValid() )
		{
			return;
		}

		if ( Networking.IsHost && Quantity <= 0 )
		{
			Quantity = MaxQuantity;
		}

		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment is null )
		{
			return;
		}

		previewRenderer.Model = equipment.GetWorldModel();
		previewRenderer.WorldScale = equipment.WorldModelScale() * 1.1f;
		AlignPreviewToModelOrigin();
		TypeText.Text = equipment.DisplayName();
		QuantityText.Text = $"{Quantity}/{MaxQuantity}";
	}

	private static bool IsUsablePreviewModel( Model? model )
	{
		return model is not null && model.IsValid() && !model.IsError;
	}

	private void RememberAuthoredPreview()
	{
		if ( _authoredPreviewSaved || !EquipmentPreview.IsValid() )
		{
			return;
		}

		_authoredPreviewPosition = EquipmentPreview.LocalPosition;
		_authoredPreviewSaved = true;
	}

	/// <summary>
	/// M4 world meshes sit on Z=0. The AK is authored centered (bottom ≈ -5.74).
	/// Lift the shared crate preview so every gun's lowest point matches that M4 sit.
	/// </summary>
	private void AlignPreviewToModelOrigin()
	{
		RememberAuthoredPreview();
		if ( !EquipmentPreview.IsValid() )
		{
			return;
		}

		var model = EquipmentRenderer.Model;
		if ( model is null || !model.IsValid() )
		{
			return;
		}

		var authored = _authoredPreviewSaved ? _authoredPreviewPosition : EquipmentPreview.LocalPosition;
		var lift = -model.Bounds.Mins.z * EquipmentRenderer.WorldScale.z;
		EquipmentPreview.LocalPosition = authored + Vector3.Up * lift;

		if ( _previewPositionSaved )
		{
			_originalPreviewPosition = EquipmentPreview.LocalPosition;
		}
	}

	public void ConfigureHost( GameModeEquipmentDto equipment, int quantity )
	{
		Assert.True( Networking.IsHost );

		EquipmentId = equipment.GameModeAddonContentId;
		MaxQuantity = Math.Max( 1, quantity );
		Quantity = MaxQuantity;
		UpdateState();
	}

	public bool Press( IPressable.Event e )
	{
		// Prevent using while rotating in hands
		var hands = Player.Local.GetComponentInChildren<HandsEquipment>();
		if ( hands.IsValid() && hands.IsHolding( GameObject, true ) )
		{
			return false;
		}

		if ( Cooldown.Current.CheckAndStartCooldown( "shipment:use", Config.Current.Game.ShipmentUseCooldown, true ) )
		{
			return false;
		}

		UseHost();

		return true;
	}

	public void OnWireUse( long owner, Vector3 userPosition )
	{
		InternalUse();
	}

	[Rpc.Host]
	private void UseHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:shipment:use", Config.Current.Game.ShipmentUseCooldown ) )
		{
			return;
		}

		// LOS check to prevent remote toggling
		var player = GameUtils.GetPlayerByConnectionId( callerId );
		if ( !player.IsValid() )
		{
			return;
		}

		var tr = Scene.Trace.Ray( player.AimRay, Config.Current.Game.ReachDistance )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.UseHitboxes()
			.Run();

		if ( !tr.Hit || tr.GameObject.Root != GameObject.Root )
		{
			return;
		}

		InternalUse();
	}

	private void InternalUse()
	{
		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment == null )
		{
			return;
		}

		Quantity--;
		DroppedEquipment.CreateHost( equipment, EquipmentPreview.WorldPosition,
			EquipmentPreview.WorldRotation, marketItemId: MarketItemId );

		if ( Quantity == 0 )
		{
			GameObject.Destroy();
		}
	}

	protected override void OnDestroyed()
	{
		Assert.True( Networking.IsHost );

		// Drop everything on destroy 
		var equipment = GameModeEquipments.FindById( EquipmentId );
		if ( equipment != null )
		{
			for ( var x = 0; x < Quantity; x++ )
			{
				DroppedEquipment.CreateHost( equipment, EquipmentPreview.WorldPosition,
					EquipmentPreview.WorldRotation, marketItemId: MarketItemId );
			}
		}

		base.OnDestroyed();
	}

	private void OnQuantityChange( int oldValue, int newValue )
	{
		QuantityText.Text = $"{Quantity}/{MaxQuantity}";
	}

	private void AnimatePreview()
	{
		// Increment total time
		_totalAnimationTime = (_totalAnimationTime + Time.Delta) % 360f;
		
		// Base values for animation
		const float bobHeight = 2f;
		const float bobSpeed = 2.0f;
		const float rotationSpeed = 45.0f;

		// Calculate vertical position using a sine wave
		var verticalOffset = MathF.Sin( _totalAnimationTime * bobSpeed ) * bobHeight;

		// Update position based on original position
		EquipmentPreview.LocalPosition = new Vector3(
			_originalPreviewPosition.x,
			_originalPreviewPosition.y,
			_originalPreviewPosition.z + verticalOffset
		);

		// Rotate around Y axis
		EquipmentPreview.LocalRotation = Rotation.FromYaw( _totalAnimationTime * rotationSpeed );
	}
}
