# lpbanker dev lane (editor only, not shipped)

Console commands for proving the Banker hub and the bank assets in the editor. Each one refuses outside the editor and
writes nothing to game.scene: every object it makes lives in the Play scene and is gone at Stop.

| Command | What it does |
|---|---|
| `lp_banker_spawn_pile` | One cash pile in front of where you aim, dropped to the floor (editor host). |
| `lp_banker_press_pile` | Presses the first pile through its own `Press`, which opens the Banker hub. |
| `lp_banker_hub [tab]` | Opens the Banker hub on a tab: 0 Overview, 1 Investments, 2 Dividends, 3 Premiums, 4 Upgrades, 5 Vault, 6 Logs. |
| `lp_banker_hub_tab <tab>` | Switches the open hub to a tab (0..6). An out-of-range tab prints a warning. |
| `lp_banker_gate_probe` | Logs what the Banker job gate decides for you. `JobGateEnabled` is on: a real game needs the `banker` job tag; the editor always passes. |
| `lp_banker_spawn_lineup` | Every landed lpbanker model plus a citizen reference, in a line across your aim (bank assets, BANK-ASSETS-2). |
| `lp_bank_vault_spawn` | One bank vault in front of where you aim, with a keypad beside it already linked to the vault (editor host). |
| `lp_bank_vault_open` | Opens the nearest vault as a Banker, or closes it when it is open. A locked-down vault refuses and says why. |
| `lp_bank_vault_lock` | Locks the nearest vault down, or unlocks it when it is locked down. |
| `lp_bank_alarm_spawn` | One bank alarm in front of where you aim (editor host). It watches vaults within 600 units. |
| `lp_bank_alarm_trip` | Arms the nearest alarm if it is disarmed, then trips it the way an opened vault does. |
| `lp_bank_alarm_reset` | Resets the nearest alarm as bank staff: armed again, or disarmed while a watched vault is still open. |

Every figure in the hub is FIXTURE data that names its ruling (I1-I6 = DIRECTION_BANKER-HUB_R1 section 8). No command
moves money, fires a rail or persists anything. Close the hub with Esc or the close button.

## The vault and the alarm (BANK-VAULT-1)

A proof run, in order: `lp_bank_vault_spawn`, `lp_bank_alarm_spawn`, `lp_keypad_set 1234`, `lp_bank_alarm_trip`,
`lp_bank_alarm_reset`, `lp_keypad_try 1234` (the keypad code opens the closed vault; with the alarm armed that trips
it), `lp_bank_vault_lock`, `lp_keypad_try 1234` again (a locked-down vault stays shut), `lp_banker_hub 5` (the Vault
tab shows the live door and alarm rows), Esc. The keypad commands act on the keypad nearest you, so stand by the
vault's keypad.

The vault and the alarm are plain components and are never written to the host's world save. The keypad that
`lp_bank_vault_spawn` makes is a DXRP entity, and a host auto-save that fires during Play can write it into the save:
spawn right after a save and stop Play before the next one.

The bank models need their source files (fbx and png under `game/Assets/addons/lifepunch/lpbanker/*/assets/source`
and `textures`). They are not in git. Without them `lp_bank_vault_spawn` warns that the vault model is missing and
spawns nothing.

## SPINE-3 is in this tree (carried by BANK-CARRY-1)

Two rows of the table above behave differently from the SPINE-1 base:

- `lp_banker_press_pile`: the pile's own `CanPress` now includes the shared hub press gate, so the command logs `canPress=false` when you stand outside hub reach. Stand next to the pile first; before SPINE-3 the command ignored reach.
- `lp_banker_gate_probe`: editor only (it warns and stops outside the editor). It still logs the job-gate line, then a second line marked `LP_BANKER_SHARED_GATE_PROBE_20261023` with `hubKey`, `pile`, `pressGate`, `hostRange` and `hostWhole` for the pile `lp_banker_spawn_pile` made. Run that command first; with no pile the line reads `pile=none`. `hostWhole=false` on that pile is the expected reading, because the whole gate's ownership permission refuses an object with no entity tag and no owner. That is a static read of the gate, not yet confirmed in the editor.

The editor guard on `lp_banker_gate_probe` came with SPINE-3, so the opening line of this file now holds for every command in the table.
