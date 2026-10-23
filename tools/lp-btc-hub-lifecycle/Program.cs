using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Compile the real lifecycle, link-selection and rack-stop bodies, without starting s&box.
// Engine lifetime dispatch is outside this check: each callback is invoked explicitly.
var root = Path.GetFullPath(args.Single());
const string hub = "game/Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/components/LpBitcoinHubEntity.cs";
const string rack = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinRackEntity.cs";
const string ident = "game/Code/Addons/lifepunch/bitcoinmining/LpBitcoinIdent.cs";

string Method(string path, string type, string name)
{
    var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, path)));
    return syntax.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
        .Single(c => c.Identifier.ValueText == type).Members.OfType<MethodDeclarationSyntax>()
        .Single(m => m.Identifier.ValueText == name)
        .WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString();
}

var source = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace LifePunch.DXRP.Addons.Bitcoin;

public static class Networking { public static bool IsHost; }
public static class Game { public static Scene ActiveScene; }
public sealed class Scene
{
    public List<LpBitcoinRackEntity> Racks = new();
    public IEnumerable<T> GetAllComponents<T>() => Racks.Cast<T>();
}
public sealed class GameObject
{
    public Guid Id = Guid.NewGuid();
    public string Name = "rack";
    public Scene Scene;
}
public static class Validity
{
    public static bool IsValid(this LpBitcoinRackEntity rack) => rack is not null && rack.Valid;
}
public class BaseEntity
{
    protected virtual void OnDestroy() { }
    protected virtual void OnDestroyed() { }
}
public sealed class Terminal
{
    public bool Linked = true;
    public void UnlinkFromHub() => Linked = false;
}
public static class LifePunchMachineDestroyFx
{
    public static int Explosions;
    public static void SpawnPrinterStyleExplosion(object entity, object explosion, object position) => Explosions++;
}
public sealed class LpBitcoinHubEntity : BaseEntity
{
    public GameObject GameObject = new();
    public float HubWalletBtc = 4f;
    public Terminal Terminal = new();
    public object Explosion, WorldPosition;
    private bool _isExploding;
    public Terminal GetLinkedTerminal() => Terminal;
    public void Invoke(string callback) => typeof(LpBitcoinHubEntity)
        .GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, null);
    HUB_METHODS
}
public sealed class LpBitcoinRackEntity
{
    public GameObject GameObject = new();
    public Guid LinkedHubId;
    public bool Valid = true, AdvancedRack, IsMining = true, VisualMining = true;
    public float MiningProgress = .75f, BitcoinAmount = 2f;
    public void ApplyRackMiningVisual(bool mining) => VisualMining = mining;
    public void RefreshLinkedTerminalScreens() { }
    RACK_METHOD
}
public static class LpBitcoinIdent
{
    ORDER_METHOD
}
public static class LifecycleChecks
{
    private static int failures;
    private static void Check(bool condition, string message)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }
    private static (LpBitcoinHubEntity hub, LpBitcoinRackEntity[] linked, LpBitcoinRackEntity other, LpBitcoinRackEntity invalid) Fixture(bool fallback = false)
    {
        var scene = new Scene();
        Game.ActiveScene = scene;
        var hub = new LpBitcoinHubEntity();
        hub.GameObject.Scene = fallback ? null : scene;
        var linked = Enumerable.Range(0, 3).Select(i => new LpBitcoinRackEntity
        {
            LinkedHubId = hub.GameObject.Id, AdvancedRack = i == 2,
            GameObject = new GameObject { Name = "rack" + i, Scene = scene }
        }).ToArray();
        var other = new LpBitcoinRackEntity { LinkedHubId = Guid.NewGuid() };
        var invalid = new LpBitcoinRackEntity { LinkedHubId = hub.GameObject.Id, Valid = false };
        scene.Racks.AddRange(linked.Append(other).Append(invalid));
        return (hub, linked, other, invalid);
    }
    private static bool Stopped(IEnumerable<LpBitcoinRackEntity> racks) =>
        racks.All(r => !r.IsMining && r.MiningProgress == 0f && !r.VisualMining && r.BitcoinAmount == 2f);

    public static int Run()
    {
        Networking.IsHost = true;
        LifePunchMachineDestroyFx.Explosions = 0;
        var general = Fixture();
        general.hub.Invoke("OnDestroy");
        Check(Stopped(general.linked), "host general destruction stops all linked racks, resets progress/visual, preserves rack balances");
        Check(general.other.IsMining && general.invalid.IsMining, "general destruction leaves unrelated and invalid racks untouched");
        Check(general.hub.HubWalletBtc == 0f && !general.hub.Terminal.Linked, "general destruction retains wallet clear and terminal unlink");
        general.hub.Invoke("OnDestroy");
        Check(Stopped(general.linked), "repeated general destruction is harmless");
        Check(LifePunchMachineDestroyFx.Explosions == 0, "general removal does not create a death explosion");

        var fallback = Fixture(true);
        fallback.hub.Invoke("OnDestroy");
        Check(Stopped(fallback.linked), "general destruction uses active-scene fallback when hub scene is unavailable");

        var health = Fixture();
        health.hub.Invoke("OnDestroyed");
        Check(Stopped(health.linked), "health destruction retains trusted rack stop");
        health.hub.Invoke("OnDestroy");
        health.hub.Invoke("OnDestroyed");
        Check(Stopped(health.linked) && LifePunchMachineDestroyFx.Explosions == 1, "health followed by general destruction remains idempotent; one explosion");

        Networking.IsHost = false;
        var client = Fixture();
        client.hub.Invoke("OnDestroy");
        client.hub.Invoke("OnDestroyed");
        Check(client.linked.All(r => r.IsMining && r.MiningProgress == .75f && r.VisualMining), "client callbacks never stop linked racks");
        Check(client.hub.Terminal.Linked, "client general destruction never unlinks the terminal");

        Networking.IsHost = true;
        var empty = new LpBitcoinHubEntity();
        Game.ActiveScene = null;
        empty.Invoke("OnDestroy");
        Check(empty.HubWalletBtc == 0f && !empty.Terminal.Linked, "no-scene destruction is safe");
        Console.WriteLine($"Hub lifecycle: {11 - failures}/11 checks passed (offline method-body harness)");
        return failures == 0 ? 0 : 1;
    }
}
""";

source = source.Replace("HUB_METHODS", string.Join("\n", new[] { "OnDestroy", "OnDestroyed", "GetLinkedRacks" }
        .Select(name => Method(hub, "LpBitcoinHubEntity", name))))
    .Replace("RACK_METHOD", Method(rack, "LpBitcoinRackEntity", "StopMiningHost"))
    .Replace("ORDER_METHOD", Method(ident, "LpBitcoinIdent", "OrderLinkedRacks"));
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("HubLifecycleSubject", new[] { CSharpSyntaxTree.ParseText(source) },
    references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
using var dll = new MemoryStream();
var result = compilation.Emit(dll);
if (!result.Success)
{
    foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
        Console.Error.WriteLine(diagnostic);
    return 2;
}
var assembly = Assembly.Load(dll.ToArray());
var lifecycle = (int)assembly.GetType("LifePunch.DXRP.Addons.Bitcoin.LifecycleChecks")!.GetMethod("Run")!.Invoke(null, null)!;
var controls = NegativeControls.Run(root);
return lifecycle == 0 && controls == 0 ? 0 : 1;
