using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class WarnCommand : ICommand
{
	public string Command => "warn";
	public string Help => Language.GetPhrase( "command.warn.help" );
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.PlayerWarn];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( args.Length < 2 )
		{
			caller.SendMessage( Language.GetPhrase( "command.warn.usage" ) );
			return true;
		}

		var targetIdentifier = args[0];
		var reason = string.Join( " ", args.Skip( 1 ) ).Trim();
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.warn.usage" ) );
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

		_ = ApplyWarning( caller, targetPlayer, reason );
		return true;
	}

	private static async global::System.Threading.Tasks.Task ApplyWarning( Player caller, Player target, string reason )
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
			Notes = $"Warned by {callerSteamName} ({callerSteamId}) via chat command.",
			Type = SanctionType.Warning
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
			caller.Success( string.Format( Language.GetPhrase( "command.warn.success" ), targetDisplayName, reason ) );
		}

		Log.Info( $"[COMMAND] {callerDisplayName} ({callerSteamId}) warned {targetDisplayName} ({targetSteamId}): {reason}" );
		_ = ServerApiClient.Audit( "Warn", $"{callerSteamName} ({callerSteamId}) warned {targetSteamName} ({targetSteamId}): {reason}", callerSteamId );
	}
}
