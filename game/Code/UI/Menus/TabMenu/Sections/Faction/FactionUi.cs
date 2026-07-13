namespace Dxura.RP.Game.UI;

internal static class FactionUi
{
	public static string FormatPermissions( FactionPermission permissions )
	{
		if ( permissions == FactionPermission.None )
		{
			return Language.GetPhrase( "faction.permissions.none" );
		}

		var labels = new List<string>();
		if ( permissions.HasFlag( FactionPermission.InviteMember ) ) labels.Add( Language.GetPhrase( "faction.permissions.invite" ) );
		if ( permissions.HasFlag( FactionPermission.KickMember ) ) labels.Add( Language.GetPhrase( "faction.permissions.kick" ) );
		if ( permissions.HasFlag( FactionPermission.ManageFaction ) ) labels.Add( Language.GetPhrase( "faction.permissions.manage" ) );
		if ( permissions.HasFlag( FactionPermission.SetRanks ) ) labels.Add( Language.GetPhrase( "faction.permissions.ranks" ) );
		if ( permissions.HasFlag( FactionPermission.WithdrawMoney ) ) labels.Add( Language.GetPhrase( "faction.permissions.withdraw" ) );
		return string.Join( ", ", labels );
	}

	public static string GetMemberName( FactionMemberInfo member )
	{
		var player = GameUtils.GetPlayerById( member.PlayerId );
		return player.IsValid() ? player.DisplayName : member.Name;
	}

	public static bool IsMemberOnline( long playerId )
	{
		var player = GameUtils.GetPlayerById( playerId );
		return player.IsValid() && player.IsConnected;
	}

	public static string GetRoleName( Guid? roleId )
	{
		if ( roleId.HasValue &&
		     FactionSystem.Instance.IsValid() &&
		     FactionSystem.Instance.FactionRoles.TryGetValue( roleId.Value, out var role ) )
		{
			return role.Name;
		}

		return Language.GetPhrase( "faction.members.unassigned" );
	}
}
