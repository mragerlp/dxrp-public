using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class GagCommand : ICommand
{
	public string Command => "gag";
	public string Help => Language.GetPhrase( "command.gag.help" );
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.PlayerGag];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( args.Length < 3 )
		{
			caller.SendMessage( Language.GetPhrase( "command.gag.usage" ) );
			caller.SendMessage( Language.GetPhrase( "command.gag.duration_examples" ) );
			return true;
		}

		var targetIdentifier = args[0];
		var durationStr = args[1];
		var reason = string.Join( " ", args.Skip( 2 ) ).Trim();
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.gag.usage" ) );
			return true;
		}

		var duration = CommandHelper.ParseDuration( durationStr );
		if ( duration == null )
		{
			caller.SendMessage( Language.GetPhrase( "command.gag.invalid_duration" ) );
			return true;
		}

		var targetPlayer = CommandHelper.ResolvePlayer( caller, targetIdentifier );
		if ( !targetPlayer.IsValid() )
			return true;

		if ( !RankSystem.CanTarget( caller.SteamId, targetPlayer.SteamId ) )
		{
			caller.SendMessage( "#command.errors.higher_rank" );
			return true;
		}

		_ = ApplyGag( caller, targetPlayer, reason, duration.Value, durationStr );
		return true;
	}

	private static async global::System.Threading.Tasks.Task ApplyGag(
		Player caller,
		Player target,
		string reason,
		TimeSpan duration,
		string durationDisplay )
	{
		var callerSteamId = caller.SteamId;
		var callerSteamName = caller.SteamName;
		var callerDisplayName = caller.DisplayName;
		var targetSteamId = target.SteamId;
		var targetSteamName = target.SteamName;
		var targetDisplayName = target.DisplayName;
		var succeeded = await ServerApiClient.SanctionPlayer( targetSteamId, new CreateSanctionDto
		{
			Reason = reason,
			Notes = $"Gagged by {callerSteamName} ({callerSteamId}) via chat command for {durationDisplay}.",
			Type = SanctionType.Gag,
			Duration = duration
		}, callerSteamId );

		await GameTask.MainThread();
		if ( !succeeded )
		{
			if ( caller.IsValid() )
			{
				caller.Error( "#generic.error" );
			}
			return;
		}

		if ( caller.IsValid() )
		{
			caller.Success( string.Format( Language.GetPhrase( "command.gag.success" ), targetDisplayName, durationDisplay, reason ) );
		}

		Log.Info( $"[COMMAND] {callerDisplayName} ({callerSteamId}) gagged {targetDisplayName} ({targetSteamId}) for {durationDisplay}: {reason}" );
		_ = ServerApiClient.Audit( "Gag", $"{callerSteamName} ({callerSteamId}) gagged {targetSteamName} ({targetSteamId}) for {durationDisplay}: {reason}", callerSteamId );
	}
}
