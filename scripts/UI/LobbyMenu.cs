using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Salon : colonne de gauche (onglets Opération / Équipement), escouade en 3D au centre,
/// action principale en bas à droite (Déployer pour l'hôte, Prêt pour les clients).
/// </summary>
public partial class LobbyMenu : MenuScreen
{
    private VBoxContainer _col;
    private Button _primary;
    private int _tab;

    public override void _Ready()
    {
        BuildFrame(Net.I.IsSolo ? "Solo" : "Salon", 0f, 760);
        AddTab("Opération", () => { _tab = 0; Refresh(); });
        AddTab("Équipement", () => { _tab = 1; Refresh(); });
        ((Button)Tabs.GetChild(0)).SetPressedNoSignal(true);

        // Colonne défilante : à 1280×720 le contenu dépasse la hauteur disponible.
        var scroll = Scroll(440);
        scroll.AnchorBottom = 1;
        scroll.OffsetRight = 440;
        Body.AddChild(scroll);
        _col = UiKit.VBox(4);
        _col.CustomMinimumSize = new Vector2(420, 0);
        _col.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        scroll.AddChild(_col);

        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", Leave));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Onglets"));
        _primary = UiKit.Button(Net.I.IsHost ? "Déployer" : "Prêt", OnPrimary, "Primary", 60);
        _primary.CustomMinimumSize = new Vector2(240, 60);
        FooterRight.AddChild(_primary);
        App.I.Background?.SetMode(MenuBackground.Mode.Squad);
        Refresh();
        UiKit.FocusLater(_primary);
    }

    private void OnPrimary()
    {
        if (Net.I.IsHost) Net.I.HostStartMission();
        else
        {
            var me = Net.I.Players.TryGetValue(Net.I.MyId, out var p) && p.Ready;
            Net.I.SetLocalReady(!me);
        }
    }

    private void Leave()
    {
        Net.I.Leave();
        App.I.ShowMain();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Leave(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) GetViewport().SetInputAsHandled();
    }

    protected override void FocusContent()
    {
        // Onglet changé : focus sur l'élément sélectionné (mission ou classe).
        RestoreFocus(_tab == 0 ? $"m{Net.I.LobbyMission}" : $"c{(int)Profile.I.Data.SelectedClass}", _primary);
    }

    public void Refresh()
    {
        if (!IsInsideTree()) return;
        var players = Net.I.Players.Values.OrderBy(p => p.Id).ToList();
        App.I.Background?.SetSquad(players, Net.I.IsSolo);
        HeaderRight.Text = Net.I.IsSolo ? "" : $"{Net.I.SessionName} · {players.Count}/{Net.MaxPlayers} joueurs";

        // La liste est reconstruite : on retrouve ensuite l'élément qui avait le focus.
        var focused = FocusedKey();
        foreach (var c in _col.GetChildren()) c.QueueFree();
        if (_tab == 0) BuildOperation();
        else BuildEquipment();
        if (focused != null) RestoreFocus(focused, _primary);

        if (Net.I.IsHost)
        {
            var others = players.Where(p => p.Id != Net.I.MyId).ToList();
            var waiting = others.Count(p => !p.Ready);
            _primary.Text = waiting == 0 ? "DÉPLOYER" : $"DÉPLOYER ({others.Count - waiting}/{others.Count} PRÊTS)";
        }
        else
        {
            var ready = Net.I.Players.TryGetValue(Net.I.MyId, out var me) && me.Ready;
            _primary.Text = ready ? "ANNULER" : "PRÊT";
        }
    }

    private void Section(string text)
    {
        if (UiKit.HasLiveChildren(_col)) _col.AddChild(UiKit.Gap(16));
        _col.AddChild(UiKit.Eyebrow(text));
        _col.AddChild(UiKit.Gap(4));
    }

    /// <summary>Ligne focalisable de la colonne : clé de focus et sortie à droite vers l'action principale.</summary>
    private void Item(Control c, string key)
    {
        SetFocusKey(c, key);
        _col.AddChild(c);
        c.FocusNeighborRight = c.GetPathTo(_primary);
    }

    private void BuildOperation()
    {
        var host = Net.I.IsHost;
        Section("Mission");
        foreach (var m in Missions.All)
        {
            var idx = m.Index;
            var locked = !MissionUnlocked(idx);
            var sel = Net.I.LobbyMission == idx;
            // Lignes compactes (une ligne) ; seule la mission choisie affiche son détail : 8+ missions tiennent sans défiler.
            var b = UiKit.Button("", () => { Net.I.SetLobbyMission(idx, Net.I.LobbyDifficulty); Refresh(); }, "Row", sel ? 56 : 38);
            b.ToggleMode = true;
            b.SetPressedNoSignal(sel);
            b.Disabled = !host || locked;
            var v = UiKit.VBox(0);
            v.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(v);
            v.OffsetLeft = 22; v.OffsetRight = -16; v.OffsetTop = sel ? 5 : 3;
            var line = UiKit.HBox(12);
            line.MouseFilter = MouseFilterEnum.Ignore;
            var title = UiKit.Label($"{idx + 1:00}  {m.Name.ToUpperInvariant()}", sel ? 21 : 19, locked ? UiKit.Dim : sel ? UiKit.Text : UiKit.Muted, true);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            line.AddChild(title);
            var cleared = Enumerable.Range(0, 5).Where(d => Profile.I.IsCleared(idx, (Difficulty)d)).Select(d => Difficulties.Names[d]).LastOrDefault();
            if (locked) line.AddChild(UiKit.Label(idx >= 3 ? "après « La reine »" : "verrouillée", 13, UiKit.Dim));
            else if (cleared != null && !sel) line.AddChild(UiKit.Label("✓", 15, UiKit.Good));
            v.AddChild(line);
            if (sel)
            {
                var goal = m.Goal switch { MissionGoal.Exterminate => "Extermination", MissionGoal.DestroyNests => "Destruction de nids", _ => "Boss" };
                var time = m.Time switch { TimeOfDay.Day => "Jour", TimeOfDay.Dusk => "Crépuscule", _ => "Nuit" };
                v.AddChild(UiKit.Label($"{goal} · {time}" + (cleared != null ? $" · ✓ {cleared}" : ""), 14, cleared != null ? UiKit.Good : UiKit.Muted));
            }
            b.AddChild(v);
            Item(b, $"m{idx}");
        }

        Section("Difficulté");
        var diff = new Stepper(Difficulties.Names.Select(n => n.ToUpperInvariant()).ToArray(), (int)Net.I.LobbyDifficulty,
            i => { Net.I.SetLobbyMission(Net.I.LobbyMission, (Difficulty)i); Refresh(); }, 52) { Wrap = false, Disabled = !host };
        diff.AddThemeFontOverride("font", UiKit.HeadFont);
        diff.AddThemeFontSizeOverride("font_size", 24);
        diff.SetIndex((int)Net.I.LobbyDifficulty);
        diff.TooltipText = host ? "← / → pour changer" : "";
        Item(diff, "diff");
        var di = (int)Net.I.LobbyDifficulty;
        _col.AddChild(UiKit.Gap(2));
        _col.AddChild(UiKit.Label($"Armes niveau {Difficulties.LevelMin[di]}–{Difficulties.LevelMax[di]} · armure +{Difficulties.ArmorPerCrate[di]:0} par caisse", 14, UiKit.Muted));

        _col.AddChild(UiKit.Gap(16));
        var brief = UiKit.Label(Missions.All[Net.I.LobbyMission].Briefing, 16, new Color(UiKit.Text, 0.82f));
        brief.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        brief.CustomMinimumSize = new Vector2(420, 0);
        _col.AddChild(brief);
        if (!host) _col.AddChild(UiKit.Label("L'hôte choisit la mission et la difficulté.", 14, UiKit.Dim));
    }

    /// <summary>Une mission est jouable si la précédente a été réussie (toutes difficultés) ; bonus après la reine.</summary>
    private static bool MissionUnlocked(int idx)
    {
        if (idx == 0) return true;
        var need = idx >= 3 ? 2 : idx - 1;
        for (var d = 0; d < 5; d++)
            if (Profile.I.IsCleared(need, (Difficulty)d)) return true;
        return false;
    }

    private void BuildEquipment()
    {
        var sel = Profile.I.Data.SelectedClass;
        Section("Classe");
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        _col.AddChild(grid);
        foreach (var cd in Classes.All)
        {
            var cls = cd.Id;
            var b = UiKit.Button("", () => { Net.I.SetLocalClass(cls); Refresh(); }, "Card", 60);
            b.ToggleMode = true;
            b.SetPressedNoSignal(cls == sel);
            b.CustomMinimumSize = new Vector2(208, 60);
            SetFocusKey(b, $"c{(int)cls}");
            var v = UiKit.VBox(0);
            v.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(v);
            v.OffsetLeft = 20; v.OffsetTop = 8;
            v.AddChild(UiKit.Label(cd.Name.ToUpperInvariant(), 19, cls == sel ? UiKit.Text : UiKit.Muted, true));
            v.AddChild(UiKit.Label($"Niv. {Profile.I.Level(cls)} · {Profile.I.Armor(cls):0} PV", 13, UiKit.Muted));
            b.AddChild(v);
            grid.AddChild(b);
        }
        // Colonne de droite de la grille : sortie à droite vers l'action principale.
        for (var i = 1; i < grid.GetChildCount(); i += 2) ((Control)grid.GetChild(i)).FocusNeighborRight = ((Control)grid.GetChild(i)).GetPathTo(_primary);
        var def = Classes.Get(sel);
        var desc = UiKit.Label($"{def.Role}. {def.AbilityName} : {def.AbilityDescription}", 15, new Color(UiKit.Text, 0.8f));
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(420, 0);
        _col.AddChild(UiKit.Gap(4));
        _col.AddChild(desc);

        Section("Armes");
        var eq = Profile.I.Equipped(sel);
        for (var i = 0; i < eq.Count; i++)
        {
            var w = eq[i];
            var b = UiKit.Button("", () => App.I.ShowArsenal(() => App.I.ShowLobby()), "Row", 56);
            var h = UiKit.HBox(14);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 18; h.OffsetRight = -18;
            h.AddChild(new ColorRect { Color = Rarities.Colors[(int)w.Rarity], CustomMinimumSize = new Vector2(3, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
            var info = UiKit.VBox(0);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            info.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            info.AddChild(UiKit.Label($"{w.T.Name}  Nv.{w.Level}", 17));
            info.AddChild(UiKit.Label($"{w.T.Category} · DPS {w.Dps:0}", 13, UiKit.Muted));
            h.AddChild(info);
            var slot = UiKit.Label($"ARME {i + 1}", 12, UiKit.Muted);
            slot.AddThemeFontOverride("font", UiKit.BodyMedium);
            slot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            h.AddChild(slot);
            b.AddChild(h);
            Item(b, $"w{i}");
        }
        _col.AddChild(UiKit.Gap(4));
        _col.AddChild(UiKit.Label("Sélectionnez une arme pour ouvrir l'arsenal.", 14, UiKit.Dim));
    }
}
