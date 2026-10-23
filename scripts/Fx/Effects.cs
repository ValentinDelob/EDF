using Godot;

namespace Rempart;

/// <summary>Effets visuels éphémères (traçantes, explosions, poussière, débris, impacts).</summary>
public static class Effects
{
    private static GradientTexture2D _soft;
    private static StandardMaterial3D _smokeMat, _fireMat, _sparkMat, _dustMat;

    private static GradientTexture2D Soft
    {
        get
        {
            if (_soft != null) return _soft;
            var g = new Gradient();
            g.SetColor(0, new Color(1, 1, 1, 1));
            g.SetColor(1, new Color(1, 1, 1, 0));
            g.AddPoint(0.5f, new Color(1, 1, 1, 0.55f));
            _soft = new GradientTexture2D
            {
                Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
            };
            return _soft;
        }
    }

    private static StandardMaterial3D Billboard(Color c, bool additive, bool unshaded)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = Soft,
            AlbedoColor = c,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            ProximityFadeEnabled = true,
            ProximityFadeDistance = 1.5f,
        };
    }

    private static StandardMaterial3D SmokeMat => _smokeMat ??= Billboard(new Color(0.35f, 0.33f, 0.31f, 0.8f), false, false);
    private static StandardMaterial3D DustMat => _dustMat ??= Billboard(new Color(0.55f, 0.52f, 0.47f, 0.75f), false, false);
    private static StandardMaterial3D FireMat => _fireMat ??= Billboard(new Color(1f, 0.55f, 0.15f, 1f), true, true);
    private static StandardMaterial3D SparkMat => _sparkMat ??= Billboard(new Color(1f, 0.8f, 0.4f, 1f), true, true);

    private static void AutoFree(Node n, float seconds)
    {
        var t = n.GetTree().CreateTimer(seconds, false);
        t.Timeout += () => { if (GodotObject.IsInstanceValid(n)) n.QueueFree(); };
    }

    private static GpuParticles3D Burst(Node3D parent, Vector3 pos, int amount, float life, Material drawMat, float size,
        Vector3 dir, float spread, float vMin, float vMax, float gravity, Gradient colors, float sizeEnd = 2f, float damping = 1f, Vector3? box = null)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = dir,
            Spread = spread,
            InitialVelocityMin = vMin,
            InitialVelocityMax = vMax,
            Gravity = new Vector3(0, gravity, 0),
            DampingMin = damping,
            DampingMax = damping * 1.5f,
            ScaleMin = 0.7f,
            ScaleMax = 1.3f,
            ScaleCurve = new CurveTexture { Curve = MakeCurve(1f, sizeEnd) },
            ColorRamp = new GradientTexture1D { Gradient = colors },
            AngleMin = 0, AngleMax = 360,
        };
        if (box.HasValue)
        {
            pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            pm.EmissionBoxExtents = box.Value;
        }
        var quad = new QuadMesh { Size = new Vector2(size, size), Material = drawMat };
        var p = new GpuParticles3D
        {
            Amount = amount,
            Lifetime = life,
            OneShot = true,
            Explosiveness = 0.95f,
            ProcessMaterial = pm,
            DrawPass1 = quad,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-60, -10, -60), new Vector3(120, 80, 120)),
        };
        parent.AddChild(p);
        p.GlobalPosition = pos;
        p.Emitting = true;
        AutoFree(p, life + 0.5f);
        return p;
    }

    private static Curve MakeCurve(float a, float b)
    {
        var c = new Curve();
        c.MaxValue = Mathf.Max(Mathf.Max(a, b), 1f);
        c.AddPoint(new Vector2(0, a));
        c.AddPoint(new Vector2(1, b));
        return c;
    }

    private static Gradient Grad(params Color[] colors)
    {
        var g = new Gradient();
        g.RemovePoint(1);
        g.SetColor(0, colors[0]);
        for (var i = 1; i < colors.Length; i++) g.AddPoint(i / (float)(colors.Length - 1), colors[i]);
        return g;
    }

    public static void Tracer(Node3D parent, Vector3 from, Vector3 to, Color color, float width = 0.06f, float life = 0.07f)
    {
        var len = from.DistanceTo(to);
        if (len < 0.5f) return;
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, width, len) },
            MaterialOverride = Mats.Glow(color, 6f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);
        mesh.GlobalPosition = (from + to) / 2;
        mesh.LookAt(to, Mathf.Abs((to - from).Normalized().Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up);
        var tw = mesh.CreateTween();
        tw.TweenProperty(mesh, "scale", new Vector3(0.1f, 0.1f, 1f), life);
        tw.TweenCallback(Callable.From(mesh.QueueFree));
    }

    public static void MuzzleFlash(Node3D parent, Vector3 pos, Color color, float size = 0.5f)
    {
        var m = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = size * 0.5f, Height = size, RadialSegments = 8, Rings = 4 },
            MaterialOverride = Mats.Glow(color, 8f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(m);
        m.GlobalPosition = pos;
        var light = new OmniLight3D { LightColor = color, LightEnergy = 2.5f, OmniRange = 6f };
        parent.AddChild(light);
        light.GlobalPosition = pos;
        var tw = m.CreateTween();
        tw.TweenProperty(m, "scale", Vector3.One * 0.1f, 0.05f);
        tw.TweenCallback(Callable.From(() => { m.QueueFree(); light.QueueFree(); }));
    }

    public static void Explosion(World world, Vector3 pos, float radius, int kind)
    {
        var root = world.FxRoot;
        var big = radius > 6;
        var color = kind == 2 ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.55f, 0.18f);
        // Boule de feu
        var ball = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = Mats.Glow(color, 7f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(ball);
        ball.GlobalPosition = pos;
        ball.Scale = Vector3.One * radius * 0.2f;
        var tw = ball.CreateTween();
        tw.TweenProperty(ball, "scale", Vector3.One * radius * 0.8f, 0.12f).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ball, "scale", Vector3.One * 0.01f, 0.25f);
        tw.TweenCallback(Callable.From(ball.QueueFree));

        var light = new OmniLight3D { LightColor = color, LightEnergy = 12f, OmniRange = radius * 4f, ShadowEnabled = false };
        root.AddChild(light);
        light.GlobalPosition = pos + Vector3.Up;
        var lt = light.CreateTween();
        lt.TweenProperty(light, "light_energy", 0f, 0.5f);
        lt.TweenCallback(Callable.From(light.QueueFree));

        Burst(root, pos, big ? 24 : 12, 0.6f, FireMat, radius * 0.5f, Vector3.Up, 90, radius * 1.5f, radius * 3f, 2f,
            Grad(new Color(1, 0.9f, 0.6f, 1), new Color(1, 0.4f, 0.1f, 0.8f), new Color(0.2f, 0.1f, 0.05f, 0)), 2.2f, 4f);
        Burst(root, pos, big ? 28 : 14, big ? 4.5f : 3f, SmokeMat, radius * 0.7f, Vector3.Up, 70, radius * 0.4f, radius * 1.2f, 1.2f,
            Grad(new Color(0.25f, 0.23f, 0.21f, 0.9f), new Color(0.4f, 0.38f, 0.36f, 0.6f), new Color(0.5f, 0.5f, 0.5f, 0)), 3.2f, 1.2f);
        Burst(root, pos, 30, 1.2f, SparkMat, 0.25f, Vector3.Up, 80, radius * 2f, radius * 5f, -14f,
            Grad(new Color(1, 0.9f, 0.5f, 1), new Color(1, 0.4f, 0.1f, 0)), 0.3f, 0.5f);
        // Marque au sol
        if (pos.Y < 1.5f)
        {
            var decal = new Decal
            {
                Size = new Vector3(radius * 1.4f, 2f, radius * 1.4f),
                TextureAlbedo = Soft,
                Modulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
                CullMask = 1,
            };
            root.AddChild(decal);
            decal.GlobalPosition = new Vector3(pos.X, 0.2f, pos.Z);
            var dt = decal.CreateTween();
            dt.TweenInterval(20f);
            dt.TweenProperty(decal, "modulate:a", 0f, 4f);
            dt.TweenCallback(Callable.From(decal.QueueFree));
        }
        Sfx.At(big ? "explosion" : "explosion_small", pos, big ? 6f : 2f, (float)GD.RandRange(0.9, 1.1), 40);
        world.ShakeCamera(pos, big ? 0.6f : 0.3f, radius * 8f);
    }

    public static void Dust(World world, Vector3 pos, Vector3 size)
    {
        var ext = new Vector3(size.X / 2, 1, size.Z / 2);
        Burst(world.FxRoot, pos + Vector3.Up * 2, 60, 7f, DustMat, Mathf.Max(size.X, size.Z) * 0.45f, Vector3.Up, 60, 2f, 7f, 0.4f,
            Grad(new Color(0.5f, 0.48f, 0.44f, 0f), new Color(0.55f, 0.52f, 0.47f, 0.85f), new Color(0.6f, 0.58f, 0.55f, 0)), 2.5f, 0.6f, ext);
        Burst(world.FxRoot, pos + Vector3.Up * size.Y * 0.5f, 40, 5f, DustMat, Mathf.Max(size.X, size.Z) * 0.35f, Vector3.Down, 40, 1f, 4f, -2f,
            Grad(new Color(0.5f, 0.48f, 0.44f, 0.8f), new Color(0.6f, 0.58f, 0.55f, 0)), 2f, 0.4f, new Vector3(size.X / 2, size.Y / 2, size.Z / 2));
    }

    public static void Debris(World world, Vector3 pos, Vector3 size, Material mat)
    {
        var count = Mathf.Clamp((int)(size.X * size.Z * size.Y / 600f), 6, 18);
        var rng = new RandomNumberGenerator();
        for (var i = 0; i < count; i++)
        {
            var s = new Vector3(rng.RandfRange(1, 3.5f), rng.RandfRange(0.8f, 2.5f), rng.RandfRange(1, 3.5f));
            var body = new RigidBody3D
            {
                CollisionLayer = 1 << 4,
                CollisionMask = 1,
                Mass = s.X * s.Y * s.Z,
            };
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = s }, MaterialOverride = mat });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = s } });
            world.FxRoot.AddChild(body);
            body.GlobalPosition = pos + new Vector3(rng.RandfRange(-size.X / 2, size.X / 2), rng.RandfRange(size.Y * 0.3f, size.Y), rng.RandfRange(-size.Z / 2, size.Z / 2));
            body.LinearVelocity = new Vector3(rng.RandfRange(-6, 6), rng.RandfRange(-2, 4), rng.RandfRange(-6, 6));
            body.AngularVelocity = new Vector3(rng.RandfRange(-3, 3), rng.RandfRange(-3, 3), rng.RandfRange(-3, 3));
            var t = body.CreateTween();
            t.TweenInterval(9f);
            t.TweenProperty(body, "scale", Vector3.One * 0.01f, 1.5f);
            t.TweenCallback(Callable.From(body.QueueFree));
        }
    }

    /// <summary>Giclée d'insecte (hémolymphe jaune-vert) à l'impact ou à la mort.</summary>
    public static void Goo(World world, Vector3 pos, Color color, float scale = 1f, int amount = 10)
    {
        var mat = (StandardMaterial3D)SparkMat.Duplicate();
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
        Burst(world.FxRoot, pos, amount, 0.7f, mat, 0.35f * scale, Vector3.Up, 70, 3f * scale, 7f * scale, -18f,
            Grad(color, new Color(color, 0)), 1.5f, 0.3f);
    }

    public static void Sparks(World world, Vector3 pos, Vector3 normal)
    {
        Burst(world.FxRoot, pos, 6, 0.35f, SparkMat, 0.12f, normal, 50, 4f, 9f, -15f,
            Grad(new Color(1, 0.9f, 0.6f, 1), new Color(1, 0.5f, 0.2f, 0)), 0.5f, 0.2f);
        Burst(world.FxRoot, pos, 3, 1.2f, DustMat, 0.6f, normal, 30, 0.5f, 1.5f, 0.5f,
            Grad(new Color(0.5f, 0.48f, 0.44f, 0.6f), new Color(0.6f, 0.58f, 0.55f, 0)), 2.5f, 1f);
    }

    public static void Smoke(World world, Vector3 pos, float size, float life)
    {
        Burst(world.FxRoot, pos, 8, life, SmokeMat, size, Vector3.Up, 30, 0.5f, 2f, 1f,
            Grad(new Color(0.3f, 0.3f, 0.3f, 0.7f), new Color(0.5f, 0.5f, 0.5f, 0)), 2.5f, 0.5f);
    }

    public static void Emerge(World world, Vector3 pos, float size)
    {
        Burst(world.FxRoot, pos, 16, 2.5f, DustMat, size * 2.5f, Vector3.Up, 50, 2f, 6f, -2f,
            Grad(new Color(0.42f, 0.36f, 0.28f, 0.9f), new Color(0.5f, 0.45f, 0.38f, 0)), 2f, 1.2f, new Vector3(size, 0.3f, size));
    }

    public static void Beam(World world, Vector3 from, Vector3 to, Color color, float width)
    {
        Tracer(world.FxRoot, from, to, color, width, 0.05f);
    }

    public static void Ring(World world, Vector3 pos, float radius, Color color, float life)
    {
        var m = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius - 0.15f, OuterRadius = radius, Rings = 32, RingSegments = 6 },
            MaterialOverride = Mats.Glow(color, 3f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        world.FxRoot.AddChild(m);
        m.GlobalPosition = pos + Vector3.Up * 0.2f;
        var tw = m.CreateTween();
        tw.TweenProperty(m, "scale", Vector3.One * 1.15f, life);
        tw.TweenCallback(Callable.From(m.QueueFree));
    }
}
