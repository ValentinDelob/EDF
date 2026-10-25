# Lance-roquettes et lance-missiles du Ranger (EDF 5), portés à l'épaule : tube sur l'axe (z = 0), poignée pistolet et
# poignée avant dessous, appui d'épaule (repère Butt). Une fonction par famille de modèle.
import math


def launch_tube(b, r, u0, u1, mat=PAINT, rings=(), flare=1.25):
    """Tube de lancement : corps, bagues, venturi arrière évasé, bouche renforcée."""
    b.add(tube(r, r, u0 + 0.06, u1 - 0.03, 0, 0, 24), mat)
    b.add(tube(r * flare, r, u0, u0 + 0.06, 0, 0, 24, False), METAL)               # venturi
    b.add(ring(r * flare, r * flare * 0.8, u0 - 0.002, u0 + 0.004, 0, 0, 24), METAL)
    b.add(ring(r * 1.1, r * 0.82, u1 - 0.03, u1, 0, 0, 24), METAL)                 # bouche
    for uu in rings:
        b.add(ring(r * 1.08, r * 0.95, uu - 0.012, uu + 0.012, 0, 0, 24), METAL)


def shoulder(b, u, r, mat=POLY):
    """Appui d'épaule sous le tube."""
    b.add(prism([(u - 0.08, -r * 0.7), (u + 0.08, -r * 0.7), (u + 0.06, -r - 0.05), (u - 0.07, -r - 0.05)], -0.03, 0.03, 0.006), mat)
    return (0, u, -r - 0.03)


def launcher_grips(b, r, grip_u=-0.08, front_u=0.24):
    grip = pistol_grip(b, grip_u, -r - 0.01, POLY, 12, 0.1, 0.03, 0.044)
    b.add(box(0.03, 0.12, 0.016, (0, grip_u + 0.02, -r - 0.004), 0.004), METAL)
    sup = vertical_grip(b, front_u, -r - 0.004, 0.08)
    return grip, sup


def side_sight(b, u, r, big=False):
    """Viseur latéral gauche sur bras de montage (lunette de tir réflexe ou conduite de tir)."""
    x = r + 0.035
    b.add(box(0.03, 0.03, 0.012, (r + 0.012, u, 0.02), 0.003), METAL)
    if big:
        b.add(box(0.05, 0.14, 0.08, (x + 0.012, u, 0.03), 0.008), PAINT)
        b.add(box(0.04, 0.002, 0.05, (x + 0.012, u - 0.071, 0.035), 0.001, 1), LENS)
        b.add(tube(0.016, 0.016, u + 0.06, u + 0.09, x + 0.012, 0.03, 16), METAL)
        b.add(tube(0.014, 0.014, u + 0.089, u + 0.091, x + 0.012, 0.03, 16), LENS)
    else:
        b.add(tube(0.015, 0.015, u - 0.07, u + 0.07, x, 0.025, 18), METAL)
        b.add(tube(0.014, 0.014, u + 0.069, u + 0.071, x, 0.025, 18), LENS)


def grant(g):
    """Type Carl Gustaf : tube d'acier, deux poignées, viseur latéral, appui d'épaule, venturi évasé."""
    b = g.body
    r = 0.045
    launch_tube(b, r, -0.48, 0.6, PAINT, (-0.2, 0.05, 0.35))
    butt = shoulder(b, -0.2, r)
    grip, sup = launcher_grips(b, r, -0.06, 0.26)
    side_sight(b, 0.05, r)
    b.add(tube_x(0.008, 0.02, (r + 0.01, -0.06, 0.0), 10), METAL)                   # levier de culasse
    laser_module(g.opt("laser"), 0.3, 0.02, -(r + 0.02))
    g.point("Muzzle", (0, 0.6, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.48, 0))


def leopard(g):
    """Lance-roquettes guidé : tube composite, unité de guidage (boîtier optronique) à gauche, désignateur laser."""
    b = g.body
    r = 0.055
    launch_tube(b, r, -0.42, 0.6, PAINT, (-0.3, 0.45), 1.15)
    b.add(loft([(-0.42, r * 1.25, r * 1.25, 0, 2.0), (-0.36, r * 1.05, r * 1.05, 0, 2.0)], 24, False), ACCENT)  # capot arrière
    butt = shoulder(b, -0.18, r)
    grip, sup = launcher_grips(b, r, -0.05, 0.22)
    side_sight(b, 0.0, r, True)
    b.add(box(0.012, 0.05, 0.03, (-(r + 0.012), 0.22, 0.01), 0.003), POLY)         # désignateur
    b.add(tube(0.006, 0.006, 0.247, 0.25, -(r + 0.012), 0.016, 10), GLOW)
    g.point("Muzzle", (0, 0.6, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.42, 0))


def goliath(g):
    """Gros calibre : tube épais à manchon, bouche massive, poignées, grande conduite de tir, appui d'épaule rembourré."""
    b = g.body
    r = 0.068
    launch_tube(b, r, -0.5, 0.62, PAINT, (-0.25, 0.1, 0.42), 1.2)
    b.add(tube(r * 1.12, r * 1.12, -0.1, 0.25, 0, 0, 24), ACCENT)
    butt = shoulder(b, -0.22, r)
    grip, sup = launcher_grips(b, r, -0.07, 0.3)
    side_sight(b, 0.05, r, True)
    g.point("Muzzle", (0, 0.62, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.5, 0))


def multi_box(b, cols, rows, cell, u0, u1, mat=PAINT):
    """Caisson multi-tubes : boîte + alvéoles visibles à l'avant."""
    w, h = cols * cell + 0.012, rows * cell + 0.012
    b.add(box(w, u1 - u0, h, (0, (u0 + u1) / 2, 0), 0.008), mat)
    for c in range(cols):
        for rr in range(rows):
            x = (c - (cols - 1) / 2) * cell
            z = (rr - (rows - 1) / 2) * cell
            b.add(tube(cell * 0.38, cell * 0.38, u1 - 0.004, u1 + 0.002, x, z, 14), POLY)
            b.add(tube(cell * 0.2, cell * 0.2, u1 - 0.006, u1 + 0.003, x, z, 10), METAL)
    return w, h


def cascade(g):
    """Caisson de 12 roquettes (4 × 3), poignée de transport, conduite de tir, appui d'épaule."""
    b = g.body
    w, h = multi_box(b, 4, 3, 0.042, -0.3, 0.5)
    b.add(loft([(-0.1, 0.008, 0.012, h / 2 + 0.04, 3.0), (0.3, 0.008, 0.012, h / 2 + 0.04, 3.0)], 12), METAL)
    for uu in (-0.1, 0.3):
        b.add(box(0.016, 0.02, 0.04, (0, uu, h / 2 + 0.02), 0.003), METAL)
    for uu in (-0.2, 0.1, 0.4):
        b.add(box(w + 0.006, 0.02, h + 0.006, (0, uu, 0), 0.003), METAL)
    butt = shoulder(b, -0.2, h / 2)
    grip, sup = launcher_grips(b, h / 2, -0.06, 0.24)
    side_sight(b, 0.05, w / 2, True)
    g.point("Muzzle", (0, 0.5, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.3, 0))


def hornet(g):
    """Quatre tubes groupés (2 × 2) cerclés, culasse commune, viseur réflexe."""
    b = g.body
    r = 0.032
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.add(tube(r, r, -0.38, 0.52, sx * r * 1.05, sz * r * 1.05, 18), PAINT)
            b.add(tube(r * 0.75, r * 0.75, 0.519, 0.522, sx * r * 1.05, sz * r * 1.05, 14), POLY)
    for uu in (-0.3, 0.0, 0.3, 0.48):
        b.add(box(r * 4.6, 0.03, r * 4.6, (0, uu, 0), 0.006), METAL)
    b.add(box(r * 4.4, 0.08, r * 4.4, (0, -0.4, 0), 0.01), ACCENT)
    butt = shoulder(b, -0.2, r * 2.1)
    grip, sup = launcher_grips(b, r * 2.1, -0.06, 0.22)
    red_dot(b, 0.0, r * 2.3)
    g.point("Muzzle", (0, 0.52, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.4, 0))


def volcano(g):
    """Six tubes en barillet tournant autour d'un axe central, carter avant et arrière, poignée de transport."""
    b = g.body
    r = 0.026
    for k in range(6):
        a = 2 * math.pi * k / 6
        x, z = math.cos(a) * r * 2.05, math.sin(a) * r * 2.05
        b.add(tube(r, r, -0.32, 0.5, x, z, 16), PAINT)
        b.add(tube(r * 0.72, r * 0.72, 0.499, 0.503, x, z, 12), POLY)
    b.add(tube(r * 0.9, r * 0.9, -0.34, 0.52, 0, 0, 16), METAL)
    for uu in (-0.32, 0.12, 0.48):
        b.add(ring(r * 3.4, r * 0.8, uu - 0.02, uu + 0.02, 0, 0, 28), METAL)
    b.add(loft([(-0.2, 0.008, 0.012, r * 3.4 + 0.035, 3.0), (0.2, 0.008, 0.012, r * 3.4 + 0.035, 3.0)], 12), METAL)
    for uu in (-0.2, 0.2):
        b.add(box(0.016, 0.02, 0.04, (0, uu, r * 3.4 + 0.015), 0.003), METAL)
    butt = shoulder(b, -0.2, r * 3.2)
    grip, sup = launcher_grips(b, r * 3.2, -0.06, 0.25)
    side_sight(b, 0.0, r * 3.3)
    g.point("Muzzle", (0, 0.5, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.32, 0))


def ibis(g):
    """Roquette guidée de haute précision : tube effilé à ailettes, autodirecteur visible à l'avant, conduite de tir."""
    b = g.body
    r = 0.05
    launch_tube(b, r, -0.45, 0.58, ACCENT, (-0.2, 0.3), 1.1)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(box(0.004, 0.12, 0.04, (math.cos(a) * (r + 0.015), -0.36, math.sin(a) * (r + 0.015)), 0.001), METAL)
    b.add(tube(r * 0.7, r * 0.5, 0.56, 0.6, 0, 0, 20), LENS)
    butt = shoulder(b, -0.18, r)
    grip, sup = launcher_grips(b, r, -0.05, 0.22)
    side_sight(b, 0.0, r, True)
    g.point("Muzzle", (0, 0.6, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.45, 0))


# ───────────────────────── Lance-missiles ─────────────────────────

def seeker_box(b, u, w):
    """Boîtier de verrouillage (écran et capteurs) sur le dessus."""
    b.add(box(0.07, 0.11, 0.05, (0, u, w + 0.035), 0.008), METAL)
    b.add(box(0.06, 0.002, 0.04, (0, u - 0.056, w + 0.037), 0.001), ENERGY)
    for s in (-1, 1):
        b.add(tube(0.008, 0.008, u + 0.05, u + 0.07, s * 0.02, w + 0.035, 12), LENS)


def emerald(g):
    """Caisson de quatre missiles (2 × 2) à capots, autodirecteur sur le dessus, poignées, appui d'épaule."""
    b = g.body
    w, h = multi_box(b, 2, 2, 0.07, -0.34, 0.52)
    seeker_box(b, 0.0, h / 2)
    butt = shoulder(b, -0.2, h / 2)
    grip, sup = launcher_grips(b, h / 2, -0.06, 0.24)
    g.point("Muzzle", (0, 0.54, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.34, 0))


def tortoise(g):
    """Air Tortoise : très gros tube à coiffe bombée (missile lent à trajectoire haute), anneaux de renfort, viseur."""
    b = g.body
    r = 0.085
    launch_tube(b, r, -0.48, 0.55, PAINT, (-0.25, 0.1, 0.4), 1.12)
    b.add(loft([(0.55, r * 0.95, r * 0.95, 0, 2.0), (0.6, r * 0.8, r * 0.8, 0, 2.0), (0.63, r * 0.45, r * 0.45, 0, 2.0), (0.64, r * 0.05, r * 0.05, 0, 2.0)], 24), ACCENT)
    butt = shoulder(b, -0.22, r)
    grip, sup = launcher_grips(b, r, -0.07, 0.25)
    seeker_box(b, 0.0, r)
    g.point("Muzzle", (0, 0.64, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.48, 0))


def mlra(g):
    """Nacelle multi-alvéoles (4 × 4 petits missiles), longeron, autodirecteur, poignées."""
    b = g.body
    w, h = multi_box(b, 4, 4, 0.034, -0.24, 0.42, PAINT)
    b.add(box(w * 0.5, 0.2, h * 0.3, (0, -0.34, -h * 0.2), 0.008), ACCENT)
    seeker_box(b, 0.05, h / 2)
    butt = shoulder(b, -0.3, h / 2)
    grip, sup = launcher_grips(b, h / 2, -0.08, 0.22)
    g.point("Muzzle", (0, 0.42, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.24, 0))


def fork(g):
    """FORK : tube unique à bouche en corolle (missile qui se divise en sous-missiles), ailettes de stabilisation."""
    b = g.body
    r = 0.06
    launch_tube(b, r, -0.45, 0.45, PAINT, (-0.2, 0.15), 1.15)
    b.add(tube(r, r * 1.5, 0.45, 0.58, 0, 0, 24, False), ACCENT)
    b.add(ring(r * 1.55, r * 1.3, 0.575, 0.585, 0, 0, 24), METAL)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(box(0.004, 0.12, 0.02, (math.cos(a) * r * 1.3, 0.52, math.sin(a) * r * 1.3), 0.001), METAL)
    butt = shoulder(b, -0.2, r)
    grip, sup = launcher_grips(b, r, -0.06, 0.24)
    seeker_box(b, -0.05, r)
    g.point("Muzzle", (0, 0.58, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.45, 0))


def prominence(g):
    """Prominence : très long tube (missile balistique lourd), double conduite de tir, bipied avant replié, bouche ogivale."""
    b = g.body
    r = 0.07
    launch_tube(b, r, -0.6, 0.75, PAINT, (-0.35, -0.05, 0.3, 0.6), 1.1)
    b.add(loft([(0.75, r * 0.95, r * 0.95, 0, 2.0), (0.8, r * 0.6, r * 0.6, 0, 2.0), (0.83, r * 0.15, r * 0.15, 0, 2.0)], 24), ACCENT)
    butt = shoulder(b, -0.25, r)
    grip, sup = launcher_grips(b, r, -0.08, 0.3)
    side_sight(b, 0.05, r, True)
    seeker_box(b, 0.25, r)
    bipod(b, 0.55, -r - 0.004, True)
    g.point("Muzzle", (0, 0.83, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.6, 0))


def falcon(g):
    """MS Falcon (contenu téléchargeable) : deux tubes profilés jumelés, carénage, autodirecteur lumineux."""
    b = g.body
    r = 0.04
    for s in (-1, 1):
        b.add(tube(r, r, -0.36, 0.5, s * (r + 0.006), 0, 20), PAINT)
        b.add(tube(r * 0.8, r * 0.8, 0.499, 0.503, s * (r + 0.006), 0, 16), ENERGY)
    b.add(loft([(-0.38, r * 2.3, r * 1.3, 0, 3.0), (-0.2, r * 2.4, r * 1.35, 0, 3.0), (0.3, r * 2.4, r * 1.35, 0, 3.0), (0.48, r * 2.2, r * 1.2, 0, 3.0)], 26), ACCENT)
    seeker_box(b, 0.0, r * 1.35)
    butt = shoulder(b, -0.2, r * 1.3)
    grip, sup = launcher_grips(b, r * 1.3, -0.06, 0.24)
    g.point("Muzzle", (0, 0.5, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.36, 0))


PALETTE = {
    "grant": ((0.16, 0.18, 0.13), (0.05, 0.05, 0.05)), "leopard": ((0.32, 0.28, 0.2), (0.12, 0.12, 0.11)),
    "goliath": ((0.12, 0.14, 0.11), (0.25, 0.22, 0.15)), "cascade": ((0.15, 0.17, 0.13), (0.05, 0.05, 0.05)),
    "hornet": ((0.2, 0.2, 0.18), (0.45, 0.32, 0.08)), "volcano": ((0.14, 0.15, 0.16), (0.05, 0.05, 0.05)),
    "ibis": ((0.1, 0.1, 0.1), (0.55, 0.57, 0.6)), "emerald": ((0.08, 0.2, 0.12), (0.5, 0.52, 0.5)),
    "tortoise": ((0.18, 0.2, 0.15), (0.6, 0.55, 0.4)), "mlra": ((0.14, 0.15, 0.13), (0.3, 0.27, 0.2)),
    "fork": ((0.2, 0.18, 0.15), (0.55, 0.2, 0.1)), "prominence": ((0.13, 0.14, 0.12), (0.7, 0.3, 0.08)),
    "falcon": ((0.7, 0.72, 0.75), (0.15, 0.17, 0.2)),
}

FAMILIES = {f: globals()[f] for f in PALETTE}


def make(family, out=None):
    clear()
    paint, accent = PALETTE.get(family, (None, None))
    mats = materials(paint, accent)
    g = Gun(family)
    FAMILIES[family](g)
    g.build(mats)
    if out:
        export(out)
    return tris()
