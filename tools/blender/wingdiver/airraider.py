# Tenue de l'Air Raider (EDF 5) — exécuté par build_char.py (--char airraider), dans son espace de noms :
# opérateur d'appui : tenue de travail gris-vert, gilet tactique à poches et bandes orange, casque à visière relevée avec
# casque audio et micro, gros sac radio à antenne fouet, désignateur laser à la ceinture, gants, rangers, genouillères.
import bpy, bmesh, math, os
from mathutils import Vector

# Poches, points de surface et camouflage : mêmes outils que le Ranger.
exec(open(os.path.join(HERE, "ranger.py"), encoding="utf-8").read(), globals())

# Pièces rigides pour le jeu (préfixe du nom → os) : casque, visière, casque audio et micro sur la tête ; sac radio sur le dos.
RIGID = {"Helmet": "head", "Visor": "head", "EarCup": "head", "Mic": "head", "RadioPack": "spine_03", "RadioUnit": "spine_03",
         "RadioKnob": "spine_03", "RadioDisplay": "spine_03", "Antenna": "spine_03"}


def outfit(base):
    W = bone_table(base)

    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot

    def wsum(i, names):
        return sum(x for b, x in W[i].items() if b in names)

    cloth = camo_material("RaiderUniform", [(0.11, 0.12, 0.105), (0.16, 0.17, 0.15), (0.075, 0.085, 0.075), (0.21, 0.21, 0.18)],
                          scale=3.0)
    glove = mat("RaiderGlove", (0.03, 0.028, 0.025), 0.55, bump=0.3, bump_scale=600)
    vest = mat("RaiderVest", (0.16, 0.135, 0.08), 0.7, sheen=0.3, bump=0.5, bump_scale=500)
    webbing = mat("RaiderWebbing", (0.07, 0.06, 0.036), 0.75, bump=0.6, bump_scale=300)
    orange = mat("RaiderOrange", (0.9, 0.28, 0.02), 0.45, sheen=0.2, bump=0.3, bump_scale=400)
    hshell = mat("RaiderHelmet", (0.16, 0.17, 0.15), 0.45, coat=0.3, bump=0.1, bump_scale=200)
    black = mat("RaiderBlack", (0.012, 0.012, 0.013), 0.45, bump=0.2, bump_scale=400)
    metal = mat("RaiderMetal", (0.05, 0.05, 0.055), 0.35, metal=0.8)
    smoke = bpy.data.materials.new("RaiderVisor")
    smoke.use_nodes = True
    sp = smoke.node_tree.nodes["Principled BSDF"]
    sp.inputs["Base Color"].default_value = (0.08, 0.06, 0.02, 1)
    sp.inputs["Roughness"].default_value = 0.04
    sp.inputs["Metallic"].default_value = 0.6
    led = glow_mat("RaiderLed", (0.2, 1.0, 0.3), 12.0)

    hands = {b for b in BARE_BONES if b not in ("head", "neck_01")}
    legs_low = {"calf_l", "calf_r", "foot_l", "foot_r", "ball_l", "ball_r"}
    torso = {"pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
    boot_top = bone_head(base, "calf_l").z - 0.1
    layer(base, "Uniform", lambda i, co, n: wsum(i, BARE_BONES) < 0.5 and (wsum(i, legs_low) < 0.75 or co.z > boot_top - 0.08),
          0.012, smooth_iter=20, smooth=0.9, mats=(cloth,), subdiv=2, pin_border=True)
    layer(base, "Gloves", lambda i, co, n: wsum(i, hands) > 0.3, 0.003, smooth_iter=3, smooth=0.5, mats=(glove,))
    boots(base, W, name="RaiderBoot", color=(0.03, 0.025, 0.02), rough=0.6, coat=0.1, scale=1.07, shaft_offset=0.02, top=boot_top)
    hide_under(base, BARE_BONES, 0.2)
    base.vertex_groups["under_suit"].add([i for i in W if wsum(i, hands) > 0.5], 1.0, "REPLACE")

    sp1, sp3 = bone_head(base, "spine_01"), bone_head(base, "spine_03")
    neck, hd = bone_head(base, "neck_01"), bone_head(base, "head")
    th = {s: bone_head(base, f"thigh_{s}") for s in "lr"}
    ca = {s: bone_head(base, f"calf_{s}") for s in "lr"}
    ua = {s: bone_head(base, f"upperarm_{s}") for s in "lr"}
    pts = [base.matrix_world @ v.co for v in body_vertices(base)]

    # Gilet tactique : torse, bretelles larges ; bandes réfléchissantes orange en travers (repère des équipes d'appui).
    def vest_keep(i, co, n):
        arms = part(i, {"upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"})
        return (part(i, torso) > 0.6 and arms < 0.15 and sp1.z - 0.04 < co.z < neck.z - 0.03
                and not (abs(co.x) > 0.11 and abs(n.x) > 0.75 and co.z > sp3.z + 0.06))
    layer(base, "Vest", vest_keep, 0.026, smooth_iter=12, smooth=0.8, mats=(vest, webbing), subdiv=1, solidify=0.014)
    for k, zs in enumerate((sp3.z - 0.04, sp3.z - 0.1)):
        layer(base, f"VestStripe_{k}", lambda i, co, n, zs=zs: vest_keep(i, co, n) and abs(co.z - zs) < 0.013, 0.041,
              smooth_iter=6, smooth=0.8, mats=(orange,), subdiv=1, solidify=0.003)
    # Poches : chargeurs de PDW, radio tactique (antenne, voyant), tablette de ciblage sur le ventre.
    for k, x in enumerate((-0.09, 0.09)):
        z = sp1.z + 0.05
        y = surface_point(pts, x, z).y - 0.06
        pouch(f"MagPouch_{k}", (vest, webbing), (x, y, z), (0.055, 0.032, 0.1))
    zt = sp1.z + 0.08
    yt = surface_point(pts, 0.0, zt).y - 0.065
    tab = prim("cube", "TargetTablet", (black,), (0, yt, zt), rot=(math.radians(-12), 0, 0), scale=(0.055, 0.008, 0.04))
    finish_obj(tab, base, bevel=0.004, subdiv=1)
    scr = prim("cube", "TabletScreen", (glow_mat("RaiderScreen", (0.25, 0.6, 1.0), 3.0),), (0, yt - 0.009, zt),
               rot=(math.radians(-12), 0, 0), scale=(0.045, 0.001, 0.03))
    finish_obj(scr, base, subdiv=0)
    zr = sp3.z + 0.02
    pr = surface_point(pts, 0.12, zr)
    pouch("RadioPouch", (vest, webbing), (pr.x + 0.03, pr.y - 0.035, zr), (0.05, 0.04, 0.11))
    l = prim("uv_sphere", "RadioLed", (led,), (pr.x + 0.03, pr.y - 0.058, zr + 0.03), radius=0.005, segments=12, ring_count=6)
    finish_obj(l, base, subdiv=0)
    # Ceinturon, désignateur laser (jumelles) sur la hanche droite.
    layer(base, "Belt", lambda i, co, n: part(i, {"pelvis", "spine_01", "thigh_l", "thigh_r"}) > 0.6
          and th["l"].z + 0.04 < co.z < th["l"].z + 0.095, 0.016, smooth_iter=8, mats=(webbing,), subdiv=1, solidify=0.01)
    ph = surface_point(pts, -0.17, th["r"].z + 0.02)
    des = prim("cube", "Designator", (black,), (ph.x - 0.045, ph.y + 0.02, th["r"].z + 0.01), scale=(0.035, 0.08, 0.045))
    finish_obj(des, base, bevel=0.01, subdiv=1)
    for k, dx in enumerate((-0.016, 0.016)):
        o = prim("cylinder", f"DesignatorLens_{k}", (metal,), (ph.x - 0.045 + dx, ph.y + 0.02 - 0.085, th["r"].z + 0.01),
                 rot=(math.radians(90), 0, 0), radius=0.015, depth=0.02, vertices=20)
        finish_obj(o, base, bevel=0.002, subdiv=1)
    # Genouillères, épaulettes de gilet orange (repère).
    for s in "lr":
        layer(base, f"KneePad_{s}", lambda i, co, n, s=s: (co - ca[s]).length < 0.07 and n.y < -0.3, 0.022,
              smooth_iter=8, mats=(black,), subdiv=1, solidify=0.012)
        layer(base, f"ArmBand_{s}", lambda i, co, n, s=s: part(i, {f"upperarm_{s}"}) > 0.8 and 0.09 < (co - ua[s]).length < 0.13,
              0.016, smooth_iter=4, mats=(orange,), subdiv=1, solidify=0.004)

    # Casque : coque, visière fumée relevée sur le front, casque audio (coquilles) et micro-tige.
    eyes = (group_center(base, "joint-l-eye") + group_center(base, "joint-r-eye")) / 2
    head = [p for p in pts if p.z > hd.z - 0.02 and (p - hd).length < 0.3 and abs(p.x) < 0.11]
    hc = sum(head, Vector()) / len(head)
    cut = lambda p: p.z > eyes.z + 0.045 - max(0.0, p.y - hd.y + 0.02) * 1.4 - max(0.0, abs(p.x) - 0.05) * 0.3
    trim_hair(cut)
    scalp(base, W)
    helm, hv = shell(base, "Helmet", [p for p in head if cut(p)], hc, 1.17, (hshell, black))
    # Visière fumée relevée : arc sur le front du casque, légèrement inclinée vers l'arrière.
    front_y, hx = extent(hv, eyes.z + 0.08)
    bm = bmesh.new()
    rows = []
    for dz, r in ((0.0, 0.0), (0.05, 0.012)):
        rows.append([bm.verts.new((math.sin(a) * (hx + 0.012 - r), hc.y - math.cos(a) * (abs(front_y - hc.y) + 0.014 - r), eyes.z + 0.065 + dz))
                     for a in (math.radians(-75 + 150 * k / 16) for k in range(17))])
    for k in range(16):
        bm.faces.new((rows[0][k], rows[0][k + 1], rows[1][k + 1], rows[1][k]))
    me = bpy.data.meshes.new("Visor")
    bm.to_mesh(me)
    bm.free()
    vo = bpy.data.objects.new("Visor", me)
    bpy.context.collection.objects.link(vo)
    me.materials.append(smoke)
    so = vo.modifiers.new("Epaisseur", "SOLIDIFY")
    so.thickness = 0.004
    finish_obj(vo, base, subdiv=2)
    for s, sx in (("l", 1), ("r", -1)):
        pv = prim("cylinder", f"VisorPivot_{s}", (black,), (sx * (hx + 0.012), hc.y - 0.01, eyes.z + 0.07), rot=(0, math.radians(90), 0),
                  radius=0.014, depth=0.01, vertices=20)
        finish_obj(pv, base, bevel=0.002, subdiv=1)
    ear_z = eyes.z - 0.03
    ear_y = hd.y + 0.015
    ear_x = max((abs(p.x) for p in pts if abs(p.z - ear_z) < 0.01 and abs(p.y - ear_y) < 0.03 and p.z > hd.z), default=0.075)
    for s, sx in (("l", 1), ("r", -1)):
        cup = prim("cylinder", f"EarCup_{s}", (black,), (sx * (ear_x + 0.022), ear_y, ear_z), rot=(0, math.radians(90), 0),
                   radius=0.04, depth=0.035, vertices=32)
        finish_obj(cup, base, bevel=0.01, subdiv=1)
        pad = prim("cylinder", f"EarCupRing_{s}", (hshell,), (sx * (ear_x + 0.041), ear_y, ear_z), rot=(0, math.radians(90), 0),
                   radius=0.03, depth=0.006, vertices=32)
        finish_obj(pad, base, bevel=0.002, subdiv=1)
    # Micro-tige : de la coquille droite vers le coin de la bouche.
    mouth = group_center(base, "mouth") if base.vertex_groups.get("mouth") else Vector((0, front_y + 0.01, eyes.z - 0.075))
    a = Vector((-(ear_x + 0.03), ear_y - 0.02, ear_z - 0.02))
    b = Vector((-0.035, mouth.y - 0.025, eyes.z - 0.085))
    mid = (a + b) / 2 + Vector((-0.03, 0, -0.01))
    boom = loft("MicBoom", (black,), [(a, Vector((0, 0, 1)), Vector((1, 0, 0)), 0.006, 0.006),
                                      (mid, Vector((0, 0, 1)), Vector((1, 0, 0)), 0.005, 0.005),
                                      (b, Vector((0, 0, 1)), Vector((1, 0, 0)), 0.005, 0.005)])
    finish_obj(boom, base, subdiv=1)
    mic = prim("uv_sphere", "Mic", (black,), tuple(b), scale=(0.011, 0.014, 0.009), segments=16, ring_count=8)
    finish_obj(mic, base, subdiv=0)

    # Sac radio : grand boîtier (relais satellite pour les frappes), antenne fouet, combiné sur la bretelle.
    back_y = surface_point(pts, 0.0, sp3.z, front=False).y
    y0 = back_y + 0.05
    X, Y = Vector((1, 0, 0)), Vector((0, 1, 0))
    pack = loft("RadioPack", (vest, webbing), [
        (Vector((0, y0 + 0.06, sp1.z - 0.04)), X, Y, 0.3, 0.14),
        (Vector((0, y0 + 0.08, sp3.z)), X, Y, 0.34, 0.18),
        (Vector((0, y0 + 0.07, neck.z - 0.04)), X, Y, 0.32, 0.16),
    ])
    finish_obj(pack, base, bevel=0.03)
    box = prim("cube", "RadioUnit", (black,), (0, y0 + 0.18, sp3.z - 0.02), scale=(0.11, 0.03, 0.13))
    finish_obj(box, base, bevel=0.008, subdiv=1)
    for k in range(4):
        kn = prim("cylinder", f"RadioKnob_{k}", (metal,), (-0.06 + k * 0.04, y0 + 0.21, sp3.z + 0.07), rot=(math.radians(90), 0, 0),
                  radius=0.008, depth=0.012, vertices=16)
        finish_obj(kn, base, subdiv=0)
    led2 = prim("cube", "RadioDisplay", (led,), (0, y0 + 0.212, sp3.z + 0.03), scale=(0.045, 0.002, 0.012))
    finish_obj(led2, base, subdiv=0)
    ant_base = Vector((0.11, y0 + 0.1, neck.z + 0.02))
    prim("cylinder", "AntennaBase", (metal,), tuple(ant_base), radius=0.012, depth=0.05, vertices=16)
    finish_obj(bpy.context.active_object, base, bevel=0.002, subdiv=1)
    whip = loft("AntennaWhip", (black,), [(ant_base + Vector((0, 0, 0.02)), X, Y, 0.006, 0.006),
                                          (ant_base + Vector((0.01, 0.03, 0.4)), X, Y, 0.004, 0.004),
                                          (ant_base + Vector((0.025, 0.09, 0.75)), X, Y, 0.0025, 0.0025)])
    finish_obj(whip, base, subdiv=1)
    hs = surface_point(pts, 0.08, neck.z - 0.08)
    hand = prim("cube", "Handset", (black,), (hs.x + 0.01, hs.y - 0.045, neck.z - 0.09), rot=(0, 0, math.radians(10)),
                scale=(0.022, 0.018, 0.06))
    finish_obj(hand, base, bevel=0.008, subdiv=1)
    print("[perso] tenue de l'Air Raider construite", flush=True)
