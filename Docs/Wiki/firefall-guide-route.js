/* firefall-guide-route.js
 *
 * New player guide. Drop-in route module, namespaced .gd-*.
 *
 * This page deliberately breaks the house epistemics. The reference pages
 * label every figure with provenance and surface conflicts instead of
 * resolving them. A guide can't do that. It has to be decisive exactly where
 * the reference is honest about uncertainty, pick one answer, and never
 * mention there was a choice. No chips, no build numbers, no patch citations.
 *
 * Where this page states something the reference marks unsettled, that's a
 * design decision the rebuild owes an answer to. Those spots are commented.
 *
 * Voice rules: second person, short sentences, no noun used before it's
 * defined. Resources, quality and crafting get the most room because that's
 * where beta lost people.
 */
(function (global) {
  'use strict';

  var V = global.V = global.V || {};

  function esc(s) {
    return String(s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // -------------------------------------------------------------------
  // Styles
  // -------------------------------------------------------------------
  // Shares the wiki's palette and chamfer but runs looser: bigger body copy,
  // more air, numerals instead of rule-headers. Should read as a different
  // kind of document wearing the same clothes.
  var CSS = [
    '.gd{--gd-cy:#5ad7e8;--gd-am:#f2a63b;--gd-am-dim:#8a5f22;',
    '--gd-ink:#d3dfe3;--gd-ink-dim:#84979d;--gd-panel:#0e181c;',
    '--gd-panel-2:#121f24;--gd-line:#1d323a;--gd-jade:#4fd9a4;',
    '--gd-rose:#e08a7a;--gd-chamfer:10px;',
    'color:var(--gd-ink);font-size:1.02rem;line-height:1.72;}',

    '.gd p{margin:0 0 1.05rem;max-width:70ch;}',
    '.gd strong{color:#eaf2f4;font-weight:600;}',
    '.gd em{color:var(--gd-cy);font-style:normal;}',

    // Section head: oversized numeral, title beside it, hairline under.
    '.gd-sec{display:flex;align-items:baseline;gap:.9rem;margin:3.2rem 0 1.2rem;',
    'padding-bottom:.6rem;border-bottom:1px solid var(--gd-line);}',
    '.gd-sec:first-child{margin-top:0;}',
    '.gd-sec i{font-family:"Orbitron",monospace;font-style:normal;font-size:1.7rem;',
    'line-height:1;color:var(--gd-am-dim);min-width:1.6em;}',
    '.gd-sec h3{margin:0;font-family:"Orbitron",monospace;font-size:1.02rem;',
    'font-weight:500;letter-spacing:.04em;color:var(--gd-am);}',

    '.gd-lede{font-size:1.14rem;line-height:1.68;color:#eaf2f4;max-width:64ch;',
    'margin:0 0 1.6rem;}',

    '.gd-mini{font-family:"Orbitron",monospace;font-size:.68rem;letter-spacing:.18em;',
    'text-transform:uppercase;color:var(--gd-cy);margin:1.8rem 0 .5rem;}',

    // Callouts. Jade = do this. Rose = the mistake everyone makes.
    '.gd-call{position:relative;background:var(--gd-line);padding:1px;',
    'margin:1.4rem 0;clip-path:polygon(var(--gd-chamfer) 0,100% 0,',
    '100% calc(100% - var(--gd-chamfer)),calc(100% - var(--gd-chamfer)) 100%,',
    '0 100%,0 var(--gd-chamfer));}',
    '.gd-call>div{background:var(--gd-panel);padding:1rem 1.2rem;',
    'clip-path:polygon(calc(var(--gd-chamfer) - 1px) 0,100% 0,',
    '100% calc(100% - var(--gd-chamfer) + 1px),calc(100% - var(--gd-chamfer) + 1px) 100%,',
    '0 100%,0 calc(var(--gd-chamfer) - 1px));}',
    '.gd-call p{margin:0 0 .6rem;font-size:.96rem;}',
    '.gd-call p:last-child{margin-bottom:0;}',
    '.gd-call b{display:block;font-family:"Orbitron",monospace;font-size:.62rem;',
    'letter-spacing:.16em;text-transform:uppercase;margin-bottom:.5rem;font-weight:500;}',
    '.gd-call--do{background:linear-gradient(180deg,var(--gd-jade),var(--gd-line) 60%);}',
    '.gd-call--do b{color:var(--gd-jade);}',
    '.gd-call--trap{background:linear-gradient(180deg,var(--gd-rose),var(--gd-line) 60%);}',
    '.gd-call--trap b{color:var(--gd-rose);}',

    // Worked example: monospace-flavoured, stepped.
    '.gd-work{background:var(--gd-panel-2);padding:1.1rem 1.3rem;margin:1.3rem 0;',
    'border-left:2px solid var(--gd-cy);}',
    '.gd-work p{font-size:.95rem;margin:0 0 .55rem;}',
    '.gd-work p:last-child{margin-bottom:0;}',
    '.gd-work .gd-choice{display:flex;gap:.8rem;align-items:baseline;',
    'padding:.4rem 0;border-bottom:1px solid rgba(29,50,58,.6);}',
    '.gd-work .gd-choice:last-of-type{border-bottom:0;}',
    '.gd-work .gd-choice span:first-child{font-family:"Orbitron",monospace;',
    'font-size:.62rem;letter-spacing:.12em;text-transform:uppercase;',
    'color:var(--gd-am);min-width:11ch;}',
    '.gd-work .gd-choice span:last-child{font-size:.92rem;}',

    '.gd-t{width:100%;border-collapse:collapse;font-size:.92rem;margin:1rem 0 1.2rem;}',
    '.gd-t th{font-family:"Orbitron",monospace;font-size:.62rem;letter-spacing:.15em;',
    'text-transform:uppercase;color:var(--gd-cy);text-align:left;font-weight:500;',
    'padding:.5rem .7rem;border-bottom:1px solid var(--gd-line);}',
    '.gd-t td{padding:.55rem .7rem;border-bottom:1px solid rgba(29,50,58,.55);}',
    '.gd-t tr:last-child td{border-bottom:0;}',

    '.gd-traps{list-style:none;padding:0;margin:1rem 0 0;}',
    '.gd-traps li{padding:.75rem 0 .75rem 1.6rem;position:relative;',
    'border-bottom:1px solid rgba(29,50,58,.55);max-width:70ch;font-size:.96rem;}',
    '.gd-traps li:last-child{border-bottom:0;}',
    '.gd-traps li:before{content:"\\2715";position:absolute;left:0;top:.85rem;',
    'color:var(--gd-rose);font-size:.7rem;}',
    '.gd-traps b{color:#eaf2f4;font-weight:600;}',

    '@media (max-width:640px){.gd{font-size:.98rem;}',
    '.gd-sec i{font-size:1.35rem;}.gd-lede{font-size:1.06rem;}',
    '.gd-work .gd-choice{flex-direction:column;gap:.1rem;}}'
  ].join('');

  function injectCSS() {
    if (document.getElementById('gd-styles')) return;
    var s = document.createElement('style');
    s.id = 'gd-styles';
    s.textContent = CSS;
    document.head.appendChild(s);
  }

  function sec(n, title) {
    return '<div class="gd-sec"><i>' + esc(n) + '</i><h3>' + esc(title) + '</h3></div>';
  }
  function callDo(title, body) {
    return '<div class="gd-call gd-call--do"><div><b>' + esc(title) + '</b>' + body + '</div></div>';
  }
  function callTrap(title, body) {
    return '<div class="gd-call gd-call--trap"><div><b>' + esc(title) + '</b>' + body + '</div></div>';
  }

  // -------------------------------------------------------------------
  // Sections
  // -------------------------------------------------------------------

  function s1() {
    return sec('01', 'There are no levels') +
      '<p class="gd-lede">You will earn XP constantly and it will never make you level up. ' +
      'XP here is money. You spend it.</p>' +

      '<p>Everything your battleframe can do, you bought. It gets faster, tougher and more ' +
      'capable because you chose to spend on it, in an order you picked. Nobody is holding ' +
      'better content behind a number you have to grind toward.</p>' +

      '<p>This changes what a short session is worth. Twenty minutes that ends with you buying ' +
      'something is real progress, permanently. There is no half-finished level bar to come back ' +
      'to and no point at which the next rank stops feeling reachable.</p>' +

      '<p>It also means <strong>XP sitting in your wallet is doing nothing.</strong> It is not ' +
      'accumulating toward anything. It is cash in a drawer. Spend it.</p>';
  }

  function s2() {
    return sec('02', 'Your first hour') +
      '<p>Open your frame. You will see three horizontal tracks, and under each one a bar showing ' +
      'a number over another number. Those are your three budgets. The tracks are how you raise ' +
      'them.</p>' +

      '<p>Each track has ten steps. <strong>The first several cost only XP and Crystite</strong> ' +
      'and no gathered materials at all, which is the game giving you a stretch where mistakes ' +
      'are cheap.</p>' +

      callDo('Do this now',
        '<p>Buy the early steps as soon as you can afford them. All of them, in any order. Do not ' +
        'research, do not plan, do not save up for something better.</p>' +
        '<p>You cannot build a bad frame this early, and every step you own makes the next choice ' +
        'clearer because you will have felt what it changed.</p>') +

      '<p>Later steps get expensive and start demanding gathered materials, and that is where ' +
      'planning starts to matter. Not yet.</p>';
  }

  function s3() {
    return sec('03', 'Your three budgets') +
      '<p>Everything you equip costs you from three pools at once. The three do not punish you ' +
      'the same way, and understanding the difference is most of what separates a good frame from ' +
      'a heavy one.</p>' +

      '<table class="gd-t"><thead><tr><th>Budget</th><th>What it does</th><th>What going deep costs you</th></tr></thead><tbody>' +
      '<tr><td><strong>Mass</strong></td><td>Physical bulk of your gear</td>' +
      '<td>Speed. The fuller your mass budget, the slower you move. Not a cliff, a slope.</td></tr>' +
      '<tr><td><strong>Power</strong></td><td>Energy your gear draws</td>' +
      '<td>Nothing directly. But whatever you do not spend becomes bonus damage, so spending it is ' +
      'giving something up.</td></tr>' +
      '<tr><td><strong>Cores</strong></td><td>Ability slots</td>' +
      '<td>Nothing. It is a hard limit. Each ability takes two, and you either fit or you do not.</td></tr>' +
      '</tbody></table>' +

      '<p>Mass is the one new players get wrong. Your mass capacity is <em>not a target to fill.</em> ' +
      'It is a ceiling to stay comfortably under. A frame loaded to nearly full moves noticeably ' +
      'worse than one at half, and in a game where you are jetting and strafing constantly, that ' +
      'is felt in every fight.</p>' +

      callTrap('The heavy frame trap',
        '<p>You find a better plating. It fits. You equip it. You do this four more times over a ' +
        'week and never notice you have traded away a fifth of your movement speed.</p>' +
        '<p>Check your speed reading whenever you change gear. If it has dropped, something you ' +
        'equipped is not worth what it cost you.</p>');
  }

  function s4() {
    return sec('04', 'Claim your surplus') +
      '<p>Your power budget has leftovers, and the leftovers do nothing at all until you tell ' +
      'them where to go.</p>' +

      '<p>Find the <strong>Allocate Surplus</strong> button under your Power track. It opens a ' +
      'panel with a slider for each weapon and each ability. Drag them.</p>' +

      '<p>Every ten points of power you place buys one percent more damage on that item. There is ' +
      'no downside, no cost, and nothing you were otherwise using. It is free, and the game will ' +
      'never once remind you to do it.</p>' +

      callDo('Fifteen seconds, up to ten percent',
        '<p>Do a full pass now. Do another every time you change gear, because your surplus ' +
        'changes when your equipment does.</p>') +

      '<p>Two things will confuse you the first time.</p>' +

      '<p><strong>Stock gear cannot take any.</strong> Those sliders stay greyed out. That is not ' +
      'a bug, it is what stock gear is: reliable, free, and not upgradeable.</p>' +

      '<p><strong>Each slot has its own ceiling</strong>, set by how advanced the item is. A Mark I ' +
      'ability holds 50 points. A Mark II weapon holds 100. This is the quiet reason to care about ' +
      'crafting better gear: it does not just perform better, it <em>holds more of your surplus.</em></p>';
  }

  function s5() {
    return sec('05', 'Where materials come from') +
      '<p>You gather by dropping a thumper, a mining rig that punches into the ground, pulls up ' +
      'raw material, and attracts everything hostile within a wide radius while it works. You ' +
      'defend it until it finishes.</p>' +

      '<p>Everything you pull up belongs to one of three groups.</p>' +

      '<table class="gd-t"><thead><tr><th>Group</th><th>Families</th><th>Feeds</th></tr></thead><tbody>' +
      '<tr><td><strong>Mineral</strong></td><td>Metal, Composite</td><td>Your Mass track</td></tr>' +
      '<tr><td><strong>Gas</strong></td><td>Reactive, Inert</td><td>Your Power track</td></tr>' +
      '<tr><td><strong>Organic</strong></td><td>Biomaterial, Enzyme</td><td>Your Cores track</td></tr>' +
      '</tbody></table>' +

      '<p>That last column matters more than it looks. The same three groups that build your gear ' +
      'also pay for your progression steps. If you have been climbing your Mass track, you are ' +
      'burning Mineral, and you will need to go get more of it specifically. Gathering is never ' +
      'generic.</p>' +

      '<p>Inside each family sit the actual materials, and they are not interchangeable. Iron and ' +
      'Copper are both Metals, both Minerals, and they behave completely differently. You do not ' +
      'need to memorise the tree. You need to know it exists, so that when a recipe asks for ' +
      'Aluminum you understand that Iron is not close enough.</p>' +

      '<div class="gd-mini">Raw and refined</div>' +
      '<p>What comes out of the ground is <strong>raw</strong>. You cannot build with raw. Take it ' +
      'to a Molecular Printer and refine it first.</p>' +

      '<p>Refining gives you three things back: Seed Crystite, which is currency; refined ' +
      'resources, which are the actual building material; and occasionally a <strong>Crystite ' +
      'Hybrid</strong>, a rare ingredient every advanced recipe wants. Hybrids turn up roughly ' +
      'once per thousand units refined, so they accumulate quietly in the background. Do not hunt ' +
      'them. Refine everything and they arrive.</p>' +

      callTrap('The one that gets everybody',
        '<p>You gather for an hour, open the printer, and the recipe tells you that you have zero ' +
        'of a material you are visibly carrying a thousand units of.</p>' +
        '<p>You have it raw. Refine it. This is the single most common first-week confusion in the ' +
        'game and it costs people entire sessions.</p>');
  }

  function s6() {
    return sec('06', 'Quality, and why rare is not better') +
      '<p class="gd-lede">This is the part that makes Firefall different from every other game ' +
      'you have crafted in. It is worth ten minutes of your attention.</p>' +

      '<p><strong>Two chunks of Iron are not the same chunk of Iron.</strong></p>' +

      '<p>Every unit of every material carries hidden values describing what it is good for. One ' +
      'batch might be excellent for Mass and poor for Power. The next batch, pulled from a ' +
      'different place, can be the reverse. The name on the stack tells you almost nothing about ' +
      'what is in it.</p>' +

      '<p>Which leads to the thing that costs new players the most:</p>' +

      '<p class="gd-lede" style="color:var(--gd-am)">Rare does not mean good. Rare means ' +
      'different.</p>' +

      '<p>A rare material is not an upgrade to a common one. It is another material with its own ' +
      'strengths. If you are building something that cares about Power, and your rare find happens ' +
      'to be poor at Power, then a common material that is excellent at Power will build you a ' +
      'better item. Every time. Without exception.</p>' +

      '<p>Stop asking <em>what is the best thing I own.</em> Start asking <em>what is the best ' +
      'thing I own for this specific part.</em></p>' +

      callDo('You do not have to memorise any of it',
        '<p>When you choose materials for a part, the printer sorts your inventory best to worst ' +
        '<em>for that part</em>. The top of the list is the right answer.</p>' +
        '<p>So the skill is not knowing the table. The skill is knowing the table exists, and ' +
        'saving your good material for the parts that deserve it.</p>');
  }

  function s7() {
    return sec('07', 'Building something') +
      '<p>Items are assembled from parts you build separately. A Plasma Cannon is not one recipe ' +
      'you pour materials into. It is two components, each built from refined resources, then ' +
      'combined.</p>' +

      '<p>Here is the sentence that makes the whole system click:</p>' +

      '<p class="gd-lede" style="color:var(--gd-am)">Each component controls a different part of ' +
      'the finished item.</p>' +

      '<p>So you are never crafting a <em>good</em> Plasma Cannon. You are deciding what kind of ' +
      'Plasma Cannon you want, by choosing which component gets your best material.</p>' +

      '<div class="gd-work">' +
      '<p><strong>Worked example: one Plasma Cannon, three outcomes</strong></p>' +
      '<p>The Barrel sets blast radius. The Reactor sets damage. You have one genuinely good ' +
      'material and one mediocre one.</p>' +
      '<div class="gd-choice"><span>Good &rarr; Reactor</span><span>Hits hard, tight blast. A ' +
      'precision weapon.</span></div>' +
      '<div class="gd-choice"><span>Good &rarr; Barrel</span><span>Wide blast, softer hit. A crowd ' +
      'weapon.</span></div>' +
      '<div class="gd-choice"><span>Split evenly</span><span>Mediocre at both. A weapon with no ' +
      'reason to exist.</span></div>' +
      '<p style="margin-top:.7rem">The third one is what most new players build, because ' +
      'spreading your good materials evenly across every part feels fair and balanced. It is ' +
      'neither. It is how you spend rare material on nothing.</p>' +
      '</div>' +

      '<p>Decide what you want the item to be <em>before</em> you start gathering. Then concentrate ' +
      'everything good into the one or two components that steer it.</p>' +

      '<div class="gd-mini">Stages</div>' +
      '<p>Items come in four stages, marked I to IV. Higher stages use more components, want ' +
      'better materials and Crystite Hybrids, and take longer to build.</p>' +

      '<p>Stage I recipes ask for a whole group. Any Mineral will do. Later stages get specific ' +
      'and start naming exact materials. That is deliberate pacing: you learn the shape of the ' +
      'system while it is forgiving, and it starts demanding precision only once you know what ' +
      'you are doing.</p>' +

      '<div class="gd-mini">It takes real time</div>' +
      '<p>Building is not instant. A finished item takes roughly as long as a thumper run, which ' +
      'is the point. Queue it, go play, come back. If a recipe wants something you do not have, ' +
      'tell the printer to track it and the game will point you at where to find it.</p>';
  }

  function s8() {
    return sec('08', 'Gear wears out') +
      '<p>Crafted equipment degrades. It takes a hit every time you die in the open world, and ' +
      'wears down steadily from ordinary use. You will get a warning when something drops to half ' +
      'condition. Repairs happen at the Garage.</p>' +

      '<p>Which is why the least glamorous advice in this guide is also the most useful:</p>' +

      '<p><strong>Do not throw away your stock gear.</strong> It never breaks, never degrades, ' +
      'never needs repairing, and costs nothing. It is worse than a good crafted item and better ' +
      'than a broken one, and it is exactly what you want equipped while your good gear is being ' +
      'fixed.</p>' +

      '<p>Keep a full stock loadout. Treat it as your floor.</p>';
  }

  function s9() {
    return sec('09', 'Collecting frames') +
      '<p>You unlock new battleframes with Pilot Tokens, and tokens come from progression steps ' +
      'deep in a frame\u2019s tracks, at the fifth, seventh, ninth and tenth step of each one.</p>' +

      '<p>Do the arithmetic, because it drives everything about how you should play. Three tracks ' +
      'paying four tokens each is <strong>twelve tokens from one fully finished frame</strong>. A ' +
      'new frame costs ten.</p>' +

      '<p>So finishing a frame buys the next one outright, with a little left over. But a frame you ' +
      'abandoned at step four paid you nothing at all, because the first token is at step five.</p>' +

      callTrap('Spreading yourself thin',
        '<p>Three frames climbed halfway give you no tokens, no deep unlocks, and three frames that ' +
        'are all slightly disappointing.</p>' +
        '<p>Pick one and take it deep. The game is built to reward that, and the frame you finish ' +
        'pays for the next one.</p>');
  }

  function s10() {
    var traps = [
      ['Hoarding XP.', 'It looks like progress. It is cash in a drawer.'],
      ['Filling your mass budget.', 'It is a ceiling, not a target. Every point you spend costs you speed.'],
      ['Never touching Allocate Surplus.', 'Free damage, sitting unclaimed, possibly for weeks.'],
      ['Salvaging your stock gear.', 'It is the only thing you own that cannot break.'],
      ['Crafting with the rarest thing you have.', 'Wrong question. Ask what is best for the part, not what is scarcest.'],
      ['Spreading good materials across every component.', 'You get an item that is mediocre at everything.'],
      ['Trying to build with raw resources.', 'Refine first. Always.'],
      ['Climbing three tracks evenly on three frames.', 'Nothing pays out until step five. Go deep on one.']
    ];
    return sec('10', 'Eight ways to waste a week') +
      '<ul class="gd-traps">' +
      traps.map(function (t) {
        return '<li><b>' + esc(t[0]) + '</b> ' + esc(t[1]) + '</li>';
      }).join('') +
      '</ul>';
  }

  // -------------------------------------------------------------------
  // Route
  // -------------------------------------------------------------------
  V.guide = function () {
    injectCSS();
    return '<div class="gd">' +
      s1() + s2() + s3() + s4() + s5() + s6() + s7() + s8() + s9() + s10() +
      '</div>';
  };

  if (global.ROUTES && !global.ROUTES.guide) {
    global.ROUTES.guide = { title: 'New Player Guide', view: V.guide };
  }

})(typeof window !== 'undefined' ? window : this);

/* ---------------------------------------------------------------------------
 * WIRING
 *
 * Script tag after data.js, router entry under 'guide', nav link to #/guide.
 * No provenance system needed, this page deliberately has none.
 *
 * DECISIONS THIS PAGE MAKES THAT THE REFERENCE LEAVES OPEN
 *
 * Each of these is stated flatly here because a guide can't hedge. Each is
 * also a thing the rebuild owes a real answer to. If any of them get settled
 * differently, this page needs editing.
 *
 *   Section 05  Says materials within a family are meaningfully different and
 *               a recipe asking for Aluminum won't take Iron. True of later
 *               stages per the notes. Stage I asks at group level, so the
 *               claim is over-tight early on. Kept because the alternative is
 *               teaching an exception before the rule.
 *
 *   Section 06  Says every unit carries hidden per-stat values that vary by
 *               where it was gathered. Resources are graded on five slots,
 *               three of them Power, Mass and CPU-Cores, and quality
 *               propagates. Nothing says quality varies by gathering location.
 *               That's an inference from the SWG lineage. If quality turns out
 *               to be fixed per resource type, this section is wrong and the
 *               whole material hunt is a different activity.
 *
 *               The section names only Mass and Power as example stats, which
 *               stays true under the five-slot reading. Leave it that way. The
 *               other three have no recovered names yet.
 *
 *   Section 07  Assigns Barrel to blast radius and Reactor to damage, which is
 *               straight from the notes, and then generalises to "each
 *               component steers one attribute". The generalisation holds
 *               across every component in the record.
 *
 *   Section 09  Assumes the ten-token frame price rather than the seven-token
 *               sale price seen late in beta.
 * ------------------------------------------------------------------------- */
