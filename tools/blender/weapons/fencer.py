# Armes du Fencer (EDF 5), une fonction par famille de modèle. Langage commun : armes lourdes montées sur l'avant-bras de
# l'exosquelette — un fourreau d'avant-bras (Accent, gaine sombre) avec poignée intérieure (repère Grip), corps massif
# (Paint, gris acier bleuté), pièces mécaniques (Metal / Alloy), bandes d'alerte et diodes (Glow), cellules (Energy).
# Mêlée (prise à une main) : marteaux vibrants à tête large, Vulcan Hammer à tête explosive, lames d'énergie sur bras,
# Force Ax, rouleau dentelé, haken (griffe en croissant), lances perforantes à vérins (Blasthole), lances jumelles, Flashing
# Spear à bobines, marteau-piqueur, Spine Driver (lanceur de pieu). Boucliers (sur l'avant-bras gauche, face vers l'avant,
# origine à la sangle) : Deflection, Ion Mirror (hexagonal, miroir), Tower (très haut), Reflector (rond, réfléchissant).
# Canons automatiques (à la hanche) : gatling six canons, FGS à canons évasés, Galleon à long canon, Dexter à tambour,
# Flame Revolver, Disruptor. Artillerie : canon à main, canon lourd Gallic, Powerdyne à rails, canon à mitraille, mortiers
# léger / lourd / à dispersion, Crusher. Lance-missiles (à l'épaule) : caissons à cellules, Leviathan, Arcane à six tubes.
import math


def ball(r, c, seg=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def sleeve(b, u0=-0.32, u1=0.08, z=-0.02, w=0.085, h=0.1):
    """Fourreau d'avant-bras : gaine ouverte à l'arrière, poignée intérieure transversale. Renvoie (prise, crosse)."""
    b.add(loft([(u0, w * 0.92, h * 0.88, z, 3.2), (u0 + 0.05, w, h, z, 3.4), (u1 - 0.04, w, h, z, 3.4), (u1, w * 0.86, h * 0.84, z, 3.0)], 24), ACCENT)
    b.add(ring(w * 1.02, w * 0.9, u0 - 0.005, u0 + 0.02, 0, z, 24), METAL)
    for uu in (u0 + 0.1, u0 + 0.22):                                         # sangles
        b.add(box(w * 2.08, 0.025, h * 2.06, (0, uu, z), 0.006), RUBBER)
    b.add(box(0.012, 0.02, h * 1.4, (0, u1 - 0.07, z), 0.004), METAL)         # poignée intérieure
    b.add(box(w * 1.6, 0.012, 0.012, (0, u0 + 0.04, z + h * 0.9), 0.002, 1), GLOW)
    return (0, u1 - 0.07, z), (0, u0, z)


def hazard(b, u, z, x=0.0, w=0.06, n=4):
    """Bande d'alerte (chevrons Accent / Paint)."""
    for k in range(n):
        b.add(prism([(u + k * 0.02, z), (u + k * 0.02 + 0.01, z), (u + k * 0.02 + 0.016, z + 0.008), (u + k * 0.02 + 0.006, z + 0.008)],
                    x - w / 2, x + w / 2, 0.0), GLOW if k % 2 == 0 else METAL)


def piston(b, u0, u1, x, z, r=0.014):
    b.add(tube(r * 1.5, r * 1.5, u0, (u0 + u1) / 2, x, z, 14), METAL)
    b.add(tube(r, r, (u0 + u1) / 2, u1, x, z, 14), ALLOY)


def pts(g, muzzle, grip, butt, sup, mag=(0, 0.0, -0.1), vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Frappe ─────────────────────────

def hammer(g, vulcan=False):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.07, 0.36, 0.08, (0, 0.24, 0.0), 0.012), PAINT)               # bras
    piston(b, 0.1, 0.4, 0.05, 0.05)
    piston(b, 0.1, 0.4, -0.05, 0.05)
    if vulcan:
        # Tête explosive : tambour à charges, tuyères.
        b.add(tube(0.13, 0.13, 0.42, 0.66, 0, 0.0, 24), PAINT)
        for k in range(6):
            a = k * math.pi / 3
            b.add(tube(0.03, 0.026, 0.62, 0.7, 0.085 * math.cos(a), 0.085 * math.sin(a), 12), METAL)
            b.add(tube(0.02, 0.02, 0.699, 0.702, 0.085 * math.cos(a), 0.085 * math.sin(a), 12), GLOW)
        b.add(ring(0.14, 0.12, 0.46, 0.5, 0, 0, 24), ACCENT)
        b.add(ring(0.14, 0.12, 0.58, 0.62, 0, 0, 24), ACCENT)
    else:
        # Tête vibrante : bloc haut, ailettes de dissipation, plaque de frappe lumineuse.
        b.add(box(0.2, 0.22, 0.4, (0, 0.54, 0.0), 0.02), PAINT)
        b.add(box(0.21, 0.04, 0.42, (0, 0.66, 0.0), 0.01), METAL)
        b.add(box(0.16, 0.006, 0.32, (0, 0.682, 0.0), 0.002, 1), ENERGY)
        for k in range(5):
            b.add(box(0.24, 0.012, 0.36, (0, 0.46 + k * 0.035, 0.0), 0.002, 1), METAL)
        b.add(box(0.05, 0.12, 0.08, (0, 0.52, 0.24), 0.01), ACCENT)
    hazard(b, 0.15, 0.04, 0, 0.06)
    pts(g, (0, 0.7, 0), grip, butt, (0, 0.3, -0.06))


def vulcan(g): hammer(g, True)


def blade(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.08, 0.2, 0.1, (0, 0.16, 0.0), 0.012), PAINT)                  # garde
    b.add(box(0.14, 0.04, 0.16, (0, 0.27, 0.0), 0.01), METAL)
    # Lame d'énergie : âme métallique effilée, tranchant lumineux.
    b.add(prism([(0.29, -0.05), (0.95, -0.012), (1.02, 0.02), (0.29, 0.05)], -0.008, 0.008, 0.002), ALLOY)
    b.add(prism([(0.3, 0.05), (1.0, 0.022), (1.02, 0.026), (0.3, 0.058)], -0.004, 0.004, 0.0), ENERGY)
    b.add(prism([(0.3, -0.058), (0.95, -0.016), (0.96, -0.012), (0.3, -0.05)], -0.004, 0.004, 0.0), ENERGY)
    for k in range(3):
        energy_cell(b, 0.05 + k * 0.07, 0.07, 0, 0.05, 0.012)
    pts(g, (0, 1.0, 0), grip, butt, (0, 0.2, -0.06))


def forceax(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.07, 0.42, 0.07, (0, 0.27, 0.0), 0.012), PAINT)               # manche
    # Tête de hache décalée vers le haut, fil d'énergie.
    b.add(prism([(0.4, 0.03), (0.56, 0.03), (0.62, 0.28), (0.5, 0.36), (0.38, 0.24)], -0.025, 0.025, 0.004), PAINT)
    b.add(prism([(0.56, 0.03), (0.63, 0.29), (0.6, 0.3), (0.53, 0.04)], -0.012, 0.012, 0.0), ENERGY)
    b.add(prism([(0.4, -0.02), (0.5, -0.02), (0.46, -0.1)], -0.02, 0.02, 0.003), METAL)   # contre-pointe
    b.add(tube(0.03, 0.03, 0.44, 0.5, 0, 0.06, 14), METAL)
    pts(g, (0, 0.6, 0.2), grip, butt, (0, 0.2, -0.06))


def roller(g):
    b = g.body
    grip, butt = sleeve(b)
    for s in (-1, 1):
        b.add(box(0.03, 0.4, 0.08, (s * 0.12, 0.26, 0.0), 0.008), PAINT)    # bras de fourche
    b.add(box(0.27, 0.08, 0.09, (0, 0.12, 0.0), 0.01), PAINT)
    # Rouleau dentelé transversal.
    b.add(tube_x(0.12, 0.2, (0, 0.5, 0.0), 28), METAL)
    for k in range(12):
        a = k * math.pi / 6
        c = (0, 0.5 + 0.13 * math.cos(a), 0.13 * math.sin(a))
        bm = box(0.2, 0.03, 0.03, (0, 0, 0), 0.003)
        b.add(bm, ALLOY, Matrix.Translation(P(*c)) @ Matrix.Rotation(-a, 4, 'X'))
    b.add(tube_x(0.03, 0.27, (0, 0.5, 0.0), 14), ENERGY)
    pts(g, (0, 0.64, 0), grip, butt, (0, 0.22, -0.06))


def haken(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.09, 0.26, 0.1, (0, 0.2, 0.0), 0.012), PAINT)
    # Griffe en croissant (deux crocs) et émetteur des ondes.
    for s in (-1, 1):
        prof = [(0.3, 0.0), (0.5, s * 0.08), (0.66, s * 0.22), (0.62, s * 0.24), (0.46, s * 0.12), (0.3, s * 0.05)]
        b.add(prism([(u, z) for (u, z) in prof], -0.03, 0.03, 0.003), METAL)
        b.add(prism([(0.5, s * 0.085), (0.66, s * 0.222), (0.64, s * 0.232), (0.49, s * 0.1)], -0.012, 0.012, 0.0), GLOW)
    b.add(tube(0.05, 0.035, 0.32, 0.42, 0, 0.0, 18), ACCENT)
    b.add(tube(0.03, 0.03, 0.42, 0.43, 0, 0.0, 18), ENERGY)
    pts(g, (0, 0.6, 0), grip, butt, (0, 0.2, -0.06))


# ───────────────────────── Perforation ─────────────────────────

def spear(b, x=0.0, l=1.05, r=0.03, glow=False):
    b.add(tube(r * 2.2, r * 2.2, 0.08, 0.36, x, 0.0, 20), PAINT)             # chambre
    b.add(ring(r * 2.4, r * 2.0, 0.12, 0.15, x, 0.0, 20), METAL)
    b.add(ring(r * 2.4, r * 2.0, 0.3, 0.33, x, 0.0, 20), METAL)
    b.add(tube(r, r, 0.36, l - 0.14, x, 0.0, 16), ALLOY)                     # tige
    b.add(tube(r * 1.5, 0.002, l - 0.14, l, x, 0.0, 16), METAL)              # pointe
    piston(b, 0.1, 0.42, x + r * 2.4, 0.0, 0.01)
    piston(b, 0.1, 0.42, x - r * 2.4, 0.0, 0.01)
    if glow:
        for k in range(4):
            b.add(ring(r * 1.6, r * 1.1, 0.45 + k * 0.1, 0.47 + k * 0.1, x, 0.0, 16), ENERGY)


def blasthole(g):
    b = g.body
    grip, butt = sleeve(b)
    spear(b, 0, 1.1, 0.032)
    hazard(b, 0.18, 0.08, 0, 0.05)
    pts(g, (0, 1.1, 0), grip, butt, (0, 0.2, -0.07))


def twinspear(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.26, 0.12, 0.12, (0, 0.12, 0.0), 0.012), PAINT)
    for s in (-1, 1):
        spear(b, s * 0.08, 1.0, 0.024)
    pts(g, (0, 1.0, 0), grip, butt, (0, 0.2, -0.07))


def flashing(g):
    b = g.body
    grip, butt = sleeve(b)
    spear(b, 0, 1.0, 0.026, True)
    for k in range(3):
        energy_cell(b, 0.12 + k * 0.08, 0.08, 0, 0.06, 0.012)
    pts(g, (0, 1.0, 0), grip, butt, (0, 0.2, -0.07))


def jackhammer(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(loft([(0.06, 0.08, 0.08, 0, 2.6), (0.34, 0.085, 0.085, 0, 2.6), (0.44, 0.06, 0.06, 0, 2.2)], 22), PAINT)
    for k in range(7):
        b.add(ring(0.09, 0.07, 0.1 + k * 0.035, 0.115 + k * 0.035, 0, 0, 22), METAL)   # ailettes
    b.add(tube(0.025, 0.025, 0.44, 0.7, 0, 0, 14), ALLOY)
    b.add(tube(0.04, 0.002, 0.7, 0.8, 0, 0, 14), METAL)
    pts(g, (0, 0.8, 0), grip, butt, (0, 0.2, -0.07))


def spinedriver(g):
    b = g.body
    grip, butt = sleeve(b)
    b.add(box(0.13, 0.6, 0.12, (0, 0.34, 0.0), 0.014), PAINT)                # rail de lancement
    b.add(box(0.06, 0.62, 0.02, (0, 0.36, 0.068), 0.004), METAL)
    b.add(tube(0.018, 0.018, 0.1, 0.62, 0, 0.0, 12), ALLOY)                  # pieu
    b.add(tube(0.03, 0.002, 0.62, 0.72, 0, 0.0, 12), METAL)
    for s in (-1, 1):
        b.add(box(0.02, 0.5, 0.03, (s * 0.075, 0.36, -0.02), 0.004), ENERGY)
    pts(g, (0, 0.72, 0), grip, butt, (0, 0.2, -0.07))


# ───────────────────────── Boucliers ─────────────────────────
# Origine : sangle de l'avant-bras ; face du bouclier à u = 0.12, dans le plan (x, z).

def shield_plate(b, outline, u, thick, mat=PAINT, border=True):
    """Plaque plane : outline = [(x, z)] (polygone convexe), épaisseur le long de u."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x, u, z)) for (x, z) in outline]
    c = [bm.verts.new(P(x, u - thick, z)) for (x, z) in outline]
    bm.faces.new(a)
    bm.faces.new(list(reversed(c)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], c[j], c[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(0.008, thick * 0.4), segments=2, affect='EDGES', profile=0.5)
    b.add(bm, mat)


def shield_mount(b):
    b.add(box(0.12, 0.1, 0.16, (0, 0.05, 0.0), 0.012), ACCENT)
    b.add(box(0.03, 0.06, 0.12, (0, 0.0, 0.0), 0.005), METAL)


def rect(w, h, ch=0.06):
    return [(-w + ch, -h), (w - ch, -h), (w, -h + ch), (w, h - ch), (w - ch, h), (-w + ch, h), (-w, h - ch), (-w, -h + ch)]


def deflection(g):
    b = g.body
    shield_mount(b)
    shield_plate(b, rect(0.3, 0.5), 0.14, 0.035)
    shield_plate(b, rect(0.26, 0.46, 0.05), 0.145, 0.008, METAL)
    b.add(box(0.5, 0.004, 0.012, (0, 0.15, 0.3), 0.001, 1), GLOW)
    b.add(box(0.5, 0.004, 0.012, (0, 0.15, -0.3), 0.001, 1), GLOW)
    for x in (-0.18, 0.18):
        b.add(box(0.012, 0.004, 0.6, (x, 0.15, 0.0), 0.001, 1), ENERGY)
    pts(g, (0, 0.16, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def ionmirror(g):
    b = g.body
    shield_mount(b)
    hexa = [(0.34 * math.cos(a), 0.4 * math.sin(a)) for a in (i * math.pi / 3 + math.pi / 6 for i in range(6))]
    shield_plate(b, hexa, 0.14, 0.03)
    inner = [(0.24 * math.cos(a), 0.28 * math.sin(a)) for a in (i * math.pi / 3 + math.pi / 6 for i in range(6))]
    shield_plate(b, inner, 0.15, 0.008, GLASS)
    b.add(tube(0.06, 0.06, 0.15, 0.158, 0, 0, 20), ENERGY)
    pts(g, (0, 0.16, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def tower(g):
    b = g.body
    shield_mount(b)
    shield_plate(b, rect(0.38, 0.72, 0.08), 0.14, 0.05)
    shield_plate(b, rect(0.34, 0.68, 0.07), 0.15, 0.01, METAL)
    b.add(box(0.2, 0.01, 0.06, (0, 0.16, 0.5), 0.003), GLASS)                 # fente de visée
    for z in (-0.4, -0.1, 0.2):
        b.add(box(0.7, 0.006, 0.02, (0, 0.157, z), 0.002, 1), ACCENT)
    pts(g, (0, 0.16, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def reflector(g):
    b = g.body
    shield_mount(b)
    disc = [(0.27 * math.cos(a), 0.27 * math.sin(a)) for a in (i * math.pi / 12 for i in range(24))]
    shield_plate(b, disc, 0.14, 0.03, METAL)
    inner = [(0.22 * math.cos(a), 0.22 * math.sin(a)) for a in (i * math.pi / 12 for i in range(24))]
    shield_plate(b, inner, 0.146, 0.006, LENS)
    b.add(ring(0.28, 0.26, 0.115, 0.15, 0, 0, 32), ACCENT)
    pts(g, (0, 0.16, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


# ───────────────────────── Canons automatiques ─────────────────────────

def gatling(g, flared=False, disruptor=False):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.06)
    b.add(loft([(0.02, 0.13, 0.12, 0.02, 3.0), (0.3, 0.14, 0.13, 0.02, 3.0), (0.36, 0.11, 0.11, 0.02, 2.6)], 24), PAINT)   # boîtier moteur
    n = 8 if disruptor else 6
    r = 0.075 if disruptor else 0.06
    for k in range(n):
        a = k * 2 * math.pi / n
        x, z = r * math.cos(a), 0.02 + r * math.sin(a)
        b.add(tube(0.017 if not flared else 0.016, 0.017 if not flared else 0.026, 0.36, 1.0, x * (1.0 if not flared else 1.0), z, 12), METAL)
    for uu in (0.55, 0.85):
        b.add(ring(r + 0.03, r - 0.01, uu, uu + 0.03, 0, 0.02, 24), ALLOY)
    b.add(tube(0.025, 0.025, 0.36, 1.0, 0, 0.02, 12), ALLOY)                  # axe
    if disruptor:
        for k in range(n):
            a = k * 2 * math.pi / n
            b.add(tube(0.01, 0.01, 0.995, 1.002, r * math.cos(a), 0.02 + r * math.sin(a), 10), GLOW)
        b.add(tube_x(0.12, 0.16, (0, 0.12, -0.17), 26), ACCENT)             # gros tambour
    else:
        b.add(box(0.14, 0.2, 0.14, (0, 0.14, -0.17), 0.012), ACCENT)        # caisse de munitions
    b.add(box(0.03, 0.26, 0.04, (0.1, 0.2, -0.08), 0.006), METAL)            # goulotte
    hazard(b, 0.05, 0.15, 0, 0.08)
    pts(g, (0, 1.0, 0.02), grip, butt, (0, 0.3, -0.12), (0, 0.14, -0.17))


def fgs(g): gatling(g, True)
def disruptor(g): gatling(g, False, True)


def galleon(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.06)
    b.add(box(0.16, 0.36, 0.16, (0, 0.16, 0.02), 0.016), PAINT)              # culasse
    b.add(tube(0.035, 0.035, 0.34, 1.15, 0, 0.02, 20), METAL)
    b.add(tube(0.05, 0.05, 0.34, 0.5, 0, 0.02, 20), PAINT)
    muzzle_brake(b, 1.15, 0.02, 0.05, 0.12, 3)
    b.add(box(0.1, 0.14, 0.16, (0, 0.1, -0.13), 0.01), ACCENT)               # chargeur
    rail(b, 0.0, 0.3, 0.1)
    pts(g, (0, 1.27, 0.02), grip, butt, (0, 0.3, -0.1), (0, 0.1, -0.13))


def dexter(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.06)
    b.add(box(0.2, 0.42, 0.16, (0, 0.2, 0.02), 0.018), PAINT)
    b.add(tube(0.045, 0.05, 0.4, 0.86, 0, 0.03, 20), METAL)
    b.add(ring(0.06, 0.045, 0.82, 0.86, 0, 0.03, 20), ALLOY)
    b.add(tube_x(0.11, 0.12, (0, 0.18, -0.14), 26), ACCENT)                  # tambour
    vents(b, 0.08, 0.36, 0.04, 0.1, 5)
    pts(g, (0, 0.86, 0.03), grip, butt, (0, 0.3, -0.1), (0, 0.18, -0.14))


def flamerevolver(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.06)
    # Barillet de buses autour de l'axe, réservoir dessous.
    b.add(tube(0.11, 0.11, 0.1, 0.38, 0, 0.02, 24), PAINT)
    for k in range(6):
        a = k * math.pi / 3
        b.add(tube(0.024, 0.02, 0.38, 0.62, 0.06 * math.cos(a), 0.02 + 0.06 * math.sin(a), 12), METAL)
    b.add(tube(0.05, 0.08, 0.62, 0.7, 0, 0.02, 20, False), METAL)
    b.add(ring(0.08, 0.07, 0.69, 0.7, 0, 0.02, 20), GLOW)
    b.add(tube(0.07, 0.07, -0.1, 0.32, 0, -0.17, 20), ACCENT)                # réservoir
    b.add(ball(0.07, (0, 0.32, -0.17)), ACCENT)
    pts(g, (0, 0.72, 0.02), grip, butt, (0, 0.3, -0.12), (0, 0.1, -0.17))


# ───────────────────────── Artillerie ─────────────────────────

def cannon(g, r=0.05, l=1.1, breech=0.2, brake=True, rails=False, cyl=False):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.08)
    b.add(box(breech, 0.38, breech, (0, 0.14, 0.02), 0.02), PAINT)
    b.add(tube(r * 1.25, r * 1.1, 0.33, 0.6, 0, 0.02, 24), PAINT)
    b.add(tube(r, r, 0.6, l, 0, 0.02, 24), METAL)
    if brake: muzzle_brake(b, l, 0.02, r * 1.3, 0.14, 2)
    if cyl:
        for s in (-1, 1):
            piston(b, 0.1, 0.55, s * (breech / 2 + 0.02), 0.06, 0.02)        # vérins de recul
    if rails:
        for s in (-1, 1):
            b.add(box(0.02, l - 0.3, 0.05, (s * (r + 0.02), 0.3 + (l - 0.3) / 2, 0.02), 0.004), ALLOY)
            b.add(box(0.006, l - 0.32, 0.03, (s * (r + 0.006), 0.3 + (l - 0.3) / 2, 0.02), 0.001, 1), ENERGY)
    hazard(b, 0.0, breech / 2 + 0.02, 0, breech * 0.6)
    pts(g, (0, l + (0.14 if brake else 0), 0.02), grip, butt, (0, 0.3, -0.12), (0, 0.1, -0.08))


def handcannon(g): cannon(g, 0.05, 1.05, 0.2)
def gallic(g): cannon(g, 0.065, 1.4, 0.26, True, False, True)
def powerdyne(g): cannon(g, 0.035, 1.3, 0.2, False, True)


def cannonshot(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.08)
    b.add(box(0.22, 0.34, 0.22, (0, 0.13, 0.02), 0.02), PAINT)
    b.add(tube(0.09, 0.12, 0.3, 0.8, 0, 0.02, 26), METAL)
    b.add(ring(0.13, 0.1, 0.78, 0.82, 0, 0.02, 26), ACCENT)
    pts(g, (0, 0.82, 0.02), grip, butt, (0, 0.3, -0.12))


def mortar_tube(b, r, u0, u1, x=0.0, z=0.02):
    b.add(tube(r, r, u0, u1, x, z, 24), PAINT)
    b.add(ring(r * 1.12, r * 0.9, u1 - 0.04, u1, x, z, 24), METAL)
    for uu in (u0 + 0.06, (u0 + u1) / 2):
        b.add(ring(r * 1.08, r * 0.95, uu, uu + 0.03, x, z, 24), METAL)


def lightmortar(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.1)
    b.add(box(0.2, 0.3, 0.16, (0, 0.1, -0.02), 0.016), ACCENT)
    mortar_tube(b, 0.07, 0.0, 0.85)
    b.add(box(0.06, 0.12, 0.08, (0.11, 0.3, 0.06), 0.008), METAL)            # conduite de tir
    b.add(box(0.002, 0.04, 0.05, (0.141, 0.3, 0.06), 0.0005, 1), LENS)
    pts(g, (0, 0.85, 0.02), grip, butt, (0, 0.3, -0.12))


def heavymortar(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.12)
    b.add(box(0.28, 0.4, 0.22, (0, 0.12, -0.02), 0.02), ACCENT)
    mortar_tube(b, 0.11, -0.05, 1.05, 0, 0.04)
    for s in (-1, 1):
        piston(b, 0.0, 0.5, s * 0.15, 0.04, 0.02)
    scope(b, 0.25, 0.16, 0.25, 0.018, 0.026, METAL, 0.155)
    pts(g, (0, 1.05, 0.04), grip, butt, (0, 0.3, -0.14))


def dispersal(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.12)
    b.add(box(0.3, 0.3, 0.26, (0, 0.1, 0.02), 0.02), ACCENT)
    for (x, z) in ((-0.07, -0.05), (0.07, -0.05), (-0.07, 0.09), (0.07, 0.09)):
        mortar_tube(b, 0.055, 0.2, 0.85, x, z)
    pts(g, (0, 0.85, 0.02), grip, butt, (0, 0.3, -0.14))


def crusher(g):
    b = g.body
    grip, butt = sleeve(b, -0.34, 0.06, -0.16)
    b.add(box(0.36, 0.44, 0.3, (0, 0.12, 0.0), 0.024), ACCENT)
    mortar_tube(b, 0.17, 0.0, 1.1, 0, 0.06)
    b.add(ball(0.15, (0, 1.06, 0.06)), GLOW)                                  # ogive visible
    for s in (-1, 1):
        piston(b, 0.0, 0.6, s * 0.21, 0.06, 0.026)
    hazard(b, 0.1, 0.16, 0, 0.2, 6)
    pts(g, (0, 1.1, 0.06), grip, butt, (0, 0.3, -0.18))


# ───────────────────────── Lance-missiles ─────────────────────────

def cell_box(b, cols, rows, cell, u0, u1, z=0.12, x=0.0, mat=PAINT):
    w, h = cols * cell + 0.03, rows * cell + 0.03
    b.add(box(w, u1 - u0, h, (x, (u0 + u1) / 2, z), 0.012), mat)
    for i in range(cols):
        for j in range(rows):
            cx = x + (i - (cols - 1) / 2) * cell
            cz = z + (j - (rows - 1) / 2) * cell
            b.add(ring(cell * 0.44, cell * 0.36, u1 - 0.006, u1 + 0.004, cx, cz, 14), METAL)
            b.add(tube(cell * 0.36, cell * 0.36, u1 - 0.02, u1 - 0.016, cx, cz, 12), GLOW)


def launcher_base(b):
    """Fourreau à l'épaule : le caisson est posé dessus (repère Butt à l'arrière)."""
    grip, butt = sleeve(b, -0.34, 0.06, -0.06)
    b.add(box(0.12, 0.2, 0.06, (0, -0.02, 0.0), 0.01), METAL)
    return grip, butt


def armhound(g):
    b = g.body
    grip, butt = launcher_base(b)
    cell_box(b, 2, 2, 0.1, -0.24, 0.42, 0.13)
    hazard(b, -0.1, 0.24, 0, 0.12)
    pts(g, (0, 0.44, 0.13), grip, butt, (0, 0.2, -0.12))


def bloodstorm(g):
    b = g.body
    grip, butt = launcher_base(b)
    cell_box(b, 5, 4, 0.05, -0.2, 0.4, 0.14)
    pts(g, (0, 0.42, 0.14), grip, butt, (0, 0.2, -0.12))


def haytal(g):
    b = g.body
    grip, butt = launcher_base(b)
    cell_box(b, 5, 3, 0.075, -0.26, 0.4, 0.15)
    b.add(box(0.1, 0.12, 0.06, (0.24, 0.0, 0.15), 0.008), METAL)
    pts(g, (0, 0.42, 0.15), grip, butt, (0, 0.2, -0.12))


def leviathan(g):
    b = g.body
    grip, butt = launcher_base(b)
    b.add(tube(0.16, 0.16, -0.5, 0.75, 0, 0.18, 28), PAINT)                  # tube géant
    b.add(ring(0.18, 0.15, 0.72, 0.78, 0, 0.18, 28), METAL)
    b.add(ring(0.18, 0.15, -0.52, -0.46, 0, 0.18, 28), METAL)
    b.add(tube(0.13, 0.002, 0.74, 0.98, 0, 0.18, 24), ACCENT)                # ogive
    hazard(b, -0.2, 0.34, 0, 0.14, 6)
    pts(g, (0, 0.98, 0.18), grip, butt, (0, 0.2, -0.12))


def arcane(g):
    b = g.body
    grip, butt = launcher_base(b)
    b.add(tube(0.08, 0.08, -0.2, 0.5, 0, 0.17, 20), PAINT)
    for k in range(6):
        a = k * math.pi / 3
        x, z = 0.1 * math.cos(a), 0.17 + 0.1 * math.sin(a)
        b.add(tube(0.045, 0.045, -0.24, 0.5, x, z, 16), METAL)
        b.add(tube(0.035, 0.035, 0.5, 0.505, x, z, 14), GLOW)
    b.add(ring(0.17, 0.14, 0.0, 0.04, 0, 0.17, 28), ACCENT)
    b.add(ring(0.17, 0.14, 0.4, 0.44, 0, 0.17, 28), ACCENT)
    pts(g, (0, 0.52, 0.17), grip, butt, (0, 0.2, -0.12))


def fgimpact(g):
    b = g.body
    grip, butt = launcher_base(b)
    # Caisson à lancement vertical : cellules ouvertes vers le haut.
    b.add(box(0.3, 0.42, 0.24, (0, 0.08, 0.15), 0.014), PAINT)
    for i in range(3):
        for j in range(3):
            cx, cu = (i - 1) * 0.09, 0.08 + (j - 1) * 0.12
            b.add(box(0.07, 0.09, 0.006, (cx, cu, 0.272), 0.002), METAL)
            b.add(box(0.05, 0.07, 0.004, (cx, cu, 0.268), 0.001, 1), GLOW)
    pts(g, (0, 0.2, 0.3), grip, butt, (0, 0.2, -0.12))


def phoenix(g):
    b = g.body
    grip, butt = launcher_base(b)
    b.add(box(0.12, 0.9, 0.05, (0, 0.2, 0.08), 0.01), PAINT)                 # rail
    b.add(tube(0.06, 0.06, -0.2, 0.55, 0, 0.17, 20), ALLOY)                   # missile posé
    b.add(tube(0.06, 0.002, 0.55, 0.72, 0, 0.17, 20), ACCENT)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        bm = box(0.004, 0.12, 0.08, (0, 0, 0), 0.001)
        b.add(bm, PAINT, Matrix.Translation(P(0.06 * math.cos(a), -0.12, 0.17 + 0.06 * math.sin(a))) @ Matrix.Rotation(a, 4, 'Y'))
    pts(g, (0, 0.72, 0.17), grip, butt, (0, 0.2, -0.12))


STEEL, ORANGE, DARK = (0.2, 0.22, 0.25), (0.6, 0.33, 0.08), (0.07, 0.075, 0.08)
FAMILIES = {f: globals()[f] for f in [
    "hammer", "vulcan", "blade", "forceax", "roller", "haken", "blasthole", "twinspear", "flashing", "jackhammer", "spinedriver",
    "deflection", "ionmirror", "tower", "reflector", "gatling", "fgs", "disruptor", "galleon", "dexter", "flamerevolver",
    "handcannon", "gallic", "powerdyne", "cannonshot", "lightmortar", "heavymortar", "dispersal", "crusher",
    "armhound", "bloodstorm", "haytal", "leviathan", "arcane", "fgimpact", "phoenix"]}
PALETTE = {f: (STEEL, DARK) for f in FAMILIES}
PALETTE.update({"vulcan": (STEEL, ORANGE), "crusher": (STEEL, ORANGE), "leviathan": ((0.32, 0.3, 0.22), DARK),
                "ionmirror": ((0.5, 0.52, 0.56), DARK), "reflector": ((0.55, 0.57, 0.6), ORANGE), "tower": ((0.24, 0.26, 0.2), DARK),
                "flamerevolver": (STEEL, (0.45, 0.12, 0.06)), "disruptor": ((0.14, 0.15, 0.17), ORANGE)})


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
