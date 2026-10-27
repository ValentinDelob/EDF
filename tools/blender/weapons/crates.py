# Caisses de butin (objets du monde → assets/models/crates/<nom>.glb), dans l'esprit d'EDF 5 : caisse d'arme verte, caisse
# d'armure rouge, mallette de soin blanche à croix rouge. Matériel militaire robuste et lisible de loin à sa silhouette :
#   arme   = conteneur long d'armement (coque en deux parties, couvercle nervuré, cornières rivetées, poignées pendantes aux
#            bouts, quatre grenouillères, patins, voyants) — silhouette allongée et basse ;
#   armure = caisse cubique durcie (nervures moulées sur les quatre faces, gros butoirs caoutchouc aux huit coins, écusson
#            de bouclier en relief sur le couvercle, poignées encastrées) — silhouette trapue ;
#   soin   = mallette médicale debout (coque blanche, grandes croix rouges en relief sur les flancs, poignée sur le dessus,
#            coins caoutchouc, bandeau rouge) — silhouette haute et mince.
# Repère du kit : x droite, u avant, z haut ; origine au sol, au centre de la caisse (la caisse est posée sur z = 0).
# Matériaux : Paint (couleur de la caisse), Accent (seconde couleur), Metal, Alloy, Rubber, Polymer, Marking (pochoirs),
# Glow (voyants : teinte de la caisse dans le jeu).
import os
exec(open(os.path.join(ROOT, "tools", "blender", "weapons", "airraider.py"), encoding="utf-8").read(), globals())
FAMILIES = {}

# Couleurs des rendus de contrôle (linéaires) ; dans le jeu, Crates.Paint / Crates.Accent.
GREEN, RED, WHITE, DARK, CROSS = (0.17, 0.26, 0.11), (0.42, 0.045, 0.035), (0.78, 0.78, 0.74), (0.07, 0.07, 0.075), (0.55, 0.03, 0.025)
OBJECTS = ["crate_weapon", "crate_armor", "crate_health"]
PALETTE = {"crate_weapon": (GREEN, DARK), "crate_armor": (RED, DARK), "crate_health": (WHITE, CROSS)}
MARK_AT = {o: None for o in OBJECTS}


def _shell(b, L, T, z0, z1, e, mat, p=7.0):
    """Coque : pavé à arêtes arrondies le long de u (superellipse d'exposant p), bouts chanfreinés de e."""
    zc, hh = (z0 + z1) / 2, (z1 - z0) / 2
    b.add(lofx([(-L / 2, 0, zc, T / 2 - e, hh - e, p - 1), (-L / 2 + e, 0, zc, T / 2, hh, p), (L / 2 - e, 0, zc, T / 2, hh, p),
                (L / 2, 0, zc, T / 2 - e, hh - e, p - 1)], 40), mat)


def _latch(b, x, u, z, side=1, w=0.07):
    """Grenouillère sur un flanc (normale ±x) : embase, levier à crochet, axe, gâche sur le couvercle."""
    b.add(box(0.012, w, 0.06, (x + side * 0.006, u, z - 0.03), 0.004), POLY)
    b.add(box(0.012, w * 0.8, 0.07, (x + side * 0.016, u, z - 0.01), 0.004), METAL)
    b.add(tube(0.006, 0.006, u - w * 0.42, u + w * 0.42, x + side * 0.02, z - 0.045, 10), ALLOY)
    b.add(box(0.016, w * 0.55, 0.022, (x + side * 0.01, u, z + 0.035), 0.004), METAL)
    b.add(box(0.008, w * 0.35, 0.012, (x + side * 0.024, u, z + 0.026), 0.002), ALLOY)


def _corner_bracket(b, x, u, z, sx, su, sz, size=0.07, mat=ALLOY):
    """Cornière d'angle rivetée (trois ailes) sur le coin (x, u, z) d'orientation (sx, su, sz)."""
    t = 0.006
    b.add(box(t, size, size, (x + sx * t / 2, u - su * size / 2, z - sz * size / 2), 0.002), mat)
    b.add(box(size, t, size, (x - sx * size / 2, u + su * t / 2, z - sz * size / 2), 0.002), mat)
    b.add(box(size, size, t, (x - sx * size / 2, u - su * size / 2, z + sz * t / 2), 0.002), mat)
    for (du, dz) in ((0.022, 0.022), (0.046, 0.022), (0.022, 0.046)):
        b.add(ball(0.0045, (x + sx * (t + 0.001), u - su * du, z - sz * dz), 8), METAL)
        b.add(ball(0.0045, (x - sx * du, u + su * (t + 0.001), z - sz * dz), 8), METAL)


def _drop_handle(b, u, z, side, w=0.16):
    """Poignée pendante au bout de la caisse (normale ±u) : deux chapes, anse cintrée, manchon caoutchouc."""
    for x in (-w / 2, w / 2):
        b.add(box(0.03, 0.014, 0.04, (x, u + side * 0.007, z), 0.004), METAL)
    pts_ = [(-w / 2, u + side * 0.02, z - 0.005), (-w / 2, u + side * 0.035, z - 0.06), (-w * 0.35, u + side * 0.04, z - 0.085),
            (w * 0.35, u + side * 0.04, z - 0.085), (w / 2, u + side * 0.035, z - 0.06), (w / 2, u + side * 0.02, z - 0.005)]
    for p0, p1 in zip(pts_, pts_[1:]):
        b.add(rod(p0, p1, 0.007, 0.007, 10), METAL)
    b.add(rod((-w * 0.3, u + side * 0.04, z - 0.085), (w * 0.3, u + side * 0.04, z - 0.085), 0.013, 0.013, 14), RUBBER)


def crate_weapon():
    """Caisse d'arme : conteneur long d'armement vert olive."""
    clear()
    mats = materials(GREEN, DARK)
    g = Gun("crate_weapon")
    b = g.body
    L, T, H = 1.2, 0.56, 0.5
    zs = 0.33                       # joint coque / couvercle
    # Patins sous la caisse (deux longerons), coque basse, joint caoutchouc, couvercle.
    for x in (-T * 0.32, T * 0.32):
        b.add(box(0.07, L - 0.06, 0.04, (x, 0, 0.02), 0.008), METAL)
        for u in (-L * 0.42, 0.0, L * 0.42):
            b.add(tz(0.012, 0.012, 0.04, 0.05, x, u, 12), ALLOY)
    _shell(b, L, T, 0.04, zs - 0.006, 0.03, PAINT)
    _shell(b, L + 0.012, T + 0.012, zs - 0.012, zs + 0.006, 0.03, RUBBER, 9)
    _shell(b, L + 0.02, T + 0.02, zs + 0.004, H - 0.03, 0.035, PAINT)
    # Dessus du couvercle : panneau surélevé à nervures longitudinales, plaque d'identification, voyants.
    b.add(lofz([(H - 0.032, 0, 0, T / 2 - 0.05, L / 2 - 0.08, 8), (H - 0.012, 0, 0, T / 2 - 0.06, L / 2 - 0.09, 8),
                (H, 0, 0, T / 2 - 0.075, L / 2 - 0.105, 8)], 40), PAINT)
    for k in (-2, -1, 1, 2):
        b.add(box(0.026, L - 0.32, 0.016, (k * 0.075, 0, H + 0.006), 0.006), PAINT)
    b.add(box(0.11, 0.2, 0.004, (0, -L * 0.33, H + 0.002), 0.002), ALLOY)
    for (dx, du) in ((-0.045, -0.09), (0.045, -0.09), (-0.045, 0.09), (0.045, 0.09)):
        b.add(ball(0.004, (dx, -L * 0.33 + du, H + 0.005), 8), METAL)
    top_text(b, "WEAPON", 0.07, -0.16, 0.0, H + 0.002, along_u=True)
    for u in (L * 0.36, L * 0.41):
        b.add(tz(0.014, 0.012, H - 0.005, H + 0.008, -0.1 if u > L * 0.38 else 0.1, L * 0.33, 14), POLY)
    for x in (-0.1, 0.1):
        b.add(tz(0.009, 0.007, H + 0.008, H + 0.016, x, L * 0.33, 14), GLOW)
    # Nervures moulées sur les flancs (coque et couvercle), cornières, pochoirs, bandes.
    for s in (-1, 1):
        x = s * T / 2
        for u in (-L * 0.25, L * 0.25):
            b.add(box(0.014, 0.03, zs - 0.12, (x + s * 0.004, u, (zs + 0.07) / 2), 0.006), PAINT)
        b.add(box(0.01, L - 0.16, 0.022, (x + s * 0.003, 0, 0.075), 0.005), PAINT)
        flank_text(b, "WEAPON", 0.085, 0.0, 0.2, x + s * 0.002, s)
        flank_text(b, "LOT 07-4412  CAL. MIXED", 0.022, 0.0, 0.12, x + s * 0.002, s)
        for u in (-L * 0.42, L * 0.42):
            b.add(box(0.003, 0.05, 0.11, (x + s * 0.0035, u, 0.2), 0.001), MARKING)
        for u in (-L * 0.3, L * 0.3):
            _latch(b, x + s * 0.01, u, zs, s)
        for su in (-1, 1):
            _corner_bracket(b, x + s * 0.004, su * (L / 2 + 0.004), H - 0.025, s, su, 1, 0.08)
            _corner_bracket(b, x + s * 0.004, su * (L / 2 + 0.004), 0.045, s, su, -1, 0.08)
    # Bouts : poignées pendantes, plaque de données.
    for su in (-1, 1):
        _drop_handle(b, su * (L / 2 + 0.003), zs - 0.04, su, 0.2)
        b.add(box(0.2, 0.003, 0.07, (0, su * (L / 2 + 0.0015), 0.13), 0.001), ALLOY)
    g.build(mats)


def crate_armor():
    """Caisse d'armure : caisse cubique durcie rouge, butoirs aux coins, écusson de bouclier sur le couvercle."""
    clear()
    mats = materials(RED, DARK)
    g = Gun("crate_armor")
    b = g.body
    L, T, H = 0.78, 0.78, 0.62
    zs = 0.42
    _shell(b, L, T, 0.03, zs - 0.006, 0.04, PAINT, 8)
    _shell(b, L + 0.012, T + 0.012, zs - 0.014, zs + 0.006, 0.04, RUBBER, 9)
    _shell(b, L + 0.02, T + 0.02, zs + 0.004, H - 0.04, 0.045, PAINT, 8)
    # Nervures moulées verticales sur les quatre faces, bandeaux horizontaux.
    for s in (-1, 1):
        for k in (-2, -1, 1, 2):
            b.add(box(0.016, 0.034, zs - 0.13, (s * T / 2 + s * 0.006, k * 0.13, (zs + 0.08) / 2), 0.007), PAINT)
            b.add(box(0.034, 0.016, zs - 0.13, (k * 0.13, s * L / 2 + s * 0.006, (zs + 0.08) / 2), 0.007), PAINT)
        b.add(box(0.012, L - 0.12, 0.03, (s * T / 2 + s * 0.004, 0, 0.07), 0.006), ACCENT)
        b.add(box(T - 0.12, 0.012, 0.03, (0, s * L / 2 + s * 0.004, 0.07), 0.006), ACCENT)
    # Pochoirs « ARMOR » sur les quatre faces (entre les nervures centrales), poignées encastrées sur deux faces.
    for s in (-1, 1):
        flank_text(b, "ARMOR", 0.075, 0.0, zs - 0.11, s * (T / 2 + 0.015), s)
        text(b, "ARMOR", 0.075, lambda a, bb, c, s=s: (-s * a, s * (L / 2 + 0.015 + c + 0.002), zs - 0.11 + bb - 0.026))
        # Poignée encastrée : cuvette sombre et barre.
        b.add(box(0.012, 0.2, 0.06, (s * (T / 2 + 0.002), 0, zs + 0.09), 0.01), ACCENT)
        b.add(tube(0.011, 0.011, -0.085, 0.085, s * (T / 2 + 0.012), zs + 0.085, 12), METAL)
        _latch(b, s * (T / 2 + 0.012), -0.2, zs, s, 0.06)
        _latch(b, s * (T / 2 + 0.012), 0.2, zs, s, 0.06)
    # Butoirs caoutchouc aux huit coins.
    for sx in (-1, 1):
        for su in (-1, 1):
            for (z, h) in ((0.0, 0.11), (H - 0.13, 0.13)):
                b.add(lofz([(z, sx * (T / 2 - 0.015), su * (L / 2 - 0.015), 0.054, 0.054, 3.5),
                            (z + 0.014, sx * (T / 2 - 0.015), su * (L / 2 - 0.015), 0.063, 0.063, 3.5),
                            (z + h - 0.014, sx * (T / 2 - 0.015), su * (L / 2 - 0.015), 0.063, 0.063, 3.5),
                            (z + h, sx * (T / 2 - 0.015), su * (L / 2 - 0.015), 0.054, 0.054, 3.5)], 24), RUBBER)
    # Couvercle : écusson de bouclier en relief (plaque d'alliage, bord sombre, chevrons), pochoir.
    zt = H - 0.04
    b.add(lofz([(zt, 0, 0, T / 2 - 0.02, L / 2 - 0.02, 8), (zt + 0.022, 0, 0, T / 2 - 0.05, L / 2 - 0.05, 8)], 40), PAINT)
    zt += 0.022
    shield = [(-0.15, 0.17), (0.15, 0.17), (0.155, 0.02), (0.11, -0.09), (0.0, -0.17), (-0.11, -0.09), (-0.155, 0.02)]

    def plate(poly, z0, z1, mat, k=1.0, du=0.0):
        bm = bmesh.new()
        lo = [bm.verts.new(P(x * k, u * k + du, z0)) for (x, u) in poly]
        hi = [bm.verts.new(P(x * k, u * k + du, z1)) for (x, u) in poly]
        bm.faces.new(list(reversed(lo)))
        bm.faces.new(hi)
        for i in range(len(poly)):
            j = (i + 1) % len(poly)
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.004, segments=2, affect='EDGES', profile=0.5)
        b.add(bm, mat)

    plate(shield, zt, zt + 0.014, ACCENT, 1.12, 0.03)
    plate(shield, zt + 0.014, zt + 0.03, ALLOY, 1.0, 0.03)
    for k in range(3):
        chev = [(-0.09, 0.08 - k * 0.07), (0.0, 0.03 - k * 0.07), (0.09, 0.08 - k * 0.07), (0.09, 0.05 - k * 0.07),
                (0.0, 0.0 - k * 0.07), (-0.09, 0.05 - k * 0.07)]
        plate(chev, zt + 0.03, zt + 0.04, PAINT, 1.0, 0.04)
    for (dx, du) in ((-0.13, 0.17), (0.13, 0.17), (0.0, -0.13)):
        b.add(ball(0.006, (dx, du + 0.03, zt + 0.032), 8), METAL)
    top_text(b, "ARMOR", 0.055, -0.27, 0.0, zt, along_u=False)
    for x in (-0.3, 0.3):
        b.add(tz(0.012, 0.01, zt, zt + 0.01, x, 0.3, 14), POLY)
        b.add(tz(0.008, 0.006, zt + 0.01, zt + 0.017, x, 0.3, 14), GLOW)
    g.build(mats)


def crate_health():
    """Mallette de soin : coque blanche debout, grandes croix rouges en relief sur les flancs, poignée sur le dessus."""
    clear()
    mats = materials(WHITE, CROSS)
    g = Gun("crate_health")
    b = g.body
    L, T, H = 0.74, 0.34, 0.54      # longueur (u), épaisseur (x), hauteur
    for s in (-1, 1):
        # Deux coques (joint vertical au milieu de l'épaisseur).
        hw = T / 4 - 0.002
        cx = s * (T / 4 + 0.002)
        e = 0.03
        b.add(lofx([(-L / 2, cx, H / 2 + 0.02, hw * 0.85, H / 2 - e, 6), (-L / 2 + e, cx, H / 2 + 0.02, hw, H / 2, 7),
                    (L / 2 - e, cx, H / 2 + 0.02, hw, H / 2, 7), (L / 2, cx, H / 2 + 0.02, hw * 0.85, H / 2 - e, 6)], 40), PAINT)
        x = s * T / 2
        # Bandeau rouge horizontal, cadre en relief, croix rouge en relief sur fond blanc.
        b.add(box(0.008, L - 0.07, 0.05, (x + s * 0.002, 0, 0.1), 0.004), ACCENT)
        for zz in (0.06, H - 0.02):
            b.add(box(0.01, L - 0.12, 0.016, (x + s * 0.003, 0, zz), 0.005), PAINT)
        cz = H * 0.6
        b.add(box(0.012, 0.11, 0.3, (x + s * 0.006, 0, cz), 0.008), ACCENT)
        b.add(box(0.012, 0.3, 0.11, (x + s * 0.006, 0, cz), 0.008), ACCENT)
        flank_text(b, "MEDICAL", 0.034, 0.0, 0.1, x + s * 0.007, s)
        flank_text(b, "FIRST AID  TRAUMA KIT", 0.016, 0.0, 0.205, x + s * 0.002, s)
    # Joint sombre entre les coques, charnières sur un bout, loquets et poignée sur le dessus.
    b.add(box(0.008, L - 0.02, H - 0.02, (0, 0, H / 2 + 0.02), 0.003), RUBBER)
    for k in range(4):
        z = 0.1 + k * (H - 0.12) / 4
        b.add(tz(0.011, 0.011, z, z + (H - 0.12) / 4 - 0.006, 0.0, -L / 2 - 0.006, 12), METAL)
    zt = H + 0.02
    for u in (-L * 0.33, L * 0.33):
        b.add(box(0.07, 0.05, 0.014, (0, u, zt + 0.004), 0.004), POLY)
        b.add(prism([(u - 0.024, zt + 0.01), (u + 0.024, zt + 0.01), (u + 0.02, zt + 0.026), (u - 0.02, zt + 0.022)], -0.025, 0.025, 0.002), ACCENT)
        b.add(tube(0.0045, 0.0045, u - 0.028, u + 0.028, 0.0, zt + 0.019, 8), METAL)
    for uu in (-0.09, 0.09):
        b.add(box(0.04, 0.03, 0.02, (0, uu, zt + 0.01), 0.005), POLY)
        b.add(rod((0, uu, zt + 0.015), (0, uu * 0.85, zt + 0.06), 0.008, 0.007, 10), POLY)
    b.add(lofx([(-0.08, 0, zt + 0.065, 0.014, 0.013, 2.2), (-0.07, 0, zt + 0.065, 0.018, 0.016, 2.2), (0.07, 0, zt + 0.065, 0.018, 0.016, 2.2),
                (0.08, 0, zt + 0.065, 0.014, 0.013, 2.2)], 16), RUBBER)
    for k in range(7):
        b.add(ring(0.0186, 0.014, -0.05 + k * 0.016, -0.046 + k * 0.016, 0, zt + 0.065, 16), RUBBER)
    # Coins caoutchouc (huit) et pieds.
    for sx in (-1, 1):
        for su in (-1, 1):
            for z in (0.0, H - 0.05):
                b.add(lofz([(z + 0.02, sx * (T / 2 - 0.03), su * (L / 2 - 0.03), 0.045, 0.045, 3.5),
                            (z + 0.03, sx * (T / 2 - 0.03), su * (L / 2 - 0.03), 0.05, 0.05, 3.5),
                            (z + 0.08, sx * (T / 2 - 0.03), su * (L / 2 - 0.03), 0.05, 0.05, 3.5),
                            (z + 0.09, sx * (T / 2 - 0.03), su * (L / 2 - 0.03), 0.045, 0.045, 3.5)], 20), RUBBER)
            b.add(tz(0.02, 0.018, 0.0, 0.022, sx * T * 0.3, su * L * 0.36, 14), RUBBER)
    # Croix rouge sur le dessus aussi (lisible de haut) et voyant.
    b.add(box(0.034, 0.1, 0.006, (0, -0.165, zt + 0.003), 0.002), ACCENT)
    b.add(box(0.1, 0.034, 0.006, (0, -0.165, zt + 0.003), 0.002), ACCENT)
    b.add(tz(0.011, 0.009, zt, zt + 0.01, 0.0, 0.165, 14), POLY)
    b.add(tz(0.007, 0.006, zt + 0.01, zt + 0.016, 0.0, 0.165, 14), GLOW)
    g.build(mats)


def make_object(name, out=None):
    globals()[name]()
    if out:
        export(out)
    return tris()
