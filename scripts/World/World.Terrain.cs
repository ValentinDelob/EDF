using Godot;

namespace Rempart;

/// <summary>
/// Hauteur du sol partagée : tout ce qui se pose « au sol » sans physique (insectes non grimpeurs, volants, alliés, caisses,
/// camions du convoi, structure défendue, véhicules au départ, apparitions, frappes aériennes) la lit ici.
/// Cartes plates : pas de relief, 0 partout (comportement inchangé). Vallée forestière : <see cref="Heightfield"/>.
/// </summary>
public partial class World
{
    /// <summary>Relief de la carte (null : sol plat à y = 0).</summary>
    public Heightfield Terrain;

    /// <summary>Hauteur du sol praticable en (x, z) : relief et tabliers (ponts), 0 sur les cartes plates.</summary>
    public float GroundHeight(float x, float z) => Terrain?.Height(x, z) ?? 0f;

    public float GroundHeight(Vector3 p) => Terrain?.Height(p.X, p.Z) ?? 0f;

    /// <summary>Hauteur du terrain seul, sans les tabliers des ponts (niveau « sous-sol » des grimpeurs) ; 0 sur les cartes plates.</summary>
    public float TerrainHeight(float x, float z) => Terrain?.Terrain(x, z) ?? 0f;

    /// <summary>Normale du sol praticable (verticale sur les cartes plates).</summary>
    public Vector3 GroundNormal(float x, float z) => Terrain?.Normal(x, z) ?? Vector3.Up;

    /// <summary>Le point ramené sur le sol (plus <paramref name="lift"/> m).</summary>
    public Vector3 OnGround(Vector3 p, float lift = 0f) => new(p.X, GroundHeight(p.X, p.Z) + lift, p.Z);

    /// <summary>Hauteur du sol de la mission en cours (0 hors mission).</summary>
    public static float GroundAt(float x, float z) => Current?.GroundHeight(x, z) ?? 0f;
}
