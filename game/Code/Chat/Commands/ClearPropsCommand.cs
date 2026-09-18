using Dxura.RP.Shared;
namespace Dxura.RP.Game.Commands;

public class ClearPropsCommand : ICommand
{
	public string Command => "clearprops";
	public string Help => "/clearprops - Clear your props, or a player's props (if permitted)";

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( !caller.IsValid() )
		{
			return false;
		}

		// Staff+ can clear another player's props
		if ( args.Length > 0 )
		{
			if ( !RankSystem.HasPermission( caller.SteamId, Permission.CommandClearProps ) )
			{
				caller.SendMessage( "#generic.permission" );
				return true;
			}

			var target = CommandHelper.ResolvePlayer( caller, string.Join( " ", args ) );
			if ( !target.IsValid() )
			{
				return true;
			}

			if ( target.SteamId != caller.SteamId && !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
			{
				caller.SendMessage( "#command.errors.higher_rank" );
				return true;
			}

			CleanupSystem.Current.CleanupConstructs( target.SteamId, ConstructType.Prop );
			caller.Success( string.Format( Language.GetPhrase( "command.clearprops.cleared_for" ), target.DisplayName ) );
			Log.Info( $"Staff {caller.DisplayName} cleared props for {target.DisplayName}" );
			_ = ServerApiClient.Audit( "ClearProps", $"{caller.SteamName} ({caller.SteamId}) cleared props for {target.SteamName} ({target.SteamId})", caller.SteamId );

			return true;
		}

		// Self-clear with cooldown
		if ( Cooldown.Current.CheckAndStartCooldown( $"{caller.SteamId}:clearprops", Config.Current.Game.UtilityClearCooldown ) )
		{
			caller.Error( "#generic.wait" );
			return true;
		}

		CleanupSystem.Current.CleanupConstructs( caller.SteamId, ConstructType.Prop );
		caller.Success( Language.GetPhrase( "command.clearprops.cleared" ) );
		Log.Info( $"Player {caller.DisplayName} cleared their props" );

		return true;
	}
}
