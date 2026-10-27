using Godot;

namespace Rempart;

/// <summary>Coéquipier distant : affichage interpolé à partir des états réseau reçus.</summary>
public partial class RemotePlayer : Node3D
{
    public long PeerId;
    /// <summary>Arme d'EDF 5 tenue (vide : arme procédurale du style transmis dans l'état).</summary>
    public string GunId;

    /// <summary>« arme|bouclier » : arme tenue et bouclier du Fencer au bras gauche (vide : aucun).</summary>
    public void SetGun(string ids)
    {
        var parts = (ids ?? "").Split('|');
        var id = parts[0];
        GunId = Weapons.Exists(id) ? id : null;
        if (GunId != null) Model.SetWeapon(Weapons.Get(GunId));
        var sh = parts.Length > 1 && Weapons.Exists(parts[1]) ? Weapons.Get(parts[1]).Spec : null;
        Model.SetShield(sh?.Kind == Edf5Kind.Shield ? sh : null);
    }
    public string PlayerName;
    public SoldierClass Class;
    public SoldierModel Model;
    public float Health = 1;
    public float MaxHealth = 1;
    public bool Downed;
    /// <summary>Fourmi rouge qui tient ce coéquipier (null sinon).</summary>
    public Enemy GrabbedBy;
    private Vector3 _netPos;
    private Vector3 _netVel;
    private float _netYaw;
    private float _pitch;
    private int _flags;
    private Label3D _label;
    private float _recoil;

    public static RemotePlayer Create(PlayerInfo info, Vector3 pos)
    {
        var r = new RemotePlayer { PeerId = info.Id, PlayerName = info.Name, Class = info.Class, MaxHealth = info.MaxArmor, Health = info.MaxArmor };
        r.Model = SoldierModel.Create(info.Class);
        r.AddChild(r.Model);
        r._label = new Label3D
        {
            Text = info.Name,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 42,
            OutlineSize = 10,
            Modulate = new Color(0.6f, 1f, 0.7f),
            Position = new Vector3(0, 2.4f, 0),
            NoDepthTest = true,
            FixedSize = false,
            PixelSize = 0.006f,
        };
        r.AddChild(r._label);
        r.Position = pos;
        r._netPos = pos;
        return r;
    }

    /// <summary>Fencer en garde de bouclier (renvoi des projectiles chez l'hôte) et direction de la garde.</summary>
    public bool Reflecting => (_flags & (1 << 12)) != 0;
    public Vector3 GuardForward => new Basis(Vector3.Up, _netYaw) * Vector3.Back;

    public void ApplyState(Vector3 pos, float yaw, float pitch, int flags, float hp, Vector3 vel)
    {
        _netPos = pos;
        _netYaw = yaw;
        _pitch = pitch;
        _flags = flags;
        _netVel = vel;
        Health = hp;
        Downed = (flags & 32) != 0;
        Model.Visible = (flags & 128) == 0;
        if (string.IsNullOrEmpty(GunId)) Model.SetWeaponStyle((WeaponStyle)((flags >> 8) & 7));
        Model.Reloading = (flags & (1 << 11)) != 0;
        Model.ShieldUp = (flags & (1 << 12)) != 0;
        if ((flags & 16) != 0) _recoil = 0.6f;
    }

    private JetpackAudio _jet;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        // Extrapolation légère + lissage.
        _netPos += _netVel * dt;
        var grabbed = GrabbedBy != null && IsInstanceValid(GrabbedBy);
        if (!grabbed) GrabbedBy = null;
        // Saisi : la position vient des mandibules de la fourmi affichée ici (World.UpdateGrabs), pas du réseau.
        var p = Position;
        if (!grabbed) Position = p.DistanceSquaredTo(_netPos) > 100 ? _netPos : p.Lerp(_netPos, Mathf.Clamp(dt * 14f, 0, 1));
        Model.Rotation = new Vector3(0, Mathf.LerpAngle(Model.Rotation.Y, _netYaw, Mathf.Clamp(dt * 12f, 0, 1)), 0);
        var speed = grabbed ? 0f : new Vector3(_netVel.X, 0, _netVel.Z).Length();
        _recoil = Mathf.MoveToward(_recoil, 0, dt * 4f);
        Model.Grabbed = grabbed;
        Model.Motion = grabbed ? Vector3.Zero : _netVel;
        Model.Animate(dt, speed, _pitch, !grabbed && (_flags & 4) != 0, (_flags & 64) != 0, Downed, (_flags & 8) != 0, _recoil);
        if ((_flags & 64) != 0 || _jet != null)
        {
            if (_jet == null) { _jet = new JetpackAudio { Position = new Vector3(0, 1.2f, 0) }; AddChild(_jet); }
            _jet.Tick((_flags & 64) != 0 && !Downed, _netVel.Y, dt);
        }
        _label.Text = Downed ? $"{PlayerName}  [À TERRE — E]" : grabbed ? $"{PlayerName}  [SAISI]" : PlayerName;
        _label.Modulate = Downed || grabbed ? new Color(1f, 0.4f, 0.3f) : new Color(0.6f, 1f, 0.7f);
    }

    public Vector3 MuzzlePosition => Model.Muzzle.GlobalPosition;
}
