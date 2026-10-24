namespace Rempart;

/// <summary>Type d'objectif (Defense, Escort, Survival : voir <see cref="ObjectiveDef"/> et <see cref="MissionObjective"/>).</summary>
public enum MissionGoal { Exterminate, DestroyNests, Boss, Defense, Escort, Survival }

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
    MapId Map = MapId.City,
    ObjectiveDef Objective = null,
    EnemyType NestType = EnemyType.Nest);

public enum MapId { City, Port, Suburb, Business, Industrial, Desert, Capital }

public enum Weather { Clear, Rain, Storm, Sandstorm }

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
        new(8, "Le relais",
            "Une antenne relais de campagne a été dressée sur la place du port pour coordonner l'évacuation par la mer. La colonie l'a repérée et va se jeter dessus. Tenez la place et protégez l'antenne jusqu'à la dernière vague : si elle tombe, la mission échoue.",
            MissionGoal.Defense, TimeOfDay.Dusk, 0.007f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 3, "Contacts au nord, ils foncent droit sur l'antenne !", (EnemyType.BlackAnt, 16))),
                new PhaseDef(6, 0,
                    new WaveDef("east", 0, "Fourmis rouges à l'est, couvrez le relais !", (EnemyType.RedAnt, 6), (EnemyType.BlackAnt, 12)),
                    new WaveDef("west", 6, "Fourmis acides à l'ouest, elles visent l'antenne de loin !", (EnemyType.AcidAnt, 8))),
                new PhaseDef(6, 0,
                    new WaveDef("south", 0, "Elles remontent des quais !", (EnemyType.BlackAnt, 14), (EnemyType.Scavenger, 4)),
                    new WaveDef("north", 8, "Guêpes au-dessus des conteneurs !", (EnemyType.Wasp, 12))),
                new PhaseDef(5, 0,
                    new WaveDef("north", 0, "Dernière vague : toute la colonie converge sur l'antenne !", (EnemyType.RedAnt, 10), (EnemyType.BlackAnt, 14)),
                    new WaveDef("east", 4, null, (EnemyType.AcidAnt, 8)),
                    new WaveDef("west", 8, "Des araignées à l'ouest !", (EnemyType.Spider, 8))),
            }, Weather.Rain, MapId.Port, new ObjectiveDef(StructureKind.RelayAntenna, 0f, -18f, 16000f)),
        new(9, "Convoi",
            "Trois camions de ravitaillement doivent traverser le centre-ville jusqu'au point de regroupement, au nord-est. Restez au contact : sans soldat à moins de 40 m, les chauffeurs s'arrêtent, et un insecte en travers de la route bloque tout le convoi. Si les trois camions sont détruits, la mission échoue.",
            MissionGoal.Escort, TimeOfDay.Day, 0.004f, 0,
            new[]
            {
                // Zones « ahead » / « behind » : devant ou derrière le convoi, le long de son trajet.
                new PhaseDef(0, 0,
                    new WaveDef("ahead", 8, "Fourmis devant le convoi ! Dégagez la route !", (EnemyType.BlackAnt, 12))),
                new PhaseDef(0, 40,
                    new WaveDef("ahead", 0, "Fourmis rouges sur l'itinéraire !", (EnemyType.RedAnt, 6), (EnemyType.BlackAnt, 8)),
                    new WaveDef("behind", 6, "Ça vient aussi par l'arrière !", (EnemyType.BlackAnt, 8))),
                new PhaseDef(0, 80,
                    new WaveDef("ahead", 0, "Fourmis acides en embuscade, protégez les camions !", (EnemyType.AcidAnt, 10)),
                    new WaveDef("ahead", 6, "Guêpes au-dessus des toits !", (EnemyType.Wasp, 10))),
                new PhaseDef(0, 125,
                    new WaveDef("ahead", 0, "Charognards et fourmis devant !", (EnemyType.BlackAnt, 14), (EnemyType.Scavenger, 4))),
                new PhaseDef(0, 170,
                    new WaveDef("ahead", 0, "Araignées sur les façades !", (EnemyType.Spider, 8), (EnemyType.BlackAnt, 8)),
                    new WaveDef("behind", 6, null, (EnemyType.BlackAnt, 10))),
                new PhaseDef(0, 220,
                    new WaveDef("ahead", 0, "Dernier barrage avant le point de regroupement !", (EnemyType.RedAnt, 10), (EnemyType.AcidAnt, 8)),
                    new WaveDef("ahead", 8, null, (EnemyType.Wasp, 10))),
            }, Weather.Clear, MapId.City,
            new ObjectiveDef(Route: new[] { -40f, 30f, 30f, 30f, 30f, -70f, -120f, -70f, -120f, -170f, 130f, -170f }, TruckSpeed: 4.5f)),
        new(10, "Dernier carré",
            "Le bourg est encerclé et l'évacuation des habitants n'est pas terminée. Tenez dix minutes face à des vagues sans fin, de plus en plus nombreuses, jusqu'au retour des hélicoptères de transport.",
            MissionGoal.Survival, TimeOfDay.Night, 0.01f, 0,
            new[]
            {
                // Survie : ces vagues tournent en boucle (une toutes les WaveInterval secondes), de plus en plus fournies.
                new PhaseDef(0, 0, new WaveDef("north", 3, "Les voilà, à la lisière nord !", (EnemyType.BlackAnt, 12))),
                new PhaseDef(0, 0, new WaveDef("west", 0, "Araignées dans les jardins à l'ouest !", (EnemyType.Spider, 5), (EnemyType.BlackAnt, 8))),
                new PhaseDef(0, 0, new WaveDef("east", 0, "Guêpes au-dessus des toits !", (EnemyType.Wasp, 10))),
                new PhaseDef(0, 0, new WaveDef("south", 0, "Elles traversent la rivière !", (EnemyType.BlackAnt, 10), (EnemyType.AcidAnt, 6))),
                new PhaseDef(0, 0, new WaveDef("north", 0, "Fourmis rouges en masse au nord !", (EnemyType.RedAnt, 8), (EnemyType.Scavenger, 3))),
                new PhaseDef(0, 0, new WaveDef("west", 0, "Mille-pattes à l'ouest !", (EnemyType.Centipede, 1), (EnemyType.BlackAnt, 10))),
            }, Weather.Storm, MapId.Suburb, new ObjectiveDef(SurviveSeconds: 600f, WaveInterval: 30f, WaveGrowth: 0.12f)),
        new(11, "Tours de verre",
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
        new(12, "Panne générale",
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
        // Machines.
        new(13, "Acier",
            "Des marcheurs de combat extraterrestres avancent dans le centre-ville sous escorte de drones. Ils restent à distance et pilonnent au canon lourd : quittez la zone marquée en rouge dès qu'un canon se charge. Leur blindage est épais, mais les articulations des genoux cèdent vite.",
            MissionGoal.Exterminate, TimeOfDay.Dusk, 0.006f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contact au nord : deux marcheurs ! Visez les genoux.", (EnemyType.Walker, 2), (EnemyType.Drone, 8))),
                new PhaseDef(3, 0,
                    new WaveDef("east", 0, "D'autres marcheurs à l'est, avec des fourmis rouges !", (EnemyType.Walker, 2), (EnemyType.RedAnt, 10)),
                    new WaveDef("west", 7, "Escadrille de drones à l'ouest !", (EnemyType.Drone, 10))),
                new PhaseDef(4, 0,
                    new WaveDef("south", 0, "Une colonne blindée remonte de la plage. Tenez la ligne !", (EnemyType.Walker, 3), (EnemyType.Drone, 12)),
                    new WaveDef("north", 8, null, (EnemyType.AcidAnt, 10))),
            }, Weather.Rain),
        new(14, "Les tripodes",
            "Des tripodes géants sortent de la forêt et réduisent le bourg en cendres avec leur rayon. Leur point faible est le noyau incandescent sous la coque. Des vaisseaux de largage déposent des fourmis : ils ne craignent rien tant que leur soute est fermée, visez l'intérieur quand elle s'ouvre.",
            MissionGoal.Exterminate, TimeOfDay.Dusk, 0.008f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Un tripode à la lisière nord ! Visez le noyau sous la coque.", (EnemyType.Tripod, 1), (EnemyType.BlackAnt, 12))),
                new PhaseDef(0, 45,
                    new WaveDef("east", 0, "Vaisseau de largage en approche par l'est !", (EnemyType.DropShip, 1))),
                new PhaseDef(6, 0,
                    new WaveDef("west", 0, "Deux tripodes à l'ouest, et un second vaisseau !", (EnemyType.Tripod, 2), (EnemyType.DropShip, 1), (EnemyType.RedAnt, 8))),
                new PhaseDef(5, 0,
                    new WaveDef("south", 0, "Ils traversent la rivière avec des marcheurs. Dernier effort !", (EnemyType.Tripod, 1), (EnemyType.Walker, 2), (EnemyType.BlackAnt, 14))),
            }, Weather.Storm, MapId.Suburb),
        // Zone industrielle.
        new(15, "La raffinerie",
            "La colonie a envahi la zone industrielle et grouille dans le parc de cuves de la raffinerie. Exterminez toutes les vagues. Les citernes sont encore pleines : une cuve percée prend feu puis explose, et emporte tout ce qui l'entoure, insectes comme soldats. Servez-vous-en, mais jamais à côté.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.003f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Fourmis dans le parc de cuves au nord ! Attention aux citernes : une balle perdue et tout saute.", (EnemyType.BlackAnt, 18)),
                    new WaveDef("east", 8, "Guêpes au-dessus de la gare de triage !", (EnemyType.Wasp, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("center", 0, "Fourmis rouges au pied des cuves ! Faites-les sauter avec, s'il le faut.", (EnemyType.RedAnt, 12), (EnemyType.BlackAnt, 10)),
                    new WaveDef("west", 6, "Fourmis acides dans la cour des entrepôts, à l'ouest !", (EnemyType.AcidAnt, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("south", 0, "Elles passent le canal par les ponts, au sud !", (EnemyType.BlackAnt, 14), (EnemyType.Spider, 8)),
                    new WaveDef("north", 6, "Nouvel essaim au-dessus de la raffinerie !", (EnemyType.Wasp, 14))),
                new PhaseDef(5, 0,
                    new WaveDef("east", 0, "Dernière vague : la colonie remonte les voies ferrées, avec des mille-pattes !", (EnemyType.RedAnt, 12), (EnemyType.Centipede, 2), (EnemyType.BlackAnt, 12)),
                    new WaveDef("center", 6, null, (EnemyType.Scavenger, 5), (EnemyType.AcidAnt, 8))),
            }, Weather.Clear, MapId.Industrial),
        new(16, "Nuit de soufre",
            "L'orage s'abat sur la zone industrielle et la colonie en profite pour creuser ses nids au cœur du site : cour des entrepôts, gare de triage, station d'épuration, route nord. Détruisez-les tous. Les cuves en feu éclairent le terrain, mais la moindre étincelle peut embraser le parc de stockage. Un marcheur de combat a été signalé au sud.",
            MissionGoal.DestroyNests, TimeOfDay.Dusk, 0.009f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Nids repérés dans les cours du site. Visez-les aux explosifs !", (EnemyType.BlackAnt, 14), (EnemyType.RedAnt, 6))),
                new PhaseDef(0, 60,
                    new WaveDef("east", 0, "Araignées sur les wagons, à l'est !", (EnemyType.Spider, 12))),
                new PhaseDef(0, 120,
                    new WaveDef("north", 0, "Essaim de guêpes sur la raffinerie, gare aux sphères de gaz !", (EnemyType.Wasp, 14), (EnemyType.AcidAnt, 8))),
                new PhaseDef(0, 180,
                    new WaveDef("beach", 0, "Un marcheur de combat sur l'autre rive du canal ! Ses obus font sauter les cuves, visez les genoux.", (EnemyType.Walker, 1), (EnemyType.Scavenger, 6)),
                    new WaveDef("west", 8, null, (EnemyType.RedAnt, 10), (EnemyType.Centipede, 1))),
            }, Weather.Storm, MapId.Industrial),
        // Base militaire du désert.
        new(17, "Fort de sable",
            "La base aérienne du désert est tombée, mais son poste de commandement tient encore sur le tarmac, entre les abris bétonnés et la tour de contrôle : il relaie les frappes de toute la région. Protégez-le jusqu'à la dernière vague. Le parc est plein : trois chars et deux hélicoptères attendent un équipage devant le parc automobile et sur les hélisurfaces. Gardez vos distances avec les soutes à munitions et le dépôt de carburant : ils sautent.",
            MissionGoal.Defense, TimeOfDay.Day, 0.003f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 3, "Contacts au nord, ils traversent la piste vers le poste !", (EnemyType.BlackAnt, 18))),
                new PhaseDef(6, 0,
                    new WaveDef("east", 0, "Fourmis rouges à l'est, elles arrivent du dépôt de carburant !", (EnemyType.RedAnt, 8), (EnemyType.BlackAnt, 12)),
                    new WaveDef("west", 6, "Fourmis acides entre les casernements, elles visent le poste de loin !", (EnemyType.AcidAnt, 10))),
                new PhaseDef(6, 0,
                    new WaveDef("beach", 0, "Elles forcent la porte sud par la route du désert !", (EnemyType.BlackAnt, 14), (EnemyType.Scavenger, 5)),
                    new WaveDef("north", 8, "Guêpes au-dessus de la piste !", (EnemyType.Wasp, 14))),
                new PhaseDef(5, 0,
                    new WaveDef("north", 0, "Dernière vague : un marcheur de combat sort des dunes avec toute la colonie. Visez les genoux !", (EnemyType.Walker, 1), (EnemyType.RedAnt, 10)),
                    new WaveDef("east", 4, null, (EnemyType.BlackAnt, 14)),
                    new WaveDef("west", 8, "Des araignées sur le toit du grand hangar !", (EnemyType.Spider, 8))),
            }, Weather.Clear, MapId.Desert, new ObjectiveDef(StructureKind.CommandPost, 0f, -62f, 16000f)),
        new(18, "Vent de sable",
            "Une tempête de sable recouvre la base et la colonie en profite pour creuser ses nids au cœur du site : derrière le grand hangar, au pied du dépôt de carburant, au nord de la piste et près du radôme. Détruisez-les tous. La visibilité tombe à une centaine de mètres : restez groupés, repérez les nids au radar et ne tirez pas à l'aveugle vers les soutes à munitions.",
            MissionGoal.DestroyNests, TimeOfDay.Day, 0.004f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Nids repérés dans la base. Visez-les aux explosifs !", (EnemyType.BlackAnt, 14), (EnemyType.RedAnt, 6))),
                new PhaseDef(0, 60,
                    new WaveDef("west", 0, "Araignées dans les casernements, elles sortent de la tempête !", (EnemyType.Spider, 12))),
                new PhaseDef(0, 120,
                    new WaveDef("east", 0, "Essaim de guêpes au-dessus du dépôt de carburant !", (EnemyType.Wasp, 14), (EnemyType.AcidAnt, 8))),
                new PhaseDef(0, 180,
                    new WaveDef("north", 0, "Mille-pattes et charognards sur la piste !", (EnemyType.Centipede, 2), (EnemyType.Scavenger, 6), (EnemyType.RedAnt, 8)),
                    new WaveDef("beach", 8, "D'autres arrivent par la route du désert !", (EnemyType.BlackAnt, 12))),
            }, Weather.Sandstorm, MapId.Desert),
        // Titan reptilien (spécification §10.2).
        new(19, "Le titan",
            "Une créature reptilienne de soixante mètres est sortie des collines et marche droit sur le centre-ville. Rien ne l'arrête : les immeubles s'effondrent à son passage et son souffle de feu embrase des rues entières. Son dos et ses flancs sont cuirassés de plaques osseuses : brisez-les aux explosifs pour mettre la chair à vif, puis visez les plaies. Quand sa gorge s'embrase, quittez la ligne de feu ; et ne restez pas dans son dos, sa queue balaie tout.",
            MissionGoal.Boss, TimeOfDay.Dusk, 0.006f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contact colossal au nord : un titan reptilien ! Brisez ses plaques d'armure, visez la chair à vif.", (EnemyType.Titan, 1), (EnemyType.RedAnt, 8)),
                    new WaveDef("east", 10, "Fourmis à l'est, elles fuient devant le titan !", (EnemyType.BlackAnt, 14))),
                new PhaseDef(0, 55,
                    new WaveDef("west", 0, "Fourmis acides et guêpes à l'ouest, elles suivent la bête !", (EnemyType.AcidAnt, 10), (EnemyType.Wasp, 10))),
                new PhaseDef(0, 115,
                    new WaveDef("beach", 0, "La colonie remonte de la plage, prise en tenaille !", (EnemyType.RedAnt, 10), (EnemyType.BlackAnt, 12))),
                new PhaseDef(0, 180,
                    new WaveDef("east", 0, "Escadrille de drones à l'est ! Ne quittez pas le titan des yeux.", (EnemyType.Drone, 10), (EnemyType.Spider, 6))),
                new PhaseDef(0, 245,
                    new WaveDef("north", 0, "Des mille-pattes surgissent des ruines au nord !", (EnemyType.RedAnt, 10), (EnemyType.Centipede, 2))),
            }, Weather.Clear, MapId.City),
        // Aliens.
        new(20, "Débarquement",
            "Des soldats extraterrestres ont pris pied en plein centre-ville. Ces humanoïdes de cinq mètres se mettent à couvert derrière les immeubles et les camions, puis se relèvent pour tirer en rafales : débusquez-les aux explosifs ou prenez-les à revers, et visez la tête. Les gardes avancent derrière un bouclier et chargent ; leur cellule dorsale est leur point faible.",
            MissionGoal.Exterminate, TimeOfDay.Dusk, 0.006f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contacts humanoïdes au nord ! Ils cherchent des abris, ne les laissez pas s'installer.", (EnemyType.AlienTrooper, 6))),
                new PhaseDef(2, 0,
                    new WaveDef("east", 0, "Des gardes à bouclier à l'est ! Contournez-les : la cellule dans leur dos est leur point faible.", (EnemyType.AlienGuard, 2), (EnemyType.AlienTrooper, 4)),
                    new WaveDef("west", 7, "Drones en soutien à l'ouest !", (EnemyType.Drone, 6))),
                new PhaseDef(3, 0,
                    new WaveDef("east", 0, "Vaisseau de largage en approche : il dépose des fantassins. Visez la soute ouverte !", (EnemyType.DropShip, 1)),
                    new WaveDef("north", 6, "Volants au-dessus des toits, au nord !", (EnemyType.AlienFlyer, 4))),
                new PhaseDef(3, 0,
                    new WaveDef("south", 0, "Dernière vague : ils remontent de la plage avec des volants. Tenez la ligne !", (EnemyType.AlienTrooper, 7), (EnemyType.AlienGuard, 2)),
                    new WaveDef("west", 6, null, (EnemyType.AlienFlyer, 4))),
            }, Weather.Clear, MapId.City),
        new(21, "Les portes",
            "Des portails extraterrestres se sont ouverts dans les terrains dévastés du quartier d'affaires. Tant qu'un portail tient debout, il laisse passer des escouades d'aliens et des machines. Détruisez-les tous : leur anneau résiste aux balles mais encaisse mal les explosifs.",
            MissionGoal.DestroyNests, TimeOfDay.Dusk, 0.007f, 4,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("center", 3, "Portails repérés entre les tours. Détruisez-les aux explosifs !", (EnemyType.AlienTrooper, 5))),
                new PhaseDef(0, 70,
                    new WaveDef("east", 0, "Volants entre les tours à l'est !", (EnemyType.AlienFlyer, 5), (EnemyType.Drone, 6))),
                new PhaseDef(0, 140,
                    new WaveDef("west", 0, "Gardes à bouclier sur le parvis ouest !", (EnemyType.AlienGuard, 3), (EnemyType.AlienTrooper, 5))),
                new PhaseDef(0, 210,
                    new WaveDef("north", 0, "Un marcheur escorte une colonne sur l'avenue nord !", (EnemyType.Walker, 1), (EnemyType.AlienTrooper, 6))),
            }, Weather.Clear, MapId.Business, null, EnemyType.Portal),
        // Capitale en ruines : finale de la campagne (spécification §9.1, §10.2).
        new(22, "Dernier rempart",
            "Le vaisseau-mère est au-dessus de la capitale. Sa coque couvre le ciel d'un bout à l'autre du centre historique et il déverse ses troupes sur ce qu'il en reste. C'est la dernière ligne : derrière nous, il n'y a plus rien. Sa coque ne craint rien ; quand son canon principal s'ouvre, le cœur incandescent et les trappes sont à nu : concentrez tout dessus. Dès que le sol s'allume sous le canon, quittez la zone. Abattez ses tourelles, tenez la place. Pour tous ceux qui sont tombés avant nous.",
            MissionGoal.Boss, TimeOfDay.Dusk, 0.0022f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Contact colossal au-dessus de la ville : le vaisseau-mère ! Visez le canon quand il s'ouvre.", (EnemyType.Mothership, 1), (EnemyType.AlienTrooper, 6)),
                    new WaveDef("east", 12, "Drones à l'est, ils sortent des soutes !", (EnemyType.Drone, 8))),
                new PhaseDef(0, 60,
                    new WaveDef("west", 0, "Le jardin est envahi : fantassins et gardes à bouclier à l'ouest !", (EnemyType.AlienTrooper, 6), (EnemyType.AlienGuard, 2))),
                new PhaseDef(0, 130,
                    new WaveDef("south", 0, "Des marcheurs franchissent le fleuve par le grand pont !", (EnemyType.Walker, 2), (EnemyType.AlienTrooper, 4))),
                new PhaseDef(0, 200,
                    new WaveDef("beach", 0, "Un tripode remonte le quai nord, près du pont éventré !", (EnemyType.Tripod, 1), (EnemyType.AlienFlyer, 4))),
                new PhaseDef(0, 280,
                    new WaveDef("center", 0, "Ils descendent le boulevard sous le viaduc !", (EnemyType.AlienGuard, 3), (EnemyType.AlienTrooper, 6), (EnemyType.Drone, 6))),
                new PhaseDef(0, 360,
                    new WaveDef("north", 0, "Nouvelle colonne sur l'avenue, avec des marcheurs. Tenez la place !", (EnemyType.Walker, 2), (EnemyType.AlienFlyer, 6))),
            }, Weather.Clear, MapId.Capital),
        new(23, "Reconquête",
            "Le vaisseau-mère s'est abattu au-delà du fleuve, mais ses troupes tiennent encore le cœur de la capitale : des machines et des aliens retranchés autour de la grande place, de l'avenue jusqu'au parlement. Reprenez la ville rue par rue. Les marcheurs se postent sur les quais et pilonnent la place ; les gardes tiennent les ponts. Gare aux façades : elles s'effondrent sous les obus.",
            MissionGoal.Exterminate, TimeOfDay.Day, 0.0025f, 0,
            new[]
            {
                new PhaseDef(0, 0,
                    new WaveDef("north", 2, "Machines sur l'avenue nord, deux marcheurs ! Visez les genoux.", (EnemyType.Walker, 2), (EnemyType.AlienTrooper, 6))),
                new PhaseDef(3, 0,
                    new WaveDef("east", 0, "Gardes à bouclier dans les rues à l'est !", (EnemyType.AlienGuard, 3), (EnemyType.AlienTrooper, 5)),
                    new WaveDef("west", 6, "Volants au-dessus du jardin !", (EnemyType.AlienFlyer, 5))),
                new PhaseDef(3, 0,
                    new WaveDef("beach", 0, "Vaisseau de largage au-dessus du quai : visez la soute ouverte !", (EnemyType.DropShip, 1)),
                    new WaveDef("south", 8, "Un tripode sur la rive sud, devant le parlement !", (EnemyType.Tripod, 1), (EnemyType.Drone, 8))),
                new PhaseDef(4, 0,
                    new WaveDef("center", 0, "Dernière poche de résistance sous le viaduc. Nettoyez la place !", (EnemyType.Walker, 2), (EnemyType.AlienGuard, 2), (EnemyType.AlienTrooper, 8)),
                    new WaveDef("north", 6, null, (EnemyType.AlienFlyer, 4))),
            }, Weather.Rain, MapId.Capital),
    };
}
