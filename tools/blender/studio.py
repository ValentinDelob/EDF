# Scène de présentation (rendus de contrôle des modèles) : sol mat, soleil doux, ciel gris, caméras listées.
# Utilisation (serveur MCP de Blender) : VIEWS = [((x, y, z), (cible)), …] ; OUTDIR = dossier ; exec(studio.py).
import bpy, math, os
from mathutils import Vector

sc = bpy.context.scene
cam = bpy.data.objects.get("StudioCam")
if cam is None:
    cam = bpy.data.objects.new("StudioCam", bpy.data.cameras.new("StudioCam"))
    sc.collection.objects.link(cam)
cam.data.lens = globals().get("LENS", 40)
sc.camera = cam
sun = bpy.data.objects.get("StudioSun")
if sun is None:
    sun = bpy.data.objects.new("StudioSun", bpy.data.lights.new("StudioSun", "SUN"))
    sc.collection.objects.link(sun)
sun.data.energy = 3.2
sun.data.angle = math.radians(8)
sun.rotation_euler = (math.radians(52), 0, math.radians(-35))
fill = bpy.data.objects.get("StudioFill")
if fill is None:
    fill = bpy.data.objects.new("StudioFill", bpy.data.lights.new("StudioFill", "SUN"))
    sc.collection.objects.link(fill)
fill.data.energy = 0.8
fill.rotation_euler = (math.radians(60), 0, math.radians(150))
if not sc.world:
    sc.world = bpy.data.worlds.new("W")
sc.world.use_nodes = True
bg = sc.world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.42, 0.45, 0.48, 1)
bg.inputs[1].default_value = 0.7
ground = bpy.data.objects.get("StudioGround")
if ground is None:
    me = bpy.data.meshes.new("StudioGround")
    s = 80
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("StudioGround", me)
    sc.collection.objects.link(ground)
    mat = bpy.data.materials.new("StudioGroundMat")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.32, 0.3, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    me.materials.append(mat)
engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
try:
    sc.eevee.use_shadows = True
    sc.eevee.use_gtao = True
except Exception:
    pass
sc.render.resolution_x, sc.render.resolution_y = globals().get("RES", (1600, 900))
sc.view_settings.view_transform = 'AgX' if 'AgX' in [v.identifier for v in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'
OUTDIR = globals().get("OUTDIR")
os.makedirs(OUTDIR, exist_ok=True)
paths = []
for i, (loc, target) in enumerate(globals().get("VIEWS", [])):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUTDIR, f"view{i}.png")
    bpy.ops.render.render(write_still=True)
    paths.append(sc.render.filepath)
result = {"rendered": paths}
