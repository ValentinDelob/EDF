using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Sélection de mission (maquette E-Mission), en solo comme en LAN : liste des missions débloquées à gauche,
/// briefing + vignette au centre, soldat, carte de classe et escouade à droite. Choisir une mission ouvre la
/// fenêtre « Difficulté » ; choisir une difficulté lance la mission (en LAN, dès que l'escouade est prête).
/// Les clients voient le choix de l'hôte et se déclarent prêts.
/// </summary>
public partial class LobbyMenu : Control
{
    private EStage _stage;
    private VBoxContainer _list;
    private readonly Dictionary<int, EPlate> _plates = new();
    private Label _brief, _choice;
    private TextureRect _thumb;
    private ECard _classCard, _squadCard;
    private SoldierPortrait _soldier;
    private EPlate _ready;
    private HBoxContainer _hints;
    private Control _diffLayer;
    private readonly List<EPlate> _diffPlates = new();
    private Label _diffMission;
    private int _diffFor = -1;
    private DeployOverlay _waiting;
    private bool _launchPending;
    private int _shownMission = -1;

    private bool Host => Net.I.IsHost;

    public override void _Ready()
    {
        Theme = UiE.Theme;
        UiKit.FillRect(this);
        MouseFilter = MouseFilterEnum.Stop;
        _stage = EFrame.Build(this, "Sélection de mission", out _);
        if (!Net.I.IsSolo)
        {
            var session = UiE.Label("", 20, new Color("cfffd8"), UiE.CondSemi);
            session.Name = "Session";
            session.Position = new Vector2(500, 28);
            _stage.AddChild(session);
        }

        _soldier = new SoldierPortrait().At(936, 60, 330, 594);
        _stage.AddChild(_soldier);

        _list = new VBoxContainer { Position = new Vector2(112, 106), Size = new Vector2(330, 0), MouseFilter = MouseFilterEnum.Ignore };
        _list.AddThemeConstantOverride("separation", 7);
        _stage.AddChild(_list);

        // Briefing (texte) + vignette.
        var box = new Panel { Position = new Vector2(448, 108), Size = new Vector2(452, 494), MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        box.AddThemeStyleboxOverride("panel", UiE.PanelBox(3, 0, 0));
        _stage.AddChild(box);
        var text = new VBoxContainer { Position = new Vector2(18, 14), Size = new Vector2(416, 0), MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 0);
        box.AddChild(text);
        _brief = UiE.Label("", 18);
        _brief.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _brief.CustomMinimumSize = new Vector2(412, 0);
        _brief.AddThemeConstantOverride("line_spacing", -3);
        text.AddChild(_brief);
        _choice = UiE.Label("", 16, UiE.Mint);
        _choice.CustomMinimumSize = new Vector2(412, 0);
        text.AddChild(_choice);
        _thumb = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        box.AddChild(_thumb);
        var rule = new ColorRect { Color = UiE.Frame, MouseFilter = MouseFilterEnum.Ignore, Name = "ThumbRule" };
        box.AddChild(rule);
        text.Resized += () => LayoutThumb(text, rule);
        _brief.Resized += () => LayoutThumb(text, rule);

        // Carte de classe (← / → : classe ; Entrée sur une arme : arsenal).
        _classCard = new ECard { Position = new Vector2(912, 384), Size = new Vector2(326, 0), StepRow = 0 };
        _classCard.Interactive();
        _classCard.OnStep = d =>
        {
            var n = Classes.All.Length;
            var i = System.Array.FindIndex(Classes.All, c => c.Id == Profile.I.Data.SelectedClass);
            Net.I.SetLocalClass(Classes.All[(i + d + n) % n].Id);
            Refresh();
        };
        _classCard.OnRow = r => { if (r >= 2) App.I.ShowArsenal(() => App.I.ShowLobby()); else _classCard.OnStep(1); };
        _stage.AddChild(_classCard);
        _squadCard = new ECard { Position = new Vector2(912, 518), Size = new Vector2(326, 0), ColumnRule = false, Visible = !Net.I.IsSolo };
        _stage.AddChild(_squadCard);

        _hints = EFrame.Hints(_stage);
        BuildDifficultyWindow();
        Refresh();
        Callable.From(FocusInitial).CallDeferred();
        UiKit.FadeIn(this);
    }

    /// <summary>Focus d'ouverture : la mission choisie (hôte) ou « Prêt » (client). L'écran peut déjà être fermé.</summary>
    private void FocusInitial()
    {
        if (!IsInstanceValid(this) || !IsInsideTree()) return;
        Control target = Host
            ? (_plates.TryGetValue(Net.I.LobbyMission, out var p) ? p : _plates.Values.LastOrDefault())
            : _ready;
        if (target != null && IsInstanceValid(target)) UiKit.FocusSilently(target);
    }

    private void LayoutThumb(Control text, ColorRect rule)
    {
        // Vignette sous le texte (au moins à 150 px du haut, comme la maquette), jusqu'au bas du cadre.
        var top = Mathf.Max(150f, text.Position.Y + text.GetCombinedMinimumSize().Y + 10f);
        rule.Position = new Vector2(0, top);
        rule.Size = new Vector2(452, 3);
        _thumb.Position = new Vector2(3, top + 3);
        _thumb.Size = new Vector2(446, 494 - 3 - top - 3);
    }

    private void Leave()
    {
        Net.I.Leave();
        App.I.ShowMain();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        if (_waiting != null) { CancelLaunch(); return; }
        if (_diffLayer.Visible) { CloseDifficulty(); return; }
        Leave();
    }

    // ------------------------------------------------------------ Liste et panneaux

    public void Refresh()
    {
        if (!IsInsideTree()) return;
        var players = Net.I.Players.Values.OrderBy(p => p.Id).ToList();
        if (_stage.GetNodeOrNull<Label>("Session") is { } s) s.Text = $"{Net.I.SessionName} · {players.Count}/{Net.MaxPlayers}";
        BuildList();
        ShowMission(Net.I.LobbyMission);
        BuildCards(players);
        BuildHints();
        if (_launchPending) TryLaunch();
    }

    private void BuildList()
    {
        // Missions visibles : débloquées (et, chez un client, celle choisie par l'hôte).
        var visible = Missions.All.Where(m => MissionProgress.Unlocked(m.Index) || (!Host && m.Index == Net.I.LobbyMission)).Select(m => m.Index).ToList();
        var focusedMission = _plates.FirstOrDefault(kv => kv.Value.HasFocus()).Value != null ? _plates.First(kv => kv.Value.HasFocus()).Key : -1;
        var readyFocused = _ready != null && _ready.HasFocus();
        if (_plates.Count != visible.Count || !visible.All(_plates.ContainsKey))
        {
            foreach (var c in _list.GetChildren()) c.QueueFree();
            _plates.Clear();
            _ready = null;
            foreach (var i in visible)
            {
                var idx = i;
                var m = Missions.All[i];
                var p = new EPlate(m.Name, () => OpenDifficulty(idx), 300) { Number = $"{i + 1:00}", Check = MissionProgress.Cleared(i) };
                if (!Host) { p.FocusMode = FocusModeEnum.None; p.MouseFilter = MouseFilterEnum.Ignore; p.MouseDefaultCursorShape = CursorShape.Arrow; }
                // Survol / focus : le briefing suit la mission désignée.
                p.FocusEntered += () => ShowMission(idx);
                // Survol à la souris : la mission s'actualise tout de suite (et prend le focus, clavier et souris restent d'accord).
                p.MouseEntered += () =>
                {
                    if (_diffLayer != null && _diffLayer.Visible) return;
                    ShowMission(idx);
                    if (p.FocusMode != FocusModeEnum.None && !p.HasFocus()) p.GrabFocus();
                };
                _list.AddChild(p);
                _plates[i] = p;
            }
            if (!Host)
            {
                _list.AddChild(UiKit.Gap(6));
                _ready = new EPlate("Prêt", () => { var me = Net.I.Players.TryGetValue(Net.I.MyId, out var pi) && pi.Ready; Net.I.SetLocalReady(!me); }, 300) { Icon = UiE.Icon.Check };
                _list.AddChild(_ready);
                _ready.FocusNeighborRight = _ready.GetPathTo(_classCard);
            }
            var focusables = _plates.Values.Where(p => p.FocusMode != FocusModeEnum.None).Cast<Control>().ToList();
            if (_ready != null) focusables.Add(_ready);
            UiKit.ChainVertical(focusables.ToArray());
            foreach (var f in focusables) f.FocusNeighborRight = f.GetPathTo(_classCard);
            _classCard.FocusNeighborLeft = _classCard.GetPathTo(focusables.Count > 0 ? focusables[0] : _classCard);
            _classCard.FocusNeighborTop = _classCard.GetPathTo(_classCard);
            _classCard.FocusNeighborBottom = _classCard.GetPathTo(_classCard);
            if (focusedMission >= 0 && _plates.TryGetValue(focusedMission, out var fp)) UiKit.FocusLater(fp);
            else if (readyFocused && _ready != null) UiKit.FocusLater(_ready);
        }
        foreach (var (i, p) in _plates)
        {
            // Chez le client, la mission de l'hôte reste allumée ; chez l'hôte, celle dont la fenêtre est ouverte.
            p.Lit = !Host ? i == Net.I.LobbyMission : _diffLayer != null && _diffLayer.Visible && i == _diffFor;
            p.Check = MissionProgress.Cleared(i);
        }
        if (_ready != null)
        {
            var me = Net.I.Players.TryGetValue(Net.I.MyId, out var pi) && pi.Ready;
            _ready.Title = me ? "Prêt (annuler)" : "Prêt";
            _ready.Lit = me; // prêt : plaque allumée
            _ready.QueueRedraw();
        }
    }

    private void ShowMission(int idx)
    {
        if (idx < 0 || idx >= Missions.All.Length) return;
        _shownMission = idx;
        var m = Missions.All[idx];
        var cond = $"Conditions : {MissionProgress.Time(m)}" + (m.Weather != Weather.Clear ? (m.Weather == Weather.Rain ? ", pluie" : ", orage") : "");
        _brief.Text = $"Opération {idx + 1:00} : {m.Name}.\nType : {MissionProgress.Goal(m)}.\n{cond}. Zone : {MissionProgress.Zone(m)}.\n\n{m.Briefing}";
        // Choix de l'hôte (clients et escouade), en vert sous le briefing.
        _choice.Text = !Net.I.IsSolo ? (Host ? "" : $"Choix de l'hôte : {MissionProgress.Label(Missions.All[Net.I.LobbyMission])} · {Difficulties.Names[(int)Net.I.LobbyDifficulty]}") : "";
        _choice.Visible = _choice.Text != "";
        _thumb.Texture = MissionProgress.Thumbnail(idx);
    }

    private void BuildCards(List<PlayerInfo> players)
    {
        var p = Profile.I;
        var cls = p.Data.SelectedClass;
        _soldier.SetClass(cls);
        var rows = new List<ECard.Row>
        {
            new(new[] { new ECard.Cell("Classe", 84), new ECard.Cell(Classes.Get(cls).Name, 0, 19) }, 30, true),
            new(new[] { new ECard.Cell("Armure:", 84), new ECard.Cell($"{p.Armor(cls):0}", 0, 19), new ECard.Cell("", 70) }, 26),
        };
        var eq = p.Equipped(cls);
        for (var i = 0; i < eq.Count; i++)
            rows.Add(new(new[] { new ECard.Cell($"Arme {i + 1}", 84), new ECard.Cell(eq[i].T.Name), new ECard.Cell($"»Lv{eq[i].Level}", 70) }, i == eq.Count - 1 ? 28 : 24));
        _classCard.SetRows(rows);

        if (Net.I.IsSolo) return;
        var sq = new List<ECard.Row>
        {
            new(new[] { new ECard.Cell("Escouade", 0, 15, UiE.Mint), new ECard.Cell($"»{players.Count}/{Net.MaxPlayers}", 60, 15) }, 22, true),
        };
        foreach (var pl in players)
        {
            var hostTag = pl.Id == 1 ? " (hôte)" : "";
            var status = pl.Id == 1 ? "»HÔTE" : pl.Ready ? "»PRÊT" : "»PAS PRÊT";
            sq.Add(new(new[] { new ECard.Cell($"{pl.Name} · {Classes.Get(pl.Class).Name}{hostTag}", 0, 15), new ECard.Cell(status, 90, 15, pl.Id == 1 || pl.Ready ? UiE.Bright : UiE.DangerSoft) }, 20));
        }
        for (var i = players.Count; i < Net.MaxPlayers; i++)
            sq.Add(new(new[] { new ECard.Cell("— emplacement libre —", 0, 15, UiE.TextMuted) }, 20));
        _squadCard.SetRows(sq);
        // La carte de classe s'allonge avec le nombre d'armes : l'escouade reste en dessous.
        _squadCard.Position = new Vector2(912, Mathf.Max(518, _classCard.Position.Y + _classCard.Size.Y + 16));
    }

    private void BuildHints()
    {
        foreach (var c in _hints.GetChildren()) c.QueueFree();
        if (Host) _hints.AddChild(UiE.KeyHint("Entrée:Choisir"));
        else _hints.AddChild(UiE.KeyHint("Entrée:Prêt"));
        _hints.AddChild(UiE.KeyHint("←→:Classe", () => UiKit.FocusSilently(_classCard)));
        _hints.AddChild(UiE.KeyHint("Échap:Retour", Leave));
    }

    // ------------------------------------------------------------ Fenêtre « Difficulté »

    private void BuildDifficultyWindow()
    {
        _diffLayer = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        UiKit.FillRect(_diffLayer);
        AddChild(_diffLayer);
        _diffLayer.AddChild(new ColorRect { Color = new Color(2 / 255f, 12 / 255f, 6 / 255f, 0.78f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        var stage = new EStage();
        _diffLayer.AddChild(stage);
        var panel = new PanelContainer { Position = new Vector2(470, 150), Size = new Vector2(380, 0), MouseFilter = MouseFilterEnum.Stop };
        var sb = UiE.PanelBox(3, 20, 18, 0.95f, 16);
        sb.ContentMarginBottom = 14;
        panel.AddThemeStyleboxOverride("panel", sb);
        stage.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        panel.AddChild(v);
        _diffMission = UiE.Label("", 15, UiE.Mint);
        v.AddChild(_diffMission);
        v.AddChild(UiE.Title("Difficulté", 30));
        var list = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 8);
        v.AddChild(list);
        for (var d = 0; d < Difficulties.Names.Length; d++)
        {
            var di = d;
            var b = new EPlate(Difficulties.Names[d], () => PickDifficulty((Difficulty)di), 330) { Number = $"{d + 1}" };
            list.AddChild(b);
            _diffPlates.Add(b);
        }
        UiKit.ChainVertical(_diffPlates.Cast<Control>().ToArray());
        var back = UiE.KeyHint("Échap:Retour", CloseDifficulty, 16);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        back.CustomMinimumSize = new Vector2(0, 36);
        v.AddChild(back);
    }

    public void OpenDifficulty(int mission)
    {
        if (!Host) return;
        _diffFor = mission;
        _diffMission.Text = MissionProgress.Label(Missions.All[mission]);
        _diffLayer.Visible = true;
        UiKit.FadeIn(_diffLayer, 0.12f);
        foreach (var (i, p) in _plates) p.Lit = i == mission;
        UiKit.FocusLater(_diffPlates[(int)Net.I.LobbyDifficulty]);
    }

    private void CloseDifficulty()
    {
        _diffLayer.Visible = false;
        foreach (var p in _plates.Values) p.Lit = false;
        if (_plates.TryGetValue(_diffFor, out var back)) UiKit.FocusLater(back);
    }

    private void PickDifficulty(Difficulty d)
    {
        Net.I.SetLobbyMission(_diffFor, d);
        _diffLayer.Visible = false;
        _launchPending = true;
        TryLaunch();
    }

    /// <summary>Lance la mission choisie dès que tous les équipiers sont prêts (immédiatement en solo).</summary>
    private void TryLaunch()
    {
        var others = Net.I.Players.Values.Where(p => p.Id != Net.I.MyId).ToList();
        var ready = others.Count(p => p.Ready);
        var label = $"{MissionProgress.Label(Missions.All[Net.I.LobbyMission])} · {Difficulties.Names[(int)Net.I.LobbyDifficulty]}";
        if (ready == others.Count)
        {
            _launchPending = false;
            Net.I.HostStartMission();
            return;
        }
        if (_waiting == null)
        {
            _waiting = new DeployOverlay(label, "", false);
            AddChild(_waiting);
            var cancel = UiE.KeyHint("Échap:Annuler", CancelLaunch, 16);
            _waiting.AddChild(cancel);
            cancel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
            cancel.Position += new Vector2(-60, -80);
            GetViewport().GuiReleaseFocus();
        }
        _waiting.Sub = $"En attente de l'escouade : {ready}/{others.Count} équipier{(others.Count > 1 ? "s" : "")} prêt{(ready > 1 ? "s" : "")}.";
    }

    private void CancelLaunch()
    {
        _launchPending = false;
        _waiting?.QueueFree();
        _waiting = null;
        if (_plates.TryGetValue(Net.I.LobbyMission, out var p)) UiKit.FocusLater(p);
    }
}
