# Fusils à pompe et fusils de précision du Ranger (EDF 5), une fonction par famille de modèle.
# Fusils à pompe : Slaughter = SPAS-12 (pompe, crosse repliable, cartouchière latérale), SGN = semi-auto à tambour (type
# AA-12), G1 Herring = fusil à canons juxtaposés renforcé (bascule, chiens extérieurs), Breecher = fusil de brèche court à
# gros calibre (type Saiga-12 / bouche dentelée), Monsoon = fusil à canons tournants, Slugger Shot = fusil à balles à pompe
# (canon rayé, lunette), Buster Shot / Fracture = modèles hors normes (contenus téléchargeables).
# Précision : KFF = à verrou (type AWM / AI AX), NP = semi-auto (type PSG-1), Lysander = anti-matériel (type M82A1),
# Fang = semi-auto élancé à crosse squelette (type SVD), Dunkel = bullpup (type WA2000), Eagle = semi-auto (type M110),
# Flounders = tir multiple (quatre canons groupés, double chargeur), Ptarmigan = anti-matériel lourd en bullpup (type
# M82A2 / XM500) à bipied, Pylon = expérimental à fût carré et ailettes, Penetrator = fusil électromagnétique perforant.
import math

# ───────────────────────── Outils propres au module ─────────────────────────


def _se(c, p):
    """Composante de superellipse (p = 2 : cercle, p grand : carré arrondi)."""
    return math.copysign(abs(c) ** (2.0 / p), c)


def sweep(secs, seg=16, caps=True, tangent=None):
    """Volume balayé dans le plan latéral (u, z) : secs = [(u, z, demi-largeur x, demi-épaisseur, exposant[, x])] ; chaque
    section est perpendiculaire au chemin (tangente déduite des voisines, ou imposée par tangent = (du, dz))."""
    bm = bmesh.new()
    loops = []
    n = len(secs)
    for i, s in enumerate(secs):
        u, z, hw, hd, p = s[:5]
        xo = s[5] if len(s) > 5 else 0.0
        if tangent:
            tu, tz = tangent
        else:
            a, c = secs[max(i - 1, 0)], secs[min(i + 1, n - 1)]
            tu, tz = c[0] - a[0], c[1] - a[1]
        L = math.hypot(tu, tz) or 1.0
        nu, nz = -tz / L, tu / L
        loop = []
        for k in range(seg):
            ang = 2 * math.pi * k / seg
            x = hw * _se(math.cos(ang), p)
            d = hd * _se(math.sin(ang), p)
            loop.append(bm.verts.new(P(xo + x, u + nu * d, z + nz * d)))
        loops.append(loop)
    for k in range(n - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    if caps:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rev(prof, c=(0.0, 0.0, 0.0), axis='u', seg=20, flutes=0, fd=0.0, caps=True, start=0.0):
    """Pièce de révolution : prof = [(t, r)] le long de l'axe ('u', 'x' ou 'z') à partir du point c (x, u, z) ; flutes :
    nombre de cannelures (molettes, canons cannelés) de profondeur fd."""
    x0, u0, z0 = c
    if flutes:
        seg = max(seg, flutes * 4)
    bm = bmesh.new()
    loops = []
    for (t, r) in prof:
        loop = []
        for k in range(seg):
            a = 2 * math.pi * k / seg + start
            rr = r
            if flutes and r > fd * 2 and math.cos((a - start) * flutes) > 0.7:
                rr = r - fd
            ca, sa = math.cos(a) * rr, math.sin(a) * rr
            if axis == 'u':
                p = (x0 + ca, u0 + t, z0 + sa)
            elif axis == 'x':
                p = (x0 + t, u0 + ca, z0 + sa)
            else:
                p = (x0 + ca, u0 + sa, z0 + t)
            loop.append(bm.verts.new(P(*p)))
        loops.append(loop)
    for k in range(len(loops) - 1):
        A, B = loops[k], loops[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((A[i], A[j], B[j], B[i]))
    if caps:
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rod(p0, p1, r, r1=None, seg=10):
    """Tige (cylindre ou cône) entre deux points quelconques du repère de travail (x, u, z)."""
    a, b = P(*p0), P(*p1)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r1 is None else r1, depth=d.length)
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix()
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return bm


def slab(profile, hw, bevel=0.002, seg=1, x=0.0):
    """Prisme latéral symétrique (profil [(u, z)] extrudé de x − hw à x + hw) aux arêtes chanfreinées (seg = 1) ou arrondies."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x - hw, u, z)) for (u, z) in profile]
    b = [bm.verts.new(P(x + hw, u, z)) for (u, z) in profile]
    n = len(profile)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=seg, affect='EDGES', profile=0.5, clamp_overlap=True)
    return bm


def slab_top(profile, z0, z1, bevel=0.0015, seg=1):
    """Prisme vu de dessus : profil [(u, x)] extrudé de z0 à z1 (freins de bouche en flèche, plaques)."""
    bm = bmesh.new()
    a = [bm.verts.new(P(x, u, z0)) for (u, x) in profile]
    b = [bm.verts.new(P(x, u, z1)) for (u, x) in profile]
    n = len(profile)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=seg, affect='EDGES', profile=0.5, clamp_overlap=True)
    return bm


def grip2(b, u, z, angle=18, h=0.105, w=0.031, d=0.046, mat=POLY, fingers=True, guard=0.068, tail=True, ribs=True):
    """Poignée pistolet détaillée : renflement de paume, creux des doigts, talon évasé, queue de castor, stries latérales,
    pontet et détente ; (u, z) = haut de la poignée (axe). Renvoie le point de la main."""
    a = math.radians(angle)
    du, dz = -math.sin(a), -math.cos(a)
    nu, nz = math.cos(a), -math.sin(a)
    secs = []
    n = 12
    for k in range(n + 1):
        t = k / n * 1.04
        front = d / 2 * (1.0 - 0.14 * t)
        if fingers and 0.18 < t < 0.92:
            front += 0.0032 * (abs(math.sin((t - 0.18) / 0.74 * math.pi * 3)) - 0.6)
        back = -d / 2 * (1.04 + 0.1 * math.sin(min(t, 1) * math.pi))
        hw = w / 2 * (1.0 + 0.1 * math.sin(min(t, 1) * math.pi))
        if t > 0.95:
            front += 0.003
            back -= 0.004
            hw *= 1.08
        c = (front + back) / 2
        secs.append((u + du * h * t + nu * c, z + dz * h * t + nz * c, hw, (front - back) / 2, 2.6))
    b.add(sweep(secs, 16, tangent=(du, dz)), mat)
    if tail:
        bu = u - d * 0.5
        b.add(slab([(bu + 0.004, z + 0.003), (bu - 0.016, z - 0.002), (bu - 0.017, z - 0.009), (bu + 0.002, z - 0.018)], w / 2 * 0.9, 0.002), mat)
    if ribs:
        for k in range(6):
            t = 0.25 + k * 0.11
            cu, cz = u + du * h * t, z + dz * h * t
            for s in (-1, 1):
                hw = w / 2 * (1.0 + 0.1 * math.sin(t * math.pi))
                b.add(rod((s * hw * 0.97, cu - nu * d * 0.42, cz - nz * d * 0.42), (s * hw * 0.97, cu + nu * d * 0.3, cz + nz * d * 0.3), 0.0011, None, 5), mat)
    if guard:
        g0 = u + guard
        gz = z + 0.002
        b.add(sweep([(g0, gz, 0.0042, 0.0026, 2.5), (g0 - 0.002, gz - 0.02, 0.0042, 0.0026, 2.5), (g0 - 0.014, gz - 0.037, 0.0042, 0.0026, 2.5),
                     (u + guard * 0.45, gz - 0.043, 0.0045, 0.0026, 2.5), (u + 0.006, gz - 0.041, 0.0045, 0.0026, 2.5)], 8), METAL)
        b.add(sweep([(u + 0.036, z + 0.004, 0.003, 0.0022, 2.4), (u + 0.034, z - 0.012, 0.003, 0.0022, 2.4),
                     (u + 0.029, z - 0.024, 0.003, 0.0022, 2.4), (u + 0.022, z - 0.029, 0.003, 0.0022, 2.4)], 8), METAL)
    return (0, u + du * h * 0.45, z + dz * h * 0.45)


def optic(b, u, z, l=0.34, r=0.015, obj=0.025, base=0.03, mat=METAL, shade=0.0, caps=False, ill=True, rings=None):
    """Lunette de précision : bonnette caoutchouc, oculaire et bague de grossissement moletée, tube, selle à tourelles
    (élévation, dérive, parallaxe, éclairage), cloche d'objectif, pare-soleil, lentilles, volets rabattables, colliers à
    vis et embases à écrou de serrage sur le rail (dessus à z = base)."""
    ue, uo = u - l / 2, u + l / 2
    c = (0, ue, z)
    b.add(rev([(-0.004, r * 1.28), (0.0, r * 1.45), (0.018, r * 1.5), (0.022, r * 1.42)], c, seg=22), RUBBER)
    b.add(rev([(0.022, r * 1.42), (0.07, r * 1.42), (0.09, r * 1.06), (0.095, r)], c, seg=22), mat)
    b.add(rev([(0.03, r * 1.5), (0.05, r * 1.5)], c, seg=24, flutes=24, fd=0.0012), mat)
    b.add(rev([(-0.002, r * 1.15), (0.0, r * 1.15)], c, seg=20), LENS)
    um = uo - 0.1 - shade
    b.add(rev([(0.0, r), (um - ue, r)], c, seg=22), mat)
    # Selle et tourelles.
    b.add(loft([(u - 0.035, r * 1.12, r * 1.12, z, 3.0), (u - 0.028, r * 1.25, r * 1.25, z, 3.0), (u + 0.028, r * 1.25, r * 1.25, z, 3.0), (u + 0.035, r * 1.12, r * 1.12, z, 3.0)], 20), mat)
    b.add(rev([(0, r * 0.75), (r * 1.25 + 0.006, r * 0.75)], (0, u, z), 'z', 16), mat)
    b.add(rev([(r * 1.25 + 0.005, r * 0.95), (r * 1.25 + 0.018, r * 0.95), (r * 1.25 + 0.02, r * 0.75)], (0, u, z), 'z', 20, flutes=18, fd=0.0011), mat)
    b.add(rev([(0, r * 0.65), (r * 1.25 + 0.004, r * 0.65)], (0, u, z), 'x', 14), mat)
    b.add(rev([(r * 1.25 + 0.003, r * 0.8), (r * 1.25 + 0.014, r * 0.8), (r * 1.25 + 0.016, r * 0.6)], (0, u, z), 'x', 18, flutes=16, fd=0.001), mat)
    b.add(rev([(0, -r * 0.6), (-(r * 1.25 + 0.012), -r * 0.6)], (0, u + 0.004, z), 'x', 14), mat)
    b.add(rev([(-(r * 1.25 + 0.008), r * 0.85), (-(r * 1.25 + 0.016), r * 0.85)], (0, u + 0.004, z), 'x', 18, flutes=14, fd=0.001), mat)
    if ill:
        b.add(rev([(0, r * 0.4), (r * 1.25 + 0.006, r * 0.4)], (0, u - 0.03, z), 'x', 10), mat)
        b.add(rev([(r * 1.25 + 0.006, r * 0.42), (r * 1.25 + 0.008, r * 0.42)], (0, u - 0.03, z), 'x', 10), GLOW)
    # Objectif, pare-soleil, lentille.
    co = (0, um, z)
    lo = uo - um
    b.add(rev([(0, r), (0.045, obj), (lo - 0.008, obj), (lo - 0.004, obj * 1.04), (lo, obj * 1.04), (lo, obj * 0.94), (lo - 0.004, obj * 0.94)], co, seg=24, caps=False), mat)
    b.add(rev([(lo - 0.01, obj * 0.95), (lo - 0.008, obj * 0.95)], co, seg=22), LENS)
    b.add(ring(obj * 1.07, obj * 0.99, uo - 0.03, uo - 0.024, 0, z, 24), RUBBER)
    if caps:
        # Volets ouverts, rabattus sur le flanc gauche (charnière latérale).
        b.add(rev([(0, obj * 0.98), (-0.004, obj * 0.98)], (-(obj + 0.008), uo + obj * 0.95, z), 'x', 18), POLY)
        b.add(box(0.008, 0.012, 0.01, (-(obj + 0.004), uo + 0.002, z), 0.002), POLY)
        b.add(rev([(0, r * 1.4), (-0.004, r * 1.4)], (-(r * 1.5 + 0.008), ue - 0.006 - r * 1.35, z), 'x', 16), POLY)
        b.add(box(0.008, 0.01, 0.01, (-(r * 1.5 + 0.004), ue - 0.006, z), 0.002), POLY)
    # Colliers et embases.
    for uu in rings or (ue + 0.12, um - 0.03):
        b.add(ring(r * 1.32, r * 0.98, uu - 0.009, uu + 0.009, 0, z, 22), mat)
        for s in (-1, 1):
            screw(b, uu, z + r * 0.8, s * r * 1.05, 0.0018, s)
        hh = max(z - r - base, 0.004)
        b.add(slab([(uu - 0.011, base), (uu + 0.011, base), (uu + 0.007, base + hh + r * 0.5), (uu - 0.007, base + hh + r * 0.5)], 0.009, 0.0015), mat)
        b.add(box(0.032, 0.026, 0.009, (0, uu, base + 0.0045), 0.002), mat)
        b.add(rev([(0, 0.0035), (0.006, 0.0035)], (0.016, uu, base + 0.004), 'x', 6), METAL)


def bipod2(b, u, z, deployed=True, leg=0.24, splay=14, rake=10, r=0.0055, mat=METAL, clamp=0.02):
    """Bipied : collier sur le fût, charnière, jambes télescopiques (tube, coulisse moletée, tige), patins caoutchouc ;
    déployé (jambes vers le bas, écartées) ou replié le long du canon."""
    if clamp:
        b.add(rev([(-0.012, clamp + 0.004), (0.012, clamp + 0.004)], (0, u, z + clamp), seg=20), mat)
    b.add(box(0.044, 0.026, 0.014, (0, u, z - 0.002), 0.003), mat)
    b.add(tube_x(0.005, 0.052, (0, u, z - 0.004), 12), ALLOY)
    for s in (-1, 1):
        top = (s * 0.02, u, z - 0.006)
        if deployed:
            dx, du, dz = math.sin(math.radians(splay)) * s, math.sin(math.radians(rake)), -1.0
        else:
            dx, du, dz = 0.0, 1.0, -0.03
        L = math.sqrt(dx * dx + du * du + dz * dz)
        dx, du, dz = dx / L, du / L, dz / L

        def at(t):
            return (top[0] + dx * leg * t, top[1] + du * leg * t, top[2] + dz * leg * t)
        b.add(rod(at(0), at(0.55), r, None, 10), mat)
        b.add(rod(at(0.5), at(0.58), r * 1.35, None, 12), ALLOY)
        b.add(rod(at(0.55), at(0.97), r * 0.72, None, 8), ALLOY)
        b.add(rod(at(0.95), at(1.04), r * 1.4, r * 1.1, 10), RUBBER)
        b.add(rod(at(0.08), at(0.2), r * 1.25, None, 10), mat)


def brake_box(b, u, z=0.0, r=0.018, l=0.075, ports=3, hw=None, hh=None, mat=METAL, top=True):
    """Frein de bouche usiné : corps carré arrondi, lumières latérales traversantes, évents supérieurs, alésage sombre."""
    hw = hw or r * 1.2
    hh = hh or r * 1.02
    body = loft([(u, hw * 0.9, hh * 0.9, z, 4.0), (u + 0.006, hw, hh, z, 4.0), (u + l - 0.004, hw, hh, z, 4.0), (u + l, hw * 0.92, hh * 0.92, z, 4.0)], 24)
    cuts = []
    span = l - 0.02
    for k in range(ports):
        uu = u + 0.012 + span * (k + 0.5) / ports
        cuts.append(box(hw * 3, span / ports * 0.62, hh * 1.3, (0, uu, z), 0.002, 2))
    if top:
        for k in range(ports):
            uu = u + 0.012 + span * (k + 0.5) / ports
            cuts.append(box(hw * 0.5, 0.004, hh * 0.9, (0, uu - span / ports * 0.32 - 0.003, z + hh), 0.0015, 2))
    b.add(carve(body, cuts), mat)
    b.add(rev([(-0.002, r * 0.42), (0.0, r * 0.42)], (0, u + l, z), seg=12), POLY)
    return (0, u + l, z)


def crown(b, u, r, z=0.0, x=0.0, seg=16):
    """Bouche du canon : couronne et âme sombre en retrait."""
    b.add(ring(r, r * 0.55, u - 0.006, u, x, z, seg), METAL)
    b.add(rev([(0, r * 0.56), (0.001, r * 0.56)], (x, u - 0.008, z), seg=seg), POLY)


def shells(b, us, x, z, axis='z', r=0.0105, l=0.068):
    """Cartouches de chasse (étui Accent, culot laiton) : dressées (axis 'z', positions us le long de u, centre en z) ou
    couchées (axis 'u', positions us = hauteurs z, centre en u = z)."""
    for v in us:
        c = (x, v, z - l / 2) if axis == 'z' else (x, z - l / 2, v)
        b.add(rev([(0, r * 0.98), (l - 0.016, r), (l - 0.015, r * 0.97)], c, axis, 12), ACCENT)
        b.add(rev([(l - 0.016, r * 1.02), (l, r * 1.02), (l + 0.0015, r * 1.12), (l + 0.003, r * 1.12)], c, axis, 12), ALLOY)


def stock_adj(b, u0, u1, zt=0.0, zb=-0.075, hw=0.019, mat=PAINT, thumb=True, cheek=True, monopod=True, pad=0.012, holes=2):
    """Crosse de précision réglable : bâti à pans coupés (trou de pouce, évidements), busc réglable sur deux colonnettes à
    molette, plaque de couche caoutchouc à entretoises et ergot bas, monopode télescopique. u0 : avant (boîtier), u1 :
    arrière (plaque de couche) ; renvoie le point d'épaule."""
    L = u0 - u1
    prof = [(u0, zt), (u1 + 0.03, zt - 0.004), (u1 + 0.01, zt - 0.002), (u1 + 0.01, zb - 0.002), (u1 + 0.05, zb), (u1 + L * 0.45, zb + 0.012),
            (u0 - 0.075, zb + 0.004), (u0 - 0.035, zb + 0.022), (u0, -0.045)]
    body = slab(prof, hw, 0.004, 1)
    cuts = []
    if thumb:
        cuts.append(slab([(u0 - 0.05, zb + 0.03), (u0 - 0.085, zb + 0.016), (u0 - 0.12, zb + 0.022), (u0 - 0.115, zt - 0.014), (u0 - 0.06, zt - 0.016)], hw * 2, 0.006, 2))
    for k in range(holes):
        uu = u1 + 0.07 + k * 0.05
        cuts.append(slab([(uu - 0.018, zb + 0.016), (uu + 0.018, zb + 0.02), (uu + 0.014, zt - 0.03), (uu - 0.018, zt - 0.03)], hw * 2, 0.005, 2))
    b.add(carve(body, cuts) if cuts else body, mat)
    if cheek:
        cu0, cu1 = u1 + 0.04, u0 - 0.05
        b.add(slab([(cu0, zt + 0.012), (cu1 - 0.015, zt + 0.014), (cu1, zt + 0.006), (cu1, zt + 0.002), (cu0, zt + 0.002)], hw * 0.95, 0.003, 2), mat)
        for uu in (cu0 + 0.03, cu1 - 0.04):
            b.add(rod((0, uu, zt - 0.006), (0, uu, zt + 0.004), 0.0045, None, 10), ALLOY)
        b.add(rev([(0, 0.008), (0.01, 0.008)], (hw, cu1 - 0.02, zt - 0.012), 'x', 16, flutes=12, fd=0.0012), METAL)
    # Plaque de couche : entretoises, coussin caoutchouc, ergot.
    for k in range(2):
        b.add(slab([(u1 + 0.01 - k * 0.006, zt + 0.002), (u1 + 0.006 - k * 0.006, zt + 0.002), (u1 + 0.006 - k * 0.006, zb), (u1 + 0.01 - k * 0.006, zb)], hw * 1.02, 0.001), METAL if k == 0 else POLY)
    b.add(slab([(u1 - 0.002, zt + 0.006), (u1 - 0.002 - pad, zt + 0.002), (u1 - 0.002 - pad, zb + 0.004), (u1 - 0.002, zb - 0.002)], hw * 1.05, 0.003, 2), RUBBER)
    b.add(slab([(u1 + 0.004, zb), (u1 - 0.014, zb - 0.012), (u1 - 0.016, zb - 0.02), (u1 + 0.006, zb - 0.01)], hw * 0.7, 0.002), METAL)
    if monopod:
        mu = u1 + 0.09
        b.add(rev([(0, 0.009), (0.012, 0.009)], (0, mu, zb + 0.004), 'z', 14), METAL)
        b.add(rev([(-0.03, 0.006), (0, 0.006)], (0, mu, zb + 0.004), 'z', 12), ALLOY)
        b.add(rev([(-0.042, 0.011), (-0.03, 0.011)], (0, mu, zb + 0.004), 'z', 16, flutes=12, fd=0.0015), METAL)
        b.add(rev([(-0.05, 0.008), (-0.042, 0.009)], (0, mu, zb + 0.004), 'z', 12), RUBBER)
    return (0, u1 - 0.002 - pad, (zt + zb) / 2)


def mag_box(b, u, z, h=0.12, w=0.026, d=0.07, bend=0.0, tilt=4, mat=POLY, ribs=3, holes=0, base=POLY, x=0.0):
    """Chargeur détaillé (droit ou courbe) : nervures de flanc, trous témoins, semelle débordante ; (u, z) = haut, renvoie
    son centre."""
    n = 8
    a = math.radians(tilt)
    pf, pb = [], []
    for k in range(n + 1):
        t = k / n
        du = bend * t * t - math.sin(a) * h * t
        pf.append((u + d / 2 + du, z - h * t))
        pb.append((u - d / 2 + du * 0.92, z - h * t))
    body = slab(list(reversed(pf)) + pb, w / 2, 0.003, 1, x=x)
    cuts = []
    for k in range(holes):
        t = 0.25 + 0.5 * k / max(1, holes - 1)
        du = bend * t * t - math.sin(a) * h * t
        cuts.append(tube_x(0.0028, w * 2, (x, u - d * 0.3 + du, z - h * t), 8))
    if cuts:
        body = carve(body, cuts)
    b.add(body, mat)
    for k in range(ribs):
        t = 0.3 + 0.5 * k / max(1, ribs - 1)
        du = bend * t * t - math.sin(a) * h * t
        b.add(box(w + 0.003, d * 0.5, 0.004, (x, u + du + d * 0.08, z - h * t), 0.0012, 1), mat)
    du = bend - math.sin(a) * h
    b.add(slab([(u - d / 2 + du - 0.004, z - h + 0.004), (u + d / 2 + du + 0.004, z - h + 0.004), (u + d / 2 + du + 0.002, z - h - 0.008),
                (u - d / 2 + du - 0.002, z - h - 0.008)], w / 2 + 0.002, 0.0025, 2, x=x), base)
    return (x, u + (bend * 0.25 - math.sin(a) * h * 0.5), z - h / 2)


def reflex(b, u, z, mat=METAL, l=0.06):
    """Viseur holographique compact : embase, capot en arceau, vitre, molette et boutons ; z = dessus du rail."""
    b.add(slab([(u - l / 2, z), (u + l / 2, z), (u + l / 2 - 0.004, z + 0.012), (u - l / 2 + 0.004, z + 0.012)], 0.016, 0.002), mat)
    hood = slab([(u - 0.006, z + 0.008), (u + l / 2, z + 0.008), (u + l / 2, z + 0.046), (u + l / 2 - 0.01, z + 0.05), (u - 0.002, z + 0.05), (u - 0.006, z + 0.044)], 0.019, 0.003, 2)
    b.add(carve(hood, [box(0.032, 0.08, 0.03, (0, u + 0.02, z + 0.029), 0.006, 2)]), mat)
    b.add(box(0.03, 0.002, 0.028, (0, u + l / 2 - 0.006, z + 0.029), 0.0005, 1), GLASS)
    b.add(box(0.026, 0.002, 0.024, (0, u + 0.0, z + 0.029), 0.0005, 1), LENS)
    b.add(rev([(0, 0.008), (0.012, 0.008)], (0.016, u - l / 2 + 0.014, z + 0.012), 'x', 14, flutes=10, fd=0.001), mat)
    for k in range(2):
        b.add(box(0.008, 0.008, 0.005, (0, u - l / 2 + 0.01 + k * 0.012, z + 0.0145), 0.0015), RUBBER)
    b.add(rev([(0, 0.0045), (0.006, 0.0045)], (-0.016, u + 0.01, z + 0.005), 'x', 10), METAL)


def carry_handle(b, u0, u1, zb, zt, mat=METAL, rail_top=True, hw=0.011):
    """Poignée de transport à deux montants ajourés et poutre supérieure (rail facultatif)."""
    for uu in (u0, u1):
        post = slab([(uu - 0.022, zb), (uu + 0.022, zb), (uu + 0.012, zt), (uu - 0.012, zt)], hw, 0.002)
        b.add(carve(post, [tube_x(0.0055, 0.05, (0, uu, (zb + zt) / 2 - 0.002), 12)]), mat)
    b.add(loft([(u0 - 0.014, hw * 0.9, 0.006, zt + 0.004, 4.0), (u0 - 0.008, hw, 0.0075, zt + 0.004, 4.0), (u1 + 0.008, hw, 0.0075, zt + 0.004, 4.0), (u1 + 0.014, hw * 0.9, 0.006, zt + 0.004, 4.0)], 16), mat)
    if rail_top:
        rail(b, u0 - 0.004, u1 + 0.004, zt + 0.011, w=0.02, mat=mat)


def vgrip(b, u, z, h=0.08, mat=POLY):
    """Poignée verticale à nervures et collier sur rail ; renvoie le point de la main."""
    b.add(slab([(u - 0.022, z), (u + 0.022, z), (u + 0.018, z - 0.012), (u - 0.018, z - 0.012)], 0.013, 0.002), mat)
    b.add(rev([(0.0, 0.0145), (-h * 0.2, 0.0135), (-h * 0.85, 0.0145), (-h, 0.0165), (-h - 0.006, 0.015)], (0, u, z - 0.01), 'z', 16, flutes=8, fd=0.0015), mat)
    b.add(rev([(0, 0.0045), (0.005, 0.0045)], (0.013, u, z - 0.006), 'x', 10), METAL)
    return (0, u, z - 0.01 - h * 0.5)


# ───────────────────────── Fusils à pompe ─────────────────────────


def slaughter(g):
    """SPAS-12 : boîtier à pans coupés (fenêtre d'éjection, lumière de chargement), tube magasin à bouchon moleté, devant de
    pompe rainuré, protège-main perforé, guidon à oreilles, crosse repliable en tôle ajourée à crochet, cartouchière."""
    b = g.body
    rcv = slab([(-0.088, 0.012), (-0.07, 0.022), (0.15, 0.022), (0.176, 0.012), (0.176, -0.044), (0.16, -0.054), (0.03, -0.054),
                (0.02, -0.044), (-0.07, -0.044), (-0.088, -0.03)], 0.0195, 0.003)
    cuts = [box(0.02, 0.07, 0.02, (0.02, 0.07, 0.002), 0.004, 2),                     # fenêtre d'éjection
            box(0.024, 0.11, 0.03, (0, 0.098, -0.056), 0.005, 2)]                     # lumière de chargement
    for s in (-1, 1):
        cuts.append(box(0.004, 0.2, 0.005, (s * 0.0205, 0.045, -0.026), 0.002, 2))  # rainure de flanc
    b.add(carve(rcv, cuts), METAL)
    b.add(box(0.013, 0.064, 0.016, (0.007, 0.07, 0.002), 0.002), ALLOY)              # culasse visible
    b.add(rod((0.012, 0.09, 0.002), (0.032, 0.09, 0.002), 0.0035, 0.003, 10), METAL)  # levier de culasse
    b.add(rev([(0, 0.0055), (0.008, 0.0055), (0.01, 0.004)], (0.032, 0.09, 0.002), 'x', 12), METAL)
    b.add(box(0.02, 0.1, 0.004, (0, 0.098, -0.046), 0.001), POLY)                    # élévateur
    pins(b, (-0.06, 0.15), -0.034, 0.0195)
    for uu in (-0.02, 0.12):
        for s in (-1, 1):
            screw(b, uu, 0.012, s * 0.0195, 0.0024, s)
    b.add(box(0.005, 0.012, 0.012, (0.0205, 0.165, -0.03), 0.002), ALLOY)            # sélecteur pompe / semi-auto
    b.add(tube_x(0.0045, 0.044, (0, 0.0, -0.048), 12), METAL)                        # sûreté transversale
    b.add(rev([(0, 0.004), (0.003, 0.004)], (-0.0195, 0.135, -0.03), 'x', 10), ALLOY)  # arrêtoir de culasse
    # Hausse à œilleton.
    b.add(slab([(-0.068, 0.022), (-0.03, 0.022), (-0.036, 0.032), (-0.062, 0.032)], 0.011, 0.002), METAL)
    for s in (-1, 1):
        b.add(slab([(-0.054, 0.03), (-0.042, 0.03), (-0.044, 0.048), (-0.052, 0.048)], 0.002, 0.0008, x=s * 0.01), METAL)
    b.add(ring(0.0065, 0.0025, -0.051, -0.045, 0, 0.04, 14), METAL)
    # Canon, tube magasin, bouchon, collier.
    b.add(rev([(0.0, 0.0125), (0.03, 0.012), (0.444, 0.0115)], (0, 0.176, 0.0), seg=20), METAL)
    crown(b, 0.62, 0.0118)
    b.add(rev([(0.0, 0.0125), (0.41, 0.0125)], (0, 0.176, -0.03), seg=20), METAL)
    b.add(rev([(0.0, 0.0138), (0.024, 0.0138), (0.03, 0.011), (0.031, 0.006)], (0, 0.586, -0.03), seg=20, flutes=16, fd=0.0012), METAL)
    b.add(loft([(0.548, 0.0145, 0.026, -0.015, 3.5), (0.566, 0.0145, 0.026, -0.015, 3.5)], 20), METAL)
    screw(b, 0.557, -0.015, 0.0145, 0.0022, 1)
    for (p0, p1) in (((0, 0.55, -0.04), (0, 0.55, -0.056)), ((0, 0.564, -0.04), (0, 0.564, -0.056)), ((0, 0.549, -0.056), (0, 0.565, -0.056))):
        b.add(rod(p0, p1, 0.0022, None, 6), METAL)
    # Devant de pompe rainuré (butées avant et arrière).
    fe = loft([(0.2, 0.021, 0.021, -0.026, 2.6), (0.212, 0.025, 0.026, -0.026, 2.6), (0.408, 0.025, 0.026, -0.026, 2.6), (0.42, 0.021, 0.021, -0.026, 2.6)], 24)
    cuts = []
    for s in (-1, 1):
        for zz in (-0.036, -0.026, -0.016):
            cuts.append(box(0.01, 0.17, 0.0032, (s * 0.026, 0.31, zz), 0.0015, 2))
    b.add(carve(fe, cuts), PAINT)
    for uu in (0.2, 0.412):
        b.add(rev([(0, 0.0265), (0.008, 0.0275)] if uu < 0.3 else [(0, 0.0275), (0.008, 0.0265)], (0, uu, -0.026), seg=24), PAINT)
    # Protège-main perforé.
    shield = ring(0.0178, 0.0146, 0.432, 0.598, 0, 0.0, 20)
    cuts = []
    for k in range(6):
        cuts += holes_ring(0.447 + k * 0.026, 0.016, 8, 0.0036, 0.012)
    b.add(carve(shield, cuts), METAL)
    for uu in (0.426, 0.598):
        b.add(ring(0.019, 0.0112, uu, uu + 0.008, 0, 0.0, 20), METAL)
    # Guidon à oreilles.
    for s in (-1, 1):
        b.add(slab([(0.588, 0.016), (0.612, 0.016), (0.607, 0.034), (0.594, 0.034)], 0.0018, 0.0006, x=s * 0.0065), METAL)
    b.add(slab([(0.586, 0.011), (0.614, 0.011), (0.612, 0.019), (0.588, 0.019)], 0.009, 0.002), METAL)
    b.add(box(0.003, 0.006, 0.016, (0, 0.6, 0.026), 0.0008, 1), METAL)
    b.add(box(0.0035, 0.002, 0.003, (0, 0.598, 0.033), 0.0005, 1), GLOW)
    grip = grip2(b, -0.045, -0.042, 20, 0.105, 0.031, 0.046, POLY)
    # Crosse repliable : bras en tôle ajourée, pivot, plaque de couche courbe, crochet d'avant-bras.
    for s in (-1, 1):
        arm = slab([(-0.07, 0.008), (-0.356, -0.002), (-0.356, -0.024), (-0.07, -0.014)], 0.0016, 0.0007, x=s * 0.0215)
        cuts = [box(0.02, 0.05, 0.0075, (s * 0.0215, uu, -0.003 + (uu + 0.07) * 0.035 - 0.0015), 0.0034, 2) for uu in (-0.14, -0.21, -0.28)]
        b.add(carve(arm, cuts), METAL)
    b.add(tube_x(0.0075, 0.048, (0, -0.078, -0.003), 14), METAL)
    for s in (-1, 1):
        b.add(rev([(0, 0.0085), (0.002, 0.0085)], (s * 0.024, -0.078, -0.003), 'x', 14), ALLOY)
    b.add(tube_x(0.005, 0.046, (0, -0.35, -0.013), 10), METAL)
    plate = [(-0.348, 0.014), (-0.358, -0.01), (-0.362, -0.04), (-0.358, -0.07), (-0.348, -0.092)]
    b.add(sweep([(u, z, 0.022, 0.009, 3.5) for (u, z) in plate], 16), METAL)
    b.add(sweep([(u - 0.01, z, 0.021, 0.003, 3.5) for (u, z) in plate[:-1]] + [(-0.355, -0.088, 0.021, 0.003, 3.5)], 16), RUBBER)
    b.add(sweep([(-0.352, -0.084, 0.004, 0.005, 2.4), (-0.33, -0.104, 0.004, 0.005, 2.4), (-0.305, -0.116, 0.004, 0.005, 2.4),
                 (-0.288, -0.112, 0.004, 0.005, 2.4), (-0.284, -0.1, 0.004, 0.005, 2.4)], 10), METAL)
    # Cartouchière sur le flanc gauche.
    b.add(slab([(0.03, 0.014), (0.16, 0.014), (0.16, -0.04), (0.03, -0.04)], 0.0018, 0.0008, x=-0.0213), METAL)
    b.add(slab([(0.034, -0.002), (0.156, -0.002), (0.156, -0.03), (0.034, -0.03)], 0.0075, 0.003, 2, x=-0.031), POLY)
    shells(b, (0.051, 0.077, 0.103, 0.129), -0.0315, -0.012)
    laser_module(g.opt("laser"), 0.33, -0.026, -0.034)
    g.point("Muzzle", (0, 0.62, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.31, -0.052))
    g.point("Butt", (0, -0.368, -0.038)); g.point("Mag", (0, 0.098, -0.056))


def sgn(g):
    """Semi-automatique à tambour (type AA-12) : bâti rectiligne du canon à la crosse (flancs évidés, fenêtre d'éjection,
    fente d'armement), poignée de transport à rail et viseur, manchon ajouré, frein de bouche, gros tambour moleté."""
    b = g.body
    body = slab([(-0.385, 0.03), (0.236, 0.03), (0.262, 0.02), (0.262, -0.046), (0.25, -0.05), (-0.08, -0.05), (-0.13, -0.054),
                 (-0.385, -0.086)], 0.027, 0.005, 1)
    cuts = []
    for s in (-1, 1):
        cuts.append(slab([(-0.365, 0.018), (-0.15, 0.018), (-0.15, -0.038), (-0.365, -0.07)], 0.003, 0.004, 2, x=s * 0.027))
        cuts.append(slab([(-0.115, 0.016), (-0.07, 0.016), (-0.07, -0.032), (-0.115, -0.034)], 0.003, 0.003, 2, x=s * 0.027))
        for zz in (0.014, -0.034):
            cuts.append(box(0.004, 0.2, 0.004, (s * 0.027, 0.13, zz), 0.0018, 2))
    cuts.append(box(0.02, 0.07, 0.022, (0.028, 0.115, -0.006), 0.004, 2))            # fenêtre d'éjection
    cuts.append(box(0.012, 0.12, 0.008, (-0.027, 0.16, 0.02), 0.003, 2))             # fente d'armement
    b.add(carve(body, cuts), PAINT)
    b.add(box(0.016, 0.066, 0.018, (0.014, 0.115, -0.006), 0.002), ALLOY)
    b.add(rod((-0.024, 0.2, 0.02), (-0.046, 0.2, 0.02), 0.004, 0.0035, 10), METAL)    # levier d'armement
    b.add(rev([(0, 0.0065), (-0.01, 0.0065), (-0.012, 0.005)], (-0.046, 0.2, 0.02), 'x', 14), METAL)
    pins(b, (-0.03, 0.04, 0.22), -0.03, 0.027, 0.0032)
    for uu in (-0.03, 0.06, 0.18):
        for s in (-1, 1):
            screw(b, uu, 0.02, s * 0.027, 0.0024, s)
    b.add(box(0.006, 0.014, 0.008, (0.028, -0.03, -0.022), 0.002), METAL)            # sélecteur
    b.add(slab([(-0.385, 0.032), (-0.4, 0.03), (-0.4, -0.084), (-0.385, -0.088)], 0.028, 0.003, 2), RUBBER)
    for k in range(5):
        b.add(box(0.058, 0.002, 0.003, (0, -0.401, 0.018 - k * 0.022), 0.0005, 1), POLY)
    b.add(rod((0, -0.33, -0.08), (0, -0.33, -0.1), 0.0025, None, 6), METAL)
    b.add(tube_x(0.006, 0.02, (0, -0.33, -0.1), 10), METAL)
    # Poignée de transport, viseur.
    carry_handle(b, -0.04, 0.2, 0.03, 0.062, METAL)
    reflex(b, 0.05, 0.081)
    # Manchon ajouré, canon, frein.
    b.add(rev([(0, 0.0135), (0.26, 0.013)], (0, 0.262, -0.004), seg=18), METAL)
    sh = loft([(0.262, 0.026, 0.026, -0.006, 3.6), (0.42, 0.026, 0.026, -0.006, 3.6), (0.43, 0.022, 0.022, -0.006, 3.6)], 24)
    cuts = []
    for s in (-1, 1):
        cuts += slots(0.28, 0.41, 5, s * 0.026, -0.006, 0.014, 0.018, 0.012)
    cuts += slots(0.28, 0.41, 5, 0, 0.02, 0.014, 0.014, 0.012, axis='z')
    b.add(carve(sh, cuts), METAL)
    b.add(slab([(0.405, 0.018), (0.425, 0.018), (0.42, 0.036), (0.41, 0.036)], 0.0035, 0.001), METAL)
    brake_box(b, 0.52, -0.004, 0.019, 0.072, 3)
    b.add(rod((0, 0.4, -0.03), (0, 0.4, -0.044), 0.0025, None, 6), METAL)
    b.add(tube_x(0.006, 0.02, (0, 0.4, -0.044), 10), METAL)
    grip = grip2(b, -0.035, -0.048, 16, 0.105, 0.032, 0.048, POLY)
    # Tambour : jante moletée, flasques vissés, moyeu de remontage, col d'alimentation.
    du, dz = 0.095, -0.144
    b.add(slab([(du - 0.032, -0.048), (du + 0.032, -0.048), (du + 0.026, -0.08), (du - 0.026, -0.08)], 0.016, 0.003), POLY)
    b.add(rev([(-0.038, 0.066), (-0.035, 0.072), (0.035, 0.072), (0.038, 0.066)], (0, du, dz), 'x', 24, flutes=36, fd=0.002), POLY)
    for s in (-1, 1):
        b.add(rev([(s * 0.037, 0.066), (s * 0.041, 0.06), (s * 0.041, 0.028), (s * 0.044, 0.024), (s * 0.044, 0.012)], (0, du, dz), 'x', 32), POLY)
        b.add(rev([(s * 0.044, 0.012), (s * 0.05, 0.011), (s * 0.052, 0.008)], (0, du, dz), 'x', 16, flutes=8, fd=0.002), METAL)
        for k in range(6):
            a = 2 * math.pi * k / 6 + 0.3
            screw(b, du + math.cos(a) * 0.05, dz + math.sin(a) * 0.05, s * 0.041, 0.0024, s)
    laser_module(g.opt("laser"), 0.34, -0.006, -0.034)
    g.point("Muzzle", (0, 0.592, -0.004)); g.point("Grip", grip); g.point("Support", (0, 0.345, -0.034))
    g.point("Butt", (0, -0.4, -0.028)); g.point("Mag", (0, du, dz))


def herring(g):
    """Canons juxtaposés renforcés : bascule claire à platines gravées, clé d'ouverture, chiens extérieurs, double détente,
    canons cerclés de colliers vissés, bande ventilée et guidon perle, devant quadrillé, crosse à poignée pistolet et
    cartouchière de crosse."""
    b = g.body
    act = slab([(-0.072, 0.014), (0.052, 0.014), (0.056, 0.004), (0.056, -0.012), (0.042, -0.03), (-0.02, -0.036), (-0.06, -0.032),
                (-0.075, -0.016)], 0.0265, 0.004, 2)
    cuts = []
    for s in (-1, 1):
        cuts.append(slab([(-0.056, 0.006), (0.034, 0.006), (0.042, -0.006), (0.03, -0.022), (-0.046, -0.024)], 0.0007, 0.001, x=s * 0.0265))
    b.add(carve(act, cuts), ALLOY)
    for s in (-1, 1):
        for (uu, zz) in ((-0.04, -0.008), (0.02, -0.012)):
            screw(b, uu, zz, s * 0.0262, 0.0026, s)
    b.add(tube_x(0.0135, 0.056, (0, 0.05, -0.016), 18), ALLOY)                       # charnière
    b.add(slab_top([(-0.02, -0.004), (-0.02, 0.004), (-0.056, 0.008), (-0.07, 0.022), (-0.064, 0.027), (-0.05, 0.012), (-0.03, 0.006)], 0.014, 0.02, 0.0012), METAL)
    b.add(rev([(0, 0.006), (0.007, 0.006)], (0, -0.022, 0.014), 'z', 12), METAL)
    for s in (-1, 1):
        b.add(slab([(-0.044, 0.006), (-0.03, 0.006), (-0.036, 0.024), (-0.05, 0.034), (-0.064, 0.036), (-0.066, 0.03), (-0.05, 0.024)], 0.0028, 0.0008, x=s * 0.012), METAL)
    # Canons, bandes, colliers, guidon.
    for s in (-1, 1):
        b.add(rev([(0, 0.0138), (0.04, 0.0132), (0.63, 0.012)], (s * 0.0128, 0.056, 0.0), seg=18), METAL)
        crown(b, 0.686, 0.012, 0.0, s * 0.0128, 16)
    rib = slab([(0.06, 0.009), (0.686, 0.007), (0.686, 0.016), (0.07, 0.019)], 0.0045, 0.0008)
    b.add(carve(rib, [box(0.02, 0.016, 0.005, (0, 0.09 + k * 0.03, 0.0115 - k * 0.00008), 0.002, 2) for k in range(20)]), METAL)
    b.add(slab([(0.06, -0.008), (0.686, -0.007), (0.686, -0.012), (0.06, -0.014)], 0.004, 0.0008), METAL)
    b.add(rev([(0, 0.0022), (0.003, 0.0022), (0.004, 0.0012)], (0, 0.678, 0.016), 'z', 10), ALLOY)
    for uu in (0.33, 0.5, 0.655):
        b.add(loft([(uu, 0.026, 0.0145, 0.0, 6.0), (uu + 0.016, 0.026, 0.0145, 0.0, 6.0)], 24), METAL)
        for s in (-1, 1):
            screw(b, uu + 0.008, 0.0, s * 0.026, 0.0022, s)
    # Devant quadrillé, verrou de devant.
    fe = loft([(0.062, 0.022, 0.012, -0.018, 2.6), (0.08, 0.029, 0.017, -0.02, 2.6), (0.3, 0.028, 0.016, -0.02, 2.6), (0.32, 0.022, 0.012, -0.018, 2.6)], 24)
    cuts = []
    for s in (-1, 1):
        for zz in (-0.026, -0.02, -0.014):
            cuts.append(box(0.008, 0.18, 0.0022, (s * 0.029, 0.19, zz), 0.001, 2))
    b.add(carve(fe, cuts), PAINT)
    b.add(box(0.012, 0.02, 0.006, (0, 0.21, -0.036), 0.002), ALLOY)
    # Poignée, double détente, crosse, cartouchière.
    grip = grip2(b, -0.06, -0.03, 16, 0.1, 0.03, 0.044, PAINT, fingers=False, guard=0.085, tail=False)
    b.add(sweep([(-0.012, -0.026, 0.003, 0.0022, 2.4), (-0.014, -0.04, 0.003, 0.0022, 2.4), (-0.019, -0.051, 0.003, 0.0022, 2.4), (-0.026, -0.055, 0.003, 0.0022, 2.4)], 8), METAL)
    stock_sculpt(b, [(-0.07, 0.022, 0.022, -0.006), (-0.12, 0.019, 0.02, -0.016), (-0.2, 0.02, 0.03, -0.028), (-0.33, 0.022, 0.05, -0.036),
                     (-0.345, 0.0215, 0.05, -0.036)], PAINT)
    b.add(slab([(-0.198, -0.01), (-0.3, -0.013), (-0.3, -0.066), (-0.198, -0.056)], 0.004, 0.002, 2, x=0.0235), POLY)
    shells(b, (-0.218, -0.244, -0.27), 0.0305, -0.036, 'z', 0.0098, 0.06)
    for (p0, p1) in (((0, -0.27, -0.082), (0, -0.27, -0.09)), ((0, 0.28, -0.034), (0, 0.28, -0.042))):
        b.add(rod(p0, p1, 0.0022, None, 6), METAL)
        b.add(tube_x(0.0055, 0.016, (0, p1[1], p1[2]), 10), METAL)
    laser_module(g.opt("laser"), 0.22, -0.02, -0.035)
    g.point("Muzzle", (0, 0.686, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.2, -0.04))
    g.point("Butt", (0, -0.357, -0.036)); g.point("Mag", (0, 0.04, -0.01))


def breecher(g):
    """Fusil de brèche court (type Saiga-12) : boîtier embouti à rivets et couvercle à rail, grand levier de sûreté, garde-main
    à rails, canon épais, bouche de brèche dentelée et ajourée, gros chargeur courbe, poignée verticale, crosse triangulaire
    repliable, viseur holographique."""
    b = g.body
    rcv = slab([(-0.1, 0.012), (0.18, 0.012), (0.19, 0.002), (0.19, -0.044), (0.13, -0.05), (0.04, -0.05), (-0.1, -0.042)], 0.022, 0.003)
    b.add(carve(rcv, [box(0.016, 0.07, 0.02, (0.022, 0.1, 0.0), 0.003, 2)]), METAL)
    b.add(box(0.014, 0.064, 0.016, (0.012, 0.1, 0.0), 0.002), ALLOY)
    b.add(loft([(-0.104, 0.017, 0.01, 0.016, 3.0), (-0.098, 0.0205, 0.013, 0.017, 3.0), (0.17, 0.0205, 0.013, 0.017, 3.0), (0.176, 0.018, 0.011, 0.016, 3.0)], 20), METAL)
    rail(b, -0.09, 0.165, 0.029)
    b.add(rod((0.022, 0.15, 0.004), (0.04, 0.15, 0.004), 0.0035, 0.003, 8), METAL)    # levier d'armement
    b.add(slab([(-0.07, 0.006), (0.08, 0.006), (0.085, -0.003), (0.03, -0.002), (-0.07, 0.0)], 0.0012, 0.0005, x=0.0235), METAL)
    b.add(slab([(-0.072, 0.003), (-0.058, 0.003), (-0.06, -0.014), (-0.072, -0.012)], 0.0012, 0.0005, x=0.0235), METAL)
    for (uu, zz) in ((-0.085, -0.03), (-0.06, -0.034), (0.03, -0.04), (0.15, -0.035), (0.17, -0.02), (0.0, -0.012)):
        for s in (-1, 1):
            screw(b, uu, zz, s * 0.022, 0.0022, s)
    b.add(slab([(0.142, -0.048), (0.15, -0.048), (0.152, -0.062), (0.142, -0.064)], 0.006, 0.001), METAL)     # arrêtoir
    # Garde-main à rails, canon, embase, guidon, bouche de brèche.
    hg = loft([(0.19, 0.024, 0.026, -0.008, 6.0), (0.36, 0.024, 0.026, -0.008, 6.0)], 24)
    cuts = []
    for s in (-1, 1):
        cuts += slots(0.205, 0.345, 4, s * 0.024, -0.008, 0.02, 0.012, 0.008)
    b.add(carve(hg, cuts), POLY)
    rail(b, 0.2, 0.355, 0.018, mat=POLY)
    for s in (-1, 1):
        rail(b, 0.2, 0.355, -0.008, x=s * 0.024, mat=POLY, side=True)
    b.add(rev([(0, 0.0175), (0.1, 0.017)], (0, 0.36, -0.004), seg=20), METAL)
    b.add(loft([(0.37, 0.016, 0.022, -0.01, 4.0), (0.395, 0.016, 0.022, -0.01, 4.0)], 18), METAL)
    b.add(slab([(0.375, 0.01), (0.392, 0.01), (0.389, 0.04), (0.379, 0.04)], 0.004, 0.001), METAL)
    br = rev([(0, 0.021), (0.008, 0.027), (0.07, 0.027), (0.074, 0.025), (0.074, 0.019), (0.07, 0.019)], (0, 0.46, -0.004), seg=24, caps=False)
    cuts = holes_ring(0.49, 0.026, 6, 0.0055, 0.014, -0.004) + holes_ring(0.512, 0.026, 6, 0.0045, 0.014, -0.004)
    b.add(carve(br, cuts), METAL)
    b.add(rev([(0.06, 0.0195), (0.062, 0.0195)], (0, 0.46, -0.004), seg=20), POLY)
    for k in range(6):
        a = 2 * math.pi * k / 6 + math.pi / 6
        c0 = (math.cos(a) * 0.023, 0.533, -0.004 + math.sin(a) * 0.023)
        b.add(rod(c0, (c0[0] * 1.05, 0.552, c0[2] * 1.05 + 0.0002), 0.0048, 0.0012, 6), METAL)
    grip = grip2(b, -0.045, -0.042, 18, 0.105, 0.032, 0.046, POLY)
    mag = mag_box(b, 0.09, -0.05, 0.14, 0.034, 0.072, 0.035, 2, POLY, ribs=3, holes=3)
    sv = vgrip(b, 0.305, -0.034, 0.08)
    # Crosse triangulaire repliable (tubes, plaque de couche, appui-joue).
    b.add(slab([(-0.1, 0.012), (-0.124, 0.006), (-0.124, -0.04), (-0.1, -0.042)], 0.018, 0.003), METAL)
    b.add(tube_x(0.006, 0.042, (0, -0.118, -0.036), 12), ALLOY)
    b.add(rod((0, -0.122, 0.0), (0, -0.36, -0.012), 0.0085, None, 14), METAL)
    b.add(rod((0, -0.122, -0.034), (0, -0.36, -0.096), 0.0075, None, 14), METAL)
    b.add(rod((0, -0.2, -0.004), (0, -0.25, -0.062), 0.005, None, 10), METAL)
    b.add(slab([(-0.354, 0.004), (-0.372, 0.002), (-0.374, -0.108), (-0.356, -0.106)], 0.017, 0.003, 2), METAL)
    b.add(slab([(-0.372, 0.004), (-0.382, 0.002), (-0.384, -0.106), (-0.374, -0.11)], 0.0175, 0.003, 2), RUBBER)
    b.add(sweep([(-0.17, -0.004, 0.013, 0.012, 2.6), (-0.21, -0.006, 0.0135, 0.0125, 2.6), (-0.29, -0.009, 0.0135, 0.0125, 2.6), (-0.32, -0.011, 0.012, 0.011, 2.6)], 16), POLY)
    reflex(b, 0.0, 0.0355)
    laser_module(g.opt("laser"), 0.27, -0.008, -0.034)
    g.point("Muzzle", (0, 0.552, -0.004)); g.point("Grip", grip); g.point("SupportV", sv)
    g.point("Butt", (0, -0.384, -0.05)); g.point("Mag", mag)


def monsoon(g):
    """Fusil à canons tournants : six canons en barillet sur plateaux festonnés et axe central, gros barillet-magasin cannelé
    coaxial (type Striker), carénage supérieur ventilé et poignée de transport, poignée verticale sur étrier, crosse
    squelette fixe."""
    b = g.body
    zc = -0.006
    rcv = slab([(-0.115, 0.024), (0.06, 0.024), (0.085, 0.016), (0.085, -0.048), (0.05, -0.054), (-0.115, -0.048)], 0.029, 0.004, 2)
    cuts = []
    for s in (-1, 1):
        cuts += slots(-0.09, 0.03, 4, s * 0.029, -0.012, 0.01, 0.026, 0.006)
    cuts.append(box(0.02, 0.06, 0.02, (0.03, 0.03, 0.002), 0.004, 2))
    b.add(carve(rcv, cuts), PAINT)
    b.add(box(0.014, 0.056, 0.016, (0.02, 0.03, 0.002), 0.002), ALLOY)
    for s in (-1, 1):
        screw(b, -0.1, 0.012, s * 0.029, 0.0026, s)
        screw(b, 0.07, -0.036, s * 0.029, 0.0026, s)
    pins(b, (-0.098, 0.064), -0.03, 0.029)
    # Barillet-magasin coaxial (cannelé), plaques, clé de remontage, axe.
    b.add(rev([(0, 0.05), (0.004, 0.058), (0.012, 0.064)], (0, 0.085, zc), seg=36), METAL)
    b.add(rev([(0, 0.064), (0.13, 0.064)], (0, 0.097, zc), seg=36, flutes=12, fd=0.007), PAINT)
    b.add(rev([(0, 0.064), (0.01, 0.058), (0.014, 0.04)], (0, 0.227, zc), seg=36), METAL)
    b.add(rev([(0, 0.012), (0.02, 0.012), (0.024, 0.009)], (0.03, 0.241, zc - 0.03), seg=12, flutes=6, fd=0.002), METAL)
    b.add(rev([(0, 0.016), (0.4, 0.016)], (0, 0.24, zc), seg=16), METAL)
    # Canons, plateaux festonnés, carénage supérieur, poignée de transport.
    for k in range(6):
        a = 2 * math.pi * k / 6
        x, z = math.cos(a) * 0.026, zc + math.sin(a) * 0.026
        b.add(rev([(0, 0.0105), (0.36, 0.0098)], (x, 0.24, z), seg=12), METAL)
        crown(b, 0.6, 0.0098, z, x, 12)
    for uu, w in ((0.25, 0.012), (0.42, 0.01), (0.565, 0.014)):
        b.add(rev([(0, 0.039), (w, 0.039)], (0, uu, zc), seg=36, flutes=6, fd=0.008), METAL)
    b.add(rev([(0, 0.008), (0.008, 0.01), (0.012, 0.006)], (0, 0.6, zc), seg=12), ALLOY)
    sh = loft([(0.24, 0.046, 0.024, zc + 0.026, 2.4), (0.52, 0.046, 0.024, zc + 0.026, 2.4)], 28)
    sh = carve(sh, [box(0.2, 0.4, 0.06, (0, 0.38, zc), 0, 1), loft([(0.2, 0.041, 0.041, zc, 2.0), (0.56, 0.041, 0.041, zc, 2.0)], 28)]
               + slots(0.27, 0.5, 6, 0, zc + 0.05, 0.016, 0.03, 0.03, axis='z'))
    b.add(sh, PAINT)
    carry_handle(b, -0.05, 0.16, 0.024, 0.07, METAL, True, 0.012)
    # Étrier inférieur et poignée verticale.
    b.add(slab([(0.23, -0.04), (0.4, -0.04), (0.4, -0.05), (0.23, -0.05)], 0.012, 0.002), METAL)
    b.add(slab([(0.23, -0.035), (0.25, -0.035), (0.25, -0.05), (0.23, -0.05)], 0.014, 0.002), METAL)
    sv = vgrip(b, 0.33, -0.05, 0.08)
    grip = grip2(b, -0.035, -0.048, 16, 0.105, 0.032, 0.048, POLY)
    # Crosse squelette fixe.
    st = slab([(-0.115, 0.018), (-0.385, 0.012), (-0.385, -0.1), (-0.33, -0.1), (-0.16, -0.05), (-0.115, -0.044)], 0.017, 0.004, 2)
    b.add(carve(st, [slab([(-0.15, 0.004), (-0.36, -0.002), (-0.36, -0.08), (-0.33, -0.084), (-0.17, -0.036)], 0.03, 0.006, 2)]), POLY)
    b.add(slab([(-0.385, 0.014), (-0.398, 0.012), (-0.398, -0.098), (-0.385, -0.102)], 0.019, 0.003, 2), RUBBER)
    b.add(slab([(-0.24, 0.016), (-0.36, 0.014), (-0.36, 0.024), (-0.25, 0.026)], 0.016, 0.003, 2), POLY)
    g.point("Muzzle", (0, 0.6, zc)); g.point("Grip", grip); g.point("SupportV", sv)
    g.point("Butt", (0, -0.398, -0.04)); g.point("Mag", (0, 0.16, zc - 0.064))


def slugshot(g):
    """Fusil à balles à pompe (type 870 « slug ») : boîtier arrondi à longue fenêtre d'éjection, montage cantilever tenu par
    un collier de canon, lunette, canon rayé cannelé à frein de bouche et guidon fibre, devant « épi de maïs », tube magasin
    prolongé, crosse Monte-Carlo."""
    b = g.body
    rcv = slab([(-0.09, 0.012), (-0.075, 0.021), (0.15, 0.021), (0.166, 0.014), (0.166, -0.034), (0.12, -0.044), (0.02, -0.044),
                (-0.02, -0.038), (-0.09, -0.034)], 0.018, 0.004, 2)
    cuts = [box(0.016, 0.085, 0.02, (0.018, 0.065, 0.0), 0.004, 2), box(0.022, 0.09, 0.02, (0, 0.075, -0.046), 0.004, 2)]
    for s in (-1, 1):
        cuts.append(box(0.004, 0.2, 0.004, (s * 0.018, 0.04, -0.024), 0.0018, 2))
    b.add(carve(rcv, cuts), METAL)
    b.add(box(0.012, 0.08, 0.016, (0.008, 0.065, 0.0), 0.002), ALLOY)
    b.add(box(0.018, 0.085, 0.004, (0, 0.075, -0.038), 0.001), POLY)
    pins(b, (-0.07, 0.14), -0.03, 0.018)
    b.add(tube_x(0.0042, 0.04, (0, 0.006, -0.04), 12), METAL)                        # sûreté transversale
    b.add(box(0.004, 0.016, 0.006, (-0.0185, 0.03, -0.036), 0.0015), METAL)          # verrou de pompe
    # Montage cantilever, collier, rail, lunette.
    b.add(loft([(0.172, 0.0165, 0.0168, 0.002, 3.0), (0.232, 0.0165, 0.0168, 0.002, 3.0)], 20), METAL)
    for s in (-1, 1):
        for uu in (0.185, 0.219):
            screw(b, uu, 0.0, s * 0.0165, 0.0022, s)
    b.add(slab([(-0.07, 0.021), (0.23, 0.021), (0.23, 0.03), (-0.07, 0.03)], 0.0095, 0.0015), METAL)
    rail(b, -0.065, 0.225, 0.03)
    optic(b, 0.075, 0.07, 0.3, 0.015, 0.022, base=0.039)
    # Canon cannelé, frein, guidon fibre, tube magasin.
    b.add(rev([(0, 0.0128), (0.03, 0.0125), (0.134, 0.0124)], (0, 0.166, 0.0), seg=20), METAL)
    b.add(rev([(0, 0.0124), (0.42, 0.0118)], (0, 0.3, 0.0), seg=24, flutes=8, fd=0.0016), METAL)
    b.add(rev([(0, 0.0125), (0.03, 0.0125)], (0, 0.72, 0.0), seg=20), METAL)
    brake_box(b, 0.75, 0.0, 0.0165, 0.06, 2)
    b.add(slab([(0.722, 0.011), (0.745, 0.011), (0.742, 0.022), (0.73, 0.022)], 0.0035, 0.0008), METAL)
    b.add(rod((0, 0.729, 0.021), (0, 0.742, 0.021), 0.0013, None, 6), GLOW)
    b.add(rev([(0, 0.0115), (0.374, 0.0115)], (0, 0.166, -0.027), seg=18), METAL)
    b.add(rev([(0.0, 0.0125), (0.022, 0.0125), (0.027, 0.01), (0.028, 0.005)], (0, 0.54, -0.027), seg=18, flutes=14, fd=0.0011), METAL)
    b.add(loft([(0.49, 0.013, 0.024, -0.013, 3.5), (0.505, 0.013, 0.024, -0.013, 3.5)], 18), METAL)
    # Devant « épi de maïs ».
    fe = loft([(0.24, 0.02, 0.019, -0.022, 2.4), (0.25, 0.024, 0.023, -0.022, 2.4), (0.43, 0.024, 0.023, -0.022, 2.4), (0.44, 0.02, 0.019, -0.022, 2.4)], 24)
    b.add(carve(fe, [ring(0.032, 0.0228, 0.262 + k * 0.019, 0.267 + k * 0.019, 0, -0.022, 24) for k in range(9)]), PAINT)
    grip = grip2(b, -0.05, -0.036, 20, 0.1, 0.03, 0.046, PAINT, tail=False)
    stock_sculpt(b, [(-0.09, 0.018, 0.024, -0.008), (-0.15, 0.019, 0.028, -0.018), (-0.33, 0.022, 0.05, -0.032), (-0.35, 0.021, 0.05, -0.032)], PAINT)
    b.add(loft([(-0.13, 0.015, 0.006, 0.008, 3.0), (-0.17, 0.0175, 0.012, 0.012, 3.0), (-0.3, 0.019, 0.012, 0.014, 3.0), (-0.33, 0.018, 0.008, 0.012, 3.0)], 18), PAINT)
    for (uu, zz) in ((-0.28, -0.075), (0.47, -0.04)):
        b.add(rod((0, uu, zz + 0.006), (0, uu, zz - 0.004), 0.0025, None, 6), METAL)
        b.add(tube_x(0.0055, 0.016, (0, uu, zz - 0.004), 10), METAL)
    laser_module(g.opt("laser"), 0.38, -0.022, -0.034)
    g.point("Muzzle", (0, 0.81, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.34, -0.046))
    g.point("Butt", (0, -0.362, -0.032)); g.point("Mag", (0, 0.075, -0.046))


def bustershot(g):
    """Buster Shot (contenu téléchargeable) : fusil à pompe géant en bullpup (type KSG) : bâti massif à pans coupés, deux
    tubes magasins visibles par des fenêtres latérales, canon carré à ailettes, énorme frein carré, pompe à poignée
    verticale, rail et viseur."""
    b = g.body
    body = slab([(-0.37, 0.026), (-0.34, 0.034), (0.13, 0.034), (0.17, 0.024), (0.17, -0.066), (0.03, -0.066), (-0.06, -0.072),
                 (-0.37, -0.096)], 0.034, 0.006, 1)
    cuts = []
    for s in (-1, 1):
        cuts.append(slab([(-0.3, -0.03), (0.13, -0.03), (0.13, -0.056), (-0.3, -0.056)], 0.009, 0.005, 2, x=s * 0.034))
        cuts.append(box(0.006, 0.36, 0.004, (s * 0.034, -0.08, 0.018), 0.002, 2))
        cuts += slots(-0.33, -0.2, 3, s * 0.034, 0.0, 0.026, 0.014, 0.008)
    cuts.append(box(0.03, 0.09, 0.03, (0, -0.15, -0.09), 0.006, 2))                  # éjection basse
    b.add(carve(body, cuts), PAINT)
    for s in (-1, 1):
        for uu in (-0.26, -0.185, -0.11, -0.035, 0.04):
            shells(b, (-0.043,), s * 0.0185, uu, 'u', 0.0125, 0.068)
        for uu in (-0.31, 0.15):
            screw(b, uu, 0.02, s * 0.034, 0.003, s)
    b.add(slab([(-0.37, 0.028), (-0.388, 0.026), (-0.388, -0.094), (-0.37, -0.098)], 0.035, 0.004, 2), RUBBER)
    rail(b, -0.3, 0.13, 0.034)
    reflex(b, 0.02, 0.0435)
    b.add(slab([(-0.28, 0.043), (-0.25, 0.043), (-0.255, 0.062), (-0.272, 0.062)], 0.012, 0.002), METAL)   # hausse de secours
    # Canon carré à ailettes, tubes, collier, frein.
    b.add(loft([(0.17, 0.022, 0.022, 0.006, 4.5), (0.6, 0.022, 0.022, 0.006, 4.5)], 24), METAL)
    for k in range(7):
        uu = 0.2 + k * 0.03
        b.add(loft([(uu, 0.03, 0.03, 0.006, 5.0), (uu + 0.007, 0.03, 0.03, 0.006, 5.0)], 24), ACCENT)
    b.add(slab([(0.19, 0.03), (0.4, 0.03), (0.4, 0.034), (0.19, 0.034)], 0.006, 0.001), METAL)
    for s in (-1, 1):
        b.add(rev([(0, 0.013), (0.33, 0.013)], (s * 0.017, 0.17, -0.044), seg=18), METAL)
        b.add(rev([(0, 0.0138), (0.024, 0.0138), (0.03, 0.01)], (s * 0.017, 0.5, -0.044), seg=18, flutes=14, fd=0.0012), METAL)
    b.add(loft([(0.47, 0.036, 0.046, -0.014, 4.5), (0.49, 0.036, 0.046, -0.014, 4.5)], 24), METAL)
    for s in (-1, 1):
        screw(b, 0.48, -0.014, s * 0.036, 0.003, s)
    brake_box(b, 0.6, 0.006, 0.026, 0.1, 3, 0.036, 0.031)
    # Pompe à poignée verticale.
    fe = loft([(0.2, 0.034, 0.02, -0.046, 3.0), (0.21, 0.038, 0.024, -0.046, 3.0), (0.37, 0.038, 0.024, -0.046, 3.0), (0.38, 0.034, 0.02, -0.046, 3.0)], 28)
    cuts = []
    for s in (-1, 1):
        for zz in (-0.052, -0.04):
            cuts.append(box(0.01, 0.14, 0.004, (s * 0.038, 0.29, zz), 0.0018, 2))
    b.add(carve(fe, cuts), POLY)
    sv = vgrip(b, 0.3, -0.068, 0.085)
    grip = grip2(b, 0.0, -0.064, 14, 0.105, 0.034, 0.05, POLY)
    g.point("Muzzle", (0, 0.7, 0.006)); g.point("Grip", grip); g.point("SupportV", sv)
    g.point("Butt", (0, -0.388, -0.034)); g.point("Mag", (0, -0.15, -0.08))


def fracture(g):
    """Fracture (contenu téléchargeable) : fusil à dispersion expérimental, coque sculptée d'une pièce (crosse ajourée,
    joints de panneaux, évents, filets lumineux), bouche fendue en éventail de trois lames et arceaux de champ, cellule
    d'énergie apparente sous le boîtier, viseur compact."""
    b = g.body
    body = loft([(-0.405, 0.02, 0.048, -0.034, 3.2), (-0.39, 0.024, 0.055, -0.03, 3.4), (-0.25, 0.022, 0.044, -0.018, 3.0), (-0.12, 0.028, 0.04, -0.006, 3.6),
                 (0.12, 0.03, 0.042, -0.006, 3.8), (0.25, 0.026, 0.032, -0.002, 3.6), (0.285, 0.02, 0.022, 0.0, 3.0)], 28)
    cuts = [slab([(-0.17, -0.016), (-0.33, -0.03), (-0.355, -0.062), (-0.31, -0.072), (-0.19, -0.046)], 0.05, 0.008, 2)]
    for s in (-1, 1):
        for uu in (-0.06, 0.04, 0.2):
            cuts.append(box(0.012, 0.0025, 0.09, (s * 0.03, uu, -0.006), 0.001, 1))
        cuts.append(box(0.012, 0.3, 0.0025, (s * 0.03, 0.07, 0.014), 0.001, 1))
        cuts += slots(-0.1, 0.02, 4, s * 0.028, -0.02, 0.008, 0.018, 0.008)
    b.add(carve(body, cuts), PAINT)
    for s in (-1, 1):
        b.add(box(0.002, 0.15, 0.0028, (s * 0.0302, 0.12, -0.012), 0.0006, 1), ENERGY)
        # Plaques de blindage rapportées (crosse, boîtier) et vis.
        b.add(slab([(-0.36, -0.006), (-0.24, -0.002), (-0.24, -0.016), (-0.36, -0.024)], 0.0015, 0.001, x=s * 0.0225), ACCENT)
        b.add(slab([(0.13, -0.022), (0.24, -0.018), (0.25, -0.03), (0.13, -0.038)], 0.0015, 0.001, x=s * 0.03), ACCENT)
        for (uu, zz) in ((0.14, -0.03), (0.235, -0.024), (-0.35, -0.015), (-0.25, -0.009)):
            screw(b, uu, zz, s * (0.0315 if uu > 0 else 0.024), 0.0018, s)
    b.add(slab([(-0.405, 0.016), (-0.418, 0.014), (-0.418, -0.084), (-0.405, -0.088)], 0.021, 0.004, 2), RUBBER)
    rail(b, -0.1, 0.15, 0.035, mat=METAL)
    reflex(b, 0.0, 0.0445)
    # Lames en éventail et arceaux de champ.
    for x0 in (-0.016, 0.0, 0.016):
        x1 = x0 * 2.8
        b.add(slab_top([(0.27, x0 - 0.0065), (0.27, x0 + 0.0065), (0.5, x1 + 0.0045), (0.535, x1 + 0.002), (0.535, x1 - 0.004), (0.5, x1 - 0.0055)], -0.016, 0.016, 0.002), METAL)
        b.add(rod((x0, 0.29, 0.0165), (x1, 0.52, 0.0165), 0.0018, None, 6), ENERGY)
    for uu in (0.33, 0.39, 0.45):
        mx = 0.016 + 0.0288 * (uu - 0.27) / 0.23 + 0.0095
        hoop = loft([(uu, mx + 0.006, 0.024, 0.0, 3.0), (uu + 0.012, mx + 0.006, 0.024, 0.0, 3.0)], 28)
        b.add(carve(hoop, [loft([(uu - 0.01, mx, 0.0158, 0.0, 3.0), (uu + 0.022, mx, 0.0158, 0.0, 3.0)], 28)]), ACCENT)
    b.add(rev([(0, 0.03), (0.02, 0.03)], (0, 0.265, 0.0), seg=24), ACCENT)
    # Cellule d'énergie sous le boîtier.
    hs = slab([(0.01, -0.044), (0.13, -0.044), (0.124, -0.084), (0.016, -0.084)], 0.02, 0.003, 2)
    b.add(carve(hs, [box(0.06, 0.08, 0.02, (0, 0.07, -0.066), 0.004, 2)]), POLY)
    energy_cell(b, 0.07, -0.066, 0.0, 0.1, 0.0135)
    grip = grip2(b, -0.04, -0.045, 18, 0.105, 0.032, 0.046, POLY)
    g.point("Muzzle", (0, 0.535, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.2, -0.05))
    g.point("Butt", (0, -0.418, -0.034)); g.point("Mag", (0, 0.07, -0.066))


# ───────────────────────── Fusils de précision ─────────────────────────


def bolt_action(b, u0, u1, r=0.0165, port=True):
    """Boîtier cylindrique de fusil à verrou : fenêtre d'éjection et culasse visible, manchon arrière, levier coudé à boule,
    tenon de recul."""
    act = rev([(0, r), (u1 - u0, r)], (0, u0, 0.0), seg=24)
    if port:
        act = carve(act, [box(0.02, 0.075, 0.018, (r, u0 + 0.12, 0.004), 0.004, 2)])
        b.add(rev([(0, r * 0.66), (0.085, r * 0.66)], (0.006, u0 + 0.075, 0.003), seg=16), ALLOY)
    b.add(act, METAL)
    b.add(rev([(-0.04, r * 0.75), (-0.034, r * 0.95), (-0.012, r * 0.95), (0, r * 0.85)], (0, u0, 0.0), seg=20), METAL)
    b.add(rev([(-0.046, r * 0.35), (-0.04, r * 0.35)], (0, u0, 0.0), seg=10), GLOW)
    b.add(rod((r * 0.7, u0 + 0.02, 0.004), (r + 0.026, u0 + 0.002, -0.022), 0.0045, 0.0038, 10), METAL)
    b.add(rev([(-0.009, 0.004), (-0.006, 0.0085), (0, 0.0098), (0.006, 0.0085), (0.009, 0.004)], (r + 0.03, u0 - 0.001, -0.026), 'x', 14), POLY)
    b.add(rev([(0, r * 1.15), (0.008, r * 1.15)], (0, u1 - 0.008, 0.0), seg=24), METAL)


def kff(g):
    """À verrou (type AWM / AI AX) : boîtier cylindrique dans un châssis à pans coupés, levier à boule, garde-main octogonal à
    fentes, long rail, canon cannelé et frein de bouche usiné, lunette, crosse réglable à trou de pouce, busc et monopode,
    bipied replié."""
    b = g.body
    bolt_action(b, -0.07, 0.16)
    ch = slab([(-0.095, 0.006), (0.165, 0.006), (0.175, -0.002), (0.175, -0.03), (0.135, -0.045), (-0.02, -0.045), (-0.095, -0.032)], 0.024, 0.004)
    cuts = []
    for s in (-1, 1):
        cuts.append(slab([(-0.07, -0.006), (-0.02, -0.006), (-0.02, -0.026), (-0.07, -0.024)], 0.003, 0.003, 2, x=s * 0.024))
        cuts.append(slab([(0.12, -0.006), (0.158, -0.006), (0.158, -0.024), (0.12, -0.03)], 0.003, 0.003, 2, x=s * 0.024))
    b.add(carve(ch, cuts), PAINT)
    for s in (-1, 1):
        screw(b, 0.14, 0.0, s * 0.024, 0.0026, s)
        screw(b, -0.085, -0.004, s * 0.024, 0.0026, s)
    b.add(slab([(0.13, -0.044), (0.138, -0.044), (0.134, -0.058), (0.128, -0.056)], 0.007, 0.001), METAL)    # arrêtoir
    hg = loft([(0.17, 0.024, 0.025, -0.004, 6.0), (0.176, 0.026, 0.027, -0.004, 6.0), (0.466, 0.026, 0.027, -0.004, 6.0), (0.472, 0.024, 0.025, -0.004, 6.0)], 24)
    cuts = []
    for s in (-1, 1):
        for zz in (0.006, -0.012):
            cuts += slots(0.2, 0.45, 6, s * 0.026, zz, 0.024, 0.0075, 0.008)
    cuts += slots(0.2, 0.45, 6, 0, -0.031, 0.024, 0.0075, 0.008, axis='z')
    b.add(carve(hg, cuts), PAINT)
    rail(b, -0.085, 0.465, 0.016)
    b.add(rev([(0, 0.0135), (0.3, 0.0128)], (0, 0.15, 0.0), seg=20), METAL)
    b.add(rev([(0, 0.0127), (0.37, 0.0116)], (0, 0.45, 0.0), seg=24, flutes=6, fd=0.0018), METAL)
    b.add(rev([(0, 0.012), (0.012, 0.012)], (0, 0.82, 0.0), seg=20), METAL)
    brake_box(b, 0.83, 0.0, 0.017, 0.075, 3)
    optic(b, 0.1, 0.072, 0.38, 0.016, 0.027, base=0.0265)
    grip = grip2(b, -0.075, -0.03, 10, 0.105, 0.03, 0.046, POLY)
    mag = mag_box(b, 0.055, -0.045, 0.07, 0.032, 0.09, 0.0, 0, METAL, ribs=2)
    butt = stock_adj(b, -0.095, -0.42, 0.006, -0.085, 0.019, PAINT)
    for (uu, zz) in ((0.43, -0.031), (-0.3, -0.08)):
        b.add(rev([(0, 0.006), (0.004, 0.006)], (0.0, uu, zz - 0.004), 'z', 12), METAL)
    bipod2(b, 0.42, -0.031, False, 0.2, clamp=0)
    g.point("Muzzle", (0, 0.905, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.034))
    g.point("Butt", butt); g.point("Mag", mag)


def np(g):
    """Semi-auto de précision (type PSG-1) : boîtier en tôle à rainures, carcasse de détente polymère, tube d'armement
    au-dessus du canon et levier latéral, devant large à évents et rail inférieur, canon lourd, griffes de montage de
    lunette, poignée à repose-paume réglable, crosse pleine réglable."""
    b = g.body
    rcv = slab([(-0.12, 0.02), (0.2, 0.02), (0.212, 0.012), (0.212, -0.022), (-0.12, -0.022)], 0.019, 0.003)
    cuts = [box(0.02, 0.07, 0.02, (0.02, 0.06, 0.003), 0.003, 2)]
    for s in (-1, 1):
        for zz in (0.012, -0.013):
            cuts.append(box(0.004, 0.3, 0.003, (s * 0.019, 0.04, zz), 0.0013, 2))
    b.add(carve(rcv, cuts), METAL)
    b.add(box(0.012, 0.064, 0.016, (0.01, 0.06, 0.003), 0.002), ALLOY)
    for uu in (-0.07, 0.13):
        b.add(slab([(uu - 0.022, 0.02), (uu + 0.022, 0.02), (uu + 0.018, 0.026), (uu - 0.018, 0.026)], 0.012, 0.0015), METAL)
    b.add(slab([(-0.115, -0.02), (0.045, -0.02), (0.05, -0.04), (-0.055, -0.046), (-0.115, -0.036)], 0.0175, 0.003, 2), POLY)
    pins(b, (-0.1, 0.03), -0.03, 0.0175)
    b.add(slab([(0.045, -0.02), (0.125, -0.02), (0.125, -0.046), (0.045, -0.046)], 0.016, 0.002), METAL)
    b.add(box(0.006, 0.014, 0.008, (0.019, -0.04, -0.03), 0.002), METAL)
    # Tube et levier d'armement, devant, rail inférieur, canon lourd.
    b.add(rev([(0, 0.0095), (0.24, 0.0095)], (0, 0.2, 0.018), seg=16), METAL)
    b.add(rod((-0.008, 0.29, 0.02), (-0.034, 0.284, 0.024), 0.0032, 0.0028, 8), METAL)
    b.add(rev([(0, 0.006), (-0.008, 0.006), (-0.01, 0.004)], (-0.034, 0.284, 0.024), 'x', 12), POLY)
    fe = loft([(0.212, 0.026, 0.03, -0.006, 4.0), (0.22, 0.029, 0.033, -0.006, 4.0), (0.43, 0.029, 0.033, -0.006, 4.0), (0.445, 0.024, 0.028, -0.006, 4.0)], 24)
    cuts = []
    for s in (-1, 1):
        cuts += slots(0.24, 0.42, 6, s * 0.029, 0.006, 0.016, 0.016, 0.008)
    cuts += slots(0.24, 0.42, 6, 0, 0.027, 0.016, 0.01, 0.008, axis='z')
    cuts.append(rev([(-0.02, 0.0105), (0.05, 0.0105)], (0, 0.42, 0.018), seg=16))
    b.add(carve(fe, cuts), POLY)
    b.add(slab([(0.24, -0.038), (0.42, -0.038), (0.42, -0.046), (0.24, -0.046)], 0.011, 0.0015), METAL)
    b.add(slab([(0.24, -0.046), (0.42, -0.046), (0.42, -0.05), (0.24, -0.05)], 0.015, 0.0015), METAL)
    b.add(rev([(0, 0.0122), (0.36, 0.0118)], (0, 0.44, 0.0), seg=20), METAL)
    b.add(rev([(0, 0.0128), (0.03, 0.0128), (0.034, 0.011)], (0, 0.8, 0.0), seg=20, flutes=12, fd=0.0012), METAL)
    crown(b, 0.834, 0.011)
    optic(b, 0.04, 0.074, 0.36, 0.016, 0.026, base=0.026, rings=(-0.07, 0.13))
    grip = grip2(b, -0.06, -0.034, 12, 0.105, 0.032, 0.048, POLY)
    b.add(slab([(-0.112, -0.128), (-0.06, -0.128), (-0.058, -0.136), (-0.112, -0.138)], 0.022, 0.002, 2), POLY)    # repose-paume
    b.add(rod((0, -0.098, -0.12), (0, -0.098, -0.13), 0.004, None, 8), ALLOY)
    mag = mag_box(b, 0.085, -0.046, 0.1, 0.024, 0.066, 0.0, 4, METAL, ribs=2)
    butt = stock_adj(b, -0.12, -0.44, 0.012, -0.08, 0.02, POLY, thumb=False, cheek=True, monopod=False, holes=0)
    for s in (-1, 1):
        b.add(slab([(-0.36, -0.01), (-0.2, -0.004), (-0.2, -0.05), (-0.36, -0.06)], 0.0012, 0.004, 2, x=s * 0.02), METAL)
    laser_module(g.opt("laser"), 0.37, -0.006, -0.036)
    g.point("Muzzle", (0, 0.834, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.33, -0.04))
    g.point("Butt", butt); g.point("Mag", mag)


def lysander(g):
    """Anti-matériel (type M82A1) : boîtier supérieur en tôle à fenêtres de refroidissement, boîtier inférieur prolongé en
    crosse, appui-joue, gros coussin amortisseur, canon lourd cannelé, frein de bouche en flèche à deux chambres, lunette
    longue, poignée de transport, bipied replié, monopode arrière."""
    b = g.body
    up = slab([(-0.12, 0.034), (0.47, 0.034), (0.49, 0.024), (0.49, -0.004), (-0.12, -0.004)], 0.028, 0.004)
    cuts = slots(0.27, 0.47, 5, 0, 0.013, 0.026, 0.02, 0.1)
    cuts += slots(0.27, 0.47, 5, 0, 0.034, 0.026, 0.012, 0.012, axis='z')
    cuts.append(box(0.02, 0.09, 0.024, (0.028, 0.06, 0.012), 0.004, 2))
    b.add(carve(up, cuts), PAINT)
    b.add(box(0.016, 0.085, 0.02, (0.016, 0.06, 0.012), 0.002), ALLOY)
    for s in (-1, 1):
        for uu in (-0.08, 0.2, 0.3, 0.4):
            screw(b, uu, 0.028, s * 0.028, 0.0024, s)
    rail(b, -0.1, 0.32, 0.034)
    low = slab([(-0.4, -0.004), (0.36, -0.004), (0.36, -0.03), (0.14, -0.058), (0.0, -0.058), (-0.12, -0.048), (-0.4, -0.064)], 0.026, 0.004)
    cuts = []
    for s in (-1, 1):
        cuts.append(slab([(-0.37, -0.014), (-0.16, -0.014), (-0.16, -0.04), (-0.37, -0.05)], 0.003, 0.003, 2, x=s * 0.026))
        cuts.append(slab([(0.18, -0.012), (0.33, -0.012), (0.33, -0.026), (0.18, -0.046)], 0.003, 0.003, 2, x=s * 0.026))
    b.add(carve(low, cuts), METAL)
    for (uu, zz) in ((-0.105, 0.0), (0.45, 0.012)):
        b.add(tube_x(0.0055, 0.064, (0, uu, zz), 14), METAL)
        b.add(rev([(0, 0.009), (0.003, 0.009)], (0.032, uu, zz), 'x', 14), ALLOY)
    b.add(slab([(-0.12, 0.032), (-0.39, 0.03), (-0.4, 0.002), (-0.12, -0.004)], 0.022, 0.006, 2), POLY)          # appui-joue
    pad = slab([(-0.4, 0.034), (-0.44, 0.032), (-0.44, -0.088), (-0.4, -0.09)], 0.03, 0.008, 2)
    b.add(carve(pad, [box(0.08, 0.004, 0.11, (0, -0.415 - k * 0.008, -0.028), 0.0015, 1) for k in range(3)]), RUBBER)
    b.add(rev([(0, 0.009), (-0.02, 0.009)], (0, -0.33, -0.06), 'z', 14), METAL)
    b.add(rev([(-0.02, 0.012), (-0.034, 0.012)], (0, -0.33, -0.06), 'z', 16, flutes=12, fd=0.0016), METAL)
    b.add(rev([(-0.034, 0.008), (-0.044, 0.01)], (0, -0.33, -0.06), 'z', 12), RUBBER)
    # Canon lourd, frein en flèche.
    b.add(rev([(0, 0.0195), (0.45, 0.0185)], (0, 0.1, 0.0), seg=22), METAL)
    b.add(rev([(0, 0.0185), (0.4, 0.0168)], (0, 0.55, 0.0), seg=24, flutes=8, fd=0.0022), METAL)
    b.add(rev([(0, 0.0175), (0.03, 0.0175)], (0, 0.95, 0.0), seg=22), METAL)
    b.add(ring(0.026, 0.017, 0.47, 0.492, 0, 0.0, 24), METAL)
    br = slab_top([(0.975, -0.024), (1.02, -0.037), (1.092, -0.037), (1.092, 0.037), (1.02, 0.037), (0.975, 0.024)], -0.023, 0.023, 0.003, 2)
    b.add(carve(br, [box(0.1, 0.024, 0.032, (0, uu, 0.0), 0.005, 2) for uu in (1.014, 1.058)]), METAL)
    b.add(rev([(0, 0.009), (0.001, 0.009)], (0, 1.093, 0.0), seg=14), POLY)
    optic(b, 0.1, 0.086, 0.4, 0.017, 0.03, base=0.0445)
    b.add(sweep([(0.34, 0.036, 0.006, 0.005, 2.5), (0.345, 0.06, 0.006, 0.005, 2.5), (0.362, 0.072, 0.006, 0.005, 2.5),
                 (0.41, 0.072, 0.006, 0.005, 2.5), (0.427, 0.06, 0.006, 0.005, 2.5), (0.432, 0.036, 0.006, 0.005, 2.5)], 10), METAL)
    grip = grip2(b, -0.06, -0.05, 16, 0.105, 0.032, 0.048, POLY)
    mag = mag_box(b, 0.08, -0.056, 0.1, 0.036, 0.11, 0.0, 0, METAL, ribs=3)
    bipod2(b, 0.46, -0.008, False, 0.28, r=0.0065, clamp=0)
    g.point("Muzzle", (0, 1.093, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.3, -0.04))
    g.point("Butt", (0, -0.44, -0.028)); g.point("Mag", mag)


def fang(g):
    """Type SVD : boîtier étroit à couvercle bombé, garde-main ajouré et bagues, tube d'emprunt de gaz, long canon fin,
    guidon à tunnel, cache-flammes fendu, lunette PSO sur montage latéral à levier, crosse squelette à trou de pouce et
    appui-joue, chargeur court courbe."""
    b = g.body
    rcv = slab([(-0.1, 0.008), (0.17, 0.008), (0.17, -0.03), (0.12, -0.04), (-0.02, -0.04), (-0.1, -0.032)], 0.017, 0.003)
    b.add(carve(rcv, [box(0.014, 0.07, 0.012, (0.017, 0.07, 0.0), 0.003, 2)]), METAL)
    b.add(loft([(-0.104, 0.013, 0.008, 0.009, 3.0), (-0.098, 0.0165, 0.011, 0.01, 3.0), (0.16, 0.0165, 0.011, 0.01, 3.0), (0.166, 0.014, 0.009, 0.009, 3.0)], 18), METAL)
    b.add(rod((0.017, 0.13, 0.002), (0.034, 0.13, 0.002), 0.003, 0.0028, 8), METAL)
    b.add(slab([(-0.06, 0.004), (0.06, 0.004), (0.064, -0.004), (-0.06, -0.002)], 0.0011, 0.0005, x=0.018), METAL)
    pins(b, (-0.08, 0.15), -0.024, 0.017)
    # Garde-main ajouré, gaz, canon, guidon, cache-flammes.
    hg = loft([(0.17, 0.02, 0.02, 0.0, 2.4), (0.18, 0.023, 0.024, 0.0, 2.4), (0.44, 0.022, 0.023, 0.0, 2.4), (0.45, 0.019, 0.02, 0.0, 2.4)], 22)
    cuts = []
    for s in (-1, 1):
        cuts += slots(0.2, 0.42, 6, s * 0.022, 0.004, 0.012, 0.022, 0.008)
    b.add(carve(hg, cuts), PAINT)
    for uu in (0.165, 0.448):
        b.add(ring(0.025, 0.009, uu, uu + 0.008, 0, 0.0, 22), METAL)
    b.add(rev([(0, 0.0065), (0.06, 0.0065)], (0, 0.45, 0.014), seg=12), METAL)
    b.add(loft([(0.5, 0.012, 0.018, 0.004, 3.0), (0.52, 0.012, 0.018, 0.004, 3.0)], 16), METAL)
    b.add(rev([(0, 0.0105), (0.62, 0.0092)], (0, 0.17, 0.0), seg=18), METAL)
    b.add(loft([(0.765, 0.011, 0.016, 0.004, 3.0), (0.79, 0.011, 0.016, 0.004, 3.0)], 16), METAL)
    b.add(ring(0.011, 0.009, 0.77, 0.786, 0, 0.026, 16), METAL)
    b.add(box(0.002, 0.004, 0.01, (0, 0.778, 0.022), 0.0004, 1), METAL)
    fh = rev([(0, 0.0105), (0.07, 0.0105), (0.072, 0.0095)], (0, 0.79, 0.0), seg=16)
    b.add(carve(fh, [box(0.03, 0.05, 0.003, (0, 0.84, 0.0), 0.001, 1), box(0.003, 0.05, 0.03, (0, 0.84, 0.0), 0.001, 1)]), METAL)
    crown(b, 0.862, 0.0095)
    # Montage latéral et lunette PSO.
    b.add(slab([(-0.03, -0.012), (0.11, -0.012), (0.11, 0.032), (-0.03, 0.032)], 0.0025, 0.0012, x=-0.0205), METAL)
    b.add(slab([(-0.02, 0.026), (0.1, 0.026), (0.1, 0.04), (-0.02, 0.04)], 0.015, 0.002, x=-0.008), METAL)
    b.add(rod((-0.024, 0.0, 0.01), (-0.036, -0.03, 0.006), 0.003, 0.0026, 8), METAL)
    optic(b, 0.05, 0.062, 0.3, 0.013, 0.019, base=0.04, rings=(-0.0, 0.09), shade=0.02)
    # Crosse squelette (poignée intégrée, trou de pouce), appui-joue, plaque de couche, pontet.
    st = slab([(-0.1, 0.008), (-0.42, 0.002), (-0.428, -0.105), (-0.395, -0.112), (-0.17, -0.112), (-0.125, -0.1), (-0.098, -0.03)], 0.017, 0.004, 2)
    b.add(carve(st, [slab([(-0.16, -0.012), (-0.35, -0.014), (-0.37, -0.03), (-0.37, -0.08), (-0.35, -0.092), (-0.19, -0.094), (-0.155, -0.08)], 0.04, 0.008, 2)]), PAINT)
    b.add(slab([(-0.19, 0.014), (-0.34, 0.012), (-0.35, 0.0), (-0.19, 0.0)], 0.0185, 0.004, 2), POLY)
    b.add(slab([(-0.428, 0.004), (-0.44, 0.002), (-0.44, -0.11), (-0.428, -0.114)], 0.0185, 0.003, 2), RUBBER)
    b.add(sweep([(-0.005, -0.03, 0.004, 0.0026, 2.5), (-0.008, -0.05, 0.004, 0.0026, 2.5), (-0.025, -0.07, 0.004, 0.0026, 2.5),
                 (-0.07, -0.074, 0.004, 0.0026, 2.5), (-0.105, -0.068, 0.004, 0.0026, 2.5)], 8), METAL)
    b.add(sweep([(-0.04, -0.03, 0.003, 0.0022, 2.4), (-0.042, -0.044, 0.003, 0.0022, 2.4), (-0.048, -0.055, 0.003, 0.0022, 2.4), (-0.056, -0.059, 0.003, 0.0022, 2.4)], 8), METAL)
    for k in range(5):
        t = 0.2 + k * 0.14
        uu, zz = -0.13 - 0.03 * t, -0.03 - 0.07 * t
        for s in (-1, 1):
            b.add(rod((s * 0.0172, uu - 0.012, zz), (s * 0.0172, uu + 0.016, zz + 0.004), 0.0011, None, 5), PAINT)
    mag = mag_box(b, 0.055, -0.038, 0.09, 0.022, 0.06, 0.016, 2, METAL, ribs=2)
    g.point("Muzzle", (0, 0.862, 0.0)); g.point("Grip", (0, -0.14, -0.065)); g.point("Support", (0, 0.31, -0.026))
    g.point("Butt", (0, -0.44, -0.052)); g.point("Mag", mag)


def dunkel(g):
    """Bullpup de précision (type WA2000) : poutre supérieure portant la lunette et reliée à la bouche par un bloc, canon
    cannelé apparent, boîtier compact à fenêtre d'éjection, devant strié, poignée en avant du chargeur, crosse ajourée à
    busc et plaque réglable."""
    b = g.body
    act = slab([(-0.16, 0.014), (0.11, 0.014), (0.125, 0.004), (0.125, -0.032), (0.08, -0.046), (-0.12, -0.046), (-0.16, -0.03)], 0.021, 0.004, 2)
    cuts = [box(0.02, 0.075, 0.02, (0.021, -0.04, 0.0), 0.004, 2)]
    for s in (-1, 1):
        cuts.append(box(0.004, 0.24, 0.004, (s * 0.021, -0.02, -0.022), 0.0018, 2))
    b.add(carve(act, cuts), METAL)
    b.add(box(0.014, 0.07, 0.016, (0.011, -0.04, 0.0), 0.002), ALLOY)
    b.add(rod((-0.021, -0.1, 0.004), (-0.04, -0.1, 0.004), 0.0035, 0.003, 8), METAL)
    pins(b, (-0.14, 0.1), -0.03, 0.021)
    # Poutre, bloc avant, canon cannelé, bouche.
    b.add(loft([(-0.2, 0.012, 0.011, 0.027, 4.5), (-0.19, 0.0145, 0.013, 0.027, 4.5), (0.55, 0.0145, 0.013, 0.027, 4.5), (0.56, 0.012, 0.011, 0.027, 4.5)], 20), PAINT)
    for s in (-1, 1):
        b.add(box(0.004, 0.6, 0.004, (s * 0.0145, 0.2, 0.027), 0.0015, 1), METAL)
    blk = slab([(0.515, 0.04), (0.575, 0.04), (0.575, -0.016), (0.53, -0.016), (0.515, 0.0)], 0.016, 0.003, 2)
    b.add(carve(blk, slots(0.525, 0.565, 2, 0, 0.02, 0.012, 0.01, 0.04)), METAL)
    for s in (-1, 1):
        screw(b, 0.545, -0.006, s * 0.016, 0.0024, s)
    b.add(rev([(0, 0.0125), (0.4, 0.0115)], (0, 0.12, 0.0), seg=24, flutes=6, fd=0.0022), METAL)
    b.add(rev([(0, 0.0115), (0.13, 0.0108)], (0, 0.52, 0.0), seg=18), METAL)
    md = rev([(0, 0.0135), (0.05, 0.0135), (0.054, 0.012)], (0, 0.65, 0.0), seg=20)
    b.add(carve(md, holes_ring(0.668, 0.013, 6, 0.003, 0.01) + holes_ring(0.686, 0.013, 6, 0.003, 0.01)), METAL)
    crown(b, 0.704, 0.012)
    # Devant strié sous le canon.
    fe = slab([(0.125, -0.008), (0.38, -0.008), (0.4, -0.016), (0.39, -0.04), (0.14, -0.046), (0.125, -0.03)], 0.02, 0.004, 2)
    cuts = []
    for s in (-1, 1):
        for k in range(8):
            cuts.append(box(0.006, 0.004, 0.022, (s * 0.02, 0.18 + k * 0.022, -0.028), 0.0015, 2))
    b.add(carve(fe, cuts), PAINT)
    optic(b, 0.02, 0.076, 0.34, 0.016, 0.025, base=0.04)
    grip = grip2(b, 0.03, -0.046, 14, 0.1, 0.031, 0.046, POLY)
    mag = mag_box(b, -0.07, -0.046, 0.085, 0.024, 0.06, 0.0, 2, METAL, ribs=2)
    # Crosse ajourée, busc, plaque.
    st = slab([(-0.16, 0.014), (-0.37, 0.018), (-0.375, -0.1), (-0.335, -0.104), (-0.16, -0.048)], 0.02, 0.004, 2)
    b.add(carve(st, [slab([(-0.2, -0.004), (-0.33, -0.002), (-0.34, -0.078), (-0.31, -0.084), (-0.21, -0.05)], 0.04, 0.008, 2)]), PAINT)
    b.add(slab([(-0.19, 0.024), (-0.35, 0.03), (-0.36, 0.016), (-0.19, 0.012)], 0.0185, 0.003, 2), PAINT)
    for uu in (-0.22, -0.32):
        b.add(rod((0, uu, 0.012), (0, uu, 0.022), 0.004, None, 10), ALLOY)
    b.add(slab([(-0.375, 0.022), (-0.381, 0.022), (-0.381, -0.104), (-0.375, -0.104)], 0.02, 0.001), METAL)
    b.add(slab([(-0.381, 0.024), (-0.395, 0.022), (-0.395, -0.1), (-0.381, -0.106)], 0.0205, 0.003, 2), RUBBER)
    g.point("Muzzle", (0, 0.704, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.27, -0.05))
    g.point("Butt", (0, -0.395, -0.04)); g.point("Mag", mag)


def eagle(g):
    """Semi-auto de précision (type M110) : boîtiers AR-10 (fenêtre d'éjection et volet, assistance de fermeture, levier en
    T, puits de chargeur évasé), garde-main flottant à fentes et rail continu, hausse et guidon repliés, réducteur de son
    cannelé à collier moleté, crosse réglable (busc, monopode), bipied replié."""
    b = g.body
    up = loft([(-0.11, 0.017, 0.02, 0.001, 7.0), (-0.106, 0.018, 0.021, 0.001, 7.0), (0.176, 0.018, 0.021, 0.001, 7.0), (0.18, 0.017, 0.02, 0.001, 7.0)], 24)
    b.add(carve(up, [box(0.02, 0.065, 0.016, (0.018, 0.04, 0.0), 0.003, 2)]), METAL)
    b.add(box(0.012, 0.06, 0.012, (0.01, 0.04, 0.0), 0.002), ALLOY)
    b.add(box(0.003, 0.066, 0.014, (0.0205, 0.04, -0.019), 0.0008), METAL)             # volet ouvert
    b.add(rod((0.016, -0.075, 0.01), (0.02, -0.03, 0.006), 0.0065, None, 12), METAL)    # assistance de fermeture
    b.add(rev([(0, 0.0075), (0.008, 0.0075)], (0.016, -0.083, 0.01), 'u', 12, flutes=8, fd=0.001), METAL)
    b.add(box(0.012, 0.02, 0.014, (0.021, -0.012, 0.006), 0.003), METAL)
    b.add(box(0.042, 0.012, 0.007, (0, -0.116, 0.014), 0.002), METAL)
    low = slab([(-0.1, -0.019), (0.13, -0.019), (0.13, -0.04), (0.118, -0.074), (0.032, -0.074), (0.028, -0.05), (-0.06, -0.05), (-0.1, -0.04)], 0.018, 0.003)
    b.add(carve(low, [box(0.026, 0.074, 0.03, (0, 0.075, -0.074), 0.003, 1)]), METAL)
    b.add(tube_x(0.0055, 0.004, (0.019, 0.08, -0.04), 12), METAL)
    b.add(box(0.004, 0.02, 0.006, (0.0195, -0.05, -0.032), 0.001), METAL)
    b.add(tube_x(0.005, 0.004, (0.0195, -0.042, -0.032), 12), METAL)
    b.add(slab([(0.0, -0.028), (0.022, -0.026), (0.022, -0.034), (0.0, -0.036)], 0.0012, 0.0005, x=-0.019), METAL)   # arrêtoir de culasse
    pins(b, (-0.085, 0.115), -0.03, 0.018)
    # Garde-main flottant, rail continu, organes de visée repliés.
    b.add(ring(0.03, 0.011, 0.176, 0.19, 0, -0.002, 24), METAL)
    hg = loft([(0.185, 0.026, 0.027, -0.002, 6.0), (0.19, 0.027, 0.028, -0.002, 6.0), (0.575, 0.027, 0.028, -0.002, 6.0), (0.58, 0.025, 0.026, -0.002, 6.0)], 24)
    cuts = []
    for s in (-1, 1):
        for zz in (0.006, -0.012):
            cuts += slots(0.21, 0.56, 7, s * 0.027, zz, 0.028, 0.0075, 0.008)
    cuts += slots(0.21, 0.56, 7, 0, -0.03, 0.028, 0.0075, 0.008, axis='z')
    b.add(carve(hg, cuts), PAINT)
    rail(b, -0.1, 0.575, 0.022)
    b.add(slab([(-0.1, 0.032), (-0.07, 0.032), (-0.072, 0.04), (-0.098, 0.04)], 0.012, 0.0015), METAL)
    b.add(slab([(0.535, 0.032), (0.565, 0.032), (0.562, 0.04), (0.538, 0.04)], 0.012, 0.0015), METAL)
    # Canon, réducteur de son.
    b.add(rev([(0, 0.0112), (0.045, 0.011)], (0, 0.58, 0.0), seg=18), METAL)
    b.add(rev([(0, 0.0235), (0.026, 0.0235)], (0, 0.62, 0.0), seg=24, flutes=24, fd=0.0014), METAL)
    b.add(rev([(0, 0.022), (0.004, 0.0215), (0.18, 0.0215), (0.188, 0.019), (0.192, 0.012)], (0, 0.646, 0.0), seg=24, flutes=16, fd=0.0012), METAL)
    crown(b, 0.838, 0.011)
    optic(b, 0.07, 0.076, 0.32, 0.015, 0.025, base=0.0325)
    grip = grip2(b, -0.045, -0.05, 20, 0.105, 0.03, 0.044, POLY)
    mag = mag_box(b, 0.075, -0.072, 0.12, 0.026, 0.07, 0.025, 2, METAL, ribs=3)
    b.add(rev([(0, 0.0155), (0.15, 0.0155)], (0, -0.25, -0.006), seg=18), METAL)
    b.add(rev([(0, 0.019), (0.012, 0.019)], (0, -0.112, -0.006), seg=18, flutes=10, fd=0.0016), METAL)
    butt = stock_adj(b, -0.17, -0.4, 0.014, -0.075, 0.018, PAINT, thumb=False, cheek=True, monopod=True, holes=1)
    bipod2(b, 0.5, -0.03, False, 0.2, clamp=0)
    g.point("Muzzle", (0, 0.838, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.36, -0.032))
    g.point("Butt", butt); g.point("Mag", mag)


def flounders(g):
    """Tir multiple : quatre canons groupés en carré dans un manchon creux ajouré à plaque de bouche, boîtier large à double
    chargeur côte à côte, rail et lunette compacte, crosse à tubes de rangement sur tube tampon."""
    b = g.body
    zc = -0.006
    rcv = slab([(-0.12, 0.026), (0.2, 0.026), (0.215, 0.016), (0.215, -0.044), (0.15, -0.05), (0.0, -0.05), (-0.12, -0.04)], 0.031, 0.005, 1)
    cuts = [box(0.02, 0.07, 0.02, (0.031, 0.06, 0.004), 0.004, 2)]
    for s in (-1, 1):
        cuts.append(slab([(-0.11, 0.016), (-0.02, 0.016), (-0.02, -0.03), (-0.11, -0.03)], 0.003, 0.003, 2, x=s * 0.031))
        cuts.append(box(0.004, 0.2, 0.004, (s * 0.031, 0.1, -0.034), 0.0018, 2))
    b.add(carve(rcv, cuts), PAINT)
    b.add(box(0.016, 0.066, 0.016, (0.02, 0.06, 0.004), 0.002), ALLOY)
    b.add(rod((-0.031, 0.17, 0.012), (-0.05, 0.17, 0.012), 0.004, 0.0035, 10), METAL)
    pins(b, (-0.1, 0.19), -0.03, 0.031)
    for s in (-1, 1):
        for uu in (-0.06, 0.1):
            screw(b, uu, 0.018, s * 0.031, 0.0024, s)
    rail(b, -0.11, 0.205, 0.026)
    # Manchon creux ajouré, quatre canons, plaque de bouche.
    sh = loft([(0.215, 0.031, 0.031, zc, 5.0), (0.6, 0.031, 0.031, zc, 5.0)], 24)
    cuts = [loft([(0.2, 0.027, 0.027, zc, 5.0), (0.62, 0.027, 0.027, zc, 5.0)], 24)]
    for k in range(7):
        uu = 0.25 + k * 0.05
        cuts.append(tube_x(0.0085, 0.1, (0, uu, zc), 12))
        if k < 6:
            cuts.append(rod((0, uu + 0.025, zc - 0.1), (0, uu + 0.025, zc + 0.1), 0.0075, None, 12))
    b.add(carve(sh, cuts), METAL)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.add(rev([(0, 0.0088), (0.43, 0.0085)], (sx * 0.0128, 0.215, zc + sz * 0.0128), seg=14), METAL)
            crown(b, 0.645, 0.0085, zc + sz * 0.0128, sx * 0.0128, 14)
    pl = loft([(0.6, 0.033, 0.033, zc, 5.0), (0.616, 0.033, 0.033, zc, 5.0)], 24)
    b.add(carve(pl, [rev([(-0.01, 0.0092), (0.03, 0.0092)], (sx * 0.0128, 0.6, zc + sz * 0.0128), seg=14) for sx in (-1, 1) for sz in (-1, 1)]), ACCENT)
    for s in (-1, 1):
        screw(b, 0.608, zc, s * 0.033, 0.0026, s)
    b.add(ring(0.036, 0.028, 0.215, 0.23, 0, zc, 24), METAL)
    # Double chargeur, puits évasé.
    b.add(slab([(0.035, -0.046), (0.125, -0.046), (0.122, -0.06), (0.038, -0.06)], 0.031, 0.003, 2), METAL)
    mags = [mag_box(b, 0.08, -0.058, 0.11, 0.023, 0.066, 0.0, 4, METAL, ribs=2, x=s * 0.0135) for s in (-1, 1)]
    optic(b, 0.04, 0.074, 0.28, 0.015, 0.024, base=0.0365)
    grip = grip2(b, -0.04, -0.044, 16, 0.105, 0.032, 0.048, POLY)
    # Crosse à tubes de rangement.
    b.add(rev([(0, 0.0155), (0.15, 0.0155)], (0, -0.27, -0.006), seg=18), METAL)
    st = slab([(-0.2, 0.012), (-0.37, 0.016), (-0.375, -0.09), (-0.34, -0.092), (-0.25, -0.05), (-0.2, -0.03)], 0.019, 0.004, 2)
    b.add(carve(st, [slab([(-0.27, -0.03), (-0.34, -0.03), (-0.345, -0.07), (-0.3, -0.06)], 0.04, 0.005, 2)]), POLY)
    for s in (-1, 1):
        b.add(rev([(0, 0.009), (0.12, 0.009), (0.13, 0.006)], (s * 0.021, -0.34, -0.004), seg=14), POLY)
        b.add(rev([(-0.006, 0.0095), (0, 0.0095)], (s * 0.021, -0.34, -0.004), seg=14, flutes=10, fd=0.0012), METAL)
    b.add(slab([(-0.375, 0.018), (-0.39, 0.016), (-0.39, -0.088), (-0.375, -0.094)], 0.021, 0.003, 2), RUBBER)
    b.add(slab([(-0.24, -0.04), (-0.21, -0.03), (-0.21, -0.036), (-0.24, -0.046)], 0.006, 0.001), METAL)
    g.point("Muzzle", (0, 0.645, zc)); g.point("Grip", grip); g.point("Support", (0, 0.36, -0.042))
    g.point("Butt", (0, -0.39, -0.035)); g.point("Mag", (0, 0.08, -0.11))


def ptarmigan(g):
    """Anti-matériel lourd en bullpup (type M82A2 / XM500) : caisson massif à fenêtres de refroidissement, chargeur derrière
    la poignée, manchon perforé autour d'un canon très épais, gros frein cylindrique percé, bipied déployé, très longue
    lunette, poignée de transport, coussin d'épaule, poignée arrière d'appui."""
    b = g.body
    body = slab([(-0.47, 0.03), (-0.43, 0.04), (0.2, 0.04), (0.25, 0.026), (0.25, -0.03), (0.06, -0.05), (-0.05, -0.064), (-0.3, -0.064),
                 (-0.47, -0.08)], 0.032, 0.006, 1)
    cuts = slots(0.05, 0.23, 4, 0, 0.012, 0.03, 0.02, 0.1)
    for s in (-1, 1):
        cuts.append(slab([(-0.44, 0.026), (-0.3, 0.028), (-0.3, -0.05), (-0.44, -0.064)], 0.004, 0.004, 2, x=s * 0.032))
        cuts.append(box(0.006, 0.4, 0.004, (s * 0.032, -0.08, 0.03), 0.002, 2))
    cuts.append(box(0.024, 0.1, 0.026, (0.032, -0.22, 0.004), 0.004, 2))
    b.add(carve(body, cuts), PAINT)
    b.add(box(0.016, 0.095, 0.022, (0.022, -0.22, 0.004), 0.002), ALLOY)
    for s in (-1, 1):
        for (uu, zz) in ((-0.38, 0.03), (-0.1, 0.03), (0.0, -0.04), (0.2, -0.02)):
            screw(b, uu, zz, s * 0.032, 0.003, s)
    for uu in (-0.27, 0.16):
        b.add(tube_x(0.006, 0.07, (0, uu, -0.012), 14), METAL)
    b.add(slab([(-0.3, 0.04), (-0.46, 0.036), (-0.46, 0.05), (-0.31, 0.054)], 0.024, 0.005, 2), POLY)                # appui-joue
    pad = slab([(-0.47, 0.044), (-0.505, 0.042), (-0.505, -0.082), (-0.47, -0.084)], 0.034, 0.008, 2)
    b.add(carve(pad, [box(0.09, 0.004, 0.11, (0, -0.48 - k * 0.008, -0.02), 0.0015, 1) for k in range(3)]), RUBBER)
    rh = slab([(-0.41, -0.07), (-0.37, -0.068), (-0.372, -0.16), (-0.4, -0.162)], 0.014, 0.004, 2)
    b.add(carve(rh, [box(0.04, 0.003, 0.004, (0, -0.37, -0.09 - k * 0.014), 0.001, 1) for k in range(5)]), POLY)
    # Manchon perforé, canon épais, frein cylindrique.
    b.add(rev([(0, 0.022), (1.1, 0.0195)], (0, -0.15, 0.0), seg=22), METAL)
    sh = ring(0.033, 0.0275, 0.25, 0.64, 0, 0.0, 28)
    cuts = []
    for k in range(6):
        cuts += holes_ring(0.29 + k * 0.058, 0.031, 8, 0.0065, 0.014)
    b.add(carve(sh, cuts), ACCENT)
    for uu in (0.25, 0.63):
        b.add(ring(0.036, 0.02, uu, uu + 0.014, 0, 0.0, 28), METAL)
    br = rev([(0, 0.026), (0.012, 0.032), (0.11, 0.032), (0.12, 0.028), (0.12, 0.018), (0.112, 0.018)], (0, 0.95, 0.0), seg=28, caps=False)
    cuts = []
    for k in range(4):
        cuts += holes_ring(0.98 + k * 0.024, 0.031, 8, 0.006, 0.014)
    b.add(carve(br, cuts), METAL)
    b.add(rev([(0.104, 0.019), (0.106, 0.019)], (0, 0.95, 0.0), seg=20), POLY)
    bipod2(b, 0.5, -0.0, True, 0.3, 16, 12, 0.0075, METAL, clamp=0.033)
    rail(b, -0.36, 0.2, 0.04)
    optic(b, -0.1, 0.098, 0.46, 0.019, 0.034, base=0.0505, shade=0.03)
    b.add(sweep([(0.13, 0.042, 0.007, 0.006, 2.5), (0.135, 0.066, 0.007, 0.006, 2.5), (0.152, 0.08, 0.007, 0.006, 2.5),
                 (0.21, 0.08, 0.007, 0.006, 2.5), (0.226, 0.066, 0.007, 0.006, 2.5), (0.232, 0.042, 0.007, 0.006, 2.5)], 10), METAL)
    grip = grip2(b, 0.0, -0.055, 12, 0.105, 0.034, 0.05, POLY)
    mag = mag_box(b, -0.14, -0.064, 0.09, 0.038, 0.11, 0.0, 0, METAL, ribs=3)
    g.point("Muzzle", (0, 1.07, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.33, -0.036))
    g.point("Butt", (0, -0.505, -0.02)); g.point("Mag", mag)


def pylon(g):
    """Fusil de précision expérimental : bâti à facettes (panneaux, évents, filets lumineux), canon carré à ailettes de
    refroidissement tenues par des longerons, bloc de bouche à lumières, lunette numérique carrée à écran latéral, crosse
    squelette anguleuse, chargeur à pans."""
    b = g.body
    body = slab([(-0.14, 0.03), (0.26, 0.03), (0.3, 0.012), (0.3, -0.03), (0.22, -0.046), (0.0, -0.05), (-0.14, -0.044)], 0.024, 0.006, 1)
    cuts = [box(0.02, 0.06, 0.016, (0.024, 0.02, 0.006), 0.003, 2)]
    for s in (-1, 1):
        cuts += slots(0.17, 0.28, 4, s * 0.024, -0.012, 0.012, 0.022, 0.008)
        cuts.append(box(0.004, 0.004, 0.07, (s * 0.024, 0.13, -0.012), 0.001, 1))
        cuts.append(box(0.004, 0.25, 0.003, (s * 0.024, 0.06, 0.018), 0.001, 1))
    b.add(carve(body, cuts), PAINT)
    for s in (-1, 1):
        b.add(box(0.0016, 0.1, 0.0026, (s * 0.0245, 0.05, -0.032), 0.0005, 1), ENERGY)
    b.add(box(0.012, 0.056, 0.012, (0.016, 0.02, 0.006), 0.002), ALLOY)
    rail(b, -0.12, 0.25, 0.03)
    # Canon carré, ailettes, longerons, bloc de bouche.
    b.add(loft([(0.3, 0.014, 0.014, 0.0, 8.0), (0.87, 0.013, 0.013, 0.0, 8.0)], 16), METAL)
    for k in range(14):
        uu = 0.34 + k * 0.034
        w = 0.03 if k % 2 == 0 else 0.026
        b.add(loft([(uu, w, w, 0.0, 6.0), (uu + 0.006, w, w, 0.0, 6.0)], 16), ACCENT)
    for (x, z, sx, sz) in ((0, 0.03, 0.004, 0.004), (0, -0.03, 0.004, 0.004), (0.03, 0, 0.004, 0.004), (-0.03, 0, 0.004, 0.004)):
        b.add(box(sx * 2 if x == 0 else 0.006, 0.47, 0.006 if x == 0 else sz * 2, (x, 0.577, z), 0.0015), METAL)
    mb = loft([(0.86, 0.02, 0.022, 0.0, 6.0), (0.94, 0.02, 0.022, 0.0, 6.0), (0.95, 0.017, 0.019, 0.0, 6.0)], 16)
    b.add(carve(mb, slots(0.87, 0.94, 3, 0, 0.0, 0.012, 0.024, 0.06) + slots(0.87, 0.94, 3, 0, 0.022, 0.012, 0.012, 0.012, axis='z')), METAL)
    b.add(rev([(0, 0.006), (0.001, 0.006)], (0, 0.951, 0.0), seg=10), POLY)
    # Lunette numérique carrée.
    sc = loft([(-0.05, 0.016, 0.018, 0.074, 6.0), (-0.04, 0.019, 0.021, 0.074, 6.0), (0.12, 0.019, 0.021, 0.074, 6.0), (0.15, 0.025, 0.027, 0.074, 6.0), (0.18, 0.025, 0.027, 0.074, 6.0)], 20)
    b.add(carve(sc, [loft([(0.165, 0.021, 0.023, 0.074, 6.0), (0.2, 0.021, 0.023, 0.074, 6.0)], 20)]), METAL)
    b.add(loft([(0.163, 0.02, 0.022, 0.074, 6.0), (0.166, 0.02, 0.022, 0.074, 6.0)], 20), LENS)
    b.add(loft([(-0.062, 0.012, 0.013, 0.074, 4.0), (-0.048, 0.014, 0.015, 0.074, 4.0)], 16), RUBBER)
    b.add(box(0.002, 0.07, 0.022, (-0.0195, 0.04, 0.076), 0.001, 1), ENERGY)
    for uu in (0.0, 0.1):
        b.add(slab([(uu - 0.018, 0.04), (uu + 0.018, 0.04), (uu + 0.012, 0.056), (uu - 0.012, 0.056)], 0.011, 0.002), METAL)
    b.add(rev([(0, 0.008), (0.01, 0.008)], (0.019, 0.05, 0.074), 'x', 14, flutes=10, fd=0.0012), METAL)
    grip = grip2(b, -0.04, -0.046, 16, 0.105, 0.031, 0.046, POLY)
    mag = mag_box(b, 0.09, -0.048, 0.09, 0.028, 0.07, 0.0, 0, ACCENT, ribs=2, base=METAL)
    st = slab([(-0.14, 0.026), (-0.42, 0.02), (-0.43, -0.09), (-0.37, -0.095), (-0.2, -0.05), (-0.14, -0.044)], 0.018, 0.005, 1)
    b.add(carve(st, [slab([(-0.18, 0.006), (-0.38, 0.0), (-0.39, -0.07), (-0.36, -0.076), (-0.22, -0.04)], 0.04, 0.006, 1)]), PAINT)
    b.add(slab([(-0.2, 0.034), (-0.38, 0.028), (-0.39, 0.016), (-0.2, 0.022)], 0.017, 0.004, 1), ACCENT)
    b.add(slab([(-0.43, 0.022), (-0.444, 0.02), (-0.444, -0.088), (-0.43, -0.092)], 0.019, 0.004, 2), RUBBER)
    g.point("Muzzle", (0, 0.95, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.24, -0.048))
    g.point("Butt", (0, -0.444, -0.035)); g.point("Mag", mag)


def penetrator(g):
    """Fusil électromagnétique perforant : carénage sculpté à évents, deux rails conducteurs ajourés à âme lumineuse tenus
    par des cadres, banques de condensateurs latérales reliées par des câbles, chargeur de projectiles, lunette, crosse
    réglable."""
    b = g.body
    body = loft([(-0.16, 0.026, 0.045, -0.012, 3.0), (-0.1, 0.034, 0.055, -0.01, 3.6), (0.22, 0.034, 0.05, -0.006, 3.8), (0.3, 0.028, 0.038, -0.004, 3.4)], 28)
    cuts = []
    for s in (-1, 1):
        cuts += slots(-0.06, 0.08, 5, s * 0.034, 0.012, 0.012, 0.022, 0.01)
        for uu in (0.1, 0.2):
            cuts.append(box(0.012, 0.0025, 0.12, (s * 0.034, uu, -0.006), 0.001, 1))
    cuts += slots(0.1, 0.26, 4, 0, 0.044, 0.024, 0.01, 0.012, axis='z')
    b.add(carve(body, cuts), PAINT)
    rail(b, -0.08, 0.1, 0.042)
    # Rails conducteurs, âmes, cadres.
    for s in (-1, 1):
        r = slab([(0.28, 0.022), (0.96, 0.016), (0.985, 0.008), (0.985, -0.008), (0.96, -0.016), (0.28, -0.022)], 0.005, 0.0015, 1, x=s * 0.017)
        b.add(carve(r, slots(0.36, 0.94, 8, s * 0.017, 0.0, 0.04, 0.01, 0.02)), METAL)
        b.add(box(0.002, 0.68, 0.006, (s * 0.0115, 0.63, 0.0), 0.0006, 1), ENERGY)
    for k in range(5):
        uu = 0.34 + k * 0.14
        hoop = loft([(uu, 0.027, 0.03, 0.0, 6.0), (uu + 0.018, 0.027, 0.03, 0.0, 6.0)], 20)
        b.add(carve(hoop, [loft([(uu - 0.01, 0.0215, 0.024, 0.0, 6.0), (uu + 0.03, 0.0215, 0.024, 0.0, 6.0)], 20)]), ACCENT)
        for s in (-1, 1):
            screw(b, uu + 0.009, 0.0, s * 0.027, 0.0022, s)
    # Condensateurs et câbles.
    for s in (-1, 1):
        for z in (-0.002, -0.032):
            energy_cell(b, 0.12, z, s * 0.044, 0.16, 0.011)
        b.add(sweep([(0.2, -0.002, 0.0035, 0.0035, 2.0, s * 0.044), (0.24, -0.01, 0.0035, 0.0035, 2.0, s * 0.04), (0.28, -0.014, 0.0035, 0.0035, 2.0, s * 0.03),
                     (0.31, -0.012, 0.0035, 0.0035, 2.0, s * 0.024)], 8), RUBBER)
    optic(b, 0.0, 0.09, 0.3, 0.016, 0.024, base=0.0525)
    grip = grip2(b, -0.05, -0.054, 16, 0.105, 0.032, 0.048, POLY)
    mag = mag_box(b, 0.08, -0.058, 0.07, 0.03, 0.08, 0.0, 0, POLY, ribs=0, base=ACCENT)
    butt = stock_adj(b, -0.16, -0.44, 0.02, -0.075, 0.02, PAINT, thumb=True, cheek=True, monopod=False, holes=1)
    g.point("Muzzle", (0, 0.985, 0.0)); g.point("Grip", grip); g.point("Support", (0, 0.24, -0.056))
    g.point("Butt", butt); g.point("Mag", mag)


# ───────────────────────── Couleurs de contrôle, marquages, assemblage ─────────────────────────

# Couleurs des rendus de contrôle (Paint, Accent) ; dans le jeu, elles viennent de SoldierModel.GunColors.
PALETTE = {
    "slaughter": ((0.05, 0.05, 0.05), (0.42, 0.08, 0.06)), "sgn": ((0.18, 0.2, 0.16), (0.05, 0.05, 0.05)),
    "herring": ((0.28, 0.16, 0.08), (0.42, 0.08, 0.06)), "breecher": ((0.32, 0.27, 0.18), (0.05, 0.05, 0.05)),
    "monsoon": ((0.14, 0.16, 0.18), (0.05, 0.05, 0.05)), "slugshot": ((0.08, 0.09, 0.07), (0.05, 0.05, 0.05)),
    "bustershot": ((0.36, 0.08, 0.05), (0.55, 0.45, 0.1)), "fracture": ((0.7, 0.72, 0.75), (0.15, 0.2, 0.3)),
    "kff": ((0.16, 0.19, 0.13), (0.05, 0.05, 0.05)), "np": ((0.05, 0.05, 0.05), (0.2, 0.2, 0.2)),
    "lysander": ((0.13, 0.14, 0.12), (0.05, 0.05, 0.05)), "fang": ((0.3, 0.17, 0.08), (0.05, 0.05, 0.05)),
    "dunkel": ((0.22, 0.2, 0.17), (0.05, 0.05, 0.05)), "eagle": ((0.32, 0.27, 0.18), (0.05, 0.05, 0.05)),
    "flounders": ((0.1, 0.12, 0.14), (0.05, 0.05, 0.05)), "ptarmigan": ((0.15, 0.17, 0.13), (0.2, 0.21, 0.2)),
    "pylon": ((0.6, 0.62, 0.65), (0.12, 0.13, 0.15)), "penetrator": ((0.12, 0.13, 0.15), (0.6, 0.3, 0.05)),
}

# Marquages au pochoir (nom de l'arme d'EDF 5) et emplacements particuliers (u, z) quand le flanc par défaut est occupé.
LABELS = {
    "slaughter": "SLAUGHTER E", "sgn": "SGN", "herring": "G1 HERRING", "breecher": "BREECHER", "monsoon": "MONSOON",
    "slugshot": "SLUGGER SHOT", "bustershot": "BUSTER SHOT", "fracture": "FRACTURE", "kff": "KFF", "np": "NP3",
    "lysander": "LYSANDER", "fang": "MR FANG", "dunkel": "DUNKEL N", "eagle": "EAGLE G", "flounders": "FLOUNDERS",
    "ptarmigan": "PTARMIGAN HA", "pylon": "PYLON A1", "penetrator": "PENETRATOR",
}
MARK_AT = {"herring": (-0.008, -0.008), "fang": (0.04, -0.024), "ptarmigan": (-0.12, 0.004), "monsoon": (-0.03, 0.004)}

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
