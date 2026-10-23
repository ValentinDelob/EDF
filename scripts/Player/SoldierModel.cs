using Godot;

namespace Rempart;

/// <summary>Soldat construit en primitives, animé procéduralement (course, visée, vol, à terre).</summary>
public partial class SoldierModel : Node3D
{
    private Node3D _hips, _torso, _head, _armL, _armR, _foreL, _foreR, _legL, _legR, _shinL, _shinR, _gun;
    private Node3D _jetFlames;
    private float _cycle;
    private float _t;
    public Node3D Muzzle { get; private set; }
    public SoldierClass Class;

    public static SoldierModel Create(SoldierClass cls)
    {
        var m = new SoldierModel { Class = cls };
        m.Build();
        return m;
    }

    private static MeshInstance3D Box(Material mat, Vector3 size, Vector3 pos)
        => new() { Mesh = new BoxMesh { Size = size, Material = mat }, Position = pos };

    private static MeshInstance3D Capsule(Material mat, float r, float h, Vector3 pos)
        => new() { Mesh = new CapsuleMesh { Radius = r, Height = h, RadialSegments = 10, Rings = 4, Material = mat }, Position = pos };

    private void Build()
    {
        var def = Classes.Get(Class);
        var uniform = Mats.Fabric(def.Primary);
        var armor = Mats.Painted(def.Secondary, 0.55f, 0.25f);
        var skin = Mats.Painted(new Color(0.72f, 0.55f, 0.43f), 0.7f);
        var boots = Mats.Painted(new Color(0.08f, 0.08f, 0.08f), 0.8f);
        var gunMat = Mats.Metal(new Color(0.12f, 0.12f, 0.13f), 0.45f);
        var heavy = Class == SoldierClass.Heavy;
        var bulk = heavy ? 1.35f : 1f;

        _hips = new Node3D { Position = new Vector3(0, 0.95f, 0) };
        AddChild(_hips);
        _hips.AddChild(Box(uniform, new Vector3(0.36f, 0.2f, 0.22f) * bulk, Vector3.Zero));

        // Jambes
        Node3D Leg(float side, out Node3D shin)
        {
            var leg = new Node3D { Position = new Vector3(0.1f * side * bulk, -0.05f, 0) };
            _hips.AddChild(leg);
            leg.AddChild(Capsule(uniform, 0.075f * bulk, 0.48f, new Vector3(0, -0.22f, 0)));
            shin = new Node3D { Position = new Vector3(0, -0.45f, 0) };
            leg.AddChild(shin);
            shin.AddChild(Capsule(heavy ? armor : uniform, 0.068f * bulk, 0.46f, new Vector3(0, -0.2f, 0)));
            shin.AddChild(Box(boots, new Vector3(0.12f, 0.08f, 0.24f) * bulk, new Vector3(0, -0.45f, 0.04f)));
            return leg;
        }
        _legL = Leg(1, out _shinL);
        _legR = Leg(-1, out _shinR);

        _torso = new Node3D { Position = new Vector3(0, 0.1f, 0) };
        _hips.AddChild(_torso);
        _torso.AddChild(Box(uniform, new Vector3(0.38f, 0.5f, 0.22f) * bulk, new Vector3(0, 0.27f, 0)));
        _torso.AddChild(Box(armor, new Vector3(0.42f, 0.38f, 0.28f) * bulk, new Vector3(0, 0.3f, 0.01f)));   // gilet pare-balles
        _torso.AddChild(Box(armor, new Vector3(0.1f, 0.08f, 0.04f), new Vector3(0.1f, 0.25f, 0.155f * bulk))); // poches
        _torso.AddChild(Box(armor, new Vector3(0.1f, 0.08f, 0.04f), new Vector3(-0.1f, 0.25f, 0.155f * bulk)));

        // Équipement dorsal par classe
        switch (Class)
        {
            case SoldierClass.Rifleman:
                _torso.AddChild(Box(uniform, new Vector3(0.3f, 0.32f, 0.14f), new Vector3(0, 0.3f, -0.18f)));
                break;
            case SoldierClass.Airborne:
                var jet = new Node3D { Position = new Vector3(0, 0.3f, -0.2f) };
                _torso.AddChild(jet);
                jet.AddChild(Box(Mats.Metal(new Color(0.55f, 0.57f, 0.6f), 0.3f), new Vector3(0.34f, 0.4f, 0.16f), Vector3.Zero));
                foreach (var s in new[] { 1f, -1f })
                    jet.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.08f, Height = 0.32f, Material = Mats.Metal(new Color(0.3f, 0.3f, 0.32f)) },
                        Position = new Vector3(0.12f * s, -0.25f, -0.03f),
                    });
                _jetFlames = new Node3D { Position = new Vector3(0, -0.5f, -0.03f), Visible = false };
                jet.AddChild(_jetFlames);
                foreach (var s in new[] { 1f, -1f })
                    _jetFlames.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.0f, Height = 0.6f, Material = Mats.Glow(new Color(0.5f, 0.75f, 1f), 6f) },
                        Position = new Vector3(0.12f * s, -0.1f, 0),
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    });
                _jetFlames.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 0.75f, 1f), LightEnergy = 2f, OmniRange = 5f });
                break;
            case SoldierClass.Support:
                _torso.AddChild(Box(uniform, new Vector3(0.32f, 0.38f, 0.18f), new Vector3(0, 0.28f, -0.2f)));
                _torso.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.012f, Height = 0.9f, Material = gunMat },
                    Position = new Vector3(0.12f, 0.8f, -0.24f),
                });
                break;
            case SoldierClass.Heavy:
                _torso.AddChild(Box(armor, new Vector3(0.5f, 0.5f, 0.24f), new Vector3(0, 0.3f, -0.24f)));
                foreach (var s in new[] { 1f, -1f })
                    _torso.AddChild(Box(armor, new Vector3(0.22f, 0.12f, 0.3f), new Vector3(0.33f * s, 0.55f, 0)));
                break;
        }

        _head = new Node3D { Position = new Vector3(0, 0.58f * bulk, 0) };
        _torso.AddChild(_head);
        _head.AddChild(Box(skin, new Vector3(0.17f, 0.2f, 0.19f), new Vector3(0, 0.09f, 0.01f)));
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.14f, Height = 0.2f, IsHemisphere = true, Material = armor },
            Position = new Vector3(0, 0.15f, 0),
            Scale = new Vector3(1f, 1.1f, 1.1f),
        });
        _head.AddChild(Box(Mats.Painted(new Color(0.05f, 0.05f, 0.06f), 0.1f, 0.4f), new Vector3(0.17f, 0.05f, 0.04f), new Vector3(0, 0.11f, 0.1f)));
        if (heavy) _head.AddChild(Box(armor, new Vector3(0.22f, 0.18f, 0.08f), new Vector3(0, 0.08f, 0.11f)));

        Node3D Arm(float side, out Node3D fore)
        {
            var arm = new Node3D { Position = new Vector3(0.24f * side * bulk, 0.48f * bulk, 0) };
            _torso.AddChild(arm);
            arm.AddChild(Capsule(uniform, 0.06f * bulk, 0.34f, new Vector3(0, -0.15f, 0)));
            fore = new Node3D { Position = new Vector3(0, -0.3f, 0) };
            arm.AddChild(fore);
            fore.AddChild(Capsule(uniform, 0.055f * bulk, 0.32f, new Vector3(0, -0.13f, 0)));
            fore.AddChild(Box(skin, new Vector3(0.07f, 0.09f, 0.08f), new Vector3(0, -0.3f, 0)));
            return arm;
        }
        _armR = Arm(-1, out _foreR);
        _armL = Arm(1, out _foreL);

        // Arme tenue
        _gun = new Node3D { Position = new Vector3(-0.12f, 0.28f, 0.32f) };
        _torso.AddChild(_gun);
        var gunLen = heavy ? 1.1f : 0.8f;
        _gun.AddChild(Box(gunMat, new Vector3(heavy ? 0.16f : 0.07f, heavy ? 0.18f : 0.12f, gunLen), Vector3.Zero));
        _gun.AddChild(Box(gunMat, new Vector3(0.05f, 0.14f, 0.07f), new Vector3(0, -0.11f, -0.05f)));
        _gun.AddChild(Box(gunMat, new Vector3(0.04f, 0.05f, 0.3f), new Vector3(0, 0.03f, gunLen / 2 + 0.12f)));
        Muzzle = new Node3D { Position = new Vector3(0, 0.03f, gunLen / 2 + 0.3f) };
        _gun.AddChild(Muzzle);

        if (heavy) Scale = Vector3.One * 1.12f;
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    /// <summary>
    /// speed : vitesse horizontale (m/s) ; aimPitch : radians ; airborne/flying/downed : états.
    /// </summary>
    public void Animate(float dt, float speed, float aimPitch, bool airborne, bool flying, bool downed, bool aiming, float recoil)
    {
        _t += dt;
        if (downed)
        {
            _hips.Position = _hips.Position.Lerp(new Vector3(0, 0.25f, 0), dt * 5);
            _hips.Rotation = _hips.Rotation.Lerp(new Vector3(-1.4f, 0, 0), dt * 5);
            if (_jetFlames != null) _jetFlames.Visible = false;
            return;
        }
        _hips.Rotation = _hips.Rotation.Lerp(Vector3.Zero, dt * 8);

        var run = Mathf.Clamp(speed / 7f, 0, 1.4f);
        _cycle += dt * (4f + speed * 1.2f) * (run > 0.05f ? 1 : 0);
        var s = Mathf.Sin(_cycle);
        var c = Mathf.Cos(_cycle);
        if (airborne)
        {
            _legL.Rotation = new Vector3(-0.5f, 0, 0);
            _legR.Rotation = new Vector3(0.2f, 0, 0);
            _shinL.Rotation = new Vector3(0.9f, 0, 0);
            _shinR.Rotation = new Vector3(0.4f, 0, 0);
        }
        else
        {
            _legL.Rotation = new Vector3(s * 0.8f * run, 0, 0);
            _legR.Rotation = new Vector3(-s * 0.8f * run, 0, 0);
            _shinL.Rotation = new Vector3(Mathf.Max(0, -c) * 1.2f * run, 0, 0);
            _shinR.Rotation = new Vector3(Mathf.Max(0, c) * 1.2f * run, 0, 0);
        }
        var bob = airborne ? 0 : Mathf.Abs(s) * 0.05f * run;
        _hips.Position = new Vector3(0, 0.95f + bob, 0);

        // Haut du corps : suit la visée
        var pitch = Mathf.Clamp(aimPitch, -1.2f, 1.2f);
        _torso.Rotation = new Vector3(-pitch * 0.45f - recoil * 0.1f, 0, 0);
        _head.Rotation = new Vector3(-pitch * 0.4f, 0, 0);
        var lean = run * 0.15f;
        _torso.Rotation += new Vector3(lean * (aiming ? 0.3f : 1f), 0, 0);
        _armR.Rotation = new Vector3(-1.35f - pitch * 0.55f + recoil * 0.25f, 0.25f, 0);
        _foreR.Rotation = new Vector3(-0.35f, 0, 0);
        _armL.Rotation = new Vector3(-1.2f - pitch * 0.55f + recoil * 0.25f, -0.55f, 0);
        _foreL.Rotation = new Vector3(-0.6f, 0, 0);
        _gun.Rotation = new Vector3(-pitch * 0.55f + recoil * 0.2f, 0, 0);
        _gun.Position = new Vector3(-0.12f, 0.28f, 0.32f - recoil * 0.06f);
        if (_jetFlames != null)
        {
            _jetFlames.Visible = flying;
            if (flying) _jetFlames.Scale = new Vector3(1, 0.8f + Mathf.Sin(_t * 40f) * 0.2f, 1);
        }
    }
}
