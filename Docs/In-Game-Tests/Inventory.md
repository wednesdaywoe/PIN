---
project: pin
kind: test-stream
title: "Inventory Delivery (I1-I3)"
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

**One suspect is now ruled out by evidence rather than by reading.**
[G1](Resource-Payout.md) passed on 2026-08-13: three `createitem 10 200` calls in a row moved a
crystite count from 0 to 600 in an inventory that was already open. Resources ride in a different
array of the same `InventoryUpdate` and go out through `SendResourceUpdate`, the sibling of the call
that sends items — so **the client does accept a partial update and does merge it into a UI on
screen**. I1's second bullet below, which would have had the client refusing partial merges
outright, cannot be what is happening. Whatever hides an item is in the item struct or the item
arrays.

**And a suspect nobody had looked at: there may be no room.** Every entry above asks whether the
item was built correctly and whether the message carried it. None asks whether the client had
anywhere to put it. PIN gives a character **every battleframe in the game at login** — 20 chassis,
each with its default modules in a PvE and a PvP configuration, built by
`GenerateCharCreateLoadoutAndItems` — which is not a quantity any retail character carried, and
Firefall's bag had a size. An item created into a full inventory would produce exactly what I1
describes: the type id resolves, the toast fires, the server lists it, and it is never drawn.

It also explains why G1 sails through while I1 doesn't. Resources are not bag items; they have their
own pool and their own array. A ceiling on the item side would leave the resource side untouched,
which is precisely the split that has been observed.

Nothing about this is confirmed — it is a hypothesis with a shape that fits, and **I3 is the entry
that measures it.** The item count is now written to the server log at every full send, which
nothing did before.

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

## [!] I1: `createitem` puts a visible item in the inventory

**Ran 2026-08-14 and landed on its own middle branch, confirmed twice.** The DevTEST Shotgun
(20003) did **not** appear until `dbg_inventory resend` pushed the full inventory; module 86074
repeated the same pattern later in the sitting. After the resend the item is entirely real: it sits
in the garage's weapon picker, was dragged into the Secondary slot by hand, and works —
`dbg_inventory 20003: guid 1F026F1900030DFD in Gear flags IsBound, IsEquipped, slotted in a loadout`
(guid type byte `FD`, as the ruled-out list predicted). So creation, the item struct, and the full
send are all right, and **the client declines to merge PIN's partial item update** — exactly the
outcome the second bullet below describes. Not fullness: I3 measured no ceiling, and resend helping
rules it out anyway. Resources merge fine ([G1](Resource-Payout.md)), so the defect is confined to
the item arrays of the partial message; the next comparison is capture message [9], the 37-byte
retail single-item add reproduced in [Capture Replay](Capture-Replay.md).
[NET-18](../ISSUE-REGISTER.md) re-scoped to exactly this and stays open.

**Workaround for every entry that spawns an item:** `createitem <id>`, then `dbg_inventory resend`,
then look in the **garage picker** — not the inventory window, whose search doesn't find the item
(searching "Dev" returns nothing; possibly the search matches localization entries a dev item
doesn't have) and whose 246-piece Gear seed makes finding one by eye impractical.

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
  right and delivery is right, but the client will not merge this particular partial
  `InventoryUpdate`. It cannot be partial merging as a mechanism: [G1](Resource-Payout.md) watched
  three of them land. So read it as the item arrays specifically, and run I3 before concluding
  anything, because a full inventory would also make `resend` fail to help.
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

## [x] I2: Equipping still round-trips

**Passed 2026-08-14, in a stronger form than written.** The swap wasn't between two seeded weapons
but onto the freshly created 20003: dragged into the Secondary slot in the garage, and
`dbg_inventory 20003` came back `flags IsBound, IsEquipped, slotted in a loadout` — the
client-initiated equip reached the server and was recorded. The displaced weapon's cleared flag
wasn't read separately; if a future session cares, that half-check is one `dbg_inventory` away.

A regression check, not a feature check. `SendEquipmentChanges` sends the same partial
`InventoryUpdate` and its `Unk` changed from 1 to 0 with everything else, so it wants one look.

1. In the garage, unequip a weapon and equip another
2. `dbg_inventory`

Pass: the swap holds in the client, and `dbg_inventory` shows the new item carrying the `IsEquipped`
flag and the old one without it.

## [x] I3: There is room for one more item

**Ran 2026-08-14, and the measurement says the hypothesis is dead.** The UI states no slot
capacity anywhere; its only meter is weight, reading **0/255** with the whole seed on board — so
whatever the seed costs, the client isn't weighing it, and there is no visible ceiling to be at.
The server's side: `SendFullInventory: 246 item(s) [Gear 246], 0 resource(s), 20 loadout(s)`,
and after `createitem 20003`, `dbg_inventory: 247 item(s) [Gear 247]`. The entire seed lives in
Gear; Bag and Cache are empty, so a created weapon isn't competing with the prestock for bag
room either. Unless the client hides a Gear-only cap of exactly ~246, fullness is not what
hides an item — I1's cause is back in the item struct or the item arrays.

Run this before I1, and before trusting any conclusion I1 has already produced.

Every other line of investigation here assumes the item was malformed or the message was wrong. This
one asks whether the client simply had nowhere to put it. PIN hands a character all 20 battleframes
at login with their default modules in two configurations each, which no retail character carried,
and Firefall's inventory had a capacity. A created item arriving at a full bag looks identical to a
created item the client rejected: type id resolves, toast fires, server lists it, nothing appears.

**This entry is a measurement, not a pass/fail on the feature.** What it is really doing is putting
a number next to a hypothesis, and the number is new — nothing measured the inventory before
2026-08-13.

1. Log in and let the world finish loading.
2. `grep -a "SendFullInventory:" ~/Games/PIN/logs/GameServer.log | tail -1`
3. Open the inventory and find whatever the UI says about capacity — a slot count, an `x / y`, a
   "full" state. Write down both numbers. If the UI says nothing about capacity anywhere, that is
   the answer to step 6 and worth recording as such.
4. `createitem 20003`
5. `dbg_inventory`, then `grep -a "dbg_inventory:" ~/Games/PIN/logs/GameServer.log | tail -1`
6. Compare: does the server's item count exceed what the UI is willing to hold?

The log line breaks the total down by sub-inventory, because that is where a ceiling would bite:

```
SendFullInventory: 412 item(s) [Gear 380, Bag 24, Cache 8], 0 resource(s), 20 loadout(s)
```

Pass — meaning the hypothesis is dead and I1's cause is elsewhere: the count is comfortably under
whatever the client shows as capacity, and the new item still doesn't appear.

**Confirmed — the inventory is full or over:** the count is at or above the UI's ceiling. Then I1 is
not a wire-format bug at all, and none of the item-struct suspects above need chasing. The fix is to
stop prestocking: `LoadHardcodedInventory` generates all 20 frames because the garage reads owned
chassis out of the loadouts, so the thing to cut is the *items*, not the loadouts. Re-run I1
immediately after, and if the Dev Shotgun appears, [NET-18](../ISSUE-REGISTER.md) closes without a
single byte of the message changing.

**Ambiguous — the count is high but under the ceiling:** worth knowing anyway, and worth trying
`createitem` on a character whose bag is deliberately near-empty before moving on. Note the two
numbers here either way; the next person should not have to re-derive them.
