import json, re, sys
t = open(sys.argv[1], encoding='utf-8').read()
# Découpe : classes (|-|Nom=) puis catégories (ligne "Nom=" seule).
out = []
cls = None
lines = t.split('\n')
cat = None
headers = None
row = None
def clean(c):
    c = re.sub(r'<ref[^>]*?/>|<ref[^>]*>.*?</ref>', '', c)
    c = re.sub(r'<br\s*/?>', ' ', c)
    c = re.sub(r'\[\[(?:[^|\]]*\|)?([^\]]*)\]\]', r'\1', c)
    c = re.sub(r"'''?", '', c)
    return c.strip()
for ln in lines:
    s = ln.strip()
    m = re.match(r'^\|-\|([^=]+)=', s)
    if m: cls = m.group(1).strip(); continue
    m = re.match(r'^([A-Za-z][^=|{}]*)=$', s)
    if m: cat = m.group(1).strip(); continue
    if s.startswith('!'):
        headers = [clean(h) for h in re.split(r'!!|\{\{!\}\}\{\{!\}\}', s[1:])]
        continue
    if s.startswith('{{!}}-'):
        continue
    if s.startswith('{{!}}') and not s.startswith('{{!}}}') and headers:
        cells = [clean(c) for c in s[5:].split('{{!}}{{!}}')]
        out.append({'class': cls, 'category': cat, **{h: (cells[i] if i < len(cells) else '') for i, h in enumerate(headers)}})
json.dump(out, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
from collections import Counter
c = Counter((r['class'], r['category']) for r in out)
for k, v in c.items(): print(k, v)
print(len(out))
