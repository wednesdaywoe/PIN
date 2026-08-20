---
project: pin
kind: stream
title: "M6: Persistence Across Sessions"
relates:
  - ../PROGRESS.md
---

# M6: Persistence Across Sessions

Current frontier as of 2026-08-14. M3, M4 and M5 all closed in the two days before it, which is
what makes this worth doing now: a session can put crystite in your pocket by thumping the ground
under you and by killing things, and the moment you log out it never happened.

Everything a session produces is lost when it ends. Inventory comes from
`CharacterInventory.LoadHardcodedInventory`, health and level from `HardcodedCharacterData`, and
RIN only receives zone, outpost and playtime through `SaveCharacterSessionDataAsync`.

The decision to make first is whether RIN owns this. RIN is optional today and the server is fully
usable without it, which is a property worth keeping. Extending the gRPC contract means the
fallback path gets further from the real one, so either the fallback grows a local store or "no
RIN" stops meaning "no persistence". Make that call explicitly rather than by accident, because
every field added afterwards inherits it.

## The decision: a local store, made 2026-08-14, user-chosen

**RIN does not own this, and the reason is that RIN has never run.** That was checked rather than
assumed: `start-pin.sh` starts exactly three servers, `WebHostManager MatrixServer GameServer`, RIN
is a separate repo that nothing here launches, and
[Session Setup](../In-Game-Tests/Session-Setup.html) already recorded the consequence before this
milestone started — "nothing answers, and it falls back to `HardcodedCharacterData.FallbackData`, so
the fallback isn't a fallback in practice, it's the character." A persistence layer behind the gRPC
contract would have been a persistence layer that has never once been exercised.

So the GameServer writes its own JSON, one file per character, which is the same thing `deposit add`
and `spawngroup add` already do for zone content. No proto change, no second service, and the whole
of it is unit-testable without a client or a `clientdb.sd2`. An interface with a RIN implementation
behind it was offered and declined: the seam is speculative until RIN implements the other half of
it, and a seam built for an implementation nobody has written is a guess about that implementation.

**What this costs is worth writing down.** If RIN ever does own character data, there are two homes
for it and a migration. And a save keys on the character guid, which in PIN is not an identity:
the character list is 38 hardcoded rows whose guid is `0x99aabbccddee0000 + zoneId`, so a save is
per-zone and every player who connects to a zone shares it. That is fine for a single-player vertical
slice and would not survive a second concurrent player. It is a property of the character list rather
than of the store, and it moves when [DATA-9](../gaps/data.md#data-9) does.

| Work | Where |
|------|-------|
| Decide RIN-owned vs local store, and what happens with no RIN | [GameServerAPI.proto](../../UdpHosts/GameServer/GRPC/GameServerAPI.proto), [layer 9](../Architecture/09-webhosts-and-rin.md) |
| Persist inventory, the resource ledger and the character record | `HardcodedCharacterData`, `CharacterInventory`, [GRPCService.cs](../../UdpHosts/GameServer/GRPC/GRPCService.cs) |
| Save on a timer and on logout, not just on zone change | `NetworkPlayer`, `Shard.MigrateOut` |

Exit: thump some crystite, kill things for loot, log out, log back in, and all of it is still
there. XP is not part of that sentence any more. M5 dropped the payout because nothing consumes it,
so there is no XP to persist until the spending half exists.

Medium size, low technical risk, one real architectural decision at the front.

## What landed, 2026-08-14

Code complete and unverified in game. [C1–C7](../In-Game-Tests/Persistence.html) are the check, and
C1 is the exit condition.

| Piece | Where |
|-------|-------|
| The record that goes to disk, and the mapping to it | [SavedCharacter.cs](../../UdpHosts/GameServer/Data/Persistence/SavedCharacter.cs) |
| Reading and writing it, atomically | [CharacterStore.cs](../../UdpHosts/GameServer/Data/Persistence/CharacterStore.cs) |
| Saving on a timer, on logout and on disconnect | [CharacterSaveSim.cs](../../UdpHosts/GameServer/Systems/Persistence/CharacterSaveSim.cs) |
| Loading at login, and coming back where you left | `NetworkPlayer.Login` |
| Telling a session's own items from the login seed | `CharacterInventory.MarkSeeded` |

Three things about it are worth knowing before reading the code.

**The autosave is the load-bearing path, not the safety net.** `RequestLogout` is the tidy exit and
it does save, but almost no PIN session ends that way: the client gets closed, the connection times
out, or the player dies and reconnects because nothing sends `RequestRespawn`
([NET-23](../gaps/network.md#net-23)). `Shard.MigrateOut` is the one exit every session takes, so it
saves too, and between the two of them the minute-long autosave interval is the most anyone loses.

**Item guids are not saved, and that is not a cut.** `GuidService` packs a shard timestamp and a
counter that restarts at zero into every guid it issues, so a guid minted last session can be handed
out again this session to something else. A restored item is minted a fresh one. Nothing outside the
server holds an item guid across a session, because the client is sent the whole inventory at login.

**The seed is not saved.** Every login regenerates all 20 battleframes and their modules, so only
what a session went and got is written down. What that costs is equipping: a saved item comes back
in the bag rather than in the slot it was in, because the loadout that referenced it was rebuilt from
scratch. Persisting loadouts is the obvious next piece and is deliberately not in this cut.

There is a fourth thing that is a lead rather than a feature. The reason the seed exists at login is
the reason [I3](../In-Game-Tests/Inventory.html) suspects the bag is full: PIN hands out every frame in
the game, which no retail character carried. Persistence is what makes it possible to stop doing
that, so [I1](../In-Game-Tests/Inventory.html) may come unstuck as a side effect of a later cut here.

## What needs a client and what doesn't

All of it can be built and unit-tested without one. The store, the save triggers and the load path
are server-side, and the existing offline suite already covers systems of this shape. Only the exit
condition needs a sitting, so the milestone can go from unstarted to code-complete-and-tested on a
machine that has never seen the client.

Two things to keep in mind while working that way. The GameServer won't boot without a
`clientdb.sd2`, so nothing here gets a smoke test until it reaches a machine that has one, and
anything that resolves an item def is being written against the loader's behaviour rather than
watched. And an inventory loaded from a store instead of from
`CharacterInventory.LoadHardcodedInventory` changes what every login hands out, which is the one
thing [I3](../In-Game-Tests/Inventory.html) suspects of breaking item delivery: PIN gives out all 20
battleframes and their modules at login and no retail character carried that. Persistence is the
natural place that stops being true, so I1 may move on its own when this lands.
