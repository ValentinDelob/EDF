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
    /// <summary>Opérateur d'appui : véhicule choisi pour l'appel (<see cref="CallIns"/>), changé par un appui bref sur la touche d'appel.</summary>
    public int CallChoice;
    /// <summary>Identifiant du dernier véhicule appelé par ce soldat (0 = aucun).</summary>
    public int CalledVehicleId;
    private float _callHeld = -1f;
    /// <summary>Grenadier blindé : blocage restant du changement de jeu d'armes (0,5 s), arme abaissée, aucun tir.</summary>
    public float SetSwapLeft;
    public const float SetSwapTime = 0.5f;
    /// <summary>Arme tenue dans chaque jeu (0 ou 1), retrouvée au retour sur ce jeu.</summary>
    private readonly int[] _setPick = new int[2];
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
    public Vehicle Vehicle;
    /// <summary>Fourmi rouge qui tient le soldat dans ses mandibules (null sinon) : ni déplacement, ni saut, ni véhicule ; la visée et le tir restent possibles.</summary>
    public Enemy GrabbedBy;
    private Vector3 _camLag;
    private float _camHeight;
    /// <summary>Mode automatique pour les tests : vise et tire seul sur l'ennemi le plus proche.</summary>
    public static bool Bot;
    /// <summary>Test : le soldat ne subit aucun dégât (--god), pour des mesures de performance stables.</summary>
    public static bool God;
    /// <summary>Test : le bot vise sans tirer (--holdfire) et reste sur place (--roof).</summary>
    public static bool BotHoldFire, BotStill;
    /// <summary>Test : point que le bot regarde (captures d'une livraison), null = comportement normal.</summary>
    public static Vector3? BotLookAt;
    /// <summary>Test (--edf5-fire) : avec <see cref="BotLookAt"/>, le bot tire aussi sur le point regardé.</summary>
    public static bool BotForceFire;
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
    private JetpackAudio _jetAudio;
    private float _stepT;

    private const float Gravity = 24f;

    public float CallCost => CallIns.BarrageCost;

    /// <summary>Deux jeux d'armes (Grenadier blindé avec plus de deux armes) : emplacements 1-2 = jeu 1, 3-4 = jeu 2.</summary>
    public bool HasSets => Classes.HasWeaponSets(Def.Id) && Weapons.Count > Classes.SetSize;
    public int SetCount => HasSets ? (Weapons.Count + Classes.SetSize - 1) / Classes.SetSize : 1;
    public int ActiveSet => HasSets ? Mathf.Clamp(Current, 0, Weapons.Count - 1) / Classes.SetSize : 0;
    /// <summary>Avancée du changement de jeu (0 = commencé, 1 = prêt).</summary>
    public float SetSwapProgress => 1f - SetSwapLeft / SetSwapTime;
    /// <summary>Avancée de l'appui long sur la touche d'appel (0..1), pour le HUD.</summary>
    public float CallHold => _callHeld >= 0 ? Mathf.Clamp(_callHeld / CallIns.HoldTime, 0, 1) : 0f;

    /// <summary>Crédits d'appel gagnés par les dégâts infligés (Opérateur d'appui seulement).</summary>
    public void EarnCredits(float damage)
    {
        if (Def.Id == SoldierClass.Support) CallCredits = Mathf.Min(CallIns.Cap, CallCredits + damage * 0.08f);
    }

    /// <summary>Distance de la caméra à l'épaule : vue rapprochée (le Grenadier, plus massif, un peu plus loin).</summary>
    private static float CamDistance(SoldierClass cls, bool aiming) => aiming ? 1.7f : cls == SoldierClass.Heavy ? 3.5f : 2.9f;
    public WeaponState Weapon => Weapons.Count > 0 ? Weapons[Mathf.Clamp(Current, 0, Weapons.Count - 1)] : null;

    public static Player Create(World world, SoldierClass cls, List<WeaponInstance> loadout, float maxArmor)
    {
        var p = new Player { World = world, Def = Classes.Get(cls), Name = "LocalPlayer", CollisionLayer = 1 << 1, CollisionMask = 1 | 1 << 5 }; // couche 6 : camions du convoi (MissionObjective)
        p.MaxHealth = p.Health = maxArmor;
        foreach (var w in loadout) p.Weapons.Add(new WeaponState(w));
        p.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = cls == SoldierClass.Heavy ? 0.5f : 0.38f, Height = cls == SoldierClass.Heavy ? 2.1f : 1.8f },
            Position = new Vector3(0, cls == SoldierClass.Heavy ? 1.05f : 0.9f, 0),
        });
        p.Model = SoldierModel.Create(cls);
        p.AddChild(p.Model);

        p._camHeight = cls == SoldierClass.Heavy ? 1.9f : 1.65f;
        p._camYaw = new Node3D { Position = new Vector3(0, p._camHeight, 0) };
        p.AddChild(p._camYaw);
        p._camPitch = new Node3D();
        p._camYaw.AddChild(p._camPitch);
        p._arm = new SpringArm3D { SpringLength = CamDistance(cls, false), CollisionMask = 1, Margin = 0.3f, Position = new Vector3(0.85f, 0.15f, 0) };
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

    /// <summary>Inclinaison de la caméra (tests : cadrage des captures).</summary>
    public void SetPitch(float pitch) => _pitch = Mathf.Clamp(pitch, -1.35f, 1.2f);

    public void SetYaw(float yaw)
    {
        _yaw = yaw;
        _modelYaw = yaw + Mathf.Pi;
    }

    public Vector3 AimForward => -_camPitch.GlobalBasis.Z;

    public override void _UnhandledInput(InputEvent e)
    {
        // Dernier périphérique utilisé : la visée assistée ne joue qu'à la manette.
        if (e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.3f || e is InputEventJoypadButton) _padActive = true;
        else if (e is InputEventMouseMotion || e is InputEventMouseButton || e is InputEventKey) _padActive = false;
        if (World.Paused || World.Ended) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            var sens = 0.0022f * Settings.I.MouseSensitivity * (Aiming ? Settings.I.AimSensitivity : 1f);
            _yaw -= mm.Relative.X * sens;
            _pitch -= mm.Relative.Y * sens * (Settings.I.InvertY ? -1 : 1);
            _pitch = Mathf.Clamp(_pitch, -1.35f, 1.2f);
        }
        if (Downed) return;
        if (e.IsActionPressed("interact") && GrabbedBy == null)
        {
            if (Vehicle != null)
            {
                if (Vehicle.CanExit) Net.I.RequestVehicleSeat(Vehicle.Id, false);
                else World.Hud?.Toast("Posez l'hélicoptère pour descendre.");
                return;
            }
            var t = World.VehicleNear(GlobalPosition);
            // Escouade à son poste à portée : on la rallie (sauf si un véhicule est plus près).
            Ally sm = null;
            var squad = World.Allies?.UnralliedNear(GlobalPosition, RallyRange, out sm) ?? -1;
            if (squad >= 0 && World.DownedTeammateNear(GlobalPosition, 3.5f) == 0
                && (t == null || sm.GlobalPosition.DistanceTo(GlobalPosition) < t.GlobalPosition.DistanceTo(GlobalPosition)))
            {
                Net.I.RequestRally(squad);
                return;
            }
            if (t != null && World.DownedTeammateNear(GlobalPosition, 3.5f) == 0) { Net.I.RequestVehicleSeat(t.Id, true); return; }
        }
        if (e.IsActionPressed("ping"))
        {
            var at = AimPoint(400f, out var col);
            var kind = col is Enemy ? 1 : World.NearestCrate(at, 4f) >= 0 ? 2 : 0;
            Net.I.SendPing(at, kind);
            return;
        }
        if (Vehicle != null) return;
        if (e.IsActionPressed("weapon_1")) SelectSlot(0);
        else if (e.IsActionPressed("weapon_2")) SelectSlot(1);
        else if (e.IsActionPressed("weapon_3")) SelectSlot(2);
        else if (e.IsActionPressed("weapon_swap")) NextWeapon(1);
        else if (e.IsActionPressed("weapon_set")) SwapSet();
        else if (e.IsActionPressed("reload")) Reload();
        else if (e.IsActionPressed("ability")) UseAbility();
        else if (e.IsActionPressed("grenade")) ThrowGrenade();
        else if (e is InputEventMouseButton mb && mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            NextWeapon(mb.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
    }

    /// <summary>Touches 1/2/3 : emplacement du jeu actif pour le Grenadier (1 et 2 seulement), emplacement direct sinon.</summary>
    private void SelectSlot(int k)
    {
        if (!HasSets) { Switch(k); return; }
        if (k < Classes.SetSize) Switch(ActiveSet * Classes.SetSize + k);
    }

    /// <summary>Arme suivante / précédente : toutes les armes, ou les deux armes du jeu actif pour le Grenadier.</summary>
    private void NextWeapon(int dir)
    {
        if (Weapons.Count == 0) return;
        if (!HasSets) { Switch((Current + dir + Weapons.Count) % Weapons.Count); return; }
        var start = ActiveSet * Classes.SetSize;
        var n = Mathf.Min(Classes.SetSize, Weapons.Count - start);
        Switch(start + (Current - start + dir + n) % n);
    }

    private void Switch(int i)
    {
        if (i < 0 || i >= Weapons.Count || i == Current || SetSwapLeft > 0) return;
        Current = i;
        Weapon.Cooldown = Mathf.Max(Weapon.Cooldown, 0.25f);
        Sfx.Ui("reload", -8f, 1.3f);
    }

    /// <summary>
    /// Grenadier blindé : bascule sur l'autre jeu d'armes. L'arme est abaissée et le tir bloqué pendant 0,5 s
    /// (les autres pairs voient l'arme abaissée par l'indicateur de rechargement de l'état réseau).
    /// Les rechargements des deux jeux continuent en arrière-plan.
    /// </summary>
    public void SwapSet()
    {
        if (!HasSets || SetSwapLeft > 0 || Downed) return;
        var set = ActiveSet;
        _setPick[set % 2] = Current % Classes.SetSize;
        var next = (set + 1) % SetCount;
        Current = Mathf.Min(next * Classes.SetSize + _setPick[next % 2], Weapons.Count - 1);
        SetSwapLeft = SetSwapTime;
        _beamActive = false;
        Sfx.Ui("reload", -4f, 0.7f);
        Sfx.Ui("dash", -16f, 0.6f);
        if (Bot) GD.Print($"[jeux] jeu {next + 1} : {Weapon.W.T.Name} (emplacement {Current + 1})");
    }

    private void Reload()
    {
        var w = Weapon;
        if (w == null || w.Reloading) return;
        // Bombes C : le rechargement déclenche d'abord celles qui sont posées.
        if (w.W.T.Spec?.Kind == Edf5Kind.Bomb) DetonateBombs();
        if (Def.Id == SoldierClass.Airborne && w.W.T.EnergyCost > 0)
        {
            if (Overheated || Energy < w.W.T.EnergyCost * 0.5f) return;
            Energy -= w.W.T.EnergyCost;
        }
        var before = w.Reloading;
        w.StartReload();
        if (!before && w.Reloading) Sfx.Ui("reload", -4f);
    }

    private JetpackAudio Jet
    {
        get
        {
            if (_jetAudio == null) { _jetAudio = new JetpackAudio { Position = new Vector3(0, 1.2f, 0) }; AddChild(_jetAudio); }
            return _jetAudio;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (World.Ended) { Velocity = Vector3.Zero; return; }
        if (Vehicle != null)
        {
            Vehicle.DriveTick(this, dt, _yaw, _pitch);
            // La conduite peut éjecter le joueur (véhicule détruit par un choc) : il reprend alors la main au pas suivant.
            if (Vehicle == null) return;
            GlobalPosition = Vehicle.SeatPosition;
            Velocity = Vector3.Zero;
            _netTimer -= dt;
            if (_netTimer <= 0)
            {
                _netTimer = 0.05f;
                Net.I.SendPlayerState(GlobalPosition, _modelYaw, _pitch, 128, Health, Current, Vehicle.Velocity);
            }
            return;
        }
        if (GrabbedBy != null)
        {
            // Pris dans les mandibules : la position est imposée par la fourmi (World.UpdateGrabs) ; à terre, elle lâche prise.
            if (!IsInstanceValid(GrabbedBy) || GrabbedBy.State == EnemyState.Dead || Downed)
            {
                var e = GrabbedBy;
                if (IsInstanceValid(e) && e.GrabPeer == Net.I.MyId) e.ReleaseGrab();
                else OnReleased(e);
            }
            else
            {
                Velocity = Vector3.Zero;
                _dodgeTime = Mathf.Min(_dodgeTime, 0f);
                _dashTime = 0f;
                _diving = false;
                Flying = false;
                if (Def.Id == SoldierClass.Airborne) Energy = Mathf.Min(100f, Energy + 16f * dt);
                _netTimer -= dt;
                if (_netTimer <= 0)
                {
                    _netTimer = 0.05f;
                    var gflags = 4 | (Aiming ? 8 : 0) | (Time.GetTicksMsec() / 1000f - _lastFireTime < 0.3f ? 16 : 0)
                                 | StyleBits << 8 | (Weapon != null && (Weapon.Reloading || SetSwapLeft > 0) ? 1 << 11 : 0);
                    Net.I.SendPlayerState(GlobalPosition, _modelYaw, _pitch, gflags, Health, Current, Vector3.Zero);
                }
                return;
            }
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
                }
                else if (!onFloor) speed = 10f;
                if (!Overheated && (sprintPressed || dodgePressed) && Energy > 15 && wish.LengthSquared() > 0.01f)
                {
                    _dashTime = 0.35f;
                    _dashDir = wish.Normalized();
                    Energy -= 15;
                    Jet.Boost();
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
        if (GlobalPosition.Y < World.GroundHeight(GlobalPosition) - 20f) GlobalPosition = World.OnGround(Vector3.Zero, 2f);

        // Envoi de l'état aux autres pairs (20 Hz).
        _netTimer -= dt;
        if (_netTimer <= 0)
        {
            _netTimer = 0.05f;
            var flags = (horiz.Length() > 0.5f ? 1 : 0) | (speed > Def.WalkSpeed + 0.1f ? 2 : 0) | (!IsOnFloor() ? 4 : 0) | (Aiming ? 8 : 0)
                        | (Time.GetTicksMsec() / 1000f - _lastFireTime < 0.3f ? 16 : 0) | (Downed ? 32 : 0) | (Flying ? 64 : 0)
                        | StyleBits << 8 | (Weapon != null && (Weapon.Reloading || SetSwapLeft > 0) ? 1 << 11 : 0);
            Net.I.SendPlayerState(GlobalPosition, _modelYaw, _pitch, flags, Health, Current, Velocity);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        // Hors du chemin de déplacement (véhicule, saisie, à terre) pour que la flamme s'éteigne toujours.
        if (Def.Id == SoldierClass.Airborne) Jet.Tick(Flying && Vehicle == null && !Downed && GrabbedBy == null, Velocity.Y, dt);
        // Caméra au stick droit de la manette.
        if (!World.Paused && !World.Ended)
        {
            var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
            var move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
            var assist = _padActive && Vehicle == null && !Downed ? AimAssistTarget(dt) : null;
            if (assist != null && (look.LengthSquared() > 0.0001f || move.LengthSquared() > 0.01f || Aiming)) PullToward(assist, dt);
            if (look.LengthSquared() > 0.0001f)
            {
                var sens = 3.2f * Settings.I.MouseSensitivity * (Aiming ? Settings.I.AimSensitivity : 1f) * dt;
                // Freinage de la visée quand le réticule passe sur un ennemi.
                if (assist != null) sens *= 1f - 0.5f * Settings.I.AimAssist;
                _yaw -= look.X * Mathf.Abs(look.X) * sens;
                _pitch -= look.Y * Mathf.Abs(look.Y) * sens * 0.7f * (Settings.I.InvertY ? -1 : 1);
                _pitch = Mathf.Clamp(_pitch, -1.35f, 1.2f);
            }
        }
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
        var zoom = Aiming ? (Vehicle != null ? Vehicle.AimZoom : Weapon != null && Weapon.W.T.Zoom > 1 ? Weapon.W.T.Zoom : 1.4f) : 1f;
        Camera.Fov = Mathf.Lerp(Camera.Fov, Settings.I.Fov / zoom, Mathf.Clamp(dt * 12f, 0, 1));
        // Saisi par une fourmi : la caméra recule et s'élève (la fourmi entière reste lisible) et amortit les secousses de la tête.
        var grabbed = GrabbedBy != null && IsInstanceValid(GrabbedBy);
        _camLag = grabbed ? _camLag.Lerp(GlobalPosition, Mathf.Clamp(dt * 4f, 0, 1)) : GlobalPosition;
        _camYaw.Position = new Vector3(0, _camHeight, 0) + (_camLag - GlobalPosition);
        var camDist = grabbed ? (Aiming ? 3.4f : 5.6f) : CamDistance(Def.Id, Aiming);
        _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, camDist, Mathf.Clamp(dt * (grabbed ? 4f : 10f), 0, 1));
        // Épaule décalée de côté : la tête de la fourmi et la victime se lisent de trois quarts, pas l'une derrière l'autre.
        _arm.Position = _arm.Position.Lerp(new Vector3(grabbed ? 1.7f : 0.85f, 0.15f + (Vehicle?.CameraLift ?? 0f) + (grabbed ? 0.3f : 0f), 0), Mathf.Clamp(dt * 4f, 0, 1));
        if (World.Underground) ClampShoulder();
        _shake = Mathf.MoveToward(_shake, 0, dt * 2.5f);
        Camera.HOffset = (float)GD.RandRange(-1, 1) * _shake * 0.25f;
        Camera.VOffset = (float)GD.RandRange(-1, 1) * _shake * 0.25f + _recoil * 0.02f;

        // Orientation du modèle
        var horiz = new Vector3(Velocity.X, 0, Velocity.Z);
        var firing = Time.GetTicksMsec() / 1000f - _lastFireTime < 0.8f;
        float targetYaw;
        if (Aiming || firing || Flying) targetYaw = _yaw + Mathf.Pi;
        else if (grabbed)
        {
            // Pris dans les mandibules : face à la tête de la fourmi.
            var toAnt = GrabbedBy.HeadPoint - GlobalPosition;
            targetYaw = toAnt.X * toAnt.X + toAnt.Z * toAnt.Z > 0.01f ? Mathf.Atan2(toAnt.X, toAnt.Z) : _modelYaw;
        }
        else if (horiz.LengthSquared() > 0.5f) targetYaw = Mathf.Atan2(horiz.X, horiz.Z);
        else targetYaw = _modelYaw;
        _modelYaw = Mathf.LerpAngle(_modelYaw, targetYaw, Mathf.Clamp(dt * 12f, 0, 1));
        Model.Rotation = new Vector3(0, _modelYaw, 0);
        Model.Grabbed = grabbed;
        Model.Animate(dt, grabbed ? 0f : horiz.Length(), _pitch, !grabbed && !IsOnFloor(), Flying, Downed, Aiming, _recoil);

        Model.Visible = Vehicle == null;
        if (SetSwapLeft > 0)
        {
            SetSwapLeft = Mathf.Max(0, SetSwapLeft - dt);
            if (SetSwapLeft <= 0) Sfx.Ui("reload", -6f, 1.45f); // verrouillage de l'exosquelette : jeu prêt
        }
        if (Weapon != null)
        {
            Model.SetWeapon(Weapon.W.T);
            // Arme d'EDF 5 : son identifiant est envoyé aux autres pairs (au changement, puis toutes les 3 s).
            _gunSyncT -= dt;
            if (Model.GunId != _sentGunId || _gunSyncT <= 0)
            {
                _sentGunId = Model.GunId;
                _gunSyncT = 3f;
                Net.I.SendWeaponModel(Model.GunId ?? "");
            }
            Model.Reloading = Weapon.Reloading || SetSwapLeft > 0;
        }
        if (Def.Id == SoldierClass.Support) HandleCallKey(dt, canAct && Vehicle == null && GrabbedBy == null);
        if (Vehicle != null)
        {
            _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, Vehicle.CameraDistance(Aiming), Mathf.Clamp(dt * 6f, 0, 1));
            if (Bot && canAct)
            {
                BotAim(dt);
                if (_botFire) Input.ActionPress("fire"); else Input.ActionRelease("fire");
                if (Vehicle is Helicopter h) BotFly(h);
            }
            HandleRevive(dt);
            return;
        }
        if (Bot && canAct) BotAim(dt);
        if (canAct && Weapon != null && SetSwapLeft <= 0) HandleFire(dt);
        else _beamActive = false;
        if (Downed) HandleDowned(dt);
        else if (!grabbed) HandleRevive(dt);
        else ReviveProgress = 0;
    }

    /// <summary>La fourmi rouge vient de saisir le soldat (tous les pairs : seulement le soldat local ici).</summary>
    public void OnGrabbed(Enemy e)
    {
        GrabbedBy = e;
        Velocity = Vector3.Zero;
        _dodgeTime = Mathf.Min(_dodgeTime, 0f);
        _dashTime = 0f;
        _diving = false;
        ReviveProgress = 0;
        AddShake(0.6f);
        DamageFlash = Mathf.Max(DamageFlash, 0.5f);
        Sfx.Ui("grunt", -2f, 1.05f, 0);
    }

    /// <summary>Libéré (fourmi tuée, soldat à terre, fourmi retirée) : il retombe, légèrement écarté de la tête.</summary>
    public void OnReleased(Enemy e)
    {
        if (GrabbedBy != e && e != null) return;
        GrabbedBy = null;
        var push = Vector3.Zero;
        if (IsInstanceValid(e))
        {
            push = GlobalPosition - e.GlobalPosition;
            push.Y = 0;
            push = push.LengthSquared() > 0.01f ? push.Normalized() * 3f : Vector3.Zero;
        }
        Velocity = push + Vector3.Up * 2f;
    }

    // ------------------------------------------------------------------ Tir

    private bool _beamActive;
    private string _sentGunId;
    private float _gunSyncT;

    /// <summary>Style d'arme procédural transmis dans l'état réseau (repli si l'arme d'EDF 5 n'est pas encore connue).</summary>
    private int StyleBits => (int)(Weapon != null ? SoldierModel.StyleFor(Weapon.W.T) : WeaponStyle.Rifle) & 7;

    public float DamageMul => (RallyTime > 0 ? 1.3f : 1f);

    private void HandleFire(float dt)
    {
        var ws = Weapon;
        var t = ws.W.T;
        var held = Input.IsActionPressed("fire") || _botFire;
        var pressed = Input.IsActionJustPressed("fire") || (_botFire && ws.Cooldown <= 0);
        _beamActive = false;
        if (IsEdf5(ws)) { HandleFireEdf5(ws, dt, held, pressed); return; }
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
        // Balise sans appui aérien (réseau souterrain) : pas de liaison, rien n'est consommé.
        if (t.Kind == WeaponKind.Airstrike && !World.AirSupport)
        {
            ws.Cooldown = 0.6f;
            Sfx.Ui("empty");
            return;
        }
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
        // La caméra regarde à travers son propre véhicule. Attention : q.Exclude renvoie une copie,
        // il faut affecter le tableau complet (un Add sur la propriété serait perdu).
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (Vehicle != null) exclude.Add(Vehicle.GetRid());
        q.Exclude = exclude;
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
                    var weak = e.IsWeakShape(shape);
                    var falloff = Mathf.Clamp(1.2f - muzzle.DistanceTo(end) / ws.W.Range, 0.5f, 1f);
                    var dmg = ws.W.Damage * falloff * (weak ? 1.5f : 1f) * DamageMul;
                    perEnemy.TryGetValue(e, out var acc);
                    perEnemy[e] = (acc.dmg + dmg, end, acc.weak || weak);
                }
                else
                {
                    Effects.Sparks(World, end, (Vector3)hit["normal"]);
                    if (col is AlienShield sh) sh.LocalHit(end);
                    else if (col is Building b && !b.Destroyed) Net.I.SendBuildingDamage(b.Id, ws.W.Damage * 0.06f, end);
                    else World.Decor?.LocalHit(col, shape, ws.W.Damage * Mathf.Clamp(1.2f - muzzle.DistanceTo(end) / ws.W.Range, 0.5f, 1f) * DamageMul, muzzle);
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
            EarnCredits(v.dmg);
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
                // Machines : le rayon compte aussi les points faibles (soute ouverte du vaisseau, noyau, genoux).
                var bweak = (e.IsMachine || e.IsMothership) && hit.Count > 0 && e.IsWeakShape(hit["shape"].AsInt32());
                Net.I.SendDamageEnemy(e.Id, _beamDamage, end, bweak);
                World.LocalHitFx(e, end, true, _beamDamage, bweak);
                HitMarker = 1f;
                _beamDamage = 0;
                _beamAccum = 0;
            }
        }
        else if (_beamAccum >= 0.1f)
        {
            if (col is AlienShield sh) sh.LocalHit(end);
            else if (col is Building b && !b.Destroyed) Net.I.SendBuildingDamage(b.Id, ws.W.Damage * 0.1f * 0.3f, end);
            else World.Decor?.LocalHit(hit, ws.W.Damage * 0.1f * DamageMul, muzzle);
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
        var kind = t.Category switch
        {
            "Canon à plasma" => ProjectileKind.Plasma,
            "Lance-grenades" => ProjectileKind.Grenade,
            "Lance-missiles" => ProjectileKind.Missile,
            "Canon de siège" => ProjectileKind.Shell,
            _ => ProjectileKind.Rocket,
        };
        // Les armes à plusieurs projectiles (ex. LR-9 Hydre) tirent une salve en éventail.
        Projectile p = null;
        for (var shot = 0; shot < Mathf.Max(1, t.Pellets); shot++)
        {
            var d = shot == 0 ? dir : Spread((aim - muzzle).Normalized(), Mathf.DegToRad(Mathf.Max(ws.W.Spread, 3f)), World.Rng);
            var v = d * t.ProjectileSpeed;
            if (kind == ProjectileKind.Grenade) v += Vector3.Up * 6f;
            p = Projectile.Spawn(World, kind, muzzle, v, true, ws.W.Damage * DamageMul, ws.W.BlastRadius);
        }
        var vel = dir * t.ProjectileSpeed;
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
        pos.Y = World.GroundHeight(pos);
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
                Jet.Boost(-5f, 0.85f);
                break;
            case SoldierClass.Support:
                if (CallCredits < CallCost || !World.AirSupport) { Sfx.Ui("empty"); return; }
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

    /// <summary>
    /// Galeries étroites (réseau souterrain) : la caméra balaie une petite sphère (pas seulement un rayon) et l'épaule
    /// décalée est ramenée vers le soldat quand elle entrerait dans une paroi ou une voûte.
    /// </summary>
    private void ClampShoulder()
    {
        _arm.Shape ??= new SphereShape3D { Radius = 0.22f };
        var from = _camPitch.GlobalPosition;
        var to = _arm.GlobalPosition;
        var d = to - from;
        var len = d.Length();
        if (len < 0.05f) return;
        var q = PhysicsRayQueryParameters3D.Create(from, to + d / len * 0.35f, 1);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0) return;
        _arm.Position *= Mathf.Clamp((from.DistanceTo((Vector3)hit["position"]) - 0.35f) / len, 0f, 1f);
    }

    public float AbilityReady => Def.Id == SoldierClass.Support
        ? (World.AirSupport ? Mathf.Clamp(CallCredits / CallCost, 0, 1) : 0f)
        : 1f - AbilityCooldown / Mathf.Max(AbilityMaxCooldown, 0.01f);

    // ------------------------------------------------------------------ Appel de véhicule (Opérateur d'appui)

    /// <summary>
    /// Touche d'appel : un appui bref change de véhicule (char ↔ hélicoptère), un appui maintenu
    /// <see cref="CallIns.HoldTime"/> s commande le véhicule choisi au point visé.
    /// </summary>
    private void HandleCallKey(float dt, bool canCall)
    {
        if (!canCall) { _callHeld = -1f; return; }
        if (Input.IsActionJustPressed("call_vehicle")) _callHeld = 0f;
        if (_callHeld < 0) return;
        if (Input.IsActionPressed("call_vehicle"))
        {
            _callHeld += dt;
            if (_callHeld < CallIns.HoldTime) return;
            _callHeld = -1f;
            TryCallVehicle();
            return;
        }
        // Relâchée avant la fin de l'appui long : véhicule suivant.
        _callHeld = -1f;
        CallChoice = (CallChoice + 1) % CallIns.Count;
        Sfx.Ui("ui_click", -6f, CallChoice == CallIns.Tank ? 0.9f : 1.2f);
    }

    /// <summary>Le véhicule déjà appelé empêche-t-il un nouvel appel ? (occupé ou encore en livraison ; une épave ne compte plus)</summary>
    public bool CalledVehicleBusy =>
        CalledVehicleId != 0 && World.Vehicles.TryGetValue(CalledVehicleId, out var v) && !v.Wrecked && (v.Driver != 0 || v.Delivering);

    /// <summary>Demande à l'hôte la livraison du véhicule choisi au point visé (les crédits sont débités à la confirmation).</summary>
    public bool TryCallVehicle()
    {
        if (Def.Id != SoldierClass.Support || Downed) return false;
        if (CallCredits < CallIns.Costs[CallChoice] || CalledVehicleBusy || !World.AirSupport)
        {
            Sfx.Ui("empty");
            return false;
        }
        var at = AimPoint(CallIns.Range * 1.5f, out _);
        Net.I.RequestVehicleCall(CallChoice, at);
        Sfx.Ui("radio");
        return true;
    }

    /// <summary>L'hôte a confirmé l'appel de ce soldat : crédits débités.</summary>
    public void OnVehicleCallConfirmed(int id, int type)
    {
        CalledVehicleId = id;
        CallCredits = Mathf.Max(0, CallCredits - CallIns.Costs[Mathf.Clamp(type, 0, CallIns.Count - 1)]);
    }

    // ------------------------------------------------------------------ Santé

    public void EnterVehicle(Vehicle v)
    {
        Vehicle = v;
        CollisionLayer = 0;
        _arm.AddExcludedObject(v.GetRid());
        World.Hud?.Toast(v.EnterHint);
    }

    public void ExitVehicle()
    {
        if (Vehicle == null) return;
        var t = Vehicle;
        Vehicle = null;
        CollisionLayer = 1 << 1;
        _arm.RemoveExcludedObject(t.GetRid());
        if (Bot) foreach (var a in new[] { "fire", "aim", "jump", "sprint", "move_forward" }) Input.ActionRelease(a);
        GlobalPosition = t.GlobalPosition + t.GlobalBasis.X * 3.5f + Vector3.Up * 0.5f;
        Velocity = Vector3.Zero;
    }

    public void TakeDamage(float dmg, Vector3 from)
    {
        if (Downed || World.Ended || God) return;
        if (Vehicle != null)
        {
            // Le blindage encaisse à la place du soldat.
            if (Vehicle.Wrecked || Vehicle.IgnoresDamageFrom(from)) return;
            Vehicle.Health -= dmg * Vehicle.ArmorFactor;
            AddShake(0.1f);
            World.Hud?.DamageFrom(from);
            if (Vehicle.Health <= 0) Net.I.SendVehicleWrecked(Vehicle.Id);
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

    private bool _padActive;
    private Enemy _assistTarget;
    private float _assistScan;

    /// <summary>
    /// Visée assistée (manette) : ennemi vivant le plus proche de l'axe de visée dans un cône de 7° (9° en visée), jusqu'à 160 m.
    /// Recherche toutes les 0,1 s.
    /// </summary>
    private Enemy AimAssistTarget(float dt)
    {
        if (Settings.I.AimAssist <= 0.01f) return null;
        _assistScan -= dt;
        if (_assistScan > 0 && _assistTarget != null && IsInstanceValid(_assistTarget) && _assistTarget.State != EnemyState.Dead) return _assistTarget;
        if (_assistScan > 0) return null;
        _assistScan = 0.1f;
        _assistTarget = null;
        var eye = Camera.GlobalPosition;
        var fwd = -Camera.GlobalBasis.Z;
        var cone = Mathf.Cos(Mathf.DegToRad(Aiming ? 9f : 7f));
        var best = cone;
        foreach (var e in World.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var to = e.Center - eye;
            var d = to.Length();
            if (d < 2f || d > 160f) continue;
            var c = fwd.Dot(to / d);
            if (c > best) { best = c; _assistTarget = e; }
        }
        return _assistTarget;
    }

    /// <summary>Attire doucement la visée vers la cible (vitesse proportionnelle au réglage, jamais instantanée).</summary>
    private void PullToward(Enemy e, float dt)
    {
        var dir = e.Center - Camera.GlobalPosition;
        var yaw = Mathf.Atan2(-dir.X, -dir.Z);
        var pitch = Mathf.Atan2(dir.Y, new Vector2(dir.X, dir.Z).Length());
        var rate = Settings.I.AimAssist * (Aiming ? 1.6f : 0.9f) * dt;
        _yaw += Mathf.Clamp(Mathf.AngleDifference(_yaw, yaw), -rate, rate);
        _pitch = Mathf.Clamp(_pitch + Mathf.Clamp(pitch - _pitch, -rate * 0.6f, rate * 0.6f), -1.35f, 1.2f);
    }

    /// <summary>Portée du ralliement d'une escouade PNJ (touche d'interaction).</summary>
    public const float RallyRange = 10f;

    private float _botRally;

    private void BotAim(float dt)
    {
        // Le bot rallie les escouades qu'il croise (tests automatiques).
        _botRally -= dt;
        if (_botRally <= 0)
        {
            _botRally = 1f;
            var sq = World.Allies?.UnralliedNear(GlobalPosition, RallyRange, out _) ?? -1;
            if (sq >= 0) Net.I.RequestRally(sq);
        }
        BotDodge(dt);
        // Test (captures) : regard imposé sur un point, sans tirer.
        if (BotLookAt is { } look)
        {
            var tl = look - _camYaw.GlobalPosition;
            _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(-tl.X, -tl.Z), Mathf.Clamp(dt * 4f, 0, 1));
            _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(Mathf.Atan2(tl.Y, new Vector2(tl.X, tl.Z).Length()), -1.2f, 1.1f), Mathf.Clamp(dt * 4f, 0, 1));
            _botFire = BotForceFire && Mathf.Abs(Mathf.AngleDifference(_yaw, Mathf.Atan2(-tl.X, -tl.Z))) < 0.05f;
            if (BotForceFire && Weapon.Ammo == 0 && !Weapon.Reloading) Reload();
            return;
        }
        // Saisi : le bot tire sur la tête de la fourmi qui le tient (le seul moyen de se libérer).
        var held = GrabbedBy != null && IsInstanceValid(GrabbedBy) ? GrabbedBy : null;
        var e = held ?? World.NearestEnemy(GlobalPosition, 140f);
        _botFire = false;
        if (e == null) return;
        var to = (held != null ? held.HeadPoint : e.Center) - _camYaw.GlobalPosition;
        var yaw = Mathf.Atan2(-to.X, -to.Z);
        var flat = new Vector2(to.X, to.Z).Length();
        if (held != null)
        {
            // Saisi, la caméra est décalée de côté (épaule à droite) : le rayon du réticule passe à côté du pivot ;
            // on tourne vers la gauche de asin(décalage / distance) pour qu'il tombe sur la tête.
            var side = _arm.Position.X;
            if (flat > side + 0.1f)
            {
                yaw += Mathf.Asin(side / flat);
                flat = Mathf.Sqrt(flat * flat - side * side);
            }
            to.Y -= _arm.Position.Y;
        }
        var pitch = Mathf.Atan2(to.Y, flat);
        _yaw = Mathf.LerpAngle(_yaw, yaw, Mathf.Clamp(dt * 5f, 0, 1));
        _pitch = Mathf.Lerp(_pitch, pitch, Mathf.Clamp(dt * 5f, 0, 1));
        _botFire = !BotHoldFire && Mathf.Abs(Mathf.AngleDifference(_yaw, yaw)) < 0.15f;
        if (Weapon.Ammo == 0 && !Weapon.Reloading) Reload();
        // Le bot du Commando aéroporté vole par intermittence (teste vol, énergie et surchauffe).
        if (Def.Id == SoldierClass.Airborne)
        {
            if (!Overheated && Energy > 60) Input.ActionPress("jump");
            else if (Energy < 25 || Overheated) Input.ActionRelease("jump");
        }
    }

    private float _botStrafeT;
    private int _botStrafe;

    /// <summary>Le bot esquive comme un joueur : pas chassés réguliers, sprint hors de la zone du canon de la forteresse.</summary>
    private void BotDodge(float dt)
    {
        if (Vehicle != null || BotStill) return;
        var danger = false;
        foreach (var e in World.EnemiesById.Values)
            if (e.CannonZone is Vector3 z && new Vector2(z.X - GlobalPosition.X, z.Z - GlobalPosition.Z).Length() < 12f) { danger = true; break; }
        _botStrafeT -= dt;
        if (_botStrafeT <= 0)
        {
            _botStrafeT = danger ? 2.5f : (float)GD.RandRange(1.2, 2.6);
            _botStrafe = danger ? (GD.Randf() < 0.5f ? -1 : 1) : GD.RandRange(-1, 1);
        }
        if (danger && _botStrafe == 0) _botStrafe = 1;
        if (_botStrafe < 0) Input.ActionPress("move_left"); else Input.ActionRelease("move_left");
        if (_botStrafe > 0) Input.ActionPress("move_right"); else Input.ActionRelease("move_right");
        if (danger) Input.ActionPress("sprint"); else Input.ActionRelease("sprint");
    }

    /// <summary>Bot en hélicoptère : tient ~25 m d'altitude, avance vers les ennemis lointains, tire des roquettes.</summary>
    private void BotFly(Helicopter h)
    {
        if (Helicopter.Test == "down") { BotHeliDown(h); return; }
        if (Helicopter.Test == "ram") { BotHeliRam(h); return; }
        if (h.Altitude < 23f) Input.ActionPress("jump"); else Input.ActionRelease("jump");
        if (h.Altitude > 28f) Input.ActionPress("sprint"); else Input.ActionRelease("sprint");
        var e = World.NearestEnemy(GlobalPosition, 400f);
        var far = e != null && new Vector2(e.GlobalPosition.X - GlobalPosition.X, e.GlobalPosition.Z - GlobalPosition.Z).Length() > 70f;
        if (e != null && far)
        {
            // Pas de cible à portée : cap sur l'ennemi le plus proche.
            var to = e.GlobalPosition - GlobalPosition;
            _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(-to.X, -to.Z), 0.05f);
            _pitch = Mathf.Lerp(_pitch, -0.25f, 0.05f);
        }
        if (far && h.Altitude > 15f) Input.ActionPress("move_forward"); else Input.ActionRelease("move_forward");
        if (_botFire && h.RocketsAvailable) Input.ActionPress("aim"); else Input.ActionRelease("aim");
    }

    /// <summary>Test --heli-test=down : vol stationnaire à ~20 m, regard à la verticale, minigun et roquettes en continu.</summary>
    private void BotHeliDown(Helicopter h)
    {
        if (h.Altitude < 18f) Input.ActionPress("jump"); else Input.ActionRelease("jump");
        if (h.Altitude > 24f) Input.ActionPress("sprint"); else Input.ActionRelease("sprint");
        Input.ActionRelease("move_forward");
        _pitch = -1.35f;
        var go = h.Altitude > 14f;
        if (go) Input.ActionPress("fire"); else Input.ActionRelease("fire");
        if (go && h.RocketsAvailable) Input.ActionPress("aim"); else Input.ActionRelease("aim");
    }

    /// <summary>Test --heli-test=ram : fonce à ~7 m d'altitude vers l'immeuble le plus proche (dégâts de collision).</summary>
    private void BotHeliRam(Helicopter h)
    {
        Building best = null;
        var bestD = float.MaxValue;
        foreach (var b in World.Buildings)
        {
            if (b.Destroyed) continue;
            var d = b.GlobalPosition.DistanceTo(h.GlobalPosition);
            if (d > 25f && d < bestD) { bestD = d; best = b; }
        }
        _ramTarget ??= best;
        if (h.Altitude < 6f) Input.ActionPress("jump"); else Input.ActionRelease("jump");
        if (h.Altitude > 9f) Input.ActionPress("sprint"); else Input.ActionRelease("sprint");
        Input.ActionRelease("fire");
        Input.ActionRelease("aim");
        if (_ramTarget == null) return;
        var to = _ramTarget.GlobalPosition - GlobalPosition;
        _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(-to.X, -to.Z), 0.1f);
        _pitch = -0.1f;
        if (Engine.GetPhysicsFrames() % 60 == 0)
            GD.Print($"[heli] éperonnage : appareil {h.GlobalPosition.Snapped(Vector3.One * 0.1f)} vitesse {h.Velocity.Length():0.0} m/s alt {h.Altitude:0.0}, cible {_ramTarget.Name} {_ramTarget.GlobalPosition.Snapped(Vector3.One)} taille {_ramTarget.Size}, blindage {h.Health:0}");
        if (h.Altitude > 4f) Input.ActionPress("move_forward"); else Input.ActionRelease("move_forward");
    }

    private Building _ramTarget;

    public void AddShake(float amount) => _shake = Mathf.Min(1.2f, _shake + amount * Settings.I.ShakeScale);
    public void ApplyRally() => RallyTime = 15f;
}
