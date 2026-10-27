# Fusils d'assaut du Ranger (EDF 5), une fonction par famille de modèle. Inspirations (IMFDB) : PA-11 = M16A4 / T65K2,
# M1 Raven = Colt 933 accessoirisé, Minion Buster = G36KA4 stylisé (poignée de transport haute, crosse squelette repliable,
# chargeur translucide à tenons), Slugger NN = AK-12 (couvercle à rail, sélecteur long, crosse télescopique),
# X-Orchid = Slugger expérimental (carénage à facettes, bobines d'accélération), G&M = Type 89 (bipied, chargeur 20 coups),
# G&M-A25/A29 = Type 64 (bois, bipied à la bouche, grand frein de bouche), T1-T3 Stork = FAMAS F1 « débullpupé » × F2000,
# T4/T5/TZ Stork = Tavor (bullpup) garde-main Manticore ; Slade, Blazer (contenus téléchargeables) : armes expérimentales.
# Usage : exec(kit.py) puis exec(rifles.py) dans Blender ; FAMILY = "pa11"… ; OUT = chemin du .glb (facultatif).
import math, os

K = globals()
SOLVER = "MANIFOLD"   # solveur des découpes booléennes (robuste sur nos formes fermées)

# Marquages au pochoir (noms d'EDF 5) et emplacement (u, z) sur un flanc bien plat.
LABELS = {"pa11": "PA-11", "raven": "M1 RAVEN", "minion": "MINION BUSTER", "slugger": "SLUGGER NN", "orchid": "X400-ORCHID",
          "gm": "G&M-21S", "gm64": "G&M-A25", "stork": "T1 STORK", "stork4": "T4 STORK", "slade": "MA9 SLADE",
          "blazer": "BLAZER"}
MARK_AT = {"pa11": (0.07, -0.056), "raven": (0.065, -0.056), "minion": (-0.048, -0.006), "slugger": (-0.052, -0.028),
           "orchid": (-0.05, -0.032), "gm": (0.0, -0.03), "gm64": (0.13, -0.026), "stork": (-0.075, -0.026),
           "stork4": (-0.255, -0.012), "slade": (-0.075, -0.012), "blazer": (-0.09, -0.004)}


# ───────────────────────── Outils propres au module ─────────────────────────

def _se(c, p):
    return math.copysign(abs(c) ** (2.0 / p), c)


def pitch(deg, u, z):
    """Matrice de tangage (+ = avant vers le haut) autour de l'axe x passant par (u, z)."""
    c = P(0, u, z)
    return Matrix.Translation(c) @ Matrix.Rotation(math.radians(-deg), 4, 'X') @ Matrix.Translation(-c)


def spin(bm, deg, x=0.0, z=0.0):
    """Rotation d'une forme autour de l'axe u passant par (x, z) (repère de travail)."""
    bmesh.ops.rotate(bm, cent=P(x, 0, z), matrix=Matrix.Rotation(math.radians(deg), 3, 'Y'), verts=bm.verts)
    return bm


def cut(part, bm, mat, cutters):
    """Ajoute à la pièce une forme découpée (carve)."""
    part.add(carve(bm, cutters, SOLVER), mat)


def shell(sections, inner=0.0025, seg=24, p_in=None):
    """Coque creuse : loft extérieur moins le même loft rétréci de « inner » (garde-mains ajourés)."""
    outer = loft(sections, seg)
    ins = [(u, max(hw - inner, 0.002), max(hh - inner, 0.002), z, p if p_in is None else p_in) for (u, hw, hh, z, p) in sections]
    ins[0] = (ins[0][0] - 0.01,) + ins[0][1:]
    ins[-1] = (ins[-1][0] + 0.01,) + ins[-1][1:]
    return carve(outer, [loft(ins, seg)], SOLVER)


def fluted(sections, n, amp, seg=48):
    """Tube à nervures longitudinales : sections = [(u, rayon, z)], n nervures d'amplitude relative amp."""
    bm = bmesh.new()
    loops = []
    for (u, r, z) in sections:
        loop = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            rr = r * (1 + amp * max(math.cos(n * a), -0.3))
            loop.append(bm.verts.new(P(rr * math.cos(a), u, z + rr * math.sin(a))))
        loops.append(loop)
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def cable(points, r=0.003, seg=8):
    """Câble / durite : tube suivant une polyligne [(x, u, z)]."""
    bm = bmesh.new()
    pts = [P(*p) for p in points]
    n = len(pts)
    loops = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        a = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        n1 = t.cross(a).normalized()
        n2 = t.cross(n1)
        loops.append([bm.verts.new(p + (n1 * math.cos(2 * math.pi * k / seg) + n2 * math.sin(2 * math.pi * k / seg)) * r)
                      for k in range(seg)])
    for k in range(n - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rivet(part, u, z, x, r=0.0022, side=1, mat=METAL):
    """Rivet à tête bombée sur un flanc."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=r, radius2=r * 0.55, depth=r * 0.8)
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2 * side, 3, 'Y'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(x + side * r * 0.35, u, z), verts=bm.verts)
    part.add(bm, mat)


def knurl(part, u0, u1, r, z=0.0, x=0.0, n=14, mat=METAL):
    """Molette moletée le long de u (bague + crans)."""
    part.add(tube(r, r, u0, u1, x, z, 20), mat)
    for k in range(n):
        a = 2 * math.pi * k / n
        part.add(box(0.0018, u1 - u0, 0.0018, (x + math.cos(a) * r, (u0 + u1) / 2, z + math.sin(a) * r), 0, 1), mat)


def knob_x(part, u, z, x, r, l, side=1, mat=METAL, n=10):
    """Bouton / tourelle d'axe x moleté, posé sur un flanc (side ±1)."""
    part.add(tube_x(r, l, (x + side * l / 2, u, z), 18), mat)
    for k in range(n):
        a = 2 * math.pi * k / n
        part.add(box(l * 0.8, 0.0016, 0.0016, (x + side * l / 2, u + math.cos(a) * r, z + math.sin(a) * r), 0, 1), mat)


def curve_strip(u, z, h, d, bend, f0, f1, x0, x1, t0=0.0, t1=1.0, lin=0.0, n=10, bevel=0.0015):
    """Bande d'un chargeur courbe : de f0 à f1 de sa profondeur (arrière → avant), de t0 à t1 de sa hauteur."""
    pb, pf = [], []
    for k in range(n + 1):
        t = t0 + (t1 - t0) * k / n
        du = bend * t * t + lin * t
        zz = z - h * t
        ub, uf = u - d / 2 + du * 0.9, u + d / 2 + du
        pb.append((ub + (uf - ub) * f0, zz))
        pf.append((ub + (uf - ub) * f1, zz))
    return prism(pb + pf[::-1], x0, x1, bevel)


def magazine(part, u, z, h, d, w, bend=0.0, lin=0.0, mat=METAL, style="ar", base_mat=POLY):
    """Chargeur détaillé (haut à (u, z), engagé dans le puits) : nervures latérales, plaque de fond, tenons ou fenêtre.
    style : "ar" (STANAG acier), "pmag" (polymère texturé à fenêtre), "ak" (nervures AK), "g36" (tenons d'accouplement)."""
    part.add(curve_strip(u, z, h, d, bend, 0, 1, -w / 2, w / 2, lin=lin), mat)
    t_b = 1.0
    ub, uf = u - d / 2 + (bend + lin) * 0.9, u + d / 2 + bend + lin
    zb = z - h
    if style == "ar":
        for (f0, f1) in ((0.08, 0.18), (0.82, 0.92)):
            part.add(curve_strip(u, z, h, d, bend, f0, f1, -w / 2 - 0.0012, w / 2 + 0.0012, 0.25, 0.97, lin, 8, 0.0006), mat)
        part.add(curve_strip(u, z, h, d, bend, 0.3, 0.7, -w / 2 - 0.0008, w / 2 + 0.0008, 0.3, 0.42, lin, 2, 0.0005), mat)
    elif style == "pmag":
        for k in range(5):
            t = 0.55 + k * 0.08
            part.add(curve_strip(u, z, h, d, bend, 0.04, 0.96, -w / 2 - 0.001, w / 2 + 0.001, t, t + 0.035, lin, 1, 0.0004), mat)
        part.add(curve_strip(u, z, h, d, bend, 0.72, 0.8, -w / 2 - 0.0004, w / 2 + 0.0004, 0.28, 0.5, lin, 3, 0.0004), GLASS)
        part.add(curve_strip(u, z, h, d, bend, 0.05, 0.15, -w / 2 - 0.0012, w / 2 + 0.0012, 0.3, 0.5, lin, 3, 0.0005), mat)
    elif style == "ak":
        for (f0, f1) in ((0.0, 0.12), (0.88, 1.0)):
            part.add(curve_strip(u, z, h, d, bend, f0, f1, -w / 2 - 0.0015, w / 2 + 0.0015, 0.2, 0.98, lin, 10, 0.0006), mat)
        part.add(curve_strip(u, z, h, d, bend, 0.12, 0.88, -w / 2 - 0.0009, w / 2 + 0.0009, 0.22, 0.3, lin, 2, 0.0005), mat)
        for k in range(3):
            t = 0.42 + k * 0.17
            part.add(curve_strip(u, z, h, d, bend, 0.3, 0.7, -w / 2 - 0.0007, w / 2 + 0.0007, t, t + 0.05, lin, 2, 0.0004), mat)
    elif style == "g36":
        part.add(curve_strip(u, z, h, d, bend, 0.15, 0.85, -w / 2 - 0.0004, w / 2 + 0.0004, 0.2, 0.85, lin, 6, 0.0005), GLASS)
        for s in (-1, 1):
            for t in (0.3, 0.7):
                du = bend * t * t + lin * t
                part.add(tube_x(0.005, 0.006, (s * (w / 2 + 0.002), u - d / 2 + 0.008 + du, z - h * t), 12), mat)
                part.add(tube_x(0.005, 0.006, (s * (w / 2 + 0.002), u + d / 2 - 0.008 + du, z - h * t), 12), mat)
    # Plaque de fond à lèvre avant et creux de préhension.
    part.add(prism([(ub - 0.004, zb + 0.002), (uf + 0.006, zb + 0.002), (uf + 0.004, zb - 0.008), (ub - 0.002, zb - 0.01)],
                   -w / 2 - 0.0025, w / 2 + 0.0025, 0.002), base_mat)
    part.add(box(w * 0.6, (uf - ub) * 0.5, 0.002, (0, (ub + uf) / 2, zb - 0.0105), 0.0005, 1), base_mat)
    return (0, u + bend * 0.3 + lin * 0.5, z - h / 2)


def grip(part, u, z, ang=20, h=0.1, w=0.03, d=0.044, mat=POLY, bump=0.15, fingers=0, stipple=True, cap=True, n=8, p=2.6):
    """Poignée pistolet ergonomique : renflement de paume, ergot de doigt (bump) ou 3 creux de doigts (fingers), stries
    latérales et talon. (u, z) = haut de la poignée ; renvoie le point de la main."""
    a = math.radians(ang)
    seg = 18
    bm = bmesh.new()
    loops = []
    for k in range(n):
        t = k / (n - 1)
        cu = u - math.sin(a) * h * t
        cz = z - math.cos(a) * h * t
        hw = w / 2 * (1.0 + 0.1 * math.sin(t * math.pi))
        df = d / 2 * (1.0 - 0.12 * t)
        db = d / 2 * (1.05 - 0.05 * t + 0.08 * math.sin(t * math.pi))
        if bump:
            df *= 1 + bump * math.exp(-((t - 0.42) / 0.1) ** 2)
        if fingers:
            df *= 1 - 0.1 * max(0.0, math.sin(t * math.pi * 3.0 + 0.4)) * (0.2 < t < 0.95)
        loop = []
        for i in range(seg):
            th = 2 * math.pi * i / seg
            c, s = math.cos(th), math.sin(th)
            loop.append(bm.verts.new(P(hw * _se(c, p), cu + (df if s > 0 else db) * _se(s, p), cz)))
        loops.append(loop)
    for k in range(n - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    part.add(bm, mat)
    if stipple:
        for s in (-1, 1):
            for k in range(7):
                t = 0.25 + k * 0.095
                cu = u - math.sin(a) * h * t
                cz = z - math.cos(a) * h * t
                hw = w / 2 * (1.0 + 0.1 * math.sin(t * math.pi))
                part.add(box(0.0016, d * 0.55, 0.0028, (s * (hw - 0.0002), cu - d * 0.06, cz), 0.0004, 1), mat, pitch(-ang, cu, cz))
    if cap:
        cu, cz = u - math.sin(a) * h, z - math.cos(a) * h
        part.add(loft([(cu - d * 0.55, w * 0.55, 0.004, cz - 0.002, 3.0), (cu + d * 0.5, w * 0.55, 0.004, cz - 0.002, 3.0)], 16),
                 mat, pitch(-ang * 0.4, cu, cz))
    return (0, u - math.sin(a) * h * 0.45, z - math.cos(a) * h * 0.45)


def trigger(part, u, z, l=0.022, mat=METAL):
    """Queue de détente courbe ; (u, z) = pivot (haut)."""
    pr = []
    for k in range(7):
        t = k / 6
        pr.append((u + 0.004 - 0.008 * t * t, z - l * t))
    for k in range(6, -1, -1):
        t = k / 6
        pr.append((u - 0.0005 - 0.008 * t * t + 0.0015 * t, z - l * t))
    part.add(prism(pr, -0.0028, 0.0028, 0.0006), mat)


def trigger_guard(part, u0, u1, ztop, zbot, w=0.008, t=0.0045, mat=METAL, round_front=True):
    """Pontet (anneau de détente) de u0 (arrière) à u1 (avant), découpé en arceau."""
    r = 0.012 if round_front else 0.003
    outer = [(u0 - 0.004, ztop), (u1, ztop), (u1, zbot + r), (u1 - r * 0.4, zbot + r * 0.25), (u1 - r, zbot), (u0 - 0.004, zbot)]
    inner = [(u0 - 0.02, ztop + 0.01), (u1 - t, ztop + 0.01), (u1 - t, zbot + r), (u1 - t - r * 0.35, zbot + t + r * 0.25),
             (u1 - r, zbot + t), (u0 - 0.02, zbot + t)]
    cut(part, prism(outer, -w / 2, w / 2, 0.0012), mat, [prism(inner, -w, w, 0)])


def sling_loop(part, u, z, x=0.0, r=0.009, side=0, mat=METAL):
    """Anneau / battant de bretelle (anneau d'axe x, ou d'axe u si side)."""
    bm = ring(r, r * 0.62, -0.0015, 0.0015, 0, 0, 14)
    if not side:
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Z'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(x, u, z), verts=bm.verts)
    part.add(bm, mat)


def bore(u, r, l=0.06, z=0.0):
    """Forme de découpe : âme du canon débouchant à la bouche (u)."""
    return tube(r, r, u - l, u + 0.01, 0, z, 14)


def birdcage(part, u, z=0.0, r=0.0112, l=0.05, mat=METAL):
    """Cache-flammes « cage à oiseaux » A2 : 5 fentes (pas en dessous), bouche creuse."""
    cutters = [bore(u + l, r * 0.5, l * 0.9, z)]
    for ang in (90, 18, 162, -54, -126):
        bm = box(0.0035, l * 0.55, 0.012, (0, u + l * 0.6, z + r), 0.0012, 2)
        cutters.append(spin(bm, ang - 90, 0, z))
    cut(part, loft([(u, r * 0.82, r * 0.82, z, 2.0), (u + 0.006, r, r, z, 2.0), (u + l, r * 1.04, r * 1.04, z, 2.0)], 20), mat, cutters)
    part.add(ring(r * 0.9, r * 0.6, u - 0.004, u + 0.001, 0, z, 16), mat)            # écrou de blocage
    return (0, u + l, z)


def prong_hider(part, u, z=0.0, r=0.0115, l=0.048, mat=METAL):
    """Cache-flammes à 3 branches (bouche ouverte)."""
    cutters = [bore(u + l, r * 0.7, l * 0.8, z)]
    for ang in (90, 210, 330):
        cutters.append(spin(box(0.004, l * 0.75, 0.03, (0, u + l * 0.65, z + r), 0.0, 1), ang - 90, 0, z))
    cut(part, loft([(u, r * 0.9, r * 0.9, z, 2.0), (u + 0.008, r, r, z, 2.0), (u + l, r, r, z, 2.0)], 20), mat, cutters)
    return (0, u + l, z)


def brake(part, u, z=0.0, r=0.017, l=0.07, rows=3, mat=METAL, top_ports=2, flats=True):
    """Frein de bouche : fentes latérales traversantes, évents supérieurs (compensateur), bouche creuse."""
    cutters = [bore(u + l, r * 0.45, l * 1.2, z)]
    for k in range(rows):
        uu = u + l * (0.22 + 0.62 * k / max(1, rows - 1))
        cutters.append(box(r * 3, l * 0.11, r * 1.1, (0, uu, z), r * 0.25, 2))
    for k in range(top_ports):
        uu = u + l * (0.3 + 0.4 * k / max(1, top_ports - 1))
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=0.0028, radius2=0.0028, depth=r * 2)
        bmesh.ops.translate(bm, vec=P(0, uu, z + r), verts=bm.verts)
        cutters.append(bm)
    p = 3.5 if flats else 2.0
    cut(part, loft([(u, r * 0.8, r * 0.8, z, p), (u + 0.006, r, r, z, p), (u + l - 0.006, r, r, z, p), (u + l, r * 0.85, r * 0.85, z, p)], 24),
        mat, cutters)
    return (0, u + l, z)


def picatinny(part, u0, u1, z, x=0.0, w=0.021, mat=METAL, side=0, numbers=False):
    """Rail Picatinny : semelle à flancs en queue d'aronde, créneaux réguliers ; side ±1 = rail latéral."""
    n = max(2, int((u1 - u0) / 0.01))
    step = (u1 - u0) / n
    if not side:
        part.add(prism([(u0, z), (u1, z), (u1, z + 0.004), (u1 - 0.002, z + 0.0095), (u0 + 0.002, z + 0.0095), (u0, z + 0.004)],
                       x - w * 0.36, x + w * 0.36, 0.0008), mat)
        part.add(loft([(u0 + 0.001, w / 2, 0.0022, z + 0.0055, 2.0), (u1 - 0.001, w / 2, 0.0022, z + 0.0055, 2.0)], 6), mat)
        for i in range(n - 1):
            uu = u0 + step * (i + 1)
            part.add(box(w * 1.02, step * 0.42, 0.0042, (x, uu, z + 0.0095), 0, 1), POLY)
    else:
        s = side
        part.add(prism([(u0, z - w * 0.36), (u1, z - w * 0.36), (u1, z + w * 0.36), (u0, z + w * 0.36)],
                       x, x + s * 0.0095, 0.0008) if s > 0 else
                 prism([(u0, z - w * 0.36), (u1, z - w * 0.36), (u1, z + w * 0.36), (u0, z + w * 0.36)], x - 0.0095, x, 0.0008), mat)
        part.add(box(0.0044, u1 - u0 - 0.002, w, (x + s * 0.0055, (u0 + u1) / 2, z), 0.001, 1), mat)
        for i in range(n - 1):
            uu = u0 + step * (i + 1)
            part.add(box(0.0042, step * 0.42, w * 1.02, (x + s * 0.0095, uu, z), 0, 1), POLY)


def aimpoint(part, u, z, mat=METAL):
    """Viseur point rouge tubulaire (type Aimpoint) : embase à collier, tube, molettes, capots rabattus. z = dessus du rail."""
    zc = z + 0.032
    part.add(prism([(u - 0.03, z), (u + 0.03, z), (u + 0.026, z + 0.012), (u - 0.026, z + 0.012)], -0.014, 0.014, 0.0015), mat)
    part.add(ring(0.0205, 0.016, u - 0.012, u + 0.012, 0, zc, 20), mat)
    part.add(box(0.024, 0.024, 0.008, (0, u, zc - 0.018), 0.002), mat)
    knob_x(part, u, z + 0.006, 0.014, 0.006, 0.008, 1, mat, 8)                          # écrou de serrage
    part.add(tube(0.0175, 0.0175, u - 0.04, u + 0.034, 0, zc, 22), mat)
    part.add(tube(0.0175, 0.0195, u + 0.034, u + 0.046, 0, zc, 22), mat)               # pare-soleil
    part.add(tube(0.0182, 0.0182, u + 0.04, u + 0.042, 0, zc, 22), LENS)
    part.add(tube(0.015, 0.015, u - 0.041, u - 0.039, 0, zc, 22), GLASS)
    knob_x(part, u + 0.008, zc, 0.0165, 0.009, 0.01, 1, mat, 10)                       # molette d'intensité
    knob_x(part, u - 0.01, zc, -0.0165, 0.007, 0.007, -1, mat, 8)                      # réglage
    part.add(tube(0.006, 0.006, u - 0.006, u + 0.006, 0, zc + 0.0215, 12), mat)       # tourelle supérieure
    # Capots de protection ouverts : l'avant basculé vers l'avant, l'arrière pendant vers l'arrière.
    part.add(tube(0.0205, 0.0205, u + 0.047, u + 0.051, 0, zc, 18), RUBBER, pitch(105, u + 0.049, zc + 0.02))
    part.add(tube(0.018, 0.018, u - 0.046, u - 0.042, 0, zc, 18), RUBBER, pitch(100, u - 0.044, zc - 0.018))
    return zc


def magnifier(part, u, z, mat=METAL):
    """Grossisseur basculant (derrière le point rouge) : tube, oculaire, embase pivotante. z = dessus du rail."""
    zc = z + 0.032
    part.add(prism([(u - 0.026, z), (u + 0.026, z), (u + 0.02, z + 0.012), (u - 0.02, z + 0.012)], -0.014, 0.014, 0.0015), mat)
    part.add(box(0.012, 0.03, 0.016, (0.012, u, z + 0.016), 0.002), mat)             # charnière de bascule
    part.add(tube_x(0.005, 0.006, (0.02, u, z + 0.016), 12), mat)
    part.add(tube(0.016, 0.016, u - 0.03, u + 0.035, 0, zc, 20), mat)
    part.add(tube(0.019, 0.016, u - 0.05, u - 0.03, 0, zc, 20), RUBBER)
    part.add(tube(0.016, 0.016, u - 0.051, u - 0.049, 0, zc, 20), LENS)
    part.add(ring(0.019, 0.015, u - 0.01, u + 0.01, 0, zc, 20), mat)
    part.add(box(0.022, 0.03, 0.01, (0, u, zc - 0.017), 0.002), mat)


def rifle_scope(part, u, z, l=0.28, r=0.0135, obj=0.022, base=0.03, mat=METAL):
    """Lunette de tir détaillée : œilleton caoutchouc, bague de grossissement moletée, tourelles à capuchons, objectif,
    colliers vissés sur embase Picatinny (dessus du rail à z = base)."""
    ue, uo = u - l / 2, u + l / 2
    part.add(tube(r * 1.55, r * 1.5, ue - 0.012, ue + 0.004, 0, z, 22), RUBBER)
    part.add(tube(r * 1.5, r * 1.3, ue + 0.004, ue + 0.05, 0, z, 22), mat)
    knurl(part, ue + 0.052, ue + 0.07, r * 1.32, z, 0, 18, mat)
    part.add(tube(r * 1.25, r, ue + 0.07, ue + 0.085, 0, z, 22), mat)
    part.add(tube(r, r, ue + 0.085, uo - 0.09, 0, z, 22), mat)
    part.add(tube(r * 1.25, r * 1.25, u - 0.025, u + 0.025, 0, z, 22), mat)           # boîtier des tourelles
    knob_x(part, u, z, r * 1.2, r * 0.7, 0.012, 1, mat, 12)
    part.add(tube_x(r * 0.55, 0.008, (-r * 1.2 - 0.004, u + 0.012, z), 14), mat)
    tt = bmesh.new()
    bmesh.ops.create_cone(tt, cap_ends=True, cap_tris=False, segments=18, radius1=r * 0.72, radius2=r * 0.68, depth=0.014)
    bmesh.ops.translate(tt, vec=P(0, u, z + r * 1.2 + 0.007), verts=tt.verts)
    part.add(tt, mat)
    for k in range(12):
        a = 2 * math.pi * k / 12
        part.add(box(0.0015, 0.0015, 0.012, (math.cos(a) * r * 0.72, u + math.sin(a) * r * 0.72, z + r * 1.2 + 0.007), 0, 1), mat)
    part.add(tube(r, obj, uo - 0.09, uo - 0.045, 0, z, 22), mat)
    part.add(tube(obj, obj, uo - 0.045, uo, 0, z, 22), mat)
    part.add(ring(obj * 1.04, obj * 0.86, uo - 0.004, uo + 0.004, 0, z, 22), mat)
    part.add(tube(obj * 0.86, obj * 0.86, uo - 0.004, uo - 0.002, 0, z, 22), LENS)
    part.add(tube(r * 1.0, r * 1.0, ue - 0.0125, ue - 0.0115, 0, z, 22), LENS)
    for uu in (ue + 0.1, uo - 0.105):
        part.add(ring(r * 1.32, r * 0.98, uu - 0.011, uu + 0.011, 0, z, 22), mat)
        hh = max(z - r - base, 0.004)
        part.add(prism([(uu - 0.016, base), (uu + 0.016, base), (uu + 0.011, base + hh + 0.004), (uu - 0.011, base + hh + 0.004)],
                       -0.011, 0.011, 0.0015), mat)
        part.add(box(0.03, 0.03, 0.007, (0, uu, base + 0.0035), 0.0015), mat)
        for s in (-1, 1):
            screw(part, uu, z - r * 0.4, s * r * 1.32, 0.002, s)
        knob_x(part, uu, base + 0.004, 0.015, 0.004, 0.006, 1, mat, 6)


def tube_z(r0, r1, z0, z1, u, x=0.0, seg=16):
    """Cylindre vertical (axe z) de z0 à z1."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=abs(z1 - z0))
    bmesh.ops.translate(bm, vec=P(x, u, (z0 + z1) / 2), verts=bm.verts)
    return bm


def holo(part, u, z, mat=METAL):
    """Viseur holographique (type EOTech) : socle à piles, capot ajouré sur la fenêtre, boutons arrière, vis de rail."""
    part.add(prism([(u - 0.05, z), (u + 0.032, z), (u + 0.032, z + 0.013), (u - 0.046, z + 0.018), (u - 0.05, z + 0.012)],
                   -0.0165, 0.0165, 0.002), mat)
    hood = prism([(u - 0.008, z + 0.008), (u + 0.032, z + 0.008), (u + 0.032, z + 0.05), (u + 0.026, z + 0.056), (u - 0.004, z + 0.056),
                  (u - 0.008, z + 0.05)], -0.0175, 0.0175, 0.002)
    cut(part, hood, mat, [box(0.028, 0.06, 0.03, (0, u + 0.012, z + 0.033), 0.003, 2)])
    part.add(box(0.027, 0.002, 0.029, (0, u + 0.02, z + 0.033), 0.0004, 1), GLASS)
    part.add(box(0.027, 0.002, 0.029, (0, u + 0.002, z + 0.033), 0.0004, 1), LENS)
    for dx in (-0.007, 0.0, 0.007):
        part.add(tube(0.0025, 0.0025, u - 0.054, u - 0.048, dx, z + 0.011, 10), RUBBER)
    knob_x(part, u - 0.03, z + 0.008, 0.0165, 0.006, 0.006, 1, mat, 8)
    knob_x(part, u + 0.018, z + 0.004, -0.0165, 0.0055, 0.005, -1, mat, 8)
    return z + 0.033


def light(part, u, z, x=0.0, r=0.0115, mat=METAL):
    """Lampe tactique : corps moleté, tête évasée, verre, embase."""
    part.add(tube(r * 0.85, r * 0.85, u - 0.07, u - 0.055, x, z, 16), mat)
    part.add(tube(r * 0.75, r * 0.75, u - 0.078, u - 0.07, x, z, 12), RUBBER)
    knurl(part, u - 0.055, u - 0.03, r, z, x, 12, mat)
    part.add(tube(r, r, u - 0.03, u + 0.01, x, z, 16), mat)
    part.add(tube(r, r * 1.35, u + 0.01, u + 0.03, x, z, 18), mat)
    part.add(ring(r * 1.42, r * 1.12, u + 0.028, u + 0.036, x, z, 18), mat)
    part.add(tube(r * 1.15, r * 1.15, u + 0.03, u + 0.032, x, z, 18), LENS)


def fore_grip(part, u, z, h=0.075, mat=POLY):
    """Poignée verticale à anneaux de prise, collier de rail, bouchon ; renvoie la main."""
    part.add(prism([(u - 0.022, z), (u + 0.022, z), (u + 0.016, z - 0.012), (u - 0.016, z - 0.012)], -0.012, 0.012, 0.0015), mat)
    knob_x(part, u, z - 0.006, 0.012, 0.004, 0.005, 1, METAL, 6)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=16, radius1=0.0135, radius2=0.0155, depth=h)
    bmesh.ops.translate(bm, vec=P(0, u, z - 0.012 - h / 2), verts=bm.verts)
    part.add(bm, mat)
    for k in range(5):
        zz = z - 0.024 - k * 0.011
        rb = bmesh.new()
        bmesh.ops.create_cone(rb, cap_ends=True, cap_tris=False, segments=16, radius1=0.0158, radius2=0.0158, depth=0.004)
        bmesh.ops.translate(rb, vec=P(0, u, zz), verts=rb.verts)
        part.add(rb, mat)
    cp = bmesh.new()
    bmesh.ops.create_cone(cp, cap_ends=True, cap_tris=False, segments=16, radius1=0.016, radius2=0.0145, depth=0.008)
    bmesh.ops.translate(cp, vec=P(0, u, z - 0.012 - h - 0.002), verts=cp.verts)
    part.add(cp, RUBBER)
    return (0, u, z - h * 0.55)


def laser_box(part, u, z, x=-0.026, mat=POLY):
    """Module laser / IR (pour opt("laser")) : boîtier à pans, deux fenêtres, interrupteur, molettes de réglage."""
    s = -1 if x < 0 else 1
    part.add(prism([(u - 0.032, z - 0.013), (u + 0.028, z - 0.013), (u + 0.032, z - 0.006), (u + 0.032, z + 0.013),
                    (u - 0.026, z + 0.013), (u - 0.032, z + 0.006)], x - 0.009, x + 0.009, 0.0015), mat)
    part.add(tube(0.0045, 0.0045, u + 0.031, u + 0.035, x, z + 0.006, 12), GLOW)
    part.add(tube(0.0055, 0.0055, u + 0.031, u + 0.035, x, z - 0.005, 12), LENS)
    part.add(box(0.004, 0.012, 0.008, (x + s * 0.0095, u - 0.016, z), 0.0012), RUBBER)
    for uu in (u + 0.004, u + 0.016):
        part.add(tube_x(0.003, 0.003, (x + s * 0.0105, uu, z + 0.006), 10), METAL)


# ───────────────────────── Famille AR (PA-11, Raven) ─────────────────────────

def ar_upper(b, u0=-0.1, u1=0.17, flat=True):
    """Boîtier supérieur AR : fenêtre d'éjection découpée + volet ouvert, déflecteur, assistance de fermeture, levier
    d'armement en T, écrou de canon ; rail si flat."""
    zc, hh = 0.0015, 0.0195
    secs = [(u0, 0.0155, hh * 0.95, zc, 5.0), (u0 + 0.006, 0.0165, hh, zc, 5.0), (u1 - 0.004, 0.0165, hh, zc, 5.0),
            (u1, 0.0158, hh * 0.96, zc, 5.0)]
    cutters = [box(0.012, 0.062, 0.017, (0.0175, 0.036, 0.0015), 0.002, 2),               # fenêtre d'éjection
               box(0.004, 0.11, 0.0025, (0.0168, 0.035, -0.014), 0, 1),                   # rainure du volet
               box(0.004, 0.11, 0.0025, (-0.0168, 0.035, -0.014), 0, 1)]
    cut(b, loft(secs, 24), METAL, cutters)
    b.add(box(0.004, 0.058, 0.014, (0.0105, 0.036, 0.0015), 0.0015), ALLOY)              # porte-culasse visible
    b.add(tube_x(0.0045, 0.002, (0.0128, 0.05, 0.0015), 12), METAL)
    # Volet anti-poussière ouvert (pend sous la fenêtre), tige de charnière.
    b.add(box(0.0022, 0.064, 0.016, (0.0192, 0.036, -0.022), 0.0008), METAL, None)
    for k in range(4):
        b.add(box(0.0012, 0.003, 0.012, (0.0205, 0.012 + k * 0.016, -0.022), 0, 1), METAL)
    b.add(tube(0.0016, 0.0016, 0.0, 0.072, 0.0178, -0.0135, 8), METAL)
    # Déflecteur d'étuis et carter d'assistance de fermeture.
    b.add(prism([(-0.013, -0.006), (0.004, -0.006), (0.001, 0.016), (-0.012, 0.013)], 0.015, 0.025, 0.0015), METAL)
    b.add(loft([(-0.075, 0.006, 0.0075, 0.006, 2.4), (-0.06, 0.0085, 0.0085, 0.006, 2.4), (-0.02, 0.0085, 0.0085, 0.006, 2.4),
                (-0.012, 0.004, 0.006, 0.006, 2.4)], 14), METAL, Matrix.Translation(P(0.0145, 0, 0)))
    b.add(tube(0.0062, 0.0062, -0.09, -0.074, 0.0145, 0.006, 14), METAL)                  # bouton
    b.add(ring(0.0068, 0.0045, -0.092, -0.088, 0.0145, 0.006, 14), METAL)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(box(0.0012, 0.012, 0.0012, (0.0145 + math.cos(a) * 0.0062, -0.082, 0.006 + math.sin(a) * 0.0062), 0, 1), METAL)
    # Levier d'armement en T et son verrou.
    b.add(prism([(u0 - 0.002, 0.008), (u0 + 0.01, 0.008), (u0 + 0.01, 0.016), (u0 - 0.002, 0.016)], -0.006, 0.006, 0.001), METAL)
    b.add(prism([(u0 - 0.014, 0.008), (u0 - 0.002, 0.008), (u0 - 0.004, 0.016), (u0 - 0.012, 0.016)], -0.022, 0.022, 0.0015), METAL)
    b.add(prism([(u0 - 0.015, 0.009), (u0 - 0.005, 0.009), (u0 - 0.007, 0.015), (u0 - 0.013, 0.015)], -0.03, -0.018, 0.0012), METAL)
    # Écrou de canon crénelé.
    b.add(tube(0.0175, 0.0175, u1, u1 + 0.014, 0, 0, 20), METAL)
    for k in range(12):
        a = 2 * math.pi * k / 12
        b.add(box(0.003, 0.012, 0.003, (math.cos(a) * 0.0175, u1 + 0.007, math.sin(a) * 0.0175), 0, 1), METAL)
    if flat:
        picatinny(b, u0 + 0.004, u1 - 0.004, 0.021)
    else:
        b.add(box(0.026, u1 - u0 - 0.01, 0.006, (0, (u0 + u1) / 2, 0.022), 0.002), METAL)


def ar_lower(b, u0=-0.09, u1=0.115, mag_u=0.07):
    """Boîtier inférieur AR (profil découpé) : puits de chargeur évasé et ouvert, pontet, sélecteur, arrêtoir, bouton
    d'éjection du chargeur avec garde, goupilles, tour du tube tampon."""
    m0, m1 = mag_u - 0.039, mag_u + 0.039
    prof = [(u0, -0.018), (u1, -0.018), (u1, -0.026), (u1 - 0.008, -0.034), (m1 + 0.002, -0.036), (m1 + 0.004, -0.079),
            (m1 - 0.002, -0.083), (m0 - 0.002, -0.083), (m0 - 0.006, -0.079), (m0 - 0.006, -0.05), (-0.025, -0.05),
            (-0.03, -0.054), (-0.062, -0.054), (-0.075, -0.044), (u0, -0.04)]
    cutters = [box(0.028, 0.066, 0.05, (0, mag_u, -0.1), 0.003, 2),                              # puits ouvert
               box(0.004, 0.06, 0.03, (0.0185, mag_u, -0.06), 0.002, 2),                         # flanc creusé du puits
               box(0.004, 0.06, 0.03, (-0.0185, mag_u, -0.06), 0.002, 2)]
    cut(b, prism(prof, -0.0175, 0.0175, 0.0018), METAL, cutters)
    b.add(prism([(m0 - 0.008, -0.074), (m1 + 0.006, -0.074), (m1 + 0.006, -0.083), (m0 - 0.008, -0.083)], -0.0195, 0.0195, 0.0015), METAL)
    # Tour du tube tampon et goupilles.
    b.add(loft([(u0 - 0.004, 0.016, 0.016, -0.008, 2.0), (u0 + 0.004, 0.016, 0.016, -0.008, 2.0)], 18), METAL)
    pins(b, (u0 + 0.01, u1 - 0.006), -0.028, 0.0175, 0.0032)
    pins(b, (-0.022, 0.0), -0.04, 0.0175, 0.0026)
    # Arrêtoir de culasse (gauche), bouton d'éjection du chargeur et sa garde (droite).
    b.add(prism([(m0 - 0.004, -0.022), (m0 + 0.008, -0.022), (m0 + 0.006, -0.042), (m0 - 0.004, -0.04)], -0.0215, -0.0175, 0.001), METAL)
    for k in range(3):
        b.add(box(0.0012, 0.009, 0.0012, (-0.0218, m0 + 0.002, -0.034 + k * 0.004), 0, 1), METAL)
    b.add(tube_x(0.0055, 0.004, (0.0195, m0 - 0.004, -0.036), 14), METAL)
    b.add(prism([(m0 - 0.014, -0.028), (m0 + 0.004, -0.028), (m0 + 0.004, -0.031), (m0 - 0.014, -0.031)], 0.017, 0.0205, 0.0008), METAL)
    b.add(prism([(m0 - 0.014, -0.041), (m0 + 0.004, -0.041), (m0 + 0.004, -0.044), (m0 - 0.014, -0.044)], 0.017, 0.0205, 0.0008), METAL)
    # Sélecteur de tir (gauche) : moyeu et levier ; repères de position.
    b.add(tube_x(0.0065, 0.003, (-0.0188, -0.044, -0.03), 14), METAL)
    b.add(prism([(-0.046, -0.032), (-0.018, -0.031), (-0.016, -0.027), (-0.046, -0.027)], -0.022, -0.0195, 0.0008), METAL)
    for k, (du, dz) in enumerate(((-0.012, 0.009), (0.0, 0.012), (0.012, 0.009))):
        b.add(box(0.0006, 0.003, 0.003, (-0.0178, -0.044 + du, -0.03 + dz), 0, 1), MARKING)
    b.add(tube_x(0.004, 0.002, (0.0185, -0.044, -0.03), 12), METAL)
    trigger_guard(b, -0.03, 0.04, -0.05, -0.075, 0.009)
    trigger(b, 0.0, -0.048)


def a2_front_sight(b, u, z=0.0, h=0.062, mat=METAL):
    """Embase de guidon triangulaire A2 ajourée, oreilles de protection, guidon, tenon de baïonnette, anneau de bretelle."""
    prof = [(u - 0.02, z - 0.013), (u + 0.02, z - 0.013), (u + 0.016, z + 0.012), (u + 0.006, z + h - 0.008), (u - 0.006, z + h - 0.008),
            (u - 0.012, z + 0.012)]
    hole = prism([(u - 0.006, z + 0.022), (u + 0.007, z + 0.022), (u + 0.003, z + h - 0.02), (u - 0.003, z + h - 0.02)], -0.03, 0.03, 0)
    cut(b, prism(prof, -0.009, 0.009, 0.002), mat, [hole])
    b.add(prism([(u - 0.007, z + h - 0.012), (u + 0.007, z + h - 0.012), (u + 0.006, z + h + 0.008), (u - 0.006, z + h + 0.008)],
                -0.011, -0.006, 0.0008), mat)
    b.add(prism([(u - 0.007, z + h - 0.012), (u + 0.007, z + h - 0.012), (u + 0.006, z + h + 0.008), (u - 0.006, z + h + 0.008)],
                0.006, 0.011, 0.0008), mat)
    b.add(box(0.0028, 0.003, 0.014, (0, u, z + h - 0.002), 0.0005, 1), mat)                 # guidon
    b.add(box(0.022, 0.034, 0.026, (0, u, z - 0.012), 0.004), mat)                          # bloc de gaz
    pins(b, (u - 0.01, u + 0.01), z - 0.016, 0.011, 0.0022)
    b.add(prism([(u + 0.008, z - 0.03), (u + 0.034, z - 0.03), (u + 0.034, z - 0.022), (u + 0.008, z - 0.02)], -0.0045, 0.0045, 0.001), mat)
    b.add(prism([(u - 0.012, z - 0.024), (u + 0.004, z - 0.024), (u + 0.001, z - 0.033), (u - 0.009, z - 0.033)], -0.0035, 0.0035, 0.001), mat)
    sling_loop(b, u - 0.004, z - 0.038, 0, 0.0075)


def pa11(g):
    """M16A4 / T65K2 : boîtiers AR, poignée de transport fixe ajourée à hausse A2, garde-main rond nervuré et ventilé, bague
    delta, embase de guidon triangulaire, cache-flammes cage à oiseaux, crosse A2 à trappe."""
    b = g.body
    ar_upper(b, -0.1, 0.17, flat=False)
    ar_lower(b, mag_u=0.07)
    # Poignée de transport A2 (évidée) : montant avant incliné, fenêtre de saisie, canal de visée, hausse à tambours.
    prof = [(-0.098, 0.024), (0.15, 0.024), (0.15, 0.03), (0.124, 0.062), (-0.04, 0.062), (-0.052, 0.074), (-0.09, 0.074), (-0.098, 0.062)]
    win = prism([(-0.035, 0.031), (0.116, 0.031), (0.104, 0.05), (-0.03, 0.05)], -0.03, 0.03, 0.0)
    chan = box(0.004, 0.17, 0.008, (0, 0.04, 0.066), 0.0, 1)
    cut(b, prism(prof, -0.0085, 0.0085, 0.0025), METAL, [win, chan])
    b.add(prism([(-0.08, 0.06), (-0.06, 0.06), (-0.062, 0.086), (-0.078, 0.086)], -0.007, 0.007, 0.0012), METAL)  # oreilles
    b.add(box(0.009, 0.006, 0.014, (0, -0.07, 0.081), 0.0012), METAL)                        # œilleton
    b.add(tube(0.0016, 0.0016, -0.075, -0.064, 0, 0.081, 8), POLY)
    knob_x(b, -0.07, 0.076, 0.0085, 0.0065, 0.006, 1, METAL, 10)                            # dérive
    b.add(tube_x(0.0115, 0.02, (0, -0.07, 0.061), 20), METAL)                               # tambour de hausse
    for k in range(16):
        a = 2 * math.pi * k / 16
        b.add(box(0.02, 0.0016, 0.0016, (0, -0.07 + math.cos(a) * 0.0115, 0.061 + math.sin(a) * 0.0115), 0, 1), METAL)
    for k in range(3):
        b.add(box(0.0006, 0.004, 0.004, (0.0089, 0.0 + k * 0.03, 0.043), 0, 1), MARKING)
    # Garde-main rond A2 nervuré, fentes de refroidissement, bague delta, chapeau avant.
    hg = fluted([(0.182, 0.03, 0.0), (0.19, 0.0335, 0.0), (0.43, 0.032, 0.0), (0.442, 0.0275, 0.0)], 14, 0.07, 56)
    vcut = []
    for ang in (55, 125):
        for k in range(8):
            uu = 0.212 + k * 0.028
            vcut.append(spin(box(0.006, 0.014, 0.012, (0, uu, 0.034), 0.0028, 2), ang - 90))
    vcut.append(box(0.0016, 0.3, 0.09, (0, 0.31, 0), 0, 1))                                  # joint des deux coquilles
    cut(b, hg, PAINT, vcut)
    b.add(tube(0.0278, 0.0278, 0.18, 0.44, 0, 0, 16), POLY)                                  # bouclier thermique interne
    b.add(tube(0.036, 0.0315, 0.162, 0.18, 0, 0.0, 24), METAL)                               # bague delta
    b.add(ring(0.0372, 0.031, 0.168, 0.174, 0, 0.0, 24), METAL)
    b.add(ring(0.0305, 0.012, 0.44, 0.452, 0, 0.0, 22), METAL)
    # Canon à épaulements, guidon, cache-flammes.
    b.add(tube(0.0105, 0.0098, 0.44, 0.49, 0, 0, 18), METAL)
    b.add(tube(0.0088, 0.0088, 0.52, 0.66, 0, 0, 18), METAL)
    a2_front_sight(b, 0.505)
    birdcage(b, 0.66, 0, 0.0115, 0.055)
    grp = grip(b, -0.042, -0.05, 22, 0.098, 0.03, 0.044, POLY, bump=0.16)
    mag = magazine(b, 0.07, -0.062, 0.175, 0.06, 0.022, bend=0.045, style="ar")
    # Crosse A2 : col étroit, trappe de rangement, plaque de couche quadrillée, vis, battant.
    butt = stock_sculpt(b, [(-0.1, 0.0175, 0.02, -0.006), (-0.135, 0.019, 0.026, -0.011), (-0.26, 0.021, 0.042, -0.024),
                            (-0.37, 0.0215, 0.052, -0.03), (-0.386, 0.0205, 0.051, -0.03)], PAINT)
    b.add(box(0.004, 0.0015, 0.084, (0, -0.3895, -0.03), 0, 1), POLY)
    b.add(box(0.03, 0.006, 0.04, (0, -0.396, -0.028), 0.002), METAL)                        # trappe
    b.add(tube(0.0035, 0.0035, -0.4, -0.393, 0, 0.006, 10), METAL)
    b.add(box(0.0015, 0.12, 0.0015, (0.0205, -0.25, -0.006), 0, 1), POLY)                   # ligne de moulage
    sling_loop(b, -0.36, -0.083, 0, 0.011)
    b.add(box(0.012, 0.02, 0.006, (0, -0.36, -0.077), 0.002), METAL)
    laser_box(g.opt("laser"), 0.36, -0.006, -0.044)
    rifle_scope(g.opt("scope"), 0.02, 0.095, 0.2, 0.012, 0.018, base=0.062)
    g.point("Muzzle", (0, 0.715, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.3, -0.03))
    g.point("Butt", butt); g.point("Mag", mag)


def quad_rail(b, u0, u1, r=0.026, mat=POLY):
    """Garde-main quadrirail (type KAC RAS) creux : pans coupés ajourés entre les rails, rails sur 4 faces."""
    secs = [(u0, r, r, 0.0, 6.0), (u1, r, r, 0.0, 6.0)]
    cutters = []
    for ang in (45, 135, 225, 315):
        for k in range(5):
            uu = u0 + 0.025 + k * (u1 - u0 - 0.05) / 4
            cutters.append(spin(box(0.008, 0.016, 0.03, (0, uu, r), 0.0035, 2), ang - 90))
    sh = shell(secs, 0.0035, 32)
    cut(b, sh, mat, cutters)
    picatinny(b, u0 + 0.004, u1 - 0.004, r - 0.002, 0, 0.021, mat)
    picatinny(b, u0 + 0.01, u1 - 0.004, 0.0, r - 0.002, 0.021, mat, 1)
    picatinny(b, u0 + 0.01, u1 - 0.004, 0.0, -r + 0.002, 0.021, mat, -1)
    b.add(prism([(u0 + 0.01, -r + 0.002), (u1 - 0.004, -r + 0.002), (u1 - 0.004, -r - 0.004), (u0 + 0.01, -r - 0.004)], -0.0075, 0.0075, 0.0008), mat)
    for i in range(int((u1 - u0 - 0.014) / 0.01) - 1):
        uu = u0 + 0.01 + (i + 1) * 0.01
        b.add(box(0.021, 0.0042, 0.0042, (0, uu, -r - 0.006), 0, 1), mat)
    b.add(box(0.021, u1 - u0 - 0.014, 0.003, (0, (u0 + u1) / 2 + 0.003, -r - 0.0055), 0.001), mat)
    # Cache-rails à lamelles sur le côté droit (prise), vis de serrage avant.
    b.add(box(0.006, 0.08, 0.02, (r + 0.011, u0 + 0.06, 0.0), 0.002), RUBBER)
    for k in range(7):
        b.add(box(0.0016, 0.004, 0.02, (r + 0.0145, u0 + 0.027 + k * 0.011, 0.0), 0, 1), RUBBER)
    for s in (-1, 1):
        screw(b, u1 - 0.008, -0.012, s * r, 0.0024, s)


def raven(g):
    """Colt 933 accessoirisé : carabine très courte à boîtier plat, garde-main quadrirail ajouré, guidon A2, cache-flammes à
    branches, crosse télescopique sur tube tampon cranté, point rouge à capots, poignée verticale, lampe."""
    b = g.body
    ar_upper(b, -0.1, 0.17, flat=True)
    ar_lower(b, mag_u=0.065)
    quad_rail(b, 0.182, 0.36)
    b.add(tube(0.0095, 0.0095, 0.36, 0.43, 0, 0, 16), METAL)
    a2_front_sight(b, 0.39, 0, 0.056)
    prong_hider(b, 0.43, 0, 0.0115, 0.05)
    # Hausse repliable (BUIS) à l'arrière du rail.
    b.add(prism([(-0.095, 0.03), (-0.065, 0.03), (-0.068, 0.038), (-0.092, 0.038)], -0.011, 0.011, 0.001), METAL)
    b.add(box(0.016, 0.024, 0.006, (0, -0.075, 0.041), 0.0015), METAL)
    knob_x(b, -0.084, 0.036, 0.011, 0.004, 0.004, 1, METAL, 6)
    grp = grip(b, -0.042, -0.05, 22, 0.098, 0.03, 0.044, POLY, bump=0.14)
    mag = magazine(b, 0.065, -0.062, 0.17, 0.06, 0.023, bend=0.045, style="pmag", mat=POLY)
    # Tube tampon cranté, écrou crénelé, plaque de bretelle, crosse télescopique CAR.
    b.add(tube(0.0158, 0.0158, -0.31, -0.096, 0, -0.008, 20), METAL)
    b.add(prism([(-0.31, -0.026), (-0.12, -0.026), (-0.12, -0.022), (-0.31, -0.022)], -0.005, 0.005, 0.0008), METAL)
    for k in range(6):
        b.add(box(0.008, 0.004, 0.004, (0, -0.24 + k * 0.016, -0.026), 0, 1), POLY)
    b.add(ring(0.02, 0.0155, -0.104, -0.094, 0, -0.008, 20), METAL)
    for k in range(8):
        a = 2 * math.pi * k / 8
        b.add(box(0.003, 0.008, 0.003, (math.cos(a) * 0.0205, -0.099, -0.008 + math.sin(a) * 0.0205), 0, 1), METAL)
    b.add(prism([(-0.096, -0.02), (-0.088, -0.02), (-0.088, -0.04), (-0.096, -0.044)], -0.019, 0.019, 0.001), METAL)
    prof = [(-0.21, 0.012), (-0.3, 0.014), (-0.315, 0.014), (-0.315, -0.075), (-0.3, -0.075), (-0.26, -0.03), (-0.21, -0.026)]
    holes = [box(0.05, 0.03, 0.01, (0, -0.295, -0.056), 0.003, 2), box(0.05, 0.018, 0.008, (0, -0.232, -0.018), 0.003, 2)]
    cut(b, prism(prof, -0.02, 0.02, 0.003), PAINT, holes)
    b.add(loft([(-0.215, 0.019, 0.0195, -0.008, 2.6), (-0.3, 0.019, 0.0195, -0.008, 2.6)], 18), PAINT)
    b.add(box(0.044, 0.012, 0.094, (0, -0.321, -0.031), 0.004), RUBBER)
    for k in range(6):
        b.add(box(0.046, 0.002, 0.003, (0, -0.327, -0.07 + k * 0.014), 0, 1), RUBBER)
    b.add(prism([(-0.27, -0.03), (-0.21, -0.03), (-0.21, -0.036), (-0.26, -0.036)], -0.004, 0.004, 0.001), PAINT)  # levier
    aimpoint(b, 0.06, 0.0305)
    magnifier(g.opt("scope"), -0.04, 0.0305)
    sv = fore_grip(b, 0.29, -0.032)
    light(b, 0.315, 0.0, 0.043)
    b.add(box(0.012, 0.03, 0.01, (0.036, 0.3, 0.0), 0.002), METAL)
    laser_box(g.opt("laser"), 0.27, 0.0, -0.044)
    g.point("Muzzle", (0, 0.48, 0.0)); g.point("Grip", grp); g.point("SupportV", sv)
    g.point("Butt", (0, -0.327, -0.031)); g.point("Mag", mag)


# ───────────────────────── Minion Buster (G36KA4) ─────────────────────────

def minion(g):
    """G36KA4 stylisé, tir explosif : coque polymère à flancs galbés et creusés, nombreuses goupilles, poignée de transport
    haute ajourée à rail et viseur holographique, levier d'armement sous la poignée, ensemble poignée-pontet intégral à
    creux de doigts, garde-main KA4 à rails et évents, chargeur translucide à tenons, crosse squelette repliable, gros frein."""
    b = g.body
    # Coque du boîtier : silhouette montante vers l'arrière, épaulement du puits, découpes latérales et fenêtre d'éjection.
    secs = [(-0.138, 0.021, 0.03, -0.006, 4.0), (-0.128, 0.025, 0.036, -0.006, 4.5), (-0.07, 0.025, 0.039, -0.01, 4.5),
            (0.06, 0.0255, 0.037, -0.009, 4.5), (0.19, 0.0245, 0.031, -0.004, 4.5), (0.2, 0.022, 0.027, -0.004, 4.0)]
    cutters = []
    for s in (-1, 1):
        cutters.append(prism([(-0.122, -0.03), (-0.068, -0.03), (-0.06, -0.018), (-0.066, 0.014), (-0.118, 0.014)], s * 0.0208, s * 0.03, 0.0))
        cutters.append(prism([(0.13, -0.03), (0.185, -0.028), (0.185, -0.012), (0.13, -0.012)], s * 0.021, s * 0.03, 0.0))
        cutters.append(box(0.004, 0.33, 0.0022, (s * 0.0255, 0.03, 0.017), 0, 1))                  # joint des coquilles
    cutters.append(box(0.014, 0.07, 0.02, (0.026, 0.06, 0.006), 0.003, 2))                      # éjection (droite)
    cutters.append(box(0.009, 0.11, 0.02, (0, 0.1, 0.03), 0.002, 2))                             # glissière du levier
    cut(b, loft(secs, 28), PAINT, cutters)
    b.add(box(0.004, 0.064, 0.016, (0.0175, 0.06, 0.006), 0.0015), ALLOY)                       # culasse visible
    b.add(prism([(-0.004, -0.004), (0.022, -0.004), (0.018, 0.02), (0.002, 0.02)], 0.02, 0.03, 0.002), PAINT)   # déflecteur
    for (uu, zz) in ((-0.128, -0.03), (-0.1, 0.012), (-0.045, -0.038), (0.03, -0.038), (0.12, 0.012), (0.17, -0.03), (0.19, 0.008)):
        b.add(tube_x(0.0042, 0.0535, (0, uu, zz), 14), METAL)
        b.add(tube_x(0.0026, 0.0545, (0, uu, zz), 10), POLY)
    # Puits de chargeur et arrêtoir à palette.
    b.add(loft([(0.056, 0.024, 0.014, -0.046, 3.5), (0.07, 0.027, 0.02, -0.052, 4.0), (0.13, 0.027, 0.02, -0.052, 4.0),
                (0.146, 0.024, 0.014, -0.046, 3.5)], 22), PAINT)
    for s in (-1, 1):
        b.add(prism([(0.07, -0.058), (0.13, -0.058), (0.126, -0.068), (0.074, -0.068)], s * 0.026, s * 0.0285, 0.0008), PAINT)
    b.add(prism([(0.045, -0.06), (0.058, -0.06), (0.058, -0.074), (0.048, -0.078)], -0.012, 0.012, 0.0015), POLY)
    # Levier d'armement (sous la poignée de transport), replié vers la gauche.
    b.add(box(0.008, 0.012, 0.016, (0, 0.152, 0.032), 0.002), METAL)
    b.add(prism([(0.146, 0.034), (0.16, 0.034), (0.158, 0.04), (0.148, 0.04)], -0.036, 0.0, 0.0015), METAL)
    b.add(tube_x(0.005, 0.01, (-0.04, 0.153, 0.037), 12), POLY)
    # Poignée de transport haute ajourée à rail supérieur, vis de fixation.
    prof = [(-0.105, 0.026), (0.18, 0.026), (0.18, 0.04), (0.158, 0.086), (-0.072, 0.086), (-0.105, 0.058)]
    win = prism([(-0.06, 0.04), (0.14, 0.04), (0.128, 0.072), (-0.05, 0.072)], -0.04, 0.04, 0.0)
    cut(b, prism(prof, -0.0175, 0.0175, 0.004), PAINT, [win, box(0.03, 0.12, 0.012, (0, 0.05, 0.04), 0.002)])
    picatinny(b, -0.068, 0.155, 0.086, 0, 0.021, METAL)
    for uu in (-0.09, 0.168):
        for s in (-1, 1):
            screw(b, uu, 0.034, s * 0.0175, 0.003, s)
    b.add(tube(0.0035, 0.0035, 0.155, 0.17, 0, 0.074, 10), METAL)                               # guidon de secours
    holo(b, 0.03, 0.0955)
    # Ensemble poignée et pontet intégral (polymère), sélecteur ambidextre.
    trigger_guard(b, -0.03, 0.054, -0.05, -0.093, 0.016, 0.007, POLY)
    b.add(prism([(-0.085, -0.04), (0.056, -0.04), (0.056, -0.054), (-0.075, -0.054)], -0.019, 0.019, 0.002), POLY)
    trigger(b, 0.008, -0.052, 0.024)
    grp = grip(b, -0.045, -0.055, 14, 0.1, 0.033, 0.048, POLY, bump=0.0, fingers=1)
    for s in (-1, 1):
        b.add(tube_x(0.0065, 0.004, (s * 0.0205, -0.06, -0.044), 14), METAL)
        b.add(prism([(-0.062, -0.047), (-0.03, -0.044), (-0.03, -0.039), (-0.062, -0.041)], s * 0.021, s * 0.024, 0.0008), METAL)
    pins(b, (-0.07, 0.04), -0.047, 0.019, 0.0028)
    # Chargeur translucide à tenons (munitions explosives : gros calibre).
    mag = magazine(b, 0.1, -0.058, 0.145, 0.07, 0.03, bend=0.03, mat=POLY, style="g36")
    # Garde-main KA4 creux à évents, rails latéraux et inférieur ; canon épais et frein de bouche.
    hsecs = [(0.198, 0.027, 0.03, -0.008, 4.0), (0.42, 0.027, 0.03, -0.008, 4.0), (0.432, 0.023, 0.025, -0.006, 3.5)]
    hc = []
    for s in (-1, 1):
        hc += slots(0.22, 0.4, 6, s * 0.027, 0.007, 0.018, 0.009, 0.012)
        hc += slots(0.24, 0.38, 4, s * 0.027, -0.018, 0.022, 0.008, 0.012)
    cut(b, shell(hsecs, 0.0035, 28), PAINT, hc)
    b.add(prism([(0.21, -0.036), (0.415, -0.036), (0.415, -0.044), (0.21, -0.044)], -0.008, 0.008, 0.0008), POLY)
    for i in range(19):
        b.add(box(0.021, 0.0042, 0.0042, (0, 0.215 + i * 0.01, -0.0455), 0, 1), POLY)
    b.add(box(0.021, 0.2, 0.003, (0, 0.3125, -0.044), 0.001), POLY)
    for s in (-1, 1):
        picatinny(b, 0.33, 0.41, -0.008, s * 0.026, 0.021, POLY, s)
    b.add(tube(0.0145, 0.0145, 0.2, 0.47, 0, 0, 18), METAL)
    b.add(ring(0.018, 0.0145, 0.44, 0.452, 0, 0, 18), METAL)
    brake(b, 0.47, 0, 0.022, 0.09, 4, METAL, 2)
    # Poignée avant inclinée à stries, lampe.
    b.add(prism([(0.32, -0.046), (0.39, -0.046), (0.394, -0.056), (0.37, -0.074), (0.35, -0.08), (0.335, -0.066)], -0.013, 0.013, 0.003), POLY)
    for k in range(5):
        b.add(box(0.027, 0.0025, 0.004, (0, 0.345 + k * 0.009, -0.07 + k * 0.004), 0, 1), POLY)
    light(b, 0.39, -0.008, 0.044)
    b.add(box(0.012, 0.03, 0.012, (0.036, 0.37, -0.008), 0.002), METAL)
    # Crosse squelette repliable : cadre ajouré, charnière verticale, appui-joue, plaque de couche.
    sp = [(-0.136, 0.024), (-0.2, 0.026), (-0.365, 0.034), (-0.38, 0.034), (-0.38, -0.074), (-0.365, -0.074), (-0.25, -0.05), (-0.136, -0.042)]
    hole = prism([(-0.19, 0.008), (-0.352, 0.016), (-0.352, -0.056), (-0.32, -0.056), (-0.245, -0.036), (-0.19, -0.03)], -0.03, 0.03, 0.0)
    cut(b, prism(sp, -0.017, 0.017, 0.004), POLY, [hole])
    b.add(tube_z(0.0075, 0.0075, -0.04, 0.026, -0.139, 0.016), METAL)
    b.add(loft([(-0.2, 0.019, 0.007, 0.03, 3.0), (-0.34, 0.019, 0.007, 0.036, 3.0)], 16), POLY)
    b.add(box(0.04, 0.014, 0.112, (0, -0.386, -0.02), 0.005), RUBBER)
    for k in range(7):
        b.add(box(0.042, 0.002, 0.004, (0, -0.393, -0.065 + k * 0.015), 0, 1), RUBBER)
    sling_loop(b, -0.37, -0.082, 0, 0.009)
    laser_box(g.opt("laser"), 0.34, -0.008, -0.044)
    g.point("Muzzle", (0, 0.56, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.36, -0.07))
    g.point("Butt", (0, -0.39, -0.02)); g.point("Mag", mag)


# ───────────────────────── Slugger NN (AK-12) et X-Orchid ─────────────────────────

def slugger(g):
    """AK-12 : boîtier embouti riveté (creux au-dessus du chargeur), couvercle à rail et hausse arrière, sélecteur long à
    cran, levier d'armement sur la culasse, garde-main à rails et évents, tube de gaz, embase de guidon, chargeur courbe en
    bakélite, frein de bouche, crosse télescopique repliable à appui-joue réglable."""
    b = g.body
    rc = [box(0.008, 0.07, 0.012, (0.0185, 0.065, 0.001), 0.002, 2),                               # éjection
          box(0.004, 0.034, 0.012, (0.0185, 0.092, -0.016), 0.004, 3),                             # creux du chargeur
          box(0.004, 0.034, 0.012, (-0.0185, 0.092, -0.016), 0.004, 3)]
    cut(b, loft([(-0.122, 0.0165, 0.024, -0.02, 8.0), (-0.116, 0.0175, 0.025, -0.02, 8.0), (0.15, 0.0175, 0.025, -0.02, 8.0),
                 (0.165, 0.019, 0.027, -0.019, 8.0)], 24), METAL, rc)
    b.add(box(0.004, 0.062, 0.01, (0.0128, 0.065, 0.001), 0.0015), ALLOY)                       # porte-culasse
    b.add(tube_x(0.0045, 0.034, (0.032, 0.122, 0.0), 12), METAL)                                 # levier d'armement
    b.add(loft([(0.115, 0.007, 0.007, 0.0, 2.0), (0.129, 0.007, 0.007, 0.0, 2.0)], 12), METAL, Matrix.Translation(P(0.05, 0, 0)))
    # Couvercle bombé à rail, bouton de verrou, hausse à œilleton.
    b.add(loft([(-0.118, 0.015, 0.011, 0.007, 2.6), (0.148, 0.016, 0.012, 0.007, 2.6), (0.158, 0.014, 0.01, 0.006, 2.6)], 22), PAINT)
    picatinny(b, -0.06, 0.145, 0.017, 0, 0.021, PAINT)
    b.add(tube(0.0045, 0.0045, -0.132, -0.118, 0, 0.004, 12), METAL)
    b.add(prism([(-0.108, 0.017), (-0.07, 0.017), (-0.074, 0.028), (-0.104, 0.03)], -0.011, 0.011, 0.0012), PAINT)
    b.add(box(0.016, 0.01, 0.014, (0, -0.1, 0.036), 0.002), PAINT)
    b.add(tube(0.0022, 0.0022, -0.106, -0.094, 0, 0.038, 8), POLY)
    # Rivets, axes, sélecteur long à cran (droite), arrêtoir de chargeur.
    for s in (-1, 1):
        for (uu, zz) in ((0.128, -0.03), (0.142, -0.03), (0.135, -0.012), (0.155, -0.022), (-0.112, -0.034), (-0.112, -0.024)):
            rivet(b, uu, zz, s * 0.0175, 0.0021, s)
        for uu in (-0.02, 0.0):
            rivet(b, uu, -0.037, s * 0.0175, 0.0026, s)
    b.add(box(0.001, 0.024, 0.002, (0.0182, -0.01, -0.041), 0, 1), METAL)                    # ressort de retenue des axes
    # Rail latéral de montage optique (queue d'aronde, côté gauche), vis.
    b.add(prism([(-0.03, -0.006), (0.075, -0.006), (0.075, -0.024), (-0.03, -0.024)], -0.0205, -0.0172, 0.0008), METAL)
    b.add(prism([(-0.028, -0.009), (0.073, -0.009), (0.073, -0.021), (-0.028, -0.021)], -0.024, -0.0205, 0.0006), METAL)
    for k in range(6):
        b.add(box(0.004, 0.003, 0.012, (-0.0232, -0.02 + k * 0.017, -0.015), 0, 1), POLY)
    for uu in (-0.024, 0.069):
        rivet(b, uu, -0.022, -0.0205, 0.0018, -1)
    b.add(prism([(-0.112, -0.004), (0.015, -0.004), (0.022, -0.009), (0.015, -0.015), (-0.098, -0.015), (-0.104, -0.022),
                 (-0.112, -0.022)], 0.0175, 0.0205, 0.0008), METAL)
    b.add(box(0.004, 0.014, 0.004, (0.022, -0.09, -0.024), 0.001), METAL)
    b.add(prism([(0.03, -0.045), (0.046, -0.045), (0.046, -0.052), (0.034, -0.062), (0.028, -0.058)], -0.007, 0.007, 0.001), METAL)
    trigger_guard(b, -0.04, 0.026, -0.045, -0.07, 0.009, 0.004)
    trigger(b, -0.006, -0.045, 0.022)
    grp = grip(b, -0.052, -0.045, 20, 0.098, 0.029, 0.04, POLY, bump=0.1)
    mag = magazine(b, 0.088, -0.035, 0.19, 0.064, 0.026, bend=0.085, mat=ACCENT, style="ak", base_mat=ACCENT)
    # Garde-main : partie basse creuse à évents et rails courts, partie haute (tube de gaz) à rail.
    lsecs = [(0.165, 0.023, 0.02, -0.01, 4.0), (0.175, 0.026, 0.023, -0.01, 4.0), (0.39, 0.025, 0.021, -0.01, 4.0), (0.4, 0.021, 0.017, -0.009, 3.5)]
    lc = []
    for s in (-1, 1):
        lc += slots(0.2, 0.36, 5, s * 0.025, -0.018, 0.02, 0.007, 0.012)
    cut(b, shell(lsecs, 0.003, 26), POLY, lc)
    for s in (-1, 1):
        picatinny(b, 0.27, 0.37, -0.004, s * 0.025, 0.016, POLY, s)
    b.add(loft([(0.165, 0.017, 0.011, 0.024, 3.0), (0.4, 0.016, 0.011, 0.024, 3.0), (0.41, 0.013, 0.009, 0.024, 2.6)], 18), POLY)
    picatinny(b, 0.18, 0.39, 0.033, 0, 0.019, POLY)
    b.add(tube(0.0075, 0.0075, 0.4, 0.44, 0, 0.024, 12), METAL)                                 # tube de gaz
    b.add(ring(0.027, 0.018, 0.158, 0.166, 0, -0.01, 20), METAL)
    b.add(tube(0.0098, 0.0098, 0.16, 0.6, 0, 0, 16), METAL)
    # Embase de guidon à capuchon ouvert, bloc de gaz, tenon de baïonnette.
    b.add(prism([(0.432, -0.012), (0.465, -0.012), (0.465, 0.032), (0.442, 0.032), (0.432, 0.016)], -0.011, 0.011, 0.002), METAL)
    b.add(prism([(0.548, -0.012), (0.58, -0.012), (0.576, 0.012), (0.568, 0.046), (0.558, 0.046), (0.552, 0.012)], -0.0085, 0.0085, 0.002), METAL)
    cut(b, tube(0.0105, 0.0105, 0.556, 0.572, 0, 0.045, 18), METAL, [box(0.006, 0.05, 0.04, (0, 0.564, 0.05), 0, 1),
                                                                     tube(0.0085, 0.0085, 0.55, 0.58, 0, 0.045, 16)])
    b.add(box(0.0028, 0.003, 0.012, (0, 0.564, 0.048), 0.0005, 1), METAL)
    b.add(box(0.008, 0.02, 0.01, (0, 0.562, -0.016), 0.002), METAL)
    brake(b, 0.6, 0, 0.0165, 0.075, 3, METAL, 2)
    # Crosse : charnière, tige télescopique crantée, corps squelette, appui-joue réglable, plaque de couche.
    b.add(prism([(-0.135, 0.006), (-0.12, 0.006), (-0.12, -0.044), (-0.135, -0.04)], -0.018, 0.018, 0.002), METAL)
    b.add(tube_z(0.006, 0.006, -0.044, 0.008, -0.13, 0.019), METAL)
    b.add(loft([(-0.135, 0.012, 0.016, -0.016, 3.0), (-0.24, 0.012, 0.016, -0.016, 3.0)], 16), METAL)
    for k in range(7):
        b.add(box(0.026, 0.004, 0.003, (0, -0.15 - k * 0.012, -0.032), 0, 1), POLY)
    sp = [(-0.205, 0.016), (-0.41, 0.026), (-0.428, 0.026), (-0.428, -0.08), (-0.41, -0.08), (-0.3, -0.05), (-0.205, -0.036)]
    hl = [prism([(-0.31, -0.002), (-0.395, 0.004), (-0.395, -0.062), (-0.34, -0.046)], -0.03, 0.03, 0.0),
          prism([(-0.225, 0.002), (-0.29, -0.004), (-0.29, -0.026), (-0.225, -0.022)], -0.03, 0.03, 0.0)]
    cut(b, prism(sp, -0.018, 0.018, 0.004), PAINT, hl)
    b.add(loft([(-0.27, 0.0175, 0.008, 0.03, 3.0), (-0.4, 0.0175, 0.008, 0.036, 3.0)], 16), POLY)
    for uu in (-0.29, -0.38):
        b.add(tube_z(0.0028, 0.0028, 0.015, 0.03, uu), METAL)
    knob_x(b, -0.33, 0.026, 0.0175, 0.005, 0.005, 1, METAL, 8)
    b.add(box(0.04, 0.014, 0.112, (0, -0.433, -0.027), 0.005), RUBBER)
    for k in range(7):
        b.add(box(0.042, 0.002, 0.004, (0, -0.44, -0.072 + k * 0.015), 0, 1), RUBBER)
    laser_box(g.opt("laser"), 0.32, -0.006, -0.042)
    rifle_scope(g.opt("scope"), 0.04, 0.075, 0.22, 0.012, 0.018, base=0.0265)
    g.point("Muzzle", (0, 0.675, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.3, -0.04))
    g.point("Butt", (0, -0.44, -0.027)); g.point("Mag", mag)


def orchid(g):
    """X400/X500-Orchid : Slugger expérimental. Boîtier à facettes (pans coupés), manchon hexagonal ajouré laissant voir la
    bobine d'accélération lumineuse, anneaux de confinement, bouche fendue, visée intégrée, chargeur courbe à jauge,
    câbles d'alimentation, crosse à trou de pouce facettée."""
    b = g.body
    rc = [box(0.012, 0.06, 0.012, (0.022, 0.06, 0.004), 0.002, 2)]
    for s in (-1, 1):
        rc.append(box(0.004, 0.2, 0.0022, (s * 0.021, 0.04, -0.012), 0, 1))                     # lignes de panneau
        rc.append(box(0.004, 0.0022, 0.04, (s * 0.021, -0.07, -0.016), 0, 1))
        rc.append(box(0.004, 0.0022, 0.03, (s * 0.021, 0.14, -0.026), 0, 1))
        rc += slots(-0.115, -0.08, 3, s * 0.021, -0.004, 0.006, 0.02, 0.004)                   # évents
    prof = [(-0.13, -0.036), (-0.13, 0.012), (-0.112, 0.022), (0.12, 0.024), (0.18, 0.016), (0.18, -0.022), (0.15, -0.042),
            (0.04, -0.046), (-0.09, -0.046)]
    cut(b, prism(prof, -0.021, 0.021, 0.006), PAINT, rc)
    b.add(box(0.004, 0.054, 0.01, (0.0165, 0.06, 0.004), 0.0015), ALLOY)
    for s in (-1, 1):
        b.add(prism([(0.0, -0.016), (0.11, -0.016), (0.118, -0.024), (0.11, -0.038), (0.0, -0.038)], s * 0.0205, s * 0.0235, 0.001), ACCENT)
        for uu in (0.008, 0.1):
            screw(b, uu, -0.027, s * 0.0235, 0.0022, s)
    b.add(loft([(-0.11, 0.014, 0.01, 0.018, 2.0), (0.12, 0.016, 0.012, 0.022, 2.0), (0.16, 0.012, 0.008, 0.018, 2.0)], 6), ACCENT)
    # Visée intégrée : capot long à lentilles avant et arrière, voyant.
    b.add(loft([(-0.08, 0.012, 0.012, 0.04, 2.0), (-0.06, 0.016, 0.016, 0.042, 2.0), (0.08, 0.016, 0.016, 0.042, 2.0),
                (0.1, 0.013, 0.013, 0.04, 2.0)], 8), PAINT)
    b.add(tube(0.011, 0.011, 0.099, 0.101, 0, 0.04, 8), LENS)
    b.add(tube(0.009, 0.009, -0.081, -0.079, 0, 0.04, 8), GLASS)
    b.add(box(0.034, 0.012, 0.004, (0, 0.02, 0.057), 0.001), ENERGY)
    for uu in (-0.04, 0.06):
        b.add(prism([(uu - 0.012, 0.028), (uu + 0.012, 0.028), (uu + 0.008, 0.034), (uu - 0.008, 0.034)], -0.01, 0.01, 0.001), METAL)
    # Sélecteur AK et levier d'armement (héritage du Slugger).
    b.add(prism([(-0.1, -0.006), (0.0, -0.006), (0.006, -0.011), (0.0, -0.016), (-0.1, -0.016)], 0.0205, 0.0235, 0.0008), METAL)
    b.add(tube_x(0.0045, 0.03, (0.034, 0.118, 0.004), 12), METAL)
    b.add(tube_x(0.0065, 0.008, (0.05, 0.118, 0.004), 12), ACCENT)
    trigger_guard(b, -0.04, 0.03, -0.044, -0.07, 0.012, 0.005, PAINT)
    trigger(b, -0.004, -0.044, 0.022)
    grp = grip(b, -0.05, -0.044, 18, 0.1, 0.03, 0.042, POLY, bump=0.0, fingers=1)
    mag = magazine(b, 0.085, -0.04, 0.18, 0.062, 0.026, bend=0.08, mat=POLY, style="ak", base_mat=PAINT)
    b.add(curve_strip(0.085, -0.04, 0.18, 0.062, 0.08, 0.4, 0.6, -0.0145, 0.0145, 0.3, 0.8, 0.0, 6, 0.0005), ENERGY)
    # Manchon hexagonal ajouré, bobine lumineuse, anneaux de confinement.
    hsecs = [(0.175, 0.028, 0.028, -0.002, 2.0), (0.19, 0.031, 0.031, -0.002, 2.0), (0.47, 0.029, 0.029, -0.002, 2.0),
             (0.5, 0.022, 0.022, -0.002, 2.0)]
    hc = []
    for ang in (30, 90, 150, 210, 270, 330):
        for k in range(4):
            hc.append(spin(box(0.016, 0.046, 0.016, (0, 0.225 + k * 0.064, 0.026), 0.003, 2), ang - 90, 0, -0.002))
    cut(b, carve(loft(hsecs, 6), [loft([(0.16, 0.024, 0.024, -0.002, 2.0), (0.52, 0.024, 0.024, -0.002, 2.0)], 6)], SOLVER), PAINT, hc)
    b.add(tube(0.016, 0.016, 0.18, 0.49, 0, -0.002, 14), ENERGY)
    for k in range(13):
        uu = 0.2 + k * 0.022
        b.add(ring(0.0215, 0.015, uu, uu + 0.008, 0, -0.002, 14), METAL)
    for uu in (0.19, 0.36):
        b.add(loft([(uu, 0.034, 0.034, -0.002, 2.0), (uu + 0.014, 0.034, 0.034, -0.002, 2.0)], 6), ACCENT)
    # Canon et bouche fendue.
    b.add(tube(0.011, 0.011, 0.49, 0.58, 0, 0, 16), METAL)
    mz = loft([(0.56, 0.017, 0.017, 0, 2.0), (0.64, 0.019, 0.019, 0, 2.0), (0.65, 0.016, 0.016, 0, 2.0)], 8)
    cut(b, mz, ACCENT, [bore(0.65, 0.007, 0.1), box(0.05, 0.05, 0.005, (0, 0.62, 0.0), 0, 1), box(0.005, 0.05, 0.05, (0, 0.62, 0.0), 0, 1)])
    # Câbles d'alimentation du boîtier au manchon, cellule latérale.
    for s in (-1, 1):
        b.add(box(0.008, 0.016, 0.012, (s * 0.022, 0.15, -0.03), 0.002), METAL)
        b.add(box(0.008, 0.016, 0.012, (s * 0.03, 0.27, -0.02), 0.002), METAL)
        b.add(cable([(s * 0.025, 0.155, -0.032), (s * 0.03, 0.18, -0.036), (s * 0.034, 0.22, -0.032), (s * 0.034, 0.262, -0.022)], 0.003), RUBBER)
    energy_cell(b, 0.045, 0.004, -0.0265, 0.07, 0.008)
    # Crosse à trou de pouce, facettée, plaque de couche.
    sp = [(-0.128, 0.018), (-0.2, 0.026), (-0.4, 0.034), (-0.416, 0.034), (-0.416, -0.07), (-0.4, -0.07), (-0.27, -0.046),
          (-0.17, -0.06), (-0.128, -0.04)]
    hl = [prism([(-0.175, -0.008), (-0.215, -0.004), (-0.225, -0.03), (-0.19, -0.042)], -0.03, 0.03, 0.0),
          prism([(-0.29, 0.012), (-0.385, 0.018), (-0.385, -0.05), (-0.31, -0.034)], -0.03, 0.03, 0.0)]
    cut(b, prism(sp, -0.019, 0.019, 0.005), PAINT, hl)
    b.add(prism([(-0.2, 0.028), (-0.4, 0.036), (-0.4, 0.03), (-0.2, 0.022)], -0.021, 0.021, 0.002), ACCENT)
    b.add(box(0.042, 0.014, 0.11, (0, -0.42, -0.018), 0.005), RUBBER)
    laser_box(g.opt("laser"), 0.3, -0.002, -0.046)
    g.point("Muzzle", (0, 0.65, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.3, -0.04))
    g.point("Butt", (0, -0.425, -0.018)); g.point("Mag", mag)


# ───────────────────────── G&M (Type 89, Type 64) ─────────────────────────

def slotted_hider(part, u, z=0.0, r=0.012, l=0.06, n=6, mat=METAL):
    """Cache-flammes à fentes longitudinales réparties sur tout le tour, bouche creuse."""
    cutters = [bore(u + l, r * 0.62, l * 0.9, z)]
    for k in range(n):
        cutters.append(spin(box(0.0035, l * 0.6, 0.012, (0, u + l * 0.62, z + r), 0.0012, 2), 360 * k / n, 0, z))
    cut(part, loft([(u, r * 0.85, r * 0.85, z, 2.0), (u + 0.008, r, r, z, 2.0), (u + l - 0.004, r, r, z, 2.0),
                    (u + l, r * 0.9, r * 0.9, z, 2.0)], 20), mat, cutters)


def gm(g):
    """Type 89 (tireur d'élite léger) : boîtier supérieur à hausse à tambour, levier d'armement à droite, boîtier inférieur
    à puits évasé et grand sélecteur, garde-main deux tons (bas nervuré, haut ajouré), embase de guidon à oreilles, bipied
    replié vers l'avant, cache-flammes à fentes, crosse fixe à trappe, chargeur 20 coups, lunette fixe sur embase."""
    b = g.body
    uc = [box(0.012, 0.06, 0.014, (0.0175, 0.045, 0.0), 0.002, 2)]
    cut(b, loft([(-0.112, 0.015, 0.018, 0.001, 6.0), (-0.105, 0.0165, 0.0195, 0.0015, 6.0), (0.168, 0.0165, 0.0195, 0.0015, 6.0),
                 (0.175, 0.0155, 0.018, 0.001, 6.0)], 24), METAL, uc)
    b.add(box(0.004, 0.056, 0.012, (0.011, 0.045, 0.0), 0.0015), ALLOY)
    b.add(tube_x(0.005, 0.03, (0.03, 0.098, 0.004), 12), METAL)                                  # levier d'armement
    b.add(tube_x(0.0075, 0.008, (0.046, 0.098, 0.004), 14), METAL)
    b.add(prism([(0.075, 0.0), (0.098, 0.0), (0.098, 0.008), (0.075, 0.008)], 0.0165, 0.0185, 0.0006), METAL)
    # Hausse à tambour (oreilles de protection, œilleton, molettes).
    b.add(prism([(-0.105, 0.021), (-0.06, 0.021), (-0.066, 0.03), (-0.1, 0.03)], -0.012, 0.012, 0.0012), METAL)
    for s in (-1, 1):
        b.add(prism([(-0.096, 0.028), (-0.07, 0.028), (-0.074, 0.05), (-0.092, 0.05)], s * 0.0065, s * 0.011, 0.0008), METAL)
    b.add(box(0.012, 0.008, 0.012, (0, -0.083, 0.038), 0.0015), METAL)
    b.add(tube(0.0018, 0.0018, -0.088, -0.078, 0, 0.04, 8), POLY)
    knob_x(b, -0.083, 0.026, 0.012, 0.0065, 0.006, 1, METAL, 10)
    knob_x(b, -0.083, 0.026, -0.012, 0.0065, 0.006, -1, METAL, 10)
    picatinny(b, -0.05, 0.16, 0.021, 0, 0.021, METAL)
    # Boîtier inférieur à puits évasé.
    lp = [(-0.1, -0.016), (0.128, -0.016), (0.128, -0.03), (0.116, -0.04), (0.108, -0.074), (0.1, -0.08), (0.034, -0.08), (0.03, -0.05),
          (-0.028, -0.05), (-0.034, -0.055), (-0.07, -0.055), (-0.1, -0.032)]
    cut(b, prism(lp, -0.0175, 0.0175, 0.002), METAL, [box(0.027, 0.062, 0.05, (0, 0.07, -0.1), 0.003, 2),
                                                     box(0.004, 0.05, 0.02, (0.0185, 0.07, -0.062), 0.002, 2),
                                                     box(0.004, 0.05, 0.02, (-0.0185, 0.07, -0.062), 0.002, 2)])
    pins(b, (-0.085, 0.118), -0.026, 0.0175, 0.0032)
    pins(b, (-0.02, 0.0), -0.038, 0.0175, 0.0026)
    # Grand sélecteur à droite (cadran repéré), arrêtoir de chargeur.
    b.add(tube_x(0.0095, 0.003, (0.019, -0.05, -0.03), 18), METAL)
    b.add(prism([(-0.052, -0.026), (-0.02, -0.032), (-0.02, -0.037), (-0.052, -0.034)], 0.019, 0.023, 0.0008), METAL)
    for k in range(4):
        a = math.radians(100 + k * 35)
        b.add(box(0.0006, 0.0025, 0.0025, (0.0179, -0.05 + math.cos(a) * 0.014, -0.03 + math.sin(a) * 0.014), 0, 1), MARKING)
    b.add(tube_x(0.005, 0.004, (0.0195, 0.03, -0.034), 12), METAL)
    trigger_guard(b, -0.03, 0.03, -0.05, -0.074, 0.009)
    trigger(b, -0.002, -0.048)
    grp = grip(b, -0.044, -0.052, 20, 0.098, 0.03, 0.043, POLY, bump=0.0, fingers=1)
    mag = magazine(b, 0.07, -0.064, 0.13, 0.058, 0.023, bend=0.02, style="ar")
    # Garde-main deux tons : bas nervuré (peint), haut ajouré (polymère), bague, chapeau.
    lo = [(0.175, 0.026, 0.026, -0.004, 2.6), (0.184, 0.029, 0.03, -0.004, 2.6), (0.45, 0.028, 0.029, -0.004, 2.6),
          (0.462, 0.023, 0.024, -0.003, 2.4)]
    lc = [box(0.08, 0.32, 0.05, (0, 0.32, 0.024), 0, 1)]
    for s in (-1, 1):
        for k in range(3):
            lc.append(box(0.004, 0.24, 0.0022, (s * 0.028, 0.32, -0.012 - k * 0.007), 0, 1))
    cut(b, loft(lo, 26), PAINT, lc)
    up = [(0.176, 0.026, 0.024, 0.0, 2.4), (0.186, 0.027, 0.027, 0.0, 2.4), (0.45, 0.026, 0.026, 0.0, 2.4), (0.46, 0.022, 0.022, 0.0, 2.4)]
    upc = [box(0.08, 0.32, 0.03, (0, 0.32, -0.016), 0, 1)]
    for ang in (60, 90, 120):
        for k in range(7):
            upc.append(spin(box(0.006, 0.022, 0.014, (0, 0.21 + k * 0.034, 0.026), 0.0028, 2), ang - 90))
    cut(b, carve(loft(up, 26), [loft([(0.17, 0.022, 0.022, 0.0, 2.4), (0.47, 0.022, 0.022, 0.0, 2.4)], 20)], SOLVER), POLY, upc)
    b.add(ring(0.031, 0.016, 0.168, 0.178, 0, -0.002, 22), METAL)
    b.add(tube(0.0105, 0.0105, 0.17, 0.48, 0, 0, 16), METAL)
    b.add(tube(0.0085, 0.0085, 0.18, 0.47, 0, 0.016, 10), METAL)                                 # tube de gaz
    b.add(tube(0.0092, 0.0092, 0.46, 0.74, 0, 0, 18), METAL)
    # Embase de guidon à oreilles, bloc de gaz, anneau de bretelle.
    b.add(prism([(0.588, -0.014), (0.622, -0.014), (0.618, 0.012), (0.612, 0.04), (0.598, 0.04), (0.592, 0.012)], -0.009, 0.009, 0.002), METAL)
    for s in (-1, 1):
        b.add(prism([(0.596, 0.03), (0.614, 0.03), (0.612, 0.058), (0.6, 0.058)], s * 0.005, s * 0.0095, 0.0008), METAL)
    b.add(box(0.0028, 0.004, 0.016, (0, 0.605, 0.05), 0.0005, 1), METAL)
    b.add(box(0.024, 0.03, 0.03, (0, 0.475, 0.004), 0.004), METAL)
    knob_x(b, 0.475, 0.012, 0.012, 0.005, 0.005, 1, METAL, 8)
    sling_loop(b, 0.6, -0.028, 0, 0.008)
    slotted_hider(b, 0.74, 0, 0.0115, 0.058, 6)
    # Bipied replié vers l'avant (pieds en griffe), collier.
    b.add(ring(0.016, 0.009, 0.49, 0.505, 0, 0, 18), METAL)
    b.add(box(0.034, 0.02, 0.012, (0, 0.497, -0.018), 0.003), METAL)
    for s in (-1, 1):
        b.add(tube(0.0045, 0.0045, 0.497, 0.69, s * 0.013, -0.02, 10), METAL)
        b.add(tube(0.0035, 0.0035, 0.6, 0.71, s * 0.013, -0.02, 10), ALLOY)
        b.add(prism([(0.7, -0.012), (0.722, -0.016), (0.718, -0.028), (0.7, -0.026)], s * 0.009, s * 0.017, 0.0012), METAL)
    # Crosse fixe : col, crosse évidée, trappe de nettoyage, battant, plaque de couche.
    butt = stock_sculpt(b, [(-0.1, 0.017, 0.021, -0.008), (-0.15, 0.019, 0.029, -0.014), (-0.3, 0.021, 0.044, -0.026),
                            (-0.4, 0.021, 0.054, -0.03), (-0.415, 0.02, 0.053, -0.03)], PAINT)
    b.add(box(0.0016, 0.18, 0.0018, (0.0205, -0.27, 0.0), 0, 1), POLY)
    b.add(box(0.0016, 0.18, 0.0018, (-0.0205, -0.27, 0.0), 0, 1), POLY)
    b.add(prism([(-0.36, -0.07), (-0.33, -0.065), (-0.33, -0.077), (-0.36, -0.083)], -0.012, 0.012, 0.002), METAL)
    sling_loop(b, -0.345, -0.086, 0, 0.009)
    for s in (-1, 1):
        screw(b, -0.12, -0.012, s * 0.018, 0.0025, s)
    rifle_scope(b, 0.035, 0.07, 0.26, 0.014, 0.021, base=0.0305)
    laser_box(g.opt("laser"), 0.38, -0.008, -0.044)
    g.point("Muzzle", (0, 0.8, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.32, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


def gm64(g):
    """Type 64 (G&M-A25/A29) : bois et acier. Boîtier haut, levier d'armement à boule, hausse et guidon repliables, garde-
    main en bois à rainures sous un capot ajouré, régulateur de gaz, grand frein de bouche, bipied à la bouche replié vers
    l'avant, crosse droite en bois à appui d'épaule repliable et poignée séparée, lunette sur montage latéral."""
    b = g.body
    rc = [box(0.012, 0.07, 0.014, (0.0185, 0.05, 0.0), 0.002, 2)]
    cut(b, loft([(-0.12, 0.0165, 0.032, -0.012, 7.0), (-0.112, 0.0185, 0.034, -0.012, 7.0), (0.17, 0.0185, 0.034, -0.012, 7.0),
                 (0.18, 0.017, 0.03, -0.01, 7.0)], 24), METAL, rc)
    b.add(box(0.004, 0.066, 0.012, (0.0125, 0.05, 0.0), 0.0015), ALLOY)
    b.add(loft([(-0.115, 0.014, 0.006, 0.022, 3.0), (0.17, 0.014, 0.006, 0.022, 3.0)], 16), METAL)
    b.add(tube_x(0.0045, 0.034, (0.034, 0.11, 0.006), 12), METAL)
    b.add(loft([(0.104, 0.0085, 0.0085, 0.006, 2.0), (0.116, 0.0085, 0.0085, 0.006, 2.0)], 12), METAL, Matrix.Translation(P(0.055, 0, 0)))
    for s in (-1, 1):
        for (uu, zz) in ((-0.1, -0.03), (0.16, -0.03), (0.16, 0.006), (-0.1, 0.006)):
            rivet(b, uu, zz, s * 0.0185, 0.0024, s)
    # Hausse repliable relevée, sûreté-sélecteur rotative (« ア・タ・レ »).
    b.add(prism([(-0.105, 0.026), (-0.08, 0.026), (-0.084, 0.03), (-0.1, 0.03)], -0.011, 0.011, 0.001), METAL)
    b.add(prism([(-0.098, 0.03), (-0.088, 0.03), (-0.09, 0.068), (-0.096, 0.068)], -0.008, 0.008, 0.001), METAL)
    cut(b, box(0.012, 0.006, 0.012, (0, -0.093, 0.062), 0.0015), METAL, [tube(0.002, 0.002, -0.1, -0.08, 0, 0.062, 8)])
    b.add(tube_x(0.011, 0.004, (0.0205, -0.045, -0.02), 18), METAL)
    b.add(prism([(-0.048, -0.02), (-0.03, -0.012), (-0.028, -0.016), (-0.046, -0.025)], 0.022, 0.026, 0.0008), METAL)
    for k in range(3):
        a = math.radians(110 + k * 45)
        b.add(box(0.0006, 0.003, 0.003, (0.0189, -0.045 + math.cos(a) * 0.016, -0.02 + math.sin(a) * 0.016), 0, 1), MARKING)
    b.add(prism([(0.035, -0.046), (0.105, -0.046), (0.105, -0.06), (0.035, -0.06)], -0.0195, 0.0195, 0.0015), METAL)
    pins(b, (-0.025, 0.0), -0.036, 0.0185, 0.0026)
    trigger_guard(b, -0.03, 0.028, -0.046, -0.07, 0.009)
    trigger(b, -0.002, -0.044)
    grp = grip(b, -0.045, -0.046, 22, 0.092, 0.03, 0.044, PAINT, bump=0.0, fingers=0, stipple=True)
    for s in (-1, 1):
        screw(b, -0.066, -0.085, s * 0.0162, 0.0025, s)
    mag = magazine(b, 0.07, -0.056, 0.115, 0.058, 0.024, bend=0.012, style="ar")
    # Garde-main en bois à rainures, capot supérieur ajouré, régulateur de gaz.
    wc = []
    for s in (-1, 1):
        for k in range(2):
            wc.append(box(0.004, 0.22, 0.003, (s * 0.027, 0.32, -0.018 - k * 0.01), 0, 1))
    cut(b, loft([(0.18, 0.024, 0.026, -0.012, 3.0), (0.192, 0.0275, 0.03, -0.012, 3.0), (0.44, 0.0265, 0.028, -0.012, 3.0),
                 (0.452, 0.022, 0.022, -0.01, 3.0)], 24), PAINT, wc)
    hc = []
    for k in range(9):
        hc.append(tube_z(0.0045, 0.0045, 0.0, 0.05, 0.205 + k * 0.026))
    hcs = [(0.182, 0.02, 0.012, 0.02, 2.4), (0.45, 0.02, 0.012, 0.02, 2.4)]
    cut(b, shell(hcs, 0.0025, 20), METAL, hc)
    b.add(tube(0.008, 0.008, 0.19, 0.45, 0, 0.02, 10), POLY)
    b.add(tube(0.0105, 0.0105, 0.18, 0.5, 0, 0, 16), METAL)
    b.add(box(0.026, 0.044, 0.03, (0, 0.475, 0.008), 0.004), METAL)
    knob_x(b, 0.47, 0.02, 0.013, 0.007, 0.006, 1, METAL, 10)
    b.add(tube(0.0095, 0.0095, 0.5, 0.76, 0, 0, 18), METAL)
    b.add(prism([(0.62, 0.008), (0.66, 0.008), (0.656, 0.016), (0.624, 0.016)], -0.008, 0.008, 0.001), METAL)
    b.add(prism([(0.636, 0.014), (0.646, 0.014), (0.644, 0.056), (0.638, 0.056)], -0.0025, 0.0025, 0.0006), METAL)
    # Bipied fixé sous le canon, jambes repliées vers la bouche.
    b.add(box(0.03, 0.026, 0.02, (0, 0.53, -0.016), 0.004), METAL)
    for s in (-1, 1):
        b.add(tube(0.005, 0.0045, 0.53, 0.72, s * 0.012, -0.022, 10), METAL)
        b.add(prism([(0.718, -0.016), (0.738, -0.022), (0.735, -0.032), (0.716, -0.03)], s * 0.008, s * 0.017, 0.0012), METAL)
    brake(b, 0.76, 0, 0.0195, 0.085, 4, METAL, 3, flats=False)
    # Crosse droite en bois, plaque de couche à appui d'épaule repliable, plaque de joue métallique.
    butt = stock_sculpt(b, [(-0.118, 0.018, 0.03, -0.014), (-0.16, 0.019, 0.034, -0.018), (-0.3, 0.021, 0.045, -0.026),
                            (-0.4, 0.0215, 0.05, -0.028), (-0.41, 0.021, 0.05, -0.028)], PAINT, pad=False)
    b.add(loft([(-0.41, 0.022, 0.051, -0.028, 5.0), (-0.42, 0.022, 0.051, -0.028, 5.0)], 20), METAL)
    b.add(prism([(-0.425, 0.02), (-0.415, 0.02), (-0.4, 0.06), (-0.41, 0.064)], -0.012, 0.012, 0.0015), METAL)
    b.add(tube_x(0.004, 0.026, (0, -0.418, 0.02), 10), METAL)
    b.add(prism([(-0.17, 0.012), (-0.33, 0.016), (-0.33, 0.022), (-0.17, 0.018)], -0.02, 0.02, 0.0015), METAL)
    for uu in (-0.18, -0.32):
        b.add(tube_z(0.0026, 0.0026, 0.016, 0.022 if uu < -0.2 else 0.019, uu), METAL)
    sling_loop(b, -0.36, -0.078, 0, 0.009)
    # Lunette sur montage latéral gauche (bras déporté au-dessus du boîtier).
    b.add(prism([(-0.075, -0.02), (0.07, -0.02), (0.07, 0.008), (-0.075, 0.008)], -0.0225, -0.0185, 0.0012), METAL)
    for uu in (-0.06, 0.055):
        knob_x(b, uu, -0.006, -0.0225, 0.0045, 0.004, -1, METAL, 8)
    b.add(prism([(-0.06, 0.004), (0.06, 0.004), (0.05, 0.046), (-0.05, 0.046)], -0.022, -0.016, 0.0015), METAL)
    b.add(prism([(-0.05, 0.038), (0.05, 0.038), (0.05, 0.046), (-0.05, 0.046)], -0.02, 0.012, 0.0012), METAL)
    rifle_scope(b, 0.03, 0.075, 0.25, 0.0135, 0.02, base=0.046)
    laser_box(g.opt("laser"), 0.38, -0.012, -0.044)
    g.point("Muzzle", (0, 0.845, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.32, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Stork (FAMAS × F2000, Tavor) ─────────────────────────

def stork(g):
    """T1-T3 Stork : FAMAS F1 « débullpupé » × F2000. Carénage polymère enveloppant à lignes de joint, longue poignée de
    transport ajourée abritant le levier d'armement et les organes de visée, garde-main intégral (toute la main), bipied
    replié le long des flancs, anneaux lance-grenades sur le canon, fenêtres d'éjection des deux côtés, crosse évidée."""
    b = g.body
    secs = [(-0.135, 0.02, 0.036, -0.018, 2.8), (-0.115, 0.028, 0.048, -0.015, 3.2), (0.2, 0.031, 0.05, -0.012, 3.4),
            (0.36, 0.028, 0.04, -0.008, 3.2), (0.43, 0.02, 0.026, -0.004, 2.8), (0.445, 0.014, 0.018, -0.002, 2.6)]
    bc = [box(0.012, 0.072, 0.018, (0.031, 0.03, 0.008), 0.003, 2), box(0.012, 0.072, 0.018, (-0.031, 0.03, 0.008), 0.003, 2)]
    for s in (-1, 1):
        bc.append(box(0.006, 0.5, 0.0022, (s * 0.03, 0.16, -0.03), 0, 1))                         # joint latéral
        bc += slots(0.24, 0.38, 5, s * 0.03, -0.004, 0.018, 0.009, 0.008)
    bc.append(shell([(0.2135, 0.041, 0.06, -0.012, 3.4), (0.2165, 0.041, 0.06, -0.012, 3.4)], 0.0109, 28))   # joint avant
    cut(b, loft(secs, 28), PAINT, bc)
    b.add(box(0.004, 0.066, 0.014, (0.0235, 0.03, 0.008), 0.0015), ALLOY)
    b.add(box(0.003, 0.07, 0.018, (-0.0265, 0.03, 0.008), 0.0012), POLY)                      # volet gauche fermé
    b.add(prism([(-0.01, 0.0), (0.002, 0.0), (0.0, 0.024), (-0.008, 0.02)], 0.026, 0.038, 0.0015), ACCENT)
    # Poignée de transport longue (FAMAS) : montants, fenêtre, levier d'armement intérieur, hausse et guidon protégés.
    prof = [(-0.07, 0.03), (0.31, 0.03), (0.31, 0.042), (0.29, 0.09), (-0.04, 0.09), (-0.07, 0.07)]
    win = prism([(-0.035, 0.044), (0.27, 0.044), (0.258, 0.076), (-0.028, 0.076)], -0.03, 0.03, 0.0)
    cut(b, prism(prof, -0.0105, 0.0105, 0.004), ACCENT, [win, box(0.004, 0.3, 0.007, (0, 0.12, 0.0905), 0.0, 1)])
    b.add(prism([(0.2, 0.076), (0.224, 0.076), (0.222, 0.052), (0.21, 0.046), (0.204, 0.05)], -0.007, 0.007, 0.0015), METAL)
    b.add(tube_x(0.003, 0.016, (0, 0.212, 0.074), 8), METAL)
    for s in (-1, 1):
        b.add(prism([(-0.062, 0.088), (-0.036, 0.088), (-0.04, 0.104), (-0.058, 0.104)], s * 0.006, s * 0.0105, 0.0008), ACCENT)
        b.add(prism([(0.278, 0.088), (0.3, 0.088), (0.296, 0.106), (0.282, 0.106)], s * 0.005, s * 0.0095, 0.0008), ACCENT)
    b.add(box(0.008, 0.008, 0.01, (0, -0.048, 0.096), 0.0012), METAL)
    b.add(box(0.0026, 0.003, 0.012, (0, 0.289, 0.098), 0.0005, 1), METAL)
    # Garde-main intégral (protège toute la main), poignée, détente.
    gp = [(-0.06, -0.05), (0.07, -0.05), (0.07, -0.066), (0.046, -0.15), (0.034, -0.166), (-0.058, -0.168), (-0.062, -0.158)]
    gi = [(-0.09, -0.06), (0.055, -0.06), (0.055, -0.068), (0.034, -0.148), (0.024, -0.155), (-0.09, -0.155)]
    cut(b, prism(gp, -0.0075, 0.0075, 0.003), POLY, [prism(gi, -0.02, 0.02, 0.0)])
    b.add(prism([(-0.08, -0.04), (0.075, -0.04), (0.072, -0.058), (-0.075, -0.058)], -0.019, 0.019, 0.003), POLY)
    trigger(b, 0.016, -0.056, 0.024)
    grp = grip(b, -0.03, -0.056, 12, 0.1, 0.03, 0.046, POLY, bump=0.0, fingers=1, cap=False)
    mag = magazine(b, 0.11, -0.05, 0.135, 0.058, 0.024, bend=0.025, style="ar")
    b.add(loft([(0.075, 0.027, 0.014, -0.055, 3.5), (0.145, 0.027, 0.014, -0.055, 3.5)], 20), PAINT)
    # Bipied replié le long des flancs, collier, canon à anneaux lance-grenades.
    b.add(ring(0.034, 0.025, 0.335, 0.35, 0, -0.008, 24), METAL)
    for s in (-1, 1):
        b.add(tube_x(0.0045, 0.012, (s * 0.033, 0.342, -0.024), 10), METAL)
        b.add(tube(0.0038, 0.0038, 0.12, 0.342, s * 0.034, -0.026, 10), METAL)
        b.add(prism([(0.104, -0.022), (0.124, -0.022), (0.124, -0.032), (0.1, -0.034)], s * 0.03, s * 0.039, 0.001), METAL)
    b.add(tube(0.0095, 0.0095, 0.44, 0.6, 0, 0, 16), METAL)
    for uu in (0.5, 0.525, 0.55):
        b.add(ring(0.0125, 0.009, uu, uu + 0.006, 0, 0, 16), METAL)
    slotted_hider(b, 0.6, 0, 0.0115, 0.05, 4)
    # Crosse évidée à plaque de couche.
    cut(b, loft([(-0.13, 0.02, 0.04, -0.02, 3.2), (-0.2, 0.022, 0.05, -0.024, 3.4), (-0.36, 0.022, 0.056, -0.028, 3.4),
                 (-0.378, 0.021, 0.055, -0.028, 3.4)], 22), PAINT,
        [prism([(-0.18, -0.03), (-0.33, -0.036), (-0.33, -0.058), (-0.2, -0.05)], -0.05, 0.05, 0.0)])
    b.add(loft([(-0.378, 0.022, 0.057, -0.028, 5.0), (-0.392, 0.022, 0.057, -0.028, 5.0)], 22), RUBBER)
    for k in range(6):
        b.add(box(0.046, 0.002, 0.004, (0, -0.395, -0.068 + k * 0.016), 0, 1), RUBBER)
    sling_loop(b, -0.35, -0.066, 0, 0.009)
    laser_box(g.opt("laser"), 0.3, -0.026, -0.04)
    rifle_scope(g.opt("scope"), 0.12, 0.13, 0.18, 0.011, 0.016, base=0.094)
    g.point("Muzzle", (0, 0.65, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.31, -0.05))
    g.point("Butt", (0, -0.392, -0.028)); g.point("Mag", mag)


def stork4(g):
    """T4/T5/TZ Stork : Tavor bullpup, garde-main Manticore. Châssis polymère galbé, appui-joue, chargeur derrière la
    poignée, grand pontet intégral, fenêtres d'éjection, levier d'armement avant gauche, garde-main alu à fentes M-LOK
    ouvertes sur le canon, rail continu, point rouge, poignée verticale, lampe."""
    b = g.body
    secs = [(-0.356, 0.026, 0.054, -0.03, 4.0), (-0.344, 0.03, 0.06, -0.028, 4.5), (-0.24, 0.031, 0.056, -0.025, 4.5),
            (-0.1, 0.03, 0.046, -0.02, 4.5), (0.04, 0.029, 0.04, -0.016, 4.2), (0.15, 0.027, 0.033, -0.012, 4.0),
            (0.165, 0.024, 0.029, -0.01, 3.6)]
    bc = [box(0.012, 0.075, 0.018, (0.031, -0.19, 0.01), 0.003, 2)]
    for s in (-1, 1):
        bc.append(box(0.006, 0.42, 0.0022, (s * 0.031, -0.1, -0.034), 0, 1))
        bc.append(prism([(-0.33, -0.05), (-0.26, -0.05), (-0.25, -0.03), (-0.33, -0.026)], s * 0.028, s * 0.04, 0.0))
    bc.append(box(0.026, 0.07, 0.05, (0, -0.12, -0.075), 0.003, 2))
    cut(b, loft(secs, 30), PAINT, bc)
    b.add(box(0.004, 0.068, 0.014, (0.0255, -0.19, 0.01), 0.0015), ALLOY)
    b.add(box(0.003, 0.075, 0.018, (-0.0305, -0.19, 0.01), 0.0012), POLY)
    b.add(loft([(-0.33, 0.025, 0.012, 0.03, 3.0), (-0.32, 0.028, 0.015, 0.032, 3.0), (-0.16, 0.028, 0.014, 0.03, 3.0),
                (-0.14, 0.024, 0.01, 0.026, 3.0)], 20), POLY)                                        # appui-joue
    picatinny(b, -0.24, 0.155, 0.028, 0, 0.021, METAL)
    # Levier d'armement avant gauche, sélecteur, arrêtoir de chargeur à palette.
    b.add(prism([(0.1, 0.0), (0.12, 0.0), (0.12, 0.012), (0.1, 0.012)], -0.034, -0.029, 0.001), METAL)
    b.add(tube_x(0.0055, 0.016, (-0.042, 0.112, 0.006), 12), METAL)
    for s in (-1, 1):
        b.add(tube_x(0.0075, 0.004, (s * 0.031, 0.01, -0.032), 14), METAL)
        b.add(prism([(0.01, -0.035), (0.035, -0.028), (0.035, -0.023), (0.01, -0.029)], s * 0.031, s * 0.034, 0.0008), METAL)
        for (uu, zz) in ((-0.3, -0.05), (-0.06, -0.03), (0.13, -0.026), (-0.2, -0.065)):
            b.add(tube_x(0.0035, 0.064, (0, uu, zz), 10), METAL)
    b.add(prism([(-0.075, -0.06), (-0.06, -0.06), (-0.058, -0.078), (-0.07, -0.08)], -0.012, 0.012, 0.0015), METAL)
    # Grand pontet intégral, poignée, détente, chargeur.
    gp = [(0.035, -0.045), (0.165, -0.04), (0.165, -0.056), (0.13, -0.09), (0.1, -0.158), (0.088, -0.168), (0.03, -0.166)]
    gi = [(0.0, -0.058), (0.142, -0.054), (0.142, -0.058), (0.117, -0.088), (0.09, -0.152), (0.0, -0.154)]
    cut(b, prism(gp, -0.009, 0.009, 0.003), PAINT, [prism(gi, -0.02, 0.02, 0.0)])
    trigger(b, 0.11, -0.052, 0.024)
    grp = grip(b, 0.07, -0.058, 10, 0.1, 0.03, 0.045, POLY, bump=0.0, fingers=1, cap=False)
    mag = magazine(b, -0.12, -0.075, 0.15, 0.06, 0.023, bend=0.04, style="pmag", mat=POLY)
    b.add(prism([(-0.358, 0.026), (-0.344, 0.026), (-0.344, -0.09), (-0.358, -0.086)], -0.031, 0.031, 0.004), RUBBER)
    for k in range(7):
        b.add(box(0.064, 0.002, 0.004, (0, -0.36, -0.078 + k * 0.015), 0, 1), RUBBER)
    sling_loop(b, -0.33, 0.033, 0.0, 0.008)
    # Garde-main Manticore (alu) creux à fentes M-LOK, rail, canon, cache-flammes.
    ms = [(0.16, 0.027, 0.03, -0.004, 6.0), (0.33, 0.027, 0.03, -0.004, 6.0), (0.338, 0.024, 0.027, -0.004, 5.0)]
    mc = []
    for s in (-1, 1):
        mc += slots(0.18, 0.32, 4, s * 0.027, 0.004, 0.026, 0.008, 0.012)
        mc += slots(0.2, 0.3, 3, s * 0.027, -0.018, 0.026, 0.008, 0.012)
    mc += slots(0.19, 0.32, 3, 0, -0.034, 0.03, 0.008, 0.012, 'z')
    cut(b, shell(ms, 0.003, 28), ACCENT, mc)
    picatinny(b, 0.16, 0.33, 0.026, 0, 0.021, ACCENT)
    for s in (-1, 1):
        screw(b, 0.17, -0.016, s * 0.027, 0.0024, s)
    b.add(tube(0.0095, 0.0095, 0.16, 0.47, 0, 0, 16), METAL)
    b.add(tube(0.0085, 0.0085, 0.2, 0.33, 0, 0.016, 10), METAL)
    prong_hider(b, 0.47, 0, 0.012, 0.05)
    aimpoint(b, 0.02, 0.0375)
    magnifier(g.opt("scope"), -0.075, 0.0375)
    sv = fore_grip(b, 0.26, -0.034)
    light(b, 0.3, 0.0, 0.043)
    b.add(box(0.012, 0.03, 0.01, (0.035, 0.285, 0.0), 0.002), METAL)
    laser_box(g.opt("laser"), 0.24, 0.0, -0.045)
    g.point("Muzzle", (0, 0.52, 0.0)); g.point("Grip", grp); g.point("SupportV", sv)
    g.point("Butt", (0, -0.358, -0.03)); g.point("Mag", mag)


# ───────────────────────── Slade, Blazer (armes expérimentales) ─────────────────────────

def slade(g):
    """MA9/MA10 Slade : fusil perforant expérimental. Carcasse à pans coupés et lignes de panneau, cœur d'énergie visible
    par une fenêtre, blindage supérieur, canon double-rail à bobines et ailettes de dissipation, bouche à fente, optique
    intégrée, chargeur-cellule à jauge, crosse squelette à facettes."""
    b = g.body
    prof = [(-0.14, -0.04), (-0.14, 0.016), (-0.115, 0.034), (0.2, 0.04), (0.29, 0.026), (0.29, -0.022), (0.2, -0.034),
            (0.13, -0.054), (0.04, -0.054), (-0.02, -0.046)]
    rc = [box(0.012, 0.11, 0.022, (0.026, 0.08, -0.016), 0.003, 2), box(0.012, 0.11, 0.022, (-0.026, 0.08, -0.016), 0.003, 2)]
    for s in (-1, 1):
        rc.append(box(0.004, 0.33, 0.0022, (s * 0.024, 0.08, 0.012), 0, 1))
        rc.append(box(0.004, 0.0022, 0.06, (s * 0.024, 0.22, 0.0), 0, 1))
        rc += slots(-0.12, -0.04, 4, s * 0.024, -0.016, 0.01, 0.026, 0.004)
    cut(b, prism(prof, -0.024, 0.024, 0.007), PAINT, rc)
    b.add(box(0.03, 0.1, 0.018, (0, 0.08, -0.016), 0.004), ENERGY)                                # cœur visible
    for k in range(5):
        b.add(box(0.05, 0.004, 0.024, (0, 0.04 + k * 0.02, -0.016), 0, 1), METAL)
    b.add(prism([(-0.1, 0.03), (0.18, 0.042), (0.25, 0.034), (0.25, 0.026), (-0.08, 0.026)], -0.019, 0.019, 0.003), ACCENT)
    for s in (-1, 1):
        b.add(prism([(0.14, -0.034), (0.26, -0.03), (0.27, -0.012), (0.15, -0.016)], s * 0.024, s * 0.027, 0.0012), ACCENT)
        for uu in (-0.12, 0.0, 0.27):
            screw(b, uu, 0.02 if uu < 0.2 else -0.004, s * 0.024, 0.0026, s)
    # Optique intégrée.
    b.add(prism([(-0.06, 0.04), (0.1, 0.044), (0.11, 0.064), (0.09, 0.07), (-0.04, 0.068), (-0.06, 0.058)], -0.015, 0.015, 0.004), PAINT)
    b.add(box(0.024, 0.002, 0.018, (0, 0.111, 0.056), 0.0005, 1), LENS)
    b.add(box(0.02, 0.002, 0.014, (0, -0.061, 0.054), 0.0005, 1), GLASS)
    b.add(box(0.03, 0.06, 0.003, (0, 0.03, 0.0705), 0.001), ENERGY)
    # Canon double-rail : deux barres, lame d'énergie, bobines, ailettes, bouche fendue.
    for s in (-1, 1):
        b.add(prism([(0.28, s * 0.008), (0.6, s * 0.009), (0.62, s * 0.014), (0.6, s * 0.024), (0.28, s * 0.024)], -0.012, 0.012, 0.002), METAL)
    b.add(box(0.006, 0.32, 0.012, (0, 0.45, 0.0), 0.001), ENERGY)
    # Épine inférieure ajourée reliant le boîtier à la bouche (dissipateur), vis de bobines.
    sc = slots(0.32, 0.58, 8, 0, -0.034, 0.016, 0.008, 0.06)
    cut(b, prism([(0.27, -0.022), (0.61, -0.022), (0.6, -0.036), (0.3, -0.044), (0.27, -0.04)], -0.014, 0.014, 0.003), PAINT, sc)
    for k in range(6):
        for s in (-1, 1):
            screw(b, 0.318 + k * 0.05, 0.022, s * 0.02, 0.0018, s)
    for k in range(6):
        uu = 0.31 + k * 0.05
        b.add(loft([(uu, 0.02, 0.032, 0.0, 6.0), (uu + 0.016, 0.02, 0.032, 0.0, 6.0)], 16), ACCENT if k % 2 else METAL)
    for k in range(9):
        uu = 0.3 + k * 0.012
        b.add(box(0.044, 0.003, 0.012, (0, uu, 0.033), 0.0008), METAL)
    cut(b, prism([(0.6, -0.03), (0.66, -0.026), (0.68, -0.012), (0.68, 0.012), (0.66, 0.026), (0.6, 0.03)], -0.02, 0.02, 0.004), ACCENT,
        [box(0.008, 0.1, 0.05, (0, 0.66, 0.0), 0.0, 1), box(0.05, 0.03, 0.008, (0, 0.65, 0.0), 0.0, 1)])
    b.add(box(0.004, 0.002, 0.03, (0, 0.645, 0.0), 0.0004, 1), ENERGY)
    # Poignée, pontet, chargeur-cellule.
    trigger_guard(b, -0.02, 0.05, -0.05, -0.075, 0.012, 0.005, PAINT)
    trigger(b, 0.012, -0.05, 0.022)
    grp = grip(b, -0.025, -0.05, 14, 0.1, 0.031, 0.045, POLY, bump=0.0, fingers=1)
    mag = magazine(b, 0.1, -0.05, 0.11, 0.07, 0.03, bend=0.0, lin=0.02, mat=PAINT, style="pmag", base_mat=ACCENT)
    b.add(curve_strip(0.1, -0.05, 0.11, 0.07, 0.0, 0.3, 0.7, -0.0158, 0.0158, 0.35, 0.85, 0.02, 3, 0.0005), ENERGY)
    # Crosse squelette à facettes, bande lumineuse, plaque de couche.
    sp = [(-0.135, 0.016), (-0.2, 0.03), (-0.39, 0.038), (-0.405, 0.034), (-0.405, -0.07), (-0.39, -0.074), (-0.28, -0.05),
          (-0.18, -0.05), (-0.135, -0.04)]
    sh = [prism([(-0.19, 0.014), (-0.37, 0.022), (-0.37, -0.054), (-0.29, -0.036), (-0.2, -0.036)], -0.03, 0.03, 0.0)]
    cut(b, prism(sp, -0.02, 0.02, 0.006), PAINT, sh)
    b.add(prism([(-0.2, 0.031), (-0.39, 0.039), (-0.39, 0.034), (-0.2, 0.026)], -0.022, 0.022, 0.001), ENERGY)
    b.add(prism([(-0.41, 0.034), (-0.402, 0.034), (-0.402, -0.07), (-0.41, -0.07)], -0.022, 0.022, 0.003), RUBBER)
    laser_box(g.opt("laser"), 0.2, -0.01, -0.036)
    g.point("Muzzle", (0, 0.68, 0.0)); g.point("Grip", grp); g.point("Support", (0, 0.3, -0.045))
    g.point("Butt", (0, -0.41, -0.018)); g.point("Mag", mag)


def blazer(g):
    """Blazer : fusil à faisceau hors normes. Corps blanc à lignes de panneau et grilles, pile d'anneaux accélérateurs à
    ailettes autour du canon d'émission, durites, émetteur à lentille et griffes, cellules latérales dans leurs berceaux,
    bloc d'alimentation sous le corps (jauge), rail et viseur holographique, poignée verticale."""
    b = g.body
    secs = [(-0.165, 0.022, 0.048, -0.012, 3.0), (-0.1, 0.035, 0.06, -0.008, 3.5), (0.2, 0.036, 0.056, -0.004, 3.5),
            (0.28, 0.03, 0.044, 0.0, 3.2), (0.32, 0.024, 0.034, 0.0, 3.0)]
    bc = []
    for s in (-1, 1):
        bc.append(box(0.008, 0.36, 0.0025, (s * 0.035, 0.06, 0.028), 0, 1))
        bc.append(box(0.008, 0.0025, 0.08, (s * 0.035, 0.22, -0.01), 0, 1))
        bc.append(box(0.008, 0.0025, 0.08, (s * 0.035, -0.06, -0.012), 0, 1))
        bc += slots(0.12, 0.2, 5, s * 0.035, 0.0, 0.008, 0.03, 0.006)
    cut(b, loft(secs, 30), PAINT, bc)
    for s in (-1, 1):
        b.add(box(0.004, 0.07, 0.026, (s * 0.032, 0.16, 0.0), 0.001), ENERGY)
        for uu in (-0.08, 0.0, 0.24):
            screw(b, uu, 0.04, s * 0.033, 0.0026, s)
    # Canon d'émission, anneaux accélérateurs à ailettes, durites.
    b.add(tube(0.016, 0.014, 0.3, 0.7, 0, 0.0, 20), METAL)
    for k in range(5):
        uu = 0.35 + k * 0.07
        r = 0.036 - k * 0.002
        b.add(ring(r, 0.0155, uu, uu + 0.022, 0, 0.0, 28), ACCENT)
        b.add(ring(r - 0.004, 0.016, uu + 0.006, uu + 0.016, 0, 0.0, 28), ENERGY)
        for j in range(8):
            a = 360 * j / 8 + 22.5
            b.add(spin(box(0.003, 0.016, 0.012, (0, uu + 0.011, r + 0.004), 0.0008), a, 0, 0.0), ACCENT)
    # Durites d'alimentation au-dessus des anneaux, colliers sur chaque anneau.
    for s in (-1, 1):
        b.add(cable([(s * 0.016, 0.25, 0.042), (s * 0.016, 0.3, 0.046), (s * 0.016, 0.62, 0.046), (s * 0.012, 0.65, 0.036)], 0.0042, 10), RUBBER)
    b.add(box(0.026, 0.03, 0.016, (0, 0.26, 0.044), 0.003), METAL)
    for k in range(5):
        uu = 0.361 + k * 0.07
        b.add(box(0.042, 0.008, 0.016, (0, uu, 0.042 - k * 0.001), 0.0015), METAL)
    # Émetteur : cône évasé, lentille, trois griffes.
    cut(b, loft([(0.69, 0.018, 0.018, 0, 2.0), (0.72, 0.026, 0.026, 0, 2.0), (0.745, 0.03, 0.03, 0, 2.0)], 24), METAL,
        [loft([(0.705, 0.012, 0.012, 0, 2.0), (0.75, 0.026, 0.026, 0, 2.0)], 20)])
    b.add(tube(0.016, 0.016, 0.71, 0.716, 0, 0, 20), LENS)
    b.add(tube(0.006, 0.006, 0.716, 0.722, 0, 0, 12), ENERGY)
    for a in (90, 210, 330):
        b.add(spin(prism([(0.72, 0.026), (0.775, 0.022), (0.78, 0.016), (0.73, 0.018)], -0.004, 0.004, 0.0015), a - 90, 0, 0.0), ACCENT)
    # Cellules latérales dans leurs berceaux, bloc d'alimentation à jauge.
    for s in (-1, 1):
        energy_cell(b, 0.04, -0.02, s * 0.042, 0.16, 0.011)
        for uu in (-0.01, 0.09):
            b.add(box(0.012, 0.012, 0.03, (s * 0.04, uu, -0.02), 0.002), METAL)
        b.add(box(0.004, 0.3, 0.008, (s * 0.035, 0.08, 0.03), 0.001), ENERGY)
    pk = [(0.0, -0.056), (0.12, -0.056), (0.13, -0.066), (0.12, -0.1), (0.0, -0.1), (-0.008, -0.07)]
    cut(b, prism(pk, -0.026, 0.026, 0.004), ACCENT, slots(0.02, 0.1, 4, 0, -0.1, 0.012, 0.03, 0.01, 'z'))
    b.add(box(0.054, 0.07, 0.006, (0, 0.06, -0.078), 0.001), ENERGY)
    for s in (-1, 1):
        b.add(box(0.004, 0.08, 0.026, (s * 0.026, 0.06, -0.078), 0.001), METAL)
    picatinny(b, -0.08, 0.2, 0.05)
    holo(b, 0.06, 0.0595)
    trigger_guard(b, -0.034, 0.03, -0.064, -0.088, 0.01, 0.005, PAINT)
    trigger(b, -0.008, -0.064, 0.02)
    grp = grip(b, -0.045, -0.062, 14, 0.1, 0.032, 0.046, POLY, bump=0.0, fingers=1)
    sv = fore_grip(b, 0.26, -0.052, 0.08)
    # Crosse blanche à bande lumineuse, évidement, plaque de couche.
    cut(b, loft([(-0.16, 0.022, 0.045, -0.01, 3.4), (-0.26, 0.023, 0.05, -0.012, 3.4), (-0.4, 0.022, 0.055, -0.012, 3.4),
                 (-0.42, 0.021, 0.054, -0.012, 3.4)], 22), PAINT,
        [prism([(-0.2, -0.03), (-0.37, -0.034), (-0.37, -0.054), (-0.23, -0.05)], -0.05, 0.05, 0.0)])
    b.add(loft([(-0.42, 0.022, 0.056, -0.012, 5.0), (-0.434, 0.022, 0.056, -0.012, 5.0)], 22), RUBBER)
    b.add(loft([(-0.18, 0.0235, 0.008, 0.012, 2.0), (-0.36, 0.0235, 0.008, 0.016, 2.0)], 14), ENERGY)
    g.point("Muzzle", (0, 0.76, 0.0)); g.point("Grip", grp); g.point("SupportV", sv)
    g.point("Butt", (0, -0.434, -0.012)); g.point("Mag", (0, 0.06, -0.08))


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
    elif os.environ.get("RIFLES_FLIP"):
        # Contrôle seulement (jamais à l'export) : arme retournée pour voir le flanc droit dans les rendus.
        bpy.data.objects[family].rotation_euler[2] = math.pi
        bpy.context.view_layer.update()
    return tris()
