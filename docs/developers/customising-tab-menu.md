---
title: Customising the Tab Menu
description: Add, remove, replace, and control entries in DXRP's in-game tab menu from an addon.
order: 2
---

DXRP's tab menu is extensible from an addon. Implement `ITabMenuSectionProvider` and register the entries your addon needs. Providers are discovered automatically from `Code/Addons`; no central registration is required.

## Add a tab

Create a Razor panel for the tab's content. A minimal panel can live alongside the rest of your addon code:

```razor
@namespace Dxura.RP.Game.UI
@inherits PanelComponent

<div class="p-6">
	<h1>My addon</h1>
	<p>This is a custom tab.</p>
</div>
```

Then implement a provider and register the panel. `Id` must be unique across the complete menu, including other addons. Prefix it with your addon identifier to avoid collisions.

```csharp
namespace Dxura.RP.Game.UI;

public sealed class MyAddonTabMenuProvider : ITabMenuSectionProvider
{
	public void RegisterTabMenuSections( TabMenuSectionRegistry registry )
	{
		registry.Register( new TabMenuSectionDefinition
		{
			Id = "my-addon.home",
			Label = "My addon",
			Icon = "extension",
			PanelType = typeof( MyAddonTab ),
			Order = 70
		} );
	}
}
```

`Label` may be plain text or a localisation token such as `"#my_addon.tab.home"`. `Icon` is a [Material Icons](https://fonts.google.com/icons) icon name. The panel type must inherit from `Panel` (Razor components that inherit `PanelComponent` do).

Entries are sorted by placement, then `Order`, then label. Normal entries appear in the main navigation; use `Placement = TabMenuEntryPlacement.Bottom` to place one in the footer.

## Control when a tab is available

Use `CanShow` to hide an entry dynamically. Use the two disabled flags when the entry should remain visible but unavailable to a dead or restricted player:

```csharp
registry.Register( new TabMenuSectionDefinition
{
	Id = "my-addon.management",
	Label = "Management",
	Icon = "admin_panel_settings",
	PanelType = typeof( ManagementTab ),
	Order = 80,
	DisabledWhenDead = true,
	DisabledWhenRestricted = true,
	CanShow = () => Player.Local.IsValid()
		&& RankSystem.HasPermission( Player.Local.SteamId, Permission.HandleTickets )
} );
```

`CanShow` is evaluated while the menu is open, so it can safely depend on the local player's current state.

## Add an action instead of a tab

An entry without `PanelType` is an action. It can either copy text to the clipboard, run code when clicked, or do both:

```csharp
registry.Register( new TabMenuSectionDefinition
{
	Id = "my-addon.website",
	Label = "Community website",
	Icon = "language",
	Placement = TabMenuEntryPlacement.Bottom,
	Order = 30,
	CopyText = () => "https://example.com",
	CopiedLabel = "Link copied"
} );
```

For custom behaviour, use `OnClick`. The context exposes the menu panel and `CloseMenu`, which you can call after opening another UI:

```csharp
OnClick = context =>
{
	// Run your addon action here.
	context.CloseMenu();
}
```

## Remove or replace default entries

Native DXRP entries are registered before addon providers. Call `Remove` with their id to remove one from the menu:

```csharp
public void RegisterTabMenuSections( TabMenuSectionRegistry registry )
{
	registry.Remove( "dashboard" );
	registry.Remove( "credits" );
	registry.Remove( "support" );
}
```

The built-in ids are:

| Id | Entry |
| --- | --- |
| `dashboard` | Dashboard |
| `rules` | Rules |
| `players` | Players |
| `jobs` | Jobs |
| `market` | Market |
| `governance` | Governance |
| `faction` | Faction |
| `credits` | Credits (footer) |
| `discord` | Discord action (footer) |
| `support` | Support action (footer) |

To replace an entry, remove it first, then register a new definition with the same id:

```csharp
registry.Remove( "dashboard" );

registry.Register( new TabMenuSectionDefinition
{
	Id = "dashboard",
	Label = "Welcome",
	Icon = "home",
	PanelType = typeof( MyDashboardTab ),
	Order = 0
} );
```

`Remove` returns `false` when no matching entry exists. This can happen when another provider has already removed it, so avoid assuming that a removed entry is still available.

The registry is rebuilt when the tab menu starts and each time it is opened. Keep registration inside `RegisterTabMenuSections`; do not store the `registry` instance or try to modify it later.
