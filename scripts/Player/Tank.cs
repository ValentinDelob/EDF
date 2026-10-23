using Godot;

namespace Rempart;

/// <summary>
/// Char de combat pilotable. Le conducteur le simule localement et diffuse son état ;
/// l'hôte attribue les places (une seule à la fois).
/// </summary>
public partial class Tank : CharacterBody3D
{
    public int Id;
    public long Driver;
    public float Health = 1600;
    public float MaxHealth = 1600;
    public bool Wrecked;
    private World _world;
    private Node3D _hull, _turret, _barrel, _muzzle;
    private Node3D[] _wheels = new Node3D[6];
    private float _turretYaw, _barrelPitch;
    private float _speed;
    private float _fireCd, _mgCd;
    private Vector3 _netPos;
    private float _netYaw, _netTurret, _netPitch;
    private float _netTimer;
    private float _treads;
    private Label3D _prompt;

    public const float CannonDamage = 380f;
    public const float CannonRadius = 10f;

    public static Tank Create(World w, int id, Vector3 pos, float yaw)
    {
        var t = new Tank { _world = w, Id = id, Name = $"Tank{id}", CollisionLayer = 1, CollisionMask = 1 };
        t.Build();
        t.Position = pos;
        t.Rotation = new Vector3(0, yaw, 0);
        t._netPos = pos;
        t._netYaw = yaw;
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
        _prompt = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 10, PixelSize = 0.006f,
            Position = new Vector3(0, 3.4f, 0), Font = UiKit.BodyFont, Visible = false, NoDepthTest = true,
        };
        AddChild(_prompt);
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false)) ((MeshInstance3D)n).CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    public bool IsLocalDriver => Driver == Net.I.MyId && Driver != 0;
    public float CannonReady => Mathf.Clamp(1f - _fireCd / 1.6f, 0, 1);
    public Vector3 SeatPosition => GlobalPosition + Vector3.Up * 2.2f;
    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;

    /// <summary>Simulation par le conducteur local : déplacement, tourelle, tir.</summary>
    public void DriveTick(Player p, float dt, float camYaw, float camPitch)
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
        // Écrase les petits insectes sur son passage.
        if (Mathf.Abs(_speed) > 4f) _world.TankCrush(this, GlobalPosition + fwd * 3.6f * Mathf.Sign(_speed), 3f, Mathf.Abs(_speed) * 6f * dt * 10f);

        // Tourelle : suit la caméra.
        var localYaw = Mathf.Wrap(camYaw + Mathf.Pi - Rotation.Y, -Mathf.Pi, Mathf.Pi);
        _turretYaw = Mathf.LerpAngle(_turretYaw, localYaw, Mathf.Clamp(dt * 2.2f, 0, 1));
        _barrelPitch = Mathf.Lerp(_barrelPitch, Mathf.Clamp(camPitch + 0.08f, -0.15f, 0.55f), Mathf.Clamp(dt * 4f, 0, 1));
        ApplyPose(dt);

        _fireCd -= dt;
        _mgCd -= dt;
        if (!_world.Paused && Input.IsActionPressed("fire") && _fireCd <= 0) FireCannon(p);
        if (!_world.Paused && Input.IsActionPressed("aim") && _mgCd <= 0) FireMg(p);

        _netTimer -= dt;
        if (_netTimer <= 0)
        {
            _netTimer = 0.05f;
            Net.I.SendTankState(Id, GlobalPosition, Rotation.Y, _turretYaw, _barrelPitch, Health, _speed);
        }
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
        _fireCd = 1.6f;
        var dir = _muzzle.GlobalBasis.Z;
        var from = _muzzle.GlobalPosition;
        Projectile.Spawn(_world, ProjectileKind.Shell, from, dir * 140f, true, CannonDamage * p.DamageMul * (1f + 0.25f * (int)_world.Difficulty), CannonRadius);
        Net.I.SendShotFx(10 + (int)ProjectileKind.Shell, from, from + dir * 140f);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.6f, 0.2f), 1.6f);
        Effects.Smoke(_world, from, 2f, 1.5f);
        Sfx.Ui("explosion_small", 0f, 0.7f);
        p.AddShake(0.35f);
        _hull.Position = Vector3.Back * 0.0f;
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

    public void ApplyState(Vector3 pos, float yaw, float turret, float pitch, float hp, float speed)
    {
        _netPos = pos;
        _netYaw = yaw;
        _netTurret = turret;
        _netPitch = pitch;
        Health = hp;
        _speed = speed;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (!IsLocalDriver && !Wrecked)
        {
            GlobalPosition = GlobalPosition.DistanceSquaredTo(_netPos) > 100 ? _netPos : GlobalPosition.Lerp(_netPos, Mathf.Clamp(dt * 12f, 0, 1));
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netYaw, Mathf.Clamp(dt * 12f, 0, 1)), 0);
            _turretYaw = Mathf.LerpAngle(_turretYaw, _netTurret, Mathf.Clamp(dt * 12f, 0, 1));
            _barrelPitch = Mathf.Lerp(_barrelPitch, _netPitch, Mathf.Clamp(dt * 12f, 0, 1));
            ApplyPose(dt);
        }
        // Invite « monter à bord » pour le joueur local à proximité.
        var lp = _world.LocalPlayer;
        var near = !Wrecked && Driver == 0 && lp != null && !lp.Downed && lp.Vehicle == null && lp.GlobalPosition.DistanceTo(GlobalPosition) < 6f;
        _prompt.Visible = near;
        if (near) _prompt.Text = $"[{UiKit.KeyName("interact")}] Monter dans le char";
    }

    public void Wreck()
    {
        if (Wrecked) return;
        Wrecked = true;
        Driver = 0;
        Effects.Explosion(_world, GlobalPosition + Vector3.Up * 1.5f, 12f, 0);
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).MaterialOverride = Mats.Painted(new Color(0.08f, 0.07f, 0.06f), 0.95f);
        _turret.Rotation = new Vector3(0.2f, _turretYaw + 0.6f, 0.15f);
        Effects.Smoke(_world, GlobalPosition + Vector3.Up * 2f, 3f, 6f);
    }
}
