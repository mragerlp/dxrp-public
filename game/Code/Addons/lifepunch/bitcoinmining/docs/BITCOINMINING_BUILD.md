# Bitcoin Miner — Build status

| Piece | Status |
|-------|--------|
| gpu-rack publish tree (34 files) | **In repo** under `models/.../gpu-rack/` |
| Raw export archive | **Local** `C:/lifepunch/reference-intake/bitcoinmining/gpu-rack-export/` |
| `BitcoinMiningAddon.cs` identity | **Done** — entities `gpu-rack` / `advanced-gpu-rack`, mesh `gpu-rack` |
| `GpuRackEntity.cs` | **Done (Phase 1)** — passive compute; credits linked hub wallet |
| `HashdTerminalHost.cs` | **Done (Phase 1)** — dual-build mount/close |
| `HashdCommandHost.cs` | **Dev-only** — local-build `hashd` / `mine` smoke; players USE hub |
| `GpuRackRegistry.cs` | **Done** — multi-rig scan + `racks` / `select` / `mining start all` |
| `HashdTerminal.razor` + `.scss` | **Phase 1 done**; **Phase 2 modules** → `RED_BITCOINMINING_PHASE2_BUILD.md` |
| `bitcoin-terminal` (`computer.fbx`) | **Intaked** — `models/.../bitcoin-terminal/`; ModelDoc TBD |
| `gpu-rack.vmdl` + 5 vmats | **Done** — compiled `_c` in repo; power anim still **TODO** |
| `gpu-rack.prefab` | **Done** — scaffold on root; tune collider/LCD in editor |
| Sounds | **TODO** — own/licensed under `sounds/bitcoin-miner/` |
| `addons.json` content row | **TODO** after prefab path confirmed |

Canon brief: `addons/docs/briefs/BITCOINMINING_ENTITY_BRIEF.md`  
Grouping: `models/.../gpu-rack/MODEL_BUILD.md` + `material-map.json`
