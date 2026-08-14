---
project: pin
kind: test-stream
title: "Reliability Under Loss (L1-L6)"
relates:
  - ../TEST-REGISTER.md
---

# Reliability Under Loss

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M8](../streams/m8-session-stability.md). Until 2026-08-14 "reliable" only meant the
server acked what the client sent; nothing tracked what the server itself sent, so a dropped packet
was gone ([NET-1](../gaps/network.md#net-1)). Now every packet on Matrix and ReliableGss is held
until the client acks it and resent if it isn't.

**This stream is the only one in the queue that has to break the network on purpose.** Everything
else here runs over loopback, where a packet is lost only when a socket buffer overflows, and that
is exactly why the bug survived this long: it costs nothing until it costs a session.

## Inducing loss

Client and server are on the same machine, so all of it goes over `lo`. Drop 5% of the GameServer's
UDP traffic in both directions and leave the web APIs and the handshake on 25000 alone:

```
sudo tc qdisc add dev lo root handle 1: prio
sudo tc qdisc add dev lo parent 1:3 handle 30: netem loss 5%
sudo tc filter add dev lo protocol ip parent 1:0 prio 3 u32 match ip dport 25001 0xffff flowid 1:3
sudo tc filter add dev lo protocol ip parent 1:0 prio 3 u32 match ip sport 25001 0xffff flowid 1:3
```

Check it took, and take it back off afterwards. **The rule survives until it is deleted or the
machine reboots**, so an entry run the next day against a forgotten qdisc measures the wrong thing:

```
tc qdisc show dev lo                      # expect the prio root and the netem child
sudo tc qdisc del dev lo root             # back to normal
```

5% is the figure the milestone was written against. Retail's own session lost far less: the 2016
capture holds 24 resends across 456619 packets, so anything above about 1% is already a worse link
than the recording this was built from.

The log lines this stream reads:

```
grep -a "resending SeqNum"      ~/Games/PIN/logs/GameServer.log
grep -a "has been dropped"      ~/Games/PIN/logs/GameServer.log
grep -a "Resent packet"         ~/Games/PIN/logs/GameServer.log
grep -a "re-acked without"      ~/Games/PIN/logs/GameServer.log
```

Debug level has to be on for the first, third and fourth. The dropped-packet warning is at Warning
because it is the only record anywhere that a client's copy of something is now permanently wrong.

---

## [ ] L1: A session under 5% loss holds together

**M8's exit condition.** Everything else in this file is diagnosis for when this fails.

1. Start the servers and get into the world **before** adding the qdisc, so a login failure can't be
   confused with a reliability one.
2. Add the qdisc as above. Confirm with `tc qdisc show dev lo`.
3. Play for ten minutes and cover the things that scope entities in and out: walk to the basin and
   back so NPCs scope in, fight something ([N14](NPC-Combat.md) has the pack), call a `thumper` and
   collect it, and open the inventory.
4. `sudo tc qdisc del dev lo root`.

Pass: nothing in the world is invisible, immortal, or standing somewhere it isn't. Specifically,
every NPC you shot took damage and died, the thumper appeared and disappeared, and the crystite
count moved.

Fail: any of those. The value of this entry is entirely in *which* one, because each names a
different view that lost a message. Note the entity and roughly when, then read
`grep -a "has been dropped"` for the same window.

## [ ] L2: A resend is accepted rather than resent again

Cheap, log-only, and the single most informative line in the stream. Run it off L1's session.

```
grep -a "resending SeqNum" ~/Games/PIN/logs/GameServer.log | grep -oE "attempt [0-9]" | sort | uniq -c
```

Pass: almost every line reads `attempt 1`. A resend that arrives gets acked, and the ack retires it,
so it is never seen again.

Fail with the counts roughly equal across attempts 1, 2 and 3: **the client is rejecting resends**,
which means the resend encoding is wrong rather than the network being bad. That would be the one
thing in M8 that offline tests can't settle — they prove PIN's encoder and PIN's decoder agree, and
the client is a third party. The suspect is the resend count PIN stamps: retail stamped 3 on all 24
resends in the capture, in both directions, and PIN copies that, but nothing proves the client
requires it.

## [ ] L3: Nothing is abandoned

```
grep -a "has been dropped" ~/Games/PIN/logs/GameServer.log
```

Pass: no output. At 5% loss, four independent copies of a packet all failing is about six in a
million, so a hit here means the loss isn't independent (a buffer overflowing drops a burst, not a
packet) or the client stopped acking altogether.

Fail: record the channel and sequence number. This is a real message the client never received and
never will, and pairing it against what went visibly wrong in L1 is the only way to learn what a
given lost message costs.

## [ ] L4: A resend the client sends is handled once, not twice

The other half of the same mechanism, and the one with the worse failure: a duplicate inbound
message means whatever it asks for happens twice.

1. From L1's session, `grep -a "Resent packet" ~/Games/PIN/logs/GameServer.log` — these are the
   client's resends arriving.
2. `grep -a "re-acked without" ~/Games/PIN/logs/GameServer.log` — these are the ones PIN recognised
   as duplicates and declined to handle again.

Pass: every `Resent packet` line on ReliableGss or Matrix whose sequence number is behind the
channel's high-water mark has a `re-acked without` line beside it. Zero of either is also a pass
here, and means the client never had to resend anything; say so rather than marking it run.

Fail: a `Resent packet` with no matching line, followed by something happening twice in the world.
Two fired shots from one trigger pull is the shape to look for.

## [ ] L5: The queue drains rather than growing

```
grep -a "resending SeqNum" ~/Games/PIN/logs/GameServer.log | grep -oE "[0-9]+ unacked" | sort -n | tail -5
```

Pass: the largest figure is small, tens rather than thousands, and doesn't climb through the
session. Every packet leaves the queue within about 1.4 seconds whatever happens to it, so the
standing size is a measure of the send rate and nothing else.

Fail with a number that grows monotonically: acks aren't retiring anything, and the cumulative
reading is wrong. Compare against the ack coverage the capture recorded (the client acked 60% of the
server's reliable packets and the session still needed 24 resends, which only works if one ack
covers the run behind it).

## [ ] L6: Does the stale thumper still haunt the client?

[NET-24](../gaps/network.md#net-24) is a finished thumper the client leaves standing in the world
forever, asking for keyframes of it every 5.5 seconds. A code read pinned the suspicion on the
scope-out being sent once on a channel that never resent anything, which is what M8 just fixed. So
this entry asks whether M8 closed a live defect or only a theoretical one.

Run this **without** the qdisc, because the question is whether the ordinary case is fixed.

1. Call a `thumper`, let it run its full 7½-minute cycle rather than collecting it early. The split
   matters: only full-cycle thumpers ever left a loop behind.
2. After it finishes, watch for five minutes.

```
grep -a "no longer exists" ~/Games/PIN/logs/GameServer.log | wc -l
```

Pass: zero, or a small handful that stop. [G6](Resource-Payout.md) is the same measurement written
before the retransmit queue existed, so its recorded counts are the baseline: 648 across four
thumpers in one 32-minute sitting.

A count that keeps climbing means the scope-out isn't being lost in transit and NET-24 is something
else. That is a useful answer and should be written into the entry, because it retires the leading
theory rather than leaving it standing.
