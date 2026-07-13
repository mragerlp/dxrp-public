namespace Dxura.RP.Game.UI;

public enum TabMenuEntryPlacement
{
	Top,
	Bottom
}

public sealed class TabMenuButtonContext
{
	public required Panel Panel { get; init; }
	public required Action CloseMenu { get; init; }
}

public sealed class TabMenuSectionDefinition
{
	public required string Id { get; init; }
	public required string Label { get; init; }
	public required string Icon { get; init; }
	public Type? PanelType { get; init; }
	public Func<string>? CopyText { get; init; }
	public string CopiedLabel { get; init; } = "#generic.copied";
	public Action<TabMenuButtonContext>? OnClick { get; init; }
	public int Order { get; init; }
	public TabMenuEntryPlacement Placement { get; init; }
	public bool DisabledWhenDead { get; init; }
	public bool DisabledWhenRestricted { get; init; }
	public Func<bool>? CanShow { get; init; }

	public bool IsTab => PanelType != null;
	public bool IsButton => !IsTab && (CopyText != null || OnClick != null);
	public bool Footer => Placement == TabMenuEntryPlacement.Bottom;

	public bool IsVisible
	{
		get
		{
			try
			{
				return CanShow?.Invoke() ?? true;
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to evaluate visibility for tab menu section '{Id}': {ex.Message}" );
				return false;
			}
		}
	}

	public bool IsAvailable( bool isPlayerDead, bool isPlayerRestricted )
	{
		if ( isPlayerDead && DisabledWhenDead )
		{
			return false;
		}

		if ( isPlayerRestricted && DisabledWhenRestricted )
		{
			return false;
		}

		return IsVisible;
	}
}

public interface ITabMenuSectionProvider
{
	void RegisterTabMenuSections( TabMenuSectionRegistry registry );
}

public sealed class TabMenuSectionRegistry
{
	private static IReadOnlyList<TabMenuSectionDefinition>? _sections;

	private readonly List<TabMenuSectionDefinition> _registeredSections = [];
	private readonly HashSet<string> _registeredIds = new( StringComparer.OrdinalIgnoreCase );

	public static IReadOnlyList<TabMenuSectionDefinition> Sections => _sections ??= BuildSections();

	public static void Reload()
	{
		_sections = null;
	}

	public void Register( TabMenuSectionDefinition section )
	{
		if ( string.IsNullOrWhiteSpace( section.Id ) )
		{
			Log.Warning( "Ignoring tab menu section with no id." );
			return;
		}

		if ( section.PanelType != null && !typeof( Panel ).IsAssignableFrom( section.PanelType ) )
		{
			Log.Warning( $"Ignoring tab menu section '{section.Id}' because its panel type is invalid." );
			return;
		}

		if ( !section.IsTab && !section.IsButton )
		{
			Log.Warning( $"Ignoring tab menu entry '{section.Id}' because it has no panel or action." );
			return;
		}

		if ( !_registeredIds.Add( section.Id ) )
		{
			Log.Warning( $"Ignoring duplicate tab menu section id '{section.Id}'." );
			return;
		}

		_registeredSections.Add( section );
	}

	public bool Remove( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
		{
			return false;
		}

		var section = _registeredSections.FirstOrDefault( section => string.Equals( section.Id, id, StringComparison.OrdinalIgnoreCase ) );
		if ( section == null )
		{
			return false;
		}

		_registeredSections.Remove( section );
		_registeredIds.Remove( section.Id );
		return true;
	}

	private static IReadOnlyList<TabMenuSectionDefinition> BuildSections()
	{
		var registry = new TabMenuSectionRegistry();
		registry.RegisterNativeSections();
		registry.RegisterAddonSections();

		return registry._registeredSections
			.OrderBy( section => section.Footer )
			.ThenBy( section => section.Order )
			.ThenBy( section => section.Label )
			.ToArray();
	}

	private void RegisterNativeSections()
	{
		Register( new TabMenuSectionDefinition
		{
			Id = "dashboard",
			Label = "#tabmenu.dashboard",
			Icon = "dashboard",
			PanelType = typeof( DashboardTabMenuSection ),
			Order = 0,
			CanShow = () => Config.Current.Game.ShowDashboardMenu
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "rules",
			Label = "#tabmenu.rules",
			Icon = "rule",
			PanelType = typeof( RulesTabMenuSection ),
			Order = 10,
			CanShow = () => Config.Current.Game.RulesEnabled
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "players",
			Label = "#tabmenu.players",
			Icon = "group",
			PanelType = typeof( PlayersTabMenuSection ),
			Order = 20
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "jobs",
			Label = "#tabmenu.jobs",
			Icon = "work",
			PanelType = typeof( JobTabMenuSection ),
			Order = 30,
			DisabledWhenDead = true,
			DisabledWhenRestricted = true,
			CanShow = () => Config.Current.Game.ShowJobsMenu
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "market",
			Label = "#tabmenu.market",
			Icon = "store",
			PanelType = typeof( MarketTabMenuSection ),
			Order = 40,
			DisabledWhenDead = true,
			DisabledWhenRestricted = true,
			CanShow = () => Config.Current.Game.ShowMarketMenu
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "governance",
			Label = "#tabmenu.governance",
			Icon = "gavel",
			PanelType = typeof( GovernanceTabMenuSection ),
			Order = 50,
			DisabledWhenDead = true,
			DisabledWhenRestricted = true,
			CanShow = () => Player.Local?.Job.IsGovernmentRole() == true
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "faction",
			Label = "#tabmenu.faction",
			Icon = "account_tree",
			PanelType = typeof( FactionTabMenuSection ),
			Order = 60,
			CanShow = () => FactionSystem.Instance.IsValid()
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "credits",
			Label = "#tabmenu.credits",
			Icon = "copyright",
			PanelType = typeof( CreditsTabMenuSection ),
			Order = 1000,
			Placement = TabMenuEntryPlacement.Bottom
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "discord",
			Label = "#tabmenu.discord",
			Icon = "discord",
			Order = 10,
			Placement = TabMenuEntryPlacement.Bottom,
			OnClick = context => DiscordWebPanel.Open( context.Panel, Config.Current.Game.DiscordUrl )
		} );

		Register( new TabMenuSectionDefinition
		{
			Id = "support",
			Label = "#generic.support.project",
			Icon = "handshake",
			Order = 20,
			Placement = TabMenuEntryPlacement.Bottom,
			CopyText = () => "https://opencollective.com/dxrp"
		} );
	}

	private void RegisterAddonSections()
	{
		var providerTypes = TypeLibrary.GetTypes<ITabMenuSectionProvider>();

		foreach ( var type in providerTypes.Where( t => !t.IsAbstract && t.TargetType != null ) )
		{
			try
			{
				if ( TypeLibrary.Create<ITabMenuSectionProvider>( type.TargetType ) is not { } provider )
				{
					continue;
				}

				provider.RegisterTabMenuSections( this );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to register tab menu sections from {type.Name}: {ex.Message}" );
			}
		}
	}
}
