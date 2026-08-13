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

### NET-1 — No retransmit queue [ ] open, scheduled as M8

`Control_PacketAvailable` logs client acks and discards them; nothing tracks what PIN itself sent
so nothing can resend it. "Reliable" currently means the server acks what the client sends, not
that the server's own messages survive loss. Invisible on a LAN, and the reason
[M8](../streams/m8-session-stability.md) has to land before this is shown to anyone over a real
connection.

<a id="net-2"></a>

### NET-2 — `CurrentShortTime` wraps every ~65 seconds [ ] open

[Shard.cs:84](../../UdpHosts/GameServer/Shard.cs#L84) truncates the shard clock to a `ushort`.
Already a known source of bugs at one player; more entities and more players is more chances to
land on the wrap. See the [public-server appendix](../streams/public-server-hardening.md) for why
this gets worse with scale, but it's not scale-gated — it can bite today.

<a id="net-3"></a>

### NET-3 — Inbound resend detection unverified [~] needs confirmation

[Channel.cs:93](../../UdpHosts/GameServer/Channel.cs#L93) carries its own TODO about whether its
resend-detection and XOR-decode logic is actually correct. Nobody has yet constructed a case that
would prove or disprove it.

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
