using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Habillage « ville envahie » commun aux trois cartes : regroupe le décor de ruines (maillages fusionnés
/// par secteur, MultiMesh à transformation complète), les collisions des gros éléments, les marques au sol,
/// les foyers d'incendie (lumière limitée par carte), les colonnes de fumée et les panaches lointains.
/// Entièrement déterministe : graines fixes et hachages de position, jamais GD.Randf.
/// </summary>
public sealed class RuinKit
{
    /// <summary>Nombre maximal de lumières de foyers par carte.</summary>
    public const int MaxLights = 8;
    private const float Cell = 64f;

    public enum Layer { Solid, Litter, Decal, Organic }

    public readonly Node3D Root;
    public readonly StaticBody3D Body;
    public readonly RandomNumberGenerator Rng;
    public readonly bool Night;
    public int Lights;

    private readonly Dictionary<(Layer, int, int), MeshKit> _kits = new();
    private readonly List<(Layer, int, int)> _order = new();
    private readonly Dictionary<(Mesh, int, int, bool), (List<Transform3D> xf, List<Color> col, float range)> _inst = new();
    private readonly List<(Mesh, int, int, bool)> _instOrder = new();
    private readonly List<(Vector2 p, float r)> _used = new();
    private readonly List<Rect2> _blocked = new();

    public RuinKit(Node3D root, StaticBody3D body, ulong seed, bool night)
    {
        Root = root;
        Body = body;
        Night = night;
        Rng = new RandomNumberGenerator { Seed = seed };
    }

    // ───────────────────────── Occupation du sol ─────────────────────────

    /// <summary>Zone interdite (empreinte d'immeuble, aire de départ, nid).</summary>
    public void Block(Rect2 r) => _blocked.Add(r);

    public void Block(Vector3 center, float halfX, float halfZ) =>
        _blocked.Add(new Rect2(center.X - halfX, center.Z - halfZ, halfX * 2, halfZ * 2));

    /// <summary>Vrai si un disque de rayon <paramref name="r"/> est libre (hors zones interdites et objets posés).</summary>
    public bool Free(Vector3 p, float r)
    {
        foreach (var b in _blocked)
            if (p.X > b.Position.X - r && p.X < b.End.X + r && p.Z > b.Position.Y - r && p.Z < b.End.Y + r) return false;
        var q = new Vector2(p.X, p.Z);
        foreach (var (u, ur) in _used)
            if (u.DistanceTo(q) < ur + r) return false;
        return true;
    }

    /// <summary>Réserve un disque (sans vérifier).</summary>
    public void Take(Vector3 p, float r) => _used.Add((new Vector2(p.X, p.Z), r));

    /// <summary>Vérifie puis réserve.</summary>
    public bool TryTake(Vector3 p, float r)
    {
        if (!Free(p, r)) return false;
        Take(p, r);
        return true;
    }

    /// <summary>Hachage stable d'une position → [0, 1[.</summary>
    public static float Hash(float x, float z, int salt)
    {
        unchecked
        {
            var h = (uint)(Mathf.RoundToInt(x * 10f) * 73856093) ^ (uint)(Mathf.RoundToInt(z * 10f) * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ───────────────────────── Géométrie ─────────────────────────

    /// <summary>MeshKit du secteur contenant <paramref name="p"/> pour une couche donnée.</summary>
    public MeshKit Kit(Layer layer, Vector3 p)
    {
        _pendingProp = -1;
        var key = (layer, Mathf.FloorToInt(p.X / Cell), Mathf.FloorToInt(p.Z / Cell));
        if (_kits.TryGetValue(key, out var k)) return k;
        k = new MeshKit();
        _kits[key] = k;
        _order.Add(key);
        return k;
    }

    /// <summary>Construit un élément dans le repère <paramref name="xf"/> (fusionné au secteur).</summary>
    public void Put(Layer layer, Transform3D xf, Action<MeshKit> build)
    {
        var k = Kit(layer, xf.Origin);
        var keep = k.Xf;
        k.Xf = xf;
        build(k);
        k.Xf = keep;
    }

    /// <summary>Instance d'un maillage partagé (MultiMesh par secteur de 64 m).</summary>
    /// <remarks>
    /// Un maillage d'une famille destructible (<see cref="Props.KindOf"/>) est enregistré auprès de
    /// <see cref="Destructibles"/> (sauf poteaux et arbres déjà couchés) ; la boîte de collision posée juste après
    /// par <see cref="Box"/> / <see cref="BoxOnGround"/> lui est rattachée.
    /// </remarks>
    public int Inst(Mesh mesh, Transform3D xf, bool shadows = true, float range = 300f, Color? color = null)
    {
        var key = (mesh, Mathf.FloorToInt(xf.Origin.X / Cell), Mathf.FloorToInt(xf.Origin.Z / Cell), shadows);
        if (!_inst.TryGetValue(key, out var b))
        {
            b = (new List<Transform3D>(), new List<Color>(), range);
            _inst[key] = b;
            _instOrder.Add(key);
            _slots[key] = new InstSlot { Origin = new Vector3((key.Item2 + 0.5f) * Cell, 0, (key.Item3 + 0.5f) * Cell) };
        }
        var index = b.xf.Count;
        b.xf.Add(xf);
        b.col.Add(color ?? Colors.White);
        var id = Destructibles.Register(mesh, Props.KindOf(mesh), xf, color);
        if (id >= 0) Destructibles.BindSlot(id, _slots[key], index);
        _pendingProp = id;
        return id;
    }

    private readonly Dictionary<(Mesh, int, int, bool), InstSlot> _slots = new();

    /// <summary>Accessoire destructible posé par le dernier <see cref="Inst"/> (reçoit la boîte suivante), -1 sinon.</summary>
    private int _pendingProp = -1;

    /// <summary>Repère posé au sol : lacet, puis tangage/roulis éventuels.</summary>
    public static Transform3D At(Vector3 p, float yaw, float pitch = 0, float roll = 0, float scale = 1f) =>
        new(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch) * new Basis(Vector3.Back, roll) * Basis.FromScale(Vector3.One * scale), p);

    // ───────────────────────── Collisions ─────────────────────────

    /// <summary>Boîte de collision (centre et orientation donnés par <paramref name="xf"/>).</summary>
    public void Box(Transform3D xf, Vector3 size)
    {
        var cs = new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Transform = xf };
        if (_pendingProp >= 0) Destructibles.AddShape(_pendingProp, cs);
        else Body.AddChild(cs);
        _pendingProp = -1;
    }

    /// <summary>Boîte posée au sol (base à y = p.Y), tournée d'un lacet.</summary>
    public void BoxOnGround(Vector3 p, float yaw, Vector3 size) =>
        Box(new Transform3D(new Basis(Vector3.Up, yaw), p + Vector3.Up * (size.Y / 2)), size);

    /// <summary>Enveloppe convexe (points en coordonnées monde).</summary>
    public void Convex(Vector3[] pts)
    {
        _pendingProp = -1;
        Body.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = pts } });
    }

    // ───────────────────────── Marques au sol ─────────────────────────

    /// <summary>Marque plate (brûlure, cratère, fissures, flaque...) posée à la hauteur <paramref name="y"/>.</summary>
    public void Decal(Mats.Decal d, Vector3 p, float size, float yaw, float y = 0.03f, float stretch = 1f)
    {
        var k = Kit(Layer.Decal, p);
        var (o, s) = Mats.DecalUv(d);
        // Marques sombres : multiplicatives (peu coûteuses) ; poussière claire et acide : fondu éclairé.
        var m = d is Mats.Decal.Dust or Mats.Decal.Acid ? Mats.GroundDecalsLit() : Mats.GroundDecals();
        var b = new Basis(Vector3.Up, yaw);
        var hx = b * new Vector3(size * 0.5f * stretch, 0, 0);
        var hz = b * new Vector3(0, 0, size * 0.5f);
        var c = new Vector3(p.X, y, p.Z);
        k.Quad(m, c - hx + hz, c + hx + hz, c + hx - hz, c - hx - hz,
            o + new Vector2(0, s.Y), o + s, o + new Vector2(s.X, 0), o);
    }

    // ───────────────────────── Feu et fumée ─────────────────────────

    /// <summary>Foyer : flammes, braises, colonne de fumée et (dans la limite par carte) lumière vacillante.</summary>
    public void Fire(Vector3 p, float scale, bool light = true)
    {
        _pendingProp = -1;
        // De jour, la lueur d'un foyer est imperceptible : les lumières sont réservées aux cartes de nuit.
        var withLight = light && Night && Lights < MaxLights;
        if (withLight) Lights++;
        var f = RuinFire.Create(p, scale, withLight, Night, (int)(Hash(p.X, p.Z, 5) * 1000));
        Root.AddChild(f);
    }

    /// <summary>Fumée seule (épave ou gravats qui couvent).</summary>
    public void Smolder(Vector3 p, float scale)
    {
        _pendingProp = -1;
        Root.AddChild(RuinFire.SmokeColumn(p, scale, scale >= 2f));
    }

    /// <summary>Panaches de fumée géants à l'horizon (incendies lointains), visibles depuis l'aire de jeu.</summary>
    public void DistantPlumes(Vector3[] spots)
    {
        foreach (var s in spots) Root.AddChild(RuinFire.Plume(s, Night));
    }

    /// <summary>Suie sur la paroi d'un cylindre vertical (cuve), en coordonnées monde.</summary>
    public void SootCylinder(Vector3 c, float radius, float height, int seed)
    {
        var k = Kit(Layer.Decal, c);
        const int seg = 40;
        var circ = Mathf.Tau * radius;
        var u0 = Hash(seed, 1, 31) * 4f;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (radius + 0.06f);
            var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (radius + 0.06f);
            var ua = u0 + circ * i / seg / 8f;
            var ub = u0 + circ * (i + 1) / seg / 8f;
            // Ordre des coins : normale vers l'extérieur.
            k.Quad(Mats.Soot(), c + d1, c + d0, c + d0 + Vector3.Up * height, c + d1 + Vector3.Up * height,
                new Vector2(ub, 1), new Vector2(ua, 1), new Vector2(ua, 1 - height / 8f), new Vector2(ub, 1 - height / 8f));
        }
    }

    // ───────────────────────── Dégâts des immeubles ─────────────────────────

    /// <summary>
    /// Calque de dégâts sur les façades d'un immeuble de ville (enfant de l'immeuble : il s'effondre avec lui).
    /// Les UV suivent la trame de fenêtres de la façade ; un décalage entier par face varie le motif.
    /// </summary>
    public static void DamageFacades(Building b, bool heavy, int seed)
    {
        var variant = Mats.FacadeVariantOf(b.FacadeMat);
        var s = b.Size;
        var k = new MeshKit();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var y0 = h > 7f ? 3.8f : 0.6f;
        var faces = new[]
        {
            (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, s.X),
            (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, s.X),
            (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, s.Z),
            (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, s.Z),
        };
        var fi = 0;
        // Calque retenu en deçà des pans coupés des angles (0,22 m) : sinon il dépasse de la silhouette dans le vide.
        const float inset = 0.3f;
        foreach (var (start0, along, normal, len0) in faces)
        {
            fi++;
            var off = normal * 0.04f;
            var start = start0 + along * inset;
            var len = len0 - 2f * inset;
            var a = start + Vector3.Up * y0 + off;
            var bb = start + along * len + Vector3.Up * y0 + off;
            var c = bb with { Y = h };
            var d = a with { Y = h };
            if (variant >= 0)
            {
                // Décalage par face : nombre entier d'U (7 m) et d'étages, pour rester aligné sur les fenêtres.
                var du = Mathf.Floor(Hash(seed, fi, 11) * 4f);
                var dv = Mathf.Floor(Hash(seed, fi, 12) * 8f);
                // U compté depuis l'angle du mur (comme la façade) : le retrait décale d'autant le début du calque.
                var u0 = du + inset / 7f;
                var u1 = u0 + len / 7f;
                var v0 = (h - y0) / 3.2f;
                k.Quad(Mats.FacadeDamage(variant, heavy && Hash(seed, fi, 13) < 0.7f), a, bb, c, d,
                    new Vector2(u0, dv + v0), new Vector2(u1, dv + v0), new Vector2(u1, dv), new Vector2(u0, dv));
            }
            else
            {
                // Hangars, conteneurs : suie générique depuis le pied des murs.
                var top = Mathf.Min(h, 8f);
                var u0 = Hash(seed, fi, 14) * 4f;
                k.Quad(Mats.Soot(), start + off, start + along * len + off, start + along * len + off + Vector3.Up * top, start + off + Vector3.Up * top,
                    new Vector2(u0, 1), new Vector2(u0 + len / 8f, 1), new Vector2(u0 + len / 8f, 1 - top / 8f), new Vector2(u0, 1 - top / 8f));
            }
            // Pied de façade noirci (vitrines incendiées).
            if (heavy && y0 > 1f && Hash(seed, fi, 15) < 0.6f)
            {
                var u0 = Hash(seed, fi, 16) * 4f;
                k.Quad(Mats.Soot(), start + off * 1.5f, start + along * len + off * 1.5f, start + along * len + off * 1.5f + Vector3.Up * 6f, start + off * 1.5f + Vector3.Up * 6f,
                    new Vector2(u0, 1), new Vector2(u0 + len / 8f, 1), new Vector2(u0 + len / 8f, 0.25f), new Vector2(u0, 0.25f));
            }
        }
        b.AddChild(new MeshInstance3D { Name = "Damage", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Suie générique sur les quatre murs d'un volume quelconque (pavillon, hangar).</summary>
    public static void SootWalls(Node3D parent, Vector3 size, int seed, float top = 8f)
    {
        var k = new MeshKit();
        var hx = size.X / 2;
        var hz = size.Z / 2;
        top = Mathf.Min(top, size.Y);
        var faces = new[]
        {
            (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, size.X),
            (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, size.X),
            (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, size.Z),
            (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, size.Z),
        };
        var fi = 0;
        foreach (var (start, along, normal, len) in faces)
        {
            fi++;
            var off = normal * 0.06f;
            var u0 = Hash(seed, fi, 21) * 4f;
            var sc = 8f / Mathf.Max(top, 3f) * 0.75f;   // panaches plus serrés sur les murs bas
            k.Quad(Mats.Soot(), start + off, start + along * len + off, start + along * len + off + Vector3.Up * top, start + off + Vector3.Up * top,
                new Vector2(u0, 1), new Vector2(u0 + len / 8f * sc, 1), new Vector2(u0 + len / 8f * sc, 1 - top / 8f * sc), new Vector2(u0, 1 - top / 8f * sc));
        }
        parent.AddChild(new MeshInstance3D { Name = "Soot", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static float Hash(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ───────────────────────── Finalisation ─────────────────────────

    /// <summary>Crée les maillages fusionnés et les MultiMesh accumulés.</summary>
    public void Flush()
    {
        foreach (var key in _order)
        {
            var k = _kits[key];
            if (k.Empty) continue;
            var (layer, cx, cz) = key;
            Root.AddChild(new MeshInstance3D
            {
                Name = $"Ruins{layer}_{cx}_{cz}",
                Mesh = k.Commit(),
                CastShadow = layer is Layer.Solid or Layer.Organic ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = layer switch { Layer.Litter => 150f, Layer.Decal => 260f, _ => 0f },
                VisibilityRangeEndMargin = 10f,
            });
        }
        _kits.Clear();
        _order.Clear();
        foreach (var key in _instOrder)
        {
            var (xfs, cols, range) = _inst[key];
            var (mesh, cx, cz, shadows) = key;
            var origin = new Vector3((cx + 0.5f) * Cell, 0, (cz + 0.5f) * Cell);
            var colored = cols.Exists(c => c != Colors.White);
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colored, Mesh = mesh, InstanceCount = xfs.Count };
            for (var i = 0; i < xfs.Count; i++)
            {
                mm.SetInstanceTransform(i, new Transform3D(xfs[i].Basis, xfs[i].Origin - origin));
                if (colored) mm.SetInstanceColor(i, cols[i]);
            }
            _slots[key].Mm = mm;
            Root.AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                Position = origin,
                CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = range + Cell * 0.71f,
            });
        }
        _inst.Clear();
        _instOrder.Clear();
        _slots.Clear();
    }
}

/// <summary>
/// Foyer d'incendie décoratif : flammes et braises (particules additives), colonne de fumée,
/// lumière vacillante à courte portée sans ombres. Purement visuel (même rendu chez tous les pairs).
/// </summary>
public partial class RuinFire : Node3D
{
    private OmniLight3D _light;
    private float _base;
    private float _t;
    private float _phase;

    public static RuinFire Create(Vector3 p, float scale, bool light, bool night, int seed)
    {
        var f = new RuinFire { Name = "Fire", Position = p, _phase = seed * 0.37f };
        f.AddChild(Flames(scale, light));
        f.AddChild(SmokeColumn(Vector3.Up * (0.8f * scale), scale, true));
        if (light)
        {
            f._base = (night ? 3.2f : 1.6f) * Mathf.Clamp(scale, 0.6f, 2f);
            f._light = new OmniLight3D
            {
                Position = Vector3.Up * (0.9f * scale + 0.4f),
                LightColor = new Color(1f, 0.55f, 0.22f),
                LightEnergy = f._base,
                OmniRange = 7f + 4f * scale,
                OmniAttenuation = 1.4f,
                ShadowEnabled = false,
                LightBakeMode = Light3D.BakeMode.Disabled,
            };
            f.AddChild(f._light);
        }
        return f;
    }

    public override void _Process(double delta)
    {
        if (_light == null) return;
        _t += (float)delta;
        var t = _t + _phase;
        var k = 0.78f + 0.12f * Mathf.Sin(t * 9.3f) + 0.07f * Mathf.Sin(t * 23.1f + 1.3f) + 0.05f * Mathf.Sin(t * 3.7f);
        _light.LightEnergy = _base * k;
    }

    private static Curve Curve2(float a, float mid, float b)
    {
        var c = new Curve { MaxValue = Mathf.Max(Mathf.Max(a, b), Mathf.Max(mid, 1f)) };
        c.AddPoint(new Vector2(0, a));
        c.AddPoint(new Vector2(0.35f, mid));
        c.AddPoint(new Vector2(1, b));
        return c;
    }

    private static Gradient Grad(params (float t, Color c)[] pts)
    {
        var g = new Gradient();
        g.SetOffset(0, pts[0].t); g.SetColor(0, pts[0].c);
        g.SetOffset(1, pts[^1].t); g.SetColor(1, pts[^1].c);
        for (var i = 1; i < pts.Length - 1; i++) g.AddPoint(pts[i].t, pts[i].c);
        return g;
    }

    private static GpuParticles3D Flames(float scale, bool embers)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 12,
            InitialVelocityMin = 0.8f * scale,
            InitialVelocityMax = 1.8f * scale,
            Gravity = new Vector3(0, 1.2f * scale, 0),
            DampingMin = 0.5f,
            DampingMax = 1f,
            ScaleMin = 0.7f,
            ScaleMax = 1.3f,
            ScaleCurve = new CurveTexture { Curve = Curve2(0.6f, 1f, 0.15f) },
            ColorRamp = new GradientTexture1D
            {
                Gradient = Grad((0, new Color(1f, 0.75f, 0.35f, 0f)), (0.12f, new Color(1f, 0.6f, 0.2f, 0.95f)),
                    (0.6f, new Color(0.9f, 0.25f, 0.05f, 0.6f)), (1, new Color(0.3f, 0.05f, 0f, 0f))),
            },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.7f, 0.1f, 0.7f) * scale,
            AngleMin = -12, AngleMax = 12,
        };
        var flames = new GpuParticles3D
        {
            Amount = Mathf.Clamp((int)(24 * scale), 14, 56),
            Lifetime = 0.9f,
            Preprocess = 1f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(1.6f, 2.4f) * scale, Material = Mats.Flame() },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-4, -1, -4) * scale, new Vector3(8, 8, 8) * scale),
        };
        // Braises montantes.
        var ep = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 25,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3.5f,
            Gravity = new Vector3(0.4f, 0.8f, 0),
            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = 2f,
            ScaleMin = 0.5f,
            ScaleMax = 1f,
            ColorRamp = new GradientTexture1D { Gradient = Grad((0, new Color(1f, 0.8f, 0.4f, 1f)), (1, new Color(1f, 0.3f, 0.05f, 0f))) },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.6f, 0.2f, 0.6f) * scale,
        };
        if (embers) flames.AddChild(new GpuParticles3D
        {
            Amount = 10,
            Lifetime = 2.2f,
            Preprocess = 2f,
            ProcessMaterial = ep,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = Mats.Flame() },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-6, -1, -6), new Vector3(12, 12, 12)),
        });
        return flames;
    }

    /// <summary>Colonne de fumée noire qui s'élargit et dérive avec le vent.</summary>
    public static GpuParticles3D SmokeColumn(Vector3 p, float scale, bool thick)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 8,
            InitialVelocityMin = 1.6f + scale * 0.6f,
            InitialVelocityMax = 2.6f + scale * 0.8f,
            Gravity = new Vector3(0.55f, 0.15f, 0.2f),
            DampingMin = 0.05f,
            DampingMax = 0.15f,
            ScaleMin = 0.8f,
            ScaleMax = 1.2f,
            ScaleCurve = new CurveTexture { Curve = Curve2(0.35f, 1.4f, 3.4f) },
            ColorRamp = new GradientTexture1D
            {
                Gradient = thick
                    ? Grad((0, new Color(0.05f, 0.045f, 0.04f, 0f)), (0.08f, new Color(0.07f, 0.065f, 0.06f, 0.6f)), (0.6f, new Color(0.14f, 0.135f, 0.13f, 0.38f)), (1, new Color(0.22f, 0.21f, 0.2f, 0f)))
                    : Grad((0, new Color(0.2f, 0.19f, 0.18f, 0f)), (0.15f, new Color(0.22f, 0.21f, 0.2f, 0.32f)), (1, new Color(0.35f, 0.34f, 0.33f, 0f))),
            },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.5f * scale,
            AngleMin = 0, AngleMax = 360,
            AngularVelocityMin = -8, AngularVelocityMax = 8,
        };
        var life = thick ? 11f : 8f;
        return new GpuParticles3D
        {
            Name = "Smoke",
            Position = p,
            Amount = thick ? 28 : 14,
            VisibilityRangeEnd = thick ? 700f : 260f,
            Lifetime = life,
            Preprocess = life,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(2.2f, 2.2f) * Mathf.Max(scale, 0.6f), Material = Mats.SmokePuff(false) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-20, -2, -20), new Vector3(60, 70, 50)),
        };
    }

    /// <summary>Panache lointain : énorme colonne sombre qui monte et s'étale (lueur orangée la nuit).</summary>
    public static Node3D Plume(Vector3 p, bool night)
    {
        var n = new Node3D { Name = "Plume", Position = p };
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 6,
            InitialVelocityMin = 6f,
            InitialVelocityMax = 9f,
            Gravity = new Vector3(1.2f, 0.1f, 0.5f),
            DampingMin = 0.02f,
            DampingMax = 0.05f,
            ScaleMin = 0.8f,
            ScaleMax = 1.25f,
            ScaleCurve = new CurveTexture { Curve = Curve2(0.4f, 1.3f, 3.2f) },
            ColorRamp = new GradientTexture1D
            {
                Gradient = Grad((0, new Color(0.06f, 0.055f, 0.05f, 0f)), (0.08f, new Color(0.08f, 0.075f, 0.07f, 0.7f)),
                    (0.7f, new Color(0.17f, 0.165f, 0.16f, 0.55f)), (1, new Color(0.24f, 0.23f, 0.22f, 0f))),
            },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 8f,
            AngleMin = 0, AngleMax = 360,
            AngularVelocityMin = -3, AngularVelocityMax = 3,
        };
        n.AddChild(new GpuParticles3D
        {
            Amount = 40,
            Lifetime = 34f,
            Preprocess = 34f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(46, 46), Material = Mats.SmokePuff(true) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-150, -20, -150), new Vector3(400, 420, 300)),
        });
        if (night)
        {
            // Lueur de l'incendie sous le panache.
            n.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh
                {
                    Size = new Vector2(70, 40),
                    Material = new StandardMaterial3D
                    {
                        AlbedoTexture = Mats.SmokePuff(true).AlbedoTexture,
                        AlbedoColor = new Color(1f, 0.38f, 0.1f, 0.55f),
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
                    },
                },
                Position = Vector3.Up * 10f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        return n;
    }
}
