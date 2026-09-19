using Dxura.RP.Shared;
using LifePunch.DXRP.Addons.StaffMenu;

int cases = 0;
void Check(bool condition, string title)
{
    if(!condition) throw new Exception("FAIL: " + title);
    cases++;
    Console.WriteLine("PASS: " + title);
}
var a = Guid.Parse("10000000-0000-0000-0000-000000000001");
var b = Guid.Parse("10000000-0000-0000-0000-000000000002");
var c = Guid.Parse("10000000-0000-0000-0000-000000000003");
var d = Guid.Parse("10000000-0000-0000-0000-000000000004");
var unknown = Guid.NewGuid();
RankDto Rank(Guid id, string name, int order, uint color, bool isDefault = false) => new()
{
    Id = id, Name = name, Order = order, Color = color, IsDefault = isDefault,
    Permissions = ["*", "!player.ban"], InheritsFromId = c,
    Flags = RankFlags.ShowInChat, ServerIds = [a, b]
};
var system = RankSystem.Instance;
system.Ranks[a] = Rank(a, "Shared name", 9, 0x12ABCDEF);
system.Ranks[b] = Rank(b, "Shared name", 9, 0x00FF00);
system.Ranks[c] = Rank(c, "None", 0, 0xFFFFFF, true);
system.Ranks[d] = Rank(d, "\u0001\u0002", 4, 0x123456);
system.Assignments[1] = [a, b, b, unknown];
system.Assignments[2] = [a];
system.Assignments[3] = [d];
GameUtils.Players = [new(1), new(1), new(2), new(3, false), new(4)];

var snapshot = system.GetRanksSnapshot();
Check(snapshot.Count == 4 && ((ICollection<RankDto>)snapshot).IsReadOnly, "snapshot includes every definition and collection is read-only");
var copied = snapshot.Single(x => x.Id == a);
Check(!ReferenceEquals(copied, system.Ranks[a]) && !ReferenceEquals(copied.Permissions, system.Ranks[a].Permissions) && !ReferenceEquals(copied.ServerIds, system.Ranks[a].ServerIds), "DTO and nested mutable lists are detached");
Check(copied.Id == a && copied.Name == "Shared name" && copied.Order == 9 && copied.Color == 0x12ABCDEF && copied.InheritsFromId == c && copied.Flags == RankFlags.ShowInChat && copied.ServerIds.SequenceEqual(new[]{a,b}), "snapshot retains every DTO field");
copied.Permissions.Clear(); copied.ServerIds.Clear();
Check(system.Ranks[a].Permissions.Count == 2 && system.Ranks[a].ServerIds.Count == 2, "mutating returned nested lists cannot change authoritative data");
system.Ranks[a].Permissions.Add("player.kick");
Check(copied.Permissions.Count == 0, "later authoritative list changes cannot alter old snapshot");
system.Ranks[a].Permissions.Remove("player.kick");

var rows = LpParityHost.ObservedRanks();
Check(rows.Count == 4 && rows.Select(x => x.Id).Distinct().Count() == 4, "all definitions included with distinct full GUID identity");
Check(rows.Count(x => x.Name == "Shared name") == 2, "duplicate display names never collapse");
Check(rows.Single(x => x.Id == a).HoldersOnline == 2 && rows.Single(x => x.Id == b).HoldersOnline == 1, "primary and secondary assignments counted once per distinct online player");
Check(rows.Single(x => x.Id == c).HoldersOnline == 0 && rows.Single(x => x.Id == d).HoldersOnline == 0, "zero-holder and default-fallback ranks remain visible without invented assignments");
Check(rows.Single(x => x.Id == d).Name == "Unnamed rank", "sanitized blank name does not drop a valid rank definition");
Check(rows.Single(x => x.Id == a).ColorHex == "#ABCDEF", "rank color is masked to six safe hex digits");
Check(rows.Single(x => x.Id == a).PermissionCount == 2 && rows.Single(x => x.Id == a).HasWildcard, "permission entry count includes denies and wildcard is explicit metadata");
Check(rows.Select(x => x.Id).SequenceEqual(new[]{a,b,d,c}), "ordering uses descending order then name then full GUID tie-break");
StaffMenuHost.Allowed = false;
Check(LpParityHost.ObservedRanks().Count == 0, "adapter itself withholds rows when rank-view permission is revoked");
StaffMenuHost.Allowed = true;
Check(LpParityHost.ObservedRanks().Count == 4, "regrant reads fresh definitions without a stale denied cache");
LpParityHost.IsLinked = false;
Check(!LpParityHost.RanksAreAvailable && LpParityHost.ObservedRanks().Count == 0 && LpParityHost.RanksUnavailableReason.Contains("not linked"), "unlinked runtime cannot label fallback ranks as Portal data");
LpParityHost.IsLinked = true; LpParityHost.IsReady = false;
Check(!LpParityHost.RanksAreAvailable && LpParityHost.ObservedRanks().Count == 0 && LpParityHost.RanksUnavailableReason.Contains("Waiting"), "pending config yields unavailable instead of a rank roster");
LpParityHost.IsReady = true; RankSystem.Instance = null!;
Check(!LpParityHost.RanksAreAvailable && LpParityHost.ObservedRanks().Count == 0, "missing rank system fails closed");
RankSystem.Instance = system;
system.Ranks[b] = Rank(b, "Changed rank", 20, 0x010203);
Check(LpParityHost.ObservedRanks()[0].Id == b && LpParityHost.ObservedRanks()[0].ColorHex == "#010203", "fresh read reflects rank name order and color updates");
system.Ranks.Clear();
Check(LpParityHost.RanksAreAvailable && LpParityHost.RanksAreComplete && LpParityHost.ObservedRanks().Count == 0, "received empty snapshot remains distinct from unavailable source");
Check(!PackageParityHost.RanksAreAvailable && !PackageParityHost.RanksAreComplete && PackageParityHost.ObservedRanks().Count == 0 && PackageParityHost.RanksUnavailableReason.Contains("parent"), "older published parent is explicitly unavailable rather than simulated");
Console.WriteLine($"Completed {cases} source-extracted checks; engine transport and remote UI are not covered.");