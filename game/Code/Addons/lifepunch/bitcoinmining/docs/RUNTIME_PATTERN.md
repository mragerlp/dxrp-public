# Bitcoin Miner — Runtime pattern

Original LIFEPUNCH implementation — `addons/docs/BITCOINMINING_IP_DOCTRINE.md`. Ship LifePunch-owned assets + proprietary code only.

The **Bitcoin Miner** is a code-driven interactive entity (mining payouts, upgrades, LIFEPUNCH hashd terminal).

## Asset split

| Layer | Slug | Path |
|-------|------|------|
| Entity | `gpu-rack` | `entities/gpu-rack/gpu-rack.prefab` |
| World mesh | `gpu-rack` | `models/lifepunch/bitcoinmining/gpu-rack/gpu-rack.vmdl` |
| Sounds | `bitcoin-miner` | `sounds/bitcoin-miner/*.sound` |

- World model: `addons/lifepunch/bitcoinmining/models/lifepunch/bitcoinmining/gpu-rack/gpu-rack.vmdl`
- Model tree: `gpu-rack/` (`source/`, `textures/{cord,psu,rack,motherboard,gpu}/`, `material-map.json`)
- Raw export archive (not published): `C:/lifepunch/reference-intake/bitcoinmining/gpu-rack-export/`

Reorganize script: `addons/scripts/Reorganize-BitcoinMinerGpuRack.ps1`

## Terminal (Phase 1 — Cornerman)

- **Open:** USE `BitcoinMinerHubEntity` → `HashdTerminal.OpenFromHub` (power gate when offline)
- **Close:** ✕ button / walk >150m from hub
- **Dev only:** `hashd` / `mine` ConCmds (`#if LIFEPUNCH_LOCAL`) — see `BITCOINMINING_TERMINAL_DOCTRINE.md`
- **Inside:** HASHD CLI (`help`, `mining`, `bitcoin`, `upgrade`, …); `menu` stubs tabbed UI (Phase 2)
- **Pattern:** `HashdTerminalHost` dual-build mount (same as StaffMenu / other LifePunch terminals)

Task brief: `addons/docs/briefs/CORNERMAN_BITCOINMINING_TERMINAL_TASK.md`

### Phase 1 source files

| File | Role |
|------|------|
| `HashdCommandHost.cs` | Dev-only ConCmd shim — hub-only open path |
| `HashdTerminalHost.cs` | `#if LIFEPUNCH_LOCAL` mount vs `GameManager.ShowUi`; viewer position for range |
| `GpuRackEntity.cs` | Synced mining state, RPCs, optional `IPressable` secondary open |
| `HashdTerminal.razor` | Define-free CLI; all gamemode coupling via host helpers |
