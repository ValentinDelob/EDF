using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Balles des armes d'EDF 5 : projectiles rectilignes à vitesse réelle (« Shot Speed »), durée de vie = portée / vitesse,
/// dégâts atténués avec la distance parcourue comme dans EDF 5 (min + (max − min) × (vie restante)^x),
/// perforation des ennemis ([PT]), balles explosives (« Effective Area »), gravité (acide), ricochets (Bound Gun).
/// Les balles du joueur local infligent les dégâts ; celles des autres joueurs sont visuelles. Rendu : un seul MultiMesh.
/// </summary>
public partial class Bullets : Node3D
{
    public struct Shot
    {
        public Vector3 Pos, Vel;
        public float Life, MaxLife, Damage, Far, FalloffExp, Blast, Gravity, Length, Width;
        /// <summary>Point de départ : la traînée ne dépasse jamais en arrière de la bouche du canon.</summary>
        public Vector3 Origin;
        public bool HasOrigin;
        public bool Local, Pierce;
        public int Bounces;
        public Color Color;
        /// <summary>Ennemis déjà traversés (balles perforantes : chacun n'est touché qu'une fois).</summary>
        public List<int> Hit;
    }

    private readonly List<Shot> _shots = new();
    private World _world;
    private MultiMeshInstance3D _mmi;
    private MultiMesh _mm;
    private const int Capacity = 3000;

    public static Bullets For(World w)
    {
        if (w.GetNodeOrNull<Bullets>("Bullets") is { } b) return b;
        b = new Bullets { Name = "Bullets", _world = w };
        w.AddChild(b);
        return b;
    }

    public override void _Ready()
    {
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
            // Traînée unitaire centrée le long de −Z (tête vers −Z), mise à l'échelle de la longueur et de l'épaisseur de chaque balle.
            Mesh = MuzzleFx.BulletMesh(),
        };
        _mmi = new MultiMeshInstance3D { Multimesh = _mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_mmi);
    }

    public void Add(Shot s)
    {
        if (_shots.Count >= Capacity) return;
        if (s.Pierce) s.Hit = new List<int>();
        if (!s.HasOrigin) { s.Origin = s.Pos; s.HasOrigin = true; }
        _shots.Add(s);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var space = GetWorld3D().DirectSpaceState;
        Dictionary<Enemy, (float dmg, Vector3 at, bool weak)> perEnemy = null;
        for (var i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            var from = s.Pos;
            s.Vel += Vector3.Down * s.Gravity * dt;
            var to = from + s.Vel * dt;
            var lifeStart = s.Life;
            var speed = Mathf.Max(s.Vel.Length(), 0.01f);
            s.Life -= dt;
            var dead = s.Life <= 0;
            // Plusieurs rayons pour une balle perforante (elle continue après chaque ennemi traversé).
            for (var guard = 0; guard < 8; guard++)
            {
                var q = PhysicsRayQueryParameters3D.Create(from, to, 1u | (1u << 2));
                if (s.Hit is { Count: > 0 })
                {
                    var ex = new Godot.Collections.Array<Rid>();
                    foreach (var id in s.Hit)
                        if (_world.EnemiesById.TryGetValue(id, out var he) && IsInstanceValid(he)) ex.Add(he.GetRid());
                    q.Exclude = ex;
                }
                if (_world.LocalPlayer is { } lp && IsInstanceValid(lp))
                {
                    var ex = q.Exclude;
                    ex.Add(lp.GetRid());
                    q.Exclude = ex;
                }
                var hit = space.IntersectRay(q);
                if (hit.Count == 0) break;
                var at = (Vector3)hit["position"];
                var normal = (Vector3)hit["normal"];
                var col = hit["collider"].AsGodotObject();
                // Atténuation au point d'impact (temps de vol réel jusqu'à lui, pas la fin de l'image : une balle rapide
                // ne perd pas une image entière de portée).
                var lifeAt = lifeStart - s.Pos.DistanceTo(at) / speed;
                var frac = s.MaxLife > 0 ? Mathf.Clamp(lifeAt / s.MaxLife, 0, 1) : 1;
                var dmg = s.Damage * (s.Far + (1f - s.Far) * Mathf.Pow(frac, s.FalloffExp));
                if (col is Enemy e && e.State != EnemyState.Dead)
                {
                    if (s.Blast > 0) Blast(at, s.Blast, dmg, s.Local, ref perEnemy, e);
                    else if (s.Local)
                    {
                        var weak = e.IsWeakShape(hit["shape"].AsInt32());
                        perEnemy ??= new Dictionary<Enemy, (float, Vector3, bool)>();
                        perEnemy.TryGetValue(e, out var acc);
                        perEnemy[e] = (acc.dmg + dmg * (weak ? 1.5f : 1f) * (_world.LocalPlayer?.DamageMul ?? 1f), at, acc.weak || weak);
                    }
                    if (s.Pierce && s.Blast <= 0)
                    {
                        s.Hit.Add(e.Id);
                        from = at;
                        continue;
                    }
                    dead = true;
                    to = at;
                    break;
                }
                // Décor, sol, immeuble : ricochet (Bound Gun) ou arrêt.
                if (s.Blast > 0) Blast(at, s.Blast, dmg, s.Local, ref perEnemy);
                else if (s.Local)
                {
                    if (col is AlienShield sh) sh.LocalHit(at);
                    else if (col is Building b && !b.Destroyed) Net.I.SendBuildingDamage(b.Id, dmg * 0.06f, at);
                    else _world.Decor?.LocalHit(col, hit["shape"].AsInt32(), dmg, from);
                }
                if (s.Blast <= 0)
                {
                    Effects.Sparks(_world, at, normal);
                    Effects.BulletHole(_world, at, normal, col);
                }
                if (s.Bounces > 0 && s.Blast <= 0)
                {
                    s.Bounces--;
                    s.Vel = s.Vel.Bounce(normal) * 0.92f;
                    to = at + normal * 0.05f;
                    break;
                }
                dead = true;
                to = at;
                break;
            }
            s.Pos = to;
            if (dead) _shots.RemoveAt(i);
            else _shots[i] = s;
        }
        if (perEnemy != null)
            foreach (var (e, v) in perEnemy)
            {
                Net.I.SendDamageEnemy(e.Id, v.dmg, v.at, v.weak);
                _world.LocalHitFx(e, v.at, true, v.dmg, v.weak);
                if (_world.LocalPlayer is { } p) { p.HitMarker = 1f; p.EarnCredits(v.dmg); }
            }
    }

    /// <summary>
    /// Balle explosive (impulsions de la Wing Diver, Minion Buster…) : effet sur chaque pair (tous simulent la balle) ; dégâts
    /// de zone calculés par le tireur et regroupés par ennemi pour l'image (pas un message réseau par explosion).
    /// </summary>
    /// <remarks><paramref name="direct"/> : ennemi touché de plein fouet, toujours atteint (un grand insecte a son centre loin du point d'impact).</remarks>
    private void Blast(Vector3 at, float radius, float dmg, bool local, ref Dictionary<Enemy, (float dmg, Vector3 at, bool weak)> perEnemy, Enemy direct = null)
    {
        Effects.Explosion(_world, at, Mathf.Max(radius, 1f), radius < 3f ? ExplosionFx.Plasma : ExplosionFx.Explosive);
        if (!local) return;
        var mul = _world.LocalPlayer?.DamageMul ?? 1f;
        foreach (var e in _world.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var d = e == direct ? 0f : e.BodyDistance(at) - e.Def.Size * 1.2f;
            if (d > radius) continue;
            // Dégâts pleins dans tout le rayon (EDF 5).
            perEnemy ??= new Dictionary<Enemy, (float, Vector3, bool)>();
            perEnemy.TryGetValue(e, out var acc);
            perEnemy[e] = (acc.dmg + dmg * mul, e.Center, acc.weak);
        }
    }

    public override void _Process(double delta)
    {
        var n = _shots.Count;
        _mm.VisibleInstanceCount = n;
        for (var i = 0; i < n; i++)
        {
            var s = _shots[i];
            var dir = s.Vel.LengthSquared() > 0.01f ? s.Vel.Normalized() : Vector3.Forward;
            var up = Mathf.Abs(dir.Y) > 0.98f ? Vector3.Right : Vector3.Up;
            // Le trait part de la tête de la balle vers l'arrière, sans dépasser le point de départ.
            var len = Mathf.Min(s.Length, Mathf.Max(0.05f, s.Pos.DistanceTo(s.Origin)));
            var basis = Basis.LookingAt(dir, up) * Basis.FromScale(new Vector3(s.Width, s.Width, len));
            _mm.SetInstanceTransform(i, new Transform3D(basis, s.Pos - dir * len * 0.5f));
            _mm.SetInstanceColor(i, s.Color);
        }
    }
}
