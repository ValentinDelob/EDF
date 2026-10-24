using Godot;

namespace Rempart;

/// <summary>
/// Aérodrome de la base : piste en béton (prolongée au-delà du rempart jusqu'à l'horizon), voie de circulation et
/// bretelles, tarmac ; marquages délavés (axe, bords, seuils, points de visée, zones de toucher), dépôts de gomme,
/// lignes de guidage jaunes vers les hangars, balises de bord de piste (petit mobilier destructible).
/// </summary>
public static partial class DesertBuilder
{
    /// <summary>Seuils de piste (piano) et points de visée, en x (symétriques).</summary>
    private const float Threshold = 470f, AimPoint = 170f;

    private static readonly Color PaintWhite = new(0.9f, 0.89f, 0.85f);
    private static readonly Color PaintYellow = new(0.92f, 0.72f, 0.3f);

    private static void BuildAirfield(Ctx c)
    {
        var apron = Mats.DesertApron();
        var runwayTone = new Color(0.84f, 0.81f, 0.77f);
        // Piste, voie de circulation, bretelles, tarmac (béton, sans collision : posés sur le sable).
        GroundRect(c, apron, -620, RunwayZ0, 620, RunwayZ1, 0.014f, runwayTone);
        GroundRect(c, apron, -200, TaxiZ - 9, 200, ApronZ0, 0.013f, new Color(0.9f, 0.87f, 0.82f));
        foreach (var x in new[] { -150f, 30f, 160f })
            GroundRect(c, apron, x - 9, RunwayZ1, x + 9, TaxiZ - 9, 0.013f, new Color(0.9f, 0.87f, 0.82f));
        GroundRect(c, apron, -205, ApronZ0, 200, ApronZ1, 0.012f, new Color(0.93f, 0.9f, 0.85f));

        var paint = Mats.AirfieldPaint();
        var y = 0.019f;
        void Stripe(float x0, float z0, float x1, float z1, Color col) =>
            GroundQuad(c, paint, new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1), col);
        var mid = (RunwayZ0 + RunwayZ1) / 2;
        // Bords de piste et axe (tirets de 30 m).
        Stripe(-Threshold, RunwayZ0 + 1.2f, Threshold, RunwayZ0 + 2.1f, PaintWhite);
        Stripe(-Threshold, RunwayZ1 - 2.1f, Threshold, RunwayZ1 - 1.2f, PaintWhite);
        for (var x = -Threshold + 40f; x < Threshold - 40f; x += 50f)
            Stripe(x, mid - 0.45f, x + 30f, mid + 0.45f, PaintWhite);
        foreach (var sx in new[] { -1f, 1f })
        {
            // Seuil « piano » : 8 bandes de part et d'autre de l'axe.
            for (var i = 0; i < 8; i++)
            {
                var z = 2.2f + i * 2.1f;
                foreach (var sz in new[] { -1f, 1f })
                {
                    var za = mid + sz * z;
                    var zb = mid + sz * (z + 1.5f);
                    Stripe(Mathf.Min(sx * Threshold, sx * (Threshold - 30f)), Mathf.Min(za, zb), Mathf.Max(sx * Threshold, sx * (Threshold - 30f)), Mathf.Max(za, zb), PaintWhite);
                }
            }
            // Points de visée (grands rectangles) et zones de toucher (paires de barres).
            foreach (var sz in new[] { -1f, 1f })
            {
                var za = mid + sz * 5f;
                var zb = mid + sz * 11f;
                Stripe(Mathf.Min(sx * AimPoint, sx * (AimPoint + 45f)), Mathf.Min(za, zb), Mathf.Max(sx * AimPoint, sx * (AimPoint + 45f)), Mathf.Max(za, zb), PaintWhite);
                foreach (var tdz in new[] { 320f, 245f })
                    for (var b = 0; b < 3; b++)
                    {
                        var z0 = mid + sz * (4f + b * 3f);
                        var z1 = mid + sz * (5.8f + b * 3f);
                        Stripe(Mathf.Min(sx * tdz, sx * (tdz + 22f)), Mathf.Min(z0, z1), Mathf.Max(sx * tdz, sx * (tdz + 22f)), Mathf.Max(z0, z1), PaintWhite);
                    }
            }
            // Dépôts de gomme (traînées sombres) dans les zones de toucher.
            for (var i = 0; i < 14; i++)
            {
                var x = sx * (AimPoint + 20f + i * 18f + RuinKit.Hash(i, sx, 83) * 10f);
                var z = mid + (RuinKit.Hash(i, sx, 84) - 0.5f) * 10f;
                Decal(c, Mats.Decal.Ash, new Vector3(x, 0, z), 2.6f, Mathf.Pi / 2, 0.022f + i * 0.0002f, 8f);
            }
        }
        // Lignes de guidage jaunes : axe de la voie, bretelles, vers chaque hangar.
        Stripe(-200f, TaxiZ - 0.2f, 200f, TaxiZ + 0.2f, PaintYellow);
        foreach (var x in new[] { -150f, 30f, 160f })
            Stripe(x - 0.2f, RunwayZ1, x + 0.2f, TaxiZ, PaintYellow);
        foreach (var x in new[] { -150f, -92f, -60f, 150f })
            Stripe(x - 0.15f, TaxiZ, x + 0.15f, ApronZ0 + 6f, PaintYellow);
        // Bord du tarmac, aires de stationnement (cadres jaunes) devant la tour.
        Stripe(-205f, ApronZ0 + 0.3f, 200f, ApronZ0 + 0.6f, PaintYellow);
        for (var i = 0; i < 2; i++)
        {
            var x0 = 72f + i * 22f;
            Stripe(x0, -100f, x0 + 0.3f, -78f, PaintYellow);
            Stripe(x0 + 18f, -100f, x0 + 18.3f, -78f, PaintYellow);
            Stripe(x0, -78.3f, x0 + 18.3f, -78f, PaintYellow);
        }

        // Balises de bord de piste (éteintes), seulement dans l'aire de jeu.
        for (var x = -200f; x <= 200f; x += 50f)
            foreach (var z in new[] { RunwayZ0 - 0.8f, RunwayZ1 + 0.8f })
                c.Props.Add(Props.RunwayLight(), new Vector3(x, 0, z), 0, range: 140f);
        // Fissures et poussière sur le béton.
        for (var i = 0; i < 60; i++)
        {
            var x = (RuinKit.Hash(i, 1, 85) - 0.5f) * 420f;
            var z = Mathf.Lerp(RunwayZ0, ApronZ1, RuinKit.Hash(i, 2, 85));
            Decal(c, i % 2 == 0 ? Mats.Decal.Dust : Mats.Decal.Cracks, new Vector3(x, 0, z), 3f + RuinKit.Hash(i, 3, 85) * 3.5f, RuinKit.Hash(i, 4, 85) * Mathf.Tau, 0.024f + i * 0.0001f, 1.4f);
        }
        // Sable soufflé sur les bords du tarmac et de la piste (langues claires).
        for (var i = 0; i < 46; i++)
        {
            var x = -200f + i * 9f + RuinKit.Hash(i, 5, 85) * 4f;
            var onRunway = i % 2 == 0;
            var z = onRunway ? RunwayZ0 + 2f + RuinKit.Hash(i, 6, 85) * 3f : ApronZ1 - 1f - RuinKit.Hash(i, 6, 85) * 3f;
            Decal(c, Mats.Decal.Dust, new Vector3(x, 0, z), 6f + RuinKit.Hash(i, 7, 85) * 6f, 0.6f + RuinKit.Hash(i, 8, 85), 0.026f + i * 0.0001f, 2f);
        }
    }
}
