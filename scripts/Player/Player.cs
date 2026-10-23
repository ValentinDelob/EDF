using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Soldat contrôlé par le joueur local : déplacements, caméra, armes et capacités de classe.</summary>
public partial class Player : CharacterBody3D
{
    public World World;
    public ClassDef Def;
    public SoldierModel Model;
    public readonly List<WeaponState> Weapons = new();
    public int Current;
    public float Health;
    public float MaxHealth;
    public bool Downed;
    public float Energy = 100f;      // Commando aéroporté
    public float Heat;               // Grenadier blindé
    public bool Overheated;
    public float CallCredits;        // Opérateur d'appui
    public float AbilityCooldown;
    public float AbilityMaxCooldown = 1f;
    public float GrenadeCooldown;
    public float RallyTime;
    public float AnchorTime;
    public float DamageFlash;
    public bool Aiming;
    public bool Flying;
    public int Kills;
    public float HitMarker;
    public float ReviveProgress;
    public long ReviveTarget;
    public bool SecondChanceUsed;
    public float DownedTime;
    public Tank Vehicle;
    /// <summary>Mode automatique pour les tests : vise et tire seul sur l'ennemi le plus proche.</summary>
    public static bool Bot;
    private bool _botFire;

    private Node3D _camYaw, _camPitch;
    private SpringArm3D _arm;
    public Camera3D Camera { get; private set; }
    private float _yaw, _pitch;
    private float _modelYaw;
    private float _recoil;
    private float _shake;
    private float _dodgeTime;
    private Vector3 _dodgeDir;
    private float _dashTime;
    private Vector3 _dashDir;
    private bool _diving;
    private float _lastFireTime = -10f;
    private float _netTimer;
    private float _beamSoundT;
    private float _jetSoundT;
    private float _stepT;

    private const float Gravity = 24f;

    public float CallCost => 300f;
    public WeaponState Weapon => Weapons.Count > 0 ? Weapons[Mathf.Clamp(Current, 0, Weapons.Count - 1)] : null;

    public static Player Create(World world, SoldierClass cls, List<WeaponInstance> loadout, float maxArmor)
    {
        var p = new Player { World = world, Def = Classes.Get(cls), Name = "LocalPlayer", CollisionLayer = 1 << 1, CollisionMask = 1 };
        p.MaxHealth = p.Health = maxArmor;
        foreach (var w in loadout) p.Weapons.Add(new WeaponState(w));
        p.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = cls == SoldierClass.Heavy ? 0.5f : 0.38f, Height = cls == SoldierClass.Heavy ? 2.1f : 1.8f },
            Position = new Vector3(0, cls == SoldierClass.Heavy ? 1.05f : 0.9f, 0),
        });
        p.Model = SoldierModel.Create(cls);
        p.AddChild(p.Model);

        p._camYaw = new Node3D { Position = new Vector3(0, cls == SoldierClass.Heavy ? 1.9f : 1.65f, 0) };
        p.AddChild(p._camYaw);
        p._camPitch = new Node3D();
        p._camYaw.AddChild(p._camPitch);
        p._arm = new SpringArm3D { SpringLength = cls == SoldierClass.Heavy ? 5f : 4.2f, CollisionMask = 1, Margin = 0.3f, Position = new Vector3(0.85f, 0.15f, 0) };
        p._arm.AddExcludedObject(p.GetRid());
        p._camPitch.AddChild(p._arm);
        p.Camera = new Camera3D { Current = true, Fov = Settings.I.Fov, Far = 3000f, Near = 0.08f };
        p._arm.AddChild(p.Camera);
        p.AbilityMaxCooldown = cls switch
        {
            SoldierClass.Rifleman => 60f,
            SoldierClass.Airborne => 12f,
            SoldierClass.Support => 3f,
            _ => 30f,
        };
        return p;
    }

    public void SetYaw(float yaw)
    {
        _yaw = yaw;
        _modelYaw = yaw + Mathf.Pi;
    }

    public Vector3 AimForward => -_camPitch.GlobalBasis.Z;

    public override void _UnhandledInput(InputEvent e)
    {
        if (World.Paused || World.Ended) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            var sens = 0.0022f * Settings.I.MouseSensitivity * (Aiming ? Settings.I.AimSensitivity : 1f);
            _yaw -= mm.Relative.X * sens;
            _pitch -= mm.Relative.Y * sens * (Settings.I.InvertY ? -1 : 1);
            _pitch = Mathf.Clamp(_pitch, -1.35f, 1.2f);
        }
        if (Downed) return;
        if (e.IsActionPressed("interact"))
        {
            if (Vehicle != null) { Net.I.RequestTankSeat(Vehicle.Id, false); return; }
            var t = World.TankNear(GlobalPosition, 6f);
            if (t != null && World.DownedTeammateNear(GlobalPosition, 3.5f) == 0) { Net.I.RequestTankSeat(t.Id, true); return; }
        }
        if (Vehicle != null) return;
        if (e.IsActionPressed("weapon_1")) Switch(0);
        else if (e.IsActionPressed("weapon_2")) Switch(1);
        else if (e.IsActionPressed("weapon_3")) Switch(2);
        else if (e.IsActionPressed("weapon_swap")) Switch((Current + 1) % Weapons.Count);
        else if (e.IsActionPressed("reload")) Reload();
        else if (e.IsActionPressed("ability")) UseAbility();
        else if (e.IsActionPressed("grenade")) ThrowGrenade();
        else if (e is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            Switch((Current + (mb.ButtonIndex == MouseButton.WheelUp ? Weapons.Count - 1 : 1)) % Weapons.Count);
    }

    private void Switch(int i)
    {
        if (i < 0 || i >= Weapons.Count || i == Current) return;
        Current = i;
        Weapon.Cooldown = Mathf.Max(Weapon.Cooldown, 0.25f);
        Sfx.Ui("reload", -8f, 1.3f);
    }

    private void Reload()
    {
        var w = Weapon;
        if (w == null || w.Reloading) return;
        if (Def.Id == SoldierClass.Airborne && w.W.T.EnergyCost > 0)
        {
            if (Overheated || Energy < w.W.T.EnergyCost * 0.5f) return;
            Energy -= w.W.T.EnergyCost;
        }
        var before = w.Reloading;
        w.StartReload();
        if (!before && w.Reloading) Sfx.Ui("reload", -4f);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (World.Ended) { Velocity = Vector3.Zero; return; }
        if (Vehicle != null)
        {
            Vehicle.DriveTick(this, dt, _yaw, _pitch);
            GlobalPosition = Vehicle.SeatPosition;
            Velocity = Vector3.Zero;
            _netTimer -= dt;
            if (_netTimer <= 0)
            {
                _netTimer = 0.05f;
                Net.I.SendPlayerState(GlobalPosition, _modelYaw, _pitch, 128, Health, Current, Vector3.Zero);
            }
            return;
        }

        var input = Vector2.Zero;
        if (!World.Paused && !Downed)
            input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var camBasis = new Basis(Vector3.Up, _yaw);
        var wish = (camBasis * new Vector3(input.X, 0, input.Y));
        if (wish.LengthSquared() > 1) wish = wish.Normalized();
        var onFloor = IsOnFloor();
        var v = Velocity;
        var sprintHeld = !World.Paused && Input.IsActionPressed("sprint") && !Downed;
        var jumpHeld = !World.Paused && Input.IsActionPressed("jump") && !Downed;
        var jumpPressed = !World.Paused && Input.IsActionJustPressed("jump") && !Downed;
        var sprintPressed = !World.Paused && Input.IsActionJustPressed("sprint") && !Downed;
        var dodgePressed = !World.Paused && Input.IsActionJustPressed("dodge") && !Downed;
        Flying = false;

        var speed = Def.WalkSpeed;
        if (AnchorTime > 0) speed *= 0.35f;

        switch (Def.Id)
        {
            case SoldierClass.Rifleman:
            case SoldierClass.Support:
                if (sprintHeld && !Aiming && input.Y < 0.1f) speed = Def.SprintSpeed;
                if (dodgePressed && onFloor && _dodgeTime <= -0.3f)
                {
                    _dodgeTime = 0.45f;
                    _dodgeDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : -camBasis.Z;
                    Sfx.Ui("dash", -10f, 1.4f);
                }
                if (jumpPressed && onFloor) v.Y = 7.5f;
                break;
            case SoldierClass.Airborne:
                if (!Overheated && jumpHeld && Energy > 0)
                {
                    Flying = true;
                    v.Y = Mathf.MoveToward(v.Y, 10f, 45f * dt);
                    Energy -= 26f * dt;
                    speed = 11f;
                    _jetSoundT -= dt;
                    if (_jetSoundT <= 0) { _jetSoundT = 0.25f; Sfx.At("jet", GlobalPosition, -6f); }
                }
                else if (!onFloor) speed = 10f;
                if (!Overheated && (sprintPressed || dodgePressed) && Energy > 15 && wish.LengthSquared() > 0.01f)
                {
                    _dashTime = 0.35f;
                    _dashDir = wish.Normalized();
                    Energy -= 15;
                    Sfx.Ui("dash", -6f, 1.3f);
                }
                if (Energy <= 0 && !Overheated) { Overheated = true; Energy = 0; Sfx.Ui("empty", 0, 0.6f); }
                var regen = Flying ? 0 : (onFloor ? 34f : 16f);
                if (Overheated) regen = 25f;
                Energy = Mathf.Min(100f, Energy + regen * dt);
                if (Overheated && Energy >= 100f) Overheated = false;
                break;
            case SoldierClass.Heavy:
                if (!Overheated && (sprintPressed || dodgePressed) && _dashTime <= 0)
                {
                    _dashTime = 0.28f;
                    _dashDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : -camBasis.Z;
                    Heat += 24f;
                    Sfx.At("dash", GlobalPosition, 0f, 0.8f);
                }
                if (!Overheated && jumpPressed && onFloor)
                {
                    v.Y = 13f;
                    Heat += 18f;
                    Sfx.At("dash", GlobalPosition, -2f, 0.7f);
                }
                if (Heat >= 100f && !Overheated) { Overheated = true; Sfx.Ui("empty", 0, 0.5f); }
                Heat = Mathf.Max(0, Heat - (Overheated ? 22f : 30f) * dt);
                if (Overheated && Heat <= 0) Overheated = false;
                break;
        }

        // Vitesse horizontale
        var horiz = new Vector3(v.X, 0, v.Z);
        var target = wish * speed;
        var accel = onFloor ? 14f : 5f;
        if (Flying) accel = 6f;
        horiz = horiz.Lerp(target, Mathf.Clamp(accel * dt, 0, 1));
        if (_dodgeTime > 0) horiz = _dodgeDir * 12f;
        if (_dashTime > 0) horiz = _dashDir * (Def.Id == SoldierClass.Heavy ? 19f : 22f);
        _dodgeTime -= dt;
        _dashTime -= dt;

        if (!onFloor && !Flying) v.Y -= Gravity * dt * (_diving ? 2.5f : 1f);
        if (_diving) v.Y = Mathf.Min(v.Y, -30f);
        v.X = horiz.X;
        v.Z = horiz.Z;
        Velocity = v;
        var wasAir = !onFloor;
        MoveAndSlide();
        if (wasAir && IsOnFloor())
        {
            if (_diving)
            {
                _diving = false;
                var dmg = 150f * (1f + 0.05f * (Weapons.Count > 0 ? Weapons.Max(w => w.W.Level) : 0)) * DamageMul;
                Net.I.SendExplosion(GlobalPosition + Vector3.Up * 0.5f, 9f, dmg, 2);
            }
            else if (v.Y < -12f) Sfx.At("hammer", GlobalPosition, -10f, 1.6f);
        }
        if (onFloor && horiz.Length() > 2f)
        {
            _stepT -= dt * horiz.Length() / 3.5f;
            if (_stepT <= 0) { _stepT = 0.5f; Sfx.At("hit", GlobalPosition, -26f, 0.45f, 0); }
        }

        // Garde-fou : retour sur la carte si chute.
        if (GlobalPosition.Y < -20f) GlobalPosition = new Vector3(0, 2, 0);

        // Envoi de l'état aux autres pairs (20 Hz).
        _netTimer -= dt;
        if (_netTimer <= 0)
        {
            _netTimer = 0.05f;
            var flags = (horiz.Length() > 0.5f ? 1 : 0) | (speed > Def.WalkSpeed + 0.1f ? 2 : 0) | (!IsOnFloor() ? 4 : 0) | (Aiming ? 8 : 0)
                        | (Time.GetTicksMsec() / 1000f - _lastFireTime < 0.3f ? 16 : 0) | (Downed ? 32 : 0) | (Flying ? 64 : 0);
            Net.I.SendPlayerState(GlobalPosition, _modelYaw, _pitch, flags, Health, Current, Velocity);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _camYaw.Rotation = new Vector3(0, _yaw, 0);
        _camPitch.Rotation = new Vector3(_pitch, 0, 0);

        foreach (var w in Weapons) w.Tick(dt);
        AbilityCooldown = Mathf.Max(0, AbilityCooldown - dt);
        GrenadeCooldown = Mathf.Max(0, GrenadeCooldown - dt);
        RallyTime = Mathf.Max(0, RallyTime - dt);
        AnchorTime = Mathf.Max(0, AnchorTime - dt);
        DamageFlash = Mathf.Max(0, DamageFlash - dt * 2f);
        HitMarker = Mathf.Max(0, HitMarker - dt * 5f);
        _recoil = Mathf.MoveToward(_recoil, 0, dt * 6f);

        var canAct = !World.Paused && !World.Ended && !Downed;
        Aiming = canAct && Input.IsActionPressed("aim");

        // Caméra : visée, tremblement
        var zoom = Aiming ? (Weapon != null && Weapon.W.T.Zoom > 1 ? Weapon.W.T.Zoom : 1.4f) : 1f;
        Camera.Fov = Mathf.Lerp(Camera.Fov, Settings.I.Fov / zoom, Mathf.Clamp(dt * 12f, 0, 1));
        _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, Aiming ? 2.4f : (Def.Id == SoldierClass.Heavy ? 5f : 4.2f), Mathf.Clamp(dt * 10f, 0, 1));
        _shake = Mathf.MoveToward(_shake, 0, dt * 2.5f);
        Camera.HOffset = (float)GD.RandRange(-1, 1) * _shake * 0.25f;
        Camera.VOffset = (float)GD.RandRange(-1, 1) * _shake * 0.25f + _recoil * 0.02f;

        // Orientation du modèle
        var horiz = new Vector3(Velocity.X, 0, Velocity.Z);
        var firing = Time.GetTicksMsec() / 1000f - _lastFireTime < 0.8f;
        float targetYaw;
        if (Aiming || firing || Flying) targetYaw = _yaw + Mathf.Pi;
        else if (horiz.LengthSquared() > 0.5f) targetYaw = Mathf.Atan2(horiz.X, horiz.Z);
        else targetYaw = _modelYaw;
        _modelYaw = Mathf.LerpAngle(_modelYaw, targetYaw, Mathf.Clamp(dt * 12f, 0, 1));
        Model.Rotation = new Vector3(0, _modelYaw, 0);
        Model.Animate(dt, horiz.Length(), _pitch, !IsOnFloor(), Flying, Downed, Aiming, _recoil);

        Model.Visible = Vehicle == null;
        if (Vehicle != null)
        {
            _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, Aiming ? 7f : 11f, Mathf.Clamp(dt * 6f, 0, 1));
            if (Bot && canAct)
            {
                BotAim(dt);
                if (_botFire) Input.ActionPress("fire"); else Input.ActionRelease("fire");
            }
            HandleRevive(dt);
            return;
        }
        if (Bot && canAct) BotAim(dt);
        if (canAct && Weapon != null) HandleFire(dt);
        else _beamActive = false;
        if (Downed) HandleDowned(dt);
        else HandleRevive(dt);
    }

    // ------------------------------------------------------------------ Tir

    private bool _beamActive;

    public float DamageMul => (RallyTime > 0 ? 1.3f : 1f) * Profile.I.DamageBonus(Def.Id);

    private void HandleFire(float dt)
    {
        var ws = Weapon;
        var t = ws.W.T;
        var held = Input.IsActionPressed("fire") || _botFire;
        var pressed = Input.IsActionJustPressed("fire") || (_botFire && ws.Cooldown <= 0);
        _beamActive = false;
        if (ws.Reloading) return;

        if (t.Kind == WeaponKind.Beam)
        {
            if (held && ws.BeamCharge > 0)
            {
                _beamActive = true;
                ws.BeamCharge -= dt / 4f; // 4 s de tir continu
                FireBeam(ws, dt);
                if (ws.BeamCharge <= 0) { ws.BeamCharge = 0; Reload(); }
            }
            return;
        }

        if (ws.Cooldown > 0) return;
        if (!(t.Automatic ? held : pressed)) return;
        if (ws.Ammo <= 0)
        {
            if (pressed) Sfx.Ui("empty");
            Reload();
            return;
        }
        ws.Cooldown = 1f / Mathf.Max(ws.W.FireRate, 0.05f);
        ws.Ammo--;
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        switch (t.Kind)
        {
            case WeaponKind.Hitscan: FireHitscan(ws); break;
            case WeaponKind.Projectile: FireProjectile(ws); break;
            case WeaponKind.Melee: FireMelee(ws); break;
            case WeaponKind.Airstrike: FireAirstrike(ws); break;
            case WeaponKind.Turret: Deploy(ws, 0); break;
            case WeaponKind.HealField: Deploy(ws, 1); break;
        }
        if (ws.Ammo <= 0) Reload();
    }

    /// <summary>Point visé : rayon depuis la caméra au centre de l'écran.</summary>
    public Vector3 AimPoint(float range, out GodotObject collider)
    {
        var from = Camera.GlobalPosition;
        var dir = -Camera.GlobalBasis.Z;
        var to = from + dir * range;
        var q = PhysicsRayQueryParameters3D.Create(from, to, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        collider = null;
        if (hit.Count == 0) return to;
        collider = hit["collider"].AsGodotObject();
        return (Vector3)hit["position"];
    }

    private Vector3 MuzzlePos => Model.Muzzle.GlobalPosition;

    private void FireHitscan(WeaponState ws)
    {
        var t = ws.W.T;
        var aim = AimPoint(ws.W.Range, out _);
        var muzzle = MuzzlePos;
        var baseDir = (aim - muzzle).Normalized();
        var spreadRad = Mathf.DegToRad(ws.W.Spread * (Aiming ? 0.55f : 1f));
        var perEnemy = new Dictionary<Enemy, (float dmg, Vector3 at, bool weak)>();
        var space = GetWorld3D().DirectSpaceState;
        var rng = World.Rng;
        Vector3 lastEnd = aim;
        for (var i = 0; i < t.Pellets; i++)
        {
            var dir = Spread(baseDir, spreadRad, rng);
            var end = muzzle + dir * ws.W.Range;
            var q = PhysicsRayQueryParameters3D.Create(muzzle, end, 1u | (1u << 2));
            q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            var hit = space.IntersectRay(q);
            if (hit.Count > 0)
            {
                end = (Vector3)hit["position"];
                var col = hit["collider"].AsGodotObject();
                var shape = hit["shape"].AsInt32();
                if (col is Enemy e && e.State != EnemyState.Dead)
                {
                    var weak = shape == 1;
                    var falloff = Mathf.Clamp(1.2f - muzzle.DistanceTo(end) / ws.W.Range, 0.5f, 1f);
                    var dmg = ws.W.Damage * falloff * (weak ? 1.5f : 1f) * DamageMul;
                    perEnemy.TryGetValue(e, out var acc);
                    perEnemy[e] = (acc.dmg + dmg, end, acc.weak || weak);
                }
                else
                {
                    Effects.Sparks(World, end, (Vector3)hit["normal"]);
                    if (col is Building b && !b.Destroyed) Net.I.SendBuildingDamage(b.Id, ws.W.Damage * 0.06f);
                }
            }
            if (i < 3 || i % 3 == 0)
            {
                var c = t.Class == SoldierClass.Airborne ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.75f, 0.35f);
                Effects.Tracer(World.FxRoot, muzzle, end, c, t.Category.Contains("précision") || t.Category.Contains("particules") ? 0.09f : 0.045f);
            }
            lastEnd = end;
        }
        foreach (var (e, v) in perEnemy)
        {
            Net.I.SendDamageEnemy(e.Id, v.dmg, v.at, v.weak);
            World.LocalHitFx(e, v.at, true, v.dmg, v.weak);
            HitMarker = 1f;
            if (Def.Id == SoldierClass.Support) CallCredits = Mathf.Min(CallCost * 2, CallCredits + v.dmg * 0.08f);
        }
        var sound = t.Category switch
        {
            "Fusil à pompe" => "shotgun",
            "Fusil de précision" => "sniper",
            "Mitrailleuse rotative" => "minigun",
            "Arme de défense" => "smg",
            "Lance électrique" => "zap",
            "Fusil à particules" => "laser",
            _ => "rifle",
        };
        Sfx.Ui(sound, sound == "sniper" ? 2f : -3f);
        Effects.MuzzleFlash(World.FxRoot, muzzle, t.Class == SoldierClass.Airborne ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.7f, 0.3f), t.Pellets > 1 ? 0.7f : 0.45f);
        _recoil = Mathf.Min(1.5f, _recoil + (t.Category == "Fusil de précision" ? 1.2f : t.Pellets > 1 ? 1f : 0.25f));
        _pitch += Mathf.DegToRad(t.Category == "Fusil de précision" ? 1.5f : t.Pellets > 1 ? 1.2f : 0.25f) * (AnchorTime > 0 ? 0.2f : 1f);
        Net.I.SendShotFx(t.Class == SoldierClass.Airborne ? 1 : 0, muzzle, lastEnd);
    }

    private static Vector3 Spread(Vector3 dir, float rad, RandomNumberGenerator rng)
    {
        if (rad <= 0) return dir;
        var up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        var right = dir.Cross(up).Normalized();
        var up2 = right.Cross(dir).Normalized();
        var r = rad * Mathf.Sqrt(rng.Randf());
        var a = rng.Randf() * Mathf.Tau;
        return (dir + right * Mathf.Cos(a) * Mathf.Tan(r) + up2 * Mathf.Sin(a) * Mathf.Tan(r)).Normalized();
    }

    private void FireBeam(WeaponState ws, float dt)
    {
        var muzzle = MuzzlePos;
        var aim = AimPoint(ws.W.Range, out var col);
        var end = aim;
        var dir = (aim - muzzle).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(muzzle, muzzle + dir * ws.W.Range, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count > 0)
        {
            end = (Vector3)hit["position"];
            col = hit["collider"].AsGodotObject();
        }
        _beamAccum += dt;
        if (col is Enemy e && e.State != EnemyState.Dead)
        {
            _beamDamage += ws.W.Damage * dt * DamageMul;
            if (_beamAccum >= 0.1f)
            {
                Net.I.SendDamageEnemy(e.Id, _beamDamage, end, false);
                World.LocalHitFx(e, end, true, _beamDamage);
                HitMarker = 1f;
                _beamDamage = 0;
                _beamAccum = 0;
            }
        }
        else if (_beamAccum >= 0.1f)
        {
            if (col is Building b && !b.Destroyed) Net.I.SendBuildingDamage(b.Id, ws.W.Damage * 0.1f * 0.3f);
            Effects.Sparks(World, end, -dir);
            _beamAccum = 0;
            _beamDamage = 0;
        }
        Effects.Beam(World, muzzle, end, new Color(0.45f, 0.8f, 1f), 0.08f);
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        _beamSoundT -= dt;
        if (_beamSoundT <= 0)
        {
            _beamSoundT = 0.2f;
            Sfx.Ui("laser", -6f);
            Net.I.SendShotFx(2, muzzle, end);
        }
    }

    private float _beamAccum, _beamDamage;

    private void FireProjectile(WeaponState ws)
    {
        var t = ws.W.T;
        var aim = AimPoint(ws.W.Range, out var col);
        var muzzle = MuzzlePos;
        var dir = Spread((aim - muzzle).Normalized(), Mathf.DegToRad(ws.W.Spread), World.Rng);
        var kind = t.Id switch
        {
            "pl7" => ProjectileKind.Plasma,
            "gl6" => ProjectileKind.Grenade,
            "ms6" => ProjectileKind.Missile,
            "cs90" => ProjectileKind.Shell,
            _ => ProjectileKind.Rocket,
        };
        var vel = dir * t.ProjectileSpeed;
        if (kind == ProjectileKind.Grenade) vel += Vector3.Up * 6f;
        var p = Projectile.Spawn(World, kind, muzzle, vel, true, ws.W.Damage * DamageMul, ws.W.BlastRadius);
        if (kind == ProjectileKind.Missile)
        {
            var target = World.EnemyNearRay(Camera.GlobalPosition, -Camera.GlobalBasis.Z, 0.25f, ws.W.Range);
            if (target != null) p.SetHoming(target);
        }
        Net.I.SendShotFx(10 + (int)kind, muzzle, muzzle + vel);
        Sfx.Ui(kind == ProjectileKind.Plasma ? "plasma" : kind == ProjectileKind.Grenade ? "launcher" : "rocket", -2f);
        Effects.MuzzleFlash(World.FxRoot, muzzle, kind == ProjectileKind.Plasma ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.6f, 0.2f), 0.8f);
        _recoil = Mathf.Min(1.5f, _recoil + 0.9f);
        _pitch += Mathf.DegToRad(kind == ProjectileKind.Shell ? 2.5f : 0.8f) * (AnchorTime > 0 ? 0.2f : 1f);
    }

    private void FireMelee(WeaponState ws)
    {
        var fwd = -Camera.GlobalBasis.Z;
        fwd.Y = 0;
        fwd = fwd.Normalized();
        var at = GlobalPosition + Vector3.Up * 1.2f + fwd * 3.5f;
        Net.I.SendExplosion(at, ws.W.BlastRadius, ws.W.Damage * DamageMul, 3);
        Sfx.Ui("hammer", 0f);
        _recoil = 1.5f;
        Effects.Ring(World, at - Vector3.Up * 1.1f, ws.W.BlastRadius, new Color(0.5f, 0.8f, 1f), 0.25f);
        Net.I.SendShotFx(7, GlobalPosition, at);
        // Élan vers l'avant
        Velocity += fwd * 8f;
    }

    private void FireAirstrike(WeaponState ws)
    {
        var aim = AimPoint(ws.W.Range, out _);
        var dir = aim - GlobalPosition;
        dir.Y = 0;
        dir = dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector3.Forward;
        Net.I.SendAirstrike(aim, dir, ws.W.Damage * DamageMul, ws.W.T.Pellets, ws.W.BlastRadius);
        Sfx.Ui("radio", 0f);
        World.Hud?.Toast("Frappe demandée : impact dans 3 secondes !");
    }

    private void Deploy(WeaponState ws, int kind)
    {
        var fwd = -Camera.GlobalBasis.Z;
        fwd.Y = 0;
        var pos = GlobalPosition + fwd.Normalized() * 2.5f;
        pos.Y = 0;
        if (kind == 0) Net.I.SendDeploy(0, pos, _yaw, ws.W.Damage, ws.W.Range, 30f);
        else Net.I.SendDeploy(1, pos, 0, ws.W.Damage, ws.W.BlastRadius, 15f);
        Sfx.Ui(kind == 0 ? "reload" : "heal", 0f);
    }

    private void ThrowGrenade()
    {
        if (GrenadeCooldown > 0 || Downed) return;
        GrenadeCooldown = 9f;
        var dir = -Camera.GlobalBasis.Z;
        var from = GlobalPosition + Vector3.Up * 1.7f + dir * 0.8f;
        var vel = dir * 22f + Vector3.Up * 6f;
        var lvl = Weapons.Count > 0 ? Weapons.Max(w => w.W.Level) : 0;
        Projectile.Spawn(World, ProjectileKind.Grenade, from, vel, true, 90f * (1 + 0.05f * lvl) * DamageMul, 6f);
        Net.I.SendShotFx(10 + (int)ProjectileKind.Grenade, from, from + vel);
        Sfx.Ui("launcher", -6f, 0.8f);
    }

    // ------------------------------------------------------------------ Capacités

    private void UseAbility()
    {
        if (Downed) return;
        switch (Def.Id)
        {
            case SoldierClass.Rifleman:
                if (AbilityCooldown > 0) return;
                AbilityCooldown = AbilityMaxCooldown;
                Net.I.SendRally(GlobalPosition);
                break;
            case SoldierClass.Airborne:
                if (AbilityCooldown > 0 || IsOnFloor()) return;
                AbilityCooldown = AbilityMaxCooldown;
                _diving = true;
                Sfx.Ui("dash", 0f, 0.7f);
                break;
            case SoldierClass.Support:
                if (CallCredits < CallCost) { Sfx.Ui("empty"); return; }
                CallCredits -= CallCost;
                var aim = AimPoint(400, out _);
                var dir = aim - GlobalPosition;
                dir.Y = 0;
                var lvl = Weapons.Count > 0 ? Weapons.Max(w => w.W.Level) : 0;
                Net.I.SendAirstrike(aim, dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector3.Forward, 260f * (1 + 0.05f * lvl), 10, 11f);
                Sfx.Ui("radio");
                World.Hud?.Toast("Barrage d'artillerie en approche !");
                break;
            case SoldierClass.Heavy:
                if (AbilityCooldown > 0) return;
                AbilityCooldown = AbilityMaxCooldown;
                AnchorTime = 8f;
                Sfx.Ui("hammer", -4f, 0.6f);
                Effects.Ring(World, GlobalPosition, 2.5f, new Color(1f, 0.6f, 0.2f), 0.5f);
                break;
        }
    }

    public float AbilityReady => Def.Id == SoldierClass.Support
        ? Mathf.Clamp(CallCredits / CallCost, 0, 1)
        : 1f - AbilityCooldown / Mathf.Max(AbilityMaxCooldown, 0.01f);

    // ------------------------------------------------------------------ Santé

    public void EnterVehicle(Tank t)
    {
        Vehicle = t;
        CollisionLayer = 0;
        World.Hud?.Toast("Char de combat : clic gauche canon, clic droit mitrailleuse, E pour descendre.");
    }

    public void ExitVehicle()
    {
        if (Vehicle == null) return;
        var t = Vehicle;
        Vehicle = null;
        CollisionLayer = 1 << 1;
        GlobalPosition = t.GlobalPosition + t.GlobalBasis.X * 3.5f + Vector3.Up * 0.5f;
    }

    public void TakeDamage(float dmg, Vector3 from)
    {
        if (Downed || World.Ended) return;
        if (Vehicle != null)
        {
            // Le blindage encaisse à la place du soldat.
            Vehicle.Health -= dmg * 0.6f;
            AddShake(0.1f);
            if (Vehicle.Health <= 0) Net.I.SendTankWrecked(Vehicle.Id);
            return;
        }
        if (_dodgeTime > 0) return; // invulnérable pendant la roulade
        if (AnchorTime > 0) dmg *= 0.5f;
        Health -= dmg;
        DamageFlash = Mathf.Min(1f, DamageFlash + 0.35f + dmg / MaxHealth);
        AddShake(Mathf.Clamp(dmg / MaxHealth * 4f, 0.1f, 0.6f));
        Sfx.Ui("hurt", -4f, (float)GD.RandRange(0.9, 1.1), 120);
        World.Hud?.DamageFrom(from);
        if (Health <= 0)
        {
            Health = 0;
            Downed = true;
            DownedTime = 0;
            Net.I.SendPlayerDowned(true);
        }
    }

    public void Heal(float amount)
    {
        if (Downed) return;
        Health = Mathf.Min(MaxHealth, Health + amount);
    }

    public void Revived()
    {
        Downed = false;
        Health = MaxHealth * 0.3f;
        Net.I.SendPlayerDowned(false);
    }

    private void HandleDowned(float dt)
    {
        DownedTime += dt;
        // En solo : une seconde chance par mission (un équipier PNJ vous relève).
        if (Net.I.IsSolo && !SecondChanceUsed && DownedTime > 5f)
        {
            SecondChanceUsed = true;
            World.Hud?.Toast("Un infirmier vous relève : seconde chance utilisée !");
            Revived();
        }
    }

    private void HandleRevive(float dt)
    {
        var target = World.DownedTeammateNear(GlobalPosition, 3.5f);
        if (target != 0 && Input.IsActionPressed("interact") && !World.Paused)
        {
            if (ReviveTarget != target) ReviveProgress = 0;
            ReviveTarget = target;
            ReviveProgress += dt / (Def.Id == SoldierClass.Rifleman ? 1.5f : 3f);
            if (ReviveProgress >= 1)
            {
                Net.I.SendRevive(target);
                ReviveProgress = 0;
            }
        }
        else
        {
            ReviveProgress = 0;
            ReviveTarget = target;
        }
    }

    private void BotAim(float dt)
    {
        var e = World.NearestEnemy(GlobalPosition, 140f);
        _botFire = false;
        if (e == null) return;
        var to = e.Center - _camYaw.GlobalPosition;
        var yaw = Mathf.Atan2(-to.X, -to.Z);
        var pitch = Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
        _yaw = Mathf.LerpAngle(_yaw, yaw, Mathf.Clamp(dt * 5f, 0, 1));
        _pitch = Mathf.Lerp(_pitch, pitch, Mathf.Clamp(dt * 5f, 0, 1));
        _botFire = Mathf.Abs(Mathf.AngleDifference(_yaw, yaw)) < 0.15f;
        if (Weapon.Ammo == 0 && !Weapon.Reloading) Reload();
        // Le bot du Commando aéroporté vole par intermittence (teste vol, énergie et surchauffe).
        if (Def.Id == SoldierClass.Airborne)
        {
            if (!Overheated && Energy > 60) Input.ActionPress("jump");
            else if (Energy < 25 || Overheated) Input.ActionRelease("jump");
        }
    }

    public void AddShake(float amount) => _shake = Mathf.Min(1.2f, _shake + amount);
    public void ApplyRally() => RallyTime = 15f;
}
