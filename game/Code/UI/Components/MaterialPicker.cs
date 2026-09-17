using System.Threading.Tasks;

namespace Dxura.RP.Game.UI;

public class MaterialPicker : Panel, IGameEvents
{
	private static readonly List<MaterialPicker> Live = new();

	private VirtualGrid _grid = null!;
	private SerializedProperty _property = null!;
	private bool _initialized;
	private int _listEpoch;

	public MaterialPicker()
	{
		Live.Add( this );
	}

	public Action<string>? OnValueChanged { get; set; }
	protected string Value { get; set; } = string.Empty;

	public SerializedProperty SerializedProperty
	{
		get => _property;
		set
		{
			_property = value;
			Value = _property.GetValue<string>();
		}
	}

	protected override void OnParametersSet()
	{
		if ( !_initialized )
		{
			_initialized = true;
			InitializeLayout();
		}

		PopulateMaterialList();
	}

	public void OnGameModeUpdated( GameModeDto? before, GameModeDto? after )
	{
		if ( !_initialized )
		{
			return;
		}

		PopulateMaterialList();
	}

	public static void RefreshLive()
	{
		for ( var i = Live.Count - 1; i >= 0; i-- )
		{
			var picker = Live[i];
			if ( picker is null || !picker.IsValid() )
			{
				Live.RemoveAt( i );
				continue;
			}

			if ( picker._initialized )
			{
				picker.PopulateMaterialList();
			}
		}
	}

	private void InitializeLayout()
	{
		AddClass("modelpicker");

		AddChild(out _grid, "canvas");
		_grid.Style.Height = 512;
		_grid.ItemSize = 64;
		_grid.AddClass("overflow-y-scroll");
		_grid.OnCreateCell = ( panel, o ) =>
		{
			_ = CreateMaterialCell(panel, o);
		};
	}

	private async Task CreateMaterialCell(Panel cell, object data)
	{
		var epoch = _listEpoch;
		var materialPath = (string)data;

		var placeholder = new Panel { Style = { Width = Length.Percent(100), Height = Length.Percent(100), BackgroundColor = Color.Gray.WithAlpha(0.5f) } };
		cell.AddChild(placeholder);
		SetupMaterialPanelInteraction(placeholder, materialPath);

		var nameLabel = placeholder.AddChild<Label>();
		nameLabel.Text = GetMaterialDisplayName(materialPath);
		nameLabel.Style.Position = PositionMode.Absolute;
		nameLabel.Style.Bottom = 0;
		nameLabel.Style.Left = 0;
		nameLabel.Style.Right = 0;
		nameLabel.Style.FontSize = 8;
		nameLabel.Style.TextAlign = TextAlign.Center;
		nameLabel.Style.BackgroundColor = Color.Black.WithAlpha(0.7f);

		try
		{
			var finalMaterialPath = materialPath;

			if ( GameModeJobDtoExtensions.IsCloudIdent( materialPath ) )
			{
				if ( GameModeBuilding.MaterialCloudOrgRejects( materialPath ) )
				{
					return;
				}

				var package = await Package.FetchAsync(materialPath, true, true);
				if ( epoch != _listEpoch || package == null ) return;

				var materialRef = await package.MountAsync();
				if ( epoch != _listEpoch || materialRef == null ) return;

				finalMaterialPath = package.GetMeta("PrimaryAsset", "");
				if(string.IsNullOrEmpty(finalMaterialPath)) return;
			}

			if ( epoch != _listEpoch ) return;

			var material = await Material.LoadAsync( finalMaterialPath );
			if ( epoch != _listEpoch || material == null ) return;

			var texture = ThumbnailCache.Get( material );
			var image = new Image { Texture = texture, Style = { PointerEvents = PointerEvents.All } };

			if (finalMaterialPath.Contains("glass") || finalMaterialPath.Contains("white"))
			{
				image.Style.BackgroundColor = Color.Gray;
			}

			placeholder.Delete();

			SetupMaterialPanelInteraction(image, materialPath);
			cell.AddChild( image );
		}
		catch (Exception ex)
		{
			Log.Warning($"Error loading material {materialPath}: {ex.Message}");
		}
	}

	private string GetMaterialDisplayName(string materialPath)
	{
		if (materialPath.Contains("."))
		{
			return materialPath.Split('.').Last();
		}

		if (materialPath.Contains("/"))
		{
			return materialPath.Split('/').Last().Replace(".vmat", "");
		}

		return materialPath.Replace(".vmat", "");
	}

	private void SetupMaterialPanelInteraction(Panel panel, string materialPath)
	{
		panel.Tooltip = materialPath;
		panel.AddClass( "icon" );
		panel.AddEventListener("onclick", () =>
		{
			SelectMaterial(materialPath);
		});
	}

	private void SelectMaterial(string materialPath)
	{
		Value = materialPath;
		OnValueChanged?.Invoke(Value);
		_property?.SetValue(Value);

		foreach (var cellPanel in _grid.Children.OfType<Panel>())
		{
			var materialPanel = cellPanel.Children.FirstOrDefault();
			if (materialPanel != null)
			{
				bool isSelected = materialPanel.Tooltip == materialPath;
				materialPanel.SetClass("selected", isSelected);

				if (isSelected)
				{
					materialPanel.Style.BorderColor = Color.White;
					materialPanel.Style.BorderWidth = 2;
				}
				else
				{
					materialPanel.Style.BorderColor = Color.Transparent;
					materialPanel.Style.BorderWidth = 0;
				}
			}
		}
	}

	private void PopulateMaterialList()
	{
		if ( !_initialized )
		{
			return;
		}

		_listEpoch++;
		_grid.Clear();

		foreach (var materialPath in GameModeBuilding.Materials.Distinct())
		{
			_grid.AddItem(materialPath);
		}

		if (GameModeBuilding.Materials.Count == 0)
		{
			var label = _grid.AddChild<Label>();
			label.Text = "No materials available";
			label.AddClass("empty-message");
		}
	}
}
