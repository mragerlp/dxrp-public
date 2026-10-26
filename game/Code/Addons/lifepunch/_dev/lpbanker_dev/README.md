# lpbanker dev lane (editor only, not shipped)

Console commands for proving the Banker hub and the bank assets in the editor. Each one refuses outside the editor and
writes nothing to game.scene: every object it makes lives in the Play scene and is gone at Stop.

| Command | What it does |
|---|---|
| `lp_banker_spawn_pile` | One cash pile in front of where you aim, dropped to the floor (editor host). |
| `lp_banker_press_pile` | Presses the first pile through its own `Press`, which opens the Banker hub. |
| `lp_banker_hub [tab]` | Opens the Banker hub on a tab: 0 Overview, 1 Investments, 2 Dividends, 3 Premiums, 4 Upgrades, 5 Vault, 6 Logs. |
| `lp_banker_hub_tab <tab>` | Switches the open hub to a tab (0..6). An out-of-range tab prints a warning. |
| `lp_banker_gate_probe` | Logs what the Banker job gate would decide for you (`JobGateEnabled` is false until a Banker job exists). |
| `lp_banker_spawn_lineup` | Every landed lpbanker model plus a citizen reference, in a line across your aim (bank assets, BANK-ASSETS-2). |

Every figure in the hub is FIXTURE data that names its ruling (I1-I6 = DIRECTION_BANKER-HUB_R1 section 8). No command
moves money, fires a rail or persists anything. Close the hub with Esc or the close button.

## After SPINE-3 (branch `cc-lp-1/SPINE-3-20261022`)

The code on this branch is the SPINE-1 base, so the table above is accurate here. Once SPINE-3 is merged, two rows change:

- `lp_banker_press_pile`: the pile's own `CanPress` now includes the shared hub press gate, so the command logs `canPress=false` when you stand outside hub reach. Stand next to the pile first; before SPINE-3 the command ignored reach.
- `lp_banker_gate_probe`: editor only (it warns and stops outside the editor). It still logs the job-gate line, then a second line marked `LP_BANKER_SHARED_GATE_PROBE_20261023` with `hubKey`, `pile`, `pressGate`, `hostRange` and `hostWhole` for the pile `lp_banker_spawn_pile` made. Run that command first; with no pile the line reads `pile=none`. `hostWhole=false` on that pile is the expected reading, because the whole gate's ownership permission refuses an object with no entity tag and no owner. That is a static read of the gate, not yet confirmed in the editor.

The editor guard on `lp_banker_gate_probe` is added by SPINE-3. On this base the command has no guard, so the opening line of this file does not yet hold for it.
