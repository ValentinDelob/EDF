using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Modèle d'insecte géant procédural : segments du corps en lofts arrondis (InsectMesh), pattes articulées
/// (hanche, fémur, tibia + tarse) animées par cinématique inverse, démarche en trépied.
/// Les maillages sont construits une seule fois par type puis partagés entre toutes les instances.
/// </summary>
public partial class BugModel : Node3D
{
    private class Leg
    {
        public Node3D Hip, Femur, Knee;
        public float Phase, Side, Fwd, Swing, Lf, Lt, Reach, HipY, Drop, Lift;
    }

    private readonly List<Leg> _legs = new();
    private readonly List<Node3D> _wings = new();
    private readonly List<Node3D> _antennae = new();
    private Node3D _head, _gaster, _mandL, _mandR, _bodyPivot;
    private float _gait;
    private float _t;
    private float _pivotY;
    private bool _flying, _drone, _dead;
    public EnemyType Type;

    public static BugModel Create(EnemyType type)
    {
        var m = new BugModel { Type = type };
        switch (type)
        {
            case EnemyType.BlackAnt: m.BuildAnt(new Color(0.07f, 0.065f, 0.06f), 1f, 1f); break;
            case EnemyType.RedAnt: m.BuildAnt(new Color(0.45f, 0.09f, 0.05f), 1.15f, 1.05f); break;
            case EnemyType.AcidAnt: m.BuildAnt(new Color(0.42f, 0.4f, 0.08f), 1f, 1f, acid: true); break;
            case EnemyType.Scavenger: m.BuildAnt(new Color(0.48f, 0.38f, 0.27f), 0.85f, 1.35f); break;
            case EnemyType.Queen: m.BuildAnt(new Color(0.25f, 0.06f, 0.05f), 3.8f, 0.85f, queen: true); break;
            case EnemyType.Wasp: m.BuildWasp(); break;
            case EnemyType.Nest: m.BuildNest(); break;
            case EnemyType.Spider: m.BuildSpider(); break;
            case EnemyType.Drone: m.BuildDrone(); break;
            case EnemyType.Fortress: m.BuildFortress(); break;
            case EnemyType.Centipede: m.BuildCentipede(); break;
            case EnemyType.Walker: m.BuildWalker(); break;
            case EnemyType.Tripod: m.BuildTripod(); break;
            case EnemyType.DropShip: m.BuildDropShip(); break;
        }
        // Gigantisme : tout le modèle (et donc la foulée, voir Animate) suit EnemyDef.Giant.
        m.Scale *= Enemies.Get(type).Giant;
        m._baseScale = m.Scale.X;
        return m;
    }

    /// <summary>Échelle du modèle à la construction (gigantisme compris), avant variante élite et animation.</summary>
    private float _baseScale = 1f;

    // ---------------- Matériaux et cache de maillages ----------------

    private static readonly Dictionary<string, ArrayMesh> MeshCache = new();
    private static StandardMaterial3D _chitin, _eye, _wingMat, _metal, _flashMat;

    /// <summary>Chitine neutre teintée par les couleurs de sommets (un seul matériau pour tous les insectes).</summary>
    private static StandardMaterial3D Chitin()
    {
        if (_chitin != null) return _chitin;
        _chitin = (StandardMaterial3D)Mats.Chitin(new Color(0.93f, 0.93f, 0.93f)).Duplicate();
        _chitin.VertexColorUseAsAlbedo = true;
        _chitin.VertexColorIsSrgb = true;
        return _chitin;
    }

    /// <summary>Yeux composés : vernis très brillant.</summary>
    private static StandardMaterial3D Eye() => _eye ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 0.06f,
        Metallic = 0.35f,
        ClearcoatEnabled = true,
        Clearcoat = 1f,
        RimEnabled = true,
        Rim = 0.3f,
    };

    /// <summary>Membrane alaire translucide (alpha par sommet).</summary>
    private static StandardMaterial3D WingMat() => _wingMat ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = 0.12f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RimEnabled = true,
        Rim = 0.5f,
    };

    private static StandardMaterial3D Metal()
    {
        if (_metal != null) return _metal;
        _metal = (StandardMaterial3D)Mats.Metal(new Color(1f, 1f, 1f), 0.32f).Duplicate();
        _metal.VertexColorUseAsAlbedo = true;
        _metal.VertexColorIsSrgb = true;
        return _metal;
    }

    /// <summary>Pièce rigide : maillage fusionné, construit une fois par type et partagé.</summary>
    private MeshInstance3D Part(Node3D parent, string key, Action<InsectMesh> build, bool shadow = true, float range = 0f)
    {
        key = $"{Type}/{key}";
        if (!MeshCache.TryGetValue(key, out var mesh))
        {
            var b = new InsectMesh();
            build(b);
            mesh = b.Commit();
            MeshCache[key] = mesh;
        }
        var mi = new MeshInstance3D
        {
            Mesh = mesh,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        if (range > 0) mi.VisibilityRangeEnd = range;
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Nombre de triangles du modèle (diagnostic).</summary>
    public int TriangleCount()
    {
        var n = 0;
        foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = ((MeshInstance3D)node).Mesh;
            if (mesh == null) continue;
            for (var s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                var arr = mesh.SurfaceGetArrays(s);
                var idx = arr[(int)Mesh.ArrayType.Index].AsInt32Array();
                n += idx.Length > 0 ? idx.Length / 3 : arr[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
            }
        }
        return n;
    }

    /// <summary>Boîte englobante des maillages dans le repère du parent (l'ennemi) : longueur, envergure, hauteur (diagnostic).</summary>
    public Aabb Bounds()
    {
        var inv = GetParent<Node3D>().GlobalTransform.AffineInverse();
        Aabb? box = null;
        foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)node;
            if (mi.Mesh == null || !mi.IsVisibleInTree()) continue;
            var b = (inv * mi.GlobalTransform) * mi.GetAabb();
            box = box?.Merge(b) ?? b;
        }
        return box ?? new Aabb();
    }

    public override void _Ready()
    {
        // Variante élite : chitine dorée (Enemy ne trouve plus de SphereMesh à recolorer).
        if (IsMachine)
        {
            if (GetParent() is Enemy { Elite: true }) ApplyMachineElite();
            return;
        }
        if (GetParent() is Enemy { Elite: true })
        {
            var gold = Mats.Chitin(new Color(0.78f, 0.58f, 0.14f));
            var chitin = Chitin();
            foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)node;
                if (mi.Mesh == null) continue;
                for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                    if (mi.Mesh.SurfaceGetMaterial(s) == chitin) mi.SetSurfaceOverrideMaterial(s, gold);
            }
        }
    }

    // ---------------- Outils de forme ----------------

    /// <summary>Loft d'un segment du corps le long de Z (clés : largeur, dessus, dessous ; t de z0 à z1).</summary>
    private static void BodyLoft(InsectMesh m, float z0, float z1, float[] width, float[] top, float[] bottom, IList<float> ts, int radial, Color col,
        float sq = 2.3f, Func<float, float> mod = null, Func<float, float, Vector3, Color, Color> paint = null, float xOff = 0f)
    {
        var rings = new InsectMesh.Ring[ts.Count];
        for (var i = 0; i < ts.Count; i++)
        {
            var t = ts[i];
            var w = InsectMesh.Spline(width, t);
            var tp = InsectMesh.Spline(top, t);
            var bt = InsectMesh.Spline(bottom, t);
            var k = mod?.Invoke(t) ?? 1f;
            var end = i == 0 || i == ts.Count - 1;
            rings[i] = new InsectMesh.Ring(new Vector3(xOff, (tp + bt) / 2, Mathf.Lerp(z0, z1, t)), Vector3.Right, Vector3.Up,
                end ? 0 : Mathf.Max(w, 0.001f) * k, end ? 0 : Mathf.Max((tp - bt) / 2, 0.001f) * k, col);
        }
        Func<int, float, Vector3, Color, Color> p = null;
        if (paint != null) p = (i, th, pos, c) => paint(ts[i], th, pos, c);
        m.Loft(rings, radial, sq, 0f, p);
    }

    private static List<float> Steps(int n, params float[] extra)
    {
        var l = new List<float>();
        for (var i = 0; i <= n; i++) l.Add(i / (float)n);
        foreach (var e in extra) l.Add(e);
        l.Sort();
        return l;
    }

    /// <summary>Cinématique inverse à deux os dans le plan de la patte (genou vers le haut).</summary>
    private static void Ik(float lf, float lt, float x, float y, out float e, out float k)
    {
        var d = Mathf.Clamp(Mathf.Sqrt(x * x + y * y), Mathf.Abs(lf - lt) + 0.02f, lf + lt - 0.02f);
        var phi = Mathf.Atan2(y, x);
        var a = Mathf.Acos(Mathf.Clamp((lf * lf + d * d - lt * lt) / (2 * lf * d), -1, 1));
        var b = Mathf.Acos(Mathf.Clamp((lf * lf + lt * lt - d * d) / (2 * lf * lt), -1, 1));
        e = phi + a;
        k = -(Mathf.Pi - b);
    }

    /// <summary>
    /// Patte articulée : hanche (lacet) → fémur (élévation) → genou (flexion) → tibia + tarse.
    /// Les maillages sont construits pour le côté droit (+X) et mis en miroir pour le côté gauche.
    /// </summary>
    private Leg AddLeg(string key, Vector3 hipPos, float side, float fwdDeg, float lf, float lt, float ls, float reach, float gammaDeg,
        Action<InsectMesh, float> femur, Action<InsectMesh, float, float> tibia, float swing = 18f, float lift = 0.35f)
    {
        var hipY = hipPos.Y;
        var gamma = Mathf.DegToRad(gammaDeg);
        var drop = ls * Mathf.Sin(gamma);
        Ik(lf, lt, reach, -(_pivotY + hipY) + drop, out var e0, out var k0);
        var tau = -gamma - (e0 + k0);
        var sideKey = side > 0 ? "R" : "L";
        var hip = new Node3D { Position = hipPos, Rotation = new Vector3(0, -Mathf.DegToRad(fwdDeg) * side, 0) };
        _bodyPivot.AddChild(hip);
        var fem = new Node3D();
        hip.AddChild(fem);
        Part(fem, $"{key}F{sideKey}", m => { m.Mirror = side < 0; femur(m, lf); }, false, 90f);
        var knee = new Node3D { Position = new Vector3(lf * side, 0, 0) };
        fem.AddChild(knee);
        Part(knee, $"{key}T{sideKey}", m => { m.Mirror = side < 0; tibia(m, lt, tau); }, false, 90f);
        var leg = new Leg
        {
            Hip = hip, Femur = fem, Knee = knee, Side = side, Fwd = fwdDeg, Swing = swing, Lf = lf, Lt = lt,
            Reach = reach, HipY = hipY, Drop = drop, Lift = lift,
        };
        _legs.Add(leg);
        PoseLeg(leg, 0f, 0f);
        return leg;
    }

    private void PoseLeg(Leg l, float fa, float lift)
    {
        l.Hip.Rotation = new Vector3(0, -Mathf.DegToRad(l.Fwd + fa * l.Swing) * l.Side, 0);
        var h = (_bodyPivot?.Position.Y ?? _pivotY) + l.HipY;
        Ik(l.Lf, l.Lt, l.Reach * (1f - lift * 0.08f), -h + l.Drop + lift * l.Lift, out var e, out var k);
        l.Femur.Rotation = new Vector3(0, 0, e * l.Side);
        l.Knee.Rotation = new Vector3(0, 0, k * l.Side);
    }

    // ---------------- Retour visuel, animation, mort ----------------

    private List<GeometryInstance3D> _meshes;
    private float _flashT = -1f;

    /// <summary>Bref flash blanc à l'impact (retour visuel des tirs).</summary>
    public void Flash()
    {
        if (Type is EnemyType.Queen or EnemyType.Nest || IsMachine) return; // cibles touchées en continu : pas de flash
        _flashMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.55f, 0.3f, 0.16f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        if (_meshes == null)
        {
            _meshes = new List<GeometryInstance3D>();
            foreach (var n in FindChildren("*", "MeshInstance3D", true, false)) _meshes.Add((GeometryInstance3D)n);
        }
        // Pas plus d'un flash toutes les 0,2 s : sous un feu continu, l'insecte ne doit pas rester blanc.
        if (_flashT > -0.2f) return;
        foreach (var m in _meshes) m.MaterialOverlay = _flashMat;
        _flashT = 0.05f;
    }

    /// <summary>Transparence de tout le modèle (0 = opaque) : insecte géant traversé par la caméra.</summary>
    public void SetFade(float transparency)
    {
        if (_meshes == null)
        {
            _meshes = new List<GeometryInstance3D>();
            foreach (var n in FindChildren("*", "MeshInstance3D", true, false)) _meshes.Add((GeometryInstance3D)n);
        }
        foreach (var m in _meshes) m.Transparency = transparency;
    }

    /// <summary>Anime la démarche (vitesse en m/s), l'attaque (0..1) et le vol.</summary>
    public void Animate(float dt, float speed, float attack)
    {
        _t += dt;
        if (_flashT > -1f)
        {
            var was = _flashT;
            _flashT -= dt;
            if (was > 0 && _flashT <= 0 && _meshes != null) foreach (var m in _meshes) m.MaterialOverlay = null;
        }
        if (_dead) return;
        if (Type == EnemyType.Centipede) { AnimateCentipede(dt, speed, attack); return; }
        if (Type == EnemyType.Nest)
        {
            Scale = Vector3.One * _baseScale * (1f + Mathf.Sin(_t * 1.5f) * 0.015f);
            return;
        }
        if (_drone)
        {
            foreach (var w in _wings) w.Rotation = new Vector3(0, _t * 40f, 0);
            if (_bodyPivot != null) _bodyPivot.Rotation = new Vector3(Mathf.Sin(_t * 1.3f) * 0.08f + attack * 0.25f, 0, Mathf.Sin(_t * 0.9f) * 0.08f);
            return;
        }
        if (_flying)
        {
            foreach (var w in _wings)
                w.RotationDegrees = new Vector3(0, 0, (Mathf.Sin(_t * 70f) * 38f + 8f) * Mathf.Sign(w.Position.X));
            if (_bodyPivot != null)
            {
                _bodyPivot.Position = new Vector3(0, Mathf.Sin(_t * 3f) * 0.25f, 0);
                _bodyPivot.Rotation = new Vector3(Mathf.Sin(_t * 1.7f) * 0.05f, 0, Mathf.Sin(_t * 1.1f) * 0.06f);
            }
            if (_gaster != null) _gaster.RotationDegrees = new Vector3(-20 - attack * 50f + Mathf.Sin(_t * 5f) * 3f, 0, 0);
            if (_head != null) _head.RotationDegrees = new Vector3(Mathf.Sin(_t * 2.3f) * 4f, Mathf.Sin(_t * 1.3f) * 8f, 0);
            return;
        }
        // Foulée proportionnelle à la taille : un grand insecte fait des pas plus longs, plus lents (pas de pieds qui glissent).
        var sc = Mathf.Max(Scale.X, 0.5f);
        var amp = Mathf.Clamp(speed / (4f * sc), 0f, 1f);
        _gait += dt * speed * 2.4f / sc;
        if (_bodyPivot != null)
        {
            // Léger tangage à chaque appui, roulis alterné, bond vers l'avant pendant la morsure.
            var lunge = Mathf.Sin(Mathf.Clamp(attack, 0, 1) * Mathf.Pi);
            var bob = (1f - Mathf.Abs(Mathf.Sin(_gait))) * 0.06f * amp;
            _bodyPivot.Position = new Vector3(0, _pivotY + bob + lunge * 0.15f, lunge * 0.6f);
            _bodyPivot.Rotation = new Vector3(lunge * 0.18f + Mathf.Sin(_gait * 2f) * 0.015f * amp, 0, Mathf.Sin(_gait) * 0.03f * amp);
            if (GrabT >= 0) GrabBody();
        }
        foreach (var l in _legs)
        {
            var u = Mathf.PosMod(_gait + l.Phase, Mathf.Tau) / Mathf.Tau;
            float fa, lift = 0f;
            if (u < 0.5f) fa = 1f - 4f * u; // appui : le pied glisse vers l'arrière
            else
            {
                var w = (u - 0.5f) * 2f; // transfert : le pied se lève et revient vers l'avant
                fa = -1f + 2f * w * w * (3f - 2f * w);
                lift = Mathf.Sin(w * Mathf.Pi);
            }
            PoseLeg(l, fa * amp, lift * amp);
        }
        if (_head != null) _head.RotationDegrees = new Vector3(-attack * 25f + Mathf.Sin(_t * 2f) * 3f, Mathf.Sin(_t * 1.3f) * 6f, 0);
        for (var i = 0; i < _antennae.Count; i++)
        {
            var s = i == 0 ? 1f : -1f;
            _antennae[i].Rotation = new Vector3(Mathf.Sin(_t * 2.1f + i * 1.7f) * 0.15f + attack * 0.35f, Mathf.Sin(_t * 1.4f + i) * 0.18f * s, Mathf.Sin(_t * 1.7f + i * 2.3f) * 0.08f);
        }
        if (_mandL != null)
        {
            // Claquements répétés pendant l'attaque.
            var open = 6f + Mathf.Sin(_t * 3f) * 3f + attack * 40f * Mathf.Abs(Mathf.Sin(_t * 9f));
            _mandL.RotationDegrees = new Vector3(0, open, 0);
            _mandR.RotationDegrees = new Vector3(0, -open, 0);
        }
        if (GrabT >= 0) GrabHead();
        if (_gaster != null)
        {
            if (GrabT >= 0) _gaster.RotationDegrees = new Vector3(-12f * GrabBlend + Mathf.Sin(_t * 7f) * 4f, Mathf.Sin(_t * 5.3f) * 5f * GrabBlend, 0);
            else if (Type == EnemyType.AcidAnt)
            {
                // Le gastre se replie sous le corps et pompe l'acide.
                _gaster.RotationDegrees = new Vector3(-attack * 70f, 0, 0);
                var pump = 1f + attack * 0.07f * Mathf.Sin(_t * 16f);
                _gaster.Scale = new Vector3(pump, pump, 1f + (pump - 1f) * 0.5f);
            }
            else if (Type == EnemyType.Spider) _gaster.RotationDegrees = new Vector3(Mathf.Sin(_t * 1.2f) * 2f, Mathf.Sin(_t * 0.8f) * 2f, 0);
            else _gaster.RotationDegrees = new Vector3(Mathf.Sin(_t * 1.7f) * 3f + Mathf.Sin(_gait * 2f) * 2f * amp, 0, 0);
        }
    }

    /// <summary>Pose de mort : retourné, pattes repliées.</summary>
    public void DeathPose()
    {
        _dead = true;
        if (Type == EnemyType.Centipede) { CentipedeDeathPose(); return; }
        foreach (var l in _legs)
        {
            l.Hip.Rotation = new Vector3(0, -Mathf.DegToRad(l.Fwd * 0.6f) * l.Side, 0);
            l.Femur.Rotation = new Vector3(0, 0, -0.5f * l.Side);
            l.Knee.Rotation = new Vector3(0, 0, -2.1f * l.Side);
        }
        if (_bodyPivot != null)
        {
            _bodyPivot.RotationDegrees = new Vector3(0, 0, 180);
            // Le corps retourné repose sur le dos, pattes en l'air.
            if (!_flying) _bodyPivot.Position = new Vector3(0, _pivotY * 0.5f, 0);
        }
        foreach (var w in _wings) w.RotationDegrees = _drone ? Vector3.Zero : new Vector3(0, 0, 25f * Mathf.Sign(w.Position.X));
        if (_mandL != null) { _mandL.RotationDegrees = new Vector3(0, 30, 0); _mandR.RotationDegrees = new Vector3(0, -30, 0); }
    }
}
