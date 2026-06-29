# Feature request: Consumable entity shipments

**Status:** Filed on upstream (Bloodwave, 2026-06-29)  
**Upstream issue:** https://github.com/dxura/dxrp/issues/92  
**Target branch (implementation):** `develop`  
**Lane:** DXRP platform — not LifePunch-specific; LifePunch benefits when merged.

**Discord context:** Bloodwave + PikPak — bulk packs for **consumable** entities only; **no pocket changes**; placeables (printers, radios, TVs) stay single-spawn.

---

## Repo evidence (dxrp-public)

| Area | Finding |
|------|---------|
| Equipment + `Quantity > 1` | `GameManager.PurchaseMarketItemHost` → `ShipmentEntity` |
| Entity purchase | Always one prefab; `Quantity` ignored |
| `ShipmentEntity` | `ConfigureHost(GameModeEquipmentDto, int)` — equipment + `DroppedEquipment` only |
| Wire automation | `ShipmentEntity` implements `IWireUsable` |
| Pocket | `PocketSystem` — separate; **out of scope** |
| Consumable examples | Pizza/joint: `BaseEntity` + `pocket_item`; destroy-on-use patterns |

**Key files:**
- `game/Code/GameManager.cs` — `PurchaseMarketItemHost`
- `game/Code/Entity/Entities/ShipmentEntity.cs`
- `game/Code/Utilities/Resource/GameModeMarketItems.cs`
- `game/Code/Api/Enums/GameModeMarketItemType.cs`
- `game/Code/Api/Dtos/GameModeMarketItemDto.cs`

---

## Issue title (as filed)

```text
feat(market): consumable entity shipments (bulk packs for use-and-destroy entities)
```

---

## Issue body (GitHub paste — canonical)

## Summary

Extend the market/shipment system so **consumable gamemode entities** (use-once props: armor vests, food, joints, etc.) can be sold and dispensed as **bulk packs** when `Quantity > 1` — similar to how **Equipment** shipments already work for Gun Dealer weapon crates.

**Non-consumable entities** (money printers, radios, TVs, permanent placeables) must **remain single-spawn only**.

**No pocket system changes are required or requested.**

---

## Background

DXRP market items have two types (`GameModeMarketItemType`):

| Type | `Quantity > 1` today |
|------|----------------------|
| **Equipment** | Spawns `ShipmentEntity` (`gameplay/entities/shipment/shipment.prefab`); unpack creates `DroppedEquipment`; supports `IWireUsable` for wire automation |
| **Entity** | **Always spawns one prefab** — `Quantity` on the DTO is ignored at purchase time |

Relevant code paths:
- `game/Code/GameManager.cs` — `PurchaseMarketItemHost`
- `game/Code/Entity/Entities/ShipmentEntity.cs` — equipment-only `ConfigureHost(GameModeEquipmentDto, int)`
- `game/Code/Utilities/Resource/GameModeMarketItems.cs`

Equipment bulk packs work end-to-end. Entity bulk packs do not — even for entities that behave like consumables (press/use → destroy, e.g. pizza with `StatusOnPress`, pocket-tagged props like joint).

---

## Problem

Server owners cannot:

- Sell **bulk consumable entity packs** from the market (e.g. 5× kevlar / 5× food per purchase)
- Use **wire automation** to passively dispense those packs (equipment shipments already support wire via `IWireUsable`)
- Rely on portal `Quantity` for Entity market rows when the referenced content is a consumable

Example raised by contributors: **kevlar** configured as a gamemode **Entity** (not Equipment) cannot be sold as a 5-pack shipment, while weapon Equipment shipments work today.

---

## Proposed scope

### In scope

- **Consumable entities only** — entities intended to be used once and destroyed (armor pickups, food, joints, similar props)
- When an Entity market row is **shipment-eligible** and `Quantity > 1`:
  - Spawn a bulk pack (reuse or extend `ShipmentEntity`, or parallel crate type)
  - Unpack grants **N instances** of the consumable entity prefab (not `DroppedEquipment`)
  - **Wire parity** — wire can trigger unpack like equipment shipments
- Portal/config way to mark which entities **may** use shipment quantity (see open design below)

### Explicitly out of scope

- **Pocket system** changes (`PocketSystem`, `pocket_item` carry rules)
- Bulk shipments for **non-consumable / permanent placeables** (printers, radios, TVs, machines, etc.)
- Changes to existing **Equipment** shipment behavior (Gun Dealer 5-packs must not regress)
- Inventory API item stacking (`ItemEntity` / `IsStackable`) — separate system

### Examples

| Content | Shipment bulk pack? |
|---------|---------------------|
| Kevlar / armor vest entity | Yes (consumable) |
| Pizza / joint / food props | Yes (consumable) |
| Money printer | No (placeable) |
| Radio / TV | No (placeable) |

---

## Open design questions (maintainer)

1. **Eligibility flag** — How should DXRP distinguish consumable vs placeable at purchase time?
   - Portal flag on `GameModeEntity` / market row?
   - Prefab component or tag?
   - Content metadata from addon row?

2. **Crate reuse** — Extend `ShipmentEntity` to support entity prefabs, or separate `ConsumableShipmentEntity`?

3. **Limits** — How should `GameModeEntity.Limit` and owned-count checks apply when buying N at once?

---

## Suggested acceptance criteria

- [ ] Shipment-eligible **consumable** Entity market row with `Quantity > 1` produces a bulk pack, not a single spawn
- [ ] Unpack spawns N consumable entity instances (correct prefab, ownership, limits)
- [ ] **Non-eligible** Entity rows (printers, radios, etc.) ignore or clamp `Quantity` to 1
- [ ] Wire can trigger unpack on consumable entity bulk packs (`IWireUsable` or shared API)
- [ ] No regression to Equipment shipments
- [ ] Brief docs: Equipment shipment vs consumable entity shipment vs placeable entity (single spawn)

---

## Motivation

Standard RP flows: supply dealers, armor vendors, and **wire-driven passive shops** selling bulk consumables without N separate market clicks or entity spam. Equipment already has this; consumable entities are the gap.

---

## Notes

Discussed by DXRP contributors after confirming the Entity purchase path ignores `Quantity` and `ShipmentEntity` is Equipment-only. Quantity on `GameModeMarketItemDto` suggests prior intent; Entity consumable shipment is the missing implementation.

---

## LifePunch follow-up (when upstream lands)

- [x] Paste upstream issue URL above after filing — [#92](https://github.com/dxura/dxrp/issues/92)
- [ ] Branch from `develop`: e.g. `lifepunch/feat-consumable-entity-shipments` or Dimmer-assigned branch
- [ ] No LIFEPUNCH headers / proprietary paths in upstream PR
- [ ] Portal: mark kevlar (and other consumables) shipment-eligible + `Quantity` on market rows
- [ ] Smoke: market buy + wire unpack on dev server

**Contributor lane:** `lifepunch/docs/DXRP_CONTRIBUTOR_LANE.md`
