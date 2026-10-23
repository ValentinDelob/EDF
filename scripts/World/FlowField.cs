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

    public FlowField()
    {
        _blocked = new bool[N * N];
        _dist = new int[N * N];
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
            _blocked[z * N + x] = blocked;
    }

    public void BlockOutside(float half, float seaZ)
    {
        for (var i = 0; i < N * N; i++)
        {
            var c = CellCenter(i);
            if (Mathf.Abs(c.X) > half + 4 || c.Z < -half - 4 || c.Z > seaZ) _blocked[i] = true;
        }
    }

    public bool IsBlocked(Vector3 p) => _blocked[Index(p)];

    public void Compute(IEnumerable<Vector3> targets)
    {
        for (var i = 0; i < _dist.Length; i++) _dist[i] = int.MaxValue;
        _queue.Clear();
        foreach (var t in targets)
        {
            var i = Index(t);
            _dist[i] = 0;
            _queue.Enqueue(i);
        }
        while (_queue.Count > 0)
        {
            var i = _queue.Dequeue();
            var x = i % N;
            var z = i / N;
            var d = _dist[i] + 1;
            Visit(x + 1, z, d); Visit(x - 1, z, d); Visit(x, z + 1, d); Visit(x, z - 1, d);
        }
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
