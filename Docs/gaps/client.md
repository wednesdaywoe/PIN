---
project: pin
kind: gap-detail
title: "Issue Register — Client & Environment (CLIENT)"
relates:
  - ../ISSUE-REGISTER.md
---

# Client & Environment (CLIENT)

Problems observed in the real client under Wine/Proton, as opposed to anything PIN's server code
gets wrong. These are environment-shaped bugs — the fix usually isn't a code change, it's a launch
option or a binary patch, and that's exactly the kind of thing that's easy to lose track of between
sessions.

<a id="client-1"></a>

### CLIENT-1 — World-entry freeze: lost wakeup in Wine's fsync path [~] mitigated, needs confirmation

An intermittent freeze on world entry survived four rounds of live debugging
([Transport-And-Lifecycle T4–T8](../In-Game-Tests/Transport-And-Lifecycle.md)) before being caught
live twice and localized. T7 found the render thread wedged in the D3D present path while holding
a game lock; T8 swapped DXVK out for wined3d and reproduced the identical freeze, which exonerates
the GPU driver and pushed the wedge one layer down: the render thread parks in
`RtlEnterCriticalSection` on the CRT/NT heap lock, reached from a D3D texture upload, while it
holds the game lock. An Awesomium web-UI worker piles onto that same game lock when a social/squad
panel opens, and 60 seconds later Wine prints the timeout. The heap lock in that chain reads
**free**, which is the signature of a lost wakeup in Wine's fsync, not a held-lock deadlock.

T9 forced that sync path off — `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1` on top of DXVK — and got four
consecutive freeze-free sessions. That's the current required client launch config, documented in
[Http-Only-Setup.md](../Http-Only-Setup.md#client-launch-options). It stays `[~]` rather than
`[x]` because the underlying bug is an intermittent Wine race: four clean sessions is strong
evidence, not proof it can't recur. Close this out only after enough further sessions make
recurrence implausible, or when a Proton build ships with the wakeup fixed upstream (then this
workaround can be dropped).

<a id="client-2"></a>

### CLIENT-2 — Firefall under Proton can't complete HTTPS login [x] closed, with a guard

Wine's WinHTTP validates the login TLS handshake against the wrong certificate store, so HTTPS
login never completes. The permanent fix: every client-facing URL is served plain HTTP instead
(a 1-byte patch to `FirefallClient.exe` removing its HTTPS-only check on the oracle URL). Setup and
undo steps: [Http-Only-Setup.md](../Http-Only-Setup.md). Confirmed by
[Transport-And-Lifecycle T1–T3](../In-Game-Tests/Transport-And-Lifecycle.md), all passing.

The guard doesn't survive a Steam file-verification pass — it reverts the patched exe — so this
has to be reapplied after any Steam-initiated verify or update. Worth a line in
[Session Setup](../In-Game-Tests/Session-Setup.md) if it isn't there already.

### CLIENT-3 — Some creature models don't render along the orientation the server sends [ ] open, cosmetic

A Melded Aranha (528) engaged at melee range stands and attacks about 90° off from the player it is
attacking. Confirmed as a model property rather than a server defect on 2026-08-13, by the only test
that can separate the two: a Chosen Fiend (1196) and an Aranha spawned in the same place, engaging
the same player. The Fiend faces the player correctly. The Aranha does not. Both got their
orientation from the same three lines of `Facing.Towards`, and the `opens fire` log line records the
bearing each was told to stand at, so "the server sent different values" is ruled out rather than
assumed.

What that leaves is the models' own forward axes. `Facing.Towards` builds a yaw around +X because
that is what [N1](../In-Game-Tests/NPC-Combat.md) confirmed — on a Chosen, a humanoid rig. Nothing
said that generalises to a creature rig, and it doesn't.

**The server has no way to know.** Nothing PIN can read carries a per-model facing offset:
`dbcharacter::Monster` has no such column, and `PoseType` — the record that would be the natural home
— carries only physics height, radius, mass and collision ids. The axis lives in the model asset,
which the client loads and the server never sees. So a fix means either a hand-built table of
per-monster yaw offsets, which is invention of exactly the kind
[DATA-10](data.md#data-10) already tracks too much of, or reading the offset out of the asset DB,
which is unexplored.

Left open and unfixed on purpose. It is cosmetic: the shot direction is computed from live positions
and never from the rendered facing, so a sideways Aranha hits exactly as hard as a forward-facing one
(49 a swing at 4m, straight off the 2026-08-13 log). Worth revisiting if a second creature turns up
wrong in a *different* direction, since two data points would say whether the offsets are per-model
or whether every non-humanoid shares one.

Not to be confused with the Aranha's `PosetypeId` of 0, which sounds alarming and isn't:
`GetCharacterPoseAsset` already falls back to the visual record's `HitboxCollisionId` for those, and
the session log has no `No suitable collisionId found` warnings.
