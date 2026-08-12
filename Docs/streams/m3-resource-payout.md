---
project: pin
kind: stream
title: "M3: Resources Come Out of the Ground"
relates:
  - ../PROGRESS.md
---

# M3: Resources Come Out of the Ground

The thumper collects nothing. `SetProgress` interpolates 0 to 1 between two timestamps, completion
fires its abilities, and the entity is removed. No resource ever changes hands.

The ledger to pay into already works. `CharacterInventory` keeps resources keyed by SDB type id
with add, consume and query, pushes partial `InventoryUpdate` messages once
`EnablePartialUpdates` is set at login, and includes them in the full sync. So unlike item loot in
M5, none of this needs protocol work. What's missing is a connected path and the data to send
through it.

Two cuts:

1. `ModifyOwnerResourcesCommand` is implemented, its def JSON loads, and `CustomDBInterface` has
   the accessor. Only its `case` in `Factory.LoadCommand` is commented out, so no chain can
   construct it. That one is a single line.
2. Of the 145 defs in `aptgss_ModifyOwnerResourcesCommandDef.json`, exactly one carries real values
   (id 50329, 200 crystite). The rest are id-and-comment shells, so what each grant does still has
   to be recovered from the client's ability data.

That buys a flat grant per beacon, the same pile every time. What a particular patch of ground
actually holds is M4, and deliberately not this milestone.

Today the only thing that puts resources in the ledger is
`HardcodedCharacterData.FallbackInventoryResources`, a fixed list every character gets at login.
Moving one resource because of something the player did is the whole milestone.

| Work | Where |
|------|-------|
| Uncomment the `ModifyOwnerResources` case | [Factory.cs:344](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs#L344-L345) |
| Pay out on thumper completion, hardcoded per beacon | [Thumper.cs](../../UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs), its `CompletedAbility` chain |
| Fill in the grant defs that matter | `StaticDB/CustomData/Todo/aptgss_ModifyOwnerResourcesCommandDef.json` |

Exit: call down a thumper, let it finish, and come away holding more crystite than you started
with, updated in the UI without a relog.

Small, and the cheapest reward on the list, which is most of why it's this early. Nothing in it is
unknown, which is exactly why the unknowns were pushed into M4 instead of being allowed to hold
this up.

## What's landed

**Cuts 1 and 2 — code complete, not yet seen in game.** The `ModifyOwnerResources` case in
[Factory.cs](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs) is uncommented, and
[Thumper.cs](../../UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs)'s `OnSuccess` now
calls the already-existing (and previously unused) `BaseEncounter.RewardWithResource` with grant id
50329 — the one `ModifyOwnerResourcesCommandDef` row with real values — on the natural
LEAVING→success transition, not the early-interaction path. Every beacon pays the same 200 crystite
regardless of node type, which is the "flat grant" this cut promised.

Cut 3 (filling in the other 144 grant defs) needs the client's ability data to recover what each
one is supposed to grant, the same way 50329 was recovered — that requires the retail `clientdb.sd2`
and/or a live capture, neither of which this session has access to. Left for a session with client
access; [DATA-5](../gaps/data.md#data-5) already tracks the size of this kind of gap.
