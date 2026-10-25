using System;
using Godot;

namespace Rempart;

/// <summary>
/// Départ des coups des armes d'EDF 5 : balles, roquettes, missiles, grenades, napalm, acide, pétards, balles rebondissantes,
/// bombes C, mines Impulse, Reverser. Le même code sert au joueur local (dégâts) et aux autres joueurs (effet seul) :
/// l'aléa de dispersion vient d'une graine transmise avec le tir (<see cref="Net.SendEdf5Shot"/>), les deux côtés tirent
/// donc les mêmes trajectoires.
/// </summary>
public static class Edf5Fire
{
    /// <summary>Gravité des grenades, de l'acide et des pétards (m/s²).</summary>
    public const float GrenadeGravity = 18f, AcidGravity = 9f;

    /// <summary>Couleur du trait des balles selon la famille.</summary>
    private static Color TracerColor(Edf5Spec s) => s.Family switch
    {
        "acid" => new Color(0.65f, 1f, 0.2f),
        "boundgun" => new Color(0.6f, 0.9f, 1f),
        "firecracker" => new Color(1f, 0.5f, 0.2f),
        "gm" or "kff" or "lysander" or "penetrator" or "dunkel" => new Color(1f, 0.88f, 0.55f),
        _ => new Color(1f, 0.72f, 0.32f),
    };

    /// <summary>
    /// Un coup (une pression de détente, une balle d'une rafale) : <paramref name="w"/> null côté distant (valeurs à ★5).
    /// <paramref name="aim"/> : point visé (guidage des roquettes Leopard / Ibis, côté local seulement).
    /// </summary>
    public static void Shoot(World world, Edf5Spec s, WeaponInstance w, Vector3 from, Vector3 dir, int seed, bool local,
        Player shooter = null, Func<Vector3> aim = null, Node3D lockTarget = null)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
        var t = w?.T ?? Weapons.Get(s.Id);
        var dmg = w?.Damage ?? t.Damage;
        var spread = Mathf.DegToRad(w?.Spread ?? t.Spread) * (shooter is { Aiming: true } ? 0.6f : 1f);
        var range = w?.Range ?? t.Range;
        var speed = w?.ShotSpeed ?? (t.ProjectileSpeed > 0 ? t.ProjectileSpeed : 300f);
        var blast = w?.BlastRadius ?? t.BlastRadius;
        var bullets = Bullets.For(world);
        switch (s.Kind)
        {
            case Edf5Kind.Bullet:
            case Edf5Kind.BoundGun:
            case Edf5Kind.Acid:
            {
                var acid = s.Kind == Edf5Kind.Acid;
                // Fusils à pompe : atténuation forte (x = 2,5) ; fusils d'assaut et de précision : linéaire (x = 1).
                var shotgun = s.Category == "Fusil à pompe";
                var sniper = s.Category == "Fusil de précision";
                var life = range / Mathf.Max(speed, 1f);
                for (var i = 0; i < Math.Max(1, s.Pellets); i++)
                {
                    var d = Spread(dir, spread, rng);
                    bullets.Add(new Bullets.Shot
                    {
                        Pos = from, Vel = d * speed, Life = life, MaxLife = life, Damage = dmg, Far = s.FarDamage,
                        FalloffExp = shotgun ? 2.5f : 1f, Pierce = s.Pierce, Blast = blast, Local = local,
                        Gravity = acid ? AcidGravity : 0f, Bounces = s.Kind == Edf5Kind.BoundGun ? 3 : 0,
                        Color = TracerColor(s), Length = sniper ? Mathf.Min(speed * 0.04f, 30f) : Mathf.Clamp(speed * 0.022f, 1.2f, 9f),
                        Width = sniper ? 0.07f : acid ? 0.16f : s.Pellets > 1 ? 0.035f : 0.05f,
                    });
                }
                break;
            }
            case Edf5Kind.Firecracker:
            {
                // Pétards : gerbe de petites charges explosives qui retombent en pluie (portée courte).
                for (var i = 0; i < Math.Max(1, s.Pellets); i++)
                {
                    var d = Spread(dir, Mathf.DegToRad(16f), rng);
                    var v = d * (32f + rng.Randf() * 18f) + Vector3.Up * (4f + rng.Randf() * 4f);
                    bullets.Add(new Bullets.Shot
                    {
                        Pos = from, Vel = v, Life = 2.5f, MaxLife = 2.5f, Damage = dmg, Far = 1f, FalloffExp = 1f,
                        Blast = Mathf.Max(blast, 0.8f), Local = local, Gravity = GrenadeGravity, Color = TracerColor(s), Length = 0.5f, Width = 0.12f,
                    });
                }
                break;
            }
            case Edf5Kind.Rocket:
            case Edf5Kind.GuidedRocket:
            {
                var d = Spread(dir, spread, rng);
                var rs = s.Family switch { "goliath" => 110f, "cascade" => 70f, "hornet" => 95f, "volcano" => 85f, _ => 90f };
                var p = Projectile.Spawn(world, ProjectileKind.Rocket, from, d * rs, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                p.Life = 8f;
                if (s.Kind == Edf5Kind.GuidedRocket && aim != null)
                {
                    p.Guide = aim;
                    p.TurnRate = s.Family == "ibis" ? 4.5f : 2.6f;
                }
                break;
            }
            case Edf5Kind.Missile:
            {
                // Missiles : départ vers le haut (Air Tortoise, Prominence : très haut puis piqué), guidage selon le rang.
                var turn = s.Homing >= 0 ? Mathf.Lerp(7f, 1.2f, s.Homing / 12f) : 2.5f;
                var n = s.Family == "fork" ? Math.Max(1, s.Pellets) : 1;
                for (var i = 0; i < n; i++)
                {
                    var up = s.Family is "tortoise" or "prominence" ? 0.85f : 0.35f;
                    var d = (dir + Vector3.Up * up + RandomOnSphere(rng) * (n > 1 ? 0.6f : 0.12f)).Normalized();
                    var ms = s.Family is "tortoise" ? 45f : s.Family == "prominence" ? 70f : 85f;
                    var p = Projectile.Spawn(world, ProjectileKind.Missile, from, d * ms, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                    p.Life = 12f;
                    if (lockTarget != null) p.SetHoming(lockTarget, turn);
                }
                break;
            }
            case Edf5Kind.HandGrenade:
            {
                // Lancer à la main : les « DNG » et la MG13 en lancer appuyé (lent, court).
                var heavy = s.Family == "dng" || s.Name is "MG13" or "MG13J";
                var v = (dir + Vector3.Up * 0.32f).Normalized() * (heavy ? 18f : 26f);
                var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, v, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                if (s.Fuse > 0) { p.Life = s.Fuse; p.Bounce = true; }
                else p.Life = 12f;
                break;
            }
            case Edf5Kind.GrenadeLauncher:
            {
                var n = Math.Max(1, s.Pellets);
                for (var i = 0; i < n; i++)
                {
                    Vector3 d;
                    if (s.Family is "brute" or "crumble")
                    {
                        // Triple / double : éventail horizontal.
                        var a = n == 1 ? 0 : Mathf.Lerp(-1f, 1f, i / (float)(n - 1)) * Mathf.DegToRad(n == 2 ? 4f : 6f);
                        d = dir.Rotated(Vector3.Up, a);
                    }
                    else d = Spread(dir, spread, rng);
                    var sp = s.Family is "stampede" or "splash" ? speed * (0.75f + 0.5f * rng.Randf()) : speed;
                    var v = d * Mathf.Min(sp, 140f) + Vector3.Up * (s.Family is "stampede" or "splash" or "sticky" ? 5f : 2.5f);
                    var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, v, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                    p.Gravity = GrenadeGravity;
                    p.Life = s.Fuse > 0 ? s.Fuse : 10f;
                    if (s.Fuse > 0) { p.Bounce = s.Family != "sticky"; p.Sticky = s.Family == "sticky"; }
                }
                break;
            }
            case Edf5Kind.Napalm:
            {
                var burn = s.Family == "geyser" ? 15f : s.Name.StartsWith("Volcanic") ? 30f : 6f;
                var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, dir * Mathf.Min(speed, 120f) + Vector3.Up * 2f, local, 0, 0);
                p.Gravity = GrenadeGravity;
                p.Life = 10f;
                var total = dmg * (shooter?.DamageMul ?? 1f);
                var geyser = s.Family == "geyser";
                var wide = s.Name.StartsWith("Volcanic");
                p.CustomImpact = (at, n) => FireField.Spawn(world, at, n, geyser ? 2.5f : wide ? 22f : 7f, burn, total, local, geyser);
                break;
            }
            case Edf5Kind.Bomb:
            {
                // Bombe C : posée devant le soldat (collée à ce qu'elle touche), explose sur commande.
                var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, (dir + Vector3.Up * 0.2f).Normalized() * 14f, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                p.Gravity = GrenadeGravity;
                p.Sticky = true;
                p.Life = 600f;
                shooter?.Edf5PlacedBomb(p);
                break;
            }
            case Edf5Kind.Impulse:
            {
                var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, (dir + Vector3.Up * 0.25f).Normalized() * 16f, false, 0, 0);
                p.Gravity = GrenadeGravity;
                p.Life = 4f;
                var search = w?.Range ?? t.Range;
                var aa = s.Name.Contains("Anti-aircraft") || s.Name.Contains("Strike");
                p.CustomImpact = (at, n) => ImpulseMine.Spawn(world, at, s, dmg * (shooter?.DamageMul ?? 1f), search, aa, local);
                break;
            }
            case Edf5Kind.ReverseShooter:
            case Edf5Kind.Reverser: // Reverse Gun : tir de soin (les Reverser à jet continu ne passent pas ici)
            {
                var p = Projectile.Spawn(world, ProjectileKind.Plasma, from, dir * 90f, false, 0, 0);
                p.Life = 6f;
                var heal = dmg;
                var radius = Mathf.Max(blast, 2f);
                var n = Math.Max(1, s.Pellets);
                p.CustomImpact = (at, nrm) =>
                {
                    Effects.Ring(world, at, radius, new Color(0.4f, 1f, 0.6f), 0.5f);
                    if (local) world.LocalHealArea(at, radius, heal * n);
                };
                break;
            }
        }
    }

    public static Vector3 Spread(Vector3 dir, float rad, RandomNumberGenerator rng)
    {
        if (rad <= 0) return dir;
        var up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        var right = dir.Cross(up).Normalized();
        var up2 = right.Cross(dir).Normalized();
        var r = rad * Mathf.Sqrt(rng.Randf());
        var a = rng.Randf() * Mathf.Tau;
        return (dir + right * Mathf.Cos(a) * Mathf.Tan(r) + up2 * Mathf.Sin(a) * Mathf.Tan(r)).Normalized();
    }

    private static Vector3 RandomOnSphere(RandomNumberGenerator rng)
    {
        var z = rng.Randf() * 2f - 1f;
        var a = rng.Randf() * Mathf.Tau;
        var r = Mathf.Sqrt(1f - z * z);
        return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
    }
}
