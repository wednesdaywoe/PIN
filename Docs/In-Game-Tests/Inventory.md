---
project: pin
kind: test-stream
title: "Inventory Delivery (I1-I2)"
relates:
  - ../TEST-REGISTER.md
---

# Inventory Delivery

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

[CharacterInventory](../../UdpHosts/GameServer/Data/CharacterInventory.cs) holds the items and sends
`InventoryUpdate` — once in full at spawn, then one partial message per change. `createitem` and
`dbg_inventory` are in
[Systems/Admin/Commands](../../UdpHosts/GameServer/Systems/Admin/Commands).

## Why this file exists

`createitem 20003` was reported on 2026-08-11 as showing the "Dev Shotgun" pickup toast and then
putting nothing in the inventory. The toast is the giveaway: it comes from `SimulateLootPickup`,
which the command sends unconditionally, so seeing it only proves the type id resolved. Nothing
about it says the item was delivered.

That gates every entry from P0 down, all of which have to slot a module that doesn't exist yet. The
R series in [Damage Decay](Damage-Decay.md) got out from under it a different way — the Biotech
frame's default weapon is a shotgun that decays, so R1 and R2 were answered on 2026-08-11 without
conjuring anything. Worth remembering as the first thing to try when `createitem` blocks an entry:
some frame already owns a weapon with the property you're after.

**What the 2016 capture says.** Diffing PIN's message against a real one — 200 `InventoryUpdate`
messages, `--controller 2 --message 129`, see [Capture Replay](Capture-Replay.md) — turns up two
divergences and rules out several other suspects:

| Field | PIN sent | Live server sent |
|-------|----------|------------------|
| `Item.Unk5` | 0 | **1**, on all 211 items sampled, every type, equipped or not |
| `InventoryUpdate.Unk` | 1 always | **0** on partial updates, 1 on the full one |

`Unk5` is the interesting one: it is the last scalar before a count-prefixed array, it never varies
in the recording, and 0 is what an empty stack would look like — which is exactly the shape of a
bug where the client accepts a message and lists nothing. Both are now fixed to match.

Ruled out along the way, so nobody re-checks them:

- **Sub-inventory mapping.** Retail put a looted Consumable in 2/Cache, weapons and modules in
  4/Gear, TinkerTools in 1/Bag. PIN's `GetInventoryTypeByItemType` agrees on every one of them.
- **`DynamicFlags` and `Durability`.** PIN writes 0 and 1000; the live inventory holds items at 0,
  1, 7 and 0, 827, 1000 respectively. Neither can be what hides an item.
- **GUID shape.** Retail item guids end in type byte `0xFD`; `GuidService.AdditionalTypes.Item` is
  `0xFD` and PIN's come out the same way.
- **Item flags on 20003.** It carries only `0x800`, which reads like "deprecated", but 0x800 is
  also set on ordinary weapons sitting in the live capture's inventory, so it isn't a hidden bit.
- **`EnablePartialUpdates`.** Set true right after the full send in `NetworkPlayer.Respawn`, which
  runs on the first `ScheduleUpdateRequest` at zone-in — long before you can type a command.

Message order was also wrong and is now corrected: retail sent `SimulateLootPickup` first (seq
55934) and the `InventoryUpdate` two sequence numbers later, PIN had them the other way round.

## [ ] I1: `createitem` puts a visible item in the inventory

Blocks P0–P6. Run it before anything that spawns an item.

1. Log in, open the inventory before creating anything, and note whether the frame's default gear
   is listed. This is the control — if the starting inventory is empty too, the problem was never
   about `createitem`.
2. `createitem 20003`
3. `dbg_inventory`
4. Look for the Dev Shotgun in the inventory and in the garage's weapon slot picker — weapons land
   in the Gear sub-inventory, which is where the loadout screen reads from, so check both.
5. If it still isn't there: `dbg_inventory resend`, which pushes the same full `InventoryUpdate`
   the client already accepted once at spawn, and look again.

Read the console output from step 3 alongside what you see:

- Pass: the item shows, and `dbg_inventory` lists it as
  `20003 guid <hex> Weapon in Gear flags 0`.
- **Listed by `dbg_inventory` but not shown, and `resend` makes it appear** — the item struct is
  right and delivery is right, but the client will not merge a partial `InventoryUpdate`. Point
  `CreateItem` at `SendFullInventory` and move on; the tests are unblocked either way. Worth a note
  here, because it would mean `SendEquipmentChanges` and `SendResourceUpdate` are dead letters too.
- **Listed by `dbg_inventory` but not shown, and `resend` doesn't help either** — the client is
  rejecting or hiding the item itself, and the remaining suspects are all in the item struct.
  Bring back the `dbg_inventory` line and the client log
  ([Client Logging](Client-Logging.md)); the next comparison to make is against the retail item in
  capture message [9], which is a 37-byte single-item add reproduced in full in
  [Capture Replay](Capture-Replay.md).
- **Not listed by `dbg_inventory`** — nothing was created, and the grep below says why:

```
grep -a "createitem" ~/Games/PIN/logs/GameServer.log | tail -5
```

## [ ] I2: Equipping still round-trips

A regression check, not a feature check. `SendEquipmentChanges` sends the same partial
`InventoryUpdate` and its `Unk` changed from 1 to 0 with everything else, so it wants one look.

1. In the garage, unequip a weapon and equip another
2. `dbg_inventory`

Pass: the swap holds in the client, and `dbg_inventory` shows the new item carrying the `IsEquipped`
flag and the old one without it.
