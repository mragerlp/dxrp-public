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

## Negative controls for the rack-link paths (HUB-WIRING-1 U3)

`NegativeControls.cs` extracts the production bodies of `LinkNearbyRackHost`,
`LinkRackByTokenHost`, `FindHubByOwnerSteamId`, `IsDevPreviewHub`,
`RefuseIfOwnerHasAnotherActiveHub`, `CanOperateTerminal`, the rack
`FindNearestUnlinked` / `FindAllUnlinked`, `LpBitcoinUi.GetOrCreatePreviewHub` and
`LifePunchEntityOwnership.SharesOperator`, and drives them against offline stand-ins.
Each check is tagged: `NEG` fails on the origin/develop bodies and passes on the fixed
ones; `CTL` is a regression guard that passes on both.

The tool takes the root folder it reads from. To see the controls fail, put the
origin/develop blobs under a scratch folder with the same relative paths and point the
harness at it (copy the bytes, for example `git show origin/develop:<path>` redirected
to a file by a byte-safe tool; the files are the ones listed in `NegativeControls.cs`
plus `LpBitcoinIdent.cs` for the lifecycle checks):

```powershell
dotnet run --project tools/lp-btc-hub-lifecycle/HubLifecycleChecks.csproj -- <scratch-root>
```

## Terminal grab gate and hub-off feedback (HUB-WIRING-2 U2)

The same file drives the terminal's `OnFixedUpdate`, `ApplyGrabGateHost`,
`HasStuckGrabTags`, `TellCallerHubIsOff` and `OpenTerminalHost` together with the real
`LifePunchPropPhysics.MaintainGrabbablePlaceableProp`, `AllowHandsGrabTags` and
`DenyHandsGrabTags` bodies, so the interplay that caused the defect (Maintain ends by
re-adding `hands_interact`) is in the test, not assumed. Add `LpBitcoinTerminalEntity.cs`
and `LifePunchPropPhysics.cs` blobs to the scratch folder to see those controls fail.
It models tags and broadcasts as counters; it does not prove that the engine's Hands
equipment then lets E reach the menu.

## Opt-in terminal reach (HUB-WIRING-2 U3)

`LifePunchMenuInteractGate.IsTerminalTargetInOpenRange` is run with the real
`LifePunchMenuInteractRange` constants and `IsInOpenRange` on synthetic bounds (inches,
feet at z = 0). The one `NEG` check is the geometry: a 12 u terminal on a 29 u desk with
its pivot at the model centre is out of the pivot reach and inside the bounds reach. The
`NEW` checks fail on the develop blobs only because the method does not exist there;
they pin that the reach stays bounded in both axes, is never narrower than the pivot
reach, and falls back to the pivot check without a renderer. The model's real pivot and
bounds are not known offline, so the sizes are an assumption.

A method the slice added (for example `IsDevPreviewHub`) is simply absent from the
develop blobs; the controls that need it fail rather than crash. The claim recovery in
`TryClaimLinkableEquipment` is a stand-in, since it is not part of that slice.
