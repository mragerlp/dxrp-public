namespace Dxura.RP.Game.Entities;

public class ContainerDecalConfig
{
	public Vector3 LocalPosition { get; set; }
	public Rotation LocalRotation { get; set; }
	public Vector3 Size { get; set; } = new Vector3( 0.4f, 0.4f, 0.4f );
}

public class ContainerEntityConfig
{
	public string ResourceId { get; init; } = string.Empty;
	public string IconPath { get; init; } = string.Empty;
	public string Unit { get; init; } = "units";
	public ContainerType ContainerType { get; init; } = ContainerType.Bag;
	public int DefaultQuantity { get; init; }
	public bool DestroyOnEmpty { get; init; } = true;
	public string? Tint { get; init; }
	public string? UseSound { get; init; }
}


[Title( "Container" )]
[Category( "Entities" )]
public sealed class ContainerEntity : BaseEntity
{
	[Property]
	[Sync( SyncFlags.FromHost )]
	[Change( nameof( OnQuantityChanged ) )]
	public int Quantity { get; set; }

	[Property]
	private ModelRenderer ModelRenderer { get; set; } = null!;

	[Property]
	private ModelCollider ModelCollider { get; set; } = null!;

	[Property]
	// ReSharper disable once CollectionNeverUpdated.Global
	public Dictionary<ContainerType, Model> TypeModels { get; set; } = [];

	[Property]
	public Dictionary<ContainerType, ContainerDecalConfig> TypeDecals { get; set; } = [];

	[Property]
	[Group( "Effects" )]
	private Decal? Decal { get; set; }

	private ContainerEntityConfig _config = new();
	
	public ContainerType ContainerType => _config.ContainerType;

	public string ResourceId => _config.ResourceId;

	public bool IsEmpty => Quantity <= 0;

	public override string? DisplayName => string.Format( Language.GetPhrase( "entity.container.quantity" ),
		GameModeEntity.DisplayName(),
		Quantity,
		LabelResolver.ResolveText( _config.Unit ) );

	protected override void OnStart()
	{
		base.OnStart();

		_config = GetConfig( new ContainerEntityConfig() );

		UpdateState();
	}

	private void OnQuantityChanged( int oldValue, int newValue )
	{
		if ( newValue < oldValue && !string.IsNullOrEmpty( _config.UseSound ) )
		{
			Sound.Play( _config.UseSound, WorldPosition );
		}

		if ( newValue > 0 )
		{
			return;
		}
		if ( _config.DestroyOnEmpty )
		{
			GameObject.Destroy();
			return;
		}

		Quantity = 0;
	}

	private void UpdateState()
	{
		if ( Networking.IsHost && Quantity <= 0 )
		{
			Quantity = _config.DefaultQuantity;
		}

		if ( Decal.IsValid() && !string.IsNullOrWhiteSpace( _config.IconPath ) )
		{
			var icon = Texture.LoadFromFileSystem( _config.IconPath, FileSystem.Mounted );
			
			if ( icon != null )
			{
				Decal.Decals = [new DecalDefinition { ColorTexture = icon }];
			}
		}

		if ( ModelRenderer.IsValid() && TypeModels.TryGetValue( _config.ContainerType, out var model ) )
		{
			ModelRenderer.Model = model;

			if ( ModelCollider.IsValid() )
			{
				ModelCollider.Model = model;
			}
		}

		if ( Decal.IsValid() && TypeDecals.TryGetValue( _config.ContainerType, out var decalConfig ) )
		{
			Decal.GameObject.LocalPosition = decalConfig.LocalPosition;
			Decal.GameObject.LocalRotation = decalConfig.LocalRotation;
			Decal.Size = decalConfig.Size;
		}

		if ( ModelRenderer.IsValid() && Color.TryParse( _config.Tint, out var tint ) )
		{
			ModelRenderer.Tint = tint;
		}
	}

}
