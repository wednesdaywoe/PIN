---
project: pin
kind: gap-detail
title: "Issue Register — Method Notes"
relates:
  - ../ISSUE-REGISTER.md
---

# Method Notes

**What belongs here vs. in [PROGRESS.md](../PROGRESS.md).** The progress ledger tracks *unbuilt*
features — milestones with an exit criterion nobody's hit yet. This register tracks *live* code or
data that runs today and runs on a guess, a hardcode, or a confirmed-wrong value. The two overlap
at the edges on purpose: a milestone's headline gap (M4's `nodeType = 20`, M8's missing retransmit
queue) gets one line in both places, because it's simultaneously "not built" and "actively wrong
right now." Don't duplicate the full write-up — one side gets the narrative, the other gets a
cross-reference.

**How an entry gets found.** Mostly by reading, not by running anything: a code comment that says
`// TODO` or names a hardcoded stand-in, a divergence noticed while cross-referencing a live
capture against an SDB column, or a test in the [Test Register](../../Game Testing/index.html) that fails
and turns out to name a real defect rather than a not-yet-implemented feature. The
[Capture Replay](../../Game Testing/Capture-Replay.html) tool is the main way "what did the real
server actually send" gets checked against "what does PIN send" without a trip to the game
machine.

**Severity is implicit in status, not a separate field.** `[ ]` open means it's wrong today and
nobody's decided when to fix it. `[~]` means either a fix shipped and hasn't been confirmed at the
client yet, or the defect itself needs a client session to even confirm — both read the same from
outside: don't trust this number until someone plays it. `[x]` means closed, and every `[x]` here
carries the guard that closed it (a code fix, a launch option, a binary patch) rather than just
disappearing — see [CLIENT-1](client.md#client-1) for what "closed but not fully certain" looks
like in practice, and [DATA-1](data.md#data-1) for a divergence that's closed *as* a documented
trade-off rather than closed by matching the original.

**An entry never just vanishes.** If something recorded here turns out to be a non-issue (the
guess was actually right, the divergence doesn't matter), it still gets a line explaining why it's
being dropped, not a silent deletion — otherwise the next session re-discovers the same question
from scratch.
