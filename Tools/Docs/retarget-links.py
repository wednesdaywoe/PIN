#!/usr/bin/env python3
"""Retarget markdown links to the converted In-Game-Tests HTML pages.

Runs over every .md under Docs/ (except the In-Game-Tests pages themselves, which
the tests-html converter already handles) plus Tests/GameServer.Tests/README.md:

-  In-Game-Tests/<stream>.md[#anchor]  ->  In-Game-Tests/<stream>.html[#anchor]
-  In-Game-Tests/Sitting-Plan.md       ->  In-Game-Tests/sitting-plan.html
-  In-Game-Tests/README.md             ->  unchanged (stays markdown)
-  TEST-REGISTER.md (any depth)        ->  In-Game-Tests/index.html

The parser handles both `[text](target)` links and bare `(target)` reference
forms, and never touches a target's trailing `)`, so a link that ends a sentence
keeps its closer.
"""

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "Docs"

STREAMS = {
    "Capture-Replay", "Charge-Camera", "Client-Logging", "Constraints-Transport",
    "Damage-Decay", "Damage-Loop", "Death-And-Respawn", "Deployables-And-Vehicles",
    "Environment", "Hostility", "Inventory", "Kill-Rewards", "NPC-Combat",
    "Persistence", "Prediction-Sweep", "Reliability", "Resource-Payout",
    "Session-Setup", "Thumper-Defence", "Thump-Placement", "Transport-And-Lifecycle",
}

# a link target: [label](PATH.md#anchor) or bare (PATH.md#anchor)
LINK_RE = re.compile(r"\]\(([^)\n]+\.md(?:#[^)\n]*)?)\)|(?<!\[)\(([^)\n]+\.md(?:#[^)\n]*)?)\)")

# split a target into path-stem/extension/anchor
TARGET_RE = re.compile(r"^(.*?)([A-Za-z0-9-]+)(\.md)(#[^)]*)?$")


def fix_target(target: str) -> str:
    m = TARGET_RE.match(target)
    if not m:
        return target
    pre, stem, ext, anchor = m.groups()
    anchor = anchor or ""
    if stem == "README":
        return target
    if stem == "Sitting-Plan":
        return pre + "sitting-plan" + ".html" + anchor
    if stem in STREAMS:
        return pre + stem + ".html" + anchor
    if stem == "TEST-REGISTER":
        return pre + "In-Game-Tests/index.html" + anchor
    return target


def retarget(text: str) -> str:
    def repl(m):
        t = m.group(1) or m.group(2)
        return "](" + fix_target(t) + ")" if m.group(1) else "(" + fix_target(t) + ")"
    return LINK_RE.sub(repl, text)


def main():
    targets = []
    for p in DOCS.rglob("*.md"):
        if "In-Game-Tests" in p.parts:
            continue
        targets.append(p)
    targets.append(ROOT / "Tests" / "GameServer.Tests" / "README.md")

    changed = 0
    for p in sorted(targets):
        if not p.exists():
            continue
        orig = p.read_text()
        new = retarget(orig)
        if new != orig:
            p.write_text(new)
            print(f"{p.relative_to(ROOT)}")
            changed += 1
    print(f"changed {changed} files")


if __name__ == "__main__":
    main()
