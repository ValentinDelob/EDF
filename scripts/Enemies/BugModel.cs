using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Modèle d'insecte géant construit à partir de primitives, avec démarche procédurale.</summary>
public partial class BugModel : Node3D
{
    private class Leg
    {
        public Node3D Hip;
        public Node3D Knee;
        public float Phase;
        public float Side;
        public float BaseYaw;
    }

    private readonly List<Leg> _legs = new();
    private readonly List<Node3D> _wings = new();
    private Node3D _head, _gaster, _mandL, _mandR, _bodyPivot;
    private float _gait;
    private float _t;
    private bool _flying;
    public EnemyType Type;

    public static BugModel Create(EnemyType type)
    {
        var m = new BugModel { Type = type };
        switch (type)
        {
            case EnemyType.BlackAnt: m.BuildAnt(new Color(0.07f, 0.065f, 0.06f), 1f, 1f); break;
            case EnemyType.RedAnt: m.BuildAnt(new Color(0.45f, 0.09f, 0.05f), 1.15f, 1.05f); break;
            case EnemyType.AcidAnt: m.BuildAnt(new Color(0.42f, 0.4f, 0.08f), 1f, 1f, acid: true); break;
            case EnemyType.Scavenger: m.BuildAnt(new Color(0.48f, 0.38f, 0.27f), 0.85f, 1.35f); break;
            case EnemyType.Queen: m.BuildAnt(new Color(0.25f, 0.06f, 0.05f), 3.8f, 1.1f, queen: true); break;
            case EnemyType.Wasp: m.BuildWasp(); break;
            case EnemyType.Nest: m.BuildNest(); break;
        }
        return m;
    }

    private static MeshInstance3D Ellipsoid(Material mat, Vector3 scale, Vector3 pos, int seg = 18)
    {
        return new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = seg, Rings = seg / 2, Material = mat },
            Scale = scale,
            Position = pos,
        };
    }

    private static MeshInstance3D Segment(Material mat, float length, float r0, float r1)
    {
        var m = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = r1, BottomRadius = r0, Height = length, RadialSegments = 7, Rings = 1, Material = mat },
            // Niveau de détail : les segments fins disparaissent au loin et ne projettent pas d'ombre.
            VisibilityRangeEnd = 90f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        m.Position = new Vector3(0, -length / 2, 0);
        return m;
    }

    private void BuildAnt(Color color, float size, float legScale, bool acid = false, bool queen = false)
    {
        var mat = Mats.Chitin(color);
        var dark = Mats.Chitin(color.Darkened(0.5f));
        var eye = Mats.Painted(new Color(0.02f, 0.02f, 0.02f), 0.05f, 0.3f);
        Scale = Vector3.One * size;
        _bodyPivot = new Node3D { Position = new Vector3(0, 1.05f, 0) };
        AddChild(_bodyPivot);

        _bodyPivot.AddChild(Ellipsoid(mat, new Vector3(0.85f, 0.7f, 1.3f), new Vector3(0, 0, 0.1f)));        // thorax
        _bodyPivot.AddChild(Ellipsoid(dark, new Vector3(0.35f, 0.35f, 0.5f), new Vector3(0, 0, -0.75f)));     // pétiole
        _gaster = new Node3D { Position = new Vector3(0, 0.15f, -1.0f) };
        _bodyPivot.AddChild(_gaster);
        var gs = queen ? new Vector3(1.6f, 1.3f, 2.6f) : new Vector3(1.35f, 1.1f, 1.85f);
        _gaster.AddChild(Ellipsoid(mat, gs, new Vector3(0, 0.15f, -gs.Z * 0.45f), 22));
        if (acid)
            _gaster.AddChild(Ellipsoid(Mats.Glow(new Color(0.75f, 0.95f, 0.15f), 1.2f), new Vector3(0.5f, 0.4f, 0.5f), new Vector3(0, 0.0f, -gs.Z * 0.9f)));
        _head = new Node3D { Position = new Vector3(0, 0.15f, 0.95f) };
        _bodyPivot.AddChild(_head);
        _head.AddChild(Ellipsoid(mat, new Vector3(0.95f, 0.8f, 0.9f), new Vector3(0, 0, 0.35f)));
        _head.AddChild(Ellipsoid(eye, new Vector3(0.22f, 0.3f, 0.28f), new Vector3(0.42f, 0.12f, 0.45f), 10));
        _head.AddChild(Ellipsoid(eye, new Vector3(0.22f, 0.3f, 0.28f), new Vector3(-0.42f, 0.12f, 0.45f), 10));
        _mandL = new Node3D { Position = new Vector3(0.22f, -0.15f, 0.75f) };
        _mandR = new Node3D { Position = new Vector3(-0.22f, -0.15f, 0.75f) };
        _head.AddChild(_mandL);
        _head.AddChild(_mandR);
        foreach (var (mand, s) in new[] { (_mandL, 1f), (_mandR, -1f) })
        {
            var blade = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(0.12f, 0.12f, 0.75f), Material = dark },
                Position = new Vector3(-0.1f * s, 0, 0.3f),
                RotationDegrees = new Vector3(90, 0, 0),
            };
            mand.AddChild(blade);
        }
        // Antennes
        foreach (var s in new[] { 1f, -1f })
        {
            var ant = new Node3D { Position = new Vector3(0.2f * s, 0.3f, 0.6f), RotationDegrees = new Vector3(-50, 25 * s, 0) };
            _head.AddChild(ant);
            var seg = Segment(dark, 1.1f, 0.04f, 0.03f);
            seg.Position = new Vector3(0, 0.55f, 0);
            ant.AddChild(seg);
            var tip = new Node3D { Position = new Vector3(0, 1.1f, 0), RotationDegrees = new Vector3(70, 0, 0) };
            ant.AddChild(tip);
            var seg2 = Segment(dark, 0.8f, 0.03f, 0.02f);
            seg2.Position = new Vector3(0, 0.4f, 0);
            tip.AddChild(seg2);
        }
        if (queen)
        {
            foreach (var s in new[] { 1f, -1f })
            {
                var wing = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.02f, 0.8f), Material = WingMat() },
                    Position = new Vector3(0.9f * s, 0.45f, -0.2f),
                    RotationDegrees = new Vector3(0, -30 * s, 10 * s),
                };
                _bodyPivot.AddChild(wing);
            }
        }
        // Pattes (3 paires) attachées au thorax.
        for (var i = 0; i < 3; i++)
        foreach (var side in new[] { 1f, -1f })
        {
            var z = 0.45f - i * 0.45f;
            var yaw = (35f - i * 35f) * side;
            var hip = new Node3D { Position = new Vector3(0.35f * side, -0.1f, z), RotationDegrees = new Vector3(0, yaw, 0) };
            _bodyPivot.AddChild(hip);
            var upperLen = 0.95f * legScale;
            var lowerLen = 1.6f * legScale;
            var upperPivot = new Node3D { RotationDegrees = new Vector3(0, 0, -70 * side) };
            hip.AddChild(upperPivot);
            var upper = Segment(mat, upperLen, 0.09f, 0.07f);
            upper.Position = new Vector3(0, upperLen / 2, 0);
            upper.RotationDegrees = Vector3.Zero;
            upperPivot.AddChild(upper);
            // Genou : le tibia repart vers l'extérieur et le bas jusqu'au sol.
            var knee = new Node3D { Position = new Vector3(0, upperLen, 0), RotationDegrees = new Vector3(0, 0, -85 * side) };
            upperPivot.AddChild(knee);
            var lower = Segment(dark, lowerLen, 0.07f, 0.03f);
            lower.Position = new Vector3(0, lowerLen / 2, 0);
            knee.AddChild(lower);
            _legs.Add(new Leg { Hip = hip, Knee = upperPivot, Phase = (i % 2 == 0 ? 0 : Mathf.Pi) + (side > 0 ? 0 : Mathf.Pi), Side = side, BaseYaw = yaw });
        }
        // Ombres seulement pour les grosses pièces (corps), pas pour les pattes ni les antennes.
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (mi.Mesh is SphereMesh && mi.Scale.X > 0.5f) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            else if (mi.Mesh is not CylinderMesh) { mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; mi.VisibilityRangeEnd = 60f; }
        }
    }

    private static StandardMaterial3D _wingMat;

    private static StandardMaterial3D WingMat() => _wingMat ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.75f, 0.8f, 0.85f, 0.35f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = 0.1f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RimEnabled = true,
        Rim = 0.6f,
    };

    private void BuildWasp()
    {
        _flying = true;
        var yellow = Mats.Chitin(new Color(0.8f, 0.6f, 0.08f));
        var black = Mats.Chitin(new Color(0.06f, 0.05f, 0.04f));
        Scale = Vector3.One * 0.9f;
        _bodyPivot = new Node3D();
        AddChild(_bodyPivot);
        _bodyPivot.AddChild(Ellipsoid(black, new Vector3(0.9f, 0.85f, 1.1f), Vector3.Zero));
        _head = new Node3D { Position = new Vector3(0, 0.05f, 0.75f) };
        _bodyPivot.AddChild(_head);
        _head.AddChild(Ellipsoid(yellow, new Vector3(0.8f, 0.75f, 0.6f), Vector3.Zero));
        _head.AddChild(Ellipsoid(Mats.Painted(new Color(0.15f, 0.05f, 0.02f), 0.05f), new Vector3(0.3f, 0.45f, 0.3f), new Vector3(0.32f, 0.08f, 0.15f), 10));
        _head.AddChild(Ellipsoid(Mats.Painted(new Color(0.15f, 0.05f, 0.02f), 0.05f), new Vector3(0.3f, 0.45f, 0.3f), new Vector3(-0.32f, 0.08f, 0.15f), 10));
        _gaster = new Node3D { Position = new Vector3(0, -0.1f, -0.7f), RotationDegrees = new Vector3(-20, 0, 0) };
        _bodyPivot.AddChild(_gaster);
        for (var i = 0; i < 4; i++)
        {
            var r = 1.05f - i * 0.18f;
            _gaster.AddChild(Ellipsoid(i % 2 == 0 ? yellow : black, new Vector3(r, r * 0.9f, 0.7f), new Vector3(0, 0, -0.45f * i - 0.2f)));
        }
        _gaster.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.08f, Height = 0.6f, Material = black },
            Position = new Vector3(0, 0, -2.05f),
            RotationDegrees = new Vector3(-90, 0, 0),
        });
        foreach (var s in new[] { 1f, -1f })
        for (var k = 0; k < 2; k++)
        {
            var hinge = new Node3D { Position = new Vector3(0.3f * s, 0.4f, 0.1f - k * 0.35f) };
            _bodyPivot.AddChild(hinge);
            hinge.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(2.2f - k * 0.5f, 0.02f, 0.7f), Material = WingMat() },
                Position = new Vector3(1.1f * s - 0.25f * k * s, 0, -0.1f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            _wings.Add(hinge);
        }
        for (var i = 0; i < 3; i++)
        foreach (var side in new[] { 1f, -1f })
        {
            var hip = new Node3D { Position = new Vector3(0.3f * side, -0.35f, 0.25f - i * 0.25f), RotationDegrees = new Vector3(30, 0, 30 * side) };
            _bodyPivot.AddChild(hip);
            var seg = Segment(black, 1.1f, 0.05f, 0.025f);
            hip.AddChild(seg);
        }
    }

    private void BuildNest()
    {
        var dirt = Mats.Dirt();
        var mound = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.5f, BottomRadius = 9f, Height = 4.5f, RadialSegments = 14, Rings = 3, Material = dirt },
            Position = new Vector3(0, 2.2f, 0),
        };
        AddChild(mound);
        var rng = new RandomNumberGenerator { Seed = 99 };
        for (var i = 0; i < 9; i++)
        {
            var a = i / 9f * Mathf.Tau;
            AddChild(Ellipsoid(dirt, new Vector3(rng.RandfRange(2, 4), rng.RandfRange(1, 2.5f), rng.RandfRange(2, 4)),
                new Vector3(Mathf.Cos(a) * 6.5f, 0.5f, Mathf.Sin(a) * 6.5f), 10));
        }
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.1f, BottomRadius = 1.4f, Height = 0.5f, Material = Mats.Painted(new Color(0.01f, 0.01f, 0.01f), 1f) },
            Position = new Vector3(0, 4.3f, 0),
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 2.0f, OuterRadius = 2.35f, Material = Mats.Glow(new Color(0.65f, 0.85f, 0.2f), 1.5f) },
            Position = new Vector3(0, 4.45f, 0),
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 5.5f, 0), LightColor = new Color(0.65f, 0.9f, 0.3f), LightEnergy = 1.5f, OmniRange = 14 });
    }

    private static StandardMaterial3D _flashMat;
    private List<GeometryInstance3D> _meshes;
    private float _flashT = -1f;

    /// <summary>Bref flash blanc à l'impact (retour visuel des tirs).</summary>
    public void Flash()
    {
        _flashMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.6f, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        if (_meshes == null)
        {
            _meshes = new List<GeometryInstance3D>();
            foreach (var n in FindChildren("*", "MeshInstance3D", true, false)) _meshes.Add((GeometryInstance3D)n);
        }
        // Pas plus d'un flash toutes les 0,2 s : sous un feu continu, l'insecte ne doit pas rester blanc.
        if (_flashT > -0.13f) return;
        foreach (var m in _meshes) m.MaterialOverlay = _flashMat;
        _flashT = 0.06f;
    }

    /// <summary>Anime la démarche (vitesse en m/s), l'attaque (0..1) et le vol.</summary>
    public void Animate(float dt, float speed, float attack)
    {
        _t += dt;
        if (_flashT > -1f)
        {
            var was = _flashT;
            _flashT -= dt;
            if (was > 0 && _flashT <= 0 && _meshes != null) foreach (var m in _meshes) m.MaterialOverlay = null;
        }
        if (Type == EnemyType.Nest)
        {
            Scale = Vector3.One * (1f + Mathf.Sin(_t * 1.5f) * 0.015f);
            return;
        }
        if (_flying)
        {
            foreach (var w in _wings)
                w.RotationDegrees = new Vector3(0, 0, Mathf.Sin(_t * 70f) * 35f * Mathf.Sign(w.Position.X));
            if (_bodyPivot != null) _bodyPivot.Position = new Vector3(0, Mathf.Sin(_t * 3f) * 0.25f, 0);
            if (_gaster != null) _gaster.RotationDegrees = new Vector3(-20 - attack * 50f, 0, 0);
            return;
        }
        var scaleLen = Scale.X;
        _gait += dt * speed * 2.4f / Mathf.Max(scaleLen, 0.5f);
        foreach (var l in _legs)
        {
            var s = Mathf.Sin(_gait + l.Phase);
            var lift = Mathf.Max(0, Mathf.Cos(_gait + l.Phase)) * Mathf.Min(speed, 6f) / 6f;
            l.Hip.RotationDegrees = new Vector3(0, l.BaseYaw + s * 22f * Mathf.Min(speed, 6f) / 6f, 0);
            l.Knee.RotationDegrees = new Vector3(0, 0, (-70 + lift * 16f) * l.Side);
        }
        if (_bodyPivot != null)
        {
            // Bond vers l'avant pendant la morsure.
            var lunge = Mathf.Sin(Mathf.Clamp(attack, 0, 1) * Mathf.Pi);
            _bodyPivot.Position = new Vector3(0, 1.05f + Mathf.Abs(Mathf.Sin(_gait * 2)) * 0.05f * Mathf.Min(speed, 6) / 6f + lunge * 0.15f, lunge * 0.6f);
            _bodyPivot.Rotation = new Vector3(lunge * 0.18f, 0, 0);
        }
        if (_head != null) _head.RotationDegrees = new Vector3(-attack * 25f + Mathf.Sin(_t * 2f) * 3f, Mathf.Sin(_t * 1.3f) * 6f, 0);
        if (_mandL != null)
        {
            var open = 15f + attack * 35f + Mathf.Sin(_t * 9f) * 4f;
            _mandL.RotationDegrees = new Vector3(0, open, 0);
            _mandR.RotationDegrees = new Vector3(0, -open, 0);
        }
        if (_gaster != null)
            _gaster.RotationDegrees = new Vector3(Type == EnemyType.AcidAnt ? -attack * 70f : Mathf.Sin(_t * 1.7f) * 3f, 0, 0);
    }

    /// <summary>Pose de mort : retourné, pattes repliées.</summary>
    public void DeathPose()
    {
        foreach (var l in _legs) l.Knee.RotationDegrees = new Vector3(0, 0, -30 * l.Side);
        if (_bodyPivot != null) _bodyPivot.RotationDegrees = new Vector3(0, 0, 180);
        foreach (var w in _wings) w.RotationDegrees = new Vector3(0, 0, 60);
    }
}
