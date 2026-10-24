using Godot;

namespace Rempart;

/// <summary>
/// Repères de l'objectif, dans l'esprit du HUD minimal (DECISIONS.md : ni panneau d'objectif, ni notification, ni radio, ni barre de boss) :
/// <list type="bullet">
/// <item>structure défendue / camions du convoi : barre fine (5 px, sans graduations, comme les alliés) et nom au-dessus du modèle,
/// petit losange jaune ; hors champ, losange et distance maintenus sur le bord de l'écran (structure, ou camion de tête) ;</item>
/// <item>convoi arrêté faute d'escorte : « À L'ARRÊT » sous le nom du camion de tête (seule consigne implicite du mode) ;</item>
/// <item>point d'arrivée du convoi : losange et distance quand il est dans le champ ;</item>
/// <item>en haut au centre, en petit : compte à rebours de survie, puis d'extraction (15 s pour ramasser les caisses).</item>
/// </list>
/// </summary>
public partial class HudObjectiveMarkers : Control
{
    public World World;
    public float S = 1f;

    private const float MaxDist = 260f;
    private string _timerText = "";
    private int _timerSecs = -1;
    private bool _timerExtraction;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) QueueRedraw();
    }

    private void Txt(Font f, float size, string text, Vector2 center, float top, Color c)
    {
        var fs = Mathf.Max(7, Mathf.RoundToInt(size));
        var w = f.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X;
        var pos = new Vector2(Mathf.Round(center.X - w / 2), Mathf.Round(top + f.GetAscent(fs)));
        DrawStringOutline(f, pos, text, HorizontalAlignment.Left, -1, fs, Mathf.Max(2, Mathf.RoundToInt(size * 0.18f)), HudE.Ink);
        DrawString(f, pos, text, HorizontalAlignment.Left, -1, fs, c);
    }

    private void Diamond(Vector2 c, float r, Color col)
    {
        var pts = new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) };
        DrawColoredPolygon(pts, new Color(col, 0.35f));
        DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, col, Mathf.Max(1f, 1.5f * S), true);
    }

    public override void _Draw()
    {
        var o = World?.Objective;
        var cam = GetViewport()?.GetCamera3D();
        if (o == null || cam == null || World.LocalPlayer == null) return;
        var screen = GetViewportRect().Size;
        var eye = cam.GlobalPosition;
        var me = World.LocalPlayer.GlobalPosition;

        DrawTimer(o, screen);

        Guard edge = null;
        foreach (var g in o.Guards)
        {
            if (g.Destroyed) continue;
            var lead = g.Kind == GuardKind.Truck && g == o.LeadTruck;
            if (!DrawGuard(cam, eye, screen, g, lead && o.StopReason == 2 && !o.Completed) && (g.Kind == GuardKind.Structure || lead)) edge = g;
        }
        if (edge != null && !o.Completed)
            DrawEdge(cam, screen, edge.Top, $"{(edge.Kind == GuardKind.Truck ? "Convoi" : edge.Label)} · {Mathf.RoundToInt(me.DistanceTo(edge.GlobalPosition))} m");

        // Point de regroupement du convoi (dans le champ seulement).
        if (o.Goal == MissionGoal.Escort && !o.Completed)
        {
            var at = o.Arrival + Vector3.Up * 4f;
            var dist = me.DistanceTo(o.Arrival);
            if (dist > 25f && !cam.IsPositionBehind(at))
            {
                var sp = cam.UnprojectPosition(at);
                if (new Rect2(Vector2.Zero, screen).HasPoint(sp))
                {
                    Diamond(sp, 7f * S, HudE.Vehicle);
                    Txt(HudE.Mono, 12 * S, $"Arrivée · {Mathf.RoundToInt(dist)} m", sp, sp.Y + 10f * S, HudE.Pale);
                }
            }
        }
    }

    /// <summary>Barre fine + nom au-dessus du modèle ; renvoie faux s'il n'est pas dans le champ.</summary>
    private bool DrawGuard(Camera3D cam, Vector3 eye, Vector2 screen, Guard g, bool stopped)
    {
        var top = g.Top;
        var dist = eye.DistanceTo(top);
        if (cam.IsPositionBehind(top)) return false;
        var sp = cam.UnprojectPosition(top);
        if (!new Rect2(Vector2.Zero, screen).HasPoint(sp)) return false;
        if (dist > MaxDist) return true;
        var truck = g.Kind == GuardKind.Truck;
        var k = Mathf.Clamp(1.12f - dist / 140f, 0.7f, 1f) * S;
        var w = (truck ? 80f : 110f) * k;
        var h = Mathf.Max(3f, 5f * k);
        var bar = new Rect2(Mathf.Round(sp.X - w / 2), Mathf.Round(sp.Y - h), w, h);
        DrawRect(bar, new Color(0, 0, 0, 0.55f));
        var f = Mathf.Clamp(g.Health / Mathf.Max(1f, g.MaxHealth), 0, 1);
        var col = f < 0.25f ? HudE.Warn : HudE.Frame;
        if (g.Flash > 0) col = col.Lerp(Colors.White, g.Flash * 0.8f);
        if (f > 0) DrawRect(new Rect2(bar.Position, new Vector2(w * f, h)), col);
        Txt(HudE.Cond, 14 * k, g.Label, new Vector2(sp.X, 0), bar.Position.Y - 20 * k, Colors.White);
        Diamond(new Vector2(sp.X, bar.Position.Y - 27 * k), 5f * k, HudE.Vehicle);
        if (stopped) Txt(HudE.Mono, 12 * k, "À L'ARRÊT", new Vector2(sp.X, 0), bar.End.Y + 3 * k, HudE.Warn);
        return true;
    }

    /// <summary>Hors champ : losange et distance maintenus sur le bord de l'écran, dans la direction de l'objectif.</summary>
    private void DrawEdge(Camera3D cam, Vector2 screen, Vector3 pos, string text)
    {
        var sp = cam.UnprojectPosition(pos);
        var center = screen / 2;
        if (cam.IsPositionBehind(pos))
        {
            var d0 = sp - center;
            sp = center - (d0.LengthSquared() < 1 ? new Vector2(0, -1) : d0.Normalized()) * screen.Length();
        }
        // Bande des bords libres : sous l'armure et le radar, au-dessus de la pile d'armes.
        var minX = 70 * S; var maxX = screen.X - 70 * S;
        var minY = 260 * S; var maxY = screen.Y - 190 * S;
        if (maxX <= minX || maxY <= minY) return; // fenêtre minuscule (tests sans affichage)
        var d = sp - center;
        var tx = d.X != 0 ? ((d.X > 0 ? maxX : minX) - center.X) / d.X : float.MaxValue;
        var ty = d.Y != 0 ? ((d.Y > 0 ? maxY : minY) - center.Y) / d.Y : float.MaxValue;
        var t = Mathf.Min(Mathf.Abs(tx), Mathf.Abs(ty));
        sp = center + d * Mathf.Min(1f, t);
        sp = new Vector2(Mathf.Clamp(sp.X, minX, maxX), Mathf.Clamp(sp.Y, minY, maxY));
        Diamond(sp, 7f * S, HudE.Vehicle);
        Txt(HudE.Mono, 12 * S, text, sp, sp.Y + 10f * S, HudE.Pale);
    }

    /// <summary>Survie puis extraction : chiffres seuls, petits, en haut au centre (aucun panneau).</summary>
    private void DrawTimer(MissionObjective o, Vector2 screen)
    {
        var extraction = o.ExtractionLeft >= 0;
        var secs = extraction ? Mathf.CeilToInt(o.ExtractionLeft) : o.SurviveLeft >= 0 ? Mathf.CeilToInt(o.SurviveLeft) : -1;
        if (secs < 0) return;
        if (secs != _timerSecs || extraction != _timerExtraction)
        {
            _timerSecs = secs;
            _timerExtraction = extraction;
            _timerText = extraction ? $"EXTRACTION  {secs}" : $"TENIR  {secs / 60:00}:{secs % 60:00}";
        }
        var urgent = !extraction && secs <= 30;
        Txt(HudE.Mono, 17 * S, _timerText, new Vector2(screen.X / 2, 0), 14 * S, extraction ? HudE.Label : urgent ? HudE.Warn : HudE.Pale);
    }
}
