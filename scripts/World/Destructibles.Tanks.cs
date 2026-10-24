using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Citernes et cuves explosives (<see cref="PropKind.Tank"/>, zone industrielle) : cuves de stockage, sphères de gaz,
/// réservoirs horizontaux, cuves de procédé, wagons-citernes.
///
/// Hôte : les balles et les explosions percent la cuve (fuite de vapeur à 80 % des PV), l'enflamment (jet de feu à
/// 50 %, explosion programmée 7 à 12 s plus tard) ; à 0 PV elle explose en moins d'une seconde. L'explosion passe par
/// <see cref="World.HostExplosion"/> (insectes, immeubles, mobilier, autres cuves : réactions en chaîne) avec un rayon
/// et des dégâts propres au modèle ; les joueurs proches sont blessés (à 40 % si un joueur l'a provoquée).
/// Tous les pairs : mêmes événements que les voitures (abîmé, en feu, détruit), joués ici : fuite, jet de feu,
/// boule de feu qui monte en champignon, tôles projetées, épave déchirée (collision exacte) qui brûle longtemps.
/// Aucune RPC nouvelle : <c>Net.BroadcastPropEvent</c> et <c>Net.BroadcastExplosionFx</c>.
/// </summary>
public partial class Destructibles
{
    /// <summary>Caractéristiques d'une cuve explosive (fixées à la génération, identiques chez tous les pairs).</summary>
    public sealed class TankSpec
    {
        /// <summary>Épave (collision exacte), posée au même repère que la cuve.</summary>
        public Mesh Wreck;
        /// <summary>Rayon du souffle (m) et dégâts de base (multipliés par la santé de la difficulté).</summary>
        public float Blast, Damage;
        /// <summary>Hauteur du cœur de la boule de feu au-dessus du pied (m).</summary>
        public float Core;
        /// <summary>Taille de l'incendie de l'épave et hauteur du foyer au-dessus du pied (fond de cuve, virole d'un wagon).</summary>
        public float FireScale, FireY = 1f;
        /// <summary>Couleur dominante de la peinture (tôles projetées).</summary>
        public Color Paint = new(0.55f, 0.55f, 0.52f);
    }

    private const int MaxTankFires = 9;
    private readonly Dictionary<int, TankSpec> _tanks = new();
    private int _tankFires, _tankLights;

    /// <summary>
    /// Pose une cuve explosive (nœud visuel + forme exacte) et l'enregistre. Hors mission, simple décor solide.
    /// Renvoie l'identifiant destructible (-1 hors mission).
    /// </summary>
    public static int AddTank(Node3D root, StaticBody3D fallback, Mesh mesh, Transform3D xf, float hp, TankSpec spec)
    {
        var node = new MeshInstance3D { Name = "Tank", Mesh = mesh, Transform = xf };
        root.AddChild(node);
        var d = Building;
        var id = Register(mesh, PropKind.Tank, xf, null);
        if (id < 0)
        {
            if (MeshKit.ShapeOf(mesh) is { } s) fallback.AddChild(new CollisionShape3D { Shape = s, Transform = xf });
            return -1;
        }
        BindNode(id, node);
        AddMeshShape(id);
        var e = d.Get(id);
        e.Hp = e.MaxHp = hp;
        d._tanks[id] = spec;
        // Préchauffage (une fois par mission) : épaves, flammes et fumées affichées un instant devant la caméra pendant
        // le déploiement, pour que la première cuve qui saute ne fige pas l'image (compilation des pipelines).
        if (d._tankPrewarm == null)
        {
            d._tankPrewarm = new TankPrewarm { Name = "TankPrewarm" };
            d.AddChild(d._tankPrewarm);
        }
        if (!d._tankPrewarm.Meshes.Contains(spec.Wreck)) d._tankPrewarm.Meshes.Add(spec.Wreck);
        return id;
    }

    private TankPrewarm _tankPrewarm;

    // ------------------------------------------------------------------ Hôte

    private void HostDamageTank(PropEntry e, float dmg, Vector3 from)
    {
        e.Hp -= dmg;
        var dir = DirByte(Away(e, from));
        if (e.Hp <= 0)
        {
            if (e.State < 2) Send(e, OpBurning, dir, 0);
            // Les cuves voisines d'une explosion partent en cascade, à quelques dixièmes de seconde d'écart.
            Schedule(e, 0.35f + RuinKit.Hash(e.Id, (int)(_clock * 10f), 6) * 0.7f);
        }
        else if (e.Hp <= e.MaxHp * 0.5f && e.State < 2)
        {
            Send(e, OpBurning, dir, 0);
            Schedule(e, 7f + RuinKit.Hash(e.Id, 11, 6) * 5f);
        }
        else if (e.Hp <= e.MaxHp * 0.8f && e.State < 1) Send(e, OpDamaged, dir, 0);
    }

    /// <summary>Explosion d'une cuve (hôte) : épave, souffle (insectes, décor, autres cuves), joueurs proches.</summary>
    private void HostExplodeTank(PropEntry e)
    {
        if (e.State >= 3 || !_tanks.TryGetValue(e.Id, out var s)) return;
        Send(e, OpDestroy, 0, 0);
        // Point bas : onde de choc, poussière et brûlure au sol (la boule de feu qui monte est jouée par chaque pair).
        var pos = e.Xf.Origin + Vector3.Up * Mathf.Min(s.Core, 2f);
        _world.HostExplosion(pos, s.Blast, s.Damage * _world.HealthMul, 0, e.LastBy);
        // Joueurs : la cuve est un danger pour tous (moins fort si un joueur l'a fait sauter, pour limiter le tir ami).
        var core = e.Xf.Origin + Vector3.Up * s.Core;
        var mul = e.LastBy != 0 ? 0.4f : 1f;
        void Hurt(long peer, Vector3 at)
        {
            var d = Mathf.Min((at + Vector3.Up).DistanceTo(core), (at + Vector3.Up).DistanceTo(pos));
            if (d <= s.Blast) Net.I.DamagePlayer(peer, s.Damage * 0.3f * mul * _world.DamageMul * Mathf.Clamp(1.2f - d / s.Blast, 0.25f, 1f), core);
        }
        if (_world.LocalPlayer is { Downed: false } lp) Hurt(Net.I.MyId, lp.GlobalPosition);
        foreach (var (peer, r) in _world.Remotes)
            if (!r.Downed) Hurt(peer, r.GlobalPosition);
    }

    // ------------------------------------------------------------------ Tous les pairs

    private void OnTankEvent(PropEntry e, int op, Vector3 dir)
    {
        switch (op)
        {
            case OpDamaged:
                if (e.Shown >= 1) return;
                e.Shown = 1;
                e.State = (byte)Mathf.Max(e.State, 1);
                FxTankLeak(e, dir, false);
                break;
            case OpBurning:
                if (e.Shown >= 2) return;
                if (e.Shown < 1) FxTankLeak(e, dir, false);
                e.Shown = 2;
                e.State = (byte)Mathf.Max(e.State, 2);
                FxTankLeak(e, dir, true);
                break;
            case OpDestroy:
                if (e.Shown >= 3) return;
                e.Shown = 3;
                e.State = 3;
                FxTankExplode(e);
                break;
        }
    }

    /// <summary>Point de la paroi tourné vers la source (fuite), à mi-hauteur.</summary>
    private static Vector3 LeakPoint(PropEntry e, Vector3 dir)
    {
        var c = e.Center;
        var r = Mathf.Min(e.Box.Size.X, e.Box.Size.Z) * 0.5f * e.Scale * 0.92f;
        // Sur la robe, aux deux tiers de la hauteur (au-dessus du châssis d'un wagon, sous le toit d'une cuve).
        var y = e.Xf.Origin.Y + Mathf.Clamp(e.Height * 0.62f, 1.5f, 9f);
        var p = c - dir * r;
        return p with { Y = y };
    }

    private void FxTankLeak(PropEntry e, Vector3 dir, bool fire)
    {
        var p = LeakPoint(e, dir);
        var dist = Fx.CameraDistance(p);
        if (!fire)
        {
            // Perforation : jet de vapeur grise, étincelles, sifflement.
            if (dist < 120f)
                for (var i = 0; i < 14; i++)
                    Fx.Spark(p, -dir * R(2f, 6f) + RandV(2f) + Vector3.Up * R(0.5f, 3f), R(0.02f, 0.04f), R(0.4f, 0.9f), new Color(1f, 0.75f, 0.4f, 0.9f));
            if (dist <= FxRange)
            {
                var vapour = RuinFire.SmokeColumn(p - dir * 0.5f, 0.6f, false);
                AddChild(vapour);
                Track(e, vapour);
            }
            Sfx.At("spit", p, 0f, 0.7f, 80);
            return;
        }
        // Jet de feu à la brèche (limité par carte, lumière la nuit pour quelques-uns), fumée noire épaisse.
        if (_tankFires < MaxTankFires)
        {
            var light = Night && _tankLights < 3;
            if (light) _tankLights++;
            var f = RuinFire.Create(p - dir * 0.6f, 1.5f, light, Night, e.Id);
            f.SetMeta("light", light);
            AddChild(f);
            Track(e, f);
            _tankFires++;
        }
        var smoke = RuinFire.SmokeColumn(p + Vector3.Up * 1.5f, 1.6f, true);
        AddChild(smoke);
        Track(e, smoke);
        Fx.Flash(p, new Color(1f, 0.55f, 0.25f), 3f, 14f, 0.4f);
        Sfx.At("spit", p, 4f, 0.4f, 120);
        Sfx.At("explosion_small", p, -4f, 0.7f, 120);
    }

    private void FxTankExplode(PropEntry e)
    {
        if (!_tanks.TryGetValue(e.Id, out var s)) { FxBreak(e, Vector3.Up); return; }
        e.Burnt = true;
        // Épave : maillage déchiré, collision exacte à la place de la cuve.
        if (e.Node is MeshInstance3D mi && IsInstanceValid(mi)) mi.Mesh = s.Wreck;
        else Hide(e);
        HideExtras(e);
        DisableShapes(e);
        if (MeshKit.ShapeOf(s.Wreck) is { } wreckShape)
            AttachShape(e, new CollisionShape3D { Shape = wreckShape, Transform = new Transform3D(e.Xf.Basis.Orthonormalized(), e.Xf.Origin) });
        var core = e.Xf.Origin + Vector3.Up * s.Core;
        var dist = Fx.CameraDistance(core);
        var big = s.Blast / 20f;
        _world.ShakeCamera(core, 0.6f * big + 0.3f, 60f + s.Blast * 4f);
        Sfx.At("explosion", core, 9f, 0.62f, 0);
        Sfx.At("collapse", core, 4f, 1.3f, 0);
        if (dist <= FxRange * 1.6f)
        {
            var fx = Fx;
            var q = Mathf.Clamp(1f - (dist - 80f) / 300f, 0.35f, 1f);
            int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * q));
            fx.Flash(core, new Color(1f, 0.58f, 0.28f), 9f * big + 4f, s.Blast * 2.6f, 1.6f);
            var fb = s.Blast * 0.3f;
            // Boule de feu qui gonfle autour de la cuve, puis colonne qui monte en champignon.
            for (var i = 0; i < N(26); i++)
            {
                var d = new Vector3(R(-1, 1), R(-0.2f, 1f), R(-1, 1)).Normalized();
                var g = R(0.04f, 0.08f);
                fx.FirePuff(core + d * fb * R(0.2f, 0.7f), d * fb * R(1.5f, 3.2f) + Vector3.Up * R(2f, 6f), fb * R(0.7f, 1.1f), R(2.4f, 3.6f), new Color(g, g * 0.95f, g * 0.9f, 0.95f));
            }
            for (var i = 0; i < N(14); i++)
            {
                var u = i / 14f;
                var g = R(0.04f, 0.07f);
                fx.FirePuff(core + new Vector3(R(-1, 1), R(0, 1), R(-1, 1)) * fb * 0.4f, new Vector3(R(-1, 1), R(9f, 16f) * (0.6f + u), R(-1, 1)) * big, fb * R(0.8f, 1.2f) * (1f + u * 0.5f), R(3f, 4.2f), new Color(g, g * 0.95f, g * 0.9f, 0.95f));
            }
            // Chapeau de fumée noire, très lent.
            for (var i = 0; i < N(18); i++)
            {
                var a = R(0, Mathf.Tau);
                var rr = R(0.2f, 1f) * fb;
                var g = R(0.04f, 0.075f);
                fx.SmokePuff(core + new Vector3(Mathf.Cos(a) * rr, fb * R(1f, 2.5f), Mathf.Sin(a) * rr), new Vector3(Mathf.Cos(a) * R(1f, 4f), R(5f, 11f), Mathf.Sin(a) * R(1f, 4f)) * big,
                    fb * R(1.3f, 2f), R(14f, 22f), new Color(g, g * 0.97f, g * 0.94f, R(0.7f, 0.9f)));
            }
            // Tôles projetées (corps physiques près de la caméra), éclats et braises.
            var nPlates = dist < RigidRange ? 9 : 5;
            for (var i = 0; i < nPlates; i++)
            {
                var d = new Vector3(R(-1, 1), R(0.3f, 1f), R(-1, 1)).Normalized();
                var size = new Vector3(R(1.2f, 3.2f), R(0.04f, 0.08f), R(0.8f, 2.2f)) * Mathf.Clamp(big, 0.6f, 1.2f);
                var col = (i % 2 == 0 ? s.Paint : new Color(0.12f, 0.1f, 0.09f)) * new Color(0.6f, 0.55f, 0.5f);
                Piece(core + d * fb * 0.5f, new Basis(RandV(1f).Normalized() + Vector3.Up * 0.01f, R(0, 3f)), size, d * R(10f, 22f) * big + Vector3.Up * R(6f, 14f), col with { A = 0.3f }, dist, 14f);
            }
            for (var i = 0; i < N(40); i++)
            {
                var d = new Vector3(R(-1, 1), R(0.2f, 1.2f), R(-1, 1));
                fx.Spark(core + RandV(fb * 0.4f), d * R(10f, 30f) * big, R(0.04f, 0.09f), R(1.2f, 2.6f), new Color(1f, 0.6f, 0.25f, 1f));
            }
        }
        // Les feux de la fuite laissent place à l'incendie de l'épave, qui finit par couver.
        if (e.Fx != null)
        {
            foreach (var n in e.Fx)
            {
                if (n is RuinFire f)
                {
                    var light = f.HasMeta("light") && f.GetMeta("light").AsBool();
                    Expire(n, 0.6f, () => { _tankFires--; if (light) _tankLights--; });
                }
                else Expire(n, 4f, null);
            }
            e.Fx.Clear();
        }
        var firePos = e.Xf.Origin + Vector3.Up * s.FireY;
        var wl = Night && _tankLights < 3;
        if (wl) _tankLights++;
        var fire = RuinFire.Create(firePos, s.FireScale, wl, Night, e.Id + 7);
        AddChild(fire);
        _tankFires++;
        var plume = RuinFire.SmokeColumn(firePos + Vector3.Up * (s.FireScale * 2f), s.FireScale * 1.3f, true);
        AddChild(plume);
        Expire(fire, R(70f, 110f), () =>
        {
            _tankFires--;
            if (wl) _tankLights--;
            var smoke = RuinFire.SmokeColumn(firePos + Vector3.Up, s.FireScale, true);
            AddChild(smoke);
            Expire(smoke, 90f, null);
        });
        Expire(plume, 120f, null);
    }
}

/// <summary>
/// Préchauffage des effets de cuve : épaves, flammes et fumée affichées en tout petit devant la caméra pendant le
/// déploiement (compilation des pipelines avant la première explosion).
/// </summary>
public partial class TankPrewarm : Node
{
    public readonly List<Mesh> Meshes = new();
    private Node3D _holder;
    private float _t = 0.3f;

    public override void _Process(double delta)
    {
        _t -= (float)delta;
        if (_t > 0) return;
        if (_holder != null)
        {
            _holder.QueueFree();
            QueueFree();
            return;
        }
        var cam = World.Current?.LocalPlayer?.Camera;
        if (cam == null) { _t = 0.2f; return; }
        _holder = new Node3D { Name = "TankPrewarmHolder", Position = new Vector3(0, 0, -0.7f) };
        cam.AddChild(_holder);
        var x = -0.03f;
        foreach (var m in Meshes)
        {
            var s = 0.006f / Mathf.Max(0.1f, m.GetAabb().GetLongestAxisSize());
            _holder.AddChild(new MeshInstance3D { Mesh = m, Scale = Vector3.One * s, Position = new Vector3(x, 0, 0) });
            x += 0.01f;
        }
        _holder.AddChild(RuinFire.Create(new Vector3(x, 0, 0), 0.01f, false, false, 1));
        _holder.AddChild(RuinFire.SmokeColumn(new Vector3(x + 0.01f, 0, 0), 0.01f, false));
        _t = 0.6f;
    }
}
