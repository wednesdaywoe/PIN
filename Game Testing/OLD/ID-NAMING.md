# Stream entry ID naming — convention and migration map

## The rule
Each stream's entry-ID prefix is derived from its own filename slug, not an
arbitrary letter. Format: `PREFIX-N` for an entry, `prefix-N-step` for its
checkbox `data-k`. `PREFIX` is 1–2 words, uppercase, hyphenated, unique.

This is why `D3` was ambiguous: both Damage-Loop and Damage-Decay wanted `D`.
Under the new rule they're `DMG-LOOP-3` and `DMG-DECAY-3` — distinct on sight,
and the prefix tells you the file without a lookup.

Sub-parts keep the dot-notation as a trailing hyphen: `D7·A` → `DMG-LOOP-7-A`.
Composite entries that reference two streams keep the `+` join, each side
renamed independently: `E4 + K4` → `ENV-4 + KILL-REWARDS-4`.

## Full map (every stream, now that all files are in hand)

| Old | New prefix        | Stream file                    |
|-----|--------------------| --------------------------------|
| D   | `DMG-LOOP`         | Damage-Loop.html               |
| R   | `DMG-DECAY`        | Damage-Decay.html              |
| E   | `ENV`              | Environment.html               |
| H   | `HOSTILITY`        | Hostility.html                 |
| S   | `THUMP-PLACE`      | Thump-Placement.html           |
| F   | `THUMPER-DEF`      | Thumper-Defence.html           |
| T   | `TRANSPORT`        | Transport-And-Lifecycle.html   |
| X   | `SOLID-WORLD`      | solid-world.html               |
| K   | `KILL-REWARDS`     | Kill-Rewards.html              |
| B   | `DEATH-RESPAWN`    | Death-And-Respawn.html         |
| V   | `DEPLOY-VEH`       | Deployables-And-Vehicles.html  |
| I   | `INVENTORY`        | Inventory.html                 |
| N   | `NPC-COMBAT`       | NPC-Combat.html                |
| C   | `PERSIST`          | Persistence.html               |
| P   | `PREDICT-SWEEP`    | Prediction-Sweep.html          |
| L   | `RELIABILITY`      | Reliability.html               |
| G   | `RESOURCE-PAYOUT`  | Resource-Payout.html           |
| W   | `CONSTRAINTS`      | Constraints-Transport.html     |
| D5* | `CHARGE-CAM`       | Charge-Camera.html             |

**Every letter from the first pass is now resolved.** The four I left untouched
last time turned out to be:
- `G` → Resource-Payout (I'd guessed Charge-Camera — wrong)
- `P` → Prediction-Sweep (I'd guessed Resource-Payout — wrong)
- `V` → Deployables-And-Vehicles (I'd guessed Persistence — wrong)
- `B`, `C`, `L` → Death-And-Respawn, Persistence, Reliability (all guessed correctly, but weren't applied on a guess)

Worth calling out precisely because three of my four guesses last time would
have been wrong if I'd acted on them — this is the case for why the tool
didn't rename anything it wasn't sure about.

**Charge-Camera is the one irregular case.** It inherited the number `5` from
Damage-Loop's own numbering when it split out into its own investigation
(see the note still in `Damage-Loop.html`: "D5 was the fourth entry in this
batch... it grew into an investigation of its own"). Rather than renumber it
to `CHARGE-CAM-1`, I kept the `5` — `CHARGE-CAM-5`, with sub-parts
`CHARGE-CAM-5a` through `CHARGE-CAM-5h` — so anything that already says "D5"
in commit messages or your own memory still lines up. If you'd rather it
renumber cleanly from 1, that's a one-line change to `rename2.py`'s D5
handling and I can push it through everywhere it's cited.

## Deliberately not touched: the M-series and the issue register

`Reliability.html` and a few other pages cite `M1`–`M8` (linking to
`streams/m8-session-stability.md` etc.) and `DATA-N` / `NET-N` (linking to
`ISSUE-REGISTER.md`). Both are already a separate, working namespace — a
roadmap-milestone system and a defect-tracker system, respectively — neither
one is a stream+task identity, so they don't carry the "which stream is this"
ambiguity `D3` had. Leaving them alone is a decision, not an oversight.

## Save-data note, still applies

`data-k` values changed for every renamed entry, so ticked boxes on your
existing pages will read as unticked the first time you open the renamed
version. Say the word and I'll write the one-time localStorage migration
script (maps old key → new key, runs once per page, safe to include
permanently).

## Still open

- `sitting-plan.html`'s and `solid-world.html`'s own `Block 0`–`Block 9`
  wrapper labels, and their internal 2-part `data-k` keys (`b0-1`, `x1-1`,
  `s0-1`) — cosmetic, low priority, said so last time and it still stands.
- Anything outside this upload — `README.md`, `ISSUE-REGISTER.md`,
  `streams/*.md`, `gaps/*.md`, `Design/Combat-Scale.md` — still has the old
  IDs in it. The rename scripts (`rename.py`, `rename2.py`, both included)
  are mechanical and reusable; point me at those files and I'll run the same
  pass.

