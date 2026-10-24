using Godot;

namespace Rempart;

/// <summary>
/// Points d'entrée publics des émetteurs d'explosion, pour les effets de destruction du décor (poussière d'effondrement,
/// éclats de mobilier, fumée d'incendie) : mêmes particules, même rendu, aucun nœud créé.
/// </summary>
public partial class ExplosionFx
{
    /// <summary>Nuage de poussière (rasant, s'étale et traîne).</summary>
    public void DustPuff(Vector3 pos, Vector3 vel, float size, float life, Color color) => Emit(_billows, TDust, pos, vel, size, life, color);

    /// <summary>Fumée qui monte et dérive avec le vent.</summary>
    public void SmokePuff(Vector3 pos, Vector3 vel, float size, float life, Color color) => Emit(_billows, TSmoke, pos, vel, size, life, color);

    /// <summary>Boule de feu (bouillonne puis se change en suie).</summary>
    public void FirePuff(Vector3 pos, Vector3 vel, float size, float life, Color color) => Emit(_billows, TFire, pos, vel, size, life, color);

    /// <summary>Éclat solide qui tombe, rebondit au sol et culbute.</summary>
    public void Chunk(Vector3 pos, Vector3 vel, float size, float life, Color color) => Emit(_chunks, TChunk, pos, vel, size, life, color);

    /// <summary>Étincelle ou éclat de verre (traînée lumineuse si la couleur est vive).</summary>
    public void Spark(Vector3 pos, Vector3 vel, float size, float life, Color color) => Emit(_sparks, TSpark, pos, vel, size, life, color);

    /// <summary>Lueur brève (réserve partagée avec les explosions).</summary>
    public void Flash(Vector3 pos, Color color, float energy, float range, float life) => StartLight(pos, color, energy, range, life);

    /// <summary>Distance à la caméra active (niveau de détail des effets de destruction).</summary>
    public float CameraDistance(Vector3 pos)
    {
        var cam = GetViewport()?.GetCamera3D();
        return cam != null ? cam.GlobalPosition.DistanceTo(pos) : 30f;
    }

    /// <summary>Charge récente des émetteurs (explosions par seconde lissées) : les effets de décor s'allègent avec.</summary>
    public float Busy => _busy;
}
