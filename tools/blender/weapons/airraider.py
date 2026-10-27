# Air Raider (EDF 5) : objets tenus (FAMILIES → assets/models/weapons/<nom>.glb) et objets du monde (OBJECTS →
# assets/models/airraid/<nom>.glb). Langage commun : matériel militaire sable et kaki (Paint), pièces sombres (Accent), métal,
# diodes. Inspirations réelles :
#   fumigène = grenade fumigène M18 (corps roulé, allumeur à cuillère, goupille et anneau) ;
#   désignateur = désignateur laser AN/PEQ-1 SOFLAM (boîtier binoculaire, émetteur central, œilletons, molette, batterie) ;
#   Limpet Gun = lance-grenades compact à canon creux (bombe collante visible dans la bouche) ; Limpet Sniper = long canon
#   cannelé, frein de bouche, lunette, bipied ; Limpet Splendor = barillet à six chambres type MGL ; Limpet Chain Gun = arme
#   à bande type Mk 19 (manchon perforé, couvercle d'alimentation, caisse de bande latérale) ; Limpet Detector = lanceur à
#   tête de détection (antenne parabolique, écran) ;
#   caisses : valise Pelican de tourelle (coins renforcés, charnières, loquets, trépied sanglé), caisse-cage de robot (cadre
#   alu, grilles), conteneur cylindrique de poste, caisse à munitions géante (déployeur d'abris et leurres) ;
#   pistolet de soutien = émetteur à bobines et cartouche d'énergie ; canon de répression = gros lance-projectiles à pompe.
# Monde : tourelles ZE sur trépied télescopique (socle « Base », tête orientable « Head » portant « Muzzle ») : ZE-GUN
# (mitrailleuse lourde à manchon), ZE Launcher (caisson à six tubes), ZE Sniper (canon long, bouclier), ZE Blaster (rotatif) ;
# robot bombe à chenilles, Assault Beetle (insecte sauteur à six pattes), Patroller (robot à six roues et tourelle), Speed Star
# (monoroue) ; postes de soin / puissance / protection (mât sur pieds, tête propre à chacun) ; aérostat leurre captif ;
# canonnière AC-130, chasseur-bombardier KM6 (type F-15E), bombardier d'attaque Kamuy (type A-10), bombardier lourd Vesta
# (type B-52), bombardier stratégique Phobos (aile volante type B-2 géante).
import math

K = globals()


# ───────────────────────── Outils propres au module ─────────────────────────

def ball(r, c, seg=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def ellipsoid(rx, ru, rz, c, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=1.0)
    bmesh.ops.scale(bm, vec=(rx, ru, rz), verts=bm.verts)
    bmesh.ops.translate(bm, vec=P(*c), verts=bm.verts)
    return bm


def pts(g, muzzle, grip, butt, sup, mag=(0, 0.0, -0.06), vertical=False):
    g.point("Muzzle", muzzle); g.point("Grip", grip); g.point("SupportV" if vertical else "Support", sup)
    g.point("Butt", butt); g.point("Mag", mag)


def bridge(loops, caps=True, closed=False):
    """Surface passant par des boucles de points (repère de travail) ; closed : la dernière rejoint la première."""
    bm = bmesh.new()
    vl = [[bm.verts.new(P(*p)) for p in loop] for loop in loops]
    n = len(vl[0])
    rng = range(len(vl)) if closed else range(len(vl) - 1)
    for k in rng:
        a, c = vl[k], vl[(k + 1) % len(vl)]
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], c[j], c[i]))
    if caps and not closed:
        bm.faces.new(list(reversed(vl[0])))
        bm.faces.new(vl[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _se(hw, hh, p, seg, a0=0.0):
    out = []
    for i in range(seg):
        a = a0 + 2 * math.pi * i / seg
        ca, sa = math.cos(a), math.sin(a)
        out.append((hw * math.copysign(abs(ca) ** (2 / p), ca), hh * math.copysign(abs(sa) ** (2 / p), sa)))
    return out


def lofx(sections, seg=20, caps=True):
    """Loft le long de u, sections (u, cx, cz, demi-largeur x, demi-hauteur z, exposant)."""
    return bridge([[(cx + a, u, cz + b) for (a, b) in _se(hw, hh, p, seg)] for (u, cx, cz, hw, hh, p) in sections], caps)


def lofz(sections, seg=20, caps=True):
    """Loft vertical, sections (z, cx, cu, demi-largeur x, demi-profondeur u, exposant)."""
    return bridge([[(cx + a, cu + b, z) for (a, b) in _se(hw, hu, p, seg)] for (z, cx, cu, hw, hu, p) in sections], caps)


def lofa(sections, seg=16, caps=True):
    """Loft le long de x, sections (x, cu, cz, demi-longueur u, demi-hauteur z, exposant)."""
    return bridge([[(x, cu + a, cz + b) for (a, b) in _se(hu, hz, p, seg)] for (x, cu, cz, hu, hz, p) in sections], caps)


def rod(a, b, r0, r1=None, seg=12, caps=True):
    """Cylindre (cône) entre deux points du repère de travail."""
    r1 = r0 if r1 is None else r1
    A, B = P(*a), P(*b)
    d = B - A
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=max(d.length, 1e-5))
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(A + B) / 2, verts=bm.verts)
    return bm


def tz(r0, r1, z0, z1, x=0.0, u=0.0, seg=20):
    """Cylindre vertical de z0 à z1."""
    return rod((x, u, z0), (x, u, z1), r0, r1, seg)


def torus(R, r, c, axis='x', seg=24, rseg=8):
    """Tore de rayon R (tube r) autour de l'axe 'x', 'u' ou 'z' passant par c."""
    loops = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ca, sa = math.cos(a), math.sin(a)
        loop = []
        for j in range(rseg):
            b = 2 * math.pi * j / rseg
            rr = R + r * math.cos(b)
            h = r * math.sin(b)
            if axis == 'x':
                p = (h, rr * ca, rr * sa)
            elif axis == 'u':
                p = (rr * ca, h, rr * sa)
            else:
                p = (rr * ca, rr * sa, h)
            loop.append((c[0] + p[0], c[1] + p[1], c[2] + p[2]))
        loops.append(loop)
    return bridge(loops, caps=False, closed=True)


def xf(bm, at=(0, 0, 0), rx=0.0, ru=0.0, rz=0.0, pivot=(0, 0, 0)):
    """Rotation (degrés) autour de x, u, z du repère de travail autour de pivot, puis translation de at."""
    piv = P(*pivot)
    bmesh.ops.translate(bm, vec=-piv, verts=bm.verts)
    # Repère de travail → Blender : u = −Y ; une rotation de +a autour de u est une rotation de −a autour de Y.
    m = Matrix.Rotation(math.radians(rz), 3, 'Z') @ Matrix.Rotation(math.radians(-ru), 3, 'Y') @ Matrix.Rotation(math.radians(rx), 3, 'X')
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=m, verts=bm.verts)
    bmesh.ops.translate(bm, vec=piv + Vector((at[0], -at[1], at[2])), verts=bm.verts)
    return bm


def _text_bm(text, h, depth):
    cu = bpy.data.curves.new("txt", 'FONT')
    cu.body = text
    cu.size = h
    cu.extrude = depth / 2
    cu.align_x = 'CENTER'
    ob = bpy.data.objects.new("txt", cu)
    bpy.context.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    return bm


def text(part, s, h, place, mat=MARKING, depth=None):
    """Texte en relief : place(tx, ty, tz) → (x, u, z) du repère de travail (tx : sens de lecture, ty : haut, tz : relief)."""
    bm = _text_bm(s, h, depth if depth is not None else h * 0.04)
    for v in bm.verts:
        v.co = P(*place(v.co.x, v.co.y, v.co.z))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    part.add(bm, mat)


def flank_text(part, s, h, u, z, x, side=1, mat=MARKING):
    """Texte sur un flanc plat (normale ±x), centré en (u, z), lisible de l'extérieur."""
    text(part, s, h, lambda a, b, c: (x + side * (c + h * 0.03), u + side * a, z + b - h * 0.35), mat)


def top_text(part, s, h, u, x, z, mat=MARKING, along_u=True):
    """Texte sur une face horizontale (normale +z), lisible de dessus."""
    if along_u:
        text(part, s, h, lambda a, b, c: (x - b + h * 0.35, u + a, z + c + h * 0.03), mat)
    else:
        text(part, s, h, lambda a, b, c: (x + a, u + b - h * 0.35, z + c + h * 0.03), mat)


def knurl(part, r, u0, u1, x=0.0, z=0.0, n=16, mat=METAL, d=0.0012):
    """Molette / bague moletée le long de u : nervures longitudinales."""
    part.add(tube(r, r, u0, u1, x, z, max(12, n)), mat)
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        part.add(box(d * 1.6, (u1 - u0) * 0.9, d * 1.6, (x + math.cos(a) * r, (u0 + u1) / 2, z + math.sin(a) * r), 0, 1), mat)


def knurl_x(part, r, w, c, n=16, mat=METAL, d=0.0012):
    """Molette d'axe x (mise au point, réglages)."""
    part.add(tube_x(r, w, c, max(12, n)), mat)
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        part.add(box(w * 0.9, d * 1.6, d * 1.6, (c[0], c[1] + math.cos(a) * r, c[2] + math.sin(a) * r), 0, 1), mat)


def rivets(part, points, r=0.003, side=1, mat=METAL, axis='x'):
    for p in points:
        if axis == 'x':
            part.add(xf(ball(r, (0, 0, 0), 8), at=(p[0], p[1], p[2])), mat)
        else:
            part.add(ball(r, p, 8), mat)


def limpet(part, u, z=0.0, r=0.02, x=0.0):
    """Bombe collante (vers +u) : galette à coupole, couronne d'aimants/griffes, diode."""
    part.add(lofx([(u - r * 0.9, x, z, r * 0.7, r * 0.7, 2), (u - r * 0.6, x, z, r * 0.95, r * 0.95, 2),
                   (u + r * 0.1, x, z, r, r, 2), (u + r * 0.4, x, z, r * 0.8, r * 0.8, 2), (u + r * 0.62, x, z, r * 0.35, r * 0.35, 2)], 16),
             ACCENT)
    part.add(ring(r * 1.04, r * 0.7, u - r * 0.15, u + r * 0.05, x, z, 16), METAL)
    for k in range(6):
        a = 2 * math.pi * k / 6
        part.add(box(r * 0.22, r * 0.5, r * 0.22, (x + math.cos(a) * r * 0.86, u + r * 0.45, z + math.sin(a) * r * 0.86), 0, 1), METAL)
    part.add(tube(r * 0.22, r * 0.18, u + r * 0.6, u + r * 0.72, x, z, 10), GLOW)


def hollow_barrel(part, ro, ri, u0, u1, z=0.0, mat=METAL, seg=22):
    """Canon de lance-grenades à âme visible : tube creux + fond d'âme sombre."""
    part.add(ring(ro, ri, u0, u1, 0, z, seg), mat)
    part.add(tube(ri, ri, u0, u0 + 0.004, 0, z, seg), POLY)


# ───────────────────────── Objets tenus ─────────────────────────

def smokecan(g):
    """Grenade fumigène de marquage M18 tenue debout dans la main : corps roulé, bourrelets, évents du dessus, allumeur à
    cuillère, goupille et anneau, bande de couleur et inscriptions enroulées."""
    b = g.body
    r, zb, zt = 0.031, -0.135, -0.008
    b.add(tz(r, r, zb + 0.005, zt - 0.005, 0, 0, 28), PAINT)
    for z0 in (zb, zt - 0.006):                                                  # bourrelets roulés haut et bas
        b.add(lofz([(z0, 0, 0, r * 0.97, r * 0.97, 2), (z0 + 0.003, 0, 0, r + 0.0018, r + 0.0018, 2), (z0 + 0.006, 0, 0, r * 0.98, r * 0.98, 2)], 28), METAL)
    for z0 in (zb + 0.03, zb + 0.06):                                             # moulures de renfort
        b.add(ring_z(r + 0.0009, r - 0.004, z0, z0 + 0.0025), PAINT)
    b.add(ring_z(r + 0.0011, r - 0.004, zt - 0.03, zt - 0.016), GLOW)              # bande de couleur de fumée
    # Couvercle à quatre évents (découpés) autour de l'allumeur.
    lid = tz(r * 0.97, r * 0.9, zt - 0.003, zt + 0.004, 0, 0, 28)
    lid = carve(lid, [tz(0.0055, 0.0055, zt - 0.01, zt + 0.01, math.cos(a) * 0.02, math.sin(a) * 0.02, 12)
                      for a in (math.pi / 4 + k * math.pi / 2 for k in range(4))])
    b.add(lid, METAL)
    # Allumeur : corps fileté, tête, cuillère qui descend le long du corps, goupille et anneau.
    b.add(tz(0.0115, 0.0115, zt + 0.003, zt + 0.014, 0, 0, 18), METAL)
    for k in range(3):
        b.add(tz(0.0125, 0.0125, zt + 0.004 + k * 0.0035, zt + 0.0055 + k * 0.0035, 0, 0, 18), METAL)
    b.add(lofz([(zt + 0.014, 0, 0, 0.01, 0.01, 2), (zt + 0.03, 0, 0.002, 0.009, 0.011, 3), (zt + 0.038, 0, 0.0, 0.007, 0.008, 2)], 18), METAL)
    spoon = [(0.006, zt + 0.04), (0.02, zt + 0.036), (r + 0.004, zt + 0.018), (r + 0.0045, zt - 0.072),
             (r + 0.0015, zt - 0.074), (r + 0.0015, zt + 0.014), (0.019, zt + 0.032), (0.006, zt + 0.0365)]
    b.add(prism(spoon, -0.0065, 0.0065, 0.0008), ALLOY)
    b.add(box(0.016, 0.004, 0.012, (0, r + 0.003, zt - 0.075), 0.001), ALLOY)      # bout recourbé de la cuillère
    b.add(tube_x(0.0018, 0.05, (0.014, 0.004, zt + 0.026), 8), METAL)             # goupille
    b.add(torus(0.013, 0.0018, (0.04, 0.004, zt + 0.018), 'x', 18, 6), METAL)     # anneau d'arrachage
    # Inscriptions enroulées sur le corps (de face) et bande jaune de marquage.
    text(b, "SMOKE", 0.0105, lambda a, bb, c: _cyl_z(r, 0.0, a, bb, c, zt - 0.06))
    text(b, "RED  M18", 0.0065, lambda a, bb, c: _cyl_z(r, 0.0, a, bb, c, zt - 0.08))
    text(b, "LOT 4471-B", 0.005, lambda a, bb, c: _cyl_z(r, math.pi, a, bb, c, zt - 0.1))
    b.add(ring_z(r + 0.0008, r - 0.004, zb + 0.012, zb + 0.016), MARKING)
    pts(g, (0, 0.0, zt + 0.04), (0, 0.0, -0.07), (0, -0.05, -0.07), (0, 0.04, -0.07), (0, 0.0, -0.07))


def ring_z(ro, ri, z0, z1, x=0.0, u=0.0, seg=28):
    bm = ring(ro, ri, 0.0, z1 - z0, 0, 0, seg)
    return xf(bm, at=(x, u, z0), rx=-90)


def _cyl_z(r, a0, tx, ty, tz_, z0):
    """Lettre posée sur un cylindre vertical de rayon r, centrée à l'angle a0 (0 = flanc +x, lecture vers +u)."""
    a = a0 + tx / r
    rr = r + 0.0004 + tz_
    return (math.cos(a) * rr, math.sin(a) * rr, z0 + ty)


def designator(g):
    """Désignateur laser type SOFLAM : boîtier binoculaire à pans coupés, deux objectifs à pare-soleil, émetteur laser
    central, œilletons caoutchouc, molette de mise au point, boutons, écran, logement de pile, rail, poignée et antenne."""
    b = g.body
    zc = 0.03
    # Boîtier principal : section en superellipse, avant et arrière chanfreinés.
    b.add(lofx([(-0.075, 0, zc, 0.055, 0.04, 3.2), (-0.065, 0, zc, 0.064, 0.048, 3.6), (0.15, 0, zc, 0.066, 0.05, 3.6),
                (0.17, 0, zc, 0.06, 0.046, 3.4), (0.178, 0, zc, 0.054, 0.04, 3.0)], 32), PAINT)
    # Nervures de prise caoutchoutées sur les flancs.
    for s in (-1, 1):
        b.add(lofx([(-0.04, s * 0.064, zc - 0.004, 0.006, 0.032, 4), (0.11, s * 0.064, zc - 0.004, 0.006, 0.032, 4)], 16), RUBBER)
        serrations(b, -0.035, 0.105, zc - 0.03, zc + 0.022, s * 0.069, 14, s, RUBBER, 0.0015)
    # Objectifs (pare-soleil, bague de réglage moletée, verres).
    for x in (-0.032, 0.032):
        b.add(tube(0.025, 0.026, 0.17, 0.2, x, zc + 0.012, 24), METAL)
        knurl(b, 0.027, 0.178, 0.19, x, zc + 0.012, 20, POLY)
        b.add(ring(0.026, 0.021, 0.2, 0.218, x, zc + 0.012, 24), POLY)            # pare-soleil
        b.add(tube(0.021, 0.021, 0.198, 0.2, x, zc + 0.012, 24), LENS)
    # Émetteur laser central (plus bas) avec diode.
    b.add(tube(0.017, 0.018, 0.17, 0.205, 0, zc - 0.03, 20), METAL)
    b.add(ring(0.019, 0.013, 0.2, 0.212, 0, zc - 0.03, 20), POLY)
    b.add(tube(0.012, 0.012, 0.2, 0.202, 0, zc - 0.03, 16), GLOW)
    # Œilletons caoutchouc à l'arrière, molette de mise au point entre eux.
    for x in (-0.032, 0.032):
        b.add(tube(0.016, 0.018, -0.092, -0.075, x, zc + 0.012, 20), METAL)
        b.add(lofx([(-0.118, x, zc + 0.012, 0.02, 0.02, 2), (-0.11, x, zc + 0.012, 0.022, 0.022, 2), (-0.092, x, zc + 0.012, 0.018, 0.018, 2)], 20), RUBBER)
        b.add(ring(0.0205, 0.011, -0.119, -0.116, x, zc + 0.012, 20), RUBBER)
        b.add(tube(0.011, 0.011, -0.112, -0.11, x, zc + 0.012, 16), LENS)
    knurl_x(b, 0.014, 0.022, (0, -0.07, zc + 0.05), 18, POLY)
    # Dessus : rail, boutons de tir et de mode, sélecteur rotatif, plaque signalétique.
    rail(b, -0.02, 0.12, zc + 0.05, 0, 0.024)
    for k, uu in enumerate((-0.05, -0.035)):
        b.add(tz(0.006, 0.0055, zc + 0.046, zc + 0.054, -0.04, uu, 14), RUBBER if k else ACCENT)
    b.add(tz(0.011, 0.01, zc + 0.046, zc + 0.058, 0.04, -0.04, 18), POLY)
    b.add(box(0.004, 0.012, 0.004, (0.04, -0.04, zc + 0.06), 0.001), METAL)
    # Côté gauche : petit écran et clavier ; côté droit : logement de pile à bouchon moleté.
    b.add(box(0.004, 0.05, 0.026, (-0.068, 0.05, zc + 0.022), 0.002), POLY)
    b.add(box(0.002, 0.04, 0.018, (-0.0705, 0.05, zc + 0.022), 0.0008), ENERGY)
    for k in range(3):
        b.add(box(0.004, 0.008, 0.006, (-0.0705, 0.03 + k * 0.016, zc - 0.002), 0.0012), RUBBER)
    b.add(tube_x(0.021, 0.02, (0.075, 0.06, zc - 0.012), 22), METAL)
    knurl_x(b, 0.022, 0.012, (0.09, 0.06, zc - 0.012), 22, POLY)
    for uu in (-0.055, 0.14):
        for zz in (zc - 0.03, zc + 0.03):
            screw(b, uu, zz, 0.066, 0.0026, 1)
            screw(b, uu, zz, -0.066, 0.0026, -1)
    # Antenne de liaison (embase, fouet) et poignée pistolet avec détente de tir laser.
    b.add(tz(0.006, 0.005, zc + 0.04, zc + 0.06, -0.05, -0.06, 12), METAL)
    b.add(rod((-0.05, -0.06, zc + 0.06), (-0.05, -0.085, zc + 0.2), 0.0022, 0.0015, 8), POLY)
    b.add(ball(0.004, (-0.05, -0.085, zc + 0.2), 8), POLY)
    b.add(box(0.05, 0.08, 0.012, (0, 0.0, -0.022), 0.003), METAL)                 # semelle de poignée
    grip = pistol_grip(b, 0.01, -0.028, POLY, 12, 0.095, 0.032, 0.046)
    b.add(box(0.005, 0.004, 0.018, (0, 0.035, -0.04), 0.001), ACCENT)            # détente
    b.add(tube_x(0.005, 0.07, (0, 0.13, -0.02), 10), METAL)                      # anneau de dragonne
    pts(g, (0, 0.212, zc - 0.03), grip, (0, -0.12, zc), (0, 0.12, -0.02))


# ── Lance-bombes collantes ──

def _launcher_lower(b, u_grip, z_top, mag=True, mag_u=0.06, mag_h=0.1, mag_w=0.05, mag_d=0.06):
    """Carcasse inférieure : poignée, pontet, sélecteur, puits et chargeur à bombes (nervuré)."""
    grip = pistol_grip(b, u_grip, z_top, POLY, 16, 0.1, 0.032, 0.046)
    b.add(box(0.004, 0.003, 0.02, (0, u_grip + 0.04, z_top - 0.018), 0.0008, 1), METAL)         # détente
    for s in (-1, 1):
        b.add(box(0.004, 0.018, 0.006, (s * 0.036, u_grip + 0.005, z_top + 0.012), 0.0015), METAL)  # sélecteur
    m = None
    if mag:
        b.add(lofx([(mag_u - mag_d / 2 - 0.006, 0, z_top - 0.008, mag_w / 2 + 0.006, 0.016, 5),
                    (mag_u + mag_d / 2 + 0.006, 0, z_top - 0.008, mag_w / 2 + 0.006, 0.016, 5)], 20), PAINT)  # puits
        m = straight_mag(b, mag_u, z_top - 0.02, mag_h, mag_w, mag_d, 4, ACCENT)
        for k in range(3):
            zz = z_top - 0.04 - k * mag_h * 0.25
            for s in (-1, 1):
                b.add(box(0.003, mag_d * 0.8, 0.006, (s * (mag_w / 2 + 0.0015), mag_u - 0.002 - (zz - z_top) * 0.07, zz), 0.001), ACCENT)
        b.add(tube_x(0.005, mag_w + 0.016, (0, mag_u + mag_d / 2 + 0.008, z_top - 0.002), 10), METAL)   # arrêtoir
    return grip, m


def _receiver(b, u0, u1, hw, z0, z1, mat=PAINT, eject=True):
    """Boîtier de lanceur : flancs chanfreinés, fenêtre d'éjection découpée à droite, goupilles, vis."""
    zc, hh = (z0 + z1) / 2, (z1 - z0) / 2
    rec = lofx([(u0, 0, zc, hw * 0.85, hh * 0.85, 4), (u0 + 0.012, 0, zc, hw, hh, 5), (u1 - 0.015, 0, zc, hw, hh, 5),
                (u1, 0, zc, hw * 0.9, hh * 0.88, 4)], 28)
    cuts = []
    if eject:
        cuts.append(box(0.03, 0.07, 0.03, (hw, u0 + (u1 - u0) * 0.55, zc + hh * 0.2), 0.004))
    cuts += slots(u0 + 0.02, u0 + 0.08, 4, -hw, zc + hh * 0.3, 0.006, hh * 0.7, 0.012)
    rec = carve(rec, cuts)
    b.add(rec, mat)
    if eject:
        b.add(box(0.004, 0.064, 0.026, (hw - 0.012, u0 + (u1 - u0) * 0.55, zc + hh * 0.2), 0.001), METAL)  # culasse visible
    pins(b, (u0 + 0.03, u1 - 0.03), zc - hh * 0.5, hw)
    for s in (-1, 1):
        for uu in (u0 + 0.025, u1 - 0.025):
            screw(b, uu, zc + hh * 0.6, s * hw, 0.0026, s)
    # Levier d'armement latéral (gauche) et sa glissière.
    b.add(box(0.004, (u1 - u0) * 0.45, 0.006, (-hw - 0.001, u0 + (u1 - u0) * 0.5, zc + hh * 0.45), 0.001), POLY)
    b.add(box(0.016, 0.012, 0.01, (-hw - 0.008, u0 + (u1 - u0) * 0.68, zc + hh * 0.45), 0.003), METAL)


def limpetgun(g):
    """Limpet Gun : carabine lance-bombes compacte, canon creux large (bombe visible), manchon ajouré, chargeur à bombes."""
    b = g.body
    _receiver(b, -0.13, 0.14, 0.034, -0.042, 0.046)
    rail(b, -0.11, 0.12, 0.046)
    red_dot(b, -0.02, 0.055)
    hollow_barrel(b, 0.027, 0.0225, 0.13, 0.36)
    # Manchon de protection ajouré au-dessus du canon et bague de bouche crénelée.
    sh = lofx([(0.15, 0, 0.004, 0.036, 0.034, 2.4), (0.32, 0, 0.004, 0.036, 0.034, 2.4)], 26, True)
    sh = carve(sh, [box(0.12, 0.2, 0.1, (0, 0.235, -0.04), 0), tube(0.029, 0.029, 0.1, 0.4, 0, 0, 24)]
               + slots(0.16, 0.31, 6, 0.0, 0.04, 0.012, 0.03, 0.02, 'z')
               + slots(0.16, 0.31, 6, 0.034, 0.012, 0.012, 0.016, 0.02) + slots(0.16, 0.31, 6, -0.034, 0.012, 0.012, 0.016, 0.02))
    b.add(sh, POLY)
    br = ring(0.033, 0.0226, 0.34, 0.37, 0, 0.0, 24)
    br = carve(br, [box(0.08, 0.02, 0.012, (0, 0.37, 0), 0), box(0.012, 0.02, 0.08, (0, 0.37, 0), 0)])
    b.add(br, ACCENT)
    limpet(b, 0.352, 0.0, 0.019)
    # Garde-main inférieur avec poignée verticale, rail latéral.
    b.add(lofx([(0.13, 0, -0.04, 0.03, 0.016, 4), (0.3, 0, -0.04, 0.028, 0.014, 4)], 20), POLY)
    vents(b, 0.15, 0.28, -0.04, 0.03, 6)
    vents(b, 0.15, 0.28, -0.04, -0.03, 6)
    sup = vertical_grip(b, 0.24, -0.054, 0.075)
    rail(b, 0.16, 0.26, -0.03, 0.032, 0.018, METAL, True)
    grip, mag = _launcher_lower(b, -0.035, -0.042, True, 0.06, 0.09, 0.048, 0.058)
    stock_collapsible(b, -0.12, -0.33, -0.005)
    b.add(box(0.03, 0.03, 0.02, (0, -0.135, -0.005), 0.004), METAL)              # embase de crosse
    pts(g, (0, 0.37, 0), grip, (0, -0.33, -0.02), sup, mag, True)


def limpetsniper(g):
    """Limpet Sniper : boîtier long et fin, canon cannelé à âme creuse, frein de bouche ajouré, lunette, bipied replié,
    crosse à busc réglable ; fléchette collante visible dans la bouche."""
    b = g.body
    _receiver(b, -0.16, 0.16, 0.026, -0.036, 0.036)
    rail(b, -0.15, 0.15, 0.036)
    scope(b, 0.0, 0.075, 0.34, 0.016, 0.026, METAL, 0.045)
    hollow_barrel(b, 0.017, 0.0125, 0.15, 0.66)
    for k in range(6):                                                           # cannelures
        a = 2 * math.pi * k / 6 + math.pi / 6
        b.add(box(0.004, 0.36, 0.004, (math.cos(a) * 0.0168, 0.42, math.sin(a) * 0.0168), 0.0008, 1), POLY)
    mb = tube(0.024, 0.024, 0.66, 0.74, 0, 0, 22)
    mb = carve(mb, slots(0.67, 0.73, 3, 0.024, 0.0, 0.012, 0.02, 0.02) + slots(0.67, 0.73, 3, -0.024, 0.0, 0.012, 0.02, 0.02)
               + [tube(0.0125, 0.0125, 0.6, 0.8, 0, 0, 18)])
    b.add(mb, METAL)
    b.add(tube(0.012, 0.004, 0.71, 0.745, 0, 0, 12), GLOW)                         # pointe de fléchette
    # Garde-main flottant octogonal à fentes.
    hg = lofx([(0.15, 0, -0.004, 0.032, 0.034, 2.6), (0.17, 0, -0.004, 0.034, 0.036, 2.6), (0.48, 0, -0.004, 0.034, 0.036, 2.6),
               (0.49, 0, -0.004, 0.03, 0.032, 2.6)], 8)
    hg = carve(hg, slots(0.2, 0.46, 7, 0.034, -0.004, 0.012, 0.03, 0.02) + slots(0.2, 0.46, 7, -0.034, -0.004, 0.012, 0.03, 0.02)
               + [tube(0.022, 0.022, 0.1, 0.6, 0, 0, 16)])
    b.add(hg, PAINT)
    rail(b, 0.2, 0.46, -0.04, 0, 0.02, METAL)
    bipod(b, 0.44, -0.045, True)
    grip, mag = _launcher_lower(b, -0.06, -0.036, True, 0.04, 0.07, 0.034, 0.05)
    butt = stock_sculpt(b, [(-0.16, 0.02, 0.026, -0.01), (-0.2, 0.022, 0.036, -0.018), (-0.36, 0.022, 0.05, -0.03),
                            (-0.45, 0.022, 0.056, -0.034), (-0.46, 0.021, 0.055, -0.034)], PAINT)
    b.add(box(0.034, 0.12, 0.016, (0, -0.33, 0.026), 0.004), POLY)               # busc
    for uu in (-0.3, -0.36):
        b.add(tz(0.004, 0.004, 0.0, 0.02, 0, uu, 10), METAL)
    b.add(box(0.012, 0.05, 0.02, (0, -0.42, -0.1), 0.004), POLY)                 # monopode arrière
    pts(g, (0, 0.745, 0), grip, (0, -0.46, -0.034), (0, 0.32, -0.04), mag)


def splendor(g):
    """Limpet Splendor : lance-bombes à barillet six coups (type MGL) : barillet à chambres découpées (bombes visibles),
    carcasse ouverte, sangle supérieure, canon court épais, crosse repliable, poignée avant verticale."""
    b = g.body
    zc = -0.044                                                                   # axe du barillet (chambre haute au canon)
    rc = 0.044
    # Barillet : cylindre à six chambres, cannelures extérieures.
    cyl = tube(0.072, 0.072, -0.05, 0.1, 0, zc, 36)
    cuts = []
    for k in range(6):
        a = math.pi / 2 + k * math.pi / 3
        cuts.append(tube(0.02, 0.02, -0.02, 0.11, math.cos(a) * rc, zc + math.sin(a) * rc, 16))
        a2 = a + math.pi / 6
        cuts.append(tube(0.009, 0.009, -0.03, 0.08, math.cos(a2) * 0.074, zc + math.sin(a2) * 0.074, 10))
    b.add(carve(cyl, cuts), ACCENT)
    for k in range(6):
        a = math.pi / 2 + k * math.pi / 3
        b.add(tube(0.018, 0.018, 0.07, 0.085, math.cos(a) * rc, zc + math.sin(a) * rc, 14), POLY)
        b.add(tube(0.008, 0.006, 0.085, 0.092, math.cos(a) * rc, zc + math.sin(a) * rc, 10), GLOW)
    b.add(tube(0.012, 0.012, -0.08, 0.13, 0, zc, 14), METAL)                     # axe
    # Carcasse : bouclier arrière, plaque avant, sangle supérieure et tirants latéraux.
    b.add(lofx([(-0.11, 0, zc + 0.01, 0.05, 0.075, 4), (-0.05, 0, zc + 0.01, 0.06, 0.085, 4)], 24), PAINT)
    b.add(lofx([(0.1, 0, zc + 0.014, 0.058, 0.08, 4), (0.125, 0, zc + 0.014, 0.05, 0.072, 4)], 24), PAINT)
    b.add(prism([(-0.06, 0.035), (0.13, 0.035), (0.13, 0.05), (-0.06, 0.05)], -0.018, 0.018, 0.003), PAINT)
    rail(b, -0.05, 0.12, 0.05)
    for s in (-1, 1):
        b.add(box(0.008, 0.18, 0.012, (s * 0.06, 0.035, zc - 0.07), 0.003), PAINT)
        screw(b, -0.08, zc + 0.04, s * 0.06, 0.003, s)
        screw(b, 0.112, zc + 0.04, s * 0.058, 0.003, s)
    b.add(tube_x(0.012, 0.13, (0, 0.11, zc - 0.07), 16), METAL)                  # charnière de basculement
    # Canon court épais, bague, guidon ; hausse en échelle.
    hollow_barrel(b, 0.032, 0.026, 0.125, 0.3)
    b.add(ring(0.038, 0.026, 0.28, 0.305, 0, 0, 26), METAL)
    for k in range(4):
        b.add(ring(0.0345, 0.03, 0.15 + k * 0.03, 0.16 + k * 0.03, 0, 0, 26), POLY)
    limpet(b, 0.292, 0.0, 0.022)
    b.add(prism([(0.27, 0.03), (0.29, 0.03), (0.285, 0.055), (0.275, 0.055)], -0.004, 0.004, 0.001), METAL)
    b.add(prism([(-0.04, 0.05), (0.0, 0.05), (-0.01, 0.1), (-0.03, 0.1)], 0.02, 0.026, 0.001), METAL)
    b.add(box(0.03, 0.006, 0.05, (0.023, -0.02, 0.075), 0.001), METAL)
    # Poignée pistolet, poignée avant sous le canon, crosse repliable squelette.
    grip = pistol_grip(b, -0.105, -0.07, POLY, 14, 0.1, 0.032, 0.046)
    b.add(lofx([(0.13, 0, -0.035, 0.022, 0.012, 3), (0.28, 0, -0.035, 0.022, 0.012, 3)], 16), POLY)
    sup = vertical_grip(b, 0.22, -0.046, 0.08)
    for zz in (-0.03, -0.1):
        b.add(rod((0, -0.11, zz), (0, -0.33, zz + (0.0 if zz > -0.05 else 0.02)), 0.008, 0.008, 10), METAL)
    b.add(prism([(-0.33, -0.09), (-0.31, -0.09), (-0.31, -0.01), (-0.33, -0.012)], -0.022, 0.022, 0.004), POLY)
    b.add(box(0.048, 0.012, 0.09, (0, -0.338, -0.05), 0.004), RUBBER)
    pts(g, (0, 0.305, 0), grip, (0, -0.34, -0.05), sup, (0, 0.03, zc), True)


def limpetchain(g):
    """Limpet Chain Gun : arme à bande type Mk 19 portée à l'épaule : boîtier carré, couvercle d'alimentation à loquet,
    manchon perforé et cache-flammes à ailettes, caisse de bande à gauche et bande de bombes, poignée de transport."""
    b = g.body
    _receiver(b, -0.14, 0.15, 0.042, -0.052, 0.05, PAINT, eject=False)
    # Couvercle d'alimentation articulé, loquet, fenêtre d'entrée de bande à gauche, sortie à droite.
    b.add(lofx([(-0.12, 0, 0.056, 0.044, 0.01, 6), (0.13, 0, 0.056, 0.044, 0.01, 6)], 20), ACCENT)
    b.add(tube_x(0.007, 0.09, (0, 0.13, 0.056), 12), METAL)
    b.add(box(0.03, 0.02, 0.014, (0, -0.125, 0.064), 0.003), METAL)
    b.add(box(0.012, 0.09, 0.03, (-0.046, 0.02, 0.02), 0.002), POLY)
    b.add(box(0.012, 0.07, 0.026, (0.046, 0.02, 0.02), 0.002), POLY)
    # Poignée de transport.
    for uu in (-0.06, 0.08):
        b.add(box(0.014, 0.014, 0.05, (0, uu, 0.085), 0.003), METAL)
    b.add(lofx([(-0.075, 0, 0.112, 0.012, 0.009, 3), (0.095, 0, 0.112, 0.012, 0.009, 3)], 14), POLY)
    # Canon creux, manchon perforé, cache-flammes à ailettes.
    hollow_barrel(b, 0.024, 0.019, 0.15, 0.5)
    jk = tube(0.036, 0.036, 0.15, 0.42, 0, 0, 26)
    cuts = [tube(0.026, 0.026, 0.1, 0.5, 0, 0, 20)]
    for k in range(5):
        cuts += holes_ring(0.18 + k * 0.05, 0.036, 8, 0.008, 0.02, 0.0)
    b.add(carve(jk, cuts), METAL)
    b.add(ring(0.04, 0.025, 0.145, 0.165, 0, 0, 26), PAINT)
    b.add(ring(0.04, 0.025, 0.41, 0.425, 0, 0, 26), PAINT)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(box(0.004, 0.06, 0.014, (math.cos(a) * 0.026, 0.52, math.sin(a) * 0.026), 0.001, 1), METAL)
    b.add(ring(0.03, 0.019, 0.49, 0.5, 0, 0, 20), METAL)
    limpet(b, 0.49, 0.0, 0.017)
    # Caisse de bande accrochée à gauche, goulotte et bande de bombes jusqu'au boîtier.
    cx = -0.105
    b.add(lofx([(-0.04, cx, -0.07, 0.04, 0.065, 6), (0.12, cx, -0.07, 0.04, 0.065, 6)], 24), ACCENT)
    b.add(box(0.084, 0.165, 0.012, (cx, 0.04, -0.002), 0.003), METAL)            # couvercle de caisse
    for uu in (0.0, 0.08):
        b.add(box(0.084, 0.006, 0.13, (cx, uu, -0.07), 0.002), METAL)            # cerclages
    b.add(box(0.03, 0.03, 0.01, (cx, 0.04, 0.008), 0.003), POLY)                 # poignée de caisse
    flank_text(b, "40MM LIMPET", 0.012, 0.04, -0.06, cx - 0.04, -1)
    for k in range(5):
        t = k / 4
        x = cx + 0.02 + t * 0.06
        z = 0.0 + math.sin(t * math.pi) * 0.03
        b.add(tube_x(0.012, 0.016, (x, 0.02, z), 14), ACCENT)
        b.add(tube_x(0.0075, 0.02, (x, 0.02, z), 10), GLOW)
        b.add(box(0.008, 0.032, 0.004, (x, 0.02, z - 0.012), 0.001), METAL)
    # Poignées et crosse.
    grip, _ = _launcher_lower(b, -0.06, -0.052, False)
    sup = vertical_grip(b, 0.3, -0.04, 0.08)
    b.add(lofx([(0.15, 0, -0.034, 0.024, 0.008, 4), (0.36, 0, -0.034, 0.024, 0.008, 4)], 14), POLY)
    stock_fixed(b, -0.14, -0.4, -0.01, 0.06, 0.1, 0.04, POLY)
    pts(g, (0, 0.53, 0), grip, (0, -0.4, -0.03), sup, (cx, 0.04, -0.07), True)


def detector(g):
    """Limpet Detector : lanceur court à bouche évasée surmonté d'une tête de détection (parabole, antenne, écran arrière,
    capteurs latéraux) ; câbles de liaison."""
    b = g.body
    _receiver(b, -0.12, 0.12, 0.032, -0.04, 0.04, PAINT)
    hollow_barrel(b, 0.03, 0.025, 0.12, 0.26)
    b.add(lofx([(0.24, 0, 0, 0.032, 0.032, 2), (0.29, 0, 0, 0.045, 0.045, 2), (0.3, 0, 0, 0.046, 0.046, 2)], 26, False), METAL)
    b.add(ring(0.047, 0.04, 0.295, 0.305, 0, 0, 26), ACCENT)
    limpet(b, 0.255, 0.0, 0.022)
    # Tête de détection : boîtier, parabole orientée vers l'avant, cornet central, antenne fouet, écran.
    b.add(lofx([(-0.1, 0, 0.075, 0.04, 0.03, 4), (-0.09, 0, 0.075, 0.046, 0.034, 4), (0.08, 0, 0.075, 0.046, 0.034, 4),
                (0.09, 0, 0.075, 0.04, 0.03, 4)], 24), ACCENT)
    for uu in (-0.06, 0.05):
        b.add(box(0.03, 0.02, 0.012, (0, uu, 0.044), 0.003), METAL)
    dish = []
    for k in range(6):
        t = k / 5
        dish.append((0.095 + 0.03 * t * t, 0, 0.09, 0.012 + 0.05 * t, 0.012 + 0.05 * t, 2))
    b.add(lofx(dish, 28, False), ALLOY)
    b.add(rod((0, 0.1, 0.09), (0, 0.15, 0.09), 0.004, 0.003, 8), METAL)
    b.add(ball(0.007, (0, 0.152, 0.09), 10), GLOW)
    for a in (0, 2.1, 4.2):
        b.add(rod((0, 0.1, 0.09), (math.cos(a) * 0.04, 0.12, 0.09 + math.sin(a) * 0.04), 0.002, 0.002, 6), METAL)
    b.add(rod((0.035, -0.08, 0.1), (0.05, -0.12, 0.24), 0.002, 0.0015, 8), POLY)
    b.add(box(0.07, 0.006, 0.05, (0, -0.103, 0.075), 0.002), POLY)               # écran arrière
    b.add(box(0.058, 0.002, 0.038, (0, -0.107, 0.075), 0.001), ENERGY)
    for s in (-1, 1):
        b.add(box(0.012, 0.05, 0.03, (s * 0.052, 0.02, 0.075), 0.003), POLY)    # capteurs latéraux
        b.add(box(0.002, 0.03, 0.012, (s * 0.059, 0.03, 0.075), 0.0008), LENS)
        flank_text(b, "SCAN", 0.008, -0.04, 0.085, s * 0.046, s)
    b.add(rod((-0.03, -0.09, 0.05), (-0.034, -0.11, 0.0), 0.004, 0.004, 8), RUBBER)   # câble
    grip, mag = _launcher_lower(b, -0.04, -0.04, True, 0.05, 0.075, 0.05, 0.06)
    b.add(lofx([(0.12, 0, -0.04, 0.028, 0.012, 4), (0.24, 0, -0.04, 0.026, 0.01, 4)], 16), POLY)
    sup = angled_grip(b, 0.2, -0.05)
    stock_collapsible(b, -0.11, -0.3, -0.005)
    pts(g, (0, 0.3, 0), grip, (0, -0.3, -0.02), sup, mag)


# ── Caisses de déploiement (portées à une main par la poignée, à l'origine) ──

def _hardcase(b, L, T, H, ztop=-0.03, label="", sub="", lz=0.02):
    """Valise Pelican : deux coques (joint vertical au milieu), cadre en relief sur les flancs, coins renforcés rivetés,
    charnières sous la caisse, deux loquets à levier et cadenas sur le dessus, poignée repliable, pieds, valve de purge."""
    zc = ztop - H / 2
    g_ = 0.003
    for s in (-1, 1):
        hw = T / 4 - g_ / 2
        cx = s * (T / 4 + g_ / 2)
        e = 0.014
        b.add(lofx([(-L / 2, cx, zc, hw * 0.8, H / 2 - e, 5), (-L / 2 + e, cx, zc, hw, H / 2, 6), (L / 2 - e, cx, zc, hw, H / 2, 6),
                    (L / 2, cx, zc, hw * 0.8, H / 2 - e, 5)], 32), PAINT)
        # Cadre en relief et nervures de rigidité sur le grand flanc.
        x = s * (T / 2)
        for zz in (ztop - 0.03, ztop - H + 0.03):
            b.add(box(0.008, L - 0.1, 0.012, (x, 0, zz), 0.003), PAINT)
        for uu in (-L / 2 + 0.05, L / 2 - 0.05):
            b.add(box(0.008, 0.012, H - 0.06, (x, uu, zc), 0.003), PAINT)
        for zz in (zc - H * 0.18, zc + H * 0.18):
            b.add(box(0.005, L - 0.16, 0.008, (x, 0, zz), 0.002), PAINT)
        # Coins renforcés (équerres d'alu rivetées).
        for uu in (-1, 1):
            for zz in (-1, 1):
                cu, cz = uu * (L / 2 - 0.02), zc + zz * (H / 2 - 0.02)
                b.add(lofx([(cu - 0.024, cx, cz, hw + 0.004, 0.026, 6), (cu + 0.024, cx, cz, hw + 0.004, 0.026, 6)], 20), ALLOY)
                b.add(box(hw * 2 + 0.008, 0.048, 0.052, (cx, cu, cz), 0.006), ALLOY)
                for (du, dz) in ((-0.012, 0.0), (0.0, -0.012) if zz > 0 else (0.0, 0.012)):
                    b.add(ball(0.0035, (x + s * 0.004, cu + du * uu, cz + dz), 8), METAL)
        # Valve de purge sur un flanc, plaque d'étiquette.
        if s > 0:
            b.add(tube_x(0.013, 0.008, (x + 0.004, L / 2 - 0.1, zc - H * 0.3), 18), POLY)
            knurl_x(b, 0.009, 0.006, (x + 0.011, L / 2 - 0.1, zc - H * 0.3), 12, POLY)
        b.add(box(0.003, L * 0.5, H * 0.24, (x + s * 0.0005, -0.01, zc + H * lz), 0.002), ACCENT)
        if label:
            flank_text(b, label, H * 0.09, -0.01, zc + H * (lz + 0.02), x + s * 0.002, s)
        if sub:
            flank_text(b, sub, H * 0.035, -0.01, zc + H * (lz - 0.08), x + s * 0.002, s)
    # Charnières sous la caisse (sur le joint) : nœuds alternés et ferrures.
    zb = ztop - H
    n = 6
    for k in range(n):
        u0 = -L * 0.38 + k * L * 0.76 / n
        b.add(tube(0.008, 0.008, u0 + 0.002, u0 + L * 0.76 / n - 0.002, 0, zb + 0.004, 12), METAL)
    b.add(tube(0.003, 0.003, -L * 0.39, L * 0.39, 0, zb + 0.004, 8), ALLOY)
    for s in (-1, 1):
        b.add(box(0.016, L * 0.76, 0.004, (s * 0.01, 0, zb - 0.001), 0.001), METAL)
    # Pieds caoutchouc sous la caisse (posée à plat sur le flanc ou debout).
    for uu in (-L / 2 + 0.06, L / 2 - 0.06):
        for s in (-1, 1):
            b.add(tz(0.009, 0.008, zb - 0.007, zb + 0.001, s * T * 0.3, uu, 12), RUBBER)
    # Loquets à levier (deux) et œillets de cadenas sur le dessus.
    for uu in (-L * 0.33, L * 0.33):
        b.add(box(0.06, 0.05, 0.012, (0, uu, ztop + 0.004), 0.003), POLY)       # embase sur le joint
        b.add(prism([(uu - 0.024, ztop + 0.008), (uu + 0.024, ztop + 0.008), (uu + 0.02, ztop + 0.022), (uu - 0.02, ztop + 0.018)],
                    -0.022, 0.022, 0.002), ACCENT)                               # levier
        b.add(tube(0.004, 0.004, uu - 0.026, uu + 0.026, 0.0, ztop + 0.016, 8), METAL)
        b.add(box(0.03, 0.006, 0.006, (0, uu + 0.024, ztop + 0.02), 0.0015), METAL)
    for uu in (-L * 0.18, L * 0.18):
        for s in (-1, 1):
            b.add(box(0.004, 0.016, 0.02, (s * 0.006, uu, ztop + 0.01), 0.0015), ALLOY)
        b.add(tube_x(0.003, 0.02, (0, uu, ztop + 0.015), 8), POLY)
    # Poignée repliable (relevée) : pivots, barre caoutchouc nervurée.
    for uu in (-0.065, 0.065):
        b.add(box(0.034, 0.024, 0.016, (0, uu, ztop + 0.008), 0.004), POLY)
        b.add(tube_x(0.006, 0.04, (0, uu, ztop + 0.012), 10), METAL)
        b.add(rod((0, uu, ztop + 0.012), (0, uu * 0.82, -0.005), 0.0065, 0.006, 10), POLY)
    b.add(lofx([(-0.06, 0, 0.0, 0.012, 0.011, 2.2), (-0.052, 0, 0.0, 0.015, 0.014, 2.2), (0.052, 0, 0.0, 0.015, 0.014, 2.2),
                (0.06, 0, 0.0, 0.012, 0.011, 2.2)], 16), RUBBER)
    for k in range(6):
        b.add(ring(0.0158, 0.012, -0.04 + k * 0.016, -0.036 + k * 0.016, 0, 0.0, 16), RUBBER)
    return zc


def sentrycase(g):
    """Valise de tourelle ZE : grande valise Pelican, trépied replié sanglé sur le flanc gauche, voyant d'état."""
    b = g.body
    L, T, H, zt = 0.56, 0.17, 0.36, -0.03
    zc = _hardcase(b, L, T, H, zt, "ZE-GUN", "SENTRY UNIT  AUTO-TRACK", 0.24)
    # Trépied replié sur le flanc gauche : trois jambes télescopiques, patins, sangles à boucle.
    x = -T / 2 - 0.022
    for k, zz in enumerate((zc - 0.11, zc - 0.065, zc - 0.02)):
        b.add(tube(0.011, 0.011, -0.24, 0.05, x - (k % 2) * 0.006, zz, 12), METAL)
        b.add(tube(0.008, 0.008, 0.05, 0.22, x - (k % 2) * 0.006, zz, 12), ALLOY)
        b.add(ring(0.014, 0.008, 0.045, 0.062, x - (k % 2) * 0.006, zz, 12), POLY)
        b.add(tube(0.016, 0.012, -0.262, -0.24, x - (k % 2) * 0.006, zz, 12), RUBBER)
    for uu in (-0.15, 0.13):
        b.add(lofx([(uu - 0.016, x + 0.008, zc - 0.065, 0.03, 0.075, 6), (uu + 0.016, x + 0.008, zc - 0.065, 0.03, 0.075, 6)], 20), POLY)
        b.add(box(0.006, 0.03, 0.03, (x - 0.026, uu, zc - 0.01), 0.002), METAL)
    b.add(tz(0.008, 0.008, zt, zt + 0.006, 0.04, -0.18, 12), METAL)
    b.add(tz(0.006, 0.006, zt + 0.006, zt + 0.01, 0.04, -0.18, 12), GLOW)
    pts(g, (0, L / 2, zc), (0, 0.0, 0.0), (0, -L / 2, zc), (0, L / 4, zc))


def robotcase(g):
    """Caisse-cage de robot : coque intérieure ventilée (grilles découpées), cadre en tubes d'alu carrés à coins moulés,
    hublot laissant voir l'œil du robot, grande poignée de transport, plaques d'identification."""
    b = g.body
    L, T, H, zt = 0.42, 0.28, 0.32, -0.04
    zc = zt - H / 2
    shell = lofx([(-L / 2 + 0.01, 0, zc, T / 2 - 0.02, H / 2 - 0.02, 7), (-L / 2 + 0.03, 0, zc, T / 2 - 0.008, H / 2 - 0.008, 8),
                  (L / 2 - 0.03, 0, zc, T / 2 - 0.008, H / 2 - 0.008, 8), (L / 2 - 0.01, 0, zc, T / 2 - 0.02, H / 2 - 0.02, 7)], 32)
    cuts = []
    for s in (-1, 1):
        for zz in (zc - 0.06, zc - 0.03, zc + 0.0):
            cuts += slots(-0.12, 0.12, 6, s * (T / 2), zz, 0.022, 0.012, 0.03)
    cuts.append(tube(0.045, 0.045, L / 2 - 0.05, L / 2 + 0.05, 0, zc + 0.03, 24))  # hublot avant
    b.add(carve(shell, cuts), PAINT)
    b.add(box(T - 0.06, L - 0.1, H - 0.06, (0, 0, zc), 0.01), POLY)              # intérieur sombre
    b.add(ball(0.03, (0, L / 2 - 0.05, zc + 0.03), 16), GLOW)                     # œil du robot
    b.add(ring(0.052, 0.044, L / 2 - 0.022, L / 2 - 0.006, 0, zc + 0.03, 24), METAL)
    b.add(tube(0.045, 0.045, L / 2 - 0.02, L / 2 - 0.018, 0, zc + 0.03, 24), GLASS)
    for k in range(6):
        a = 2 * math.pi * k / 6
        b.add(ball(0.003, (math.cos(a) * 0.048, L / 2 - 0.004, zc + 0.03 + math.sin(a) * 0.048), 6), METAL)
    # Cadre : douze tubes carrés et huit coins moulés.
    t = 0.022
    ex, eu, ez = T / 2, L / 2, H / 2
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.add(box(t, L - 0.03, t, (sx * ex, 0, zc + sz * ez), 0.004), ALLOY)
        for su in (-1, 1):
            b.add(box(t, t, H - 0.03, (sx * ex, su * eu, zc), 0.004), ALLOY)
    for su in (-1, 1):
        for sz in (-1, 1):
            b.add(box(T - 0.03, t, t, (0, su * eu, zc + sz * ez), 0.004), ALLOY)
    for sx in (-1, 1):
        for su in (-1, 1):
            for sz in (-1, 1):
                b.add(box(0.036, 0.036, 0.036, (sx * ex, su * eu, zc + sz * ez), 0.008), METAL)
                b.add(ball(0.004, (sx * (ex + 0.018), su * eu, zc + sz * ez), 6), ALLOY)
    # Diagonales de renfort sur les flancs.
    for sx in (-1, 1):
        b.add(rod((sx * (ex + 0.002), -eu + 0.02, zc + 0.035), (sx * (ex + 0.002), eu - 0.02, zc + 0.035), 0.006, 0.006, 8), ALLOY)
        for su in (-1, 1):
            for sz in (-1, 1):
                b.add(prism([(su * (eu - 0.012), zc + sz * (ez - 0.012)), (su * (eu - 0.07), zc + sz * (ez - 0.012)),
                             (su * (eu - 0.012), zc + sz * (ez - 0.07))], sx * ex - 0.004, sx * ex + 0.004, 0.002), ALLOY)
        flank_text(b, "ROBOT BOMB", 0.026, -0.08, zc + 0.08, sx * (T / 2 - 0.006), sx)
        b.add(box(0.004, 0.09, 0.03, (sx * (T / 2 - 0.006), 0.1, zc + 0.08), 0.002), ACCENT)
        b.add(box(0.002, 0.06, 0.006, (sx * (T / 2 - 0.004), 0.1, zc + 0.08), 0.001), GLOW)
    # Poignée de transport en arceau sur le dessus.
    for uu in (-0.08, 0.08):
        b.add(box(0.04, 0.03, 0.02, (0, uu, zt + 0.01), 0.004), METAL)
        b.add(rod((0, uu, zt + 0.015), (0, uu * 0.8, 0.0), 0.007, 0.007, 10), METAL)
    b.add(lofx([(-0.07, 0, 0.0, 0.013, 0.013, 2), (0.07, 0, 0.0, 0.013, 0.013, 2)], 16), RUBBER)
    top_text(b, "THIS SIDE UP", 0.014, -0.1, 0.06, zt - 0.008)
    pts(g, (0, L / 2, zc), (0, 0.0, 0.0), (0, -L / 2, zc), (0, L / 4, zc))


def postcase(g):
    """Conteneur de poste (soin, puissance, protection) : fût cylindrique blanc à cerclages, couvercles vissés moletés,
    colliers à boucle portant la poignée, fenêtre-jauge lumineuse, croix et inscriptions."""
    b = g.body
    r, L = 0.09, 0.6
    zc = -0.03 - r - 0.02
    b.add(lofx([(-L / 2, 0, zc, r * 0.92, r * 0.92, 2), (-L / 2 + 0.015, 0, zc, r, r, 2), (L / 2 - 0.015, 0, zc, r, r, 2),
                (L / 2, 0, zc, r * 0.92, r * 0.92, 2)], 36), PAINT)
    for uu in (-0.2, -0.07, 0.07, 0.2):                                          # nervures roulées
        b.add(ring(r + 0.004, r - 0.01, uu - 0.006, uu + 0.006, 0, zc, 36), PAINT)
    for s in (-1, 1):                                                            # couvercles vissés
        u0 = s * L / 2
        b.add(tube(r * 0.97, r * 0.97, min(u0, u0 + s * 0.035), max(u0, u0 + s * 0.035), 0, zc, 36), ACCENT)
        for k in range(18):
            a = 2 * math.pi * k / 18
            b.add(box(0.008, 0.03, 0.008, (math.cos(a) * r * 0.98, u0 + s * 0.017, zc + math.sin(a) * r * 0.98), 0.002), ACCENT)
        b.add(tube(r * 0.55, r * 0.5, min(u0 + s * 0.035, u0 + s * 0.045), max(u0 + s * 0.035, u0 + s * 0.045), 0, zc, 24), METAL)
        b.add(box(0.012, 0.012, r * 0.9, (0, u0 + s * 0.047, zc), 0.003), METAL)  # barrette de dévissage
    # Colliers à boucle et poignée.
    for uu in (-0.12, 0.12):
        b.add(ring(r + 0.009, r - 0.002, uu - 0.014, uu + 0.014, 0, zc, 36), METAL)
        b.add(box(0.03, 0.03, 0.03, (0, uu, zc + r + 0.01), 0.005), METAL)
        b.add(box(0.026, 0.03, 0.014, (r + 0.006, uu, zc), 0.003), METAL)         # boucle de serrage
        b.add(prism([(uu - 0.014, zc + 0.006), (uu + 0.014, zc + 0.006), (uu + 0.01, zc + 0.03), (uu - 0.01, zc + 0.026)],
                    r + 0.008, r + 0.016, 0.001), ACCENT)
    for uu in (-0.12, 0.12):
        b.add(rod((0, uu, zc + r + 0.02), (0, uu * 0.6, 0.0), 0.007, 0.007, 10), POLY)
    b.add(lofx([(-0.075, 0, 0.0, 0.013, 0.013, 2), (0.075, 0, 0.0, 0.013, 0.013, 2)], 16), RUBBER)
    # Fenêtre-jauge, croix (soin) et inscriptions sur les deux flancs.
    for s in (-1, 1):
        b.add(box(0.012, 0.16, 0.026, (s * (r - 0.001), 0.0, zc), 0.004), METAL)
        b.add(box(0.004, 0.14, 0.012, (s * (r + 0.005), 0.0, zc), 0.002), ENERGY)
        text(b, "SUPPORT POST", 0.02, lambda a, bb, c, s=s: _cyl_u(r, s, a, bb, c, 0.0, zc, 0.55))
        text(b, "DEPLOY - TURN CAP", 0.0105, lambda a, bb, c, s=s: _cyl_u(r, s, a, bb, c, 0.0, zc, -0.6))
        for (w, h) in ((0.05, 0.016), (0.016, 0.05)):                             # croix
            b.add(box(0.008, w, h, (s * (r - 0.001), -0.2 * s, zc), 0.001), ACCENT)
    pts(g, (0, L / 2, zc), (0, 0.0, 0.0), (0, -L / 2, zc), (0, L / 4, zc))


def _cyl_u(r, side, tx, ty, tz_, u0, zc, th0):
    """Lettre posée sur un cylindre d'axe u (rayon r, centre z = zc), flanc ±x, lecture vers +u à droite (−u à gauche)."""
    rr = r + 0.0006 + tz_
    if side > 0:
        th = th0 + ty / r
        return (rr * math.cos(th), u0 + tx, zc + rr * math.sin(th))
    th = math.pi - th0 - ty / r
    return (rr * math.cos(th), u0 - tx, zc + rr * math.sin(th))


def deployer(g):
    """Déployeur (abris, leurres, barrières) : caisse à munitions géante type M2A1 : couvercle débordant à levier à came,
    flancs emboutis, poignée de couvercle, panneau de commande à interrupteurs, antenne, sangles et piquets d'ancrage."""
    b = g.body
    L, T, H, zt = 0.4, 0.2, 0.27, -0.035
    zc = zt - H / 2 - 0.02
    body = lofx([(-L / 2, 0, zc, T / 2 - 0.004, H / 2 - 0.004, 9), (-L / 2 + 0.006, 0, zc, T / 2, H / 2, 10),
                 (L / 2 - 0.006, 0, zc, T / 2, H / 2, 10), (L / 2, 0, zc, T / 2 - 0.004, H / 2 - 0.004, 9)], 36)
    b.add(body, PAINT)
    # Emboutis horizontaux et verticaux (flancs et bouts).
    for s in (-1, 1):
        for zz in (zc - 0.07, zc + 0.04):
            b.add(lofx([(-L / 2 + 0.04, s * (T / 2), zz, 0.006, 0.012, 2.5), (L / 2 - 0.04, s * (T / 2), zz, 0.006, 0.012, 2.5)], 12), PAINT)
        b.add(box(0.004, L * 0.42, 0.06, (s * (T / 2 + 0.001), -0.05, zc - 0.015), 0.002), ACCENT)
        flank_text(b, "DEPLOYER", 0.03, -0.05, zc - 0.01, s * (T / 2 + 0.003), s)
        flank_text(b, "BUNKER / DECOY UNIT  EDF", 0.0105, -0.05, zc - 0.035, s * (T / 2 + 0.003), s)
        b.add(box(0.004, 0.05, 0.012, (s * (T / 2 + 0.002), 0.105, zc - 0.015), 0.002), MARKING)
        for uu in (-L / 2 + 0.012, L / 2 - 0.012):
            b.add(box(0.008, 0.024, H - 0.01, (s * (T / 2 - 0.002), uu, zc), 0.003), METAL)   # cornières
    for su in (-1, 1):
        b.add(box(T - 0.06, 0.006, H * 0.6, (0, su * (L / 2 + 0.002), zc - 0.01), 0.004), PAINT)
    # Couvercle débordant (joint caoutchouc), levier à came sur le bout avant, charnières à l'arrière.
    b.add(lofx([(-L / 2 - 0.008, 0, zt - 0.012, T / 2 + 0.008, 0.016, 8), (L / 2 + 0.008, 0, zt - 0.012, T / 2 + 0.008, 0.016, 8)], 32), PAINT)
    b.add(lofx([(-L / 2 + 0.002, 0, zt - 0.03, T / 2 + 0.002, 0.004, 8), (L / 2 - 0.002, 0, zt - 0.03, T / 2 + 0.002, 0.004, 8)], 32), RUBBER)
    b.add(prism([(L / 2 + 0.004, zt - 0.005), (L / 2 + 0.02, zt - 0.005), (L / 2 + 0.024, zt - 0.1), (L / 2 + 0.014, zt - 0.105),
                 (L / 2 + 0.006, zt - 0.03)], -0.03, 0.03, 0.002), ACCENT)       # levier à came
    b.add(tube_x(0.008, 0.07, (0, L / 2 + 0.012, zt - 0.03), 12), METAL)
    b.add(box(0.05, 0.012, 0.03, (0, L / 2 + 0.006, zt - 0.12), 0.003), METAL)   # gâche
    for uu in (-0.1, 0.1):
        b.add(tube(0.008, 0.008, uu - 0.03, uu + 0.03, -T / 2 - 0.006, zt - 0.018, 12), METAL)
    # Poignée de couvercle (relevée) sur deux pattes.
    for uu in (-0.07, 0.07):
        b.add(box(0.03, 0.026, 0.008, (0, uu, zt + 0.006), 0.003), METAL)
        b.add(rod((0, uu, zt + 0.008), (0, uu * 0.8, 0.0), 0.006, 0.006, 10), METAL)
    b.add(lofx([(-0.065, 0, 0.0, 0.012, 0.012, 2), (0.065, 0, 0.0, 0.012, 0.012, 2)], 16), POLY)
    # Panneau de commande sur le couvercle : interrupteurs à levier, voyants, antenne.
    b.add(box(0.07, 0.1, 0.008, (0.05, -0.12, zt + 0.006), 0.002), POLY)
    for k in range(3):
        uu = -0.15 + k * 0.03
        b.add(tz(0.005, 0.005, zt + 0.01, zt + 0.014, 0.05, uu, 10), METAL)
        b.add(rod((0.05, uu, zt + 0.014), (0.05, uu + 0.006, zt + 0.03), 0.0018, 0.0015, 6), ALLOY)
        b.add(tz(0.003, 0.003, zt + 0.01, zt + 0.014, 0.075, uu, 8), GLOW if k == 0 else ENERGY)
    b.add(tz(0.007, 0.006, zt + 0.004, zt + 0.02, -0.06, -0.15, 12), METAL)
    b.add(rod((-0.06, -0.15, zt + 0.02), (-0.06, -0.19, zt + 0.25), 0.0025, 0.0015, 8), POLY)
    # Sangle et piquets d'ancrage le long du flanc gauche.
    for uu in (-0.165, 0.165):
        b.add(box(T + 0.012, 0.03, 0.004, (0, uu, zc - H / 2 - 0.004), 0.001), POLY)
        b.add(box(0.004, 0.03, H, (-T / 2 - 0.004, uu, zc), 0.001), POLY)
        b.add(box(0.008, 0.036, 0.022, (-T / 2 - 0.007, uu, zc - 0.05), 0.002), METAL)   # boucle
    for k, zz in enumerate((zc - 0.1, zc - 0.075)):
        b.add(rod((-T / 2 - 0.016, -0.17, zz), (-T / 2 - 0.016, 0.16, zz), 0.007, 0.002, 10), METAL)
        b.add(box(0.012, 0.02, 0.03, (-T / 2 - 0.016, -0.175, zz), 0.003), METAL)
    pts(g, (0, L / 2, zc), (0, 0.0, 0.0), (0, -L / 2, zc), (0, L / 4, zc))


def assistgun(g):
    """Pistolet de soutien (soin, renfort, protection, balise) : boîtier blanc galbé, émetteur à trois bobines et parabole,
    cartouche d'énergie apparente dans une fenêtre, poignée avant, crosse filaire repliable."""
    b = g.body
    b.add(lofx([(-0.1, 0, 0.002, 0.024, 0.03, 3), (-0.085, 0, 0.004, 0.03, 0.042, 3.5), (0.06, 0, 0.004, 0.032, 0.046, 3.5),
                (0.11, 0, 0.0, 0.03, 0.04, 3), (0.12, 0, 0.0, 0.026, 0.034, 2.5)], 30), PAINT)
    # Fenêtre latérale montrant la cartouche d'énergie, capot supérieur à fentes.
    for s in (-1, 1):
        b.add(box(0.006, 0.09, 0.03, (s * 0.03, -0.01, 0.01), 0.004), ACCENT)
        b.add(box(0.004, 0.074, 0.016, (s * 0.032, -0.01, 0.01), 0.002), ENERGY)
        b.add(box(0.005, 0.002, 0.022, (s * 0.033, 0.0, 0.01), 0.0005), METAL)
        b.add(box(0.005, 0.002, 0.022, (s * 0.033, -0.02, 0.01), 0.0005), METAL)
    cap = lofx([(-0.07, 0, 0.044, 0.02, 0.012, 3), (0.06, 0, 0.044, 0.02, 0.012, 3)], 20)
    b.add(carve(cap, slots(-0.06, 0.05, 6, 0, 0.056, 0.008, 0.024, 0.012, 'z')), ACCENT)
    energy_cell(b, -0.03, 0.068, 0, 0.07, 0.012)
    for uu in (-0.06, 0.0):
        b.add(box(0.012, 0.008, 0.02, (0, uu, 0.056), 0.002), METAL)
    # Émetteur : tube, trois bobines énergétiques, parabole et pointe.
    b.add(tube(0.018, 0.018, 0.11, 0.26, 0, 0, 20), ALLOY)
    for k in range(3):
        uu = 0.14 + k * 0.035
        b.add(ring(0.03, 0.018, uu, uu + 0.018, 0, 0, 24), METAL)
        b.add(ring(0.031, 0.02, uu + 0.004, uu + 0.014, 0, 0, 24), ENERGY)
    dish = [(0.25 + 0.03 * (k / 4) ** 2, 0, 0, 0.018 + 0.026 * k / 4, 0.018 + 0.026 * k / 4, 2) for k in range(5)]
    b.add(lofx(dish, 26, False), PAINT)
    b.add(rod((0, 0.25, 0), (0, 0.3, 0), 0.006, 0.002, 10), METAL)
    b.add(ball(0.009, (0, 0.3, 0), 12), ENERGY)
    for a in (math.pi / 2, math.pi / 2 + 2.09, math.pi / 2 + 4.19):
        b.add(rod((math.cos(a) * 0.04, 0.28, math.sin(a) * 0.04), (0, 0.3, 0), 0.0018, 0.0018, 6), METAL)
    # Garde-main inférieur, poignée avant, poignée pistolet, crosse filaire repliée.
    b.add(lofx([(0.06, 0, -0.04, 0.022, 0.014, 3), (0.2, 0, -0.03, 0.02, 0.012, 3)], 18), POLY)
    sup = vertical_grip(b, 0.15, -0.048, 0.07)
    grip = pistol_grip(b, -0.03, -0.035, POLY, 14, 0.095, 0.03, 0.044)
    for s in (-1, 1):
        b.add(rod((s * 0.018, -0.1, 0.0), (s * 0.018, -0.25, -0.01), 0.005, 0.005, 10), METAL)
    b.add(box(0.044, 0.014, 0.07, (0, -0.255, -0.025), 0.005), RUBBER)
    b.add(tube_x(0.006, 0.05, (0, -0.1, 0.0), 10), METAL)
    pts(g, (0, 0.3, 0), grip, (0, -0.26, -0.02), sup, (0, -0.03, 0.068), True)


def suppressgun(g):
    """Canon de répression : gros lance-projectiles à pompe : boîtier massif, canon à bouche en tulipe ajourée, manchon à
    fentes, devant de pompe caoutchouté sur magasin tubulaire, poignée de transport, crosse à amortisseur."""
    b = g.body
    _receiver(b, -0.12, 0.16, 0.038, -0.05, 0.05, PAINT)
    rail(b, -0.1, 0.06, 0.05)
    hollow_barrel(b, 0.034, 0.028, 0.15, 0.56)
    flare = lofx([(0.5, 0, 0, 0.036, 0.036, 2), (0.56, 0, 0, 0.046, 0.046, 2), (0.6, 0, 0, 0.058, 0.058, 2), (0.61, 0, 0, 0.058, 0.058, 2)], 30)
    cuts = [lofx([(0.48, 0, 0, 0.028, 0.028, 2), (0.56, 0, 0, 0.038, 0.038, 2), (0.62, 0, 0, 0.052, 0.052, 2)], 24)]
    for k in range(8):
        a = 2 * math.pi * k / 8
        cuts.append(xf(box(0.02, 0.06, 0.01, (0, 0, 0), 0.003), at=(math.cos(a) * 0.05, 0.575, math.sin(a) * 0.05), ru=-math.degrees(a)))
    b.add(carve(flare, cuts), METAL)
    b.add(ring(0.06, 0.05, 0.605, 0.615, 0, 0, 30), ACCENT)
    jk = lofx([(0.16, 0, 0.008, 0.044, 0.044, 2.4), (0.44, 0, 0.008, 0.044, 0.044, 2.4)], 28)
    jk = carve(jk, [box(0.12, 0.3, 0.1, (0, 0.3, -0.05), 0), tube(0.036, 0.036, 0.1, 0.5, 0, 0, 24)]
               + slots(0.18, 0.42, 8, 0.0, 0.05, 0.012, 0.03, 0.03, 'z') + slots(0.18, 0.42, 8, 0.044, 0.02, 0.012, 0.022, 0.03)
               + slots(0.18, 0.42, 8, -0.044, 0.02, 0.012, 0.022, 0.03))
    b.add(jk, POLY)
    b.add(prism([(0.4, 0.035), (0.42, 0.035), (0.415, 0.07), (0.405, 0.07)], -0.004, 0.004, 0.001), METAL)
    # Magasin tubulaire et devant de pompe nervuré.
    b.add(tube(0.02, 0.02, 0.15, 0.47, 0, -0.058, 18), METAL)
    b.add(tube(0.021, 0.018, 0.47, 0.49, 0, -0.058, 18), METAL)
    b.add(lofx([(0.22, 0, -0.06, 0.032, 0.03, 2.4), (0.23, 0, -0.06, 0.036, 0.034, 2.4), (0.37, 0, -0.06, 0.036, 0.034, 2.4),
                (0.38, 0, -0.06, 0.032, 0.03, 2.4)], 24), RUBBER)
    for k in range(8):
        b.add(ring(0.038, 0.03, 0.24 + k * 0.016, 0.247 + k * 0.016, 0, -0.06, 24), RUBBER)
    # Poignée de transport, cartouches de réserve sur le flanc gauche.
    for uu in (-0.08, 0.04):
        b.add(box(0.016, 0.016, 0.05, (0, uu, 0.08), 0.004), PAINT)
    b.add(lofx([(-0.095, 0, 0.11, 0.013, 0.01, 3), (0.055, 0, 0.11, 0.013, 0.01, 3)], 14), POLY)
    b.add(box(0.012, 0.13, 0.04, (-0.044, 0.0, -0.01), 0.003), POLY)
    for k in range(4):
        b.add(tz(0.011, 0.011, -0.03, 0.01, -0.058, -0.045 + k * 0.03, 14), ACCENT)
    grip, _ = _launcher_lower(b, -0.04, -0.05, False)
    stock_fixed(b, -0.12, -0.42, -0.012, 0.07, 0.11, 0.044, POLY)
    b.add(tube(0.012, 0.012, -0.34, -0.2, 0, 0.03, 12), METAL)                  # amortisseur
    pts(g, (0, 0.61, 0), grip, (0, -0.42, -0.03), (0, 0.3, -0.07))


FAMILIES = {f: globals()[f] for f in [
    "smokecan", "designator", "limpetgun", "limpetsniper", "splendor", "limpetchain", "detector",
    "sentrycase", "robotcase", "postcase", "deployer", "assistgun", "suppressgun"]}
SAND, OLIVE, DARK, WHITE = (0.55, 0.48, 0.34), (0.26, 0.29, 0.2), (0.1, 0.1, 0.1), (0.8, 0.8, 0.78)
PALETTE = {f: (SAND, DARK) for f in FAMILIES}
PALETTE.update({"smokecan": (OLIVE, DARK), "designator": (OLIVE, DARK), "assistgun": (WHITE, DARK), "postcase": (WHITE, DARK)})
LABELS = {"smokecan": "M18 SMOKE", "designator": "GUIDE LASER", "limpetgun": "LIMPET GUN", "limpetsniper": "LIMPET SNIPER",
          "splendor": "LIMPET SPLENDOR", "limpetchain": "LIMPET CHAIN", "detector": "LIMPET DETECTOR", "assistgun": "ASSIST GUN",
          "suppressgun": "SUPPRESS GUN", "sentrycase": "ZE-GUN", "robotcase": "ROBOT", "postcase": "POST", "deployer": "DEPLOYER"}
MARK_AT = {"smokecan": None, "postcase": None, "sentrycase": (0.17, -0.31), "robotcase": (0.0, -0.33), "deployer": (0.13, -0.27),
           "splendor": (-0.08, -0.09), "designator": (0.12, 0.0), "assistgun": (-0.075, -0.015), "limpetchain": (-0.11, -0.03)}


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


# ───────────────────────── Objets du monde ─────────────────────────

def part_obj(part, name, mats, parent, loc=(0, 0, 0)):
    o = part.obj(name, mats, parent)
    o.location = P(-loc[0], loc[1], loc[2])
    return o


# ── Tourelles ZE : socle trépied (« Base ») + tête orientable (« Head », origine au pivot, porte « Muzzle ») ──

PIVOT = 1.02


def _tripod(b):
    """Trépied télescopique : moyeu à trois chapes, jambes hautes acier + basses alu, colliers de serrage moletés, patins
    à pointe, entretoises vers le fût ; fût central, batterie, couronne de rotation boulonnée, câble."""
    hub = 0.74
    b.add(tz(0.075, 0.075, hub - 0.06, hub + 0.06, 0, 0, 24), ACCENT)
    b.add(tz(0.085, 0.085, hub + 0.02, hub + 0.035, 0, 0, 24), METAL)
    b.add(tz(0.052, 0.052, 0.38, PIVOT - 0.08, 0, 0, 20), METAL)                     # fût
    b.add(tz(0.06, 0.055, 0.34, 0.38, 0, 0, 20), METAL)
    for k in range(3):
        a = math.pi / 2 + k * 2 * math.pi / 3
        ca, sa = math.cos(a), math.sin(a)
        # Chape au moyeu.
        b.add(xf(box(0.06, 0.05, 0.09, (0, 0, 0), 0.008), at=(ca * 0.09, sa * 0.09, hub), rz=-math.degrees(a)), ACCENT)
        top = (ca * 0.11, sa * 0.11, hub)
        mid = (ca * 0.42, sa * 0.42, 0.34)
        foot = (ca * 0.68, sa * 0.68, 0.035)
        b.add(rod(top, mid, 0.026, 0.026, 14), METAL)
        b.add(rod(mid, foot, 0.019, 0.019, 14), ALLOY)
        b.add(rod(tuple(m - (m - t) * 0.06 for m, t in zip(mid, top)), tuple(m + (f - m) * 0.06 for m, f in zip(mid, foot)), 0.033, 0.033, 16), POLY)
        b.add(xf(box(0.02, 0.03, 0.05, (0, 0, 0), 0.006), at=(mid[0] + ca * 0.035, mid[1] + sa * 0.035, mid[2] + 0.01), rz=-math.degrees(a)), METAL)
        b.add(tz(0.055, 0.05, 0.0, 0.025, foot[0], foot[1], 18), RUBBER)               # patin
        b.add(tz(0.012, 0.004, -0.06, 0.0, foot[0], foot[1], 8), METAL)                 # pointe
        b.add(ball(0.024, foot, 10), METAL)
        b.add(rod((ca * 0.06, sa * 0.06, 0.38), (ca * 0.4, sa * 0.4, 0.36), 0.012, 0.012, 10), METAL)   # entretoise
        b.add(ball(0.018, (ca * 0.4, sa * 0.4, 0.36), 8), METAL)
    # Batterie accrochée au fût (fenêtre de charge), câble vers la tête.
    b.add(lofz([(0.46, 0.0, -0.08, 0.06, 0.035, 4), (0.64, 0.0, -0.08, 0.06, 0.035, 4)], 20), PAINT)
    b.add(box(0.07, 0.01, 0.1, (0, -0.116, 0.55), 0.003), ACCENT)
    b.add(box(0.05, 0.004, 0.06, (0, -0.122, 0.55), 0.001), ENERGY)
    pts_c = [(0.0, -0.11, 0.6), (0.05, -0.12, 0.7), (0.07, -0.1, 0.82), (0.065, -0.07, PIVOT - 0.06)]
    for p0, p1 in zip(pts_c, pts_c[1:]):
        b.add(rod(p0, p1, 0.009, 0.009, 8), RUBBER)
    # Couronne de rotation boulonnée.
    b.add(tz(0.13, 0.13, PIVOT - 0.08, PIVOT - 0.05, 0, 0, 32), ACCENT)
    b.add(tz(0.115, 0.115, PIVOT - 0.05, PIVOT - 0.005, 0, 0, 32), METAL)
    for k in range(12):
        a = 2 * math.pi * k / 12
        b.add(tz(0.008, 0.008, PIVOT - 0.05, PIVOT - 0.044, math.cos(a) * 0.12, math.sin(a) * 0.12, 6), ALLOY)
    for s in (-1, 1):
        flank_text(b, "ZE", 0.05, 0.0, 0.55, s * 0.0602, s)


def _yoke(h, tr, ax):
    """Plateau tournant boulonné et fourche : deux bras effilés à nervure portant les tourillons (hauteur tr, écart ax),
    flasques de tourillon à six vis, butées d'élévation."""
    h.add(tz(0.125, 0.12, 0.0, 0.04, 0, 0, 32), PAINT)
    h.add(lofz([(0.04, 0, 0, ax + 0.02, 0.1, 6), (0.07, 0, 0, ax + 0.02, 0.1, 6)], 28), ACCENT)
    for k in range(10):
        a = 2 * math.pi * k / 10
        h.add(tz(0.007, 0.007, 0.04, 0.046, math.cos(a) * 0.105, math.sin(a) * 0.105, 6), ALLOY)
    for s in (-1, 1):
        x = s * ax
        prof = [(-0.1, 0.06), (0.1, 0.06), (0.06, tr + 0.02), (0.035, tr + 0.055), (-0.035, tr + 0.055), (-0.06, tr + 0.02)]
        h.add(prism(prof, x - 0.016, x + 0.016, 0.005), PAINT)
        h.add(prism([(-0.07, 0.07), (0.07, 0.07), (0.02, tr - 0.03), (-0.02, tr - 0.03)], x + s * 0.016, x + s * 0.024, 0.002), PAINT)
        h.add(tube_x(0.048, 0.03, (x + s * 0.02, 0.0, tr), 22), METAL)
        h.add(tube_x(0.03, 0.02, (x + s * 0.042, 0.0, tr), 16), ACCENT)
        for k in range(6):
            a = 2 * math.pi * k / 6
            h.add(tube_x(0.005, 0.008, (x + s * 0.036, math.cos(a) * 0.038, tr + math.sin(a) * 0.038), 6), ALLOY)
        h.add(box(0.02, 0.025, 0.03, (x, 0.09, tr - 0.06), 0.004), RUBBER)          # butée d'élévation


def _sensor(h, u, z, x=0.0):
    """Capteur de visée : boîtier, deux optiques et télémètre laser."""
    h.add(lofx([(u - 0.1, x, z, 0.05, 0.04, 4), (u + 0.08, x, z, 0.05, 0.04, 4), (u + 0.1, x, z, 0.044, 0.035, 4)], 20), ACCENT)
    h.add(tube(0.024, 0.026, u + 0.1, u + 0.13, x - 0.018, z + 0.008, 18), METAL)
    h.add(tube(0.02, 0.02, u + 0.129, u + 0.131, x - 0.018, z + 0.008, 18), LENS)
    h.add(tube(0.012, 0.012, u + 0.1, u + 0.125, x + 0.025, z - 0.012, 12), METAL)
    h.add(tube(0.009, 0.009, u + 0.124, u + 0.126, x + 0.025, z - 0.012, 12), GLOW)
    h.add(box(0.06, 0.02, 0.01, (x, u + 0.1, z + 0.045), 0.002), POLY)           # pare-soleil


def sentry(name, kind):
    clear()
    mats = materials(SAND, DARK)
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    base = Part()
    _tripod(base)
    part_obj(base, "Base", mats, root)
    h = Part()
    tr = 0.2                                                                      # hauteur des tourillons
    ax = {"zelauncher": 0.17, "zeblaster": 0.155, "zesniper": 0.115}.get(kind, 0.12)
    _yoke(h, tr, ax)
    if kind == "zelauncher":
        # Caisson lance-missiles 2 × 3 tubes (bouches découpées, ogives visibles), relevé de 8°, déflecteur arrière.
        pod = lofx([(-0.3, 0, 0, 0.13, 0.1, 6), (-0.28, 0, 0, 0.14, 0.11, 7), (0.32, 0, 0, 0.14, 0.11, 7), (0.34, 0, 0, 0.13, 0.1, 6)], 28)
        cuts = []
        for x in (-0.085, 0.0, 0.085):
            for z in (-0.05, 0.05):
                cuts.append(tube(0.036, 0.036, 0.25, 0.4, x, z, 16))
        pod = carve(pod, cuts)
        h2 = Part()
        h2.add(pod, PAINT)
        for x in (-0.085, 0.0, 0.085):
            for z in (-0.05, 0.05):
                h2.add(lofx([(0.25, x, z, 0.03, 0.03, 2), (0.29, x, z, 0.03, 0.03, 2), (0.315, x, z, 0.018, 0.018, 2), (0.325, x, z, 0.005, 0.005, 2)], 14), GLOW)
                h2.add(ring(0.04, 0.034, 0.335, 0.345, x, z, 16), METAL)
        for s in (-1, 1):
            for uu in (-0.24, -0.13, 0.3):
                h2.add(box(0.008, 0.02, 0.2, (s * 0.142, uu, 0), 0.003), ACCENT)   # cerclages
            flank_text(h2, "ZE LAUNCHER", 0.026, 0.2, -0.02, s * 0.146, s)
            h2.add(box(0.02, 0.07, 0.02, (s * 0.15, -0.185, 0.075), 0.005), METAL)
        h2.add(box(0.3, 0.02, 0.24, (0, -0.32, 0), 0.006), METAL)                 # déflecteur
        for k in range(5):
            h2.add(box(0.26, 0.012, 0.01, (0, -0.335, -0.08 + k * 0.04), 0.002), POLY)
        _sensor(h2, -0.05, 0.16, 0.0)
        # Le caisson construit autour de son axe est relevé puis posé sur les tourillons.
        bm = bmesh.new()
        me = bpy.data.meshes.new("tmp")
        h2.bm.to_mesh(me)
        h2.bm.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        xf(bm, rx=8.0)
        xf(bm, at=(0, 0, tr))
        _merge(h, bm)
        a = math.radians(8)
        muzzle = (0, 0.34 * math.cos(a), tr + 0.34 * math.sin(a))
    elif kind == "zesniper":
        # Long boîtier, canon de 1,3 m à frein de bouche, bouclier blindé, lunette-capteur longue, chargeur latéral.
        h.add(lofx([(-0.4, 0, tr, 0.07, 0.08, 5), (-0.38, 0, tr, 0.08, 0.09, 6), (0.22, 0, tr, 0.08, 0.09, 6), (0.26, 0, tr, 0.06, 0.07, 5)], 24), PAINT)
        h.add(tube(0.03, 0.03, 0.26, 0.5, 0, tr, 18), METAL)
        h.add(tube(0.024, 0.02, 0.5, 1.42, 0, tr, 18), METAL)
        for k in range(8):
            a = 2 * math.pi * k / 8
            h.add(box(0.008, 0.6, 0.008, (math.cos(a) * 0.024, 0.85, tr + math.sin(a) * 0.024), 0.002, 1), POLY)
        mb = lofx([(1.4, 0, tr, 0.04, 0.035, 4), (1.56, 0, tr, 0.04, 0.035, 4)], 20)
        mb = carve(mb, slots(1.42, 1.54, 3, 0.04, tr, 0.025, 0.05, 0.03) + slots(1.42, 1.54, 3, -0.04, tr, 0.025, 0.05, 0.03)
                   + [tube(0.016, 0.016, 1.3, 1.6, 0, tr, 14)])
        h.add(mb, METAL)
        h.add(prism([(0.28, tr - 0.2), (0.34, tr - 0.2), (0.36, tr + 0.22), (0.3, tr + 0.24)], -0.26, 0.26, 0.01), PAINT)  # bouclier
        h.add(tube(0.04, 0.04, 0.27, 0.37, 0, tr, 18), METAL)
        for s in (-1, 1):
            for zz in (tr - 0.15, tr + 0.17):
                h.add(xf(ball(0.008, (0, 0, 0), 6), at=(s * 0.21, 0.355, zz)), ALLOY)
        h.add(tube(0.035, 0.035, -0.28, 0.18, 0, tr + 0.16, 22), METAL)              # lunette-capteur
        h.add(tube(0.035, 0.05, 0.18, 0.26, 0, tr + 0.16, 22), METAL)
        h.add(tube(0.046, 0.046, 0.259, 0.262, 0, tr + 0.16, 22), LENS)
        for uu in (-0.2, 0.1):
            h.add(box(0.04, 0.04, 0.08, (0, uu, tr + 0.1), 0.006), ACCENT)
        h.add(box(0.07, 0.12, 0.1, (0, -0.24, tr - 0.12), 0.008), ACCENT)        # chargeur
        flank_text(h, "ZE SNIPER", 0.03, -0.27, tr + 0.02, -0.081, -1)
        flank_text(h, "ZE SNIPER", 0.03, -0.27, tr + 0.02, 0.081, 1)
        muzzle = (0, 1.56, tr)
    elif kind == "zeblaster":
        # Canon rotatif à six tubes : carter, moteur, anneaux énergétiques, ailettes de refroidissement, cellules latérales.
        h.add(lofx([(-0.3, 0, tr, 0.11, 0.12, 4), (-0.28, 0, tr, 0.12, 0.13, 5), (0.15, 0, tr, 0.12, 0.13, 5), (0.2, 0, tr, 0.1, 0.11, 4)], 28), PAINT)
        h.add(tube(0.085, 0.085, 0.18, 0.3, 0, tr, 28), METAL)
        for k in range(6):
            a = 2 * math.pi * k / 6
            h.add(tube(0.016, 0.016, 0.3, 0.95, math.cos(a) * 0.05, tr + math.sin(a) * 0.05, 12), METAL)
        h.add(tube(0.02, 0.02, 0.3, 0.97, 0, tr, 12), ALLOY)
        for uu in (0.45, 0.7, 0.92):
            h.add(ring(0.075, 0.03, uu, uu + 0.03, 0, tr, 24), METAL)
        for k in range(5):
            uu = 0.34 + k * 0.022
            h.add(ring(0.1, 0.07, uu, uu + 0.01, 0, tr, 28), ALLOY)                  # ailettes
        for uu in (0.56, 0.62):
            h.add(ring(0.082, 0.07, uu, uu + 0.025, 0, tr, 28), ENERGY)
        h.add(ring(0.08, 0.068, 0.96, 0.98, 0, tr, 24), ENERGY)
        for s in (-1, 1):
            energy_cell(h, -0.22, tr - 0.03, s * 0.16, 0.16, 0.035)
            h.add(box(0.03, 0.03, 0.1, (s * 0.15, -0.22, tr + 0.04), 0.006), ACCENT)
            flank_text(h, "ZE BLASTER", 0.024, -0.12, tr + 0.085, s * 0.121, s)
        h.add(tube(0.06, 0.06, -0.36, -0.28, 0, tr, 20), ACCENT)                    # moteur
        for k in range(8):
            h.add(ring(0.066, 0.05, -0.355 + k * 0.009, -0.351 + k * 0.009, 0, tr, 20), METAL)
        _sensor(h, 0.0, tr + 0.18)
        muzzle = (0, 0.98, tr)
    else:
        # ZE-GUN : boîtier de mitrailleuse lourde, canon à manchon perforé et frein de bouche, caisse de bande et goulotte,
        # bouclier, capteur.
        h.add(lofx([(-0.32, 0, tr, 0.075, 0.09, 4), (-0.3, 0, tr, 0.085, 0.1, 6), (0.2, 0, tr, 0.085, 0.1, 6), (0.24, 0, tr, 0.07, 0.085, 5)], 24), PAINT)
        h.add(lofx([(-0.3, 0, tr + 0.1, 0.07, 0.015, 6), (0.18, 0, tr + 0.1, 0.07, 0.015, 6)], 20), ACCENT)  # couvercle
        h.add(tube_x(0.012, 0.16, (0, 0.17, tr + 0.1), 12), METAL)
        # Plaque de couche à poignées en bêche (type M2), détente papillon, rivets et levier d'armement.
        h.add(box(0.15, 0.03, 0.15, (0, -0.335, tr), 0.01), ACCENT)
        for s in (-1, 1):
            h.add(rod((s * 0.055, -0.35, tr - 0.05), (s * 0.055, -0.42, tr - 0.06), 0.01, 0.01, 10), METAL)
            h.add(tz(0.016, 0.016, tr - 0.1, tr - 0.02, s * 0.055, -0.43, 14), RUBBER)
            for uu in (-0.25, -0.15, -0.05, 0.05, 0.15):
                for zz in (tr - 0.07, tr + 0.07):
                    h.add(xf(ball(0.005, (0, 0, 0), 6), at=(s * 0.086, uu, zz)), METAL)
        h.add(box(0.05, 0.012, 0.03, (0, -0.36, tr - 0.02), 0.004), METAL)
        h.add(box(0.012, 0.06, 0.02, (0.092, -0.12, tr + 0.03), 0.004), METAL)
        h.add(box(0.03, 0.014, 0.03, (0.105, -0.15, tr + 0.03), 0.004), POLY)
        jk = tube(0.045, 0.045, 0.24, 0.72, 0, tr, 24)
        cuts = [tube(0.03, 0.03, 0.2, 0.8, 0, tr, 18)]
        for k in range(7):
            cuts += holes_ring(0.29 + k * 0.06, 0.045, 8, 0.011, 0.03, tr)
        h.add(carve(jk, cuts), METAL)
        h.add(tube(0.022, 0.022, 0.24, 0.86, 0, tr, 16), METAL)
        mb = tube(0.036, 0.036, 0.84, 0.94, 0, tr, 18)
        mb = carve(mb, slots(0.86, 0.92, 2, 0.036, tr, 0.02, 0.04, 0.03) + slots(0.86, 0.92, 2, -0.036, tr, 0.02, 0.04, 0.03)
                   + [tube(0.014, 0.014, 0.8, 1.0, 0, tr, 12)])
        h.add(mb, METAL)
        h.add(ring(0.05, 0.03, 0.24, 0.27, 0, tr, 24), ACCENT)
        h.add(prism([(0.27, tr - 0.16), (0.31, tr - 0.16), (0.33, tr + 0.18), (0.29, tr + 0.2)], -0.24, 0.24, 0.01), PAINT)  # bouclier
        for s in (-1, 1):
            for zz in (tr - 0.12, tr + 0.14):
                h.add(xf(ball(0.008, (0, 0, 0), 6), at=(s * 0.19, 0.325, zz)), ALLOY)
        # Caisse de bande à droite, goulotte de cartouches vers le boîtier.
        h.add(lofx([(-0.2, -0.245, tr - 0.02, 0.07, 0.1, 6), (0.06, -0.245, tr - 0.02, 0.07, 0.1, 6)], 24), ACCENT)
        h.add(box(0.15, 0.27, 0.014, (-0.245, -0.07, tr + 0.085), 0.003), METAL)
        h.add(box(0.03, 0.06, 0.012, (-0.245, -0.07, tr + 0.098), 0.004), POLY)
        for uu in (-0.15, 0.0):
            h.add(box(0.146, 0.012, 0.2, (-0.245, uu, tr - 0.02), 0.003), METAL)
        flank_text(h, "12.7MM  ZE", 0.022, -0.075, tr - 0.02, -0.316, -1)
        for k in range(5):
            t = k / 4
            x = -0.2 + t * 0.12
            zz = tr + 0.05 + math.sin(t * math.pi) * 0.04
            h.add(tube(0.008, 0.006, -0.18, -0.12, x, zz + 0.06, 8), ALLOY)
            h.add(box(0.012, 0.012, 0.008, (x, -0.19, zz + 0.054), 0.002), METAL)
        flank_text(h, "ZE-GUN", 0.03, -0.22, tr + 0.02, 0.086, 1)
        _sensor(h, -0.05, tr + 0.18)
        muzzle = (0, 0.94, tr)
    ho = part_obj(h, "Head", mats, root, (0, 0, PIVOT))
    empty("Muzzle", P(-muzzle[0], muzzle[1], muzzle[2]), ho)
    finish()
    bpy.context.view_layer.update()
    return root


def _merge(part, bm):
    """Ajoute à la pièce un bmesh déjà matérialisé (indices de matériaux conservés)."""
    me = bpy.data.meshes.new("tmp")
    bm.to_mesh(me)
    bm.free()
    part.bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def disc(r, h, z):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=24, radius1=r, radius2=r, depth=h)
    bmesh.ops.translate(bm, vec=P(0, 0, z), verts=bm.verts)
    return bm


# ── Robots ──

def _stadium(u0, u1, zc, r, t):
    """Point du parcours d'une chenille (deux roues de rayon r en u0 et u1), t en mètres depuis l'arrière du brin haut."""
    L = u1 - u0
    per = 2 * L + 2 * math.pi * r
    t %= per
    if t < L:
        return (u0 + t, zc + r)
    t -= L
    if t < math.pi * r:
        a = math.pi / 2 - t / r
        return (u1 + r * math.cos(a), zc + r * math.sin(a))
    t -= math.pi * r
    if t < L:
        return (u1 - t, zc - r)
    t -= L
    a = -math.pi / 2 - t / r
    return (u0 + r * math.cos(a), zc + r * math.sin(a))


def track(b, x, u0, u1, zc, r, w, n=36):
    """Chenille : patins à crampons suivant le parcours, roue motrice dentée à l'avant, tendeur à l'arrière, galets."""
    per = 2 * (u1 - u0) + 2 * math.pi * r
    step = per / n
    for k in range(n):
        t = k * step
        u, z = _stadium(u0, u1, zc, r, t)
        u2, z2 = _stadium(u0, u1, zc, r, t + 0.01)
        ang = math.degrees(math.atan2(z2 - z, -(u2 - u)))
        shoe = box(w, step * 0.86, 0.016, (0, 0, 0), 0.003, 1)
        b.add(xf(shoe, at=(x, u, z), rx=ang), RUBBER)
        b.add(_cleat(w, step, x, u, z, zc, u0, u1, r, ang), METAL)
    rr = r - 0.018
    for uu in (u0, u1):
        b.add(tube_x(rr, w * 0.8, (x, uu, zc), 22), ACCENT)
        b.add(tube_x(rr * 0.45, w * 0.9, (x, uu, zc), 14), METAL)
    for k in range(10):                                                           # dents de la roue motrice
        a = 2 * math.pi * k / 10
        b.add(box(w * 0.3, 0.022, 0.022, (x, u1 + math.cos(a) * rr, zc + math.sin(a) * rr), 0.003), METAL)
    nr = 3
    for k in range(nr):
        uu = u0 + (u1 - u0) * (k + 1) / (nr + 1)
        b.add(tube_x(rr * 0.7, w * 0.7, (x, uu, zc - r + rr * 0.7 + 0.016), 18), ACCENT)
        b.add(tube_x(rr * 0.3, w * 0.8, (x, uu, zc - r + rr * 0.7 + 0.016), 10), METAL)


def _cleat(w, step, x, u, z, zc, u0, u1, r, ang):
    """Crampon extérieur d'un patin (côté opposé au centre du parcours)."""
    cu = min(max(u, u0), u1)
    du, dz = u - cu, z - zc
    n = math.hypot(du, dz) or 1.0
    du, dz = du / n * 0.012, dz / n * 0.012
    return xf(box(w * 0.92, step * 0.24, 0.012, (0, 0, 0), 0.002, 1), at=(x, u + du, z + dz), rx=ang)


def robot(name):
    clear()
    mats = materials((0.9, 0.75, 0.15) if name == "speedstar" else SAND, DARK)
    g = Gun(name)
    b = g.body
    {"beetle": _beetle, "patroller": _patroller, "speedstar": _speedstar}.get(name, _robotbomb)(b)
    g.build(mats)
    return g


def _robotbomb(b):
    """Robot bombe : petit engin à chenilles, caisse à glacis chanfreiné, charge cylindrique cerclée à bandes d'alerte,
    détonateur à fils, caméra avant, antenne, gyrophare."""
    for s in (-1, 1):
        track(b, s * 0.25, -0.24, 0.24, 0.11, 0.1, 0.1, 40)
        b.add(lofx([(-0.3, s * 0.25, 0.235, 0.065, 0.012, 4), (0.3, s * 0.25, 0.235, 0.065, 0.012, 4)], 16), PAINT)  # garde-boue
    hull = [(-0.3, 0.1), (0.2, 0.1), (0.32, 0.18), (0.3, 0.3), (0.16, 0.34), (-0.28, 0.34), (-0.32, 0.3)]
    b.add(prism(hull, -0.19, 0.19, 0.012), PAINT)
    for s in (-1, 1):
        b.add(box(0.012, 0.3, 0.06, (s * 0.195, -0.04, 0.24), 0.004), ACCENT)
        for uu in (-0.2, 0.1):
            b.add(xf(ball(0.007, (0, 0, 0), 6), at=(s * 0.2, uu, 0.31)), METAL)
    b.add(box(0.28, 0.02, 0.1, (0, -0.32, 0.22), 0.005), ACCENT)                 # panneau arrière
    for k in range(4):
        b.add(box(0.24, 0.006, 0.008, (0, -0.333, 0.19 + k * 0.022), 0.002), POLY)
    # Caméra avant et phares.
    b.add(lofx([(0.26, 0, 0.29, 0.05, 0.03, 4), (0.32, 0, 0.29, 0.05, 0.03, 4)], 18), ACCENT)
    b.add(tube(0.02, 0.02, 0.32, 0.335, 0, 0.29, 16), LENS)
    for s in (-1, 1):
        b.add(tube(0.016, 0.016, 0.3, 0.32, s * 0.12, 0.25, 14), METAL)
        b.add(tube(0.013, 0.013, 0.319, 0.321, s * 0.12, 0.25, 14), GLOW)
    # Charge cylindrique couchée (axe x) dans son berceau, cerclages, bandes d'alerte, détonateur.
    zc = 0.47
    b.add(tube_x(0.13, 0.36, (0, -0.02, zc), 32), ACCENT)
    for s in (-1, 1):
        b.add(tube_x(0.12, 0.02, (s * 0.185, -0.02, zc), 32), METAL)
        b.add(tube_x(0.05, 0.02, (s * 0.2, -0.02, zc), 16), ACCENT)
    for x in (-0.11, 0.11):
        b.add(tube_x(0.134, 0.03, (x, -0.02, zc), 32), METAL)
        b.add(box(0.03, 0.06, 0.13, (x, -0.02, 0.37), 0.004), METAL)              # berceau
    for k in range(6):
        b.add(tube_x(0.1315, 0.02, (-0.06 + k * 0.024, -0.02, zc), 32), MARKING if k % 2 else ACCENT)
    b.add(box(0.07, 0.07, 0.05, (0, -0.02, zc + 0.15), 0.008), POLY)             # détonateur
    b.add(tz(0.02, 0.02, zc + 0.175, zc + 0.205, 0, -0.02, 14), GLOW)
    for k, (xa, xb) in enumerate(((-0.03, -0.1), (0.03, 0.1))):
        b.add(rod((xa, -0.04, zc + 0.14), (xb, -0.1, zc + 0.05), 0.005, 0.005, 6), RUBBER)
        b.add(rod((xb, -0.1, zc + 0.05), (xb, -0.12, 0.34), 0.005, 0.005, 6), RUBBER)
    flank_text(b, "DANGER  HE", 0.03, 0.0, 0.24, 0.202, 1)
    flank_text(b, "DANGER  HE", 0.03, 0.0, 0.24, -0.202, -1)
    b.add(tz(0.012, 0.01, 0.34, 0.38, -0.14, -0.25, 10), METAL)
    b.add(rod((-0.14, -0.25, 0.38), (-0.16, -0.3, 0.85), 0.004, 0.002, 6), POLY)
    b.add(ball(0.008, (-0.16, -0.3, 0.85), 8), GLOW)


def _leg(b, hip, knee, foot, r=0.022):
    """Patte articulée : rotules, cuisse et tibia, vérin hydraulique, griffe."""
    b.add(ball(r * 1.6, hip, 12), METAL)
    b.add(rod(hip, knee, r, r * 0.85, 12), PAINT)
    b.add(ball(r * 1.4, knee, 12), METAL)
    b.add(rod(knee, foot, r * 0.8, r * 0.45, 12), ACCENT)
    # Vérin : du milieu de la cuisse à mi-tibia, décalé vers le haut.
    a = tuple(h + (k - h) * 0.3 for h, k in zip(hip, knee))
    c = tuple(k + (f - k) * 0.35 for k, f in zip(knee, foot))
    a = (a[0], a[1], a[2] + 0.04)
    m = tuple((p + q) / 2 for p, q in zip(a, c))
    b.add(rod(a, m, r * 0.45, r * 0.45, 8), METAL)
    b.add(rod(m, c, r * 0.28, r * 0.28, 8), ALLOY)
    b.add(ball(r * 0.8, foot, 10), METAL)
    for k in range(3):                                                            # griffes
        ang = k * 2 * math.pi / 3
        b.add(rod(foot, (foot[0] + math.cos(ang) * 0.05, foot[1] + math.sin(ang) * 0.05, 0.0), r * 0.35, r * 0.15, 6), METAL)


def _beetle(b):
    """Assault Beetle : robot sauteur en forme de scarabée : élytres blindés bombés (joint central, nervures, rivets, évents),
    pronotum à rebord, tête à yeux multiples, corne et mandibules, six pattes articulées à vérins écartées, ventre sombre,
    tuyères de saut sous l'arrière."""
    zb = 0.36
    # Élytres : deux demi-coques bombées séparées par un joint.
    for s in (-1, 1):
        cx = s * 0.155
        b.add(lofx([(-0.5, cx * 0.6, zb + 0.02, 0.07, 0.06, 2.2), (-0.44, cx, zb + 0.05, 0.13, 0.15, 2.3), (-0.28, cx, zb + 0.07, 0.155, 0.2, 2.4),
                    (-0.06, cx, zb + 0.07, 0.155, 0.2, 2.4), (0.08, cx, zb + 0.05, 0.145, 0.16, 2.3), (0.12, cx, zb + 0.03, 0.12, 0.11, 2.2)], 26), PAINT)
        # Nervure longitudinale, rebord inférieur, rivets.
        b.add(lofx([(-0.42, s * 0.2, zb + 0.2, 0.012, 0.012, 2), (-0.24, s * 0.215, zb + 0.235, 0.014, 0.014, 2), (0.0, s * 0.215, zb + 0.235, 0.014, 0.014, 2),
                    (0.1, s * 0.2, zb + 0.18, 0.012, 0.012, 2)], 10), PAINT)
        b.add(lofx([(-0.44, cx, zb - 0.06, 0.14, 0.02, 3), (0.1, cx, zb - 0.06, 0.15, 0.02, 3)], 20), ACCENT)
        for uu in (-0.36, -0.24, -0.12, 0.0):
            b.add(ball(0.009, (s * 0.285, uu, zb + 0.06), 8), METAL)
        for k in range(3):                                                        # évents
            b.add(xf(box(0.06, 0.012, 0.02, (0, 0, 0), 0.004), at=(s * 0.1, -0.34 + k * 0.03, zb + 0.255), ru=-s * 12), POLY)
    b.add(box(0.012, 0.46, 0.02, (0, -0.17, zb + 0.262), 0.004), POLY)            # joint des élytres
    # Ventre et pronotum (bouclier avant à rebord), tête.
    b.add(lofx([(-0.4, 0, zb - 0.07, 0.22, 0.08, 2.4), (0.25, 0, zb - 0.05, 0.18, 0.07, 2.4)], 22), ACCENT)
    b.add(lofx([(0.06, 0, zb + 0.04, 0.2, 0.12, 2.6), (0.12, 0, zb + 0.06, 0.22, 0.15, 2.8), (0.24, 0, zb + 0.05, 0.19, 0.13, 2.8),
                (0.29, 0, zb + 0.03, 0.15, 0.1, 2.6)], 24), PAINT)
    b.add(lofx([(0.235, 0, zb + 0.05, 0.195, 0.135, 2.8), (0.26, 0, zb + 0.05, 0.19, 0.13, 2.8)], 24), ACCENT)
    b.add(lofx([(0.27, 0, zb + 0.0, 0.12, 0.09, 2.2), (0.34, 0, zb + 0.01, 0.13, 0.095, 2.4), (0.41, 0, zb - 0.01, 0.1, 0.075, 2.2),
                (0.44, 0, zb - 0.03, 0.05, 0.04, 2)], 22), ACCENT)
    for (x, z, r) in ((-0.055, 0.04, 0.022), (0.055, 0.04, 0.022), (-0.09, 0.0, 0.015), (0.09, 0.0, 0.015), (0.0, 0.07, 0.012)):
        b.add(ball(r, (x, 0.4, zb + z), 10), GLOW)
    # Corne recourbée (trois tronçons) et mandibules.
    hp = [(0, 0.36, zb + 0.08), (0, 0.44, zb + 0.18), (0, 0.5, zb + 0.3), (0, 0.5, zb + 0.38)]
    for k, (p0, p1) in enumerate(zip(hp, hp[1:])):
        b.add(rod(p0, p1, 0.035 - k * 0.01, 0.025 - k * 0.01, 12), METAL)
        b.add(ball(0.027 - k * 0.009, p1, 10), METAL)
    for s in (-1, 1):
        man = prism([(0.4, zb - 0.06), (0.5, zb - 0.05), (0.57, zb - 0.09), (0.53, zb - 0.085), (0.48, zb - 0.075), (0.4, zb - 0.09)], -0.012, 0.012, 0.004)
        b.add(xf(man, at=(s * 0.05, 0, 0), rz=-s * 12, pivot=(0, 0.4, zb)), METAL)
    # Six pattes écartées (avant vers l'avant, arrière vers l'arrière).
    for s in (-1, 1):
        for k, uu in enumerate((0.18, 0.02, -0.16)):
            spread = (1 - k) * 0.24
            hip = (s * 0.2, uu, zb - 0.04)
            knee = (s * 0.46, uu + spread * 0.55, zb + 0.16)
            foot = (s * 0.66, uu + spread * 1.1, 0.03)
            _leg(b, hip, knee, foot, 0.024)
    # Tuyères de saut sous l'arrière.
    for x in (-0.1, 0.1):
        b.add(rod((x, -0.34, zb - 0.06), (x, -0.36, zb - 0.2), 0.04, 0.055, 16), METAL)
        b.add(rod((x, -0.36, zb - 0.2), (x, -0.36, zb - 0.205), 0.045, 0.045, 16), GLOW)
    for s in (-1, 1):
        b.add(rod((s * 0.06, 0.38, zb + 0.06), (s * 0.2, 0.55, zb + 0.24), 0.006, 0.003, 6), METAL)
        b.add(ball(0.01, (s * 0.2, 0.55, zb + 0.24), 8), GLOW)
        flank_text(b, "A-BEETLE", 0.03, -0.2, zb - 0.06, s * 0.302, s)


def _wheel(b, x, u, z, r, w, s, n=14):
    """Roue tout-terrain : pneu à crampons, jante à six rayons, moyeu."""
    b.add(lofa([(x - w / 2, u, z, r * 0.9, r * 0.9, 2), (x - w * 0.4, u, z, r, r, 2), (x + w * 0.4, u, z, r, r, 2), (x + w / 2, u, z, r * 0.9, r * 0.9, 2)], 28), RUBBER)
    for k in range(n):
        a = 2 * math.pi * k / n
        b.add(xf(box(w * 0.9, 0.025, 0.018, (0, 0, 0), 0.003, 1), at=(x, u + math.cos(a) * r, z + math.sin(a) * r), rx=-math.degrees(a) + 90), RUBBER)
    b.add(tube_x(r * 0.6, w * 1.02, (x, u, z), 22), ACCENT)
    b.add(tube_x(r * 0.25, w * 1.2, (x + s * 0.01, u, z), 14), METAL)
    for k in range(5):
        a = 2 * math.pi * k / 5
        b.add(tube_x(r * 0.05, w * 1.1, (x, u + math.cos(a) * r * 0.42, z + math.sin(a) * r * 0.42), 8), METAL)


def _patroller(b):
    """Patroller : robot de patrouille à six roues sur balanciers, châssis à pans coupés, tourelle à deux mitrailleuses,
    mât de capteurs, antennes, phares et caisses de munitions."""
    zr, r = 0.14, 0.13
    for s in (-1, 1):
        x = s * 0.36
        for uu in (-0.3, 0.0, 0.3):
            _wheel(b, x, uu, zr, r, 0.1, s)
        # Balanciers et bogie.
        b.add(rod((s * 0.29, -0.3, zr), (s * 0.29, 0.0, zr + 0.12), 0.022, 0.022, 10), METAL)
        b.add(rod((s * 0.29, 0.0, zr + 0.12), (s * 0.29, 0.3, zr), 0.022, 0.022, 10), METAL)
        b.add(tube_x(0.035, 0.05, (s * 0.27, 0.0, zr + 0.12), 14), ACCENT)
    hull = [(-0.4, 0.2), (0.32, 0.2), (0.44, 0.28), (0.4, 0.38), (0.2, 0.44), (-0.34, 0.44), (-0.42, 0.36)]
    b.add(prism(hull, -0.24, 0.24, 0.014), PAINT)
    for s in (-1, 1):
        b.add(box(0.02, 0.6, 0.12, (s * 0.245, -0.03, 0.32), 0.006), ACCENT)      # flancs blindés
        for uu in (-0.28, -0.08, 0.12):
            b.add(xf(ball(0.008, (0, 0, 0), 6), at=(s * 0.256, uu, 0.37)), METAL)
        b.add(box(0.06, 0.16, 0.08, (s * 0.2, -0.3, 0.48), 0.008), ACCENT)          # caisses de munitions
        b.add(tube(0.02, 0.02, 0.42, 0.44, s * 0.15, 0.33, 14), METAL)
        b.add(tube(0.016, 0.016, 0.439, 0.441, s * 0.15, 0.33, 14), GLOW)
        flank_text(b, "PATROLLER", 0.04, -0.06, 0.31, s * 0.256, s)
    for k in range(5):
        b.add(box(0.36, 0.01, 0.006, (0, -0.25 + k * 0.03, 0.445), 0.002), POLY)    # grille moteur
    # Tourelle : couronne, casemate, deux mitrailleuses, capteur.
    b.add(tz(0.16, 0.16, 0.44, 0.47, 0, 0.05, 28), METAL)
    b.add(lofz([(0.47, 0, 0.05, 0.16, 0.18, 4), (0.58, 0, 0.06, 0.15, 0.17, 4), (0.62, 0, 0.06, 0.12, 0.14, 4)], 24), PAINT)
    for s in (-1, 1):
        b.add(tube(0.03, 0.03, 0.16, 0.26, s * 0.07, 0.53, 14), METAL)
        b.add(tube(0.012, 0.012, 0.26, 0.6, s * 0.07, 0.53, 12), METAL)
        b.add(tube(0.018, 0.016, 0.56, 0.62, s * 0.07, 0.53, 12), METAL)
    _sensor(b, 0.02, 0.68)
    # Mât de capteurs et antennes.
    b.add(tz(0.015, 0.012, 0.62, 0.95, -0.08, -0.08, 10), METAL)
    b.add(lofx([(-0.12, -0.08, 0.97, 0.04, 0.03, 4), (-0.02, -0.08, 0.97, 0.04, 0.03, 4)], 16), ACCENT)
    b.add(tube(0.018, 0.018, -0.02, 0.0, -0.08, 0.97, 14), LENS)
    b.add(ball(0.015, (-0.08, -0.07, 1.0), 8), GLOW)
    for x in (0.12, 0.17):
        b.add(rod((x, -0.3, 0.44), (x + 0.01, -0.34, 1.0), 0.004, 0.002, 6), POLY)


def _speedstar(b):
    """Speed Star : robot monoroue : grand pneu à crampons, jante à rayons, nacelle centrale suspendue (œil, ailerons),
    queue stabilisatrice à roulette, balise de leurre."""
    R, w, zc = 0.42, 0.16, 0.42
    b.add(torus(R - 0.02, 0.055, (0, 0, zc), 'x', 48, 12), RUBBER)
    for k in range(32):
        a = 2 * math.pi * k / 32
        b.add(xf(box(w * 0.9, 0.04, 0.02, (0, 0, 0), 0.004, 1), at=(0, math.cos(a) * (R + 0.03), zc + math.sin(a) * (R + 0.03)),
                 rx=-math.degrees(a) + 90), RUBBER)
    b.add(xf(ring(R - 0.06, R - 0.1, -0.06, 0.06, 0, 0, 40), at=(0, 0, zc), rz=90), ACCENT)    # jante
    for s in (-1, 1):
        b.add(tube_x(0.12, 0.02, (s * 0.07, 0, zc), 28), METAL)
        for k in range(8):
            a = 2 * math.pi * k / 8 + (0.2 if s > 0 else -0.2)
            b.add(rod((s * 0.07, math.cos(a) * 0.11, zc + math.sin(a) * 0.11), (s * 0.045, math.cos(a) * (R - 0.09), zc + math.sin(a) * (R - 0.09)),
                      0.012, 0.01, 8), ALLOY)
    # Nacelle centrale (paint jaune), œil lumineux, ailerons, moteurs de moyeu.
    b.add(ellipsoid(0.11, 0.2, 0.16, (0, 0.0, zc - 0.02), 22), PAINT)
    b.add(lofx([(0.14, 0, zc + 0.01, 0.06, 0.05, 2), (0.2, 0, zc + 0.01, 0.05, 0.04, 2)], 18), ACCENT)
    b.add(tube(0.035, 0.035, 0.2, 0.21, 0, zc + 0.01, 18), GLOW)
    for s in (-1, 1):
        b.add(tube_x(0.07, 0.06, (s * 0.12, 0, zc), 22), ACCENT)
        b.add(prism([(-0.12, zc + 0.12), (0.06, zc + 0.12), (0.0, zc + 0.24), (-0.16, zc + 0.26)], s * 0.03 - 0.008, s * 0.03 + 0.008, 0.004), PAINT)
    # Queue stabilisatrice à roulette.
    b.add(rod((0, -0.12, zc - 0.05), (0, -0.62, 0.1), 0.025, 0.018, 12), METAL)
    b.add(tube_x(0.06, 0.04, (0, -0.64, 0.06), 18), RUBBER)
    b.add(tube_x(0.025, 0.05, (0, -0.64, 0.06), 10), METAL)
    b.add(prism([(-0.5, 0.12), (-0.36, 0.14), (-0.5, 0.36), (-0.58, 0.34)], -0.008, 0.008, 0.004), ACCENT)
    # Balise de leurre au sommet.
    b.add(rod((0, 0.0, zc + 0.14), (0, 0.0, zc + 0.56), 0.012, 0.01, 10), METAL)
    b.add(lofz([(zc + 0.56, 0, 0, 0.05, 0.05, 2), (zc + 0.6, 0, 0, 0.06, 0.06, 2), (zc + 0.66, 0, 0, 0.04, 0.04, 2)], 18), ACCENT)
    b.add(ball(0.045, (0, 0, zc + 0.69), 14), GLOW)
    for k in range(4):
        a = k * math.pi / 2
        b.add(rod((math.cos(a) * 0.05, math.sin(a) * 0.05, zc + 0.63), (math.cos(a) * 0.12, math.sin(a) * 0.12, zc + 0.6), 0.004, 0.003, 6), METAL)


# ── Postes (soin, puissance, protection) ──

def post(name):
    """Poste déployable : quatre pieds repliables à patins, coffre à batterie octogonal, mât télescopique gainé de câbles,
    tête propre au poste : distributeur de soin (croix, buses), bobines de puissance (anneaux, paratonnerre), projecteur de
    protection (pétales, cristal)."""
    clear()
    color = {"lifevendor": (0.2, 0.75, 0.35), "powerpost": (0.85, 0.35, 0.1), "guardpost": (0.25, 0.45, 0.85)}[name]
    mats = materials(WHITE, color)
    g = Gun(name)
    b = g.body
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        ca, sa = math.cos(a), math.sin(a)
        b.add(rod((ca * 0.13, sa * 0.13, 0.3), (ca * 0.42, sa * 0.42, 0.03), 0.02, 0.018, 12), METAL)
        b.add(ball(0.03, (ca * 0.13, sa * 0.13, 0.3), 10), ACCENT)
        b.add(tz(0.05, 0.045, 0.0, 0.025, ca * 0.43, sa * 0.43, 16), RUBBER)
        b.add(rod((ca * 0.24, sa * 0.24, 0.19), (ca * 0.08, sa * 0.08, 0.12), 0.01, 0.01, 8), ALLOY)
    # Coffre à batterie octogonal, panneaux, voyants.
    b.add(lofz([(0.1, 0, 0, 0.15, 0.15, 1.0), (0.12, 0, 0, 0.17, 0.17, 1.0), (0.4, 0, 0, 0.17, 0.17, 1.0), (0.43, 0, 0, 0.14, 0.14, 1.0)], 8), PAINT)
    b.add(lofz([(0.24, 0, 0, 0.176, 0.176, 1.0), (0.27, 0, 0, 0.176, 0.176, 1.0)], 8), ACCENT)
    for k in range(4):
        a = k * math.pi / 2
        ca, sa = math.cos(a), math.sin(a)
        b.add(xf(box(0.008, 0.1, 0.08, (0, 0, 0), 0.003), at=(ca * 0.125, sa * 0.125, 0.34), rz=-math.degrees(a)), ACCENT)
        b.add(xf(box(0.004, 0.06, 0.012, (0, 0, 0), 0.001), at=(ca * 0.131, sa * 0.131, 0.34), rz=-math.degrees(a)), ENERGY)
        for dz in (0.15, 0.2):
            b.add(xf(box(0.006, 0.09, 0.01, (0, 0, 0), 0.002), at=(ca * 0.127, sa * 0.127, dz), rz=-math.degrees(a)), POLY)
    flank_text(b, {"lifevendor": "LIFE VENDOR", "powerpost": "POWER POST", "guardpost": "GUARD POST"}[name], 0.022, 0.0, 0.31, 0.131, 1)
    flank_text(b, {"lifevendor": "LIFE VENDOR", "powerpost": "POWER POST", "guardpost": "GUARD POST"}[name], 0.022, 0.0, 0.31, -0.131, -1)
    # Mât télescopique et câbles.
    b.add(tz(0.06, 0.055, 0.43, 0.75, 0, 0, 20), PAINT)
    b.add(tz(0.07, 0.07, 0.74, 0.78, 0, 0, 20), ACCENT)
    b.add(tz(0.045, 0.045, 0.78, 1.05, 0, 0, 18), ALLOY)
    for zz in (0.5, 0.6, 0.86, 0.96):
        b.add(tz(0.064 if zz < 0.75 else 0.049, 0.064 if zz < 0.75 else 0.049, zz, zz + 0.012, 0, 0, 20), METAL)
    b.add(tz(0.055, 0.055, 1.04, 1.07, 0, 0, 18), ACCENT)
    for k in range(2):
        a = k * math.pi + 0.4
        pts_c = [(math.cos(a + t * 0.9) * (0.068 if t < 0.5 else 0.055), math.sin(a + t * 0.9) * (0.068 if t < 0.5 else 0.055), 0.43 + t * 0.62)
                 for t in (0, 0.25, 0.5, 0.75, 1.0)]
        for p0, p1 in zip(pts_c, pts_c[1:]):
            b.add(rod(p0, p1, 0.006, 0.006, 6), RUBBER)
    zh = 1.07
    if name == "lifevendor":
        b.add(lofz([(zh, 0, 0, 0.12, 0.1, 4), (zh + 0.03, 0, 0, 0.15, 0.13, 5), (zh + 0.24, 0, 0, 0.15, 0.13, 5), (zh + 0.27, 0, 0, 0.12, 0.1, 4)], 28), PAINT)
        for s in (-1, 1):
            for (w, h) in ((0.12, 0.04), (0.04, 0.12)):
                b.add(box(w, 0.012, h, (0, s * 0.13, zh + 0.135), 0.004), ACCENT)
            b.add(box(0.012, 0.08, 0.15, (s * 0.15, 0, zh + 0.135), 0.004), ACCENT)
            b.add(box(0.004, 0.06, 0.12, (s * 0.155, 0, zh + 0.135), 0.002), ENERGY)
        for k in range(8):                                                        # buses de brume
            a = 2 * math.pi * k / 8
            b.add(rod((math.cos(a) * 0.1, math.sin(a) * 0.09, zh + 0.27), (math.cos(a) * 0.13, math.sin(a) * 0.12, zh + 0.31), 0.012, 0.015, 8), METAL)
            b.add(ball(0.01, (math.cos(a) * 0.13, math.sin(a) * 0.12, zh + 0.315), 6), ENERGY)
        b.add(tz(0.06, 0.04, zh + 0.27, zh + 0.32, 0, 0, 18), ACCENT)
    elif name == "powerpost":
        b.add(tz(0.07, 0.07, zh, zh + 0.32, 0, 0, 20), ACCENT)
        for k in range(5):
            zz = zh + 0.03 + k * 0.06
            b.add(xf(torus(0.11 - k * 0.008, 0.016, (0, 0, 0), 'z', 32, 8), at=(0, 0, zz)), ENERGY if k % 2 == 0 else METAL)
        for k in range(8):                                                        # radiateur à ailettes
            a = 2 * math.pi * k / 8
            b.add(xf(box(0.006, 0.05, 0.28, (0, 0, 0), 0.002), at=(math.cos(a) * 0.075, math.sin(a) * 0.075, zh + 0.16), rz=-math.degrees(a) + 90), METAL)
        b.add(tz(0.04, 0.012, zh + 0.32, zh + 0.5, 0, 0, 14), ALLOY)                # paratonnerre
        b.add(ball(0.03, (0, 0, zh + 0.5), 12), ENERGY)
        b.add(xf(torus(0.06, 0.008, (0, 0, 0), 'z', 24, 6), at=(0, 0, zh + 0.42)), METAL)
    else:
        b.add(tz(0.07, 0.09, zh, zh + 0.08, 0, 0, 22), ACCENT)
        for k in range(4):                                                        # pétales émetteurs
            a = k * math.pi / 2
            b.add(_petal(a, zh), PAINT)
            b.add(_petal(a, zh, 0.011, 0.006), ENERGY)
        b.add(tz(0.035, 0.035, zh + 0.08, zh + 0.18, 0, 0, 14), METAL)
        b.add(lofz([(zh + 0.16, 0, 0, 0.01, 0.01, 2), (zh + 0.24, 0, 0, 0.05, 0.05, 1.0), (zh + 0.36, 0, 0, 0.01, 0.01, 2)], 6), ENERGY)
    g.build(mats)
    return g


def _petal(a, zh, off=0.0, th=0.008):
    """Pétale de projecteur de protection : plaque en ogive (profil dans le plan u-z, épaisseur en x, donc face au rayon
    +x), inclinée de 20° vers l'extérieur puis tournée à l'angle a autour de l'axe vertical ; off : liseré lumineux devant."""
    pet = prism([(-0.05, 0.0), (0.05, 0.0), (0.085, 0.2), (0.0, 0.27), (-0.085, 0.2)] if not off else
                [(-0.03, 0.04), (0.03, 0.04), (0.05, 0.18), (0.0, 0.23), (-0.05, 0.18)], -th, th, 0.003)
    xf(pet, ru=20)
    xf(pet, at=(0.1 + off, 0, zh + 0.07))
    xf(pet, rz=math.degrees(a))
    return pet


# ── Leurre : aérostat captif ──

def decoy():
    """Leurre : aérostat captif (enveloppe profilée, empennage en Y gonflable, lattes de nez, nacelle à balise et haut-
    parleur, suspentes) relié par son câble à un treuil de sol (tambour, cadre, moteur, groupe électrogène)."""
    clear()
    mats = materials((0.2, 0.65, 0.3), DARK)
    g = Gun("decoy")
    b = g.body
    # Treuil de sol.
    b.add(prism([(-0.3, 0.0), (0.3, 0.0), (0.3, 0.06), (-0.3, 0.06)], -0.25, 0.25, 0.01), METAL)
    for s in (-1, 1):
        b.add(box(0.04, 0.5, 0.04, (s * 0.23, 0, 0.03), 0.006), ACCENT)
        b.add(prism([(-0.12, 0.06), (0.12, 0.06), (0.04, 0.38), (-0.04, 0.38)], s * 0.2 - 0.015, s * 0.2 + 0.015, 0.005), ACCENT)
    b.add(tube_x(0.13, 0.36, (0, 0, 0.3), 28), METAL)                              # tambour
    for s in (-1, 1):
        b.add(tube_x(0.17, 0.02, (s * 0.17, 0, 0.3), 28), ACCENT)
    for k in range(9):
        b.add(tube_x(0.134, 0.012, (-0.15 + k * 0.036, 0, 0.3), 24), RUBBER)        # spires de câble
    b.add(tube_x(0.07, 0.12, (0.27, 0, 0.3), 18), POLY)                            # moteur
    b.add(lofx([(-0.3, 0.0, 0.16, 0.12, 0.1, 6), (-0.08, 0.0, 0.16, 0.12, 0.1, 6)], 20), PAINT)   # groupe électrogène
    for k in range(5):
        b.add(box(0.2, 0.006, 0.006, (0, -0.28 + k * 0.04, 0.265), 0.001), POLY)
    b.add(rod((0.0, 0.1, 0.42), (0.0, 0.0, 0.3), 0.012, 0.012, 8), METAL)
    b.add(tube_x(0.025, 0.06, (0, 0.1, 0.43), 12), METAL)                          # poulie de guidage
    # Câble jusqu'à l'aérostat.
    zb = 6.0
    b.add(rod((0, 0.1, 0.45), (0, 0.25, zb - 1.15), 0.008, 0.008, 6), POLY)
    # Enveloppe : nez arrondi, ventre plus plein, poupe effilée.
    L = 4.2
    secs = []
    for k in range(17):
        t = k / 16
        u = L * 0.45 - t * L
        r = 0.85 * (math.sin(math.pi * min(1.0, (t * 1.05) ** 0.75)) ** 0.8) + 0.04
        secs.append((u, 0, zb, r, r, 2))
    b.add(lofx(secs, 32), PAINT)
    for k in range(8):                                                            # lattes de nez
        a = 2 * math.pi * k / 8
        pts_n = [(math.cos(a) * rr, uu, zb + math.sin(a) * rr) for (uu, rr) in ((L * 0.45 - 0.02, 0.15), (L * 0.4, 0.45), (L * 0.33, 0.66))]
        for p0, p1 in zip(pts_n, pts_n[1:]):
            b.add(rod(p0, p1, 0.015, 0.015, 6), ACCENT)
    b.add(ring(0.866, 0.82, 0.0, 0.06, 0, zb, 40), ACCENT)                         # ceinture de suspentes
    # Empennage en Y inversé : trois ailerons gonflables.
    for ang in (90, 210, 330):
        a = math.radians(ang)
        fin = lofa([(0.0, -1.55, 0, 0.55, 0.06, 2.2), (0.75, -1.75, 0, 0.35, 0.04, 2.2), (0.95, -1.8, 0, 0.25, 0.03, 2.2)], 16)
        xf(fin, rz=0)
        # Aileron construit le long de x, tourné autour de l'axe u de l'enveloppe.
        xf(fin, at=(0, 0, 0), ru=ang)
        xf(fin, at=(0, 0, zb))
        b.add(fin, PAINT)
        b.add(xf(xf(rod((0.95, -1.8 - 0.25, 0), (0.95, -1.8 + 0.25, 0), 0.03, 0.03, 8), ru=ang), at=(0, 0, zb)), ACCENT)
    # Nacelle sous le ventre : balise, haut-parleur, suspentes.
    zp = zb - 1.12
    b.add(lofx([(-0.3, 0, zp, 0.12, 0.1, 3), (0.25, 0, zp, 0.14, 0.12, 3), (0.32, 0, zp, 0.1, 0.09, 2.5)], 22), ACCENT)
    b.add(ball(0.07, (0, 0.0, zp - 0.13), 14), GLOW)
    b.add(lofx([(0.32, 0, zp, 0.05, 0.05, 2), (0.42, 0, zp, 0.1, 0.1, 2)], 20, False), METAL)
    for (x, u) in ((-0.1, -0.25), (0.1, -0.25), (-0.1, 0.25), (0.1, 0.25)):
        b.add(rod((x, u, zp + 0.08), (x * 6, u * 2.2, zb - 0.72), 0.006, 0.006, 4), POLY)
    text(b, "EDF DECOY", 0.32, lambda a, bb, c: _cyl_u(0.88, 1, a, bb, c, 0.25, zb, 0.0))
    text(b, "EDF DECOY", 0.32, lambda a, bb, c: _cyl_u(0.88, -1, a, bb, c, 0.25, zb, 0.0))
    g.build(mats)
    return g


# ── Avions (échelle réelle, nez vers +u, envergure le long de x) ──

AIRFOIL = [(1.0, 0.0), (0.8, 0.028), (0.6, 0.05), (0.4, 0.062), (0.22, 0.062), (0.1, 0.048), (0.03, 0.028), (0.0, 0.0),
           (0.03, -0.022), (0.1, -0.034), (0.22, -0.04), (0.4, -0.04), (0.6, -0.032), (0.8, -0.018)]


def wing(sections, mirror=True):
    """Aile : sections (x, u du bord d'attaque, corde, z, épaisseur relative) → bmesh(s) (moitié droite + miroir)."""
    out = []
    for sg in ((1, -1) if mirror else (1,)):
        loops = [[(sg * x, ule - c * ch, z + t * ch * th / 0.1) for (c, t) in AIRFOIL] for (x, ule, ch, z, th) in sections]
        out.append(bridge(loops))
    return out


def fin(sections, x0=0.0, cant=0.0):
    """Dérive : sections (z, u du bord d'attaque, corde, épaisseur relative) ; cant : inclinaison vers l'extérieur (°)."""
    loops = [[(x0 + t * ch * th / 0.1, ule - c * ch, z) for (c, t) in AIRFOIL] for (z, ule, ch, th) in sections]
    bm = bridge(loops)
    if cant:
        z0 = sections[0][0]
        xf(bm, ru=cant, pivot=(x0, 0, z0))
    return bm


def grooves(secs, us, d=0.006):
    """Ajoute des rainures de panneaux (anneaux légèrement rentrés) à un fuselage lofx."""
    out = list(secs)
    for u in us:
        for i in range(len(out) - 1):
            a, c = out[i], out[i + 1]
            if (a[0] - u) * (c[0] - u) < 0:
                t = (u - a[0]) / (c[0] - a[0])
                lerp = lambda k: a[k] + (c[k] - a[k]) * t
                w = 0.04 * (1 if c[0] > a[0] else -1)
                mid = tuple(lerp(k) for k in range(6))
                s0 = (u - w,) + mid[1:]
                s1 = (u, mid[1], mid[2], mid[3] * (1 - d), mid[4] * (1 - d), mid[5])
                s2 = (u + w,) + mid[1:]
                out[i + 1:i + 1] = [s0, s1, s2]
                break
    return out


def fuse_r(secs, u):
    """Demi-largeur, demi-hauteur et z du fuselage à l'abscisse u (interpolation)."""
    s = sorted(secs, key=lambda q: q[0])
    for a, c in zip(s, s[1:]):
        if a[0] <= u <= c[0]:
            t = (u - a[0]) / (c[0] - a[0])
            return tuple(a[k] + (c[k] - a[k]) * t for k in (3, 4, 2))
    return s[-1][3], s[-1][4], s[-1][2]


def jet(b, x, u0, u1, z, r, pod=True):
    """Réacteur en nacelle : capot profilé, entrée d'air (lèvre, aubes sombres), tuyère à volets, cône d'éjection."""
    L = u1 - u0
    if pod:
        b.add(lofx([(u1, x, z, r * 0.92, r * 0.92, 2), (u1 - L * 0.06, x, z, r * 1.02, r * 1.02, 2), (u1 - L * 0.4, x, z, r * 1.05, r * 1.05, 2),
                    (u0 + L * 0.15, x, z, r * 0.85, r * 0.85, 2), (u0, x, z, r * 0.7, r * 0.7, 2)], 24, False), PAINT)
    b.add(ring(r * 0.96, r * 0.8, u1 - 0.05, u1 + 0.02, x, z, 24), ALLOY)
    b.add(tube(r * 0.82, r * 0.82, u1 - L * 0.12, u1 - L * 0.1, x, z, 24), POLY)
    b.add(tube(r * 0.25, r * 0.05, u1 - L * 0.1, u1 - L * 0.02, x, z, 12), METAL)
    b.add(ring(r * 0.72, r * 0.6, u0 - L * 0.08, u0 + 0.02, x, z, 20), METAL)
    b.add(tube(r * 0.6, r * 0.6, u0 - L * 0.05, u0 - L * 0.04, x, z, 20), POLY)
    b.add(tube(r * 0.3, r * 0.05, u0 - L * 0.05, u0 - L * 0.15, x, z, 12), ACCENT)


def prop(b, x, u, z, R, n=4, hub=0.3):
    """Hélice : cône de moyeu et pales vrillées."""
    b.add(lofx([(u - 0.2, x, z, hub, hub, 2), (u + 0.2, x, z, hub * 0.85, hub * 0.85, 2), (u + 0.5, x, z, hub * 0.45, hub * 0.45, 2),
                (u + 0.65, x, z, 0.04, 0.04, 2)], 18), ACCENT)
    for k in range(n):
        a = 360.0 * k / n + 20
        for seg, (z0, z1, w, pitch) in enumerate(((hub * 0.8, R * 0.5, 0.32, 35), (R * 0.5, R, 0.26, 18))):
            bl = box(w, 0.06, z1 - z0, (0, 0, (z0 + z1) / 2), 0.02, 1)
            xf(bl, rz=pitch)
            xf(bl, ru=a)
            xf(bl, at=(x, u, z))
            b.add(bl, POLY)
        tip = box(0.24, 0.062, 0.12, (0, 0, R - 0.06), 0.02, 1)
        xf(tip, rz=18)
        xf(tip, ru=a)
        xf(tip, at=(x, u, z))
        b.add(tip, MARKING)


def store(b, x, u, z, r, L, kind="bomb", pylon=0.25):
    """Charge sous voilure : bombe (ogive, ailettes en croix, cerclage) ou missile (corps fin, ailerons), pylône."""
    if kind == "bomb":
        b.add(lofx([(u - L / 2, x, z, r * 0.45, r * 0.45, 2), (u - L * 0.3, x, z, r, r, 2), (u + L * 0.2, x, z, r, r, 2),
                    (u + L * 0.42, x, z, r * 0.6, r * 0.6, 2), (u + L / 2, x, z, r * 0.12, r * 0.12, 2)], 16), ACCENT)
        b.add(ring(r * 1.03, r * 0.9, u + L * 0.25, u + L * 0.3, x, z, 16), MARKING)
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            b.add(xf(box(0.02, L * 0.22, r * 1.4, (0, 0, 0), 0.005), at=(x + math.cos(a) * r * 0.9, u - L * 0.4, z + math.sin(a) * r * 0.9), ru=math.degrees(a) - 90), ACCENT)
    else:
        b.add(lofx([(u - L / 2, x, z, r * 0.9, r * 0.9, 2), (u + L * 0.4, x, z, r, r, 2), (u + L / 2, x, z, r * 0.1, r * 0.1, 2)], 12), ALLOY)
        b.add(tube(r * 0.95, r * 0.95, u + L * 0.36, u + L * 0.4, x, z, 12), GLOW)
        for k in range(4):
            a = k * math.pi / 2
            b.add(xf(box(0.015, L * 0.12, r * 2.4, (0, 0, 0), 0.004), at=(x + math.cos(a) * r, u - L * 0.42, z + math.sin(a) * r), ru=math.degrees(a) - 90), ALLOY)
            b.add(xf(box(0.012, L * 0.06, r * 1.4, (0, 0, 0), 0.003), at=(x + math.cos(a) * r, u + L * 0.25, z + math.sin(a) * r), ru=math.degrees(a) - 90), ALLOY)
    if pylon:
        b.add(prism([(u - L * 0.3, z + r), (u + L * 0.25, z + r), (u + L * 0.15, z + r + pylon), (u - L * 0.25, z + r + pylon)],
                    x - 0.06, x + 0.06, 0.02), PAINT)


def canopy(b, u0, u1, z, hw, hh, x=0.0, frames=()):
    """Verrière en bulle (loft vitré) et arceaux."""
    secs = [(u0, x, z, hw * 0.4, hh * 0.3, 2), (u0 + (u1 - u0) * 0.2, x, z, hw * 0.9, hh * 0.9, 2), (u0 + (u1 - u0) * 0.55, x, z, hw, hh, 2),
            (u1 - (u1 - u0) * 0.1, x, z, hw * 0.7, hh * 0.75, 2), (u1, x, z, hw * 0.25, hh * 0.3, 2)]
    b.add(lofx(secs, 24), GLASS)
    for uf in frames:
        hwf, hhf, _ = fuse_r([(s[0], 0, s[2], s[3], s[4], 2) for s in secs], uf)
        b.add(_frame(x, uf, z, hwf, hhf), ACCENT)


def _frame(x, u, z, hw, hh):
    """Arceau de verrière : demi-anneau elliptique au-dessus de z."""
    loops = []
    for k in range(13):
        a = math.pi * k / 12
        cx, cz = math.cos(a) * (hw + 0.02), math.sin(a) * (hh + 0.02)
        loops.append([(x + cx * f, u + du, z + cz * f) for (f, du) in ((1.0, -0.04), (1.0, 0.04), (0.96, 0.04), (0.96, -0.04))])
    return bridge(loops, caps=True)


def windows(b, us, x, z, w, h, side=1):
    """Rangée de hublots (verre sombre) affleurant un flanc."""
    for u in us:
        b.add(box(0.06, w, h, (x, u, z), min(w, h) * 0.3, 2), GLASS)


def aircraft(name):
    """Avions à l'échelle réelle, nez vers +u (avant), ailes le long de x."""
    clear()
    mats = materials((0.36, 0.39, 0.38), DARK)
    g = Gun(name)
    b = g.body
    {"gunship": _gunship, "km6": _km6, "kamuy": _kamuy, "vesta": _vesta, "phobos": _phobos}[name](b)
    g.build(mats)
    return g


def _gunship(b):
    """Canonnière quadrimoteur type AC-130 : fuselage cargo à rampe relevée, aile haute, quatre turbopropulseurs à hélices
    quadripales, carénages de train, grande dérive, trois canons sur le flanc gauche (105 mm, 40 mm, 25 mm rotatif), boules
    optroniques, verrière à vitrages multiples, hublots."""
    secs = [(15.0, 0, -0.1, 0.25, 0.3, 2), (14.7, 0, -0.1, 0.9, 1.0, 2), (14.0, 0, 0.0, 1.45, 1.55, 2.1), (12.8, 0, 0.1, 1.9, 1.98, 2.2),
            (11.5, 0, 0.1, 2.0, 2.05, 2.3), (-5.0, 0, 0.1, 2.0, 2.05, 2.3), (-8.0, 0, 0.6, 1.75, 1.6, 2.2), (-11.5, 0, 1.4, 1.05, 0.95, 2.1),
            (-14.5, 0, 2.1, 0.45, 0.5, 2), (-15.0, 0, 2.2, 0.2, 0.25, 2)]
    secs = grooves(secs, (12.0, 9.0, 5.0, 1.0, -3.0, -7.0, -10.5), 0.004)
    b.add(lofx(secs, 32), PAINT)
    b.add(lofx([(14.95, 0, -0.1, 0.27, 0.32, 2), (15.25, 0, -0.1, 0.05, 0.06, 2)], 16), ACCENT)  # radôme
    # Poste de pilotage : vitrages latéraux, hublots, porte.
    for s in (-1, 1):
        for k, (u, z, w, h) in enumerate(((13.95, 1.05, 0.55, 0.5), (13.3, 1.25, 0.6, 0.45), (12.6, 1.35, 0.55, 0.38))):
            hw_, hh_, zc_ = fuse_r(secs, u)
            ang = math.asin(min(0.95, (z - zc_) / hh_))
            xx = math.cos(ang) * hw_ * 0.995
            b.add(xf(box(0.08, w, h, (0, 0, 0), 0.04), at=(s * xx, u, z), ru=-s * math.degrees(ang) * 0.7), GLASS)
        windows(b, (8.5, 3.0, -1.0), s * 1.98, 0.6, 0.35, 0.35)
        b.add(box(0.06, 1.0, 1.7, (s * 1.97, 10.4, -0.2), 0.05), ACCENT)          # porte de soute (cadre)
    b.add(lofx([(13.2, 0, 1.6, 0.85, 0.45, 2.4), (14.25, 0, 1.25, 0.95, 0.4, 2.4)], 20), GLASS)   # pare-brise supérieur
    # Rampe cargo (cadre) et ligne de flotteur.
    b.add(prism([(-8.5, -1.25), (-12.6, 0.6), (-12.4, 0.75), (-8.3, -1.1)], -1.3, 1.3, 0.05), ACCENT)
    # Carénages de train (sponsons) avec trappes.
    for s in (-1, 1):
        b.add(lofx([(-3.5, s * 1.85, -1.2, 0.5, 0.75, 2.2), (-2.5, s * 1.95, -1.1, 0.7, 0.9, 2.4), (2.6, s * 1.95, -1.1, 0.7, 0.9, 2.4),
                    (3.6, s * 1.85, -1.2, 0.5, 0.75, 2.2)], 22), PAINT)
        for uu in (-1.2, 1.3):
            b.add(box(0.06, 1.6, 1.0, (s * 2.66, uu, -1.2), 0.04), ACCENT)
    b.add(box(1.0, 1.8, 0.06, (0, 12.0, -1.98), 0.04), ACCENT)                   # trappes du train avant
    # Aile haute à faible flèche, volets, saumons.
    zw = 1.75
    for w in wing([(0.0, 3.2, 4.6, zw, 0.16), (5.0, 3.0, 4.4, zw + 0.05, 0.15), (12.0, 2.4, 3.6, zw + 0.15, 0.14), (20.0, 1.6, 2.4, zw + 0.3, 0.12)]):
        b.add(w, PAINT)
    for s in (-1, 1):
        for (x0, x1) in ((3.0, 9.0), (9.5, 15.0)):                                # volets (lignes)
            b.add(box(x1 - x0, 0.12, 0.05, (s * (x0 + x1) / 2, -0.75 + (x0 + x1) / 2 * 0.06 * 0, zw + 0.18), 0.02), ACCENT)
        b.add(box(1.2, 0.5, 0.15, (s * 19.6, 1.0, zw + 0.3), 0.06), GLOW)          # feux de position
        # Turbopropulseurs.
        for x in (5.4, 10.6):
            xs = s * x
            b.add(lofx([(6.2, xs, zw - 0.55, 0.55, 0.6, 2.2), (5.6, xs, zw - 0.6, 0.75, 0.85, 2.3), (3.0, xs, zw - 0.45, 0.72, 0.9, 2.3),
                        (-1.5, xs, zw - 0.2, 0.5, 0.6, 2.2), (-3.0, xs, zw, 0.25, 0.25, 2)], 22), PAINT)
            b.add(lofx([(5.9, xs, zw - 1.15, 0.28, 0.12, 2), (4.8, xs, zw - 1.2, 0.3, 0.18, 2), (3.8, xs, zw - 1.0, 0.25, 0.12, 2)], 14), PAINT)   # prise d'air
            b.add(tube(0.2, 0.2, 5.9, 5.95, xs, zw - 1.15, 12), POLY)
            b.add(tube(0.2, 0.22, 2.0, 2.6, xs + s * 0.72, zw - 0.6, 12), METAL)    # échappement
            prop(b, xs, 6.25, zw - 0.55, 2.05, 4, 0.42)
    # Empennages.
    zt = 2.3
    for w in wing([(0.0, -10.6, 4.0, zt, 0.12), (8.0, -12.0, 2.2, zt + 0.1, 0.1)]):
        b.add(w, PAINT)
    b.add(fin([(zt, -9.4, 5.6, 0.13), (zt + 3.0, -11.4, 4.2, 0.12), (zt + 6.0, -13.4, 2.6, 0.11)]), PAINT)
    b.add(fin([(zt + 0.6, -13.6, 1.2, 0.1), (zt + 5.8, -14.8, 1.0, 0.1)], 0.0), ACCENT)  # gouverne de direction
    for s in (-1, 1):
        flank_text(b, "EDF", 1.1, -11.6, zt + 3.2, s * 0.32, s)
        flank_text(b, "GS-130  01", 0.36, -11.6, zt + 1.7, s * 0.3, s)
        text(b, "E D F  AIR RAID", 0.5, lambda a, bb, c, s=s: _cyl_u(2.0, s, a, bb, c, -2.5, 0.1, 0.25))
    # Armement du flanc gauche (−x) : 105 mm, 40 mm, 25 mm rotatif, chacun dans une embrasure.
    for (u, r, L, z) in ((-2.0, 0.17, 2.6, -0.3), (3.0, 0.09, 1.9, -0.2), (7.0, 0.05, 1.4, -0.1)):
        b.add(box(0.2, r * 7, r * 7, (-1.95, u, z), 0.05), ACCENT)
        b.add(tube_x(r * 1.6, 0.6, (-2.2, u, z), 16), METAL)
        b.add(tube_x(r, L, (-2.0 - L / 2, u, z), 16), METAL)
        b.add(tube_x(r * 1.35, 0.35, (-2.0 - L + 0.15, u, z), 16), METAL)
    for k in range(6):                                                           # tubes du canon rotatif
        a = 2 * math.pi * k / 6
        b.add(tube_x(0.02, 1.2, (-2.75, 7.0 + math.cos(a) * 0.05, -0.1 + math.sin(a) * 0.05), 6), METAL)
    # Boules optroniques et antennes.
    for (x, u, z, r) in ((-1.2, 12.2, -1.95, 0.4), (-1.5, 4.5, -1.9, 0.35)):
        b.add(tz(r * 0.8, r * 0.8, z + 0.2, z + 0.4, x, u, 20), ACCENT)
        b.add(ball(r, (x, u, z), 20), ACCENT)
        b.add(tube_x(r * 0.4, 0.05, (x - r * 0.95, u, z), 14), LENS)
    for (u, h) in ((8.0, 0.7), (2.0, 0.5), (-4.0, 0.6)):
        b.add(prism([(u - 0.4, 2.1), (u + 0.2, 2.1), (u - 0.1, 2.1 + h), (u - 0.35, 2.1 + h)], -0.04, 0.04, 0.01), ACCENT)


def _km6(b):
    """Chasseur-bombardier biplace type F-15E : radôme effilé, verrière en tandem, entrées d'air carrées, deux réacteurs,
    aile en flèche, deux dérives, stabilisateurs, réservoirs conformes, bombes et missiles en bout d'aile."""
    secs = [(9.5, 0, 0.0, 0.05, 0.06, 2), (8.6, 0, 0.0, 0.35, 0.38, 2), (6.8, 0, 0.05, 0.62, 0.68, 2.2), (4.8, 0, 0.15, 0.75, 0.8, 2.4),
            (3.0, 0, 0.1, 1.3, 0.8, 3.0), (-3.0, 0, 0.05, 1.45, 0.72, 3.4), (-5.6, 0, 0.0, 1.35, 0.62, 3.2), (-6.8, 0, 0.0, 1.25, 0.55, 3.0)]
    secs = grooves(secs, (7.5, 5.6, 2.0, -1.0, -4.0), 0.005)
    b.add(lofx(secs, 30), PAINT)
    b.add(lofx([(9.45, 0, 0.0, 0.07, 0.08, 2), (9.9, 0, 0.0, 0.02, 0.02, 2)], 12), ACCENT)
    b.add(rod((0, 9.9, 0), (0, 10.6, 0), 0.03, 0.01, 8), METAL)                  # perche de pitot
    canopy(b, 3.0, 6.6, 0.6, 0.48, 0.55, 0.0, (4.3, 5.4))
    b.add(lofx([(2.6, 0, 0.55, 0.4, 0.42, 2.2), (0.0, 0, 0.5, 0.35, 0.3, 2.2), (-3.5, 0, 0.45, 0.25, 0.15, 2)], 18), PAINT)   # dos
    # Entrées d'air rectangulaires inclinées et réservoirs conformes.
    for s in (-1, 1):
        x = s * 1.05
        b.add(lofx([(4.2, x, -0.05, 0.42, 0.5, 6), (3.2, x, -0.05, 0.45, 0.55, 6), (0.0, x, 0.0, 0.42, 0.5, 5)], 20), PAINT)
        b.add(xf(box(0.8, 0.1, 0.94, (0, 0, 0), 0.04), at=(x, 4.25, -0.05)), POLY)
        b.add(lofx([(1.8, s * 1.45, -0.1, 0.25, 0.4, 2.4), (-4.5, s * 1.45, -0.05, 0.25, 0.4, 2.4)], 16), PAINT)   # réservoir conforme
    # Tuyères.
    for s in (-1, 1):
        b.add(tube(0.55, 0.5, -7.3, -6.7, s * 0.65, 0.0, 22), METAL)
        b.add(ring(0.52, 0.42, -7.6, -7.25, s * 0.65, 0.0, 22), ALLOY)
        b.add(tube(0.42, 0.42, -7.2, -7.18, s * 0.65, 0.0, 20), POLY)
        b.add(tube(0.25, 0.25, -7.19, -7.17, s * 0.65, 0.0, 16), GLOW)
    # Aile en flèche, stabilisateurs, dérives jumelles inclinées.
    zw = 0.05
    for w in wing([(1.0, 2.6, 6.8, zw, 0.06), (4.0, -0.2, 3.8, zw - 0.05, 0.05), (7.0, -2.8, 1.5, zw - 0.1, 0.045)]):
        b.add(w, PAINT)
    for w in wing([(1.2, -4.8, 2.6, 0.0, 0.05), (4.2, -6.4, 1.2, -0.05, 0.045)]):
        b.add(w, PAINT)
    for s in (-1, 1):
        b.add(fin([(0.5, -3.9, 3.0, 0.06), (3.4, -6.0, 1.3, 0.05)], s * 1.05, s * 10), PAINT)
        # Missiles en bout d'aile, bombes sous voilure.
        store(b, s * 7.05, -2.2, zw - 0.12, 0.09, 2.9, "missile", 0)
        store(b, s * 3.2, 0.2, zw - 0.75, 0.24, 3.0, "bomb", 0.42)
        store(b, s * 5.2, -1.4, zw - 0.6, 0.18, 2.4, "bomb", 0.38)
        store(b, s * 1.45, 0.0, -0.75, 0.2, 2.5, "bomb", 0.2)
        text(b, "EDF", 0.55, lambda a, bb, c, s=s: _cyl_u(0.74, s, a, bb, c, 6.0, 0.05, -0.2))
    b.add(box(0.6, 1.2, 0.08, (0, 6.5, -0.7), 0.03), ACCENT)                     # trappe du train avant
    for s in (-1, 1):
        b.add(box(0.8, 2.0, 0.06, (s * 1.0, -1.5, -0.68), 0.03), ACCENT)


def _kamuy(b):
    """Bombardier d'attaque type A-10 : nez à canon rotatif, verrière bulle, aile droite épaisse à nacelles de train,
    deux réacteurs en nacelle sur l'arrière du fuselage, empennage à dérives jumelles en bout, nombreuses charges."""
    secs = [(9.0, 0, -0.1, 0.35, 0.4, 2), (8.4, 0, -0.05, 0.7, 0.8, 2.2), (6.5, 0, 0.1, 0.95, 1.05, 2.4), (3.0, 0, 0.1, 1.0, 1.05, 2.4),
            (-3.0, 0, 0.15, 0.9, 0.95, 2.3), (-7.5, 0, 0.45, 0.5, 0.55, 2.1), (-9.0, 0, 0.6, 0.25, 0.3, 2)]
    secs = grooves(secs, (7.2, 4.0, 0.5, -2.5, -5.5), 0.005)
    b.add(lofx(secs, 28), PAINT)
    # Canon rotatif dans le nez.
    for k in range(7):
        a = 2 * math.pi * k / 7
        b.add(tube(0.04, 0.04, 8.7, 10.0, math.cos(a) * 0.12, -0.25 + math.sin(a) * 0.12, 8), METAL)
    b.add(tube(0.18, 0.18, 9.0, 9.2, 0, -0.25, 14), METAL)
    b.add(tube(0.17, 0.17, 9.85, 9.9, 0, -0.25, 14), METAL)
    canopy(b, 4.6, 7.2, 0.95, 0.55, 0.6, 0.0, (5.6, 6.6))
    b.add(lofx([(4.6, 0, 0.95, 0.45, 0.35, 2), (0.0, 0, 0.9, 0.3, 0.25, 2), (-2.0, 0, 0.8, 0.1, 0.1, 2)], 16), PAINT)
    # Aile droite épaisse basse, nacelles de train, saumons recourbés.
    zw = -0.55
    for w in wing([(0.0, 2.6, 3.4, zw, 0.16), (3.5, 2.4, 3.0, zw + 0.05, 0.15), (9.0, 1.8, 1.9, zw + 0.35, 0.13)]):
        b.add(w, PAINT)
    for s in (-1, 1):
        b.add(lofx([(3.4, s * 2.6, zw - 0.15, 0.32, 0.38, 2.2), (2.6, s * 2.6, zw - 0.2, 0.38, 0.45, 2.3), (-0.6, s * 2.6, zw - 0.05, 0.3, 0.32, 2.2)], 18), PAINT)
        b.add(tube_x(0.38, 0.2, (s * 2.6, 2.5, zw - 0.55), 18), RUBBER)            # roue semi-escamotée
        b.add(box(0.5, 0.06, 0.6, (s * 9.0, 0.8, zw + 0.45), 0.04), PAINT)         # saumon
    # Réacteurs en nacelle sur pylônes.
    for s in (-1, 1):
        x = s * 1.5
        b.add(lofx([(-2.4, x, 1.4, 0.65, 0.65, 2), (-2.6, x, 1.4, 0.68, 0.68, 2), (-4.6, x, 1.4, 0.68, 0.68, 2), (-5.8, x, 1.4, 0.5, 0.5, 2)], 24, False), PAINT)
        jet(b, x, -5.8, -2.4, 1.4, 0.62, False)
        b.add(prism([(-2.8, 0.55), (-5.2, 0.6), (-5.0, 0.95), (-3.2, 0.95)], min(x - s * 0.9, x - s * 0.7), max(x - s * 0.9, x - s * 0.7), 0.03), PAINT)
    # Empennage horizontal et dérives jumelles en bout.
    zt = 0.6
    for w in wing([(0.0, -6.8, 2.0, zt, 0.1), (3.2, -7.0, 1.6, zt, 0.1)]):
        b.add(w, PAINT)
    for s in (-1, 1):
        b.add(fin([(zt - 0.9, -6.5, 2.0, 0.1), (zt + 2.2, -7.0, 1.6, 0.1)], s * 3.2), PAINT)
        flank_text(b, "EDF", 0.7, -7.6, zt + 1.0, s * 3.2 + s * 0.12, s)
        flank_text(b, "KAMUY", 0.4, -7.6, zt + 0.2, s * 3.2 + s * 0.12, s)
        flank_text(b, "KAMUY", 0.4, -7.6, zt + 0.2, s * 3.2 - s * 0.12, -s)
    # Charges : six pylônes de bombes, deux paniers de roquettes.
    for s in (-1, 1):
        for k, x in enumerate((1.4, 3.6, 5.0, 6.4, 7.8)):
            if k == 3:
                b.add(lofx([(1.2, s * x, zw - 0.75, 0.3, 0.3, 2), (-1.4, s * x, zw - 0.75, 0.3, 0.3, 2)], 18), ACCENT)
                for j in range(7):
                    a = 2 * math.pi * j / 7
                    b.add(tube(0.07, 0.07, 1.15, 1.21, s * x + math.cos(a) * 0.18, zw - 0.75 + math.sin(a) * 0.18, 8), GLOW)
                b.add(prism([(0.6, zw - 0.45), (-0.6, zw - 0.45), (-0.4, zw - 0.15), (0.4, zw - 0.15)], s * x - 0.06, s * x + 0.06, 0.02), PAINT)
            else:
                store(b, s * x, 1.0, zw - 0.6, 0.2 if k < 2 else 0.16, 2.6 if k < 2 else 2.2, "bomb", 0.3)
    for s in (-1, 1):
        text(b, "EDF", 0.6, lambda a, bb, c, s=s: _cyl_u(1.0, s, a, bb, c, -0.5, 0.15, 0.1))


def _vesta(b):
    """Bombardier lourd type B-52 : long fuselage, cockpit vitré, aile haute en flèche, huit réacteurs en quatre nacelles
    doubles sur pylônes, balancines de bout d'aile, très haute dérive, tourelle de queue, missiles de croisière."""
    secs = [(13.5, 0, -0.1, 0.25, 0.3, 2), (13.0, 0, -0.05, 0.9, 1.0, 2.1), (11.5, 0, 0.1, 1.35, 1.5, 2.3), (9.0, 0, 0.1, 1.4, 1.6, 2.4),
            (-8.0, 0, 0.2, 1.3, 1.5, 2.4), (-11.5, 0, 0.6, 0.8, 0.9, 2.2), (-13.0, 0, 0.8, 0.4, 0.5, 2)]
    secs = grooves(secs, (11.0, 7.5, 3.0, -1.0, -5.0, -9.0), 0.004)
    b.add(lofx(secs, 30), PAINT)
    b.add(lofx([(13.45, 0, -0.1, 0.27, 0.32, 2), (13.8, 0, -0.1, 0.05, 0.05, 2)], 14), ACCENT)
    for s in (-1, 1):
        for k in range(4):
            u = 12.0 - k * 0.55
            b.add(xf(box(0.08, 0.45, 0.35, (0, 0, 0), 0.03), at=(s * (1.1 - k * 0.05 + 0.15), u, 0.95 + k * 0.03), ru=-s * 35), GLASS)
        b.add(xf(box(0.08, 0.3, 0.2, (0, 0, 0), 0.02), at=(s * 1.35, 9.6, 0.4)), GLASS)
    b.add(lofx([(11.0, 0, 1.2, 0.8, 0.45, 2.4), (12.6, 0, 0.75, 0.75, 0.35, 2.4)], 18), GLASS)
    # Aile haute en flèche avec dièdre négatif.
    zw = 1.35
    for w in wing([(0.0, 4.0, 7.5, zw, 0.13), (4.0, 1.8, 6.0, zw - 0.05, 0.12), (10.0, -2.4, 3.6, zw - 0.25, 0.1), (16.0, -6.4, 1.6, zw - 0.5, 0.09)]):
        b.add(w, PAINT)
    for s in (-1, 1):
        # Nacelles doubles sur pylônes.
        for x, ule in ((5.0, 2.0), (9.5, -0.9)):
            xs = s * x
            z = zw - 1.25
            b.add(prism([(ule - 0.6, zw - 0.2), (ule - 3.2, zw - 0.25), (ule - 2.8, z + 0.55), (ule - 0.4, z + 0.55)], xs - 0.12, xs + 0.12, 0.03), PAINT)
            b.add(lofx([(ule + 0.3, xs, z, 0.85, 0.5, 3), (ule - 3.2, xs, z, 0.85, 0.48, 3)], 22), PAINT)
            for dx in (-0.42, 0.42):
                jet(b, xs + dx, ule - 3.8, ule + 0.5, z - 0.02, 0.4, True)
        # Balancines de bout d'aile.
        b.add(lofx([(-5.0, s * 15.0, zw - 0.75, 0.15, 0.2, 2), (-6.6, s * 15.0, zw - 0.75, 0.18, 0.22, 2), (-7.6, s * 15.0, zw - 0.75, 0.1, 0.12, 2)], 14), PAINT)
        b.add(tube_x(0.22, 0.12, (s * 15.0, -6.4, zw - 1.05), 14), RUBBER)
        # Missiles de croisière sous l'aile intérieure.
        for k in range(3):
            store(b, s * (2.3 + k * 0.45), 1.6 - k * 0.3, zw - 0.75 - (k % 2) * 0.35, 0.18, 4.2, "missile", 0.25 if k == 1 else 0)
        flank_text(b, "EDF", 1.5, -10.6, zw + 3.4, s * 0.3, s)
        flank_text(b, "VESTA  B-07", 0.45, -10.6, zw + 1.9, s * 0.28, s)
        text(b, "E D F   STRATEGIC", 0.6, lambda a, bb, c, s=s: _cyl_u(1.4, s, a, bb, c, 3.5, 0.15, 0.2))
    # Empennages et tourelle de queue.
    zt = 0.9
    for w in wing([(0.0, -8.6, 4.0, zt, 0.1), (7.0, -11.0, 1.6, zt + 0.1, 0.09)]):
        b.add(w, PAINT)
    b.add(fin([(zt + 0.5, -6.4, 6.5, 0.12), (zt + 4.0, -9.6, 4.0, 0.11), (zt + 7.0, -12.0, 2.0, 0.1)]), PAINT)
    b.add(lofx([(-12.6, 0, 0.8, 0.45, 0.5, 2), (-13.6, 0, 0.8, 0.35, 0.4, 2)], 16), ACCENT)
    b.add(tube(0.06, 0.05, -15.2, -13.5, 0.0, 0.8, 8), METAL)
    b.add(box(0.5, 0.06, 0.3, (0, -13.6, 1.1), 0.02), GLASS)
    b.add(box(1.6, 7.0, 0.06, (0, 0.5, -1.27), 0.03), ACCENT)                    # trappes de soute


def _phobos(b):
    """Bombardier stratégique géant type B-2 : aile volante à bord de fuite en dents de scie, bosse de cockpit vitrée,
    quatre réacteurs enfouis (bosses, entrées d'air en S, tuyères en tranchée), trappes de soute, feux."""
    span = 26.0
    tl = math.tan(math.radians(33))
    te = [(0.0, -11.0), (4.0, -7.2), (8.5, -11.2), (13.0, -7.0), (span, -6.6)]

    def te_u(x):
        for (x0, u0), (x1, u1) in zip(te, te[1:]):
            if x0 <= x <= x1:
                return u0 + (u1 - u0) * (x - x0) / (x1 - x0)
        return te[-1][1]

    xs = [0.0, 1.5, 3.0, 4.0, 6.0, 8.5, 10.5, 13.0, 16.0, 19.5, 23.0, span]
    sec = []
    for x in xs:
        ule = 11.0 - x * tl
        ch = ule - te_u(x)
        th = 0.075 if x < 3.0 else 0.072 - (x - 3.0) / span * 0.025
        sec.append((x, ule, max(ch, 0.6), -x * 0.02, th))
    for w in wing(sec):
        b.add(w, PAINT)
    # Bosse de cockpit et vitrages.
    b.add(lofx([(10.4, 0, 0.0, 0.3, 0.3, 2), (8.5, 0, 0.6, 1.6, 1.0, 2.4), (5.0, 0, 0.7, 2.2, 1.35, 2.6), (0.0, 0, 0.6, 2.4, 1.3, 2.6),
                (-5.0, 0, 0.3, 1.8, 0.9, 2.4), (-9.0, 0, 0.0, 0.5, 0.3, 2)], 26), PAINT)
    for s in (-1, 1):
        for k in range(2):
            b.add(xf(box(0.08, 0.9, 0.45, (0, 0, 0), 0.04), at=(s * (0.75 + k * 0.6), 7.6 - k * 0.5, 1.45 - k * 0.05), ru=-s * 30, rx=-20), GLASS)
    # Bosses moteurs, entrées d'air, tranchées d'éjection.
    for s in (-1, 1):
        for x in (3.6, 6.4):
            xs_ = s * x
            ule = 11.0 - x * tl
            b.add(lofx([(ule - 1.2, xs_, 0.4, 0.5, 0.25, 2.4), (ule - 3.0, xs_, 0.85, 1.1, 0.75, 2.4), (ule - 8.0, xs_, 0.75, 1.0, 0.7, 2.4),
                        (te_u(x) + 1.0, xs_, 0.35, 0.6, 0.3, 2.4)], 22), PAINT)
            b.add(xf(box(1.6, 0.4, 0.4, (0, 0, 0), 0.1), at=(xs_, ule - 2.6, 1.35), rx=25), POLY)   # entrée d'air
            b.add(box(1.7, 0.18, 0.12, (xs_, ule - 2.85, 1.55), 0.06), ALLOY)  # lèvre
        # Tranchée d'éjection (bord de fuite, sombre).
        b.add(box(4.6, 2.4, 0.1, (s * 5.0, te_u(5.0) + 2.3, 0.2), 0.05), ACCENT)
        b.add(box(4.2, 1.6, 0.04, (s * 5.0, te_u(5.0) + 2.4, 0.25), 0.03), GLOW)
        # Trappes de soute et du train sous l'aile.
        b.add(box(2.0, 5.0, 0.08, (s * 1.25, 1.5, -0.85), 0.04), ACCENT)
        b.add(box(1.6, 3.4, 0.08, (s * 7.0, 1.0, -0.62), 0.04), ACCENT)
        b.add(box(0.6, 1.0, 0.2, (s * (span - 0.4), -6.0, -0.45), 0.08), GLOW)    # feux de bout d'aile
        top_text(b, "EDF", 2.4, -2.0, s * 15.0, 0.2, False)
    b.add(box(0.8, 2.0, 0.08, (0, 6.5, -0.55), 0.04), ACCENT)                    # trappe du train avant
    b.add(prism([(-2.0, 1.85), (-1.2, 1.85), (-1.7, 2.4), (-2.1, 2.4)], -0.04, 0.04, 0.02), ACCENT)   # antenne dorsale
    b.add(box(0.5, 0.8, 0.06, (0, 3.2, 1.97), 0.03), ACCENT)                     # réceptacle de ravitaillement
    for (u, z) in ((-3.0, 1.75), (2.0, -0.5)):
        b.add(ball(0.15, (0, u, z), 10), GLOW)
    text(b, "PHOBOS", 0.9, lambda a, bb, c: (a, 0.5 + bb, 1.92 + c))


OBJECTS = ["zegun", "zelauncher", "zesniper", "zeblaster", "robotbomb", "beetle", "patroller", "speedstar",
           "lifevendor", "powerpost", "guardpost", "decoy", "gunship", "km6", "kamuy", "vesta", "phobos"]
MARK_AT.update({o: None for o in OBJECTS})


def make_object(name, out=None):
    if name.startswith("ze"): sentry(name, name)
    elif name in ("robotbomb", "beetle", "patroller", "speedstar"): robot(name)
    elif name in ("lifevendor", "powerpost", "guardpost"): post(name)
    elif name == "decoy": decoy()
    else: aircraft(name)
    if out:
        export(out)
    return tris()
