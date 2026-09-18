using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class JailCommand : ICommand
{
	public string Command => "jail";
	public string Help => Language.GetPhrase( "command.jail.help" );
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.PlayerJail];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( args.Length < 3 )
		{
			caller.SendMessage( Language.GetPhrase( "command.jail.usage" ) );
			caller.SendMessage( Language.GetPhrase( "command.jail.duration_examples" ) );
			return true;
		}

		var targetIdentifier = args[0];
		var durationStr = args[1];
		var reason = string.Join( " ", args.Skip( 2 ) ).Trim();
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.jail.usage" ) );
			return true;
		}

		var duration = CommandHelper.ParseDuration( durationStr );
		if ( duration == null )
		{
			caller.SendMessage( Language.GetPhrase( "command.jail.invalid_duration" ) );
			return true;
		}

		var targetPlayer = CommandHelper.ResolvePlayer( caller, targetIdentifier );
		if ( !targetPlayer.IsValid() )
			return true;

		if ( caller.SteamId == targetPlayer.SteamId || !RankSystem.CanTarget( caller.SteamId, targetPlayer.SteamId ) )
		{
			caller.SendMessage( "#command.errors.higher_rank" );
			return true;
		}

		_ = ApplyJail( caller, targetPlayer, reason, duration.Value, durationStr );
		return true;
	}

	private static async global::System.Threading.Tasks.Task ApplyJail(
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
			Notes = $"Jailed by {callerSteamName} ({callerSteamId}) via chat command for {durationDisplay}.",
			Type = SanctionType.Jail,
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
			caller.Success( string.Format( Language.GetPhrase( "command.jail.success" ), targetDisplayName, durationDisplay, reason ) );
		}

		Log.Info( $"[COMMAND] {callerDisplayName} ({callerSteamId}) jailed {targetDisplayName} ({targetSteamId}) for {durationDisplay}: {reason}" );
		_ = ServerApiClient.Audit( "Jail", $"{callerSteamName} ({callerSteamId}) jailed {targetSteamName} ({targetSteamId}) for {durationDisplay}: {reason}", callerSteamId );
	}
}
