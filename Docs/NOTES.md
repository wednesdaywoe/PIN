REMINDER
add a fourth item to the client-machine checklist at battleframe-constraints.md:218-226 — do unknown JSON keys in garage_slots survive into Lua? One session settles whether route 2 can carry capacities at all, and route 2 is the one you called underrated.



Notes for researching how Beta worked. 

>From IGN - Jun 18 2016

Red 5 Studios noticed a pretty major issue while beta testing its shooter MMO Firefall. Previously, Red 5 had a traditional MMO leveling curve, with initially fast leveling that gradually slowed down. As it turns out, toward the higher levels, it slowed down far too much.
“What that did, is it really disincentivized people from playing the game,” said lead designer Scott Youngblood, “because the rewards were so far out that they just gave up. So one of the things I was looking at doing was increasing the rate of reward, giving players more tangible goals to achieve in-between levels. But then it dawned on me, well if we did that, what’s the point of having levels at all?”

So Red 5 redesigned the system without levels, trading a typical approach to MMO design for a tier system for all the game’s Battleframes. Battleframes are superpowered suits of armor in Firefall that alter your ability set, functioning essentially like classes. You’re not limited to one, so it’s possible to power up a variety of different Battleframes and then switch between them at specific in-game locations.

Red 5 Studios noticed a pretty major issue while beta testing its shooter MMO Firefall. Previously, Red 5 had a traditional MMO leveling curve, with initially fast leveling that gradually slowed down. As it turns out, toward the higher levels, it slowed down far too much.
“What that did, is it really disincentivized people from playing the game,” said lead designer Scott Youngblood, “because the rewards were so far out that they just gave up. So one of the things I was looking at doing was increasing the rate of reward, giving players more tangible goals to achieve in-between levels. But then it dawned on me, well if we did that, what’s the point of having levels at all?”

So Red 5 redesigned the system without levels, trading a typical approach to MMO design for a tier system for all the game’s Battleframes. Battleframes are superpowered suits of armor in Firefall that alter your ability set, functioning essentially like classes. You’re not limited to one, so it’s possible to power up a variety of different Battleframes and then switch between them at specific in-game locations.

The current plan is to include five total Battleframe tiers, where each higher tier isn’t only more powerful, but also allows access to unique mechanics. “You go from tier one, which has no crafting options right now, and tech up your frame to tier two, you immediately start being able to craft options for your gear. Tier three offers a different type of way to upgrade your characters called passive bonuses. Those are really cheap but scattered all around the tech tree. Even if you log on and only play for two or three matches, or one match, you could probably afford to buy one of those passives. But you want to get them all because they stack up to a pretty decent bonus. Each additional tier that we go up has more horizontal progression options as well as more mechanics that feed into this system.”

This tiered system is another way Red 5 gets around balance issues with its PvP matchmaking system for arena battles. “When you go to matchmake for PvP it’s going to look at the frame you’re currently wearing and match you for that tier. That creates some interesting scenarios. Let’s say I have a tier two BioTech, but I have a tier five Assault. When I queue for that tier two BioTech and I get into a match, my tier five Assault will no longer be accessible to switch to during combat. So that’s the way we keep the power consistent.”

Eventually you’ll unlock a large number of combat options to equip on your frame, from different ammunition types to movement boosters, but not all can be active at a time. Everything you equip consumes resource points, so you need to make decisions about which powerful items you want equipped at any one time to stay under the resource point cap.

This ties into Red 5’s recent changes to the crafting system, which according to Youngblood was modeled on what he liked about crafting in Star Wars Galaxies. By visiting crafting terminals you’re able to match up blueprints and materials you’ve collected to construct useable items. To build better weapons, you need to use rarer materials than what the weapons were originally built with, and you additionally need to pay close attention to the statistics the materials are influencing. If you want to boost a sniper rifle’s damage output for instance, not just any resource will do. Even a super rare resource type might not be the most effective way to boost damage, so there’s a lot of room to explore, collect and experiment with putting together the best components and arrive at the desired results.

In addition to adjusting the progression and crafting mechanics, Red 5 has made sweeping changes to the actual function of the classes. The Medic, which used to be a dedicated healing class, was completely scrapped and replaced with the BioTech. The BioTech has a number of new abilities and its healing is more skill-based, letting healers take a more active role in a fight. The Engineer frame, which can deploy turrets, can also now stick turrets to walls and ceilings, allowing those using the frame to set up more varied defensive perimeters.

Firefall has been in closed beta testing for some time now, and Red 5 would ideally like to officially launch the game soon. “We’re getting close,” said Youngblood. “For the previous milestones, we were focused on the character progression and the leveling, and we feel like we’ve gotten that going mostly in the right direction. We’ve still got a lot of balance work to do there. I feel a hell of a lot better about this system than I did our previous leveling system. The other thing is that we’ve really been focusing on is increasing the skill ceiling in the game. We want this to be a viable esports product. But in order for it to be an esports product, we need it to have that high skill tier.”

>Reddit Dev post 

Hello, everyone! I’m Cameron Winston, a Senior Game Designer here at Red 5 Studios, and I want to talk to you guys for a bit about the work we’re doing to tweak player progression. A while back, when we first overhauled player progression entirely, we made the switch from traditional leveling to tech trees for each battleframe. It’s a decision that we believe was for the best, but it came with some problems of its own.

What were these problems?

Well, as it turns out the previous iteration of the tech tree was hard to understand, and it had arbitrary limitations that prevented players from exploring how they could customize their frames. Additionally, the way our system was built before prevented us from really expanding in a way that worked, as progression would be tied to an ever-growing combat system which would have to be built on itself. This doesn’t make for a very sustainable form of expansion since the number of possibilities would continue to grow, and trying to balance all of those possibilities would drive us all completely mad.

This also affected our PvP, as this vertical progression would put new players at a disadvantage in competitive PvP.

So, how are we fixing it?

Our ultimate goal with Firefall is to let you play the game however you want – so long as it’s a shooter. The purpose of this retooling of the progression system is to give players like you the ability to drive your own Firefall experience within as much variance of the shooter gameplay as we can support. What that means is that if you want to play as someone who wants to run around shooting things, you can. If you want to sacrifice speed for survivability and march through the world with your massive Gatling gun, you can. If you want to stick to the shadows and pick enemies off from impossible distances, you can.

The point is that we want you to feel like you can do whatever you want.

To help achieve this, we’ve started by changing how you unlock battleframes going forward. Rather than having you level up a particular battleframe to unlock its more powerful iterations, you’ll have a far more flat access to the frames. You’ll still have your different classes, like Assault and Recon and so on, but each battleframe will now have its own unique progression tree. By being able to level each battleframe uniquely, and by allowing you to choose how you level it, we hope that you’ll be able to develop a stable of battleframes that play to your strengths for each battleframe archetype.

Each battleframe tech tree will be split into three different tracts, each of which represents a different constraint – one each for Power, Mass, and Cores. By traveling down a particular tract and unlocking certain abilities, you will increase the limit on that constraint, allowing you to use more potent abilities. So how do these break down?

Mass: Your mass affects your movement speed, and uses a sliding scale with different thresholds. The base threshold is 100%, which is normal speed. Increasing the amount of mass your battleframe is capable of carrying will increase your movement speed, while using heavy equipment beyond your limit will cause you to move slower. What will be important for players is in figuring out which combination of speed vs. durability, survivability, and raw power works best.

Power: Power for each battleframe will have a base power requirement, which will have to be maintained for the frame to be fully functional. Battleframes which exceed their power output will see their weapons and abilities deal less damage, while frames with an excess of power will see more damage dealt. The key will be in balancing their power against their weapons and abilities.

Cores: “Cores” is what we’re renaming “CPUs”, and this one is straightforward. As you improve your battleframe, you will have the chance to unlock more cores. These cores determine how many abilities you can equip on your battleframe at once. Alternatively, if you don’t want to equip abilities you can use these cores to improve other aspects of your battleframe directly. What you use the cores on will drain fewer resources than they otherwise would, potentially leading to further tweaks of the frame. You start with 8 cores, and each ability requires 2 cores. As abilities progress down stages, they increase in core cost. As you play, you can choose to unequip abilities to get more cores, and use them to alter your frames stats.

The first five unlocks for each battleframe will be earned purely through experience. At this point, you’ll be asked to unlock further “stages” of progression through increased experience cost and resources, with the resource in question being unique to each battleframe. By sticking with a particular battleframe and spending the required experience and resources, you can eventually max that frame out and unlock some pretty awesome potential.

As you progress your battleframe, you’ll unlock new visuals for that particular frame, and tokens that you can use to unlock other frames along with the option to purchase frames for Red Beans. There will also be an added emphasis on crafting items with the inclusion of a four-stage progression of item crafting. This will offer more powerful gear and abilities, but will come at increased constraint cost.

While early unlocks will come quickly, the progression of each battleframe will be a much longer process than it is at present.

Great! What does this mean for PvP?

We’re also making changes to our PvP that will level the playing field. All players who jump into a PvP match will be competing with stock versions of the battleframes they have unlocked. These battleframes have power levels that are higher than what a player will typically start off with in PvE, but lower than the very top-tiered frames. You will not be able to alter the stock PvP frame loadouts, but you will have access to all the frames you’ve unlocked when you spawn – just like now.

