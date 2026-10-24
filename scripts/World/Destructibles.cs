using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Famille d'accessoire destructible (comportement, points de vie, résistance à l'écrasement).</summary>
public enum PropKind : byte { None, Small, Furniture, Pole, Tree, Shelter, Barrier, Car, Van, Truck, Wreck, Container }

/// <summary>Origine des dégâts infligés à un accessoire.</summary>
public enum PropHit : byte { Bullet = 0, Blast = 1, Crush = 2 }

/// <summary>Emplacement d'un MultiMesh (rempli à la finalisation du lot) : une instance est masquée ou déplacée sans nœud.</summary>
public sealed class InstSlot
{
    public MultiMesh Mm;
    public Vector3 Origin;
}

/// <summary>Accessoire destructible enregistré (aucun nœud par accessoire intact : MultiMesh + forme de collision).</summary>
public sealed class PropEntry
{
    public int Id;
    public PropKind Kind;
    /// <summary>Repère monde du pied (échelle comprise), mis à jour par les poussées.</summary>
    public Transform3D Xf;
    /// <summary>Boîte englobante du maillage (repère local, sans échelle).</summary>
    public Aabb Box;
    public Mesh Mesh;
    public Color Tint = Colors.White;
    public float Hp, MaxHp;
    /// <summary>État logique (hôte) : 0 intact, 1 abîmé, 2 en feu, 3 détruit.</summary>
    public byte State;
    /// <summary>État affiché (tous les pairs).</summary>
    public byte Shown;
    public bool Flattened, FlatShown, Burnt;
    public long LastBy;
    public InstSlot Slot;
    public int Index = -1;
    public Node3D Node;
    public List<CollisionShape3D> Shapes;
    public List<Node3D> Extras;
    public List<Node> Fx;
    /// <summary>Écrasement vertical (1 intact, ~0,4 aplati) et bosse (petite rotation) du visuel.</summary>
    public float Squash = 1f;
    public Basis Dent = Basis.Identity;

    public float Scale => Xf.Basis.X.Length();
    public float Height => Mathf.Max(Box.End.Y * Scale * Squash, 0.3f);
    /// <summary>Repère du visuel : pied, bosse, écrasement.</summary>
    public Transform3D Visual => new(Xf.Basis * Dent * Basis.FromScale(new Vector3(1f, Squash, 1f)), Xf.Origin);
    public Vector3 Center => Xf * Box.GetCenter();
    /// <summary>Demi-encombrement horizontal.</summary>
    public float Radius => Mathf.Max(Box.Size.X, Box.Size.Z) * 0.5f * Scale;
    /// <summary>Rayon d'emprise au sol (tronc d'un arbre, fût d'un poteau).</summary>
    public float FootR => Kind is PropKind.Pole or PropKind.Tree ? 0.45f : Mathf.Min(Box.Size.X, Box.Size.Z) * 0.5f * Scale;
    public bool IsVehicle => Kind is PropKind.Car or PropKind.Van or PropKind.Truck;
}

/// <summary>
/// Destruction du mobilier urbain et des véhicules (voir docs/ARCHITECTURE.md, « Destruction du décor »).
///
/// Les générateurs de carte enregistrent chaque accessoire destructible dans l'ordre de pose (identifiants
/// déterministes, identiques chez tous les pairs) : emplacement dans un MultiMesh ou nœud visuel, formes de
/// collision propres dans des corps statiques par cellule de 50 m (désactiver une forme ne reconstruit que sa
/// cellule). L'hôte applique les dégâts (balles, explosions, écrasement par les gros insectes, le char,
/// l'hélicoptère, la forteresse, l'effondrement d'un immeuble) et diffuse des événements compacts
/// (identifiant + code d'événement, direction et amplitude sur un entier) ; chaque pair joue alors l'effet :
/// poteaux et arbres qui basculent, mobilier qui éclate en débris, voitures qui fument, brûlent puis explosent,
/// épaves aplaties, conteneurs poussés.
/// </summary>
public partial class Destructibles : Node3D
{
    /// <summary>Registre en cours de remplissage (pendant la génération de la carte de la mission), sinon null.</summary>
    public static Destructibles Building { get; private set; }

    public const int OpDamaged = 1, OpBurning = 2, OpDestroy = 3, OpFlatten = 4, OpPush = 5;
    private const float BodyCell = 50f;
    private const float GridCell = 8f;

    private readonly struct KindInfo
    {
        public readonly float Hp, Bullet, Blast;
        public readonly int Tough;
        public KindInfo(float hp, float bullet, float blast, int tough) { Hp = hp; Bullet = bullet; Blast = blast; Tough = tough; }
    }

    // Points de vie, sensibilité aux balles et aux explosions, résistance à l'écrasement (0 = tout écrase).
    private static readonly KindInfo[] Info =
    {
        new(1, 0, 0, 99),         // None
        new(45, 1f, 1.2f, 0),     // Small : poubelle, borne, potelet, plot...
        new(130, 0.6f, 1f, 1),    // Furniture : banc, balançoire, canapé
        new(220, 0.25f, 1f, 1),   // Pole : lampadaire, feu, panneau, poteau
        new(300, 0.15f, 0.8f, 2), // Tree
        new(240, 0.5f, 1f, 2),    // Shelter : abribus, cabane
        new(650, 0.08f, 1f, 3),   // Barrier : séparateur béton, gabion
        new(180, 1f, 1f, 2),      // Car
        new(260, 1f, 1f, 3),      // Van
        new(380, 0.8f, 1f, 4),    // Truck
        new(500, 0.05f, 0.6f, 3), // Wreck : épave calcinée (aplatie)
        new(1e9f, 0f, 1f, 5),     // Container : poussé par les explosions
    };

    private World _world;
    private readonly List<PropEntry> _all = new();
    private readonly Dictionary<(int, int), StaticBody3D> _bodies = new();
    private readonly Dictionary<long, List<int>> _grid = new();
    private readonly Dictionary<ulong, int> _shapeOwner = new();
    private readonly Dictionary<(Mesh, int), ConcavePolygonShape3D> _scaledShapes = new();
    private readonly List<int> _query = new();

    public int Count => _all.Count;

    /// <summary>Crée le registre de la mission (avant la génération de la carte).</summary>
    public static Destructibles Begin(World world)
    {
        var d = new Destructibles { Name = "Destructibles", _world = world };
        world.AddChild(d);
        Building = d;
        Rempart.Building.ResetFires();
        return d;
    }

    /// <summary>Fin de la génération : plus aucun enregistrement.</summary>
    public void EndBuild()
    {
        if (Building == this) Building = null;
        // Épaves calcinées prêtes (pas d'à-coup à la première voiture qui explose).
        for (var v = 0; v < 4; v++) Props.WreckCar(v);
        Props.WreckVan(0);
        Props.WreckVan(1);
        Props.WreckTruck();
        Rempart.Building.WarmUp();
        DebrisPool.For(_world);
        _prewarmT = 0.2f;
        if (Screenshot.CmdArg("fps") != null || Screenshot.CmdArg("wreck") != null)
        {
            var counts = new int[Info.Length];
            foreach (var e in _all) counts[(int)e.Kind]++;
            var parts = new List<string>();
            for (var k = 1; k < counts.Length; k++) if (counts[k] > 0) parts.Add($"{(PropKind)k} {counts[k]}");
            GD.Print($"[decor] {_all.Count} accessoires destructibles ({string.Join(", ", parts)}), {_bodies.Count} corps de collision");
        }
    }

    public override void _ExitTree()
    {
        if (Building == this) Building = null;
    }

    // ------------------------------------------------------------------ Enregistrement (génération)

    /// <summary>
    /// Enregistre un accessoire posé (repère du pied, échelle comprise). Renvoie son identifiant, ou -1 hors mission,
    /// pour une famille indestructible, ou pour un poteau / un arbre déjà couché.
    /// </summary>
    public static int Register(Mesh mesh, PropKind kind, Transform3D xf, Color? tint)
    {
        var d = Building;
        if (d == null || kind == PropKind.None || mesh == null) return -1;
        if (kind is PropKind.Pole or PropKind.Tree && xf.Basis.Y.Normalized().Y < 0.8f) return -1;
        var info = Info[(int)kind];
        var e = new PropEntry
        {
            Id = d._all.Count, Kind = kind, Xf = xf, Box = mesh.GetAabb(), Mesh = mesh, Hp = info.Hp, MaxHp = info.Hp,
            Tint = tint ?? Colors.White,
        };
        d._all.Add(e);
        d.Insert(e);
        return e.Id;
    }

    public static void BindSlot(int id, InstSlot slot, int index)
    {
        if (Building?.Get(id) is not { } e) return;
        e.Slot = slot;
        e.Index = index;
    }

    public static void BindNode(int id, Node3D node)
    {
        if (Building?.Get(id) is { } e) e.Node = node;
    }

    /// <summary>Nœud lié (lumière d'un lampadaire) : masqué à la destruction.</summary>
    public static void AddExtra(int id, Node3D node)
    {
        if (Building?.Get(id) is not { } e) return;
        (e.Extras ??= new List<Node3D>()).Add(node);
    }

    /// <summary>Forme de collision de l'accessoire (transformée en coordonnées monde), rangée dans le corps de sa cellule.</summary>
    public static void AddShape(int id, CollisionShape3D cs)
    {
        var d = Building ?? World.Current?.Decor;
        if (d?.Get(id) is not { } e) return;
        d.AttachShape(e, cs);
    }

    /// <summary>Forme exacte (triangles solides du maillage) à l'échelle de l'accessoire.</summary>
    public static void AddMeshShape(int id)
    {
        var d = Building;
        if (d?.Get(id) is not { } e) return;
        var s = e.Scale;
        ConcavePolygonShape3D shape;
        if (Mathf.Abs(s - 1f) < 0.01f) shape = MeshKit.ShapeOf(e.Mesh);
        else
        {
            var key = (e.Mesh, Mathf.RoundToInt(s * 100));
            if (!d._scaledShapes.TryGetValue(key, out shape))
            {
                var faces = MeshKit.FacesOf(e.Mesh);
                var data = new Vector3[faces.Length];
                for (var i = 0; i < faces.Length; i++) data[i] = faces[i] * s;
                shape = faces.Length == 0 ? null : new ConcavePolygonShape3D { Data = data, BackfaceCollision = true };
                d._scaledShapes[key] = shape;
            }
        }
        if (shape == null) return;
        d.AttachShape(e, new CollisionShape3D { Shape = shape, Transform = new Transform3D(e.Xf.Basis.Orthonormalized(), e.Xf.Origin) });
    }

    private void AttachShape(PropEntry e, CollisionShape3D cs)
    {
        var p = cs.Position;
        var key = (Mathf.FloorToInt(p.X / BodyCell), Mathf.FloorToInt(p.Z / BodyCell));
        if (!_bodies.TryGetValue(key, out var body))
        {
            body = new StaticBody3D { Name = $"Props_{key.Item1}_{key.Item2}", CollisionLayer = 1, CollisionMask = 0 };
            AddChild(body);
            _bodies[key] = body;
        }
        body.AddChild(cs);
        (e.Shapes ??= new List<CollisionShape3D>()).Add(cs);
        _shapeOwner[cs.GetInstanceId()] = e.Id;
    }

    // ------------------------------------------------------------------ Recherche

    public PropEntry Get(int id) => id >= 0 && id < _all.Count ? _all[id] : null;

    private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    private void Insert(PropEntry e)
    {
        var c = e.Xf.Origin;
        var r = Mathf.Min(e.Radius, 8f);
        for (var x = Mathf.FloorToInt((c.X - r) / GridCell); x <= Mathf.FloorToInt((c.X + r) / GridCell); x++)
        for (var z = Mathf.FloorToInt((c.Z - r) / GridCell); z <= Mathf.FloorToInt((c.Z + r) / GridCell); z++)
        {
            var k = Key(x, z);
            if (!_grid.TryGetValue(k, out var list)) _grid[k] = list = new List<int>();
            list.Add(e.Id);
        }
    }

    /// <summary>Accessoires dont une cellule de la grille touche le disque (doublons retirés). La liste est réutilisée.</summary>
    public List<int> Near(Vector3 c, float r)
    {
        _query.Clear();
        for (var x = Mathf.FloorToInt((c.X - r) / GridCell); x <= Mathf.FloorToInt((c.X + r) / GridCell); x++)
        for (var z = Mathf.FloorToInt((c.Z - r) / GridCell); z <= Mathf.FloorToInt((c.Z + r) / GridCell); z++)
        {
            if (!_grid.TryGetValue(Key(x, z), out var list)) continue;
            foreach (var id in list)
                if (!_query.Contains(id)) _query.Add(id);
        }
        return _query;
    }

    /// <summary>Identifiant de l'accessoire touché par un rayon (collider + indice de forme), -1 sinon.</summary>
    public int FromHit(GodotObject collider, int shape)
    {
        if (collider is not StaticBody3D sb || sb.GetParent() != this || shape < 0) return -1;
        var owner = sb.ShapeFindOwner(shape);
        return sb.ShapeOwnerGetOwner(owner) is CollisionShape3D cs && _shapeOwner.TryGetValue(cs.GetInstanceId(), out var id) ? id : -1;
    }

    /// <summary>Distance d'un point au volume approché de l'accessoire (cylindre vertical).</summary>
    private static float DistanceTo(PropEntry e, Vector3 p)
    {
        var o = e.Xf.Origin;
        var flat = new Vector2(p.X - o.X, p.Z - o.Z).Length() - e.Radius;
        var dy = p.Y < o.Y ? o.Y - p.Y : p.Y > o.Y + e.Height ? p.Y - o.Y - e.Height : 0f;
        return new Vector2(Mathf.Max(flat, 0f), dy).Length();
    }

    private static byte DirByte(Vector3 d) => (byte)(Mathf.PosMod(Mathf.Atan2(d.X, d.Z) / Mathf.Tau, 1f) * 256f);
    private static Vector3 DirOf(int b) { var a = b / 256f * Mathf.Tau; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }

    /// <summary>Direction horizontale de la source vers l'accessoire (repli : hachage stable de l'identifiant).</summary>
    private static Vector3 Away(PropEntry e, Vector3 from)
    {
        var d = e.Xf.Origin - from;
        d.Y = 0;
        if (d.LengthSquared() < 0.01f) d = DirOf((int)(RuinKit.Hash(e.Id, 3, 17) * 256));
        return d.Normalized();
    }

    // ------------------------------------------------------------------ Côté joueur (dégâts envoyés à l'hôte)

    private readonly Dictionary<int, (float dmg, Vector3 from)> _pending = new();
    private readonly Dictionary<int, float> _crushSent = new();
    private float _flushT;

    /// <summary>
    /// Balle locale qui touche peut-être un accessoire : les dégâts sont cumulés et envoyés à l'hôte toutes les 0,1 s.
    /// Renvoie vrai si un accessoire a été touché.
    /// </summary>
    public bool LocalHit(GodotObject collider, int shape, float dmg, Vector3 from)
    {
        var id = FromHit(collider, shape);
        if (id < 0 || dmg <= 0) return false;
        _pending.TryGetValue(id, out var acc);
        _pending[id] = (acc.dmg + dmg, from);
        return true;
    }

    /// <summary>Raccourci : résultat brut d'un <c>IntersectRay</c>.</summary>
    public bool LocalHit(Godot.Collections.Dictionary hit, float dmg, Vector3 from) =>
        hit.Count > 0 && LocalHit(hit["collider"].AsGodotObject(), hit["shape"].AsInt32(), dmg, from);

    /// <summary>
    /// Écrasement par un véhicule piloté localement (char, hélicoptère) : chaque accessoire écrasable dans le disque
    /// est signalé une fois à l'hôte (force : 2 hélicoptère, 3 char).
    /// </summary>
    public void LocalCrush(Vector3 c, float r, int strength, Vector3 from)
    {
        var now = Time.GetTicksMsec() / 1000f;
        foreach (var id in Near(c, r + 4f))
        {
            var e = _all[id];
            if (!Crushable(e, strength) || DistanceTo(e, c) > r) continue;
            if (_crushSent.TryGetValue(id, out var t) && now - t < 1.5f) continue;
            _crushSent[id] = now;
            Net.I.SendPropCrush(id, strength, from);
        }
    }

    /// <summary>Contact d'un véhicule local avec une forme du décor (collision de glissement).</summary>
    public void LocalContact(GodotObject collider, int shape, int strength, Vector3 from)
    {
        var id = FromHit(collider, shape);
        if (id < 0 || !Crushable(_all[id], strength)) return;
        var now = Time.GetTicksMsec() / 1000f;
        if (_crushSent.TryGetValue(id, out var t) && now - t < 1.5f) return;
        _crushSent[id] = now;
        Net.I.SendPropCrush(id, strength, from);
    }

    private static bool Crushable(PropEntry e, int strength)
    {
        if (strength < Info[(int)e.Kind].Tough) return false;
        if (e.IsVehicle || e.Kind is PropKind.Wreck or PropKind.Container) return !e.Flattened;
        return e.State < 3;
    }

    // ------------------------------------------------------------------ Hôte

    private readonly List<(int id, float at)> _explodeAt = new();
    private readonly Dictionary<int, Vector3> _lastPos = new();
    private float _clock, _crushT;

    /// <summary>Dégâts (hôte) : balles, explosions, chocs. Fait évoluer l'état et diffuse les événements.</summary>
    public void HostDamage(int id, float dmg, PropHit type, Vector3 from, long by)
    {
        if (Get(id) is not { } e || dmg <= 0) return;
        var info = Info[(int)e.Kind];
        var mul = type switch { PropHit.Bullet => info.Bullet, PropHit.Blast => info.Blast, _ => 1f };
        if (e.Kind == PropKind.Container)
        {
            // Conteneur : poussé (et cabossé) par les explosions proches, jamais détruit.
            if (type == PropHit.Blast && dmg > 60f) Send(e, OpPush, DirByte(Away(e, from)), (int)Mathf.Clamp(dmg / 40f, 2f, 30f));
            return;
        }
        if (e.State >= 3 || mul <= 0) return;
        if (by != 0) e.LastBy = by;
        e.Hp -= dmg * mul;
        if (e.IsVehicle)
        {
            if (e.Hp <= 0) Schedule(e, 0.12f + RuinKit.Hash(e.Id, (int)_clock, 5) * 0.25f);
            else if (e.Hp <= e.MaxHp * 0.5f && e.State < 2)
            {
                Send(e, OpBurning, DirByte(Away(e, from)), 0);
                Schedule(e, 5f + RuinKit.Hash(e.Id, 9, 2) * 3f);
            }
            else if (e.Hp <= e.MaxHp * 0.82f && e.State < 1) Send(e, OpDamaged, DirByte(Away(e, from)), 0);
            return;
        }
        if (e.Hp > 0) return;
        if (e.Kind == PropKind.Wreck)
        {
            if (!e.Flattened) Send(e, OpFlatten, DirByte(Away(e, from)), 0);
            e.State = 3;
            return;
        }
        Send(e, OpDestroy, DirByte(Away(e, from)), 0);
    }

    /// <summary>Écrasement (hôte) : <paramref name="strength"/> 1 à 6 (gros insecte, char, forteresse, immeuble).</summary>
    public void HostCrush(int id, int strength, Vector3 from, long by)
    {
        if (Get(id) is not { } e || !Crushable(e, strength)) return;
        if (by != 0) e.LastBy = by;
        var dir = DirByte(Away(e, from));
        if (e.Kind == PropKind.Container && strength < 6) { Send(e, OpPush, dir, 20); return; }
        if (e.IsVehicle || e.Kind is PropKind.Wreck or PropKind.Container) { Send(e, OpFlatten, dir, 0); return; }
        Send(e, OpDestroy, dir, 0);
    }

    /// <summary>Explosion (hôte) : dégâts dégressifs aux accessoires dans le rayon.</summary>
    public void HostBlast(Vector3 pos, float radius, float dmg, long by)
    {
        var ids = new List<int>(Near(pos, radius + 6f));
        foreach (var id in ids)
        {
            var e = _all[id];
            var d = DistanceTo(e, pos);
            if (d > radius) continue;
            var f = Mathf.Clamp(1f - d / radius * 0.6f, 0.4f, 1f);
            HostDamage(id, dmg * f, PropHit.Blast, pos, by);
        }
    }

    /// <summary>Tout ce qui se trouve dans l'emprise (immeuble qui s'effondre) est écrasé.</summary>
    public void HostCrushArea(Vector3 c, float hx, float hz, int strength)
    {
        var ids = new List<int>(Near(c, Mathf.Max(hx, hz) * 1.42f));
        foreach (var id in ids)
        {
            var o = _all[id].Xf.Origin;
            if (Mathf.Abs(o.X - c.X) <= hx && Mathf.Abs(o.Z - c.Z) <= hz) HostCrush(id, strength, c, 0);
        }
    }

    private void Schedule(PropEntry e, float delay)
    {
        for (var i = 0; i < _explodeAt.Count; i++)
            if (_explodeAt[i].id == e.Id)
            {
                if (_clock + delay < _explodeAt[i].at) _explodeAt[i] = (e.Id, _clock + delay);
                return;
            }
        _explodeAt.Add((e.Id, _clock + delay));
    }

    private void Send(PropEntry e, int op, int dir, int amount)
    {
        // État logique immédiat chez l'hôte (pas de double envoi pendant le trajet réseau).
        switch (op)
        {
            case OpDamaged: e.State = 1; break;
            case OpBurning: e.State = 2; break;
            case OpDestroy: e.State = 3; break;
            case OpFlatten: e.Flattened = true; break;
        }
        Net.I.BroadcastPropEvent(e.Id, op | (dir & 0xFF) << 8 | (amount & 0xFF) << 16);
    }

    /// <summary>Tic de l'hôte : explosions de véhicules programmées, écrasement par les gros insectes (10 Hz).</summary>
    public void HostTick(float dt)
    {
        _clock += dt;
        for (var i = _explodeAt.Count - 1; i >= 0; i--)
        {
            var (id, at) = _explodeAt[i];
            if (_clock < at) continue;
            _explodeAt.RemoveAt(i);
            HostExplodeVehicle(_all[id]);
        }
        _crushT -= dt;
        if (_crushT > 0) return;
        _crushT = 0.1f;
        if (_lastPos.Count > _world.EnemiesById.Count * 2 + 64) _lastPos.Clear();
        foreach (var en in _world.EnemiesById.Values)
        {
            if (en.State == EnemyState.Dead || en.Def.Flying || en.IsStatic) continue;
            var s = en.Def.Size;
            var strength = s >= 10f ? 6 : s >= 3.5f ? 4 : s >= 2.4f ? 3 : s >= 1.8f ? 2 : s >= 1.4f ? 1 : -1;
            if (strength < 0) continue;
            // Un insecte immobile (ou accroché à une façade, loin du pied des accessoires) n'écrase rien. Les ennemis sont
            // déplacés par position (pas de vitesse physique) : on compare à la position du tic précédent.
            var p = en.GlobalPosition;
            var moved = !_lastPos.TryGetValue(en.Id, out var last) || last.DistanceSquaredTo(p) > 0.0025f;
            _lastPos[en.Id] = p;
            if (!moved && !en.IsBoss) continue;
            var r = s * 0.85f;
            var ids = new List<int>(Near(p, r + 3f));
            foreach (var id in ids)
            {
                var e = _all[id];
                if (!Crushable(e, strength)) continue;
                if (p.Y > e.Xf.Origin.Y + e.Height + s * 0.6f || p.Y < e.Xf.Origin.Y - s) continue;
                var o = e.Xf.Origin;
                if (new Vector2(p.X - o.X, p.Z - o.Z).Length() - e.FootR > r) continue;
                HostCrush(id, strength, p, 0);
            }
        }
    }

    /// <summary>Explosion d'un véhicule (hôte) : épave, boule de feu et souffle (insectes, décor, joueurs proches).</summary>
    private void HostExplodeVehicle(PropEntry e)
    {
        if (e.State >= 3 || !e.IsVehicle) return;
        Send(e, OpDestroy, 0, 0);
        var big = e.Kind == PropKind.Truck ? 1.25f : e.Kind == PropKind.Van ? 1.1f : 1f;
        var pos = e.Center + Vector3.Up * 0.3f;
        var radius = 7.5f * big;
        // Souffle : mêmes règles qu'une explosion de joueur pour les insectes et le décor (crédité au dernier tireur).
        _world.HostExplosion(pos, radius, 110f * big * _world.HealthMul, 0, e.LastBy);
        // Joueurs : aucun tir ami (voiture détruite par un joueur) ; sinon (insecte, effondrement), comme une explosion ennemie.
        if (e.LastBy != 0) return;
        void Hurt(long peer, Vector3 at)
        {
            var d = (at + Vector3.Up).DistanceTo(pos);
            if (d <= radius) Net.I.DamagePlayer(peer, 35f * big * _world.DamageMul * Mathf.Clamp(1.2f - d / radius, 0.3f, 1f), pos);
        }
        if (_world.LocalPlayer is { Downed: false } lp) Hurt(Net.I.MyId, lp.GlobalPosition);
        foreach (var (peer, r) in _world.Remotes)
            if (!r.Downed) Hurt(peer, r.GlobalPosition);
    }

    // ------------------------------------------------------------------ Tous les pairs

    private static readonly bool LogEvents = Screenshot.CmdArg("fps") != null;
    private int _events;
    private float _logT = 5f;
    private float _prewarmT = -1f;
    private Node3D _prewarm;

    /// <summary>
    /// Préchauffage des shaders (plaques de dégâts, morceaux) : de minuscules quads devant la caméra pendant l'écran de
    /// déploiement, pour que le premier impact ne fige pas l'image le temps de la compilation.
    /// </summary>
    private void Prewarm(float dt)
    {
        _prewarmT -= dt;
        if (_prewarmT > 0) return;
        if (_prewarm != null)
        {
            _prewarm.QueueFree();
            _prewarm = null;
            _prewarmT = -1f;
            return;
        }
        var cam = _world.LocalPlayer?.Camera;
        if (cam == null) { _prewarmT = 0.2f; return; }
        _prewarm = new Node3D { Name = "Prewarm", Position = new Vector3(0, 0, -0.6f) };
        cam.AddChild(_prewarm);
        var x = -0.02f;
        foreach (var m in Rempart.Building.ScarMaterials)
        {
            _prewarm.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 0.004f }, MaterialOverride = m, Position = new Vector3(x, 0, 0) });
            x += 0.01f;
        }
        if (DebrisPool.PieceMesh is { } piece) _prewarm.AddChild(new MeshInstance3D { Mesh = piece, Scale = Vector3.One * 0.004f, Position = new Vector3(x, 0, 0) });
        _prewarmT = 0.5f;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_prewarmT >= 0) Prewarm(dt);
        if (LogEvents && (_logT -= dt) <= 0)
        {
            _logT = 5f;
            var gone = 0;
            foreach (var e in _all) if (e.Shown >= 3 || e.FlatShown) gone++;
            GD.Print($"[decor] {_events} événements reçus, {gone} accessoires détruits ou aplatis, débris {DebrisPool.ActiveCount}");
        }
        _flushT -= dt;
        if (_flushT <= 0 && _pending.Count > 0)
        {
            _flushT = 0.1f;
            foreach (var (id, v) in _pending) Net.I.SendPropDamage(id, v.dmg, (int)PropHit.Bullet, v.from);
            _pending.Clear();
        }
        TickFx(dt);
    }

    /// <summary>Événement diffusé par l'hôte (appliqué chez tous les pairs, hôte compris).</summary>
    public void OnEvent(int id, int packed)
    {
        if (Get(id) is not { } e) return;
        _events++;
        var op = packed & 0xFF;
        var dir = DirOf((packed >> 8) & 0xFF);
        var amount = ((packed >> 16) & 0xFF) * 0.05f;
        switch (op)
        {
            case OpDamaged:
                if (e.Shown >= 1) return;
                e.Shown = 1;
                e.State = (byte)Mathf.Max(e.State, 1);
                FxDamaged(e, dir);
                break;
            case OpBurning:
                if (e.Shown >= 2) return;
                if (e.Shown < 1) FxDamaged(e, dir);
                e.Shown = 2;
                e.State = (byte)Mathf.Max(e.State, 2);
                FxBurning(e);
                break;
            case OpDestroy:
                if (e.Shown >= 3) return;
                e.Shown = 3;
                e.State = 3;
                if (e.IsVehicle) FxExplode(e, dir);
                else if (e.Kind is PropKind.Pole or PropKind.Tree) StartFall(e, dir);
                else FxBreak(e, dir);
                break;
            case OpFlatten:
                if (e.FlatShown) return;
                e.FlatShown = e.Flattened = true;
                FxFlatten(e, dir);
                break;
            case OpPush:
                FxPush(e, dir, amount);
                break;
        }
    }
}
