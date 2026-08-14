#!/usr/bin/env python3
"""Inline every external script into index.html so the wiki opens standalone."""
import re, sys, os
html = open('index.html', encoding='utf-8').read()
for name in ('data.js', 'dump-1962.js',
             'firefall-timeline-route.js', 'firefall-constraints-route.js',
             'firefall-crafting-route.js'):
    body = open(name, encoding='utf-8').read()
    # The HTML parser ends a script element at the first </script it sees, even
    # inside a comment or a string. The route modules quote their own script
    # tags in their wiring notes, so without this the build silently truncates
    # a module mid-file and everything after it is parsed as markup.
    body = body.replace('</script', r'<\/script')
    html = html.replace(f'<script src="{name}"></script>',
                        f'<script>\n/* inlined from {name} */\n{body}\n</script>')
out = sys.argv[1] if len(sys.argv) > 1 else 'firefall-reference.html'
open(out, 'w', encoding='utf-8').write(html)
print(f'{out}: {os.path.getsize(out)/1024:.0f} KB')
