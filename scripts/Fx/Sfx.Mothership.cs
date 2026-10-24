using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sons du vaisseau-mère : bourdonnement de coque (boucle d'infrabasses battantes), corne d'arrivée et de phase (cuivre
/// grave saturé à deux notes), ouverture et fermeture de la couronne (raclement, vérins, chocs), charge du canon
/// (sifflement qui monte, crépitements), décharge (coup de bélier et souffle), colonne d'énergie (boucle rugissante et
/// bourdonnante), lancement des soutes, agonie (gémissement de métal, explosions en chapelet), écrasement au loin.
/// </summary>
public partial class Sfx
{
    private static void BuildMothership(Dictionary<string, byte[]> snd, Dictionary<string, byte[]> loops)
    {
        var rng = new Random(2026);
        loops["ms_hum"] = MakeLoop(4f, HumLoop(rng, 4f), 0.85f);
        loops["ms_beam"] = MakeLoop(3f, BeamLoop(rng, 3f), 0.9f);
        snd["ms_horn"] = Make(6.5f, Horn(rng));
        snd["ms_open"] = Make(3.2f, IrisMove(rng, true));
        snd["ms_close"] = Make(2.8f, IrisMove(rng, false));
        snd["ms_charge"] = Make(7.8f, Charge(rng, 7.5f));
        snd["ms_fire"] = Make(3.5f, Discharge(rng));
        snd["ms_launch"] = Make(3f, Launch(rng));
        snd["ms_death"] = Make(8f, Agony(rng));
        snd["ms_crash"] = Make(9f, ShipCrash(rng));
    }

    /// <summary>
    /// Bourdonnement de la coque : fondamentale de 32 Hz et partiels qui battent lentement, souffle grave modulé ; nombre
    /// entier de cycles sur la boucle (aucune couture).
    /// </summary>
    private static Func<int, float> HumLoop(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var table = NoiseTable(rng, n);
        var lp = new Svf();
        lp.Set(90f, 0.8f);
        var mid = new Svf();
        mid.Set(260f, 2.5f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            // Fréquences arrondies à un nombre entier de cycles sur 4 s.
            var f1 = 32f; var f2 = 32.5f; var f3 = 48.25f; var f4 = 64f;
            var w = Mathf.Sin(Mathf.Tau * f1 * t) * 1f + Mathf.Sin(Mathf.Tau * f2 * t) * 0.8f + Mathf.Sin(Mathf.Tau * f3 * t) * 0.35f + Saw(f4 * t) * 0.18f;
            var swell = 0.75f + 0.25f * Mathf.Sin(Mathf.Tau * t / seconds);
            var x = table[j];
            var rumble = lp.Run(x) * 2.4f;
            mid.Run(x);
            return (w * 0.55f + rumble + mid.Band * 0.25f) * swell;
        };
    }

    /// <summary>Colonne d'énergie : grondement de plasma, bourdonnement à 110 Hz en dents de scie, crépitements.</summary>
    private static Func<int, float> BeamLoop(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var table = NoiseTable(rng, n);
        var lp = new Svf();
        lp.Set(180f, 0.7f);
        var bp = new Svf();
        bp.Set(1400f, 1.2f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = table[j];
            var roar = lp.Run(x) * 3.2f;
            bp.Run(x);
            var buzz = Saw(110f * t) * 0.35f + Saw(165f * t) * 0.2f + Mathf.Sin(Mathf.Tau * 55f * t) * 0.4f;
            var am = 0.75f + 0.25f * Mathf.Sin(Mathf.Tau * 12f * t / seconds * 3f);
            var crack = Mathf.Abs(table[(j * 13 + 7) % n]) > 0.998f ? table[(j * 5) % n] * 2.5f : 0f;
            return (roar + buzz * am + bp.Band * 0.6f + crack) * 0.8f;
        };
    }

    /// <summary>Corne : deux notes graves (la, puis ré) en dents de scie saturées, formants de cuivre, sous-basse.</summary>
    private static Func<float, float> Horn(Random rng)
    {
        var ph = 0f;
        var fa = new Svf();
        var fb = new Svf();
        fa.Set(420f, 3f);
        fb.Set(1150f, 4f);
        var lp = new Svf();
        lp.Set(60f, 0.7f);
        return t =>
        {
            var f = t < 2.6f ? 55f : Mathf.Lerp(55f, 41.2f, Mathf.SmoothStep(2.6f, 3.1f, t));
            f *= 1f + 0.006f * Mathf.Sin(t * 5.5f);
            ph += f / Rate;
            var src = Saw(ph) * 0.8f + Saw(ph * 1.005f) * 0.6f + Saw(ph * 2f) * 0.3f;
            fa.Run(src);
            fb.Run(src);
            var v = MathF.Tanh((fa.Band * 2.4f + fb.Band * 0.9f + src * 0.4f) * 1.6f);
            var sub = Mathf.Sin(Mathf.Tau * ph * 0.5f) * 0.6f + lp.Run(Noise(rng)) * 1.5f;
            var env = Mathf.Min(t / 0.6f, 1f) * (t > 5.4f ? Mathf.Max(0f, 1f - (t - 5.4f) / 1.1f) : 1f);
            return (v + sub) * env;
        };
    }

    /// <summary>Couronne qui s'ouvre ou se ferme : raclement de métal, sifflement de vérins, chocs aux butées.</summary>
    private static Func<float, float> IrisMove(Random rng, bool open)
    {
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(120f, 0.8f);
        var len = open ? 2.6f : 2.2f;
        return t =>
        {
            var n = Noise(rng);
            var k = Mathf.Clamp(t / len, 0f, 1f);
            bp.Set(300f + 900f * (open ? k : 1f - k), 6f);
            bp.Run(n);
            var grind = bp.Band * 1.8f * Mathf.Sin(k * Mathf.Pi) * (0.7f + 0.3f * Mathf.Sin(t * 47f));
            var hiss = n * 0.12f * Env(t, 0.1f, len);
            var rumble = lp.Run(n) * 2.6f * Mathf.Sin(k * Mathf.Pi);
            var clunks = Thump(t, 50f, 0.25f) * 1.2f + (t > len ? Thump(t - len, 38f, 0.4f) * 1.6f + n * Mathf.Exp(-(t - len) / 0.02f) : 0f);
            return grind + hiss + rumble + clunks;
        };
    }

    /// <summary>Charge du canon : sifflement qui monte (harmoniques qui se resserrent), grondement qui enfle, arcs électriques.</summary>
    private static Func<float, float> Charge(Random rng, float len)
    {
        var ph = 0f;
        var lp = new Svf();
        lp.Set(70f, 0.7f);
        var bp = new Svf();
        return t =>
        {
            var k = Mathf.Clamp(t / len, 0f, 1f);
            var f = 70f + 820f * k * k;
            ph += f / Rate;
            var tone = Mathf.Sin(Mathf.Tau * ph) * 0.4f + Mathf.Sin(Mathf.Tau * ph * 1.5f) * 0.18f * k + Saw(ph * 0.5f) * 0.12f;
            var n = Noise(rng);
            bp.Set(500f + 3000f * k, 2f);
            bp.Run(n);
            var arcs = rng.NextDouble() < 0.0008 + 0.006 * k ? n * 1.6f : 0f;
            var rumble = lp.Run(n) * (1.5f + 3f * k);
            var env = Mathf.Min(t / 0.4f, 1f) * (t > len ? Mathf.Exp(-(t - len) / 0.05f) : 1f);
            return (tone * (0.4f + 0.8f * k) + bp.Band * 0.4f * k + arcs + rumble) * env;
        };
    }

    /// <summary>Décharge : coup de bélier infrasonore, claquement, souffle qui s'ouvre.</summary>
    private static Func<float, float> Discharge(Random rng)
    {
        var lp = new Svf();
        return t =>
        {
            var n = Noise(rng);
            lp.Set(200f + 4000f * Mathf.Exp(-t / 0.15f), 0.8f);
            return Thump(t, 24f, 0.9f) * 2f + Thump(t, 60f, 0.2f) * 1.2f + lp.Run(n) * 2f * Env(t, 0.005f, 2.6f) + n * Mathf.Exp(-t / 0.01f) * 1.2f;
        };
    }

    /// <summary>Lancement : portes de soute (choc, raclement), puis souffle des réacteurs qui s'éloigne.</summary>
    private static Func<float, float> Launch(Random rng)
    {
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(150f, 0.8f);
        return t =>
        {
            var n = Noise(rng);
            var k = Mathf.Clamp((t - 0.8f) / 2f, 0f, 1f);
            bp.Set(900f - 600f * k, 1.4f);
            bp.Run(n);
            var whoosh = t > 0.8f ? bp.Band * 1.5f * Mathf.Sin(k * Mathf.Pi) : 0f;
            return Thump(t, 45f, 0.3f) * 1.2f + lp.Run(n) * 1.4f * Env(t, 0.05f, 0.8f) + whoosh;
        };
    }

    /// <summary>Agonie : gémissement de métal qui se tord (glissando grave), explosions en chapelet, craquements.</summary>
    private static Func<float, float> Agony(Random rng)
    {
        var ph = 0f;
        var fa = new Svf();
        fa.Set(320f, 5f);
        var lp = new Svf();
        lp.Set(80f, 0.7f);
        var booms = new float[9];
        for (var i = 0; i < booms.Length; i++) booms[i] = 0.3f + (float)rng.NextDouble() * 6.5f;
        return t =>
        {
            var f = 90f - 55f * Mathf.Clamp(t / 6f, 0f, 1f) + 6f * Mathf.Sin(t * 1.7f);
            ph += f / Rate;
            var n = Noise(rng);
            fa.Run(Saw(ph) + n * 0.3f);
            var groan = fa.Band * 1.6f * Env(t, 0.4f, 7f);
            var b = 0f;
            foreach (var bt in booms)
                if (t > bt) b += Thump(t - bt, 32f, 0.5f) * 0.9f + n * Mathf.Exp(-(t - bt) / 0.03f) * 0.5f;
            return groan + b + lp.Run(n) * 2.2f * Env(t, 0.2f, 7.5f);
        };
    }

    /// <summary>Écrasement au loin : choc sourd énorme, grondement qui roule longtemps, échos.</summary>
    private static Func<float, float> ShipCrash(Random rng)
    {
        var lp = new Svf();
        lp.Set(60f, 0.6f);
        var lp2 = new Svf();
        lp2.Set(260f, 0.7f);
        return t =>
        {
            var n = Noise(rng);
            var roll = lp.Run(n) * 4f * Env(t, 0.08f, 8.5f);
            var body = lp2.Run(n) * 1.6f * Env(t, 0.02f, 3f);
            var echo = t > 1.4f ? Thump(t - 1.4f, 22f, 1.1f) * 0.8f : 0f;
            return Thump(t, 18f, 1.6f) * 2.2f + Thump(t, 40f, 0.5f) * 1.2f + roll + body + echo;
        };
    }
}
