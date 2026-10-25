# Fusils à pompe et fusils de précision du Ranger (EDF 5), une fonction par famille de modèle.
# Fusils à pompe : Slaughter = SPAS-12 (pompe, crosse repliable), SGN = semi-auto à tambour (type AA-12), G1 Herring = fusil
# à canons juxtaposés renforcé, Breecher = fusil de brèche court à gros calibre, Monsoon = fusil à canons tournants,
# Slugger Shot = fusil à balles (canon long, lunette), Buster Shot / Fracture = modèles hors normes (contenus téléchargeables).
# Précision : KFF = à verrou (type M24/AWM), NP = semi-auto (type PSG-1), Lysander = anti-matériel (type M82),
# Fang = semi-auto à crosse squelette (type SVD), Dunkel = bullpup (type WA2000), Eagle = semi-auto à rail (type M110),
# Flounders = tir en rafale, double chargeur, Ptarmigan = anti-matériel lourd à bipied, Pylon = expérimental à fût carré,
# Penetrator = fusil électromagnétique perforant (rails).
import math


def pump_body(b, u0, u1, z0=-0.035, z1=0.02, mat=METAL):
    receiver(b, u0, u1, 0.018, z0, z1, mat, 6.0)
    b.add(box(0.004, 0.07, 0.022, (0.018, (u0 + u1) / 2 + 0.02, (z0 + z1) / 2 + 0.006), 0.001), POLY)   # éjection


def slaughter(g):
    """SPAS-12 : boîtier massif, tube magasin sous le canon, devant de pompe ajouré, crosse repliable, garde-main perforé."""
    b = g.body
    pump_body(b, -0.08, 0.17)
    b.add(tube(0.011, 0.011, 0.17, 0.66, 0, 0.004, 18), METAL)                       # canon
    b.add(tube(0.012, 0.012, 0.17, 0.6, 0, -0.024, 18), METAL)                       # tube magasin
    b.add(tube(0.013, 0.013, 0.59, 0.61, 0, -0.024, 18), METAL)
    b.add(loft([(0.2, 0.024, 0.024, -0.016, 2.2), (0.42, 0.024, 0.024, -0.016, 2.2)], 22), POLY)  # devant de pompe
    for k in range(6):
        b.add(ring(0.0255, 0.022, 0.22 + k * 0.032, 0.232 + k * 0.032, 0, -0.016, 22), POLY)
    b.add(loft([(0.42, 0.017, 0.022, -0.004, 2.4), (0.62, 0.017, 0.022, -0.004, 2.4)], 18), METAL)    # protège-main ajouré
    vents(b, 0.44, 0.6, 0.008, 0.017, 6, 0.01)
    vents(b, 0.44, 0.6, 0.008, -0.017, 6, 0.01)
    b.add(box(0.006, 0.02, 0.012, (0, 0.63, 0.026), 0.002), METAL)                   # guidon
    b.add(box(0.03, 0.03, 0.02, (0, -0.04, 0.03), 0.004), METAL)                     # hausse
    grip = pistol_grip(b, -0.05, -0.035, POLY, 18, 0.11, 0.032, 0.048)
    # Crosse repliable (tubes et crochet de bras).
    for s in (-1, 1):
        b.add(tube(0.006, 0.006, -0.36, -0.08, s * 0.016, -0.01, 10), METAL)
    b.add(loft([(-0.36, 0.022, 0.05, -0.03, 4.0), (-0.375, 0.022, 0.05, -0.03, 4.0)], 18), METAL)
    b.add(prism([(-0.37, -0.08), (-0.35, -0.08), (-0.3, -0.12), (-0.31, -0.13)], -0.004, 0.004, 0.002), METAL)
    butt = (0, -0.375, -0.03)
    laser_module(g.opt("laser"), 0.36, -0.02, -0.03)
    g.point("Muzzle", (0, 0.66, 0.004)); g.point("Grip", grip); g.point("Support", (0, 0.31, -0.04))
    g.point("Butt", butt); g.point("Mag", (0, 0.12, -0.05))


def sgn(g):
    """Semi-automatique à tambour : boîtier carré, gros tambour, rail, frein de bouche, poignée de transport."""
    b = g.body
    receiver(b, -0.12, 0.24, 0.026, -0.045, 0.025, PAINT, 6.0)
    rail(b, -0.1, 0.22, 0.025)
    b.add(tube(0.012, 0.012, 0.24, 0.52, 0, 0.0, 18), METAL)
    b.add(loft([(0.24, 0.026, 0.03, -0.006, 5.0), (0.42, 0.026, 0.03, -0.006, 5.0)], 20), POLY)
    vents(b, 0.26, 0.4, -0.006, 0.026, 5, 0.03)
    muzzle_brake(b, 0.52, 0, 0.02, 0.07, 3)
    b.add(loft([(-0.06, 0.008, 0.012, 0.07, 3.0), (0.16, 0.008, 0.012, 0.07, 3.0)], 12), PAINT)   # poignée de transport
    for uu in (-0.06, 0.16):
        b.add(box(0.016, 0.02, 0.05, (0, uu, 0.05), 0.003), PAINT)
    grip = pistol_grip(b, -0.03, -0.045, POLY, 15, 0.1, 0.032, 0.046)
    mag = drum_mag(b, 0.1, -0.045, 0.075, 0.07, POLY)
    butt = stock_sculpt(b, [(-0.12, 0.024, 0.04, -0.008), (-0.25, 0.024, 0.048, -0.014), (-0.36, 0.023, 0.052, -0.018), (-0.375, 0.022, 0.05, -0.018)], PAINT)
    red_dot(b, 0.08, 0.034)
    laser_module(g.opt("laser"), 0.36, -0.006, -0.04)
    g.point("Muzzle", (0, 0.59, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.33, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


def herring(g):
    """Canons juxtaposés renforcés : bascule, gros devant, crosse pleine à poignée pistolet."""
    b = g.body
    receiver(b, -0.08, 0.06, 0.024, -0.045, 0.012, METAL, 5.0)
    for s in (-1, 1):
        b.add(tube(0.0125, 0.0125, 0.06, 0.62, s * 0.0125, 0.0, 18), METAL)
    b.add(box(0.008, 0.56, 0.008, (0, 0.34, 0.012), 0.002), METAL)                  # bande ventilée
    b.add(loft([(0.08, 0.028, 0.022, -0.022, 3.0), (0.33, 0.026, 0.02, -0.02, 3.0)], 20), PAINT)
    grip = pistol_grip(b, -0.04, -0.04, PAINT, 20, 0.1, 0.03, 0.044)
    butt = stock_sculpt(b, [(-0.08, 0.02, 0.026, -0.01), (-0.18, 0.021, 0.034, -0.02), (-0.33, 0.022, 0.05, -0.034), (-0.345, 0.021, 0.05, -0.034)], PAINT)
    b.add(box(0.024, 0.012, 0.01, (0, -0.06, 0.018), 0.002), METAL)                  # clé d'ouverture
    laser_module(g.opt("laser"), 0.25, -0.03, -0.04)
    g.point("Muzzle", (0, 0.62, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.22, -0.045))
    g.point("Butt", butt); g.point("Mag", (0, 0.04, -0.03))


def breecher(g):
    """Fusil de brèche : très court, gros calibre, bouche dentelée, chargeur droit, poignée verticale."""
    b = g.body
    receiver(b, -0.1, 0.2, 0.025, -0.05, 0.026, PAINT, 6.0)
    b.add(tube(0.017, 0.017, 0.2, 0.42, 0, -0.004, 20), METAL)
    b.add(tube(0.024, 0.024, 0.42, 0.47, 0, -0.004, 20), METAL)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(box(0.008, 0.02, 0.008, (math.cos(a) * 0.024, 0.475, -0.004 + math.sin(a) * 0.024), 0.002), METAL)
    rail(b, -0.08, 0.18, 0.026)
    b.add(box(0.04, 0.12, 0.04, (0, 0.3, -0.034), 0.006), POLY)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 15, 0.1, 0.032, 0.046)
    mag = straight_mag(b, 0.1, -0.05, 0.12, 0.03, 0.07, 3, POLY)
    vertical_grip(b, 0.31, -0.054)
    b.add(tube(0.016, 0.016, -0.28, -0.1, 0, -0.01, 18), METAL)
    butt = stock_sculpt(b, [(-0.22, 0.02, 0.03, -0.015), (-0.27, 0.021, 0.046, -0.025), (-0.29, 0.021, 0.046, -0.025)], POLY)
    holo_sight(b, 0.04, 0.034)
    laser_module(g.opt("laser"), 0.3, 0.0, -0.04)
    g.point("Muzzle", (0, 0.49, -0.004)); g.point("Grip", grip); g.point("SupportV", (0, 0.31, -0.095))
    g.point("Butt", butt); g.point("Mag", mag)


def monsoon(g):
    """Fusil à canons tournants : six canons en barillet, carter, gros tambour, poignée de transport haute."""
    b = g.body
    receiver(b, -0.12, 0.2, 0.03, -0.055, 0.03, PAINT, 5.0)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(tube(0.008, 0.008, 0.2, 0.6, math.cos(a) * 0.022, -0.01 + math.sin(a) * 0.022, 12), METAL)
    for uu in (0.22, 0.45, 0.6):
        b.add(ring(0.036, 0.008, uu - 0.01, uu + 0.01, 0, -0.01, 24), METAL)
    b.add(loft([(-0.06, 0.008, 0.012, 0.085, 3.0), (0.2, 0.008, 0.012, 0.085, 3.0)], 12), METAL)
    for uu in (-0.06, 0.2):
        b.add(box(0.016, 0.02, 0.06, (0, uu, 0.058), 0.003), METAL)
    grip = pistol_grip(b, -0.03, -0.055, POLY, 15, 0.1, 0.032, 0.046)
    mag = drum_mag(b, 0.08, -0.055, 0.08, 0.08, PAINT)
    butt = stock_sculpt(b, [(-0.12, 0.026, 0.045, -0.01), (-0.26, 0.025, 0.05, -0.016), (-0.36, 0.024, 0.054, -0.02), (-0.375, 0.023, 0.052, -0.02)], POLY)
    vertical_grip(b, 0.32, -0.04)
    g.point("Muzzle", (0, 0.6, -0.01)); g.point("Grip", grip); g.point("SupportV", (0, 0.32, -0.085))
    g.point("Butt", butt); g.point("Mag", mag)


def slugshot(g):
    """Fusil à balles : pompe, canon long rayé à frein, lunette basse, crosse sculptée."""
    b = g.body
    pump_body(b, -0.09, 0.17)
    b.add(tube(0.012, 0.012, 0.17, 0.74, 0, 0.004, 18), METAL)
    muzzle_brake(b, 0.74, 0.004, 0.017, 0.06, 2)
    b.add(tube(0.011, 0.011, 0.17, 0.56, 0, -0.022, 16), METAL)
    b.add(loft([(0.26, 0.024, 0.022, -0.016, 2.4), (0.44, 0.024, 0.022, -0.016, 2.4)], 20), PAINT)
    for k in range(5):
        b.add(ring(0.0255, 0.021, 0.28 + k * 0.032, 0.29 + k * 0.032, 0, -0.016, 22), PAINT)
    rail(b, -0.07, 0.15, 0.02)
    scope(b, 0.04, 0.074, 0.26, 0.014, 0.02, base=0.029)
    grip = pistol_grip(b, -0.05, -0.035, POLY, 20, 0.105, 0.03, 0.046)
    butt = stock_sculpt(b, [(-0.09, 0.018, 0.024, -0.008), (-0.16, 0.02, 0.032, -0.016), (-0.33, 0.022, 0.05, -0.03), (-0.35, 0.021, 0.05, -0.03)], PAINT)
    laser_module(g.opt("laser"), 0.38, -0.016, -0.034)
    g.point("Muzzle", (0, 0.8, 0.004)); g.point("Grip", grip); g.point("Support", (0, 0.35, -0.04))
    g.point("Butt", butt); g.point("Mag", (0, 0.12, -0.05))


def bustershot(g):
    """Buster Shot : fusil de très gros calibre (contenu téléchargeable), canon carré, double tube, cuve de munitions."""
    b = g.body
    receiver(b, -0.13, 0.24, 0.032, -0.06, 0.03, PAINT, 5.0)
    b.add(box(0.05, 0.36, 0.05, (0, 0.42, -0.004), 0.008), METAL)
    b.add(box(0.06, 0.04, 0.06, (0, 0.6, -0.004), 0.01), ACCENT)
    b.add(tube(0.02, 0.02, 0.6, 0.625, 0, -0.004, 22), POLY)
    for s in (-1, 1):
        b.add(box(0.006, 0.3, 0.012, (s * 0.027, 0.42, 0.012), 0.002), ACCENT)
    rail(b, -0.1, 0.22, 0.03)
    grip = pistol_grip(b, -0.03, -0.06, POLY, 15, 0.1, 0.034, 0.05)
    mag = straight_mag(b, 0.11, -0.06, 0.1, 0.05, 0.09, 0, ACCENT)
    butt = stock_sculpt(b, [(-0.13, 0.028, 0.045, -0.01), (-0.3, 0.027, 0.055, -0.018), (-0.38, 0.026, 0.056, -0.02), (-0.395, 0.025, 0.055, -0.02)], POLY)
    holo_sight(b, 0.05, 0.039)
    vertical_grip(b, 0.38, -0.03)
    g.point("Muzzle", (0, 0.63, -0.004)); g.point("Grip", grip); g.point("SupportV", (0, 0.38, -0.075))
    g.point("Butt", butt); g.point("Mag", mag)


def fracture(g):
    """Fracture : fusil à dispersion expérimental (contenu téléchargeable), bouche fendue en éventail, bobines d'énergie."""
    b = g.body
    b.add(loft([(-0.14, 0.024, 0.045, -0.01, 3.0), (-0.08, 0.032, 0.055, -0.008, 3.5), (0.22, 0.032, 0.05, -0.004, 3.5), (0.28, 0.024, 0.036, 0.0, 3.0)], 24), PAINT)
    for s in (-1, 0, 1):
        b.add(box(0.012, 0.3, 0.012, (s * 0.022, 0.42, 0.0), 0.003), METAL)
    for k in range(4):
        b.add(ring(0.04, 0.03, 0.3 + k * 0.07, 0.315 + k * 0.07, 0, 0.0, 24), ACCENT)
        b.add(ring(0.034, 0.031, 0.303 + k * 0.07, 0.312 + k * 0.07, 0, 0.0, 24), ENERGY)
    energy_cell(b, 0.05, -0.06, 0.0, 0.1, 0.014)
    rail(b, -0.08, 0.2, 0.05)
    grip = pistol_grip(b, -0.04, -0.055, POLY, 14)
    butt = stock_sculpt(b, [(-0.14, 0.024, 0.045, -0.01), (-0.3, 0.024, 0.052, -0.012), (-0.39, 0.023, 0.054, -0.012), (-0.405, 0.022, 0.052, -0.012)], PAINT)
    red_dot(b, 0.05, 0.059)
    g.point("Muzzle", (0, 0.58, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.24, -0.05))
    g.point("Butt", butt); g.point("Mag", (0, 0.05, -0.06))


# ───────────────────────── Fusils de précision ─────────────────────────

def bolt_receiver(b, u0, u1, mat=METAL):
    b.add(tube(0.017, 0.017, u0, u1, 0, 0.0, 22), mat)
    b.add(tube(0.008, 0.008, u0 + 0.02, u0 + 0.06, 0.026, 0.0, 12), METAL)          # levier de culasse
    b.add(sphere_ball(0.011, (0.04, u0 + 0.045, -0.012)), METAL)


def sphere_ball(r, c):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def kff(g):
    """À verrou (type AWM) : châssis vert, culasse ronde, canon lourd à frein, grosse lunette, crosse réglable, bipied."""
    b = g.body
    bolt_receiver(b, -0.08, 0.16)
    b.add(loft([(-0.1, 0.022, 0.03, -0.022, 4.0), (0.22, 0.024, 0.032, -0.024, 4.5), (0.4, 0.022, 0.028, -0.018, 4.0)], 22), PAINT)
    b.add(tube(0.012, 0.0105, 0.16, 0.86, 0, 0, 18), METAL)
    muzzle_brake(b, 0.86, 0, 0.018, 0.07, 3)
    rail(b, -0.06, 0.15, 0.017)
    scope(b, 0.05, 0.068, 0.36, 0.016, 0.026, base=0.026)
    grip = pistol_grip(b, -0.06, -0.045, PAINT, 12, 0.1, 0.03, 0.046)
    mag = straight_mag(b, 0.06, -0.05, 0.07, 0.03, 0.08, 0)
    butt = stock_sculpt(b, [(-0.1, 0.022, 0.03, -0.02), (-0.2, 0.023, 0.045, -0.03), (-0.37, 0.024, 0.06, -0.035), (-0.39, 0.023, 0.058, -0.035)], PAINT)
    b.add(loft([(-0.2, 0.022, 0.012, 0.024, 3.0), (-0.34, 0.022, 0.012, 0.024, 3.0)], 16), POLY)    # busc
    b.add(tube_x(0.015, 0.05, (0, -0.3, -0.105), 12), POLY)                           # monopode
    bipod(b, 0.38, -0.04, True)
    g.point("Muzzle", (0, 0.93, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.05))
    g.point("Butt", butt); g.point("Mag", mag)


def np(g):
    """Semi-auto de précision (type PSG-1) : boîtier rectangulaire, garde-main noir, lunette, crosse réglable à busc."""
    b = g.body
    receiver(b, -0.12, 0.2, 0.02, -0.04, 0.022, METAL, 6.0)
    b.add(loft([(0.2, 0.026, 0.034, -0.01, 3.5), (0.48, 0.024, 0.03, -0.008, 3.5)], 22), POLY)
    vents(b, 0.24, 0.45, -0.01, 0.025, 7, 0.02)
    b.add(tube(0.011, 0.01, 0.48, 0.82, 0, 0, 18), METAL)
    flash_hider(b, 0.82, 0, 0.013, 0.05)
    scope(b, 0.04, 0.07, 0.34, 0.016, 0.025, base=0.022)
    grip = pistol_grip(b, -0.04, -0.04, POLY, 12, 0.1, 0.032, 0.048)
    b.add(box(0.03, 0.03, 0.03, (0, -0.06, -0.07), 0.004), POLY)                      # repose-paume
    mag = straight_mag(b, 0.08, -0.04, 0.1, 0.026, 0.06, 6)
    butt = stock_sculpt(b, [(-0.12, 0.022, 0.03, -0.012), (-0.22, 0.023, 0.044, -0.024), (-0.38, 0.023, 0.058, -0.03), (-0.4, 0.022, 0.056, -0.03)], POLY)
    b.add(loft([(-0.18, 0.022, 0.012, 0.03, 3.0), (-0.34, 0.022, 0.012, 0.03, 3.0)], 16), POLY)
    laser_module(g.opt("laser"), 0.4, -0.008, -0.036)
    g.point("Muzzle", (0, 0.87, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.32, -0.045))
    g.point("Butt", butt); g.point("Mag", mag)


def lysander(g):
    """Anti-matériel (type M82) : grand boîtier en tôle, canon lourd et frein de bouche en tête de flèche, bipied, crosse à amortisseur."""
    b = g.body
    receiver(b, -0.24, 0.3, 0.024, -0.05, 0.03, PAINT, 7.0)
    b.add(loft([(0.3, 0.026, 0.034, -0.008, 4.0), (0.42, 0.024, 0.03, -0.006, 4.0)], 20), PAINT)
    vents(b, 0.3, 0.41, -0.004, 0.026, 5, 0.03)
    b.add(tube(0.016, 0.016, 0.42, 1.0, 0, 0, 20), METAL)
    b.add(prism([(1.0, -0.02), (1.08, -0.035), (1.11, -0.035), (1.11, 0.035), (1.08, 0.035), (1.0, 0.02)], -0.022, 0.022, 0.004), METAL)  # frein en flèche
    rail(b, -0.14, 0.28, 0.03)
    scope(b, 0.06, 0.08, 0.38, 0.017, 0.028, base=0.039)
    grip = pistol_grip(b, -0.06, -0.05, POLY, 12, 0.1, 0.032, 0.048)
    mag = straight_mag(b, 0.06, -0.05, 0.1, 0.032, 0.09, 0)
    b.add(box(0.04, 0.12, 0.08, (0, -0.3, -0.03), 0.01), POLY)                         # amortisseur de crosse
    b.add(box(0.044, 0.012, 0.11, (0, -0.37, -0.035), 0.005), RUBBER)
    b.add(loft([(-0.27, 0.02, 0.014, 0.022, 3.0), (-0.35, 0.02, 0.014, 0.022, 3.0)], 14), POLY)
    butt = (0, -0.375, -0.035)
    bipod(b, 0.4, -0.04, True)
    g.point("Muzzle", (0, 1.11, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.24, -0.06))
    g.point("Butt", butt); g.point("Mag", mag)


def fang(g):
    """Type SVD : long garde-main ventilé, boîtier étroit, crosse squelette à busc, lunette latérale, chargeur courbe court."""
    b = g.body
    receiver(b, -0.1, 0.17, 0.016, -0.04, 0.012, METAL, 6.0)
    b.add(loft([(-0.1, 0.015, 0.012, 0.016, 3.0), (0.17, 0.015, 0.012, 0.016, 3.0)], 18), METAL)
    b.add(loft([(0.18, 0.024, 0.026, -0.004, 2.6), (0.44, 0.022, 0.024, -0.004, 2.6)], 22), PAINT)
    vents(b, 0.2, 0.42, 0.004, 0.023, 6, 0.016)
    vents(b, 0.2, 0.42, 0.004, -0.023, 6, 0.016)
    b.add(tube(0.01, 0.009, 0.44, 0.88, 0, 0, 18), METAL)
    flash_hider(b, 0.88, 0, 0.012, 0.06)
    scope(b, 0.03, 0.07, 0.3, 0.015, 0.024, base=0.03)
    grip = (0, -0.1, -0.06)
    # Crosse squelette avec poignée intégrée (évidement).
    b.add(prism([(-0.1, -0.03), (-0.16, -0.11), (-0.4, -0.06), (-0.4, 0.03), (-0.36, 0.03), (-0.12, 0.02)], -0.012, 0.012, 0.004), PAINT)
    b.add(prism([(-0.14, -0.03), (-0.17, -0.085), (-0.33, -0.05), (-0.33, 0.0), (-0.17, 0.0)], -0.014, 0.014, 0.003), POLY)
    b.add(loft([(-0.24, 0.016, 0.012, 0.04, 3.0), (-0.36, 0.016, 0.012, 0.04, 3.0)], 14), PAINT)
    mag = curved_mag(b, 0.06, -0.04, 0.1, 0.024, 0.07, 0.02)
    butt = (0, -0.4, -0.015)
    g.point("Muzzle", (0, 0.94, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.03))
    g.point("Butt", butt); g.point("Mag", mag)


def dunkel(g):
    """Bullpup de précision (type WA2000) : bâti en poutre, canon cerclé, chargeur derrière la poignée, crosse ajourée."""
    b = g.body
    receiver(b, -0.32, 0.1, 0.024, -0.05, 0.02, METAL, 6.0)
    b.add(loft([(0.1, 0.022, 0.016, 0.03, 3.0), (0.5, 0.02, 0.014, 0.028, 3.0)], 18), PAINT)       # poutre supérieure
    b.add(tube(0.012, 0.011, 0.1, 0.6, 0, 0, 18), METAL)
    for uu in (0.2, 0.32, 0.44):
        b.add(ring(0.016, 0.011, uu, uu + 0.01, 0, 0, 18), METAL)
    muzzle_brake(b, 0.6, 0, 0.017, 0.06, 2)
    b.add(box(0.03, 0.04, 0.04, (0, 0.48, 0.018), 0.005), PAINT)
    scope(b, -0.08, 0.075, 0.32, 0.016, 0.025, base=0.03)
    grip = pistol_grip(b, 0.06, -0.05, PAINT, 12, 0.1, 0.03, 0.046)
    b.add(box(0.016, 0.1, 0.05, (0, 0.06, -0.07), 0.01), PAINT)
    mag = straight_mag(b, -0.1, -0.05, 0.1, 0.026, 0.06, 0)
    b.add(prism([(-0.32, 0.02), (-0.32, -0.06), (-0.24, -0.1), (-0.2, -0.05)], -0.018, 0.018, 0.004), PAINT)
    b.add(box(0.04, 0.012, 0.12, (0, -0.33, -0.03), 0.005), RUBBER)
    butt = (0, -0.335, -0.03)
    g.point("Muzzle", (0, 0.66, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, 0.0))
    g.point("Butt", butt); g.point("Mag", mag)


def eagle(g):
    """Semi-auto de précision (type M110) : AR allongé, garde-main flottant à rails, réducteur de son, lunette."""
    b = g.body
    receiver(b, -0.1, 0.18, 0.017, -0.018, 0.022, METAL)
    receiver(b, -0.09, 0.12, 0.018, -0.052, -0.018, METAL)
    b.add(loft([(0.075 - 0.04, 0.018, 0.017, -0.064, 5.0), (0.075 + 0.04, 0.018, 0.017, -0.064, 5.0)], 18), METAL)
    rail(b, -0.09, 0.56, 0.022)
    receiver(b, 0.18, 0.56, 0.026, -0.03, 0.022, PAINT, 6.0)
    vents(b, 0.21, 0.54, -0.004, 0.026, 9, 0.024)
    b.add(tube(0.01, 0.01, 0.56, 0.68, 0, 0, 16), METAL)
    suppressor(b, 0.68, 0, 0.021, 0.18)
    scope(b, 0.05, 0.075, 0.32, 0.016, 0.025, base=0.031)
    grip = pistol_grip(b, -0.04, -0.052, POLY, 20)
    mag = straight_mag(b, 0.075, -0.075, 0.1, 0.026, 0.07, 4)
    b.add(tube(0.016, 0.016, -0.28, -0.1, 0, -0.008, 18), METAL)
    butt = stock_sculpt(b, [(-0.2, 0.02, 0.03, -0.012), (-0.26, 0.021, 0.046, -0.022), (-0.34, 0.021, 0.052, -0.028), (-0.355, 0.02, 0.05, -0.028)], PAINT)
    bipod(b, 0.5, -0.03, True)
    g.point("Muzzle", (0, 0.86, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.36, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


def flounders(g):
    """Tir en rafale : boîtier large à double chargeur côte à côte, canon court fileté, lunette compacte."""
    b = g.body
    receiver(b, -0.12, 0.22, 0.03, -0.05, 0.024, PAINT, 6.0)
    rail(b, -0.1, 0.2, 0.024)
    b.add(loft([(0.22, 0.026, 0.028, -0.01, 3.5), (0.46, 0.024, 0.026, -0.008, 3.5)], 20), POLY)
    b.add(tube(0.012, 0.011, 0.46, 0.76, 0, 0, 18), METAL)
    muzzle_brake(b, 0.76, 0, 0.017, 0.06, 3)
    for s in (-1, 1):
        part_mag = Part()
        straight_mag(part_mag, 0.08, -0.05, 0.11, 0.022, 0.06, 4)
        bmesh.ops.translate(part_mag.bm, vec=Vector((s * 0.014, 0, 0)), verts=part_mag.bm.verts)
        me = bpy.data.meshes.new("t")
        part_mag.bm.to_mesh(me)
        b.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    scope(b, 0.04, 0.072, 0.26, 0.015, 0.023, base=0.033)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 15)
    butt = stock_sculpt(b, [(-0.12, 0.024, 0.036, -0.01), (-0.24, 0.024, 0.048, -0.02), (-0.37, 0.023, 0.055, -0.026), (-0.385, 0.022, 0.053, -0.026)], PAINT)
    g.point("Muzzle", (0, 0.82, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.34, -0.04))
    g.point("Butt", butt); g.point("Mag", (0, 0.08, -0.1))


def ptarmigan(g):
    """Anti-matériel lourd : boîtier en caisson, canon très épais à frein double, gros bipied, lunette longue, crosse à poignée arrière."""
    b = g.body
    receiver(b, -0.22, 0.32, 0.03, -0.06, 0.03, ACCENT, 6.0)
    b.add(tube(0.02, 0.019, 0.32, 1.02, 0, 0, 22), METAL)
    b.add(tube(0.03, 0.03, 0.32, 0.5, 0, 0, 22), PAINT)                                # manchon
    for k in range(2):
        muzzle_brake(b, 1.02 + k * 0.07, 0, 0.026, 0.065, 2)
    rail(b, -0.18, 0.3, 0.03)
    scope(b, 0.05, 0.085, 0.42, 0.018, 0.03, base=0.039)
    grip = pistol_grip(b, -0.06, -0.06, POLY, 12, 0.1, 0.034, 0.05)
    mag = straight_mag(b, 0.08, -0.06, 0.09, 0.036, 0.1, 0)
    b.add(box(0.044, 0.16, 0.1, (0, -0.3, -0.03), 0.012), PAINT)
    b.add(box(0.05, 0.014, 0.12, (0, -0.385, -0.035), 0.005), RUBBER)
    b.add(prism([(-0.3, -0.08), (-0.26, -0.08), (-0.27, -0.15), (-0.3, -0.15)], -0.014, 0.014, 0.004), POLY)    # poignée arrière
    butt = (0, -0.39, -0.035)
    bipod(b, 0.45, -0.04, False)
    g.point("Muzzle", (0, 1.16, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.24, -0.07))
    g.point("Butt", butt); g.point("Mag", mag)


def pylon(g):
    """Expérimental : fût carré à ailettes de refroidissement, lunette numérique carrée, crosse ajourée."""
    b = g.body
    b.add(prism([(-0.14, 0.03), (0.3, 0.03), (0.34, 0.0), (0.3, -0.045), (-0.14, -0.05)], -0.024, 0.024, 0.005), PAINT)
    b.add(box(0.028, 0.5, 0.028, (0, 0.58, -0.006), 0.004), METAL)
    for k in range(10):
        b.add(box(0.044, 0.006, 0.044, (0, 0.38 + k * 0.04, -0.006), 0.002), ACCENT)
    b.add(box(0.034, 0.03, 0.034, (0, 0.85, -0.006), 0.005), ACCENT)
    b.add(box(0.03, 0.12, 0.034, (0, 0.06, 0.06), 0.005), METAL)                      # lunette numérique
    b.add(box(0.026, 0.002, 0.028, (0, 0.121, 0.06), 0.0005, 1), LENS)
    b.add(box(0.012, 0.03, 0.03, (0, 0.06, 0.04), 0.003), METAL)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 14)
    mag = straight_mag(b, 0.1, -0.05, 0.09, 0.028, 0.07, 0, ACCENT)
    b.add(prism([(-0.14, 0.03), (-0.4, 0.02), (-0.4, -0.07), (-0.3, -0.07), (-0.2, -0.03), (-0.14, -0.05)], -0.018, 0.018, 0.004), PAINT)
    b.add(box(0.04, 0.012, 0.1, (0, -0.405, -0.025), 0.005), RUBBER)
    butt = (0, -0.41, -0.025)
    g.point("Muzzle", (0, 0.87, -0.006)); g.point("Grip", grip); g.point("Support", (0, 0.25, -0.05))
    g.point("Butt", butt); g.point("Mag", mag)


def penetrator(g):
    """Fusil électromagnétique perforant : deux rails conducteurs, condensateurs, carénage, lunette, cellules d'énergie."""
    b = g.body
    b.add(loft([(-0.16, 0.024, 0.045, -0.012, 3.0), (-0.1, 0.032, 0.055, -0.01, 3.5), (0.24, 0.032, 0.05, -0.006, 3.5), (0.3, 0.026, 0.038, -0.004, 3.0)], 24), PAINT)
    for s in (-1, 1):
        b.add(box(0.01, 0.62, 0.03, (s * 0.018, 0.6, 0.0), 0.003), METAL)
        b.add(box(0.004, 0.58, 0.012, (s * 0.012, 0.6, 0.0), 0.001), ENERGY)
    for k in range(5):
        b.add(box(0.06, 0.012, 0.012, (0, 0.36 + k * 0.12, 0.024), 0.003), ACCENT)
        b.add(box(0.06, 0.012, 0.012, (0, 0.36 + k * 0.12, -0.024), 0.003), ACCENT)
    for s in (-1, 1):
        energy_cell(b, 0.05, -0.03, s * 0.04, 0.16, 0.012)
    scope(b, 0.02, 0.09, 0.3, 0.016, 0.024, base=0.05)
    grip = pistol_grip(b, -0.05, -0.05, POLY, 14, 0.1, 0.032, 0.046)
    butt = stock_sculpt(b, [(-0.16, 0.024, 0.045, -0.012), (-0.3, 0.024, 0.052, -0.015), (-0.42, 0.023, 0.055, -0.016), (-0.435, 0.022, 0.053, -0.016)], PAINT)
    g.point("Muzzle", (0, 0.92, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.26, -0.05))
    g.point("Butt", butt); g.point("Mag", (0, 0.05, -0.04))


PALETTE = {
    "slaughter": ((0.05, 0.05, 0.05), (0.2, 0.2, 0.2)), "sgn": ((0.18, 0.2, 0.16), (0.05, 0.05, 0.05)),
    "herring": ((0.28, 0.16, 0.08), (0.05, 0.05, 0.05)), "breecher": ((0.32, 0.27, 0.18), (0.05, 0.05, 0.05)),
    "monsoon": ((0.14, 0.16, 0.18), (0.05, 0.05, 0.05)), "slugshot": ((0.08, 0.09, 0.07), (0.05, 0.05, 0.05)),
    "bustershot": ((0.36, 0.08, 0.05), (0.55, 0.45, 0.1)), "fracture": ((0.7, 0.72, 0.75), (0.15, 0.2, 0.3)),
    "kff": ((0.16, 0.19, 0.13), (0.05, 0.05, 0.05)), "np": ((0.05, 0.05, 0.05), (0.2, 0.2, 0.2)),
    "lysander": ((0.13, 0.14, 0.12), (0.05, 0.05, 0.05)), "fang": ((0.3, 0.17, 0.08), (0.05, 0.05, 0.05)),
    "dunkel": ((0.22, 0.2, 0.17), (0.05, 0.05, 0.05)), "eagle": ((0.32, 0.27, 0.18), (0.05, 0.05, 0.05)),
    "flounders": ((0.1, 0.12, 0.14), (0.05, 0.05, 0.05)), "ptarmigan": ((0.15, 0.17, 0.13), (0.2, 0.21, 0.2)),
    "pylon": ((0.6, 0.62, 0.65), (0.12, 0.13, 0.15)), "penetrator": ((0.12, 0.13, 0.15), (0.6, 0.3, 0.05)),
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
