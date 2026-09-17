using Dxura.RP.Game.UI;

namespace Dxura.RP.Game.Tools;

[Tool( "#tool.material.name", "#tool.material.description", "#tool.group.render" )]
public class MaterialTool : BaseTool
{
	[Property]
	[MaterialProperty]
	public string SelectedMaterial { get; set; } = "";

	[Property]
	public List<string> MaterialOptions { get; set; } = new();

	public override string Attack1Control => "#tool.material.attack1";
	public override string Attack2Control => "#tool.material.attack2";
	public override string ReloadControl => "#tool.material.reload";

	public event Action? OnMaterialsRefreshed;

	public override void OnEquip()
	{
		base.OnEquip();
		RefreshMaterials();
		ToolMenu.Instance?.UpdateInspector();
		OnMaterialsRefreshed?.Invoke(); // Force refresh to ensure MaterialPicker updates
	}

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		RefreshMaterials();
		ToolMenu.Instance?.UpdateInspector();
	}

	public static void RefreshFromGameMode( GameModeDto? before, GameModeDto? after )
	{
		if ( CurrentTool is MaterialTool tool )
		{
			tool.OnGameModeUpdated( before, after );
		}
	}

	private void RefreshMaterials()
	{
		MaterialOptions.Clear();

		// Gamemode building list when present, GameConfig whitelist as fallback
		foreach ( var materialPath in GameModeBuilding.Materials )
		{
			MaterialOptions.Add( materialPath );
		}

		if ( MaterialOptions.Count == 0 )
		{
			SelectedMaterial = "";
		}
		else if ( string.IsNullOrWhiteSpace( SelectedMaterial ) || !MaterialOptions.Contains( SelectedMaterial ) )
		{
			SelectedMaterial = MaterialOptions[0];
		}

		OnMaterialsRefreshed?.Invoke();
	}

	public override void PrimaryUseStart()
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "tool:material:use", Config.Current.Game.ActionQuickCooldown, true ) )
		{
			return;
		}

		var tr = PerformEyeTrace();

		if ( !tr.Hit || !tr.GameObject.IsValid() )
		{
			return;
		}

		var go = tr.GameObject.Root;

		if ( !GameUtils.HasPermission( Player.Local.SteamId, go ) )
		{
			Notify.Error( "#generic.permission" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( SelectedMaterial ) )
		{
			Notify.Error( "#tool.material.no_selection" );
			return;
		}

		if ( !GameModeBuilding.IsMaterialAllowed( SelectedMaterial ) )
		{
			Notify.Error( "#generic.forbidden" );
			return;
		}

		var prop = go.GetComponent<Prop>();
		if ( !prop.IsValid() )
		{
			return;
		}

		if ( prop.Data is PropData propData )
		{
			var newData = propData with
			{
				Material = SelectedMaterial
			};
			Construct.Current.UpdateConstructPlayer( prop.Type, newData, go );
		}

		Tool.DoUseEffects( true, tr.HitPosition, tr.Normal );
	}

	public override void SecondaryUseStart()
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "tool:material:use", Config.Current.Game.ActionQuickCooldown, true ) )
		{
			return;
		}

		var tr = PerformEyeTrace();

		if ( !tr.Hit || !tr.GameObject.IsValid() )
		{
			return;
		}

		var go = tr.GameObject.Root;

		var prop = go.GetComponent<Prop>();

		if ( !prop.IsValid() )
		{
			return;
		}

		if ( prop.Data is PropData propData && !string.IsNullOrWhiteSpace( propData.Material ) )
		{
			if ( !GameModeBuilding.Materials.Contains( propData.Material ) || !GameModeBuilding.IsMaterialAllowed( propData.Material ) )
			{
				Notify.Error( "#generic.forbidden" );
				return;
			}

			SelectedMaterial = propData.Material;
			Notify.Success( "#tool.material.copied" );
			Tool.DoUseEffects( true, tr.HitPosition, tr.Normal );
		}
	}

	public override void ReloadUseStart()
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "tool:material:use", Config.Current.Game.ActionQuickCooldown, true ) )
		{
			return;
		}

		var tr = PerformEyeTrace();
		if ( !tr.Hit || !tr.GameObject.IsValid() )
		{
			return;
		}

		var root = tr.GameObject.Root;

		if ( !GameUtils.HasPermission( Player.Local.SteamId, root ) )
		{
			Notify.Error( "#generic.permission" );
			return;
		}

		var prop = root.GetComponent<Prop>();
		if ( !prop.IsValid() )
		{
			return;
		}

		if ( prop.Data is PropData propData )
		{
			var newData = propData with
			{
				Material = ""
			};
			Construct.Current.UpdateConstructPlayer( prop.Type, newData, root );
		}

		Tool.DoUseEffects( true, tr.HitPosition, tr.Normal );
		Notify.Success( "#tool.material.reset" );
	}
}
