/* firefall-crafting-route.js
 *
 * Systems page for the beta crafting economy: resource aspects, refining,
 * component-based recipes, quality propagation, and the four crafting stages.
 *
 * Drop-in route module in the same shape as firefall-timeline-route.js and
 * firefall-constraints-route.js. Namespaced .cft-* throughout.
 *
 * The join with the constraints page is the resource aspect list. Resources
 * are graded on Power, Mass and CPU-Cores, which are the same three budgets a
 * frame spends against, so picking materials at the terminal is the constraint
 * tradeoff made at manufacture time. Neither page is complete without the other.
 *
 * Era anchor: 0.7. Modules get a footer only, as the thing that replaced this.
 */
(function (global) {
  'use strict';

  var V = global.V = global.V || {};

  var PROV = {
    dump:    { label: 'dump',    title: 'Build 1962 client database' },
    notes:   { label: 'notes',   title: 'Official patch notes' },
    build:   { label: 'build',   title: 'Read off a client build directly' },
    legacy:  { label: 'legacy',  title: 'Superseded-era source, kept for the record' },
    derived: { label: 'derived', title: 'Computed from other recovered figures' }
  };

  function esc(s) {
    return String(s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function prov(kind, detail) {
    var p = PROV[kind] || PROV.notes;
    var text = detail ? p.label + ' ' + detail : p.label;
    return '<span class="cft-prov cft-prov--' + kind + '" title="' +
      esc(p.title) + '">' + esc(text) + '</span>';
  }

  // -------------------------------------------------------------------
  // Data
  // -------------------------------------------------------------------

  // Groups map one-to-one onto the progression trees. Whatever you thump
  // gates both what you can build and which tree you can climb.
  var RESOURCES = [
    {
      group: 'Mineral', tree: 'Mass tree',
      families: [
        { name: 'Metal',     items: ['Copper', 'Iron', 'Aluminum'] },
        { name: 'Composite', items: ['Carbon', 'Silicate', 'Ceramic'] }
      ]
    },
    {
      group: 'Gas', tree: 'Power tree',
      families: [
        { name: 'Reactive', items: ['Methine', 'Octine'] },
        { name: 'Inert',    items: ['Nitrine', 'Radine'] }
      ]
    },
    {
      group: 'Organic', tree: 'CPU tree',
      families: [
        { name: 'Biomaterial', items: ['Petrochem', 'Biopolymer', 'Xenograft'] },
        { name: 'Enzyme',      items: ['Toxin', 'Regenic', 'Anabolic'] }
      ]
    }
  ];

  // Every component recovered from the notes, with what it steers where known.
  var COMPONENTS = [
    { name: 'Plasma Cannon Barrel I', item: 'Plasma Cannon I', steers: 'AOE radius', src: ['notes', '0.6'] },
    { name: 'Plasma Reactor I',       item: 'Plasma Cannon I', steers: 'Damage',     src: ['notes', '0.6'] },
    { name: 'Personal Health Sensor', item: 'Repairing Nanites', steers: 'Resilience, keyed to Mass as of 0.7.1688', src: ['notes', '0.7.1688'] },
    { name: 'Shield Energizer',       item: 'Overseer', steers: 'Keyed to Mass as of 0.7.1688', src: ['notes', '0.7.1688'] },
    { name: 'Flame Applicator',       item: 'Immolate', steers: 'Thermal resistance', src: ['notes', '0.7.1688'] },
    { name: 'Stasis Field Unit',      item: 'Stasis Field', steers: 'Toughness', src: ['notes', '0.7.1688'] },
    { name: 'Health Propagator',      item: 'Supply Station', steers: 'Malleability', src: ['notes', '0.7.1688'] },
    { name: 'Energy Generator',       item: 'Burn Jets, Afterburner, Emergency Response', steers: 'Recharge, based on Power', src: ['notes', '0.7.1688'] },
    { name: 'Instant Healing System', item: 'Supply Station', steers: 'Healing, keyed to Mass as of 0.7.1688', src: ['notes', '0.7.1688'] },
    { name: 'Deployable Power Relays II', item: 'Deployable Shield II-IV', steers: 'Unknown', src: ['notes', '0.7.1694'] }
  ];

  var CONFLICTS = [
    {
      claim: 'Resources are graded on three aspects: Power, Mass and CPU-Cores.',
      source: 'Patch 0.6',
      status: 'Contested',
      body: 'By 0.7 the notes also name per-material stats that are not any of the three: Regenics for resilience, Xenografts for thermal resistance, Biopolymers for toughness, Aluminum for malleability. Either resources carry named stats on top of the three aspects, or the names are flavour labels for aspect values and the vocabulary drifted. The two readings differ in how many axes a crafter optimises across, so this is worth settling before the recipe tables get authored.',
      src: ['notes', '0.6 / 0.7.1688']
    },
    {
      claim: 'A component keys to one constraint type, which drives its output quality.',
      source: 'Patch 0.7.1688',
      status: 'Ambiguous',
      body: 'One line reads "changed the constraint type of the component Personal Health Sensor from Power to Mass", which sounds like the budget it charges. Another reads "fixed Shield Energizer to accept Mass as its stat over Power, this will now affect the output depending on the resource quality input", which makes the same field drive output. Both may be true if constraint type does double duty, charging one budget and selecting which resource aspect is read.',
      src: ['notes', '0.7.1688']
    },
    {
      claim: 'Five families of three resources, fifteen total.',
      source: 'Patch 0.6',
      status: 'Superseded',
      body: 'The 0.6.1637 rename lists six families across three groups, four of three and two of two, sixteen resources in all. The count changed during the reorganisation.',
      src: ['notes', '0.6.1637']
    },
    {
      claim: 'Stage 2 items will almost always have superior performance than Stage 1.',
      source: 'Patch 0.6.1621',
      status: 'Design tension',
      body: 'Taken literally this flattens the horizontal promise. If stage dominates material quality then the material hunt is decorative and the ladder is vertical again with extra steps. The notes gesture at the fix in the same breath, promising that more powerful gear is more constraint-hungry, and the surplus caps give it somewhere to land: a Stage IV item absorbs 200 allocated power against a Stage I item\u2019s 50, so stage can buy headroom instead of raw output.',
      src: ['notes', '0.6.1621']
    }
  ];

  var OPEN = [
    'Which resource is best at which aspect. The notes say each resource in a family is best at one, but never publish the table.',
    'Whether quality is a single number per resource unit or a vector across aspects.',
    'How component quality combines when an item takes several components. Averaged, weighted, or independent per steered attribute.',
    'What permanent repair costs. Beta never resolved it.',
    'The full nanoprint tree. Only scattered components survive in the notes.',
    'Whether the Stage I to IV progression changes which attributes a component can steer, or only how far.'
  ];

  // -------------------------------------------------------------------
  // Styles
  // -------------------------------------------------------------------
  var CSS = [
    '.cft{--cft-cy:#5ad7e8;--cft-am:#f2a63b;--cft-am-dim:#8a5f22;',
    '--cft-ink:#c8d6da;--cft-ink-dim:#7d8f95;--cft-panel:#0e181c;',
    '--cft-panel-2:#121f24;--cft-line:#1d323a;--cft-jade:#4fd9a4;',
    '--cft-violet:#a98cf0;--cft-chamfer:10px;color:var(--cft-ink);}',

    '.cft-panel{position:relative;background:var(--cft-line);',
    'clip-path:polygon(var(--cft-chamfer) 0,100% 0,100% calc(100% - var(--cft-chamfer)),',
    'calc(100% - var(--cft-chamfer)) 100%,0 100%,0 var(--cft-chamfer));',
    'padding:1px;margin:0 0 1.6rem;}',
    '.cft-panel>.cft-panel-in{background:var(--cft-panel);',
    'clip-path:polygon(calc(var(--cft-chamfer) - 1px) 0,100% 0,100% calc(100% - var(--cft-chamfer) + 1px),',
    'calc(100% - var(--cft-chamfer) + 1px) 100%,0 100%,0 calc(var(--cft-chamfer) - 1px));',
    'padding:1.15rem 1.3rem;}',

    '.cft-h{display:flex;align-items:center;gap:.85rem;margin:2.4rem 0 1rem;}',
    '.cft-h:first-child{margin-top:0;}',
    '.cft-h span{font-family:"Orbitron",monospace;font-size:.78rem;',
    'letter-spacing:.22em;text-transform:uppercase;color:var(--cft-am);white-space:nowrap;}',
    '.cft-h:after{content:"";flex:1;height:1px;background:linear-gradient(',
    '90deg,var(--cft-cy),rgba(90,215,232,0));}',

    '.cft-sub{font-family:"Orbitron",monospace;font-size:.7rem;letter-spacing:.18em;',
    'text-transform:uppercase;color:var(--cft-cy);margin:1.4rem 0 .5rem;}',

    '.cft p{line-height:1.62;margin:0 0 .9rem;max-width:74ch;}',
    '.cft-lede{font-size:1.02rem;color:#dde8eb;}',

    '.cft-prov{display:inline-block;font-family:"Orbitron",monospace;font-size:.58rem;',
    'letter-spacing:.13em;text-transform:uppercase;padding:.13em .5em;margin-left:.45em;',
    'vertical-align:.12em;border:1px solid currentColor;border-radius:1px;opacity:.85;',
    'white-space:nowrap;cursor:help;}',
    '.cft-prov--dump{color:var(--cft-cy);}',
    '.cft-prov--notes{color:var(--cft-am);}',
    '.cft-prov--build{color:var(--cft-jade);}',
    '.cft-prov--legacy{color:var(--cft-ink-dim);}',
    '.cft-prov--derived{color:var(--cft-violet);}',

    '.cft-t{width:100%;border-collapse:collapse;font-size:.88rem;margin:.2rem 0 .4rem;}',
    '.cft-t th{font-family:"Orbitron",monospace;font-size:.62rem;letter-spacing:.15em;',
    'text-transform:uppercase;color:var(--cft-cy);text-align:left;padding:.5rem .7rem;',
    'border-bottom:1px solid var(--cft-line);font-weight:500;}',
    '.cft-t td{padding:.5rem .7rem;border-bottom:1px solid rgba(29,50,58,.55);vertical-align:top;}',
    '.cft-t tr:last-child td{border-bottom:0;}',
    '.cft-t .cft-dim{color:var(--cft-ink-dim);}',

    // The refining chain. Four stages, arrow-separated, wrapping on narrow.
    '.cft-chain{display:flex;flex-wrap:wrap;align-items:stretch;gap:.5rem;margin:.3rem 0 .9rem;}',
    '.cft-step{flex:1 1 120px;background:var(--cft-panel-2);padding:.7rem .8rem;',
    'border-top:2px solid var(--cft-am-dim);}',
    '.cft-step b{display:block;font-family:"Orbitron",monospace;font-size:.62rem;',
    'letter-spacing:.13em;text-transform:uppercase;color:var(--cft-am);',
    'font-weight:500;margin-bottom:.35rem;}',
    '.cft-step span{font-size:.8rem;line-height:1.45;color:var(--cft-ink-dim);}',

    '.cft-conf{border-left:2px solid var(--cft-am-dim);padding:.15rem 0 .15rem 1rem;margin:0 0 1.35rem;}',
    '.cft-conf--no{border-left-color:#c05a5a;}',
    '.cft-conf-claim{color:#dde8eb;font-style:italic;margin:0 0 .3rem;}',
    '.cft-conf-meta{font-family:"Orbitron",monospace;font-size:.6rem;letter-spacing:.14em;',
    'text-transform:uppercase;color:var(--cft-ink-dim);margin:0 0 .5rem;}',
    '.cft-conf-meta b{color:var(--cft-am);font-weight:500;}',
    '.cft-conf p{margin:0;font-size:.9rem;}',

    '.cft-ul{margin:0 0 1rem;padding-left:1.1rem;max-width:74ch;}',
    '.cft-ul li{line-height:1.6;margin-bottom:.4rem;}',
    '.cft-note{font-size:.85rem;color:var(--cft-ink-dim);max-width:74ch;}',

    '@media (max-width:640px){.cft-t{font-size:.8rem;}',
    '.cft-t th,.cft-t td{padding:.4rem .45rem;}.cft-step{flex:1 1 100%;}}'
  ].join('');

  function injectCSS() {
    if (document.getElementById('cft-styles')) return;
    var s = document.createElement('style');
    s.id = 'cft-styles';
    s.textContent = CSS;
    document.head.appendChild(s);
  }

  function h(label) { return '<div class="cft-h"><span>' + esc(label) + '</span></div>'; }
  function panel(inner) { return '<div class="cft-panel"><div class="cft-panel-in">' + inner + '</div></div>'; }
  function srcChip(src) { return src ? prov(src[0], src[1]) : ''; }

  // -------------------------------------------------------------------
  // Sections
  // -------------------------------------------------------------------

  function secIntro() {
    // Not 'Crafting' — the wiki shell puts that above as the page title, and a
    // section header repeating it just reads as a stutter.
    return h('One system, not two') +
      '<p class="cft-lede">Crafting and the constraint economy are one system. Resources are ' +
      'graded on three aspects, Power, Mass and CPU-Cores, which are the same three budgets a ' +
      'battleframe spends against. Choosing materials at the terminal is the constraint tradeoff, ' +
      'made at the moment of manufacture.' + prov('notes', '0.6') + '</p>' +

      '<p>That is the load-bearing fact for anyone reconstructing this. Beta crafting is not a ' +
      'parallel progression track bolted onto the frame; it is the mechanism by which the ' +
      'constraint budgets acquire interesting numbers. Remove constraints and crafted items have ' +
      'nothing meaningful left to vary, which is exactly what happened at 1.0 and exactly why a ' +
      'whole separate module economy had to be invented to replace it.</p>' +

      '<p>The design was modelled on Star Wars Galaxies. Better gear needs rarer materials, but ' +
      'rarity alone is not the answer, because what a material is good at matters more than how ' +
      'scarce it is. A very rare resource can be the wrong choice for the stat you are trying to ' +
      'push.' + prov('legacy') + '</p>';
  }

  function secChain() {
    return h('The chain') +
      '<div class="cft-chain">' +
      '<div class="cft-step"><b>Raw</b><span>Thumped or gathered. Carries aspect values that ' +
      'persist into everything downstream.</span></div>' +
      '<div class="cft-step"><b>Refined</b><span>Split into Seed Crystite, refined resources, ' +
      'and occasionally a Crystite Hybrid.</span></div>' +
      '<div class="cft-step"><b>Component</b><span>Each one steers a different attribute of the ' +
      'finished item. Quality of input sets quality of component.</span></div>' +
      '<div class="cft-step"><b>Item</b><span>Assembled from components at a Molecular Printer. ' +
      'Takes real time to build.</span></div>' +
      '</div>' +

      '<p>The propagation rule is stated plainly: the quality of the resources used determines ' +
      'the quality of the component crafted, and the higher the component\u2019s quality, the ' +
      'better it affects its related attribute.' + prov('notes', '0.6') + ' Aspect stats of raw ' +
      'resources persist into their refined versions, so nothing is lost at the refining step.</p>' +

      '<p>Depth exists so that each component is a separate steering wheel. A Plasma Cannon I ' +
      'takes a Plasma Cannon Barrel I and a Plasma Reactor I; the barrel drives AOE radius and ' +
      'the reactor drives damage. Two independent material choices, two independent outcomes on ' +
      'the same weapon.' + prov('notes', '0.6') + '</p>' +

      '<div class="cft-sub">Crystite Hybrids</div>' +
      '<p>Refining yields a hybrid at 0.1% per unit, so a thousand units guarantees one and the ' +
      'remainder carries a proportional chance of another. Hybrids power the more advanced ' +
      'recipes.' + prov('notes', '0.6') + ' Coloured hybrids come from creature drops, Aranha, ' +
      'Thrasher, Hisser and Culex DNA plus Chosen Tech pieces, and gate Stage 2 recipes and ' +
      'above.' + prov('notes', '0.6.1621') + '</p>';
  }

  function secResources() {
    var rows = [];
    RESOURCES.forEach(function (g) {
      g.families.forEach(function (f, i) {
        rows.push('<tr>' +
          (i === 0 ? '<td rowspan="' + g.families.length + '">' + esc(g.group) +
            '<br><span class="cft-note">' + esc(g.tree) + '</span></td>' : '') +
          '<td>' + esc(f.name) + '</td>' +
          '<td>' + f.items.map(esc).join(', ') + '</td></tr>');
      });
    });

    return h('Resources') +
      '<p>Sixteen resources in six families across three groups, as of the 0.6.1637 rename. The ' +
      'groups are the same three that gate the progression trees, so what you thump determines ' +
      'both what you can build and which tree you can climb.' + prov('notes', '0.6.1637') + '</p>' +

      '<table class="cft-t"><thead><tr>' +
      '<th>Group</th><th>Family</th><th>Resources</th>' +
      '</tr></thead><tbody>' + rows.join('') + '</tbody></table>' +

      '<p style="margin-top:1.2rem">Each resource in a family is best at one specific aspect. ' +
      'The notes say so repeatedly but never publish which is best at what, so that table has to ' +
      'be authored.' + prov('notes', '0.6') + '</p>' +

      '<p>Nanoprints ask for group-level materials at Stage I and get more specific as the stages ' +
      'climb.' + prov('notes', '0.6.1637') + ' That is a good pacing device: early crafting takes ' +
      'anything you happen to have, and the material hunt narrows into something targeted only ' +
      'once you know what you are doing.</p>' +

      '<p class="cft-note">Every resource was renamed in 0.6.1637, away from descriptive names ' +
      'like Bio-Organic Smartgel toward plain ones like Xenograft. Sources written before that ' +
      'patch use the old vocabulary, so a name that appears nowhere in the resource table is ' +
      'probably a pre-1637 alias rather than a missing item.</p>';
  }

  function secComponents() {
    var rows = COMPONENTS.map(function (c) {
      return '<tr><td>' + esc(c.name) +
        '</td><td class="cft-dim">' + esc(c.item) +
        '</td><td>' + esc(c.steers) + srcChip(c.src) + '</td></tr>';
    }).join('');

    return h('Components') +
      '<p>Every component recovered from the notes. This is a fraction of the real tree, but it ' +
      'is enough to show the pattern: one component, one steered attribute, one constraint ' +
      'keying.</p>' +

      '<table class="cft-t"><thead><tr>' +
      '<th>Component</th><th>Feeds</th><th>Steers</th>' +
      '</tr></thead><tbody>' + rows + '</tbody></table>' +

      '<p style="margin-top:1.2rem">Patch 0.7.1688 is worth reading in full if you are authoring ' +
      'this. It reassigns four separate components between Power and Mass in a single build, which ' +
      'establishes that the constraint keying was a hand-placed design lever still being tuned two ' +
      'builds before the end of beta, not an emergent property of a formula.' + prov('notes', '0.7.1688') +
      '</p>' +

      '<p>The semantic pattern behind those reassignments is consistent: things that are physically ' +
      'bulky key to Mass, things that project or sustain an effect key to Power. Health sensors and ' +
      'healing systems moved to Mass. Fuel chargers became Energy Generators running off Power. The ' +
      'two beta item samples bracket the same axis, with an ammunition-loading Scrambler at 88 mass ' +
      'against 45 power and a deployable Beacon at 48 mass against 58 power.' + prov('derived') + '</p>';
  }

  function secStages() {
    return h('Stages') +
      '<p>Four stages per item were planned from the start; 0.6 shipped the first two and all four ' +
      'exist by 0.7, with Stage IV abilities appearing in the crafting fix lists.' +
      prov('notes', '0.6 / 0.7.1694') + ' Higher stages mean more complex recipes, more powerful ' +
      'output, and explicitly more constraint hunger.</p>' +

      panel(
        '<div class="cft-sub">Why stage matters twice</div>' +
        '<p style="margin:0 0 .6rem">Stage is the Roman numeral in an item\u2019s name, and it sets ' +
        'the surplus allocation cap at fifty per stage. A Stage I ability absorbs 50 placed power; ' +
        'a Stage IV weapon absorbs 200.' + prov('notes', '0.6.1637') + '</p>' +
        '<p class="cft-note" style="margin:0">That gives stage somewhere to go other than raw ' +
        'output. If higher stages buy headroom rather than damage, the material hunt stays ' +
        'load-bearing instead of being flattened by a vertical ladder.</p>'
      );
  }

  function secDurability() {
    return h('Durability and repair') +
      '<p>Crafted equipment degrades. Ten percent on death and respawn in the open world, plus ' +
      'ongoing decay from general use. A HUD warning fires at 50%, repairs happen at the Garage, ' +
      'and items can break beyond repair.' + prov('notes', '0.6.1621') + '</p>' +

      '<p>Repairs were free of Crystite, and the notes flag that as temporary. Beta ended without ' +
      'resolving what permanent looked like.' + prov('notes', '0.6.1621') + '</p>' +

      '<p>This is the one genuinely unfinished corner of the system, and it is the corner where ' +
      'modules were later solving a real problem. Without sockets, a good roll is a wasting asset ' +
      'and the only way to refresh it is to craft another. Whether that reads as a healthy reason ' +
      'to return to the terminal or as a tax on attachment depends entirely on how expensive the ' +
      'repair curve is and how much of a roll\u2019s quality survives it. Worth deciding ' +
      'deliberately rather than inheriting.</p>';
  }

  function secConflicts() {
    var cards = CONFLICTS.map(function (c) {
      var mod = c.status === 'Superseded' ? '' : (c.status === 'Contested' ? ' cft-conf--no' : '');
      return '<div class="cft-conf' + mod + '">' +
        '<p class="cft-conf-claim">\u201c' + esc(c.claim) + '\u201d</p>' +
        '<p class="cft-conf-meta">' + esc(c.source) + ' &nbsp;\u00b7&nbsp; <b>' + esc(c.status) + '</b>' +
        srcChip(c.src) + '</p><p>' + esc(c.body) + '</p></div>';
    }).join('');

    return h('Unsettled') + cards;
  }

  function secAfter() {
    return h('What replaced it') +
      '<p>Crafting was temporarily disabled in Update 1.6 pending a new system that never arrived, ' +
      'with research points, Crystite and research time refunded, and thumping reduced to yielding ' +
      'Crystite only.' + prov('notes', '1.6') + '</p>' +

      '<p>The replacement arrived earlier than that, at 1.0, in the form of socketed modules. Both ' +
      'systems answer the same question, which is how two copies of one weapon differ, but they ' +
      'answer it in opposite places. Crafting authors the variance before the item exists, through ' +
      'material choice, and the result is fixed. Modules author it afterward, through swappable ' +
      'drops, reversible for a fee.</p>' +

      '<p>They do not compose. Variance arriving from two directions reads as noise from both. And ' +
      'a 0.7-anchored reconstruction has less need of sockets than it looks, because surplus power ' +
      'allocation is already a reversible, rearrangeable layer sitting on top of fixed gear. The ' +
      'thing modules were for is largely already there.</p>' +

      '<p class="cft-note">Careful with the word in the dump. In beta, module means a piece of ' +
      'equipment: Burn Jets is called a module, the ultimate slot is the Hyper-Kinesis Module, ' +
      'Passive Module is a slot category, and coloured crystite hybrid modules are crafting ' +
      'components. None of those are sockets. Matching on the display name will pair beta slot ' +
      'names against 1.0 socketables.' + prov('notes', '0.5.1494 / 0.6') + '</p>';
  }

  function secOpen() {
    return h('Still open') +
      '<ul class="cft-ul">' +
      OPEN.map(function (o) { return '<li>' + esc(o) + '</li>'; }).join('') +
      '</ul>';
  }

  // -------------------------------------------------------------------
  // Route
  // -------------------------------------------------------------------
  V.crafting = function () {
    injectCSS();
    return '<div class="cft">' +
      secIntro() + secChain() + secResources() + secComponents() +
      secStages() + secDurability() + secConflicts() + secAfter() + secOpen() +
      '</div>';
  };

  if (global.ROUTES && !global.ROUTES.crafting) {
    global.ROUTES.crafting = { title: 'Crafting', view: V.crafting };
  }

})(typeof window !== 'undefined' ? window : this);

/* ---------------------------------------------------------------------------
 * WIRING
 *
 * Same three steps as the constraints module. Script tag after data.js, a
 * router entry under the key 'crafting', a nav link to #/crafting.
 *
 * Uses the same fifth provenance kind, 'derived'. If you already mapped that
 * away on the constraints page, do the same here in PROV.
 *
 * Cross-links worth adding by hand once both pages are live:
 *   - Constraints, "What equipment costs" -> here, for where item costs come from
 *   - Here, "Components" -> Constraints, for what the three budgets do
 *   - Eras -> here, for the 1.6 disabling
 * ------------------------------------------------------------------------- */
