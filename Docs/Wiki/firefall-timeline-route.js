/* ==========================================================================
   FIREFALL WIKI — TIMELINE ROUTE (drop-in)
   --------------------------------------------------------------------------
   Adds a #/timeline page to the existing reference wiki. Static lore, so the
   view is a plain template string — it doesn't read from DATA like the entity
   views do. Uses the wiki's own helpers (pnl, and the shared .pnl/.tab/.note
   classes) so it inherits the house style.

   THREE SMALL WIRING STEPS — all in index.html:

   1. FONT  — add Orbitron and a display token (project standard going forward).
        In <head>:
          <link rel="preconnect" href="https://fonts.googleapis.com">
          <link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@500;600;700;800&display=swap" rel="stylesheet">
        In :root, add:
          --f-display:'Orbitron','Rajdhani','Roboto Condensed',system-ui,sans-serif;
        Then point the display faces at it (h1 and entity titles read as
        Firefall chapter headings this way; leave h2 on the mono face — it's
        doing system-label duty):
          h1{font-family:var(--f-display);letter-spacing:.01em}
          h3{font-family:var(--f-display)}
          .brand b{font-family:var(--f-display);letter-spacing:.30em}

   2. CSS   — paste the STYLE ADDITIONS block into the wiki's <style>.

   3. ROUTE — paste the V.timeline function in with the other V.* views, add
        one nav link, and register one route (see ROUTER + NAV at the bottom).
   ========================================================================== */


/* ======================= STYLE ADDITIONS (into <style>) ===================
   The wiki already defines --jade, --violet, --rust and --cyan-low. These add
   the tint/edge variants the timeline needs, plus a namespaced .ltl timeline
   so it never collides with the existing patch-history .tl.

:root{
  --jade-mid:rgba(95,211,95,.4);  --jade-low:rgba(95,211,95,.09);
  --violet-mid:rgba(155,107,201,.4);
}

.pnl.veil{background:var(--violet-mid)}
.pnl.veil>.in{background:linear-gradient(160deg,#130d1c 0%,var(--panel) 70%)}
.pnl.hold{background:var(--jade-mid)}
.pnl.hold>.in{background:linear-gradient(160deg,#0c1a12 0%,var(--panel) 72%)}

.tab.v{background:var(--violet-mid)}  .tab.v>span{color:var(--violet)}
.tab.j{background:var(--jade-mid)}    .tab.j>span{color:var(--jade)}
.note.won{border-left-color:var(--jade);background:var(--jade-low)}
.note.won .eyebrow{color:var(--jade)}

.tml-legend{display:flex;flex-wrap:wrap;gap:18px;margin:16px 0 6px;font-family:var(--f-mono);
  font-size:11px;letter-spacing:.06em;color:var(--mute)}
.tml-legend span{display:inline-flex;align-items:center;gap:9px}
.tml-legend i{width:10px;height:10px;transform:rotate(45deg);background:var(--void);border:1px solid var(--cyan)}
.tml-legend i.pivot{background:var(--amber);border-color:var(--amber);box-shadow:0 0 10px rgba(242,160,30,.6)}
.tml-legend i.enigma{border-color:var(--violet);box-shadow:0 0 10px rgba(155,107,201,.5)}
.tml-legend i.won{border-color:var(--jade);box-shadow:0 0 10px rgba(95,211,95,.5)}

.ltl{list-style:none;position:relative;margin:22px 0 0;padding-left:36px}
.ltl::before{content:'';position:absolute;left:9px;top:12px;bottom:12px;width:1px;
  background:linear-gradient(180deg,transparent,var(--cyan-mid) 6%,var(--cyan-mid) 94%,transparent)}
.ltl>li{position:relative;padding-bottom:28px}
.ltl>li:last-child{padding-bottom:0}
.ltl>li::before{content:'';position:absolute;left:-31px;top:9px;width:12px;height:12px;
  background:var(--void);border:1px solid var(--cyan);transform:rotate(45deg)}
.ltl>li.pivot::before{background:var(--amber);border-color:var(--amber);box-shadow:0 0 14px rgba(242,160,30,.65)}
.ltl>li.enigma::before{border-color:var(--violet);box-shadow:0 0 14px rgba(155,107,201,.5)}
.ltl>li.won::before{border-color:var(--jade);box-shadow:0 0 14px rgba(95,211,95,.5)}
.ltl .when{font-family:var(--f-mono);font-size:10.5px;letter-spacing:.16em;text-transform:uppercase;color:var(--amber);margin-bottom:8px}
.ltl>li.enigma .when{color:var(--violet)}
.ltl>li.won .when{color:var(--jade)}
.ltl h3{font-family:var(--f-display);font-weight:600;margin-bottom:7px}
.ltl .aside{font-size:12.5px;color:var(--mute);margin-top:10px;padding-left:12px;border-left:1px solid var(--cyan-mid)}
.ltl>li.enigma .aside{border-left-color:var(--violet-mid)}
.ltl>li.won .aside{border-left-color:var(--jade-mid)}

======================= END STYLE ADDITIONS ============================= */


/* ============================ THE VIEW ==================================== */
V.timeline = () => `
  <div class="crumb"><a href="#/">Home</a> / Timeline</div>
  <h1>Timeline of the World</h1>
  <p class="lede">The road from a rock falling out of the sky to the war you fought as a recruit.
  Dates before the crash are approximate — the record never agreed with itself, and the game was in
  no hurry to settle it. Some entries are things the game showed you and then refused to explain;
  those are marked apart on purpose.</p>

  <div class="tml-legend">
    <span><i></i> Event</span>
    <span><i class="pivot"></i> Inciting incident</span>
    <span><i class="enigma"></i> Never explained</span>
    <span><i class="won"></i> Ground reclaimed</span>
  </div>

  <h2>Before the eye of the storm</h2>
  <ul class="ltl">
    <li>
      <div class="when">c. 2178 · The Firefall</div><h3>Fire falls from the sky</h3>
      <p>An asteroid tracked as a near miss is caught by the Moon's gravity and comes down instead.
      The impact throws the world into a nine-year winter; the old powers, the United States among
      them, do not survive it. The game takes its name from this — the fire that rained down — though
      by the time you play it is generations gone and half legend.</p>
    </li>
    <li>
      <div class="when">The dark age · after the impact</div><h3>Crystite is found in the ash</h3>
      <p>Survivors pull a blue crystalline mineral from the impact debris that gives up more energy
      than anything before it. <b>Crystite</b> becomes the thing the rebuilt world runs on — its
      power, its currency, and eventually its undoing. Everything downstream traces back to it.</p>
    </li>
    <li>
      <div class="when">A generation on · The Crystite Wars</div><h3>The scramble becomes a war</h3>
      <p>A power source that good is also a prize worth killing for. Control of Crystite fractures the
      recovering world into open conflict — the fire everyone was rebuilt out of, and the one no one
      wants to light again.</p>
    </li>
    <li>
      <div class="when">The peace · The Accord</div><h3>The nations fold into one</h3>
      <p>To keep humanity from burning its second chance the way it burned the first, the surviving
      states merge into <b>the Accord</b> — a single government whose founding job is to keep Crystite
      from starting another war. It is the flag you fight under, and it has been overstretched from
      the day it was raised.</p>
    </li>
    <li>
      <div class="when">The reach outward · Alpha Prime</div><h3>The Arclight is built</h3>
      <p>The richest Crystite lies off-world, at <b>Alpha Prime</b> in the Alpha Centauri system. To
      hold that supply line the Accord builds the <b>CMS Arclight</b> — the largest ship ever made,
      able to fold space itself and cross the gulf in a single jump.</p>
      <p class="aside">The ships that ran that route brought back more than cargo. Some of the
      wildlife you fight in the wastes never evolved on Earth.</p>
    </li>
  </ul>

  <h2>The crash</h2>
  <ul class="ltl">
    <li class="pivot">
      <div class="tab"><span>Inciting incident</span></div>
      <div class="when">c. 2233 · The maiden fold</div><h3>The Arclight comes down</h3>
      ${pnl(`<p>On its first fold, something fails. The jump tears — and something comes through the
        tear with the ship. Unable to complete the fold, the Arclight falls back toward the world it
        never finished leaving and comes down along the Brazilian coast.</p>
        <p>Its hull is still there. Half-submerged off <b>Copacabana</b>, too vast to move and too
        strange to pull apart, it sits on the horizon of everything that follows. Every job you take,
        every line you hold, happens in the shadow of that wreck. This is where the game begins.</p>`, 'lit')}
      <p class="aside">The officer who rode it down lived. The people he was bringing home did not.
      He commands the Accord's war now.</p>
    </li>
  </ul>

  <h2>What came out of it</h2>
  <ul class="ltl">
    <li class="enigma">
      <div class="tab v"><span>Never explained</span></div>
      <div class="when">After the crash · The Melding</div><h3>The Melding</h3>
      ${pnl(`<p>Out of the tear came the <b>Melding</b>: an energy storm the colour of Crystite that
        spread across the sky and did not stop. It rewrites whatever it touches — stone, forest,
        animal, person — and swallowed nearly the entire planet. Only the eye of it stayed clear.</p>
        <p>What it is, whether it thinks, whether it wants anything — no one who claimed to know was
        ever proven right. Walk too far in and the world distorts, then dims, then kills you. That is
        the whole of what the game will confirm.</p>`, 'veil')}
    </li>
    <li class="enigma">
      <div class="tab v"><span>Never explained</span></div>
      <div class="when">Shortly after · The Chosen</div><h3>The Chosen</h3>
      ${pnl(`<p>Then the storm gave up an enemy. The <b>Chosen</b> walked out of the Melding armed,
        organised, and fixed on one purpose: the end of humankind. They field war-machines the size
        of buildings and they do not negotiate.</p>
        <p>Where they come from, what they were before, why they hate — the game never tells you, and
        the people fighting them never find out. They are simply the thing the storm sent.</p>`, 'veil')}
    </li>
  </ul>

  <h2>Where you come in</h2>
  <ul class="ltl">
    <li>
      <div class="when">Present day · New Eden</div><h3>The eye of the storm</h3>
      <p>What's left of humanity holds a strip of survivable Brazil called <b>New Eden</b>, ringed by
      the Melding on every side. The Accord — gutted since the crash, far too few — cannot hold the
      line with soldiers alone.</p>
      <p>So it opens the <b>ARES Initiative</b> and hands power armour to anyone who will fight. A
      <b>Battleframe</b>: health that knits itself back together, jets to put you over the wreckage,
      wings to glide the rest. That's you — not the first to wear the number, not the last, just the
      one holding it now.</p>
    </li>
  </ul>

  <h2>The war you fought</h2>
  <ul class="ltl">
    <li>
      <div class="when">Razor's Edge · The Razorwind threat</div><h3>The Chosen get a name</h3>
      <p>The faceless mass sharpens into tribes, and the one thrown at you first is the
      <b>Razorwind</b>. <b>Dredge</b>, an outpost the Accord cannot afford to lose, comes under a
      Titan-led siege that takes a full platoon to hold. At <b>Jericho</b> you stand over a
      <b>Melding Repulsor</b> — the technology that physically holds the storm at bay — and defend it
      against wave after wave.</p>
      <p class="aside">Naming a tribe is not understanding one. You learn how a Razorwind fights.
      You never learn what it is.</p>
    </li>
    <li class="won">
      <div class="tab j"><span>Ground reclaimed</span></div>
      <div class="when">Razor's Edge → Devil's Due · The War Effort</div><h3>Into Devil's Tusk</h3>
      ${pnl(`<p>Down the coast lies <b>Devil's Tusk</b>, a lava-scarred zone the Melding took long ago
        — and buried beneath it, a <b>pre-Melding weapons research facility</b> the Accord means to
        reach first. The battlecruiser <b>U.A.S. Vanguard</b> anchors offshore to force the issue.</p>
        <p>Here you do the one thing that happens nowhere else in the game: you push the Melding
        <i>back</i>. <b>Dark Crystite</b> torn from the zone is refined into cores that feed a
        collective <b>War Effort</b>, and as the cores stack up the storm-line recedes and lost ground
        returns. A leaderboard keeps count of who bled most to move it.</p>`, 'hold')}
      <p class="aside">Every other front is a holding action. This is the only map where the arrow
      points the Accord's way.</p>
    </li>
    <li>
      <div class="when">Devil's Due · The Chosen Prison</div><h3>The prison on the coast</h3>
      <p>The Accord learns its missing people are held in a <b>Chosen Prison</b> inside the zone, and
      the campaign builds to a battle in three parts: pull the captured scientists out under fire;
      then weather an all-out Chosen assault, working the coastal anti-air guns to knock Chosen ships
      out of the sky while a strike team flies transports into the prison itself — racing to free the
      prisoners before the Vanguard is lost beneath them.</p>
      <div class="note won"><span class="eyebrow">From here on</span>
      <p>This is as far as the shipped story reached before the servers went dark in 2017. Everything
      else the wiki documents — frames, weapons, thumping, the fight for Devil's Tusk — lives inside
      these last few entries. The timeline ends where the game ran out.</p></div>
    </li>
  </ul>`;


/* ======================= ROUTER + NAV (one line each) =====================

   ROUTER — wherever the hash is dispatched to a view (the switch/lookup that
   already handles '#/eras', '#/perks', etc.), add the timeline case, e.g.:

       case 'timeline': return V.timeline();
   or, if it's a map:
       timeline: V.timeline,

   NAV — add a link in the rail nav list alongside Eras/Perks, e.g.:

       <a href="#/timeline">Timeline</a>

   Nothing else changes. The view is self-contained and reads no DATA.
   ========================================================================== */
