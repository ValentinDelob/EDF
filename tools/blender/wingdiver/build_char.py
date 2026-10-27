# Wing Diver (personnage féminin réaliste) construite dans Blender en arrière-plan, à partir de MPFB 2 (MakeHuman, actifs CC0).
#   BLENDER_USER_RESOURCES=.mpfb/res  blender -b --python tools/blender/wingdiver/build_char.py -- --stage N --preview DOSSIER [--save F.blend]
# Étapes : 1 corps, visage et cheveux sous une combinaison neutre ; 2 combinaison de la Wing Diver ; 3 armure, casque, réacteur ;
# 4 textures et matériaux finaux. --preview : rendus de contrôle (face, trois-quarts, profil, dos, portrait).
# MPFB 2 et les actifs système de MakeHuman (CC0) doivent être installés dans BLENDER_USER_RESOURCES (dossier .mpfb, hors Git).
import bpy, bmesh, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default
STAGE = int(arg("--stage", "1"))
PREVIEW = arg("--preview")
SAVE = arg("--save")
HERE = os.path.dirname(os.path.abspath(__file__))

from bl_ext.user_default.mpfb.services.humanservice import HumanService


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_human():
    """Corps féminin athlétique et harmonieux (jeune adulte, ~1,70 m), peau réaliste, yeux, sourcils, cils, dents, queue de cheval."""
    info = HumanService._create_default_human_info_dict()
    info["phenotype"].update({
        "gender": 0.0, "age": 0.42, "muscle": 0.62, "weight": 0.42, "proportions": 0.9, "height": 0.62,
        "cupsize": 0.62, "firmness": 0.8,
        "race": {"caucasian": 0.7, "asian": 0.2, "african": 0.1},
    })
    info["rig"] = "game_engine"
    info["skin_mhmat"] = "young_caucasian_female/young_caucasian_female.mhmat"
    info["skin_material_type"] = "ENHANCED_SSS"
    info["eyes"] = "high-poly/high-poly.mhclo"
    info["eyes_material_type"] = "MAKESKIN"
    info["eyebrows"] = "eyebrow010/eyebrow010.mhclo"
    info["eyelashes"] = "eyelashes02/eyelashes02.mhclo"
    info["teeth"] = "teeth_base/teeth_base.mhclo"
    info["tongue"] = "tongue01/tongue01.mhclo"
    info["hair"] = "ponytail01/ponytail01.mhclo"
    info["alternative_materials"] = {}
    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 1
    base = HumanService.deserialize_from_dict(info, settings)
    bake_shape(base)
    fix_materials(base)
    body = base.vertex_groups["body"].index
    top = max((base.matrix_world @ v.co).z for v in base.data.vertices if any(g.group == body and g.weight > 0.5 for g in v.groups))
    print(f"[perso] taille du corps : {top:.3f} m", flush=True)
    return base


def bake_shape(base):
    """Morphologie MPFB (clés de forme des cibles) appliquée au maillage : les mesures et les couches suivent le corps final."""
    if not base.data.shape_keys:
        return
    mix = base.shape_key_add(name="mix", from_mix=True)
    co = [d.co.copy() for d in mix.data]
    base.shape_key_clear()
    for v, c in zip(base.data.vertices, co):
        v.co = c
    base.data.update()


def fix_materials(base):
    """Peau : un seul matériau sur tout le corps (lèvres, oreilles…) ; cheveux, cils, sourcils : transparence par tramage."""
    print("[perso] matériaux du corps : " + ", ".join(f"{i}:{m.name if m else None}" for i, m in enumerate(base.data.materials)), flush=True)
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            print(f"[perso] objet {ob.name} : " + ", ".join(m.name if m else "None" for m in ob.data.materials), flush=True)
    for ob in bpy.data.objects:
        if ob.type != "MESH" or ob == base:
            continue
        if "ponytail" in ob.name.lower() or "hair" in ob.name.lower():
            hair_material(ob)
            continue
        if "high-poly" in ob.name.lower():
            eye_texture(ob, "brownlight_eye.png")
        for m in ob.data.materials:
            if not m:
                continue
            if hasattr(m, "surface_render_method"):
                m.surface_render_method = "DITHERED"
            if hasattr(m, "use_transparency_overlap"):
                m.use_transparency_overlap = True
            if hasattr(m, "blend_method"):
                try:
                    m.blend_method = "HASHED"
                except Exception:
                    pass


def eye_texture(ob, png):
    """Iris : texture d'œil MakeHuman (CC0) choisie."""
    path = os.path.join(os.environ.get("BLENDER_USER_RESOURCES", ""), "extensions", ".user", "user_default", "mpfb", "data", "eyes",
                        "materials", png)
    if not os.path.exists(path):
        return
    img = bpy.data.images.load(path)
    for m in ob.data.materials:
        if m and m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image and not any(k in n.image.name.lower() for k in ("normal", "bump")):
                    print(f"[perso] œil : {n.image.name} → {png}", flush=True)
                    n.image = img


def hair_material(ob):
    """Cheveux châtain foncé : texture de mèches (alpha) teintée, reflets doux, transparence par tramage."""
    tex = None
    for m in ob.data.materials:
        if m and m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image:
                    tex = n.image
    m = bpy.data.materials.new("HairWD")
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = 0.42
    p.inputs["Base Color"].default_value = (0.085, 0.05, 0.03, 1)
    if tex:
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = tex
        hsv = nt.nodes.new("ShaderNodeHueSaturation")
        hsv.inputs["Saturation"].default_value = 0.35
        hsv.inputs["Value"].default_value = 0.75
        nt.links.new(t.outputs["Color"], hsv.inputs["Color"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs["B"].default_value = (0.36, 0.22, 0.13, 1)
        nt.links.new(hsv.outputs["Color"], mix.inputs["A"])
        nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
        nt.links.new(t.outputs["Alpha"], p.inputs["Alpha"])
    if hasattr(m, "surface_render_method"):
        m.surface_render_method = "DITHERED"
    ob.data.materials.clear()
    ob.data.materials.append(m)


def bone_weights(obj):
    """Os dominant par sommet (seuls les groupes qui sont des os du squelette comptent : pas « lips », « ears »…)."""
    bones = {b.name for b in obj.parent.data.bones} if obj.parent and obj.parent.type == "ARMATURE" else None
    names = {g.index: g.name for g in obj.vertex_groups if bones is None or g.name in bones}
    dom = {}
    for v in obj.data.vertices:
        best, bw = None, 0.0
        for g in v.groups:
            n = names.get(g.group, "")
            if g.weight > bw and not n.startswith(("joint-", "helper-")) and n not in ("body", "Left", "Right", "Mid", "HelperGeometry", "JointCubes"):
                best, bw = n, g.weight
        dom[v.index] = best
    return dom


def region_weight(obj, bones):
    """Poids total de chaque sommet sur les os donnés."""
    idx = {g.index for g in obj.vertex_groups if g.name in bones}
    return {v.index: sum(g.weight for g in v.groups if g.group in idx) for v in obj.data.vertices}


# Os laissés nus par la combinaison : tête, haut du cou, mains.
BARE_BONES = {"head", "neck_01", "hand_l", "hand_r"} | {f"{f}_0{i}_{s}" for f in ("thumb", "index", "middle", "ring", "pinky")
                                                        for i in (1, 2, 3) for s in ("l", "r")}


def bodysuit(base, name, offset, bones, threshold=0.5):
    """Couche moulante tirée du corps : sommets pesant moins de threshold sur les os nus donnés, décalée de offset le long des normales."""
    me = base.data.copy()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.matrix_world = base.matrix_world
    w = region_weight(base, bones)
    bm = bmesh.new()
    bm.from_mesh(me)
    body_idx = base.vertex_groups["body"].index
    in_body = {v.index for v in base.data.vertices if any(g.group == body_idx and g.weight > 0.5 for g in v.groups)}
    kill = [v for v in bm.verts if v.index not in in_body or w.get(v.index, 0) >= threshold]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    bm.to_mesh(me)
    bm.free()
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    # Tissu tendu : les petits reliefs du corps (nombril…) s'effacent, la silhouette reste.
    sm = ob.modifiers.new("Tissu", "SMOOTH")
    sm.factor = 0.8
    sm.iterations = 8
    sub = ob.modifiers.new("Subdiv", "SUBSURF")
    sub.levels = sub.render_levels = 1
    if base.parent:
        ob.parent = base.parent
        ob.matrix_parent_inverse = base.parent.matrix_world.inverted()
    return ob


def bone_table(obj):
    """Poids par os de chaque sommet : {indice: {os: poids}} (groupes du squelette seulement)."""
    bones = {b.name for b in obj.parent.data.bones} if obj.parent and obj.parent.type == "ARMATURE" else set()
    names = {g.index: g.name for g in obj.vertex_groups if g.name in bones}
    return {v.index: {names[g.group]: g.weight for g in v.groups if g.group in names} for v in obj.data.vertices}


def mat(name, color, rough=0.5, metal=0.0, coat=0.0, sheen=0.0, bump=0.0, bump_scale=900.0):
    """Matériau PBR simple ; bump : grain de tissu ou de polymère (bruit fin)."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if coat:
        p.inputs["Coat Weight"].default_value = coat
        p.inputs["Coat Roughness"].default_value = 0.15
    if sheen:
        p.inputs["Sheen Weight"].default_value = sheen
        p.inputs["Sheen Roughness"].default_value = 0.4
    if bump:
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = bump_scale
        nz.inputs["Detail"].default_value = 4.0
        bp = nt.nodes.new("ShaderNodeBump")
        bp.inputs["Strength"].default_value = bump
        bp.inputs["Distance"].default_value = 0.0005
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
        nt.links.new(nz.outputs["Fac"], bp.inputs["Height"])
        nt.links.new(bp.outputs["Normal"], p.inputs["Normal"])
    if STAGE >= 4:
        # Finition : crasse légère dans les creux (occlusion ambiante) et variation de teinte à grande échelle.
        ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
        ao.inputs["Distance"].default_value = 0.04
        ao.inputs["Color"].default_value = (*color, 1)
        dirt = nt.nodes.new("ShaderNodeMix")
        dirt.data_type = "RGBA"
        dirt.blend_type = "MULTIPLY"
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(ao.outputs["AO"], inv.inputs[1])
        k = nt.nodes.new("ShaderNodeMath")
        k.operation = "MULTIPLY"
        k.inputs[1].default_value = 0.55
        nt.links.new(inv.outputs[0], k.inputs[0])
        nt.links.new(k.outputs[0], dirt.inputs["Factor"])
        nt.links.new(ao.outputs["Color"], dirt.inputs["A"])
        dirt.inputs["B"].default_value = (0.32, 0.3, 0.28, 1)
        nt.links.new(dirt.outputs["Result"], p.inputs["Base Color"])
    return m


def stencil(text, name, material, loc, rot, size, target=None):
    """Marquage au pochoir (texte en relief de 0,3 mm) plaqué sur la surface d'une pièce."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = text
    cu.size = size
    cu.extrude = 0.0003
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = [math.radians(a) for a in rot]
    ob.data.materials.append(material)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target="MESH")
    ob = bpy.context.active_object
    if target:
        sw = ob.modifiers.new("Plaquage", "SHRINKWRAP")
        sw.target = target
        sw.wrap_method = "NEAREST_SURFACEPOINT"
        sw.offset = 0.0008
    return ob


def layer(base, name, keep, offset, smooth_iter=6, smooth=0.8, classify=None, mats=(), subdiv=1, solidify=0.0, attrs=None):
    """
    Couche tirée du corps : sommets i tels que keep(i, co, normale) ; décalée de offset le long des normales, lissée, subdivisée.
    classify(centre de face, normale, indices d'origine) → indice de matériau dans mats.
    """
    me = base.data.copy()
    me.materials.clear()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.matrix_world = base.matrix_world
    body_idx = base.vertex_groups["body"].index
    in_body = {v.index for v in base.data.vertices if any(g.group == body_idx and g.weight > 0.5 for g in v.groups)}
    bm = bmesh.new()
    bm.from_mesh(me)
    lay = bm.verts.layers.int.new("orig")
    for v in bm.verts:
        v[lay] = v.index
    bm.normal_update()
    kill = [v for v in bm.verts if v.index not in in_body or not keep(v.index, v.co, v.normal)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    # Pièces isolées de quelques faces (éclats au bord de la sélection) : retirées.
    loose = [f for f in bm.faces if all(len(e.link_faces) < 2 for e in f.edges)]
    bmesh.ops.delete(bm, geom=loose, context="FACES")
    bm.normal_update()
    if solidify:
        # Plaques : bord adouci (lissage le long du contour : courbe nette au lieu d'un escalier de faces).
        border = [v for v in bm.verts if v.is_boundary]
        for _ in range(12):
            new = {}
            for v in border:
                nb = [e.other_vert(v) for e in v.link_edges if e.is_boundary]
                if len(nb) == 2:
                    new[v] = v.co * 0.5 + (nb[0].co + nb[1].co) * 0.25
            for v, c in new.items():
                v.co = c
        bm.normal_update()
    # Champs par sommet, calculés sur la surface du corps (panneaux de couleur continus : la limite est tracée dans le
    # matériau et reste courbe après subdivision).
    for aname, fn in (attrs or {}).items():
        fl = bm.verts.layers.float.new(aname)
        for v in bm.verts:
            v[fl] = max(0.0, min(1.0, fn(v[lay], v.co, v.normal)))
    for v in bm.verts:
        v.co += v.normal * offset
    for m in mats:
        me.materials.append(m)
    if classify:
        for f in bm.faces:
            f.material_index = classify(f.calc_center_median(), f.normal, [v[lay] for v in f.verts])
    bm.to_mesh(me)
    bm.free()
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    if smooth_iter:
        sm = ob.modifiers.new("Lissage", "SMOOTH")
        sm.factor = smooth
        sm.iterations = smooth_iter
        if solidify:
            # Plaques : le lissage épargne le contour (déjà adouci le long du bord), qui reste net.
            inner = ob.vertex_groups.new(name="interieur")
            border = {i for e in ob.data.edges for i in e.vertices if e.use_seam} if False else set()
            counts = {}
            for e in ob.data.edges:
                key = tuple(sorted(e.vertices))
                counts[key] = 0
            for poly in ob.data.polygons:
                for ek in poly.edge_keys:
                    counts[tuple(sorted(ek))] = counts.get(tuple(sorted(ek)), 0) + 1
            border = {i for k, c in counts.items() if c == 1 for i in k}
            inner.add([v.index for v in ob.data.vertices if v.index not in border], 1.0, "REPLACE")
            sm.vertex_group = "interieur"
    if solidify:
        so = ob.modifiers.new("Epaisseur", "SOLIDIFY")
        so.thickness = solidify
        so.offset = 1.0
        so.use_even_offset = True
        # Tranche des plaques dans le second matériau (liseré).
        if len(mats) > 1 and not classify:
            so.material_offset_rim = 1
    if subdiv:
        sub = ob.modifiers.new("Subdiv", "SUBSURF")
        sub.levels = sub.render_levels = subdiv
    for p in ob.data.polygons:
        p.use_smooth = True
    if base.parent:
        ob.parent = base.parent
        ob.matrix_parent_inverse = base.parent.matrix_world.inverted()
    return ob


def hide_under(base, bare, threshold):
    """Le corps est masqué sous la combinaison (pas de peau qui traverse le tissu) ; une marge reste visible aux bords."""
    w = region_weight(base, bare)
    g = base.vertex_groups.get("under_suit") or base.vertex_groups.new(name="under_suit")
    g.add([i for i, x in w.items() if x < threshold], 1.0, "REPLACE")
    m = base.modifiers.new("Sous la combinaison", "MASK")
    m.vertex_group = "under_suit"
    m.invert_vertex_group = True


def neutral_suit(base):
    """Étape 1 : combinaison grise unie (couvre le corps sauf tête, cou haut et mains)."""
    ob = bodysuit(base, "SuitBase", 0.004, BARE_BONES)
    hide_under(base, BARE_BONES, 0.2)
    m = bpy.data.materials.new("SuitNeutral")
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (0.2, 0.21, 0.23, 1)
    p.inputs["Roughness"].default_value = 0.55
    ob.data.materials.clear()
    ob.data.materials.append(m)
    return ob


def studio():
    sc = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    try:
        sc.eevee.taa_render_samples = 64
        sc.eevee.use_shadows = True
        sc.eevee.use_raytracing = True
    except Exception:
        pass
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast"
    sc.view_settings.exposure = -0.45
    world = bpy.data.worlds.new("Studio")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.16, 0.17, 0.19, 1)
    bg.inputs["Strength"].default_value = 0.6
    sc.world = world
    # Éclairage trois points + contre-jour.
    def light(name, kind, energy, loc, rot, size, color=(1, 1, 1)):
        d = bpy.data.lights.new(name, kind)
        d.energy = energy
        d.color = color
        if kind == "AREA":
            d.size = size
        o = bpy.data.objects.new(name, d)
        sc.collection.objects.link(o)
        o.location = loc
        o.rotation_euler = [math.radians(a) for a in rot]
    light("Key", "AREA", 450, (1.6, -2.2, 2.4), (55, 0, 35), 1.6, (1, 0.96, 0.92))
    light("Fill", "AREA", 160, (-2.2, -1.6, 1.5), (70, 0, -55), 2.0, (0.9, 0.95, 1))
    light("Rim", "AREA", 380, (0.4, 2.4, 2.2), (-60, 0, 170), 1.2, (0.85, 0.9, 1))
    light("Top", "AREA", 120, (0, 0, 3.4), (0, 0, 0), 2.0)
    # Sol gris mat.
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.095, 0.1, 1)
    fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    floor.data.materials.append(fm)


def render(path, cam_loc, target, lens, res):
    sc = bpy.context.scene
    cam_d = bpy.data.cameras.new("Cam")
    cam_d.lens = lens
    cam = bpy.data.objects.new("Cam", cam_d)
    sc.collection.objects.link(cam)
    cam.location = cam_loc
    d = Vector(target) - Vector(cam_loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print(f"[perso] rendu {path}", flush=True)


def previews(folder, height):
    os.makedirs(folder, exist_ok=True)
    mid = (0, 0, height * 0.52)
    dist = height * 2.05
    views = {
        "face": (0, -dist, height * 0.55),
        "trois_quarts": (dist * 0.72, -dist * 0.72, height * 0.6),
        "profil": (dist, 0, height * 0.55),
        "dos": (-dist * 0.35, dist * 0.94, height * 0.6),
    }
    for name, loc in views.items():
        render(os.path.join(folder, f"etape{STAGE}_{name}.png"), loc, mid, 50, (900, 1350))
    head = (0, 0, height * 0.93)
    render(os.path.join(folder, f"etape{STAGE}_portrait.png"), (0.32, -0.62, height * 0.95), head, 85, (900, 1100))


def panel_material(name, base_col, panels, seam=(0.01, 0.012, 0.016)):
    """
    Tissu à panneaux : couleur de base, puis pour chaque (attribut, couleur, rugosité) un panneau là où l'attribut dépasse 0,5,
    bord net et légèrement adouci, couture sombre de 2 à 3 mm le long de chaque limite ; grain de tissu fin.
    """
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Sheen Weight"].default_value = 0.3
    p.inputs["Sheen Roughness"].default_value = 0.4
    col = nt.nodes.new("ShaderNodeRGB")
    col.outputs[0].default_value = (*base_col, 1)
    color_out = col.outputs[0]
    rough = nt.nodes.new("ShaderNodeValue")
    rough.outputs[0].default_value = 0.42
    rough_out = rough.outputs[0]
    seam_total = None
    for aname, pcol, prough in panels:
        at = nt.nodes.new("ShaderNodeAttribute")
        at.attribute_name = aname
        edge = nt.nodes.new("ShaderNodeMapRange")
        edge.inputs["From Min"].default_value = 0.47
        edge.inputs["From Max"].default_value = 0.53
        nt.links.new(at.outputs["Fac"], edge.inputs["Value"])
        c = nt.nodes.new("ShaderNodeRGB")
        c.outputs[0].default_value = (*pcol, 1)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(edge.outputs["Result"], mix.inputs["Factor"])
        nt.links.new(color_out, mix.inputs["A"])
        nt.links.new(c.outputs[0], mix.inputs["B"])
        color_out = mix.outputs["Result"]
        r = nt.nodes.new("ShaderNodeMix")
        r.data_type = "FLOAT"
        rv = nt.nodes.new("ShaderNodeValue")
        rv.outputs[0].default_value = prough
        nt.links.new(edge.outputs["Result"], r.inputs["Factor"])
        nt.links.new(rough_out, r.inputs["A"])
        nt.links.new(rv.outputs[0], r.inputs["B"])
        rough_out = r.outputs["Result"]
        # Couture : bande étroite autour de 0,5.
        d = nt.nodes.new("ShaderNodeMath")
        d.operation = "SUBTRACT"
        d.inputs[1].default_value = 0.5
        nt.links.new(at.outputs["Fac"], d.inputs[0])
        a = nt.nodes.new("ShaderNodeMath")
        a.operation = "ABSOLUTE"
        nt.links.new(d.outputs[0], a.inputs[0])
        band = nt.nodes.new("ShaderNodeMapRange")
        band.inputs["From Min"].default_value = 0.045
        band.inputs["From Max"].default_value = 0.075
        band.inputs["To Min"].default_value = 1.0
        band.inputs["To Max"].default_value = 0.0
        nt.links.new(a.outputs[0], band.inputs["Value"])
        if seam_total is None:
            seam_total = band.outputs["Result"]
        else:
            mx = nt.nodes.new("ShaderNodeMath")
            mx.operation = "MAXIMUM"
            nt.links.new(seam_total, mx.inputs[0])
            nt.links.new(band.outputs["Result"], mx.inputs[1])
            seam_total = mx.outputs[0]
    if seam_total is not None:
        sc = nt.nodes.new("ShaderNodeRGB")
        sc.outputs[0].default_value = (*seam, 1)
        smix = nt.nodes.new("ShaderNodeMix")
        smix.data_type = "RGBA"
        nt.links.new(seam_total, smix.inputs["Factor"])
        nt.links.new(color_out, smix.inputs["A"])
        nt.links.new(sc.outputs[0], smix.inputs["B"])
        color_out = smix.outputs["Result"]
    nt.links.new(color_out, p.inputs["Base Color"])
    nt.links.new(rough_out, p.inputs["Roughness"])
    # Grain de tissu (bruit très fin) et léger relief des coutures.
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 1100.0
    nz.inputs["Detail"].default_value = 3.0
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    h = nt.nodes.new("ShaderNodeMath")
    h.operation = "SUBTRACT"
    nt.links.new(nz.outputs["Fac"], h.inputs[0])
    if seam_total is not None:
        sh = nt.nodes.new("ShaderNodeMath")
        sh.operation = "MULTIPLY"
        sh.inputs[1].default_value = 0.6
        nt.links.new(seam_total, sh.inputs[0])
        nt.links.new(sh.outputs[0], h.inputs[1])
    bp = nt.nodes.new("ShaderNodeBump")
    bp.inputs["Strength"].default_value = 0.3
    bp.inputs["Distance"].default_value = 0.0006
    nt.links.new(h.outputs[0], bp.inputs["Height"])
    nt.links.new(bp.outputs["Normal"], p.inputs["Normal"])
    return m


def smooth01(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def wd_suit(base):
    """
    Étape 2 : combinaison de la Wing Diver (EDF 5) : tissu technique blanc, panneaux bleu marine sur les flancs, l'intérieur des
    bras et l'arrière des jambes, articulations noires (coudes, genoux), gants noirs, bottes blanches à semelle noire.
    """
    W = bone_table(base)

    def wsum(i, names):
        return sum(x for b, x in W[i].items() if b in names)
    hands = {b for b in BARE_BONES if b not in ("head", "neck_01")}
    legs_low = {"calf_l", "calf_r", "foot_l", "foot_r", "ball_l", "ball_r"}
    torso = {"pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
    arms = {"upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r"}
    white = mat("SuitWhite", (0.78, 0.8, 0.83), 0.42, sheen=0.25, bump=0.25)
    navy = mat("SuitNavy", (0.018, 0.035, 0.1), 0.38, sheen=0.35, bump=0.3)
    black = mat("SuitBlack", (0.012, 0.012, 0.014), 0.55, sheen=0.2, bump=0.5, bump_scale=400)
    glove = mat("Glove", (0.015, 0.015, 0.017), 0.33, coat=0.2, bump=0.2, bump_scale=600)
    boot = mat("BootWhite", (0.8, 0.81, 0.83), 0.28, coat=0.5)
    sole = mat("BootSole", (0.02, 0.02, 0.02), 0.7, bump=0.4, bump_scale=300)

    def classify_suit(c, n, idx):
        ws = {}
        for i in idx:
            for b, x in W[i].items():
                ws[b] = ws.get(b, 0) + x
        tot = sum(ws.values()) or 1

        def part(names):
            return sum(x for b, x in ws.items() if b in names) / tot
        upper = part({"upperarm_l", "upperarm_r"})
        lower = part({"lowerarm_l", "lowerarm_r"})
        thigh = part({"thigh_l", "thigh_r"})
        calf = part({"calf_l", "calf_r"})
        # Articulations : coudes et genoux (poids partagés entre deux os).
        if min(upper, lower) > 0.22 or (min(thigh, calf) > 0.25 and n.y < 0.3):
            return 2
        if part(arms) > 0.5:
            return 1 if n.z < -0.25 or n.y > 0.55 else 0
        if part({"thigh_l", "thigh_r", "calf_l", "calf_r"}) > 0.5:
            return 1 if n.y > 0.35 or (abs(n.x) > 0.75 and abs(c.x) < 0.07) else 0
        if part(torso) > 0.5:
            # Bassin : échancrure bleu marine (justaucorps) ; flancs et colonne bleu marine.
            if c.z < 0.86 and (abs(c.x) < 0.05 or abs(n.x) > 0.45):
                return 1
            if abs(n.x) > 0.62:
                return 1
            if n.y > 0.5 and abs(c.x) < 0.06:
                return 1
        return 0

    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot

    def navy_field(i, co, n):
        """Panneaux bleu marine : flancs du buste en sablier, colonne au dos, justaucorps échancré, dessous des bras, arrière et
        côté extérieur des jambes."""
        side = 1.0 if co.x * n.x > 0 else 0.0
        f_torso = max(smooth01((abs(n.x) - 0.5) / 0.25),
                      smooth01((n.y - 0.35) / 0.25) * smooth01((0.075 - abs(co.x)) / 0.03),
                      smooth01((0.81 + 0.9 * abs(co.x) - co.z) / 0.02))
        f_arm = smooth01((-n.z - 0.1) / 0.3)
        f_leg = max(smooth01((n.y - 0.45) / 0.25) * 0.0, smooth01((abs(n.x) - 0.82) / 0.12) * side)
        t, a, l = part(i, torso), part(i, arms), part(i, {"thigh_l", "thigh_r", "calf_l", "calf_r"})
        return (t * f_torso + a * f_arm + l * f_leg) / max(t + a + l, 1e-6)

    def black_field(i, co, n):
        """Articulations noires : coudes et genoux (poids partagés entre deux os)."""
        u, lo = part(i, {"upperarm_l", "upperarm_r"}), part(i, {"lowerarm_l", "lowerarm_r"})
        th, ca = part(i, {"thigh_l", "thigh_r"}), part(i, {"calf_l", "calf_r"})
        return max(smooth01((min(u, lo) - 0.12) / 0.2), smooth01((min(th, ca) - 0.12) / 0.2))

    suit_mat = panel_material("SuitWD", (0.8, 0.82, 0.85),
                              [("navy", (0.016, 0.03, 0.09), 0.36), ("black", (0.012, 0.012, 0.014), 0.55)])
    suit = layer(base, "Suit", lambda i, co, n: wsum(i, BARE_BONES) < 0.5 and wsum(i, legs_low) < 0.6,
                 0.004, smooth_iter=8, mats=(suit_mat,), subdiv=2, attrs={"navy": navy_field, "black": black_field})
    layer(base, "Gloves", lambda i, co, n: wsum(i, hands) > 0.3, 0.0022, smooth_iter=2, smooth=0.5, mats=(glove,))
    if STAGE < 3:
        layer(base, "Boots", lambda i, co, n: wsum(i, legs_low) > 0.35, 0.012, smooth_iter=30, smooth=0.9,
              classify=lambda c, n, idx: 1 if c.z < 0.035 else 0, mats=(boot, sole))
    hide_under(base, BARE_BONES, 0.2)
    base.vertex_groups["under_suit"].add([i for i in W if wsum(i, hands) > 0.5], 1.0, "REPLACE")
    return suit


def body_vertices(base):
    """Sommets du corps visible (sans la géométrie d'aide de MPFB : cubes des articulations, aides des cheveux…)."""
    g = base.vertex_groups["body"].index
    return [v for v in base.data.vertices if any(x.group == g and x.weight > 0.5 for x in v.groups)]


def bone_head(base, name):
    rig = base.parent
    return rig.matrix_world @ rig.data.bones[name].head_local


def group_center(base, gname):
    g = base.vertex_groups.get(gname)
    pts = [base.matrix_world @ v.co for v in base.data.vertices if any(x.group == g.index and x.weight > 0.5 for x in v.groups)]
    return sum(pts, Vector()) / max(len(pts), 1)


def finish_obj(ob, base, bevel=0.0, subdiv=2):
    if bevel:
        bv = ob.modifiers.new("Biseau", "BEVEL")
        bv.width = bevel
        bv.segments = 3
        bv.limit_method = "ANGLE"
    if subdiv:
        s = ob.modifiers.new("Subdiv", "SUBSURF")
        s.levels = s.render_levels = subdiv
    for p in ob.data.polygons:
        p.use_smooth = True
    if base.parent:
        ob.parent = base.parent
        ob.matrix_parent_inverse = base.parent.matrix_world.inverted()
    return ob


def prim(kind, name, mats, loc, rot=(0, 0, 0), scale=(1, 1, 1), **kw):
    getattr(bpy.ops.mesh, f"primitive_{kind}_add")(location=loc, rotation=rot, **kw)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for m in mats:
        ob.data.materials.append(m)
    return ob


def glow_mat(name, color, strength):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Emission Color"].default_value = (*color, 1)
    p.inputs["Emission Strength"].default_value = strength
    return m


def boots(base, W):
    """Bottes : tige moulée sur le mollet, pied en coque (enveloppe convexe : pas d'orteils), semelle noire crantée."""
    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot
    boot = mat("BootWhite", (0.8, 0.81, 0.83), 0.28, coat=0.5)
    sole = mat("BootSole", (0.02, 0.02, 0.02), 0.7, bump=0.4, bump_scale=300)
    layer(base, "BootShaft", lambda i, co, n: part(i, {"calf_l", "calf_r"}) > 0.3 and part(i, {"foot_l", "foot_r", "ball_l", "ball_r"}) < 0.8,
          0.012, smooth_iter=12, smooth=0.8, mats=(boot,))
    for side in ("l", "r"):
        feet = {f"foot_{side}", f"ball_{side}"}
        pts = [base.matrix_world @ v.co for v in body_vertices(base) if part(v.index, feet) > 0.15]
        c = sum(pts, Vector()) / len(pts)
        bm = bmesh.new()
        for p in pts:
            q = c + (p - c) * 1.035
            q.x = c.x + (q.x - c.x) * 0.97
            q.z = max(q.z, 0.0)
            bm.verts.new(q)
        bmesh.ops.convex_hull(bm, input=bm.verts)
        me = bpy.data.meshes.new(f"Shoe_{side}")
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(f"Shoe_{side}", me)
        bpy.context.collection.objects.link(ob)
        me.materials.append(boot)
        me.materials.append(sole)
        for f in me.polygons:
            f.material_index = 1 if f.center.z < 0.03 else 0
        finish_obj(ob, base, bevel=0.006, subdiv=2)


def armor(base):
    """
    Étape 3 : armure blanche laquée à liserés bleu marine (plastron galbé, épaulières, protège-avant-bras, plaques de hanches et
    de cuisses, genouillères, col), bottes en coque et unité de vol dorsale ; tête nue (ni casque, ni visière, ni micro).
    """
    W = bone_table(base)

    def part(i, names):
        tot = sum(W[i].values()) or 1.0
        return sum(x for b, x in W[i].items() if b in names) / tot
    plate = mat("ArmorWhite", (0.84, 0.85, 0.87), 0.22, coat=0.7, bump=0.05, bump_scale=200)
    trim = mat("ArmorNavy", (0.02, 0.04, 0.11), 0.3, coat=0.5)
    dark = mat("ArmorDark", (0.03, 0.032, 0.036), 0.38, metal=0.6)
    collar = mat("Collar", (0.012, 0.012, 0.014), 0.5, sheen=0.2)
    glow = glow_mat("JetGlow", (0.35, 0.75, 1.0), 25.0)

    sp3, neck, hd = bone_head(base, "spine_03"), bone_head(base, "neck_01"), bone_head(base, "head")
    ua = {s: bone_head(base, f"upperarm_{s}") for s in "lr"}
    la = {s: bone_head(base, f"lowerarm_{s}") for s in "lr"}
    hn = {s: bone_head(base, f"hand_{s}") for s in "lr"}
    th = {s: bone_head(base, f"thigh_{s}") for s in "lr"}
    ca = {s: bone_head(base, f"calf_{s}") for s in "lr"}

    def seg_t(co, a, b):
        ab = b - a
        return (co - a).dot(ab) / ab.length_squared

    def side(co):
        return "l" if co.x > 0 else "r"

    # Plastron : buste et haut de poitrine, encolure en V.
    def chest(i, co, n):
        return (part(i, {"spine_03", "spine_02", "clavicle_l", "clavicle_r"}) > 0.55 and n.y < -0.15 and abs(co.x) < 0.16
                and sp3.z + 0.04 < co.z < neck.z - 0.035 - 0.5 * max(0.0, 0.07 - abs(co.x)))
    layer(base, "ChestPlate", chest, 0.012, smooth_iter=10, smooth=0.7, mats=(plate, trim), subdiv=2, solidify=0.006)
    # Plaque dorsale (support du réacteur).
    def back(i, co, n):
        return part(i, {"spine_03", "spine_02"}) > 0.5 and n.y > 0.3 and abs(co.x) < 0.13 and sp3.z - 0.02 < co.z < neck.z - 0.05
    layer(base, "BackPlate", back, 0.012, smooth_iter=8, mats=(dark,), subdiv=2, solidify=0.008)
    # Épaulières.
    def shoulder(i, co, n):
        s = side(co)
        return (co - ua[s]).length < 0.115 and n.z > -0.15 and abs(co.x) > abs(ua[s].x) - 0.04 and part(i, {f"upperarm_{s}", f"clavicle_{s}"}) > 0.4
    layer(base, "Shoulders", shoulder, 0.014, smooth_iter=10, smooth=0.8, mats=(plate, trim), subdiv=2, solidify=0.006)
    # Protège-avant-bras (côté extérieur).
    def forearm(i, co, n):
        s = side(co)
        t = seg_t(co, la[s], hn[s])
        out = Vector((1 if s == "l" else -1, 0, 0.6)).normalized()
        return part(i, {f"lowerarm_{s}"}) > 0.7 and 0.18 < t < 0.88 and n.dot(out) > 0.05
    layer(base, "Forearms", forearm, 0.011, smooth_iter=8, mats=(plate, trim), subdiv=2, solidify=0.006)
    # Plaques de hanches (côté extérieur du bassin).
    def hips(i, co, n):
        s = side(co)
        return (part(i, {"pelvis", f"thigh_{s}"}) > 0.6 and co.x * n.x > 0 and abs(n.x) > 0.5
                and th[s].z - 0.1 < co.z < th[s].z + 0.09)
    layer(base, "HipPlates", hips, 0.012, smooth_iter=8, mats=(trim, plate), subdiv=2, solidify=0.006)
    # Plaques de cuisses (devant).
    def thighs(i, co, n):
        s = side(co)
        t = seg_t(co, th[s], ca[s])
        return part(i, {f"thigh_{s}"}) > 0.75 and n.y < -0.3 and 0.3 < t < 0.78
    layer(base, "ThighPlates", thighs, 0.011, smooth_iter=8, mats=(plate, trim), subdiv=2, solidify=0.006)
    # Genouillères.
    def knees(i, co, n):
        s = side(co)
        return (co - ca[s]).length < 0.075 and n.y < -0.25
    layer(base, "KneePads", knees, 0.016, smooth_iter=8, mats=(trim, plate), subdiv=2, solidify=0.008)
    # Col montant.
    def neckring(i, co, n):
        return 0.2 < part(i, {"neck_01"}) < 0.85 and co.z < hd.z - 0.04
    layer(base, "Collar", neckring, 0.006, smooth_iter=6, mats=(collar,), subdiv=2, solidify=0.012)
    boots(base, W)
    # Cuir chevelu : sous-couche châtain sous la calotte de cheveux (aucune peau visible entre les mèches ni à la nuque).
    hair = next((o for o in bpy.data.objects if o.type == "MESH" and "ponytail" in o.name.lower()), None)
    if hair:
        from mathutils.kdtree import KDTree
        hw = hair.matrix_world
        tree = KDTree(len(hair.data.vertices))
        for v in hair.data.vertices:
            tree.insert(hw @ v.co, v.index)
        tree.balance()
        bw = base.matrix_world
        layer(base, "Scalp", lambda i, co, n: part(i, {"head", "neck_01"}) > 0.6 and tree.find(bw @ co)[2] < 0.011,
              0.0015, smooth_iter=2, smooth=0.5, mats=(mat("Scalp", (0.06, 0.04, 0.028), 0.7),))

    # Pas de casque : tête nue (cheveux et visage visibles).

    # Unité de vol (EDF 5) : corps dorsal galbé à cœur plasma lumineux, deux ailes-propulseurs effilées en flèche (vers le haut,
    # l'extérieur et l'arrière) à arête lumineuse, buses sous les ailes, stabilisateurs bas.
    back_pts = [base.matrix_world @ v.co for v in body_vertices(base)
                if part(v.index, {"spine_03", "spine_02"}) > 0.5 and abs((base.matrix_world @ v.co).x) < 0.05]
    back_y = max(p.y for p in back_pts)
    jz = sp3.z + 0.1
    jetpack(base, back_y, jz, plate, trim, dark, glow)


def loft(name, mats, sections):
    """
    Volume tendu entre des sections rectangulaires : [(centre, axe de largeur, axe d'épaisseur, largeur, épaisseur), …].
    Après biseau et subdivision : formes effilées et lisses (ailes, carénages).
    """
    bm = bmesh.new()
    rings = []
    for c, wa, ta, w, t in sections:
        wa, ta = wa.normalized(), ta.normalized()
        rings.append([bm.verts.new(c + wa * (sw * w / 2) + ta * (st * t / 2)) for sw, st in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for a, b in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    for m in mats:
        me.materials.append(m)
    return ob


def jetpack(base, back_y, jz, plate, trim, dark, glow):
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    y0 = back_y + 0.02
    # Corps dorsal : carénage plus large en haut, plus fin en bas, bombé vers l'arrière.
    core = loft("JetCore", (plate,), [
        (Vector((0, y0 + 0.035, jz - 0.2)), X, Y, 0.14, 0.05),
        (Vector((0, y0 + 0.05, jz - 0.05)), X, Y, 0.2, 0.08),
        (Vector((0, y0 + 0.05, jz + 0.08)), X, Y, 0.24, 0.085),
        (Vector((0, y0 + 0.035, jz + 0.15)), X, Y, 0.2, 0.05),
    ])
    finish_obj(core, base, bevel=0.018)
    # Cœur plasma : hublot rond lumineux serti d'une bague sombre, au centre du dos.
    cz = jz + 0.0
    prim("torus", "PlasmaRing", (dark,), (0, y0 + 0.095, cz), rot=(math.radians(90), 0, 0),
         major_radius=0.045, minor_radius=0.011, major_segments=48, minor_segments=12)
    finish_obj(bpy.context.active_object, base, subdiv=1)
    prim("uv_sphere", "PlasmaCore", (glow,), (0, y0 + 0.088, cz), scale=(1, 0.45, 1), radius=0.038, segments=32, ring_count=16)
    finish_obj(bpy.context.active_object, base, subdiv=1)
    # Arête centrale sombre (dos du carénage).
    spine = loft("JetSpine", (dark,), [
        (Vector((0, y0 + 0.088, jz + 0.06)), X, Y, 0.03, 0.02),
        (Vector((0, y0 + 0.09, jz + 0.14)), X, Y, 0.02, 0.016),
    ])
    finish_obj(spine, base, bevel=0.004)
    for s, sx in (("l", 1), ("r", -1)):
        out = Vector((sx, 0, 0))
        root = Vector((sx * 0.09, y0 + 0.065, jz + 0.04))
        mid = root + Vector((sx * 0.1, 0.05, 0.06))
        mid2 = root + Vector((sx * 0.18, 0.1, 0.14))
        tip = root + Vector((sx * 0.25, 0.14, 0.23))
        span = (tip - root).normalized()
        thick = (Y - span * Y.dot(span)).normalized()
        width = span.cross(thick).normalized()
        if width.z < 0:
            width = -width
        # Aile galbée : racine épaisse, profil qui s'affine et se cambre vers l'arrière.
        wing = loft(f"Wing_{s}", (plate, trim), [
            (root, width, thick, 0.13, 0.055),
            (mid, width, thick, 0.11, 0.045),
            (mid2 + thick * 0.012, width, thick, 0.075, 0.03),
            (tip + thick * 0.02, width, thick, 0.03, 0.012),
        ])
        so = wing.modifiers.new("Liseré", "SOLIDIFY")
        so.thickness = 0.002
        so.material_offset_rim = 1
        finish_obj(wing, base, bevel=0.01)
        # Arête lumineuse sous l'aile (bord d'attaque bas).
        edge = loft(f"WingGlow_{s}", (glow,), [
            (root - width * 0.058 + thick * 0.0, span, thick, 0.004, 0.006),
            (tip - width * 0.016, span, thick, 0.002, 0.004),
        ])
        finish_obj(edge, base, subdiv=0)
        # Bande bleu marine sur le dessus de l'aile.
        band = loft(f"WingBand_{s}", (trim,), [
            (root.lerp(tip, 0.25) + width * 0.03 + thick * 0.02, width, thick, 0.03, 0.004),
            (root.lerp(tip, 0.7) + width * 0.012 + thick * 0.012, width, thick, 0.016, 0.003),
        ])
        finish_obj(band, base, bevel=0.002, subdiv=1)
        # Buse de poussée sous la racine de l'aile, orientée vers le bas et un peu vers l'arrière.
        # Buse : carénage arrondi (capsule) prolongé d'une tuyère évasée, fond lumineux.
        nz = Vector((sx * 0.115, y0 + 0.07, jz - 0.13))
        prim("uv_sphere", f"NozzleBody_{s}", (plate,), (nz.x, nz.y, nz.z), scale=(0.8, 0.8, 1.5), radius=0.036, segments=32, ring_count=16)
        finish_obj(bpy.context.active_object, base, subdiv=1)
        prim("cone", f"Nozzle_{s}", (dark,), (nz.x, nz.y + 0.004, nz.z - 0.06), radius1=0.03, radius2=0.022, depth=0.035, vertices=32)
        finish_obj(bpy.context.active_object, base, bevel=0.003, subdiv=1)
        prim("cylinder", f"NozzleGlow_{s}", (glow,), (nz.x, nz.y + 0.004, nz.z - 0.078), radius=0.024, depth=0.003, vertices=32)
        finish_obj(bpy.context.active_object, base, subdiv=0)
        # Stabilisateur bas (petite aile en flèche vers le bas et l'extérieur).
        sr = Vector((sx * 0.06, y0 + 0.06, jz - 0.17))
        st = sr + Vector((sx * 0.1, 0.06, -0.12))
        sspan = (st - sr).normalized()
        sthick = (Y - sspan * Y.dot(sspan)).normalized()
        swidth = sspan.cross(sthick).normalized()
        fin = loft(f"Fin_{s}", (trim,), [(sr, swidth, sthick, 0.05, 0.016), (st, swidth, sthick, 0.018, 0.006)])
        finish_obj(fin, base, bevel=0.005)
        # Bras de liaison corps → aile.
        arm = loft(f"Strut_{s}", (dark,), [(Vector((sx * 0.05, y0 + 0.07, jz + 0.03)), X, Y, 0.03, 0.03),
                                           (root + out * 0.01, X, Y, 0.035, 0.035)])
        finish_obj(arm, base, bevel=0.006)


def finishing(base):
    """
    Étape 4 : marquages au pochoir (matricule sur le plastron et les ailes, pas d'inscription « EDF »),
    grilles d'aération et bandes lumineuses du réacteur.
    """
    navy = bpy.data.materials["ArmorNavy"]
    dark = bpy.data.materials["ArmorDark"]
    glow = bpy.data.materials["JetGlow"]
    soft = glow_mat("JetStrip", (0.35, 0.75, 1.0), 6.0)
    ua = bone_head(base, "upperarm_l")
    sp3 = bone_head(base, "spine_03")
    obs = bpy.data.objects
    shoulders, chest, core = obs.get("Shoulders"), obs.get("ChestPlate"), obs.get("JetCore")
    stencil("WD-07", "Mark_Chest", navy, Vector((0.075, -0.16, sp3.z + 0.2)), (90, 0, 0), 0.012, chest)
    if core:
        # Marquage et grilles d'aération en bas du carénage.
        bb = [core.matrix_world @ Vector(c) for c in core.bound_box]
        back = max(p.y for p in bb)
        zmin = min(p.z for p in bb)
        for k in range(4):
            prim("cube", f"JetVent_{k}", (dark,), (0, back - 0.012, zmin + 0.06 + k * 0.014), scale=(0.045, 0.004, 0.004))
            finish_obj(bpy.context.active_object, base, bevel=0.002, subdiv=0)
    for s, sx in (("l", 1), ("r", -1)):
        wing = obs.get(f"Wing_{s}")
        if wing:
            bb = [wing.matrix_world @ Vector(c) for c in wing.bound_box]
            c = sum(bb, Vector()) / 8
            stencil("07", f"Mark_Wing_{s}", navy, (c.x, max(p.y for p in bb), c.z), (90, 0, 180), 0.03, wing)


clear_scene()
base = make_human()
height = base.dimensions.z
if STAGE == 1:
    neutral_suit(base)
elif STAGE >= 2:
    wd_suit(base)
if STAGE >= 3:
    armor(base)
if STAGE >= 4:
    finishing(base)
if PREVIEW:
    studio()
    previews(PREVIEW, height)
if SAVE:
    bpy.ops.wm.save_as_mainfile(filepath=SAVE)
EXPORT = arg("--export")
if EXPORT:
    # Modèle du jeu (voir export_game.py) : remplace les matériaux, à faire en dernier.
    exec(open(os.path.join(HERE, "export_game.py"), encoding="utf-8").read(), globals())
    remember_images()
    export_game(base, EXPORT)
print("[perso] fin", flush=True)
