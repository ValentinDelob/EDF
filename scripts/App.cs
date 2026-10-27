using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Racine de l'application : navigation entre les écrans et lancement des missions.</summary>
public partial class App : Node
{
    public static App I { get; private set; }
    private CanvasLayer _ui;
    private Control _screen;
    private MenuBackground _bg;
    private World _world;
    private ResultsScreen _results;
    private string _pendingNotice;

    public override void _EnterTree() => I = this;

    public override void _Ready()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        _ui = new CanvasLayer { Layer = 10 };
        AddChild(_ui);
        Net.I.MissionStarting += StartMission;
        Net.I.ReturnToLobby += BackToLobby;
        Net.I.SessionEnded += reason =>
        {
            CleanupWorld();
            _pendingNotice = reason;
            ShowMain();
        };
        Net.I.LobbyChanged += () =>
        {
            if (_screen is JoinMenu && Net.I.Players.ContainsKey(Net.I.MyId)) ShowLobby();
            else if (_screen is LobbyMenu l) l.Refresh();
        };
        ShowMain();
        AutoTest.Run(this);
    }

    /// <summary>
    /// Direction E : les menus ont un fond 2D animé (<see cref="MenuBackdrop"/>, dessiné par chaque écran) ;
    /// la ville 3D (<see cref="MenuBackground"/>) n'est plus chargée derrière les menus.
    /// </summary>
    private void EnsureBackground() { }

    private void SetScreen(Control c)
    {
        // Fondu de sortie court de l'écran précédent (le nouvel écran a son propre fondu d'entrée).
        if (_screen != null && IsInstanceValid(_screen))
        {
            var old = _screen;
            // Plus d'entrées pour l'ancien écran, mais il doit continuer à traiter son fondu.
            old.MouseFilter = Control.MouseFilterEnum.Ignore;
            old.SetProcessInput(false);
            old.SetProcessUnhandledInput(false);
            foreach (var n in old.FindChildren("*", "Control", true, false))
                if (n is Control ctl) { ctl.MouseFilter = Control.MouseFilterEnum.Ignore; ctl.FocusMode = Control.FocusModeEnum.None; }
            // Tween porté par App : celui de l'écran désactivé ne serait jamais traité (l'écran resterait affiché).
            var tw = old.CreateTween();
            tw.TweenProperty(old, "modulate:a", 0f, 0.12f);
            tw.TweenCallback(Callable.From(old.QueueFree));
        }
        _screen = c;
        _ui.AddChild(c);
    }

    public void ShowMain()
    {
        EnsureBackground();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var m = new MainMenu();
        SetScreen(m);
        if (_pendingNotice != null) { m.Notice(_pendingNotice); _pendingNotice = null; }
    }

    public void ShowJoin()
    {
        EnsureBackground();
        SetScreen(new JoinMenu());
    }

    public void ShowLobby()
    {
        EnsureBackground();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetScreen(new LobbyMenu());
    }

    public void ShowArsenal(Action back)
    {
        EnsureBackground();
        SetScreen(new ArsenalMenu { OnBack = back });
    }

    public void ShowGuide()
    {
        EnsureBackground();
        SetScreen(new GuideMenu());
    }

    public void ShowOptions()
    {
        var o = new OptionsMenu { OnClose = () => { } };
        _ui.AddChild(o);
    }

    public void StartSolo()
    {
        Net.I.StartSolo();
        ShowLobby();
    }

    public void Host()
    {
        var err = Net.I.Host();
        if (err != Error.Ok)
        {
            _pendingNotice = $"Impossible d'héberger (port {Net.GamePort} occupé ?) : {err}";
            ShowMain();
            return;
        }
        ShowLobby();
    }

    private static readonly string[] Tips =
    {
        "Les caisses rouges contiennent des armes : plus la difficulté est haute, plus elles sont puissantes.",
        "Les charognards avalent les caisses au sol. Tuez-les pour tout récupérer, avec un bonus.",
        "Visez la tête des insectes : les points faibles infligent 50 % de dégâts en plus.",
        "Les nids ne craignent vraiment que les explosifs : roquettes, grenades et frappes d'artillerie.",
        "Le char de combat attend près du point de départ : approchez-vous et appuyez sur la touche d'interaction.",
        "Le Commando aéroporté partage la même énergie pour voler et recharger : ne tombez pas à court en plein ciel.",
        "L'Opérateur d'appui remplit sa jauge en infligeant des dégâts ; pleine, elle déclenche un barrage d'artillerie.",
        "Un équipier à terre se relève en maintenant la touche d'interaction près de lui.",
    };

    private Control BuildLoading(MissionDef m, Difficulty d)
    {
        // Écran « DÉPLOIEMENT » de la maquette E-Mission (mission · difficulté, barre animée), avec un conseil.
        return new DeployOverlay($"{MissionProgress.Label(m)} · {Difficulties.Names[(int)d]}",
            "Conseil : " + Tips[(int)(GD.Randi() % Tips.Length)], true);
    }

    private async void StartMission(int mission, Difficulty d, int seed)
    {
        CleanupWorld();
        _screen?.QueueFree();
        _screen = null;
        // Écran de chargement affiché pendant la génération de la ville (deux images pour qu'il soit dessiné).
        var loading = BuildLoading(Missions.All[mission], d);
        _ui.AddChild(loading);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _bg?.QueueFree();
        _bg = null;
        _world = World.Create(mission, d, seed);
        AddChild(_world);
        var fade = loading.CreateTween();
        fade.TweenInterval(0.3f);
        fade.TweenProperty(loading, "modulate:a", 0f, 0.5f);
        fade.TweenCallback(Callable.From(loading.QueueFree));
        _world.Finished += success =>
        {
            _results = new ResultsScreen
            {
                Report = _world.Report,
                OnContinue = () =>
                {
                    if (Net.I.IsHost) Net.I.HostReturnToLobby();
                },
                // Rejouer : même mission, même difficulté (l'hôte relance pour toute l'escouade).
                OnReplay = () =>
                {
                    if (Net.I.IsHost) Net.I.HostStartMission();
                },
            };
            AddChild(_results);
        };
    }

    private void BackToLobby()
    {
        CleanupWorld();
        if (Net.I.InSession) ShowLobby();
        else ShowMain();
    }

    private void CleanupWorld()
    {
        GetTree().Paused = false;
        _results?.QueueFree();
        _results = null;
        if (_world != null)
        {
            _world.QueueFree();
            _world = null;
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Quit() => GetTree().Quit();

    public World CurrentWorld => _world;
    public MenuBackground Background => _bg;
    public Control CurrentScreen => _screen;
}

/// <summary>
/// Pilotage automatique pour les tests et les captures : arguments après « -- » :
/// --autostart=0 --class=0 --difficulty=1 --shots=chemin --shot-times=4,10 --quit-after=20 --menu-shots.
/// </summary>
public static partial class AutoTest
{
    /// <summary>Attend que la mission soit chargée (durée variable, partie LAN), puis demande la place du véhicule.</summary>
    private static void BoardWhenReady(App app, int vehicleId, Vector3 near)
    {
        app.GetTree().CreateTimer(1f).Timeout += () =>
        {
            var w = app.CurrentWorld;
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) { BoardWhenReady(app, vehicleId, near); return; }
            app.GetTree().CreateTimer(1.5f).Timeout += () =>
            {
                if (w.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) return;
                w.LocalPlayer.GlobalPosition = near;
                Net.I.RequestVehicleSeat(vehicleId, true);
            };
        };
    }

    private static void ArsenalShots(App app)
    {
        var rng = new RandomNumberGenerator { Seed = 42 };
        var pity = 0;
        foreach (var cd in Classes.All)
            for (var i = 0; i < 12; i++)
            {
                var w = Loot.RollWeapon(cd.Id, (Difficulty)(i % 5), i / 11f, rng, ref pity);
                Profile.I.Grant(w);
            }
        // Migration d'armes d'un profil version 1 (rareté + 5 étoiles 1..5), lue comme depuis le fichier JSON.
        foreach (var json in new[]
                 {
                     """{"Uid":"legacy0001","TemplateId":"fa30","Level":33,"Rarity":4,"Stars":[5,3,1,2,4],"IsNew":false}""",
                     """{"Uid":"legacy0002","TemplateId":"art1","Level":20,"Rarity":2,"Stars":[4,2,5,3,1],"IsNew":false}""",
                 })
        {
            var old = System.Text.Json.JsonSerializer.Deserialize<WeaponInstance>(json);
            old.MigrateLegacy();
            GD.Print($"Migration {old.TemplateId} Nv.{old.Level} : étoiles [{string.Join(", ", old.Stars)}] → {System.Text.Json.JsonSerializer.Serialize(old)}");
            Profile.I.Data.Arsenal.Add(old);
        }
        // Migration version 3 : les doublons d'un même modèle (ajoutés tels quels ci-dessus) sont fusionnés.
        var before = Profile.I.Data.Arsenal.Count;
        Profile.I.MergeDuplicates();
        var dupLeft = Profile.I.Data.Arsenal.GroupBy(x => x.TemplateId).Count(g => g.Count() > 1);
        GD.Print($"Fusion des doublons : {before} → {Profile.I.Data.Arsenal.Count} armes, doublons restants : {dupLeft}");
        // Migration version 4 : un Grenadier d'ancien profil (2 emplacements) est complété (armes et équipements) sans toucher aux deux premiers.
        var heavyKey = SoldierClass.Heavy.ToString();
        var oldHeavy = Profile.I.Data.Equipped[heavyKey].Take(2).ToList();
        Profile.I.Data.Equipped[heavyKey] = oldHeavy.ToList();
        Profile.I.FillEquipped();
        var newHeavy = Profile.I.Data.Equipped[heavyKey];
        GD.Print($"Migration Grenadier : {oldHeavy.Count} → {newHeavy.Count} emplacements ({string.Join(", ", Profile.I.Equipped(SoldierClass.Heavy).Select(x => x.T.Name))})"
                 + (newHeavy.Count == Classes.Get(SoldierClass.Heavy).WeaponSlots && newHeavy.Take(2).SequenceEqual(oldHeavy) && newHeavy.Distinct().Count() == newHeavy.Count ? "" : " ERROR"));
        var tree = app.GetTree();
        void Key(Key k)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
        }
        void At(float s, Action a) => tree.CreateTimer(s, true, false, true).Timeout += a;
        At(3f, () => app.ShowArsenal(app.ShowMain));
        At(5.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_open"));
        At(6f, () => { Key(Godot.Key.Right); Key(Godot.Key.Down); });
        At(7f, () => Screenshot.Take(app.GetViewport(), "arsenal_nav"));
        At(7.5f, () => { Key(Godot.Key.E); Key(Godot.Key.Enter); });
        At(8.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_equip"));
        At(9f, () => Key(Godot.Key.Tab));
        At(10.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class2"));
        At(11f, () => Key(Godot.Key.Tab));
        At(11.5f, () => Key(Godot.Key.Right));
        At(12.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class3"));
        At(13f, () => Key(Godot.Key.Tab));
        At(14.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class4"));
        // Grenadier : emplacement 4 (jeu 2) visé, équipement d'une arme non équipée.
        At(15f, () => { Key(Godot.Key.E); Key(Godot.Key.E); Key(Godot.Key.E); Key(Godot.Key.Left); Key(Godot.Key.Down); Key(Godot.Key.Down); });
        At(16f, () => Key(Godot.Key.Enter));
        At(17f, () => Screenshot.Take(app.GetViewport(), "arsenal_heavy_sets"));
        // Souris : clic sur l'arme équipée n° 2, puis sur une arme libre → elle remplace la n° 2, la n° 1 ne bouge pas.
        At(18f, () => Key(Godot.Key.Tab));
        System.Collections.Generic.List<WeaponInstance> eqBefore = null;
        WeaponInstance pick = null;
        At(19f, () =>
        {
            if (app.CurrentScreen is not ArsenalMenu am) return;
            eqBefore = Profile.I.Equipped(am.ShownClass);
            pick = Profile.I.Data.Arsenal.FirstOrDefault(x => x.T.Class == am.ShownClass && !eqBefore.Contains(x));
            am.TestClick(eqBefore[1]);
        });
        At(19.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_target2"));
        At(20.5f, () => (app.CurrentScreen as ArsenalMenu)?.TestClick(pick));
        At(21f, () =>
        {
            if (app.CurrentScreen is not ArsenalMenu am || pick == null) return;
            var eq = Profile.I.Equipped(am.ShownClass);
            var ok = eq[0] == eqBefore[0] && eq[1] == pick;
            GD.Print($"[arsenal] clic arme 2 puis {pick.T.Name} : {string.Join(" / ", eq.Select(x => x.T.Name))}" + (ok ? " OK" : " ERROR"));
            Screenshot.Take(app.GetViewport(), "arsenal_replaced2");
        });
    }

    public static void Run(App app)
    {
        var dir = Screenshot.CmdArg("shots");
        // --perf : journal [perf] toutes les 3 s (processeur, rendu, appels de dessin, sections chaudes : voir Perf).
        if (Screenshot.CmdArg("perf") != null) Perf.Install(app);
        var quitAfter =Screenshot.CmdArg("quit-after");
        if (quitAfter != null && float.TryParse(quitAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
            app.GetTree().CreateTimer(q, true, false, true).Timeout += app.Quit;

        // --menu-shots=arsenal : profil de test garni d'armes de tous types, puis captures de l'arsenal
        // (navigation clavier simulée, changement d'emplacement et de classe).
        if (Screenshot.CmdArg("menu-shots") == "arsenal")
        {
            ArsenalShots(app);
            return;
        }
        if (Screenshot.CmdArg("menu-shots") != null)
        {
            var tree = app.GetTree();
            void Shot(float at, Action before, string name)
            {
                tree.CreateTimer(at, true, false, true).Timeout += () =>
                {
                    before?.Invoke();
                    tree.CreateTimer(1.2f, true, false, true).Timeout += () => Screenshot.Take(app.GetViewport(), name);
                };
            }
            Shot(3f, null, "menu_main");
            Shot(6f, app.StartSolo, "menu_lobby");
            Shot(9f, () => (app.CurrentScreen as LobbyMenu)?.OpenDifficulty(Net.I.LobbyMission), "menu_difficulty");
            Shot(12f, () => app.ShowOptions(), "menu_options");
            Shot(15f, () => app.ShowArsenal(app.ShowMain), "menu_arsenal");
            Shot(18f, app.ShowJoin, "menu_join");
            Shot(21f, app.ShowGuide, "menu_guide");
            // Encyclopédie : bestiaire partiellement rempli (profil de test), armes, opérations.
            Shot(24f, () =>
            {
                foreach (var t in new[] { EnemyType.BlackAnt, EnemyType.RedAnt, EnemyType.Wasp, EnemyType.Spider })
                    Profile.I.Data.Bestiary[t.ToString()] = 37;
                (app.CurrentScreen as GuideMenu)?.ShowTab(3);
            }, "menu_guide_bestiary");
            Shot(27f, () => (app.CurrentScreen as GuideMenu)?.ShowTab(4), "menu_guide_weapons");
            Shot(30f, () => (app.CurrentScreen as GuideMenu)?.ShowTab(5), "menu_guide_operations");
        }

        // --end-after=N : termine la mission (succès) après N s avec du butin fictif, pour tester le bilan.
        if (Screenshot.CmdArg("end-after") is { } ea && float.TryParse(ea, NumberStyles.Float, CultureInfo.InvariantCulture, out var endAfter))
            app.GetTree().CreateTimer(endAfter, true, false, true).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                w.CratesCollected[(int)CrateType.Weapon] += 3;
                w.CratesCollected[(int)CrateType.Armor] += 14;
                // Butin fixe en plus du tirage : une arme nouvelle, une arme de départ améliorée (niveau + étoiles)
                // et un doublon sans amélioration, pour vérifier toutes les lignes du bilan.
                var cls = Profile.I.Data.SelectedClass;
                var starter = Weapons.StarterLoadout[cls];
                var fresh = Weapons.ForClass(cls).FirstOrDefault(t => Profile.I.Data.Arsenal.All(a => a.TemplateId != t.Id));
                var improved = new WeaponInstance { TemplateId = starter[0], Level = 18 };
                improved.Stars[(int)WeaponStat.Damage] = 3;
                improved.Stars[(int)WeaponStat.Reload] = 2;
                MissionReport.TestDrops = new() { improved, new WeaponInstance { TemplateId = starter[1], Level = 0 } };
                if (fresh != null)
                {
                    var nw = new WeaponInstance { TemplateId = fresh.Id, Level = 12 };
                    nw.Stars[(int)WeaponStat.Damage] = 1;
                    MissionReport.TestDrops.Add(nw);
                }
                w.EndMission(true);
            };
        // --pause-at=N : ouvre le menu pause après N s (captures du menu pause).
        if (Screenshot.CmdArg("pause-at") is { } pa && float.TryParse(pa, NumberStyles.Float, CultureInfo.InvariantCulture, out var pauseAt))
            app.GetTree().CreateTimer(pauseAt, true, false, true).Timeout += () =>
            {
                app.CurrentWorld?.TogglePause();
            };
        // --tank / --heli : le joueur monte dans le char ou l'hélicoptère peu après le chargement de la mission
        // (le bot fait voler l'hélicoptère).
        if (Screenshot.CmdArg("tank") != null) BoardWhenReady(app, 1, new Vector3(14, 1, 17));
        if (Screenshot.CmdArg("heli") != null) BoardWhenReady(app, 2, new Vector3(-16, 1, 19));
        // --heli-test=down|ram : le bot vise sous l'appareil et tire (roquettes et minigun), ou fonce dans un immeuble.
        Helicopter.Test = Screenshot.CmdArg("heli-test");
        // --board=N : affiche le tableau des scores (Tab) à partir de N secondes.
        if (Screenshot.CmdArg("board") is { } bd && float.TryParse(bd, NumberStyles.Float, CultureInfo.InvariantCulture, out var boardAt))
            app.GetTree().CreateTimer(boardAt).Timeout += () => Input.ActionPress("scoreboard");
        // Test LAN : --autohost=2 (attend 2 joueurs puis lance) / --autojoin=127.0.0.1
        Player.Bot = Screenshot.CmdArg("bot") != null;
        Player.God = Screenshot.CmdArg("god") != null;
        Player.BotHoldFire = Screenshot.CmdArg("holdfire") != null;
        RoofTest(app);
        GrabTest(app);
        ClassTests(app); // --heavy-sets, --call-vehicle (AutoTest.Classes.cs)
        TankDriveTest(app); // --tank-drive (AutoTest.Tank.cs)
        Edf5Tests(app); // --weapons, --edf5-fire (AutoTest.Edf5.cs)
        AnimTest(app); // --anim-test (AutoTest.Anim.cs)
        LookTests(app); // --crate-test (AutoTest.Look.cs)
        ObjectiveArgs(app); // --survive-time, --obj-hp, --convoy-speed, --objlog, --timescale (AutoTest.Objectives.cs)
        if (Screenshot.CmdArg("name") is { } nm) Settings.I.PlayerName = nm;
        if (Screenshot.CmdArg("class") is { } cc && int.TryParse(cc, out var ccls)) Profile.I.Data.SelectedClass = (SoldierClass)ccls;
        var mission0 = int.TryParse(Screenshot.CmdArg("mission") ?? "0", out var mm) ? mm : 0;
        if (Screenshot.CmdArg("autohost") is { } ah && int.TryParse(ah, out var expected))
        {
            app.GetTree().CreateTimer(0.5f).Timeout += app.Host;
            var started = false;
            Net.I.LobbyChanged += () =>
            {
                if (started || Net.I.Players.Count < expected) return;
                started = true;
                app.GetTree().CreateTimer(2f).Timeout += () =>
                {
                    Net.I.SetLobbyMission(mission0, Difficulty.Normal);
                    Net.I.HostStartMission();
                };
            };
            WreckTest(app, dir); // --wreck=N : destructions décidées par l'hôte, vues du client
            TitanTest(app); // --titan-plates : plaques brisées par l'hôte, vues du client
            MothershipTest(app, dir); // --mothership-act : canon, tourelles, lancements décidés par l'hôte
            AlienNetLog(app);
            ScheduleShots(app, dir, "host");
            return;
        }
        if (Screenshot.CmdArg("autojoin") is { } ip)
        {
            app.GetTree().CreateTimer(2f).Timeout += () => { app.ShowJoin(); Net.I.Join(ip); };
            AlienNetLog(app);
            ScheduleShots(app, dir, "client");
            return;
        }

        var auto = Screenshot.CmdArg("autostart");
        if (auto == null) return;
        Hud.ForceHidden = Screenshot.CmdArg("nohud") != null; // --nohud : captures sans interface (fonds de maquettes)
        // --nohud : masque aussi les noms flottants (Label3D) pour des vignettes de mission sans texte.
        if (Hud.ForceHidden)
        {
            void HideLabels()
            {
                if (app.CurrentWorld is { } w)
                    foreach (var n in w.FindChildren("*", "Label3D", true, false))
                    {
                        ((Label3D)n).Visible = false;
                        ((Label3D)n).Transparency = 1f;
                    }
                app.GetTree().CreateTimer(0.5f).Timeout += HideLabels;
            }
            HideLabels();
        }
        if (Screenshot.CmdArg("class") is { } c && int.TryParse(c, out var cls)) Profile.I.Data.SelectedClass = (SoldierClass)cls;
        var diff = Screenshot.CmdArg("difficulty") is { } ds && int.TryParse(ds, out var di) ? (Difficulty)di : Difficulty.Normal;
        int.TryParse(auto, out var mission);
        Player.Bot = Screenshot.CmdArg("bot") != null;
        app.GetTree().CreateTimer(1.0f).Timeout += () =>
        {
            app.StartSolo();
            Net.I.SetLobbyMission(mission, diff);
            Net.I.HostStartMission();
        };
        // --stress=N : fait apparaître N fourmis autour du joueur (test de performance), --novsync.
        if (Screenshot.CmdArg("novsync") != null)
        {
            // Mesure sans plafond : ni synchronisation verticale, ni limite d'images du profil.
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
        }
        if (Screenshot.CmdArg("stress") is { } st && int.TryParse(st, out var stressN))
            app.GetTree().CreateTimer(4f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                for (var i = 0; i < stressN; i++)
                {
                    var a = i * 2.399f;
                    var r = 40f + (i % 30) * 3f;
                    w.HostSpawnEnemy(i % 7 == 0 ? EnemyType.RedAnt : EnemyType.BlackAnt, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r - 40));
                }
            };
        // --bury=N : envoie N ennemis sous la carte (test de la réapparition des ennemis coincés, journal avec --fps).
        if (Screenshot.CmdArg("bury") is { } bu && int.TryParse(bu, out var buryN))
            app.GetTree().CreateTimer(8f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                // Moitié sous la carte, moitié emmurés au cœur d'un immeuble.
                var blds = w.Buildings.Where(b => !b.Destroyed).ToList();
                var k = 0;
                foreach (var e in w.EnemiesById.Values.Where(e => !e.IsStatic && !e.Def.Boss).Take(buryN).ToList())
                {
                    e.GlobalPosition = k % 2 == 0 || blds.Count == 0
                        ? new Vector3(e.GlobalPosition.X, -20f, e.GlobalPosition.Z)
                        : blds[k % blds.Count].GlobalPosition + Vector3.Up * 1.5f;
                    k++;
                }
                GD.Print($"[test] {buryN} ennemis enterrés ou emmurés");
                var ids = w.EnemiesById.Values.Where(e => !e.IsStatic && !e.Def.Boss).Take(buryN).Select(e => e.Id).ToList();
                for (var t = 3; t <= 24; t += 3)
                    app.GetTree().CreateTimer(t).Timeout += () =>
                        GD.Print("[test] " + string.Join(" | ", ids.Select(i => w.EnemiesById.TryGetValue(i, out var x) ? $"{i}:{x.State} {x.GlobalPosition.Snapped(Vector3.One * 0.1f)}" : $"{i}:retiré")));
            };
        if (Screenshot.CmdArg("showcase") is { } sc && sc is not ("machines" or "titan" or "aliens" or "mothership")) Showcase(app);
        MachineTest(app, dir);
        TitanTest(app); // --titan-act, --titan-plates, --titan-hp (AutoTest.Titan.cs)
        MothershipTest(app, dir); // --showcase=mothership, --mothership-act, --mothership-cam… (AutoTest.Mothership.cs)
        AlienTest(app, dir);
        BoomTest(app, dir);
        WreckTest(app, dir);
        ScheduleShots(app, dir, $"mission{mission}");
    }

    /// <summary>
    /// --showcase[=big|face] : spécimens figés devant le soldat immobile, dimensions réelles dans le journal (« [taille] »).
    /// Par défaut un de chaque insecte, big : nid et reine, face : une fourmi noire juste devant le soldat (comparaison d'échelle avec EDF).
    /// </summary>
    private static void Showcase(App app)
    {
        app.GetTree().CreateTimer(1f).Timeout += () =>
        {
            var w = app.CurrentWorld;
            // Attend que la mission soit chargée (écran de déploiement) et le soldat en place.
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 1f) { Showcase(app); return; }
            Enemy.Frozen = true;
            Player.BotHoldFire = true;
            var mode = Screenshot.CmdArg("showcase");
            var spawned = new System.Collections.Generic.List<Enemy>();
            var pp = w.LocalPlayer.GlobalPosition;
            Enemy Put(EnemyType t, Vector3 p, float yaw)
            {
                var e = w.HostSpawnEnemy(t, p);
                e.GlobalPosition = p;
                e.Rotation = new Vector3(0, yaw, 0);
                e.MaxHealth = e.Health = 1e7f; // les alliés ne doivent pas abattre les spécimens
                spawned.Add(e);
                return e;
            }
            // Soldat immobile (le bot vise le spécimen le plus proche) ; les alliés qui l'entourent au départ masqueraient le cadrage.
            Player.BotStill = true;
            foreach (var act in new[] { "move_left", "move_right", "sprint" }) Input.ActionRelease(act);
            foreach (var n in w.FindChildren("*", "Node3D", true, false))
                if (n is Ally a) a.Visible = false;
            if (mode == "face")
            {
                // Une fourmi noire juste devant le soldat, deux autres plus loin.
                foreach (var (t, off, turn) in new[] { (EnemyType.BlackAnt, new Vector3(-1f, 0, -7.5f), 0.35f), (EnemyType.BlackAnt, new Vector3(22f, 0, -40f), -0.4f), (EnemyType.RedAnt, new Vector3(-26f, 0, -48f), 0.5f) })
                {
                    var p = new Vector3(pp.X + off.X, 0, pp.Z + off.Z);
                    Put(t, p, Mathf.Atan2(pp.X - p.X, pp.Z - p.Z) + turn);
                }
            }
            else if (mode == "big")
            {
                var nest = Enemies.Get(EnemyType.Nest).Size;
                var queen = Enemies.Get(EnemyType.Queen).Size;
                Put(EnemyType.Nest, new Vector3(pp.X - 14f * nest - 10f, 0, pp.Z - 16f - 16f * nest), 0.6f);
                Put(EnemyType.Queen, new Vector3(pp.X + 2f * queen, 0, pp.Z - 10f - 5f * queen), 0.9f);
            }
            else
            {
                // Rangée devant le soldat, espacée selon la taille de chacun, reculée selon le plus grand.
                var types = new[] { EnemyType.BlackAnt, EnemyType.Spider, EnemyType.AcidAnt, EnemyType.Drone, EnemyType.Wasp };
                var widths = types.Select(t => 5.5f * Enemies.Get(t).Size).ToArray();
                var x = pp.X - widths.Sum() / 2f;
                var z = pp.Z - 12f - 8f * types.Max(t => Enemies.Get(t).Size);
                for (var i = 0; i < types.Length; i++)
                {
                    x += widths[i] / 2f;
                    var fly = types[i] is EnemyType.Wasp or EnemyType.Drone;
                    Put(types[i], new Vector3(x, fly ? 3.5f * Enemies.Get(types[i]).Size : 0, z), 0.6f);
                    x += widths[i] / 2f;
                }
                // Mille-pattes au premier plan (le plus proche : le bot le vise), de trois quarts, le corps en S pour montrer la trace.
                var cs = Enemies.Get(EnemyType.Centipede).Size;
                Put(EnemyType.Centipede, new Vector3(pp.X + 3f * cs, 0, pp.Z - 6f * cs), 1.4f).Model.SeedCentipedeTrail(0.45f);
            }
            // Dimensions réelles de chaque modèle (journal), une fois sortis de terre.
            app.GetTree().CreateTimer(3f).Timeout += () =>
            {
                foreach (var e in spawned)
                {
                    if (!GodotObject.IsInstanceValid(e)) continue;
                    var b = e.Model.Bounds();
                    GD.Print($"[taille] {e.Def.Type} : longueur {b.Size.Z:0.0} m, envergure {b.Size.X:0.0} m, hauteur {b.End.Y:0.0} m (Size {e.Def.Size:0.00})");
                }
            };
        };
    }

    private static float[] FloatList(string s) => (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();

    /// <summary>
    /// Tests des explosions (effets seuls, aucun dégât) :
    /// --boom=N : N explosions par seconde autour du joueur à partir de 5 s (salves de roquettes, mesure avec --fps) ;
    /// --boom-at=T1,T2 [--boom-kind=K1,K2] [--boom-radius=R1,R2] [--boom-dist=D] : une explosion à D m (20) devant la caméra
    /// à chaque instant, avec captures à +0,05 / 0,2 / 0,6 / 1,5 / 4 s (le bot reste immobile et ne tire pas).
    /// </summary>
    private static void BoomTest(App app, string dir)
    {
        var tree = app.GetTree();
        // --boom=0 : même mesure sans explosion (référence).
        if (Screenshot.CmdArg("boom") is { } bs && float.TryParse(bs, NumberStyles.Float, CultureInfo.InvariantCulture, out var perSec) && perSec >= 0)
        {
            var rng = new RandomNumberGenerator { Seed = 7 };
            var count = 0;
            void Tick()
            {
                if (Screenshot.CmdArg("novsync") != null && (Engine.MaxFps != 0 || DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled))
                {
                    // Les réglages vidéo du profil sont réappliqués au lancement de la mission : mesure sans plafond.
                    DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                    Engine.MaxFps = 0;
                }
                var w = app.CurrentWorld;
                if (perSec <= 0) { tree.CreateTimer(1f, false).Timeout += Tick; return; }
                if (w?.LocalPlayer != null && GodotObject.IsInstanceValid(w.LocalPlayer))
                {
                    var cam = w.GetViewport().GetCamera3D();
                    var fwd = cam != null ? -cam.GlobalBasis.Z : Vector3.Forward;
                    var yaw = Mathf.Atan2(fwd.X, fwd.Z) + rng.RandfRange(-1.1f, 1.1f);
                    var d = rng.RandfRange(10f, 55f);
                    var p = w.LocalPlayer.GlobalPosition + new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * d;
                    p.Y = rng.Randf() < 0.8f ? 0.4f : rng.RandfRange(2f, 12f);
                    var roll = rng.Randf();
                    int kind = roll < 0.12f ? 2 : roll < 0.17f ? 4 : 0;
                    var radius = roll > 0.9f ? rng.RandfRange(12f, 22f) : rng.RandfRange(4f, 10f);
                    if (kind == 4) Effects.Explosion(w, p, radius * 0.6f, 4); else w.OnExplosionFx(p, radius, kind);
                    count++;
                }
                tree.CreateTimer(1f / perSec, false).Timeout += Tick;
            }
            tree.CreateTimer(5f, false).Timeout += Tick;
            // Moyenne d'images par seconde depuis 10 s (chargement et premières explosions exclus) et pire image.
            ulong startFrame = 0, startMs = 0;
            var worst = 0.0;
            void Report()
            {
                var now = Time.GetTicksMsec();
                if (startMs == 0 && app.CurrentWorld != null && app.CurrentWorld.Elapsed > 6f) { startMs = now; startFrame = Engine.GetProcessFrames(); worst = 0; }
                var avg = startMs > 0 && now > startMs ? (Engine.GetProcessFrames() - startFrame) * 1000.0 / (now - startMs) : 0;
                GD.Print($"[boom] {count} explosions, {Engine.GetFramesPerSecond()} i/s (moyenne {avg:0}, pire image {worst * 1000:0} ms), {Effects.Stats}");
                tree.CreateTimer(2f, true, false, true).Timeout += Report;
            }
            void Worst()
            {
                if (startMs > 0) worst = Mathf.Max(worst, app.GetProcessDeltaTime());
                tree.CreateTimer(0, true, false, true).Timeout += Worst;
            }
            tree.CreateTimer(6f, true, false, true).Timeout += Report;
            tree.CreateTimer(6f, true, false, true).Timeout += Worst;
        }

        if (Screenshot.CmdArg("boom-at") is not { } ba) return;
        var times = FloatList(ba);
        var kinds = FloatList(Screenshot.CmdArg("boom-kind") ?? "0");
        var radii = FloatList(Screenshot.CmdArg("boom-radius") ?? "6");
        var dist = float.TryParse(Screenshot.CmdArg("boom-dist") ?? "20", NumberStyles.Float, CultureInfo.InvariantCulture, out var dd) ? dd : 20f;
        Player.BotStill = true;
        Player.BotHoldFire = true;
        for (var i = 0; i < times.Length; i++)
        {
            var kind = (int)kinds[Mathf.Min(i, kinds.Length - 1)];
            var radius = radii[Mathf.Min(i, radii.Length - 1)];
            var at = times[i];
            tree.CreateTimer(at, true, false, true).Timeout += () =>
            {
                var w = app.CurrentWorld;
                var cam = w?.GetViewport().GetCamera3D();
                if (cam == null) return;
                var fwd = -cam.GlobalBasis.Z;
                fwd.Y = 0;
                var p = cam.GlobalPosition + fwd.Normalized() * dist;
                p.Y = 0.4f;
                if (kind == 4) Effects.Explosion(w, p, radius, 4); else w.OnExplosionFx(p, radius, kind);
            };
            if (dir == null) continue;
            foreach (var dt in new[] { 0.05f, 0.2f, 0.6f, 1.5f, 4f })
            {
                var name = $"boom_k{kind}_r{radius:0}_t{at:0}_{dt.ToString("0.00", CultureInfo.InvariantCulture)}";
                tree.CreateTimer(at + dt, true, false, true).Timeout += () =>
                {
                    // Enregistrement PNG hors du fil principal : les captures suivantes restent à l'heure.
                    var img = app.GetViewport().GetTexture().GetImage();
                    var path = $"{dir}/{name}_f{Engine.GetFramesDrawn()}.png";
                    System.Threading.Tasks.Task.Run(() => img.SavePng(path));
                };
            }
        }
    }

    /// <summary>
    /// --roof[=N] : le soldat (bot immobile) est posé sur le toit de l'immeuble le plus proche du départ et N insectes (30)
    /// apparaissent autour : ils doivent escalader les façades pour l'atteindre. --holdfire : le bot vise sans tirer.
    /// Fonctionne aussi avec --autohost (le soldat de l'hôte monte sur le toit, test de l'escalade vue du client).
    /// </summary>
    private static void RoofTest(App app)
    {
        if (Screenshot.CmdArg("roof") is not { } arg) return;
        var n = int.TryParse(arg, out var v) ? v : 30;
        Player.BotStill = true;
        void Try()
        {
            app.GetTree().CreateTimer(1f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 2.5f) { Try(); return; }
                // Immeuble le plus proche du départ dont le toit est plat à l'endroit où l'on pose le soldat.
                Building best = null;
                var spot = Vector3.Zero;
                var bd = float.MaxValue;
                var space = w.GetWorld3D().DirectSpaceState;
                foreach (var b in w.Buildings)
                {
                    if (b.Destroyed || b.Size.Y < 6f || b.Size.Y > 26f || b.Size.X < 9f || b.Size.Z < 9f) continue;
                    var d = new Vector2(b.GlobalPosition.X, b.GlobalPosition.Z).Length();
                    if (d >= bd) continue;
                    var p = b.GlobalPosition + new Vector3(b.Size.X * 0.22f, 0, b.Size.Z * 0.22f);
                    var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(p + Vector3.Up * (b.Size.Y + 30f), p, 1));
                    if (hit.Count == 0 || ((Vector3)hit["normal"]).Y < 0.95f || ((Vector3)hit["position"]).Y < b.GlobalPosition.Y + b.Size.Y - 0.5f) continue;
                    bd = d;
                    best = b;
                    spot = (Vector3)hit["position"];
                }
                if (best == null) return;
                var c = best.GlobalPosition;
                w.LocalPlayer.GlobalPosition = spot + Vector3.Up * 1.5f;
                w.LocalPlayer.Velocity = Vector3.Zero;
                GD.Print($"[roof] soldat sur l'immeuble {best.Id} ({best.Size.X:0}×{best.Size.Z:0} m, {best.Size.Y:0} m de haut) en {c.X:0},{c.Z:0}");
                if (!Net.I.IsHost) return;
                for (var i = 0; i < n; i++)
                {
                    var a = i * Mathf.Tau / n;
                    var r = Mathf.Max(best.Size.X, best.Size.Z) / 2 + 12f + (i % 3) * 6f;
                    var type = i % 9 == 4 ? EnemyType.Spider : i % 11 == 7 ? EnemyType.Centipede : i % 5 == 2 ? EnemyType.RedAnt : EnemyType.BlackAnt;
                    w.HostSpawnEnemy(type, c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
            };
        }
        Try();
    }

    /// <summary>
    /// --grab[=client|ally] : une fourmi rouge sort de terre à ~9 m devant le soldat (bot immobile) et le saisit ; une nouvelle
    /// apparaît 4 s après la mort de la précédente. =client : devant le soldat du client (avec --autohost, vue de la victime
    /// côté client) ; =ally : devant un soldat allié (les insectes ignorent sinon les alliés pendant ce test).
    /// =bite : une fourmi noire à 7 m qui mord, caméra de profil (animation de morsure).
    /// Journal « [grab] » : début et fin de chaque saisie. Avec --god --holdfire, la saisie dure indéfiniment (captures).
    /// </summary>
    private static void GrabTest(App app)
    {
        if (Screenshot.CmdArg("grab") is not { } mode) return;
        Player.BotStill = true;
        Enemy.IgnoreAllies = mode != "ally";
        // =bite : une fourmi noire (morsures), vue de profil.
        var bite = mode == "bite";
        Enemy.NoCameraFade = bite;
        var type = bite ? EnemyType.BlackAnt : EnemyType.RedAnt;
        var nextAt = 0f;
        Enemy mine = null;
        void Tick()
        {
            app.GetTree().CreateTimer(0.5f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null || w.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) { Tick(); return; }
                if (!Net.I.IsHost || w.Elapsed < 3f) { Tick(); return; }
                if (mine != null && GodotObject.IsInstanceValid(mine) && mine.State != EnemyState.Dead) { nextAt = w.Elapsed + 4f; Tick(); return; }
                if (w.Elapsed < nextAt) { Tick(); return; }
                Node3D victim = w.LocalPlayer;
                var face = w.LocalPlayer.AimForward;
                if (mode == "client" && w.Remotes.Count > 0)
                {
                    var r = w.Remotes.Values.First();
                    victim = r;
                    face = new Vector3(Mathf.Sin(r.Model.Rotation.Y), 0, Mathf.Cos(r.Model.Rotation.Y));
                }
                else if (mode == "ally" && w.Allies.ById.Values.FirstOrDefault(a => !a.Dead) is { } al)
                {
                    victim = al;
                    face = new Vector3(Mathf.Sin(al.Model.Rotation.Y), 0, Mathf.Cos(al.Model.Rotation.Y));
                }
                face.Y = 0;
                face = face.LengthSquared() > 0.01f ? face.Normalized() : Vector3.Forward;
                var at = victim.GlobalPosition;
                var p = new Vector3(at.X, 0, at.Z) + face * (bite ? 7f : 9f);
                if (bite)
                {
                    // Caméra de profil, à mi-chemin entre la fourmi et le soldat.
                    var cam = new Camera3D { Fov = 50, Current = true };
                    w.AddChild(cam);
                    var mid = (p + new Vector3(at.X, 0, at.Z)) * 0.5f + Vector3.Up * 1.8f;
                    cam.GlobalPosition = mid + new Vector3(face.Z, 0, -face.X) * 15f + Vector3.Up * 1.2f;
                    cam.LookAt(mid, Vector3.Up);
                    cam.MakeCurrent();
                    // Ralenti (1/20) une fois la fourmi au contact : les captures décomposent la morsure.
                    app.GetTree().CreateTimer(3f).Timeout += () =>
                    {
                        if (mine != null && GodotObject.IsInstanceValid(mine)) GD.Print($"[grab] morsure : fourmi à {mine.GlobalPosition.DistanceTo(victim.GlobalPosition):0.0} m, état {mine.State}");
                        if (Screenshot.CmdArg("shots") != null) Engine.TimeScale = 0.05;
                    };
                }
                // =ally : le soldat (observateur) recule derrière l'allié, que la fourmi visera en premier.
                if (victim is Ally) w.LocalPlayer.GlobalPosition = new Vector3(at.X, 0.5f, at.Z) - face * 16f + new Vector3(face.Z, 0, -face.X) * 6f;
                var ant = mine = w.HostSpawnEnemy(type, p);
                ant.Rotation = new Vector3(0, Mathf.Atan2(at.X - p.X, at.Z - p.Z), 0);
                // --holdfire : personne ne doit l'abattre (saisie durable pour les captures).
                if (Player.BotHoldFire) ant.MaxHealth = ant.Health = 1e6f;
                GD.Print($"[grab] {ant.Def.Name.ToLowerInvariant()} {ant.Id} ({ant.Health:0} PV) apparue à 9 m de {victim.Name} (t = {Time.GetTicksMsec() / 1000f:0.0} s)");
                nextAt = w.Elapsed + 4f;
                Tick();
            };
        }
        Tick();
    }

    private static void ScheduleShots(App app, string dir, string prefix)
    {
        if (dir == null) return;
        var times = (Screenshot.CmdArg("shot-times") ?? "6,12").Split(',')
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 5f);
        foreach (var t in times)
            app.GetTree().CreateTimer(t, true, false, true).Timeout += () => Screenshot.Take(app.GetViewport(), prefix);
    }
}
