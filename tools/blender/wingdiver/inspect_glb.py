# Inspection d'un .glb de personnage : triangles par maillage, os, clés de forme, matériaux et textures.
#   blender -b --factory-startup --python tools/blender/wingdiver/inspect_glb.py -- fichier.glb
import bpy, sys

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=path)
total = 0
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if o.type == "MESH":
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += tris
        keys = [k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else []
        mats = [m.name for m in o.data.materials if m]
        print(f"[glb] {o.name}: {tris} tri, {len(o.data.vertices)} sommets, groupes {len(o.vertex_groups)}, clés {keys}, mat {mats}")
    elif o.type == "ARMATURE":
        print(f"[glb] squelette {o.name}: {len(o.data.bones)} os : {[b.name for b in o.data.bones][:80]}")
    else:
        print(f"[glb] {o.type} {o.name} {tuple(round(x, 3) for x in o.matrix_world.translation)}")
print(f"[glb] total {total} triangles")
for im in bpy.data.images:
    print(f"[glb] image {im.name} {im.size[0]}x{im.size[1]}")
