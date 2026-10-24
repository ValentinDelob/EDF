using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Effets de destruction du décor, joués chez chaque pair à réception d'un événement de l'hôte : chute des poteaux
/// et des arbres (pivot au pied, accélération de tige qui bascule, rebond), mobilier qui éclate en débris
/// (réserve de corps rigides <see cref="DebrisPool"/> près de la caméra, éclats de particules au-delà), voitures
/// cabossées, en feu puis explosées (épave calcinée qui saute), épaves aplaties, conteneurs poussés.
/// Le niveau de détail baisse avec la distance et la charge des émetteurs.
/// </summary>
public partial class Destructibles
{
    private const int MaxFires = 10;
    private const float RigidRange = 75f, FxRange = 260f;

    private sealed class Fall
    {
        public PropEntry E;
        public Basis Start;
        public Vector3 Axis, Dir;
        public float A, V, Target, G;
        public int Bounces;
    }

    private sealed class Anim
    {
        public PropEntry E;
        public float T, Dur, Hop, Spin;
        public Transform3D From, To;
        public Vector3 SpinAxis;
    }

    private readonly List<Fall> _falls = new();
    private readonly List<Anim> _anims = new();
    private readonly List<(Node node, float at, Action then)> _expire = new();
    private readonly Dictionary<Mesh, Color[]> _palettes = new();
    private readonly RandomNumberGenerator _rng = new() { Seed = 77 };
    private int _fires, _fireLights;
    private float _fxClock;

    private float R(float a, float b) => _rng.RandfRange(a, b);
    private Vector3 RandV(float s) => new(R(-s, s), R(-s, s), R(-s, s));

    // ------------------------------------------------------------------ Visuel

    private static void SetXf(PropEntry e, Transform3D xf)
    {
        if (e.Slot?.Mm != null && e.Index >= 0) e.Slot.Mm.SetInstanceTransform(e.Index, new Transform3D(xf.Basis, xf.Origin - e.Slot.Origin));
        else if (e.Node != null && IsInstanceValid(e.Node)) e.Node.GlobalTransform = xf;
    }

    private static void Hide(PropEntry e)
    {
        if (e.Slot?.Mm != null && e.Index >= 0) e.Slot.Mm.SetInstanceTransform(e.Index, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), e.Xf.Origin - e.Slot.Origin));
        else if (e.Node != null && IsInstanceValid(e.Node)) e.Node.Visible = false;
        HideExtras(e);
    }

    private static void HideExtras(PropEntry e)
    {
        if (e.Extras == null) return;
        foreach (var n in e.Extras)
            if (IsInstanceValid(n)) n.Visible = false;
    }

    private void DisableShapes(PropEntry e)
    {
        if (e.Shapes == null) return;
        foreach (var cs in e.Shapes) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
    }

    /// <summary>Couleurs dominantes du maillage (couleurs de sommet, pondérées par leur nombre), mises en cache.</summary>
    private Color[] Palette(PropEntry e)
    {
        if (!_palettes.TryGetValue(e.Mesh, out var pal))
        {
            var counts = new Dictionary<int, (int n, Color c)>();
            for (var s = 0; s < e.Mesh.GetSurfaceCount(); s++)
            {
                var arrays = e.Mesh.SurfaceGetArrays(s);
                var cols = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
                if (cols == null || cols.Length == 0) continue;
                for (var i = 0; i < cols.Length; i += 3)
                {
                    var c = cols[i];
                    var key = (int)(c.R * 7.99f) * 64 + (int)(c.G * 7.99f) * 8 + (int)(c.B * 7.99f);
                    counts.TryGetValue(key, out var v);
                    counts[key] = (v.n + 1, c);
                }
            }
            var list = new List<(int n, Color c)>(counts.Values);
            list.Sort((a, b) => b.n.CompareTo(a.n));
            pal = new Color[Mathf.Clamp(list.Count, 1, 4)];
            for (var i = 0; i < pal.Length; i++) pal[i] = i < list.Count ? list[i].c : new Color(0.35f, 0.34f, 0.32f);
            _palettes[e.Mesh] = pal;
        }
        if (e.Tint == Colors.White) return pal;
        var tinted = new Color[pal.Length];
        for (var i = 0; i < pal.Length; i++) tinted[i] = i == 0 ? e.Tint : pal[i];
        return tinted;
    }

    private bool Night => _world?.Mission?.Time == TimeOfDay.Night;

    private ExplosionFx Fx => ExplosionFx.For(_world);

    /// <summary>Lance un morceau : corps rigide près de la caméra, éclat de particules plus loin.</summary>
    private void Piece(Vector3 pos, Basis rot, Vector3 size, Vector3 vel, Color c, float dist, float life = 8f)
    {
        if (dist < RigidRange) DebrisPool.For(_world).Spawn(new Transform3D(rot, pos), size, vel, RandV(5f), c, life + R(-1.5f, 1.5f));
        else Fx.Chunk(pos, vel, Mathf.Max(size.X, Mathf.Max(size.Y, size.Z)) * 0.6f, R(3f, 5f), c);
    }

    private void DustAt(Vector3 p, float size, int n, float spread, float dist, Color? color = null)
    {
        if (dist > FxRange) return;
        var c = color ?? new Color(0.3f, 0.285f, 0.26f, 0.5f);
        var fx = Fx;
        for (var i = 0; i < n; i++)
        {
            var d = new Vector3(R(-1, 1), R(0.1f, 0.6f), R(-1, 1));
            fx.DustPuff(p + new Vector3(d.X * spread, 0.2f, d.Z * spread), d * R(1f, 3f), size * R(0.7f, 1.2f), R(2.5f, 4.5f), c * R(0.85f, 1.1f));
        }
    }

    private void GlassBurst(Vector3 p, int n, float dist)
    {
        if (dist > 120f) return;
        var fx = Fx;
        for (var i = 0; i < n; i++)
        {
            var d = new Vector3(R(-1, 1), R(0.2f, 1.2f), R(-1, 1));
            fx.Spark(p + RandV(0.5f), new Vector3(d.X * R(1.5f, 4.5f), d.Y * R(1f, 3f), d.Z * R(1.5f, 4.5f)), R(0.02f, 0.04f), R(0.6f, 1.3f), new Color(0.75f, 0.85f, 0.9f, 0.45f));
        }
        Sfx.At("glass", p, -2f, R(0.9f, 1.15f), 60);
    }

    // ------------------------------------------------------------------ Mobilier qui éclate

    private void FxBreak(PropEntry e, Vector3 dir)
    {
        Hide(e);
        DisableShapes(e);
        var c = e.Center;
        var dist = Fx.CameraDistance(c);
        if (dist > FxRange) return;
        var pal = Palette(e);
        var size = e.Box.Size * e.Scale;
        var n = e.Kind switch { PropKind.Small => 3, PropKind.Furniture => 5, PropKind.Shelter => 11, PropKind.Barrier => 5, _ => 4 };
        if (dist > RigidRange) n = Mathf.Max(2, n / 2);
        var basis = e.Xf.Basis.Orthonormalized();
        for (var i = 0; i < n; i++)
        {
            var s = e.Kind switch
            {
                // Abri : vitres et toit en panneaux minces ; mobilier : lattes ; béton : blocs.
                PropKind.Shelter => new Vector3(R(0.4f, 1.1f), R(0.4f, 1.2f), R(0.03f, 0.06f)),
                PropKind.Furniture => new Vector3(size.X * R(0.3f, 0.6f), R(0.05f, 0.12f), Mathf.Max(size.Z * R(0.15f, 0.35f), 0.08f)),
                PropKind.Barrier => size * new Vector3(R(0.3f, 0.5f), R(0.35f, 0.6f), R(0.35f, 0.55f)),
                _ => size * new Vector3(R(0.35f, 0.6f), R(0.3f, 0.5f), R(0.35f, 0.6f)),
            };
            var off = basis * new Vector3(R(-0.3f, 0.3f) * size.X, R(-0.2f, 0.3f) * size.Y, R(-0.3f, 0.3f) * size.Z);
            var vel = dir * R(1.5f, 4.5f) + Vector3.Up * R(1.5f, 4f) + RandV(1.2f);
            if (e.Kind == PropKind.Barrier) vel *= 0.6f;
            Piece(c + off, basis * new Basis(RandV(1f).Normalized() + Vector3.Up * 0.01f, R(0, 3f)), s, vel, e.Kind == PropKind.Shelter && i % 3 != 0 ? new Color(0.5f, 0.56f, 0.58f, 0.3f) : pal[i % pal.Length], dist);
        }
        var fx = Fx;
        var bits = dist < 120f ? 10 : 4;
        for (var i = 0; i < bits; i++)
            fx.Chunk(c + RandV(0.3f), dir * R(1f, 4f) + Vector3.Up * R(2f, 5f) + RandV(1.5f), R(0.04f, 0.12f), R(2.5f, 4f), pal[i % pal.Length] * 0.8f);
        var big = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        DustAt(e.Xf.Origin, big * 0.55f + 0.6f, e.Kind == PropKind.Barrier ? 5 : 3, big * 0.3f, dist,
            e.Kind == PropKind.Barrier ? new Color(0.42f, 0.4f, 0.37f, 0.6f) : null);
        if (e.Kind == PropKind.Shelter) GlassBurst(c + Vector3.Up * 0.5f, 26, dist);
        if (e.Kind == PropKind.Barrier) Sfx.At("hammer", c, 2f, R(0.5f, 0.65f), 40);
        else Sfx.At("smash", c, e.Kind == PropKind.Small ? -4f : 0f, R(0.85f, 1.15f), 40);
    }

    // ------------------------------------------------------------------ Poteaux et arbres qui tombent

    private void StartFall(PropEntry e, Vector3 dir)
    {
        DisableShapes(e);
        HideExtras(e);
        foreach (var f in _falls) if (f.E == e) return;
        var tree = e.Kind == PropKind.Tree;
        var l = Mathf.Max(e.Height, 1.2f);
        _falls.Add(new Fall
        {
            E = e, Start = e.Visual.Basis, Dir = dir, Axis = Vector3.Up.Cross(dir).Normalized(),
            A = 0.03f, V = tree ? 0.12f : 0.35f, G = 1.5f * 9.8f / l,
            Target = Mathf.Pi / 2 - (tree ? 0.12f : 0.035f),
        });
        var dist = Fx.CameraDistance(e.Xf.Origin);
        // Pied arraché : éclats et poussière au pied, grincement (métal) ou craquement (bois).
        DustAt(e.Xf.Origin, 1.2f, 2, 0.3f, dist);
        if (dist < 120f)
            for (var i = 0; i < 6; i++)
                Fx.Chunk(e.Xf.Origin + Vector3.Up * 0.3f, dir * R(1f, 3f) + Vector3.Up * R(1.5f, 3.5f) + RandV(1f), R(0.04f, 0.1f), R(2f, 3.5f), tree ? new Color(0.3f, 0.22f, 0.14f) : new Color(0.25f, 0.25f, 0.26f));
        Sfx.At(tree ? "smash" : "pole_fall", e.Xf.Origin + Vector3.Up * 2f, tree ? 2f : -2f, tree ? 0.55f : R(1.05f, 1.2f), 40);
    }

    private void TickFall(Fall f, float dt)
    {
        f.V += f.G * Mathf.Sin(f.A + 0.06f) * dt;
        f.A += f.V * dt;
        if (f.A >= f.Target)
        {
            f.A = f.Target;
            if (f.Bounces == 0) Impact(f);
            f.Bounces++;
            f.V = f.Bounces == 1 ? -f.V * 0.16f : 0f;
        }
        var e = f.E;
        SetXf(e, new Transform3D(new Basis(f.Axis, f.A) * f.Start, e.Xf.Origin));
    }

    private void Impact(Fall f)
    {
        var e = f.E;
        var tree = e.Kind == PropKind.Tree;
        var l = e.Height;
        var tip = e.Xf.Origin + f.Dir * l;
        var dist = Fx.CameraDistance(e.Xf.Origin + f.Dir * (l * 0.5f));
        // Nuage de poussière le long du fût, feuilles / éclats à la cime.
        var n = Mathf.Clamp((int)(l / 1.6f), 2, 7);
        for (var i = 1; i <= n; i++)
        {
            var p = e.Xf.Origin + f.Dir * (l * i / (n + 0.5f));
            DustAt(p, tree ? 1.8f : 1.1f, 1, 0.2f, dist);
        }
        if (dist < 120f)
        {
            var fx = Fx;
            for (var i = 0; i < (tree ? 14 : 8); i++)
                fx.Chunk(tip + RandV(0.6f) + Vector3.Up * 0.4f, RandV(2.5f) + Vector3.Up * R(2f, 4f), R(0.04f, 0.12f), R(2f, 4f),
                    tree ? new Color(0.24f, 0.2f, 0.12f) : new Color(0.22f, 0.22f, 0.24f));
            if (!tree)
                for (var i = 0; i < 12; i++)
                    fx.Spark(tip + Vector3.Up * 0.3f, RandV(4f) + Vector3.Up * R(2f, 5f), R(0.02f, 0.04f), R(0.4f, 0.9f), new Color(1f, 0.7f, 0.3f, 0.9f));
        }
        Sfx.At(tree ? "hammer" : "vehicle_crash", tip, tree ? 3f : -3f, tree ? 0.4f : R(1.3f, 1.5f), 40);
        _world.ShakeCamera(tip, tree ? 0.3f : 0.15f, tree ? 30f : 18f);
        if (!Net.I.IsHost) return;
        // Ce qui se trouve sous le fût : dégâts légers aux insectes, bosse aux véhicules.
        var a = e.Xf.Origin + Vector3.Up * 0.5f;
        var b = tip + Vector3.Up * 0.5f;
        foreach (var en in new List<Enemy>(_world.EnemiesById.Values))
        {
            if (en.State == EnemyState.Dead || en.Def.Flying) continue;
            var q = Geometry3D.GetClosestPointToSegment(en.Center, a, b);
            if (q.DistanceTo(en.Center) <= 1f + en.Def.Size * 0.6f)
                _world.HostDamageEnemy(en.Id, (tree ? 45f : 25f) * _world.HealthMul, en.Center, e.LastBy, false);
        }
        foreach (var id in new List<int>(Near((a + b) / 2, l * 0.5f + 3f)))
        {
            var o = _all[id];
            if (o == e || !o.IsVehicle) continue;
            var q = Geometry3D.GetClosestPointToSegment(o.Center, a, b);
            if (DistanceTo(o, q) < 0.5f) HostDamage(id, tree ? 80f : 45f, PropHit.Crush, e.Xf.Origin, e.LastBy);
        }
    }

    // ------------------------------------------------------------------ Véhicules

    private Vector3 Hood(PropEntry e) => e.Xf * new Vector3(0, e.Box.End.Y * 0.62f, e.Box.End.Z * 0.55f);

    private void Track(PropEntry e, Node n) => (e.Fx ??= new List<Node>()).Add(n);

    private void FxDamaged(PropEntry e, Vector3 dir)
    {
        // Carrosserie cabossée côté impact (légère bascule) et vitres brisées.
        var side = e.Xf.Basis.Orthonormalized().Inverse() * dir;
        e.Dent = new Basis(new Vector3(side.Z, 0, -side.X).Normalized() + Vector3.Up * 0.001f, R(0.02f, 0.035f));
        e.Squash = 0.985f;
        SetXf(e, e.Visual);
        var dist = Fx.CameraDistance(e.Center);
        GlassBurst(e.Center + Vector3.Up * 0.4f, 12, dist);
        if (dist > FxRange) return;
        var smoke = RuinFire.SmokeColumn(Hood(e), 0.45f, false);
        AddChild(smoke);
        Track(e, smoke);
    }

    private void FxBurning(PropEntry e)
    {
        var hood = Hood(e);
        if (_fires < MaxFires)
        {
            var light = Night && _fireLights < 3;
            if (light) _fireLights++;
            var fire = RuinFire.Create(hood, e.Kind == PropKind.Car ? 0.75f : 0.95f, light, Night, e.Id);
            fire.SetMeta("light", light);
            AddChild(fire);
            Track(e, fire);
            _fires++;
        }
        else
        {
            var smoke = RuinFire.SmokeColumn(hood, 0.9f, true);
            AddChild(smoke);
            Track(e, smoke);
        }
        Sfx.At("spit", hood, -2f, 0.45f, 80);
    }

    private Mesh WreckMesh(PropEntry e) => e.Kind switch
    {
        PropKind.Van => Props.WreckVan(e.Id % 2),
        PropKind.Truck => Props.WreckTruck(),
        _ => Props.WreckCar(e.Id % 4),
    };

    private void FxExplode(PropEntry e, Vector3 dir)
    {
        e.Burnt = true;
        if (e.Node is MeshInstance3D mi && IsInstanceValid(mi)) mi.Mesh = WreckMesh(e);
        else if (e.Node == null) Hide(e);
        HideExtras(e);
        var c = e.Center;
        var dist = Fx.CameraDistance(c);
        // L'épave saute et retombe en tournant un peu.
        var hop = e.Kind == PropKind.Truck ? 0.35f : e.Kind == PropKind.Van ? 0.6f : 0.95f;
        e.Dent = new Basis(RandV(1f).Normalized() + Vector3.Up * 0.01f, R(0.02f, 0.05f));
        var to = e.Visual;
        _anims.Add(new Anim { E = e, Dur = 0.9f, Hop = hop, From = to, To = to, SpinAxis = new Vector3(R(-1, 1), 0, R(-1, 1)).Normalized() + Vector3.Up * 0.01f, Spin = R(0.12f, 0.3f) });
        if (dist <= FxRange)
        {
            // Portières, capot, pare-chocs (peinture de la carrosserie), pièces sombres, éclats de verre.
            var pal = Palette(e);
            var paint = e.Tint != Colors.White ? e.Tint : pal[0];
            var basis = e.Xf.Basis.Orthonormalized();
            var nPanels = dist < RigidRange ? 5 : 3;
            for (var i = 0; i < nPanels; i++)
            {
                var size = new Vector3(R(0.7f, 1.3f), R(0.03f, 0.06f), R(0.5f, 1.1f));
                var v = (RandV(1f) with { Y = 0 }).Normalized() * R(3f, 7f) + Vector3.Up * R(6f, 11f);
                Piece(c + Vector3.Up * 0.5f + RandV(0.6f), basis * new Basis(RandV(1f).Normalized() + Vector3.Up * 0.01f, R(0, 3f)), size, v,
                    new Color(paint.R * 0.55f, paint.G * 0.5f, paint.B * 0.45f, 0.3f), dist, 10f);
            }
            for (var i = 0; i < 3; i++)
                Piece(c + RandV(0.5f), basis, new Vector3(R(0.25f, 0.5f), R(0.2f, 0.4f), R(0.25f, 0.5f)), RandV(4f) + Vector3.Up * R(4f, 8f),
                    new Color(0.06f, 0.055f, 0.05f, 0.3f), dist, 9f);
            GlassBurst(c + Vector3.Up * 0.6f, 30, dist);
        }
        // Le feu continue sur l'épave, puis couve.
        var hasFire = false;
        if (e.Fx != null)
            foreach (var n in e.Fx)
                if (n is RuinFire) hasFire = true;
        if (!hasFire) FxBurning(e);
        if (e.Fx == null) return;
        foreach (var n in e.Fx)
        {
            if (n is RuinFire fire)
            {
                var light = fire.HasMeta("light") && fire.GetMeta("light").AsBool();
                var hood = Hood(e);
                Expire(fire, R(18f, 26f), () =>
                {
                    _fires--;
                    if (light) _fireLights--;
                    var smoke = RuinFire.SmokeColumn(hood, 0.8f, true);
                    AddChild(smoke);
                    Expire(smoke, 45f, null);
                });
            }
            else Expire(n, 6f, null);
        }
        e.Fx.Clear();
    }

    private void Expire(Node n, float after, Action then) => _expire.Add((n, _fxClock + after, then));

    private void FxFlatten(PropEntry e, Vector3 dir)
    {
        e.Squash = e.Kind == PropKind.Container ? 0.55f : 0.42f;
        e.Dent = new Basis(new Vector3(dir.Z, 0, -dir.X).Normalized() + Vector3.Up * 0.001f, R(0.03f, 0.07f));
        SetXf(e, e.Visual);
        // Collision : boîte écrasée à la place de la forme exacte.
        DisableShapes(e);
        var size = e.Box.Size * e.Scale;
        size.Y *= e.Squash;
        var center = e.Xf.Origin + e.Xf.Basis.Orthonormalized() * new Vector3(e.Box.GetCenter().X * e.Scale, size.Y / 2, e.Box.GetCenter().Z * e.Scale);
        AttachShape(e, new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Transform = new Transform3D(e.Xf.Basis.Orthonormalized(), center) });
        var c = e.Center;
        var dist = Fx.CameraDistance(c);
        GlassBurst(c + Vector3.Up * 0.3f, 18, dist);
        DustAt(e.Xf.Origin, Mathf.Max(size.X, size.Z) * 0.5f, 3, size.X * 0.4f, dist);
        if (dist < 120f)
            for (var i = 0; i < 10; i++)
                Fx.Spark(c + RandV(0.8f), RandV(4f) + Vector3.Up * R(1f, 4f), R(0.02f, 0.04f), R(0.3f, 0.8f), new Color(1f, 0.75f, 0.35f, 0.9f));
        Sfx.At("vehicle_crash", c, 2f, R(0.6f, 0.75f), 40);
    }

    private void FxPush(PropEntry e, Vector3 dir, float amount)
    {
        var from = e.Visual;
        var oldXf = new Transform3D(e.Xf.Basis.Orthonormalized(), e.Xf.Origin);
        var yaw = (RuinKit.Hash(e.Id, (int)(amount * 20), 4) - 0.5f) * amount * 0.12f;
        e.Xf = new Transform3D(new Basis(Vector3.Up, yaw) * e.Xf.Basis, e.Xf.Origin + dir * amount);
        e.Dent *= new Basis(new Vector3(dir.Z, 0, -dir.X).Normalized() + Vector3.Up * 0.001f, Mathf.Min(0.01f * amount, 0.03f));
        var newXf = new Transform3D(e.Xf.Basis.Orthonormalized(), e.Xf.Origin);
        if (e.Shapes != null)
            foreach (var cs in e.Shapes) cs.Transform = newXf * oldXf.AffineInverse() * cs.Transform;
        _anims.Add(new Anim { E = e, Dur = 0.35f, From = from, To = e.Visual });
        var dist = Fx.CameraDistance(e.Center);
        DustAt(e.Xf.Origin - dir * 1f, 2f, 3, e.Radius * 0.6f, dist);
        Sfx.At("vehicle_crash", e.Center, 0f, R(0.45f, 0.55f), 60);
    }

    // ------------------------------------------------------------------ Boucle

    private void TickFx(float dt)
    {
        _fxClock += dt;
        for (var i = _falls.Count - 1; i >= 0; i--)
        {
            var f = _falls[i];
            TickFall(f, dt);
            if (f.Bounces >= 2) _falls.RemoveAt(i);
        }
        for (var i = _anims.Count - 1; i >= 0; i--)
        {
            var a = _anims[i];
            a.T += dt;
            var u = Mathf.Clamp(a.T / a.Dur, 0f, 1f);
            var k = u * u * (3f - 2f * u);
            var o = a.From.Origin.Lerp(a.To.Origin, k) + Vector3.Up * (a.Hop * 4f * u * (1f - u));
            var basis = new Basis(a.From.Basis.X.Lerp(a.To.Basis.X, k), a.From.Basis.Y.Lerp(a.To.Basis.Y, k), a.From.Basis.Z.Lerp(a.To.Basis.Z, k));
            if (a.Spin > 0) basis = new Basis(a.SpinAxis.Normalized(), a.Spin * Mathf.Sin(u * Mathf.Pi)) * basis;
            SetXf(a.E, new Transform3D(basis, o));
            if (u >= 1f)
            {
                SetXf(a.E, a.E.Visual);
                _anims.RemoveAt(i);
                if (a.Hop > 0) DustAt(a.E.Xf.Origin, 2.2f, 4, a.E.Radius * 0.6f, Fx.CameraDistance(a.E.Center));
            }
        }
        for (var i = _expire.Count - 1; i >= 0; i--)
        {
            var (n, at, then) = _expire[i];
            if (_fxClock < at) continue;
            _expire.RemoveAt(i);
            if (IsInstanceValid(n)) n.QueueFree();
            then?.Invoke();
        }
    }
}
