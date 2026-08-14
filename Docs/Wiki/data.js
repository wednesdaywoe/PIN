/* ==========================================================================
   FIREFALL 1.6 REFERENCE — ENTITY DATABASE
   --------------------------------------------------------------------------
   Everything the wiki renders comes from this object. To extend the wiki you
   add records here; you never touch the rendering code.

   status values:
     'doc'      documented in the 1.6 notes, numbers unknown
     'partial'  some numbers recovered (usually from a patch note)
     'verified' numbers confirmed against game data / repo
     'built'    implemented in the reimplementation

   A null in a `stats` object is a deliberate hole. The Gaps view is generated
   by counting them, so leaving a field null is how you file a to-do.
   ========================================================================== */

const DATA = {

/* -- ARCHETYPES ---------------------------------------------------------- */
archetypes: [
  { id:'assault',    name:'Assault',    role:'Offense', accent:'#e2643a' },
  { id:'biotech',    name:'Biotech',    role:'Support', accent:'#66c96b' },
  { id:'engineer',   name:'Engineer',   role:'Support', accent:'#e0b23a' },
  { id:'dreadnaught',name:'Dreadnaught',role:'Defense', accent:'#5a86c9' },
  { id:'recon',      name:'Recon',      role:'Offense', accent:'#9b6bc9' }
],

/* -- BATTLEFRAMES -------------------------------------------------------- */
frames: [
  { id:'accord-assault', name:'Accord Assault', archetype:'assault', tier:'accord',
    role:'Offense', status:'doc',
    blurb:'The Assault dashes into the front lines, devastating enemies with powerful attacks.',
    tiers:{ health:'High', speed:'Standard', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'plasma-cannon',
    abilities:['meteor-strike','overcharge','afterburner','shockwave'],
    notes:'Enough health to take a few hits; deals high AoE damage at moderate range. Branches into precision damage (Tigerclaw) or close-range AoE (Firecat).',
    source:'Part 1' },

  { id:'tigerclaw', name:'Tigerclaw', archetype:'assault', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'Long-range precision specialisation of the Assault line.',
    tiers:{ health:'High', speed:'Standard', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'fusion-cannon',
    abilities:['hellfire','pulsar','afterburner','supercharge'],
    notes:'Afterburner thrust force is increased by 50% on this frame.',
    source:'Part 1' },

  { id:'firecat', name:'Firecat', archetype:'assault', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'Close-range area-of-effect specialisation of the Assault line.',
    tiers:{ health:'High', speed:'Standard', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'phason-thrower',
    abilities:['thermal-wave','fuel-cloud','afterburner','fuel-air-bomb'],
    notes:'Afterburner leaves a damaging flame trail on this frame.',
    source:'Part 1' },

  { id:'accord-biotech', name:'Accord Biotech', archetype:'biotech', tier:'accord',
    role:'Support', status:'doc',
    blurb:'The Biotech heals allies and weakens enemies with advanced chemicals and toxins.',
    tiers:{ health:'Standard', speed:'High', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'smart-blaster',
    abilities:['healing-generator','poison-ball','adrenaline-rush','heroism'],
    notes:'Branches into damage (Recluse) or group support (Dragonfly).',
    source:'Part 1' },

  { id:'dragonfly', name:'Dragonfly', archetype:'biotech', tier:'advanced',
    role:'Support', status:'doc',
    blurb:'Group support specialisation of the Biotech line.',
    tiers:{ health:'Standard', speed:'High', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'bio-rifle',
    abilities:['healing-wave','emergency-response','healing-dome'],
    notes:'Only frame with a dedicated healing beam on alternate fire.',
    source:'Part 1' },

  { id:'recluse', name:'Recluse', archetype:'biotech', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'Damage specialisation of the Biotech line.',
    tiers:{ health:'Standard', speed:'High', jet:'Very High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'bolt-driver',
    abilities:['creeping-death','poison-trail','necrosis'],
    notes:'Advanced-frame role differs from its Accord parent: Support becomes Offense.',
    source:'Part 1' },

  { id:'accord-engineer', name:'Accord Engineer', archetype:'engineer', tier:'accord',
    role:'Support', status:'doc',
    blurb:'The Engineer supports allies by building turrets and other machinery.',
    tiers:{ health:'Standard', speed:'High', jet:'Standard', jetRecharge:'Very High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'arc-thrower',
    abilities:['heavy-turret','overclock','supply-station','anti-personnel-turret'],
    notes:'Fortifies locations with turrets and supply pads.',
    source:'Part 1' },

  { id:'bastion', name:'Bastion', archetype:'engineer', tier:'advanced',
    role:'Support', status:'partial',
    blurb:'Deployable-density specialisation of the Engineer line.',
    tiers:{ health:'Standard', speed:'High', jet:'Standard', jetRecharge:'Very High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'mine-launcher',
    abilities:['multi-turrets','deployable-shield','fortify'],
    notes:'Primary weapon was completely redesigned in 1.6.1940 — see the Mine Launcher page.',
    source:'Part 1' },

  { id:'electron', name:'Electron', archetype:'engineer', tier:'advanced',
    role:'Support', status:'doc',
    blurb:'Direct damage and group buff specialisation of the Engineer line.',
    tiers:{ health:'Standard', speed:'High', jet:'Standard', jetRecharge:'Very High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'shock-rail',
    abilities:['bulwark','boomerang-shot','electrical-storm'],
    notes:'Shock charges stack to two, then detonate.',
    source:'Part 1' },

  { id:'accord-dreadnaught', name:'Accord Dreadnaught', archetype:'dreadnaught', tier:'accord',
    role:'Defense', status:'doc',
    blurb:'The Dreadnaught is a heavily-armed fortress that shields their teammates from harm.',
    tiers:{ health:'Very High', speed:'Standard', jet:'Standard', jetRecharge:'High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'heavy-machine-gun',
    abilities:['heavy-armor','turret-mode','charge','absorption-bomb'],
    notes:'Lower damage than dedicated Offense frames, far more durable; built to pull enemy attention.',
    source:'Part 1' },

  { id:'mammoth', name:'Mammoth', archetype:'dreadnaught', tier:'advanced',
    role:'Defense', status:'doc',
    blurb:'Group defence specialisation of the Dreadnaught line.',
    tiers:{ health:'Very High', speed:'Standard', jet:'Standard', jetRecharge:'High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'rotary-blaster',
    abilities:['gravity-pull','thunderdome','dreadfield'],
    notes:'',
    source:'Part 1' },

  { id:'rhino', name:'Rhino', archetype:'dreadnaught', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'High damage specialisation of the Dreadnaught line.',
    tiers:{ health:'Very High', speed:'Standard', jet:'Standard', jetRecharge:'High' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'photon-lance',
    abilities:['penetrating-rounds','sundering-blast','mighty-charge'],
    notes:'Advanced-frame role differs from its Accord parent: Defense becomes Offense.',
    source:'Part 1' },

  { id:'accord-recon', name:'Accord Recon', archetype:'recon', tier:'accord',
    role:'Offense', status:'doc',
    blurb:'The Recon picks off enemies with precise attacks from long range.',
    tiers:{ health:'Standard', speed:'Very High', jet:'High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'marksman-rifle',
    abilities:['cryo-shot','teleport-beacon','remote-explosive','artillery-strike'],
    notes:'Branches into group defence and close range (Raptor) or group offence and piercing sniper (Nighthawk).',
    source:'Part 1' },

  { id:'nighthawk', name:'Nighthawk', archetype:'recon', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'Piercing sniper and group offence specialisation of the Recon line.',
    tiers:{ health:'Standard', speed:'Very High', jet:'High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'sniper-rifle',
    abilities:['decoy','sin-beacon','eruption'],
    notes:'',
    source:'Part 1' },

  { id:'raptor', name:'Raptor', archetype:'recon', tier:'advanced',
    role:'Offense', status:'doc',
    blurb:'Close range and group defence specialisation of the Recon line.',
    tiers:{ health:'Standard', speed:'Very High', jet:'High', jetRecharge:'Standard' },
    stats:{ healthValue:null, speedValue:null, jetPool:null, jetRegen:null },
    primary:'charge-rifle',
    abilities:['smoke-screen','assassinate','overload'],
    notes:'',
    source:'Part 1' }
],

/* -- SIGNATURE WEAPONS --------------------------------------------------- */
weapons: [
  { id:'plasma-cannon', name:'Plasma Cannon', frames:['accord-assault'], damageType:'Thermal', status:'doc',
    desc:'Fires bursts of plasma that explode on impact for area damage. Sustained fire overheats the weapon, forcing a cooldown before it can fire again.',
    alt:'Assault stance — aim down sights for improved accuracy and rate of fire at reduced movement speed.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, heatPerShot:null, cooldownTime:null } },

  { id:'fusion-cannon', name:'Fusion Cannon', frames:['tigerclaw'], damageType:'Thermal', status:'doc',
    desc:'Launches bolts of plasma that detonate in a small area at the point of impact. Serves as both a precision weapon and an ordnance delivery device.',
    alt:'Assault stance — aim down sights for improved accuracy and rate of fire at reduced movement speed.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, blastRadius:null } },

  { id:'phason-thrower', name:'Phason Thrower', frames:['firecat'], damageType:'Thermal', status:'partial',
    desc:'Sprays a high-energy stream of molten material, dealing thermal damage in an area at the point of impact. Damage increases as the weapon heats up.',
    alt:'Assault stance — aim down sights for improved accuracy and rate of fire at reduced movement speed.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+20%', heatDamageScale:null } },

  { id:'smart-blaster', name:'Smart Blaster', frames:['accord-biotech'], damageType:'Chemical', status:'partial',
    desc:'Fires nano-toxin globules that heal allies and deal small-radius area damage to enemies on impact.',
    alt:'Aim down sights for improved accuracy.',
    stats:{ dps:null, magazine:10, ammoPerShot:1, ammoCapacity:null, critBonus:null, healPerShot:null } },

  { id:'bio-rifle', name:'Bio Rifle', frames:['dragonfly'], damageType:'Chemical', status:'doc',
    desc:'Fires toxic projectiles at hostile targets.',
    alt:'Hold to fire a beam that heals friendly targets.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, healBeamRate:null } },

  { id:'bolt-driver', name:'Bolt Driver', frames:['recluse'], damageType:'Chemical', status:'partial',
    desc:'Fires a bolt laced with deadly toxins, dealing direct chemical damage.',
    alt:'Aim down sights to fire with increased accuracy.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+40%' } },

  { id:'arc-thrower', name:'Arc Thrower', frames:['accord-engineer'], damageType:'Energy', status:'doc',
    desc:'Fires a focused, short-range beam that chains to one additional nearby target, damaging enemies and repairing allied deployables.',
    alt:'Aim down sights for improved accuracy.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, chainRange:null, repairRate:null } },

  { id:'mine-launcher', name:'Mine Launcher', frames:['bastion'], damageType:'Energy', status:'partial',
    desc:'Primary fire deploys arc mines that fire a beam at nearby enemies, damaging them over time. Alternate fire deploys repair mines that repair nearby deployables, prioritising whichever needs it most.',
    descSuperseded:'Fires grenades that attach to the target or surface. Alternate fire detonates all grenades, damaging enemies and repairing allied deployables in the effect radius.',
    supersededBy:'1.6.1940',
    alt:'Deploy repair mines.',
    stats:{ mineDuration:20, mineCount:'= magazine size', mineRange:'scales with weapon range', dps:null, repairRate:null },
    notes:'Mine count is driven by the equipped launcher\'s magazine size, and mine range by the weapon range stat — both are indirections worth mirroring in the implementation. Mines cannot be attacked or destroyed. Swapping to a secondary deactivates deployed mines; swapping back reactivates them.' },

  { id:'shock-rail', name:'Shock Rail', frames:['electron'], damageType:'Energy', status:'partial',
    desc:'Fires an energy beam that applies a shock charge. At two charges the target detonates, consuming the charges and dealing additional damage to it and nearby enemies.',
    alt:'Aim down sights for increased accuracy.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+40%', chargeDuration:null, detonationDamage:null } },

  { id:'heavy-machine-gun', name:'Heavy Machine Gun', frames:['accord-dreadnaught'], damageType:'Kinetic', status:'doc',
    desc:'Spins up then fires rapidly, effective at short to medium range. Accuracy improves while maintaining continuous fire.',
    alt:'Spin up the barrel without firing.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, spinUpTime:null, accuracyRampTime:null } },

  { id:'rotary-blaster', name:'Rotary Blaster', frames:['mammoth'], damageType:'Kinetic', status:'doc',
    desc:'Fires high calibre, explosive rounds.',
    alt:'Spin up the barrel without firing.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:null, spinUpTime:null },
    notes:'Referred to as "Rotary Cannon" in patch 1.6.1934. Treat as the same weapon; pick one name for the codebase.' },

  { id:'photon-lance', name:'Photon Lance', frames:['rhino'], damageType:'Energy', status:'partial',
    desc:'Fires a beam that deals increasing damage the longer it is held.',
    alt:'Pre-heat the Photon Lance.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+20%', rampTime:null, rampMultiplier:null } },

  { id:'marksman-rifle', name:'Marksman Rifle', frames:['accord-recon'], damageType:'Kinetic', status:'partial',
    desc:'Deals precise damage to a single target at long range.',
    alt:'Aim down sights to fire with increased accuracy.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+40%', accuracy:null } },

  { id:'sniper-rifle', name:'Sniper Rifle', frames:['nighthawk'], damageType:'Kinetic', status:'partial',
    desc:'Deals direct damage and penetrates through up to five enemies or shields.',
    alt:'Aim down sights to fire with increased accuracy.',
    stats:{ dps:null, magazine:10, ammoCapacity:80, critBonus:'+50%', penetration:5 } },

  { id:'charge-rifle', name:'Charge Rifle', frames:['raptor'], damageType:'Energy', status:'partial',
    desc:'Automatically gains charge over time and expends all stored charge when fired.',
    alt:'Aim down sights to fire with increased accuracy.',
    stats:{ dps:null, magazine:null, ammoCapacity:null, critBonus:'+40%', chargeRate:null, maxCharge:null } }
],

/* -- ABILITIES ----------------------------------------------------------- */
abilities: [
  /* Assault line */
  { id:'meteor-strike', name:'Meteor Strike', archetype:'assault', frames:['accord-assault'], slot:'ability', status:'partial',
    desc:'Leap into the air and smash back down, knocking back and damaging enemies near the point of impact.',
    stats:{ cooldown:null, energyCost:null, damage:null, radius:null, damageReduction:'75% while active' } },
  { id:'overcharge', name:'Overcharge', archetype:'assault', frames:['accord-assault'], slot:'ability', status:'doc',
    desc:'Signature weapons generate no heat, rate of fire increases, and ammunition is unlimited for the duration.',
    stats:{ cooldown:null, energyCost:null, duration:null, rofBonus:null } },
  { id:'afterburner', name:'Afterburner', archetype:'assault', frames:['accord-assault','tigerclaw','firecat'], slot:'ability', status:'doc',
    desc:'Rocket forward at high speed in the direction you are facing.',
    stats:{ cooldown:null, energyCost:null, thrustForce:null, distance:null },
    frameVariants:{ firecat:'Leaves a trail of flame that damages nearby enemies.', tigerclaw:'Total thrust force increased by 50%.' },
    notes:'Shared across all three Assault frames with per-frame modifiers. Model as one ability with a variant table rather than three copies.' },
  { id:'shockwave', name:'Shockwave', archetype:'assault', frames:['accord-assault'], slot:'ultimate', status:'doc',
    desc:'Unleash a massive burst of energy, dealing extreme damage to all enemies in its path.',
    stats:{ chargeRequired:null, damage:null, range:null, width:null } },

  { id:'hellfire', name:'Hellfire', archetype:'assault', frames:['tigerclaw'], slot:'ability', status:'doc',
    desc:'Hover for the duration, launching a salvo of missiles at your target reticle for kinetic area damage.',
    stats:{ cooldown:null, energyCost:null, duration:null, missileCount:null, damagePerMissile:null } },
  { id:'pulsar', name:'Pulsar', archetype:'assault', frames:['tigerclaw'], slot:'ability', status:'doc',
    desc:'Fire a pulsar that attaches at the point of impact. Damaging the pulsar detonates it for massive energy damage in a large area. Attached to an enemy, it slows them and explodes after three seconds.',
    stats:{ cooldown:null, energyCost:null, damage:null, radius:null, attachedFuse:3, projectileSpeed:null } },
  { id:'supercharge', name:'Supercharge', archetype:'assault', frames:['tigerclaw'], slot:'ultimate', status:'partial',
    desc:'Grants unlimited jet energy and increased movement speed for the duration, and modifies your other abilities while active.',
    descSuperseded:'Grants unlimited jet energy, increases movement speed, and accelerates your ability cooldowns for the duration.',
    supersededBy:'1.6.1940',
    stats:{ chargeRequired:null, duration:12, speedBonus:null },
    notes:'While active: Pulsar gains a gravity field pulling enemies in; Hellfire fires twice as many missiles over the same duration. The cooldown-acceleration effect was removed.' },

  { id:'thermal-wave', name:'Thermal Wave', archetype:'assault', frames:['firecat'], slot:'ability', status:'partial',
    desc:'Channel a blast of flame that pierces all enemies in a line, dealing high thermal damage over the duration.',
    stats:{ cooldown:null, energyCost:null, range:30, coneAngle:null, dps:null, duration:null } },
  { id:'fuel-cloud', name:'Fuel Cloud', archetype:'assault', frames:['firecat'], slot:'ability', status:'doc',
    desc:'Launch a projectile that creates a slowing fuel cloud. Damaging the cloud ignites it, refreshing its duration and turning it into an inferno dealing thermal damage over time.',
    stats:{ cooldown:null, energyCost:null, radius:null, duration:null, slowAmount:null, ignitedDps:null } },
  { id:'fuel-air-bomb', name:'Fuel Air Bomb', archetype:'assault', frames:['firecat'], slot:'ultimate', status:'doc',
    desc:'Toss an explosive that deals high thermal damage and sets the target area on fire, burning enemies for several seconds.',
    stats:{ chargeRequired:null, impactDamage:null, burnDps:null, burnDuration:null, radius:null } },

  /* Biotech line */
  { id:'healing-generator', name:'Healing Generator', archetype:'biotech', frames:['accord-biotech'], slot:'ability', status:'doc',
    desc:'Deploy a generator that gradually heals nearby allies.',
    stats:{ cooldown:null, energyCost:null, duration:null, healPerSecond:null, radius:null, deployableHealth:null },
    notes:'Deployable. Loses health over time at a rate derived from its duration; repairable by an Engineer.' },
  { id:'poison-ball', name:'Poison Ball', archetype:'biotech', frames:['accord-biotech'], slot:'ability', status:'doc',
    desc:'Fire a ball of noxious chemicals that explodes on impact, poisoning enemies in the area. Poisoned enemies are slowed and take chemical damage over time.',
    stats:{ cooldown:null, energyCost:null, radius:null, dps:null, poisonDuration:null, slowAmount:null } },
  { id:'adrenaline-rush', name:'Adrenaline Rush', archetype:'biotech', frames:['accord-biotech','recluse','dragonfly'], slot:'ability', status:'doc',
    desc:'You and nearby allies gain improved movement speed and weapon rate of fire for the duration.',
    stats:{ cooldown:null, energyCost:null, duration:null, speedBonus:null, rofBonus:null, radius:null },
    notes:'Shared across all three Biotech frames in the PvP kit. Same shared-ability pattern as Afterburner.' },
  { id:'heroism', name:'Heroism', archetype:'biotech', frames:['accord-biotech'], slot:'ultimate', status:'doc',
    desc:'Slows nearby enemies while you and nearby allies gain a percentage of damage dealt as healing.',
    stats:{ chargeRequired:null, duration:null, lifestealPercent:null, slowAmount:null, radius:null } },

  { id:'healing-wave', name:'Healing Wave', archetype:'biotech', frames:['dragonfly'], slot:'ability', status:'doc',
    desc:'Fire a wave that heals friendly targets while damaging and knocking back enemies caught in it.',
    stats:{ cooldown:null, energyCost:null, healAmount:null, damage:null, range:null, knockback:null } },
  { id:'emergency-response', name:'Emergency Response', archetype:'biotech', frames:['dragonfly'], slot:'ability', status:'doc',
    desc:'Instantly teleport in the direction you are aiming, healing all nearby friendly targets on arrival.',
    stats:{ cooldown:null, energyCost:null, teleportRange:null, healAmount:null, healRadius:null } },
  { id:'healing-dome', name:'Healing Dome', archetype:'biotech', frames:['dragonfly'], slot:'ultimate', status:'doc',
    desc:'Project a dome that heals allies within its radius for the duration.',
    stats:{ chargeRequired:null, duration:null, healPerSecond:null, radius:null } },

  { id:'creeping-death', name:'Creeping Death', archetype:'biotech', frames:['recluse'], slot:'ability', status:'partial',
    desc:'Fire a shot that creates a cloud of poisonous chemicals at a fixed radius. Enemies within suffer chemical damage over the duration.',
    descSuperseded:'Fire a shot which creates a cloud of poisonous chemicals. Enemies within the cloud suffer chemical damage over the duration of the effect.',
    supersededBy:'1.6.1946',
    stats:{ cooldown:null, energyCost:null, radius:null, dps:null, duration:null },
    notes:'Three distinct states across 1.6. The growing-radius version shipped in 1940 was bugged — the radius never actually grew, so it only damaged enemies within 1m of the origin. 1946 abandoned growth entirely for a fixed radius applied immediately. Implement the 1946 behaviour; the growth mechanic was never working.' },
  { id:'poison-trail', name:'Poison Trail', archetype:'biotech', frames:['recluse'], slot:'ability', status:'doc',
    desc:'Fire a trail of poisonous gas that snares and deals chemical damage over time to enemies lingering in it.',
    stats:{ cooldown:null, energyCost:null, trailLength:null, duration:null, dps:null, snareAmount:null } },
  { id:'necrosis', name:'Necrosis', archetype:'biotech', frames:['recluse'], slot:'ultimate', status:'doc',
    desc:'Emit a sphere of toxic gas, dealing extreme chemical damage over time around you.',
    stats:{ chargeRequired:null, duration:null, dps:null, radius:null } },

  /* Engineer — deployables with patch history */
  { id:'heavy-turret', name:'Heavy Turret', archetype:'engineer', frames:['accord-engineer'], slot:'ability', status:'doc',
    desc:'Deploy an automated heavy turret that targets and fires on nearby enemies.',
    stats:{ cooldown:null, energyCost:null, duration:null, dps:null, deployableHealth:null, decayRate:null, targetRange:null },
    notes:'Deployable. Health decay rate was slowed in 1.6.1934.' },
  { id:'overclock', name:'Overclock', archetype:'engineer', frames:['accord-engineer','bastion','electron'], slot:'ability', status:'doc',
    desc:'Overclock your jumpjets, improving air control and granting unlimited jet energy for the duration.',
    stats:{ cooldown:null, energyCost:null, duration:null, airControlBonus:null } },
  { id:'supply-station', name:'Supply Station', archetype:'engineer', frames:['accord-engineer'], slot:'ability', status:'doc',
    desc:'Deploy a station that automatically generates health and ammunition power-ups.',
    stats:{ cooldown:null, energyCost:null, duration:null, spawnInterval:null, deployableHealth:null },
    notes:'Deployable, repairable by an Engineer.' },
  { id:'anti-personnel-turret', name:'Anti-Personnel Turret', archetype:'engineer', frames:['accord-engineer'], slot:'ultimate', status:'doc',
    desc:'Deploy a powerful turret that must be manually operated. You or an ally may interact with it to enter it.',
    stats:{ chargeRequired:null, duration:null, dps:null, deployableHealth:null } },
  { id:'multi-turrets', name:'Multi Turrets', archetype:'engineer', frames:['bastion'], slot:'ability', status:'doc',
    desc:'Deploy a small automated turret. A maximum of three may be active at once.',
    stats:{ cooldown:null, energyCost:null, duration:null, maxActive:3, dps:null, deployableHealth:null, decayRate:null },
    notes:'Health decay rate was slowed substantially in 1.6.1934.' },
  { id:'deployable-shield', name:'Deployable Shield', archetype:'engineer', frames:['bastion'], slot:'ability', status:'doc',
    desc:'Deploy a shield generator creating a one-way energy barrier that blocks incoming projectiles.',
    stats:{ cooldown:null, energyCost:null, duration:null, shieldHealth:null, decayRate:null, width:null },
    notes:'One-way: friendly fire passes out, enemy fire is blocked. Decay slowed and max shield health raised in 1.6.1940.' },
  { id:'fortify', name:'Fortify', archetype:'engineer', frames:['bastion'], slot:'ultimate', status:'doc',
    desc:'All of your deployables deal bonus damage and become invulnerable for the duration, then self-destruct when it expires.',
    stats:{ chargeRequired:null, duration:null, damageBonus:null } },
  { id:'bulwark', name:'Bulwark', archetype:'engineer', frames:['electron'], slot:'ability', status:'doc',
    desc:'Release a pulse of energy granting you and nearby allies a shield that absorbs incoming damage.',
    stats:{ cooldown:null, energyCost:null, shieldAmount:null, duration:null, radius:null },
    notes:'Could make the Electron invulnerable prior to the 1.6.1940 fix — worth a regression test in the reimplementation.' },
  { id:'boomerang-shot', name:'Boomerang Shot', archetype:'engineer', frames:['electron'], slot:'ability', status:'doc',
    desc:'Fire an energy projectile that returns at max range. Enemies hit take energy damage and become vulnerable; allies hit gain damage resistance.',
    stats:{ cooldown:null, energyCost:null, damage:null, maxRange:null, vulnerability:null, resistance:null, buffDuration:null } },
  { id:'electrical-storm', name:'Electrical Storm', archetype:'engineer', frames:['electron'], slot:'ultimate', status:'doc',
    desc:'Fire a slow-moving electrical storm that stuns and deals energy damage to all enemies in its path.',
    stats:{ chargeRequired:null, dps:null, travelSpeed:null, radius:null, stunDuration:null } },

  /* Dreadnaught line */
  { id:'heavy-armor', name:'Heavy Armor', archetype:'dreadnaught', frames:['accord-dreadnaught'], slot:'ability', status:'doc',
    desc:'Take reduced damage from all sources and gain a forward shield that completely blocks enemy projectiles for the duration.',
    stats:{ cooldown:null, energyCost:null, duration:null, damageReduction:null, shieldArc:null } },
  { id:'turret-mode', name:'Turret Mode', archetype:'dreadnaught', frames:['accord-dreadnaught'], slot:'ability', status:'doc',
    desc:'Become immobile in exchange for increased accuracy and rate of fire. Activate again to cancel.',
    stats:{ cooldown:null, energyCost:null, accuracyBonus:null, rofBonus:null },
    notes:'Toggled rather than timed — no duration field. Worth noting because it needs different state handling from every other ability.' },
  { id:'charge', name:'Charge', archetype:'dreadnaught', frames:['accord-dreadnaught','mammoth','rhino'], slot:'ability', status:'doc',
    desc:'Charge forward at high speed, knocking away enemies in your path and dealing damage in a circular area at the end of the charge.',
    stats:{ cooldown:null, energyCost:null, distance:null, speed:null, impactDamage:null, radius:null },
    notes:'Shared across all three Dreadnaught frames in the PvP kit; only the Accord frame lists it in PvE.' },
  { id:'absorption-bomb', name:'Absorption Bomb', archetype:'dreadnaught', frames:['accord-dreadnaught'], slot:'ultimate', status:'doc',
    desc:'Brace for impact, briefly gaining extreme damage resistance and taunting nearby enemies. When the resistance expires it explodes, dealing damage scaled to the amount absorbed.',
    stats:{ chargeRequired:null, duration:null, damageReduction:null, absorbToDamageRatio:null, radius:null, tauntRadius:null } },
  { id:'gravity-pull', name:'Gravity Pull', archetype:'dreadnaught', frames:['mammoth'], slot:'ability', status:'doc',
    desc:'Drag enemies in a forward cone to your location, dealing kinetic damage in the process.',
    stats:{ cooldown:null, energyCost:null, range:null, coneAngle:null, damage:null } },
  { id:'thunderdome', name:'Thunderdome', archetype:'dreadnaught', frames:['mammoth'], slot:'ability', status:'doc',
    desc:'Create a spherical energy shield around yourself that blocks enemy projectiles. Can be destroyed early by damage.',
    stats:{ cooldown:null, energyCost:null, duration:null, shieldHealth:null, radius:null } },
  { id:'dreadfield', name:'Dreadfield', archetype:'dreadnaught', frames:['mammoth'], slot:'ultimate', status:'doc',
    desc:'Project a field dealing kinetic damage over time that also weakens enemies within it, reducing the damage they deal.',
    stats:{ chargeRequired:null, duration:null, dps:null, radius:null, damageReduction:null } },
  { id:'penetrating-rounds', name:'Penetrating Rounds', archetype:'dreadnaught', frames:['rhino'], slot:'ability', status:'doc',
    desc:'Your primary weapon gains unlimited ammunition and penetrates through multiple targets for the duration.',
    stats:{ cooldown:null, energyCost:null, duration:null, penetration:null } },
  { id:'sundering-blast', name:'Sundering Blast', archetype:'dreadnaught', frames:['rhino'], slot:'ability', status:'doc',
    desc:'Release an explosion of energy from your reactor, snaring and dealing kinetic damage to enemies in the radius.',
    stats:{ cooldown:null, energyCost:null, damage:null, radius:null, snareAmount:null, snareDuration:null } },
  { id:'mighty-charge', name:'Mighty Charge', archetype:'dreadnaught', frames:['rhino'], slot:'ultimate', status:'doc',
    desc:'Charge forward at incredible speed, knocking aside and dealing extreme kinetic damage to enemies in your path.',
    stats:{ chargeRequired:null, distance:null, speed:null, damage:null } },

  /* Recon line */
  { id:'cryo-shot', name:'Cryo Shot', archetype:'recon', frames:['accord-recon'], slot:'ability', status:'doc',
    desc:'Fire a freezing projectile that damages and snares enemies on detonation, leaving a cryogenic field that slows any enemy entering it.',
    stats:{ cooldown:null, energyCost:null, damage:null, radius:null, snareDuration:null, fieldDuration:null, fieldSlow:null } },
  { id:'teleport-beacon', name:'Teleport Beacon', archetype:'recon', frames:['accord-recon','nighthawk','raptor'], slot:'ability', status:'doc',
    desc:'Throw a beacon that opens a portal at its impact point. Activate again to teleport to it.',
    stats:{ cooldown:null, energyCost:null, throwRange:null, portalDuration:null },
    notes:'Shared across all three Recon frames in the PvP kit; only the Accord frame lists it in PvE.' },
  { id:'remote-explosive', name:'Remote Explosive', archetype:'recon', frames:['accord-recon'], slot:'ability', status:'doc',
    desc:'Launch a mine that can be remotely detonated for kinetic area damage.',
    stats:{ cooldown:null, energyCost:null, damage:null, radius:null, mineDuration:null, maxActive:null } },
  { id:'artillery-strike', name:'Artillery Strike', archetype:'recon', frames:['accord-recon'], slot:'ultimate', status:'doc',
    desc:'Paint a target for an artillery strike, dealing massive kinetic damage over a large area.',
    stats:{ chargeRequired:null, damage:null, radius:null, delay:null } },
  { id:'decoy', name:'Decoy', archetype:'recon', frames:['nighthawk'], slot:'ability', status:'doc',
    desc:'Enter stealth, becoming invisible and untargetable. A holographic decoy is left behind that taunts enemies and explodes after a brief delay.',
    stats:{ cooldown:null, energyCost:null, stealthDuration:null, decoyFuse:null, decoyDamage:null, decoyRadius:null } },
  { id:'sin-beacon', name:'SIN Beacon', archetype:'recon', frames:['nighthawk'], slot:'ability', status:'doc',
    desc:'Deploy a beacon that adds nearby enemies to the Shared Intelligence Network, revealing them on the minimap and through walls. Tagged targets also become vulnerable, taking increased damage from all sources.',
    stats:{ cooldown:null, energyCost:null, duration:null, radius:null, vulnerability:null } },
  { id:'eruption', name:'Eruption', archetype:'recon', frames:['nighthawk'], slot:'ultimate', status:'doc',
    desc:'For the duration, enemies you kill explode for additional thermal damage in a radius.',
    stats:{ chargeRequired:null, duration:null, explosionDamage:null, radius:null } },
  { id:'smoke-screen', name:'Smoke Screen', archetype:'recon', frames:['raptor'], slot:'ability', status:'doc',
    desc:'Deploy a smoke screen. Allies passing through gain damage reduction; enemies caught inside suffer a weapon accuracy penalty.',
    stats:{ cooldown:null, energyCost:null, duration:null, radius:null, allyDamageReduction:null, enemyAccuracyPenalty:null } },
  { id:'assassinate', name:'Assassinate', archetype:'recon', frames:['raptor'], slot:'ability', status:'doc',
    desc:'Teleport to a targeted enemy and strike with a devastating melee attack. A kill immediately refreshes the cooldown.',
    stats:{ cooldown:null, energyCost:null, range:null, damage:null },
    notes:'Cooldown refresh on kill is a reset, not a reduction — needs an explicit kill hook rather than a cooldown modifier.' },
  { id:'overload', name:'Overload', archetype:'recon', frames:['raptor'], slot:'ultimate', status:'doc',
    desc:'For the duration, enemies you shoot unleash an explosion of energy for additional damage. Any individual enemy can trigger this at most once per second.',
    stats:{ chargeRequired:null, duration:null, explosionDamage:null, radius:null, perTargetIcd:1 } }
],

/* -- PERKS (sample — see the Perks note for the full-import plan) --------- */
perks: [
  { id:'expanded-munitions', name:'Expanded Munitions', unlock:'Any battleframe — level 1', category:'universal', status:'doc',
    effect:'Increases total ammo capacity by 20%. Additive with other ammo capacity increases from perks or gear.' },
  { id:'extra-plating', name:'Extra Plating', unlock:'Any battleframe — level 1', category:'universal', status:'doc',
    effect:'Increases bonus health by 8%.' },
  { id:'optimized-targeting', name:'Optimized Targeting', unlock:'Any battleframe — level 1', category:'universal', status:'doc',
    effect:'Increases all damage dealt by 2%.' },
  { id:'prototype-pistons', name:'Prototype Pistons', unlock:'Any battleframe — level 1', category:'universal', status:'doc',
    effect:'Increases base movement speed by +0.25 m/s.' },
  { id:'microcompression-suspension', name:'Microcompression Suspension', unlock:'Any advanced battleframe — level 20', category:'universal', status:'doc',
    effect:'Absorbs ground impact energy, allowing safe landings from great heights in the event of jumpjet or pilot failure.' },

  { id:'plasma-enthusiast', name:'Plasma Enthusiast', unlock:'Accord Assault — level 15', category:'assault', status:'doc',
    effect:'Thermal weapons and abilities deal 10% additional damage.' },
  { id:'searing-flames', name:'Searing Flames', unlock:'Firecat — level 20', category:'assault', status:'doc',
    effect:'Dealing thermal damage reduces the target\'s damage by 2% for 5 seconds, stacking to 20%.' },
  { id:'epicenter', name:'Epicenter', unlock:'Firecat — level 25', category:'assault', status:'doc',
    effect:'Increases damage dealt by area effect abilities that emanate from you by 15%.' },
  { id:'plasma-scoops', name:'Plasma Scoops', unlock:'Tigerclaw — level 25', category:'assault', status:'doc',
    effect:'After taking thermal damage, grants 80 energy per second regeneration for 4 seconds. Once per 10 seconds.' },
  { id:'invigorate', name:'Invigorate', unlock:'Advanced Assault frame — level 30', category:'assault', status:'doc',
    effect:'Activating any ability increases movement and attack speed by 15% for 6 seconds for you and allies within 8m.',
    notes:'Was decreasing rate of fire rather than increasing it until 1.6.1946. Sign error — check yours.' },
  { id:'corpse-combustion', name:'Corpse Combustion', unlock:'Firecat — level 35', category:'assault', status:'partial',
    effect:'Killed enemies burst into flames for 6 seconds, dealing 40 hp/s (at level 1) to enemies within 3m. Once per 3 seconds.' },
  { id:'second-wind', name:'Second Wind', unlock:'Advanced Assault frame — level 40', category:'assault', status:'doc',
    effect:'Killing 5 or more enemies within 5 seconds while below 35% health heals you for 60% of maximum health.' },

  { id:'sure-shot', name:'Sure Shot', unlock:'Nighthawk — level 35', category:'recon', status:'doc',
    effect:'Dealing weapon damage has a 10% chance to trigger a 50% rate of fire boost for 3 seconds. Once per 10 seconds.',
    notes:'Same sign error as Invigorate, fixed in 1.6.1946.' },
  { id:'hardware-genius', name:'Hardware Genius', unlock:'Bastion — level 25', category:'engineer', status:'doc',
    effect:'Increases damage dealt to deployables, machinery and vehicles by 20%.' },

  /* The duplicate-name problem: same perk name, two sources, different numbers. */
  { id:'dancing-flames-vendor', name:'Dancing Flames', unlock:'ARES Supplier', category:'vendor', status:'doc', collides:'dancing-flames-achievement',
    effect:'Thermal weapon damage has a 50% chance to splash onto an enemy within 10m, setting them on fire for 3 seconds. Once per second.' },
  { id:'dancing-flames-achievement', name:'Dancing Flames', unlock:'Achievement', category:'achievement', status:'doc', collides:'dancing-flames-vendor',
    effect:'Thermal weapon damage has a 50% chance to splash onto an enemy within 5m, dealing 40 damage per second (at level 1) over 3 seconds. Once per second.' },
  { id:'melding-infection-vendor', name:'Melding Infection', unlock:'ARES Supplier', category:'vendor', status:'doc', collides:'melding-infection-achievement',
    effect:'Melding weapon damage reduces the target\'s damage by 20% for 5 seconds.' },
  { id:'melding-infection-achievement', name:'Melding Infection', unlock:'Achievement', category:'achievement', status:'doc', collides:'melding-infection-vendor',
    effect:'Melding weapon damage reduces the target\'s damage by 10% for 5 seconds.' },

  { id:'arcing-bolts', name:'Arcing Bolts', unlock:'UNKNOWN', category:'unknown', status:'missing',
    effect:'No description exists in any 1.6 document.',
    notes:'Referenced only once, in the 1.6.1946 bug-fix list ("was doing too little damage"), and absent from every perk table. Either it was cut from the notes or it belongs to a frame whose list is incomplete. Flagged so it does not silently disappear.' }
],

/* -- PATCHES: ordered deltas applied on top of the 1.6 base state --------- */
patches: [
  { id:'1.6.0', name:'Update 1.6 — Base', date:'Base state', kind:'base',
    summary:'The three-part update notes. Everything below is a delta applied on top of this.',
    entries:[] },

  { id:'1.6.1934', name:'Patch 1.6.1934', date:'Status Update #2', kind:'patch',
    summary:'Combat balance pass, heavy bug-fix sweep, 30-plus encounter crashes.',
    entries:[
      { ref:'phason-thrower', type:'change', text:'Base critical bonus damage reduced from 30% to 20%. Average DPS increased by 5%.' },
      { ref:'photon-lance',   type:'change', text:'Base critical bonus damage reduced from 30% to 20%.' },
      { ref:'shock-rail',     type:'change', text:'Base critical bonus damage increased from 30% to 40%.' },
      { ref:'bolt-driver',    type:'change', text:'Base critical damage increased from 30% to 40%.' },
      { ref:'charge-rifle',   type:'change', text:'Base critical damage increased from 30% to 40%.' },
      { ref:'marksman-rifle', type:'change', text:'Average DPS increased by ~10%. Base critical damage increased from 30% to 40%. Base accuracy significantly improved.' },
      { ref:'sniper-rifle',   type:'change', text:'Base damage was less than half of intended and now deals the correct amount. Critical damage increased from 30% to 50%. Magazine 5 to 10. Max ammo 40 to 80.' },
      { ref:'smart-blaster',  type:'change', text:'Now consumes 1 ammo per shot instead of 5; base magazine reduced from 20 to 10. Net effect: shots between reloads rise from 4 to 10.' },
      { ref:'meteor-strike',  type:'fix',    text:'No longer stuck in the ability animation when bouncing off angled terrain. Can now hit enemies mid-air. Gained a targeting reticle.' },
      { ref:'heavy-turret',   type:'change', text:'Health now decays at a slightly slower rate.' },
      { ref:'multi-turrets',  type:'change', text:'Health now decays at a much slower rate.' },
      { ref:'mine-launcher',  type:'fix',    text:'Mines no longer spawn excessive particle effects, which had caused severe performance loss when several were deployed and repairing.' },
      { ref:null, type:'change', text:'Camera jerking during weapon fire removed (applies to PvP weapons as well).' },
      { ref:null, type:'change', text:'Perks that reduce ability cooldowns now provide a 10% cooldown reduction.' },
      { ref:null, type:'fix',    text:'Third-person carry animations for Assault Rifle, Burst Rifle and SMG no longer stuck in the aim-down-sights pose.' }
    ] },

  { id:'1.6.1940', name:'Patch 1.6.1940', date:'Status Update #4', kind:'patch',
    summary:'Two full reworks — Bastion\'s primary weapon and Tigerclaw\'s ultimate. French and German localisation restored.',
    entries:[
      { ref:'mine-launcher', type:'rework', text:'Redesigned. Primary fire deploys arc mines that beam nearby enemies over time; alternate fire deploys repair mines that prioritise the most damaged deployable. Mines last 20 seconds and cannot be attacked or destroyed. Count is set by magazine size, range by the weapon range stat. Swapping weapons deactivates and reactivates them.' },
      { ref:'supercharge', type:'rework', text:'No longer reduces the cooldown of other abilities. Base duration increased from 10 to 12 seconds. Now modifies other Tigerclaw abilities while active: Pulsar gains a gravity field pulling enemies in; Hellfire fires twice as many missiles over the same duration.' },
      { ref:'thermal-wave', type:'change', text:'Base range increased from 15 to 30 metres. Slightly wider cone of effect. Visual effects improved.' },
      { ref:'pulsar', type:'change', text:'Projectile speed increased by approximately 50%.' },
      { ref:'creeping-death', type:'change', text:'Now affected by the effect radius statistic. Radius grows over the ability duration. Damage significantly increased.' },
      { ref:'deployable-shield', type:'change', text:'Decay rate slowed significantly. Maximum shield health slightly increased.' },
      { ref:'sniper-rifle', type:'fix', text:'No longer occasionally deals no damage when striking an enemy.' },
      { ref:'bulwark', type:'fix', text:'No longer able to make the Electron invulnerable.' },
      { ref:'hardware-genius', type:'fix', text:'Now correctly deals increased damage against machines.' }
    ] },

  { id:'1.6.1942', name:'Patch 1.6.1942', date:'Status Update #5', kind:'patch',
    summary:'Migration compensation and encounter stability. No combat balance changes.',
    entries:[
      { ref:null, type:'change', text:'Compensation crates granted for pre-1.6 cores; two Titan Tokens per pre-1.6 Kanaloa weapon. Token cap is 8 — spend before salvaging.' },
      { ref:null, type:'change', text:'Zone transfer logic reworked; /joinleader and instance transitions should no longer fail.' },
      { ref:null, type:'fix', text:'Players should no longer be missing perks caused by the progression migration.' },
      { ref:null, type:'fix', text:'Nian difficulty now scales with participant count.' }
    ] },

  { id:'1.6.1946', name:'Patch 1.6.1946', date:'Final 1.6 patch', kind:'patch',
    summary:'Two sign-error perk fixes and the final Creeping Death decision.',
    entries:[
      { ref:'creeping-death', type:'rework', text:'Now has a fixed range. The growing radius from 1940 never actually increased, so it only damaged enemies within 1 metre of the origin. Changed to deal damage across its full area of effect immediately.' },
      { ref:'mine-launcher', type:'fix', text:'No longer fails to repair Deployable Shield.' },
      { ref:'arcing-bolts', type:'fix', text:'Was doing too little damage.' },
      { ref:'sure-shot', type:'fix', text:'Now correctly increases rate of fire — it was previously decreasing it.' },
      { ref:'invigorate', type:'fix', text:'Now correctly increases rate of fire — it was previously decreasing it.' },
      { ref:null, type:'change', text:'Reduced maximum wandering encounters in Sertao but increased their spawn rate; reduced ambient NPC count. Both to lower total NPC count so critical job NPCs spawn reliably.' }
    ] }
],

/* -- SYSTEMS: prose reference pages -------------------------------------- */
systems: [
  { id:'progression', name:'Progression', status:'doc', body:[
    { h:'The shape of a character', p:'A character picks one of five archetypes and levels its Accord frame from 1 to 20. At 20 the frame stops earning experience entirely until the player travels to the Advanced Battlelab at Trans-hub, tests both advanced frames for that archetype, and commits to one. From there the chosen advanced frame levels to 40. Levelling is per-battleframe, not per-character, so a player who owns several frames levels each independently.' },
    { h:'Why the hard stop at 20 matters', p:'The experience block is not a soft nudge — it is the mechanism that guarantees every player physically tries both advanced frames before choosing. If you reimplement this as an optional prompt, players will drift past it and the branch loses its meaning. The Battlelab is a tutorial disguised as a gate.' },
    { h:'Elite Ranks', p:'At maximum level the experience bar changes colour and further experience feeds Elite Ranks, a per-battleframe advancement track with a static experience cost per rank. Each rank presents a choice of six upgrades drawn from a basic pool, with a chance for one slot to be replaced by a rare-pool upgrade. Every tenth rank the entire selection is drawn from the rare pool.' },
    { h:'Elite Rank pools', p:'Basic: reload speed, ammo capacity, health, health regen, healing dealt, deployable health, healing received, jump height, jet energy, run speed, jet energy regen. Rare: battleframe unlocks, perks from other battleframes, maximum perk point increases, power rating, weapon damage, ability recharge rate, ability damage, ultimate charge rate, or lump sums of Crystite or credits.' },
    { h:'The design intent', p:'The guaranteed rare selection every tenth rank is the load-bearing piece. It converts a random-reward system into one with a floor, so even an unlucky player reaches the exciting outcomes — free battleframes, cross-frame perks — on a predictable schedule. Random rewards with no floor produce players who feel cheated; this structure produces players who count to ten.' }
  ]},

  { id:'gear', name:'Gear and slots', status:'doc', body:[
    { h:'What replaced cores', p:'The single battleframe core was split into five primary slots: Head, Torso, Arms, Legs, and Reactor. The first four contribute to total health; the Reactor supplies a large power rating bonus. Higher rarity items additionally carry weapon and ability stats.' },
    { h:'Secondary slots', p:'Four more slots were added. The Operating System carries stats that apply across every ability at once — an OS with cooldown reduction reduces all ability cooldowns. The Medical System governs passive health regeneration and provides a burst heal on a dedicated hotkey; it can also carry reduced recharge time, increased heal strength, bonus shielding, or temporary defence. Two Gadget slots hold activated effects, such as summoning NPC defenders or giving grenade auxiliaries a chance not to enter cooldown.' },
    { h:'Auxiliary weapon', p:'One slot, two families. Melee: Brawling (strengthens the weapon-butt attack), Energy Swords (fast, arc damage in front), Battle Hammers (slow wind-up, high damage, area effect). Grenades: Fragmentation, Incendiary (damage over time), Freeze (slow), Chemical (acid pool), Tesla (electrical field, electrocuted enemies deal reduced damage).' },
    { h:'Rarity is a level offset', p:'Rarity does not multiply stats; it shifts effective item level. Common equals its item level, Uncommon equals +5 levels, Rare +10, Epic +15. This is worth copying exactly — it means one stat curve drives all rarities, and rarity becomes a single integer offset rather than a separate balance table per tier.' },
    { h:'Corporation bonuses', p:'Every item carries a manufacturer with a fixed bonus. Accord: weapon handling and reload speed. Omnidyne-M: maximum energy. Astrek: energy recharge rate. Kisuton: maximum ammunition.' }
  ]},

  { id:'builds', name:'How builds actually worked', status:'doc', body:[
    { h:'Four independent levers', p:'A build is the product of four systems that do not overlap: the frame choice (fixed weapon and ability kit), perks (levelled unlocks plus purchased and earned ones), gear (nine slots of stats plus corporation bonuses), and tinkering (up to nine upgrade ranks per weapon, ability, or core, plus gold stars). Nothing in the frame kit is swappable — you cannot mix Firecat abilities onto a Tigerclaw. Customisation lives entirely in the other three levers.' },
    { h:'Perks are the identity layer', p:'Perks unlock at levels 1, 10, 13, 15, 20, 25, 30, 35 and 40, gated to specific frames. Advanced-frame perks at 30 and 40 are shared between both branches of an archetype; the 20/25/35 perks are branch-exclusive. Layered on top are perks bought from the ARES Supplier with reputation, granted by Elite Ranks, and earned through achievements. Elite Ranks can also grant perks belonging to other battleframes, which is the only cross-frame leak in the system.' },
    { h:'Tinkering', p:'Weapons, abilities and cores upgrade up to nine times, shown as a +x suffix. Each upgrade adds a flat base bonus and raises all relevant ratings. Upgrades can roll a critical result that adds a gold star, which grants extra stats permanently. Optional ingredients raise gold star chance. A maxed item can have its rank reset — the +x drops to the number of gold stars already earned, keeping the stars while freeing up rolls to chase more.' },
    { h:'Why the reset rule is clever', p:'Resetting to the star count rather than to zero means progress is never destroyed, only the portion that was guaranteed. It gives a player at the ceiling something to keep doing without a treadmill that punishes them, and it makes gold stars the real long-tail chase rather than the +9. Worth preserving.' },
    { h:'Modules', p:'Modules no longer have levels or level requirements and function in both weapons and abilities. Combining one module with two others of the same colour and tier yields the next tier up. Combining two modules of the same tier but different colours yields a dual-stat module of that tier. Combining two different-stat modules of the same colour into a dual-stat module is not possible.' }
  ]},

  { id:'jetball', name:'Jetball (PvP)', status:'doc', body:[
    { h:'Format', p:'Two teams, ten minute match, highest score wins. Players spawn in a team room with a battleframe terminal and may switch to any frame they own for PvE at any point during the match by returning to the terminal. All PvP loadouts use regulation gear, so equipment is equalised and only frame choice and skill vary.' },
    { h:'Scoring', p:'One point for carrying the ball through the goal ring. Two points for throwing it through from more than 10m away. Four points for throwing it through from a marked circle in a side alcove. After a score, the scoring team must leave the enemy goal room quickly or automated defences activate.' },
    { h:'The carrier rule', p:'A carrier cannot attack or use abilities. Using an ability immediately drops the ball, though the carrier may pick it back up. Primary fire throws the ball and can be charged for distance; alternate fire locks onto a teammate for a homing pass. This is the entire tension of the mode — the ball converts a combatant into a target who depends on escorts.' },
    { h:'Standardised kits', p:'PvP loadouts are not the PvE kits. Each frame gets a fixed primary, a secondary tied to its archetype (Assault: Burst Rifle, Biotech: SMG, Engineer: Assault Rifle, Dreadnaught: Grenade Launcher, Recon: Shotgun), an Energy Sword auxiliary, and a fixed three abilities plus ultimate. Several frames get abilities they do not carry in PvE — Recluse and Dragonfly both receive Adrenaline Rush, and all three Recon frames receive Teleport Beacon.' },
    { h:'Ranks', p:'PvP rank is account-wide across all characters and frames, capped at 50 (Commander), earned through Rank Points. Ranking up unlocks additional secondary weapons for the PvP loadout. PvP also awards PvE experience, Crystite and PvE equipment scaled to battleframe level and time participated, with a bonus percentage for the winning team.' }
  ]}
],

/* -- KNOWN GAPS: things absent from the source entirely ------------------ */
absent: [
  { area:'Ability numbers', detail:'No cooldowns, energy costs, durations, radii or damage figures appear anywhere in the 1.6 notes. Every ability page has null stats until you pull them from game data.' },
  { area:'Frame base stats', detail:'Health, movement speed, jet energy and jet recharge exist only as tier words (Standard / High / Very High). The tier-to-value mapping is unrecovered.' },
  { area:'Secondary weapons', detail:'Burst Rifle, SMG, Assault Rifle, Shotgun and Grenade Launcher are named in the PvP kits and nowhere described. PvP ranks are said to unlock more of them, with no list of which unlocks at which rank.' },
  { area:'Damage type interactions', detail:'Six types are named — Thermal, Chemical, Cryogenic, Energy, Melding, Kinetic — with no resistance or effectiveness table. Only the flat 10% resistance perks imply a system.' },
  { area:'Bestiary', detail:'Chosen types and creatures appear only incidentally in bug fixes: Nian, Brontodon King, Big Brother, Tarantus, Aliyan, Yuki Lin, Chosen Darkslips, skivers, Reapers.' },
  { area:'Missions', detail:'Twenty core missions are listed by name only. No objectives, boss mechanics, level requirements or reward tables.' },
  { area:'Elite Rank tables', detail:'The upgrade pools are described but there is no rank-by-rank table and no experience cost per rank.' },
  { area:'Item catalogue', detail:'Slots and the rarity offset rule are documented; no actual gear list, stat ranges or drop sources.' },
  { area:'Achievements', detail:'Four perks are gated behind achievements that are never listed.' },
  { area:'Controls', detail:'Only K (activities / dashboard) and H (bounties) are documented.' },
  { area:'Zones', detail:'Coral Forest and Sertao points of interest are named but there is no geography, no map, no level ranges per region.' }
]

};

/* ==========================================================================
   BUILD OVERRIDES
   Where the reimplementation departs from what shipped. Keyed by entity id.
   The wiki renders these as a diff beside the original; it never replaces it.
   Delete the example once you have real ones.
   ========================================================================== */
const BUILD_OVERRIDES = {
  'creeping-death': {
    shipped: 'Fixed radius, applied immediately (1.6.1946), after the growing radius shipped in 1940 turned out never to grow.',
    build:   'Growing radius, implemented so that it actually grows over the ability duration.',
    rationale: 'The 1940 design was the intent and the 1946 change was a retreat from a bug, not a decision that the mechanic was bad. Building the working version is a departure from what shipped and is recorded as one.'
  }
};
