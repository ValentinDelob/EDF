using Godot;

namespace Rempart;

public enum ProjectileKind { Rocket = 0, Plasma = 1, Grenade = 2, Missile = 3, Shell = 4, Energy = 5, Acid = 10, Stinger = 11, AcidBig = 12, Web = 13, Laser = 14, Bomb = 15, Bolt = 16, HeavyBolt = 17 }

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

    // Armes d'EDF 5 (voir Player.Edf5.cs).
    /// <summary>Grenade à retardement : rebondit (amortie) jusqu'à la fin de <see cref="Life"/>, puis explose.</summary>
    public bool Bounce;
    /// <summary>Grenade collante : se fixe à ce qu'elle touche (ennemi compris) et explose à la fin de <see cref="Life"/>.</summary>
    public bool Sticky;
    /// <summary>Roquette guidée : vise à chaque instant le point renvoyé (réticule du tireur), virage limité (rad/s).</summary>
    public System.Func<Vector3> Guide;
    public float TurnRate = 3f;
    /// <summary>
    /// Vol des missiles d'EDF 5 : poussée de départ sans guidage pendant <see cref="HomingDelay"/> s, montée jusqu'à l'altitude
    /// <see cref="ClimbTo"/> (tirs en piqué : Air Tortoise, Prominence, MLRA, FG High Altitude Impact), puis le moteur accélère
    /// (<see cref="Accel"/>) jusqu'à <see cref="MaxSpeed"/> et le guidage prend la cible.
    /// </summary>
    public float HomingDelay, ClimbTo, MaxSpeed, Accel;
    /// <summary>Bombe à détecteur : explose d'elle-même quand un ennemi entre dans ce rayon (0 : jamais).</summary>
    public float ProximityRadius;
    private float _proxT;
    /// <summary>Impact remplaçant l'explosion standard (napalm, sous-munitions, soin…) : position, normale.</summary>
    public System.Action<Vector3, Vector3> CustomImpact;
    /// <summary>Tir ennemi renvoyé par la garde d'un bouclier du Fencer : il frappe désormais les ennemis.</summary>
    public bool Reflected;
    /// <summary>Dégâts d'un tir ennemi renvoyé (multiple des dégâts infligés aux soldats : échelle des ennemis).</summary>
    public const float ReflectDamageMul = 8f;
    private Node3D _stuckTo;

    /// <summary>Ce à quoi la grenade collante est collée (null sinon).</summary>
    public Node3D StuckTo => _stuckTo;
    /// <summary>Normale de la surface où la grenade s'est collée (dessus de la bombe).</summary>
    public Vector3 StuckNormal { get; private set; } = Vector3.Up;
    private Vector3 _stuckOffset;

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
            case ProjectileKind.Energy: color = new Color(0.55f, 0.85f, 1f); size = 0.12f; break; // lances guidées de la Wing Diver
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
                : kind is ProjectileKind.Laser or ProjectileKind.Energy ? new Vector3(1, 1, 10) : kind == ProjectileKind.Bolt ? new Vector3(1, 1, 16)
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

    /// <summary>Guidage vers une cible avec une capacité de virage donnée (rad/s, rang « Homing » des missiles d'EDF 5).</summary>
    public void SetHoming(Node3D target, float turnRate)
    {
        _homingTarget = target;
        TurnRate = turnRate;
    }

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

    private static readonly bool Trace = Screenshot.CmdArg("missile-trace") != null;
    private float _traceT;

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (Trace && Kind == ProjectileKind.Missile && (_traceT -= dt) <= 0)
        {
            _traceT = 0.2f;
            GD.Print($"[missile] {GetInstanceId()} pos={GlobalPosition} v={Velocity.Length():0} dir={Velocity.Normalized()} climb={ClimbTo:0.0} hd={HomingDelay:0.00} tgt={(_homingTarget is Node3D h && IsInstanceValid(h) ? h.GlobalPosition.ToString() : "-")} turn={TurnRate:0.00} life={Life:0.0}");
        }
        Life -= dt;
        if (Life <= 0) { Impact(GlobalPosition, Vector3.Up, null); return; }
        if (ProximityRadius > 0 && (_proxT -= dt) <= 0)
        {
            _proxT = 0.2f;
            foreach (var e in _world.EnemiesById.Values)
                if (e.State != EnemyState.Dead && e.BodyDistance(GlobalPosition) < ProximityRadius + e.Def.Size)
                {
                    Life = 0.05f;
                    ProximityRadius = 0;
                    break;
                }
        }
        if (_stuckTo != null)
        {
            // Grenade collante : suit ce qu'elle a touché jusqu'à l'explosion.
            if (IsInstanceValid(_stuckTo)) GlobalPosition = _stuckTo.GlobalTransform * _stuckOffset;
            return;
        }
        if (MaxSpeed > 0)
        {
            var s0 = Velocity.Length();
            Velocity = Velocity / Mathf.Max(s0, 0.001f) * Mathf.MoveToward(s0, MaxSpeed, Accel * dt);
        }
        Vector3? steer = _homingTarget != null && IsInstanceValid(_homingTarget)
            ? (_homingTarget is Enemy he ? he.Center : _homingTarget.GlobalPosition + Vector3.Up) : Guide?.Invoke();
        if (HomingDelay > 0) { HomingDelay -= dt; steer = null; }
        else if (ClimbTo > 0)
        {
            // Montée : le missile se redresse vers le ciel jusqu'à l'altitude de bascule, puis pique sur la cible.
            // La bascule commence un demi-rayon de virage avant l'altitude visée (le missile ne monte pas bien au-dessus).
            if (GlobalPosition.Y < ClimbTo - Velocity.Length() / Mathf.Max(TurnRate, 0.5f) * 0.5f)
            {
                var flat = new Vector3(Velocity.X, 0, Velocity.Z);
                steer = GlobalPosition + Vector3.Up * 60f + (flat.LengthSquared() > 0.01f ? flat.Normalized() * 12f : Vector3.Zero);
            }
            else ClimbTo = 0;
        }
        if (steer is { } goal)
        {
            var sp = Velocity.Length();
            var want = (goal - GlobalPosition).Normalized();
            var cur = Velocity / Mathf.Max(sp, 0.001f);
            var ang = cur.AngleTo(want);
            // Guidage terminal vers une cible verrouillée : près du but, le virage suffit toujours à l'atteindre (rayon de virage
            // inférieur à la distance restante) ; un missile d'EDF 5 ne tourne pas en rond autour d'une cible immobile.
            var turn = TurnRate;
            if (_homingTarget != null && ClimbTo <= 0) turn = Mathf.Max(turn, sp / Mathf.Max(goal.DistanceTo(GlobalPosition), 1f) * 3f);
            var step = Mathf.Min(1f, turn * dt / Mathf.Max(ang, 0.0001f));
            Velocity = cur.Slerp(want, step).Normalized() * sp;
        }
        Velocity += Vector3.Down * Gravity * dt;
        var from = GlobalPosition;
        var to2 = from + Velocity * dt;
        var enemyShot = (int)Kind >= 10 && !Reflected;
        // Garde d'un bouclier du Fencer (hôte) : le tir repart vers son tireur.
        if (enemyShot && Authoritative && _world.HostTryReflect(this))
        {
            Reflected = true;
            enemyShot = false;
            to2 = from + Velocity * dt;
        }
        // Les tirs ennemis touchent le décor et les joueurs ; ceux des joueurs le décor et les ennemis.
        uint mask = enemyShot ? 1u : (1u | (1u << 2));
        var q = PhysicsRayQueryParameters3D.Create(from, to2, mask);
        if (_exclude != null) q.Exclude = _exclude;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count > 0)
        {
            var hp = (Vector3)hit["position"];
            var hn = (Vector3)hit["normal"];
            var hc = hit["collider"].AsGodotObject();
            if (Sticky && hc is Node3D n3)
            {
                _stuckTo = n3;
                StuckNormal = hn;
                _stuckOffset = n3.GlobalTransform.AffineInverse() * hp;
                GlobalPosition = hp;
                return;
            }
            if (Bounce)
            {
                Velocity = Velocity.Bounce(hn) * 0.45f;
                if (Velocity.Length() < 1.5f) Velocity = Vector3.Zero;
                GlobalPosition = hp + hn * 0.12f;
                return;
            }
            Impact(hp, hn, hc);
            return;
        }
        if (enemyShot && Authoritative && _world.HostProjectileHitsPlayer(this, from, to2)) { Impact(to2, Vector3.Up, null); return; }
        var floorY = World.GroundAt(to2.X, to2.Z);
        if (to2.Y < floorY - 1f)
        {
            if (Bounce || Sticky) { GlobalPosition = new Vector3(to2.X, floorY + 0.12f, to2.Z); Velocity = Bounce ? Velocity.Bounce(Vector3.Up) * 0.45f : Vector3.Zero; return; }
            Impact(new Vector3(to2.X, floorY, to2.Z), Vector3.Up, null);
            return;
        }
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
        // Abri de l'Air Raider : le tir ennemi use ses murs (hôte).
        if (enemyShot && !Reflected && Authoritative && collider is BunkerWall bw) bw.Hit(Damage);
        // Tir renvoyé : dégâts à l'ennemi touché (calculés par l'hôte), éclaboussure seule.
        if (enemyShot && Reflected && Authoritative && collider is Enemy re && re.State != EnemyState.Dead)
        {
            Net.I.SendDamageEnemy(re.Id, Damage * ReflectDamageMul, pos, false);
            _world.LocalHitFx(re, pos, true, Damage * ReflectDamageMul, false);
        }
        if (enemyShot)
        {
            if (Kind is ProjectileKind.Laser or ProjectileKind.Bolt) Effects.Sparks(_world, pos, normal);
            else if (Kind == ProjectileKind.HeavyBolt) Effects.Explosion(_world, pos + normal * 0.3f, 2.4f, ExplosionFx.Plasma);
            else if (Kind == ProjectileKind.Bomb) Effects.Explosion(_world, pos + normal * 0.3f, Radius * 0.8f, 0);
            else Effects.Goo(_world, pos, Kind == ProjectileKind.Web ? new Color(0.9f, 0.9f, 0.85f) : new Color(0.7f, 0.95f, 0.15f), Kind == ProjectileKind.AcidBig ? 2f : 0.8f, 8);
            // Gros jet d'acide : éclaboussure et gaz toxique (effet seul, sans feu).
            if (Kind == ProjectileKind.AcidBig) Effects.Explosion(_world, pos + normal * 0.2f, 3.5f, ExplosionFx.Acid);
            if (Authoritative && !Reflected) _world.HostEnemyProjectileImpact(this, pos);
        }
        else if (CustomImpact != null) CustomImpact(pos, normal);
        else if (Authoritative)
        {
            // Points de l'Air Raider : dégâts estimés de l'explosion.
            AirRaid.Explode(_world, pos + normal * 0.3f, Radius, Damage, Kind == ProjectileKind.Plasma ? 2 : 0);
            // Coup au but sur un grand ennemi dont le centre est hors du rayon de l'explosion : il est touché quand même.
            if (collider is Enemy he && he.State != EnemyState.Dead && he.BodyDistance(pos) - he.Def.Size * 1.2f > Radius)
            {
                Net.I.SendDamageEnemy(he.Id, Damage, pos, false);
                _world.LocalHitFx(he, pos, true, Damage, false);
                _world.LocalPlayer?.EarnCredits(Damage);
            }
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
