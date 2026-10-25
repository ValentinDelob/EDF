using Godot;

namespace Rempart;

/// <summary>
/// Arme de bord d'un véhicule : chargeur (ou bande, panier) + réserve limitée par sortie.
/// Le chargeur vide se recharge automatiquement depuis la réserve ; le véhicule arrêté à son point
/// de départ se réarme (voir <see cref="Vehicle.AmmoTick"/>).
/// </summary>
public sealed class VehicleWeapon
{
    public readonly string Name;
    public readonly string AmmoName;
    public readonly HudWeaponIcon Icon;
    public readonly int MagSize;
    public readonly int MaxTotal;
    public readonly float ReloadTime;
    public int Mag;
    public int Reserve;
    public float ReloadLeft;

    public VehicleWeapon(string name, string ammoName, HudWeaponIcon icon, int magSize, int maxTotal, float reloadTime)
    {
        Name = name;
        AmmoName = ammoName;
        Icon = icon;
        MagSize = magSize;
        MaxTotal = maxTotal;
        ReloadTime = reloadTime;
        Mag = magSize;
        Reserve = maxTotal - magSize;
    }

    public int Total => Mag + Reserve;
    public bool Reloading => ReloadLeft > 0;
    public bool Empty => Total <= 0;
    public bool CanFire => !Reloading && Mag > 0;
    public float ReloadProgress => Reloading ? 1f - ReloadLeft / Mathf.Max(ReloadTime, 0.01f) : 1f;

    /// <summary>Consomme une munition ; lance le rechargement quand le chargeur se vide.</summary>
    public bool TryFire()
    {
        if (!CanFire) return false;
        Mag--;
        if (Mag == 0) StartReload();
        return true;
    }

    public bool StartReload()
    {
        if (Reloading || Mag >= MagSize || Reserve <= 0) return false;
        ReloadLeft = ReloadTime;
        return true;
    }

    /// <summary>Avance le rechargement ; vrai quand il vient de se terminer.</summary>
    public bool Tick(float dt)
    {
        if (ReloadLeft <= 0) return false;
        ReloadLeft -= dt;
        if (ReloadLeft > 0) return false;
        ReloadLeft = 0;
        var take = Mathf.Min(MagSize - Mag, Reserve);
        Mag += take;
        Reserve -= take;
        return true;
    }

    /// <summary>Ajoute une fraction de la dotation complète à la réserve (réarmement au point de départ).</summary>
    public void Rearm(float fraction)
    {
        var add = Mathf.Max(1, Mathf.CeilToInt(MaxTotal * fraction));
        Reserve = Mathf.Min(Reserve + add, MaxTotal - Mag);
        if (Mag == 0) StartReload();
    }

    /// <summary>Total reçu du conducteur (autres pairs) : chargeur plein d'abord, le reste en réserve.</summary>
    public void SetTotal(int total)
    {
        total = Mathf.Clamp(total, 0, MaxTotal);
        if (total == Total) return;
        Mag = Mathf.Min(Mathf.Min(Mag, MagSize), total);
        if (Mag == 0) Mag = Mathf.Min(MagSize, total);
        Reserve = total - Mag;
    }
}

/// <summary>
/// Base commune des véhicules pilotables (char, hélicoptère). Le conducteur simule le véhicule
/// localement et diffuse son état à 20 Hz ; les autres pairs l'interpolent. L'hôte attribue
/// les places (un seul conducteur à la fois). Les identifiants sont identiques chez tous les pairs.
/// </summary>
public abstract partial class Vehicle : CharacterBody3D
{
    public int Id;
    public long Driver;
    public float Health;
    public float MaxHealth;
    public bool Wrecked;
    /// <summary>Véhicule appelé par un Opérateur d'appui (pair qui l'a commandé ; 0 = véhicule garé de la carte).</summary>
    public long CalledBy;
    /// <summary>Livraison en cours (largage, voir <see cref="VehicleDrop"/>) : ni place, ni simulation, position imposée.</summary>
    public bool Delivering;
    protected World _world;
    protected Label3D _prompt;

    // Dernier état reçu (ou état courant si l'on conduit).
    protected Vector3 _netPos, _netRot;
    protected float _netA, _netB, _netC, _netSpeed;
    private float _netTimer;

    /// <summary>Armes de bord : [0] = clic gauche, [1] = clic droit. Munitions limitées par sortie.</summary>
    public VehicleWeapon[] Arms = System.Array.Empty<VehicleWeapon>();
    /// <summary>Point de départ : le véhicule s'y réarme à l'arrêt.</summary>
    protected Vector3 _home;
    private float _rearmT, _impactCd;
    private bool _wreckSent;

    // ---------- Description (interface, caméra, dégâts) ----------

    /// <summary>Nom affiché dans le HUD.</summary>
    public abstract string DisplayName { get; }
    /// <summary>Texte de l'invite « monter à bord ».</summary>
    public abstract string PromptText { get; }
    /// <summary>Message affiché en montant à bord.</summary>
    public abstract string EnterHint { get; }
    /// <summary>Message affiché quand le véhicule est détruit avec nous à bord.</summary>
    public abstract string WreckedHint { get; }
    /// <summary>Position du siège : le soldat y est collé, les ennemis visent ce point.</summary>
    public abstract Vector3 SeatPosition { get; }
    /// <summary>Distance d'interaction pour monter à bord.</summary>
    public virtual float InteractRadius => 6f;
    /// <summary>Rayon supplémentaire pour les tirs ennemis (le véhicule est plus gros qu'un soldat).</summary>
    public virtual float HitRadius => 3f;
    /// <summary>Part des dégâts encaissée par le blindage.</summary>
    public virtual float ArmorFactor => 0.6f;
    /// <summary>Longueur du bras de caméra (normal / visée).</summary>
    public virtual float CameraDistance(bool aiming) => aiming ? 7f : 11f;
    /// <summary>Surélévation de la caméra.</summary>
    public virtual float CameraLift => 0f;
    /// <summary>Zoom quand le clic droit est maintenu (1 = aucun).</summary>
    public virtual float AimZoom => 1.4f;
    /// <summary>Peut-on descendre maintenant ? (l'hélicoptère doit être posé)</summary>
    public virtual bool CanExit => true;
    /// <summary>Rayon (horizontal) autour du point de départ où le véhicule arrêté se réarme.</summary>
    public virtual float RearmRadius => 12f;
    /// <summary>Le véhicule est-il immobile (et posé) à son point de départ ?</summary>
    protected virtual bool AtRearmPoint =>
        new Vector2(GlobalPosition.X - _home.X, GlobalPosition.Z - _home.Z).Length() < RearmRadius && Velocity.Length() < 1.5f;
    /// <summary>Réarmement en cours (affiché par le HUD).</summary>
    public bool Rearming { get; private set; }
    /// <summary>Point de réarmement (radar).</summary>
    public Vector3 Home => _home;

    public bool IsLocalDriver => Driver == Net.I.MyId && Driver != 0;

    protected void InitCommon(World w, int id, Vector3 pos, float yaw, float health)
    {
        _world = w;
        Id = id;
        Health = MaxHealth = health;
        CollisionLayer = 1;
        CollisionMask = 1;
        Position = pos;
        Rotation = new Vector3(0, yaw, 0);
        _netPos = pos;
        _netRot = new Vector3(0, yaw, 0);
        _home = pos;
        _prompt = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 40, OutlineSize = 10, PixelSize = 0.006f,
            Font = UiKit.BodyFont, Visible = false, NoDepthTest = true,
        };
        AddChild(_prompt);
    }

    protected void CastShadows()
    {
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false)) ((MeshInstance3D)n).CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    /// <summary>Simulation par le conducteur local (déplacement, armes).</summary>
    public abstract void DriveTick(Player p, float dt, float camYaw, float camPitch);

    /// <summary>
    /// Diffuse l'état à 20 Hz. <paramref name="a"/>, <paramref name="b"/> et <paramref name="c"/> sont propres au véhicule
    /// (char : tourelle et canon ; hélicoptère : régime du rotor, lacet et site de la tourelle de nez).
    /// Les munitions restantes voyagent avec l'état : un joueur qui reprend le véhicule retrouve sa dotation entamée.
    /// </summary>
    protected void BroadcastState(float dt, Vector3 rot, float a, float b, float c, float speed)
    {
        // Le conducteur garde son propre état à jour : à la descente, le véhicule reste où il est.
        _netPos = GlobalPosition;
        _netRot = rot;
        _netA = a;
        _netB = b;
        _netC = c;
        _netSpeed = speed;
        _netTimer -= dt;
        if (_netTimer > 0) return;
        _netTimer = 0.05f;
        Net.I.SendVehicleState(Id, GlobalPosition, rot, a, b, c, Health, speed, PackAmmo());
    }

    public void ApplyState(Vector3 pos, Vector3 rot, float a, float b, float c, float hp, float speed, int ammo)
    {
        _netPos = pos;
        _netRot = rot;
        _netA = a;
        _netB = b;
        _netC = c;
        Health = hp;
        _netSpeed = speed;
        UnpackAmmo(ammo);
    }

    /// <summary>Munitions totales des deux armes de bord (16 et 15 bits).</summary>
    private int PackAmmo()
    {
        var a0 = Arms.Length > 0 ? Mathf.Clamp(Arms[0].Total, 0, 0xFFFF) : 0;
        var a1 = Arms.Length > 1 ? Mathf.Clamp(Arms[1].Total, 0, 0x7FFF) : 0;
        return a0 | (a1 << 16);
    }

    private void UnpackAmmo(int v)
    {
        if (Arms.Length > 0) Arms[0].SetTotal(v & 0xFFFF);
        if (Arms.Length > 1) Arms[1].SetTotal((v >> 16) & 0x7FFF);
    }

    /// <summary>Interpolation de l'état réseau quand on ne conduit pas.</summary>
    protected abstract void RemoteTick(float dt);

    /// <summary>Animations communes à tous les pairs (rotor, poussière…).</summary>
    protected virtual void VisualTick(float dt) { }

    protected void InterpolatePosition(float dt)
    {
        GlobalPosition = GlobalPosition.DistanceSquaredTo(_netPos) > 100 ? _netPos : GlobalPosition.Lerp(_netPos, Mathf.Clamp(dt * 12f, 0, 1));
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (Delivering)
        {
            VisualTick(dt);
            _prompt.Visible = false;
            return;
        }
        if (!IsLocalDriver && !Wrecked) RemoteTick(dt);
        VisualTick(dt);
        // Invite « monter à bord » pour le joueur local à proximité.
        var lp = _world.LocalPlayer;
        var near = !Wrecked && Driver == 0 && lp != null && !lp.Downed && lp.Vehicle == null && lp.GlobalPosition.DistanceTo(GlobalPosition) < InteractRadius;
        _prompt.Visible = near;
        if (near) _prompt.Text = $"[{UiKit.KeyName("interact")}] {PromptText}";
    }

    /// <summary>
    /// Fin de la livraison (tous les pairs) : le véhicule est posé à <paramref name="pos"/>, qui devient son point
    /// de réarmement ; il se comporte ensuite exactement comme un véhicule garé.
    /// </summary>
    public virtual void OnDelivered(Vector3 pos)
    {
        Delivering = false;
        GlobalPosition = pos;
        Velocity = Vector3.Zero;
        _netPos = pos;
        _home = pos;
    }

    // ---------- Munitions, réarmement ----------

    /// <summary>Recharge manuelle (touche de rechargement à bord).</summary>
    public void ManualReload()
    {
        var any = false;
        foreach (var w in Arms) any |= w.StartReload();
        if (any) Sfx.Ui("reload", -4f, 0.8f);
    }

    /// <summary>
    /// Avance les rechargements et réarme le véhicule arrêté à son point de départ : dotation limitée par sortie,
    /// comme les véhicules EDF ; retour à la base pour refaire le plein (~8 s pour une dotation complète).
    /// </summary>
    protected void AmmoTick(float dt)
    {
        foreach (var w in Arms)
            if (w.Tick(dt)) Sfx.Ui("reload", -6f, 0.7f);
        var need = false;
        foreach (var w in Arms) need |= w.Total < w.MaxTotal;
        if (!need || !AtRearmPoint)
        {
            if (Rearming && !need) _world.Hud?.Toast("Réarmement terminé.");
            Rearming = false;
            _rearmT = 0;
            return;
        }
        if (!Rearming) { Rearming = true; _world.Hud?.Toast("Réarmement en cours…"); }
        _rearmT += dt;
        if (_rearmT < 0.5f) return;
        _rearmT = 0;
        foreach (var w in Arms) w.Rearm(1f / 16f);
        Sfx.Ui("reload", -12f, 1.25f);
    }

    // ---------- Chocs ----------

    /// <summary>
    /// Dégâts de collision après un MoveAndSlide : vitesse d'impact = composante de la vitesse d'avant le choc
    /// dirigée contre la surface touchée. Sous le seuil (contact doux), rien ; au-delà, dégâts croissants
    /// (linéaire + quadratique) : un choc violent peut détruire le véhicule. Calcul par le conducteur ;
    /// le blindage voyage dans l'état réseau.
    /// </summary>
    /// <returns>Excès de vitesse d'impact au-dessus du seuil (0 si contact doux).</returns>
    protected float CollisionDamage(Player p, Vector3 before, float wallThreshold, float floorThreshold, float perMs, float quad, float dt)
    {
        _impactCd -= dt;
        var bestExcess = 0f;
        var bestPos = GlobalPosition;
        var bestN = Vector3.Up;
        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var c = GetSlideCollision(i);
            var n = c.GetNormal();
            var into = -before.Dot(n);
            if (into <= 0) continue;
            // Contact franc avec un accessoire destructible : le véhicule le renverse ou l'écrase (hôte).
            if (into > 1.5f) _world.Decor?.LocalContact(c.GetCollider(), c.GetColliderShapeIndex(), CrushStrength, GlobalPosition);
            var excess = into - (n.Y > 0.7f ? floorThreshold : wallThreshold);
            if (excess > bestExcess) { bestExcess = excess; bestPos = c.GetPosition(); bestN = n; }
        }
        if (bestExcess <= 0 || _impactCd > 0) return 0f;
        _impactCd = 0.2f;
        var dmg = bestExcess * perMs + bestExcess * bestExcess * quad;
        Health -= dmg;
        var k = Mathf.Clamp(bestExcess / 20f, 0, 1);
        p?.AddShake(Mathf.Lerp(0.25f, 1.1f, k));
        Sfx.Ui("vehicle_crash", Mathf.Lerp(-16f, -7f, k), (float)GD.RandRange(0.9, 1.1)); // discret : un frottement contre un mur ne doit pas tonner
        Effects.Sparks(_world, bestPos, bestN);
        if (bestExcess > 5f) Effects.Sparks(_world, bestPos + bestN * 0.4f, bestN);
        if (bestExcess > 10f) Effects.Smoke(_world, bestPos, 1.4f, 1.6f);
        if (OS.IsDebugBuild() && Player.Bot) GD.Print($"[vehicule] choc {Name} : excès {bestExcess:0.0} m/s, dégâts {dmg:0}, blindage {Health:0}");
        OnCollisionImpact(bestExcess);
        return bestExcess;
    }

    /// <summary>Force d'écrasement du décor (voir <see cref="Destructibles"/>) : 2 renverse le mobilier, 3 aplatit aussi les voitures.</summary>
    protected virtual int CrushStrength => 2;

    /// <summary>Réaction propre au véhicule après un choc (alarme du cockpit…).</summary>
    protected virtual void OnCollisionImpact(float excess) { }

    /// <summary>Envoie la destruction une seule fois quand le blindage est épuisé (conducteur).</summary>
    protected void CheckWrecked()
    {
        if (Health > 0 || _wreckSent || Wrecked) return;
        _wreckSent = true;
        Net.I.SendVehicleWrecked(Id);
    }

    /// <summary>Le véhicule ignore-t-il un coup venant de ce point ? (ex. morsure d'un insecte au sol sous l'hélicoptère)</summary>
    public virtual bool IgnoresDamageFrom(Vector3 from) => false;

    /// <summary>Destruction (appelée chez tous les pairs).</summary>
    public abstract void Wreck();

    /// <summary>Un tir de l'arme de bord a été annoncé par le conducteur distant (sons en boucle).</summary>
    public virtual void OnRemoteFire() { }

    protected void BlackenMeshes()
    {
        var burnt = Mats.Painted(new Color(0.08f, 0.07f, 0.06f), 0.95f);
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).MaterialOverride = burnt;
    }
}
