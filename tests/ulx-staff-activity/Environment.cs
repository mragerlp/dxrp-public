namespace LifePunch.DXRP.Addons.StaffMenu;

// Minimal environment shapes; production projection methods and taxonomy are extracted verbatim.
public readonly record struct StaffMenuPlayer(long SteamId, string Name, int GroupOrder);
public readonly record struct StaffStateFlag(string Label, bool Illegitimate);
public readonly record struct StaffAuditEntry(string Action, long PlayerSteamId, string Player, string Description);

internal static class StaffMenuHost
{
    public static bool Allowed = true;
    public static readonly List<StaffAuditEntry> Events = new();
    public static readonly List<StaffMenuPlayer> Players = new();
    public static readonly HashSet<long> StaffIds = new();
    public static readonly Dictionary<long, IReadOnlyList<StaffStateFlag>> Flags = new();
    public static int DataReads;

    public static bool CanViewAudit() => Allowed;
    public static IReadOnlyList<StaffMenuPlayer> OnlinePlayers()
    {
        DataReads++;
        return Players;
    }

    public static bool IsStaffMember(long steamId) => StaffIds.Contains(steamId);
    public static IReadOnlyList<StaffStateFlag> GetStateFlags(long steamId)
        => Flags.TryGetValue(steamId, out var value) ? value : Array.Empty<StaffStateFlag>();

    public static IReadOnlyList<StaffAuditEntry> GetAuditEntries(
        string playerId, string entityId, bool matchDescription = false,
        IReadOnlyCollection<string>? actions = null)
    {
        DataReads++;
        // Retain the generic accessor's name/ID substring behavior deliberately. The projection
        // must enforce exact numeric actor identity itself instead of relying on this search.
        var selected = actions is null ? null : new HashSet<string>(actions, StringComparer.OrdinalIgnoreCase);
        return Events.Where(row =>
            (selected is null || selected.Contains(row.Action)) &&
            (playerId.Length == 0 || row.PlayerSteamId.ToString().Contains(playerId, StringComparison.OrdinalIgnoreCase)
                || row.Player.Contains(playerId, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    public static void Reset()
    {
        Allowed = true;
        Events.Clear();
        Players.Clear();
        StaffIds.Clear();
        Flags.Clear();
        DataReads = 0;
    }
}
