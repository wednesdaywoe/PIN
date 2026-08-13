# Restoration

**The goal is to restore the loops that were good, on top of build 1962. Not to recreate Firefall
at a particular date.**

That distinction decides more than it sounds like it does, so it is written down here rather than
rediscovered every time a question comes up.

Where the [Wiki](Wiki/README.md) records what Firefall *was*, this records what we are choosing to
bring back and what we are deliberately not.

## What this settles

**Crafting needs a surface, not the original surface.** v1.6 deleted the crafting panel and the
client cannot be rebuilt, but nothing requires the replacement to look like what was removed.
Repurposing the surviving `Tinkering` panel, or authoring a minimal one in Lua, are both legitimate
answers. Fidelity was the expensive constraint and it does not apply.

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

The beta budget survives in the data but isn't wired up. `dbitems::ItemTypeAttributeModifier`
carries per-attribute weight, power and cpu coefficients, and loads, but nothing reads it.
`dbitems::FrameMassRange` carries the mass-to-speed rule, has no loader, and isn't confirmed
present in 1962. Rebuilding CPU/Mass/Power is new construction, not removal, and the gate was
whether 1962's garage still shows three budgets or only an aggregate.

**Checked in game 2026-08-13: the garage shows a power rating and an equipment score, and nothing
else.** Two aggregates, no CPU, mass or power bars, so the beta budget has no surface left in the
client — the same shape of problem as the deleted crafting panel, and the same answer applies:
a replacement surface would have to be authored, not restored. Only `PowerRating` (attribute 1451)
is modelled server-side, hardcoded to 100 in
[CharacterLoadout.cs](../UdpHosts/GameServer/Data/CharacterLoadout.cs) and 681 in
[CharacterEntity.cs](../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs); equipment score
appears nowhere in the codebase, so the client is deriving it. That makes the three-budget model a
deliberate build rather than a reconnection, and it is not on any milestone.

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
