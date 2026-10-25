# Génère scripts/Data/Edf5/Edf5Weapons.<Classe>.cs depuis tools/edf5/weapons.json (tables du wiki
# theearthdefenseforce.fandom.com « Weapons EDF5 », elles-mêmes issues du site officiel weapon.edf.jpn.com/5_eng).
# Usage : uv run python tools/edf5/gen_weapons.py
#
# Chaque valeur des tables est celle du maximum d'étoiles de la caractéristique (« 131(★7) » : 131 à ★7) ;
# une valeur sans étoile est fixe. « 17.0～8.5 » : dégâts à bout portant ～ en fin de portée (atténuation).
import json, re, sys, os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, 'tools', 'edf5', 'weapons.json')
OUT = os.path.join(ROOT, 'scripts', 'Data', 'Edf5')

GRADES = ['S++', 'S+', 'S', 'A+', 'A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D', 'E', 'F']

# Familles de modèles 3D (un modèle Blender par famille, variantes par arme) : (motif du nom, famille, nature).
RANGER = {
    'Assault Rifles': ('Fusil d\'assaut', [
        (r'^PA-11', 'pa11'), (r'^Minion', 'minion'), (r'^Slugger NN', 'slugger'), (r'^G&M-A2[59]', 'gm64'), (r'^G&M', 'gm'),
        (r'Raven|Sirius', 'raven'), (r'^(T4|T5|TZ) Stork', 'stork4'), (r'Stork', 'stork'), (r'Orchid', 'orchid'), (r'Slade', 'slade'), (r'^Blazer', 'blazer'),
    ], 'Bullet'),
    'Shotguns': ('Fusil à pompe', [
        (r'^Slaughter', 'slaughter'), (r'^SGN', 'sgn'), (r'Herring', 'herring'), (r'Breecher', 'breecher'),
        (r'^Monsoon', 'monsoon'), (r'^Slugger Shot', 'slugshot'), (r'^Buster Shot', 'bustershot'), (r'^Fracture', 'fracture'),
    ], 'Bullet'),
    'Sniper Rifles': ('Fusil de précision', [
        (r'^KFF', 'kff'), (r'^NP', 'np'), (r'Lysander', 'lysander'), (r'Fang', 'fang'), (r'^Dunkel', 'dunkel'),
        (r'^Eagle', 'eagle'), (r'^Flounders', 'flounders'), (r'^Ptarmigan', 'ptarmigan'), (r'^Pylon', 'pylon'), (r'^Penetrator', 'penetrator'),
    ], 'Bullet'),
    'Rocket Launchers': ('Lance-roquettes', [
        (r'^Grant', 'grant'), (r'^Leopard', 'leopard'), (r'^Goliath', 'goliath'), (r'^Cascade', 'cascade'),
        (r'Hornet', 'hornet'), (r'^Volcano', 'volcano'), (r'^Ibis', 'ibis'),
    ], 'Rocket'),
    'Missile Launchers': ('Lance-missiles', [
        (r'Emerald', 'emerald'), (r'^Air Tortoise', 'tortoise'), (r'^MLRA', 'mlra'), (r'^FORK', 'fork'),
        (r'^Prominence', 'prominence'), (r'^MS Falcon', 'falcon'),
    ], 'Missile'),
    'Grenades': ('Grenades', [
        (r'^MG\d', 'mg'), (r'^DNG', 'dng'), (r'^Bound Grenade', 'boundgren'), (r'^Grenade Launcher', 'um'),
        (r'Napalm', 'napalm'), (r'^Stampede', 'stampede'), (r'^Splash', 'splash'), (r'^Sticky', 'sticky'),
        (r'BRUTE', 'brute'), (r'^Flame Geyser', 'geyser'), (r'CRUMBLE', 'crumble'),
    ], 'Grenade'),
    'Special': ('Armes spéciales', [
        (r'Acid', 'acid'), (r'^Burner|^Flamethrower', 'flamer'), (r'^Flame Cannon|^Magma', 'flamecannon'),
        (r'^Firecracker', 'firecracker'), (r'Bound (Gun|Shotgun)', 'boundgun'), (r'Bomb$|Special Bomb', 'cbomb'),
        (r'Impulse', 'impulse'), (r'Reverse (Shooter|Bomber)', 'revshooter'), (r'Revers|Reverse Gun', 'reverser'),
    ], 'Special'),
}

# Nature de tir par famille (dispatch dans Player.Edf5.cs).
KIND = {
    'leopard': 'GuidedRocket', 'ibis': 'GuidedRocket',
    'mg': 'HandGrenade', 'dng': 'HandGrenade', 'boundgren': 'HandGrenade',
    'um': 'GrenadeLauncher', 'brute': 'GrenadeLauncher', 'crumble': 'GrenadeLauncher', 'stampede': 'GrenadeLauncher',
    'splash': 'GrenadeLauncher', 'sticky': 'GrenadeLauncher', 'napalm': 'Napalm', 'geyser': 'Napalm',
    'acid': 'Acid', 'flamer': 'Flame', 'flamecannon': 'Flame', 'firecracker': 'Firecracker', 'boundgun': 'BoundGun',
    'cbomb': 'Bomb', 'impulse': 'Impulse', 'reverser': 'Reverser', 'revshooter': 'ReverseShooter',
}


def num(s):
    m = re.search(r'-?\d+(?:\.\d+)?', s or '')
    return float(m.group(0)) if m else None


def stat(s):
    """« 131(★7) » → (131, 7) ; « 12.0 » → (12, 0) ; « - » → None."""
    if s is None or s.strip() in ('', '-'):
        return None
    v = num(s)
    if v is None:
        return None
    m = re.search(r'★(\d+)', s)
    return (v, int(m.group(1)) if m else 0)


def damage(s):
    """Dégâts : (max, étoiles, fin de portée ou None, projectiles, rafale, perforant)."""
    if s is None or s.strip() in ('', '-'):
        return None
    pt = '[PT]' in s
    s2 = s.replace('[PT]', '').strip()
    burst = re.search(r'\((\d+) Burst\)', s2)
    stars = re.search(r'★(\d+)', s2)
    head = re.split(r'\(★', s2)[0]
    pel = re.search(r'×(\d+)', head)
    rng = re.match(r'\s*(\d+(?:\.\d+)?)\s*～\s*(\d+(?:\.\d+)?)', head)
    if rng:
        mx, far = float(rng.group(1)), float(rng.group(2))
    else:
        mx, far = num(head), None
    return (mx, int(stars.group(1)) if stars else 0, far, int(pel.group(1)) if pel else 1,
            int(burst.group(1)) if burst else 1, pt)


def grade(s):
    if s is None or s.strip() in ('', '-'):
        return None
    g = re.match(r'(S\+\+|S\+|S|A\+|A-|A|B\+|B-|B|C\+|C-|C|D|E|F)', s.strip())
    m = re.search(r'★(\d+)', s)
    return (GRADES.index(g.group(1)), int(m.group(1)) if m else 0) if g else None


def slug(name):
    s = name.replace('@DLC1', '').replace('@DLC2', '').replace('α', 'a').replace('&', '')
    return re.sub(r'[^a-z0-9]', '', s.lower())


def f(v):
    return f'{v:g}f'


def st(t):
    return 'default' if t is None else f'new({f(t[0])}, {t[1]})'


def get(r, *keys):
    for k in keys:
        if k in r and r[k] not in ('', '-'):
            return r[k]
    return None


def gen_ranger(rows):
    lines = []
    ids = set()
    for r in rows:
        cat = r['category']
        if cat not in RANGER:
            continue
        fr, fams, base = RANGER[cat]
        name = r['Name'].replace(' @DLC1', '').replace(' @DLC2', '').strip()
        dlc = 1 if '@DLC1' in r['Name'] else 2 if '@DLC2' in r['Name'] else 0
        fam = next((fm for pat, fm in fams if re.search(pat, name)), None)
        if fam is None:
            sys.exit(f'famille inconnue : {cat} / {name}')
        kind = KIND.get(fam, base)
        wid = slug(name)
        while wid in ids:
            wid += 'b'
        ids.add(wid)
        d = damage(get(r, 'Damage', 'Damage/Amount Healed'))
        a = [f'"{wid}"', json.dumps(name, ensure_ascii=False), f'"{fr}"', f'Edf5Kind.{kind}', f'"{fam}"', r['LV']]
        p = []
        if dlc: p.append(f'Dlc = {dlc}')
        ammo = stat(get(r, 'Ammo', 'Ammo/Number'))
        if ammo: p.append(f'Ammo = {st(ammo)}')
        rof = stat(get(r, 'Rate of Fire (Fire/sec)'))
        if rof: p.append(f'Rof = {st(rof)}')
        if d:
            p.append(f'Damage = new({f(d[0])}, {d[1]})')
            if d[2] is not None and d[0] > 0: p.append(f'FarDamage = {f(d[2] / d[0])}')
            if d[3] > 1: p.append(f'Pellets = {d[3]}')
            if d[4] > 1: p.append(f'Burst = {d[4]}')
            if d[5]: p.append('Pierce = true')
        rl = stat(get(r, 'Reload (sec)'))
        if rl: p.append(f'Reload = {st(rl)}')
        rg = stat(get(r, 'Range (m)', 'Range (sec)'))
        if rg: p.append(f'Range = {st(rg)}')
        sp = stat(get(r, 'Shot Speed (m/sec)'))
        if sp: p.append(f'Speed = {st(sp)}')
        acc = grade(get(r, 'Accuracy'))
        if acc: p.append(f'Accuracy = new({acc[0]}, {acc[1]})')
        bl = stat(get(r, 'Blast Area (m)', 'Effective Area (Radius m)'))
        if bl: p.append(f'Blast = {st(bl)}')
        z = get(r, 'Zoom')
        if z and num(z): p.append(f'Zoom = {f(num(z))}')
        if get(r, 'Laser Sight') == 'Equipped': p.append('LaserSight = true')
        lo = get(r, 'Lock-On (Range/Targets)')
        if lo:
            m = re.match(r'\s*([\d.]+)m\s*/\s*(\d+) Targets', lo)
            ls = re.search(r'★(\d+)', lo)
            p.append(f'LockRange = new({f(float(m.group(1)))}, {ls.group(1) if ls else 0})')
            p.append(f'LockTargets = {m.group(2)}')
        lt = stat(get(r, 'Lock-On Time (sec)'))
        if lt: p.append(f'LockTime = {st(lt)}')
        h = get(r, 'Homing Capability')
        if h: p.append(f'Homing = {GRADES.index(h) if h in GRADES else 4 if h == "A++" else 5}')
        det = get(r, 'Detonates by')
        if det == 'Timed':
            p.append(f'Fuse = {f(num(get(r, "Detonation Time (sec)") or "3"))}')
        sd = stat(get(r, 'Search Distance'))
        if sd: p.append(f'Search = {st(sd)}')
        lines.append(f'        new({", ".join(a)}) {{ {", ".join(p)} }},')
    return lines


def main():
    rows = json.load(open(SRC, encoding='utf-8'))
    os.makedirs(OUT, exist_ok=True)
    ranger = gen_ranger([r for r in rows if r['class'] == 'Ranger'])
    with open(os.path.join(OUT, 'Edf5Weapons.Ranger.cs'), 'w', encoding='utf-8', newline='\n') as o:
        o.write('// Fichier généré par tools/edf5/gen_weapons.py — ne pas modifier à la main.\n')
        o.write('namespace Rempart;\n\npublic static partial class Edf5Weapons\n{\n')
        o.write('    /// <summary>Armes du Ranger d\'EDF 5 (Fusilier) : valeurs au maximum d\'étoiles de chaque caractéristique.</summary>\n')
        o.write('    public static readonly Edf5Spec[] Ranger =\n    {\n')
        o.write('\n'.join(ranger) + '\n    };\n}\n')
    print(f'Ranger : {len(ranger)} armes')


main()
