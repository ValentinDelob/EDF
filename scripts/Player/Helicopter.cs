using Godot;

namespace Rempart;

/// <summary>
/// Hélicoptère d'attaque pilotable : vol arcade mais inertiel (collectif, inclinaison, lacet qui suit la caméra),
/// minigun de nez (tir instantané) et paniers de roquettes (salves de 4). Simulation et réseau : voir <see cref="Vehicle"/>.
/// </summary>
public partial class Helicopter : Vehicle
{
    private Node3D _body, _rotor, _tailRotor, _gun, _gunBarrels, _gunMuzzle;
    private readonly Node3D[] _podMuzzles = new Node3D[2];
    private StandardMaterial3D _discMat;
    private MeshInstance3D _disc;
    private GpuParticles3D _dust;

    // Pilotage
    private float _tiltF, _tiltS;      // inclinaison avant / latérale (radians, + = nez bas / penché vers la gauche)
    private float _yawRate;
    private float _vs;                  // vitesse verticale
    private float _rotorSpeed;          // régime du rotor (0..1)
    private float _rotorAngle, _tailAngle, _barrelSpin;
    private float _agl;                 // hauteur au-dessus du sol (ou du toit) sous l'appareil
    private bool _wreckSent;
    private float _chopT;

    // Armes
    private float _mgCd, _rocketCd, _salvoT;
    private int _salvoLeft, _salvoSide;
    private float _gunYaw, _gunPitch;

    // Épave
    private bool _falling, _settled;
    private float _fallFrom, _spin;

    public const float MaxAltitude = 120f;
    public const float MinAltitude = 2f;
    private const float RocketCooldown = 3.5f;
    private const float RocketSpeed = 95f;

    public static Helicopter Create(World w, int id, Vector3 pos, float yaw)
    {
        var h = new Helicopter { Name = $"Heli{id}" };
        h.InitCommon(w, id, pos, yaw, 1100);
        h.Build();
        return h;
    }

    // ------------------------------------------------------------------ Modèle

    private void Build()
    {
        var paint = Mats.Painted(new Color(0.25f, 0.29f, 0.23f), 0.55f, 0.25f);
        var paint2 = Mats.Painted(new Color(0.2f, 0.23f, 0.19f), 0.6f, 0.25f);
        var dark = Mats.Metal(new Color(0.1f, 0.1f, 0.1f), 0.55f);
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.12f, 0.14f), Metallic = 0.6f, Roughness = 0.08f };

        // Corps pivotant (inclinaison) autour du centre de gravité.
        _body = new Node3D { Position = new Vector3(0, 1.6f, 0) };
        AddChild(_body);
        void Add(Node3D parent, Mesh m, Vector3 pos, Vector3 rotDeg = default, Vector3? scale = null) =>
            parent.AddChild(new MeshInstance3D { Mesh = m, Position = pos, RotationDegrees = rotDeg, Scale = scale ?? Vector3.One });

        // Fuselage étroit (biplace en tandem), arrondi.
        Add(_body, new CapsuleMesh { Radius = 0.85f, Height = 6.6f, RadialSegments = 14, Rings = 6, Material = paint }, new Vector3(0, 0, 0.7f), new Vector3(90, 0, 0), new Vector3(0.85f, 1f, 1f));
        Add(_body, new BoxMesh { Size = new Vector3(1.3f, 0.7f, 2.6f), Material = paint2 }, new Vector3(0, 0.9f, -0.5f)); // carter moteur
        foreach (var s in new[] { -1f, 1f })
        {
            Add(_body, new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.32f, Height = 1.4f, RadialSegments = 10, Material = paint2 }, new Vector3(0.75f * s, 0.8f, -0.4f), new Vector3(90, 0, 0)); // turbines
            Add(_body, new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.1f, RadialSegments = 10, Material = dark }, new Vector3(0.75f * s, 0.8f, -1.12f), new Vector3(90, 0, 0)); // tuyères
        }
        // Verrières (pilote devant, tireur derrière et plus haut).
        Add(_body, new SphereMesh { Radius = 0.62f, Height = 1.24f, RadialSegments = 14, Rings = 8, Material = glass }, new Vector3(0, 0.55f, 2.35f), default, new Vector3(0.95f, 0.85f, 1.6f));
        Add(_body, new SphereMesh { Radius = 0.62f, Height = 1.24f, RadialSegments = 14, Rings = 8, Material = glass }, new Vector3(0, 0.78f, 1.05f), default, new Vector3(0.95f, 0.85f, 1.4f));
        // Poutre de queue, dérive, stabilisateur.
        Add(_body, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.5f, Height = 5.6f, RadialSegments = 10, Material = paint }, new Vector3(0, 0.35f, -4.6f), new Vector3(-90, 0, 0));
        Add(_body, new BoxMesh { Size = new Vector3(0.12f, 1.8f, 1.0f), Material = paint }, new Vector3(0, 1.0f, -7.25f), new Vector3(-15, 0, 0));
        Add(_body, new BoxMesh { Size = new Vector3(2.4f, 0.08f, 0.6f), Material = paint2 }, new Vector3(0, 0.35f, -6.6f));
        // Ailettes : support des paniers de roquettes.
        Add(_body, new BoxMesh { Size = new Vector3(3.6f, 0.12f, 1.0f), Material = paint2 }, new Vector3(0, -0.15f, 0.4f), new Vector3(0, 0, 0));
        for (var i = 0; i < 2; i++)
        {
            var s = i == 0 ? 1f : -1f; // 0 = gauche (+X, l'appareil regarde vers +Z), 1 = droite
            Add(_body, new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 1.8f, RadialSegments = 12, Material = paint2 }, new Vector3(1.65f * s, -0.45f, 0.5f), new Vector3(90, 0, 0));
            Add(_body, new CylinderMesh { TopRadius = 0.24f, BottomRadius = 0.24f, Height = 0.04f, RadialSegments = 12, Material = dark }, new Vector3(1.65f * s, -0.45f, 1.41f), new Vector3(90, 0, 0));
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.3f, 0.5f), Material = paint2 }, new Vector3(1.65f * s, -0.22f, 0.5f)); // pylône
            _podMuzzles[i] = new Node3D { Position = new Vector3(1.65f * s, -0.45f, 1.6f) };
            _body.AddChild(_podMuzzles[i]);
            // Patins
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.1f, 4.4f), Material = dark }, new Vector3(0.95f * s, -1.5f, 0.5f));
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.5f), Material = dark }, new Vector3(0.95f * s, -1.42f, 2.85f), new Vector3(-30, 0, 0));
            foreach (var z in new[] { 1.6f, -0.6f })
                Add(_body, new BoxMesh { Size = new Vector3(0.08f, 0.85f, 0.08f), Material = dark }, new Vector3(0.78f * s, -1.1f, z), new Vector3(0, 0, -25 * s));
        }
        // Tourelle de nez (minigun).
        _gun = new Node3D { Position = new Vector3(0, -0.75f, 3.3f) };
        _body.AddChild(_gun);
        Add(_gun, new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 10, Rings = 6, Material = dark }, Vector3.Zero);
        _gunBarrels = new Node3D { Position = new Vector3(0, -0.05f, 0.2f) };
        _gun.AddChild(_gunBarrels);
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3f;
            Add(_gunBarrels, new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 1.1f, RadialSegments = 6, Material = dark }, new Vector3(Mathf.Cos(a) * 0.07f, Mathf.Sin(a) * 0.07f, 0.55f), new Vector3(90, 0, 0));
        }
        _gunMuzzle = new Node3D { Position = new Vector3(0, 0, 1.15f) };
        _gunBarrels.AddChild(_gunMuzzle);

        // Rotor principal : 4 pales + disque flou à haut régime.
        _rotor = new Node3D { Position = new Vector3(0, 1.45f, 0.1f) };
        _body.AddChild(_rotor);
        Add(_rotor, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 0.5f, Material = dark }, new Vector3(0, -0.2f, 0));
        Add(_rotor, new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.18f, Material = dark }, new Vector3(0, 0.05f, 0));
        Add(_rotor, new BoxMesh { Size = new Vector3(0.34f, 0.05f, 11.5f), Material = dark }, new Vector3(0, 0.08f, 0));
        Add(_rotor, new BoxMesh { Size = new Vector3(11.5f, 0.05f, 0.34f), Material = dark }, new Vector3(0, 0.08f, 0));
        _discMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.12f, 0.12f, 0f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 5.75f, BottomRadius = 5.75f, Height = 0.02f, RadialSegments = 32, Material = _discMat },
            Position = new Vector3(0, 0.09f, 0),
        };
        _rotor.AddChild(_disc);
        // Rotor de queue (côté gauche de la dérive).
        _tailRotor = new Node3D { Position = new Vector3(0.18f, 1.25f, -7.45f) };
        _body.AddChild(_tailRotor);
        Add(_tailRotor, new BoxMesh { Size = new Vector3(0.05f, 1.7f, 0.18f), Material = dark }, Vector3.Zero);
        Add(_tailRotor, new BoxMesh { Size = new Vector3(0.05f, 0.18f, 1.7f), Material = dark }, Vector3.Zero);

        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.4f, 2.6f, 8.0f) }, Position = new Vector3(0, 1.35f, 0.4f) });
        _prompt.Position = new Vector3(0, 4.4f, 0);
        CastShadows();
        _disc.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        BuildDust();
    }

    /// <summary>Poussière soulevée par le souffle du rotor près du sol.</summary>
    private void BuildDust()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(0.55f, 0.5f, 0.42f, 0f));
        g.AddPoint(0.25f, new Color(0.58f, 0.53f, 0.45f, 0.45f));
        g.SetColor(g.GetPointCount() - 1, new Color(0.62f, 0.58f, 0.52f, 0f));
        var scale = new Curve { MaxValue = 4f };
        scale.AddPoint(new Vector2(0, 1f));
        scale.AddPoint(new Vector2(1, 4f));
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 2.5f,
            Direction = new Vector3(1, 0, 0),
            Spread = 180,
            Flatness = 1f,
            InitialVelocityMin = 9f,
            InitialVelocityMax = 16f,
            DampingMin = 4f,
            DampingMax = 7f,
            Gravity = new Vector3(0, 0.8f, 0),
            ScaleCurve = new CurveTexture { Curve = scale },
            ColorRamp = new GradientTexture1D { Gradient = g },
        };
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) } },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0), Width = 32, Height = 32,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _dust = new GpuParticles3D
        {
            Amount = 48,
            Lifetime = 1.4f,
            LocalCoords = false,
            Emitting = false,
            TopLevel = true,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(1.6f, 1.6f), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-30, -5, -30), new Vector3(60, 15, 60)),
        };
        AddChild(_dust);
    }

    // ------------------------------------------------------------------ Description

    public float Altitude => _agl;
    public float RocketsReady => _salvoLeft > 0 ? 0f : Mathf.Clamp(1f - _rocketCd / RocketCooldown, 0, 1);
    public override Vector3 SeatPosition => GlobalPosition + Vector3.Up * 2.0f;
    public override float InteractRadius => 7f;
    public override float HitRadius => 3.2f;
    public override float ArmorFactor => 0.7f;
    public override float CameraDistance(bool aiming) => 14f;
    public override float CameraLift => 1.8f;
    public override float AimZoom => 1f;
    public override bool CanExit => _agl < 1.2f && Velocity.Length() < 4f;
    public override string DisplayName => "Hélicoptère d'attaque";
    public override string PromptText => "Piloter l'hélicoptère";
    public override string EnterHint =>
        $"Hélicoptère : [{UiKit.KeyName("jump")}] monter, [{UiKit.KeyName("sprint")}] descendre, clic gauche minigun, clic droit roquettes. Posez-vous pour descendre.";
    public override string WreckedHint => "Hélicoptère abattu ! Évacuation.";
    public override string HudAmmo => RocketsReady >= 1 ? "4" : _salvoLeft > 0 ? $"{_salvoLeft}" : $"{Mathf.RoundToInt(RocketsReady * 100)}";
    public override string HudMag => RocketsReady >= 1 ? "ROQUETTES" : _salvoLeft > 0 ? "SALVE" : "%";
    public override float HudReady => RocketsReady;
    public override string HudSub => $"Blindage {Mathf.CeilToInt(Mathf.Max(0, Health))}   ·   Alt. {Mathf.RoundToInt(_agl)} m";
    public override string HudOther => $"Clic gauche : minigun   ·   [{UiKit.KeyName("jump")}] monter  [{UiKit.KeyName("sprint")}] descendre";

    /// <summary>Les insectes au sol ne mordent pas un appareil en vol ; les tirs proches touchent.</summary>
    public override bool IgnoresDamageFrom(Vector3 from) => _agl > 5f && from.DistanceTo(GlobalPosition + Vector3.Up * 1.5f) > 12f;

    // ------------------------------------------------------------------ Vol

    private float MeasureAgl()
    {
        var from = GlobalPosition + Vector3.Up * 0.3f;
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 200f, 1u);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0) return Mathf.Max(0, GlobalPosition.Y);
        var p = (Vector3)hit["position"];
        if (_dust != null) _dust.GlobalPosition = p + Vector3.Up * 0.3f;
        return Mathf.Max(0, GlobalPosition.Y - p.Y);
    }

    /// <summary>Simulation par le pilote local : collectif, inclinaison, lacet, armes.</summary>
    public override void DriveTick(Player p, float dt, float camYaw, float camPitch)
    {
        if (Wrecked) return;
        var paused = _world.Paused;
        var input = paused ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var up = !paused && Input.IsActionPressed("jump");
        var down = !paused && (Input.IsActionPressed("sprint") || Input.IsActionPressed("dodge"));
        var collective = (up ? 1f : 0f) - (down ? 1f : 0f);

        _rotorSpeed = Mathf.MoveToward(_rotorSpeed, 1f, dt * 0.5f); // ~2 s de mise en route
        var lift = _rotorSpeed > 0.85f;
        _agl = MeasureAgl();
        var landed = IsOnFloor() && _vs <= 0.1f;

        // Lacet : le nez rejoint la caméra en douceur (vitesse de rotation bornée).
        var diff = Mathf.AngleDifference(Rotation.Y, camYaw + Mathf.Pi);
        var targetRate = landed ? 0f : Mathf.Clamp(diff * 2.2f, -1.5f, 1.5f);
        _yawRate = Mathf.Lerp(_yawRate, targetRate, Mathf.Clamp(dt * 3f, 0, 1));
        Rotation = new Vector3(0, Rotation.Y + _yawRate * dt, 0);

        // Inclinaison : W/S basculent le nez, A/D penchent l'appareil. Réponse lente = poids.
        var tiltK = Mathf.Clamp(dt * (landed ? 6f : 1.8f), 0, 1);
        _tiltF = Mathf.Lerp(_tiltF, landed ? 0f : -input.Y * 0.3f, tiltK);
        _tiltS = Mathf.Lerp(_tiltS, landed ? 0f : -input.X * 0.26f, tiltK);

        // Vitesse horizontale : poussée selon l'inclinaison, traînée (vitesse max ~ 36 m/s).
        var fwd = GlobalBasis.Z;
        var left = GlobalBasis.X;
        var hv = new Vector3(Velocity.X, 0, Velocity.Z);
        if (!landed && lift) hv += (fwd * _tiltF + left * _tiltS) * 80f * dt;
        hv *= Mathf.Exp(-(landed ? 5f : 0.7f) * dt);

        // Collectif : vitesse verticale cible, maintien d'altitude au neutre, plancher et plafond.
        float targetVs;
        if (!lift) targetVs = landed ? 0f : -10f;
        else
        {
            targetVs = collective * (collective > 0 ? 10f : 8f);
            if (!landed && !down && _agl < MinAltitude) targetVs = Mathf.Max(targetVs, (MinAltitude - _agl) * 3f);
            if (down && _agl < 3f) targetVs = Mathf.Max(targetVs, -2.5f); // posé en douceur
            if (GlobalPosition.Y > MaxAltitude - 3f) targetVs = Mathf.Min(targetVs, (MaxAltitude - GlobalPosition.Y) * 1.5f);
        }
        _vs = Mathf.MoveToward(_vs, targetVs, 14f * dt);
        if (landed && _vs < 0) _vs = 0;

        var before = hv + Vector3.Up * _vs;
        Velocity = before;
        MoveAndSlide();
        var after = GetRealVelocity();
        _vs = Velocity.Y;
        // Choc contre un immeuble ou atterrissage brutal.
        var loss = (before - after).Length();
        if (loss > 13f && !landed)
        {
            Health -= (loss - 13f) * 22f;
            p.AddShake(Mathf.Clamp(loss / 30f, 0.2f, 0.8f));
            Sfx.Ui("hit", 0f, 0.5f);
            Effects.Sparks(_world, GlobalPosition + Vector3.Up * 1.5f + after.Normalized() * 2f, -before.Normalized());
        }
        if (Health <= 0 && !_wreckSent)
        {
            _wreckSent = true;
            Net.I.SendVehicleWrecked(Id);
        }

        // Inclinaison visuelle en virage.
        var speedK = Mathf.Clamp(hv.Length() / 30f, 0, 1);
        var bank = _yawRate * 0.18f * speedK;
        ApplyPose(_tiltF, _tiltS + bank);

        // Armes
        _mgCd -= dt;
        _rocketCd -= dt;
        AimGun(p, dt);
        if (!paused && Input.IsActionPressed("fire") && _mgCd <= 0) FireMinigun(p);
        if (!paused && Input.IsActionPressed("aim") && _salvoLeft == 0 && _rocketCd <= 0) { _salvoLeft = 4; _salvoT = 0; }
        if (_salvoLeft > 0)
        {
            _salvoT -= dt;
            if (_salvoT <= 0)
            {
                FireRocket(p, _salvoSide);
                _salvoSide = 1 - _salvoSide;
                _salvoT = 0.12f;
                if (--_salvoLeft == 0) _rocketCd = RocketCooldown;
            }
        }

        BroadcastState(dt, new Vector3(_tiltF, Rotation.Y, _tiltS + bank), _rotorSpeed, _gunYaw, hv.Length());
    }

    // tiltS > 0 : penché vers la gauche (+X local), donc rotation négative autour de Z.
    private void ApplyPose(float tiltF, float tiltS) => _body.Rotation = new Vector3(tiltF, 0, -tiltS);

    // ------------------------------------------------------------------ Armes

    private void AimGun(Player p, float dt)
    {
        var aim = p.AimPoint(260f, out _);
        var local = _gun.GetParent<Node3D>().ToLocal(aim) - _gun.Position;
        var yaw = Mathf.Clamp(Mathf.Atan2(local.X, local.Z), -1.6f, 1.6f);
        var pitch = Mathf.Clamp(Mathf.Atan2(-local.Y, new Vector2(local.X, local.Z).Length()), -0.2f, 1.2f);
        _gunYaw = Mathf.LerpAngle(_gunYaw, yaw, Mathf.Clamp(dt * 10f, 0, 1));
        _gunPitch = Mathf.Lerp(_gunPitch, pitch, Mathf.Clamp(dt * 10f, 0, 1));
        _gun.Rotation = new Vector3(_gunPitch, _gunYaw, 0);
    }

    private float DamageScale(Player p) => p.DamageMul * (1f + 0.25f * (int)_world.Difficulty);

    private void FireMinigun(Player p)
    {
        _mgCd = 0.055f;
        _barrelSpin += 1.2f;
        var from = _gunMuzzle.GlobalPosition;
        var aim = p.AimPoint(260f, out _);
        var dir = (aim - from).Normalized();
        dir = (dir + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)) * 0.012f).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * 260f, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var end = from + dir * 260f;
        if (hit.Count > 0)
        {
            end = (Vector3)hit["position"];
            if (hit["collider"].AsGodotObject() is Enemy e && e.State != EnemyState.Dead)
            {
                var dmg = 13f * DamageScale(p);
                Net.I.SendDamageEnemy(e.Id, dmg, end, false);
                _world.LocalHitFx(e, end, true, dmg);
            }
            else Effects.Sparks(_world, end, (Vector3)hit["normal"]);
        }
        Effects.Tracer(_world.FxRoot, from, end, new Color(1f, 0.75f, 0.35f), 0.05f);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.7f, 0.3f), 0.35f);
        Sfx.Ui("minigun", -5f);
        Net.I.SendShotFx(0, from, end);
    }

    private void FireRocket(Player p, int side)
    {
        var from = _podMuzzles[side].GlobalPosition;
        var aim = p.AimPoint(400f, out _);
        var dir = (aim - from).Normalized();
        dir = (dir + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)) * 0.02f).Normalized();
        var vel = dir * RocketSpeed + new Vector3(Velocity.X, 0, Velocity.Z) * 0.5f;
        Projectile.Spawn(_world, ProjectileKind.Rocket, from, vel, true, 160f * DamageScale(p), 6f);
        Net.I.SendShotFx(10 + (int)ProjectileKind.Rocket, from, from + vel);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.6f, 0.2f), 0.9f);
        Effects.Smoke(_world, from - GlobalBasis.Z * 1.5f, 1.2f, 1f);
        Sfx.Ui("rocket", -3f, (float)GD.RandRange(0.95, 1.1));
        p.AddShake(0.08f);
    }

    // ------------------------------------------------------------------ Autres pairs, effets

    protected override void RemoteTick(float dt)
    {
        var k = Mathf.Clamp(dt * 12f, 0, 1);
        InterpolatePosition(dt);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netRot.Y, k), 0);
        _tiltF = Mathf.Lerp(_tiltF, _netRot.X, k);
        _tiltS = Mathf.Lerp(_tiltS, _netRot.Z, k);
        ApplyPose(_tiltF, _tiltS);
        _gunYaw = Mathf.LerpAngle(_gunYaw, _netB, k);
        _gun.Rotation = new Vector3(0.1f, _gunYaw, 0);
        if (Driver != 0) _rotorSpeed = Mathf.Lerp(_rotorSpeed, _netA, k);
        _agl = MeasureAgl();
    }

    protected override void VisualTick(float dt)
    {
        if (Driver == 0 || Wrecked) _rotorSpeed = Mathf.MoveToward(_rotorSpeed, 0f, dt * (Wrecked ? 0.6f : 0.25f));
        if (IsLocalDriver == false && Wrecked) _agl = MeasureAgl();
        _rotorAngle = Mathf.Wrap(_rotorAngle + _rotorSpeed * 26f * dt, 0, Mathf.Tau);
        _tailAngle = Mathf.Wrap(_tailAngle + _rotorSpeed * 60f * dt, 0, Mathf.Tau);
        _rotor.Rotation = new Vector3(0, _rotorAngle, 0);
        _tailRotor.Rotation = new Vector3(_tailAngle, 0, 0);
        _discMat.AlbedoColor = new Color(0.12f, 0.12f, 0.12f, Mathf.Clamp((_rotorSpeed - 0.4f) * 0.45f, 0, 0.25f));
        _barrelSpin = Mathf.MoveToward(_barrelSpin, 0, dt * 3f);
        _gunBarrels.Rotation = new Vector3(0, 0, _gunBarrels.Rotation.Z + _barrelSpin * 30f * dt);

        // Souffle du rotor au sol.
        var dustOn = _rotorSpeed > 0.6f && _agl < 14f && !Wrecked;
        _dust.Emitting = dustOn;
        if (dustOn) _dust.AmountRatio = Mathf.Clamp(1.2f - _agl / 14f, 0.15f, 1f) * _rotorSpeed;

        // Battement des pales.
        if (_rotorSpeed > 0.2f)
        {
            _chopT -= dt;
            if (_chopT <= 0)
            {
                _chopT = 0.11f / _rotorSpeed;
                if (IsLocalDriver) Sfx.Ui("dash", -16f, 0.42f + _rotorSpeed * 0.1f);
                else Sfx.At("dash", GlobalPosition + Vector3.Up * 2f, -8f, 0.42f + _rotorSpeed * 0.1f);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_settled)
        {
            // Posé sur ses patins dès la première image (même résultat chez tous les pairs).
            _settled = true;
            if (Driver == 0)
            {
                MoveAndCollide(Vector3.Down * 1.5f);
                _netPos = GlobalPosition;
            }
        }
        if (!_falling) return;
        // L'épave tombe en vrille puis s'écrase.
        var dt = (float)delta;
        var v = Velocity;
        v.Y -= 22f * dt;
        v.X *= Mathf.Exp(-0.5f * dt);
        v.Z *= Mathf.Exp(-0.5f * dt);
        Velocity = v;
        _spin = Mathf.MoveToward(_spin, 3.5f, dt * 2f);
        Rotation = new Vector3(0, Rotation.Y + _spin * dt, 0);
        _tiltS = Mathf.MoveToward(_tiltS, 0.35f, dt * 0.4f);
        _tiltF = Mathf.MoveToward(_tiltF, 0.25f, dt * 0.3f);
        ApplyPose(_tiltF, _tiltS);
        MoveAndSlide();
        if (IsOnFloor() || GlobalPosition.Y < -2f)
        {
            _falling = false;
            Velocity = Vector3.Zero;
            if (_fallFrom - GlobalPosition.Y > 3f)
            {
                Effects.Explosion(_world, GlobalPosition + Vector3.Up * 1.2f, 10f, 0);
                Effects.Smoke(_world, GlobalPosition + Vector3.Up * 2f, 3f, 8f);
            }
            _tiltF = 0.08f;
            _tiltS = 0.3f;
            ApplyPose(_tiltF, _tiltS);
        }
    }

    public override void Wreck()
    {
        if (Wrecked) return;
        Wrecked = true;
        Driver = 0;
        _salvoLeft = 0;
        Effects.Explosion(_world, GlobalPosition + Vector3.Up * 1.8f, 10f, 0);
        Effects.Smoke(_world, GlobalPosition + Vector3.Up * 2f, 3f, 6f);
        BlackenMeshes();
        _disc.Visible = false;
        _falling = true;
        _fallFrom = GlobalPosition.Y;
        _spin = _yawRate;
    }
}
