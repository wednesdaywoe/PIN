---
project: pin
kind: test-stream
title: Answers Available Without a Client
relates:
  - ../TEST-REGISTER.md
---

# Answers Available Without a Client

Part of the [in-game test queue](README.md).

Some of what's queued elsewhere is a question about what the real server sent, not about what PIN
does with it. Those can be read off a recording of a live session instead of waiting for a trip to
the game machine. [Tools/CaptureReplay](../../Tools/CaptureReplay) decodes a capture through PIN's
own framing and AeroMessages types.

Get the captures once — they are not in this repo:

```
git clone --depth 1 https://github.com/themeldingwars/Documentation.git ~/Games/PIN/captures
gunzip -k "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng.gz"
```

Only the one file is ever used, and it is 19 MB of the clone, so this does the same job:

```
mkdir -p ~/Games/PIN/captures/Captures && cd ~/Games/PIN/captures/Captures
curl -sLO "https://raw.githubusercontent.com/themeldingwars/Documentation/master/Captures/2016-11-15%20-%20Gameplay.pcapng.gz"
gunzip -k "2016-11-15 - Gameplay.pcapng.gz"
```

**Use the 2016 capture.** AeroMessages describes V66 and only that recording is from the same era:
99.8% of its 406,890 GSS messages resolve to a definition and all but 239 deserialize with exact
byte consumption. The 2014 (build 1802) and 2015 (build 1869) recordings frame perfectly but their
message ids have drifted, so a message *name* printed for those two is a guess against the wrong
version. They are good for framing, Control and Matrix questions, not for reading contents.

A session overview, then the two queries that matter below:

```
dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng"

dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng" \
    --controller Character_CombatController --message 105 --dump 40

dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng" \
    --controller Character_BaseController --entity 0x7F3CB864AB349200 --dump 400
```

`0x7F3CB864AB349200` is the player character in that session. Filter to it — most
`Character_BaseController` keyframes in the capture belong to NPCs being spawned with their stats
still zeroed, and reading those as if they were the player's gives an answer of all zeros for
everything.

## What it answers

**Live shield values.** The roadmap's M1 wants to know whether `Battleframe` shipped real shields
or whether [HardcodedCharacterData](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs) is
inventing them. `Character_BaseController` carries `CurrentShields` and `MaxShields` next to
`CurrentHealth` and `MaxHealth`, so the live server's answer is in the third query above. Across
the 36 messages that carry those fields for the player entity, `CurrentShields` and `MaxShields`
are 0 every time, while `MaxHealth` reads 19192. That is one session on one battleframe, so on its
own it did not separate "this build didn't use shields" from "this character had no shield-bearing
frame equipped".

**Settled, 2026-08-11: the build didn't use shields.** The second capture that would have
disambiguated it isn't usable — 2014 and 2015 have drifted message ids, per the warning above — so
the answer came from the db instead, which is what M1 asked for anyway:

```
cp Tools/MinimalSDB/config.example.json Tools/MinimalSDB/config.json   # point "input" at the retail clientdb.sd2
cd Tools/MinimalSDB && dotnet run --project . -- dump
```

Against retail `prod-1962` (built 2016-05-05), `dbitems::Battleframe` has 1676 rows and
`base_shields` is non-zero on **5** of them. Scaling can't rescue that, since it multiplies and
anything times zero is zero, so the capture's live 0 is the frames and not the loadout. The recharge
pair is the surprise: `shield_recharge_per_sec` (779 rows, typically 150, max 450) and
`shield_recharge_delay_ms` (778 rows, typically 10000) are broadly populated. Tuned recharge sitting
next to a zeroed pool is what a mechanic looks like after it was switched off and its other columns
were left where they lay.

`base_health` in the same table reads ~1000 against the capture's live 19192, which confirms it as a
pre-scaling base. Worth remembering before anything tries to source health from it directly; the
scaling that closes that gap isn't implemented.

What [HardcodedCharacterData](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs) does with
this: the recharge pair now uses the shipped 150/10000, and the pool stays non-zero at 3000 — a
value that at least occurs in the table — as a deliberate divergence, so the absorb-with-overflow
path in `TakeDamage` stays observable. Setting it to 0 matches retail exactly and is a one-line
change if faithfulness beats observability later.

**What an item pickup looks like on the wire.** Used on 2026-08-11 to diff PIN's `InventoryUpdate`
against a real one after `createitem` produced a pickup toast and no item — see
[Inventory Delivery](Inventory.md) for the conclusions and what got ruled out.

```
dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng" \
    --controller 2 --message 129 --direction s2c --dump 200

dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng" \
    --controller 2 --message 132 --direction s2c --dump 500
```

The session opens with one full `InventoryUpdate` (`ClearExistingData = 1`, 255+ items, 21858
bytes) and then sends 199 partials. The reference case is a player looting item 82337: message 132
`SimulateLootPickup` at seq 55934, then message 129 at seq 55936 carrying exactly one item, 37
bytes on the wire.

```
000100 A1410100 FDD4035C68295846 02 630B2A58 01 0000 0000 0000 01 00 0000 00 0000000000
```

Reading it back through the struct: `ClearExistingData = 0`, one item — `Unk1 = 0`,
`SdbId = 82337`, `GUID = 0x465829685C03D4FD` (low byte `0xFD`, the item type code),
`SubInventory = 2`, `TimestampEpoch = 1479150435`, `DynamicFlags = 1`, `Durability = 0`,
`Unk3 = Unk4 = 0`, **`Unk5 = 1`**, no `Unk6`, `Unk7 = 0`, no `Modules` — then empty resources and
loadouts, **`Unk = 0`**, and two empty second arrays. That is the message any single-item add in
PIN should be able to be laid next to.

**Real damage numbers.** `TookHit` (controller 5, message 105) carries a `DamageHitStruct` with
`DamageValue` and `DamageType` per hit, 466 of them in the 2016 session. Those are live-server
damage numbers, which is what R4 in [Damage Decay](Damage-Decay.md) needs to check the decay curve
against. They arrive without the shooter's range attached, so pairing a hit with a distance means
correlating against the `MovementView` and `ConfirmedPoseUpdate` traffic around it.

**What a reconciliation looks like.** `Character_LocalEffectsController` message 1 carries a status
effect slot with its effect id, source entity and time — the write PIN never sent before
[D5h](Charge-Camera.md). 17 of them in the 2016 session, and they are the reference for the
[prediction sweep](Prediction-Sweep.md).

**What retail's transport did, which is a different layer from all of the above.** `--transport`
skips the messages and reads the framing: what got resent, how long the sender waited, and how the
far side acked.

```
dotnet run --project Tools/CaptureReplay -- "$HOME/Games/PIN/captures/Captures/2016-11-15 - Gameplay.pcapng" \
    --no-deserialize --transport
```

The whole of [M8](../streams/m8-session-stability.md) was calibrated off this on 2026-08-14, and it
is the first time this stream has decided an implementation rather than confirmed one. 24 resends
across 456619 sub-packets give the retransmit timeout (322–665ms, median 452), the resend count the
header carries (3 on all 24, never 1 or 2), that a resend is byte-identical to its original (15 of 15
where both survive), and that an ack is cumulative rather than per packet (60% of the server's
reliable packets acked, 24 resends in the session). It also closed
[NET-3](../ISSUE-REGISTER.md) outright: the XOR decode PIN had been carrying a TODO about is correct,
and the 15 pairs are the proof.

## What it does not answer

[H1](Hostility.md) and [R1](Damage-Decay.md) are not capture questions and are still client work.
Both ask whether *PIN's* loader reads an SDB column correctly — H1 about `hostility_stance`, R1
about the `DamageDecay` family — and a recording of Red 5's server says nothing about how PIN
parses a database. Read them off the GameServer log as written.

**Nothing about death, checked 2026-08-15.** The session holds zero `Killed` messages
(`Character_CombatView` 108), one `Respawned` — the login spawn — and a null `RespawnTimes` in all
500 of the player's `Character_BaseController` keyframes. Nobody dies in it. So
[NET-23](../ISSUE-REGISTER.md) cannot be answered here, and the trip is recorded rather than
repeated. The client's own `Bleedout.lua` answered it instead; see
[Client UI Source](../Client-UI-Source.md) for why that oracle usually wins for UI-gated questions.

**Array-valued fields are truncated in the dump.** `PrintValue` stops after four entries per array,
so a message carrying 255 items shows four. Counting field values across a whole inventory needs
the printer changed first; single-item messages are complete and safe to count.
