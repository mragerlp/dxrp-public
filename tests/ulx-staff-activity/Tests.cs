using LifePunch.DXRP.Addons.StaffMenu;

const long Rager = 76561198051390817L;
const long Owner = 76561198103223564L;
const long Civilian = 76561198000000001L;
var passed = 0;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

void Run(string name, Action body)
{
    StaffMenuHost.Reset();
    body();
    passed++;
    Console.WriteLine($"PASS {name}");
}

void Player(long id, string name, int order, bool staff = true)
{
    StaffMenuHost.Players.Add(new(id, name, order));
    if (staff) StaffMenuHost.StaffIds.Add(id);
}

void Event(string action, long actor, string? name = null, string description = "")
    => StaffMenuHost.Events.Add(new(action, actor, name ?? $"Player {actor}", description));

Run("Chat and BankDeposit do not inflate staff activity", () =>
{
    Player(Rager, "Mr. Rager", 6);
    for (var i = 0; i < 5; i++)
    {
        Event("Chat", Rager);
        Event("BankDeposit", Rager, description: "Salary");
    }
    Check(StaffObserveHost.StaffActivity().Single().RingEventCount == 0,
        "Ten ordinary events were presented as staff actions.");
    Check(StaffObserveHost.StaffActionLog(Rager).Count == 0, "Ordinary events entered the staff log.");
});

Run("Taxonomy is shared and case insensitive", () =>
{
    Player(Rager, "Mr. Rager", 6);
    Event("Freeze", Rager);
    Event("warn", Rager);
    Event("Chat", Rager);
    Event("BankDeposit", Rager);
    Check(StaffObserveHost.StaffActivity().Single().RingEventCount == 2, "Staff action count is wrong.");
    Check(StaffObserveHost.StaffActionLog(Rager).Select(row => row.Action).SequenceEqual(new[] { "Freeze", "warn" }),
        "Staff taxonomy projection changed event order or included unrelated actions.");
});

Run("A spoofed actor name does not match another numeric actor", () =>
{
    Event("Freeze", Owner, $"Spoof {Rager}");
    Event("Warn", Rager, "Mr. Rager");
    var rows = StaffObserveHost.StaffActionLog(Rager);
    Check(rows.Count == 1 && rows[0].PlayerSteamId == Rager, "Actor-name substring bypassed numeric filtering.");
});

Run("Target description and actor ID prefixes are not actor matches", () =>
{
    Event("Teleport", Owner, "Bloodwave", $"Brought {Rager}");
    Event("Freeze", Rager, "Mr. Rager");
    Check(StaffObserveHost.StaffActionLog(Rager).Count == 1, "Target text was treated as the actor.");
    Check(StaffObserveHost.StaffActionLog(76561198L).Count == 0, "A partial actor ID matched a complete ID.");
});

Run("Permission denial returns empty without reading audit or roster", () =>
{
    Player(Rager, "Mr. Rager", 6);
    Event("Freeze", Rager);
    StaffMenuHost.Allowed = false;
    Check(StaffObserveHost.StaffActionLog().Count == 0, "Denied viewer received staff log.");
    Check(StaffObserveHost.StaffActivity().Count == 0, "Denied viewer received roster activity.");
    Check(StaffMenuHost.DataReads == 0, "Denied projection read protected sources.");
});

Run("Each online staff count agrees with its actor-filtered log", () =>
{
    Player(Rager, "Mr. Rager", 6);
    Player(Owner, "Bloodwave", 10);
    Player(Civilian, "Player", 0, staff: false);
    Event("Freeze", Rager);
    Event("Chat", Rager);
    Event("Warn", Owner);
    Event("Kick", Owner);
    Event("BankDeposit", Owner);
    Event("SetArmor", Civilian);
    Event("StaffTicketResolved", 0, "system");
    var rows = StaffObserveHost.StaffActivity();
    Check(rows.Count == 2 && rows.All(row => row.Player.SteamId != Civilian), "Civilian entered staff roster.");
    foreach (var row in rows)
        Check(row.RingEventCount == StaffObserveHost.StaffActionLog(row.Player.SteamId).Count,
            $"Count/log disagreement for {row.Player.SteamId}.");
    Check(rows.Select(row => row.Player.SteamId).SequenceEqual(new[] { Owner, Rager }), "Rank ordering changed.");
});

Run("Display caps do not silently truncate the projection count", () =>
{
    Player(Rager, "Mr. Rager", 6);
    for (var i = 0; i < 81; i++) Event("Freeze", Rager);
    Check(StaffObserveHost.StaffActivity().Single().RingEventCount == 81, "Projection applied a UI row cap.");
    Check(StaffObserveHost.StaffActionLog(Rager).Count == 81, "Actor projection silently lost events.");
});

Run("Empty authorized activity retains staff and current flags", () =>
{
    Player(Rager, "Mr. Rager", 6);
    var flags = new[] { new StaffStateFlag("Frozen", false) };
    StaffMenuHost.Flags[Rager] = flags;
    var row = StaffObserveHost.StaffActivity().Single();
    Check(row.RingEventCount == 0 && row.Flags.SequenceEqual(flags), "Zero activity removed staff or changed flags.");
});

Console.WriteLine($"PASS {passed}/{passed} staff activity regressions");
