using System.Globalization;
using Godot;

namespace Rempart;

/// <summary>
/// Tests des objectifs Défense / Escorte / Survie (arguments après « -- ») :
/// --survive-time=S (durée de survie), --obj-hp=F (PV de la structure et des camions × F), --convoy-speed=F (vitesse du convoi × F),
/// --objlog (journal « [objectif] » toutes les 3 s), --timescale=F (accélère le temps du jeu, tests sans affichage).
/// Avec --bot en mission d'escorte, le soldat suit le convoi (replacé à ~12 m du camion de tête dès qu'il s'en écarte).
/// </summary>
public static partial class AutoTest
{
    private static float FloatArg(string name, float fallback) =>
        Screenshot.CmdArg(name) is { } s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static void ObjectiveArgs(App app)
    {
        MissionObjective.SurviveOverride = FloatArg("survive-time", -1f);
        MissionObjective.HpMul = FloatArg("obj-hp", 1f);
        MissionObjective.SpeedMul = FloatArg("convoy-speed", 1f);
        MissionObjective.Log = Screenshot.CmdArg("objlog") != null;
        var scale = FloatArg("timescale", 1f);
        if (scale > 0 && !Mathf.IsEqualApprox(scale, 1f))
        {
            Engine.TimeScale = scale;
            Engine.MaxPhysicsStepsPerFrame = Mathf.Max(8, Mathf.CeilToInt(8 * scale));
        }
        if (Screenshot.CmdArg("bot") == null) return;

        void Follow()
        {
            app.GetTree().CreateTimer(0.5f).Timeout += Follow;
            var w = app.CurrentWorld;
            var p = w?.LocalPlayer;
            if (p == null || p.Downed || p.Vehicle != null || w.Objective == null) return;
            Vector3 spot;
            if (w.Objective.Goal == MissionGoal.Escort && w.Objective.LeadTruck is { } lead)
            {
                var fwd = new Vector3(Mathf.Sin(lead.Yaw), 0, Mathf.Cos(lead.Yaw));
                var right = new Vector3(Mathf.Cos(lead.Yaw), 0, -Mathf.Sin(lead.Yaw));
                spot = lead.GlobalPosition - fwd * 6f + right * 6f;
            }
            // Défense : le bot (qui esquive en pas chassés) reste à portée de tir de la structure.
            else if (w.Objective.Goal == MissionGoal.Defense && w.Objective.Structure is { Destroyed: false } s)
                spot = s.GlobalPosition + new Vector3(0, 0, s.Half.Y + 10f);
            else return;
            if (p.GlobalPosition.DistanceTo(spot) > 18f) p.GlobalPosition = spot + Vector3.Up * 1f;
        }
        Follow();
    }
}
