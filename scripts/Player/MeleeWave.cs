using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Coups de mêlée du Fencer : onde de choc en éventail (marteaux, haken, rouleaux), lame d'énergie qui vole (Force Blade,
/// Dynamo Blade, Force Ax, dispersions) et pointe de lance (Blasthole Spear, Jackhammer). Le tireur seul inflige les dégâts
/// (regroupés par ennemi, <see cref="FencerFire.Apply"/>) ; les autres pairs voient le même effet.
/// </summary>
public partial class MeleeWave : Node3D
{
    private enum Mode { Arc, Blade, Spike }

    private Mode _mode;
    private World _world;
    private MeshInstance3D _mesh;
    private StandardMaterial3D _mat;
    private Color _color;
    private float _life, _maxLife;
    // Lame : vitesse, rayon de coupe, dégâts, ennemis déjà touchés.
    private Vector3 _vel;
    private float _radius, _damage;
    private bool _local;
    private readonly HashSet<Enemy> _hit = new();

    public static readonly Color Steel = new(0.75f, 0.92f, 1f), Hot = new(1f, 0.72f, 0.38f);

    private static StandardMaterial3D Material(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    /// <summary>Croissant plat (plan XZ, ouvert vers −Z) : demi-angle, rayon intérieur relatif (rayon extérieur 1).</summary>
    private static ArrayMesh Crescent(float halfAngle, float inner, int seg = 24)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < seg; i++)
        {
            var a0 = Mathf.Lerp(-halfAngle, halfAngle, i / (float)seg);
            var a1 = Mathf.Lerp(-halfAngle, halfAngle, (i + 1) / (float)seg);
            // Lame plus épaisse au centre, effilée aux pointes.
            float Th(float a) => Mathf.Lerp(1f, inner, 0.25f + 0.75f * Mathf.Cos(a / halfAngle * Mathf.Pi * 0.5f));
            Vector3 P(float a, float r) => new(Mathf.Sin(a) * r, 0, -Mathf.Cos(a) * r);
            var o0 = P(a0, 1f); var o1 = P(a1, 1f);
            var n0 = P(a0, Th(a0)); var n1 = P(a1, Th(a1));
            st.AddVertex(n0); st.AddVertex(o0); st.AddVertex(o1);
            st.AddVertex(n0); st.AddVertex(o1); st.AddVertex(n1);
        }
        return st.Commit();
    }

    private static MeleeWave Make(World world, Mode mode, Vector3 at, Vector3 dir, Color c, float life)
    {
        var w = new MeleeWave { _world = world, _mode = mode, _color = c, _life = life, _maxLife = life };
        w._mat = Material(c);
        world.FxRoot.AddChild(w);
        w.GlobalPosition = at;
        var up = Mathf.Abs(dir.Y) > 0.98f ? Vector3.Right : Vector3.Up;
        w.LookAt(at + dir, up);
        return w;
    }

    /// <summary>Onde de choc : un croissant qui s'élargit jusqu'à <paramref name="reach"/> (effet seul, dégâts déjà appliqués).</summary>
    public static void Arc(World world, Vector3 at, Vector3 dir, float reach, float halfAngle, Color c)
    {
        var w = Make(world, Mode.Arc, at, dir, c, 0.32f);
        w._radius = reach;
        w._mesh = new MeshInstance3D { Mesh = Crescent(halfAngle, 0.8f), MaterialOverride = w._mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        w._mesh.Scale = Vector3.One * 1f;
        w.AddChild(w._mesh);
        // Second croissant plus pâle juste derrière : l'onde a de l'épaisseur.
        w._mesh.AddChild(new MeshInstance3D
        {
            Mesh = Crescent(halfAngle * 0.96f, 0.9f), MaterialOverride = Material(new Color(1f, 1f, 1f, 0.6f)),
            Position = new Vector3(0, 0.06f, 0), Scale = Vector3.One * 0.93f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Lame d'énergie volante : perce tous les ennemis sur sa route (dégâts du tireur seulement).</summary>
    public static void Blade(World world, Vector3 at, Vector3 dir, float speed, float range, float width, float roll, float damage, bool local, Color c)
    {
        var w = Make(world, Mode.Blade, at, dir, c, range / Mathf.Max(speed, 1f));
        w._vel = dir * speed;
        w._radius = width;
        w._damage = damage;
        w._local = local;
        w._mesh = new MeshInstance3D
        {
            Mesh = Crescent(Mathf.DegToRad(62f), 0.72f), MaterialOverride = w._mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = Vector3.One * width, Rotation = new Vector3(0, 0, roll),
        };
        w.AddChild(w._mesh);
        w._mesh.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 1.2f, OmniRange = width * 2.5f });
    }

    /// <summary>Pointe de lance : jaillit jusqu'à <paramref name="reach"/> puis se rétracte (effet seul).</summary>
    public static void Spike(World world, Vector3 at, Vector3 dir, float reach, float radius, Color c)
    {
        var w = Make(world, Mode.Spike, at, dir, c, 0.22f);
        w._radius = reach;
        var cone = new CylinderMesh { TopRadius = 0f, BottomRadius = radius, Height = 1f, RadialSegments = 10, Rings = 1 };
        var pivot = new Node3D();
        w.AddChild(pivot);
        // Cône couché le long de −Z, base au départ.
        w._mesh = new MeshInstance3D { Mesh = cone, MaterialOverride = w._mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0), Position = new Vector3(0, 0, -0.5f) };
        pivot.AddChild(w._mesh);
        pivot.Scale = new Vector3(1, 1, 0.01f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _life -= dt;
        var t = 1f - Mathf.Clamp(_life / _maxLife, 0, 1);
        switch (_mode)
        {
            case Mode.Arc:
                // Expansion rapide (racine) et fondu.
                _mesh.Scale = Vector3.One * Mathf.Max(0.5f, _radius * Mathf.Sqrt(t));
                _mat.AlbedoColor = new Color(_color, (1f - t) * 0.9f);
                break;
            case Mode.Spike:
            {
                var ext = t < 0.3f ? t / 0.3f : t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                ((Node3D)GetChild(0)).Scale = new Vector3(1, 1, Mathf.Max(0.01f, ext * _radius));
                _mat.AlbedoColor = new Color(_color, 0.95f * (t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f));
                break;
            }
            case Mode.Blade:
                _mat.AlbedoColor = new Color(_color, 0.85f * Mathf.Min(1f, _life / 0.15f));
                break;
        }
        if (_life <= 0) QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_mode != Mode.Blade || _life <= 0) return;
        var dt = (float)delta;
        var from = GlobalPosition;
        var to = from + _vel * dt;
        // Le sol arrête la lame (poussière) ; les ennemis non.
        var ground = World.GroundAt(to.X, to.Z);
        if (to.Y < ground - 0.5f)
        {
            Effects.Dust(_world, new Vector3(to.X, ground, to.Z), new Vector3(_radius, 0.5f, _radius));
            _life = Mathf.Min(_life, 0.12f);
            _vel = Vector3.Zero;
        }
        if (_local)
        {
            Dictionary<Enemy, (float dmg, Vector3 at, bool weak)> per = null;
            foreach (var e in _world.EnemiesById.Values)
            {
                if (e.State == EnemyState.Dead || _hit.Contains(e)) continue;
                var c = Geometry3D.GetClosestPointToSegment(e.Center, from, to);
                if (e.BodyDistance(c) > _radius * 0.8f + e.Def.Size * 1.1f) continue;
                _hit.Add(e);
                per ??= new();
                per[e] = (_damage, c, false);
            }
            if (per != null) FencerFire.Apply(_world, per);
        }
        GlobalPosition = to;
    }
}
