using Dxura.RP.Game.Commands;

namespace Dxura.RP.Game.UI;

/// <summary>Registers DXRP's built-in selected-player actions through the same API addons use.</summary>
public sealed class NativePlayerActionProvider : IPlayerActionProvider
{
	private const string AdminGroup = "admin";

	public void RegisterPlayerActions( PlayerActionRegistry registry )
	{
		RegisterPrimaryActions( registry );
		RegisterTextInputActions( registry );
	}

	private static void RegisterPrimaryActions( PlayerActionRegistry registry )
	{
		registry.Register( new PlayerActionDefinition
		{
			Id = "party-invite",
			Label = "#party.invite_action",
			Order = 0,
			CanShow = context => PartySystem.Instance is { } partySystem &&
			                           partySystem.IsValid() &&
			                           RankSystem.HasLocalPermission( Permission.CommandParty ) &&
			                           IsOtherPlayer( context ) &&
			                           context.TargetPlayer.IsConnected &&
			                           !partySystem.IsInParty( context.TargetPlayer.SteamId ) &&
			                           (!partySystem.IsInParty( context.LocalPlayer.SteamId ) || partySystem.IsLeader( context.LocalPlayer.SteamId )),
			OnExecute = context =>
			{
				PartySystem.Instance?.RequestInvite( context.TargetPlayer.SteamId.ToString() );
				return true;
			}
		} );

		RegisterTargetCommand( registry, "teleport-to", "#tabmenu.players.teleport_to", GotoCommand.Name, "btn-secondary", 10 );
		RegisterTargetCommand( registry, "bring", "#tabmenu.players.bring", BringCommand.Name, "btn-secondary", 20 );
		RegisterTargetCommand( registry, "return", "#tabmenu.players.return", ReturnCommand.Name, "btn-secondary", 30 );

		registry.Register( new PlayerActionDefinition
		{
			Id = "spectate",
			Label = "Spectate",
			Order = 40,
			Command = SpectateCommand.Name,
			CanShow = context => IsOtherPlayer( context ) && RankSystem.CanLocalTarget( context.TargetPlayer.SteamId )
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "unwanted",
			Label = "#tabmenu.players.unwanted",
			Order = 50,
			CloseMenuAfterExecute = false,
			CanShow = context => IsOtherPlayer( context ) &&
			                     context.LocalPlayer.Job.IsGovernmentRole() &&
			                     context.TargetPlayer.HasStatus( Constants.WantedStatus ),
			OnExecute = ExecuteUnwanted
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "force-screenshot",
			Label = "#tabmenu.players.screenshot",
			ButtonClass = "btn-bad",
			Order = 60,
			CloseMenuAfterExecute = false,
			CanShow = context => IsOtherPlayer( context ) &&
			                     RankSystem.HasLocalPermission( Permission.ForceScreenshot ) &&
			                     !string.IsNullOrEmpty( ServerApiLink.Current?.TenantId ),
			OnExecute = ExecuteForceScreenshot
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "view-pocket",
			Label = "#tabmenu.players.pocket.view",
			ButtonClass = "btn-secondary",
			Order = 70,
			CloseMenuAfterExecute = false,
			CanShow = _ => RankSystem.HasLocalPermission( Permission.ViewPocket ),
			OnExecute = ExecuteViewPocket
		} );
	}

	private static void RegisterTextInputActions( PlayerActionRegistry registry )
	{
		registry.Register( new PlayerActionDefinition
		{
			Id = "message",
			Label = "#tabmenu.players.message",
			Placement = PlayerActionPlacement.TextInputInline,
			Order = 0,
			Command = MsgCommand.Name,
			RequiresText = true,
			RequiredTextPhrase = "#generic.message",
			BlurTextAfterExecute = true,
			CanShow = IsOtherPlayer,
			CommandArguments = context => [$"\"{context.TargetPlayer.DisplayName}\"", context.Text]
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "wanted",
			Label = "#tabmenu.players.wanted",
			Placement = PlayerActionPlacement.TextInputBelow,
			Order = 10,
			RequiresText = true,
			BlurTextAfterExecute = true,
			CanShow = context => IsOtherPlayer( context ) &&
			                     context.LocalPlayer.Job.IsGovernmentRole() &&
			                     !context.TargetPlayer.HasStatus( Constants.WantedStatus ),
			OnExecute = ExecuteWanted
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "warrant",
			Label = "#tabmenu.players.action.warrant",
			Placement = PlayerActionPlacement.TextInputBelow,
			Order = 20,
			RequiresText = true,
			BlurTextAfterExecute = true,
			CanShow = context => IsOtherPlayer( context ) &&
			                     context.LocalPlayer.Job.IsGovernmentRole() &&
			                     !context.LocalPlayer.Job.IsMayoralRole() &&
			                     !context.TargetPlayer.HasStatus( Constants.WarrantStatus ) &&
			                     !context.TargetPlayer.Job.IsGovernmentRole(),
			OnExecute = ExecuteWarrant
		} );

		registry.Register( new PlayerActionDefinition
		{
			Id = "demote",
			Label = "#tabmenu.players.action.demote",
			ButtonClass = "btn-warn",
			Group = AdminGroup,
			Placement = PlayerActionPlacement.TextInputBelow,
			Order = 100,
			CloseMenuAfterExecute = false,
			RequiresText = true,
			BlurTextAfterExecute = true,
			CanShow = context => IsOtherPlayer( context ) &&
			                     Config.Current.Game.DemoteEnabled &&
			                     context.LocalPlayer.PlayTime >= Config.Current.Game.DemoteMinPlaytime &&
			                     !context.TargetPlayer.Job.IsSameJob( GameModeJobs.Default ) &&
			                     context.TargetPlayer.Job.Demotable,
			OnExecute = ExecuteDemote
		} );

		RegisterModerationCommand( registry, "jail", "#tabmenu.players.jail_10m", "jail", Permission.PlayerJail, "btn-warn", 110,
			context => [context.TargetPlayer.SteamId.ToString(), "10m", context.Text] );
		RegisterModerationCommand( registry, "ban", "#tabmenu.players.ban_1d", "ban", Permission.PlayerBan, "btn-bad", 120,
			context => [context.TargetPlayer.SteamId.ToString(), "1d", context.Text] );
		RegisterModerationCommand( registry, "warn", "#tabmenu.players.warn", "warn", Permission.PlayerWarn, "btn-warn", 130,
			context => [context.TargetPlayer.SteamId.ToString(), context.Text] );

		registry.Register( new PlayerActionDefinition
		{
			Id = "kick",
			Label = "#tabmenu.players.kick",
			ButtonClass = "btn-bad",
			Group = AdminGroup,
			Placement = PlayerActionPlacement.TextInputBelow,
			Order = 140,
			RequiresText = true,
			BlurTextAfterExecute = true,
			CanShow = context => CanModerate( context, Permission.PlayerKick ),
			OnExecute = context =>
			{
				AdminSystem.Instance.KickPlayerHost( context.TargetPlayer.SteamId, context.Text );
				Notify.Success( Language.GetPhrase( "tabmenu.players.kicked" ) );
				return true;
			}
		} );
	}

	private static void RegisterTargetCommand( PlayerActionRegistry registry, string id, string label, string command, string buttonClass, int order )
	{
		registry.Register( new PlayerActionDefinition
		{
			Id = id,
			Label = label,
			ButtonClass = buttonClass,
			Order = order,
			Command = command,
			CanShow = IsOtherPlayer
		} );
	}

	private static void RegisterModerationCommand(
		PlayerActionRegistry registry,
		string id,
		string label,
		string command,
		Permission permission,
		string buttonClass,
		int order,
		Func<PlayerActionContext, string[]> arguments )
	{
		registry.Register( new PlayerActionDefinition
		{
			Id = id,
			Label = label,
			ButtonClass = buttonClass,
			Group = AdminGroup,
			Placement = PlayerActionPlacement.TextInputBelow,
			Order = order,
			Command = command,
			CommandArguments = arguments,
			RequiresText = true,
			BlurTextAfterExecute = true,
			CanShow = context => CanModerate( context, permission )
		} );
	}

	private static bool IsOtherPlayer( PlayerActionContext context ) => !context.TargetPlayer.IsLocalPlayer;

	private static bool CanModerate( PlayerActionContext context, Permission permission ) =>
		IsOtherPlayer( context ) &&
		RankSystem.CanLocalTarget( context.TargetPlayer.SteamId ) &&
		RankSystem.HasLocalPermission( permission );

	private static bool ExecuteUnwanted( PlayerActionContext context )
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "wanted", Config.Current.Game.WantedCooldown, true ) )
		{
			return false;
		}

		Governance.Current.Unwanted( context.TargetPlayer.SteamId );
		context.CloseMenu();
		return true;
	}

	private static bool ExecuteForceScreenshot( PlayerActionContext context )
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "screenshot:force", Config.Current.Game.ActionCooldown ) )
		{
			Notify.Cooldown( "screenshot:force" );
			return false;
		}

		AdminSystem.Instance.ForceScreenshotHost( context.TargetPlayer.SteamId );
		Notify.Success( "#tabmenu.players.screenshot_forced" );
		context.CloseMenu();
		return true;
	}

	private static bool ExecuteViewPocket( PlayerActionContext context )
	{
		var pocketView = context.Panel.Descendants.OfType<PlayerPocketView>().FirstOrDefault();
		if ( pocketView == null )
		{
			return false;
		}

		pocketView.RequestPocketContents();
		return true;
	}

	private static bool ExecuteWanted( PlayerActionContext context )
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "wanted", Config.Current.Game.WantedCooldown, true ) )
		{
			return false;
		}

		Governance.Current.Wanted( context.TargetPlayer.SteamId, context.Text );
		return true;
	}

	private static bool ExecuteWarrant( PlayerActionContext context )
	{
		if ( Cooldown.Current.CheckAndStartCooldown( "warrant", Config.Current.Game.WarrantCooldown, true ) )
		{
			return false;
		}

		Governance.Current.Warrant( context.TargetPlayer.SteamId, Governance.WarrantAction.Request, context.Text );
		return true;
	}

	private static bool ExecuteDemote( PlayerActionContext context )
	{
		var target = context.TargetPlayer;
		if ( Cooldown.Current.CheckAndStartCooldown( $"demote:{target.SteamId}:{target.Job.Id}", Config.Current.Game.VoteCooldown, true ) ||
		     Cooldown.Current.CheckAndStartCooldown( "vote", Config.Current.Game.VoteCooldown, true ) )
		{
			return false;
		}

		Game.Chat.Current.SubmitPlayerChat( $"Demote {target.DisplayName} for {context.Text}", MessageType.GlobalChat );
		VoteSystem.Instance.StartVoteHost( target.SteamId, VoteType.DemotePlayer, customData: target.Job.Id.ToString() );
		return true;
	}
}
