using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Mine Impulse d'EDF 5 : posée au sol, elle attend qu'un ennemi entre dans sa distance de détection (« Search Distance »)
/// puis tire d'un coup sa gerbe de projectiles vers lui ; les modèles antiaériens (« Anti-aircraft », « Strike ») visent
/// aussi ce qui vole. Une mine sert une fois ; elle s'éteint au bout de 4 minutes.
/// </summary>
public partial class ImpulseMine : Node3D
{
    private World _world;
    private Edf5Spec _s;
    private float _dmg, _search, _life = 240f, _scan;
    private bool _aa, _local;
    private MeshInstance3D _lamp;

    public static void Spawn(World w, Vector3 at, Edf5Spec s, float dmg, float search, bool aa, bool local)
    {
        var m = new ImpulseMine { _world = w, _s = s, _dmg = dmg, _search = search, _aa = aa, _local = local };
        w.FxRoot.AddChild(m);
        m.GlobalPosition = at;
        m.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 0.18f, RadialSegments = 14 },
            MaterialOverride = Mats.Metal(new Color(0.18f, 0.2f, 0.17f), 0.5f), Position = Vector3.Up * 0.09f,
        });
        m._lamp = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f }, MaterialOverride = Mats.Glow(new Color(1f, 0.2f, 0.1f), 3f),
            Position = Vector3.Up * 0.2f,
        };
        m.AddChild(m._lamp);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _life -= dt;
        if (_life <= 0) { QueueFree(); return; }
        _lamp.Visible = Mathf.PosMod(_life, 1f) < 0.5f;
        _scan -= dt;
        if (_scan > 0) return;
        _scan = 0.2f;
        var p = GlobalPosition;
        var target = _world.EnemiesById.Values
            .Where(e => e.State != EnemyState.Dead && (_aa || !e.Def.Flying) && e.Center.DistanceTo(p) < _search + e.Def.Size)
            .OrderBy(e => e.Center.DistanceSquaredTo(p)).FirstOrDefault();
        if (target == null) return;
        var from = p + Vector3.Up * 0.3f;
        var dir = (target.Center - from).Normalized();
        var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
        var bullets = Bullets.For(_world);
        var range = _s.Range.Present ? _s.Range.Max : 75f;
        for (var i = 0; i < Mathf.Max(1, _s.Pellets); i++)
        {
            var d = Edf5Fire.Spread(dir, Mathf.DegToRad(_aa ? 4f : 9f), rng);
            bullets.Add(new Bullets.Shot
            {
                Pos = from, Vel = d * 180f, Life = range / 180f, MaxLife = range / 180f, Damage = _dmg, Far = 1f, FalloffExp = 1f,
                Local = _local, Color = new Color(1f, 0.8f, 0.4f), Length = 2.5f, Width = 0.05f,
            });
        }
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.7f, 0.3f), 0.9f);
        Sfx.At("shotgun", from, -2f, 1.2f);
        QueueFree();
    }
}
