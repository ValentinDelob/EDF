# Tenue du Fencer (EDF 5) — exécuté par build_char.py (--char fencer), dans son espace de noms :
# exosquelette lourd (« Power Skeleton ») : sous-combinaison sombre, coques épaisses par segment de membre, épaulières massives,
# cuirasse, gantelets, bottes renforcées, casque intégral à fente de visée lumineuse, groupe d'alimentation dorsal à ventilations
# et propulseurs latéraux. Gris acier, liserés orange.
import bpy, bmesh, math, os
from mathutils import Vector

# Coques, bandeaux et poches : outils communs (ranger.py).
exec(open(os.path.join(HERE, "ranger.py"), encoding="utf-8").read(), globals())


def hull(name, pts, mats, scale=1.0, center=None, squash=(1, 1, 1)):
    """Coque en enveloppe convexe des points (agrandie de scale autour du centre) : pièces de blindage massives et lisses."""
    c = center if center is not None else sum(pts, Vector()) / len(pts)
    bm = bmesh.new()
    for p in pts:
        d = (p - c) * scale
        bm.verts.new(c + Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.004)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    return ob


def rod(base, name, mats, a, b, r, vertices=16):
    """Cylindre de a à b (vérins, tubes)."""
    d = b - a
    ob = prim("cylinder", name, mats, tuple((a + b) / 2), rot=tuple(d.to_track_quat("Z", "Y").to_euler()), radius=r, depth=d.length,
              vertices=vertices)
    return finish_obj(ob, base, bevel=min(r * 0.3, 0.003), subdiv=1)


def outfit(base):
    W = bone_table(base)

    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot

    def wsum(i, names):
        return sum(x for b, x in W[i].items() if b in names)

    def side(co):
        return "l" if co.x > 0 else "r"

    def seg_t(co, a, b):
        ab = b - a
        return (co - a).dot(ab) / ab.length_squared

    under = mat("FencerUnder", (0.03, 0.032, 0.035), 0.55, sheen=0.3, bump=0.4, bump_scale=700)
    steel = mat("FencerSteel", (0.075, 0.082, 0.09), 0.42, metal=0.35, coat=0.35, bump=0.05, bump_scale=150)
    dark = mat("FencerDark", (0.05, 0.052, 0.056), 0.4, metal=0.7)
    orange = mat("FencerOrange", (0.85, 0.32, 0.04), 0.35, coat=0.5)
    rubber = mat("FencerRubber", (0.015, 0.015, 0.016), 0.7, bump=0.5, bump_scale=250)
    visor = glow_mat("FencerVisor", (1.0, 0.45, 0.08), 18.0)

    hands = {b for b in BARE_BONES if b not in ("head", "neck_01")}
    # Sous-combinaison intégrale (mains gantées, tête sous le casque).
    layer(base, "Under", lambda i, co, n: wsum(i, {"head"}) < 0.5, 0.004, smooth_iter=8, smooth=0.7, mats=(under,), subdiv=2)
    hide_under(base, set(), 0.5)
    base.vertex_groups["under_suit"].add([v.index for v in base.data.vertices], 1.0, "REPLACE")
    for o in bpy.data.objects:
        # Cheveux, sourcils et cils : cachés sous le casque intégral.
        if o.type == "MESH" and o is not base and o.parent == base.parent and any(
                k in o.name.lower() for k in (CFG["hair"], CFG["brows"], CFG["lashes"], "eye")):
            o.hide_render = o.hide_viewport = True

    pts = {v.index: base.matrix_world @ v.co for v in body_vertices(base)}
    sp1, sp3 = bone_head(base, "spine_01"), bone_head(base, "spine_03")
    neck, hd = bone_head(base, "neck_01"), bone_head(base, "head")
    ua = {s: bone_head(base, f"upperarm_{s}") for s in "lr"}
    la = {s: bone_head(base, f"lowerarm_{s}") for s in "lr"}
    hn = {s: bone_head(base, f"hand_{s}") for s in "lr"}
    th = {s: bone_head(base, f"thigh_{s}") for s in "lr"}
    ca = {s: bone_head(base, f"calf_{s}") for s in "lr"}
    ft = {s: bone_head(base, f"foot_{s}") for s in "lr"}

    # Cuirasse : coque pleine du buste (avant et dos), dessus et dessous en acier sombre ; ventrière en deux lames.
    chest = [p for i, p in pts.items() if part(i, {"spine_02", "spine_03", "clavicle_l", "clavicle_r"}) > 0.5
             and part(i, {"upperarm_l", "upperarm_r"}) < 0.1 and sp3.z - 0.1 < p.z < neck.z - 0.01]
    cu = hull("Cuirass", chest, (steel, dark), scale=1.14, squash=(1.0, 1.05, 1.0))
    for f in cu.data.polygons:
        f.material_index = 1 if abs(f.normal.z) > 0.65 else 0
    finish_obj(cu, base, bevel=0.015, subdiv=2)
    for k, (z0, z1) in enumerate(((sp1.z + 0.045, sp3.z - 0.08), (sp1.z - 0.03, sp1.z + 0.055))):
        ab = [p for i, p in pts.items() if part(i, {"spine_01", "spine_02", "pelvis"}) > 0.5 and z0 < p.z < z1
              and part(i, {"thigh_l", "thigh_r"}) < 0.2]
        a = hull(f"AbPlate_{k}", ab, (dark, steel), scale=1.12 - 0.02 * k)
        for f in a.data.polygons:
            f.material_index = 1 if f.normal.y < -0.5 else 0
        finish_obj(a, base, bevel=0.01, subdiv=2)
    layer(base, "Belt", lambda i, co, n: part(i, {"pelvis", "spine_01", "thigh_l", "thigh_r"}) > 0.6
          and th["l"].z + 0.02 < co.z < th["l"].z + 0.08, 0.026, smooth_iter=8, mats=(dark, orange), subdiv=1, solidify=0.016)
    # Plaques de hanches et tassettes.
    layer(base, "Tassets", lambda i, co, n: part(i, {"pelvis", "thigh_l", "thigh_r"}) > 0.6 and abs(n.x) > 0.4 and co.x * n.x > 0
          and th["l"].z - 0.15 < co.z < th["l"].z + 0.03, 0.035, smooth_iter=8, mats=(steel, orange), subdiv=2, solidify=0.014)

    # Épaulières massives (coques convexes au-dessus de l'épaule, liseré orange) et protège-bras.
    for s in "lr":
        sx = 1 if s == "l" else -1
        sh = [p for i, p in pts.items() if (p - ua[s]).length < 0.14 and p.z > ua[s].z - 0.05 and p.x * sx > abs(ua[s].x) - 0.05]
        c = ua[s] + Vector((sx * 0.03, 0, 0.03))
        ob = hull(f"Pauldron_{s}", sh, (steel,), scale=1.62, center=c, squash=(1.12, 1.08, 0.85))
        finish_obj(ob, base, bevel=0.012, subdiv=2)
        arm = [p for i, p in pts.items() if part(i, {f"upperarm_{s}"}) > 0.8 and 0.35 < seg_t(p, ua[s], la[s]) < 0.85]
        finish_obj(hull(f"Rerebrace_{s}", arm, (steel,), scale=1.28), base, bevel=0.008, subdiv=2)
        # Gantelet : avant-bras massif (moteur des armes lourdes), plaque de dos de main.
        fa = [p for i, p in pts.items() if part(i, {f"lowerarm_{s}"}) > 0.7 and 0.1 < seg_t(p, la[s], hn[s]) < 0.98]
        g = hull(f"Gauntlet_{s}", fa, (steel,), scale=1.75, squash=(1.1, 1.1, 1.0))
        finish_obj(g, base, bevel=0.01, subdiv=2)
        hand = [p for i, p in pts.items() if part(i, {f"hand_{s}"}) > 0.8 and (p - hn[s]).length < 0.07]
        finish_obj(hull(f"HandPlate_{s}", hand, (dark,), scale=1.15), base, bevel=0.005, subdiv=1)
        # Coudière.
        elbow = [p for i, p in pts.items() if (p - la[s]).length < 0.06]
        finish_obj(hull(f"Elbow_{s}", elbow, (dark,), scale=1.3), base, bevel=0.006, subdiv=2)
        # Cuissard (avant et côtés), genouillère saillante, jambière.
        thigh = [p for i, p in pts.items() if part(i, {f"thigh_{s}"}) > 0.8 and 0.2 < seg_t(p, th[s], ca[s]) < 0.85]
        finish_obj(hull(f"Cuisse_{s}", thigh, (steel, dark), scale=1.18), base, bevel=0.01, subdiv=2)
        knee = [p for i, p in pts.items() if (p - ca[s]).length < 0.075 and p.y < ca[s].y + 0.01]
        k = hull(f"Knee_{s}", knee, (orange, dark), scale=1.5, center=ca[s] + Vector((0, 0.02, 0)))
        for f in k.data.polygons:
            f.material_index = 0 if f.normal.y < -0.6 else 1
        finish_obj(k, base, bevel=0.01, subdiv=2)
        shin = [p for i, p in pts.items() if part(i, {f"calf_{s}"}) > 0.8 and 0.15 < seg_t(p, ca[s], ft[s]) < 0.9]
        finish_obj(hull(f"Greave_{s}", shin, (steel,), scale=1.25), base, bevel=0.01, subdiv=2)
    # Vérins de l'exosquelette : corps sombre et tige chromée le long de l'extérieur des cuisses et des jambes.
    chrome = mat("FencerPiston", (0.6, 0.6, 0.62), 0.15, metal=1.0)
    for s in "lr":
        sx = 1 if s == "l" else -1
        for k, (a, b, off) in enumerate(((th[s] + Vector((0, 0, -0.06)), ca[s] + Vector((0, 0, 0.05)), 0.112),
                                         (ca[s] + Vector((0, 0, -0.04)), ft[s] + Vector((0, 0, 0.1)), 0.088))):
            a = a + Vector((sx * off, 0.01, 0))
            b = b + Vector((sx * off, 0.01, 0))
            m = a.lerp(b, 0.55)
            rod(base, f"Piston_{s}{k}", (dark,), a, m, 0.017)
            rod(base, f"PistonRod_{s}{k}", (chrome,), m, b, 0.009)
            for q in (a, b):
                j = prim("uv_sphere", f"PistonJoint_{s}{k}", (dark,), tuple(q), radius=0.02, segments=16, ring_count=8)
                finish_obj(j, base, subdiv=0)
    boots(base, W, name="FencerBoot", color=(0.06, 0.062, 0.066), rough=0.4, coat=0.2, scale=1.22, shaft_offset=0.03)

    # Casque intégral : coque convexe de la tête, fente de visée lumineuse, mentonnière, crête.
    eyes = (group_center(base, "joint-l-eye") + group_center(base, "joint-r-eye")) / 2
    head = [p for i, p in pts.items() if part(i, {"head"}) > 0.7]
    hc = sum(head, Vector()) / len(head)
    # Coque arrondie (ellipsoïde ajusté au crâne) et mentonnière anguleuse (enveloppe du bas du visage).
    lo = Vector((min(p.x for p in head), min(p.y for p in head), min(p.z for p in head)))
    hi = Vector((max(p.x for p in head), max(p.y for p in head), max(p.z for p in head)))
    ctr = (lo + hi) / 2 + Vector((0, 0.008, 0.02))
    h = prim("uv_sphere", "Helmet", (steel,), tuple(ctr), scale=tuple((hi - lo) / 2 * 1.13), segments=48, ring_count=24)
    hv = [ctr + v.co for v in h.data.vertices]
    finish_obj(h, base, subdiv=1)
    jaw = [p for p in head if p.z < eyes.z - 0.025 and p.y < hc.y]
    j = hull("ChinGuard", jaw, (dark,), scale=1.3, center=hc + Vector((0, 0.02, 0)))
    finish_obj(j, base, bevel=0.008, subdiv=2)
    # Visière : bandeau sombre enveloppant à hauteur des yeux, fente lumineuse au milieu ; crête sur le sommet.
    front_y, hx = extent(hv, eyes.z)
    band(base, "VisorFrame", (dark,), ctr.y, hx + 0.002, abs(front_y - ctr.y) + 0.002, eyes.z, 0.04, 170, thick=0.008)
    band(base, "Visor", (visor,), ctr.y, hx + 0.01, abs(front_y - ctr.y) + 0.01, eyes.z + 0.002, 0.012, 130, thick=0.003)
    top = max(v.z for v in hv)
    # Crête : lame orange qui suit le profil du sommet du casque (de l'avant vers la nuque).
    prof = sorted((v for v in hv if abs(v.x) < 0.004 and v.z > ctr.z + 0.02 and v.y > front_y + 0.03), key=lambda v: v.y)
    secs = []
    for v in prof:
        nrm = (v - ctr).normalized()
        secs.append((v + nrm * 0.008, Vector((1, 0, 0)), nrm, 0.018, 0.02))
    crest = loft("Crest", (orange,), secs)
    finish_obj(crest, base, bevel=0.003, subdiv=1)
    # Gorgerin : collier épais entre casque et cuirasse.
    nk = [p for i, p in pts.items() if part(i, {"neck_01"}) > 0.5]
    finish_obj(hull("Gorget", nk, (dark,), scale=1.45, squash=(1, 1, 0.8)), base, bevel=0.008, subdiv=2)

    # Groupe d'alimentation dorsal : bloc central, deux propulseurs latéraux à buses, grilles de ventilation.
    by = max(v.co.y for v in cu.data.vertices) - 0.02
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    core = loft("PowerUnit", (steel, dark), [
        (Vector((0, by + 0.05, sp1.z + 0.02)), X, Y, 0.24, 0.1),
        (Vector((0, by + 0.075, sp3.z)), X, Y, 0.32, 0.15),
        (Vector((0, by + 0.06, neck.z - 0.02)), X, Y, 0.28, 0.12),
    ])
    finish_obj(core, base, bevel=0.03, subdiv=2)
    for k in range(5):
        v = prim("cube", f"Vent_{k}", (dark,), (0, by + 0.152, sp3.z - 0.06 + k * 0.026), scale=(0.1, 0.006, 0.006))
        finish_obj(v, base, bevel=0.002, subdiv=0)
    for s, sx in (("l", 1), ("r", -1)):
        t = prim("cylinder", f"Booster_{s}", (steel, orange), (sx * 0.2, by + 0.09, sp3.z - 0.02), radius=0.05, depth=0.28, vertices=24)
        finish_obj(t, base, bevel=0.008, subdiv=1)
        n = prim("cone", f"BoosterNozzle_{s}", (dark,), (sx * 0.2, by + 0.09, sp3.z - 0.2), radius1=0.06, radius2=0.042, depth=0.08,
                 vertices=24)
        finish_obj(n, base, bevel=0.004, subdiv=1)
        ring = prim("torus", f"BoosterRing_{s}", (orange,), (sx * 0.2, by + 0.09, sp3.z + 0.07), major_radius=0.052, minor_radius=0.008)
        finish_obj(ring, base, subdiv=1)
        arm = loft(f"BoosterMount_{s}", (dark,), [
            (Vector((sx * 0.1, by + 0.08, sp3.z)), Z, Y, 0.12, 0.05),
            (Vector((sx * 0.16, by + 0.09, sp3.z)), Z, Y, 0.1, 0.04),
        ])
        finish_obj(arm, base, bevel=0.01, subdiv=1)
        glow = prim("cylinder", f"BoosterGlow_{s}", (visor,), (sx * 0.2, by + 0.09, sp3.z - 0.238), radius=0.04, depth=0.004, vertices=24)
        finish_obj(glow, base, subdiv=0)
    print("[perso] tenue du Fencer construite", flush=True)
