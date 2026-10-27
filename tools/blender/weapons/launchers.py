# Lance-roquettes et lance-missiles du Ranger (EDF 5), portés à l'épaule : tube (ou caisson) sur l'axe u (z = 0), poignée
# pistolet sous un boîtier de mise à feu, poignée avant verticale (repère SupportV), appui d'épaule rembourré sous le tube
# (repère Butt : le tube repose sur l'épaule). Une fonction par famille de modèle.
#
# Langage commun : tubes composites (surface de révolution à joints d'enroulement, bagues métalliques), venturis évasés creux
# (col visible), bouches creuses (âme sombre), unités de visée optroniques (objectifs, oculaire caoutchouc, écrans), caissons
# multi-alvéoles découpés (têtes de roquettes visibles, opercules arrière), sangles, câbles, étiquettes d'avertissement cintrées.
import math
import bmesh
from mathutils import Vector, Matrix


# ───────────────────────── Outils de forme (propres au module) ─────────────────────────

def mv(bm, x=0.0, u=0.0, z=0.0):
    """Déplace une forme (repère de travail)."""
    bmesh.ops.translate(bm, vec=P(x, u, z), verts=bm.verts)
    return bm


def rot_u(bm, a, cx=0.0, cz=0.0):
    """Tourne une forme autour d'un axe parallèle à u passant par (cx, cz) : a = 0 → +x (droite), π/2 → dessus."""
    bmesh.ops.rotate(bm, cent=P(cx, 0, cz), matrix=Matrix.Rotation(-a, 3, 'Y'), verts=bm.verts)
    return bm


def lathe(pts, seg=24, x=0.0, z=0.0, closed=False, arc=None):
    """Surface de révolution autour de l'axe u : pts = [(u, r)].
    closed : profil fermé (pièce creuse : contour extérieur puis intérieur) ; sinon pièce pleine bouchée aux extrémités
    (r = 0 : pointe). arc = (a0, a1) : secteur angulaire (plaques cintrées ; profil fermé exigé)."""
    bm = bmesh.new()
    if arc:
        angs = [arc[0] + (arc[1] - arc[0]) * i / seg for i in range(seg + 1)]
    else:
        angs = [2 * math.pi * i / seg for i in range(seg)]
    n = len(angs)
    loops = []
    for (u, r) in pts:
        if r < 1e-5 and not closed:
            loops.append([bm.verts.new(P(x, u, z))])
        else:
            loops.append([bm.verts.new(P(x + r * math.cos(a), u, z + r * math.sin(a))) for a in angs])
    m = len(loops)
    pairs = [(k, k + 1) for k in range(m - 1)] + ([(m - 1, 0)] if closed else [])
    for ka, kb in pairs:
        A, B = loops[ka], loops[kb]
        for i in range(seg):
            j = (i + 1) % n
            if len(A) == 1:
                bm.faces.new((A[0], B[j], B[i]))
            elif len(B) == 1:
                bm.faces.new((A[i], A[j], B[0]))
            else:
                bm.faces.new((A[i], A[j], B[j], B[i]))
    if arc:
        bm.faces.new([L[0] for L in loops])
        bm.faces.new([L[-1] for L in loops])
    elif not closed:
        for L in (loops[0], loops[-1]):
            if len(L) > 2:
                bm.faces.new(L)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def shell(u0, u1, r_out, r_in, seg=24, x=0.0, z=0.0, ch=0.0, arc=None):
    """Bague / manchon creux à chanfreins (ch) : plus fin que ring() et cintrable (arc)."""
    c = min(ch, (u1 - u0) * 0.3, (r_out - r_in) * 0.45)
    pts = [(u0, r_in), (u0, r_out - c), (u0 + c, r_out), (u1 - c, r_out), (u1, r_out - c), (u1, r_in)]
    if c <= 0:
        pts = [(u0, r_in), (u0, r_out), (u1, r_out), (u1, r_in)]
    return lathe(pts, seg, x, z, True, arc)


def tube_z(r, z0, z1, x=0.0, u=0.0, seg=14, r1=None):
    """Cylindre vertical (axe z)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r1 is None else r1, depth=abs(z1 - z0))
    bmesh.ops.translate(bm, vec=P(x, u, (z0 + z1) / 2), verts=bm.verts)
    return bm


def zloft(sections, seg=14):
    """Loft vertical (poignées) : sections = [(z, demi-largeur x, demi-profondeur u, u du centre, exposant)], de haut en bas."""
    bm = bmesh.new()
    loops = []
    for (z, hw, hd, uc, p) in sections:
        loop = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ca, sa = math.cos(a), math.sin(a)
            loop.append(bm.verts.new(P(hw * math.copysign(abs(ca) ** (2 / p), ca), uc + hd * math.copysign(abs(sa) ** (2 / p), sa), z)))
        loops.append(loop)
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(loops[0])
    bm.faces.new(list(reversed(loops[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def sweep(pts, rw, rh=None, seg=8, sub=4, up=(0, 0, 1)):
    """Câble / sangle / poignée cintrée : section elliptique (rw × rh, rh selon « up ») balayée le long d'une courbe lissée
    (Catmull-Rom) passant par pts = [(x, u, z)]."""
    rh = rw if rh is None else rh
    V = [P(*p) for p in pts]
    S = []
    for i in range(len(V) - 1):
        p0 = V[i - 1] if i > 0 else V[i]
        p1, p2 = V[i], V[i + 1]
        p3 = V[i + 2] if i + 2 < len(V) else V[i + 1]
        for k in range(sub):
            t = k / sub
            S.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    S.append(V[-1])
    bm = bmesh.new()
    N = P(*up).normalized()
    loops = []
    for i, p in enumerate(S):
        T = (S[min(i + 1, len(S) - 1)] - S[max(i - 1, 0)]).normalized()
        N = (N - T * N.dot(T))
        if N.length < 1e-6:
            N = T.orthogonal()
        N.normalize()
        B = T.cross(N)
        loops.append([bm.verts.new(p + B * (rw * math.cos(2 * math.pi * k / seg)) + N * (rh * math.sin(2 * math.pi * k / seg))) for k in range(seg)])
    for k in range(len(loops) - 1):
        A, Bl = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], Bl[j], Bl[i]))
    bm.faces.new(loops[0])
    bm.faces.new(list(reversed(loops[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


class _Grab:
    """Collecteur de formes (pour récupérer le texte de stencil() avant de le cintrer)."""

    def __init__(self):
        self.items = []

    def add(self, bm, mat, mtx=None):
        self.items.append((bm, mat))


def bend(bm, r, ang, cx=0.0, cz=0.0, side=1):
    """Cintre une forme plate construite contre le plan x = 0 (épaisseur selon ±x, side) sur un cylindre d'axe u
    (rayon r, centre cx, cz), à l'angle ang (0 = flanc droit, π/2 = dessus, π = flanc gauche)."""
    for v in bm.verts:
        X, U, Z = v.co.x, -v.co.y, v.co.z
        th = ang + side * Z / r
        rad = r + side * X
        v.co = P(cx + rad * math.cos(th), U, cz + rad * math.sin(th))
    return bm


def text_bm(text, h, side=1, res=3):
    """Texte plat (une face, courbes peu subdivisées : ~25 triangles par lettre) dans le plan d'un flanc (x = 0), centré
    en (u = 0, z = 0), lisible depuis le côté « side » (même convention que stencil())."""
    cu = bpy.data.curves.new("txt", 'FONT')
    cu.body = text
    cu.size = h
    cu.resolution_u = res
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    ob = bpy.data.objects.new("txt", cu)
    bpy.context.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    for v in bm.verts:
        tx, ty = v.co.x, v.co.y
        v.co = P(0.0, tx if side > 0 else -tx, ty)
    bm.normal_update()
    if bm.faces and sum(f.normal.x for f in bm.faces) * side < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def flat_text(part, text, u, z, x, h, mat=MARKING, side=1):
    """Marquage plat sur un flanc plan (x), centré en (u, z), lisible depuis l'extérieur (side ±1)."""
    part.add(mv(text_bm(text, h, side), x + side * 0.0004, u, z), mat)


def wrap_text(part, text, u, r, ang, h, mat=MARKING, cx=0.0, cz=0.0):
    """Texte collé sur un cylindre (centré en u, à l'angle ang), lisible de l'extérieur."""
    side = 1 if math.cos(ang) >= -1e-6 else -1
    bm = text_bm(text, h, side)
    bend(bm, r + 0.0005, ang, cx, cz, side)
    part.add(mv(bm, 0, u, 0), mat)


def plate(part, u, z, x, side, lu, lz, text=None, th=None, stripes=True, mat=MARKING, ink=POLY):
    """Étiquette d'avertissement plane sur un flanc (x, normale side) : plaque claire, bandes de danger, texte noir."""
    part.add(box(0.0016, lu, lz, (x + side * 0.0008, u, z), 0.0006, 1), mat)
    xs = x + side * 0.0016
    us = u - side * 0  # (repère : u croît vers la bouche)
    if stripes:
        w = lz * 0.26
        for k in range(3):
            uu = u - lu / 2 + 0.003 + k * w * 0.9
            part.add(prism([(uu, z - lz / 2 + 0.002), (uu + w * 0.45, z - lz / 2 + 0.002), (uu + w * 0.9, z + lz / 2 - 0.002),
                            (uu + w * 0.45, z + lz / 2 - 0.002)], min(xs, xs + side * 0.0005), max(xs, xs + side * 0.0005), 0), ink)
    if text:
        flat_text(part, text, u + (lu * 0.14 if stripes else 0), z, xs, th or lz * 0.42, ink, side)


def cut_slots(u0, u1, n, x, z, w, h, depth, axis='x'):
    """Formes de découpe bon marché (fentes à bouts adoucis d'un seul pan)."""
    out = []
    step = (u1 - u0) / max(1, n)
    for k in range(n):
        uu = u0 + step * (k + 0.5)
        if axis == 'x':
            out.append(box(depth, w, h, (x, uu, z), min(w, h) * 0.35, 1))
        else:
            out.append(box(h, w, depth, (x, uu, z), min(w, h) * 0.35, 1))
    return out


def label(part, u, r, ang, lu, lz, text=None, th=None, stripes=False, cx=0.0, cz=0.0, mat=MARKING, ink=POLY):
    """Étiquette d'avertissement cintrée (plaque claire, texte noir, bandes de danger) : lu × lz, centrée en (u, ang)."""
    da = lz / 2 / r
    part.add(shell(u - lu / 2, u + lu / 2, r + 0.0012, r - 0.0005, max(4, int(lz / 0.006)), cx, cz, 0, (ang - da, ang + da)), mat)
    if text:
        wrap_text(part, text, u + (lu * 0.12 if stripes else 0), r + 0.0012, ang, th or lz * 0.42, ink, cx, cz)
    if stripes:
        w = lz * 0.28
        u0 = u - lu / 2 + 0.003
        for k in range(3):
            uu = u0 + k * w * 0.9
            if uu + w * 0.9 > u - lu * 0.16:
                break
            bm = prism([(uu, -lz / 2 + 0.002), (uu + w * 0.45, -lz / 2 + 0.002), (uu + w * 0.9, lz / 2 - 0.002), (uu + w * 0.45, lz / 2 - 0.002)], 0.0, 0.0006, 0)
            part.add(bend(bm, r + 0.0012, ang, cx, cz), ink)


def rivets(part, u, r, angs, rr=0.0028, mat=METAL, cx=0.0, cz=0.0):
    """Rivets / têtes de vis bombées posés radialement sur un cylindre."""
    for a in angs:
        bm = lathe([(-rr * 0.2, rr), (rr * 0.25, rr * 0.85), (rr * 0.5, rr * 0.4), (rr * 0.55, 0.0)], 8)
        # Axe u → axe radial : on couche la forme sur x puis on la tourne.
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Z'), verts=bm.verts)
        mv(bm, r, u, 0)
        part.add(rot_u(bm, a, 0, 0) if (cx == 0 and cz == 0) else mv(rot_u(bm, a), cx, 0, cz), mat)


def band(part, u, r, w=0.022, mat=METAL, h=0.0035, seg=32, cx=0.0, cz=0.0, bolts=0):
    """Bague de renfort / collier sur un tube (chanfreinée), avec n boulons éventuels."""
    part.add(shell(u - w / 2, u + w / 2, r + h, r - 0.002, seg, cx, cz, h * 0.6), mat)
    if bolts:
        rivets(part, u, r + h, [math.pi / 2 + 2 * math.pi * k / bolts for k in range(bolts)], min(0.0035, w * 0.18), METAL, cx, cz)


def clamp(part, u, r, w=0.03, mat=METAL, ang=math.pi / 2, seg=32):
    """Collier de serrage : bague + oreilles boulonnées à l'angle ang."""
    part.add(shell(u - w / 2, u + w / 2, r + 0.005, r - 0.002, seg, 0, 0, 0.002), mat)
    lug = merge(box(0.016, w * 0.75, 0.005, (r + 0.012, u, -0.0055), 0.0012), box(0.016, w * 0.75, 0.005, (r + 0.012, u, 0.0055), 0.0012),
                tube_z(0.0032, -0.012, 0.012, r + 0.014, u, 10))
    part.add(rot_u(lug, ang), mat)


def merge(*bms):
    """Réunit plusieurs formes en une (pour les tourner ensemble)."""
    out = bmesh.new()
    for bm in bms:
        me = bpy.data.meshes.new("m")
        bm.to_mesh(me)
        bm.free()
        out.from_mesh(me)
        bpy.data.meshes.remove(me)
    return out


def comp_tube(part, r, u0, u1, mat=PAINT, seams=(), bore=None, wall=0.13, seg=32, rear_ch=True):
    """Tube composite : surface de révolution à joints d'enroulement creusés (seams), bouche creuse sur « bore » mètres
    (âme sombre au fond). Renvoie le rayon de l'âme."""
    rin = r * (1 - wall)
    pts = [(u0, r * 0.96), (u0 + 0.006, r)] if rear_ch else [(u0, r)]
    for s in sorted(seams):
        pts += [(s - 0.004, r), (s - 0.0015, r * 0.986), (s + 0.0015, r * 0.986), (s + 0.004, r)]
    pts += [(u1 - 0.004, r), (u1, r * 0.975)]
    if bore:
        pts += [(u1, rin), (u1 - bore, rin)]
    part.add(lathe(pts, seg), mat)
    if bore:
        part.add(tube(rin * 0.999, rin * 0.999, u1 - bore + 0.0015, u1 - bore + 0.003, 0, 0, seg), POLY)
    return rin


def venturi(part, u_rear, u_front, r, flare=1.4, mat=METAL, seg=32, ribs=0, rib_mat=None):
    """Venturi arrière creux : cloche évasée (profil en tulipe), col étroit visible, lèvre renforcée, nervures optionnelles."""
    L = u_front - u_rear
    R = r * flare
    outer = [(u_front, r * 1.03)]
    for k in range(1, 7):
        t = k / 6
        outer.append((u_front - L * 0.92 * t, r * 1.0 + (R - r) * t ** 1.7))
    outer += [(u_rear + 0.004, R * 1.02), (u_rear, R * 0.99)]
    inner = [(u_rear, R * 0.9)]
    for k in range(1, 5):
        t = k / 4
        inner.append((u_rear + L * 0.6 * t, R * 0.9 - (R * 0.9 - r * 0.5) * t ** 0.8))
    inner += [(u_front - L * 0.1, r * 0.48), (u_front, r * 0.48)]
    part.add(lathe(outer + inner, seg, closed=True), mat)
    part.add(tube(r * 0.49, r * 0.49, u_rear + L * 0.66, u_rear + L * 0.68, 0, 0, seg // 2), POLY)        # col (sombre)
    part.add(shell(u_rear - 0.004, u_rear + 0.012, R * 1.05, R * 0.9, seg, 0, 0, 0.003), mat)          # lèvre
    for k in range(ribs):
        a = 2 * math.pi * k / ribs + math.pi / ribs
        prof = [(u_front - L * 0.1, r * 0.98), (u_front - L * 0.05, r * 1.12), (u_rear + L * 0.18, R * 1.06), (u_rear + 0.012, R * 1.04),
                (u_rear + 0.012, R * 0.96), (u_rear + L * 0.3, r + (R - r) * 0.55)]
        rib = prism(prof, -0.003, 0.003, 0.001)
        part.add(rot_u(rib, a - math.pi / 2), rib_mat if rib_mat is not None else mat)


def muzzle_collar(part, u0, u1, r, rin, mat=METAL, bumper=True, seg=32):
    """Bouche renforcée creuse (bourrelet caoutchouc optionnel)."""
    part.add(lathe([(u0, rin), (u0, r * 1.02), (u0 + 0.006, r * 1.09), (u1 - 0.006, r * 1.09), (u1, r * 1.05), (u1, rin)], seg, closed=True), mat)
    if bumper:
        part.add(shell(u1 - 0.004, u1 + 0.014, r * 1.1, rin * 1.02, seg, 0, 0, 0.004), RUBBER)


# ───────────────────────── Ensembles communs ─────────────────────────

def trigger_group(part, u, zt, w=0.034, mat=POLY):
    """Boîtier de mise à feu sous le tube (zt = surface basse du tube) + poignée pistolet, détente, sûreté, vis.
    Renvoie le point de la main droite."""
    zb = zt - 0.032
    part.add(prism([(u - 0.055, zt + 0.014), (u + 0.115, zt + 0.014), (u + 0.115, zt - 0.006), (u + 0.095, zb), (u - 0.025, zb),
                    (u - 0.06, zt - 0.012)], -w / 2, w / 2, 0.003), mat)
    part.add(box(w + 0.006, 0.18, 0.007, (0, u + 0.03, zt + 0.002), 0.002), METAL)                      # semelle
    grip = pistol_grip(part, u, zb + 0.004, POLY, 16, 0.115, 0.033, 0.048)
    part.add(prism([(u + 0.028, zb + 0.002), (u + 0.04, zb + 0.002), (u + 0.037, zb - 0.02), (u + 0.026, zb - 0.027),
                    (u + 0.03, zb - 0.012)], -0.003, 0.003, 0.0008), METAL)                            # détente
    part.add(tube_x(0.0075, 0.006, (-w / 2 - 0.003, u + 0.072, zt - 0.014), 14), METAL)                # sûreté
    part.add(box(0.004, 0.032, 0.007, (-w / 2 - 0.0065, u + 0.06, zt - 0.012), 0.0015), METAL)
    part.add(box(0.005, 0.03, 0.012, (w / 2 + 0.002, u + 0.07, zt - 0.012), 0.002), RUBBER)             # bouton d'armement
    for s in (-1, 1):
        screw(part, u - 0.035, zt - 0.006, s * w / 2, 0.0026, s)
        screw(part, u + 0.098, zt - 0.003, s * w / 2, 0.0026, s)
        serrations(part, u - 0.05, u - 0.03, zt - 0.02, zt - 0.006, s * w / 2, 4, s)
    return grip


def front_grip(part, u, zt, clamp_r=None, h=0.1, mat=POLY):
    """Poignée avant verticale à empreintes de doigts, pied boulonné, bouton de verrouillage. Renvoie la main gauche."""
    if clamp_r:
        clamp(part, u, clamp_r, 0.034, METAL, 0.0)
    part.add(prism([(u - 0.032, zt + 0.012), (u + 0.032, zt + 0.012), (u + 0.025, zt - 0.016), (u - 0.025, zt - 0.016)], -0.017, 0.017, 0.003), METAL)
    z0 = zt - 0.014
    prof = [(0, 0.017, 0.019), (0.012, 0.0175, 0.021), (0.026, 0.0158, 0.0175), (0.038, 0.0172, 0.0205), (0.052, 0.0158, 0.0175),
            (0.064, 0.0172, 0.0205), (0.078, 0.0158, 0.0178), (h - 0.008, 0.0185, 0.0225), (h, 0.018, 0.021)]
    part.add(zloft([(z0 - dz, hw, hd, u - dz * 0.12, 2.6) for (dz, hw, hd) in prof], 16), mat)
    part.add(tube(0.0055, 0.0055, u + 0.012, u + 0.026, 0, z0 - 0.02, 10), RUBBER)                   # bouton
    part.add(tube_x(0.004, 0.036, (0, u, zt - 0.006), 10), METAL)                                       # goupille
    return (0, u - h * 0.06, z0 - h * 0.5)


def shoulder_pad(part, u, zt, l=0.18, w=0.036, d=0.032, clamp_r=None):
    """Appui d'épaule rembourré sous le tube (semelle, coussin strié, colliers). Renvoie le repère Butt (contact épaule)."""
    if clamp_r:
        for uu in (u - l * 0.36, u + l * 0.36):
            clamp(part, uu, clamp_r, 0.022, METAL, math.pi / 2)
    part.add(prism([(u - l / 2 + 0.012, zt + 0.01), (u + l / 2 - 0.012, zt + 0.01), (u + l / 2 - 0.022, zt - 0.012),
                    (u - l / 2 + 0.022, zt - 0.012)], -w * 0.75, w * 0.75, 0.002), POLY)
    zc = zt - 0.01 - d / 2
    pad = loft([(u - l / 2, w * 0.7, d * 0.3, zc + d * 0.2, 3.0), (u - l / 2 + 0.025, w, d / 2, zc, 3.0), (u + l / 2 - 0.035, w, d / 2, zc, 3.0),
                (u + l / 2, w * 0.6, d * 0.28, zc + d * 0.22, 3.0)], 20)
    pad = carve(pad, cut_slots(u - l / 2 + 0.03, u + l / 2 - 0.04, 6, 0, zc - d / 2, 0.005, w * 2.4, 0.01, 'z'))
    part.add(pad, RUBBER)
    return (0, u, zc - d / 2)


def swivel(part, u, r, ang=-math.pi / 2 - 0.6):
    """Anneau de bretelle sur sa patte (sur un tube de rayon r, à l'angle ang)."""
    bm = merge(box(0.012, 0.016, 0.01, (r + 0.004, u, 0), 0.002), mv(ring(0.011, 0.0075, -0.0025, 0.0025, 0, 0, 12), r + 0.016, u, 0))
    part.add(rot_u(bm, ang), METAL)


def sling(part, u0, u1, r, ang=0.0, w=0.022, sag=0.0):
    """Bretelle repliée le long du tube (sangle plate, boucle), du collier u0 au collier u1, côté ang."""
    pts = []
    for k in range(9):
        t = k / 8
        rr = r + 0.007 + sag * math.sin(math.pi * t)
        pts.append((rr, u0 + (u1 - u0) * t, 0))
    st = sweep(pts, w / 2, 0.0018, 8, 2, (1, 0, 0))
    bk = box(0.006, 0.03, w * 1.15, (r + 0.0095 + sag * 0.9, u0 + (u1 - u0) * 0.35, 0), 0.0015)
    part.add(rot_u(st, ang), POLY)
    part.add(rot_u(bk, ang), METAL)


def carry_handle(part, u0, u1, zt, h=0.055, x=0.0, mat=METAL):
    """Poignée de transport cintrée sur deux pieds (dessus du tube ou du caisson)."""
    for uu in (u0, u1):
        part.add(prism([(uu - 0.018, zt - 0.004), (uu + 0.018, zt - 0.004), (uu + 0.01, zt + 0.012), (uu - 0.01, zt + 0.012)], x - 0.012, x + 0.012, 0.002), mat)
    m = (u0 + u1) / 2
    part.add(sweep([(x, u0, zt + 0.006), (x, u0 + 0.012, zt + h * 0.8), (x, m, zt + h), (x, u1 - 0.012, zt + h * 0.8), (x, u1, zt + 0.006)], 0.0085, 0.007, 10, 5), mat)
    part.add(sweep([(x, m - (u1 - u0) * 0.28, zt + h * 0.98), (x, m, zt + h + 0.001), (x, m + (u1 - u0) * 0.28, zt + h * 0.98)], 0.0105, 0.0095, 10, 4), RUBBER)


def cable(part, pts, r=0.0045, mat=RUBBER):
    """Câble électrique gainé avec ses deux connecteurs."""
    part.add(sweep(pts, r, r, 8, 5), mat)
    for p in (pts[0], pts[-1]):
        part.add(box(0.013, 0.013, 0.013, p, 0.003), METAL)


# ───────────────────────── Optiques et électronique ─────────────────────────

def reflex_sight(part, u, r, side=-1, zc=0.034, l=0.13, rr=0.019, mat=METAL):
    """Viseur réflexe sur bras latéral : corps tubulaire, pare-soleil, tourelles, oculaire caoutchouc, molette de serrage."""
    x = side * (r + 0.05)
    xa0, xa1 = side * r * 0.6, side * (r + 0.05)
    part.add(box(abs(xa1 - xa0), 0.044, 0.012, ((xa0 + xa1) / 2, u, zc - rr - 0.004), 0.003), mat)          # bras
    part.add(box(0.022, 0.06, 0.024, (side * (r + 0.006), u, zc - rr - 0.006), 0.004), mat)                # embase
    part.add(tube_x(0.009, 0.012, (side * (r + 0.024), u + 0.02, zc - rr - 0.016), 16), METAL)            # molette
    b0, b1 = u - l / 2, u + l / 2
    part.add(lathe([(b0, rr * 0.82), (b0 + 0.004, rr * 1.06), (b0 + 0.022, rr * 1.06), (b0 + 0.028, rr), (b1 - 0.034, rr),
                    (b1 - 0.028, rr * 1.12), (b1, rr * 1.12)], 20, x, zc), mat)
    part.add(shell(b1, b1 + 0.035, rr * 1.12, rr * 1.02, 20, x, zc, 0.002), mat)                         # pare-soleil
    part.add(tube(rr * 1.02, rr * 1.02, b1 + 0.001, b1 + 0.003, x, zc, 20), LENS)
    part.add(lathe([(b0 - 0.03, rr * 1.18), (b0 - 0.026, rr * 1.2), (b0, rr * 0.92), (b0, rr * 0.72), (b0 - 0.026, rr * 1.02)], 20, x, zc, True), RUBBER)
    part.add(tube(rr * 0.75, rr * 0.75, b0 - 0.002, b0 + 0.001, x, zc, 16), GLASS)
    part.add(tube_z(0.0095, zc + rr * 0.8, zc + rr + 0.012, x, u + 0.005, 14), mat)                     # tourelles
    part.add(tube_z(0.011, zc + rr + 0.01, zc + rr + 0.014, x, u + 0.005, 14), POLY)
    part.add(tube_x(0.0095, 0.022, (x + side * rr, u + 0.005, zc), 14), mat)
    part.add(tube_x(0.011, 0.004, (x + side * (rr + 0.011), u + 0.005, zc), 14), POLY)
    for uu in (b0 + 0.04, b1 - 0.045):
        part.add(shell(uu - 0.006, uu + 0.006, rr * 1.12, rr * 0.95, 20, x, zc, 0.002), METAL)
    return (x, u, zc - rr)


def flip_sight(part, u, r, side=-1, rear=False):
    """Hausse / guidon d'appoint repliable sur le dessus gauche."""
    a = math.pi / 2 + side * -0.35
    base = box(0.01, 0.026, 0.024, (r + 0.004, u, 0), 0.002)
    if rear:
        blade = prism([(u - 0.012, 0.0), (u + 0.012, 0.0), (u + 0.01, 0.03), (u - 0.01, 0.03)], -0.003, 0.003, 0.001)
    else:
        blade = prism([(u - 0.008, 0.0), (u + 0.008, 0.0), (u + 0.002, 0.034), (u - 0.002, 0.034)], -0.0025, 0.0025, 0.0008)
    bmesh.ops.rotate(blade, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'), verts=blade.verts)
    mv(blade, r + 0.007, 0, 0)
    part.add(rot_u(merge(base, blade), a), METAL)


def fcu(part, u, r, side=-1, zc=0.045, l=0.16, w=0.07, h=0.085, mat=PAINT, arm=True):
    """Conduite de tir optronique : boîtier aux angles adoucis à ailettes, objectif à pare-soleil, télémètre laser,
    oculaire caoutchouc, écran arrière, boutons, bras de montage. Renvoie le point de départ du câble."""
    x = side * (r + w / 2 + 0.024)
    if arm:
        xa0, xa1 = side * r * 0.55, side * (r + 0.03)
        part.add(box(abs(xa1 - xa0), 0.06, 0.022, ((xa0 + xa1) / 2, u, zc - h * 0.3), 0.004), METAL)
        part.add(tube_x(0.012, 0.014, (side * (r + 0.02), u, zc - h * 0.3 - 0.016), 16), METAL)
    body = loft([(u - l / 2, w / 2 * 0.9, h / 2 * 0.9, 0, 6), (u - l / 2 + 0.008, w / 2, h / 2, 0, 6), (u + l / 2 - 0.012, w / 2, h / 2, 0, 6),
                 (u + l / 2, w / 2 * 0.88, h / 2 * 0.88, 0, 6)], 28)
    mv(body, x, 0, zc)
    body = carve(body, cut_slots(u - l / 2 + 0.03, u + l / 2 - 0.05, 6, x, zc + h / 2, 0.005, w * 0.62, 0.008, 'z')
                 + cut_slots(u - l / 2 + 0.03, u + l / 2 - 0.05, 5, x + side * w / 2, zc - h * 0.15, 0.005, h * 0.35, 0.008, 'x'))
    part.add(body, mat)
    fu = u + l / 2
    R = h * 0.3
    zo = zc + h * 0.1
    xo = x - side * w * 0.12
    part.add(shell(fu - 0.004, fu + 0.035, R * 1.08, R * 0.92, 24, xo, zo, 0.003), METAL)               # objectif
    part.add(tube(R * 0.93, R * 0.93, fu + 0.004, fu + 0.007, xo, zo, 24), LENS)
    xl, zl = x + side * w * 0.27, zc - h * 0.26
    part.add(shell(fu - 0.004, fu + 0.016, 0.011, 0.007, 14, xl, zl, 0.002), METAL)                     # télémètre
    part.add(tube(0.0072, 0.0072, fu + 0.004, fu + 0.006, xl, zl, 14), GLOW)
    part.add(tube(0.0075, 0.0075, fu + 0.0, fu + 0.006, xl, zc + h * 0.33, 12), LENS)
    bu = u - l / 2
    xe, ze = x - side * w * 0.15, zc + h * 0.12
    part.add(lathe([(bu - 0.04, 0.026), (bu - 0.036, 0.028), (bu, 0.019), (bu, 0.014), (bu - 0.036, 0.023)], 20, xe, ze, True), RUBBER)
    part.add(tube(0.014, 0.014, bu - 0.003, bu + 0.001, xe, ze, 16), GLASS)
    part.add(box(0.03, 0.003, 0.022, (x + side * w * 0.22, bu - 0.001, zc - h * 0.18), 0.001), ENERGY)    # écran
    part.add(box(0.036, 0.006, 0.028, (x + side * w * 0.22, bu + 0.001, zc - h * 0.18), 0.002), POLY)
    for k in range(3):
        part.add(tube_z(0.0045, zc + h / 2 - 0.004, zc + h / 2 + 0.004, x + side * (w * 0.3 - k * 0.012), bu + 0.022, 10), RUBBER)
    for uu in (u - l / 2 + 0.015, u + l / 2 - 0.02):
        screw(part, uu, zc + h * 0.33, x + side * w / 2, 0.0028, side)
        screw(part, uu, zc - h * 0.33, x + side * w / 2, 0.0028, side)
    return (x - side * w * 0.3, u - l * 0.25, zc - h / 2)


def seeker(part, u, zt, w=0.08, l=0.14, h=0.06, mat=METAL):
    """Unité de verrouillage (missiles) sur rail : capteur sous dôme, écran sous visière, évents, antenne."""
    rail(part, u - l / 2 + 0.01, u + l / 2 - 0.01, zt - 0.006, 0, 0.03)
    zc = zt + 0.012 + h / 2
    body = loft([(u - l / 2, w / 2 * 0.9, h / 2 * 0.88, zc, 5), (u - l / 2 + 0.008, w / 2, h / 2, zc, 5), (u + l / 2 - 0.016, w / 2, h / 2, zc, 5),
                 (u + l / 2, w / 2 * 0.86, h / 2 * 0.84, zc, 5)], 24)
    body = carve(body, cut_slots(u - l / 2 + 0.03, u + l / 2 - 0.04, 5, 0, zc + h / 2, 0.005, w * 0.55, 0.008, 'z'))
    part.add(body, mat)
    fu = u + l / 2
    R = h * 0.3
    part.add(shell(fu - 0.004, fu + 0.012, R * 1.25, R * 0.95, 20, 0, zc, 0.002), POLY)
    dome = [(fu, R)] + [(fu + R * math.sin(t * math.pi / 2) * 0.8, R * math.cos(t * math.pi / 2)) for t in (0.25, 0.5, 0.75, 1.0)]
    part.add(lathe(dome, 20, 0, zc), GLASS)
    part.add(tube(R * 0.35, R * 0.35, fu - 0.002, fu + R * 0.55, 0, zc, 12), LENS)
    bu = u - l / 2
    part.add(box(w * 0.75, 0.003, h * 0.6, (0, bu - 0.001, zc), 0.001), ENERGY)                          # écran
    for s in (-1, 1):
        part.add(box(0.004, 0.035, h * 0.75, (s * w * 0.42, bu - 0.016, zc + 0.002), 0.0012), POLY)       # visière
    part.add(box(w * 0.88, 0.035, 0.004, (0, bu - 0.016, zc + h * 0.39), 0.0012), POLY)
    part.add(tube_z(0.0025, zc + h / 2 - 0.004, zc + h / 2 + 0.05, w * 0.32, bu + 0.02, 8), METAL)       # antenne
    part.add(tube_z(0.0045, zc + h / 2 - 0.004, zc + h / 2 + 0.008, w * 0.32, bu + 0.02, 10), POLY)
    for k in range(3):
        part.add(tube_x(0.0045, 0.006, (-w / 2 - 0.002, bu + 0.025 + k * 0.016, zc - h * 0.15), 10), RUBBER)
    return (-w / 2, bu + 0.03, zc - h * 0.3)


def rocket_tip(part, u_base, r, l, x=0.0, z=0.0, mat=ACCENT, seg=14):
    """Ogive de roquette / missile (vue dans son alvéole)."""
    pts = [(u_base, r)]
    for k in range(1, 5):
        t = k / 4
        pts.append((u_base + l * t, r * math.sqrt(max(0.0, 1 - t * t)) * 0.98 + 0.0005))
    pts[-1] = (u_base + l, 0.0)
    part.add(lathe(pts, seg, x, z), mat)
    part.add(shell(u_base - 0.004, u_base + 0.004, r * 1.04, r * 0.7, seg, x, z), METAL)


def pod_box(part, cols, rows, cell, u0, u1, mat=PAINT, depth=0.07, tips=ACCENT, p=8.0, rear_covers=True, extra=()):
    """Caisson multi-alvéoles aux angles adoucis : alvéoles découpées à l'avant (têtes de roquettes visibles) et à
    l'arrière (opercules à rupture), bords chanfreinés. Renvoie (demi-largeur, demi-hauteur)."""
    hw, hh = cols * cell / 2 + 0.01, rows * cell / 2 + 0.01
    body = loft([(u0, hw - 0.006, hh - 0.006, 0, p), (u0 + 0.008, hw, hh, 0, p), (u1 - 0.008, hw, hh, 0, p), (u1, hw - 0.006, hh - 0.006, 0, p)], 36)
    cuts = []
    cells = [((c - (cols - 1) / 2) * cell, (k - (rows - 1) / 2) * cell) for c in range(cols) for k in range(rows)]
    for (x, z) in cells:
        cuts.append(tube(cell * 0.41, cell * 0.41, u1 - depth, u1 + 0.02, x, z, 14))
        if rear_covers:
            cuts.append(tube(cell * 0.38, cell * 0.38, u0 - 0.02, u0 + 0.018, x, z, 12))
    part.add(carve(body, cuts + list(extra)), mat)
    for (x, z) in cells:
        part.add(tube(cell * 0.41, cell * 0.41, u1 - depth - 0.001, u1 - depth + 0.004, x, z, 14), POLY)
        rocket_tip(part, u1 - depth * 0.62, cell * 0.33, depth * 0.5, x, z, tips, 12)
        part.add(shell(u1 - 0.006, u1 + 0.002, cell * 0.45, cell * 0.405, 16, x, z, 0.0015), METAL)
        if rear_covers:
            part.add(tube(cell * 0.38, cell * 0.38, u0 + 0.008, u0 + 0.011, x, z, 14), POLY)
            part.add(box(cell * 0.6, 0.004, 0.003, (x, u0 + 0.008, z), 0.0008, 1), METAL)
            part.add(box(0.003, 0.004, cell * 0.6, (x, u0 + 0.008, z), 0.0008, 1), METAL)
    return hw, hh


def pod_frame(part, u, hw, hh, w=0.024, mat=METAL, p=8.0, bolts=True):
    """Cadre de renfort autour d'un caisson (loft creux simulé : anneau superellipse) avec boulons latéraux."""
    part.add(loft([(u - w / 2, hw + 0.002, hh + 0.002, 0, p), (u - w / 2 + 0.003, hw + 0.006, hh + 0.006, 0, p),
                   (u + w / 2 - 0.003, hw + 0.006, hh + 0.006, 0, p), (u + w / 2, hw + 0.002, hh + 0.002, 0, p)], 36), mat)
    if bolts:
        for s in (-1, 1):
            for zz in (-hh * 0.6, 0, hh * 0.6):
                screw(part, u, zz, s * (hw + 0.006), 0.0032, s)


# ───────────────────────── Lance-roquettes ─────────────────────────

def grant(g):
    """Grant (type Carl Gustaf M4, 84 mm) : tube composite à joints d'enroulement et bagues, venturi évasé à charnière et
    levier de verrouillage, bouche à bourrelet, viseur réflexe sur bras gauche, hausse et guidon d'appoint, deux poignées,
    appui d'épaule, bretelle repliée, étiquettes « souffle arrière »."""
    b = g.body
    r = 0.046
    rin = comp_tube(b, r, -0.37, 0.53, PAINT, (-0.27, -0.17, 0.0, 0.14, 0.28, 0.4), 0.09)
    muzzle_collar(b, 0.5, 0.565, r, rin)
    for uu in (-0.3, 0.08, 0.44):
        band(b, uu, r, 0.02, METAL, 0.003)
    # Culasse : bague à tenons, charnière à droite, levier de verrouillage, venturi.
    b.add(lathe([(-0.4, r * 1.05), (-0.395, r * 1.18), (-0.36, r * 1.18), (-0.352, r * 1.02)], 32), METAL)
    for k in range(6):
        a = math.pi / 6 + k * math.pi / 3
        b.add(rot_u(box(0.008, 0.028, 0.014, (r * 1.18 + 0.002, -0.378, 0), 0.002), a), METAL)
    venturi(b, -0.56, -0.4, r * 1.02, 1.42, METAL)
    b.add(prism([(-0.43, -0.018), (-0.37, -0.018), (-0.37, 0.018), (-0.43, 0.018)], r * 1.15, r * 1.15 + 0.02, 0.003), METAL)   # charnière
    b.add(tube_z(0.008, -0.026, 0.026, r * 1.15 + 0.012, -0.4, 14), METAL)
    b.add(sweep([(r * 1.22, -0.38, 0.006), (0.062, -0.42, 0.014), (0.066, -0.46, 0.022), (0.07, -0.5, 0.028)], 0.005, 0.005, 8, 4), METAL)
    b.add(tube(0.009, 0.009, -0.53, -0.495, 0.071, 0.029, 12), RUBBER)                                  # levier
    grip = trigger_group(b, -0.04, -r)
    sup = front_grip(b, 0.2, -r - 0.004, r)
    butt = shoulder_pad(b, -0.21, -r - 0.002, 0.17, 0.034, 0.03, r)
    reflex_sight(b, 0.06, r, -1, 0.032)
    flip_sight(b, 0.45, r, -1)
    flip_sight(b, -0.12, r, -1, True)
    swivel(b, 0.44, r, -math.pi / 2 - 0.9)
    swivel(b, -0.3, r, -math.pi / 2 - 0.9)
    sling(b, -0.28, 0.42, r + 0.003, -0.35, 0.022)
    label(b, -0.24, r, math.pi / 2 + 0.25, 0.1, 0.032, "DANGER", 0.011, True)
    label(b, 0.32, r, math.pi + 0.15, 0.09, 0.024, "84 MM HE", 0.009)
    wrap_text(b, "SOUFFLE ARRIERE 30 M", -0.23, r * 1.2, math.pi / 2 + 1.0, 0.007, MARKING)
    laser_module(g.opt("laser"), 0.33, -0.02, r + 0.015)
    g.point("Muzzle", (0, 0.58, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.5, 0))


def leopard(g):
    """Leopard (type Javelin) : tube de lancement composite entre deux gros capuchons caoutchouc, unité de commande et de
    lancement (CLU) à gauche avec poignées, objectifs jour / thermique, oculaire et écran ; batterie de refroidissement,
    sangle de portage, autodirecteur visible dans la bouche."""
    b = g.body
    r = 0.062
    comp_tube(b, r, -0.5, 0.58, PAINT, (-0.36, -0.2, 0.05, 0.3, 0.45), None, rear_ch=False)
    # Capuchons caoutchouc côtelés (avant creux : coiffe vitrée du missile au fond).
    b.add(lathe([(-0.63, 0.066), (-0.625, 0.078), (-0.6, 0.086), (-0.5, 0.086), (-0.49, 0.072), (-0.48, 0.064)], 32), RUBBER)
    b.add(tube(0.05, 0.05, -0.632, -0.628, 0, 0, 24), POLY)
    for uu in (-0.585, -0.555, -0.525):
        b.add(shell(uu - 0.006, uu + 0.006, 0.09, 0.08, 32, 0, 0, 0.002), RUBBER)
    b.add(lathe([(0.56, 0.064), (0.57, 0.074), (0.585, 0.084), (0.69, 0.084), (0.7, 0.078), (0.7, 0.056), (0.62, 0.054)], 32), RUBBER)
    for uu in (0.61, 0.64, 0.67):
        b.add(shell(uu - 0.006, uu + 0.006, 0.088, 0.08, 32, 0, 0, 0.002), RUBBER)
    b.add(lathe([(0.621, 0.054), (0.64, 0.045), (0.655, 0.025), (0.66, 0.0)], 24), GLASS)
    b.add(shell(0.62, 0.628, 0.055, 0.04, 24, 0, 0, 0), METAL)
    for uu in (-0.42, 0.52):
        band(b, uu, r, 0.026, ACCENT, 0.004, 32, 0, 0, 6)
    # Batterie de refroidissement (BCU) sous l'arrière droit.
    b.add(lathe([(-0.47, 0.016), (-0.465, 0.022), (-0.33, 0.022), (-0.325, 0.016)], 16, r * 0.62, -r * 0.82), POLY)
    b.add(shell(-0.44, -0.43, 0.024, 0.02, 16, r * 0.62, -r * 0.82), METAL)
    b.add(shell(-0.36, -0.35, 0.024, 0.02, 16, r * 0.62, -r * 0.82), METAL)
    # CLU à gauche.
    side, u = -1, 0.0
    x = side * (r + 0.085)
    clu = loft([(u - 0.13, 0.062, 0.07, 0, 7), (u - 0.12, 0.07, 0.078, 0, 7), (u + 0.1, 0.07, 0.078, 0, 7), (u + 0.12, 0.064, 0.07, 0, 7)], 32)
    mv(clu, x, 0, 0.03)
    clu = carve(clu, cut_slots(u - 0.09, u + 0.07, 7, x + side * 0.07, 0.03 + 0.03, 0.006, 0.026, 0.01, 'x')
                + cut_slots(u - 0.09, u + 0.06, 6, x, 0.03 + 0.078, 0.006, 0.08, 0.01, 'z'))
    b.add(clu, PAINT)
    b.add(box(0.05, 0.08, 0.03, (side * (r + 0.02), u, 0.0), 0.006), METAL)                              # attache
    for (xo, zo, R) in ((-0.022, 0.05, 0.03), (0.024, 0.055, 0.022), (0.022, 0.005, 0.014)):
        xx = x + side * xo
        b.add(shell(u + 0.115, u + 0.15, R * 1.12, R * 0.9, 24, xx, zo, 0.003), METAL)
        b.add(tube(R * 0.92, R * 0.92, u + 0.12, u + 0.124, xx, zo, 24), LENS)
    b.add(lathe([(u - 0.18, 0.03), (u - 0.175, 0.032), (u - 0.13, 0.022), (u - 0.13, 0.016), (u - 0.175, 0.026)], 20, x - side * 0.02, 0.06, True), RUBBER)
    b.add(tube(0.017, 0.017, u - 0.133, u - 0.129, x - side * 0.02, 0.06, 16), GLASS)
    b.add(box(0.04, 0.003, 0.03, (x + side * 0.025, u - 0.131, 0.0), 0.001), ENERGY)
    for zz in (0.06, -0.02):
        b.add(sweep([(x + side * 0.064, u - 0.07, zz), (x + side * 0.1, u - 0.04, zz), (x + side * 0.1, u + 0.04, zz), (x + side * 0.064, u + 0.07, zz)], 0.011, 0.011, 10, 4), POLY)
    b.add(tube_x(0.016, 0.05, (x, u - 0.06, -0.06), 16), POLY)                                         # batterie CLU
    for k in range(4):
        b.add(tube_z(0.005, 0.104, 0.112, x + side * (0.03 - k * 0.016), u - 0.1, 10), RUBBER)
    grip = trigger_group(b, -0.04, -r)
    sup = front_grip(b, 0.24, -r - 0.004, r)
    butt = shoulder_pad(b, -0.25, -r - 0.002, 0.16, 0.036, 0.03, r)
    cable(b, [(x + 0.03, -0.1, -0.06), (-0.05, -0.12, -0.075), (-0.022, -0.06, -0.08)], 0.0045)
    sling(b, -0.47, 0.52, r + 0.004, 0.25, 0.026)
    label(b, -0.1, r, math.pi / 2 + 0.1, 0.12, 0.034, "MISSILE GUIDE", 0.008, True)
    wrap_text(b, "AVANT  >>>", 0.4, r, math.pi / 2 + 0.6, 0.012, MARKING)
    g.point("Muzzle", (0, 0.7, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.6, 0))


def goliath(g):
    """Goliath : énorme canon sans recul — venturi nervuré à lèvre épaisse, bloc de culasse à leviers, manchon de
    refroidissement ajouré, bouche à évents, grande conduite de tir, poignée de transport, bipied replié."""
    b = g.body
    r = 0.082
    rin = comp_tube(b, r, -0.6, 0.66, PAINT, (-0.45, -0.32, 0.38, 0.5), 0.16)
    venturi(b, -0.84, -0.62, r * 1.04, 1.55, METAL, 32, 8, ACCENT)
    # Bloc de culasse et ses deux leviers de verrouillage.
    b.add(lathe([(-0.64, r * 1.08), (-0.632, r * 1.24), (-0.56, r * 1.24), (-0.55, r * 1.06)], 32), METAL)
    b.add(rot_u(prism([(-0.63, r), (-0.56, r), (-0.565, r * 1.42), (-0.62, r * 1.42)], -0.016, 0.016, 0.003), -math.pi / 2), METAL)  # charnière
    b.add(tube_z(0.012, -0.03, 0.03, r * 1.42 - 0.012, -0.595, 16), METAL)
    for s in (-1, 1):
        b.add(sweep([(s * r * 1.2, -0.6, r * 0.55), (s * (r * 1.2 + 0.035), -0.63, r * 0.9), (s * (r * 1.0 + 0.04), -0.7, r * 1.25)], 0.008, 0.008, 8, 4), METAL)
        b.add(tube(0.013, 0.013, -0.73, -0.69, s * (r + 0.04), r * 1.25, 14), RUBBER)
    rivets(b, -0.596, r * 1.24, [math.pi / 2 + k * math.pi / 4 for k in range(8)], 0.004)
    # Manchon de refroidissement ajouré.
    sl = []
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        for bm in cut_slots(-0.2, 0.26, 4, 0, r + 0.012, 0.07, 0.018, 0.03, 'z'):
            sl.append(rot_u(bm, a - math.pi / 2))
    b.add(carve(lathe([(-0.24, r * 1.0), (-0.235, r * 1.14), (0.295, r * 1.14), (0.3, r * 1.0), (0.3, r * 0.98), (-0.24, r * 0.98)], 32, closed=True), sl), ACCENT)
    b.add(lathe([(-0.24, r * 1.05), (0.3, r * 1.05)], 40), METAL)
    for uu in (-0.26, 0.32):
        band(b, uu, r * 1.12, 0.03, METAL, 0.006, 40, 0, 0, 8)
    # Bouche à évents.
    mz = lathe([(0.62, rin), (0.62, r * 1.04), (0.63, r * 1.18), (0.78, r * 1.18), (0.785, r * 1.12), (0.785, rin)], 36, closed=True)
    mz = carve(mz, [rot_u(bm, a) for a in (0, math.pi) for bm in cut_slots(0.64, 0.77, 4, r * 1.15, 0, 0.018, 0.05, 0.06, 'x')])
    b.add(mz, METAL)
    b.add(shell(0.785, 0.8, r * 1.15, rin * 1.02, 36, 0, 0, 0.004), RUBBER)
    grip = trigger_group(b, -0.07, -r, 0.038)
    sup = front_grip(b, 0.44, -r - 0.004, r)
    butt = shoulder_pad(b, -0.36, -r - 0.002, 0.2, 0.04, 0.034, r)
    cp = fcu(b, 0.02, r, -1, 0.05, 0.19, 0.08, 0.1, PAINT)
    cable(b, [cp, (-0.06, -0.06, -0.1), (-0.025, -0.02, -0.105)], 0.005)
    carry_handle(b, -0.12, 0.12, r * 1.14, 0.06)
    bipod(b, 0.6, -r - 0.006, True)
    swivel(b, 0.5, r, -math.pi / 2 + 0.9)
    label(b, -0.42, r, math.pi / 2, 0.14, 0.05, "DANGER", 0.016, True)
    label(b, 0.46, r, math.pi - 0.2, 0.12, 0.03, "GOLIATH 106", 0.01)
    wrap_text(b, "ZONE ARRIERE 50 M", -0.38, r, math.pi / 2 + 0.9, 0.01, MARKING)
    laser_module(g.opt("laser"), 0.5, -0.03, r + 0.02)
    g.point("Muzzle", (0, 0.8, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.7, 0))


def cascade(g):
    """Cascade : caisson de 12 roquettes (4 × 3) aux angles adoucis, alvéoles à ogives visibles, opercules arrière, cadres
    boulonnés, panneau d'accès, poignée de transport, conduite de tir sur bras gauche reliée par câble."""
    b = g.body
    hw0, hh0 = 4 * 0.048 / 2 + 0.01, 3 * 0.048 / 2 + 0.01
    louvers = [c for s in (-1, 1) for c in cut_slots(0.13, 0.37, 7, s * hw0, -hh0 * 0.42, 0.012, 0.03, 0.014, 'x')]
    louvers += cut_slots(-0.3, -0.18, 4, 0, hh0, 0.01, hw0 * 1.1, 0.012, 'z')
    hw, hh = pod_box(b, 4, 3, 0.048, -0.38, 0.5, PAINT, 0.07, ACCENT, extra=louvers)
    for uu in (-0.33, 0.05, 0.44):
        pod_frame(b, uu, hw, hh, 0.026, METAL)
    for s in (-1, 1):
        b.add(box(0.004, 0.2, hh * 1.3, (s * (hw + 0.001), -0.14, 0.0), 0.002), ACCENT)                   # panneau d'accès
        for uu in (-0.23, -0.05):
            for zz in (-hh * 0.55, hh * 0.55):
                screw(b, uu, zz, s * (hw + 0.003), 0.0025, s)
    carry_handle(b, -0.12, 0.22, hh + 0.006, 0.06)
    grip = trigger_group(b, -0.06, -hh)
    sup = front_grip(b, 0.26, -hh - 0.004)
    butt = shoulder_pad(b, -0.24, -hh - 0.002, 0.16, 0.04, 0.03)
    cp = fcu(b, 0.04, hw, -1, 0.06, 0.13, 0.06, 0.075, METAL)
    cable(b, [cp, (-hw * 0.9, -0.06, -hh - 0.01), (-0.02, -0.04, -hh - 0.03)], 0.0045)
    flat_text(b, "12 x 70 MM", 0.25, hh * 0.15, hw, 0.016, MARKING, 1)
    flat_text(b, "ROQUETTES HE", -0.14, -hh * 0.45, hw + 0.002, 0.011, MARKING, 1)
    plate(b, 0.3, hh * 0.45, -hw, -1, 0.12, 0.03, "DANGER", 0.012)
    plate(b, 0.3, hh * 0.45, hw, 1, 0.12, 0.03, "DANGER", 0.012)
    g.point("Muzzle", (0, 0.5, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.38, 0))


def hornet(g):
    """Hornet : lanceur compact à quatre tubes courts (2 × 2) à petits venturis, trois cadres boulonnés, carter de culasse
    à connecteurs, rail supérieur à viseur point rouge, bouchons de bouche."""
    b = g.body
    r = 0.03
    d = 0.033
    pos = [(sx * d, sz * d) for sx in (-1, 1) for sz in (-1, 1)]
    for (x, z) in pos:
        b.add(lathe([(-0.24, r * 0.96), (-0.235, r), (0.4, r), (0.404, r * 0.98), (0.404, r * 0.8), (0.37, r * 0.8)], 18, x, z), PAINT)
        b.add(tube(r * 0.8, r * 0.8, 0.371, 0.374, x, z, 14), POLY)
        b.add(shell(0.39, 0.415, r * 1.08, r * 0.8, 18, x, z, 0.002), RUBBER)
        b.add(lathe([(-0.24, r * 1.02), (-0.27, r * 1.15), (-0.3, r * 1.28), (-0.3, r * 1.12), (-0.27, r * 0.85), (-0.24, r * 0.6)], 18, x, z, True), METAL)
    # Cadres et carter de culasse.
    for uu, w in ((-0.21, 0.03), (0.06, 0.024), (0.33, 0.024)):
        fr = loft([(uu - w / 2, d + r + 0.004, d + r + 0.004, 0, 3.2), (uu - w / 2 + 0.004, d + r + 0.009, d + r + 0.009, 0, 3.2),
                   (uu + w / 2 - 0.004, d + r + 0.009, d + r + 0.009, 0, 3.2), (uu + w / 2, d + r + 0.004, d + r + 0.004, 0, 3.2)], 32)
        b.add(fr, METAL)
        for s in (-1, 1):
            screw(b, uu, 0.0, s * (d + r + 0.009), 0.0032, s)
    b.add(prism([(-0.2, -0.01), (0.33, -0.01), (0.33, 0.01), (-0.2, 0.01)], -0.012, 0.012, 0.002), METAL)  # longeron central
    rail(b, -0.15, 0.26, d + r + 0.004, 0, 0.024)
    red_dot(b, 0.02, d + r + 0.014)
    b.add(prism([(0.28, d + r + 0.008), (0.3, d + r + 0.008), (0.296, d + r + 0.04), (0.288, d + r + 0.04)], -0.004, 0.004, 0.001), METAL)  # guidon
    b.add(box(0.03, 0.06, 0.04, (-(d + r + 0.012), -0.12, 0.0), 0.005), POLY)                              # boîtier de mise à feu
    for k in range(2):
        b.add(tube_x(0.007, 0.012, (-(d + r + 0.03), -0.135 + k * 0.03, 0.0), 12), METAL)
    cable(b, [(-(d + r + 0.034), -0.135, 0.0), (-(d + r + 0.03), -0.17, -0.05), (-0.02, -0.1, -(d + r + 0.03))], 0.004)
    grip = trigger_group(b, -0.05, -(d + r + 0.004))
    sup = front_grip(b, 0.2, -(d + r + 0.008))
    butt = shoulder_pad(b, -0.19, -(d + r + 0.006), 0.13, 0.034, 0.028)
    label(b, 0.2, r, math.pi / 2 + 0.6, 0.09, 0.022, "HORNET", 0.009, False, -d, d)
    flat_text(b, "4 x 66 MM", 0.12, -d, d + r + 0.0005, 0.011, MARKING, 1)
    laser_module(g.opt("laser"), 0.3, -0.04, d + r + 0.02)
    g.point("Muzzle", (0, 0.42, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.3, 0))


def volcano(g):
    """Volcano : six tubes en barillet tournant (plaques porte-tubes ajourées, axe central, couronne dentée), carter
    arrière à moteur d'entraînement, demi-capot ventilé, berceau fixe portant les poignées, poignée de transport."""
    b = g.body
    r = 0.026
    R = 0.058
    pos = [(math.cos(2 * math.pi * k / 6 + math.pi / 6) * R, math.sin(2 * math.pi * k / 6 + math.pi / 6) * R) for k in range(6)]
    for (x, z) in pos:
        b.add(lathe([(-0.2, r), (0.48, r), (0.482, r * 0.97), (0.482, r * 0.78), (0.44, r * 0.78)], 16, x, z), PAINT)
        b.add(tube(r * 0.78, r * 0.78, 0.441, 0.444, x, z, 12), POLY)
        b.add(shell(0.465, 0.49, r * 1.12, r * 0.8, 16, x, z, 0.002), METAL)
        b.add(shell(0.255, 0.27, r * 1.07, r * 0.98, 16, x, z, 0.002), METAL)
    # Plaques porte-tubes ajourées.
    for uu, t in ((0.4, 0.02), (0.12, 0.016)):
        pl = lathe([(uu - t / 2, 0.093), (uu + t / 2, 0.093)], 40)
        cuts = []
        for k in range(6):
            a = 2 * math.pi * k / 6
            cuts.append(tube(0.009, 0.009, uu - 0.02, uu + 0.02, math.cos(a) * 0.068, math.sin(a) * 0.068, 10))
        b.add(carve(pl, cuts), METAL)
    b.add(tube(0.015, 0.015, -0.2, 0.47, 0, 0, 16), METAL)                                                  # axe central
    b.add(lathe([(0.47, 0.022), (0.5, 0.022), (0.51, 0.012), (0.51, 0.0)], 6), METAL)                    # écrou
    # Carter arrière : tambour, couronne dentée, opercules de culasse.
    b.add(lathe([(-0.36, 0.08), (-0.354, 0.092), (-0.2, 0.092), (-0.195, 0.086)], 40), ACCENT)
    b.add(tube(0.075, 0.075, -0.364, -0.36, 0, 0, 32), POLY)
    for (x, z) in pos:
        b.add(tube(0.02, 0.02, -0.368, -0.362, x, z, 12), METAL)
    b.add(shell(-0.19, -0.165, 0.098, 0.08, 48, 0, 0, 0.002), METAL)
    for k in range(36):
        b.add(rot_u(box(0.008, 0.022, 0.006, (0.101, -0.178, 0), 0.001), 2 * math.pi * k / 36), METAL)
    # Moteur d'entraînement (droite) à ailettes.
    mx, mz = 0.092, -0.05
    b.add(lathe([(-0.33, 0.024), (-0.325, 0.03), (-0.17, 0.03), (-0.165, 0.022)], 18, mx, mz), METAL)
    for k in range(7):
        b.add(shell(-0.31 + k * 0.02, -0.304 + k * 0.02, 0.036, 0.029, 18, mx, mz, 0.001), METAL)
    b.add(tube(0.014, 0.014, -0.165, -0.155, mx, mz, 12), POLY)
    # Demi-capot ventilé entre carter et plaque médiane.
    cap = shell(-0.2, 0.108, 0.1, 0.094, 30, 0, 0, 0.003, (0.05, math.pi - 0.05))
    cuts = []
    for a in (0.6, 1.1, 2.04, 2.54):
        cuts += [rot_u(bm, a - math.pi / 2) for bm in cut_slots(-0.15, 0.08, 5, 0, 0.097, 0.03, 0.012, 0.03, 'z')]
    b.add(carve(cap, cuts), PAINT)
    carry_handle(b, -0.14, 0.06, 0.1, 0.055)
    # Berceau fixe sous le barillet.
    b.add(prism([(-0.34, -0.088), (0.12, -0.088), (0.14, -0.1), (0.14, -0.115), (-0.3, -0.115), (-0.34, -0.104)], -0.022, 0.022, 0.003), METAL)
    grip = trigger_group(b, -0.07, -0.114)
    sup = front_grip(b, 0.24, -0.1)
    b.add(prism([(0.12, -0.1), (0.3, -0.1), (0.3, -0.112), (0.12, -0.114)], -0.012, 0.012, 0.002), METAL)
    butt = shoulder_pad(b, -0.25, -0.114, 0.13, 0.036, 0.028)
    reflex_sight(b, -0.04, 0.094, -1, 0.07)
    label(b, -0.27, 0.092, math.pi / 2 + 0.5, 0.1, 0.03, "DANGER", 0.01, True)
    wrap_text(b, "VOLCANO", -0.28, 0.092, 0.35, 0.013, MARKING)
    wrap_text(b, "6 x 60 MM", -0.28, 0.092, math.pi - 0.15, 0.011, MARKING)
    g.point("Muzzle", (0, 0.49, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.36, 0))


def ibis(g):
    """Ibis (type Panzerfaust 3) : tube mince à contre-masse arrière, grosse charge militaire dépassant à l'avant avec sonde
    d'impact télescopique, appareil de visée optronique en surplomb à gauche, désignateur laser."""
    b = g.body
    r = 0.05
    comp_tube(b, r, -0.5, 0.31, PAINT, (-0.38, -0.25, 0.1, 0.22), None)
    b.add(lathe([(-0.56, r * 1.18), (-0.555, r * 1.24), (-0.5, r * 1.14), (-0.49, r * 1.0), (-0.49, r * 0.7), (-0.55, r * 0.82), (-0.56, r * 0.95)], 32, closed=True), METAL)
    b.add(tube(r * 0.72, r * 0.72, -0.512, -0.508, 0, 0, 20), POLY)
    for uu in (-0.47, 0.29):
        band(b, uu, r, 0.03, ACCENT, 0.004, 32, 0, 0, 6)
    # Charge militaire (110 mm) et sonde télescopique.
    b.add(lathe([(0.3, 0.046), (0.315, 0.058), (0.33, 0.061), (0.45, 0.061), (0.5, 0.056), (0.55, 0.043), (0.58, 0.028), (0.59, 0.016)], 32), ACCENT)
    b.add(shell(0.335, 0.345, 0.063, 0.058, 32, 0, 0, 0.001), METAL)
    b.add(shell(0.43, 0.44, 0.063, 0.058, 32, 0, 0, 0.001), METAL)
    b.add(lathe([(0.585, 0.013), (0.68, 0.013), (0.68, 0.011), (0.76, 0.011), (0.77, 0.0165), (0.785, 0.017), (0.792, 0.012), (0.795, 0.0)], 16), METAL)
    for uu in (0.68, 0.585):
        b.add(shell(uu - 0.004, uu + 0.004, 0.016, 0.011, 16, 0, 0, 0.001), METAL)
    # Appareil de visée (surplomb gauche, poignée de transport intégrée).
    side = -1
    x = side * 0.03
    body = loft([(-0.2, 0.034, 0.04, 0, 6), (-0.19, 0.038, 0.045, 0, 6), (0.04, 0.038, 0.045, 0, 6), (0.055, 0.034, 0.04, 0, 6)], 28)
    mv(body, x, 0, r + 0.065)
    body = carve(body, cut_slots(-0.16, 0.0, 5, x + side * 0.038, r + 0.065, 0.006, 0.03, 0.01, 'x'))
    b.add(body, PAINT)
    for uu in (-0.15, 0.0):
        b.add(prism([(uu - 0.025, r - 0.006), (uu + 0.025, r - 0.006), (uu + 0.018, r + 0.024), (uu - 0.018, r + 0.024)], x - 0.02, x + 0.02, 0.003), METAL)
        clamp(b, uu, r, 0.026, METAL, math.pi)
    for (xo, zo, R) in ((0.012, 0.012, 0.022), (-0.018, -0.02, 0.011)):
        b.add(shell(0.05, 0.085, R * 1.12, R * 0.9, 20, x + xo, r + 0.065 + zo, 0.002), METAL)
        b.add(tube(R * 0.92, R * 0.92, 0.056, 0.06, x + xo, r + 0.065 + zo, 20), LENS)
    b.add(lathe([(-0.24, 0.024), (-0.236, 0.026), (-0.2, 0.017), (-0.2, 0.012), (-0.236, 0.02)], 20, x + 0.005, r + 0.075, True), RUBBER)
    b.add(tube(0.013, 0.013, -0.203, -0.199, x + 0.005, r + 0.075, 16), GLASS)
    b.add(box(0.026, 0.003, 0.018, (x - 0.012, -0.201, r + 0.045), 0.001), ENERGY)
    b.add(tube_x(0.012, 0.02, (x + side * 0.045, -0.06, r + 0.065), 16), METAL)
    b.add(tube_x(0.014, 0.004, (x + side * 0.057, -0.06, r + 0.065), 16), RUBBER)
    b.add(box(0.018, 0.05, 0.024, (side * (r + 0.006), 0.18, -0.018), 0.003), POLY)                       # désignateur
    b.add(tube(0.006, 0.006, 0.205, 0.208, side * (r + 0.006), -0.012, 10), GLOW)
    b.add(tube(0.007, 0.007, 0.205, 0.208, side * (r + 0.006), -0.026, 10), LENS)
    grip = trigger_group(b, -0.06, -r)
    sup = front_grip(b, 0.16, -r - 0.004, r)
    butt = shoulder_pad(b, -0.27, -r - 0.002, 0.16, 0.034, 0.03, r)
    label(b, -0.33, r, math.pi + 0.3, 0.1, 0.026, "DANGER", 0.009, True)
    wrap_text(b, "110 MM  HEAT", 0.39, 0.061, math.pi - 0.2, 0.009, POLY)
    g.point("Muzzle", (0, 0.795, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.55, 0))


# ───────────────────────── Lance-missiles ─────────────────────────

def emerald(g):
    """Emerald : caisson arrondi de quatre missiles (2 × 2) à coiffes vitrées d'autodirecteur, unité de verrouillage à
    écran sur le dessus, antenne IFF en grille repliable à gauche, cadres, poignées et appui d'épaule."""
    b = g.body
    cell = 0.08
    hw, hh = cell + 0.01, cell + 0.01
    body = loft([(-0.42, hw - 0.008, hh - 0.008, 0, 3.6), (-0.41, hw, hh, 0, 3.6), (0.49, hw, hh, 0, 3.6), (0.5, hw - 0.008, hh - 0.008, 0, 3.6)], 40)
    cells = [(sx * cell / 2, sz * cell / 2) for sx in (-1, 1) for sz in (-1, 1)]
    cuts = [tube(cell * 0.43, cell * 0.43, 0.43, 0.52, x, z, 20) for (x, z) in cells]
    cuts += [tube(cell * 0.4, cell * 0.4, -0.44, -0.4, x, z, 20) for (x, z) in cells]
    b.add(carve(body, cuts), PAINT)
    for (x, z) in cells:
        b.add(tube(cell * 0.43, cell * 0.43, 0.429, 0.433, x, z, 18), POLY)
        b.add(lathe([(0.44, cell * 0.36), (0.465, cell * 0.33), (0.485, cell * 0.22), (0.494, 0.0)], 18, x, z), GLASS)
        b.add(shell(0.436, 0.448, cell * 0.38, cell * 0.3, 18, x, z, 0.001), METAL)
        b.add(shell(0.494, 0.502, cell * 0.46, cell * 0.43, 20, x, z, 0.001), METAL)
        b.add(tube(cell * 0.4, cell * 0.4, -0.395, -0.39, x, z, 18), POLY)
        b.add(tube(cell * 0.12, cell * 0.12, -0.4, -0.392, x, z, 10), METAL)
    for uu in (-0.36, 0.02, 0.42):
        pod_frame(b, uu, hw, hh, 0.028, METAL, 3.6, False)
    for s in (-1, 1):
        for uu in (-0.2, 0.2):
            b.add(box(0.004, 0.1, hh * 0.9, (s * (hw + 0.001), uu, 0), 0.002), ACCENT)
            for zz in (-hh * 0.35, hh * 0.35):
                screw(b, uu - 0.04, zz, s * (hw + 0.003), 0.0025, s)
                screw(b, uu + 0.04, zz, s * (hw + 0.003), 0.0025, s)
    cp = seeker(b, 0.02, hh + 0.004, 0.08, 0.15, 0.062)
    # Antenne IFF en grille (gauche).
    ax = -(hw + 0.02)
    b.add(box(0.02, 0.04, 0.02, (-(hw + 0.008), 0.12, hh * 0.4), 0.003), METAL)
    b.add(tube_x(0.008, 0.03, (ax, 0.12, hh * 0.4), 12), METAL)
    for zz in (hh * 0.4 + 0.005, hh * 0.4 + 0.11):
        b.add(box(0.006, 0.17, 0.006, (ax - 0.012, 0.12, zz), 0.0015), METAL)
    for k in range(6):
        b.add(box(0.004, 0.004, 0.105, (ax - 0.012, 0.04 + k * 0.032, hh * 0.4 + 0.0575), 0.001), METAL)
    b.add(box(0.003, 0.17, 0.003, (ax - 0.012, 0.12, hh * 0.4 + 0.0575), 0.0008), METAL)
    cable(b, [cp, (-hw - 0.01, -0.03, hh * 0.2), (-hw * 0.6, -0.06, -hh - 0.02), (-0.02, -0.04, -hh - 0.03)], 0.0045)
    grip = trigger_group(b, -0.07, -hh)
    sup = front_grip(b, 0.25, -hh - 0.004)
    butt = shoulder_pad(b, -0.25, -hh - 0.002, 0.16, 0.04, 0.03)
    flat_text(b, "MISSILE SOL-AIR", -0.2, -hh * 0.6, hw, 0.012, MARKING, 1)
    flat_text(b, "MISSILE SOL-AIR", -0.2, -hh * 0.6, -hw, 0.012, MARKING, -1)
    plate(b, 0.3, -hh * 0.62, hw, 1, 0.12, 0.03, "DANGER", 0.012)
    plate(b, 0.3, -hh * 0.62, -hw, -1, 0.12, 0.03, "DANGER", 0.012)
    g.point("Muzzle", (0, 0.5, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.42, 0))


def tortoise(g):
    """Air Tortoise : tube court et très gros portant une « carapace » de plaques de blindage cintrées rivetées, coiffe
    bombée du missile dépassant à l'avant, cerclages boulonnés, pavillon arrière, deux poignées de transport latérales,
    unité de verrouillage."""
    b = g.body
    r = 0.095
    comp_tube(b, r, -0.42, 0.44, PAINT, (-0.3, 0.3), None)
    # Coiffe bombée.
    nose = [(0.43, 0.088)] + [(0.43 + 0.17 * math.sin(t * math.pi / 2), 0.088 * math.cos(t * math.pi / 2)) for t in (0.2, 0.4, 0.6, 0.8, 1.0)]
    nose[-1] = (0.6, 0.0)
    b.add(lathe(nose, 32), ACCENT)
    b.add(shell(0.43, 0.46, r * 1.08, 0.084, 36, 0, 0, 0.003), METAL)
    b.add(lathe([(0.52, 0.006), (0.52, 0.015), (0.6, 0.004)], 8, 0, 0), METAL)
    # Pavillon arrière.
    venturi(b, -0.54, -0.42, r * 1.0, 1.18, METAL, 36)
    # Carapace : plaques cintrées en quinconce.
    for row, (u0, u1) in enumerate(((-0.36, -0.2), (-0.18, -0.02), (0.0, 0.16), (0.18, 0.34))):
        off = 0.0 if row % 2 == 0 else 0.22
        for k in range(4):
            a0 = 0.2 + off + k * 0.62
            a1 = a0 + 0.56
            if a1 > math.pi - 0.05:
                continue
            b.add(shell(u0, u1, r + 0.009, r - 0.001, 8, 0, 0, 0.004, (a0, a1)), ACCENT)
            am = (a0 + a1) / 2
            rivets(b, u0 + 0.014, r + 0.009, [a0 + 0.08, a1 - 0.08], 0.0032)
            rivets(b, u1 - 0.014, r + 0.009, [a0 + 0.08, a1 - 0.08], 0.0032)
    for uu in (-0.39, 0.39):
        band(b, uu, r, 0.03, METAL, 0.007, 40, 0, 0, 10)
    for s in (-1, 1):
        b.add(sweep([(s * r * 0.9, -0.14, -r * 0.4), (s * (r + 0.05), -0.1, -r * 0.2), (s * (r + 0.05), 0.08, -r * 0.2), (s * r * 0.9, 0.12, -r * 0.4)], 0.009, 0.009, 10, 4), METAL)
        b.add(sweep([(s * (r + 0.05), -0.07, -r * 0.2), (s * (r + 0.05), 0.0, -r * 0.2), (s * (r + 0.05), 0.05, -r * 0.2)], 0.012, 0.012, 10, 3), RUBBER)
    cp = seeker(b, 0.27, r + 0.01, 0.075, 0.13, 0.055, METAL)
    grip = trigger_group(b, -0.06, -r, 0.036)
    sup = front_grip(b, 0.26, -r - 0.004, r)
    butt = shoulder_pad(b, -0.26, -r - 0.002, 0.16, 0.04, 0.032, r)
    cable(b, [cp, (-0.04, 0.2, r * 0.9), (-r * 0.75, 0.0, r * 0.62), (-r * 0.98, -0.06, -r * 0.3), (-0.02, -0.03, -r - 0.025)], 0.005)
    label(b, 0.2, r, math.pi + 0.35, 0.12, 0.034, "AIR TORTOISE", 0.009, False)
    label(b, -0.16, r, -0.3, 0.12, 0.03, "DANGER", 0.01, True)
    g.point("Muzzle", (0, 0.6, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.5, 0))


def mlra(g):
    """MLRA : nacelle cylindrique de 19 mini-missiles (couronnes de 1 + 6 + 12 alvéoles), carénage avant, cône arrière à
    opercules, deux sangles de berceau sur une poutre porteuse qui reçoit poignées, crosse squelette et unité de verrouillage."""
    b = g.body
    R = 0.1
    cells = [(0.0, 0.0)] + [(math.cos(k * math.pi / 3) * 0.037, math.sin(k * math.pi / 3) * 0.037) for k in range(6)] \
        + [(math.cos(k * math.pi / 6 + math.pi / 12) * 0.073, math.sin(k * math.pi / 6 + math.pi / 12) * 0.073) for k in range(12)]
    cr = 0.0145
    pod = lathe([(-0.22, R * 0.97), (-0.214, R), (0.4, R), (0.406, R * 0.97)], 40)
    pod = carve(pod, [tube(cr, cr, 0.34, 0.43, x, z, 12) for (x, z) in cells])
    b.add(pod, PAINT)
    for (x, z) in cells:
        b.add(tube(cr, cr, 0.339, 0.342, x, z, 10), POLY)
        rocket_tip(b, 0.355, cr * 0.82, 0.03, x, z, ACCENT, 10)
    b.add(shell(0.37, 0.41, R * 1.05, R * 0.97, 40, 0, 0, 0.004), ACCENT)                                # carénage
    b.add(lathe([(-0.22, R * 0.98), (-0.3, R * 0.86), (-0.36, R * 0.62), (-0.37, R * 0.58)], 40), METAL)  # cône arrière
    rc = carve(tube(R * 0.6, R * 0.6, -0.385, -0.36, 0, 0, 32), [tube(0.016, 0.016, -0.4, -0.37, math.cos(a) * 0.035, math.sin(a) * 0.035, 10)
                                                               for a in (k * math.pi / 3 for k in range(6))])
    b.add(rc, POLY)
    rivets(b, -0.26, R * 0.92, [k * math.pi / 4 for k in range(8)], 0.0035)
    for uu in (-0.12, 0.26):
        band(b, uu, R, 0.034, METAL, 0.004, 40)
        b.add(prism([(uu - 0.02, -R - 0.002), (uu + 0.02, -R - 0.002), (uu + 0.014, -R - 0.028), (uu - 0.014, -R - 0.028)], -0.02, 0.02, 0.002), METAL)
        b.add(tube_x(0.005, 0.048, (0, uu, -R - 0.016), 10), METAL)
    for k in range(4):
        b.add(rot_u(box(0.004, 0.5, 0.008, (R + 0.001, 0.08, 0), 0.0015), math.pi / 4 + k * math.pi / 2), METAL)  # glissières
    # Poutre porteuse, crosse squelette.
    zt = -R - 0.028
    b.add(prism([(-0.5, zt + 0.004), (0.32, zt + 0.004), (0.34, zt - 0.006), (0.32, zt - 0.02), (-0.5, zt - 0.02)], -0.016, 0.016, 0.003), METAL)
    b.add(carve(prism([(-0.62, zt - 0.02), (-0.5, zt + 0.004), (-0.42, zt + 0.004), (-0.44, zt - 0.03), (-0.6, zt - 0.075), (-0.63, zt - 0.075)], -0.014, 0.014, 0.003),
                [box(0.06, 0.07, 0.016, (0, -0.52, zt - 0.025), 0.006, 3)]), POLY)
    b.add(box(0.032, 0.014, 0.07, (0, -0.632, zt - 0.045), 0.005), RUBBER)
    grip = trigger_group(b, -0.07, zt - 0.02)
    sup = front_grip(b, 0.22, zt - 0.02)
    butt = shoulder_pad(b, -0.3, zt - 0.02, 0.15, 0.034, 0.028)
    cp = seeker(b, 0.0, R + 0.005, 0.08, 0.15, 0.058)
    cable(b, [cp, (-R * 0.9, -0.05, R * 0.3), (-R * 0.95, -0.08, -R * 0.4), (-0.02, -0.08, zt - 0.04)], 0.0045)
    label(b, 0.18, R, math.pi + 0.25, 0.12, 0.034, "19 x 40 MM", 0.011, False)
    label(b, -0.04, R, 0.3, 0.1, 0.03, "DANGER", 0.01, True)
    g.point("Muzzle", (0, 0.42, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.36, 0))


def fork(g):
    """FORK : tube à bouche en corolle de six pétales (missile qui se divise), grappe de têtes de sous-missiles visible,
    venturi arrière à grille, ailettes de stabilisation du tube, unité de verrouillage."""
    b = g.body
    r = 0.058
    comp_tube(b, r, -0.45, 0.4, PAINT, (-0.3, -0.15, 0.12, 0.26), None)
    venturi(b, -0.58, -0.45, r, 1.3, METAL)
    for k in range(3):
        b.add(rot_u(box(0.004, 0.006, r * 2.3, (0, -0.55, 0), 0.001, 1), k * math.pi / 3), METAL)           # grille
    # Corolle : cloche évasée fendue en six pétales, bague de base boulonnée, nervures.
    b.add(lathe([(0.39, r * 1.0), (0.4, r * 1.12), (0.44, r * 1.12), (0.45, r * 1.0), (0.45, r * 0.85), (0.39, r * 0.85)], 36, closed=True), METAL)
    rivets(b, 0.415, r * 1.12, [k * math.pi / 3 + math.pi / 6 for k in range(6)], 0.0035)
    outer = [(0.45, r * 1.04)] + [(0.45 + 0.13 * t, r * (1.04 + 0.62 * t ** 1.8)) for t in (0.25, 0.5, 0.75, 1.0)]
    inner = [(0.45 + 0.13 * t, r * (0.94 + 0.62 * t ** 1.8)) for t in (1.0, 0.75, 0.5, 0.25)] + [(0.45, r * 0.9)]
    bell = lathe(outer + [(0.585, r * 1.62)] + inner, 36, closed=True)
    cuts = []
    for k in range(6):
        sl = prism([(0.475, -0.0025), (0.6, -0.013), (0.6, 0.013), (0.475, 0.0025)], r * 0.6, r * 2.0, 0.0)
        cuts.append(rot_u(sl, k * math.pi / 3))
    b.add(carve(bell, cuts), ACCENT)
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        rib = prism([(0.45, r * 1.03), (0.52, r * 1.13), (0.575, r * 1.5), (0.575, r * 1.6), (0.45, r * 1.1)], -0.003, 0.003, 0.0008)
        b.add(rot_u(rib, a - math.pi / 2), METAL)
    b.add(tube(r * 0.85, r * 0.85, 0.37, 0.374, 0, 0, 24), POLY)
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        rocket_tip(b, 0.38, 0.014, 0.06, math.cos(a) * 0.03, math.sin(a) * 0.03, METAL, 10)
    rocket_tip(b, 0.38, 0.016, 0.08, 0, 0, GLOW, 12)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(rot_u(prism([(-0.42, r), (-0.3, r), (-0.34, r + 0.032), (-0.4, r + 0.036)], -0.0025, 0.0025, 0.001), a - math.pi / 2), METAL)
    band(b, 0.33, r, 0.028, METAL, 0.004, 36, 0, 0, 6)
    band(b, -0.43, r, 0.028, METAL, 0.004, 36)
    cp = seeker(b, -0.06, r + 0.004, 0.075, 0.13, 0.055)
    grip = trigger_group(b, -0.06, -r)
    sup = front_grip(b, 0.22, -r - 0.004, r)
    butt = shoulder_pad(b, -0.24, -r - 0.002, 0.15, 0.034, 0.03, r)
    cable(b, [cp, (-r * 0.95, -0.1, r * 0.2), (-r * 0.9, -0.08, -r * 0.6), (-0.02, -0.05, -r - 0.025)], 0.0045)
    label(b, 0.05, r, math.pi + 0.2, 0.12, 0.03, "FORK", 0.012, False)
    label(b, -0.36, r, math.pi / 2 + 0.7, 0.09, 0.026, "DANGER", 0.009, True)
    g.point("Muzzle", (0, 0.59, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.55, 0))


def prominence(g):
    """Prominence : très long lanceur de missile balistique lourd — tube à nombreux cerclages, ogive du missile en tête,
    déflecteur de souffle arrière ajouré, conduite de tir latérale et unité de verrouillage, deux poignées de transport,
    bipied avant replié, rails latéraux."""
    b = g.body
    r = 0.074
    comp_tube(b, r, -0.82, 0.84, PAINT, (-0.7, -0.55, -0.4, -0.1, 0.05, 0.2, 0.45, 0.6), None)
    og = [(0.83, 0.068)] + [(0.83 + 0.19 * t, 0.068 * (1 - t ** 1.6) + 0.002) for t in (0.2, 0.4, 0.6, 0.8)] + [(1.02, 0.0)]
    b.add(lathe(og, 32), ACCENT)
    b.add(shell(0.82, 0.86, r * 1.07, 0.064, 36, 0, 0, 0.003), METAL)
    b.add(shell(0.9, 0.906, 0.058, 0.05, 32, 0, 0, 0.0), METAL)
    # Déflecteur arrière ajouré.
    df = lathe([(-0.82, r * 1.02), (-0.86, r * 1.1), (-0.96, r * 1.32), (-0.97, r * 1.32), (-0.97, r * 1.2), (-0.86, r * 0.9), (-0.82, r * 0.8)], 36, closed=True)
    df = carve(df, [rot_u(bm, a) for a in (k * math.pi / 4 for k in range(8)) for bm in cut_slots(-0.95, -0.87, 2, r * 1.15, 0, 0.022, 0.03, 0.06, 'x')])
    b.add(df, METAL)
    b.add(tube(r * 0.8, r * 0.8, -0.84, -0.836, 0, 0, 24), POLY)
    for uu in (-0.8, -0.25, 0.3, 0.78):
        band(b, uu, r, 0.032, METAL, 0.005, 36, 0, 0, 8)
    for s in (-1, 1):
        rail(b, 0.14, 0.26, 0.0, s * (r + 0.002), 0.022, METAL, True)
    cp = seeker(b, 0.36, r + 0.004, 0.08, 0.15, 0.06)
    fp = fcu(b, 0.0, r, -1, 0.045, 0.17, 0.07, 0.09, PAINT)
    carry_handle(b, -0.56, -0.36, r + 0.004, 0.055)
    carry_handle(b, 0.55, 0.72, r + 0.004, 0.05)
    bipod(b, 0.66, -r - 0.006, True)
    grip = trigger_group(b, -0.08, -r, 0.036)
    sup = front_grip(b, 0.3, -r - 0.004, r)
    butt = shoulder_pad(b, -0.33, -r - 0.002, 0.2, 0.038, 0.032, r)
    cable(b, [fp, (-0.06, -0.1, -r - 0.02), (-0.022, -0.05, -r - 0.03)], 0.005)
    cable(b, [cp, (-0.045, 0.2, r + 0.03), (-r * 0.9, 0.08, r * 0.4)], 0.004)
    swivel(b, 0.62, r, -math.pi / 2 - 0.9)
    swivel(b, -0.7, r, -math.pi / 2 - 0.9)
    sling(b, -0.68, 0.6, r + 0.006, -0.35, 0.026)
    label(b, -0.66, r, math.pi / 2, 0.14, 0.05, "DANGER", 0.016, True)
    label(b, 0.55, r, math.pi + 0.15, 0.16, 0.034, "PROMINENCE", 0.011, False)
    wrap_text(b, "MISSILE BALISTIQUE  -  NE PAS SE TENIR DERRIERE", -0.4, r, math.pi + 0.55, 0.009, MARKING)
    laser_module(g.opt("laser"), 0.55, -0.03, r + 0.02)
    g.point("Muzzle", (0, 1.02, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.9, 0))


def falcon(g):
    """MS Falcon (contenu téléchargeable) : deux tubes jumelés dans un carénage profilé futuriste à arêtes, bouches à
    anneaux lumineux, bandes d'énergie encastrées, ailerons canard, œil d'autodirecteur, tuyères arrière."""
    b = g.body
    r = 0.04
    sx = 0.048
    shell_ = loft([(-0.44, 0.07, 0.045, 0, 3.0), (-0.4, 0.1, 0.06, 0, 3.0), (0.25, 0.105, 0.062, 0, 3.0), (0.42, 0.098, 0.056, 0, 3.0),
                   (0.47, 0.094, 0.052, 0, 3.0)], 40)
    cuts = [tube(r * 0.95, r * 0.95, 0.38, 0.5, s * sx, 0, 22) for s in (-1, 1)]
    cuts += [tube(r * 0.75, r * 0.75, -0.46, -0.41, s * sx, 0, 18) for s in (-1, 1)]
    for s in (-1, 1):
        cuts.append(box(0.012, 0.5, 0.008, (s * 0.103, 0.0, 0.018), 0.003, 2))
        cuts.append(box(0.034, 0.2, 0.032, (s * 0.105, -0.03, -0.018), 0.006, 2))                       # fenêtre de cellule
        cuts += cut_slots(-0.37, -0.25, 5, s * 0.1, -0.02, 0.008, 0.03, 0.02, 'x')                          # évents arrière
    b.add(carve(shell_, cuts), PAINT)
    for uu in (-0.31, 0.2):
        b.add(loft([(uu - 0.012, 0.104, 0.0615, 0, 3.0), (uu - 0.008, 0.108, 0.065, 0, 3.0), (uu + 0.008, 0.108, 0.065, 0, 3.0),
                    (uu + 0.012, 0.104, 0.0615, 0, 3.0)], 40), ACCENT)                                    # cerclages
    for s in (-1, 1):
        energy_cell(b, -0.03, -0.018, s * 0.094, 0.17, 0.012)
        for uu in (-0.11, -0.03, 0.05):
            b.add(box(0.004, 0.006, 0.034, (s * 0.098, uu, -0.018), 0.001), METAL)
    b.add(loft([(0.05, 0.02, 0.012, -0.064, 3.0), (0.06, 0.024, 0.016, -0.066, 3.0), (0.16, 0.024, 0.016, -0.066, 3.0), (0.18, 0.018, 0.012, -0.064, 3.0)], 20), METAL)
    b.add(tube(0.008, 0.008, 0.178, 0.184, 0, -0.068, 12), LENS)                                         # capteur ventral
    for s in (-1, 1):
        b.add(box(0.004, 0.49, 0.005, (s * 0.1, 0.0, 0.018), 0.0015), ENERGY)                             # bandes d'énergie
        b.add(tube(r * 0.95, r * 0.95, 0.379, 0.382, s * sx, 0, 20), POLY)
        rocket_tip(b, 0.395, r * 0.7, 0.05, s * sx, 0, ACCENT, 16)
        b.add(shell(0.462, 0.474, r * 1.06, r * 0.95, 24, s * sx, 0, 0.002), ENERGY)                     # anneaux lumineux
        b.add(lathe([(-0.41, r * 0.75), (-0.47, r * 0.86), (-0.5, r * 0.95), (-0.5, r * 0.82), (-0.47, r * 0.7), (-0.41, r * 0.6)], 20, s * sx, 0, True), METAL)
        b.add(tube(r * 0.6, r * 0.6, -0.43, -0.426, s * sx, 0, 16), GLOW)
        b.add(rot_u(prism([(0.17, -0.003), (0.29, -0.003), (0.33, 0.003), (0.21, 0.003)], 0.09, 0.15, 0.001), 0.0 if s > 0 else math.pi), ACCENT)
        b.add(rot_u(prism([(-0.4, 0.05), (-0.22, 0.05), (-0.27, 0.088), (-0.37, 0.098)], -0.003, 0.003, 0.001), -s * 0.45), ACCENT)
    # Arête dorsale, œil de l'autodirecteur, poignée.
    b.add(prism([(-0.36, 0.055), (0.32, 0.058), (0.36, 0.062), (0.2, 0.075), (-0.2, 0.075), (-0.34, 0.068)], -0.012, 0.012, 0.003), ACCENT)
    b.add(lathe([(0.22, 0.03), (0.27, 0.03), (0.285, 0.022), (0.292, 0.0)], 20, 0, 0.068), METAL)
    b.add(lathe([(0.27, 0.024), (0.28, 0.021), (0.29, 0.012), (0.294, 0.0)], 18, 0, 0.068), GLASS)
    b.add(tube(0.008, 0.008, 0.286, 0.292, 0, 0.068, 10), GLOW)
    rail(b, -0.18, 0.12, 0.075, 0, 0.022)
    b.add(box(0.05, 0.08, 0.045, (0, -0.03, 0.11), 0.01), METAL)
    b.add(box(0.042, 0.003, 0.032, (0, -0.071, 0.11), 0.001), ENERGY)
    b.add(lathe([(0.01, 0.014), (0.03, 0.016), (0.03, 0.0)], 14, 0, 0.11), LENS)
    grip = trigger_group(b, -0.06, -0.058)
    sup = front_grip(b, 0.24, -0.06)
    butt = shoulder_pad(b, -0.24, -0.058, 0.15, 0.036, 0.028)
    b.add(prism([(-0.3, -0.045), (0.3, -0.045), (0.28, -0.064), (-0.28, -0.064)], -0.03, 0.03, 0.003), METAL)
    flat_text(b, "MS FALCON", -0.2, -0.03, 0.104, 0.014, MARKING, 1)
    g.point("Muzzle", (0, 0.48, 0)); g.point("Grip", grip); g.point("SupportV", sup)
    g.point("Butt", butt); g.point("Mag", (0, -0.42, 0))


PALETTE = {
    "grant": ((0.16, 0.18, 0.13), (0.05, 0.05, 0.05)), "leopard": ((0.32, 0.28, 0.2), (0.12, 0.12, 0.11)),
    "goliath": ((0.12, 0.14, 0.11), (0.25, 0.22, 0.15)), "cascade": ((0.15, 0.17, 0.13), (0.05, 0.05, 0.05)),
    "hornet": ((0.2, 0.2, 0.18), (0.45, 0.32, 0.08)), "volcano": ((0.14, 0.15, 0.16), (0.05, 0.05, 0.05)),
    "ibis": ((0.1, 0.1, 0.1), (0.55, 0.57, 0.6)), "emerald": ((0.08, 0.2, 0.12), (0.5, 0.52, 0.5)),
    "tortoise": ((0.18, 0.2, 0.15), (0.6, 0.55, 0.4)), "mlra": ((0.14, 0.15, 0.13), (0.3, 0.27, 0.2)),
    "fork": ((0.2, 0.18, 0.15), (0.55, 0.2, 0.1)), "prominence": ((0.13, 0.14, 0.12), (0.7, 0.3, 0.08)),
    "falcon": ((0.7, 0.72, 0.75), (0.15, 0.17, 0.2)),
}

LABELS = {
    "grant": "GRANT", "leopard": "LEOPARD GRL", "goliath": "GOLIATH", "cascade": "CASCADE", "hornet": "HORNET",
    "volcano": "VOLCANO", "ibis": "IBIS", "emerald": "EMERALD", "tortoise": "AIR TORTOISE", "mlra": "MLRA",
    "fork": "FORK", "prominence": "PROMINENCE", "falcon": "MS FALCON",
}

# Marquages automatiques (nom + n° de série) : sur le flanc du tube, en avant de la poignée.
MARK_AT = {
    "grant": (0.0, 0.0), "leopard": (0.32, 0.0), "goliath": (0.1, -0.03), "cascade": (-0.2, 0.0), "hornet": None,
    "volcano": None, "ibis": (-0.12, 0.0), "emerald": (-0.12, 0.0), "tortoise": None, "mlra": (0.04, 0.02),
    "fork": (-0.12, 0.0), "prominence": (-0.15, 0.0), "falcon": (0.15, -0.005),
}

FAMILIES = {f: globals()[f] for f in PALETTE}


def make(family, out=None):
    clear()
    paint, accent = PALETTE.get(family, (None, None))
    mats = materials(paint, accent)
    g = Gun(family)
    FAMILIES[family](g)
    g.build(mats)
    if out:
        export(out)
    return tris()
