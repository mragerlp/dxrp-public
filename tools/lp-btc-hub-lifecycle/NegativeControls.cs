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

    // Returns 0 when every check passed, 1 otherwise, 2 if the subject would not compile.
    public static int Run(string root)
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
}
