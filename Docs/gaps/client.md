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
