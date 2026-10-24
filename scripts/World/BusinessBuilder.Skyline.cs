using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Quartier au-delà des murs invisibles : îlots de tours et de bureaux jusqu'à ~1 km (ligne d'horizon), sol
/// d'enrobé, sans collision ni ombres portées. Maillages fusionnés par secteurs de 400 m (peu d'appels de rendu).
/// </summary>
public static partial class BusinessBuilder
{
    private static void BuildSkyline(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 3131 };
        // Sol lointain autour de l'aire de jeu (couronne de dalles, sans collision).
        var asphalt = Mats.Asphalt();
        foreach (var (x0, z0, x1, z1) in new[] { (-1800f, -1800f, 1800f, -262f), (-1800f, 200f, 1800f, 1800f), (-1800f, -262f, -262f, 200f), (262f, -262f, 1800f, 200f) })
            CityBuilder.AddSlab(c.Root, c.Body, new Vector3((x0 + x1) / 2, -0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1f, z1 - z0), asphalt, collide: false);

        var sectors = new Dictionary<(int, int), MeshKit>();
        MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / 400f), Mathf.FloorToInt(p.Z / 400f));
            if (!sectors.TryGetValue(key, out var k)) sectors[key] = k = new MeshKit();
            return k;
        }
        var facades = new[] { 6, 7, 2, 7, 6, 0 };
        // Îlots de 44 m sur la trame des rues (pas de 60 m), au-delà des îlots de bordure.
        for (var ix = -16; ix <= 15; ix++)
        for (var iz = -16; iz <= 15; iz++)
        {
            var cx = 280f + ix * 60f;
            var cz = iz >= 0 ? 232f + iz * 60f : -280f + (iz + 1) * 60f;   // au sud, recul : le viaduc domine des terrains vagues
            if (cx > -250f && cx < 250f && cz > -250f && cz < 180f) continue;
            var dist = new Vector2(cx, cz).Length();
            if (dist > 1150f) continue;
            // Deux grappes de grandes tours (pôles d'affaires voisins) au nord-ouest et à l'est.
            var cluster = Mathf.Max(Mathf.Clamp(1f - new Vector2(cx + 420f, cz + 380f).Length() / 330f, 0, 1), Mathf.Clamp(1f - new Vector2(cx - 520f, cz + 60f).Length() / 300f, 0, 1));
            var n = rng.Randf() < 0.45f ? 2 : 1;
            for (var i = 0; i < n; i++)
            {
                var w = rng.RandfRange(18f, n == 1 ? 40f : 20f);
                var d = rng.RandfRange(18f, 40f);
                var tall = rng.Randf() < 0.18f + cluster * 0.6f;
                var hgt = tall ? rng.RandfRange(70f, 120f + cluster * 110f) : rng.RandfRange(14f, 48f);
                hgt = Mathf.Snapped(hgt, 3.2f) + 0.6f;
                var off = n == 1 ? Vector2.Zero : new Vector2(i == 0 ? -11f : 11f, rng.RandfRange(-2, 2));
                var p = new Vector3(cx + off.X, 0, cz + off.Y);
                var v = facades[rng.RandiRange(0, facades.Length - 1)];
                var lit = c.Night && rng.Randf() < 0.08f;
                var tint = new Color(0.82f, 0.82f, 0.82f) * (0.85f + 0.25f * rng.Randf());
                tint.A = 1f;
                FarBox(At(p), Mats.Facade(v, lit), p, new Vector3(w, hgt, d), tint);
                // Quelques tours lointaines en feu (panache au sommet).
                if (tall && rng.Randf() < 0.12f) c.Ruins.Smolder(p + Vector3.Up * (hgt + 2f), 3f);
            }
        }
        foreach (var (key, k) in sectors)
            c.Root.AddChild(new MeshInstance3D { Name = $"Skyline_{key.Item1}_{key.Item2}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Volume lointain : quatre façades (UV en mètres, trame depuis le haut comme les immeubles), toit sombre.</summary>
    private static void FarBox(MeshKit k, Material facade, Vector3 p, Vector3 s, Color tint)
    {
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var dark = new Color(tint.R * 0.7f, tint.G * 0.69f, tint.B * 0.68f);
        float V(float y) => (h - y) / 3.2f;
        void Wall(Vector3 a, Vector3 b, float len, Vector3 n)
        {
            k.QuadC(facade, p + a, p + b, p + b + Vector3.Up * h, p + a + Vector3.Up * h,
                new Vector2(0, V(0)), new Vector2(len / 7f, V(0)), new Vector2(len / 7f, 0), new Vector2(0, 0), tint, tint, dark, dark, n);
        }
        Wall(new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), s.X, Vector3.Back);
        Wall(new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), s.X, Vector3.Forward);
        Wall(new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), s.Z, Vector3.Right);
        Wall(new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), s.Z, Vector3.Left);
        k.Col = new Color(0.22f, 0.22f, 0.22f);
        var y = h;
        k.Quad(Mats.Detail(), p + new Vector3(-hx, y, hz), p + new Vector3(hx, y, hz), p + new Vector3(hx, y, -hz), p + new Vector3(-hx, y, -hz));
        // Acrotère sombre (silhouette nette sur le ciel).
        k.Col = new Color(0.2f, 0.2f, 0.21f);
        k.Xf = new Transform3D(Basis.Identity, p);
        k.Ring(Mats.Detail(), hx, hz, h - 0.2f, h + 1.2f, 0.3f, 0.05f);
        k.Xf = Transform3D.Identity;
    }
}
