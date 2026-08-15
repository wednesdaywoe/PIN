---
project: pin
kind: test-stream
title: "Persistence (C1-C7)"
relates:
  - ../TEST-REGISTER.md
---

# Persistence

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M6](../streams/m6-persistence.md). The milestone is that a session leaves a mark: what
you thumped, what you killed for and where you logged out are all there when you come back. Nothing
about this involves RIN, which has never answered a request in this project. The GameServer writes
one JSON file per character.

```
~/Games/PIN/GameServer/save/character-99aabbccddee0100.json
```

That id is zone 448's character with its controller byte masked off, and it will be the same file
every session, because every login is the same character. **A deploy does not touch it**, unlike the
authored CustomData that [S6](Thump-Placement.md) was bitten by: the save directory is not in the
build output, so there is nothing for the install rsync to overwrite. `./start-pin.sh` without
`--no-build` is safe here.

What is saved is resources, items the login seed didn't hand out, the outpost you were nearest, and
playtime. What is deliberately not saved is loadouts and what was equipped, because every login
regenerates all 20 battleframes from `HardcodedCharacterData` and an item restored into a slot no
loadout knows about would be equipped according to nothing. A saved item comes back in the bag.

The log lines this stream reads:

```
grep -a "Saved character" ~/Games/PIN/logs/GameServer.log
grep -a "Restored character" ~/Games/PIN/logs/GameServer.log
grep -a "Could not read" ~/Games/PIN/logs/GameServer.log
```

A save happens on logout, on disconnect, and otherwise once a minute when something has changed.
Nothing is written when nothing changed, so a `Saved character` line means something actually moved.

---

## [x] C1: Crystite survives a logout

**Passed 2026-08-14.** 37 crystite earned from L6's full-cycle thumper, a menu logout, and the
count was intact on screen after logging back in.

**M6's exit condition.** Everything else in this file is diagnosis for when this fails.

1. Log in. `grep -a "Restored character"` — on a first ever run there'll be nothing, which is the
   correct answer for a character with no save.
2. Note the crystite count in the inventory. `dbg_inventory 10` prints the server's own figure.
3. Earn some, by whichever route is quickest that day: `thumper` on a deposit and collect it, or
   kill something with a loot roll. [S3](Thump-Placement.md) and [K1](Kill-Rewards.md) have the
   steps. It needs to be a number you can remember.
4. Log out through the client's own menu rather than closing the window, so this exercises the
   logout path rather than C5's.
5. `grep -a "Saved character" ~/Games/PIN/logs/GameServer.log` — expect a line ending `(logout)`
   naming at least 1 resource kind.
6. Log back in. `grep -a "Restored character"`.
7. Open the inventory.

Pass: the crystite count is what it was at step 3, on screen and not only in the log. Fail with the
count back at zero means the save wrote and the load didn't, or the load ran before the seed; the
`Restored character` line separates those two immediately.

## [x] C2: The save file says what it should

**Passed 2026-08-14**, read on the sitting's first save.

Cheap, and worth doing once the first time C1 passes, because every later entry trusts this file.

```
cat ~/Games/PIN/GameServer/save/character-99aabbccddee0100.json
```

Pass: `Version` is 1, `CharacterId` matches the filename, `LastZoneId` is 448, `Resources` lists the
crystite from C1 with the right quantity, and `TimePlayedSecs` is roughly the session length in
seconds rather than 0 or something enormous.

Worth reading `Items` too: on a session where nothing was created it should be `[]` and **not** a
list of 246 battleframe modules. If the seed is in there, `MarkSeeded` ran at the wrong moment and
every login is now saving a copy of a constant.

## [x] C3: Nothing doubles when you relog twice

**Passed 2026-08-14** — same count across both extra relogs.

The failure this is written for is specific and quiet: resources are restored by adding them to the
inventory, so a restore that ran twice, or a save that included what the restore had just put back
on top of itself, would multiply. One relog can't show it. Two can.

1. Get to a known crystite count. Note it.
2. Log out, log in. Note it.
3. Log out, log in again. Note it.

Pass: the same number three times. Fail: it doubles, or grows by the starting amount each time.

## [x] C4: You come back where you left

**Passed 2026-08-14.** Traveled from the starting outpost to Copacabana, logged out by the market
terminals, and logged back in standing at Copacabana's spawn area — repositioned to the outpost's
spawn point rather than the exact logout spot, which is the design: the save carries the nearest
outpost, not a position.

1. Log in, and note which outpost you spawn at.
2. Travel to a different outpost. Zone 448's are in [Session Setup](Session-Setup.md); `tp` to one
   if walking is slow, but land somewhere you have stood before ([N16](NPC-Combat.md) is the entry
   that explains why that matters).
3. Log out, and read the `Saved character` line's `outpost` figure.
4. Log back in.

Pass: you spawn at the outpost from step 2, and the login logs `Zone 448 Outpost <that id>`.

Note it takes the nearest outpost to where you are standing, not the last one you touched, so
logging out in open ground gives you whichever is closest. A logout further from every outpost than
the zone's spawn point is gives the default, by design.

## [x] C5: A session that never logs out still keeps its crystite

**Passed 2026-08-14.** 50 crystite earned, client killed outright with no logout, relaunched:
same outpost, count intact with the extra 50. The `(disconnect)`-versus-`(autosave)` grep wasn't
read — the data survived either way, and that split only starts mattering if a last-minute change
ever goes missing. **Stronger than the entry asked:** the same sitting also killed and restarted
the *server*, and the character came back intact — the save file is the durable copy, not shard
memory.

This is the one that matters most in practice and it is the one C1 doesn't cover. Most PIN sessions
end without `RequestLogout`: the client gets closed, the connection times out, or the player dies and
has to reconnect because nothing sends `RequestRespawn` ([NET-23](../ISSUE-REGISTER.md)).

1. Earn crystite as in C1.
2. Wait for a `Saved character` line ending `(autosave)`. It comes within a minute of the change.
3. Kill the client outright — close the window, don't log out.
4. `grep -a "Saved character"` for a line ending `(disconnect)`.
5. Log back in.

Pass: the crystite is there. If step 4 produced nothing but step 2 did, the autosave carried it and
the disconnect path didn't fire, which is worth recording either way: the data survived, but on a
session where the last minute mattered it wouldn't have.

## [x] C6: An item survives

**Passed 2026-08-14, riding along C1-C3 rather than run as written.** The Scan Hammer (56826)
survived the sitting's three relogs, visible in the inventory afterwards, and the save file read
in C2 carries the session's created items. The step-2 `dbg_inventory 56826` count comparison was
not separately recorded; the visible-and-saved evidence covers what this entry exists to prove.

Items are the half of the inventory that [I1](Inventory.md) is still stuck on, so run this knowing
that a failure might belong to that entry rather than to this one.

1. `createitem 56826` — a Scan Hammer, the same item [S2](Thump-Placement.md) uses.
2. `dbg_inventory 56826` to confirm the server has it. **This is the check, not the client's bag**,
   because whether the client draws a new item is I1's open question and not M6's.
3. Log out and back in.
4. `dbg_inventory 56826`.

Pass: the count is the same both times, and the save file's `Items` array holds one entry for 56826.
Fail with the item gone after a relog, while C1 passes, means resources persist and items don't,
which is a real M6 defect. Fail with `dbg_inventory` finding it at step 2 but the client never
showing it at any point is I1 and not this.

## [x] C7: A broken save doesn't stop you logging in

**Passed 2026-08-14, run last as instructed.** Login survived into the world at the default
watchtower spawn with a genuinely reset character (no crystite, no Scan Hammer). The log carried
the full story in one line: `Could not read save/character-99aabbccddee0100.json (Expected depth
to be zero at the end of the JSON payload ... Path: $.Resources[0]). Moved it to
save/character-99aabbccddee0100.json.corrupt-20260815-021037 and carried on with a fresh
character` — and both files sit in `save/` side by side, quarantine and fresh copy.

A save file is the only thing in PIN that a session writes and the next session must read, so the
failure where it can't be read has to be harmless. Offline tests cover the parsing; this checks that
the login survives it.

1. Log out.
2. Corrupt the file deliberately:
   `printf '{ "Resources": [' > ~/Games/PIN/GameServer/save/character-99aabbccddee0100.json`
3. Log in.

Pass: you get into the world with an empty resource count, the log carries a `Could not read` line,
and a `character-...json.corrupt-<stamp>` file is sitting next to where the original was. Fail: the
login hangs or the shard throws, which would mean one bad file locks the character out permanently.

Afterwards the character is genuinely reset, so run this last or expect to re-earn C1's crystite.
