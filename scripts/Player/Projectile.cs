using Godot;

namespace Rempart;

public enum ProjectileKind { Rocket = 0, Plasma = 1, Grenade = 2, Missile = 3, Shell = 4, Acid = 10, Stinger = 11, AcidBig = 12, Web = 13, Laser = 14, Bomb = 15, Bolt = 16, HeavyBolt = 17 }

/// <summary>
/// Projectile simulé localement. S'il appartient au joueur local (ou à l'hôte pour les ennemis),
/// son impact déclenche les dégâts ; sinon il est purement visuel.
/// </summary>
public partial class Projectile : Node3D
{
    public ProjectileKind Kind;
    public Vector3 Velocity;
    public float Gravity;
    public float Life = 6f;
    public bool Authoritative;
    public float Damage;
    public float Radius;
    public Enemy SourceEnemy;
    private World _world;
    private GpuParticles3D _trail;
    private Node3D _homingTarget;
    /// <summary>Corps ignorés par le rayon de collision (véhicule tireur : la roquette ne lui explose jamais dessus).</summary>
    private Godot.Collections.Array<Rid> _exclude;

    /// <summary>Appelé à l'impact (position, objet touché ou null) — tests automatisés.</summary>
    public event System.Action<Vector3, GodotObject> Impacted;

    /// <summary>Le projectile traverse ce corps (le véhicule qui l'a tiré).</summary>
    public void IgnoreBody(CollisionObject3D body)
    {
        if (body == null) return;
        _exclude ??= new Godot.Collections.Array<Rid>();
        _exclude.Add(body.GetRid());
    }

    /// <summary>Gravité des missiles en cloche de la forteresse (trajectoire calculée par le tireur).</summary>
    public const float BombGravity = 16f;

    private static StandardMaterial3D _boltHalo;

    public static Projectile Spawn(World world, ProjectileKind kind, Vector3 from, Vector3 velocity, bool authoritative, float damage, float radius)
    {
        var p = new Projectile { Kind = kind, Velocity = velocity, Authoritative = authoritative, Damage = damage, Radius = radius, _world = world };
        var enemyShot = (int)kind >= 10;
        p.Gravity = kind switch
        {
            ProjectileKind.Grenade => 18f,
            ProjectileKind.Acid or ProjectileKind.AcidBig or ProjectileKind.Web => 9f,
            ProjectileKind.Shell => 2f,
            ProjectileKind.Bomb => BombGravity,
            _ => 0f,
        };
        Color color;
        float size;
        switch (kind)
        {
            case ProjectileKind.Rocket: case ProjectileKind.Missile: case ProjectileKind.Shell: color = new Color(1f, 0.7f, 0.3f); size = 0.18f; break;
            case ProjectileKind.Plasma: color = new Color(0.4f, 0.75f, 1f); size = 0.6f; break;
            case ProjectileKind.Grenade: color = new Color(0.25f, 0.3f, 0.2f); size = 0.14f; break;
            case ProjectileKind.Stinger: color = new Color(1f, 0.85f, 0.4f); size = 0.12f; break;
            case ProjectileKind.AcidBig: color = new Color(0.7f, 0.95f, 0.15f); size = 0.9f; break;
            case ProjectileKind.Web: color = new Color(0.92f, 0.92f, 0.88f); size = 0.5f; break;
            case ProjectileKind.Laser: color = new Color(1f, 0.15f, 0.1f); size = 0.12f; break;
            case ProjectileKind.Bomb: color = new Color(1f, 0.3f, 0.12f); size = 0.35f; break;
            // Traits d’énergie des aliens : violet pâle, longs et vifs (bien visibles en rafale).
            case ProjectileKind.Bolt: color = new Color(0.92f, 0.8f, 1f); size = 0.1f; break;
            case ProjectileKind.HeavyBolt: color = new Color(0.88f, 0.7f, 1f); size = 0.26f; break;
            default: color = new Color(0.7f, 0.95f, 0.15f); size = 0.45f; break;
        }
        var glow = kind != ProjectileKind.Grenade;
        p.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = size, Height = size * 2, RadialSegments = 10, Rings = 5 },
            MaterialOverride = glow ? Mats.Glow(color, kind is ProjectileKind.Bolt or ProjectileKind.HeavyBolt ? 6f : enemyShot ? 2.5f : 5f) : Mats.Metal(color),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = kind is ProjectileKind.Rocket or ProjectileKind.Missile or ProjectileKind.Shell or ProjectileKind.Stinger or ProjectileKind.Bomb ? new Vector3(1, 1, 4)
                : kind == ProjectileKind.Laser ? new Vector3(1, 1, 10) : kind == ProjectileKind.Bolt ? new Vector3(1, 1, 16)
                : kind == ProjectileKind.HeavyBolt ? new Vector3(1, 1, 5) : Vector3.One,
        });
        if (kind is ProjectileKind.Bolt or ProjectileKind.HeavyBolt)
        {
            // Halo violet additif autour du cœur blanc des traits d'énergie des aliens.
            _boltHalo ??= new StandardMaterial3D
            {
                AlbedoColor = new Color(BugModel.AlienEnergy, 0.45f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            };
            p.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = size * 2.6f, Height = size * 5.2f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = _boltHalo,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Scale = kind == ProjectileKind.Bolt ? new Vector3(1, 1, 7) : new Vector3(1, 1, 3),
            });
        }
        if (kind is ProjectileKind.Rocket or ProjectileKind.Missile or ProjectileKind.Plasma or ProjectileKind.Shell or ProjectileKind.Bomb)
        {
            p.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 1.5f, OmniRange = 6f });
            p._trail = MakeTrail(kind == ProjectileKind.Plasma ? new Color(0.5f, 0.75f, 1f, 0.6f) : new Color(0.6f, 0.6f, 0.6f, 0.55f), kind == ProjectileKind.Plasma);
            p.AddChild(p._trail);
        }
        world.FxRoot.AddChild(p);
        p.GlobalPosition = from;
        if (velocity.LengthSquared() > 0.01f) p.LookAt(from + velocity, Vector3.Up);
        return p;
    }

    public void SetHoming(Node3D target) => _homingTarget = target;

    private static GpuParticles3D MakeTrail(Color c, bool additive)
    {
        var g = new Gradient();
        g.SetColor(0, c);
        g.SetColor(1, new Color(c, 0));
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 180,
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 0.8f,
            Gravity = new Vector3(0, 0.6f, 0),
            ScaleCurve = new CurveTexture { Curve = new Curve() },
            ColorRamp = new GradientTexture1D { Gradient = g },
        };
        var curve = ((CurveTexture)pm.ScaleCurve).Curve;
        curve.MaxValue = 3;
        curve.AddPoint(new Vector2(0, 0.6f));
        curve.AddPoint(new Vector2(1, 3f));
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) } },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0), Width = 32, Height = 32,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        return new GpuParticles3D
        {
            Amount = 60,
            Lifetime = 1.2f,
            LocalCoords = false,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.5f, 0.5f), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-200, -50, -200), new Vector3(400, 100, 400)),
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        Life -= dt;
        if (Life <= 0) { Impact(GlobalPosition, Vector3.Up, null); return; }
        if (_homingTarget != null && IsInstanceValid(_homingTarget))
        {
            var to = (_homingTarget.GlobalPosition + Vector3.Up - GlobalPosition).Normalized();
            Velocity = Velocity.Lerp(to * Velocity.Length(), Mathf.Clamp(dt * 3f, 0, 1));
        }
        Velocity += Vector3.Down * Gravity * dt;
        var from = GlobalPosition;
        var to2 = from + Velocity * dt;
        var enemyShot = (int)Kind >= 10;
        // Les tirs ennemis touchent le décor et les joueurs ; ceux des joueurs le décor et les ennemis.
        uint mask = enemyShot ? 1u : (1u | (1u << 2));
        var q = PhysicsRayQueryParameters3D.Create(from, to2, mask);
        if (_exclude != null) q.Exclude = _exclude;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count > 0)
        {
            Impact((Vector3)hit["position"], (Vector3)hit["normal"], hit["collider"].AsGodotObject());
            return;
        }
        if (enemyShot && Authoritative && _world.HostProjectileHitsPlayer(this, from, to2)) { Impact(to2, Vector3.Up, null); return; }
        if (to2.Y < -1f) { Impact(new Vector3(to2.X, 0, to2.Z), Vector3.Up, null); return; }
        GlobalPosition = to2;
        if (Velocity.LengthSquared() > 0.01f && Kind != ProjectileKind.Grenade) LookAt(to2 + Velocity, Vector3.Up);
    }

    private bool _done;

    private void Impact(Vector3 pos, Vector3 normal, GodotObject collider)
    {
        if (_done) return;
        _done = true;
        Impacted?.Invoke(pos, collider);
        var enemyShot = (int)Kind >= 10;
        if (enemyShot)
        {
            if (Kind is ProjectileKind.Laser or ProjectileKind.Bolt) Effects.Sparks(_world, pos, normal);
            else if (Kind == ProjectileKind.HeavyBolt) Effects.Explosion(_world, pos + normal * 0.3f, 2.4f, ExplosionFx.Plasma);
            else if (Kind == ProjectileKind.Bomb) Effects.Explosion(_world, pos + normal * 0.3f, Radius * 0.8f, 0);
            else Effects.Goo(_world, pos, Kind == ProjectileKind.Web ? new Color(0.9f, 0.9f, 0.85f) : new Color(0.7f, 0.95f, 0.15f), Kind == ProjectileKind.AcidBig ? 2f : 0.8f, 8);
            // Gros jet d'acide : éclaboussure et gaz toxique (effet seul, sans feu).
            if (Kind == ProjectileKind.AcidBig) Effects.Explosion(_world, pos + normal * 0.2f, 3.5f, ExplosionFx.Acid);
            if (Authoritative) _world.HostEnemyProjectileImpact(this, pos);
        }
        else if (Authoritative)
        {
            Net.I.SendExplosion(pos + normal * 0.3f, Radius, Damage, Kind == ProjectileKind.Plasma ? 2 : 0);
        }
        if (_trail != null)
        {
            // Laisse la traînée se dissiper.
            _trail.Emitting = false;
            var t = _trail;
            RemoveChild(t);
            _world.FxRoot.AddChild(t);
            t.GlobalPosition = pos;
            var timer = _world.GetTree().CreateTimer(1.5f);
            timer.Timeout += () => { if (IsInstanceValid(t)) t.QueueFree(); };
        }
        QueueFree();
    }
}
