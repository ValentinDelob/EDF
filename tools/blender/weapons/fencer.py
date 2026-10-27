# Armes du Fencer (EDF 5), une fonction par famille de modèle. Matériel lourd d'exosquelette, façon engin de chantier
# militaire : blindages à pans coupés superposés, vérins hydrauliques, flexibles annelés, charnières, radiateurs à ailettes,
# bandes d'alerte jaune et noir, vis et rivets. Les armes tenues passent sur l'avant-bras : un fourreau (Accent, gaine
# sombre) à lames de blindage articulées (Paint, acier bleuté), joint caoutchouc à l'arrière, collerette boulonnée à
# l'avant, vérins latéraux et poignée intérieure (repère Grip).
# Mêlée (prise à une main) : marteau vibrant à tête large et radiateurs (Vibro Hammer), marteau à barillet de charges
# (Vulcan Hammer), lame d'énergie forgée (Force / Dynamo Blade), hache à fer en croissant (Force Ax), rouleau à dents sous
# capot (Vibro Roller), pince à deux mâchoires (Haken). Perforation : lance télescopique à charge (Blasthole), lances
# jumelles, Flashing Spear à bobines d'accélération, marteau-piqueur à foret hélicoïdal, Spine Driver (pieu sur rail en U).
# Boucliers (bras gauche, origine à la poignée, face vers +u) : Deflection à trois pans, Ion Mirror hexagonal à miroir à
# facettes, Tower très haut à écailles et fente de visée, Reflector rond bombé. Canons automatiques (à la hanche) : gatling
# FG à caisse de munitions et goulotte, FGS à canons évasés, Disruptor à manchon ajouré et tambour, Galleon à long canon et
# frein de bouche, Dexter à tambour et pare-chaleur, Flame Revolver à réservoirs. Artillerie : canon à main NC à culasse,
# Gallic lourd à manchon ajouré, Powerdyne à rails, Cannon Shot à bouche évasée, mortiers léger / lourd / à dispersion
# (quatre tubes), Crusher géant. Lance-missiles (sur l'épaule, pylône sur le fourreau) : Arm Hound 2×2, Blood Storm 5×4,
# Haytal à façade en gradins, Leviathan géant, Arcane à six tubes rotatifs, FG High Altitude Impact (lancement vertical,
# trappes ouvertes), Phoenix sur rail.
import math


# ───────────────────────── Outils de forme (propres au module) ─────────────────────────

def ball(r, c, seg=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def _map(axis, a, c, s):
    """Point d'un profil 2D (a, c) à l'abscisse s de l'axe d'extrusion : 'u' profil (x, z), 'x' profil (u, z), 'z' (x, u)."""
    if axis == 'u':
        return P(a, s, c)
    if axis == 'x':
        return P(s, a, c)
    return P(a, c, s)


def ploft(secs, axis='u', caps=True):
    """Loft polygonal (arêtes vives, pans coupés) : secs = [(abscisse, [(a, c), …])], même nombre de points par section."""
    bm = bmesh.new()
    loops = [[bm.verts.new(_map(axis, a, c, s)) for (a, c) in pts] for (s, pts) in secs]
    n = len(loops[0])
    for A, B in zip(loops, loops[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((A[i], A[j], B[j], B[i]))
    if caps:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def hollow(outer, inner, axis='u'):
    """Coque creuse : deux lofts (extérieur, intérieur) reliés aux deux bouts."""
    bm = bmesh.new()
    O = [[bm.verts.new(_map(axis, a, c, s)) for (a, c) in pts] for (s, pts) in outer]
    I = [[bm.verts.new(_map(axis, a, c, s)) for (a, c) in pts] for (s, pts) in inner]
    n = len(O[0])

    def strip(A, B):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((A[i], A[j], B[j], B[i]))
    for A, B in zip(O, O[1:]):
        strip(A, B)
    for A, B in zip(I, I[1:]):
        strip(B, A)
    strip(I[0], O[0])
    strip(O[-1], I[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def octo(hw, hh, ch, cz=0.0, cx=0.0, chb=None):
    """Octogone à pans coupés (demi-largeur, demi-hauteur, chanfrein ; chb : chanfrein du bas)."""
    ch = max(ch, 0.002)
    cb = ch if chb is None else max(chb, 0.002)
    return [(cx + hw, cz - hh + cb), (cx + hw, cz + hh - ch), (cx + hw - ch, cz + hh), (cx - hw + ch, cz + hh),
            (cx - hw, cz + hh - ch), (cx - hw, cz - hh + cb), (cx - hw + cb, cz - hh), (cx + hw - cb, cz - hh)]


def ngon(r, n, cz=0.0, cx=0.0, rot=0.0, rz=None):
    rz = r if rz is None else rz
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cz + rz * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def _sec(s):
    return octo(s[1], s[2], s[4], s[3], s[5] if len(s) > 5 else 0.0)


def ol(secs, axis='u', caps=True):
    """Loft octogonal : secs = [(abscisse, demi-largeur, demi-hauteur, centre, chanfrein[, centre latéral])]."""
    return ploft([(s[0], _sec(s)) for s in secs], axis, caps)


def ohollow(secs, t, axis='u'):
    inner = [(s[0], s[1] - t, s[2] - t, s[3], max(s[4] - t * 0.4, 0.002)) + tuple(s[5:]) for s in secs]
    return hollow([(s[0], _sec(s)) for s in secs], [(s[0], _sec(s)) for s in inner], axis)


def nl(secs, n, axis='u', rot=0.0):
    """Loft à n pans : secs = [(abscisse, rayon, centre[, centre latéral])]."""
    return ploft([(s[0], ngon(s[1], n, s[2], s[3] if len(s) > 3 else 0.0, rot)) for s in secs], axis)


def nhollow(secs, n, t, axis='u', rot=0.0):
    """Tube à n pans creux : secs = [(abscisse, rayon extérieur, centre[, centre latéral])], paroi t."""
    o = [(s[0], ngon(s[1], n, s[2], s[3] if len(s) > 3 else 0.0, rot)) for s in secs]
    i = [(s[0], ngon(s[1] - t, n, s[2], s[3] if len(s) > 3 else 0.0, rot)) for s in secs]
    return hollow(o, i, axis)


def sweep2(outer, inner, s0, s1, axis='u', sc0=1.0, sc1=1.0, c=(0.0, 0.0)):
    """Bande épaisse : deux polylignes ouvertes (extérieure, intérieure) extrudées de s0 à s1 (échelle sc0 → sc1 autour de c)."""
    bm = bmesh.new()

    def lp(pts, s, k):
        return [bm.verts.new(_map(axis, c[0] + (a - c[0]) * k, c[1] + (q - c[1]) * k, s)) for (a, q) in pts]
    o0, o1, i0, i1 = lp(outer, s0, sc0), lp(outer, s1, sc1), lp(inner, s0, sc0), lp(inner, s1, sc1)
    n = len(outer)
    for i in range(n - 1):
        bm.faces.new((o0[i], o0[i + 1], o1[i + 1], o1[i]))
        bm.faces.new((i0[i], i1[i], i1[i + 1], i0[i + 1]))
        bm.faces.new((o0[i], i0[i], i0[i + 1], o0[i + 1]))
        bm.faces.new((o1[i], o1[i + 1], i1[i + 1], i1[i]))
    for i in (0, n - 1):
        bm.faces.new((o0[i], o1[i], i1[i], i0[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def band(center, widths, x0, x1, axis='x'):
    """Pièce pleine le long d'une ligne centrale 2D (profil latéral u, z), largeur variable, extrudée de x0 à x1."""
    n = len(center)
    L, R = [], []
    for i in range(n):
        a, c = center[max(i - 1, 0)], center[min(i + 1, n - 1)]
        du, dz = c[0] - a[0], c[1] - a[1]
        ln = math.hypot(du, dz) or 1.0
        nu, nz = -dz / ln, du / ln
        w = max(widths[i], 0.003) / 2
        L.append((center[i][0] + nu * w, center[i][1] + nz * w))
        R.append((center[i][0] - nu * w, center[i][1] - nz * w))
    return sweep2(L, R, x0, x1, axis)


def arc(cu, cz, r, a0, a1, n):
    return [(cu + r * math.cos(math.radians(a0 + (a1 - a0) * k / (n - 1))), cz + r * math.sin(math.radians(a0 + (a1 - a0) * k / (n - 1))))
            for k in range(n)]


def _cylv(A, C, r0, r1, seg=12, caps=True):
    d = C - A
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=max(d.length, 1e-5))
    m = Matrix.Translation((A + C) / 2) @ d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def cyl(a, c, r0, r1=None, seg=12, caps=True):
    """Cylindre (cône) entre deux points quelconques du repère de travail."""
    return _cylv(P(*a), P(*c), r0, r0 if r1 is None else r1, seg, caps)


def stud(b, c, n=(1, 0, 0), r=0.004, mat=METAL, hexa=False):
    """Rivet bombé (ou tête de boulon hexagonale avec rondelle) posé en c, normale n."""
    N = P(*n).normalized()
    d = r * (0.8 if hexa else 0.7)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6 if hexa else 8, radius1=r, radius2=r if hexa else r * 0.5, depth=d)
    bmesh.ops.translate(bm, vec=(0, 0, d * 0.35 + (r * 0.3 if hexa else 0)), verts=bm.verts)
    if hexa:
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=r * 1.4, radius2=r * 1.4, depth=r * 0.35)
    b.add(bm, mat, Matrix.Translation(P(*c)) @ N.to_track_quat('Z', 'Y').to_matrix().to_4x4())


def rivets(b, a, c, k, n=(1, 0, 0), r=0.004, mat=METAL, hexa=False):
    for i in range(k):
        t = i / max(k - 1, 1)
        stud(b, tuple(a[j] + (c[j] - a[j]) * t for j in range(3)), n, r, mat, hexa)


def spline(ctrl, sub=6):
    """Courbe de Catmull-Rom par les points de contrôle (repère de travail) → points Blender."""
    V = [P(*c) for c in ctrl]
    ext = [V[0] * 2 - V[1]] + V + [V[-1] * 2 - V[-2]]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(sub):
            t = k / sub
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
    out.append(V[-1])
    return out


def cable(b, ctrl, r=0.008, mat=RUBBER, seg=8, sub=8, ribs=False, ends=ALLOY):
    """Câble ou flexible hydraulique (annelé si ribs) passant par les points de contrôle, raccords sertis aux bouts."""
    pts = spline(ctrl, sub)
    n = len(pts)
    bm = bmesh.new()
    tang = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    up = Vector((0, 0, 1)) if abs(tang[0].z) < 0.9 else Vector((1, 0, 0))
    nx = tang[0].cross(up).normalized()
    loops = []
    for i, p in enumerate(pts):
        t = tang[i]
        nx = (nx - t * nx.dot(t)).normalized()
        ny = t.cross(nx)
        rr = r * (1.22 if ribs and i % 2 and 0 < i < n - 1 else 1.0)
        loops.append([bm.verts.new(p + (nx * math.cos(a) + ny * math.sin(a)) * rr) for a in (2 * math.pi * j / seg for j in range(seg))])
    for A, B in zip(loops, loops[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    b.add(bm, mat)
    if ends is not None:
        for (p, t) in ((pts[0], tang[0]), (pts[-1], -tang[-1])):
            b.add(_cylv(p - t * r * 0.5, p + t * r * 3.2, r * 1.45, r * 1.45, 8), ends)
            b.add(_cylv(p + t * r * 3.2, p + t * r * 4.2, r * 1.45, r * 1.1, 6), ends)


def links(b, ctrl, w=0.05, h=0.02, mat=METAL, sub=5, gap=0.78):
    """Goulotte d'alimentation articulée (maillons) le long d'une courbe."""
    pts = spline(ctrl, sub)
    for A, C in zip(pts, pts[1:]):
        d = C - A
        if d.length < 1e-5:
            continue
        bm = box(w, d.length * gap, h, (0, 0, 0), min(h, d.length) * 0.2, 1)
        b.add(bm, mat, Matrix.Translation((A + C) / 2) @ d.normalized().to_track_quat('Y', 'Z').to_matrix().to_4x4())


def piston(b, a, c, r=0.013, body=METAL, rod=ALLOY, eyes=True):
    """Vérin hydraulique de a vers c : corps, presse-étoupe, tige chromée, chapes à œil et raccord de flexible."""
    A, C = Vector(a), Vector(c)
    d = (C - A).normalized()
    m = A.lerp(C, 0.52)
    b.add(cyl(tuple(A + d * r * 0.5), tuple(m), r * 1.6, seg=12), body)
    b.add(cyl(tuple(m - d * 0.004), tuple(m + d * 0.014), r * 1.9, seg=12), body)
    b.add(cyl(tuple(A + d * r * 2.2), tuple(A + d * r * 3.4), r * 1.85, seg=12), body)
    b.add(cyl(tuple(m), tuple(C - d * r), r, seg=10), rod)
    if eyes:
        for p in (A, C):
            b.add(cyl((p.x - r * 1.25, p.y, p.z), (p.x + r * 1.25, p.y, p.z), r * 1.5, seg=10), body)
            b.add(cyl((p.x - r * 1.6, p.y, p.z), (p.x + r * 1.6, p.y, p.z), r * 0.6, seg=6), rod)
    f = A.lerp(C, 0.42)
    side = Vector((0, 0, 1)) if abs(d.z) < 0.8 else Vector((1, 0, 0))
    b.add(cyl(tuple(f), tuple(f + side * r * 2.4), r * 0.5, seg=6), ALLOY)


def hinge(b, c, L, r, n=3, mat=METAL, pin=ALLOY):
    """Charnière d'axe x : n nœuds séparés, axe traversant à têtes."""
    sl = L / n
    for k in range(n):
        x0 = c[0] - L / 2 + k * sl + 0.002
        b.add(cyl((x0, c[1], c[2]), (x0 + sl - 0.004, c[1], c[2]), r, seg=12), mat)
    b.add(cyl((c[0] - L / 2 - 0.005, c[1], c[2]), (c[0] + L / 2 + 0.005, c[1], c[2]), r * 0.5, seg=8), pin)


def fins_u(b, u0, u1, n, hw, z0, z1, x=0.0, t=0.005, mat=METAL):
    """Radiateur : n ailettes minces perpendiculaires à u."""
    for k in range(n):
        uu = u0 + (u1 - u0) * (k + 0.5) / n
        b.add(box(2 * hw, t, z1 - z0, (x, uu, (z0 + z1) / 2), t * 0.3, 1), mat)


def ofins(b, u0, u1, n, hw, hh, cz=0.0, ch=0.03, t=0.006, mat=METAL, cx=0.0):
    """Ailettes octogonales de refroidissement autour d'un corps."""
    for k in range(n):
        uu = u0 + (u1 - u0) * (k + 0.5) / n
        b.add(ol([(uu - t / 2, hw, hh, cz, ch, cx), (uu + t / 2, hw, hh, cz, ch, cx)]), mat)


def rectc(p0, p1, q0, q1, ch):
    return [(p0 + ch, q0), (p1 - ch, q0), (p1, q0 + ch), (p1, q1 - ch), (p1 - ch, q1), (p0 + ch, q1), (p0, q1 - ch), (p0, q0 + ch)]


def onp(plane, off, p, q, h=0.0):
    """Point (x, u, z) sur une face plane : 'x±' coordonnées (u, z), 'z±' (u, x), 'u±' (x, z) ; h : hauteur au-dessus."""
    d = off + (1 if plane[1] == '+' else -1) * h
    if plane[0] == 'x':
        return (d, p, q)
    if plane[0] == 'z':
        return (q, p, d)
    return (p, d, q)


def nrm(plane):
    sg = 1 if plane[1] == '+' else -1
    return {'x': (sg, 0, 0), 'z': (0, 0, sg), 'u': (0, sg, 0)}[plane[0]]


def face_plate(poly, plane, off, t):
    s1 = off + (1 if plane[1] == '+' else -1) * t
    if plane[0] == 'z':
        poly = [(q, p) for (p, q) in poly]
    return ploft([(off, poly), (s1, poly)], plane[0])


def panel(b, p0, p1, q0, q1, plane, off, t=0.008, ch=0.01, mat=PAINT, bolts=True, br=0.0035):
    """Plaque de blindage rapportée à coins coupés, boulonnée aux quatre coins."""
    b.add(face_plate(rectc(p0, p1, q0, q1, ch), plane, off, t), mat)
    if bolts:
        i = ch * 0.6 + br * 1.6
        for (p, q) in ((p0 + i, q0 + i), (p1 - i, q0 + i), (p0 + i, q1 - i), (p1 - i, q1 - i)):
            stud(b, onp(plane, off, p, q, t), nrm(plane), br, METAL, True)


def clip(poly, x0, x1, y0, y1):
    """Découpe d'un polygone 2D par un rectangle (Sutherland-Hodgman)."""
    def cut(pts, inside, inter):
        out = []
        for i in range(len(pts)):
            a, c = pts[i - 1], pts[i]
            if inside(c):
                if not inside(a):
                    out.append(inter(a, c))
                out.append(c)
            elif inside(a):
                out.append(inter(a, c))
        return out

    def ix(v):
        return lambda a, c: (v, a[1] + (c[1] - a[1]) * (v - a[0]) / (c[0] - a[0]))

    def iy(v):
        return lambda a, c: (a[0] + (c[0] - a[0]) * (v - a[1]) / (c[1] - a[1]), v)
    pts = poly
    for ins, it in ((lambda p: p[0] >= x0, ix(x0)), (lambda p: p[0] <= x1, ix(x1)), (lambda p: p[1] >= y0, iy(y0)), (lambda p: p[1] <= y1, iy(y1))):
        if not pts:
            return []
        pts = cut(pts, ins, it)
    out = []
    for p in pts:
        if not out or abs(p[0] - out[-1][0]) + abs(p[1] - out[-1][1]) > 1e-5:
            out.append(p)
    if len(out) > 1 and abs(out[0][0] - out[-1][0]) + abs(out[0][1] - out[-1][1]) < 1e-5:
        out.pop()
    return out


def hazard(b, p0, p1, q0, q1, plane, off, t=0.0025):
    """Bande d'alerte : rayures obliques jaunes (Marking) sur fond noir, posée sur une face plane."""
    b.add(face_plate(rectc(p0, p1, q0, q1, 0.002), plane, off, t), POLY)
    H = q1 - q0
    per = max(H * 1.3, 0.016)
    a = p0 - H - per
    while a < p1:
        c = clip([(a, q0), (a + per / 2, q0), (a + per / 2 + H, q1), (a + H, q1)], p0 + 0.002, p1 - 0.002, q0 + 0.002, q1 - 0.002)
        if len(c) >= 3:
            b.add(face_plate(c, plane, off, t * 1.5), MARKING)
        a += per


def leds(b, plane, off, p, q, n=3, step=0.012, mat=GLOW):
    """Fenêtre de témoins (verre) et diodes."""
    b.add(face_plate(rectc(p - step * 0.8, p + step * (n - 0.2), q - 0.007, q + 0.007, 0.002), plane, off, 0.002), GLASS)
    for k in range(n):
        b.add(face_plate(rectc(p + k * step - 0.003, p + k * step + 0.003, q - 0.003, q + 0.003, 0.001), plane, off, 0.0032), mat)


def label(b, text, c, h, plane='u-', mat=MARKING, depth=0.0008):
    """Inscription au pochoir en relief sur une face 'u+' (avant), 'u-' (arrière) ou 'z+' (dessus), centrée en c."""
    cu = bpy.data.curves.new("lab", 'FONT')
    cu.body = text
    cu.size = h
    cu.extrude = depth / 2
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    ob = bpy.data.objects.new("lab", cu)
    bpy.context.collection.objects.link(ob)
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    x, u, z = c
    for v in bm.verts:
        X, Y, Z = v.co
        if plane == 'u+':
            v.co = P(x - X, u + Z, z + Y)
        elif plane == 'u-':
            v.co = P(x + X, u - Z, z + Y)
        else:
            v.co = P(x - Y, u + X, z + Z)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    b.add(bm, mat)


def lamella(b, u0, u1, hw, hh, ch, cz=0.0, t=0.011, drop=0.36, mat=PAINT, sc=0.965):
    """Lame de blindage articulée couvrant le dessus et le haut des flancs d'un profil octogonal (bord avant plus large)."""
    zl = cz + hh - 2 * hh * drop
    out = [(hw + t, zl), (hw + t, cz + hh - ch + t * 0.4), (hw - ch + t * 0.4, cz + hh + t), (-hw + ch - t * 0.4, cz + hh + t),
           (-hw - t, cz + hh - ch + t * 0.4), (-hw - t, zl)]
    inn = [(hw, zl), (hw, cz + hh - ch), (hw - ch, cz + hh), (-hw + ch, cz + hh), (-hw, cz + hh - ch), (-hw, zl)]
    b.add(sweep2(out, inn, u0, u1, 'u', sc, 1.0, (0.0, cz)), mat)


def pts(g, muzzle, grip, butt, sup, mag=(0, 0.0, -0.1), vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Fourreau d'avant-bras ─────────────────────────

def sleeve(b, u0=-0.32, u1=0.08, z=-0.02, w=0.085, h=0.1, ram=True, hose=True):
    """Fourreau d'avant-bras : coque octogonale creuse (l'avant-bras entre par l'arrière), joint caoutchouc côtelé, trois
    lames de blindage articulées rivetées, plaques latérales boulonnées (alerte, témoins, grille), patin de dessous,
    collerette boulonnée de fixation de l'arme, vérins et flexible. Renvoie (prise, crosse)."""
    ch = min(w, h) * 0.42
    b.add(ohollow([(u0, w * 0.93, h * 0.91, z, ch * 0.9), (u0 + 0.03, w, h, z, ch), (u1 - 0.05, w, h, z, ch),
                   (u1, w * 0.95, h * 0.94, z, ch * 0.95)], 0.012), ACCENT)
    # Joint arrière côtelé.
    b.add(ohollow([(u0 - 0.024, w * 0.88, h * 0.86, z, ch * 0.8), (u0 + 0.012, w * 0.96, h * 0.95, z, ch * 0.88)], 0.022), RUBBER)
    for k in range(2):
        uu = u0 - 0.016 + k * 0.014
        b.add(ohollow([(uu, w * 0.93, h * 0.91, z, ch * 0.85), (uu + 0.006, w * 0.93, h * 0.91, z, ch * 0.85)], 0.02), RUBBER)
    # Lames de blindage.
    n = 3
    a0, a1 = u0 + 0.035, u1 - 0.045
    L = (a1 - a0) / n
    for k in range(n):
        a = a0 + k * L
        lamella(b, a, a + L + 0.012, w, h, ch, z)
        for xx in (-w * 0.5, 0.0, w * 0.5):
            stud(b, (xx, a + L + 0.002, z + h + 0.011), (0, 0, 1), 0.004)
    # Plaques latérales.
    q0, q1 = z - h + ch + 0.006, z + h * 0.28 - 0.008
    for s in (1, -1):
        pl = 'x+' if s > 0 else 'x-'
        panel(b, u0 + 0.05, u1 - 0.06, q0, q1, pl, s * w, 0.007, 0.012, PAINT)
        if s > 0:
            hazard(b, u0 + 0.075, u0 + 0.19, q0 + 0.008, q0 + 0.024, pl, w + 0.007)
            leds(b, pl, w + 0.007, u1 - 0.13, q1 - 0.016)
        else:
            for k in range(6):
                b.add(face_plate(rectc(u0 + 0.09 + k * 0.022, u0 + 0.1 + k * 0.022, q0 + 0.01, q1 - 0.01, 0.003), pl, -w - 0.007, 0.004), POLY)
    # Patin de dessous à nervures.
    b.add(face_plate(rectc(u0 + 0.05, u1 - 0.07, -w * 0.5, w * 0.5, 0.015), 'z-', z - h, 0.008), POLY)
    for k in range(4):
        uu = u0 + 0.08 + k * (u1 - u0 - 0.17) / 3
        b.add(box(w * 0.9, 0.008, 0.006, (0, uu, z - h - 0.01), 0.002), RUBBER)
    # Collerette avant boulonnée.
    b.add(ohollow([(u1 - 0.035, w * 1.08, h * 1.07, z, ch * 1.05), (u1 + 0.004, w * 1.08, h * 1.07, z, ch * 1.05)], 0.03), METAL)
    for s in (1, -1):
        for zz in (z - h * 0.35, z + h * 0.35):
            stud(b, (s * w * 1.08, u1 - 0.016, zz), (s, 0, 0), 0.0048, METAL, True)
    for xx in (-w * 0.4, w * 0.4):
        stud(b, (xx, u1 - 0.016, z + h * 1.07), (0, 0, 1), 0.0048, METAL, True)
    # Vérins latéraux et chapes.
    if ram:
        zr = z - h * 0.74
        for s in (1, -1):
            xr = s * (w + 0.012)
            b.add(box(0.026, 0.03, 0.03, (s * (w + 0.004), u0 + 0.07, zr), 0.004), METAL)
            b.add(box(0.026, 0.03, 0.03, (s * (w + 0.004), u1 - 0.055, zr), 0.004), METAL)
            piston(b, (xr, u0 + 0.07, zr), (xr, u1 - 0.055, zr), 0.0095)
    # Flexible hydraulique annelé et poignée intérieure (gâchette au pouce).
    if hose:
        cable(b, [(-w * 0.55, u0 + 0.045, z + h + 0.016), (-w * 0.8, u0 + 0.12, z + h + 0.05), (-w * 0.8, u1 - 0.14, z + h + 0.05),
                  (-w * 0.6, u1 - 0.035, z + h + 0.03)], 0.0085, RUBBER, 8, 10, True)
    b.add(cyl((0, u1 - 0.07, z - h * 0.55), (0, u1 - 0.07, z + h * 0.5), 0.018, seg=10), RUBBER)
    b.add(box(0.016, 0.02, 0.012, (0, u1 - 0.07, z + h * 0.56), 0.003), ALLOY)
    return (0, u1 - 0.07, z), (0, u0, z)


SLEEVE_GUN = (-0.36, 0.04, -0.12, 0.085, 0.095)


# ───────────────────────── Frappe ─────────────────────────

def hammer(g):
    """Vibro Hammer : bras à vérins et bobines, tête large à cadre de frappe vibrant, radiateurs dessus et dessous."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.072, 0.078, 0.0, 0.03), (0.12, 0.068, 0.074, 0.0, 0.028), (0.4, 0.058, 0.066, 0.0, 0.024), (0.47, 0.066, 0.076, 0.0, 0.028)]), PAINT)
    b.add(prism([(0.1, 0.07), (0.44, 0.06), (0.45, 0.076), (0.12, 0.088)], -0.03, 0.03), METAL)          # dorsale
    rivets(b, (0, 0.14, 0.083), (0, 0.42, 0.071), 6, (0, 0, 1), 0.0038)
    ofins(b, 0.27, 0.41, 6, 0.074, 0.08, 0.0, 0.03, 0.007, METAL)                                         # bobines
    for s in (1, -1):
        energy_cell(b, 0.19, 0.035, s * 0.074, 0.1, 0.016)
        b.add(box(0.03, 0.04, 0.034, (s * 0.096, 0.085, -0.05), 0.004), METAL)
        b.add(box(0.03, 0.04, 0.034, (s * 0.1, 0.475, -0.05), 0.004), METAL)
        piston(b, (s * 0.1, 0.085, -0.05), (s * 0.1, 0.475, -0.05), 0.013)
    # Tête.
    b.add(ol([(0.455, 0.1, 0.17, 0.0, 0.04), (0.48, 0.1, 0.17, 0.0, 0.04)]), METAL)
    b.add(ol([(0.475, 0.115, 0.2, 0.0, 0.05), (0.495, 0.126, 0.216, 0.0, 0.056), (0.68, 0.126, 0.216, 0.0, 0.056), (0.7, 0.118, 0.206, 0.0, 0.05)]), PAINT)
    b.add(ol([(0.7, 0.132, 0.226, 0.0, 0.062), (0.745, 0.132, 0.226, 0.0, 0.062), (0.752, 0.124, 0.218, 0.0, 0.058)]), METAL)
    b.add(ol([(0.74, 0.1, 0.188, 0.0, 0.044), (0.758, 0.1, 0.188, 0.0, 0.044)]), ENERGY)
    for k in range(7):
        b.add(box(0.205, 0.012, 0.014, (0, 0.762, -0.15 + k * 0.05), 0.003), METAL)                     # grille vibrante
    for (x, z) in ((-0.117, -0.12), (0.117, -0.12), (-0.117, 0.12), (0.117, 0.12), (-0.117, 0.0), (0.117, 0.0), (-0.05, -0.208), (0.05, -0.208),
                   (-0.05, 0.208), (0.05, 0.208)):
        stud(b, (x, 0.752, z), (0, 1, 0), 0.0055, METAL, True)
    fins_u(b, 0.505, 0.675, 8, 0.092, 0.214, 0.272)
    fins_u(b, 0.505, 0.675, 8, 0.092, -0.272, -0.214)
    for s in (1, -1):
        pl = 'x+' if s > 0 else 'x-'
        panel(b, 0.51, 0.67, -0.13, 0.1, pl, s * 0.126, 0.01, 0.025, ACCENT)
        hazard(b, 0.505, 0.675, 0.118, 0.152, pl, s * 0.126)
        b.add(face_plate(rectc(0.55, 0.63, -0.06, 0.03, 0.01), pl, s * 0.136, 0.004), METAL)
    cable(b, [(-0.045, -0.18, 0.1), (-0.06, 0.05, 0.16), (-0.05, 0.3, 0.14), (-0.05, 0.47, 0.17)], 0.011, RUBBER, 8, 10, True)
    cable(b, [(0.05, 0.02, 0.08), (0.07, 0.2, 0.12), (0.06, 0.46, 0.14)], 0.007, POLY, 6, 8)
    pts(g, (0, 0.77, 0), grip, butt, (0, 0.3, -0.06))


def vulcan(g):
    """Vulcan Hammer : col à quatre vérins de recul, barillet de charges cannelé (douilles visibles), enclume de frappe et
    tuyères d'échappement orientées vers l'arrière."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.082, 0.086, 0.0, 0.034), (0.3, 0.075, 0.08, 0.0, 0.03), (0.345, 0.092, 0.096, 0.0, 0.038)]), PAINT)
    for s in (1, -1):
        hazard(b, 0.12, 0.27, -0.022, 0.022, 'x+' if s > 0 else 'x-', s * 0.0775)
    for sx in (1, -1):
        for sz in (1, -1):
            piston(b, (sx * 0.072, 0.1, sz * 0.082), (sx * 0.086, 0.4, sz * 0.096), 0.011)
    b.add(cyl((0, 0.32, 0), (0, 0.64, 0), 0.042, seg=12), METAL)                                           # axe du barillet
    drum = nl([(0.36, 0.122, 0.0), (0.372, 0.14, 0.0), (0.588, 0.14, 0.0), (0.6, 0.122, 0.0)], 18)
    cuts = [cyl((0.14 * math.cos(a), 0.34, 0.14 * math.sin(a)), (0.14 * math.cos(a), 0.62, 0.14 * math.sin(a)), 0.028, seg=10)
            for a in (math.pi / 6 + k * math.pi / 3 for k in range(6))]
    b.add(carve(drum, cuts), ACCENT)
    for k in range(6):
        a = k * math.pi / 3
        x, z = 0.085 * math.cos(a), 0.085 * math.sin(a)
        b.add(cyl((x, 0.352, z), (x, 0.362, z), 0.03, seg=14), ALLOY)                                   # culots de douilles
        b.add(cyl((x, 0.348, z), (x, 0.353, z), 0.011, seg=8), METAL)
    b.add(nhollow([(0.44, 0.146, 0.0), (0.47, 0.146, 0.0)], 18, 0.012), METAL)
    b.add(nhollow([(0.52, 0.146, 0.0), (0.55, 0.146, 0.0)], 18, 0.012), METAL)
    # Enclume.
    b.add(ol([(0.6, 0.12, 0.12, 0.0, 0.045), (0.62, 0.135, 0.135, 0.0, 0.052), (0.72, 0.135, 0.135, 0.0, 0.052), (0.74, 0.122, 0.122, 0.0, 0.046)]), PAINT)
    face = ol([(0.735, 0.115, 0.115, 0.0, 0.042), (0.8, 0.095, 0.095, 0.0, 0.034)])
    cuts = [box(0.3, 0.012, 0.008, (0, 0.8, zz), 0) for zz in (-0.05, 0.0, 0.05)] + [box(0.008, 0.012, 0.3, (xx, 0.8, 0), 0) for xx in (-0.05, 0.0, 0.05)]
    b.add(carve(face, cuts), METAL)
    b.add(cyl((0, 0.795, 0), (0, 0.803, 0), 0.03, seg=16), GLOW)
    for sx in (1, -1):
        for sz in (1, -1):
            p0 = (sx * 0.09, 0.69, sz * 0.09)
            p1 = (sx * 0.17, 0.61, sz * 0.17)
            b.add(cyl(p0, p1, 0.026, 0.036, seg=12), METAL)
            b.add(cyl(p1, (sx * 0.172, 0.608, sz * 0.172), 0.03, seg=12), GLOW)
    for s in (1, -1):
        panel(b, 0.63, 0.71, -0.06, 0.06, 'x+' if s > 0 else 'x-', s * 0.135, 0.008, 0.012, ACCENT)
    cable(b, [(-0.045, -0.18, 0.1), (-0.07, 0.1, 0.15), (-0.08, 0.38, 0.17), (-0.06, 0.62, 0.15)], 0.011, RUBBER, 8, 10, True)
    pts(g, (0, 0.8, 0), grip, butt, (0, 0.3, -0.06))


def blade(g):
    """Force Blade : boîtier émetteur ventilé à cellules, garde, lame forgée à dos épais, gorge lumineuse et fil d'énergie."""
    b = g.body
    grip, butt = sleeve(b)
    hous = ol([(0.06, 0.062, 0.085, 0.0, 0.03), (0.1, 0.068, 0.092, 0.0, 0.032), (0.29, 0.064, 0.088, 0.0, 0.03), (0.33, 0.074, 0.1, 0.0, 0.036)])
    b.add(carve(hous, slots(0.12, 0.28, 6, 0.066, -0.02, 0.012, 0.06, 0.03) + slots(0.12, 0.28, 6, -0.066, -0.02, 0.012, 0.06, 0.03)), PAINT)
    b.add(ol([(0.115, 0.05, 0.07, -0.02, 0.02), (0.285, 0.05, 0.07, -0.02, 0.02)]), POLY)                  # fond des évents
    for uu in (0.135, 0.2, 0.265):
        energy_cell(b, uu, 0.108, 0, 0.055, 0.016)
    for s in (1, -1):
        b.add(box(0.008, 0.2, 0.012, (s * 0.024, 0.2, 0.118), 0.002), METAL)
    b.add(ol([(0.33, 0.09, 0.135, 0.0, 0.04), (0.37, 0.09, 0.135, 0.0, 0.04)]), METAL)                    # garde
    for (x, z) in ((-0.06, -0.1), (0.06, -0.1), (-0.06, 0.1), (0.06, 0.1)):
        stud(b, (x, 0.37, z), (0, 1, 0), 0.006, METAL, True)
    # Lame (dos en bas, fil en haut, pointe à contre-fil).

    def sec(t, zb, zt):
        zm = zb + (zt - zb) * 0.45
        return [(0.0, zt), (t * 0.35, zt - 0.012), (t, zm), (t, zb + 0.006), (t * 0.6, zb), (-t * 0.6, zb), (-t, zb + 0.006), (-t, zm), (-t * 0.35, zt - 0.012)]
    prof = [(0.37, 0.013, -0.055, 0.062), (0.46, 0.012, -0.05, 0.06), (0.8, 0.0095, -0.04, 0.052), (1.04, 0.0075, -0.03, 0.044),
            (1.15, 0.0055, -0.016, 0.032), (1.225, 0.0015, -0.002, 0.007)]
    bl = ploft([(u, sec(t, zb, zt)) for (u, t, zb, zt) in prof])
    cuts = [box(0.006, 0.52, 0.016, (s * 0.0125, 0.7, -0.008), 0.002) for s in (1, -1)]
    b.add(carve(bl, cuts), ALLOY)
    for s in (1, -1):
        b.add(box(0.002, 0.5, 0.006, (s * 0.0092, 0.7, -0.008), 0.0008, 1), ENERGY)                     # gorge
    edge = [(u, [(0.0022, zt - 0.008), (0.0022, zt + 0.004), (-0.0022, zt + 0.004), (-0.0022, zt - 0.008)]) for (u, t, zb, zt) in prof[:-1]]
    edge.append((1.228, [(0.0006, 0.004), (0.0006, 0.008), (-0.0006, 0.008), (-0.0006, 0.004)]))
    b.add(ploft(edge), ENERGY)
    b.add(ploft([(u, [(t * 0.7, zb - 0.004), (t * 0.7, zb + 0.004), (-t * 0.7, zb + 0.004), (-t * 0.7, zb - 0.004)]) for (u, t, zb, zt) in prof[:4]]), METAL)
    b.add(ol([(0.37, 0.024, 0.075, 0.003, 0.012), (0.44, 0.022, 0.07, 0.003, 0.011)]), METAL)               # bride de lame
    for s in (1, -1):
        for zz in (-0.035, 0.04):
            stud(b, (s * 0.022, 0.405, zz), (s, 0, 0), 0.0045, METAL, True)
    cable(b, [(0.04, -0.15, 0.1), (0.06, 0.05, 0.12), (0.05, 0.2, 0.1), (0.035, 0.33, 0.085)], 0.007, RUBBER, 6, 8)
    pts(g, (0, 1.22, 0.004), grip, butt, (0, 0.2, -0.06))


def forceax(g):
    """Force Ax : long manche octogonal à manchons, douille de fer, fer en croissant ajouré à fil d'énergie, pointe arrière."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.048, 0.052, 0.0, 0.02), (0.1, 0.042, 0.046, 0.0, 0.017), (0.82, 0.036, 0.04, 0.0, 0.014), (0.86, 0.03, 0.034, 0.0, 0.012)]), PAINT)
    ofins(b, 0.13, 0.3, 7, 0.046, 0.05, 0.0, 0.018, 0.012, RUBBER)
    b.add(box(0.03, 0.04, 0.03, (0, 0.11, -0.06), 0.004), METAL)
    b.add(box(0.03, 0.04, 0.03, (0, 0.5, -0.055), 0.004), METAL)
    piston(b, (0, 0.11, -0.068), (0, 0.5, -0.064), 0.012)
    b.add(ol([(0.53, 0.054, 0.064, 0.01, 0.022), (0.55, 0.06, 0.07, 0.01, 0.025), (0.76, 0.06, 0.07, 0.01, 0.025), (0.78, 0.054, 0.064, 0.01, 0.022)]), METAL)
    rivets(b, (0.06, 0.58, -0.03), (0.06, 0.73, -0.03), 4, (1, 0, 0), 0.004, METAL, True)
    rivets(b, (-0.06, 0.58, -0.03), (-0.06, 0.73, -0.03), 4, (-1, 0, 0), 0.004, METAL, True)
    edge = arc(0.5, 0.2, 0.33, -33, 56, 12)
    full = [(0.58, 0.06), (0.75, 0.06)] + edge + [(0.6, 0.43), (0.585, 0.3), (0.6, 0.17)]
    holes = [cyl((-0.1, 0.665, 0.19), (0.1, 0.665, 0.19), 0.034, seg=14), cyl((-0.1, 0.63, 0.33), (0.1, 0.63, 0.33), 0.026, seg=14)]
    b.add(carve(prism(full, -0.011, 0.011, 0.002), holes), ALLOY)
    inner = arc(0.5, 0.2, 0.28, -24, 52, 10)
    body = [(0.585, 0.06), (0.74, 0.06)] + inner + [(0.6, 0.4), (0.592, 0.3), (0.605, 0.17)]
    holes = [cyl((-0.1, 0.665, 0.19), (0.1, 0.665, 0.19), 0.04, seg=14), cyl((-0.1, 0.63, 0.33), (0.1, 0.63, 0.33), 0.032, seg=14)]
    b.add(carve(prism(body, -0.024, 0.024, 0.003), holes), PAINT)
    eg = arc(0.5, 0.2, 0.333, -31, 54, 12)
    b.add(band(eg, [0.012] * len(eg), -0.003, 0.003), ENERGY)
    for s in (1, -1):
        for (u, z) in ((0.62, 0.09), (0.7, 0.09), (0.73, 0.27), (0.68, 0.39), (0.61, 0.24)):
            stud(b, (s * 0.024, u, z), (s, 0, 0), 0.0045)
        hazard(b, 0.6, 0.73, -0.045, -0.025, 'x+' if s > 0 else 'x-', s * 0.06)
    b.add(prism([(0.6, -0.04), (0.71, -0.04), (0.64, -0.2), (0.62, -0.19)], -0.016, 0.016, 0.003), METAL)       # pointe
    pts(g, (0, 0.82, 0.22), grip, butt, (0, 0.2, -0.06))


def roller(g):
    """Vibro Roller : fourche ajourée, traverse, rouleau à dents en quinconce, flasques, moteur latéral à ailettes, capot."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(-0.16, 0.06, 0.085, 0.0, 0.025, 0.1), (0.16, 0.06, 0.085, 0.0, 0.025, 0.1)], 'x'), PAINT)
    for s in (1, -1):
        arm = prism([(0.06, -0.075), (0.06, 0.075), (0.16, 0.085), (0.5, 0.055), (0.6, 0.04), (0.625, 0.0), (0.6, -0.04), (0.5, -0.055), (0.16, -0.085)],
                    s * 0.13, s * 0.158, 0.003)
        b.add(carve(arm, slots(0.22, 0.46, 3, s * 0.144, 0.0, 0.05, 0.05, 0.05)), PAINT)
        hazard(b, 0.2, 0.47, 0.05, 0.068, 'x+' if s > 0 else 'x-', s * 0.158)
        rivets(b, (s * 0.158, 0.2, -0.055), (s * 0.158, 0.5, -0.04), 5, (s, 0, 0), 0.004)
        b.add(nl([(s * 0.158, 0.075, 0.0, 0.56), (s * 0.172, 0.068, 0.0, 0.56)], 14, 'x'), METAL)
        piston(b, (s * 0.1, 0.17, 0.09), (s * 0.1, 0.45, 0.165), 0.011)
    # Moteur à ailettes (droite), palier (gauche).
    b.add(nl([(0.172, 0.06, 0.0, 0.56), (0.24, 0.06, 0.0, 0.56), (0.25, 0.045, 0.0, 0.56)], 14, 'x'), ACCENT)
    for k in range(4):
        xx = 0.18 + k * 0.016
        b.add(nl([(xx, 0.078, 0.0, 0.56), (xx + 0.006, 0.078, 0.0, 0.56)], 14, 'x'), METAL)
    for k in range(6):
        a = k * math.pi / 3
        stud(b, (0.25, 0.56 + 0.032 * math.cos(a), 0.032 * math.sin(a)), (1, 0, 0), 0.005, METAL, True)
    b.add(nl([(-0.172, 0.05, 0.0, 0.56), (-0.2, 0.04, 0.0, 0.56)], 12, 'x'), METAL)
    # Rouleau.
    b.add(nl([(-0.126, 0.1, 0.0, 0.56), (-0.118, 0.115, 0.0, 0.56), (0.118, 0.115, 0.0, 0.56), (0.126, 0.1, 0.0, 0.56)], 20, 'x'), METAL)
    for s in (1, -1):
        b.add(nl([(s * 0.118, 0.13, 0.0, 0.56), (s * 0.13, 0.13, 0.0, 0.56)], 20, 'x'), ACCENT)
        b.add(nl([(s * 0.13, 0.05, 0.0, 0.56), (s * 0.133, 0.05, 0.0, 0.56)], 14, 'x'), ENERGY)
    for i in range(14):
        for j in range(4):
            a = 2 * math.pi * (i + 0.5 * (j % 2)) / 14
            x = -0.09 + j * 0.06
            ca, sa = math.cos(a), math.sin(a)
            b.add(cyl((x, 0.56 + 0.11 * ca, 0.11 * sa), (x, 0.56 + 0.155 * ca, 0.155 * sa), 0.014, 0.003, seg=4), ALLOY)
    for j in range(5):
        x = -0.12 + j * 0.06
        b.add(nl([(x - 0.003, 0.118, 0.0, 0.56), (x + 0.003, 0.118, 0.0, 0.56)], 20, 'x'), ALLOY)
    # Capot au-dessus du rouleau.
    out = arc(0.56, 0.0, 0.192, 15, 160, 10)
    inn = arc(0.56, 0.0, 0.178, 15, 160, 10)
    b.add(sweep2(out, inn, -0.145, 0.145, 'x'), PAINT)
    for s in (1, -1):
        b.add(sweep2(arc(0.56, 0.0, 0.2, 15, 160, 10), arc(0.56, 0.0, 0.19, 15, 160, 10), s * 0.135, s * 0.15, 'x'), METAL)
    rivets(b, (-0.1, 0.56, 0.193), (0.1, 0.56, 0.193), 5, (0, 0, 1), 0.004)
    pts(g, (0, 0.72, 0), grip, butt, (0, 0.22, -0.06))


def haken(g):
    """Vibro / Quake Haken : poignet émetteur, deux mâchoires courbes articulées sur charnières, dents, vérins de serrage."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.078, 0.084, 0.0, 0.03), (0.26, 0.072, 0.078, 0.0, 0.028), (0.31, 0.084, 0.094, 0.0, 0.034)]), PAINT)
    ofins(b, 0.12, 0.25, 5, 0.086, 0.09, 0.0, 0.034, 0.006, METAL)
    b.add(ol([(0.31, 0.06, 0.06, 0.0, 0.022), (0.36, 0.055, 0.055, 0.0, 0.02)]), METAL)
    b.add(cyl((0, 0.36, 0), (0, 0.44, 0), 0.042, 0.02, seg=14), ACCENT)
    for k in range(3):
        b.add(ring(0.04 - k * 0.006, 0.02, 0.37 + k * 0.022, 0.38 + k * 0.022, 0, 0, 14), ENERGY)
    for sz in (1, -1):
        c = [(0.33, 0.07), (0.43, 0.15), (0.54, 0.205), (0.65, 0.19), (0.73, 0.13), (0.775, 0.05)]
        c = [(u, sz * z) for (u, z) in c]
        b.add(band(c, [0.07, 0.068, 0.058, 0.046, 0.032, 0.004], -0.03, 0.03), METAL)
        oc = [(0.36, sz * 0.105), (0.44, sz * 0.18), (0.54, sz * 0.232), (0.64, sz * 0.218)]
        b.add(band(oc, [0.026, 0.03, 0.028, 0.012], -0.038, 0.038), ACCENT)
        b.add(band([(0.44, sz * 0.192), (0.54, sz * 0.245), (0.62, sz * 0.235)], [0.006] * 3, -0.012, 0.012), GLOW)
        for (u, z) in ((0.5, 0.15), (0.6, 0.145), (0.69, 0.1)):
            b.add(prism([(u - 0.02, sz * z), (u + 0.02, sz * z), (u + 0.005, sz * (z - 0.04))], -0.024, 0.024, 0.002), ALLOY)
        hinge(b, (0, 0.33, sz * 0.07), 0.09, 0.02)
        piston(b, (0, 0.12, sz * 0.1), (0, 0.43, sz * 0.19), 0.01)
        b.add(box(0.026, 0.03, 0.02, (0, 0.12, sz * 0.092), 0.004), METAL)
        for s in (1, -1):
            for (u, z) in ((0.42, 0.14), (0.55, 0.19)):
                stud(b, (s * 0.03, u, sz * z), (s, 0, 0), 0.0045)
    pts(g, (0, 0.77, 0), grip, butt, (0, 0.2, -0.06))


# ───────────────────────── Perforation ─────────────────────────

def blasthole(g):
    """Blasthole Spear : chambre de charge à lames, chargeur de cartouches latéral, éjecteurs, lance télescopique à
    trois éléments, colliers boulonnés, vérins de rappel et pointe à quatre arêtes."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.082, 0.092, 0.0, 0.034), (0.1, 0.088, 0.098, 0.0, 0.037), (0.38, 0.088, 0.098, 0.0, 0.037), (0.43, 0.072, 0.082, 0.0, 0.03)]), PAINT)
    for k in range(2):
        lamella(b, 0.11 + k * 0.13, 0.25 + k * 0.13, 0.088, 0.098, 0.037, 0.0, 0.01, 0.3, ACCENT)
    b.add(ol([(0.12, 0.03, 0.07, -0.005, 0.012, 0.11), (0.34, 0.03, 0.07, -0.005, 0.012, 0.11)]), ACCENT)     # chargeur
    for k in range(4):
        uu = 0.15 + k * 0.05
        b.add(cyl((0.13, uu, -0.02), (0.146, uu, -0.02), 0.019, seg=14), ALLOY)
        b.add(cyl((0.143, uu, -0.02), (0.148, uu, -0.02), 0.007, seg=8), METAL)
    hazard(b, 0.14, 0.34, -0.035, -0.005, 'x-', -0.088)
    for sx in (1, -1):
        for sz in (1, -1):
            p0, p1 = (sx * 0.05, 0.42, sz * 0.055), (sx * 0.1, 0.36, sz * 0.11)
            b.add(cyl(p0, p1, 0.018, 0.024, seg=10), METAL)
            b.add(cyl(p1, (sx * 0.101, 0.359, sz * 0.111), 0.019, seg=10), GLOW)
    b.add(ol([(0.43, 0.052, 0.052, 0.0, 0.02), (0.72, 0.05, 0.05, 0.0, 0.019)]), METAL)
    b.add(ol([(0.7, 0.066, 0.066, 0.0, 0.026), (0.745, 0.066, 0.066, 0.0, 0.026)]), ACCENT)
    b.add(ol([(0.745, 0.04, 0.04, 0.0, 0.015), (0.97, 0.039, 0.039, 0.0, 0.015)]), ALLOY)
    b.add(ol([(0.955, 0.052, 0.052, 0.0, 0.02), (0.99, 0.052, 0.052, 0.0, 0.02)]), ACCENT)
    b.add(ol([(0.99, 0.031, 0.031, 0.0, 0.012), (1.13, 0.03, 0.03, 0.0, 0.012)]), ALLOY)
    for uu in (0.722, 0.972):
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            r = 0.066 if uu < 0.9 else 0.052
            stud(b, (r * 0.75 * math.cos(a), uu, r * 0.75 * math.sin(a)), (math.cos(a), 0, math.sin(a)), 0.005, METAL, True)
    b.add(nl([(1.12, 0.045, 0.0), (1.15, 0.056, 0.0), (1.33, 0.004, 0.0)], 4, rot=math.pi / 4), METAL)          # pointe
    for k in range(4):
        a = k * math.pi / 2
        bm = prism([(1.15, 0.0), (1.25, 0.0), (1.15, 0.06)], -0.004, 0.004, 0.0)
        b.add(bm, ALLOY, Matrix.Rotation(-a, 4, 'Y'))
    for sz in (1, -1):
        b.add(box(0.03, 0.04, 0.03, (0, 0.13, sz * 0.118), 0.004), METAL)
        piston(b, (0, 0.13, sz * 0.122), (0, 0.69, sz * 0.078), 0.012)
    cable(b, [(-0.045, -0.18, 0.1), (-0.07, 0.0, 0.15), (-0.09, 0.15, 0.1), (-0.09, 0.3, 0.06)], 0.01, RUBBER, 8, 10, True)
    pts(g, (0, 1.33, 0), grip, butt, (0, 0.2, -0.07))


def twinspear(g):
    """Blast Twin Spears : chambre double large, cheminées d'échappement, deux lances à colliers, brides de liaison."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.14, 0.08, 0.0, 0.035), (0.1, 0.152, 0.088, 0.0, 0.04), (0.36, 0.152, 0.088, 0.0, 0.04), (0.41, 0.13, 0.075, 0.0, 0.034)]), PAINT)
    for s in (1, -1):
        panel(b, 0.12, 0.34, -0.035, 0.035, 'x+' if s > 0 else 'x-', s * 0.152, 0.008, 0.012, ACCENT)
    hazard(b, 0.12, 0.34, -0.02, 0.02, 'z+', 0.088)
    for s in (1, -1):
        x = s * 0.1
        b.add(cyl((x, 0.24, 0.07), (x, 0.24, 0.16), 0.024, seg=12), METAL)
        b.add(nhollow([(0.152, 0.03, 0.24, x), (0.17, 0.03, 0.24, x)], 12, 0.006, 'z'), ACCENT)
        b.add(cyl((x, 0.24, 0.165), (x, 0.24, 0.167), 0.02, seg=12), GLOW)
        x = s * 0.075
        b.add(ol([(0.41, 0.05, 0.05, 0.0, 0.02, x), (0.47, 0.05, 0.05, 0.0, 0.02, x)]), METAL)
        b.add(ol([(0.47, 0.034, 0.034, 0.0, 0.013, x), (0.82, 0.033, 0.033, 0.0, 0.013, x)]), METAL)
        b.add(ol([(0.8, 0.044, 0.044, 0.0, 0.017, x), (0.835, 0.044, 0.044, 0.0, 0.017, x)]), ACCENT)
        b.add(ol([(0.835, 0.025, 0.025, 0.0, 0.01, x), (1.02, 0.024, 0.024, 0.0, 0.01, x)]), ALLOY)
        b.add(nl([(1.01, 0.036, 0.0, x), (1.035, 0.044, 0.0, x), (1.18, 0.003, 0.0, x)], 4, rot=math.pi / 4), METAL)
    for uu in (0.6, 0.9):
        b.add(ol([(uu, 0.125, 0.045, 0.0, 0.018), (uu + 0.04, 0.125, 0.045, 0.0, 0.018)]), METAL)
        rivets(b, (-0.04, uu + 0.02, 0.045), (0.04, uu + 0.02, 0.045), 3, (0, 0, 1), 0.005, METAL, True)
    spine = prism([(0.41, -0.03), (0.41, 0.03), (0.9, 0.022), (0.9, -0.022)], -0.008, 0.008, 0.002)
    b.add(carve(spine, slots(0.46, 0.86, 4, 0.0, 0.0, 0.06, 0.022, 0.05)), ACCENT)
    cable(b, [(-0.045, -0.18, 0.1), (-0.08, 0.0, 0.13), (-0.11, 0.1, 0.1)], 0.01, RUBBER, 8, 10, True)
    pts(g, (0, 1.18, 0), grip, butt, (0, 0.2, -0.07))


def flashing(g):
    """Flashing Spear : corps à cellules transversales, poutre inférieure, cinq bobines d'accélération, dard à ailettes."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.07, 0.086, 0.0, 0.03), (0.1, 0.076, 0.092, 0.0, 0.032), (0.44, 0.072, 0.082, 0.0, 0.03), (0.49, 0.06, 0.07, 0.0, 0.026)]), PAINT)
    b.add(ol([(0.11, 0.05, 0.034, 0.124, 0.012), (0.4, 0.05, 0.034, 0.124, 0.012)]), ACCENT)
    for k in range(5):
        uu = 0.14 + k * 0.058
        b.add(cyl((-0.068, uu, 0.124), (0.068, uu, 0.124), 0.018, seg=12), ENERGY)
        for s in (1, -1):
            b.add(cyl((s * 0.056, uu, 0.124), (s * 0.074, uu, 0.124), 0.021, seg=12), METAL)
    for s in (1, -1):
        panel(b, 0.12, 0.42, -0.06, 0.03, 'x+' if s > 0 else 'x-', s * 0.072, 0.007, 0.012, ACCENT)
    hazard(b, 0.14, 0.4, -0.05, -0.03, 'x+', 0.079)
    b.add(prism([(0.44, -0.075), (0.44, -0.03), (1.0, -0.055), (1.04, -0.07), (1.0, -0.085)], -0.02, 0.02, 0.003), METAL)
    for k in range(5):
        uu = 0.55 + k * 0.1
        b.add(ohollow([(uu, 0.056, 0.056, 0.0, 0.022), (uu + 0.034, 0.056, 0.056, 0.0, 0.022)], 0.014), METAL)
        b.add(ohollow([(uu + 0.004, 0.043, 0.043, 0.0, 0.017), (uu + 0.03, 0.043, 0.043, 0.0, 0.017)], 0.005), ENERGY)
        b.add(box(0.03, 0.03, 0.03, (0, uu + 0.017, -0.06), 0.004), METAL)
        for s in (1, -1):
            stud(b, (s * 0.056, uu + 0.017, 0.0), (s, 0, 0), 0.0048, METAL, True)
    b.add(ol([(0.49, 0.018, 0.018, 0.0, 0.007), (1.09, 0.017, 0.017, 0.0, 0.007)]), ALLOY)
    b.add(nl([(1.08, 0.03, 0.0), (1.1, 0.036, 0.0), (1.24, 0.003, 0.0)], 4, rot=math.pi / 4), METAL)
    for k in range(4):
        b.add(prism([(1.09, 0.0), (1.17, 0.0), (1.09, 0.044)], -0.003, 0.003, 0.0), ENERGY, Matrix.Rotation(-k * math.pi / 2, 4, 'Y'))
    cable(b, [(-0.045, -0.18, 0.1), (-0.06, 0.05, 0.15), (-0.04, 0.12, 0.13)], 0.009, RUBBER, 8, 10, True)
    pts(g, (0, 1.24, 0), grip, butt, (0, 0.2, -0.07))


def jackhammer(g):
    """Jackhammer : bloc moteur à ailettes octogonales, compresseur à manomètre, mandrin hexagonal, foret hélicoïdal."""
    b = g.body
    grip, butt = sleeve(b)
    b.add(ol([(0.06, 0.09, 0.09, 0.0, 0.035), (0.1, 0.095, 0.095, 0.0, 0.038), (0.37, 0.095, 0.095, 0.0, 0.038), (0.43, 0.07, 0.07, 0.0, 0.028)]), PAINT)
    ofins(b, 0.12, 0.35, 10, 0.118, 0.118, 0.0, 0.046, 0.006, METAL)
    b.add(ol([(0.1, 0.06, 0.04, 0.15, 0.015), (0.3, 0.06, 0.04, 0.15, 0.015)]), ACCENT)
    b.add(cyl((0.06, 0.24, 0.15), (0.075, 0.24, 0.15), 0.024, seg=16), METAL)
    b.add(cyl((0.074, 0.24, 0.15), (0.077, 0.24, 0.15), 0.019, seg=16), GLASS)
    b.add(box(0.002, 0.003, 0.016, (0.078, 0.243, 0.153), 0, 1), GLOW)
    hazard(b, 0.13, 0.28, -0.05, 0.05, 'z+', 0.19)
    for s in (1, -1):
        cable(b, [(s * 0.05, 0.12, 0.19), (s * 0.06, 0.05, 0.23), (s * 0.08, -0.05, 0.16), (s * 0.06, -0.15, 0.1)], 0.009, RUBBER, 8, 8, True)
    b.add(nl([(0.43, 0.065, 0.0), (0.5, 0.065, 0.0)], 6, rot=math.pi / 6), METAL)
    b.add(nl([(0.5, 0.05, 0.0), (0.53, 0.042, 0.0)], 12), ALLOY)
    for k in range(6):
        a = k * math.pi / 3
        stud(b, (0.056 * math.cos(a), 0.465, 0.056 * math.sin(a)), (math.cos(a), 0, math.sin(a)), 0.005, METAL, True)
    secs = []
    n = 18
    for k in range(n + 1):
        t = k / n
        u = 0.53 + t * 0.3
        rot = t * 4 * math.pi
        r = 0.032 * (1 - 0.15 * t)
        # Section à quatre lèvres (goujures) tournant le long du foret : hélice.
        secs.append((u, [(r * (0.4 if i % 2 else 1.0) * math.cos(rot + i * math.pi / 4), r * (0.4 if i % 2 else 1.0) * math.sin(rot + i * math.pi / 4))
                         for i in range(8)]))
    secs.append((0.92, [(0.002 * math.cos(i * math.pi / 4), 0.002 * math.sin(i * math.pi / 4)) for i in range(8)]))
    b.add(ploft(secs), ALLOY)
    pts(g, (0, 0.92, 0), grip, butt, (0, 0.2, -0.07))


def spinedriver(g):
    """Spine Driver : rail en U profilé, glissières d'énergie, cadres de bobines, pieu empenné, condensateurs dessous."""
    b = g.body
    grip, butt = sleeve(b)
    out = [(0.072, 0.05), (0.072, -0.04), (0.05, -0.066), (-0.05, -0.066), (-0.072, -0.04), (-0.072, 0.05)]
    inn = [(0.05, 0.05), (0.05, -0.03), (0.036, -0.046), (-0.036, -0.046), (-0.05, -0.03), (-0.05, 0.05)]
    b.add(sweep2(out, inn, 0.06, 0.94, 'u', 1.0, 0.94, (0.0, 0.0)), PAINT)
    for s in (1, -1):
        b.add(box(0.03, 0.86, 0.01, (s * 0.061, 0.5, 0.055), 0.003), METAL)
        b.add(box(0.003, 0.8, 0.024, (s * 0.049, 0.5, 0.01), 0.001, 1), ENERGY)
        hazard(b, 0.12, 0.26, -0.03, 0.0, 'x+' if s > 0 else 'x-', s * 0.072)
        rivets(b, (s * 0.072, 0.3, -0.045), (s * 0.072, 0.9, -0.045), 7, (s, 0, 0), 0.0035)
    for uu in (0.3, 0.55, 0.8):
        b.add(ohollow([(uu, 0.09, 0.09, -0.005, 0.035), (uu + 0.04, 0.09, 0.09, -0.005, 0.035)], 0.016), METAL)
        b.add(ohollow([(uu + 0.005, 0.077, 0.077, -0.005, 0.03), (uu + 0.035, 0.077, 0.077, -0.005, 0.03)], 0.004), ENERGY)
        for s in (1, -1):
            stud(b, (s * 0.09, uu + 0.02, 0.03), (s, 0, 0), 0.005, METAL, True)
    b.add(ol([(0.1, 0.016, 0.016, 0.0, 0.006), (0.98, 0.015, 0.015, 0.0, 0.006)]), ALLOY)
    b.add(nl([(0.97, 0.026, 0.0), (0.99, 0.032, 0.0), (1.11, 0.003, 0.0)], 4, rot=math.pi / 4), METAL)
    for k in range(4):
        b.add(prism([(0.1, 0.0), (0.22, 0.0), (0.12, 0.036), (0.1, 0.036)], -0.002, 0.002, 0.0), METAL, Matrix.Rotation(-(k * math.pi / 2 + math.pi / 4), 4, 'Y'))
    for s in (1, -1):
        x = s * 0.035
        b.add(cyl((x, 0.1, -0.09), (x, 0.5, -0.09), 0.024, seg=12), METAL)
        for uu in (0.16, 0.3, 0.44):
            b.add(ring(0.026, 0.02, uu, uu + 0.012, x, -0.09, 12), ENERGY)
    cable(b, [(0.05, -0.12, 0.08), (0.08, 0.1, 0.08), (0.08, 0.28, 0.05)], 0.008, RUBBER, 8, 8)
    pts(g, (0, 1.11, 0), grip, butt, (0, 0.2, -0.07))


# ───────────────────────── Boucliers ─────────────────────────
# Origine : poignée verticale tenue de la main gauche ; face du bouclier vers +u, plaque à partir de u ≈ 0.12.

def wing_bolt(b, m, c, r=0.0055):
    """Boulon hexagonal posé sur une pièce transformée par m (normale +u de la pièce)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=r, radius2=r, depth=r * 0.8)
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=r * 1.4, radius2=r * 1.4, depth=r * 0.35)
    bmesh.ops.translate(bm, vec=(0, 0, r * 0.3), verts=bm.verts)
    b.add(bm, METAL, m @ Matrix.Translation(P(*c)) @ P(0, 1, 0).to_track_quat('Z', 'Y').to_matrix().to_4x4())


def shield_back(b, pu=0.12, gen_z=0.18, text=None):
    """Dos du bouclier : poignée en D caoutchoutée, montants, coussin d'appui, générateur à ailettes et câbles."""
    b.add(cyl((0, 0, -0.075), (0, 0, 0.075), 0.017, seg=12), RUBBER)
    for sz in (1, -1):
        b.add(ol([(-0.012, 0.018, 0.016, sz * 0.085, 0.006), (pu, 0.018, 0.016, sz * 0.085, 0.006)]), METAL)
        b.add(cyl((0, 0, sz * 0.07), (0, 0, sz * 0.1), 0.021, seg=12), METAL)
        stud(b, (0.018, 0.06, sz * 0.085), (1, 0, 0), 0.004, METAL, True)
    b.add(ol([(pu - 0.03, 0.07, 0.11, 0.0, 0.03, -0.1), (pu, 0.075, 0.12, 0.0, 0.032, -0.1)]), ACCENT)
    b.add(ol([(pu - 0.075, 0.08, 0.05, gen_z, 0.018), (pu - 0.065, 0.085, 0.055, gen_z, 0.02), (pu, 0.085, 0.055, gen_z, 0.02)]), ACCENT)
    for k in range(5):
        b.add(box(0.15, 0.04, 0.004, (0, pu - 0.035, gen_z - 0.03 + k * 0.015), 0.001, 1), METAL)
    leds(b, 'u-', pu - 0.075, -0.03, gen_z + 0.035, 3, 0.012)
    for s in (1, -1):
        cable(b, [(s * 0.07, pu - 0.04, gen_z), (s * 0.14, pu - 0.05, gen_z - 0.05), (s * 0.2, pu - 0.01, gen_z - 0.1)], 0.007, RUBBER, 6, 6)
    if text:
        label(b, text, (0.0, pu - 0.075, gen_z - 0.003), 0.018, 'u-')


def deflection(g):
    """Deflection Shield : trois pans (ailes repliées), plaques rapportées, émetteurs déflecteurs, bande d'alerte, nervures."""
    b = g.body
    shield_back(b, 0.12, 0.2, "DEFLECTION")
    b.add(ploft([(0.12, rectc(-0.17, 0.17, -0.5, 0.5, 0.05)), (0.155, rectc(-0.17, 0.17, -0.5, 0.5, 0.05))]), ACCENT)
    b.add(ploft([(0.155, rectc(-0.15, 0.15, -0.3, 0.3, 0.03)), (0.168, rectc(-0.14, 0.14, -0.29, 0.29, 0.03))]), PAINT)
    b.add(ploft([(0.155, rectc(-0.15, 0.15, -0.48, -0.34, 0.03)), (0.166, rectc(-0.14, 0.14, -0.47, -0.35, 0.03))]), PAINT)
    for s in (1, -1):
        m = Matrix.Translation(P(s * 0.17, 0.155, 0)) @ Matrix.Rotation(s * math.radians(22), 4, 'Z')
        o = rectc(0.0, 0.15, -0.48, 0.48, 0.05) if s > 0 else rectc(-0.15, 0.0, -0.48, 0.48, 0.05)
        b.add(ploft([(-0.035, o), (0.0, o)]), ACCENT, m)
        o = rectc(0.012, 0.13, -0.44, 0.44, 0.03) if s > 0 else rectc(-0.13, -0.012, -0.44, 0.44, 0.03)
        b.add(ploft([(0.0, o), (0.012, o)]), PAINT, m)
        for zz in (-0.3, 0.0, 0.3):
            b.add(box(0.1, 0.004, 0.008, (s * 0.07, 0.013, zz), 0.001, 1), ENERGY, m)
        for (x, zz) in ((0.035, -0.4), (0.11, -0.4), (0.035, 0.4), (0.11, 0.4)):
            wing_bolt(b, m, (s * x, 0.012, zz))
        hinge_m = Matrix.Translation(P(s * 0.17, 0.14, 0))
        for zz in (-0.36, 0.0, 0.36):
            b.add(cyl((0, 0, zz - 0.06), (0, 0, zz + 0.06), 0.014, seg=10), METAL, hinge_m)
    # Émetteurs déflecteurs.
    for zz in (0.36, 0.43):
        b.add(ol([(-0.14, 0.012, 0.012, zz, 0.004, 0.17), (0.14, 0.012, 0.012, zz, 0.004, 0.17)], 'x'), METAL)
        b.add(box(0.26, 0.004, 0.006, (0, 0.183, zz), 0.001, 1), ENERGY)
    for (x, zz) in ((-0.12, -0.27), (0.12, -0.27), (-0.12, 0.27), (0.12, 0.27), (0, -0.27), (0, 0.27)):
        stud(b, (x, 0.168, zz), (0, 1, 0), 0.006, METAL, True)
    hazard(b, -0.13, 0.13, -0.45, -0.37, 'u+', 0.166)
    b.add(face_plate(rectc(-0.06, 0.06, -0.05, 0.05, 0.015), 'u+', 0.168, 0.006), METAL)
    b.add(face_plate(rectc(-0.035, 0.035, -0.025, 0.025, 0.008), 'u+', 0.174, 0.003), ENERGY)
    for zz in (-0.3, 0.3):
        b.add(ol([(-0.16, 0.012, 0.016, zz, 0.005, 0.11), (0.16, 0.012, 0.016, zz, 0.005, 0.11)], 'x'), METAL)
    b.add(ol([(-0.45, 0.016, 0.012, 0.11, 0.005, 0.0), (0.45, 0.016, 0.012, 0.11, 0.005, 0.0)], 'z'), METAL)
    pts(g, (0, 0.18, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def ionmirror(g):
    """Ion Mirror Shield : fond hexagonal, six segments de cadre séparés boulonnés, miroir à facettes, nœuds ioniques."""
    b = g.body
    shield_back(b, 0.12, 0.2, "ION MIRROR")
    hexo = [(0.36 * math.cos(math.pi / 2 + k * math.pi / 3), 0.43 * math.sin(math.pi / 2 + k * math.pi / 3)) for k in range(6)]
    hexi = [(x * 0.74, z * 0.74) for (x, z) in hexo]
    b.add(ploft([(0.12, hexo), (0.148, [(x * 0.99, z * 0.99) for (x, z) in hexo])]), ACCENT)
    for k in range(6):
        j = (k + 1) % 6
        quad = [hexo[k], hexo[j], hexi[j], hexi[k]]
        cx = sum(p[0] for p in quad) / 4
        cz = sum(p[1] for p in quad) / 4
        q = [(cx + (x - cx) * 0.93, cz + (z - cz) * 0.93) for (x, z) in quad]
        b.add(ploft([(0.148, q), (0.172, [(cx + (x - cx) * 0.9, cz + (z - cz) * 0.9) for (x, z) in q])]), PAINT)
        for t in (0.3, 0.7):
            x = hexo[k][0] + (hexo[j][0] - hexo[k][0]) * t
            z = hexo[k][1] + (hexo[j][1] - hexo[k][1]) * t
            stud(b, (cx + (x * 0.87 - cx) * 0.8, 0.172, cz + (z * 0.87 - cz) * 0.8), (0, 1, 0), 0.0055, METAL, True)
        x, z = hexo[k]
        b.add(cyl((x * 0.97, 0.14, z * 0.97), (x * 0.97, 0.18, z * 0.97), 0.036, 0.03, seg=12), METAL)
        b.add(ball(0.022, (x * 0.97, 0.185, z * 0.97), 10), ENERGY)
    b.add(hollow([(0.148, hexi), (0.158, hexi)], [(0.148, [(x * 0.95, z * 0.95) for (x, z) in hexi]), (0.158, [(x * 0.95, z * 0.95) for (x, z) in hexi])]), ENERGY)
    secs = [(0.148, [(x * 0.95, z * 0.95) for (x, z) in hexi]), (0.18, [(x * 0.6, z * 0.6) for (x, z) in hexi]),
            (0.196, [(x * 0.2, z * 0.2) for (x, z) in hexi]), (0.198, [(x * 0.05, z * 0.05) for (x, z) in hexi])]
    b.add(ploft(secs), GLASS)
    for k in range(6):
        x, z = hexi[k]
        b.add(cyl((x * 0.6, 0.181, z * 0.6), (x * 0.2, 0.197, z * 0.2), 0.0025, seg=4), ALLOY)
    pts(g, (0, 0.2, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def tower(g):
    """Tower Shield : très haut, écailles de blindage superposées, fente de visée à visière, rails latéraux, béquilles."""
    b = g.body
    shield_back(b, 0.12, 0.3, "TOWER")
    b.add(ploft([(0.12, rectc(-0.36, 0.36, -0.86, 0.64, 0.07)), (0.15, rectc(-0.36, 0.36, -0.86, 0.64, 0.07))]), ACCENT)
    rows = [(-0.84, -0.5), (-0.52, -0.18), (-0.2, 0.14), (0.12, 0.4)]
    for i, (z0, z1) in enumerate(rows):
        b.add(prism([(0.15, z0), (0.168, z0), (0.19, z1 - 0.02), (0.186, z1), (0.15, z1)], -0.33, 0.33, 0.003), PAINT)
        rivets(b, (-0.29, 0.172, z0 + 0.03), (0.29, 0.172, z0 + 0.03), 7, (0, 1, 0), 0.0055)
    hazard(b, -0.3, 0.3, -0.78, -0.72, 'u+', 0.175)
    # Fente de visée et visière.
    b.add(prism([(0.15, 0.4), (0.16, 0.4), (0.16, 0.62), (0.15, 0.62)], -0.33, 0.33, 0.003), PAINT)
    b.add(box(0.3, 0.012, 0.05, (0, 0.162, 0.5), 0.003), GLASS)
    b.add(prism([(0.15, 0.53), (0.21, 0.545), (0.205, 0.56), (0.15, 0.56)], -0.2, 0.2, 0.003), METAL)
    for x in (-0.2, 0.2):
        b.add(prism([(0.15, 0.45), (0.2, 0.53), (0.2, 0.55), (0.15, 0.55)], x - 0.008, x + 0.008, 0.002), METAL)
    for s in (1, -1):
        b.add(ol([(-0.85, 0.016, 0.03, 0.17, 0.008, s * 0.35), (0.6, 0.016, 0.03, 0.17, 0.008, s * 0.35)], 'z'), METAL)
        rivets(b, (s * 0.35, 0.2, -0.7), (s * 0.35, 0.2, 0.45), 6, (0, 1, 0), 0.006, METAL, True)
        b.add(prism([(0.12, -0.86), (0.2, -0.86), (0.17, -1.0), (0.15, -1.0)], s * 0.2 - 0.015, s * 0.2 + 0.015, 0.003), METAL)
    b.add(ol([(-0.32, 0.012, 0.016, -0.35, 0.005, 0.11), (0.32, 0.012, 0.016, -0.35, 0.005, 0.11)], 'x'), METAL)
    b.add(ol([(-0.32, 0.012, 0.016, 0.2, 0.005, 0.11), (0.32, 0.012, 0.016, 0.2, 0.005, 0.11)], 'x'), METAL)
    pts(g, (0, 0.2, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


def reflector(g):
    """Reflector : disque bombé, centre réfléchissant, huit nervures rayonnantes, couronne boulonnée, émetteur central."""
    b = g.body
    shield_back(b, 0.12, 0.17, "REFLECTOR")
    b.add(nl([(0.12, 0.3, 0.0), (0.135, 0.31, 0.0), (0.16, 0.31, 0.0)], 32), ACCENT)
    b.add(nl([(0.16, 0.285, 0.0), (0.18, 0.25, 0.0), (0.198, 0.19, 0.0)], 32), METAL)
    b.add(nl([(0.196, 0.19, 0.0), (0.21, 0.12, 0.0), (0.216, 0.05, 0.0), (0.217, 0.01, 0.0)], 32), LENS)
    for k in range(12):
        a = k * math.pi / 6
        stud(b, (0.297 * math.cos(a), 0.16, 0.297 * math.sin(a)), (0, 1, 0), 0.0065, METAL, True)
    for k in range(8):
        a = k * math.pi / 4 + math.pi / 8
        rib = prism([(0.162, 0.29), (0.172, 0.29), (0.2, 0.2), (0.205, 0.17), (0.19, 0.17)], -0.009, 0.009, 0.002)
        b.add(rib, ALLOY, Matrix.Rotation(a, 4, 'Y'))
    b.add(nl([(0.212, 0.04, 0.0), (0.226, 0.03, 0.0)], 12), METAL)
    b.add(ball(0.02, (0, 0.226, 0), 10), ENERGY)
    pts(g, (0, 0.22, 0), (0, 0.0, 0), (0, -0.05, 0), (0, 0.0, 0))


# ───────────────────────── Canons automatiques ─────────────────────────

def gun_housing(b, u0, u1, hw, hh, cz=0.0, ch=None, mat=PAINT, vents=True):
    """Boîtier de canon octogonal à arêtes chanfreinées et évents découpés sur les flancs."""
    ch = ch or min(hw, hh) * 0.42
    hb = ol([(u0, hw * 0.88, hh * 0.88, cz, ch * 0.85), (u0 + 0.03, hw, hh, cz, ch), (u1 - 0.04, hw, hh, cz, ch), (u1, hw * 0.9, hh * 0.9, cz, ch * 0.88)])
    if vents:
        L = u1 - u0
        hb = carve(hb, slots(u0 + L * 0.35, u0 + L * 0.7, 5, hw, cz - hh * 0.25, 0.01, hh * 0.5, 0.03) +
                   slots(u0 + L * 0.35, u0 + L * 0.7, 5, -hw, cz - hh * 0.25, 0.01, hh * 0.5, 0.03))
        b.add(ol([(u0 + L * 0.33, hw - 0.012, hh * 0.4, cz - hh * 0.25, 0.01), (u0 + L * 0.72, hw - 0.012, hh * 0.4, cz - hh * 0.25, 0.01)]), POLY)
    b.add(hb, mat)
    # Trappes de visite boulonnées à l'avant des flancs, rivets en bas de caisse.
    L = u1 - u0
    for s in (1, -1):
        pl = 'x+' if s > 0 else 'x-'
        plate_grid(b, u0 + L * 0.74, u1 - 0.05, cz - hh * 0.45, cz + hh * 0.5, pl, s * hw, 1, 2, ACCENT)
        rivets(b, (s * hw, u0 + 0.05, cz - hh + ch + 0.008), (s * hw, u1 - 0.05, cz - hh + ch + 0.008), 9, (s, 0, 0), 0.0035)


def plate_grid(b, p0, p1, q0, q1, plane, off, nu=2, nq=1, mat=ACCENT, t=0.006, gap=0.008):
    """Plaques rapportées en grille (joints entre plaques), boulonnées."""
    du = (p1 - p0 - gap * (nu - 1)) / nu
    dq = (q1 - q0 - gap * (nq - 1)) / nq
    for i in range(nu):
        for j in range(nq):
            a, c = p0 + i * (du + gap), q0 + j * (dq + gap)
            panel(b, a, a + du, c, c + dq, plane, off, t, min(du, dq) * 0.18, mat, True, 0.003)


def carry_handle(b, u0, u1, z, h=0.07):
    cable(b, [(0, u0, z - 0.005), (0, u0 + 0.02, z + h), (0, u1 - 0.02, z + h), (0, u1, z - 0.005)], 0.011, METAL, 10, 6, False, None)
    b.add(cyl((0, u0 + 0.05, z + h + 0.002), (0, u1 - 0.05, z + h + 0.002), 0.016, seg=10), RUBBER)


def gatling(g, flared=False, disruptor=False):
    """FG Hand Gatling : boîtier moteur ventilé à lames, moteur arrière à ailettes, rotor hexagonal, six canons et deux
    colliers, poignée de transport, caisse de munitions latérale et goulotte articulée."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    gun_housing(b, -0.16, 0.32, 0.112, 0.112)
    lamella(b, -0.1, 0.06, 0.112, 0.112, 0.047, 0.0, 0.01, 0.3, ACCENT)
    lamella(b, 0.05, 0.21, 0.112, 0.112, 0.047, 0.0, 0.01, 0.3, ACCENT)
    b.add(nl([(-0.22, 0.05, 0.0), (-0.16, 0.06, 0.0)], 14), METAL)
    for k in range(3):
        b.add(nl([(-0.215 + k * 0.018, 0.075, 0.0), (-0.209 + k * 0.018, 0.075, 0.0)], 14), METAL)
    carry_handle(b, -0.06, 0.2, 0.122)
    hazard(b, -0.12, 0.0, -0.08, -0.04, 'x+', 0.112)
    leds(b, 'x+', 0.112, 0.2, 0.06)
    b.add(nl([(0.32, 0.098, 0.0), (0.36, 0.098, 0.0)], 6, rot=math.pi / 6), METAL)
    for k in range(6):
        a = k * math.pi / 3
        stud(b, (0.07 * math.cos(a + math.pi / 6), 0.36, 0.07 * math.sin(a + math.pi / 6)), (0, 1, 0), 0.006, METAL, True)
    b.add(cyl((0, 0.36, 0), (0, 1.0, 0), 0.022, seg=10), ALLOY)
    for k in range(6):
        a = k * math.pi / 3
        x, z = 0.054 * math.cos(a), 0.054 * math.sin(a)
        b.add(cyl((x, 0.36, z), (x, 1.0, z), 0.0165, seg=10), METAL)
        b.add(cyl((x, 0.96, z), (x, 1.03, z), 0.021, seg=10), METAL)
        b.add(cyl((x, 1.03, z), (x, 1.032, z), 0.011, seg=8), POLY)
    for uu in (0.62, 0.92):
        b.add(nl([(uu, 0.084, 0.0), (uu + 0.028, 0.084, 0.0)], 6), ACCENT)
    # Caisse de munitions et goulotte.
    b.add(ol([(-0.08, 0.06, 0.09, -0.03, 0.02, -0.2), (0.24, 0.06, 0.09, -0.03, 0.02, -0.2)]), ACCENT)
    b.add(ol([(-0.07, 0.064, 0.012, 0.064, 0.005, -0.2), (0.23, 0.064, 0.012, 0.064, 0.005, -0.2)]), PAINT)
    for uu in (0.0, 0.08, 0.16):
        b.add(box(0.006, 0.012, 0.15, (-0.262, uu, -0.03), 0.002), PAINT)
    b.add(box(0.02, 0.02, 0.03, (-0.262, 0.2, 0.04), 0.004), METAL)
    cable(b, [(-0.2, 0.0, 0.078), (-0.2, 0.02, 0.1), (-0.2, 0.14, 0.1), (-0.2, 0.16, 0.078)], 0.006, METAL, 6, 4, False, None)
    links(b, [(-0.18, 0.2, 0.078), (-0.17, 0.27, 0.12), (-0.12, 0.3, 0.1), (-0.1, 0.3, 0.05)], 0.05, 0.018)
    b.add(box(0.04, 0.12, 0.04, (-0.135, 0.06, -0.02), 0.006), METAL)
    stencil_ammo(b, -0.262, 0.08, -0.06)
    pts(g, (0, 1.03, 0), grip, butt, (0, 0.3, -0.12), (-0.2, 0.08, -0.03))


def stencil_ammo(b, x, u, z):
    stencil(b, "7.62x51", u - 0.04, z, x, 0.016, side=-1)


def fgs(g):
    """FGS Diffusion Gatling : carter rond à ailettes, canons divergents à bouches évasées, cloche, tambour sous le flanc."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(nl([(-0.15, 0.1, 0.0), (-0.12, 0.118, 0.0), (0.3, 0.118, 0.0), (0.34, 0.1, 0.0)], 16), PAINT)
    for k in range(6):
        uu = -0.08 + k * 0.05
        b.add(nl([(uu, 0.13, 0.0), (uu + 0.008, 0.13, 0.0)], 16), METAL)
    lamella(b, 0.2, 0.32, 0.1, 0.122, 0.04, 0.0, 0.01, 0.3, ACCENT)
    carry_handle(b, -0.1, 0.16, 0.13)
    b.add(nl([(-0.21, 0.06, 0.0), (-0.15, 0.07, 0.0)], 12), METAL)
    b.add(nl([(0.34, 0.08, 0.0), (0.4, 0.08, 0.0)], 6, rot=math.pi / 6), METAL)
    b.add(cyl((0, 0.4, 0), (0, 0.75, 0), 0.024, seg=10), ALLOY)
    for k in range(6):
        a = k * math.pi / 3
        ca, sa = math.cos(a), math.sin(a)
        b.add(cyl((0.048 * ca, 0.38, 0.048 * sa), (0.09 * ca, 0.82, 0.09 * sa), 0.016, seg=10), METAL)
        b.add(cyl((0.09 * ca, 0.82, 0.09 * sa), (0.096 * ca, 0.88, 0.096 * sa), 0.018, 0.03, seg=10), METAL)
        b.add(cyl((0.096 * ca, 0.879, 0.096 * sa), (0.096 * ca, 0.881, 0.096 * sa), 0.022, seg=10), POLY)
    b.add(nl([(0.5, 0.09, 0.0), (0.525, 0.09, 0.0)], 6), ACCENT)
    b.add(nhollow([(0.74, 0.115, 0.0), (0.8, 0.13, 0.0), (0.84, 0.14, 0.0)], 12, 0.01), PAINT)
    b.add(nhollow([(0.84, 0.146, 0.0), (0.855, 0.146, 0.0)], 12, 0.016), ACCENT)
    b.add(nl([(0.17, 0.11, -0.05, 0.0), (0.27, 0.11, -0.05, 0.0)], 18, 'x'), ACCENT)
    for xx in (0.18, 0.26):
        b.add(nl([(xx, 0.118, -0.05, 0.0), (xx + 0.008, 0.118, -0.05, 0.0)], 18, 'x'), METAL)
    b.add(nl([(0.27, 0.04, -0.05, 0.0), (0.285, 0.035, -0.05, 0.0)], 10, 'x'), METAL)
    hazard(b, -0.1, 0.1, -0.02, 0.02, 'x-', -0.118)
    links(b, [(0.2, 0.0, 0.06), (0.17, 0.0, 0.11), (0.1, 0.05, 0.13)], 0.04, 0.014)
    pts(g, (0, 0.88, 0), grip, butt, (0, 0.3, -0.12), (0.22, 0.0, -0.05))


def disruptor(g):
    """Disruptor : gros boîtier à lames, huit canons, manchon de refroidissement ajouré, gros tambour latéral à nervures."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    gun_housing(b, -0.18, 0.34, 0.135, 0.13)
    lamella(b, -0.12, 0.08, 0.135, 0.13, 0.055, 0.0, 0.012, 0.3, ACCENT)
    lamella(b, 0.07, 0.25, 0.135, 0.13, 0.055, 0.0, 0.012, 0.3, ACCENT)
    carry_handle(b, -0.08, 0.2, 0.142)
    b.add(nl([(-0.24, 0.06, 0.0), (-0.18, 0.075, 0.0)], 14), METAL)
    b.add(nl([(0.34, 0.112, 0.0), (0.4, 0.112, 0.0)], 8, rot=math.pi / 8), METAL)
    b.add(cyl((0, 0.4, 0), (0, 1.0, 0), 0.026, seg=10), ALLOY)
    for k in range(8):
        a = k * math.pi / 4
        x, z = 0.074 * math.cos(a), 0.074 * math.sin(a)
        b.add(cyl((x, 0.4, z), (x, 1.0, z), 0.016, seg=10), METAL)
        b.add(cyl((x, 1.0, z), (x, 1.004, z), 0.012, seg=10), GLOW)
    jacket = nhollow([(0.42, 0.112, 0.0), (0.8, 0.112, 0.0)], 16, 0.01)
    cuts = []
    for uu in (0.47, 0.54, 0.61, 0.68, 0.75):
        cuts += holes_ring(uu, 0.108, 8, 0.013, 0.04)
    b.add(carve(jacket, cuts), PAINT)
    for uu in (0.4, 0.8, 0.96):
        b.add(nhollow([(uu, 0.122, 0.0), (uu + 0.03, 0.122, 0.0)], 16, 0.026), ACCENT)
    b.add(nl([(-0.29, 0.13, -0.03, 0.08), (-0.17, 0.13, -0.03, 0.08)], 22, 'x'), ACCENT)
    for xx in (-0.28, -0.18):
        b.add(nl([(xx, 0.14, -0.03, 0.08), (xx + 0.01, 0.14, -0.03, 0.08)], 22, 'x'), METAL)
    for k in range(8):
        a = k * math.pi / 4
        b.add(box(0.1, 0.012, 0.012, (-0.23, 0.08 + 0.131 * math.cos(a), -0.03 + 0.131 * math.sin(a)), 0.003), METAL)
    b.add(nl([(-0.3, 0.05, -0.03, 0.08), (-0.31, 0.04, -0.03, 0.08)], 10, 'x'), METAL)
    hazard(b, 0.05, 0.25, -0.1, -0.06, 'x+', 0.135)
    links(b, [(-0.2, 0.18, 0.1), (-0.17, 0.26, 0.13), (-0.13, 0.3, 0.1)], 0.05, 0.016)
    pts(g, (0, 1.0, 0), grip, butt, (0, 0.3, -0.12), (-0.23, 0.08, -0.03))


def galleon(g):
    """Galleon : culasse longue à fenêtre d'éjection, ressorts de recul, canon étagé cannelé, gros frein de bouche ajouré,
    chargeur latéral et lunette."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    rec = ol([(-0.15, 0.078, 0.092, 0.0, 0.032), (-0.11, 0.088, 0.106, 0.0, 0.038), (0.36, 0.088, 0.106, 0.0, 0.038), (0.41, 0.074, 0.09, 0.0, 0.03)])
    b.add(carve(rec, [box(0.05, 0.12, 0.05, (0.09, 0.16, 0.02), 0.006)]), PAINT)
    b.add(box(0.03, 0.1, 0.012, (0.07, 0.16, 0.0), 0.002), ALLOY)
    lamella(b, -0.1, 0.1, 0.088, 0.106, 0.038, 0.0, 0.009, 0.25, ACCENT)
    rail(b, 0.0, 0.33, 0.106)
    scope(b, 0.15, 0.18, 0.26, 0.02, 0.03, METAL, 0.12)
    for s in (1, -1):
        b.add(cyl((s * 0.04, 0.36, 0.072), (s * 0.04, 0.62, 0.072), 0.018, seg=12), METAL)
        b.add(cyl((s * 0.04, 0.62, 0.072), (s * 0.04, 0.66, 0.072), 0.012, seg=10), ALLOY)
    b.add(box(0.11, 0.03, 0.04, (0, 0.65, 0.06), 0.006), METAL)
    b.add(nl([(0.41, 0.05, 0.0), (0.58, 0.05, 0.0), (0.6, 0.038, 0.0)], 12), PAINT)
    brl = nl([(0.6, 0.032, 0.0), (1.24, 0.032, 0.0)], 12)
    b.add(carve(brl, [box(0.008, 0.36, 0.008, (0.032 * math.cos(a), 0.92, 0.032 * math.sin(a)), 0.003) for a in (k * math.pi / 3 for k in range(6))]), METAL)
    b.add(nl([(0.86, 0.04, 0.0), (0.9, 0.04, 0.0)], 12), ACCENT)
    mb = ol([(1.22, 0.05, 0.04, 0.0, 0.016), (1.25, 0.062, 0.05, 0.0, 0.02), (1.38, 0.062, 0.05, 0.0, 0.02), (1.4, 0.055, 0.045, 0.0, 0.018)])
    b.add(carve(mb, [box(0.2, 0.026, 0.06, (0, uu, 0.0), 0.004) for uu in (1.28, 1.32, 1.36)]), METAL)
    b.add(ol([(0.02, 0.028, 0.085, -0.04, 0.012, -0.12), (0.22, 0.028, 0.085, -0.04, 0.012, -0.12)]), ACCENT)
    for uu in (0.06, 0.12, 0.18):
        b.add(box(0.006, 0.012, 0.15, (-0.148, uu, -0.04), 0.002), PAINT)
    b.add(box(0.04, 0.16, 0.05, (-0.1, 0.12, -0.02), 0.006), METAL)
    hazard(b, -0.08, 0.06, -0.08, -0.05, 'x+', 0.088)
    pts(g, (0, 1.4, 0), grip, butt, (0, 0.3, -0.12), (-0.12, 0.12, -0.04))


def dexter(g):
    """Dexter : culasse massive, fenêtre d'éjection, canon court sous pare-chaleur hexagonal ajouré, tube magasin, bec de
    bouche et gros tambour à droite."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    rec = ol([(-0.14, 0.09, 0.11, 0.0, 0.04), (-0.1, 0.1, 0.122, 0.0, 0.045), (0.32, 0.1, 0.122, 0.0, 0.045), (0.37, 0.085, 0.1, 0.0, 0.036)])
    b.add(carve(rec, [box(0.05, 0.11, 0.05, (-0.1, 0.16, 0.02), 0.006)]), PAINT)
    lamella(b, -0.08, 0.14, 0.1, 0.122, 0.045, 0.0, 0.01, 0.28, ACCENT)
    carry_handle(b, -0.04, 0.24, 0.132)
    b.add(cyl((0, 0.37, 0.01), (0, 0.86, 0.01), 0.04, seg=16), METAL)
    shield = nhollow([(0.4, 0.068, 0.01), (0.78, 0.068, 0.01)], 6, 0.006, rot=math.pi / 6)
    cuts = []
    for uu in (0.45, 0.52, 0.59, 0.66, 0.73):
        for a in (math.pi / 6 + k * math.pi / 3 for k in range(6)):
            cuts.append(cyl((0.05 * math.cos(a), uu, 0.01 + 0.05 * math.sin(a)), (0.08 * math.cos(a), uu, 0.01 + 0.08 * math.sin(a)), 0.012, seg=8))
    b.add(carve(shield, cuts), PAINT)
    b.add(ol([(0.84, 0.046, 0.046, 0.01, 0.016), (0.86, 0.058, 0.04, 0.01, 0.014), (0.92, 0.062, 0.034, 0.01, 0.012)]), METAL)
    b.add(ol([(0.915, 0.05, 0.022, 0.01, 0.008), (0.922, 0.05, 0.022, 0.01, 0.008)]), POLY)
    b.add(cyl((0, 0.37, -0.075), (0, 0.72, -0.075), 0.026, seg=12), METAL)
    b.add(cyl((0, 0.72, -0.075), (0, 0.74, -0.075), 0.03, seg=12), ACCENT)
    b.add(box(0.03, 0.03, 0.06, (0, 0.6, -0.045), 0.005), METAL)
    b.add(nl([(0.1, 0.13, -0.02, 0.1), (0.2, 0.13, -0.02, 0.1)], 22, 'x'), ACCENT)
    for k in range(10):
        a = k * math.pi / 5
        b.add(cyl((0.2, 0.1, -0.02), (0.2, 0.1 + 0.12 * math.cos(a), -0.02 + 0.12 * math.sin(a)), 0.006, seg=6), METAL)
    b.add(nl([(0.2, 0.05, -0.02, 0.1), (0.215, 0.04, -0.02, 0.1)], 12, 'x'), METAL)
    b.add(box(0.012, 0.05, 0.012, (0.222, 0.1, -0.02), 0.003), ALLOY)
    hazard(b, -0.08, 0.08, -0.08, -0.05, 'x-', -0.1)
    pts(g, (0, 0.92, 0.01), grip, butt, (0, 0.3, -0.12), (0.15, 0.1, -0.02))


def flamerevolver(g):
    """Flame Revolver : carter, barillet octogonal à six buses évasées, carénage à ailettes, veilleuse, deux réservoirs
    cerclés à fonds bombés, vannes, flexibles et manomètre."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    gun_housing(b, -0.14, 0.3, 0.1, 0.1, 0.0, None, PAINT, False)
    lamella(b, -0.08, 0.14, 0.1, 0.1, 0.042, 0.0, 0.01, 0.3, ACCENT)
    b.add(nl([(0.3, 0.1, 0.0), (0.31, 0.112, 0.0), (0.47, 0.112, 0.0), (0.48, 0.1, 0.0)], 8, rot=math.pi / 8), ACCENT)
    for k in range(6):
        a = k * math.pi / 3
        x, z = 0.064 * math.cos(a), 0.064 * math.sin(a)
        b.add(cyl((x, 0.47, z), (x, 0.66, z), 0.021, seg=10), METAL)
        b.add(cyl((x, 0.66, z), (x, 0.7, z), 0.021, 0.028, seg=10), METAL)
        b.add(cyl((x, 0.699, z), (x, 0.701, z), 0.02, seg=10), POLY)
    b.add(nhollow([(0.56, 0.112, 0.0), (0.68, 0.112, 0.0)], 16, 0.008), PAINT)
    for k in range(4):
        b.add(nhollow([(0.575 + k * 0.03, 0.124, 0.0), (0.583 + k * 0.03, 0.124, 0.0)], 16, 0.014), METAL)
    b.add(cyl((0, 0.66, -0.12), (0, 0.72, -0.105), 0.008, seg=8), METAL)
    b.add(ball(0.012, (0, 0.725, -0.104), 8), GLOW)
    for s in (1, -1):
        x = s * 0.155
        b.add(nl([(-0.2, 0.056, -0.08, x), (0.22, 0.056, -0.08, x)], 16), ACCENT)
        b.add(ball(0.056, (x, -0.2, -0.08), 16), ACCENT)
        b.add(ball(0.056, (x, 0.22, -0.08), 16), ACCENT)
        for uu in (-0.1, 0.12):
            b.add(nhollow([(uu, 0.061, -0.08, x), (uu + 0.02, 0.061, -0.08, x)], 16, 0.008), METAL)
            b.add(box(0.02, 0.02, 0.03, (s * 0.115, uu + 0.01, -0.06), 0.004), METAL)
        b.add(cyl((x, 0.24, -0.08), (x, 0.3, -0.08), 0.014, seg=8), METAL)
        b.add(tube_x(0.016, 0.03, (x, 0.29, -0.06), 10), ALLOY)
        cable(b, [(x, 0.3, -0.08), (s * 0.15, 0.36, -0.04), (s * 0.1, 0.4, -0.02), (s * 0.08, 0.44, 0.0)], 0.011, RUBBER, 8, 8, True)
        hazard(b, -0.12, 0.08, -0.098, -0.062, 'x+' if s > 0 else 'x-', s * 0.211)
    b.add(cyl((0.06, 0.2, 0.1), (0.06, 0.2, 0.13), 0.006, seg=6), METAL)
    b.add(cyl((0.06, 0.19, 0.15), (0.06, 0.21, 0.15), 0.022, seg=14), METAL)
    b.add(cyl((0.06, 0.209, 0.15), (0.06, 0.212, 0.15), 0.018, seg=14), GLASS)
    pts(g, (0, 0.7, 0), grip, butt, (0, 0.3, -0.12), (0.155, 0.0, -0.08))


# ───────────────────────── Artillerie ─────────────────────────

def shells(b, x, u, z, n, du=0.05, r=0.02, l=0.12, axis_u=True):
    """Obus de réserve couchés dans un râtelier (douille laiton, ogive)."""
    for k in range(n):
        uu = u + k * du if not axis_u else u
        zz = z if not axis_u else z + k * du
        b.add(cyl((x, uu, zz), (x, uu + l, zz), r, seg=12), ALLOY)
        b.add(cyl((x, uu + l, zz), (x, uu + l + r * 2.2, zz), r * 0.95, r * 0.25, seg=12), METAL)
        b.add(cyl((x, uu - 0.004, zz), (x, uu, zz), r * 1.12, seg=12), ALLOY)


def handcannon(g):
    """NC Hand Cannon : culasse à levier, canon à pans étagé et frettes, cylindres de recul, râtelier d'obus à droite."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.16, 0.09, 0.1, 0.0, 0.04), (-0.12, 0.112, 0.122, 0.0, 0.048), (0.26, 0.112, 0.122, 0.0, 0.048), (0.32, 0.092, 0.1, 0.0, 0.038)]), PAINT)
    lamella(b, -0.1, 0.06, 0.112, 0.122, 0.048, 0.0, 0.01, 0.28, ACCENT)
    lamella(b, 0.05, 0.21, 0.112, 0.122, 0.048, 0.0, 0.01, 0.28, ACCENT)
    b.add(nl([(-0.2, 0.06, 0.0), (-0.16, 0.07, 0.0)], 6, rot=math.pi / 6), METAL)
    b.add(cyl((-0.09, -0.04, 0.05), (-0.17, -0.1, 0.1), 0.012, seg=8), METAL)
    b.add(ball(0.02, (-0.17, -0.1, 0.1), 10), RUBBER)
    b.add(tube_x(0.024, 0.02, (-0.122, -0.04, 0.05), 12), METAL)
    b.add(nl([(0.32, 0.076, 0.0), (0.56, 0.076, 0.0), (0.6, 0.06, 0.0), (1.0, 0.056, 0.0), (1.02, 0.068, 0.0), (1.09, 0.068, 0.0)], 12, rot=math.pi / 12), METAL)
    b.add(cyl((0, 1.0, 0), (0, 1.091, 0), 0.036, seg=12), POLY)
    for uu in (0.64, 0.78, 0.92):
        b.add(nl([(uu, 0.066, 0.0), (uu + 0.03, 0.066, 0.0)], 12, rot=math.pi / 12), ACCENT)
    for s in (1, -1):
        piston(b, (s * 0.045, 0.3, 0.09), (s * 0.045, 0.62, 0.072), 0.014)
    b.add(box(0.14, 0.03, 0.04, (0, 0.63, 0.06), 0.006), METAL)
    b.add(ol([(-0.06, 0.03, 0.04, -0.02, 0.012, 0.142), (0.2, 0.03, 0.04, -0.02, 0.012, 0.142)]), ACCENT)
    shells(b, 0.142, -0.05, 0.035, 1, 0.0, 0.019, 0.17)
    shells(b, 0.142, -0.05, -0.075, 1, 0.0, 0.019, 0.17)
    hazard(b, 0.0, 0.22, -0.09, -0.06, 'x-', -0.112)
    pts(g, (0, 1.09, 0), grip, butt, (0, 0.3, -0.12), (0.142, 0.05, -0.02))


def gallic(g):
    """30mm Gallic : culasse lourde à ailettes, manchon de canon ajouré, vérins de recul, double frein de bouche,
    caisse de bande et goulotte."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.18, 0.1, 0.11, 0.0, 0.042), (-0.14, 0.128, 0.13, 0.0, 0.052), (0.3, 0.128, 0.13, 0.0, 0.052), (0.36, 0.1, 0.105, 0.0, 0.04)]), PAINT)
    ofins(b, -0.12, 0.06, 6, 0.14, 0.142, 0.0, 0.058, 0.007, METAL)
    lamella(b, 0.08, 0.3, 0.128, 0.13, 0.052, 0.0, 0.01, 0.28, ACCENT)
    b.add(nl([(0.36, 0.07, 0.0), (0.42, 0.07, 0.0)], 12), METAL)
    b.add(nl([(0.42, 0.045, 0.0), (1.38, 0.042, 0.0)], 14), METAL)
    jk = nhollow([(0.44, 0.072, 0.0), (0.98, 0.072, 0.0)], 12, 0.008)
    cuts = []
    for uu in (0.5, 0.58, 0.66, 0.74, 0.82, 0.9):
        cuts += holes_ring(uu, 0.07, 6, 0.014, 0.04)
    b.add(carve(jk, cuts), PAINT)
    for uu in (0.42, 0.97):
        b.add(nl([(uu, 0.084, 0.0), (uu + 0.035, 0.084, 0.0)], 12), ACCENT)
    for s in (1, -1):
        piston(b, (s * 0.09, 0.3, 0.06), (s * 0.09, 0.8, 0.04), 0.017)
        b.add(box(0.05, 0.04, 0.04, (s * 0.075, 0.81, 0.04), 0.006), METAL)
    mb = ol([(1.36, 0.06, 0.05, 0.0, 0.02), (1.39, 0.088, 0.066, 0.0, 0.028), (1.56, 0.088, 0.066, 0.0, 0.028), (1.58, 0.078, 0.06, 0.0, 0.025)])
    b.add(carve(mb, [box(0.3, 0.04, 0.08, (0, uu, 0.0), 0.006) for uu in (1.43, 1.51)]), METAL)
    b.add(ol([(-0.1, 0.06, 0.1, -0.04, 0.022, -0.22), (0.2, 0.06, 0.1, -0.04, 0.022, -0.22)]), ACCENT)
    b.add(ol([(-0.09, 0.064, 0.012, 0.064, 0.005, -0.22), (0.19, 0.064, 0.012, 0.064, 0.005, -0.22)]), PAINT)
    for uu in (-0.04, 0.05, 0.14):
        b.add(box(0.006, 0.012, 0.17, (-0.282, uu, -0.04), 0.002), PAINT)
    links(b, [(-0.2, 0.17, 0.078), (-0.18, 0.24, 0.12), (-0.14, 0.26, 0.1), (-0.12, 0.26, 0.05)], 0.06, 0.02)
    hazard(b, -0.1, 0.06, -0.1, -0.07, 'x+', 0.128)
    pts(g, (0, 1.58, 0), grip, butt, (0, 0.3, -0.12), (-0.22, 0.05, -0.04))


def powerdyne(g):
    """Powerdyne : culasse, deux rails conducteurs à faces d'énergie, cinq cadres isolants boulonnés, bancs de
    condensateurs latéraux et câbles d'alimentation."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.15, 0.09, 0.1, 0.0, 0.038), (-0.11, 0.1, 0.112, 0.0, 0.042), (0.28, 0.1, 0.112, 0.0, 0.042), (0.32, 0.085, 0.095, 0.0, 0.035)]), PAINT)
    lamella(b, -0.08, 0.26, 0.1, 0.112, 0.042, 0.0, 0.01, 0.25, ACCENT)
    for sz in (1, -1):
        b.add(ol([(0.25, 0.026, 0.02, sz * 0.05, 0.008), (1.3, 0.026, 0.02, sz * 0.05, 0.008), (1.34, 0.02, 0.016, sz * 0.05, 0.006)]), METAL)
        b.add(box(0.03, 1.05, 0.004, (0, 0.8, sz * 0.03), 0.001, 1), ENERGY)
    for uu in (0.36, 0.6, 0.84, 1.08, 1.28):
        b.add(ohollow([(uu, 0.062, 0.086, 0.0, 0.026), (uu + 0.03, 0.062, 0.086, 0.0, 0.026)], 0.024), ACCENT)
        for s in (1, -1):
            stud(b, (s * 0.062, uu + 0.015, 0.04), (s, 0, 0), 0.005, METAL, True)
            stud(b, (s * 0.062, uu + 0.015, -0.04), (s, 0, 0), 0.005, METAL, True)
    for s in (1, -1):
        for zz in (-0.035, 0.035):
            x = s * 0.135
            b.add(cyl((x, -0.12, zz), (x, 0.26, zz), 0.032, seg=12), METAL)
            for uu in (-0.06, 0.06, 0.18):
                b.add(ring(0.034, 0.028, uu, uu + 0.014, x, zz, 12), ENERGY)
        b.add(box(0.03, 0.34, 0.11, (s * 0.105, 0.07, 0.0), 0.006), ACCENT)
        cable(b, [(s * 0.13, 0.26, 0.035), (s * 0.12, 0.32, 0.08), (s * 0.06, 0.4, 0.1), (s * 0.03, 0.5, 0.09)], 0.008, POLY, 6, 8)
        cable(b, [(s * 0.13, 0.26, -0.035), (s * 0.12, 0.4, -0.08), (s * 0.05, 0.62, -0.09), (s * 0.03, 0.75, -0.08)], 0.008, POLY, 6, 8)
    hazard(b, -0.06, 0.2, -0.02, 0.02, 'z+', 0.112)
    pts(g, (0, 1.34, 0), grip, butt, (0, 0.3, -0.12))


def cannonshot(g):
    """NCS Cannon Shot : culasse trapue, bouche octogonale évasée creuse (âme visible), frettes, poignée, chargeur."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.14, 0.1, 0.11, 0.0, 0.04), (-0.1, 0.12, 0.128, 0.0, 0.05), (0.24, 0.12, 0.128, 0.0, 0.05), (0.29, 0.1, 0.11, 0.0, 0.042)]), PAINT)
    lamella(b, -0.08, 0.2, 0.12, 0.128, 0.05, 0.0, 0.01, 0.28, ACCENT)
    carry_handle(b, -0.04, 0.2, 0.138)
    b.add(ohollow([(0.28, 0.086, 0.086, 0.0, 0.034), (0.5, 0.09, 0.09, 0.0, 0.036), (0.72, 0.112, 0.112, 0.0, 0.045),
                   (0.76, 0.132, 0.132, 0.0, 0.053), (0.82, 0.135, 0.135, 0.0, 0.054)], 0.02), METAL)
    b.add(ol([(0.5, 0.072, 0.072, 0.0, 0.03), (0.52, 0.072, 0.072, 0.0, 0.03)]), POLY)
    for uu, r in ((0.38, 0.098), (0.52, 0.102), (0.66, 0.115)):
        b.add(ohollow([(uu, r, r, 0.0, r * 0.4), (uu + 0.03, r, r, 0.0, r * 0.4)], 0.02), ACCENT)
        for s in (1, -1):
            stud(b, (s * r, uu + 0.015, 0.0), (s, 0, 0), 0.006, METAL, True)
    for k in range(8):
        a = k * math.pi / 4 + math.pi / 8
        b.add(prism([(0.8, 0.12), (0.84, 0.12), (0.84, 0.145), (0.82, 0.145)], -0.006, 0.006, 0.001), METAL, Matrix.Rotation(-a + math.pi / 2, 4, 'Y'))
    b.add(ol([(-0.02, 0.04, 0.09, -0.06, 0.016, -0.16), (0.2, 0.04, 0.09, -0.06, 0.016, -0.16)]), ACCENT)
    b.add(box(0.06, 0.14, 0.05, (-0.12, 0.09, -0.04), 0.006), METAL)
    hazard(b, -0.06, 0.18, -0.09, -0.055, 'x+', 0.12)
    pts(g, (0, 0.84, 0), grip, butt, (0, 0.3, -0.12), (-0.16, 0.09, -0.06))


def mortar_tube(b, r, u0, u1, n=16, z=0.0, x=0.0, mat=PAINT):
    """Tube de mortier creux à pans, bague de bouche, collier de culasse."""
    b.add(nhollow([(u0, r, z, x), (u1, r, z, x)], n, r * 0.14), mat)
    b.add(nhollow([(u1 - 0.05, r * 1.12, z, x), (u1 - 0.01, r * 1.12, z, x), (u1, r * 1.06, z, x)], n, r * 0.24), METAL)
    b.add(nhollow([(u0, r * 1.14, z, x), (u0 + 0.07, r * 1.14, z, x)], n, r * 0.26), METAL)
    b.add(nl([(u0 + 0.1, r * 0.86, z, x), (u0 + 0.12, r * 0.86, z, x)], n), POLY)
    # Lisses de renfort, frette médiane boulonnée, anneau de marquage.
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        b.add(ol([(u0 + 0.075, 0.008, 0.008, z + r * math.sin(a), 0.003, x + r * math.cos(a)),
                  (u1 - 0.055, 0.008, 0.008, z + r * math.sin(a), 0.003, x + r * math.cos(a))]), METAL)
    um = (u0 + u1) / 2
    b.add(nhollow([(um, r * 1.13, z, x), (um + 0.035, r * 1.13, z, x)], n, r * 0.2), ACCENT)
    for k in range(4):
        a = k * math.pi / 2
        stud(b, (x + r * 1.13 * math.cos(a), um + 0.0175, z + r * 1.13 * math.sin(a)), (math.cos(a), 0, math.sin(a)), 0.0045, METAL, True)
    b.add(nhollow([(u1 - 0.1, r * 1.012, z, x), (u1 - 0.08, r * 1.012, z, x)], n, r * 0.05), MARKING)


def mortar_shell(b, x, u, z, r=0.03, l=0.16):
    """Obus de mortier couché (corps, ogive, empennage)."""
    b.add(cyl((x, u, z), (x, u + l * 0.55, z), r * 0.45, r, seg=12), ALLOY)
    b.add(cyl((x, u + l * 0.55, z), (x, u + l, z), r, r * 0.2, seg=12), ACCENT)
    b.add(ring(r * 1.04, r * 0.9, u + l * 0.5, u + l * 0.56, x, z, 12), MARKING)
    for k in range(4):
        a = k * math.pi / 2
        b.add(prism([(u - 0.02, 0.0), (u + 0.03, 0.0), (u + 0.02, r * 0.95), (u - 0.02, r * 0.95)], -0.0015, 0.0015, 0.0), METAL,
              Matrix.Translation(P(x, 0, z)) @ Matrix.Rotation(-a, 4, 'Y'))


def lightmortar(g):
    """FH Light Mortar : socle de culasse, tube creux à collier et tourillons, visée télémétrique, vérin de pointage,
    deux obus de réserve."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.14, 0.09, 0.09, 0.0, 0.036), (-0.1, 0.1, 0.1, 0.0, 0.04), (0.1, 0.1, 0.1, 0.0, 0.04), (0.14, 0.085, 0.085, 0.0, 0.034)]), ACCENT)
    lamella(b, -0.08, 0.1, 0.1, 0.1, 0.04, 0.0, 0.01, 0.28, PAINT)
    mortar_tube(b, 0.07, 0.1, 0.86)
    b.add(nhollow([(0.42, 0.084, 0.0), (0.5, 0.084, 0.0)], 16, 0.014), METAL)
    for s in (1, -1):
        b.add(cyl((s * 0.08, 0.46, 0), (s * 0.115, 0.46, 0), 0.024, seg=12), METAL)
        b.add(cyl((s * 0.115, 0.46, 0), (s * 0.122, 0.46, 0), 0.016, seg=12), ALLOY)
    b.add(box(0.04, 0.05, 0.03, (-0.105, 0.3, 0.035), 0.004), METAL)
    b.add(ol([(0.24, 0.026, 0.028, 0.072, 0.01, -0.13), (0.4, 0.026, 0.028, 0.072, 0.01, -0.13)]), METAL)
    b.add(cyl((-0.13, 0.4, 0.072), (-0.13, 0.405, 0.072), 0.02, seg=14), LENS)
    b.add(cyl((-0.13, 0.215, 0.072), (-0.13, 0.24, 0.072), 0.018, seg=12), RUBBER)
    b.add(tube_x(0.012, 0.012, (-0.162, 0.32, 0.075), 10), METAL)
    piston(b, (0, 0.06, -0.082), (0, 0.4, -0.078), 0.011)
    carry_handle(b, 0.54, 0.74, 0.07, 0.05)
    for zz in (-0.04, 0.035):
        mortar_shell(b, 0.12, 0.0, zz, 0.028, 0.17)
    b.add(box(0.012, 0.18, 0.11, (0.098, 0.08, -0.002), 0.003), METAL)
    hazard(b, -0.08, 0.06, -0.075, -0.05, 'x-', -0.1)
    pts(g, (0, 0.86, 0), grip, butt, (0, 0.3, -0.12))


def heavymortar(g):
    """FHD Heavy Mortar : bloc de culasse à ailettes et trappe de chargement sur charnière, gros tube creux, manchon de
    recul à deux vérins, lunette de pointage."""
    b = g.body
    grip, butt = sleeve(b, -0.38, 0.02, -0.14, 0.09, 0.1)
    b.add(ol([(-0.2, 0.12, 0.12, 0.0, 0.048), (-0.16, 0.14, 0.14, 0.0, 0.056), (0.12, 0.14, 0.14, 0.0, 0.056), (0.17, 0.12, 0.12, 0.0, 0.048)]), ACCENT)
    ofins(b, -0.15, 0.0, 6, 0.152, 0.152, 0.0, 0.062, 0.007, METAL)
    b.add(prism([(0.02, 0.14), (0.14, 0.14), (0.14, 0.155), (0.04, 0.165)], -0.08, 0.08, 0.003), PAINT)
    hinge(b, (0, 0.02, 0.15), 0.17, 0.014)
    b.add(box(0.03, 0.012, 0.02, (0, 0.14, 0.162), 0.003), ALLOY)
    mortar_tube(b, 0.105, 0.12, 1.08, 18)
    b.add(nhollow([(0.15, 0.13, 0.0), (0.48, 0.13, 0.0), (0.5, 0.118, 0.0)], 18, 0.024), METAL)
    for s in (1, -1):
        piston(b, (s * 0.155, 0.12, 0.0), (s * 0.155, 0.6, 0.0), 0.02)
        b.add(box(0.05, 0.04, 0.05, (s * 0.135, 0.6, 0.0), 0.006), METAL)
        b.add(box(0.05, 0.04, 0.05, (s * 0.15, 0.12, 0.0), 0.006), METAL)
    scope(b, 0.2, 0.17, 0.24, 0.019, 0.028, METAL, 0.14)
    b.add(box(0.05, 0.08, 0.02, (0, 0.2, 0.135), 0.004), METAL)
    hazard(b, 0.56, 0.8, -0.03, 0.03, 'z+', 0.105)
    for s in (1, -1):
        hazard(b, -0.12, 0.08, -0.12, -0.08, 'x+' if s > 0 else 'x-', s * 0.14)
    pts(g, (0, 1.08, 0), grip, butt, (0, 0.3, -0.14))


def dispersal(g):
    """YH Dispersal Mortar : quatre tubes creux en carré, trois cadres boulonnés, capot de blindage, culasse commune."""
    b = g.body
    grip, butt = sleeve(b, *SLEEVE_GUN)
    b.add(ol([(-0.16, 0.13, 0.13, 0.0, 0.05), (-0.12, 0.145, 0.145, 0.0, 0.058), (0.14, 0.145, 0.145, 0.0, 0.058), (0.19, 0.13, 0.13, 0.0, 0.05)]), ACCENT)
    lamella(b, -0.1, 0.14, 0.145, 0.145, 0.058, 0.0, 0.012, 0.3, PAINT)
    ofins(b, -0.13, 0.0, 5, 0.156, 0.156, 0.0, 0.064, 0.007, METAL)
    for s in (1, -1):
        plate_grid(b, 0.02, 0.17, -0.06, 0.04, 'x+' if s > 0 else 'x-', s * 0.145, 2, 1, PAINT)
    for (x, z) in ((-0.062, -0.062), (0.062, -0.062), (-0.062, 0.062), (0.062, 0.062)):
        b.add(nhollow([(0.18, 0.054, z, x), (0.86, 0.054, z, x)], 12, 0.008), PAINT)
        b.add(nhollow([(0.82, 0.06, z, x), (0.86, 0.06, z, x)], 12, 0.014), METAL)
        b.add(nl([(0.5, 0.046, z, x), (0.52, 0.046, z, x)], 12), POLY)
    for uu in (0.25, 0.52, 0.76):
        b.add(ohollow([(uu, 0.135, 0.135, 0.0, 0.05), (uu + 0.035, 0.135, 0.135, 0.0, 0.05)], 0.03), ACCENT)
        for s in (1, -1):
            for zz in (-0.06, 0.06):
                stud(b, (s * 0.135, uu + 0.017, zz), (s, 0, 0), 0.005, METAL, True)
    lamella(b, 0.25, 0.8, 0.135, 0.135, 0.05, 0.0, 0.01, 0.15, PAINT, 1.0)
    rivets(b, (-0.08, 0.3, 0.147), (-0.08, 0.75, 0.147), 5, (0, 0, 1), 0.004)
    rivets(b, (0.08, 0.3, 0.147), (0.08, 0.75, 0.147), 5, (0, 0, 1), 0.004)
    for s in (1, -1):
        b.add(face_plate(rectc(0.255, 0.8, -0.095, 0.095, 0.02), 'x+' if s > 0 else 'x-', s * 0.12, 0.014), ACCENT)
        hazard(b, 0.32, 0.5, -0.03, 0.03, 'x+' if s > 0 else 'x-', s * 0.134)
        rivets(b, (s * 0.134, 0.3, -0.075), (s * 0.134, 0.76, -0.075), 6, (s, 0, 0), 0.0035)
        carry = [(s * 0.136, 0.32, -0.08), (s * 0.18, 0.36, -0.08), (s * 0.18, 0.5, -0.08), (s * 0.136, 0.54, -0.08)]
        cable(b, carry, 0.01, METAL, 8, 5, False, None)
    pts(g, (0, 0.86, 0), grip, butt, (0, 0.3, -0.14))


def crusher(g):
    """Super-Heavy Crusher : énorme tube creux à frettes, obus visible dans la bouche, quatre vérins de recul, bloc de
    culasse massif à radiateurs, bandes d'alerte."""
    b = g.body
    grip, butt = sleeve(b, -0.4, 0.0, -0.2, 0.09, 0.1)
    b.add(ol([(-0.24, 0.17, 0.17, 0.0, 0.066), (-0.2, 0.2, 0.2, 0.0, 0.08), (0.1, 0.2, 0.2, 0.0, 0.08), (0.16, 0.18, 0.18, 0.0, 0.072)]), PAINT)
    ofins(b, -0.18, 0.04, 7, 0.214, 0.214, 0.0, 0.086, 0.008, METAL)
    for s in (1, -1):
        hazard(b, 0.05, 0.15, -0.1, 0.1, 'x+' if s > 0 else 'x-', s * 0.2)
    b.add(nhollow([(0.14, 0.17, 0.0), (1.12, 0.17, 0.0)], 20, 0.02), ACCENT)
    b.add(nhollow([(1.06, 0.188, 0.0), (1.12, 0.188, 0.0), (1.13, 0.18, 0.0)], 20, 0.04), METAL)
    for uu in (0.2, 0.45, 0.7, 0.92):
        b.add(nhollow([(uu, 0.186, 0.0), (uu + 0.04, 0.186, 0.0)], 20, 0.03), METAL)
        for k in range(8):
            a = k * math.pi / 4 + math.pi / 8
            stud(b, (0.186 * math.cos(a), uu + 0.02, 0.186 * math.sin(a)), (math.cos(a), 0, math.sin(a)), 0.006, METAL, True)
    b.add(nl([(0.8, 0.148, 0.0), (0.95, 0.148, 0.0)], 20), ALLOY)
    b.add(nl([(0.95, 0.148, 0.0), (1.0, 0.13, 0.0), (1.04, 0.09, 0.0), (1.06, 0.03, 0.0)], 20), METAL)
    b.add(nl([(0.9, 0.151, 0.0), (0.93, 0.151, 0.0)], 20), MARKING)
    for sx in (1, -1):
        for sz in (1, -1):
            a = (sx * 0.15, 0.1, sz * 0.15)
            c = (sx * 0.14, 0.62, sz * 0.14)
            piston(b, a, c, 0.022)
    hazard(b, 0.5, 0.66, -0.08, 0.08, 'z+', 0.17)
    cable(b, [(-0.06, -0.3, -0.08), (-0.1, -0.2, 0.0), (-0.12, -0.12, 0.12), (-0.08, -0.05, 0.2)], 0.012, RUBBER, 8, 10, True)
    pts(g, (0, 1.13, 0), grip, butt, (0, 0.3, -0.2))


# ───────────────────────── Lance-missiles ─────────────────────────

def launcher_base(b, zp=0.15):
    """Fourreau à l'épaule et pylône articulé portant le caisson (repère Butt à l'arrière)."""
    grip, butt = sleeve(b, -0.34, 0.06, -0.1)
    b.add(ol([(-0.16, 0.05, 0.05, 0.03, 0.02), (0.04, 0.05, 0.05, 0.03, 0.02)]), PAINT)
    hinge(b, (0, -0.06, zp - 0.06), 0.12, 0.022)
    piston(b, (0, 0.02, 0.0), (0, 0.12, zp - 0.07), 0.01)
    return grip, butt


def cell_pod(b, cols, rows, cell, u0, u1, z=0.15, x=0.0, mat=PAINT, depth=0.06, margin=0.035, deco=True, ribs=True):
    """Caisson à cellules : coque à pans coupés, alvéoles creusées, bagues et ogives de missiles au fond ; avec deco :
    cadre avant boulonné, lames de blindage sur le dessus, nervures latérales, grille d'échappement arrière."""
    hw = cols * cell / 2 + margin
    hh = rows * cell / 2 + margin
    ch = min(hw, hh) * 0.3
    pod = ol([(u0, hw * 0.92, hh * 0.92, z, ch * 0.9, x), (u0 + 0.04, hw, hh, z, ch, x), (u1 - 0.02, hw, hh, z, ch, x), (u1, hw * 0.97, hh * 0.97, z, ch, x)])
    cuts = []
    centres = []
    for i in range(cols):
        for j in range(rows):
            cx = x + (i - (cols - 1) / 2) * cell
            cz = z + (j - (rows - 1) / 2) * cell
            centres.append((cx, cz))
            cuts.append(cyl((cx, u1 - depth, cz), (cx, u1 + 0.05, cz), cell * 0.4, seg=12))
    b.add(carve(pod, cuts), mat)
    for (cx, cz) in centres:
        b.add(ring(cell * 0.46, cell * 0.4, u1 - 0.004, u1 + 0.006, cx, cz, 12), METAL)
        b.add(cyl((cx, u1 - depth - 0.002, cz), (cx, u1 - depth * 0.35, cz), cell * 0.3, cell * 0.05, seg=10), ALLOY)
        b.add(ring(cell * 0.31, cell * 0.2, u1 - depth - 0.004, u1 - depth + 0.006, cx, cz, 10), ACCENT)
    if deco:
        e = 0.012
        b.add(ohollow([(u1 - 0.035, hw + e, hh + e, z, ch + e * 0.4, x), (u1 + 0.012, hw + e, hh + e, z, ch + e * 0.4, x)], 0.026), METAL)
        for sx in (1, -1):
            for sz in (1, -1):
                stud(b, (x + sx * (hw - 0.002), u1 + 0.012, z + sz * (hh - 0.002)), (0, 1, 0), 0.005, METAL, True)
            pl = 'x+' if sx > 0 else 'x-'
            if ribs:
                step = (u1 - u0 - 0.16) / 3
                for k in range(4):
                    uu = u0 + 0.08 + k * step
                    b.add(face_plate(rectc(uu - 0.008, uu + 0.008, z - hh + ch, z + hh - ch, 0.004), pl, x + sx * hw, 0.01), METAL)
                for k in range(3):
                    uu = u0 + 0.08 + k * step
                    panel(b, uu + 0.018, uu + step - 0.018, z - (hh - ch) * 0.75, z + (hh - ch) * 0.6, pl, x + sx * hw, 0.005, 0.01, ACCENT, True, 0.003)
                if sx < 0:
                    stencil(b, "DANGER", u0 + 0.08 + 2 * step - 0.035, z + (hh - ch) * 0.25, x - hw - 0.005, min(0.022, step * 0.18), side=-1)
        L = (u1 - u0 - 0.08) / 2
        for k in range(2):
            lamella(b, u0 + 0.045 + k * L, u0 + 0.045 + (k + 1) * L + 0.01, hw, hh, ch, z, 0.009, 0.18, ACCENT)
        b.add(face_plate(rectc(x - hw * 0.7, x + hw * 0.7, z - hh * 0.7, z + hh * 0.7, 0.015), 'u-', u0, 0.006), POLY)
        n = max(3, int(hh * 1.4 / 0.03))
        for k in range(n):
            zz = z - hh * 0.6 + k * hh * 1.2 / (n - 1)
            b.add(box(hw * 1.35, 0.01, 0.008, (x, u0 - 0.009, zz), 0.002), METAL)
    return hw, hh


def armhound(g):
    """Arm Hound : caisson 2×2 à pans coupés, volet supérieur relevé sur charnière, rails de transport, fenêtre de
    l'autodirecteur, grille d'échappement arrière."""
    b = g.body
    zp = 0.17
    grip, butt = launcher_base(b, zp)
    hw, hh = cell_pod(b, 2, 2, 0.11, -0.26, 0.42, zp)
    # Pare-soleil avant sur charnière (volet de protection relevé).
    hinge(b, (0, 0.425, zp + hh + 0.018), hw * 1.5, 0.011)
    b.add(prism([(0.42, zp + hh + 0.012), (0.43, zp + hh + 0.01), (0.5, zp + hh - 0.01), (0.5, zp + hh + 0.004), (0.43, zp + hh + 0.026)],
                -hw * 0.8, hw * 0.8, 0.002), ACCENT)
    for s in (1, -1):
        pl = 'x+' if s > 0 else 'x-'
        b.add(box(0.012, 0.44, 0.012, (s * (hw + 0.026), 0.04, zp + hh * 0.75), 0.003), METAL)
        for uu in (-0.15, 0.25):
            b.add(box(0.03, 0.012, 0.012, (s * (hw + 0.01), uu, zp + hh * 0.75), 0.003), METAL)
    hazard(b, -0.2, 0.0, zp - 0.04, zp + 0.0, 'x+', hw + 0.008)
    b.add(face_plate(rectc(0.25, 0.36, -0.06, 0.06, 0.012), 'z+', zp + hh, 0.004), METAL)
    b.add(face_plate(rectc(0.27, 0.34, -0.04, 0.04, 0.008), 'z+', zp + hh + 0.004, 0.003), GLASS)
    b.add(face_plate(rectc(-0.1, 0.1, zp - 0.1, zp + 0.1, 0.02), 'u-', -0.26, 0.006), POLY)
    for k in range(5):
        b.add(face_plate(rectc(-0.085, 0.085, zp - 0.08 + k * 0.04, zp - 0.07 + k * 0.04, 0.003), 'u-', -0.266, 0.006), METAL)
    pts(g, (0, 0.42, zp), grip, butt, (0, 0.2, -0.12))


def bloodstorm(g):
    """Blood Storm : large caisson plat 5×4, déflecteurs de souffle latéraux inclinés, lames de blindage, capot arrière."""
    b = g.body
    zp = 0.16
    grip, butt = launcher_base(b, zp)
    hw, hh = cell_pod(b, 5, 4, 0.058, -0.22, 0.4, zp, 0.0, PAINT, 0.05, 0.03, True, False)
    ch = min(hw, hh) * 0.3
    carry_handle(b, -0.12, 0.2, zp + hh + 0.01, 0.06)
    for s in (1, -1):
        for k in range(4):
            uu = -0.12 + k * 0.12
            b.add(prism([(uu, zp - hh * 0.8), (uu + 0.05, zp - hh * 0.8), (uu + 0.09, zp + hh * 0.8), (uu + 0.04, zp + hh * 0.8)],
                        s * hw, s * (hw + 0.025), 0.002), METAL)
        hazard(b, -0.2, -0.13, zp - 0.06, zp + 0.06, 'x+' if s > 0 else 'x-', s * hw)
    b.add(ol([(-0.27, hw * 0.8, hh * 0.8, zp, ch * 0.7), (-0.22, hw * 0.9, hh * 0.9, zp, ch * 0.8)]), METAL)
    fins_u(b, -0.27, -0.23, 3, hw * 0.7, zp - hh * 0.95, zp - hh * 0.8)
    pts(g, (0, 0.4, zp), grip, butt, (0, 0.2, -0.12))


def haytal(g):
    """Haytal : caisson 5×3 à façade en gradins (rangée du haut la plus avancée), boîtier capteur latéral, poignées."""
    b = g.body
    zp = 0.16
    grip, butt = launcher_base(b, zp)
    cell = 0.075
    for j in range(3):
        cz = zp + (j - 1) * cell
        u1 = 0.32 + j * 0.045
        cell_pod(b, 5, 1, cell, -0.26 + j * 0.02, u1, cz, 0.0, PAINT, 0.055, 0.02, False)
        b.add(ohollow([(u1 - 0.025, 5 * cell / 2 + 0.03, cell / 2 + 0.028, cz, 0.012), (u1 + 0.01, 5 * cell / 2 + 0.03, cell / 2 + 0.028, cz, 0.012)], 0.02), METAL)
    hw = 5 * cell / 2 + 0.02
    lamella(b, -0.2, 0.05, hw, 3 * cell / 2 + 0.02, 0.03, zp, 0.01, 0.12, ACCENT)
    lamella(b, 0.04, 0.3, hw, 3 * cell / 2 + 0.02, 0.03, zp, 0.01, 0.12, ACCENT)
    b.add(face_plate(rectc(-hw * 0.7, hw * 0.7, zp - 0.09, zp + 0.09, 0.015), 'u-', -0.26, 0.006), POLY)
    for k in range(6):
        b.add(box(hw * 1.35, 0.01, 0.008, (0, -0.269, zp - 0.075 + k * 0.03), 0.002), METAL)
    for s in (1, -1):
        pl = 'x+' if s > 0 else 'x-'
        plate_grid(b, -0.2, 0.28, zp - 0.075, zp + 0.075, pl, s * hw, 3, 1, ACCENT)
        cable(b, [(s * (hw + 0.008), -0.1, zp + 0.11), (s * (hw + 0.04), -0.06, zp + 0.13), (s * (hw + 0.04), 0.14, zp + 0.13),
                  (s * (hw + 0.008), 0.18, zp + 0.11)], 0.009, METAL, 8, 5, False, None)
    b.add(ol([(0.0, 0.03, 0.05, zp + 0.02, 0.012, hw + 0.04), (0.2, 0.03, 0.05, zp + 0.02, 0.012, hw + 0.04)]), METAL)
    b.add(cyl((hw + 0.04, 0.2, zp + 0.035), (hw + 0.04, 0.205, zp + 0.035), 0.022, seg=14), LENS)
    b.add(cyl((hw + 0.04, 0.2, zp - 0.012), (hw + 0.04, 0.205, zp - 0.012), 0.008, seg=8), GLOW)
    hazard(b, -0.2, -0.05, zp - 0.04, zp - 0.01, 'x-', -hw - 0.008)
    pts(g, (0, 0.41, zp + cell), grip, butt, (0, 0.2, -0.12))


def leviathan(g):
    """Leviathan : énorme tube à 16 pans, lisses longitudinales, frettes boulonnées, tromblon arrière évasé, missile
    visible à ogive et canards, viseur latéral, poignée."""
    b = g.body
    zp = 0.2
    grip, butt = launcher_base(b, zp)
    b.add(nhollow([(-0.5, 0.165, zp), (0.74, 0.165, zp)], 16, 0.016), PAINT)
    b.add(nhollow([(-0.66, 0.2, zp), (-0.58, 0.18, zp), (-0.5, 0.17, zp)], 16, 0.012), METAL)
    b.add(nhollow([(0.72, 0.18, zp), (0.78, 0.18, zp), (0.79, 0.172, zp)], 16, 0.03), METAL)
    for uu in (-0.42, -0.1, 0.25, 0.55):
        b.add(nhollow([(uu, 0.178, zp), (uu + 0.04, 0.178, zp)], 16, 0.02), ACCENT)
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            stud(b, (0.178 * math.cos(a), uu + 0.02, zp + 0.178 * math.sin(a)), (math.cos(a), 0, math.sin(a)), 0.006, METAL, True)
    for k in range(4):
        a = k * math.pi / 2
        b.add(ol([(-0.5, 0.012, 0.012, zp + 0.168 * math.sin(a), 0.004, 0.168 * math.cos(a)), (0.72, 0.012, 0.012, zp + 0.168 * math.sin(a), 0.004, 0.168 * math.cos(a))]), METAL)
    b.add(nl([(0.4, 0.145, zp), (0.6, 0.145, zp)], 16), ALLOY)
    b.add(nl([(0.6, 0.145, zp), (0.68, 0.13, zp), (0.74, 0.1, zp), (0.79, 0.045, zp), (0.8, 0.012, zp)], 16), ACCENT)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        bm = prism([(0.62, 0.0), (0.68, 0.0), (0.66, 0.025), (0.63, 0.025)], -0.003, 0.003, 0.0)
        b.add(bm, METAL, Matrix.Translation(P(0, 0, zp)) @ Matrix.Rotation(-a, 4, 'Y') @ Matrix.Translation(P(0, 0, 0.13)))
    b.add(ol([(-0.05, 0.03, 0.045, zp + 0.02, 0.012, -0.22), (0.2, 0.03, 0.045, zp + 0.02, 0.012, -0.22)]), METAL)
    b.add(cyl((-0.22, 0.2, zp + 0.03), (-0.22, 0.205, zp + 0.03), 0.022, seg=14), LENS)
    b.add(cyl((-0.22, -0.08, zp + 0.03), (-0.22, -0.05, zp + 0.03), 0.02, seg=12), RUBBER)
    b.add(box(0.06, 0.06, 0.02, (-0.18, 0.08, zp + 0.02), 0.004), METAL)
    carry_handle(b, -0.2, 0.15, zp + 0.17)
    hazard(b, -0.35, -0.2, -0.03, 0.03, 'z+', zp + 0.165)
    stencil(b, "DANGER ARRIERE", -0.45, zp - 0.08, 0.162, 0.024, side=1)
    pts(g, (0, 0.8, zp), grip, butt, (0, 0.2, -0.12))


def arcane(g):
    """Arcane 6-Barrel : six tubes creux rotatifs autour d'un moteur, plaques avant/arrière hexagonales boulonnées,
    carénages latéraux, ogives visibles."""
    b = g.body
    zp = 0.18
    grip, butt = launcher_base(b, zp)
    b.add(nl([(-0.26, 0.05, zp), (0.52, 0.05, zp)], 12), METAL)
    b.add(nl([(-0.34, 0.07, zp), (-0.27, 0.08, zp)], 12), ACCENT)
    for k in range(3):
        b.add(nl([(-0.33 + k * 0.022, 0.09, zp), (-0.325 + k * 0.022, 0.09, zp)], 12), METAL)
    for k in range(6):
        a = k * math.pi / 3
        x, z = 0.1 * math.cos(a), zp + 0.1 * math.sin(a)
        b.add(nhollow([(-0.26, 0.045, z, x), (0.5, 0.045, z, x)], 12, 0.007), METAL)
        b.add(cyl((x, 0.4, z), (x, 0.47, z), 0.034, 0.006, seg=10), ALLOY)
        b.add(nhollow([(0.49, 0.05, z, x), (0.515, 0.05, z, x)], 12, 0.012), ACCENT)
    for uu in (-0.24, 0.12, 0.44):
        hexp = ngon(0.165, 6, zp, 0.0, math.pi / 6)
        b.add(ploft([(uu, hexp), (uu + 0.035, hexp)]), PAINT)
        for k in range(6):
            a = k * math.pi / 3 + math.pi / 6
            stud(b, (0.15 * math.cos(a), uu + 0.035, zp + 0.15 * math.sin(a)), (0, 1, 0), 0.005, METAL, True)
    # Couronne dentée d'entraînement, moteur latéral et lisses de cage entre les tubes.
    b.add(nl([(-0.2, 0.172, zp), (-0.17, 0.172, zp)], 24), ACCENT)
    for k in range(24):
        a = k * math.pi / 12
        b.add(box(0.014, 0.03, 0.014, (0.178 * math.cos(a), -0.185, zp + 0.178 * math.sin(a)), 0.002), METAL, None)
    b.add(ol([(-0.26, 0.04, 0.05, zp - 0.12, 0.016, 0.16), (-0.08, 0.04, 0.05, zp - 0.12, 0.016, 0.16)]), ACCENT)
    hazard(b, -0.22, -0.12, zp - 0.15, zp - 0.1, 'x+', 0.2)
    leds(b, 'x+', 0.2, -0.24, zp - 0.085, 3, 0.012)
    cable(b, [(0.16, -0.08, zp - 0.12), (0.17, 0.0, zp - 0.13), (0.12, 0.08, zp - 0.15), (0.05, 0.1, zp - 0.16)], 0.009, RUBBER, 8, 8, True)
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        b.add(ol([(-0.205, 0.009, 0.009, zp + 0.15 * math.sin(a), 0.003, 0.15 * math.cos(a)), (0.44, 0.009, 0.009, zp + 0.15 * math.sin(a), 0.003, 0.15 * math.cos(a))]), METAL)
    pts(g, (0, 0.52, zp), grip, butt, (0, 0.2, -0.12))


def fgimpact(g):
    """FG High Altitude Impact : caisson de lancement vertical, 3×3 alvéoles ouvertes vers le haut, trappes relevées sur
    charnières, ogives, nervures latérales."""
    b = g.body
    zp = 0.16
    grip, butt = launcher_base(b, zp)
    hw, hh = 0.17, 0.13
    body = ol([(-0.2, hw * 0.94, hh * 0.94, zp, 0.04), (-0.16, hw, hh, zp, 0.045), (0.32, hw, hh, zp, 0.045), (0.36, hw * 0.94, hh * 0.94, zp, 0.04)])
    cuts = []
    for i in range(3):
        for j in range(3):
            cx, cu = (i - 1) * 0.1, 0.08 + (j - 1) * 0.13
            cuts.append(box(0.07, 0.09, 0.12, (cx, cu, zp + hh), 0.008))
    b.add(carve(body, cuts), PAINT)
    for i in range(3):
        for j in range(3):
            cx, cu = (i - 1) * 0.1, 0.08 + (j - 1) * 0.13
            b.add(cyl((cx, cu, zp + hh - 0.06), (cx, cu, zp + hh - 0.01), 0.028, 0.004, seg=10), ALLOY)
            hinge(b, (cx, cu - 0.05, zp + hh + 0.006), 0.07, 0.007, 2)
            b.add(box(0.068, 0.006, 0.088, (cx, cu - 0.058, zp + hh + 0.05), 0.002), ACCENT)
            b.add(box(0.04, 0.004, 0.006, (cx, cu - 0.054, zp + hh + 0.08), 0.001), GLOW)
    for s in (1, -1):
        for k in range(5):
            uu = -0.12 + k * 0.1
            b.add(box(0.012, 0.016, hh * 1.6, (s * (hw + 0.004), uu, zp), 0.003), METAL)
        hazard(b, -0.15, 0.3, zp - hh + 0.03, zp - hh + 0.06, 'x+' if s > 0 else 'x-', s * hw)
    b.add(face_plate(rectc(-0.12, 0.12, zp - 0.08, zp + 0.08, 0.02), 'u+', 0.36, 0.008), ACCENT)
    leds(b, 'u+', 0.368, -0.04, zp, 4)
    pts(g, (0, 0.08, zp + hh + 0.06), grip, butt, (0, 0.2, -0.12))


def phoenix(g):
    """Phoenix : rail de lancement à glissières, missile de croisière (corps, bagues, ogive, ailes médianes, empennage,
    tuyère), colliers de maintien et déflecteur de souffle arrière."""
    b = g.body
    zp = 0.19
    grip, butt = launcher_base(b, zp - 0.06)
    b.add(ol([(-0.32, 0.055, 0.025, zp - 0.1, 0.01), (0.5, 0.055, 0.025, zp - 0.1, 0.01), (0.56, 0.04, 0.018, zp - 0.1, 0.008)]), PAINT)
    for s in (1, -1):
        b.add(box(0.012, 0.82, 0.016, (s * 0.03, 0.09, zp - 0.07), 0.003), METAL)
        hazard(b, -0.28, -0.12, zp - 0.115, zp - 0.085, 'x+' if s > 0 else 'x-', s * 0.055)
    b.add(nl([(-0.26, 0.05, zp), (-0.24, 0.06, zp), (0.48, 0.06, zp)], 14), ALLOY)
    b.add(nl([(0.48, 0.06, zp), (0.56, 0.055, zp), (0.64, 0.04, zp), (0.7, 0.02, zp), (0.72, 0.004, zp)], 14), ACCENT)
    b.add(nl([(-0.3, 0.032, zp), (-0.26, 0.046, zp)], 12), METAL)
    for uu in (0.0, 0.3):
        b.add(nl([(uu, 0.062, zp), (uu + 0.02, 0.062, zp)], 14), MARKING if uu > 0.2 else ACCENT)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        fin = prism([(-0.25, 0.0), (-0.12, 0.0), (-0.18, 0.07), (-0.24, 0.07)], -0.003, 0.003, 0.0)
        b.add(fin, ACCENT, Matrix.Translation(P(0, 0, zp)) @ Matrix.Rotation(-a, 4, 'Y') @ Matrix.Translation(P(0, 0, 0.055)))
        if k % 2 == 0:
            wing = prism([(0.08, 0.0), (0.28, 0.0), (0.24, 0.1), (0.18, 0.1)], -0.003, 0.003, 0.0)
            b.add(wing, ALLOY, Matrix.Translation(P(0, 0, zp)) @ Matrix.Rotation(-a - math.pi / 4, 4, 'Y') @ Matrix.Translation(P(0, 0, 0.055)))
    for uu in (-0.15, 0.2):
        b.add(ol([(uu, 0.07, 0.075, zp - 0.025, 0.02), (uu + 0.04, 0.07, 0.075, zp - 0.025, 0.02)]), METAL)
        b.add(nl([(uu - 0.002, 0.064, zp), (uu + 0.042, 0.064, zp)], 14), POLY)
    b.add(prism([(-0.36, zp - 0.12), (-0.33, zp - 0.12), (-0.38, zp + 0.08), (-0.41, zp + 0.08)], -0.1, 0.1, 0.003), METAL)
    pts(g, (0, 0.72, zp), grip, butt, (0, 0.2, -0.12))


STEEL, ORANGE, DARK = (0.2, 0.22, 0.25), (0.6, 0.33, 0.08), (0.07, 0.075, 0.08)
FAMILIES = {f: globals()[f] for f in [
    "hammer", "vulcan", "blade", "forceax", "roller", "haken", "blasthole", "twinspear", "flashing", "jackhammer", "spinedriver",
    "deflection", "ionmirror", "tower", "reflector", "gatling", "fgs", "disruptor", "galleon", "dexter", "flamerevolver",
    "handcannon", "gallic", "powerdyne", "cannonshot", "lightmortar", "heavymortar", "dispersal", "crusher",
    "armhound", "bloodstorm", "haytal", "leviathan", "arcane", "fgimpact", "phoenix"]}
PALETTE = {f: (STEEL, DARK) for f in FAMILIES}
PALETTE.update({"vulcan": (STEEL, ORANGE), "crusher": (STEEL, ORANGE), "leviathan": ((0.32, 0.3, 0.22), DARK),
                "ionmirror": ((0.5, 0.52, 0.56), DARK), "reflector": ((0.55, 0.57, 0.6), ORANGE), "tower": ((0.24, 0.26, 0.2), DARK),
                "flamerevolver": (STEEL, (0.45, 0.12, 0.06)), "disruptor": ((0.14, 0.15, 0.17), ORANGE)})
LABELS = {"hammer": "VIBRO HAMMER", "vulcan": "VULCAN HAMMER", "blade": "FORCE BLADE", "forceax": "FORCE AX", "roller": "VIBRO ROLLER",
          "haken": "VIBRO HAKEN", "blasthole": "BLASTHOLE SPEAR", "twinspear": "BLAST TWIN SPEARS", "flashing": "FLASHING SPEAR",
          "jackhammer": "JACKHAMMER", "spinedriver": "SPINE DRIVER", "gatling": "FG7 HAND GATLING", "fgs": "FGS DIFFUSION",
          "disruptor": "DISRUPTOR", "galleon": "GALLEON", "dexter": "DEXTER", "flamerevolver": "FLAME REVOLVER",
          "handcannon": "NC101", "gallic": "30MM GALLIC", "powerdyne": "POWERDYNE", "cannonshot": "NCS1 CANNON SHOT",
          "lightmortar": "FH10", "heavymortar": "FHD1", "dispersal": "YH1 DISPERSAL", "crusher": "CRUSHER",
          "armhound": "ARM HOUND", "bloodstorm": "BLOOD STORM", "haytal": "HAYTAL", "leviathan": "LEVIATHAN",
          "arcane": "ARCANE 6", "fgimpact": "FG01 HAI", "phoenix": "PHOENIX"}
# Marquages : sur le fourreau (plaque latérale) par défaut ; boucliers : inscription au dos (pas de flanc).
MARK_AT = {f: None for f in ("deflection", "ionmirror", "tower", "reflector")}
for _f in ("hammer", "vulcan", "blade", "forceax", "roller", "haken", "blasthole", "twinspear", "flashing", "jackhammer", "spinedriver"):
    MARK_AT[_f] = (-0.12, -0.05)
for _f in ("gatling", "fgs", "disruptor", "galleon", "dexter", "flamerevolver", "handcannon", "gallic", "powerdyne", "cannonshot",
           "lightmortar", "dispersal"):
    MARK_AT[_f] = (-0.16, -0.15)
MARK_AT.update({"heavymortar": (-0.18, -0.17), "crusher": (-0.2, -0.23)})
for _f in ("armhound", "bloodstorm", "haytal", "leviathan", "arcane", "fgimpact", "phoenix"):
    MARK_AT[_f] = (-0.14, -0.13)


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
