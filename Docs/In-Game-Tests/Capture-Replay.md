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
are 0 every time, while `MaxHealth` reads 19192. That is one session on one battleframe, so it is
evidence and not proof — it does not separate "this build didn't use shields" from "this character
had no shield-bearing frame equipped". Worth reading against a second capture before acting on it.

**Real damage numbers.** `TookHit` (controller 5, message 105) carries a `DamageHitStruct` with
`DamageValue` and `DamageType` per hit, 466 of them in the 2016 session. Those are live-server
damage numbers, which is what R4 in [Damage Decay](Damage-Decay.md) needs to check the decay curve
against. They arrive without the shooter's range attached, so pairing a hit with a distance means
correlating against the `MovementView` and `ConfirmedPoseUpdate` traffic around it.

**What a reconciliation looks like.** `Character_LocalEffectsController` message 1 carries a status
effect slot with its effect id, source entity and time — the write PIN never sent before
[D5h](Charge-Camera.md). 17 of them in the 2016 session, and they are the reference for the
[prediction sweep](Prediction-Sweep.md).

## What it does not answer

[H1](Hostility.md) and [R1](Damage-Decay.md) are not capture questions and are still client work.
Both ask whether *PIN's* loader reads an SDB column correctly — H1 about `hostility_stance`, R1
about the `DamageDecay` family — and a recording of Red 5's server says nothing about how PIN
parses a database. Read them off the GameServer log as written.
