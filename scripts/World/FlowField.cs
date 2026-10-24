using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Champ de flux sur grille (cellules de 4 m) : distance aux joueurs vivants contournant les immeubles.
/// Recalculé par l'hôte quelques fois par seconde ; les ennemis au sol suivent la pente.
/// </summary>
public class FlowField
{
    public const float Cell = 4f;
    public const float Extent = 240f;
    public readonly int N = (int)(Extent * 2 / Cell);
    private readonly bool[] _blocked;
    private readonly int[] _dist;
    private readonly Queue<int> _queue = new();

    /// <summary>Hauteur de l'immeuble qui couvre la cellule (0 : pas d'immeuble ; bloquée sans hauteur : hors carte).</summary>
    private readonly float[] _height;
    /// <summary>Distance pondérée pour les insectes grimpeurs (immeubles franchissables, plus coûteux).</summary>
    private readonly int[] _climb;
    /// <summary>Files par distance (algorithme de Dial : coûts entiers bornés, file circulaire).</summary>
    private readonly List<int>[] _buckets = new List<int>[Buckets];
    private const int Buckets = 128;

    /// <summary>Coût d'une cellule libre pour les grimpeurs (unités : quart de cellule).</summary>
    private const int FreeCost = 4;

    public FlowField()
    {
        _blocked = new bool[N * N];
        _dist = new int[N * N];
        _height = new float[N * N];
        _climb = new int[N * N];
        for (var i = 0; i < Buckets; i++) _buckets[i] = new List<int>();
    }

    public int Index(Vector3 p)
    {
        var x = Mathf.Clamp((int)((p.X + Extent) / Cell), 0, N - 1);
        var z = Mathf.Clamp((int)((p.Z + Extent) / Cell), 0, N - 1);
        return z * N + x;
    }

    public Vector3 CellCenter(int i) => new((i % N) * Cell - Extent + Cell / 2, 0, (i / N) * Cell - Extent + Cell / 2);

    public void SetBlockedRect(Vector3 center, Vector3 size, bool blocked, float margin = 1.5f)
    {
        var minX = (int)((center.X - size.X / 2 - margin + Extent) / Cell);
        var maxX = (int)((center.X + size.X / 2 + margin + Extent) / Cell);
        var minZ = (int)((center.Z - size.Z / 2 - margin + Extent) / Cell);
        var maxZ = (int)((center.Z + size.Z / 2 + margin + Extent) / Cell);
        for (var z = Mathf.Max(minZ, 0); z <= Mathf.Min(maxZ, N - 1); z++)
        for (var x = Mathf.Max(minX, 0); x <= Mathf.Min(maxX, N - 1); x++)
        {
            _blocked[z * N + x] = blocked;
            _height[z * N + x] = blocked ? Mathf.Max(_height[z * N + x], size.Y) : 0f;
        }
    }

    /// <summary>Cellule franchissable par un grimpeur (libre, ou couverte par un immeuble à escalader).</summary>
    private bool Climbable(int i) => !_blocked[i] || _height[i] > 0f;

    /// <summary>Coût d'entrée dans une cellule pour un grimpeur : un immeuble coûte selon sa hauteur (montée + descente).</summary>
    private int ClimbCost(int i) => _blocked[i] ? Mathf.Min(FreeCost * 2 + Mathf.RoundToInt(_height[i] * 0.5f), Buckets - 1) : FreeCost;
    public void BlockOutside(float half, float seaZ)
    {
        for (var i = 0; i < N * N; i++)
        {
            var c = CellCenter(i);
            if (Mathf.Abs(c.X) > half + 4 || c.Z < -half - 4 || c.Z > seaZ) _blocked[i] = true;
        }
    }

    public bool IsBlocked(Vector3 p) => _blocked[Index(p)];

    /// <summary>Centre de la cellule libre la plus proche de <paramref name="p"/> (couronnes jusqu'à <paramref name="maxRing"/>), ou null.</summary>
    public Vector3? NearestFree(Vector3 p, int maxRing)
    {
        var i = Index(p);
        int x0 = i % N, z0 = i / N;
        for (var r = 0; r <= maxRing; r++)
        {
            var best = -1;
            var bd = float.MaxValue;
            for (var dz = -r; dz <= r; dz++)
            for (var dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                int x = x0 + dx, z = z0 + dz;
                if (x < 0 || z < 0 || x >= N || z >= N || _blocked[z * N + x]) continue;
                var d = CellCenter(z * N + x).DistanceSquaredTo(new Vector3(p.X, 0, p.Z));
                if (d < bd) { bd = d; best = z * N + x; }
            }
            if (best >= 0) return CellCenter(best);
        }
        return null;
    }

    public void Compute(IEnumerable<Vector3> targets)
    {
        for (var i = 0; i < _dist.Length; i++) _dist[i] = int.MaxValue;
        for (var i = 0; i < _climb.Length; i++) _climb[i] = int.MaxValue;
        _queue.Clear();
        foreach (var b in _buckets) b.Clear();
        var pending = 0;
        foreach (var t in targets)
        {
            var i = Index(t);
            _dist[i] = 0;
            _queue.Enqueue(i);
            _climb[i] = 0;
            _buckets[0].Add(i);
            pending++;
        }
        while (_queue.Count > 0)
        {
            var i = _queue.Dequeue();
            var x = i % N;
            var z = i / N;
            var d = _dist[i] + 1;
            Visit(x + 1, z, d); Visit(x - 1, z, d); Visit(x, z + 1, d); Visit(x, z - 1, d);
        }
        // Grimpeurs : plus courts chemins pondérés (Dial), les immeubles sont des cellules chères mais
        // franchissables (joueur sur un toit compris).
        for (var d = 0; pending > 0; d++)
        {
            var bucket = _buckets[d % Buckets];
            for (var k = 0; k < bucket.Count; k++)
            {
                var i = bucket[k];
                pending--;
                if (_climb[i] != d) continue;
                var x = i % N;
                var z = i / N;
                ClimbVisit(x + 1, z, d, ref pending); ClimbVisit(x - 1, z, d, ref pending);
                ClimbVisit(x, z + 1, d, ref pending); ClimbVisit(x, z - 1, d, ref pending);
            }
            bucket.Clear();
        }
    }

    private void ClimbVisit(int x, int z, int d, ref int pending)
    {
        if (x < 0 || z < 0 || x >= N || z >= N) return;
        var j = z * N + x;
        if (!Climbable(j)) return;
        var nd = d + ClimbCost(j);
        if (_climb[j] <= nd) return;
        _climb[j] = nd;
        _buckets[nd % Buckets].Add(j);
        pending++;
    }

    /// <summary>Direction (plan XZ) des grimpeurs : droit vers les joueurs, par-dessus les immeubles si c'est plus court.</summary>
    public Vector3 ClimbDirection(Vector3 p)
    {
        var i = Index(p);
        var x = i % N;
        var z = i / N;
        var best = _climb[i];
        var dir = Vector3.Zero;
        for (var dz = -1; dz <= 1; dz++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            var nx = x + dx;
            var nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= N || nz >= N) continue;
            var j = nz * N + nx;
            if (!Climbable(j)) continue;
            if (dx != 0 && dz != 0 && (!Climbable(z * N + nx) || !Climbable(nz * N + x))) continue;
            if (_climb[j] < best)
            {
                best = _climb[j];
                dir = new Vector3(dx, 0, dz);
            }
        }
        return dir == Vector3.Zero ? dir : dir.Normalized();
    }

    private void Visit(int x, int z, int d)
    {
        if (x < 0 || z < 0 || x >= N || z >= N) return;
        var j = z * N + x;
        if (_blocked[j] || _dist[j] <= d) return;
        _dist[j] = d;
        _queue.Enqueue(j);
    }

    /// <summary>Direction (plan XZ) vers la cellule voisine la plus proche des joueurs.</summary>
    public Vector3 Direction(Vector3 p)
    {
        var i = Index(p);
        var x = i % N;
        var z = i / N;
        var best = _dist[i];
        var dir = Vector3.Zero;
        for (var dz = -1; dz <= 1; dz++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dz == 0) continue;
            var nx = x + dx;
            var nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= N || nz >= N) continue;
            var j = nz * N + nx;
            if (_blocked[j]) continue;
            // Diagonale autorisée seulement si les deux côtés sont libres (pas de coupe d'angle).
            if (dx != 0 && dz != 0 && (_blocked[z * N + nx] || _blocked[nz * N + x])) continue;
            var d = _dist[j];
            if (d < best)
            {
                best = d;
                dir = new Vector3(dx, 0, dz);
            }
        }
        if (dir == Vector3.Zero && _blocked[i])
        {
            // Coincé dans une cellule bloquée : sortir vers la cellule libre la plus proche.
            for (var r = 1; r < 4 && dir == Vector3.Zero; r++)
            for (var dz = -r; dz <= r; dz++)
            for (var dx = -r; dx <= r; dx++)
            {
                var nx = x + dx;
                var nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= N || nz >= N) continue;
                if (!_blocked[nz * N + nx]) { dir = new Vector3(dx, 0, dz); break; }
            }
        }
        return dir == Vector3.Zero ? dir : dir.Normalized();
    }

    public int Distance(Vector3 p) => _dist[Index(p)];
}
