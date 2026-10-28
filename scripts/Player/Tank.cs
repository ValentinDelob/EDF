using Godot;

namespace Rempart;

/// <summary>
/// Char de combat pilotable (simulation par le conducteur et réseau : voir <see cref="Vehicle"/> ; modèle : Tank.Model.cs).
/// </summary>
public partial class Tank : Vehicle
{
    private Node3D _hull, _turret, _barrel, _muzzle;
    /// <summary>Pivot du visuel (caisse et tourelle) : incliné selon la pente du sol (relief), à plat sur les cartes plates.</summary>
    private Node3D _tilt;
    private float _turretYaw, _barrelPitch;
    private float _speed;
    private float _mgCd;

    public const float CannonDamage = 380f;
    public const float CannonRadius = 10f;

    // Dotation par sortie (réarmement au point de départ, à l'arrêt) :
    // canon 30 obus (1 chargé, 1,6 s de chargement), mitrailleuse coaxiale 5 bandes de 250.
    public readonly VehicleWeapon Cannon = new("Canon principal", "Obus", HudWeaponIcon.Tank, 1, 30, 1.6f);
    public readonly VehicleWeapon Coax = new("Mitrailleuse coaxiale", "Balles", HudWeaponIcon.Gatling, 250, 1250, 4f);

    public static Tank Create(World w, int id, Vector3 pos, float yaw)
    {
        var t = new Tank { Name = $"Tank{id}" };
        // Les appelants posent les véhicules 0,3 m au-dessus du sol (hélicoptère) : le char, lui, repose sur ses chenilles.
        t.InitCommon(w, id, pos - Vector3.Up * 0.3f, yaw, 1600);
        t.Arms = new[] { t.Cannon, t.Coax };
        t.Build();
        return t;
    }

    private void Build()
    {
        _tilt = new Node3D { Name = "Tilt" };
        AddChild(_tilt);
        BuildModel();
        // Hauteur pilotée par la suspension (sondes) : pas de notion de sol ni d'accroche du corps physique.
        MotionMode = MotionModeEnum.Floating;
        // Boîte relevée de la garde au sol : bordures, gravats et murets bas passent dessous (Tank.Suspension.cs).
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.6f, 2.2f - Clearance, 7.4f) }, Position = new Vector3(0, (2.2f + Clearance) * 0.5f, 0) });
        _prompt.Position = new Vector3(0, 3.4f, 0);
        CastShadows();
    }

    public string BlockDebug = "";
    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;
    public override Vector3 SeatPosition => GlobalPosition + Vector3.Up * 2.2f;
    public override string DisplayName => "Char de combat";
    public override string PromptText => "Monter dans le char";
    public override string EnterHint =>
        $"Char de combat : clic gauche canon, clic droit mitrailleuse, [{UiKit.KeyName("reload")}] recharger. Munitions limitées : arrêtez-vous au point de départ pour réarmer.";
    public override string WreckedHint => "Char détruit ! Évacuation.";
    protected override int CrushStrength => 5; // écrase voitures, camions, blocs de béton ; pousse les conteneurs

    /// <summary>Simulation par le conducteur local : déplacement, tourelle, tir.</summary>
    public override void DriveTick(Player p, float dt, float camYaw, float camPitch)
    {
        if (Wrecked) return;
        var input = _world.Paused ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var turn = -input.X * 1.1f * dt * (Mathf.Abs(_speed) > 0.5f || input.Y == 0 ? 1f : 0.6f);
        Rotation = new Vector3(0, Rotation.Y + turn, 0);
        var target = -input.Y * (input.Y < 0 ? 15f : 7f);
        if (Input.IsActionPressed("sprint") && input.Y < 0) target = 20f;
        _speed = Mathf.MoveToward(_speed, target, (Mathf.Abs(target) > Mathf.Abs(_speed) ? 7f : 12f) * dt);
        var fwd = GlobalBasis.Z;
        var v = fwd * _speed;
        v.Y = GroundVelocityY(dt);
        // Montée sur une marche ou un obstacle bas (dessus vérifié sous 1,1 m) : levée directe — collée à l'obstacle,
        // ou l'effleurant déjà, la caisse serait retenue par le glissement.
        if (v.Y > 0f) { GlobalPosition += Vector3.Up * v.Y * dt; v.Y = 0f; }
        Velocity = v;
        MoveAndSlide();
        StepOver();
        if (OS.IsDebugBuild() && Player.Bot == false && GetSlideCollisionCount() > 0 && Screenshot.CmdArg("tank-drive") != null)
        {
            var c = GetSlideCollision(0);
            var col = c.GetCollider() as Node;
            var id = _world.Decor?.FromHit(c.GetCollider(), c.GetColliderShapeIndex()) ?? -1;
            var kind = id >= 0 ? _world.Decor.Get(id).Kind.ToString() : "non destructible";
            BlockDebug = $"{col?.Name} ({kind}) à y={c.GetPosition().Y - GlobalPosition.Y:0.00} n={c.GetNormal()}";
        }
        // Éperonnage : un choc contre un mur à pleine vitesse abîme légèrement le char (seuil 8 m/s) et le freine.
        if (CollisionDamage(p, v, 8f, 12f, 18f, 0.4f, dt) > 0) _speed *= 0.25f;
        CheckWrecked();
        // Écrase les petits insectes sur son passage.
        if (Mathf.Abs(_speed) > 4f) _world.TankCrush(this, GlobalPosition + fwd * 3.6f * Mathf.Sign(_speed), 3f, Mathf.Abs(_speed) * 6f * dt * 10f);
        // Renverse le mobilier, aplatit voitures et fourgons (signalé à l'hôte).
        if (Mathf.Abs(_speed) > 1.5f) _world.Decor?.LocalCrush(GlobalPosition + fwd * 3.2f * Mathf.Sign(_speed), 2.2f, CrushStrength, GlobalPosition);

        // Tourelle : suit la caméra.
        var localYaw = Mathf.Wrap(camYaw + Mathf.Pi - Rotation.Y, -Mathf.Pi, Mathf.Pi);
        _turretYaw = Mathf.LerpAngle(_turretYaw, localYaw, Mathf.Clamp(dt * 2.2f, 0, 1));
        _barrelPitch = Mathf.Lerp(_barrelPitch, Mathf.Clamp(camPitch + 0.08f, -0.15f, 0.55f), Mathf.Clamp(dt * 4f, 0, 1));
        ApplyPose(dt);

        _mgCd -= dt;
        AmmoTick(dt);
        if (!_world.Paused && Input.IsActionJustPressed("reload")) ManualReload();
        if (!_world.Paused && Input.IsActionPressed("fire"))
        {
            if (Cannon.TryFire()) FireCannon(p);
            else if (Cannon.Empty && Input.IsActionJustPressed("fire")) Sfx.Ui("empty");
        }
        if (!_world.Paused && Input.IsActionPressed("aim") && _mgCd <= 0)
        {
            if (Coax.TryFire()) FireMg(p);
            else if (Coax.Empty && Input.IsActionJustPressed("aim")) Sfx.Ui("empty");
        }

        BroadcastState(dt, Rotation, _turretYaw, _barrelPitch, 0f, _speed);
    }

    private void ApplyPose(float dt)
    {
        _turret.Rotation = new Vector3(0, _turretYaw, 0);
        _barrel.Rotation = new Vector3(-_barrelPitch, 0, 0);
        Suspension(dt, AnimateTracks(dt));
    }

    private void FireCannon(Player p)
    {
        var dir = _muzzle.GlobalBasis.Z;
        var from = _muzzle.GlobalPosition;
        Projectile.Spawn(_world, ProjectileKind.Shell, from, dir * 140f, true, CannonDamage * p.DamageMul * (1f + 0.25f * (int)_world.Difficulty), CannonRadius)
            .IgnoreBody(this);
        Net.I.SendShotFx(10 + (int)ProjectileKind.Shell, from, from + dir * 140f);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.6f, 0.2f), 1.6f, dir);
        Effects.Smoke(_world, from, 2f, 1.5f);
        // Souffle du coup de canon : poussière soulevée au sol sous la bouche.
        var ground = World.GroundAt(from.X, from.Z);
        if (from.Y - ground < 4f) Effects.Dust(_world, new Vector3(from.X, ground, from.Z) + dir with { Y = 0 } * 2f, new Vector3(5f, 1.2f, 5f));
        Sfx.Ui("explosion_small", 0f, 0.7f);
        p.AddShake(0.35f);
        Recoil();
        RecoilKick();
    }

    private void FireMg(Player p)
    {
        _mgCd = 0.09f;
        var from = _turret.GlobalPosition + _turret.GlobalBasis * new Vector3(0.5f, 0.9f, 0.3f);
        var aim = p.AimPoint(200f, out var col);
        var dir = (aim - from).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * 200f, 1u | (1u << 2));
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var end = from + dir * 200f;
        if (hit.Count > 0)
        {
            end = (Vector3)hit["position"];
            if (hit["collider"].AsGodotObject() is Enemy e && e.State != EnemyState.Dead)
            {
                var dmg = 14f * p.DamageMul * (1f + 0.25f * (int)_world.Difficulty);
                var weak = (e.IsMachine || e.IsMothership) && e.IsWeakShape(hit["shape"].AsInt32()); // soute ouverte, noyau, genoux, cœur du vaisseau-mère
                Net.I.SendDamageEnemy(e.Id, dmg, end, weak);
                _world.LocalHitFx(e, end, true, dmg, weak);
            }
            else
            {
                Effects.Sparks(_world, end, (Vector3)hit["normal"]);
                Effects.BulletHole(_world, end, (Vector3)hit["normal"], hit["collider"].AsGodotObject(), 0.3f);
                _world.Decor?.LocalHit(hit, 14f * p.DamageMul, from);
            }
        }
        Effects.Tracer(_world.FxRoot, from, end, new Color(1f, 0.75f, 0.35f), 0.05f);
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.7f, 0.3f), 0.3f, dir);
        Sfx.Ui("minigun", -6f);
        Net.I.SendShotFx(0, from, end);
    }

    protected override void RemoteTick(float dt)
    {
        var k = Mathf.Clamp(dt * 12f, 0, 1);
        InterpolatePosition(dt);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netRot.Y, k), 0);
        _turretYaw = Mathf.LerpAngle(_turretYaw, _netA, k);
        _barrelPitch = Mathf.Lerp(_barrelPitch, _netB, k);
        _speed = _netSpeed;
        ApplyPose(dt);
    }

    public override void Wreck()
    {
        if (Wrecked) return;
        Wrecked = true;
        Driver = 0;
        Effects.Explosion(_world, GlobalPosition + Vector3.Up * 1.5f, 12f, 0);
        BlackenMeshes();
        _turret.Rotation = new Vector3(0.2f, _turretYaw + 0.6f, 0.15f);
        Effects.Smoke(_world, GlobalPosition + Vector3.Up * 2f, 3f, 6f);
    }
}
