using Godot;

namespace Rempart;

/// <summary>
/// Mobilier de la capitale : candélabres de fonte (rue : une lanterne ; pont et place : trois lanternes sur un fût
/// orné), colonne d'affichage, kiosque à journaux, grille de jardin. Familles destructibles par les préfixes des clés
/// (<see cref="KindFor"/>) : « lamp… » = poteau (il bascule), « busstop… » = abri (il éclate), « fencepanel… » = petit mobilier.
/// </summary>
public static partial class Props
{
    private static readonly Color CastIron = new(0.1f, 0.115f, 0.11f);

    /// <summary>Candélabre de fonte : socle mouluré, fût cannelé, crosse et lanterne(s) ; trois lanternes pour le pont et la place.</summary>
    public static ArrayMesh CapitalLamp(bool night, bool triple) => Cached($"lampCap{night}{triple}", k =>
    {
        var m = Mats.DetailMetal();
        Material glass = night ? Mats.Lamps() : Mats.Detail();
        k.Col = CastIron;
        k.Cylinder(m, Vector3.Zero, 0.34f, 0.28f, 0.5f, 8);
        k.Cylinder(m, new Vector3(0, 0.5f, 0), 0.24f, 0.2f, 0.9f, 8);
        k.Cylinder(m, new Vector3(0, 1.4f, 0), 0.13f, 0.09f, triple ? 3.2f : 3.6f, 8);
        var top = triple ? 4.6f : 5f;
        k.Cylinder(m, new Vector3(0, top, 0), 0.16f, 0.1f, 0.25f, 8);
        void Lantern(Vector3 p)
        {
            k.Col = CastIron;
            k.Cylinder(m, p, 0.12f, 0.24f, 0.18f, 6);
            k.Col = night ? new Color(1f, 0.82f, 0.55f) : new Color(0.62f, 0.6f, 0.52f);
            k.Cylinder(glass, p + Vector3.Up * 0.18f, 0.24f, 0.3f, 0.55f, 6);
            k.Col = CastIron;
            k.Cylinder(m, p + Vector3.Up * 0.73f, 0.34f, 0.04f, 0.28f, 6);
            k.Cylinder(m, p + Vector3.Up * 1.0f, 0.04f, 0.01f, 0.2f, 4);
        }
        if (triple)
        {
            Lantern(new Vector3(0, top + 0.25f, 0));
            foreach (var s in new[] { -1f, 1f })
            {
                k.Col = CastIron;
                k.Tube(m, new Vector3(0, top - 0.2f, 0), new Vector3(s * 0.8f, top + 0.05f, 0), 0.05f, 5);
                k.Tube(m, new Vector3(s * 0.8f, top + 0.05f, 0), new Vector3(s * 0.9f, top - 0.25f, 0), 0.04f, 5);
                Lantern(new Vector3(s * 0.9f, top - 0.35f, 0));
            }
        }
        else
        {
            k.Col = CastIron;
            k.Tube(m, new Vector3(0, top + 0.1f, 0), new Vector3(0, top + 0.4f, 0.55f), 0.05f, 5);
            Lantern(new Vector3(0, top - 0.05f, 0.62f));
        }
    });

    /// <summary>Colonne d'affichage : cylindre vert sombre, affiches lacérées, dôme à écailles (famille abri).</summary>
    public static ArrayMesh MorrisColumn() => Cached("busstopMorris", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.1f, 0.16f, 0.12f);
        k.Cylinder(m, Vector3.Zero, 0.72f, 0.72f, 0.35f, 14);
        k.Col = new Color(0.55f, 0.52f, 0.45f);
        k.Cylinder(d, new Vector3(0, 0.35f, 0), 0.62f, 0.62f, 2.4f, 14, false);
        k.Col = new Color(0.1f, 0.16f, 0.12f);
        k.Cylinder(m, new Vector3(0, 2.75f, 0), 0.7f, 0.74f, 0.25f, 14);
        k.Cylinder(m, new Vector3(0, 3f, 0), 0.66f, 0.12f, 0.7f, 14);
        k.Cylinder(m, new Vector3(0, 3.7f, 0), 0.08f, 0.02f, 0.3f, 6);
        // Lambeaux d'affiches sombres.
        k.Col = new Color(0.3f, 0.12f, 0.1f);
        k.Box(d, new Vector3(0.63f, 1.5f, 0), new Vector3(0.02f, 1.1f, 0.6f));
        k.Col = new Color(0.12f, 0.14f, 0.2f);
        k.Box(d, new Vector3(-0.3f, 1.2f, 0.56f), new Vector3(0.5f, 0.9f, 0.02f), new Basis(Vector3.Up, 0.5f));
    });

    /// <summary>Kiosque à journaux : caisson vert, auvent, toit en dôme, volets baissés (famille abri).</summary>
    public static ArrayMesh Kiosk() => Cached("busstopKiosk", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.1f, 0.17f, 0.13f);
        k.Box(m, new Vector3(0, 1.2f, 0), new Vector3(2.6f, 2.4f, 1.8f));
        k.Col = new Color(0.36f, 0.37f, 0.35f);
        k.Box(m, new Vector3(0, 1.3f, 0.91f), new Vector3(2.2f, 1.6f, 0.04f));
        k.Col = new Color(0.1f, 0.17f, 0.13f);
        k.Box(m, new Vector3(0, 2.5f, 0.2f), new Vector3(3f, 0.12f, 2.6f));
        k.Frustum(m, new Vector3(0, 2.56f, 0.2f), new Vector2(3f, 2.6f), new Vector2(0.6f, 0.4f), 0.7f);
        k.Col = new Color(0.55f, 0.45f, 0.2f);
        k.Box(d, new Vector3(0, 2.25f, 1.12f), new Vector3(2.4f, 0.3f, 0.06f));
    });
}
