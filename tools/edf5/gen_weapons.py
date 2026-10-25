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

GRADES = ['S++', 'S+', 'S', 'A+', 'A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D', 'E', 'F', 'G']

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


# Wing Diver (Commando aéroporté) : (catégorie française, familles, nature par défaut). Mode d'alimentation par famille
# (atwiki EDF5, « エネルギー供給方式 ») : Reload = recharge payée en énergie, Charge = tir chargé (bouton tenu), ChargeBurst =
# chargeur rempli en tenant le bouton puis vidé en rafale, Direct = chaque tir payé aussitôt (guidées, spéciales).
WINGDIVER = {
    'Short Range': ('Courte portée', [
        (r'Rapier', 'rapier'), (r'^Phalanx', 'phalanx'), (r'^Power Lance|^(Double|Triple|Twin) Lance', 'powerlance'),
        (r'^Dragoon', 'dragoon'), (r'^Spark (Vine|Foil)', 'sparkvine'), (r'^Spark Whip', 'sparkwhip'), (r'^Monster Lance', 'monsterlance'),
    ], 'Arc'),
    'Kinetic Weapons': ('Blasters', [
        (r'^Mag (Blaster|Laser)', 'magblaster'), (r'^Destroyer', 'destroyer'), (r'^Spine', 'spine'), (r'^Reflectron', 'reflectron'),
    ], 'Beam'),
    'Energy Weapons': ('Électriques', [
        (r'^Lightning', 'lightningbow'), (r'^Bolt (Gun|Power|Maximum|Break)', 'boltgun'), (r'^Thunder Crossbow', 'crossbow'),
    ], 'Spark'),
    'Pulse Weapons': ('Pulsation', [
        (r'^Stardust', 'stardust'), (r'^Pulse Buster', 'pulsebuster'), (r'Pulse (Machine|Rapid)', 'pulsemg'),
    ], 'Pulse'),
    'Long Range': ('Longue portée', [
        (r'^Closed', 'closedlaser'), (r'^Bolt Shooter', 'boltshooter'), (r'^MONSTER', 'monster'), (r'^Raijin', 'raijin'),
    ], 'Laser'),
    'Ranged Weapons': ('Artillerie plasma', [
        (r'^Plasmafall', 'plasmafall'), (r'^Plasma (Big|Great) Cannon', 'bigcannon'), (r'^Plasma Heavy', 'heavycannon'),
        (r'^Plasma Burst', 'burstcannon'), (r'Plasma Cannon', 'plasmacannon'), (r'^Grit', 'grit'), (r'^Splasher', 'splasher'),
        (r'^Blink|^Split Ball', 'blinkball'),
    ], 'PlasmaShot'),
    'Homing Weapons': ('Guidées', [
        (r'^Mirage|^Rapid Mirage', 'mirage'), (r'^Geist', 'geist'), (r'^Ghost', 'ghostchaser'), (r'^Fenrir', 'fenrir'), (r'^Psiblade', 'psiblade'),
    ], 'Homing'),
    'Special': ('Spéciales', [
        (r'Diffuser', 'diffuser'), (r'^Gleipnir|^EX Gleipnir', 'gleipnir'), (r'^Thunder (Cluster|Orb)', 'thundercluster'),
        (r'^Heaven', 'heavensgate'), (r'^Psi (Cluster|Blast)', 'psicluster'), (r'^Final Days', 'finaldays'),
    ], 'Orb'),
    'Plasma Cores': ('Cœurs plasma', [
        (r'Turbo', 'turbocore'), (r'Rush', 'rushcore'), (r'Reverse', 'reversecore'), (r'Loading', 'loadingcore'), (r'Jet', 'jetcore'),
        (r'Return', 'returncore'), (r'Sky High', 'skyhighcore'), (r'Bird', 'birdcore'), (r'Big', 'bigcore'), (r'Mighty', 'mightycore'),
        (r'Plasma Core', 'plasmacore'),
    ], 'Core'),
}

WD_KIND = {'powerlance': 'Lance', 'dragoon': 'Lance', 'monsterlance': 'Laser', 'sparkvine': 'Spark', 'sparkwhip': 'Spark',
           'boltshooter': 'Spark', 'raijin': 'Spark', 'grit': 'PlasmaShot', 'blinkball': 'HandGrenade'}
SUPPLY = {
    'rapier': 'Reload', 'phalanx': 'ChargeBurst', 'powerlance': 'Charge', 'dragoon': 'Charge', 'sparkvine': 'Reload',
    'sparkwhip': 'Reload', 'monsterlance': 'ChargeBurst', 'magblaster': 'Reload', 'destroyer': 'Reload', 'spine': 'Reload',
    'reflectron': 'Reload', 'lightningbow': 'ChargeBurst', 'boltgun': 'Reload', 'crossbow': 'Charge', 'pulsemg': 'ChargeBurst',
    'stardust': 'Charge', 'pulsebuster': 'ChargeBurst', 'closedlaser': 'ChargeBurst', 'boltshooter': 'Reload',
    'monster': 'ChargeBurst', 'raijin': 'Charge', 'plasmacannon': 'Charge', 'plasmafall': 'Charge', 'bigcannon': 'Charge',
    'heavycannon': 'Charge', 'burstcannon': 'ChargeBurst', 'grit': 'ChargeBurst', 'splasher': 'ChargeBurst',
    'blinkball': 'Reload', 'mirage': 'Direct', 'geist': 'Direct', 'ghostchaser': 'Direct', 'fenrir': 'Direct',
    'psiblade': 'Direct', 'diffuser': 'Direct', 'gleipnir': 'Direct', 'thundercluster': 'Direct', 'heavensgate': 'Direct',
    'psicluster': 'Direct', 'finaldays': 'Direct',
}
# Exceptions par arme (atwiki) : versions à recharge d'armes chargées.
SUPPLY_BY_NAME = {'Raijin Limit Custom': 'Reload', 'Psi Blast Wall': 'Reload'}


def boost(s):
    """« Forward+25%/Sideways+15% » → (avant, côté, arrière) en fractions."""
    out = {'Forward': 0.0, 'Sideways': 0.0, 'Backward': 0.0}
    for k, v in re.findall(r'(Forward|Sideways|Backward)\+(\d+)%', s or ''):
        out[k] = int(v) / 100
    return out['Forward'], out['Sideways'], out['Backward']


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
    g = re.match(r'(S\+\+|S\+|S|A\+|A-|A|B\+|B-|B|C\+|C-|C|D|E|F|G)', s.strip())
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


def gen_class(rows, table, kinds, supply=None):
    lines = []
    ids = set()
    for r in rows:
        cat = r['category']
        if cat not in table:
            continue
        fr, fams, base = table[cat]
        name = r['Name'].replace(' @DLC1', '').replace(' @DLC2', '').strip()
        dlc = 1 if '@DLC1' in r['Name'] else 2 if '@DLC2' in r['Name'] else 0
        fam = next((fm for pat, fm in fams if re.search(pat, name)), None)
        if fam is None:
            sys.exit(f'famille inconnue : {cat} / {name}')
        kind = kinds.get(fam, base)
        wid = slug(name)
        while wid in ids:
            wid += 'b'
        ids.add(wid)
        a = [f'"{wid}"', json.dumps(name, ensure_ascii=False), f'"{fr}"', f'Edf5Kind.{kind}', f'"{fam}"', r['LV']]
        p = []
        if dlc: p.append(f'Dlc = {dlc}')
        if supply is not None and kind != 'Core':
            p.append(f'Supply = Edf5Supply.{SUPPLY_BY_NAME.get(name, supply.get(fam, "Reload"))}')
        if kind == 'Core':
            p.append(f'MaxEnergy = {st(stat(get(r, "Max Energy")))}')
            p.append(f'EnergyCharge = {f(num(get(r, "Charge (Energy/sec)")) or 0)}')
            p.append(f'EmergencyCharge = {st(stat(get(r, "EMG Charge (Energy/sec)")))}')
            p.append(f'FlightCost = {f(num(get(r, "Flight Consumption (Energy/sec)")) or 0)}')
            p.append(f'BoostCost = {f(num(get(r, "Boost Consumption")) or 0)}')
            fw, sd, bw = boost(get(r, 'Boost Speed'))
            if fw or sd or bw: p.append(f'BoostSpeed = ({f(fw)}, {f(sd)}, {f(bw)})')
            wc = get(r, 'Weapon Charge Speed')
            if wc and num(wc): p.append(f'WeaponCharge = {f(num(wc) / 100)}')
            fs = get(r, 'Flight Speed')
            if fs and 'Greatly' in fs: p.append('FlightSpeed = -0.6f')
            elif fs and num(fs): p.append(f'FlightSpeed = {f(num(fs) / 100)}')
            lines.append(f'        new({", ".join(a)}) {{ {", ".join(p)} }},')
            continue
        d = damage(get(r, 'Damage', 'Damage/Amount Healed'))
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
        rl = stat(get(r, 'Reload (sec)', 'Charge Time(sec)'))
        if rl: p.append(f'Reload = {st(rl)}')
        en = stat(get(r, 'Energy Cost'))
        if en: p.append(f'Energy = {st(en)}')
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
        lo = get(r, 'Lock-On (Range/Targets)', 'Lock-on (Range/Targets)')
        if lo:
            m = re.match(r'\s*([\d.]+)m\s*/\s*(\d+) Targets', lo)
            ls = re.search(r'★(\d+)', lo)
            p.append(f'LockRange = new({f(float(m.group(1)))}, {ls.group(1) if ls else 0})')
            p.append(f'LockTargets = {m.group(2)}')
        lt = stat(get(r, 'Lock-On Time (sec)', 'Lock-on Time (sec)'))
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


def write(name, doc, lines):
    with open(os.path.join(OUT, f'Edf5Weapons.{name}.cs'), 'w', encoding='utf-8', newline='\n') as o:
        o.write('// Fichier généré par tools/edf5/gen_weapons.py — ne pas modifier à la main.\n')
        o.write('namespace Rempart;\n\npublic static partial class Edf5Weapons\n{\n')
        o.write(f'    /// <summary>{doc} : valeurs au maximum d\'étoiles de chaque caractéristique.</summary>\n')
        o.write(f'    public static readonly Edf5Spec[] {name} =\n    {{\n')
        o.write('\n'.join(lines) + '\n    };\n}\n')
    print(f'{name} : {len(lines)} armes')


def main():
    rows = json.load(open(SRC, encoding='utf-8'))
    os.makedirs(OUT, exist_ok=True)
    write('Ranger', "Armes du Ranger d'EDF 5 (Fusilier)", gen_class([r for r in rows if r['class'] == 'Ranger'], RANGER, KIND))
    write('WingDiver', "Armes et cœurs plasma de la Wing Diver d'EDF 5 (Commando aéroporté)",
          gen_class([r for r in rows if r['class'] == 'Wing Diver'], WINGDIVER, WD_KIND, SUPPLY))


main()
