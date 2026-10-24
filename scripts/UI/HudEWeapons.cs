using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Famille visuelle d'une arme (silhouette, pictogramme de munition, étiquette).</summary>
public enum HudWeaponIcon { Rifle, Shotgun, Sniper, Rocket, Missile, Grenade, Gatling, Energy, Beacon, Turret, Heal, Hammer, Siege, Tank, Heli }

/// <summary>Pictogramme de munition dans la case de gauche des plaques d'arme.</summary>
public enum HudAmmoGlyph { Bullet, Shell, Rocket, Bolt, Round, Cross, Beacon }

/// <summary>Silhouettes vertes d'armes (boîte 128×48 de la maquette) et pictogrammes de munitions.</summary>
public static class HudWeaponArt
{
    public static HudWeaponIcon IconFor(string category)
    {
        var c = category ?? "";
        if (c.Contains("pompe")) return HudWeaponIcon.Shotgun;
        if (c.Contains("précision")) return HudWeaponIcon.Sniper;
        if (c.Contains("roquettes")) return HudWeaponIcon.Rocket;
        if (c.Contains("missiles")) return HudWeaponIcon.Missile;
        if (c.Contains("grenades")) return HudWeaponIcon.Grenade;
        if (c.Contains("Mitrailleuse")) return HudWeaponIcon.Gatling;
        if (c.Contains("Laser") || c.Contains("électrique") || c.Contains("plasma") || c.Contains("particules")) return HudWeaponIcon.Energy;
        if (c.Contains("Frappe") || c.Contains("Bombardement")) return HudWeaponIcon.Beacon;
        if (c.Contains("Tourelle")) return HudWeaponIcon.Turret;
        if (c.Contains("soin")) return HudWeaponIcon.Heal;
        if (c.Contains("Marteau")) return HudWeaponIcon.Hammer;
        if (c.Contains("siège")) return HudWeaponIcon.Siege;
        return HudWeaponIcon.Rifle;
    }

    public static string AmmoName(HudWeaponIcon i) => i switch
    {
        HudWeaponIcon.Shotgun => "Cartouches",
        HudWeaponIcon.Rocket or HudWeaponIcon.Heli => "Roquettes",
        HudWeaponIcon.Missile => "Missiles",
        HudWeaponIcon.Grenade => "Grenades",
        HudWeaponIcon.Siege or HudWeaponIcon.Tank => "Obus",
        HudWeaponIcon.Energy => "Énergie",
        HudWeaponIcon.Beacon => "Frappe",
        HudWeaponIcon.Turret => "Tourelle",
        HudWeaponIcon.Heal => "Soin",
        HudWeaponIcon.Hammer => "Charge",
        _ => "Balles",
    };

    public static HudAmmoGlyph GlyphFor(HudWeaponIcon i) => i switch
    {
        HudWeaponIcon.Shotgun => HudAmmoGlyph.Shell,
        HudWeaponIcon.Rocket or HudWeaponIcon.Missile or HudWeaponIcon.Heli => HudAmmoGlyph.Rocket,
        HudWeaponIcon.Grenade or HudWeaponIcon.Siege or HudWeaponIcon.Tank => HudAmmoGlyph.Round,
        HudWeaponIcon.Energy or HudWeaponIcon.Hammer => HudAmmoGlyph.Bolt,
        HudWeaponIcon.Heal => HudAmmoGlyph.Cross,
        HudWeaponIcon.Beacon or HudWeaponIcon.Turret => HudAmmoGlyph.Beacon,
        _ => HudAmmoGlyph.Bullet,
    };

    private static HudShape S(params float[] xy) => new(xy);
    private static HudShape R(float x, float y, float w, float h) => new(x, y, x + w, y, x + w, y + h, x, y + h);

    private static HudShape Oct(float cx, float cy, float r)
    {
        var a = new float[16];
        for (var i = 0; i < 8; i++)
        {
            var t = Mathf.Pi / 8 + i * Mathf.Pi / 4;
            a[2 * i] = cx + Mathf.Cos(t) * r;
            a[2 * i + 1] = cy + Mathf.Sin(t) * r;
        }
        return new HudShape(a);
    }

    /// <summary>Silhouettes (canon vers la droite) dans une boîte 128×48.</summary>
    public static readonly Dictionary<HudWeaponIcon, HudShape[]> Silhouettes = new()
    {
        // Fusil d'assaut : reprise exacte de la maquette (FA-21 Vigie).
        [HudWeaponIcon.Rifle] = new[]
        {
            S(2, 18, 24, 15, 32, 15, 32, 29, 24, 31, 6, 33, 2, 31), R(32, 13, 44, 16), S(76, 15, 104, 15, 104, 25, 76, 26),
            R(104, 18, 16, 4), R(118, 16, 7, 8), S(58, 29, 68, 29, 71, 45, 61, 45), S(40, 29, 49, 29, 47, 41, 39, 41),
            R(46, 5, 24, 6), R(53, 10, 9, 4),
        },
        [HudWeaponIcon.Shotgun] = new[]
        {
            S(2, 18, 26, 16, 30, 16, 30, 30, 22, 30, 6, 33, 2, 31), R(30, 14, 30, 14), R(60, 15, 62, 5), R(64, 21, 32, 7),
            S(38, 28, 47, 28, 45, 40, 37, 40), R(118, 13, 4, 3),
        },
        [HudWeaponIcon.Sniper] = new[]
        {
            S(2, 17, 24, 15, 30, 15, 30, 28, 20, 30, 4, 32, 2, 30), R(30, 14, 34, 12), R(64, 17, 54, 3), R(116, 15, 10, 7),
            R(36, 4, 32, 7), R(42, 10, 4, 4), R(58, 10, 4, 4), S(40, 26, 48, 26, 46, 38, 38, 38), S(92, 20, 95, 20, 101, 38, 98, 38),
        },
        [HudWeaponIcon.Rocket] = new[]
        {
            R(10, 13, 102, 14), S(112, 10, 125, 7, 125, 33, 112, 30), S(2, 10, 10, 13, 10, 27, 2, 30), S(52, 27, 61, 27, 63, 42, 54, 42),
            R(38, 5, 16, 8), R(26, 27, 12, 6), S(74, 27, 80, 27, 78, 36, 72, 36),
        },
        [HudWeaponIcon.Missile] = new[]
        {
            R(8, 9, 98, 22), R(106, 7, 9, 26), R(115, 11, 9, 18), S(48, 31, 57, 31, 59, 45, 50, 45), R(30, 2, 22, 7), R(18, 31, 14, 6),
        },
        [HudWeaponIcon.Grenade] = new[]
        {
            S(2, 18, 22, 16, 28, 16, 28, 30, 6, 33, 2, 31), R(28, 15, 10, 15), Oct(48, 25, 12), R(60, 15, 46, 10), R(104, 13, 10, 14),
            S(30, 30, 38, 30, 37, 42, 29, 42), R(66, 8, 18, 6),
        },
        [HudWeaponIcon.Gatling] = new[]
        {
            S(4, 16, 20, 13, 20, 31, 4, 28), R(20, 11, 40, 22), R(60, 13, 56, 4), R(60, 19, 56, 4), R(60, 25, 56, 4),
            R(112, 10, 7, 22), R(28, 3, 24, 5), R(36, 7, 4, 5), R(26, 33, 22, 11),
        },
        [HudWeaponIcon.Energy] = new[]
        {
            S(2, 21, 20, 14, 70, 12, 96, 16, 96, 27, 70, 31, 20, 30, 2, 28), R(96, 17, 16, 9), S(112, 14, 125, 11, 125, 16, 115, 19),
            S(112, 29, 125, 32, 125, 27, 115, 24), R(40, 8, 4, 27), R(50, 8, 4, 27), R(60, 8, 4, 27), S(28, 29, 37, 29, 40, 44, 31, 44),
        },
        [HudWeaponIcon.Beacon] = new[]
        {
            R(48, 12, 28, 34), R(68, 0, 3, 13), R(50, 8, 8, 5), R(58, 40, 8, 4),
        },
        [HudWeaponIcon.Turret] = new[]
        {
            R(44, 8, 32, 17), R(76, 13, 42, 5), R(114, 11, 8, 9), S(57, 25, 62, 25, 46, 46, 41, 46), S(60, 25, 65, 25, 81, 46, 76, 46),
            R(58, 25, 4, 21), R(50, 3, 14, 5),
        },
        [HudWeaponIcon.Heal] = new[]
        {
            R(54, 4, 20, 40), R(40, 14, 48, 20), R(14, 20, 20, 8), R(94, 20, 20, 8),
        },
        [HudWeaponIcon.Hammer] = new[]
        {
            R(6, 21, 84, 6), R(2, 19, 8, 10), R(88, 6, 24, 36), R(112, 10, 6, 28), R(80, 18, 8, 12),
        },
        [HudWeaponIcon.Siege] = new[]
        {
            S(4, 13, 34, 9, 34, 33, 4, 37), R(34, 12, 80, 17), R(112, 9, 12, 23), S(40, 29, 50, 29, 52, 45, 42, 45), R(18, 3, 20, 7),
        },
        [HudWeaponIcon.Tank] = new[]
        {
            S(6, 31, 18, 24, 108, 24, 120, 31, 112, 42, 14, 42), S(36, 13, 78, 13, 86, 24, 30, 24), R(84, 15, 42, 4), R(50, 8, 10, 5),
        },
        [HudWeaponIcon.Heli] = new[]
        {
            S(28, 22, 44, 15, 76, 14, 92, 20, 96, 30, 72, 34, 36, 32), R(92, 21, 28, 5), S(116, 12, 124, 12, 124, 26, 120, 26), R(8, 7, 104, 3),
            R(58, 9, 4, 6), R(36, 40, 52, 2), R(46, 33, 3, 8), R(72, 33, 3, 8), R(30, 30, 16, 4),
        },
    };

    /// <summary>Pictogrammes de munition, centrés sur l'origine (environ 16 × 32).</summary>
    public static readonly Dictionary<HudAmmoGlyph, HudShape[]> Glyphs = new()
    {
        [HudAmmoGlyph.Bullet] = new[] { S(-4, -10, -3, -13, -1.5f, -15, 0, -15.5f, 1.5f, -15, 3, -13, 4, -10, 4, 12, -4, 12) },
        [HudAmmoGlyph.Shell] = new[] { R(-5, -13, 10, 20), R(-6, 7, 12, 6) },
        [HudAmmoGlyph.Rocket] = new[] { S(0, -16, 3, -10, 3, 8, 6, 14, -6, 14, -3, 8, -3, -10) },
        [HudAmmoGlyph.Bolt] = new[] { S(2, -16, -6, 2, -1, 2, -3, 16, 6, -3, 1, -3, 4, -16) },
        [HudAmmoGlyph.Round] = new[] { S(-5, -6, -3, -12, 0, -14, 3, -12, 5, -6, 5, 13, -5, 13) },
        [HudAmmoGlyph.Cross] = new[] { R(-2.5f, -10, 5, 20), R(-10, -2.5f, 20, 5) },
        [HudAmmoGlyph.Beacon] = new[] { R(-5, -6, 10, 19), R(1, -16, 2, 10) },
    };
}

/// <summary>
/// Pile d'armes (bas droite) : arme active en grand (nom au-dessus, silhouette verte à gauche,
/// munitions « 27 / 40 », étiquette du type, barre sous les chiffres = chargeur ou rechargement,
/// numéro d'emplacement), autres armes en petit (nom + type, munitions ou « Prête »).
/// En véhicule : armes du char ou de l'hélicoptère, même présentation.
/// </summary>
public partial class HudWeapons : HudBlock
{
    // Origine du bloc dans la maquette : (824, 412) ; taille 400 × 248 ; marges droite 56, bas 60.
    private static readonly Vector2 MainBox = new(138, 180), MainIcon = new(8, 186);
    private const float MainNameTop = 154, NameRight = 386;
    private static readonly HudShape MainShape = new(10, 1, 226, 1, 234, 9, 252, 9, 261, 18, 261, 46, 252, 55, 234, 55, 226, 63, 10, 63, 1, 54, 1, 10);
    private static readonly HudShape SmallShape = new(7, 1, 182, 1, 188, 7, 202, 7, 209, 13, 209, 25, 202, 31, 188, 31, 182, 35, 7, 35, 1, 29, 1, 7);
    private static readonly StyleBoxFlat MainSlot = Slot(6), SmallSlot = Slot(4);

    private static StyleBoxFlat Slot(int r)
    {
        var sb = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.4f), BorderColor = HudE.Line, AntiAliasing = true };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(r);
        return sb;
    }

    /// <summary>État affiché d'une arme (chaînes mises en cache : aucune allocation si rien ne change).</summary>
    private sealed class Entry
    {
        public string Name = "", Ammo = "", Cur = "", Max = "", Status = "", Slot = "";
        public HudWeaponIcon Icon;
        public float Bar;
        public bool Reloading, Empty;
        public object Source;
        public int Key = int.MinValue, CurKey = int.MinValue, MaxKey = int.MinValue, SlotKey = -1;

        public void SetSlot(int i)
        {
            if (i == SlotKey) return;
            SlotKey = i;
            Slot = (i + 1).ToString();
        }

        public void SetCur(int v) { if (v != CurKey) { CurKey = v; Cur = v.ToString(); } }
        public void SetMax(int v) { if (v != MaxKey) { MaxKey = v; Max = v.ToString(); } }
    }

    private readonly Entry _main = new();
    private readonly Entry[] _others = { new(), new(), new() };
    private int _otherCount;

    public HudWeapons()
    {
        LogicalSize = new Vector2(400, 248);
        Margin = new Vector2(56, 60);
        AtRight = AtBottom = true;
    }

    private static void FromWeapon(Entry e, WeaponState ws, bool small)
    {
        var w = ws.W;
        if (!ReferenceEquals(e.Source, w))
        {
            e.Source = w;
            e.Icon = HudWeaponArt.IconFor(w.T.Category);
            e.Ammo = HudWeaponArt.AmmoName(e.Icon);
            e.Name = small ? $"{w.T.Name} ({w.T.Category})" : w.T.Name;
            e.Key = int.MinValue;
        }
        var beam = w.T.Kind == WeaponKind.Beam;
        var mag = w.Magazine;
        e.Reloading = ws.Reloading;
        if (beam)
        {
            var pct = Mathf.RoundToInt(ws.BeamCharge * 100);
            e.SetCur(pct);
            e.SetMax(100);
            e.Bar = ws.BeamCharge;
            e.Empty = pct <= 0;
        }
        else
        {
            e.SetCur(ws.Ammo);
            e.SetMax(mag);
            e.Bar = e.Reloading ? ws.ReloadProgress : mag > 0 ? (float)ws.Ammo / mag : 0;
            e.Empty = ws.Ammo <= 0;
        }
        if (!small) return;
        // Petite plaque : « Prête », « Recharge » ou munitions restantes.
        var key = e.Reloading ? -1 : beam ? 100000 + e.CurKey : mag <= 1 ? (ws.Ammo > 0 ? -2 : -3) : ws.Ammo * 1000 + mag;
        if (key == e.Key) return;
        e.Key = key;
        e.Status = key switch
        {
            -1 => "Recharge",
            -2 => "Prête",
            -3 => "Vide",
            _ => beam ? $"{e.CurKey} %" : $"{ws.Ammo} / {mag}",
        };
    }

    /// <summary>
    /// Arme de bord : comme une arme d'infanterie, grand chiffre = coups dans le chargeur (bande, panier),
    /// petit chiffre = réserve restante de la sortie (munitions limitées) ; « Réarmement » sur l'aire de départ.
    /// </summary>
    private static void FromVehicleWeapon(Entry e, VehicleWeapon w, Vehicle v, bool small)
    {
        if (!ReferenceEquals(e.Source, w))
        {
            e.Source = w;
            e.Icon = w.Icon;
            e.Ammo = w.AmmoName;
            e.Name = small ? $"{w.Name} ({w.AmmoName})" : w.Name;
            e.Key = int.MinValue;
        }
        e.Reloading = w.Reloading;
        e.SetCur(w.Mag);
        e.SetMax(w.Reserve);
        e.Bar = w.Reloading ? w.ReloadProgress : w.MagSize > 0 ? (float)w.Mag / w.MagSize : 0;
        e.Empty = w.Mag <= 0;
        if (!small) return;
        var key = w.Reloading ? -1 : w.Empty ? -3 : v.Rearming ? -4 : w.Mag * 100000 + w.Reserve;
        if (key == e.Key) return;
        e.Key = key;
        e.Status = key switch
        {
            -1 => "Recharge",
            -3 => "Vide",
            -4 => "Réarmement",
            _ => $"{w.Mag} / {w.Reserve}",
        };
    }

    private void FromVehicle(Vehicle v)
    {
        _otherCount = 0;
        if (v.Arms.Length == 0) return;
        FromVehicleWeapon(_main, v.Arms[0], v, false);
        _main.SetSlot(0);
        for (var i = 1; i < v.Arms.Length && _otherCount < _others.Length; i++)
        {
            var e = _others[_otherCount++];
            FromVehicleWeapon(e, v.Arms[i], v, true);
            e.SetSlot(i);
        }
    }

    public override void _Process(double delta)
    {
        var p = World?.LocalPlayer;
        if (p == null || !IsVisibleInTree()) return;
        if (p.Vehicle is { } veh) FromVehicle(veh);
        else if (p.Weapon is { } ws)
        {
            FromWeapon(_main, ws, false);
            _main.SetSlot(Mathf.Clamp(p.Current, 0, p.Weapons.Count - 1));
            _otherCount = 0;
            // Les autres armes, de la plus proche (suivante) à la plus éloignée.
            var n = p.Weapons.Count;
            for (var k = 1; k < n && _otherCount < _others.Length; k++)
            {
                var i = (_main.SlotKey + k) % n;
                var e = _others[_otherCount++];
                FromWeapon(e, p.Weapons[i], true);
                e.SetSlot(i);
            }
        }
        else _otherCount = 0;
        QueueRedraw();
    }

    private void DrawHudShapes(HudShape[] parts, Vector2 origin, float scale, Color fill, Color line, float width)
    {
        DrawSetTransform(origin * S, 0, new Vector2(S * scale, S * scale));
        foreach (var sh in parts)
        {
            DrawColoredPolygon(sh.Fill, fill);
            if (width > 0) DrawPolyline(sh.Outline, line, width / scale, true);
        }
        Shapes();
    }

    public override void _Draw()
    {
        var p = World?.LocalPlayer;
        if (p == null || (p.Weapon == null && p.Vehicle == null)) return;
        Shapes();

        // Autres armes (petites plaques), empilées vers le haut.
        for (var i = 0; i < _otherCount; i++)
        {
            var e = _others[i];
            var dy = -74f * i;
            var o = new Vector2(190, 100 + dy);
            Text(HudE.Cond, 13, e.Name, NameRight - 250, 80 + dy, 250, HorizontalAlignment.Right, HudE.NameText, false);
            Shapes(o);
            Plate(SmallShape, HudE.Plate, HudE.Frame, 1.4f);
            DrawStyleBox(SmallSlot, new Rect2(8, 7, 22, 22));
            Shapes();
            DrawHudShapes(HudWeaponArt.Glyphs[HudWeaponArt.GlyphFor(e.Icon)], o + new Vector2(19, 18), 0.55f, HudE.Frame, default, 0);
            Shapes(o);
            Text(HudE.Mono, 16, e.Status, 40, 8, 140, HorizontalAlignment.Center, e.Reloading ? HudE.Reload : Colors.White, true, true, o);
            Tab(e.Ammo, 82, 25, 56, 11, 9, o);
            Text(HudE.Mono, 12, e.Slot, 186, 10, 0, HorizontalAlignment.Left, HudE.Label, false, true, o);
            Shapes();
        }

        // Arme active.
        var m = _main;
        Text(HudE.CondSpaced(Mathf.Max(1, Mathf.RoundToInt(S))), 21, m.Name, NameRight - 250, MainNameTop, 250, HorizontalAlignment.Right, Colors.White, false);
        DrawHudShapes(HudWeaponArt.Silhouettes[m.Icon], MainIcon, 1f, HudE.Icon, HudE.IconLine, 1f);
        // Rainures de la silhouette du fusil d'assaut (maquette).
        if (m.Icon == HudWeaponIcon.Rifle)
        {
            Shapes(MainIcon);
            DrawLine(new Vector2(36, 20), new Vector2(72, 20), HudE.IconLine, 1.4f);
            DrawLine(new Vector2(80, 20), new Vector2(100, 20), HudE.IconLine, 1.4f);
        }
        var b = MainBox;
        Shapes(b);
        Plate(MainShape, HudE.PlateDeep, HudE.Frame, 1.8f);
        DrawStyleBox(MainSlot, new Rect2(9, 9, 30, 46));
        Shapes();
        DrawHudShapes(HudWeaponArt.Glyphs[HudWeaponArt.GlyphFor(m.Icon)], b + new Vector2(24, 32), 1f, HudE.Frame, default, 0);
        Shapes(b);
        DrawLine(new Vector2(132, 12), new Vector2(146, 40), HudE.Frame, 1.6f, true);
        DrawRect(new Rect2(44, 47, 176, 3), HudE.Line);
        var bar = Mathf.Clamp(m.Bar, 0, 1);
        if (bar > 0) DrawRect(new Rect2(44, 47, 176 * bar, 3), m.Reloading ? HudE.Reload : HudE.Tick);
        DrawRect(new Rect2(236, 20, 8, 24), HudE.Frame);
        var curCol = m.Reloading ? new Color(1, 1, 1, 0.45f) : m.Empty ? HudE.Empty : Colors.White;
        Text(HudE.Mono, 38, m.Cur, 40, 6, 86, HorizontalAlignment.Right, curCol, true, true, b);
        Text(HudE.Mono, 30, m.Max, 152, 12, 0, HorizontalAlignment.Left, HudE.Pale, true, true, b);
        Tab(m.Reloading ? "Recharge" : m.Ammo, 104, 55, 60, 13, 11, b);
        Text(HudE.Mono, 11, m.Slot, 238, 46, 0, HorizontalAlignment.Left, HudE.Label, false, true, b);
    }
}
