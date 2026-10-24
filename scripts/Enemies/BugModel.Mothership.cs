using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Vaisseau-mère (boss final) : coque en pointe de flèche de 420 m sur 300, épaisse d'une trentaine de mètres, en vol
/// stationnaire au-dessus de la capitale. Dessous (ce que voient les soldats) : plaques ventrales, tranchées lumineuses
/// rayonnant depuis le canon principal, quatre soutes latérales à portes, dix tourelles (quatre escamotées jusqu'à la
/// deuxième phase) ; canon principal : couronne blindée à huit pétales qui s'ouvrent sur un cœur incandescent et ses
/// quatre griffes émettrices. Dessus : arête dorsale, tours, antennes ; poupe : six tuyères, lames latérales.
/// Une seule pièce de coque fusionnée (≈ 30 000 triangles, sans ombre : à 165 m d'altitude, l'ombre d'un soleil bas
/// tomberait loin hors de la carte), pétales, tourelles et portes en pièces animées partagées.
/// </summary>
public partial class BugModel
{
    public bool IsMothership => Type == EnemyType.Mothership;

    /// <summary>Altitude de vol stationnaire (centre de la coque) et altitude d'arrivée.</summary>
    public const float MsAlt = 165f, MsArrivalAlt = 430f;

    /// <summary>Dessous de la coque (y, repère du modèle) au point (x, z) : section superelliptique aplatie du loft.</summary>
    public static float MsUnder(float z, float x)
    {
        var t = MsTop(z);
        var b = MsBottom(z);
        var w = MsHalfWidth(z);
        var u = Mathf.Clamp(Mathf.Abs(x) / Mathf.Max(w, 1f), 0f, 1f);
        return (t - b) / 2f - (t + b) / 2f * 0.75f * Mathf.Pow(1f - Mathf.Pow(u, 3.2f), 1f / 3.2f);
    }

    /// <summary>Canon principal : centre de la couronne (repère du modèle), rayon de la couronne, rayon du cœur.</summary>
    public static readonly Vector3 MsIris = new(0, MsUnder(18f, 0f) - 1.2f, 18f);
    public const float MsIrisR = 32f, MsCoreR = 12.5f;
    /// <summary>Cœur : rentré dans la couronne fermée (au-dessus de son centre), descendu sous elle quand le canon s'ouvre.</summary>
    public const float MsCoreUp = 5f, MsCoreDrop = 13f;

    private static readonly Vector2[] TurretXZ =
    {
        new(-78f, 92f), new(78f, 92f), new(-108f, -10f), new(108f, -10f), new(-70f, -120f), new(70f, -120f),
        new(0f, 120f), new(-46f, -48f), new(46f, -48f), new(0f, -150f),
    };

    /// <summary>Tourelles (repère du modèle, sous la coque) : 0 à 5 déployées d'emblée, 6 à 9 à la deuxième phase.</summary>
    public static readonly Vector3[] MsTurrets = System.Array.ConvertAll(TurretXZ, p => new Vector3(p.X, MsUnder(p.Y, p.X) - 2.4f, p.Y));
    public const int MsTurretCount = 10, MsTurretsInitial = 6;

    private static readonly Vector2[] BayXZ = { new(-122f, 50f), new(122f, 50f), new(-128f, -78f), new(128f, -78f) };

    /// <summary>Soutes de lancement (centre de la porte, repère du modèle).</summary>
    public static readonly Vector3[] MsBays = System.Array.ConvertAll(BayXZ, p => new Vector3(p.X, MsUnder(p.Y, p.X) - 0.5f, p.Y));

    private Node3D _msBody;
    private readonly Node3D[] _msPetal = new Node3D[8];
    private readonly Node3D[] _msTurret = new Node3D[MsTurretCount];
    private readonly Node3D[] _msTurretGun = new Node3D[MsTurretCount];
    private readonly Node3D[] _msBayDoor = new Node3D[MsBays.Length * 2];
    private Node3D _msCore;
    private MeshInstance3D _msCoreMesh, _msBeam, _msBeamCore, _msSight, _msEngines;
    private OmniLight3D _msCoreLight, _msImpactLight;
    private StandardMaterial3D _msCoreMat, _msEngineMat;
    private ShaderMaterial _msBeamMat, _msBeamCoreMat, _msSightMat;
    private float _msT, _msIris, _msIrisTarget;
    private readonly float[] _msDeploy = new float[MsTurretCount];
    private readonly float[] _msDeployTarget = new float[MsTurretCount];
    private readonly bool[] _msTurretDead = new bool[MsTurretCount];
    private readonly float[] _msBay = new float[MsBays.Length];
    private readonly float[] _msBayTarget = new float[MsBays.Length];
    private bool _msDying;

    /// <summary>Charge du canon (0..1) : lueur du cœur, lumière ; 1 pendant le tir.</summary>
    public float MsCharge;
    /// <summary>Tir en cours : faisceau du cœur au point d'impact (monde).</summary>
    public bool MsFiring;
    public Vector3 MsImpact;
    /// <summary>Rayon de visée pendant la charge (fin trait vers le point visé), 0 = masqué.</summary>
    public float MsSight;

    // ---------------- Accès (monde) ----------------

    /// <summary>Centre du cœur (monde), descendu quand le canon est ouvert.</summary>
    public Vector3 MsCorePos => _msCore != null && _msCore.IsInsideTree() ? _msCore.GlobalPosition : GlobalTransform * (MsIris + Vector3.Up * (MsCoreUp - (MsCoreUp + MsCoreDrop) * _msIris));
    public float MsIrisOpen => _msIris;
    public Vector3 MsTurretPos(int i) => _msTurret[i] != null && _msTurret[i].IsInsideTree() ? _msTurret[i].GlobalPosition : GlobalTransform * MsTurrets[i];
    public Vector3 MsTurretMuzzle(int i) => _msTurretGun[i] != null && _msTurretGun[i].IsInsideTree() ? _msTurretGun[i].GlobalTransform * new Vector3(0, 0, 9.5f) : MsTurretPos(i);
    public Vector3 MsBayPos(int i) => GlobalTransform * (MsBays[i] + Vector3.Down * 4f);
    public bool MsTurretDeployed(int i) => _msDeploy[i] > 0.95f && !_msTurretDead[i];

    public void MsSetIris(bool open) => _msIrisTarget = open ? 1f : 0f;
    public void MsDeployTurret(int i, bool on) => _msDeployTarget[i] = on ? 1f : 0f;
    public void MsSetBay(int i, bool open) => _msBayTarget[i] = open ? 1f : 0f;

    // ---------------- Construction ----------------

    private void BuildMothership()
    {
        _flying = true;
        _msBody = new Node3D { Name = "Body" };
        AddChild(_msBody);
        var hull = Part(_msBody, "hull", BuildMsHull, false);
        hull.ExtraCullMargin = 50f;
        _msEngineMat = (StandardMaterial3D)Mats.Glow(new Color(1f, 0.38f, 0.12f), 4f).Duplicate();
        _msEngines = Part(_msBody, "engines", BuildMsEngines, false);
        _msEngines.MaterialOverride = _msEngineMat;

        // Couronne du canon : huit pétales pivotant sur leur bord extérieur.
        var iris = new Node3D { Name = "Iris", Position = MsIris };
        _msBody.AddChild(iris);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            var pivot = new Node3D { Position = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (MsIrisR - 1f), Rotation = new Vector3(0, -a, 0) };
            iris.AddChild(pivot);
            var hinge = new Node3D();
            pivot.AddChild(hinge);
            Part(hinge, "petal", BuildMsPetal, false);
            _msPetal[i] = hinge;
        }
        // Cœur : sphère d'énergie (matériau propre, lueur animée), griffes émettrices, lumière.
        _msCore = new Node3D { Name = "Core", Position = MsIris };
        _msBody.AddChild(_msCore);
        // Matière éclairée (non « unshaded ») : l'émission dépasse 1 et alimente le halo (glow) de l'environnement.
        _msCoreMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.05f, 0.12f),
            EmissionEnabled = true,
            Emission = new Color(0.72f, 0.45f, 1f),
            EmissionEnergyMultiplier = 2f,
            Roughness = 0.2f,
            RimEnabled = true,
            Rim = 1f,
            RimTint = 1f,
        };
        _msCoreMesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = MsCoreR, Height = MsCoreR * 2f, RadialSegments = 32, Rings = 16 }, MaterialOverride = _msCoreMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _msCore.AddChild(_msCoreMesh);
        Part(_msCore, "claws", BuildMsClaws, false);
        _msCoreLight = new OmniLight3D { LightColor = new Color(0.75f, 0.5f, 1f), LightEnergy = 0f, OmniRange = 160f, OmniAttenuation = 0.8f, ShadowEnabled = false, Position = Vector3.Down * 14f };
        _msCore.AddChild(_msCoreLight);

        // Tourelles : socle en dôme et affût jumelé orientable ; les quatre dernières restent dans la coque au début.
        for (var i = 0; i < MsTurretCount; i++)
        {
            var t = new Node3D { Name = $"Turret{i}", Position = MsTurrets[i] };
            _msBody.AddChild(t);
            Part(t, "turretbase", BuildMsTurretBase, false);
            var gun = new Node3D { Position = Vector3.Down * 3.4f };
            t.AddChild(gun);
            Part(gun, "turretgun", BuildMsTurretGun, false);
            _msTurret[i] = t;
            _msTurretGun[i] = gun;
            _msDeploy[i] = _msDeployTarget[i] = i < MsTurretsInitial ? 1f : 0f;
        }
        // Soutes : deux portes à charnières par soute.
        for (var b = 0; b < MsBays.Length; b++)
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? 1f : -1f;
                var hinge = new Node3D { Position = MsBays[b] + new Vector3(side * 9f, 0.4f, 0) };
                _msBody.AddChild(hinge);
                var door = new Node3D { Position = new Vector3(-side * 4.5f, 0, 0) };
                hinge.AddChild(door);
                Part(door, "baydoor", m =>
                {
                    m.Use(MachArmor());
                    PlateAt(m, Vector3.Zero, new Vector3(4.5f, 0.5f, 13f), MSteel, 0, 0, 0, 0.3f);
                    for (var k = -2; k <= 2; k++) m.Box(new Transform3D(Basis.Identity, new Vector3(0, -0.55f, k * 5f)), new Vector3(4.2f, 0.12f, 0.25f), MDark);
                }, false);
                _msBayDoor[b * 2 + s] = hinge;
            }

        // Faisceau du canon (gaine additive violette, âme blanche), trait de visée, lumière de l'impact.
        _msBeamMat = BeamMaterial(new Color(0.62f, 0.32f, 1f), 3.2f);
        _msBeamCoreMat = BeamMaterial(new Color(0.95f, 0.9f, 1f), 9f);
        _msSightMat = BeamMaterial(new Color(1f, 0.22f, 0.4f), 3f);
        _msBeam = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 9f, BottomRadius = 11f, Height = 1f, RadialSegments = 24, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = _msBeamMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true, Visible = false };
        _msBeamCore = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 3.2f, BottomRadius = 4.2f, Height = 1f, RadialSegments = 16, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = _msBeamCoreMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true, Visible = false };
        _msSight = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.6f, Height = 1f, RadialSegments = 8, Rings = 1, CapTop = false, CapBottom = false }, MaterialOverride = _msSightMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true, Visible = false };
        AddChild(_msBeam);
        AddChild(_msBeamCore);
        AddChild(_msSight);
        _msImpactLight = new OmniLight3D { LightColor = new Color(0.8f, 0.55f, 1f), LightEnergy = 0f, OmniRange = 70f, ShadowEnabled = false, TopLevel = true };
        AddChild(_msImpactLight);
    }

    /// <summary>
    /// Colonne d'énergie : additive, plus dense au cœur de la colonne qu'à ses bords (incidence), flux qui défile vers le
    /// bas (bruit), intensité HDR (halo). Paramètres : couleur, énergie, opacité globale.
    /// </summary>
    private const string BeamCode = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, fog_disabled;
uniform vec4 color : source_color = vec4(0.7, 0.4, 1.0, 1.0);
uniform float energy = 4.0;
uniform float alpha = 1.0;
uniform float flow = 1.0;
float h(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float n(vec2 p) { vec2 i = floor(p), f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(h(i), h(i + vec2(1, 0)), u.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), u.x), u.y); }
void fragment() {
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float body = pow(facing, 1.6);
	float streak = n(vec2(UV.x * 18.0, UV.y * 6.0 + TIME * 3.5 * flow)) * 0.6 + n(vec2(UV.x * 5.0, UV.y * 2.0 + TIME * 1.7 * flow)) * 0.4;
	float k = body * (0.55 + 0.75 * streak) * alpha;
	ALBEDO = color.rgb * energy * k;
	ALPHA = clamp(k, 0.0, 1.0);
}";

    private static Shader _beamShader;

    private static ShaderMaterial BeamMaterial(Color c, float energy)
    {
        _beamShader ??= new Shader { Code = BeamCode };
        var m = new ShaderMaterial { Shader = _beamShader };
        m.SetShaderParameter("color", c);
        m.SetShaderParameter("energy", energy);
        return m;
    }

    /// <summary>Demi-envergure de la coque (plan en pointe de flèche) pour une abscisse z (−210 poupe … 210 proue).</summary>
    public static float MsHalfWidth(float z) => InsectMesh.Spline(new[] { 95f, 140f, 150f, 150f, 147f, 135f, 112f, 80f, 40f, 6f }, (z + 210f) / 420f);

    private static float MsTop(float z) => InsectMesh.Spline(new[] { 14f, 18f, 19f, 19f, 18f, 16f, 13f, 9f, 5f, 2f }, (z + 210f) / 420f);
    private static float MsBottom(float z) => InsectMesh.Spline(new[] { 9f, 11f, 12f, 12f, 12f, 11f, 10f, 8f, 5f, 2f }, (z + 210f) / 420f);

    /// <summary>Coque : loft en lentille aplatie, plaques ventrales et dorsales, tranchées lumineuses, soutes, superstructures.</summary>
    private static void BuildMsHull(InsectMesh m)
    {
        var rng = new RandomNumberGenerator { Seed = 9001 };
        float R(float a, float b) => rng.RandfRange(a, b);
        var hullDark = new Color(0.2f, 0.205f, 0.215f);
        var hullMid = new Color(0.28f, 0.285f, 0.295f);
        m.Use(MachArmor());
        // Loft principal (dessus bombé, dessous plus plat) le long de z.
        var rings = new List<InsectMesh.Ring>();
        rings.Add(new InsectMesh.Ring(new Vector3(0, 0, -212f), Vector3.Right, Vector3.Up, 0, 0, hullDark));
        for (var i = 0; i <= 28; i++)
        {
            var z = Mathf.Lerp(-210f, 210f, i / 28f);
            var t = MsTop(z);
            var b = MsBottom(z);
            rings.Add(new InsectMesh.Ring(new Vector3(0, (t - b) / 2f, z), Vector3.Right, Vector3.Up, MsHalfWidth(z), (t + b) / 2f, hullDark));
        }
        rings.Add(new InsectMesh.Ring(new Vector3(0, 0, 212f), Vector3.Right, Vector3.Up, 0, 0, hullDark));
        m.Loft(rings, 44, 3.2f, 0.25f, (i, th, p, c) =>
        {
            // Bandes plus claires et joints sombres par secteurs.
            var band = Mathf.PosMod(p.Z, 36f) < 1.2f || Mathf.PosMod(p.X, 30f) < 0.8f;
            return band ? MGroove : (Mathf.PosMod(Mathf.Floor(p.Z / 36f) + Mathf.Floor(p.X / 30f), 3f) < 1f ? hullMid : c);
        });
        // Plaques ventrales (ce que voient les soldats) : pavage irrégulier, plaques en relief et en creux.
        for (var z = -196f; z < 200f; z += 17f)
        {
            // Plaques jointives alignées (joints de 1,2 m) : un bordé lisible, pas un semis de cubes.
            var hw = MsHalfWidth(z) - 10f;
            var x0 = -Mathf.Floor(hw / 17f) * 17f;
            for (var x = x0; x < hw; x += 17f)
            {
                if (new Vector2(x - MsIris.X, z - MsIris.Z).Length() < MsIrisR + 8f) continue;
                var bay = false;
                foreach (var bp in MsBays) if (Mathf.Abs(x - bp.X) < 14f && Mathf.Abs(z - bp.Z) < 18f) bay = true;
                if (bay) continue;
                var hh = R(0.2f, 0.5f);
                var y = MsUnder(z, x) - hh + 0.25f;
                var col = R(0, 1) < 0.3f ? hullMid : R(0, 1) < 0.6f ? MSteel : hullDark;
                m.Box(new Transform3D(Basis.Identity, new Vector3(x, y, z)), new Vector3(7.9f, hh, 7.9f), col);
                // Modules d'équipement en relief (alignés sur la trame).
                if (R(0, 1) < 0.14f)
                    m.Box(new Transform3D(Basis.Identity, new Vector3(x + (R(0, 1) < 0.5f ? -3f : 3f), y - hh - 0.8f, z)), new Vector3(R(1.5f, 2.6f), 0.9f, R(4f, 6.5f)), MDark);
            }
        }
        // Grandes tranchées longitudinales (violet) et ports d'émission autour du canon : l'échelle se lit du sol.
        foreach (var s in new[] { -1f, 1f })
            for (var z = -180f; z < 160f; z += 10f)
            {
                if (Mathf.Abs(z - MsIris.Z) < MsIrisR + 6f && Mathf.Abs(s * 42f) < MsIrisR + 14f) continue;
                m.Box(new Transform3D(Basis.Identity, new Vector3(s * 42f, MsUnder(z, s * 42f) - 0.6f, z)), new Vector3(2.4f, 0.9f, 5.2f), MDark);
            }
        // Quilles ventrales et nervures rayonnant depuis le canon.
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12f + 0.13f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (var d = MsIrisR + 10f; d < 200f; d += 14f)
            {
                var p = MsIris + dir * d;
                if (Mathf.Abs(p.X) > MsHalfWidth(p.Z) - 12f || Mathf.Abs(p.Z) > 200f) break;
                var y = MsUnder(p.Z, p.X) - 2.6f;
                m.Box(new Transform3D(new Basis(Vector3.Up, -a), new Vector3(p.X, y, p.Z)), new Vector3(7.2f, 2.4f, 1.4f), MDark);
            }
        }
        // Soutes : puits sombres encadrés.
        foreach (var bp in MsBays)
        {
            m.Box(new Transform3D(Basis.Identity, bp + Vector3.Up * 3f), new Vector3(9.5f, 4f, 13.5f), MDark);
            m.Box(new Transform3D(Basis.Identity, bp + new Vector3(10.6f, -0.4f, 0)), new Vector3(1.4f, 1.6f, 15f), MSteel);
            m.Box(new Transform3D(Basis.Identity, bp + new Vector3(-10.6f, -0.4f, 0)), new Vector3(1.4f, 1.6f, 15f), MSteel);
        }
        // Couronne du canon (logement), puits sombre.
        m.Torus(MsIris + Vector3.Up * 1.5f, Basis.Identity, MsIrisR + 3f, 5.5f, 48, 10, MSteel);
        m.Torus(MsIris + Vector3.Down * 1.5f, Basis.Identity, MsIrisR + 6f, 2.2f, 48, 6, MDark);
        m.Use(MachMech());
        m.Ellipsoid(MsIris + Vector3.Up * 6f, new Vector3(MsIrisR, 9f, MsIrisR), 6, 32, MDark);
        // Dessus : arête dorsale en gradins, trois tours de commandement, antennes, plaques.
        m.Use(MachArmor());
        for (var z = -180f; z < 170f; z += 22f)
        {
            var t = MsTop(z);
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, t + 2.5f, z)), new Vector3(16f - Mathf.Abs(z) * 0.03f, 4f, 10.5f), MSteel);
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, t + 6.5f, z + 4f)), new Vector3(7f, 2.6f, 6f), hullMid);
        }
        foreach (var (z, h) in new[] { (-60f, 34f), (-110f, 26f), (20f, 22f) })
        {
            var t = MsTop(z);
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, t + h / 2f, z)), new Vector3(14f, h / 2f, 18f), hullDark);
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, t + h + 2f, z - 4f)), new Vector3(20f, 2f, 10f), MSteel);
            m.Box(new Transform3D(Basis.Identity, new Vector3(0, t + h * 0.7f, z + 18f)), new Vector3(9f, h * 0.3f, 2f), MSteel);
        }
        for (var i = 0; i < 260; i++)
        {
            var z = R(-200f, 190f);
            var hw = MsHalfWidth(z) * 0.82f;
            var x = R(-hw, hw);
            var y = MsTop(z) * (1f - Mathf.Pow(Mathf.Abs(x) / (MsHalfWidth(z) + 0.01f), 3.2f)) * 0.92f;
            var col = R(0, 1) < 0.3f ? hullMid : R(0, 1) < 0.6f ? MSteel : hullDark;
            m.Box(new Transform3D(new Basis(Vector3.Up, R(-0.2f, 0.2f)), new Vector3(x, y + R(0.3f, 1.2f), z)), new Vector3(R(2f, 9f), R(0.5f, 2.6f), R(2f, 9f)), col);
        }
        // Lames latérales inclinées vers le bas (silhouette hostile).
        foreach (var s in new[] { -1f, 1f })
            for (var i = 0; i < 6; i++)
            {
                var z = -150f + i * 52f;
                var hw = MsHalfWidth(z);
                var basis = new Basis(Vector3.Up, s * (0.25f + i * 0.04f)) * new Basis(Vector3.Back, s * 0.32f);
                PlateAt(m, new Vector3(s * (hw + 10f), -2f, z), new Vector3(16f, 1.2f, 6f), MSteel, s * (0.25f + i * 0.04f), 0, s * 0.32f, 0.5f, 0.5f);
                m.Box(new Transform3D(basis, new Vector3(s * (hw + 4f), -1f, z)), new Vector3(6f, 2.4f, 8f), hullDark);
            }
        // Antennes et mâts.
        for (var i = 0; i < 18; i++)
        {
            var z = R(-170f, 120f);
            var x = R(-0.5f, 0.5f) * MsHalfWidth(z);
            var y = MsTop(z) * 0.9f;
            m.Cone(new Vector3(x, y, z), new Vector3(x + R(-1, 1), y + R(10f, 28f), z + R(-2, 2)), R(0.5f, 1.2f), 5, MSteel);
        }
        // Poupe : bloc moteur.
        m.Box(new Transform3D(Basis.Identity, new Vector3(0, 2f, -206f)), new Vector3(80f, 12f, 8f), hullDark);
        foreach (var x in new[] { -60f, -36f, -12f, 12f, 36f, 60f })
        {
            Cyl(m, new Vector3(x, 2f, -204f), new Vector3(x, 2f, -218f), 9.5f, 18, MSteel, 0.8f, 8f);
            m.Torus(new Vector3(x, 2f, -218f), new Basis(Vector3.Right, Mathf.Pi / 2), 8.6f, 1.2f, 18, 5, MDark);
        }
        // Balises et filets lumineux (rouge) : bords, tranchées, dessous.
        m.Use(MachRed());
        for (var z = -200f; z < 205f; z += 18f)
            foreach (var s in new[] { -1f, 1f })
                m.Ellipsoid(new Vector3(s * (MsHalfWidth(z) - 1f), -1f, z), Vector3.One * 1.1f, 2, 6, Colors.White);
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12f + 0.13f + Mathf.Pi / 12f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (var d = MsIrisR + 14f; d < 190f; d += 9f)
            {
                var p = MsIris + dir * d;
                if (Mathf.Abs(p.X) > MsHalfWidth(p.Z) - 14f || Mathf.Abs(p.Z) > 196f) break;
                m.Box(new Transform3D(new Basis(Vector3.Up, -a), new Vector3(p.X, MsUnder(p.Z, p.X) - 0.4f, p.Z)), new Vector3(0.9f, 0.3f, 3.6f), Colors.White);
            }
        }
        // Galeries éclairées le long des bords, sous la coque (deux lignes qui suivent le contour : elles donnent l'échelle).
        m.Use(Mats.Glow(new Color(0.72f, 0.62f, 1f), 2.6f));
        foreach (var s in new[] { -1f, 1f })
            foreach (var inset in new[] { 15f, 27f })
                for (var z = -186f; z < 176f; z += 12f)
                {
                    if (RuinKit.Hash(z, inset + s * 7f, 911) < 0.18f) continue; // tronçons éteints
                    var z1 = z + 10f;
                    var a = new Vector3(s * (MsHalfWidth(z) - inset), 0, z);
                    var b = new Vector3(s * (MsHalfWidth(z1) - inset), 0, z1);
                    if (Mathf.Abs(a.X) < 14f || Mathf.Abs(b.X) < 14f) continue;
                    a.Y = MsUnder(a.Z, a.X) - 0.25f;
                    b.Y = MsUnder(b.Z, b.X) - 0.25f;
                    var d = b - a;
                    var rot = new Basis(Vector3.Up, Mathf.Atan2(d.X, d.Z));
                    m.Box(new Transform3D(rot, (a + b) / 2), new Vector3(0.45f, 0.2f, d.Length() / 2f), Colors.White);
                }
        foreach (var bp in MsBays)
        {
            m.Box(new Transform3D(Basis.Identity, bp + new Vector3(0, -0.6f, 14.2f)), new Vector3(9f, 0.25f, 0.25f), Colors.White);
            m.Box(new Transform3D(Basis.Identity, bp + new Vector3(0, -0.6f, -14.2f)), new Vector3(9f, 0.25f, 0.25f), Colors.White);
        }
        // Anneau lumineux de la couronne (violet : énergie du cœur).
        m.Use(Mats.Glow(new Color(0.6f, 0.3f, 1f), 3.5f));
        m.Torus(MsIris + Vector3.Down * 3.2f, Basis.Identity, MsIrisR + 2.5f, 1.1f, 48, 4, Colors.White);
        foreach (var s in new[] { -1f, 1f })
            for (var z = -180f; z < 160f; z += 10f)
            {
                if (Mathf.Abs(z - MsIris.Z) < MsIrisR + 6f && Mathf.Abs(s * 42f) < MsIrisR + 14f) continue;
                m.Box(new Transform3D(Basis.Identity, new Vector3(s * 42f, MsUnder(z, s * 42f) - 1.55f, z)), new Vector3(0.7f, 0.08f, 4.4f), Colors.White);
            }
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6f + Mathf.Pi / 6f;
            var p = MsIris + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 64f;
            m.Ellipsoid(new Vector3(p.X, MsUnder(p.Z, p.X) - 0.9f, p.Z), new Vector3(4.5f, 0.35f, 4.5f), 2, 14, Colors.White);
        }
        m.Use(MachArmor());
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6f + Mathf.Pi / 6f;
            var p = MsIris + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 64f;
            m.Torus(new Vector3(p.X, MsUnder(p.Z, p.X) - 0.9f, p.Z), Basis.Identity, 5.6f, 1.2f, 20, 5, MSteel);
        }
    }

    /// <summary>Tuyères (lueur orange, intensité animée par la matière propre du nœud).</summary>
    private static void BuildMsEngines(InsectMesh m)
    {
        m.Use(MachHot());
        foreach (var x in new[] { -60f, -36f, -12f, 12f, 36f, 60f })
            m.Ellipsoid(new Vector3(x, 2f, -217.5f), new Vector3(7.6f, 7.6f, 0.5f), 2, 18, Colors.White);
        // Plots de sustentation sous la coque.
        foreach (var (x, z) in new[] { (-70f, 40f), (70f, 40f), (-80f, -90f), (80f, -90f) })
            m.Ellipsoid(new Vector3(x, MsUnder(z, x) - 1.3f, z), new Vector3(11f, 0.5f, 11f), 2, 18, Colors.White);
    }

    /// <summary>Pétale de la couronne : coin blindé (repère : charnière au bord extérieur, pointe vers le centre).</summary>
    private static void BuildMsPetal(InsectMesh m)
    {
        m.Use(MachArmor());
        // Le pétale part de la charnière (x = 0) vers le centre de la couronne (x négatif, local).
        var len = MsIrisR - 1f;
        var outline = new[] { new Vector3(0, 0, -12.6f), new Vector3(-len, -9f, -0.6f), new Vector3(-len, -9f, 0.6f), new Vector3(0, 0, 12.6f) };
        PlateAt(m, new Vector3(-len * 0.45f, -4.2f, 0), new Vector3(len * 0.52f, 1.4f, 9f), MSteel, 0, 0, -0.27f, 0.6f, 0.25f);
        m.Box(new Transform3D(new Basis(Vector3.Back, -0.27f), new Vector3(-len * 0.3f, -3.4f, 0)), new Vector3(len * 0.3f, 0.6f, 2f), MDark);
        m.Box(new Transform3D(Basis.Identity, new Vector3(-1f, 0, 0)), new Vector3(1.6f, 1.6f, 11f), MDark);
        _ = outline;
        m.Use(MachRed());
        m.Box(new Transform3D(new Basis(Vector3.Back, -0.27f), new Vector3(-len * 0.55f, -6.1f, 0)), new Vector3(len * 0.25f, 0.15f, 0.3f), Colors.White);
    }

    /// <summary>Griffes émettrices autour du cœur (quatre crochets pointés vers le bas).</summary>
    private static void BuildMsClaws(InsectMesh m)
    {
        m.Use(MachMech());
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4f + Mathf.Pi / 4f;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var pts = new[] { d * 10f + Vector3.Up * 9f, d * 16f + Vector3.Up * 2f, d * 15f + Vector3.Down * 8f, d * 9f + Vector3.Down * 16f };
            m.Hook(pts, 2.4f, 8, MSteel, Vector3.Up);
        }
        m.Torus(Vector3.Up * 8f, Basis.Identity, 13f, 1.8f, 24, 6, MDark);
    }

    private static void BuildMsTurretBase(InsectMesh m)
    {
        m.Use(MachArmor());
        m.Ellipsoid(Vector3.Down * 1f, new Vector3(6.5f, 3.2f, 6.5f), 5, 16, MSteel);
        m.Torus(Vector3.Up * 0.5f, Basis.Identity, 6.8f, 0.9f, 16, 5, MDark);
        m.Use(MachRed());
        m.Ellipsoid(Vector3.Down * 3.9f, new Vector3(1.4f, 0.4f, 1.4f), 2, 8, Colors.White);
    }

    private static void BuildMsTurretGun(InsectMesh m)
    {
        m.Use(MachMech());
        m.Ellipsoid(Vector3.Zero, new Vector3(3f, 2.2f, 3.6f), 4, 12, MSteel);
        foreach (var s in new[] { -1f, 1f })
        {
            Cyl(m, new Vector3(s * 1.2f, -0.2f, 1.5f), new Vector3(s * 1.2f, -0.2f, 9.5f), 0.55f, 8, MDark);
            Cyl(m, new Vector3(s * 1.2f, -0.2f, 5f), new Vector3(s * 1.2f, -0.2f, 6f), 0.8f, 8, MSteel);
        }
        m.Use(MachHot());
        foreach (var s in new[] { -1f, 1f }) m.Ellipsoid(new Vector3(s * 1.2f, -0.2f, 9.5f), Vector3.One * 0.45f, 2, 6, Colors.White);
    }

    // ---------------- Animation ----------------

    /// <summary>Tourelles orientées vers ce point (monde), ou rien.</summary>
    public Vector3? MsAim;

    /// <summary>Bercement, couronne, cœur et faisceau, tourelles, soutes, tuyères (tous les pairs).</summary>
    public void AnimateMothership(float dt)
    {
        _msT += dt;
        if (!_msDying)
        {
            _msBody.Position = new Vector3(0, Mathf.Sin(_msT * 0.21f) * 1.6f, 0);
            _msBody.Rotation = new Vector3(Mathf.Sin(_msT * 0.13f) * 0.008f, 0, Mathf.Sin(_msT * 0.17f) * 0.011f);
        }
        // Couronne : les pétales s'abaissent (charnière extérieure) puis s'écartent.
        _msIris = Mathf.MoveToward(_msIris, _msIrisTarget, dt / 2.4f);
        var k = _msIris * _msIris * (3f - 2f * _msIris);
        for (var i = 0; i < 8; i++) _msPetal[i].Rotation = new Vector3(0, 0, -k * 1.15f);
        _msCore.Position = MsIris + Vector3.Up * (MsCoreUp - (MsCoreUp + MsCoreDrop) * k);
        var pulse = 0.5f + 0.5f * Mathf.Sin(_msT * (3f + MsCharge * 14f));
        var energy = _msDying ? 0f : 1.5f + k * 3f + MsCharge * (18f + 10f * pulse) + (MsFiring ? 20f : 0f);
        _msCoreMat.EmissionEnergyMultiplier = energy;
        _msCoreMesh.Scale = Vector3.One * (0.85f + 0.15f * k + MsCharge * 0.12f * pulse);
        _msCoreLight.LightEnergy = _msDying ? 0f : k * 1.5f + MsCharge * 6f + (MsFiring ? 10f : 0f);
        // Faisceau : colonne du cœur au point d'impact.
        var from = _msCore.GlobalPosition + Vector3.Down * MsCoreR * 0.8f;
        if (MsFiring && !_msDying)
        {
            var to = MsImpact;
            var d = to - from;
            var len = d.Length();
            if (len > 1f)
            {
                var wob = 1f + 0.08f * Mathf.Sin(_msT * 37f) + 0.05f * Mathf.Sin(_msT * 71f);
                var mid = (from + to) / 2;
                _msBeam.GlobalTransform = new Transform3D(ColumnBasis(d / len, len, wob), mid);
                _msBeamCore.GlobalTransform = new Transform3D(ColumnBasis(d / len, len, wob * 0.9f), mid);
                _msBeam.Visible = _msBeamCore.Visible = true;
            }
            _msImpactLight.GlobalPosition = to + Vector3.Up * 6f;
            _msImpactLight.LightEnergy = 12f + 4f * pulse;
        }
        else
        {
            _msBeam.Visible = _msBeamCore.Visible = false;
            _msImpactLight.LightEnergy = 0f;
        }
        // Trait de visée pendant la charge (rouge, de plus en plus franc).
        if (MsSight > 0.01f && !_msDying)
        {
            var to = MsImpact;
            var d = to - from;
            var len = Mathf.Max(d.Length(), 0.01f);
            _msSight.GlobalTransform = new Transform3D(ColumnBasis(d / len, len, 1f + MsSight * 2.5f), (from + to) / 2);
            _msSightMat.SetShaderParameter("alpha", 0.2f + 0.8f * MsSight * (0.6f + 0.4f * pulse));
            _msSight.Visible = true;
        }
        else _msSight.Visible = false;
        // Tourelles : sortie de la coque, visée.
        for (var i = 0; i < MsTurretCount; i++)
        {
            _msDeploy[i] = Mathf.MoveToward(_msDeploy[i], _msDeployTarget[i], dt / 3f);
            var t = _msTurret[i];
            t.Position = MsTurrets[i] + Vector3.Up * (1f - _msDeploy[i]) * 8f;
            if (_msTurretDead[i] || MsAim is not { } aim) continue;
            var g = _msTurretGun[i];
            var rel = t.GlobalTransform.AffineInverse() * aim;
            var yaw = Mathf.Atan2(rel.X, rel.Z);
            var pitch = Mathf.Clamp(Mathf.Atan2(-rel.Y - 3.4f, new Vector2(rel.X, rel.Z).Length()), -0.2f, 1.45f);
            var kk = Mathf.Clamp(dt * 1.6f, 0f, 1f);
            g.Rotation = new Vector3(Mathf.Lerp(g.Rotation.X, pitch, kk), Mathf.LerpAngle(g.Rotation.Y, yaw, kk), 0);
        }
        // Soutes.
        for (var b = 0; b < MsBays.Length; b++)
        {
            _msBay[b] = Mathf.MoveToward(_msBay[b], _msBayTarget[b], dt / 1.8f);
            var h = _msBay[b] * _msBay[b] * (3f - 2f * _msBay[b]);
            _msBayDoor[b * 2].Rotation = new Vector3(0, 0, -h * 1.5f);
            _msBayDoor[b * 2 + 1].Rotation = new Vector3(0, 0, h * 1.5f);
        }
        // Tuyères qui vacillent.
        _msEngineMat.EmissionEnergyMultiplier = _msDying ? Mathf.Max(0f, _msEngineMat.EmissionEnergyMultiplier - dt * 1.2f) : 3.2f + 0.6f * Mathf.Sin(_msT * 9f) + 0.4f * Mathf.Sin(_msT * 23f);
    }

    /// <summary>Repère d'une colonne (cylindre unitaire le long de Y local) : axe <paramref name="dir"/>, longueur, épaisseur.</summary>
    private static Basis ColumnBasis(Vector3 dir, float len, float width)
    {
        var x = dir.Cross(Vector3.Forward);
        x = x.LengthSquared() > 1e-4f ? x.Normalized() : dir.Cross(Vector3.Right).Normalized();
        var z = x.Cross(dir).Normalized();
        return new Basis(x * width, dir * len, z * width);
    }

    /// <summary>Tourelle détruite : affût arraché, socle noirci (tous les pairs).</summary>
    public void MsKillTurret(int i)
    {
        if (_msTurretDead[i]) return;
        _msTurretDead[i] = true;
        _msTurretGun[i].Visible = false;
        foreach (var n in _msTurret[i].GetChildren())
            if (n is MeshInstance3D mi) mi.MaterialOverride = Mats.Charred();
    }

    public bool MsTurretDead(int i) => _msTurretDead[i];

    /// <summary>Mort : le cœur s'éteint, les pétales pendent, les feux de position et les tuyères meurent.</summary>
    public void MothershipDeath()
    {
        _msDying = true;
        MsFiring = false;
        MsSight = 0f;
        _msIrisTarget = 0.55f;
        _msCoreMat.EmissionEnergyMultiplier = 0f;
        _msCoreMat.AlbedoColor = new Color(0.1f, 0.08f, 0.12f);
        _msCoreMat.Emission = new Color(0.2f, 0.1f, 0.3f);
        foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)node;
            if (mi.Mesh == null) continue;
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                if (mi.Mesh.SurfaceGetMaterial(s) == MachRed()) mi.SetSurfaceOverrideMaterial(s, Mats.Painted(new Color(0.08f, 0.02f, 0.02f), 0.3f, 0.4f));
        }
    }

    /// <summary>
    /// Préchauffage pendant le déploiement si la mission compte le vaisseau-mère : maillages construits et un exemplaire
    /// minuscule devant la caméra, canon ouvert, faisceau et trait de visée visibles (shaders compilés).
    /// </summary>
    public static void PrewarmMothership(Camera3D cam, MissionDef mission)
    {
        var has = false;
        foreach (var ph in mission.Phases)
            foreach (var w in ph.Waves)
                foreach (var (t, _) in w.Groups)
                    if (t == EnemyType.Mothership) has = true;
        if (!has || cam == null) return;
        var root = new Node3D { Name = "MothershipPrewarm", Position = new Vector3(0.3f, 0, -4f), Scale = Vector3.One * 0.0015f };
        cam.AddChild(root);
        var m = Create(EnemyType.Mothership);
        root.AddChild(m);
        m._msIris = m._msIrisTarget = 1f;
        m.MsCharge = 1f;
        foreach (var fx in new Node3D[] { m._msBeam, m._msBeamCore, m._msSight })
        {
            fx.TopLevel = false;
            fx.Visible = true;
        }
        cam.GetTree().CreateTimer(1.5f).Timeout += () => { if (IsInstanceValid(root)) root.QueueFree(); };
    }
}
