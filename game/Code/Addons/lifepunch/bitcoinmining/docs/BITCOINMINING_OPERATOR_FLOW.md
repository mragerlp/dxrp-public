# LIFEPUNCH Bitcoin — operator flow (DXRP Market)

Canonical player journey for Phase A proof and UI copy. Portal purchase limits are enforced by **DXRP Market content rows**, not this addon alone.

## Portal limits (per operator)

| Item | Max |
|------|-----|
| Bitcoin Hub | **1** |
| Bitcoin Terminal | **1** |
| GPU Rack | **3** (linked to that hub) |

Code constants: `LpBitcoinIdent.PortalMaxHubsPerOperator`, `PortalMaxTerminalsPerOperator`, `PortalMaxRacksPerHub`.

## Player steps

1. **DXRP menu → Market tab** — buy **Bitcoin Hub** (hub spawns **powered off**).
2. **Pick up / place** — hands (`hands_interact`), drop where they want it.
3. **USE hub** — **Secure Boot**: set 4-digit PIN, confirm PIN.
4. **Hub admin UI** — toggle **POWER ON** with the **header power switch** (top-right). Nothing links while off.
5. **Link terminal** — buy/spawn terminal from Market, place near hub, **Link terminal** on Overview (or Settings) while hub is **on**.
6. **USE terminal** — CRT rig0 (`rig@hub>`). LCD shows power off / not linked until steps 4–5 complete.
7. **Link GPU racks** — buy up to **3** racks from Market, place near hub, at rig0 type **`link`** (repeat per rack). Slots: **GPU Rack 1 … 3** (`GPURack-1 … GPURack-3`).
8. **Mine** — `mine` / `stop` at terminal; upgrades on hub **Servers** tab; wallet cashout on hub **Wallet**.

## Authority rules (code)

- Hub **`IsPowered == false`** → no terminal link, no rack link, no rig0 commands, terminal LCD **POWER OFF**.
- Terminal must be **linked at hub** before rack **`link`** at rig0.
- Rack yield comes from **CPU/core upgrades**, not a separate “advanced” tier.

## Dev playtest shortcuts

| Command | Use |
|---------|-----|
| `lp_bitcoin_spawn_kit` | Hub + terminal + 2 racks (unlinked, hub off) |
| `lp_bitcoin_hub_power_toggle` | Flip power without UI |
| `lp_bitcoin_link_terminal` | Requires hub **on** (same as player) |
| `lp_bitcoin_dev_link_all` | Skips setup — dev only |

Flatgrass proof: Law 5 — screenshot after Stop→Play sync, not prefab stage.
