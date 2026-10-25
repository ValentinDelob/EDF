using Godot;

namespace Rempart;

public enum WeaponStyle { Rifle = 0, Shotgun = 1, Sniper = 2, Launcher = 3, Energy = 4, Minigun = 5, Hammer = 6, Device = 7 }

/// <summary>
/// Soldat procédural : maillages lissés (lofts) fusionnés par articulation, camouflage généré,
/// animation procédurale (course, visée épaulée avec mains sur l'arme par IK, vol, réception, à terre).
/// </summary>
public partial class SoldierModel : Node3D
{
    private Node3D _hips, _torso, _head, _armL, _armR, _foreL, _foreR, _legL, _legR, _shinL, _shinR, _gun;
    private Node3D _handL, _handR, _footL, _footR;
    private Node3D _jetFlames;
    private float _cycle;
    private float _t;
    public Node3D Muzzle { get; private set; }
    public SoldierClass Class;

    /// <summary>Nombre de triangles du modèle (corps + arme), pour diagnostic.</summary>
    public int Triangles => _bodyTris + _gunTris;
    private int _bodyTris, _gunTris;

    // Proportions (mètres) : bassin, longueurs des segments du bras.
    private const float HipY = 0.98f;
    private const float UpperLen = 0.29f;
    private const float ForeLen = 0.25f;
    /// <summary>Centre de la prise de la main (repère de la main, poignet à l'origine).</summary>
    private static readonly Vector3 GripC = new(0, -0.1f, 0.03f);

    private static readonly Color[] SkinTones =
    {
        new(0.80f, 0.62f, 0.52f), new(0.70f, 0.53f, 0.42f), new(0.58f, 0.42f, 0.32f),
        new(0.42f, 0.29f, 0.21f), new(0.32f, 0.22f, 0.16f), new(0.76f, 0.6f, 0.47f),
    };

    public static SoldierModel Create(SoldierClass cls)
    {
        var m = new SoldierModel { Class = cls };
        m.Build();
        return m;
    }

    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    // ───────────────────────── Apparence par classe ─────────────────────────

    private struct Look
    {
        public Material Camo, Helmet;
        public Color HelmetCol, Gear, Strap, Boot, Glove, Pad, Accent, Pack, ArmorCol;
    }

    private static Look LookFor(SoldierClass cls, ClassDef def)
    {
        var p = def.Primary;
        var s = def.Secondary;
        var white = Colors.White;
        switch (cls)
        {
            case SoldierClass.Airborne:
                return new Look
                {
                    Camo = SoldierMats.Camo("air", p.Darkened(0.12f), p.Darkened(0.42f), p.Lerp(s, 0.35f).Darkened(0.25f), p.Lightened(0.15f), 11),
                    Helmet = SoldierMats.Hard(0.45f, 0.1f), HelmetCol = p.Lerp(s, 0.25f).Darkened(0.1f),
                    Gear = s.Darkened(0.35f), Strap = new Color(0.1f, 0.11f, 0.13f), Boot = new Color(0.12f, 0.12f, 0.13f),
                    Glove = new Color(0.1f, 0.1f, 0.11f), Pad = new Color(0.12f, 0.13f, 0.15f), Accent = s, Pack = s.Darkened(0.35f),
                    ArmorCol = p,
                };
            case SoldierClass.Support:
                return new Look
                {
                    Camo = SoldierMats.Camo("sup", p, p.Lightened(0.22f), new Color(0.4f, 0.31f, 0.21f), new Color(0.44f, 0.44f, 0.34f), 23),
                    Helmet = null, HelmetCol = white,
                    Gear = new Color(0.5f, 0.42f, 0.3f), Strap = new Color(0.36f, 0.3f, 0.22f), Boot = new Color(0.52f, 0.42f, 0.3f),
                    Glove = new Color(0.44f, 0.37f, 0.27f), Pad = new Color(0.4f, 0.34f, 0.25f), Accent = s, Pack = new Color(0.47f, 0.4f, 0.29f),
                    ArmorCol = p,
                };
            case SoldierClass.Heavy:
                return new Look
                {
                    Camo = SoldierMats.Camo("hvy", p.Darkened(0.2f), p.Darkened(0.45f), p.Lerp(new Color(0.3f, 0.27f, 0.22f), 0.5f), p.Lightened(0.1f), 37),
                    Helmet = SoldierMats.Hard(0.38f, 0.35f), HelmetCol = p,
                    Gear = new Color(0.16f, 0.17f, 0.18f), Strap = new Color(0.09f, 0.09f, 0.1f), Boot = new Color(0.1f, 0.1f, 0.11f),
                    Glove = new Color(0.08f, 0.08f, 0.09f), Pad = p.Darkened(0.2f), Accent = s, Pack = p,
                    ArmorCol = p,
                };
            default:
                return new Look
                {
                    Camo = SoldierMats.Camo("rif", p, p.Darkened(0.4f), new Color(0.33f, 0.27f, 0.19f), p.Lightened(0.22f), 5),
                    Helmet = null, HelmetCol = white,
                    Gear = new Color(0.3f, 0.32f, 0.24f), Strap = new Color(0.22f, 0.23f, 0.18f), Boot = new Color(0.38f, 0.31f, 0.22f),
                    Glove = new Color(0.16f, 0.15f, 0.13f), Pad = new Color(0.14f, 0.15f, 0.13f), Accent = s.Lightened(0.15f), Pack = new Color(0.28f, 0.3f, 0.22f),
                    ArmorCol = p,
                };
        }
    }

    // ───────────────────────── Construction ─────────────────────────

    private void Build()
    {
        var def = Classes.Get(Class);
        var heavy = Class == SoldierClass.Heavy;
        var k = heavy ? 1.12f : 1f; // carrure
        Vector2 R(float x, float z) => new(x * k, z * k);
        var look = LookFor(Class, def);
        var camo = look.Camo;
        var gear = SoldierMats.Gear();
        var hard = SoldierMats.Hard(0.55f, 0.05f);
        var armor = SoldierMats.Hard(0.38f, 0.35f);
        var tone = SkinTones[GD.RandRange(0, SkinTones.Length - 1)];
        var skin = SoldierMats.Skin(tone);
        var lens = SoldierMats.Lens(new Color(0.05f, 0.06f, 0.07f));
        var w = Colors.White;
        var black = new Color(0.06f, 0.06f, 0.065f);
        Color Sh(float f) => new(f, f, f);

        // ── Bassin : pantalon, ceinturon et poches ──
        _hips = new Node3D { Position = new Vector3(0, HipY, 0) };
        AddChild(_hips);
        var mh = new SoldierMesh();
        mh.Tube(camo, w, new[] { V(0, -0.13f, -0.005f), V(0, -0.08f, -0.01f), V(0, 0f, -0.01f), V(0, 0.07f, -0.005f), V(0, 0.11f, 0) },
            new[] { R(0.1f, 0.075f), R(0.145f, 0.1f), R(0.158f, 0.108f), R(0.15f, 0.103f), R(0.145f, 0.1f) }, 14, 2.2f);
        mh.Tube(gear, look.Strap, new[] { V(0, 0.022f, -0.005f), V(0, 0.028f, -0.005f), V(0, 0.082f, -0.005f), V(0, 0.088f, -0.005f) },
            new[] { R(0.158f, 0.11f), R(0.166f, 0.117f), R(0.164f, 0.115f), R(0.156f, 0.108f) }, 16, 2.4f, null, false, false);
        mh.Box(hard, new Color(0.12f, 0.12f, 0.12f), V(0, 0.055f, 0.112f * k), V(0.05f, 0.04f, 0.014f));
        void BeltPouch(float ang, Vector3 size, Color c, float y = 0.035f)
        {
            var pos = V(Mathf.Sin(ang) * (0.166f * k + size.Z * 0.45f), y, Mathf.Cos(ang) * (0.117f * k + size.Z * 0.45f) - 0.005f);
            mh.Box(gear, c, pos, size, new Basis(Vector3.Up, ang));
        }
        BeltPouch(1.15f, V(0.07f, 0.09f, 0.04f), look.Gear);
        BeltPouch(-1.15f, V(0.07f, 0.09f, 0.04f), look.Gear);
        BeltPouch(Mathf.Pi, V(0.15f, 0.1f, 0.05f), look.Gear * 0.95f, 0.02f);
        BeltPouch(2.3f, V(0.08f, 0.08f, 0.045f), look.Gear * 0.9f);
        BeltPouch(-2.35f, V(0.09f, 0.09f, 0.045f), heavy ? look.Gear : new Color(0.55f, 0.18f, 0.14f) * 0.8f); // trousse de secours
        _hips.AddChild(mh.Build());
        _bodyTris += mh.Triangles;

        // ── Jambes ──
        Node3D Leg(float side, out Node3D shin, out Node3D foot)
        {
            var leg = new Node3D { Position = new Vector3(0.095f * side * k, -0.06f, 0) };
            _hips.AddChild(leg);
            var mt = new SoldierMesh();
            mt.Tube(camo, w, new[] { V(0, 0.06f, 0), V(0, -0.02f, 0.002f), V(0, -0.12f, 0.01f), V(0, -0.24f, 0.008f), V(0, -0.34f, 0.004f), V(0, -0.42f, 0), V(0, -0.47f, 0) },
                new[] { R(0.085f, 0.09f), R(0.096f, 0.1f), R(0.094f, 0.098f), R(0.083f, 0.086f), R(0.071f, 0.074f), R(0.063f, 0.066f), R(0.058f, 0.06f) }, 10, 2.1f);
            // Poche cargo latérale et son rabat.
            mt.Box(camo, Sh(0.9f), V(side * (0.08f * k + 0.008f), -0.2f, 0.008f), V(0.028f, 0.13f, 0.12f));
            mt.Box(camo, Sh(0.78f), V(side * (0.083f * k + 0.012f), -0.13f, 0.008f), V(0.026f, 0.03f, 0.125f));
            if (heavy) mt.Box(armor, look.ArmorCol, V(0, -0.2f, 0.085f * k), V(0.14f, 0.2f, 0.035f) * k);
            leg.AddChild(mt.Build());
            _bodyTris += mt.Triangles;

            shin = new Node3D { Position = new Vector3(0, -0.44f, 0) };
            leg.AddChild(shin);
            var ms = new SoldierMesh();
            ms.Tube(camo, w, new[] { V(0, 0.05f, 0), V(0, -0.03f, 0), V(0, -0.12f, -0.012f), V(0, -0.21f, -0.008f), V(0, -0.28f, 0) },
                new[] { R(0.062f, 0.065f), R(0.058f, 0.06f), R(0.057f, 0.066f), R(0.051f, 0.057f), R(0.047f, 0.05f) }, 10, 2.1f);
            ms.Tube(gear, look.Boot, new[] { V(0, -0.24f, 0.002f), V(0, -0.3f, 0.004f), V(0, -0.36f, 0.006f), V(0, -0.41f, 0.008f) },
                new[] { R(0.052f, 0.056f), R(0.05f, 0.055f), R(0.051f, 0.058f), R(0.052f, 0.06f) }, 12, 2.3f);
            ms.Tube(gear, look.Boot * 0.8f, new[] { V(0, -0.235f, 0.002f), V(0, -0.25f, 0.002f) }, new[] { R(0.056f, 0.06f), R(0.056f, 0.06f) }, 12, 2.3f); // col de la tige
            // Genouillère (coque + sangle).
            if (heavy) ms.Ellipsoid(armor, look.ArmorCol, V(0, -0.01f, 0.052f * k), V(0.062f, 0.08f, 0.03f) * k);
            else ms.Ellipsoid(hard, look.Pad, V(0, -0.01f, 0.05f), V(0.054f, 0.064f, 0.03f));
            ms.Tube(gear, look.Strap, new[] { V(0, -0.055f, 0), V(0, -0.07f, 0) }, new[] { R(0.06f, 0.063f), R(0.06f, 0.063f) }, 10, 2.1f, null, false, false);
            if (heavy) ms.Box(armor, look.ArmorCol, V(0, -0.2f, 0.055f * k), V(0.1f, 0.18f, 0.03f) * k);
            shin.AddChild(ms.Build());
            _bodyTris += ms.Triangles;

            foot = new Node3D { Position = new Vector3(0, -0.4f, 0) };
            shin.AddChild(foot);
            var mf = new SoldierMesh();
            mf.Tube(gear, look.Boot, new[] { V(0, -0.04f, -0.085f), V(0, -0.036f, -0.07f), V(0, -0.032f, -0.02f), V(0, -0.045f, 0.05f), V(0, -0.052f, 0.12f), V(0, -0.056f, 0.165f), V(0, -0.058f, 0.185f) },
                new[] { new Vector2(0.025f, 0.03f), new Vector2(0.04f, 0.045f), new Vector2(0.047f, 0.05f), new Vector2(0.034f, 0.05f), new Vector2(0.027f, 0.047f), new Vector2(0.02f, 0.04f), new Vector2(0.008f, 0.02f) },
                10, 3f, Vector3.Up);
            mf.Box(hard, new Color(0.07f, 0.065f, 0.06f), V(0, -0.069f, 0.05f), V(0.104f, 0.022f, 0.285f), null, 0.008f);
            foot.AddChild(mf.Build());
            _bodyTris += mf.Triangles;
            return leg;
        }
        _legL = Leg(1, out _shinL, out _footL);
        _legR = Leg(-1, out _shinR, out _footR);

        // ── Torse : chemise de combat, porte-plaques, poches MOLLE ──
        _torso = new Node3D { Position = new Vector3(0, 0.07f, 0) };
        _hips.AddChild(_torso);
        var mb = new SoldierMesh();
        mb.Tube(camo, w, new[] { V(0, -0.05f, -0.005f), V(0, 0.05f, 0), V(0, 0.15f, 0.008f), V(0, 0.25f, 0.012f), V(0, 0.33f, 0.005f), V(0, 0.39f, -0.005f), V(0, 0.43f, -0.012f), V(0, 0.465f, -0.012f) },
            new[] { R(0.14f, 0.1f), R(0.145f, 0.102f), R(0.155f, 0.11f), R(0.17f, 0.118f), R(0.178f, 0.115f), R(0.17f, 0.1f), R(0.125f, 0.085f), R(0.07f, 0.065f) }, 14, 2.3f);
        // Col montant de la chemise.
        mb.Tube(camo, Sh(0.85f), new[] { V(0, 0.43f, -0.012f), V(0, 0.47f, -0.01f), V(0, 0.5f, -0.006f) },
            new[] { new Vector2(0.075f, 0.07f), new Vector2(0.066f, 0.062f), new Vector2(0.064f, 0.06f) }, 12, 2f, null, false, false);
        var gc = look.Gear;
        var plateCol = heavy ? look.ArmorCol : gc;
        var plateMat = heavy ? armor : gear;
        mb.Box(plateMat, plateCol, V(0, 0.235f, 0.128f * k), V(0.3f * k, 0.3f, 0.05f), new Basis(Vector3.Right, -0.05f), 0.016f, 8f);
        mb.Box(plateMat, plateCol, V(0, 0.25f, -0.13f * k), V(0.31f * k, 0.32f, 0.05f), new Basis(Vector3.Right, 0.04f), 0.016f, 8f);
        // Ceinture abdominale (cummerbund).
        mb.Tube(gear, gc * 0.92f, new[] { V(0, 0.07f, 0), V(0, 0.077f, 0), V(0, 0.2f, 0.004f), V(0, 0.207f, 0.004f) },
            new[] { R(0.164f, 0.12f), R(0.172f, 0.127f), R(0.18f, 0.132f), R(0.172f, 0.125f) }, 16, 2.6f, null, false, false);
        foreach (var s in new[] { 1f, -1f })
        {
            // Bretelles du porte-plaques.
            mb.Tube(gear, gc * 0.95f, new[] { V(s * 0.1f * k, 0.37f, 0.14f * k), V(s * 0.112f * k, 0.44f, 0.07f), V(s * 0.115f * k, 0.46f, -0.02f), V(s * 0.108f * k, 0.41f, -0.125f * k) },
                new[] { new Vector2(0.03f, 0.009f), new Vector2(0.03f, 0.009f), new Vector2(0.03f, 0.009f), new Vector2(0.03f, 0.009f) }, 8, 4f, Vector3.Right);
        }
        // Porte-chargeurs ventraux et poche administrative.
        for (var i = -1; i <= 1; i++)
        {
            mb.Box(gear, gc * 0.97f, V(i * 0.075f * k, 0.15f, 0.168f * k), V(0.068f, 0.105f, 0.045f));
            mb.Box(gear, gc * 0.82f, V(i * 0.075f * k, 0.205f, 0.171f * k), V(0.07f, 0.02f, 0.05f));
        }
        mb.Box(gear, gc, V(0, 0.29f, 0.166f * k), V(0.15f, 0.075f, 0.03f));
        mb.Box(gear, look.Accent, V(0.06f, 0.345f, 0.158f * k), V(0.08f, 0.026f, 0.006f), new Basis(Vector3.Right, -0.05f), 0.003f); // bande patronymique
        // Poche radio latérale + antenne courte.
        mb.Box(gear, gc * 0.9f, V(0.17f * k, 0.2f, 0.07f * k), V(0.05f, 0.13f, 0.055f), new Basis(Vector3.Up, 1.1f));
        mb.Tube(hard, black, new[] { V(0.175f * k, 0.26f, 0.07f * k), V(0.18f * k, 0.36f, 0.06f * k) }, new[] { new Vector2(0.006f, 0.006f), new Vector2(0.004f, 0.004f) }, 5);

        switch (Class)
        {
            case SoldierClass.Rifleman:
            {
                // Sac d'assaut + rouleau de couchage.
                var pk = look.Pack;
                mb.Box(gear, pk, V(0, 0.26f, -0.235f), V(0.3f, 0.36f, 0.15f), null, 0.04f, 5f);
                mb.Box(gear, pk * 0.9f, V(0, 0.44f, -0.23f), V(0.28f, 0.05f, 0.15f), null, 0.02f, 5f);
                mb.Box(gear, pk * 0.95f, V(0, 0.2f, -0.32f), V(0.22f, 0.18f, 0.04f), null, 0.015f, 5f);
                foreach (var s in new[] { 1f, -1f })
                {
                    mb.Box(gear, pk * 0.93f, V(s * 0.165f, 0.22f, -0.235f), V(0.04f, 0.2f, 0.1f), null, 0.012f, 5f);
                    mb.Box(gear, look.Strap, V(s * 0.08f, 0.26f, -0.312f), V(0.022f, 0.34f, 0.006f), null, 0.002f);
                }
                mb.Tube(gear, new Color(0.24f, 0.26f, 0.2f), new[] { V(-0.16f, 0.515f, -0.22f), V(-0.15f, 0.515f, -0.22f), V(0.15f, 0.515f, -0.22f), V(0.16f, 0.515f, -0.22f) },
                    new[] { new Vector2(0.045f, 0.045f), new Vector2(0.056f, 0.056f), new Vector2(0.056f, 0.056f), new Vector2(0.045f, 0.045f) }, 12, 2f, Vector3.Up);
                foreach (var s in new[] { 1f, -1f })
                    mb.Tube(gear, look.Strap, new[] { V(s * 0.09f, 0.515f, -0.22f), V(s * 0.1f, 0.515f, -0.22f) }, new[] { new Vector2(0.06f, 0.06f), new Vector2(0.06f, 0.06f) }, 12, 2f, Vector3.Up);
                break;
            }
            case SoldierClass.Airborne:
            {
                var jet = new Node3D { Position = new Vector3(0, 0.3f, -0.225f) };
                _torso.AddChild(jet);
                var mj = new SoldierMesh();
                var alu = new Color(0.62f, 0.64f, 0.67f);
                var gun = new Color(0.3f, 0.31f, 0.33f);
                mj.Box(armor, alu, V(0, 0.02f, 0), V(0.32f, 0.4f, 0.13f), null, 0.04f, 5f);
                mj.Box(armor, look.Accent, V(0, 0.12f, -0.067f), V(0.22f, 0.05f, 0.01f), null, 0.004f);
                mj.Box(hard, black, V(0, -0.05f, -0.068f), V(0.16f, 0.12f, 0.012f), null, 0.004f);
                foreach (var s in new[] { 1f, -1f })
                {
                    mj.Tube(armor, gun, new[] { V(s * 0.12f, 0.17f, -0.045f), V(s * 0.12f, 0.14f, -0.045f), V(s * 0.12f, 0.1f, -0.045f), V(s * 0.12f, -0.18f, -0.045f), V(s * 0.12f, -0.22f, -0.045f) },
                        new[] { new Vector2(0.02f, 0.02f), new Vector2(0.06f, 0.06f), new Vector2(0.068f, 0.068f), new Vector2(0.068f, 0.068f), new Vector2(0.055f, 0.055f) }, 12, 2f);
                    mj.Tube(armor, new Color(0.35f, 0.33f, 0.31f), new[] { V(s * 0.12f, -0.21f, -0.045f), V(s * 0.12f, -0.25f, -0.045f), V(s * 0.12f, -0.32f, -0.045f) },
                        new[] { new Vector2(0.045f, 0.045f), new Vector2(0.055f, 0.055f), new Vector2(0.072f, 0.072f) }, 12, 2f, null, false, true);
                    mj.Tube(armor, alu, new[] { V(s * 0.12f, 0.02f, -0.045f), V(s * 0.12f, -0.02f, -0.045f) },
                        new[] { new Vector2(0.071f, 0.071f), new Vector2(0.071f, 0.071f) }, 12, 2f, null, false, false); // collier
                    mj.Box(armor, alu, V(s * 0.215f, 0.06f, -0.02f), V(0.07f, 0.26f, 0.035f), new Basis(Vector3.Forward, s * 0.15f), 0.01f, 5f); // ailerons
                }
                jet.AddChild(mj.Build());
                _bodyTris += mj.Triangles;
                _jetFlames = new Node3D { Position = new Vector3(0, -0.5f, -0.045f), Visible = false };
                jet.AddChild(_jetFlames);
                foreach (var s in new[] { 1f, -1f })
                    _jetFlames.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.0f, Height = 0.7f, RadialSegments = 10, Rings = 1, Material = Mats.Glow(new Color(0.5f, 0.75f, 1f), 6f) },
                        Position = new Vector3(0.12f * s, -0.1f, 0),
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    });
                _jetFlames.AddChild(new OmniLight3D { LightColor = new Color(0.5f, 0.75f, 1f), LightEnergy = 2f, OmniRange = 5f });
                break;
            }
            case SoldierClass.Support:
            {
                var pk = look.Pack;
                mb.Box(gear, pk, V(0, 0.25f, -0.25f), V(0.32f, 0.42f, 0.18f), null, 0.045f, 5f);
                mb.Box(gear, pk * 0.9f, V(0, 0.47f, -0.245f), V(0.3f, 0.05f, 0.17f), null, 0.02f, 5f);
                mb.Box(hard, new Color(0.13f, 0.14f, 0.12f), V(0, 0.36f, -0.36f), V(0.2f, 0.14f, 0.07f), null, 0.012f, 6f); // poste radio
                mb.Box(hard, look.Accent, V(0.04f, 0.38f, -0.397f), V(0.08f, 0.03f, 0.006f), null, 0.002f);
                foreach (var dx in new[] { -0.06f, -0.03f })
                    mb.Tube(hard, black, new[] { V(dx, 0.37f, -0.4f), V(dx, 0.37f, -0.405f) }, new[] { new Vector2(0.008f, 0.008f), new Vector2(0.008f, 0.008f) }, 6);
                foreach (var s in new[] { 1f, -1f })
                    mb.Box(gear, pk * 0.93f, V(s * 0.18f, 0.2f, -0.25f), V(0.045f, 0.22f, 0.12f), null, 0.012f, 5f);
                mb.Tube(hard, black, new[] { V(0.1f, 0.43f, -0.34f), V(0.105f, 0.47f, -0.345f) }, new[] { new Vector2(0.012f, 0.012f), new Vector2(0.01f, 0.01f) }, 6);
                mb.Tube(hard, black, new[] { V(0.105f, 0.47f, -0.345f), V(0.115f, 0.85f, -0.37f), V(0.125f, 1.2f, -0.395f) }, new[] { new Vector2(0.005f, 0.005f), new Vector2(0.004f, 0.004f), new Vector2(0.002f, 0.002f) }, 5);
                // Combiné radio accroché à la bretelle.
                mb.Box(hard, black, V(-0.1f, 0.38f, 0.155f), V(0.035f, 0.08f, 0.025f), new Basis(Vector3.Forward, 0.3f), 0.008f, 4f);
                break;
            }
            case SoldierClass.Heavy:
            {
                var ac = look.ArmorCol;
                mb.Box(armor, ac, V(0, 0.3f, -0.29f), V(0.5f, 0.5f, 0.22f), null, 0.06f, 4f);                 // groupe moteur
                mb.Box(armor, ac.Darkened(0.3f), V(0, 0.3f, -0.405f), V(0.36f, 0.36f, 0.02f), null, 0.01f, 6f);
                foreach (var s in new[] { 1f, -1f })
                {
                    mb.Tube(armor, new Color(0.18f, 0.18f, 0.19f), new[] { V(s * 0.17f, 0.12f, -0.33f), V(s * 0.17f, 0.5f, -0.33f) }, new[] { new Vector2(0.035f, 0.035f), new Vector2(0.035f, 0.035f) }, 10);
                    // Épaulières articulées : plaques cintrées superposées.
                    for (var l = 0; l < 3; l++)
                    {
                        var tilt = new Basis(Vector3.Back, -s * (0.45f + l * 0.22f));
                        var ctr = V(s * (0.2f + l * 0.035f) * k, 0.36f - l * 0.05f, 0);
                        var rad = 0.115f - l * 0.012f;
                        var arc = new Vector3[7];
                        var ar = new Vector2[7];
                        for (var i = 0; i < 7; i++)
                        {
                            var a = -1.25f + i * 2.5f / 6f;
                            arc[i] = ctr + tilt * V(0, Mathf.Cos(a) * rad * 0.85f, Mathf.Sin(a) * rad * 1.1f);
                            ar[i] = new Vector2((0.105f - l * 0.012f) * (1f - 0.25f * Mathf.Abs(a) / 1.25f), 0.013f);
                        }
                        mb.Tube(armor, l == 1 ? ac.Darkened(0.12f) : ac, arc, ar, 6, 4f, tilt * Vector3.Right);
                    }
                    mb.Box(armor, look.Accent, V(s * 0.215f * k, 0.468f, 0.0f), V(0.05f, 0.008f, 0.17f), new Basis(Vector3.Back, -s * 0.45f), 0.003f);
                }
                mb.Tube(Mats.Glow(new Color(1f, 0.55f, 0.2f), 2f), w, new[] { V(-0.18f, 0.22f, -0.405f), V(0.18f, 0.22f, -0.405f) }, new[] { new Vector2(0.012f, 0.012f), new Vector2(0.012f, 0.012f) }, 8, 2f, Vector3.Up);
                break;
            }
        }
        _torso.AddChild(mb.Build());
        _bodyTris += mb.Triangles;

        // ── Tête : cou, crâne, visage, casque, lunettes, casque audio ──
        _head = new Node3D { Position = new Vector3(0, 0.465f, 0) };
        _torso.AddChild(_head);
        var mk = new SoldierMesh();
        mk.Tube(skin, w, new[] { V(0, -0.04f, -0.005f), V(0, 0.04f, 0), V(0, 0.09f, 0.008f) },
            new[] { new Vector2(0.052f, 0.05f), new Vector2(0.05f, 0.05f), new Vector2(0.048f, 0.05f) }, 12, 2f, null, false, false);
        mk.Ellipsoid(skin, w, V(0, 0.175f, -0.006f), V(0.076f, 0.1f, 0.097f), null, 12, 7);
        var stub = new Color(0.8f, 0.78f, 0.76f); // barbe naissante
        mk.Tube(skin, w, new[] { V(0, 0.058f, 0.056f), V(0, 0.066f, 0.05f), V(0, 0.08f, 0.042f), V(0, 0.096f, 0.031f), V(0, 0.116f, 0.021f), V(0, 0.145f, 0.012f), V(0, 0.175f, 0f) },
            new[] { new Vector2(0.03f, 0.014f), new Vector2(0.046f, 0.027f), new Vector2(0.058f, 0.042f), new Vector2(0.066f, 0.059f), new Vector2(0.07f, 0.072f), new Vector2(0.074f, 0.083f), new Vector2(0.07f, 0.08f) },
            14, 2.1f, null, true, false, false, new[] { stub, stub, stub, Sh(0.9f), Sh(0.95f), w, w });
        mk.Tube(skin, w, new[] { V(0, 0.174f, 0.084f), V(0, 0.152f, 0.099f), V(0, 0.139f, 0.096f) },
            new[] { new Vector2(0.01f, 0.008f), new Vector2(0.016f, 0.013f), new Vector2(0.016f, 0.01f) }, 8);
        mk.Box(skin, new Color(0.72f, 0.56f, 0.54f), V(0, 0.108f, 0.089f), V(0.034f, 0.006f, 0.008f), null, 0.002f);
        // Sourcils (sous le bord des lunettes).
        mk.Tube(skin, Sh(0.55f), new[] { V(-0.055f, 0.203f, 0.083f), V(0, 0.2f, 0.095f), V(0.055f, 0.203f, 0.083f) },
            new[] { new Vector2(0.006f, 0.004f), new Vector2(0.006f, 0.004f), new Vector2(0.006f, 0.004f) }, 6, 2f, Vector3.Up);

        var hc = look.Helmet == null ? Colors.White : look.HelmetCol;
        var helmetMat = look.Helmet ?? camo;
        var hr = heavy ? V(0.112f, 0.126f, 0.13f) : V(0.104f, 0.118f, 0.122f);
        var hcen = V(0, 0.19f, -0.008f);
        float Bottom(float th)
        {
            var c = Mathf.Cos(th);
            if (heavy) return c > 0 ? 0.21f - 0.11f * (1 - c) : 0.1f;
            return 0.2f + 0.015f * Mathf.Pow(Mathf.Max(0, c), 2) - 0.07f * Mathf.Pow(Mathf.Max(0, -c), 1.5f);
        }
        var rim = mk.Shell(helmetMat, hc, hcen, hr, Bottom, 18, 6);
        var rimPts = new Vector3[rim.Length];
        var rimRad = new Vector2[rim.Length];
        for (var i = 0; i < rim.Length; i++)
        {
            var o = rim[i] - hcen;
            rimPts[i] = rim[i] + new Vector3(o.X, 0, o.Z).Normalized() * 0.002f;
            rimRad[i] = new Vector2(0.007f, 0.007f);
        }
        mk.Tube(hard, look.Helmet == null ? new Color(0.12f, 0.12f, 0.11f) : hc.Darkened(0.4f), rimPts, rimRad, 5, 2f, Vector3.Up, false, false, true);
        Vector3 OnShell(float th, float y, float off)
        {
            var f = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((y - hcen.Y) / hr.Y, 2)));
            return new Vector3(Mathf.Sin(th) * (hr.X * f + off), y, Mathf.Cos(th) * (hr.Z * f + off) + hcen.Z);
        }
        foreach (var s in new[] { 1f, -1f })
        {
            // Rails latéraux ARC.
            var rail = new Vector3[5];
            var rr = new Vector2[5];
            for (var i = 0; i < 5; i++)
            {
                rail[i] = OnShell(s * (0.95f + i * 0.28f), 0.222f, 0.006f);
                rr[i] = new Vector2(0.011f, 0.006f);
            }
            mk.Tube(hard, new Color(0.1f, 0.1f, 0.1f), rail, rr, 6, 5f, Vector3.Up);
        }
        // Support de JVN, contrepoids et bande velcro.
        mk.Box(hard, new Color(0.08f, 0.08f, 0.085f), OnShell(0, 0.252f, 0.008f), V(0.056f, 0.04f, 0.022f), new Basis(Vector3.Right, -0.55f), 0.006f, 5f);
        mk.Box(gear, look.Gear * 0.9f, OnShell(Mathf.Pi, 0.205f, 0.012f), V(0.085f, 0.06f, 0.03f), new Basis(Vector3.Right, 0.35f), 0.01f, 5f);
        mk.Box(gear, look.Gear * 0.75f, OnShell(0, 0.302f, -0.001f) + V(0, 0, -0.06f), V(0.06f, 0.01f, 0.08f), null, 0.003f);
        if (heavy)
        {
            // Masque blindé à visière fendue (exosquelette).
            var face = new Vector3[9];
            var fr2 = new Vector2[9];
            var vis = new Vector3[9];
            var vr = new Vector2[9];
            for (var i = 0; i < 9; i++)
            {
                var a = -1.3f + i * 2.6f / 8f;
                var chin = 1f - 0.35f * Mathf.Abs(a) / 1.3f;
                face[i] = V(Mathf.Sin(a) * 0.1f, 0.135f - 0.012f * Mathf.Cos(a), Mathf.Cos(a) * 0.112f + 0.004f);
                fr2[i] = new Vector2(0.075f * chin + 0.012f, 0.012f);
                vis[i] = V(Mathf.Sin(a) * 0.104f, 0.175f, Mathf.Cos(a) * 0.12f + 0.004f);
                vr[i] = new Vector2(0.014f, 0.006f);
            }
            mk.Tube(armor, look.ArmorCol.Darkened(0.15f), face, fr2, 8, 4f, Vector3.Up);
            mk.Tube(lens, w, vis, vr, 6, 4f, Vector3.Up);
            mk.Box(armor, look.Accent, V(0, 0.105f, 0.125f), V(0.05f, 0.008f, 0.012f), null, 0.003f);
            foreach (var s in new[] { 1f, -1f })
                mk.Box(armor, new Color(0.15f, 0.15f, 0.16f), V(s * 0.06f, 0.1f, 0.11f), V(0.03f, 0.03f, 0.02f), new Basis(Vector3.Up, s * 0.6f), 0.006f, 5f); // filtres
        }
        else
        {
            // Lunettes balistiques enveloppantes.
            var gl = new Vector3[9];
            var gr = new Vector2[9];
            var fr = new Vector3[9];
            var frr = new Vector2[9];
            for (var i = 0; i < 9; i++)
            {
                var a = -1.15f + i * 2.3f / 8f;
                var dip = 0.004f * Mathf.Cos(a * 2.7f); // échancrure du nez
                gl[i] = V(Mathf.Sin(a) * 0.082f, 0.18f + dip, Mathf.Cos(a) * 0.1f);
                gr[i] = new Vector2(0.016f, 0.0025f);
                fr[i] = V(Mathf.Sin(a) * 0.083f, 0.196f, Mathf.Cos(a) * 0.101f);
                frr[i] = new Vector2(0.0025f, 0.004f);
            }
            mk.Tube(lens, w, gl, gr, 6, 4f, Vector3.Up);
            mk.Tube(hard, new Color(0.08f, 0.08f, 0.08f), fr, frr, 5, 2f, Vector3.Up);
            // Casque audio (coquilles) et micro.
            foreach (var s in new[] { 1f, -1f })
            {
                mk.Ellipsoid(hard, new Color(0.13f, 0.13f, 0.12f), V(s * 0.084f, 0.158f, -0.006f), V(0.024f, 0.042f, 0.037f), null, 10, 5);
            }
            mk.Tube(hard, black, new[] { V(-0.092f, 0.14f, 0.02f), V(-0.078f, 0.115f, 0.065f), V(-0.035f, 0.1f, 0.092f) },
                new[] { new Vector2(0.003f, 0.003f), new Vector2(0.003f, 0.003f), new Vector2(0.003f, 0.003f) }, 5);
            mk.Ellipsoid(gear, new Color(0.1f, 0.1f, 0.1f), V(-0.032f, 0.1f, 0.094f), V(0.008f, 0.008f, 0.008f), null, 6, 4);
        }
        _head.AddChild(mk.Build());
        _bodyTris += mk.Triangles;

        // ── Bras ──
        var shoulderX = 0.19f * k;
        Node3D Arm(float side, out Node3D fore, out Node3D hand)
        {
            var arm = new Node3D { Position = new Vector3(shoulderX * side, 0.4f, 0) };
            _torso.AddChild(arm);
            var ma = new SoldierMesh();
            ma.Tube(camo, w, new[] { V(0, 0.045f, 0), V(0, 0.02f, 0.003f), V(0, -0.04f, 0.008f), V(0, -0.12f, 0.004f), V(0, -0.2f, 0), V(0, -0.26f, 0), V(0, -0.3f, 0) },
                new[] { R(0.035f, 0.04f), R(0.062f, 0.066f), R(0.064f, 0.066f), R(0.054f, 0.058f), R(0.05f, 0.052f), R(0.047f, 0.049f), R(0.046f, 0.048f) }, 10, 2.1f);
            ma.Box(gear, look.Accent, V(side * 0.061f * k, -0.075f, 0.002f), V(0.006f, 0.045f, 0.04f), null, 0.002f); // écusson
            if (heavy) ma.Box(armor, look.ArmorCol, V(side * 0.05f * k, -0.13f, 0), V(0.035f, 0.14f, 0.1f), null, 0.012f, 4f);
            arm.AddChild(ma.Build());
            _bodyTris += ma.Triangles;

            fore = new Node3D { Position = new Vector3(0, -UpperLen, 0) };
            arm.AddChild(fore);
            var mo = new SoldierMesh();
            mo.Tube(camo, w, new[] { V(0, 0.035f, 0), V(0, -0.03f, -0.004f), V(0, -0.1f, 0), V(0, -0.17f, 0), V(0, -0.2f, 0) },
                new[] { R(0.045f, 0.047f), R(0.05f, 0.05f), R(0.045f, 0.042f), R(0.038f, 0.035f), R(0.037f, 0.034f) }, 10, 2.1f);
            mo.Tube(gear, look.Glove, new[] { V(0, -0.175f, 0), V(0, -0.22f, 0), V(0, -0.25f, 0) },
                new[] { R(0.038f, 0.035f), R(0.034f, 0.03f), R(0.031f, 0.027f) }, 10, 2.2f);
            if (heavy)
            {
                mo.Box(armor, look.ArmorCol, V(0, -0.1f, 0.0f), V(0.1f, 0.15f, 0.095f), null, 0.025f, 3f);
            }
            fore.AddChild(mo.Build());
            _bodyTris += mo.Triangles;

            hand = new Node3D { Position = new Vector3(0, -ForeLen, 0) };
            fore.AddChild(hand);
            var mn = new SoldierMesh();
            var gl = look.Glove;
            mn.Box(gear, gl, V(0, -0.045f, 0.004f), V(0.078f, 0.085f, 0.03f), null, 0.01f, 4f);
            // Doigts repliés autour de la prise : un seul loft aplati (silhouette de poing ganté).
            var fx = side * 0.003f;
            mn.Tube(gear, gl * 0.9f, new[] { V(fx, -0.078f, 0.0f), V(fx, -0.112f, 0.006f), V(fx, -0.126f, 0.03f), V(fx, -0.113f, 0.053f), V(fx, -0.09f, 0.056f) },
                new[] { new Vector2(0.039f, 0.0115f), new Vector2(0.039f, 0.0115f), new Vector2(0.037f, 0.011f), new Vector2(0.035f, 0.0105f), new Vector2(0.032f, 0.0095f) }, 8, 2.6f, Vector3.Right);
            mn.Tube(gear, gl * 0.95f, new[] { V(side * 0.03f, -0.015f, 0.012f), V(side * 0.045f, -0.045f, 0.03f), V(side * 0.035f, -0.07f, 0.048f), V(side * 0.016f, -0.082f, 0.055f) },
                new[] { new Vector2(0.013f, 0.013f), new Vector2(0.012f, 0.012f), new Vector2(0.011f, 0.011f), new Vector2(0.0095f, 0.0095f) }, 6, 2f, Vector3.Right, false, true);
            hand.AddChild(mn.Build());
            _bodyTris += mn.Triangles;
            return arm;
        }
        _armR = Arm(-1, out _foreR, out _handR);
        _armL = Arm(1, out _foreL, out _handL);

        // Arme tenue (reconstruite selon l'arme équipée).
        _gun = new Node3D();
        _torso.AddChild(_gun);
        SetWeaponStyle(heavy ? WeaponStyle.Minigun : WeaponStyle.Rifle);

        if (heavy) Scale = Vector3.One * 1.12f;
        Animate(0.016f, 0, 0, false, false, false, false, 0);
    }

    // ───────────────────────── Armes ─────────────────────────

    private enum Hold { Shoulder, Launcher, Hip, OneHand }

    /// <summary>Points de prise de l'arme (repère local de l'arme) et orientation des mains.</summary>
    private struct Rig
    {
        public Hold Hold;
        public Vector3 Grip, Support, Butt, Mag;
        public Basis GripB, SupB;
    }

    private Rig _rig;

    public WeaponStyle Style { get; private set; } = (WeaponStyle)(-1);
    /// <summary>Vrai pendant un rechargement : l'arme est abaissée et inclinée.</summary>
    public bool Reloading;
    private float _reloadBlend;

    /// <summary>Repère de la main à partir de l'axe de la prise (pouce) et de la direction des doigts.</summary>
    private static Basis HandBasis(Vector3 x, Vector3 f)
    {
        x = x.Normalized();
        var y = -(f - x * f.Dot(x)).Normalized();
        return new Basis(x, y, x.Cross(y));
    }

    // Main droite sur une poignée pistolet ; main gauche sous le garde-main.
    private static readonly Basis PistolGripR = HandBasis(V(0, -0.94f, -0.34f), V(0.45f, -0.25f, 0.85f));
    private static readonly Basis HandguardL = HandBasis(V(0, 0.3f, 0.95f), V(-0.85f, -0.2f, 0.3f));
    private static readonly Basis VerticalGripL = HandBasis(V(0, 0.97f, 0.25f), V(-0.4f, -0.3f, 0.85f));
    private static readonly Basis TopHandleL = HandBasis(V(0, 0, -1f), V(-1f, -0.2f, 0.1f));
    private static readonly Basis ShaftR = HandBasis(V(0, 0.15f, -1f), V(0.6f, -0.6f, 0.1f));
    private static readonly Basis ShaftL = HandBasis(V(0, -0.15f, 1f), V(-0.6f, -0.6f, 0.1f));

    /// <summary>Reconstruit le modèle de l'arme tenue.</summary>
    public void SetWeaponStyle(WeaponStyle style)
    {
        if (style == Style || _gun == null) return;
        Style = style;
        foreach (var c in _gun.GetChildren()) c.QueueFree();
        var g = new SoldierMesh();
        var gm = SoldierMats.Hard(0.42f, 0.7f);  // métal
        var gp = SoldierMats.Hard(0.72f, 0f);    // polymère
        var lens = SoldierMats.Lens(new Color(0.15f, 0.2f, 0.25f));
        var dark = new Color(0.1f, 0.1f, 0.11f);
        var poly = new Color(0.16f, 0.155f, 0.145f);
        var tan = new Color(0.46f, 0.39f, 0.28f);
        var olive = new Color(0.3f, 0.33f, 0.24f);
        var alu = new Color(0.62f, 0.64f, 0.68f);
        var glow = Mats.Glow(new Color(0.45f, 0.75f, 1f), 3f);
        Vector2 Q(float a, float b) => new(a, b);
        Vector2[] Rr(float r, int n) { var a = new Vector2[n]; for (var i = 0; i < n; i++) a[i] = Q(r, r); return a; }
        var rig = new Rig { Hold = Hold.Shoulder, GripB = PistolGripR, SupB = HandguardL };

        void PistolGrip(Vector3 top, Vector3 bottom)
            => g.Tube(gp, poly, new[] { top, bottom }, new[] { Q(0.014f, 0.022f), Q(0.014f, 0.02f) }, 8, 3f);
        void Optic(float y, float z0, float z1, float r)
        {
            g.Tube(gm, dark, new[] { V(0, y, z0), V(0, y, z0 + 0.025f), V(0, y, z1 - 0.025f), V(0, y, z1) }, new[] { Q(r * 1.25f, r * 1.25f), Q(r, r), Q(r, r), Q(r * 1.35f, r * 1.35f) }, 10, 2f, Vector3.Up);
            g.Ellipsoid(lens, Colors.White, V(0, y, z1 + 0.001f), V(r * 1.2f, 0.003f, r * 1.2f), new Basis(Vector3.Right, Mathf.Pi / 2), 8, 2);
            g.Ellipsoid(lens, Colors.White, V(0, y, z0 - 0.001f), V(r * 1.1f, 0.003f, r * 1.1f), new Basis(Vector3.Right, Mathf.Pi / 2), 8, 2);
        }

        float muzzleZ;
        switch (style)
        {
            case WeaponStyle.Shotgun:
                g.Box(gm, dark, V(0, 0, 0), V(0.055f, 0.075f, 0.28f), null, 0.01f);
                g.Tube(gm, dark, new[] { V(0, 0.018f, 0.14f), V(0, 0.018f, 0.7f) }, Rr(0.016f, 2), 10, 2f, Vector3.Up);
                g.Tube(gm, dark, new[] { V(0, -0.022f, 0.14f), V(0, -0.022f, 0.6f) }, Rr(0.015f, 2), 10, 2f, Vector3.Up);
                g.Tube(gp, poly, new[] { V(0, -0.022f, 0.28f), V(0, -0.022f, 0.3f), V(0, -0.022f, 0.46f), V(0, -0.022f, 0.48f) }, new[] { Q(0.022f, 0.022f), Q(0.029f, 0.029f), Q(0.029f, 0.029f), Q(0.022f, 0.022f) }, 10, 2.6f, Vector3.Up);
                PistolGrip(V(0, -0.035f, -0.09f), V(0, -0.13f, -0.12f));
                g.Tube(gp, poly, new[] { V(0, -0.01f, -0.14f), V(0, -0.035f, -0.3f), V(0, -0.05f, -0.4f) }, new[] { Q(0.03f, 0.021f), Q(0.05f, 0.022f), Q(0.062f, 0.024f) }, 10, 4f, Vector3.Up);
                g.Box(gm, dark, V(0, 0.045f, 0.02f), V(0.02f, 0.012f, 0.12f), null, 0.003f);
                rig.Grip = V(0, -0.09f, -0.11f); rig.Support = V(0, -0.022f, 0.38f); rig.Butt = V(0, -0.05f, -0.4f); rig.Mag = V(0, -0.04f, 0.05f);
                muzzleZ = 0.72f;
                break;
            case WeaponStyle.Sniper:
                g.Box(gm, dark, V(0, 0, 0), V(0.055f, 0.06f, 0.42f), null, 0.01f);
                g.Tube(gp, olive, new[] { V(0, -0.012f, 0.1f), V(0, -0.012f, 0.48f) }, new[] { Q(0.032f, 0.03f), Q(0.028f, 0.028f) }, 10, 3f, Vector3.Up);
                g.Tube(gm, dark, new[] { V(0, 0.01f, 0.2f), V(0, 0.01f, 0.95f) }, new[] { Q(0.016f, 0.016f), Q(0.012f, 0.012f) }, 10, 2f, Vector3.Up);
                g.Box(gm, dark, V(0, 0.01f, 0.97f), V(0.04f, 0.03f, 0.06f), null, 0.006f);
                Optic(0.085f, -0.14f, 0.2f, 0.02f);
                g.Box(gm, dark, V(0, 0.055f, -0.08f), V(0.026f, 0.03f, 0.02f), null, 0.004f);
                g.Box(gm, dark, V(0, 0.055f, 0.12f), V(0.026f, 0.03f, 0.02f), null, 0.004f);
                PistolGrip(V(0, -0.04f, -0.1f), V(0, -0.135f, -0.13f));
                g.Tube(gp, olive, new[] { V(0, -0.0f, -0.2f), V(0, -0.02f, -0.36f), V(0, -0.03f, -0.5f) }, new[] { Q(0.035f, 0.022f), Q(0.06f, 0.024f), Q(0.068f, 0.025f) }, 10, 4f, Vector3.Up);
                g.Box(gp, olive, V(0, 0.035f, -0.34f), V(0.04f, 0.03f, 0.12f), null, 0.01f); // appui-joue
                g.Box(gm, dark, V(0, -0.06f, 0.05f), V(0.032f, 0.06f, 0.075f), null, 0.006f);
                foreach (var s in new[] { 1f, -1f })
                    g.Tube(gm, dark, new[] { V(s * 0.012f, -0.035f, 0.44f), V(s * 0.014f, -0.04f, 0.66f) }, Rr(0.006f, 2), 5);
                rig.Grip = V(0, -0.095f, -0.12f); rig.Support = V(0, -0.012f, 0.28f); rig.Butt = V(0, -0.03f, -0.5f); rig.Mag = V(0, -0.07f, 0.05f);
                muzzleZ = 1.0f;
                break;
            case WeaponStyle.Launcher:
                g.Tube(gp, olive, new[] { V(0, 0, -0.46f), V(0, 0, -0.42f), V(0, 0, 0.7f), V(0, 0, 0.74f) }, new[] { Q(0.068f, 0.068f), Q(0.058f, 0.058f), Q(0.058f, 0.058f), Q(0.07f, 0.07f) }, 14, 2f, Vector3.Up, false, false);
                g.Tube(gm, dark, new[] { V(0, 0, -0.43f), V(0, 0, -0.42f) }, Rr(0.06f, 2), 14, 2f, Vector3.Up, true, false);
                g.Box(gp, dark, V(-0.075f, 0.035f, 0.08f), V(0.05f, 0.08f, 0.13f), null, 0.012f); // conduite de tir
                Optic(0.075f, 0.0f, 0.12f, 0.016f);
                g.Tube(gp, poly, new[] { V(0, -0.05f, -0.06f), V(0, -0.14f, -0.08f) }, new[] { Q(0.014f, 0.022f), Q(0.014f, 0.02f) }, 8, 3f);
                g.Tube(gp, poly, new[] { V(0, -0.05f, 0.24f), V(0, -0.14f, 0.26f) }, new[] { Q(0.014f, 0.022f), Q(0.014f, 0.02f) }, 8, 3f);
                g.Box(gp, olive.Darkened(0.2f), V(0, -0.06f, -0.2f), V(0.05f, 0.03f, 0.12f), null, 0.01f); // épaulière
                rig.Hold = Hold.Launcher; rig.SupB = VerticalGripL;
                rig.Grip = V(0, -0.105f, -0.075f); rig.Support = V(0, -0.105f, 0.25f); rig.Butt = V(0, -0.06f, -0.2f); rig.Mag = V(0, 0, -0.44f);
                muzzleZ = 0.78f;
                break;
            case WeaponStyle.Energy:
                g.Tube(gm, alu, new[] { V(0, 0.0f, -0.33f), V(0, 0.01f, -0.25f), V(0, 0.02f, -0.05f), V(0, 0.02f, 0.2f), V(0, 0.01f, 0.36f), V(0, 0.005f, 0.4f) },
                    new[] { Q(0.06f, 0.024f), Q(0.055f, 0.028f), Q(0.05f, 0.04f), Q(0.045f, 0.038f), Q(0.035f, 0.032f), Q(0.025f, 0.025f) }, 12, 3.5f, Vector3.Up);
                g.Box(gm, dark, V(0, -0.02f, -0.29f), V(0.05f, 0.11f, 0.06f), null, 0.012f); // talon de crosse
                g.Tube(glow, Colors.White, new[] { V(0, 0.062f, -0.05f), V(0, 0.062f, 0.25f) }, Rr(0.009f, 2), 8, 2f, Vector3.Up);
                foreach (var s in new[] { 1f, -1f })
                    g.Tube(glow, Colors.White, new[] { V(s * 0.04f, 0.01f, 0.0f), V(s * 0.037f, 0.01f, 0.25f) }, Rr(0.005f, 2), 6, 2f, Vector3.Up);
                g.Tube(gm, dark, new[] { V(0, 0.005f, 0.38f), V(0, 0.005f, 0.6f) }, new[] { Q(0.03f, 0.03f), Q(0.022f, 0.022f) }, 10, 2f, Vector3.Up);
                g.Tube(glow, Colors.White, new[] { V(0, 0.005f, 0.6f), V(0, 0.005f, 0.635f) }, Rr(0.028f, 2), 10, 2f, Vector3.Up);
                PistolGrip(V(0, -0.03f, -0.08f), V(0, -0.13f, -0.11f));
                Optic(0.09f, -0.06f, 0.05f, 0.016f);
                g.Box(gm, dark, V(0, -0.055f, 0.06f), V(0.03f, 0.07f, 0.06f), null, 0.008f); // cellule d'énergie
                rig.Grip = V(0, -0.09f, -0.1f); rig.Support = V(0, 0.0f, 0.24f); rig.Butt = V(0, -0.02f, -0.33f); rig.Mag = V(0, -0.07f, 0.06f);
                muzzleZ = 0.64f;
                break;
            case WeaponStyle.Minigun:
                g.Box(gp, olive, V(0, 0, 0), V(0.16f, 0.17f, 0.42f), null, 0.03f, 5f);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * Mathf.Tau / 6f;
                    var o = V(Mathf.Cos(a) * 0.045f, Mathf.Sin(a) * 0.045f + 0.01f, 0);
                    g.Tube(gm, dark, new[] { o + V(0, 0, 0.2f), o + V(0, 0, 0.8f) }, Rr(0.013f, 2), 6, 2f, Vector3.Up);
                }
                foreach (var z in new[] { 0.42f, 0.78f })
                    g.Tube(gm, dark, new[] { V(0, 0.01f, z - 0.02f), V(0, 0.01f, z + 0.02f) }, Rr(0.068f, 2), 14, 2f, Vector3.Up);
                g.Box(hard(), dark, V(0.14f, -0.06f, -0.05f), V(0.1f, 0.18f, 0.2f), null, 0.015f, 6f); // caisse de munitions
                g.Tube(gm, dark, new[] { V(0, 0.09f, -0.12f), V(0, 0.15f, -0.08f), V(0, 0.15f, 0.08f), V(0, 0.09f, 0.12f) }, Rr(0.014f, 4), 6); // poignée de transport
                g.Tube(gp, poly, new[] { V(0, -0.08f, -0.13f), V(0, -0.19f, -0.16f) }, new[] { Q(0.016f, 0.024f), Q(0.016f, 0.022f) }, 8, 3f);
                rig.Hold = Hold.Hip; rig.SupB = TopHandleL;
                rig.Grip = V(0, -0.15f, -0.15f); rig.Support = V(0, 0.15f, 0.0f); rig.Butt = V(0, 0, -0.2f); rig.Mag = V(0.1f, -0.06f, -0.05f);
                muzzleZ = 0.82f;
                break;
            case WeaponStyle.Hammer:
                g.Tube(gm, dark, new[] { V(0, 0, -0.25f), V(0, 0, 0.65f) }, Rr(0.02f, 2), 8, 2f, Vector3.Up);
                g.Tube(gp, poly, new[] { V(0, 0, -0.22f), V(0, 0, 0.0f) }, Rr(0.025f, 2), 8, 2f, Vector3.Up);
                g.Box(gp, olive, V(0, 0, 0.85f), V(0.28f, 0.28f, 0.42f), null, 0.04f, 5f);
                g.Box(glow, Colors.White, V(0, 0, 0.85f), V(0.3f, 0.05f, 0.44f), null, 0.01f);
                g.Tube(gm, dark, new[] { V(0, 0, 1.06f), V(0, 0, 1.1f) }, Rr(0.1f, 2), 12, 2f, Vector3.Up);
                rig.Hold = Hold.Hip; rig.GripB = ShaftR; rig.SupB = ShaftL;
                rig.Grip = V(0, 0, -0.12f); rig.Support = V(0, 0, 0.22f); rig.Butt = V(0, 0, -0.25f); rig.Mag = V(0, 0, 0.6f);
                muzzleZ = 1.1f;
                break;
            case WeaponStyle.Device:
                g.Box(gp, dark, V(0, -0.02f, 0.03f), V(0.07f, 0.16f, 0.05f), null, 0.012f, 5f);
                g.Box(Mats.Glow(new Color(1f, 0.3f, 0.15f), 3f), Colors.White, V(0, 0.03f, 0.056f), V(0.05f, 0.04f, 0.004f), null, 0.002f);
                g.Tube(gp, dark, new[] { V(0.02f, 0.06f, 0.03f), V(0.02f, 0.24f, 0.03f) }, new[] { Q(0.006f, 0.006f), Q(0.003f, 0.003f) }, 5);
                rig.Hold = Hold.OneHand; rig.GripB = HandBasis(V(0, -1f, 0), V(0.3f, 0, 1f));
                rig.Grip = V(0, -0.06f, 0.03f); rig.Support = V(0, 0, 0.1f); rig.Butt = V(0, -0.06f, 0f); rig.Mag = V(0, 0.03f, 0.05f);
                muzzleZ = 0.15f;
                break;
            default: // fusil d'assaut / arme de défense
                g.Box(gm, dark, V(0, 0.008f, 0.03f), V(0.05f, 0.05f, 0.25f), null, 0.008f);
                g.Box(gm, dark, V(0, -0.038f, 0.0f), V(0.044f, 0.045f, 0.17f), null, 0.008f);
                g.Tube(gp, poly, new[] { V(0, -0.05f, 0.075f), V(0, -0.12f, 0.087f), V(0, -0.19f, 0.112f) }, new[] { Q(0.012f, 0.034f), Q(0.012f, 0.035f), Q(0.012f, 0.035f) }, 8, 6f);
                PistolGrip(V(0, -0.05f, -0.055f), V(0, -0.145f, -0.085f));
                g.Tube(gm, dark, new[] { V(0, -0.062f, -0.035f), V(0, -0.075f, 0.0f), V(0, -0.062f, 0.03f) }, Rr(0.003f, 3), 4);
                g.Tube(gm, dark, new[] { V(0, 0.0f, -0.09f), V(0, 0.0f, -0.24f) }, Rr(0.015f, 2), 8, 2f, Vector3.Up);
                g.Tube(gp, poly, new[] { V(0, -0.005f, -0.17f), V(0, -0.018f, -0.28f), V(0, -0.025f, -0.36f) }, new[] { Q(0.028f, 0.021f), Q(0.05f, 0.022f), Q(0.06f, 0.023f) }, 10, 4f, Vector3.Up);
                g.Tube(gm, dark, new[] { V(0, 0.006f, 0.15f), V(0, 0.006f, 0.44f) }, new[] { Q(0.029f, 0.028f), Q(0.029f, 0.028f) }, 8, 4f, Vector3.Up);
                g.Box(gm, dark, V(0, 0.041f, 0.15f), V(0.022f, 0.01f, 0.56f), null, 0.003f);
                g.Tube(gm, dark, new[] { V(0, 0.006f, 0.44f), V(0, 0.006f, 0.54f) }, Rr(0.011f, 2), 8, 2f, Vector3.Up);
                g.Tube(gm, new Color(0.05f, 0.05f, 0.05f), new[] { V(0, 0.006f, 0.54f), V(0, 0.006f, 0.6f) }, Rr(0.016f, 2), 8, 2f, Vector3.Up);
                Optic(0.078f, -0.03f, 0.09f, 0.017f);
                g.Box(gm, dark, V(0, 0.056f, 0.03f), V(0.024f, 0.026f, 0.07f), null, 0.004f);
                g.Box(gp, tan, V(-0.034f, 0.012f, 0.36f), V(0.02f, 0.035f, 0.07f), null, 0.005f); // module laser
                rig.Grip = V(0, -0.1f, -0.072f); rig.Support = V(0, 0.006f, 0.21f); rig.Butt = V(0, -0.02f, -0.36f); rig.Mag = V(0, -0.14f, 0.09f);
                muzzleZ = 0.6f;
                break;
        }
        _rig = rig;
        _gun.AddChild(g.Build());
        _gunTris = g.Triangles;
        Muzzle = new Node3D { Position = new Vector3(0, 0.006f, muzzleZ) };
        _gun.AddChild(Muzzle);

        static Material hard() => SoldierMats.Hard(0.6f, 0.1f);
    }

    public static WeaponStyle StyleFor(WeaponTemplate t) => t.Category switch
    {
        "Fusil à pompe" => WeaponStyle.Shotgun,
        "Fusil de précision" or "Fusil à particules" => WeaponStyle.Sniper,
        "Lance-roquettes" or "Lance-grenades" or "Canon de siège" or "Lance-missiles" => WeaponStyle.Launcher,
        "Laser à rayon" or "Lance électrique" or "Canon à plasma" => WeaponStyle.Energy,
        // Wing Diver d'EDF 5 (repli tant que le modèle de la famille manque).
        "Courte portée" or "Blasters" or "Électriques" or "Pulsation" or "Artillerie plasma" or "Guidées" or "Spéciales" => WeaponStyle.Energy,
        "Longue portée" => WeaponStyle.Sniper,
        "Mitrailleuse rotative" => WeaponStyle.Minigun,
        "Marteau à énergie" => WeaponStyle.Hammer,
        "Frappe d'artillerie" or "Tourelle automatique" or "Champ de soin" or "Bombardement en ligne" => WeaponStyle.Device,
        _ => WeaponStyle.Rifle,
    };

    // ───────────────────────── Animation ─────────────────────────

    private float _aimBlend, _airBlend, _flyBlend, _land, _airTime, _fireHold, _freeL, _downBlend, _grabBlend;
    private bool _wasAir;

    /// <summary>Pris dans les mandibules d'une fourmi : jambes qui pendent et se débattent, bassin ballotté.</summary>
    public bool Grabbed;

    /// <summary>
    /// speed : vitesse horizontale (m/s) ; aimPitch : radians ; airborne/flying/downed : états.
    /// </summary>
    private (float speed, float pitch, bool air, bool fly, bool down, bool aim, float recoil) _lastPose;

    /// <summary>
    /// Tir : l'arme est épaulée sur-le-champ (pose de visée complète) et la pose est recalculée aussitôt, pour que le coup parte
    /// bien de la bouche du canon et non de l'arme encore baissée.
    /// </summary>
    public void SnapAim()
    {
        if (_aimBlend >= 0.999f) { _fireHold = Mathf.Max(_fireHold, 0.8f); return; }
        _aimBlend = 1f;
        _fireHold = 0.8f;
        var l = _lastPose;
        Animate(0f, l.speed, l.pitch, l.air, l.fly, l.down, true, l.recoil);
    }

    public void Animate(float dt, float speed, float aimPitch, bool airborne, bool flying, bool downed, bool aiming, float recoil)
    {
        _lastPose = (speed, aimPitch, airborne, flying, downed, aiming, recoil);
        _t += dt;
        float Ease(float rate) => Mathf.Min(1f, dt * rate);

        _downBlend = Mathf.MoveToward(_downBlend, downed ? 1f : 0f, dt * 4f);
        if (downed) { speed = 0; airborne = false; flying = false; aiming = false; recoil = 0; }
        _grabBlend = Mathf.MoveToward(_grabBlend, Grabbed && !downed ? 1f : 0f, dt * 6f);
        if (_grabBlend > 0f) { speed = 0; airborne = false; flying = false; }

        // Tir récent : l'arme reste épaulée un instant même sans visée.
        if (recoil > 0.02f) _fireHold = 0.8f;
        _fireHold = Mathf.Max(0, _fireHold - dt);
        var aimT = aiming || _fireHold > 0 ? 1f : 0f;
        _aimBlend = Mathf.MoveToward(_aimBlend, aimT, dt * 7f);
        var aimB = Mathf.SmoothStep(0, 1, _aimBlend);
        _airBlend = Mathf.MoveToward(_airBlend, airborne && !flying ? 1f : 0f, dt * 6f);
        _flyBlend = Mathf.MoveToward(_flyBlend, flying ? 1f : 0f, dt * 4f);

        // Réception de saut : flexion des genoux proportionnelle au temps de chute.
        if (airborne) _airTime += dt;
        else
        {
            if (_wasAir && _airTime > 0.25f) _land = Mathf.Clamp(_airTime * 0.9f, 0.35f, 1f);
            _airTime = 0;
        }
        _wasAir = airborne;
        _land = Mathf.MoveToward(_land, 0, dt * 3.2f);

        var run = Mathf.Clamp(speed / 7f, 0, 1.4f) * (1 - _airBlend) * (1 - _flyBlend);
        _cycle += dt * (4f + Mathf.Min(speed, 12f) * 1.2f) * (run > 0.05f ? 1 : 0);
        var s = Mathf.Sin(_cycle);
        var c = Mathf.Cos(_cycle);
        var gaitW = Mathf.Min(run, 1.2f);
        var breath = Mathf.Sin(_t * 1.7f);
        var still = 1 - Mathf.Clamp(run * 3f, 0, 1);

        // ── Jambes ──
        var stride = 0.5f * gaitW;
        var thL = -s * stride;
        var thR = s * stride;
        var knL = (0.12f + Mathf.Max(0, c) * 1.15f) * gaitW;
        var knR = (0.12f + Mathf.Max(0, -c) * 1.15f) * gaitW;
        // Posture de tir à l'arrêt : pied gauche en avant, genoux souples.
        var stance = aimB * still;
        thL += -0.16f * stance; thR += 0.1f * stance;
        knL += 0.12f * stance; knR += 0.14f * stance;
        // Saut : jambes repliées ; vol : jambes pendantes vers l'arrière.
        thL = Mathf.Lerp(thL, -0.6f, _airBlend); knL = Mathf.Lerp(knL, 1.0f, _airBlend);
        thR = Mathf.Lerp(thR, 0.15f, _airBlend); knR = Mathf.Lerp(knR, 0.5f, _airBlend);
        var sway = Mathf.Sin(_t * 2.1f) * 0.06f;
        thL = Mathf.Lerp(thL, 0.12f + sway, _flyBlend); knL = Mathf.Lerp(knL, 0.45f, _flyBlend);
        thR = Mathf.Lerp(thR, 0.22f - sway, _flyBlend); knR = Mathf.Lerp(knR, 0.6f, _flyBlend);
        // Réception.
        thL -= 0.55f * _land; thR -= 0.45f * _land;
        knL += 1.0f * _land; knR += 0.9f * _land;
        // À terre.
        thL = Mathf.Lerp(thL, -0.15f, _downBlend); knL = Mathf.Lerp(knL, 0.35f, _downBlend);
        thR = Mathf.Lerp(thR, 0.05f, _downBlend); knR = Mathf.Lerp(knR, 0.7f, _downBlend);
        // Saisi : jambes pendantes qui battent l'air (pédalage désordonné).
        var gb = Mathf.SmoothStep(0, 1, _grabBlend);
        if (gb > 0f)
        {
            var k1 = Mathf.Sin(_t * 8.5f);
            var k2 = Mathf.Sin(_t * 7.1f + 2f);
            thL = Mathf.Lerp(thL, -0.15f + k1 * 0.55f, gb); knL = Mathf.Lerp(knL, 0.55f + Mathf.Max(0, -k1) * 1.1f, gb);
            thR = Mathf.Lerp(thR, 0.05f + k2 * 0.55f, gb); knR = Mathf.Lerp(knR, 0.55f + Mathf.Max(0, -k2) * 1.1f, gb);
        }
        var spread = 0.035f + 0.04f * stance + 0.12f * gb;
        _legL.Rotation = new Vector3(thL, 0, spread);
        _legR.Rotation = new Vector3(thR, 0, -spread);
        _shinL.Rotation = new Vector3(knL, 0, 0);
        _shinR.Rotation = new Vector3(knR, 0, 0);
        var toe = (1 - _flyBlend) * (1 - _airBlend * 0.5f);
        _footL.Rotation = new Vector3(-(thL + knL) * 0.85f * toe + 0.25f * _flyBlend, 0, -spread);
        _footR.Rotation = new Vector3(-(thR + knR) * 0.85f * toe + 0.25f * _flyBlend, 0, spread);

        // ── Bassin ──
        var bob = Mathf.Abs(c) * 0.035f * gaitW - 0.02f * gaitW;
        var hipsY = HipY + bob - 0.13f * _land - 0.03f * stance + breath * 0.002f;
        var hipsYaw = s * 0.09f * gaitW * (1 - aimB * 0.6f) - 0.22f * aimB;
        var hipsRoll = s * 0.035f * gaitW;
        var hipsPos = new Vector3(0, hipsY, 0).Lerp(new Vector3(0, 0.25f, 0), _downBlend);
        var hipsRot = new Vector3(0, hipsYaw, hipsRoll).Lerp(new Vector3(-1.4f, 0, 0), _downBlend);
        // Saisi : bassin ballotté (le corps se tord pour se dégager).
        hipsRot += new Vector3(0.28f + Mathf.Sin(_t * 5.3f) * 0.14f, Mathf.Sin(_t * 3.7f) * 0.22f, Mathf.Sin(_t * 15.1f) * 0.22f + Mathf.Sin(_t * 6.1f) * 0.12f) * gb;
        if (dt >= 0.5f) { _hips.Position = hipsPos; _hips.Rotation = hipsRot; }
        else
        {
            _hips.Position = _hips.Position.Lerp(hipsPos, Ease(20f));
            _hips.Rotation = hipsRot;
        }

        // ── Torse et tête ──
        var pitch = Mathf.Clamp(aimPitch, -1.2f, 1.2f) * (1 - _downBlend);
        var lean = run * (0.17f - 0.11f * aimB) + 0.22f * _land + 0.25f * _flyBlend + 0.08f * _airBlend;
        var torsoYaw = -s * 0.12f * gaitW * (1 - aimB * 0.7f) - 0.14f * aimB;
        _torso.Rotation = new Vector3(-pitch * 0.45f - recoil * 0.06f + lean + breath * 0.012f, torsoYaw, -hipsRoll * 0.6f);
        var look = Mathf.Sin(_t * 0.37f) * 0.18f * still * (1 - aimB) * (1 - _downBlend) + 0.5f * _downBlend;
        _head.Rotation = new Vector3(-pitch * 0.4f - lean * 0.7f + 0.16f * aimB, -(hipsYaw + torsoYaw) + look, 0.1f * aimB);

        // ── Arme : pose cible dans le repère du bassin (sans lacet), convertie dans le repère du torse ──
        var aimR = new Basis(Vector3.Right, -pitch);
        var p0 = new Vector3(0, 0.47f, 0);
        Transform3D aimX, lowX;
        switch (_rig.Hold)
        {
            case Hold.Launcher:
                aimX = new Transform3D(aimR, p0 + aimR * V(-0.17f, 0.115f, 0.0f) - aimR * _rig.Butt);
                lowX = new Transform3D(new Basis(Vector3.Up, 0.25f) * new Basis(Vector3.Right, 0.35f), p0 + V(-0.08f, -0.15f, 0.22f));
                break;
            case Hold.Hip:
            {
                var hb = new Basis(Vector3.Right, -pitch * 0.85f);
                aimX = new Transform3D(hb, p0 + hb * V(-0.13f, -0.28f, 0.34f));
                lowX = new Transform3D(new Basis(Vector3.Up, 0.2f) * new Basis(Vector3.Right, 0.3f), p0 + V(-0.08f, -0.32f, 0.3f));
                break;
            }
            case Hold.OneHand:
                aimX = new Transform3D(aimR, p0 + aimR * V(-0.14f, -0.12f, 0.4f));
                lowX = new Transform3D(new Basis(Vector3.Right, 0.9f), p0 + V(-0.2f, -0.42f, 0.14f));
                break;
            default:
                aimX = new Transform3D(aimR, p0 + aimR * V(-0.115f, 0.035f, 0.025f) - aimR * _rig.Butt);
                // Port « patrouille » : canon en travers du corps, vers le sol à gauche.
                lowX = new Transform3D(new Basis(Vector3.Up, 0.85f) * new Basis(Vector3.Right, 0.45f) * new Basis(Vector3.Back, 0.5f),
                    p0 + new Basis(Vector3.Right, -pitch * 0.2f) * V(-0.02f, -0.17f, 0.24f));
                break;
        }
        var gx = lowX.InterpolateWith(aimX, aimB);
        if (_downBlend > 0.001f)
        {
            // À terre : arme posée en travers de la poitrine.
            var dx = new Transform3D(new Basis(Vector3.Back, 0.5f) * new Basis(Vector3.Right, Mathf.Pi / 2), p0 + V(0.0f, -0.12f, 0.2f));
            gx = gx.InterpolateWith(dx, Mathf.SmoothStep(0, 1, _downBlend));
        }
        // Balancement de la course, recul, respiration.
        gx.Origin += new Vector3(s * 0.015f, Mathf.Abs(c) * 0.018f - 0.009f, 0) * gaitW * (1 - aimB * 0.6f) + new Vector3(0, breath * 0.004f, 0);
        gx.Basis = new Basis(Vector3.Up, s * 0.05f * gaitW * (1 - aimB * 0.7f)) * gx.Basis;
        gx.Origin -= gx.Basis.Z * recoil * 0.05f;
        gx.Basis *= new Basis(Vector3.Right, -recoil * 0.1f);
        _reloadBlend = Mathf.MoveToward(_reloadBlend, Reloading ? 1f : 0f, dt * 6f);
        if (_reloadBlend > 0.001f)
        {
            var rb = Mathf.SmoothStep(0, 1, _reloadBlend);
            var rx = new Transform3D(new Basis(Vector3.Up, 0.35f) * new Basis(Vector3.Right, 0.45f) * new Basis(Vector3.Back, 0.6f), p0 + V(-0.02f, -0.12f, 0.3f));
            gx = gx.InterpolateWith(rx, rb);
        }
        // Repère du bassin sans lacet (le canon reste aligné sur la visée quand le bassin pivote).
        var frame = new Transform3D(Basis.FromEuler(new Vector3(_hips.Rotation.X, 0, 0)), _hips.Position);
        var torsoModel = _hips.Transform * _torso.Transform;
        var gunT = torsoModel.AffineInverse() * frame * gx;
        _gun.Transform = gunT;

        // ── Bras : IK à deux segments vers les poignées de l'arme ──
        var gripB = gunT.Basis * _rig.GripB;
        var gripP = gunT * _rig.Grip;
        var supB = gunT.Basis * _rig.SupB;
        var supP = gunT * _rig.Support;
        if (_reloadBlend > 0.001f)
        {
            // Main gauche vers le puits de chargeur, avec un léger va-et-vient.
            var mp = gunT * (_rig.Mag + new Vector3(0, -0.04f + Mathf.Sin(_t * 9f) * 0.03f, 0));
            supP = supP.Lerp(mp, Mathf.SmoothStep(0, 1, _reloadBlend));
        }
        var freeT = _rig.Hold == Hold.OneHand || _downBlend > 0.5f ? 1f
            : (!aiming && _fireHold <= 0 && speed > 7.2f && !airborne && _rig.Hold == Hold.Shoulder ? 1f : 0f);
        _freeL = Mathf.MoveToward(_freeL, freeT, dt * 5f);

        var poleR = new Vector3(-0.6f, -1f, -0.25f);
        var poleL = new Vector3(0.55f, -1f, -0.3f);
        SolveArm(_armR, _foreR, _handR, gripP - gripB * GripC, gripB, poleR, 0, default, default);
        // Bras gauche libre : balancement opposé à la jambe gauche.
        var swing = s * 0.55f * gaitW;
        var freeArm = Basis.FromEuler(new Vector3(swing - 0.05f + 0.2f * _flyBlend, 0, 0.12f));
        var freeFore = Basis.FromEuler(new Vector3(-0.35f - 0.6f * gaitW - 0.3f * _flyBlend, 0, 0));
        if (!PoseShield(dt, frame, torsoModel, pitch, poleL))
            SolveArm(_armL, _foreL, _handL, supP - supB * GripC, supB, poleL, Mathf.SmoothStep(0, 1, _freeL), freeArm, freeFore);

        if (_jetFlames != null)
        {
            _jetFlames.Visible = flying;
            if (flying) _jetFlames.Scale = new Vector3(1, 0.8f + Mathf.Sin(_t * 40f) * 0.2f, 1);
        }
    }

    /// <summary>
    /// IK analytique épaule-coude-poignet dans le repère du torse. Le coude est orienté vers <paramref name="pole"/>,
    /// la main prend l'orientation demandée. <paramref name="free"/> mélange avec une pose libre (bras ballant).
    /// </summary>
    private static void SolveArm(Node3D arm, Node3D fore, Node3D hand, Vector3 wrist, Basis handB, Vector3 pole, float free, Basis freeArm, Basis freeFore)
    {
        var sh = arm.Position;
        var d = wrist - sh;
        var len = Mathf.Clamp(d.Length(), 0.08f, UpperLen + ForeLen - 0.002f);
        var dn = d.LengthSquared() > 1e-8f ? d.Normalized() : Vector3.Down;
        var cosA = Mathf.Clamp((UpperLen * UpperLen + len * len - ForeLen * ForeLen) / (2 * UpperLen * len), -1f, 1f);
        var sinA = Mathf.Sqrt(1 - cosA * cosA);
        var perp = pole - dn * pole.Dot(dn);
        if (perp.LengthSquared() < 1e-6f) perp = Vector3.Back - dn * dn.Z;
        perp = perp.Normalized();
        var elbow = sh + dn * (UpperLen * cosA) + perp * (UpperLen * sinA);
        var w = sh + dn * len;
        var yu = (sh - elbow).Normalized();
        var xu = dn.Cross(perp).Normalized();
        var zu = xu.Cross(yu);
        if (zu.Dot(perp) > 0) { xu = -xu; zu = -zu; }
        var bu = new Basis(xu, yu, zu);
        var yf = (elbow - w).Normalized();
        var bf = new Basis(xu, yf, xu.Cross(yf));
        var foreLocal = bu.Transposed() * bf;
        var handLocal = bf.Transposed() * handB.Orthonormalized();
        if (free > 0.001f)
        {
            var qa = bu.GetRotationQuaternion().Slerp(freeArm.GetRotationQuaternion(), free);
            var qf = foreLocal.GetRotationQuaternion().Slerp(freeFore.GetRotationQuaternion(), free);
            var qh = handLocal.GetRotationQuaternion().Slerp(Quaternion.Identity, free);
            bu = new Basis(qa);
            foreLocal = new Basis(qf);
            handLocal = new Basis(qh);
        }
        arm.Basis = bu;
        fore.Basis = foreLocal;
        hand.Basis = handLocal;
    }
}
