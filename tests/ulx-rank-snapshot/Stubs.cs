using Dxura.RP.Shared;
public partial class RankSystem
{
    public static RankSystem Instance = new();
    public Dictionary<Guid, RankDto> Ranks { get; } = new();
    public Dictionary<long, List<Guid>> Assignments { get; } = new();
    public List<Guid> GetPlayerRankIds(long steamId) => Assignments.TryGetValue(steamId, out var value) ? value : [];
}
public static class RankValidity { public static bool IsValid(this RankSystem? value) => value != null; }
public sealed record Player(long SteamId, bool Valid = true) { public bool IsValid() => Valid; }
public static class GameUtils { public static List<Player> Players = []; }
public static class StaffMenuHost
{
    public static bool Allowed = true;
    public static bool CanView(string permission)
    {
        if(permission != "portal.rank.view") throw new Exception("Wrong rank permission identifier");
        return Allowed;
    }
}
internal static partial class LpParityHost
{
    public static bool IsLinked = true;
    public static bool IsReady = true;
    public static bool CanViewRanks() => StaffMenuHost.CanView("portal.rank.view");
}