using Godot;

namespace Rempart;

/// <summary>Guide du joueur : commandes, classes et règles du butin.</summary>
public partial class GuideMenu : MenuScreen
{
    private VBoxContainer _col;

    public override void _Ready()
    {
        BuildFrame("Guide", 0.88f);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(980, 0) };
        UiKit.FillRect(scroll);
        scroll.AnchorRight = 0;
        scroll.OffsetRight = 980;
        _col = UiKit.VBox(8);
        _col.CustomMinimumSize = new Vector2(940, 0);
        scroll.AddChild(_col);
        Body.AddChild(scroll);
        AddTab("Les bases", BuildBasics);
        AddTab("Classes", BuildClasses);
        AddTab("Butin et progression", BuildLoot);
        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", () => App.I.ShowMain()));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Onglets"));
        App.I.Background?.SetMode(MenuBackground.Mode.Title);
        SelectTab(0);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { App.I.ShowMain(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) GetViewport().SetInputAsHandled();
    }

    private void Clear()
    {
        foreach (var c in _col.GetChildren()) c.QueueFree();
    }

    private void Section(string title)
    {
        if (_col.GetChildCount() > 0) _col.AddChild(UiKit.Gap(18));
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
            ("grenade", "Grenade"), ("interact", "Relever, monter dans le char"), ("scoreboard", "Tableau des scores"), ("=Échap", "Pause"));
        Para("Toutes les touches et la sensibilité de la souris se règlent dans Options › Contrôles. Une manette est aussi reconnue.");
        Section("Multijoueur en réseau local");
        Para("Un joueur choisit « Héberger (LAN) », les autres « Rejoindre (LAN) » : la partie apparaît automatiquement, sinon saisissez l'adresse IP de l'hôte. Un soldat à terre peut être relevé par un équipier qui maintient la touche d'interaction près de lui. Si toute l'escouade est à terre, la mission échoue.");
        Section("Véhicules et alliés");
        Para("Un char de combat attend près du point de départ : approchez-vous et appuyez sur la touche d'interaction. Des escouades de soldats alliés vous suivent et tirent sur les insectes.");
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
        Para("Chaque arme a un niveau (0 à 100), une rareté (Standard, Renforcée, Élite, Prototype, Légendaire) et des étoiles par statistique. Un doublon ajoute une étoile à l'arme déjà possédée. Plus la difficulté est élevée, plus les armes trouvées sont puissantes.");
        Section("Progression");
        Para("L'armure ramassée s'ajoute définitivement à la classe jouée. L'expérience fait monter la classe de niveau : +1 % de dégâts et +3 PV par niveau. Les crédits servent à améliorer les armes dans l'arsenal.");
        Section("Échec");
        Para("En cas d'échec, les armes ramassées sont conservées et l'armure est réduite de moitié ; les crédits sont perdus.");
    }
}
