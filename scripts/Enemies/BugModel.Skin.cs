using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Insectes « cuits » en maillages à os (performances, spécification §13.3 : 300 ennemis actifs).
///
/// À la construction, chaque pièce rigide est un MeshInstance3D enfant d'un nœud articulé (hanche, fémur, genou, tête...).
/// Avec 300 fourmis, cela fait ~6 000 instances de rendu dont la transformation change à chaque image : notifications
/// différées, mises à jour du serveur de rendu, appels de dessin. Après la construction, <see cref="Bake"/> :
/// <list type="bullet">
/// <item>fusionne les pièces en deux maillages partagés par type (avec ombre : corps, tête, gastre, mandibules ;
/// sans ombre : pattes, antennes, ailes), chaque sommet lié (poids 1) à l'os de sa pièce, dans le repère de la pièce ;</item>
/// <item>sort l'armature (les mêmes nœuds <see cref="Bone"/>, que l'animation continue de piloter) de l'arbre de scène :
/// ses poses ne vivent plus que côté C# (aucun appel au moteur, aucune notification) ;</item>
/// <item>à chaque animation (<see cref="SyncSkin"/>), compose les repères des nœuds dans l'espace du modèle et
/// les envoie au squelette du serveur de rendu (le GPU déforme les deux maillages).</item>
/// </list>
/// Rendu identique : mêmes maillages, mêmes matériaux, mêmes ombres ; la portée de visibilité des pièces fines (70-90 m)
/// est conservée à 90 m pour le groupe sans ombre. Fondu caméra, flash d'impact et chitine dorée des élites agissent
/// sur les deux instances comme avant sur les pièces.
/// </summary>
public partial class BugModel
{
    /// <summary>Maillages fusionnés d'un type (partagés), et pièces d'origine pour <see cref="Bounds"/>.</summary>
    private sealed class SkinData
    {
        public readonly List<ArrayMesh> Meshes = new();
        public readonly List<bool> Shadow = new();
        public readonly List<float> Range = new();
        /// <summary>Boîte de chaque pièce dans le repère de son nœud, et index du nœud dans l'armature.</summary>
        public readonly List<(Aabb Box, int Node)> Parts = new();
        public int Bones;
    }

    private static readonly Dictionary<EnemyType, SkinData> SkinCache = new();
    private static readonly HashSet<EnemyType> SkinChecked = new();

    /// <summary>Insectes cuits (les machines, la forteresse et le nid gardent leurs pièces : lumières, formes recalées, peu nombreux).</summary>
    private static bool Bakes(EnemyType t) => t is EnemyType.BlackAnt or EnemyType.RedAnt or EnemyType.AcidAnt or EnemyType.Scavenger
        or EnemyType.Queen or EnemyType.Wasp or EnemyType.Spider or EnemyType.Centipede or EnemyType.Drone;

    /// <summary>Racine (hors scène) de l'armature d'animation, ou null si le modèle n'est pas cuit.</summary>
    private Node3D _rig;
    /// <summary>Nœuds de l'armature, parents avant enfants.</summary>
    private Bone[] _rigNodes;
    /// <summary>Parent de chaque nœud dans <see cref="_rigNodes"/> (-1 : enfant direct du modèle).</summary>
    private int[] _rigParent;
    /// <summary>Os de rendu porté par chaque nœud (-1 : nœud sans pièce).</summary>
    private int[] _rigBone;
    /// <summary>Repère de chaque nœud dans l'espace du modèle (dernière synchronisation).</summary>
    private Transform3D[] _rigXf;
    /// <summary>Dernier repère envoyé pour chaque os (on n'envoie que ce qui change).</summary>
    private Transform3D[] _boneSent;
    private Rid _skeleton;
    private MeshInstance3D[] _skinMeshes;
    private SkinData _skin;

    /// <summary>Le modèle est-il cuit (maillages à os) ?</summary>
    public bool Baked => _rig != null;

    private void Bake()
    {
        if (!Bakes(Type)) return;
        // Armature : enfants directs de type Bone (pas les lumières), ne contenant que des Bone et des pièces.
        var roots = new List<Bone>();
        foreach (var c in GetChildren())
        {
            if (c.GetType() == typeof(Node3D)) return; // nœud ordinaire : structure inattendue, on garde les pièces
            if (c is not Bone) continue;
            if (!PureRig(c)) return; // structure inattendue : on garde les pièces
            roots.Add((Bone)c);
        }
        if (roots.Count == 0) return;

        var nodes = new List<Bone>();
        var parents = new List<int>();
        var parts = new List<(MeshInstance3D Mi, int Node)>();
        foreach (var r in roots) Collect(r, -1, nodes, parents, parts);
        if (parts.Count == 0) return;

        // Os de rendu : un par nœud portant au moins une pièce.
        var bone = new int[nodes.Count];
        for (var i = 0; i < bone.Length; i++) bone[i] = -1;
        var bones = 0;
        foreach (var (_, n) in parts)
            if (bone[n] < 0) bone[n] = bones++;

        if (!SkinCache.TryGetValue(Type, out var data))
        {
            data = BuildSkin(parts, bone, bones);
            SkinCache[Type] = data;
        }
        if (data.Bones != bones) return; // ne devrait pas arriver : même construction pour chaque instance d'un type

        // Les pièces disparaissent ; l'armature sort de la scène.
        foreach (var (mi, _) in parts)
        {
            mi.GetParent().RemoveChild(mi);
            mi.Free();
        }
        _rig = new Node3D { Name = "Rig" };
        foreach (var r in roots)
        {
            RemoveChild(r);
            _rig.AddChild(r);
        }
        // Poses tenues côté C# désormais : l'animation n'appelle plus le moteur.
        var check = SkinChecked.Add(Type);
        foreach (var n in nodes)
        {
            n.Freeze();
            // Contrôle (une fois par type) : la composition C# reproduit celle de Node3D.
            if (!check) continue;
            var a = n.Transform;
            var b = ((Node3D)n).Transform;
            if (!a.IsEqualApprox(b)) GD.PushWarning($"Bone {Type}/{n.Name} : repère C# {a} ≠ moteur {b}");
        }
        if (check)
        {
            // Rotation sur les trois axes et échelle non uniforme.
            var t = new Bone { Position = new Vector3(0.4f, -1.2f, 2f), Rotation = new Vector3(0.3f, -1.1f, 0.7f), Scale = new Vector3(1.2f, 0.8f, 1.1f) };
            t.Freeze();
            if (!t.Transform.IsEqualApprox(((Node3D)t).Transform)) GD.PushWarning($"Bone : repère C# {t.Transform} ≠ moteur {((Node3D)t).Transform}");
            t.Free();
        }
        _rigNodes = nodes.ToArray();
        _rigParent = parents.ToArray();
        _rigBone = bone;
        _rigXf = new Transform3D[nodes.Count];
        _boneSent = new Transform3D[bones];
        _skin = data;
        _skeleton = RenderingServer.SkeletonCreate();
        RenderingServer.SkeletonAllocateData(_skeleton, bones);
        _skinMeshes = new MeshInstance3D[data.Meshes.Count];
        for (var g = 0; g < data.Meshes.Count; g++)
        {
            var mi = new MeshInstance3D
            {
                Name = data.Shadow[g] ? "Skin" : "SkinFine",
                Mesh = data.Meshes[g],
                CastShadow = data.Shadow[g] ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                Skeleton = new NodePath(),
            };
            if (data.Range[g] > 0) mi.VisibilityRangeEnd = data.Range[g];
            AddChild(mi);
            _skinMeshes[g] = mi;
        }
        SyncSkin(true);
    }

    private static bool PureRig(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is MeshInstance3D) { if (c.GetChildCount() > 0) return false; continue; }
            if (c is not Bone || !PureRig(c)) return false;
        }
        return true;
    }

    private static void Collect(Bone n, int parent, List<Bone> nodes, List<int> parents, List<(MeshInstance3D, int)> parts)
    {
        var idx = nodes.Count;
        nodes.Add(n);
        parents.Add(parent);
        foreach (var c in n.GetChildren())
        {
            if (c is MeshInstance3D mi) parts.Add((mi, idx));
            else Collect((Bone)c, idx, nodes, parents, parts);
        }
    }

    /// <summary>Fusion des pièces d'un type : un maillage par groupe (avec / sans ombre), une surface par matériau.</summary>
    private static SkinData BuildSkin(List<(MeshInstance3D Mi, int Node)> parts, int[] bone, int bones)
    {
        var data = new SkinData { Bones = bones };
        foreach (var shadow in new[] { true, false })
        {
            var surfs = new Dictionary<Material, SkinSurface>();
            var order = new List<Material>();
            var range = 0f;
            var allRanged = true;
            foreach (var (mi, node) in parts)
            {
                if ((mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off) != shadow || mi.Mesh == null) continue;
                if (mi.VisibilityRangeEnd > 0) range = Mathf.Max(range, mi.VisibilityRangeEnd);
                else allRanged = false;
                var xf = mi.Transform;
                var mesh = mi.Mesh;
                data.Parts.Add((xf * mesh.GetAabb(), node));
                for (var s = 0; s < mesh.GetSurfaceCount(); s++)
                {
                    var mat = mi.MaterialOverride ?? mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                    if (!surfs.TryGetValue(mat, out var dst))
                    {
                        surfs[mat] = dst = new SkinSurface();
                        order.Add(mat);
                    }
                    dst.Append(mesh.SurfaceGetArrays(s), xf, bone[node]);
                }
            }
            if (order.Count == 0) continue;
            var am = new ArrayMesh();
            foreach (var mat in order)
            {
                am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfs[mat].Arrays());
                am.SurfaceSetMaterial(am.GetSurfaceCount() - 1, mat);
            }
            data.Meshes.Add(am);
            data.Shadow.Add(shadow);
            data.Range.Add(allRanged ? range : 0f);
        }
        return data;
    }

    private sealed class SkinSurface
    {
        private readonly List<Vector3> _v = new(), _n = new();
        private readonly List<float> _t = new(), _w = new();
        private readonly List<Color> _c = new();
        private readonly List<int> _i = new(), _b = new();

        public void Append(Godot.Collections.Array a, Transform3D xf, int bone)
        {
            var v = a[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var n = a[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var t = a[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
            var c = a[(int)Mesh.ArrayType.Color].AsColorArray();
            var idx = a[(int)Mesh.ArrayType.Index].AsInt32Array();
            var basis = xf.Basis;
            var nb = basis.Inverse().Transposed();
            var start = _v.Count;
            for (var k = 0; k < v.Length; k++)
            {
                _v.Add(xf * v[k]);
                _n.Add(n.Length > k ? (nb * n[k]).Normalized() : Vector3.Up);
                if (t.Length >= (k + 1) * 4)
                {
                    var tv = (basis * new Vector3(t[k * 4], t[k * 4 + 1], t[k * 4 + 2])).Normalized();
                    _t.Add(tv.X); _t.Add(tv.Y); _t.Add(tv.Z); _t.Add(t[k * 4 + 3]);
                }
                else { _t.Add(1f); _t.Add(0f); _t.Add(0f); _t.Add(1f); }
                _c.Add(c.Length > k ? c[k] : Colors.White);
                _b.Add(bone); _b.Add(0); _b.Add(0); _b.Add(0);
                _w.Add(1f); _w.Add(0f); _w.Add(0f); _w.Add(0f);
            }
            if (idx.Length > 0) foreach (var i in idx) _i.Add(start + i);
            else for (var k = 0; k < v.Length; k++) _i.Add(start + k);
        }

        public Godot.Collections.Array Arrays()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
            arrays[(int)Mesh.ArrayType.Tangent] = _t.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _c.ToArray();
            arrays[(int)Mesh.ArrayType.Bones] = _b.ToArray();
            arrays[(int)Mesh.ArrayType.Weights] = _w.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = _i.ToArray();
            return arrays;
        }
    }

    /// <summary>
    /// Compose les repères de l'armature dans l'espace du modèle et envoie au squelette de rendu les os qui ont bougé.
    /// Appelé après chaque animation (et après la pose de mort).
    /// </summary>
    public void SyncSkin(bool force = false)
    {
        if (_rigNodes == null) return;
        for (var i = 0; i < _rigNodes.Length; i++)
        {
            var local = _rigNodes[i].Transform;
            var p = _rigParent[i];
            var xf = p < 0 ? local : Mul(_rigXf[p], local);
            _rigXf[i] = xf;
            var b = _rigBone[i];
            if (b < 0 || (!force && Same(xf, _boneSent[b]))) continue;
            _boneSent[b] = xf;
            RenderingServer.SkeletonBoneSetTransform(_skeleton, b, xf);
        }
    }

    /// <summary>a · b développé (l'opérateur de Transform3D passe par des propriétés de lignes, coûteuses hors optimisation).</summary>
    /// <remarks>Basis range ses lignes dans des champs (Row0..2) ; ses axes X, Y, Z sont des propriétés recalculées.</remarks>
    private static Transform3D Mul(in Transform3D a, in Transform3D b)
    {
        Transform3D r;
        r.Basis.Row0 = Row(a.Basis.Row0, b.Basis);
        r.Basis.Row1 = Row(a.Basis.Row1, b.Basis);
        r.Basis.Row2 = Row(a.Basis.Row2, b.Basis);
        var o = b.Origin;
        r.Origin = new Vector3(
            a.Basis.Row0.X * o.X + a.Basis.Row0.Y * o.Y + a.Basis.Row0.Z * o.Z + a.Origin.X,
            a.Basis.Row1.X * o.X + a.Basis.Row1.Y * o.Y + a.Basis.Row1.Z * o.Z + a.Origin.Y,
            a.Basis.Row2.X * o.X + a.Basis.Row2.Y * o.Y + a.Basis.Row2.Z * o.Z + a.Origin.Z);
        return r;
    }

    /// <summary>Ligne de a · B à partir de la ligne correspondante de a.</summary>
    private static Vector3 Row(in Vector3 ar, in Basis b) => new(
        ar.X * b.Row0.X + ar.Y * b.Row1.X + ar.Z * b.Row2.X,
        ar.X * b.Row0.Y + ar.Y * b.Row1.Y + ar.Z * b.Row2.Y,
        ar.X * b.Row0.Z + ar.Y * b.Row1.Z + ar.Z * b.Row2.Z);

    /// <summary>Égalité exacte (un os immobile garde exactement le même repère : rien à envoyer).</summary>
    private static bool Same(in Transform3D a, in Transform3D b) =>
        a.Origin == b.Origin && a.Basis.Row0 == b.Basis.Row0 && a.Basis.Row1 == b.Basis.Row1 && a.Basis.Row2 == b.Basis.Row2;

    /// <summary>Attache le squelette de rendu aux instances (chaque entrée en scène : MeshInstance3D le détache).</summary>
    private void AttachSkin()
    {
        if (_skinMeshes == null) return;
        foreach (var mi in _skinMeshes) RenderingServer.InstanceAttachSkeleton(mi.GetInstance(), _skeleton);
    }

    /// <summary>Repère d'un nœud de l'armature dans l'espace du modèle (calculé à la demande, l'armature étant hors scène).</summary>
    private Transform3D RigLocal(Node3D n)
    {
        var i = System.Array.IndexOf(_rigNodes, n);
        if (i < 0) return Transform3D.Identity;
        var xf = _rigNodes[i].Transform;
        for (var p = _rigParent[i]; p >= 0; p = _rigParent[p]) xf = _rigNodes[p].Transform * xf;
        return xf;
    }

    /// <summary>Position globale d'un nœud du modèle (armature cuite comprise).</summary>
    public Vector3 RigGlobalPosition(Node3D n) => _rig == null ? n.GlobalPosition : GlobalTransform * RigLocal(n).Origin;

    /// <summary>Point de prise entre les mandibules, en coordonnées globales (fourmis).</summary>
    public Vector3 GrabSocketPosition => RigGlobalPosition(GrabSocket);

    /// <summary>Centre de la tête, en coordonnées globales (fourmis).</summary>
    public Vector3 HeadCenterPosition => RigGlobalPosition(HeadCenter);

    public override void _Notification(int what)
    {
        if (what == NotificationEnterTree && _skinMeshes != null) Callable.From(AttachSkin).CallDeferred();
        else if (what == NotificationPredelete && _rig != null)
        {
            if (IsInstanceValid(_rig)) _rig.Free();
            if (_skeleton.IsValid) RenderingServer.FreeRid(_skeleton);
        }
    }
}
