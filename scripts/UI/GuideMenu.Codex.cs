using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Encyclopédie du Guide : bestiaire (ennemis abattus au moins une fois), armes obtenues par classe
/// et opérations terminées par difficulté.
/// </summary>
public partial class GuideMenu
{
    /// <summary>Ouvre un onglet (captures automatiques).</summary>
    public void ShowTab(int i) => SelectTab(i);

    private static string EnemyNotes(EnemyType t) => t switch
    {
        EnemyType.BlackAnt => "Fonce en meute et mord. Fragile, mais toujours en nombre ; escalade les façades.",
        EnemyType.RedAnt => "Plus massive et résistante. Saisit sa victime dans ses mandibules : tuez-la pour libérer le soldat.",
        EnemyType.AcidAnt => "Garde ses distances et crache de l'acide à une trentaine de mètres.",
        EnemyType.Wasp => "Essaim volant qui harcèle à distance avec ses dards. Les armes à tir rapide en viennent à bout.",
        EnemyType.Scavenger => "Rapide, il fonce sur les caisses au sol et les avale. L'abattre rend toutes les caisses avalées, plus une.",
        EnemyType.Nest => "Structure organique qui génère des insectes en continu. Les explosifs la détruisent vite.",
        EnemyType.Queen => "Reine colossale : pond des fourmis et projette de l'acide en arc de cercle.",
        EnemyType.Spider => "Bondit sur sa proie et tisse des fils. Redoutable au contact ; le fusil à pompe la calme.",
        EnemyType.Drone => "Machine volante en formation, tirs laser. Vulnérable aux missiles.",
        EnemyType.Fortress => "Quadrupède géant : tourelles laser, barrages de missiles et canon principal à charge. Visez le noyau.",
        EnemyType.Centipede => "Corps de douze segments qui serpente ; chaque segment encaisse séparément. Morsure et crachat d'acide.",
        EnemyType.Walker => "Robot bipède de quinze mètres. Garde ses distances et pilonne au canon lourd : la ligne rouge annonce le tir. Visez les genoux.",
        EnemyType.Tripod => "Marcheur à trois pattes. Son rayon balaie le sol et rase les immeubles ; le tracé s'allume avant le tir. Visez le noyau lumineux sous la coque.",
        EnemyType.DropShip => "Vaisseau de largage qui dépose des fourmis par sa soute. Invulnérable soute fermée : tirez dans la soute quand elle s'ouvre.",
        EnemyType.Titan => "Reptile colossal de soixante mètres qui traverse les immeubles. Souffle de feu annoncé par sa gorge qui s'embrase, coup de queue contre qui le prend à revers. Brisez ses plaques d'armure et visez la chair à vif.",
        EnemyType.AlienTrooper => "Humanoïde de cinq mètres armé d'un fusil à énergie. Se met à couvert derrière les immeubles et les véhicules, se relève pour tirer en rafales, puis replonge. Débusquez-le aux explosifs ou prenez-le à revers ; la tête encaisse double.",
        EnemyType.AlienGuard => "Colosse cuirassé derrière un bouclier de tour qui arrête les balles. Charge et frappe au bouclier, qu'il abaisse un instant après la charge. Contournez-le : la cellule d'énergie dans son dos est son point faible.",
        EnemyType.AlienFlyer => "Alien à réacteurs dorsaux. Tourne au-dessus des toits, passe en rase-mottes pour mitrailler au laser. Fragile : abattez-le pendant ses passes.",
        EnemyType.Portal => "Anneau extraterrestre qui fait passer des escouades d'aliens et des machines tant qu'il tient. Les balles l'égratignent à peine : employez les explosifs.",
        EnemyType.Mothership => "Vaisseau de commandement de plusieurs centaines de mètres, en vol stationnaire au-dessus de la ville. Ses soutes lâchent drones, volants et vaisseaux de largage ; son canon principal dévaste une zone entière, annoncée par un disque au sol. Sa coque est invulnérable : visez le cœur et les trappes quand le canon s'ouvre, et abattez ses tourelles.",
        _ => "Données en cours d'analyse.",
    };

    private void BuildBestiary()
    {
        Clear();
        var seen = Profile.I.Data.Bestiary;
        var met = Enemies.All.Count(e => seen.ContainsKey(e.Type.ToString()));
        Section($"Bestiaire — {met}/{Enemies.All.Length} espèces identifiées");
        Para("Un ennemi apparaît ici dès que votre escouade en a abattu un. Points de vie indiqués en difficulté Normal, pour un joueur.");
        foreach (var e in Enemies.All)
        {
            if (!seen.TryGetValue(e.Type.ToString(), out var kills))
            {
                _col.AddChild(UiKit.Gap(10));
                _col.AddChild(UiE.Title("? ? ?", 20, UiE.TextDim, UiE.CondSemi));
                Para("Espèce non identifiée.");
                continue;
            }
            _col.AddChild(UiKit.Gap(10));
            _col.AddChild(UiE.Title(e.Name.ToUpperInvariant(), 20, UiE.Mint, UiE.CondSemi));
            var kind = e.Boss ? "Boss" : e.Static ? "Structure" : e.Type is EnemyType.AlienTrooper or EnemyType.AlienGuard or EnemyType.AlienFlyer ? "Alien" : e.Flying ? "Volant" : e.Type is EnemyType.Drone or EnemyType.Fortress or EnemyType.Walker or EnemyType.Tripod or EnemyType.DropShip ? "Machine" : "Insecte";
            Para($"{kind} · {e.Health:0} PV · {(e.Ranged ? "attaque à distance" : "corps à corps")} · {kills} abattu{(kills > 1 ? "s" : "")}");
            Para(EnemyNotes(e.Type));
        }
    }

    private void BuildWeapons()
    {
        Clear();
        var owned = Profile.I.Data.Arsenal.Select(w => w.TemplateId).ToHashSet();
        Section($"Armes — {Weapons.All.Count(t => owned.Contains(t.Id))}/{Weapons.All.Length} obtenues");
        foreach (var c in Classes.All)
        {
            var list = Weapons.ForClass(c.Id).ToList();
            _col.AddChild(UiKit.Gap(10));
            _col.AddChild(UiE.Title($"{c.Name.ToUpperInvariant()}  ·  {list.Count(t => owned.Contains(t.Id))}/{list.Count}", 20, UiE.Mint, UiE.CondSemi));
            foreach (var g in list.GroupBy(t => t.Category))
                Para($"{g.Key} : " + string.Join(", ", g.Select(t => owned.Contains(t.Id) ? t.Name : "? ? ?")));
        }
    }

    private void BuildOperations()
    {
        Clear();
        Section($"Opérations — {MissionProgress.ClearedCount}/{Missions.All.Length} terminées");
        Para("Une coche par difficulté réussie. Les opérations se débloquent l'une après l'autre ; seules les opérations débloquées sont listées.");
        var grid = new GridContainer { Columns = 1 + Difficulties.Names.Length };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 6);
        grid.AddChild(UiE.Label("", 16, UiE.TextDim));
        foreach (var d in Difficulties.Names) grid.AddChild(UiE.Label(d.ToUpperInvariant(), 16, UiE.TextDim));
        foreach (var m in Missions.All.Where(m => MissionProgress.Unlocked(m.Index)))
        {
            var name = UiE.Label(MissionProgress.Label(m), 17, UiE.Text);
            name.CustomMinimumSize = new Vector2(380, 0);
            grid.AddChild(name);
            for (var d = 0; d < Difficulties.Names.Length; d++)
            {
                var done = Profile.I.IsCleared(m.Index, (Difficulty)d);
                var l = UiE.Label(done ? "✓" : "·", 18, done ? UiE.Lime : UiE.TextDim);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                grid.AddChild(l);
            }
        }
        _col.AddChild(grid);
    }
}
