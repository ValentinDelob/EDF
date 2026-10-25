# Armes de la Wing Diver (EDF 5), une fonction par famille de modèle. Langage commun : coque claire profilée (Paint), pièces
# internes sombres (Accent / Metal), bandes et cellules lumineuses (Energy), crosse courte, poignée pistolet.
# Courte portée : Rapier = émetteur fourchu d'arc de plasma, Phalanx = émetteur en éventail chargé, Power Lance = long canon
# fin à pointe de lance, Dragoon Lance = lance lourde perforante, Spark Vine / Whip = tige à bobines, Monster Lance = lance
# à condensateurs. Blasters : Mag Blaster = lentille focalisante, Destroyer = bouche large multi-émetteurs, Spine = rails
# perforants, Reflectron = prisme. Électriques : Lightning Bow = arceaux d'arc, Bolt Gun = double électrode, Thunder Crossbow
# = arbalète à bras. Impulsions : chambres à noyau cylindrique (Pulse MG), bouche en grappe (Stardust), barillet (Buster).
# Longue portée : Closed Laser = long tube à ailettes, MONSTER = fusil à condensateurs, Bolt Shooter = électrodes longues,
# Raijin = double rail géant. Artillerie : gros alésages et noyau de plasma visible. Guidées : réseaux d'émetteurs.
# Spéciales : lanceur de sphère d'énergie (gantelet à berceau).
import math


def shell(b, u0, u1, hw, hh, z=0.0, mat=PAINT, p=3.2, taper=0.75):
    """Coque profilée : sections superellipse, effilée vers l'avant."""
    b.add(loft([(u0, hw * 0.7, hh * 0.75, z, p), (u0 + 0.04, hw, hh, z, p), (u1 - 0.06, hw * (0.5 + 0.5 * taper), hh * (0.5 + 0.5 * taper), z, p),
                (u1, hw * taper * 0.8, hh * taper * 0.8, z, p)], 26), mat)


def strip(b, u0, u1, z, x=0.0, w=0.004, h=0.006):
    b.add(box(w, u1 - u0, h, (x, (u0 + u1) / 2, z), 0.001, 1), ENERGY)


def wd_base(b, stock=True, grip_u=-0.03, z=-0.04):
    """Poignée, pontet intégré et crosse courte communs."""
    grip = pistol_grip(b, grip_u, z, POLY, 12, 0.095, 0.03, 0.044)
    if stock:
        butt = stock_sculpt(b, [(-0.12, 0.02, 0.03, -0.012), (-0.2, 0.021, 0.04, -0.02), (-0.3, 0.02, 0.046, -0.024), (-0.315, 0.019, 0.045, -0.024)], PAINT)
        strip(b, -0.28, -0.14, -0.024 + 0.0, 0.021, 0.003, 0.012)
    else:
        butt = (0, -0.16, -0.02)
    return grip, butt


def emitter_ring(b, u, r, z=0.0, n=1):
    for k in range(n):
        b.add(ring(r * (1 - 0.12 * k), r * 0.7, u + k * 0.03, u + k * 0.03 + 0.014, 0, z, 24), ACCENT)
        b.add(ring(r * 0.86 * (1 - 0.12 * k), r * 0.72, u + k * 0.03 + 0.003, u + k * 0.03 + 0.011, 0, z, 24), ENERGY)


def pts(g, muzzle, grip, butt, sup, mag=(0, 0.05, -0.04), vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Courte portée ─────────────────────────

def rapier(g):
    b = g.body
    shell(b, -0.12, 0.26, 0.03, 0.045, -0.005)
    b.add(box(0.03, 0.1, 0.03, (0, 0.3, 0.0), 0.006), ACCENT)
    for s in (-1, 1):  # émetteur fourchu
        b.add(prism([(0.3, s * 0.006), (0.46, s * 0.03), (0.47, s * 0.026), (0.34, s * 0.002)], -0.006, 0.006, 0.002), METAL,
              Matrix.Rotation(0, 4, 'Y'))
        b.add(box(0.008, 0.14, 0.01, (s * 0.022, 0.4, 0.0), 0.002), METAL)
        b.add(box(0.004, 0.1, 0.004, (s * 0.022, 0.41, 0.0), 0.001, 1), ENERGY)
    energy_cell(b, 0.05, -0.05, 0, 0.1, 0.014)
    strip(b, -0.08, 0.2, 0.04)
    grip, butt = wd_base(b)
    pts(g, (0, 0.47, 0), grip, butt, (0, 0.18, -0.05))


def phalanx(g):
    b = g.body
    shell(b, -0.13, 0.24, 0.034, 0.05, -0.005)
    b.add(loft([(0.24, 0.02, 0.02, 0, 2.0), (0.38, 0.05, 0.022, 0, 3.0), (0.42, 0.055, 0.02, 0, 3.0)], 22), ACCENT)  # émetteur en éventail
    for k in range(5):
        x = (k - 2) * 0.02
        b.add(box(0.006, 0.08, 0.008, (x, 0.4, 0.0), 0.001), METAL)
        b.add(box(0.003, 0.06, 0.003, (x, 0.41, 0.006), 0.0008, 1), ENERGY)
    for s in (-1, 1):
        energy_cell(b, 0.08, 0.01, s * 0.04, 0.14, 0.012)
    grip, butt = wd_base(b)
    pts(g, (0, 0.44, 0), grip, butt, (0, 0.2, -0.05))


def lance(g, heavy=False, monster=False):
    b = g.body
    r = 0.024 if heavy else 0.016
    shell(b, -0.13, 0.22, 0.032 if heavy else 0.028, 0.05 if heavy else 0.044, -0.006)
    b.add(tube(r, r * 0.8, 0.2, 0.7 if heavy else 0.64, 0, 0.004, 20), METAL)
    for uu in ((0.28, 0.4, 0.52, 0.62) if heavy else (0.3, 0.46)):
        b.add(ring(r * 1.6, r * 0.95, uu, uu + 0.02, 0, 0.004, 22), ACCENT)
        b.add(ring(r * 1.45, r * 1.0, uu + 0.005, uu + 0.015, 0, 0.004, 22), ENERGY)
    tip = 0.7 if heavy else 0.64
    b.add(tube(r * 0.8, 0.002, tip, tip + 0.08, 0, 0.004, 18), ACCENT)                # pointe de lance
    if monster:
        for s in (-1, 1):
            b.add(tube(0.016, 0.016, 0.0, 0.2, s * 0.045, -0.01, 16), ACCENT)
            b.add(tube(0.012, 0.012, 0.02, 0.18, s * 0.045, -0.01, 16), ENERGY)
    if heavy:
        b.add(box(0.07, 0.12, 0.02, (0, 0.05, 0.05), 0.006), ACCENT)                  # ailerons dorsaux
    strip(b, -0.1, 0.18, 0.045 if heavy else 0.04)
    grip, butt = wd_base(b)
    vertical_grip(b, 0.28, -0.014)
    pts(g, (0, tip + 0.08, 0.004), grip, butt, (0, 0.28, -0.06), vertical=True)


def powerlance(g): lance(g)
def dragoon(g): lance(g, heavy=True)
def monsterlance(g): lance(g, heavy=True, monster=True)


def spark(g, whip=False):
    b = g.body
    shell(b, -0.12, 0.2, 0.03, 0.045, -0.005)
    b.add(tube(0.008, 0.008, 0.2, 0.58, 0, 0.004, 12), METAL)
    for k in range(6):
        uu = 0.24 + k * 0.055
        b.add(ring(0.022, 0.009, uu, uu + 0.02, 0, 0.004, 20), ACCENT)
        b.add(ring(0.019, 0.01, uu + 0.006, uu + 0.014, 0, 0.004, 20), ENERGY)
    if whip:
        for a in (-35, 35):
            bm = tube(0.005, 0.003, 0.0, 0.12, 0, 0, 10)
            b.add(bm, METAL, Matrix.Translation(P(0, 0.58, 0.004)) @ Matrix.Rotation(math.radians(a), 4, 'Z'))
    b.add(sphere_ball(0.016, (0, 0.6, 0.004)), ENERGY)
    grip, butt = wd_base(b)
    pts(g, (0, 0.61, 0.004), grip, butt, (0, 0.17, -0.05))


def sparkvine(g): spark(g)
def sparkwhip(g): spark(g, True)


def sphere_ball(r, c, mat=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


# ───────────────────────── Blasters ─────────────────────────

def blaster(g, kind):
    b = g.body
    shell(b, -0.13, 0.3, 0.032, 0.05, -0.005)
    if kind == "destroyer":
        b.add(loft([(0.3, 0.03, 0.03, 0, 3.0), (0.42, 0.06, 0.04, 0, 3.5)], 22), ACCENT)
        for x in (-0.03, 0.0, 0.03):
            for z in (-0.015, 0.015):
                b.add(tube(0.008, 0.008, 0.415, 0.425, x, z, 10), ENERGY)
        mz = 0.43
    elif kind == "spine":
        for s in (-1, 1):
            b.add(box(0.008, 0.36, 0.02, (s * 0.018, 0.46, 0.0), 0.002), METAL)
            b.add(box(0.003, 0.32, 0.008, (s * 0.012, 0.46, 0.0), 0.001), ENERGY)
        mz = 0.64
    elif kind == "reflectron":
        b.add(tube(0.016, 0.016, 0.3, 0.46, 0, 0, 16), METAL)
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=3, radius1=0.04, radius2=0.04, depth=0.05)
        b.add(bm, GLASS, Matrix.Translation(P(0, 0.49, 0.0)) @ Matrix.Rotation(math.radians(90), 4, 'X'))
        mz = 0.52
    else:  # magblaster
        b.add(tube(0.02, 0.02, 0.3, 0.44, 0, 0, 20), METAL)
        emitter_ring(b, 0.44, 0.036, 0, 2)
        b.add(tube(0.02, 0.02, 0.5, 0.502, 0, 0, 18), LENS)
        mz = 0.5
    energy_cell(b, 0.06, -0.06, 0, 0.12, 0.014)
    strip(b, -0.08, 0.26, 0.045)
    rail(b, -0.06, 0.18, 0.045, w=0.02)
    grip, butt = wd_base(b)
    pts(g, (0, mz, 0), grip, butt, (0, 0.22, -0.05))


def magblaster(g): blaster(g, "magblaster")
def destroyer(g): blaster(g, "destroyer")
def spine(g): blaster(g, "spine")
def reflectron(g): blaster(g, "reflectron")


# ───────────────────────── Électriques ─────────────────────────

def lightningbow(g):
    b = g.body
    shell(b, -0.12, 0.24, 0.03, 0.048, -0.005)
    for s in (-1, 1):  # arceaux d'arc au-dessus et au-dessous
        pts_ = [(0.2, s * 0.04), (0.32, s * 0.09), (0.44, s * 0.06), (0.5, s * 0.02)]
        for (u0, z0), (u1, z1) in zip(pts_, pts_[1:]):
            d = math.hypot(u1 - u0, z1 - z0)
            a = math.atan2(z1 - z0, u1 - u0)
            bm = box(0.012, d, 0.01, (0, 0, 0), 0.002)
            b.add(bm, ACCENT, Matrix.Translation(P(0, (u0 + u1) / 2, (z0 + z1) / 2)) @ Matrix.Rotation(-a, 4, 'X'))
    b.add(tube(0.006, 0.006, 0.22, 0.52, 0, 0.0, 10), ENERGY)
    grip, butt = wd_base(b)
    pts(g, (0, 0.52, 0), grip, butt, (0, 0.18, -0.05))


def boltgun(g):
    b = g.body
    shell(b, -0.12, 0.22, 0.034, 0.046, -0.005)
    for s in (-1, 1):
        b.add(tube(0.009, 0.006, 0.22, 0.48, s * 0.022, 0.0, 12), METAL)
        b.add(sphere_ball(0.012, (s * 0.022, 0.49, 0.0)), ENERGY)
    for uu in (0.28, 0.38):
        b.add(box(0.07, 0.012, 0.02, (0, uu, 0), 0.004), ACCENT)
    grip, butt = wd_base(b)
    pts(g, (0, 0.5, 0), grip, butt, (0, 0.2, -0.05))


def crossbow(g):
    b = g.body
    shell(b, -0.12, 0.34, 0.03, 0.045, -0.005)
    for s in (-1, 1):  # bras d'arbalète
        bm = box(0.2, 0.02, 0.012, (0, 0, 0), 0.003)
        b.add(bm, ACCENT, Matrix.Translation(P(s * 0.11, 0.3, 0.01)) @ Matrix.Rotation(math.radians(-s * 15), 4, 'Z'))
        b.add(sphere_ball(0.012, (s * 0.2, 0.27, 0.01)), ENERGY)
    b.add(box(0.008, 0.3, 0.006, (0, 0.2, 0.05), 0.001), ENERGY)
    grip, butt = wd_base(b)
    pts(g, (0, 0.36, 0), grip, butt, (0, 0.18, -0.05))


# ───────────────────────── Impulsions ─────────────────────────

def pulse(g, kind="pulsemg"):
    b = g.body
    shell(b, -0.13, 0.26, 0.036, 0.052, -0.005)
    if kind == "stardust":
        b.add(tube(0.045, 0.05, 0.26, 0.44, 0, 0, 24), ACCENT)
        for k in range(7):
            a = 2 * math.pi * k / 7
            b.add(tube(0.009, 0.009, 0.439, 0.443, math.cos(a) * 0.03, math.sin(a) * 0.03, 10), ENERGY)
        b.add(tube(0.009, 0.009, 0.439, 0.443, 0, 0, 10), ENERGY)
        mz = 0.45
    elif kind == "pulsebuster":
        for k in range(4):
            a = 2 * math.pi * k / 4 + math.pi / 4
            b.add(tube(0.012, 0.012, 0.26, 0.48, math.cos(a) * 0.022, math.sin(a) * 0.022, 12), METAL)
        b.add(ring(0.042, 0.01, 0.3, 0.33, 0, 0, 24), ACCENT)
        mz = 0.48
    else:
        b.add(tube(0.016, 0.016, 0.26, 0.5, 0, 0, 18), METAL)
        emitter_ring(b, 0.47, 0.03, 0, 1)
        mz = 0.5
    # Chambre à noyau d'impulsion, visible sur le flanc.
    b.add(tube_x(0.035, 0.08, (0, 0.08, -0.01)), ACCENT)
    b.add(tube_x(0.025, 0.082, (0, 0.08, -0.01)), ENERGY)
    strip(b, -0.09, 0.22, 0.05)
    grip, butt = wd_base(b)
    vertical_grip(b, 0.22, -0.054)
    pts(g, (0, mz, 0), grip, butt, (0, 0.22, -0.1), vertical=True)


def pulsemg(g): pulse(g)
def stardust(g): pulse(g, "stardust")
def pulsebuster(g): pulse(g, "pulsebuster")


# ───────────────────────── Longue portée ─────────────────────────

def longgun(g, kind):
    b = g.body
    shell(b, -0.14, 0.28, 0.03, 0.05, -0.005)
    if kind == "closedlaser":
        b.add(tube(0.016, 0.014, 0.28, 0.95, 0, 0, 18), METAL)
        for k in range(8):
            b.add(box(0.05, 0.008, 0.05, (0, 0.32 + k * 0.07, 0), 0.002), ACCENT)
        emitter_ring(b, 0.93, 0.024, 0, 1)
        mz = 0.96
    elif kind == "monster":
        b.add(tube(0.02, 0.018, 0.28, 0.9, 0, 0, 20), METAL)
        for s in (-1, 1):
            b.add(tube(0.018, 0.018, 0.0, 0.3, s * 0.05, 0.0, 16), ACCENT)
            b.add(tube(0.013, 0.013, 0.02, 0.28, s * 0.05, 0.0, 16), ENERGY)
        emitter_ring(b, 0.86, 0.034, 0, 2)
        mz = 0.92
    elif kind == "raijin":
        for s in (-1, 1):
            b.add(box(0.014, 0.66, 0.04, (s * 0.024, 0.58, 0.0), 0.003), METAL)
            b.add(box(0.004, 0.6, 0.016, (s * 0.016, 0.58, 0.0), 0.001), ENERGY)
        for k in range(5):
            b.add(box(0.08, 0.016, 0.016, (0, 0.32 + k * 0.13, 0.03), 0.003), ACCENT)
        mz = 0.92
    else:  # boltshooter
        for s in (-1, 1):
            b.add(tube(0.008, 0.006, 0.28, 0.85, s * 0.016, 0.0, 12), METAL)
        b.add(sphere_ball(0.016, (0, 0.86, 0.0)), ENERGY)
        mz = 0.87
    scope(b, 0.05, 0.085, 0.3, 0.015, 0.024, base=0.05)
    strip(b, -0.1, 0.24, 0.045)
    grip, butt = wd_base(b)
    pts(g, (0, mz, 0), grip, butt, (0, 0.24, -0.05))


def closedlaser(g): longgun(g, "closedlaser")
def monster(g): longgun(g, "monster")
def raijin(g): longgun(g, "raijin")
def boltshooter(g): longgun(g, "boltshooter")


# ───────────────────────── Artillerie plasma ─────────────────────────

def plasma(g, kind="plasmacannon"):
    b = g.body
    r = {"heavycannon": 0.06, "bigcannon": 0.055, "grit": 0.024, "splasher": 0.045}.get(kind, 0.042)
    shell(b, -0.14, 0.2, 0.038, 0.055, -0.005)
    if kind == "burstcannon":
        for s in (-1, 1):
            b.add(tube(0.03, 0.03, 0.2, 0.56, s * 0.032, 0.0, 20), PAINT)
            b.add(tube(0.02, 0.02, 0.559, 0.563, s * 0.032, 0.0, 16), ENERGY)
    elif kind == "plasmafall":
        b.add(tube(r, r, 0.2, 0.52, 0, 0.0, 22), PAINT)
        b.add(tube(r * 0.75, r * 0.75, 0.519, 0.523, 0, 0.0, 20), ENERGY)
        b.add(box(0.03, 0.2, 0.05, (0, 0.38, r + 0.02), 0.006), ACCENT)                # tuyère de tir plongeant
    else:
        b.add(tube(r, r * 1.05, 0.2, 0.56, 0, 0.0, 24), PAINT)
        b.add(tube(r * 0.78, r * 0.78, 0.559, 0.563, 0, 0.0, 22), ENERGY)
        for uu in (0.26, 0.42):
            b.add(ring(r * 1.15, r * 0.95, uu, uu + 0.025, 0, 0.0, 24), ACCENT)
        if kind == "grit":
            for k in range(6):
                b.add(box(0.06, 0.006, 0.06, (0, 0.24 + k * 0.05, 0.0), 0.002), ACCENT)
    # Noyau de plasma visible dans une fenêtre sur le dessus.
    b.add(tube(0.022, 0.022, 0.0, 0.14, 0, 0.05, 18), ENERGY)
    b.add(box(0.05, 0.16, 0.012, (0, 0.07, 0.072), 0.004), ACCENT)
    grip, butt = wd_base(b)
    vertical_grip(b, 0.3, -r - 0.004)
    pts(g, (0, 0.57, 0), grip, butt, (0, 0.3, -r - 0.05), vertical=True)


def plasmacannon(g): plasma(g)
def plasmafall(g): plasma(g, "plasmafall")
def bigcannon(g): plasma(g, "bigcannon")
def heavycannon(g): plasma(g, "heavycannon")
def burstcannon(g): plasma(g, "burstcannon")
def grit(g): plasma(g, "grit")
def splasher(g): plasma(g, "splasher")


def blinkball(g):
    b = g.body
    b.add(sphere_ball(0.035, (0, 0, 0)), PAINT)
    for a in (0, 90):
        bm = ring(0.037, 0.033, -0.004, 0.004, 0, 0, 26)
        b.add(bm, ENERGY, Matrix.Rotation(math.radians(a), 4, 'Z') @ Matrix.Rotation(math.radians(90), 4, 'Y'))
    g.point("Muzzle", (0, 0.05, 0)); g.point("Grip", (0, 0, 0)); g.point("Support", (0, 0.06, 0)); g.point("Butt", (0, -0.1, 0)); g.point("Mag", (0, 0, 0))


# ───────────────────────── Guidées et spéciales ─────────────────────────

def homing(g, kind="mirage"):
    b = g.body
    shell(b, -0.12, 0.24, 0.034, 0.05, -0.005)
    n = {"mirage": 5, "geist": 3, "ghostchaser": 7, "fenrir": 2, "psiblade": 1}.get(kind, 4)
    for k in range(n):
        x = (k - (n - 1) / 2) * 0.018
        b.add(tube(0.006, 0.006, 0.24, 0.4 - abs(k - (n - 1) / 2) * 0.02, x, 0.01, 10), METAL)
        b.add(sphere_ball(0.008, (x, 0.4 - abs(k - (n - 1) / 2) * 0.02, 0.01)), ENERGY)
    b.add(box(0.06, 0.08, 0.04, (0, 0.06, 0.06), 0.006), ACCENT)                       # capteur de verrouillage
    b.add(box(0.05, 0.002, 0.03, (0, 0.019, 0.06), 0.001), ENERGY)
    grip, butt = wd_base(b)
    pts(g, (0, 0.42, 0.01), grip, butt, (0, 0.18, -0.05))


def mirage(g): homing(g, "mirage")
def geist(g): homing(g, "geist")
def ghostchaser(g): homing(g, "ghostchaser")
def fenrir(g): homing(g, "fenrir")
def psiblade(g): homing(g, "psiblade")


def orb(g, kind="diffuser"):
    """Lanceur de sphère : gantelet à berceau tenant une sphère d'énergie, couleur selon la famille."""
    b = g.body
    shell(b, -0.08, 0.16, 0.034, 0.045, -0.005, ACCENT)
    for s in (-1, 1):
        bm = box(0.01, 0.12, 0.02, (0, 0, 0), 0.003)
        b.add(bm, PAINT, Matrix.Translation(P(s * 0.04, 0.2, 0.0)) @ Matrix.Rotation(math.radians(-s * 20), 4, 'Z'))
    b.add(sphere_ball(0.045, (0, 0.24, 0.0)), GLOW if kind in ("heavensgate", "psicluster") else ENERGY)
    b.add(ring(0.055, 0.05, 0.236, 0.244, 0, 0.0, 26), METAL)
    grip, butt = wd_base(b, stock=False)
    pts(g, (0, 0.26, 0), grip, butt, (0, 0.12, -0.05))


def diffuser(g): orb(g)
def gleipnir(g): orb(g, "gleipnir")
def thundercluster(g): orb(g, "thundercluster")
def heavensgate(g): orb(g, "heavensgate")
def psicluster(g): orb(g, "psicluster")
def finaldays(g): orb(g, "finaldays")


WHITE, GREY, DARK = (0.78, 0.8, 0.83), (0.55, 0.57, 0.6), (0.12, 0.13, 0.15)
PALETTE = {f: (WHITE, DARK) for f in [
    "rapier", "phalanx", "powerlance", "dragoon", "monsterlance", "sparkvine", "sparkwhip", "magblaster", "destroyer", "spine",
    "reflectron", "lightningbow", "boltgun", "crossbow", "pulsemg", "stardust", "pulsebuster", "closedlaser", "monster", "raijin",
    "boltshooter", "plasmacannon", "plasmafall", "bigcannon", "heavycannon", "burstcannon", "grit", "splasher", "blinkball",
    "mirage", "geist", "ghostchaser", "fenrir", "psiblade", "diffuser", "gleipnir", "thundercluster", "heavensgate", "psicluster", "finaldays"]}
# Quelques familles se distinguent par la teinte de la coque.
PALETTE.update({"dragoon": (GREY, DARK), "monster": (GREY, (0.5, 0.12, 0.08)), "raijin": (DARK, (0.6, 0.5, 0.15)),
                "heavycannon": (GREY, (0.5, 0.25, 0.05)), "monsterlance": (GREY, (0.5, 0.12, 0.08)), "fenrir": (DARK, GREY),
                "finaldays": (DARK, (0.55, 0.1, 0.1))})

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
