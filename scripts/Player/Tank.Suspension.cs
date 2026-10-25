using Godot;

namespace Rempart;

/// <summary>
/// Suspension et franchissement du char :
/// - quatre sondes aux coins des chenilles donnent la hauteur et l'inclinaison du char (toutes cartes : trottoirs, gravats,
///   épaves aplaties, relief) ; la boîte de collision est relevée de <see cref="Clearance"/> : bordures et obstacles bas
///   passent dessous, le char les gravit ;
/// - la partie suspendue (caisse et tourelle) repose sur des ressorts amortis : cabrage à l'accélération, plongée au freinage,
///   roulis en virage, à-coups des bosses, recul du canon ; le train de roulement reste collé au sol ;
/// - deux pots d'échappement : voile léger au ralenti, bouffées noires à l'accélération.
/// </summary>
public partial class Tank
{
    /// <summary>Garde au sol : hauteur du bas de la boîte de collision (obstacles franchissables en dessous).</summary>
    public const float Clearance = 0.55f;
    // Sondes à l'aplomb du nez et de l'arrière des chenilles : un obstacle bas est « vu » avant que la caisse ne le touche.
    private const float ProbeX = 1.43f, ProbeFront = 3.75f, ProbeRear = 3.5f;

    private Node3D _sprung;
    private float _sp, _spV, _sr, _srV, _sh, _shV;
    private float _lastSpeed, _accelSm, _lastGround = float.NaN;
    private readonly float[] _probe = new float[5];
    private GpuParticles3D[] _haze, _puffs;
    /// <summary>Hauteur d'origine visée pour franchir un obstacle bas touché de face (décroît une fois passé).</summary>
    private float _stepTarget = float.NegativeInfinity, _stepT;
    public string StepDebug = "";

    /// <summary>
    /// Après le déplacement : un obstacle touché de face (bordure haute, muret, épave, sacs de sable) dont le dessus est à moins
    /// de 1,1 m est escaladé — la caisse est soulevée juste au-dessus ; plus haut, c'est un mur et le char s'y arrête.
    /// </summary>
    private void StepOver()
    {
        _stepT -= (float)GetPhysicsProcessDeltaTime();
        if (_stepT <= 0) _stepTarget = float.NegativeInfinity;
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return;
        var y = GlobalPosition.Y;
        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var c = GetSlideCollision(i);
            var n = c.GetNormal();
            if (Mathf.Abs(n.Y) > 0.6f || c.GetPosition().Y - y > 1.15f) continue;
            // Dessus de l'obstacle juste derrière le point de contact.
            var at = c.GetPosition() - new Vector3(n.X, 0, n.Z).Normalized() * 0.06f;
            var q = PhysicsRayQueryParameters3D.Create(new Vector3(at.X, y + 1.6f, at.Z), new Vector3(at.X, y - 0.2f, at.Z), 1);
            q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            var hit = space.IntersectRay(q);
            if (hit.Count == 0) { StepDebug = "pas de dessus"; continue; }
            var top = ((Vector3)hit["position"]).Y;
            StepDebug = $"dessus {top - y:0.00} m, cible {_stepTarget:0.00}, y {y:0.00}, vy {Velocity.Y:0.0}";
            // Mur, ou poutre au-dessus d'un vide (contact bien plus haut que le dessus trouvé) : pas d'escalade.
            if (top - y > 1.1f || c.GetPosition().Y > top + 0.15f) continue;
            _stepTarget = Mathf.Max(_stepTarget, top - Clearance + 0.06f);
            _stepT = 0.6f;
        }
    }

    /// <summary>Partie suspendue (caisse + tourelle) sous un pivot propre, pots d'échappement.</summary>
    private void BuildSuspension(Node3D model)
    {
        var pivot = new Vector3(0, 1.0f, 0);
        _sprung = new Node3D { Name = "Sprung", Position = pivot };
        model.AddChild(_sprung);
        foreach (var name in new[] { "Hull", "Turret" })
        {
            if (model.FindChild(name, false, false) is not Node3D n) continue;
            model.RemoveChild(n);
            _sprung.AddChild(n);
            n.Position -= pivot;
        }
        _haze = new GpuParticles3D[2];
        _puffs = new GpuParticles3D[2];
        for (var i = 0; i < 2; i++)
        {
            var at = new Vector3(i == 0 ? -1.25f : 1.25f, 1.27f, -3.86f) - pivot;
            _haze[i] = Effects.ExhaustEmitter(false);
            _puffs[i] = Effects.ExhaustEmitter(true);
            _haze[i].Position = _puffs[i].Position = at;
            _haze[i].Emitting = _puffs[i].Emitting = false;
            _sprung.AddChild(_haze[i]);
            _sprung.AddChild(_puffs[i]);
        }
    }

    /// <summary>
    /// Hauteur du sol sous les quatre coins (repère de cap seul : l'inclinaison du visuel ne rétroagit pas) :
    /// [0] avant gauche, [1] avant droit, [2] arrière gauche, [3] arrière droit, [4] milieu du nez (épave, bloc entre les chenilles).
    /// Faux si aucune sonde ne touche.
    /// </summary>
    private bool ProbeGround()
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return false;
        var basis = new Basis(Vector3.Up, Rotation.Y);
        var p = GlobalPosition;
        var any = false;
        var ex = new Godot.Collections.Array<Rid> { GetRid() };
        for (var i = 0; i < 5; i++)
        {
            var local = i == 4 ? new Vector3(0, 0, ProbeFront) : new Vector3(i % 2 == 0 ? -ProbeX : ProbeX, 0, i < 2 ? ProbeFront : -ProbeRear);
            var c = p + basis * local;
            var q = PhysicsRayQueryParameters3D.Create(c + Vector3.Up * (Clearance + 0.9f), c + Vector3.Down * 3f, 1);
            q.Exclude = ex;
            var hit = space.IntersectRay(q);
            // Au-delà de 1,1 m au-dessus du char : un mur, pas une marche (le char ne l'escalade pas).
            if (hit.Count > 0) { _probe[i] = Mathf.Min(((Vector3)hit["position"]).Y, p.Y + 1.1f); any = true; }
            else _probe[i] = p.Y - 3f;
        }
        return any;
    }

    /// <summary>Conducteur : vitesse verticale qui suit le sol sondé (montée franche des marches, chute au-delà d'un rebord).</summary>
    private float GroundVelocityY(float dt)
    {
        if (!ProbeGround()) return Velocity.Y - 24f * dt;
        // Nez : la plus haute des sondes avant (un obstacle sous le milieu de la caisse soulève l'avant).
        var front = Mathf.Max((_probe[0] + _probe[1]) * 0.5f, _probe[4]);
        var target = (front + (_probe[2] + _probe[3]) * 0.5f) * 0.5f;
        // Appui sur les points les plus hauts : une chenille sur un trottoir porte ce côté.
        target = Mathf.Max(target, Mathf.Max((_probe[0] + _probe[2]) * 0.5f, (_probe[1] + _probe[3]) * 0.5f) - 0.08f);
        target = Mathf.Max(target, _stepTarget);
        var dy = target - GlobalPosition.Y;
        if (dy < -0.7f) return Velocity.Y - 24f * dt;         // rebord : le char tombe
        return Mathf.Clamp(dy * 12f, -9f, 6f);
    }

    /// <summary>Pose du train et de la partie suspendue (tous les pairs).</summary>
    private void Suspension(float dt, float yawRate)
    {
        if (dt <= 0f || _sprung == null) return;
        // Inclinaison rigide du char sur le sol sondé (trottoir sous une chenille, pente, gravats).
        if (!Wrecked && ProbeGround())
        {
            var front = Mathf.Max((_probe[0] + _probe[1]) * 0.5f, _probe[4]);
            var rear = (_probe[2] + _probe[3]) * 0.5f;
            var left = (_probe[0] + _probe[2]) * 0.5f;
            var right = (_probe[1] + _probe[3]) * 0.5f;
            var n = new Vector3((left - right) / (2f * ProbeX), 1f, -(front - rear) / (ProbeFront + ProbeRear)).Normalized();
            if (Mathf.Abs(front - rear) > 2.5f || Mathf.Abs(left - right) > 2f) n = Vector3.Up; // sonde sur un toit, un mur
            _tilt.Quaternion = _tilt.Quaternion.Slerp(new Quaternion(Vector3.Up, n), Mathf.Clamp(dt * 9f, 0, 1));
            // Bosse : quand le sol monte sous le char, la caisse est poussée vers le haut puis rebondit.
            var g = (front + rear) * 0.5f;
            if (!float.IsNaN(_lastGround)) _shV += Mathf.Clamp((g - _lastGround) / dt, -6f, 6f) * 0.35f;
            _lastGround = g;
        }
        // Accélération longitudinale lissée (les pairs distants reçoivent la vitesse par à-coups).
        var accel = (_speed - _lastSpeed) / dt;
        _lastSpeed = _speed;
        _accelSm = Mathf.Lerp(_accelSm, accel, 1f - Mathf.Exp(-dt * 6f));
        // Cibles : cabrage à l'accélération (nez qui monte : tangage négatif), roulis vers l'extérieur du virage.
        var pitchT = Mathf.Clamp(-_accelSm * 0.0075f, -0.06f, 0.06f);
        var rollT = Mathf.Clamp(_speed * yawRate * 0.0035f, -0.05f, 0.05f);
        // Ressorts amortis (raideur, amortissement) : la caisse oscille légèrement avant de se stabiliser.
        Spring(ref _sp, ref _spV, pitchT, 55f, 7f, dt);
        Spring(ref _sr, ref _srV, rollT, 60f, 7f, dt);
        Spring(ref _sh, ref _shV, 0f, 80f, 9f, dt);
        _sh = Mathf.Clamp(_sh, -0.12f, 0.12f);
        _sprung.Position = new Vector3(0, 1.0f + _sh, 0);
        _sprung.Rotation = new Vector3(_sp, 0, _sr);
        Exhaust(dt);
    }

    public float Speed => _speed;
    /// <summary>Tangage, roulis (degrés) et débattement (cm) de la caisse, pour le journal de test.</summary>
    public string SprungDebug => $"tangage {Mathf.RadToDeg(_sp):0.0}° roulis {Mathf.RadToDeg(_sr):0.0}° débattement {_sh * 100f:0} cm";

    private static void Spring(ref float x, ref float v, float target, float k, float c, float dt)
    {
        v += ((target - x) * k - v * c) * dt;
        x += v * dt;
    }

    /// <summary>Recul du coup de canon : la caisse bascule à l'opposé de la tourelle.</summary>
    private void RecoilKick()
    {
        _spV -= Mathf.Cos(_turretYaw) * 0.9f;
        _srV += Mathf.Sin(_turretYaw) * 0.7f;
    }

    /// <summary>Échappement : moteur en marche avec un conducteur ; densité des bouffées noires selon l'effort (accélération, vitesse).</summary>
    private void Exhaust(float dt)
    {
        var on = Driver != 0 && !Wrecked && !Delivering;
        var effort = Mathf.Clamp(Mathf.Max(_accelSm, 0f) / 6f + Mathf.Abs(_speed) / 40f, 0f, 1f);
        for (var i = 0; i < 2; i++)
        {
            if (_haze[i].Emitting != on) _haze[i].Emitting = on;
            var puff = on && effort > 0.08f;
            if (_puffs[i].Emitting != puff) _puffs[i].Emitting = puff;
            _puffs[i].AmountRatio = Mathf.Lerp(0.15f, 1f, effort);
        }
    }
}
