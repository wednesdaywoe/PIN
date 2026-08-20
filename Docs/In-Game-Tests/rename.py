import re, glob, sys

# prefix map: old single-letter -> (new dashed prefix, max entry seen)
PMAP = {
    'D': 'dmg-loop',
    'R': 'dmg-decay',
    'E': 'env',
    'H': 'hostility',
    'S': 'thump-place',
    'F': 'thumper-def',
    'T': 'transport',
    'X': 'solid-world',
    'K': 'kill-rewards',
}
PMAP_UPPER = {k: v.upper() for k, v in PMAP.items()}

files = glob.glob('*.html')

def process(text):
    # 1. data-k triple pattern: data-k="d3-3-1"  -> data-k="dmg-loop-3-1"
    def sub_datak(m):
        letter, num, num2, step = m.group(1), m.group(2), m.group(3), m.group(4)
        if num != num2 or letter.upper() not in PMAP:
            return m.group(0)
        return 'data-k="%s-%s-%s"' % (PMAP[letter.upper()], num, step)
    text = re.sub(r'data-k="([a-zA-Z])(\d+)-(\d+)-(\d+)"', sub_datak, text)

    # 2. data-k note pattern: data-k="n-d1" -> data-k="n-dmg-loop-1"
    def sub_note(m):
        letter, num = m.group(1), m.group(2)
        if letter.upper() not in PMAP:
            return m.group(0)
        return 'data-k="n-%s-%s"' % (PMAP[letter.upper()], num)
    text = re.sub(r'data-k="n-([a-zA-Z])(\d+)"', sub_note, text)

    # 3. result box id/for: id="r-d1" / for="r-d1"
    def sub_result(m):
        attr, letter, num = m.group(1), m.group(2), m.group(3)
        if letter.upper() not in PMAP:
            return m.group(0)
        return '%s="r-%s-%s"' % (attr, PMAP[letter.upper()], num)
    text = re.sub(r'(id|for)="r-([a-zA-Z])(\d+)"', sub_result, text)

    # 4. section anchor id: id="-d1-weapon-damage-loop-end-to-end"
    def sub_anchor(m):
        letter, num = m.group(1), m.group(2)
        if letter.upper() not in PMAP:
            return m.group(0)
        return 'id="-%s-%s-' % (PMAP[letter.upper()], num)
    text = re.sub(r'id="-([a-zA-Z])(\d+)-', sub_anchor, text)

    # 4b. href fragment anchors: href="#-d1-..." or href="Damage-Loop.html#-d1-..."
    def sub_href_anchor(m):
        pre, letter, num = m.group(1), m.group(2), m.group(3)
        if letter.upper() not in PMAP:
            return m.group(0)
        return '%s#-%s-%s-' % (pre, PMAP[letter.upper()], num)
    text = re.sub(r'(href="[^"]*)#-([a-zA-Z])(\d+)-', sub_href_anchor, text)

    # 5. dotted sub-part: D7·A -> DMG-LOOP-7-A  (do before plain token sub)
    def sub_dotted(m):
        letter, num, sub = m.group(1), m.group(2), m.group(3)
        if letter not in PMAP_UPPER:
            return m.group(0)
        return '%s-%s-%s' % (PMAP_UPPER[letter], num, sub)
    text = re.sub(r'\b([A-Z])(\d{1,2})·([A-Z])\b', sub_dotted, text)

    # 6. plain uppercase token as whole word: D1, D2 ... R4 ... etc.
    def sub_token(m):
        letter, num = m.group(1), m.group(2)
        if letter not in PMAP_UPPER:
            return m.group(0)
        return '%s-%s' % (PMAP_UPPER[letter], num)
    text = re.sub(r'\b([A-Z])(\d{1,2})\b', sub_token, text)

    return text

for fn in files:
    with open(fn, encoding='utf-8') as f:
        orig = f.read()
    new = process(orig)
    if new != orig:
        with open(fn, 'w', encoding='utf-8') as f:
            f.write(new)
        print("updated:", fn)
    else:
        print("no change:", fn)
