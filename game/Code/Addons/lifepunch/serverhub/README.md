# LIFEPUNCH Server Hub (addon ident: `serverhub`)

PROPRIETARY & CONFIDENTIAL - (c) 2026 lifepunch.co. All rights reserved.
Author account: mrragerlp - Public alias (in-game / Steam / Discord): Bloodwave

The central server hub menu: one surface a player opens to read the state of the server
they are standing on. **v1 is a shell with one live tab.**

---

## Opening it

```
bind f4 serverhub      # or any key you like
serverhub              # console, toggles
lp_serverhub           # namespaced alias, same behaviour
```

The keybind is a **proposal, not a default** - nothing here registers `f4` or any other key.
Binding a key is the player's call, and choosing the server-wide default is the principal's.

> `hub`, `playerhub` and `menu` are already taken by other surfaces in this tree. This addon
> deliberately does not register them. If you meant the *player* hub - skills, stats, store -
> that is `playerhub`, a different addon with a different scope.

---

## The tabs

| Tab | State | What backs it |
|-----|-------|---------------|
| **Jobs** | **Live** | `GameModeJobs.All` / `AllGroups` - this server's own gamemode config |
| **Shop** | **Live** | `GameModeMarketItems.All` - the same catalog the native market screen sells from |
| **Server info** | Static copy + 2 live counters | The same job read, so the two tabs cannot disagree |
| **Banker** | **Reserved placeholder** | Nothing. There is no banker logic in this addon. |

---

## The one rule this addon follows

**It reports. The only thing it can do is ask the server to sell you something.**

No job is applied from here. No balance is edited from here. Nothing is sent to the server when
you merely open it. The single exception is the Shop's Buy control, and it is deliberately the
narrowest possible action: it sends **one item id** to a host RPC that prices the item and
charges for it itself.

The complete set of writes this addon performs, anywhere:

| Write | Where | What it is |
|-------|-------|------------|
| `Player.Local.LockCamera = menuOpen` | `LpServerHubHost.cs` | Client-local camera lock, so the cursor frees and the panel is clickable. The same engine hook the native staff menu uses. Not replicated, not game state. |
| `go.Name = "LifePunchServerHub"` | `LpServerHubHost.cs` | Names a GameObject this addon created itself. |
| `GameManager.Instance.PurchaseMarketItemHost( id )` | `LpServerHubShopActions.cs` | The one outbound call. Sends an item id and nothing else. |

There are no others. No `[Broadcast]`, no assignment to any job, balance, or config. Both
`WalletBalance` and `BankBalance` are `private set` on `Player`, so the money read stays
read-only by construction - there is no write path to reach even by accident.

### Why the purchase is safe to have here

- **The hub never charges anyone.** It cannot: the RPC payload is a `Guid`. Price, funds check
  and debit all happen host-side, in `PurchaseMarketItemHost`, which returns early unless
  `ChargeHost` succeeds. The debit gates the grant.
- **The hub never grants anything.** No item is spawned, equipped, or given by this addon.
- **The price shown is a label.** It is computed with the same expression the host uses, so the
  two agree, but nothing in this addon spends the number it computes.
- **The checks before the call are courtesy, not gates.** `CanPurchase`, the affordability test
  and the cooldown exist so a player gets an immediate reason instead of a silent no-op. The
  host re-runs all of them.
- **`PurchaseEntityHost` is never called.** It is the adjacent `GameManager` RPC with a very
  similar name that takes a client-supplied item body and spawns it with no debit. Read the
  header of `LpServerHubShopActions.cs` before touching the purchase path.

---

## Layout

```
serverhub/
  README.md
  code/
    data/
      LpServerHubModels.cs        view models + LpServerHubData, the ONE read seam
    ui/
      LpServerHubHost.cs          mount host (open/close/teardown)
      LpServerHubTabs.cs          tab enum + labels, icons, blurbs
      LpServerHubTokens.scss      locked token surface - the only file with literals
      LpServerHubPrimitives.scss  currency identity, entity dot, status chips
      LpServerHubRoot.razor       shell: header, nav, tab host, foot
      Jobs/   LpServerHubJobs.razor        LIVE
      Shop/   LpServerHubShop.razor        LIVE
              LpServerHubShopActions.cs    the ONE outbound call - read its header
      Info/   LpServerHubInfo.razor        original copy + live counters
      Banker/ LpServerHubBanker.razor      reserved placeholder
              LpServerHubBankerSocket.cs   the attach contract, no logic
```

Do not name any subdirectory `editor` or `unittest`: `rp.csproj` excludes both from the
build glob, and files under them would vanish from the compile with no error.

---

## Self-containment

This addon references **no other LIFEPUNCH addon**. Not `lifepunchulx`, not `lifepunchcore`,
not `playerhub`. Its only outward references are DXRP seams, each behind `#if !LIFEPUNCH_LOCAL`
so the addon also builds standalone:

- `GameModeJobs`, `GameModeJobDto`, `GameModeJobGroupDto` - the job roster
- `GameUtils.GetPlayersByJob` - occupancy
- `GameModeMarketItems`, `GameModeMarketItemDto` - the shop catalog and its permission helpers
- `Player.Local` - wallet, bank, camera lock
- `GameManager.ShowUi` - preferred mount path
- `GameManager.PurchaseMarketItemHost( Guid )` - the one outbound call
- `Notify`, `Cooldown`, `Constants.EntityTag`, `Config.Current.Game` - purchase-time courtesy checks

This costs a little duplication (the footer and scroll helpers are not reused) and buys
independence: the shared-helper directory was being restructured by another workstream while
this addon was written, and nothing here was affected by it.

---

## Adding the Banker later

`LpServerHubBankerSocket.cs` declares `LpServerHubBankerVm`. The Banker lane constructs one
and hands it to `LpServerHubBanker` through its `Model` property. That is the entire seam:

- `Model` is null on every path today, and the tab renders its reserved state.
- The shell, the nav, and the tab host need **no change** when the model becomes non-null.
- The field list in that file is a **starting proposal, not a settled schema**. It is the
  Banker lane's to reshape.

The placeholder draws inert grey shapes rather than sample balances. That is deliberate: a
placeholder showing plausible numbers trains players to read values that are not real, and it
is indistinguishable from a working feature until the real one ships.

---

## Verification status

Gated **offline** at Roslyn-syntax and structure level only:

- 10/10 `.cs` and `.razor` files parse with **0 syntax errors** (Roslyn `ParseText`, with a
  positive control proving the gate reports).
- 0 raw colour or metric literals in any component stylesheet; `LpServerHubTokens.scss` is
  the only file permitted to hold them.
- 0 adjacent `@(...)` pairs inside a single attribute; 0 non-ASCII bytes in any `.razor`.

- 0 references to `PurchaseEntityHost` or `SpawnMarketItemHost` anywhere in the addon.

**Not verified:** this has never been compiled, mounted, hotloaded, or seen on screen. No
editor, playtest, or screenshot was run. **No purchase has ever been executed.** Nothing here
is a runtime, economic, or visual claim - the purchase path is argued from reading the host
code, not from watching a balance move.
