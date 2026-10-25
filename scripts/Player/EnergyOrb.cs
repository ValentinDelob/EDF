using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Sphère d'énergie des armes spéciales de la Wing Diver d'EDF 5 : lancée devant soi, elle flotte puis frappe seule pendant
/// <see cref="Duration"/> secondes. Le motif suit la famille : Diffuser = pluie d'impulsions explosives, Gleipnir = projectiles
/// guidés sur les ennemis proches, Thunder Cluster / Orb = éclairs qui ricochent, Heaven's Gate = rideau de lasers vers le sol,
/// Psi Cluster / Blast Wall = lasers en cône vers l'avant, Final Days = bombardement de plasma. « Ammo » = nombre total de
/// coups (cadence plafonnée, dégâts ajustés pour garder le total). Dégâts seulement chez le tireur.
/// </summary>
public partial class EnergyOrb : Node3D
{
    public const float Duration = 10f;
    private const float MaxRate = 50f;

    private World _world;
    private Edf5Spec _s;
    private Vector3 _fwd;
    private float _dmg, _rate, _acc, _left = Duration, _rise = 0.8f;
    private bool _local;
    private RandomNumberGenerator _rng;
    private MeshInstance3D _core;

    public static void Spawn(World w, Edf5Spec s, float dmg, Vector3 from, Vector3 dir, bool local)
    {
        var shots = s.Ammo.Present ? Mathf.Max(s.Ammo.Max, 1f) : 60f;
        var rate = Mathf.Min(shots / Duration, MaxRate);
        var o = new EnergyOrb
        {
            _world = w, _s = s, _local = local, _rate = rate, _dmg = dmg * shots / (rate * Duration),
            _fwd = (dir with { Y = 0 }).LengthSquared() > 0.01f ? (dir with { Y = 0 }).Normalized() : Vector3.Forward,
            _rng = new RandomNumberGenerator { Seed = (ulong)(from.X * 1000 + from.Z * 7) },
        };
        w.FxRoot.AddChild(o);
        o.GlobalPosition = from + o._fwd * 2f;
        var c = s.Family switch { "gleipnir" => new Color(0.6f, 0.9f, 1f), "thundercluster" => new Color(0.8f, 0.7f, 1f), "heavensgate" or "psicluster" => new Color(1f, 0.5f, 0.35f), _ => new Color(0.35f, 1f, 0.95f) };
        o._core = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = Mats.Glow(c, 4f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        o.AddChild(o._core);
        o.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 3f, OmniRange = 14f });
        Sfx.At("plasma", from, -2f, 0.7f);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _left -= dt;
        // Vol d'approche : la sphère avance et monte un peu, puis reste en place.
        if (_rise > 0)
        {
            _rise -= dt;
            GlobalPosition += (_fwd * 14f + Vector3.Up * 6f) * dt;
        }
        _core.Scale = Vector3.One * (1f + 0.12f * Mathf.Sin(_left * 12f));
        if (_left <= 0) { QueueFree(); return; }
        _acc += _rate * dt;
        while (_acc >= 1f)
        {
            _acc -= 1f;
            Emit();
        }
    }

    private void Emit()
    {
        var p = GlobalPosition;
        var bullets = Bullets.For(_world);
        void Shot(Vector3 dir, float speed, float range, Color c, float blast = 0f, int bounces = 0, float width = 0.1f, float len = 1.5f) =>
            bullets.Add(new Bullets.Shot
            {
                Pos = p, Vel = dir.Normalized() * speed, Life = range / speed, MaxLife = range / speed, Damage = _dmg, Far = 1f, FalloffExp = 1f,
                Blast = blast, Bounces = bounces, Local = _local, Color = c, Length = len, Width = width,
            });
        Vector3 Rnd() => new Vector3(_rng.Randf() * 2 - 1, _rng.Randf() * 2 - 1, _rng.Randf() * 2 - 1);
        switch (_s.Family)
        {
            case "gleipnir":
            {
                var target = _world.EnemiesById.Values.Where(e => e.State != EnemyState.Dead && e.Center.DistanceTo(p) < 120f)
                    .OrderBy(_ => _rng.Randf()).FirstOrDefault();
                var pr = Projectile.Spawn(_world, ProjectileKind.Energy, p, (Rnd() + Vector3.Up).Normalized() * 60f, _local, _dmg, 1.2f);
                pr.Life = 4f;
                if (target != null) pr.SetHoming(target, 6f);
                break;
            }
            case "thundercluster":
                Shot((Rnd() with { Y = -Mathf.Abs(Rnd().Y) - 0.2f }), 260f, 60f, new Color(0.8f, 0.7f, 1f), 0f, 3, 0.06f, 3f);
                break;
            case "heavensgate":
            {
                // Rideau vertical : points alignés en travers de la direction du lancer, lasers vers le sol.
                var side = _fwd.Cross(Vector3.Up);
                var o = p + side * ((_rng.Randf() * 2 - 1) * 12f);
                bullets.Add(new Bullets.Shot
                {
                    Pos = o, Vel = Vector3.Down * 900f, Life = 0.12f, MaxLife = 0.12f, Damage = _dmg, Far = 1f, FalloffExp = 1f,
                    Pierce = true, Local = _local, Color = new Color(1f, 0.5f, 0.35f), Length = 30f, Width = 0.12f,
                });
                break;
            }
            case "psicluster":
                Shot((_fwd + Vector3.Down * 0.35f + Rnd() * 0.35f), 700f, 120f, new Color(1f, 0.5f, 0.35f), 0f, 0, 0.07f, 12f);
                break;
            case "finaldays":
            {
                var pr = Projectile.Spawn(_world, ProjectileKind.Plasma, p, (Rnd() * 0.6f + Vector3.Up).Normalized() * 30f, _local, _dmg, 8f);
                pr.Gravity = 14f;
                pr.Life = 8f;
                break;
            }
            default: // diffuser : impulsions explosives vers le bas, en gerbe
                Shot((Rnd() * 0.8f + Vector3.Down), 90f, 120f, new Color(0.35f, 1f, 0.95f), 3f, 0, 0.2f, 1f);
                break;
        }
    }
}
