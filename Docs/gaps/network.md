---
project: pin
kind: gap-detail
title: "Issue Register — Networking & Protocol (NET)"
relates:
  - ../ISSUE-REGISTER.md
---

# Networking & Protocol (NET)

Wire-level, session, physics-resolution and replication defects — places where what the server
sends, drops, or resolves doesn't match what the protocol or the client expects. Grouped below by
subsystem; IDs are flat across all of them.

## Transport and session

<a id="net-1"></a>

### NET-1 — No retransmit queue [~] built 2026-08-14, unverified in game

`Control_PacketAvailable` logged client acks and discarded them; nothing tracked what PIN itself
sent, so nothing could resend it. "Reliable" meant the server acked what the client sent, not that
the server's own messages survived loss. Invisible on a LAN, and the reason
[M8](../streams/m8-session-stability.md) had to land before this was shown to anyone over a real
connection.

**Built 2026-08-14.** [RetransmitQueue](../../UdpHosts/GameServer/RetransmitQueue.cs) holds every
Matrix and ReliableGss packet until the client acks it, and `Channel.SendOverdue` sends again what
hasn't been answered in 450ms. [L1–L6](../In-Game-Tests/Reliability.md) are the check and none have
run; L1 is the exit condition and needs a session played under induced loss.

**Every constant in it came off the 2016 capture rather than out of the air**, which is what
`CaptureReplay --transport` was written for. That capture holds 24 resends across 456619
sub-packets:

- **450ms before resending.** The 15 resends whose original is also in the capture went out 322 to
  665ms after it, median 452, on a link whose round trip measured about 165ms.
- **Resend count 3 in the header.** All 24 carry 3, both directions, both reliable channels. Not one
  carries 1 or 2 despite every one being a first resend, so the two-bit field reads as a marker at
  its top value rather than as an attempt counter. PIN sends what the client was built to receive.
- **The same bytes.** All 15 pairs are byte-identical once the XOR is undone, so a resend is the
  original with a new header and a masked body, not a rebuild.
- **A cumulative ack.** `NextSeqNum` is `AckForNum + 1` on 36753 of 36759 acks, and the client acked
  only 60% of the server's reliable packets across a session that needed 24 resends, which is only
  possible if one ack covers the run behind it.

**The one thing retail cannot answer is how many attempts to make**, because no sequence in the
capture is resent twice. PIN stops after three, which is where the header's resend field stops
counting, and logs a Warning when it does: a dropped reliable packet is the only event in the server
that leaves a client's copy of something permanently wrong, and nothing above the transport layer
ever finds out.

<a id="net-2"></a>

### NET-2 — `CurrentShortTime` wraps every ~65 seconds [ ] open

[Shard.cs:84](../../UdpHosts/GameServer/Shard.cs#L84) truncates the shard clock to a `ushort`.
Already a known source of bugs at one player; more entities and more players is more chances to
land on the wrap. See the [public-server appendix](../streams/public-server-hardening.md) for why
this gets worse with scale, but it's not scale-gated — it can bite today.

<a id="net-3"></a>

### NET-3 — Inbound resend detection [x] confirmed and fixed 2026-08-14

`Channel` carried its own TODO about whether its resend-detection and XOR-decode logic was correct,
and nobody had constructed a case that would prove or disprove it. The 2016 capture is that case.
It holds 24 resent packets, and the 15 whose original also survives decode to byte-identical
payloads once the XOR is undone, so the detection and the table are both right. The TODO is gone and
the reasoning is in the comment that replaced it.

**Reading it found a live bug next to it.** A recognised resend was decoded and then dispatched like
any other packet, so anything the client resent ran twice — one trigger pull firing two shots, one
interaction resolving twice. A resend is only sent when the sender believes its packet went unacked,
so a resend of a sequence already behind the channel's high-water mark is a lost ack rather than a
lost packet: PIN now re-acks it, which is what stops the loop, and drops the copy.

**A second fault on the same path could take the shard down.** A resent fragment arriving inside a
split run hit `SortedDictionary.Add` with a key already in the dictionary, which throws, on the
shard thread, with no isolation around it — the same shape as [NET-21](#net-21). It's an indexer
assignment now.

Neither has been seen in game. The capture holds exactly one client-to-server resend across the
whole session, so this is rare enough that only [L4](../In-Game-Tests/Reliability.md) under induced
loss is likely to exercise it.

<a id="net-4"></a>

### NET-4 — `MTUProbe` silently dropped [~] needs confirmation

[NetworkClient.cs:308](../../UdpHosts/GameServer/NetworkClient.cs#L308) receives the control packet
and does nothing with it — no response sent. Unclear whether the client ever depends on getting one
back; needs a capture to check what retail did here.

<a id="net-5"></a>

### NET-5 — Oversized UGSS messages aren't split [ ] open

[Channel.cs:472](../../UdpHosts/GameServer/Channel.cs#L472): a message too large for one RGSS frame
has no split-and-reassemble path. Fine until something large enough to need it gets sent.

## Physics and hit resolution

<a id="net-6"></a>

### NET-6 — Physics material id 0 has no fallback [ ] open

[PhysicsEngine.cs:287](../../UdpHosts/GameServer/Physics/PhysicsEngine.cs#L287) (see also
[layer 7](../Architecture/07-physics-and-world.md)) resolves material id 0 to null instead of a
default, silently dropping hit attribution for anything that lands on it.

<a id="net-7"></a>

### NET-7 — HKX loader desyncs shape child index [ ] open, significant

[PhysicsEngine.Shapes.cs:154](../../UdpHosts/GameServer/Physics/PhysicsEngine.Shapes.cs#L154): the
HKX loader adds extra children beyond what the shape defs describe, so the child index used to
resolve a hit no longer lines up with the def that named it. Can misattribute which body part —
including headshots — a hit actually landed on. Worth prioritizing over the rest of this section;
it's the one with a gameplay-visible failure mode.

<a id="net-8"></a>

### NET-8 — Shapeless tagfiles fall back to a placeholder box [ ] open

[PhysicsEngine.Shapes.cs:83](../../UdpHosts/GameServer/Physics/PhysicsEngine.Shapes.cs#L83) plus
[TagfileLoader.cs:39,412,418](../../UdpHosts/GameServer/Physics/TagfileLoader/TagfileLoader.cs): a tagfile with no
shapes gets a generic box collider instead of failing loud or being flagged. Makes a missing-shape
bug look like working collision until someone notices the box doesn't match the model.

## Entities and replication

<a id="net-9"></a>

### NET-9 — Entity scope-in bypasses proper tick logic [ ] open, workaround

[EntityManager.cs:1810](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs#L1810) is
marked `TEMP: Hack` in its own code — it pushes new entities to clients by bypassing the real
scope/distance tick logic rather than going through it. Works today; the real path it's standing in
for still needs building.

<a id="net-10"></a>

### NET-10 — `ScopeRange == 0` semantics unknown [~] needs confirmation

[EntityManager.cs:183](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs#L183):
unclear whether zero means "no scope range" or "use the default" — currently falls back to the
component default, unconfirmed against what the original data intended.

<a id="net-11"></a>

### NET-11 — Weapon ammo overrides not applied [ ] open

[WeaponSim.cs:79](../../UdpHosts/GameServer/Systems/WeaponSim/WeaponSim.cs#L79): ammo resolution
for a fired shot ignores any override, always resolving the base ammo type.

<a id="net-12"></a>

### NET-12 — `MovementState` packing widened without confirmation [~] needs confirmation

[MovementRelay.cs:48](../../UdpHosts/GameServer/Systems/MovementRelay/MovementRelay.cs#L48) carries
its own comment flagging doubt — "This was ushort previously!" — about whether the wider packing is
correct or a regression.

<a id="net-13"></a>

### NET-13 — Vehicle handling: seat assignment and view refresh [ ] open

Two related vehicle defects: seat-assignment logic blocks taking the last seat on some vehicles
(Hauler id 201, Convoy id 66,
[VehicleEntity.cs:264](../../UdpHosts/GameServer/Entities/Vehicle/VehicleEntity.cs#L264)); and
vehicle entry recreates every view on the vehicle as a blunt full refresh instead of a targeted
update ([VehicleEntity.cs:302](../../UdpHosts/GameServer/Entities/Vehicle/VehicleEntity.cs#L302)),
which works but costs more than it needs to.

<a id="net-14"></a>

### NET-14 — `SpawnDeployable` computes a faction it then discards [ ] open

[Deployables-And-Vehicles.md V6](../In-Game-Tests/Deployables-And-Vehicles.md): `SpawnDeployable`
resolves `factionId` from owner/override/SDB-default, then assigns `deployableInfo.DefaultFaction`
directly instead — `useOwnerFaction` and `overrideFactionId` are dead code. Not yet caught live
because deployable 348 happens to default to faction 1 anyway, but any deployable whose
`DefaultFaction` is 0 would get faction 0 instead of the intended fallback.

## Client prediction

<a id="net-15"></a>

### NET-15 — `OrientationLockCommand` sends an unpaired cancel event [ ] open, deferred by design

[OrientationLockCommand.cs:29-36](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Movement/OrientationLockCommand.cs#L29-L36)
sends `ForcedMovementCancelled` on removal for a movement-start the server never announced. This is
D5f from [Charge-Camera](../In-Game-Tests/Charge-Camera.md) — a real protocol inconsistency, and an
attempted fix for it failed and was reverted, because no safe cancel-time can be derived without a
real capture to check against. The user-visible symptom this was chasing (stuck vertical mouselook)
is fixed regardless, via a different mechanism — see D5h. Left open because the inconsistency
itself is still there, just not currently causing visible harm.

<a id="net-16"></a>

### NET-16 — Predicted effects reach the owning client twice [ ] open, low priority

[Charge-Camera.md D5h](../In-Game-Tests/Charge-Camera.md): every predicted effect now arrives via
both `CombatController`/`CombatView` and the newly-written `LocalEffectsController`, tripling
rejection noise on the client. Functionally harmless, but likely diverges from retail, which almost
certainly sent owner-private effects once, not twice.

<a id="net-17"></a>

### NET-17 — `LocalEffectsData.Entity` semantics unconfirmed [~] needs confirmation

[Charge-Camera.md](../In-Game-Tests/Charge-Camera.md): whether `Entity` names the initiator or the
carrier/target of a predicted effect is unconfirmed — only self-applied effects have been tested.
Blocks correctness for any effect predicted onto a target rather than the caster.
[Prediction-Sweep P5](../In-Game-Tests/Prediction-Sweep.md) exists to answer this.

<a id="net-18"></a>

### NET-18 — `createitem` delivery fix unverified against the original symptom [~] needs confirmation

[Inventory.md](../In-Game-Tests/Inventory.md): two wire-format divergences (`Item.Unk5`,
`InventoryUpdate.Unk` ordering) were fixed, but I1 — does a created item actually show up in-game —
is still unrun. The fix is plausible but not yet confirmed to resolve the original toast-with-no-item
symptom it was meant to close.

<a id="net-19"></a>

### NET-19 — Two 2026-08-11 prediction fixes await confirmation [~] needs confirmation

Two fixes deployed the same day, both unverified pending
[Prediction-Sweep P0](../In-Game-Tests/Prediction-Sweep.md): PIN previously never sent
`UnlocksUpdate` (certificates), silently blocking cert-gated module slotting client-side; and
`EntityManager.cs:1160` sent `ProgressionXPRefresh` with no `entityId`, so it addressed entity 0 and
was dropped by the real client. If P0 doesn't hold, [DATA-9](data.md#data-9)'s stub endpoints are
the next suspect.

<a id="net-20"></a>

### NET-20 — The Prediction Sweep is almost entirely unverified [~] needs confirmation, large scope

[Prediction-Sweep.md](../In-Game-Tests/Prediction-Sweep.md) covers 47 effects shaped like the one
D5h fixed (27 player-reachable); all of it blocks on P0. One rollup entry rather than 47, since
none of them are independently actionable until P0 confirms the cert-gate fix holds.

<a id="net-21"></a>

### NET-21 — An encounter could kill the shard thread outright [x] fixed 2026-08-12

The Coral Forest thumper reliably ended every session it was left running in. Around seven and a
half minutes after the zone loaded, `Thumper.OnSuccess` reached `BaseEncounter.RewardWithResource`,
threw a `NullReferenceException`, and took `Shard.RunThread` with it: no tick, no physics, no AI, no
movement for anyone still connected, and nothing in the log but a stack trace at the very end. It
was found while chasing something else entirely, in the tail of a
[NPC combat](../In-Game-Tests/NPC-Combat.md) session log.

Three separate defects, stacked so that fixing only the visible one would have swapped a crash for a
different crash:

1. **A null participant.** `EncounterManager.CreateThumper` built its participant set as
   `[owner.Player]`. `CharacterEntity.Player` is null for every NPC, and the debug thumper is called
   down by the Aero, so the set held a single null from the moment it was created. Nothing
   dereferenced it until the payout, minutes later. Now filtered at the source, and
   `BaseEncounter.LiveParticipants` skips nulls for the two helpers that reach through a participant.
2. **Mutation during iteration.** `Tick` walked `_encountersToUpdate` with a `foreach` while
   `OnUpdate` → `OnSuccess` → `StopUpdatingEncounter` removed from that same set. Thumper does this
   on its LEAVING tick, so the payout NRE and a `Collection was modified` were racing to be thrown;
   the NRE won and hid the other. Both update loops now iterate a snapshot.
3. **No net under any of it.** An unhandled throw from one encounter stopped the entire shard. The
   update loop now catches per encounter, logs it, and stops updating the offender — content
   misbehaving costs that encounter, not the server.

Verified by running the server headless through a full unattended thumper cycle — no client needed,
the state machine advances on its own clock in about seven and a half minutes. The confirmation is
exact rather than circumstantial: in the crashed session the last line before the stack trace was
`Executing Chain 1154394 (RemoveEffect), Self: ThumperEntity`, and in the verification run that same
chain fired at the same point in the lifecycle, the thumper went silent as `OnSuccess` removed it,
and the shard kept ticking for three more minutes with no unhandled exception and nothing caught by
the new guard.

One caveat on that run: it was the build with the null filtered at `CreateThumper` rather than in the
`BaseEncounter` constructor, which is where it ended up. Both produce the same empty participant set
and the snapshot and try/catch are identical between them, so the run stands — but the shipped binary
gets its own confirmation from the next session that leaves the Coral Forest thumper running.

The general lesson is point 3. `Shard.Tick` calls its systems in a row with no isolation between
them, so any of them can still do this; the encounter loop is simply the one that was caught doing
it. Worth extending the same treatment outward if a second system ever manages it.

<a id="net-22"></a>

### NET-22 — An NPC's pose never reached the client [x] fixed 2026-08-13

Monsters teleported. Found on the first locomotion session, 2026-08-13: an NPC would sit at its
spawn, then appear somewhere else entirely, having neither walked nor slid. The steering was not the
problem — server-side it was stepping about 30cm every 50ms, exactly as `SteeringTests` says it
should.

**Position was only half of it.** The same session turned up a second symptom that had been read as
roughness rather than a defect since combat testing began: an NPC took several seconds to react to a
target moving, holding its old aim and firing where the player used to be before snapping round.
That is this bug too. `SetAim` writes the aim and body yaw into the movement view, so it travelled —
or rather didn't — by the same route as the position.

What made it read as slow AI rather than as a missing update is that the two halves of "an NPC is
shooting at you" go different ways. `SetFireBurst` writes `WeaponBurstFiredProp` into
`Character_CombatView`, which *is* flushed to scoped clients, so the client was told to draw the
burst on time and drew it along an aim seconds old. Nothing replicates the projectile itself —
`ProjectileSim` sends nothing to scoped clients — so the direction the shot appears to take is
inferred entirely from the stale aim.

The damage was never wrong. `NpcCombat.Fire` passes `shot.Direction`, recomputed each tick from live
positions, straight into `WeaponSim.OnFireWeaponProjectile`; it never reads the replicated aim. Only
the picture was stale, which is why N2, N5 and N6 could pass against this bug without anyone
noticing it.

Nor was the AI slow: `TargetSelection.Tick` and `NpcCombat.Face` both run on the 50ms AI tick, and
`Face` re-aims on a deadband of about a degree. The only genuine delay is acquisition — threat gains
20/s against a threshold of 10, so half a second to notice a new target — and that is deliberate.

Nothing replicated it. `EntityManager.FlushChanges` skips `Character_MovementView` on purpose:

> We don't flush Character_MovementView as those changes are basically handled entirely by
> CurrentPoseUpdate

That is true for a player, whose client sends a pose on every input and whose `MovementRelay`
forwards it to everyone else as a `CurrentPoseUpdate`. It stopped being true the moment the server
started moving a character nobody was driving. An NPC had neither half: its movement view was never
flushed, and nothing sent a pose on its behalf.

So the only position a client ever held was the one in its scope-in keyframe. The next time it heard
anything was the checksum reconciliation in `EntityManager` noticing its copy no longer matched and
sending a fresh keyframe — at which point the monster covered the whole distance in a single frame.
The teleport was not the NPC moving wrongly; it was the client being told, late and all at once,
about movement that had already finished.

Fixed by `Systems/AI/NpcPose.cs`, which sends a `CurrentPoseUpdate` to the scoped clients once per AI
tick, and only when the pose has actually changed so an idle monster stays silent. It carries
`Shard.CurrentShortTime`, which is what the client interpolates between poses with.

Two things this cost, worth remembering next time something looks like an AI bug:

- **The server log could not have shown it.** Every line the AI writes describes the server's own
  copy, which was correct throughout. A defect that lives entirely in what was *not* sent is
  invisible to a log of what was decided. `SteeringTests` passing and the log reading correctly were
  both true and both irrelevant.
- **It masked the entry it broke.** [N8](../In-Game-Tests/NPC-Combat.md) asks you to watch a monster
  the whole way in, and [N9](../In-Game-Tests/NPC-Combat.md) asks whether the run animates. Neither
  is answerable when the approach is not drawn, so a single replication gap took out both the
  milestone's exit condition and the entry watching the one value in this work that had never been
  seen on the wire.

Confirmed the same day: with the pose replicated, [N8–N13](../In-Game-Tests/NPC-Combat.md) all pass.
That also settles `0x2004` as the running movement state — an NPC now plays a run animation while it
closes, so the value derived from `Movestate` and `MovementFlags` in
[N9](../In-Game-Tests/NPC-Combat.md) is right, and the packing [NET-12](#net-12) flags as unconfirmed
has at least one asserted value that the client agrees with.

<a id="net-23"></a>

### NET-23 — A dead player has no way back [ ] open, found 2026-08-13

Death is a one-way door. Found by [N16](../In-Game-Tests/NPC-Combat.md) on 2026-08-13, which is the
first time in PIN's history that a player has been killed by the game rather than by a command: the
tester stood in front of Basin Mouth with invulnerability off and let it run.

The server handled the kill correctly and everything after it stopped.

```
17:48:16 Fallback took 1 damage from CharacterEntity (2305015472095170560), 0 shields and 0 health left
17:48:16 NPC 2305015472095171584 target 11072869122414870784 -> null
17:48:16 NPC 2305015472095171328 target 11072869122414870784 -> null
17:48:16 NPC 2305015472095170816 sets off home at 11m/s, 29.886986m away, stopping at 1.5m
17:48:56 RECEIVED CloseConnection
```

`CharacterEntity.Die` ran — all six attackers dropped target and walked home inside the same second,
which only happens once `Alive` is false — and it sent a `KilledEvent` to the scoped clients and set
`CharacterStatus.Dead`. Forty seconds later the tester quit, because nothing else was going to
happen.

**The machinery to come back exists and was never asked to run.** `BaseController.RequestRespawn`
(command 198) is implemented, guards correctly on the character being `Dead`, and calls
`NetworkPlayer.Respawn`, which is a complete and working routine — it is what puts every player into
the world at login. The client simply never sent the command. Nothing in the session log between the
kill and the disconnect is anything but keyframe chatter.

So the gap is on the way out, not the way in: the client is not being told enough at death to offer a
respawn. The likely candidate is `RespawnTimesData` — `Respawn` writes `RespawnTimesProp` twice and
clears it, with a comment saying it isn't understood, and `Die` never touches it at all. Retail drove
the death screen's countdown and spawn-point list from that field, so a client with nothing in it
plausibly has no UI to offer. Unconfirmed: nobody has watched what a retail server sends on death,
and the [2016 capture](../In-Game-Tests/Capture-Replay.md) is the cheap place to look before guessing
at the field's shape.

Two smaller things fall out of the same entry:

- `Die` had no log line, so the only evidence a player had died was arithmetic across thousands of
  damage lines. It now logs at Information, which is what makes the sequence above greppable.
- Nothing subscribes to `CharacterDiedEvent`. The event is enqueued and dropped for players; for NPCs
  `Die` reaches around it and sets a corpse lifetime directly. Whatever fixes this should probably go
  in the subscriber rather than deeper into `Die`.

Not an M2 defect — M2 is about whether a monster is worth fighting, and being killed by one is the
evidence that it is. It belongs to whichever milestone owns the player lifecycle, and until then a
tester who dies has to reconnect.

<a id="net-24"></a>

### NET-24 — A finished thumper never leaves the client [ ] open

A thumper that completes its cycle pays out correctly and is removed from the shard. **On screen it
stays standing.** The tester's report, 2026-08-14: "on sending thumper away, sound effect and launch
prep animation played, thumper remained on the ground."

The server side is doing what it should. `Thumper.OnSuccess` calls `Shard.EntityMan.Remove`, which
scopes the entity out of every player holding it and then drops it. The client, however, goes on
asking for a fresh copy of the dead entity's `ResourceNode_ObserverView` — and PIN answers each one
with a warning, because `NetworkClient` masks the controller byte off the requested id and finds
nothing under it:

```
KeyframeRequest failed to find 2233904037877645104 (ResourceNode_ObserverView)
```

**It never stops.** One 32-minute sitting on 2026-08-14 logged 648 of these across four abandoned
thumpers — 259, 218, 134 and 37 requests, each starting three to four seconds after its thumper was
removed and continuing at a flat rate of about one every 5.5 seconds until the client disconnected.
The load is trivial; the leak is not, because every completed thumper in a session adds another
permanent loop, and nothing ever retires one.

**Two things narrow it.** First, no earlier log in the project contains a single one of these,
because no thumper had ever finished while a player was watching — [S3–S5](../In-Game-Tests/Thump-Placement.md)
on 2026-08-14 were the first runs to take one all the way through. Second, and more useful: **the
thumpers a player cut short left no stale requests at all.** Two early collections that day (both at
deposit 5, completions 0.04 and 0.03) produced none, while all four full-cycle runs produced them.
That is the same split as [DATA-18](data.md#data-18) — `OnInteraction` transitions a `THUMPING`
thumper straight to `LEAVING` itself, where a completed one is transitioned from `OnUpdate` — so the
suspicion is that something about the `CLOSING`/`COMPLETED` leg leaves the client holding a view the
scope-out does not cover, not that removal is broken.

The client's own log shows it adding an observer view to a thumper **already in state 7, `LEAVING`**
(`tfResourceNode::AddView(Observer) - owner=Fallback, beaconId=33978, state=7`), with the owner
unresolvable. An entity introduced to a client twelve seconds before it is destroyed, through
[NET-9](#net-9)'s scope-in hack, is a plausible way to end up with a copy nothing later accounts
for.

No session has been shown to end because of this. The 2026-08-14 client did quit with two loops
outstanding, seventeen seconds after the second one began, but it quit cleanly — its own log reads
`Application shutdown called: no error` — which is not what a client killed by a network fault
writes.

**A code read on 2026-08-14 cleared the message itself and moved the suspicion to the channel.**
The scope-out PIN sends is the right one: msg 6 with an empty body on the entity's only view. The
2016 capture has no ResourceNode traffic at all, so it cannot answer this directly, but it does carry
`Deployable_ObserverView msg 6, ~0 byte body` — and a deployable is a single-view entity like a
thumper, so one empty msg 6 on the sole view is how retail removed one. Nothing else in the server
ever sends a message naming a thumper's entity id; the only three are the keyframe, the view deltas
and that scope-out.

**So the likeliest reading is that NET-24 is [NET-1](#net-1) in costume.** That scope-out is sent
once, on a channel that acks inbound packets and has no outbound tracking whatsoever —
`Channel` only ever calls `SendAck`, and `NetworkClient.NetworkTick`'s own comment promises a
"reliable retransmission" that does not exist. Lose that one packet and nothing ever says it again.

**The client's `AddView(Observer)` at state 7 reframes the timeline.** Only a keyframe makes the
client add a view, and only `ScopeIn` or an answered `KeyframeRequest` sends one — so that line is
the server *answering a request* during `LEAVING`. The request loop was therefore already running
before removal, and the failures only become visible in the log once the entity is gone. The ~5.5s
cadence is the client's own retry, not something removal started.

That also gives the full-cycle-versus-cut-short split a mechanism instead of a coincidence. Thumper
view deltas flush on `UnreliableGss`, and a full cycle sends about 106 of them — 100 from
`SetProgress` alone, at 3s intervals across 300s of thumping — against roughly 7 for a collection at
4%. Fifteen times the exposure to a silent drop, on a view whose `StateInfo` only changes at
transitions and is never re-sent. `Character_CombatView` is already pinned to `ReliableGss` with a
comment about the client latching the last value it saw; thumper state is the same kind of field.
**That change is deliberately not made yet** — [G6](../In-Game-Tests/Resource-Payout.md) is written
to measure whether deltas are being lost before anything is tuned on the guess that they are.

**What landed on 2026-08-14**, none of it verified in game:

- A failed keyframe request for a *view* now answers with the scope-out for that typecode instead of
  only logging. The server had established, 648 times in one sitting, that a client was holding an
  entity it did not have, and replied with a log line. Re-sending costs one packet per stale request
  and ends the loop whichever way the first one was lost — dropped, or raced past by a keyframe the
  network thread answered while the shard tick was removing the entity. Controller typecodes still
  just warn: they are removed by a different message and nothing has been seen asking for a dead one.
- **Successful keyframe requests now log at Debug.** They were Verbose while failures were Warning, so
  no log could separate "asking all along" from "started when it died" — the one reading that
  discriminates the hypotheses above.
- A queued scope-in whose entity died before the queue reached it is dropped. `_queuedScopeIn` drains
  one per 20ms and `Remove` takes the scope set with it, so this handed clients fresh copies of
  removed entities — NET-24's symptom arriving by a second route.

The same pass fixed a latent shard kill next door. `GetScopedPlayers` reads the scope map with
`TryGetValue` and documents a missing key as normal, but `ScopeIn`, `ScopeOut` and the periodic scope
check all used the raw indexer, which throws `KeyNotFoundException`. `Shard.Tick` and
`Shard.RunThread` catch nothing — NET-21's fix only wrapped the encounter loop — so an entity removed
from the network thread part-way through a scope pass could take the whole shard down. All three now
read it safely.

<a id="net-25"></a>

### NET-25 — An ack claims a packet that never arrived [ ] open, found 2026-08-14

`Channel.Process` acks the highest inbound sequence it has seen, not the highest it has seen with no
gap behind it. So if the client sends 8, 9 and 10 and 8 is lost, PIN acks 9 and then 10, and the
client reads that as confirmation that 8 arrived.

**That is the exact mirror of [NET-1](#net-1), on the inbound side, and it survives NET-1's fix.**
An ack is cumulative — 36753 of the 2016 capture's 36759 acks put `NextSeqNum` at `AckForNum + 1`,
which is only meaningful if `AckForNum` names the end of an unbroken run — so PIN is telling the
client that everything up to the number it names got through. Whatever was in packet 8 is lost
silently, and the client is the only party that could have resent it.

**Found by reading, not by running, and deliberately not fixed the same day.** Fixing it means PIN
holding its ack at the gap until the missing packet arrives, which needs a record of what was taken
above the gap so the resends of 9 and 10 aren't run a second time. `DeliveredSequences` is already
that record, added for the duplicate suppression in [NET-3](#net-3), so the work is small. What is
missing is the other half of the policy: what to do when the gap never fills. The capture shows
retail's client acking, never retail's server recovering, so there is no evidence for how long to
stall before giving up and jumping the ack forward, and a wrong answer here stalls the channel for
the rest of the session rather than losing one message. That is a worse failure than the one being
fixed, which is why this is recorded rather than guessed at.

Costs a lost client command — a shot, an interaction, an ability — roughly as often as the link
drops a reliable packet, which is never on loopback and rarely anywhere else. Every session PIN has
ever run has been on loopback.
