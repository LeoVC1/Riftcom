import re
import json
import os

here = os.path.dirname(os.path.abspath(__file__))
html_path = os.path.join(here, 'gallery.html')
out_path = os.path.join(here, 'cards.json')

with open(html_path, encoding='utf-8', errors='ignore') as f:
    html = f.read()

# Same extraction the C# importer uses.
marker = '"cards":{"items":['
start = html.find(marker)
if start < 0:
    raise SystemExit('Bloco de cartas não encontrado (a página deve ter mudado).')

array_start = start + len(marker) - 1  # points at '['
i = array_start
depth = 0
in_str = False
esc = False
BS = chr(92)  # backslash

while i < len(html):
    c = html[i]
    if esc:
        esc = False
    elif c == BS and in_str:
        esc = True
    elif c == '"':
        in_str = not in_str
    elif not in_str:
        if c == '[':
            depth += 1
        elif c == ']':
            depth -= 1
            if depth == 0:
                break
    i += 1

raw = html[array_start:i+1]
items = json.loads(raw)

with open(out_path, 'w', encoding='utf-8') as f:
    json.dump(items, f, ensure_ascii=False, indent=2)

print(f'Total cards: {len(items)}')
print(f'Saved to: {out_path}')
print(f'File size: {os.path.getsize(out_path)} bytes')

# Distribuição por set
from collections import Counter
sets = Counter()
rarities = Counter()
types = Counter()
for c in items:
    v = c.get('set', {}).get('value', {})
    sets[(v.get('id'), v.get('label'))] += 1
    r = c.get('rarity', {}).get('value', {})
    rarities[r.get('id')] += 1
    for t in c.get('cardType', {}).get('type', []):
        types[t.get('id')] += 1

print('\nBy set:')
for (sid, slabel), n in sorted(sets.items()):
    print(f'  {sid:5s} {slabel:20s} {n}')
print('\nBy rarity:')
for r, n in rarities.most_common():
    print(f'  {r:12s} {n}')
print('\nBy card type:')
for t, n in types.most_common():
    print(f'  {t:15s} {n}')
