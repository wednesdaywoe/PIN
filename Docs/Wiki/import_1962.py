#!/usr/bin/env python3
"""
import_1962.py -- turn the SdbDocs generated tables into wiki data.

Reads the five Reference/*.md files produced by Tools/SdbDocs from a build-1962
clientdb.sd2 and writes dump-1962.js, which the wiki loads alongside the
hand-authored data.js.

Nothing here is hand-corrected. Where the dump and the patch notes disagree the
wiki shows both; this script only reports the disagreement, it does not resolve
it. Rerun after regenerating the tables.

  python3 import_1962.py --src <dir of .md files> --out dump-1962.js
"""

import argparse, json, os, re, sys
from collections import defaultdict

BUILD = "1962"

# --------------------------------------------------------------------------
# Markdown table reading
# --------------------------------------------------------------------------

def read_sections(path):
    """Split a generated page into {section heading: [table rows as dicts]}.

    Each page has one or more '## Heading' blocks, each containing a pipe table
    with a header row and a separator. A page can carry several tables under one
    heading (Weapon-Templates does), so rows are keyed by heading and every
    table found under it is appended.
    """
    text = open(path, encoding="utf-8").read()
    sections, heading, header = defaultdict(list), "(top)", None
    for line in text.splitlines():
        if line.startswith("## "):
            heading, header = line[3:].strip(), None
            continue
        if not line.startswith("|"):
            header = None
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if all(set(c) <= set("-: ") and c for c in cells):
            continue                                    # separator
        if header is None:
            header = cells
            continue
        if len(cells) != len(header):
            continue                                    # malformed, skip
        sections[heading].append(dict(zip(header, cells)))
    return sections


def num(s):
    """Parse a cell that may be a number, a 'lo to hi' range, or nothing."""
    if s is None:
        return None
    s = s.strip()
    if not s:
        return None
    m = re.fullmatch(r"(-?[\d.]+)\s+to\s+(-?[\d.]+)", s)
    if m:
        return [float(m.group(1)), float(m.group(2))]
    try:
        return float(s) if "." in s else int(s)
    except ValueError:
        return s


def norm(s):
    """Normalise a name for matching: casefold, collapse whitespace, drop
    punctuation. 'Firecat  - Assault Type' and 'firecat - assault type' are the
    same certificate and the data spells one of them with a double space."""
    return re.sub(r"[^a-z0-9]+", " ", (s or "").lower()).strip()


# --------------------------------------------------------------------------
# Per-page parsers
# --------------------------------------------------------------------------

def parse_attributes(src):
    s = read_sections(os.path.join(src, "Attributes.md"))
    cats = {r["Id"]: {"internal": r["Internal name"], "display": r["Display name"],
                      "scalar": r["Scalar"] == "yes",
                      "moduleEffectiveness": num(r["Module effectiveness"])}
            for r in s["Categories"]}
    defs = {}
    for r in s["Definitions"]:
        defs[r["Id"]] = {
            "internal": r["Internal name"], "display": r["Display name"],
            "category": r["Category"], "inverse": r["Inverse"] == "yes",
            "items": num(r["Items"]) or 0,
        }
    return cats, defs


def parse_weapons(src):
    s = read_sections(os.path.join(src, "Weapons.md"))
    dmg = {r["Id"]: r["Display name"] or r["Internal name"] for r in s["Damage types"]}

    def rows(section, npc):
        out = []
        for r in section:
            tmpl = r["Template"]
            m = re.search(r"\((\d+)\)\s*$", tmpl)
            out.append({
                "name": r["Weapon"].lstrip("* "),
                "template": m.group(1) if m else None,
                "templateName": re.sub(r"\s*\(\d+\)\s*$", "", tmpl),
                "items": num(r["Items"]), "level": num(r["Level"]),
                "quality": num(r["Quality"]), "damage": num(r["Damage/round"]),
                "clip": num(r["Clip"]), "msBurst": num(r["ms/burst"]),
                "reloadMs": num(r["Reload ms"]), "range": num(r["Range"]),
                "damageType": r["Damage type"], "firstId": r["First id"],
                "npc": npc,
            })
        return out

    return dmg, rows(s["Player weapons"], False) + rows(s["NPC weapons"], True)


def parse_templates(src):
    """Three tables keyed by the same id. Merge them into one record each."""
    s = read_sections(os.path.join(src, "Weapon templates"
                     if os.path.exists(os.path.join(src, "Weapon templates"))
                     else "Weapon-Templates.md"))
    merged = {}
    for heading, rows in s.items():
        for r in rows:
            tid = r.get("Id")
            if not tid:
                continue
            rec = merged.setdefault(tid, {"id": tid, "name": r.get("Name", "")})
            for k, v in r.items():
                if k in ("Id", "Name"):
                    continue
                rec[k] = num(v)
    return merged


def parse_abilities(src):
    s = read_sections(os.path.join(src, "Abilities.md"))
    out = []
    for r in s["Abilities with a card"]:
        stats = []
        for part in (r["Stats"] or "").split("·"):
            part = part.strip()
            if not part:
                continue
            m = re.match(r"^(.*?)\s+(-?[\d.]+(?:\s+to\s+-?[\d.]+)?)$", part)
            if m:
                stats.append({"name": m.group(1).strip(), "value": num(m.group(2))})
            else:
                stats.append({"name": part, "value": None})
        certs = [c.strip() for c in (r["Requires"] or "").split(",") if c.strip()]
        out.append({
            "chain": r["Chain"], "name": r["Ability"], "certs": certs,
            "modules": num(r["Modules"]), "level": num(r["Level"]),
            "power": num(r["Power"]), "stats": stats,
            "desc": clean_markup(r["Description"]),
        })
    return out


def clean_markup(s):
    """Client strings carry [color=#xxxxxx] tokens. Strip them but keep the
    text they wrapped -- the tokens mark the ability's own section labels
    ('Activate:', 'Firecat Only:') and those are worth keeping."""
    s = re.sub(r"\[/?color[^\]]*\]", "", s or "")
    return re.sub(r"\s+", " ", s).strip()


def parse_recipes(src):
    s = read_sections(os.path.join(src, "Recipes.md"))
    out = []
    for heading, rows in s.items():
        m = re.search(r"type (\d+)", heading)
        btype = int(m.group(1)) if m else None
        for r in rows:
            ings = []
            for part in (r["Ingredients"] or "").split(","):
                part = part.strip()
                if not part:
                    continue
                mm = re.match(r"^([\d.]+)x\s+(.*)$", part)
                ings.append({"qty": num(mm.group(1)), "item": mm.group(2)}
                            if mm else {"qty": None, "item": part})
            out.append({
                "id": r["Id"], "output": r["Output"], "qty": num(r["Qty"]),
                "time": r["Build time"], "parallel": num(r["Parallel"]),
                "research": r["Research"] or None, "ings": ings, "type": btype,
            })
    return out


# --------------------------------------------------------------------------
# Matching the dump to the hand-authored entities
# --------------------------------------------------------------------------

# A template is the weapon's behaviour; the named items using it are its tiers.
# Name matching alone is not safe here -- several weapons kept a pre-1.6
# template in the database alongside the rebuilt one, and the internal names
# are designer shorthand ("David's Magic HMG" is the Dreadnaught HMG). So the
# mapping is declared. Each role is a separate template:
#   pve     the live PvE primary at build 1962
#   alt     its alternate fire, which is its own template
#   pvp     the standardised PvP version
#   legacy  a pre-1.6 template still in the db -- evidence, not the live weapon
WEAPON_TEMPLATES = {
  "plasma-cannon":     {"pve":"12129","alt":"12255","pvp":"12175","legacy":"14"},
  "fusion-cannon":     {"pve":"60",   "alt":"12253","pvp":"12212"},
  "phason-thrower":    {"pve":"12157","alt":"12254","legacy":"11962"},
  "smart-blaster":     {"pve":"12135","alt":"11938","pvp":"12209","legacy":"12134"},
  "bio-rifle":         {"pve":"11970","alt":"42",   "pvp":"12213"},
  "bolt-driver":       {"pve":"11959","pvp":"12186"},
  "arc-thrower":       {"pve":"12132","alt":"11980","pvp":"12208","legacy":"62"},
  "mine-launcher":     {"pve":"11957","alt":"11958"},
  "shock-rail":        {"pve":"11935","alt":"11948","pvp":"12218"},
  "heavy-machine-gun": {"pve":"12115","alt":"34",   "pvp":"12207","legacy":"32"},
  "rotary-blaster":    {"pve":"12106","alt":"12109","pvp":"12216"},
  "photon-lance":      {"pve":"12161","alt":"12164","legacy":"11960"},
  "marksman-rifle":    {"pve":"55",   "alt":"56"},
  "sniper-rifle":      {"pve":"12118","alt":"12119","pvp":"12217","legacy":"3"},
  "charge-rifle":      {"pve":"12120","pvp":"12219"},
}

# Where a hand-authored ability name differs from the dump's chain name.
ABILITY_ALIASES = {
  "multi-turrets": "multi turret",
  "cryo-shot":     "cryo bolt",
}

CERT_TO_FRAME = {
    "assault type": "accord-assault", "biotech type": "accord-biotech",
    "engineer type": "accord-engineer", "dreadnaught type": "accord-dreadnaught",
    "recon type": "accord-recon",
    "tigerclaw assault type": "tigerclaw", "firecat assault type": "firecat",
    "dragonfly biotech type": "dragonfly", "recluse biotech type": "recluse",
    "bastion engineer type": "bastion", "electron engineer type": "electron",
    "mammoth dreadnaught type": "mammoth", "rhino dreadnaught type": "rhino",
    "nighthawk recon type": "nighthawk", "raptor recon type": "raptor",
    # frames the 1.6 notes never mention; no hand-authored entity to point at
    "archangel assault type": None, "arsenal dreadnaught type": None,
}


def match_weapons(hand_weapons, templates, weapon_rows):
    """Attach the declared templates and their item tiers to each weapon."""
    tiers = defaultdict(list)
    for w in weapon_rows:
        if w["template"] and not w["npc"]:
            tiers[w["template"]].append(w)

    def lvl(r):
        v = r["level"]
        return v[0] if isinstance(v, list) else (v or 0)

    matched, missed = {}, []
    for w in hand_weapons:
        roles = WEAPON_TEMPLATES.get(w["id"])
        if not roles:
            missed.append(w["id"]); continue
        rec = {"roles": {}}
        for role, tid in roles.items():
            t = templates.get(tid)
            if not t:
                continue
            rec["roles"][role] = {
                "id": tid, "name": t["name"], "tmpl": t,
                "tiers": sorted(tiers.get(tid, []), key=lvl),
            }
        matched[w["id"]] = rec
    return matched, missed


def match_abilities(hand_abilities, dump_abilities):
    by_norm = defaultdict(list)
    for a in dump_abilities:
        by_norm[norm(a["name"])].append(a)
    matched, missed = {}, []
    for a in hand_abilities:
        key = ABILITY_ALIASES.get(a["id"]) or norm(a["name"])
        cands = by_norm.get(key, [])
        if cands:
            # prefer the chain with the most modules: the shipping one rather
            # than a test rig sharing the name
            best = max(cands, key=lambda c: c["modules"] or 0)
            matched[a["id"]] = {
                "chain": best["chain"], "stats": best["stats"],
                "certs": best["certs"], "level": best["level"],
                "power": best["power"], "modules": best["modules"],
                "desc": best["desc"],
                "alsoChains": [c["chain"] for c in cands if c is not best],
            }
        else:
            missed.append(a["id"])
    return matched, missed


# --------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, help="directory holding the .md tables")
    ap.add_argument("--hand", default="hand.json", help="entity list from emit_hand.js")
    ap.add_argument("--out", default="dump-1962.js")
    args = ap.parse_args()

    # normalise the uploaded filenames (they arrive with a numeric prefix)
    for f in os.listdir(args.src):
        m = re.match(r"^\d+_(.*\.md)$", f)
        if m and not os.path.exists(os.path.join(args.src, m.group(1))):
            try:
                os.symlink(os.path.join(args.src, f), os.path.join(args.src, m.group(1)))
            except OSError:
                pass

    cats, attrs = parse_attributes(args.src)
    dmgtypes, weapons = parse_weapons(args.src)
    templates = parse_templates(args.src)
    abilities = parse_abilities(args.src)
    recipes = parse_recipes(args.src)

    print(f"parsed: {len(attrs)} attributes, {len(cats)} categories, "
          f"{len(templates)} templates, {len(weapons)} weapon rows, "
          f"{len(abilities)} carded abilities, {len(recipes)} recipes")

    # pull the hand-authored ids out of data.js without executing it
    hand = json.load(open(args.hand, encoding="utf-8"))
    hand_weapons, hand_abilities = hand["weapons"], hand["abilities"]

    wmatch, wmiss = match_weapons(hand_weapons, templates, weapons)
    amatch, amiss = match_abilities(hand_abilities, abilities)
    print(f"matched: {len(wmatch)}/{len(hand_weapons)} weapons, "
          f"{len(amatch)}/{len(hand_abilities)} abilities")
    if wmiss:
        print("  unmatched weapons:  " + ", ".join(wmiss))
    if amiss:
        print("  unmatched abilities: " + ", ".join(amiss))

    # the orphaned beta crafting graph: recipes whose output carries the ^Q
    # suffix, which is the four-stage component system v1.6 stripped
    orphans = [r for r in recipes if "^Q" in r["output"]]
    orphan_with_ings = [r for r in orphans if r["ings"]]
    print(f"orphaned ^Q recipes: {len(orphans)}, {len(orphan_with_ings)} still have ingredients")

    out = {
        "build": BUILD,
        "generated": "prod-1962, built 2016-05-05",
        "attributeCategories": cats,
        "attributes": {k: v for k, v in attrs.items() if v["items"]},   # used ones only
        "damageTypes": dmgtypes,
        "weaponMatch": wmatch,
        "abilityMatch": amatch,
        "unmatched": {"weapons": wmiss, "abilities": amiss},
        "certToFrame": CERT_TO_FRAME,
        "counts": {
            "attributes": len(attrs), "attributesUsed": sum(1 for a in attrs.values() if a["items"]),
            "categories": len(cats), "templates": len(templates),
            "weaponRows": len(weapons), "playerWeapons": sum(1 for w in weapons if not w["npc"]),
            "abilitiesCarded": len(abilities), "recipes": len(recipes),
            "orphans": len(orphans), "orphansWithIngredients": len(orphan_with_ings),
        },
        # every carded ability, so the wiki can list the ones the notes never named
        "abilities": abilities,
        # recipes are large; store compactly and let the wiki index them
        "recipes": [[r["id"], r["output"], r["qty"], r["time"], r["research"],
                     [[i["qty"], i["item"]] for i in r["ings"]], r["type"]]
                    for r in recipes],
    }

    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write("/* GENERATED by import_1962.py -- do not edit by hand. */\n")
        fh.write("const DUMP = ")
        json.dump(out, fh, ensure_ascii=False, separators=(",", ":"))
        fh.write(";\n")
    print(f"wrote {args.out} ({os.path.getsize(args.out)/1024:.0f} KB)")


if __name__ == "__main__":
    main()
