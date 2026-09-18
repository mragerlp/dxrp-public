namespace Dxura.RP.Game;

/// <summary>
/// Resolves the building whitelists (dxura/dxrp#273). When the gamemode carries a
/// non-empty <c>BuildingProps</c> / <c>BuildingMaterials</c> list, that list is the
/// sole grant — exact string membership, no additional <c>RestrictCloudOrg</c> check.
/// When the list is empty or missing, behavior falls back to the legacy gates:
/// <c>GameConfig.RestrictCloudOrg</c> for cloud props/materials and
/// <c>GameConfig.MaterialWhitelist</c> for materials.
/// </summary>
public static class GameModeBuilding
{
	private static List<string>? PropList
	{
		get
		{
			var list = Config.Current.GameMode.BuildingProps;
			return list is { Count: > 0 } ? list : null;
		}
	}

	private static List<string>? MaterialList
	{
		get
		{
			var list = Config.Current.GameMode.BuildingMaterials;
			return list is { Count: > 0 } ? list : null;
		}
	}

	/// <summary>
	/// Materials the picker, tool, and preloader enumerate.
	/// Fallback: <c>GameConfig.MaterialWhitelist</c>.
	/// </summary>
	public static IReadOnlyList<string> Materials =>
		MaterialList ?? (IReadOnlyList<string>)Config.Current.Game.MaterialWhitelist;

	/// <summary>
	/// Whether a model string (cloud ident or local ".vmdl"/".vmdl_c" path) may be
	/// spawned. Fallback keeps today's gate: local paths pass, cloud idents must
	/// match the <c>RestrictCloudOrg</c> prefix when one is set.
	/// </summary>
	public static bool IsPropAllowed( string modelPath )
	{
		var list = PropList;
		if ( list != null )
		{
			return list.Contains( modelPath );
		}

		if ( modelPath.EndsWith( ".vmdl" ) || modelPath.EndsWith( ".vmdl_c" ) )
		{
			return true;
		}

		return !LegacyCloudOrgRejects( modelPath );
	}

	/// <summary>
	/// Whether a material string (cloud ident or local ".vmat" path) may be applied.
	/// Fallback: exact membership in <c>GameConfig.MaterialWhitelist</c>.
	/// </summary>
	public static bool IsMaterialAllowed( string material )
	{
		var list = MaterialList;
		if ( list != null )
		{
			return list.Contains( material );
		}

		return Config.Current.Game.MaterialWhitelist.Contains( material );
	}

	/// <summary>
	/// Legacy <c>RestrictCloudOrg</c> prefix gate for an enumerated cloud material.
	/// Inactive once a material list exists — a listed item is already the grant.
	/// </summary>
	public static bool MaterialCloudOrgRejects( string cloudIdent )
	{
		return MaterialList == null && LegacyCloudOrgRejects( cloudIdent );
	}

	private static bool LegacyCloudOrgRejects( string cloudIdent )
	{
		var org = Config.Current.Game.RestrictCloudOrg;
		return org != null && !cloudIdent.StartsWith( org );
	}
}
