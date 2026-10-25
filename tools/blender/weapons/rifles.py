# Fusils d'assaut du Ranger (EDF 5), une fonction par famille de modèle. Inspirations (IMFDB) : PA-11 = M16A4 / T65K2,
# M1 Raven = Colt 933 accessoirisé, Minion Buster = G36KA4 stylisé (viseur holographique, lampe, poignée inclinée),
# Slugger NN / X-Orchid = AK-12 prototype (chargeur courbe, crosse RPK), G&M = Type 89 (bipied, chargeur 20 coups),
# G&M-A25/A29 = Type 64, T1-T3 Stork = FAMAS F1 « débullpupé » × F2000, T4/T5/TZ Stork = Tavor (bullpup) garde-main
# Manticore ; Slade, Blazer (contenus téléchargeables) : armes expérimentales futuristes.
# Usage : exec(kit.py) puis exec(rifles.py) dans Blender ; FAMILY = "pa11"… ; OUT = chemin du .glb (facultatif).
import math

K = globals()


def pa11(g):
    """M16A4 / T65K2 : boîtiers supérieur et inférieur, poignée de transport fixe, garde-main rond à rainures, crosse A2."""
    b = g.body
    # Boîtier supérieur : flancs adoucis, rail sur le dessus, fenêtre d'éjection, assistance de fermeture, déflecteur.
    receiver(b, -0.1, 0.17, 0.0165, -0.018, 0.021)
    rail(b, -0.095, 0.165, 0.021)
    b.add(box(0.004, 0.06, 0.018, (0.0165, 0.035, 0.002), 0.001), POLY)               # fenêtre d'éjection
    b.add(box(0.003, 0.064, 0.02, (0.0185, 0.035, 0.002), 0.0008), METAL)            # volet ouvert
    b.add(box(0.012, 0.02, 0.016, (0.02, -0.012, 0.004), 0.003), METAL)               # déflecteur d'étuis
    b.add(tube(0.0075, 0.0075, -0.06, -0.02, 0.021, 0.002, 12), METAL)                # assistance de fermeture
    b.add(tube(0.0085, 0.0085, -0.02, -0.014, 0.021, 0.002, 12), METAL)
    b.add(box(0.04, 0.01, 0.007, (0, -0.104, 0.012), 0.002), METAL)                   # levier d'armement en T
    # Boîtier inférieur : puits de chargeur évasé, pontet, sélecteur, arrêtoir, goupilles.
    receiver(b, -0.09, 0.115, 0.0175, -0.05, -0.018)
    b.add(loft([(0.032, 0.017, 0.016, -0.06, 5.0), (0.108, 0.017, 0.016, -0.06, 5.0)], 18), METAL)
    b.add(loft([(0.03, 0.019, 0.006, -0.077, 5.0), (0.11, 0.019, 0.006, -0.077, 5.0)], 18), METAL)   # évasement
    b.add(tube_x(0.0055, 0.004, (0.019, 0.075, -0.035), 12), METAL)                  # arrêtoir de chargeur
    b.add(box(0.004, 0.02, 0.006, (0.0195, -0.05, -0.03), 0.001), METAL)             # sélecteur
    b.add(tube_x(0.005, 0.004, (0.0195, -0.042, -0.03), 12), METAL)
    pins(b, (-0.075, 0.1), -0.03, 0.0175)
    b.add(box(0.004, 0.003, 0.02, (0, 0.0, -0.06), 0.0008, 1), METAL)                # détente
    # Poignée de transport T65K2 : arche sur le rail, hausse à œilleton à l'arrière.
    for uu in (-0.065, 0.13):
        b.add(prism([(uu - 0.02, 0.03), (uu + 0.02, 0.03), (uu + 0.012, 0.058), (uu - 0.012, 0.058)], -0.009, 0.009, 0.002), METAL)
    b.add(loft([(-0.08, 0.009, 0.006, 0.062, 4.0), (0.145, 0.009, 0.006, 0.062, 4.0)], 14), METAL)
    b.add(box(0.022, 0.022, 0.016, (0, -0.06, 0.076), 0.003), METAL)
    b.add(tube(0.003, 0.003, -0.072, -0.048, 0, 0.082, 8), POLY)                      # œilleton
    b.add(tube_x(0.007, 0.006, (0.014, -0.06, 0.072), 12), METAL)                     # tambour de hausse
    # Garde-main rond à rainures (A2), bague, embase de guidon triangulaire, canon, cache-flammes.
    b.add(loft([(0.18, 0.03, 0.03, 0.0, 2.0), (0.19, 0.034, 0.034, 0.0, 2.0), (0.44, 0.032, 0.032, 0.0, 2.0), (0.452, 0.027, 0.027, 0.0, 2.0)], 26), PAINT)
    for k in range(10):
        uu = 0.198 + k * 0.024
        b.add(ring(0.0346, 0.03, uu, uu + 0.008, 0, 0.0, 26), PAINT)
    b.add(ring(0.037, 0.012, 0.165, 0.182, 0, 0.0, 26), METAL)
    b.add(tube(0.0098, 0.0098, 0.44, 0.52, 0, 0, 18), METAL)
    b.add(tube(0.0088, 0.0088, 0.52, 0.66, 0, 0, 18), METAL)
    b.add(prism([(0.49, -0.012), (0.528, -0.012), (0.522, 0.012), (0.512, 0.062), (0.502, 0.062), (0.494, 0.012)], -0.009, 0.009, 0.002), METAL)
    b.add(box(0.018, 0.034, 0.024, (0, 0.508, -0.014), 0.003), METAL)                # bloc d'emprunt de gaz
    b.add(box(0.008, 0.024, 0.01, (0, 0.512, -0.03), 0.002), METAL)                 # tenon de baïonnette
    b.add(tube_x(0.006, 0.012, (0, 0.5, -0.034), 10), METAL)                         # anneau de bretelle avant
    flash_hider(b, 0.66, 0, 0.0115, 0.055)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 22, 0.105, 0.03, 0.044)
    mag = curved_mag(b, 0.07, -0.08, 0.19, 0.022, 0.058, 0.04)
    # Crosse A2 sculptée, tube et porte-grenadière.
    butt = stock_sculpt(b, [(-0.1, 0.017, 0.02, -0.006), (-0.14, 0.019, 0.028, -0.012), (-0.27, 0.021, 0.044, -0.025),
                            (-0.37, 0.021, 0.053, -0.03), (-0.385, 0.02, 0.052, -0.03)], PAINT)
    b.add(box(0.004, 0.06, 0.03, (0.021, -0.32, -0.03), 0.0015), POLY)               # trappe de crosse
    b.add(tube_x(0.006, 0.03, (0, -0.36, -0.08), 10), METAL)                          # battant de bretelle
    laser_module(g.opt("laser"), 0.36, -0.005, -0.042)
    scope(g.opt("scope"), 0.04, 0.09, 0.2, 0.012, 0.018, base=0.068)
    g.point("Muzzle", (0, 0.715, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.03))
    g.point("Butt", butt); g.point("Mag", mag)


def ar_lower(b, u0=-0.09, u1=0.115, mag_u=0.07):
    """Boîtier inférieur type AR (puits de chargeur, pontet, sélecteur, goupilles) commun au PA-11 et au Raven."""
    receiver(b, u0, u1, 0.0175, -0.05, -0.018)
    b.add(loft([(mag_u - 0.038, 0.017, 0.016, -0.06, 5.0), (mag_u + 0.038, 0.017, 0.016, -0.06, 5.0)], 18), METAL)
    b.add(loft([(mag_u - 0.04, 0.019, 0.006, -0.077, 5.0), (mag_u + 0.04, 0.019, 0.006, -0.077, 5.0)], 18), METAL)
    b.add(tube_x(0.0055, 0.004, (0.019, mag_u + 0.005, -0.035), 12), METAL)
    b.add(box(0.004, 0.02, 0.006, (0.0195, -0.05, -0.03), 0.001), METAL)
    b.add(tube_x(0.005, 0.004, (0.0195, -0.042, -0.03), 12), METAL)
    pins(b, (u0 + 0.015, u1 - 0.015), -0.03, 0.0175)
    b.add(box(0.004, 0.003, 0.02, (0, 0.0, -0.06), 0.0008, 1), METAL)


def ar_upper(b, u0=-0.1, u1=0.17):
    receiver(b, u0, u1, 0.0165, -0.018, 0.021)
    rail(b, u0 + 0.005, u1 - 0.005, 0.021)
    b.add(box(0.004, 0.06, 0.018, (0.0165, 0.035, 0.002), 0.001), POLY)
    b.add(box(0.003, 0.064, 0.02, (0.0185, 0.035, 0.002), 0.0008), METAL)
    b.add(box(0.012, 0.02, 0.016, (0.02, -0.012, 0.004), 0.003), METAL)
    b.add(tube(0.0075, 0.0075, -0.06, -0.02, 0.021, 0.002, 12), METAL)
    b.add(box(0.04, 0.01, 0.007, (0, u0 - 0.004, 0.012), 0.002), METAL)


def raven(g):
    """Colt 933 accessoirisé : carabine très courte, garde-main quadrirail, chargeur fin, crosse télescopique, point rouge."""
    b = g.body
    ar_upper(b, -0.1, 0.17)
    ar_lower(b, mag_u=0.065)
    # Garde-main quadrirail court, rails latéraux et inférieur, canon court.
    receiver(b, 0.17, 0.36, 0.024, -0.026, 0.021, POLY, 6.0)
    rail(b, 0.18, 0.355, 0.0, x=0.024, mat=POLY, side=True)
    rail(b, 0.18, 0.355, 0.0, x=-0.024, mat=POLY, side=True)
    rail(b, 0.18, 0.355, 0.021, mat=POLY)
    b.add(ring(0.03, 0.012, 0.165, 0.176, 0, 0.0, 24), METAL)
    b.add(tube(0.0085, 0.0085, 0.36, 0.43, 0, 0, 16), METAL)
    b.add(prism([(0.37, -0.012), (0.398, -0.012), (0.394, 0.012), (0.386, 0.05), (0.378, 0.05), (0.374, 0.012)], -0.008, 0.008, 0.002), METAL)
    flash_hider(b, 0.43, 0, 0.011, 0.05)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 22, 0.105, 0.03, 0.044)
    mag = straight_mag(b, 0.065, -0.08, 0.15, 0.021, 0.056, 9)
    b.add(tube(0.015, 0.015, -0.27, -0.1, 0, -0.008, 18), METAL)                      # tube tampon
    butt = stock_sculpt(b, [(-0.2, 0.019, 0.026, -0.012), (-0.24, 0.02, 0.04, -0.022), (-0.3, 0.02, 0.048, -0.028), (-0.31, 0.02, 0.048, -0.028)], PAINT)
    b.add(box(0.006, 0.08, 0.012, (0, -0.24, -0.06), 0.002), PAINT)                  # levier de crosse
    red_dot(b, 0.03, 0.03)
    vertical_grip(b, 0.29, -0.026)
    flashlight(b, 0.32, 0.0, 0.034)
    laser_module(g.opt("laser"), 0.27, 0.0, -0.038)
    scope(g.opt("scope"), 0.06, 0.08, 0.18, 0.012, 0.017, base=0.03)
    g.point("Muzzle", (0, 0.48, 0.0)); g.point("Grip", grip); g.point("SupportV", (0, 0.29, -0.065))
    g.point("Butt", butt); g.point("Mag", mag)


def minion(g):
    """G36KA4 stylisé : carcasse polymère massive, rail haut, garde-main ventilé, gros calibre explosif."""
    b = g.body
    receiver(b, -0.13, 0.22, 0.026, -0.058, 0.03, PAINT, 5.0)
    b.add(loft([(0.22, 0.028, 0.034, -0.012, 4.5), (0.42, 0.028, 0.034, -0.012, 4.5), (0.44, 0.024, 0.028, -0.008, 4.0)], 22), PAINT)
    vents(b, 0.25, 0.41, -0.012, 0.028, 6, 0.03, POLY)
    vents(b, 0.25, 0.41, -0.012, -0.028, 6, 0.03, POLY)
    rail(b, 0.24, 0.42, -0.046, w=0.022, mat=POLY)
    rail(b, -0.11, 0.21, 0.03)
    b.add(box(0.004, 0.07, 0.024, (0.026, 0.06, 0.004), 0.001), POLY)                # fenêtre d'éjection
    b.add(box(0.008, 0.012, 0.05, (0.024, 0.15, 0.005), 0.002), METAL)               # levier d'armement relevé
    b.add(box(0.06, 0.02, 0.008, (0, 0.15, 0.033), 0.003), METAL)
    b.add(tube(0.013, 0.013, 0.43, 0.56, 0, -0.004, 18), METAL)
    muzzle_brake(b, 0.56, -0.004, 0.021, 0.075, 3)
    grip = pistol_grip(b, -0.04, -0.058, POLY, 15, 0.1, 0.034, 0.048)
    b.add(box(0.012, 0.11, 0.05, (0, 0.0, -0.075), 0.008), PAINT)                    # pontet intégral
    mag = straight_mag(b, 0.1, -0.058, 0.13, 0.032, 0.075, 3, POLY)
    b.add(box(0.034, 0.012, 0.024, (0, 0.1, -0.06), 0.002), METAL)
    butt = stock_sculpt(b, [(-0.13, 0.022, 0.035, -0.012), (-0.2, 0.023, 0.04, -0.018), (-0.33, 0.023, 0.05, -0.02), (-0.36, 0.022, 0.05, -0.02)], POLY)
    b.add(box(0.05, 0.08, 0.03, (0, -0.25, -0.02), 0.004), POLY)                      # évidement de crosse (squelette)
    holo_sight(b, 0.02, 0.043)
    angled_grip(b, 0.36, -0.054)
    flashlight(b, 0.39, -0.012, 0.038)
    laser_module(g.opt("laser"), 0.34, -0.012, -0.04)
    g.point("Muzzle", (0, 0.64, -0.004)); g.point("Grip", grip); g.point("Support", (0, 0.36, -0.07))
    g.point("Butt", butt); g.point("Mag", mag)


def slugger(g, futuristic=False):
    """AK-12 prototype : boîtier à couvercle et rail, levier d'armement, chargeur courbe, crosse RPK, frein de bouche."""
    b = g.body
    rec = ACCENT if futuristic else METAL
    receiver(b, -0.12, 0.17, 0.018, -0.052, 0.006, rec, 6.0)
    b.add(loft([(-0.115, 0.016, 0.014, 0.012, 3.0), (0.17, 0.016, 0.014, 0.012, 3.0)], 20), rec)         # couvercle bombé
    rail(b, -0.1, 0.16, 0.024)
    b.add(box(0.006, 0.12, 0.022, (0.019, 0.03, -0.026), 0.001), METAL)              # levier de sûreté (AK)
    b.add(box(0.012, 0.012, 0.012, (0.024, 0.11, 0.0), 0.003), METAL)               # levier d'armement
    b.add(box(0.004, 0.08, 0.014, (0.018, 0.08, -0.004), 0.001), POLY)              # fenêtre d'éjection
    b.add(loft([(0.17, 0.026, 0.032, -0.012, 3.5), (0.39, 0.024, 0.03, -0.012, 3.5), (0.4, 0.02, 0.024, -0.01, 3.0)], 22), PAINT)
    rail(b, 0.19, 0.38, -0.012, x=0.026, mat=POLY, side=True)
    rail(b, 0.19, 0.38, -0.012, x=-0.026, mat=POLY, side=True)
    b.add(tube(0.011, 0.011, 0.16, 0.4, 0, 0.03, 14), METAL)                          # tube de gaz
    b.add(tube(0.0095, 0.0095, 0.39, 0.6, 0, 0, 16), METAL)
    b.add(box(0.02, 0.03, 0.05, (0, 0.42, 0.012), 0.004), METAL)                    # bloc de gaz
    front_sight(b, 0.575, 0.006)
    muzzle_brake(b, 0.6, 0, 0.016, 0.07, 3)
    grip = pistol_grip(b, -0.045, -0.052, POLY, 20)
    mag = curved_mag(b, 0.08, -0.05, 0.2, 0.026, 0.066, 0.08, PAINT if futuristic else METAL)
    butt = stock_sculpt(b, [(-0.12, 0.02, 0.026, -0.016), (-0.17, 0.02, 0.032, -0.026), (-0.32, 0.021, 0.048, -0.045),
                            (-0.4, 0.021, 0.06, -0.05), (-0.415, 0.02, 0.058, -0.05)], PAINT)
    if futuristic:
        energy_cell(b, 0.29, -0.042, 0.0, 0.14, 0.008)
        b.add(box(0.034, 0.1, 0.022, (0, -0.02, 0.043), 0.005), PAINT)
        b.add(box(0.036, 0.004, 0.024, (0, 0.03, 0.043), 0.001), ENERGY)
        red_dot(b, 0.07, 0.03)
    laser_module(g.opt("laser"), 0.32, -0.012, -0.042)
    scope(g.opt("scope"), 0.04, 0.085, 0.2, 0.012, 0.017, base=0.033)
    g.point("Muzzle", (0, 0.67, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.045))
    g.point("Butt", butt); g.point("Mag", mag)


def orchid(g):
    slugger(g, True)


def gm(g, type64=False):
    """Type 89 / Type 64 : fusil de tireur d'élite léger, chargeur 20 coups, bipied, lunette, garde-main plein."""
    b = g.body
    receiver(b, -0.11, 0.17, 0.0165, -0.018, 0.02, METAL)
    receiver(b, -0.09, 0.12, 0.0175, -0.052, -0.018, METAL)
    b.add(loft([(0.035, 0.017, 0.016, -0.062, 5.0), (0.105, 0.017, 0.016, -0.062, 5.0)], 18), METAL)
    pins(b, (-0.07, 0.1), -0.034, 0.0175)
    b.add(box(0.004, 0.06, 0.016, (0.0165, 0.04, 0.0), 0.001), POLY)
    b.add(box(0.012, 0.012, 0.012, (0.02, 0.1, 0.004), 0.003), METAL)
    if type64:
        b.add(loft([(-0.11, 0.015, 0.008, 0.026, 3.0), (0.16, 0.015, 0.008, 0.026, 3.0)], 18), METAL)
    else:
        rail(b, -0.1, 0.16, 0.02)
    b.add(loft([(0.17, 0.028, 0.032, -0.006, 2.6), (0.18, 0.031, 0.035, -0.006, 2.6), (0.46, 0.029, 0.031, -0.006, 2.6), (0.475, 0.024, 0.026, -0.004, 2.4)], 24), PAINT)
    vents(b, 0.21, 0.44, 0.006, 0.029, 8, 0.012)
    vents(b, 0.21, 0.44, 0.006, -0.029, 8, 0.012)
    b.add(tube(0.0095, 0.0095, 0.47, 0.74, 0, 0, 18), METAL)
    if type64:
        for k in range(6):
            a = 2 * math.pi * k / 6
            b.add(box(0.002, 0.16, 0.002, (math.cos(a) * 0.0095, 0.6, math.sin(a) * 0.0095), 0.0005, 1), POLY)
        muzzle_brake(b, 0.74, 0, 0.017, 0.065, 2)
    else:
        flash_hider(b, 0.74)
    b.add(prism([(0.6, -0.01), (0.63, -0.01), (0.624, 0.012), (0.616, 0.055), (0.608, 0.055), (0.604, 0.012)], -0.008, 0.008, 0.002), METAL)
    bipod(b, 0.49, -0.034, True)
    grip = pistol_grip(b, -0.04, -0.052, PAINT if type64 else POLY, 20)
    mag = straight_mag(b, 0.07, -0.075, 0.12, 0.024, 0.06, 6)
    butt = stock_sculpt(b, [(-0.11, 0.017, 0.022, -0.008), (-0.15, 0.019, 0.03, -0.014), (-0.3, 0.021, 0.045, -0.026),
                            (-0.4, 0.021, 0.055, -0.03), (-0.415, 0.02, 0.054, -0.03)], PAINT)
    if type64:
        b.add(loft([(-0.24, 0.018, 0.012, 0.03, 3.0), (-0.36, 0.018, 0.012, 0.03, 3.0)], 16), PAINT)  # appui-joue
    scope(b, 0.03, 0.078, 0.26, 0.014, 0.021, base=0.03 if not type64 else 0.034)
    laser_module(g.opt("laser"), 0.38, -0.01, -0.04)
    g.point("Muzzle", (0, 0.81, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.32, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


def gm64(g):
    gm(g, True)


def stork(g):
    """FAMAS F1 « débullpupé » × F2000 : carénage enveloppant, grande poignée de transport ajourée, viseur intégré."""
    b = g.body
    b.add(loft([(-0.13, 0.02, 0.038, -0.02, 2.8), (-0.11, 0.028, 0.05, -0.016, 3.2), (0.2, 0.031, 0.052, -0.012, 3.4),
                (0.38, 0.027, 0.04, -0.008, 3.2), (0.44, 0.02, 0.026, -0.004, 2.8)], 26), PAINT)
    vents(b, 0.24, 0.38, -0.024, 0.03, 5, 0.022, POLY)
    vents(b, 0.24, 0.38, -0.024, -0.03, 5, 0.022, POLY)
    b.add(box(0.004, 0.07, 0.018, (0.031, 0.03, 0.006), 0.001), POLY)                # éjection
    b.add(box(0.006, 0.05, 0.004, (0.032, 0.03, 0.026), 0.001), ACCENT)             # déflecteur
    # Poignée de transport ajourée (FAMAS) avec optique intégrée et guidon.
    b.add(loft([(-0.06, 0.01, 0.007, 0.085, 3.0), (0.31, 0.01, 0.007, 0.085, 3.0)], 14), ACCENT)
    for uu in (-0.03, 0.28):
        b.add(prism([(uu - 0.03, 0.03), (uu + 0.03, 0.03), (uu + 0.012, 0.082), (uu - 0.012, 0.082)], -0.009, 0.009, 0.003), ACCENT)
    b.add(tube(0.012, 0.012, 0.06, 0.18, 0, 0.1, 18), METAL)
    b.add(tube(0.011, 0.011, 0.179, 0.181, 0, 0.1, 18), LENS)
    b.add(box(0.008, 0.014, 0.018, (0, 0.3, 0.1), 0.002), ACCENT)
    b.add(tube(0.009, 0.009, 0.43, 0.6, 0, 0, 16), METAL)
    flash_hider(b, 0.6, 0, 0.012, 0.05)
    grip = pistol_grip(b, -0.03, -0.062, POLY, 12, 0.1, 0.03, 0.048)
    b.add(box(0.012, 0.13, 0.05, (0, 0.0, -0.08), 0.008), POLY)                       # pontet intégral
    mag = straight_mag(b, 0.08, -0.062, 0.14, 0.024, 0.058, 4)
    butt = stock_sculpt(b, [(-0.13, 0.02, 0.04, -0.02), (-0.2, 0.022, 0.05, -0.024), (-0.36, 0.022, 0.056, -0.028), (-0.38, 0.021, 0.055, -0.028)], PAINT)
    b.add(loft([(-0.17, 0.023, 0.014, -0.02, 3.0), (-0.32, 0.023, 0.014, -0.022, 3.0)], 16), POLY)  # évidement
    laser_module(g.opt("laser"), 0.32, -0.025, -0.038)
    scope(g.opt("scope"), 0.12, 0.13, 0.18, 0.011, 0.016, base=0.092)
    g.point("Muzzle", (0, 0.65, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.32, -0.05))
    g.point("Butt", butt); g.point("Mag", mag)


def stork4(g):
    """Tavor (bullpup) à garde-main Manticore : chargeur derrière la poignée, crosse courte et haute, rail continu."""
    b = g.body
    b.add(loft([(-0.34, 0.026, 0.052, -0.03, 4.0), (-0.32, 0.03, 0.06, -0.026, 4.5), (0.1, 0.03, 0.058, -0.022, 4.5),
                (0.16, 0.028, 0.05, -0.016, 4.0)], 26), PAINT)
    b.add(box(0.004, 0.08, 0.018, (0.03, -0.18, 0.01), 0.001), POLY)                  # éjection (arrière, bullpup)
    b.add(box(0.008, 0.012, 0.03, (0.032, 0.08, 0.012), 0.002), METAL)               # levier d'armement
    receiver(b, 0.16, 0.33, 0.026, -0.03, 0.022, ACCENT, 6.0)                          # garde-main Manticore
    vents(b, 0.18, 0.32, -0.004, 0.026, 6, 0.026, POLY)
    vents(b, 0.18, 0.32, -0.004, -0.026, 6, 0.026, POLY)
    rail(b, -0.24, 0.33, 0.028)
    b.add(tube(0.009, 0.009, 0.33, 0.47, 0, 0, 16), METAL)
    flash_hider(b, 0.47)
    b.add(box(0.018, 0.1, 0.06, (0, 0.06, -0.075), 0.012), POLY)                       # pontet intégral bullpup
    grip = pistol_grip(b, 0.07, -0.062, POLY, 10, 0.1, 0.03, 0.045)
    mag = curved_mag(b, -0.12, -0.08, 0.15, 0.024, 0.058, 0.035)
    b.add(loft([(-0.34, 0.031, 0.06, -0.03, 4.5), (-0.352, 0.031, 0.06, -0.03, 4.5)], 22), RUBBER)
    butt = (0, -0.35, -0.03)
    red_dot(b, 0.0, 0.037)
    vertical_grip(b, 0.26, -0.03)
    flashlight(b, 0.28, 0.0, 0.034)
    laser_module(g.opt("laser"), 0.24, 0.0, -0.038)
    scope(g.opt("scope"), -0.04, 0.085, 0.2, 0.012, 0.017, base=0.037)
    g.point("Muzzle", (0, 0.52, 0.0)); g.point("Grip", grip); g.point("SupportV", (0, 0.26, -0.065))
    g.point("Butt", butt); g.point("Mag", mag)


def slade(g):
    """Fusil expérimental (contenu téléchargeable) : carénage anguleux, double rail d'énergie, canon carré."""
    b = g.body
    b.add(prism([(-0.14, 0.03), (0.22, 0.04), (0.42, 0.02), (0.42, -0.04), (0.12, -0.055), (-0.14, -0.045)], -0.024, 0.024, 0.005), PAINT)
    b.add(prism([(-0.1, 0.04), (0.2, 0.05), (0.3, 0.032), (-0.06, 0.03)], -0.018, 0.018, 0.004), ACCENT)
    for s in (-1, 1):
        b.add(box(0.004, 0.24, 0.006, (s * 0.025, 0.2, 0.0), 0.001), ENERGY)
        b.add(box(0.003, 0.16, 0.003, (s * 0.025, 0.2, -0.03), 0.001), ACCENT)
    b.add(box(0.026, 0.2, 0.026, (0, 0.5, -0.004), 0.004), METAL)
    for k in range(4):
        b.add(box(0.03, 0.006, 0.03, (0, 0.43 + k * 0.045, -0.004), 0.002), ACCENT)
    b.add(box(0.036, 0.03, 0.036, (0, 0.61, -0.004), 0.006), ACCENT)
    b.add(box(0.03, 0.002, 0.03, (0, 0.626, -0.004), 0.0005, 1), ENERGY)
    rail(b, -0.08, 0.16, 0.046)
    grip = pistol_grip(b, -0.02, -0.05, POLY, 14)
    mag = straight_mag(b, 0.08, -0.055, 0.12, 0.03, 0.06, 0, PAINT)
    energy_cell(b, 0.08, -0.125, 0.0, 0.05, 0.012)
    butt = stock_sculpt(b, [(-0.14, 0.022, 0.04, -0.005), (-0.25, 0.022, 0.05, -0.01), (-0.38, 0.021, 0.055, -0.012), (-0.4, 0.02, 0.054, -0.012)], PAINT)
    b.add(loft([(-0.18, 0.023, 0.012, -0.005, 2.0), (-0.34, 0.023, 0.012, -0.008, 2.0)], 14), ENERGY)
    red_dot(b, 0.04, 0.055)
    g.point("Muzzle", (0, 0.63, -0.004)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.05))
    g.point("Butt", butt); g.point("Mag", mag)


def blazer(g):
    """Blazer : fusil à faisceau hors normes (contenu téléchargeable), anneaux accélérateurs et cellules d'énergie."""
    b = g.body
    b.add(loft([(-0.16, 0.022, 0.05, -0.01, 3.0), (-0.1, 0.035, 0.06, -0.008, 3.5), (0.25, 0.035, 0.055, -0.004, 3.5),
                (0.32, 0.026, 0.04, 0.0, 3.0)], 26), PAINT)
    b.add(tube(0.016, 0.014, 0.3, 0.68, 0, 0.0, 20), METAL)
    for k in range(5):
        uu = 0.36 + k * 0.07
        b.add(ring(0.034 - k * 0.002, 0.016, uu, uu + 0.02, 0, 0.0, 26), ACCENT)
        b.add(ring(0.029 - k * 0.002, 0.017, uu + 0.006, uu + 0.014, 0, 0.0, 26), ENERGY)
    for s in (-1, 1):
        energy_cell(b, 0.05, -0.02, s * 0.04, 0.16, 0.011)
        b.add(box(0.004, 0.3, 0.008, (s * 0.035, 0.08, 0.03), 0.001), ENERGY)
    rail(b, -0.08, 0.2, 0.05)
    grip = pistol_grip(b, -0.04, -0.05, POLY, 14, 0.1, 0.032, 0.046)
    mag = (0, 0.05, -0.02)
    butt = stock_sculpt(b, [(-0.16, 0.022, 0.045, -0.01), (-0.26, 0.023, 0.05, -0.012), (-0.4, 0.022, 0.055, -0.012), (-0.42, 0.021, 0.054, -0.012)], PAINT)
    holo_sight(b, 0.06, 0.058)
    vertical_grip(b, 0.26, -0.05)
    g.point("Muzzle", (0, 0.7, 0.0)); g.point("Grip", grip); g.point("SupportV", (0, 0.26, -0.09))
    g.point("Butt", butt); g.point("Mag", mag)


# Couleurs de travail par famille (Paint, Accent) : les variantes du jeu les reprennent.
PALETTE = {
    "pa11": ((0.05, 0.05, 0.048), (0.3, 0.27, 0.2)), "raven": ((0.2, 0.17, 0.12), (0.05, 0.05, 0.05)),
    "minion": ((0.16, 0.18, 0.16), (0.08, 0.08, 0.08)), "slugger": ((0.06, 0.06, 0.06), (0.25, 0.14, 0.07)),
    "orchid": ((0.55, 0.57, 0.6), (0.12, 0.2, 0.32)), "gm": ((0.12, 0.14, 0.1), (0.05, 0.05, 0.05)),
    "gm64": ((0.28, 0.16, 0.08), (0.05, 0.05, 0.05)), "stork": ((0.13, 0.15, 0.17), (0.07, 0.08, 0.09)),
    "stork4": ((0.33, 0.28, 0.19), (0.12, 0.12, 0.11)), "slade": ((0.2, 0.21, 0.23), (0.6, 0.25, 0.05)),
    "blazer": ((0.75, 0.76, 0.78), (0.1, 0.12, 0.16)),
}

FAMILIES = {"pa11": pa11, "raven": raven, "minion": minion, "slugger": slugger, "orchid": orchid, "gm": gm, "gm64": gm64,
            "stork": stork, "stork4": stork4, "slade": slade, "blazer": blazer}


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
