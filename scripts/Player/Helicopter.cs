using Godot;

namespace Rempart;

/// <summary>
/// Hélicoptère d'attaque pilotable : vol arcade mais inertiel (collectif, inclinaison, lacet qui suit la caméra),
/// minigun en tourelle de menton (orientable, débattement limité) et paniers de roquettes latéraux (salves de 4).
/// Munitions limitées par sortie (réarmement posé sur l'aire de départ), dégâts de collision selon la vitesse
/// d'impact (rotor compris), sons synthétisés en boucle (rotor, turbine, minigun). Simulation et réseau : voir <see cref="Vehicle"/>.
/// </summary>
public partial class Helicopter : Vehicle
{
    private Node3D _body, _rotor, _tailRotor, _gun, _gunBarrels, _gunMuzzle;
    private readonly Node3D[] _pods = new Node3D[2];
    private readonly Node3D[] _podMuzzles = new Node3D[2];
    private StandardMaterial3D _discMat;
    private MeshInstance3D _disc;
    private GpuParticles3D _dust;

    // Pilotage
    private float _tiltF, _tiltS;      // inclinaison avant / latérale (radians, + = nez bas / penché vers la gauche)
    private float _yawRate;
    private float _vs;                  // vitesse verticale
    private float _rotorSpeed;          // régime du rotor (0..1)
    private float _load;                // charge du rotor (collectif, vitesse) : -1..1, module la turbine et le battement
    private float _rotorAngle, _tailAngle, _barrelSpin;
    private float _agl;                 // hauteur au-dessus du sol (ou du toit) sous l'appareil

    // Armes
    private float _mgCd, _salvoT, _salvoGap;
    private int _salvoLeft, _salvoSide;
    private float _gunYaw, _gunPitch;   // tourelle de nez (radians, pitch + = vers le bas)
    private float _podYaw, _podPitch;
    private float _spin;                // rotation des canons du minigun (0..1)
    private float _lastShot = -10f, _remoteFire = -10f;

    // Épave
    private bool _falling, _settled;
    private float _fallFrom, _spinWreck;

    // Sons
    private AudioStreamPlayer3D _sRotor, _sTurbine, _sGun, _sMotor;
    private bool _audioReady;
    private Vector3 _lastPos, _estVel;
    private float _warnT, _clock;

    public const float MaxAltitude = 120f;
    public const float MinAltitude = 2f;
    private const float RocketSpeed = 95f;
    private const float SalvoGap = 0.8f;

    // Débattements : tourelle de menton (lacet ±90°, site −80° / +15°) et paniers (lacet ±28°, site −75° / +10°).
    private const float GunYawMax = Mathf.Pi / 2f;
    private static readonly float GunDown = Mathf.DegToRad(80f), GunUp = Mathf.DegToRad(15f);
    private static readonly float PodYawMax = Mathf.DegToRad(28f), PodDown = Mathf.DegToRad(75f), PodUp = Mathf.DegToRad(10f);

    // Dotation par sortie (réarmement posé sur l'aire de départ) : minigun 6 bandes de 300, roquettes 3 pleins de 16 (2 × 8).
    public readonly VehicleWeapon Minigun = new("Minigun M134", "Balles", HudWeaponIcon.Gatling, 300, 1800, 5f);
    public readonly VehicleWeapon Rockets = new("Roquettes air-sol", "Roquettes", HudWeaponIcon.Heli, 16, 48, 7f);

    /// <summary>Test automatisé : « down » (vise sous l'appareil et tire), « ram » (fonce dans un immeuble).</summary>
    public static string Test;

    public static Helicopter Create(World w, int id, Vector3 pos, float yaw)
    {
        var h = new Helicopter { Name = $"Heli{id}" };
        h.InitCommon(w, id, pos, yaw, 1100);
        h.Arms = new[] { h.Minigun, h.Rockets };
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
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.3f, 0.5f), Material = paint2 }, new Vector3(1.65f * s, -0.22f, 0.5f)); // pylône
            // Panier orientable (site et léger lacet) autour de son point d'emport.
            _pods[i] = new Node3D { Position = new Vector3(1.65f * s, -0.45f, 0.5f) };
            _body.AddChild(_pods[i]);
            Add(_pods[i], new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 1.8f, RadialSegments = 12, Material = paint2 }, Vector3.Zero, new Vector3(90, 0, 0));
            Add(_pods[i], new CylinderMesh { TopRadius = 0.24f, BottomRadius = 0.24f, Height = 0.04f, RadialSegments = 12, Material = dark }, new Vector3(0, 0, 0.91f), new Vector3(90, 0, 0));
            _podMuzzles[i] = new Node3D { Position = new Vector3(0, 0, 1.3f) };
            _pods[i].AddChild(_podMuzzles[i]);
            // Patins
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.1f, 4.4f), Material = dark }, new Vector3(0.95f * s, -1.5f, 0.5f));
            Add(_body, new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.5f), Material = dark }, new Vector3(0.95f * s, -1.42f, 2.85f), new Vector3(-30, 0, 0));
            foreach (var z in new[] { 1.6f, -0.6f })
                Add(_body, new BoxMesh { Size = new Vector3(0.08f, 0.85f, 0.08f), Material = dark }, new Vector3(0.78f * s, -1.1f, z), new Vector3(0, 0, -25 * s));
        }
        // Tourelle de menton (minigun) : carénage fixe + tourelle orientable.
        Add(_body, new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.3f, Height = 0.22f, RadialSegments = 12, Material = paint2 }, new Vector3(0, -0.55f, 3.3f));
        _gun = new Node3D { Position = new Vector3(0, -0.75f, 3.3f) };
        _body.AddChild(_gun);
        Add(_gun, new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 10, Rings = 6, Material = dark }, Vector3.Zero);
        Add(_gun, new BoxMesh { Size = new Vector3(0.34f, 0.22f, 0.5f), Material = dark }, new Vector3(0, -0.05f, 0.25f)); // berceau
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

        // Fuselage + disque du rotor : les pales touchent les murs (choc du rotor = dégâts).
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.4f, 2.6f, 8.0f) }, Position = new Vector3(0, 1.35f, 0.4f) });
        AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 5.2f, Height = 0.4f }, Position = new Vector3(0, 3.15f, 0.1f) });
        _prompt.Position = new Vector3(0, 4.4f, 0);
        CastShadows();
        _disc.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        BuildDust();
        BuildAudio();
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
    /// <summary>Une salve de roquettes peut partir maintenant.</summary>
    public bool RocketsAvailable => _salvoLeft == 0 && _salvoGap <= 0 && Rockets.CanFire;
    public override Vector3 SeatPosition => GlobalPosition + Vector3.Up * 2.0f;
    public override float InteractRadius => 7f;
    public override float HitRadius => 3.2f;
    public override float ArmorFactor => 0.7f;
    public override float CameraDistance(bool aiming) => 14f;
    public override float CameraLift => 1.8f;
    public override float AimZoom => 1f;
    public override bool CanExit => _agl < 1.2f && Velocity.Length() < 4f;
    public override float RearmRadius => 14f;
    protected override bool AtRearmPoint => base.AtRearmPoint && _agl < 1.2f;
    public override string DisplayName => "Hélicoptère d'attaque";
    public override string PromptText => "Piloter l'hélicoptère";
    public override string EnterHint =>
        $"Hélicoptère : [{UiKit.KeyName("jump")}] monter, [{UiKit.KeyName("sprint")}] descendre, clic gauche minigun, clic droit roquettes, [{UiKit.KeyName("reload")}] recharger. Munitions limitées : posez-vous sur l'aire de départ pour réarmer.";
    public override string WreckedHint => "Hélicoptère abattu ! Évacuation.";

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
        _vs = Velocity.Y;
        // Chocs (fuselage ou rotor contre un mur, un toit, le sol) : dégâts selon la vitesse d'impact.
        // Seuils : 5 m/s contre un mur, 6 m/s à l'atterrissage ; 10 m/s ≈ 165, 20 m/s ≈ 585, plein régime (36 m/s) = destruction.
        if (!landed) CollisionDamage(p, before, 5f, 6f, 30f, 0.6f, dt);
        CheckWrecked();

        // Charge du rotor (son) : collectif + vitesse.
        _load = Mathf.Lerp(_load, Mathf.Clamp(collective * 0.7f + hv.Length() / 36f * 0.5f, -1f, 1f), Mathf.Clamp(dt * 2f, 0, 1));

        // Inclinaison visuelle en virage.
        var speedK = Mathf.Clamp(hv.Length() / 30f, 0, 1);
        var bank = _yawRate * 0.18f * speedK;
        ApplyPose(_tiltF, _tiltS + bank);

        // Armes
        _mgCd -= dt;
        _salvoGap -= dt;
        AmmoTick(dt);
        if (!paused && Input.IsActionJustPressed("reload")) ManualReload();
        AimWeapons(p, dt);
        var fireHeld = !paused && Input.IsActionPressed("fire");
        // Le minigun doit tourner avant de tirer (~0,2 s), puis ralentit en ~0,9 s.
        _spin = Mathf.MoveToward(_spin, fireHeld && !Minigun.Empty ? 1f : 0f, dt * (fireHeld ? 2.8f : 1.1f));
        if (fireHeld && _spin > 0.55f && _mgCd <= 0)
        {
            if (Minigun.TryFire()) FireMinigun(p);
            else if (Minigun.Empty && Input.IsActionJustPressed("fire")) Sfx.Ui("empty");
        }
        if (!paused && Input.IsActionPressed("aim") && _salvoLeft == 0 && _salvoGap <= 0)
        {
            if (Rockets.CanFire) { _salvoLeft = Mathf.Min(4, Rockets.Mag); _salvoT = 0; }
            else if (Rockets.Empty && Input.IsActionJustPressed("aim")) Sfx.Ui("empty");
        }
        if (_salvoLeft > 0)
        {
            _salvoT -= dt;
            if (_salvoT <= 0)
            {
                if (Rockets.TryFire()) FireRocket(p, _salvoSide);
                _salvoSide = 1 - _salvoSide;
                _salvoT = 0.12f;
                if (--_salvoLeft == 0 || !Rockets.CanFire) { _salvoLeft = 0; _salvoGap = SalvoGap; }
            }
        }

        BroadcastState(dt, new Vector3(_tiltF, Rotation.Y, _tiltS + bank), _rotorSpeed, _gunYaw, _gunPitch, hv.Length());
    }

    // tiltS > 0 : penché vers la gauche (+X local), donc rotation négative autour de Z.
    private void ApplyPose(float tiltF, float tiltS) => _body.Rotation = new Vector3(tiltF, 0, -tiltS);

    // ------------------------------------------------------------------ Armes

    /// <summary>Direction d'un affût (repère du fuselage) pour un lacet et un site (+ = vers le bas).</summary>
    private static Vector3 MountDir(float yaw, float pitch) =>
        new(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

    /// <summary>
    /// Angles d'affût les plus proches d'une direction voulue <paramref name="d"/> (repère du fuselage), dans les limites
    /// de débattement. Deux écritures sont essayées (directe, et « par-dessous » quand la cible est derrière et très bas) :
    /// on garde celle qui pointe le plus près de la cible. Viser à la verticale fait donc pointer l'arme vers le bas.
    /// </summary>
    private static (float yaw, float pitch) SolveMount(Vector3 d, float yawMax, float up, float down)
    {
        var yaw = Mathf.Atan2(d.X, d.Z);
        var pitch = Mathf.Atan2(-d.Y, new Vector2(d.X, d.Z).Length());
        var y1 = Mathf.Clamp(yaw, -yawMax, yawMax);
        var p1 = Mathf.Clamp(pitch, -up, down);
        var y2 = Mathf.Clamp(Mathf.Wrap(yaw + Mathf.Pi, -Mathf.Pi, Mathf.Pi), -yawMax, yawMax);
        var p2 = Mathf.Clamp(Mathf.Pi - pitch, -up, down);
        return MountDir(y1, p1).Dot(d) >= MountDir(y2, p2).Dot(d) ? (y1, p1) : (y2, p2);
    }

    private Vector3 ToBodyDir(Vector3 worldDir) => (_body.GlobalBasis.Inverse() * worldDir).Normalized();

    private Vector3 _gunAimDir = Vector3.Forward, _podAimDir = Vector3.Forward;

    /// <summary>Oriente la tourelle de menton et les paniers vers le point visé (dans leurs limites).</summary>
    private void AimWeapons(Player p, float dt)
    {
        var aim = p.AimPoint(400f, out _);
        // Tourelle de menton.
        var pivot = _gun.GlobalPosition;
        var dLocal = ToBodyDir(aim - pivot);
        var (gy, gp) = SolveMount(dLocal, GunYawMax, GunUp, GunDown);
        _gunYaw = Mathf.LerpAngle(_gunYaw, gy, Mathf.Clamp(dt * 14f, 0, 1));
        _gunPitch = Mathf.Lerp(_gunPitch, gp, Mathf.Clamp(dt * 14f, 0, 1));
        _gun.Rotation = new Vector3(_gunPitch, _gunYaw, 0);
        _gunAimDir = (_body.GlobalBasis * MountDir(gy, gp)).Normalized();
        if (Test == "down" && Engine.GetPhysicsFrames() % 120 == 0)
            GD.Print($"[heli] tourelle de menton : lacet {Mathf.RadToDeg(gy):0}°, site {Mathf.RadToDeg(gp):0}° (vers le bas)");
        // Paniers (même solution pour les deux, depuis le milieu des emports).
        var podMid = (_pods[0].GlobalPosition + _pods[1].GlobalPosition) * 0.5f;
        var (py, pp) = SolveMount(ToBodyDir(aim - podMid), PodYawMax, PodUp, PodDown);
        _podYaw = Mathf.LerpAngle(_podYaw, py, Mathf.Clamp(dt * 8f, 0, 1));
        _podPitch = Mathf.Lerp(_podPitch, pp, Mathf.Clamp(dt * 8f, 0, 1));
        foreach (var pod in _pods) pod.Rotation = new Vector3(_podPitch, _podYaw, 0);
        _podAimAt = aim;
        _podAimDir = (_body.GlobalBasis * MountDir(py, pp)).Normalized();
    }

    private Vector3 _podAimAt;

    private float DamageScale(Player p) => p.DamageMul * (1f + 0.25f * (int)_world.Difficulty);

    /// <summary>Rafale du minigun : part de la bouche de la tourelle de menton, dans l'axe de la tourelle.</summary>
    private void FireMinigun(Player p)
    {
        _mgCd = 0.055f;
        _barrelSpin += 1.2f;
        _lastShot = _clock;
        var from = _gunMuzzle.GlobalPosition;
        var dir = (_gunAimDir + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)) * 0.012f).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * 300f, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var end = from + dir * 300f;
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
        Net.I.SendShotFx(8, from, end);
        if (Test == "down" && GD.Randf() < 0.05f)
            GD.Print($"[heli] minigun : bouche {from.Y - GlobalPosition.Y:0.0} m / centre, dir.y {dir.Y:0.00}, impact {end.Y - GlobalPosition.Y:0.0} m");
    }

    /// <summary>Roquette : part du panier (le fuselage est ignoré par la roquette), vers le point visé dans le débattement des paniers.</summary>
    private void FireRocket(Player p, int side)
    {
        var muzzle = _podMuzzles[side].GlobalPosition;
        // Vise le point visé depuis ce panier ; hors débattement, l'axe borné du panier (toujours vers le bas si l'on vise en dessous).
        var toAim = (_podAimAt - muzzle).Normalized();
        var dir = toAim.Dot(_podAimDir) > 0.995f ? toAim : _podAimDir;
        dir = (dir + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)) * 0.015f).Normalized();
        var from = muzzle + dir * 0.3f;
        var vel = dir * RocketSpeed;
        var pr = Projectile.Spawn(_world, ProjectileKind.Rocket, from, vel, true, 160f * DamageScale(p), 6f);
        pr.IgnoreBody(this);
        if (Test == "down")
        {
            var heli = GlobalPosition;
            pr.Impacted += (at, col) => GD.Print($"[heli] roquette : impact {heli.DistanceTo(at):0.0} m de l'appareil, {heli.Y - at.Y:0.0} m sous le centre, sur {(col as Node)?.Name ?? "-"} ({(col is Vehicle ? "VEHICULE" : "décor/ennemi")})");
        }
        Net.I.SendShotFx(10 + (int)ProjectileKind.Rocket, from, from + vel);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.6f, 0.2f), 0.9f);
        Effects.Smoke(_world, muzzle - dir * 1.5f, 1.2f, 1f);
        Sfx.Ui("heli_rocket", -2f, (float)GD.RandRange(0.95, 1.08));
        p.AddShake(0.08f);
    }

    protected override void OnCollisionImpact(float excess)
    {
        // Alarme du cockpit après un choc.
        if (IsLocalDriver) Sfx.Ui("heli_warn", -6f, 1.15f, 250);
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
        _gunPitch = Mathf.Lerp(_gunPitch, _netC, k);
        _gun.Rotation = new Vector3(_gunPitch, _gunYaw, 0);
        if (Driver != 0) _rotorSpeed = Mathf.Lerp(_rotorSpeed, _netA, k);
        _agl = MeasureAgl();
        var remoteFiring = _clock - _remoteFire < 0.15f;
        _spin = Mathf.MoveToward(_spin, remoteFiring ? 1f : 0f, dt * (remoteFiring ? 2.8f : 1.1f));
        // Charge estimée par la vitesse verticale et horizontale (pas de collectif sur le réseau).
        _load = Mathf.Lerp(_load, Mathf.Clamp(_estVel.Y / 10f * 0.7f + _netSpeed / 36f * 0.5f, -1f, 1f), Mathf.Clamp(dt * 2f, 0, 1));
    }

    public override void OnRemoteFire()
    {
        _remoteFire = _clock;
        _lastShot = _clock;
        _barrelSpin += 1.2f;
    }

    protected override void VisualTick(float dt)
    {
        _clock += dt;
        if (Driver == 0 || Wrecked) _rotorSpeed = Mathf.MoveToward(_rotorSpeed, 0f, dt * (Wrecked ? 0.6f : 0.25f));
        if (IsLocalDriver == false && Wrecked) _agl = MeasureAgl();
        if (Driver == 0 || Wrecked) _spin = Mathf.MoveToward(_spin, 0f, dt * 1.1f);
        _rotorAngle = Mathf.Wrap(_rotorAngle + _rotorSpeed * 26f * dt, 0, Mathf.Tau);
        _tailAngle = Mathf.Wrap(_tailAngle + _rotorSpeed * 60f * dt, 0, Mathf.Tau);
        _rotor.Rotation = new Vector3(0, _rotorAngle, 0);
        _tailRotor.Rotation = new Vector3(_tailAngle, 0, 0);
        _discMat.AlbedoColor = new Color(0.12f, 0.12f, 0.12f, Mathf.Clamp((_rotorSpeed - 0.4f) * 0.45f, 0, 0.25f));
        _barrelSpin = Mathf.MoveToward(_barrelSpin, 0, dt * 3f);
        _gunBarrels.Rotation = new Vector3(0, 0, _gunBarrels.Rotation.Z + (_barrelSpin + _spin * 1.5f) * 30f * dt);

        // Souffle du rotor au sol.
        var dustOn = _rotorSpeed > 0.6f && _agl < 14f && !Wrecked;
        _dust.Emitting = dustOn;
        if (dustOn) _dust.AmountRatio = Mathf.Clamp(1.2f - _agl / 14f, 0.15f, 1f) * _rotorSpeed;

        // Vitesse estimée (Doppler, charge des autres pairs).
        if (dt > 0)
        {
            var v = (GlobalPosition - _lastPos) / dt;
            if (v.LengthSquared() < 80f * 80f) _estVel = _estVel.Lerp(v, Mathf.Clamp(dt * 6f, 0, 1));
            _lastPos = GlobalPosition;
        }
        AudioTick(dt);
    }

    // ------------------------------------------------------------------ Sons

    private AudioStreamPlayer3D LoopPlayer(float unitSize, float maxDistance)
    {
        var p = new AudioStreamPlayer3D
        {
            Bus = "Sfx", UnitSize = unitSize, MaxDistance = maxDistance, VolumeDb = -80f,
            AttenuationFilterCutoffHz = 6000f, AttenuationFilterDb = -18f, Position = new Vector3(0, 2.5f, 0),
        };
        AddChild(p);
        return p;
    }

    private void BuildAudio()
    {
        // Le rotor porte loin (portée ~450 m) ; la turbine et le minigun moins.
        _sRotor = LoopPlayer(28f, 450f);
        _sTurbine = LoopPlayer(14f, 260f);
        _sGun = LoopPlayer(22f, 320f);
        _sMotor = LoopPlayer(8f, 120f);
    }

    private static void Attach(AudioStreamPlayer3D p, string name)
    {
        var s = Sfx.Loop(name);
        if (s == null) return;
        p.Stream = s;
        p.Play((float)GD.RandRange(0, s.GetLength() * 0.9));
    }

    private static void SetVol(AudioStreamPlayer3D p, float gain, float baseDb, float dt)
    {
        var target = gain <= 0.001f ? -80f : baseDb + Mathf.LinearToDb(gain);
        p.VolumeDb = Mathf.MoveToward(p.VolumeDb, target, dt * 90f);
        p.StreamPaused = p.VolumeDb <= -79f;
    }

    /// <summary>
    /// Mixage des boucles : battement des pales et turbine suivent le régime (montée en régime à l'embarquement,
    /// décélération à la descente), la charge (collectif, vitesse) ; Doppler et atténuation pour les appareils
    /// des autres joueurs (le pilote local entend son appareil sans atténuation) ; minigun : moteur qui
    /// monte en régime puis rafale à ~3000 coups/min.
    /// </summary>
    private void AudioTick(float dt)
    {
        if (!_audioReady)
        {
            if (Sfx.Loop("heli_rotor") == null) return;
            Attach(_sRotor, "heli_rotor");
            Attach(_sTurbine, "heli_turbine");
            Attach(_sGun, "heli_minigun");
            Attach(_sMotor, "heli_minigun_motor");
            _audioReady = true;
        }
        var local = IsLocalDriver;
        var model = local ? AudioStreamPlayer3D.AttenuationModelEnum.Disabled : AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
        foreach (var pl in new[] { _sRotor, _sTurbine, _sGun, _sMotor }) pl.AttenuationModel = model;

        // Doppler (autres appareils) : composante de la vitesse vers l'auditeur.
        var doppler = 1f;
        var cam = GetViewport()?.GetCamera3D();
        if (!local && cam != null)
        {
            var toCam = cam.GlobalPosition - GlobalPosition;
            var dist = toCam.Length();
            if (dist > 0.5f)
            {
                var vr = _estVel.Dot(toCam / dist);
                doppler = Mathf.Clamp(343f / (343f - Mathf.Clamp(vr, -120f, 120f)), 0.75f, 1.35f);
            }
        }

        var rs = _rotorSpeed;
        // Rotor : régime + légère montée sous charge ; très bas régime = presque silencieux.
        var rotorGain = Mathf.SmoothStep(0.02f, 0.45f, rs);
        _sRotor.PitchScale = Mathf.Max(0.05f, (0.35f + 0.65f * rs) * (1f + 0.06f * _load) * doppler);
        SetVol(_sRotor, rotorGain * (0.85f + 0.15f * Mathf.Abs(_load)), local ? -7f : 0f, dt);
        // Turbine : démarre avant le rotor (sifflement qui monte), redescend à la coupure.
        var spool = Mathf.Clamp(rs * 1.25f, 0, 1);
        _sTurbine.PitchScale = Mathf.Max(0.05f, (0.3f + 0.7f * spool) * (1f + 0.09f * _load) * doppler);
        SetVol(_sTurbine, Mathf.SmoothStep(0.0f, 0.3f, spool) * (0.8f + 0.2f * Mathf.Max(0, _load)), local ? -15f : -8f, dt);
        // Minigun : moteur selon la rotation, rafale tant que les coups partent.
        _sMotor.PitchScale = Mathf.Max(0.05f, (0.35f + 0.65f * _spin) * doppler);
        SetVol(_sMotor, _spin, local ? -16f : -12f, dt);
        var firing = _clock - _lastShot < 0.09f && _spin > 0.5f;
        _sGun.PitchScale = (0.92f + 0.08f * _spin) * doppler;
        SetVol(_sGun, firing ? 1f : 0f, local ? -5f : 0f, dt * 3f);

        // Alarme du cockpit : blindage faible.
        if (local && !Wrecked && Health < MaxHealth * 0.3f)
        {
            _warnT -= dt;
            if (_warnT <= 0) { _warnT = 1.1f; Sfx.Ui("heli_warn", -8f); }
        }
        else _warnT = 0;
    }

    // ------------------------------------------------------------------ Épave

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
                _home = GlobalPosition;
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
        _spinWreck = Mathf.MoveToward(_spinWreck, 3.5f, dt * 2f);
        Rotation = new Vector3(0, Rotation.Y + _spinWreck * dt, 0);
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
        _spinWreck = _yawRate;
    }
}
