# Armes de la Wing Diver (EDF 5), une fonction par famille de modèle. Langage commun : armes à énergie futuristes, coque claire
# profilée (Paint) à joints creusés, pièces internes sombres (Accent / Metal), fentes et fenêtres lumineuses (Energy) creusées
# dans la coque, bobines, condensateurs, isolateurs, émetteurs ciselés. Silhouettes très différentes d'une famille à l'autre :
# Courte portée : Rapier = griffe d'arc à trois branches, Phalanx = éventail de six lames émettrices, Power Lance = longue
#   lance fine à anneaux accélérateurs, Dragoon Lance = lance lourde hexagonale à ailerons, Monster Lance = lance à cadre
#   ouvert et tambours de condensateurs, Spark Vine = tige isolée à bobine, Spark Whip = bobine-enrouleur et trois fouets.
# Blasters : Mag Blaster = anneaux magnétiques focalisants, Destroyer = capot large à six alésages et radiateurs, Spine =
#   colonne de vertèbres autour d'un noyau, Reflectron = cage à prisme et miroirs.
# Électriques : Lightning Bow = arc vertical à branches et corde d'énergie, Bolt Gun = double électrode à isolateurs,
#   Thunder Crossbow = arbalète à bras horizontaux et carreau d'énergie.
# Impulsions : Pulse MG = tambour de chambre et manchon ventilé, Stardust = canon à grappe hexagonal, Pulse Buster = quatre
#   canons tenus par des brides.
# Longue portée : Closed Laser = long canon à ailettes, MONSTER = fusil lourd à bancs de condensateurs, Raijin = double rail
#   géant, Bolt Shooter = longues électrodes parallèles.
# Artillerie plasma : gros alésages, noyau visible par des fenêtres dorsales ; tir plongeant (Plasmafall), double canon
#   (Burst), canon blindé (Heavy), radiateur (Grit), bouche plate (Splasher). Blink Ball = grenade sphérique.
# Guidées : réseau d'alésages (Mirage), nacelles (Geist), barillet (Ghost Chaser), double tube à mâchoires (Fenrir), lame
#   (Psiblade). Lanceurs de sphères : gantelet à berceau, sphère d'énergie tenue par griffes, anneaux, électrodes ou halo.
# Repère de travail du kit : P(x, u, z), u vers la bouche, x à droite, z en haut, mètres ; origine sur l'axe du canon.
import math


# ───────────────────────── Outils propres au module ─────────────────────────

def S(u, hw, hh, z=0.0, p=3.0):
    """Section de coque : (u, demi-largeur, demi-hauteur, z du centre, exposant superellipse)."""
    return (u, hw, hh, z, p)


def M(c=(0.0, 0.0, 0.0), pitch=0.0, yaw=0.0, roll=0.0):
    """Matrice : rotation (pitch > 0 : bouche vers le haut ; roll autour de l'axe u) puis translation en c (repère de travail)."""
    return (Matrix.Translation(P(*c)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
            @ Matrix.Rotation(math.radians(-pitch), 4, 'X') @ Matrix.Rotation(math.radians(roll), 4, 'Y'))


def xf(bm, mtx):
    bmesh.ops.transform(bm, matrix=mtx, verts=bm.verts)
    return bm


def hprism(profile, z0, z1, bevel=0.001):
    """Prisme horizontal : profile = [(u, x)] (polygone vu de dessus), extrudé de z0 à z1."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x, u, z0)) for (u, x) in profile]
    c = [bm.verts.new(P(x, u, z1)) for (u, x) in profile]
    n = len(profile)
    bm.faces.new(a)
    bm.faces.new(list(reversed(c)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], c[j], c[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5)
    return bm


def bez(p0, p1, p2, p3, n=12):
    """Courbe de Bézier cubique (points de dimension quelconque)."""
    out = []
    for k in range(n + 1):
        t = k / n
        a, b_, c, d = (1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3
        out.append(tuple(a * p0[i] + b_ * p1[i] + c * p2[i] + d * p3[i] for i in range(len(p0))))
    return out


def sweep(path, r, seg=10, r1=None, caps=True, closed=False):
    """Tube le long d'une polyligne path = [(x, u, z)] (câbles, branches, bobines) ; rayon r → r1 ; repères transportés."""
    bm = bmesh.new()
    pts = [P(*p) for p in path]
    n = len(pts)
    tans = []
    for i in range(n):
        if closed:
            a, c = pts[(i - 1) % n], pts[(i + 1) % n]
        else:
            a, c = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        tans.append((c - a).normalized())
    t0 = tans[0]
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    nrm = t0.cross(ref).normalized()
    loops = []
    for i in range(n):
        t = tans[i]
        nrm = (nrm - t * nrm.dot(t)).normalized()
        bi = t.cross(nrm)
        rr = r if r1 is None else r + (r1 - r) * i / max(1, n - 1)
        loops.append([bm.verts.new(pts[i] + (nrm * math.cos(a) + bi * math.sin(a)) * rr)
                      for a in (2 * math.pi * k / seg for k in range(seg))])
    for i in range(n if closed else n - 1):
        A, B = loops[i], loops[(i + 1) % n]
        for k in range(seg):
            bm.faces.new((A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k]))
    if caps and not closed:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def torus_u(u, R, r, x=0.0, z=0.0, seg=28, rseg=8):
    """Tore autour de l'axe u (spire de bobine, anneau)."""
    return sweep([(x + R * math.cos(a), u, z + R * math.sin(a)) for a in (2 * math.pi * k / seg for k in range(seg))], r, rseg, closed=True)


def helix(u0, u1, R, r, turns, x=0.0, z=0.0, rseg=6, per=14):
    n = max(8, int(turns * per))
    return sweep([(x + R * math.cos(2 * math.pi * turns * k / n), u0 + (u1 - u0) * k / n, z + R * math.sin(2 * math.pi * turns * k / n))
                  for k in range(n + 1)], r, rseg)


def ball(r, c, us=20, vs=12):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=us, v_segments=vs, radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def sphere_ball(r, c, mat=None):
    return ball(r, c, 16, 10)


def band(path, w0, w1=None):
    """Polygone (u, z) d'une bande d'épaisseur variable (w0 → w1) le long d'un chemin 2D (branches d'arc, lames)."""
    w1 = w0 if w1 is None else w1
    n = len(path)
    L, R = [], []
    for i, (u, z) in enumerate(path):
        a, c = path[max(i - 1, 0)], path[min(i + 1, n - 1)]
        du, dz = c[0] - a[0], c[1] - a[1]
        d = math.hypot(du, dz) or 1.0
        nu, nz = -dz / d, du / d
        w = (w0 + (w1 - w0) * i / max(1, n - 1)) / 2
        L.append((u + nu * w, z + nz * w))
        R.append((u - nu * w, z - nz * w))
    return L + R[::-1]


def sring(u, hw, hh, zc, p, depth, w, seg=28):
    """Forme de découpe : anneau superellipse (rainure de joint autour d'une coque de section (hw, hh, p))."""
    bm = bmesh.new()
    vs = []
    for (uu, d) in ((u - w / 2, 0.012), (u + w / 2, 0.012), (u + w / 2, -depth), (u - w / 2, -depth)):
        loop = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ca, sa = math.cos(a), math.sin(a)
            loop.append(bm.verts.new(P((hw + d) * math.copysign(abs(ca) ** (2 / p), ca), uu,
                                       zc + (hh + d) * math.copysign(abs(sa) ** (2 / p), sa))))
        vs.append(loop)
    for k in range(4):
        a, c = vs[k], vs[(k + 1) % 4]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], c[j], c[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def cut(bm, cutters):
    """Découpe booléenne robuste : toutes les formes réunies en un seul objet (une seule différence, auto-intersections
    tolérées), puis contrôle ; en cas d'échec (coque vidée), on retombe sur les découpes une à une, en gardant celles qui
    réussissent. Renvoie la pièce découpée (bmesh)."""
    n0 = len(bm.faces)
    ref = bm.copy()
    allc = bmesh.new()
    me = bpy.data.meshes.new("cj")
    for c in cutters:
        c.to_mesh(me)
        allc.from_mesh(me)
    bpy.data.meshes.remove(me)
    out = _bool(bm, allc)
    if _sane(out, ref):
        ref.free()
        for c in cutters:
            c.free()
        return out
    out.free()
    cur = ref
    for c in cutters:
        t = _bool(cur.copy(), c)
        if _sane(t, cur):
            cur.free()
            cur = t
        else:
            t.free()
    return cur


def _bool(bm, cutter):
    me = bpy.data.meshes.new("cv")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("cv", me)
    bpy.context.collection.objects.link(ob)
    cm = bpy.data.meshes.new("cc")
    cutter.to_mesh(cm)
    cutter.free()
    co = bpy.data.objects.new("cc", cm)
    bpy.context.collection.objects.link(co)
    md = ob.modifiers.new("cut", 'BOOLEAN')
    md.operation = 'DIFFERENCE'
    md.solver = 'EXACT'
    md.object = co
    md.use_self = True
    md.use_hole_tolerant = True
    dg = bpy.context.evaluated_depsgraph_get()
    out = bmesh.new()
    tmp = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    out.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)
    bpy.data.objects.remove(co)
    bpy.data.meshes.remove(cm)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return out


def _sane(out, ref):
    """La découpe n'a pas détruit la pièce : même étendue et assez de faces."""
    if len(out.faces) < len(ref.faces) * 0.8 or not out.verts:
        return False
    for i in range(3):
        a = [v.co[i] for v in out.verts]
        b_ = [v.co[i] for v in ref.verts]
        if abs(min(a) - min(b_)) > 0.002 or abs(max(a) - max(b_)) > 0.002:
            return False
    return True


class Hull:
    """Coque profilée (loft superellipse) qu'on cisèle avant de l'ajouter : rainures de joints, lignes de panneaux, fentes
    et fenêtres creusées avec un fond (plaque cachée dans la coque, visible seulement par les découpes : Energy = fentes
    lumineuses), trous traversants. mtx : placement final (décalage latéral, inclinaison)."""

    def __init__(self, secs, mat=PAINT, seg=28, mtx=None):
        self.secs = sorted(secs)
        self.mat, self.seg, self.mtx = mat, seg, mtx
        self.cuts, self.ins = [], []

    def at(self, u):
        s = self.secs
        if u <= s[0][0]:
            return s[0][1:]
        for a, c in zip(s, s[1:]):
            if u <= c[0]:
                t = (u - a[0]) / max(c[0] - a[0], 1e-9)
                return tuple(a[i] + (c[i] - a[i]) * t for i in range(1, 5))
        return s[-1][1:]

    def sx(self, u, z):
        hw, hh, zc, p = self.at(u)
        t = min(abs(z - zc) / hh, 0.985)
        return hw * (1 - t ** p) ** (1 / p)

    def sz(self, u, x=0.0, top=True):
        hw, hh, zc, p = self.at(u)
        t = min(abs(x) / hw, 0.985)
        return zc + (1 if top else -1) * hh * (1 - t ** p) ** (1 / p)

    def groove(self, u, depth=0.0018, w=0.003):
        hw, hh, zc, p = self.at(u)
        self.cuts.append(sring(u, hw, hh, zc, p, depth, w, self.seg))

    def seam(self, u0, u1, z, depth=0.0014, w=0.0024, sides=(-1, 1), n=8):
        """Ligne de panneau creusée sur les flancs, à hauteur z, qui suit la coque."""
        us = [u0 + (u1 - u0) * k / n for k in range(n + 1)]
        for s in sides:
            out = [(u, s * (self.sx(u, z) + 0.004)) for u in us]
            inn = [(u, s * (self.sx(u, z) - depth)) for u in reversed(us)]
            self.cuts.append(hprism(out + inn, z - w / 2, z + w / 2, 0))

    def tseam(self, u0, u1, x=0.0, depth=0.0014, w=0.0024, n=8, top=True):
        """Ligne de panneau creusée sur le dessus (ou le dessous)."""
        sg = 1 if top else -1
        us = [u0 + (u1 - u0) * k / n for k in range(n + 1)]
        out = [(u, self.sz(u, x, top) + sg * 0.004) for u in us]
        inn = [(u, self.sz(u, x, top) - sg * depth) for u in reversed(us)]
        self.cuts.append(prism(out + inn, x - w / 2, x + w / 2, 0))

    def _xmin(self, u, z, h):
        return min(self.sx(u, z - h / 2), self.sx(u, z + h / 2), self.sx(u, z))

    def _side(self, uu, z, w, h, depth, s, r):
        xo = self.sx(uu, z) + 0.01
        xi = min(self._xmin(uu - w / 2, z, h), self._xmin(uu + w / 2, z, h)) - depth
        r = min(w, h) * 0.45 if r is None else r
        self.cuts.append(box(xo - xi, w, h, (s * (xo + xi) / 2, uu, z), r, 3))

    def _fill(self, u0, u1, z, h, depth, s, mat, n=8):
        us = [u0 + (u1 - u0) * k / n for k in range(n + 1)]
        xm = [self._xmin(u, z, h * 1.25) for u in us]
        out = [(u, s * (x - depth * 0.45)) for u, x in zip(us, xm)]
        inn = [(u, s * (x - depth * 0.95)) for u, x in reversed(list(zip(us, xm)))]
        self.ins.append((hprism(out + inn, z - h * 0.62, z + h * 0.62, 0), mat))

    def slots(self, u0, u1, n, z, h, w=None, depth=0.003, fill=POLY, sides=(-1, 1), r=None):
        """n fentes sur les flancs, de u0 à u1, à hauteur z (hauteur h) ; fond de la couleur fill (None : creux simple)."""
        step = (u1 - u0) / n
        w = w or step * 0.55
        for s in sides:
            for k in range(n):
                self._side(u0 + step * (k + 0.5), z, w, h, depth, s, r)
            if fill is not None:
                self._fill(u0, u1, z, h, depth, s, fill)

    def window(self, u0, u1, z0, z1, depth=0.003, fill=ENERGY, sides=(-1, 1), r=None):
        """Fenêtre (ou panneau en creux) rectangulaire arrondie sur les flancs."""
        for s in sides:
            self._side((u0 + u1) / 2, (z0 + z1) / 2, u1 - u0, z1 - z0, depth, s, r if r is not None else min(u1 - u0, z1 - z0) * 0.3)
            if fill is not None:
                self._fill(u0, u1, (z0 + z1) / 2, z1 - z0, depth, s, fill)

    def tslots(self, u0, u1, n, x, wx, w=None, depth=0.003, fill=POLY, top=True, r=None):
        """n fentes transversales sur le dessus (ou le dessous), centrées en x, larges de wx."""
        sg = 1 if top else -1
        step = (u1 - u0) / n
        w = w or step * 0.55
        zm = lambda u: min(self.sz(u, x - wx / 2, top), self.sz(u, x + wx / 2, top), self.sz(u, x, top), key=lambda v: sg * v)
        for k in range(n):
            uu = u0 + step * (k + 0.5)
            zo = self.sz(uu, x, top) + sg * 0.01
            zi = min(zm(uu - w / 2), zm(uu + w / 2), key=lambda v: sg * v) - sg * depth
            rr = min(w, wx) * 0.45 if r is None else r
            self.cuts.append(box(wx, w, abs(zo - zi), (x, uu, (zo + zi) / 2), rr, 3))
        if fill is not None:
            us = [u0 + (u1 - u0) * k / 8 for k in range(9)]
            zz = [min(self.sz(u, x - wx * 0.62, top), self.sz(u, x + wx * 0.62, top), key=lambda v: sg * v) for u in us]
            out = [(u, v - sg * depth * 0.45) for u, v in zip(us, zz)]
            inn = [(u, v - sg * depth * 0.95) for u, v in reversed(list(zip(us, zz)))]
            self.ins.append((prism(out + inn, x - wx * 0.62, x + wx * 0.62, 0), fill))

    def hole(self, u, z, lu, lz, r=None):
        """Trou traversant (crosse squelette, poignée de transport découpée)."""
        self.cuts.append(box(0.4, lu, lz, (0, u, z), min(lu, lz) * 0.45 if r is None else r, 4))

    def cut(self, bm, fill=None):
        """Découpe libre (forme fournie), avec éventuellement une pièce de fond."""
        self.cuts.append(bm)
        if fill is not None:
            self.ins.append(fill)

    def build(self, b):
        bm = loft(self.secs, self.seg)
        if self.cuts:
            bm = cut(bm, self.cuts)
        b.add(bm, self.mat, self.mtx.copy() if self.mtx is not None else None)
        for bm2, m in self.ins:
            b.add(bm2, m, self.mtx.copy() if self.mtx is not None else None)
        return self


def plate(b, h, u0, u1, z0, z1, mat=ACCENT, t=0.0022, screws=True, sides=(-1, 1)):
    """Plaque rapportée sur les flancs d'une coque (suit sa courbure le long de u), vissée aux quatre coins."""
    n = 6
    us = [u0 + (u1 - u0) * k / n for k in range(n + 1)]
    xo = [max(h.sx(u, z0), h.sx(u, z1), h.sx(u, (z0 + z1) / 2)) + t for u in us]
    xi = [min(h.sx(u, z0), h.sx(u, z1)) - 0.003 for u in us]
    for s in sides:
        prof = [(u, s * x) for u, x in zip(us, xo)] + [(u, s * x) for u, x in reversed(list(zip(us, xi)))]
        b.add(hprism(prof, z0, z1, 0.0008), mat)
        if screws:
            for k in (0, n):
                uu = us[k] + (0.006 if k == 0 else -0.006)
                for zz in (z0 + 0.0045, z1 - 0.0045):
                    screw(b, uu, zz, s * xo[k], 0.0016, s)


def strip(b, u0, u1, z, x=0.0, w=0.004, h=0.006):
    b.add(box(w, u1 - u0, h, (x, (u0 + u1) / 2, z), 0.001, 1), ENERGY)


def holed_ring(b, u0, u1, ro, ri, n, hr, mat=ACCENT, x=0.0, z=0.0, seg=28):
    """Bague percée de trous radiaux (accélérateur, frein d'émetteur)."""
    bm = ring(ro, ri, u0, u1, x, z, seg)
    bm = cut(bm, holes_ring((u0 + u1) / 2, (ro + ri) / 2, n, hr, (ro - ri) * 2.2, z, x))
    b.add(bm, mat)


def accel(b, u, ro, ri, w=0.018, mat=ACCENT, n=8):
    """Anneau accélérateur : deux demi-bagues percées et une bande lumineuse entre elles."""
    holed_ring(b, u, u + w * 0.4, ro, ri, n, min(w * 0.12, (ro - ri) * 0.3), mat)
    holed_ring(b, u + w * 0.6, u + w, ro, ri, n, min(w * 0.12, (ro - ri) * 0.3), mat)
    b.add(ring(ro * 0.93, ri, u + w * 0.4, u + w * 0.6, 0, 0, 28), ENERGY)


def crown(b, u, r, n, l, mat=PAINT, t=0.004, roll0=0.0):
    """Couronne d'émetteur : bague et n griffes tournées vers l'avant, bande lumineuse intérieure."""
    b.add(ring(r * 1.1, r * 0.72, u, u + l * 0.3, 0, 0, 28), ACCENT)
    b.add(ring(r * 0.8, r * 0.6, u + l * 0.3, u + l * 0.36, 0, 0, 28), ENERGY)
    prof = [(u + l * 0.1, r * 0.8), (u + l * 0.35, r * 1.18), (u + l, r * 0.98), (u + l * 0.92, r * 0.78), (u + l * 0.4, r * 0.76)]
    for k in range(n):
        b.add(prism(prof, -t, t, 0.0008), mat, M(roll=roll0 + 360 * k / n))


def wd_grip(b, u=-0.03, z=-0.04, angle=14, h=0.095, mat=POLY):
    """Poignée pistolet, détente, sélecteur, talon de poignée ; renvoie le point de la main."""
    gp = pistol_grip(b, u, z, mat, angle, h, 0.03, 0.044)
    b.add(prism([(u + 0.03, z + 0.004), (u + 0.04, z + 0.004), (u + 0.034, z - 0.012), (u + 0.031, z - 0.03), (u + 0.026, z - 0.03),
                 (u + 0.029, z - 0.012)], -0.003, 0.003, 0.0008), METAL)
    a = math.radians(angle)
    ub, zb = u - math.sin(a) * h, z - math.cos(a) * h
    b.add(box(0.034, 0.05, 0.008, (0, ub, zb - 0.002), 0.003), ACCENT)                        # talon
    for k in range(4):                                                                       # stries de prise avant
        t = 0.25 + k * 0.15
        b.add(box(0.026, 0.004, 0.006, (0, u - math.sin(a) * h * t + 0.021, z - math.cos(a) * h * t), 0.001, 1), RUBBER)
    b.add(tube_x(0.005, 0.008, (0.017, u + 0.005, z + 0.012), 12), METAL)                     # sélecteur
    b.add(box(0.003, 0.016, 0.004, (0.021, u + 0.005, z + 0.018), 0.0008, 1), METAL)
    return gp


def wd_vgrip(b, u, z, h=0.085, rake=8.0, mat=POLY):
    """Poignée verticale à creux de doigts ; renvoie le point de la main gauche."""
    secs = [(0.0, 0.017, 0.02, 0, 3.0), (0.008, 0.0165, 0.019, 0, 3.0), (0.022, 0.0145, 0.0165, 0, 2.6), (0.035, 0.016, 0.019, 0, 2.6),
            (0.048, 0.0145, 0.0165, 0, 2.6), (0.061, 0.016, 0.019, 0, 2.6), (h - 0.012, 0.0155, 0.018, 0, 2.6), (h, 0.017, 0.02, 0, 3.2)]
    rot = Matrix.Translation(P(0, u, z)) @ Matrix.Rotation(math.radians(-rake), 4, 'X') @ Matrix.Rotation(math.radians(90), 4, 'X')
    b.add(loft(secs, 18), mat, rot)
    b.add(loft([(h, 0.019, 0.022, 0, 3.4), (h + 0.008, 0.019, 0.022, 0, 3.4)], 18), ACCENT, rot.copy())
    b.add(box(0.03, 0.05, 0.01, (0, u, z + 0.002), 0.003), ACCENT)                           # embase
    b.add(tube_x(0.0035, 0.034, (0, u - 0.014, z + 0.002), 10), METAL)
    return (0, u + math.sin(math.radians(rake)) * h * 0.5, z - h * 0.5)


def wd_stock(b, u0=-0.14, u1=-0.33, z=-0.012, h=0.07, w=0.019, drop=0.014, kind="skel", mat=PAINT):
    """Crosse : 'skel' (squelette évidée), 'full' (pleine à appui-joue), 'rods' (deux tiges + plaque), 'stub' (courte)."""
    z1 = z - drop
    if kind == "rods":
        for zz in (z + 0.01, z - 0.018):
            b.add(tube(0.0055, 0.0055, u1 + 0.04, u0 + 0.01, 0, zz, 12), METAL)
            b.add(ring(0.0085, 0.005, u0 - 0.004, u0 + 0.006, 0, zz, 12), ACCENT)
        u0, z = u1 + 0.07, z1 + 0.004
    mid = (u0 + u1) / 2
    hl = Hull([S(u0, w * 0.8, h * 0.32, z, 2.8), S(u0 - 0.025, w, h * 0.4, z - drop * 0.15, 3.2),
               S(mid, w * 1.02, h * 0.47, (z + z1) / 2 - 0.003, 3.4), S(u1 + 0.014, w * 1.06, h * 0.5, z1, 3.6),
               S(u1 + 0.004, w * 1.0, h * 0.48, z1, 3.0)], mat, 24)
    zm = (z + z1) / 2 - 0.003
    if kind == "skel" and u0 - u1 > 0.12:
        hl.hole(mid + 0.01, zm - h * 0.06, (u0 - u1) * 0.42, h * 0.34)
        hl.window(u1 + 0.035, u0 - 0.045, zm + h * 0.27, zm + h * 0.33, 0.0025, ENERGY)
    elif kind == "full" and u0 - u1 > 0.12:
        hl.window(u1 + 0.05, u0 - 0.05, zm - h * 0.12, zm + h * 0.1, 0.002, POLY)
        hl.seam(u1 + 0.03, u0 - 0.03, zm + h * 0.27)
    else:
        hl.seam(u1 + 0.02, u0 - 0.01, zm + h * 0.2)
    hl.groove(u1 + 0.022)
    if u0 - u1 > 0.09:
        hl.groove(u0 - 0.03)
    hl.build(b)
    if kind == "full" and u0 - u1 > 0.12:                                                   # appui-joue
        b.add(loft([S(u1 + 0.04, w * 0.7, 0.006, z1 + h * 0.48, 3), S(u1 + 0.05, w * 0.8, 0.01, z1 + h * 0.49, 3),
                    S(mid + 0.03, w * 0.8, 0.01, zm + h * 0.47, 3), S(mid + 0.05, w * 0.6, 0.006, zm + h * 0.46, 3)], 18), ACCENT)
    b.add(loft([S(u1 + 0.006, w * 0.98, h * 0.47, z1, 3.0), S(u1 - 0.006, w * 1.03, h * 0.5, z1, 3.6), S(u1 - 0.012, w * 0.94, h * 0.45, z1, 3.0)], 22), RUBBER)
    for k in range(4):
        b.add(box(w * 2.1, 0.003, 0.003, (0, u1 - 0.006, z1 - h * 0.3 + k * h * 0.2), 0.0005, 1), POLY)
    return (0, u1 - 0.008, z1)


def cell_pack(b, u, z, l=0.1, r=0.016, mat=ACCENT, n=3, x=0.0):
    """Cellule d'énergie (le « chargeur ») : boîtier à fenêtres lumineuses, bagues, verrou."""
    h = Hull([S(u - l / 2, r * 0.75, r * 0.75, z, 2.6), S(u - l / 2 + 0.006, r * 1.12, r * 1.08, z, 3.2),
              S(u + l / 2 - 0.006, r * 1.12, r * 1.08, z, 3.2), S(u + l / 2, r * 0.8, r * 0.78, z, 2.6)], mat, 22,
             Matrix.Translation(P(x, 0, 0)) if x else None)
    h.slots(u - l / 2 + 0.014, u + l / 2 - 0.014, n, z - r * 0.1, r * 0.95, depth=0.004, fill=ENERGY)
    h.groove(u - l / 2 + 0.009)
    h.groove(u + l / 2 - 0.009)
    h.tseam(u - l / 2 + 0.012, u + l / 2 - 0.012, 0, top=False)
    h.build(b)
    b.add(box(0.008, 0.012, 0.006, (x, u + l / 2 - 0.02, z - r * 1.1), 0.002), METAL)       # verrou
    return (x, u, z)


def reflex(b, u, z):
    """Viseur réflex fin : embase, capot à montants, vitre, diode."""
    b.add(hprism([(u - 0.026, -0.011), (u + 0.02, -0.011), (u + 0.028, -0.006), (u + 0.028, 0.006), (u + 0.02, 0.011), (u - 0.026, 0.011)],
                 z, z + 0.007), METAL)
    for s in (-1, 1):
        b.add(prism([(u - 0.004, z + 0.006), (u + 0.026, z + 0.006), (u + 0.022, z + 0.034), (u + 0.006, z + 0.032)], s * 0.009, s * 0.013, 0.0008), PAINT)
    b.add(box(0.026, 0.022, 0.004, (0, u + 0.014, z + 0.034), 0.0012), ACCENT)
    b.add(box(0.018, 0.002, 0.024, (0, u + 0.016, z + 0.02), 0.0005, 1), GLASS)
    b.add(tube(0.0025, 0.0025, u - 0.012, u - 0.006, 0, z + 0.011, 8), GLOW)
    b.add(tube_x(0.004, 0.006, (0.012, u - 0.014, z + 0.012), 10), METAL)


def wd_sight(b, u, z, l=0.22, r=0.014, base=0.05):
    """Lunette d'énergie : corps à facettes douces, cloches, bande lumineuse, tourelles, deux montures sur la coque."""
    h = Hull([S(u - l / 2, r * 1.25, r * 1.2, z, 2.4), S(u - l / 2 + 0.014, r * 1.45, r * 1.4, z, 2.8), S(u - l / 2 + 0.05, r, r * 1.05, z, 2.8),
              S(u + l / 2 - 0.07, r, r * 1.05, z, 2.8), S(u + l / 2 - 0.03, r * 1.6, r * 1.5, z, 2.8), S(u + l / 2, r * 1.62, r * 1.52, z, 2.6)], ACCENT, 24)
    h.groove(u - l / 2 + 0.03, 0.0012, 0.0022)
    h.groove(u + l / 2 - 0.014, 0.0012, 0.0022)
    h.window(u - l * 0.18, u + l * 0.12, z - 0.0025, z + 0.0025, 0.002, ENERGY)
    h.build(b)
    b.add(tube(r * 1.4, r * 1.4, u + l / 2 - 0.004, u + l / 2 + 0.001, 0, z, 24), LENS)
    b.add(tube(r * 1.15, r * 1.15, u - l / 2 - 0.001, u - l / 2 + 0.003, 0, z, 20), GLASS)
    b.add(loft([S(u - l / 2 + 0.002, r * 1.3, r * 1.25, z, 2.2), S(u - l / 2 - 0.018, r * 1.35, r * 1.3, z, 2.2)], 20, False), RUBBER)
    b.add(tube(r * 0.55, r * 0.5, u - 0.02, u + 0.014, 0, z + r * 1.05 + 0.006, 14), METAL)    # tourelles
    b.add(tube_x(r * 0.55, 0.026, (r + 0.012, u - 0.003, z), 14), METAL)
    for uu in (u - l * 0.22, u + l * 0.18):
        b.add(prism([(uu - 0.014, base), (uu + 0.014, base), (uu + 0.008, z - r * 0.6), (uu - 0.008, z - r * 0.6)], -0.007, 0.007, 0.0015), METAL)
        b.add(ring(r * 1.2, r * 0.95, uu - 0.007, uu + 0.007, 0, z, 22), METAL)
        screw(b, uu, base + 0.006, 0.007, 0.0022)


def finish_pts(g, muzzle, grip, butt, sup, mag, vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


def receiver_std(b, u0=-0.15, u1=0.2, hw=0.031, hh=0.046, zc=-0.004, p=3.3, mat=PAINT, seg=28):
    """Boîtier standard (arrière arrondi, avant effilé) ; renvoie la coque non construite pour la ciseler."""
    return Hull([S(u0, hw * 0.6, hh * 0.58, zc - 0.002, 2.4), S(u0 + 0.018, hw * 0.92, hh * 0.88, zc, p - 0.3),
                 S((u0 + u1) * 0.45, hw, hh, zc, p), S(u1 - 0.05, hw * 0.94, hh * 0.86, zc + 0.003, p - 0.2),
                 S(u1, hw * 0.7, hh * 0.58, 0.0, p - 0.6)], mat, seg)


# ───────────────────────── Courte portée ─────────────────────────

def rapier(g):
    """Rapier : arme d'arc de plasma compacte ; griffe émettrice à trois branches autour d'une aiguille, bobines."""
    b = g.body
    h = Hull([S(-0.16, 0.017, 0.024, -0.006, 2.4), S(-0.145, 0.028, 0.04, -0.006, 3.0), S(-0.02, 0.033, 0.05, -0.006, 3.4),
              S(0.1, 0.031, 0.044, -0.002, 3.2), S(0.17, 0.026, 0.032, 0.0, 2.8), S(0.2, 0.021, 0.024, 0.0, 2.4)])
    for u in (-0.115, 0.055, 0.15):
        h.groove(u)
    h.seam(-0.105, 0.05, 0.016)
    h.seam(-0.105, 0.05, -0.03)
    h.slots(0.065, 0.145, 4, -0.012, 0.016, depth=0.004, fill=ENERGY)
    h.tslots(-0.09, 0.03, 5, 0.0, 0.024, depth=0.003, fill=POLY)
    h.window(-0.1, -0.06, -0.022, 0.008, 0.002, None)
    h.build(b)
    # Arête dorsale à cran de visée.
    b.add(prism([(-0.13, 0.03), (-0.11, 0.054), (-0.07, 0.056), (-0.066, 0.05), (-0.05, 0.05), (0.06, 0.05), (0.11, 0.04), (0.11, 0.03)],
                -0.005, 0.005, 0.0012), ACCENT)
    for s in (-1, 1):
        screw(b, -0.03, -0.024, s * h.sx(-0.03, -0.024), 0.0025, s)
        screw(b, 0.03, -0.024, s * h.sx(0.03, -0.024), 0.0025, s)
    # Col d'émetteur : bagues, bobines, plateau percé.
    b.add(tube(0.022, 0.025, 0.19, 0.236, 0, 0, 22), ACCENT)
    for k in range(4):
        b.add(torus_u(0.2 + k * 0.01, 0.026, 0.0038, seg=24, rseg=6), ALLOY)
    holed_ring(b, 0.236, 0.25, 0.032, 0.008, 9, 0.0035, PAINT)
    b.add(tube(0.018, 0.018, 0.249, 0.252, 0, 0, 20), ENERGY)
    # Aiguille centrale.
    b.add(tube(0.006, 0.003, 0.25, 0.36, 0, 0, 10), METAL)
    b.add(ball(0.006, (0, 0.362, 0), 10, 6), ENERGY)
    for t in (0.28, 0.31):
        b.add(tube(0.009, 0.009, t, t + 0.006, 0, 0, 12), ACCENT)
    # Trois branches en griffe (pied, branche effilée, arête lumineuse, pointe).
    path = bez((0, 0.245, 0.024), (0, 0.3, 0.07), (0, 0.38, 0.064), (0, 0.43, 0.022), 12)
    glow = bez((0, 0.3, 0.052), (0, 0.34, 0.06), (0, 0.39, 0.052), (0, 0.425, 0.022), 8)
    for k in range(3):
        m = M(roll=90 + k * 120)
        b.add(sweep(path, 0.0085, 10, 0.0035), PAINT, m.copy())
        b.add(sweep(glow, 0.0032, 8, 0.002), ENERGY, m.copy())
        b.add(box(0.014, 0.024, 0.012, (0, 0.248, 0.028), 0.003), ACCENT, m.copy())
        b.add(ball(0.0045, (0, 0.432, 0.02), 10, 6), ENERGY, m.copy())
    mag = cell_pack(b, 0.105, -0.062, 0.09, 0.017)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.27, -0.012, 0.064, 0.018, 0.01, "stub")
    finish_pts(g, (0, 0.44, 0), grip, butt, (0, 0.12, -0.085), mag)


def phalanx(g):
    """Phalanx : rafale chargée ; moyeu large et éventail de six lames émettrices, condensateurs latéraux."""
    b = g.body
    h = Hull([S(-0.17, 0.02, 0.026, -0.006, 2.4), S(-0.15, 0.034, 0.04, -0.006, 3.2), S(0.0, 0.042, 0.046, -0.004, 3.8),
              S(0.14, 0.04, 0.04, 0.0, 3.6), S(0.2, 0.034, 0.028, 0.0, 3.0)])
    for u in (-0.12, -0.02, 0.1):
        h.groove(u)
    h.seam(-0.11, 0.17, 0.016)
    h.tslots(-0.09, 0.07, 6, 0.0, 0.03, depth=0.003, fill=ENERGY)        # jauge de charge
    h.slots(0.11, 0.18, 3, -0.018, 0.012, fill=POLY)
    h.build(b)
    plate(b, h, -0.13, -0.06, -0.034, -0.008)
    hub = Hull([S(0.19, 0.03, 0.024, 0, 3), S(0.215, 0.05, 0.026, 0, 3.4), S(0.26, 0.066, 0.022, 0, 4.0), S(0.275, 0.066, 0.018, 0, 4.0)], ACCENT, 24)
    hub.groove(0.235)
    hub.slots(0.2, 0.26, 3, 0.0, 0.012, fill=ENERGY)
    hub.build(b)
    blade = [(0.0, -0.007), (0.1, -0.0055), (0.15, -0.002), (0.158, 0.0), (0.15, 0.002), (0.1, 0.0055), (0.0, 0.007)]
    for k in range(6):
        m = M((0, 0.255, 0), yaw=-30 + k * 12)
        b.add(hprism(blade, -0.0045, 0.0045), PAINT, m.copy())
        b.add(box(0.003, 0.06, 0.0025, (0, 0.11, 0.0045), 0.0006, 1), ENERGY, m.copy())
        b.add(box(0.003, 0.06, 0.0025, (0, 0.11, -0.0045), 0.0006, 1), ENERGY, m.copy())
        b.add(ball(0.0042, (0, 0.158, 0), 10, 6), ENERGY, m.copy())
    for sg in (-1, 1):                                                   # bec supérieur / inférieur
        z0 = sg * 0.012
        b.add(hprism([(0.25, -0.066), (0.31, -0.086), (0.33, -0.07), (0.335, 0.0), (0.33, 0.07), (0.31, 0.086), (0.25, 0.066)],
                     min(z0, z0 + sg * 0.006), max(z0, z0 + sg * 0.006), 0.0012), PAINT)
        for x in (-0.05, 0.0, 0.05):
            b.add(tube(0.0035, 0.0035, 0.0, 0.004, 0, 0, 10), METAL, M((x, 0.3, z0 + sg * 0.006), pitch=90 * sg))
    # Condensateurs latéraux et leurs brides.
    for s in (-1, 1):
        x = s * 0.054
        c = Hull([S(-0.07, 0.011, 0.011, -0.01, 2.2), S(-0.062, 0.016, 0.016, -0.01, 2.2), S(0.13, 0.016, 0.016, -0.01, 2.2),
                  S(0.138, 0.011, 0.011, -0.01, 2.2)], ACCENT, 20, Matrix.Translation(P(x, 0, 0)))
        c.slots(-0.04, 0.11, 4, -0.01, 0.014, depth=0.004, fill=ENERGY, sides=(s,))
        c.groove(-0.05)
        c.groove(0.12)
        c.build(b)
        for uu in (-0.03, 0.09):
            b.add(box(0.03, 0.016, 0.016, (s * 0.04, uu, -0.01), 0.003), METAL)
            screw(b, uu, -0.01, s * 0.07, 0.0024, s)
        b.add(sweep([(s * 0.054, 0.138, -0.01), (s * 0.056, 0.16, -0.008), (s * 0.05, 0.2, -0.006), (s * 0.036, 0.215, -0.006)], 0.004, 8), RUBBER)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.16, -0.36, -0.012, 0.072, 0.02, 0.014, "full")
    finish_pts(g, (0, 0.415, 0), grip, butt, (0, 0.15, -0.05), (0, 0.03, -0.01))


def spearhead(b, u0, l, r, wide=1.0, nb=4, mat=METAL, roll0=45.0):
    """Pointe de lance : noyau en losange, lames radiales, bague lumineuse."""
    b.add(loft([S(u0, r, r, 0, 2.0), S(u0 + 0.014, r * 1.15, r * 1.15, 0, 1.4), S(u0 + l * 0.55, r * 0.6, r * 0.6, 0, 1.3),
                S(u0 + l, 0.0012, 0.0012, 0, 1.3)], 12), ACCENT)
    b.add(ring(r * 1.25, r * 0.9, u0, u0 + 0.008, 0, 0, 20), ENERGY)
    prof = [(u0 + 0.006, 0.0), (u0 + 0.006, r * 1.9 * wide), (u0 + 0.03, r * 2.1 * wide), (u0 + l * 0.9, 0.002), (u0 + l * 0.6, 0.0)]
    for k in range(nb):
        b.add(prism(prof, -0.0016, 0.0016, 0.0006), mat, M(roll=roll0 + 360 * k / nb))


def powerlance(g):
    """Power Lance : longue lance fine ; gaine octogonale, canon à anneaux accélérateurs, rails latéraux, pointe à 4 lames."""
    b = g.body
    h = receiver_std(b, -0.15, 0.17, 0.027, 0.042)
    for u in (-0.11, 0.02, 0.13):
        h.groove(u)
    h.seam(-0.1, 0.12, 0.012)
    h.slots(0.035, 0.12, 3, -0.016, 0.01, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.125, -0.06, -0.03, -0.01)
    s2 = Hull([S(0.15, 0.022, 0.022, 0, 2.0), S(0.17, 0.025, 0.025, 0, 2.0), S(0.39, 0.022, 0.022, 0, 2.0), S(0.41, 0.016, 0.016, 0, 2.0)], PAINT, 8)
    for u in (0.21, 0.27, 0.33):
        s2.groove(u, 0.0014, 0.0025)
    s2.build(b)
    b.add(tube(0.0105, 0.009, 0.4, 0.88, 0, 0, 16), METAL)
    for uu in (0.45, 0.55, 0.65, 0.75):
        accel(b, uu, 0.021, 0.0095, 0.018)
    for s in (-1, 1):
        b.add(box(0.004, 0.46, 0.007, (s * 0.017, 0.65, 0), 0.0012), METAL)
        b.add(box(0.0015, 0.42, 0.0025, (s * 0.0193, 0.65, 0), 0.0004, 1), ENERGY)
    b.add(ring(0.024, 0.009, 0.86, 0.88, 0, 0, 22), ACCENT)
    spearhead(b, 0.88, 0.13, 0.014)
    reflex(b, -0.02, h.sz(-0.02) - 0.002)
    sup = wd_vgrip(b, 0.3, -0.022, 0.08)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.14, -0.33, -0.012, 0.064, 0.018, 0.012, "skel")
    finish_pts(g, (0, 1.01, 0), grip, butt, sup, (0, 0.05, -0.04), True)


def dragoon(g):
    """Dragoon Lance : lance lourde perforante ; gaine hexagonale ciselée, ailerons dorsaux, banc de condensateurs,
    canon épais à colliers percés, pointe large à quatre lames."""
    b = g.body
    h = receiver_std(b, -0.16, 0.17, 0.034, 0.05, p=3.6)
    for u in (-0.12, -0.04, 0.12):
        h.groove(u)
    h.seam(-0.1, 0.12, 0.018)
    h.seam(-0.1, 0.12, -0.026)
    h.slots(0.0, 0.11, 4, -0.004, 0.012, fill=POLY)
    h.build(b)
    plate(b, h, -0.14, -0.07, -0.03, 0.0)
    hx = Hull([S(0.14, 0.03, 0.03, 0, 2.0), S(0.17, 0.038, 0.038, 0, 2.0), S(0.6, 0.034, 0.034, 0, 2.0), S(0.64, 0.026, 0.026, 0, 2.0)], PAINT, 6)
    for u in (0.23, 0.33, 0.43, 0.53):
        hx.groove(u, 0.002, 0.004)
    hx.build(b)
    for s in (-1, 1):                                                    # fentes lumineuses sur les pans latéraux
        for k in range(4):
            uu = 0.185 + k * 0.1
            b.add(box(0.004, 0.032, 0.012, (s * 0.0352, uu + 0.02, 0.0), 0.0012), ENERGY)
            b.add(box(0.006, 0.04, 0.018, (s * 0.034, uu + 0.02, 0.0), 0.002), ACCENT)
    b.add(tube(0.0175, 0.016, 0.62, 0.96, 0, 0, 18), METAL)
    for uu in (0.7, 0.83):
        holed_ring(b, uu, uu + 0.03, 0.03, 0.016, 10, 0.004, ACCENT)
        b.add(ring(0.026, 0.016, uu + 0.03, uu + 0.036, 0, 0, 24), ENERGY)
    for k in range(6):
        b.add(box(0.003, 0.08, 0.008, (0, 0.0, 0.0), 0.0008), METAL, M((0, 0.76, 0), roll=k * 60) @ Matrix.Translation(P(0, 0, 0.019)))
    spearhead(b, 0.95, 0.16, 0.02, 1.25)
    # Ailerons dorsaux et banc de condensateurs.
    for s in (-1, 1):
        b.add(prism([(0.2, 0.026), (0.48, 0.026), (0.46, 0.04), (0.3, 0.075), (0.26, 0.075)], -0.003, 0.003, 0.001), ACCENT,
              M((s * 0.012, 0, 0), roll=s * 18))
    for k in range(3):
        uu = -0.06 + k * 0.045
        b.add(tube_x(0.013, 0.05, (0, uu, 0.058), 16), ACCENT)
        b.add(tube_x(0.0136, 0.016, (0, uu, 0.058), 16), ENERGY)
        b.add(tube_x(0.007, 0.056, (0, uu, 0.058), 10), METAL)
    b.add(box(0.044, 0.14, 0.008, (0, -0.015, 0.046), 0.002), METAL)
    reflex(b, 0.08, h.sz(0.08) - 0.002)
    sup = wd_vgrip(b, 0.32, -0.034, 0.082)
    grip = wd_grip(b)
    mag = cell_pack(b, 0.12, -0.066, 0.08, 0.016)
    butt = wd_stock(b, -0.15, -0.37, -0.012, 0.078, 0.021, 0.014, "full")
    finish_pts(g, (0, 1.11, 0), grip, butt, sup, mag, True)


def monsterlance(g):
    """Monster Lance : lance à cadre ouvert ; deux rails autour d'un noyau lumineux, brides, tambours de condensateurs
    au-dessus, pointe à trois lames."""
    b = g.body
    h = receiver_std(b, -0.17, 0.2, 0.036, 0.054, p=3.6)
    for u in (-0.13, -0.05, 0.08, 0.15):
        h.groove(u)
    h.seam(-0.12, 0.16, -0.028)
    h.slots(-0.03, 0.07, 4, -0.012, 0.014, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.15, -0.08, -0.034, 0.0)
    for sg in (-1, 1):                                                   # rails haut / bas
        r = Hull([S(0.18, 0.012, 0.008, sg * 0.024, 4), S(0.2, 0.014, 0.009, sg * 0.024, 4), S(0.94, 0.012, 0.008, sg * 0.022, 4),
                  S(0.97, 0.008, 0.005, sg * 0.02, 4)], PAINT, 20)
        r.tslots(0.25, 0.9, 10, 0.0, 0.016, depth=0.0025, fill=POLY, top=sg > 0)
        r.build(b)
        b.add(box(0.012, 0.74, 0.002, (0, 0.58, sg * 0.0155), 0.0004, 1), ENERGY)
    b.add(tube(0.0065, 0.0065, 0.18, 0.96, 0, 0, 12), ENERGY)
    for uu in (0.28, 0.44, 0.6, 0.76, 0.9):                               # brides en cadre
        fr = cut(box(0.046, 0.024, 0.074, (0, uu, 0), 0.006), [box(0.03, 0.05, 0.03, (0, uu, 0), 0.008, 3)])
        b.add(fr, ACCENT)
        for s in (-1, 1):
            screw(b, uu, 0.0, s * 0.023, 0.0024, s)
    spearhead(b, 0.96, 0.15, 0.016, 1.3, 3, METAL, 90)
    for s in (-1, 1):                                                    # tambours de condensateurs
        x = s * 0.034
        c = Hull([S(-0.08, 0.012, 0.012, 0.064, 2.2), S(-0.07, 0.019, 0.019, 0.064, 2.2), S(0.3, 0.019, 0.019, 0.064, 2.2),
                  S(0.31, 0.012, 0.012, 0.064, 2.2)], PAINT, 20, Matrix.Translation(P(x, 0, 0)))
        for u in (-0.04, 0.06, 0.16, 0.26):
            c.groove(u, 0.002, 0.004)
        c.slots(-0.03, 0.25, 5, 0.064, 0.016, depth=0.004, fill=ENERGY, sides=(s,))
        c.build(b)
        b.add(tube(0.011, 0.011, 0.305, 0.32, x, 0.064, 16), ENERGY)
    for uu in (-0.03, 0.1, 0.24):
        b.add(box(0.1, 0.02, 0.016, (0, uu, 0.05), 0.004), METAL)
    sup = wd_vgrip(b, 0.36, -0.03, 0.085)
    grip = wd_grip(b)
    mag = cell_pack(b, 0.12, -0.07, 0.09, 0.018)
    butt = wd_stock(b, -0.16, -0.39, -0.014, 0.084, 0.022, 0.016, "full", ACCENT)
    finish_pts(g, (0, 1.11, 0), grip, butt, sup, mag, True)


def sparkvine(g):
    """Spark Vine / Spark Foil : tige isolée (pile de disques céramiques) enroulée d'une bobine, électrode en cage."""
    b = g.body
    h = receiver_std(b, -0.15, 0.19, 0.03, 0.046)
    for u in (-0.1, 0.02, 0.12):
        h.groove(u)
    h.seam(-0.1, 0.16, 0.014)
    h.slots(0.03, 0.115, 4, -0.02, 0.012, fill=POLY)
    h.tslots(-0.07, 0.07, 3, 0.0, 0.024, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.125, -0.06, -0.03, -0.01)
    b.add(tube(0.025, 0.014, 0.18, 0.235, 0, 0, 20), ACCENT)
    b.add(tube(0.0065, 0.0065, 0.23, 0.6, 0, 0, 12), METAL)
    b.add(helix(0.235, 0.575, 0.0125, 0.0026, 15), ALLOY)
    for k in range(6):
        uu = 0.25 + k * 0.064
        b.add(tube(0.02, 0.022, uu, uu + 0.008, 0, 0, 22), PAINT)
        b.add(tube(0.022, 0.017, uu + 0.008, uu + 0.014, 0, 0, 22), PAINT)
        b.add(ring(0.016, 0.006, uu + 0.014, uu + 0.018, 0, 0, 18), ENERGY)
    b.add(tube(0.012, 0.008, 0.59, 0.61, 0, 0, 16), METAL)
    b.add(ball(0.0135, (0, 0.625, 0), 18, 12), ENERGY)
    for k in range(3):
        b.add(sweep(bez((0, 0.6, 0.009), (0, 0.615, 0.022), (0, 0.64, 0.02), (0, 0.648, 0.006), 8), 0.0026, 8, 0.0018), METAL, M(roll=k * 120))
    b.add(sweep([(0, 0.13, -0.038), (0, 0.17, -0.034), (0, 0.21, -0.024), (0, 0.245, -0.012)], 0.004, 8), RUBBER)
    mag = cell_pack(b, 0.105, -0.064, 0.085, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.14, -0.31, -0.012, 0.064, 0.018, 0.012, "skel")
    finish_pts(g, (0, 0.64, 0), grip, butt, (0, 0.12, -0.085), mag)


def sparkwhip(g):
    """Spark Whip : enrouleur de câble (tambour transversal), buse, trois fouets segmentés à pointes lumineuses."""
    b = g.body
    h = Hull([S(-0.15, 0.018, 0.026, -0.008, 2.4), S(-0.13, 0.03, 0.044, -0.008, 2.8), S(0.04, 0.034, 0.05, -0.006, 3.0),
              S(0.16, 0.032, 0.044, -0.004, 2.8), S(0.19, 0.026, 0.034, -0.004, 2.4)])
    for u in (-0.1, 0.0, 0.1):
        h.groove(u)
    h.seam(-0.09, 0.17, -0.024)
    h.window(0.01, 0.09, 0.006, 0.018, 0.003, ENERGY)
    h.build(b)
    plate(b, h, -0.125, -0.06, -0.036, -0.012)
    b.add(prism([(-0.12, 0.03), (-0.1, 0.056), (0.12, 0.056), (0.16, 0.034)], -0.008, 0.008, 0.0015), ACCENT)
    for u in (-0.06, 0.0, 0.06):
        b.add(box(0.02, 0.006, 0.005, (0, u, 0.057), 0.001), METAL)
    # Tambour enrouleur.
    b.add(tube_x(0.035, 0.064, (0, 0.22, -0.004), 28), ACCENT)
    for k in range(9):
        b.add(tube_x(0.0372, 0.0045, (-0.026 + k * 0.0065, 0.22, -0.004), 28), ALLOY)
    for s in (-1, 1):
        b.add(tube_x(0.041, 0.006, (s * 0.035, 0.22, -0.004), 28), PAINT)
        b.add(tube_x(0.013, 0.012, (s * 0.04, 0.22, -0.004), 16), ENERGY)
        for k in range(6):
            a = 2 * math.pi * k / 6
            b.add(tube_x(0.003, 0.004, (s * 0.039, 0.22 + 0.026 * math.cos(a), -0.004 + 0.026 * math.sin(a)), 8), METAL)
    b.add(box(0.075, 0.05, 0.012, (0, 0.205, -0.042), 0.004), PAINT)
    # Buse et fouets.
    b.add(tube(0.02, 0.013, 0.25, 0.3, 0, 0, 18), PAINT)
    holed_ring(b, 0.29, 0.305, 0.019, 0.008, 6, 0.003)
    for k, yaw in enumerate((-13, 0, 13)):
        m = M((0, 0.3, 0), yaw=yaw)
        path = bez((0, 0.0, 0.0), (0, 0.1, 0.02 - abs(yaw) * 0.0006), (0, 0.2, -0.005), (0, 0.31, -0.035 + k * 0.004), 14)
        b.add(sweep(path, 0.0045, 8, 0.0025), METAL, m.copy())
        for t in (3, 5, 7, 9, 11):
            b.add(ball(0.006 - t * 0.00022, path[t], 10, 6), ENERGY if t % 4 == 1 else ACCENT, m.copy())
        b.add(ball(0.0065, path[-1], 12, 8), ENERGY, m.copy())
    mag = cell_pack(b, 0.105, -0.064, 0.085, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.14, -0.3, -0.014, 0.064, 0.018, 0.012, "skel")
    finish_pts(g, (0, 0.61, -0.03), grip, butt, (0, 0.12, -0.085), mag)


# ───────────────────────── Blasters ─────────────────────────

def magblaster(g):
    """Mag Blaster : boîtier anguleux, trois grands anneaux magnétiques percés, tirants, bobine et lentille de bouche."""
    b = g.body
    h = Hull([S(-0.16, 0.02, 0.03, -0.006, 2.6), S(-0.14, 0.034, 0.048, -0.006, 3.6), S(0.04, 0.036, 0.054, -0.004, 4.2),
              S(0.2, 0.034, 0.05, 0.0, 4.0), S(0.245, 0.03, 0.04, 0.0, 3.4)])
    for u in (-0.11, 0.0, 0.12, 0.21):
        h.groove(u)
    h.seam(-0.1, 0.2, 0.024)
    h.window(0.03, 0.19, -0.026, -0.018, 0.003, ENERGY)
    h.tslots(-0.07, 0.03, 4, 0.0, 0.028)
    h.slots(-0.1, -0.04, 3, 0.0, 0.016, fill=POLY)
    h.build(b)
    plate(b, h, -0.13, -0.05, -0.042, -0.016)
    top = h.sz(0.0)
    rail(b, -0.08, 0.17, top - 0.004, w=0.02)
    reflex(b, 0.0, top + 0.006)
    b.add(tube(0.017, 0.017, 0.24, 0.5, 0, 0, 20), METAL)
    b.add(helix(0.26, 0.46, 0.021, 0.0028, 9), ALLOY)
    for uu in (0.27, 0.35, 0.43):
        holed_ring(b, uu, uu + 0.022, 0.047, 0.018, 10, 0.0055, ACCENT)
        b.add(ring(0.041, 0.03, uu - 0.002, uu + 0.024, 0, 0, 30), ENERGY)
        b.add(ring(0.05, 0.044, uu + 0.006, uu + 0.016, 0, 0, 30), PAINT)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(tube(0.004, 0.004, 0.25, 0.49, 0.036 * math.cos(a), 0.036 * math.sin(a), 10), METAL)
    b.add(ring(0.032, 0.012, 0.48, 0.5, 0, 0, 26), ACCENT)
    b.add(tube(0.016, 0.016, 0.495, 0.503, 0, 0, 22), LENS)
    b.add(tube(0.008, 0.008, 0.502, 0.505, 0, 0, 14), ENERGY)
    mag = cell_pack(b, 0.12, -0.07, 0.09, 0.018)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.35, -0.012, 0.07, 0.02, 0.014, "full")
    finish_pts(g, (0, 0.51, 0), grip, butt, (0, 0.13, -0.09), mag)


def destroyer(g):
    """Destroyer Blaster : capot large à six alésages lumineux, radiateurs à ailettes, poignée de transport."""
    b = g.body
    h = receiver_std(b, -0.16, 0.2, 0.034, 0.05, p=3.6)
    for u in (-0.11, 0.0, 0.13):
        h.groove(u)
    h.seam(-0.1, 0.15, 0.02)
    h.slots(-0.08, 0.0, 4, -0.012, 0.014, fill=POLY)
    h.build(b)
    plate(b, h, -0.14, -0.09, -0.03, 0.01)
    hood = Hull([S(0.15, 0.036, 0.046, 0, 3.6), S(0.2, 0.062, 0.056, 0, 4.2), S(0.4, 0.07, 0.05, 0, 4.6), S(0.43, 0.066, 0.046, 0, 4.2)], PAINT, 32)
    for u in (0.235, 0.34):
        hood.groove(u, 0.002, 0.004)
    hood.tseam(0.21, 0.42, 0.0)
    hood.tslots(0.25, 0.33, 4, 0.0, 0.07, fill=ENERGY)
    for x in (-0.04, 0.0, 0.04):
        for z in (-0.02, 0.02):
            hood.cut(tube(0.0125, 0.0125, 0.38, 0.46, x, z, 18))
    hood.ins.append((box(0.13, 0.006, 0.08, (0, 0.397, 0), 0), ENERGY))
    hood.build(b)
    for x in (-0.04, 0.0, 0.04):
        for z in (-0.02, 0.02):
            b.add(ring(0.016, 0.0125, 0.426, 0.434, x, z, 18), METAL)
    for s in (-1, 1):                                                    # radiateurs latéraux
        for k in range(8):
            uu = 0.205 + k * 0.022
            b.add(box(0.016, 0.0045, 0.074, (s * (hood.sx(uu, 0) + 0.006), uu, 0), 0.0012), ACCENT)
        b.add(box(0.006, 0.17, 0.008, (s * (hood.sx(0.28, 0) + 0.012), 0.28, 0.0), 0.002), METAL)
    path = [(0, 0.05, 0.05), (0, 0.07, 0.088), (0, 0.11, 0.096), (0, 0.22, 0.096), (0, 0.26, 0.085), (0, 0.29, 0.058)]
    b.add(sweep(path, 0.0065, 10), METAL)
    b.add(tube(0.0095, 0.0095, 0.11, 0.21, 0, 0.096, 14), RUBBER)
    sup = wd_vgrip(b, 0.3, -0.048, 0.08)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.34, -0.012, 0.07, 0.02, 0.012, "rods")
    finish_pts(g, (0, 0.44, 0), grip, butt, sup, (0, 0.05, -0.04), True)


def spine(g):
    """Spine Blaster : colonne de vertèbres décroissantes autour d'un noyau lumineux, rails inférieurs, couronne."""
    b = g.body
    h = receiver_std(b, -0.16, 0.21, 0.03, 0.048, p=3.3)
    for u in (-0.11, 0.0, 0.14):
        h.groove(u)
    h.seam(-0.1, 0.18, 0.018)
    h.slots(0.02, 0.13, 5, -0.018, 0.012, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.135, -0.06, -0.032, -0.008)
    b.add(tube(0.008, 0.008, 0.2, 0.92, 0, 0, 14), ENERGY)
    for k in range(13):
        uu = 0.22 + k * 0.053
        sc = 1.0 - 0.025 * k
        b.add(ring(0.024 * sc, 0.0085, uu, uu + 0.014, 0, 0, 20), PAINT)
        b.add(ring(0.02 * sc, 0.0085, uu + 0.014, uu + 0.02, 0, 0, 20), ACCENT)
        b.add(prism([(uu - 0.004, 0.02 * sc), (uu + 0.014, 0.02 * sc), (uu + 0.002, 0.05 * sc), (uu - 0.01, 0.048 * sc)], -0.0035, 0.0035, 0.0008), PAINT)
        for s in (-1, 1):
            b.add(hprism([(uu, s * 0.02 * sc), (uu + 0.012, s * 0.02 * sc), (uu - 0.004, s * 0.038 * sc), (uu - 0.01, s * 0.036 * sc)], -0.003, 0.003, 0.0008), ACCENT)
    for s in (-1, 1):
        b.add(box(0.005, 0.7, 0.008, (s * 0.012, 0.56, -0.022), 0.0015), METAL)
    hg = Hull([S(0.19, 0.03, 0.022, -0.026, 3), S(0.22, 0.032, 0.026, -0.03, 3.4), S(0.38, 0.03, 0.022, -0.03, 3.4), S(0.4, 0.024, 0.016, -0.028, 3)], PAINT, 24)
    hg.slots(0.23, 0.37, 6, -0.034, 0.01, fill=POLY)
    hg.build(b)
    crown(b, 0.9, 0.022, 5, 0.06, PAINT)
    reflex(b, 0.0, h.sz(0.0) - 0.002)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.34, -0.012, 0.066, 0.019, 0.012, "skel")
    mag = cell_pack(b, 0.11, -0.066, 0.08, 0.016)
    finish_pts(g, (0, 0.96, 0), grip, butt, (0, 0.3, -0.05), mag)


def reflectron(g):
    """Reflectron Laser : coque à facettes, cage à trois montants tenant un prisme lumineux, miroirs en pétales."""
    b = g.body
    h = Hull([S(-0.16, 0.018, 0.026, -0.004, 1.8), S(-0.14, 0.03, 0.044, -0.004, 1.9), S(0.02, 0.038, 0.056, -0.002, 2.0),
              S(0.18, 0.032, 0.046, 0.0, 1.9), S(0.22, 0.024, 0.032, 0.0, 1.8)], PAINT, 16)
    for u in (-0.1, 0.0, 0.13):
        h.groove(u, 0.002, 0.004)
    h.window(0.02, 0.12, -0.01, 0.0, 0.003, ENERGY)
    h.build(b)
    b.add(prism([(-0.12, 0.02), (-0.09, 0.064), (0.12, 0.054), (0.17, 0.02)], -0.004, 0.004, 0.001), ACCENT)
    holed_ring(b, 0.21, 0.235, 0.036, 0.01, 6, 0.005, ACCENT)
    for k in range(3):
        m = M(roll=90 + k * 120)
        b.add(sweep(bez((0, 0.225, 0.03), (0, 0.27, 0.06), (0, 0.33, 0.055), (0, 0.37, 0.024), 10), 0.0055, 8, 0.004), PAINT, m.copy())
        b.add(ball(0.006, (0, 0.372, 0.022), 10, 6), METAL, m.copy())
    b.add(loft([S(0.25, 0.002, 0.002, 0, 1.0), S(0.3, 0.026, 0.026, 0, 1.0), S(0.31, 0.026, 0.026, 0, 1.0), S(0.36, 0.002, 0.002, 0, 1.0)], 4), ENERGY)
    b.add(ring(0.02, 0.012, 0.3, 0.31, 0, 0, 4), METAL, M(roll=45))
    for k in range(4):                                                   # miroirs en pétales
        m = M(roll=45 + k * 90)
        b.add(prism([(0.22, 0.034), (0.26, 0.048), (0.27, 0.07), (0.235, 0.05)], -0.0015, 0.0015, 0.0005), ALLOY, m.copy())
    for s in (-1, 1):
        b.add(sweep([(s * 0.03, 0.12, -0.02), (s * 0.04, 0.16, -0.02), (s * 0.036, 0.2, -0.01), (s * 0.026, 0.215, -0.004)], 0.0035, 8), RUBBER)
    reflex(b, -0.04, h.sz(-0.04) - 0.002)
    mag = cell_pack(b, 0.11, -0.068, 0.08, 0.017)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.33, -0.012, 0.066, 0.019, 0.012, "full")
    finish_pts(g, (0, 0.37, 0), grip, butt, (0, 0.12, -0.09), mag)


# ───────────────────────── Électriques ─────────────────────────

def lightningbow(g):
    """Lightning Bow : arc vertical ; branches à bande lumineuse, poulies, corde d'énergie, flèche-électrode."""
    b = g.body
    h = receiver_std(b, -0.15, 0.13, 0.028, 0.044)
    for u in (-0.1, 0.0, 0.09):
        h.groove(u)
    h.seam(-0.09, 0.1, 0.016)
    h.slots(-0.06, 0.06, 4, -0.02, 0.01, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.13, -0.075, -0.03, -0.006)
    riser = Hull([S(0.11, 0.022, 0.05, 0, 3), S(0.14, 0.026, 0.07, 0, 3.4), S(0.2, 0.024, 0.07, 0, 3.4), S(0.22, 0.018, 0.05, 0, 3)], ACCENT, 24)
    riser.groove(0.17, 0.0015, 0.003)
    riser.build(b)
    up = bez((0.17, 0.06), (0.25, 0.12), (0.17, 0.22), (0.27, 0.3), 16)
    for sg in (-1, 1):
        pth = [(u, sg * z) for (u, z) in up]
        b.add(prism(band(pth, 0.03, 0.012), -0.009, 0.009, 0.0015), PAINT)
        b.add(prism(band(pth[2:-1], 0.007, 0.004), -0.0098, 0.0098, 0.0006), ENERGY)
        tu, tz = pth[-1]
        b.add(tube_x(0.016, 0.022, (0, tu, tz), 20), METAL)
        b.add(tube_x(0.007, 0.026, (0, tu, tz), 12), ENERGY)
        b.add(sweep([(0, tu - 0.004, tz - sg * 0.014), (0, 0.06, 0.0)], 0.0022, 6), ENERGY)
        b.add(prism([(0.14, sg * 0.06), (0.2, sg * 0.06), (0.19, sg * 0.08), (0.15, sg * 0.08)], -0.012, 0.012, 0.001), METAL)
    b.add(box(0.012, 0.02, 0.014, (0, 0.06, 0.0), 0.003), METAL)                 # encoche
    b.add(tube(0.004, 0.004, 0.06, 0.34, 0, 0, 10), ENERGY)
    b.add(tube(0.008, 0.001, 0.34, 0.38, 0, 0, 4), METAL)
    for k in range(3):
        b.add(prism([(0.07, 0.004), (0.1, 0.004), (0.085, 0.014)], -0.0008, 0.0008, 0), ENERGY, M(roll=k * 120))
    mag = cell_pack(b, 0.09, -0.062, 0.07, 0.015)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.14, -0.31, -0.012, 0.064, 0.018, 0.012, "skel")
    finish_pts(g, (0, 0.38, 0), grip, butt, (0, 0.17, -0.07), mag)


def boltgun(g):
    """Bolt Gun : carabine compacte à double électrode (piles d'isolateurs), arc d'amorçage, sphère de condensateur."""
    b = g.body
    h = Hull([S(-0.12, 0.02, 0.028, -0.006, 2.4), S(-0.1, 0.036, 0.044, -0.006, 3.2), S(0.04, 0.04, 0.05, -0.004, 3.4),
              S(0.14, 0.038, 0.04, 0.0, 3.0), S(0.17, 0.032, 0.03, 0.0, 2.6)])
    for u in (-0.08, 0.02, 0.12):
        h.groove(u)
    h.seam(-0.07, 0.14, 0.014)
    h.slots(0.03, 0.11, 4, -0.02, 0.012, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.09, -0.04, -0.03, -0.006)
    b.add(hprism([(0.15, -0.05), (0.19, -0.05), (0.2, -0.038), (0.2, 0.038), (0.19, 0.05), (0.15, 0.05)], -0.016, 0.016, 0.002), ACCENT)
    for s in (-1, 1):
        x = s * 0.03
        b.add(tube(0.0075, 0.006, 0.18, 0.43, x, 0, 12), METAL)
        for k in range(6):
            uu = 0.205 + k * 0.022
            b.add(tube(0.0145, 0.0145, uu, uu + 0.01, x, 0, 18), PAINT)
            b.add(tube(0.011, 0.011, uu + 0.01, uu + 0.022, x, 0, 14), ACCENT)
        b.add(tube(0.006, 0.0045, 0.43, 0.44, x, 0, 12), ACCENT)
        b.add(ball(0.0095, (x, 0.447, 0), 14, 10), ENERGY)
    b.add(hprism([(0.36, -0.04), (0.38, -0.04), (0.38, 0.04), (0.36, 0.04)], -0.01, 0.01, 0.002), ACCENT)
    b.add(sweep([(-0.024 + k * 0.006, 0.447 + (0.007 if k % 2 else -0.004), 0.003 * (k % 3 - 1)) for k in range(9)], 0.0016, 6), ENERGY)
    b.add(ball(0.03, (0, -0.075, 0.042), 20, 12), ACCENT)                   # sphère de condensateur
    b.add(torus_u(-0.075, 0.031, 0.003, 0, 0.042, 28, 6), ENERGY)
    b.add(box(0.024, 0.05, 0.024, (0, -0.075, 0.02), 0.004), METAL)
    mag = cell_pack(b, 0.1, -0.062, 0.08, 0.016)
    grip = wd_grip(b, -0.03, -0.04)
    butt = wd_stock(b, -0.11, -0.25, -0.014, 0.062, 0.018, 0.01, "rods")
    finish_pts(g, (0, 0.46, 0), grip, butt, (0, 0.11, -0.085), mag)


def crossbow(g):
    """Thunder Crossbow : arbalète ; bras horizontaux à bande lumineuse, poulies, corde, carreau d'énergie, étrier."""
    b = g.body
    h = Hull([S(-0.16, 0.018, 0.026, -0.006, 2.4), S(-0.14, 0.03, 0.044, -0.006, 3.0), S(0.06, 0.032, 0.048, -0.004, 3.2),
              S(0.3, 0.03, 0.036, 0.0, 3.0), S(0.37, 0.026, 0.026, 0.0, 2.6)])
    for u in (-0.1, 0.02, 0.16, 0.28):
        h.groove(u)
    h.tseam(0.06, 0.34, 0.0, 0.003, 0.006)
    h.slots(0.08, 0.27, 6, -0.016, 0.012, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.13, -0.06, -0.032, -0.008)
    zb = 0.036
    wing = bez((0.33, 0.02), (0.37, 0.1), (0.31, 0.18), (0.36, 0.26), 16)
    for s in (-1, 1):
        pth = [(u, s * x) for (u, x) in wing]
        b.add(hprism(band(pth, 0.036, 0.014), zb - 0.011, zb + 0.011, 0.0015), PAINT)
        b.add(hprism(band(pth[2:-1], 0.007, 0.004), zb - 0.0118, zb + 0.0118, 0.0006), ENERGY)
        tu, tx = pth[-1]
        b.add(tube(0.014, 0.014, -0.012, 0.012, 0, 0, 18), METAL, M((tx, tu, zb), pitch=90))
        b.add(tube(0.006, 0.006, -0.015, 0.015, 0, 0, 10), ENERGY, M((tx, tu, zb), pitch=90))
        b.add(sweep([(tx - s * 0.012, tu - 0.004, zb), (0, 0.1, zb)], 0.002, 6), ENERGY)
    b.add(hprism([(0.3, -0.03), (0.37, -0.03), (0.37, 0.03), (0.3, 0.03)], zb - 0.012, zb + 0.012, 0.003), ACCENT)
    b.add(box(0.026, 0.03, 0.02, (0, 0.09, zb), 0.004), METAL)                    # gâchette de corde
    b.add(tube(0.0035, 0.0035, 0.1, 0.42, 0, zb + 0.006, 8), ENERGY)               # carreau
    b.add(loft([S(0.42, 0.006, 0.003, zb + 0.006, 1.2), S(0.428, 0.008, 0.004, zb + 0.006, 1.2), S(0.46, 0.001, 0.001, zb + 0.006, 1.2)], 4), METAL)
    for k in range(3):
        b.add(prism([(0.11, 0.003), (0.15, 0.003), (0.12, 0.013)], -0.0008, 0.0008, 0), ENERGY, M((0, 0, zb + 0.006), roll=k * 120))
    b.add(sweep([(-0.03, 0.37, -0.0), (-0.036, 0.43, -0.02), (0.0, 0.452, -0.03), (0.036, 0.43, -0.02), (0.03, 0.37, 0.0)], 0.0045, 8), METAL)
    mag = cell_pack(b, 0.11, -0.064, 0.09, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.37, -0.012, 0.07, 0.019, 0.014, "full")
    finish_pts(g, (0, 0.46, zb + 0.006), grip, butt, (0, 0.24, -0.04), mag)


# ───────────────────────── Impulsions ─────────────────────────

def pulsemg(g):
    """Pulse Machine Gun : boîtier de fusil, tambour de chambre à couronne lumineuse, manchon ventilé, poignée verticale."""
    b = g.body
    h = Hull([S(-0.16, 0.02, 0.03, -0.006, 2.6), S(-0.14, 0.032, 0.046, -0.006, 3.4), S(0.02, 0.036, 0.054, -0.004, 3.8),
              S(0.18, 0.034, 0.048, 0.0, 3.6), S(0.22, 0.028, 0.036, 0.0, 3.0)])
    for u in (-0.11, -0.03, 0.09, 0.17):
        h.groove(u)
    h.seam(-0.1, 0.2, 0.022)
    h.slots(0.1, 0.17, 3, -0.02, 0.014, fill=POLY)
    h.build(b)
    plate(b, h, -0.13, -0.06, -0.034, -0.01)
    b.add(tube_x(0.044, 0.084, (0, 0.03, -0.006), 32), ACCENT)                # tambour de chambre
    b.add(tube_x(0.038, 0.092, (0, 0.03, -0.006), 32), ENERGY)
    b.add(tube_x(0.027, 0.098, (0, 0.03, -0.006), 20), METAL)
    for k in range(8):
        a = 2 * math.pi * k / 8
        b.add(box(0.096, 0.006, 0.006, (0, 0.03 + 0.034 * math.cos(a), -0.006 + 0.034 * math.sin(a)), 0.001), METAL)
    top = h.sz(0.0)
    rail(b, -0.1, 0.2, top - 0.004, w=0.02)
    reflex(b, -0.06, top + 0.006)
    sh = Hull([S(0.21, 0.022, 0.022, 0, 2), S(0.23, 0.026, 0.026, 0, 2), S(0.48, 0.024, 0.024, 0, 2), S(0.5, 0.019, 0.019, 0, 2)], PAINT, 24)
    sh.slots(0.25, 0.47, 7, 0.0, 0.01, fill=ENERGY)
    sh.tslots(0.25, 0.47, 7, 0.0, 0.01, fill=POLY)
    sh.groove(0.24)
    sh.build(b)
    b.add(tube(0.012, 0.012, 0.49, 0.54, 0, 0, 16), METAL)
    holed_ring(b, 0.52, 0.55, 0.019, 0.009, 8, 0.0035)
    b.add(ring(0.016, 0.009, 0.55, 0.555, 0, 0, 18), ENERGY)
    sup = wd_vgrip(b, 0.31, -0.024, 0.08)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.35, -0.012, 0.068, 0.019, 0.012, "rods")
    finish_pts(g, (0, 0.56, 0), grip, butt, sup, (0, 0.03, -0.006), True)


def stardust(g):
    """Stardust Cannon : canon à grappe ; gaine hexagonale à sept alésages lumineux, cellules latérales, poignée haute."""
    b = g.body
    h = receiver_std(b, -0.16, 0.18, 0.036, 0.052, p=3.6)
    for u in (-0.11, -0.02, 0.1):
        h.groove(u)
    h.seam(-0.1, 0.15, 0.02)
    h.slots(-0.06, 0.08, 5, -0.018, 0.012, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.14, -0.075, -0.036, -0.01)
    hx = Hull([S(0.15, 0.048, 0.048, 0, 2), S(0.17, 0.062, 0.062, 0, 2), S(0.44, 0.06, 0.06, 0, 2), S(0.47, 0.054, 0.054, 0, 2)], PAINT, 6)
    for u in (0.2, 0.28, 0.36, 0.42):
        hx.groove(u, 0.0025, 0.005)
    bores = [(0, 0)] + [(0.03 * math.cos(math.pi / 6 + k * math.pi / 3), 0.03 * math.sin(math.pi / 6 + k * math.pi / 3)) for k in range(6)]
    for (x, z) in bores:
        hx.cut(tube(0.0105, 0.0105, 0.43, 0.5, x, z, 16))
    hx.ins.append((tube(0.05, 0.05, 0.437, 0.442, 0, 0, 6), ENERGY))
    hx.build(b)
    for (x, z) in bores:
        b.add(ring(0.0135, 0.0105, 0.465, 0.474, x, z, 16), METAL)
    for s in (-1, 1):
        b.add(tube(0.012, 0.012, 0.19, 0.41, s * 0.05, -0.036, 16), ACCENT)
        for k in range(4):
            uu = 0.21 + k * 0.05
            b.add(tube(0.0128, 0.0128, uu, uu + 0.02, s * 0.05, -0.036, 16), ENERGY)
    path = [(0, 0.08, 0.05), (0, 0.1, 0.1), (0, 0.14, 0.11), (0, 0.3, 0.1), (0, 0.34, 0.07)]
    b.add(sweep(path, 0.007, 10), ACCENT)
    b.add(tube(0.01, 0.01, 0.15, 0.28, 0, 0.106, 14), RUBBER)
    sup = wd_vgrip(b, 0.3, -0.058, 0.08)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.35, -0.012, 0.072, 0.02, 0.014, "full")
    finish_pts(g, (0, 0.48, 0), grip, butt, sup, (0, 0.05, -0.04), True)


def pulsebuster(g):
    """Pulse Buster : quatre canons serrés par deux brides percées, grande chambre ronde à bague lumineuse."""
    b = g.body
    h = receiver_std(b, -0.17, 0.14, 0.036, 0.054, p=3.6)
    for u in (-0.12, -0.03, 0.08):
        h.groove(u)
    h.seam(-0.11, 0.12, 0.022)
    h.slots(-0.08, 0.06, 5, -0.02, 0.012, fill=POLY)
    h.build(b)
    plate(b, h, -0.15, -0.09, -0.036, -0.006)
    ch = Hull([S(0.11, 0.05, 0.05, 0, 2), S(0.13, 0.062, 0.062, 0, 2), S(0.22, 0.062, 0.062, 0, 2), S(0.25, 0.048, 0.048, 0, 2)], PAINT, 32)
    ch.groove(0.15, 0.0025, 0.005)
    ch.groove(0.2, 0.0025, 0.005)
    ch.build(b)
    b.add(ring(0.0632, 0.05, 0.172, 0.178, 0, 0, 32), ENERGY)
    pos = [(0.028 * math.cos(math.pi / 4 + k * math.pi / 2), 0.028 * math.sin(math.pi / 4 + k * math.pi / 2)) for k in range(4)]
    for (x, z) in pos:
        b.add(tube(0.0135, 0.0135, 0.24, 0.64, x, z, 16), METAL)
        b.add(tube(0.0095, 0.0095, 0.635, 0.644, x, z, 14), ENERGY)
        for uu in (0.3, 0.38, 0.46):
            b.add(ring(0.0155, 0.013, uu, uu + 0.008, x, z, 16), ACCENT)
    b.add(tube(0.008, 0.008, 0.24, 0.62, 0, 0, 10), ENERGY)
    for uu in (0.42, 0.6):
        cl = cut(tube(0.05, 0.05, uu, uu + 0.025, 0, 0, 8), [tube(0.0135, 0.0135, uu - 0.01, uu + 0.04, x, z, 16) for (x, z) in pos])
        b.add(cl, PAINT)
        for k in range(4):
            a = k * math.pi / 2
            b.add(tube(0.004, 0.004, uu + 0.024, uu + 0.028, 0.042 * math.cos(a), 0.042 * math.sin(a), 8), METAL)
    path = [(0, 0.0, 0.05), (0, 0.03, 0.098), (0, 0.07, 0.11), (0, 0.24, 0.1), (0, 0.27, 0.06)]
    b.add(sweep(path, 0.007, 10), ACCENT)
    b.add(tube(0.01, 0.01, 0.08, 0.22, 0, 0.106, 14), RUBBER)
    sup = wd_vgrip(b, 0.34, -0.04, 0.08)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.16, -0.38, -0.014, 0.078, 0.021, 0.014, "full")
    finish_pts(g, (0, 0.65, 0), grip, butt, sup, (0, 0.17, 0), True)


# ───────────────────────── Longue portée ─────────────────────────

def closedlaser(g):
    """Closed Laser : très long canon carré à ailettes de refroidissement et fentes dorsales lumineuses, lunette, bipied."""
    b = g.body
    h = receiver_std(b, -0.16, 0.22, 0.028, 0.044)
    for u in (-0.11, 0.0, 0.14):
        h.groove(u)
    h.seam(-0.1, 0.2, 0.016)
    h.slots(0.02, 0.16, 5, -0.018, 0.01, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.13, -0.06, -0.03, -0.008)
    br = Hull([S(0.2, 0.026, 0.03, -0.004, 4), S(0.46, 0.025, 0.028, -0.004, 4), S(0.5, 0.017, 0.02, 0, 4), S(1.1, 0.016, 0.019, 0, 4),
               S(1.12, 0.013, 0.015, 0, 3)], PAINT, 24)
    br.groove(0.25)
    br.groove(0.42)
    br.slots(0.27, 0.41, 7, -0.006, 0.014, fill=POLY)
    br.tslots(0.78, 1.08, 10, 0.0, 0.01, fill=ENERGY)
    br.seam(0.78, 1.08, 0.0)
    br.build(b)
    for k in range(9):
        uu = 0.52 + k * 0.026
        b.add(box(0.058, 0.0035, 0.054, (0, uu, 0), 0.0012), METAL)
    b.add(box(0.008, 0.25, 0.006, (0, 0.63, 0.028), 0.002), ACCENT)
    b.add(box(0.008, 0.25, 0.006, (0, 0.63, -0.028), 0.002), ACCENT)
    holed_ring(b, 1.11, 1.14, 0.022, 0.008, 8, 0.0035)
    b.add(tube(0.008, 0.008, 1.138, 1.142, 0, 0, 14), LENS)
    b.add(ring(0.018, 0.012, 1.14, 1.145, 0, 0, 20), ENERGY)
    bipod(b, 0.72, -0.024)
    wd_sight(b, 0.0, 0.088, 0.26, 0.014, h.sz(0.0) - 0.004)
    mag = cell_pack(b, 0.12, -0.064, 0.08, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.15, -0.38, -0.012, 0.07, 0.019, 0.016, "skel")
    finish_pts(g, (0, 1.15, 0), grip, butt, (0, 0.33, -0.04), mag)


def monster(g):
    """MONSTER : fusil laser lourd ; bancs de condensateurs latéraux, gaine ciselée, couronne à six griffes, grande lunette."""
    b = g.body
    h = Hull([S(-0.2, 0.022, 0.03, -0.008, 2.6), S(-0.18, 0.036, 0.052, -0.008, 3.4), S(0.04, 0.042, 0.062, -0.004, 3.8),
              S(0.2, 0.04, 0.056, 0.0, 3.6), S(0.25, 0.034, 0.044, 0.0, 3.2)])
    for u in (-0.14, -0.04, 0.08, 0.2):
        h.groove(u)
    h.seam(-0.13, 0.22, 0.026)
    h.seam(-0.13, 0.22, -0.034)
    h.tslots(-0.12, -0.02, 4, 0.0, 0.03)
    h.build(b)
    plate(b, h, -0.17, -0.07, -0.042, -0.012)
    for s in (-1, 1):
        x = s * 0.058
        c = Hull([S(-0.05, 0.016, 0.016, -0.006, 2.2), S(-0.04, 0.026, 0.026, -0.006, 2.2), S(0.33, 0.026, 0.026, -0.006, 2.2),
                  S(0.345, 0.016, 0.016, -0.006, 2.2)], ACCENT, 20, Matrix.Translation(P(x, 0, 0)))
        c.slots(-0.02, 0.31, 5, -0.006, 0.02, depth=0.005, fill=ENERGY, sides=(s,))
        for u in (-0.03, 0.32):
            c.groove(u, 0.002, 0.004)
        c.build(b)
        for uu in (0.0, 0.15, 0.29):
            b.add(box(0.04, 0.02, 0.03, (s * 0.04, uu, -0.006), 0.004), METAL)
            screw(b, uu, -0.006, s * 0.06, 0.0028, s)
        b.add(tube(0.013, 0.013, 0.34, 0.352, x, -0.006, 16), ENERGY)
    br = Hull([S(0.24, 0.03, 0.034, 0, 3), S(0.27, 0.032, 0.036, 0, 3), S(0.86, 0.026, 0.03, 0, 3), S(0.9, 0.022, 0.024, 0, 2.6)], PAINT, 20)
    for u in (0.32, 0.46, 0.6, 0.74):
        br.groove(u, 0.002, 0.004)
    br.tslots(0.34, 0.84, 8, 0.0, 0.012, fill=ENERGY)
    br.slots(0.5, 0.7, 3, -0.008, 0.012, fill=POLY)
    br.build(b)
    b.add(tube(0.018, 0.018, 0.88, 1.12, 0, 0, 18), METAL)
    accel(b, 0.96, 0.03, 0.017, 0.024, ACCENT, 6)
    b.add(ring(0.026, 0.017, 1.03, 1.05, 0, 0, 20), ACCENT)
    crown(b, 1.1, 0.034, 6, 0.08, PAINT)
    wd_sight(b, 0.02, 0.105, 0.34, 0.019, h.sz(0.02) - 0.004)
    b.add(tube(0.008, 0.008, -0.33, -0.17, 0, -0.07, 12), METAL)                   # monopode replié
    b.add(box(0.03, 0.03, 0.016, (0, -0.32, -0.078), 0.004), RUBBER)
    mag = cell_pack(b, 0.13, -0.074, 0.1, 0.019)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.19, -0.44, -0.014, 0.088, 0.023, 0.016, "full")
    finish_pts(g, (0, 1.19, 0), grip, butt, (0, 0.42, -0.04), mag)


def raijin(g):
    """Raijin : canon à double rail géant ; rails haut et bas à faces lumineuses, cadres de serrage, armature, lunette."""
    b = g.body
    h = Hull([S(-0.22, 0.024, 0.034, -0.008, 2.6), S(-0.2, 0.04, 0.058, -0.008, 3.6), S(0.04, 0.046, 0.068, -0.004, 4.2),
              S(0.2, 0.046, 0.066, 0.0, 4.2), S(0.24, 0.04, 0.056, 0.0, 3.6)])
    for u in (-0.16, -0.06, 0.06, 0.18):
        h.groove(u, 0.002, 0.004)
    h.seam(-0.15, 0.22, 0.03)
    h.slots(-0.04, 0.17, 6, -0.016, 0.018, fill=ENERGY)
    h.tslots(-0.14, -0.04, 4, 0.0, 0.036)
    h.build(b)
    plate(b, h, -0.19, -0.07, -0.05, -0.02)
    for sg in (-1, 1):
        r = Hull([S(0.22, 0.024, 0.014, sg * 0.032, 5), S(0.24, 0.026, 0.016, sg * 0.032, 5), S(1.26, 0.024, 0.014, sg * 0.03, 5),
                  S(1.3, 0.018, 0.01, sg * 0.028, 4)], PAINT, 24)
        r.slots(0.3, 1.22, 7, sg * 0.032, 0.012, fill=POLY)
        r.build(b)
        b.add(box(0.03, 1.04, 0.003, (0, 0.76, sg * 0.0175), 0.0006, 1), ENERGY)
        b.add(box(0.04, 1.02, 0.003, (0, 0.76, sg * 0.0195), 0.0006, 1), METAL)
    b.add(loft([S(0.3, 0.012, 0.012, 0, 2), S(0.33, 0.014, 0.014, 0, 2.2), S(0.37, 0.012, 0.012, 0, 2.2), S(0.39, 0.004, 0.004, 0, 2)], 16), ALLOY)
    for k in range(6):
        uu = 0.3 + k * 0.186
        for sg in (-1, 1):
            b.add(box(0.07, 0.03, 0.01, (0, uu, sg * 0.055), 0.003), ACCENT)
        for s in (-1, 1):
            b.add(box(0.012, 0.03, 0.12, (s * 0.032, uu, 0), 0.003), ACCENT)
            screw(b, uu, 0.03, s * 0.038, 0.0026, s)
    wd_sight(b, 0.0, 0.11, 0.32, 0.018, h.sz(0.0) - 0.004)
    sup = wd_vgrip(b, 0.42, -0.06, 0.08)
    grip = wd_grip(b)
    mag = cell_pack(b, 0.14, -0.08, 0.1, 0.02)
    butt = wd_stock(b, -0.21, -0.46, -0.014, 0.09, 0.024, 0.016, "full", ACCENT)
    finish_pts(g, (0, 1.3, 0), grip, butt, sup, mag, True)


def boltshooter(g):
    """Bolt Shooter : longues électrodes parallèles à groupes d'isolateurs, brides, sphères de pointe, lunette."""
    b = g.body
    h = receiver_std(b, -0.17, 0.22, 0.03, 0.046)
    for u in (-0.12, -0.02, 0.12):
        h.groove(u)
    h.seam(-0.11, 0.2, 0.016)
    h.slots(0.0, 0.17, 6, -0.016, 0.01, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.14, -0.06, -0.03, -0.008)
    hg = Hull([S(0.2, 0.032, 0.022, -0.02, 3), S(0.23, 0.034, 0.026, -0.024, 3.4), S(0.42, 0.03, 0.022, -0.024, 3.4), S(0.44, 0.024, 0.016, -0.022, 3)], PAINT, 24)
    hg.slots(0.24, 0.41, 6, -0.03, 0.01, fill=POLY)
    hg.build(b)
    for s in (-1, 1):
        x = s * 0.018
        b.add(tube(0.0065, 0.005, 0.2, 0.98, x, 0.004, 12), METAL)
        for g0 in (0.46, 0.64, 0.82):
            for k in range(4):
                uu = g0 + k * 0.014
                b.add(tube(0.012, 0.012, uu, uu + 0.007, x, 0.004, 16), PAINT)
                b.add(tube(0.0085, 0.0085, uu + 0.007, uu + 0.014, x, 0.004, 12), ACCENT)
        b.add(ball(0.009, (x, 0.985, 0.004), 14, 10), ENERGY)
    for uu in (0.24, 0.56, 0.74, 0.92):
        b.add(hprism([(uu, -0.03), (uu + 0.016, -0.03), (uu + 0.016, 0.03), (uu, 0.03)], -0.006, 0.014, 0.002), ACCENT)
        b.add(box(0.008, 0.01, 0.004, (0, uu + 0.008, 0.016), 0.001), ENERGY)
    b.add(sweep([(-0.018 + k * 0.0045, 0.99 + (0.006 if k % 2 else -0.002), 0.004 + 0.003 * (k % 3 - 1)) for k in range(9)], 0.0015, 6), ENERGY)
    wd_sight(b, 0.0, 0.088, 0.24, 0.014, h.sz(0.0) - 0.004)
    mag = cell_pack(b, 0.12, -0.064, 0.08, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.16, -0.38, -0.012, 0.068, 0.019, 0.014, "skel")
    finish_pts(g, (0, 1.0, 0.004), grip, butt, (0, 0.32, -0.05), mag)


# ───────────────────────── Artillerie plasma ─────────────────────────

def plasma_rear(b, hw=0.044, hh=0.058, mat=PAINT):
    """Boîtier d'artillerie plasma : arrière massif, noyau visible par des fentes dorsales, évents latéraux."""
    h = Hull([S(-0.17, hw * 0.5, hh * 0.5, -0.012, 2.6), S(-0.15, hw * 0.85, hh * 0.8, -0.01, 3.2), S(0.0, hw, hh, -0.004, 3.6),
              S(0.16, hw, hh * 0.95, 0.0, 3.6), S(0.2, hw * 0.9, hh * 0.85, 0.0, 3.2)], mat)
    for u in (-0.11, -0.03, 0.12):
        h.groove(u, 0.002, 0.004)
    h.seam(-0.12, 0.18, 0.022)
    h.tslots(-0.08, 0.08, 4, 0.0, 0.026, fill=ENERGY)
    h.slots(0.0, 0.1, 4, -0.026, 0.014, fill=POLY)
    h.build(b)
    plate(b, h, -0.14, -0.075, -0.042, -0.01)
    for s in (-1, 1):
        for uu in (-0.06, 0.14):
            screw(b, uu, -0.03, s * h.sx(uu, -0.03), 0.0028, s)
    return h


def plasma_barrel(b, u0, u1, r, z=0.0, mat=PAINT, mtx=None, slots=True):
    """Fût de canon plasma : gaine ronde rainurée, fenêtres lumineuses, bouche évasée à alésage lumineux."""
    br = Hull([S(u0, r * 0.85, r * 0.85, z, 2), S(u0 + 0.025, r, r, z, 2), S(u1 - 0.07, r * 0.94, r * 0.94, z, 2), S(u1 - 0.05, r * 1.06, r * 1.06, z, 2),
               S(u1, r * 1.1, r * 1.1, z, 2)], mat, 32, mtx)
    n = max(2, int((u1 - u0 - 0.1) / 0.09))
    for k in range(n):
        br.groove(u0 + 0.06 + k * (u1 - u0 - 0.14) / max(1, n - 1), 0.002, 0.004)
    if slots:
        br.slots(u0 + 0.05, u1 - 0.1, 4, z, r * 0.4, fill=ENERGY)
    br.cut(tube(r * 0.72, r * 0.72, u1 - 0.03, u1 + 0.02, 0, z, 28), (tube(r * 0.75, r * 0.75, u1 - 0.026, u1 - 0.022, 0, z, 28), ENERGY))
    br.build(b)
    m = mtx.copy() if mtx is not None else None
    b.add(ring(r * 0.72, r * 0.5, u1 - 0.026, u1 - 0.004, 0, z, 28), METAL, m)
    return br


def plasma_common(b, g, mz, sup_u, z_under, hw=0.044, hh=0.058, stock="full"):
    plasma_rear(b, hw, hh)
    path = [(0, -0.04, hh - 0.004), (0, -0.02, hh + 0.04), (0, 0.02, hh + 0.05), (0, 0.14, hh + 0.05), (0, 0.18, hh + 0.03), (0, 0.19, hh - 0.006)]
    b.add(sweep(path, 0.007, 10), ACCENT)
    b.add(tube(0.0105, 0.0105, 0.02, 0.14, 0, hh + 0.05, 14), RUBBER)
    sup = wd_vgrip(b, sup_u, z_under, 0.082)
    grip = wd_grip(b, -0.03, -0.044)
    mag = cell_pack(b, 0.1, -hh - 0.016, 0.09, 0.018)
    butt = wd_stock(b, -0.16, -0.38, -0.014, 0.084, 0.023, 0.014, stock)
    finish_pts(g, mz, grip, butt, sup, mag, True)


def plasmacannon(g):
    """Plasma Cannon : gros fût rond rainuré, anneaux de refroidissement, bouche évasée, noyau dorsal visible."""
    b = g.body
    r = 0.046
    plasma_barrel(b, 0.18, 0.6, r)
    for uu in (0.22, 0.26, 0.3):
        b.add(ring(r * 1.16, r * 0.95, uu, uu + 0.008, 0, 0, 32), ACCENT)
    for s in (-1, 1):
        b.add(box(0.006, 0.2, 0.01, (s * (r + 0.002), 0.43, -0.024), 0.002), METAL)
    spine_ = cut(prism([(0.33, r - 0.006), (0.35, r + 0.012), (0.52, r + 0.012), (0.55, r - 0.006)], -0.007, 0.007, 0.0015),
                 [box(0.03, 0.006, 0.012, (0, 0.37 + k * 0.03, r + 0.012), 0.002, 2) for k in range(5)])
    b.add(spine_, ACCENT)
    plasma_common(b, g, (0, 0.61, 0), 0.38, -r - 0.004)


def plasmafall(g):
    """Plasmafall : mortier plasma à tir plongeant ; fût incliné vers le ciel sur un berceau à charnière."""
    b = g.body
    r = 0.04
    pivot = (0, 0.14, 0.07)
    mt = M(pivot, pitch=28)
    bm = Hull([S(-0.06, r * 0.7, r * 0.7, 0, 2), S(-0.04, r * 1.05, r * 1.05, 0, 2), S(0.02, r * 1.05, r * 1.05, 0, 2), S(0.04, r * 0.9, r * 0.9, 0, 2)], ACCENT, 32, mt.copy())
    bm.groove(-0.01, 0.002, 0.004)
    bm.build(b)
    plasma_barrel(b, 0.03, 0.38, r, 0.0, PAINT, mt.copy())
    for uu in (0.12, 0.2):
        b.add(ring(r * 1.14, r * 0.95, uu, uu + 0.01, 0, 0, 32), ACCENT, mt.copy())
    for s in (-1, 1):                                                    # berceau et vérin
        b.add(prism([(0.02, 0.03), (0.24, 0.03), (0.2, 0.06), (0.16, 0.1), (0.1, 0.1)], s * 0.026, s * 0.034, 0.002), ACCENT)
        b.add(tube_x(0.016, 0.012, (s * 0.034, 0.14, 0.07), 20), METAL)
        b.add(sweep([(s * 0.03, 0.22, 0.04), (s * 0.034, 0.27, 0.07), (s * 0.034, 0.29, 0.11)], 0.006, 10), METAL)
    pl = plasma_common(b, g, None, 0.36, -0.046)
    b.add(tube(0.03, 0.028, 0.2, 0.42, 0, -0.01, 24), PAINT)                    # tube de visée inférieur
    holed_ring(b, 0.4, 0.43, 0.032, 0.016, 8, 0.004)
    tip = 0.38
    g.point("Muzzle", (0, pivot[1] + tip * math.cos(math.radians(28)), pivot[2] + tip * math.sin(math.radians(28))))


def bigcannon(g):
    """Plasma Big Cannon : très gros alésage, longue gaine, pavillon évasé, entretoises latérales."""
    b = g.body
    r = 0.062
    plasma_barrel(b, 0.18, 0.74, r)
    b.add(loft([S(0.72, r * 1.08, r * 1.08, 0, 2), S(0.77, r * 1.32, r * 1.32, 0, 2), S(0.79, r * 1.3, r * 1.3, 0, 2)], 32, False), ACCENT)
    b.add(ring(r * 1.3, r * 1.0, 0.788, 0.792, 0, 0, 32), ENERGY)
    for s in (-1, 1):
        b.add(tube(0.008, 0.008, 0.1, 0.62, s * (r + 0.012), 0.0, 12), METAL)
        for uu in (0.24, 0.44, 0.6):
            b.add(box(0.03, 0.016, 0.02, (s * (r + 0.004), uu, 0), 0.004), ACCENT)
    for uu in (0.26, 0.31, 0.36):
        b.add(ring(r * 1.12, r * 0.95, uu, uu + 0.01, 0, 0, 32), ACCENT)
    plasma_common(b, g, (0, 0.8, 0), 0.42, -r - 0.004, 0.048, 0.064)


def heavycannon(g):
    """Plasma Heavy Cannon : fût blindé carré à plaques vissées, radiateur dorsal, cadre de bouche."""
    b = g.body
    hv = Hull([S(0.16, 0.048, 0.05, 0, 5), S(0.19, 0.062, 0.062, 0, 6), S(0.62, 0.058, 0.058, 0, 6), S(0.66, 0.05, 0.05, 0, 5)], PAINT, 32)
    for u in (0.24, 0.42, 0.58):
        hv.groove(u, 0.0025, 0.005)
    hv.slots(0.27, 0.56, 6, 0.0, 0.022, fill=ENERGY)
    hv.cut(tube(0.036, 0.036, 0.6, 0.7, 0, 0, 28), (tube(0.04, 0.04, 0.62, 0.626, 0, 0, 28), ENERGY))
    hv.build(b)
    for s in (-1, 1):
        for (u0, u1) in ((0.2, 0.235), (0.43, 0.575)):
            b.add(box(0.006, u1 - u0, 0.09, (s * 0.061, (u0 + u1) / 2, 0), 0.002), ACCENT)
            for uu in (u0 + 0.008, u1 - 0.008):
                for zz in (-0.035, 0.035):
                    screw(b, uu, zz, s * 0.064, 0.0026, s)
    for k in range(9):
        uu = 0.24 + k * 0.034
        b.add(box(0.1, 0.006, 0.026, (0, uu, 0.07), 0.0015), ACCENT)
    b.add(box(0.016, 0.3, 0.012, (0, 0.375, 0.06), 0.003), METAL)
    b.add(ring(0.052, 0.036, 0.655, 0.675, 0, 0, 4), METAL, M(roll=45))
    plasma_common(b, g, (0, 0.68, 0), 0.4, -0.062, 0.05, 0.064)


def burstcannon(g):
    """Plasma Burst Cannon : deux fûts côte à côte reliés par des brides, bouches lumineuses jumelles."""
    b = g.body
    r = 0.03
    for s in (-1, 1):
        plasma_barrel(b, 0.18, 0.6, r, 0.0, PAINT, Matrix.Translation(P(s * 0.034, 0, 0)), slots=False)
    for uu in (0.24, 0.4, 0.54):
        b.add(hprism([(uu, -0.07), (uu + 0.022, -0.07), (uu + 0.022, 0.07), (uu, 0.07)], -0.022, 0.022, 0.003), ACCENT)
        b.add(box(0.012, 0.012, 0.006, (0, uu + 0.011, 0.024), 0.002), ENERGY)
    b.add(box(0.03, 0.34, 0.012, (0, 0.38, 0.026), 0.003), METAL)
    plasma_common(b, g, (0, 0.61, 0), 0.36, -0.034, 0.052, 0.058)


def grit(g):
    """Grit Shot / Grit Gun : canon plasma perforant, long et fin, radiateur à ailettes serrées, lunette."""
    b = g.body
    r = 0.026
    plasma_barrel(b, 0.18, 0.96, r)
    for k in range(14):
        uu = 0.3 + k * 0.022
        b.add(ring(0.045, r * 0.9, uu, uu + 0.004, 0, 0, 28), ALLOY)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(tube(0.004, 0.004, 0.28, 0.62, 0.042 * math.cos(a), 0.042 * math.sin(a), 8), METAL)
    wd_sight(b, 0.0, 0.1, 0.24, 0.015, 0.058)
    plasma_common(b, g, (0, 0.97, 0), 0.24, -0.046, 0.04, 0.054, "skel")


def splasher(g):
    """Splasher : bouche plate et large à cinq alésages en ligne, gaine aplatie, ailettes de déflexion."""
    b = g.body
    sp = Hull([S(0.16, 0.044, 0.04, 0, 3), S(0.2, 0.06, 0.034, 0, 3.6), S(0.48, 0.078, 0.03, 0, 4.2), S(0.5, 0.074, 0.026, 0, 4.0)], PAINT, 32)
    for u in (0.25, 0.34, 0.43):
        sp.groove(u, 0.002, 0.004)
    sp.tslots(0.24, 0.44, 5, 0.0, 0.06, fill=ENERGY)
    for k in range(5):
        sp.cut(tube(0.0095, 0.0095, 0.46, 0.54, -0.05 + k * 0.025, 0, 16))
    sp.ins.append((box(0.15, 0.006, 0.03, (0, 0.468, 0), 0), ENERGY))
    sp.build(b)
    for k in range(5):
        b.add(ring(0.012, 0.0095, 0.495, 0.502, -0.05 + k * 0.025, 0, 16), METAL)
    for s in (-1, 1):
        b.add(hprism([(0.3, s * 0.06), (0.46, s * 0.074), (0.5, s * 0.096), (0.44, s * 0.096)], -0.004, 0.004, 0.001), ACCENT)
    plasma_common(b, g, (0, 0.51, 0), 0.36, -0.034)


# ───────────────────────── Grenade sphérique ─────────────────────────

def blinkball(g):
    """Blink Ball : sphère à coque rainurée sur trois axes, hublots lumineux, goupille et anneau."""
    b = g.body
    r = 0.04
    sh = ball(r, (0, 0, 0), 28, 16)
    cuts = [ring(r + 0.01, r - 0.003, -0.002, 0.002, 0, 0, 32)]
    cuts.append(xf(ring(r + 0.01, r - 0.003, -0.002, 0.002, 0, 0, 32), Matrix.Rotation(math.radians(90), 4, 'X')))
    cuts.append(xf(ring(r + 0.01, r - 0.003, -0.002, 0.002, 0, 0, 32), Matrix.Rotation(math.radians(90), 4, 'Z')))
    for a in (45, 135, 225, 315):
        for e in (-35, 35):
            cuts.append(xf(tube(0.009, 0.009, r - 0.006, r + 0.02, 0, 0, 14), M(yaw=a, pitch=e)))
    b.add(cut(sh, cuts), PAINT)
    b.add(ball(r - 0.0045, (0, 0, 0), 20, 12), ENERGY)
    b.add(tube(0.008, 0.007, 0.0, 0.014, 0, 0, 14), METAL, M((0, 0, r - 0.003), pitch=90))
    b.add(tube(0.01, 0.01, 0.0, 0.004, 0, 0, 16), ACCENT, M((0, 0, r - 0.004), pitch=90))
    b.add(torus_u(0.0, 0.012, 0.0018, 0, 0, 20, 6), METAL, M((0.012, 0, r + 0.01), roll=0))
    for a in (0, 180):
        b.add(ring(0.012, 0.008, r - 0.002, r + 0.002, 0, 0, 16), ACCENT, M(yaw=a + 90))
    g.point("Muzzle", (0, 0.05, 0)); g.point("Grip", (0, 0, 0)); g.point("Support", (0, 0.06, 0)); g.point("Butt", (0, -0.1, 0)); g.point("Mag", (0, 0, 0))


# ───────────────────────── Armes guidées ─────────────────────────

def homing_body(b, mat=PAINT, hw=0.034, hh=0.05, u1=0.2):
    """Corps des armes guidées : boîtier ciselé, capteur de verrouillage dorsal (dôme, lentille, bande lumineuse)."""
    h = receiver_std(b, -0.15, u1, hw, hh, p=3.4, mat=mat)
    for u in (-0.1, 0.0, u1 - 0.06):
        h.groove(u)
    h.seam(-0.1, u1 - 0.03, 0.018)
    h.slots(0.02, u1 - 0.08, 4, -0.02, 0.012, fill=ENERGY)
    h.build(b)
    plate(b, h, -0.125, -0.06, -0.032, -0.008)
    top = h.sz(0.04)
    sn = Hull([S(-0.02, 0.016, 0.012, top + 0.008, 2.6), S(0.0, 0.028, 0.018, top + 0.012, 3.0), S(0.08, 0.026, 0.016, top + 0.012, 3.0),
               S(0.1, 0.02, 0.012, top + 0.008, 2.6)], ACCENT, 24)
    sn.window(0.01, 0.07, top + 0.008, top + 0.013, 0.002, ENERGY)
    sn.build(b)
    b.add(ball(0.016, (0, 0.1, top + 0.012), 18, 10), LENS)
    b.add(ring(0.018, 0.012, 0.094, 0.1, 0, top + 0.012, 20), METAL)
    return h


def homing_end(b, g, mz, sup, stock="skel"):
    mag = cell_pack(b, 0.11, -0.066, 0.08, 0.016)
    grip = wd_grip(b)
    butt = wd_stock(b, -0.14, -0.33, -0.012, 0.066, 0.019, 0.012, stock)
    finish_pts(g, mz, grip, butt, sup, mag)


def mirage(g):
    """Mirage : bloc lance-missiles arrondi à quinze alésages lumineux (3 × 5), fenêtres de charge, ailettes, capteur."""
    b = g.body
    homing_body(b)
    blk = Hull([S(0.16, 0.036, 0.034, 0, 3.0), S(0.2, 0.056, 0.046, 0, 3.6), S(0.31, 0.06, 0.048, 0, 3.8), S(0.35, 0.056, 0.044, 0, 3.6),
                S(0.37, 0.05, 0.038, 0, 3.2)], PAINT, 32)
    for u in (0.22, 0.3):
        blk.groove(u, 0.002, 0.004)
    blk.tseam(0.2, 0.35, 0.0)
    blk.tslots(0.235, 0.29, 3, 0.0, 0.06, fill=ENERGY)
    blk.window(0.235, 0.29, -0.008, 0.008, 0.003, ENERGY)
    blk.slots(0.23, 0.295, 4, -0.03, 0.01, fill=POLY)
    for i in range(5):
        for j in range(3):
            blk.cut(tube(0.0075, 0.0075, 0.33, 0.4, -0.04 + i * 0.02, -0.02 + j * 0.02, 12))
    blk.ins.append((box(0.1, 0.004, 0.06, (0, 0.345, 0), 0), ENERGY))
    blk.build(b)
    for i in range(5):
        for j in range(3):
            b.add(ring(0.0095, 0.0075, 0.362, 0.37, -0.04 + i * 0.02, -0.02 + j * 0.02, 12), METAL)
    for s in (-1, 1):
        b.add(hprism([(0.2, s * 0.05), (0.33, s * 0.056), (0.36, s * 0.08), (0.27, s * 0.08)], -0.003, 0.003, 0.001), ACCENT)
        b.add(hprism([(0.28, s * 0.07), (0.33, s * 0.072), (0.34, s * 0.077), (0.29, s * 0.077)], -0.0036, 0.0036, 0.0005), ENERGY)
        b.add(prism([(0.21, 0.04), (0.33, 0.042), (0.35, 0.056), (0.25, 0.058)], s * 0.026, s * 0.03, 0.001), ACCENT)
    homing_end(b, g, (0, 0.37, 0), (0, 0.15, -0.05))


def geist(g):
    """Geist : trois nacelles fantômes en goutte sur des bras courbes, nez lumineux."""
    b = g.body
    homing_body(b)
    b.add(loft([S(0.19, 0.026, 0.03, 0, 3), S(0.23, 0.028, 0.03, 0, 3.2), S(0.26, 0.02, 0.022, 0, 3)], 22), ACCENT)
    for k in range(3):
        m = M(roll=90 + k * 120)
        b.add(sweep(bez((0, 0.22, 0.012), (0, 0.27, 0.02), (0, 0.27, 0.05), (0, 0.31, 0.056), 10), 0.0055, 8), METAL, m.copy())
        pod = Hull([S(0.27, 0.004, 0.004, 0.058, 2), S(0.29, 0.013, 0.013, 0.058, 2.2), S(0.36, 0.016, 0.016, 0.058, 2.2), S(0.42, 0.006, 0.006, 0.058, 2)], PAINT, 18, m.copy())
        pod.groove(0.33, 0.0012, 0.002)
        pod.slots(0.3, 0.36, 2, 0.058, 0.006, fill=ENERGY)
        pod.build(b)
        b.add(ball(0.0062, (0, 0.418, 0.058), 12, 8), ENERGY, m.copy())
        b.add(prism([(0.28, 0.07), (0.31, 0.07), (0.3, 0.084), (0.285, 0.084)], -0.0015, 0.0015, 0.0005), ACCENT, m.copy())
    b.add(tube(0.008, 0.005, 0.25, 0.32, 0, 0, 12), ENERGY)
    homing_end(b, g, (0, 0.42, 0), (0, 0.15, -0.05))


def ghostchaser(g):
    """Ghost Chaser : barillet ouvert de sept tubes autour d'un noyau, brides percées, ailes de capteurs."""
    b = g.body
    homing_body(b)
    pos = [(0.034 * math.cos(2 * math.pi * k / 7), 0.034 * math.sin(2 * math.pi * k / 7)) for k in range(7)]
    for uu in (0.2, 0.42):
        b.add(cut(tube(0.05, 0.05, uu, uu + 0.022, 0, 0, 28), [tube(0.0095, 0.0095, uu - 0.01, uu + 0.04, x, z, 14) for (x, z) in pos]), ACCENT)
    for (x, z) in pos:
        b.add(tube(0.0095, 0.0095, 0.19, 0.46, x, z, 14), METAL)
        b.add(tube(0.0065, 0.0065, 0.455, 0.465, x, z, 12), ENERGY)
        b.add(ring(0.011, 0.0095, 0.3, 0.31, x, z, 14), PAINT)
    b.add(tube(0.012, 0.012, 0.19, 0.44, 0, 0, 16), PAINT)
    b.add(tube(0.008, 0.008, 0.44, 0.452, 0, 0, 14), ENERGY)
    for s in (-1, 1):
        b.add(hprism([(0.0, s * 0.03), (0.14, s * 0.03), (0.12, s * 0.07), (0.06, s * 0.074)], -0.003, 0.003, 0.001), ACCENT)
        b.add(hprism([(0.07, s * 0.06), (0.11, s * 0.058), (0.11, s * 0.064), (0.07, s * 0.068)], -0.0034, 0.0034, 0.0005), ENERGY)
    homing_end(b, g, (0, 0.47, 0), (0, 0.15, -0.05), "full")


def fenrir(g):
    """Fenrir : double tube de lancement sous des mâchoires anguleuses, yeux lumineux, coque sombre."""
    b = g.body
    homing_body(b, PAINT, 0.04, 0.054, 0.16)
    for s in (-1, 1):
        x = s * 0.032
        t = Hull([S(0.1, 0.022, 0.022, -0.008, 2.6), S(0.13, 0.029, 0.029, -0.008, 3), S(0.48, 0.028, 0.028, -0.008, 3), S(0.5, 0.025, 0.025, -0.008, 2.6)],
                 ACCENT, 24, Matrix.Translation(P(x, 0, 0)))
        for u in (0.2, 0.3, 0.4):
            t.groove(u, 0.0018, 0.0035)
        t.cut(tube(0.019, 0.019, 0.47, 0.55, 0, -0.008, 22), (tube(0.022, 0.022, 0.476, 0.48, 0, -0.008, 22), ENERGY))
        t.build(b)
    jaw = prism([(0.1, 0.02), (0.44, 0.02), (0.54, 0.0), (0.5, 0.034), (0.3, 0.05), (0.12, 0.06)], -0.05, 0.05, 0.002)
    jc = [box(0.12, 0.004, 0.012, (0, uu, 0.05 - (uu - 0.12) * 0.05), 0.0015, 2) for uu in (0.16, 0.2)]
    jc += [box(0.01, 0.12, 0.008, (s * 0.05, 0.3, 0.034), 0.003, 2) for s in (-1, 1)]
    b.add(cut(jaw, jc), PAINT)
    for s in (-1, 1):
        b.add(box(0.004, 0.11, 0.003, (s * 0.0485, 0.3, 0.034), 0.0008, 1), ENERGY)
        for k in range(3):                                               # crocs
            uu = 0.42 + k * 0.035
            b.add(prism([(uu, 0.022), (uu + 0.02, 0.022), (uu + 0.006, -0.002)], s * 0.044, s * 0.05, 0.0006), METAL)
    low = prism([(0.12, -0.035), (0.42, -0.035), (0.5, -0.02), (0.46, -0.05), (0.2, -0.056)], -0.046, 0.046, 0.002)
    b.add(cut(low, [box(0.1, 0.004, 0.01, (0, uu, -0.052), 0.0015, 2) for uu in (0.24, 0.28, 0.32, 0.36)]), PAINT)
    for k in range(4):
        uu = 0.26 + k * 0.05
        b.add(prism([(uu, 0.03), (uu + 0.03, 0.03), (uu + 0.02, 0.052 - k * 0.002), (uu + 0.004, 0.052 - k * 0.002)], -0.052, 0.052, 0.001), ACCENT)
    for s in (-1, 1):
        b.add(prism([(0.44, 0.012), (0.48, 0.008), (0.47, 0.016)], s * 0.05, s * 0.0515, 0), ENERGY)
        b.add(prism([(0.2, -0.05), (0.3, -0.06), (0.34, -0.052)], s * 0.04, s * 0.046, 0.001), ACCENT)
    homing_end(b, g, (0, 0.54, -0.008), (0, 0.15, -0.065), "full")


def psiblade(g):
    """Psiblade : émetteur en lame ; longue lame dorsale à tranchant lumineux, garde, corps effilé."""
    b = g.body
    homing_body(b)
    b.add(hprism([(0.17, -0.05), (0.2, -0.056), (0.215, -0.03), (0.215, 0.03), (0.2, 0.056), (0.17, 0.05)], -0.016, 0.016, 0.002), ACCENT)
    b.add(tube(0.016, 0.01, 0.2, 0.3, 0, 0, 18), PAINT)
    blade = [(0.21, -0.012), (0.62, 0.004), (0.6, 0.018), (0.4, 0.034), (0.22, 0.036)]
    b.add(prism(blade, -0.0035, 0.0035, 0.0008), PAINT)
    b.add(prism([(0.24, 0.032), (0.4, 0.03), (0.59, 0.017), (0.6, 0.02), (0.4, 0.036), (0.24, 0.038)], -0.0045, 0.0045, 0.0005), ENERGY)
    b.add(prism([(0.23, -0.004), (0.5, 0.0), (0.5, 0.006), (0.23, 0.01)], -0.0042, 0.0042, 0.0004), ACCENT)
    for s in (-1, 1):
        b.add(hprism([(0.2, s * 0.05), (0.26, s * 0.06), (0.24, s * 0.07), (0.2, s * 0.064)], -0.004, 0.004, 0.001), PAINT)
    homing_end(b, g, (0, 0.62, 0.004), (0, 0.15, -0.05))


# ───────────────────────── Lanceurs de sphères ─────────────────────────

def orb_base(b, g, kind, sphere_r=0.045, su=0.25, mat=ACCENT, trim=PAINT, hh=1.0):
    """Gantelet à berceau : boîtier court sombre, poignée, plateau percé ; renvoie la position de la sphère."""
    h = Hull([S(-0.13, 0.02, 0.026 * hh, -0.01, 2.6), S(-0.11, 0.034, 0.045 * hh, -0.008, 3.2), S(0.02, 0.04, 0.05 * hh, -0.004, 3.4),
              S(0.1, 0.038, 0.044 * hh, 0.0, 3.2), S(0.15, 0.032, 0.034, 0.0, 2.8)], mat)
    for u in (-0.08, 0.02, 0.11):
        h.groove(u)
    h.seam(-0.07, 0.13, 0.016)
    h.slots(-0.06, 0.08, 5, -0.02, 0.012, fill=ENERGY)
    h.tslots(-0.06, 0.06, 4, 0.0, 0.026, fill=POLY)
    h.build(b)
    plate(b, h, -0.105, -0.07, -0.034, -0.008, trim)
    b.add(prism([(-0.1, 0.03), (-0.08, 0.05 * hh + 0.006), (0.08, 0.046 * hh + 0.006), (0.12, 0.03)], -0.006, 0.006, 0.0015), trim)
    holed_ring(b, 0.15, 0.17, 0.042, 0.012, 10, 0.004, trim)
    b.add(tube(0.03, 0.03, 0.168, 0.172, 0, 0, 24), ENERGY)
    b.add(ball(sphere_r, (0, su, 0), 28, 18), GLOW if kind in ("heavensgate", "psicluster") else ENERGY)
    return (0, su, 0)


def orb_end(b, g, mz):
    mag = cell_pack(b, 0.07, -0.064, 0.08, 0.016)
    grip = wd_grip(b, -0.03, -0.04)
    finish_pts(g, mz, grip, (0, -0.14, -0.02), (0, 0.08, -0.085), mag)


def diffuser(g):
    """Diffuser : sphère tenue par trois griffes articulées."""
    b = g.body
    su = 0.25
    orb_base(b, g, "diffuser", 0.045, su)
    for k in range(3):
        m = M(roll=90 + k * 120)
        path = bez((0, 0.165, 0.03), (0, 0.2, 0.07), (0, 0.27, 0.07), (0, 0.3, 0.03), 12)
        b.add(sweep(path, 0.007, 10, 0.004), PAINT, m.copy())
        b.add(box(0.016, 0.026, 0.014, (0, 0.18, 0.04), 0.004), METAL, m.copy())
        b.add(tube_x(0.006, 0.02, (0, 0.215, 0.064), 12), METAL, m.copy())
        b.add(ball(0.005, path[-1], 10, 6), ENERGY, m.copy())
    orb_end(b, g, (0, 0.3, 0))


def gleipnir(g):
    """Gleipnir : sphère entravée par trois anneaux gyroscopiques et une chaîne de maillons."""
    b = g.body
    su = 0.25
    orb_base(b, g, "gleipnir", 0.04, su, PAINT, ACCENT, 0.9)
    for k, (yaw, pitch) in enumerate(((0, 0), (90, 0), (0, 90))):
        R = 0.052 + k * 0.006
        b.add(torus_u(0.0, R, 0.0035, 0, 0, 36, 8), METAL if k != 1 else PAINT, M((0, su, 0), yaw=yaw, pitch=pitch))
    for s in (-1, 1):
        b.add(sweep([(s * 0.03, 0.15, 0.0), (s * 0.06, 0.2, 0.0), (s * 0.063, 0.25, 0.0)], 0.0045, 8), ACCENT)
    for k in range(4):
        b.add(torus_u(0.0, 0.007, 0.0018, 0, 0, 14, 6), ALLOY, M((0, 0.17 + k * 0.011, -0.05), roll=90 * (k % 2), yaw=90))
    orb_end(b, g, (0, 0.31, 0))


def thundercluster(g):
    """Thunder Cluster : sphère cernée de six électrodes à pointe lumineuse."""
    b = g.body
    su = 0.25
    orb_base(b, g, "thundercluster", 0.04, su, ACCENT, PAINT, 0.9)
    for k in range(6):
        m = M(roll=k * 60)
        b.add(sweep([(0, 0.17, 0.034), (0, 0.22, 0.062), (0, 0.27, 0.06), (0, 0.3, 0.044)], 0.004, 8), METAL, m.copy())
        for t in (0.21, 0.24):
            b.add(tube(0.007, 0.007, t, t + 0.006, 0, 0.062, 12), PAINT, m.copy())
        b.add(ball(0.006, (0, 0.302, 0.042), 10, 6), ENERGY, m.copy())
    orb_end(b, g, (0, 0.3, 0))


def heavensgate(g):
    """Heaven's Gate : grand halo vertical à rayons tenant la sphère, ailettes de halo."""
    b = g.body
    su = 0.26
    orb_base(b, g, "heavensgate", 0.038, su, PAINT, ACCENT, 0.85)
    b.add(torus_u(su, 0.088, 0.007, 0, 0, 48, 10), PAINT)
    b.add(torus_u(su + 0.002, 0.079, 0.003, 0, 0, 48, 6), ENERGY)
    for k in range(8):
        m = M((0, su, 0), roll=k * 45)
        b.add(box(0.004, 0.006, 0.04, (0, 0, 0.06), 0.001), METAL, m.copy())
        b.add(prism([(-0.008, 0.09), (0.008, 0.09), (0.003, 0.11 + 0.008 * (k % 2)), (-0.003, 0.11 + 0.008 * (k % 2))], -0.003, 0.003, 0.0008), ACCENT, m.copy())
    for s in (-1, 1):
        b.add(sweep([(s * 0.03, 0.16, 0.0), (s * 0.07, 0.2, 0.0), (s * 0.086, su, 0.0)], 0.005, 8), ACCENT)
    orb_end(b, g, (0, 0.3, 0))


def psicluster(g):
    """Psi Cluster : grappe de quatre petites sphères dans une coupe ajourée."""
    b = g.body
    su = 0.24
    orb_base(b, g, "psicluster", 0.022, su, PAINT, ACCENT, 1.0)
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        b.add(ball(0.019, (0.03 * math.cos(a), su + 0.01, 0.03 * math.sin(a)), 20, 12), GLOW)
    cup = cut(loft([S(0.17, 0.03, 0.03, 0, 2), S(0.2, 0.056, 0.056, 0, 2), S(0.23, 0.062, 0.062, 0, 2), S(0.236, 0.06, 0.06, 0, 2)], 32),
                [loft([S(0.18, 0.026, 0.026, 0, 2), S(0.2, 0.05, 0.05, 0, 2), S(0.24, 0.057, 0.057, 0, 2)], 32)]
                + [xf(box(0.08, 0.03, 0.016, (0, 0.215, 0), 0.006, 3), M(roll=k * 45)) for k in range(4)])
    b.add(cup, PAINT)
    b.add(torus_u(0.236, 0.06, 0.003, 0, 0, 36, 6), ENERGY)
    orb_end(b, g, (0, 0.28, 0))


def finaldays(g):
    """Final Days : grande sphère sombre fissurée de lumière, couronne de huit griffes recourbées."""
    b = g.body
    su = 0.26
    orb_base(b, g, "finaldays", 0.03, su, PAINT, ACCENT, 1.1)
    sh = ball(0.05, (0, su, 0), 28, 16)
    cuts = []
    for k in range(5):
        cuts.append(xf(ring(0.062, 0.046, -0.0018, 0.0018, 0, 0, 32), M((0, su, 0), yaw=k * 36 + 10, pitch=70)))
    b.add(cut(sh, cuts), ACCENT)
    b.add(ball(0.0465, (0, su, 0), 24, 14), ENERGY)
    for k in range(8):
        m = M(roll=k * 45)
        path = bez((0, 0.17, 0.034), (0, 0.22, 0.09), (0, 0.3, 0.08), (0, 0.33, 0.02), 12)
        b.add(sweep(path, 0.0055, 8, 0.0018), PAINT, m.copy())
    orb_end(b, g, (0, 0.33, 0))


# ───────────────────────── Couleurs, marquages, construction ─────────────────────────

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

LABELS = {"rapier": "RAPIER", "phalanx": "PHALANX", "powerlance": "POWER LANCE", "dragoon": "DRAGOON LANCE",
          "monsterlance": "MONSTER LANCE", "sparkvine": "SPARK VINE", "sparkwhip": "SPARK WHIP", "magblaster": "MAG BLASTER",
          "destroyer": "DESTROYER", "spine": "SPINE BLASTER", "reflectron": "REFLECTRON", "lightningbow": "LIGHTNING BOW",
          "boltgun": "BOLT GUN", "crossbow": "THUNDER CROSSBOW", "pulsemg": "PULSE MG", "stardust": "STARDUST",
          "pulsebuster": "PULSE BUSTER", "closedlaser": "CLOSED LASER", "monster": "MONSTER", "raijin": "RAIJIN",
          "boltshooter": "BOLT SHOOTER", "plasmacannon": "PLASMA CANNON", "plasmafall": "PLASMAFALL", "bigcannon": "BIG CANNON",
          "heavycannon": "HEAVY CANNON", "burstcannon": "BURST CANNON", "grit": "GRIT SHOT", "splasher": "SPLASHER",
          "mirage": "MIRAGE", "geist": "GEIST", "ghostchaser": "GHOST CHASER", "fenrir": "FENRIR", "psiblade": "PSIBLADE",
          "diffuser": "DIFFUSER", "gleipnir": "GLEIPNIR", "thundercluster": "THUNDER CLUSTER", "heavensgate": "HEAVEN'S GATE",
          "psicluster": "PSI CLUSTER", "finaldays": "FINAL DAYS"}
MARK_AT = {"blinkball": None}

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
