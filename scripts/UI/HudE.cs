using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// HUD « direction E » (ADN EDF, cf. docs/maquettes/DECISIONS.md et project/E-HUD.dc.html) :
/// jetons de couleur, polices et outils de dessin communs aux blocs du HUD.
/// Toutes les coordonnées des blocs sont celles de la maquette (écran 1280×720) ; le facteur
/// <see cref="HudBlock.S"/> les met à l'échelle de la fenêtre (et de l'option « Échelle du HUD »).
/// Les formes sont dessinées sous une transformation d'échelle, le texte à sa taille réelle (net).
/// </summary>
public static class HudE
{
    public static readonly Color Frame = new("46E86B");
    public static readonly Color Line = new("2C9C48");
    public static readonly Color Plate = new(6 / 255f, 30 / 255f, 14 / 255f, 0.62f);
    public static readonly Color PlateDeep = new(6 / 255f, 30 / 255f, 14 / 255f, 0.66f);
    public static readonly Color Gauge = new("33C653");
    public static readonly Color GaugeHi = new(0xA6 / 255f, 1f, 0xB4 / 255f, 0.55f);
    public static readonly Color Tick = new("7CFF8E");
    public static readonly Color Ring = new("2FB24E");
    public static readonly Color Ink = new("021008");
    public static readonly Color Label = new("9CFFB0");
    public static readonly Color Pale = new("D8F5DD");
    public static readonly Color NameText = new("E8F5EA");
    public static readonly Color Enemy = new("FF3A2A");
    public static readonly Color Ally = new("5FD0FF");
    /// <summary>Véhicules sur le radar (jaune).</summary>
    public static readonly Color Vehicle = new("FFD43A");
    public static readonly Color Downed = new("FF6E5A");
    public static readonly Color DownedText = new("FF8C7A");
    public static readonly Color DownedLine = new("E8432E");
    public static readonly Color Reticle = new("FF2A1E");
    /// <summary>Alerte (armure basse, chargeur vide) : ambre, distinct du vert même pour un daltonien.</summary>
    public static readonly Color Warn = new("FFB13B");
    public static readonly Color Empty = new("FF5A3C");
    public static readonly Color Reload = new("FFF1A8");
    public static readonly Color Icon = new("3BE160");
    public static readonly Color IconLine = new("0E5A22");

    private static Font _mono, _cond, _condBold;
    private static readonly Dictionary<int, FontVariation> Spaced = new();
    private static ImageTexture _dot;

    /// <summary>Disque blanc (16 px, bord lissé) : points du radar dessinés en rectangles texturés, regroupés par le rendu 2D.</summary>
    public static Texture2D Dot
    {
        get
        {
            if (_dot != null) return _dot;
            const int n = 16;
            var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                // Couverture du pixel (4 × 4 échantillons) : bord du disque sans crénelage.
                var cov = 0;
                for (var sy = 0; sy < 4; sy++)
                for (var sx = 0; sx < 4; sx++)
                {
                    var dx = x + (sx + 0.5f) / 4f - n / 2f;
                    var dy = y + (sy + 0.5f) / 4f - n / 2f;
                    if (dx * dx + dy * dy <= n * n / 4f) cov++;
                }
                img.SetPixel(x, y, new Color(1, 1, 1, cov / 16f));
            }
            return _dot = ImageTexture.CreateFromImage(img);
        }
    }

    private static Font Load(string path, Font fallback)
    {
        var f = ResourceLoader.Exists(path) ? ResourceLoader.Load<FontFile>(path) : null;
        return f ?? fallback;
    }

    /// <summary>« Share Tech Mono » : chiffres et textes techniques.</summary>
    public static Font Mono => _mono ??= Load("res://assets/fonts/ShareTechMono-Regular.ttf", new SystemFont { FontNames = new[] { "Consolas", "Courier New" } });
    /// <summary>« Saira Condensed » demi-gras (500-600 de la maquette).</summary>
    public static Font Cond => _cond ??= Load("res://assets/fonts/SairaCondensed-SemiBold.ttf", UiKit.HeadSemi);
    /// <summary>« Saira Condensed » gras (700).</summary>
    public static Font CondBold => _condBold ??= Load("res://assets/fonts/SairaCondensed-Bold.ttf", UiKit.HeadFont);

    /// <summary>Saira demi-gras avec interlettrage (letter-spacing de la maquette, en px écran).</summary>
    public static Font CondSpaced(int px)
    {
        if (px <= 0) return Cond;
        if (!Spaced.TryGetValue(px, out var v))
        {
            v = new FontVariation { BaseFont = Cond };
            v.SetSpacing(TextServer.SpacingType.Glyph, px);
            Spaced[px] = v;
        }
        return v;
    }

    /// <summary>Facteur d'échelle : maquette 1280×720 ajustée à la fenêtre, puis option « Échelle du HUD ».</summary>
    public static float ScaleFor(Vector2 screen)
        => Mathf.Min(screen.X / 1280f, screen.Y / 720f) * Mathf.Clamp(Settings.I.HudScale, 0.8f, 1.2f);

    /// <summary>« 1 120 » : milliers séparés par une espace, comme dans la maquette.</summary>
    public static string Thousands(int n)
    {
        if (n < 0) n = 0;
        return n < 1000 ? n.ToString() : $"{n / 1000} {n % 1000:000}";
    }

    public static Vector2[] Pts(params float[] xy)
    {
        var a = new Vector2[xy.Length / 2];
        for (var i = 0; i < a.Length; i++) a[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return a;
    }

    /// <summary>Contour fermé (premier point répété) pour DrawPolyline.</summary>
    public static Vector2[] Closed(Vector2[] p)
    {
        var a = new Vector2[p.Length + 1];
        p.CopyTo(a, 0);
        a[p.Length] = p[0];
        return a;
    }

    public static Vector2[] Rect(float x, float y, float w, float h) => Pts(x, y, x + w, y, x + w, y + h, x, y + h);
}

/// <summary>Plaque à contour (polygone rempli + liseré), formes statiques précalculées.</summary>
public readonly struct HudShape
{
    public readonly Vector2[] Fill, Outline;
    public HudShape(params float[] xy)
    {
        Fill = HudE.Pts(xy);
        Outline = HudE.Closed(Fill);
    }
}

/// <summary>Bloc du HUD positionné dans un coin de l'écran, en coordonnées de maquette.</summary>
public abstract partial class HudBlock : Control
{
    public World World;
    /// <summary>Échelle maquette → pixels écran.</summary>
    public float S = 1f;
    /// <summary>Taille du bloc dans la maquette.</summary>
    protected Vector2 LogicalSize;
    /// <summary>Marge (maquette) depuis le coin d'ancrage.</summary>
    protected Vector2 Margin;
    protected bool AtRight, AtBottom;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Layout(float s, Vector2 screen)
    {
        S = s;
        Size = LogicalSize * s;
        var x = AtRight ? screen.X - (Margin.X + LogicalSize.X) * s : Margin.X * s;
        var y = AtBottom ? screen.Y - (Margin.Y + LogicalSize.Y) * s : Margin.Y * s;
        Position = new Vector2(Mathf.Round(x), Mathf.Round(y));
        QueueRedraw();
    }

    /// <summary>Repère des formes : coordonnées de maquette décalées de <paramref name="origin"/>.</summary>
    protected void Shapes(Vector2 origin = default) => DrawSetTransform(origin * S, 0, new Vector2(S, S));

    protected void Plate(in HudShape sh, Color fill, Color line, float width)
    {
        DrawColoredPolygon(sh.Fill, fill);
        DrawPolyline(sh.Outline, line, width, true);
    }

    /// <summary>
    /// Texte à la manière de la maquette : boîte (x, top, largeur) en px de maquette, alignement,
    /// contour sombre (text-shadow). <paramref name="tight"/> = line-height 1 ; sinon line-height normal.
    /// Le repère des formes courant est rétabli ensuite (origine <paramref name="origin"/>).
    /// </summary>
    protected void Text(Font font, int size, string text, float x, float top, float width, HorizontalAlignment align, Color color,
        bool tight = true, bool outline = true, Vector2 origin = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var fs = Mathf.Max(6, Mathf.RoundToInt(size * S));
        var asc = font.GetAscent(fs);
        var desc = font.GetDescent(fs);
        var baseline = tight ? (fs - (asc + desc)) * 0.5f + asc : asc;
        var pos = new Vector2((origin.X + x) * S, (origin.Y + top) * S + baseline);
        var w = width > 0 ? width * S : -1;
        if (outline)
        {
            DrawStringOutline(font, pos + new Vector2(0, 1), text, align, w, fs, Mathf.Max(3, Mathf.RoundToInt(4 * S)), new Color(0, 0, 0, 0.35f));
            DrawStringOutline(font, pos, text, align, w, fs, Mathf.Max(2, Mathf.RoundToInt(2.5f * S)), HudE.Ink);
        }
        DrawString(font, pos, text, align, w, fs, color);
        Shapes(origin);
    }

    /// <summary>Étiquette pleine (« Balles », « Roquettes »…) : rectangle vert, texte sombre centré.</summary>
    protected void Tab(string text, float x, float y, float w, float h, int size, Vector2 origin = default)
    {
        DrawRect(new Rect2(x, y, w, h), HudE.Frame);
        Text(HudE.CondBold, size, text, x, y + (h - size) * 0.5f, w, HorizontalAlignment.Center, HudE.Ink, true, false, origin);
    }
}

/// <summary>Armure (haut gauche) : cadre EDF cranté, onglet « Armure », jauge graduée, PV actuels / max.</summary>
public partial class HudArmor : HudBlock
{
    private static readonly HudShape FrameShape = new(1, 30, 11, 20, 92, 20, 100, 12, 200, 12, 208, 20, 391, 20, 399, 28, 399, 58, 391, 66, 160, 66, 150, 76, 150, 100, 141, 109, 11, 109, 1, 99);
    private static readonly HudShape VehShape = new(168, 72, 399, 72, 399, 98, 391, 106, 160, 106, 160, 80);
    private const float GaugeX = 21, GaugeW = 362;

    private float _shown = -1, _ghost = -1, _ghostHold, _flash, _lastHp = -1;
    private int _curInt = -1, _maxInt = -1, _vehInt = -1;
    private string _cur = "", _max = "", _veh = "";

    public HudArmor()
    {
        LogicalSize = new Vector2(400, 110);
        Margin = new Vector2(64, 52);
    }

    public override void _Process(double delta)
    {
        var p = World?.LocalPlayer;
        if (p == null || !IsVisibleInTree()) return;
        var dt = (float)delta;
        var hp = Mathf.Max(0, p.Health);
        if (_shown < 0) { _shown = _ghost = _lastHp = hp; }
        if (hp < _lastHp - 0.01f)
        {
            // Dégâts : la partie perdue reste visible un instant (rouge) puis se vide.
            _flash = 1f;
            _ghost = Mathf.Max(_ghost, _shown);
            _ghostHold = 0.45f;
        }
        _lastHp = hp;
        // Descente fluide (ou remontée lors d'un soin).
        _shown = Mathf.Lerp(_shown, hp, 1f - Mathf.Exp(-dt * 14f));
        if (Mathf.Abs(_shown - hp) < 0.5f) _shown = hp;
        _ghostHold -= dt;
        if (_ghostHold <= 0) _ghost = Mathf.Lerp(_ghost, _shown, 1f - Mathf.Exp(-dt * 5f));
        if (_ghost < _shown) _ghost = _shown;
        _flash = Mathf.Max(0, _flash - dt * 3f);

        var cur = Mathf.CeilToInt(hp);
        var max = Mathf.CeilToInt(p.MaxHealth);
        if (cur != _curInt) { _curInt = cur; _cur = HudE.Thousands(cur); }
        if (max != _maxInt) { _maxInt = max; _max = HudE.Thousands(max); }
        if (p.Vehicle is { } v)
        {
            var vi = Mathf.CeilToInt(Mathf.Max(0, v.Health));
            if (vi != _vehInt) { _vehInt = vi; _veh = HudE.Thousands(vi); }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var p = World?.LocalPlayer;
        if (p == null) return;
        Shapes();
        var max = Mathf.Max(1f, p.MaxHealth);
        var frac = Mathf.Clamp(_shown / max, 0, 1);
        var ghost = Mathf.Clamp(_ghost / max, 0, 1);
        var low = p.Health / max < 0.25f;
        var pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() * 0.008f) : 0f;

        var frameCol = HudE.Frame.Lerp(HudE.Downed, _flash * 0.85f);
        Plate(FrameShape, HudE.Plate, frameCol, 1.8f);
        DrawRect(new Rect2(-1, 44, 7, 38), low ? HudE.Frame.Lerp(HudE.Warn, pulse) : frameCol);

        // Jauge : fond, partie perdue, remplissage, reflet, graduations.
        DrawRect(new Rect2(20, 30, 364, 26), new Color(0, 0, 0, 0.6f));
        var fillW = GaugeW * frac;
        if (ghost > frac + 0.001f)
            DrawRect(new Rect2(GaugeX + fillW, 31, GaugeW * (ghost - frac), 24), new Color(HudE.Downed, 0.85f));
        if (fillW > 0.5f)
        {
            var fill = low ? HudE.Warn : HudE.Gauge;
            DrawRect(new Rect2(GaugeX, 31, fillW, 24), fill.Lerp(Colors.White, _flash * 0.35f));
            DrawRect(new Rect2(GaugeX, 31, fillW, 7), HudE.GaugeHi);
        }
        DrawRect(new Rect2(20, 30, 364, 26), HudE.Line, false, 1f);
        var dark = new Color(HudE.Ink, 0.45f);
        for (var x = 20f; x < 384f; x += 10.1f) DrawRect(new Rect2(x, 42, 1.2f, 14), dark);
        var major = new Color(HudE.Tick, 0.9f);
        for (var x = 20f; x < 384f; x += 36.5f) DrawRect(new Rect2(x, 34, 1.5f, 22), major);
        for (var x = GaugeX + fillW; x < 384f; x += 9.9f) DrawRect(new Rect2(x, 32, 1, 4), HudE.Line);
        DrawRect(new Rect2(56, 80, 88, 1.4f), HudE.Line);

        Text(HudE.CondSpaced(Mathf.Max(1, Mathf.RoundToInt(S))), 13, "Armure", 100, 9.5f, 108, HorizontalAlignment.Center, HudE.Label);
        Text(HudE.CondBold, 18, "PV", 14, 78, 0, HorizontalAlignment.Left, HudE.Tick, false);
        Text(HudE.Mono, 22, _cur, 44, 59, 98, HorizontalAlignment.Right, low ? HudE.Warn : Colors.White);
        Text(HudE.Mono, 20, _max, 44, 86, 98, HorizontalAlignment.Right, HudE.Pale);

        // En véhicule : blindage du véhicule dans une petite plaque sous la jauge.
        if (p.Vehicle is { } v)
        {
            var vf = Mathf.Clamp(v.Health / Mathf.Max(1f, v.MaxHealth), 0, 1);
            var vlow = vf < 0.3f;
            Plate(VehShape, HudE.Plate, HudE.Frame, 1.4f);
            Text(HudE.Cond, 13, "Blindage", 172, 75, 0, HorizontalAlignment.Left, HudE.Label);
            Text(HudE.Mono, 16, _veh, 290, 75, 100, HorizontalAlignment.Right, vlow ? HudE.Warn : Colors.White);
            DrawRect(new Rect2(172, 94, 218, 6), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(172, 94, 218 * vf, 6), vlow ? HudE.Warn : HudE.Gauge);
            DrawRect(new Rect2(172, 94, 218, 6), HudE.Line, false, 1f);
        }
        else if (p.Def.Id == SoldierClass.Support) DrawCallCredits(p);
    }

    private int _credInt = -1, _credChoice = -1, _credState = -1;
    private string _cred = "", _credLabel = "";

    /// <summary>
    /// Opérateur d'appui à pied : crédits d'appel dans la petite plaque sous la jauge (même emplacement que le blindage d'un véhicule).
    /// Jauge jusqu'au plafond, repères du barrage (capacité) et du véhicule choisi, appui long sur la touche d'appel en surimpression.
    /// </summary>
    private void DrawCallCredits(Player p)
    {
        var cost = CallIns.Costs[p.CallChoice];
        var busy = p.CalledVehicleBusy;
        var state = busy ? 2 : p.CallCredits >= cost ? 1 : 0;
        var credits = Mathf.FloorToInt(p.CallCredits);
        if (credits != _credInt) { _credInt = credits; _cred = credits.ToString(); }
        if (p.CallChoice != _credChoice || state != _credState)
        {
            _credChoice = p.CallChoice;
            _credState = state;
            var name = CallIns.Names[p.CallChoice];
            _credLabel = busy ? $"{name} engagé" : $"{name} {cost:0}";
        }
        var ready = state == 1;
        Plate(VehShape, HudE.Plate, ready ? HudE.Frame : HudE.Line, 1.4f);
        Text(HudE.Cond, 13, "Appui", 172, 75, 0, HorizontalAlignment.Left, HudE.Label);
        Text(HudE.Cond, 13, _credLabel, 214, 75, 0, HorizontalAlignment.Left, ready ? Colors.White : HudE.Pale);
        Text(HudE.Mono, 16, _cred, 290, 75, 100, HorizontalAlignment.Right, ready ? HudE.Tick : Colors.White);
        var frac = Mathf.Clamp(p.CallCredits / CallIns.Cap, 0, 1);
        DrawRect(new Rect2(172, 94, 218, 6), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(172, 94, 218 * frac, 6), ready ? HudE.Gauge : HudE.Line);
        var hold = p.CallHold;
        if (hold > 0) DrawRect(new Rect2(172, 94, 218 * hold, 6), HudE.Reload);
        DrawRect(new Rect2(172, 94, 218, 6), HudE.Line, false, 1f);
        // Repères : barrage d'artillerie (petit) et véhicule choisi (grand).
        DrawRect(new Rect2(172 + 218 * CallIns.BarrageCost / CallIns.Cap - 0.5f, 92, 1.2f, 10), new Color(HudE.Label, 0.7f));
        DrawRect(new Rect2(172 + 218 * cost / CallIns.Cap - 0.75f, 90, 1.6f, 14), ready ? HudE.Tick : HudE.Pale);
    }
}

/// <summary>
/// Radar (haut droite) : anneau segmenté épais, couronne de graduations, cercles, croix, cône de vision
/// (le radar tourne avec la caméra : haut = devant), ennemis rouges (hors portée : triangles sur le bord),
/// alliés bleus (joueurs et escouades PNJ), allié à terre cerclé, véhicules jaunes (hélicoptère = losange à rotor,
/// char = carré ; plein = libre, contour = piloté par un coéquipier, hors portée : triangle jaune), flèche du joueur, repère « N ».
/// </summary>
public partial class HudRadar : HudBlock
{
    private const float Range = 120f;
    private const float Inner = 82f;   // rayon utile (intérieur de la couronne de graduations)
    private const float RimR = 98f;    // triangles hors portée
    private const int MaxOffRange = 48;
    private static readonly Vector2 C = new(108, 108);

    private static readonly HudShape Bracket = new(200, 10, 214, 10, 230, 26, 230, 190, 214, 206, 200, 206, 200, 196, 210, 196, 220, 186, 220, 30, 210, 20, 200, 20);
    private static readonly HudShape Arrow = new(108, 100, 114, 114, 108, 111, 102, 114);
    private static readonly Vector2[] Ticks = BuildTicks();
    private static readonly Vector2[] Cone = BuildCone();
    private static readonly Vector2[] ConeLine = HudE.Closed(Cone);
    private readonly Vector2[] _tri = new Vector2[3];

    public HudRadar()
    {
        LogicalSize = new Vector2(240, 230);
        Margin = new Vector2(34, 28);
        AtRight = true;
    }

    private static Vector2[] BuildTicks()
    {
        // stroke-dasharray « 1 7.9 » sur r = 85 : 60 graduations radiales de 6 px.
        var n = 60;
        var a = new Vector2[n * 2];
        for (var i = 0; i < n; i++)
        {
            var ang = i * 8.9f / 85f;
            var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            a[2 * i] = C + d * 82f;
            a[2 * i + 1] = C + d * 88f;
        }
        return a;
    }

    private static Vector2[] BuildCone()
    {
        const int seg = 12;
        var half = Mathf.DegToRad(21.2f);
        var a = new Vector2[seg + 2];
        a[0] = C;
        for (var i = 0; i <= seg; i++)
        {
            var ang = -Mathf.Pi / 2 - half + 2 * half * i / seg;
            a[i + 1] = C + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 86f;
        }
        return a;
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) QueueRedraw();
    }

    private Vector2 Rel(Player p, float yaw, Vector3 w)
    {
        var d = w - p.GlobalPosition;
        var v = new Vector2(d.X, d.Z).Rotated(yaw);
        return new Vector2(-v.X, -v.Y);
    }

    private void OffTri(Vector2 dir, Color c)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        _tri[0] = C + dir * (RimR + 5);
        _tri[1] = C + dir * (RimR - 3) + perp * 4.2f;
        _tri[2] = C + dir * (RimR - 3) - perp * 4.2f;
        DrawColoredPolygon(_tri, c);
    }

    private readonly Vector2[] _quad = new Vector2[4];
    private readonly Vector2[] _quadLine = new Vector2[5];

    private void DrawVehicleMark(Vector2 q, bool heli, bool occupied)
    {
        var r = heli ? 7f : 5.5f;
        if (heli)
        {
            _quad[0] = q + new Vector2(0, -r); _quad[1] = q + new Vector2(r, 0); _quad[2] = q + new Vector2(0, r); _quad[3] = q + new Vector2(-r, 0);
        }
        else
        {
            _quad[0] = q + new Vector2(-r, -r); _quad[1] = q + new Vector2(r, -r); _quad[2] = q + new Vector2(r, r); _quad[3] = q + new Vector2(-r, r);
        }
        for (var i = 0; i < 4; i++) _quadLine[i] = _quad[i];
        _quadLine[4] = _quad[0];
        if (!occupied) DrawColoredPolygon(_quad, HudE.Vehicle);
        DrawPolyline(_quadLine, occupied ? HudE.Vehicle : HudE.Ink, occupied ? 2f : 1.2f, true);
        var lineCol = occupied ? HudE.Vehicle : HudE.Ink;
        if (heli)
        {
            // Rotor : croix en X.
            var d = r * 0.55f;
            DrawLine(q + new Vector2(-d, -d), q + new Vector2(d, d), lineCol, 1.4f, true);
            DrawLine(q + new Vector2(-d, d), q + new Vector2(d, -d), lineCol, 1.4f, true);
        }
        else DrawCircle(q, 2f, lineCol); // tourelle
    }

    public override void _Draw()
    {
        var p = World?.LocalPlayer;
        if (p == null) return;
        Shapes();
        Plate(Bracket, HudE.Plate, HudE.Frame, 1.4f);
        DrawRect(new Rect2(222, 70, 6, 12), HudE.Frame);
        DrawRect(new Rect2(222, 134, 6, 12), HudE.Frame);
        DrawCircle(C, 104, HudE.PlateDeep);
        // Anneau segmenté (8 segments, dasharray « 66 11.75 », rotation -80°).
        const float seg = 66f / 99f, step = 77.75f / 99f;
        var a0 = Mathf.DegToRad(-80f);
        for (var i = 0; i < 8; i++)
        {
            var s0 = a0 + i * step;
            DrawArc(C, 99, s0, s0 + seg, 14, HudE.Ring, 9f, true);
        }
        for (var i = 0; i < 8; i++)
        {
            var s0 = a0 + i * step;
            DrawArc(C, 99, s0, s0 + seg, 14, HudE.Tick, 2f, true);
        }
        DrawArc(C, 91, 0, Mathf.Tau, 64, HudE.Frame, 1f, true);
        DrawMultiline(Ticks, new Color(HudE.Frame, 0.8f), 1f);
        DrawArc(C, 56, 0, Mathf.Tau, 48, new Color(HudE.Frame, 0.55f), 1f, true);
        DrawArc(C, 28, 0, Mathf.Tau, 32, new Color(HudE.Frame, 0.45f), 1f, true);
        var cross = new Color(HudE.Frame, 0.4f);
        DrawLine(new Vector2(108, 20), new Vector2(108, 196), cross, 1f);
        DrawLine(new Vector2(20, 108), new Vector2(196, 108), cross, 1f);
        DrawColoredPolygon(Cone, new Color(170 / 255f, 1f, 180 / 255f, 0.2f));
        DrawPolyline(ConeLine, new Color(HudE.Label, 0.6f), 1f, true);

        var fwd = p.AimForward;
        var yaw = Mathf.Atan2(fwd.X, fwd.Z);
        const float k = Inner / Range;

        // Ennemis : points rouges ; au-delà de la portée, triangles sur le bord (limités en nombre).
        var off = 0;
        foreach (var e in World.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var v = Rel(p, yaw, e.GlobalPosition);
            var len = v.Length();
            if (len <= Range)
            {
                var q = C + v * k;
                if (e.IsMachine)
                {
                    // Machines : carré cerclé (losange pour le vaisseau), plus grand selon la taille.
                    var r = e.Def.Type == EnemyType.Walker ? 4.6f : 5.6f;
                    var pts = e.Def.Type == EnemyType.DropShip
                        ? new[] { q + new Vector2(0, -r * 1.25f), q + new Vector2(r * 1.25f, 0), q + new Vector2(0, r * 1.25f), q + new Vector2(-r * 1.25f, 0) }
                        : new[] { q + new Vector2(-r, -r), q + new Vector2(r, -r), q + new Vector2(r, r), q + new Vector2(-r, r) };
                    DrawColoredPolygon(pts, HudE.Enemy);
                    DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, HudE.Ink, 1f, true);
                }
                else if (e.IsAlien)
                {
                    // Aliens humanoïdes : triangle pointe en haut, cerné.
                    var pts = new[] { q + new Vector2(0, -4.8f), q + new Vector2(4.3f, 3.4f), q + new Vector2(-4.3f, 3.4f) };
                    DrawColoredPolygon(pts, HudE.Enemy);
                    DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[0] }, HudE.Ink, 1f, true);
                }
                else if (e.IsBoss || e.IsStatic)
                {
                    DrawCircle(q, 6f, HudE.Enemy);
                    DrawArc(q, 6f, 0, Mathf.Tau, 16, HudE.Ink, 1f, true);
                }
                // Point en texture : un DrawCircle crée un polygone (tampons du GPU) par appel, ~13 µs pièce à 300 ennemis.
                else DrawTextureRect(HudE.Dot, new Rect2(q - new Vector2(3.4f, 3.4f), new Vector2(6.8f, 6.8f)), false, HudE.Enemy);
            }
            else if (off < MaxOffRange && len < Range * 3f)
            {
                off++;
                OffTri(v / len, HudE.Enemy);
            }
        }

        // Alliés PNJ (petits points bleus).
        if (World.Allies != null)
            foreach (var a in World.Allies.ById.Values)
            {
                if (a.Dead) continue;
                var v = Rel(p, yaw, a.GlobalPosition);
                var len = v.Length();
                if (len <= Range) DrawCircle(C + v * k, 2.6f, new Color(HudE.Ally, 0.85f));
            }

        // Coéquipiers : points bleus cerclés ; à terre : anneau rouge-orangé ; hors portée : triangles.
        foreach (var rp in World.Remotes.Values)
        {
            var v = Rel(p, yaw, rp.GlobalPosition);
            var len = v.Length();
            var col = rp.Downed ? HudE.Downed : HudE.Ally;
            if (len > Range) { OffTri(v / len, col); continue; }
            var q = C + v * k;
            if (rp.Downed)
            {
                DrawArc(q, 5f, 0, Mathf.Tau, 20, HudE.Downed, 2f, true);
                DrawCircle(q, 2f, HudE.Downed);
            }
            else
            {
                DrawCircle(q, 4f, HudE.Ally);
                DrawArc(q, 4f, 0, Mathf.Tau, 16, HudE.Ink, 1f, true);
            }
        }

        // Véhicules (jaune) : hélicoptère = losange barré d'un rotor, char = carré avec canon. Plein = libre,
        // contour seul = piloté par un coéquipier, absent = le nôtre (flèche du joueur) ou épave ; hors portée : triangle jaune.
        foreach (var veh in World.Vehicles.Values)
        {
            if (veh.Wrecked || veh == p.Vehicle) continue;
            var v = Rel(p, yaw, veh.GlobalPosition);
            var len = v.Length();
            if (len > Range) { if (len > 0.01f) OffTri(v / len, HudE.Vehicle); continue; }
            DrawVehicleMark(C + v * k, veh is Helicopter, veh.Driver != 0);
        }

        // Signalements (losanges).
        foreach (var ping in World.Pings)
        {
            if (ping.until < World.Elapsed) continue;
            var v = Rel(p, yaw, ping.pos);
            var len = v.Length();
            if (len > Range) v = v / len * Range;
            var q = C + v * k;
            var pc = ping.kind == 1 ? HudE.Enemy : ping.kind == 2 ? UiKit.Gold : HudE.Label;
            _tri[0] = q + new Vector2(0, -5); _tri[1] = q + new Vector2(5, 0); _tri[2] = q + new Vector2(0, 5);
            DrawColoredPolygon(_tri, pc);
            _tri[1] = q + new Vector2(-5, 0);
            DrawColoredPolygon(_tri, pc);
        }

        // Joueur.
        Plate(Arrow, Colors.White, HudE.Ink, 1f);

        // Repère du nord (-Z), sur le pourtour : le radar tourne avec la caméra.
        var north = Rel(p, yaw, p.GlobalPosition + new Vector3(0, 0, -100f));
        if (north.LengthSquared() > 0.01f)
        {
            var np = C + north.Normalized() * 111f;
            Text(HudE.Mono, 12, "N", np.X - 10, np.Y - 6, 20, HorizontalAlignment.Center, HudE.Label);
        }
    }
}
