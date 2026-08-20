#!/usr/bin/env python3
"""Convert In-Game-Tests markdown stream docs into interactive run-sheet HTML.

The output reuses Docs/In-Game-Tests/tests.css and tests.js (the shared run-sheet
assets extracted from solid-world.html / sitting-plan.html). Every numbered list
becomes a checkbox step, every fenced code block a command with a copy button,
every entry heading a collapsible block with a result box, and every page keeps
its ticks and notes in localStorage with a Copy run report button.

Entry headings carry GitHub-compatible slugs so existing markdown cross-links
(ISSUE-REGISTER.md, PROGRESS.md, gaps/*.md) keep resolving against the HTML.
"""

import re
import sys
from pathlib import Path

import markdown
from bs4 import BeautifulSoup, Tag

ROOT = Path(__file__).resolve().parents[2]          # repo root
IGT = ROOT / "Docs" / "In-Game-Tests"

MD = markdown.Markdown(
    extensions=["tables", "fenced_code", "sane_lists"],
    output_format="html5",
)

MARKER_RE = re.compile(r"^(##|###)\s*\[([ xX!~\-])\]\s*([A-Za-z0-9][A-Za-z0-9]*)\s*[.:]\s*(.*)$")
HEADING_RE = re.compile(r"^(#{1,3})\s+(.*)$")

MARKER_CLASS = {
    "x": "pass", "X": "pass", "!": "fail", "~": "warn", "-": "none", " ": "none",
}

# stream file basename -> link text used in cross-links
STREAMS = {
    "Capture-Replay": "Capture Replay",
    "Charge-Camera": "Charge Camera",
    "Client-Logging": "Client Logging",
    "Constraints-Transport": "Constraints Transport",
    "Damage-Decay": "Damage Decay",
    "Damage-Loop": "Damage Loop",
    "Death-And-Respawn": "Death and Respawn",
    "Deployables-And-Vehicles": "Deployables and Vehicles",
    "Environment": "Environment",
    "Hostility": "Hostility",
    "Inventory": "Inventory",
    "Kill-Rewards": "Kill Rewards",
    "NPC-Combat": "NPC Combat",
    "Persistence": "Persistence",
    "Prediction-Sweep": "Prediction Sweep",
    "Reliability": "Reliability",
    "Resource-Payout": "Resource Payout",
    "Session-Setup": "Session Setup",
    "Sitting-Plan": "One-Sitting Run Order",
    "Thumper-Defence": "Thumper Defence",
    "Thump-Placement": "Thump Placement",
    "Transport-And-Lifecycle": "Transport and Lifecycle",
}

KEPT_MARKDOWN = {"README.md", "ISSUE-REGISTER.md", "PROGRESS.md"}


def github_slug(text: str) -> str:
    """GitHub-compatible heading slug, matching the anchors the .md links use.

    Task-list markers ([x], [ ], [!], [~]) are dropped leaving their trailing
    space, so '## [x] D6: A basic creature dies in about two seconds' slugs to
    '-d6-a-basic-creature-dies-in-about-two-seconds'.
    """
    text = re.sub(r"^\[[ xX!~\-]\]", "", text)      # drop the marker, keep its space
    text = text.lower()
    text = re.sub(r"[^\w \-]", "", text, flags=re.UNICODE)
    text = re.sub(r" +", "-", text)
    return text


def retarget_links(fragment):
    """Rewrite intra-folder .md links to .html, and TEST-REGISTER to index.html."""
    for a in fragment.find_all("a", href=True):
        href = a["href"]
        if href.endswith(".md") or ".md#" in href:
            stem = href.split("#")[0].split(".md")[0].split("/")[-1]
            if stem in STREAMS:
                a["href"] = href.replace(".md", ".html")
            elif href.endswith("TEST-REGISTER.md"):
                a["href"] = "index.html"


def render_inline(text: str) -> str:
    """Render an inline markdown string to HTML (no block wrappers)."""
    MD.reset()
    out = MD.convert(text).strip()
    if out.startswith("<p>"):
        out = out[3:]
    if out.endswith("</p>"):
        out = out[:-4]
    return out.strip()


def render_md(text: str) -> str:
    MD.reset()
    return MD.convert(text).strip()


def render_details(text: str) -> str:
    """Render markdown that contains raw <details>/<summary> blocks.

    python-markdown passes the inside of a raw block-level HTML tag straight
    through, so any markdown inside it would survive unconverted. Extract each
    details block, render its inner markdown separately, and splice it back.
    """
    details_re = re.compile(
        r"<details>(.*?)</details>", re.S | re.I
    )
    text = render_md(text)

    def repl(m):
        inner = m.group(1)
        inner = re.sub(r"<summary>(.*?)</summary>", r"<summary>\1</summary>", inner, flags=re.S | re.I)
        m2 = re.search(r"<summary>(.*?)</summary>", inner, re.S | re.I)
        body = inner
        if m2:
            summary = m2.group(1)
            body = inner.replace(m2.group(0), "")
        else:
            summary = ""
        rendered = render_md(body).strip()
        return f"<details><summary>{summary}</summary>\n{rendered}\n</details>"

    return details_re.sub(repl, text)


def soupify(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "html.parser")


def wire_commands(frag):
    """Wrap every <pre> in a .cmd span with a copy button.

    If a <pre>/<code> contains multiple commands (separated by newlines),
    split it into one .cmd span per line so each in-game command has its
    own copy field — the user can copy one at a time instead of having
    to manually deselect others from a shared field."""
    for pre in frag.find_all("pre"):
        code = pre.find("code")
        if not code:
            continue

        # Determine the non‑empty lines inside the code block
        full = code.get_text(separator="\n", strip=True)
        lines = [l for l in full.split("\n") if l.strip()]

        if len(lines) <= 1:
            # Single command — keep the existing behaviour
            cmd = frag.new_tag("span", **{"class": "cmd"})
            pre.wrap(cmd)
            btn = frag.new_tag("button", type="button", **{"class": "copy"})
            btn.string = "Copy"
            cmd.append(btn)
        else:
            # Multiple commands — replace this <pre> with N separate .cmd spans,
            # one per line, so each command has its own copy field.
            parent = pre.parent
            pre.replace_with("")  # remove the original <pre>

            cmd_spans = []
            for i, line in enumerate(lines):
                cmd = frag.new_tag("span", **{"class": "cmd"})

                new_pre = frag.new_tag("pre")
                new_code = frag.new_tag("code")
                new_code.string = line
                new_pre.append(new_code)
                cmd.append(new_pre)

                btn = frag.new_tag("button", type="button", **{"class": "copy"})
                btn.string = "Copy"
                cmd.append(btn)

                cmd_spans.append(cmd)

            # Insert all new .cmd spans at the original <pre>'s position.
            # If the parent is a Tag, insert each span after the previous one.
            for i, cmd in enumerate(cmd_spans):
                if i == 0:
                    parent.insert(0, cmd)  # prepend — simplest position
                else:
                    # insert after the previous .cmd
                    prev = cmd_spans[i - 1]
                    prev.insert_after(cmd)


def wire_tables(frag):
    for table in frag.find_all("table"):
        wrap = frag.new_tag("div", **{"class": "tbl-scroll"})
        table.wrap(wrap)


def wire_subheading_ids(frag):
    """Give h2/h3/h4 elements GitHub-compatible id attributes so deep links keep working."""
    for el in frag.find_all(["h2", "h3", "h4"], id=False):
        el["id"] = github_slug(el.get_text(" ", strip=True))


def wire_callouts(frag):
    for p in frag.find_all("p"):
        low = p.get_text(" ", strip=True).lower()
        cls = None
        if low.startswith("pass") or low.startswith("passed"):
            cls = "pass"
        elif low.startswith("fail") or low.startswith("failed"):
            cls = "fail"
        elif low.startswith("callout") or low.startswith("warning") or low.startswith("stop"):
            cls = "stop"
        if cls:
            call = frag.new_tag("div", **{"class": "callout " + cls})
            p.wrap(call)


def build_entry(entry_id, marker_char, title, body_md, n):
    """Render one `## [x] ID: Title` section as a run-sheet entry."""
    html = render_details(body_md)
    frag = soupify(html)
    retarget_links(frag)
    wire_commands(frag)
    wire_tables(frag)
    wire_subheading_ids(frag)

    # numbered lists -> checkbox steps; keys are unique across all lists in the entry
    step_no = 0
    for ol in frag.find_all("ol"):
        steps = frag.new_tag("div", **{"class": "steps"})
        ol.wrap(steps)
        for li in ol.find_all("li", recursive=False):
            step_no += 1
            label = frag.new_tag("label", **{"class": "step"})
            box = frag.new_tag("input", type="checkbox")
            box["data-k"] = f"{entry_id.lower()}-{n}-{step_no}"
            label.append(box)
            body = frag.new_tag("span", **{"class": "step-body"})
            lede = frag.new_tag("span", **{"class": "lede"})
            for child in list(li.contents):
                if isinstance(child, Tag) and child.name == "p":
                    lede.extend(child.contents)
                else:
                    lede.append(child)
            body.append(lede)
            label.append(body)
            li.replace_with(label)

    wire_callouts(frag)

    # result box per entry
    result = frag.new_tag("div", **{"class": "result"})
    lab = frag.new_tag("label")
    lab["for"] = f"r-{entry_id.lower()}"
    lab.string = "Result"
    ta = frag.new_tag("textarea", id=f"r-{entry_id.lower()}", **{"data-k": f"n-{entry_id.lower()}"})
    ta["placeholder"] = "What happened"
    result.append(lab)
    result.append(ta)

    entry = frag.new_tag("div", **{"class": "entry"})
    head = frag.new_tag("div", **{"class": "entry-head"})
    idspan = frag.new_tag("span", **{"class": "id"})
    idspan.string = entry_id
    marker = frag.new_tag("span", **{"class": "marker " + MARKER_CLASS.get(marker_char, "none")})
    marker.string = f"[{marker_char}]"
    tspan = frag.new_tag("span", **{"class": "entry-t"})
    tspan_md = BeautifulSoup(render_inline(title), "html.parser")
    tspan.extend([c for c in tspan_md.contents])
    head.extend([idspan, marker, tspan])
    entry.append(head)
    for child in list(frag.contents):
        entry.append(child)
    entry.append(result)
    return entry


def parse_sections(md_text: str):
    """Return (intro_lines, sections). Sections are dicts:
    {level, marker, id, title, body} where marker is None for plain ## docs."""
    lines = md_text.splitlines()
    intro = []
    sections = []
    cur = None

    def close():
        if cur is not None:
            sections.append(cur)

    for i, line in enumerate(lines):
        m = MARKER_RE.match(line)
        if m:
            close()
            level, marker, entry_id, title = m.groups()
            cur = {"level": level, "marker": marker, "id": entry_id.strip(), "title": title, "body": []}
            continue
        h = HEADING_RE.match(line)
        if h and h.group(1) == "##":
            close()
            cur = {"level": "##", "marker": None, "id": None, "title": h.group(2).strip(), "body": []}
            continue
        if cur is not None:
            cur["body"].append(line)
        elif h and h.group(1) == "#":
            # top-level document title goes into the masthead; skip it here
            continue
        else:
            intro.append(line)
    close()
    return intro, sections


def convert_file(path: Path):
    if path.exists():
        text = path.read_text()
    else:
        import subprocess
        rel = path.resolve().relative_to(ROOT.resolve())
        text = subprocess.check_output(
            ["git", "-C", str(ROOT), "show", f"HEAD:{rel}"], text=True
        )
    fm = {}
    if text.startswith("---"):
        end = text.index("---", 3)
        for line in text[3:end].splitlines():
            if ":" in line:
                k, _, v = line.partition(":")
                fm[k.strip()] = v.strip().strip('"')
        text = text[end + 3:]

    title = fm.get("title") or path.stem.replace("-", " ")

    intro, sections = parse_sections(text)
    intro_html = render_md("\n".join(intro)) if intro else ""
    intro_soup = soupify(intro_html)
    retarget_links(intro_soup)
    wire_commands(intro_soup)
    wire_tables(intro_soup)
    wire_subheading_ids(intro_soup)

    standfirst = ""
    if intro_soup.find("p"):
        standfirst = intro_soup.find("p").get_text(" ", strip=True)

    out = []
    out.append(f"<title>PIN — {title}</title>")
    out.append('<link rel="stylesheet" href="tests.css">')
    out.append('<div class="wrap">')
    out.append('  <header class="masthead">')
    out.append('    <div class="eyebrow">PIN · in-game test queue</div>')
    out.append(f"    <h1>{title}</h1>")
    if standfirst:
        out.append(f'    <p class="standfirst">{standfirst}</p>')
    out.append("  </header>")
    out.append('  <div class="bar">')
    out.append('    <span class="tally"><b id="done">0</b>/<span id="total">0</span> steps</span>')
    out.append('    <span class="meter"><i id="meter"></i></span>')
    out.append('    <button type="button" id="expand">Expand all</button>')
    out.append('    <button type="button" id="report" class="primary">Copy run report</button>')
    out.append('    <button type="button" id="reset">Reset</button>')
    out.append("  </div>")

    if intro_soup and intro_soup.find_all(["p", "table", "pre", "ul", "h2", "h3", "ol"]):
        out.append('  <section class="doc">')
        out.append("    " + str(intro_soup).replace("\n", "\n    "))
        out.append("  </section>")

    block_idx = 0
    for i, sec in enumerate(sections):
        body_md = "\n".join(sec["body"]).strip()
        if sec["marker"] is not None:
            slug = github_slug(f"[{sec['marker']}] {sec['id']}: {sec['title']}")
        else:
            slug = github_slug(sec["title"])
        if sec["marker"] is not None:
            block_idx += 1
            entry = build_entry(sec["id"], sec["marker"], sec["title"], body_md, block_idx)
            out.append(f'  <section class="block" data-open="true" id="{slug}">')
            out.append('    <button class="block-head" type="button">')
            out.append(f'      <span class="block-n">{sec["id"]}</span>')
            out.append(f'      <span class="block-t">{render_inline(sec["title"])}</span>')
            out.append('      <span class="block-meta"><span class="chev"></span></span>')
            out.append("    </button>")
            out.append("    <div class=\"block-body\">")
            out.append("      " + str(entry).replace("\n", "\n      "))
            out.append("    </div>")
            out.append("  </section>")
        else:
            body = render_details(body_md)
            soup = soupify(body)
            retarget_links(soup)
            wire_commands(soup)
            wire_tables(soup)
            wire_callouts(soup)
            wire_subheading_ids(soup)
            out.append(f'  <section class="doc" id="{slug}">')
            out.append(f"    <h2>{render_inline(sec['title'])}</h2>")
            out.append("    " + str(soup).replace("\n", "\n    "))
            out.append("  </section>")

    out.append("</div>")
    out.append('<div class="toast" id="toast"></div>')
    out.append(f'<script src="tests.js" data-ls="pin-{path.stem.lower()}-v1"></script>')

    return "\n".join(out)


def main():
    targets = [Path(a) for a in sys.argv[1:]] if len(sys.argv) > 1 else sorted(IGT.glob("*.md"))
    for path in targets:
        if path.name == "README.md":
            continue
        if path.suffix != ".md":
            continue
        html = convert_file(path)
        out_path = path.with_suffix(".html")
        out_path.write_text(html + "\n")
        print(f"{path.name} -> {out_path.name} ({len(html)} bytes)")


if __name__ == "__main__":
    main()
