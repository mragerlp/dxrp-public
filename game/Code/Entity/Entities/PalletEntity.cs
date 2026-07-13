using Sandbox.Diagnostics;

namespace Dxura.RP.Game.Entities;

public sealed class PalletEntityConfig
{
	public uint MaxLayers { get; init; } = 4;
}

[Title( "Pallet" )]
[Category( "Entities" )]
public class PalletEntity : BaseEntity, Component.ITriggerListener
{
	// The pallet's own solid collider - defines the physical support surface (so players/props
	// can rest on the pallet) and the footprint used to lay out the stacking grid.
	[Property]
	public required BoxCollider PlacementArea { get; set; }

	private PalletEntityConfig _config = null!;

	[Property]
	[Sync( SyncFlags.FromHost )]
	[Change( nameof( OnItemCountChanged ) )]
	public int ItemCount { get; private set; }

	// Footprint and model of whatever item is currently stacked, taken from the first one
	// placed. Synced so host and clients (including late joiners) derive the same slot
	// grid/capacity and can (re)build cargo visuals purely from replicated state, with no
	// per-item RPCs needed.
	[Property]
	[Sync( SyncFlags.FromHost )]
	private Vector3 ItemFootprint { get; set; }

	[Property]
	[Sync( SyncFlags.FromHost )]
	[Change( nameof( OnModelPathChanged ) )]
	private string ModelPath { get; set; } = string.Empty;

	// Pooled visual props, one per slot index ever used. Reused (shown/hidden, re-modeled)
	// across sell/refill cycles instead of being destroyed and recreated from scratch.
	private readonly List<GameObject> _visualPool = new();

	// Host-only. Gates which items are accepted so cargo stays a single, homogeneous item
	// type until it's fully sold off.
	[Property]
	private string _resourceId = string.Empty;

	// Shown while empty, before any item has defined a real footprint to grid out from.
	private const int DefaultCapacity = 60;

	private static readonly Vector3 FallbackItemSize = new( 6f, 6f, 4f );
	private Vector3 EffectiveItemSize => ItemFootprint.LengthSquared > 0f ? ItemFootprint : FallbackItemSize;

	private int Columns => Math.Max( 1, (int)(PlacementArea.Scale.x / EffectiveItemSize.x) );
	private int Rows => Math.Max( 1, (int)(PlacementArea.Scale.y / EffectiveItemSize.y) );
	private int Capacity => ItemFootprint.LengthSquared > 0f
		? Columns * Rows * (int)_config.MaxLayers
		: DefaultCapacity;

	protected override void OnStart()
	{
		base.OnStart();

		_config = GetConfig( new PalletEntityConfig() );

		// Snapshot deserialization restores the serialized cargo fields, but the visual pool is
		// runtime-only. Rebuild it explicitly instead of depending on property change callback order.
		SyncVisuals();
	}

	public void OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost || !other.IsValid() )
		{
			return;
		}

		var root = other.Root;

		if ( root == GameObject )
		{
			return;
		}

		var sellable = root.GetComponent<ResourceComponent>();
		if ( !sellable.IsValid() || !sellable.PalletStackable )
		{
			return;
		}

		// Only accept more of whatever item type is already loaded, once one is set
		if ( !string.IsNullOrEmpty( _resourceId ) && sellable.ResourceId != _resourceId )
		{
			return;
		}

		if ( ItemCount >= Capacity )
		{
			return;
		}

		var renderer = root.GetComponent<ModelRenderer>();
		var model = renderer.IsValid() ? renderer.Model : null;

		if ( model is null )
		{
			return;
		}

		if ( string.IsNullOrEmpty( _resourceId ) )
		{
			_resourceId = sellable.ResourceId;
			ItemFootprint = model.Bounds.Size;
			ModelPath = model.ResourcePath;
		}

		root.Destroy();
		ItemCount++;
	}

	/// <summary>
	/// Sells and clears all cargo stacked on this pallet. Intended to be called by drop points
	/// when the pallet is sitting in their trigger. Returns the number of items sold.
	/// </summary>
	public int SellCargoHost()
	{
		Assert.True( Networking.IsHost );

		var count = ItemCount;
		if ( count == 0 )
		{
			return 0;
		}

		ItemCount = 0;
		_resourceId = string.Empty;
		return count;
	}

	// Runs on host and on every client (including late joiners catching up on initial state),
	// purely from synced values - no RPCs required. Idempotent, so it doesn't matter which of
	// ItemCount/ModelPath's change callbacks fires last after the very first item is placed.
	private void OnItemCountChanged( int oldValue, int newValue ) => SyncVisuals();

	private void OnModelPathChanged( string oldValue, string newValue ) => SyncVisuals();

	private void SyncVisuals()
	{
		Model? model = string.IsNullOrEmpty( ModelPath ) ? null : Model.Load( ModelPath );

		for ( var i = 0; i < ItemCount && model is not null; i++ )
		{
			var visual = GetOrCreatePooledVisual( i );
			var (localPosition, localRotation) = GetSlotTransform( i );

			visual.Enabled = true;
			visual.LocalPosition = localPosition;
			visual.LocalRotation = localRotation;

			var visualRenderer = visual.Components.Get<ModelRenderer>();
			if ( visualRenderer.Model != model )
			{
				visualRenderer.Model = model;
			}
		}

		// Anything beyond the current count (or all of it, if empty/no model) just gets
		// hidden - it stays pooled for the next time this slot index is needed.
		var activeCount = model is null ? 0 : ItemCount;
		for ( var i = activeCount; i < _visualPool.Count; i++ )
		{
			_visualPool[i].Enabled = false;
		}
	}

	private GameObject GetOrCreatePooledVisual( int index )
	{
		if ( index < _visualPool.Count )
		{
			return _visualPool[index];
		}

		var visual = new GameObject( true, "pallet_item" ) { Parent = GameObject };
		visual.Components.Create<ModelRenderer>();
		_visualPool.Add( visual );

		return visual;
	}

	private (Vector3 Position, Rotation Rotation) GetSlotTransform( int index )
	{
		var columns = Columns;
		var rows = Rows;
		var perLayer = columns * rows;

		var layer = index / perLayer;
		var indexInLayer = index % perLayer;
		var col = indexInLayer % columns;
		var row = indexInLayer / columns;

		var itemSize = EffectiveItemSize;
		var startX = -((columns - 1) * itemSize.x) / 2f;
		var startY = -((rows - 1) * itemSize.y) / 2f;
		var topZ = PlacementArea.Center.z + PlacementArea.Scale.z / 2f;

		var localPosition = PlacementArea.Center with
		{
			x = startX + col * itemSize.x,
			y = startY + row * itemSize.y,
			z = topZ + itemSize.z / 2f + layer * itemSize.z
		};

		return (localPosition, Rotation.Identity);
	}

	public override string DisplayName => string.Format( Language.GetPhrase( "entity.pallet.count" ), ItemCount, Capacity );
}
