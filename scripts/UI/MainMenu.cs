using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Menu principal (maquette E-Menu) : sept plaques à gauche, panneau d'information central (texte),
/// soldat de la classe choisie à droite, carte de profil, version dans la bande basse.
/// </summary>
public partial class MainMenu : Control
{
    private EPlate _quit;
    private VBoxContainer _info;
    private Label _notice;
    private readonly List<(EPlate plate, Func<string[]> lines)> _items = new();

    public override void _Ready()
    {
        Theme = UiE.Theme;
        UiKit.FillRect(this);
        var stage = EFrame.Build(this, "Menu principal", out _);
        var brand = UiE.Label("REMPART", 30, new Color("cfffd8"), UiE.Spaced(UiE.CondXBold, 10));
        brand.Position = new Vector2(520, 22);
        stage.AddChild(brand);

        // Soldat (classe choisie) derrière la carte de profil.
        stage.AddChild(new SoldierPortrait().At(930, 50, 340, 612));

        // Plaques.
        var col = new VBoxContainer { Position = new Vector2(130, 118), Size = new Vector2(340, 0), MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 9);
        stage.AddChild(col);
        var p = Profile.I;
        var next = MissionProgress.Next;
        string[] Progress() => new[]
        {
            "", $"Opérations terminées : {MissionProgress.ClearedCount}/{Missions.All.Length}", "",
            $"Prochaine : {MissionProgress.Label(next)}", $"Zone : {MissionProgress.Zone(next)}",
        };
        Add(col, "Jouer en solo", () => App.I.StartSolo(), () => new[] { "Jouer en solo.", "Choisir une opération et la mener seul." }.Concat(Progress()).ToArray());
        Add(col, "Héberger (LAN)", () => App.I.Host(), () => new[] { "Héberger une partie.", "Jusqu'à 4 soldats sur le réseau local." }.Concat(Progress()).ToArray());
        Add(col, "Rejoindre (LAN)", () => App.I.ShowJoin(), () => new[] { "Rejoindre une partie.", "Parties détectées sur le réseau local ou adresse IP de l'hôte." });
        Add(col, "Arsenal", () => App.I.ShowArsenal(() => App.I.ShowMain()), () => new[]
        {
            "Arsenal.", "Choisir les armes de chaque classe.", "",
            $"Armes possédées : {p.Data.Arsenal.Count}",
        });
        Add(col, "Options", () => App.I.ShowOptions(), () => new[] { "Options.", "Contrôles, vidéo, audio et jeu." });
        Add(col, "Guide", () => App.I.ShowGuide(), () => new[] { "Guide.", "Commandes, classes, butin et progression." });
        _quit = Add(col, "Quitter", () => App.I.Quit(), () => new[] { "Quitter.", "Retour au bureau." });
        _quit.Dim = true;
        UiKit.ChainVertical(_items.Select(i => (Control)i.plate).ToArray());

        // Panneau d'information central (texte seulement).
        var panel = UiE.Panel(3, 18, 16).At(500, 120, 420, 300);
        panel.Size = new Vector2(420, 300);
        stage.AddChild(panel);
        _info = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _info.AddThemeConstantOverride("separation", 0);
        panel.AddChild(_info);
        _notice = UiE.Label("", 16, UiE.Danger);
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notice.Position = new Vector2(500, 430);
        _notice.Size = new Vector2(420, 0);
        stage.AddChild(_notice);

        // Carte de profil.
        var cls = p.Data.SelectedClass;
        var card = new ECard { Position = new Vector2(900, 440), Size = new Vector2(340, 0) };
        card.SetRows(new[]
        {
            new ECard.Row(new[] { new ECard.Cell("Profil", 96), new ECard.Cell(Settings.I.PlayerName, 0, 20) }, 32, true),
            new ECard.Row(new[] { new ECard.Cell("Classe", 96), new ECard.Cell(Classes.Get(cls).Name) }, 25),
            new ECard.Row(new[] { new ECard.Cell("Armure:", 96), new ECard.Cell($"{p.Armor(cls):0}") }, 25),
            new ECard.Row(new[] { new ECard.Cell("Missions", 96), new ECard.Cell($"{MissionProgress.ClearedCount}/{Missions.All.Length}") }, 29),
        });
        stage.AddChild(card);

        var ver = UiE.Label($"v{ProjectSettings.GetSetting("application/config/version")}", 15, new Color("cfefd6"));
        ver.Position = new Vector2(60, 674);
        stage.AddChild(ver);
        var hints = EFrame.Hints(stage);
        hints.AddChild(UiE.KeyHint("Entrée:OK"));
        hints.AddChild(UiE.KeyHint("Échap:Retour", () => EPlate_Focus(_quit)));
        hints.AddChild(UiE.KeyHint("↑↓:Choisir"));

        ShowInfo(_items[0].lines());
        UiKit.FocusLater(_items[0].plate);
        UiKit.FadeIn(this);
    }

    private EPlate Add(VBoxContainer col, string title, Action onClick, Func<string[]> lines)
    {
        var b = new EPlate(title, onClick, 310);
        col.AddChild(b);
        b.FocusEntered += () => ShowInfo(lines());
        _items.Add((b, lines));
        return b;
    }

    private static void EPlate_Focus(Control c) => UiKit.FocusSilently(c);

    private void ShowInfo(string[] lines)
    {
        foreach (var c in _info.GetChildren()) c.QueueFree();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == "") { _info.AddChild(UiKit.Gap(14)); continue; }
            var head = i < 2;
            var green = !head && lines[i].StartsWith("Opérations");
            var l = UiE.Label(lines[i], head ? 19 : 17, head ? UiE.Text : green ? UiE.Mint : UiE.TextDim);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(380, 0);
            l.AddThemeConstantOverride("line_spacing", -2);
            _info.AddChild(l);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Échap : place le focus sur « Quitter » (une seconde validation quitte), sans fermer brutalement.
        if (!e.IsActionPressed("ui_cancel")) return;
        UiKit.FocusSilently(_quit);
        GetViewport().SetInputAsHandled();
    }

    public void Notice(string text) => _notice.Text = text ?? "";
}
