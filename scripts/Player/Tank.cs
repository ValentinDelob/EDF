using Godot;

namespace Rempart;

/// <summary>
/// Char de combat pilotable (simulation par le conducteur et réseau : voir <see cref="Vehicle"/>).
/// </summary>
public partial class Tank : Vehicle
{
    private Node3D _hull, _turret, _barrel, _muzzle;
    private Node3D[] _wheels = new Node3D[6];
    private float _turretYaw, _barrelPitch;
    private float _speed;
    private float _mgCd;
    private float _treads;

    public const float CannonDamage = 380f;
    public const float CannonRadius = 10f;

    // Dotation par sortie (réarmement au point de départ, à l'arrêt) :
    // canon 30 obus (1 chargé, 1,6 s de chargement), mitrailleuse coaxiale 5 bandes de 250.
    public readonly VehicleWeapon Cannon = new("Canon principal", "Obus", HudWeaponIcon.Tank, 1, 30, 1.6f);
    public readonly VehicleWeapon Coax = new("Mitrailleuse coaxiale", "Balles", HudWeaponIcon.Gatling, 250, 1250, 4f);

    public static Tank Create(World w, int id, Vector3 pos, float yaw)
    {
        var t = new Tank { Name = $"Tank{id}" };
        t.InitCommon(w, id, pos, yaw, 1600);
        t.Arms = new[] { t.Cannon, t.Coax };
        t.Build();
        return t;
    }

    private void Build()
    {
        var paint = Mats.Painted(new Color(0.3f, 0.34f, 0.26f), 0.6f, 0.2f);
        var dark = Mats.Metal(new Color(0.12f, 0.12f, 0.12f), 0.6f);
        var track = Mats.Painted(new Color(0.08f, 0.08f, 0.08f), 0.9f);
        _hull = new Node3D();
        AddChild(_hull);
        _hull.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.4f, 1.1f, 6.6f), Material = paint }, Position = new Vector3(0, 1.15f, 0) });
        _hull.AddChild(new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(3.4f, 0.6f, 1.2f), Material = paint }, Position = new Vector3(0, 1.0f, 3.6f), RotationDegrees = new Vector3(90, 180, 0) });
        foreach (var s in new[] { -1f, 1f })
        {
            _hull.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.95f, 7.2f), Material = track }, Position = new Vector3(1.75f * s, 0.55f, 0) });
            _hull.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.12f, 7.0f), Material = paint }, Position = new Vector3(1.75f * s, 1.08f, 0) }); // garde-boue
            for (var i = 0; i < 3; i++)
            {
                var wheel = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.84f, RadialSegments = 14, Material = dark },
                    Position = new Vector3(1.75f * s, 0.5f, -2.2f + i * 2.2f),
                    RotationDegrees = new Vector3(0, 0, 90),
                };
                _hull.AddChild(wheel);
                _wheels[(s < 0 ? 0 : 3) + i] = wheel;
            }
        }
        _turret = new Node3D { Position = new Vector3(0, 1.75f, -0.3f) };
        AddChild(_turret);
        _turret.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.15f, BottomRadius = 1.4f, Height = 0.75f, RadialSegments = 10, Material = paint }, Position = new Vector3(0, 0.37f, 0) });
        _turret.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.5f, 1.2f), Material = paint }, Position = new Vector3(0, 0.45f, -1.1f) });
        _turret.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.3f, Height = 0.3f, Material = dark }, Position = new Vector3(0.5f, 0.85f, -0.3f) }); // tourelleau
        _barrel = new Node3D { Position = new Vector3(0, 0.45f, 0.9f) };
        _turret.AddChild(_barrel);
        _barrel.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.15f, Height = 3.8f, Material = dark }, Position = new Vector3(0, 0, 1.9f), RotationDegrees = new Vector3(90, 0, 0) });
        _barrel.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.17f, Height = 0.5f, Material = dark }, Position = new Vector3(0, 0, 3.7f), RotationDegrees = new Vector3(90, 0, 0) });
        _muzzle = new Node3D { Position = new Vector3(0, 0, 4.1f) };
        _barrel.AddChild(_muzzle);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.6f, 2.0f, 7.0f) }, Position = new Vector3(0, 1.1f, 0) });
        _prompt.Position = new Vector3(0, 3.4f, 0);
        CastShadows();
    }

    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;
    public override Vector3 SeatPosition => GlobalPosition + Vector3.Up * 2.2f;
    public override string DisplayName => "Char de combat";
    public override string PromptText => "Monter dans le char";
    public override string EnterHint =>
        $"Char de combat : clic gauche canon, clic droit mitrailleuse, [{UiKit.KeyName("reload")}] recharger. Munitions limitées : arrêtez-vous au point de départ pour réarmer.";
    public override string WreckedHint => "Char détruit ! Évacuation.";

    /// <summary>Simulation par le conducteur local : déplacement, tourelle, tir.</summary>
    public override void DriveTick(Player p, float dt, float camYaw, float camPitch)
    {
        if (Wrecked) return;
        var input = _world.Paused ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var turn = -input.X * 1.1f * dt * (Mathf.Abs(_speed) > 0.5f || input.Y == 0 ? 1f : 0.6f);
        Rotation = new Vector3(0, Rotation.Y + turn, 0);
        var target = -input.Y * (input.Y < 0 ? 15f : 7f);
        if (Input.IsActionPressed("sprint") && input.Y < 0) target = 20f;
        _speed = Mathf.MoveToward(_speed, target, (Mathf.Abs(target) > Mathf.Abs(_speed) ? 7f : 12f) * dt);
        var fwd = GlobalBasis.Z;
        var v = fwd * _speed;
        v.Y = IsOnFloor() ? -1f : Velocity.Y - 24f * dt;
        Velocity = v;
        MoveAndSlide();
        // Éperonnage : un choc contre un mur à pleine vitesse abîme légèrement le char (seuil 8 m/s) et le freine.
        if (CollisionDamage(p, v, 8f, 12f, 18f, 0.4f, dt) > 0) _speed *= 0.25f;
        CheckWrecked();
        // Écrase les petits insectes sur son passage.
        if (Mathf.Abs(_speed) > 4f) _world.TankCrush(this, GlobalPosition + fwd * 3.6f * Mathf.Sign(_speed), 3f, Mathf.Abs(_speed) * 6f * dt * 10f);

        // Tourelle : suit la caméra.
        var localYaw = Mathf.Wrap(camYaw + Mathf.Pi - Rotation.Y, -Mathf.Pi, Mathf.Pi);
        _turretYaw = Mathf.LerpAngle(_turretYaw, localYaw, Mathf.Clamp(dt * 2.2f, 0, 1));
        _barrelPitch = Mathf.Lerp(_barrelPitch, Mathf.Clamp(camPitch + 0.08f, -0.15f, 0.55f), Mathf.Clamp(dt * 4f, 0, 1));
        ApplyPose(dt);

        _mgCd -= dt;
        AmmoTick(dt);
        if (!_world.Paused && Input.IsActionJustPressed("reload")) ManualReload();
        if (!_world.Paused && Input.IsActionPressed("fire"))
        {
            if (Cannon.TryFire()) FireCannon(p);
            else if (Cannon.Empty && Input.IsActionJustPressed("fire")) Sfx.Ui("empty");
        }
        if (!_world.Paused && Input.IsActionPressed("aim") && _mgCd <= 0)
        {
            if (Coax.TryFire()) FireMg(p);
            else if (Coax.Empty && Input.IsActionJustPressed("aim")) Sfx.Ui("empty");
        }

        BroadcastState(dt, Rotation, _turretYaw, _barrelPitch, 0f, _speed);
    }

    private void ApplyPose(float dt)
    {
        _turret.Rotation = new Vector3(0, _turretYaw, 0);
        _barrel.Rotation = new Vector3(-_barrelPitch, 0, 0);
        _treads += _speed * dt;
        foreach (var w in _wheels) if (w != null) w.Rotation = new Vector3(_treads / 0.42f, 0, Mathf.Pi / 2);
        _hull.Rotation = new Vector3(Mathf.Clamp(-_speed * 0.004f, -0.05f, 0.05f), 0, 0);
    }

    private void FireCannon(Player p)
    {
        var dir = _muzzle.GlobalBasis.Z;
        var from = _muzzle.GlobalPosition;
        Projectile.Spawn(_world, ProjectileKind.Shell, from, dir * 140f, true, CannonDamage * p.DamageMul * (1f + 0.25f * (int)_world.Difficulty), CannonRadius)
            .IgnoreBody(this);
        Net.I.SendShotFx(10 + (int)ProjectileKind.Shell, from, from + dir * 140f);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.6f, 0.2f), 1.6f);
        Effects.Smoke(_world, from, 2f, 1.5f);
        Sfx.Ui("explosion_small", 0f, 0.7f);
        p.AddShake(0.35f);
        var tw = CreateTween();
        tw.TweenProperty(_barrel, "position:z", 0.4f, 0.05f);
        tw.TweenProperty(_barrel, "position:z", 0.9f, 0.35f);
    }

    private void FireMg(Player p)
    {
        _mgCd = 0.09f;
        var from = _turret.GlobalPosition + _turret.GlobalBasis * new Vector3(0.5f, 0.9f, 0.3f);
        var aim = p.AimPoint(200f, out var col);
        var dir = (aim - from).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * 200f, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var end = from + dir * 200f;
        if (hit.Count > 0)
        {
            end = (Vector3)hit["position"];
            if (hit["collider"].AsGodotObject() is Enemy e && e.State != EnemyState.Dead)
            {
                var dmg = 14f * p.DamageMul * (1f + 0.25f * (int)_world.Difficulty);
                Net.I.SendDamageEnemy(e.Id, dmg, end, false);
                _world.LocalHitFx(e, end, true, dmg);
            }
            else Effects.Sparks(_world, end, (Vector3)hit["normal"]);
        }
        Effects.Tracer(_world.FxRoot, from, end, new Color(1f, 0.75f, 0.35f), 0.05f);
        Sfx.Ui("minigun", -6f);
        Net.I.SendShotFx(0, from, end);
    }

    protected override void RemoteTick(float dt)
    {
        var k = Mathf.Clamp(dt * 12f, 0, 1);
        InterpolatePosition(dt);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netRot.Y, k), 0);
        _turretYaw = Mathf.LerpAngle(_turretYaw, _netA, k);
        _barrelPitch = Mathf.Lerp(_barrelPitch, _netB, k);
        _speed = _netSpeed;
        ApplyPose(dt);
    }

    public override void Wreck()
    {
        if (Wrecked) return;
        Wrecked = true;
        Driver = 0;
        Effects.Explosion(_world, GlobalPosition + Vector3.Up * 1.5f, 12f, 0);
        BlackenMeshes();
        _turret.Rotation = new Vector3(0.2f, _turretYaw + 0.6f, 0.15f);
        Effects.Smoke(_world, GlobalPosition + Vector3.Up * 2f, 3f, 6f);
    }
}
