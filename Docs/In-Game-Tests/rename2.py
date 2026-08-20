import re, glob

PMAP = {
    'B': 'death-respawn',
    'V': 'deploy-veh',
    'I': 'inventory',
    'N': 'npc-combat',
    'C': 'persist',
    'P': 'predict-sweep',
    'L': 'reliability',
    'G': 'resource-payout',
    'W': 'constraints',
    'K': 'kill-rewards',  # rerun-safe: already-converted files have no bare K left
}
PMAP_UPPER = {k: v.upper() for k, v in PMAP.items()}

def process(text):
    # D5 family special case (Charge-Camera stole D5 from Damage-Loop's numbering).
    # Handles bare D5, dotted D5·a, and appended D5a / D5h etc. Keeps the digit 5.
    def sub_d5(m):
        suffix = m.group(1) or m.group(2) or ''
        return 'CHARGE-CAM-5%s' % suffix
    text = re.sub(r'\bD5·([a-zA-Z])\b|\bD5([a-h])\b', sub_d5, text)
    text = re.sub(r'\bD5\b', 'CHARGE-CAM-5', text)
    # also the lowercase lone data-k for charge-camera's own entries: d5-*, d5a-* etc if present
    def sub_d5_datak(m):
        suf = m.group(1) or ''
        num, step = m.group(2), m.group(3)
        return 'data-k="charge-cam-5%s-%s"' % (suf, step)
    text = re.sub(r'data-k="d5([a-h]?)-(\d+)-(\d+)"', sub_d5_datak, text)

    def sub_datak(m):
        letter, num, step = m.group(1), m.group(2), m.group(4)
        if letter.upper() not in PMAP:
            return m.group(0)
        return 'data-k="%s-%s-%s"' % (PMAP[letter.upper()], num, step)
    text = re.sub(r'data-k="([a-zA-Z])(\d+)-(\d+)-(\d+)"', sub_datak, text)

    def sub_note(m):
        letter, num = m.group(1), m.group(2)
        if letter.upper() not in PMAP:
            return m.group(0)
        return 'data-k="n-%s-%s"' % (PMAP[letter.upper()], num)
    text = re.sub(r'data-k="n-([a-zA-Z])(\d+)"', sub_note, text)

    def sub_result(m):
        attr, letter, num = m.group(1), m.group(2), m.group(3)
        if letter.upper() not in PMAP:
            return m.group(0)
        return '%s="r-%s-%s"' % (attr, PMAP[letter.upper()], num)
    text = re.sub(r'(id|for)="r-([a-zA-Z])(\d+)"', sub_result, text)

    def sub_anchor(m):
        letter, num = m.group(1), m.group(2)
        if letter.upper() not in PMAP:
            return m.group(0)
        return 'id="-%s-%s-' % (PMAP[letter.upper()], num)
    text = re.sub(r'id="-([a-zA-Z])(\d+)-', sub_anchor, text)

    def sub_href_anchor(m):
        pre, letter, num = m.group(1), m.group(2), m.group(3)
        if letter.upper() not in PMAP:
            return m.group(0)
        return '%s#-%s-%s-' % (pre, PMAP[letter.upper()], num)
    text = re.sub(r'(href="[^"]*)#-([a-zA-Z])(\d+)-', sub_href_anchor, text)

    def sub_dotted(m):
        letter, num, sub = m.group(1), m.group(2), m.group(3)
        if letter not in PMAP_UPPER:
            return m.group(0)
        return '%s-%s-%s' % (PMAP_UPPER[letter], num, sub)
    text = re.sub(r'\b([A-Z])(\d{1,2})·([A-Z])\b', sub_dotted, text)

    def sub_token(m):
        letter, num = m.group(1), m.group(2)
        if letter not in PMAP_UPPER:
            return m.group(0)
        return '%s-%s' % (PMAP_UPPER[letter], num)
    text = re.sub(r'\b([A-Z])(\d{1,2})\b', sub_token, text)

    return text

for fn in glob.glob('*.html'):
    with open(fn, encoding='utf-8') as f:
        orig = f.read()
    new = process(orig)
    if new != orig:
        with open(fn, 'w', encoding='utf-8') as f:
            f.write(new)
        print("updated:", fn)
