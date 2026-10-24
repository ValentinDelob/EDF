using Godot;

namespace Rempart;

/// <summary>
/// Alerte de saisie (soldat pris dans les mandibules d'une fourmi rouge) : plaque discrète sous le réticule,
/// liseré rouge qui pulse à chaque morsure, « SAISI — tirez sur la fourmi ! ». Coordonnées de maquette autour du centre de l'écran.
/// </summary>
public partial class HudGrab : HudBlock
{
    private static readonly HudShape PlateShape = new(-150, 0, 150, 0, 160, 10, 160, 24, 150, 34, -150, 34, -160, 24, -160, 10);
    private const float Top = 150f;
    private float _show, _pulse, _lastHp = -1;

    public HudGrab()
    {
        LogicalSize = new Vector2(0, 0);
    }

    /// <summary>Centre du bloc = centre de l'écran (le dessin est relatif à ce point).</summary>
    public void Center(float s, Vector2 screen)
    {
        S = s;
        Position = (screen * 0.5f).Round();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var p = World?.LocalPlayer;
        if (p == null || !IsVisibleInTree()) return;
        var dt = (float)delta;
        var grabbed = p.GrabbedBy != null && IsInstanceValid(p.GrabbedBy) && !p.Downed;
        _show = Mathf.MoveToward(_show, grabbed ? 1f : 0f, dt * (grabbed ? 6f : 3f));
        // Chaque morsure (perte d'armure) ravive le liseré.
        if (grabbed && p.Health < _lastHp - 0.01f) _pulse = 1f;
        _lastHp = p.Health;
        _pulse = Mathf.Max(0, _pulse - dt * 2.5f);
        if (_show > 0.001f || Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_show <= 0.001f) return;
        var a = _show;
        Shapes(new Vector2(0, Top));
        var line = HudE.DownedLine.Lerp(new Color(1f, 0.85f, 0.75f), _pulse * 0.6f);
        line.A = a;
        Plate(PlateShape, new Color(0.18f, 0.02f, 0.01f, 0.62f * a), line, 1.6f + _pulse * 1.4f);
        // Chevrons d'alerte de part et d'autre.
        var c = new Color(HudE.Downed, a * (0.6f + 0.4f * Mathf.Sin(Time.GetTicksMsec() * 0.012f)));
        DrawColoredPolygon(HudE.Pts(-150, 9, -140, 17, -150, 25), c);
        DrawColoredPolygon(HudE.Pts(150, 9, 140, 17, 150, 25), c);
        var txt = new Color(HudE.DownedText.Lerp(Colors.White, _pulse * 0.5f), a);
        Text(HudE.CondBold, 18, "SAISI — TIREZ SUR LA FOURMI !", -150, 8, 300, HorizontalAlignment.Center, txt, origin: new Vector2(0, Top));
    }
}
