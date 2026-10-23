using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Banlieue boisée envahie : pavillons incendiés (toits calcinés, murs noircis), jardins abandonnés
/// (mobilier renversé, clôtures arrachées), arbres morts, épaves sur les rues, barrages militaires,
/// arbres abattus en travers des rues, résine sur le clocher, œufs près des nids, incendies.
/// Générateur à graine fixe et hachages de position (identique chez tous les pairs).
/// </summary>
public static partial class SuburbBuilder
{
    private static RuinKit _ruins;
    private static readonly List<(Building b, BuildingStyle st)> _houseStyles = new();

    private static void BeginRuins(Ctx c)
    {
        _houseStyles.Clear();
        _ruins = new RuinKit(c.Root, c.Body, 0x5B0B, c.Night);
        _ruins.Block(new Rect2(-30, -30, 60, 60));
        foreach (var n in new[] { new Vector3(0, 0, -118), new Vector3(-185, 0, -45), new Vector3(-120, 0, 131), new Vector3(182, 0, 130) })
            _ruins.Block(n, 9f, 9f);
    }

    /// <summary>Arbre mort à la place d'un arbre de jardin ou d'alignement (une fois sur trois).</summary>
    private static ArrayMesh DeadTreeAt(Vector3 p) =>
        RuinKit.Hash(p.X, p.Z, 121) < 0.33f ? Props.DeadTree((int)(RuinKit.Hash(p.X, p.Z, 122) * 4)) : null;

    /// <summary>Lampadaire abattu (couché en travers du trottoir), avec collision.</summary>
    private static bool RuinLamp(Vector3 p, float yaw)
    {
        var r = _ruins;
        if (r == null || RuinKit.Hash(p.X, p.Z, 123) > 0.18f) return false;
        var fall = yaw + (RuinKit.Hash(p.X, p.Z, 124) - 0.5f) * 1.4f;
        var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.04f) * new Basis(Vector3.Up, Mathf.Pi / 2);
        r.Inst(Props.StreetLamp(false), new Transform3D(basis, p + Vector3.Up * 0.17f), true, 240f);
        var dir = new Basis(Vector3.Up, fall) * Vector3.Back;
        r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + dir * 3.6f + Vector3.Up * 0.18f), new Vector3(0.32f, 0.32f, 7.2f));
        return true;
    }

    /// <summary>Voiture garée : épave calcinée (vrai) ou voiture abandonnée couverte de cendre, portière ouverte.</summary>
    private static bool RuinCar(Ctx c, Vector3 p, float yaw, Props.VehicleType type, ref Color paint, float len)
    {
        var r = _ruins;
        if (r == null) return false;
        paint = paint.Lerp(new Color(0.34f, 0.33f, 0.3f), 0.5f).Darkened(0.1f);
        r.Take(p, len * 0.5f);
        var fate = RuinKit.Hash(p.X, p.Z, 125);
        if (fate < 0.38f)
        {
            var v = (int)(RuinKit.Hash(p.X, p.Z, 126) * 4);
            var t = type == Props.VehicleType.Van ? Props.VehicleType.Van : Props.VehicleType.Car;
            var size = Props.WreckSize(t, v);
            r.Inst(t == Props.VehicleType.Van ? Props.WreckVan(v % 2) : Props.WreckCar(v), RuinKit.At(p, yaw), true, 300f);
            r.BoxOnGround(p, yaw, size);
            r.Decal(Mats.Decal.Scorch, p, size.Z * 1.5f, yaw, p.Y + 0.03f, 0.75f);
            if (fate < 0.05f) r.Smolder(p + Vector3.Up * 1.2f, 0.9f);
            return true;
        }
        if (fate < 0.55f && type == Props.VehicleType.Car)
        {
            var hinge = p + new Basis(Vector3.Up, yaw) * new Vector3(-0.92f, 0, len * 0.18f);
            r.Inst(Props.CarDoor(paint), new Transform3D(new Basis(Vector3.Up, yaw - 1.0f), hinge), true, 160f);
        }
        return false;
    }

    // ───────────────────────── Habillage ─────────────────────────

    private static void DressSuburb(Ctx c)
    {
        var r = _ruins;
        var rng = r.Rng;
        var rects = new List<Rect2>();
        foreach (var b in c.Res.Buildings)
        {
            var rect = new Rect2(b.Position.X - b.Size.X / 2, b.Position.Z - b.Size.Z / 2, b.Size.X, b.Size.Z);
            rects.Add(rect);
            r.Block(rect.Grow(0.5f));
        }

        // Scènes d'abord (priorité des lumières de foyers).
        MilitaryRoadblock(c, _grande, -92f);
        MilitaryRoadblock(c, _r12, 62f);
        BelfryResin(c);

        // Pavillons incendiés ou noircis, jardins abandonnés.
        var burnt = 0;
        foreach (var (b, st) in _houseStyles)
        {
            var h = RuinKit.Hash(b.Id, 51, 7);
            var home = st.Use is HouseUse.Home or HouseUse.Hall;
            if (home && h < 0.17f)
            {
                BurnHouse(c, b, st, burnt++);
                continue;
            }
            if (h < 0.45f) RuinKit.SootWalls(b, b.Size, b.Id, b.Size.Y * 0.8f);
            if (st.Use == HouseUse.Home) AbandonedGarden(c, b, st);
        }

        // Épaves et arbres abattus le long des rues.
        foreach (var road in c.Roads)
        {
            if (road.Gravel) continue;
            for (var s = 12f; s < road.Length - 12f; s += 13f)
            {
                var roll = rng.Randf();
                if (roll > 0.12f) continue;
                var (p, t) = road.At(s);
                if (Mathf.Abs(p.X) > 228 || p.Z > SouthEdge - 4 || OnBridge(p)) continue;
                var lat = Vector3.Up.Cross(t) * rng.RandfRange(-road.Hw * 0.5f, road.Hw * 0.5f);
                var q = p + lat + Vector3.Up * RoadY(road);
                var yaw = Mathf.Atan2(t.X, t.Z) + (rng.Randf() < 0.5f ? 0 : Mathf.Pi) + rng.RandfRange(-0.6f, 0.6f);
                if (roll < 0.03f && road.Hw <= 3.5f) FallenTree(q, t, road);
                else if (r.Free(q, 3f)) Wreck(q, yaw, rng);
            }
        }

        // Marques au sol : fissures et brûlures sur les rues, cendres et cratères dans les jardins.
        foreach (var road in c.Roads)
        {
            if (road.Gravel) continue;
            for (var s = 6f; s < road.Length - 6f; s += 9f)
            {
                if (rng.Randf() > 0.35f) continue;
                var (p, t) = road.At(s);
                if (Mathf.Abs(p.X) > 230 || OnBridge(p)) continue;
                var q = p + Vector3.Up.Cross(t) * rng.RandfRange(-road.Hw, road.Hw);
                var roll = rng.Randf();
                var kind = roll < 0.4f ? Mats.Decal.Cracks : roll < 0.6f ? Mats.Decal.Ichor : roll < 0.78f ? Mats.Decal.Scorch : roll < 0.9f ? Mats.Decal.Oil : Mats.Decal.Acid;
                r.Decal(kind, q, kind == Mats.Decal.Scorch || kind == Mats.Decal.Cracks ? rng.RandfRange(4f, 8f) : rng.RandfRange(2f, 4f),
                    rng.RandfRange(0, Mathf.Tau), RoadY(road) + 0.02f + s * 0.00001f, rng.RandfRange(0.7f, 1.3f));
            }
        }
        for (var i = 0; i < 70; i++)
        {
            var p = new Vector3(rng.RandfRange(-150, 225), 0, rng.RandfRange(-158, 80));
            var code = c.Mask.Get(p.X, p.Z);
            if (code is RoadC or RiverC || (Mathf.Abs(p.X) < 20 && Mathf.Abs(p.Z) < 20)) continue;
            var roll = rng.Randf();
            var kind = roll < 0.45f ? Mats.Decal.Ash : roll < 0.65f ? Mats.Decal.Scorch : roll < 0.8f ? Mats.Decal.Crater : Mats.Decal.Ichor;
            r.Decal(kind, p, rng.RandfRange(3.5f, 9f), rng.RandfRange(0, Mathf.Tau), 0.035f + i * 0.0002f);
        }

        // Œufs, résine et hémolymphe près des nids.
        foreach (var n in new[] { new Vector3(0, 0, -118), new Vector3(-185, 0, -45), new Vector3(-120, 0, 131), new Vector3(182, 0, 130) })
        {
            var seed = (int)(Mathf.Abs(n.X) * 3 + Mathf.Abs(n.Z));
            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
                var p = n + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (10f + i);
                if (c.Mask.Get(p.X, p.Z) == RiverC) continue;
                Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.5f, seed + i * 7);
                r.Decal(Mats.Decal.Acid, p, 5f, a, 0.04f);
            }
            for (var i = 0; i < 5; i++)
            {
                var a = RuinKit.Hash(seed, i, 7) * Mathf.Tau;
                var p = n + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (6f + i * 3);
                r.Decal(Mats.Decal.Ichor, p, 3f + i * 0.4f, a, 0.042f + i * 0.0003f);
            }
        }

        // Débris le long des rues (papiers, tuiles, planches, tôles).
        foreach (var road in c.Roads)
        {
            for (var s = 2f; s < road.Length; s += 2.2f)
            {
                var (p, t) = road.At(s);
                if (Mathf.Abs(p.X) > 232 || p.Z > SouthEdge || OnBridge(p)) continue;
                var hsh = RuinKit.Hash(p.X, p.Z, 131 + road.Index);
                if (hsh > 0.22f) continue;
                var q = p + Vector3.Up.Cross(t) * ((RuinKit.Hash(p.X, p.Z, 132) - 0.5f) * road.Edge * 2.2f);
                var kind = (int)(RuinKit.Hash(p.X, p.Z, 133) * Props.DebrisKinds);
                q.Y = RoadY(road);
                r.Inst(Props.Debris(kind), RuinKit.At(q, hsh * 40f, 0, 0, 0.75f + hsh * 2f), false, 90f);
            }
        }

        r.DistantPlumes(new[]
        {
            new Vector3(-360, 40, -230), new Vector3(70, 45, -420), new Vector3(330, 40, -280),
            new Vector3(430, 30, 60), new Vector3(-440, 30, 80), new Vector3(120, 20, 420),
        });
        r.Flush();
        _ruins = null;
        _houseStyles.Clear();
    }

    /// <summary>Épave sur la chaussée (voiture, fourgon, parfois retournée).</summary>
    private static void Wreck(Vector3 p, float yaw, RandomNumberGenerator rng)
    {
        var r = _ruins;
        var van = rng.Randf() < 0.18f;
        var v = rng.RandiRange(0, 3);
        var t = van ? Props.VehicleType.Van : Props.VehicleType.Car;
        var size = Props.WreckSize(t, v);
        var flip = !van && rng.Randf() < 0.15f;
        var b = new Basis(Vector3.Up, yaw);
        var origin = p;
        if (flip) { b *= new Basis(Vector3.Back, Mathf.Pi); origin += Vector3.Up * (size.Y + 0.02f); }
        var xf = new Transform3D(b, origin);
        r.Inst(van ? Props.WreckVan(v % 2) : Props.WreckCar(v), xf, true, 300f);
        r.Box(new Transform3D(b, xf * new Vector3(0, size.Y / 2, 0)), size);
        r.Take(p, size.Z / 2);
        r.Decal(Mats.Decal.Scorch, p, size.Z * 1.5f, yaw, p.Y + 0.03f, 0.75f);
        if (rng.Randf() < 0.08f) r.Smolder(p + Vector3.Up * 1.2f, 0.9f);
    }

    /// <summary>Arbre mort abattu en travers de la rue (tronc solide).</summary>
    private static void FallenTree(Vector3 p, Vector3 t, Road road)
    {
        var r = _ruins;
        if (!r.Free(p, 4f)) return;
        var across = Vector3.Up.Cross(t).Normalized();
        var dir = (across + t * 0.35f).Normalized();
        var yaw = Mathf.Atan2(dir.X, dir.Z);
        var foot = p - dir * (road.Hw + 1.5f);
        // L'arbre couché : axe du tronc (Y du modèle) vers « dir ».
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.08f);
        var v = (int)(RuinKit.Hash(p.X, p.Z, 141) * 2);
        r.Inst(Props.DeadTree(v), new Transform3D(basis, foot + Vector3.Up * 0.3f), true, 300f);
        var len = 6.5f + v * 0.8f;
        r.Box(new Transform3D(new Basis(Vector3.Up, yaw), foot + dir * (len * 0.35f) + Vector3.Up * 0.35f), new Vector3(0.6f, 0.6f, len * 0.7f));
        // Souche arrachée et motte de terre.
        Props.ResinBlob(r.Kit(RuinKit.Layer.Solid, foot), foot + Vector3.Up * 0.1f, new Vector3(0.9f, 0.5f, 0.9f), (int)p.X);
        r.Decal(Mats.Decal.Dust, foot, 3.5f, yaw, p.Y + 0.03f);
        r.Take(p, 4f);
    }

    /// <summary>Pavillon incendié : toit calciné, poutres apparentes, murs noircis, gravats, feu ou fumée.</summary>
    private static void BurnHouse(Ctx c, Building b, BuildingStyle st, int n)
    {
        var r = _ruins;
        var s = b.Size;
        RuinKit.SootWalls(b, s, b.Id, s.Y);
        // Toit : pans recouverts de tuiles noircies et de trous (même géométrie que Building.HomeRoof).
        var k = new MeshKit();
        var ridgeX = st.Facing <= 1;
        var lu = ridgeX ? s.X : s.Z;
        var lv = ridgeX ? s.Z : s.X;
        var sv = lv / 2;
        var pitch = st.RoofPitch > 0 ? st.RoofPitch : 0.8f;
        var rr = sv * pitch;
        const float oe = 0.45f, og = 0.35f, t = 0.16f, lift = 0.06f;
        var hip = st.Roof == RoofShape.Hip;
        Vector3 W(float u, float v, float y) => ridgeX ? new Vector3(u, y, v) : new Vector3(v, y, u);
        var h = s.Y;
        var yE = h - oe * pitch + t + lift;
        var yR = h + rr + t + lift;
        var ue = lu / 2 + (hip ? oe : og);
        var rh = hip ? Mathf.Max(0, lu / 2 - sv) : ue;
        var roof = Mats.BurntRoof();
        k.Col = Colors.White;
        foreach (var sg in new[] { -1f, 1f })
        {
            var a = W(-ue, sg * (sv + oe), yE);
            var bb = W(ue, sg * (sv + oe), yE);
            var cc = W(rh, 0, yR);
            var e = W(-rh, 0, yR);
            if ((bb - a).Cross(cc - a).Y > 0) k.Quad(roof, a, bb, cc, e); else k.Quad(roof, a, e, cc, bb);
            if (hip)
            {
                var run = sv + oe;
                var p0 = W(sg * ue, -run, yE);
                var p1 = W(sg * ue, run, yE);
                var p2 = W(sg * rh, 0, yR);
                k.Tri(roof, p0, p1, p2, new Vector3(0, -50, 0));
            }
        }
        // Chevrons calcinés dressés au-dessus d'un trou du toit.
        var d = Mats.Detail();
        k.Col = new Color(0.06f, 0.05f, 0.045f);
        var side = RuinKit.Hash(b.Id, 52, 7) < 0.5f ? -1f : 1f;
        for (var i = 0; i < 5; i++)
        {
            var u = (i - 2) * 0.6f + (RuinKit.Hash(b.Id, i, 53) - 0.5f) * 0.3f;
            var top = W(u, 0, yR + 0.1f);
            var low = W(u, side * sv * (0.6f + RuinKit.Hash(b.Id, i, 54) * 0.3f), yE + (yR - yE) * 0.25f);
            k.Tube(d, low, top + Vector3.Up * RuinKit.Hash(b.Id, i, 55) * 0.4f, 0.08f, 4);
        }
        k.Col = new Color(0.05f, 0.045f, 0.04f);
        k.Tube(d, W(-lu * 0.3f, 0, yR + 0.05f), W(lu * 0.25f, 0, yR - 0.4f), 0.13f, 5); // faîtière effondrée
        b.AddChild(new MeshInstance3D { Name = "BurntRoof", Mesh = k.Commit() });

        // Gravats et tuiles tombées devant, cendres, incendie.
        var face = st.Facing switch { 0 => Vector3.Back, 1 => Vector3.Forward, 2 => Vector3.Right, _ => Vector3.Left };
        var depth = (st.Facing <= 1 ? s.Z : s.X) / 2;
        var pc = b.Position + face * (depth + 1.3f) + new Vector3(face.Z, 0, face.X) * ((RuinKit.Hash(b.Id, 56, 7) - 0.5f) * 3f);
        pc.Y = 0.02f;
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, pc), pc, Mathf.Atan2(face.X, face.Z), new Vector3(4f, 1f, 2.4f), b.Id * 5, new Color(0.3f, 0.25f, 0.22f), true);
        r.Convex(pts);
        r.Take(pc, 2.2f);
        r.Decal(Mats.Decal.Ash, b.Position + face * depth, Mathf.Max(s.X, s.Z) * 1.4f, b.Id, 0.04f);
        if (n % 2 == 0) r.Fire(b.Position + Vector3.Up * (h + rr * 0.4f), 1.8f, n < 4);
        else r.Smolder(b.Position + Vector3.Up * (h + rr), 2.2f);
    }

    /// <summary>Jardin de devant abandonné : mobilier renversé, clôture arrachée, valises, matelas.</summary>
    private static void AbandonedGarden(Ctx c, Building b, BuildingStyle st)
    {
        var r = _ruins;
        var hsh = RuinKit.Hash(b.Id, 61, 7);
        if (hsh > 0.55f) return;
        var face = st.Facing switch { 0 => Vector3.Back, 1 => Vector3.Forward, 2 => Vector3.Right, _ => Vector3.Left };
        var lat = new Vector3(Mathf.Abs(face.Z), 0, Mathf.Abs(face.X));
        var depth = (st.Facing <= 1 ? b.Size.Z : b.Size.X) / 2;
        var width = (st.Facing <= 1 ? b.Size.X : b.Size.Z) / 2;
        var n = 1 + (int)(RuinKit.Hash(b.Id, 62, 7) * 3);
        for (var i = 0; i < n; i++)
        {
            var off = (RuinKit.Hash(b.Id, 63 + i, 7) - 0.5f) * width * 1.6f;
            var p = b.Position + face * (depth + 1.6f + RuinKit.Hash(b.Id, 70 + i, 7) * 2.2f) + lat * off;
            p.Y = 0.03f;
            if (!r.Free(p, 0.9f)) continue;
            r.Take(p, 0.9f);
            var kind = (int)(RuinKit.Hash(b.Id, 80 + i, 7) * 6);
            var yaw = RuinKit.Hash(b.Id, 90 + i, 7) * Mathf.Tau;
            switch (kind)
            {
                case 0: r.Inst(Props.GardenChair(), new Transform3D(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, 1.45f), p + Vector3.Up * 0.24f), false, 120f); break;
                case 1: r.Inst(Props.GardenTable(), new Transform3D(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, 1.5f), p + Vector3.Up * 0.5f), false, 130f); break;
                case 2:
                    r.Inst(Props.Sofa(), RuinKit.At(p, yaw), true, 150f);
                    r.BoxOnGround(p, yaw, new Vector3(1.9f, 0.85f, 0.85f));
                    break;
                case 3: r.Inst(Props.Mattress(), RuinKit.At(p, yaw, 0.1f), false, 130f); break;
                case 4: r.Inst(Props.FencePanel(), RuinKit.At(p, yaw), false, 120f); break;
                default: r.Inst(Props.Suitcase(), RuinKit.At(p, yaw), false, 100f); break;
            }
        }
        if (hsh < 0.12f) r.Decal(Mats.Decal.Ash, b.Position + face * (depth + 3f), 5f, b.Id, 0.04f);
    }

    /// <summary>Barrage militaire abandonné sur une rue : séparateurs, sacs de sable, camion, barbelés.</summary>
    private static void MilitaryRoadblock(Ctx c, Road road, float x)
    {
        var r = _ruins;
        var s = road == _grande ? ArcAtX(road, x) : ArcAtZ(road, x);
        var (p, t) = road.At(s);
        p.Y = RoadY(road);
        var lft = Vector3.Up.Cross(t);
        var yaw = Mathf.Atan2(t.X, t.Z);
        // Chicane de séparateurs.
        // Chicane : un séparateur de chaque côté, décalés, pour laisser passer les véhicules (char compris).
        foreach (var (along, lat) in new[] { (-6f, -(road.Hw - 1.5f)), (2f, road.Hw - 1.5f) })
        {
            var q = p + t * along + lft * lat;
            r.Inst(Props.Barrier(along > 0 ? 1 : 0), RuinKit.At(q, yaw + Mathf.Pi / 2 + along * 0.01f), true, 240f);
            r.BoxOnGround(q, yaw + Mathf.Pi / 2 + along * 0.01f, Props.BarrierSize);
        }
        // Nid de sacs de sable sur l'accotement.
        var nest = p + lft * (road.Edge + 2.4f) - t * 2f;
        var k = r.Kit(RuinKit.Layer.Solid, nest);
        foreach (var (a, b) in new[] { (nest - t * 1.6f + lft * 1.4f, nest + t * 1.6f + lft * 1.4f), (nest + t * 1.6f + lft * 1.4f, nest + t * 1.6f - lft * 1.4f) })
        {
            var (bc, bs, byaw) = Props.SandbagWall(k, a, b, 6, (int)(a.X * 3 + a.Z));
            r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
        }
        // Camion bâché abandonné sur le bas-côté.
        var tp = p - t * 14f - lft * (road.Hw + 1.4f);
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, yaw + 0.12f), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, yaw + 0.12f), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        Props.RazorWire(k, p + t * 7f - lft * (road.Edge + 3f), p + t * 7f - lft * (road.Hw - 0.5f));
        var bp = nest - lft * 0.2f - t * 0.2f;
        r.Inst(Props.Drum(1), RuinKit.At(bp, 0), true, 160f);
        r.Fire(bp + Vector3.Up * 0.85f, 0.45f);
        r.Decal(Mats.Decal.Scorch, p, 8f, yaw, p.Y + 0.02f);
        r.Take(p, 9f);
        r.Take(tp, 4.5f);
    }

    /// <summary>Résine qui ronge le pied et l'angle du clocher, cocon accroché à l'abat-son.</summary>
    private static void BelfryResin(Ctx c)
    {
        var r = _ruins;
        var corner = new Vector3(3.75f, 0, -22.75f);
        var k = r.Kit(RuinKit.Layer.Organic, corner);
        for (var i = 0; i < 12; i++)
        {
            var y = 0.3f + i * 1.45f;
            var s = 1.5f - i * 0.09f;
            Props.ResinBlob(k, corner + new Vector3(0.2f + s * 0.15f, y, 0.2f + s * 0.15f), new Vector3(s, s * 1.1f, s), 300 + i);
        }
        Props.ResinBlob(k, corner + new Vector3(1.4f, 0.2f, 1.2f), new Vector3(2.6f, 0.8f, 2.2f), 320);
        Props.ResinBlob(k, new Vector3(-3.9f, 15.5f, -26.5f), new Vector3(1.0f, 2.2f, 1.2f), 321);
        Props.Web(r.Kit(RuinKit.Layer.Organic, corner), new[]
        {
            new Vector3(-3.75f, 18f, -22.7f), new Vector3(3.75f, 15f, -22.7f), new Vector3(6f, 0.3f, -14f), new Vector3(-5f, 0.3f, -12f),
        }, 330);
    }
}
