namespace Dxura.RP.Shared;

public class FactionMemberDto
{
	public long PlayerId { get; set; }
	public Guid? RoleId { get; set; }
	public string Name { get; set; } = null!;

#if ASPNETCORE
	public static FactionMemberDto FromEntity( Domain.Entities.TenantPlayer entity ) => new()
	{
		PlayerId = entity.Id,
		RoleId = entity.FactionRoleId,
		Name = entity.RpName ?? entity.Player?.Name ?? entity.Id.ToString()
	};
#endif
}
