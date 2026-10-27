# Rendus de contrôle des armes (Blender, après make()) : studio neutre, cadrage automatique sur la boîte englobante de l'arme,
# une vue de profil et une vue de trois quarts avant. Usage (serveur MCP) : exec(kit.py), exec(<famille>.py), exec(preview.py),
# puis preview(prefixe, res=(800, 450)) → [chemins].
import bpy, math
from mathutils import Vector


def _studio():
    sc = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    try:
        sc.eevee.use_shadows = True
        sc.eevee.use_gtao = True
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    vt = [v.identifier for v in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items]
    sc.view_settings.view_transform = 'AgX' if 'AgX' in vt else 'Filmic'
    if not sc.world:
        sc.world = bpy.data.worlds.new("W")
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.5, 0.53, 0.56, 1)
    bg.inputs[1].default_value = 0.9
    for name, energy, rot in (("PrevKey", 3.0, (50, 0, -40)), ("PrevFill", 0.9, (65, 0, 140)), ("PrevRim", 1.6, (20, 0, 200))):
        o = bpy.data.objects.get(name)
        if o is None:
            o = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
            sc.collection.objects.link(o)
        o.data.energy = energy
        o.data.angle = math.radians(6)
        o.rotation_euler = tuple(math.radians(a) for a in rot)
    cam = bpy.data.objects.get("PrevCam")
    if cam is None:
        cam = bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam"))
        sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def _bounds():
    pts = []
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.name.startswith("Prev") and o.visible_get():
            pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def preview(prefix, res=(800, 450), views=("side", "three")):
    sc, cam = _studio()
    sc.render.resolution_x, sc.render.resolution_y = res
    lo, hi = _bounds()
    c = (lo + hi) / 2
    size = max((hi - lo).length, 0.05)
    out = []
    for v in views:
        # Avant de l'arme vers −Y (repère du kit) : profil orthographique cadré sur l'arme, trois quarts en perspective.
        d = {"side": Vector((1.0, 0.0, 0.0)), "three": Vector((0.8, -0.75, 0.38)), "top": Vector((0.05, 0.0, 1.0))}[v].normalized()
        if v == "side":
            cam.data.type = 'ORTHO'
            ext = hi - lo
            cam.data.ortho_scale = max(ext.y, ext.z * res[0] / res[1]) * 1.12
            cam.location = c + d * (size * 2 + 1)
        else:
            cam.data.type = 'PERSP'
            cam.data.lens = 50
            cam.location = c + d * size * 1.75
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f"{prefix}_{v}.png"
        bpy.ops.render.render(write_still=True)
        out.append(sc.render.filepath)
    return out
