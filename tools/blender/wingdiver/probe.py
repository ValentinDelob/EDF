# Sonde : crée une humaine MPFB et affiche dimensions, groupes de sommets et os du squelette « game_engine ».
import bpy, sys
from bl_ext.user_default.mpfb.services.humanservice import HumanService

info = HumanService._create_default_human_info_dict()
info["phenotype"].update({"gender": 0.0, "age": 0.45, "muscle": 0.6, "weight": 0.45, "proportions": 0.85, "height": 0.55,
                          "cupsize": 0.6, "firmness": 0.75})
info["rig"] = "game_engine"
settings = HumanService.get_default_deserialization_settings()
base = HumanService.deserialize_from_dict(info, settings)
print("[probe] base", base.name, "dims", tuple(round(x, 3) for x in base.dimensions))
print("[probe] groups", [g.name for g in base.vertex_groups][:200])
rig = base.parent
if rig and rig.type == "ARMATURE":
    for b in rig.data.bones:
        h = rig.matrix_world @ b.head_local
        print(f"[probe] bone {b.name} head=({h.x:.3f},{h.y:.3f},{h.z:.3f})")
