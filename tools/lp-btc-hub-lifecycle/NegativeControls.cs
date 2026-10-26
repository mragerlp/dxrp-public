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
    // File text, or an empty string when the tree under test does not have the file (an added file is absent from the older
    // blobs, and its absence must read as a failing check rather than stop the run).
    public static string Read(string root, string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
    }

    // The source text of the invocations made inside the members whose member name starts with the prefix, whitespace squeezed.
    public static string CallTexts(string members, string prefix)
    {
        var tree = CSharpSyntaxTree.ParseText("class Probe {\n" + members + "\n}");
        return string.Join(",", tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.ValueText.StartsWith(prefix, StringComparison.Ordinal))
            .Select(i => string.Concat(i.ToString().Where(ch => !char.IsWhiteSpace(ch)))));
    }

    // All overloads of the named methods, attribute lists dropped, leading/trailing trivia dropped (that also drops
    // the surrounding #if/#endif that wrap whole members). Names absent from the file are skipped: a method added
    // by the slice under test does not exist in the develop blobs, and its absence must read as a failing check.
    public static string Methods(string root, string path, string type, params string[] names)
    {
        var tree = CSharpSyntaxTree.ParseText(Read(root, path));
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(c => c.Identifier.ValueText == type);
        if (cls is null)
            return string.Empty;
        return string.Join("\n", cls.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => names.Contains(m.Identifier.ValueText))
            .Select(m => m.WithAttributeLists(default).WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
    }

    // Same, for any member kind: methods (all overloads), fields and expression-bodied properties by name.
    // Used where the subject needs the real constants rather than a copy of their values.
    public static string Members(string root, string path, string type, params string[] names)
    {
        var tree = CSharpSyntaxTree.ParseText(Read(root, path));
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(c => c.Identifier.ValueText == type);
        if (cls is null)
            return string.Empty;
        return string.Join("\n", cls.Members.Where(m => m switch
            {
                MethodDeclarationSyntax x => names.Contains(x.Identifier.ValueText),
                PropertyDeclarationSyntax x => names.Contains(x.Identifier.ValueText),
                FieldDeclarationSyntax x => x.Declaration.Variables.Any(v => names.Contains(v.Identifier.ValueText)),
                _ => false,
            })
            .Select(m => (m is MethodDeclarationSyntax md ? md.WithAttributeLists(default) : m)
                .WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
    }

    // Members of the @code block of a .razor file (the block that runs from the line-start "@code" to the last brace).
    // A name that is absent yields nothing, exactly as in Members.
    public static string RazorMembers(string root, string path, params string[] names)
    {
        var text = Read(root, path);
        var at = text.IndexOf("\n@code", StringComparison.Ordinal);
        if (at < 0)
            return string.Empty;
        var open = text.IndexOf('{', at);
        var close = text.LastIndexOf('}');
        var tree = CSharpSyntaxTree.ParseText("class RazorCode {\n" + text.Substring(open + 1, close - open - 1) + "\n}");
        return SelectMembers(tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(), names);
    }

    // Whole enum / class declarations by name (the purchase envelope types live outside any class).
    public static string Types(string root, string path, params string[] names)
    {
        var tree = CSharpSyntaxTree.ParseText(Read(root, path));
        return string.Join("\n", tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => names.Contains(t.Identifier.ValueText))
            .Select(t => t.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
    }

    // Names of the invocations made inside the named members whose member name starts with the prefix, as name/argument count.
    public static string Calls(string members, string prefix)
    {
        var tree = CSharpSyntaxTree.ParseText("class Probe {\n" + members + "\n}");
        return string.Join(",", tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(i => (Name: i.Expression is MemberAccessExpressionSyntax m ? m.Name.Identifier.ValueText : string.Empty, Args: i.ArgumentList.Arguments.Count))
            .Where(c => c.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(c => c.Name + "/" + c.Args));
    }

    // Identifiers used inside the members, from the given watch list: what a request path must never touch.
    public static string Touches(string members, params string[] watch)
    {
        var tree = CSharpSyntaxTree.ParseText("class Probe {\n" + members + "\n}");
        return string.Join(",", tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(i => i.Identifier.ValueText).Where(watch.Contains).Distinct().OrderBy(n => n, StringComparer.Ordinal));
    }

    static string SelectMembers(ClassDeclarationSyntax cls, string[] names)
        => string.Join("\n", cls.Members.Where(m => m switch
            {
                MethodDeclarationSyntax x => names.Contains(x.Identifier.ValueText),
                PropertyDeclarationSyntax x => names.Contains(x.Identifier.ValueText),
                FieldDeclarationSyntax x => x.Declaration.Variables.Any(v => names.Contains(v.Identifier.ValueText)),
                _ => false,
            })
            .Select(m => (m is MethodDeclarationSyntax md ? md.WithAttributeLists(default) : m)
                .WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));

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
        var gate = RunGate(root);
        var upgrade = RunUpgrade(root);
        var racks = RunRacks(root);
        return Math.Max(Math.Max(link, Math.Max(terminal, gate)), Math.Max(upgrade, racks));
    }

    const string HubUi = "game/Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/ui/hub/";
    const string Skeleton = HubUi + "LpBtcHubSkeleton.razor";
    const string RacksTab = HubUi + "LpBtcHubRacks.razor";
    const string Spy = HubUi + "LpBtcHubCommandSpy.cs";
    const string UiHost = "game/Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/components/LpBtcHubUiHost.cs";
    const string Hashd = "game/Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor";
    const string PurchaseFlow = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinPurchaseFlow.cs";

    // HUB-UPGRADE-1: the native hub UI's Upgrade control against the one purchase rail. The real bodies are the
    // shell's RequestUpgrade / OnPurchaseResult / CanUpgrade (Skeleton), the Racks tab's state members, the rack's
    // RequestPurchaseComputeTier + PurchaseComputeTierHost, LpBitcoinPurchaseFlow.PurchaseComputeTierHost, the hub's
    // NotifyPurchaseResult(+ToCaller), LpBtcHubUiHost.OnPurchaseResult and HASHD's ConfirmPurchase; the hub's
    // CanManageHub, the upgrade ledger and the compute track are stand-ins. A call is made by reflection, so a member
    // that the tree under test lacks reads as a failing check, not a crash.
    static int RunUpgrade(string root)
    {
        string[] skeletonNames = { "_upgradeRackId", "_upgradeNote", "_upgradeNoteGood", "_blockedNote", "_showcase", "_live",
            "Preview", "UseFixture", "BoundHub", "BtcSign", "CanUpgrade", "UpgradeHint", "RequestUpgrade", "OnPurchaseResult", "Blocked" };
        var skeleton = HarnessKit.RazorMembers(root, Skeleton, skeletonNames);
        var requestPath = HarnessKit.RazorMembers(root, Skeleton, "RequestUpgrade", "OnPurchaseResult", "CanUpgrade");
        var source = UpgradeTemplate
            .Replace("PURCHASE_TYPES", HarnessKit.Types(root, PurchaseFlow, "LpBitcoinPurchaseResultCode", "LpBitcoinPurchaseResult"))
            .Replace("FLOW_METHODS", HarnessKit.Methods(root, PurchaseFlow, "LpBitcoinPurchaseFlow", "PurchaseComputeTierHost"))
            .Replace("HUB_METHODS", HarnessKit.Members(root, Hub, "LpBitcoinHubEntity", "NotifyPurchaseResultToCaller", "NotifyPurchaseResult", "RelayHubKey", "RelayKindComputeTier"))
            .Replace("RACK_METHODS", HarnessKit.Methods(root, Rack, "LpBitcoinRackEntity", "RequestPurchaseComputeTier", "PurchaseComputeTierHost"))
            .Replace("UIHOST_METHODS", HarnessKit.Methods(root, UiHost, "LpBtcHubUiHost", "OnPurchaseResult"))
            .Replace("SPY_MEMBERS", HarnessKit.Members(root, Spy, "LpBtcHubCommandSpy", "BlockedCount", "ShowcaseMutationAttempts", "Reason", "Reset", "Blocked", "Send"))
            .Replace("SKELETON_MEMBERS", skeleton)
            .Replace("RACKS_MEMBERS", HarnessKit.RazorMembers(root, RacksTab, "CanUpgrade", "UpgradeRackId", "UpgradeHint", "UpgradeNote",
                "UpgradeNoteGood", "UpgradeWorking", "UpgradeLive", "UpgradeClass", "UpgradeLabel", "UpgradeTooltip"))
            .Replace("HASHD_MEMBERS", HarnessKit.RazorMembers(root, Hashd, "_purchasePendingRackId", "_purchaseResultCode", "_purchaseResultTier",
                "_purchaseResultCostSats", "_purchaseResultShortfallSats", "_sincePurchaseResult", "_confirmRackId", "StepperWorking", "ConfirmPurchase", "OnPurchaseResult"))
            .Replace("HASHD_CALLS", HarnessKit.Calls(HarnessKit.RazorMembers(root, Hashd, "ConfirmPurchase"), "Request"))
            .Replace("SKELETON_CALLS", HarnessKit.Calls(HarnessKit.RazorMembers(root, Skeleton, "RequestUpgrade"), "Request"))
            .Replace("REQUEST_PATH_PRESENT", requestPath.Contains("RequestUpgrade") ? "true" : "false")
            .Replace("REQUEST_PATH_TOUCHES", HarnessKit.Touches(requestPath, "QuoteSats", "NextQuoteSats", "HubWalletBtc", "WalletBtc", "TryCommitPurchase",
                "PushAlertHost", "ComputeTier", "BitcoinAmount", "RequestCashOutHub", "RequestSendHubWallet"))
            .Replace("RACKS_MARKUP_ROUTES_UPGRADE", MarkupRoutesUpgrade(root) ? "true" : "false")
            .Replace("SHELL_BINDS_UPGRADE", File.ReadAllText(Path.Combine(root, Skeleton)).Contains("OnUpgrade=\"@(") ? "true" : "false")
            .Replace("UI_WALLET_WRITES", UiWalletWrites(root).ToString());
        try
        {
            var assembly = HarnessKit.Compile("HubUpgradeSubject", source);
            return (int)assembly.GetType("UpgradeSubject.UpgradeControls")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (InvalidOperationException)
        {
            return 2;
        }
    }

    // True when the Racks tab sends the Upgrade press to OnUpgrade and no longer to the inert OnBlocked("upgrade") path.
    static bool MarkupRoutesUpgrade(string root)
    {
        var markup = File.ReadAllText(Path.Combine(root, RacksTab));
        return markup.Contains("OnUpgrade?.Invoke(") && !markup.Contains("OnBlocked?.Invoke( \"upgrade\" )");
    }

    // Writes to HubWalletBtc anywhere in the native hub UI folder: the client must never keep a balance of its own.
    static int UiWalletWrites(string root)
    {
        var writes = 0;
        var pattern = new System.Text.RegularExpressions.Regex(@"\bHubWalletBtc\s*(?:[-+*/]?=(?!=)|\+\+|--)");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, HubUi), "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal)))
            writes += pattern.Matches(File.ReadAllText(file)).Count;
        return writes;
    }

    const string MenuGate = "game/Code/Addons/lifepunch/lifepunchcore/LifePunchMenuInteractGate.cs";
    const string MenuRange = "game/Code/Addons/lifepunch/lifepunchcore/LifePunchMenuInteractRange.cs";

    static int RunGate(string root)
    {
        var source = GateTemplate
            .Replace("GATE_METHODS", HarnessKit.Members(root, MenuGate, "LifePunchMenuInteractGate", "IsTerminalTargetInOpenRange"))
            .Replace("RANGE_MEMBERS", HarnessKit.Members(root, MenuRange, "LifePunchMenuInteractRange",
                "MetersToUnits", "OpenHorizontalMeters", "OpenVerticalMeters", "OpenHorizontalUnits", "OpenVerticalUnits",
                "IsInOpenRange", "IsWithin"));
        try
        {
            var assembly = HarnessKit.Compile("HubGateSubject", source);
            return (int)assembly.GetType("GateSubject.GateControls")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (InvalidOperationException)
        {
            return 2;
        }
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
        if (t == "adv") { advanced = true; return true; }
        if (t == "std-1") { slot = 1; return true; }
        if (t == "std-2") { slot = 2; return true; }
        return false;
    }
    public static bool CanLinkToDeclaredSlot(bool advanced, int slot, IReadOnlyList<LpBitcoinRackEntity> linked, out string error) { error = null; return true; }
    public static bool CanLinkRackToHub(LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linked, out string error) { error = null; return true; }
    public static string FormatRackSlotDisplayName(LpBitcoinRackEntity rack, IReadOnlyList<LpBitcoinRackEntity> linked) => "GPU Rack " + linked.Count;
    public static string FormatDeclaredLinkSlotToken(bool advanced, int slot) => advanced ? "adv" : "std-" + slot;
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
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: true, terminal: false); Call(hub, "LinkRackByTokenHost", "std-1");
        Neg(hub.Alerts.Count == 1 && Said(hub, "terminal"), "LinkRackByTokenHost pushes an alert on the no-terminal path");
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: false, terminal: true); Call(hub, "LinkNearbyRackHost");
        Neg(hub.Alerts.Count == 1 && Said(hub, "power"), "LinkNearbyRackHost pushes an alert on the power-off path");
        s = NewScene(); hub = AddHub(s, "Hub", 7, powered: false, terminal: true); Call(hub, "LinkRackByTokenHost", "std-1");
        Neg(hub.Alerts.Count == 1 && Said(hub, "power"), "LinkRackByTokenHost pushes an alert on the power-off path");
        var silent = true;
        foreach (var powered in new[] { true, false })
        foreach (var terminal in new[] { true, false })
        {
            s = NewScene(); hub = AddHub(s, "Hub", 7, powered, terminal); hub.CanManage = false; AddRack(s, 7);
            Call(hub, "LinkNearbyRackHost"); Call(hub, "LinkRackByTokenHost", "std-1");
            silent &= hub.Alerts.Count == 0 && hub.Linked.Count == 0;
        }
        Ctl(silent, "a caller who cannot manage the hub gets silence and links nothing, in all four power/terminal states");
        s = NewScene(); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkNearbyRackHost");
        Ctl(hub.Linked.Count == 1 && Said(hub, "linked"), "LinkNearbyRackHost still links an owned rack in range");
        s = NewScene(); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkRackByTokenHost", "std-1");
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
        s = NewScene(); AddHub(s, "LpBitcoinPreviewHub", 7); hub = AddHub(s, "Hub", 7); AddRack(s, 7); Call(hub, "LinkRackByTokenHost", "std-1");
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
    public static bool IsCallerAllowedTerminal(Connection caller, GameObject target) => Allowed;
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

    // The opt-in bounds-based terminal reach (HUB-WIRING-2 U3), run on SYNTHETIC bounds with the real range constants.
    // NEW = a check on the added method itself: it fails on the develop blobs only because the method does not exist there.
    // The one NEG check is the geometry: a pivot-centred desk terminal the pivot reach refuses and the bounds reach admits.
    const string GateTemplate = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace GateSubject;

public enum FindMode { EverythingInSelfAndDescendants }
public struct Vector3
{
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    public float Length => MathF.Sqrt(x * x + y * y + z * z);
}
public struct BBox
{
    public Vector3 Mins, Maxs;
    public Vector3 ClosestPoint(Vector3 p) => new Vector3(Math.Clamp(p.x, Mins.x, Maxs.x), Math.Clamp(p.y, Mins.y, Maxs.y), Math.Clamp(p.z, Mins.z, Maxs.z));
}
public sealed class ModelRenderer { public BBox Bounds; }
public sealed class ComponentList { public ModelRenderer Renderer; public T Get<T>(FindMode mode) where T : class => Renderer as T; }
public sealed class GameObject { public Vector3 WorldPosition; public ComponentList Components = new(); }
public static class Validity { public static bool IsValid(this ModelRenderer r) => r is not null; }
public static class LifePunchMenuInteractRange
{
    RANGE_MEMBERS
}
public static class LifePunchMenuInteractGate
{
    GATE_METHODS
}
public static class GateControls
{
    static int negTotal, negFail, newTotal, newFail;
    static void Neg(bool ok, string name) { negTotal++; if (!ok) negFail++; Console.WriteLine((ok ? "PASS NEG " : "FAIL NEG ") + name); }
    static void New(bool ok, string name) { newTotal++; if (!ok) newFail++; Console.WriteLine((ok ? "PASS NEW " : "FAIL NEW ") + name); }
    // null = the method is not in the tree under test.
    static bool? Reach(Vector3 viewer, GameObject target)
    {
        var m = typeof(LifePunchMenuInteractGate).GetMethod("IsTerminalTargetInOpenRange", BindingFlags.NonPublic | BindingFlags.Static);
        return m is null ? null : (bool)m.Invoke(null, new object[] { viewer, target });
    }
    // A terminal whose world bounds are a cube of the given size around 'center' and whose pivot is 'pivot'.
    static GameObject Terminal(Vector3 pivot, Vector3 center, float size, bool renderer = true)
    {
        var go = new GameObject { WorldPosition = pivot };
        if (renderer)
        {
            var h = size / 2f;
            go.Components.Renderer = new ModelRenderer { Bounds = new BBox { Mins = new Vector3(center.x - h, center.y - h, center.z - h), Maxs = new Vector3(center.x + h, center.y + h, center.z + h) } };
        }
        return go;
    }

    public static int Run()
    {
        // Inches: a desk top at 29 u, a 12 u terminal standing on it with its pivot at the centre of the model (z 35).
        var desk = Terminal(new Vector3(0, 0, 35), new Vector3(0, 0, 35), 12f);
        var viewer = new Vector3(30, 0, 0); // standing at the desk, feet on the floor
        var pivotReach = LifePunchMenuInteractRange.IsInOpenRange(viewer, desk.WorldPosition);
        Neg(!pivotReach && Reach(viewer, desk) == true, "a desk terminal with a centred pivot is out of pivot reach but inside bounds reach");
        var tallDesk = Terminal(new Vector3(0, 0, 46), new Vector3(0, 0, 46), 12f);
        New(Reach(viewer, tallDesk) == false, "bounds reach is still bounded vertically (box bottom at 40 u from the feet is refused)");
        New(Reach(new Vector3(80, 0, 0), desk) == false, "bounds reach is still bounded horizontally (a viewer 80 u away is refused)");

        var violations = 0; var pivotPasses = 0; var boundsPasses = 0;
        for (var x = -100; x <= 100; x += 5)
        for (var y = -100; y <= 100; y += 5)
        foreach (var z in new[] { -20f, 0f, 20f, 40f })
        {
            var v = new Vector3(x, y, z);
            var byPivot = LifePunchMenuInteractRange.IsInOpenRange(v, desk.WorldPosition);
            var byBounds = Reach(v, desk) == true;
            if (byPivot) pivotPasses++;
            if (byBounds) boundsPasses++;
            if (byPivot && !byBounds) violations++;
        }
        New(violations == 0 && pivotPasses > 0 && boundsPasses >= pivotPasses, $"bounds reach is never narrower than pivot reach when the pivot is inside the bounds (pivot {pivotPasses}, bounds {boundsPasses} of the sampled viewers)");

        var noRenderer = Terminal(new Vector3(0, 0, 35), new Vector3(0, 0, 35), 12f, renderer: false);
        var same = true;
        for (var x = -60; x <= 60; x += 5)
        foreach (var z in new[] { 0f, 20f, 35f })
        {
            var v = new Vector3(x, 10, z);
            same &= Reach(v, noRenderer) == LifePunchMenuInteractRange.IsInOpenRange(v, noRenderer.WorldPosition);
        }
        New(same, "a terminal without a renderer falls back to exactly the pivot check");

        Console.WriteLine($"Gate controls: {negTotal + newTotal - negFail - newFail}/{negTotal + newTotal} checks passed " +
            $"({negFail}/{negTotal} negative controls failing, {newFail}/{newTotal} added-method checks failing)");
        return negFail + newFail == 0 ? 0 : 1;
    }
}
""";

    // The native hub UI's Upgrade control against the one purchase rail (HUB-UPGRADE-1). NEG = fails on the 94edc411
    // blobs (the control is inert there) and passes at HEAD; CTL = a guard on the unchanged rail that passes on both.
    // Stand-ins: the hub's CanManageHub (a connection id match), the upgrade ledger, the compute track quote, the
    // engine types. The extracted bodies are real; nothing here proves engine compile, replication or Razor rendering.
    const string UpgradeTemplate = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace UpgradeSubject;

public sealed class PropertyAttribute : Attribute { }
public static class Networking { public static bool IsHost = true; }
public static class Log { public static void Info(string m) { } public static void Warning(string m) { } }
public static class Rpc { public static Guid CallerId; }
public sealed class Connection { public Guid Id; public static Connection Local = new Connection { Id = Guid.NewGuid() }; }
public interface IValid { bool Valid { get; } }
public static class Validity { public static bool IsValid(this IValid v) => v is not null && v.Valid; }
public sealed class GameObject { public Guid Id = Guid.NewGuid(); }
public sealed class Scene { public List<object> Components = new(); public IEnumerable<T> GetAllComponents<T>() => Components.OfType<T>().ToList(); }
public static class Game { public static Scene ActiveScene; }
public struct TimeSince { public float T; public static implicit operator TimeSince(int v) => new TimeSince { T = v }; }
public enum LpBitcoinHubAlertKind { TerminalCommand, HubTransfer }
PURCHASE_TYPES
public interface ILpBitcoinHubFormat { string Tier(int t); string BtcPrice(long s); }
public sealed class FormatStub : ILpBitcoinHubFormat { public string Tier(int t) => "T" + t; public string BtcPrice(long s) => s.ToString(); }
public sealed class LpBtcHubShowcaseFixture { }
// The hub result relay (HUB-RACKS-1) is covered by its own group; here it only has to exist for the hub body to compile.
public static class LpHubResultRelay { public static int Sent; public static void SendToCaller(string hubKey, Guid callerId, string kind, string payload) { Sent++; } }
public static class LpBitcoinIdent
{
    public static string FormatRackSlotTerminalToken(LpBitcoinRackEntity r, IReadOnlyList<LpBitcoinRackEntity> l) => r.AssignedSlotToken;
    public static string RomanTier(int t) => new[] { "", "I", "II", "III", "IV", "V", "VI" }[t];
}
public static class LpBitcoinComputeTrack
{
    public const string TrackId = "rack_compute";
    public static void EnsureRegistered() { }
    public static string ClassOf(LpBitcoinRackEntity r) => r.Advanced ? "advanced" : "standard";
    public static long QuoteSats(LpBitcoinRackEntity r, int tier) => tier > 5 ? -1L : 50_000L * tier;
    public static void Apply(LpBitcoinRackEntity r, int tier) { r.ClockGhz = 1f + tier; }
    public static int EffectMultiplierFor(int tier) => tier;
}
public static class LpBitcoinEconomy { public static float RackBtcCapacityFor(LpBitcoinRackEntity r) => 1f; }
public static class LifePunchUpgradeLedger
{
    public static int Commits; public static bool Reject; public static Dictionary<string, int> Tiers = new();
    public static void Reset() { Commits = 0; Reject = false; Tiers = new(); }
    public static int MaxTier(long owner, string track, string slot, string cls) => Tiers.TryGetValue(slot, out var t) ? t : 0;
    public static bool TryCommitPurchase(long owner, string track, string slot, string cls, int tier, long cost, Guid caller, out string error)
    {
        error = null;
        if (Reject) { error = "ledger rejected"; return false; }
        Commits++; Tiers[slot] = tier; return true;
    }
}
public sealed class LpBitcoinHubEntity : IValid
{
    public GameObject GameObject = new GameObject();
    public bool Valid { get; set; } = true;
    public long Owner = 1; public float HubWalletBtc; public bool IsPowered = true;
    public Guid ManagerConn;
    public List<LpBitcoinRackEntity> Linked = new();
    public List<string> Alerts = new();
    // Stand-in for the host's own test: the caller must be the hub's manager.
    public bool CanManageHub(Guid callerId) => callerId == ManagerConn;
    public List<LpBitcoinRackEntity> GetLinkedRacks() => Linked;
    public void RefreshLinkedTerminalScreens() { }
    internal void PushAlertHost(LpBitcoinHubAlertKind kind, string message) => Alerts.Add(message);
    // The hub's own request twin (no caller in the tree): present so a UI that wrongly calls it still compiles and reads red.
    public void RequestPurchaseComputeTier(Guid rackId) { }
    HUB_METHODS
}
public sealed class LpBitcoinRackEntity : IValid
{
    public GameObject GameObject = new GameObject();
    public bool Valid { get; set; } = true;
    public bool Advanced; public int ComputeTier; public float ClockGhz = 1f; public string AssignedSlotToken = "std-1";
    public LpBitcoinHubEntity HubRef; public int HubLookups;
    // Counts how often a host body ran: one lookup per rail entry.
    private LpBitcoinHubEntity GetLinkedHub() { HubLookups++; return HubRef; }
    RACK_METHODS
}
internal static class LpBitcoinPurchaseFlow
{
    FLOW_METHODS
}
public sealed class LpBitcoinHubStateLive { public LpBitcoinHubEntity Hub; }
public static class LpBtcHubCommandSpy
{
    SPY_MEMBERS
}
internal static class LpBtcHubUiHost
{
    UIHOST_METHODS
}
public sealed class LpHashdPanel : IValid
{
    public bool Valid { get; set; } = true;
    public LpBitcoinHubEntity Hub;
    HASHD_MEMBERS
}
public sealed class LpBtcHubSkeleton : IValid
{
    public bool Valid { get; set; } = true;
    private readonly ILpBitcoinHubFormat Format = new FormatStub();
    private void StateHasChanged() { }
    SKELETON_MEMBERS
}
public sealed class RackLite { public Guid Id; }
public sealed class LpBtcHubRacks
{
    public RackLite Detail;
    RACKS_MEMBERS
}
public static class UpgradeControls
{
    const string HashdCalls = "HASHD_CALLS";
    const string SkeletonCalls = "SKELETON_CALLS";
    const bool RequestPathPresent = REQUEST_PATH_PRESENT;
    const string RequestPathTouches = "REQUEST_PATH_TOUCHES";
    const bool RacksMarkupRoutesUpgrade = RACKS_MARKUP_ROUTES_UPGRADE;
    const bool ShellBindsUpgrade = SHELL_BINDS_UPGRADE;
    const int WalletWritesInUi = UI_WALLET_WRITES;

    static int negTotal, negFail, ctlTotal, ctlFail;
    static void Neg(bool ok, string name) { negTotal++; if (!ok) negFail++; Console.WriteLine((ok ? "PASS NEG " : "FAIL NEG ") + name); }
    static void Ctl(bool ok, string name) { ctlTotal++; if (!ok) ctlFail++; Console.WriteLine((ok ? "PASS CTL " : "FAIL CTL ") + name); }
    // A member the tree under test lacks throws inside the call; that reads as a failing check.
    static bool Try(Func<bool> f) { try { return f(); } catch (Exception) { return false; } }
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n)
    {
        var t = o.GetType();
        var p = t.GetProperty(n, All);
        if (p is not null) return p.GetValue(o);
        return t.GetField(n, All)?.GetValue(o);
    }
    static void Set(object o, string n, object v)
    {
        var t = o.GetType();
        var p = t.GetProperty(n, All);
        if (p is not null) { p.SetValue(o, v); return; }
        t.GetField(n, All)?.SetValue(o, v);
    }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, All).Invoke(o, a);
    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-9f;
    static bool Has(string s, string needle) => s is not null && s.Contains(needle, StringComparison.OrdinalIgnoreCase);
    static float Cost(int tier) => 50_000L * tier / 100_000_000f;

    sealed class Fx { public LpBitcoinHubEntity Hub; public LpBitcoinRackEntity Rack; public LpBtcHubSkeleton Panel; public LpBtcHubRacks Racks; public LpHashdPanel Hashd; }
    static Fx Make(bool owner = true, float wallet = 0.01f, bool answered = true, int tier = 0, bool hashd = false)
    {
        var scene = new Scene(); Game.ActiveScene = scene; Networking.IsHost = true;
        LifePunchUpgradeLedger.Reset(); LpBtcHubCommandSpy.Reset();
        Rpc.CallerId = Connection.Local.Id;
        var f = new Fx();
        f.Hub = new LpBitcoinHubEntity { HubWalletBtc = wallet, ManagerConn = owner ? Connection.Local.Id : Guid.NewGuid() };
        f.Rack = new LpBitcoinRackEntity { ComputeTier = tier, HubRef = answered ? f.Hub : null };
        f.Hub.Linked.Add(f.Rack);
        LifePunchUpgradeLedger.Tiers[f.Rack.AssignedSlotToken] = tier;
        f.Panel = new LpBtcHubSkeleton();
        Set(f.Panel, "_live", new LpBitcoinHubStateLive { Hub = f.Hub });
        scene.Components.Add(f.Panel);
        f.Racks = new LpBtcHubRacks { Detail = new RackLite { Id = f.Rack.GameObject.Id } };
        if (hashd) { f.Hashd = new LpHashdPanel { Hub = f.Hub }; scene.Components.Add(f.Hashd); }
        return f;
    }
    static void Press(Fx f) { Rpc.CallerId = Connection.Local.Id; Call(f.Panel, "RequestUpgrade", f.Rack.GameObject.Id); }
    // Copies the shell's values into the Racks tab the way the markup binds them.
    static void Sync(Fx f)
    {
        Set(f.Racks, "CanUpgrade", Get(f.Panel, "CanUpgrade") ?? false);
        Set(f.Racks, "UpgradeRackId", Get(f.Panel, "_upgradeRackId") ?? Guid.Empty);
        Set(f.Racks, "UpgradeHint", Get(f.Panel, "UpgradeHint") ?? string.Empty);
        Set(f.Racks, "UpgradeNote", Get(f.Panel, "_upgradeNote") ?? string.Empty);
        Set(f.Racks, "UpgradeNoteGood", Get(f.Panel, "_upgradeNoteGood") ?? false);
    }
    static bool Live(Fx f) { Sync(f); return (bool)Get(f.Racks, "UpgradeLive"); }
    static Guid Pending(Fx f) => (Guid)Get(f.Panel, "_upgradeRackId");
    static string Note(Fx f) => (string)Get(f.Panel, "_upgradeNote");
    static bool Good(Fx f) => (bool)Get(f.Panel, "_upgradeNoteGood");
    static string Snap(Fx f) => $"{f.Hub.HubWalletBtc:R}|{f.Rack.ComputeTier}|{LifePunchUpgradeLedger.Commits}|{string.Join(";", f.Hub.Alerts)}";
    static LpBitcoinPurchaseResult Result(int code) => new LpBitcoinPurchaseResult { Code = (LpBitcoinPurchaseResultCode)code, NewTier = 1, CostPaidSats = 50_000, ShortfallSats = 400_000 };

    public static int Run()
    {
        Fx f; float w;

        // The control reaches the one rail, and the host alone moves the money.
        f = Make(); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { Press(f); return LifePunchUpgradeLedger.Commits == 1 && f.Rack.ComputeTier == 1 && Near(f.Hub.HubWalletBtc, w - Cost(1)); }),
            "an owner pressing Upgrade buys the next tier through the host rail: one debit of the host quote, one ledger commit");
        f = Make();
        Neg(Try(() => { Press(f); return Pending(f) == Guid.Empty && Good(f) && Has(Note(f), "online"); }),
            "the host envelope reaches the native hub UI with no HASHD panel mounted: the request clears and the note reads online");
        f = Make(wallet: 0.0001f); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { Press(f); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && !Good(f) && Has(Note(f), "short by") && Pending(f) == Guid.Empty && f.Hub.Alerts.Count == 1; }),
            "a short wallet is refused by the host: wallet untouched, no commit, the note names the host's shortfall");
        f = Make(); w = f.Hub.HubWalletBtc; LifePunchUpgradeLedger.Reject = true;
        Neg(Try(() => { Press(f); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && f.Rack.ComputeTier == 0 && !Good(f) && Has(Note(f), "nothing was charged") && Pending(f) == Guid.Empty; }),
            "a purchase the ledger rejects leaves the wallet as it was and the note says nothing was charged");
        f = Make(tier: 5); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { Press(f); return LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && !Good(f) && Has(Note(f), "not available") && Pending(f) == Guid.Empty; }),
            "a press past the top tier is answered by the rail and the note says nothing was charged");

        // Who sees the control enabled, and when.
        Neg(Try(() => Live(Make())), "the hub owner sees the control enabled");
        f = Make(owner: false); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { var live = Live(f); Press(f); return !live && f.Rack.HubLookups == 0 && LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && f.Hub.Alerts.Count == 0 && Has(Note(f), "owner"); }),
            "a non-owner sees the control disabled, and a press sends nothing to the host");
        f = Make(); f.Hub.IsPowered = false;
        Neg(Try(() => { var live = Live(f); Press(f); return live && LifePunchUpgradeLedger.Commits == 1; }),
            "an unpowered hub does not disable the control, because the rail does not refuse on power (no gate is invented)");
        f = Make(answered: false);
        Neg(Try(() =>
        {
            Press(f);
            var held = Pending(f) == f.Rack.GameObject.Id && f.Rack.HubLookups == 1;
            var live = Live(f); var label = (string)Get(f.Racks, "UpgradeLabel");
            Press(f);
            return held && !live && label == "Working..." && f.Rack.HubLookups == 1;
        }), "while a request is in flight the control is disabled and reads Working, and a second press sends nothing");
        Neg(Try(() =>
        {
            foreach (var code in new[] { 0, 1, 2, 3, 99 })
            {
                var g = Make(answered: false); Press(g);
                Call(g.Hub, "NotifyPurchaseResultToCaller", Connection.Local.Id, Result(code));
                if (Pending(g) != Guid.Empty || !Live(g)) return false;
            }
            return true;
        }), "every host result code re-enables the control, including an unknown one");
        Neg(Try(() =>
        {
            var notes = new List<string>(); var goods = 0;
            foreach (var code in new[] { 0, 1, 2, 3 })
            {
                var g = Make(answered: false); Press(g);
                Call(g.Hub, "NotifyPurchaseResultToCaller", Connection.Local.Id, Result(code));
                notes.Add(Note(g)); if (Good(g)) goods++;
            }
            return notes.All(n => !string.IsNullOrEmpty(n)) && notes.Distinct().Count() == 4 && goods == 1;
        }), "each host result code has its own note, and only a success reads as one");
        f = Make(); Set(f.Panel, "UseFixture", true);
        Neg(Try(() => { Press(f); return f.Rack.HubLookups == 0 && LpBtcHubCommandSpy.BlockedCount == 1 && LifePunchUpgradeLedger.Commits == 0; }),
            "in a preview or fixture view the press is counted as blocked and nothing is sent");
        f = Make();
        Neg(Try(() => { Rpc.CallerId = Connection.Local.Id; Call(f.Panel, "RequestUpgrade", Guid.NewGuid()); return f.Rack.HubLookups == 0 && Has(Note(f), "no longer linked") && Pending(f) == Guid.Empty; }),
            "a rack that is no longer linked is refused by the UI without a host call");
        Neg(Try(() =>
        {
            var a = Make(answered: false);
            var b = new LpBtcHubSkeleton(); var hubB = new LpBitcoinHubEntity { ManagerConn = Connection.Local.Id };
            Set(b, "_live", new LpBitcoinHubStateLive { Hub = hubB }); Game.ActiveScene.Components.Add(b);
            var other = Guid.NewGuid(); Set(b, "_upgradeRackId", other);
            Press(a);
            Call(a.Hub, "NotifyPurchaseResultToCaller", Connection.Local.Id, Result(0));
            return Pending(a) == Guid.Empty && (Guid)Get(b, "_upgradeRackId") == other;
        }), "a result is routed to the panel bound to its own hub and to no other");

        // The call is HASHD's call.
        Neg(HashdCalls.Length > 0 && SkeletonCalls == HashdCalls && HashdCalls == "RequestPurchaseComputeTier/0",
            "the native control calls the same host member with the same arguments as HASHD (RequestPurchaseComputeTier, no arguments)");
        Neg(Try(() =>
        {
            var h = Make(hashd: true); Rpc.CallerId = Connection.Local.Id; Call(h.Hashd, "ConfirmPurchase", h.Rack);
            var viaHashd = Snap(h);
            var n = Make(); Press(n);
            return viaHashd == Snap(n) && LifePunchUpgradeLedger.Commits == 1;
        }), "the native press and HASHD's press leave the same wallet, tier, ledger and alert feed on equal fixtures");
        Neg(RequestPathPresent && RequestPathTouches.Length == 0,
            "the native request path names no price, wallet, ledger, alert or other money identifier (the host decides everything)");
        Neg(RacksMarkupRoutesUpgrade && ShellBindsUpgrade,
            "the Racks tab sends the Upgrade press to OnUpgrade, not to the inert OnBlocked path, and the shell binds it");
        Neg(Try(() =>
        {
            var r = new LpBtcHubRacks { Detail = new RackLite { Id = Guid.NewGuid() } };
            Set(r, "CanUpgrade", true); var idle = (bool)Get(r, "UpgradeLive");
            Set(r, "UpgradeRackId", Guid.NewGuid()); var busy = (bool)Get(r, "UpgradeLive");
            Set(r, "UpgradeRackId", Guid.Empty); Set(r, "CanUpgrade", false); var denied = (bool)Get(r, "UpgradeLive");
            Set(r, "UpgradeHint", "why");
            return idle && !busy && !denied && (string)Get(r, "UpgradeTooltip") == "why" && (string)Get(r, "UpgradeLabel") == "Upgrade";
        }), "the Racks control is live only for a manager with nothing in flight, and its disabled tooltip carries the reason");

        // Guards on what this slice must not touch.
        f = Make(hashd: true); Set(f.Hashd, "_purchaseResultCode", -2);
        Ctl(Try(() => { Rpc.CallerId = Connection.Local.Id; Call(f.Hashd, "ConfirmPurchase", f.Rack); return LifePunchUpgradeLedger.Commits == 1 && f.Rack.ComputeTier == 1 && (int)Get(f.Hashd, "_purchaseResultCode") == 0; }),
            "HASHD's own control still buys once and receives the envelope");
        f = Make(); w = f.Hub.HubWalletBtc;
        Ctl(Try(() => { Rpc.CallerId = Guid.NewGuid(); Call(f.Rack, "RequestPurchaseComputeTier"); return LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && f.Hub.Alerts.Count == 1; }),
            "the host still refuses a caller who cannot manage the hub, whatever the UI shows: wallet untouched, alert pushed");
        f = Make(); w = f.Hub.HubWalletBtc; LifePunchUpgradeLedger.Reject = true;
        Ctl(Try(() => { Rpc.CallerId = Connection.Local.Id; Call(f.Rack, "RequestPurchaseComputeTier"); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && f.Rack.ComputeTier == 0; }),
            "the host rail still restores the debit exactly when the ledger rejects");
        Ctl(Try(() => { var sent = 0; var live = LpBtcHubCommandSpy.Send(false, "x", () => sent++); var shown = LpBtcHubCommandSpy.Send(true, "x", () => sent++); return live && !shown && sent == 1; }),
            "the command spy still sends on the live path and refuses under a showcase");
        Ctl(WalletWritesInUi == 0, "no file of the native hub UI writes HubWalletBtc");

        Console.WriteLine($"Upgrade controls: {negTotal + ctlTotal - negFail - ctlFail}/{negTotal + ctlTotal} checks passed " +
            $"({negFail}/{negTotal} negative controls failing, {ctlFail}/{ctlTotal} regression guards failing)");
        return negFail + ctlFail == 0 ? 0 : 1;
    }
}
""";

    const string PlayerHubCode = "game/Code/Addons/lifepunch/playerhub/code/";
    const string PhSource = PlayerHubCode + "data/LpPlayerHubBtcSource.cs";
    const string PhModels = PlayerHubCode + "data/LpPlayerHubModels.cs";
    const string PhBtc = PlayerHubCode + "ui/Btc/LpPlayerHubBtc.razor";
    const string PhRoot = PlayerHubCode + "ui/LpPlayerHubRoot.razor";
    const string RelayFile = "game/Code/Addons/lifepunch/lifepunchcore/LpHubResult.cs";
    const string StateLive = HubUi + "LpBitcoinHubStateLive.cs";
    const string IdentFile = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinIdent.cs";

    // HUB-RACKS-1: the Player Hub $BTC tab's rack list and per-rack Upgrade control against the ONE purchase rail and the
    // SPINE-1 hub result relay. The real bodies are the tab's Source (Build, LiveHash, RequestRackUpgrade, OnRelayResult),
    // the view-model records, the tab's and the Root's @code members, the real relay (LpHubResultRelay.SendToCaller), the
    // hub's NotifyPurchaseResult(+ToCaller), the rack's RequestPurchaseComputeTier + PurchaseComputeTierHost,
    // LpBitcoinPurchaseFlow and HASHD's ConfirmPurchase + OnPurchaseResult. Stand-ins: the hub's CanManageHub (a connection
    // id match), the upgrade ledger, the compute-track quote and track registry, the engine types. Members are called by
    // reflection, so a member the tree under test lacks reads as a failing check, never a crash.
    static int RunRacks(string root)
    {
        var requestPath = HarnessKit.Members(root, PhSource, "LpPlayerHubBtcSource", "RequestRackUpgrade", "OnRelayResult", "Say", "CanUpgradeHub");
        var hubText = HarnessKit.Read(root, Hub);
        var source = RacksTemplate
            .Replace("PURCHASE_TYPES", HarnessKit.Types(root, PurchaseFlow, "LpBitcoinPurchaseResultCode", "LpBitcoinPurchaseResult"))
            .Replace("FLOW_METHODS", HarnessKit.Methods(root, PurchaseFlow, "LpBitcoinPurchaseFlow", "PurchaseComputeTierHost"))
            .Replace("HUB_MEMBERS", HarnessKit.Members(root, Hub, "LpBitcoinHubEntity", "NotifyPurchaseResultToCaller", "NotifyPurchaseResult", "RelayHubKey", "RelayKindComputeTier"))
            .Replace("RACK_METHODS", HarnessKit.Methods(root, Rack, "LpBitcoinRackEntity", "RequestPurchaseComputeTier", "PurchaseComputeTierHost"))
            .Replace("RELAY_METHODS", HarnessKit.Methods(root, RelayFile, "LpHubResultRelay", "SendToCaller"))
            .Replace("IDENT_MEMBERS", HarnessKit.Members(root, IdentFile, "LpBitcoinIdent", "RomanTier"))
            .Replace("STATELIVE_MEMBERS", HarnessKit.Members(root, StateLive, "LpBitcoinHubStateLive", "TrackTopTier", "LadderTopTier"))
            .Replace("SOURCE_MEMBERS", HarnessKit.Members(root, PhSource, "LpPlayerHubBtcSource", "BtcFormat", "RackBtcFormat", "HashdRateFactor",
                "FindLocalHub", "LiveHash", "IsTopTier", "NextTierLabelFor", "UpgradeDeniedNote", "CanUpgradeHub", "RequestRackUpgrade", "OnRelayResult", "Say", "Build", "Click"))
            .Replace("MODEL_TYPES", HarnessKit.Types(root, PhModels, "LpBtcVm", "LpBtcRackVm", "LpBtcUpgradeState"))
            .Replace("TAB_MEMBERS", HarnessKit.RazorMembers(root, PhBtc, "UpgradeWorking", "UpgradeLive", "UpgradeClass", "UpgradeLabel", "UpgradeTooltip", "UpgradeNoteClass", "TierChipText"))
            .Replace("ROOT_MEMBERS", HarnessKit.RazorMembers(root, PhRoot, "_upgrade", "BtcModel", "OnHubResult"))
            .Replace("HASHD_MEMBERS", HarnessKit.RazorMembers(root, Hashd, "_purchasePendingRackId", "_purchaseResultCode", "_purchaseResultTier",
                "_purchaseResultCostSats", "_purchaseResultShortfallSats", "_sincePurchaseResult", "_confirmRackId", "StepperWorking", "ConfirmPurchase", "OnPurchaseResult"))
            .Replace("HASHD_CALL_TEXT", HarnessKit.CallTexts(HarnessKit.RazorMembers(root, Hashd, "ConfirmPurchase"), "Request"))
            .Replace("SOURCE_CALL_TEXT", HarnessKit.CallTexts(HarnessKit.Members(root, PhSource, "LpPlayerHubBtcSource", "RequestRackUpgrade"), "Request"))
            .Replace("REQUEST_PATH_PRESENT", requestPath.Contains("RequestRackUpgrade") ? "true" : "false")
            .Replace("REQUEST_PATH_TOUCHES", HarnessKit.Touches(requestPath, "QuoteSats", "NextQuoteSats", "HubWalletBtc", "WalletBtc", "TryCommitPurchase",
                "PushAlertHost", "BitcoinAmount", "RequestCashOutHub", "RequestSendHubWallet", "EffectMultiplierFor", "CostPaidSats", "ShortfallSats"))
            .Replace("RELAY_CALLS_IN_FILE", System.Text.RegularExpressions.Regex.Matches(hubText, @"LpHubResultRelay\s*\.\s*SendToCaller\s*\(").Count.ToString())
            .Replace("RELAY_CALLS_IN_FUNNEL", HarnessKit.Calls(HarnessKit.Methods(root, Hub, "LpBitcoinHubEntity", "NotifyPurchaseResultToCaller"), "SendToCaller"))
            .Replace("RELAY_CALLS_IN_BROADCAST", HarnessKit.Calls(HarnessKit.Methods(root, Hub, "LpBitcoinHubEntity", "NotifyPurchaseResult"), "SendToCaller"))
            .Replace("TAB_MARKUP_ROUTES", TabMarkupRoutes(root) ? "true" : "false")
            .Replace("ROOT_SUBSCRIBES", HarnessKit.Read(root, PhRoot).Contains("@implements ILpHubResult") ? "true" : "false")
            .Replace("PH_WALLET_WRITES", PlayerHubPatternCount(root, @"\bHubWalletBtc\s*(?:[-+*/]?=(?!=)|\+\+|--)").ToString())
            .Replace("PH_RPC_DECLS", PlayerHubPatternCount(root, @"(?m)^\s*\[\s*Rpc\.").ToString())
            .Replace("PH_PRICE_NAMES", PlayerHubPatternCount(root, @"\b(?:QuoteSats|NextQuoteSats|EffectMultiplierFor|TryCommitPurchase)\b").ToString());
        try
        {
            var assembly = HarnessKit.Compile("PlayerHubRacksSubject", source);
            return (int)assembly.GetType("RacksSubject.RackControls")!.GetMethod("Run")!.Invoke(null, null)!;
        }
        catch (InvalidOperationException)
        {
            return 2;
        }
    }

    // True when the tab's per-rack button sends its press to the view model's Upgrade action and is disabled by the tab's own
    // UpgradeLive test, and the Root hands its upgrade state to Build.
    static bool TabMarkupRoutes(string root)
    {
        var tab = HarnessKit.Read(root, PhBtc);
        var rootText = HarnessKit.Read(root, PhRoot);
        return tab.Contains("onclick=\"@rack.Upgrade\"") && tab.Contains("disabled=\"@(!UpgradeLive( rack ))\"")
               && rootText.Contains("Build( StateHasChanged, _upgrade )");
    }

    static int PlayerHubPatternCount(string root, string pattern)
    {
        var count = 0;
        var re = new System.Text.RegularExpressions.Regex(pattern);
        var dir = Path.Combine(root, PlayerHubCode);
        if (!Directory.Exists(dir))
            return 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".razor", StringComparison.Ordinal)))
            count += re.Matches(File.ReadAllText(file)).Count;
        return count;
    }

    // The Player Hub $BTC tab against the one purchase rail and the hub result relay (HUB-RACKS-1). NEG = fails on the
    // 92547428 blobs (PlayerHub has no upgrade control and the hub publishes nothing to the relay there) and passes at HEAD;
    // CTL = a guard on the unchanged rail or read path that passes on both.
    const string RacksTemplate = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace RacksSubject;

public static class Networking { public static bool IsHost = true; }
public static class Log { public static void Info(string m) { } public static void Warning(string m) { } }
public static class Rpc { public static Guid CallerId; }
public sealed class Connection { public Guid Id; public static Connection Local = new Connection { Id = Guid.NewGuid() }; }
public interface IValid { bool Valid { get; } }
public static class Validity { public static bool IsValid(this IValid v) => v is not null && v.Valid; }
public sealed class GameObject { public Guid Id = Guid.NewGuid(); }
public sealed class Scene { public List<object> Components = new(); public IEnumerable<T> GetAllComponents<T>() => Components.OfType<T>().ToList(); }
public static class Game { public static Scene ActiveScene; public static ulong SteamId = 1; }
public struct TimeSince { public float T; public static implicit operator TimeSince(int v) => new TimeSince { T = v }; }
public enum LpBitcoinHubAlertKind { TerminalCommand, HubTransfer }
PURCHASE_TYPES
public static class LpBitcoinUiSounds { public static int Clicks; public static void PlayKeyboardClick() { Clicks++; } }
public static class LpBitcoinIdent
{
    IDENT_MEMBERS
    public static string FormatRackSlotTerminalToken(LpBitcoinRackEntity r, IReadOnlyList<LpBitcoinRackEntity> l) => r.AssignedSlotToken;
    public static string FormatRackSlotDisplayName(LpBitcoinRackEntity r, IReadOnlyList<LpBitcoinRackEntity> l) => "GPU Rack " + (l.ToList().IndexOf(r) + 1);
}
public sealed class TrackDef { public int MaxTier = 5; }
public static class LifePunchUpgradeTracks { public static TrackDef Def = new TrackDef(); public static TrackDef Get(string id) => Def; }
public static class LpBitcoinComputeTrack
{
    public const string TrackId = "rack_compute";
    public static void EnsureRegistered() { }
    public static string ClassOf(LpBitcoinRackEntity r) => r.AdvancedRack ? "advanced" : "standard";
    public static long QuoteSats(LpBitcoinRackEntity r, int tier) => tier > 5 ? -1L : 50_000L * tier;
    public static void Apply(LpBitcoinRackEntity r, int tier) { r.ClockGhz = 1f + tier; }
    public static int EffectMultiplierFor(int tier) => tier;
}
public static class LpBitcoinEconomy
{
    public static int PortalEconomyRevision;
    public static float RackBtcCapacityFor(LpBitcoinRackEntity r) => 1f;
    public static float BtcToCashUsd(float btc) => btc * 60000f;
}
public static class LifePunchUpgradeLedger
{
    public static int Commits; public static bool Reject; public static Dictionary<string, int> Tiers = new();
    public static void Reset() { Commits = 0; Reject = false; Tiers = new(); }
    public static int MaxTier(long owner, string track, string slot, string cls) => Tiers.TryGetValue(slot, out var t) ? t : 0;
    public static bool TryCommitPurchase(long owner, string track, string slot, string cls, int tier, long cost, Guid caller, out string error)
    {
        error = null;
        if (Reject) { error = "ledger rejected"; return false; }
        Commits++; Tiers[slot] = tier; return true;
    }
}
public static class LpBtcHubUiHost { public static int Calls; public static void OnPurchaseResult(LpBitcoinHubEntity hub, int code, int newTier, long costSats, long shortfallSats) { Calls++; } }
public interface ILpHubResult
{
    void OnHubResult(string hubKey, Guid callerId, string kind, string payload);
    static void Post(Action<ILpHubResult> a) { foreach (var c in Game.ActiveScene.GetAllComponents<ILpHubResult>()) a(c); }
}
public sealed class LpHubResultRelay
{
    RELAY_METHODS
}
public sealed class LpBitcoinHubEntity : IValid
{
    public GameObject GameObject = new GameObject();
    public bool Valid { get; set; } = true;
    public static LpBitcoinHubEntity Current;
    public static LpBitcoinHubEntity FindHubByOwnerSteamId(ulong steamId) => Current;
    public long Owner = 1; public float HubWalletBtc; public bool IsPowered = true; public bool AccessPinIsSet; public bool Terminal = true;
    public Guid ManagerConn;
    public List<LpBitcoinRackEntity> Linked = new();
    public List<string> Alerts = new();
    // Stand-in for the host's own test: the caller must be the hub's manager.
    public bool CanManageHub(Guid callerId) => callerId == ManagerConn;
    // The hub's own request twin (no caller in the tree): present so a UI that wrongly calls it still compiles and reads red.
    public void RequestPurchaseComputeTier(Guid rackId) { }
    public List<LpBitcoinRackEntity> GetLinkedRacks() => Linked;
    public bool HasLinkedTerminal() => Terminal;
    public float GetRackPendingBtc() => Linked.Sum(r => r.BitcoinAmount);
    public void SetPowered(bool on) { IsPowered = on; }
    public void RequestDepositRacksToHub() { }
    public void RequestCashOutAllHub() { }
    public void RefreshLinkedTerminalScreens() { }
    internal void PushAlertHost(LpBitcoinHubAlertKind kind, string message) => Alerts.Add(message);
    HUB_MEMBERS
}
public sealed class LpBitcoinRackEntity : IValid
{
    public GameObject GameObject = new GameObject();
    public bool Valid { get; set; } = true;
    public bool AdvancedRack; public int ComputeTier; public float ClockGhz = 1f; public int CoreCount = 4; public float MiningRatePerMinute = 0.1f;
    public float BitcoinAmount; public float UsdValue; public bool IsMining; public string AssignedSlotToken = "std-1";
    public void RequestSetMining(bool on) { IsMining = on; }
    public LpBitcoinHubEntity HubRef; public int HubLookups;
    // Counts how often a host body ran: one lookup per rail entry.
    private LpBitcoinHubEntity GetLinkedHub() { HubLookups++; return HubRef; }
    RACK_METHODS
}
internal static class LpBitcoinPurchaseFlow
{
    FLOW_METHODS
}
public sealed class LpBitcoinHubStateLive
{
    STATELIVE_MEMBERS
}
MODEL_TYPES
public sealed class LpHubStateVm { }
public static class LpPlayerHubBtcSource
{
    SOURCE_MEMBERS
}
public sealed class LpPlayerHubBtc
{
    public LpBtcVm Model { get; set; }
    TAB_MEMBERS
}
public sealed class LpPlayerHubRoot
{
    public int Renders;
    private void StateHasChanged() { Renders++; }
    ROOT_MEMBERS
}
// The scene event subscription the Root declares with @implements: forwards to the Root's own OnHubResult member.
public sealed class RootAdapter : ILpHubResult
{
    public object Root; public int Delivered;
    public void OnHubResult(string hubKey, Guid callerId, string kind, string payload)
    {
        Delivered++;
        Root.GetType().GetMethod("OnHubResult", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Root, new object[] { hubKey, callerId, kind, payload });
    }
}
public sealed class LpHashdPanel : IValid
{
    public bool Valid { get; set; } = true;
    public LpBitcoinHubEntity Hub;
    HASHD_MEMBERS
}
public static class RackControls
{
    const string HashdCallText = "HASHD_CALL_TEXT";
    const string SourceCallText = "SOURCE_CALL_TEXT";
    const bool RequestPathPresent = REQUEST_PATH_PRESENT;
    const string RequestPathTouches = "REQUEST_PATH_TOUCHES";
    const int RelayCallsInFile = RELAY_CALLS_IN_FILE;
    const string RelayCallsInFunnel = "RELAY_CALLS_IN_FUNNEL";
    const string RelayCallsInBroadcast = "RELAY_CALLS_IN_BROADCAST";
    const bool TabMarkupRoutes = TAB_MARKUP_ROUTES;
    const bool RootSubscribes = ROOT_SUBSCRIBES;
    const int WalletWritesInPlayerHub = PH_WALLET_WRITES;
    const int RpcDeclsInPlayerHub = PH_RPC_DECLS;
    const int PriceNamesInPlayerHub = PH_PRICE_NAMES;

    static int negTotal, negFail, ctlTotal, ctlFail;
    static void Neg(bool ok, string name) { negTotal++; if (!ok) negFail++; Console.WriteLine((ok ? "PASS NEG " : "FAIL NEG ") + name); }
    static void Ctl(bool ok, string name) { ctlTotal++; if (!ok) ctlFail++; Console.WriteLine((ok ? "PASS CTL " : "FAIL CTL ") + name); }
    // A member the tree under test lacks throws inside the call; that reads as a failing check.
    static bool Try(Func<bool> f) { try { return f(); } catch (Exception) { return false; } }
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static object Get(object o, string n)
    {
        var t = o.GetType();
        var p = t.GetProperty(n, All);
        if (p is not null) return p.GetValue(o);
        return t.GetField(n, All)?.GetValue(o);
    }
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, All).Invoke(o, a);
    static object CallSource(string n, params object[] a) => typeof(LpPlayerHubBtcSource).GetMethod(n, All).Invoke(null, a);
    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-9f;
    static bool Has(string s, string needle) => s is not null && s.Contains(needle, StringComparison.OrdinalIgnoreCase);
    static float Cost(int tier) => 50_000L * tier / 100_000_000f;

    sealed class Fx { public LpBitcoinHubEntity Hub; public LpBitcoinRackEntity Rack; public LpPlayerHubRoot Root; public RootAdapter Panel; public LpPlayerHubBtc Tab; public LpHashdPanel Hashd; }
    static Fx Make(bool owner = true, float wallet = 0.01f, bool answered = true, int tier = 0, bool hashd = false, bool powered = true, int tracks = 5)
    {
        var scene = new Scene(); Game.ActiveScene = scene; Networking.IsHost = true; Game.SteamId = 1;
        LifePunchUpgradeLedger.Reset(); LpBtcHubUiHost.Calls = 0; LpBitcoinUiSounds.Clicks = 0; LifePunchUpgradeTracks.Def = new TrackDef { MaxTier = tracks };
        Rpc.CallerId = Connection.Local.Id;
        var f = new Fx();
        f.Hub = new LpBitcoinHubEntity { HubWalletBtc = wallet, IsPowered = powered, ManagerConn = owner ? Connection.Local.Id : Guid.NewGuid() };
        LpBitcoinHubEntity.Current = f.Hub;
        f.Rack = new LpBitcoinRackEntity { ComputeTier = tier, HubRef = answered ? f.Hub : null };
        f.Hub.Linked.Add(f.Rack);
        LifePunchUpgradeLedger.Tiers[f.Rack.AssignedSlotToken] = tier;
        f.Root = new LpPlayerHubRoot();
        f.Panel = new RootAdapter { Root = f.Root };
        scene.Components.Add(f.Panel);
        f.Tab = new LpPlayerHubBtc();
        if (hashd) { f.Hashd = new LpHashdPanel { Hub = f.Hub }; scene.Components.Add(f.Hashd); }
        return f;
    }
    // The view model the open Player Hub renders from, and the tab fed with it the way the markup binds it.
    static object Vm(Fx f) { var vm = Get(f.Root, "BtcModel"); f.Tab.Model = (LpBtcVm)vm; return vm; }
    static object RackVm(Fx f) => ((System.Collections.IEnumerable)Get(Vm(f), "Racks")).Cast<object>().First();
    static void Press(Fx f) { Rpc.CallerId = Connection.Local.Id; ((Action)Get(RackVm(f), "Upgrade")).Invoke(); }
    static bool Live(Fx f) { var r = RackVm(f); return (bool)Call(f.Tab, "UpgradeLive", r); }
    static string Label(Fx f) => (string)Call(f.Tab, "UpgradeLabel", RackVm(f));
    static string Tip(Fx f) => (string)Call(f.Tab, "UpgradeTooltip", RackVm(f));
    static object State(Fx f) => Get(f.Root, "_upgrade");
    static bool Pending(Fx f) => (bool)Get(State(f), "Pending");
    static string Note(Fx f) => (string)Get(State(f), "Note");
    static bool Good(Fx f) => (bool)Get(State(f), "NoteGood");
    static string Snap(Fx f) => $"{f.Hub.HubWalletBtc:R}|{f.Rack.ComputeTier}|{LifePunchUpgradeLedger.Commits}|{string.Join(";", f.Hub.Alerts)}";
    static LpBitcoinPurchaseResult Result(int code) => new LpBitcoinPurchaseResult { Code = (LpBitcoinPurchaseResultCode)code, NewTier = 1, CostPaidSats = 50_000, ShortfallSats = 400_000 };

    public static int Run()
    {
        Fx f; float w;

        // Reading: the rack list, its tier labels and the top of the ladder come from the host's replicated state.
        f = Make(tier: 2);
        Neg(Try(() => { var r = RackVm(f); return (string)Get(r, "TierLabel") == "TIER II" && !(bool)Get(r, "AtTopTier") && (string)Get(r, "NextTierLabel") == "TIER III"; }),
            "a rack below the top reads its tier label and the next tier's label from the ladder");
        f = Make(tier: 5);
        Neg(Try(() => { var r = RackVm(f); return (bool)Get(r, "AtTopTier") && (string)Get(r, "NextTierLabel") == string.Empty && Call(f.Tab, "TierChipText", r).ToString().EndsWith("MAX"); }),
            "a rack at the top of the ladder reads the top fact and its chip says MAX");
        f = Make(tier: 3, tracks: 3);
        Neg(Try(() => (bool)Get(RackVm(f), "AtTopTier")), "the top of the ladder is the registered track's own MaxTier, not a number copied into PlayerHub");
        f = Make(tier: 0);
        Ctl(Try(() => (string)Get(RackVm(f), "TierLabel") == "STOCK" && (string)Get(RackVm(f), "Label") == "GPU Rack 1" && (bool)Get(Vm(f), "HasHub")),
            "the rack list is read from the hub's linked racks: one card per linked rack, a stock rack reads STOCK");
        f = Make();
        Ctl(Try(() =>
        {
            var a = (int)CallSource("LiveHash"); f.Rack.ComputeTier = 1; var b = (int)CallSource("LiveHash");
            f.Hub.Linked.Add(new LpBitcoinRackEntity()); var c = (int)CallSource("LiveHash");
            return a != b && b != c;
        }), "LiveHash folds the rack list: a tier change and a new rack both change it");

        // The control reaches the one rail, and the host alone moves the money.
        f = Make(); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { Press(f); return LifePunchUpgradeLedger.Commits == 1 && f.Rack.ComputeTier == 1 && Near(f.Hub.HubWalletBtc, w - Cost(1)); }),
            "an owner pressing Upgrade in the Player Hub buys the next tier through the host rail: one debit of the host quote, one ledger commit");
        f = Make();
        Neg(Try(() => { Press(f); return !Pending(f) && Good(f) && Has(Note(f), "is online") && f.Panel.Delivered == 1; }),
            "the host answer reaches the Player Hub through the hub result relay with no HASHD panel mounted: the request clears and the note reads online");
        f = Make(wallet: 0.0001f); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { Press(f); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && !Good(f) && Has(Note(f), "does not have enough") && !Pending(f) && f.Hub.Alerts.Count == 1; }),
            "a short wallet is refused by the host: wallet untouched, no commit, the note says nothing was charged");
        f = Make(); w = f.Hub.HubWalletBtc; LifePunchUpgradeLedger.Reject = true;
        Neg(Try(() => { Press(f); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && f.Rack.ComputeTier == 0 && !Good(f) && Has(Note(f), "nothing was charged") && !Pending(f); }),
            "a purchase the ledger rejects leaves the wallet as it was and the note says nothing was charged");
        f = Make(tier: 5); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { var live = Live(f); var label = Label(f); Press(f); return !live && label == "Maxed" && f.Rack.HubLookups == 0 && LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && Has(Note(f), "top tier"); }),
            "a rack at the top tier shows the control disabled and a press sends nothing to the host (the rail would refuse)");

        // Who sees the control enabled, and when.
        Neg(Try(() => Live(Make())), "the hub owner sees the control enabled on a rack below the top");
        f = Make(owner: false); w = f.Hub.HubWalletBtc;
        Neg(Try(() => { var live = Live(f); var tip = Tip(f); Press(f); return !live && Has(tip, "owner") && f.Rack.HubLookups == 0 && LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && f.Hub.Alerts.Count == 0 && Has(Note(f), "owner"); }),
            "a non-owner sees the control disabled with the reason, and a press sends nothing to the host");
        f = Make(powered: false);
        Neg(Try(() => { var live = Live(f); Press(f); return live && LifePunchUpgradeLedger.Commits == 1; }),
            "an unpowered hub does not disable the control, because the rail does not refuse on power (no gate is invented)");
        f = Make(answered: false);
        Neg(Try(() =>
        {
            Press(f);
            var held = Pending(f) && f.Rack.HubLookups == 1;
            var live = Live(f); var label = Label(f); var id = (string)Get(Vm(f), "UpgradeRackId");
            Press(f);
            return held && !live && label == "Working..." && id == f.Rack.GameObject.Id.ToString() && f.Rack.HubLookups == 1;
        }), "while a request is in flight the control is disabled and reads Working, and a second press sends nothing");
        Neg(Try(() =>
        {
            foreach (var code in new[] { 0, 1, 2, 3, 99 })
            {
                var g = Make(answered: false); Press(g);
                Call(g.Hub, "NotifyPurchaseResultToCaller", Connection.Local.Id, Result(code));
                if (Pending(g) || !Live(g)) return false;
            }
            return true;
        }), "every host result code re-enables the control, including an unknown one");
        Neg(Try(() =>
        {
            var notes = new List<string>(); var goods = 0;
            foreach (var code in new[] { 0, 1, 2, 3 })
            {
                var g = Make(answered: false); Press(g);
                Call(g.Hub, "NotifyPurchaseResultToCaller", Connection.Local.Id, Result(code));
                notes.Add(Note(g)); if (Good(g)) goods++;
            }
            return notes.All(n => !string.IsNullOrEmpty(n)) && notes.Distinct().Count() == 4 && goods == 1;
        }), "each host result code has its own note, and only a success reads as one");
        f = Make();
        Neg(Try(() => { Rpc.CallerId = Connection.Local.Id; CallSource("RequestRackUpgrade", f.Hub, Guid.NewGuid(), State(f), (Action)(() => { })); return f.Rack.HubLookups == 0 && Has(Note(f), "no longer linked") && !Pending(f); }),
            "a rack that is no longer linked is refused by the UI without a host call");
        f = Make(answered: false);
        Neg(Try(() =>
        {
            Press(f);
            var s = State(f);
            // Not this hub, not this kind, or addressed to someone else: ignored while the request waits.
            CallSource("OnRelayResult", s, "otherhub", Connection.Local.Id, "compute-tier", "0;1;1;1", (Action)(() => { }));
            CallSource("OnRelayResult", s, "bitcoinhub", Connection.Local.Id, "other-kind", "0;1;1;1", (Action)(() => { }));
            CallSource("OnRelayResult", s, "bitcoinhub", Guid.NewGuid(), "compute-tier", "0;1;1;1", (Action)(() => { }));
            var held = Pending(f);
            CallSource("OnRelayResult", s, "bitcoinhub", Connection.Local.Id, "compute-tier", "not a number", (Action)(() => { }));
            return held && !Pending(f) && !Good(f);
        }), "the Player Hub only takes the Bitcoin hub's compute-tier answer addressed to this caller, and a garbled payload clears the request without claiming success");
        f = Make();
        Neg(Try(() => { CallSource("OnRelayResult", State(f), "bitcoinhub", Connection.Local.Id, "compute-tier", "0;1;1;1", (Action)(() => { })); return string.IsNullOrEmpty(Note(f)); }),
            "an answer arriving while this panel is not waiting for one (a HASHD purchase) is ignored");

        // The relay: published on the existing result path only, and delivered to the asking caller only.
        Neg(Try(() =>
        {
            var g = Make(answered: false); Press(g);
            Call(g.Hub, "NotifyPurchaseResultToCaller", Guid.NewGuid(), Result(0));
            return Pending(g) && g.Panel.Delivered == 0;
        }), "a result addressed to another caller is not delivered to this Player Hub");
        Neg(RelayCallsInFile == 1 && RelayCallsInFunnel == "SendToCaller/4" && RelayCallsInBroadcast.Length == 0,
            "the hub publishes to the relay in one place only: the host-side result funnel, once, and never from the broadcast body");
        Neg(Try(() => { var g = Make(hashd: true); Rpc.CallerId = Connection.Local.Id; Call(g.Hashd, "ConfirmPurchase", g.Rack); return g.Panel.Delivered == 1 && LpBtcHubUiHost.Calls == 1 && (int)Get(g.Hashd, "_purchaseResultCode") == 0; }),
            "a HASHD purchase still reaches HASHD and the native hub UI once each, and the relay delivers its copy too");
        Neg(Try(() => (string)typeof(LpBitcoinHubEntity).GetField("RelayHubKey", All).GetValue(null) == "bitcoinhub" && (string)typeof(LpBitcoinHubEntity).GetField("RelayKindComputeTier", All).GetValue(null) == "compute-tier"),
            "the hub's relay key and kind are the ones the Player Hub filters on");

        // The call is HASHD's call.
        Neg(HashdCallText.Length > 0 && SourceCallText == HashdCallText && HashdCallText == "rack.RequestPurchaseComputeTier()",
            "the Player Hub calls the same host member as HASHD, byte for byte: rack.RequestPurchaseComputeTier() with no arguments");
        Neg(Try(() =>
        {
            var h = Make(hashd: true); Rpc.CallerId = Connection.Local.Id; Call(h.Hashd, "ConfirmPurchase", h.Rack);
            var viaHashd = Snap(h);
            var n = Make(); Press(n);
            return viaHashd == Snap(n) && LifePunchUpgradeLedger.Commits == 1;
        }), "the Player Hub press and HASHD's press leave the same wallet, tier, ledger and alert feed on equal fixtures");
        Neg(RequestPathPresent && RequestPathTouches.Length == 0,
            "the Player Hub request path names no price, wallet, ledger, alert or envelope-figure identifier (the host decides everything)");
        Neg(TabMarkupRoutes && RootSubscribes,
            "the tab sends the Upgrade press to the view model's action, the Root subscribes to the relay event and hands its upgrade state to Build");
        Neg(Try(() =>
        {
            // Records are built by reflection so a tree whose records lack the new fields reads as a failing check.
            var t = new LpPlayerHubBtc();
            object Rack(bool top) => Activator.CreateInstance(typeof(LpBtcRackVm), "a", "l", "TIER I", "c", "r", "0", "0", false, false, null, top, "TIER II", null);
            object Vm(bool can, string busy) => Activator.CreateInstance(typeof(LpBtcVm), true, true, false, true, true, "0", "0", "0", 1, new List<LpBtcRackVm>(), null, null, null, "n", can, "why", busy, "", false);
            t.Model = (LpBtcVm)Vm(true, ""); var idle = (bool)Call(t, "UpgradeLive", Rack(false)); var maxed = (bool)Call(t, "UpgradeLive", Rack(true));
            t.Model = (LpBtcVm)Vm(true, "a"); var busy = (bool)Call(t, "UpgradeLive", Rack(false));
            t.Model = (LpBtcVm)Vm(false, ""); var denied = (bool)Call(t, "UpgradeLive", Rack(false)); var tip = (string)Call(t, "UpgradeTooltip", Rack(false));
            return idle && !maxed && !busy && !denied && tip == "why";
        }), "the tab's control is live only for a manager, below the top, with nothing in flight, and its disabled tooltip carries the reason");
        f = Make();
        Neg(Try(() => { Press(f); var fresh = Make(); return !Pending(fresh) && string.IsNullOrEmpty(Note(fresh)); }),
            "a freshly opened Player Hub holds no request and no note (the state belongs to the open panel)");

        // Guards on what this slice must not touch.
        f = Make(hashd: true); Rpc.CallerId = Connection.Local.Id; Call(f.Hashd, "ConfirmPurchase", f.Rack);
        Ctl(Try(() => LifePunchUpgradeLedger.Commits == 1 && f.Rack.ComputeTier == 1 && (int)Get(f.Hashd, "_purchaseResultCode") == 0),
            "HASHD's own control still buys once and receives the envelope");
        f = Make(); w = f.Hub.HubWalletBtc;
        Ctl(Try(() => { Rpc.CallerId = Guid.NewGuid(); Call(f.Rack, "RequestPurchaseComputeTier"); return LifePunchUpgradeLedger.Commits == 0 && Near(f.Hub.HubWalletBtc, w) && f.Hub.Alerts.Count == 1; }),
            "the host still refuses a caller who cannot manage the hub, whatever the UI shows: wallet untouched, alert pushed");
        f = Make(); w = f.Hub.HubWalletBtc; LifePunchUpgradeLedger.Reject = true;
        Ctl(Try(() => { Rpc.CallerId = Connection.Local.Id; Call(f.Rack, "RequestPurchaseComputeTier"); return Near(f.Hub.HubWalletBtc, w) && LifePunchUpgradeLedger.Commits == 0 && f.Rack.ComputeTier == 0; }),
            "the host rail still restores the debit exactly when the ledger rejects");
        Ctl(WalletWritesInPlayerHub == 0, "no file of the Player Hub writes HubWalletBtc");
        Ctl(PriceNamesInPlayerHub == 0, "no file of the Player Hub names the quote, the effect multiplier or the ledger commit (no client price)");
        Ctl(RpcDeclsInPlayerHub == 0, "the Player Hub declares no RPC of its own (the request reuses the rack's)");

        Console.WriteLine($"PlayerHub rack controls: {negTotal + ctlTotal - negFail - ctlFail}/{negTotal + ctlTotal} checks passed " +
            $"({negFail}/{negTotal} negative controls failing, {ctlFail}/{ctlTotal} regression guards failing)");
        return negFail + ctlFail == 0 ? 0 : 1;
    }
}
""";
}
