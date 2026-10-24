using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Rempart;

public enum GuardKind : byte { Structure = 0, Truck = 1 }

/// <summary>
/// Élément à protéger : structure de la mission « Défense » (antenne relais, poste de commandement)
/// ou camion du convoi de la mission « Escorte ». Les insectes le prennent pour cible comme un soldat.
/// </summary>
public partial class Guard : Node3D
{
    public int Id;
    public GuardKind Kind;
    public string Label = "";
    public float Health, MaxHealth;
    public bool Destroyed;
    /// <summary>Demi-dimensions de l'emprise au sol (repère local, x et z) ; hauteur du modèle.</summary>
    public Vector2 Half;
    public float Height;
    /// <summary>Éclair de la barre de vie quand il est touché (1 → 0).</summary>
    public float Flash;
    /// <summary>Camion : abscisse le long du trajet (m), dernière valeur reçue de l'hôte, vitesse.</summary>
    public float S, NetS, Speed;
    public float Yaw;
    public Node3D Model;
    public CollisionObject3D Body;
    public MeshInstance3D Beacon;
    private float _lastHealth = -1f;

    /// <summary>Point de l'emprise le plus proche (au sol).</summary>
    public Vector3 ClosestPoint(Vector3 p)
    {
        var xf = GlobalTransform;
        var l = xf.AffineInverse() * p;
        l.X = Mathf.Clamp(l.X, -Half.X, Half.X);
        l.Z = Mathf.Clamp(l.Z, -Half.Y, Half.Y);
        l.Y = 0;
        return xf * l;
    }

    /// <summary>Hauteur du repère du HUD (au-dessus de l'abri pour l'antenne : le mât de 24 m sortirait vite du champ).</summary>
    public float MarkerHeight = 5f;

    /// <summary>Repère du HUD (barre de vie, nom).</summary>
    public Vector3 Top => GlobalPosition + Vector3.Up * MarkerHeight;

    /// <summary>Point visé par les insectes volants (mi-hauteur).</summary>
    public Vector3 Middle => GlobalPosition + Vector3.Up * (Height * 0.5f);

    /// <summary>PV reçus de l'hôte : déclenche l'éclair de la barre quand ils baissent.</summary>
    public void SetHealth(float hp)
    {
        if (_lastHealth >= 0 && hp < _lastHealth - 0.5f) Flash = 1f;
        _lastHealth = hp;
        Health = hp;
    }

    public override void _Process(double delta)
    {
        Flash = Mathf.Max(0, Flash - (float)delta * 3f);
        // Feu clignotant (gyrophare du camion, balise rouge de l'antenne).
        if (Beacon != null && !Destroyed)
        {
            var t = Time.GetTicksMsec() * 0.001f;
            Beacon.Visible = Kind == GuardKind.Truck ? Mathf.PosMod(t * 2.2f + Id * 0.37f, 1f) < 0.5f : Mathf.PosMod(t * 0.8f, 1f) < 0.35f;
        }
    }
}

/// <summary>
/// Objectif de mission, sur chaque pair (nœud enfant du monde) :
/// <list type="bullet">
/// <item><b>Défense</b> : une structure (antenne relais, poste de commandement) posée sur une place dégagée ; la mission échoue si elle tombe ;</item>
/// <item><b>Escorte</b> : 2 ou 3 camions roulent le long d'un trajet ; ils s'arrêtent si un insecte barre la route ou si aucun soldat
/// n'est à moins de <see cref="ObjectiveDef.EscortRadius"/> m ; la mission échoue si tous sont détruits ;</item>
/// <item><b>Survie</b> : compte à rebours tenu par le directeur de mission ;</item>
/// <item>toutes missions : compte à rebours d'extraction (15 s pour ramasser les caisses une fois l'objectif rempli).</item>
/// </list>
/// Structure et trajet sont reconstruits à l'identique par chaque pair (carte déterministe) ; l'hôte décide des dégâts,
/// de l'avancée du convoi et des minuteurs, et diffuse un petit état (10 Hz) plus un événement fiable par destruction.
/// </summary>
public partial class MissionObjective : Node3D
{
    /// <summary>Tests : durée de survie imposée (--survive-time), multiplicateurs des PV (--obj-hp) et de la vitesse du convoi (--convoy-speed).</summary>
    public static float SurviveOverride = -1f, HpMul = 1f, SpeedMul = 1f;
    /// <summary>Tests (--objlog) : journal « [objectif] » toutes les 3 s.</summary>
    public static bool Log;

    public const float TruckGap = 14f;
    private const float ExtractionDelay = 15f;

    public World W;
    public ObjectiveDef Def;
    public MissionGoal Goal;
    public readonly List<Guard> Guards = new();
    public Guard Structure;
    public readonly List<Guard> Trucks = new();

    /// <summary>Objectif rempli : plus d'échec possible, extraction en cours.</summary>
    public bool Completed;
    /// <summary>Survie : secondes restantes (−1 hors survie).</summary>
    public float SurviveLeft = -1f;
    /// <summary>Extraction : secondes restantes (−1 tant que l'objectif n'est pas rempli).</summary>
    public float ExtractionLeft = -1f;
    /// <summary>Escorte : convoi à l'arrêt (raison : 1 route barrée, 2 aucune escorte).</summary>
    public int StopReason;

    private Vector3[] _route = System.Array.Empty<Vector3>();
    private float[] _cum = System.Array.Empty<float>();
    public float RouteLength { get; private set; }
    public Vector3 Arrival => _route.Length > 0 ? _route[^1] : Vector3.Zero;
    private float _sendT, _crushT, _logT;

    public static MissionObjective Create(World w)
    {
        var o = new MissionObjective { W = w, Def = w.Mission.Objective ?? new ObjectiveDef(), Goal = w.Mission.Goal, Name = "Objective" };
        w.AddChild(o);
        o.Build();
        return o;
    }

    public float SurviveSeconds => SurviveOverride > 0 ? SurviveOverride : Def.SurviveSeconds;

    private void Build()
    {
        if (Goal == MissionGoal.Defense) BuildStructure();
        else if (Goal == MissionGoal.Escort) BuildConvoy();
        else if (Goal == MissionGoal.Survival) SurviveLeft = SurviveSeconds;
    }

    // ------------------------------------------------------------------ Défense

    private void BuildStructure()
    {
        var antenna = Def.Structure == StructureKind.RelayAntenna;
        var half = antenna ? new Vector2(4.6f, 4.6f) : new Vector2(5.2f, 4.2f);
        var spot = FindSpot(new Vector3(Def.AnchorX, 0, Def.AnchorZ), Mathf.Max(half.X, half.Y) + 2f);
        var g = new Guard
        {
            Id = 0, Kind = GuardKind.Structure, Label = antenna ? "Antenne relais" : "Poste de commandement",
            Half = half, Height = antenna ? 24f : 6f, MarkerHeight = antenna ? 6.5f : 7f, Name = "DefendedStructure",
        };
        g.MaxHealth = g.Health = Def.StructureHp * HpMul;
        AddChild(g);
        g.Position = spot;
        g.Model = antenna ? ObjectiveModels.RelayAntenna(out g.Beacon) : ObjectiveModels.CommandPost(out g.Beacon);
        g.AddChild(g.Model);
        // Solide pour les soldats seulement (couche 6, comme les camions) : les tirs passent (les insectes collés à la structure
        // restent atteignables) et les sondes d'escalade l'ignorent (pas d'insecte coincé dans le socle).
        var body = new StaticBody3D { CollisionLayer = 1 << 5, CollisionMask = 0, Name = "Solid" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(half.X * 2 - 1.4f, 2.4f, half.Y * 2 - 1.4f) }, Position = new Vector3(0, 1.2f, 0) });
        if (antenna) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.6f, g.Height, 1.6f) }, Position = new Vector3(0, g.Height / 2, 0) });
        g.AddChild(body);
        g.Body = body;
        W.Flow.SetBlockedRect(spot, new Vector3(half.X * 2, g.Height, half.Y * 2), true, 0.5f);
        Structure = g;
        Guards.Add(g);
    }

    /// <summary>
    /// Place dégagée la plus proche du point voulu (ni immeuble, ni mobilier, ni départ des soldats, ni véhicules),
    /// identique chez tous les pairs (carte déterministe) : rien à écraser ni à synchroniser au chargement.
    /// </summary>
    private Vector3 FindSpot(Vector3 want, float radius)
    {
        var avoid = new List<Vector3>(W.City.PlayerSpawns) { new(14, 0, 22), new(-16, 0, 24) };
        var props = new List<Vector2>();
        for (var i = 0; W.Decor?.Get(i) is { } e; i++)
        {
            var o = e.Xf.Origin;
            if (new Vector2(o.X - want.X, o.Z - want.Z).Length() < 80f) props.Add(new Vector2(o.X, o.Z));
        }
        for (var ring = 0; ring < 14; ring++)
        {
            var n = ring == 0 ? 1 : 8 * ring;
            for (var k = 0; k < n; k++)
            {
                var a = k * Mathf.Tau / n;
                var p = want + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (ring * 4f);
                if (avoid.Any(v => new Vector2(v.X - p.X, v.Z - p.Z).Length() < radius + 5f)) continue;
                if (props.Any(v => v.DistanceTo(new Vector2(p.X, p.Z)) < radius + 1f)) continue;
                var free = true;
                for (var dz = -radius; dz <= radius && free; dz += 2f)
                for (var dx = -radius; dx <= radius && free; dx += 2f)
                    if (W.Flow.IsBlocked(p + new Vector3(dx, 0, dz))) free = false;
                if (free) return p;
            }
        }
        return want;
    }

    // ------------------------------------------------------------------ Escorte

    private void BuildConvoy()
    {
        var r = Def.Route ?? new[] { 0f, 30f, 0f, -120f };
        _route = new Vector3[r.Length / 2];
        for (var i = 0; i < _route.Length; i++) _route[i] = new Vector3(r[i * 2], 0, r[i * 2 + 1]);
        _cum = new float[_route.Length];
        for (var i = 1; i < _route.Length; i++) _cum[i] = _cum[i - 1] + _route[i - 1].DistanceTo(_route[i]);
        RouteLength = _cum[^1];
        var count = Mathf.Clamp(Def.Trucks, 1, 4);
        for (var i = 0; i < count; i++)
        {
            var g = new Guard
            {
                Id = i, Kind = GuardKind.Truck, Label = $"Camion {i + 1}",
                Half = new Vector2(1.35f, 3.9f), Height = 3.5f, MarkerHeight = 4.6f, Name = $"ConvoyTruck{i}",
            };
            g.MaxHealth = g.Health = Def.TruckHp * HpMul;
            g.S = g.NetS = (count - 1 - i) * TruckGap;
            AddChild(g);
            g.Model = ObjectiveModels.Truck(out g.Beacon);
            g.AddChild(g.Model);
            // Solide pour les soldats (couche 6) ; ni les tirs ni les sondes d'escalade des insectes ne le touchent.
            var body = new AnimatableBody3D { CollisionLayer = 1 << 5, CollisionMask = 0, SyncToPhysics = false, Name = "Solid" };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.5f, 3.2f, 7.6f) }, Position = new Vector3(0, 1.75f, 0) });
            g.AddChild(body);
            g.Body = body;
            PlaceTruck(g, 1f);
            Trucks.Add(g);
            Guards.Add(g);
        }
        if (Log)
        {
            var blocked = 0;
            for (var s = 0f; s < RouteLength; s += 2f) if (W.Flow.IsBlocked(RoutePoint(s))) blocked++;
            GD.Print($"[objectif] trajet du convoi : {RouteLength:0} m, {blocked} points dans une cellule bloquée");
        }
    }

    /// <summary>Point du trajet à l'abscisse <paramref name="s"/> (bornée).</summary>
    public Vector3 RoutePoint(float s)
    {
        if (_route.Length == 0) return Vector3.Zero;
        if (s <= 0) return _route[0];
        for (var i = 1; i < _route.Length; i++)
            if (s <= _cum[i])
            {
                var t = (s - _cum[i - 1]) / Mathf.Max(0.001f, _cum[i] - _cum[i - 1]);
                return _route[i - 1].Lerp(_route[i], t);
            }
        return _route[^1];
    }

    /// <summary>Le camion regarde le long d'une corde (virages adoucis aux carrefours).</summary>
    private void PlaceTruck(Guard g, float blend)
    {
        var p = RoutePoint(g.S);
        var ahead = RoutePoint(g.S + 5f) - RoutePoint(g.S - 5f);
        if (ahead.LengthSquared() > 0.01f)
        {
            var yaw = Mathf.Atan2(ahead.X, ahead.Z);
            g.Yaw = blend >= 1f ? yaw : Mathf.LerpAngle(g.Yaw, yaw, blend);
        }
        if (g.Destroyed) return;
        g.Position = p;
        g.Rotation = new Vector3(0, g.Yaw, 0);
    }

    /// <summary>Camion de tête (premier camion intact), ou null.</summary>
    public Guard LeadTruck => Trucks.FirstOrDefault(t => !t.Destroyed);

    public bool ConvoyLost => Goal == MissionGoal.Escort && Trucks.All(t => t.Destroyed);

    public bool StructureLost => Structure != null && Structure.Destroyed;

    /// <summary>Tous les camions intacts sont à leur place au point de regroupement.</summary>
    public bool ConvoyArrived
    {
        get
        {
            if (Goal != MissionGoal.Escort) return false;
            var k = 0;
            var any = false;
            foreach (var t in Trucks)
            {
                if (t.Destroyed) continue;
                any = true;
                if (t.S < RouteLength - k * TruckGap - 0.5f) return false;
                k++;
            }
            return any;
        }
    }

    /// <summary>Avancée du convoi (camion de tête), de 0 à 1.</summary>
    public float ConvoyProgress => RouteLength > 0 && LeadTruck is { } l ? Mathf.Clamp(l.S / RouteLength, 0, 1) : 0f;

    private void HostConvoy(float dt)
    {
        var alive = Trucks.Where(t => !t.Destroyed).ToList();
        if (alive.Count == 0) return;
        // Escorte : au moins un soldat debout à moins de EscortRadius m d'un camion.
        var escorted = W.AlivePlayerPositions().Any(p => alive.Any(t => Flat(p, t.GlobalPosition) <= Def.EscortRadius));
        var blocked = Completed || RoadBlocked(alive[0]);
        StopReason = !escorted ? 2 : blocked ? 1 : 0;
        var want = StopReason == 0 ? Def.TruckSpeed * SpeedMul : 0f;
        _crushT -= dt;
        var crush = _crushT <= 0;
        if (crush) _crushT = 0.3f;
        for (var k = 0; k < alive.Count; k++)
        {
            var t = alive[k];
            var limit = RouteLength - k * TruckGap;
            if (k > 0) limit = Mathf.Min(limit, alive[k - 1].S - TruckGap);
            t.Speed = Mathf.MoveToward(t.Speed, want, (want > t.Speed ? 2.5f : 6f) * dt);
            var next = Mathf.Min(t.S + t.Speed * dt, limit);
            if (next < t.S) next = t.S;
            t.Speed = Mathf.Min(t.Speed, (next - t.S) / Mathf.Max(dt, 0.0001f) + 0.01f);
            t.S = next;
            PlaceTruck(t, Mathf.Clamp(dt * 3f, 0, 1));
            // Le camion qui roule écrase le mobilier devant lui (voitures garées en travers comprises).
            if (crush && t.Speed > 0.5f)
            {
                var fwd = new Vector3(Mathf.Sin(t.Yaw), 0, Mathf.Cos(t.Yaw));
                W.Decor?.HostCrushArea(t.GlobalPosition + fwd * (t.Half.Y + 1.2f), 2f, 2f, 5);
            }
        }
    }

    /// <summary>Un insecte au sol barre la route devant le camion de tête.</summary>
    private bool RoadBlocked(Guard lead)
    {
        var fwd = new Vector3(Mathf.Sin(lead.Yaw), 0, Mathf.Cos(lead.Yaw));
        var front = lead.GlobalPosition + fwd * (lead.Half.Y + 3f);
        foreach (var e in W.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead || e.Def.Flying || e.IsStatic) continue;
            if (e.GlobalPosition.Y > 5f) continue; // sur un mur, un toit
            if (Flat(e.GlobalPosition, front) < 2.2f * e.Def.Size + 2.5f) return true;
        }
        return false;
    }

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // ------------------------------------------------------------------ Ciblage (hôte)

    /// <summary>
    /// L'objectif devient la cible d'un insecte s'il n'est guère plus loin que sa cible actuelle (soldat ou allié) :
    /// légère préférence pour l'objectif, comme dans EDF.
    /// </summary>
    public bool HostPickTarget(Enemy e, Vector3 current, out Vector3 point, out Guard guard)
    {
        point = current;
        guard = null;
        if (Completed || Guards.Count == 0) return false;
        var pos = e.GlobalPosition;
        var best = float.MaxValue;
        var bestPoint = Vector3.Zero;
        foreach (var g in Guards)
        {
            if (g.Destroyed) continue;
            var cp = g.ClosestPoint(pos);
            var d = Flat(pos, cp);
            if (d < best) { best = d; bestPoint = cp; guard = g; }
        }
        if (guard == null || best > Flat(pos, current) * 1.3f + 4f) { guard = null; return false; }
        point = e.Def.Flying ? guard.Middle : bestPoint;
        return true;
    }

    /// <summary>Cibles supplémentaires du champ de flux : autour de la structure, sous les camions.</summary>
    public IEnumerable<Vector3> FlowTargets()
    {
        if (Completed) yield break;
        foreach (var g in Guards)
        {
            if (g.Destroyed) continue;
            if (g.Kind == GuardKind.Truck) { yield return g.GlobalPosition; continue; }
            var c = g.GlobalPosition;
            yield return c + new Vector3(g.Half.X + 3f, 0, 0);
            yield return c - new Vector3(g.Half.X + 3f, 0, 0);
            yield return c + new Vector3(0, 0, g.Half.Y + 3f);
            yield return c - new Vector3(0, 0, g.Half.Y + 3f);
        }
    }

    /// <summary>Dégâts d'un insecte à l'objectif (hôte).</summary>
    public void HostDamage(Guard g, float dmg)
    {
        if (Completed || g.Destroyed || W.Ended) return;
        g.SetHealth(Mathf.Max(0, g.Health - dmg));
        if (g.Health <= 0) Net.I.BroadcastGuardDestroyed(g.Id);
    }

    // ------------------------------------------------------------------ Fin d'objectif

    /// <summary>
    /// Hôte : objectif rempli. Plus d'échec possible ; 15 s pour ramasser les caisses, puis fin de mission
    /// (en Facile et Normal, les caisses restantes sont collectées automatiquement au bilan).
    /// </summary>
    public void HostComplete()
    {
        if (Completed) return;
        Completed = true;
        ExtractionLeft = ExtractionDelay;
        SendState();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (W == null || W.Ended) return;
        if (Net.I.IsHost) HostTick(dt);
        else ClientTick(dt);
        if (Log)
        {
            _logT -= dt;
            if (_logT <= 0)
            {
                _logT = 3f;
                GD.Print($"[objectif] t={W.Elapsed:0} {Describe()} · {W.EnemiesById.Count} ennemis · {W.ObjectiveText}");
            }
        }
    }

    private void HostTick(float dt)
    {
        if (Goal == MissionGoal.Escort) HostConvoy(dt);
        if (Completed && ExtractionLeft > 0)
        {
            ExtractionLeft = Mathf.Max(0, ExtractionLeft - dt);
            if (ExtractionLeft <= 0) W.EndMission(true);
        }
        _sendT -= dt;
        if (_sendT <= 0)
        {
            _sendT = 0.1f;
            SendState();
        }
    }

    private void ClientTick(float dt)
    {
        if (ExtractionLeft > 0) ExtractionLeft = Mathf.Max(0, ExtractionLeft - dt);
        if (SurviveLeft > 0 && !Completed) SurviveLeft = Mathf.Max(0, SurviveLeft - dt);
        foreach (var t in Trucks)
        {
            if (t.Destroyed) continue;
            // Interpolation vers l'abscisse reçue (10 Hz), sans jamais reculer.
            var gap = t.NetS - t.S;
            t.S = gap > 6f ? t.NetS : t.S + Mathf.Max(0, gap) * Mathf.Clamp(dt * 8f, 0, 1);
            PlaceTruck(t, Mathf.Clamp(dt * 3f, 0, 1));
        }
    }

    /// <summary>État de l'objectif (hôte → clients) : drapeaux, minuteurs, puis par élément : id, PV (16 bits), abscisse.</summary>
    private void SendState()
    {
        if (Net.I.IsSolo) return;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)((Completed ? 1 : 0) | (StopReason << 1)));
        w.Write(SurviveLeft);
        w.Write(ExtractionLeft);
        w.Write((byte)Guards.Count);
        foreach (var g in Guards)
        {
            w.Write((byte)g.Id);
            w.Write((ushort)Mathf.RoundToInt(Mathf.Clamp(g.Health / Mathf.Max(1f, g.MaxHealth), 0, 1) * 65535f));
            w.Write(g.S);
        }
        w.Flush();
        Net.I.BroadcastObjectiveState(ms.ToArray());
    }

    public void OnState(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);
        var flags = r.ReadByte();
        Completed = (flags & 1) != 0;
        StopReason = (flags >> 1) & 3;
        SurviveLeft = r.ReadSingle();
        ExtractionLeft = r.ReadSingle();
        int n = r.ReadByte();
        for (var i = 0; i < n; i++)
        {
            var id = r.ReadByte();
            var hp = r.ReadUInt16() / 65535f;
            var s = r.ReadSingle();
            var g = Guards.FirstOrDefault(x => x.Id == id);
            if (g == null) continue;
            g.SetHealth(hp * g.MaxHealth);
            g.NetS = s;
        }
    }

    /// <summary>Destruction d'un élément (tous les pairs) : explosion, épave (camion poussé sur le côté, mât de l'antenne qui s'abat).</summary>
    public void OnGuardDestroyed(int id)
    {
        var g = Guards.FirstOrDefault(x => x.Id == id);
        if (g == null || g.Destroyed) return;
        g.Destroyed = true;
        g.Health = 0;
        if (g.Beacon != null) g.Beacon.Visible = false;
        Effects.Explosion(W, g.GlobalPosition + Vector3.Up * 2f, g.Kind == GuardKind.Truck ? 7f : 10f, 0);
        Effects.Smoke(W, g.GlobalPosition + Vector3.Up * 2f, 4f, 6f);
        ObjectiveModels.Char(g.Model);
        if (g.Kind == GuardKind.Truck)
        {
            var tw = g.CreateTween();
            // L'épave est projetée sur le bas-côté (les camions suivants passent) et se couche à moitié.
            var side = new Vector3(Mathf.Cos(g.Yaw), 0, -Mathf.Sin(g.Yaw)) * 4.5f;
            tw.SetParallel();
            tw.TweenProperty(g, "position", g.Position + side, 0.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            tw.TweenProperty(g, "rotation", new Vector3(0, g.Yaw + 0.5f, 0.35f), 0.6f);
        }
        else if (g.Model.GetNodeOrNull<Node3D>("Mast") is { } mast)
        {
            // Pas de Tween sans mât (poste de commandement) : un Tween vide est une erreur moteur.
            var tw = g.CreateTween();
            tw.TweenInterval(0.4f);
            tw.TweenProperty(mast, "rotation", new Vector3(1.45f, 0, 0.2f), 2.2f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tw.TweenCallback(Callable.From(() => Effects.Dust(W, g.GlobalPosition + new Vector3(0, 0, g.Height * 0.6f), new Vector3(6, 4, g.Height))));
        }
        if (g.Body != null) g.Body.CollisionLayer = 0;
    }

    /// <summary>Résumé lisible (journal de test).</summary>
    public string Describe()
    {
        var parts = new List<string>();
        foreach (var g in Guards) parts.Add($"{g.Label} {(g.Destroyed ? "détruit" : $"{g.Health:0}/{g.MaxHealth:0}")}{(g.Kind == GuardKind.Truck ? $" s={g.S:0}" : "")}");
        if (Goal == MissionGoal.Escort) parts.Add($"convoi {ConvoyProgress * 100:0} % arrêt={StopReason}");
        if (SurviveLeft >= 0) parts.Add($"survie {SurviveLeft:0} s");
        if (ExtractionLeft >= 0) parts.Add($"extraction {ExtractionLeft:0.0} s");
        return string.Join(" · ", parts);
    }
}
