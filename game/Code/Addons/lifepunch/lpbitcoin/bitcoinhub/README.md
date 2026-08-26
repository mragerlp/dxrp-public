# Staging hub — power / LED attach (Phase A)



Greenfield lane mounts `_dev` only. Staging stack attaches at spawn (or manually on prefab):



| Component | Role |

|-----------|------|

| `LpBitcoinStagingHubPower` | `IsPowered` sync, USE → panel |

| `LpBitcoinStagingHubVisuals` | Fence emissive + `status_led` point light |

| `LpBitcoinStagingHubPowerLeds` | `g_flSelfIllumScale` / tint on compiled fence vmat |

| `LpBitcoinStagingHubDestroyFx` | 250 HP death → DXRP printer explosion |

| `LpBitcoinStagingHubPhysics` | Static world machine collider + grab-tag recovery |



Production equivalents live in `bitcoinmining/` (`LpBitcoinHubEntity`, `LpBitcoinHubVisuals`, `LpHashdPanel`).



## DXRP hands vs hub (doctrine)



The hub is a **world machine**, not a carry prop. Compare to DXRP money printer:



| Input | DXRP requirement | Money printer | Bitcoin hub |

|-------|------------------|---------------|-------------|

| **Grab (LMB)** | `hands_interact` on root | Yes | **No** — removed from prefab |

| **Pocket (RMB)** | `pocket_item` on root | Yes | **No** — no tag; also `lifepunch_nopocket` |

| **USE / menu** | `IPressable` on entity component | Yes | Yes — `LpBitcoinStagingHubPower` / `LpBitcoinHubEntity` |

| **While grabbed** | adds `grabbed` (player walks through prop) | N/A if not grabbable | N/A |

| **After release** | `CollideGuard` may add `playerclip` | Ghost risk | Mitigated by `SettleAsWorldMachine` + tag strip |



`LifePunchMenuInteractGate` treats `lifepunch_nopocket` like a pocket deny even if something else in the scene has `pocket_item` behind the hub.



Pocket path reference: `PocketSystem.PickupHost` requires `pocket_item`; hub never gets that tag.



## Health & destruction



- **Max HP:** `250` (`LpBitcoinIdent.HubMaxHealth`) — prefab `HealthComponent` + runtime enforce on host.

- **Gun/melee:** `GameObject.TakeDamageHost` finds root `HealthComponent` (same as printer).

- **Explosions:** `IAreaDamageReceiver` on destroy component / `LpBitcoinHubEntity` forwards to health.

- **On death:** clone `prefabs/helpers/explosion.prefab` + `AreaDamage` (60, `DamageFlags.Explosion`) — same pattern as `PrinterEntity.OnDestroyed` (no smoke loop on hub baseline).

- **Dev test:** `lp_staging_hub_kill_test` (host, after `lp_spawn_staging_hub`).



## Playtest



1. `lp_spawn_staging_hub`

2. `lp_staging_hub_power_on` / `lp_staging_hub_power_off` — or `lp_staging_hub_power_toggle`

3. `lp_staging_hub_ui` — minimal power panel (same buttons)

4. **USE** hub in-world — `IPressable` on power component (no `hands_interact` required)

5. `lp_staging_status_led_tune` — log point-light anchor if panel LED needs nudging

6. `lp_staging_hub_kill_test` — explosion proof (optional)



**OFF** = red fence emissive + red status point light. **ON** = green.



**Collision:** hub is a **world machine** — no `hands_interact` (not grabbable with physics hands). `SettleAsWorldMachine` = static box + frozen rigidbody so players cannot walk through. If a hub was grabbed before this fix: `lp_staging_hub_collision_fix`.



Vmat: `bitcoinhub-sm-fence-led.vmat` — runtime drives self-illum; compile with emissive mask present.



## Prefab attach (optional)



On `bitcoinhub.prefab` root:



- `HealthComponent` — 250 / 250

- Tags: `entity`, `solid`, `lifepunch_nopocket` — **not** `hands_interact`, **not** `pocket_item`

- `LpBitcoinStagingHubDestroyFx` — wire `Explosion` → `prefabs/helpers/explosion.prefab`



Or rely on `lp_spawn_staging_hub` — calls `EnsureHubGameplayStack` (power + destroy FX).



---



# bitcoinhub — gameplay code map



**Prefab / model (Assets):** `Assets/addons/lifepunch/lpbitcoin/bitcoinhub/assets/`



Sources below live in **`Code/Addons/lifepunch/lpbitcoin/bitcoinhub/code/`** (same namespace `LifePunch.DXRP.Addons.Bitcoin`, one DXRP compile tree). Package-wide helpers remain in **`bitcoinmining/`**.



| File | Role |

|------|------|

| `LpBitcoinHubEntity.cs` | `code/components/LpBitcoinHubEntity.cs` | Hub USE, power, PIN, hashd anchor, printer-style death |
| `LpBitcoinHubVisuals.cs` | `code/components/LpBitcoinHubVisuals.cs` | Mesh/state visuals |
| `LpBitcoinHubPin.cs` | `code/components/LpBitcoinHubPin.cs` | PIN gate |
| `LpBitcoinHubAlert.cs` / `LpBitcoinHubAlertBridge.cs` | `code/components/` | Alerts |
| `LpHashdPanel.razor` (+ `.scss`) | `code/ui/` | Hub terminal UI |
| `LpHashdUiHost.cs` | `code/components/LpHashdUiHost.cs` | UI host |
| `LpBitcoinDevSpawn.cs` | `code/components/LpBitcoinDevSpawn.cs` | Dev spawn / orient audit |



Shared cyber helpers: `Code/Addons/lifepunch/LifePunch*.cs` (parent folder).



**Endgame:** move terminal/rack files to `lpbitcoin/{entity}/code/` per `PACKAGE_STAGING_LAYOUT.md` (hub promoted 2026-06-23).

