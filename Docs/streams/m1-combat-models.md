---
project: pin
kind: stream
title: "M1: Confirm the Combat Models"
relates:
  - ../PROGRESS.md
---

# M1: Confirm the Combat Models

Three combat numbers were guesses that a real client answers in an afternoon: the faction stance
encoding, the range decay curve, and whether `Battleframe` shipped live shield values. All three
were already queued in [In-Game Tests](../In-Game-Tests/README.md), and all three degrade safely
when wrong.

It went first because everything after it tunes against these. Building AI that fights the player
is harder to judge when you can't tell whether a fight feels wrong because the AI is bad or because
shields are invented.

## What was answered

- **H1 — faction stance encoding.** The column is the signed scale `ToStance` assumed, so it needed
  no fix and the cross-faction guard was deleted. Done 2026-08-11.
- **R1 — the range decay curve.** The BioTech Needler Shotgun resolves to full damage to 7m and
  17.5 at 70m, exactly `Range × DamageDecayRangefrac` and `DamagePerRound × MinDamageFrac`, and R2
  measured the curve landing on real shots. Done 2026-08-11. [SDBUtils](../../UdpHosts/GameServer/StaticDB/SDBUtils.cs),
  [DamageFalloff.cs](../../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs).
- **`dbitems::Battleframe` shields.** 5 of 1676 rows carry a non-zero `base_shields`, so build 1962
  had no shields; the recharge pair is real at 150/sec and 10000ms. Done 2026-08-11, numbers read
  in [Capture Replay](../In-Game-Tests/Capture-Replay.html).
- The placeholders were replaced: recharge pair now the shipped values, pool kept non-zero on
  purpose as a divergence — see [HardcodedCharacterData.cs](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs)
  and issue [DATA-1](../gaps/data.md).

**M1 is done, 2026-08-11.** All three questions answered in one afternoon at the client, and none
of them needed the model rewritten: the stance column is the signed scale, the decay curve reads
its columns the way `Resolve` assumed, and shields were never live in build 1962. The one number
still knowingly wrong is the shield pool, kept non-zero on purpose and tracked as [DATA-1](../gaps/data.md).

Exit met: no combat number in the server is an unconfirmed guess, and the one that remains is
written down as deliberate.

What's left in the R series is agreement rather than confirmation — R4 asks whether the *client*
computes the same falloff the server does, which can't invalidate the model, only reveal a second
one. It doesn't hold M2 up.
