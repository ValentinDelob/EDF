using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Marqueurs projetés dans le monde (plein écran) :
/// <list type="bullet">
/// <item>coéquipiers (joueurs) : nom + barre fine de 5 px sans graduations au-dessus de la tête,
/// masquée hors écran / derrière la caméra / au-delà de 150 m, légèrement réduite avec la distance ;</item>
/// <item>escouades PNJ : barre seule, plus petite (40 × 3), sans nom, à moins de 45 m (repérage sans surcharger l'écran) ;</item>
/// <item>coéquipier à terre : « Nom · 18 m » / « Relever [E] », barre rouge vide (remplie en vert pendant qu'on le relève),
/// flèche et anneau ; maintenu sur le bord de l'écran quand il est hors champ ;</item>
/// <item>soi-même à terre : une ligne discrète au centre (seule information vitale conservée hors maquette).</item>
/// </list>
/// </summary>
public partial class HudWorldMarkers : Control
{
    public World World;
    public float S = 1f;

    private const float BarMaxDist = 150f, NpcMaxDist = 45f;
    private static readonly HudShape DownedBar = new(3, 0, 100, 0, 97, 8, 0, 8);
    private static readonly HudShape DownedArrow = new(48, 40, 72, 40, 60, 56);
    private readonly Dictionary<long, (int meters, string text)> _downedText = new();
    private readonly HashSet<long> _labelsHidden = new();
    private string _reviveText = "";
    private float _keyRefresh;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _keyRefresh -= (float)delta;
        if (_keyRefresh <= 0)
        {
            _keyRefresh = 2f;
            _reviveText = $"Relever [{UiKit.KeyName("interact")}]";
        }
        QueueRedraw();
    }

    private void Txt(Font f, float size, string text, Vector2 center, float top, Color c)
    {
        var fs = Mathf.Max(7, Mathf.RoundToInt(size));
        var w = f.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X;
        var pos = new Vector2(Mathf.Round(center.X - w / 2), Mathf.Round(top + f.GetAscent(fs)));
        DrawStringOutline(f, pos, text, HorizontalAlignment.Left, -1, fs, Mathf.Max(2, Mathf.RoundToInt(size * 0.18f)), HudE.Ink);
        DrawString(f, pos, text, HorizontalAlignment.Left, -1, fs, c);
    }

    public override void _Draw()
    {
        var p = World?.LocalPlayer;
        var cam = GetViewport()?.GetCamera3D();
        if (p == null || cam == null) return;
        var screen = GetViewportRect().Size;
        var eye = cam.GlobalPosition;
        var me = p.GlobalPosition;

        // Escouades PNJ : petite barre, sans nom.
        if (World.Allies != null)
            foreach (var a in World.Allies.ById.Values)
            {
                if (a.Dead) continue;
                var head = a.GlobalPosition + Vector3.Up * 2.1f;
                var dist = eye.DistanceTo(head);
                if (dist > NpcMaxDist || cam.IsPositionBehind(head)) continue;
                var sp = cam.UnprojectPosition(head);
                if (!new Rect2(Vector2.Zero, screen).HasPoint(sp)) continue;
                var k = Mathf.Clamp(1.1f - dist / 60f, 0.7f, 1f) * S;
                var w = 40f * k;
                var h = Mathf.Max(2f, 3f * k);
                var r = new Rect2(sp.X - w / 2, sp.Y - h, w, h);
                DrawRect(r, new Color(0, 0, 0, 0.55f));
                var f = Mathf.Clamp(a.Health / Mathf.Max(1f, a.MaxHealth), 0, 1);
                DrawRect(new Rect2(r.Position, new Vector2(w * f, h)), new Color(HudE.Ally, 0.9f));
            }

        foreach (var rp in World.Remotes.Values)
        {
            HideNameLabel(rp);
            if (rp.Downed) DrawDowned(p, cam, screen, me, rp.GlobalPosition, rp.PlayerName ?? "", rp.PeerId);
            else DrawMate(cam, eye, screen, rp.GlobalPosition, rp.PlayerName ?? "", rp.Health / Mathf.Max(1f, rp.MaxHealth));
        }
        if (p.Downed)
        {
            var t = Net.I.IsSolo && !p.SecondChanceUsed ? "À terre — un infirmier arrive" : "À terre — attendez un équipier";
            Txt(HudE.Cond, 20 * S, t, new Vector2(screen.X / 2, 0), screen.Y / 2 + 60 * S, HudE.DownedText);
        }
    }

    /// <summary>
    /// Le HUD affiche le nom (et l'état « à terre ») des coéquipiers : l'étiquette 3D de RemotePlayer
    /// ferait doublon, on la masque une fois pour chaque joueur.
    /// </summary>
    private void HideNameLabel(RemotePlayer rp)
    {
        if (!_labelsHidden.Add(rp.PeerId)) return;
        foreach (var c in rp.GetChildren())
            if (c is Label3D l) l.Visible = false;
    }

    /// <summary>Coéquipier debout : nom + barre fine (5 px, sans graduations) au-dessus de la tête.</summary>
    private void DrawMate(Camera3D cam, Vector3 eye, Vector2 screen, Vector3 pos, string name, float frac)
    {
        var head = pos + Vector3.Up * 2.2f;
        var dist = eye.DistanceTo(head);
        if (dist > BarMaxDist || cam.IsPositionBehind(head)) return;
        var sp = cam.UnprojectPosition(head);
        if (!new Rect2(Vector2.Zero, screen).HasPoint(sp)) return;
        var k = Mathf.Clamp(1.12f - dist / 100f, 0.7f, 1f) * S;
        var w = 100f * k;
        var h = Mathf.Max(3f, 5f * k);
        var bar = new Rect2(Mathf.Round(sp.X - w / 2), Mathf.Round(sp.Y - h), w, h);
        DrawRect(bar, new Color(0, 0, 0, 0.55f));
        var f = Mathf.Clamp(frac, 0, 1);
        var col = f < 0.25f ? HudE.Warn : HudE.Frame;
        if (f > 0) DrawRect(new Rect2(bar.Position, new Vector2(w * f, h)), col);
        Txt(HudE.Cond, 14 * k, name, new Vector2(sp.X, 0), bar.Position.Y - 20 * k, Colors.White);
    }

    private void DrawDowned(Player p, Camera3D cam, Vector2 screen, Vector3 me, Vector3 pos, string name, long id)
    {
        var anchor = pos + Vector3.Up * 0.4f;
        var meters = Mathf.RoundToInt(me.DistanceTo(pos));
        if (!_downedText.TryGetValue(id, out var cached) || cached.meters != meters)
        {
            cached = (meters, $"{name} · {meters} m");
            _downedText[id] = cached;
        }
        var s = S;
        var sp = cam.UnprojectPosition(anchor);
        var behind = cam.IsPositionBehind(anchor);
        var center = screen / 2;
        // Hors champ : direction projetée, puis maintien sur le bord de l'écran.
        if (behind)
        {
            var d = sp - center;
            sp = center - (d.LengthSquared() < 1 ? new Vector2(0, -1) : d.Normalized()) * screen.Length();
        }
        var minX = 64 * s; var maxX = screen.X - 64 * s;
        var minY = 76 * s; var maxY = screen.Y - 16 * s;
        if (behind || sp.X < minX || sp.X > maxX || sp.Y < minY || sp.Y > maxY)
        {
            var d = sp - center;
            var tx = d.X != 0 ? ((d.X > 0 ? maxX : minX) - center.X) / d.X : float.MaxValue;
            var ty = d.Y != 0 ? ((d.Y > 0 ? maxY : minY) - center.Y) / d.Y : float.MaxValue;
            var t = Mathf.Min(Mathf.Abs(tx), Mathf.Abs(ty));
            sp = center + d * Mathf.Min(1f, t);
            sp = new Vector2(Mathf.Clamp(sp.X, minX, maxX), Mathf.Clamp(sp.Y, minY, maxY));
        }
        // Boîte de la maquette 120 × 70, ancrée sur le centre de l'anneau (60, 62).
        var o = sp - new Vector2(60, 62) * s;
        DrawSetTransform(o, 0, new Vector2(s, s));
        DrawColoredPolygon(DownedArrow.Fill, HudE.Downed);
        DrawPolyline(DownedArrow.Outline, HudE.Ink, 1f, true);
        DrawArc(new Vector2(60, 62), 5, 0, Mathf.Tau, 20, HudE.Downed, 1.5f, true);
        DrawSetTransform(o + new Vector2(10, 36) * s, 0, new Vector2(s, s));
        DrawColoredPolygon(DownedBar.Fill, new Color(0, 0, 0, 0.6f));
        if (p.ReviveTarget == id && p.ReviveProgress > 0)
        {
            var w = 100 * Mathf.Clamp(p.ReviveProgress, 0, 1);
            DrawRect(new Rect2(1, 1, Mathf.Max(0, w - 2), 6), HudE.Frame);
        }
        DrawPolyline(DownedBar.Outline, HudE.DownedLine, 1f, true);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        Txt(HudE.Mono, 13 * s, cached.text, new Vector2(sp.X, 0), o.Y, Colors.White);
        Txt(HudE.Mono, 13 * s, _reviveText, new Vector2(sp.X, 0), o.Y + 18 * s, HudE.DownedText);
    }
}

/// <summary>Réticule discret : croix fine rouge (~16 px) et point central, marqueur de touche blanc.</summary>
public partial class HudReticle : Control
{
    public World World;
    public float S = 1f;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) QueueRedraw();
    }

    public override void _Draw()
    {
        var p = World?.LocalPlayer;
        if (p == null || p.Downed || (p.Weapon == null && p.Vehicle == null)) return;
        DrawSetTransform(Vector2.Zero, 0, new Vector2(S, S));
        // Légère ouverture selon la dispersion (reste discret : +4 px au plus).
        var gap = 4f;
        if (p.Vehicle == null && p.Weapon is { } ws)
            gap += Mathf.Clamp(ws.W.Spread * (p.Aiming ? 0.55f : 1f) * 1.2f, 0f, 4f);
        var c = new Color(HudE.Reticle, 0.9f);
        const float arm = 4f, wid = 1.4f;
        DrawLine(new Vector2(-gap - arm, 0), new Vector2(-gap, 0), c, wid, true);
        DrawLine(new Vector2(gap, 0), new Vector2(gap + arm, 0), c, wid, true);
        DrawLine(new Vector2(0, -gap - arm), new Vector2(0, -gap), c, wid, true);
        DrawLine(new Vector2(0, gap), new Vector2(0, gap + arm), c, wid, true);
        DrawCircle(Vector2.Zero, 1.1f, HudE.Reticle);
        if (p.HitMarker > 0)
        {
            var hc = new Color(1, 1, 1, 0.85f * Mathf.Clamp(p.HitMarker, 0, 1));
            var a = 3f + (1 - p.HitMarker) * 1.5f;
            var b = a + 2f;
            DrawLine(new Vector2(-b, -b), new Vector2(-a, -a), hc, wid, true);
            DrawLine(new Vector2(b, -b), new Vector2(a, -a), hc, wid, true);
            DrawLine(new Vector2(-b, b), new Vector2(-a, a), hc, wid, true);
            DrawLine(new Vector2(b, b), new Vector2(a, a), hc, wid, true);
        }
    }
}
