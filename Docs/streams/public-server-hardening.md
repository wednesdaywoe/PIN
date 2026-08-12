---
project: pin
kind: stream
title: "Appendix: What a Public Server Would Need"
relates:
  - ../PROGRESS.md
---

# Appendix: What a Public Server Would Need

None of this is scheduled and none of it should start before the slice closes. It's written down
because every milestone above assumes one trusted player on localhost, and it's easy to lose track
of how much the server gets away with because of that. A server that twenty strangers can connect
to is a different project from the one in the milestones, sharing maybe half its work.

## There is no identity

`AccountsController.Login` returns the same hardcoded account id to anyone who asks, and
[NetworkClient.cs:223](../../UdpHosts/GameServer/NetworkClient.cs#L223) takes the character guid
off the wire and believes it. Any client can claim to be any character. What's needed is real
accounts, a session token minted at web login, and the game server checking that a socket's
claimed character belongs to that session before `Player.Login` runs.

This is the biggest item here and the only one with no existing code to build from. Everything
else on this list is hardening something that already works.

## Nothing the client sends is checked

Movement is client-authoritative, weapon fire starts with a client message, and `PRNG.Spread`
seeds off the client's clock. That's fine when the client is you. With strangers it needs speed
and teleport bounds, fire rate ceilings, ammo accounting, and a server-side spread seed. Full
server authority over movement is a rewrite and shouldn't be the first move; bounds-checking what
arrives catches most of it for a fraction of the work. M4's placement trust gap (see
[m4-thump-placement.md](m4-thump-placement.md)) is the same problem in miniature.

## Two of these are already milestones

M8 is the one that blocks internet play outright, and its reasoning doesn't change here, it just
stops being optional. M6 does change: a public server can't have "no RIN" as a supported mode, so
the decision at the front of that milestone gets made for you, along with backups and a migration
story that a single-player server never needed.

## Shape

| Today | What a public server needs |
|-------|----------------------------|
| One process hosts one `ZoneId`, and MatrixServer answers `KISS` with a hardcoded `25001` | Something that routes a client to the right process for the right zone. Matrix can't currently say anything else |
| `_maxPlayersPerShard = 64`, gameplay single-threaded per shard | The cap is probably fine. It's never been tested above one, and scope-in batching, change flush, and the 16-bit `EntityRefMap` are where it would show |
| `Shard.RunThread` spins on `Thread.Yield()` with no sleep | A paced timestep. Every shard pegs a core right now whether or not anyone is in it, so idle zones cost the same as busy ones |
| `CurrentShortTime` wraps every ~65 seconds | Already a known source of bugs at one player (tracked as [NET-2](../gaps/network.md)). More entities and more players is more chances to land on it |
| ASP.NET dev certs and `localhost` in `firefall.ini` | Real certs, real DNS, and finding out how much the client actually validates |

## Operations

Twelve HTTP/HTTPS port pairs and two UDP listeners facing the internet, with no rate limiting
anywhere and UDP being what it is. `IsBanned` is hardcoded false and permissions come from
`HardcodedCharacterData`, so bans, reports, chat moderation and deciding who's allowed to run
commands are all greenfield. The Seq container in [compose.yaml](../../docker/compose.yaml) would
have to become real monitoring, and the `.bat` launchers a deploy.

## Distribution

Every player needs Firefall installed, a patched `FirefallClient.exe` and a hand-edited ini.
Assets stream from WebAsset, which costs nothing against a local install and becomes a bandwidth
bill plus a rights question against a public one, since they aren't ours to hand out. Requiring
players to bring their own install is what the README already does and where this usually lands.

## Rough order

Reliability, then identity, then persistence, then validating client input, then topology and ops.
The first and third are M8 and M6 and can't move. Identity is the one to think about early, since
"which character is this socket" reaches into login, persistence and every command that trusts a
caller.
