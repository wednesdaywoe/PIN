/* firefall-constraints-route.js
 *
 * Systems page for the beta constraint economy: Mass, Power, CPU/Cores, the
 * thirty-node progression trees, and surplus power allocation.
 *
 * Drop-in route module in the same shape as firefall-timeline-route.js.
 * Everything is namespaced .cst-* so it can't collide with .tl or .ltl.
 *
 * WIRING (three steps, see notes at the bottom of this file):
 *   1. <script src="firefall-constraints-route.js"></script> after data.js
 *   2. Add 'constraints' to the nav/router table
 *   3. Repoint the --cst-* palette vars at the wiki globals if you'd rather
 *      not carry a second copy of the colors
 *
 * Era anchor: this page documents 0.7. Where 1.6 data is cited it's cited as
 * an artifact of a dead system, not as a current figure.
 */
(function (global) {
  'use strict';

  var V = global.V = global.V || {};

  // ---------------------------------------------------------------------
  // Provenance
  // ---------------------------------------------------------------------
  // Four kinds match the rest of the wiki. 'derived' is new. Most of the good
  // figures on this page are arithmetic over other recovered figures, and
  // labelling those as 'build' would overstate them: nobody ever saw the
  // number on screen. If you'd rather not add a fifth kind, map derived to
  // notes and lose the distinction.
  var PROV = {
    dump:    { label: 'dump',    title: 'Build 1962 client database' },
    notes:   { label: 'notes',   title: 'Official patch notes' },
    build:   { label: 'build',   title: 'Read off a client build directly' },
    legacy:  { label: 'legacy',  title: 'Superseded-era source, kept for the record' },
    derived: { label: 'derived', title: 'Computed from other recovered figures' }
  };

  function prov(kind, detail) {
    var p = PROV[kind] || PROV.notes;
    var text = detail ? p.label + ' ' + detail : p.label;
    return '<span class="cst-prov cst-prov--' + kind + '" title="' +
      esc(p.title) + '">' + esc(text) + '</span>';
  }

  function esc(s) {
    return String(s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // ---------------------------------------------------------------------
  // Data
  // ---------------------------------------------------------------------

  // Cost is indexed on node position, not on which tree the node sits in.
  // CPU 6, Mass 7 and Mass 8 form a clean doubling run across two different
  // trees, which is what pins this down.
  //
  // Nodes 3-5 and 9-10 have never been seen. They carry interpolated values
  // rather than blanks, anchored on the recovered rows instead of on one global
  // fit: 3-5 bridge the 8,000 -> 70,000 gap at a constant 1.72x per step, and
  // 9-10 continue the 2.0x step measured across 6 -> 8. Crystite and group
  // resource are bridged the same way and land on the 1:1.25 ratio every
  // recovered row holds. est marks them, so the table greys them and the chip
  // reads 'derived'. They are the shape of the curve, not readings off a client
  // — anything that needs a real number still needs the capture.
  var COST_LADDER = [
    { idx: 1,  xp: '4,000',     cy: '100',    res: 'none',   src: ['build', '0.7.1683'] },
    { idx: 2,  xp: '8,000',     cy: '200',    res: 'none',   src: ['build', '0.7.1683'] },
    { idx: 3,  xp: '13,800',    cy: '300',    res: '375',    src: ['derived'], est: true },
    { idx: 4,  xp: '23,700',    cy: '450',    res: '560',    src: ['derived'], est: true },
    { idx: 5,  xp: '40,700',    cy: '670',    res: '840',    src: ['derived'], est: true },
    { idx: 6,  xp: '70,000',    cy: '1,000',  res: '1,250',  src: ['build', '0.7.1679'] },
    { idx: 7,  xp: '150,000',   cy: '2,000',  res: '2,500',  src: ['build', '0.7.1679'] },
    { idx: 8,  xp: '300,000',   cy: '4,000',  res: '5,000',  src: ['build', '0.7.1683'] },
    { idx: 9,  xp: '600,000',   cy: '8,000',  res: '10,000', src: ['derived'], est: true },
    { idx: 10, xp: '1,200,000', cy: '16,000', res: '20,000', src: ['derived'], est: true }
  ];

  var KNOWN_NODES = [
    {
      tree: 'Power', idx: 1, name: 'Standard Crystite Reactor',
      grants: 'Power +20',
      note: 'No group resource. XP and Crystite only.',
      src: ['build', '0.7.1683']
    },
    {
      tree: 'Power', idx: 2, name: 'Upgraded Energizing Array',
      grants: 'Power +20',
      note: 'No group resource.',
      src: ['build', '0.7.1683']
    },
    {
      tree: 'CPU', idx: 1, name: 'Standard SmartGel',
      grants: 'Cores +1 (assumed)',
      note: 'Name recovered from an ability requirement line, not from the node itself.',
      src: ['build', '0.7.1679']
    },
    {
      tree: 'CPU', idx: 6, name: 'Advanced SmartGel',
      grants: 'Cores +1, Energy +30',
      note: 'First node seen to pay into two pools at once.',
      src: ['build', '0.7.1679']
    },
    {
      tree: 'Mass', idx: 7, name: 'Low Density Servos',
      grants: 'Mass +200, Pilot Token +1, speed ceiling 100%',
      note: 'Token matches the 5/7/9/10 schedule.',
      src: ['build', '0.7.1679']
    },
    {
      tree: 'Mass', idx: 8, name: 'Ultra Density Optimization',
      grants: 'Mass +200, speed ceiling 110%',
      note: 'Carries a fourth requirement line beyond the resource, almost certainly a crafted item. Name not legible in the capture.',
      src: ['build', '0.7.1683']
    }
  ];

  // Capacity at a known unlock count. None of these are base values except
  // the Engineer's cores.
  var FRAMES = [
    {
      name: 'R-40 "Raptor"', role: 'Recon',
      unlocks: '18 / 30', mass: '1400', pwr: '800', cpu: '13',
      src: ['build', '0.7.1679']
    },
    {
      name: 'E-10 Accord Engineer', role: 'Engineer',
      unlocks: '8 / 30', mass: '1000', pwr: '500', cpu: '8',
      src: ['build', '0.7.1683']
    }
  ];

  var ALLOCATION = [
    { slot: 'Signature Weapon',  item: 'R36 Assault Rifle II',        stage: 'II',   cap: 100, put: 44,  bonus: '+4.4%' },
    { slot: 'Secondary Weapon',  item: 'Recovered Grenade Launcher II', stage: 'II', cap: 100, put: 100, bonus: '+10.0%' },
    { slot: 'Ability 1',         item: 'Recovered SIN Beacon I',      stage: 'I',    cap: 50,  put: 50,  bonus: '+5.0%' },
    { slot: 'Ability 2',         item: 'Resonating Bolts I',          stage: 'I',    cap: 50,  put: 50,  bonus: '+5.0%' },
    { slot: 'Ability 3',         item: 'Stock Power Field',           stage: '-',    cap: 0,   put: 0,   bonus: '-' },
    { slot: 'H.K.M.',            item: 'Accord Artillery Strike I',   stage: 'I',    cap: 50,  put: 50,  bonus: '+5.0%' }
  ];

  var CONFLICTS = [
    {
      claim: 'Battleframes come in five tiers, each unlocking new mechanics.',
      source: 'IGN interview with Scott Youngblood',
      status: 'Superseded',
      body: 'Patch 0.6 removed tiers and moved frame unlocks to Pilot Tokens. Build 1545 still lists frames as "Accord Assaultframe / Assault 1" and "Tigerclaw Assaultframe / Assault 2", so the article describes roughly the 0.5 client. Its stated publication date is also wrong by about three years.',
      src: ['legacy', null]
    },
    {
      claim: 'Frames over their power output deal less damage; frames with excess deal more.',
      source: 'Cameron Winston, Red 5 progression post',
      status: 'Superseded',
      body: 'Patch 0.6.1637 states there is no longer any damage penalty associated with Power, and replaces the automatic bonus with manual surplus allocation. The post describes the system as it stood at 0.6 launch.',
      src: ['legacy', null]
    },
    {
      claim: 'The progression resource is unique to each battleframe.',
      source: 'Cameron Winston, Red 5 progression post',
      status: 'Superseded',
      body: 'True at 0.6 launch. Patch 0.7.1665 changed it to a resource group per tree: Mineral for Mass, Gas for Power, Organics for CPU. Node tooltips at 0.7 confirm the group scheme.',
      src: ['legacy', null]
    },
    {
      claim: 'You start with 8 cores and each ability costs 2.',
      source: 'Cameron Winston, Red 5 progression post',
      status: 'Confirmed',
      body: 'The Engineer displays CPU 8/8 with an untouched CPU tree. The Raptor displays 13 with five CPU nodes bought. Both resolve to a base of 8, on frames of different weight classes, so the base looks universal rather than class-scaled.',
      src: ['derived', null]
    },
    {
      claim: 'Max movement speed is capped by Mass unlocks at 90/100/110/120%.',
      source: 'Patch 0.6.1637',
      status: 'Partly right',
      body: 'That ladder is the ceiling, not the output. Displayed speed tracks how full the mass budget is: the Raptor at 49% loaded shows 100%, the Engineer at 88% loaded shows 80%. The notes describe only half the model.',
      src: ['build', '0.7.1679 / 0.7.1683']
    },
    {
      claim: '1.6 "power rating" and "cores" are the same stats renamed.',
      source: 'Common assumption',
      status: 'False',
      body: 'Update 1.6 introduced an additive gear score called power rating (a frame reads 320.50, a single weapon module 11) and a set of five chassis equipment pieces called cores. Neither has any relationship to the 0.7 Power budget or CPU cores beyond the word. Match on field identity, never on display name.',
      src: ['dump', null]
    }
  ];

  var OPEN = [
    'Node grants for indices 3 to 6 and 9 to 10 in each tree.',
    'Measured XP and Crystite costs for indices 3 to 5 and 9 to 10. The ladder interpolates them and a capture has never confirmed one.',
    'Whether group resource starts at node 3, later, or only from node 6. The ladder assumes node 3.',
    'The exact shape of the speed falloff curve. Two load-ratio samples is not a curve.',
    'Base Mass and Power for all thirteen remaining frames. Only cores are solved.',
    'Whether allocated surplus is stored per item or per saved loadout.',
    'What Energy is exactly. It is a separate pool from Power and almost certainly the ability and jet reservoir, but nothing states it.',
    'Whether the crafted-item gate applies only at node 10 or from node 8 onward.'
  ];

  // ---------------------------------------------------------------------
  // Styles
  // ---------------------------------------------------------------------
  // Local palette so the file drops in standalone. Repoint these at the wiki
  // globals if you want one source of truth for the colors.
  var CSS = [
    '.cst{--cst-cy:#5ad7e8;--cst-cy-dim:#2b7d88;--cst-am:#f2a63b;',
    '--cst-am-dim:#8a5f22;--cst-ink:#c8d6da;--cst-ink-dim:#7d8f95;',
    '--cst-bg:#0a1114;--cst-panel:#0e181c;--cst-panel-2:#121f24;',
    '--cst-line:#1d323a;--cst-jade:#4fd9a4;--cst-violet:#a98cf0;',
    '--cst-chamfer:10px;color:var(--cst-ink);}',

    // Chamfered panel. Outer clip makes the corner cut, inner element inset by
    // 1px gives the hairline edge without a border fighting the clip.
    '.cst-panel{position:relative;background:var(--cst-line);',
    'clip-path:polygon(var(--cst-chamfer) 0,100% 0,100% calc(100% - var(--cst-chamfer)),',
    'calc(100% - var(--cst-chamfer)) 100%,0 100%,0 var(--cst-chamfer));',
    'padding:1px;margin:0 0 1.6rem;}',
    '.cst-panel>.cst-panel-in{background:var(--cst-panel);',
    'clip-path:polygon(calc(var(--cst-chamfer) - 1px) 0,100% 0,100% calc(100% - var(--cst-chamfer) + 1px),',
    'calc(100% - var(--cst-chamfer) + 1px) 100%,0 100%,0 calc(var(--cst-chamfer) - 1px));',
    'padding:1.15rem 1.3rem;}',

    // Section header: amber mono label, cyan rule running out to the margin.
    '.cst-h{display:flex;align-items:center;gap:.85rem;margin:2.4rem 0 1rem;}',
    '.cst-h:first-child{margin-top:0;}',
    '.cst-h span{font-family:"Orbitron",monospace;font-size:.78rem;',
    'letter-spacing:.22em;text-transform:uppercase;color:var(--cst-am);',
    'white-space:nowrap;}',
    '.cst-h:after{content:"";flex:1;height:1px;background:linear-gradient(',
    '90deg,var(--cst-cy),rgba(90,215,232,0));}',

    '.cst-sub{font-family:"Orbitron",monospace;font-size:.7rem;',
    'letter-spacing:.18em;text-transform:uppercase;color:var(--cst-cy);',
    'margin:1.4rem 0 .5rem;}',

    '.cst p{line-height:1.62;margin:0 0 .9rem;max-width:74ch;}',
    '.cst-lede{font-size:1.02rem;color:#dde8eb;}',

    // Provenance chip.
    '.cst-prov{display:inline-block;font-family:"Orbitron",monospace;',
    'font-size:.58rem;letter-spacing:.13em;text-transform:uppercase;',
    'padding:.13em .5em;margin-left:.45em;vertical-align:.12em;',
    'border:1px solid currentColor;border-radius:1px;opacity:.85;',
    'white-space:nowrap;cursor:help;}',
    '.cst-prov--dump{color:var(--cst-cy);}',
    '.cst-prov--notes{color:var(--cst-am);}',
    '.cst-prov--build{color:var(--cst-jade);}',
    '.cst-prov--legacy{color:var(--cst-ink-dim);}',
    '.cst-prov--derived{color:var(--cst-violet);}',

    // Tables.
    '.cst-t{width:100%;border-collapse:collapse;font-size:.88rem;margin:.2rem 0 .4rem;}',
    '.cst-t th{font-family:"Orbitron",monospace;font-size:.62rem;',
    'letter-spacing:.15em;text-transform:uppercase;color:var(--cst-cy);',
    'text-align:left;padding:.5rem .7rem;border-bottom:1px solid var(--cst-line);',
    'font-weight:500;}',
    '.cst-t td{padding:.5rem .7rem;border-bottom:1px solid rgba(29,50,58,.55);',
    'vertical-align:top;}',
    '.cst-t tr:last-child td{border-bottom:0;}',
    '.cst-t .cst-num{font-variant-numeric:tabular-nums;white-space:nowrap;}',
    // Interpolated rows read as secondary at a glance, before anyone gets to
    // the chip. Index and source stay upright so the row still scans.
    '.cst-t .cst-est td{color:var(--cst-ink-dim);font-style:italic;}',
    '.cst-t .cst-est td:first-child,.cst-t .cst-est td:last-child{font-style:normal;}',

    // The surplus ledger. This is the one place worth spending some ink,
    // because the arithmetic is the whole argument.
    '.cst-ledger{background:var(--cst-panel-2);padding:1rem 1.2rem;',
    'font-family:"Orbitron",monospace;font-size:.82rem;line-height:2;',
    'font-variant-numeric:tabular-nums;margin:.4rem 0 1rem;}',
    '.cst-ledger .cst-l{display:flex;justify-content:space-between;gap:2rem;}',
    '.cst-ledger .cst-l span:first-child{color:var(--cst-ink-dim);}',
    '.cst-ledger .cst-rule{height:1px;background:var(--cst-line);margin:.4rem 0;}',
    '.cst-ledger .cst-tot{color:var(--cst-am);}',
    '.cst-ledger .cst-ok{color:var(--cst-jade);}',

    // Conflict cards.
    '.cst-conf{border-left:2px solid var(--cst-am-dim);padding:.15rem 0 .15rem 1rem;',
    'margin:0 0 1.35rem;}',
    '.cst-conf--ok{border-left-color:var(--cst-jade);}',
    '.cst-conf--no{border-left-color:#c05a5a;}',
    '.cst-conf-claim{color:#dde8eb;font-style:italic;margin:0 0 .3rem;}',
    '.cst-conf-meta{font-family:"Orbitron",monospace;font-size:.6rem;',
    'letter-spacing:.14em;text-transform:uppercase;color:var(--cst-ink-dim);',
    'margin:0 0 .5rem;}',
    '.cst-conf-meta b{color:var(--cst-am);font-weight:500;}',
    '.cst-conf--ok .cst-conf-meta b{color:var(--cst-jade);}',
    '.cst-conf--no .cst-conf-meta b{color:#d97b7b;}',
    '.cst-conf p{margin:0;font-size:.9rem;color:var(--cst-ink);}',

    '.cst-ul{margin:0 0 1rem;padding-left:1.1rem;max-width:74ch;}',
    '.cst-ul li{line-height:1.6;margin-bottom:.4rem;}',

    // Chart. Both scales are in the DOM; only the selected one is displayed.
    '.cst-chart{margin:.2rem 0 1.4rem;}',
    '.cst-chart svg{display:block;width:100%;height:auto;overflow:visible;}',
    '.cst-chart[data-scale="log"] .cst-c-lin,',
    '.cst-chart[data-scale="lin"] .cst-c-log{display:none;}',
    '.cst-chart-head{display:flex;align-items:center;justify-content:space-between;',
    'gap:1rem;flex-wrap:wrap;margin-bottom:.7rem;}',
    '.cst-legend{display:flex;gap:1.1rem;flex-wrap:wrap;align-items:center;',
    'font-size:.76rem;color:var(--cst-ink-dim);}',
    '.cst-legend span{display:inline-flex;align-items:center;gap:.42em;}',
    '.cst-legend i{width:12px;height:2px;display:inline-block;}',
    '.cst-seg{display:flex;gap:.35rem;}',
    '.cst-seg button{font-family:"Orbitron",monospace;font-size:.6rem;letter-spacing:.14em;',
    'text-transform:uppercase;padding:.4em .85em;cursor:pointer;background:transparent;',
    'color:var(--cst-ink-dim);border:1px solid var(--cst-line);}',
    '.cst-seg button:hover{color:var(--cst-ink);}',
    '.cst-seg button[aria-pressed="true"]{color:var(--cst-cy);border-color:var(--cst-cy);}',
    '.cst-tick{font-family:"Orbitron",monospace;font-size:9.5px;fill:var(--cst-ink-dim);}',
    '.cst-endlabel{font-family:"Orbitron",monospace;font-size:10px;font-weight:500;}',

    '.cst-note{font-size:.85rem;color:var(--cst-ink-dim);max-width:74ch;}',

    '@media (max-width:640px){',
    '.cst-t{font-size:.8rem;}.cst-t th,.cst-t td{padding:.4rem .45rem;}',
    '.cst-ledger{font-size:.72rem;}',
    '.cst-ledger .cst-l{gap:1rem;}}'
  ].join('');

  function injectCSS() {
    if (document.getElementById('cst-styles')) return;
    var s = document.createElement('style');
    s.id = 'cst-styles';
    s.textContent = CSS;
    document.head.appendChild(s);
  }

  // ---------------------------------------------------------------------
  // Render helpers
  // ---------------------------------------------------------------------
  function h(label) {
    return '<div class="cst-h"><span>' + esc(label) + '</span></div>';
  }

  function panel(inner) {
    return '<div class="cst-panel"><div class="cst-panel-in">' + inner + '</div></div>';
  }

  function srcChip(src) {
    return src ? prov(src[0], src[1]) : '';
  }

  // ---------------------------------------------------------------------
  // Cost curve
  // ---------------------------------------------------------------------
  // Emitted as an SVG string during render, so there is no post-insert hook to
  // miss. Both scales are drawn and CSS shows one, which is why the toggle only
  // has to flip an attribute: re-rendering the route the way the weapon pages
  // do would scroll the reader back to the top, away from the chart they just
  // clicked. Hover text rides on <title> children, so it costs no listeners and
  // screen readers get it too.

  var SERIES = [
    { key: 'xp',  label: 'XP',       color: 'var(--cst-cy)' },
    { key: 'cy',  label: 'Crystite', color: 'var(--cst-am)' },
    { key: 'res', label: 'Resource', color: 'var(--cst-jade)' }
  ];

  // The ladder stores display strings. Charting wants numbers, and 'none' is
  // an absent cost rather than a zero one.
  function num(v) {
    return (v == null || v === 'none') ? null : Number(String(v).replace(/,/g, ''));
  }

  function svgEl(tag, attrs, inner) {
    var s = '<' + tag;
    for (var k in attrs) if (attrs[k] != null) s += ' ' + k + '="' + attrs[k] + '"';
    return inner == null ? s + '/>' : s + '>' + inner + '</' + tag + '>';
  }

  function tickLabel(v) {
    if (v >= 1000000) return (v / 1000000) + 'M';
    if (v >= 1000) return (v / 1000) + 'k';
    return String(v);
  }

  function curveSvg(scale) {
    var W = 940, H = 380, L = 58, R = 92, T = 14, B = 40;
    var iw = W - L - R, ih = H - T - B;
    var x = function (i) { return +(L + (i - 1) / 9 * iw).toFixed(1); };
    var y, ticks;
    if (scale === 'log') {
      var lo = Math.log10(80), hi = Math.log10(2000000);
      y = function (v) { return +(T + ih - (Math.log10(v) - lo) / (hi - lo) * ih).toFixed(1); };
      ticks = [100, 1000, 10000, 100000, 1000000];
    } else {
      y = function (v) { return +(T + ih - v / 1250000 * ih).toFixed(1); };
      ticks = [0, 250000, 500000, 750000, 1000000, 1250000];
    }

    var out = '';
    ticks.forEach(function (t) {
      out += svgEl('line', { x1: L, x2: L + iw, y1: y(t), y2: y(t),
        stroke: 'var(--cst-line)', 'stroke-width': 1 });
      out += svgEl('text', { x: L - 9, y: y(t) + 3.5, 'text-anchor': 'end',
        'class': 'cst-tick' }, tickLabel(t));
    });
    out += svgEl('line', { x1: L, x2: L + iw, y1: T + ih, y2: T + ih,
      stroke: 'var(--cst-ink-dim)', 'stroke-width': 1 });
    COST_LADDER.forEach(function (n) {
      out += svgEl('text', { x: x(n.idx), y: T + ih + 20, 'text-anchor': 'middle',
        'class': 'cst-tick' }, n.idx);
    });
    out += svgEl('text', { x: L + iw / 2, y: H - 2, 'text-anchor': 'middle',
      'class': 'cst-tick' }, 'Node');

    var ends = [];
    SERIES.forEach(function (s) {
      var pts = COST_LADDER.filter(function (n) { return num(n[s.key]) != null; });
      // Segment at a time rather than one polyline, so a stretch that leans on
      // an interpolated endpoint can go dashed on its own.
      for (var i = 0; i < pts.length - 1; i++) {
        var a = pts[i], b = pts[i + 1], est = a.est || b.est;
        out += svgEl('line', {
          x1: x(a.idx), y1: y(num(a[s.key])), x2: x(b.idx), y2: y(num(b[s.key])),
          stroke: s.color, 'stroke-width': 2, 'stroke-linecap': 'round',
          'stroke-dasharray': est ? '5 5' : null, opacity: est ? '.75' : null
        });
      }
      pts.forEach(function (n) {
        out += svgEl('circle', {
          cx: x(n.idx), cy: y(num(n[s.key])), r: 4.5,
          fill: n.est ? 'var(--cst-panel)' : s.color,
          stroke: s.color, 'stroke-width': 2
        }, svgEl('title', {}, 'Node ' + n.idx + ' — ' + s.label + ' ' +
          (n.est ? '~' : '') + n[s.key] + (n.est ? ' (derived)' : '')));
      });
      var last = pts[pts.length - 1];
      ends.push({ y: y(num(last[s.key])), color: s.color, label: s.label });
    });

    ends.sort(function (a, b) { return a.y - b.y; });
    for (var j = 1; j < ends.length; j++) {
      if (ends[j].y - ends[j - 1].y < 14) ends[j].y = ends[j - 1].y + 14;
    }
    ends.forEach(function (e) {
      out += svgEl('text', { x: L + iw + 11, y: e.y + 3.5, 'class': 'cst-endlabel',
        fill: e.color }, e.label);
    });

    return svgEl('svg', {
      viewBox: '0 0 ' + W + ' ' + H, role: 'img', 'class': 'cst-c-' + scale,
      'aria-label': 'Cost per node for XP, Crystite and group resource, nodes 1 to 10, ' +
        (scale === 'log' ? 'logarithmic' : 'linear') + ' scale'
    }, out);
  }

  function ratioSvg() {
    var W = 940, H = 190, L = 58, R = 92, T = 18, B = 32;
    var iw = W - L - R, ih = H - T - B;
    var y = function (v) { return +(T + ih - v / 2.4 * ih).toFixed(1); };
    var rows = [];
    for (var i = 1; i < COST_LADDER.length; i++) {
      var a = COST_LADDER[i - 1], b = COST_LADDER[i];
      rows.push({ from: a.idx, to: b.idx, r: num(b.xp) / num(a.xp), est: a.est || b.est });
    }

    var out = '';
    [1, 1.5, 2].forEach(function (t) {
      out += svgEl('line', { x1: L, x2: L + iw, y1: y(t), y2: y(t),
        stroke: 'var(--cst-line)', 'stroke-width': 1 });
      out += svgEl('text', { x: L - 9, y: y(t) + 3.5, 'text-anchor': 'end',
        'class': 'cst-tick' }, t.toFixed(1) + '×');
    });
    out += svgEl('line', { x1: L, x2: L + iw, y1: T + ih, y2: T + ih,
      stroke: 'var(--cst-ink-dim)', 'stroke-width': 1 });

    var bw = iw / 9 - 14;
    rows.forEach(function (r, i) {
      var bx = +(L + (iw / 9) * i + 7).toFixed(1), mid = +(bx + bw / 2).toFixed(1);
      out += svgEl('rect', {
        x: bx, y: y(r.r), width: +bw.toFixed(1), height: +(T + ih - y(r.r)).toFixed(1),
        fill: r.est ? 'none' : 'var(--cst-cy)', stroke: 'var(--cst-cy)',
        'stroke-width': r.est ? 2 : 0, 'stroke-dasharray': r.est ? '5 5' : null
      }, svgEl('title', {}, 'Node ' + r.from + ' to ' + r.to + ': ' + r.r.toFixed(2) +
        '×' + (r.est ? ' (derived)' : '')));
      out += svgEl('text', { x: mid, y: y(r.r) - 6, 'text-anchor': 'middle',
        'class': 'cst-tick' }, r.r.toFixed(2) + '×');
      out += svgEl('text', { x: mid, y: T + ih + 17, 'text-anchor': 'middle',
        'class': 'cst-tick' }, r.from + '→' + r.to);
    });

    return svgEl('svg', { viewBox: '0 0 ' + W + ' ' + H, role: 'img',
      'aria-label': 'XP multiplier from each node to the next' }, out);
  }

  function chartBlock() {
    return '<div class="cst-chart" data-scale="log">' +
      '<div class="cst-chart-head"><div class="cst-legend">' +
      SERIES.map(function (s) {
        return '<span><i style="background:' + s.color + '"></i>' + esc(s.label) + '</span>';
      }).join('') +
      '<span>solid = recovered, dashed = derived</span></div>' +
      '<div class="cst-seg">' +
      '<button type="button" aria-pressed="true" onclick="cstToggleScale(this,\'log\')">Log</button>' +
      '<button type="button" aria-pressed="false" onclick="cstToggleScale(this,\'lin\')">Linear</button>' +
      '</div></div>' +
      curveSvg('log') + curveSvg('lin') +
      '</div>';
  }

  // Reached from the inline handler on the scale buttons.
  global.cstToggleScale = function (btn, scale) {
    var wrap = btn.closest('.cst-chart');
    if (!wrap) return;
    wrap.setAttribute('data-scale', scale);
    var bs = wrap.querySelectorAll('.cst-seg button');
    for (var i = 0; i < bs.length; i++) {
      bs[i].setAttribute('aria-pressed', bs[i] === btn ? 'true' : 'false');
    }
  };

  // ---------------------------------------------------------------------
  // Sections
  // ---------------------------------------------------------------------

  function secIntro() {
    return h('The level-less system') +
      '<p class="cst-lede">Firefall\u2019s beta has no character level. Experience is a ' +
      'currency that sits in your wallet next to Organic, Mineral and Gas, and you spend ' +
      'it on nodes in three per-frame progression trees.' + prov('build', '0.7.1679') + '</p>' +

      '<p>That inversion is the whole design. A level is one number that only goes up, so ' +
      'the only lever for pacing it is how fast it climbs, and stretching the curve at the ' +
      'top is exactly what Red 5 said had driven players off. Once XP is spendable there is ' +
      'no curve left to stretch. Every session\u2019s earnings have somewhere to go the ' +
      'moment you get them, and "how far along am I" stops being a rank and becomes a ' +
      'question of which things you bought.</p>' +

      '<p>The three trees are Mass, Power and CPU. Each is a budget your equipment spends ' +
      'against, and each has ten nodes that raise the budget and gate what you\u2019re ' +
      'allowed to equip. Thirty nodes total, which is what the counter under the frame ' +
      'portrait is reporting when it reads 18/30.' + prov('derived') + '</p>';
  }

  function secTrees() {
    var rows = COST_LADDER.map(function (n) {
      var t = n.est ? '~' : '';
      return '<tr class="' + (n.est ? 'cst-est' : '') + '"><td class="cst-num">' + n.idx +
        '</td><td class="cst-num">' + t + n.xp +
        '</td><td class="cst-num">' + t + n.cy +
        '</td><td class="cst-num">' + (n.res === 'none' ? n.res : t + n.res) +
        '</td><td>' + srcChip(n.src) + '</td></tr>';
    }).join('');

    var nodeRows = KNOWN_NODES.map(function (n) {
      return '<tr><td class="cst-num">' + esc(n.tree) + ' ' + n.idx +
        '</td><td>' + esc(n.name) +
        '</td><td>' + esc(n.grants) +
        '</td><td>' + esc(n.note) + srcChip(n.src) + '</td></tr>';
    }).join('');

    return h('The three trees') +

      '<p>Cost is a function of node position, not of which tree the node sits in. ' +
      'CPU 6, Mass 7 and Mass 8 run 1,000 / 2,000 / 4,000 Crystite and 1,250 / 2,500 / ' +
      '5,000 group resource across two different trees. Clean doubling that ignores the ' +
      'tree boundary means one ladder covers all thirty nodes.' + prov('derived') + '</p>' +

      '<p>Nodes 1 and 2 cost only XP and Crystite with no group resource at all, which is ' +
      'the "first unlocks earned purely through experience" promise showing up in the ' +
      'client. The resource requirement switches on somewhere before node 6.' +
      prov('build', '0.7.1683') + '</p>' +

      panel(
        '<div class="cst-sub">Cost by node index</div>' +
        chartBlock() +
        '<table class="cst-t"><thead><tr>' +
        '<th>Node</th><th>XP</th><th>Crystite</th><th>Group resource</th><th>Source</th>' +
        '</tr></thead><tbody>' + rows + '</tbody></table>' +
        '<p class="cst-note">Group resource is Mineral for Mass, Gas for Power and Organics ' +
        'for CPU.' + prov('notes', '0.7.1665') + ' The greyed rows are interpolated rather ' +
        'than read. Nodes 3 to 5 bridge the 8,000 to 70,000 gap at 1.72&times; a step; 9 and ' +
        '10 carry on the 2.0&times; doubling that holds firmly across 6 to 8. Both land on ' +
        'the recovered anchors exactly and keep the 1:1.25 Crystite-to-resource ratio every ' +
        'measured row has. Fitting one exponential across all five recovered points instead ' +
        'overshoots node 6 by 17% and undershoots node 8 by 9%, which is why the bridges are ' +
        'anchored locally rather than globally.</p>' +

        '<p class="cst-note" style="margin-top:.7rem">Resource appearing at node 3 is the ' +
        'softest assumption in the table. What is known is that nodes 1 and 2 have none and ' +
        'node 6 has 1,250; it could switch on anywhere between, or not exist below 6 at all.</p>'
      ) +

      panel(
        '<div class="cst-sub">Step multiplier</div>' +
        ratioSvg() +
        '<p class="cst-note" style="margin:.7rem 0 0">Each node’s XP over the node before ' +
        'it. Flat bars would mean a pure exponential, and these are not flat. Both recovered ' +
        'ends sit at 2.0&times; or above while the interpolated middle sits at 1.72&times;, so ' +
        'the curve appears to slacken through the part nobody has seen. That dip is an ' +
        'artifact of the method rather than a finding: any four steps whose product is 8.75 ' +
        'join 8,000 to 70,000, and spreading the shortfall evenly is merely the least ' +
        'assuming of them. It is also the least likely in one specific way — if the real ' +
        'ladder held 2.0&times; on three of those four steps, the remaining one is 1.09&times;, ' +
        'very nearly free. A rung that cheap would be a deliberate breather, and this ' +
        'interpolation would hide it completely.</p>'
      ) +

      '<div class="cst-sub">Recovered nodes</div>' +
      '<table class="cst-t"><thead><tr>' +
      '<th>Node</th><th>Name</th><th>Grants</th><th>Notes</th>' +
      '</tr></thead><tbody>' + nodeRows + '</tbody></table>' +

      '<p style="margin-top:1.2rem">Pilot Tokens come at the 5th, 7th, 9th and 10th node of ' +
      'each tree.' + prov('notes', '0.6') + ' Twelve tokens for a fully maxed frame against ' +
      'a ten-token price for a new one, so completing a frame buys the next with a little ' +
      'left over. That is the engine behind keeping a stable of frames, and it is deliberately ' +
      'tight rather than generous. The 10th node also wants a crafted item unique to the ' +
      'frame.' + prov('notes', '0.6') + '</p>';
  }

  function secMass() {
    return h('Mass') +

      '<p>Mass does two jobs. The capacity number is your budget for equipping heavy gear. ' +
      'How full that budget is sets your movement speed.</p>' +

      '<p>The patch notes describe speed as a four-step ladder tied to unlock count, ' +
      '90 / 100 / 110 / 120%.' + prov('notes', '0.6.1637') + ' The client says that ladder is ' +
      'the ceiling, not the output. Displayed speed tracks load ratio:</p>' +

      panel(
        '<div class="cst-ledger">' +
        '<div class="cst-l"><span>Raptor &nbsp; 691 / 1400</span><span>49% loaded &rarr; speed 100%</span></div>' +
        '<div class="cst-l"><span>Engineer &nbsp; 880 / 1000</span><span>88% loaded &rarr; speed 80%</span></div>' +
        '</div>' +
        '<p class="cst-note" style="margin:0">Two samples, two frames, two builds.' +
        prov('build', '0.7.1679 / 0.7.1683') + ' Enough to rule out the step model, not ' +
        'enough to fit the curve.</p>'
      ) +

      '<p>This is a better system than the note implies. Running light pays continuously, so ' +
      'shaving thirty mass off a build is always worth something instead of only mattering ' +
      'when it crosses a threshold. It also explains why hovering Mass Tech 8 flips the ' +
      'Engineer\u2019s readout from 80% to 110%: the node adds 200 capacity, which drops him ' +
      'from 88% loaded to 73%, and raises the ceiling at the same time.</p>' +

      '<p>Worth replicating from the UI: hovering an unbought node live-previews its effect ' +
      'on the displayed caps. The Raptor\u2019s header reads 691/1600 under a Mass Tech 7 ' +
      'hover and 691/1400 otherwise. That is what makes the trees legible without a respec.' +
      prov('build', '0.7.1679') + '</p>';
  }

  function secPower() {
    var rows = ALLOCATION.map(function (r) {
      var capTxt = r.cap ? r.put + ' / ' + r.cap : '\u2013 / \u2013';
      return '<tr><td>' + esc(r.slot) +
        '</td><td>' + esc(r.item) +
        '</td><td class="cst-num">' + esc(r.stage) +
        '</td><td class="cst-num">' + capTxt +
        '</td><td class="cst-num">' + esc(r.bonus) + '</td></tr>';
    }).join('');

    return h('Power and surplus allocation') +

      '<p>Power is the centrepiece, and it is the part both surviving dev sources get ' +
      'wrong. There is no damage penalty for running your budget close, and no automatic ' +
      'bonus for running it lean.' + prov('notes', '0.6.1637') + ' Whatever headroom you have ' +
      'is a pool you place by hand.</p>' +

      '<p>Surplus is capacity minus consumption. Open the Allocate Surplus button under the ' +
      'Power tree and you get a slider per eligible slot.</p>' +

      panel(
        '<div class="cst-sub">The ledger balances</div>' +
        '<div class="cst-ledger">' +
        '<div class="cst-l"><span>Power capacity</span><span>800</span></div>' +
        '<div class="cst-l"><span>Consumed by equipped gear</span><span>446</span></div>' +
        '<div class="cst-rule"></div>' +
        '<div class="cst-l cst-tot"><span>Surplus</span><span>354</span></div>' +
        '<div class="cst-l"><span>Placed across five slots</span><span>294</span></div>' +
        '<div class="cst-l"><span>Held back</span><span>60</span></div>' +
        '<div class="cst-rule"></div>' +
        '<div class="cst-l cst-ok"><span>294 + 60</span><span>354 \u2713</span></div>' +
        '</div>' +
        '<p class="cst-note" style="margin:0">Two frames of the same session nine seconds ' +
        'apart show 104 unallocated and then 60, with the signature weapon slider reading ' +
        '44/100 and the cursor still on it. The books balance in both states.' +
        prov('build', '0.7.1679') + '</p>'
      ) +

      '<div class="cst-sub">Conversion and caps</div>' +
      '<p>Ten power buys one percent bonus damage.' + prov('notes', '0.6.1637') +
      ' The cap per slot is fifty per crafting Stage, and the Roman numeral in an item\u2019s ' +
      'name is that Stage.</p>' +

      '<table class="cst-t"><thead><tr>' +
      '<th>Slot</th><th>Item</th><th>Stage</th><th>Placed / cap</th><th>Bonus</th>' +
      '</tr></thead><tbody>' + rows + '</tbody></table>' +

      '<p style="margin-top:1.2rem">Stock gear has no Stage, so it has no cap, so it reads ' +
      'as a dash and can absorb nothing. The Engineer confirms the rule from the other ' +
      'direction: an all-stock loadout at 450/500 reports 50 unallocated power and has ' +
      'nowhere to put any of it.' + prov('build', '0.7.1683') + '</p>' +

      '<p>Eligible slots are the two weapons, the three abilities and the HKM. Plating, ' +
      'servos, jumpjets and the passive module never appear in the dialog.' +
      prov('notes', '0.6.1637') + '</p>' +

      '<p>Why it matters for the rebuild: under the old automatic rule, headroom was a hidden ' +
      'stat with no decision in it. Making it placeable turns leftover budget into an ' +
      'authored choice, so two pilots with identical frames, identical gear and an identical ' +
      '354 surplus can end up with different damage profiles. It also couples progression to ' +
      'crafting twice over, since a Stage II weapon both performs better and soaks twice the ' +
      'surplus. One design constraint falls out of that: a full Stage IV loadout can absorb ' +
      'up to 1,200 placed power across six slots, so if the Power ladder only ever grows ' +
      'enough to equip your gear, the allocation game quietly dies at endgame. The ladder has ' +
      'to outrun consumption.</p>';
  }

  function secCores() {
    return h('CPU and Cores') +

      '<p>Cores are ability slots. Every frame starts with 8 and each ability costs 2, so a ' +
      'stock loadout of four abilities exactly fills a fresh frame.' + prov('derived') + '</p>' +

      '<p>The base is confirmed twice over. The Engineer shows CPU 8/8 with an untouched CPU ' +
      'tree. The Raptor shows 13 with five CPU nodes bought, and 8 + 5 = 13. Two frames of ' +
      'different weight classes landing on the same base means cores are universal, unlike ' +
      'Mass and Power.' + prov('build', '0.7.1679 / 0.7.1683') + '</p>' +

      '<p>CPU nodes pay into two pools. Advanced SmartGel grants Cores +1 and Energy +30, ' +
      'Energy being the separate reservoir abilities and jets draw on.' + prov('build', '0.7.1679') +
      '</p>' +

      '<p>The tree is also a certification system, not just a budget. The Recovered SIN Beacon ' +
      'lists "Requires CPU Tech 1 - Standard SmartGel" as a hard requirement, and Stage I gear ' +
      'needs progression unlocks to equip at all.' + prov('notes', '0.6.1637') + ' Jumpjet ' +
      'visual effects also change as you climb the CPU tree, and completing all ten nodes of ' +
      'a tree on an advanced frame unlocks that frame\u2019s warpaint.' + prov('notes', '0.7.1665') +
      ' The trees carry the cosmetic payoff that levels used to.</p>';
  }

  function secItems() {
    return h('What equipment costs') +

      '<p>Build 1962 still defines Mass, Power and CPU as stats 951, 952 and 953, and 1,125 ' +
      'items still carry values for them, stored as negatives.' + prov('dump') + ' The system ' +
      'was switched off at the presentation layer rather than deleted, the same way beta ' +
      'crafting was.</p>' +

      '<p>Read that table with care. Across all 1,125 rows there are only seven distinct Mass ' +
      'values, Power is exactly half of Mass in 1,006 of 1,015 tuned rows, and a plating with ' +
      '1,100 health costs the same as one with 2,100. What survived at scale is a late-era ' +
      'pricing formula, and nothing was ever tuned against a live constraint economy after ' +
      '0.7, because nothing read the field.' + prov('dump') + '</p>' +

      '<div class="cst-sub">The stage ladder is probably real</div>' +
      '<p>The 1962 Mass values run 48 / 64 / 80 / 120 by crafting tier. The one beta ability ' +
      'cost read off a 0.7 client, the Stage I Recovered SIN Beacon, is Mass 48. Same number, ' +
      'same rung, and beta had four crafting stages. So the rungs look inherited from beta ' +
      'even though the per-item spread around them was flattened later.' + prov('derived') + '</p>' +

      '<p>The beta samples support that reading from both sides. The SIN Beacon sits on the ' +
      'ladder at 48 but carries Power 58, which is nowhere near the Mass/2 rule. Five Regulation ' +
      'items in 1962 sit at Mass 88 / Power 45, off the ladder entirely and off the formula, and ' +
      'one of them carries Cores 2 to complete the triple. Those are hand-authored beta rows ' +
      'that passed through every rebalance untouched.' + prov('dump') + '</p>' +

      '<div class="cst-sub">Stock against crafted</div>' +
      '<p>Stock gear has infinite durability, no Stage, and takes no surplus. Crafted and ' +
      'recovered gear is quality-rated, degrades, and is allocation-eligible. That split is why ' +
      'crafted quality matters rather than just crafted tier: a better roll can be lighter or ' +
      'cheaper on a constraint, not only stronger.' + prov('build', '0.7.1679') + '</p>' +

      '<p>The bracketed number beside prefixed items, [643] or [912], is a per-instance rolled ' +
      'quality. Every prefixed item carries one and no stock item does, which is why it will ' +
      'never turn up in the item table: it is a property of the instance, not the template.' +
      prov('derived') + '</p>';
  }

  function secFrames() {
    var rows = FRAMES.map(function (f) {
      return '<tr><td>' + esc(f.name) + '<br><span class="cst-note">' + esc(f.role) + '</span>' +
        '</td><td class="cst-num">' + esc(f.unlocks) +
        '</td><td class="cst-num">' + esc(f.mass) +
        '</td><td class="cst-num">' + esc(f.pwr) +
        '</td><td class="cst-num">' + esc(f.cpu) +
        '</td><td>' + srcChip(f.src) + '</td></tr>';
    }).join('');

    return h('Frame capacities') +

      '<p>These are capacities at a known unlock count, not base values. Only the ' +
      'Engineer\u2019s core figure is a true base, because his CPU tree is untouched.</p>' +

      '<table class="cst-t"><thead><tr>' +
      '<th>Frame</th><th>Unlocks</th><th>Mass</th><th>Power</th><th>Cores</th><th>Source</th>' +
      '</tr></thead><tbody>' + rows + '</tbody></table>' +

      '<p style="margin-top:1.2rem">Mass and Power clearly differ by frame while cores do not. ' +
      'Solving the bases needs the grant ladder, which is only partly recovered, so they stay ' +
      'underdetermined for now. One useful constraint: Power nodes 1 and 2 both grant +20, and ' +
      'if all ten did the same the whole tree would only contribute 200. That cannot explain ' +
      'the Raptor\u2019s 800 against the Engineer\u2019s 500 unless the frames start far apart ' +
      'or the later grants escalate. Both are worth testing against any third sample.' +
      prov('derived') + '</p>' +

      '<p class="cst-note">The remaining thirteen frames have no sample at all. Those numbers ' +
      'are honest design work rather than reconstruction, and belong in BUILD_OVERRIDES with ' +
      'the rationale attached.</p>';
  }

  function secConflicts() {
    var cards = CONFLICTS.map(function (c) {
      var mod = c.status === 'Confirmed' ? ' cst-conf--ok'
        : (c.status === 'False' ? ' cst-conf--no' : '');
      return '<div class="cst-conf' + mod + '">' +
        '<p class="cst-conf-claim">\u201c' + esc(c.claim) + '\u201d</p>' +
        '<p class="cst-conf-meta">' + esc(c.source) + ' &nbsp;\u00b7&nbsp; <b>' +
        esc(c.status) + '</b>' + srcChip(c.src) + '</p>' +
        '<p>' + esc(c.body) + '</p></div>';
    }).join('');

    return h('Where the sources disagree') +
      '<p>Three written accounts of this system survive and they describe three different ' +
      'versions of it. None is wrong so much as differently dated.</p>' +
      cards;
  }

  function secUnits() {
    return h('Units, as archaeology') +

      '<p>Older builds show what the constraints used to be, and the units are the ' +
      'interesting part.</p>' +

      '<table class="cst-t"><thead><tr>' +
      '<th>Build</th><th>Caps</th><th>Sample cost</th>' +
      '</tr></thead><tbody>' +
      '<tr><td class="cst-num">1409</td>' +
      '<td class="cst-num">Mass 6435 kg, Power 3815 GW, CPU in mL</td>' +
      '<td>Accord Bio Needler: CPU 190 mL, PWR 106 GW, MASS 547 kg</td></tr>' +
      '<tr><td class="cst-num">1545</td>' +
      '<td class="cst-num">all three normalised to 1000.00</td>' +
      '<td>Accord Plating: CPU 0.50 mL, Power 0.49 GW, Capacity 7.38 kg</td></tr>' +
      '<tr><td class="cst-num">0.7</td>' +
      '<td class="cst-num">per-frame, unitless</td>' +
      '<td>Recovered SIN Beacon I: Mass 48, Power 58, Cores 2</td></tr>' +
      '</tbody></table>' +

      '<p style="margin-top:1.2rem">CPU measured in millilitres is the detail worth keeping. ' +
      'SmartGel is a literal coolant fluid, which is why every CPU node is named for a gel ' +
      'grade and why the flavour text talks about cooling agents and data throughput. When 0.6 ' +
      'renamed CPU to Cores it converted a continuous 1000-unit fluid volume into 8 discrete ' +
      'slots. That is a change of kind, not a rename, and it is why a dev post can say an ' +
      'ability costs 2 cores while the 0.5 client was charging 190 mL for a rifle.' +
      prov('build', '1409 / 1545') + '</p>' +

      '<p>Build 1545 also settles the tier question on screen. Its frame list reads "Accord ' +
      'Assaultframe / Assault 1" and "Tigerclaw Assaultframe / Assault 2".' +
      prov('build', '1545') + '</p>';
  }

  function secOpen() {
    return h('Still open') +
      '<ul class="cst-ul">' +
      OPEN.map(function (o) { return '<li>' + esc(o) + '</li>'; }).join('') +
      '</ul>' +
      '<p class="cst-note">Node grants, node costs and frame bases never shipped to the ' +
      'client. All sixty certificates survive in 1962 with their names and flavour text in ' +
      'five languages, but every payload column on them is zero, so the numbers lived ' +
      'server-side alongside the spawn tables.' + prov('dump') + ' Certificates 11 to 20 exist ' +
      'as unnamed blanks in each tree, which says Red 5 meant to double the trees and never ' +
      'did. That is real design intent, but it postdates the 0.7 anchor and never shipped, so ' +
      'it belongs on the Eras page as context rather than here as a figure.</p>';
  }

  // ---------------------------------------------------------------------
  // Route
  // ---------------------------------------------------------------------
  V.constraints = function () {
    injectCSS();
    return '<div class="cst">' +
      secIntro() +
      secTrees() +
      secMass() +
      secPower() +
      secCores() +
      secItems() +
      secFrames() +
      secConflicts() +
      secUnits() +
      secOpen() +
      '</div>';
  };

  // Best-effort self-registration. If the wiki's router table has a different
  // name this is a no-op and you wire it by hand, same as the timeline module.
  if (global.ROUTES && !global.ROUTES.constraints) {
    global.ROUTES.constraints = {
      title: 'Constraints & Progression',
      view: V.constraints
    };
  }

})(typeof window !== 'undefined' ? window : this);

/* ---------------------------------------------------------------------------
 * WIRING
 *
 * 1. Script tag, after data.js and before the router boots:
 *      <script src="firefall-constraints-route.js"></script>
 *
 * 2. Router. The module tries to register itself into window.ROUTES under the
 *    key 'constraints'. If your table lives somewhere else, add it manually:
 *      constraints: { title: 'Constraints & Progression', view: V.constraints }
 *    Then add a nav entry pointing at #/constraints.
 *
 * 3. Fonts. Uses Orbitron for headers, labels and the ledger, same as the rest
 *    of the wiki. Already loaded, so nothing to add.
 *
 * 4. Palette. The .cst block declares its own --cst-* vars so the file drops in
 *    standalone. Repoint them at the wiki globals if you'd rather have one
 *    source of truth for the colors.
 *
 * 5. Provenance. This page uses a fifth kind, 'derived', for figures computed
 *    from other recovered figures rather than read off a source. Several of the
 *    best numbers here are arithmetic, and calling those 'build' would claim
 *    somebody saw them on screen. If you don't want a fifth kind, map derived
 *    to notes in PROV and drop the .cst-prov--derived rule.
 * ------------------------------------------------------------------------- */
