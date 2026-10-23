using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Salon : mission + difficulté (hôte), classe et équipement (chacun), escouade, lancement.</summary>
public partial class LobbyMenu : Control
{
    private VBoxContainer _missions, _squad, _loadout;
    private HBoxContainer _difficulty, _classes;
    private Label _classDesc, _sub;
    private Button _ready, _launch;
    private bool _isReady;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.Scrim(0.72f));
        var m = UiKit.Margin(72, 56, 72, 48);
        UiKit.FillRect(m);
        AddChild(m);
        var root = UiKit.VBox(28);
        m.AddChild(root);

        // En-tête
        var head = UiKit.HBox(24);
        head.AddChild(UiKit.Button("‹ Quitter", Leave, null, 44));
        var titles = UiKit.VBox(0);
        titles.AddChild(UiKit.Heading(Net.I.IsSolo ? "Campagne solo" : "Salon de mission", 40));
        _sub = UiKit.Label("", 15, UiKit.Muted);
        titles.AddChild(_sub);
        head.AddChild(titles);
        head.AddChild(UiKit.Spacer(false, true));
        _difficulty = UiKit.HBox(2);
        _difficulty.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_difficulty);
        root.AddChild(head);

        var cols = UiKit.HBox(40);
        cols.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(cols);

        // Missions
        var c1 = UiKit.VBox(10);
        c1.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        c1.AddChild(UiKit.Eyebrow("Mission"));
        _missions = UiKit.VBox(10);
        c1.AddChild(_missions);
        cols.AddChild(c1);

        // Classe et équipement
        var c2 = UiKit.VBox(10);
        c2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        c2.SizeFlagsStretchRatio = 1.3f;
        c2.AddChild(UiKit.Eyebrow("Classe"));
        _classes = UiKit.HBox(8);
        c2.AddChild(_classes);
        _classDesc = UiKit.Label("", 15, new Color(UiKit.Text, 0.8f));
        _classDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        c2.AddChild(_classDesc);
        c2.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var eh = UiKit.HBox();
        var eq = UiKit.Eyebrow("Équipement");
        eq.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        eh.AddChild(eq);
        eh.AddChild(UiKit.Button("Ouvrir l'arsenal", () => App.I.ShowArsenal(() => App.I.ShowLobby()), "Chip", 36));
        c2.AddChild(eh);
        _loadout = UiKit.VBox(8);
        c2.AddChild(_loadout);
        cols.AddChild(c2);

        // Escouade
        var c3 = UiKit.VBox(10);
        c3.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        c3.SizeFlagsStretchRatio = 0.8f;
        c3.AddChild(UiKit.Eyebrow(Net.I.IsSolo ? "Soldat" : "Escouade"));
        _squad = UiKit.VBox(8);
        c3.AddChild(_squad);
        c3.AddChild(UiKit.Spacer());
        _ready = UiKit.Button("Je suis prêt", ToggleReady, null, 52);
        _ready.Visible = !Net.I.IsHost;
        c3.AddChild(_ready);
        _launch = UiKit.Button("Lancer la mission", () => Net.I.HostStartMission(), "Primary", 64);
        _launch.Visible = Net.I.IsHost;
        _launch.AddThemeFontSizeOverride("font_size", 26);
        c3.AddChild(_launch);
        cols.AddChild(c3);

        Refresh();
        if (Net.I.IsHost) _launch.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Leave()
    {
        Net.I.Leave();
        App.I.ShowMain();
    }

    private void ToggleReady()
    {
        _isReady = !_isReady;
        Net.I.SetLocalReady(_isReady);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Leave(); GetViewport().SetInputAsHandled(); }
    }

    public void Refresh()
    {
        if (!IsInsideTree()) return;
        var host = Net.I.IsHost;
        _sub.Text = Net.I.IsSolo ? "Choisissez une mission, votre classe et vos armes." : $"{Net.I.SessionName} · {Net.I.Players.Count}/{Net.MaxPlayers} joueurs";

        // Difficulté
        foreach (var c in _difficulty.GetChildren()) c.QueueFree();
        for (var i = 0; i < Difficulties.Names.Length; i++)
        {
            var d = (Difficulty)i;
            var b = UiKit.Button(Difficulties.Names[i], () => { Net.I.SetLobbyMission(Net.I.LobbyMission, d); Refresh(); }, "Chip", 40);
            b.ToggleMode = true;
            b.SetPressedNoSignal(Net.I.LobbyDifficulty == d);
            b.Disabled = !host;
            _difficulty.AddChild(b);
        }

        // Missions
        foreach (var c in _missions.GetChildren()) c.QueueFree();
        foreach (var mdef in Missions.All)
        {
            var idx = mdef.Index;
            var b = UiKit.Button("", () => { Net.I.SetLobbyMission(idx, Net.I.LobbyDifficulty); Refresh(); }, "Card", 112);
            b.ToggleMode = true;
            b.SetPressedNoSignal(Net.I.LobbyMission == idx);
            b.Disabled = !host && Net.I.LobbyMission != idx;
            var v = UiKit.VBox(4);
            v.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(v);
            v.OffsetLeft = 18; v.OffsetTop = 14; v.OffsetRight = -18;
            var top = UiKit.HBox();
            var n = UiKit.Label($"{idx + 1} · {mdef.Name}", 22, UiKit.Text, true);
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(n);
            var done = Enumerable.Range(0, 5).Where(d => Profile.I.IsCleared(idx, (Difficulty)d)).Select(d => Difficulties.Names[d]).LastOrDefault();
            if (done != null) top.AddChild(UiKit.Label("✓ " + done, 13, UiKit.Good));
            v.AddChild(top);
            var desc = UiKit.Label(mdef.Briefing, 14, UiKit.Muted);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.MaxLinesVisible = 2;
            v.AddChild(desc);
            b.AddChild(v);
            _missions.AddChild(b);
        }

        // Classes
        foreach (var c in _classes.GetChildren()) c.QueueFree();
        var sel = Profile.I.Data.SelectedClass;
        foreach (var cd in Classes.All)
        {
            var cls = cd.Id;
            var b = UiKit.Button("", () => { Net.I.SetLocalClass(cls); Refresh(); }, "Card", 96);
            b.ToggleMode = true;
            b.SetPressedNoSignal(cls == sel);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var v = UiKit.VBox(2);
            v.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(v);
            v.OffsetLeft = 14; v.OffsetRight = -10; v.OffsetBottom = -12;
            v.Alignment = BoxContainer.AlignmentMode.End;
            var name = UiKit.Label(cd.Name, 17, UiKit.Text, true);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            v.AddChild(name);
            v.AddChild(UiKit.Label($"{Profile.I.Armor(cls):0} PV", 13, UiKit.Muted));
            b.AddChild(v);
            _classes.AddChild(b);
        }
        var def = Classes.Get(sel);
        _classDesc.Text = $"{def.Role}. {def.AbilityName} : {def.AbilityDescription}";

        // Équipement
        foreach (var c in _loadout.GetChildren()) c.QueueFree();
        var eq = Profile.I.Equipped(sel);
        for (var i = 0; i < eq.Count; i++)
        {
            var w = eq[i];
            var row = UiKit.Panel();
            row.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Surface, UiKit.Line, 1, 6, 16, 12));
            var h = UiKit.HBox(14);
            row.AddChild(h);
            var slot = UiKit.Label($"{i + 1}", 18, UiKit.Muted, true);
            slot.CustomMinimumSize = new Vector2(20, 0);
            slot.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(slot);
            var info = UiKit.VBox(0);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            info.AddChild(UiKit.Label($"{w.T.Name}  Nv.{w.Level}", 17));
            info.AddChild(UiKit.Label($"{w.T.Category} · DPS {w.Dps:0}", 13, UiKit.Muted));
            h.AddChild(info);
            var r = UiKit.Label(Rarities.Names[(int)w.Rarity].ToUpperInvariant(), 12, Rarities.Colors[(int)w.Rarity], true);
            r.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(r);
            _loadout.AddChild(row);
        }

        // Escouade
        foreach (var c in _squad.GetChildren()) c.QueueFree();
        foreach (var p in Net.I.Players.Values.OrderBy(p => p.Id))
        {
            var row = UiKit.HBox(10);
            row.CustomMinimumSize = new Vector2(0, 52);
            var info = UiKit.VBox(0);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            info.AddChild(UiKit.Label(p.Name + (p.Id == 1 && !Net.I.IsSolo ? "  ·  hôte" : ""), 18));
            info.AddChild(UiKit.Label($"{Classes.Get(p.Class).Name} · {p.MaxArmor:0} PV", 13, UiKit.Muted));
            row.AddChild(info);
            if (!Net.I.IsSolo)
            {
                var st = UiKit.Label(p.Id == 1 ? "" : p.Ready ? "Prêt" : "En attente", 14, p.Ready ? UiKit.Good : UiKit.Muted, true);
                st.VerticalAlignment = VerticalAlignment.Center;
                row.AddChild(st);
            }
            _squad.AddChild(row);
            _squad.AddChild(UiKit.Rule());
        }
        if (!Net.I.IsSolo)
            for (var i = Net.I.Players.Count; i < Net.MaxPlayers; i++)
            {
                var l = UiKit.Label("Emplacement libre", 15, new Color(UiKit.Muted, 0.6f));
                l.CustomMinimumSize = new Vector2(0, 44);
                l.VerticalAlignment = VerticalAlignment.Center;
                _squad.AddChild(l);
            }

        var me = Net.I.Players.TryGetValue(Net.I.MyId, out var mine) ? mine : null;
        _isReady = me?.Ready ?? false;
        _ready.Text = _isReady ? "Annuler « prêt »" : "Je suis prêt";
        if (host)
        {
            var others = Net.I.Players.Values.Where(p => p.Id != Net.I.MyId).ToList();
            var allReady = others.All(p => p.Ready);
            _launch.Text = allReady ? "Lancer la mission" : $"Lancer ({others.Count(p => p.Ready)}/{others.Count} prêts)";
        }
    }
}
