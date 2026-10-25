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


# Fencer (Grenadier blindé). Armes de mêlée : frappe (trois niveaux de charge) et perforation ; boucliers ; canons
# automatiques ; artillerie (canons à main, canons lourds, mortiers) ; lance-missiles ; quatre types d'équipement de soutien.
FENCER = {
    'CC Strikers': ('Frappe', [
        (r'^Vulcan', 'vulcan'), (r'Roller', 'roller'), (r'Haken', 'haken'), (r'^Force Ax', 'forceax'),
        (r'Blade', 'blade'), (r'Hammer', 'hammer'),
    ], 'Strike'),
    'CC Piercers': ('Perforation', [
        (r'^Blasthole', 'blasthole'), (r'^Blast Twin', 'twinspear'), (r'^Flashing', 'flashing'), (r'^Jackhammer', 'jackhammer'),
        (r'^Spine Driver', 'spinedriver'),
    ], 'Pierce'),
    'Shields': ('Boucliers', [
        (r'Reflector', 'reflector'), (r'Ion Mirror', 'ionmirror'), (r'Tower|Great', 'tower'), (r'Deflection|Master', 'deflection'),
    ], 'Shield'),
    'Autocannons': ('Canons automatiques', [
        (r'^FGS', 'fgs'), (r'Hand Gatling', 'gatling'), (r'^Galleon', 'galleon'), (r'^Dexter', 'dexter'),
        (r'Flame Revolver', 'flamerevolver'), (r'Disruptor', 'disruptor'),
    ], 'Bullet'),
    'Cannons': ('Artillerie', [
        (r'Hand Cannon', 'handcannon'), (r'Gallic|Battle Cannon|Rage Cannon', 'gallic'), (r'^Powerdyne', 'powerdyne'),
        (r'Cannon Shot|Canister', 'cannonshot'), (r'^YH', 'dispersal'), (r'^Super-Heavy Crusher', 'crusher'),
        (r'^FHD|^FHE', 'heavymortar'), (r'^FH', 'lightmortar'),
    ], 'Bullet'),
    'Missile Launchers': ('Lance-missiles', [
        (r'Hound', 'armhound'), (r'Blood Storm', 'bloodstorm'), (r'Leviathan', 'leviathan'), (r'^Haytal', 'haytal'),
        (r'^Arcane', 'arcane'), (r'High Altitude', 'fgimpact'), (r'^Phoenix', 'phoenix'),
    ], 'Missile'),
    'Enhanced Boosters': ('Propulseurs', [(r'^Dash Cell', 'dashcell'), (r'^Add Booster', 'addbooster'), (r'^Multi', 'multicharger')], 'Gear'),
    'Enhanced Shields': ('Renforts de bouclier', [(r'^Deflect Cell', 'deflectcell'), (r'^Barricade', 'barricade'), (r'Protection Armor', 'shieldarmor')], 'Gear'),
    'Enhanced Cannons': ('Stabilisateurs', [(r'Muzzle', 'muzzle')], 'Gear'),
    'Enhanced Exoskeleton': ('Exosquelettes', [(r'Exoskeleton', 'exo')], 'Gear'),
}
F_KIND = {'flamerevolver': 'Flame', 'lightmortar': 'Mortar', 'heavymortar': 'Mortar', 'dispersal': 'Mortar', 'crusher': 'Mortar'}


# Air Raider (Opérateur d'appui) : frappes aériennes (artillerie, canonnières, bombardiers, missiles, satellites), bombes
# collantes, armes posées (robots, tourelles), équipements de soutien (guidage, soins, renforts), abris et leurres.
# Les véhicules (chars, blindés, hélicoptères, exosquelettes, armes spéciales) viendront à part.
AIRRAIDER = {
    'Arillery Units': ("Tirs d'artillerie", [
        (r'Howitzer', 'howitzer'), (r'^Cannon Gun', 'cannongun'), (r'^Gigantic', 'gigantic'), (r'Mortar', 'mortarstrike'),
    ], 'Artillery'),
    'Gunships': ('Canonnière', [
        (r'Neutralize', 'gsneutral'), (r'Rocket Cannon', 'gsrocket'), (r'Autocannon|[Mm]inigun|Vulcan', 'gsauto'),
        (r'^105mm', 'gs105'), (r'Triple|Quadruple', 'gsmulti'), (r'150mm|180mm|190mm', 'gs150'),
    ], 'Gunship'),
    'Bombers': ('Bombardiers', [
        (r'KM6', 'km6'), (r'Kamuy', 'kamuy'), (r'Vesta', 'vesta'), (r'Phobos', 'phobos'), (r'^Scout', 'scoutbomber'),
    ], 'Bomber'),
    'Missiles': ('Missiles guidés', [(r'Lionic', 'lionic'), (r'Tempest', 'tempest'), (r'Cruise|Missile Group', 'cruise')], 'AirMissile'),
    'Satellites': ('Satellites', [(r'^Bulge', 'bulge'), (r'^Spritefall', 'spritefall')], 'Satellite'),
    'Limpet Guns': ('Bombes collantes', [
        (r'Sniper', 'limpetsniper'), (r'Splendor', 'splendor'), (r'Chain', 'limpetchain'), (r'Detector', 'detector'),
        (r'^Limpet Shot', 'limpetshot'), (r'^Buster', 'busterbomb'), (r'^Limpet Gun', 'limpetgun'),
    ], 'Limpet'),
    'Stationary Weapons': ('Armes posées', [
        (r'Robot Bomb', 'robotbomb'), (r'Beetle', 'beetle'), (r'^Patroller', 'patroller'), (r'^Speed Star', 'speedstar'),
        (r'Sniper', 'zesniper'), (r'Launcher', 'zelauncher'), (r'Blaster', 'zeblaster'), (r'GUN', 'zegun'),
    ], 'Sentry'),
    'Support Equipment': ('Soutien', [
        (r'Guide Kit|Guided Device', 'guidekit'), (r'Beacon', 'beacongun'), (r'Life Vendor', 'lifevendor'),
        (r'Life Spout', 'lifespout'), (r'Power Assist', 'powerassist'), (r'Power Post|Offensive', 'powerpost'),
        (r'Guard Assist', 'guardassist'), (r'Guard Post|Zone Protector', 'guardpost'),
    ], 'Post'),
    'Special': ('Abris et leurres', [
        (r'^Decoy', 'decoy'), (r'^Suppress', 'suppress'), (r'Anti-Aircraft', 'aabunker'), (r'Prison', 'prison'),
        (r'Borderline|Electromagnetic Wall', 'emwall'), (r'Shelter', 'shelter'), (r'Heavy Bunker|Absolute', 'heavybunker'),
        (r'Bunker', 'embunker'),
    ], 'Bunker'),
}
AR_KIND = {'robotbomb': 'Robot', 'beetle': 'Robot', 'patroller': 'Robot', 'speedstar': 'Robot',
           'guidekit': 'Guide', 'beacongun': 'Guide', 'decoy': 'Decoy', 'suppress': 'Bullet'}


def ar_extra(r, fam, kind, p):
    """Champs propres à l'Air Raider (avant les champs communs)."""
    shots = get(r, 'Shots')
    if shots:
        m = re.match(r'\s*(\d+)(?:×(\d+))?', shots)
        p.append(f'Shots = {m.group(1)}')
        if m.group(2) and int(m.group(2)) > 1: p.append(f'Rows = {m.group(2)}')
    ar = num(get(r, 'Attack Range (Radius m)'))
    if ar: p.append(f'AttackRadius = {f(ar)}')
    rl = get(r, 'Reload (pt)', 'Reload')
    if rl:
        st_ = stat(rl)
        p.append(f'Reload = {st(st_)}')
        if 'pt' in rl or get(r, 'Reload (pt)'): p.append('PointReload = true')
    lr = stat(get(r, 'Laser Range (m)'))
    if lr: p.append(f'Range = {st(lr)}')
    heal = stat(get(r, 'Amount Healed (HP/Frame)', 'Increase/Reduction Rate (%)'))
    if heal: p.append(f'Damage = {st(heal)}')
    er = stat(get(r, 'Effective Range (Radius m)'))
    if er: p.append(f'Blast = {st(er)}')
    et = num(get(r, 'Effective Time (sec)'))
    if et: p.append(f'Duration = {f(et)}')
    bt = num(get(r, 'Burn Time (sec)'))
    if bt: p.append(f'Duration = {f(bt)}')
    du = stat(get(r, 'Durability'))
    if du: p.append(f'Durability = {st(du)}')
    la = num(get(r, 'Loaded Ammo'))
    if la: p.append(f'Loaded = {int(la)}')
    gs, gr = num(get(r, 'Lock-on Speed')), num(get(r, 'Lock-on Range'))
    if gs: p.append(f'GuideSpeed = {f(gs)}')
    if gr: p.append(f'GuideRange = {f(gr)}')


def strike(s):
    """« Clash Close Range(★9) 0.0 / 56.6×2 / 48.3m » → (attaque, charge, dégâts, étoiles, nombre, portée)."""
    if s is None or s.strip() in ('', '-'):
        return None
    m = re.match(r'\s*(.*?)\(★(\d+)\+?\)\s*([\d.]+)\s*/\s*([\d.]+)(?:×(\d+))?\s*/\s*([\d.]+)m', s)
    if not m:
        sys.exit(f'frappe illisible : {s}')
    return (m.group(1).strip(), float(m.group(3)), float(m.group(4)), int(m.group(2)), int(m.group(5) or 1), float(m.group(6)))


def thrust(s):
    """« [PT]453.0×2 / 40.0m(★8) » → (dégâts, étoiles, nombre, portée)."""
    if s is None or s.strip() in ('', '-'):
        return None
    m = re.match(r'\s*(?:\[PT\])?([\d.]+)(?:×(\d+))?\s*/\s*([\d.]+)m\(★(\d+)', s)
    if not m:
        sys.exit(f'attaque illisible : {s}')
    return (float(m.group(1)), int(m.group(4)), int(m.group(2) or 1), float(m.group(3)))


def pct_pair(s):
    """« 70% / 50% [Heavy] » → (0.7, 0.5) ; « - / - » → None."""
    v = re.findall(r'(\d+)%', s or '')
    return (int(v[0]) / 100, int(v[1]) / 100) if len(v) == 2 else None


def fencer_extra(r, fam, kind, p):
    """Champs propres au Fencer. Vrai : la ligne est complète (frappe, bouclier, équipement)."""
    if kind == 'Strike':
        lv = [strike(get(r, k)) for k in ('Regular (Charge/Damage/Range)', 'High Voltage (Charge/Damage/Range)', 'Maximum (Charge/Damage/Range)')]
        lv = [x for x in lv if x]
        top = max(lv, key=lambda x: x[2] * x[4])
        p.append(f'Damage = new({f(top[2])}, {max(x[3] for x in lv)})')
        p.append(f'Range = new({f(max(x[5] for x in lv))}, 0)')
        p.append('Strikes = new Edf5Strike[] { ' + ', '.join(
            f'new({json.dumps(x[0])}, {f(x[1])}, {f(round(x[2] / top[2], 4))}, {x[4]}, {f(x[5])})' for x in lv) + ' }')
        cs = stat(get(r, 'Charge Speed (sec)'))
        if cs: p.append(f'Swing = {st(cs)}')
        dd = num(get(r, 'Defense Damage(%)'))
        if dd: p.append(f'Guard = {f(dd / 100)}')
        return True
    if kind == 'Pierce':
        if get(r, 'Attack 1 (Damage/Range)'):
            a1, a2 = thrust(get(r, 'Attack 1 (Damage/Range)')), thrust(get(r, 'Attack 2 (Damage/Range)'))
            p.append(f'Damage = new({f(a2[0])}, {a2[1]})')
            p.append('Pierce = true')
            if a2[2] > 1: p.append(f'Pellets = {a2[2]}')
            p.append(f'Range = new({f(a2[3])}, 0)')
            p.append('Strikes = new Edf5Strike[] { ' + ', '.join(
                f'new("{n}", 0f, {f(round(a[0] / a2[0], 4))}, {a[2]}, {f(a[3])})' for n, a in (('Attack 1', a1), ('Attack 2', a2))) + ' }')
        sw = stat(get(r, 'Swing/Ready Speed'))
        if sw: p.append(f'Swing = {st(sw)}')
        return False
    if kind == 'Shield':
        p.append(f'Durability = {st(stat(get(r, "Durability")))}')
        p.append(f'GuardAngle = {f(num(get(r, "Defense Angle")))}')
        p.append(f'Guard = {f(num(get(r, "Damage Defense (%)")) / 100)}')
        p.append(f'ReflectRadius = {f(num(get(r, "Physical Reverse Range (Radius)")))}')
        p.append(f'ReflectCost = {f(num(get(r, "Reversal Durability Drop(%)")) / 100)}')
        ns = pct_pair(get(r, 'Normal Speed (Speed/Roatation)'))
        if ns: p.append(f'Carry = ({f(ns[0])}, {f(ns[1])})')
        ss = pct_pair(get(r, 'Stance Speed (Speed/Rotation)'))
        if ss: p.append(f'Stance = ({f(ss[0])}, {f(ss[1])})')
        return True
    if kind == 'Gear':
        g = {
            'Consecutive Dashes': 'Dashes', 'Consecutive Boosts': 'Boosts', 'Dash Interval (%)': 'DashInterval',
            'Horizontal Boost Speed (%)': 'BoostGain', 'Shield Consumption (%)': 'ShieldUse', 'Shields Durability': 'ShieldDurability',
            'Shield Defense Angle': 'ShieldAngle', 'Damage Deduction Rate (%)': 'ShieldCut', 'Knockback Reduction Rate (%)': 'KnockbackCut',
            'Recoil Reduction (%)': 'RecoilCut', 'Weight Impact on Speed (%)': 'WeightSpeed', 'Weight Impact on Rotation Inertia (%)': 'WeightTurn',
            'Walking Speed (%)': 'WalkSpeed',
        }
        for col, field in g.items():
            v = num(get(r, col))
            if v is None: continue
            if field in ('Dashes', 'Boosts'): p.append(f'{field} = {int(v)}')
            elif field == 'ShieldAngle': p.append(f'{field} = {f(v)}')
            else: p.append(f'{field} = {f(v / 100)}')
        return True
    return False


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
        if table is AIRRAIDER:
            ar_extra(r, fam, kind, p)
        if table is FENCER and fencer_extra(r, fam, kind, p):
            ammo = stat(get(r, 'Ammo'))
            if ammo and kind == 'Strike': p.append(f'Ammo = {st(ammo)}')
            rl = stat(get(r, 'Reload (sec)'))
            if rl and kind in ('Strike', 'Shield'): p.append(f'Reload = {st(rl)}')
            lines.append(f'        new({", ".join(a)}) {{ {", ".join(p)} }},')
            continue
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
        d = None if any(x.startswith(('Strikes', 'Damage')) for x in p) else damage((get(r, 'Damage', 'Damage/Amount Healed') or '').replace('x', '×') or None)
        ammo = stat(get(r, 'Ammo', 'Ammo/Number', 'Number'))
        if ammo: p.append(f'Ammo = {st(ammo)}')
        rof = stat(get(r, 'Rate of Fire (Fire/sec)', 'Rate of Fire (Shot/sec)'))
        if rof: p.append(f'Rof = {st(rof)}')
        if d:
            p.append(f'Damage = new({f(d[0])}, {d[1]})')
            if d[2] is not None and d[0] > 0: p.append(f'FarDamage = {f(d[2] / d[0])}')
            if d[3] > 1: p.append(f'Pellets = {d[3]}')
            if d[4] > 1: p.append(f'Burst = {d[4]}')
            if d[5]: p.append('Pierce = true')
        rl = stat(get(r, 'Reload (sec)', 'Charge Time(sec)', 'Reload(sec)'))
        if rl and not any(x.startswith('Reload') for x in p): p.append(f'Reload = {st(rl)}')
        en = stat(get(r, 'Energy Cost'))
        if en: p.append(f'Energy = {st(en)}')
        rg = stat(get(r, 'Range (m)', 'Range (sec)'))
        if table is AIRRAIDER and not any(x.startswith('Blast') for x in p) and stat(get(r, 'Blast Area (Radius m)')):
            p.append(f'Blast = {st(stat(get(r, "Blast Area (Radius m)")))}')
        if rg and not any(x.startswith('Range') for x in p): p.append(f'Range = {st(rg)}')
        sp = stat(get(r, 'Shot Speed (m/sec)'))
        if sp: p.append(f'Speed = {st(sp)}')
        acc = grade(get(r, 'Accuracy'))
        if acc: p.append(f'Accuracy = new({acc[0]}, {acc[1]})')
        bl = stat(get(r, 'Blast Area (m)', 'Effective Area (Radius m)', 'Blast Area (Radius m)'))
        if bl and not any(x.startswith('Blast') for x in p): p.append(f'Blast = {st(bl)}')
        z = get(r, 'Zoom')
        if z and num(z): p.append(f'Zoom = {f(num(z))}')
        if get(r, 'Laser Sight') == 'Equipped': p.append('LaserSight = true')
        lo = get(r, 'Lock-On (Range/Targets)', 'Lock-on (Range/Targets)', 'Lock-on(Range/Targets)')
        if lo:
            m = re.match(r'\s*([\d.]+)m\s*/\s*(\d+) Targets', lo)
            ls = re.search(r'★(\d+)', lo)
            p.append(f'LockRange = new({f(float(m.group(1)))}, {ls.group(1) if ls else 0})')
            p.append(f'LockTargets = {m.group(2)}')
            if 'Guide Kit' in lo: p.append('GuideOnly = true')
        lt = stat(get(r, 'Lock-On Time (sec)', 'Lock-on Time (sec)'))
        if lt: p.append(f'LockTime = {st(lt)}')
        h = get(r, 'Homing Capability')
        if h: p.append(f'Homing = {GRADES.index(h) if h in GRADES else 4 if h == "A++" else 5}')
        det = get(r, 'Detonates by')
        if det == 'Timed':
            p.append(f'Fuse = {f(num(get(r, "Detonation Time (sec)") or "3"))}')
        dl = stat(get(r, 'Delay (sec)'))
        if dl: p.append(f'SpinUp = {st(dl)}')
        sd = stat(get(r, 'Search Distance', 'Search Distance (m)'))
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
    write('AirRaider', "Armes et équipements de l'Air Raider d'EDF 5 (Opérateur d'appui), hors véhicules",
          gen_class([r for r in rows if r['class'] == 'Air Raider'], AIRRAIDER, AR_KIND))
    write('Fencer', "Armes et équipements de soutien du Fencer d'EDF 5 (Grenadier blindé)",
          gen_class([r for r in rows if r['class'] == 'Fencer'], FENCER, F_KIND))


main()
