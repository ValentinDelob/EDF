using Godot;

namespace Rempart;

/// <summary>Guide du joueur : commandes, classes et règles du butin.</summary>
public partial class GuideMenu : MenuScreen
{
    private VBoxContainer _col;
    private ScrollContainer _scroll;

    public override void _Ready()
    {
        BuildFrame("Guide", 0.88f);
        _scroll = Scroll(980);
        UiKit.FillRect(_scroll);
        _scroll.AnchorRight = 0;
        _scroll.OffsetRight = 980;
        _col = UiKit.VBox(8);
        _col.CustomMinimumSize = new Vector2(940, 0);
        _scroll.AddChild(_col);
        Body.AddChild(_scroll);
        AddTab("Les bases", BuildBasics);
        AddTab("Classes", BuildClasses);
        AddTab("Butin et progression", BuildLoot);
        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", () => App.I.ShowMain()));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Onglets"));
        FooterLeft.AddChild(UiKit.KeyHint("↑ / ↓", "Défiler"));
        App.I.Background?.SetMode(MenuBackground.Mode.Title);
        SelectTab(0);
    }

    // Page de lecture : rien de focalisable, la croix directionnelle fait défiler le texte.
    protected override void FocusContent() => GetViewport()?.GuiReleaseFocus();

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { App.I.ShowMain(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) { GetViewport().SetInputAsHandled(); return; }
        var dy = e.IsActionPressed("ui_down", true) ? 1 : e.IsActionPressed("ui_up", true) ? -1 : 0;
        if (dy == 0) return;
        var tw = _scroll.CreateTween();
        tw.TweenProperty(_scroll, "scroll_vertical", _scroll.ScrollVertical + dy * 96, 0.12f).SetEase(Tween.EaseType.Out);
        GetViewport().SetInputAsHandled();
    }

    private void Clear()
    {
        foreach (var c in _col.GetChildren()) c.QueueFree();
        _scroll.ScrollVertical = 0;
    }

    private void Section(string title)
    {
        if (UiKit.HasLiveChildren(_col)) _col.AddChild(UiKit.Gap(18));
        _col.AddChild(UiKit.Eyebrow(title, UiKit.Accent));
    }

    private void Para(string text)
    {
        var l = UiKit.Label(text, 17, new Color(UiKit.Text, 0.88f));
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(920, 0);
        _col.AddChild(l);
    }

    private void Keys(params (string action, string label)[] items)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var (action, label) in items)
        {
            var k = action.StartsWith("=") ? action[1..] : UiKit.KeyName(action);
            grid.AddChild(UiKit.KeyHint(k, label));
        }
        _col.AddChild(grid);
        _col.AddChild(UiKit.Gap(4));
    }

    private void BuildBasics()
    {
        Clear();
        Section("Objectif");
        Para("Repoussez les vagues d'insectes géants et de machines qui envahissent la ville. Chaque ennemi abattu peut lâcher une caisse : ramassez-les en passant dessus pour gagner armes, armure et crédits. La mission est réussie quand l'objectif est rempli.");
        Section("Commandes");
        Keys(("move_forward", "Avancer"), ("move_back", "Reculer"), ("move_left", "Gauche"), ("move_right", "Droite"),
            ("jump", "Sauter / voler"), ("sprint", "Courir / propulsion"), ("dodge", "Roulade / esquive"), ("fire", "Tirer"),
            ("aim", "Viser"), ("reload", "Recharger"), ("weapon_swap", "Arme suivante"), ("ability", "Capacité de classe"),
            ("grenade", "Grenade"), ("interact", "Relever, monter dans un véhicule"), ("scoreboard", "Tableau des scores"), ("=Échap", "Pause"));
        Para("Toutes les touches et la sensibilité de la souris se règlent dans Options › Contrôles. Une manette est aussi reconnue.");
        Section("Radar");
        Para("Le radar (en haut à droite) tourne avec la caméra : le haut est toujours devant vous et le repère « N » indique le nord. Points rouges : insectes ; orange : ennemis volants ; grands points : boss et nids ; bleus : alliés et équipiers ; petits carrés : caisses, de la couleur de leur contenu ; losanges : marqueurs de l'escouade.");
        Section("Multijoueur en réseau local");
        Para("Un joueur choisit « Héberger (LAN) », les autres « Rejoindre (LAN) » : la partie apparaît automatiquement, sinon saisissez l'adresse IP de l'hôte. Un soldat à terre peut être relevé par un équipier qui maintient la touche d'interaction près de lui. Si toute l'escouade est à terre, la mission échoue.");
        Section("Véhicules et alliés");
        Para("Un char de combat attend près du point de départ : approchez-vous et appuyez sur la touche d'interaction. Un hélicoptère d'attaque est garé de l'autre côté de la place : saut pour monter, course ou roulade pour descendre, avancer/reculer pour piquer ou cabrer, la caméra oriente le nez ; minigun au clic gauche, salves de roquettes au clic droit, posez-vous pour en sortir. Des escouades de soldats alliés vous suivent et tirent sur les insectes.");
    }

    private void BuildClasses()
    {
        Clear();
        foreach (var c in Classes.All)
        {
            Section(c.Name);
            Para($"{c.Role} · armure de départ {c.BaseArmor:0} PV · {c.WeaponSlots} armes.");
            Para($"{c.AbilityName} : {c.AbilityDescription}");
            var extra = c.Id switch
            {
                SoldierClass.Rifleman => "Polyvalent, idéal pour débuter. Relève ses équipiers deux fois plus vite.",
                SoldierClass.Airborne => "Maintenez la touche de saut pour voler : le vol et les rechargements consomment la même énergie. À zéro, tout est bloqué jusqu'à la recharge complète.",
                SoldierClass.Support => "Infliger des dégâts remplit la jauge d'appui ; pleine, la capacité déclenche un barrage d'artillerie. Balises de frappe, tourelles et champs de soin se déploient avec le tir.",
                _ => "Lent, mais ses poussées (course ou esquive) et son saut assisté le déplacent vite. Chaque poussée chauffe l'exosquelette : attention à la surchauffe.",
            };
            Para(extra);
        }
    }

    private void BuildLoot()
    {
        Clear();
        Section("Caisses");
        Para("Rouge : arme · vert : armure (points de vie maximum permanents) · rose : soin immédiat · jaune : crédits. Les caisses proches sont attirées vers vous. Attention aux charognards : ils avalent les caisses au sol, tuez-les pour les récupérer.");
        Section("Armes");
        Para($"Chaque arme a un niveau (0 à 100) et, pour chaque caractéristique (dégâts, cadence, chargeur, rechargement, précision, portée), de 0 à {WeaponInstance.MaxStars} étoiles : chaque étoile ajoute un bonus. Plus la difficulté et le niveau sont élevés, plus les armes trouvées ont d'étoiles.");
        Section("Progression");
        Para("L'armure ramassée s'ajoute définitivement à la classe jouée. L'expérience fait monter la classe de niveau : +1 % de dégâts et +3 PV par niveau.");
        Section("Échec");
        Para("En cas d'échec, les armes ramassées sont conservées et l'armure est réduite de moitié ; les crédits sont perdus.");
    }
}
