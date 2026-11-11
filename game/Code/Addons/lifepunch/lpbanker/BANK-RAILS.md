# BANK-RAILS: what real bank money would need (design for a ruling; nothing here is built)

Status: DESIGN ONLY, written under BANK-1 R1 (unit U5). No code in this addon moves value today. The vault
(`vault/LpBankVaultEntity.cs`) has a door state and nothing else; its contents on the Banker hub are fixture text;
the alarm makes noise. Every option below waits on the owner's ruling and its own GO.

## 1. The law this has to pass

Law B (ratified): money moves only through the rails that already exist. No new value-moving RPC. No price or
amount a client supplies.

The rails that exist in this tree (read 2026-11-11, `game/Code`):

| Rail | Where | What it does |
|---|---|---|
| `Player.ChargeHost( amount, reason, useBank )` | `Player/Player.Roleplay.cs:88`; used at `Chat/Commands/CoinflipCommand.cs:115`, `DropMoneyCommand.cs:64-70` | Host debit of one player's wallet or bank balance. |
| `Player.PayHost( amount, reason, inBank )` | `Player/Player.Roleplay.cs:226` | Host credit of one player's wallet or bank balance. |
| `LpBitcoinWallet.TryPayBank( callerId, amount, reason )` | `Addons/lifepunch/bitcoinmining/LpBitcoinWallet.cs:42` | The house wrapper: resolves the caller on the host and pays their bank balance. |

All three are per-player. There is no rail for a balance that belongs to a place or an institution. That one fact
decides most of what follows.

Rules any option must keep (the house money rules, `lifepunch-economy`):

1. The host resolves who is asking (`Rpc.CallerId`) and how much. The client sends neither.
2. Debit before any await; if an await must follow the debit, restore additively on failure.
3. Every mutation on the bank rail writes its audit line with a `"LIFEPUNCH <verb>"` reason.
4. A replayed request returns the earlier result; it never pays twice (an operation id).
5. Nothing that persists may be created by a restore (no mint on reload).

## 2. Four things the bank could do with money

### 2a. A real vault balance

The question: does the vault hold dollars that exist nowhere else?

| Option | What it is | Under Law B | Cost |
|---|---|---|---|
| V0 (today) | No balance. The Vault tab shows fixture text. | Passes: nothing moves. | None. |
| V1 Personal deposits | "The vault" is a view over each depositor's own bank balance. A deposit is `ChargeHost` (wallet) then `PayHost` (bank); the vault only totals what online players hold. | Passes with existing rails only. | The total is a display, not a pot. Nothing can be stolen from it, so it cannot fund a heist by itself. |
| V2 Server-held pot | The vault holds one synced number on the host. Deposits debit a player (`ChargeHost`); withdrawals credit a player (`PayHost`). The pot itself is new state. | The player legs pass. The pot is a new store of value: it needs a ruling that a host-held, non-player balance is allowed, a persistence decision, and a no-mint-on-restore proof. | Medium. One entity, one persisted number, two audited legs. |
| V3 Portal-held pot | The pot is an account in the Portal ledger, and the game asks the Portal to move money between it and players. | Cleanest ledger. Needs a Portal endpoint that does not exist in this tree. | Blocked on the Portal, not on game code. |

Recommendation: V1 first (buildable now, zero new value), then V2 only with a written ruling. V3 is the right
end state and is not mine to schedule.

### 2b. Banker dividends

The hub already draws Investments and Dividends as fixture (rulings I1, I2 in `hub/LpBankerHubState.cs`).

| Option | What it is | Under Law B |
|---|---|---|
| D0 (today) | Fixture figures, inert buttons. | Passes. |
| D1 Flat salary bonus | The Banker job's salary (a Portal row) is the whole reward. No dividend code. | Passes: salary is an existing rail the Portal already drives. |
| D2 Paid from the pot | Each tick, the host pays investors a rate from the V2 pot through `PayHost`. | Passes only if V2 is ruled. The rate, tick and cap are host constants or server config, never client input. A pot that reaches zero stops paying; it never goes negative. |
| D3 Paid from nothing | Each tick, the host credits investors a rate with no matching debit. | This is a faucet. It needs an explicit ruling as a faucet with a daily cap; without one it is a mint and must not be built. |

Recommendation: D1 now. D2 after V2. D3 only as a named, capped faucet.

### 2c. Bank Guard pay

| Option | What it is | Under Law B |
|---|---|---|
| G1 Salary only | The Bank Guard job's salary in its Portal row. | Passes. Nothing to build. |
| G2 Duty bonus | A bonus for time spent with the alarm armed, or for a foiled raid, paid with `PayHost`. | Passes as a faucet only with a ruling and a cap. "Foiled raid" needs a host-decided outcome, never a client claim. |

Recommendation: G1. Revisit G2 after the heist exists, because its trigger is a heist outcome.

### 2d. Heist loot

| Option | What it is | Under Law B |
|---|---|---|
| H0 (today) | Nothing can be taken. The Vault tab row says so. | Passes. |
| H1 Fixed purse | A successful heist pays the crew a fixed, host-set amount with `PayHost`, with a cooldown. | A faucet: needs a ruling, a cap per heist and per day, and a cooldown. Simple and safe to bound. |
| H2 Take from the pot | A successful heist moves part of the V2 pot to the crew (`PayHost`), debiting the pot first. | Zero-sum, so no mint, but only if V2 is ruled. The share, the floor left in the pot and the cooldown are host-set. |
| H3 Physical loot | The heist spawns money-bag items the crew must carry out; cash is paid when a bag is delivered. | The bag is new persistent-looking value. It must not survive a restart as money (rule 5), and the payout on delivery is H1 or H2 underneath. |

Recommendation: H1 for the first heist (bounded, testable), H3's bags as presentation on top of it, H2 when a pot
exists.

## 3. How the built pieces plug in (no money needed for any of this)

- The vault door already has the three states a heist needs: Closed, Open, Locked down. A heist tool (drill, C4)
  would be one more host-internal command beside the keypad signal, decided in `LpBankVaultRules.Decide`.
- The vault raises `ILpBankVaultEvent.OnBankVaultDoorOpened( vaultId, byKeypad )` on the host. A heist system
  listens to that the same way the alarm does; nothing in the vault changes.
- The alarm already tells Bank Guards and police when it trips. A PD response slice reads the same state.
- The job gate (`vault/LpBankJobGate.cs`) is keyed on job tags and creates no job. Jobs are Portal rows.

Bank heist (roadmap section B) and the Official map bank zone: I was given their names by the dispatch and have
not read the roadmap or the map files, so this section states only the seams above. Where the vault stands, which
doors are the vault's, and whether the bank zone is a trigger volume or a map entity are placement decisions for
the map slice. The vault is a plain component today (it is not saved with the world); a map-placed bank needs
either a prefab in the scene or a persistence ruling.

## 4. What I need ruled, in order

1. V: is a host-held, non-player balance allowed at all (V2), or does the bank stay a view over personal balances (V1)?
2. D: is the Banker's reward salary only (D1), or a dividend, and from where?
3. G: salary only, or a duty bonus?
4. H: fixed purse (H1), pot share (H2), or both, and the caps and cooldowns.
5. Persistence: does the vault (and any pot) survive a restart?
6. An older pair of banker designs exists (a BTC statement and a dollar vault) and was reconciled toward one of
   them in October. I did not re-read those records today; if they already answer 1 to 4, they win over this page.
