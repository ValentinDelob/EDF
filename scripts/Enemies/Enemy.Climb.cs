using Godot;

namespace Rempart;

/// <summary>
/// Déplacement des insectes grimpeurs (fourmis, araignée, mille-pattes, charognard) : ils marchent sur toute surface
/// solide (sol, murs, toits, voitures, murets), le corps aligné sur la normale, comme dans EDF.
///
/// Suivi de surface (hôte) par rayons contre la couche « monde » :
/// - sonde avant, à mi-hauteur du corps, le long du déplacement : une paroi devant → angle rentrant (sol → mur, mur → plafond) ;
/// - sonde d'accroche, du dessus de la nouvelle position vers la surface : marches, bordures, toits en pente ;
/// - sans accroche, sonde de contournement sous l'arête, vers l'arrière : angle saillant (mur → toit, toit → façade opposée) ;
/// - sinon le corps chute (gravité) jusqu'à la prochaine surface.
/// Les sondes sont espacées en terrain plat dégagé (selon la distance parcourue) et à chaque pas près des immeubles.
/// </summary>
public partial class Enemy
{
    /// <summary>Normale lissée de la surface sous le corps (axe Y du corps).</summary>
    private Vector3 _up = Vector3.Up;
    /// <summary>Normale de la surface sondée (cible du lissage).</summary>
    private Vector3 _surfN = Vector3.Up;
    /// <summary>Direction du regard dans le plan de la surface (axe Z du corps).</summary>
    private Vector3 _fwd = Vector3.Back;
    /// <summary>Sur un plafond (dessous de balcon, de corniche) : direction de sortie vers l'arête.</summary>
    private Vector3 _ceilDir;
    private bool _falling;
    private float _fallV;
    private float _probeDist;
    /// <summary>Point de chute du cadavre (sol sous l'insecte) : position du butin.</summary>
    public Vector3 LandingPoint;

    /// <summary>Insecte qui escalade les surfaces (pas les volants, ni le nid, ni les boss, ni les machines).</summary>
    public bool Climbs => !Def.Flying && !Def.Static && !Def.Boss && !IsMachine;

    /// <summary>Hauteur des sondes au-dessus de la surface : en dessous, l'obstacle est enjambé ; au-dessus, c'est une paroi.</summary>
    private float ProbeH => 0.75f * Def.Size;

    /// <summary>Rayon partagé : un nœud RayCast3D n'alloue rien par requête (IntersectRay crée un dictionnaire de résultats).</summary>
    private static RayCast3D _caster;

    /// <summary>Rayon contre le décor solide ; la normale renvoyée regarde l'origine du rayon. Les murs invisibles de bord de carte ne comptent pas.</summary>
    private bool Ray(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
    {
        if (_caster == null || !IsInstanceValid(_caster) || _caster.GetParent() != _world)
        {
            _caster = new RayCast3D { Name = "SurfaceProbe", Enabled = false, CollisionMask = 1, HitBackFaces = true, ExcludeParent = false };
            _world.AddChild(_caster);
        }
        // Le monde est à l'origine : la position locale est la position globale.
        _caster.Position = from;
        _caster.TargetPosition = to - from;
        _caster.ForceRaycastUpdate();
        point = to;
        normal = Vector3.Up;
        if (!_caster.IsColliding()) return false;
        point = _caster.GetCollisionPoint();
        normal = _caster.GetCollisionNormal();
        // Origine à l'intérieur d'un solide (normale nulle) : ce n'est pas une surface praticable.
        if (normal.LengthSquared() < 0.25f) return false;
        if (normal.Dot(from - point) < 0) normal = -normal;
        if (Mathf.Abs(point.X) > CityBuilder.Half + 3f || point.Z < -CityBuilder.Half - 3f || point.Z > CityBuilder.SeaStart + 7f) return false;
        return true;
    }

    /// <summary>
    /// Direction de marche dans le plan de la surface. Sur une paroi, vouloir la traverser fait monter
    /// (escalade) et vouloir s'en éloigner fait descendre ; sur un plafond, on sort par l'arête.
    /// </summary>
    private Vector3 SurfaceDir(Vector3 want, Vector3 n)
    {
        if (n.Y < -0.5f && _ceilDir != Vector3.Zero) return _ceilDir;
        var t = want - n * want.Dot(n);
        if (Mathf.Abs(n.Y) < 0.55f)
        {
            var up = Vector3.Up - n * n.Y;
            if (up.LengthSquared() > 1e-4f) t += up.Normalized() * (-want.Dot(n) * 1.2f);
        }
        return t.LengthSquared() > 1e-4f ? t.Normalized() : Vector3.Zero;
    }

    private float _undergroundT;
    /// <summary>Insectes remis à la surface depuis le lancement (journal --fps).</summary>
    public static int UndergroundFixes;

    /// <summary>
    /// Garde-fou : un insecte passé sous le sol (accroché à la face cachée d'une dalle, d'une bordure) est remis à la surface.
    /// Un creux à ciel ouvert (escalier du métro, lit de rivière) reste permis : seul compte le décor au-dessus du corps.
    /// </summary>
    private bool FixUnderground(float dt)
    {
        var p = GlobalPosition;
        if (p.Y > -0.15f) { _undergroundT = 0; return false; }
        _undergroundT -= dt;
        if (_undergroundT > 0) return false;
        _undergroundT = 0.1f;
        // Ciel dégagé au-dessus (hors du corps lui-même) : vrai creux, rien à corriger.
        if (!Ray(p + Vector3.Up * 0.05f, p + Vector3.Up * Mathf.Max(3f, -p.Y + 1.5f), out _, out _)
            && !Ray(new Vector3(p.X, 0.05f, p.Z), new Vector3(p.X, -0.05f, p.Z), out _, out _)) return false;
        var top = new Vector3(p.X, Mathf.Max(p.Y, 0f) + 60f, p.Z);
        var land = Ray(top, new Vector3(p.X, -40f, p.Z), out var lp, out var ln) && ln.Y > 0.5f ? lp : new Vector3(p.X, 0, p.Z);
        GlobalPosition = land;
        _surfN = Vector3.Up;
        _up = Vector3.Up;
        _ceilDir = Vector3.Zero;
        _falling = false;
        _fallV = 0;
        _probeDist = 0;
        UndergroundFixes++;
        return true;
    }

    /// <summary>Point sous le niveau du sol là où le sol existe (face cachée d'une dalle, d'une bordure, pied d'un mur enterré).</summary>
    private bool UnderSlab(Vector3 pt) =>
        pt.Y < -0.08f && Ray(new Vector3(pt.X, 0.1f, pt.Z), new Vector3(pt.X, -0.1f, pt.Z), out _, out _);

    /// <summary>Avance d'un pas sur la surface (hôte) et recale position et normale.</summary>
    private void ClimbStep(Vector3 dir, float dist, float dt)
    {
        if (FixUnderground(dt)) return;
        var p = GlobalPosition;
        _speedNow = dist / Mathf.Max(dt, 1e-4f);
        if (_falling)
        {
            _fallV = Mathf.Min(_fallV + 28f * dt, 60f);
            var drop = p + Vector3.Down * _fallV * dt + dir * dist * 0.3f;
            if (Ray(p + Vector3.Up * 0.5f, drop + Vector3.Down * 0.1f, out var lp, out var ln))
            {
                _falling = false;
                _fallV = 0;
                GlobalPosition = lp;
                _surfN = ln;
                Sfx.At("hit", lp, -8f, 0.6f, 40);
            }
            else GlobalPosition = drop.Y < -3f ? new Vector3(drop.X, 0, drop.Z) : drop;
            if (GlobalPosition.Y <= 0 && _falling) { _falling = false; _surfN = Vector3.Up; GlobalPosition = GlobalPosition with { Y = 0 }; }
            return;
        }

        var n = _surfN;
        var step = dir * dist;
        // Sondes espacées selon la distance parcourue : ~0,6 m au sol, ~0,3 m sur les parois et les toits, × taille de l'insecte
        // (les sondes portent assez loin, en multiples de la taille, pour couvrir le chemin fait sans sonder).
        _probeDist += dist;
        var flat = n.Y > 0.97f && _up.Y > 0.97f && p.Y < 0.6f;
        // Un pas qui descendrait sous le niveau du sol (descente d'un mur, d'une bordure) sonde tout de suite : sans cela
        // l'insecte continuait tout droit sous la dalle jusqu'au sondage suivant.
        var crossesGround = p.Y >= -0.05f && (p + step).Y < -0.02f;
        if (_probeDist < (flat ? 0.6f : 0.3f) * Mathf.Max(Def.Size, 1f) && dist > 0 && !crossesGround)
        {
            GlobalPosition = p + step;
            return;
        }
        var travelled = _probeDist;
        _probeDist = 0;
        if (dist <= 1e-5f) return;

        var lift = n * ProbeH;
        // 1. Angle rentrant : paroi devant le corps.
        if (Ray(p + lift, p + lift + dir * (travelled + 1.3f * Def.Size), out var wp, out var wn) && wn.Dot(n) < 0.6f && !UnderSlab(wp)
            && (p + lift).DistanceTo(wp) <= travelled + 0.8f * Def.Size)
        {
            var reach = travelled + 1.3f * Def.Size;
            // Obstacle étroit (poteau, potelet, lampadaire) : on le contourne au lieu de l'escalader.
            var side = n.Cross(dir);
            var lat = side.LengthSquared() > 1e-4f ? side.Normalized() * (0.55f * Def.Size) : Vector3.Zero;
            var wide = lat == Vector3.Zero
                || Ray(p + lift + lat, p + lift + lat + dir * (reach + 0.4f), out _, out _)
                && Ray(p + lift - lat, p + lift - lat + dir * (reach + 0.4f), out _, out _);
            if (!wide)
            {
                var slide = dir - wn * dir.Dot(wn);
                slide -= n * slide.Dot(n);
                dir = slide.LengthSquared() > 1e-4f ? slide.Normalized() : lat.Normalized();
                step = dir * dist;
            }
            else
            {
                // Pivot sur l'arête : on glisse sur le nouveau plan, à la hauteur des pieds.
                var d0 = (p - wp).Dot(wn);
                GlobalPosition = p - wn * Mathf.Max(d0, 0f) + wn * 0.02f;
                if (wn.Y < -0.5f)
                {
                    var out_ = n - wn * n.Dot(wn);
                    _ceilDir = out_.LengthSquared() > 1e-4f ? out_.Normalized() : Vector3.Zero;
                }
                _surfN = wn;
                _speedNow = dist / Mathf.Max(dt, 1e-4f);
                return;
            }
        }
        // 2. Accroche : la surface sous la nouvelle position (marches, bordures, pentes).
        var q = p + step;
        if (Ray(q + lift, q - n * (ProbeH + 0.6f + travelled), out var sp, out var sn) && !UnderSlab(sp))
        {
            GlobalPosition = sp;
            SetSurface(sn);
            _speedNow = dist / Mathf.Max(dt, 1e-4f);
            return;
        }
        // 3. Angle saillant : on passe l'arête et on s'accroche à la face suivante, sous le bord.
        var under = q - n * (0.35f * Def.Size);
        if (Ray(under, under - dir * (travelled + 1.2f * Def.Size), out var ep, out var en) && !UnderSlab(ep))
        {
            GlobalPosition = ep;
            SetSurface(en);
            _speedNow = dist / Mathf.Max(dt, 1e-4f);
            return;
        }
        // 4. Plus de surface : chute. Arrivé sous le sol (bas d'un mur descendu), on se pose sur le sol à la place.
        if (UnderSlab(q))
        {
            var top = new Vector3(q.X, 2.5f, q.Z);
            GlobalPosition = Ray(top, new Vector3(q.X, -0.2f, q.Z), out var gp, out var gn) && gn.Y > 0.5f ? gp : new Vector3(q.X, 0, q.Z);
            SetSurface(Vector3.Up);
            _speedNow = dist / Mathf.Max(dt, 1e-4f);
            return;
        }
        _falling = true;
        _fallV = 0;
        _surfN = Vector3.Up;
        _ceilDir = Vector3.Zero;
        GlobalPosition = q;
    }

    private void SetSurface(Vector3 n)
    {
        if (n.Y >= -0.5f) _ceilDir = Vector3.Zero;
        _surfN = n;
    }

    /// <summary>Lisse la normale et oriente le corps (Y = normale, Z = regard dans le plan).</summary>
    private void OrientOnSurface(Vector3 face, float dt)
    {
        var k = 1f - Mathf.Exp(-dt * 12f);
        var up = _up.Lerp(_surfN, k);
        _up = up.LengthSquared() > 1e-4f ? up.Normalized() : _surfN;
        var f = face - _up * face.Dot(_up);
        if (f.LengthSquared() < 1e-4f) f = _fwd - _up * _fwd.Dot(_up);
        if (f.LengthSquared() < 1e-4f) f = _up.Y < 0.9f ? Vector3.Up - _up * _up.Y : Vector3.Back;
        f = f.Normalized();
        var cur = _fwd - _up * _fwd.Dot(_up);
        cur = cur.LengthSquared() > 1e-4f ? cur.Normalized() : f;
        // Rotation vers le regard voulu (lerp normalisé ; demi-tour : on passe par le côté).
        var turn = cur.Lerp(f, Mathf.Clamp(dt * 7f, 0, 1));
        if (turn.LengthSquared() < 1e-3f) turn = cur + _up.Cross(cur) * 0.2f;
        _fwd = turn.Normalized();
        Basis = BasisFrom(_up, _fwd);
        _yaw = Mathf.Atan2(_fwd.X, _fwd.Z);
    }

    /// <summary>Repère orthonormé à partir d'une normale et d'une direction de regard.</summary>
    private static Basis BasisFrom(Vector3 up, Vector3 fwd)
    {
        var z = fwd - up * fwd.Dot(up);
        if (z.LengthSquared() < 1e-6f) z = Mathf.Abs(up.Y) < 0.9f ? Vector3.Up - up * up.Y : Vector3.Back;
        z = z.Normalized();
        var x = up.Cross(z).Normalized();
        return new Basis(x, up, z);
    }

    // ---------------- Réseau : orientation compacte ----------------

    /// <summary>Vecteur unitaire en 2 octets (projection octaédrique).</summary>
    public static ushort PackDir(Vector3 v)
    {
        var l1 = Mathf.Abs(v.X) + Mathf.Abs(v.Y) + Mathf.Abs(v.Z);
        if (l1 < 1e-6f) return PackDir(Vector3.Up);
        var x = v.X / l1;
        var y = v.Z / l1;
        if (v.Y < 0)
        {
            var ox = (1f - Mathf.Abs(y)) * (x >= 0 ? 1f : -1f);
            var oy = (1f - Mathf.Abs(x)) * (y >= 0 ? 1f : -1f);
            x = ox;
            y = oy;
        }
        var bx = (byte)Mathf.Clamp(Mathf.RoundToInt((x * 0.5f + 0.5f) * 254f), 0, 255);
        var by = (byte)Mathf.Clamp(Mathf.RoundToInt((y * 0.5f + 0.5f) * 254f), 0, 255);
        return (ushort)(bx | (by << 8));
    }

    public static Vector3 UnpackDir(ushort p)
    {
        var x = (p & 0xFF) / 254f * 2f - 1f;
        var y = (p >> 8) / 254f * 2f - 1f;
        var z = 1f - Mathf.Abs(x) - Mathf.Abs(y);
        if (z < 0)
        {
            var ox = (1f - Mathf.Abs(y)) * (x >= 0 ? 1f : -1f);
            var oy = (1f - Mathf.Abs(x)) * (y >= 0 ? 1f : -1f);
            x = ox;
            y = oy;
        }
        // Repère du codage : (x, z) dans le plan, y vertical.
        return new Vector3(x, z, y).Normalized();
    }

    /// <summary>Position en 3 entiers 16 bits (1/64 m, ±512 m).</summary>
    public static void PackPos(System.IO.BinaryWriter w, Vector3 p)
    {
        w.Write((short)Mathf.Clamp(Mathf.RoundToInt(p.X * 64f), short.MinValue, short.MaxValue));
        w.Write((short)Mathf.Clamp(Mathf.RoundToInt(p.Y * 64f), short.MinValue, short.MaxValue));
        w.Write((short)Mathf.Clamp(Mathf.RoundToInt(p.Z * 64f), short.MinValue, short.MaxValue));
    }

    public static Vector3 UnpackPos(System.IO.BinaryReader r) => new(r.ReadInt16() / 64f, r.ReadInt16() / 64f, r.ReadInt16() / 64f);

    /// <summary>Résumé pour le journal de test (--fps) : insectes sur un mur, en hauteur (toits), en chute, altitude max.</summary>
    public static string ClimbStats(System.Collections.Generic.IEnumerable<Enemy> all)
    {
        int walls = 0, high = 0, falling = 0;
        var top = 0f;
        foreach (var e in all)
        {
            if (e.State == EnemyState.Dead || !e.Climbs) continue;
            var up = e.Basis.Y;
            if (Mathf.Abs(up.Y) < 0.5f) walls++;
            else if (e.GlobalPosition.Y > 2.5f) high++;
            if (e._falling) falling++;
            top = Mathf.Max(top, e.GlobalPosition.Y);
        }
        return $"murs {walls} hauteur {high} chute {falling} ymax {top:0.0} sous-sol {UndergroundFixes}";
    }

    // ---------------- Mort : chute au sol ----------------

    /// <summary>Point d'impact au sol sous le corps (toit compris), recherché vers le bas depuis le corps.</summary>
    private Vector3 GroundBelow(Vector3 from)
    {
        var start = from + _up * (0.6f * Def.Size) + Vector3.Up * 0.3f;
        return Ray(start, start + Vector3.Down * 400f, out var p, out _) ? p : new Vector3(from.X, 0, from.Z);
    }
}
