# Kit de modélisation des armes (Blender, par script) : pièces détachées réutilisables par toutes les familles d'armes.
# Repère de travail : u vers la bouche du canon, x à droite, z en haut ; converti en repère Blender (avant −Y, haut +Z) :
# l'export glTF donne l'avant +Z, haut +Y, comme les armes tenues par le soldat dans Godot (SoldierModel).
# Unités : mètres, taille réelle. Origine : axe du canon, au-dessus de la poignée pistolet.
#
# Chaque arme (une famille = un fichier .glb) contient :
#   Body       : tout le corps de l'arme (plusieurs matériaux) ;
#   opt_*      : pièces montrées ou non selon la variante (opt_laser, opt_scope, opt_holo, opt_light, opt_bipod…) ;
#   Muzzle, Grip, Support, Butt, Mag : repères vides (bouche, main droite, main gauche, crosse à l'épaule, chargeur).
# Matériaux (remplacés dans Godot par les matières du jeu) :
#   Metal (acier phosphaté), Polymer (polymère noir), Paint (couleur de la famille), Accent (seconde couleur),
#   Alloy (alu anodisé clair), Rubber, Glass (optiques), Lens (traitement des lentilles), Glow (diode, laser), Energy (cellules).
import bpy, bmesh, math
from mathutils import Vector, Matrix

MATS = ["Metal", "Polymer", "Paint", "Accent", "Alloy", "Rubber", "Glass", "Lens", "Glow", "Energy"]
METAL, POLY, PAINT, ACCENT, ALLOY, RUBBER, GLASS, LENS, GLOW, ENERGY = range(10)

# Couleurs de travail (rendus de contrôle) ; dans le jeu, Paint/Accent viennent de la variante.
BASE = {"Metal": ((0.045, 0.045, 0.05), 0.42, 0.85), "Polymer": ((0.035, 0.035, 0.034), 0.7, 0.0),
        "Paint": ((0.16, 0.17, 0.12), 0.6, 0.1), "Accent": ((0.32, 0.27, 0.18), 0.65, 0.05),
        "Alloy": ((0.42, 0.43, 0.45), 0.35, 0.9), "Rubber": ((0.02, 0.02, 0.02), 0.95, 0.0),
        "Glass": ((0.02, 0.03, 0.04), 0.05, 0.5), "Lens": ((0.1, 0.25, 0.35), 0.05, 0.3),
        "Glow": ((1.0, 0.15, 0.08), 0.3, 0.0), "Energy": ((0.3, 0.75, 1.0), 0.3, 0.0)}


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


def materials(paint=None, accent=None):
    out = []
    for n in MATS:
        c, r, mt = BASE[n]
        if n == "Paint" and paint: c = paint
        if n == "Accent" and accent: c = accent
        m = bpy.data.materials.new(n)
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (*c, 1)
        b.inputs["Roughness"].default_value = r
        b.inputs["Metallic"].default_value = mt
        if n in ("Glow", "Energy"):
            b.inputs["Emission Color"].default_value = (*c, 1)
            b.inputs["Emission Strength"].default_value = 4.0
        out.append(m)
    return out


def P(x, u, z):
    """Repère de travail (x droite, u avant, z haut) → Blender (avant −Y)."""
    return Vector((x, -u, z))


class Part:
    """Accumulateur de géométrie (un objet Blender à la fin)."""

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

    def obj(self, name, mats, parent=None, sharp=32.0):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-6)
        # Miroir : le x « droite » du repère de travail devient la droite de l'arme dans Godot (canon vers +Z : droite = −X).
        bmesh.ops.scale(self.bm, vec=(-1, 1, 1), verts=self.bm.verts)
        bmesh.ops.reverse_faces(self.bm, faces=self.bm.faces)
        # Lissage, arêtes vives au-delà de « sharp » degrés (normales découpées à l'export).
        for f in self.bm.faces:
            f.smooth = True
        lim = math.radians(sharp)
        for e in self.bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0) > lim:
                e.smooth = False
        self.bm.to_mesh(me)
        self.bm.free()
        for m in mats:
            me.materials.append(m)
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        if parent:
            o.parent = parent
        return o


def empty(name, at, parent=None):
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    e.empty_display_size = 0.03
    e.location = at
    if parent:
        e.parent = parent
    return e


# ───────────────────────── Primitives ─────────────────────────

def box(sx, su, sz, c, bevel=0.003, seg=2):
    """Pavé centré en c (repère de travail), arêtes arrondies."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, su, sz), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(sx, su, sz) * 0.45), segments=seg, affect='EDGES', profile=0.5)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def tube(r0, r1, u0, u1, x=0.0, z=0.0, seg=20, caps=True):
    """Cylindre (cône) le long de u, de u0 à u1."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=abs(u1 - u0))
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(x, (u0 + u1) / 2, z), verts=bm.verts)
    return bm


def ring(r_out, r_in, u0, u1, x=0.0, z=0.0, seg=24):
    """Bague (tube creux), ex. bague de lunette, frein de bouche."""
    bm = bmesh.new()
    vs = []
    for (u, r) in ((u0, r_out), (u1, r_out), (u1, r_in), (u0, r_in)):
        loop = [bm.verts.new(P(x + r * math.cos(a), u, z + r * math.sin(a))) for a in (i * 2 * math.pi / seg for i in range(seg))]
        vs.append(loop)
    for k in range(4):
        a, b = vs[k], vs[(k + 1) % 4]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def loft(sections, seg=20, caps=True):
    """Profil arrondi le long de u : sections = [(u, demi-largeur, demi-hauteur, z, exposant superellipse)]."""
    bm = bmesh.new()
    loops = []
    for (u, hw, hh, z, p) in sections:
        loop = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ca, sa = math.cos(a), math.sin(a)
            x = hw * math.copysign(abs(ca) ** (2 / p), ca)
            zz = hh * math.copysign(abs(sa) ** (2 / p), sa)
            loop.append(bm.verts.new(P(x, u, z + zz)))
        loops.append(loop)
    for k in range(len(loops) - 1):
        a, b = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if caps:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def prism(profile, x0, x1, bevel=0.0015):
    """Prisme latéral : profile = [(u, z)] (polygone dans le plan de côté), extrudé de x0 à x1."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x0, u, z)) for (u, z) in profile]
    b = [bm.verts.new(P(x1, u, z)) for (u, z) in profile]
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


# ───────────────────────── Pièces d'armes ─────────────────────────

def rail(part, u0, u1, z, x=0.0, w=0.021, mat=METAL, side=False):
    """Rail Picatinny : semelle + créneaux (dessus, ou côté si side)."""
    n = max(2, int((u1 - u0) / 0.01))
    if not side:
        part.add(box(w, u1 - u0, 0.006, (x, (u0 + u1) / 2, z + 0.003), 0.001), mat)
        for i in range(n):
            u = u0 + (i + 0.5) * (u1 - u0) / n
            part.add(box(w * 0.92, 0.0052, 0.0045, (x, u, z + 0.0082), 0.0008, 1), mat)
    else:
        s = 1 if x > 0 else -1
        part.add(box(0.006, u1 - u0, w, (x + s * 0.003, (u0 + u1) / 2, z), 0.001), mat)
        for i in range(n):
            u = u0 + (i + 0.5) * (u1 - u0) / n
            part.add(box(0.0045, 0.0052, w * 0.92, (x + s * 0.0082, u, z), 0.0008, 1), mat)


def pistol_grip(part, u, z, mat=POLY, angle=18, h=0.105, w=0.03, d=0.042):
    """Poignée pistolet inclinée vers l'arrière ; (u, z) = haut de la poignée. Renvoie le point de la main."""
    a = math.radians(angle)
    pts = []
    for k in range(6):
        t = k / 5
        uu = u - math.sin(a) * h * t
        zz = z - math.cos(a) * h * t
        # Renflement de paume et doigts.
        hw = w / 2 * (1.0 + 0.12 * math.sin(t * math.pi))
        hd = d / 2 * (1.05 - 0.15 * t)
        pts.append((uu, hw, hd, zz))
    bm = bmesh.new()
    seg = 14
    loops = []
    for (uu, hw, hd, zz) in pts:
        loop = []
        for i in range(seg):
            ang = 2 * math.pi * i / seg
            loop.append(bm.verts.new(P(hw * math.cos(ang), uu + hd * math.sin(ang), zz)))
        loops.append(loop)
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    part.add(bm, mat)
    # Pontet.
    part.add(box(0.006, 0.07, 0.004, (0, u + 0.035, z - 0.042), 0.0015), METAL)
    part.add(box(0.006, 0.004, 0.04, (0, u + 0.07, z - 0.022), 0.0015), METAL)
    return (0, u - math.sin(a) * h * 0.45, z - math.cos(a) * h * 0.45)


def straight_mag(part, u, z, h=0.12, w=0.024, d=0.06, tilt=6, mat=METAL):
    """Chargeur droit ; (u, z) = haut. Renvoie son centre."""
    a = math.radians(tilt)
    prof = [(u - d / 2, z), (u + d / 2, z), (u + d / 2 - math.sin(a) * h, z - h), (u - d / 2 - math.sin(a) * h - 0.004, z - h)]
    part.add(prism(prof, -w / 2, w / 2, 0.002), mat)
    part.add(box(w + 0.004, d + 0.006, 0.01, (0, u - math.sin(a) * h, z - h - 0.004), 0.002), POLY)
    return (0, u - math.sin(a) * h / 2, z - h / 2)


def curved_mag(part, u, z, h=0.15, w=0.026, d=0.062, bend=0.06, mat=METAL):
    """Chargeur courbe (type AK) ; arc vers l'avant."""
    n = 8
    prof_f, prof_b = [], []
    for k in range(n + 1):
        t = k / n
        du = bend * t * t
        zz = z - h * t
        prof_f.append((u + d / 2 + du, zz))
        prof_b.append((u - d / 2 + du * 0.9, zz))
    prof = prof_b + list(reversed(prof_f))
    part.add(prism(list(reversed(prof)), -w / 2, w / 2, 0.002), mat)
    for k in range(1, 4):
        t = k / 4
        part.add(box(w + 0.003, 0.004, 0.006, (0, u + bend * t * t + d / 2 - 0.002, z - h * t), 0.001), mat)
    return (0, u + bend * 0.3, z - h / 2)


def drum_mag(part, u, z, r=0.06, w=0.07, mat=METAL):
    """Chargeur tambour sous l'arme."""
    part.add(tube_x(r, w, (0, u, z - r - 0.03)), mat)
    part.add(box(0.026, 0.05, 0.04, (0, u, z - 0.02), 0.003), mat)
    for s in (-1, 1):
        part.add(tube_x(r * 0.55, 0.006, (s * (w / 2 + 0.002), u, z - r - 0.03)), POLY)
    return (0, u, z - r)


def tube_x(r, length, c, seg=24):
    """Cylindre d'axe x (tambours, molettes)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=length)
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def barrel(part, u0, u1, r=0.0095, z=0.0, mat=METAL, flutes=0):
    part.add(tube(r, r, u0, u1, 0, z, 18), mat)
    for k in range(flutes):
        a = 2 * math.pi * k / flutes
        part.add(box(0.003, (u1 - u0) * 0.6, 0.003, (math.cos(a) * r, (u0 + u1) / 2, z + math.sin(a) * r), 0.0008, 1), POLY)


def flash_hider(part, u, z=0.0, r=0.012, l=0.055, mat=METAL):
    part.add(tube(r, r * 1.05, u, u + l, 0, z, 16), mat)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        part.add(box(0.004, l * 0.55, 0.004, (math.cos(a) * r * 1.02, u + l * 0.62, z + math.sin(a) * r * 1.02), 0.0006, 1), POLY)
    return (0, u + l, z)


def muzzle_brake(part, u, z=0.0, r=0.018, l=0.07, ports=3, mat=METAL):
    part.add(tube(r, r, u, u + l, 0, z, 18), mat)
    for k in range(ports):
        uu = u + l * (0.25 + 0.6 * k / max(1, ports - 1))
        for s in (-1, 1):
            part.add(box(0.004, l * 0.12, r * 1.2, (s * r * 0.98, uu, z), 0.0008, 1), POLY)
    return (0, u + l, z)


def suppressor(part, u, z=0.0, r=0.02, l=0.17, mat=METAL):
    part.add(tube(r * 0.85, r, u, u + 0.015, 0, z, 20), mat)
    part.add(tube(r, r, u + 0.015, u + l - 0.01, 0, z, 20), mat)
    part.add(tube(r, r * 0.8, u + l - 0.01, u + l, 0, z, 20), mat)
    return (0, u + l, z)


def holo_sight(part, u, z, mat=METAL):
    """Viseur holographique (boîtier + fenêtre)."""
    part.add(box(0.034, 0.075, 0.012, (0, u, z + 0.006), 0.003), mat)
    part.add(box(0.006, 0.034, 0.036, (-0.016, u + 0.012, z + 0.03), 0.0025), mat)
    part.add(box(0.006, 0.034, 0.036, (0.016, u + 0.012, z + 0.03), 0.0025), mat)
    part.add(box(0.038, 0.034, 0.006, (0, u + 0.012, z + 0.05), 0.0025), mat)
    part.add(box(0.026, 0.002, 0.03, (0, u + 0.012, z + 0.03), 0.0005, 1), GLASS)
    part.add(box(0.022, 0.03, 0.01, (0.0, u - 0.024, z + 0.017), 0.003), mat)


def red_dot(part, u, z, mat=METAL):
    part.add(box(0.026, 0.03, 0.01, (0, u, z + 0.005), 0.002), mat)
    part.add(tube(0.017, 0.017, u - 0.025, u + 0.025, 0, z + 0.03, 20), mat)
    part.add(tube(0.015, 0.015, u + 0.02, u + 0.026, 0, z + 0.03, 20, True), LENS)


def scope(part, u, z, l=0.3, r=0.015, obj=0.024, mat=METAL, base=0.03):
    """Lunette : tube, cloche d'objectif et d'oculaire, tourelles, colliers posés sur le rail (dessus à z = base)."""
    part.add(tube(r * 1.45, r * 1.25, u - l / 2, u - l / 2 + 0.07, 0, z, 22), mat)
    part.add(tube(r, r, u - l / 2 + 0.07, u + l / 2 - 0.09, 0, z, 22), mat)
    part.add(tube(r, obj, u + l / 2 - 0.09, u + l / 2 - 0.04, 0, z, 22), mat)
    part.add(tube(obj, obj, u + l / 2 - 0.04, u + l / 2, 0, z, 22), mat)
    part.add(tube(obj * 0.88, obj * 0.88, u + l / 2 - 0.002, u + l / 2 + 0.001, 0, z, 22), LENS)
    part.add(tube(r * 1.1, r * 1.1, u - l / 2 - 0.001, u - l / 2 + 0.001, 0, z, 22), LENS)
    part.add(tube(r * 0.5, r * 0.5, u - 0.03, u + 0.03, 0, z + r + 0.008, 14), mat)
    part.add(tube_x(r * 0.5, 0.03, (r + 0.012, u, z)), mat)
    for uu in (u - l / 2 + 0.1, u + l / 2 - 0.12):
        part.add(ring(r * 1.25, r, uu - 0.01, uu + 0.01, 0, z), mat)
        hh = max(z - r - base, 0.004)
        part.add(box(0.022, 0.024, hh, (0, uu, base + hh / 2), 0.002), mat)
        part.add(box(0.03, 0.03, 0.008, (0, uu, base + 0.004), 0.002), mat)


def laser_module(part, u, z, x=-0.026, mat=POLY):
    """Module laser de côté (lentille lumineuse)."""
    part.add(box(0.018, 0.06, 0.028, (x, u, z), 0.003), mat)
    part.add(tube(0.004, 0.004, u + 0.03, u + 0.034, x, z + 0.006, 10), GLOW)
    part.add(tube(0.005, 0.005, u + 0.03, u + 0.034, x, z - 0.006, 10), LENS)


def flashlight(part, u, z, x=0.026, mat=METAL):
    part.add(tube(0.012, 0.012, u - 0.05, u + 0.03, x, z, 16), mat)
    part.add(tube(0.012, 0.016, u + 0.03, u + 0.045, x, z, 16), mat)
    part.add(tube(0.014, 0.014, u + 0.044, u + 0.046, x, z, 16), LENS)


def vertical_grip(part, u, z, h=0.075, mat=POLY):
    part.add(tube(0.015, 0.013, u, u + 0.001, 0, z, 12), mat)
    part.add(loft([(u - 0.016, 0.014, 0.004, z - 0.004, 2.0), (u + 0.016, 0.014, 0.004, z - 0.004, 2.0)], 12), mat)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=0.014, radius2=0.016, depth=h)
    bmesh.ops.translate(bm, vec=P(0, u, z - h / 2 - 0.006), verts=bm.verts)
    part.add(bm, mat)
    return (0, u, z - h * 0.55)


def angled_grip(part, u, z, mat=POLY):
    part.add(prism([(u - 0.04, z), (u + 0.02, z), (u + 0.03, z - 0.016), (u - 0.005, z - 0.03)], -0.012, 0.012, 0.003), mat)
    return (0, u - 0.005, z - 0.018)


def bipod(part, u, z, folded=True, mat=METAL):
    part.add(box(0.03, 0.02, 0.012, (0, u, z), 0.003), mat)
    for s in (-1, 1):
        if folded:
            part.add(tube(0.0045, 0.0045, u + 0.01, u + 0.2, s * 0.012, z - 0.008, 8), mat)
        else:
            bm = tube(0.0045, 0.0045, 0, 0.2, 0, 0, 8)
            m = Matrix.Translation(P(s * 0.012, u, z)) @ Matrix.Rotation(math.radians(-70), 4, 'X') @ Matrix.Rotation(math.radians(s * 12), 4, 'Y')
            part.add(bm, mat, m)


def stock_fixed(part, u0, u1, z, h0=0.04, h1=0.09, w=0.038, mat=POLY, drop=0.02):
    """Crosse pleine (du boîtier en u0 à la plaque de couche en u1 < u0)."""
    prof = [(u0, z + h0 / 2), (u0, z - h0 / 2), (u1 + 0.02, z - drop - h1 / 2), (u1, z - drop - h1 / 2 + 0.01),
            (u1, z - drop + h1 / 2), (u1 + 0.03, z - drop + h1 / 2 + 0.004)]
    part.add(prism(prof, -w / 2, w / 2, 0.004), mat)
    part.add(box(w + 0.004, 0.012, h1 + 0.004, (0, u1 - 0.004, z - drop), 0.004), RUBBER)
    return (0, u1, z - drop)


def stock_sculpt(part, sections, mat=POLY, pad=True):
    """Crosse sculptée : sections = [(u, demi-largeur, demi-hauteur, z du centre)] de l'avant vers la plaque de couche."""
    part.add(loft([(u, hw, hh, zc, 3.4) for (u, hw, hh, zc) in sections], 22), mat)
    u, hw, hh, zc = sections[-1]
    if pad:
        part.add(loft([(u, hw * 1.02, hh * 1.02, zc, 5.0), (u - 0.012, hw * 1.02, hh * 1.02, zc, 5.0)], 22), RUBBER)
        for k in range(5):
            part.add(box(hw * 2.06, 0.002, 0.003, (0, u - 0.006, zc - hh * 0.7 + k * hh * 0.35), 0.0005, 1), POLY)
    return (0, u, zc)


def receiver(part, u0, u1, hw, z0, z1, mat=METAL, p=7.0):
    """Boîtier aux arêtes adoucies (superellipse) de u0 à u1, du bas z0 au haut z1."""
    zc, hh = (z0 + z1) / 2, (z1 - z0) / 2
    part.add(loft([(u0, hw * 0.96, hh * 0.96, zc, p), (u0 + 0.004, hw, hh, zc, p), (u1 - 0.004, hw, hh, zc, p), (u1, hw * 0.96, hh * 0.96, zc, p)], 24), mat)


def pins(part, us, z, hw, r=0.0035, mat=METAL):
    """Axes traversants (goupilles de démontage) visibles des deux côtés."""
    for u in us:
        part.add(tube_x(r, hw * 2 + 0.003, (0, u, z), 12), mat)


def stock_collapsible(part, u0, u1, z, mat=POLY):
    """Crosse télescopique : tube tampon + crosse coulissante."""
    part.add(tube(0.015, 0.015, u1 + 0.05, u0, 0, z, 16), METAL)
    prof = [(u1 + 0.1, z + 0.02), (u1 + 0.1, z - 0.012), (u1 + 0.02, z - 0.07), (u1, z - 0.068), (u1, z + 0.026), (u1 + 0.04, z + 0.03)]
    part.add(prism(prof, -0.019, 0.019, 0.004), mat)
    part.add(box(0.042, 0.012, 0.1, (0, u1 - 0.004, z - 0.02), 0.004), RUBBER)
    return (0, u1, z - 0.02)


def charging_handle(part, u, z, x=0.0, mat=METAL):
    part.add(box(0.04, 0.012, 0.008, (x, u, z), 0.002), mat)


def front_sight(part, u, z, mat=METAL):
    part.add(prism([(u - 0.012, z), (u + 0.012, z), (u + 0.006, z + 0.045), (u - 0.004, z + 0.045)], -0.008, 0.008, 0.002), mat)
    part.add(box(0.003, 0.004, 0.015, (0, u, z + 0.05), 0.0006, 1), mat)


def vents(part, u0, u1, z, x, n, h=0.012, mat=POLY):
    """Fentes de refroidissement sur un garde-main (côté x)."""
    for k in range(n):
        uu = u0 + (k + 0.5) * (u1 - u0) / n
        part.add(box(0.004, (u1 - u0) / n * 0.5, h, (x, uu, z), 0.0008, 1), mat)


def energy_cell(part, u, z, x=0.0, l=0.06, r=0.012):
    part.add(tube(r, r, u - l / 2, u + l / 2, x, z, 14), ENERGY)
    for uu in (u - l / 2, u + l / 2):
        part.add(ring(r * 1.2, r * 0.9, uu - 0.004, uu + 0.004, x, z, 14), METAL)


# ───────────────────────── Assemblage et export ─────────────────────────

class Gun:
    """Construction d'une arme : corps, options, repères. build() crée les objets."""

    def __init__(self, name):
        self.name = name
        self.body = Part()
        self.opts = {}
        self.points = {}

    def opt(self, name):
        if name not in self.opts:
            self.opts[name] = Part()
        return self.opts[name]

    def point(self, name, c):
        self.points[name] = c

    def build(self, mats):
        root = bpy.data.objects.new(self.name, None)
        bpy.context.collection.objects.link(root)
        self.body.obj("Body", mats, root)
        for n, p in self.opts.items():
            p.obj("opt_" + n, mats, root)
        for n, c in self.points.items():
            empty(n, P(-c[0], c[1], c[2]), root)
        return root


def export(path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        if not o.name.startswith("Studio"):
            o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True, export_yup=True,
                              export_texcoords=True, export_normals=True, export_materials='EXPORT')


def tris():
    return sum(len(p.vertices) - 2 for o in bpy.data.objects if o.type == 'MESH' for p in o.data.polygons)
