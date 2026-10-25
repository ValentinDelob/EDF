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
    /// <summary>Cibles verrouillées pour le prochain coup d'une arme guidée de la Wing Diver (null : tir droit devant).</summary>
    public static System.Collections.Generic.List<Node3D> HomingTargets;

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
        Player shooter = null, Func<Vector3> aim = null, Node3D lockTarget = null, float power = 1f)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed };
        var t = w?.T ?? Weapons.Get(s.Id);
        var dmg = (w?.Damage ?? t.Damage) * power;
        var spread = Mathf.DegToRad(w?.Spread ?? t.Spread) * (shooter is { Aiming: true } ? 0.6f : 1f);
        var range = w?.Range ?? t.Range;
        var speed = w?.ShotSpeed ?? (t.ProjectileSpeed > 0 ? t.ProjectileSpeed : 300f);
        var blast = w?.BlastRadius ?? t.BlastRadius;
        var bullets = Bullets.For(world);
        if (WingDiverShot(world, s, w, t, from, dir, rng, local, shooter, dmg, spread, range, speed, blast, power)) return;
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

    private static readonly Color ArcBlue = new(0.55f, 0.82f, 1f), Electric = new(0.75f, 0.7f, 1f), PulseCyan = new(0.35f, 1f, 0.95f),
        LaserRed = new(1f, 0.35f, 0.25f);

    /// <summary>Coups des armes de la Wing Diver (faux pour les autres natures).</summary>
    private static bool WingDiverShot(World world, Edf5Spec s, WeaponInstance w, WeaponTemplate t, Vector3 from, Vector3 dir,
        RandomNumberGenerator rng, bool local, Player shooter, float dmg, float spread, float range, float speed, float blast, float power)
    {
        var bullets = Bullets.For(world);
        var mul = shooter?.DamageMul ?? 1f;
        // Armes à charge à projectiles multiples (Thunder Crossbow, Stardust Cannon…) : autant de projectiles que la charge.
        var pellets = Math.Max(1, s.Pellets);
        if (s.Supply == Edf5Supply.Charge && pellets > 1)
        {
            pellets = Math.Max(1, (int)MathF.Round(pellets * power));
            dmg /= Mathf.Max(power, 0.05f);
        }
        void Add(Vector3 d, float sp, float rangeM, float width, float len, Color c, int bounces = 0, float grav = 0f, bool pierce = false, float bl = 0f, float far = -1f) =>
            bullets.Add(new Bullets.Shot
            {
                Pos = from, Vel = d * sp, Life = rangeM / sp, MaxLife = rangeM / sp, Damage = dmg, Far = far >= 0 ? far : s.FarDamage, FalloffExp = 1f,
                Pierce = pierce || s.Pierce, Blast = bl, Local = local, Gravity = grav, Bounces = bounces, Color = c, Length = len, Width = width,
            });
        switch (s.Kind)
        {
            case Edf5Kind.Arc:
                // Lames d'arc de plasma : gerbe à très courte portée (Rapier), décharge plus longue et plus dense (Phalanx).
                for (var i = 0; i < pellets; i++)
                {
                    var d = s.Family == "phalanx" && s.Name.Contains("W") ? dir.Rotated(Vector3.Up, (rng.Randf() - 0.5f) * spread * 2.5f) : Spread(dir, spread, rng);
                    Add(d, 420f, range * (0.75f + 0.25f * rng.Randf()), 0.05f, 2.2f, ArcBlue);
                }
                return true;
            case Edf5Kind.Lance:
                Add(dir, 2400f, range, s.Family == "dragoon" ? 0.2f : 0.13f, Mathf.Min(range * 0.6f, 70f), ArcBlue, pierce: s.Family == "dragoon");
                Effects.Beam(world, from, from + dir * Mathf.Min(range, 60f), ArcBlue, s.Family == "dragoon" ? 0.12f : 0.07f);
                return true;
            case Edf5Kind.Spark:
            {
                var sp = speed > 0 ? speed : 400f;
                for (var i = 0; i < pellets; i++)
                    Add(Spread(dir, Mathf.Max(spread, Mathf.DegToRad(s.Family is "sparkwhip" or "boltgun" ? 9f : 3f)), rng), sp, range, 0.06f, 3f, Electric, 3);
                return true;
            }
            case Edf5Kind.Beam:
                for (var i = 0; i < pellets; i++)
                    Add(Spread(dir, spread, rng), 1400f, range, s.Family == "destroyer" ? 0.05f : 0.09f, 9f, s.Family == "spine" ? Electric : ArcBlue,
                        s.Family == "reflectron" ? 2 : 0);
                return true;
            case Edf5Kind.Pulse:
            {
                var sp = speed > 0 ? speed : 150f;
                var way = s.Name.Contains("Way") ? pellets : 0;
                for (var i = 0; i < pellets; i++)
                {
                    var d = way > 1 ? dir.Rotated(Vector3.Up, Mathf.Lerp(-1f, 1f, i / (float)(way - 1)) * Mathf.DegToRad(4f + 2.5f * way)) : Spread(dir, spread, rng);
                    Add(d, sp, range, 0.22f, 1.1f, PulseCyan, bl: Mathf.Max(blast, 0.8f), far: 1f);
                }
                return true;
            }
            case Edf5Kind.Laser:
                for (var i = 0; i < pellets; i++)
                    Add(Spread(dir, spread, rng), speed > 0 ? Mathf.Min(speed, 3500f) : 3000f, range, 0.06f, Mathf.Min(range * 0.25f, 60f), LaserRed);
                return true;
            case Edf5Kind.PlasmaShot:
            {
                var sp = Mathf.Clamp(speed > 0 ? speed : 120f, 40f, 180f);
                for (var i = 0; i < pellets; i++)
                {
                    Vector3 d;
                    if (s.Family == "splasher") d = (dir + Vector3.Up * 0.6f + RandomOnSphere(rng) * 0.35f).Normalized();
                    else if (pellets > 1 && s.Family != "plasmafall") d = dir.Rotated(Vector3.Up, Mathf.Lerp(-1f, 1f, i / (float)(pellets - 1)) * Mathf.DegToRad(5f));
                    else d = Spread(dir, spread, rng);
                    var up = s.Family == "plasmafall" ? Vector3.Up * (i * 0.08f) : Vector3.Zero;
                    var p = Projectile.Spawn(world, ProjectileKind.Plasma, from, (d + up).Normalized() * sp * (s.Family == "splasher" ? 0.45f : 1f), local, dmg * mul, Mathf.Max(blast, 2f));
                    p.Gravity = s.Family == "plasmafall" ? 28f : s.Family is "bigcannon" or "heavycannon" ? 14f : 9f;
                    p.Life = Mathf.Clamp(range / sp, 2f, 14f);
                }
                return true;
            }
            case Edf5Kind.Homing:
            {
                var targets = HomingTargets;
                var sp = s.Family switch { "geist" => 28f, "fenrir" => 160f, "ghostchaser" => 55f, "psiblade" => 80f, _ => 95f };
                var turn = s.Homing >= 0 ? Mathf.Lerp(8f, 1.5f, s.Homing / 12f) : 4f;
                for (var i = 0; i < pellets; i++)
                {
                    var d = s.Family == "ghostchaser" ? (dir + RandomOnSphere(rng) * 0.9f).Normalized()
                        : pellets > 1 ? Spread(dir, Mathf.DegToRad(18f), rng) : dir;
                    var p = Projectile.Spawn(world, ProjectileKind.Energy, from, d * sp, local, dmg * mul, blast > 0 ? blast : 1.2f);
                    p.Life = 6f;
                    if (targets is { Count: > 0 }) p.SetHoming(targets[i % targets.Count], turn);
                }
                return true;
            }
            case Edf5Kind.Orb:
                EnergyOrb.Spawn(world, s, dmg * mul, from, dir, local);
                return true;
        }
        return false;
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
