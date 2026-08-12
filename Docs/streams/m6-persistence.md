---
project: pin
kind: stream
title: "M6: Persistence Across Sessions"
relates:
  - ../PROGRESS.md
---

# M6: Persistence Across Sessions

Everything a session produces is lost when it ends. Inventory comes from
`CharacterInventory.LoadHardcodedInventory`, health and level from `HardcodedCharacterData`, and
RIN only receives zone, outpost and playtime through `SaveCharacterSessionDataAsync`.

The decision to make first is whether RIN owns this. RIN is optional today and the server is fully
usable without it, which is a property worth keeping. Extending the gRPC contract means the
fallback path gets further from the real one, so either the fallback grows a local store or "no
RIN" stops meaning "no persistence". Make that call explicitly rather than by accident, because
every field added afterwards inherits it.

| Work | Where |
|------|-------|
| Decide RIN-owned vs local store, and what happens with no RIN | [GameServerAPI.proto](../../UdpHosts/GameServer/GRPC/GameServerAPI.proto), [layer 9](../Architecture/09-webhosts-and-rin.md) |
| Persist XP, level, inventory and the resource ledger | `HardcodedCharacterData`, `CharacterInventory`, [GRPCService.cs](../../UdpHosts/GameServer/GRPC/GRPCService.cs) |
| Save on a timer and on logout, not just on zone change | `NetworkPlayer`, `Shard.MigrateOut` |

Exit: thump some crystite, kill things for XP and loot, log out, log back in, and all of it is
still there.

Medium size, low technical risk, one real architectural decision at the front. Wants M3 and M5
landed first, so there's something worth saving.
