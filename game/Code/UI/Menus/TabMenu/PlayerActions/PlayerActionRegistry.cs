namespace Dxura.RP.Game.UI;

public enum PlayerActionPlacement
{
	Primary,
	TextInputInline,
	TextInputBelow
}

/// <summary>Runtime context passed to selected-player actions.</summary>
public sealed class PlayerActionContext
{
	public required Panel Panel { get; init; }
	public required Player LocalPlayer { get; init; }
	public required Player TargetPlayer { get; init; }
	public required Action CloseMenu { get; init; }
	public required string Text { get; init; }
	public required Func<string, bool> EnsureText { get; init; }
	public required Action BlurText { get; init; }
}

/// <summary>
/// Describes a selected-player action. Actions may execute callbacks or dispatch registered commands
/// through the normal host-authoritative permission and cooldown path.
/// </summary>
public sealed class PlayerActionDefinition
{
	public required string Id { get; init; }
	public required string Label { get; init; }
	public string ButtonClass { get; init; } = "btn-primary";
	public string? Group { get; init; }
	public int Order { get; init; }
	public PlayerActionPlacement Placement { get; init; }
	public bool CloseMenuAfterExecute { get; init; } = true;
	public bool RequiresText { get; init; }
	public string RequiredTextPhrase { get; init; } = "#generic.reason";
	public bool BlurTextAfterExecute { get; init; }
	public Func<PlayerActionContext, bool>? CanShow { get; init; }
	public Func<PlayerActionContext, bool>? OnExecute { get; init; }
	public string? Command { get; init; }
	public Func<PlayerActionContext, string[]>? CommandArguments { get; init; }

	internal bool IsValid => OnExecute != null || !string.IsNullOrWhiteSpace( Command );

	public bool IsVisible( PlayerActionContext context )
	{
		try
		{
			if ( !context.LocalPlayer.IsValid() || !context.TargetPlayer.IsValid() || CanShow?.Invoke( context ) == false )
			{
				return false;
			}

			if ( string.IsNullOrWhiteSpace( Command ) )
			{
				return true;
			}

			return Game.Chat.Current is { } chat &&
			       chat.TryGetCommand( Command, out var command ) &&
			       command != null &&
			       chat.CanAccessCommand( context.LocalPlayer, command );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to evaluate player action '{Id}': {ex.Message}" );
			return false;
		}
	}

	public void Execute( PlayerActionContext context )
	{
		if ( !IsVisible( context ) || (RequiresText && !context.EnsureText( RequiredTextPhrase )) )
		{
			return;
		}

		try
		{
			var executed = OnExecute?.Invoke( context ) ?? ExecuteCommand( context );
			if ( !executed )
			{
				return;
			}

			if ( BlurTextAfterExecute )
			{
				context.BlurText();
			}

			if ( CloseMenuAfterExecute )
			{
				context.CloseMenu();
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to execute player action '{Id}': {ex.Message}" );
		}
	}

	private bool ExecuteCommand( PlayerActionContext context )
	{
		if ( string.IsNullOrWhiteSpace( Command ) || Game.Chat.Current is not { } chat )
		{
			return false;
		}

		chat.ExecuteCommandHost( Command, CommandArguments?.Invoke( context ) ?? [context.TargetPlayer.SteamId.ToString()] );
		return true;
	}
}

/// <summary>Implemented by addons that contribute actions to the selected-player view.</summary>
public interface IPlayerActionProvider
{
	void RegisterPlayerActions( PlayerActionRegistry registry );
}

/// <summary>Discovers and collates native and addon-provided selected-player actions.</summary>
public sealed class PlayerActionRegistry
{
	private static IReadOnlyList<PlayerActionDefinition>? _actions;

	private readonly List<PlayerActionDefinition> _registeredActions = [];
	private readonly HashSet<string> _registeredIds = new( StringComparer.OrdinalIgnoreCase );

	public static IReadOnlyList<PlayerActionDefinition> Actions => _actions ??= BuildActions();

	public static void Reload() => _actions = null;

	public void Register( PlayerActionDefinition action )
	{
		if ( string.IsNullOrWhiteSpace( action.Id ) || !action.IsValid )
		{
			Log.Warning( "Ignoring invalid player action definition." );
			return;
		}

		if ( !_registeredIds.Add( action.Id ) )
		{
			Log.Warning( $"Ignoring duplicate player action id '{action.Id}'." );
			return;
		}

		_registeredActions.Add( action );
	}

	public bool Remove( string id )
	{
		var action = _registeredActions.FirstOrDefault( x => string.Equals( x.Id, id, StringComparison.OrdinalIgnoreCase ) );
		if ( action == null )
		{
			return false;
		}

		_registeredActions.Remove( action );
		_registeredIds.Remove( action.Id );
		return true;
	}

	private static IReadOnlyList<PlayerActionDefinition> BuildActions()
	{
		var registry = new PlayerActionRegistry();
		foreach ( var type in TypeLibrary.GetTypes<IPlayerActionProvider>().Where( t => !t.IsAbstract && t.TargetType != null ) )
		{
			try
			{
				TypeLibrary.Create<IPlayerActionProvider>( type.TargetType )?.RegisterPlayerActions( registry );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to register player actions from {type.Name}: {ex.Message}" );
			}
		}

		return registry._registeredActions
			.OrderBy( action => action.Placement )
			.ThenBy( action => action.Order )
			.ThenBy( action => action.Label )
			.ToArray();
	}
}
