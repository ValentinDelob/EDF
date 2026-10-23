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
    PhaseDef[] Phases);

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
                    new WaveDef("west", 0, "Renforts ennemis à l'ouest.", (EnemyType.AcidAnt, 10), (EnemyType.RedAnt, 8), (EnemyType.Scavenger, 4))),
            }),
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
            }),
    };
}
