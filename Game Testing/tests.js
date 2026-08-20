(function () {
  "use strict";

  var script = document.currentScript;
  var cfg = script && script.dataset ? script.dataset : {};
  var LS = cfg.ls || ("pin-tests-" + (location.pathname.split("/").pop() || "page"));

  var state = {};

  try {
    state = JSON.parse(localStorage.getItem(LS) || "{}") || {};
  } catch (e) {
    state = {};
  }

  function save() {
    try { localStorage.setItem(LS, JSON.stringify(state)); } catch (e) { /* private mode: page still works */ }
  }

  var toastEl = document.getElementById("toast");
  var toastTimer = null;

  function toast(msg) {
    if (!toastEl) { return; }
    toastEl.textContent = msg;
    toastEl.setAttribute("data-on", "1");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.removeAttribute("data-on"); }, 1400);
  }

  /* ---- copy, with a fallback for sandboxes that block the clipboard API ---- */

  function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      return navigator.clipboard.writeText(text);
    }
    return new Promise(function (resolve, reject) {
      var ta = document.createElement("textarea");
      ta.value = text;
      ta.setAttribute("readonly", "");
      ta.style.position = "fixed";
      ta.style.top = "-1000px";
      document.body.appendChild(ta);
      ta.select();
      var ok = false;
      try { ok = document.execCommand("copy"); } catch (e) { ok = false; }
      document.body.removeChild(ta);
      ok ? resolve() : reject(new Error("copy blocked"));
    });
  }

  document.addEventListener("click", function (ev) {
    var btn = ev.target.closest(".copy");
    if (!btn) { return; }
    ev.preventDefault();
    ev.stopPropagation();
    var code = btn.parentNode.querySelector("code");
    if (!code) { return; }
    copyText(code.textContent).then(function () {
      btn.setAttribute("data-done", "1");
      btn.textContent = "Copied";
      setTimeout(function () {
        btn.removeAttribute("data-done");
        btn.textContent = "Copy";
      }, 1100);
    }).catch(function () {
      toast("Copy blocked — select the text instead");
    });
  });

  /* ---- checkboxes ---- */

  var boxes = Array.prototype.slice.call(document.querySelectorAll(".step input[type=checkbox]"));
  var doneEl = document.getElementById("done");
  var totalEl = document.getElementById("total");
  var meterEl = document.getElementById("meter");
  if (totalEl) { totalEl.textContent = boxes.length; }

  function tally() {
    var n = 0;
    for (var i = 0; i < boxes.length; i++) { if (boxes[i].checked) { n++; } }
    if (doneEl) { doneEl.textContent = n; }
    if (meterEl) { meterEl.style.width = boxes.length ? (n / boxes.length * 100) + "%" : "0%"; }
  }

  /* Steps that ship ticked in the HTML are entries already run and written up.
     Seed them into storage once, so the index meters can read them back. */
  var seeded = state._seeded === 1;

  boxes.forEach(function (box) {
    var k = box.getAttribute("data-k");
    if (!seeded && box.defaultChecked) { state[k] = 1; }
    box.checked = state[k] === 1;
    box.addEventListener("change", function () {
      state[k] = box.checked ? 1 : 0;
      if (!box.checked) { delete state[k]; }
      save();
      tally();
    });
  });

  if (!seeded && boxes.length) { state._seeded = 1; save(); }

  tally();

  /* ---- result notes ---- */

  var notes = Array.prototype.slice.call(document.querySelectorAll("textarea[data-k]"));
  notes.forEach(function (ta) {
    var k = ta.getAttribute("data-k");
    if (state[k]) { ta.value = state[k]; }
    ta.addEventListener("input", function () {
      if (ta.value.trim()) { state[k] = ta.value; } else { delete state[k]; }
      save();
    });
  });

  /* ---- blocks ---- */

  document.addEventListener("click", function (ev) {
    var head = ev.target.closest(".block-head");
    if (!head) { return; }
    var block = head.parentNode;
    block.setAttribute("data-open", block.getAttribute("data-open") === "false" ? "true" : "false");
  });

  var expandBtn = document.getElementById("expand");
  if (expandBtn) {
    expandBtn.addEventListener("click", function () {
      var blocks = document.querySelectorAll(".block");
      var anyClosed = document.querySelector('.block[data-open="false"]') !== null;
      for (var i = 0; i < blocks.length; i++) {
        blocks[i].setAttribute("data-open", anyClosed ? "true" : "false");
      }
      expandBtn.textContent = anyClosed ? "Collapse all" : "Expand all";
    });
  }

  /* ---- run report ---- */

  var reportBtn = document.getElementById("report");
  if (reportBtn) {
    reportBtn.addEventListener("click", function () {
      var title = cfg.title || (document.querySelector(".masthead h1") || {}).textContent || "Run report";
      var out = ["# " + title, ""];
      var blocks = document.querySelectorAll(".block");

      for (var i = 0; i < blocks.length; i++) {
        var b = blocks[i];
        var n = b.querySelector(".block-n");
        var t = b.querySelector(".block-t");
        var bx = b.querySelectorAll(".step input[type=checkbox]");
        var checked = 0;
        for (var j = 0; j < bx.length; j++) { if (bx[j].checked) { checked++; } }

        out.push("## " + (n ? n.textContent.trim() : "") + " — " + (t ? t.childNodes[0].textContent.trim() : "") + "  (" + checked + "/" + bx.length + " steps)");

        var entries = b.querySelectorAll(".entry");
        for (var e = 0; e < entries.length; e++) {
          var ta = entries[e].querySelector("textarea[data-k]");
          if (ta && ta.value.trim()) {
            var idEl = entries[e].querySelector(".id");
            var titleEl = entries[e].querySelector(".entry-t");
            out.push("");
            out.push("**" + (idEl ? idEl.textContent.trim() + " — " : "") + (titleEl ? titleEl.textContent.trim() : "") + "**");
            out.push("");
            out.push(ta.value.trim());
          }
        }
        out.push("");
      }

      copyText(out.join("\n")).then(function () {
        toast("Run report copied");
      }).catch(function () {
        toast("Copy blocked");
      });
    });
  }

  /* ---- chip progress meters (index page) ---- */

  var chips = Array.prototype.slice.call(document.querySelectorAll(".chip[data-ls]"));

  /* Returns null when this page has never been opened, so the caller can fall
     back to the baseline the page ships with. */
  function chipDone(lsKey) {
    var s;
    try {
      s = JSON.parse(localStorage.getItem(lsKey) || "null");
    } catch (e) { s = null; }
    if (!s || s._seeded !== 1) { return null; }
    var n = 0;
    Object.keys(s).forEach(function (k) { if (k.charAt(0) !== "_" && s[k] === 1) { n++; } });
    return n;
  }

  function refreshChips() {
    chips.forEach(function (chip) {
      var ls = chip.getAttribute("data-ls");
      var total = parseInt(chip.getAttribute("data-total") || "0", 10);
      var base = parseInt(chip.getAttribute("data-base") || "0", 10);
      var stored = chipDone(ls);
      var done = stored === null ? base : stored;
      var bar = chip.querySelector(".chip-meter i");
      var count = chip.querySelector(".chip-count");
      if (bar) {
        bar.style.width = total ? (done / total * 100) + "%" : "0%";
      }
      if (count) {
        count.textContent = total ? done + "/" + total : "";
      }
      chip.classList.toggle("done", total > 0 && done >= total);
    });
  }

  if (chips.length) {
    refreshChips();
window.addEventListener("focus", refreshChips);
}

/* ---- search streams in the index grid ---- */

var searchForm = document.getElementById("search-stream");
if (searchForm) {
  var input = searchForm.querySelector("input");
  var chips = Array.prototype.slice.call(document.querySelectorAll(".chip"));
  var noResults = document.getElementById("no-results");
  var activeInitiative = "all";

  function chipMatches(chip, query) {
    var text = (chip.querySelector("b") ? chip.querySelector("b").textContent : "") + " " +
               (chip.querySelector("span") ? chip.querySelector("span").textContent : "");
    var textMatch = text.toLowerCase().indexOf(query) >= 0;
    var initiative = chip.getAttribute("data-initiative") || "";
    var initiativeMatch = activeInitiative === "all" || initiative === activeInitiative;
    return textMatch && initiativeMatch;
  }

  function filterChips(query) {
    query = (query || "").toLowerCase().trim();
    var found = 0;
    chips.forEach(function (chip) {
      var match = chipMatches(chip, query);
      chip.style.display = match ? "" : "none";
      if (match) { found++; }
    });

    /* A system header with every stream filtered out is a label pointing at
       nothing, so hide the header and its empty grid too. Headers are the
       permanent axis here; they just have nothing to show under this filter. */
    var groups = Array.prototype.slice.call(document.querySelectorAll(".index-grid .chips"));
    groups.forEach(function (group) {
      var visible = Array.prototype.slice.call(group.querySelectorAll(".chip"))
        .some(function (c) { return c.style.display !== "none"; });
      group.hidden = !visible;
      var heading = group.previousElementSibling;
      if (heading && heading.tagName === "H3") { heading.hidden = !visible; }
    });

    if (noResults) {
      noResults.style.display = found ? "none" : "block";
    }
  }

  function clearSearch() {
    input.value = "";
    filterChips("");
    input.focus();
  }

  input.addEventListener("input", function () { filterChips(input.value); });

  searchForm.addEventListener("submit", function (ev) {
    ev.preventDefault();
    clearSearch();
  });

  if (noResults) {
    noResults.addEventListener("click", clearSearch);
  }

  var pills = Array.prototype.slice.call(document.querySelectorAll(".initiative-filter button"));
  pills.forEach(function (pill) {
    pill.addEventListener("click", function () {
      activeInitiative = pill.getAttribute("data-filter") || "all";
      pills.forEach(function (p) { p.setAttribute("aria-pressed", p === pill ? "true" : "false"); });
      filterChips(input ? input.value : "");
    });
  });
}

/* ---- reset ---- */

  var resetBtn = document.getElementById("reset");
  if (resetBtn) {
    resetBtn.addEventListener("click", function () {
      if (!window.confirm("Clear every tick and every note on this page?")) { return; }
      state = {};
      save();
      boxes.forEach(function (b) { b.checked = false; });
      notes.forEach(function (t) { t.value = ""; });
      tally();
      toast("Cleared");
    });
  }
})();
