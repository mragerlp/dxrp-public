# Dimmer review checklist — lifepunchulx style parity

Use this when Bloodwave shares screenshots or asks whether lifepunchulx **looks native** beside Party Menu.  
**No DXRP code change is required** unless Party Menu tokens themselves need adjustment.

---

## Visual consistency (Party Menu lens)

- [ ] Panel background reads as **flat #191919 / #111111** stack — not blue-tinted dashboard
- [ ] Border weight **≤ 0.5px** subtle rgba white — not heavy 1px chrome rings
- [ ] Corner radius **~2px** on panels/buttons — not 22px shell / 9px pills (unless owner explicitly keeps large console)
- [ ] Selected nav/tab uses **opacity 1 + ~8% accent wash** — not solid fill or thick colored ring
- [ ] Player roster selected row is **obvious but quiet** — not full-width solid accent block
- [ ] Primary actions use **compact button** padding (~7×13) — not 90px action tiles (unless owner keeps tile grid)
- [ ] Typography: uppercase small title bar OR equivalent simple hierarchy — not badge + glow + dual title stack
- [ ] No gradient backgrounds on open (console clean)
- [ ] Material icons render as glyphs — not ligature text

## Behavior (unchanged — spot check if testing)

- [ ] `/party` menu still independent — lifepunchulx changes did not touch PartyMenu files
- [ ] lifepunchulx opens from existing staff aliases
- [ ] Kick/freeze/waypoints/settings still execute

## Paths referenced

| Reference | Path |
|-----------|------|
| Party Menu | `game/Code/UI/HUD/Components/PartyMenu.razor.scss` |
| TabMenu | `game/Code/UI/Menus/TabMenu/TabMenu.razor.scss` |
| This workspace | `dxrp-staffmenu/` |

## Verdict template

```text
VISUAL PARITY: PASS | REVISE | HOLD
Notes: …
Party Menu alignment: …
Suggested upstream change (if any): none expected — implementation is monorepo lifepunchulx
```
