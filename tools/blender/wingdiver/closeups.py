# Gros plans d'un personnage enregistré (.blend) : buste de trois-quarts, dos (réacteur), jambes.
#   blender -b perso.blend --python tools/blender/wingdiver/closeups.py -- DOSSIER PREFIXE
import bpy, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
folder, prefix = argv[0], argv[1]
sc = bpy.context.scene
shots = {
    "buste": ((0.55, -0.75, 1.42), (0.0, 0.0, 1.3), 60),
    "epaule": ((0.55, -0.35, 1.45), (0.15, 0.0, 1.38), 70),
    "reacteur": ((-0.45, 0.95, 1.45), (0.0, 0.1, 1.25), 55),
    "jambes": ((0.5, -1.1, 0.55), (0.0, 0.0, 0.45), 50),
}
for name, (loc, target, lens) in shots.items():
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cam = bpy.data.objects.new(name, cd)
    sc.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1000
    sc.render.filepath = os.path.join(folder, f"{prefix}_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"[perso] gros plan {name}", flush=True)
