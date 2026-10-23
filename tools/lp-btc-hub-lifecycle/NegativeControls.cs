using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Negative controls for the hub link paths (HUB-WIRING-1 U3). Same idea as the lifecycle checks in Program.cs:
// the real production method bodies are extracted from the tree under <root>, compiled against small offline
// stand-ins for the engine types, and driven directly. Point <root> at a folder holding the origin/develop blobs
// (README has the commands) and every "NEG" check must FAIL; point it at the current tree and every check must PASS.
// A "CTL" check is a regression guard that passes on both, so the table can tell a control from a guard.
// Nothing here proves engine compile, replication or runtime behavior.
internal static class HarnessKit
{
    // All overloads of the named methods, attribute lists dropped, leading/trailing trivia dropped (that also drops
    // the surrounding #if/#endif that wrap whole members). Names absent from the file are skipped: a method added
    // by the slice under test does not exist in the develop blobs, and its absence must read as a failing check.
    public static string Methods(string root, string path, string type, params string[] names)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)));
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == type);
        return string.Join("\n", cls.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => names.Contains(m.Identifier.ValueText))
            .Select(m => m.WithAttributeLists(default).WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
    }

    public static Assembly Compile(string assemblyName, string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(assemblyName, new[] { CSharpSyntaxTree.ParseText(source) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
        using var dll = new MemoryStream();
        var result = compilation.Emit(dll);
        if (!result.Success)
        {
            foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                Console.Error.WriteLine(diagnostic);
            throw new InvalidOperationException(assemblyName + " did not compile");
        }
        return Assembly.Load(dll.ToArray());
    }
}

internal static class NegativeControls
{
    const string Hub = "game/Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/components/LpBitcoinHubEntity.cs";
    const string Rack = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinRackEntity.cs";
    const string Ui = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinUi.cs";
    const string Ownership = "game/Code/Addons/lifepunch/lifepunchcore/LifePunchEntityOwnership.cs";

    const string Terminal = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinTerminalEntity.cs";
    const string PropPhysics = "game/Code/Addons/lifepunch/lifepunchcore/LifePunchPropPhysics.cs";

    // 0 when every check of both groups passed, 1 when any failed, 2 when a subject would not compile.
    public static int Run(string root)
    {
        var link = RunLink(root);
        var terminal = RunTerminal(root);
        return Math.Max(link, terminal);
    }

    static int RunTerminal(string root)
    {
        var source = TerminalTemplate
            .Replace("TERMINAL_METHODS", HarnessKit.Methods(root, Terminal, "LpBitcoinTerminalEntity",
                "OnFixedUpdate", "ApplyGrabGateHost", "HasStuckGrabTags", "TellCallerHubIsOff", "OpenTerminalHost"))
            .Replace("PHYSICS_METHODS", HarnessKit.Methods(root, PropPhysics, "LifePunchPropPhysics",
                "MaintainGrabbablePlaceableProp", "AllowHandsGrabTags", "DenyHandsGrabTags"));
        try
        {
            var assembly = HarnessKit.Compile("HubTerminalSubject", source);
            return (int)assembly.GetType("TermSubject.TerminalControls")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (InvalidOperationException)
        {
            return 2;
        }
    }

    static int RunLink(string root)
    {
        var source = Template
            .Replace("HUB_METHODS", HarnessKit.Methods(root, Hub, "LpBitcoinHubEntity",
                "LinkNearbyRackHost", "LinkRackByTokenHost", "FindHubByOwnerSteamId", "IsDevPreviewHub",
                "RefuseIfOwnerHasAnotherActiveHub", "CanOperateTerminal"))
            .Replace("RACK_METHODS", HarnessKit.Methods(root, Rack, "LpBitcoinRackEntity", "FindNearestUnlinked", "FindAllUnlinked"))
            .Replace("UI_METHODS", HarnessKit.Methods(root, Ui, "LpBitcoinUi", "GetOrCreatePreviewHub", "EnsureSampleRacks", "CreatePreviewRack"))
            .Replace("OWNERSHIP_METHODS", HarnessKit.Methods(root, Ownership, "LifePunchEntityOwnership", "SharesOperator"));
        try
        {
            var assembly = HarnessKit.Compile("HubLinkSubject", source);
            return (int)assembly.GetType("LinkSubject.LinkControls")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (InvalidOperationException)
        {
            return 2;
        }
    }

    const string Template = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace LinkSubject;

public static class Networking { public static bool IsHost = true; }
public static class Game { public static Scene ActiveScene; }
public static class Log { public static void Info(string m) { } public static void Warning(string m) { } }
public static class Rpc { public static Guid CallerId = Guid.NewGuid(); }
public enum LpBitcoinHubAlertKind { TerminalCommand }
public struct Vector3
{
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public float Distance(Vector3 o) => MathF.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y) + (z - o.z) * (z - o.z));
}
public interface IComponent { GameObject GameObject { get; set; } }
public sealed class GameObject
{
    public Guid Id = Guid.NewGuid();
    public string Name = "";
    public Scene Scene;
    public T AddComponent<T>() where T : class, IComponent, new() { var c = new T(); c.GameObject = this; Scene.Components.Add(c); return c; }
}
public sealed class Scene
{
    public List<object> Components = new();
    public IEnumerable<T> GetAllComponents<T>() => Components.OfType<T>().ToList();
    public GameObject CreateObject() => new GameObject { Scene = this };
}
public static class Validity
{
    public static bool IsValid(this LpBitcoinHubEntity h) => h is not null && h.Valid;
    public static bool IsValid(this LpBitcoinRackEntity r) => r is not null && r.Valid;
}
public static class LpBitcoinIdent
{
    public const int PortalMaxRacksPerHub = 3, PortalMaxStandardRacksPerHub = 2, PortalMaxAdvancedRacksPerHub = 1;
    public static bool TryParseLinkRackSlotToken(string t, out bool advanced, out int slot)
    {
        advanced = false; slot = 0;
        if (t == "advancedgpurack") { advanced = true; return true; }
        if (t == "gpurack-1") { slot = 1; return true; }
        if (t == "gpurack-2") { slot = 2; return true; }
        return false;
    }
    public static bool CanLinkToDeclaredSlot(bool advanced, int slot, IReadOnlyList<LpBitcoinRackEntity> linked, out string error) { error = null; return true; }
    public static bool CanLinkRackToHub(LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linked, out string error) { error = null; return true; }
    public static string FormatRackSlotDisplayName(LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linked) => "GPU Rack " + linked.Count;
    public static string FormatDeclaredLinkSlotToken(bool advanced, int slot) => advanced ? "advancedgpurack" : "gpurack-" + slot;
}
public static class LifePunchEntityOwnership
{
    OWNERSHIP_METHODS
}
public sealed class LpBitcoinHubEntity : IComponent
{
    public GameObject GameObject { get; set; }
    public bool Valid = true, IsPowered, HasTerm = true, CanManage = true, CallerIsOwnerStub = true;
    public long Owner;
    public float RackLinkRange = 500f;
    public Vector3 WorldPosition;
    public List<(LpBitcoinHubAlertKind Kind, string Message)> Alerts = new();
    public List<LpBitcoinRackEntity> Linked = new();
    public bool HasLinkedTerminal() => HasTerm;
    public bool CanManageHub(Guid callerId) => CanManage;
    public List<LpBitcoinRackEntity> GetLinkedRacks() => Linked;
    public bool TryBindOwner(Guid callerId) { if (Owner == 0) Owner = 1; return true; }
    public void BindOwnerFromLocalViewer() { if (Owner == 0) Owner = 1; }
    public void ApplyPoweredState(bool on) => IsPowered = on;
    public void RefreshLinkedTerminalScreens() { }
    internal void PushAlertHost(LpBitcoinHubAlertKind kind, string message) => Alerts.Add((kind, message));
    // Stand-in for the real claim recovery (untouched by the slice): an unclaimed item binds to the hub owner for the owner's own caller.
    private bool TryClaimLinkableEquipment(LpBitcoinRackEntity equipment, Guid callerId, out string error)
    {
        error = "claim refused";
        if (LifePunchEntityOwnership.SharesOperator(Owner, equipment.Owner)) return true;
        if (equipment.Owner == 0 && CallerIsOwnerStub) { equipment.Owner = Owner; return true; }
        return false;
    }
    HUB_METHODS
}
public sealed class LpBitcoinRackEntity : IComponent
{
    public GameObject GameObject { get; set; }
    public bool Valid = true, AdvancedRack;
    public Guid LinkedHubId;
    public long Owner;
    public Vector3 WorldPosition;
    public void LinkToHub(LpBitcoinHubEntity hub) { LinkedHubId = hub.GameObject.Id; hub.Linked.Add(this); }
    RACK_METHODS
}
public static class LpBitcoinUi
{
    UI_METHODS
}
public static class LinkControls
{
    static int negTotal, negFail, ctlTotal, ctlFail;
    static void Neg(bool ok, string name) { negTotal++; if (!ok) negFail++; Console.WriteLine((ok ? "PASS NEG " : "FAIL NEG ") + name); }
    static void Ctl(bool ok, string name) { ctlTotal++; if (!ok) ctlFail++; Console.WriteLine((ok ? "PASS CTL " : "FAIL CTL ") + name); }
    static Scene NewScene() { var s = new Scene(); Game.ActiveScene = s; Networking.IsHost = true; return s; }
    static LpBitcoinHubEntity AddHub(Scene s, string name, long owner, bool powered = true, bool terminal = true)
    {
        var go = s.CreateObject(); go.Name = name;
        var h = go.AddComponent<LpBitcoinHubEntity>(); h.Owner = owner; h.IsPowered = powered; h.HasTerm = terminal;
        return h;
    }
    static LpBitcoinRackEntity AddRack(Scene s, long owner, bool advanced = false, float x = 50f)
    {
        var go = s.CreateObject(); go.Name = "rack";
        var r = go.AddComponent<LpBitcoinRackEntity>(); r.Owner = owner; r.AdvancedRack = advanced; r.WorldPosition = new Vector3(x, 0, 0);
        return r;
    }
    static object Call(object o, string name, params object[] args)
        => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
    static bool Said(LpBitcoinHubEntity h, string needle) => h.Alerts.Any(a => a.Message.Contains(needle, StringComparison.OrdinalIgnoreCase));

    public static int Run()
    {
        Scene s; LpBitcoinHubEntity hub, preview, real, other; LpBitcoinRackEntity rack;

        // (a) the two branches HUB-WIRING-1 revived: no terminal, and hub power off, on both link paths.
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: true, terminal: false); Call(hub, "LinkNearbyRackHost");
        Neg(hub.Alerts.Count == 1 && Said(hub, "terminal"), "LinkNearbyRackHost pushes an alert on the no-terminal path");
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: true, terminal: false); Call(hub, "LinkRackByTokenHost", "gpurack-1");
        Neg(hub.Alerts.Count == 1 && Said(hub, "terminal"), "LinkRackByTokenHost pushes an alert on the no-terminal path");
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: false, terminal: true); Call(hub, "LinkNearbyRackHost");
        Neg(hub.Alerts.Count == 1 && Said(hub, "power"), "LinkNearbyRackHost pushes an alert on the power-off path");
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: false, terminal: true); Call(hub, "LinkRackByTokenHost", "gpurack-1");
        Neg(hub.Alerts.Count == 1 && Said(hub, "power"), "LinkRackByTokenHost pushes an alert on the power-off path");
        var silent = true;
        foreach (var powered in new[] { true, false })
        foreach (var terminal in new[] { true, false })
        {
            s = NewScene(); hub = AddHub(s, "Hub", 7, powered, terminal); hub.CanManage = false; AddRack(s, 7);
            Call(hub, "LinkNearbyRackHost"); Call(hub, "LinkRackByTokenHost", "gpurack-1");
            silent &= hub.Alerts.Count == 0 && hub.Linked.Count == 0;
        }
        Ctl(silent, "a caller who cannot manage the hub gets silence and links nothing, in all four power/terminal states");
        s = NewScene(); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkNearbyRackHost");
        Ctl(hub.Linked.Count == 1 && Said(hub, "linked"), "LinkNearbyRackHost still links an owned rack in range");
        s = NewScene(); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkRackByTokenHost", "gpurack-1");
        Ctl(hub.Linked.Count == 1 && Said(hub, "linked"), "LinkRackByTokenHost still links an owned rack in range");

        // (b) preview hubs (LpBitcoinPreview*) are scaffolding, never an operator's real hub.
        s = NewScene(); preview = AddHub(s, "LpBitcoinPreviewHub", 7); real = AddHub(s, "Hub", 7);
        Neg(LpBitcoinHubEntity.FindHubByOwnerSteamId(7) == real, "FindHubByOwnerSteamId returns the real hub when a preview hub with the same owner comes first");
        s = NewScene(); AddHub(s, "LpBitcoinPreviewHub", 7);
        Neg(LpBitcoinHubEntity.FindHubByOwnerSteamId(7) is null, "FindHubByOwnerSteamId finds nothing when only a preview hub carries the owner");
        s = NewScene(); preview = AddHub(s, "LpBitcoinPreviewHub", 7); real = AddHub(s, "Hub", 7);
        Neg(!(bool)Call(real, "RefuseIfOwnerHasAnotherActiveHub"), "a leftover preview hub does not trip the one-hub-per-operator refusal");
        s = NewScene(); real = AddHub(s, "Hub", 7); other = AddHub(s, "Hub2", 7);
        Ctl((bool)Call(real, "RefuseIfOwnerHasAnotherActiveHub") && Said(real, "One active HASHD hub"), "a second real hub of the same owner is still refused");
        s = NewScene(); AddHub(s, "LpBitcoinPreviewHub", 7); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkRackByTokenHost", "gpurack-1");
        Neg(hub.Linked.Count == 1 && !Said(hub, "One active HASHD hub"), "a leftover preview hub no longer blocks the real hub from linking a rack");

        // (c) Owner == 0 racks reach the claim recovery; a rack owned by someone else does not.
        s = NewScene(); hub = AddHub(s, "Hub", 7); rack = AddRack(s, 0);
        Neg(LpBitcoinRackEntity.FindNearestUnlinked(hub, 500f) == rack, "FindNearestUnlinked admits an Owner 0 rack");
        Neg(LpBitcoinRackEntity.FindAllUnlinked(hub, 500f).Count == 1, "FindAllUnlinked admits an Owner 0 rack");
        s = NewScene(); hub = AddHub(s, "Hub", 7); AddRack(s, 9);
        Ctl(LpBitcoinRackEntity.FindNearestUnlinked(hub, 500f) is null && LpBitcoinRackEntity.FindAllUnlinked(hub, 500f).Count == 0, "both finders still skip a rack owned by another operator");
        s = NewScene(); hub = AddHub(s, "Hub", 7); rack = AddRack(s, 7);
        Ctl(LpBitcoinRackEntity.FindNearestUnlinked(hub, 500f) == rack && LpBitcoinRackEntity.FindAllUnlinked(hub, 500f).Count == 1, "both finders still admit the operator's own rack");
        s = NewScene(); hub = AddHub(s, "Hub", 7); rack = AddRack(s, 0); Call(hub, "LinkNearbyRackHost");
        Neg(hub.Linked.Count == 1 && rack.Owner == 7, "an Owner 0 rack is claimed for the hub owner and linked end to end");

        // (d) the dev preview hub is reused, and a real hub still wins.
        s = NewScene(); var first = LpBitcoinUi.GetOrCreatePreviewHub(); var second = LpBitcoinUi.GetOrCreatePreviewHub();
        Neg(second == first && s.GetAllComponents<LpBitcoinHubEntity>().Count() == 1, "GetOrCreatePreviewHub reuses its preview hub instead of minting another");
        s = NewScene(); real = AddHub(s, "Hub", 7);
        Ctl(LpBitcoinUi.GetOrCreatePreviewHub() == real && s.GetAllComponents<LpBitcoinHubEntity>().Count() == 1, "GetOrCreatePreviewHub returns a real hub when one exists");
        s = NewScene(); LpBitcoinUi.GetOrCreatePreviewHub(); var withRacks = LpBitcoinUi.GetOrCreatePreviewHub(true);
        Ctl(withRacks.Linked.Count == 3, "GetOrCreatePreviewHub(withSampleRacks) hands back a hub with the canonical 2 + 1 rack mix");

        Console.WriteLine($"Hub link controls: {negTotal + ctlTotal - negFail - ctlFail}/{negTotal + ctlTotal} checks passed " +
            $"({negFail}/{negTotal} negative controls failing, {ctlFail}/{ctlTotal} regression guards failing)");
        return negFail + ctlFail == 0 ? 0 : 1;
    }
}
""";

    // The terminal's host-side grab gate and menu open (HUB-WIRING-2 U2). LifePunchPropPhysics bodies are the real ones,
    // because the defect is the interplay: Maintain ends by re-adding hands_interact, which undid the gate's Deny.
    const string TerminalTemplate = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace TermSubject;

public static class Networking { public static bool IsHost = true; }
public static class Time { public static float Now; }
public static class Log { public static int Warnings; public static void Info(string m) { } public static void Warning(string m) { Warnings++; } }
public sealed class Connection { }
public static class Rpc { public static Connection Caller = new(); public static Guid CallerId = Guid.NewGuid(); }
public enum LpBitcoinHubAlertKind { TerminalCommand }
public enum FindMode { EverythingInSelf }
public sealed class CollideGuard { }
public static class Constants { public const string HandsInteractTag = "hands_interact"; public const string PocketItemTag = "pocket_item"; }
public sealed class TagSet
{
    public HashSet<string> Set = new();
    public bool Has(string t) => Set.Contains(t);
    public void Add(string t) => Set.Add(t);
    public void Remove(string t) => Set.Remove(t);
}
public sealed class ComponentList { public T Get<T>(FindMode mode) where T : class => null; }
public sealed class GameObject { public Guid Id = Guid.NewGuid(); public TagSet Tags = new(); public ComponentList Components = new(); }
public sealed class GameManager
{
    public static GameManager Instance = new();
    public int TagBroadcasts;
    public void BroadcastTagHost(GameObject go, bool add, params string[] tags)
    {
        TagBroadcasts++;
        foreach (var t in tags) { if (add) go.Tags.Add(t); else go.Tags.Remove(t); }
    }
}
public static class Validity
{
    public static bool IsValid(this GameObject g) => g is not null;
    public static bool IsValid(this CollideGuard c) => c is not null;
    public static bool IsValid(this GameManager m) => m is not null;
    public static bool IsValid(this LpBitcoinHubEntity h) => h is not null;
}
public static class LifePunchMenuInteractGate
{
    public static bool Allowed = true;
    public static bool IsCallerAllowed(Connection caller, GameObject target) => Allowed;
}
public static class LifePunchPropPhysics
{
    public const string GrabbedTag = "grabbed";
    public const string PlayerClipTag = "playerclip";
    public const string NoCollideTag = "no_collide";
    PHYSICS_METHODS
}
public sealed class LpBitcoinHubEntity
{
    public GameObject GameObject = new();
    public bool IsPowered, CanManage = true;
    public List<string> Alerts = new();
    public bool CanManageHub(Guid callerId) => CanManage;
    internal void PushAlertHost(LpBitcoinHubAlertKind kind, string message) => Alerts.Add(message);
}
public class BaseEntity
{
    public GameObject GameObject = new();
    protected virtual void OnFixedUpdate() { }
}
public sealed class LpBitcoinTerminalEntity : BaseEntity
{
    const float HubOffAlertCooldownSeconds = 3f;
    public Guid LinkedHubId;
    public LpBitcoinHubEntity Hub;
    public int Opened;
    int _spawnDropGraceTicks;
    bool? _grabGateMenuGoverns;
    float _hubOffAlertAfter;
    internal LpBitcoinHubEntity FindLinkedHub() => Hub;
    private void OpenTerminal(Guid callerId, Guid hubId) => Opened++;
    TERMINAL_METHODS
}
public static class TerminalControls
{
    static int negTotal, negFail, ctlTotal, ctlFail;
    static void Neg(bool ok, string name) { negTotal++; if (!ok) negFail++; Console.WriteLine((ok ? "PASS NEG " : "FAIL NEG ") + name); }
    static void Ctl(bool ok, string name) { ctlTotal++; if (!ok) ctlFail++; Console.WriteLine((ok ? "PASS CTL " : "FAIL CTL ") + name); }
    static object Call(object o, string name, params object[] args)
        => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
    static void Set(object o, string field, object value)
        => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, value);
    // A terminal as it spawns: grabbable (hands_interact on), host side, past the spawn grace.
    static LpBitcoinTerminalEntity Make(bool linked, bool powered)
    {
        Networking.IsHost = true; GameManager.Instance = new GameManager(); LifePunchMenuInteractGate.Allowed = true;
        var t = new LpBitcoinTerminalEntity();
        t.GameObject.Tags.Add(Constants.HandsInteractTag);
        if (linked) { t.Hub = new LpBitcoinHubEntity { IsPowered = powered }; t.LinkedHubId = t.Hub.GameObject.Id; }
        return t;
    }
    static void Tick(LpBitcoinTerminalEntity t, int n = 1) { for (var i = 0; i < n; i++) Call(t, "OnFixedUpdate"); }
    static bool Hands(LpBitcoinTerminalEntity t) => t.GameObject.Tags.Has(Constants.HandsInteractTag);

    public static int Run()
    {
        LpBitcoinTerminalEntity t;

        // Grab gate: a terminal whose linked hub is powered belongs to the menu, so Hands must not hold it.
        t = Make(true, true); Tick(t, 6);
        Neg(!Hands(t), "a terminal under a powered hub does not keep hands_interact after the gate applies");
        t = Make(true, true); Tick(t, 3); GameManager.Instance.TagBroadcasts = 0; Tick(t, 20);
        Neg(GameManager.Instance.TagBroadcasts == 0, "no tag broadcast per host tick in the menu-governed steady state");
        t = Make(false, false); Tick(t, 3); GameManager.Instance.TagBroadcasts = 0; Tick(t, 20);
        Neg(GameManager.Instance.TagBroadcasts == 0, "no tag broadcast per host tick in the grabbable steady state");
        t = Make(false, false); Tick(t, 6);
        Ctl(Hands(t), "an unlinked terminal stays Hands-grabbable");
        t = Make(true, true); Tick(t, 3); t.GameObject.Tags.Add("grabbed"); Tick(t);
        Neg(!t.GameObject.Tags.Has("grabbed") && !Hands(t), "a stuck grabbed tag is cleared under a powered hub and hands_interact stays off");
        t = Make(true, true); Tick(t, 3); t.GameObject.Tags.Add("no_collide"); Tick(t);
        Neg(!t.GameObject.Tags.Has("no_collide") && !Hands(t), "a stuck no_collide tag is cleared under a powered hub and hands_interact stays off");
        t = Make(false, false); Tick(t, 3); t.GameObject.Tags.Add("grabbed"); t.GameObject.Tags.Add("playerclip"); Tick(t);
        Ctl(!t.GameObject.Tags.Has("grabbed") && !t.GameObject.Tags.Has("playerclip") && Hands(t), "stuck grabbed and playerclip tags are still cleared while the terminal is grabbable");
        t = Make(false, false); Tick(t, 3);
        t.Hub = new LpBitcoinHubEntity { IsPowered = true }; t.LinkedHubId = t.Hub.GameObject.Id; Tick(t, 4);
        Neg(!Hands(t), "when the hub powers on the terminal sheds hands_interact and keeps it shed");
        t = Make(true, true); Tick(t, 3); t.Hub.IsPowered = false; Tick(t, 4);
        Ctl(Hands(t), "when the hub powers off the terminal becomes grabbable again");
        t = Make(true, true); Set(t, "_spawnDropGraceTicks", 3); Tick(t, 3);
        var heldDuringGrace = Hands(t); Tick(t);
        Ctl(heldDuringGrace && !Hands(t), "the spawn grace keeps its upkeep and the gate starts the tick after it");

        // Opening a terminal whose hub is off tells a manager, once per cooldown, and never opens anything.
        t = Make(true, false); Call(t, "OpenTerminalHost");
        Neg(t.Hub.Alerts.Count == 1 && t.Hub.Alerts[0].Contains("power", StringComparison.OrdinalIgnoreCase) && t.Opened == 0, "an unpowered hub answers a terminal open with an alert and opens nothing");
        t = Make(true, false); t.Hub.CanManage = false; Call(t, "OpenTerminalHost");
        Ctl(t.Hub.Alerts.Count == 0 && t.Opened == 0, "a caller who cannot manage the hub writes nothing into its alert feed");
        t = Make(true, false); Time.Now = 100f; for (var i = 0; i < 5; i++) Call(t, "OpenTerminalHost"); var burst = t.Hub.Alerts.Count; Time.Now = 110f; Call(t, "OpenTerminalHost");
        Neg(burst == 1 && t.Hub.Alerts.Count == 2, "repeat presses inside the cooldown add one alert, and a later press adds another");
        t = Make(true, true); Call(t, "OpenTerminalHost");
        Ctl(t.Opened == 1 && t.Hub.Alerts.Count == 0, "a powered hub still opens the terminal with no alert");
        t = Make(true, false); LifePunchMenuInteractGate.Allowed = false; Call(t, "OpenTerminalHost"); LifePunchMenuInteractGate.Allowed = true;
        Ctl(t.Hub.Alerts.Count == 0 && t.Opened == 0, "a caller the range gate refuses gets nothing, not even an alert");
        t = Make(false, false); var warnings = Log.Warnings; Call(t, "OpenTerminalHost");
        Ctl(t.Opened == 0 && Log.Warnings == warnings + 1, "an unlinked terminal still only logs its host warning");

        Console.WriteLine($"Terminal controls: {negTotal + ctlTotal - negFail - ctlFail}/{negTotal + ctlTotal} checks passed " +
            $"({negFail}/{negTotal} negative controls failing, {ctlFail}/{ctlTotal} regression guards failing)");
        return negFail + ctlFail == 0 ? 0 : 1;
    }
}
""";
}
