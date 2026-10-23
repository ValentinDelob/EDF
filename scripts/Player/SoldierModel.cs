using Godot;

namespace Rempart;

public enum WeaponStyle { Rifle = 0, Shotgun = 1, Sniper = 2, Launcher = 3, Energy = 4, Minigun = 5, Hammer = 6, Device = 7 }

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

    private static MeshInstance3D Ellip(Material mat, Vector3 scale, Vector3 pos)
        => new() { Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 7, Material = mat }, Scale = scale, Position = pos };

    private static MeshInstance3D Cyl(Material mat, float r, float h, Vector3 pos, Vector3 rotDeg)
        => new() { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = 10, Material = mat }, Position = pos, RotationDegrees = rotDeg };

    private void Build()
    {
        var def = Classes.Get(Class);
        var uniform = Mats.Fabric(def.Primary);
        var uniformDark = Mats.Fabric(def.Primary.Darkened(0.25f));
        var gear = Mats.Fabric(def.Secondary);
        var plate = Mats.Painted(def.Secondary.Darkened(0.1f), 0.6f, 0.15f);
        var skin = Mats.Painted(new Color(0.68f, 0.52f, 0.42f), 0.65f);
        var black = Mats.Painted(new Color(0.06f, 0.06f, 0.065f), 0.75f);
        var boots = Mats.Painted(new Color(0.16f, 0.13f, 0.1f), 0.85f);
        var lens = Mats.Painted(new Color(0.08f, 0.1f, 0.12f), 0.05f, 0.6f);
        var gunMat = Mats.Metal(new Color(0.1f, 0.1f, 0.11f), 0.45f);
        var gunPoly = Mats.Painted(new Color(0.18f, 0.17f, 0.15f), 0.7f);
        var heavy = Class == SoldierClass.Heavy;
        var bulk = heavy ? 1.3f : 1f;

        _hips = new Node3D { Position = new Vector3(0, 0.95f, 0) };
        AddChild(_hips);
        _hips.AddChild(Ellip(uniformDark, new Vector3(0.36f, 0.22f, 0.24f) * bulk, Vector3.Zero));
        _hips.AddChild(Box(black, new Vector3(0.38f, 0.06f, 0.26f) * bulk, new Vector3(0, 0.06f, 0)));          // ceinture
        _hips.AddChild(Box(gear, new Vector3(0.08f, 0.1f, 0.06f), new Vector3(0.17f * bulk, 0.02f, 0.06f)));     // poche de cuisse
        _hips.AddChild(Box(gear, new Vector3(0.08f, 0.1f, 0.06f), new Vector3(-0.17f * bulk, 0.02f, -0.04f)));

        Node3D Leg(float side, out Node3D shin)
        {
            var leg = new Node3D { Position = new Vector3(0.1f * side * bulk, -0.04f, 0) };
            _hips.AddChild(leg);
            leg.AddChild(Capsule(uniform, 0.085f * bulk, 0.5f, new Vector3(0, -0.22f, 0)));
            shin = new Node3D { Position = new Vector3(0, -0.45f, 0) };
            leg.AddChild(shin);
            shin.AddChild(Ellip(heavy ? plate : black, new Vector3(0.13f, 0.12f, 0.08f) * bulk, new Vector3(0, -0.02f, 0.06f))); // genouillère
            shin.AddChild(Capsule(heavy ? plate : uniform, 0.07f * bulk, 0.46f, new Vector3(0, -0.21f, 0)));
            shin.AddChild(Box(boots, new Vector3(0.12f, 0.12f, 0.27f) * bulk, new Vector3(0, -0.44f, 0.04f)));
            shin.AddChild(Box(black, new Vector3(0.13f, 0.03f, 0.28f) * bulk, new Vector3(0, -0.5f, 0.04f)));         // semelle
            return leg;
        }
        _legL = Leg(1, out _shinL);
        _legR = Leg(-1, out _shinR);

        _torso = new Node3D { Position = new Vector3(0, 0.1f, 0) };
        _hips.AddChild(_torso);
        _torso.AddChild(Ellip(uniform, new Vector3(0.42f, 0.6f, 0.26f) * bulk, new Vector3(0, 0.28f, 0)));          // buste
        _torso.AddChild(Box(plate, new Vector3(0.38f, 0.36f, 0.08f) * bulk, new Vector3(0, 0.3f, 0.11f * bulk)));      // plaque avant
        _torso.AddChild(Box(plate, new Vector3(0.38f, 0.38f, 0.07f) * bulk, new Vector3(0, 0.3f, -0.11f * bulk)));     // plaque arrière
        for (var i = -1; i <= 1; i++)
            _torso.AddChild(Box(gear, new Vector3(0.09f, 0.12f, 0.05f), new Vector3(i * 0.11f * bulk, 0.2f, 0.17f * bulk))); // porte-chargeurs
        _torso.AddChild(Box(gear, new Vector3(0.12f, 0.07f, 0.04f), new Vector3(0.08f, 0.42f, 0.16f * bulk)));       // radio de poitrine
        foreach (var s in new[] { 1f, -1f })
            _torso.AddChild(Box(plate, new Vector3(0.06f, 0.05f, 0.22f) * bulk, new Vector3(0.17f * s * bulk, 0.5f, 0)));   // bretelles

        switch (Class)
        {
            case SoldierClass.Rifleman:
                _torso.AddChild(Box(gear, new Vector3(0.3f, 0.34f, 0.16f), new Vector3(0, 0.3f, -0.22f)));
                _torso.AddChild(Ellip(uniformDark, new Vector3(0.3f, 0.1f, 0.12f), new Vector3(0, 0.5f, -0.22f)));       // sac de couchage
                break;
            case SoldierClass.Airborne:
                var jet = new Node3D { Position = new Vector3(0, 0.3f, -0.21f) };
                _torso.AddChild(jet);
                var alu = Mats.Metal(new Color(0.58f, 0.6f, 0.63f), 0.3f);
                jet.AddChild(Box(alu, new Vector3(0.34f, 0.42f, 0.14f), Vector3.Zero));
                foreach (var s in new[] { 1f, -1f })
                {
                    jet.AddChild(Cyl(Mats.Metal(new Color(0.25f, 0.25f, 0.27f)), 0.075f, 0.36f, new Vector3(0.13f * s, -0.22f, -0.04f), Vector3.Zero));
                    jet.AddChild(Box(alu, new Vector3(0.08f, 0.26f, 0.04f), new Vector3(0.24f * s, 0.05f, -0.02f)));     // ailerons
                }
                _jetFlames = new Node3D { Position = new Vector3(0, -0.5f, -0.04f), Visible = false };
                jet.AddChild(_jetFlames);
                foreach (var s in new[] { 1f, -1f })
                    _jetFlames.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.0f, Height = 0.7f, Material = Mats.Glow(new Color(0.5f, 0.75f, 1f), 6f) },
                        Position = new Vector3(0.13f * s, -0.1f, 0),
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    });
                _jetFlames.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 0.75f, 1f), LightEnergy = 2f, OmniRange = 5f });
                break;
            case SoldierClass.Support:
                _torso.AddChild(Box(gear, new Vector3(0.32f, 0.4f, 0.2f), new Vector3(0, 0.28f, -0.24f)));
                _torso.AddChild(Box(black, new Vector3(0.18f, 0.12f, 0.08f), new Vector3(0, 0.4f, -0.36f)));            // poste radio
                _torso.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.012f, Height = 1.0f, Material = black },
                    Position = new Vector3(0.12f, 0.85f, -0.3f),
                    RotationDegrees = new Vector3(-8, 0, -6),
                });
                break;
            case SoldierClass.Heavy:
                _torso.AddChild(Box(plate, new Vector3(0.52f, 0.52f, 0.26f), new Vector3(0, 0.3f, -0.26f)));            // groupe moteur
                _torso.AddChild(Cyl(Mats.Glow(new Color(1f, 0.55f, 0.2f), 2f), 0.04f, 0.4f, new Vector3(0, 0.3f, -0.4f), new Vector3(0, 0, 90)));
                foreach (var s in new[] { 1f, -1f })
                    _torso.AddChild(Ellip(plate, new Vector3(0.3f, 0.18f, 0.32f), new Vector3(0.34f * s, 0.55f, 0)));    // épaulières
                break;
        }

        _head = new Node3D { Position = new Vector3(0, 0.6f * bulk, 0) };
        _torso.AddChild(_head);
        _head.AddChild(Cyl(skin, 0.055f, 0.08f, new Vector3(0, -0.02f, 0), Vector3.Zero));                          // cou
        _head.AddChild(Ellip(skin, new Vector3(0.17f, 0.22f, 0.2f), new Vector3(0, 0.1f, 0.01f)));
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f, IsHemisphere = true, RadialSegments = 16, Rings = 6, Material = plate },
            Position = new Vector3(0, 0.14f, -0.005f),
            Scale = new Vector3(1.02f, 1.0f, 1.12f),
        });
        _head.AddChild(Cyl(plate, 0.155f, 0.03f, new Vector3(0, 0.14f, 0), Vector3.Zero));                          // bord du casque
        _head.AddChild(Box(lens, new Vector3(0.17f, 0.05f, 0.05f), new Vector3(0, 0.13f, 0.1f)));                     // lunettes
        _head.AddChild(Box(black, new Vector3(0.05f, 0.04f, 0.04f), new Vector3(0, 0.25f, 0.13f)));                   // support de JVN
        if (heavy) _head.AddChild(Box(plate, new Vector3(0.2f, 0.14f, 0.07f), new Vector3(0, 0.06f, 0.12f)));        // visière blindée

        Node3D Arm(float side, out Node3D fore)
        {
            var arm = new Node3D { Position = new Vector3(0.24f * side * bulk, 0.5f * bulk, 0) };
            _torso.AddChild(arm);
            arm.AddChild(Ellip(heavy ? plate : uniform, new Vector3(0.15f, 0.13f, 0.15f) * bulk, new Vector3(0, -0.02f, 0)));
            arm.AddChild(Capsule(uniform, 0.06f * bulk, 0.34f, new Vector3(0, -0.15f, 0)));
            fore = new Node3D { Position = new Vector3(0, -0.3f, 0) };
            arm.AddChild(fore);
            fore.AddChild(Capsule(heavy ? plate : uniform, 0.055f * bulk, 0.32f, new Vector3(0, -0.13f, 0)));
            fore.AddChild(Ellip(black, new Vector3(0.09f, 0.1f, 0.09f) * bulk, new Vector3(0, -0.31f, 0)));            // gant
            return arm;
        }
        _armR = Arm(-1, out _foreR);
        _armL = Arm(1, out _foreL);

        // Arme tenue (reconstruite selon l'arme équipée).
        _gun = new Node3D { Position = new Vector3(-0.12f, 0.28f, 0.32f) };
        _torso.AddChild(_gun);
        SetWeaponStyle(heavy ? WeaponStyle.Minigun : WeaponStyle.Rifle);

        if (heavy) Scale = Vector3.One * 1.12f;
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    public WeaponStyle Style { get; private set; } = (WeaponStyle)(-1);
    /// <summary>Vrai pendant un rechargement : l'arme est abaissée et inclinée.</summary>
    public bool Reloading;
    private float _reloadBlend;

    /// <summary>Reconstruit le modèle de l'arme tenue.</summary>
    public void SetWeaponStyle(WeaponStyle style)
    {
        if (style == Style || _gun == null) return;
        Style = style;
        foreach (var c in _gun.GetChildren()) c.QueueFree();
        var metal = Mats.Metal(new Color(0.1f, 0.1f, 0.11f), 0.45f);
        var poly = Mats.Painted(new Color(0.18f, 0.17f, 0.15f), 0.7f);
        var olive = Mats.Painted(new Color(0.3f, 0.33f, 0.24f), 0.6f, 0.2f);
        var black = Mats.Painted(new Color(0.06f, 0.06f, 0.065f), 0.75f);
        var alu = Mats.Metal(new Color(0.6f, 0.62f, 0.66f), 0.3f);
        var glow = Mats.Glow(new Color(0.45f, 0.75f, 1f), 3f);
        float muzzleZ;
        switch (style)
        {
            case WeaponStyle.Shotgun:
                _gun.AddChild(Box(metal, new Vector3(0.07f, 0.1f, 0.4f), new Vector3(0, 0, 0.02f)));
                _gun.AddChild(Cyl(metal, 0.022f, 0.5f, new Vector3(0, 0.02f, 0.45f), new Vector3(90, 0, 0)));
                _gun.AddChild(Cyl(poly, 0.03f, 0.22f, new Vector3(0, -0.03f, 0.38f), new Vector3(90, 0, 0)));   // pompe
                _gun.AddChild(Box(poly, new Vector3(0.06f, 0.1f, 0.26f), new Vector3(0, -0.02f, -0.28f)));
                muzzleZ = 0.72f;
                break;
            case WeaponStyle.Sniper:
                _gun.AddChild(Box(metal, new Vector3(0.07f, 0.1f, 0.5f), new Vector3(0, 0, 0)));
                _gun.AddChild(Cyl(metal, 0.016f, 0.75f, new Vector3(0, 0.02f, 0.6f), new Vector3(90, 0, 0)));
                _gun.AddChild(Cyl(black, 0.035f, 0.3f, new Vector3(0, 0.1f, 0.05f), new Vector3(90, 0, 0)));     // lunette
                _gun.AddChild(Box(poly, new Vector3(0.06f, 0.12f, 0.3f), new Vector3(0, -0.02f, -0.36f)));
                _gun.AddChild(Box(metal, new Vector3(0.12f, 0.02f, 0.02f), new Vector3(0, -0.06f, 0.55f)));    // bipied
                muzzleZ = 0.98f;
                break;
            case WeaponStyle.Launcher:
                _gun.AddChild(Cyl(olive, 0.09f, 1.15f, new Vector3(0, 0.12f, 0.15f), new Vector3(90, 0, 0)));
                _gun.AddChild(Cyl(black, 0.1f, 0.12f, new Vector3(0, 0.12f, 0.72f), new Vector3(90, 0, 0)));
                _gun.AddChild(Box(black, new Vector3(0.05f, 0.12f, 0.06f), new Vector3(0, -0.02f, 0.05f)));
                _gun.AddChild(Box(black, new Vector3(0.04f, 0.1f, 0.12f), new Vector3(-0.08f, 0.2f, 0.1f)));  // viseur
                muzzleZ = 0.8f;
                break;
            case WeaponStyle.Energy:
                _gun.AddChild(Box(alu, new Vector3(0.08f, 0.12f, 0.5f), new Vector3(0, 0, 0.05f)));
                _gun.AddChild(Box(glow, new Vector3(0.09f, 0.03f, 0.3f), new Vector3(0, 0.07f, 0.05f)));
                _gun.AddChild(Cyl(alu, 0.04f, 0.3f, new Vector3(0, 0.0f, 0.45f), new Vector3(90, 0, 0)));
                _gun.AddChild(Cyl(glow, 0.045f, 0.04f, new Vector3(0, 0.0f, 0.6f), new Vector3(90, 0, 0)));
                muzzleZ = 0.64f;
                break;
            case WeaponStyle.Minigun:
                _gun.AddChild(Box(olive, new Vector3(0.18f, 0.2f, 0.45f), new Vector3(0, 0, 0)));
                for (var i = 0; i < 6; i++)
                    _gun.AddChild(Cyl(metal, 0.016f, 0.6f, new Vector3(Mathf.Cos(i * 1.047f) * 0.05f, Mathf.Sin(i * 1.047f) * 0.05f + 0.01f, 0.5f), new Vector3(90, 0, 0)));
                _gun.AddChild(Box(black, new Vector3(0.1f, 0.18f, 0.2f), new Vector3(0.14f, -0.06f, -0.05f)));  // caisse de munitions
                muzzleZ = 0.82f;
                break;
            case WeaponStyle.Hammer:
                _gun.AddChild(Cyl(metal, 0.03f, 1.0f, new Vector3(0, 0, 0.3f), new Vector3(90, 0, 0)));
                _gun.AddChild(Box(olive, new Vector3(0.3f, 0.3f, 0.45f), new Vector3(0, 0, 0.85f)));
                _gun.AddChild(Box(glow, new Vector3(0.32f, 0.05f, 0.47f), new Vector3(0, 0, 0.85f)));
                muzzleZ = 1.1f;
                break;
            case WeaponStyle.Device:
                _gun.AddChild(Box(black, new Vector3(0.12f, 0.18f, 0.08f), new Vector3(0, 0, 0.05f)));
                _gun.AddChild(Box(Mats.Glow(new Color(1f, 0.3f, 0.15f), 3f), new Vector3(0.08f, 0.04f, 0.02f), new Vector3(0, 0.06f, 0.1f)));
                _gun.AddChild(Cyl(black, 0.006f, 0.3f, new Vector3(0.04f, 0.2f, 0.05f), Vector3.Zero));
                muzzleZ = 0.15f;
                break;
            default: // fusil d'assaut / arme de défense
                _gun.AddChild(Box(metal, new Vector3(0.065f, 0.1f, 0.37f), new Vector3(0, 0, 0.02f)));
                _gun.AddChild(Box(poly, new Vector3(0.06f, 0.08f, 0.25f), new Vector3(0, -0.005f, 0.25f)));
                _gun.AddChild(Cyl(metal, 0.014f, 0.29f, new Vector3(0, 0.01f, 0.45f), new Vector3(90, 0, 0)));
                _gun.AddChild(Box(poly, new Vector3(0.05f, 0.09f, 0.22f), new Vector3(0, -0.01f, -0.27f)));
                var mag = Box(metal, new Vector3(0.04f, 0.14f, 0.06f), new Vector3(0, -0.1f, 0.06f));
                mag.RotationDegrees = new Vector3(-12, 0, 0);
                _gun.AddChild(mag);
                _gun.AddChild(Cyl(black, 0.022f, 0.14f, new Vector3(0, 0.08f, 0.02f), new Vector3(90, 0, 0)));
                muzzleZ = 0.62f;
                break;
        }
        Muzzle = new Node3D { Position = new Vector3(0, 0.01f, muzzleZ) };
        _gun.AddChild(Muzzle);
    }

    public static WeaponStyle StyleFor(WeaponTemplate t) => t.Category switch
    {
        "Fusil à pompe" => WeaponStyle.Shotgun,
        "Fusil de précision" or "Fusil à particules" => WeaponStyle.Sniper,
        "Lance-roquettes" or "Lance-grenades" or "Canon de siège" or "Lance-missiles" => WeaponStyle.Launcher,
        "Laser à rayon" or "Lance électrique" or "Canon à plasma" => WeaponStyle.Energy,
        "Mitrailleuse rotative" => WeaponStyle.Minigun,
        "Marteau à énergie" => WeaponStyle.Hammer,
        "Frappe d'artillerie" or "Tourelle automatique" or "Champ de soin" or "Bombardement en ligne" => WeaponStyle.Device,
        _ => WeaponStyle.Rifle,
    };

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
        _reloadBlend = Mathf.MoveToward(_reloadBlend, Reloading ? 1f : 0f, dt * 6f);
        _gun.Rotation = new Vector3(-pitch * 0.55f + recoil * 0.2f + _reloadBlend * 0.7f, _reloadBlend * 0.4f, _reloadBlend * 0.5f);
        _gun.Position = new Vector3(-0.12f + _reloadBlend * 0.05f, 0.28f - _reloadBlend * 0.12f, 0.32f - recoil * 0.06f - _reloadBlend * 0.08f);
        if (_reloadBlend > 0.01f)
        {
            _armL.Rotation += new Vector3(_reloadBlend * (0.6f + Mathf.Sin(_t * 9f) * 0.15f), 0.3f * _reloadBlend, 0);
            _armR.Rotation += new Vector3(_reloadBlend * 0.5f, 0, 0);
        }
        if (_jetFlames != null)
        {
            _jetFlames.Visible = flying;
            if (flying) _jetFlames.Scale = new Vector3(1, 0.8f + Mathf.Sin(_t * 40f) * 0.2f, 1);
        }
    }
}
