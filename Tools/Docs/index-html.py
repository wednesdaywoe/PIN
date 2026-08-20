#!/usr/bin/env python3
"""Build Docs/In-Game-Tests/index.html — the main index of the in-game test queue.

The index is the converted TEST-REGISTER.md: the frontier narrative plus the
grouped stream list, with every In-Game-Tests/<stream>.md link rewritten to the
same-folder .html page and every out-of-folder link prefixed with ../ so the page
works from inside In-Game-Tests/. A "streams" jump grid is prepended.
"""

import re
import sys
from pathlib import Path

import markdown
from bs4 import BeautifulSoup

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "Docs"
IGT = DOCS / "In-Game-Tests"

MD = markdown.Markdown(extensions=["tables", "fenced_code", "sane_lists"], output_format="html5")

GROUPS = [
    ("Read first",
     ["Session-Setup", "Transport-And-Lifecycle"]),
    ("Answerable without a client",
     ["Capture-Replay"]),
    ("Inventory",
     ["Inventory"]),
    ("Combat",
     ["NPC-Combat", "Hostility", "Damage-Decay", "Deployables-And-Vehicles"]),
    ("Resources",
     ["Kill-Rewards", "Resource-Payout", "Thump-Placement", "Thumper-Defence", "Damage-Loop", "Environment"]),
    ("Death and respawn",
     ["Death-And-Respawn"]),
    ("Persistence",
     ["Persistence"]),
    ("Session stability",
     ["Reliability"]),
    ("Client prediction",
     ["Charge-Camera", "Client-Logging", "Prediction-Sweep"]),
    ("Battleframe constraints",
     ["Constraints-Transport"]),
    ("Terrain collision",
     ["solid-world"]),
    ("Run sheets",
     ["sitting-plan"]),
]

TITLES = {
    "Session-Setup": "Session Setup",
    "Session-Setup": "Session Setup",
    "Transport-And-Lifecycle": "Transport and Lifecycle",
    "Capture-Replay": "Answers Available Without a Client",
    "Inventory": "Inventory Delivery",
    "NPC-Combat": "NPC Combat",
    "Hostility": "Hostility Rules",
    "Damage-Decay": "Range Based Damage Decay",
    "Deployables-And-Vehicles": "Damage to Deployables and Vehicles",
    "Kill-Rewards": "Kill Rewards",
    "Resource-Payout": "Resource Payout",
    "Thump-Placement": "Thump Placement",
    "Thumper-Defence": "Thumper Defence",
    "Damage-Loop": "Damage Loop",
    "Environment": "Environmental Damage",
    "Death-And-Respawn": "Death and Respawn",
    "Persistence": "Persistence",
    "Reliability": "Reliability Under Loss",
    "Charge-Camera": "Charge Camera Lockup",
    "Client-Logging": "Client Logging",
    "Prediction-Sweep": "Prediction Reconciliation Sweep",
    "Constraints-Transport": "Constraints Transport",
    "solid-world": "Solid World Run Sheet",
    "sitting-plan": "One-Sitting Run Order",
}

BLURBS = {
    "Session-Setup": "build, deploy, admin commands and the type-id tables every stream spawns with — reference",
    "Transport-And-Lifecycle": "getting a client into the world over HTTP-only, closing the world-entry freeze",
    "Transport-And-Lifecycle": "getting a client into the world over HTTP-only, closing the world-entry freeze",
    "Capture-Replay": "reading wire-format and data questions off a 2016 capture — reference",
    "Inventory": "whether a created item becomes a visible, equippable item the client accepts",
    "NPC-Combat": "monsters that notice you, fight, track, give up and come back",
    "Hostility": "the faction-stance encoding and what can and cannot damage what",
    "Damage-Decay": "whether damage actually drops with range, per weapon",
    "Deployables-And-Vehicles": "deployable and vehicle health, destruction and splash",
    "Kill-Rewards": "what a kill pays, how often, and who the kill is credited to",
    "Resource-Payout": "what a thumper pays and how the payout is delivered",
    "Thump-Placement": "where thumping pays, read off the map and the ground",
    "Thumper-Defence": "the four waves, the machine that survives them, and the payout",
    "Damage-Loop": "weapon damage end to end, headshots, splash falloff and creature tiering",
    "Environment": "drowning, melding, invulnerability and NPCs' exemption from hazards",
    "Death-And-Respawn": "the bleedout, the give-up key and the 30-second fallback",
    "Persistence": "what survives a logout, a killed client, a server restart and a corrupt save",
    "Reliability": "a session under induced packet loss, and what the resends prove",
    "Charge-Camera": "the stuck-camera investigation and the fix that ended it",
    "Client-Logging": "instrumenting the client's own ability engine — reference",
    "Prediction-Sweep": "every prediction-shaped effect, reconciled or ghosted",
    "Constraints-Transport": "whether unknown JSON keys in garage_slots survive into Lua",
    "solid-world": "the run sheet against terrain collision — 5 of 7, run 2026-08-17",
    "sitting-plan": "every solo-runnable entry in one unbroken session, in order",
}

# localStorage key each stream page saves its ticks under. Auto-generated pages
# use "pin-<stem>-v1" (stem lowercased); the two hand-written run sheets predate
# that and carry their own keys, which the chips must mirror exactly.
LS_KEYS = {
    "solid-world": "pin-solid-world-v1",
    "sitting-plan": "pin-sitting-v1",
}


def render_md(text: str) -> str:
    MD.reset()
    return MD.convert(text).strip()


def retarget_register_links(soup):
    """Rewrite links so the register works from inside In-Game-Tests/."""
    for a in soup.find_all("a", href=True):
        href = a["href"]
        if href.split("#")[0].endswith("README.md"):
            a["href"] = href.replace("In-Game-Tests/", "").replace("README.md#", "README.md#")
        elif href.startswith("In-Game-Tests/"):
            rest = href[len("In-Game-Tests/"):]
            a["href"] = rest.replace(".md", ".html")
        elif href.startswith("streams/") or href.startswith("Design/") or href.startswith("gaps/"):
            a["href"] = "../" + href
        elif href.endswith(".md") and not href.startswith("../"):
            a["href"] = "../" + href


def build_stream_grid():
    """The chip grid. Each chip is stamped with the stream page's localStorage
    key (data-ls) and its total step count (data-total) so the index JS can
    draw a live progress meter from the page's saved ticks."""
    rows = []
    for group, members in GROUPS:
        links = []
        for stem in members:
            total = count_steps(IGT / f"{stem}.html")
            ls = LS_KEYS.get(stem, f"pin-{stem.lower()}-v1")
            meter = ""
            if total > 0:
                meter = (
                    '<span class="chip-meter"><i></i></span>'
                    f'<span class="chip-count">{0}/{total}</span>'
                )
            links.append(
                f'<a class="chip" href="{stem}.html" data-ls="{ls}" data-total="{total}">'
                f'<b>{TITLES[stem]}</b><span>{BLURBS[stem]}</span>'
                f'{meter}</a>'
            )
        rows.append(f'<h3>{group}</h3><div class="chips">{"".join(links)}</div>')
    return '<section class="doc index-grid">' + "".join(rows) + "</section"


def count_steps(path: Path) -> int:
    """Count the interactive steps on a stream page's own localStorage key.

    The page counts one tick per checked box; the index needs the same total to
    draw a fraction. Falls back to 0 (no meter) for a page that has no steps.
    """
    if not path.exists():
        return 0
    text = path.read_text(errors="ignore")
    return text.count('class="step"')


# Search filtering text injected into the generated HTML <script> block.
SEARCH_SCRIPT = """
<script>
/* ---- search streams in the index grid ---- */

var searchForm = document.getElementById("search-stream");
if (searchForm) {
  var input = searchForm.querySelector("input");
  var chips = Array.prototype.slice.call(document.querySelectorAll(".chip"));
  var noResults = document.getElementById("no-results");

  function filterChips(query) {
    query = (query || "").toLowerCase().trim();
    var found = 0;
    chips.forEach(function (chip) {
      var text = (chip.querySelector("b") ? chip.querySelector("b").textContent : "") + " " +
                 (chip.querySelector("span") ? chip.querySelector("span").textContent : "");
      var match = text.toLowerCase().indexOf(query) >= 0;
      chip.style.display = match ? "" : "none";
      if (match) { found++; }
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
}
</script>"""


def main():
    register_path = DOCS / "TEST-REGISTER.md"
    if register_path.exists():
        register = register_path.read_text()
    else:
        import subprocess
        register = subprocess.check_output(
            ["git", "-C", str(ROOT), "show", "HEAD:Docs/TEST-REGISTER.md"], text=True
        )
    body = register
    if body.startswith("---"):
        end = body.index("---", 3)
        body = body[end + 3:]

    # the register's own stream list doubles as the frontier list; keep the
    # narrative but let the grid be the navigation
    html = render_md(body)
    soup = BeautifulSoup(html, "html.parser")
    # the register's own # Test Register title is now the masthead h1
    for h1 in soup.find_all("h1"):
        h1.decompose()
    retarget_register_links(soup)

    out = []
    out.append("<title>PIN — In-Game Test Queue</title>")
    out.append('<link rel="stylesheet" href="tests.css">')
    out.append('<div class="wrap">')
    out.append("  <header class=\"masthead\">")
    out.append("    <div class=\"eyebrow\">PIN · in-game test queue</div>")
    out.append("    <h1>In-Game Test Queue</h1>")
    out.append("    <p class=\"standfirst\">Every stream the queue runs against a real client, in one place. "
               "Open a stream, tick its steps, copy its commands, and the ticks and notes save themselves "
               "in the browser. The master register this index is built from is the method: "
               "<a href=\"README.md\">README.md</a>.</p>")
    out.append("  </header>")
    out.append('  <form class="search" id="search-stream" action="">')
    out.append('    <input type="search" placeholder="Search streams…" autofocus>')
    out.append('    <button type="submit" aria-label="Clear search">✕</button>')
    out.append("  </form>")
    out.append(build_stream_grid())
    out.append('  <div id="no-results" class="notice">No streams match your search</div>')
    out.append('  <section class="doc index-body">')
    out.append(str(soup))
    out.append("</section>")
    out.append("</div>")
    out.append('<div class="toast" id="toast"></div>')
    out.append(SEARCH_SCRIPT)
    out.append('<script src="tests.js" data-ls="pin-index-v1"></script>')

    (IGT / "index.html").write_text("\n".join(out) + "\n")
    print("index.html written")


if __name__ == "__main__":
    main()