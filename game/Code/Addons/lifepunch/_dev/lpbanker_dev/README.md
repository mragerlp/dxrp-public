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
