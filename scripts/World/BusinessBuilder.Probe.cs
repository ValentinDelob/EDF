using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Outil de développement « --bizprobe » : des capsules de la taille d'un soldat suivent des parcours (escaliers de
/// secours, rampes du parking, bretelle et travée effondrée du viaduc, métro) avec la vraie physique, et le journal
/// indique si elles arrivent à la hauteur attendue (« [probe] ... ok »). Vérifie que les collisions suivent le visuel.
/// </summary>
public static partial class BusinessBuilder
{
    private static void AttachProbes(Ctx c)
    {
        if (Screenshot.CmdArg("bizprobe") == null) return;
        var walkers = new List<BizWalker>();
        // Escaliers de secours des deux pavillons du parvis.
        walkers.Add(StairWalker("escalier pavillon ouest", new Vector3(-46, Walk, 32), new Vector3(20, 3 * 3.2f + 0.6f, 14), 2));
        walkers.Add(StairWalker("escalier pavillon est", new Vector3(46, Walk, 34), new Vector3(18, 2 * 3.2f + 0.6f, 14), 3));
        // Parking (100, -35) : quatre rampes jusqu'au toit-terrasse.
        var pk = new Vector3(100, Walk, -35);
        walkers.Add(new BizWalker
        {
            Label = "rampes du parking", GoalY = Walk + 4 * Building.ParkingLevel,
            Path = new List<Vector3> { pk + new Vector3(-21.4f, 0, -12.5f), pk + new Vector3(-14, 0, -12.5f), pk + new Vector3(15, 0, -12.5f), pk + new Vector3(15, 0, 12.5f),
                pk + new Vector3(-15, 0, 12.5f), pk + new Vector3(-15, 0, -12.5f), pk + new Vector3(15, 0, -12.5f), pk + new Vector3(15, 0, 12.5f), pk + new Vector3(-15, 0, 12.5f) },
        });
        // Bretelle du viaduc puis tablier.
        var rz = (RampZ0 + RampZ1) / 2;
        walkers.Add(new BizWalker
        {
            Label = "bretelle du viaduc", GoalY = DeckTop,
            Path = new List<Vector3> { new(RampX0 - 8f, 0, rz), new(RampX1, 0, rz), new(RampJoin - 4f, 0, rz), new(RampJoin - 4f, 0, HighwayZ + 3f), new(RampJoin + 20f, 0, HighwayZ + 3f) },
        });
        // Travée effondrée : de la chaussée au tablier ouest.
        walkers.Add(new BizWalker
        {
            Label = "travée effondrée", GoalY = DeckTop,
            Path = new List<Vector3> { new(186f, 0, 131.5f), new(150f, 0, 131.5f), new(132f, 0, 131.5f) },
        });
        // Métro : descente jusqu'au palier bas.
        var mx = MetroPit.GetCenter().X;
        walkers.Add(new BizWalker
        {
            Label = "escalier du métro", GoalY = -MetroDepth,
            Path = new List<Vector3> { new(mx, 0, MetroPit.Position.Y - 4f), new(mx, 0, MetroPit.Position.Y + 2f), new(mx, 0, MetroPit.End.Y - 1.2f) },
        });
        // Référence : monter sur un trottoir (bordure de 15 cm) depuis la chaussée.
        walkers.Add(new BizWalker { Label = "bordure de trottoir", GoalY = Walk, Path = new List<Vector3> { new(70, 0, -100), new(81, 0, -100) } });
        foreach (var w in walkers)
        {
            w.GlobalPositionOnReady = w.Path[0] + Vector3.Up * 1.2f;
            c.Root.AddChild(w);
        }
        // Tour debout de l'angle nord-est du parvis : des impacts répétés arrachent des panneaux, sans effondrement.
        foreach (var t in c.Towers)
            if (t.Position.DistanceTo(new Vector3(46, Walk, -46)) < 1f) c.Root.AddChild(new BizHitter { Target = t });
    }

    /// <summary>Parcours d'un escalier de secours (même géométrie que <c>Building.BuildFireEscape</c>), jusqu'au toit.</summary>
    private static BizWalker StairWalker(string label, Vector3 pos, Vector3 s, int facing)
    {
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var (start, along, normal, len) = facing switch
        {
            0 => (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, s.X),
            1 => (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, s.X),
            2 => (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, s.Z),
            _ => (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, s.Z),
        };
        const float run = 5.2f, laneW = 1.1f;
        var flights = Mathf.CeilToInt((s.Y + 0.95f) / 3.2f);
        var aA = len / 2 - run / 2;
        var aB = len / 2 + run / 2;
        Vector3 W(float a, float o) => pos + start + along * a + normal * o;
        var inner = 0.2f + laneW / 2;
        var outer = 0.2f + laneW + 0.05f + laneW / 2;
        var path = new List<Vector3> { W(aA - 1.6f, inner) };
        for (var k = 0; k < flights; k++)
        {
            var lane = k % 2 == 0 ? inner : outer;
            var to = k % 2 == 0 ? aB + 0.7f : aA - 0.7f;
            path.Add(W(to, lane));
            if (k + 1 < flights) path.Add(W(to, k % 2 == 0 ? outer : inner));
        }
        var end = flights % 2 == 1 ? aB + 0.7f : aA - 0.7f;
        path.Add(W(end, -2.5f));
        return new BizWalker { Label = label, Path = path, GoalY = pos.Y + s.Y };
    }
}

/// <summary>Outil « --bizprobe » : impacts répétés (hôte) sur une tour debout, puis bilan (paliers, état).</summary>
public partial class BizHitter : Node
{
    public Building Target;
    private float _t, _next = 6f;
    private int _hits;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t < _next || Target == null || World.Current == null || !Net.I.IsHost) return;
        _next += 0.5f;
        if (_hits++ < 12)
        {
            // Façade sud (vers le parvis), à des hauteurs variées : 30 % des PV par impact (un immeuble ordinaire tomberait au quatrième).
            var p = Target.GlobalPosition + new Vector3((_hits % 5 - 2) * 3f, 12f + _hits * 6f, Target.Size.Z / 2 + 0.5f);
            World.Current.HostBuildingDamage(Target.Id, Target.MaxHealth * 0.3f, p);
            return;
        }
        GD.Print($"[probe] tour debout n°{Target.Id} : {(Target.Destroyed ? "ÉCHEC (effondrée)" : "ok (toujours debout)")}, {Target.HostStage} arrachements de panneaux, PV {Target.Health:0}/{Target.MaxHealth:0}");
        SetProcess(false);
    }
}

/// <summary>Capsule de test qui suit un parcours avec la physique du jeu (outil « --bizprobe »).</summary>
public partial class BizWalker : CharacterBody3D
{
    public string Label;
    public List<Vector3> Path;
    public float GoalY;
    public Vector3 GlobalPositionOnReady;
    private int _i = 1;
    private float _t, _stuckT;
    private Vector3 _last;
    private bool _done;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        FloorMaxAngle = Mathf.DegToRad(46f);
        FloorSnapLength = 0.4f;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.8f }, Position = Vector3.Up * 0.9f });
        GlobalPosition = GlobalPositionOnReady;
        _last = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done) return;
        var dt = (float)delta;
        _t += dt;
        if (_t < 2f) { Velocity = Vector3.Down; MoveAndSlide(); return; } // laisser le décor se poser
        var target = Path[_i];
        var d = target - GlobalPosition;
        d.Y = 0;
        if (d.Length() < 0.35f)
        {
            _i++;
            if (_i >= Path.Count) { Finish(); return; }
            return;
        }
        // Même schéma que le soldat : vitesse verticale conservée au sol, gravité en l'air.
        var v = d.Normalized() * 3.5f;
        v.Y = IsOnFloor() ? Velocity.Y : Velocity.Y - 20f * dt;
        Velocity = v;
        MoveAndSlide();
        _stuckT += dt;
        if (_stuckT > 3f)
        {
            if (GlobalPosition.DistanceTo(_last) < 0.3f)
            {
                GD.Print($"[probe] {Label} : BLOQUÉ à {GlobalPosition.Snapped(Vector3.One * 0.01f)} (étape {_i}/{Path.Count - 1}, vers {target.Snapped(Vector3.One * 0.1f)})");
                _done = true;
                return;
            }
            _stuckT = 0f;
            _last = GlobalPosition;
        }
        if (_t > 90f) { GD.Print($"[probe] {Label} : DÉLAI dépassé à {GlobalPosition.Snapped(Vector3.One * 0.01f)}"); _done = true; }
    }

    private void Finish()
    {
        _done = true;
        var ok = Mathf.Abs(GlobalPosition.Y - GoalY) < 0.6f;
        GD.Print($"[probe] {Label} : {(ok ? "ok" : "ÉCHEC")} — arrivée à {GlobalPosition.Snapped(Vector3.One * 0.01f)} (hauteur attendue {GoalY:0.00})");
    }
}
