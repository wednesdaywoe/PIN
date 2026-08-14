#!/usr/bin/env python3
"""Inline data.js and dump-1962.js into index.html so the wiki opens standalone."""
import re, sys, os
html = open('index.html', encoding='utf-8').read()
for name in ('data.js', 'dump-1962.js'):
    body = open(name, encoding='utf-8').read()
    html = html.replace(f'<script src="{name}"></script>',
                        f'<script>\n/* inlined from {name} */\n{body}\n</script>')
out = sys.argv[1] if len(sys.argv) > 1 else 'firefall-reference.html'
open(out, 'w', encoding='utf-8').write(html)
print(f'{out}: {os.path.getsize(out)/1024:.0f} KB')
