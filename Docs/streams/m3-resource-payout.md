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

Nothing puts resources in the ledger today. A character logs in on zero of everything — the fixed
`FallbackInventoryResources` list every character used to get is gone from the code, and
[G1](../../Game Testing/Resource-Payout.html) confirmed the zero on screen. Moving one resource because
of something the player did is the whole milestone.

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
50329 — the one `ModifyOwnerResourcesCommandDef` row with real values. Every beacon pays the same
200 crystite regardless of node type, which is the "flat grant" this cut promised.

The payout sits in `OnSuccess` rather than in the interaction handler, but **that does not make the
early-collect path pay less**, which an earlier reading of this claimed. Interacting with a thumper
mid-thump moves it to `LEAVING`, and `OnUpdate` calls `OnSuccess` when that state's twelve seconds
elapse, whichever way it was entered. So a thumper cut short after ten seconds pays the same 200 as
one that ran the full seven and a half minutes. Retail scaled yield to how long the thing ran;
`SetProgress` already keeps the figure that would allow it and nothing reads it. That is M4's yield
work, not a defect here — [G4](../../Game Testing/Resource-Payout.html) is written to confirm the
behaviour rather than to fail it.

**COMPLETE 2026-08-13. [G1, G2 and G5](../../Game Testing/Resource-Payout.html) all passed, all first
attempt.**

The milestone closed on a thumper called down by the `thumper` admin command rather than by the
player, and that was a decision rather than an oversight. The retail route needs a beacon item in
the inventory, which is [I1](../../Game Testing/Inventory.html)'s open problem; keeping M3 open on it
would have meant keeping it open on something M3 cannot fix. G3 stays in the queue and reopens the
question the moment items are deliverable. A thumper called down at a player's feet ran its full cycle and
paid, logged as `paying 200 of resource 10 to 1 participant(s)`, and the crystite arrived on screen.
Every countdown ran to the length it announced, which nothing had confirmed before — the whole
sequence is reproduced in G2.

G1 went first and settled the delivery half on its own: three `createitem 10 200` calls took a
crystite count from 0 to 600 with the inventory open. It also paid a debt elsewhere, being the first
evidence anywhere that this client merges a partial `InventoryUpdate` into a UI on screen, which
removes a suspect from [I1](../../Game Testing/Inventory.html). G5 passed unattended in the same session:
the zone's NPC-owned debug thumper completed and paid nobody without taking the shard down.

**What has still never run is the first cut.** `Thumper.OnSuccess` reads grant 50329 out of
`CustomDBInterface` and pays directly, so the uncommented `ModifyOwnerResources` case in
`Factory.LoadCommand` was not on the path — no chain has ever constructed that command. The
milestone's reward works; the aptitude route to it is untested and will stay that way until
something drives a payout through an ability chain rather than through the encounter.

**G3 and G4 are left and neither blocks the milestone.** G3 is the retail calldown; G2 used the
admin command. G4 is the early collect, which G2 half answered by accident — the tester interacted
at `COMPLETED` rather than waiting out its 120 seconds and was paid in full twelve seconds later.

Two
things went in to make them readable. `RewardWithResource` now logs what it paid and **how many
participants it paid it to**, because "nothing arrived" and "nobody was there to be paid" are the
same picture from the screen — and the zone's own debug thumper is owned by the Aero, so it really
does complete with an empty participant set. `ThumperEntity.TransitionToState` logs every state
change, since a seven-and-a-half-minute cycle spent watching a machine stand still gives no way to
tell thumping from stalled.

**Getting a thumper down at all is the part with no answer yet.** The retail route is the client's
own calldown UI, which needs a beacon item in the inventory, and item delivery is
[I1](../../Game Testing/Inventory.html)'s open problem. So a `thumper` admin command was added — it
calls one down at your feet, owned by you, through the same `CreateThumper` the real path ends in —
and the retail route became its own entry (G3) rather than a precondition for testing the payout.

Cut 3 (filling in the other 144 grant defs) needs the client's ability data to recover what each
one is supposed to grant, the same way 50329 was recovered — that requires the retail `clientdb.sd2`
and/or a live capture, neither of which this session has access to. Left for a session with client
access; [DATA-5](../gaps/data.md#data-5) already tracks the size of this kind of gap.
