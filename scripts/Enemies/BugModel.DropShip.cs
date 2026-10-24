using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Vaisseau de largage : transporteur blindé d'environ 44 m, coque haute à arête dorsale, deux quilles latérales qui
/// encadrent une soute ventrale fermée par deux portes à charnières, quatre nacelles de sustentation sur pylônes
/// (tuyères vers le bas), propulseurs arrière, tourelles laser sous le nez. Seule la soute ouverte est vulnérable.
/// </summary>
public partial class BugModel
{
    /// <summary>Soute (point faible) : centre et demi-tailles, repère du modèle.</summary>
    public static readonly Vector3 ShipBayPos = new(0, -2.0f, -0.5f);
    public static readonly Vector3 ShipBayHalf = new(3.3f, 1.5f, 8.5f);
    private const float ShipDoorY = -3.35f, ShipDoorX = 3.35f;
    private static readonly Vector3[] ShipPods = { new(11.6f, 0.4f, 9.5f), new(-11.6f, 0.4f, 9.5f), new(11.6f, 0.4f, -11f), new(-11.6f, 0.4f, -11f) };
    private static readonly Vector3[] ShipTurrets = { new(2.6f, -2.35f, 15.2f), new(-2.6f, -2.35f, 15.2f) };

    private readonly Node3D[] _sDoor = new Node3D[2];
    private readonly List<Node3D> _sFlames = new();
    private readonly List<Node3D> _sTurrets = new();
    private OmniLight3D _sBayLight;
    private MeshInstance3D _sEyes;
    private float _sHatch, _sHatchTarget, _sThrust = 1f;

    /// <summary>Ouverture de la soute (0 fermée → 1 ouverte), animée chez chaque pair.</summary>
    public float ShipHatch => _sHatch;
    public void ShipSetHatch(bool open) => _sHatchTarget = open ? 1f : 0f;
    /// <summary>Poussée des tuyères (0..1.5) : lueur et flammes.</summary>
    public void ShipThrust(float k) => _sThrust = k;
    public Vector3 ShipTurretMuzzle(int i) => _sTurrets[i % _sTurrets.Count].GlobalTransform * new Vector3(0, -0.35f, 2.1f);
    public Vector3 ShipBay => GlobalTransform * ShipBayPos;

    private void BuildDropShip()
    {
        _flying = true;
        _bodyPivot = new Node3D();
        AddChild(_bodyPivot);
        Part(_bodyPivot, "hull", BuildShipHull, true, 0f);

        // Portes de soute : charnières le long des quilles, s'ouvrent vers le bas.
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? 1f : -1f;
            var door = new Node3D { Position = new Vector3(ShipDoorX * side, ShipDoorY, ShipBayPos.Z) };
            _bodyPivot.AddChild(door);
            _sDoor[i] = door;
            Part(door, side > 0 ? "doorR" : "doorL", m =>
            {
                m.Mirror = side < 0;
                m.Use(MachArmor());
                Plate(m, new Transform3D(Basis.Identity, new Vector3(-ShipDoorX / 2, -0.05f, 0)), new Vector3(ShipDoorX / 2, 0.16f, ShipBayHalf.Z), MSteel, 0.06f);
                // Raidisseurs intérieurs et joints extérieurs.
                for (var k = -3; k <= 3; k++)
                    m.Box(new Transform3D(Basis.Identity, new Vector3(-ShipDoorX / 2, 0.16f, k * 2.4f)), new Vector3(ShipDoorX / 2 - 0.15f, 0.08f, 0.1f), MDark);
                for (var k = -1; k <= 1; k++)
                    Groove(m, new Vector3(-0.2f, -0.22f, k * 5.2f - 1.2f), new Vector3(-ShipDoorX + 0.2f, -0.22f, k * 5.2f - 1.2f), Vector3.Down);
                PlateAt(m, new Vector3(-ShipDoorX * 0.55f, -0.28f, 0), new Vector3(0.5f, 0.06f, ShipBayHalf.Z - 1f), MGun);
                m.Use(MachMech());
                for (var k = -1; k <= 1; k++) Cyl(m, new Vector3(0, 0, k * 6f - 0.6f), new Vector3(0, 0, k * 6f + 0.6f), 0.32f, 10, MDark);
            });
        }

        // Nacelles : flammes de sustentation (cônes additifs qui vacillent).
        foreach (var p in ShipPods)
        {
            var f = new Node3D { Position = p + new Vector3(0, -2.6f, 0) };
            _bodyPivot.AddChild(f);
            // Jet : gaine orangée qui s'efface vers le bas, cœur plus clair et plus court.
            f.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.3f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 1, CapTop = false, CapBottom = false },
                MaterialOverride = FlameMat(new Color(1f, 0.42f, 0.14f, 0.55f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, -0.5f, 0),
            });
            f.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.75f, BottomRadius = 0.15f, Height = 1f, RadialSegments = 12, Rings = 1, CapTop = false, CapBottom = false },
                MaterialOverride = FlameMat(new Color(1f, 0.85f, 0.6f, 0.7f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, -0.3f, 0),
                Scale = new Vector3(1f, 0.6f, 1f),
            });
            _sFlames.Add(f);
        }

        // Tourelles laser sous le nez.
        foreach (var p in ShipTurrets)
        {
            var t = new Node3D { Position = p };
            _bodyPivot.AddChild(t);
            Part(t, "turret", m =>
            {
                m.Use(MachMech());
                m.Ellipsoid(new Vector3(0, -0.2f, 0), new Vector3(0.95f, 0.7f, 0.95f), 5, 12, MSteel);
                Cyl(m, new Vector3(0.28f, -0.35f, 0.4f), new Vector3(0.28f, -0.35f, 2.1f), 0.13f, 8, MDark);
                Cyl(m, new Vector3(-0.28f, -0.35f, 0.4f), new Vector3(-0.28f, -0.35f, 2.1f), 0.13f, 8, MDark);
                m.Use(MachRed());
                m.Ellipsoid(new Vector3(0, -0.15f, 0.88f), new Vector3(0.18f, 0.12f, 0.08f), 2, 8, Colors.White);
            });
            _sTurrets.Add(t);
        }
        _sEyes = Part(_bodyPivot, "eyes", m =>
        {
            m.Use(MachRed());
            for (var s = -1; s <= 1; s += 2)
            for (var k = 0; k < 4; k++)
                m.Box(new Transform3D(new Basis(Vector3.Up, 0.5f * s), new Vector3(s * (1.1f + k * 0.62f), 0.35f - k * 0.08f, 19.55f - k * 0.55f)), new Vector3(0.24f, 0.05f, 0.03f), Colors.White);
        }, false);
        _sBayLight = new OmniLight3D { Position = ShipBayPos + new Vector3(0, -1f, 0), LightColor = new Color(1f, 0.2f, 0.1f), LightEnergy = 0f, OmniRange = 16f };
        _bodyPivot.AddChild(_sBayLight);
        AnimateDropShip(0f, 0f, 0f);
    }

    /// <summary>Coque : fuselage haut à arête, quilles de soute, intérieur de soute, pylônes et nacelles, poupe et propulseurs.</summary>
    private static void BuildShipHull(InsectMesh m)
    {
        m.Use(MachArmor());
        var grooves = new[] { 0.16f, 0.165f, 0.17f, 0.33f, 0.335f, 0.34f, 0.5f, 0.505f, 0.51f, 0.67f, 0.675f, 0.68f, 0.84f, 0.845f, 0.85f };
        // Fuselage haut : nez en coin, dos plat à arête, ventre au niveau du plafond de soute.
        BodyLoft(m, -22f, 20.5f,
            new[] { 3.2f, 6.8f, 8.6f, 9.2f, 9.3f, 9.0f, 7.6f, 5.2f, 1.8f },
            new[] { 1.6f, 3.4f, 4.2f, 4.5f, 4.5f, 4.3f, 3.6f, 2.4f, 0.8f },
            new[] { -0.4f, -0.6f, -0.55f, -0.5f, -0.5f, -0.5f, -0.6f, -0.9f, -0.6f },
            Steps(20, grooves), 26, MGun, 3.2f, null, (t, th, p, c) =>
            {
                foreach (var g in new[] { 0.165f, 0.335f, 0.505f, 0.675f, 0.845f })
                    if (Mathf.Abs(t - g) < 0.006f) return MGroove;
                if (p.Y < 0.2f) return MSteel;
                if (Mathf.Abs(p.Y - 2.2f) < 0.1f) return MGroove;
                if (Mathf.Abs(p.X) > 7.5f && p.Y > 0.6f && p.Y < 2.1f && t > 0.2f && t < 0.62f) return MOchre;
                return c;
            });
        // Arête dorsale en plaques et capteur de toit.
        for (var k = -6; k <= 5; k++)
            PlateAt(m, new Vector3(0, 4.55f - Mathf.Abs(k + 0.5f) * 0.03f, k * 2.9f + 0.5f), new Vector3(1.4f, 0.22f, 1.3f), MSteel, 0, 0.03f * k);
        for (var s = -1; s <= 1; s += 2)
        for (var k = -3; k <= 3; k++)
            PlateAt(m, new Vector3(5.2f * s, 3.75f, k * 4.6f), new Vector3(2.2f, 0.16f, 2.0f), MGun, 0, 0, 0.22f * s);
        // Quilles de soute (flancs de la soute) avec leurs plaques.
        for (var s = -1; s <= 1; s += 2)
        {
            Plate(m, new Transform3D(Basis.Identity, new Vector3(6.4f * s, -1.95f, -0.8f)), new Vector3(2.95f, 1.6f, 13.2f), MSteel, 0.3f, 6f, 0.75f, 0.7f);
            for (var k = -2; k <= 2; k++)
                PlateAt(m, new Vector3(9.25f * s, -1.8f, k * 4.8f - 0.8f), new Vector3(0.18f, 1.2f, 2.1f), MGun, 0, 0, -0.18f * s);
            Groove(m, new Vector3(9.45f * s, -1.0f, -12f), new Vector3(9.45f * s, -1.0f, 10.5f), Vector3.Right * s);
        }
        // Fonds de soute (avant et arrière).
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -1.95f, ShipBayPos.Z + ShipBayHalf.Z + 0.6f)), new Vector3(3.5f, 1.45f, 0.6f), MSteel, 0.15f);
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -1.95f, ShipBayPos.Z - ShipBayHalf.Z - 0.6f)), new Vector3(3.5f, 1.45f, 0.6f), MSteel, 0.15f);
        // Menton et capteurs du nez.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -1.6f, 14.5f)), new Vector3(3.6f, 1.2f, 4.2f), MSteel, 0.3f, 6f, 0.7f, 0.6f);
        PlateAt(m, new Vector3(0, 1.0f, 19.3f), new Vector3(3.2f, 0.4f, 0.9f), MSteel, 0, 0.5f);
        // Pylônes et nacelles de sustentation.
        foreach (var p in ShipPods)
        {
            var s = Mathf.Sign(p.X);
            PlateAt(m, new Vector3(p.X * 0.72f, p.Y + 0.9f, p.Z), new Vector3(2.6f, 0.45f, 1.5f), MGun, 0, 0, -0.12f * s, 0.12f);
            Cyl(m, p + Vector3.Down * 2.1f, p + Vector3.Up * 1.8f, 2.35f, 18, MGun, 0.3f, 2.05f);
            for (var k = 0; k < 3; k++) Cyl(m, p + Vector3.Up * (0.6f - k * 0.9f), p + Vector3.Up * (0.75f - k * 0.9f), 2.42f, 18, MGroove);
            PlateAt(m, p + new Vector3(2.2f * s, 0.2f, 0), new Vector3(0.18f, 1.4f, 1.6f), MSteel);
        }
        // Poupe : bloc moteur et deux propulseurs.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 1.6f, -21.4f)), new Vector3(4.2f, 1.9f, 1.4f), MSteel, 0.25f);
        for (var s = -1; s <= 1; s += 2)
        {
            Cyl(m, new Vector3(2.4f * s, 1.5f, -20.5f), new Vector3(2.4f * s, 1.5f, -23.6f), 1.35f, 16, MGun, 0.2f, 1.15f);
            // Ailerons-mâts arrière.
            PlateAt(m, new Vector3(3.0f * s, 5.4f, -17.5f), new Vector3(0.16f, 1.6f, 2.4f), MSteel, 0, 0.35f, 0.3f * s, 0.08f, 0.6f);
        }
        m.Use(MachMech());
        // Intérieur de la soute : plafond nervuré, crochets de largage, rails.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -0.45f, ShipBayPos.Z)), new Vector3(3.4f, 0.12f, ShipBayHalf.Z + 0.3f), MDark, 0.04f);
        for (var k = -4; k <= 4; k++)
        {
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, -0.75f, ShipBayPos.Z + k * 1.9f)), new Vector3(3.3f, 0.22f, 0.14f), MSteel);
            if (k % 2 == 0)
            {
                Cyl(m, new Vector3(0, -0.8f, ShipBayPos.Z + k * 1.9f), new Vector3(0, -2.1f, ShipBayPos.Z + k * 1.9f), 0.18f, 8, MDark);
                m.Cone(new Vector3(0.0f, -2.1f, ShipBayPos.Z + k * 1.9f), new Vector3(0.6f, -2.6f, ShipBayPos.Z + k * 1.9f), 0.14f, 5, MBare, true);
                m.Cone(new Vector3(0.0f, -2.1f, ShipBayPos.Z + k * 1.9f), new Vector3(-0.6f, -2.6f, ShipBayPos.Z + k * 1.9f), 0.14f, 5, MBare, true);
            }
        }
        for (var s = -1; s <= 1; s += 2)
        {
            Cyl(m, new Vector3(2.9f * s, -0.9f, ShipBayPos.Z - ShipBayHalf.Z), new Vector3(2.9f * s, -0.9f, ShipBayPos.Z + ShipBayHalf.Z), 0.16f, 6, MBare);
            for (var k = 0; k < 6; k++) Cable(m, new Vector3(3.3f * s, -0.7f, ShipBayPos.Z - 7f + k * 2.8f), new Vector3(3.3f * s, -0.7f, ShipBayPos.Z - 5.6f + k * 2.8f), 0.07f, 0.5f, MDark);
        }
        // Tuyères des nacelles et des propulseurs.
        foreach (var p in ShipPods)
        {
            m.Torus(p + Vector3.Down * 2.2f, Basis.Identity, 1.7f, 0.4f, 20, 6, MDark);
            for (var k = 0; k < 8; k++)
            {
                var a = k * Mathf.Tau / 8;
                m.Box(new Transform3D(new Basis(Vector3.Up, -a), p + new Vector3(Mathf.Cos(a) * 1.0f, -2.15f, Mathf.Sin(a) * 1.0f)), new Vector3(0.55f, 0.05f, 0.1f), MDark);
            }
        }
        for (var s = -1; s <= 1; s += 2) m.Torus(new Vector3(2.4f * s, 1.5f, -23.6f), new Basis(Vector3.Right, Mathf.Pi / 2), 1.0f, 0.22f, 18, 5, MDark);
        // Antennes.
        Cyl(m, new Vector3(1.2f, 4.6f, 6f), new Vector3(1.3f, 8.0f, 5.4f), 0.07f, 6, MDark);
        Cyl(m, new Vector3(-1.0f, 4.6f, -4f), new Vector3(-1.1f, 6.8f, -4.5f), 0.06f, 6, MDark);
        m.Ellipsoid(new Vector3(0, 4.9f, 10.5f), new Vector3(1.1f, 0.5f, 1.1f), 4, 14, MSteel);
        // Lueurs : tuyères, propulseurs, feux de position, bandes rouges de la soute.
        m.Use(MachHot());
        foreach (var p in ShipPods) m.Ellipsoid(p + Vector3.Down * 2.25f, new Vector3(1.3f, 0.06f, 1.3f), 2, 16, Colors.White);
        for (var s = -1; s <= 1; s += 2) m.Ellipsoid(new Vector3(2.4f * s, 1.5f, -23.55f), new Vector3(0.82f, 0.82f, 0.06f), 2, 14, Colors.White);
        m.Use(MachRed());
        m.Ellipsoid(new Vector3(1.3f, 8.05f, 5.37f), Vector3.One * 0.12f, 2, 6, Colors.White);
        foreach (var p in ShipPods) m.Ellipsoid(p + new Vector3(2.38f * Mathf.Sign(p.X), 0.2f, 0), Vector3.One * 0.16f, 2, 6, Colors.White);
        for (var s = -1; s <= 1; s += 2)
            m.Box(new Transform3D(Basis.Identity, new Vector3(3.2f * s, -0.62f, ShipBayPos.Z)), new Vector3(0.05f, 0.05f, ShipBayHalf.Z - 0.5f), Colors.White);
    }

    // ---------------- Animation ----------------

    /// <summary>Portes de soute, flammes des tuyères, tourelles, léger roulis de vol.</summary>
    public void AnimateDropShip(float dt, float speed, float attack)
    {
        _t += dt;
        _sHatch = Mathf.MoveToward(_sHatch, _sHatchTarget, dt / 2.2f);
        var h = _sHatch * _sHatch * (3f - 2f * _sHatch);
        for (var i = 0; i < 2; i++) _sDoor[i].Rotation = new Vector3(0, 0, (i == 0 ? 1f : -1f) * h * 1.85f);
        _sBayLight.LightEnergy = _dead ? 0f : h * 2.5f;
        if (_dead) return;
        _bodyPivot.Position = new Vector3(0, Mathf.Sin(_t * 0.7f) * 0.35f, 0);
        _bodyPivot.Rotation = new Vector3(Mathf.Sin(_t * 0.45f) * 0.012f, 0, Mathf.Sin(_t * 0.55f) * 0.018f);
        for (var i = 0; i < _sFlames.Count; i++)
        {
            var f = _sFlames[i];
            var flick = 0.85f + 0.15f * Mathf.Sin(_t * 31f + i * 1.9f) + 0.08f * Mathf.Sin(_t * 67f + i);
            var len = (2.2f + speed * 0.08f) * _sThrust * flick;
            f.Scale = new Vector3(1f, Mathf.Max(len, 0.05f), 1f);
        }
        if (_mHasAim)
            foreach (var t in _sTurrets)
            {
                var rel = _bodyPivot.GlobalTransform.AffineInverse() * _mAim - t.Position;
                var yaw = Mathf.Atan2(rel.X, rel.Z);
                var pitch = Mathf.Clamp(Mathf.Atan2(-rel.Y, new Vector2(rel.X, rel.Z).Length()), -0.2f, 1.4f);
                var k = Mathf.Clamp(dt * 3f, 0, 1);
                t.Rotation = new Vector3(Mathf.Lerp(t.Rotation.X, pitch, k), Mathf.LerpAngle(t.Rotation.Y, yaw, k), 0);
            }
    }

    /// <summary>Mort : les flammes s'éteignent, les portes pendent, les feux rouges meurent.</summary>
    public void DropShipDeath()
    {
        _dead = true;
        foreach (var f in _sFlames) f.Visible = false;
        var tw = CreateTween().SetParallel();
        for (var i = 0; i < 2; i++) tw.TweenProperty(_sDoor[i], "rotation", new Vector3(0.1f, 0, (i == 0 ? 1f : -1f) * (0.9f + i * 0.5f)), 1.6f);
        tw.Chain().TweenCallback(Callable.From(() => { if (IsInstanceValid(_sEyes)) _sEyes.MaterialOverride = Mats.Painted(new Color(0.08f, 0.02f, 0.02f), 0.3f, 0.4f); }));
    }
}
