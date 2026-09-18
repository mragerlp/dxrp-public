using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class KickCommand : ICommand
{
	public string Command => "kick";
	public string Help => Language.GetPhrase( "command.kick.help" );
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.PlayerKick];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( args.Length < 2 )
		{
			caller.SendMessage( Language.GetPhrase( "command.kick.usage" ) );
			return true;
		}

		var targetIdentifier = args[0];
		var reason = string.Join( " ", args.Skip( 1 ) ).Trim();
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.kick.usage" ) );
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

		_ = ApplyKick( caller, targetPlayer, reason );
		return true;
	}

	private static async global::System.Threading.Tasks.Task ApplyKick( Player caller, Player target, string reason )
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
			Notes = $"Kicked by {callerSteamName} ({callerSteamId}) via chat command.",
			Type = SanctionType.Kick
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

		var liveTarget = GameUtils.GetPlayerById( targetSteamId );
		if ( liveTarget.IsValid() && liveTarget.Connection != null )
		{
			GameNetworkManager.Instance.KickPlayer( liveTarget.Connection, reason );
		}

		if ( caller.IsValid() )
		{
			caller.Success( string.Format( Language.GetPhrase( "command.kick.success" ), targetDisplayName, reason ) );
		}

		Log.Info( $"[COMMAND] {callerDisplayName} ({callerSteamId}) kicked {targetDisplayName} ({targetSteamId}): {reason}" );
		_ = ServerApiClient.Audit( "Kick", $"{callerSteamName} ({callerSteamId}) kicked {targetSteamName} ({targetSteamId}): {reason}", callerSteamId );
	}
}
