using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Relief d'une carte : grille de hauteurs régulière (pas de <see cref="Step"/> m), interpolée par triangles exactement
/// comme la forme de collision <see cref="HeightMapShape3D"/> qu'elle produit (diagonale de (i + 1, j) à (i, j + 1), vérifiée
/// par « --forestprobe » contre des rayons),
/// plus des tabliers praticables posés par-dessus (ponts, couronnement de barrage). Hors de la grille : hauteur du bord.
///
/// <see cref="World.GroundHeight(float, float)"/> la consulte pour tout ce qui marche « au sol » sans physique (insectes non
/// grimpeurs, alliés, caisses, camions du convoi, apparitions) ; les cartes sans relief n'en ont pas (sol plat à y = 0).
/// </summary>
public sealed class Heightfield
{
    public readonly float X0, Z0, Step;
    public readonly int Nx, Nz;
    /// <summary>Hauteurs (m), ligne par ligne : indice j × Nx + i pour le point (X0 + i × Step, Z0 + j × Step).</summary>
    public readonly float[] H;

    /// <summary>Tablier praticable rectangulaire (pont, couronnement) : hauteur linéaire le long de l'axe, plate en travers.</summary>
    public readonly struct Deck
    {
        public readonly Vector2 Center, Dir;
        public readonly float HalfLen, HalfWid, Y0, Y1;

        public Deck(Vector2 center, Vector2 dir, float halfLen, float halfWid, float y0, float y1)
        {
            Center = center;
            Dir = dir.Normalized();
            HalfLen = halfLen;
            HalfWid = halfWid;
            Y0 = y0;
            Y1 = y1;
        }

        public bool Contains(float x, float z, out float y)
        {
            var d = new Vector2(x, z) - Center;
            var along = d.Dot(Dir);
            var across = d.X * Dir.Y - d.Y * Dir.X;
            y = Mathf.Lerp(Y0, Y1, (along + HalfLen) / (2f * HalfLen));
            return Mathf.Abs(along) <= HalfLen && Mathf.Abs(across) <= HalfWid;
        }
    }

    private readonly List<Deck> _decks = new();
    private Rect2 _deckBounds;

    public Heightfield(float x0, float z0, float step, int nx, int nz)
    {
        X0 = x0;
        Z0 = z0;
        Step = step;
        Nx = nx;
        Nz = nz;
        H = new float[nx * nz];
    }

    public float X1 => X0 + (Nx - 1) * Step;
    public float Z1 => Z0 + (Nz - 1) * Step;

    public float At(int i, int j) => H[Mathf.Clamp(j, 0, Nz - 1) * Nx + Mathf.Clamp(i, 0, Nx - 1)];

    public void AddDeck(Deck d)
    {
        _decks.Add(d);
        var r = Mathf.Sqrt(d.HalfLen * d.HalfLen + d.HalfWid * d.HalfWid);
        var box = new Rect2(d.Center - new Vector2(r, r), new Vector2(r, r) * 2f);
        _deckBounds = _decks.Count == 1 ? box : _deckBounds.Merge(box);
    }

    /// <summary>Hauteur du terrain seul (sans les tabliers), interpolée comme la forme de collision.</summary>
    public float Terrain(float x, float z)
    {
        var fx = Mathf.Clamp((x - X0) / Step, 0f, Nx - 1.0001f);
        var fz = Mathf.Clamp((z - Z0) / Step, 0f, Nz - 1.0001f);
        var i = (int)fx;
        var j = (int)fz;
        var u = fx - i;
        var v = fz - j;
        var k = j * Nx + i;
        var h10 = H[k + 1];
        var h01 = H[k + Nx];
        if (u + v <= 1f)
        {
            var h00 = H[k];
            return h00 + u * (h10 - h00) + v * (h01 - h00);
        }
        var h11 = H[k + Nx + 1];
        return h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
    }

    /// <summary>Hauteur du sol praticable : terrain, ou tablier au-dessus.</summary>
    public float Height(float x, float z)
    {
        var h = Terrain(x, z);
        if (_decks.Count == 0 || !_deckBounds.HasPoint(new Vector2(x, z))) return h;
        foreach (var d in _decks)
            if (d.Contains(x, z, out var y) && y > h) h = y;
        return h;
    }

    /// <summary>Normale du sol praticable (différences finies sur un pas de grille).</summary>
    public Vector3 Normal(float x, float z)
    {
        var e = Step;
        var dx = Height(x + e, z) - Height(x - e, z);
        var dz = Height(x, z + e) - Height(x, z - e);
        return new Vector3(-dx, 2f * e, -dz).Normalized();
    }

    /// <summary>Pente du terrain (0 à plat, 1 à la verticale) au point de grille (i, j), sur ses voisins directs.</summary>
    public float SlopeAt(int i, int j)
    {
        var dx = (At(i + 1, j) - At(i - 1, j)) / (2f * Step);
        var dz = (At(i, j + 1) - At(i, j - 1)) / (2f * Step);
        return 1f - 1f / Mathf.Sqrt(1f + dx * dx + dz * dz);
    }

    /// <summary>
    /// Forme de collision du terrain (couche monde) : HeightMapShape3D carrée de côté 2^n + 1 si possible, mise à l'échelle
    /// uniforme du pas (hauteurs divisées d'autant) pour garder exactement la triangulation de <see cref="Terrain"/>.
    /// </summary>
    public CollisionShape3D CollisionShape()
    {
        var data = new float[H.Length];
        for (var i = 0; i < H.Length; i++) data[i] = H[i] / Step;
        return new CollisionShape3D
        {
            Name = "Terrain",
            Shape = new HeightMapShape3D { MapWidth = Nx, MapDepth = Nz, MapData = data },
            Position = new Vector3(X0 + (Nx - 1) * Step * 0.5f, 0f, Z0 + (Nz - 1) * Step * 0.5f),
            Scale = Vector3.One * Step,
        };
    }
}
