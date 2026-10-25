using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Coups du Fencer (Grenadier blindé) qui ne passent pas par les balles ordinaires : frappes chargées, coups de lance,
/// mortiers. Même principe que <see cref="Edf5Fire"/> : le tireur applique les dégâts, les autres pairs rejouent l'effet à
/// partir de la graine ; les deux bits bas de la graine portent le niveau de charge ou l'attaque enchaînée.
/// </summary>
public static class FencerFire
{
    /// <summary>Gravité des obus de mortier (m/s²) : trajectoire en cloche lisible.</summary>
    public const float MortarGravity = 30f;

    /// <summary>
    /// Allonge réelle d'une onde de choc ou d'un coup de lance : les portées d'EDF 5 de ces armes (48 à 145 m pour un marteau)
    /// sont celles de projectiles invisibles très brefs ; à l'écran, la frappe porte à quelques mètres et l'onde au plus à
    /// une trentaine (la valeur d'EDF 5 reste affichée dans l'arsenal).
    /// </summary>
    public static float Reach(float range) => Mathf.Min(range, 4f + range * 0.16f);
    public static float PierceReach(float range) => Mathf.Min(range, 3f + range * 0.22f);

    /// <summary>Niveau de charge / attaque porté par la graine d'un coup de mêlée.</summary>
    public static int Level(int seed) => seed & 3;
    public static int WithLevel(int seed, int level) => (seed & ~3) | (level & 3);

    /// <summary>Dégâts locaux regroupés : un message par ennemi, effet d'impact, marqueur de touche.</summary>
    public static void Apply(World world, Dictionary<Enemy, (float dmg, Vector3 at, bool weak)> per)
    {
        foreach (var (e, v) in per)
        {
            Net.I.SendDamageEnemy(e.Id, v.dmg, v.at, v.weak);
            world.LocalHitFx(e, v.at, true, v.dmg, v.weak);
            if (world.LocalPlayer is { } p) { p.HitMarker = 1f; p.EarnCredits(v.dmg); }
        }
    }

    /// <summary>Ennemis dans un éventail horizontal (onde de choc) : dégâts à chacun.</summary>
    private static void Sector(World world, Vector3 from, Vector3 dir, float reach, float half, float dmg, Dictionary<Enemy, (float, Vector3, bool)> per)
    {
        var flat = new Vector3(dir.X, 0, dir.Z);
        flat = flat.LengthSquared() > 0.001f ? flat.Normalized() : Vector3.Forward;
        foreach (var e in world.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var c = e.Center;
            var size = e.Def.Size * 1.1f;
            var d = e.BodyDistance(from);
            if (d > reach + size) continue;
            var to = c - from;
            var tf = new Vector3(to.X, 0, to.Z);
            // Tout près : touché quel que soit l'angle (le corps déborde de l'éventail).
            if (d > size + 1.5f && tf.LengthSquared() > 0.01f && flat.AngleTo(tf.Normalized()) > half + Mathf.Atan2(size, Mathf.Max(tf.Length(), 0.1f))) continue;
            if (Mathf.Abs(to.Y) > reach * 0.5f + size + 2f) continue;
            per[e] = (dmg, c, false);
        }
    }

    /// <summary>Ennemis le long d'un segment (coup de lance) : tous s'il perce, sinon le premier.</summary>
    private static void Line(World world, Vector3 from, Vector3 to, float width, float dmg, bool pierce, Dictionary<Enemy, (float, Vector3, bool)> per)
    {
        Enemy first = null;
        var best = float.MaxValue;
        foreach (var e in world.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var p = Geometry3D.GetClosestPointToSegment(e.Center, from, to);
            if (e.BodyDistance(p) > width + e.Def.Size * 1.1f) continue;
            if (pierce) { per[e] = (dmg, p, false); continue; }
            var d = from.DistanceSquaredTo(p);
            if (d < best) { best = d; first = e; }
        }
        if (first != null) per[first] = (dmg, Geometry3D.GetClosestPointToSegment(first.Center, from, to), false);
    }

    /// <summary>Coups du Fencer (faux pour les autres natures).</summary>
    public static bool Shot(World world, Edf5Spec s, WeaponInstance w, WeaponTemplate t, Vector3 from, Vector3 dir, int seed,
        RandomNumberGenerator rng, bool local, Player shooter, float dmg, float spread, float range, float speed, float blast)
    {
        switch (s.Kind)
        {
            case Edf5Kind.Strike:
                Strike(world, s, from, dir, Level(seed), rng, local, shooter, dmg);
                return true;
            case Edf5Kind.Pierce:
                Pierce(world, s, from, dir, Level(seed), rng, local, dmg, spread, range, speed);
                return true;
            case Edf5Kind.Mortar:
                Mortar(world, s, from, dir, rng, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                return true;
        }
        return false;
    }

    private static void Strike(World world, Edf5Spec s, Vector3 from, Vector3 dir, int level, RandomNumberGenerator rng, bool local, Player shooter, float dmg)
    {
        var st = s.Strikes[Math.Clamp(level, 0, s.Strikes.Length - 1)];
        var power = dmg * st.Power * (shooter?.DamageMul ?? 1f);
        var flat = new Vector3(dir.X, 0, dir.Z);
        flat = flat.LengthSquared() > 0.001f ? flat.Normalized() : Vector3.Forward;
        var per = local ? new Dictionary<Enemy, (float, Vector3, bool)>() : null;
        var a = st.Attack;
        if (s.Family == "vulcan" && level > 0)
        {
            // Vulcan Hammer chargé : explosion au point frappé (rayon = portée d'EDF 5 : 4 à 10 m).
            var at = from + flat * 3.5f + Vector3.Down * 0.6f;
            if (local) Net.I.SendExplosion(at, st.Range, power, 0);
            MeleeWave.Arc(world, at + Vector3.Down * 0.5f, flat, st.Range, Mathf.Pi, MeleeWave.Hot);
            return;
        }
        if (a.Contains("Charge") && s.Family is "roller" or "forceax")
        {
            // Rouleau, Force Ax : charge vers l'avant sur la portée, en frappant ce qui est sur la route.
            if (local && shooter != null) shooter.FencerLunge(flat, st.Range, power);
            MeleeWave.Arc(world, from + Vector3.Down * 0.9f, flat, 3.5f, Mathf.DegToRad(50f), MeleeWave.Steel);
            if (s.Family == "forceax") MeleeWave.Blade(world, from, dir, 140f, st.Range, 3.2f, 0f, power, local, MeleeWave.Steel);
            return;
        }
        if (a.Contains("Slash") || a.Contains("Dispersal"))
        {
            // Lames d'énergie qui volent : seule, ou dispersées en éventail (avant / arrière).
            var back = a.StartsWith("Back");
            var baseDir = back ? -dir : dir;
            var n = Math.Max(1, st.Count);
            var fan = n > 1 ? Mathf.DegToRad(a.Contains("Slash") ? 50f : 40f) : 0f;
            var width = a.Contains("Long") ? 4.5f : a.Contains("Midium") ? 3.2f : s.Family == "haken" ? 2.2f : 2.6f;
            var sp = a.Contains("Long") ? 220f : 150f;
            for (var i = 0; i < n; i++)
            {
                var yaw = n > 1 ? Mathf.Lerp(-fan, fan, i / (float)(n - 1)) : 0f;
                var d = baseDir.Rotated(Vector3.Up, yaw);
                var roll = (rng.Randf() - 0.5f) * 0.6f + (s.Family == "forceax" ? 0.25f : 0f);
                MeleeWave.Blade(world, from + d * 1.2f, d, sp, Mathf.Min(st.Range, 400f), width, roll, power, local, s.Family == "haken" ? MeleeWave.Hot : MeleeWave.Steel);
            }
            return;
        }
        // Choc (marteaux, haken, rouleau et Vulcan au premier niveau) : onde en éventail au sol.
        var reach = Reach(st.Range);
        var half = Mathf.DegToRad(a.Contains("Wide") ? 80f : a.Contains("Midium") ? 55f : 40f);
        if (local) Sector(world, from, flat, reach, half, power * Math.Max(1, st.Count), per);
        MeleeWave.Arc(world, from + Vector3.Down * 1.1f, flat, reach, half, s.Family is "vulcan" ? MeleeWave.Hot : MeleeWave.Steel);
        Effects.Dust(world, from + flat * 2.5f + Vector3.Down * 1.2f, new Vector3(2.5f, 0.4f, 2.5f));
        if (per is { Count: > 0 }) Apply(world, per);
    }

    private static void Pierce(World world, Edf5Spec s, Vector3 from, Vector3 dir, int attack, RandomNumberGenerator rng, bool local,
        float dmg, float spread, float range, float speed)
    {
        var per = local ? new Dictionary<Enemy, (float, Vector3, bool)>() : null;
        if (s.Family == "spinedriver")
        {
            // Spine Driver : pieu d'acier projeté (portée réelle d'EDF 5).
            var d = Edf5Fire.Spread(dir, spread, rng);
            var sp = speed > 0 ? speed : 520f;
            Bullets.For(world).Add(new Bullets.Shot
            {
                Pos = from, Vel = d * sp, Life = range / sp, MaxLife = range / sp, Damage = dmg, Far = 1f, FalloffExp = 1f, Local = local,
                Color = new Color(0.85f, 0.9f, 1f), Length = 3.5f, Width = 0.14f,
            });
            MeleeWave.Spike(world, from, d, 3f, 0.18f, MeleeWave.Steel);
            return;
        }
        float mul = 1f, reachM = range;
        var n = 1;
        if (s.Strikes is { Length: > 0 } atk)
        {
            var a = atk[Math.Clamp(attack, 0, atk.Length - 1)];
            mul = a.Power;
            reachM = a.Range;
            n = Math.Max(1, a.Count);
        }
        var reach = PierceReach(reachM);
        var right = dir.Cross(Vector3.Up).Normalized();
        if (right.LengthSquared() < 0.01f) right = Vector3.Right;
        for (var i = 0; i < n; i++)
        {
            var off = n > 1 ? right * (i == 0 ? -0.45f : 0.45f) : Vector3.Zero;
            var d = s.Family is "jackhammer" or "flashing" ? Edf5Fire.Spread(dir, Mathf.DegToRad(2.5f), rng) : dir;
            var o = from + off;
            if (local) Line(world, o, o + d * reach, s.Family == "jackhammer" ? 0.7f : 0.9f, dmg * mul, s.Pierce, per);
            var thick = s.Family is "blasthole" or "twinspear" ? (attack > 0 ? 0.32f : 0.24f) : 0.16f;
            MeleeWave.Spike(world, o, d, reach, thick, attack > 0 ? MeleeWave.Hot : MeleeWave.Steel);
        }
        if (per is { Count: > 0 }) Apply(world, per);
    }

    /// <summary>
    /// Vitesse de départ d'un obus de mortier vers le point visé (vecteur complet, transmis tel quel aux autres pairs) :
    /// cloche basse (mortiers légers et lourds) ou haute (dispersion, Crusher).
    /// </summary>
    public static Vector3 MortarLaunch(Edf5Spec s, Vector3 from, Vector3 target, float maxRange)
    {
        var g = MortarGravity;
        var d = target - from;
        var flat = new Vector3(d.X, 0, d.Z);
        var x = Mathf.Max(flat.Length(), 1f);
        var y = d.Y;
        var high = s.Family is "dispersal" or "crusher";
        // Vitesse juste suffisante pour une cloche marquée, plafonnée par la portée de l'arme.
        var v = Mathf.Clamp(Mathf.Sqrt(g * (x + Mathf.Max(y, 0f) * 1.5f)) * (high ? 1.05f : 1.2f), 25f, Mathf.Sqrt(g * Mathf.Max(maxRange, 50f)));
        var v2 = v * v;
        var disc = v2 * v2 - g * (g * x * x + 2f * y * v2);
        float ang;
        if (disc < 0) ang = Mathf.Pi / 4f;
        else ang = Mathf.Atan((v2 + (high ? 1f : -1f) * Mathf.Sqrt(disc)) / (g * x));
        var h = flat / x;
        return (h * Mathf.Cos(ang) + Vector3.Up * Mathf.Sin(ang)) * v;
    }

    private static void Mortar(World world, Edf5Spec s, Vector3 from, Vector3 vel, RandomNumberGenerator rng, bool local, float dmg, float blast)
    {
        var n = Math.Max(1, s.Pellets);
        var size = s.Family switch { "crusher" => 7f, "heavymortar" => 2.2f, "dispersal" => 1f, _ => 1.4f };
        for (var i = 0; i < n; i++)
        {
            var v = vel;
            if (n > 1)
            {
                // Dispersion : chaque obus dévie un peu en direction et en vitesse (même graine chez tous les pairs).
                v = v.Rotated(Vector3.Up, (rng.Randf() - 0.5f) * 0.22f) * (0.88f + 0.24f * rng.Randf());
            }
            var p = Projectile.Spawn(world, ProjectileKind.Shell, from, v, local, dmg, blast);
            p.Gravity = MortarGravity;
            p.Life = 40f;
            p.Scale = Vector3.One * size;
        }
    }
}
