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
