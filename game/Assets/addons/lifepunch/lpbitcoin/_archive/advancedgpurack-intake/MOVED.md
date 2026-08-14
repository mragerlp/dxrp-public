# MOVED — advancedgpurack slot

**Jun 2026 executive decision:** There is only one ship tier — **GPU Rack** (stacked farm mesh).

- **Canonical ModelDoc slot:** `lpbitcoin/gpurack/` (not this folder).
- **Canonical vmdl (play):** `bitcoinmining/.../gpu-rack/gpu-rack-stacked.vmdl`
- **Canonical prefab (play, rename pending):** `bitcoinmining/entities/advancedgpurack/advanced-gpu-rack.prefab` → will move to `gpurack/gpu-rack.prefab`

Do not add new assets here. See `BITCOINMINING-07` in `TECH_DEBT.md`.

**Jun 2026 compile law:** s&box compiles every `.vmdl` under `Assets/`. Archive folders must **not** contain `.vmdl` / `.vmat` — docs and source FBX only. The duplicate `gpu-rack-stacked.vmdl` here was removed because it pointed at the retired `advancedgpurack/` FBX path and broke ModelDoc on every editor boot.
