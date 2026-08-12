---
project: pin
kind: stream
title: "M5: Killing Something Pays"
relates:
  - ../PROGRESS.md
---

# M5: Killing Something Pays

Nothing rewards a kill. XP exists on the wire only as a `ProgressionXPRefresh` sent once at
scope-in, and there's no loot at all.

The XP half is mostly known shape: award on the death event from M2, hold it on the character,
push the existing message. Item loot is the part that needs protocol work first, because what the
client expects when something drops isn't established anywhere in the codebase, and the resource
path from M3 doesn't answer it. `AddLootTable`, `SpawnLoot` and `RequireLootStore` all have def
JSONs sitting unused in `CustomData/Todo/`, which is the place to start looking.

| Work | Where |
|------|-------|
| Award XP on kill, track it on the character, push progression updates | `CharacterEntity`, [EntityManager ScopeIn](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs) |
| Work out how the client wants a drop represented, then spawn it | protocol RE first; the unused loot defs in `StaticDB/CustomData/Todo/` and [CatchAll](../../WebHosts/WebHost.CatchAll) logs are the leads |
| Pick up a drop and put it in the bag | [CharacterInventory.cs](../../UdpHosts/GameServer/Data/CharacterInventory.cs), `api/v3/characters/{id}/inventories/bag` |

Exit: kill an NPC, watch XP go up, pick up what it dropped, see it in the bag.

The XP side is small. Item loot is unknown until the protocol question is answered, and that
answer could make it anywhere from a day to a milestone of its own.
