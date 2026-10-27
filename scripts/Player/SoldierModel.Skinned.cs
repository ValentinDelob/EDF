using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Wing Diver modélisée dans Blender (tools/blender/wingdiver, MPFB 2 / MakeHuman CC0) : maillage déformé par un squelette
/// (assets/models/characters/wingdiver.glb). Le squelette suit l'animation procédurale du soldat : les nœuds du modèle
/// procédural (bassin, buste, tête, bras, jambes) restent la référence (mêmes poses, mêmes prises de l'arme, mêmes réseaux),
/// leurs maillages sont cachés et chaque os en recopie la rotation (bras : aussi la position des articulations, pour que les
/// mains tiennent l'arme). Queue de cheval animée par ressort (SpringBoneSimulator3D), expressions du visage en clés de forme
/// (clignement, effort au tir, douleur, cri à terre, concentration en visée).
/// </summary>
public partial class SoldierModel
{
    private const string WingDiverPath = "res://assets/models/characters/wingdiver.glb";
    /// <summary>Échelle du modèle : épaules à la hauteur de celles du soldat procédural (bras de même longueur).</summary>
    private const float WingDiverScale = 1.1f;

    private Node3D _skinRoot;
    private Skeleton3D _skel;
    private readonly List<BoneMap> _maps = new();
    private readonly List<(MeshInstance3D mesh, int[] shapes)> _faces = new();
    private Transform3D _modelFromSkel;
    private bool _skinReady;

    /// <summary>Position de l'os : celle du repos (None), celle de l'articulation procédurale (Exact), ou le décalage de repos
    /// suivi dans le repère du nœud (Offset : bassin).</summary>
    private enum PosMode { None, Exact, Offset }

    private struct BoneMap
    {
        public int Bone;
        public Node3D Node;
        public Quaternion Align;
        public PosMode Pos;
    }

    /// <summary>Douleur (0..1) : flash de dégâts du soldat, pour l'expression du visage.</summary>
    public float Pain;

    private static readonly string[] Expressions = { "blink", "smile", "effort", "pain", "shout", "focus" };

    /// <summary>Wing Diver : charge le modèle à squelette ; faux si absent (le soldat procédural reste affiché).</summary>
    private bool AttachSkinned()
    {
        if (Class != SoldierClass.Airborne || !ResourceLoader.Exists(WingDiverPath)) return false;
        var scene = GD.Load<PackedScene>(WingDiverPath);
        if (scene == null) return false;
        _skinRoot = scene.Instantiate<Node3D>();
        _skinRoot.Scale = Vector3.One * WingDiverScale;
        AddChild(_skinRoot);
        _skel = FindSkeleton(_skinRoot);
        if (_skel == null) { _skinRoot.QueueFree(); _skinRoot = null; return false; }
        // Maillages procéduraux du corps cachés (l'arme, le bouclier et les flammes du réacteur restent).
        foreach (var n in _hips.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            if (!IsUnder(mi, _gun) && (_jetFlames == null || !IsUnder(mi, _jetFlames))) mi.Visible = false;
        }
        foreach (var n in _skinRoot.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            // Formes de commande du squelette de MPFB exportées avec le modèle : jamais affichées.
            if (mi.Name.ToString().StartsWith("Icosphere")) { mi.Visible = false; continue; }
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            WingDiverMaterials(mi);
            if (mi.Mesh is ArrayMesh am && am.GetBlendShapeCount() > 0)
            {
                var idx = new int[Expressions.Length];
                for (var k = 0; k < Expressions.Length; k++) idx[k] = mi.FindBlendShapeByName(Expressions[k]);
                _faces.Add((mi, idx));
            }
        }
        BuildBoneMaps();
        PlaceJetFlames();
        AddPonytail();
        return true;
    }

    private static Skeleton3D FindSkeleton(Node root)
    {
        foreach (var n in root.FindChildren("*", "Skeleton3D", true, false)) return (Skeleton3D)n;
        return null;
    }

    private static bool IsUnder(Node n, Node ancestor)
    {
        for (var p = n; p != null; p = p.GetParent())
            if (p == ancestor) return true;
        return false;
    }

    /// <summary>Transformation d'un nœud dans le repère du modèle (chaîne des parents jusqu'au modèle).</summary>
    private Transform3D ModelXf(Node3D n, bool rest = false)
    {
        var t = rest ? new Transform3D(Basis.Identity, n.Position) : n.Transform;
        for (var p = n.GetParent() as Node3D; p != null && p != this; p = p.GetParent() as Node3D)
            t = (rest && IsRig(p) ? new Transform3D(Basis.Identity, p.Position) : p.Transform) * t;
        return t;
    }

    private bool IsRig(Node3D n) => n == _hips || n == _torso || n == _head || n == _armL || n == _armR || n == _foreL || n == _foreR
                                    || n == _handL || n == _handR || n == _legL || n == _legR || n == _shinL || n == _shinR
                                    || n == _footL || n == _footR;

    /// <summary>Correspondance os → nœud procédural, et alignement des directions au repos (pose en A ↔ bras le long du corps).</summary>
    private void BuildBoneMaps()
    {
        _modelFromSkel = ModelXf(_skel);
        Transform3D BoneRest(int b) => _modelFromSkel * _skel.GetBoneGlobalRest(b);
        Vector3 BoneDir(string bone, string child)
        {
            var b = _skel.FindBone(bone);
            var c = _skel.FindBone(child);
            if (b < 0 || c < 0) return Vector3.Down;
            return (BoneRest(c).Origin - BoneRest(b).Origin).Normalized();
        }
        void Map(string bone, Node3D node, PosMode pos, string child = null, Vector3? nodeDir = null)
        {
            var b = _skel.FindBone(bone);
            if (b < 0 || node == null) return;
            var align = Quaternion.Identity;
            if (child != null && nodeDir is { } nd)
            {
                var bd = BoneDir(bone, child);
                align = ArcQuat(bd, nd.Normalized());
            }
            _maps.Add(new BoneMap { Bone = b, Node = node, Align = align, Pos = pos });
        }
        Map("pelvis", _hips, PosMode.Offset);
        Map("spine_01", _hips, PosMode.None);
        Map("spine_02", _torso, PosMode.None);
        Map("spine_03", _torso, PosMode.None);
        Map("clavicle_l", _torso, PosMode.None);
        Map("clavicle_r", _torso, PosMode.None);
        Map("neck_01", _torso, PosMode.None);
        Map("head", _head, PosMode.None);
        foreach (var (s, arm, fore, hand, leg, shin, foot) in new[] { ("l", _armL, _foreL, _handL, _legL, _shinL, _footL), ("r", _armR, _foreR, _handR, _legR, _shinR, _footR) })
        {
            Map($"upperarm_{s}", arm, PosMode.Exact, $"lowerarm_{s}", fore.Position);
            Map($"lowerarm_{s}", fore, PosMode.Exact, $"hand_{s}", hand.Position);
            Map($"hand_{s}", hand, PosMode.Exact, $"middle_01_{s}", Vector3.Down);
            Map($"thigh_{s}", leg, PosMode.None, $"calf_{s}", shin.Position);
            Map($"calf_{s}", shin, PosMode.None, $"foot_{s}", foot.Position);
            Map($"foot_{s}", foot, PosMode.None);
        }
        // Ordre du squelette (parents d'abord).
        _maps.Sort((a, b) => a.Bone.CompareTo(b.Bone));
        _skinReady = true;
    }

    private static Quaternion ArcQuat(Vector3 from, Vector3 to)
    {
        var d = from.Dot(to);
        if (d > 0.9999f) return Quaternion.Identity;
        if (d < -0.9999f)
        {
            var axis = from.Cross(Vector3.Right);
            if (axis.LengthSquared() < 1e-6f) axis = from.Cross(Vector3.Up);
            return new Quaternion(axis.Normalized(), Mathf.Pi);
        }
        return new Quaternion(from.Cross(to).Normalized(), Mathf.Acos(Mathf.Clamp(d, -1f, 1f)));
    }

    /// <summary>Pose du squelette depuis les nœuds procéduraux (après <see cref="Animate"/>).</summary>
    private void UpdateSkin(float dt)
    {
        if (!_skinReady) return;
        var skelFromModel = _modelFromSkel.AffineInverse();
        foreach (var m in _maps)
        {
            var node = ModelXf(m.Node);
            var nodeRest = ModelXf(m.Node, true);
            var boneRest = _modelFromSkel * _skel.GetBoneGlobalRest(m.Bone);
            // Rotation : celle du nœud depuis son repos, appliquée à l'os (aligné sur la direction du nœud au repos).
            var rot = node.Basis.Orthonormalized() * nodeRest.Basis.Orthonormalized().Inverse() * new Basis(m.Align)
                      * boneRest.Basis.Orthonormalized();
            var cur = _modelFromSkel * _skel.GetBoneGlobalPose(m.Bone);
            var pos = m.Pos switch
            {
                PosMode.Exact => node.Origin,
                PosMode.Offset => node.Origin + node.Basis.Orthonormalized() * (boneRest.Origin - nodeRest.Origin),
                _ => cur.Origin,
            };
            var g = new Transform3D(rot, pos);
            // Repère du squelette (sans l'échelle : seule la position est ramenée).
            var gs = new Transform3D(skelFromModel.Basis.Orthonormalized() * g.Basis, skelFromModel * g.Origin);
            _skel.SetBoneGlobalPose(m.Bone, gs);
        }
        CurlFingers();
        UpdateFace(dt);
    }

    // ───────── Mains ─────────

    private int[] _fingersL, _fingersR, _thumbs;
    /// <summary>Axe de flexion des phalanges (repère local des os de MPFB : l'os suit Y, la flexion tourne autour de X).</summary>
    private static readonly Vector3 CurlAxis = Vector3.Right;

    /// <summary>Doigts refermés sur la poignée (main gauche relâchée quand elle quitte l'arme).</summary>
    private void CurlFingers()
    {
        _fingersL ??= FingerBones("l");
        _fingersR ??= FingerBones("r");
        _thumbs ??= new[] { "thumb_01_l", "thumb_02_l", "thumb_03_l", "thumb_01_r", "thumb_02_r", "thumb_03_r" }
            .Select(_skel.FindBone).Where(b => b >= 0).ToArray();
        void Curl(int[] bones, float amount)
        {
            foreach (var b in bones)
            {
                var rest = _skel.GetBoneRest(b).Basis.GetRotationQuaternion();
                _skel.SetBonePoseRotation(b, rest * new Quaternion(CurlAxis, amount));
            }
        }
        Curl(_fingersR, 1.15f);
        Curl(_fingersL, Mathf.Lerp(1.05f, 0.35f, _freeL));
        Curl(_thumbs, 0.45f);
    }

    private int[] FingerBones(string side)
    {
        var list = new List<int>();
        foreach (var f in new[] { "index", "middle", "ring", "pinky" })
            for (var k = 1; k <= 3; k++)
            {
                var b = _skel.FindBone($"{f}_0{k}_{side}");
                if (b >= 0) list.Add(b);
            }
        return list.ToArray();
    }

    // ───────── Matières ─────────

    private static void WingDiverMaterials(MeshInstance3D mi)
    {
        var mesh = mi.Mesh;
        if (mesh == null) return;
        for (var i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            if (mesh.SurfaceGetMaterial(i) is not BaseMaterial3D src) continue;
            var m = (BaseMaterial3D)src.Duplicate();
            switch (src.ResourceName)
            {
                case "WD_Skin":
                    m.Roughness = 0.55f;
                    m.SubsurfScatterEnabled = true;
                    m.SubsurfScatterStrength = 0.35f;
                    break;
                case "WD_Hair":
                    m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash;
                    m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                    m.Roughness = 0.45f;
                    break;
                case "WD_Brows" or "WD_Lashes":
                    m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
                    m.AlphaScissorThreshold = 0.35f;
                    m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                    break;
                case "WD_Eyes":
                    m.Roughness = 0.08f;
                    m.ClearcoatEnabled = true;
                    m.Clearcoat = 1f;
                    break;
                case "WD_Suit":
                    m.Roughness = 0.45f;
                    m.RimEnabled = true;
                    m.Rim = 0.15f;
                    break;
                case "ArmorWhite" or "BootWhite":
                    m.AlbedoColor = new Color(0.84f, 0.85f, 0.87f);
                    m.Roughness = 0.24f;
                    m.ClearcoatEnabled = true;
                    m.Clearcoat = 0.6f;
                    break;
                case "ArmorNavy":
                    m.AlbedoColor = new Color(0.02f, 0.04f, 0.11f);
                    m.Roughness = 0.3f;
                    m.ClearcoatEnabled = true;
                    m.Clearcoat = 0.4f;
                    break;
                case "ArmorDark":
                    m.AlbedoColor = new Color(0.03f, 0.032f, 0.036f);
                    m.Roughness = 0.38f;
                    m.Metallic = 0.6f;
                    break;
                case "Scalp":
                    // Cuir chevelu sous les cheveux : châtain sombre, mat (les trous entre les mèches ne montrent pas la peau).
                    m.AlbedoTexture = null;
                    m.AlbedoColor = new Color(0.075f, 0.05f, 0.035f);
                    m.Roughness = 0.75f;
                    break;
                case "Collar" or "Glove" or "BootSole":
                    m.AlbedoColor = new Color(0.015f, 0.015f, 0.017f);
                    m.Roughness = src.ResourceName == "Glove" ? 0.33f : 0.6f;
                    break;
                case "JetGlow":
                    m.AlbedoColor = new Color(0.35f, 0.75f, 1f);
                    m.EmissionEnabled = true;
                    m.Emission = new Color(0.35f, 0.75f, 1f);
                    m.EmissionEnergyMultiplier = 5f;
                    break;
            }
            mi.SetSurfaceOverrideMaterial(i, m);
        }
    }

    // ───────── Réacteur, cheveux ─────────

    /// <summary>Flammes du réacteur sous les buses de l'unité de vol (repères JetNozzle_l / _r du modèle).</summary>
    private void PlaceJetFlames()
    {
        if (_jetFlames == null) return;
        var l = _skinRoot.FindChild("JetNozzle_l", true, false) as Node3D;
        var r = _skinRoot.FindChild("JetNozzle_r", true, false) as Node3D;
        if (l == null || r == null) return;
        var torsoRest = ModelXf(_torso, true);
        var pl = torsoRest.AffineInverse() * ModelXf(l);
        var pr = torsoRest.AffineInverse() * ModelXf(r);
        _jetFlames.GetParent()?.RemoveChild(_jetFlames);
        _torso.AddChild(_jetFlames);
        _jetFlames.Position = (pl.Origin + pr.Origin) / 2 + new Vector3(0, 0.08f, 0);
        var half = Mathf.Abs(pl.Origin.X - pr.Origin.X) / 2;
        foreach (var c in _jetFlames.GetChildren())
            if (c is MeshInstance3D f)
            {
                f.Position = new Vector3(Mathf.Sign(f.Position.X) * half, -0.12f, 0);
                f.Scale = Vector3.One * 0.6f;
            }
    }

    /// <summary>Queue de cheval : chaîne hair_01 → hair_04 animée par ressort (inertie de la course, du vol, des poussées).</summary>
    private void AddPonytail()
    {
        if (_skel.FindBone("hair_01") < 0 || _skel.FindBone("hair_04") < 0) return;
        var sim = new SpringBoneSimulator3D { SettingCount = 1 };
        _skel.AddChild(sim);
        sim.SetRootBoneName(0, "hair_01");
        sim.SetEndBoneName(0, "hair_04");
        sim.SetExtendEndBone(0, true);
        sim.SetEndBoneLength(0, 0.06f);
        sim.SetStiffness(0, 0.6f);
        sim.SetDrag(0, 0.35f);
        sim.SetGravity(0, 0.9f);
        sim.SetRadius(0, 0.03f);
    }

    // ───────── Visage ─────────

    private float _blinkT = 2f, _blink, _effort, _pain, _shout, _focus;

    /// <summary>Expressions : clignement régulier, effort au tir et à l'envol, douleur aux dégâts, cri à terre, concentration en visée.</summary>
    private void UpdateFace(float dt)
    {
        if (_faces.Count == 0) return;
        var p = _lastPose;
        _blinkT -= dt;
        if (_blinkT <= 0)
        {
            _blink = 1f;
            _blinkT = (float)GD.RandRange(2.5, 5.5);
        }
        _blink = Mathf.MoveToward(_blink, 0f, dt * 7f);
        _effort = Mathf.MoveToward(_effort, p.recoil > 0.1f || p.fly ? 0.8f : 0f, dt * 4f);
        _pain = Mathf.MoveToward(_pain, Mathf.Clamp(Pain * 2f, 0f, 1f), dt * 6f);
        _shout = Mathf.MoveToward(_shout, p.down ? 0.9f : 0f, dt * 3f);
        _focus = Mathf.MoveToward(_focus, p.aim && p.recoil < 0.1f ? 0.7f : 0f, dt * 4f);
        // Les expressions fortes priment (pas de sourire ou de concentration pendant la douleur).
        var focus = _focus * (1f - _pain) * (1f - _shout);
        var effort = _effort * (1f - _pain);
        var blink = Mathf.Max(_blink, _pain * 0.3f);
        var values = new[] { blink, 0f, effort, _pain, _shout, focus };
        foreach (var (mesh, idx) in _faces)
            for (var k = 0; k < idx.Length; k++)
                if (idx[k] >= 0) mesh.SetBlendShapeValue(idx[k], values[k]);
    }
}
