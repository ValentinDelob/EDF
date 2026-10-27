# Tenue du Ranger (Fusilier d'EDF 5) — exécuté par build_char.py (--char ranger), dans son espace de noms :
# treillis camouflé, gants, rangers, genouillères et coudières, gilet porte-plaques à poches, ceinturon, casque de combat
# à lunettes, sac à dos d'assaut.
import bpy, bmesh, math
from mathutils import Vector

# Pièces rigides pour le jeu (préfixe du nom → os) : casque et accessoires sur la tête, sac sur le haut du dos.
RIGID = {"Helmet": "head", "Goggle": "head", "NVGMount": "head", "Backpack": "spine_03", "PackStrap": "spine_03",
         "PackPocket": "spine_03"}


def camo_material(name, colors, scale=7.0):
    """Camouflage en taches (bruit seuillé en 4 couleurs, coordonnées de l'objet) et grain de toile."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = 0.78
    p.inputs["Sheen Weight"].default_value = 0.25
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = 3.0
    nz.inputs["Roughness"].default_value = 0.6
    nz.inputs["Distortion"].default_value = 0.6
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (*colors[0], 1)
    els[1].position, els[1].color = 0.44, (*colors[1], 1)
    for pos, c in ((0.53, colors[2]), (0.62, colors[3])):
        e = els.new(pos)
        e.color = (*c, 1)
    nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], p.inputs["Base Color"])
    weave = nt.nodes.new("ShaderNodeTexNoise")
    weave.inputs["Scale"].default_value = 900.0
    nt.links.new(tc.outputs["Object"], weave.inputs["Vector"])
    bp = nt.nodes.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = 0.35
    bp.inputs["Distance"].default_value = 0.0005
    nt.links.new(weave.outputs["Fac"], bp.inputs["Height"])
    nt.links.new(bp.outputs["Normal"], p.inputs["Normal"])
    return m


def surface_point(pts, x, z, front=True):
    """Point de la surface du corps (devant ou dans le dos) à l'abscisse x et la hauteur z."""
    near = [p for p in pts if abs(p.x - x) < 0.025 and abs(p.z - z) < 0.025]
    if not near:
        return Vector((x, -0.12 if front else 0.12, z))
    p = min(near, key=lambda q: q.y) if front else max(near, key=lambda q: q.y)
    return p.copy()


def pouch(name, mats, at, size, tilt=0.0, flap=True):
    """Poche MOLLE : bloc arrondi, rabat sur le dessus, bouton-pression."""
    ob = prim("cube", name, mats, at, rot=(math.radians(tilt), 0, 0), scale=(size[0] / 2, size[1] / 2, size[2] / 2))
    finish_obj(ob, base, bevel=min(size) * 0.25, subdiv=1)
    if flap:
        f = prim("cube", name + "_rabat", mats, (at[0], at[1] - size[1] * 0.05, at[2] + size[2] * 0.42),
                 scale=(size[0] / 2 * 1.04, size[1] / 2 * 1.08, size[2] * 0.12))
        finish_obj(f, base, bevel=0.004, subdiv=1)
    return ob


def shell(base, name, pts, center, scale, mats, thick=0.01):
    """
    Coque de casque : enveloppe convexe des points du crâne (agrandie de scale autour du centre), dessous ouvert, épaisseur vers
    l'intérieur, tranche dans le second matériau. Rend aussi les sommets de la coque (pour poser les accessoires).
    """
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(center + (p - center) * scale)
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.003)
    bm.normal_update()
    zmin = min(v.co.z for v in bm.verts)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.3 or (f.normal.z < 0.2 and all(
        v.co.z < zmin + 0.004 for v in f.verts))], context="FACES")
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    verts = [v.co.copy() for v in bm.verts]
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    so = ob.modifiers.new("Epaisseur", "SOLIDIFY")
    so.thickness = thick
    so.offset = -1.0
    if len(mats) > 1:
        so.material_offset_rim = 1
    finish_obj(ob, base, bevel=0.003, subdiv=2)
    return ob, verts


def trim_hair(cut):
    """Cheveux sous un casque : les mèches au-dessus du bord (dans la coque) sont masquées, elles ne la traversent plus."""
    for o in bpy.data.objects:
        if o.type == "MESH" and CFG["hair"] in o.name.lower():
            g = o.vertex_groups.new(name="sous_casque")
            g.add([v.index for v in o.data.vertices if cut(o.matrix_world @ v.co)], 1.0, "REPLACE")
            m = o.modifiers.new("Sous le casque", "MASK")
            m.vertex_group = "sous_casque"
            m.invert_vertex_group = True


def extent(verts, z, tol=0.015):
    """Devant (y minimal) et demi-largeur de la coque à la hauteur z."""
    near = [v for v in verts if abs(v.z - z) < tol] or verts
    return min(v.y for v in near), max(abs(v.x) for v in near)


def band(base, name, mats, cy, rx, ry, z, h, arc, thick=0.006):
    """Bandeau en arc d'ellipse (centre (0, cy), demi-axes rx, ry) autour de la tête, ouvert vers l'arrière, hauteur h."""
    bm = bmesh.new()
    rows = [[bm.verts.new((math.sin(a) * rx, cy - math.cos(a) * ry, z + dz))
             for a in (math.radians(-arc / 2 + arc * k / 24) for k in range(25))] for dz in (-h / 2, h / 2)]
    for k in range(24):
        bm.faces.new((rows[0][k], rows[0][k + 1], rows[1][k + 1], rows[1][k]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    so = ob.modifiers.new("Epaisseur", "SOLIDIFY")
    so.thickness = thick
    so.offset = 1.0
    finish_obj(ob, base, subdiv=1)
    return ob


def outfit(base):
    W = bone_table(base)

    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot

    def wsum(i, names):
        return sum(x for b, x in W[i].items() if b in names)

    camo = camo_material("RangerCamo", [(0.055, 0.065, 0.035), (0.11, 0.1, 0.06), (0.025, 0.027, 0.018), (0.17, 0.14, 0.085)])
    glove = mat("RangerGlove", (0.025, 0.025, 0.024), 0.5, bump=0.3, bump_scale=600)
    tan = mat("RangerVest", (0.15, 0.12, 0.075), 0.72, sheen=0.3, bump=0.5, bump_scale=500)
    webbing = mat("RangerWebbing", (0.09, 0.08, 0.05), 0.75, bump=0.6, bump_scale=300)
    black = mat("RangerBlack", (0.02, 0.02, 0.022), 0.5, bump=0.2, bump_scale=400)
    olive = mat("RangerHelmet", (0.06, 0.07, 0.04), 0.62, bump=0.25, bump_scale=200)
    metal = mat("RangerMetal", (0.05, 0.05, 0.055), 0.35, metal=0.8)
    lens = bpy.data.materials.new("RangerLens")
    lens.use_nodes = True
    lp = lens.node_tree.nodes["Principled BSDF"]
    lp.inputs["Base Color"].default_value = (0.05, 0.07, 0.06, 1)
    lp.inputs["Roughness"].default_value = 0.05
    lp.inputs["Metallic"].default_value = 0.3

    hands = {b for b in BARE_BONES if b not in ("head", "neck_01")}
    legs_low = {"calf_l", "calf_r", "foot_l", "foot_r", "ball_l", "ball_r"}
    torso = {"pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
    # Treillis (un peu ample : décalage plus grand, tissu lissé), rentré dans les rangers.
    boot_top = bone_head(base, "calf_l").z - 0.1
    layer(base, "Uniform", lambda i, co, n: wsum(i, BARE_BONES) < 0.5 and (wsum(i, legs_low) < 0.75 or co.z > boot_top - 0.08),
          0.012, smooth_iter=20, smooth=0.9, mats=(camo,), subdiv=2, pin_border=True)
    layer(base, "Gloves", lambda i, co, n: wsum(i, hands) > 0.3, 0.003, smooth_iter=3, smooth=0.5, mats=(glove,))
    boots(base, W, name="RangerBoot", color=(0.018, 0.015, 0.012), rough=0.55, coat=0.15, scale=1.07, shaft_offset=0.02,
          top=boot_top)
    hide_under(base, BARE_BONES, 0.2)
    base.vertex_groups["under_suit"].add([i for i in W if wsum(i, hands) > 0.5], 1.0, "REPLACE")

    sp3, neck, hd = bone_head(base, "spine_03"), bone_head(base, "neck_01"), bone_head(base, "head")
    sp1 = bone_head(base, "spine_01")
    th = {s: bone_head(base, f"thigh_{s}") for s in "lr"}
    ca = {s: bone_head(base, f"calf_{s}") for s in "lr"}
    la = {s: bone_head(base, f"lowerarm_{s}") for s in "lr"}
    pts = [base.matrix_world @ v.co for v in body_vertices(base)]

    # Gilet porte-plaques : buste et dos, épaules en bretelles, sous les bras dégagé.
    def vest(i, co, n):
        arms = part(i, {"upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"})
        return (part(i, torso) > 0.6 and arms < 0.15 and sp1.z - 0.02 < co.z < neck.z - 0.03
                and not (abs(co.x) > 0.11 and abs(n.x) > 0.75 and co.z > sp3.z + 0.06))
    layer(base, "Vest", vest, 0.03, smooth_iter=12, smooth=0.8, mats=(tan, webbing), subdiv=1, solidify=0.016)
    # Plaques avant / arrière (plus rigides) et sangles MOLLE.
    def plate_front(i, co, n):
        return part(i, torso) > 0.7 and n.y < -0.4 and abs(co.x) < 0.12 and sp3.z - 0.12 < co.z < neck.z - 0.08
    layer(base, "PlateFront", plate_front, 0.052, smooth_iter=14, smooth=0.9, mats=(tan, webbing), subdiv=1, solidify=0.012)
    def plate_back(i, co, n):
        return part(i, torso) > 0.7 and n.y > 0.4 and abs(co.x) < 0.13 and sp3.z - 0.14 < co.z < neck.z - 0.06
    layer(base, "PlateBack", plate_back, 0.05, smooth_iter=14, smooth=0.9, mats=(tan, webbing), subdiv=1, solidify=0.012)
    for k in range(4):
        z = sp3.z - 0.09 + k * 0.045
        y = surface_point(pts, 0.0, z).y - 0.066
        b = prim("cube", f"Molle_{k}", (webbing,), (0, y, z), scale=(0.115, 0.004, 0.009))
        finish_obj(b, base, bevel=0.002, subdiv=0)
    # Poches à chargeurs (trois sur le ventre), radio (côté gauche), trousse de secours (côté droit).
    for k, x in enumerate((-0.07, 0.0, 0.07)):
        z = sp1.z + 0.06
        y = surface_point(pts, x, z).y - 0.07
        pouch(f"MagPouch_{k}", (tan, webbing), (x, y, z), (0.062, 0.035, 0.11))
    zr = sp3.z - 0.02
    pr = surface_point(pts, 0.13, zr)
    pouch("RadioPouch", (tan, webbing), (pr.x + 0.045, pr.y + 0.01, zr), (0.05, 0.045, 0.12))
    prim("cylinder", "RadioAntenna", (black,), (pr.x + 0.06, pr.y + 0.015, zr + 0.14), radius=0.004, depth=0.16, vertices=8)
    finish_obj(bpy.context.active_object, base, subdiv=0)
    pm = surface_point(pts, -0.13, zr)
    pouch("MedPouch", (mat("RangerMed", (0.42, 0.08, 0.06), 0.7, bump=0.4, bump_scale=500), webbing),
          (pm.x - 0.04, pm.y + 0.01, zr), (0.06, 0.05, 0.09))
    # Ceinturon et étui.
    def belt(i, co, n):
        return part(i, {"pelvis", "spine_01", "thigh_l", "thigh_r"}) > 0.6 and th["l"].z + 0.04 < co.z < th["l"].z + 0.095
    layer(base, "Belt", belt, 0.016, smooth_iter=8, mats=(webbing,), subdiv=1, solidify=0.01)
    ph = surface_point(pts, -0.16, th["r"].z - 0.04)
    pouch("Holster", (black,), (ph.x - 0.03, ph.y + 0.04, th["r"].z - 0.03), (0.04, 0.06, 0.16), flap=False)
    # Genouillères et coudières.
    for s in "lr":
        layer(base, f"KneePad_{s}", lambda i, co, n, s=s: (co - ca[s]).length < 0.07 and n.y < -0.3, 0.022,
              smooth_iter=8, mats=(black,), subdiv=1, solidify=0.012)
        layer(base, f"ElbowPad_{s}", lambda i, co, n, s=s: (co - la[s]).length < 0.055 and n.y > 0.1, 0.014,
              smooth_iter=6, mats=(black,), subdiv=1, solidify=0.008)

    # Casque de combat : coque (enveloppe convexe du crâne au-dessus de la ligne du bord : bord net, sans escalier), bandeau
    # et verres de lunettes de protection sur le devant, rails latéraux, support de vision nocturne.
    eyes = (group_center(base, "joint-l-eye") + group_center(base, "joint-r-eye")) / 2
    head = [p for p in pts if p.z > hd.z - 0.02 and (p - hd).length < 0.3 and abs(p.x) < 0.11]
    hc = sum(head, Vector()) / len(head)
    cut = lambda p: p.z > eyes.z + 0.04 - max(0.0, p.y - hd.y + 0.02) * 1.4 - max(0.0, abs(p.x) - 0.05) * 0.4
    trim_hair(cut)
    scalp(base, W)
    helm, hv = shell(base, "Helmet", [p for p in head if cut(p)], hc, 1.17, (olive, black))
    front_y, hx = extent(hv, eyes.z + 0.075)
    band(base, "Goggles", (black,), hd.y + 0.01, hx + 0.004, abs(front_y - hd.y - 0.01) + 0.004, eyes.z + 0.075, 0.03, 160)
    for s, sx in (("l", 1), ("r", -1)):
        g = prim("uv_sphere", f"GoggleLens_{s}", (lens,), (sx * 0.035, front_y - 0.012, eyes.z + 0.075), scale=(0.028, 0.012, 0.02),
                 segments=24, ring_count=12)
        finish_obj(g, base, subdiv=0)
        f = prim("torus", f"GoggleFrame_{s}", (black,), (sx * 0.035, front_y - 0.01, eyes.z + 0.075), rot=(math.radians(90), 0, 0),
                 scale=(1.0, 0.72, 1.0), major_radius=0.028, minor_radius=0.005)
        finish_obj(f, base, subdiv=1)
    _, hx2 = extent(hv, eyes.z + 0.1)
    for s, sx in (("l", 1), ("r", -1)):
        r = prim("cube", f"HelmetRail_{s}", (metal,), (sx * (hx2 + 0.004), hd.y + 0.01, eyes.z + 0.1), scale=(0.006, 0.05, 0.008))
        finish_obj(r, base, bevel=0.002, subdiv=0)
    fy, _ = extent(hv, eyes.z + 0.125)
    m = prim("cube", "NVGMount", (metal,), (0, fy - 0.008, eyes.z + 0.125), rot=(math.radians(-25), 0, 0), scale=(0.022, 0.012, 0.018))
    finish_obj(m, base, bevel=0.004)

    # Sac d'assaut : volume tendu, rabat, sangles de compression.
    back_y = surface_point(pts, 0.0, sp3.z, front=False).y
    y0 = back_y + 0.06
    pack = loft("Backpack", (olive, webbing), [
        (Vector((0, y0 + 0.05, sp1.z - 0.02)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.26, 0.12),
        (Vector((0, y0 + 0.07, sp3.z - 0.02)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.3, 0.16),
        (Vector((0, y0 + 0.05, neck.z - 0.08)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.26, 0.12),
    ])
    finish_obj(pack, base, bevel=0.035)
    for k, z in enumerate((sp1.z + 0.08, sp3.z + 0.05)):
        s = prim("cube", f"PackStrap_{k}", (webbing,), (0, y0 + 0.155, z), scale=(0.16, 0.006, 0.012))
        finish_obj(s, base, bevel=0.003, subdiv=0)
    pouch("PackPocket", (olive, webbing), (0, y0 + 0.16, sp1.z + 0.06), (0.18, 0.05, 0.12))
    print("[perso] tenue du Ranger construite", flush=True)
