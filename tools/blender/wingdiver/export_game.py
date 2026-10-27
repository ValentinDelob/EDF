# Export de la Wing Diver pour le jeu (exécuté par build_char.py avec --export DOSSIER, dans le même espace de noms).
# Maillages déformés par le squelette « game_engine » de MPFB (poids recopiés du corps), chaîne d'os de la queue de cheval
# (hair_01 → hair_04, animée par ressort dans le jeu), expressions du visage en clés de forme (blink, smile, effort, pain,
# shout, focus) sur le corps, les sourcils et les cils, textures (peau, yeux, cheveux, combinaison cuite), unité de vol rigide
# sur spine_03, repères des buses (JetNozzle_l / _r). Un seul .glb : assets/models/characters/wingdiver.glb.
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from bl_ext.user_default.mpfb.services.targetservice import TargetService

EXPRESSIONS = {
    "blink": {"eye-left-closure": 1.0, "eye-right-closure": 1.0},
    "smile": {"mouth-corner-puller": 0.75, "mouth-elevation": 0.15, "eye-left-slit": 0.3, "eye-right-slit": 0.3},
    "effort": {"eyebrows-left-down": 0.6, "eyebrows-right-down": 0.6, "mouth-compression": 0.5, "eye-left-slit": 0.4,
               "eye-right-slit": 0.4, "nose-left-elevation": 0.3, "nose-right-elevation": 0.3},
    "pain": {"eyebrows-left-inner-up": 0.85, "eyebrows-right-inner-up": 0.85, "eye-left-closure": 0.45, "eye-right-closure": 0.45,
             "mouth-open": 0.25, "mouth-depression-retraction": 0.5, "mouth-retraction": 0.35},
    "shout": {"mouth-open": 0.55, "eyebrows-left-down": 0.4, "eyebrows-right-down": 0.4, "neck-platysma": 0.4,
              "mouth-retraction": 0.3, "eye-left-slit": 0.2, "eye-right-slit": 0.2},
    "focus": {"eyebrows-left-down": 0.35, "eyebrows-right-down": 0.35, "eye-left-slit": 0.35, "eye-right-slit": 0.35,
              "mouth-compression": 0.2},
}

# Pièces rigides de l'unité de vol (réunies, attachées à spine_03).
JET_PREFIX = ("JetCore", "PlasmaRing", "PlasmaCore", "JetSpine", "Wing_", "WingGlow_", "WingBand_", "NozzleBody_", "Nozzle_",
              "NozzleGlow_", "Fin_", "Strut_", "JetVent_", "Mark_Wing_")


def _units_dir():
    root = os.environ.get("BLENDER_USER_RESOURCES", "")
    return os.path.join(root, "extensions", "user_default", "mpfb", "data", "targets", "expression", "units", "caucasian")


def add_expressions(base):
    """Clés de forme des expressions (mélanges des unités d'expression de MakeHuman, CC0), puis unités retirées."""
    if not base.data.shape_keys:
        base.shape_key_add(name="Basis")
    units = set()
    for mix in EXPRESSIONS.values():
        units |= set(mix)
    for u in sorted(units):
        path = os.path.join(_units_dir(), f"{u}.target.gz")
        TargetService.load_target(base, path, weight=0.0, name=u)
    keys = base.data.shape_keys.key_blocks
    for name, mix in EXPRESSIONS.items():
        for k in keys:
            k.value = 0.0
        for u, w in mix.items():
            keys[u].value = w
        base.shape_key_add(name=name, from_mix=True)
    for k in keys:
        k.value = 0.0
    for u in units:
        base.shape_key_remove(base.data.shape_keys.key_blocks[u])
    print(f"[perso] expressions : {', '.join(EXPRESSIONS)}", flush=True)


def follow_expressions(base, ob):
    """Sourcils et cils suivent les expressions du visage : déformation de surface liée au corps, cuite en clés de forme."""
    for m in ob.modifiers:
        m.show_viewport = False
    sd = ob.modifiers.new("Suivi", "SURFACE_DEFORM")
    sd.target = base
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.surfacedeform_bind(modifier=sd.name)
    keys = base.data.shape_keys.key_blocks
    if not ob.data.shape_keys:
        ob.shape_key_add(name="Basis")
    for name in EXPRESSIONS:
        for k in keys:
            k.value = 0.0
        keys[name].value = 1.0
        dg = bpy.context.evaluated_depsgraph_get()
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        if len(me.vertices) == len(ob.data.vertices):
            sk = ob.shape_key_add(name=name, from_mix=False)
            for d, v in zip(sk.data, me.vertices):
                d.co = v.co
        ev.to_mesh_clear()
    for k in keys:
        k.value = 0.0
    ob.modifiers.remove(sd)
    for m in ob.modifiers:
        m.show_viewport = True


def apply_modifiers(ob, keep=("ARMATURE",)):
    """Modificateurs appliqués (sauf le squelette) : maillage final de la pièce."""
    for m in list(ob.modifiers):
        if m.type in keep:
            continue
        with bpy.context.temp_override(object=ob, active_object=ob):
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except RuntimeError as e:
                print(f"[perso] {ob.name} : modificateur {m.name} non appliqué ({e})", flush=True)
                ob.modifiers.remove(m)


def copy_weights(base, ob, rig, rigid_bone=None):
    """Poids du squelette recopiés du sommet du corps le plus proche (ou un seul os pour une pièce rigide) ; squelette ajouté."""
    bones = [b.name for b in rig.data.bones]
    for g in list(ob.vertex_groups):
        ob.vertex_groups.remove(g)
    groups = {b: ob.vertex_groups.new(name=b) for b in bones}
    mw = ob.matrix_world
    if rigid_bone:
        groups[rigid_bone].add([v.index for v in ob.data.vertices], 1.0, "REPLACE")
    else:
        bw = base.matrix_world
        names = {g.index: g.name for g in base.vertex_groups if g.name in groups}
        tree = KDTree(len(base.data.vertices))
        for v in base.data.vertices:
            tree.insert(bw @ v.co, v.index)
        tree.balance()
        for v in ob.data.vertices:
            _, idx, _ = tree.find(mw @ v.co)
            for g in base.data.vertices[idx].groups:
                if g.group in names and g.weight > 0.001:
                    groups[names[g.group]].add([v.index], g.weight, "REPLACE")
    for m in [m for m in ob.modifiers if m.type == "ARMATURE"]:
        ob.modifiers.remove(m)
    am = ob.modifiers.new("Squelette", "ARMATURE")
    am.object = rig
    wm = ob.matrix_world.copy()
    ob.parent = rig
    ob.matrix_world = wm


def delete_hidden(base):
    """Corps : sommets cachés retirés (géométrie d'aide de MPFB, peau sous les vêtements) ; clés de forme conservées."""
    body = base.vertex_groups["body"].index
    under = base.vertex_groups.get("under_suit")
    ui = under.index if under else -1
    keep = set()
    for v in base.data.vertices:
        gw = {g.group: g.weight for g in v.groups}
        if gw.get(body, 0) > 0.5 and gw.get(ui, 0) < 0.5:
            keep.add(v.index)
    bm = bmesh.new()
    bm.from_mesh(base.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    bm.to_mesh(base.data)
    bm.free()
    for m in [m for m in base.modifiers if m.type in ("MASK", "SUBSURF")]:
        base.modifiers.remove(m)


def ponytail_bones(rig, hair):
    """Chaîne d'os de la queue de cheval (hair_01 → hair_04, sous « head ») et poids des mèches le long de la chaîne."""
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    hw = hair.matrix_world
    pts = [(v.index, hw @ v.co) for v in hair.data.vertices]
    tail = [(i, p) for i, p in pts if p.y > head.y + 0.05 and p.z < head.z + 0.1]
    if len(tail) < 20:
        print("[perso] queue de cheval introuvable", flush=True)
        return
    ztop = max(p.z for _, p in tail)
    zbot = min(p.z for _, p in tail)
    n = 4
    centers = []
    for k in range(n + 1):
        z = ztop - (ztop - zbot) * k / n
        band = [p for _, p in tail if abs(p.z - z) < (ztop - zbot) / (2 * n) + 0.01]
        c = sum(band, Vector()) / len(band) if band else Vector((0, head.y + 0.1, z))
        centers.append(c)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    inv = rig.matrix_world.inverted()
    parent = eb["head"]
    for k in range(n):
        b = eb.new(f"hair_{k + 1:02d}")
        b.head = inv @ centers[k]
        b.tail = inv @ centers[k + 1]
        b.parent = parent
        b.use_connect = k > 0
        parent = b
    bpy.ops.object.mode_set(mode="OBJECT")
    groups = {f"hair_{k + 1:02d}": (hair.vertex_groups.get(f"hair_{k + 1:02d}") or hair.vertex_groups.new(name=f"hair_{k + 1:02d}"))
              for k in range(n)}
    hg = hair.vertex_groups.get("head") or hair.vertex_groups.new(name="head")
    for i, p in tail:
        t = max(0.0, min(0.999, (ztop - p.z) / max(ztop - zbot, 1e-4))) * n
        k = int(t)
        f = t - k
        for g in list(hair.data.vertices[i].groups):
            hair.vertex_groups[g.group].remove([i])
        groups[f"hair_{k + 1:02d}"].add([i], 1.0 - f * 0.5, "REPLACE")
        if k + 1 < n:
            groups[f"hair_{k + 2:02d}"].add([i], f * 0.5, "REPLACE")
        elif k == 0:
            hg.add([i], 0.3, "REPLACE")
    print(f"[perso] queue de cheval : {len(tail)} sommets, de {ztop:.2f} à {zbot:.2f} m", flush=True)


def image_material(name, image, alpha=False, rough=0.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = rough
    if image:
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = image
        nt.links.new(t.outputs["Color"], p.inputs["Base Color"])
        if alpha:
            nt.links.new(t.outputs["Alpha"], p.inputs["Alpha"])
    return m


def _tree_images(tree, seen=None):
    """Images d'un arbre de nœuds, groupes imbriqués compris (le matériau de peau de MPFB les range dans des groupes)."""
    seen = seen or set()
    for n in tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            yield n.image
        elif n.type == "GROUP" and n.node_tree and n.node_tree.name not in seen:
            seen.add(n.node_tree.name)
            yield from _tree_images(n.node_tree, seen)


def first_image(ob):
    """Texture de couleur du premier matériau (pas les cartes de normales, de relief, de brillance)."""
    for m in ob.data.materials:
        if m and m.node_tree:
            imgs = [i for i in _tree_images(m.node_tree) if not any(k in i.name.lower() for k in ("normal", "bump", "spec", "rough", "sss"))]
            diffuse = [i for i in imgs if "diffuse" in i.name.lower()]
            if diffuse or imgs:
                return (diffuse or imgs)[0]
    return None


def save_image(img, out_dir, fname):
    # Fichier de travail hors des ressources du jeu (le .glb embarque l'image ; Godot l'extrait à l'import).
    img.filepath_raw = os.path.join(bpy.app.tempdir, fname)
    img.file_format = "PNG"
    img.save()
    return img


def hair_texture(src, out_dir):
    """Texture des cheveux teintée châtain (même réglage que le rendu : désaturée, multipliée)."""
    w, h = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    lum = px[..., :3].mean(axis=2, keepdims=True)
    # Valeurs de l'image en sRVB : châtain moyen (le rendu de Blender multipliait en linéaire, bien plus sombre ici).
    rgb = (lum + (px[..., :3] - lum) * 0.4) * np.array([0.95, 0.68, 0.48], dtype=np.float32)
    out = np.concatenate([np.clip(rgb, 0, 1), px[..., 3:4]], axis=2)
    img = bpy.data.images.new("wd_hair", w, h, alpha=True)
    img.pixels[:] = out.ravel()
    return save_image(img, out_dir, "wd_hair.png")


def bake_suit(suit, out_dir, size=2048):
    """Couleur de la combinaison (panneaux, coutures) cuite dans sa texture (même dépliage que la peau)."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 4
    sc.cycles.device = "CPU"
    img = bpy.data.images.new("wd_suit", size, size, alpha=False)
    mat = suit.data.materials[0]
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    suit.select_set(True)
    bpy.context.view_layer.objects.active = suit
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=8)
    save_image(img, out_dir, "wd_suit.png")
    return img


def export_game(base, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    rig = base.parent
    obs = bpy.data.objects
    # 1. Expressions (sur le corps complet, avant toute suppression de sommets), suivies par sourcils et cils.
    add_expressions(base)
    brows = [o for o in obs if o.type == "MESH" and ("eyebrow" in o.name.lower() or "eyelash" in o.name.lower())]
    for o in brows:
        follow_expressions(base, o)
    # 2. Combinaison : niveau de subdivision du rendu pour la cuisson, puis maillage de jeu (subdivision 1, UV linéaires).
    suit = obs.get("Suit")
    if suit:
        for m in suit.modifiers:
            if m.type == "SUBSURF":
                m.uv_smooth = "NONE"
        suit_tex = bake_suit(suit, out_dir)
        for m in suit.modifiers:
            if m.type == "SUBSURF":
                m.levels = m.render_levels = 1
        suit.data.materials.clear()
        suit.data.materials.append(image_material("WD_Suit", suit_tex, rough=0.45))
    # 3. Pièces de l'armure et unité de vol : modificateurs appliqués (subdivision réduite pour le jeu).
    layers = [o for o in obs if o.type == "MESH" and o.name in (
        "Suit", "Gloves", "BootShaft", "Shoe_l", "Shoe_r", "ChestPlate", "BackPlate", "Shoulders", "Forearms", "HipPlates",
        "ThighPlates", "KneePads", "Collar") or o.name.startswith("Mark_Chest")]
    jet = [o for o in obs if o.type == "MESH" and o.name.startswith(JET_PREFIX)]
    for o in layers + jet:
        for m in [m for m in o.modifiers if m.type == "SUBSURF"]:
            o.modifiers.remove(m)
        if o.name == "Gloves":
            d = o.modifiers.new("Allègement", "DECIMATE")
            d.ratio = 0.45
        apply_modifiers(o, keep=())
    # Repères des buses de l'unité de vol (flammes en jeu).
    for s in ("l", "r"):
        g = obs.get(f"NozzleGlow_{s}")
        if g:
            e = obs.new(f"JetNozzle_{s}", None)
            bpy.context.collection.objects.link(e)
            e.location = g.matrix_world.translation
    # Unité de vol : une seule pièce, rigide sur spine_03.
    if jet:
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in jet:
            o.parent = None
            o.select_set(True)
        bpy.context.view_layer.objects.active = jet[0]
        bpy.ops.object.join()
        jp = bpy.context.active_object
        jp.name = "Jetpack"
        d = jp.modifiers.new("Allègement", "DECIMATE")
        d.ratio = 0.5
        apply_modifiers(jp, keep=())
        copy_weights(base, jp, rig, rigid_bone="spine_03")
    # 4. Poids du squelette pour chaque couche (recopiés du corps complet, avant la suppression des sommets cachés).
    for o in layers:
        copy_weights(base, o, rig, rigid_bone="spine_03" if o.name == "BackPlate" else None)
    # 5. Queue de cheval : chaîne d'os animée en jeu.
    hair = next((o for o in obs if o.type == "MESH" and "ponytail" in o.name.lower()), None)
    if hair:
        apply_modifiers(hair)
        ponytail_bones(rig, hair)
        hair.data.materials.clear()
        hair.data.materials.append(image_material("WD_Hair", hair_texture(first_image_cache["hair"], out_dir), alpha=True, rough=0.45))
    # 6. Corps : peau cachée retirée, peau texturée.
    skin = first_image_cache.get("skin")
    delete_hidden(base)
    base.data.materials.clear()
    base.data.materials.append(image_material("WD_Skin", skin, rough=0.5))
    for o in obs:
        if o.type != "MESH" or o in (base, hair) or o.name in [l.name for l in layers] or o.name == "Jetpack":
            continue
        low = o.name.lower()
        if "eyebrow" in low or "eyelash" in low:
            img = first_image_cache.get(low)
            o.data.materials.clear()
            o.data.materials.append(image_material("WD_Brows" if "eyebrow" in low else "WD_Lashes", img, alpha=True))
        elif "high-poly" in low:
            o.data.materials.clear()
            o.data.materials.append(image_material("WD_Eyes", first_image_cache.get("eyes"), rough=0.1))
        elif "teeth" in low or "tongue" in low:
            if "teeth" in low:
                d = o.modifiers.new("Allègement", "DECIMATE")
                d.ratio = 0.15
                apply_modifiers(o)
            img = first_image_cache.get(low)
            o.data.materials.clear()
            o.data.materials.append(image_material("WD_Teeth" if "teeth" in low else "WD_Tongue", img, rough=0.3))
    for o in obs:
        if o.type == "MESH":
            for m in [m for m in o.modifiers if m.type == "SUBSURF"]:
                o.modifiers.remove(m)
    # 7. Export (squelette, maillages déformés, clés de forme, textures intégrées).
    for o in [o for o in obs if o.type in ("LIGHT", "CAMERA") or o.name.startswith(("Plane", "Icosphere"))]:
        bpy.data.objects.remove(o)
    path = os.path.join(out_dir, "wingdiver.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=False, export_skins=True, export_morph=True,
                              export_morph_normal=True, export_animations=False, export_apply=False, export_yup=True,
                              export_image_format="AUTO")
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in obs if o.type == "MESH")
    print(f"[perso] export : {path} ({tris} triangles)", flush=True)


first_image_cache = {}


def remember_images():
    """Textures d'origine (avant que l'export ne remplace les matériaux)."""
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        low = o.name.lower()
        img = first_image(o)
        if low == base.name.lower():
            first_image_cache["skin"] = img
        elif "ponytail" in low:
            first_image_cache["hair"] = img
        elif "high-poly" in low:
            first_image_cache["eyes"] = img
        else:
            first_image_cache[low] = img
