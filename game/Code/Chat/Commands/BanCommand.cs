using Dxura.RP.Shared;

namespace Dxura.RP.Game.Commands;

public class BanCommand : ICommand
{
	public string Command => "ban";
	public string Help => Language.GetPhrase( "command.ban.help" );
	public bool IsUsableWhileDead => true;
	public Permission[] RequiredPermissions => [Permission.PlayerBan];

	public bool ExecuteHost( Player caller, string[] args, string raw )
	{
		if ( args.Length < 3 )
		{
			caller.SendMessage( Language.GetPhrase( "command.ban.usage" ) );
			caller.SendMessage( Language.GetPhrase( "command.ban.duration_examples" ) );
			return true;
		}

		var targetIdentifier = args[0];
		var durationStr = args[1];
		var reason = string.Join( " ", args.Skip( 2 ) ).Trim();
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			caller.SendMessage( Language.GetPhrase( "command.ban.usage" ) );
			return true;
		}

		// Parse duration
		var permanent = IsPermanentDuration( durationStr );
		var duration = CommandHelper.ParseDuration( durationStr );
		if ( duration == null && !permanent )
		{
			caller.SendMessage( Language.GetPhrase( "command.ban.invalid_duration" ) );
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
		var durationDisplay = permanent
			? Language.GetPhrase( "command.ban.duration_permanent" )
			: string.Format( Language.GetPhrase( "command.ban.duration_temporary" ), durationStr );

		_ = ApplyBan( caller, targetPlayer, reason, duration, durationDisplay );
		return true;
	}

	private static async global::System.Threading.Tasks.Task ApplyBan(
		Player caller,
		Player target,
		string reason,
		TimeSpan? duration,
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
			Notes = $"Banned by {callerSteamName} ({callerSteamId}) via chat command.",
			Type = SanctionType.Ban,
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

		var liveTarget = GameUtils.GetPlayerById( targetSteamId );
		if ( liveTarget.IsValid() && liveTarget.Connection != null )
		{
			GameNetworkManager.Instance.KickPlayer( liveTarget.Connection, reason, isBan: true );
		}

		if ( caller.IsValid() )
		{
			caller.Success( string.Format( Language.GetPhrase( "command.ban.success" ), targetDisplayName, durationDisplay, reason ) );
		}

		Log.Info( $"[COMMAND] {callerDisplayName} ({callerSteamId}) banned {targetDisplayName} ({targetSteamId}) {durationDisplay}: {reason}" );
		_ = ServerApiClient.Audit( "Ban", $"{callerSteamName} ({callerSteamId}) banned {targetSteamName} ({targetSteamId}) {durationDisplay}: {reason}", callerSteamId );
	}

	private static bool IsPermanentDuration( string input )
	{
		return input.Equals( "permanent", StringComparison.OrdinalIgnoreCase )
		       || input.Equals( "perm", StringComparison.OrdinalIgnoreCase );
	}
}
