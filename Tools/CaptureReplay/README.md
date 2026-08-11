# CaptureReplay

Decodes a Firefall packet capture through PIN's own wire format, so questions about what the real
server sent can be read off a recording instead of guessed at and re-tested in the client.

The captures live in [themeldingwars/Documentation](https://github.com/themeldingwars/Documentation)
under `Captures/` — three Wireshark recordings of live sessions (2014 build 1802, 2015 build 1869,
2016) plus a Fiddler `.saz` of the 1869 web APIs. The UDP traffic is not encrypted. PIN never
implemented decryption because there was never any to implement.

## Usage

```
dotnet run --project Tools/CaptureReplay -- <capture.pcapng[.gz]> [options]
```

| Option | Effect |
|--------|--------|
| `--controller <name\|id>` | Filter to one controller, by `Enums.GSS.Controllers` name or number |
| `--message <id>` | Filter to one message id |
| `--entity <0xHEX>` | Filter to one entity |
| `--direction <c2s\|s2c>` | Filter to one side of the conversation |
| `--dump <n>` | Field-level detail for the first n matches (defaults to 10 when filtering) |
| `--top <n>` | Rows in the summary histogram (default 25) |
| `--server <ip>` | Override server detection |
| `--no-deserialize` | Framing and histogram only |

With no filter it prints framing health, the channel mix, a `(controller, message)` histogram, and
the pairs that have no AeroMessages definition.

```
dotnet run --project Tools/CaptureReplay -- "2016-11-15 - Gameplay.pcapng" \
    --controller Character_LocalEffectsController --dump 4
```

## How it reads a datagram

Every step reuses the definition PIN already runs on, so the tool cannot drift from the server:

| Layer | Source |
|-------|--------|
| 4-byte socket id, then a chain of framed sub-packets | [GameServer.cs](../../UdpHosts/GameServer/GameServer.cs) `Receive` |
| Header bit layout — 2b channel, 2b resend, 1b split, 11b length | [GamePacketHeader.cs](../../UdpHosts/GameServer/Packets/GamePacketHeader.cs) |
| Sequence number on every channel except Control | [Channel.cs](../../UdpHosts/GameServer/Channel.cs) |
| Resend de-obfuscation, XOR by `[0xFF, 0xAA, 0xCC]` | [Channel.cs](../../UdpHosts/GameServer/Channel.cs) |
| Split reassembly — concatenate by sequence until a non-split packet | [Channel.cs](../../UdpHosts/GameServer/Channel.cs) |
| Controller, 7-byte entity id, message id | [NetworkClient.cs](../../UdpHosts/GameServer/NetworkClient.cs) `GSS_PacketAvailable` |
| `(controller, message)` → message type | `AeroMessageId` attributes on AeroMessages, read by reflection |

The header's 11-bit length counts its own two bytes, which is the detail that makes a naive parser
report zero valid packets.

Controller and view types are delta-encoded — a keyframe packs every field, an update packs only
what changed behind a leading bitmask. The tool tries `Unpack` first and retries as
`UnpackChanges` before calling a body undecodable.

A read only counts when it consumes the body exactly. Not throwing is not the same as being
right: handed a body from a different client version, Aero will read plausible garbage and stop
early, which is what makes the 2014 and 2015 numbers below honest.

Because a capture holds both halves of the conversation, direction picks the id space:
client→server resolves against `MsgSrc.Command`, server→client against `MsgSrc.Message`. The
server is whichever endpoint received the `POKE`.

## What decodes today

| Capture | Datagrams | Framing failures | GSS messages | Matched a definition | Deserialized |
|---------|-----------|------------------|--------------|----------------------|--------------|
| 2014-09-19 · 1802 | 11,895 | 0 | 14,995 | 88.6% | 11,002 |
| 2015-05-02 · 1869 | 30,686 | 0 | 38,375 | 89.2% | 28,243 |
| 2016-11-15 | 309,562 | 0 | 406,890 | **99.8%** | **405,888** |

Framing is exact on all three: every declared length lands on the next header for the whole
session, across ~350k datagrams.

**Use the 2016 capture.** AeroMessages describes V66, and only the 2016 recording is from that
era — it resolves 99.8% of its messages and all but 239 of them deserialize. The 2014 and 2015
recordings frame perfectly but their message ids have drifted, so a name the tool prints for those
two is a guess against the wrong version, and roughly a quarter of their bodies fail the
exact-consumption check as a result. They are useful for framing-level and Control/Matrix
questions, not for reading message contents.

## Known gaps

- `Character_LocalEffectsController` message 4 does not deserialize under either the full or the
  delta layout. Message 1 does, and is the one that carries the owner-private status effect slots.
- 763 messages in the 2016 capture land on `(controller, message)` pairs AeroMessages has no type
  for, and 239 more match a type but will not read cleanly. The summary lists the former with
  their body sizes; both sets are candidates for fixing AeroMessages definitions.
- Split reassembly follows `Channel.cs`, which keys fragments by sequence number and assumes a run
  is never interleaved with another on the same channel. That holds in these captures.
