using Dxura.RP.Shared;
using Sandbox.Diagnostics;
using System.Threading.Tasks;

namespace Dxura.RP.Game;

public struct FactionInfo
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Tag { get; set; }
	public string? Description { get; set; }
	public uint Balance { get; set; }
	public uint Level { get; set; }
	public uint Experience { get; set; }
	public uint MaxMembers { get; set; }
	public int MemberCount { get; set; }
}

public struct FactionRoleInfo
{
	public Guid Id { get; set; }
	public Guid FactionId { get; set; }
	public string Name { get; set; }
	public string? Description { get; set; }
	public int Order { get; set; }
	public FactionPermission Permission { get; set; }
}

public struct FactionMemberInfo
{
	public long PlayerId { get; set; }
	public Guid FactionId { get; set; }
	public Guid? RoleId { get; set; }
	public string Name { get; set; }
}

public class FactionSystem : SingletonComponent<FactionSystem>
{
	public const int FactionNameMaxLength = 64;
	public const int FactionTagMaxLength = 3;
	public const int FactionDescriptionMaxLength = 500;
	public const int RoleNameMaxLength = 64;
	public const int RoleDescriptionMaxLength = 500;

	private const FactionPermission AllPermissions =
		FactionPermission.InviteMember |
		FactionPermission.KickMember |
		FactionPermission.ManageFaction |
		FactionPermission.SetRanks |
		FactionPermission.WithdrawMoney;

	[Sync( SyncFlags.FromHost )] public NetDictionary<Guid, FactionInfo> Factions { get; set; } = new();
	[Sync( SyncFlags.FromHost )] public NetDictionary<Guid, FactionRoleInfo> FactionRoles { get; set; } = new();
	[Sync( SyncFlags.FromHost )] public NetDictionary<long, FactionMemberInfo> FactionMembers { get; set; } = new();
	[Sync( SyncFlags.FromHost )] public bool IsLoaded { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool LoadFailed { get; private set; }
	[Sync( SyncFlags.FromHost )] public int Revision { get; private set; }

	private bool _isLoading;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			_ = LoadFactions();
		}
	}

	private async Task LoadFactions()
	{
		Assert.True( Networking.IsHost );
		if ( _isLoading )
		{
			return;
		}

		_isLoading = true;
		IsLoaded = false;
		LoadFailed = false;

		var factions = await ServerApiClient.GetAllFactions();
		await GameTask.MainThread();

		try
		{
			if ( factions == null )
			{
				LoadFailed = true;
				return;
			}

			Factions.Clear();
			FactionRoles.Clear();
			FactionMembers.Clear();

			foreach ( var faction in factions )
			{
				ApplyFaction( faction, false );
			}

			IsLoaded = true;
			Revision++;
		}
		finally
		{
			_isLoading = false;
		}
	}

	private void ApplyFaction( FactionDto faction, bool incrementRevision = true )
	{
		Factions[faction.Id] = new FactionInfo
		{
			Id = faction.Id,
			Name = faction.Name,
			Tag = faction.Tag,
			Description = faction.Description,
			Balance = faction.Balance,
			Level = faction.Level,
			Experience = faction.Experience,
			MaxMembers = faction.MaxMembers,
			MemberCount = faction.MemberCount
		};

		var oldRoleIds = FactionRoles
			.Where( entry => entry.Value.FactionId == faction.Id )
			.Select( entry => entry.Key )
			.ToList();
		foreach ( var roleId in oldRoleIds )
		{
			FactionRoles.Remove( roleId );
		}

		foreach ( var role in faction.Roles )
		{
			FactionRoles[role.Id] = new FactionRoleInfo
			{
				Id = role.Id,
				FactionId = faction.Id,
				Name = role.Name,
				Description = role.Description,
				Order = role.Order,
				Permission = role.Permission
			};
		}

		var oldMemberIds = FactionMembers
			.Where( entry => entry.Value.FactionId == faction.Id )
			.Select( entry => entry.Key )
			.ToList();
		foreach ( var playerId in oldMemberIds )
		{
			FactionMembers.Remove( playerId );
		}

		foreach ( var member in faction.Members )
		{
			FactionMembers[member.PlayerId] = new FactionMemberInfo
			{
				PlayerId = member.PlayerId,
				FactionId = faction.Id,
				RoleId = member.RoleId,
				Name = member.Name
			};
		}

		if ( incrementRevision )
		{
			Revision++;
		}
	}

	private void RemoveFactionState( Guid factionId )
	{
		Factions.Remove( factionId );

		foreach ( var roleId in FactionRoles
			         .Where( entry => entry.Value.FactionId == factionId )
			         .Select( entry => entry.Key )
			         .ToList() )
		{
			FactionRoles.Remove( roleId );
		}

		foreach ( var playerId in FactionMembers
			         .Where( entry => entry.Value.FactionId == factionId )
			         .Select( entry => entry.Key )
			         .ToList() )
		{
			FactionMembers.Remove( playerId );
		}

		Revision++;
	}

	public IEnumerable<FactionRoleInfo> GetFactionRoles( Guid factionId )
	{
		return FactionRoles.Values.Where( role => role.FactionId == factionId );
	}

	public IEnumerable<FactionMemberInfo> GetFactionMembers( Guid factionId )
	{
		return FactionMembers.Values.Where( member => member.FactionId == factionId );
	}

	private bool HasFactionPermission( Player player, Guid factionId, FactionPermission permission )
	{
		if ( !player.IsInFaction || player.FactionId != factionId )
		{
			return false;
		}

		var role = player.GetFactionRole();
		return role != null &&
		       role.Value.FactionId == factionId &&
		       role.Value.Permission.HasFlag( permission );
	}

	private static bool IsValidTag( string tag )
	{
		return tag.Length is > 0 and <= FactionTagMaxLength &&
		       tag.All( character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' );
	}

	private static bool IsValidPermission( FactionPermission permission )
	{
		return (permission & ~AllPermissions) == 0;
	}

	private static string? NormalizeOptionalText( string? value )
	{
		return string.IsNullOrWhiteSpace( value ) ? null : value.Trim();
	}

	public async Task RefreshFaction( Guid factionId )
	{
		Assert.True( Networking.IsHost );

		var faction = await ServerApiClient.GetFaction( factionId );
		if ( faction == null )
		{
			return;
		}

		await GameTask.MainThread();
		ApplyFaction( faction );
	}

	[Rpc.Host]
	public void RefreshFactionsHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:refresh", Config.Current.Game.ActionLongCooldown ) )
		{
			return;
		}

		if ( GameUtils.GetPlayerByConnectionId( callerId ) == null )
		{
			return;
		}

		_ = LoadFactions();
	}

	[Rpc.Host]
	public void CreateFactionHost( string name, string tag, string? description )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:create", Config.Current.Game.ActionLongCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null )
		{
			return;
		}

		name = name?.Trim() ?? string.Empty;
		tag = tag?.Trim().ToUpperInvariant() ?? string.Empty;
		description = NormalizeOptionalText( description );

		if ( caller.IsInFaction )
		{
			caller.Error( "#faction.already_in" );
			return;
		}

		if ( name.Length is < 1 or > FactionNameMaxLength )
		{
			caller.Error( "#faction.name_invalid" );
			return;
		}

		if ( !IsValidTag( tag ) )
		{
			caller.Error( "#faction.tag_invalid" );
			return;
		}

		if ( description?.Length > FactionDescriptionMaxLength )
		{
			caller.Error( "#faction.description_invalid" );
			return;
		}

		var cost = Config.Current.Game.FactionCreateCost;
		_ = GameTask.RunInThreadAsync( async () =>
		{
			if ( !await caller.ChargeHost( cost, "Created a faction" ) )
			{
				return;
			}

			var faction = await ServerApiClient.CreateFaction( new CreateFactionDto
			{
				Name = name,
				Tag = tag,
				Description = description
			} );

			if ( faction == null )
			{
				await RefundFactionCreation( caller, cost, name, "faction create request failed" );
				return;
			}

			var leaderRole = await ServerApiClient.CreateFactionRole( faction.Id, new CreateFactionRoleDto
			{
				Name = "Leader",
				Order = 0,
				Permission = AllPermissions
			} );

			if ( leaderRole == null )
			{
				await ServerApiClient.DeleteFaction( faction.Id );
				await RefundFactionCreation( caller, cost, name, "leader role creation failed" );
				return;
			}

			var memberAdded = await ServerApiClient.AddFactionMember( faction.Id, new AddFactionMemberDto
			{
				PlayerId = caller.SteamId,
				RoleId = leaderRole.Id
			} );

			if ( !memberAdded )
			{
				await ServerApiClient.DeleteFaction( faction.Id );
				await RefundFactionCreation( caller, cost, name, "creator membership failed" );
				return;
			}

			var completeFaction = await ServerApiClient.GetFaction( faction.Id );
			await GameTask.MainThread();

			if ( completeFaction != null )
			{
				ApplyFaction( completeFaction );
			}
			else
			{
				faction.MemberCount = 1;
				faction.Roles = [leaderRole];
				faction.Members = [new FactionMemberDto
				{
					PlayerId = caller.SteamId,
					RoleId = leaderRole.Id,
					Name = caller.DisplayName
				}];
				ApplyFaction( faction );
			}

			if ( caller.IsValid() )
			{
				caller.FactionId = faction.Id;
				caller.FactionRoleId = leaderRole.Id;
				caller.PendingFactionInviteId = null;
				caller.PendingFactionInviterId = 0;
				caller.Success( "#faction.create_success" );
			}

			Log.Info( $"[Faction] {caller.DisplayName} ({caller.SteamId}) created faction '{faction.Name}' [{faction.Tag}] (ID: {faction.Id})" );
			Chat.Current.BroadcastSystemText( string.Format(
				Language.GetPhrase( "faction.created" ), caller.DisplayName, faction.Name, faction.Tag ) );
		} );
	}

	private static async Task RefundFactionCreation( Player caller, uint cost, string name, string reason )
	{
		Log.Warning( $"[Faction] Failed to create faction '{name}' for {caller.DisplayName} ({caller.SteamId}): {reason}" );
		var refunded = await caller.PayHost( cost, "Faction creation refund" );
		await GameTask.MainThread();

		if ( caller.IsValid() )
		{
			caller.Error( refunded ? "#faction.create_failed_refunded" : "#faction.create_failed" );
		}

		if ( !refunded )
		{
			Log.Error( $"[Faction] Failed to refund {cost} to {caller.SteamId} after faction creation failure" );
		}
	}

	[Rpc.Host]
	public void UpdateFactionHost( Guid factionId, string? description )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:update", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		description = NormalizeOptionalText( description );
		if ( caller == null || !Factions.ContainsKey( factionId ) )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.ManageFaction ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		if ( description?.Length > FactionDescriptionMaxLength )
		{
			caller.Error( "#faction.description_invalid" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var faction = await ServerApiClient.UpdateFaction( factionId, new UpdateFactionDto
			{
				Description = description
			} );

			await GameTask.MainThread();
			if ( faction == null )
			{
				caller.Error( "#faction.update_failed" );
				return;
			}

			ApplyFaction( faction );
			caller.Success( "#faction.update_success" );
		} );
	}

	[Rpc.Host]
	public void DeleteFactionHost( Guid factionId )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:delete", Config.Current.Game.ActionLongCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null || !Factions.ContainsKey( factionId ) )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.ManageFaction ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var deleted = await ServerApiClient.DeleteFaction( factionId );
			await GameTask.MainThread();

			if ( !deleted )
			{
				caller.Error( "#faction.delete_failed" );
				return;
			}

			RemoveFactionState( factionId );
			foreach ( var player in GameUtils.Players.Where( player => player.IsValid() ) )
			{
				if ( player.FactionId == factionId )
				{
					player.FactionId = null;
					player.FactionRoleId = null;
				}

				if ( player.PendingFactionInviteId == factionId )
				{
					player.PendingFactionInviteId = null;
					player.PendingFactionInviterId = 0;
				}
			}

			caller.Success( "#faction.delete_success" );
		} );
	}

	[Rpc.Host]
	public void CreateFactionRoleHost( Guid factionId, string name, string? description, int order, FactionPermission permission )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:role:create", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		name = name?.Trim() ?? string.Empty;
		description = NormalizeOptionalText( description );
		if ( caller == null || !Factions.ContainsKey( factionId ) )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.SetRanks ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		if ( name.Length is < 1 or > RoleNameMaxLength ||
		     description?.Length > RoleDescriptionMaxLength ||
		     order < 0 ||
		     !IsValidPermission( permission ) )
		{
			caller.Error( "#faction.role_invalid" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var role = await ServerApiClient.CreateFactionRole( factionId, new CreateFactionRoleDto
			{
				Name = name,
				Description = description,
				Order = order,
				Permission = permission
			} );

			await GameTask.MainThread();
			if ( role == null )
			{
				caller.Error( "#faction.role_create_failed" );
				return;
			}

			FactionRoles[role.Id] = new FactionRoleInfo
			{
				Id = role.Id,
				FactionId = factionId,
				Name = role.Name,
				Description = role.Description,
				Order = role.Order,
				Permission = role.Permission
			};
			Revision++;
			caller.Success( "#faction.role_create_success" );
		} );
	}

	[Rpc.Host]
	public void UpdateFactionRoleHost( Guid factionId, Guid roleId, string name, string? description, int order, FactionPermission permission )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:role:update", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		name = name?.Trim() ?? string.Empty;
		description = description?.Trim() ?? string.Empty;
		if ( caller == null ||
		     !FactionRoles.TryGetValue( roleId, out var existingRole ) ||
		     existingRole.FactionId != factionId )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.SetRanks ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		if ( name.Length is < 1 or > RoleNameMaxLength ||
		     description.Length > RoleDescriptionMaxLength ||
		     order < 0 ||
		     !IsValidPermission( permission ) )
		{
			caller.Error( "#faction.role_invalid" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var role = await ServerApiClient.UpdateFactionRole( factionId, roleId, new UpdateFactionRoleDto
			{
				Name = name,
				Description = description,
				Order = order,
				Permission = permission
			} );

			await GameTask.MainThread();
			if ( role == null )
			{
				caller.Error( "#faction.role_update_failed" );
				return;
			}

			FactionRoles[role.Id] = new FactionRoleInfo
			{
				Id = role.Id,
				FactionId = factionId,
				Name = role.Name,
				Description = role.Description,
				Order = role.Order,
				Permission = role.Permission
			};
			Revision++;
			caller.Success( "#faction.role_update_success" );
		} );
	}

	[Rpc.Host]
	public void InviteFactionMemberHost( Guid factionId, long targetSteamId )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:invite", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null || !Factions.TryGetValue( factionId, out var faction ) )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.InviteMember ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		var target = GameUtils.GetPlayerById( targetSteamId );
		if ( target == null || !target.IsValid() || !target.IsConnected || target.IsInFaction )
		{
			caller.Error( "#faction.player_not_found" );
			return;
		}

		target.PendingFactionInviteId = factionId;
		target.PendingFactionInviterId = caller.SteamId;
		target.Info( string.Format( Language.GetPhrase( "faction.invite.received" ), caller.DisplayName, faction.Name ) );
		caller.Success( string.Format( Language.GetPhrase( "faction.invite.sent" ), target.DisplayName ) );
	}

	[Rpc.Host]
	public void RespondFactionInviteHost( bool accept )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:invite:respond", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null || !caller.PendingFactionInviteId.HasValue )
		{
			return;
		}

		var factionId = caller.PendingFactionInviteId.Value;
		var inviterId = caller.PendingFactionInviterId;
		caller.PendingFactionInviteId = null;
		caller.PendingFactionInviterId = 0;

		if ( !accept )
		{
			caller.Info( "#faction.invite.declined" );
			return;
		}

		if ( caller.IsInFaction || !Factions.ContainsKey( factionId ) )
		{
			caller.Error( "#faction.invite.invalid" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var added = await ServerApiClient.AddFactionMember( factionId, new AddFactionMemberDto
			{
				PlayerId = caller.SteamId
			} );

			await GameTask.MainThread();
			if ( !added )
			{
				caller.Error( "#faction.invite.accept_failed" );
				return;
			}

			caller.FactionId = factionId;
			caller.FactionRoleId = null;
			await RefreshFaction( factionId );
			caller.Success( "#faction.invite.accepted" );

			var inviter = GameUtils.GetPlayerById( inviterId );
			if ( inviter.IsValid() )
			{
				inviter.Success( string.Format( Language.GetPhrase( "faction.invite.joined" ), caller.DisplayName ) );
			}
		} );
	}

	[Rpc.Host]
	public void KickFactionMemberHost( Guid factionId, long targetSteamId )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:kick", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null ||
		     !FactionMembers.TryGetValue( targetSteamId, out var member ) ||
		     member.FactionId != factionId )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.KickMember ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		if ( caller.SteamId == targetSteamId )
		{
			caller.Error( "#faction.members.cannot_kick_self" );
			return;
		}

		var target = GameUtils.GetPlayerById( targetSteamId );
		_ = GameTask.RunInThreadAsync( async () =>
		{
			var removed = await ServerApiClient.RemoveFactionMember( factionId, targetSteamId );
			await GameTask.MainThread();

			if ( !removed )
			{
				caller.Error( "#faction.members.kick_failed" );
				return;
			}

			if ( target.IsValid() )
			{
				target.FactionId = null;
				target.FactionRoleId = null;
				target.Warn( "#faction.members.kicked" );
			}

			await RefreshFaction( factionId );
			caller.Success( "#faction.members.kick_success" );
		} );
	}

	[Rpc.Host]
	public void LeaveFactionHost()
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:leave", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null || !caller.FactionId.HasValue )
		{
			return;
		}

		var factionId = caller.FactionId.Value;
		_ = GameTask.RunInThreadAsync( async () =>
		{
			var removed = await ServerApiClient.RemoveFactionMember( factionId, caller.SteamId );
			await GameTask.MainThread();

			if ( !removed )
			{
				caller.Error( "#faction.leave_failed" );
				return;
			}

			caller.FactionId = null;
			caller.FactionRoleId = null;
			await RefreshFaction( factionId );
			caller.Success( "#faction.leave_success" );
		} );
	}

	[Rpc.Host]
	public void SetMemberRoleHost( Guid factionId, long targetSteamId, Guid roleId )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:setrole", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null ||
		     !FactionMembers.TryGetValue( targetSteamId, out var member ) ||
		     member.FactionId != factionId )
		{
			return;
		}

		if ( roleId != Guid.Empty &&
		     (!FactionRoles.TryGetValue( roleId, out var role ) || role.FactionId != factionId) )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.SetRanks ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		var target = GameUtils.GetPlayerById( targetSteamId );
		_ = GameTask.RunInThreadAsync( async () =>
		{
			var updated = await ServerApiClient.AddFactionMember( factionId, new AddFactionMemberDto
			{
				PlayerId = targetSteamId,
				RoleId = roleId == Guid.Empty ? null : roleId
			} );

			await GameTask.MainThread();
			if ( !updated )
			{
				caller.Error( "#faction.members.role_failed" );
				return;
			}

			if ( target.IsValid() )
			{
				target.FactionRoleId = roleId == Guid.Empty ? null : roleId;
			}

			await RefreshFaction( factionId );
			caller.Success( "#faction.members.role_success" );
		} );
	}

	[Rpc.Host]
	public void DeleteFactionRoleHost( Guid factionId, Guid roleId )
	{
		var callerId = Rpc.CallerId;
		if ( Cooldown.Current.CheckAndStartCooldown( $"{callerId}:faction:role:delete", Config.Current.Game.ActionCooldown ) )
		{
			return;
		}

		var caller = GameUtils.GetPlayerByConnectionId( callerId );
		if ( caller == null ||
		     !FactionRoles.TryGetValue( roleId, out var role ) ||
		     role.FactionId != factionId )
		{
			return;
		}

		if ( !HasFactionPermission( caller, factionId, FactionPermission.SetRanks ) )
		{
			caller.Error( "#faction.no_permission" );
			return;
		}

		_ = GameTask.RunInThreadAsync( async () =>
		{
			var deleted = await ServerApiClient.DeleteFactionRole( factionId, roleId );
			await GameTask.MainThread();

			if ( !deleted )
			{
				caller.Error( "#faction.role_delete_failed" );
				return;
			}

			foreach ( var player in GameUtils.Players.Where( player => player.IsValid() && player.FactionRoleId == roleId ) )
			{
				player.FactionRoleId = null;
			}

			await RefreshFaction( factionId );
			caller.Success( "#faction.role_delete_success" );
		} );
	}
}
