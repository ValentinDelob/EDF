# Hélicoptère d'attaque de Rempart, modélisé par script dans Blender (exécuté via le serveur MCP de Blender ou :
#   blender --background --python tools/blender/helicopter.py -- assets/models/helicopter.glb).
# Repère Blender : avant vers -Y, haut +Z, sol à z = 0 (glTF : avant +Z, haut +Y, comme l'hélicoptère dans Godot).
# Objets nommés (lus par scripts/Player/Helicopter.cs) :
#   Body (fuselage, ailettes, nacelles, train, dérive, radôme), Rotor (moyeu + pales, pivot sur le mât),
#   TailRotor (pivot sur l'axe, côté droit), Gun (tourelle de menton, pivot en lacet) > Cannon (pivot en site) >
#   Barrels (canon rotatif à trois tubes, tourne sur son axe) > GunMuzzle,
#   Pod_L / Pod_R (paniers de roquettes, pivot sur le pylône) > PodMuzzle_L / PodMuzzle_R.
# Matériaux : Paint, PaintDark, Dark, Rubber, Glass, Lamp, LampRed, LampGreen, Blade.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

MATS = ["Paint", "PaintDark", "Dark", "Rubber", "Glass", "Lamp", "LampRed", "LampGreen", "Blade"]
PAINT, PAINT2, DARK, RUBBER, GLASS, LAMP, LRED, LGREEN, BLADE = range(9)


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def materials():
    cols = {"Paint": ((0.15, 0.165, 0.13), 0.55, 0.15), "PaintDark": ((0.1, 0.11, 0.09), 0.6, 0.15),
            "Dark": ((0.05, 0.05, 0.05), 0.45, 0.6), "Rubber": ((0.025, 0.025, 0.025), 0.9, 0.0),
            "Glass": ((0.02, 0.035, 0.04), 0.05, 0.6), "Lamp": ((0.95, 0.9, 0.75), 0.3, 0.0),
            "LampRed": ((0.9, 0.08, 0.05), 0.3, 0.0), "LampGreen": ((0.1, 0.85, 0.2), 0.3, 0.0),
            "Blade": ((0.07, 0.072, 0.068), 0.7, 0.2)}
    out = []
    for n in MATS:
        c, r, mt = cols[n]
        m = bpy.data.materials.new(n)
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = (*c, 1)
            b.inputs["Roughness"].default_value = r
            b.inputs["Metallic"].default_value = mt
            if n.startswith("Lamp"):
                b.inputs["Emission Color"].default_value = (*c, 1)
                b.inputs["Emission Strength"].default_value = 3.0
        out.append(m)
    return out


class Part:
    def __init__(self):
        self.bm = bmesh.new()

    def add(self, bm, mat, mtx=None):
        if mtx is not None:
            bmesh.ops.transform(bm, matrix=mtx, verts=bm.verts)
        for f in bm.faces:
            f.material_index = mat
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me)
        bm.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def obj(self, name, mats, pivot=(0, 0, 0), parent=None, smooth=None):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in mats:
            me.materials.append(m)
        me.transform(Matrix.Translation(-Vector(pivot)))
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        o.location = pivot
        if parent:
            o.parent = parent
            o.location = Vector(pivot) - world_loc(parent)
        if smooth is not None:
            for p in me.polygons:
                p.use_smooth = True
            try:
                me.set_sharp_from_angle(angle=math.radians(smooth))
            except Exception:
                pass
        return o


def world_loc(p):
    loc = Vector((0, 0, 0))
    while p is not None:
        loc += p.location
        p = p.parent
    return loc


def box(size, center, bevel=0.02, seg=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=seg, affect='EDGES', profile=0.5)
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


def sphere(r, center, scale=(1, 1, 1), seg=24, rings=12):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts)
    return bm


def superellipse(t, p):
    c, s = math.cos(t), math.sin(t)
    return (math.copysign(abs(c) ** (2 / p), c), math.copysign(abs(s) ** (2 / p), s))


def loft(sections, n=40, cap_start=True, cap_end=True, axis='Y'):
    """Surface lissée à partir de sections (pos, cx, cz, demi-largeur, demi-hauteur dessus, demi-hauteur dessous, exposant)."""
    bm = bmesh.new()
    rings = []
    for (pos, cx, cz, w, ht, hb, p) in sections:
        ring = []
        for i in range(n):
            t = i / n * math.tau
            ex, ez = superellipse(t, p)
            h = ht if ez > 0 else hb
            x, z = cx + ex * w, cz + ez * h
            ring.append(bm.verts.new((x, pos, z) if axis == 'Y' else (pos, x, z)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_start:
        c = bm.verts.new(sum((v.co for v in rings[0]), Vector()) / n)
        for i in range(n):
            bm.faces.new((rings[0][(i + 1) % n], rings[0][i], c))
    if cap_end:
        c = bm.verts.new(sum((v.co for v in rings[-1]), Vector()) / n)
        for i in range(n):
            bm.faces.new((rings[-1][i], rings[-1][(i + 1) % n], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def poly_prism(points, thick, axis_normal, center_offset=0.0):
    """Plaque plane : contour (liste de Vector 3D, plan quelconque) épaissie selon la normale donnée."""
    bm = bmesh.new()
    n = Vector(axis_normal).normalized()
    a = [bm.verts.new(Vector(p) + n * (center_offset - thick / 2)) for p in points]
    b = [bm.verts.new(Vector(p) + n * (center_offset + thick / 2)) for p in points]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    k = len(points)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rot_about(m3, pivot):
    return Matrix.Translation(pivot) @ m3.to_4x4() @ Matrix.Translation(-Vector(pivot))


# ---------------------------------------------------------------- fuselage

FUS = [
    # y,      cx,  cz,   w,    hTop, hBot, exp
    (-5.25, 0.0, 1.38, 0.10, 0.12, 0.10, 2.4),
    (-5.10, 0.0, 1.40, 0.30, 0.30, 0.28, 2.4),
    (-4.80, 0.0, 1.45, 0.44, 0.42, 0.40, 2.6),
    (-4.20, 0.0, 1.52, 0.52, 0.40, 0.50, 2.8),
    (-3.40, 0.0, 1.58, 0.58, 0.38, 0.58, 3.4),
    (-2.80, 0.0, 1.62, 0.60, 0.46, 0.62, 3.4),
    (-2.40, 0.0, 1.64, 0.62, 0.50, 0.64, 3.4),
    (-1.40, 0.0, 1.70, 0.66, 0.66, 0.68, 3.2),
    (-0.40, 0.0, 1.72, 0.70, 0.80, 0.70, 3.2),
    (0.70, 0.0, 1.74, 0.70, 0.78, 0.68, 3.2),
    (1.70, 0.0, 1.80, 0.60, 0.62, 0.55, 3.0),
    (2.60, 0.0, 1.90, 0.40, 0.42, 0.38, 2.6),
    (3.80, 0.0, 1.98, 0.30, 0.32, 0.30, 2.4),
    (5.20, 0.0, 2.04, 0.23, 0.26, 0.24, 2.3),
    (6.50, 0.0, 2.08, 0.19, 0.22, 0.20, 2.3),
    (7.40, 0.0, 2.10, 0.15, 0.20, 0.16, 2.2),
]


def canopy(part):
    """Verrière en tandem étagée : panneaux plans de verre teinté et montants (pilote devant/bas, tireur derrière/haut)."""
    # Contours des pare-brise : prismes à facettes posés sur le fuselage.
    def cab(y0, y1, zb, zt, wb, wt, front_drop):
        bm = bmesh.new()
        # Base (largeur wb) et toit (largeur wt), avant incliné.
        p = [(-wb, y0 + front_drop, zb), (wb, y0 + front_drop, zb), (wb, y1, zb), (-wb, y1, zb),
             (-wt, y0 + front_drop + 0.55, zt), (wt, y0 + front_drop + 0.55, zt), (wt, y1 - 0.15, zt), (-wt, y1 - 0.15, zt)]
        v = [bm.verts.new(q) for q in p]
        for f in [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]:
            bm.faces.new([v[i] for i in f])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return bm
    # Habitacle avant (pilote-tireur bas devant), habitacle arrière plus haut.
    part.add(cab(-4.05, -2.75, 1.92, 2.42, 0.56, 0.36, 0.0), GLASS)
    part.add(cab(-2.85, -1.30, 2.12, 2.72, 0.60, 0.38, 0.05), GLASS)
    # Montants : arêtes des verrières.
    def frame(y0, y1, zb, zt, wb, wt, fd):
        pts = [((-wb, y0 + fd, zb), (-wt, y0 + fd + 0.55, zt)), ((wb, y0 + fd, zb), (wt, y0 + fd + 0.55, zt)),
               ((-wb, y1, zb), (-wt, y1 - 0.15, zt)), ((wb, y1, zb), (wt, y1 - 0.15, zt)),
               ((-wt, y0 + fd + 0.55, zt), (wt, y0 + fd + 0.55, zt)), ((-wt, y1 - 0.15, zt), (wt, y1 - 0.15, zt)),
               ((-wt, y0 + fd + 0.55, zt), (-wt, y1 - 0.15, zt)), ((wt, y0 + fd + 0.55, zt), (wt, y1 - 0.15, zt)),
               ((0, y0 + fd, zb), (0, y0 + fd + 0.55, zt))]
        for a, b in pts:
            a, b = Vector(a), Vector(b)
            d = b - a
            L = d.length
            m = d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
            bm = cyl(0.028, 0.028, L, (0, 0, 0), 'Z', 8)
            bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ m, verts=bm.verts)
            part.add(bm, PAINT2)
    frame(-4.05, -2.75, 1.92, 2.42, 0.565, 0.365, 0.0)
    frame(-2.85, -1.30, 2.12, 2.72, 0.605, 0.385, 0.05)
    # Rebord de verrière (seuil).
    part.add(box((1.24, 2.9, 0.06), (0, -2.65, 1.93), 0.02), PAINT2)


def build_body(mats, root):
    body = Part()
    fus = loft(FUS, 44)
    body.add(fus, PAINT)
    canopy(body)
    # Capot dorsal (avionique) entre la verrière et le mât, carénage de la boîte de transmission.
    body.add(loft([(-1.35, 0, 2.30, 0.30, 0.10, 0.25, 2.6), (-0.9, 0, 2.38, 0.42, 0.22, 0.3, 2.8), (0.2, 0, 2.45, 0.48, 0.28, 0.35, 3.0),
                   (1.1, 0, 2.40, 0.42, 0.22, 0.32, 2.8), (1.9, 0, 2.20, 0.25, 0.08, 0.25, 2.4)], 28), PAINT)
    # Nacelles moteur (gauche/droite) : entrée d'air ronde, carter, éjecteurs d'échappement inclinés vers l'extérieur.
    for s in (-1, 1):
        nx = s * 0.86
        body.add(loft([(-0.55, nx, 2.12, 0.20, 0.20, 0.20, 2.0), (-0.40, nx, 2.12, 0.30, 0.30, 0.30, 2.0), (0.4, nx, 2.12, 0.33, 0.33, 0.33, 2.2),
                       (1.4, nx, 2.10, 0.31, 0.31, 0.31, 2.2), (1.95, nx, 2.08, 0.22, 0.24, 0.22, 2.0)], 28, cap_start=False), PAINT)
        body.add(cyl(0.2, 0.2, 0.06, (nx, -0.56, 2.12), 'Y', 24), DARK)               # entrée d'air
        body.add(cyl(0.08, 0.08, 0.1, (nx, -0.6, 2.12), 'Y', 12), PAINT2)             # cône d'entrée
        ex = Matrix.Rotation(math.radians(s * 22), 3, 'Z')
        body.add(cyl(0.21, 0.17, 0.5, (0, 0, 0), 'Y', 20, ex), PAINT2, Matrix.Translation((nx + s * 0.08, 2.12, 2.1)))   # suppresseur IR
        body.add(cyl(0.14, 0.14, 0.05, (0, 0, 0), 'Y', 20, ex), RUBBER, Matrix.Translation((nx + s * 0.18, 2.37, 2.1)))
        # Pylône de liaison nacelle-fuselage.
        body.add(box((0.35, 1.6, 0.18), (s * 0.55, 0.5, 2.05), 0.06), PAINT)
    # Ailettes (anhédrales légères) avec deux pylônes chacune et rails lance-missiles en bout.
    for s in (-1, 1):
        wing = [Vector((s * 0.6, -0.15, 1.62)), Vector((s * 2.45, -0.05, 1.50)), Vector((s * 2.45, 0.62, 1.50)), Vector((s * 0.6, 0.85, 1.62))]
        body.add(poly_prism(wing, 0.1, (0, 0, 1)), PAINT)
        body.add(box((0.08, 0.7, 0.2), (s * 2.47, 0.28, 1.52), 0.03), PAINT2)          # saumon
        body.add(box((0.05, 0.08, 0.05), (s * 2.5, -0.05, 1.53), 0.01), LGREEN if s > 0 else LRED)  # feux de position
        for px in (1.6, 2.2):
            body.add(box((0.1, 0.55, 0.28), (s * px, 0.32, 1.38), 0.03), PAINT2)       # pylône
        # Rails extérieurs : quatre missiles antichar.
        for k in range(4):
            mx = s * (2.2 + (0.13 if k % 2 else -0.13))
            mz = 1.02 if k < 2 else 0.76
            body.add(cyl(0.085, 0.085, 1.55, (mx, -0.05, mz), 'Y', 14), PAINT2)
            body.add(sphere(0.085, (mx, -0.83, mz), (1, 1.6, 1), 14, 8), DARK)
            for f in range(4):
                a = f / 4 * math.tau + math.pi / 4
                body.add(box((0.012, 0.22, 0.14), (mx + math.cos(a) * 0.12, 0.62, mz + math.sin(a) * 0.12), 0.0), PAINT2)
        body.add(box((0.32, 1.7, 0.08), (s * 2.2, 0.0, 1.17), 0.02), DARK)              # lanceur
    # Train d'atterrissage : jambes principales à bras tiré, roues, roulette de queue.
    for s in (-1, 1):
        wx = s * 1.02
        body.add(cyl(0.34, 0.34, 0.2, (wx, -2.2, 0.34), 'X', 28), RUBBER)                   # roue
        body.add(cyl(0.21, 0.21, 0.23, (wx, -2.2, 0.34), 'X', 22), PAINT2)                  # jante
        body.add(cyl(0.07, 0.07, 0.3, (wx, -2.2, 0.34), 'X', 12), DARK)                     # moyeu
        body.add(box((0.1, 0.62, 0.1), (wx - s * 0.16, -1.95, 0.42), 0.03), DARK,          # bras tiré
                 rot_about(Matrix.Rotation(math.radians(-14), 3, 'X'), (wx - s * 0.16, -1.95, 0.42)))
        body.add(cyl(0.065, 0.065, 0.75, (wx - s * 0.16, -1.68, 0.86), 'Z', 12), PAINT2)   # amortisseur
        body.add(cyl(0.045, 0.045, 0.4, (wx - s * 0.16, -1.68, 0.42), 'Z', 12), DARK)      # tige chromée
        body.add(box((0.32, 0.5, 0.2), (wx - s * 0.2, -1.68, 1.3), 0.06), PAINT)           # carénage d'emplanture
    # Roulette de queue : dérive ventrale sous la poutre, fourche courte.
    vfin = [Vector((0, 6.75, 1.95)), Vector((0, 7.55, 2.0)), Vector((0, 7.4, 0.75)), Vector((0, 7.05, 0.72))]
    body.add(poly_prism(vfin, 0.12, (1, 0, 0)), PAINT)
    body.add(cyl(0.05, 0.05, 0.45, (0, 7.22, 0.52), 'Z', 10), DARK)
    body.add(box((0.2, 0.1, 0.06), (0, 7.22, 0.3), 0.02), DARK)
    body.add(cyl(0.17, 0.17, 0.12, (0, 7.28, 0.17), 'X', 20), RUBBER)
    body.add(cyl(0.09, 0.09, 0.14, (0, 7.28, 0.17), 'X', 14), PAINT2)
    # Dérive en flèche, stabilisateur horizontal.
    fin = [Vector((0, 6.55, 2.1)), Vector((0, 7.45, 2.15)), Vector((0, 8.05, 3.75)), Vector((0, 7.65, 3.85)), Vector((0, 7.15, 2.9))]
    body.add(poly_prism(fin, 0.14, (1, 0, 0)), PAINT)
    stab = [Vector((-1.7, 7.35, 2.0)), Vector((1.7, 7.35, 2.0)), Vector((1.7, 7.85, 2.0)), Vector((-1.7, 7.95, 2.0))]
    body.add(poly_prism(stab, 0.07, (0, 0, 1)), PAINT)
    body.add(box((0.32, 0.4, 0.3), (0.12, 7.75, 3.32), 0.06), PAINT2)                   # boîte de renvoi de queue
    body.add(box((0.06, 0.06, 0.06), (0, 8.04, 3.78), 0.0), LAMP)                       # feu arrière
    # Mât, radôme de mât (fixe), anticollision.
    body.add(cyl(0.16, 0.2, 0.7, (0, 0.2, 2.95), 'Z', 18), DARK)
    body.add(cyl(0.12, 0.12, 0.55, (0, 0.2, 3.5), 'Z', 12), DARK)
    body.add(sphere(0.62, (0, 0.2, 3.9), (1, 1, 0.42), 28, 12), PAINT2)
    body.add(box((0.1, 0.1, 0.06), (0, 2.2, 2.33), 0.01), LRED)
    # Boule de capteurs (désignation / vision nocturne) dans le nez, fenêtres sombres.
    body.add(cyl(0.3, 0.3, 0.46, (0, -5.08, 1.08), 'Z', 32), PAINT2)                       # tambour orientable
    body.add(sphere(0.3, (0, -5.08, 0.85), (1, 1, 0.35), 32, 8), PAINT2)
    body.add(cyl(0.31, 0.31, 0.05, (0, -5.08, 1.31), 'Z', 32), DARK)
    # Fenêtres des capteurs, tangentes au tambour (désignation à gauche, caméra thermique à droite).
    for x, wdt, h in ((-0.1, 0.2, 0.18), (0.13, 0.11, 0.11)):
        ang = math.asin(x / 0.3)
        win = box((wdt, 0.03, h), (0, 0, 0), 0.008)
        bmesh.ops.transform(win, matrix=Matrix.Translation((0.3 * math.sin(ang), -5.08 - 0.3 * math.cos(ang), 1.08)) @ Matrix.Rotation(-ang, 4, 'Z'), verts=win.verts)
        body.add(win, GLASS)
    body.add(sphere(0.18, (0, -5.08, 1.62), (1, 1, 0.9), 18, 10), PAINT2)               # tourelle de pilotage nocturne
    body.add(box((0.14, 0.06, 0.1), (0, -5.24, 1.62), 0.02), GLASS)
    # Coupe-câbles au-dessus et sous la verrière, sondes, antennes, marchepieds.
    body.add(box((0.04, 0.5, 0.12), (0, -4.6, 0.95), 0.01), DARK, rot_about(Matrix.Rotation(math.radians(30), 3, 'X'), (0, -4.6, 0.95)))
    for s in (-1, 1):
        body.add(cyl(0.012, 0.012, 0.45, (s * 0.3, -4.95, 1.82), 'Y', 6), DARK)         # sondes Pitot
        body.add(box((0.25, 0.05, 0.02), (s * 0.66, -2.2, 1.25), 0.0), DARK)            # marchepieds
        body.add(box((0.25, 0.05, 0.02), (s * 0.68, -1.0, 1.0), 0.0), DARK)
        body.add(box((0.28, 0.38, 0.22), (s * 0.66, 1.3, 1.45), 0.05), PAINT2)          # brouilleur / contre-mesures
    body.add(box((0.02, 0.18, 0.22), (0, 3.4, 2.36), 0.01), DARK, rot_about(Matrix.Rotation(math.radians(25), 3, 'X'), (0, 3.4, 2.36)))
    body.add(box((0.03, 0.25, 0.35), (0, -0.6, 1.0), 0.01), DARK)
    # Sponsons : caissons d'avionique en saillie sur les flancs, de la verrière aux ailettes.
    for s in (-1, 1):
        body.add(loft([(-3.0, s * 0.56, 1.3, 0.06, 0.12, 0.14, 2.4), (-2.6, s * 0.66, 1.32, 0.17, 0.2, 0.24, 3.0),
                       (-1.2, s * 0.72, 1.34, 0.2, 0.22, 0.27, 3.2), (0.2, s * 0.74, 1.34, 0.2, 0.22, 0.27, 3.2),
                       (0.8, s * 0.7, 1.33, 0.15, 0.16, 0.2, 2.8), (1.2, s * 0.62, 1.32, 0.06, 0.08, 0.1, 2.4)], 28), PAINT)
        body.add(box((0.02, 1.4, 0.03), (s * 0.935, -1.0, 1.42), 0.0), PAINT2)          # ligne de trappe
        body.add(box((0.02, 0.03, 0.4), (s * 0.935, -0.3, 1.32), 0.0), PAINT2)
    # Ventre : carénage du canon et coffre à munitions.
    body.add(loft([(-3.9, 0, 1.05, 0.16, 0.08, 0.06, 2.4), (-3.3, 0, 1.0, 0.28, 0.1, 0.1, 2.6), (-2.2, 0, 1.0, 0.36, 0.12, 0.12, 2.8),
                   (-1.0, 0, 1.03, 0.34, 0.1, 0.1, 2.6)], 24), PAINT2)
    # Bande foncée de cheminement (relief des trappes).
    for y in (-1.1, 0.2, 1.4):
        body.add(box((1.42, 0.03, 1.2), (0, y, 1.7), 0.0), PAINT2)
    return body.obj("Body", mats, (0, 0, 0), root, smooth=35)


ROTOR_HUB = (0, 0.2, 3.46)
TAIL_HUB = (0.32, 7.75, 3.32)
GUN_PIVOT = (0, -3.55, 0.88)
POD_PIVOT_X, POD_PIVOT_Y, POD_PIVOT_Z = 1.6, 0.32, 1.12


def blade(length, chord, root, sweep_tip=True):
    """Pale : profil aplati, vrillage léger, saumon en flèche ; le long de +X depuis l'axe."""
    secs = []
    n = 10
    for i in range(n + 1):
        t = i / n
        x = root + (length - root) * t
        c = chord * (1.0 if t < 0.9 else 1.0 - (t - 0.9) * 4.0)
        twist = math.radians(8 * (1 - t))
        secs.append((x, c, twist, 0.035 * chord / 0.5 * (1.0 - 0.4 * t)))
    bm = bmesh.new()
    rings = []
    for (x, c, tw, th) in secs:
        ring = []
        for (u, v) in [(-0.5, 0), (-0.2, 1), (0.25, 0.8), (0.5, 0), (0.25, -0.5), (-0.2, -0.6)]:
            y, z = u * c, v * th
            y2 = y * math.cos(tw) - z * math.sin(tw)
            z2 = y * math.sin(tw) + z * math.cos(tw)
            ring.append(bm.verts.new((x, y2 + (0.12 * max(0, x - length * 0.9) if sweep_tip else 0), z2 - 0.004 * x)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(6):
            j = (i + 1) % 6
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def build_rotor(mats, root):
    r = Part()
    hx, hy, hz = ROTOR_HUB
    r.add(cyl(0.34, 0.3, 0.22, (hx, hy, hz), 'Z', 24), DARK)                            # moyeu
    r.add(cyl(0.16, 0.16, 0.5, (hx, hy, hz + 0.12), 'Z', 14), DARK)
    for k in range(4):
        a = k / 4 * math.tau + math.radians(45)
        m = Matrix.Translation((hx, hy, hz)) @ Matrix.Rotation(a, 4, 'Z')
        arm = box((0.6, 0.16, 0.1), (0.48, 0, 0.0), 0.02)
        bmesh.ops.transform(arm, matrix=m, verts=arm.verts)
        r.add(arm, DARK)
        damper = cyl(0.045, 0.045, 0.45, (0.4, 0.14, 0.06), 'X', 8)
        bmesh.ops.transform(damper, matrix=m, verts=damper.verts)
        r.add(damper, PAINT2)
        bl = blade(5.75, 0.52, 0.7)
        bmesh.ops.transform(bl, matrix=m, verts=bl.verts)
        r.add(bl, BLADE)
        tip = box((0.25, 0.53, 0.03), (5.62, 0.04, -0.02), 0.0)
        bmesh.ops.transform(tip, matrix=m, verts=tip.verts)
        r.add(tip, PAINT)
    r.obj("Rotor", mats, ROTOR_HUB, root, smooth=40)

    t = Part()
    tx, ty, tz = TAIL_HUB
    t.add(cyl(0.12, 0.12, 0.2, (tx, ty, tz), 'X', 16), DARK)
    t.add(sphere(0.1, (tx + 0.12, ty, tz), (1, 1, 1), 12, 8), PAINT2)
    # Rotor de queue en ciseaux : deux paires de pales décalées de 55°.
    for k, a in enumerate([0, math.radians(55), math.pi, math.pi + math.radians(55)]):
        bl = blade(1.25, 0.17, 0.12, False)
        m = Matrix.Translation((tx + (0.03 if k % 2 else -0.03), ty, tz)) @ Matrix.Rotation(a, 4, 'X') @ Matrix.Rotation(math.radians(-90), 4, 'Y')
        # La pale pousse le long de +X ; on la couche dans le plan YZ (axe du rotor de queue = X).
        bmesh.ops.transform(bl, matrix=Matrix.Translation((tx, ty, tz)) @ Matrix.Rotation(a, 4, 'X') @ Matrix.Rotation(math.radians(90), 4, 'Y') @ Matrix.Translation((0, 0, 0)), verts=bl.verts)
        t.add(bl, BLADE)
    t.obj("TailRotor", mats, TAIL_HUB, root, smooth=40)


def build_gun(mats, root):
    gx, gy, gz = GUN_PIVOT
    g = Part()
    g.add(cyl(0.3, 0.26, 0.2, (gx, gy, gz + 0.08), 'Z', 24), DARK)                      # couronne
    g.add(sphere(0.26, (gx, gy, gz - 0.06), (1, 1, 0.7), 22, 10), PAINT2)                # carter de tourelle
    g.add(box((0.18, 0.4, 0.2), (gx + 0.22, gy - 0.05, gz - 0.06), 0.04), PAINT2)        # alimentation
    gun = g.obj("Gun", mats, GUN_PIVOT, root, smooth=35)
    c = Part()
    c.add(box((0.24, 0.6, 0.22), (gx, gy - 0.25, gz - 0.08), 0.03), DARK)                # culasse et moteur
    c.add(cyl(0.11, 0.11, 0.35, (gx, gy - 0.65, gz - 0.08), 'Y', 18), PAINT2)            # carter avant
    cannon = c.obj("Cannon", mats, (gx, gy, gz - 0.08), gun, smooth=40)
    # Canon rotatif à trois tubes (objet à part : il tourne en tirant).
    bz = gz - 0.08
    br = Part()
    for k in range(3):
        a = k / 3 * math.tau
        ox, oz = math.cos(a) * 0.055, math.sin(a) * 0.055
        br.add(cyl(0.026, 0.026, 1.45, (gx + ox, gy - 1.5, bz + oz), 'Y', 10), DARK)
    for yy in (-0.95, -1.6, -2.18):
        br.add(cyl(0.09, 0.09, 0.05, (gx, gy + yy, bz), 'Y', 16), DARK)               # colliers
    br.add(cyl(0.035, 0.035, 1.4, (gx, gy - 1.5, bz), 'Y', 8), PAINT2)                   # axe
    barrels = br.obj("Barrels", mats, (gx, gy, bz), cannon, smooth=40)
    mz = bpy.data.objects.new("GunMuzzle", None)
    bpy.context.collection.objects.link(mz)
    mz.parent = barrels
    mz.location = (0, -2.25, 0)


def build_pods(mats, root):
    for s, tag in ((-1, "L"), (1, "R")):
        px = s * POD_PIVOT_X
        p = Part()
        # Panier 19 tubes : corps cylindrique, ogive arrière, face avant à alvéoles.
        p.add(cyl(0.29, 0.29, 1.7, (px, POD_PIVOT_Y + 0.0, POD_PIVOT_Z - 0.32), 'Y', 28), PAINT2)
        p.add(sphere(0.29, (px, POD_PIVOT_Y + 0.85, POD_PIVOT_Z - 0.32), (1, 0.7, 1), 24, 10), PAINT2)
        p.add(cyl(0.3, 0.3, 0.06, (px, POD_PIVOT_Y - 0.85, POD_PIVOT_Z - 0.32), 'Y', 28), DARK)
        tubes = [(0, 0)] + [(0.11 * math.cos(a), 0.11 * math.sin(a)) for a in [i / 6 * math.tau for i in range(6)]] + \
                [(0.21 * math.cos(a), 0.21 * math.sin(a)) for a in [i / 12 * math.tau for i in range(12)]]
        for (tx, tz) in tubes:
            p.add(cyl(0.043, 0.043, 0.07, (px + tx, POD_PIVOT_Y - 0.86, POD_PIVOT_Z - 0.32 + tz), 'Y', 10), RUBBER)
        for yy in (-0.6, 0.0, 0.6):
            p.add(cyl(0.295, 0.295, 0.04, (px, POD_PIVOT_Y + yy, POD_PIVOT_Z - 0.32), 'Y', 28), DARK)
        p.add(box((0.08, 0.5, 0.06), (px, POD_PIVOT_Y, POD_PIVOT_Z - 0.03), 0.01), DARK)    # attache
        pod = p.obj(f"Pod_{tag}", mats, (px, POD_PIVOT_Y, POD_PIVOT_Z), root, smooth=40)
        mz = bpy.data.objects.new(f"PodMuzzle_{tag}", None)
        bpy.context.collection.objects.link(mz)
        mz.parent = pod
        mz.location = (0, -1.25, -0.32)


def main(out):
    clear()
    mats = materials()
    root = bpy.data.objects.new("Helicopter", None)
    bpy.context.collection.objects.link(root)
    build_body(mats, root)
    build_rotor(mats, root)
    build_gun(mats, root)
    build_pods(mats, root)
    if out:
        os.makedirs(os.path.dirname(out), exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        for o in bpy.data.objects:
            o.select_set(True)
        bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True, export_apply=True,
                                  export_yup=True, export_texcoords=True, export_normals=True, export_materials='EXPORT')
    tris = sum(len(p.vertices) - 2 for o in bpy.data.objects if o.type == 'MESH' for p in o.data.polygons)
    return {"objects": len(bpy.data.objects), "tris": tris}


OUT = globals().get("OUT")
if OUT is None and "--" in sys.argv:
    OUT = sys.argv[sys.argv.index("--") + 1]
result = main(OUT)
