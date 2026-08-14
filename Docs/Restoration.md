# Restoration

**The goal is to restore the loops that were good, on top of build 1962. Not to recreate Firefall
at a particular date.**

That distinction decides more than it sounds like it does, so it is written down here rather than
rediscovered every time a question comes up.

Where the [Wiki](Wiki/README.md) records what Firefall *was*, this records what we are choosing to
bring back and what we are deliberately not.

## What this settles

**Crafting needs a surface, not the original surface.** v1.6 deleted the crafting panel (the MPU
component, plain Lua/XML) but left the engine's recipe API and the whole Fabrication protocol in
the binary, so only the screen is gone and nothing requires its replacement to look like what was
removed. Repurposing the surviving `Tinkering` panel, or authoring a minimal one in Lua against
the surviving API, are both legitimate answers. Fidelity was the expensive constraint and it does
not apply.

**No obligation to the whole recipe set.** 9,228 blueprints exist and 5,052 have a resolvable
output and ingredient. A curated subset that makes the loop work is a complete answer; shipping all
of them is not a requirement.

**The research chain is optional.** `research_blueprint_id` and `head_blueprint_id` resolve at 100%
and describe a discovery layer sitting on top of crafting. It can be implemented, simplified, or
skipped, and that is a gameplay call rather than a fidelity one.

**Levels are already inert.** PIN never implemented leveling. Character level is the constant 45 in
`HardcodedCharacterData`, XP always goes out as zero, `LevelUpEvent` is never sent, and level-based
stat scaling was never written. Power comes entirely from the loadout: `CalculateItemAttributes`
sums the chassis and every slotted item, and level isn't a term in it. That's the beta model,
arrived at by accident. So level-45 balance is a number the client draws rather than a system
crafted output has to satisfy.

**XP is a separate question from levels, and the beta had one without the other.** Beta XP was a
currency: you earned it and you spent it on upgrading your frame. It was part of the resource
economy rather than a track that pulled a level number up behind it (user, 2026-08-13). So "no
levels" does not imply "no XP", and dropping XP from [M5](streams/m5-kill-rewards.md) closed a
milestone rather than a design question — nothing in PIN consumes XP today, and building the
spending side is its own piece of work, not a rider on paying out for a kill.

Worth knowing because it makes a column name legible: `dbcharacter::Monster.xp_resource_id` says
*resource*, which is what XP was. 1962's own rows don't corroborate it — the column is set on 2 of
3109 monsters and one of the two ids carries no `Resource` flag — but by 1962 the beta economy was
long gone, so their silence is expected rather than evidence.

**The item-side costs survive in the client db (checked 2026-08-14).** Mass, Power and CPU are
still defined in `dbitems::AttributeDefinition`, side by side as attribute ids 951, 952 and 953,
and 1,125 items carry values for them in `dbitems::AttributeRange`, stored negative because
they're costs against the budget. They sit on exactly the stratum where the pre-1.6 weapon
templates survived: 1.0-era servos, platings, jumpjets, abilities and crafted modules. The
formula-side tables noted earlier are still there too: `dbitems::ItemTypeAttributeModifier`
loads its weight, power and cpu coefficients that nothing reads, and `dbitems::FrameMassRange`
still has no loader.

The values are a late pricing formula, not beta design intent. Across all 1,125 items mass takes
only seven distinct values, running 48/64/80/120 by tier; power is exactly half of mass in 1,006
of 1,015 tuned rows; CPU is 2 or 3; and within a same-slot, same-tier group the numbers barely
move, so a 1,100-health plating costs the same as a 2,100-health one. A known-answer probe
settled what happened to the beta numbers: the beta Stock SIN Scrambler cost 88 mass, 45 power,
2 cores, and while the item itself survives by exact name with its costs stripped, five
"Regulation" tier-7 items (Plasma Stormer, Supernova, Smart Bomb, Stasis Shot, Antimatter Field)
carry exactly 88/45, Antimatter Field with the CPU 2 as well. The non-formula ratio marks them as
hand-authored rows that slipped through every rebalance. So the table wasn't rescaled from beta,
it was re-templated, and only fragments of the original design survive in it.

**Checked in game 2026-08-13: the garage shows a power rating and an equipment score, and nothing
else.** Two aggregates, no CPU, mass or power bars. Only `PowerRating` (attribute 1451) is modelled
server-side, hardcoded to 100 in
[CharacterLoadout.cs](../UdpHosts/GameServer/Data/CharacterLoadout.cs) and 681 in
[CharacterEntity.cs](../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs); equipment score
appears nowhere in the codebase, so the client is deriving it.

**The bars themselves survive, though, in Lua.**
`gui/components/MainUI/Sinvironment/garage/helpers/SNV_ConstraintsBars.lua` is intact: a three-bar
widget keyed on exactly `"mass"`, `"power"` and `"cpu"`, with hover preview deltas and an
over-budget tint, and a header comment documenting the whole API. Nothing in the client requires
the file, so it's orphaned rather than deleted. The garage still carries the scars around it.
`BattleframeGarage.lua` computes a `{mass, power, cpu}` delta on every `OnExamine` and then
discards it, `RecalculateConstraints` survives as a commented-out field, and the skin still defines
`constraint_preview_higher`, `constraint_preview_lower`, `exceed_constraint` and a
`constraints_scale` region.

What's gone is the engine half. Item-info field names appear as plain strings in
`FirefallClient.exe`, so their absence carries weight, and there's no whole-word `mass` or `cpu` in
the binary at all. Every read of `item_info.constraints` in the garage falls back to zeroes because
the numbers never arrive.

**The tech tree survives as names and nothing else (checked 2026-08-14).** All 60 progression
nodes exist in `dbitems::Certificate`: Mass, Power and CPU Tech 1 through 10, fully named and
described in five languages ("Mass Tech 7 - Low Density Servos" is cert 705), plus unnamed blanks
reserved for 11 through 20. But every payload column on them is zero, and a scan of every table in
the db for their cert ids found only id collisions. The grant amounts, the node costs and the
per-frame capacities never shipped; they were server data, like the spawn tables. Rebuilding the
tree means authoring those numbers, with beta tooltips and patch notes as the only sources. The
one primary data point in hand: a Raptor at 18 of 30 unlocks showing Mass 1400, Power 800, CPU 13,
with Mass Tech 7 granting +200 (user, 2026-08-14). The client's progression panel (`BFPLogic.lua`,
around `Game.GetProgressionUnlocks`) only renders power-rating and health grants now, so the tree
would need a surface of its own as well.

That looks like a better starting position than the crafting panel, which has no surviving
surface — but the comparison runs the other way (checked 2026-08-13). Crafting lost only its Lua:
the full Fabrication command set survives in both the client binary and AeroMessages, and the
engine still exports `GetRecipeIds`/`GetRecipeInfo`, which `Mainframe.lua` still calls. Missing
Lua can be authored. The bars are the inverse case — the Lua survived and the engine feed is what
died, inside a binary we can't rebuild. Thumping still sits above both, with every layer intact.
The build is real either way, and still not on any milestone, but it's re-wiring a preserved
widget rather than authoring one. The question it turns on is how three numbers reach a widget
the engine won't feed. The item half may already be within reach: the costs sit in the client's
own db as attributes 951 to 953, so the first thing to check is whether the engine still hands
them to Lua in an item's ordinary stat list, which one tooltip inspection would settle. The frame
half has no source anywhere and has to be authored either way.

**Sources become design references.** The [patch corpus](Wiki/Sources.md) stops being a spec to
conform to and becomes an argument about what each loop was for and why it worked. Beta-era
material is back in play precisely because we are not bound to 1962's decisions.

## What the data leaves open

Build 1962 shipped **both** resource models, which means the choice is real rather than forced.

| | |
|--|--|
| Beta-era named resources | Brimstone, Ferrite, Silicate, Basalt, Coralite, Quartzite, Azurite, Bismuth, Regolith (ids 75537–75597) |
| Later resources | Copper, Iron, Aluminum, Carbon, Ceramic, Methine (ids 77703+) |
| Total | 111 `dbitems::ResourceItem` rows, all quality 1 |
| `dbitems::ResourceStat` | 5 rows — the five-stat structure that made beta resources gradeable |
| `dbzonemetadata::ResourceNodeType` | 46 named node types, explicitly level-banded: *"Tier 1 (lvls 1-19) Resource Vein - Iron"*, *"Tier 2 (lvls 20-29) Resource Vein - Tungsten & Iron"* |

So the shipped data supports the graded-material economy the beta had *and* the level-banded vein
model retail moved to. Both are readable; neither is imposed. The open decisions:

- **Which resource economy.** Five-stat graded materials, where what you thumped mattered, or
  level-banded veins that hand out tiered inputs. The first is the loop most people mean when they
  say they miss crafting; the second is what the node types are already written for.
- **Where crafted output lands.** Into 1962's existing item and progression system, or into a
  parallel track that does not have to satisfy level-45 balance.
- **How resources are carried.** Whether they fit the existing inventory model or need handling of
  their own.

None of these block starting.

## Order

**Thumping first.** Data, protocol and client UI are all present and the work is entirely
server-side — `ResourceNode` is a first-class GSS controller, the whole scan-to-completion command
set is defined in AeroMessages, and `Thumper.lua` is still in the client. Nothing about it is
gated on a decision above; a node that spawns, gets thumped and pays out is reachable without
settling the economy question.

**Crafting second**, because it is the only part with a client-side blocker, and because the
economy decision is easier to make once resources actually exist in the world.

See [Thumping and Crafting](Wiki/Thumping-And-Crafting.md) for the survival audit both rest on.
