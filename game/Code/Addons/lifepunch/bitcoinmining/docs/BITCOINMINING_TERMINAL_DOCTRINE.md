# Bitcoin Miner terminal doctrine (v2)

**Owner canon (2026-06).** Parent: `addons/docs/PHYSICAL_TERMINAL_DOCTRINE.md`. Production laws: `addons/docs/CYBER_REFERENCE_LAWS.md`.

## Two surfaces (hub body vs HASHD monitor)

| Surface | Entity | Opens | Typing? |
|---------|--------|-------|---------|
| **Hub management** | `bitcoin-miner` (Ophion) | `LpHashdPanel` — Overview / Racks / Wallet / Settings | **No** — clickable dashboard + PIN gate |
| **Ops console** | `bitcoin-terminal` CRT only | `LpBitcoinTerminalPanel` — `rig0>` | **Yes** — mining, deposit, status |

| Entity | Role | Player USE? |
|--------|------|-------------|
| **Bitcoin Miner** (`bitcoin-miner`) | Hub — power, PIN, wallet, rack upgrades | **Yes** → admin panel |
| **HASHD monitor** (`bitcoin-terminal`) | rig0 ops console | **Yes** → CRT (hub-linked + powered) |
| **GPU Rack** (`gpu-rack` / large) | Linked compute — accrues BTC on rack | **No** — racks do not open the CRT |

**Terminal access is only through the bitcoin terminal entity.** GPU Rack entities never open the CRT. Mine/stop/deposit commands are typed at the terminal; rack USE is disabled.

Terminal registers to a hub only when the operator links it from **hub admin → Overview or Settings** while **hub power is on** (within `LinkRange`, default 512u). Until linked, USE on the terminal is disabled and the LCD shows **NOT LINKED** or **POWER OFF**. A linked terminal is required to **deposit** rack BTC into the hub wallet. Racks link via rig0 **`link`** at the CRT (hub powered + terminal linked). Operator flow: `docs/BITCOINMINING_OPERATOR_FLOW.md`.

## Hub wallet flow (PIN-gated)

The hub is the operator's **wallet and control plane**:

1. Racks mine → BTC accrues on each rack (`LpBitcoinRackEntity.BitcoinAmount`).
2. Operator USEs **bitcoin terminal** → CRT → `deposit` / `deposit all` / `deposit <index>` moves rack BTC into **`HubWalletBtc`** on the linked hub.
3. Operator USEs **hub** → PIN unlock → **Wallet** tab → cash out a specific amount or **Cash out all** to in-game bank (DXRP wallet pay path).

**Non-persistence:** Hub wallet BTC does **not** survive hub entity destruction or DXRP client disconnect cleanup — cash out to bank before teardown. Rack pending BTC on destroyed racks is likewise lost. P2P hub transfers (`send <steamid> <amount>`) move BTC between hub wallets by owner Steam ID; recipient must cash out to bank.

**Economy rails:** `addons/docs/CYBER_ECONOMY_RAILS.md` — BTC always bank cashout; hacker attacks wallet-only.

## Surfaces (do not conflate)

| Surface | What it is | Mining / economy? |
|---------|------------|-------------------|
| **Developer console** | s&box `>` — `lp_bitcoin_*` dev spawn / preview only | **Never** for players |
| **Hub panel** | `LpHashdPanel` | Power, metrics, upgrades, **wallet cashout** — no typed commands |
| **CRT** | `LpBitcoinTerminalPanel` | **Yes** — `mining start/stop`, `deposit`, `status` — **not** direct bank sell |
| **Rack LCD** | World telemetry (when wired) | Display only |

## Economy model (v2 code)

- **BTC balance per linked rack** — mining ticks on host while hub powered + rack mining.
- **Hub wallet** — `LpBitcoinHubEntity.HubWalletBtc`; filled by terminal deposit RPCs.
- **Cash out** — hub admin Wallet tab or `RequestCashOutHub` / `RequestCashOutAllHub`; credits player **bank** via `LpBitcoinWallet.TryPayBank` (`PayHost` with `inBank: true`).
- **No rack USE → sell** — `sell` at CRT returns an error directing operators to hub wallet cashout.
- **Hardware upgrades** — COMPUTE track purchase (`RequestPurchaseComputeTier` → `LpBitcoinPurchaseFlow`); BTC debited from the hub wallet, ledger-committed (slice 2).
- **Hub authority** — `IsPowered`, `CanOperateTerminal`, owner/PIN (`LpBitcoinHubEntity`).

Populate exact payout constants in `BITCOIN_REFERENCE_IMPLEMENTATION.md` on sign-off.

## Login / open flow

1. **USE hub** → `LpHashdPanel` (PIN gate when configured).
2. **USE bitcoin terminal** (not rack) → CRT boot splash → `rig0>` (blocked until hub links terminal in Settings; blocked if hub off).
3. **No player job verbs** in dev ConCmds in shipped builds — `LpBitcoinDevSpawn` removed before portal publish.

## Code seams (v2)

| File | Role |
|------|------|
| `LpBitcoinHubEntity.cs` | Hub `IPressable`; power; owner/PIN; `HubWalletBtc`; deposit/cashout RPCs |
| `LpBitcoinTerminalEntity.cs` | CRT prop; hub range link; sole player path to CRT |
| `LpBitcoinRackEntity.cs` | Mining tick; `Press` returns false (no CRT) |
| `LpBitcoinTerminalPanel.razor` | Gray CRT + boot graphic |
| `LpHashdPanel.razor` | Amber hub admin — wallet cashout UI |
| `LpBitcoinTerminalCommands.cs` | rig0 command dispatch (`deposit`, not `sell`) |
