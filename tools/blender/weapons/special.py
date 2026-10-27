# Grenades et armes spéciales du Ranger (EDF 5), une fonction par famille de modèle. Matériel militaire et du génie.
# Grenades à main (tenues d'une main, repère Grip au centre) : MG = grenade à fragmentation quadrillée type MK2 (cuillère,
# goupille, anneau), DNG = grenade à manche nervurée type Stielhandgranate modernisée, Bound Grenade = boule en caoutchouc
# à crampons, ceinture boulonnée et minuterie. Lance-grenades : UM = monocoup à canon basculant type M79 (hausse à échelle),
# BRUTE = trois canons en triangle (poignée de transport), CRUMBLE = deux canons superposés type fusil de chasse,
# Stampede = revolver à barillet type Milkor MGL, Splash = gros tube à pompe (sous-munitions dans la bouche),
# Sticky = lanceur à chargeur camembert et cartouche de gel adhésif, Volatile Napalm = lanceur à réservoir latéral de gel,
# Flame Geyser = tube à ailettes de refroidissement et pavillon.
# Spéciales : Acid = pulvérisateur à réservoir translucide encagé et pompe, Burner/Flamethrower = lance-flammes à deux
# bouteilles (type M2), Flame/Magma Cannon = canon à flammes lourd à fût ajouré et bidon, Firecracker = faisceau de tubes,
# Bound Gun = mitraillette à déflecteur de bouche, C Bomb = pain d'explosif et détonateur radio, Impulse = mine disque à
# ports de tir, Reverser = pistolet de soin futuriste, Reverse Shooter = lanceur de soin à fioles.
import math

# Marquages au pochoir (noms d'EDF 5) ; emplacement (u, z) sur un flanc plat, None : pas de marquage.
LABELS = {"mg": "MG11", "dng": "DNG1", "boundgren": "BOUND", "um": "UM1", "brute": "BRUTE J", "crumble": "CRUMBLE",
          "stampede": "STAMPEDE M1", "splash": "SPLASH", "sticky": "STICKY", "napalm": "V-NAPALM", "geyser": "FLAME GEYSER",
          "acid": "ACID GUN", "flamer": "FLAMETHROWER", "flamecannon": "FLAME CANNON", "firecracker": "FIRECRACKER",
          "boundgun": "H88 BOUND", "cbomb": "C20", "impulse": "IMPULSE Y8", "reverser": "REVERSER",
          "revshooter": "REV SHOOTER"}
MARK_AT = {"mg": None, "boundgren": None, "dng": (0.055, 0.0), "cbomb": (0.0, -0.004), "impulse": (0.0, -0.006),
           "reverser": (-0.075, -0.004), "revshooter": (-0.075, -0.004), "firecracker": (0.15, -0.02),
           "stampede": (-0.05, -0.02), "sticky": (-0.06, -0.012), "splash": (-0.04, -0.01), "brute": (0.035, -0.002), "napalm": (-0.04, -0.004)}


# ───────────────────────── Outils propres au module ─────────────────────────

def sphere(r, c, scale=(1, 1, 1), seg=18, rings=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def _axpt(t, a, b, x, u, z, axis):
    if axis == 'u':
        return P(x + a, t, z + b)
    if axis == 'z':
        return P(x + a, u + b, t)
    return P(t, u + a, z + b)


def revsurf(rows, seg=24, x=0.0, u=0.0, z=0.0, axis='u', caps=True, angs=None):
    """Surface de révolution : rows = [(t, r)] le long de l'axe (u, z ou x) ; r peut être une fonction de l'angle (cannelures,
    nervures, quadrillage) ; r = 0 : pôle (un seul sommet)."""
    angs = angs or [2 * math.pi * i / seg for i in range(seg)]
    bm = bmesh.new()
    loops = []
    for (t, r) in rows:
        if not callable(r) and r <= 1e-7:
            loops.append([bm.verts.new(_axpt(t, 0, 0, x, u, z, axis))])
            continue
        loop = []
        for a in angs:
            rr = r(a) if callable(r) else r
            loop.append(bm.verts.new(_axpt(t, rr * math.cos(a), rr * math.sin(a), x, u, z, axis)))
        loops.append(loop)
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        if len(A) == 1 and len(B) == 1:
            continue
        if len(A) == 1:
            for i in range(len(B)):
                bm.faces.new((A[0], B[i], B[(i + 1) % len(B)]))
        elif len(B) == 1:
            for i in range(len(A)):
                bm.faces.new((A[i], A[(i + 1) % len(A)], B[0]))
        else:
            n = len(A)
            for i in range(n):
                bm.faces.new((A[i], A[(i + 1) % n], B[(i + 1) % n], B[i]))
    if caps:
        if len(loops[0]) > 1:
            bm.faces.new(list(reversed(loops[0])))
        if len(loops[-1]) > 1:
            bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def vcyl(r0, r1, z0, z1, x=0.0, u=0.0, seg=16):
    """Cylindre vertical (axe z)."""
    return revsurf([(z0, 0), (z0, r0), (z1, r1), (z1, 0)], seg, x=x, u=u, axis='z')


def cup(x, u, z0, z1, ro, ri, depth, seg=16):
    """Godet vertical creux (port, manchon) : bord extérieur ro, intérieur ri, creux de « depth » sous le bord."""
    return revsurf([(z0, 0), (z0, ro), (z1, ro), (z1, ri), (z1 - depth, ri), (z1 - depth, 0)], seg, x=x, u=u, axis='z')


def capsule(u0, u1, r, x=0.0, z=0.0, dome=None, seg=24, n=5, ribs=()):
    """Réservoir / bouteille à fonds bombés le long de u ; ribs = [(u, saillie)] : moulures de renfort."""
    d = r if dome is None else dome
    rows = []
    for k in range(n + 1):
        th = math.pi / 2 * k / n
        rows.append((u0 + d * (1 - math.cos(th)), r * math.sin(th)))
    for (uu, h) in ribs:
        rows += [(uu - 0.004, r), (uu - 0.002, r + h), (uu + 0.002, r + h), (uu + 0.004, r)]
    for k in range(n, -1, -1):
        th = math.pi / 2 * k / n
        rows.append((u1 - d * (1 - math.cos(th)), r * math.sin(th)))
    return revsurf(rows, seg, x=x, z=z)


def bore_barrel(part, prof, ri, depth, x=0.0, z=0.0, seg=24, mat=METAL):
    """Canon creux : profil extérieur [(u, r)] du tonnerre à la bouche ; âme de rayon ri visible sur « depth »."""
    u0, u1 = prof[0][0], prof[-1][0]
    rows = [(u0, 0)] + list(prof) + [(u1, ri), (u1 - depth, ri), (u1 - depth, 0)]
    part.add(revsurf(rows, seg, x=x, z=z), mat)


def torus(R, r, c, axis='x', seg=24, rseg=8):
    """Tore (anneau de goupille, volant de vanne, anneau de grenadière) ; axis = axe de révolution."""
    bm = bmesh.new()
    loops = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        loop = []
        for j in range(rseg):
            bb = 2 * math.pi * j / rseg
            rad, h = R + r * math.cos(bb), r * math.sin(bb)
            p, q = rad * math.cos(a), rad * math.sin(a)
            co = {"x": (c[0] + h, c[1] + p, c[2] + q), "u": (c[0] + p, c[1] + h, c[2] + q), "z": (c[0] + p, c[1] + q, c[2] + h)}[axis]
            loop.append(bm.verts.new(P(*co)))
        loops.append(loop)
    for i in range(seg):
        A, B = loops[i], loops[(i + 1) % seg]
        for j in range(rseg):
            k = (j + 1) % rseg
            bm.faces.new((A[j], A[k], B[k], B[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def circ(r, n=8):
    return [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def rrect(w, h, r, n=3):
    """Rectangle à coins arrondis (contour 2D centré)."""
    pts = []
    for (cx, cz, a0) in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def bez(p0, p1, p2, p3, n=12):
    """Courbe de Bézier cubique (points du repère de travail)."""
    out = []
    for k in range(n + 1):
        t = k / n
        s = 1 - t
        out.append(tuple(s ** 3 * a + 3 * s * s * t * b + 3 * s * t * t * c + t ** 3 * d for a, b, c, d in zip(p0, p1, p2, p3)))
    return out


def sweep(path, prof, closed=False, caps=True, up=None):
    """Balayage d'un profil 2D [(a, b)] le long d'un chemin [(x, u, z)] : durites, câbles, sangles, poignées de transport.
    up (repère de travail) fixe l'axe « a » du profil (sinon transport parallèle)."""
    pts = [P(*p) for p in path]
    n = len(pts)
    tans = []
    for i in range(n):
        if closed:
            a, b = pts[(i - 1) % n], pts[(i + 1) % n]
        else:
            a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        tans.append((b - a).normalized())
    upv = P(*up) if up else None
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = tans[0].cross(ref).normalized()
    bm = bmesh.new()
    loops = []
    for i in range(n):
        t = tans[i]
        base = upv if upv is not None else nrm
        nn = base - t * base.dot(t)
        if nn.length < 1e-6:
            nn = nrm
        nrm = nn.normalized()
        bi = t.cross(nrm)
        loops.append([bm.verts.new(pts[i] + nrm * a + bi * b) for (a, b) in prof])
    m = len(prof)
    for k in range(n if closed else n - 1):
        A, B = loops[k], loops[(k + 1) % n]
        for i in range(m):
            j = (i + 1) % m
            bm.faces.new((A[i], A[j], B[j], B[i]))
    if caps and not closed:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def fillet(poly, r, n=4):
    """Polygone 2D fermé aux coins arrondis (rayon r, n points par coin) : cadres de crosse, anses."""
    out = []
    m = len(poly)
    for i in range(m):
        p0, p1, p2 = Vector(poly[i - 1]), Vector(poly[i]), Vector(poly[(i + 1) % m])
        a, c = (p0 - p1).normalized(), (p2 - p1).normalized()
        rr = min(r, (p0 - p1).length * 0.45, (p2 - p1).length * 0.45)
        s0, s1 = p1 + a * rr, p1 + c * rr
        for k in range(n + 1):
            t = k / n
            q = s0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + s1 * t * t
            out.append((q.x, q.y))
    return out


def hull_u(circles, u0, u1, seg=20):
    """Plaque / bride enveloppant des cercles [(x, z, r)] (coque convexe), de u0 à u1 : colliers de canons, flasques."""
    bm = bmesh.new()
    for (x, z, r) in circles:
        for i in range(seg):
            a = 2 * math.pi * i / seg
            for uu in (u0, u1):
                bm.verts.new(P(x + r * math.cos(a), uu, z + r * math.sin(a)))
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def plan(profile, z0, z1, bevel=0.0008):
    """Plaque vue de dessus : profile = [(x, u)] extrudé de z0 à z1 (leviers, gâchettes à plat)."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x, u, z0)) for (x, u) in profile]
    b = [bm.verts.new(P(x, u, z1)) for (x, u) in profile]
    n = len(profile)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5)
    return bm


def rot_u(bm, deg, x=0.0, z=0.0):
    """Rotation d'une forme autour de l'axe u passant par (x, z)."""
    bmesh.ops.rotate(bm, cent=P(x, 0, z), matrix=Matrix.Rotation(math.radians(deg), 3, 'Y'), verts=bm.verts)
    return bm


def rot_z(bm, deg, x=0.0, u=0.0):
    """Rotation d'une forme autour d'un axe vertical passant par (x, u)."""
    bmesh.ops.rotate(bm, cent=P(x, u, 0), matrix=Matrix.Rotation(math.radians(deg), 3, 'Z'), verts=bm.verts)
    return bm


def hexnut(part, u0, u1, r, x=0.0, z=0.0, mat=METAL):
    part.add(tube(r, r, u0, u1, x, z, 6), mat)


def bolt_top(part, x, u, z, r=0.0028, mat=METAL):
    """Tête de vis hexagonale vue du dessus (posée sur une face horizontale à la hauteur z)."""
    part.add(revsurf([(z - 0.001, 0), (z - 0.001, r), (z + r * 0.7, r), (z + r * 0.8, r * 0.6), (z + r * 0.8, 0)], 6, x=x, u=u, axis='z'), mat)


def swivel(part, u, z, x=0.0):
    """Grenadière (anneau de bretelle) sous l'arme."""
    part.add(box(0.008, 0.012, 0.006, (x, u, z - 0.002), 0.0015), METAL)
    part.add(torus(0.008, 0.0016, (x, u, z - 0.011), 'x', 16, 6), METAL)


def grip(part, u, z, hw=0.024, angle=15, mat=POLY):
    """Poignée pistolet du kit + détente, sélecteur (flanc gauche) et axe ; renvoie le point de la main."""
    gp = pistol_grip(part, u, z, mat, angle, 0.105, 0.032, 0.046)
    part.add(prism([(u + 0.026, z - 0.001), (u + 0.034, z - 0.001), (u + 0.032, z - 0.016), (u + 0.025, z - 0.033),
                    (u + 0.021, z - 0.031), (u + 0.026, z - 0.016)], -0.0028, 0.0028, 0.0008), METAL)
    part.add(prism([(u - 0.016, z + 0.014), (u + 0.004, z + 0.019), (u + 0.006, z + 0.013), (u - 0.014, z + 0.008)], -hw - 0.003, -hw + 0.0005, 0.0007), METAL)
    part.add(tube_x(0.0055, 0.004, (-hw - 0.001, u - 0.012, z + 0.011), 14), METAL)
    pins(part, (u + 0.012, u + 0.05), z + 0.006, hw)
    return gp


def hose(part, path, r=0.006, mat=RUBBER, fit=METAL, seg=10):
    """Durite souple avec raccords sertis aux deux bouts."""
    part.add(sweep(path, circ(r, seg)), mat)
    for (p, q) in ((path[0], path[1]), (path[-1], path[-2])):
        d = Vector(q) - Vector(p)
        d.normalize()
        e = Vector(p) + d * 0.012
        part.add(sweep([tuple(p), tuple(e)], circ(r * 1.35, 6)), fit)


def gauge(part, x, u, z, r=0.012, face='z', mat=METAL):
    """Manomètre : boîtier, verre, aiguille ; face 'z' (dessus) ou 'x' (flanc droit)."""
    if face == 'z':
        part.add(revsurf([(z, 0), (z, r), (z + 0.008, r), (z + 0.009, r * 0.85), (z + 0.007, r * 0.82), (z + 0.007, 0)], 20, x=x, u=u, axis='z'), mat)
        part.add(vcyl(r * 0.82, r * 0.82, z + 0.0068, z + 0.0072, x, u, 20), MARKING)
        part.add(box(r * 0.12, r * 0.7, 0.0006, (x + r * 0.15, u + r * 0.25, z + 0.0076), 0, 1), ACCENT)
        part.add(vcyl(r * 0.8, r * 0.8, z + 0.0078, z + 0.0084, x, u, 20), GLASS)
        part.add(vcyl(0.003, 0.003, z - 0.008, z, x, u, 6), mat)
    else:
        part.add(revsurf([(x, 0), (x, r), (x + 0.008, r), (x + 0.009, r * 0.85), (x + 0.007, r * 0.82), (x + 0.007, 0)], 20, u=u, z=z, axis='x'), mat)
        part.add(revsurf([(x + 0.0068, 0), (x + 0.0068, r * 0.82), (x + 0.0072, r * 0.82), (x + 0.0072, 0)], 20, u=u, z=z, axis='x'), MARKING)
        part.add(box(0.0006, r * 0.12, r * 0.7, (x + 0.0076, u + r * 0.2, z + r * 0.25), 0, 1), ACCENT)
        part.add(revsurf([(x + 0.0078, 0), (x + 0.0078, r * 0.8), (x + 0.0084, r * 0.8), (x + 0.0084, 0)], 20, u=u, z=z, axis='x'), GLASS)


def handwheel(part, c, R=0.013, axis='z', mat=ACCENT):
    """Volant de vanne à trois rayons."""
    part.add(torus(R, 0.0022, c, axis, 20, 6), mat)
    for k in range(3):
        a = 2 * math.pi * k / 3
        if axis == 'z':
            p = (c[0] + math.cos(a) * R, c[1] + math.sin(a) * R, c[2])
        elif axis == 'u':
            p = (c[0] + math.cos(a) * R, c[1], c[2] + math.sin(a) * R)
        else:
            p = (c[0], c[1] + math.cos(a) * R, c[2] + math.sin(a) * R)
        part.add(sweep([c, p], circ(0.0016, 6)), mat)
    part.add(sphere(0.004, c, (1, 1, 1), 10, 6), METAL)


def side_panel(part, u0, u1, z0, z1, hw, mat=ACCENT):
    """Trappes de visite vissées sur les deux flancs d'un boîtier (support des marquages)."""
    for s in (-1, 1):
        part.add(box(0.0024, u1 - u0, z1 - z0, (s * (hw + 0.0006), (u0 + u1) / 2, (z0 + z1) / 2), 0.0009), mat)
        for uu in (u0 + 0.007, u1 - 0.007):
            for zz in (z0 + 0.006, z1 - 0.006):
                screw(part, uu, zz, s * (hw + 0.0018), 0.0019, s)


def hand_points(g, c=(0, 0, 0)):
    g.point("Muzzle", (c[0], c[1] + 0.05, c[2])); g.point("Grip", c); g.point("Support", (c[0], c[1] + 0.06, c[2]))
    g.point("Butt", (c[0], c[1] - 0.1, c[2])); g.point("Mag", c)


# ───────────────────────── Grenades à main ─────────────────────────

def _mg_r(t):
    """Rayon du corps ovoïde de la MG à la hauteur t."""
    zc, R = -0.008, 0.0285
    H = 0.04 if t > zc else 0.036
    q = 1 - ((t - zc) / H) ** 2
    return R * max(q, 0.0) ** 0.5


def mg(g):
    """Grenade à fragmentation type MK2 : corps ovoïde quadrillé (12 colonnes × 6 rangs), bague de bouchon, allumeur à
    oreilles, cuillère en U qui épouse le corps, goupille et anneau, bouchon de fond, bande jaune d'explosif."""
    b = g.body
    cell_a = 2 * math.pi / 12
    angs = [(c + f) * cell_a for c in range(12) for f in (0.0, 0.12, 0.35, 0.65, 0.88)]
    g0, g1, nrow = -0.035, 0.0124, 5
    ch = (g1 - g0) / nrow

    def rowf(t, fr):
        rb = _mg_r(t)
        hg = 1.0 if fr == 0.0 else 0.0

        def f(a):
            fa = (a / cell_a) % 1.0
            vg = 1.0 if (fa < 1e-4 or fa > 1 - 1e-4) else 0.0
            dome = 0.0007 * math.sin(math.pi * fa) * math.sin(math.pi * fr)
            return rb - 0.0021 * max(hg, vg) + dome
        return f
    rows = [(-0.0425, 0), (-0.0425, _mg_r(-0.042) - 0.001), (-0.042, _mg_r(-0.042)), (-0.039, _mg_r(-0.039))]
    for k in range(nrow):
        for fr in (0.0, 0.15, 0.5, 0.85):
            t = g0 + (k + fr) * ch
            rows.append((t, rowf(t, fr)))
    rows.append((g1, rowf(g1, 0.0)))
    rows += [(0.0145, _mg_r(0.0145)), (0.0175, _mg_r(0.0175)), (0.0205, _mg_r(0.0205)), (0.024, _mg_r(0.024)), (0.026, _mg_r(0.026)), (0.026, 0)]
    b.add(revsurf(rows, axis='z', angs=angs), PAINT)
    # Bande jaune (explosif) sur l'épaule lisse.
    b.add(revsurf([(0.0148, _mg_r(0.0148) + 0.0004), (0.0175, _mg_r(0.0175) + 0.0004), (0.0202, _mg_r(0.0202) + 0.0004)], 48, axis='z', caps=False), ACCENT)
    # Bouchon de fond et bague filetée de l'allumeur (moletée).
    b.add(revsurf([(-0.0445, 0), (-0.0445, 0.0075), (-0.041, 0.0078), (-0.041, 0)], 16, axis='z'), METAL)
    b.add(box(0.012, 0.0016, 0.0012, (0, 0, -0.0448), 0, 1), POLY)
    b.add(revsurf([(0.023, 0), (0.023, 0.0128), (0.025, lambda a: 0.0136 + 0.0005 * math.cos(24 * a)),
                   (0.031, lambda a: 0.0136 + 0.0005 * math.cos(24 * a)), (0.032, 0.012), (0.032, 0)], 48, axis='z'), METAL)
    # Corps de l'allumeur et tête de percuteur.
    b.add(revsurf([(0.031, 0), (0.031, 0.0095), (0.046, 0.0095), (0.049, 0.0075), (0.05, 0.0)], 18, axis='z'), METAL)
    b.add(tube(0.004, 0.004, -0.012, 0.008, 0, 0.047, 10), METAL)
    # Oreilles d'accrochage, cuillère, goupille et anneau : construits côté u puis tournés (cuillère sur le flanc gauche).
    lev = Part()
    b0, b = b, lev
    for s in (-1, 1):
        b.add(prism([(0.006, 0.033), (0.014, 0.036), (0.016, 0.046), (0.012, 0.05), (0.006, 0.048)], s * 0.0075 - 0.0012, s * 0.0075 + 0.0012, 0.0004), METAL)
    b.add(tube_x(0.0012, 0.019, (0, 0.0125, 0.0445), 8), METAL)
    # Cuillère en U : crochet sur les oreilles, passe sur l'allumeur et descend le long du corps (côté −u).
    path = [(0, 0.0135, 0.041), (0, 0.0145, 0.048), (0, 0.009, 0.0535), (0, 0.0, 0.0545), (0, -0.008, 0.0535),
            (0, -0.0125, 0.0495), (0, -0.0145, 0.043), (0, -0.0158, 0.036)]
    for zz in (0.03, 0.024, 0.018, 0.012, 0.005, -0.002, -0.009, -0.015):
        path.append((0, -(max(_mg_r(zz), 0.0136) + 0.0027), zz))
    path.append((0, -(_mg_r(-0.017) + 0.0048), -0.0205))
    w, th, fl = 0.0068, 0.0011, 0.0026
    prof = [(-w, 0), (w, 0), (w, fl), (w - th, fl), (w - th, th), (-w + th, th), (-w + th, fl), (-w, fl)]
    b.add(sweep(path, prof, up=(1, 0, 0)), METAL)
    # Goupille, anneau de traction et agrafe de sécurité.
    b.add(tube_x(0.0011, 0.03, (0.002, 0.0115, 0.044), 8), METAL)
    b.add(torus(0.011, 0.0014, (0.026, 0.0115, 0.0355), 'u', 22, 6), METAL)
    b.add(sweep([(0.017, 0.0115, 0.044), (0.019, 0.0115, 0.0455), (0.021, 0.0115, 0.044)], circ(0.0011, 6)), METAL)
    me = bpy.data.meshes.new("lev")
    lev.bm.to_mesh(me)
    lev.bm.free()
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    b0.add(bm, METAL, Matrix.Rotation(math.radians(90), 4, 'Z'))
    hand_points(g)


def dng(g):
    """Grenade à manche DNG : tête nervurée à bandes d'acier et plaque d'identification, allumeur à capuchon rouge moleté,
    manche à cannelures de prise, bouchon de fond moleté, cordon et anneau de traction, agrafe de ceinture."""
    b = g.body
    rib = lambda a: 0.0385 + 0.0024 * max(0.0, math.cos(16 * a)) ** 2
    b.add(revsurf([(-0.004, 0), (-0.004, 0.022), (-0.001, 0.031), (0.004, 0.037), (0.008, rib), (0.096, rib),
                   (0.1, 0.037), (0.106, 0.031), (0.11, 0.018), (0.11, 0)], 96), PAINT)
    for uu in (0.006, 0.098):
        b.add(ring(0.0418, 0.035, uu - 0.006, uu + 0.006, 0, 0, 40), METAL)
        b.add(box(0.012, 0.01, 0.006, (0, uu, -0.044), 0.0015), METAL)                       # serre-bande
        b.add(tube_x(0.0018, 0.016, (0, uu, -0.046), 8), METAL)
    # Plaques d'identification planes sur les flancs (marquage).
    for s in (-1, 1):
        b.add(box(0.0016, 0.05, 0.022, (s * 0.0405, 0.052, 0.0), 0.0006, 1), ACCENT)
        for uu in (0.03, 0.074):
            screw(b, uu, 0.0, s * 0.0413, 0.0016, s)
    # Allumeur : col fileté, corps, capuchon rouge moleté.
    b.add(revsurf([(0.109, 0), (0.109, 0.017), (0.116, 0.017), (0.117, 0.0135), (0.126, 0.0135), (0.127, 0.015), (0.13, 0.015), (0.13, 0)], 24), METAL)
    knurl = lambda a: 0.0145 + 0.0006 * math.cos(20 * a)
    b.add(revsurf([(0.129, 0), (0.129, knurl), (0.143, knurl), (0.147, 0.011), (0.149, 0.006), (0.15, 0)], 40), ACCENT)
    # Manche : col, cannelures de prise, bouchon de fond moleté.
    rows = [(-0.003, 0.019), (-0.01, 0.0165), (-0.018, 0.0165)]
    uu = -0.022
    while uu > -0.16:
        rows += [(uu, 0.0168), (uu - 0.003, 0.0152), (uu - 0.006, 0.0152), (uu - 0.009, 0.0168)]
        uu -= 0.012
    rows += [(-0.166, 0.017), (-0.17, 0.0175)]
    b.add(revsurf([(-0.003, 0)] + rows + [(-0.17, 0)], 24), POLY)
    kn = lambda a: 0.0195 + 0.0007 * math.cos(18 * a)
    b.add(revsurf([(-0.168, 0), (-0.168, 0.018), (-0.171, kn), (-0.186, kn), (-0.189, 0.016), (-0.19, 0.0)], 36), METAL)
    # Cordon et anneau de traction pendant sous le bouchon.
    b.add(sweep(bez((0, -0.19, 0.0), (0, -0.198, 0.0), (0, -0.2, -0.006), (0, -0.198, -0.012), 6), circ(0.0012, 6)), MARKING)
    b.add(torus(0.0095, 0.0016, (0, -0.198, -0.022), 'x', 20, 6), METAL)
    # Agrafe de ceinture sur le manche.
    b.add(sweep([(0.017, -0.03, 0.0), (0.0195, -0.034, 0.0), (0.0205, -0.05, 0.0), (0.0205, -0.095, 0.0), (0.0185, -0.1, 0.0)], rrect(0.0016, 0.009, 0.0006, 1), up=(1, 0, 0)), METAL)
    b.add(box(0.004, 0.008, 0.012, (0.0175, -0.03, 0.0), 0.001), METAL)
    hand_points(g, (0, -0.09, 0))


def _ball_disp(R, nlat, nlon, fn):
    """Sphère déplacée selon la normale : fn(lat, lon) -> saillie (crampons)."""
    bm = bmesh.new()
    top = bm.verts.new(P(0, 0, R + fn(math.pi / 2, 0)))
    bot = bm.verts.new(P(0, 0, -R - fn(-math.pi / 2, 0)))
    loops = []
    for i in range(1, nlat):
        la = -math.pi / 2 + math.pi * i / nlat
        loop = []
        for j in range(nlon):
            lo = 2 * math.pi * j / nlon
            rr = R + fn(la, lo)
            loop.append(bm.verts.new(P(rr * math.cos(la) * math.cos(lo), rr * math.cos(la) * math.sin(lo), rr * math.sin(la))))
        loops.append(loop)
    n = nlon
    for i in range(n):
        bm.faces.new((bot, loops[0][(i + 1) % n], loops[0][i]))
        bm.faces.new((top, loops[-1][i], loops[-1][(i + 1) % n]))
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(n):
            bm.faces.new((A[i], A[(i + 1) % n], B[(i + 1) % n], B[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def boundgren(g):
    """Grenade rebondissante : boule de caoutchouc à crampons, trois cerceaux jaunes, ceinture d'acier boulonnée à
    l'équateur, minuterie à cadran cranté, voyant, clé d'armement et anneau ; bouchon de fond."""
    b = g.body
    R = 0.039

    def lugs(la, lo):
        if abs(la) > math.radians(70):
            return 0.0
        v = abs(math.cos(9 * lo)) * abs(math.cos(6 * la))
        return 0.0026 * max(0.0, (v - 0.35) / 0.65) ** 0.7
    b.add(_ball_disp(R, 30, 72, lugs), RUBBER)
    # Cerceaux méridiens (bandes en relief).
    for a in (0, 60, 120):
        ar = math.radians(a)
        path = []
        for k in range(48):
            th = 2 * math.pi * k / 48
            rr = R + 0.0028
            path.append((rr * math.cos(th) * math.cos(ar), rr * math.cos(th) * math.sin(ar), rr * math.sin(th)))
        b.add(sweep(path, rrect(0.0075, 0.0026, 0.0009, 1), closed=True, up=(-math.sin(ar), math.cos(ar), 0)), ACCENT)
    # Ceinture d'acier à l'équateur et boulons entre les cerceaux.
    b.add(revsurf([(-0.0055, R + 0.0005), (-0.0045, R + 0.0038), (0.0045, R + 0.0038), (0.0055, R + 0.0005)], 72, axis='z', caps=False), METAL)
    for k in range(6):
        a = math.radians(30 + 60 * k)
        b.add(tube(0.0026, 0.0026, R + 0.003, R + 0.0062, 0, 0, 6), METAL, Matrix.Rotation(a + math.pi / 2, 4, 'Z'))
    # Minuterie au sommet : embase, cadran cranté, dôme, voyant, clé d'armement.
    b.add(revsurf([(R - 0.004, 0), (R - 0.004, 0.017), (R + 0.003, 0.017), (R + 0.004, 0.0155), (R + 0.004, 0)], 32, axis='z'), METAL)
    dial = lambda a: 0.0148 + (0.0012 if math.cos(12 * a) > 0.3 else 0.0)
    b.add(revsurf([(R + 0.0035, 0), (R + 0.0035, dial), (R + 0.0095, dial), (R + 0.0095, 0)], 72, axis='z'), ACCENT)
    b.add(revsurf([(R + 0.009, 0.0115), (R + 0.0125, 0.009), (R + 0.0145, 0.004), (R + 0.015, 0)], 24, axis='z'), METAL)
    b.add(sphere(0.0028, (0.0075, 0.0, R + 0.0115), (1, 1, 1), 10, 6), GLOW)
    b.add(box(0.0035, 0.014, 0.004, (-0.004, 0.0, R + 0.016), 0.0008), METAL)
    for k in range(12):
        a = 2 * math.pi * k / 12
        b.add(box(0.0012, 0.0012, 0.0015, (math.cos(a) * 0.0105, math.sin(a) * 0.0105, R + 0.0098), 0, 1), MARKING)
    b.add(torus(0.009, 0.0014, (0.0, -0.016, R + 0.012), 'x', 20, 6), METAL)
    b.add(revsurf([(-R - 0.0025, 0), (-R - 0.0025, 0.009), (-R + 0.003, 0.0105), (-R + 0.003, 0)], 20, axis='z'), METAL)
    hand_points(g)


# ───────────────────────── Lance-grenades ─────────────────────────

def um(g):
    """Monocoup 40 mm à canon basculant (type M79) : boîtier à flancs creusés, verrou de bascule, charnière, canon à
    tonnerre renforcé et âme visible, hausse à échelle graduée relevée, guidon à oreilles, garde-main rainuré, crosse pleine."""
    b = g.body
    rec = prism([(-0.095, 0.022), (0.0, 0.028), (0.06, 0.031), (0.078, 0.028), (0.08, -0.004), (0.075, -0.034), (0.035, -0.047),
                 (-0.055, -0.047), (-0.095, -0.03)], -0.027, 0.027, 0.003)
    rec = carve(rec, [box(0.006, 0.07, 0.026, (s * 0.027, -0.018, -0.012), 0.004) for s in (-1, 1)])
    b.add(rec, PAINT)
    # Verrou de bascule sur le dessus (levier pivotant).
    b.add(plan([(-0.004, -0.06), (0.004, -0.06), (0.012, -0.02), (0.022, -0.012), (0.02, -0.004), (-0.004, -0.02)], 0.024, 0.031), METAL)
    b.add(vcyl(0.006, 0.006, 0.026, 0.033, 0, -0.054, 14), METAL)
    # Charnière de bascule.
    b.add(tube_x(0.0095, 0.058, (0, 0.07, -0.034), 18), METAL)
    for s in (-1, 1):
        screw(b, 0.07, -0.034, s * 0.029, 0.0045, s)
        screw(b, -0.07, -0.01, s * 0.0275, 0.003, s)
    # Canon : tonnerre renforcé, âme de 40 mm, bague de bouche.
    bore_barrel(b, [(0.06, 0.031), (0.112, 0.031), (0.118, 0.0262), (0.335, 0.025), (0.338, 0.0275), (0.362, 0.0275), (0.364, 0.026)], 0.0205, 0.07, seg=32)
    b.add(ring(0.0328, 0.03, 0.08, 0.09, 0, 0, 32), METAL)
    # Hausse à échelle relevée (montants gradués, curseur à œilleton) et embase à charnière.
    b.add(box(0.03, 0.03, 0.007, (0, 0.15, 0.0275), 0.002), METAL)
    b.add(tube_x(0.004, 0.032, (0, 0.14, 0.033), 10), METAL)
    for s in (-1, 1):
        b.add(box(0.003, 0.007, 0.058, (s * 0.0115, 0.142, 0.062), 0.0008), METAL)
        for k in range(7):
            b.add(box(0.0008, 0.003, 0.0011, (s * 0.0131, 0.1435, 0.042 + 0.0075 * k), 0, 1), MARKING)
    b.add(box(0.026, 0.006, 0.005, (0, 0.142, 0.0915), 0.0012), METAL)
    b.add(box(0.03, 0.009, 0.01, (0, 0.142, 0.07), 0.002), METAL)
    b.add(ring(0.0045, 0.0022, 0.137, 0.147, 0, 0.079, 14), METAL)
    b.add(box(0.0032, 0.004, 0.006, (0.0165, 0.142, 0.07), 0.0008), METAL)                  # vis de blocage du curseur
    # Guidon à oreilles de protection.
    b.add(box(0.016, 0.018, 0.006, (0, 0.33, 0.0255), 0.0015), METAL)
    b.add(prism([(0.326, 0.026), (0.334, 0.026), (0.332, 0.041), (0.328, 0.041)], -0.0012, 0.0012, 0.0004), METAL)
    for s in (-1, 1):
        b.add(prism([(0.322, 0.026), (0.338, 0.026), (0.336, 0.04), (0.324, 0.038)], s * 0.0068 - 0.001, s * 0.0068 + 0.001, 0.0004), METAL)
    # Garde-main rainuré sous le canon, grenadière.
    fg = loft([(0.115, 0.03, 0.02, -0.014, 3.0), (0.125, 0.034, 0.025, -0.012, 3.0), (0.29, 0.034, 0.025, -0.012, 3.0), (0.3, 0.03, 0.02, -0.014, 3.0)], 28)
    fg = carve(fg, slots(0.14, 0.275, 7, 0.034, -0.02, 0.0065, 0.026, 0.008) + slots(0.14, 0.275, 7, -0.034, -0.02, 0.0065, 0.026, 0.008))
    b.add(fg, PAINT)
    swivel(b, 0.26, -0.037)
    gp = grip(b, -0.035, -0.046, 0.027)
    butt = stock_sculpt(b, [(-0.093, 0.024, 0.032, -0.012), (-0.2, 0.021, 0.042, -0.03), (-0.33, 0.022, 0.052, -0.038), (-0.345, 0.022, 0.051, -0.038)], PAINT)
    b.add(prism([(-0.13, 0.012), (-0.25, 0.006), (-0.27, -0.004), (-0.13, -0.002)], -0.02, 0.02, 0.003), PAINT)   # busc
    swivel(b, -0.3, -0.088)
    g.point("Muzzle", (0, 0.364, 0)); g.point("Grip", gp); g.point("Support", (0, 0.21, -0.06))
    g.point("Butt", butt); g.point("Mag", (0, 0.08, 0))


def brute(g):
    """Lance-grenades à trois canons en triangle : boîtier massif, plaque de culasse, colliers, bloc de bouche à guidon,
    poignée de transport à rail et viseur, rail inférieur et poignée verticale, levier d'ouverture, crosse lourde."""
    b = g.body
    C = [(0.0, 0.03), (-0.027, -0.016), (0.027, -0.016)]
    b.add(loft([(-0.1, 0.028, 0.032, -0.012, 4.0), (-0.06, 0.034, 0.044, -0.004, 4.0), (0.07, 0.05, 0.05, 0.004, 4.5), (0.115, 0.052, 0.05, 0.004, 4.5)], 32), PAINT)
    b.add(hull_u([(x, z, 0.03) for (x, z) in C], 0.11, 0.135), METAL)
    for (x, z) in C:
        bore_barrel(b, [(0.13, 0.025), (0.5, 0.025), (0.502, 0.0235)], 0.0195, 0.06, x, z, 28)
        b.add(ring(0.0262, 0.024, 0.2, 0.212, x, z, 28), METAL)
    b.add(hull_u([(x, z, 0.0285) for (x, z) in C], 0.255, 0.275), ACCENT)
    b.add(hull_u([(x, z, 0.0285) for (x, z) in C], 0.44, 0.475), METAL)
    b.add(prism([(0.452, 0.056), (0.466, 0.056), (0.463, 0.072), (0.455, 0.072)], -0.0014, 0.0014, 0.0004), METAL)
    for s in (-1, 1):
        b.add(prism([(0.448, 0.056), (0.47, 0.056), (0.468, 0.07), (0.45, 0.068)], s * 0.008 - 0.0012, s * 0.008 + 0.0012, 0.0004), METAL)
        for uu in (0.265, 0.458):
            screw(b, uu, 0.006, s * 0.044, 0.0026, s)
    # Flancs du boîtier : panneau rapporté vissé, levier d'ouverture à droite.
    for s in (-1, 1):
        b.add(box(0.003, 0.11, 0.05, (s * 0.049, 0.035, -0.002), 0.0012), ACCENT)
        for (uu, zz) in ((-0.012, 0.018), (0.082, 0.018), (-0.012, -0.022), (0.082, -0.022)):
            screw(b, uu, zz, s * 0.0505, 0.0022, s)
    b.add(prism([(0.03, 0.02), (0.1, 0.03), (0.104, 0.022), (0.04, 0.008)], 0.05, 0.057, 0.0012), METAL)
    b.add(tube_x(0.007, 0.012, (0.054, 0.035, 0.014), 14), METAL)
    # Poignée de transport à rail, viseur réflexe.
    hp = [(0, 0.0, 0.046), (0, 0.012, 0.08), (0, 0.03, 0.09), (0, 0.2, 0.09), (0, 0.222, 0.082), (0, 0.235, 0.056)]
    b.add(sweep(hp, rrect(0.016, 0.012, 0.004, 2), up=(1, 0, 0)), METAL)
    rail(b, 0.04, 0.19, 0.095)
    red_dot(b, 0.1, 0.105)
    # Rail inférieur et poignée verticale.
    b.add(hull_u([(-0.027, -0.016, 0.026), (0.027, -0.016, 0.026), (0, -0.04, 0.012)], 0.29, 0.42), PAINT)
    sv = vertical_grip(b, 0.35, -0.052)
    gp = grip(b, -0.045, -0.044, 0.034)
    butt = stock_sculpt(b, [(-0.098, 0.028, 0.034, -0.012), (-0.2, 0.026, 0.045, -0.026), (-0.34, 0.027, 0.056, -0.032), (-0.355, 0.027, 0.055, -0.032)], PAINT)
    b.add(box(0.03, 0.12, 0.01, (0, -0.24, 0.019), 0.004), RUBBER)                   # appui-joue
    swivel(b, -0.31, -0.086)
    g.point("Muzzle", (0, 0.5, 0.0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", butt); g.point("Mag", (0, 0.135, 0))


def crumble(g):
    """Double lance-grenades à canons superposés (type fusil de chasse) : bascule à platines vissées, clé d'ouverture sur
    le dessus, bande ventilée, flasque entre canons, devant quadrillé, deux détentes, crosse anglaise à busc."""
    b = g.body
    zt, zb = 0.0, -0.052
    rec = prism([(-0.09, 0.02), (0.0, 0.026), (0.1, 0.027), (0.1, -0.078), (0.07, -0.083), (-0.05, -0.083), (-0.09, -0.062)], -0.029, 0.029, 0.004)
    b.add(rec, PAINT)
    for s in (-1, 1):
        b.add(prism([(-0.075, 0.008), (0.05, 0.012), (0.075, -0.01), (0.07, -0.06), (-0.04, -0.072), (-0.075, -0.05)], s * 0.029 - 0.0012, s * 0.029 + 0.0012, 0.0012), ALLOY)
        for (uu, zz) in ((-0.06, -0.01), (0.04, -0.005), (0.01, -0.05), (-0.03, -0.035)):
            screw(b, uu, zz, s * 0.0302, 0.0026, s)
        b.add(tube_x(0.008, 0.002, (s * 0.03, 0.09, zb), 18), METAL)                        # axe de bascule
    # Clé d'ouverture sur le dessus.
    b.add(plan([(-0.005, -0.075), (0.005, -0.075), (0.026, -0.045), (0.024, -0.036), (-0.005, -0.052)], 0.022, 0.029), METAL)
    b.add(vcyl(0.0065, 0.0065, 0.024, 0.031, 0, -0.07, 14), METAL)
    for z in (zt, zb):
        bore_barrel(b, [(0.095, 0.0265), (0.13, 0.0265), (0.135, 0.024), (0.47, 0.024), (0.472, 0.0225)], 0.0195, 0.06, 0, z, 28)
    b.add(box(0.008, 0.36, 0.03, (0, 0.29, (zt + zb) / 2), 0.002), METAL)                  # flasque entre canons
    # Bande ventilée sur le canon supérieur.
    b.add(box(0.011, 0.36, 0.0028, (0, 0.29, 0.0295), 0.0008), METAL)
    for k in range(10):
        b.add(box(0.006, 0.01, 0.006, (0, 0.13 + 0.036 * k, 0.0255), 0.001), METAL)
    b.add(sphere(0.0028, (0, 0.462, 0.033), (1, 1, 1), 10, 6), MARKING)                    # guidon perle
    b.add(hull_u([(0, zt, 0.0262), (0, zb, 0.0262)], 0.448, 0.462), METAL)
    # Devant : bois sous le canon inférieur, rainures de quadrillage.
    fe = loft([(0.11, 0.028, 0.02, zb - 0.008, 3.0), (0.12, 0.033, 0.026, zb - 0.006, 3.0), (0.31, 0.031, 0.024, zb - 0.006, 3.0), (0.33, 0.026, 0.018, zb - 0.004, 3.0)], 28)
    cuts = []
    for s in (-1, 1):
        for k in range(4):
            cuts.append(box(0.006, 0.15, 0.0022, (s * 0.033, 0.22, zb - 0.02 + k * 0.007), 0.0008))
    b.add(carve(fe, cuts), ACCENT)
    gp = grip(b, -0.04, -0.081, 0.029, 20)
    b.add(prism([(-0.0125, -0.083), (-0.007, -0.083), (-0.009, -0.098), (-0.014, -0.11), (-0.017, -0.108), (-0.013, -0.097)], -0.0028, 0.0028, 0.0008), METAL)
    butt = stock_sculpt(b, [(-0.088, 0.025, 0.045, -0.032), (-0.2, 0.021, 0.045, -0.052), (-0.34, 0.022, 0.058, -0.064), (-0.355, 0.022, 0.057, -0.064)], ACCENT)
    b.add(box(0.003, 0.14, 0.03, (0.021, -0.25, -0.05), 0.001), PAINT)                    # écusson de crosse
    swivel(b, -0.3, -0.122)
    g.point("Muzzle", (0, 0.472, 0)); g.point("Grip", gp); g.point("Support", (0, 0.22, -0.088))
    g.point("Butt", butt); g.point("Mag", (0, 0.1, -0.026))


def stampede(g):
    """Revolver lance-grenades (type Milkor MGL) : barillet cannelé à six chambres chargées, plaques avant/arrière,
    pontet supérieur à rail, axe et tube de ressort, canon court à garde-main ajouré, poignée verticale, crosse télescopique."""
    b = g.body
    cz, rc = -0.042, 0.042
    fl = lambda a: 0.066 - 0.009 * max(0.0, math.cos(6 * (a - math.pi / 2) + math.pi)) ** 4
    b.add(revsurf([(0.012, 0), (0.012, 0.059), (0.017, fl), (0.15, fl), (0.155, 0.059), (0.155, 0)], 72, z=cz), PAINT)
    for k in range(6):
        a = math.pi / 2 + k * math.pi / 3
        x, z = math.cos(a) * rc, cz + math.sin(a) * rc
        b.add(ring(0.0225, 0.0195, 0.151, 0.158, x, z, 20), METAL)
        b.add(revsurf([(0.146, 0), (0.146, 0.0185), (0.152, 0.018), (0.156, 0.012), (0.158, 0)], 16, x=x, z=z), ACCENT)
    # Axe, plaque arrière, boîtier arrière.
    b.add(tube(0.012, 0.012, -0.004, 0.19, 0, cz, 18), METAL)
    b.add(tube(0.07, 0.07, -0.004, 0.012, 0, cz, 48), METAL)
    b.add(prism([(-0.1, 0.018), (-0.003, 0.03), (-0.003, -0.105), (-0.035, -0.112), (-0.1, -0.065)], -0.031, 0.031, 0.004), PAINT)
    for s in (-1, 1):
        screw(b, -0.07, -0.01, s * 0.032, 0.003, s)
        screw(b, -0.02, -0.08, s * 0.032, 0.003, s)
    # Plaque avant (canon, axe, tube de ressort), pontet supérieur et rail.
    b.add(hull_u([(0, 0, 0.034), (0, cz, 0.02), (0, -0.1, 0.012)], 0.158, 0.185), METAL)
    b.add(tube(0.0095, 0.0095, -0.004, 0.17, 0, -0.1, 16), METAL)
    b.add(box(0.03, 0.195, 0.011, (0, 0.09, 0.0345), 0.002), METAL)
    b.add(box(0.012, 0.03, 0.05, (0, 0.0, 0.01), 0.002), METAL)
    rail(b, 0.0, 0.18, 0.04)
    red_dot(b, 0.07, 0.0505)
    # Canon et garde-main ajouré.
    bore_barrel(b, [(0.17, 0.026), (0.42, 0.026), (0.425, 0.0245)], 0.0205, 0.06, seg=28)
    hg = loft([(0.2, 0.032, 0.032, 0.0, 4.0), (0.205, 0.035, 0.035, 0.0, 4.0), (0.37, 0.035, 0.035, 0.0, 4.0), (0.375, 0.032, 0.032, 0.0, 4.0)], 28)
    hg = carve(hg, slots(0.215, 0.36, 5, 0.035, 0.008, 0.016, 0.012, 0.01) + slots(0.215, 0.36, 5, -0.035, 0.008, 0.016, 0.012, 0.01))
    b.add(hg, POLY)
    rail(b, 0.21, 0.36, 0.035)
    sv = vertical_grip(b, 0.31, -0.034)
    gp = grip(b, -0.05, -0.068, 0.031)
    butt = stock_collapsible(b, -0.1, -0.34, -0.01)
    swivel(b, -0.08, -0.062)
    g.point("Muzzle", (0, 0.425, 0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", butt); g.point("Mag", (0, 0.08, cz))


def splash(g):
    """Gros tube à pompe : boîtier, collier, canon de 80 mm à anneaux de refroidissement et couronne crénelée, bouquet de
    sous-munitions dans la bouche, magasin tubulaire, devant de pompe rainuré et barres d'action, crosse squelette."""
    b = g.body
    b.add(prism([(-0.09, 0.03), (0.07, 0.046), (0.09, 0.046), (0.09, -0.058), (0.05, -0.062), (-0.05, -0.05), (-0.09, -0.035)], -0.032, 0.032, 0.004), PAINT)
    b.add(revsurf([(0.07, 0), (0.07, 0.054), (0.1, 0.056), (0.104, 0.052), (0.104, 0)], 40), METAL)
    br = revsurf([(0.1, 0), (0.1, 0.05), (0.39, 0.048), (0.395, 0.052), (0.43, 0.052), (0.43, 0.041), (0.37, 0.041), (0.37, 0)], 40)
    notches = []
    for k in range(8):
        c = box(0.012, 0.014, 0.03, (0, 0.43, 0.05), 0.002)
        notches.append(rot_u(c, 45 * k + 22.5))
    b.add(carve(br, notches), METAL)
    for k in range(7):
        b.add(ring(0.0515, 0.047, 0.14 + 0.03 * k, 0.148 + 0.03 * k, 0, 0, 40), PAINT)
    # Sous-munitions dans la bouche.
    cl = [(0, 0)] + [(math.cos(a) * 0.024, math.sin(a) * 0.024) for a in (math.radians(30 + 60 * k) for k in range(6))]
    for (x, z) in cl:
        b.add(sphere(0.0118, (x, 0.405, z), (1, 1.3, 1), 14, 8), ACCENT)
        b.add(ring(0.0122, 0.009, 0.399, 0.403, x, z, 14), METAL)
    # Magasin tubulaire, devant de pompe, barres d'action, collier avant.
    b.add(tube(0.021, 0.021, 0.09, 0.37, 0, -0.082, 24), METAL)
    b.add(revsurf([(0.37, 0.021), (0.372, lambda a: 0.023 + 0.0006 * math.cos(16 * a)), (0.385, lambda a: 0.023 + 0.0006 * math.cos(16 * a)), (0.388, 0.018), (0.388, 0)], 32, z=-0.082), METAL)
    fe = loft([(0.16, 0.026, 0.025, -0.084, 3.0), (0.168, 0.031, 0.03, -0.086, 3.0), (0.29, 0.031, 0.03, -0.086, 3.0), (0.298, 0.026, 0.025, -0.084, 3.0)], 28)
    cuts = []
    for k in range(8):
        cuts.append(box(0.08, 0.005, 0.016, (0, 0.178 + 0.015 * k, -0.112), 0.0015))
    b.add(carve(fe, cuts), POLY)
    for s in (-1, 1):
        b.add(box(0.004, 0.13, 0.007, (s * 0.024, 0.1, -0.072), 0.001), METAL)
    b.add(hull_u([(0, 0, 0.053), (0, -0.082, 0.024)], 0.345, 0.365), METAL)
    # Rehausse à rail et viseur.
    b.add(box(0.024, 0.15, 0.008, (0, 0.08, 0.052), 0.002), METAL)
    rail(b, 0.01, 0.15, 0.056)
    red_dot(b, 0.07, 0.0664)
    gp = grip(b, -0.035, -0.05, 0.032)
    # Crosse squelette ajourée.
    st = prism([(-0.088, 0.024), (-0.33, 0.002), (-0.345, 0.006), (-0.345, -0.084), (-0.33, -0.09), (-0.088, -0.034)], -0.016, 0.016, 0.004)
    st = carve(st, [prism([(-0.12, 0.0), (-0.3, -0.012), (-0.3, -0.03), (-0.12, -0.016)], -0.03, 0.03, 0.004),
                    prism([(-0.15, -0.032), (-0.3, -0.05), (-0.3, -0.07), (-0.15, -0.04)], -0.03, 0.03, 0.002)])
    b.add(st, POLY)
    b.add(box(0.036, 0.012, 0.1, (0, -0.351, -0.04), 0.004), RUBBER)
    g.point("Muzzle", (0, 0.43, 0)); g.point("Grip", gp); g.point("Support", (0, 0.235, -0.112))
    g.point("Butt", (0, -0.355, -0.04)); g.point("Mag", (0, 0.2, -0.082))


def sticky(g):
    """Lanceur de grenades collantes : chargeur camembert sur le dessus (nervures rayonnantes, moyeu), canon à manchon
    ajouré et griffes de guidage, cartouche de gel adhésif à hublots sous le canon et durite d'injection, crosse courte."""
    b = g.body
    b.add(loft([(-0.12, 0.03, 0.034, -0.012, 4.0), (-0.1, 0.034, 0.04, -0.01, 5.0), (0.1, 0.034, 0.04, -0.006, 5.0), (0.125, 0.03, 0.034, -0.004, 4.0)], 32), PAINT)
    for s in (-1, 1):
        b.add(box(0.003, 0.15, 0.03, (s * 0.034, 0.0, -0.01), 0.001), ACCENT)
        for uu in (-0.065, 0.065):
            screw(b, uu, -0.01, s * 0.0355, 0.0024, s)
    # Chargeur camembert.
    rb = lambda a: 0.073 + 0.0028 * max(0.0, math.cos(20 * a)) ** 4
    b.add(revsurf([(0.032, 0), (0.032, 0.066), (0.036, rb), (0.058, rb), (0.063, 0.069), (0.063, 0)], 120, axis='z'), PAINT)
    for r0 in (0.03, 0.05):
        b.add(revsurf([(0.0628, r0), (0.0648, r0 + 0.002), (0.0648, r0 + 0.006), (0.0628, r0 + 0.008)], 48, axis='z', caps=False), METAL)
    b.add(vcyl(0.02, 0.018, 0.062, 0.071, 0, 0, 24), METAL)
    for k in range(6):
        a = 2 * math.pi * k / 6
        bolt_top(b, math.cos(a) * 0.014, math.sin(a) * 0.014, 0.071, 0.0022)
    b.add(box(0.03, 0.05, 0.012, (0, 0.0, 0.028), 0.003), METAL)                        # col d'alimentation
    b.add(box(0.008, 0.02, 0.008, (0.07, 0.03, 0.05), 0.002), ACCENT)                    # loquet
    rail(b, -0.025, 0.025, 0.071)
    red_dot(b, 0.0, 0.0814)
    # Canon, manchon ajouré, griffes de guidage.
    bore_barrel(b, [(0.12, 0.028), (0.4, 0.028), (0.405, 0.026)], 0.0215, 0.05, seg=28)
    sl = ring(0.037, 0.0335, 0.14, 0.31, 0, 0, 40)
    cuts = []
    for k in range(8):
        cuts += [rot_u(c, 45 * k) for c in slots(0.155, 0.295, 3, 0.036, 0.0, 0.03, 0.009, 0.01)]
    b.add(carve(sl, cuts), ACCENT)
    for uu in (0.14, 0.31):
        b.add(ring(0.039, 0.033, uu - 0.006, uu + 0.006, 0, 0, 40), METAL)
    for k in range(6):
        fin = prism([(0.33, 0.026), (0.395, 0.03), (0.415, 0.04), (0.405, 0.043), (0.35, 0.036)], -0.0028, 0.0028, 0.0008)
        b.add(rot_u(fin, 60 * k + 30), METAL)
    # Cartouche de gel adhésif à hublots, durite d'injection.
    can = revsurf([(0.03, 0), (0.03, 0.022), (0.035, 0.027), (0.255, 0.027), (0.26, 0.022), (0.26, 0)], 32, z=-0.064)
    can = carve(can, [box(0.03, 0.05, 0.012, (s * 0.027, uu, -0.064), 0.004) for s in (-1, 1) for uu in (0.09, 0.19)])
    b.add(can, METAL)
    b.add(tube(0.0235, 0.0235, 0.04, 0.25, 0, -0.064, 24), ENERGY)
    b.add(revsurf([(0.26, 0.02), (0.262, lambda a: 0.022 + 0.0006 * math.cos(14 * a)), (0.272, lambda a: 0.022 + 0.0006 * math.cos(14 * a)), (0.274, 0)], 28, z=-0.064), METAL)
    hose(b, bez((0, 0.275, -0.064), (0, 0.3, -0.064), (0, 0.33, -0.05), (0, 0.33, -0.032), 10), 0.005)
    gp = grip(b, -0.05, -0.05, 0.034)
    butt = stock_sculpt(b, [(-0.118, 0.026, 0.03, -0.012), (-0.2, 0.024, 0.04, -0.022), (-0.28, 0.024, 0.048, -0.026), (-0.293, 0.023, 0.047, -0.026)], POLY)
    g.point("Muzzle", (0, 0.405, 0)); g.point("Grip", gp); g.point("Support", (0, 0.16, -0.092))
    g.point("Butt", butt); g.point("Mag", (0, 0.0, 0.05))


def napalm(g):
    """Lanceur de napalm : boîtier, canon à manchon pare-chaleur percé et bec évasé, électrode d'allumage, réservoir de gel
    latéral à fonds bombés (colliers, regard vitré, manomètre, vanne à volant), durite vers le canon, garde-main, crosse."""
    b = g.body
    b.add(loft([(-0.1, 0.024, 0.03, -0.012, 4.0), (-0.08, 0.028, 0.036, -0.01, 5.0), (0.1, 0.028, 0.036, -0.006, 5.0), (0.12, 0.024, 0.03, -0.004, 4.0)], 32), PAINT)
    side_panel(b, -0.085, -0.005, -0.024, 0.014, 0.028)
    bore_barrel(b, [(0.1, 0.03), (0.42, 0.03)], 0.024, 0.05, seg=28)
    sh = ring(0.038, 0.0345, 0.26, 0.415, 0, 0, 36)
    cuts = []
    for k in range(5):
        cuts += holes_ring(0.278 + 0.031 * k, 0.036, 10, 0.0042, 0.012)
    b.add(carve(sh, cuts), METAL)
    for uu in (0.26, 0.415):
        b.add(ring(0.04, 0.03, uu - 0.005, uu + 0.005, 0, 0, 36), METAL)
    bore_barrel(b, [(0.415, 0.031), (0.44, 0.034), (0.47, 0.043), (0.482, 0.045), (0.484, 0.043)], 0.037, 0.05, seg=32)
    b.add(tube(0.004, 0.004, 0.43, 0.485, 0, -0.05, 10), METAL)
    b.add(sweep([(0, 0.43, -0.044), (0, 0.43, -0.05)], circ(0.004, 8)), METAL)
    b.add(sphere(0.0045, (0, 0.488, -0.05), (1, 1, 1), 10, 6), GLOW)
    # Réservoir de gel sur le flanc droit.
    tx, tz = -0.068, -0.026
    b.add(capsule(0.0, 0.3, 0.034, tx, tz, 0.026, 32, 5, ((0.09, 0.0015), (0.2, 0.0015))), PAINT)
    for uu in (0.04, 0.26):
        b.add(ring(0.0372, 0.033, uu - 0.007, uu + 0.007, tx, tz, 32), METAL)
        b.add(box(0.018, 0.012, 0.012, (-0.037, uu, tz), 0.002), METAL)                     # bras de fixation
        b.add(box(0.008, 0.012, 0.008, (tx, uu, tz - 0.04), 0.0015), METAL)
        b.add(tube_x(0.002, 0.018, (tx, uu, tz - 0.042), 8), METAL)
    b.add(box(0.004, 0.17, 0.014, (tx - 0.0335, 0.15, tz), 0.0015), METAL)
    b.add(box(0.003, 0.15, 0.008, (tx - 0.0355, 0.15, tz), 0.001), GLASS)
    gauge(b, tx, 0.07, tz + 0.034, 0.011)
    b.add(vcyl(0.005, 0.005, tz + 0.03, tz + 0.05, tx, 0.22, 10), METAL)
    handwheel(b, (tx, 0.22, tz + 0.052), 0.012, 'z')
    hose(b, bez((tx, 0.31, tz), (tx, 0.345, tz), (-0.02, 0.3, -0.06), (0.0, 0.27, -0.04), 12), 0.0055)
    b.add(loft([(0.13, 0.026, 0.02, -0.03, 3.0), (0.14, 0.031, 0.026, -0.03, 3.0), (0.24, 0.031, 0.026, -0.03, 3.0), (0.25, 0.026, 0.02, -0.03, 3.0)], 24), POLY)
    for k in range(5):
        b.add(ring(0.0318, 0.026, 0.153 + 0.018 * k, 0.159 + 0.018 * k, 0, -0.03, 24), RUBBER)
    rail(b, -0.07, 0.07, 0.026)
    red_dot(b, 0.0, 0.0364)
    gp = grip(b, -0.035, -0.045, 0.028)
    butt = stock_sculpt(b, [(-0.098, 0.022, 0.028, -0.012), (-0.2, 0.021, 0.04, -0.024), (-0.32, 0.022, 0.05, -0.03), (-0.335, 0.021, 0.049, -0.03)], PAINT)
    g.point("Muzzle", (0, 0.484, 0)); g.point("Grip", gp); g.point("Support", (0, 0.2, -0.058))
    g.point("Butt", butt); g.point("Mag", (tx, 0.15, tz))


def geyser(g):
    """Flame Geyser : canon lourd à radiateur de disques et tirants, pavillon évasé à veilleuse annulaire, deux bouteilles
    de propergol sous le canon (collecteur, tuyau), boîtier d'allumage et câble, manchon de prise, crosse télescopique."""
    b = g.body
    b.add(loft([(-0.1, 0.026, 0.032, -0.014, 4.0), (-0.08, 0.03, 0.04, -0.012, 5.0), (0.1, 0.03, 0.04, -0.008, 5.0), (0.12, 0.026, 0.034, -0.006, 4.0)], 32), PAINT)
    side_panel(b, -0.07, 0.07, -0.03, 0.014, 0.03)
    bore_barrel(b, [(0.1, 0.032), (0.49, 0.032)], 0.025, 0.05, seg=28)
    for k in range(9):
        uu = 0.165 + 0.021 * k
        b.add(ring(0.062, 0.03, uu, uu + 0.0045, 0, 0, 40), ACCENT)
    for uu in (0.15, 0.345):
        b.add(ring(0.064, 0.03, uu, uu + 0.012, 0, 0, 40), METAL)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        x, z = math.cos(a) * 0.054, math.sin(a) * 0.054
        b.add(tube(0.0028, 0.0028, 0.145, 0.362, x, z, 8), METAL)
        hexnut(b, 0.141, 0.147, 0.0045, x, z)
        hexnut(b, 0.358, 0.364, 0.0045, x, z)
    # Manchon de prise caoutchouté.
    b.add(revsurf([(0.37, 0.033), (0.372, 0.037)] + [p for k in range(6) for p in ((0.378 + 0.015 * k, 0.037), (0.381 + 0.015 * k, 0.0395), (0.387 + 0.015 * k, 0.0395), (0.39 + 0.015 * k, 0.037))] + [(0.47, 0.037), (0.472, 0.033)], 28, caps=False), RUBBER)
    # Pavillon et veilleuse annulaire.
    bore_barrel(b, [(0.48, 0.034), (0.5, 0.036), (0.53, 0.046), (0.552, 0.056), (0.56, 0.057), (0.562, 0.054)], 0.048, 0.06, seg=40)
    b.add(ring(0.044, 0.04, 0.53, 0.536, 0, 0, 32), GLOW)
    # Bouteilles de propergol et collecteur.
    for s in (-1, 1):
        b.add(capsule(-0.03, 0.2, 0.022, s * 0.026, -0.078, 0.018, 24, 4, ((0.03, 0.0012), (0.14, 0.0012))), PAINT)
        b.add(ring(0.0245, 0.021, 0.08, 0.092, s * 0.026, -0.078, 24), METAL)
    b.add(box(0.084, 0.02, 0.026, (0, 0.21, -0.078), 0.004), METAL)
    b.add(box(0.03, 0.01, 0.04, (0, 0.08, -0.048), 0.003), METAL)
    hose(b, bez((0, 0.22, -0.078), (0, 0.25, -0.078), (0, 0.27, -0.065), (0, 0.27, -0.03), 10), 0.006, METAL, METAL)
    gauge(b, 0.042, 0.21, -0.078, 0.01, 'x')
    # Boîtier d'allumage et câble vers le pavillon.
    b.add(box(0.022, 0.06, 0.018, (0.0, 0.07, 0.04), 0.003), METAL)
    b.add(box(0.016, 0.02, 0.004, (0.0, 0.085, 0.05), 0.001), GLOW)
    b.add(sweep(bez((0.008, 0.1, 0.044), (0.03, 0.2, 0.072), (0.03, 0.42, 0.06), (0.03, 0.5, 0.034), 16), circ(0.0022, 6)), RUBBER)
    gp = grip(b, -0.04, -0.048, 0.03)
    butt = stock_collapsible(b, -0.1, -0.35, -0.012)
    g.point("Muzzle", (0, 0.562, 0)); g.point("Grip", gp); g.point("Support", (0, 0.42, -0.04))
    g.point("Butt", butt); g.point("Mag", (0, 0.1, -0.078))


# ───────────────────────── Armes spéciales ─────────────────────────

def acid(g):
    """Pulvérisateur d'acide : réservoir translucide encagé (barres, bagues boulonnées, bouchon de remplissage à poignée en
    T), selles, pompe à piston sur le flanc gauche, durite, lance fine à colliers, buse à jet plat et garde, poignée verticale."""
    b = g.body
    b.add(loft([(-0.1, 0.024, 0.03, -0.014, 4.0), (-0.08, 0.026, 0.034, -0.012, 5.0), (0.16, 0.026, 0.034, -0.008, 5.0), (0.185, 0.022, 0.028, -0.006, 4.0)], 32), PAINT)
    side_panel(b, -0.075, 0.07, -0.026, 0.012, 0.026)
    tz = 0.072
    b.add(capsule(-0.075, 0.17, 0.038, 0, tz, 0.024, 32, 5), ENERGY)
    for uu in (-0.062, 0.157):
        b.add(ring(0.043, 0.036, uu - 0.01, uu + 0.01, 0, tz, 36), METAL)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        b.add(tube(0.003, 0.003, -0.062, 0.157, math.cos(a) * 0.041, tz + math.sin(a) * 0.041, 8), METAL)
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        for uu in (-0.074, 0.169):
            b.add(sphere(0.0022, (math.cos(a) * 0.04, uu, tz + math.sin(a) * 0.04), (1, 1, 1), 8, 4), METAL)
    b.add(vcyl(0.012, 0.012, tz + 0.034, tz + 0.046, 0, 0.12, 24), METAL)
    b.add(box(0.03, 0.006, 0.006, (0, 0.12, tz + 0.05), 0.0015), METAL)
    for uu in (-0.03, 0.12):
        b.add(prism([(uu - 0.012, 0.018), (uu + 0.012, 0.018), (uu + 0.012, 0.04), (uu - 0.012, 0.04)], -0.02, 0.02, 0.002), METAL)
    # Pompe à piston (flanc gauche).
    b.add(tube(0.012, 0.012, -0.06, 0.13, -0.04, 0.0, 20), METAL)
    b.add(ring(0.0138, 0.011, 0.122, 0.13, -0.04, 0.0, 20), METAL)
    b.add(tube(0.0035, 0.0035, -0.1, -0.06, -0.04, 0.0, 8), ALLOY)
    b.add(box(0.008, 0.008, 0.034, (-0.04, -0.102, 0.0), 0.0025), POLY)
    for uu in (-0.03, 0.09):
        b.add(box(0.016, 0.01, 0.01, (-0.032, uu, 0.0), 0.002), METAL)
    hose(b, bez((-0.04, 0.135, 0.0), (-0.04, 0.165, 0.0), (-0.03, 0.19, 0.04), (-0.012, 0.18, 0.062), 10), 0.004)
    hose(b, bez((0, 0.19, tz - 0.01), (0, 0.22, tz - 0.01), (0, 0.22, 0.02), (0, 0.205, 0.004), 10), 0.006)
    # Lance, colliers, buse à jet plat.
    b.add(tube(0.009, 0.009, 0.18, 0.56, 0, 0.0, 16), METAL)
    b.add(loft([(0.24, 0.018, 0.016, -0.004, 3.0), (0.25, 0.022, 0.02, -0.004, 3.0), (0.35, 0.022, 0.02, -0.004, 3.0), (0.36, 0.018, 0.016, -0.004, 3.0)], 24), POLY)
    for uu in (0.42, 0.5):
        b.add(ring(0.0125, 0.008, uu - 0.004, uu + 0.004, 0, 0, 16), METAL)
        hexnut(b, uu - 0.006, uu + 0.006, 0.0035, 0.0, -0.013)
    b.add(revsurf([(0.555, 0), (0.555, 0.012), (0.575, 0.013), (0.58, 0.01), (0.585, 0)], 20), ACCENT)
    b.add(prism([(0.578, 0.0045), (0.6, 0.007), (0.602, -0.007), (0.578, -0.0045)], -0.016, 0.016, 0.0015), METAL)
    b.add(box(0.026, 0.002, 0.0018, (0, 0.6025, 0.0), 0, 1), POLY)
    b.add(ring(0.024, 0.021, 0.545, 0.552, 0, 0, 28), METAL)
    sv = vertical_grip(b, 0.3, -0.024)
    gp = grip(b, -0.04, -0.044, 0.026)
    butt = stock_sculpt(b, [(-0.098, 0.022, 0.028, -0.014), (-0.22, 0.021, 0.042, -0.024), (-0.33, 0.022, 0.05, -0.028), (-0.345, 0.021, 0.049, -0.028)], PAINT)
    g.point("Muzzle", (0, 0.602, 0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", butt); g.point("Mag", (0, 0.05, tz))


def flamer(g):
    """Lance-flammes (type M2) : deux bouteilles jumelles à fonds bombés et sangles, robinets à volant, collecteur et
    durite, lance à manchon isolant bagué, pare-chaleur percé, buse, veilleuse et cartouche d'allumage, manomètre."""
    b = g.body
    b.add(loft([(-0.1, 0.024, 0.028, -0.012, 4.0), (-0.08, 0.026, 0.032, -0.01, 5.0), (0.14, 0.026, 0.032, -0.008, 5.0), (0.165, 0.022, 0.026, -0.006, 4.0)], 32), PAINT)
    side_panel(b, -0.07, 0.07, -0.022, 0.012, 0.026)
    for s in (-1, 1):
        x = s * 0.036
        b.add(capsule(-0.16, 0.14, 0.032, x, -0.078, 0.024, 28, 5, ((-0.05, 0.0012), (0.05, 0.0012))), ACCENT)
        b.add(tube(0.008, 0.008, 0.135, 0.17, x, -0.078, 12), METAL)
        hexnut(b, 0.15, 0.158, 0.011, x, -0.078)
        handwheel(b, (x, 0.162, -0.052), 0.009, 'z', ACCENT)
        b.add(vcyl(0.0035, 0.0035, -0.07, -0.052, x, 0.162, 8), METAL)
    for uu in (-0.1, 0.08):
        b.add(sweep([(x, uu, z) for (x, z) in rrect(0.146, 0.072, 0.03, 4)], rrect(0.012, 0.002, 0.0008, 1), closed=True, up=(0, 1, 0)), POLY)
        b.add(box(0.016, 0.014, 0.008, (0, uu, -0.042), 0.002), METAL)
    b.add(tube_x(0.008, 0.08, (0, 0.17, -0.078), 14), METAL)
    hose(b, bez((0, 0.175, -0.078), (0, 0.21, -0.078), (0, 0.2, -0.03), (0, 0.18, -0.012), 10), 0.007)
    # Lance, manchon isolant, pare-chaleur, buse.
    b.add(tube(0.016, 0.016, 0.16, 0.565, 0, 0.0, 20), METAL)
    b.add(loft([(0.19, 0.022, 0.022, 0, 2.0), (0.2, 0.028, 0.028, 0, 2.0), (0.37, 0.028, 0.028, 0, 2.0), (0.38, 0.022, 0.022, 0, 2.0)], 24), POLY)
    for k in range(6):
        b.add(ring(0.031, 0.027, 0.215 + 0.028 * k, 0.222 + 0.028 * k, 0, 0, 24), RUBBER)
    sh = ring(0.027, 0.0235, 0.43, 0.56, 0, 0, 28)
    cuts = []
    for k in range(4):
        cuts += holes_ring(0.447 + 0.03 * k, 0.025, 8, 0.0036, 0.01)
    b.add(carve(sh, cuts), METAL)
    b.add(ring(0.029, 0.016, 0.425, 0.433, 0, 0, 28), METAL)
    bore_barrel(b, [(0.555, 0.019), (0.575, 0.017), (0.6, 0.012), (0.605, 0.0125)], 0.007, 0.02, seg=20)
    # Veilleuse et cartouche d'allumage.
    b.add(tube(0.0045, 0.0045, 0.47, 0.588, 0, -0.035, 10), METAL)
    b.add(revsurf([(0.588, 0.0045), (0.594, 0.006), (0.596, 0.004), (0.596, 0)], 10, z=-0.035), METAL)
    b.add(sphere(0.0055, (0, 0.601, -0.035), (1, 1.4, 1), 10, 6), GLOW)
    fl = lambda a: 0.014 - 0.0018 * max(0.0, math.cos(6 * a)) ** 2
    b.add(revsurf([(0.44, 0), (0.44, 0.012), (0.443, fl), (0.478, fl), (0.481, 0.012), (0.481, 0)], 36, z=-0.04), METAL)
    b.add(box(0.008, 0.02, 0.012, (0, 0.46, -0.026), 0.002), METAL)
    b.add(sweep(bez((0.006, 0.44, -0.042), (0.03, 0.4, -0.05), (0.03, 0.3, -0.06), (0.02, 0.17, -0.04), 14), circ(0.0022, 6)), RUBBER)
    gauge(b, 0, 0.06, 0.022, 0.012)
    sv = vertical_grip(b, 0.29, -0.028)
    gp = grip(b, -0.03, -0.042, 0.026)
    butt = stock_sculpt(b, [(-0.098, 0.022, 0.028, -0.008), (-0.2, 0.021, 0.038, -0.016), (-0.3, 0.022, 0.045, -0.02), (-0.315, 0.021, 0.044, -0.02)], PAINT)
    g.point("Muzzle", (0, 0.605, 0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", butt); g.point("Mag", (0, 0.0, -0.078))


def flamecannon(g):
    """Canon à flammes lourd : boîtier massif, fût ajouré à lumières en quinconce, tête de bouche à trois veilleuses,
    gros bidon sous le canon (cerclages, régulateur à dôme, durite), poignée de transport, poignée verticale."""
    b = g.body
    b.add(loft([(-0.12, 0.03, 0.036, -0.01, 4.0), (-0.1, 0.034, 0.044, -0.006, 5.0), (0.12, 0.036, 0.046, -0.002, 5.0), (0.145, 0.03, 0.04, 0.0, 4.0)], 32), PAINT)
    for s in (-1, 1):
        b.add(box(0.003, 0.17, 0.04, (s * 0.035, 0.0, -0.004), 0.0012), ACCENT)
        for (uu, zz) in ((-0.075, 0.01), (0.075, 0.01), (-0.075, -0.018), (0.075, -0.018)):
            screw(b, uu, zz, s * 0.0365, 0.0024, s)
    bore_barrel(b, [(0.12, 0.03), (0.6, 0.03)], 0.022, 0.05, seg=28)
    jk2 = ring(0.046, 0.042, 0.16, 0.5, 0, 0, 40)
    cuts = []
    for k in range(8):
        off = 0.0 if k % 2 == 0 else 0.02
        cuts += [rot_u(c, 45 * k) for c in slots(0.175 + off, 0.485 + off, 7, 0.045, 0.0, 0.026, 0.011, 0.012)]
    b.add(carve(jk2, cuts), METAL)
    for uu in (0.155, 0.505):
        b.add(ring(0.05, 0.03, uu - 0.008, uu + 0.008, 0, 0, 40), METAL)
    # Tête de bouche et veilleuses.
    bore_barrel(b, [(0.59, 0.042), (0.6, 0.05), (0.625, 0.05), (0.64, 0.044), (0.645, 0.04)], 0.024, 0.03, seg=36)
    for k in range(3):
        a = math.radians(90 + 120 * k)
        x, z = math.cos(a) * 0.052, math.sin(a) * 0.052
        b.add(tube(0.006, 0.0045, 0.6, 0.652, x, z, 10), METAL)
        b.add(sphere(0.005, (x, 0.656, z), (1, 1.3, 1), 10, 6), GLOW)
    # Bidon sous le canon.
    dz = -0.1
    b.add(capsule(0.04, 0.31, 0.048, 0, dz, 0.03, 36, 5, ((0.1, 0.002), (0.25, 0.002))), ACCENT)
    for uu in (0.075, 0.275):
        b.add(ring(0.0515, 0.046, uu - 0.008, uu + 0.008, 0, dz, 36), METAL)
        b.add(box(0.03, 0.016, 0.034, (0, uu, -0.04), 0.003), METAL)
    b.add(revsurf([(0.0, 0), (0.0, 0.016), (0.015, 0.016), (0.02, 0.012), (0.03, 0.006), (0.04, 0.0)], 20, z=dz), METAL)
    gauge(b, 0.048, 0.17, dz, 0.012, 'x')
    hose(b, bez((0, 0.31, dz), (0, 0.36, dz), (0, 0.37, -0.06), (0, 0.35, -0.044), 12), 0.008)
    # Poignée de transport.
    b.add(sweep([(0, -0.02, 0.04), (0, -0.005, 0.078), (0, 0.02, 0.09), (0, 0.18, 0.09), (0, 0.205, 0.078), (0, 0.22, 0.045)], rrect(0.018, 0.014, 0.005, 2), up=(1, 0, 0)), METAL)
    b.add(loft([(0.04, 0.012, 0.009, 0.092, 3.0), (0.16, 0.012, 0.009, 0.092, 3.0)], 18), RUBBER)
    sv = vertical_grip(b, 0.39, -0.045)
    gp = grip(b, -0.045, -0.048, 0.034)
    butt = stock_sculpt(b, [(-0.118, 0.026, 0.034, -0.01), (-0.22, 0.025, 0.044, -0.022), (-0.34, 0.026, 0.052, -0.028), (-0.355, 0.025, 0.051, -0.028)], PAINT)
    g.point("Muzzle", (0, 0.645, 0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", butt); g.point("Mag", (0, 0.17, dz))


def firecracker(g):
    """Lance-pétards : faisceau de 5 × 4 tubes chargés, flasques latérales ajourées, cloisons, cerclages, capot à rail et
    viseur, boîtier de mise à feu et câbles, poignée sous le faisceau, appui d'épaule."""
    b = g.body
    xs = [(-2 + k) * 0.024 for k in range(5)]
    zs = [(-1.5 + k) * 0.024 for k in range(4)]
    for x in xs:
        for z in zs:
            b.add(ring(0.0112, 0.0094, 0.005, 0.335, x, z, 16), POLY)
            b.add(revsurf([(0.28, 0), (0.28, 0.0092), (0.3, 0.009), (0.312, 0.005), (0.316, 0)], 12, x=x, z=z), ACCENT)
    for uu in (0.0, 0.16, 0.3):
        pl = box(0.128, 0.01, 0.104, (0, uu, 0), 0.003)
        if uu > 0.25:
            pl = carve(pl, [tube(0.0105, 0.0105, uu - 0.02, uu + 0.02, x, z, 16) for x in xs for z in zs])
        b.add(pl, PAINT)
    for s in (-1, 1):
        sp = prism([(-0.004, -0.054), (0.31, -0.054), (0.318, -0.046), (0.318, 0.046), (0.31, 0.054), (-0.004, 0.054)], s * 0.064 - 0.002, s * 0.064 + 0.002, 0.0015)
        sp = carve(sp, [tube_x(0.022, 0.02, (s * 0.064, uu, 0.0), 24) for uu in (0.08, 0.235)])
        b.add(sp, PAINT)
        for uu in (0.0, 0.16, 0.3):
            for zz in (-0.04, 0.04):
                screw(b, uu, zz, s * 0.066, 0.0024, s)
    for uu in (0.08, 0.235):
        b.add(sweep([(x, uu, z) for (x, z) in rrect(0.136, 0.112, 0.008, 3)], rrect(0.014, 0.0022, 0.0008, 1), closed=True, up=(0, 1, 0)), METAL)
    # Capot supérieur à rail, viseur.
    b.add(box(0.07, 0.24, 0.006, (0, 0.15, 0.056), 0.002), PAINT)
    rail(b, 0.06, 0.24, 0.059)
    red_dot(b, 0.15, 0.0694)
    # Boîtier de mise à feu et câbles vers la poignée.
    b.add(box(0.06, 0.03, 0.04, (0, -0.02, -0.02), 0.004), METAL)
    b.add(box(0.02, 0.004, 0.012, (0.012, -0.036, -0.008), 0.001), GLOW)
    for k, x in enumerate((-0.012, 0.012)):
        b.add(sweep(bez((x, -0.02, -0.04), (x, -0.02, -0.06), (x * 1.5, 0.01, -0.07), (x * 0.5, 0.03, -0.07), 10), circ(0.002, 6)), RUBBER if k else ACCENT)
    b.add(box(0.04, 0.12, 0.012, (0, 0.07, -0.058), 0.003), METAL)
    gp = grip(b, 0.05, -0.062, 0.02)
    st = prism([(0.0, -0.02), (-0.18, -0.034), (-0.2, -0.03), (-0.2, -0.1), (-0.185, -0.105), (-0.02, -0.06)], -0.016, 0.016, 0.003)
    st = carve(st, [prism([(-0.04, -0.036), (-0.16, -0.045), (-0.16, -0.08), (-0.06, -0.062)], -0.03, 0.03, 0.004)])
    b.add(st, POLY)
    b.add(box(0.036, 0.012, 0.074, (0, -0.206, -0.067), 0.004), RUBBER)
    g.point("Muzzle", (0, 0.335, 0)); g.point("Grip", gp); g.point("Support", (0, 0.2, -0.056))
    g.point("Butt", (0, -0.21, -0.067)); g.point("Mag", (0, 0.15, 0))


def boundgun(g):
    """Arme à balles rebondissantes : boîtier supérieur à fenêtre d'éjection et levier d'armement, carcasse et puits de
    chargeur, garde-main ajouré à rail, déflecteur de bouche à ailettes vrillées, crosse squelette à charnière."""
    b = g.body
    up = prism([(-0.11, 0.022), (0.15, 0.024), (0.16, 0.017), (0.16, -0.02), (0.12, -0.028), (-0.02, -0.028), (-0.11, -0.018)], -0.022, 0.022, 0.003)
    up = carve(up, [box(0.01, 0.05, 0.014, (0.022, 0.02, 0.006), 0.003), box(0.006, 0.09, 0.004, (-0.022, 0.05, 0.012), 0.0015)])
    b.add(up, PAINT)
    b.add(box(0.002, 0.044, 0.008, (0.019, 0.02, 0.006), 0.0008), METAL)                 # culasse visible
    b.add(box(0.012, 0.01, 0.008, (-0.026, 0.085, 0.012), 0.002), METAL)                  # levier d'armement
    b.add(prism([(-0.06, -0.026), (0.115, -0.026), (0.11, -0.05), (0.04, -0.05), (-0.06, -0.044)], -0.02, 0.02, 0.003), POLY)
    hg = loft([(0.155, 0.02, 0.02, 0.0, 4.0), (0.16, 0.022, 0.022, 0.0, 5.0), (0.3, 0.022, 0.022, 0.0, 5.0), (0.305, 0.02, 0.02, 0.0, 4.0)], 28)
    hg = carve(hg, slots(0.17, 0.29, 5, 0.022, 0.0, 0.012, 0.011, 0.01) + slots(0.17, 0.29, 5, -0.022, 0.0, 0.012, 0.011, 0.01))
    b.add(hg, ALLOY)
    b.add(tube(0.012, 0.012, 0.16, 0.3, 0, 0, 16), METAL)
    rail(b, -0.09, 0.3, 0.023)
    b.add(tube(0.008, 0.008, 0.3, 0.355, 0, 0, 16), METAL)
    dv = revsurf([(0.35, 0), (0.35, 0.014), (0.355, 0.016), (0.42, 0.016), (0.425, 0.013), (0.425, 0.006), (0.4, 0.006), (0.4, 0)], 24)
    dv = carve(dv, [rot_u(box(0.04, 0.012, 0.006, (0, 0.392, 0.0), 0.002), 45 + 90 * k) for k in range(2)])
    b.add(dv, METAL)
    for k in range(4):
        fin = prism([(0.36, 0.014), (0.41, 0.016), (0.43, 0.024), (0.41, 0.026), (0.37, 0.02)], -0.0022, 0.0022, 0.0006)
        bmesh.ops.rotate(fin, cent=P(0, 0.395, 0.02), matrix=Matrix.Rotation(math.radians(14), 3, 'Z'), verts=fin.verts)
        b.add(rot_u(fin, 90 * k), ACCENT)
    mag = straight_mag(b, 0.075, -0.048, 0.13, 0.022, 0.045, 4)
    b.add(box(0.026, 0.012, 0.006, (0, 0.105, -0.046), 0.0015), METAL)                    # arrêtoir
    sv = vertical_grip(b, 0.25, -0.022)
    gp = grip(b, -0.03, -0.042, 0.022)
    red_dot(b, 0.02, 0.0334)
    # Crosse squelette à charnière.
    b.add(vcyl(0.006, 0.006, -0.03, 0.016, 0, -0.112, 14), METAL)
    st = prism([(-0.112, 0.012), (-0.29, 0.0), (-0.3, 0.004), (-0.3, -0.075), (-0.29, -0.08), (-0.25, -0.08), (-0.112, -0.03)], -0.012, 0.012, 0.003)
    st = carve(st, [prism([(-0.13, -0.002), (-0.27, -0.01), (-0.27, -0.06), (-0.24, -0.062), (-0.13, -0.022)], -0.02, 0.02, 0.004)])
    b.add(st, POLY)
    b.add(box(0.03, 0.01, 0.086, (0, -0.304, -0.038), 0.004), RUBBER)
    g.point("Muzzle", (0, 0.425, 0)); g.point("Grip", gp); g.point("SupportV", sv)
    g.point("Butt", (0, -0.305, -0.035)); g.point("Mag", mag)


def cbomb(g):
    """Charge C : pain d'explosif plastique emballé (ruban adhésif), deux détonateurs et leurs fils, boîtier de
    télécommande (clavier, voyant, écran, interrupteur sous capot rouge, antenne fouet), patins aimantés dessous."""
    b = g.body
    b.add(loft([(-0.06, 0.032, 0.019, 0, 6.0), (-0.056, 0.036, 0.022, 0, 6.0), (0.056, 0.036, 0.022, 0, 6.0), (0.06, 0.032, 0.019, 0, 6.0)], 32), ACCENT)
    for uu in (-0.034, 0.034):
        b.add(loft([(uu - 0.007, 0.0372, 0.0232, 0, 6.0), (uu + 0.007, 0.0372, 0.0232, 0, 6.0)], 32, False), POLY)
    for s in (-1, 1):
        b.add(box(0.0012, 0.026, 0.008, (s * 0.036, 0.0, 0.011), 0.0004, 1), MARKING)       # étiquette
    # Détonateurs plantés dans la face avant et fils.
    for k, x in enumerate((-0.014, 0.014)):
        b.add(tube(0.0035, 0.0035, 0.052, 0.074, x, 0.004, 10), METAL)
        b.add(ring(0.0045, 0.003, 0.07, 0.074, x, 0.004, 10), METAL)
        b.add(sweep(bez((x, 0.074, 0.004), (x, 0.09, 0.004), (x * 0.5, 0.06, 0.045), (x * 0.5, 0.036, 0.034), 12), circ(0.0013, 6)), POLY if k else PAINT)
    # Boîtier de télécommande.
    b.add(prism([(-0.035, 0.021), (0.034, 0.021), (0.034, 0.034), (0.02, 0.046), (-0.035, 0.046)], -0.022, 0.022, 0.002), POLY)
    for i in range(3):
        for j in range(2):
            b.add(box(0.007, 0.006, 0.002, ((j - 0.5) * 0.011, -0.026 + i * 0.009, 0.0468), 0.0008), RUBBER)
    b.add(box(0.026, 0.002, 0.008, (0, 0.0275, 0.04), 0.0005, 1), GLASS)
    b.add(sphere(0.0025, (0.012, 0.004, 0.047), (1, 1, 1), 10, 6), GLOW)
    b.add(vcyl(0.0016, 0.0016, 0.046, 0.054, -0.012, 0.004, 8), METAL)                     # interrupteur
    b.add(box(0.008, 0.012, 0.004, (-0.012, 0.004, 0.056), 0.001), ACCENT)                # capot rouge
    b.add(vcyl(0.004, 0.0035, 0.046, 0.054, 0.016, -0.028, 12), METAL)
    b.add(vcyl(0.0016, 0.0009, 0.054, 0.125, 0.016, -0.028, 8), METAL)
    b.add(sphere(0.0022, (0.016, -0.028, 0.126), (1, 1, 1), 8, 4), METAL)
    for s in (-1, 1):
        screw(b, -0.03, 0.033, s * 0.0222, 0.0018, s)
        screw(b, 0.028, 0.026, s * 0.0222, 0.0018, s)
    for (x, uu) in ((-0.022, -0.04), (0.022, -0.04), (-0.022, 0.04), (0.022, 0.04)):
        b.add(vcyl(0.008, 0.008, -0.026, -0.021, x, uu, 16), RUBBER)
    hand_points(g)


def impulse(g):
    """Mine Impulse : disque rainuré, plateau à huit ports de tir (godets), dôme capteur vitré à voyant, anse de transport
    rabattue, commutateur d'armement, plaques latérales, pieds d'ancrage repliés."""
    b = g.body
    rows = [(-0.022, 0), (-0.022, 0.054), (-0.019, 0.058), (-0.016, 0.06)]
    for k in range(4):
        zz = -0.013 + k * 0.006
        rows += [(zz, 0.06), (zz + 0.001, 0.0575), (zz + 0.003, 0.0575), (zz + 0.004, 0.06)]
    rows += [(0.01, 0.06), (0.014, 0.056), (0.016, 0.052), (0.016, 0)]
    b.add(revsurf(rows, 48, axis='z'), PAINT)
    b.add(revsurf([(0.0155, 0.05), (0.018, 0.05), (0.018, 0.026), (0.0155, 0.022)], 48, axis='z', caps=False), METAL)
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        b.add(cup(math.cos(a) * 0.037, math.sin(a) * 0.037, 0.012, 0.024, 0.0085, 0.0062, 0.006, 16), METAL)
        b.add(vcyl(0.004, 0.004, 0.017, 0.0185, math.cos(a) * 0.037, math.sin(a) * 0.037, 10), GLOW)
    b.add(vcyl(0.02, 0.02, 0.016, 0.022, 0, 0, 28), METAL)
    b.add(revsurf([(0.021, 0.016), (0.026, 0.014), (0.03, 0.009), (0.032, 0)], 24, axis='z'), GLASS)
    b.add(sphere(0.004, (0, 0, 0.027), (1, 1, 1), 10, 6), GLOW)
    # Anse rabattue et charnières.
    path = [(math.cos(a) * 0.054, math.sin(a) * 0.054, 0.019) for a in (math.radians(-70 + 140 * k / 14) for k in range(15))]
    b.add(sweep(path, circ(0.0022, 6)), METAL)
    for a in (-70, 70):
        ar = math.radians(a)
        b.add(box(0.008, 0.008, 0.006, (math.cos(ar) * 0.054, math.sin(ar) * 0.054, 0.0175), 0.0015), METAL)
    # Commutateur d'armement, plaques latérales, pieds.
    b.add(box(0.008, 0.012, 0.012, (0.0, -0.062, 0.0), 0.002), METAL)
    b.add(box(0.005, 0.006, 0.008, (0.0, -0.068, 0.002), 0.001), ACCENT)
    for s in (-1, 1):
        b.add(box(0.002, 0.03, 0.012, (s * 0.0605, 0.0, -0.006), 0.0006, 1), POLY)
    for k in range(3):
        a = math.radians(90 + 120 * k)
        leg = prism([(0.0, -0.0215), (0.03, -0.0215), (0.034, -0.0245), (0.0, -0.0255)], -0.004, 0.004, 0.0006)
        bmesh.ops.translate(leg, vec=P(0, 0.025, 0), verts=leg.verts)
        b.add(rot_z(leg, math.degrees(a) - 90), METAL)
    hand_points(g)


def reverser(g, shooter=False):
    """Pistolet de soin (Reverser) : coque blanche organique à filets de jonction, ouïes lumineuses, croix médicale,
    cartouche de régénération dans un berceau, émetteur à anneaux ; Reverse Shooter : tube de lancement à bobines,
    trois fioles, poignée verticale."""
    b = g.body
    L = 0.05 if shooter else 0.0
    secs = [(-0.13, 0.018, 0.03, -0.01, 2.6), (-0.1, 0.026, 0.045, -0.005, 3.0), (-0.02, 0.03, 0.05, 0.0, 3.2),
            (0.12 + L, 0.03, 0.045, 0.004, 3.2), (0.19 + L, 0.024, 0.034, 0.004, 2.8), (0.215 + L, 0.018, 0.024, 0.004, 2.4)]
    body = loft(secs, 36)
    body = carve(body, [box(0.012, 0.06 + L * 0.6, 0.0045, (s * 0.03, 0.14 + L * 0.3, zz), 0.002) for s in (-1, 1) for zz in (-0.016, -0.006, 0.004)])
    b.add(body, PAINT)
    for s in (-1, 1):
        b.add(box(0.002, 0.075 + L, 0.022, (s * 0.024, 0.135 + L / 2, -0.004), 0.0005, 1), ENERGY)
        b.add(box(0.0015, 0.024, 0.007, (s * 0.0305, 0.04, 0.008), 0.0006, 1), ACCENT)
        b.add(box(0.0015, 0.007, 0.024, (s * 0.0305, 0.04, 0.008), 0.0006, 1), ACCENT)
    # Filets de jonction (bandes d'alliage qui épousent la coque).
    for uu in (-0.055, 0.075):
        hw, hh, zc = 0.0295, 0.0485, -0.002
        b.add(loft([(uu - 0.0025, hw + 0.0012, hh + 0.0012, zc, 3.1), (uu + 0.0025, hw + 0.0012, hh + 0.0012, zc, 3.1)], 36, False), ALLOY)
    # Cartouche de régénération dans son berceau.
    cz = 0.064
    b.add(capsule(-0.04, 0.12, 0.017, 0, cz, 0.012, 24, 4), ENERGY)
    for uu in (-0.035, 0.115):
        b.add(ring(0.021, 0.015, uu - 0.008, uu + 0.008, 0, cz, 24), ALLOY)
        b.add(box(0.014, 0.012, 0.02, (0, uu, 0.046), 0.003), ALLOY)
    for s in (-1, 1):
        b.add(tube(0.0025, 0.0025, -0.035, 0.115, s * 0.019, cz, 8), ALLOY)
    b.add(box(0.006, 0.004, 0.004, (0.022, 0.04, cz), 0.001), GLOW)
    if shooter:
        # Tube de lancement à bobines, fioles, poignée verticale.
        bore_barrel(b, [(0.24, 0.026), (0.5, 0.026), (0.505, 0.03), (0.52, 0.03)], 0.02, 0.06, seg=32, mat=ALLOY)
        for k in range(5):
            uu = 0.29 + 0.04 * k
            b.add(ring(0.031, 0.025, uu, uu + 0.014, 0, 0, 32), ACCENT)
            b.add(ring(0.0285, 0.025, uu + 0.016, uu + 0.022, 0, 0, 32), ENERGY)
        b.add(ring(0.024, 0.02, 0.5, 0.51, 0, 0, 28), ENERGY)
        for k, x in enumerate((-0.022, 0.0, 0.022)):
            b.add(capsule(0.16, 0.24, 0.0095, x, 0.05, 0.007, 16, 3), ENERGY)
            b.add(ring(0.011, 0.008, 0.155, 0.168, x, 0.05, 16), ALLOY)
        b.add(box(0.06, 0.1, 0.008, (0, 0.2, 0.036), 0.003), ALLOY)
        sv = vertical_grip(b, 0.33, -0.026, 0.07, PAINT)
        mz = 0.52
    else:
        b.add(revsurf([(0.21, 0.02), (0.225, 0.02), (0.228, 0.017), (0.236, 0.017), (0.24, 0.022), (0.25, 0.022), (0.252, 0.016), (0.252, 0)], 28, z=0.004), ALLOY)
        b.add(ring(0.0215, 0.016, 0.228, 0.236, 0, 0.004, 28), ENERGY)
        b.add(revsurf([(0.245, 0.012), (0.258, 0.008), (0.262, 0)], 20, z=0.004), ENERGY)
        for k in range(3):
            fin = prism([(0.18, 0.02), (0.235, 0.024), (0.255, 0.032), (0.23, 0.033), (0.19, 0.028)], -0.002, 0.002, 0.0006)
            bmesh.ops.translate(fin, vec=P(0, 0, 0.004), verts=fin.verts)
            b.add(rot_u(fin, 90 + 120 * k, 0, 0.004), ACCENT)
        mz = 0.262
    gp = grip(b, -0.03, -0.04, 0.026)
    # Crosse en boucle fuselée (cadre arrondi) et plaque de couche souple.
    loop = fillet([(-0.11, 0.006), (-0.305, -0.008), (-0.305, -0.072), (-0.2, -0.066), (-0.11, -0.036)], 0.022, 5)
    b.add(sweep([(0, u, z) for (u, z) in loop], rrect(0.026, 0.013, 0.006, 2), closed=True, up=(1, 0, 0)), PAINT)
    b.add(loft([(-0.3, 0.017, 0.036, -0.04, 4.0), (-0.312, 0.018, 0.038, -0.04, 4.0), (-0.322, 0.016, 0.034, -0.04, 3.0)], 24), RUBBER)
    b.add(box(0.004, 0.06, 0.004, (0.0135, -0.2, -0.0035), 0.001), ENERGY)
    g.point("Muzzle", (0, mz, 0.004)); g.point("Grip", gp)
    if shooter:
        g.point("SupportV", sv)
    else:
        g.point("Support", (0, 0.15, -0.045))
    g.point("Butt", (0, -0.32, -0.04)); g.point("Mag", (0, 0.04, cz))


def revshooter(g):
    reverser(g, True)


PALETTE = {
    "mg": ((0.18, 0.21, 0.13), (0.75, 0.6, 0.12)), "dng": ((0.16, 0.18, 0.12), (0.7, 0.12, 0.06)),
    "boundgren": ((0.1, 0.1, 0.1), (0.75, 0.55, 0.08)), "um": ((0.16, 0.18, 0.13), (0.05, 0.05, 0.05)),
    "brute": ((0.14, 0.15, 0.13), (0.3, 0.27, 0.2)), "crumble": ((0.3, 0.27, 0.19), (0.32, 0.18, 0.09)),
    "stampede": ((0.12, 0.13, 0.11), (0.55, 0.42, 0.12)), "splash": ((0.2, 0.22, 0.25), (0.6, 0.4, 0.08)),
    "sticky": ((0.22, 0.2, 0.17), (0.65, 0.5, 0.08)), "napalm": ((0.45, 0.18, 0.06), (0.12, 0.12, 0.11)),
    "geyser": ((0.5, 0.25, 0.05), (0.15, 0.15, 0.14)), "acid": ((0.16, 0.18, 0.14), (0.3, 0.6, 0.1)),
    "flamer": ((0.14, 0.15, 0.13), (0.55, 0.16, 0.06)), "flamecannon": ((0.12, 0.12, 0.12), (0.6, 0.3, 0.05)),
    "firecracker": ((0.55, 0.12, 0.08), (0.75, 0.6, 0.15)), "boundgun": ((0.16, 0.2, 0.26), (0.55, 0.6, 0.65)),
    "cbomb": ((0.2, 0.22, 0.16), (0.55, 0.5, 0.35)), "impulse": ((0.15, 0.17, 0.13), (0.7, 0.12, 0.06)),
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
