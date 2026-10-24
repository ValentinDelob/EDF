using Godot;

namespace Rempart;

/// <summary>
/// Animation procédurale des aliens humanoïdes : marche et course à pieds posés (cinématique inverse plane, aucun
/// glissement), visée épaulée (l'arme suit la cible, les mains la rejoignent par cinématique inverse à deux os), mise à
/// genou derrière un abri, sursaut à l'impact, recul du tir, vol (volant : jambes pendantes, inclinaison, flammes),
/// bouclier du garde (levé, abaissé, charge, coup), et chute de mort façon poupée de chiffon.
/// Les consignes viennent d'<see cref="Enemy"/> (hôte comme clients) : <see cref="AlienCrouch"/>, <see cref="AlienAiming"/>...
/// </summary>
public partial class BugModel
{
    /// <summary>Consigne d'accroupissement derrière un abri (0..1).</summary>
    public float AlienCrouch;
    /// <summary>Consigne de visée épaulée (0..1).</summary>
    public float AlienAiming;
    /// <summary>Garde : charge (course, bouclier en avant).</summary>
    public bool AlienCharging;
    /// <summary>Garde : bouclier abaissé (récupération après une charge, sursaut).</summary>
    public bool AlienShieldDown;

    private float _aT, _aCycle, _aCrouchB, _aAimB, _aRunB, _aRecoil, _aFlinch, _aFlinchSide = 1f, _aBash, _aShieldDownB, _aChargeB;
    private float _aYaw, _aPitch, _aLean, _aBank;
    private Vector3 _aAimWorld, _aPrevPos, _aVel;
    private bool _aHasAim, _aPrevOk;
    private float _aLegYaw;

    /// <summary>Vitesse de marche imposée (vitrine figée) ; négative : déduite du déplacement réel.</summary>
    public float AlienSpeedOverride = -1f;

    // Prises des mains (repère de la main : -Y = doigts ; x = axe de la prise), d'après le soldat.
    private static Basis AHand(Vector3 x, Vector3 f)
    {
        x = x.Normalized();
        var y = -(f - x * f.Dot(x)).Normalized();
        return new Basis(x, y, x.Cross(y));
    }

    private static readonly Basis APistolGripR = AHand(new Vector3(0, -0.94f, -0.34f), new Vector3(0.45f, -0.25f, 0.85f));
    private static readonly Basis AHandguardL = AHand(new Vector3(0, 0.3f, 0.95f), new Vector3(-0.85f, -0.2f, 0.3f));
    private static readonly Basis AShieldGripL = AHand(new Vector3(0, 1f, 0.05f), new Vector3(-1f, -0.1f, 0.3f));

    /// <summary>Point visé (monde) : le haut du corps et l'arme s'orientent vers lui.</summary>
    public void AlienAim(Vector3 world)
    {
        _aAimWorld = world;
        _aHasAim = true;
    }

    /// <summary>Recul d'un tir.</summary>
    public void AlienFire() => _aRecoil = 1f;

    /// <summary>Sursaut à l'impact (côté tiré au hasard).</summary>
    public void AlienFlinch()
    {
        if (_aFlinch > 0.6f) return;
        _aFlinch = 1f;
        _aFlinchSide = GD.Randf() < 0.5f ? -1f : 1f;
    }

    /// <summary>Garde : coup de bouclier.</summary>
    public void AlienBash() => _aBash = 1f;

    /// <summary>Bouche de l'arme (monde).</summary>
    public Vector3 AlienMuzzle => RigGlobalPosition(_aMuzzle);

    /// <summary>Hauteur du centre du torse au repos (modèle), pour le centre de l'ennemi.</summary>
    public float AlienChestHeight => _ad == null ? 3f : _ad.HipY + _ad.SpineY + _ad.ChestY * 0.75f;

    public void AnimateAlien(float dt, float speed, float attack)
    {
        AnimateAlienRig(dt, speed, attack);
        Perf.Begin(Perf.S.Skin);
        SyncSkin();
        Perf.End(Perf.S.Skin);
    }

    private void AnimateAlienRig(float dt, float speed, float attack)
    {
        _aT += dt;
        if (_flashT > -1f)
        {
            var was = _flashT;
            _flashT -= dt;
            if (was > 0 && _flashT <= 0 && _meshes != null) foreach (var m in _meshes) m.MaterialOverlay = null;
        }
        if (_dead || _ad == null) return;
        var d = _ad;
        var L = d.L;
        var flyer = Type == EnemyType.AlienFlyer;
        var guard = Type == EnemyType.AlienGuard;
        _aCrouchB = Mathf.MoveToward(_aCrouchB, AlienCrouch, dt * 2.6f);
        _aAimB = Mathf.MoveToward(_aAimB, AlienAiming, dt * 4f);
        _aRecoil = Mathf.Max(0, _aRecoil - dt * 6f);
        _aFlinch = Mathf.Max(0, _aFlinch - dt * (guard ? 6f : 4f));
        _aBash = Mathf.Max(0, _aBash - dt * 2.8f);
        _aShieldDownB = Mathf.MoveToward(_aShieldDownB, AlienShieldDown ? 1f : 0f, dt * 3f);
        _aChargeB = Mathf.MoveToward(_aChargeB, AlienCharging ? 1f : 0f, dt * 4f);
        // Un seul appel au moteur pour le repère global (visée, vitesse).
        var inTree = IsInsideTree();
        var gtr = inTree ? GlobalTransform : Transform3D.Identity;

        // Visée relative au corps (lacet, site) depuis le haut du torse.
        if (_aHasAim && inTree)
        {
            var local = gtr.AffineInverse() * _aAimWorld;
            var v = local - new Vector3(0, AlienChestHeight + 0.5f * L, 0);
            var ty = Mathf.Clamp(Mathf.Atan2(v.X, v.Z), -1.3f, 1.3f);
            var tp = Mathf.Clamp(Mathf.Atan2(v.Y, new Vector2(v.X, v.Z).Length()), -1.1f, 1.1f);
            var k = Mathf.Min(1f, dt * 7f);
            _aYaw = Mathf.LerpAngle(_aYaw, ty, k);
            _aPitch = Mathf.Lerp(_aPitch, tp, k);
        }
        var aimB = Mathf.SmoothStep(0, 1, _aAimB);
        var cr = Mathf.SmoothStep(0, 1, _aCrouchB);
        var amp = 0f;

        // ── Jambes et bassin ──
        if (flyer)
        {
            // Inclinaison selon la vitesse (repère du modèle), jambes pendantes qui se balancent.
            var gp = gtr.Origin;
            if (_aPrevOk && dt > 1e-4f)
            {
                var vel = gtr.Basis.Inverse() * ((gp - _aPrevPos) / dt);
                _aLean = Mathf.Lerp(_aLean, Mathf.Clamp(vel.Z * 0.035f, -0.35f, 0.55f), Mathf.Min(1f, dt * 3f));
                _aBank = Mathf.Lerp(_aBank, Mathf.Clamp(-vel.X * 0.03f, -0.4f, 0.4f), Mathf.Min(1f, dt * 3f));
            }
            _aPrevPos = gp;
            _aPrevOk = true;
            _aHips.Position = new Vector3(0, d.HipY + Mathf.Sin(_aT * 2.1f) * 0.08f * L, 0);
            _aHips.Rotation = new Vector3(_aLean, 0, _aBank);
            for (var i = 0; i < 2; i++)
            {
                var sw = Mathf.Sin(_aT * 1.7f + i * 1.9f);
                _aThigh[i].Rotation = new Vector3(-0.32f - _aLean * 0.4f + sw * 0.1f, 0, (i == 0 ? -1f : 1f) * 0.07f);
                _aShin[i].Rotation = new Vector3(0.75f + sw * 0.15f + i * 0.2f, 0, 0);
                _aFoot[i].Rotation = new Vector3(0.55f, 0, 0);
            }
            var thrust = 0.85f + 0.12f * Mathf.Sin(_aT * 37f) + 0.08f * Mathf.Sin(_aT * 61f) + Mathf.Clamp(speed * 0.02f, 0f, 0.3f);
            foreach (var f in _aFlame) if (f != null) f.Scale = new Vector3(1f, thrust, 1f);
        }
        else
        {
            // Démarche déduite du déplacement réel (hôte comme clients) : vitesse dans le repère du corps ; les jambes
            // (bassin) se tournent vers la marche, à reculons au-delà de 110°, le torse reste face à la cible.
            if (inTree)
            {
                var gp = gtr.Origin;
                if (_aPrevOk && dt > 1e-4f)
                {
                    var v = gtr.Basis.Inverse() * ((gp - _aPrevPos) / dt);
                    v.Y = 0;
                    if (v.LengthSquared() > 400f) v = Vector3.Zero; // téléportation, correction réseau
                    _aVel = _aVel.Lerp(v, Mathf.Min(1f, dt * 8f));
                }
                _aPrevPos = gp;
                _aPrevOk = true;
            }
            var vel = AlienSpeedOverride >= 0f ? new Vector3(0, 0, AlienSpeedOverride) : _aVel;
            var spd = vel.Length();
            var legYaw = 0f;
            var dirSign = 1f;
            if (spd > 0.4f)
            {
                var ang = Mathf.Atan2(vel.X, vel.Z);
                if (Mathf.Abs(ang) > 1.9f) { ang -= Mathf.Pi * Mathf.Sign(ang); dirSign = -1f; }
                legYaw = Mathf.Clamp(ang, -1.2f, 1.2f);
            }
            _aLegYaw = Mathf.LerpAngle(_aLegYaw, legYaw * (1f - cr), Mathf.Min(1f, dt * 6f));
            _aRunB = Mathf.MoveToward(_aRunB, spd > 7f ? 1f : 0f, dt * 3f);
            speed = spd;
            var stance = Mathf.Lerp(0.62f, 0.4f, _aRunB);
            var stride = Mathf.Clamp(0.7f + speed * 0.3f, 0.7f, 3.2f) * L;
            amp = Mathf.Clamp(speed / 1.1f, 0f, 1f) * (1f - cr);
            _aCycle = Mathf.PosMod(_aCycle + dt * dirSign * speed * stance / stride, 1f);
            var bob = -0.07f * L * amp * (0.5f + 0.5f * Mathf.Cos(_aCycle * Mathf.Tau * 2f)) - 0.12f * L * _aRunB;
            var hipsY = d.HipY - 0.05f * L * aimB + bob - 0.1f * L * _aChargeB;
            hipsY = Mathf.Lerp(hipsY, 1.36f * L, cr);
            _aHips.Position = new Vector3(0, hipsY, 0);
            _aHips.Rotation = new Vector3(0, _aLegYaw + Mathf.Sin(_aCycle * Mathf.Tau) * 0.08f * amp, 0);
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var u = Mathf.PosMod(_aCycle + i * 0.5f, 1f);
                var (fa, lift) = GaitCurve(u, stance);
                var fz = fa * stride * 0.5f * amp;
                var fy = d.Ankle + lift * (0.22f + 0.04f * speed) * L * amp;
                // À genou : genou droit au sol, pied gauche planté devant.
                fz = Mathf.Lerp(fz, i == 0 ? -0.82f * L : 0.72f * L, cr);
                fy = Mathf.Lerp(fy, i == 0 ? 0.3f * L : d.Ankle, cr);
                var jointY = hipsY + d.JointY;
                PlanarIk(d.Thigh, d.Shin, fy - jointY, fz, true, out var th, out var sh);
                _aThigh[i].Rotation = new Vector3(-th, 0, side * 0.035f);
                _aShin[i].Rotation = new Vector3(th - sh, 0, 0);
                var toe = -0.3f * lift * amp + (i == 0 ? 1.05f * cr : 0f);
                _aFoot[i].Rotation = new Vector3(sh + toe, 0, -side * 0.035f);
            }
        }

        // ── Torse et tête ──
        var tw = Mathf.Sin(_aCycle * Mathf.Tau) * 0.1f * amp * (1f - aimB * 0.7f);
        var lean = 0.05f + aimB * 0.1f + _aRunB * 0.22f + cr * 0.3f + _aChargeB * 0.2f - _aFlinch * 0.22f + (flyer ? 0.1f : 0f);
        var breath = Mathf.Sin(_aT * 1.3f) * 0.015f;
        var yawShare = aimB * 0.36f + (guard ? 0.15f : 0f);
        _aSpine.Rotation = new Vector3(lean * 0.6f + breath, _aYaw * yawShare - _aLegYaw + tw * 0.5f + _aFlinchSide * 0.16f * _aFlinch, -tw * 0.3f);
        _aChest.Rotation = new Vector3(lean * 0.4f - _aPitch * 0.22f * aimB + breath, _aYaw * yawShare - tw, _aFlinchSide * 0.08f * _aFlinch);
        var torsoYaw = 2f * _aYaw * yawShare;
        var look = Mathf.Sin(_aT * 0.43f) * 0.2f * (1f - aimB) * cr;
        _aHead.Rotation = new Vector3(-_aPitch * 0.45f - lean * 0.75f + Mathf.Sin(_aT * 0.9f) * 0.03f,
            Mathf.Clamp(_aYaw - torsoYaw, -0.9f, 0.9f) * 0.85f + look, _aFlinchSide * 0.15f * _aFlinch);

        // ── Arme : pose visée (épaulée vers la cible) ou au repos, convertie dans le repère du torse ──
        var chestM = _aHips.Transform * _aSpine.Transform * _aChest.Transform;
        var chestInv = chestM.AffineInverse();
        var cp = Mathf.Cos(_aPitch);
        var dirM = new Vector3(Mathf.Sin(_aYaw) * cp, Mathf.Sin(_aPitch), Mathf.Cos(_aYaw) * cp);
        var bM = Basis.LookingAt(-dirM, Vector3.Up);
        Transform3D gx;
        if (guard)
        {
            // Blaster tenu d'une main à la hanche droite.
            var hip = chestM * (new Vector3(-0.62f, -0.3f, 0.42f) * L);
            var aimC = chestInv * new Transform3D(bM, hip - bM * d.Grip);
            var lowC = new Transform3D(new Basis(Vector3.Up, -0.15f) * new Basis(Vector3.Right, 0.95f), new Vector3(-0.66f, -0.45f, 0.35f) * L - new Basis(Vector3.Right, 0.95f) * d.Grip);
            gx = lowC.InterpolateWith(aimC, Mathf.Max(aimB, 0.25f) * (1f - _aChargeB * 0.8f));
        }
        else
        {
            var aimC = chestInv * new Transform3D(bM, chestM * d.Pocket - bM * d.Butt);
            var lowC = new Transform3D(new Basis(Vector3.Up, 0.75f) * new Basis(Vector3.Right, 0.6f) * new Basis(Vector3.Back, 0.45f), new Vector3(0.05f, 0.1f, 0.66f) * L);
            var coverC = new Transform3D(new Basis(Vector3.Up, 0.25f) * new Basis(Vector3.Right, -1.2f), new Vector3(-0.2f, 0.05f, 0.62f) * L);
            gx = lowC.InterpolateWith(aimC, aimB);
            if (cr > 0.001f) gx = gx.InterpolateWith(coverC, cr * (1f - aimB));
        }
        gx.Origin -= gx.Basis.Z * (_aRecoil * 0.14f * L);
        gx.Basis = (gx.Basis * new Basis(Vector3.Right, -_aRecoil * 0.14f - _aFlinch * 0.15f)).Orthonormalized();
        _aGun.Position = gx.Origin;
        _aGun.Rotation = gx.Basis.GetEuler();

        // ── Bouclier (garde) : levé face à la cible, en avant pendant la charge, abaissé sur le côté ──
        Transform3D shieldX = default;
        if (_aShield != null)
        {
            var up = new Transform3D(new Basis(Vector3.Up, -0.12f + Mathf.Clamp(_aYaw - torsoYaw, -0.4f, 0.4f) * 0.6f), new Vector3(0.24f, 0.0f, 0.98f) * L);
            var charge = new Transform3D(new Basis(Vector3.Right, 0.12f), new Vector3(0.05f, -0.05f, 1.12f) * L);
            var down = new Transform3D(new Basis(Vector3.Up, 1.0f) * new Basis(Vector3.Back, -0.25f), new Vector3(0.78f, -0.55f, 0.3f) * L);
            shieldX = up.InterpolateWith(charge, _aChargeB).InterpolateWith(down, Mathf.SmoothStep(0, 1, _aShieldDownB));
            shieldX.Origin += new Vector3(0, 0, Mathf.Sin(_aBash * Mathf.Pi) * 0.6f * L);
            shieldX.Basis = shieldX.Basis.Orthonormalized();
            _aShield.Position = shieldX.Origin;
            _aShield.Rotation = shieldX.Basis.GetEuler();
        }

        // ── Bras : mains sur l'arme (ou main gauche sur la poignée du bouclier) ──
        var gripB = gx.Basis * APistolGripR;
        var gripP = gx * d.Grip;
        AlienArmIk(0, gripP - gripB * d.GripC, gripB, new Vector3(-0.6f, -1f, -0.25f));
        if (_aShield != null)
        {
            var hb = shieldX.Basis * AShieldGripL;
            AlienArmIk(1, shieldX * (new Vector3(0, 0.1f, -0.3f) * L) - hb * d.GripC, hb, new Vector3(0.7f, -1f, -0.4f));
        }
        else
        {
            var supB = gx.Basis * AHandguardL;
            AlienArmIk(1, gx * d.Support - supB * d.GripC, supB, new Vector3(0.55f, -1f, -0.3f));
        }
    }

    /// <summary>Cinématique inverse épaule-coude-poignet (repère du torse), coude orienté vers <paramref name="pole"/>.</summary>
    private void AlienArmIk(int i, Vector3 wrist, Basis handB, Vector3 pole)
    {
        var up = _ad.Upper;
        var fo = _ad.Fore;
        var sh = _aArm[i].Position;
        var dv = wrist - sh;
        var len = Mathf.Clamp(dv.Length(), 0.15f * up, up + fo - 0.005f);
        var dn = dv.LengthSquared() > 1e-8f ? dv.Normalized() : Vector3.Down;
        var cosA = Mathf.Clamp((up * up + len * len - fo * fo) / (2f * up * len), -1f, 1f);
        var sinA = Mathf.Sqrt(1f - cosA * cosA);
        var perp = pole - dn * pole.Dot(dn);
        if (perp.LengthSquared() < 1e-6f) perp = Vector3.Back - dn * dn.Z;
        perp = perp.Normalized();
        var elbow = sh + dn * (up * cosA) + perp * (up * sinA);
        var w = sh + dn * len;
        var yu = (sh - elbow).Normalized();
        var xu = dn.Cross(perp).Normalized();
        var zu = xu.Cross(yu);
        if (zu.Dot(perp) > 0) { xu = -xu; zu = -zu; }
        var bu = new Basis(xu, yu, zu);
        var yf = (elbow - w).Normalized();
        var bf = new Basis(xu, yf, xu.Cross(yf));
        _aArm[i].Rotation = bu.GetEuler();
        _aFore[i].Rotation = (bu.Transposed() * bf).Orthonormalized().GetEuler();
        _aHand[i].Rotation = (bf.Transposed() * handB.Orthonormalized()).Orthonormalized().GetEuler();
    }

    /// <summary>Repère d'un nœud de l'armature dans le repère de l'ennemi (dernière synchronisation ; formes de collision).</summary>
    public Transform3D AlienNodeXf(Node3D n) => AlienNodeXf(n, Transform);

    /// <summary>Idem, avec le repère du modèle déjà lu (plusieurs formes recalées par image).</summary>
    public Transform3D AlienNodeXf(Node3D n, in Transform3D model)
    {
        if (_rigNodes == null) return model * RigLocal(n);
        _aIdx ??= new System.Collections.Generic.Dictionary<Node3D, int>();
        if (!_aIdx.TryGetValue(n, out var i)) _aIdx[n] = i = System.Array.IndexOf(_rigNodes, n);
        return i < 0 ? model : model * _rigXf[i];
    }

    private System.Collections.Generic.Dictionary<Node3D, int> _aIdx;

    public Node3D AlienHeadNode => _aHead;
    public Node3D AlienChestNode => _aChest;
    public Node3D AlienHipsNode => _aHips;
    public Node3D AlienCoreNode => _aCore;
    public Node3D AlienShieldNode => _aShield;
    public Node3D AlienThighNode(int i) => _aThigh[i & 1];
    public Node3D AlienShinNode(int i) => _aShin[i & 1];
    public Node3D AlienFootNode(int i) => _aFoot[i & 1];
    public AlienDimsView AlienDimensions => new(_ad?.L ?? 1f, _ad?.W ?? 1f, _ad?.Thigh ?? 1.2f, _ad?.Shin ?? 1.2f);

    /// <summary>Longueurs utiles aux formes de collision.</summary>
    public readonly record struct AlienDimsView(float L, float W, float Thigh, float Shin);

    // ---------------- Mort ----------------

    private Bone[] _aDeathBones;
    private Vector3[] _aD0Pos, _aD0Rot, _aD1Pos, _aD1Rot;

    /// <summary>
    /// Chute de mort (tous les pairs) : les genoux cèdent, le corps bascule en arrière ou en avant avec l'accélération
    /// d'une chute, rebondit un peu et s'étale ; bras projetés, tête qui roule, arme et bouclier lâchés au sol.
    /// </summary>
    public void AlienDeath(bool backward)
    {
        if (_ad == null || _dead) return;
        _dead = true;
        var d = _ad;
        var L = d.L;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var list = new System.Collections.Generic.List<Bone> { _aHips, _aSpine, _aChest, _aHead, _aGun };
        if (_aShield != null) list.Add(_aShield);
        for (var i = 0; i < 2; i++) list.AddRange(new[] { _aThigh[i], _aShin[i], _aFoot[i], _aArm[i], _aFore[i], _aHand[i] });
        _aDeathBones = list.ToArray();
        var n = _aDeathBones.Length;
        _aD0Pos = new Vector3[n]; _aD0Rot = new Vector3[n]; _aD1Pos = new Vector3[n]; _aD1Rot = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            _aD0Pos[i] = _aD1Pos[i] = _aDeathBones[i].Position;
            _aD0Rot[i] = _aD1Rot[i] = _aDeathBones[i].Rotation;
        }
        var dir = backward ? -1f : 1f;
        void Set(Bone b, Vector3 rot, Vector3? pos = null)
        {
            var k = System.Array.IndexOf(_aDeathBones, b);
            if (k < 0) return;
            _aD1Rot[k] = rot;
            if (pos is { } p) _aD1Pos[k] = p;
        }
        var flyer = Type == EnemyType.AlienFlyer;
        var lieY = 0.42f * L * d.W;
        Set(_aHips, new Vector3(0, rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(-0.15f, 0.15f)), new Vector3(rng.RandfRange(-0.3f, 0.3f) * L, lieY, dir * 1.15f * L));
        Set(_aSpine, new Vector3(dir * 1.5f, rng.RandfRange(-0.15f, 0.15f), rng.RandfRange(-0.12f, 0.12f)));
        Set(_aChest, new Vector3(dir * 0.06f, rng.RandfRange(-0.2f, 0.2f), 0));
        Set(_aHead, new Vector3(backward ? -0.35f : -0.7f, rng.RandfRange(-0.9f, 0.9f), rng.RandfRange(-0.3f, 0.3f)));
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1f : 1f;
            var spread = rng.RandfRange(0.08f, 0.3f);
            Set(_aThigh[i], new Vector3(backward ? -1.42f + rng.RandfRange(0f, 0.35f) : 1.48f, 0, side * spread));
            Set(_aShin[i], new Vector3(backward ? rng.RandfRange(0.1f, 0.7f) : rng.RandfRange(0.1f, 0.9f), 0, 0));
            Set(_aFoot[i], new Vector3(backward ? 0.6f : 1.2f, 0, 0));
            Set(_aArm[i], new Vector3(rng.RandfRange(-0.6f, 0.4f), rng.RandfRange(-0.3f, 0.3f), side * rng.RandfRange(0.9f, 1.5f)));
            Set(_aFore[i], new Vector3(rng.RandfRange(-0.9f, -0.1f), 0, 0));
            Set(_aHand[i], new Vector3(rng.RandfRange(-0.4f, 0.4f), 0, 0));
        }
        // Arme (et bouclier) lâchés : posés à plat près du corps, convertis dans le repère du torse final.
        Transform3D Xf(Vector3 rot, Vector3 pos) => new(Basis.FromEuler(rot), pos);
        int Ix(Bone b) => System.Array.IndexOf(_aDeathBones, b);
        var chestEnd = Xf(_aD1Rot[Ix(_aHips)], _aD1Pos[Ix(_aHips)]) * Xf(_aD1Rot[Ix(_aSpine)], _aD1Pos[Ix(_aSpine)]) * Xf(_aD1Rot[Ix(_aChest)], _aD1Pos[Ix(_aChest)]);
        var inv = chestEnd.AffineInverse();
        var gunGround = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(-1.2f, 1.2f)) * new Basis(Vector3.Back, Mathf.Pi / 2 * (rng.Randf() < 0.5f ? 1f : -1f)),
            new Vector3(-1.1f * L, 0.14f * L, dir * 0.6f * L + rng.RandfRange(-0.6f, 0.6f)));
        var gl = inv * gunGround;
        Set(_aGun, gl.Basis.Orthonormalized().GetEuler(), gl.Origin);
        if (_aShield != null)
        {
            var sg = new Transform3D(new Basis(Vector3.Up, rng.RandfRange(-0.6f, 0.6f)) * new Basis(Vector3.Right, -Mathf.Pi / 2), new Vector3(1.5f * L, 0.12f * L, dir * 0.3f * L));
            var sl = inv * sg;
            Set(_aShield, sl.Basis.Orthonormalized().GetEuler(), sl.Origin);
        }
        foreach (var f in _aFlame) if (f != null) f.Scale = new Vector3(1f, 0.001f, 1f);
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(PoseAlienDeath), 0f, 1f, flyer ? 0.9f : 1.35f);
    }

    /// <summary>Pose de la chute à l'instant k (0..1) : jambes qui cèdent d'abord, puis bascule accélérée et léger rebond.</summary>
    private void PoseAlienDeath(float k)
    {
        if (_aDeathBones == null || !IsInstanceValid(this)) return;
        var fall = k < 0.8f ? (k / 0.8f) * (k / 0.8f) : 1f - 0.07f * Mathf.Sin((k - 0.8f) / 0.2f * Mathf.Pi);
        var legs = Mathf.Min(1f, k / 0.45f);
        legs = 1f - (1f - legs) * (1f - legs);
        for (var i = 0; i < _aDeathBones.Length; i++)
        {
            var b = _aDeathBones[i];
            var isLeg = System.Array.IndexOf(_aShin, b) >= 0 || System.Array.IndexOf(_aFoot, b) >= 0;
            var f = isLeg ? Mathf.Lerp(legs, fall, 0.5f) : fall;
            b.Position = _aD0Pos[i].Lerp(_aD1Pos[i], f);
            b.Rotation = new Vector3(Mathf.LerpAngle(_aD0Rot[i].X, _aD1Rot[i].X, f), Mathf.LerpAngle(_aD0Rot[i].Y, _aD1Rot[i].Y, f), Mathf.LerpAngle(_aD0Rot[i].Z, _aD1Rot[i].Z, f));
        }
        SyncSkin();
    }
}
