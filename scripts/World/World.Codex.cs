using System.Collections.Generic;

namespace Rempart;

/// <summary>Encyclopédie : éliminations de l'escouade par type d'ennemi, versées au bestiaire du profil en fin de mission.</summary>
public partial class World
{
    public readonly Dictionary<EnemyType, int> KillsByType = new();

    private void CountKill(Enemy e)
    {
        KillsByType.TryGetValue(e.Def.Type, out var n);
        KillsByType[e.Def.Type] = n + 1;
    }
}
