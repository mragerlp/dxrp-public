using Dxura.RP.Shared;

namespace Dxura.RP.Game;

public static class GameModeEquipmentDtoExtensions
{
	private static readonly Dictionary<string, Model> ModelCache = new( StringComparer.OrdinalIgnoreCase );

	public static GameModeAddonContentDto? Content( this GameModeEquipmentDto? dto )
	{
		return dto == null ? null : GameModeAddonContents.FindById( dto.GameModeAddonContentId );
	}

	public static string Identifier( this GameModeEquipmentDto? dto )
	{
		return GameModeAddonContents.GetLookupKey( dto.Content() );
	}

	public static string PrefabPath( this GameModeEquipmentDto? dto )
	{
		return dto.Content()?.PrimaryReference ?? string.Empty;
	}

	public static string? SecondaryPrefabPath( this GameModeEquipmentDto? dto )
	{
		return dto.Content()?.SecondaryReference;
	}

	public static string Grouping( this GameModeEquipmentDto? dto )
	{
		return dto.Content()?.Grouping ?? string.Empty;
	}

	public static string Name( this GameModeEquipmentDto? dto )
	{
		return dto?.NameOverride ?? dto.Content()?.Name ?? string.Empty;
	}

	public static string Description( this GameModeEquipmentDto? dto )
	{
		return dto?.DescriptionOverride ?? dto.Content()?.Description ?? string.Empty;
	}

	public static EquipmentSlot SlotValue( this GameModeEquipmentDto? dto )
	{
		var grouping = dto.Grouping();
		if ( string.IsNullOrWhiteSpace( grouping ) )
		{
			return EquipmentSlot.Undefined;
		}

		return Enum.TryParse<EquipmentSlot>( grouping, true, out var slot ) ? slot : EquipmentSlot.Undefined;
	}

	public static string DisplayName( this GameModeEquipmentDto? dto )
	{
		if ( dto == null )
		{
			return string.Empty;
		}

		var name = dto.Name();
		return name.StartsWith( '#' ) ? Language.GetPhrase( name[1..] ) : name;
	}

	public static string DisplayDescription( this GameModeEquipmentDto? dto )
	{
		if ( dto == null )
		{
			return string.Empty;
		}

		return dto.Description();
	}

	public static string DisplayIcon( this GameModeEquipmentDto? dto )
	{
		return dto.Content()?.IconPath ?? string.Empty;
	}

	public static Model? GetWorldModel( this GameModeEquipmentDto? dto )
	{
		if ( dto is null )
		{
			return null;
		}

		var worldModelPath = dto.Content()?.WorldModelPath;
		var prefabPath = dto.PrefabPath();
		var cacheKey = $"{dto.GameModeAddonContentId:N}|{worldModelPath}|{prefabPath}";

		if ( ModelCache.TryGetValue( cacheKey, out var cached ) && IsUsableModel( cached ) )
		{
			return cached;
		}

		var loaded = TryLoadWorldModel( worldModelPath );
		if ( loaded is null )
		{
			loaded = TryLoadWorldModelFromPrefab( prefabPath );
			if ( loaded is not null )
			{
				Log.Info( $"GetWorldModel prefab-fallback name={dto.DisplayName()} model={loaded.ResourcePath}" );
			}
		}

		if ( loaded is not null )
		{
			ModelCache[cacheKey] = loaded;
		}

		return loaded;
	}

	private static Model? TryLoadWorldModel( string? modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
		{
			return null;
		}

		var loaded = Model.Load( modelPath );
		return IsUsableModel( loaded ) ? loaded : null;
	}

	private static Model? TryLoadWorldModelFromPrefab( string? prefabPath )
	{
		var renderer = TryFindWorldPreviewRenderer( prefabPath );
		return renderer.IsValid() ? renderer.Model : null;
	}

	private static ModelRenderer? TryFindWorldPreviewRenderer( string? prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
		{
			return null;
		}

		var prefab = GameObject.GetPrefab( prefabPath );
		if ( !prefab.IsValid() )
		{
			return null;
		}

		var equipment = prefab.Components.Get<Equipment>( FindMode.EverythingInSelfAndDescendants );
		if ( equipment.IsValid() && IsUsableWorldPreviewRenderer( equipment.ModelRenderer ) )
		{
			return equipment.ModelRenderer;
		}

		ModelRenderer? best = null;
		var bestScore = int.MinValue;
		foreach ( var renderer in prefab.GetComponentsInChildren<ModelRenderer>( true ) )
		{
			if ( !IsUsableWorldPreviewRenderer( renderer ) )
			{
				continue;
			}

			var score = ScoreWorldPreviewRenderer( renderer );
			if ( score <= bestScore )
			{
				continue;
			}

			bestScore = score;
			best = renderer;
		}

		return best;
	}

	private static bool IsUsableWorldPreviewRenderer( ModelRenderer renderer )
	{
		if ( !renderer.IsValid() || !renderer.Enabled || !renderer.GameObject.Enabled || !IsUsableModel( renderer.Model ) )
		{
			return false;
		}

		var material = renderer.MaterialOverride;
		if ( material.IsValid() )
		{
			var materialPath = material.ResourcePath ?? string.Empty;
			if ( materialPath.Contains( "invisible", StringComparison.OrdinalIgnoreCase ) )
			{
				return false;
			}
		}

		return true;
	}

	private static int ScoreWorldPreviewRenderer( ModelRenderer renderer )
	{
		var score = (int)renderer.Model.Bounds.Size.Length;
		if ( renderer.GameObject.Name.Contains( "mesh", StringComparison.OrdinalIgnoreCase ) )
		{
			score += 1000;
		}

		var resourcePath = renderer.Model.ResourcePath ?? string.Empty;
		if ( resourcePath.StartsWith( "addons/", StringComparison.OrdinalIgnoreCase ) )
		{
			score += 500;
		}

		if ( resourcePath.Contains( "models/weapons/sbox", StringComparison.OrdinalIgnoreCase ) )
		{
			score -= 800;
		}

		return score;
	}

	private static bool IsUsableModel( Model? model )
	{
		return model is not null && model.IsValid() && !model.IsError;
	}

	public static float WorldModelScale( this GameModeEquipmentDto? dto )
	{
		var configuredScale = dto.Content()?.WorldModelScale;
		if ( configuredScale.HasValue && float.IsFinite( configuredScale.Value ) && configuredScale.Value > 0f )
		{
			return configuredScale.Value;
		}

		return TryLoadWorldModelScaleFromPrefab( dto.PrefabPath() );
	}

	private static float TryLoadWorldModelScaleFromPrefab( string? prefabPath )
	{
		var renderer = TryFindWorldPreviewRenderer( prefabPath );
		if ( !renderer.IsValid() )
		{
			return 1f;
		}

		var scale = renderer.WorldScale;
		if ( !float.IsFinite( scale.x ) || !float.IsFinite( scale.y ) || !float.IsFinite( scale.z )
			|| scale.x <= 0f || scale.y <= 0f || scale.z <= 0f
			|| MathF.Abs( scale.x - scale.y ) > 0.0001f
			|| MathF.Abs( scale.x - scale.z ) > 0.0001f )
		{
			return 1f;
		}

		return scale.x;
	}

	public static bool IsValid( this GameModeEquipmentDto? dto )
	{
		return dto != null && !string.IsNullOrWhiteSpace( dto.Identifier() );
	}
}
