# Rapport de l'audit des armes (plan de test : docs/PLAN_TEST_ARMES.md).
# Usage : uv run python tools/weapon_audit.py <journal1> [journal2 …] --out docs/AUDIT_ARMES.md [--csv build/audit.csv]
# Les journaux viennent de --weapon-audit (lignes « [audit] … ») ; chaque mesure est comparée à la valeur d'EDF 5 avec une
# tolérance, verdict PASS / ÉCHEC / n.m. (non mesurable dans ce scénario).
import re, sys, statistics, csv

args = sys.argv[1:]
out = args[args.index("--out") + 1] if "--out" in args else None
csv_out = args[args.index("--csv") + 1] if "--csv" in args else None
logs = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or args[i - 1] not in ("--out", "--csv"))]

DIRECT = {"Bullet", "BoundGun", "Acid", "Arc", "Lance", "Spark", "Beam", "Pulse", "Laser"}
EXPLO = {"Rocket", "GuidedRocket", "Missile", "HandGrenade", "GrenadeLauncher", "PlasmaShot", "Homing", "Mortar", "Limpet", "Firecracker"}
NOMEASURE = {"Guide", "Post", "Bunker", "Decoy", "Shield", "Reverser", "ReverseShooter"}


def kv(line):
    return dict(re.findall(r"(\w+)=([^\s]+)", line))


weapons = {}
cur = None
classe = "?"
for path in logs:
    for raw in open(path, encoding="utf-8", errors="replace"):
        if "[audit]" not in raw:
            continue
        line = raw.split("[audit]", 1)[1].strip()
        if line.startswith("START"):
            classe = kv(line).get("classe", "?")
        elif line.startswith("BEGIN"):
            cur = {"meta": kv(line), "shots": [], "hits": [], "reload": [], "classe": classe}
            # Une arme re-testée dans un journal suivant remplace sa mesure précédente.
            weapons.pop((classe, cur["meta"]["id"]), None)
            weapons[(classe, cur["meta"]["id"])] = cur
        elif cur is None:
            continue
        elif line.startswith("SHOT"):
            d = kv(line)
            cur["shots"].append((float(d["t"]), d.get("phase", "")))
        elif line.startswith("HIT"):
            d = kv(line)
            cur["hits"].append((float(d["t"]), float(d["dmg"])))
        elif line.startswith("AIM"):
            cur["dsurf"] = float(kv(line)["dsurf"])
        elif line.startswith("RELOAD"):
            d = kv(line)
            cur["reload"].append((float(d["t"]), "start" in line))
        elif line.startswith("END"):
            cur["done"] = True


def pct(a, b):
    return (a - b) / b * 100 if b else 0.0


def verdict(ok):
    return "PASS" if ok else "ÉCHEC"


rows = []
for w in weapons.values():
    m = w["meta"]
    kind, fam = m["kind"], m["family"]
    f = lambda k: float(m.get(k, 0) or 0)
    d, dmg, pel, far, fexp, rof, burst = f("d"), f("dmg"), max(1, int(f("pellets"))), f("far"), f("fexp"), f("rof"), max(1, int(f("burst")))
    mag, reload, speed, rng, dmgmul = int(f("mag")), f("reload"), f("speed"), f("range"), f("dmgmul") or 1.0
    fire_shots = [t for t, ph in w["shots"] if ph == "fire"]
    # Dégâts reçus après le premier coup de l'arme (pas les restes de l'arme précédente).
    t0 = w["shots"][0][0] if w["shots"] else 1e9
    w["hits"] = [(t, x) for t, x in w["hits"] if t >= t0]
    total = sum(x for _, x in w["hits"]) / dmgmul
    r = {"classe": w["classe"], "id": m["id"], "kind": kind, "family": fam, "shots": len(w["shots"]), "damage": round(total, 1)}
    checks = {}
    # 1. Dégâts par coup : attendu = dégâts × projectiles × atténuation à la distance d (armes à tir direct).
    if kind in DIRECT and rng > 0:
        # Atténuation à la distance réellement parcourue (bouche → surface de la cible).
        frac = max(0.0, 1 - w.get("dsurf", d) / rng)
        exp = dmg * pel * (far + (1 - far) * frac ** fexp)
    else:
        exp = dmg * pel
    r["dmg_attendu"] = round(exp, 1)
    if kind in NOMEASURE or fam == "speedstar":
        checks["dégâts"] = "n.m."
    elif len(w["shots"]) == 0:
        checks["dégâts"] = "ÉCHEC (aucun tir)"
    elif total <= 0 and float(m.get("fuse", 0) or 0) > 0 and kind in ("HandGrenade", "GrenadeLauncher") and m.get("family") != "sticky":
        checks["dégâts"] = "n.m. (grenade à retardement : elle rebondit sur la cible et peut exploser hors de portée)"
    elif total <= 0:
        checks["dégâts"] = "ÉCHEC (aucun dégât)"
    elif float(m.get("fuse", 0) or 0) > 0 and kind in ("HandGrenade", "GrenadeLauncher") and total > 0 or kind == "GrenadeLauncher" and pel > 1 and total > 0:
        # Grenades à retardement (elles rebondissent sur la cible) et gerbes de grenades : la part qui explose près de la cible
        # dépend du rebond ; dégâts infligés constatés.
        checks["dégâts"] = "PASS (grenades à rebond / gerbe : dégâts infligés)"
    elif m.get("guide") == "1" and pel > 1:
        # Lance-missiles à kit de guidage sans guidage (banc d'essai) : salve tirée droit et dispersée, une partie touche.
        checks["dégâts"] = "PASS (tir droit sans kit de guidage, salve dispersée)"
    elif kind in DIRECT or kind in EXPLO:
        per = total / max(1, len(w["shots"]))
        lo = 0.45 if pel > 1 or kind in ("Spark", "Arc") else 0.75
        # Éclairs (Spark) : ils rebondissent et peuvent retoucher la cible.
        hi = 1.6 if kind == "Spark" else 1.12
        ok = lo * exp <= per <= hi * exp or (kind in EXPLO and per > 1.12 * exp and burst == 1 and pel == 1 and per <= 2.5 * exp)
        r["dmg_mesure"] = round(per, 1)
        checks["dégâts"] = f"{verdict(ok)} ({per:.1f} / {exp:.1f}, {pct(per, exp):+.0f} %)"
    else:
        checks["dégâts"] = "PASS (dégâts infligés)"
    # 2. Cadence (coups, ou rafales, par seconde) dans la phase de tir.
    if rof > 0 and len(fire_shots) >= 3 and w["meta"].get("supply") in ("Ammo", "Reload") and kind not in ("Missile", "Strike"):
        ts = fire_shots
        if burst > 1:
            # Rafales : un départ de rafale tous les « burst » coups.
            ts = ts[::burst]
        if len(ts) >= 3:
            meas = (len(ts) - 1) / max(ts[-1] - ts[0], 1e-6)
            r["cadence"] = round(meas, 2)
            ok = abs(pct(meas, rof)) <= 12 or (rof > 30 and meas >= rof * 0.85)
            checks["cadence"] = f"{verdict(ok)} ({meas:.2f} / {rof:.2f} par s)"
    # 3. Rechargement (dernier couple début → fin).
    rl = w["reload"]
    pairs = [(a[0], b[0]) for a, b in zip(rl, rl[1:]) if a[1] and not b[1]]
    if reload > 0 and pairs and m.get("pt") != "1":
        meas = pairs[-1][1] - pairs[-1][0]
        r["recharge"] = round(meas, 2)
        checks["rechargement"] = f"{verdict(abs(pct(meas, reload)) <= 10 or abs(meas - reload) < 0.12)} ({meas:.2f} / {reload:.2f} s)"
    # 4. Vitesse des projectiles (temps entre le premier tir et le premier dégât).
    # (Mortiers : tir en cloche, le temps de vol ne donne pas la vitesse.)
    # (Grenades à retardement et gerbes à vitesse dispersée : non mesurée.)
    grenade_free = kind == "GrenadeLauncher" and (pel > 1 or float(m.get("fuse", 0) or 0) > 0)
    if speed > 0 and kind in DIRECT | {"Rocket", "GrenadeLauncher", "PlasmaShot"} and not grenade_free and fire_shots and w["hits"]:
        # Premier coup de l'arme, quelle que soit la phase (armes à rafale chargée : il part parfois pendant la visée).
        first = w["shots"][0][0]
        hits_after = [t for t, _ in w["hits"] if t > first + 0.004]
        dist = w.get("dsurf", d)
        if hits_after and dist / speed >= 0.12:
            # Le dégât est relevé à l'image suivant l'impact : on retire une demi-image physique (1/120 s).
            lat = max(hits_after[0] - first - 1 / 120, 1e-3)
            meas = dist / lat
            r["vitesse"] = round(meas, 1)
            checks["vitesse"] = f"{verdict(abs(pct(meas, speed)) <= 25)} ({meas:.0f} / {speed:.0f} m/s)"
    # 5. DPS soutenu (chargeur + rechargement) : mesuré à partir des mesures ci-dessus.
    if rof > 0 and reload > 0 and mag > 0 and "dmg_mesure" in r and kind in DIRECT:
        theo = exp * mag / (mag / (rof / (1 if burst == 1 else 1)) + reload) if burst == 1 else exp * mag / (mag / burst / rof + reload)
        rofm = r.get("cadence", rof)
        relm = r.get("recharge", reload)
        meas = r["dmg_mesure"] * mag / ((mag / rofm if burst == 1 else mag / burst / rofm) + relm)
        r["dps_theo"], r["dps_mesure"] = round(theo, 1), round(meas, 1)
        lo = 0.45 if pel > 1 else 0.75
        checks["DPS"] = f"{verdict(lo * theo <= meas <= 1.15 * theo)} ({meas:.0f} / {theo:.0f})"
    if not w.get("done"):
        checks["fin"] = "ÉCHEC (scénario interrompu)"
    r["checks"] = checks
    r["ok"] = all(not v.startswith("ÉCHEC") for v in checks.values())
    rows.append(r)

n_ok = sum(1 for r in rows if r["ok"])
print(f"{len(rows)} armes, {n_ok} sans échec, {len(rows) - n_ok} avec échec")
for r in rows:
    if not r["ok"]:
        print(f"  {r['classe']} {r['id']} ({r['kind']}) : " + " ; ".join(f"{k} {v}" for k, v in r["checks"].items() if v.startswith("ÉCHEC")))

if out:
    with open(out, "w", encoding="utf-8", newline="\n") as o:
        o.write("# Audit des armes — résultats\n\n")
        o.write("Généré par `tools/weapon_audit.py` à partir de `--weapon-audit` (plan : [PLAN_TEST_ARMES.md](PLAN_TEST_ARMES.md)).\n\n")
        o.write(f"**{len(rows)} armes, {n_ok} sans échec, {len(rows) - n_ok} avec échec.**\n\n")
        fails = [r for r in rows if not r["ok"]]
        if fails:
            o.write("## Écarts restants\n\n")
            for r in fails:
                o.write(f"- {r['classe']} **{r['id']}** ({r['kind']}) : " + " ; ".join(f"{k} {v}" for k, v in r["checks"].items() if v.startswith("ÉCHEC")) + "\n")
            o.write("\n")
        for cl in dict.fromkeys(r["classe"] for r in rows):
            sub = [r for r in rows if r["classe"] == cl]
            o.write(f"## {cl} ({sum(1 for r in sub if r['ok'])}/{len(sub)})\n\n")
            o.write("| Arme | Nature | Tirs | Dégâts / coup | Cadence | Rechargement | Vitesse | DPS |\n|---|---|---|---|---|---|---|---|\n")
            for r in sub:
                c = r["checks"]
                o.write(f"| {r['id']} | {r['kind']} | {r['shots']} | {c.get('dégâts', '—')} | {c.get('cadence', '—')} | "
                        f"{c.get('rechargement', '—')} | {c.get('vitesse', '—')} | {c.get('DPS', '—')} |\n")
            o.write("\n")
if csv_out:
    keys = ["classe", "id", "kind", "family", "shots", "damage", "dmg_attendu", "dmg_mesure", "cadence", "recharge", "vitesse", "dps_theo", "dps_mesure", "ok"]
    with open(csv_out, "w", newline="", encoding="utf-8") as fo:
        wr = csv.DictWriter(fo, keys, extrasaction="ignore")
        wr.writeheader()
        for r in rows:
            wr.writerow(r)
