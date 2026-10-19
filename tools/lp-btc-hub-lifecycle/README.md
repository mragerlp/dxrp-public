# Bitcoin hub destruction checks

From the repository root, with .NET SDK 10:

```powershell
dotnet run --project tools/lp-btc-hub-lifecycle/HubLifecycleChecks.csproj -- .
```

The harness parses and compiles the current production bodies of hub `OnDestroy`,
`OnDestroyed`, `GetLinkedRacks`, rack `StopMiningHost`, and `OrderLinkedRacks`.
It checks host/client boundaries, all linked standard/advanced racks, unrelated and
invalid racks, balance preservation, repeated callbacks, and missing-scene fallback.
Removing the general-destruction rack stop must fail the regression cases.

Engine networking, scene objects, terminal refresh and destruction FX are small
offline substitutes. The test explicitly invokes each callback; it does not prove
s&box callback timing, a live engine compile, replication, or editor behavior.
Roslyn is referenced from the installed SDK, with no NuGet package dependency.
