# Construction des modèles d'armes en arrière-plan (sans fenêtre) :
#   blender -b --factory-startup --python tools/blender/build_weapons.py -- <module> [familles…] [--export] [--preview DOSSIER]
#     [--views side,three] [--res 900x420]
# <module> : rifles, longguns, launchers, special, wingdiver, fencer, airraider (airraider : aussi les objets du monde « obj:nom »),
# crates (caisses de butin, objets du monde seulement : assets/models/crates/).
# --export : écrit assets/models/weapons/<famille>.glb (+ _mask.png, texture de détail cuite) ; airraider « obj:… » dans
# assets/models/airraid/. --palette : écrit seulement la palette du module (aucun modèle). --preview : rendus de contrôle <DOSSIER>/<famille>_<vue>.png. Sans famille : toutes celles du module.
import bpy, sys, os, time, traceback

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
mod = argv[0]
fams, export, prev, views, res = [], False, None, ("side", "three"), (900, 420)
i = 1
while i < len(argv):
    a = argv[i]
    if a == "--export": export = True
    elif a == "--palette": export, fams = True, ["--none--"]
    elif a == "--preview": prev = argv[i + 1]; i += 1
    elif a == "--views": views = tuple(argv[i + 1].split(",")); i += 1
    elif a == "--res": res = tuple(int(x) for x in argv[i + 1].split("x")); i += 1
    else: fams.append(a)
    i += 1

G = {"__name__": "weapons", "ROOT": ROOT}
for f in ("kit.py", f"{mod}.py", "preview.py"):
    exec(open(os.path.join(ROOT, "tools", "blender", "weapons", f), encoding="utf-8").read(), G)
if not fams:
    fams = list(G.get("FAMILIES", {}))
    if mod in ("airraider", "crates"):
        fams += ["obj:" + o for o in G["OBJECTS"]]
if prev:
    os.makedirs(prev, exist_ok=True)
if export:
    # Couleurs des familles (peinture, accent) lues par le jeu (SoldierModel.GunColors) : un fichier par module.
    import json
    pal = {k: [list(v[0]), list(v[1])] for k, v in G.get("PALETTE", {}).items() if v[0] is not None}
    pdir = os.path.join(ROOT, "assets", "models", "weapons", "palettes")
    os.makedirs(pdir, exist_ok=True)
    with open(os.path.join(pdir, mod + ".json"), "w", encoding="utf-8", newline="\n") as fo:
        json.dump(pal, fo, indent=1, sort_keys=True)
ok, err = 0, 0
for f in fams:
    if f == "--none--":
        break
    t0 = time.time()
    try:
        is_obj = f.startswith("obj:")
        name = f[4:] if is_obj else f
        folder = ("crates" if mod == "crates" else "airraid") if is_obj else "weapons"
        out = os.path.join(ROOT, "assets", "models", folder, name + ".glb") if export else None
        tris = G["make_object"](name, out) if is_obj else G["make"](name, out)
        if prev:
            # La cuisson remplace les matériaux : rendu fidèle seulement sans export (ou avant) ; on reconstruit si besoin.
            if export:
                G["make_object"](name) if is_obj else G["make"](name)
            G["preview"](os.path.join(prev, name), res, views)
        print(f"[modele] {f} : {tris} triangles, {time.time() - t0:.1f} s", flush=True)
        ok += 1
    except Exception:
        err += 1
        print(f"[modele] {f} ERREUR\n{traceback.format_exc()}", flush=True)
print(f"[modele] fin : {ok} ok, {err} erreurs", flush=True)
