using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Caisse de butin au sol, comme dans EDF : modèle Blender propre à chaque type (assets/models/crates : conteneur d'arme vert,
/// caisse d'armure rouge, mallette de soin blanche à croix rouge), éjectée du cadavre, elle retombe, rebondit, bascule et se
/// pose à plat. Voyants et halo au sol pulsés à la teinte du type ; fin faisceau qui ne se voit que de loin (repérage).
/// </summary>
public partial class Crate : Node3D
{
    public int Id;
    public CrateType Type;
    public bool Taken;
    private float _t;
    private Node3D _body;
    private MeshInstance3D _beam, _halo;
    private ShaderMaterial _haloMat;
    private Vector3 _vel;
    private bool _landed;
    private int _bounces;
    // Bascule pendant la chute (roulis, tangage), amortie au sol ; cap fixe tiré à l'apparition.
    private Vector3 _tilt, _tiltVel;
    private float _yaw;
    /// <summary>Hauteur du sol sous la caisse : surface sondée sous son point d'apparition (toit, trottoir, quai, éboulis).</summary>
    public float Floor = 0f;

    private static readonly Dictionary<CrateType, PackedScene> Scenes = new();

    private static string ModelName(CrateType t) => t switch
    {
        CrateType.Weapon => "crate_weapon",
        CrateType.Armor => "crate_armor",
        _ => "crate_health",
    };

    /// <summary>Modèle Blender du type, aux matières du jeu (voyants à la teinte de la caisse) ; null si absent.</summary>
    public static Node3D Model(CrateType type)
    {
        if (!Scenes.TryGetValue(type, out var scene))
        {
            var path = $"res://assets/models/crates/{ModelName(type)}.glb";
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            Scenes[type] = scene;
        }
        if (scene == null) return null;
        var model = scene.Instantiate<Node3D>();
        SoldierModel.ApplyMats(model, Crates.Paint[(int)type], Crates.Accent[(int)type], null,
            Mats.GunMask($"res://assets/models/crates/{ModelName(type)}.glb"));
        var glow = Mats.Glow(Crates.Tint(type), 4f);
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                if (mi.Mesh.SurfaceGetMaterial(i)?.ResourceName == "Glow") mi.SetSurfaceOverrideMaterial(i, glow);
        }
        return model;
    }

    public static Crate Create(int id, CrateType type, Vector3 pos)
    {
        var c = new Crate { Id = id, Type = type, Name = $"Crate{id}" };
        c._body = new Node3D();
        c.AddChild(c._body);
        var color = Crates.Tint(type);
        var model = Model(type);
        if (model != null) c._body.AddChild(model);
        else c._body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.5f, 0.8f), Material = Mats.Painted(Crates.Paint[(int)type], 0.5f, 0.2f) },
            Position = new Vector3(0, 0.25f, 0),
        });
        // Halo pulsé au sol (disque additif) et fin faisceau de repérage, qui s'efface de près.
        c._haloMat = HaloMat(color);
        c._halo = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(2.6f, 2.6f), Material = c._haloMat },
            Position = new Vector3(0, 0.03f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        c.AddChild(c._halo);
        c._beam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.09f, Height = 6f, RadialSegments = 8, CapTop = false, CapBottom = false, Material = BeamMat(color) },
            Position = new Vector3(0, 3.2f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        c.AddChild(c._beam);
        c.Position = pos + Vector3.Up * 1.2f;
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)(id * 7919 + 13);
        c._vel = new Vector3(rng.RandfRange(-2.5f, 2.5f), rng.RandfRange(5.5f, 7.5f), rng.RandfRange(-2.5f, 2.5f));
        c._yaw = rng.RandfRange(-Mathf.Pi, Mathf.Pi);
        c._tiltVel = new Vector3(rng.RandfRange(-5f, 5f), 0, rng.RandfRange(-5f, 5f));
        return c;
    }

    private static StandardMaterial3D BeamMat(Color c) => new()
    {
        AlbedoColor = new Color(c, 0.22f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
        DistanceFadeMinDistance = 9f,
        DistanceFadeMaxDistance = 26f,
    };

    private static Shader _haloShader;

    private static ShaderMaterial HaloMat(Color c)
    {
        _haloShader ??= new Shader
        {
            Code = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec3 tint : source_color;
uniform float pulse = 1.0;
void fragment() {
    float r = length(UV - 0.5) * 2.0;
    // Anneau net qui s'élargit avec la pulsation, sur un voile doux.
    float ring = smoothstep(0.08, 0.0, abs(r - mix(0.55, 0.9, fract(TIME * 0.6))));
    float glow = (1.0 - smoothstep(0.0, 1.0, r)) * 0.35;
    ALBEDO = tint * (glow * pulse + ring * (1.0 - fract(TIME * 0.6)) * 0.9);
}",
        };
        var m = new ShaderMaterial { Shader = _haloShader };
        m.SetShaderParameter("tint", c);
        return m;
    }

    /// <summary>Ramassage (tous les pairs) : éclat à la teinte de la caisse et anneau qui monte en s'élargissant.</summary>
    public static void PickupFx(World world, Vector3 pos, CrateType type)
    {
        var color = Crates.Tint(type);
        MuzzleFx.Spawn(world.FxRoot, pos + Vector3.Up * 0.5f, color, 1.4f, null);
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1f, Rings = 32, RingSegments = 6 },
            MaterialOverride = Mats.Glow(color, 4f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = Vector3.One * 0.4f,
        };
        world.FxRoot.AddChild(ring);
        ring.GlobalPosition = pos + Vector3.Up * 0.15f;
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "scale", new Vector3(1.5f, 0.6f, 1.5f), 0.4f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(ring, "global_position", pos + Vector3.Up * 1.8f, 0.45f);
        tw.Chain().TweenProperty(ring, "scale", new Vector3(1.7f, 0.01f, 1.7f), 0.12f);
        tw.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>Glisse vers le joueur (effet d'aimant local).</summary>
    public void Attract(Vector3 target, float dt)
    {
        if (!_landed) return;
        var to = target - Position;
        to.Y = 0;
        var d = to.Length();
        if (d < 0.1f) return;
        var np = Position + to / d * Mathf.Min(d, (14f - d) * dt);
        // Glisse en suivant le relief ; sur un toit ou un quai, elle reste à sa hauteur de pose.
        np.Y = Mathf.Max(World.GroundAt(np.X, np.Z), Floor);
        Position = np;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        if (!_landed)
        {
            _vel += Vector3.Down * 20f * dt;
            var p = Position + _vel * dt;
            // Sol de la carte (relief de la vallée) ; 0 sur les cartes plates.
            var floor = Mathf.Max(World.GroundAt(p.X, p.Z), Floor);
            if (p.Y <= floor)
            {
                p.Y = floor;
                // Rebonds amortis, puis la caisse se pose.
                if (_bounces < 2 && _vel.Y < -3f)
                {
                    _vel = new Vector3(_vel.X * 0.45f, -_vel.Y * 0.32f, _vel.Z * 0.45f);
                    _tiltVel = -_tiltVel * 0.5f + new Vector3(_vel.Z, 0, -_vel.X) * 0.8f;
                    _bounces++;
                }
                else _landed = true;
            }
            Position = p;
        }
        // Bascule : libre en l'air, ressort amorti vers la position à plat une fois au sol.
        if (!_landed) _tilt = (_tilt + _tiltVel * dt).Clamp(new Vector3(-0.7f, 0, -0.7f), new Vector3(0.7f, 0, 0.7f));
        else
        {
            _tiltVel += (-_tilt * 90f - _tiltVel * 12f) * dt;
            _tilt += _tiltVel * dt;
        }
        _body.Rotation = new Vector3(_tilt.X, _yaw, _tilt.Z);
        _halo.Visible = _landed;
        _haloMat.SetShaderParameter("pulse", 0.75f + 0.25f * Mathf.Sin(_t * 3f));
        _beam.Visible = _landed;
    }
}
