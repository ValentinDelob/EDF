using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Frappes appelées de l'Air Raider : artillerie et bombardiers sur un fumigène lancé, canonnière, missiles et satellites sur le
/// point désigné au laser. Le tireur lance la frappe chez lui (dégâts) et l'annonce aux autres pairs avec le point visé
/// (<see cref="Net.SendEdf5Shot"/> : « from » = cible), qui la rejouent à l'identique grâce à la graine (effet seul).
/// </summary>
public static partial class AirRaid
{
    /// <summary>Altitude de la canonnière et des bombardiers (m au-dessus de la cible).</summary>
    public const float GunshipAlt = 150f, BomberAlt = 70f;

    /// <summary>Natures dont le tireur envoie lui-même l'annonce réseau (au point frappé, pas à la bouche de l'arme).</summary>
    public static bool SendsItself(Edf5Spec s) => s.Kind is Edf5Kind.Artillery or Edf5Kind.Bomber or Edf5Kind.Gunship
        or Edf5Kind.AirMissile or Edf5Kind.Satellite;

    /// <summary>Frappe qui exige le ciel (refusée sous terre).</summary>
    public static bool NeedsSky(Edf5Spec s) => SendsItself(s);

    /// <summary>Point laser de chaque tireur local (missiles et rayon du satellite le suivent tant que le bouton est tenu).</summary>
    public static readonly Dictionary<Player, Node3D> LaserDots = new();

    /// <summary>Point laser du joueur (créé à la demande, placé dans la scène).</summary>
    public static Node3D DotFor(Player p)
    {
        if (LaserDots.TryGetValue(p, out var d) && GodotObject.IsInstanceValid(d)) return d;
        d = new Node3D();
        d.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.1f, 0.06f), 6f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        d.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.2f, 0.1f), LightEnergy = 2f, OmniRange = 6f });
        p.World.FxRoot.AddChild(d);
        LaserDots[p] = d;
        return d;
    }

    /// <summary>Explosion du tireur : dégâts chez l'hôte, effet chez tous ; les points estimés vont au tireur.</summary>
    public static void Explode(World w, Vector3 at, float radius, float dmg, int kind = 0)
    {
        Net.I.SendExplosion(at, radius, dmg, kind);
        w.LocalPlayer?.EarnCredits(w.BlastEstimate(at, radius, dmg));
    }

    public static bool Shot(World world, Edf5Spec s, WeaponInstance w, WeaponTemplate t, Vector3 from, Vector3 dir, int seed, bool local,
        Player shooter, float dmg)
    {
        if (!SendsItself(s)) return false;
        var flat = new Vector3(dir.X, 0, dir.Z);
        flat = flat.LengthSquared() > 0.001f ? flat.Normalized() : Vector3.Forward;
        if (!local || shooter == null)
        {
            Begin(world, s, w, t, from, flat, seed, false, null, dmg);
            return true;
        }
        if (s.Kind is Edf5Kind.Artillery or Edf5Kind.Bomber)
        {
            // Fumigène lancé : la frappe part de son point de chute (direction des bombardiers : soldat → fumigène).
            var v = (dir + Vector3.Up * 0.35f).Normalized() * 24f;
            var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, v, false, 0, 0);
            p.Gravity = Edf5Fire.GrenadeGravity;
            p.Life = 8f;
            p.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 0.25f }, MaterialOverride = Mats.Glow(new Color(1f, 0.25f, 0.1f), 3f) });
            p.CustomImpact = (at, n) =>
            {
                var d2 = at - shooter.GlobalPosition;
                d2.Y = 0;
                var fd = d2.LengthSquared() > 1f ? d2.Normalized() : flat;
                Begin(world, s, w, t, at, fd, seed, true, shooter, dmg);
                Net.I.SendEdf5Shot(s.Id, at, fd, seed);
            };
            return true;
        }
        // Canonnière, missiles, satellites : point désigné au laser.
        var range = w?.Range ?? t.Range;
        var target = shooter.AimPoint(Mathf.Max(range, 50f), out _);
        Begin(world, s, w, t, target, flat, seed, true, shooter, dmg);
        Net.I.SendEdf5Shot(s.Id, target, flat, seed);
        return true;
    }

    private static void Begin(World world, Edf5Spec s, WeaponInstance w, WeaponTemplate t, Vector3 target, Vector3 dir, int seed, bool local,
        Player shooter, float dmg)
    {
        var strike = new Strike
        {
            World = world, S = s, Target = target, Dir = dir, Local = local, Shooter = shooter, Damage = dmg,
            Blast = w?.BlastRadius ?? t.BlastRadius, Speed = w?.ShotSpeed ?? t.ProjectileSpeed,
            Rng = new RandomNumberGenerator { Seed = (ulong)(uint)seed },
        };
        world.FxRoot.AddChild(strike);
        strike.GlobalPosition = target;
    }

    /// <summary>Déroulement d'une frappe (minuterie, coups, appareils) ; dégâts seulement chez le tireur.</summary>
    public partial class Strike : Node3D
    {
        public World World;
        public Edf5Spec S;
        public Vector3 Target, Dir;
        public bool Local;
        public Player Shooter;
        public float Damage, Blast, Speed;
        public RandomNumberGenerator Rng;
        private float _t;
        private int _fired;
        private Node3D _craft, _beam, _dot;
        private float _craftT;
        private Vector3 _beamAt;
        private readonly List<Projectile> _missiles = new();

        private int Count => Math.Max(1, S.Shots) * Math.Max(1, S.Rows);

        /// <summary>Délai avant le premier coup, intervalle entre les coups.</summary>
        private (float delay, float gap) Timing => S.Kind switch
        {
            Edf5Kind.Artillery => S.Family switch
            {
                "howitzer" or "gigantic" => (4f, 0.55f), "cannongun" => (3f, 0.12f), _ => (2.5f, 0.28f),
            },
            Edf5Kind.Gunship => S.Family switch
            {
                "gsauto" => (0.8f, 0.035f), "gs105" => (0.8f, 0.22f), "gsneutral" => (0.6f, 0.12f), "gsmulti" => (0.6f, 0.18f), _ => (0.6f, 0.1f),
            },
            Edf5Kind.AirMissile => (0.4f, S.Family == "lionic" ? 0.07f : 0.3f),
            Edf5Kind.Satellite => S.Family == "bulge" ? (1.2f, 1f / 60f) : S.Shots == 1 ? (1.4f, 0.1f) : (1.8f, 4.5f / Math.Max(1, S.Shots)),
            _ => (0f, 0.1f),
        };

        public override void _Ready()
        {
            // Fumigène rouge sur la cible ; sifflement de l'artillerie.
            if (S.Kind is Edf5Kind.Artillery or Edf5Kind.Bomber)
            {
                Effects.SignalSmoke(World, Target, new Color(1f, 0.25f, 0.15f), 2.2f, 4f);
                Effects.Ring(World, Target, S.AttackRadius > 0 ? S.AttackRadius : 10f, new Color(1f, 0.35f, 0.15f), 2.5f);
                Sfx.At("radio", Target, -4f, 1f);
            }
            if (S.Kind == Edf5Kind.Artillery) Sfx.At("whistle", Target + Vector3.Up * 30f, 6f);
            if (S.Kind == Edf5Kind.Gunship) _craft = Aircraft("gunship", 1.4f);
            if (S.Kind == Edf5Kind.Bomber) _craft = Aircraft(S.Family, 1f);
            if (S.Kind == Edf5Kind.AirMissile && Local && Shooter != null) _dot = DotFor(Shooter);
            if (S.Kind == Edf5Kind.Satellite)
            {
                _beamAt = Target;
                if (S.Family == "bulge" && Local && Shooter != null) _dot = DotFor(Shooter);
                Sfx.At("e5_satellite", Target, 0f, 1f);
            }
        }

        private Node3D Aircraft(string family, float scale)
        {
            var n = AirRaidModels.Craft(family);
            n.Scale = Vector3.One * scale;
            World.FxRoot.AddChild(n);
            Sfx.At("e5_flyover", Target + Vector3.Up * BomberAlt, 4f, family == "gunship" ? 0.7f : 1f);
            return n;
        }

        public override void _Process(double delta)
        {
            var dt = (float)delta;
            _t += dt;
            var (delay, gap) = Timing;
            switch (S.Kind)
            {
                case Edf5Kind.Bomber: TickBomber(dt); return;
                case Edf5Kind.Gunship: TickGunship(); break;
            }
            if (S.Kind == Edf5Kind.Satellite && S.Family == "bulge") { TickBulge(dt, delay); return; }
            while (_fired < Count && _t >= delay + _fired * gap) Fire(_fired++);
            // Missiles : ils suivent le point laser tant qu'ils volent ; la frappe s'efface après le dernier impact.
            _missiles.RemoveAll(m => !IsInstanceValid(m));
            if (_fired >= Count && _missiles.Count == 0 && _t > delay + Count * gap + 1f)
            {
                if (_craft != null && IsInstanceValid(_craft) && S.Kind != Edf5Kind.Gunship) _craft.QueueFree();
                if (S.Kind != Edf5Kind.Gunship || _t > delay + Count * gap + 6f) Finish();
            }
        }

        private void Finish()
        {
            if (_craft != null && IsInstanceValid(_craft)) _craft.QueueFree();
            if (_beam != null && IsInstanceValid(_beam)) _beam.QueueFree();
            QueueFree();
        }

        private Vector3 InDisk(float r)
        {
            var a = Rng.Randf() * Mathf.Tau;
            var d = Mathf.Sqrt(Rng.Randf()) * r;
            var p = Target + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            p.Y = World.GroundHeight(p);
            return p;
        }

        /// <summary>Un coup de la frappe (obus d'artillerie, tir de canonnière, missile, trait orbital).</summary>
        private void Fire(int i)
        {
            switch (S.Kind)
            {
                case Edf5Kind.Artillery:
                {
                    var at = InDisk(S.AttackRadius);
                    var from = at + Vector3.Up * 140f + Dir * -40f;
                    var p = Projectile.Spawn(World, ProjectileKind.Shell, from, (at - from).Normalized() * 160f, Local, Damage, Blast);
                    p.Gravity = 0f;
                    p.Life = 3f;
                    if (S.Family is "howitzer" or "gigantic") p.Scale = Vector3.One * (S.Family == "gigantic" ? 6f : 3f);
                    break;
                }
                case Edf5Kind.Gunship:
                {
                    var gun = _craft != null && IsInstanceValid(_craft) ? _craft.GlobalPosition : Target + Vector3.Up * GunshipAlt;
                    var at = S.AttackRadius > 0 && S.Shots > 1 ? InDisk(S.AttackRadius) : Target;
                    var sp = Mathf.Clamp(Speed > 0 ? Speed : 600f, 300f, 900f);
                    var d = (at - gun).Normalized();
                    switch (S.Family)
                    {
                        case "gsauto":
                            Bullets.For(World).Add(new Bullets.Shot
                            {
                                Pos = gun, Vel = d * sp, Life = 2f, MaxLife = 2f, Damage = Damage, Far = 1f, FalloffExp = 1f, Local = Local,
                                Blast = S.AttackRadius >= 13f ? 1.5f : 0f, Color = new Color(1f, 0.75f, 0.35f), Length = 8f, Width = 0.09f,
                            });
                            break;
                        case "gsrocket":
                        {
                            var p = Projectile.Spawn(World, ProjectileKind.Rocket, gun, d * Mathf.Min(sp, 400f), Local, Damage, Mathf.Max(S.AttackRadius, 6f));
                            p.Life = 5f;
                            break;
                        }
                        default:
                        {
                            var bl = S.Family switch
                            {
                                "gsneutral" => 3.5f, "gs105" => 5f, "gsmulti" => Math.Max(S.AttackRadius * 0.6f, 6f),
                                _ => S.Name.StartsWith("180") || S.Name.StartsWith("190") ? 9f : 7f,
                            };
                            var p = Projectile.Spawn(World, ProjectileKind.Shell, gun, d * sp, Local, Damage, bl);
                            p.Gravity = 0f;
                            p.Life = 4f;
                            p.Scale = Vector3.One * (bl > 6f ? 2.2f : 1.4f);
                            break;
                        }
                    }
                    if (_fired % 3 == 0) Sfx.At("e5_gunshipfire", gun, 2f, (float)GD.RandRange(0.92, 1.05), 40);
                    break;
                }
                case Edf5Kind.AirMissile:
                {
                    // Missiles de croisière : départ loin derrière le soldat, en altitude ; ils suivent le point laser.
                    var side = Dir.Cross(Vector3.Up);
                    var from = Target - Dir * 260f + Vector3.Up * 180f + side * (Rng.Randf() - 0.5f) * 80f;
                    var sp = S.Family switch { "lionic" => 150f, "tempest" => 70f, _ => 95f };
                    var p = Projectile.Spawn(World, ProjectileKind.Missile, from, (Target - from).Normalized() * sp, Local, Damage, Mathf.Max(Blast, 6f));
                    p.Life = 25f;
                    if (S.Family == "tempest") p.Scale = Vector3.One * 4f;
                    else if (S.Family == "cruise") p.Scale = Vector3.One * 2f;
                    var turn = S.Homing >= 0 ? Mathf.Lerp(3.5f, 0.8f, S.Homing / 12f) : 2f;
                    p.SetHoming(_dot ?? MarkerAt(Target), turn);
                    _missiles.Add(p);
                    if (_fired == 0) Sfx.At("rocket", from, 6f, S.Family == "tempest" ? 0.6f : 0.85f);
                    break;
                }
                case Edf5Kind.Satellite:
                {
                    // Spritefall : traits orbitaux au hasard dans la zone (un seul, large, en « Shooting Mode »).
                    var single = S.Shots == 1;
                    var at = single ? Target : InDisk(S.AttackRadius);
                    var r = single ? S.AttackRadius : 6f;
                    OrbitalBeam(World, at, r, single ? 0.6f : 0.35f);
                    if (Local) Explode(World, at + Vector3.Up * 0.5f, r, Damage, ExplosionFx.Plasma);
                    break;
                }
            }
        }

        private Node3D _marker;

        /// <summary>Cible fixe des missiles (autres pairs, tireur sans laser).</summary>
        private Node3D MarkerAt(Vector3 at)
        {
            if (_marker != null) return _marker;
            _marker = new Node3D();
            AddChild(_marker);
            _marker.GlobalPosition = at;
            return _marker;
        }

        /// <summary>Canonnière : orbite au-dessus de la cible pendant la frappe.</summary>
        private void TickGunship()
        {
            if (_craft == null || !IsInstanceValid(_craft)) return;
            var a = _t * 0.35f;
            var pos = Target + new Vector3(Mathf.Cos(a) * 110f, GunshipAlt, Mathf.Sin(a) * 110f);
            var tan = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            _craft.GlobalPosition = pos;
            _craft.LookAt(pos + tan, Vector3.Up);
            _craft.Rotation = _craft.Rotation with { Z = 0.35f };
        }

        /// <summary>Bombardier : passe en ligne droite et largue ses bombes (ou mitraille) sur l'axe, en rangées.</summary>
        private void TickBomber(float dt)
        {
            var speed = S.Name.Contains("High-Speed") ? 190f : S.Family is "km6" or "kamuy" ? 140f : 105f;
            const float lead = 2.2f;
            // Longueur du tapis : nombre de bombes × espacement (rafales serrées pour les KM6 sans bombes).
            var spacing = Blast > 0 ? Mathf.Clamp(Blast * 0.8f, 3f, 14f) : 1.6f;
            var length = Mathf.Min(S.Shots * spacing, 320f);
            var start = -length / 2f;
            var planeX = (_t - lead) * speed + start;
            if (_craft != null && IsInstanceValid(_craft))
            {
                var pp = Target + Dir * (planeX) + Vector3.Up * BomberAlt;
                _craft.GlobalPosition = pp;
                _craft.LookAt(pp + Dir, Vector3.Up);
            }
            var rows = Math.Max(1, S.Rows);
            var side = Dir.Cross(Vector3.Up).Normalized();
            // Largage : chaque bombe quand l'avion passe au-dessus de son point.
            while (_fired < Count)
            {
                var k = _fired / rows;
                var row = _fired % rows;
                var along = start + k * (length / Math.Max(1, S.Shots - 1));
                if (planeX < along - 30f) break;
                var spread = S.AttackRadius;
                var lateral = rows > 1 ? Mathf.Lerp(-spread, spread, row / (float)(rows - 1)) * 0.6f : 0f;
                Vector3 at;
                if (S.Name.Contains("Cluster")) at = Target + Dir * along + side * (Rng.Randf() - 0.5f) * spread * 2f;
                else at = Target + Dir * along + side * (lateral + (Rng.Randf() - 0.5f) * 2f);
                at.Y = World.GroundHeight(at);
                Drop(at);
                _fired++;
            }
            if (planeX > length / 2f + 500f) Finish();
        }

        private void Drop(Vector3 at)
        {
            var from = at + Vector3.Up * BomberAlt - Dir * 30f;
            if (Blast <= 0)
            {
                // KM6 : rafales de canons, pas de bombes.
                Bullets.For(World).Add(new Bullets.Shot
                {
                    Pos = from, Vel = (at - from).Normalized() * 700f, Life = 0.4f, MaxLife = 0.4f, Damage = Damage, Far = 1f, FalloffExp = 1f,
                    Local = Local, Color = new Color(1f, 0.75f, 0.35f), Length = 6f, Width = 0.08f, Pierce = true,
                });
                return;
            }
            var p = Projectile.Spawn(World, ProjectileKind.Shell, from, (at - from).Normalized() * 75f, Local, Damage, Blast);
            p.Scale = Vector3.One * 1.6f;
            p.Gravity = 0f;
            p.Life = 4f;
            if (S.Duration > 0)
            {
                // Vesta : bombes incendiaires (feu qui brûle la durée d'EDF 5 ; dégâts totaux = dégâts de la bombe).
                var burn = S.Duration;
                var dmg = Damage;
                var r = Blast;
                var w = World;
                var local = Local;
                p.CustomImpact = (pos, n) =>
                {
                    Effects.Explosion(w, pos, r * 0.6f, ExplosionFx.Explosive);
                    FireField.Spawn(w, pos, n, r, burn, dmg, local, false);
                };
            }
        }

        /// <summary>Bulge Laser : rayon continu du satellite, qui suit le laser du tireur à vitesse limitée.</summary>
        private void TickBulge(float dt, float delay)
        {
            var duration = S.Shots / 60f;
            if (_t < delay) return;
            if (_t > delay + duration) { Finish(); return; }
            var goal = _dot != null && IsInstanceValid(_dot) ? _dot.GlobalPosition : Target;
            _beamAt = _beamAt.MoveToward(goal, 25f * dt);
            _beamAt.Y = Mathf.Max(World.GroundHeight(_beamAt), goal.Y);
            if (_beam == null)
            {
                _beam = OrbitalColumn(S.AttackRadius);
                World.FxRoot.AddChild(_beam);
            }
            _beam.GlobalPosition = _beamAt;
            if (!Local) return;
            // Dégâts : 60 coups par seconde dans le rayon de la frappe.
            var ticks = dt * 60f;
            Dictionary<Enemy, (float, Vector3, bool)> per = null;
            foreach (var e in World.EnemiesById.Values)
            {
                if (e.State == EnemyState.Dead) continue;
                var c = e.Center;
                if (new Vector2(c.X - _beamAt.X, c.Z - _beamAt.Z).Length() > S.AttackRadius + e.Def.Size * 1.1f) continue;
                per ??= new();
                per[e] = (Damage * ticks, c, false);
            }
            if (per != null) FencerFire.Apply(World, per);
        }
    }

    /// <summary>Colonne d'énergie orbitale (Bulge Laser) : cylindre lumineux du ciel au sol.</summary>
    public static Node3D OrbitalColumn(float radius)
    {
        var n = new Node3D();
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.55f, 0.35f, 0.75f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        n.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 0.5f, BottomRadius = radius, Height = 400f, RadialSegments = 16 },
            MaterialOverride = mat, Position = new Vector3(0, 200f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        n.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 0.2f, BottomRadius = radius * 0.4f, Height = 400f, RadialSegments = 12 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.9f, 0.8f), 5f), Position = new Vector3(0, 200f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        n.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.3f), LightEnergy = 4f, OmniRange = radius * 6f, Position = Vector3.Up * 2f });
        return n;
    }

    /// <summary>Trait orbital bref (Spritefall) : colonne qui s'éteint.</summary>
    public static void OrbitalBeam(World w, Vector3 at, float radius, float life)
    {
        var n = OrbitalColumn(Mathf.Max(radius * 0.5f, 1.5f));
        w.FxRoot.AddChild(n);
        n.GlobalPosition = at;
        var tw = n.CreateTween();
        tw.TweenProperty(n, "scale", new Vector3(0.05f, 1f, 0.05f), life).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(n.QueueFree));
    }
}
