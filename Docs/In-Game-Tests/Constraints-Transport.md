---
project: pin
kind: test-stream
title: "Constraints Transport (W1)"
relates:
  - ../TEST-REGISTER.md
  - ../streams/battleframe-constraints.md
---

# Constraints Transport

Part of the [in-game test queue](README.md). Setup and launch:
[Session Setup](Session-Setup.md).

The check on route 2 of [Battleframe Constraints](../streams/battleframe-constraints.md): sending
capacity numbers to the garage by extending the `garage_slots` web payload. That route only exists
if the client's native JSON parser passes keys it has never heard of through to Lua instead of
parsing against a known schema and dropping the rest. The parser is engine code, so no source read
settles it; one logged response does.

How it was instrumented, for the next probe like it: the server sent three made-up keys on the
Firecat garage slot, one per JSON shape (`pin_probe: 1337`, `pin_probe_block: {mass, power, cpu}`,
`pin_probe_list: [951, 952, 953]`), and the client's `WebCache.lua` — the one component every web
response passes through — dumped any `garage_slots` response to `console.log`. Both ends were
removed after the run; the client file went back to its original from a `.pinbak` copy, and the
probe properties were deleted from
[GarageSlots.cs](../../WebHosts/WebHost.ClientApi/Characters/Models/GarageSlots.cs) and
[AccountsController.cs](../../WebHosts/WebHost.ClientApi/Accounts/AccountsController.cs).

No garage visit is needed for a probe like this: the durability HUD subscribes to `garage_slots`
with auto-request at `OnPlayerReady`, so world entry fires the fetch.

---

## [x] W1: Do unknown JSON keys in garage_slots survive into Lua?

Passed 2026-08-14, on the second launch. **They survive, in all three shapes.** The logged table
carried `pin_probe=1337`, `pin_probe_block={power=222, mass=111, cpu=333}` and
`pin_probe_list={1=951, 2=952, 3=953}` alongside the slot's known fields, over plain HTTP with
status 200. **Route 2 is open**: capacities can ride the existing payload, flat or nested, no
schema on the client side to satisfy.

Two incidental answers off the same line:

- A JSON `null` doesn't become a Lua `nil` value under its key — the key is dropped entirely. The
  crafting-station slot sent `pin_probe_block: null` and the logged table has no such key. So an
  absent capacity and a null capacity are indistinguishable in Lua; send a value or send nothing.
- Numbers arrive as numbers, strings as strings, booleans as booleans. No stringly-typed layer to
  guard against.

The first launch produced no line and a lesson instead: in this client, the base Lua libraries are
opt-in per component. `WebCache.lua` requires `table` explicitly, and the probe's `unicode.find`
call crashed with `attempt to index a nil value (global 'unicode')` until the patch also said
`require "unicode"`. Any future instrumentation of a component should check its `require` lines
before borrowing a library that other files seem to use freely. The crash also took out every web
response for that session — `OnUrlResponse` died before notifying subscribers — which is the
blast radius to expect when a patch in that file goes wrong.
