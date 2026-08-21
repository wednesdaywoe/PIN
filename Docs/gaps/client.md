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
([Transport-And-Lifecycle T4–T8](../../Game Testing/Transport-And-Lifecycle.html)) before being caught
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
[Transport-And-Lifecycle T1–T3](../../Game Testing/Transport-And-Lifecycle.html), all passing.

The guard doesn't survive a Steam file-verification pass — it reverts the patched exe — so this
has to be reapplied after any Steam-initiated verify or update. Worth a line in
[Session Setup](../../Game Testing/Session-Setup.html) if it isn't there already.

<a id="client-3"></a>

### CLIENT-3 — Some creature models don't render along the orientation the server sends [x] FIXED &amp; VERIFIED 2026-08-15

A Melded Aranha (528) engaged at melee range stands and attacks about 90° off from the player it is
attacking. Confirmed as a model property rather than a server defect on 2026-08-13, by the only test
that can separate the two: a Chosen Fiend (1196) and an Aranha spawned in the same place, engaging
the same player. The Fiend faces the player correctly. The Aranha does not. Both got their
orientation from the same three lines of `Facing.Towards`, and the `opens fire` log line records the
bearing each was told to stand at, so "the server sent different values" is ruled out rather than
assumed.

What that leaves is the models' own forward axes. `Facing.Towards` builds a yaw around +X because
that is what [N1](../../Game Testing/NPC-Combat.html) confirmed — on a Chosen, a humanoid rig. Nothing
said that generalises to a creature rig, and it doesn't.

**The server has no way to know.** Nothing PIN can read carries a per-model facing offset:
`dbcharacter::Monster` has no such column, and `PoseType` — the record that would be the natural home
— carries only physics height, radius, mass and collision ids. The axis lives in the model asset,
which the client loads and the server never sees. So a fix means either a hand-built table of
per-monster yaw offsets, which is invention of exactly the kind
[DATA-10](data.md#data-10) already tracks too much of, or reading the offset out of the asset DB,
which is unexplored.

Left open and unfixed on purpose. It is cosmetic: the shot direction is computed from live positions
and never from the rendered facing, so a sideways Aranha hits exactly as hard as a forward-facing one
(49 a swing at 4m, straight off the 2026-08-13 log). Worth revisiting if a second creature turns up
wrong in a *different* direction, since two data points would say whether the offsets are per-model
or whether every non-humanoid shares one.

**Still present as of 2026-08-14, and the real unknown is coverage, not the Aranha.** Only two
creature types have ever stood in a PIN session — the Chosen Fiend (1196, faces correctly) and the
Melded Aranha (528, 90° off) — so the sample is one humanoid rig and one creature rig, and the
creature rig is wrong. `dbcharacter::Monster` has ~3,100 more rows nobody has seen render. There is
no way to know how many share this until they are spawned, which makes M7's wave authoring the next
place this bites: every creature type a wave introduces should get a ten-second facing check when it
first appears, because that observation is free during a sitting and unrecoverable after it.

Not to be confused with the Aranha's `PosetypeId` of 0, which sounds alarming and isn't:
`GetCharacterPoseAsset` already falls back to the visual record's `HitboxCollisionId` for those, and
the session log has no `No suitable collisionId found` warnings.

**Solved from the retail capture on 2026-08-15, and everything above about a per-model axis was the
wrong frame.** The third route nobody had tried — read what the real server sent — settled it in one
run: `CaptureReplay --facing` opens the capture's `RoutedMultipleMessage` envelopes (where every
remote pose actually travels; none are standalone) and compares each entity's sent orientation with
its travel and its aim. Every rig retail ever oriented sits at exactly **−90° under PIN's +X-forward
reading** — 40+ monster types, walkers with spreads under 20°, and, decisively, the session's remote
*players*, whose quaternions are client-authored and therefore state the client's own convention.
There is no per-model table, in retail or anywhere: **the client's local forward is +Y, and PIN's
+X convention was 90° wrong for every character it oriented.**

Why only the Aranha showed it: a humanoid's rendered body tracks the aim vector — a plain direction,
convention-free, which PIN always sent correctly — so the Chosen masked the bad quaternion and N1's
"+X confirmed" was really confirming the aim path. A creature rig follows the quaternion, and the
sideways Aranha was the one honest witness. The coverage worry above dissolves with the cause: the
~3,100 unrendered monsters were all being sent the same wrong convention, and now all get the same
right one.

The fix is one constant: `Facing.Towards` yaws a quarter turn short of the bearing
(`ForwardAxisOffset`, [NpcCombat.cs](../../UdpHosts/GameServer/Systems/AI/NpcCombat.cs)), which is
the convention player orientations already arrive in, so every consumer — muzzle origin, physics
pose, the wire — now treats manufactured and client-authored orientations alike. Pinned by
[FacingTests](../../Tests/GameServer.Tests/AI/FacingTests.cs).

**Verified in game the same day, twice over.** The tester's Aranha square up and attack head-on.
And the watchtower's NPC-owned debug thumper had already called its own wave by login, so two
sappers were standing at it — both facing the machine correctly, which checks the fixed convention
against a non-character objective nobody set out to test.

One landmine found on the way, worth its own flag: AeroMessages' `QuantisedFloat` float conversion
doesn't invert its own quantise — positives come back mirrored. PIN only ever *encodes* on the live
path, so nothing in the server is currently wrong, but any future inbound read of a quantised field
(or capture analysis, which is how it surfaced) must decode by the encoder's convention, as
`FacingReport.Dequantise` does.

### CLIENT-4 — Some held resources never appear in the inventory panel [ ] open, partly walked back

Found 2026-08-21 on the [CRAFT2](../../Game%20Testing/Crafting.html) pass. The build spent 15
Crystite, 1 Chitin Fibers and 1 Copper Wiring, and the server's counts moved correctly for all
three — the character's resource kinds went 7 to 6 as one stack emptied out. **The tester saw the
deduction toasts for all three, but could not find the two crafting materials in the panel**, and
read it at the time as the panel only ever drawing Crystite and Red Bean tokens.

**That reading was too strong, and CRAFT2b walked it back the same day.** On the class-line test the
tester did see Iron Bars, Tungsten Bars and Copper Wiring listed, so refined materials are not
undrawn. The tester then sharpened the claim to the tier below: **raw resources — Iron Ore,
Petrochemical and the like — appear to have no display at all.**

> **FALSIFIED 2026-08-21. Iron Ore draws in the ordinary inventory, with its icon and its 24 units,
> seen on screen by the tester.** Everything below this line about the raw tier having "nowhere to be
> drawn" is wrong, and it was wrong before it was written — `Inventory.lua:3062` unwraps every
> resource entry into a `.raw` and a `.refined` side and pushes **both** into the list it draws.
> Nothing about the raw/refined split was ever the dividing line.
>
> What the shipped panel really drops is **whatever `Player.GetInventory()` omits**, which is a
> different set: the melded biomaterials (77343, 77344), reachable only through
> `Player.GetInventoryItemsOfType(15)`. See [client-ui UI1](../streams/client-ui.md) for the
> measurement and [MatList](../../Client/Addons/MatList) for a list that merges both.
>
> The CRAFT3 constraint below is void. An economy priced in raw resources hands the player materials
> that display today.

**The tester then supplied the answer from memory, and the client confirms it: raw resources were
never shown in the inventory. They were shown in the molecular printer, and the printer is gone.**
During the thumping work iron arrived as a toast and never appeared as a value anywhere — which is
correct behaviour for this build, not a bug.

**The printer's panel is still shipped, and it is an empty husk.**
`system/gui/components/MainUI/Panels/FabTest/FabTest.lua` is 528 lines, 1,056 bytes, and **zero
non-whitespace characters** — the file was blanked, not deleted. `FabTest.xml` keeps the component
declaration and about a hundred blank lines where the layout used to be. Red 5 emptied the crafting
UI and shipped the shell. Grepping all of `system/gui` for "printer", "molecular" or "fabrication"
returns nothing else at all.

So there is no display route for the raw tier, and no panel to fix — there is a named, empty slot
where one would go. This is the same cull [CRAFT1](../../Game%20Testing/Crafting.html) found from the
binary side, where 748 exported `Game.*` names contain no fabrication sender.

**The category tree does not explain it, which rules out a data fix.** `dbitems::Resource_Types` is a
parent tree and the inventory panel filters on subtype **15** (`lib_SubTypeIds.lua`,
`Resource = 15`), which is **"Crafting Components"**. Both tiers hang under it: refined at 128 Metals
/ 131 Biomaterials / 3614 Electronics directly beneath 15, raw at 3288 Raw Metals beneath **149 "Raw
Resource"** beneath 15, with a **148 "Refined Resource"** node alongside. The ores are classified
correctly. They have nowhere to be drawn.

**This is a hard constraint on [CRAFT3](../../Game%20Testing/Crafting.html), not a bug to fix
afterwards.** An economy priced in raw resources gives the player materials they cannot see, count or
plan with. An economy priced in refined materials — Iron Bars, Chitin Fibers, Copper Wiring — uses
items that already draw in the ordinary inventory today, confirmed in game. Either build the panel
or price the economy where the display already works.

This is not a delivery gap: `InventoryUpdate` carries the rows and the toasts prove they arrive.

**A concrete sub-case did come out of CRAFT2b and is worth its own line.** The crafted output,
Cryogenic Recharger I (item 81626), draws in the panel with **no name on the row**. The name text is
not missing from the data — `name_id` 177438 resolves to "Cryogenic Recharger I^Q" in all six
languages. What the item does not have is an icon: `web_icon_id` is **0**, where every item that
displays normally carries one (Meteor Strike 231564, Iron Bars 263225, Heavy Machine Gun 441248).
An iconless row is the first suspect for a row that draws blank. The `^Q` suffix marks it as
pre-1.6 content, the same stratum as the `^CY` materials.

It is also the first item this character has ever held outside Gear: `dbg_inventory` reads
`[Gear 67, Bag 2]`, and the server had to guess the sub-inventory, logging
`Unknown InventoryType for ItemType CraftingComponent, defaulting to Bag`. **There is no way to
check that guess against retail** — the 2016 capture contains no crafting component at all, in a
356-entry inventory.

`system/gui` is loose Lua and names what the panel binds, so this is readable rather than
guessable.

**It blocks nothing yet and it blocks a lot soon.** CRAFT2 passed without it, because the toasts
and the server log carried the evidence. But CRAFT3 chooses a resource economy and CRAFT4 prices
recipes against it, and a player cannot manage materials they cannot see — a thumper payout the
inventory won't show is a loop with its middle missing. Settle it before CRAFT4, and treat the
answer as a constraint on the CRAFT3 decision rather than a bug to fix afterwards.
