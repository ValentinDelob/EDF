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

    /// <summary>Graine d'un missile verrouillé : bit 30 + identifiant de l'ennemi visé.</summary>
    public static int MissileSeed(int enemyId) => (1 << 30) | (enemyId & 0x3FFFFFFF);

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
        if (lockTarget == null && s.Kind == Edf5Kind.Missile && (seed & (1 << 30)) != 0
            && world.EnemiesById.TryGetValue(seed & 0x3FFFFFFF, out var locked)) lockTarget = locked;
        var t = w?.T ?? Weapons.Get(s.Id);
        var dmg = (w?.Damage ?? t.Damage) * power;
        var spread = Mathf.DegToRad(w?.Spread ?? t.Spread) * (shooter is { Aiming: true } ? 0.6f : 1f);
        var range = w?.Range ?? t.Range;
        var speed = w?.ShotSpeed ?? (t.ProjectileSpeed > 0 ? t.ProjectileSpeed : 300f);
        var blast = w?.BlastRadius ?? t.BlastRadius;
        var bullets = Bullets.For(world);
        if (WingDiverShot(world, s, w, t, from, dir, rng, local, shooter, dmg, spread, range, speed, blast, power)) return;
        if (FencerFire.Shot(world, s, w, t, from, dir, seed, rng, local, shooter, dmg, spread, range, speed, blast)) return;
        if (AirRaid.Shot(world, s, w, t, from, dir, seed, local, shooter, dmg)) return;
        if (ArDeploy.Shot(world, s, w, t, from, dir, seed, rng, local, shooter, dmg, spread, range, speed, blast)) return;
        if (s.Kind == Edf5Kind.Limpet) { Limpet(world, s, from, dir, rng, local, shooter, dmg * (shooter?.DamageMul ?? 1f), spread, speed, blast); return; }
        switch (s.Kind)
        {
            case Edf5Kind.Bullet:
            case Edf5Kind.BoundGun:
            case Edf5Kind.Acid:
            {
                var acid = s.Kind == Edf5Kind.Acid;
                // Fusils à pompe : atténuation forte (x = 2,5) ; fusils d'assaut et de précision : linéaire (x = 1).
                var shotgun = s.Category == "Fusil à pompe" || s.Family is "dexter" or "cannonshot";
                var sniper = s.Category == "Fusil de précision";
                // Fencer : obus des canons à main et canons lourds (traits épais), canons Galleon.
                var shell = s.Family is "handcannon" or "gallic" or "powerdyne" or "galleon";
                // Sans « Shot Speed » dans les tables (Bound Gun) : 360 m/s (6 m par image d'EDF 5).
                if (speed <= 0) speed = 360f;
                var life = range / speed;
                for (var i = 0; i < Math.Max(1, s.Pellets); i++)
                {
                    var d = Spread(dir, spread, rng);
                    bullets.Add(new Bullets.Shot
                    {
                        Pos = from, Vel = d * speed, Life = life, MaxLife = life, Damage = dmg, Far = s.FarDamage,
                        FalloffExp = shotgun ? 2.5f : 1f, Pierce = s.Pierce, Blast = blast, Local = local,
                        Gravity = acid ? AcidGravity : 0f, Bounces = s.Kind == Edf5Kind.BoundGun ? 3 : 0,
                        Color = TracerColor(s), Length = sniper || shell ? Mathf.Min(speed * 0.04f, 30f) : Mathf.Clamp(speed * 0.022f, 1.2f, 9f),
                        Width = shell ? (s.Family == "gallic" ? 0.22f : 0.14f) : sniper ? 0.07f : acid ? 0.16f : s.Pellets > 1 ? (s.Family == "cannonshot" ? 0.07f : 0.035f) : 0.05f,
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
                // Guidage : virage (rad/s) selon le rang « Homing » d'EDF 5 (S++ très vif … C+ lent).
                var turn = s.Homing >= 0 ? Mathf.Lerp(5.5f, 1.1f, s.Homing / 12f) : 2.5f;
                var n = s.Family is "fork" or "bloodstorm" or "haytal" ? Math.Max(1, s.Pellets) : 1;
                var fl = Flight(s);
                for (var i = 0; i < n; i++)
                {
                    // Départ : direction de la visée relevée selon le lanceur, avec la dispersion d'une salve ; missiles à kit de
                    // guidage du Fencer (Leviathan, Haytal, Phoenix) sans cible : tir droit.
                    var scatter = s.GuideOnly && lockTarget == null ? (n > 1 ? 0.18f : 0.02f) : fl.scatter;
                    // Sans cible : départ droit vers la visée (sauf le relèvement léger des lanceurs ordinaires).
                    var up = lockTarget != null ? fl.up : s.GuideOnly ? 0f : Mathf.Min(fl.up, 0.08f);
                    var d = (dir + Vector3.Up * up + RandomOnSphere(rng) * scatter).Normalized();
                    var p = Projectile.Spawn(world, ProjectileKind.Missile, from, d * fl.launch, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                    p.Life = s.Family is "leviathan" or "tortoise" or "prominence" or "fgimpact" ? 20f : 12f;
                    p.MaxSpeed = fl.top;
                    p.Accel = fl.accel;
                    p.HomingDelay = fl.delay + (n > 1 ? rng.Randf() * 0.12f : 0f);
                    // Sans cible, pas de montée : le missile part droit vers la visée.
                    if (fl.climb > 0 && lockTarget != null) p.ClimbTo = from.Y + fl.climb + rng.Randf() * fl.climb * 0.2f;
                    if (s.Family is "leviathan" or "phoenix") p.Scale = Vector3.One * (s.Family == "leviathan" ? 3.2f : 1.8f);
                    else if (s.Family is "bloodstorm" or "mlra") p.Scale = Vector3.One * 0.6f;
                    else if (s.Family is "tortoise" or "prominence") p.Scale = Vector3.One * 1.8f;
                    if (lockTarget != null) p.SetHoming(lockTarget, turn);
                }
                break;
            }
            case Edf5Kind.HandGrenade:
            {
                // Lancer à la main : les « DNG » et la MG13 en lancer appuyé (lent, court).
                var heavy = s.Family == "dng" || s.Name is "MG13" or "MG13J";
                var v = (dir + Vector3.Up * 0.32f).Normalized() * (heavy ? 18f : 26f);
                // Split Ball (Wing Diver) : la boule se divise en « Pellets » boules lancées en éventail.
                var balls = Math.Max(1, s.Pellets);
                for (var i = 0; i < balls; i++)
                {
                    var vi = balls == 1 ? v : v.Rotated(Vector3.Up, Mathf.Lerp(-1f, 1f, i / (float)(balls - 1)) * Mathf.DegToRad(10f)) * (0.9f + 0.2f * rng.Randf());
                    var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, vi, local, dmg * (shooter?.DamageMul ?? 1f), blast);
                    if (s.Fuse > 0) { p.Life = s.Fuse; p.Bounce = true; }
                    else p.Life = 12f;
                }
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
                    var v = d * sp + Vector3.Up * (s.Family is "stampede" or "splash" or "sticky" ? 5f : 2.5f);
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
                // Copie d'un autre pair : la mise à feu n'est pas transmise, la bombe s'efface (l'explosion vient de l'hôte).
                if (!local) p.Life = 8f;
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

    /// <summary>
    /// Bombes collantes de l'Air Raider : elles se collent à ce qu'elles touchent et explosent à la mise à feu (rechargement ou
    /// tir chargeur vide) ; Splendor, Buster Bomb et détecteurs éclatent en gerbe d'éclats ; détecteurs : mise à feu d'eux-mêmes
    /// à l'approche d'un ennemi (« Search Distance »).
    /// </summary>
    private static void Limpet(World world, Edf5Spec s, Vector3 from, Vector3 dir, RandomNumberGenerator rng, bool local, Player shooter,
        float dmg, float spread, float speed, float blast)
    {
        var burst = s.Family is "splendor" or "busterbomb" or "detector";
        // Limpet Shot et Splendor Shot (EDF 5 : 13,6 × 15 × 8) : plusieurs bombes par tir ; Splendor Shot : 8 éclats par bombe ;
        // autres Splendor, Buster Bomb, détecteurs : « Pellets » = éclats.
        var many = s.Family == "limpetshot" || s.Name == "Splendor Shot";
        var bombs = many ? Math.Max(1, s.Pellets) : 1;
        var frags = s.Name == "Splendor Shot" ? 8 : burst ? Math.Max(1, s.Pellets) : 0;
        var sp = speed > 0 ? speed : 300f;
        for (var i = 0; i < bombs; i++)
        {
            var d = many ? Spread(dir, Mathf.Max(spread, Mathf.DegToRad(6f)), rng) : Spread(dir, spread, rng);
            var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, d * sp, local, dmg, blast > 0 ? blast : 2.5f);
            p.Gravity = 6f;
            p.Sticky = true;
            p.Life = local ? 600f : 8f;
            p.Scale = Vector3.One * (s.Family is "busterbomb" or "limpetsniper" ? 1.6f : 1f);
            p.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f }, MaterialOverride = Mats.Glow(new Color(1f, 0.25f, 0.1f), 5f), Position = Vector3.Up * 0.12f });
            if (frags > 0)
            {
                var n = frags;
                p.CustomImpact = (at, nrm) =>
                {
                    // Gerbe d'éclats en demi-sphère dans la direction du dessus de la bombe (EDF 5) : collée sur un ennemi,
                    // elle l'épargne presque ; posée sous des ennemis ou dans un espace fermé, elle fait le plus de dégâts.
                    Effects.Explosion(world, at, 1.6f, ExplosionFx.Explosive);
                    if (p.StuckTo != null) nrm = p.StuckNormal;
                    for (var k = 0; k < n; k++)
                    {
                        var fd = (RandomOnSphere(rng) + nrm * 0.6f).Normalized();
                        Bullets.For(world).Add(new Bullets.Shot
                        {
                            Pos = at + nrm * 0.2f, Vel = fd * 220f, Life = 0.12f, MaxLife = 0.12f, Damage = dmg, Far = 1f, FalloffExp = 1f,
                            Local = local, Color = new Color(1f, 0.7f, 0.3f), Length = 1.5f, Width = 0.04f,
                        });
                    }
                };
            }
            if (s.Family == "detector" && local) p.ProximityRadius = s.Search.Present ? s.Search.Max * 0.3f : 8f;
            if (local) shooter?.Edf5PlacedBomb(p);
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
                // Vitesse d'EDF 5 (« Shot Speed »).
                var sp = speed > 0 ? speed : 120f;
                // Plasmafall : les boules montent puis retombent en pluie autour du point visé (tireur local ; les copies des
                // autres pairs gardent l'éventail montant, sans dégâts).
                if (s.Family == "plasmafall" && shooter != null)
                {
                    var aimAt = shooter.AimPoint(Mathf.Min(range, 400f), out _);
                    const float g = 28f;
                    for (var i = 0; i < pellets; i++)
                    {
                        var a = rng.Randf() * Mathf.Tau;
                        var tgt = aimAt + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Mathf.Sqrt(rng.Randf()) * Mathf.Max(blast * 0.4f, 2f);
                        var apex = Mathf.Max(from.Y, tgt.Y) + 18f + rng.Randf() * 6f;
                        var vy = Mathf.Sqrt(2f * g * (apex - from.Y));
                        var tt = vy / g + Mathf.Sqrt(2f * (apex - tgt.Y) / g);
                        var flat = new Vector3(tgt.X - from.X, 0, tgt.Z - from.Z) / tt;
                        var pf = Projectile.Spawn(world, ProjectileKind.Plasma, from, flat + Vector3.Up * vy, local, dmg * mul, Mathf.Max(blast, 2f));
                        pf.Gravity = g;
                        pf.Life = tt + 1f;
                    }
                    return true;
                }
                for (var i = 0; i < pellets; i++)
                {
                    Vector3 d;
                    // Splasher : gerbe de boules de plasma lancée devant soi, en légère cloche.
                    if (s.Family == "splasher") d = (dir + Vector3.Up * 0.06f + RandomOnSphere(rng) * 0.12f).Normalized();
                    else if (pellets > 1 && s.Family != "plasmafall") d = dir.Rotated(Vector3.Up, Mathf.Lerp(-1f, 1f, i / (float)(pellets - 1)) * Mathf.DegToRad(5f));
                    else d = Spread(dir, spread, rng);
                    var up = s.Family == "plasmafall" ? Vector3.Up * (i * 0.08f) : Vector3.Zero;
                    var p = Projectile.Spawn(world, ProjectileKind.Plasma, from, (d + up).Normalized() * sp, local, dmg * mul, Mathf.Max(blast, 2f));
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

    /// <summary>
    /// Vol des missiles par lanceur, d'après EDF 5 : relèvement au départ, dispersion de la salve, vitesse de sortie, vitesse de
    /// croisière et accélération, durée de la poussée sans guidage, hauteur de montée des tirs en piqué.
    /// </summary>
    private static (float up, float scatter, float launch, float top, float accel, float delay, float climb) Flight(Edf5Spec s) => s.Family switch
    {
        // Ranger
        "emerald" => (0.12f, 0.06f, 35f, 120f, 160f, 0.18f, 0f),
        "tortoise" => (0.55f, 0.08f, 18f, 55f, 40f, 0.35f, 45f),
        "mlra" => (0.9f, 0.35f, 30f, 105f, 140f, 0.25f, 30f),
        "fork" => (0.25f, 0.15f, 40f, 115f, 150f, 0.2f, 0f),
        "prominence" => (3f, 0.05f, 25f, 90f, 70f, 0.3f, 140f),
        "falcon" => (0.08f, 0.04f, 60f, 170f, 250f, 0.12f, 0f),
        // Fencer
        "armhound" => (0.7f, 0.3f, 30f, 125f, 170f, 0.22f, 0f),
        "bloodstorm" => (0.35f, 0.45f, 40f, 85f, 90f, 0.15f, 0f),
        "arcane" => (0.15f, 0.08f, 40f, 115f, 160f, 0.15f, 0f),
        "fgimpact" => (3f, 0.12f, 30f, 110f, 90f, 0.25f, 120f),
        "leviathan" => (0.05f, 0.02f, 25f, 60f, 30f, 0.4f, 0f),
        "haytal" => (0.08f, 0.2f, 40f, 95f, 90f, 0.2f, 0f),
        "phoenix" => (0.03f, 0.02f, 60f, 170f, 220f, 0.15f, 0f),
        _ => (0.3f, 0.12f, 35f, 100f, 120f, 0.2f, 0f),
    };

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
