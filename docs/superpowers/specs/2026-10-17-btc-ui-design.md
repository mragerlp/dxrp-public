# DRIVE-BTC-UI-1 - one Bitcoin UI in the house shell (design spec)

## 0. Header

- Seat: CC-DRIVE-1 | Machine: VENGEANCE | Slice: DRIVE-BTC-UI-1 under DRIVE-MORNING-1 | Date: 2026-10-17
- Dispatch: C:/cavelux/comms/staging/DISPATCH_DRIVE-MORNING-1_R1.md 6015 B / ACF766E7EF532A289E7E2782165C9575618013C5D7DA0BCF494EB25FD1BB46F6 (re-hashed for this draft: MATCH)
- Direction: C:/cavelux/comms/staging/DIRECTION_BTC-UI_R1.md 6503 B / C56C01BE530E731EFCA4AE6C09DE9EE1C28BEC3C5DEBEDC673A36CC6F57554BB (re-hashed: MATCH). It says "This is DIRECTION, not a GO" (DIRECTION:2).
- S1: C:/cavelux/comms/outbox/cc-drive-1/CC-DRIVE-1_DRIVE-BTC-UI-1_RETURN_2026-10-17-R1.md (its :3 names the standing dispatch DISPATCH_CC-DRIVE-1_STANDING_R1.md 12023 B).
- Pinned commit: b3d3b525 (b3d3b525706fa8584c67deffc15b20730b064392 = origin/develop). Worktree D:/Cavelux/dxrp-workbench/.claude/worktrees/DRIVE-BTC-UI-1, branch cc-drive-1/DRIVE-BTC-UI-1-20261017, clean at draft time.
- AS-RUN: claude-opus-5-5 (self-declared, UNVERIFIED)
- Standing: ADVICE-CLASS until the principal's GO. This slice is docs only. Every build slice waits for Phase A and its own "go <id>" (DIRECTION:2, :29-32).
- Revalidate: every path:line is at b3d3b525. Re-grep each seam before building on it. The live tree is HEAD d9c08031 and differs (section 13).

## 1. The word and the purpose

The principal's word, verbatim (DIRECTION:5):

> "take everything good about this horrid mess of UI for the bitcoin hub and the terminal and recreate what should be integrated with the playerhub, and then use our native UX/UI shell to make a new bitcoin UX/UI for the bitcoin hub. the hashd terminal needs fixing, but it can stay its current style."

The law behind it (DIRECTION:6): R-2832-A (the ULX top-tab grammar is the house shell), R-2823-A ($BTC and $LP Store are sibling tabs of the Player Hub), R-2832-B ($LP is cosmetics only).

Purpose: replace the orange BITCOIN OPS hub app with a Bitcoin Hub UI built in the Player Hub's house shell. One client view model feeds the hub UI, the Player Hub $BTC tab and the terminal readouts, so one fact reads one way everywhere. The HASHD terminal keeps its CRT style and gets its W6 fixes. Money moves only on the existing rails.

Target assumption (an ASSUMPTION, not a fact): the hub UI lives under lpbitcoin/bitcoinhub/code/ui/, and the view model lives under bitcoinmining/ in namespace LifePunch.DXRP.Addons.Bitcoin. The Player Hub already compiles against that namespace (BS:20). The build slice re-proves this before its first edit.

## 2. Verified seams

### 2.1 Cite legend

Every code path is under game/Code/Addons/lifepunch/ at b3d3b525. "[B]" stands for the U+20BF bitcoin sign. UI strings are transliterated to ASCII: middle dot -> ".", em dash -> "-", arrow -> "->", multiplication sign -> "x".

| Code | Path |
|---|---|
| HP / HPS | lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor / .razor.scss (the orange BITCOIN OPS app, despite the name) |
| HE | lpbitcoin/bitcoinhub/code/components/LpBitcoinHubEntity.cs |
| UH | lpbitcoin/bitcoinhub/code/components/LpHashdUiHost.cs |
| DS | lpbitcoin/bitcoinhub/code/components/LpBitcoinDevSpawn.cs |
| TP / TPS | bitcoinmining/LpBitcoinTerminalPanel.razor / .razor.scss (the HASHD terminal) |
| TE / TC / TS | bitcoinmining/LpBitcoinTerminalEntity.cs / LpBitcoinTerminalCommands.cs / LpBitcoinTerminalScreen.cs |
| RE / EC / ID / CT | bitcoinmining/LpBitcoinRackEntity.cs / LpBitcoinEconomy.cs / LpBitcoinIdent.cs / LpBitcoinComputeTrack.cs |
| SR / WA / PF / BU | bitcoinmining/LpBitcoinHubSendRules.cs / LpBitcoinWallet.cs / LpBitcoinPurchaseFlow.cs / LpBitcoinUi.cs |
| PR / PRS | playerhub/code/ui/LpPlayerHubRoot.razor / .razor.scss |
| TK / PM | playerhub/code/ui/LpPlayerHubTokens.scss / LpPlayerHubPrimitives.scss |
| PHH / PCC | playerhub/code/ui/LpPlayerHubHost.cs / LpPlayerHubChatCommand.cs |
| PB / PBS | playerhub/code/ui/Btc/LpPlayerHubBtc.razor / .razor.scss |
| BS | playerhub/code/data/LpPlayerHubBtcSource.cs |
| MS | LpMenuShell.razor (package root) |
| FT | lifepunchcore/LifePunchUiFooter.razor |

### 2.2 File inventory per UI

| UI | Path | Lines | Role |
|---|---|---|---|
| Hub app | HP | 3227 | PanelComponent. SECURE BOOT PIN gate replaces the whole shell (HP:16-88). Otherwise MS with sidebar and header controls (HP:91-128). Screens: Servers, Wallet, Transfers, System/Logs, Settings. MS draws the Dashboard. |
| Hub app | HPS | 8180 | Two palettes, $hashd-* and $ops-*, plus raw hex on 219 lines. Poppins font (HPS:57). |
| Hub app | UH | 84 | Open() closes the previews and the terminal, mounts, then BindHub (UH:28-37). |
| Hub app | MS + MS.scss | 337 / 255 | Sidebar / header / dashboard shell, accent passed inline (HP:92). |
| All | HE | 1464 | [Sync] state (HE:37-58). Every hub rail. Result envelopes (HE:1254-1329). |
| All | RE, EC, ID, CT | 540 / 124 / 446 / 158 | Rack state, rates, names, tier ladder. |
| Terminal | TP / TPS | 985 / 843 | CRT header, COMMANDS + STATUS sidebar, PIN gate, boot splash, log, rig@hub> prompt. |
| Terminal | TE / TC | 377 / 411 | Press/open path (TE:321-360). Command grammar (TC:19-410). |
| Terminal | TS | 55 | World LCD text. |
| Terminal | LpOpsCrtTerminal.scss | 486 | Shared CRT shell (style stays). |
| Player Hub | PR / PRS | 274 / 266 | House shell (ULX grammar): header, tab strip, page head, tab host, footer. |
| Player Hub | TK / PM | 53 / 68 | Locked tokens, panel primitives. |
| Player Hub | PB / PBS / BS | 160 / 341 / 155 | $BTC tab and its single read seam. |
| Player Hub | PHH / PCC | 196 / 41 | ConCmds playerhub / hub / playerhub_tab (PHH:52-81). Chat /playerhub with alias hub (PCC:28-29). |
| Shared | FT | 62 | Three-zone footer, Product + AccentColor params (FT:19-38). |

### 2.3 Readouts - BITCOIN OPS hub app (HP)

| # | Screen | Label | Value source | Unit | Precision / format | Trigger |
|---|---|---|---|---|---|---|
| H1 | header | power switch ON/OFF | IsPowered HE:37; HP:111-116 | - | - | BuildHash |
| H2 | header | wallet chip [B] n | HubWalletBtc HE:54; HP:117-120 | BTC | F4 HP:1782 | 0.5 s telemetry hash |
| H3 | header | Cash out all chip (label doubles as feedback) | NotifyCashOutSuccess HE:1254-1263 | - | - | RPC, 2.5 s clear |
| H4 | PIN gate | title / subtitle / pass label / 4 dots / error | AccessPinIsSet HE:38, AccessPinNeedsReEnrollment HE:49, local pass state | digits | 4 (LpBitcoinHubPin.cs:16-17) | BuildHash; NotifyPinResult HE:1268-1291 |
| H5 | Dashboard | callout naming a retired "Upgrades" tab | static HP:1055-1058 | - | - | static |
| H6 | Dashboard | Power on hub Done/Required + Power on/off | IsPowered; HP:1076-1086 | - | - | BuildHash |
| H7 | Dashboard | Link terminal + Link / Unlink / No terminal in range | HasLinkedTerminal HE:490; range TE:30 (512); HP:1087-1105 | - | - | telemetry hash |
| H8 | Dashboard | Link GPU racks: Done when count > 0; footnote | GetLinkedRacks HE:356; HP:1106-1118 (Done at HP:1109) | racks | int | BuildHash |
| H9 | Dashboard | Setup Ready / In progress | local HP:1139 | - | - | BuildHash |
| H10 | Dashboard | Linked racks n / 3 | HP:1150-1155; cap ID:52-58 | racks | int | BuildHash |
| H11 | Dashboard | Hub wallet [B] n + "$ n estimated value" | amount = wallet (HP:1160); $ = (int)BtcToCashUsd(wallet + pending) (HP:2982-2984) | BTC / USD | F4 / int then N2, so always ".00" (HP:1163) | telemetry hash |
| H12 | sidebar | 5 tabs + SETTINGS, alert dot | UnreadAlertCount HE:58 | - | - | BuildHash |
| H13 | Servers home | Hub card POWERED/OFF, n/3 RACKS | HP:523-537, 2087-2098 | racks | int | BuildHash |
| H14 | Servers home | Terminal card LINKED, ONLINE (= linked && hub powered) | HP:538-552, 2107 | - | - | telemetry hash |
| H15 | Servers home | GPU Racks MINING/FULL/UNLINKED n/3 + [B] sum | HP:557-585, 2117-2136 | BTC | 0.######## (HP:2132) | telemetry hash |
| H16 | Hub detail | chips, Wallet vital [B] n | HP:157-166; Owner [Sync] only under LIFEPUNCH_LOCAL (HE:50-52) | BTC | 0.######## (HP:2103) | telemetry hash |
| H17 | Hub / Terminal detail | 6 hub tracks, 6 security tracks, PLANNED | static HP:167-195, 228-256, 972-994 | - | - | static |
| H18 | Terminal detail | LINKED / ONLINE / SECURED; Link / Access / Power vitals | HP:218-227, 2107-2115 | - | - | telemetry hash |
| H19 | Rack chooser | GPU Rack 1 / 2 / Advanced, assigned BY LIST POSITION | HP:1983-2022 (ignores AssignedSlotToken RE:45) | - | - | telemetry hash |
| H20 | Rack chooser | mining "x TH/s . n BTC"; idle "TIER n or STOCK . x GHz" | TH/s = MiningRatePerMinute x 18.5f (HP:2155); ClockGhz (HP:2160) | TH/s / BTC / GHz | F1 / F4 / F2 | telemetry hash |
| H21 | Rack detail | chips LINKED / MINING / FULL / yield / [B] invested | HP:352-368, 1463-1470, 2187-2195 | x / BTC | 0.# / 0.## | telemetry hash |
| H22 | Rack detail | Compute "TIER n . x GHz", projected from tier | HP:1354-1366 | GHz | F2 (HP:1358) | telemetry hash |
| H23 | Rack detail | Rate [B] n /min, projected | HP:1376-1381 | BTC/min | 0.#### (HP:1381) | telemetry hash |
| H24 | Rack detail | Buffer [B] cur / [B] cap (n%) | HP:1476-1489; cap EC:120-123 | BTC / % | F4 / int | telemetry hash |
| H25 | Rack detail | upgrade tracks I-V (Compute live, 4 PLANNED, Fan RGB NOT ENABLED) | ComputeTier RE:55; HP:420-500 | tier | Roman | telemetry hash |
| H26 | Rack detail | price [B] n / $ n | QuoteSats CT:149-157; HP:1453-1457 | BTC / USD | 0.## / N0 | economy revision |
| H27 | Rack detail | delta "+x GHz . ~N payback" | HP:1400-1421 | GHz / min / hr / d | 0.# / N0 / 0.# | telemetry hash |
| H28 | Rack detail | confirm chip TIER n . [B] n / WORKING / SHORT / REJECTED | NotifyPurchaseResult HE:1314-1329; HP:463-488 | BTC | 0.## / 0.#### | RPC |
| H29 | Wallet | Hub wallet / Still on racks [B] n | HP:612, 616; pending HE:792 | BTC | F6 | telemetry hash |
| H30 | Wallet | Terminal linked Yes / "No - link on Dashboard" | HP:618-621 | - | - | telemetry hash |
| H31 | Wallet | USD value (hub), wallet only | HP:622-625 | USD | N2 (HP:624) | hash + economy revision |
| H32 | Wallet | Exchange rate "$n / [B]" plus "(m x)" under an event | EC:90-96; HP:626-635 | USD per BTC | N0 | PortalEconomyRevision EC:75 |
| H33 | Wallet | Your cash $ n (on-hand cash, not the bank) | WA:53-60; HP:636-639 | USD | int, unformatted | NOT hashed |
| H34 | Wallet | BTC amount input; Bank deposit preview "n BTC -> $n"; Cash out [B] n | parse InvariantCulture HP:2934-2938; preview EC:98-99 at display rate; button HP:2882 | BTC -> USD | F6 -> N2; button F4 | bind + RPC |
| H35 | Transfers | Your receive ID | Owner / SteamId HP:719-724, 2321-2333 | Steam ID | int | BuildHash |
| H36 | Transfers | hub wallet; static send grammar + example | HP:728; HP:756-762 | BTC | F6 | telemetry hash |
| H37 | Transfers | Live hub rate grid 0.01 / 0.05 / 0.10 / 0.25 -> $ | HP:791-799 | BTC -> USD | F2 / N2 | economy revision |
| H38 | Transfers | Still on racks, or literal "0" | HP:2341-2345 | BTC | F6 | telemetry hash |
| H39 | Logs | icon + kind + message + "just now / Nm ago / Nh ago" | AlertFeed HE:57, decoded HE:794-795; ages host stamp vs client Time.Now (HP:1900-1910) | time | m / h buckets | BuildHash + 30 s bucket |
| H40 | Settings | Hub power (mirror), Change PIN, terminal link, notifications + Clear, recipe | HP:877-927 | entries | int | BuildHash |
| H41 | footer | Bitcoin Ops + URL copy | FT:19-31 | - | - | footer hash |

### 2.4 Readouts - HASHD terminal (TP, TC) and world LCD (TS)

| # | Screen | Label | Value source | Unit | Precision / format | Trigger |
|---|---|---|---|---|---|---|
| T1 | header | HASHD TERMINAL + "hub power on/off . n racks . sel" | IsPowered; linked racks | - | - | BuildHash |
| T2 | sidebar | COMMANDS (16 static strings); the clicked row's text becomes ">copied" for 1.5 s | TP:70-79, 380-400 | - | - | flash timer |
| T3 | sidebar | STATUS Power / Racks linked / Selected | TP:80-94 | - | - | BuildHash |
| T4 | cog dropdown | rack name, FULL/MINING/IDLE, "token . [B] n/n" | ID:124-136 (token-first) | BTC | F4 (TP:285) | BuildHash at 1e-4 |
| T5 | PIN gate | HASHD / dots / incorrect pin | RequestTerminalUnlock HE:1028 | digits | 4 | RPC result |
| T6 | boot | INITIALIZING RIG... -> WORD... -> READY, "click to skip boot" | TP:135-143, 221-231, 854-873 | s | 0.35 s per line + 0.4 s hold | OnUpdate |
| T7 | log | imperative lines, max 200, never refreshed | TP:144-146 | - | - | append only |
| T8 | prompt | rig@hub> / "type command..." | TP:147-150, 232 | - | - | static |
| T9 | status cmd | "token . state . n/n BTC (p%)" / rate / wallet + pending | TC:208-217 | BTC, BTC/min, % | F6, F0 | one-shot |
| T10 | info / wallet / racks cmds | GHz, cap, $; wallet and on-racks with $ | TC:183-244 | GHz / BTC / USD | F2 / F6 / N0 or int | one-shot |
| T11 | results | deposit / send / mining / link + ERR lines | TC:246-273; SR:42-72 | BTC | F6; printed before the host resolves | one-shot |
| L1 | LCD | NOT LINKED / POWER OFF / STANDBY | TS:21-31 | - | - | screen refresh |
| L2 | LCD | IDLE or MINING; [B] total = racks + wallet (a third wallet reading) | TS:33-46 (TS:36) | BTC / USD / BTC per min | 0.00000000 / N0 / 0.00000 "/m" | screen refresh |

### 2.5 Readouts - Player Hub $BTC tab (PB, BS)

| # | Label | Value source | Unit | Precision / format | Trigger |
|---|---|---|---|---|---|
| P1 | BITCOIN HUB + PIN SET/NO PIN + n RACKS | hub = FIRST hub owned by the viewer (BS:31-35, HE:447-466); PB:24-29, 122 | racks | int | Root BuildHash adds LiveHash while $BTC is active (PR:270-271; BS:42-61) |
| P2 | POWER ON/OFF + switch | IsPowered BS:115; PB:33-41 | - | - | LiveHash |
| P3 | HUB WALLET [B] n / $ n | BS:118-119; PB:44-48 | BTC / USD | F6 (BS:29) / N0, wallet only | LiveHash (exchange rate NOT hashed) |
| P4 | PENDING ON RACKS [B] n | BS:111, 120; PB:49-53 | BTC | F6 | LiveHash |
| P5 | rack label | ID:124-136 token-first (BS:94) | - | - | token NOT hashed |
| P6 | tier chip "TIER " + RomanTier, so "TIER STOCK" at tier 0 | BS:95; ID:139-140 | tier | Roman | LiveHash |
| P7 | CLOCK "x.x GHz x n" (n = cores, always 1) | synced ClockGhz BS:96 | GHz | 0.0 | ClockGhz NOT hashed |
| P8 | RATE "n/min", no sign | BS:97 | BTC/min | F6 | LiveHash |
| P9 | mined [B] n / $ n; MINING/IDLE | BS:98-99; PB:92-98 | BTC / USD | F6 / N0 of int | LiveHash |
| P10 | Deposit all / Cash out all enable | string compare against "0.000000" (PB:123-124) | - | - | render |

### 2.6 Controls today -> rail

| Surface | Control | Rail (path:line) |
|---|---|---|
| Hub app | header switch, Settings switch, Dashboard power card | ToggleHubPower HP:2430-2436 -> SetPowered HE:321 -> SetPoweredHost HE:337-352 |
| Hub app | Cash out all (header, Wallet) / Cash out [B] n | RequestCashOutAllHub HE:442 / RequestCashOutHub HE:440 (HP:2940-2963) |
| Hub app | Upgrade + confirm chip | rack.RequestPurchaseComputeTier RE:390-398 -> PF |
| Hub app | Link / Unlink terminal (card, Settings) | RequestLinkNearbyTerminal / RequestUnlinkTerminal HE:551-553 |
| Hub app | PIN keypad (unlock / set / change) | HE:429-434 |
| Hub app | Logs open / Clear | RequestMarkAlertsRead / RequestClearAlerts HE:812-814 |
| Hub app | (none) | no deposit, mining or send control in HP (M1 grep, positive control RequestCashOutAllHub) |
| Terminal | prompt commands | mining RE:310; deposit HE:436-438; send HE:444; link / unlink HE:521-523 |
| Terminal | PIN keypad | RequestTerminalUnlock HE:1028 |
| Player Hub | POWER / Deposit all / Cash out all / rack MINING | BS:123-143, 102-108 -> HE:321, 436, 442; RE:310 |

## 3. Keep list (DIRECTION section 2 -> code)

| # | DIRECTION item | Status | Code |
|---|---|---|---|
| K1 | Checklist power -> link terminal -> link racks, DONE + undo | PARTIAL: power and terminal rows have undo; the racks row only routes to Servers. Rack unlink exists only at rig0 (TC:135). | HP:1067-1119 |
| K2 | Snapshot: setup ready, linked racks n/max, hub wallet + estimated value | PRESENT with the W2 mislabel | HP:1136-1167, 2982-2984 |
| K3 | Rack status pills LINKED / MINING / yield | PRESENT (+FULL, invested) | HP:352-368, 2187-2195 |
| K4 | COMPUTE / RATE / BUFFER (fill %) / YIELD | PRESENT (compute and rate projected, W3) | HP:393-418, 1354-1381, 1476-1489 |
| K5 | UPGRADE TRACKS with I-V ladder | PRESENT | HP:420-497, 996-1002 |
| K6 | Cost in BTC and $ + payback | PRESENT | HP:1400-1421, 1453-1457 |
| K7 | Compute live; Cooling, Power delivery, Efficiency, Payout buffer PLANNED | PRESENT | HP:1227, 1282-1290, 1352 |
| K8 | Inline confirm TIER n . cost / check / cross | PRESENT (state from host envelope only) | HP:463-487; HE:1314-1329 |
| K9 | Terminal detail LINK / ACCESS / POWER | PRESENT | HP:200-227, 2105-2115 |
| K10 | SECURITY TRACKS (6) as PLANNED ladders | PRESENT (static) | HP:228-253, 984-994 |
| K11 | Wallet: hub wallet, still on racks, terminal linked, USD value, exchange rate, your cash | PRESENT ("Your cash" is on-hand cash, not the bank the cash-out pays) | HP:609-640; WA:53-60 |
| K12 | Amount input / bank deposit preview / cash out + cash out all | PRESENT | HP:642-691, 2926-2963 |
| K13 | Receive ID (Steam id, copy) | PRESENT | HP:717-738 |
| K14 | Transfers hub wallet | PRESENT | HP:725-729 |
| K15 | Send only from the linked terminal; grammar + copy example | PRESENT (example hard-coded) | HP:741-772 |
| K16 | LIVE HUB RATE table | PRESENT (display rate, no donor multiplier) | HP:776-799 |
| K17 | Transfers still on racks | PRESENT | HP:800-803, 2341-2345 |
| K18 | Logs: commands, capacity, intrusion, transfers, relative times | PRESENT (20-entry synced feed) | HP:808-858, 1900-1910; HE:898-920 |
| K19 | Settings hub power mirror | PRESENT | HP:877-886 |
| K20 | Settings Change PIN | PRESENT | HP:888-897 |
| K21 | Settings terminal link toggle | PRESENT | HP:899-910 |
| K22 | Settings notifications count + clear | PRESENT | HP:912-921 |
| K23 | Settings getting-started recipe | PRESENT | HP:924-927 |
| K24 | Terminal command list | PRESENT | TP:70-79; TC:404-410 |
| K25 | Terminal STATUS column | PRESENT | TP:80-94 |
| K26 | Boot sequence, click to skip | PRESENT | TP:135-143, 221-232 |
| K27 | rig@hub> prompt | PRESENT | TP:147-150, 232 |
| K28 | Terminal PIN keypad | PRESENT (own gate) | TP:97-134, 474-478 |
| K29 | PH POWER switch | PRESENT | PB:33-41; BS:123-129 |
| K30 | PH HUB WALLET | PRESENT | PB:44-48 |
| K31 | PH PENDING ON RACKS | PRESENT | PB:49-53 |
| K32 | PH Deposit all | PRESENT (no terminal check, see W2/R-BTCUI-11) | PB:57-60, 123 |
| K33 | PH Cash out all | PRESENT (CanManage is the literal true, BS:117) | PB:61-64, 124 |
| K34 | PH GPU RACKS cards: CLOCK x n, RATE /min, mined BTC / $, MINING switch | PRESENT | PB:71-106; BS:92-108 |
| K35 | PH PIN SET and n RACKS pills | PRESENT | PB:26-29, 122 |

No item in DIRECTION section 2 is ABSENT. K1 is the only PARTIAL.

## 4. W1-W7 root causes

Frame rule: the principal's 17 screenshots are not on disk (C:/cavelux/telemetry/sbox/proof/btc-ui-ref does not exist), so his figures are UNVERIFIED-BY-FRAME. Only the seat's own frame (section 13) is evidence.

**W1 - two shells for one object.**
- Symptom: the Player Hub speaks the house shell; the hub opens an orange Poppins app with a sidebar; the terminal is a third style.
- Mechanism: three independent PanelComponents, each with its own sheet, font and palette, and no shared shell. The Player Hub roots at lp-playerhub (PR:17, 181) on TK (PRS:9). The hub app is MS with AccentColor "#e87d3e" (HP:91-96) and Poppins (HPS:57); it opens from the hub press (HE:296-319 -> UH:28-37). The terminal is lp-ops-crt (TPS:18-23). BU:15-20 keeps the hub app and the terminal mutually exclusive but ignores the Player Hub.
- Confidence: CONFIRMED by code. The orange shell is CONFIRMED-BY-OWN-FRAME.

**W2 - one state, several readings.**
- (a) Symptom: Transfers 0.500200 on racks vs 0.000000 elsewhere (UNVERIFIED-BY-FRAME). Mechanism: every pending readout is one expression, HE:792, read at HP:616, HP:2343 and BS:111. For one hub at one instant they cannot disagree. Two vectors remain: captures taken at different times, or the Player Hub reading a different hub. The Player Hub uses the first owned hub (BS:31-35, HE:447-466); the hub app is bound to the pressed hub (HE:312-319). Confidence: the single expression is CONFIRMED; the cause of his split is PLAUSIBLE.
- (b) Symptom: $2,500.00 estimated value against a 0.0000 wallet. Mechanism: the row labelled "Hub wallet" shows the wallet at F4 (HP:1160) and a $ figure of (int)BtcToCashUsd(wallet + pending) printed N2 (HP:1163, 2982-2984). With the default $5000/BTC (EC:23, 32): 0.5002 x 5000 -> int 2500 -> "2,500.00". Confidence: the mechanism is CONFIRMED. The fit to his figure assumes the default rate (UNVERIFIED-BY-FRAME). The own frame shows "$0.00 estimated value" under [B]0.0000, which is consistent.
- (c) Precision by site:
  - F4: HP:1782 (header, snapshot), HP:1483, HP:2155, HP:2882, TP:285.
  - F6: HP:612, 616, 728; BS:29; TC:215-217; HE:1246.
  - 0.########: HP:2103, 2132.
  - 0.##: HP:1454. 0.####: HP:1381. F2: HP:795. 8 dp on the LCD: TS:44.
  - USD is N0 in the Player Hub and terminal and N2 in the hub app.
  - Confidence: CONFIRMED.

**W3 - rack identity.**
- Symptom: his Tier V 78.1 GHz "GPU Rack 1" in the Player Hub vs "GPU Rack 1" at STOCK 2.44 GHz in Ops (UNVERIFIED-BY-FRAME).
- Mechanism: the hub app names slots by list position. HP:1983-2022 hard-codes the first standard rack as ("GPU Rack 1", "gpurack-1"), in OrderLinkedRacks order: advanced, then name, then Id (ID:163-169). Every other surface answers from the rack's own AssignedSlotToken (ID:293-309; RE:41-45, "slot identity is never re-derived from link order"). The hub app's rack Compute vital uses ProjectedClockGhz(ComputeTier) (HP:1354-1366), while the Player Hub reads the synced ClockGhz (BS:96). The "Tier I" is plausibly the next-tier chip, TIER(ComputeTier+1) (HP:1371-1372). Tier V = 2.44 x 32 = 78.08 GHz (CT:129-144).
- Confidence: the mechanism is CONFIRMED; the exact pairing in his frames is PLAUSIBLE.

**W4 - rate units.**
- Symptom: GHz x n and BTC/min in the Player Hub, TH/s in Ops Servers, GHz in rack detail.
- Mechanism: the canonical rate is MiningRatePerMinute = ClockGhz x 0.005 x cores x yield x 60/90 (EC:19-20, 108-112). "TH/s" is that BTC/min value times a bare 18.5f literal at 4 sites, all in HP (HP:2155, 2204, 2221, 1912-1916), with no model basis. The Player Hub prints "F6/min" with no sign (BS:97) and "x n" for cores that are always 1 (CT:143). Stock is 0.008133 BTC/min, shown as 0.2 TH/s.
- Confidence: CONFIRMED.

**W5 - PIN entered twice on two keypads.**
- Mechanism: two client keypads and one host secret.
  - Hub keypad: clr wipes the whole buffer and steps back a pass, under a backspace icon (HP:64-69, 2499-2527). Modes: unlock, set, change (HE:429-434). The unlock is panel-local and re-asked on every open (UH:28-37 remounts).
  - Terminal keypad: clr deletes one digit (TP:475-477), unlock only (HE:1028).
  - Both validate the same digest, with one per-caller lockout of 5 fails -> 30 s (HE:961-987).
  - The host rails check ownership, not an unlock session (HE:1356-1366), so the PIN is a client-side view gate.
- Confidence: CONFIRMED.

**W6 - terminal open failure, copied overlay, blank first paint.**
- (i) An unlinked terminal is not pressable (TE:321-322). The not-linked and hub-off refusals are host Log.Warning only (TE:347-357), so the player gets nothing.
- Reach and hands_interact: the M1 inventory reports that the terminal uses a pivot range while the hub uses renderer bounds (LifePunchMenuInteractRange.cs:26-74). It also reports that MaintainGrabbablePlaceableProp re-adds hands_interact every tick (LifePunchPropPhysics.cs:184-254), while ApplyGrabGateHost removes it only on an edge (TE:160-179, re-opened: edge-only). With Hands equipped, E is then refused (LifePunchMenuInteractGate.cs:157-191).
- (ii) There is no toast. The clicked command row's own text becomes ">copied" for 1.5 s (TP:74-76, 380-400). The boot / PIN overlay covers the sidebar (TPS:236-253).
- (iii) Bind clears the log and raises the overlay (TP:323-357). Boot lines append one per 0.35 s, and FinishBoot fires after 2.15 s (TP:230-231, 854-873), so the first frame shows an empty log.
- Confidence: each chain is CONFIRMED by code. The reach and tag chains were carried from M1 and not re-opened here. Runtime behaviour is UNVERIFIED.

**W7 - duplicated power controls.**
- Mechanism: one stored field, IsPowered [Sync] (HE:37). Four writers call one rail: the header (HP:111), Settings (HP:882), the Dashboard card (HP:1085) and the Player Hub (PB:38, BS:123-129), all reaching SetPowered -> SetPoweredHost (HE:321, 337-352).
- Divergence paths:
  - The Player Hub may read a different hub (BS:31-35).
  - The terminal's boot line "hub power" goes stale.
  - SetPoweredHost refuses a non-owner silently (HE:340-341).
- Confidence: the single source is CONFIRMED; the cause of the observed divergence is PLAUSIBLE.

## 5. ONE-STATE view model

One client-side builder, LpBitcoinHubState (proposed name), keyed by one hub GameObject.Id and rebuilt from synced fields. It hashes every input it shows: today PortalEconomyRevision, ClockGhz, AssignedSlotToken and HasLinkedTerminal are missing from the Player Hub hash (BS:42-61). Readers: HUB = the new hub UI, PH = the Player Hub $BTC tab, TERM = terminal STATUS / dropdown / LCD (the terminal's command output stays imperative text). "R-n" = ruling R-BTCUI-n.

| Field | Type | Unit | Canonical precision | Source (path:line) | Refresh | Readers |
|---|---|---|---|---|---|---|
| HubId | Guid | - | - | bound hub UH:35; PH resolver BS:31-35 per R-13 | on bind | all |
| IsPowered | bool | - | - | HE:37 | hash | all |
| PinSet / PinNeedsReEnroll | bool | - | - | HE:38 / HE:49 | hash | HUB, PH |
| OwnerSteamId | long | Steam ID | integer | HE:50-52 (non-LOCAL path UNVERIFIED) | hash | HUB |
| WalletBtc | float | BTC | R-1 | HE:54 | hash at sat resolution (BS:51 pattern) | all |
| WalletUsd | float | USD | R-3 | EC:77 | hash + PortalEconomyRevision (EC:75) | HUB, PH |
| PendingBtc / PendingUsd | float | BTC / USD | R-1 / R-3 | HE:792; EC:77 | hash of per-rack sats | all |
| RateLabel | string | USD per BTC | N0 | EC:90-96 | PortalEconomyRevision | HUB |
| TerminalLinked / TerminalInRange | bool | - | - | HE:490 / HE:510-511, TE:30 | hash (new) | HUB, PH |
| RackCount / RackMax | int | racks | int | HE:356-365 / ID:52-58 | hash | all |
| Rack.Id | Guid | - | - | rack GameObject.Id (BS:93) | hash | all (action target) |
| Rack.Token / Rack.Name | string | - | - | ID:143-155 / ID:124-136 (token-first) | hash incl. AssignedSlotToken RE:45 | all |
| Rack.Advanced | bool | - | - | RE:32 ([Property]; replication UNVERIFIED) | hash | all |
| Rack.IsMining | bool | - | - | RE:46 | hash | all |
| Rack.Btc / Rack.CapBtc / Rack.FillPct | float / float / int | BTC / BTC / % | R-1; integer % | RE:47; EC:120-123; HP:1476-1480 | hash | all |
| Rack.Full | bool | - | - | Btc >= Cap (RE:324-325) | derived | all |
| Rack.Tier / Rack.TierLabel | int / string | tier | RomanTier, "STOCK" at 0, no "TIER " prefix (R-4) | RE:55; ID:139-140 | hash | all |
| Rack.NextTierLabel | string | - | "Next: TIER n" | HP:1371-1372 | derived | HUB |
| Rack.ClockGhz | float | GHz | R-2 | RE:48, synced (never ProjectedClockGhz for current state) | hash (new) | all |
| Rack.RateBtcPerMin | float | BTC/min | R-2 | RE:61; EC:108-112 | derived | all |
| Rack.Yield | float | x | 0.# | RE:58-60 | derived | HUB |
| Rack.NextQuoteSats / NextQuoteUsd | long / float | sats / USD | R-1 price form / R-3 | CT:149-157; EC:77 | derived | HUB |
| Alerts / Unread | list / int | - | - | HE:57-58, 794-795 | hash | HUB |
| LocalCashUsd | int | USD | N0 | WA:53-60 | hash (new; today unhashed) | HUB |
| LastResult | struct | - | - | HE:1254-1263, 1314-1329 | RPC | HUB (PH per R-11) |
| Actions | delegates | - | - | section 7 rails only | - | HUB, PH |

Duplicates this removes (M1 census, CONFIRMED by code):
- Rack name: positional (HP:1983-2022) vs token (BS:94; TP:258-259).
- Tier label: three dialects (BS:95; HP:1013-1020, 1365; TC:231).
- Clock: projected (HP:1354-1367) vs synced (BS:96; HP:2160; TC:231).
- Rate: F6/min (BS:97), 0.####/min (HP:1381), TH/s (HP:2155), F6 BTC/min (TC:216), 0.00000/m (TS:46).
- Wallet precision: F4 / F6 / 8 dp across HP, BS, TC and TS.
- Wallet $: wallet-only N0 (BS:119), wallet+pending (HP:2982-2984), wallet-only N2 (HP:624), and the LCD total (TS:36).
- Pending: five readers at three precisions.
- FULL state: present in HP:357-360; "CAP FULL" only when mining (TC:211-212); absent in the Player Hub.
- Power writers (W7).
- PIN gates (W5).
- Manage authority: CanManage = true (BS:117) vs host CanManageHub (HE:1356-1366).
- Deposit preconditions: three sets (section 7).
- Linked-rack counts: five readers.
- Exchange-rate refresh: hashed in HP, not in the Player Hub.
- Action feedback: reaches LpHashdPanel only (HE:1260, 1325).
- Hub resolution: three rules (BS:31-35; UH:35; TE:362-373).

## 6. Information architecture

### 6.0 Locked visual and engine constraint contract

- Tokens are canon: TK:13-53. Colours: $canvas $bg $surface $surface-inset $card-header $border $lp-blue $lp-blue-hover $accent-soft $accent-soft-strong $accent-focus $text-main $text-dim $good $bad $amber $btc $cash $good-soft $bad-soft $dim-soft $dim-soft-strong. Font: $font-mono. Geometry: $radius $radius-chip $radius-card $radius-switch $switch-travel $hit $gap-control $gap-content $gap-dense $panel-pad $bar-h $row-h $header-h $tabs-h.
- No raw hex, radius or spacing in any new rule body. The shell itself already carries raw literals (PRS:183-184 gap 18px / padding 0 20px; PRS:199 font-size 13px; PM:52-53 gap 3px / padding 8px 10px). The hub UI reuses those classes and never re-types the numbers. Tokenising them is R-BTCUI-10.
- TK:9 flags that $lp-blue follows ULX #1689ff, not LIFEPUNCH_UI_STANDARD's #017AEF. That canon question is carried, not resolved here.
- Law 17: the [B] sign is $btc (TK:31), the $ sign is $cash (TK:32), amounts are $text-main. Spans: lp-money-btc / lp-money-cash / lp-money-amt / lp-money-divider (PB:46-47, 93-95).
- Separator law: composites are built in C# (exemplar HP:1360-1366).
- Engine: flex only, no grid, no @media, no inline-flex. Every state is a standalone class (exemplars PRS:208-213 tab-sel; PB:126-129 lp-switch-off). No compound state selectors: HP:111 "header-power-switch on/off" is the retiring anti-example. @code stays ASCII. Never two adjacent @(...) in one attribute. A PROPRIETARY header goes on every new file. Every fixed height in the 700 px window carries a PANE-BUDGET comment (exemplar PBS:5-8).
- The HASHD terminal keeps its CRT identity and is exempt from this contract.

### 6.1 Bitcoin Hub UI (new, house shell)

Shell grammar reused from the Player Hub:
- window (PR:18); header title-stack with wordmark-life "lifepunch" + wordmark-accent ".btc" and title-sub "BITCOIN HUB" (PR:19-25, per R-8);
- header-actions with header-chip pills and chrome-close (PR:26-38);
- tab strip using tab / tab-sel (PR:41-48, 183);
- page head with title + blurb + LIVE chip (PR:51-62); tab host (PR:64-85);
- LifePunchUiFooter with Product "Bitcoin Hub" (PR:88; FT:19-38);
- panels from PM:9-59 (lp-hub-panel, lp-hub-bar, lp-hub-chip-row, lp-hub-field-row / lp-hub-field);
- chips, switches and money spans from the $BTC plate grammar (PB:23-29, 38, 46-47, 126-135).

Header pills: PIN SET / NO PIN, n/3 RACKS, the wallet chip [B] n (a mirror that opens Wallet), and the single power switch (R-5). Open path: hub press -> OpenHubHost -> OpenHub -> UH.Open (HE:296-319), unchanged.

| Tab | Content (keep list) | Controls -> rail | Notes |
|---|---|---|---|
| Overview | K1 checklist (power -> link terminal -> link racks; DONE + undo); K2 snapshot: setup, racks n/3, Hub wallet [B] + $ (wallet only), Pending on racks [B] + $ as its own row (R-3) | Power on/off -> SetPowered HE:321; Link / Unlink terminal -> HE:551-553; "Racks" -> local tab | No "Upgrades" copy (HP:1055-1058, 1164 are stale); the racks row reads Done only when the count > 0 AND matches the token slots |
| Racks | 3 fixed slots mapped BY TOKEN (gpurack-1, gpurack-2, advancedgpurack) -> detail: K3 chips (+FULL), K4 readout from VM ClockGhz / Rate / Buffer / Yield, K5-K8 upgrade tracks with PLANNED rows and the inline confirm | Upgrade confirm -> rack.RequestPurchaseComputeTier RE:390-398; Mining switch -> RequestSetMining RE:310 (new here, R-11) | Empty slot hints kept (HP:370-390); NextTierLabel explicit; no TH/s |
| Terminal | K9 Link / Access / Power readout; K10 six security tracks PLANNED | Link / Unlink -> HE:551-553 | Read-only page about the linked HASHD terminal entity; it opens nothing (R-15). The cosmetic rows carry no economy reference. |
| Wallet | K11, K12: hub wallet, pending, terminal linked, USD value, exchange rate, your cash; amount input + preview + Cash out + Cash out all | RequestCashOutHub HE:440; RequestCashOutAllHub HE:442; Deposit all -> RequestDepositRacksToHub HE:436 (new here, R-11) | Preview labelled per R-14; unparseable input shows feedback (today HP:2942-2943 is silent) |
| Transfers | K13-K17: receive id + copy, hub wallet, send grammar + copy example, live rate table, still on racks | Clipboard only | Send stays terminal-only (DIRECTION:9, R-11) |
| Logs | K18 feed rows (kind icon, message with [B] / $ split, relative time) + empty state | Opening marks read HE:812; Clear (in Settings) HE:814 | Relative time is UNVERIFIED across host/client clocks (HP:1900-1910) |
| Settings | K19-K23: hub power (read-only mirror pointing at the header switch, R-5), Change PIN, terminal link toggle, notifications n + Clear, recipe line | Change PIN -> HE:433-434; link -> HE:551-553; Clear -> HE:814 | Recipe copy updated to the new tab names |

PIN keypad moved into the shell (W5). The PIN view replaces the tab host inside the same window: header and footer stay, the tab strip is hidden until unlock, close works. Keys 1-9 / clr / 0 / ok are flat lp-hub buttons at $hit. Dots show n of 4 (LpBitcoinHubPin.cs:16-17), with an error line from NotifyPinResult (HE:1268-1291). Modes and rails are unchanged: unlock HE:431, set HE:429, change HE:433-434, lockout HE:961-987. The clr semantics and unlock lifetime come from R-6. Visual style follows R-6.

### 6.2 HASHD terminal (style stays; W6 fixes in DRIVE-BTC-2)

Keep everything: the command list, STATUS column, boot with click-to-skip, rig@hub> prompt and its own PIN keypad (K24-K28). The palette is untouched (TPS:8-16). Fix:
- (a) refusal feedback to the caller for not linked, hub off and out of range, instead of host Log.Warning (TE:347-357);
- (b) the reach rule (R-12);
- (c) the per-tick hands_interact re-add (R-12; the core file is gated, G7);
- (d) the copy acknowledgement next to the row, not replacing its text (TP:74-76);
- (e) paint the prompt and static help on the first frame; the boot overlay must not blank the log or cover the sidebar (TP:323-357, TPS:236-253), and the PIN-first order stays.

Optional by R-12: hold success text until the host confirms (TC:272).

### 6.3 Player Hub $BTC tab

- Keeps: K29-K35 (power, wallet, pending, Deposit all, Cash out all, rack cards, pills).
- Gains:
  - VM-driven figures at the ruled precision;
  - the [B] sign on the rate (Law 17);
  - a FULL chip on full racks;
  - the Deposit all disabled reason "Link a terminal to deposit" (host rule HE:1147);
  - "STOCK" instead of "TIER STOCK";
  - a "2 hubs owned" chip when more than one hub is owned (R-13);
  - the link-out line (R-7);
  - cash-out result feedback, if R-11 opens the receiver edit.
- Loses: the CLOCK "x 1" suffix (R-2) and the string-compare enable logic (PB:123-124), replaced by VM floats.
- Nothing is added that needs hub proximity (DIRECTION:22).

## 7. Money rails (existing only)

| Rail | Exemplar (path:line) | What it moves | Guards |
|---|---|---|---|
| SetPowered | HE:321 -> SetPoweredHost HE:337-352 | No value. Flips IsPowered; power-off stops racks (HE:345-349). | CanManageHub(Rpc.CallerId) HE:340; refusal is silent |
| RequestSetMining | RE:310 -> SetMiningHost RE:312-333 | No value directly. Enables the host tick that mints into the rack buffer, capped. | linked hub, IsPowered, CanOperateTerminal (RE:316); refused when full (RE:324-325) |
| RequestDepositRacksToHub / RequestDepositRack | HE:436 -> HE:1141-1160 / HE:438 -> HE:1123-1139 | BTC rack buffer -> HubWalletBtc (+=), then ClearBalanceHost (RE:380-385). No ledger. Await-free. | CanManageHub HE:1144; HasLinkedTerminal HE:1147; no power check |
| RequestCashOutHub / RequestCashOutAllHub | HE:440 / HE:442 -> CashOutHubHost HE:1162-1199 | BTC wallet -> DXRP bank $ (whole dollars, uint HE:1255) via TryPayBank inBank: true (WA:42-51). Ledger reason "LIFEPUNCH hub BTC cashout" (HE:1190). Debit before await (HE:1183), additive restore (HE:1192), audit (HE:1196). | CanManageHub HE:1166; 0 < amount <= wallet HE:1169; FinalUsd > 0 HE:1173. The "all" amount is read on the client (HE:442) and re-checked by the host |
| Terminal send | TC:246-273 -> RequestSendHubWallet HE:444 -> SendHubWalletHost HE:1214-1252 | BTC wallet -> another owner's first hub wallet. No ledger. HubTransfer alerts on both sides (HE:1245-1248). | client parse SR:35-78; host EvaluateHost (HE:1218-1225), fail-closed apply (HE:1231) |
| PurchaseComputeTier | RE:390-398 -> PF:60-122 | BTC wallet -> burned, with ledger TryCommitPurchase (PF:108-110). Snapshot restore in an await-free segment (PF:104-112). | CanManageHub && Owner != 0 (PF:64); host-resolved price (PF:80); wallet >= cost (PF:89) |

Non-value rails the UI also uses: link / unlink terminal (HE:551-553), link / unlink rack (HE:521-523, terminal only), PIN (HE:429-434, 1028), alerts (HE:812-814).

Authority surface: every rail is [Rpc.Host] and resolves the caller from Rpc.CallerId. The only client-supplied identity is the send recipient, which the host re-resolves (HE:1217). The only client-supplied amounts are cash-out and send, both re-checked on the host. Price is never client-supplied. Two existing authority facts are recorded, not changed: CanManageHub is true for anyone when no PIN is set and Owner is 0 (HE:1361-1362), and always true under LIFEPUNCH_LOCAL (HE:1358-1359).

## 8. Non-goals

- No new RPC, field or code path that moves, mints, prices or destroys value. No change to any rail body, the ledger, the payout math (EC:80-85) or the donor policy.
- No host-side PIN session or other new authority contract (R-16 records the question).
- No HASHD terminal restyle: the palette, CRT shell and command grammar stay.
- No edit under lifepunchdxrp/ (upstream) or lifepunchulx/. No change to the four Player Hub fixture tabs or the $LP Store. No $LP spend.
- No TH/s or other synthetic unit. No new player ConCmd or chat command. No remote opening of the hub UI from the Player Hub.
- No portal API. No grid, @media or JS. No send control in the hub UI unless R-11 rules otherwise.
- No edit to LpMenuShell.razor: it is retired from the hub app only, and its other consumers are UNVERIFIED.

## 9. Target files

### DRIVE-BTC-UI-2 (hub UI build)

| Class | Path | Note |
|---|---|---|
| Create now | bitcoinmining/LpBitcoinHubState.cs | ONE-STATE builder + hash (section 5) |
| Create now | playerhub/code/ui/LpPlayerHubShell.scss | Shared shell partial, rules copied from PRS (R-10 option B). The Player Hub is not switched to it until UI-3. |
| Modify after GO | lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor | Rebuilt in the house shell, keeping the type name (R-9 option A) |
| Modify after GO | lpbitcoin/bitcoinhub/code/ui/LpHashdPanel.razor.scss | Replaced by a token-only sheet importing TK, PM and the shell partial |
| Modify after GO | lpbitcoin/bitcoinhub/code/components/LpBitcoinDevSpawn.cs | Only the preview ConCmd targets (DS:904, 960, 998, 1017, 1036), so they deep-link the new tabs |
| Create only when ruled | lpbitcoin/bitcoinhub/code/ui/LpBtcHubRoot.razor (+ .scss) | Only under R-9 option B |
| Create only when ruled | HE:1254-1263, HE:1314-1329 receiver retarget | Only under R-9 option B or R-11 Player Hub feedback; a money file, needs his word |
| FORBIDDEN | HE rail bodies (HE:337-352, 1123-1252), PF, WA, EC payout math, LpBitcoinDonorPayout.cs, the ledger, MS, TP/TPS, lifepunchdxrp/, lifepunchulx/ | - |

### DRIVE-BTC-2 (terminal fix)

| Class | Path | Note |
|---|---|---|
| Modify after GO | bitcoinmining/LpBitcoinTerminalPanel.razor (+ .scss layout rules only) | Copy acknowledgement, first paint, overlay scope; palette TPS:8-16 untouched |
| Modify after GO | bitcoinmining/LpBitcoinTerminalEntity.cs | Press / open refusal feedback (TE:321-360), reach |
| Create only when ruled | a caller-filtered refusal broadcast on TE (no value) | R-12 |
| Create only when ruled | lifepunchcore/LifePunchPropPhysics.cs, LifePunchMenuInteractGate.cs | Shared core, G7 |
| FORBIDDEN | LpOpsCrtTerminal.scss, TC grammar, the CRT palette, every money rail | - |

### DRIVE-BTC-UI-3 (Player Hub integration)

| Class | Path | Note |
|---|---|---|
| Modify after GO | playerhub/code/data/LpPlayerHubBtcSource.cs | Reads LpBitcoinHubState |
| Modify after GO | playerhub/code/ui/Btc/LpPlayerHubBtc.razor (+ .scss) | Gains / loses per 6.3 |
| Modify after GO | playerhub/code/data/LpPlayerHubModels.cs | LpBtcVm fields |
| Modify after GO | playerhub/code/ui/LpPlayerHubRoot.razor.scss | Import the shell partial, drop the duplicate rules |
| FORBIDDEN | The fixture tabs (Overview, Store, Skills, Stats), $LP, lifepunchulx/, every money rail | - |

## 10. Acceptance, gates and the execution gate

### Hard integration gates (each BLOCKS the named slice)

- G1 - all build slices: Phase A is complete and the literal "go <slice-id>" has been given (DIRECTION:2).
- G2 - UI-2: R-BTCUI-1, -2, -3 and -4 are ruled before any format constant is written.
- G3 - UI-2: R-BTCUI-9 is ruled before the first edit. Option B needs an HE edit, which needs his explicit word.
- G4 - UI-2 acceptance: his 17 screenshots are pinned under C:/cavelux/telemetry/sbox/proof/btc-ui-ref/, or he waives the comparison.
- G5 - all: a slice that finds it needs a rail-body, ledger, payout or authority change STOPS and files a ruling.
- G6 - UI-3: R-BTCUI-13 (hub resolution) is ruled.
- G7 - BTC-2: the owner contract for the lifepunchcore files is in place before they are edited.
- G8 - all: the live tree is re-sensed (kit-sync -WhatIf) before every sync. The live playerhub files are uncommitted LF copies on d9c08031.

### Execution gate (every build slice)

razor_lint on each new or changed razor; kit-sync -WhatIf first; a positive code-string ID in a Log.Info whose sbox-dev.log line postdates the sync (share-mode read); captures read back by the seat; ui_panel_dump structural assertions; his click for every interactive flow, because synthetic clicks do not reach s&box panels. The razor-ui pre-flight runs: hex/radius diff grep empty, no $var-<digit>, no compound selectors, PROPRIETARY header, PANE-BUDGET lines.

### DRIVE-BTC-UI-2

- [ ] Positive ID LP_BTCHUB_SHELL_<yyyymmdd> greps to exactly one definition, and its log line postdates the sync.
- [ ] Captures: PIN view in unlock, set and change modes; each of the 7 tabs; rack detail with an open confirm chip. Each PNG is read by the seat.
- [ ] Panel dump per tab: 7 tab nodes, exactly 1 tab-sel, no LpMenuShell node, no Poppins font, header pills present.
- [ ] One figure, one reading: wallet, pending, rack Btc, rate and clock show the same text on the hub UI and the Player Hub for one hub (dump diff).
- [ ] Racks map by token: rebinding gpurack-2 moves the label with the rack (dump before and after).
- [ ] Transaction cases (his click): cash out all with wallet 0 disabled; amount > wallet -> "Insufficient funds"; unparseable amount -> visible feedback; deposit with no terminal disabled with its reason; purchase short -> SHORT chip; wrong PIN x5 -> lockout message.
- [ ] Zero raw hex, radius or spacing in the new sheet (grep). Cosmetic rows carry no YieldMultiplier or ComputeTier reference.

### DRIVE-BTC-2

- [ ] Positive ID LP_BTC_TERMINAL_W6_<yyyymmdd>, log-fresh.
- [ ] Pressing an unlinked terminal or opening with the hub off shows the player a message (capture), and no host-only Log.Warning remains as the sole signal.
- [ ] The first frame shows the prompt and help (capture at open); the boot overlay leaves the sidebar visible; the PIN-first order holds.
- [ ] A copy click shows its acknowledgement beside the row while the row text stays (capture).
- [ ] With Hands equipped, E opens the terminal (his click). The hands_interact tag is stable across 100 ticks (log sensor), if R-12 rules the core edit.
- [ ] TPS:8-16 is byte-identical to b3d3b525 (diff).

### DRIVE-BTC-UI-3

- [ ] Positive ID LP_PLAYERHUB_BTC_UI3_<yyyymmdd>, log-fresh.
- [ ] Capture and dump of the $BTC tab: [B] on the rate, STOCK label, FULL chip, Deposit reason, link-out line.
- [ ] An exchange-rate change (lp_bitcoin_portal_rate, his word) repaints the Player Hub $ figures without any other input moving.
- [ ] The Player Hub shell imports the shared partial with no visual delta (capture diff of the Overview tab).

## 11. QoL

- **Discover.** The hub UI opens only by pressing the hub. The Overview checklist teaches the order. The Player Hub link-out (R-7) tells the player where the rest lives. The Settings recipe line and the per-tab blurbs (PR:53 pattern) explain each tab. The terminal is discovered from the Terminal tab readout.
- **Esc closes every layer.** VERIFIED ABSENCE: no Input.EscapePressed handler exists in playerhub, lpbitcoin or bitcoinmining. Positive controls from the same grep: lplockpick/LockpickMinigame.razor:167 and lptags/tags/code/ui/LpTagsMenu.razor:314-317. The terminal's "escape" only clears the log selection (bitcoinmining/LpBitcoinTerminalLogPanel.cs:100). Rule: Esc closes the innermost layer first (confirm chip -> PIN change view -> rack detail -> panel), consuming the press with the LpTagsMenu pattern. Same in the terminal and the Player Hub.
- **Bad input - PIN.** Digits only, 4 maximum (LpBitcoinHubPin.cs:16-17). Wrong-PIN and lockout text comes from the host (HE:969-984). clr per R-6.
- **Bad input - send grammar.** Keep the parser messages (TC:249-265; SR:49, 56, 65, 72). Flag: SR:63 parses with the current culture, while the hub cash-out parses invariant (HP:2937). R-12 decides whether DRIVE-BTC-2 aligns them.
- **Bad input - amounts.** An unparseable amount gets an inline message such as "Enter a BTC amount like 0.0025" (today HP:2942-2943 returns silently). An amount over the wallet gets "Insufficient funds".
- **Help text.** Tab blurbs, empty states ("No alerts yet.", no rack in a slot), the terminal help (TC:404-410).
- **Conflicts.**
  - The Player Hub owns ConCmd "hub" and chat alias "/hub" (PHH:55; PCC:29). Other names are taken too: "serverhub" (serverhub/code/ui/LpServerHubHost.cs:65), "menu" and "ulx" (lifepunchulx/StaffMenuHost.cs:598, 601), "tags".
  - The new UI registers no ConCmd and no chat command. The dev previews keep the lp_bitcoin_preview_* names (DS:904-1036).
  - BU:15-20 does not close the Player Hub. Opening the hub UI should close the Player Hub (PHH:103), and both must call the menu input block the way PR:251-259 does.
  - E-press competition with Hands is W6.

## 12. Rulings the principal owes

| ID | Question | Options | Recommendation |
|---|---|---|---|
| R-BTCUI-1 | BTC display precision | (a) F6 for every BTC figure; (b) F6 for balances, pending, buffers and rates, with ladder prices trimmed ("0.######", so 0.25); (c) F4; (d) 8 dp | (b). It matches the host alerts (HE:1246), the terminal (TC:215-217) and the Player Hub (BS:29); a stock rate of 0.008133 needs 6 dp. |
| R-BTCUI-2 | Rate and clock units | rate: [B]/min, [B]/tick (90 s), or TH/s with a named constant; clock: GHz F2 or 0.0; core suffix kept or dropped | [B]/min at F6 with the sign, GHz F2, drop "x 1" (cores are locked, CT:143), delete TH/s (bare 18.5f, HP:2155) |
| R-BTCUI-3 | Wallet $ and pending | (a) wallet only; (b) wallet + pending under a "Total" label; (c) both shown separately | (c), plus USD N0 everywhere, because the bank pays whole dollars (uint, HE:1255). Never a combined figure under "Hub wallet". |
| R-BTCUI-4 | Rack naming and identity | (a) token-first everywhere (ID:124-136, 293-309); (b) positional | (a). Action target = GameObject.Id; label and token from ID; "STOCK" at tier 0; next tier labelled "Next". |
| R-BTCUI-5 | Hub power single source | (a) the header switch is the only writer in the hub UI, the Settings row is a read-only mirror, the Player Hub switch stays as the quick action; (b) every mirror stays a writer | (a). Add caller-visible feedback when SetPoweredHost refuses (HE:340-341); that is a non-value client notice. |
| R-BTCUI-6 | PIN keypad in the shell | style: flat house buttons or a keypad tile look; clr: backspace one digit or clear all; unlock lifetime: per open or per session | Flat house buttons at $hit, backspace one digit (matches the icon HP:68 and the terminal TP:476), per open (the host keeps no session anyway) |
| R-BTCUI-7 | Player Hub link-out | (a) a hint line "Open at your Bitcoin Hub for racks, upgrades, transfers and logs"; (b) a remote-open button (new host RPC and authority surface) | (a). (b) breaks DIRECTION:22 and needs a contract. |
| R-BTCUI-8 | Brand and accent | lifepunch.btc wordmark with $lp-blue chrome (tab underline), or $btc orange chrome | lifepunch.btc + $lp-blue chrome; $btc only on the [B] sign (Law 17, chrome/status separation) |
| R-BTCUI-9 | Build seam | (a) rebuild LpHashdPanel in place, keeping the type, so envelopes (HE:1260, 1325) and UH keep working with no entity edit; (b) a new component with retargeted receivers | (a) |
| R-BTCUI-10 | Shell sharing and token gaps | (a) copy the shell rules; (b) a new shared partial, adopted by the Player Hub in UI-3; also tokenise PRS:183-199 and PM:52-53, and name tokens for keypad dot, fill bar and stepper sizes | (b), plus a token ruling before UI-2 adds any new fixed size |
| R-BTCUI-11 | New controls on existing rails in the hub UI; deposit preconditions; Player Hub feedback | add Deposit all (HE:436) and the Mining switch (RE:310) to the hub UI or not; the deposit enable rule follows the host (terminal linked) or the Player Hub (power); send stays terminal-only or gets a form; Player Hub cash-out feedback via a receiver edit | Add both controls; the enable rule mirrors the host exactly with a visible reason; send stays terminal-only; Player Hub feedback deferred until he approves the receiver edit |
| R-BTCUI-12 | Terminal W6 scope | refusal channel (caller-filtered broadcast vs screen line), reach (bounds like the hub vs pivot), grab gate in the shared core, optimistic success text, culture parse | Caller broadcast; bounds reach; the core edit under an owner contract (G7); hold success text until the host confirms; invariant parse |
| R-BTCUI-13 | Which hub the Player Hub shows | (a) first owned (today, BS:31-35); (b) last opened; (c) first owned plus a "n hubs owned" chip | (c) |
| R-BTCUI-14 | Cash-out preview vs paid $ | the preview uses the display rate without the donor multiplier (EC:77 vs EC:80-85): label it "est." and show the paid figure from NotifyCashOutSuccess, or hide the preview | Label "est. before bonuses" and show the paid $ after the host confirms |
| R-BTCUI-15 | Names | the tab names "Terminal" (for the HASHD terminal's status, razor-ui naming law) and "Racks" (formerly Servers) | Confirm both as DIRECTION:23 words them |
| R-BTCUI-16 | What the PIN means | the PIN gates the hub UI view only; the Player Hub quick actions drive the same rails without it (HE:1356-1366) | Accept and record it. A host PIN session is a new authority contract, outside these slices. |

## 13. Evidence notes

- **Own frame.** C:/cavelux/telemetry/sbox/proof/DRIVE-BTC-UI-1_1.png, 910687 B / 60C7CF7D7728D738FE0B6AE9757DC13405D2E35E628D064081D3693F2EBC7151 (re-hashed: MATCH), 1280x720, file time 2026-10-17T13:46:24Z. It was opened by lp_bitcoin_preview_hub, which bypasses the PIN (DS:917) and reuses the first non-preview hub in the scene (BU:47-51).
  - The frame shows the BITCOIN OPS app over the world scene. Left sidebar: avatar, DASHBOARD (selected), SERVERS, WALLET, TRANSFERS, SYSTEM / LOGS, with SETTINGS at the foot. Header: orange wordmark, power switch OFF, wallet chip [B]0.0000, "Cash out all", close.
  - Body: a callout naming "Upgrades". GETTING STARTED shows Power on hub REQUIRED; Link terminal REQUIRED ("Hub power off - turn on first", disabled "No terminal in range"); Link GPU racks DONE with an orange SERVERS button. The footnote "Complete all three steps to start mining." renders across the card's bottom edge.
  - SNAPSHOT shows SETUP In progress, LINKED RACKS 3 / 3, and HUB WALLET [B]0.0000 with "$0.00 estimated value". The footnote "Deeper details: ... Upgrades tab for track tiers" is clipped at the card edge.
  - Quick buttons: UPGRADES (orange fill), Wallet, Transfers. Footer: "LIFEPUNCH Bitcoin Ops" / "proprietary IP" / "lifepunch.co".
  - What it proves: W1's orange sidebar shell, the F4 wallet, the stale Upgrades copy, and "racks DONE while the hub is off with no terminal" (HP:1109). It also shows two text-overflow defects that the W list does not name.
  - It shows no other tab, no Player Hub and no terminal.
- **UNVERIFIED-BY-FRAME rule.** Every other figure in this spec attributed to his 17 screenshots (0.500200, $2,500.00, 78.1 GHz, Tier I and so on) is UNVERIFIED-BY-FRAME until they are saved under proof/btc-ui-ref/ (DIRECTION:35).
- **Live vs worktree** (as reported by the M1 inventory, not re-sensed by this draft). 70 of 76 addon files are byte-identical. The 6 playerhub files (BS, PB, PBS, PR, PRS, TK) differ in line endings only: CRLF in the worktree, LF live, with equal line counts, so line numbers hold. The live HEAD is d9c08031 (codex/ulx-polish-20260914), and the live playerhub files are uncommitted there. One archived asset differs (bitcoinhub/_archive/phase2-fan-spin/bitcoinhub-fan.vmdl); it is not cited.
- **Cites re-opened by this draft at b3d3b525.** TK, PM, PR and BS whole; PB whole; PRS:1-30 and 179-213; HE:35-58, 296-358, 426-466, 519-558, 788-796, 810-816, 955-988, 1026-1034, 1123-1263, 1300-1374; RE:30-62, 306-398; EC:15-124; HP:14-30, 60-128, 608-640, 756-799, 1074-1166, 1352-1382, 1453-1489, 1780-1783, 1900-1910, 1980-2022, 2150-2161, 2339-2346, 2430-2436, 2494-2530, 2869-2985; TP:70-150, 212-232, 323-400, 474-478, 854-873; TE:28-31, 160-179, 319-360; TC:19-30, 208-218, 246-273, 400-410; ID:124-140, 160-169, 293-309; SR:35-78; WA:40-60; CT:126-157; TS:33-47; PF:60-122; BU:10-63; UH:26-84; DS:900-921; PHH:28-104; PCC:10-40; FT:15-40; HPS:38-46; PBS:1-20. Every other cite was carried from the M1 inventory and is for the verifier to re-open.
- **Canon location.** LIFEPUNCH_UI_STANDARD.md was not found under docs/ or game/Code/Addons/lifepunch/docs/ in this tree. This spec takes tokens from TK, as the dispatch directs.
- **Closing law (verbatim):** absence of a contract is a blocker, not permission to invent local substitutes.

## 14. Verification

Adversarial citation check by two read-only verifiers (workflow wf_46c1eccc-a3f) at b3d3b525 in this worktree:

- Verifier A re-opened every distinct citation behind W1-W7, the money rails, rack identity and the rate math: 311 of 311 (346 raw, de-duplicated). Result: 309 CONFIRMED, 2 REFUTED, 0 UNREADABLE. The two refuted cites do not appear in this spec: LpBitcoinTerminalEntity.cs:265-266 is the hub-to-terminal link-range helper, not the press/open reach check; LpBitcoinTerminalPanel.razor.scss:129-130 is off by one (the cited comment is at :131).
- Verifier B re-opened a stratified sample of the readouts: 60 of 144 (20 per UI - hub app, HASHD terminal, Player Hub tab - every Nth row by position), value and format cites. Result: 60 CONFIRMED, 0 REFUTED.
- Every other path:line in this spec is carried from the read-only inventory (workflow wf_3c6f4ef3-7fa) and was not independently re-opened. Treat it as UNVERIFIED until the build slice re-greps it (section 0, Revalidate).
- Frames: one own capture, C:/cavelux/telemetry/sbox/proof/DRIVE-BTC-UI-1_1.png 910687 B / 60C7CF7D7728D738FE0B6AE9757DC13405D2E35E628D064081D3693F2EBC7151 (lp_bitcoin_preview_hub in Play). The principal's 17 screenshots are not on disk; every figure attributed to them is UNVERIFIED-BY-FRAME.
