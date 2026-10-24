namespace Rempart;

/// <summary>Structure à défendre (mission « Défense ») : modèle et nom affiché au-dessus de sa barre de vie.</summary>
public enum StructureKind { CommandPost, RelayAntenna }

/// <summary>
/// Réglages des objectifs « Défense », « Escorte » et « Survie » (voir <see cref="MissionObjective"/>).
/// Les positions sont en mètres sur le plan (x, z) ; la carte étant identique chez tous les pairs,
/// la structure et le trajet du convoi sont reconstruits à l'identique par chaque pair.
/// </summary>
public record ObjectiveDef(
    // Défense : structure posée au plus près de (AnchorX, AnchorZ), sur une place dégagée.
    StructureKind Structure = StructureKind.CommandPost,
    float AnchorX = 0f,
    float AnchorZ = -18f,
    float StructureHp = 9000f,
    // Escorte : points de passage (x0, z0, x1, z1…) le long des rues, camions, vitesse et rayon d'escorte.
    float[] Route = null,
    int Trucks = 3,
    float TruckHp = 2600f,
    float TruckSpeed = 5f,
    float EscortRadius = 40f,
    // Survie : durée à tenir et intervalle entre deux vagues (les vagues des phases tournent en boucle, de plus en plus fournies).
    float SurviveSeconds = 600f,
    float WaveInterval = 30f,
    float WaveGrowth = 0.18f);
