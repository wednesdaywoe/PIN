# Resource economy design

Working design for the resource and crafting economy. Targets the 0.6/0.7 model,
not live 1.x.

Status: structure is settled, numbers aren't. Everything below is a decision
unless it's under Open questions.

## The model

This is a quality economy, not a quantity economy.

In most MMO crafting a recipe wants a count. You either have it or you don't, and
the output is fixed. Here every resource stack carries stat values and the
component inherits them, so there are two independent curves. Quantity gates
whether you can build the item at all. Quality gates how good it comes out.

That's the reason the numbers can't be designed in recipe-first order. Recipe
costs only balance quantity. Until quality has a source and a distribution,
there's nothing to balance the other curve against.

Three layers stay separate and are easy to collapse by accident:

The taxonomy is the group, family, resource tree. Progression through it is
specificity, not amount: early nanoprints call for anything in a group and later
stages demand a named resource.

The attribute matrix is which material stat each resource is good at. Each
recipe line reads exactly one attribute off its input, but a resource's own
slot set can carry more than one (see The attribute layer below).

The aspects are Power, Mass, and Cores. Those are the constraint budget on the
finished item, not a property the recipe reads.

## The attribute layer

Resolved from `Crafting-Chain.md`, generated off the build-1962 `clientdb.sd2`.

Two crafting systems ship in that build and they share tables. Blueprint id
tells them apart: ids 75430 to 85611 are the graded system this document
targets, and everything outside that range is the v1.6 rework, a fixed
ingredient list that reads no stats. The lattice discussed later under "What
the build-1962 dump gives us" belongs to the v1.6 rework. Treat the two as
unrelated systems that happen to live in the same database.

### What a resource carries

Every resource carries up to five stats, indexed 1 through 5. The index has a
generic engine name (Purity, Power, Mass, CPU, and a fifth slot that was
authored but never shipped) and a player-facing name set per group, not per
resource:

| Slot | Mineral | Organic | Gas |
|---|---|---|---|
| 1 | Thermal Resistance | Thermal Resistance | Thermal Resistance |
| 2 | Conductivity | Potency | Compressibility |
| 3 | Malleability | Resilience | Volatility |
| 4 | Density | Potential Energy | Potential Energy |
| 5 | Toughness | Toughness | (Gas only has four) |

This settles the Purity discrepancy raised earlier. Slot 1's engine name
stays Purity permanently. Its player-facing label was repointed to Thermal
Resistance. The 0.6.1637 removal killed a refining-yield mechanic that
happened to share a variable with a slot that, on the crafting side, had
already become an ordinary material stat. Same field, two jobs, one name
collision, nothing left to reconcile.

### The sixteen resources

| Resource | Group | Slot(s) | Named propert(y/ies) |
|---|---|---|---|
| Copper | Mineral | 2 | Conductivity |
| Aluminum | Mineral | 3 | Malleability |
| Iron | Mineral | 5 | Toughness |
| Carbon | Mineral | 5 | Toughness |
| Silicate | Mineral | 4 | Density |
| Ceramic | Mineral | 1 | Thermal Resistance |
| Methine | Gas | 3 | Volatility |
| Octine | Gas | 4 | Potential Energy |
| Nitrine | Gas | 2 | Compressibility |
| Radine | Gas | 1 | Thermal Resistance |
| Petrochemical | Organic | 4 | Potential Energy |
| Biopolymer | Organic | 1, 5 | Thermal Resistance, Toughness |
| Xenografts | Organic | 1, 5 | Thermal Resistance, Toughness |
| Toxins | Organic | 2 | Potency |
| Regenics | Organic | 3 | Resilience |
| Anabolics | Organic | 4 | Potential Energy |

Ceramic lost its own material class by 1962, folded into a Hybrids catch-all
that reads only slot 1. Its identity survives, its independence doesn't.

This closes the open question that used to sit below about vector versus
single-dominant attributes. The schema supports up to five slots per
resource. Fourteen of sixteen use exactly one. Biopolymer and Xenografts use
two, Thermal Resistance and Toughness both, which is exactly why the 0.7.1688
patch had them competing against each other on both stats. The two-slot
resources aren't an edge case to design around. They're proof the format was
built for it and the beta only exercised it twice.

### Where the sixteen come from in game

Implemented 2026-08-22. The shipped `dbzonemetadata::ResourceNodeType` table
holds 46 vein types and not one of them pays any of the sixteen; 42 pay ore in
the Raw Metals class, which by 1962 exactly one blueprint consumes. So the vein
types are PIN's own content now, the same way deposit positions already were:
`UdpHosts/GameServer/StaticDB/CustomData/resource_node_type.json` defines a vein
by id, name and payout rows, `SDBInterface.ApplyResourceNodeTypeOverrides` lays
them over the shipped table at startup, and everything downstream — sampler,
geo scan, outpost radar, thumper payout — reads through the ordinary accessors
and cannot tell the difference.

Ids 700 to 715 are the sixteen, one vein per material, each paying its refined
item directly because refining is cut. All sixteen start on the shipped tier-1
gradient, 25–35 at the centre falling to 0–5 at the rim, which is deliberately
flat: tiering is the deposit roll's job, not the vein type's, and per-deposit
`richness` already exists to vary a single vein. `veintype list | show | reload`
reads and re-reads them in game.

An id the client never shipped turned out not to matter. `ObserverView` carries
the vein type id to the client and no surviving Lua reads it; on 2026-10-02 a
thumper on a `deposit add 700` vein ran a full defended cycle, paid 25 Copper
(77703), and the client drew the node and kept the session with no error. The
Copper landed in inventory named and described as a Mineral - Metal crafting
component, with the `^CY` suffix printed literally as "[CY]".

### Refining, before it was cut

The pre-1637 chain is recoverable: `ResourceItem.refines_into` still lists
sixteen live raw-to-refined pairs, one per resource, Raw Copper into
Copper^CY and on through Radine. These are the pre-1.6 material items, not
the Bars the 1962 thumper pays out. See Refining: removed above for why the
mechanic was cut and why it stayed cut.

### The one thing the dump can't give you

Per-instance stat values were never static data. The client read them off a
base-36 string parsed at runtime from the item itself, and the 2016 capture
shows the server had stopped writing that string by the end. There's no
quality distribution to reverse-engineer, from this dump or any other capture
of the same era. The deposit roll and global pool below were always
something to design, not something to recover, and this confirms it rather
than changing it.

## Deposits

The world holds a fixed population of deposit slots. Each occupied slot is one
vein: a type, a quality, and a quantity.

### The roll

A slot doesn't roll its three values independently. It gets a value budget and
spends it across quality and quantity. A vein is deep and mediocre or shallow and
excellent, rarely both.

Independent rolls produce incoherent veins and a lottery. Budget spending
produces a tradeoff, which means both kinds of vein have a use. Deep ones feed
bulk demand at low stages. Shallow rich ones get saved for something that
matters. Neither is strictly better, so both are worth traveling for.

Zone sets the mean budget. The roll adds variance around it. Ranges between
adjacent zones overlap, so a lucky roll in a mid zone occasionally beats an
unlucky one in a hard zone. Without overlap players just go to the highest zone
they can survive and stop looking.

Resource type is weighted by zone, not locked to it. Common in some places, rare
in others. That's what makes the thumper map markers worth reading and gives
trading a reason to exist.

### Slot lifecycle

A slot frees on depletion or on expiry, whichever comes first.

Depletion-driven churn alone is a population trap. It works beautifully at
Firefall's scale, where thousands of players chewed through veins constantly and
the map shifted because it was being eaten. At private-server population the map
freezes, the scan hammer degrades into a one-time lookup, and the dynamic economy
is dynamic in name only.

Expiry is the floor. Churn rate becomes whichever is faster, consumption or the
clock. Busy server, players drive it. Quiet server, the clock does.

Two implementation notes:

Stagger initial lifetimes at world start. Otherwise every slot placed at boot
expires together and the map visibly blinks on a repeating interval.

Expiry yields to an active dig. A vein with a thumper attached doesn't evaporate
under it.

### Refill delay

The delay between a slot freeing and refilling is the knob controlling how much
the game is about travel and searching versus parking. Instant refill means
players camp one spot forever. Slow refill means local supply genuinely runs dry
and you have to move.

## Throughput

Once slot count, mean budget, and refill delay are set, the faucet is defined:

    zone throughput = slots x mean budget / refill time

That gives resource units per hour a zone can produce, which can be held against
the resource costs in the node cost curve to see how many hours a node costs.
That check happens before any recipe gets written.

See claude/node-cost-curve-estimates.md for the sink side. Resource cost there is
a clean multiple of crystite cost at every recovered node, which makes it a usable
anchor.

## Quality bands

Quality snaps into a small number of named bands. Consolidation inside a band is
automatic, free, and invisible. Consolidation across bands never happens
implicitly.

This bounds stack count at resource count times band count instead of leaving it
unbounded, and it fixes the storage problem at the source rather than with a
player-facing tool.

Continuous quality survives on top of it. The band is the bucket, and the stack
carries a real value that's the weighted average of what went in. Adding to a
stack drifts its value, but only within the band, so the drift is bounded and
can't dilute good material with junk.

Band names reuse the item rarity vocabulary the game already has, so a vein can be
described by band instead of a percentage. That makes the scan hammer readable at
a glance and makes the crafting decision sharper. "Do I burn my rare Iron on this"
is a better question than choosing among forty stacks that differ by a couple of
points.

## Blending

Blending is the deliberate operation: combining across bands at a station.

You take a large pile of low-band material plus some good stuff and push them
together into a mid-band stack big enough to print with. The weighted average does
the work. You've raised the floor on your bulk material and permanently spent your
ceiling to do it.

This solves a problem players actually have, which is holding plenty of junk and
one precious stack while the recipe wants decent material in volume. And it has a
real wrong answer, which is what makes it a decision rather than a formality.

Two rules that keep it from breaking the economy:

It's a one-way door. Blended material never separates back out. If it did, the
cost disappears and so does the decision.

The average rounds down or truncates, never up. Rounding up creates a loop where
repeated micro-blends ratchet quality, and quality inflation would undo the entire
deposit design.

Note the danger if these slip. A weighted average looks value-neutral because
total quality times quantity is conserved. It isn't neutral, because it destroys
the top end and blending is always the convenient move. Left unconstrained, every
player's stash converges on the server-wide mean for every resource, quality stops
being an axis, and the whole thing collapses back into a quantity economy. Banded
auto-consolidation is what prevents that: casual merging can't cross a band, so
the top end survives by default and is only spent on purpose.

## Three levers

Quality, quantity, and crystite each do one job and none of them overlap.

Quality decides how good the item is. Quantity decides how fast you get it,
trading against crystite. Crystite sets the floor on pace.

Saying it that plainly matters because the three otherwise compete, and a sink
that competes with another sink is a sink players learn to ignore.

### Quality influence widens with tier

The stat swing between a worst-band input and a best-band input gets larger as
tier goes up. At the bottom of the tree band choice nudges the output. At the top
it's most of the output, so a tier-four component built from junk comes out
genuinely bad and one built from a rare-band vein comes out genuinely excellent.

This is the answer to materials going linear while crystite goes geometric. The
obvious fix would be to bend the material quantity curve upward to match, but that
just makes late-game gathering a longer grind. Widening quality influence instead
makes it a hunt, and the question stops being how much you gathered and becomes
which vein you found. Small quantities at the top of the tree stop being a problem
the moment the choice of material carries the weight.

It also makes the rest of the design pay off. If quality influence were flat, the
vein budget model and the scan hammer would be decoration.

And it paces blending for free. Burning your best material to lift a bulk pile
costs far more at the top of the tree than at the bottom, so blending is naturally
an early and mid game tool and a late game sacrifice. No extra system needed.

### The two sinks trade against each other

Material quantity buys down crystite cost. Spend more material, pay less currency.

The inverse already exists in the shipped data and is worth keeping: a
currency-only path that takes no materials at all and pays for it in build time.

Between them, a player who's rich in currency and poor in material waits longer,
and a player who's rich in material and poor in currency spends material instead.
Both reach the same item. This also gives bulk low-band material a permanent job,
which is exactly what blending produces, so the two systems feed each other.

### One rule that keeps it from collapsing

Quality never buys down crystite.

If high-band material both improved the output and cut the cost, it would be
strictly dominant by a wide margin and every other path would stop existing.
Quality touches the output. Quantity touches the price.

## Refining: removed

Refining is cut. Its outputs move to the thumper, which now produces raw material,
crystite, and the roll for crystite hybrids on completion.

The reasoning: a step earns its place in a loop only if the player can be wrong
about it. Purity was the decision in refining. Once 0.6.1637 removed Purity and
made it flat one-to-one, nothing was left to get right or wrong, so it decayed
into ceremony. Every second spent there was overhead, and overhead in a loop
repeated hundreds of times is the worst kind.

Post-1637 refining wasn't even subtractive. Material came back one-to-one plus
crystite plus a hybrid chance. It was friction, not tax. The one real cost, the
crystite charge to refine hybrids, was itself removed by 1.0. They sanded at it
until only the click was left.

Folding the outputs into the thumper keeps both sinks it was carrying, removes a
step from every gathering loop, and concentrates the payoff on the completion
beat, which the patch notes already treat as the moment that matters.

Blending takes the slot refining occupied in the flow. Same pause between
gathering and crafting, but a choice instead of a formality.

## Crafting stations and place

Crafting stations exist only in major towns. Not outposts, not watchtowers.

A crafting station isn't a UI, it's a place, and its scarcity is the only thing
generating congregation. Players gather in a town because something they need is
only there. Put a station at every outpost and you haven't saved a walk, you've
deleted the town.

The trip is good friction where refining was bad friction, and the difference is
worth stating because it's reusable. Refining was solitary, instant, and produced
nothing but delay. The trip to town costs time and buys presence: it moves you
through the world, collides you with other players, and ends somewhere other
things are available. Friction that only costs time is waste. Friction that costs
time and buys presence is content.

### Capacity sets the rhythm, not distance

How often players return is set mostly by how much they can carry.

Someone who can hold ten digs of material does ten digs and makes one trip.
Stretch the distance and they do fifteen digs first. Someone who can hold two is
back constantly regardless of travel time.

So carrying capacity is the population knob for towns. Travel time only sets what
the trip costs, and pushing it hard on a small server produces long solitary walks
ending in an empty room, which is worse than either extreme.

A full haul should be worth more than one trip's walking. If it isn't, players
optimize into fewer, larger runs and town visits get rare no matter what the
distance is.

### Give the trip more than one purpose

If the station is the only thing there, the trip's a chore. Town should also be
where you retool your loadout, read the resource map, hit vendors, and see who's
around. Pairing the garage with the crafting station is natural, since players
usually arrive holding better components than they left with.

### No portable stations

Never ship a mobile crafting station. Not as a reward, not as a store item, not
one-use. It's a one-way door: once remote crafting exists the town empties
permanently and it can't be reversed without taking something away from players.

Firefall shipped this. The Devil's Due PvP store sold a one-use P.U.C.K., a mobile
tinkering station, for a trivial token cost. That's the erosion, documented.

## What the build-1962 dump gives us

Measured from the generated `clientdb.sd2` tables, build 1962. Read the era
warning at the end of this section before using any of it.

### The material lattice

The 1.x material set isn't a list, it's a lattice. Twelve base materials sit in
three families of four tiers:

| Family | Tier 1 | Tier 2 | Tier 3 | Tier 4 |
|---|---|---|---|---|
| Metal | Iron Bars | Tungsten Bars | Titanium Bars | Uranium Rods |
| Electronic | Copper Wiring | Semiconductors | Crystatic Powercell | Optical Coolers |
| Organic | Chitin Fibers | Carbon Powder | Biopolymer Thread | Crystatic Nucleotides |

Above them sit three crafted chains of four rungs each, and each chain draws on a
different pair of families. Three families give exactly three pairs, so the set is
combinatorially complete with nothing left over:

| Chain | Families | Rung 1 | Rung 2 | Rung 3 | Rung 4 |
|---|---|---|---|---|---|
| A | metal, electronic | Synthetic Ligatures | Adaptive Fibers | Thermionic Transformer | Fusion Motor |
| B | organic, electronic | Artificial Sinews | Twinned Muscle Fibers | Transnucleic Battery | Hyperkeg |
| C | metal, organic | Biosteel Frame | Entropic Nanotubes | Polyphasic Fabric | Superconductive Fuel Cells |

Every rung follows one template: the tier's two base materials, plus one unit of
the rung below. Metal counts double, so a rung that includes metal takes twenty of
it and ten of the other, and chain B with no metal in it takes ten and ten. Build
time is uniform across all twelve.

This matters because it's the same idea as the 0.6.1637 group and family tree,
just wearing different names. The taxonomy being designed here can inherit the
lattice's regularity directly, which is worth doing: it makes recipes predictable
without making them boring, and it means a player who learns one chain has learned
all three.

### Materials grow linearly, crystite grows geometrically

Expanding a finished intermediate all the way down to base units:

| Rung | Chain A and C | Chain B |
|---|---|---|
| 1 | 30 | 20 |
| 2 | 60 | 40 |
| 3 | 90 | 60 |
| 4 | 120 | 80 |

Straight-line growth. Each rung adds one tier's worth and nothing compounds.

Crystite does the opposite. Fitting the crystite cost of each item's recipe ladder
across the 122 items with twelve or more distinct rungs gives a median multiplier
of about 1.20 per rung at a median R-squared of 0.99. That's about as clean a
geometric curve as real shipped data ever gets. A ladder typically runs from a low
rung near 15 crystite to a high rung near 8,000, roughly a 380-fold climb.

So the two sinks diverge hard. By the top of the tree, crystite is doing almost
all the gating and materials are doing almost none. Left alone that undoes the
quality economy from the far end, because late-game players stop caring what they
gather and only farm currency.

The divergence is kept rather than corrected. See Three levers above: quality
influence widens with tier instead of the material quantity curve bending upward,
which keeps materials decisive at the top without turning late-game gathering into
a longer grind.

### Build time tracks crystite with a floor

Across recipes that take both crystite and materials, build time is about 0.22
seconds per crystite at the top of the ladder. Low rungs pay a higher ratio, so
small items are relatively slower per unit of currency, which is the shape of a
minimum-time floor rather than a different curve.

### The crystite-only escape hatch

454 recipes take crystite and nothing else. They pay for it in time: about 23
times the build wait, per crystite, compared to the same item made with materials.

Kept, and paired with an inverse. A player who's currency-rich and material-poor
waits, and a player who's material-rich and currency-poor spends material to buy
the crystite cost down. It converts a hard block into a slow path in both
directions, which is almost always the better failure mode. See Three levers.

### Attributes

1,933 attribute definitions exist and only 894 are carried by any item, so
roughly half the vocabulary shipped unused. What's live is dominated by Power
Rating, the four ability ratings (recharge, duration, area, potency), and the
weapon ratings (rate of fire, spread, range, magazine, handling).

That's more than enough distinct targets for the one-component-drives-one-attribute
model. It is not a constraint on the design.

### Era warning

This dump is build 1962, which is v1.7. Crafting was switched off in v1.6 and never
came back, so this is the last state of a system that had already been rebuilt
twice since the model being designed here.

The material names do not transfer. Nothing in the lattice above matches the
0.6.1637 resource set. What transfers is shape: the family-by-tier grid, the
pairing rule, the uniform rung template, the geometric currency curve against the
linear material curve, and the crystite-only alternate path. Use the structure,
not the strings.

## Open questions

Numbers on the deposit side, all of them. Slot count per zone, mean budget per
zone, refill delay, carrying capacity.

How many bands. Enough to make the axis meaningful, few enough to keep stack count
sane and band names distinct.

Whether the value budget splits between quality and quantity on a curve or a
straight line, and how much variance sits around the zone mean.

Whether blending is free or carries a cost, and if it carries one, whether that's
crystite or a yield loss.

Whether hybrid rolls scale with haul quality, haul size, or neither.

How fast quality influence widens per tier. The shape is decided, the rate isn't.
It has to be steep enough that a top-tier component from junk is visibly bad, and
shallow enough that a mid-band input is still worth crafting with.

The exchange rate for buying crystite cost down with material quantity, and the
floor on how far it can be bought down.

The time penalty on the currency-only path. The shipped data ran it at about
23 times the build wait, which is a starting point rather than a target.

## Source notes

Recovered from patch notes, treat as real:

| Fact | Source |
|---|---|
| Resources carry three aspects: Power, Mass, Cores | v0.6.85760 |
| Each resource in a family is best at one aspect | v0.6.85760 |
| Component quality follows input resource quality | v0.6.85760 |
| Purity added, then removed with refining flattened to one-to-one | v0.6.85760, then v0.6.1637 |
| Group, family, resource tree and the resource renames | v0.6.1637 |
| Nanoprints call for a group early and a named resource later | v0.6.1637 |
| Components read one named material attribute each | v0.7.1688 |
| Veins have depth, radius, and density as separate tunables | v1.0.1791 |
| Thumper completion pays a double resource bonus and squad XP | v0.6.85760 |
| Mobile tinkering station sold in the PvP store | Devil's Due site update |
| Twelve base materials as three families by four tiers | build-1962 clientdb, Recipes |
| Three intermediate chains, one per family pair, four rungs each | build-1962 clientdb, blueprint type 3 |
| Rung template of twenty plus ten plus one, metal counted double | build-1962 clientdb, blueprint type 3 |
| Crystite ladder is geometric at about 1.20 per rung | build-1962 clientdb, 122-item fit |
| Intermediate material cost is linear in base units | build-1962 clientdb, chain expansion |
| Build time near 0.22 seconds per crystite at the top of the ladder | build-1962 clientdb, Recipes |
| 454 crystite-only recipes at about 23 times the build wait | build-1962 clientdb, Recipes |
| 894 of 1,933 attributes are carried by an item | build-1962 clientdb, Attributes |
| Resource stat slots get player-facing names set per group, not per resource | Crafting-Chain.md, Resource_Stat_Names |
| Purity's slot is permanently named that at the engine level; every group displays it as Thermal Resistance | Crafting-Chain.md, Resource_Stat_Names |
| All sixteen resources mapped to their slot and named property | Crafting-Chain.md, ResourceStat / ResourceItem |
| Two crafting systems share tables in build 1962, split by blueprint id range 75430-85611 vs everything else | Crafting-Chain.md |
| Per-instance resource stat values were generated at runtime from a base-36 string, never static, and had stopped being written by the 2016 capture | Crafting-Chain.md, lib_Items.lua GetResourceStats |

Everything else on this page is designed, not recovered.

Two discrepancies worth resolving against the client:

The v0.6 launch notes say five families of three, fifteen resources total. The
rename in v0.6.1637 lists six families, two of which hold only two resources,
which comes out to sixteen. One of those is wrong or the reorg changed the count.

The vein geometry values come from v1.0.1791, which is a later era than the model
being targeted here. Provenance is solid but the era isn't matched.
