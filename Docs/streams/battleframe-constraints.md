---
project: pin
kind: stream
title: "Battleframe Constraints: Mass, Power and CPU"
relates:
  - ../PROGRESS.md
  - ../Restoration.md
---

# Battleframe Constraints: Mass, Power and CPU

**Scoped 2026-08-14 from the repo alone, with no client on the machine.** Everything below about
PIN's own code was read and confirmed here. Everything about `clientdb.sd2` is carried over from
[Restoration](../Restoration.md), which checked it on 2026-08-13 and 2026-08-14, because the db
ships with the client and isn't on this box. The two are marked apart where it matters.

Not on any milestone. This is the scoping pass, so the exit condition at the bottom is a target
rather than a commitment.

## The surface the reference shots already specify

[UI Reference](../UI%20Reference/) holds four frames from a beta guide video, and between them they
pin the layout down further than a description would. A Raptor at 18 of 30 unlocks reads MASS
691/1400, PWR 446/800, CPU 8/13. Each of the three is one row: icon, label, `used/capacity`, an
inline strip of tech nodes, and a bar.

Two things beyond the bars:

- The mass row carries **"Speed Multiplier: 100%"** underneath. Mass isn't pass/fail, it's a curve
  into movement speed.
- The power row carries an **"Allocate Surplus"** button and "104 unallocated power", opening a
  second panel where surplus is placed per slot for a percentage boost: Signature Weapon (+4.4%) at
  44/100, Secondary Weapon (+10.0%) at 100/100, each ability at 50/50. That's a separate system
  from the constraint budget and it should be scoped separately.

Node tooltips give the grant format too. `Mass Tech 7 - Low Density Servos` grants Mass +200, Pilot
Tokens +1 and Max Speed Boost 100%, costing 150,000 XP / 2,000 CY / 2,500 Mineral, and hovering it
previews the bar as 691/**1600**. `CPU Tech 6 - Advanced SmartGel` grants Cores +1 and Energy +30
for 70,000 XP / 1,000 CY / 1,250 Organic. So CPU capacity is counted in cores, which is why it's a
number like 13 rather than a number like 1400, and the four currency counters on the same screen
(XP, Organic, Mineral, Gas) are the beta economy Restoration already describes.

## The used half is probably already on the wire

This is the part that moves the framing.
[`CharacterStatsData.ItemAttributes`](../../Lib/AeroMessages/AeroMessages/GSS/V66/Shared.cs#L250) is
an open array of `(ushort attributeId, float value)` tagged against `dbitems::AttributeDefinition`,
and [`ApplyItemStats`](../../UdpHosts/GameServer/Data/CharacterLoadout.cs#L331-L342) sums every
attribute row on the chassis and each slotted item with no filter on the id at all. Attributes 951,
952 and 953 are ordinary `AttributeDefinition` ids. So they're already being summed and already
going out on every `ApplyLoadout` → `SetCharacterStats` →
[`EquipmentView.CharacterStatsProp`](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L843-L850),
as negatives, because they're stored as costs.

Nothing needs to be added to the protocol for the used half. Two things need checking:

- **Weapons are left out.**
  [CharacterLoadout.cs:399](../../UdpHosts/GameServer/Data/CharacterLoadout.cs#L399) only sums
  ability and chassis slots. Primary and Secondary go to the separate `WeaponA`/`WeaponB` arrays
  instead. The beta allocate-power panel lists both weapons as consumers, so their costs belong in
  the total and currently aren't.
- Whether the items a stock loadout actually slots are among the 1,125 that carry the attributes.

## The capacity half has no source, and that's now confirmed in PIN too

[`dbitems::Battleframe`](../../UdpHosts/GameServer/StaticDB/Records/dbitems/Battleframe.cs) carries
BaseHealth, BaseEnergy, BaseShields and BodyMass. BodyMass is physics, not budget. There is no mass,
power or CPU capacity column of any kind, so there's nothing to load and nothing to recover. Frame
capacities have to be authored, which is what Restoration already concluded from the client side.

## Retail's own item costs can't produce three bars

Restoration's audit of `dbitems::AttributeRange` found power is exactly half of mass in 1,006 of
1,015 tuned rows. If that holds per item then it holds for the sum, so used power is used mass over
two for every loadout anyone can build. The power bar would be a scaled copy of the mass bar and
carry no information of its own. Two of the three bars would be the same bar.

The beta shot corroborates the audit from the other direction: 446 power against 691 mass is a ratio
of 0.65, not 0.5, so beta wasn't priced by that formula. It's the re-templating Restoration
identified, seen in a screenshot instead of in the table.

The consequence is a design decision that has to be made before any of this draws, and it's made
below. CPU survives the same test, because 2 or 3 cores against a capacity of 13 gives a
five-module ceiling that bites well before mass does.

CPU clears that test but has a different one to fail, and this one is visible from the repo.
[Attributes](../Wiki/Reference/Attributes.md), generated by `Tools/SdbDocs` from the same db, counts
the items carrying each attribute: 1081 on mass, 1108 on power, 373 on CPU. So the coverage isn't
even. Mass and power are priced across the whole tuned catalogue and CPU is priced on a third of it,
which means a CPU bar built on retail data sits still for most of what a player equips. It also
means the three are not interchangeable when the sweep comes back: a frame whose stock loadout has
no CPU cost at all is a frame with nothing to calibrate a CPU capacity against.

## Item costs get re-authored so power binds where mass doesn't

Decision 2026-08-14, user-chosen. The alternative was accepting mass and CPU as the only live
constraints, and that takes the surplus allocation pool with it: no power budget means no surplus
to place, and the allocate panel is the most distinctive thing in the system. Half the rule's cost
is paid to delete the mechanic it exists for.

Two beta prices set the range. A Stock SIN Scrambler cost 88 mass against 45 power, and a
Recovered SIN Beacon I 48 against 58 (user, 2026-08-14). Read carefully, the Scrambler is not a
counterexample to the half rule, it sits on it, 45 where the formula wants 44. So the beta spread
runs from the half rule upward rather than around it, and retail didn't re-centre the table, it
collapsed everything onto the cheap-to-run end. That's the same finding as the hand-authored 88/45
survivors, from the pricing side.

The split follows what an item is. Ammunition is bulky and cheap to run, a deployable emitter is
light and draws hard. Plating, ammunition, servos and structural gear price into mass; emitters,
fields, cloaks, turret electronics and anything that projects or sustains an effect price into
power. The Raptor's 446 against 691 is a mixed loadout sitting between the two anchors.

Beta was still working that axis by hand two builds from the end of 0.7. Build 1688 moved the
Personal Health Sensor from power to mass, put the Shield Energizer and the Instant Healing System
on mass over power, and swapped the Fuel Charger to an Energy Generator so its cooldown reduction
ran off power (user, 2026-08-14; the patch mirror isn't on this machine, so this is unverified
here). Four lines in one patch reassigning which budget a module keys to makes this a per-component
authoring lever, not an emergent property of a formula. Two caveats on reading it. Three of the
four moved toward mass, which is the flattening that ends in retail's half rule already starting.
And the Fuel Charger line is the weakest of the four, because power the budget and energy the
ability pool are separate systems and that note could mean either.

`dbitems::ItemTypeAttributeModifier` is where the rule gets written. It prices a point of a given
stat into weight, power and CPU separately and keys on `CraftingTypeId` as well as `AttributeId`,
so the same stat can cost differently depending on what class of item carries it. That's the design
rule's exact shape, which means the axis is authored as a coefficient set rather than as 1,125
hand-priced rows.

That also answers why power isn't redundant at the mechanism level rather than the data level. Mass
is a continuous penalty, paid through the speed curve. Power is an opportunity cost, paid in
surplus not allocated. Cores are a hard gate. Three constraints, three kinds of pain.

Weapons fold into the totals on the same argument. The allocate-surplus panel treats Signature and
Secondary as first-class targets with 100-point caps, so they're in the power economy by
definition, and a heavy weapon that didn't slow you would be conspicuous against the speed curve.

## Two orphan tables that belong to this work

Both have a record class in the repo and no loader in
[ISDBLoader](../../UdpHosts/GameServer/StaticDB/Loaders/ISDBLoader.cs) or
[SDBInterface](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs).

`dbitems::FrameMassRange { SpeedMult, MaxUsedMass }` is the Speed Multiplier line from the
screenshot: the used-mass to speed curve. Its record has no frame id column, so it's either a global
curve or the record is missing one, and that's checkable the moment the db is in reach.

`dbitems::ItemTypeAttributeModifier { AttributeId, WeightCoefficient, PowerCoefficient,
CpuCoefficient, MapToAttributeId, CraftingTypeId, ... }` prices a point of any given stat in each
of the three budgets. It's the formula behind the costs, it's what crafted output would need to be
priced against, and per the decision above it's where the re-authored mass/power split gets
written.

## What is loaded is the tech tree's shell

`dbitems::Certificate` is loaded, with `GetAllCertificates()` already exposed
([SDBInterface.cs:611](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs#L611)). Its payload
columns are zero, per Restoration, but the shape is suggestive: `BonusAmount` and `CertType`
together are the obvious home for "Mass +200", and `XpValue`/`XpType` for the 150,000 XP. So the 60
nodes can be authored into a table that exists and is already in memory, rather than into a new one.

## Where the capacity numbers would go

`ItemAttributes` entries have to carry ids that exist in `AttributeDefinition`, and no "mass
capacity" attribute is known. Three routes:

1. Find or repurpose `AttributeDefinition` ids and ride the array that's already flowing.
2. Extend the HTTP garage payload. `api/v3/characters/{characterId}/garage_slots` is live and
   hand-built ([AccountsController.cs:117](../../WebHosts/WebHost.ClientApi/Accounts/AccountsController.cs#L117)),
   and [`Equippedslots`](../../WebHosts/WebHost.ClientApi/Characters/Models/GarageSlots.cs#L23-L30)
   already has an `AllocatedPower` field and an `ItemLimits` block sitting unused. It's JSON, so
   there's no schema to satisfy.
3. Author them client-side as Lua constants.

Route 2 is underrated. The fields are already there, the endpoint is already answered, and the
allocate-surplus system would need `AllocatedPower` anyway.

## The sweep, and what it settles

[Tools/ConstraintSweep](../../Tools/ConstraintSweep/) is written and builds, and hasn't been run
because the db isn't on this machine. It walks every shipped loadout the way `CharacterLoadout`
does, chassis first and then each slot through `SDBInterface.GetItemAttributeRange`, and reports a
mass, power and CPU total per frame with the slot-by-slot breakdown behind it. Weapons are totalled
apart, so the cost PIN currently drops is a number in the report rather than a caveat under it.

It refuses to produce a report if attributes 951, 952 and 953 don't resolve to mass, power and CPU
in the db it was pointed at, since every total would otherwise be measuring something else. Two
figures in the output cross-check the audits already done here: the per-attribute item counts
against the reference doc's 1081 / 1108 / 373, and the half rule against 1,006 of 1,015. The items
that break the half rule come out by name, which is where the hand-authored beta survivors are.

Three things the answer decides. Whether stock loadouts carry costs at all, because if the 1,125
priced items are all off them then there's nothing to calibrate against and the bars read zero on a
fresh character. What a capacity has to be, which is the whole reason the frame half can't be
authored yet. And whether CPU is priced on the frames people actually play, given it covers a third
of the catalogue.

## Work

| Work | Where |
|------|-------|
| Sweep the shipped loadouts for their real 951/952/953 totals | written 2026-08-14 as [Tools/ConstraintSweep](../../Tools/ConstraintSweep/), not yet run |
| Sum weapon costs into the loadout total | decided 2026-08-14, they fold in; [CharacterLoadout.cs:399](../../UdpHosts/GameServer/Data/CharacterLoadout.cs#L399) |
| Author the mass/power coefficient set | `dbitems::ItemTypeAttributeModifier`, keyed on `CraftingTypeId`; needs the sweep first to know what the current split costs |
| Author per-frame capacities | new; `CustomData` is where authored data already lives, since `dbitems::Battleframe` has no columns for it |
| Load `FrameMassRange` and apply the speed curve | [StaticDBLoader.cs](../../UdpHosts/GameServer/StaticDB/Loaders/StaticDBLoader.cs), [SDBInterface.cs](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs) |
| Load `ItemTypeAttributeModifier` | same pair |
| Decide the capacity transport, then send it | one of the three routes above |
| Author the 60 certificate payloads | `dbitems::Certificate` is loaded; the columns are zero |
| Re-bind the widget and feed `item_info.constraints` | `SNV_ConstraintsBars.lua`, `BattleframeGarage.lua`, on the client machine |

## Exit

Open the garage, see three bars carrying real numbers, swap a plating for a heavier one and watch
mass move and speed drop.

## What can't be done from here

Three checks need the machine with the client on it, and the first one decides how much of the rest
is even reachable:

- Whether the engine hands attributes 951 to 953 to Lua in an item's ordinary stat list. One tooltip
  inspection settles it. If it doesn't, the used half has to travel by route 2 or 3 as well.
- `SNV_ConstraintsBars.lua`'s actual API, rather than Restoration's summary of it.
- Whether `RecalculateConstraints` can be un-commented in `BattleframeGarage.lua` without the rest
  of the removed engine half.

Everything above the exit line is server-side and doesn't wait on any of that. The costing sweep is
the one exception, and only narrowly: it needs `clientdb.sd2`, which ships with the client, but not
a client running.

## Aside: PowerRating has two values

The one aggregate the garage does draw today is attribute 1451, and PIN sets it twice from two
places: 100 as a loadout fallback in
[CharacterLoadout.cs:118](../../UdpHosts/GameServer/Data/CharacterLoadout.cs#L118), and a hardcoded
681 in [CharacterEntity.cs:1704](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L1704).
Not this stream's problem, but worth knowing before anyone reads a power number in game and trusts
it.
