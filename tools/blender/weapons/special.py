# Grenades et armes spéciales du Ranger (EDF 5), une fonction par famille de modèle.
# Grenades à main (tenues d'une main, repère Grip au centre) : MG = grenade à fragmentation, DNG = grosse charge,
# Bound Grenade = sphère rebondissante. Lance-grenades : UM = monocoup à canon basculant (type M79/M320 à crosse),
# BRUTE = trois canons, CRUMBLE = deux canons, Stampede = revolver à barillet (type MGL), Splash / Sticky = canon court
# à gros tube, Volatile/Volcanic Napalm = lanceur à réservoir de gel, Flame Geyser = lanceur à colonne de feu.
# Spéciales : Acid = pulvérisateur à réservoir, Burner/Flamethrower = lance-flammes à réservoirs, Flame/Magma Cannon =
# canon à flammes lourd, Firecracker = caisson à pétards, Bound Gun = arme à balles rebondissantes, C Bomb = charge
# explosive et détonateur, Impulse = mine disque, Reverser = pistolet de soin à cartouche, Reverse Shooter = lanceur de soin.
import math


def sphere(r, c, scale=(1, 1, 1), seg=18, rings=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def hand_points(g, c=(0, 0, 0)):
    g.point("Muzzle", (c[0], c[1] + 0.05, c[2])); g.point("Grip", c); g.point("Support", (c[0], c[1] + 0.06, c[2]))
    g.point("Butt", (c[0], c[1] - 0.1, c[2])); g.point("Mag", c)


def mg(g):
    """Grenade à fragmentation : corps ovoïde quadrillé, bouchon allumeur, cuiller, goupille et anneau."""
    b = g.body
    b.add(sphere(0.032, (0, 0, -0.005), (1, 1, 1.25), 18, 12), PAINT)
    # Rainures de fragmentation : bagues horizontales autour du corps.
    for k in range(4):
        z = -0.03 + k * 0.016
        rr = 0.032 * math.sqrt(max(0.0, 1 - ((z + 0.005) / 0.04) ** 2)) + 0.0008
        b.add(ring(rr, rr - 0.003, -0.0015, 0.0015, 0, 0, 22), PAINT, Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.radians(90), 4, 'X'))
    b.add(box(0.026, 0.026, 0.02, (0, 0, 0.042), 0.006), METAL)                     # allumeur
    b.add(prism([(0.012, 0.05), (0.02, 0.05), (0.034, 0.0), (0.03, -0.03), (0.026, -0.03), (0.03, 0.0)], -0.006, 0.006, 0.001), METAL)  # cuiller
    b.add(ring(0.012, 0.009, -0.002, 0.002, 0.02, 0.05, 16), METAL)                # anneau
    hand_points(g)


def dng(g):
    """Grosse charge à lancer : cylindre bagué, poignée de lancer, allumeur à capuchon rouge."""
    b = g.body
    b.add(tube(0.045, 0.045, -0.06, 0.06, 0, 0, 22), PAINT)
    for uu in (-0.06, 0.0, 0.06):
        b.add(ring(0.048, 0.04, uu - 0.008, uu + 0.008, 0, 0, 22), METAL)
    b.add(tube(0.02, 0.02, -0.12, -0.06, 0, 0, 14), POLY)                           # poignée
    b.add(tube(0.016, 0.012, 0.06, 0.085, 0, 0, 14), ACCENT)
    hand_points(g, (0, -0.09, 0))


def boundgren(g):
    """Grenade rebondissante : sphère caoutchoutée à bandes, allumeur encastré."""
    b = g.body
    b.add(sphere(0.042, (0, 0, 0), (1, 1, 1), 22, 14), RUBBER)
    for a in (0, 60, 120):
        bm = ring(0.0435, 0.04, -0.004, 0.004, 0, 0, 26)
        b.add(bm, ACCENT, Matrix.Rotation(math.radians(a), 4, 'Z') @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    b.add(tube(0.012, 0.012, -0.004, 0.004, 0, 0.042, 14), METAL)
    hand_points(g)


def gl_frame(b, mat=PAINT, u0=-0.08, u1=0.12):
    """Carcasse de lance-grenades : boîtier, poignée pistolet, crosse."""
    receiver(b, u0, u1, 0.024, -0.05, 0.02, mat, 6.0)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 15, 0.1, 0.032, 0.046)
    butt = stock_sculpt(b, [(u0, 0.022, 0.03, -0.012), (u0 - 0.12, 0.022, 0.045, -0.022), (u0 - 0.25, 0.022, 0.05, -0.026), (u0 - 0.265, 0.021, 0.048, -0.026)], mat)
    return grip, butt


def um(g):
    """Monocoup 40 mm à canon basculant : gros canon court, garde-main, hausse à échelle, crosse pleine."""
    b = g.body
    grip, butt = gl_frame(b)
    b.add(tube(0.028, 0.028, 0.1, 0.46, 0, -0.005, 24), METAL)
    b.add(tube(0.022, 0.022, 0.459, 0.462, 0, -0.005, 20), POLY)
    b.add(loft([(0.14, 0.032, 0.024, -0.03, 3.0), (0.36, 0.032, 0.024, -0.03, 3.0)], 18), PAINT)
    b.add(box(0.02, 0.03, 0.05, (0, 0.14, 0.045), 0.003), METAL)                     # hausse à échelle
    b.add(box(0.006, 0.012, 0.02, (0, 0.44, 0.03), 0.002), METAL)
    rail(b, 0.0, 0.1, 0.02)
    g.point("Muzzle", (0, 0.46, -0.005)); g.point("Grip", grip); g.point("Support", (0, 0.27, -0.05))
    g.point("Butt", butt); g.point("Mag", (0, 0.1, -0.005))


def brute(g, n=3):
    """Lance-grenades à canons multiples côte à côte (BRUTE ×3, CRUMBLE ×2), culasse commune, viseur réflexe."""
    b = g.body
    grip, butt = gl_frame(b, PAINT, -0.08, 0.14)
    for k in range(n):
        x = (k - (n - 1) / 2) * 0.05
        b.add(tube(0.024, 0.024, 0.12, 0.48, x, 0.0, 22), METAL)
        b.add(tube(0.019, 0.019, 0.479, 0.482, x, 0.0, 18), POLY)
    for uu in (0.14, 0.32, 0.46):
        b.add(box(n * 0.05 + 0.01, 0.02, 0.054, (0, uu, 0), 0.005), ACCENT)
    b.add(box(0.04, 0.12, 0.03, (0, 0.27, -0.042), 0.005), POLY)
    red_dot(b, 0.06, 0.03)
    g.point("Muzzle", (0, 0.48, 0)); g.point("Grip", grip); g.point("Support", (0, 0.27, -0.06))
    g.point("Butt", butt); g.point("Mag", (0, 0.12, 0))


def crumble(g):
    brute(g, 2)


def stampede(g):
    """Revolver lance-grenades (type MGL) : barillet à six chambres, canon court, crosse télescopique, poignée avant."""
    b = g.body
    grip, butt = gl_frame(b, PAINT, -0.09, 0.05)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(tube(0.022, 0.022, 0.05, 0.2, math.cos(a) * 0.04, -0.01 + math.sin(a) * 0.04, 18), METAL)
    b.add(tube(0.012, 0.012, 0.04, 0.21, 0, -0.01, 14), METAL)
    b.add(tube(0.025, 0.025, 0.2, 0.42, 0, 0.03, 20), METAL)
    b.add(loft([(0.22, 0.03, 0.03, 0.03, 3.5), (0.38, 0.03, 0.03, 0.03, 3.5)], 20), POLY)
    rail(b, 0.22, 0.38, 0.06)
    vertical_grip(b, 0.31, 0.0)
    red_dot(b, 0.29, 0.066)
    g.point("Muzzle", (0, 0.42, 0.03)); g.point("Grip", grip); g.point("SupportV", (0, 0.31, -0.045))
    g.point("Butt", butt); g.point("Mag", (0, 0.12, -0.01))


def splash(g, sticky=False):
    """Canon court à gros tube (projette une gerbe de grenades), réservoir de sous-munitions sous le canon."""
    b = g.body
    grip, butt = gl_frame(b)
    b.add(tube(0.04, 0.045, 0.1, 0.4, 0, 0.0, 24), PAINT)
    b.add(ring(0.05, 0.036, 0.39, 0.42, 0, 0.0, 24), METAL)
    b.add(tube(0.03, 0.03, 0.12, 0.3, 0, -0.06, 18), ACCENT)                         # magasin
    if sticky:
        for k in range(8):
            a = 2 * math.pi * k / 8
            b.add(box(0.004, 0.06, 0.008, (math.cos(a) * 0.046, 0.36, math.sin(a) * 0.046), 0.001), ACCENT)
    red_dot(b, 0.0, 0.02)
    g.point("Muzzle", (0, 0.42, 0)); g.point("Grip", grip); g.point("Support", (0, 0.25, -0.09))
    g.point("Butt", butt); g.point("Mag", (0, 0.2, -0.06))


def sticky(g):
    splash(g, True)


def napalm(g, geyser=False):
    """Lanceur de napalm : canon court, réservoir de gel incendiaire translucide sur le côté, buse en bec."""
    b = g.body
    grip, butt = gl_frame(b, ACCENT)
    b.add(tube(0.03, 0.03, 0.1, 0.42, 0, 0.0, 22), METAL)
    b.add(tube(0.036, 0.024, 0.42, 0.47, 0, 0.0, 22), METAL)
    b.add(tube(0.035, 0.035, 0.0, 0.3, 0.05, -0.02, 20), PAINT)                      # réservoir
    for uu in (0.03, 0.27):
        b.add(ring(0.038, 0.03, uu - 0.008, uu + 0.008, 0.05, -0.02, 20), METAL)
    if geyser:
        for k in range(4):
            b.add(box(0.07, 0.008, 0.07, (0, 0.2 + k * 0.05, 0.0), 0.004), ACCENT)
    red_dot(b, 0.0, 0.02)
    g.point("Muzzle", (0, 0.47, 0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.04))
    g.point("Butt", butt); g.point("Mag", (0.05, 0.15, -0.02))


def geyser(g):
    napalm(g, True)


def acid(g):
    """Pulvérisateur d'acide : réservoir vert translucide sur le dessus, pompe, longue buse, poignée verticale."""
    b = g.body
    receiver(b, -0.1, 0.18, 0.026, -0.05, 0.02, PAINT, 6.0)
    b.add(tube(0.045, 0.045, -0.06, 0.18, 0, 0.07, 22), ENERGY)                     # réservoir
    for uu in (-0.06, 0.06, 0.18):
        b.add(ring(0.048, 0.04, uu - 0.008, uu + 0.008, 0, 0.07, 22), METAL)
    b.add(tube(0.014, 0.012, 0.18, 0.56, 0, 0.0, 16), METAL)
    b.add(tube(0.022, 0.014, 0.56, 0.6, 0, 0.0, 16), ACCENT)
    b.add(tube(0.008, 0.008, 0.18, 0.4, 0, -0.03, 10), POLY)                         # durite
    grip = pistol_grip(b, -0.04, -0.05, POLY, 15)
    butt = stock_sculpt(b, [(-0.1, 0.022, 0.03, -0.012), (-0.22, 0.022, 0.044, -0.022), (-0.33, 0.022, 0.05, -0.026), (-0.345, 0.021, 0.048, -0.026)], PAINT)
    vertical_grip(b, 0.3, -0.012)
    g.point("Muzzle", (0, 0.6, 0)); g.point("Grip", grip); g.point("SupportV", (0, 0.3, -0.058))
    g.point("Butt", butt); g.point("Mag", (0, 0.06, 0.07))


def flamer(g, cannon=False):
    """Lance-flammes : deux réservoirs jumelés sous le corps, lance à veilleuse, garde-main isolé ; canon à flammes plus lourd."""
    b = g.body
    receiver(b, -0.1, 0.16, 0.026, -0.04, 0.02, PAINT, 6.0)
    r = 0.04 if cannon else 0.032
    for s in (-1, 1):
        b.add(tube(r, r, -0.12, 0.2, s * (r + 0.004), -0.07, 20), ACCENT)
        b.add(sphere(r, (s * (r + 0.004), 0.2, -0.07), (1, 0.4, 1), 18, 8), ACCENT)
        b.add(sphere(r, (s * (r + 0.004), -0.12, -0.07), (1, 0.4, 1), 18, 8), ACCENT)
    lance_r = 0.03 if cannon else 0.016
    b.add(tube(lance_r, lance_r, 0.16, 0.62 if cannon else 0.58, 0, 0.0, 20), METAL)
    b.add(loft([(0.18, lance_r + 0.012, lance_r + 0.012, 0, 2.0), (0.38, lance_r + 0.012, lance_r + 0.012, 0, 2.0)], 20), POLY)
    for k in range(5):
        b.add(ring(lance_r + 0.016, lance_r + 0.01, 0.2 + k * 0.04, 0.21 + k * 0.04, 0, 0, 20), POLY)
    b.add(tube(lance_r + 0.008, lance_r + 0.012, 0.6 if cannon else 0.56, 0.64 if cannon else 0.6, 0, 0.0, 20), METAL)
    b.add(tube(0.006, 0.006, 0.5, 0.58, 0, -lance_r - 0.008, 10), METAL)              # veilleuse
    b.add(sphere(0.007, (0, 0.585, -lance_r - 0.008)), GLOW)
    grip = pistol_grip(b, -0.03, -0.04, POLY, 12)
    butt = stock_sculpt(b, [(-0.1, 0.022, 0.03, -0.006), (-0.2, 0.022, 0.04, -0.014), (-0.3, 0.022, 0.045, -0.018), (-0.315, 0.021, 0.044, -0.018)], PAINT)
    vertical_grip(b, 0.29, -lance_r - 0.012)
    g.point("Muzzle", (0, 0.64 if cannon else 0.6, 0)); g.point("Grip", grip); g.point("SupportV", (0, 0.29, -lance_r - 0.06))
    g.point("Butt", butt); g.point("Mag", (0, 0.04, -0.07))


def flamecannon(g):
    flamer(g, True)


def firecracker(g):
    """Caisson à pétards : boîte à alvéoles multiples tenue comme un lance-roquettes compact, viseur réflexe."""
    b = g.body
    b.add(box(0.12, 0.3, 0.1, (0, 0.15, 0.0), 0.01), PAINT)
    for c in range(4):
        for rr in range(3):
            x = (c - 1.5) * 0.026
            z = (rr - 1) * 0.026
            b.add(tube(0.009, 0.009, 0.299, 0.303, x, z, 10), POLY)
    b.add(box(0.13, 0.02, 0.11, (0, 0.02, 0.0), 0.004), ACCENT)
    grip = pistol_grip(b, 0.05, -0.05, POLY, 12)
    red_dot(b, 0.15, 0.05)
    b.add(prism([(0.0, -0.05), (-0.18, -0.03), (-0.2, -0.08), (-0.02, -0.08)], -0.02, 0.02, 0.004), POLY)
    g.point("Muzzle", (0, 0.3, 0)); g.point("Grip", grip); g.point("Support", (0, 0.2, -0.05))
    g.point("Butt", (0, -0.2, -0.055)); g.point("Mag", (0, 0.15, -0.05))


def boundgun(g):
    """Arme à balles rebondissantes : compacte, canon à ailettes déflectrices, chargeur droit, crosse repliée."""
    b = g.body
    receiver(b, -0.1, 0.2, 0.022, -0.045, 0.024, PAINT, 6.0)
    b.add(tube(0.011, 0.011, 0.2, 0.42, 0, 0.0, 16), METAL)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(box(0.004, 0.08, 0.016, (math.cos(a) * 0.016, 0.39, math.sin(a) * 0.016), 0.001), ACCENT)
    rail(b, -0.08, 0.18, 0.024)
    grip = pistol_grip(b, -0.03, -0.045, POLY, 15)
    mag = straight_mag(b, 0.07, -0.045, 0.14, 0.022, 0.05, 4)
    b.add(prism([(-0.1, 0.0), (-0.28, -0.01), (-0.28, -0.05), (-0.1, -0.04)], -0.012, 0.012, 0.003), POLY)
    vertical_grip(b, 0.26, -0.045)
    red_dot(b, 0.04, 0.033)
    g.point("Muzzle", (0, 0.43, 0)); g.point("Grip", grip); g.point("SupportV", (0, 0.26, -0.09))
    g.point("Butt", (0, -0.28, -0.03)); g.point("Mag", mag)


def cbomb(g):
    """Charge C : pain d'explosif emballé, ruban, détonateur à antenne et voyant."""
    b = g.body
    b.add(box(0.07, 0.12, 0.045, (0, 0, 0), 0.006), ACCENT)
    for uu in (-0.03, 0.03):
        b.add(box(0.074, 0.012, 0.049, (0, uu, 0), 0.002), POLY)
    b.add(box(0.03, 0.04, 0.02, (0, 0.0, 0.032), 0.004), METAL)
    b.add(box(0.003, 0.003, 0.07, (0.01, 0.01, 0.075), 0.0008, 1), METAL)            # antenne
    b.add(sphere(0.005, (-0.008, 0.012, 0.045)), GLOW)
    hand_points(g)


def impulse(g):
    """Mine Impulse : disque à capteurs et lamelles de tir, voyant rouge."""
    b = g.body
    bm = tube(0.055, 0.06, -0.015, 0.015, 0, 0, 24)
    b.add(bm, PAINT, Matrix.Rotation(math.radians(90), 4, 'X'))
    for k in range(8):
        a = 2 * math.pi * k / 8
        b.add(box(0.012, 0.012, 0.008, (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.019), 0.002), METAL)
    b.add(sphere(0.008, (0, 0, 0.02)), GLOW)
    hand_points(g)


def reverser(g, shooter=False):
    """Pistolet de soin : corps clair arrondi, cartouche de régénération verte, bouche diffuseur ; Reverse Shooter : canon de lancement."""
    b = g.body
    b.add(loft([(-0.12, 0.022, 0.04, -0.005, 3.0), (-0.06, 0.028, 0.05, 0.0, 3.5), (0.16, 0.028, 0.045, 0.002, 3.5), (0.22, 0.02, 0.03, 0.004, 3.0)], 24), PAINT)
    b.add(tube(0.022, 0.022, -0.02, 0.12, 0, 0.06, 18), ENERGY)                      # cartouche
    for uu in (-0.02, 0.12):
        b.add(ring(0.025, 0.018, uu - 0.008, uu + 0.008, 0, 0.06, 18), ACCENT)
    if shooter:
        b.add(tube(0.03, 0.03, 0.2, 0.46, 0, 0.004, 22), ACCENT)
        b.add(ring(0.036, 0.024, 0.44, 0.47, 0, 0.004, 22), METAL)
        mz = 0.47
    else:
        b.add(tube(0.012, 0.02, 0.22, 0.32, 0, 0.004, 18), ACCENT)
        b.add(tube(0.018, 0.018, 0.319, 0.322, 0, 0.004, 16), ENERGY)
        mz = 0.32
    grip = pistol_grip(b, -0.03, -0.035, POLY, 12)
    b.add(prism([(-0.12, 0.0), (-0.3, -0.01), (-0.3, -0.06), (-0.12, -0.035)], -0.016, 0.016, 0.004), PAINT)
    if shooter:
        red_dot(b, 0.15, 0.047)
    
    g.point("Muzzle", (0, mz, 0.004)); g.point("Grip", grip); g.point("Support", (0, 0.16, -0.04))
    g.point("Butt", (0, -0.3, -0.035)); g.point("Mag", (0, 0.05, 0.06))


def revshooter(g):
    reverser(g, True)


PALETTE = {
    "mg": ((0.18, 0.21, 0.13), (0.6, 0.1, 0.06)), "dng": ((0.16, 0.18, 0.12), (0.7, 0.12, 0.06)),
    "boundgren": ((0.1, 0.1, 0.1), (0.75, 0.55, 0.08)), "um": ((0.16, 0.18, 0.13), (0.05, 0.05, 0.05)),
    "brute": ((0.14, 0.15, 0.13), (0.3, 0.27, 0.2)), "crumble": ((0.3, 0.27, 0.19), (0.12, 0.12, 0.11)),
    "stampede": ((0.12, 0.13, 0.11), (0.05, 0.05, 0.05)), "splash": ((0.2, 0.22, 0.25), (0.6, 0.4, 0.08)),
    "sticky": ((0.22, 0.2, 0.17), (0.65, 0.5, 0.08)), "napalm": ((0.45, 0.18, 0.06), (0.12, 0.12, 0.11)),
    "geyser": ((0.5, 0.25, 0.05), (0.15, 0.15, 0.14)), "acid": ((0.16, 0.18, 0.14), (0.3, 0.6, 0.1)),
    "flamer": ((0.14, 0.15, 0.13), (0.55, 0.16, 0.06)), "flamecannon": ((0.12, 0.12, 0.12), (0.6, 0.3, 0.05)),
    "firecracker": ((0.55, 0.12, 0.08), (0.75, 0.6, 0.15)), "boundgun": ((0.16, 0.2, 0.26), (0.55, 0.6, 0.65)),
    "cbomb": ((0.2, 0.22, 0.16), (0.55, 0.5, 0.35)), "impulse": ((0.15, 0.17, 0.13), (0.05, 0.05, 0.05)),
    "reverser": ((0.85, 0.86, 0.88), (0.25, 0.6, 0.35)), "revshooter": ((0.8, 0.82, 0.85), (0.25, 0.55, 0.32)),
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
