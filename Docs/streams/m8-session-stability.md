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

## What landed, 2026-08-14

Code complete and unverified in game. [L1–L6](../In-Game-Tests/Reliability.md) are the check, and L1
is the exit condition.

| Piece | Where |
|-------|-------|
| Holding a reliable packet until it's acked, and encoding the resend | [RetransmitQueue.cs](../../UdpHosts/GameServer/RetransmitQueue.cs) |
| Comparing sequence numbers that wrap | [Sequence.cs](../../UdpHosts/GameServer/Packets/Sequence.cs) |
| Tracking on send, resending on the network tick | `Channel.Send`, `Channel.SendOverdue` |
| Retiring what an ack covers | `NetworkClient.Control_PacketAvailable` |
| Reading retail's own numbers off the capture | [CaptureReplay `--transport`](../../Tools/CaptureReplay/README.md) |

**Nothing here is a guessed constant, and that was the point of doing the measurement first.** The
2016 capture holds 24 resent packets across 456619 sub-packets, and reading them settled every
number the implementation needed:

| Question | What the capture says |
|----------|----------------------|
| How long before resending? | 322 to 665ms after the original, median 452ms, on a link whose round trip measured about 165ms. PIN waits 450ms |
| What resend count goes in the header? | 3, on all 24, in both directions and on both reliable channels. Not one carries 1 or 2 despite every one being a first resend, so the field reads as a marker at its top value rather than a counter |
| Are the resent bytes the same bytes? | Yes. 15 of the 24 have their original in the capture as well, and all 15 are byte-identical to it once the XOR is undone |
| Is an ack cumulative or per packet? | Cumulative. `NextSeqNum` is `AckForNum + 1` on 36753 of 36759 acks, and the client acked only 60% of the server's reliable packets while the session needed 24 resends, which is only possible if an ack covers the run behind it |

The one number retail can't answer is how many times to try, because the capture holds no sequence
resent twice. PIN stops at three, which is where the header's two-bit resend field stops counting,
and gives up loudly: a dropped packet is the only event in the server that leaves a client's copy of
something permanently wrong, so it logs at Warning rather than Debug.

**The inbound half turned out to be the half with a live bug in it.** `Channel` decoded a resend
correctly and then handed it to the controller anyway, so a client resending something PIN had
already processed ran that message twice. It now re-acks and drops the duplicate, which is also what
stops the client resending it a third time. Alongside that, a resent fragment arriving inside a
split run used to hit `SortedDictionary.Add` on a key already there — an exception on the shard
thread, which is exactly how [NET-21](../gaps/network.md#net-21) took the whole server down.

**One defect came out of the read and is recorded rather than fixed.** PIN acks the highest inbound
sequence it has seen, not the highest with no gap behind it, so a lost client packet is reported back
to the client as received and never resent ([NET-25](../gaps/network.md#net-25)). That is NET-1's own
failure on the inbound side and NET-1's fix does nothing for it. Holding the ack at the gap is small;
deciding what to do when the gap never fills is not, and the capture records retail's client acking
but never retail's server recovering, so the giving-up half has nothing to calibrate against. A wrong
answer stalls the channel for the rest of the session, which is worse than losing one message.

**What this might close beyond itself is [NET-24](../gaps/network.md#net-24)**, the finished thumper
the client never removes. That entry's code read found nothing wrong with the scope-out message and
moved the suspicion to the channel it goes out on, sent once and never repeated. If that reading is
right, L6 comes back at zero without anything else changing. If it comes back still climbing, the
theory is retired, which is worth as much.

---

## Keeping this current

A milestone is done when its exit criterion has been seen in game, not when the code compiles.
Move the check into [Test Register](../TEST-REGISTER.md) as the work lands and record the result
there. When a milestone turns out to be two milestones, split it in [PROGRESS.md](../PROGRESS.md)
rather than quietly widening it, and when something in the Deferred list becomes necessary, move it
up with the reason it changed.
