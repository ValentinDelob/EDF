using Godot;

namespace Rempart;

/// <summary>
/// Réseau souterrain : lumière et ambiance. Ni ciel ni soleil : fond noir, ambiance très faible, brouillard sombre et
/// brume volumétrique ; la lumière vient des lampes de secours, des fusées éclairantes, des foyers, des excroissances
/// luminescentes de la ruche, du jour qui tombe du puits de ventilation et de la lampe de casque des soldats.
/// </summary>
public static partial class UndergroundBuilder
{
    /// <summary>Étalonnage appliqué après <see cref="Grade.Apply"/> : obscurité, brouillard sombre, soleil éteint.</summary>
    public static void ApplyGrade(Godot.Environment env, DirectionalLight3D sun)
    {
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = new Color(0.004f, 0.005f, 0.005f);
        env.Sky = null;
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = new Color(0.32f, 0.36f, 0.34f);
        env.AmbientLightEnergy = 0.42f;
        env.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
        env.FogEnabled = true;
        env.FogLightColor = new Color(0.018f, 0.021f, 0.02f);
        env.FogLightEnergy = 1f;
        env.FogDensity = 0.013f;
        env.FogSkyAffect = 1f;
        env.FogAerialPerspective = 0f;
        env.TonemapExposure = 1.6f;
        env.GlowIntensity = 0.75f;
        env.GlowBloom = 0.06f;
        env.GlowHdrThreshold = 0.9f;
        env.AdjustmentSaturation = 0.62f;
        env.AdjustmentContrast = 1.1f;
        sun.LightEnergy = 0f;
        sun.Visible = false;
    }

    /// <summary>Réglages graphiques propres au souterrain (après <see cref="World.ApplyGraphics"/>).</summary>
    public static void ApplyGraphics(Godot.Environment env, DirectionalLight3D sun, int quality)
    {
        // Pas d'illumination globale SDFGI sous terre : coûteuse, elle éclaircit la roche et fuit d'une galerie à l'autre.
        env.SdfgiEnabled = false;
        env.VolumetricFogDensity = 0.022f;
        env.VolumetricFogAlbedo = new Color(0.5f, 0.52f, 0.5f);
        env.VolumetricFogEmission = new Color(0, 0, 0);
        env.VolumetricFogAnisotropy = 0.55f;
        env.VolumetricFogLength = 48f;
        env.VolumetricFogAmbientInject = 0.15f;
        env.SsaoRadius = 2.2f;
        env.SsaoIntensity = 2.4f;
        sun.ShadowEnabled = false;
        sun.Visible = false;
    }

    // ───────────────────────── Station ─────────────────────────

    /// <summary>
    /// Éclairage de la station : lampes de secours ambrées sur les murs latéraux, deux files de luminaires suspendus
    /// au-dessus des bords du quai (presque tous morts, un qui grésille), lueur du tableau d'affichage.
    /// </summary>
    private static void BuildLights(Ctx c)
    {
        for (var x = -72f; x <= 72f; x += 24f)
        {
            WallLamp(c, new Vector3(x, 5f, -35.3f), Vector3.Back, ((int)(x + 72) / 24) % 2 == 0, 2.6f, 18f);
            WallLamp(c, new Vector3(x + 12f, 5f, 27.3f), Vector3.Forward, ((int)(x + 72) / 24) % 3 != 1, 2.6f, 18f);
        }
        var met = Mats.DetailMetal();
        var n = 0;
        foreach (var z in new[] { -22f, 13.5f })
            for (var x = -76f; x <= 76f; x += 9.5f, n++)
            {
                var ceil = CeilAt(c, x, z, out _);
                var y = 7.2f;
                var p = new Vector3(x, y, z);
                var k = c.Dressing.At(p);
                k.Col = new Color(0.12f, 0.12f, 0.12f);
                k.Tube(met, new Vector3(x - 1.2f, y, z), new Vector3(x - 1.2f, ceil + 0.3f, z), 0.02f, 3);
                k.Tube(met, new Vector3(x + 1.2f, y, z), new Vector3(x + 1.2f, ceil + 0.3f, z), 0.02f, 3);
                // Luminaire décroché d'un côté une fois sur sept.
                var hang = n % 7 == 3 ? new Basis(Vector3.Back, 0.5f) : Basis.Identity;
                k.Col = new Color(0.32f, 0.33f, 0.32f);
                k.Box(met, p, new Vector3(2.8f, 0.18f, 0.4f), hang);
                var lit = n % 4 == 1;
                var g = c.Glows.At(p);
                g.Box(lit ? Mats.Glow(Neon, 3.5f) : Mats.Painted(new Color(0.42f, 0.43f, 0.42f), 0.4f), p + hang * new Vector3(0, -0.1f, 0), new Vector3(2.5f, 0.04f, 0.26f), hang);
                if (lit) Omni(c, p + Vector3.Down * 0.6f, Neon, 1.6f, 15f, 0.7f);
                else if (n == 9)
                {
                    var f = new FlickerLight
                    {
                        Position = p + Vector3.Down * 0.6f, LightColor = Neon, LightEnergy = 1.8f, OmniRange = 15f, OmniAttenuation = 1.3f,
                        ShadowEnabled = false, LightVolumetricFogEnergy = 0.7f, DistanceFadeEnabled = true, DistanceFadeBegin = 70f, DistanceFadeLength = 20f,
                    };
                    c.Root.AddChild(f);
                    c.Lights++;
                }
            }
        // Tableau d'affichage des départs (lignes ambrées) au-dessus du quai.
        var bk = c.Dressing.At(new Vector3(-30, 0, -4));
        bk.Col = new Color(0.08f, 0.08f, 0.08f);
        bk.BoxMinMax(met, new Vector3(-33f, 4.6f, -4.3f), new Vector3(-27f, 5.6f, -3.9f));
        bk.Tube(met, new Vector3(-32f, 5.6f, -4.1f), new Vector3(-32f, CeilAt(c, -32f, -4f, out _) + 0.3f, -4.1f), 0.03f, 4);
        bk.Tube(met, new Vector3(-28f, 5.6f, -4.1f), new Vector3(-28f, CeilAt(c, -28f, -4f, out _) + 0.3f, -4.1f), 0.03f, 4);
        var gb = c.Glows.At(new Vector3(-30, 0, -4));
        for (var i = 0; i < 3; i++)
            foreach (var zf in new[] { -4.32f, -3.88f })
                gb.BoxMinMax(Mats.Glow(new Color(1f, 0.55f, 0.1f), 2.2f), new Vector3(-32.6f, 4.8f + i * 0.28f, zf - 0.005f), new Vector3(-32.6f + 3.2f + (i % 2) * 1.6f, 4.92f + i * 0.28f, zf + 0.005f));
    }

    // ───────────────────────── Lampes ─────────────────────────

    /// <summary>Lumière ponctuelle sans ombre, éteinte au-delà de 70 m (fondu).</summary>
    private static OmniLight3D Omni(Ctx c, Vector3 p, Color col, float energy, float range, float fog = 1f, float decay = 1.3f)
    {
        var l = new OmniLight3D
        {
            Position = p, LightColor = col, LightEnergy = energy, OmniRange = range, OmniAttenuation = decay,
            ShadowEnabled = false, LightVolumetricFogEnergy = fog, LightBakeMode = Light3D.BakeMode.Disabled,
            DistanceFadeEnabled = true, DistanceFadeBegin = 70f, DistanceFadeLength = 20f,
        };
        c.Root.AddChild(l);
        c.Lights++;
        return l;
    }

    /// <summary>Couleurs des sources.</summary>
    private static readonly Color Amber = new(1f, 0.62f, 0.28f);
    private static readonly Color Neon = new(0.78f, 0.92f, 0.88f);
    private static readonly Color FlareRed = new(1f, 0.16f, 0.08f);
    private static readonly Color Bio = new(0.42f, 1f, 0.55f);
    private static readonly Color Daylight = new(0.62f, 0.7f, 0.8f);

    /// <summary>
    /// Lampe de secours murale (boîtier, grille, globe ambré allumé) tournée vers <paramref name="inward"/> ;
    /// <paramref name="lit"/> : avec lumière.
    /// </summary>
    private static void WallLamp(Ctx c, Vector3 p, Vector3 inward, bool lit, float energy = 1.6f, float range = 13f)
    {
        var k = c.Dressing.At(p);
        var yaw = Mathf.Atan2(inward.X, inward.Z);
        var keep = k.Xf;
        k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        k.Col = new Color(0.2f, 0.21f, 0.2f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 0, 0.06f), new Vector3(0.42f, 0.28f, 0.12f));
        for (var i = -1; i <= 1; i++) k.Box(Mats.DetailMetal(), new Vector3(i * 0.09f, 0, 0.2f), new Vector3(0.02f, 0.24f, 0.02f));
        k.Xf = keep;
        var g = c.Glows.At(p);
        g.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        g.Box(lit ? Mats.Glow(Amber, 5f) : Mats.Painted(new Color(0.15f, 0.12f, 0.08f), 0.3f), new Vector3(0, 0, 0.16f), new Vector3(0.3f, 0.16f, 0.1f));
        g.Xf = Transform3D.Identity;
        if (lit) Omni(c, p + inward * 0.8f, Amber, energy, range, 1.2f);
    }

    /// <summary>Fusée éclairante rouge posée au sol (bâton incandescent, lueur, fumée rose qui s'étire).</summary>
    private static void Flare(Ctx c, Vector3 p, float yaw)
    {
        var g = c.Glows.At(p);
        g.Xf = new Transform3D(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.12f), p + Vector3.Up * 0.05f);
        g.Cylinder(Mats.Glow(new Color(1f, 0.3f, 0.2f), 9f), Vector3.Zero, 0.025f, 0.025f, 0.3f, 6);
        g.Xf = Transform3D.Identity;
        Omni(c, p + Vector3.Up * 0.4f, FlareRed, 3.2f, 11f, 1.6f);
        c.Root.AddChild(LowSmoke(p + Vector3.Up * 0.2f, 0.5f, new Color(0.55f, 0.18f, 0.16f, 0.28f)));
    }

    /// <summary>Foyer souterrain : flammes et braises de <see cref="RuinFire"/>, fumée basse qui s'étale sous la voûte.</summary>
    private static void Fire(Ctx c, Vector3 p, float scale, bool light = true)
    {
        var f = RuinFire.Create(p, scale, light, true, (int)(Hh(p.X, p.Z, 5) * 1000));
        if (f.GetNodeOrNull("Smoke") is { } smoke) { f.RemoveChild(smoke); smoke.Free(); }
        f.AddChild(LowSmoke(Vector3.Up * (0.8f * scale), scale, new Color(0.06f, 0.055f, 0.05f, 0.55f)));
        c.Root.AddChild(f);
        if (light) c.Lights++;
    }

    /// <summary>Fumée lente qui monte de quelques mètres et s'élargit (elle ne traverse pas la voûte).</summary>
    private static GpuParticles3D LowSmoke(Vector3 p, float scale, Color col)
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(col, 0f));
        grad.SetColor(1, new Color(col.Lightened(0.3f), 0f));
        grad.AddPoint(0.15f, col);
        grad.AddPoint(0.7f, new Color(col.Lightened(0.15f), col.A * 0.5f));
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0.35f));
        curve.AddPoint(new Vector2(1, 1f));
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 20,
            InitialVelocityMin = 0.6f + 0.4f * scale,
            InitialVelocityMax = 1.2f + 0.5f * scale,
            Gravity = new Vector3(0.12f, 0.05f, 0.05f),
            DampingMin = 0.3f,
            DampingMax = 0.6f,
            ScaleMin = 0.8f,
            ScaleMax = 1.3f,
            ScaleCurve = new CurveTexture { Curve = curve },
            ColorRamp = new GradientTexture1D { Gradient = grad },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.4f * scale,
            AngleMin = 0, AngleMax = 360,
            AngularVelocityMin = -6, AngularVelocityMax = 6,
        };
        return new GpuParticles3D
        {
            Name = "LowSmoke",
            Position = p,
            Amount = 16,
            Lifetime = 6f,
            Preprocess = 6f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(2.6f, 2.6f) * Mathf.Max(scale, 0.6f), Material = Mats.SmokePuff(false) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = 120f,
            VisibilityAabb = new Aabb(new Vector3(-8, -1, -8), new Vector3(16, 12, 16)),
        };
    }

    // ───────────────────────── Lampe de casque ─────────────────────────

    /// <summary>
    /// Lampe de casque d'un soldat (carte souterraine) : faisceau étroit et chaud qui suit la visée (ombres pour le
    /// soldat local seulement) et petite lueur d'appoint autour de lui.
    /// </summary>
    public static void AttachTorch(Node3D holder, bool local)
    {
        var spot = new SpotLight3D
        {
            Name = "Torch",
            Position = new Vector3(0.25f, 0.15f, -0.4f),
            LightColor = new Color(1f, 0.93f, 0.82f),
            LightEnergy = local ? 7f : 4f,
            SpotRange = 44f,
            SpotAngle = 27f,
            SpotAngleAttenuation = 1.4f,
            SpotAttenuation = 1.1f,
            ShadowEnabled = local,
            ShadowBias = 0.06f,
            LightVolumetricFogEnergy = 0.35f,
            LightBakeMode = Light3D.BakeMode.Disabled,
        };
        if (!local)
        {
            // Coéquipier : lampe sur le casque du modèle (avant du modèle = +Z), légèrement plongeante.
            spot.Position = new Vector3(0.18f, 1.62f, 0.25f);
            spot.RotationDegrees = new Vector3(-6f, 180f, 0f);
        }
        holder.AddChild(spot);
        if (!local) return;
        holder.AddChild(new OmniLight3D
        {
            Name = "TorchFill",
            Position = new Vector3(0, 0.4f, 1.2f),
            LightColor = new Color(0.9f, 0.9f, 0.85f),
            LightEnergy = 0.35f,
            OmniRange = 7f,
            ShadowEnabled = false,
            LightVolumetricFogEnergy = 0f,
            LightBakeMode = Light3D.BakeMode.Disabled,
        });
    }
}

/// <summary>Néon qui grésille : coupures brèves par salves, rallumages hésitants (réseau souterrain).</summary>
public partial class FlickerLight : OmniLight3D
{
    private float _base = -1f;
    private float _t;
    private float _next = 1.5f;
    private float _burst;

    public override void _Process(double delta)
    {
        if (_base < 0) _base = LightEnergy;
        var dt = (float)delta;
        _t += dt;
        _next -= dt;
        if (_next <= 0)
        {
            _burst = 0.25f + GD.Randf() * 0.9f;
            _next = 1.5f + GD.Randf() * 5f;
        }
        if (_burst > 0)
        {
            _burst -= dt;
            LightEnergy = Mathf.Sin(_t * 61f) + Mathf.Sin(_t * 23f) > 0.3f ? _base : _base * 0.05f;
        }
        else LightEnergy = _base;
    }
}
