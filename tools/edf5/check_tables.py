# Vérifie les tables d'armes du jeu (scripts/Data/Edf5/Edf5Weapons.*.cs) contre la table de référence d'EDF 5
# (tools/edf5/weapons.json : wiki « Weapons EDF5 », relevé du site officiel weapon.edf.jpn.com/5_eng).
# Usage : uv run python tools/edf5/check_tables.py [--verbose]
# Compare, arme par arme (par nom), munitions, cadence, dégâts, rechargement / charge, portée, vitesse, souffle.
import json, re, sys, os, glob

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ref = json.load(open(os.path.join(ROOT, "tools", "edf5", "weapons.json"), encoding="utf-8"))
verbose = "--verbose" in sys.argv


def num(v):
    """Premier nombre d'une cellule (« 17.0～8.5(★8) » → 17.0) ; None si absent."""
    m = re.search(r"-?\d+(?:\.\d+)?", (v or "").replace(",", ""))
    return float(m.group()) if m else None


# Colonnes de la référence → champ des tables du jeu.
COLS = {
    "Ammo": ["Ammo", "Ammo/Number", "Number"],
    "Rof": ["Rate of Fire (Fire/sec)"],
    "Damage": ["Damage", "Damage/Amount Healed"],
    "Reload": ["Reload (sec)", "Charge Time(sec)"],
    "Range": ["Range (m)"],
    "Speed": ["Shot Speed (m/sec)"],
    "Blast": ["Blast Area (m)", "Blast Area (Radius m)", "Effective Area (Radius m)"],
}

game = {}
for path in glob.glob(os.path.join(ROOT, "scripts", "Data", "Edf5", "Edf5Weapons.*.cs")):
    for line in open(path, encoding="utf-8"):
        m = re.search(r'new\("([a-z0-9]+)", "([^"]+)", "[^"]*", Edf5Kind\.(\w+), "[^"]*", (\d+)\)', line)
        if not m:
            continue
        fields = {k: float(v) for k, v in re.findall(r"\b(Ammo|Rof|Damage|Reload|Range|Speed|Blast) = new\((-?[\d.]+)f", line)}
        # Clé : nom et niveau (deux « T3 Stork » dans EDF 5).
        game[(m.group(2), int(m.group(4)))] = (m.group(1), m.group(3), fields)

checked = mism = missing = 0
report = []
for r in ref:
    name = re.sub(r"\s*@DLC\d*$", "", r.get("Name", "")).strip()
    if not name or "Crew" in r or "Max Energy" in r:
        continue  # véhicules de l'Air Raider (hors périmètre), cœurs plasma
    key = (name, int(num(r.get("LV")) or 0))
    if key not in game:
        missing += 1
        report.append(f"absente du jeu : {r.get('class')} / {r.get('category')} / {name} (niv. {key[1]})")
        continue
    wid, kind, f = game[key]
    for field, cols in COLS.items():
        rv = next((num(r[c]) for c in cols if c in r and num(r[c]) is not None), None)
        if rv is None or field not in f:
            continue
        checked += 1
        gv = f[field]
        if abs(gv - rv) > max(0.011, abs(rv) * 0.005):
            mism += 1
            report.append(f"écart : {name} ({wid}) {field} jeu={gv} référence={rv}")

print(f"{checked} valeurs comparées, {mism} écarts, {missing} armes de la référence absentes du jeu")
for line in report if verbose else report[:40]:
    print("  " + line)
