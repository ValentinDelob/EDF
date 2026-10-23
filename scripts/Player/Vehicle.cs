using Godot;

namespace Rempart;

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
    protected World _world;
    protected Label3D _prompt;

    // Dernier état reçu (ou état courant si l'on conduit).
    protected Vector3 _netPos, _netRot;
    protected float _netA, _netB, _netSpeed;
    private float _netTimer;

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

    // ---------- HUD (bloc de l'arme active) ----------

    /// <summary>Grand chiffre (munitions / état de l'arme principale).</summary>
    public abstract string HudAmmo { get; }
    /// <summary>Libellé à droite du grand chiffre.</summary>
    public abstract string HudMag { get; }
    /// <summary>Progression du rechargement de l'arme principale (1 = prête).</summary>
    public abstract float HudReady { get; }
    /// <summary>Ligne d'information secondaire (autres armes, commandes).</summary>
    public abstract string HudOther { get; }
    /// <summary>Sous-titre (blindage, altitude…).</summary>
    public virtual string HudSub => $"Blindage {Mathf.CeilToInt(Mathf.Max(0, Health))}";

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
    /// Diffuse l'état à 20 Hz. <paramref name="a"/> et <paramref name="b"/> sont propres au véhicule
    /// (tourelle et canon du char, régime du rotor de l'hélicoptère).
    /// </summary>
    protected void BroadcastState(float dt, Vector3 rot, float a, float b, float speed)
    {
        // Le conducteur garde son propre état à jour : à la descente, le véhicule reste où il est.
        _netPos = GlobalPosition;
        _netRot = rot;
        _netA = a;
        _netB = b;
        _netSpeed = speed;
        _netTimer -= dt;
        if (_netTimer > 0) return;
        _netTimer = 0.05f;
        Net.I.SendVehicleState(Id, GlobalPosition, rot, a, b, Health, speed);
    }

    public void ApplyState(Vector3 pos, Vector3 rot, float a, float b, float hp, float speed)
    {
        _netPos = pos;
        _netRot = rot;
        _netA = a;
        _netB = b;
        Health = hp;
        _netSpeed = speed;
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
        if (!IsLocalDriver && !Wrecked) RemoteTick(dt);
        VisualTick(dt);
        // Invite « monter à bord » pour le joueur local à proximité.
        var lp = _world.LocalPlayer;
        var near = !Wrecked && Driver == 0 && lp != null && !lp.Downed && lp.Vehicle == null && lp.GlobalPosition.DistanceTo(GlobalPosition) < InteractRadius;
        _prompt.Visible = near;
        if (near) _prompt.Text = $"[{UiKit.KeyName("interact")}] {PromptText}";
    }

    /// <summary>Le véhicule ignore-t-il un coup venant de ce point ? (ex. morsure d'un insecte au sol sous l'hélicoptère)</summary>
    public virtual bool IgnoresDamageFrom(Vector3 from) => false;

    /// <summary>Destruction (appelée chez tous les pairs).</summary>
    public abstract void Wreck();

    protected void BlackenMeshes()
    {
        var burnt = Mats.Painted(new Color(0.08f, 0.07f, 0.06f), 0.95f);
        foreach (var n in FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).MaterialOverride = burnt;
    }
}
