namespace Rempart;

public enum MissionGoal { Exterminate, DestroyNests, Boss }

public enum TimeOfDay { Day, Dusk, Night }

/// <summary>Un groupe d'ennemis qui arrive d'une zone d'apparition.</summary>
public record WaveDef(string Zone, float Delay, string Message, params (EnemyType type, int count)[] Groups);

/// <summary>
/// Une phase se déclenche quand la précédente est réduite à <see cref="TriggerRemaining"/> ennemis
/// (ou à 0), ou après <see cref="TriggerTime"/> secondes si renseigné.
/// </summary>
public record PhaseDef(int TriggerRemaining, float TriggerTime, params WaveDef[] Waves);

public record MissionDef(
    int Index,
    string Name,
    string Briefing,
    MissionGoal Goal,
    TimeOfDay Time,
    float Fog,
    int NestCount,
    PhaseDef[] Phases,
    Weather Weather = Weather.Clear,
    MapId Map = MapId.City);

public enum MapId { City, Port, Suburb, Business }

public enum Weather { Clear, Rain, Storm }

public static class Missions
{
    public static readonly MissionDef[] All =
    {
        new(0, "Premier contact",
            "Des fourmis géantes ont surgi dans le quartier du port. Éliminez toutes les vagues et récupérez le matériel qu'elles laissent derrière elles.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.002f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contact au nord ! Des fourmis géantes approchent.", (EnemyType.BlackAnt, 18))),
                new PhaseDef(5, 0,
                    new WaveDef("east", 0, "Deuxième vague à l'est, avec des fourmis rouges blindées.", (EnemyType.BlackAnt, 16), (EnemyType.RedAnt, 6)),
                    new WaveDef("west", 6, null, (EnemyType.BlackAnt, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("south", 0, "Fourmis acides au sud ! Gardez vos distances.", (EnemyType.AcidAnt, 12), (EnemyType.BlackAnt, 14)),
                    new WaveDef("north", 8, "Des charognards ! Ils dévorent les caisses au sol.", (EnemyType.Scavenger, 6), (EnemyType.RedAnt, 6))),
            }),
        new(1, "Nids sous la ville",
            "Les fourmis ont creusé des nids dans le centre-ville. Détruisez-les tous avant que la colonie ne submerge le quartier.",
            MissionGoal.DestroyNests, TimeOfDay.Dusk, 0.006f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Nids repérés. Détruisez-les aux explosifs !", (EnemyType.BlackAnt, 12), (EnemyType.RedAnt, 4))),
                new PhaseDef(0, 70,
                    new WaveDef("east", 0, "Essaim de guêpes en approche par l'est !", (EnemyType.Wasp, 14))),
                new PhaseDef(0, 140,
                    new WaveDef("west", 0, "Renforts ennemis à l'ouest, avec des mille-pattes géants !", (EnemyType.AcidAnt, 10), (EnemyType.RedAnt, 8), (EnemyType.Scavenger, 4), (EnemyType.Centipede, 2))),
            }, Weather.Rain),
        new(2, "La reine",
            "Une reine fourmi a été localisée sur la plage. Abattez-la : toute la colonie défendra sa mère.",
            MissionGoal.Boss, TimeOfDay.Night, 0.012f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("beach", 1, "Cible prioritaire : la reine. Abattez-la à tout prix !", (EnemyType.Queen, 1), (EnemyType.RedAnt, 8)),
                    new WaveDef("north", 10, null, (EnemyType.BlackAnt, 16))),
                new PhaseDef(0, 50,
                    new WaveDef("east", 0, "Essaim de guêpes !", (EnemyType.Wasp, 12), (EnemyType.AcidAnt, 8))),
                new PhaseDef(0, 110,
                    new WaveDef("west", 0, "La colonie envoie tout ce qu'elle a !", (EnemyType.BlackAnt, 20), (EnemyType.RedAnt, 10), (EnemyType.Scavenger, 6))),
                new PhaseDef(0, 180,
                    new WaveDef("south", 0, "Nouvel essaim au sud !", (EnemyType.Wasp, 16), (EnemyType.AcidAnt, 10))),
            }, Weather.Storm),
        new(3, "La toile",
            "Des araignées géantes ont tissé leurs toiles entre les immeubles. Elles bondissent sur leurs proies : gardez vos distances et couvrez-vous mutuellement.",
            MissionGoal.Exterminate, TimeOfDay.Dusk, 0.008f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Mouvements suspects au nord… des araignées !", (EnemyType.Spider, 12))),
                new PhaseDef(6, 0,
                    new WaveDef("east", 0, "Elles arrivent par l'est, avec des fourmis !", (EnemyType.Spider, 10), (EnemyType.BlackAnt, 14)),
                    new WaveDef("west", 6, null, (EnemyType.Spider, 8))),
                new PhaseDef(6, 0,
                    new WaveDef("south", 0, "Dernière vague : tout le nid se réveille !", (EnemyType.Spider, 16), (EnemyType.RedAnt, 8), (EnemyType.Scavenger, 4), (EnemyType.Centipede, 3))),
            }, Weather.Clear, MapId.Port),
        new(4, "Ciel noir",
            "Une flotte de drones extraterrestres escorte des essaims de guêpes au-dessus du quartier d'affaires. Abattez tout ce qui vole.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.003f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contacts aériens ! Des drones d'attaque !", (EnemyType.Drone, 12))),
                new PhaseDef(5, 0,
                    new WaveDef("east", 0, "Guêpes et drones à l'est !", (EnemyType.Drone, 10), (EnemyType.Wasp, 12)),
                    new WaveDef("south", 5, "Fourmis acides au sol !", (EnemyType.AcidAnt, 12))),
                new PhaseDef(6, 0,
                    new WaveDef("west", 0, "Escadrille lourde à l'ouest, tenez bon !", (EnemyType.Drone, 18), (EnemyType.Wasp, 14), (EnemyType.RedAnt, 10))),
            }),
        new(5, "Les docks",
            "La colonie s'est installée dans le port de commerce. Détruisez les nids entre les conteneurs, sous la pluie, avant l'arrivée des renforts.",
            MissionGoal.DestroyNests, TimeOfDay.Night, 0.008f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Nids repérés dans le parc à conteneurs. Visez-les aux explosifs !", (EnemyType.RedAnt, 10), (EnemyType.AcidAnt, 6))),
                new PhaseDef(0, 60,
                    new WaveDef("east", 0, "Araignées sur les cuves à l'est !", (EnemyType.Spider, 12))),
                new PhaseDef(0, 120,
                    new WaveDef("west", 0, "Charognards et guêpes à l'ouest !", (EnemyType.Scavenger, 8), (EnemyType.Wasp, 12))),
            }, Weather.Rain, MapId.Port),
        new(6, "La forteresse",
            "Une forteresse marcheuse extraterrestre descend sur la ville par le nord, escortée de fourmis rouges. Elle écrase tout sur son passage. Visez le noyau lumineux à l'avant de la coque et fuyez la zone marquée en rouge quand son canon principal se charge.",
            MissionGoal.Boss, TimeOfDay.Dusk, 0.007f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contact colossal au nord : une forteresse marcheuse ! Visez son noyau !", (EnemyType.Fortress, 1), (EnemyType.RedAnt, 10))),
                new PhaseDef(0, 60,
                    new WaveDef("east", 0, "Escadrille de drones à l'est !", (EnemyType.Drone, 10))),
                new PhaseDef(0, 120,
                    new WaveDef("west", 0, "Nouvelle vague de drones à l'ouest, avec des fourmis !", (EnemyType.Drone, 12), (EnemyType.RedAnt, 8))),
                new PhaseDef(0, 180,
                    new WaveDef("south", 0, "Des araignées au sud ! Elles nous prennent à revers !", (EnemyType.Spider, 14))),
            }, Weather.Storm),
        new(7, "Lisière",
            "La colonie sort de la forêt et se répand dans un paisible lotissement de banlieue, bordé au sud par une rivière. Tenez le bourg et exterminez toutes les vagues avant qu'elles n'atteignent la place de l'église.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.005f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Mouvements à la lisière nord ! Des fourmis sortent du bois.", (EnemyType.BlackAnt, 18)),
                    new WaveDef("west", 9, "D'autres fourmis à l'ouest, elles longent la forêt !", (EnemyType.BlackAnt, 12))),
                new PhaseDef(6, 0,
                    new WaveDef("west", 0, "Des araignées descendent des arbres à l'ouest !", (EnemyType.Spider, 10), (EnemyType.BlackAnt, 8)),
                    new WaveDef("north", 6, "Fourmis acides au nord, gardez vos distances !", (EnemyType.AcidAnt, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("east", 0, "Essaim de guêpes au-dessus des toits, à l'est !", (EnemyType.Wasp, 14)),
                    new WaveDef("south", 6, "Elles traversent la rivière au sud !", (EnemyType.BlackAnt, 12), (EnemyType.Scavenger, 5))),
                new PhaseDef(5, 0,
                    new WaveDef("north", 0, "Dernière vague : fourmis rouges en masse depuis la forêt, des spécimens d'élite repérés !", (EnemyType.RedAnt, 14), (EnemyType.BlackAnt, 10)),
                    new WaveDef("west", 5, null, (EnemyType.RedAnt, 8), (EnemyType.Spider, 6))),
            }, Weather.Clear, MapId.Suburb),
        new(8, "Tours de verre",
            "Le quartier d'affaires est tombé. Des guêpes nichent entre les tours et les fourmis escaladent les façades pour déloger nos tireurs des toits. Tenez les hauteurs : le parking à étages, les escaliers de secours et le viaduc de l'autoroute dominent le parvis.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.0025f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Fourmis sur l'avenue nord ! Elles remontent vers le parvis.", (EnemyType.BlackAnt, 18)),
                    new WaveDef("east", 8, "Guêpes entre les tours à l'est !", (EnemyType.Wasp, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("west", 0, "Fourmis rouges à l'ouest, elles grimpent aux façades !", (EnemyType.RedAnt, 12), (EnemyType.BlackAnt, 10)),
                    new WaveDef("north", 6, "Nouvel essaim au-dessus des toits !", (EnemyType.Wasp, 14))),
                new PhaseDef(6, 0,
                    new WaveDef("south", 0, "Fourmis acides sous le viaduc, au sud !", (EnemyType.AcidAnt, 12), (EnemyType.BlackAnt, 10)),
                    new WaveDef("beach", 6, "Araignées sur l'autoroute ! Elles sautent de pile en pile.", (EnemyType.Spider, 8))),
                new PhaseDef(5, 0,
                    new WaveDef("east", 0, "Dernière vague : guêpes et fourmis rouges en masse, ne lâchez pas les toits !", (EnemyType.Wasp, 18), (EnemyType.RedAnt, 10)),
                    new WaveDef("west", 6, null, (EnemyType.BlackAnt, 14), (EnemyType.Scavenger, 5))),
            }, Weather.Clear, MapId.Business),
        new(9, "Panne générale",
            "Le réseau électrique a lâché : le quartier d'affaires est plongé dans le noir, sous la pluie. La colonie a creusé ses nids au pied des tours. Détruisez-les à la lueur des incendies et méfiez-vous des araignées qui guettent depuis les façades.",
            MissionGoal.DestroyNests, TimeOfDay.Night, 0.01f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Nids repérés au pied des tours. Visez-les aux explosifs !", (EnemyType.BlackAnt, 14), (EnemyType.RedAnt, 6))),
                new PhaseDef(0, 60,
                    new WaveDef("east", 0, "Araignées sur les façades à l'est !", (EnemyType.Spider, 12))),
                new PhaseDef(0, 120,
                    new WaveDef("north", 0, "Essaim de guêpes sur l'avenue nord !", (EnemyType.Wasp, 14), (EnemyType.AcidAnt, 8))),
                new PhaseDef(0, 180,
                    new WaveDef("west", 0, "Mille-pattes et charognards à l'ouest, ils sortent du métro !", (EnemyType.Centipede, 2), (EnemyType.Scavenger, 6), (EnemyType.RedAnt, 8))),
            }, Weather.Rain, MapId.Business),
    };
}
