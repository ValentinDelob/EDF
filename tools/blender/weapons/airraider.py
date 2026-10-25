# Air Raider (EDF 5) : objets tenus (FAMILIES → assets/models/weapons/<nom>.glb) et objets du monde (OBJECTS →
# assets/models/airraid/<nom>.glb). Langage commun : matériel militaire sable et kaki (Paint), pièces sombres (Accent), métal,
# diodes. Tenus : fumigène de marquage, désignateur laser, lance-bombes collantes (canon court, bombe visible), lance-pieux
# Sniper, Splendor à tambour, détecteur, caisses de déploiement (tourelle, robot, poste, abri), pistolets de soutien,
# canon de répression. Monde : tourelles ZE (socle trépied + tête orientable « Head » portant « Muzzle »), lanceur ZE, tireur
# d'élite, blaster ; robot bombe à chenilles, coléoptère sauteur, patrouilleur, Speed Star ; postes de soin / puissance /
# protection ; ballon leurre ; canonnière (quadrimoteur à canons latéraux) et bombardiers KM6, Kamuy, Vesta, Phobos.
import math


def ball(r, c, seg=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def pts(g, muzzle, grip, butt, sup, mag=(0, 0.0, -0.06), vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Objets tenus ─────────────────────────

def smokecan(g):
    """Fumigène de marquage (frappes d'artillerie et bombardiers) : boîte cylindrique, goupille, bande rouge."""
    b = g.body
    b.add(tube(0.032, 0.032, -0.06, 0.08, 0, 0.0, 20), PAINT)
    b.add(ring(0.034, 0.03, 0.0, 0.03, 0, 0.0, 20), GLOW)
    b.add(tube(0.02, 0.02, 0.08, 0.1, 0, 0.0, 16), METAL)
    b.add(box(0.008, 0.06, 0.04, (0.03, 0.06, 0.0), 0.002), METAL)
    pts(g, (0, 0.1, 0), (0, 0.0, 0), (0, -0.06, 0), (0, 0.04, 0))


def designator(g):
    """Désignateur laser (canonnière, missiles, satellites, kit de guidage) : boîtier, double optique, poignée, antenne."""
    b = g.body
    b.add(box(0.07, 0.24, 0.09, (0, 0.06, 0.02), 0.01), PAINT)
    for x in (-0.018, 0.018):
        b.add(tube(0.017, 0.019, 0.18, 0.21, x, 0.03, 16), METAL)
        b.add(tube(0.015, 0.015, 0.209, 0.212, x, 0.03, 16), LENS)
    b.add(tube(0.008, 0.008, 0.18, 0.215, 0, -0.005, 10), GLOW)
    grip = pistol_grip(b, -0.02, -0.025, POLY, 10, 0.09, 0.03, 0.042)
    b.add(tube(0.004, 0.003, 0.0, 0.0, -0.03, 0.07, 6), METAL)
    b.add(box(0.004, 0.004, 0.12, (-0.03, -0.03, 0.12), 0.001), METAL)  # antenne
    pts(g, (0, 0.215, -0.005), grip, (0, -0.06, 0.02), (0, 0.12, -0.03))


def limpet_gun(g, barrel=0.2, r=0.022, drum=False, scope_on=False, chain=False):
    """Lance-bombes collantes : boîtier, canon court large, bombe visible au départ, poignée et crosse courte."""
    b = g.body
    receiver(b, -0.1, 0.12, 0.034, -0.04, 0.04, PAINT)
    b.add(tube(r, r, 0.12, 0.12 + barrel, 0, 0.0, 20), METAL)
    b.add(ring(r * 1.3, r * 0.95, 0.12 + barrel - 0.02, 0.12 + barrel, 0, 0.0, 20), ACCENT)
    b.add(ball(r * 0.85, (0, 0.12 + barrel - 0.005, 0.0)), GLOW)
    grip = pistol_grip(b, -0.03, -0.04, POLY, 14, 0.1, 0.03, 0.044)
    if drum: b.add(tube_x(0.05, 0.06, (0, 0.04, -0.07), 22), ACCENT)
    else: straight_mag(b, 0.05, -0.04, 0.09, 0.03, 0.05, 4, ACCENT)
    if chain:
        for k in range(3):
            b.add(ring(r * 1.25, r * 0.95, 0.16 + k * 0.05, 0.175 + k * 0.05, 0, 0.0, 18), ALLOY)
    stock_fixed(b, -0.1, -0.3, -0.01, 0.04, 0.08, 0.036, POLY)
    rail(b, -0.06, 0.1, 0.045)
    if scope_on: scope(b, 0.0, 0.045, 0.26, 0.015, 0.024)
    sup = vertical_grip(b, 0.16, -r - 0.004, 0.07)
    pts(g, (0, 0.12 + barrel, 0), grip, (0, -0.3, 0.0), sup, (0, 0.05, -0.08), True)


def limpetgun(g): limpet_gun(g)
def limpetsniper(g): limpet_gun(g, 0.38, 0.016, scope_on=True)
def splendor(g): limpet_gun(g, 0.18, 0.03, drum=True)
def limpetchain(g): limpet_gun(g, 0.26, 0.02, drum=True, chain=True)


def detector(g):
    limpet_gun(g, 0.16, 0.026)
    b = g.body
    b.add(box(0.05, 0.06, 0.04, (0, 0.0, 0.07), 0.006), ACCENT)            # module de détection
    b.add(box(0.03, 0.002, 0.02, (0, 0.031, 0.07), 0.001), GLOW)


def case(g, color_strip=GLOW):
    """Caisse de déploiement portée à une main (tourelle, robot, poste, abri, leurre)."""
    b = g.body
    b.add(box(0.12, 0.34, 0.24, (0.0, 0.02, -0.16), 0.014), PAINT)
    b.add(box(0.13, 0.36, 0.02, (0.0, 0.02, -0.03), 0.004), ACCENT)
    b.add(box(0.03, 0.1, 0.03, (0.0, 0.02, 0.0), 0.006), POLY)             # poignée
    b.add(box(0.122, 0.2, 0.012, (0.0, 0.02, -0.12), 0.002, 1), color_strip)
    pts(g, (0, 0.2, -0.16), (0, 0.02, 0.0), (0, -0.15, -0.16), (0, 0.15, -0.16))


def sentrycase(g): case(g)
def robotcase(g): case(g, ACCENT)
def postcase(g): case(g, ENERGY)
def deployer(g): case(g, ENERGY)


def assistgun(g):
    """Pistolet de soutien (soin, renfort, protection, balise) : canon à bulbe, cartouche lumineuse."""
    b = g.body
    receiver(b, -0.08, 0.1, 0.028, -0.035, 0.035, PAINT)
    b.add(tube(0.03, 0.022, 0.1, 0.24, 0, 0.0, 20), ALLOY)
    b.add(ball(0.03, (0, 0.24, 0.0)), ENERGY)
    energy_cell(b, 0.0, 0.05, 0, 0.1, 0.014)
    grip = pistol_grip(b, -0.02, -0.035, POLY, 14, 0.095, 0.03, 0.044)
    pts(g, (0, 0.27, 0), grip, (0, -0.12, 0), (0, 0.14, -0.04))


def suppressgun(g):
    """Canon de répression : gros fusil à pompe à canon évasé."""
    b = g.body
    receiver(b, -0.1, 0.14, 0.036, -0.045, 0.04, PAINT)
    b.add(tube(0.03, 0.05, 0.14, 0.46, 0, 0.0, 22), METAL)
    b.add(ring(0.055, 0.04, 0.44, 0.47, 0, 0.0, 22), ACCENT)
    b.add(tube(0.018, 0.018, 0.14, 0.36, 0, -0.045, 14), POLY)
    grip = pistol_grip(b, -0.03, -0.045, POLY, 14, 0.1, 0.03, 0.044)
    stock_fixed(b, -0.1, -0.32, -0.01, 0.045, 0.085, 0.038, POLY)
    pts(g, (0, 0.47, 0), grip, (0, -0.32, 0), (0, 0.26, -0.05))


FAMILIES = {f: globals()[f] for f in [
    "smokecan", "designator", "limpetgun", "limpetsniper", "splendor", "limpetchain", "detector",
    "sentrycase", "robotcase", "postcase", "deployer", "assistgun", "suppressgun"]}
SAND, OLIVE, DARK, WHITE = (0.55, 0.48, 0.34), (0.26, 0.29, 0.2), (0.1, 0.1, 0.1), (0.8, 0.8, 0.78)
PALETTE = {f: (SAND, DARK) for f in FAMILIES}
PALETTE.update({"smokecan": (OLIVE, DARK), "designator": (OLIVE, DARK), "assistgun": (WHITE, DARK), "postcase": (WHITE, DARK)})


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


# ───────────────────────── Objets du monde ─────────────────────────

def part_obj(part, name, mats, parent, loc=(0, 0, 0)):
    o = part.obj(name, mats, parent)
    o.location = P(-loc[0], loc[1], loc[2])
    return o


def tripod(b, h=0.9):
    for k in range(3):
        a = k * 2 * math.pi / 3
        bm = box(0.05, 0.05, h * 1.1, (0, 0, 0), 0.008)
        b.add(bm, METAL, Matrix.Translation(P(0.28 * math.cos(a), 0.28 * math.sin(a), h * 0.45))
              @ Matrix.Rotation(math.radians(25), 4, Vector((-math.sin(a), -math.cos(a), 0))))
    b.add(tube(0.12, 0.12, -0.12, 0.12, 0, h, 18), ACCENT)


def sentry(name, kind):
    clear()
    mats = materials(SAND, DARK)
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    base = Part()
    tripod(base, 0.9)
    # Socle tournant posé sur le trépied.
    base.add(disc(0.2, 0.06, 0.95), PAINT)
    part_obj(base, "Base", mats, root)
    head = Part()
    # La tête pivote autour de son origine (au-dessus du socle).
    head.add(box(0.36, 0.5, 0.3, (0, 0.05, 0.12), 0.03), PAINT)
    head.add(box(0.37, 0.12, 0.08, (0, -0.16, 0.24), 0.01), ACCENT)
    head.add(box(0.06, 0.02, 0.04, (0.12, 0.3, 0.2), 0.004), GLOW)
    if kind == "zelauncher":
        for x in (-0.11, 0.11):
            for z in (0.06, 0.18):
                head.add(tube(0.045, 0.045, 0.0, 0.42, x, z, 14), METAL)
                head.add(tube(0.035, 0.035, 0.419, 0.422, x, z, 12), GLOW)
        muzzle = (0, 0.44, 0.12)
    elif kind == "zesniper":
        head.add(tube(0.025, 0.02, 0.3, 1.2, 0, 0.14, 14), METAL)
        head.add(tube(0.035, 0.035, 1.15, 1.25, 0, 0.14, 14), METAL)
        head.add(tube(0.03, 0.03, -0.05, 0.25, 0, 0.32, 14), METAL)
        head.add(tube(0.028, 0.028, 0.249, 0.252, 0, 0.32, 14), LENS)
        muzzle = (0, 1.25, 0.14)
    elif kind == "zeblaster":
        head.add(tube(0.06, 0.04, 0.3, 0.8, 0, 0.14, 18), ALLOY)
        for k in range(4):
            head.add(ring(0.07, 0.05, 0.4 + k * 0.1, 0.42 + k * 0.1, 0, 0.14, 18), ENERGY)
        muzzle = (0, 0.82, 0.14)
    else:
        for x in (-0.06, 0.06):
            head.add(tube(0.022, 0.022, 0.3, 0.85, x, 0.12, 14), METAL)
            head.add(tube(0.03, 0.03, 0.78, 0.85, x, 0.12, 14), METAL)
        head.add(box(0.12, 0.2, 0.16, (0.25, 0.0, 0.06), 0.012), ACCENT)  # caisse de bande
        muzzle = (0, 0.86, 0.12)
    ho = part_obj(head, "Head", mats, root, (0, 0, 1.08))
    empty("Muzzle", P(-muzzle[0], muzzle[1], muzzle[2]), ho)
    return root



def disc(r, h, z):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=24, radius1=r, radius2=r, depth=h)
    bmesh.ops.translate(bm, vec=P(0, 0, z), verts=bm.verts)
    return bm


def robot(name):
    clear()
    mats = materials((0.9, 0.75, 0.15) if name == "speedstar" else SAND, DARK)
    g = Gun(name)
    b = g.body
    if name == "beetle":
        b.add(ball(0.4, (0, 0, 0.45), 18), PAINT)                           # carapace
        b.add(box(0.7, 0.5, 0.12, (0, 0, 0.32), 0.04), ACCENT)
        for s in (-1, 1):
            for k in (-1, 0, 1):
                bm = box(0.06, 0.06, 0.5, (0, 0, 0), 0.01)
                b.add(bm, METAL, Matrix.Translation(P(s * 0.4, k * 0.18, 0.2)) @ Matrix.Rotation(math.radians(-s * 35), 4, 'Y'))
        b.add(box(0.12, 0.04, 0.05, (0, 0.38, 0.5), 0.01), GLOW)
        b.add(prism([(0.3, 0.55), (0.55, 0.62), (0.52, 0.66), (0.28, 0.6)], -0.04, 0.04, 0.005), METAL)  # corne
    elif name == "patroller":
        b.add(box(0.5, 0.7, 0.28, (0, 0, 0.32), 0.04), PAINT)
        for s in (-1, 1):
            b.add(box(0.12, 0.8, 0.22, (s * 0.31, 0, 0.14), 0.05), RUBBER)  # chenilles
        b.add(tube(0.12, 0.12, -0.05, 0.12, 0, 0.6, 16), ACCENT)
        b.add(tube(0.025, 0.025, 0.1, 0.5, 0, 0.6, 12), METAL)
        b.add(box(0.1, 0.04, 0.04, (0, 0.14, 0.72), 0.01), GLOW)
    elif name == "speedstar":
        b.add(ball(0.32, (0, 0, 0.35), 18), PAINT)
        for k in range(5):
            a = k * 2 * math.pi / 5
            b.add(box(0.08, 0.08, 0.3, (0, 0, 0), 0.02), ACCENT, Matrix.Translation(P(0.34 * math.cos(a), 0, 0.35 + 0.34 * math.sin(a)))
                  @ Matrix.Rotation(a + math.pi / 2, 4, 'Y'))
        b.add(ring(0.36, 0.3, -0.03, 0.03, 0, 0.35, 24), GLOW)
    else:  # robot bombe à chenilles
        b.add(box(0.46, 0.6, 0.26, (0, 0, 0.3), 0.04), PAINT)
        for s in (-1, 1):
            b.add(box(0.1, 0.7, 0.2, (s * 0.28, 0, 0.13), 0.05), RUBBER)
        b.add(tube(0.14, 0.14, -0.15, 0.15, 0, 0.5, 18), ACCENT)            # charge
        b.add(box(0.2, 0.02, 0.05, (0, 0.31, 0.38), 0.005), GLOW)
        b.add(box(0.36, 0.02, 0.04, (0, 0.0, 0.66), 0.005), GLOW)
    g.build(mats)
    return g


def post(name):
    clear()
    color = {"lifevendor": (0.2, 0.75, 0.35), "powerpost": (0.85, 0.35, 0.1), "guardpost": (0.25, 0.45, 0.85)}[name]
    mats = materials(WHITE, color)
    g = Gun(name)
    b = g.body
    b.add(box(0.5, 0.5, 0.12, (0, 0, 0.06), 0.03), ACCENT)                 # socle
    b.add(box(0.22, 0.22, 1.0, (0, 0, 0.6), 0.04), PAINT)                  # mât
    b.add(box(0.24, 0.24, 0.08, (0, 0, 1.1), 0.02), ACCENT)
    b.add(ball(0.12, (0, 0, 1.26), 16), ENERGY)
    for k in range(4):
        a = k * math.pi / 2
        b.add(box(0.02, 0.06, 0.6, (0.115 * math.cos(a), 0.115 * math.sin(a), 0.6), 0.004), ENERGY)
    g.build(mats)
    return g


def decoy():
    clear()
    mats = materials((0.2, 0.65, 0.3), DARK)
    g = Gun("decoy")
    b = g.body
    b.add(box(0.5, 0.5, 0.3, (0, 0, 0.15), 0.04), ACCENT)                  # treuil
    b.add(box(0.015, 0.015, 4.6, (0, 0, 2.6), 0.002), METAL)               # câble
    b.add(ball(1.4, (0, 0, 6.2), 22), PAINT)                               # ballon
    b.add(ring(1.42, 1.3, -0.08, 0.08, 0, 6.2, 32), ACCENT)
    for k in range(4):
        a = k * math.pi / 2
        b.add(box(0.06, 0.06, 0.12, (1.38 * math.cos(a), 1.38 * math.sin(a), 6.2), 0.01), GLOW)
    g.build(mats)
    return g


def aircraft(name):
    """Avions à l'échelle réelle, nez vers +u (avant), ailes le long de x."""
    clear()
    mats = materials((0.36, 0.39, 0.38), DARK)
    g = Gun(name)
    b = g.body
    L, W, R = {"gunship": (30, 40, 2.2), "km6": (16, 14, 1.1), "kamuy": (18, 18, 1.3), "vesta": (26, 32, 1.8), "phobos": (34, 46, 2.2)}[name]
    b.add(loft([(-L / 2, R * 0.4, R * 0.5, 0, 2.2), (-L * 0.3, R, R, 0, 2.4), (L * 0.3, R, R, 0, 2.4), (L / 2, R * 0.25, R * 0.3, -R * 0.2, 2.0)], 20), PAINT)
    b.add(box(W, L * 0.18, R * 0.25, (0, L * 0.05, R * (0.4 if name in ("gunship", "phobos", "vesta") else -0.3)), 0.1), PAINT)   # ailes
    b.add(box(W * 0.38, L * 0.1, R * 0.18, (0, -L * 0.42, R * 0.5), 0.06), PAINT)                    # empennage
    b.add(box(R * 0.15, L * 0.12, R * 1.6, (0, -L * 0.42, R * 1.3), 0.06), PAINT)
    b.add(box(R * 0.7, L * 0.08, R * 0.3, (0, L * 0.4, R * 0.55), 0.05), GLASS)                       # verrière
    n_eng = 4 if name in ("gunship", "phobos", "vesta") else 2 if name == "kamuy" else 1
    for k in range(n_eng):
        x = (k - (n_eng - 1) / 2) * (W * 0.45 / max(1, n_eng - 1)) if n_eng > 1 else 0
        if n_eng == 1: b.add(tube(R * 0.45, R * 0.4, -L / 2 - 0.6, -L / 2 + 1.5, 0, 0, 16), METAL); continue
        b.add(tube(R * 0.35, R * 0.3, L * 0.02, L * 0.22, x, R * 0.2, 16), ACCENT)
        b.add(tube(R * 0.26, R * 0.26, L * 0.219, L * 0.222, x, R * 0.2, 16), METAL)
    if name == "gunship":
        for k, u in enumerate((L * 0.15, -L * 0.05, -L * 0.2)):
            b.add(tube(0.18 - k * 0.04, 0.18 - k * 0.04, 0, 2.2, R * 0.9, 0, 12), METAL,
                  Matrix.Translation(P(R * 0.9, u, 0)) @ Matrix.Rotation(math.radians(-90), 4, 'Z') @ Matrix.Translation(P(-R * 0.9, -u, 0)))
    for s in (-1, 1):
        b.add(box(0.15, 0.4, 0.15, (s * W * 0.48, L * 0.05, R * 0.4), 0.02), GLOW)
    g.build(mats)
    return g


OBJECTS = ["zegun", "zelauncher", "zesniper", "zeblaster", "robotbomb", "beetle", "patroller", "speedstar",
           "lifevendor", "powerpost", "guardpost", "decoy", "gunship", "km6", "kamuy", "vesta", "phobos"]


def make_object(name, out=None):
    if name.startswith("ze"): sentry(name, name)
    elif name in ("robotbomb", "beetle", "patroller", "speedstar"): robot(name)
    elif name in ("lifevendor", "powerpost", "guardpost"): post(name)
    elif name == "decoy": decoy()
    else: aircraft(name)
    if out:
        export(out)
    return tris()
