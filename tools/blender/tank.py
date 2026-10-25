# Char de combat moderne de Rempart, modélisé par script dans Blender (exécuté via le serveur MCP de Blender ou :
#   blender --background --python tools/blender/tank.py -- assets/models/tank.glb).
# Repère Blender : avant du char vers -Y, haut +Z (glTF : avant +Z, haut +Y, comme le char dans Godot).
# Objets nommés lus par scripts/Player/Tank.cs :
#   Hull (caisse, jupes), Track_L / Track_R (bandes de chenilles, UV v = abscisse le long de la bande en mètres),
#   RoadWheel_L_n, Sprocket_L, Idler_L, Roller_L_n (et _R) : roues qui tournent (rayon lu dans l'AABB),
#   Turret (pivot = couronne), Gun (pivot = tourillons), Tube (fût qui recule au tir), Muzzle (vide, bouche du canon).
# Matériaux : Paint, Accent, Dark, Rubber, Track, Glass, Lamp (remplacés côté Godot par les matières du jeu).
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

MATS = ["Paint", "Accent", "Dark", "Rubber", "Track", "Glass", "Lamp"]
PAINT, ACCENT, DARK, RUBBER, TRACK, GLASS, LAMP = range(7)


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def materials():
    cols = {"Paint": (0.20, 0.23, 0.26), "Accent": (0.42, 0.42, 0.22), "Dark": (0.06, 0.06, 0.06), "Rubber": (0.03, 0.03, 0.03),
            "Track": (0.05, 0.05, 0.05), "Glass": (0.05, 0.08, 0.1), "Lamp": (0.9, 0.85, 0.7)}
    out = []
    for n in MATS:
        m = bpy.data.materials.new(n)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*cols[n], 1)
            bsdf.inputs["Roughness"].default_value = 0.6
        out.append(m)
    return out


class Part:
    """Géométrie d'un objet, accumulée pièce par pièce (index de matériau par face)."""

    def __init__(self):
        self.bm = bmesh.new()

    def add(self, bm, mat, mtx=Matrix.Identity(4)):
        bmesh.ops.transform(bm, matrix=mtx, verts=bm.verts)
        for f in bm.faces:
            f.material_index = mat
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me)
        bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def obj(self, name, mats, loc=(0, 0, 0), parent=None, smooth_angle=None):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in mats:
            me.materials.append(m)
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        # La géométrie est construite dans le repère du parent : on la recentre sur le pivot.
        me.transform(Matrix.Translation(-Vector(loc)))
        o.location = loc
        if parent:
            o.parent = parent
            o.location = Vector(loc) - parent_world(parent)
        if smooth_angle is not None:
            for p in me.polygons:
                p.use_smooth = True
            try:
                me.set_sharp_from_angle(angle=math.radians(smooth_angle))
            except Exception:
                pass
        return o


def parent_world(p):
    loc = Vector((0, 0, 0))
    while p is not None:
        loc += p.location
        p = p.parent
    return loc


def box(size, center, bevel=0.02):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=1, affect='EDGES', profile=0.5)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return bm


def cyl(r1, r2, depth, center, axis='Z', seg=24, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=depth)
    if axis == 'X':
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'), verts=bm.verts)
    elif axis == 'Y':
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-90), 3, 'X'), verts=bm.verts)
    if rot is not None:
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return bm


def prism_x(profile, x0, x1):
    """Profil (y, z) extrudé entre x0 et x1 (caisse vue de côté)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile]
    b = [bm.verts.new((x1, y, z)) for y, z in profile]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def prism_z(poly, z0, z1, top_scale=1.0, top_shift=(0, 0)):
    """Contour vu de dessus (x, y) extrudé de z0 à z1, le dessus réduit (pans inclinés de tourelle)."""
    bm = bmesh.new()
    cx = sum(p[0] for p in poly) / len(poly)
    cy = sum(p[1] for p in poly) / len(poly)
    a = [bm.verts.new((x, y, z0)) for x, y in poly]
    b = [bm.verts.new((cx + (x - cx) * top_scale + top_shift[0], cy + (y - cy) * top_scale + top_shift[1], z1)) for x, y in poly]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def grille(part, center, size, n, mat_frame, mat_slat, axis='Z'):
    """Grille d'aération : cadre et lames."""
    sx, sy, sz = size
    part.add(box((sx, sy, sz * 0.4), (center[0], center[1], center[2] - sz * 0.3), 0.01), mat_frame)
    for i in range(n):
        t = (i + 0.5) / n - 0.5
        part.add(box((sx * 0.92, sy / n * 0.45, sz), (center[0], center[1] + t * sy * 0.92, center[2]), 0.005), mat_slat)


# ---------------------------------------------------------------- chenilles

RW_R, RW_Z = 0.36, 0.49
RW_Y = [-2.55 + i * 0.85 for i in range(7)]
IDLER = (-3.22, 0.66, 0.31)
SPROCKET = (3.22, 0.70, 0.34)
ROLLERS = [(-1.7, 0.98, 0.1), (0.0, 0.98, 0.1), (1.7, 0.98, 0.1)]
TRACK_T = 0.13     # épaisseur de la bande
TRACK_X = 1.43     # axe de la chenille
TRACK_W = 0.72


def track_loop():
    """Contour intérieur de la bande : enveloppe convexe des roues (tendeuse, galets extrêmes, barbotin) + brins droits."""
    circles = [IDLER, (RW_Y[0], RW_Z, RW_R), (RW_Y[-1], RW_Z, RW_R), SPROCKET]
    cy = sum(c[0] for c in circles) / 4
    cz = sum(c[1] for c in circles) / 4
    n = len(circles)
    tangents = []
    for i in range(n):
        a, b = circles[i], circles[(i + 1) % n]
        A, B = Vector((a[0], a[1])), Vector((b[0], b[1]))
        d = B - A
        L = d.length
        d /= L
        phi = math.acos(max(-1, min(1, (a[2] - b[2]) / L)))
        best = None
        for s in (1, -1):
            c, sn = math.cos(s * phi), math.sin(s * phi)
            nrm = Vector((d.x * c - d.y * sn, d.x * sn + d.y * c))
            pa, pb = A + nrm * a[2], B + nrm * b[2]
            mid = (pa + pb) / 2
            out = (mid - Vector((cy, cz))).dot(nrm)
            if best is None or out > best[0]:
                best = (out, pa, pb, nrm)
        tangents.append(best)
    pts = []
    for i in range(n):
        # Arc sur le cercle i, du point d'arrivée (tangente i-1) au point de départ (tangente i).
        c = circles[i]
        C = Vector((c[0], c[1]))
        pin = tangents[i - 1][2]
        pout = tangents[i][1]
        a0 = math.atan2(pin.y - C.y, pin.x - C.x)
        a1 = math.atan2(pout.y - C.y, pout.x - C.x)
        # Sens : on garde l'arc dont le milieu est le plus loin du centre de l'enveloppe.
        best = None
        for sgn in (1, -1):
            da = (a1 - a0) % (2 * math.pi) if sgn > 0 else -((a0 - a1) % (2 * math.pi))
            am = a0 + da / 2
            m = C + Vector((math.cos(am), math.sin(am))) * c[2]
            dist = (m - Vector((cy, cz))).length
            if best is None or dist > best[0]:
                best = (dist, da)
        da = best[1]
        k = max(2, int(abs(da) / math.radians(12)))
        for j in range(k + 1):
            ang = a0 + da * j / k
            pts.append(C + Vector((math.cos(ang), math.sin(ang))) * c[2])
    # Ré-échantillonnage régulier (≈ 3 cm) : assez fin pour le relief des crampons, déplacé par le shader.
    out = []
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        L = (q - p).length
        k = max(1, int(L / 0.03))
        for j in range(k):
            out.append(p.lerp(q, j / k))
    return out


def track_mesh(side):
    loop = track_loop()
    n = len(loop)
    # Normales sortantes (vers l'extérieur de la boucle).
    cy = sum(p.x for p in loop) / n
    cz = sum(p.y for p in loop) / n
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    # Seconde UV : normale sortante de la bande dans le repère glTF (y, z) sur les arêtes extérieures, (0, 0) à l'intérieur
    # (le shader de Godot y déplace les sommets selon le motif des crampons qui défile).
    uv2 = bm.loops.layers.uv.new("UVNormal")
    x0, x1 = side * (TRACK_X - TRACK_W / 2), side * (TRACK_X + TRACK_W / 2)
    nrms = []
    rings = []
    s = 0.0
    lens = []
    for i in range(n):
        p, q = loop[i - 1], loop[(i + 1) % n]
        t = (q - p).normalized()
        nrm = Vector((t.y, -t.x))
        if nrm.dot(loop[i] - Vector((cy, cz))) < 0:
            nrm = -nrm
        inner = loop[i]
        outer = inner + nrm * TRACK_T
        nrms.append(nrm)
        # Section : bande (intérieur, extérieur) + crampons simulés par le shader.
        r = [bm.verts.new((x0, inner.x, inner.y)), bm.verts.new((x1, inner.x, inner.y)),
             bm.verts.new((x1, outer.x, outer.y)), bm.verts.new((x0, outer.x, outer.y))]
        rings.append(r)
        if i > 0:
            s += (loop[i] - loop[i - 1]).length
        lens.append(s)
    total = s + (loop[0] - loop[-1]).length
    for i in range(n):
        j = (i + 1) % n
        v0, v1 = lens[i], (lens[j] if j > 0 else total)
        for k in range(4):
            kk = (k + 1) % 4
            f = bm.faces.new((rings[i][k], rings[j][k], rings[j][kk], rings[i][kk]))
            # u : 0..1 autour de la section (dessus de bande = face extérieure k=2), v : mètres le long de la bande.
            us = [k / 4, k / 4, (k + 1) / 4, (k + 1) / 4]
            vs = [v0, v1, v1, v0]
            ring = [i, j, j, i]
            corner = [k, k, kk, kk]
            for l, uu, vv, rr, cc in zip(f.loops, us, vs, ring, corner):
                l[uv].uv = (uu, vv)
                n2 = nrms[rr]
                outer_v = 1.0 if cc in (2, 3) else 0.0
                # Blender (0, n.x, n.y) -> glTF (0, n.y, -n.x)
                l[uv2].uv = (n2.y * outer_v, -n2.x * outer_v)
            f.material_index = TRACK
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, total


def road_wheel(part, x, y, z, r, side):
    w = 0.27
    for k in (-1, 1):
        cx = x + side * k * 0.17
        part.add(cyl(r, r, w * 0.85, (cx, y, z), 'X', 28), RUBBER)                    # bandage caoutchouc
        part.add(cyl(r * 0.82, r * 0.82, w * 0.9, (cx, y, z), 'X', 28), DARK)         # jante
        part.add(cyl(r * 0.36, r * 0.36, w * 1.05, (cx, y, z), 'X', 16), PAINT)       # moyeu
        for b in range(6):
            a = b / 6 * math.tau
            part.add(cyl(0.022, 0.022, w * 1.12, (cx, y + math.cos(a) * r * 0.24, z + math.sin(a) * r * 0.24), 'X', 6), DARK)


def sprocket(part, x, y, z, r, side):
    for k in (-1, 1):
        cx = x + side * k * 0.17
        part.add(cyl(r * 0.88, r * 0.88, 0.1, (cx, y, z), 'X', 24), DARK)
        for t in range(13):
            a = t / 13 * math.tau
            m = Matrix.Rotation(a, 4, 'X')
            tooth = box((0.1, 0.09, 0.12), (0, 0, r * 0.93), 0.01)
            bmesh.ops.transform(tooth, matrix=m, verts=tooth.verts)
            bmesh.ops.translate(tooth, vec=Vector((cx, y, z)), verts=tooth.verts)
            part.add(tooth, DARK)
    part.add(cyl(r * 0.45, r * 0.55, 0.5, (x + side * 0.05, y, z), 'X', 18), PAINT)   # carter de réducteur
    part.add(cyl(r * 0.2, r * 0.2, 0.62, (x + side * 0.05, y, z), 'X', 12), DARK)


# ---------------------------------------------------------------- caisse

def build_hull(mats, root):
    hull = Part()
    # Caisse basse (entre les chenilles), glacis inférieur incliné.
    hull.add(prism_x([(-3.62, 0.98), (-3.28, 0.42), (3.45, 0.42), (3.66, 0.78), (3.66, 1.16), (-3.62, 1.16)], -1.08, 1.08), PAINT)
    # Caisse haute (au-dessus des chenilles) : long glacis peu incliné, plage arrière.
    up = [(-3.78, 1.08), (-3.78, 1.16), (-2.55, 1.52), (3.55, 1.52), (3.78, 1.36), (3.78, 1.08)]
    hull.add(prism_x(up, -1.80, 1.80), PAINT)
    # Plaques de blindage additionnel sur le glacis (relief).
    for s in (-1, 1):
        hull.add(box((1.25, 0.9, 0.06), (s * 0.95, -3.05, 1.33), 0.015), PAINT,
                 Matrix.Translation((s * 0.95, -3.05, 1.33)) @ Matrix.Rotation(math.radians(-16.5), 4, 'X') @ Matrix.Translation((-s * 0.95, 3.05, -1.33)))
    # Poste de pilotage : épiscopes et trappe.
    hull.add(cyl(0.34, 0.34, 0.05, (0.62, -2.35, 1.5), 'Z', 20), PAINT)
    for i in range(3):
        hull.add(box((0.16, 0.08, 0.07), (0.2 + i * 0.2, -2.52, 1.5), 0.01), GLASS)
    # Plage moteur : grilles d'aération (accent) et caisson d'échappement.
    grille(hull, (-0.75, 2.45, 1.55), (1.05, 1.5, 0.05), 10, DARK, ACCENT)
    grille(hull, (0.75, 2.45, 1.55), (1.05, 1.5, 0.05), 10, DARK, ACCENT)
    hull.add(box((1.6, 0.5, 0.16), (0, 3.45, 1.55), 0.02), DARK)
    for s in (-1, 1):
        grille(hull, (s * 1.25, 3.79, 1.25), (0.7, 0.04, 0.28), 6, DARK, DARK)
    # Garde-boue et caissons latéraux de rangement.
    for s in (-1, 1):
        hull.add(box((0.5, 0.42, 0.05), (s * 1.53, -3.66, 1.12), 0.01), PAINT)
        hull.add(box((0.5, 0.35, 0.05), (s * 1.53, 3.7, 1.12), 0.01), PAINT)
        for i, y in enumerate([0.2, 1.3, 2.4]):
            hull.add(box((0.42, 0.95, 0.24), (s * 1.55, y, 1.66), 0.03), PAINT)
            hull.add(box((0.44, 0.05, 0.05), (s * 1.55, y - 0.3, 1.79), 0.01), DARK)
    # Phares avant, feux arrière, crochets de remorquage.
    for s in (-1, 1):
        hull.add(box((0.32, 0.14, 0.18), (s * 1.38, -3.72, 1.28), 0.02), DARK)
        hull.add(cyl(0.06, 0.06, 0.03, (s * 1.3, -3.8, 1.29), 'Y', 12), LAMP)
        hull.add(cyl(0.06, 0.06, 0.03, (s * 1.46, -3.8, 1.29), 'Y', 12), LAMP)
        hull.add(box((0.22, 0.06, 0.12), (s * 1.45, 3.8, 1.3), 0.01), LAMP)
        hull.add(box((0.12, 0.25, 0.1), (s * 0.7, -3.62, 0.9), 0.02), DARK)
        hull.add(box((0.12, 0.25, 0.1), (s * 0.7, 3.62, 0.85), 0.02), DARK)
    # Jupes latérales blindées : 6 panneaux (le premier, plus épais), bord inférieur caoutchouc.
    for s in (-1, 1):
        ys = [(-3.75, -2.75), (-2.7, -1.5), (-1.45, -0.25), (-0.2, 1.0), (1.05, 2.25), (2.3, 3.6)]
        for i, (a, b) in enumerate(ys):
            th = 0.14 if i == 0 else 0.07
            hull.add(box((th, b - a, 0.52), (s * (1.83 + th / 2), (a + b) / 2, 0.92), 0.015), PAINT)
            for k in range(3):
                hull.add(cyl(0.025, 0.025, 0.02, (s * (1.83 + th + 0.005), a + (k + 0.5) * (b - a) / 3, 1.1), 'X', 8), DARK)
        hull.add(box((0.05, 7.3, 0.12), (s * 1.86, -0.05, 0.62), 0.01), RUBBER)
    # Roues et rouleaux porteurs (objets à part : ils tournent).
    hull_o = hull.obj("Hull", mats, (0, 0, 0), root, smooth_angle=30)
    for side, tag in ((-1, "L"), (1, "R")):
        x = side * TRACK_X
        for i, y in enumerate(RW_Y):
            p = Part()
            road_wheel(p, x, y, RW_Z, RW_R, side)
            p.obj(f"RoadWheel_{tag}_{i}", mats, (x, y, RW_Z), root, smooth_angle=35)
        p = Part()
        road_wheel(p, x, IDLER[0], IDLER[1], IDLER[2], side)
        p.obj(f"Idler_{tag}", mats, (x, IDLER[0], IDLER[1]), root, smooth_angle=35)
        p = Part()
        sprocket(p, x, SPROCKET[0], SPROCKET[1], SPROCKET[2], side)
        p.obj(f"Sprocket_{tag}", mats, (x, SPROCKET[0], SPROCKET[1]), root, smooth_angle=35)
        for i, (y, z, r) in enumerate(ROLLERS):
            p = Part()
            p.add(cyl(r, r, 0.42, (x, y, z), 'X', 14), RUBBER)
            p.add(cyl(r * 0.6, r * 0.6, 0.46, (x, y, z), 'X', 10), DARK)
            p.obj(f"Roller_{tag}_{i}", mats, (x, y, z), root, smooth_angle=35)
        # Bras de suspension (fixes) sous la caisse.
        susp = Part()
        for y in RW_Y:
            susp.add(box((0.14, 0.5, 0.1), (side * 1.13, y + 0.22, RW_Z + 0.08), 0.02), DARK)
        susp.obj(f"Suspension_{tag}", mats, (0, 0, 0), root)
        bm, total = track_mesh(side)
        t = Part()
        t.add(bm, TRACK)
        o = t.obj(f"Track_{tag}", mats, (0, 0, 0), root)
    return hull_o


# ---------------------------------------------------------------- tourelle et canon

TURRET_POS = (0, 0.25, 1.52)
TRUNNION = (0, -1.55, 0.5)   # dans le repère de la tourelle


def build_turret(mats, root):
    t = Part()
    tx, ty, tz = TURRET_POS
    # Coque de tourelle : coins en flèche (blindage en coin), joues, nuque (panier arrière à part).
    poly = [(-0.5, -1.72), (-1.08, -2.36), (-1.72, -1.42), (-1.76, 1.25), (-1.48, 2.2), (1.48, 2.2), (1.76, 1.25), (1.72, -1.42), (1.08, -2.36), (0.5, -1.72)]
    t.add(prism_z([(x + tx, y + ty) for x, y in poly], tz, tz + 0.82, 0.95, (0, 0.03)), PAINT)
    # Collerette basse (couronne) et sous-face.
    t.add(cyl(1.25, 1.35, 0.14, (tx, ty, tz + 0.02), 'Z', 32), DARK)
    # Panneaux d'accent sur les coins avant (rappel de la référence) : grilles inclinées.
    for s in (-1, 1):
        g = Part()
        grille(g, (0, 0, 0), (0.55, 0.62, 0.06), 6, DARK, ACCENT)
        m = Matrix.Translation((tx + s * 1.18, ty - 1.62, tz + 0.86)) @ Matrix.Rotation(math.radians(s * 32), 4, 'Z')
        bm = g.bm
        g.bm = bmesh.new()
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me)
        t.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        bm.free()
        g.bm.free()
    # Caissons latéraux de rangement le long des flancs.
    for s in (-1, 1):
        t.add(box((0.3, 1.6, 0.48), (tx + s * 1.86, ty + 0.55, tz + 0.36), 0.03), PAINT)
        t.add(box((0.32, 0.04, 0.4), (tx + s * 1.86, ty + 0.05, tz + 0.36), 0.005), DARK)
        t.add(box((0.32, 0.04, 0.4), (tx + s * 1.86, ty + 1.05, tz + 0.36), 0.005), DARK)
    # Panier arrière (barres).
    by0, by1 = ty + 2.18, ty + 2.75
    for s in (-1, 1):
        t.add(box((0.05, by1 - by0, 0.05), (tx + s * 1.35, (by0 + by1) / 2, tz + 0.15), 0.0), DARK)
        t.add(box((0.05, by1 - by0, 0.05), (tx + s * 1.35, (by0 + by1) / 2, tz + 0.6), 0.0), DARK)
    for i in range(6):
        x = tx - 1.35 + i * 0.54
        t.add(box((0.04, 0.04, 0.5), (x, by1, tz + 0.38), 0.0), DARK)
    t.add(box((2.75, 0.05, 0.05), (tx, by1, tz + 0.6), 0.0), DARK)
    t.add(box((2.75, 0.05, 0.05), (tx, by1, tz + 0.15), 0.0), DARK)
    t.add(box((2.6, 0.5, 0.32), (tx, by0 + 0.28, tz + 0.32), 0.04), ACCENT)   # bâche roulée
    # Viseur panoramique du chef (tête orientable), viseur du tireur, trappes.
    t.add(cyl(0.2, 0.22, 0.42, (tx + 0.72, ty - 0.35, tz + 1.0), 'Z', 18), PAINT)
    t.add(box((0.42, 0.36, 0.3), (tx + 0.72, ty - 0.38, tz + 1.32), 0.04), DARK)
    t.add(box((0.3, 0.04, 0.18), (tx + 0.72, ty - 0.57, tz + 1.32), 0.01), GLASS)
    t.add(box((0.5, 0.42, 0.3), (tx - 0.82, ty - 1.25, tz + 0.95), 0.04), PAINT)
    t.add(box((0.34, 0.04, 0.18), (tx - 0.82, ty - 1.47, tz + 0.97), 0.01), GLASS)
    t.add(cyl(0.38, 0.38, 0.07, (tx + 0.72, ty + 0.5, tz + 0.84), 'Z', 22), PAINT)
    t.add(cyl(0.34, 0.34, 0.07, (tx - 0.7, ty + 0.45, tz + 0.84), 'Z', 22), PAINT)
    # Tourelleau téléopéré (mitrailleuse) sur la trappe du chef.
    t.add(box((0.5, 0.6, 0.32), (tx + 0.72, ty + 0.55, tz + 1.05), 0.04), DARK)
    t.add(cyl(0.035, 0.035, 0.9, (tx + 0.82, ty + 0.0, tz + 1.1), 'Y', 10), DARK)
    t.add(box((0.2, 0.2, 0.18), (tx + 0.55, ty + 0.4, tz + 1.12), 0.02), DARK)
    # Lance-pots fumigènes : deux batteries de quatre tubes.
    for s in (-1, 1):
        for i in range(4):
            a = math.radians(-35 + i * 14)
            rot = Matrix.Rotation(math.radians(-40), 3, 'X') @ Matrix.Rotation(s * a, 3, 'Z')
            t.add(cyl(0.065, 0.065, 0.32, (0, 0, 0), 'Y', 12, rot), DARK, Matrix.Translation((tx + s * 1.8, ty - 1.05 + i * 0.17, tz + 0.58)))
    # Antennes.
    for s in (-1, 1):
        t.add(cyl(0.05, 0.05, 0.12, (tx + s * 1.2, ty + 1.9, tz + 0.88), 'Z', 8), DARK)
        t.add(cyl(0.012, 0.006, 2.2, (tx + s * 1.2, ty + 1.9, tz + 2.0), 'Z', 6), DARK)
    turret = t.obj("Turret", mats, (tx, ty, tz), root, smooth_angle=30)

    # Canon : masque (pivot aux tourillons), fût à part (recul).
    gx, gy, gz = tx + TRUNNION[0], ty + TRUNNION[1], tz + TRUNNION[2]
    g = Part()
    g.add(box((0.86, 0.62, 0.56), (gx, gy - 0.05, gz), 0.05), PAINT)
    g.add(box((0.5, 0.2, 0.42), (gx, gy - 0.42, gz), 0.03), DARK)
    g.add(box((0.2, 0.2, 0.2), (gx - 0.36, gy - 0.4, gz + 0.06), 0.02), DARK)     # mitrailleuse coaxiale
    g.add(cyl(0.03, 0.03, 0.3, (gx - 0.36, gy - 0.6, gz + 0.06), 'Y', 8), DARK)
    gun = g.obj("Gun", mats, (gx, gy, gz), turret, smooth_angle=30)
    tube = Part()
    L = 5.3
    tube.add(cyl(0.135, 0.105, L, (gx, gy - 0.5 - L / 2, gz), 'Y', 24), PAINT)
    for k in range(4):                                                          # manchon thermique segmenté
        tube.add(cyl(0.15, 0.15, 0.05, (gx, gy - 0.9 - k * 0.8, gz), 'Y', 24), DARK)
    tube.add(cyl(0.17, 0.17, 0.72, (gx, gy - 3.5, gz), 'Y', 24), PAINT)         # évacuateur de fumées
    tube.add(cyl(0.155, 0.17, 0.12, (gx, gy - 3.05, gz), 'Y', 24), PAINT)
    tube.add(cyl(0.17, 0.155, 0.12, (gx, gy - 3.95, gz), 'Y', 24), PAINT)
    tube.add(cyl(0.125, 0.125, 0.18, (gx, gy - 0.5 - L + 0.09, gz), 'Y', 24), DARK)   # frein de bouche
    tube.add(box((0.08, 0.1, 0.06), (gx, gy - 0.5 - L + 0.25, gz + 0.14), 0.01), DARK)  # capteur de bouche
    tube_o = tube.obj("Tube", mats, (gx, gy, gz), gun, smooth_angle=40)
    muzzle = bpy.data.objects.new("Muzzle", None)
    bpy.context.collection.objects.link(muzzle)
    muzzle.parent = tube_o
    muzzle.location = (0, -0.5 - L - 0.05, 0)
    return turret


def main(out):
    clear()
    mats = materials()
    root = bpy.data.objects.new("Tank", None)
    bpy.context.collection.objects.link(root)
    build_hull(mats, root)
    build_turret(mats, root)
    if out:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for o in bpy.data.objects:
            o.select_set(True)
        bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True, export_apply=True,
                                  export_yup=True, export_texcoords=True, export_normals=True, export_materials='EXPORT')
    tris = sum(len(p.vertices) - 2 for o in bpy.data.objects if o.type == 'MESH' for p in o.data.polygons)
    return {"objects": len(bpy.data.objects), "tris": tris, "track_len": track_loop and round(sum((track_loop()[i] - track_loop()[i - 1]).length for i in range(len(track_loop()))), 3)}


OUT = globals().get("OUT")
if OUT is None and "--" in sys.argv:
    OUT = sys.argv[sys.argv.index("--") + 1]
result = main(OUT)
