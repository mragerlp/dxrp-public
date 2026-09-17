using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class SetHealthCommand : ICommand
{
	private const float MaximumHealth = 1_000_000f;

	public string Command => "sethealth";
	public string Help => "Set a player's health to a specific value, ignoring max health limits (Staff only)";
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.CommandSetHealth];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		// Validate arguments
		if ( args.Length < 2 )
		{
			caller.SendMessage( Language.GetPhrase( "command.sethealth.usage" ) );
			return true;
		}

		// Parse the target identifier (username or Steam ID)
		var targetIdentifier = args[0];
		
		// Parse the health amount
		if ( !float.TryParse( args[1], out var healthAmount ) || !float.IsFinite( healthAmount ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.sethealth.invalid_amount" ) );
			return true;
		}

		// Validate health amount (must be positive)
		if ( healthAmount <= 0 || healthAmount > MaximumHealth )
		{
			caller.SendMessage( Language.GetPhrase( "command.sethealth.must_positive" ) );
			return true;
		}

		var targetPlayer = CommandHelper.ResolvePlayer( caller, targetIdentifier );
		if ( !targetPlayer.IsValid() )
		{
			return true;
		}

		if ( targetPlayer.SteamId != caller.SteamId && !RankSystem.CanTarget( caller.SteamId, targetPlayer.SteamId ) )
		{
			caller.SendMessage( "#command.errors.higher_rank" );
			return true;
		}

		// Check if target has a health component
		if ( !targetPlayer.HealthComponent.IsValid() )
		{
			caller.SendMessage( string.Format( Language.GetPhrase( "command.sethealth.no_health" ), targetPlayer.DisplayName ) );
			return true;
		}

		// Spawn first: SpawnHost restores the component to max health, so assigning before it would
		// silently overwrite the requested value while the success/audit text still claimed otherwise.
		if ( targetPlayer.HealthComponent.State == LifeState.Dead )
		{
			targetPlayer.SpawnHost( inPlace: true );
			if ( !targetPlayer.HealthComponent.IsValid() )
			{
				caller.SendMessage( string.Format( Language.GetPhrase( "command.sethealth.no_health" ), targetPlayer.DisplayName ) );
				return true;
			}
		}

		// Set the player's health after any revive, ignoring normal max-health constraints.
		targetPlayer.HealthComponent.Health = healthAmount;

		// Notify the caller
		caller.SendMessage( string.Format( Language.GetPhrase( "command.sethealth.set" ), targetPlayer.DisplayName, healthAmount ) );

		// Notify the target player
		targetPlayer.SendMessage( string.Format( Language.GetPhrase( "command.sethealth.set_target" ), healthAmount ) );

		// Log the action
		Log.Info( $"[COMMAND] {caller.DisplayName} ({caller.SteamId}) set {targetPlayer.DisplayName} ({targetPlayer.SteamId})'s health to {healthAmount}" );

		// Log to Discord
		_ = ServerApiClient.Audit( "SetHealth", $"{caller.SteamName} ({caller.SteamId}) set {targetPlayer.SteamName} ({targetPlayer.SteamId})'s health to {healthAmount}", caller.SteamId );

		return true;
	}
}
