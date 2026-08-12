---
project: pin
kind: stream
title: "M8: Session Stability"
relates:
  - ../PROGRESS.md
---

# M8: Session Stability

Outbound reliability isn't implemented. `Control_PacketAvailable` logs client acks and discards
them, and there's no retransmit queue, so "reliable" currently means the server acks what the
client sends rather than resending what the client missed. Tracked as [NET-1](../gaps/network.md).

On a LAN with no loss this is invisible. Over the internet a dropped entity state message means the
client's copy of that entity is permanently wrong, which shows up as things that are invisible,
immortal, or standing somewhere they aren't. That's not a bug anyone can debug from the symptom, so
it needs to land before the slice gets shown to anyone playing over a real connection.

It's listed last because it blocks nothing above it, not because it's optional.

| Work | Where |
|------|-------|
| Track sent reliable messages, retransmit on missing ack | [Channel.cs](../../UdpHosts/GameServer/Channel.cs), `NetworkClient.Control_PacketAvailable` |
| Confirm the inbound resend path, which carries its own TODO about whether it's correct | same, [layer 2](../Architecture/02-networking.md) |
| Test under induced packet loss | needs a client and a way to drop packets |

Exit: play a session with a few percent artificial loss and nothing desyncs.

Medium size and self-contained, but the failure it fixes is only reproducible with a real client on
a lossy link, which makes verification the expensive part.

---

## Keeping this current

A milestone is done when its exit criterion has been seen in game, not when the code compiles.
Move the check into [Test Register](../TEST-REGISTER.md) as the work lands and record the result
there. When a milestone turns out to be two milestones, split it in [PROGRESS.md](../PROGRESS.md)
rather than quietly widening it, and when something in the Deferred list becomes necessary, move it
up with the reason it changed.
