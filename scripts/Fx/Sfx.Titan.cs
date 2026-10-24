using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sons du titan reptilien : rugissement grave (fondamentale de ~60 Hz en dents de scie, formants qui s'ouvrent,
/// râpe du grognement, saturation), cri de douleur, râle d'agonie, inspiration qui précède le souffle, embrasement et
/// grondement continu du jet de feu (boucle sans couture), pas colossal, balayage de la queue, plaque d'armure qui éclate.
/// </summary>
public partial class Sfx
{
    private static void BuildTitan(Dictionary<string, byte[]> snd, Dictionary<string, byte[]> loops)
    {
        var rng = new Random(1606);
        snd["titan_roar"] = Make(4.6f, Roar(rng, 4.4f, 48f, 74f, 40f, 31f, 1f));
        snd["titan_pain"] = Make(2.4f, Roar(rng, 2.2f, 70f, 118f, 62f, 37f, 1.35f));
        snd["titan_death"] = Make(5.6f, Roar(rng, 5.4f, 62f, 70f, 26f, 23f, 0.8f));
        snd["titan_charge"] = Make(2.2f, Inhale(rng, 1.9f));
        snd["titan_flare"] = Make(1.6f, Flare(rng));
        snd["titan_step"] = Make(2.6f, TitanStep(rng));
        snd["titan_tail"] = Make(1.8f, TailWhoosh(rng));
        snd["titan_plate"] = Make(2.0f, PlateCrack(rng));
        loops["titan_fire"] = MakeLoop(3f, FireLoop(rng, 3f), 0.9f);
    }

    /// <summary>Son ponctuel prêt (variante tirée au hasard), pour les lecteurs propres à un ennemi ; null tant que la synthèse n'est pas finie.</summary>
    public static AudioStreamWav Stream(string name) => I?.Pick(name);

    /// <summary>
    /// Rugissement : hauteur qui monte de <paramref name="f0"/> à <paramref name="f1"/> puis retombe vers <paramref name="f2"/>,
    /// grognement modulé à <paramref name="growl"/> Hz, formants qui s'ouvrent (gueule béante) puis se referment, saturation.
    /// </summary>
    private static Func<float, float> Roar(Random rng, float len, float f0, float f1, float f2, float growl, float bright)
    {
        var ph = 0f;
        var jitter = 0f;
        var fa = new Svf();
        var fb = new Svf();
        var fc = new Svf();
        var body = new Svf();
        var rough = new Svf();
        rough.Set(60f, 0.7f);
        return t =>
        {
            var k = Mathf.Clamp(t / len, 0f, 1f);
            var f = k < 0.22f ? Mathf.Lerp(f0, f1, Mathf.SmoothStep(0, 1, k / 0.22f)) : Mathf.Lerp(f1, f2, Mathf.SmoothStep(0, 1, (k - 0.22f) / 0.78f));
            jitter += (Noise(rng) * 0.5f - jitter) * 0.002f;
            f *= 1f + 0.035f * Mathf.Sin(t * 5.1f) + jitter * 0.4f;
            ph += f / Rate;
            var n = Noise(rng);
            rough.Run(n);
            // Râpe : modulation d'amplitude irrégulière (cordes vocales qui claquent).
            var am = 0.6f + 0.4f * Mathf.Sin(t * Mathf.Tau * growl + rough.Band * 40f);
            var src = (Saw(ph) * 0.7f + Saw(ph * 2.003f) * 0.28f + Saw(ph * 0.5f) * 0.25f) * am + n * 0.32f * bright * (0.6f + 0.4f * am);
            var open = Mathf.Sin(Mathf.Min(k * 1.6f, 1f) * Mathf.Pi * 0.5f) * (1f - Mathf.SmoothStep(0.75f, 1f, k) * 0.6f);
            fa.Set(320f + 330f * open, 3f);
            fb.Set(850f + 450f * open, 4f);
            fc.Set(2300f * bright, 5f);
            body.Set(240f, 0.7f);
            fa.Run(src);
            fb.Run(src);
            fc.Run(src);
            var low = body.Run(src);
            var v = fa.Band * 1.5f + fb.Band * 0.95f + fc.Band * 0.35f * bright + low * 0.8f + Mathf.Sin(ph * Mathf.Pi) * 0.45f;
            v = MathF.Tanh(v * 1.9f);
            var env = Mathf.Min(t / 0.28f, 1f) * (k > 0.8f ? Mathf.Pow(1f - (k - 0.8f) / 0.2f, 1.6f) : 1f);
            return v * env * 0.95f;
        };
    }

    /// <summary>Inspiration avant le souffle : souffle qui monte en fréquence, grondement qui enfle, braises qui crépitent.</summary>
    private static Func<float, float> Inhale(Random rng, float len)
    {
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(70f, 0.7f);
        return t =>
        {
            var k = Mathf.Clamp(t / len, 0f, 1f);
            var n = Noise(rng);
            bp.Set(300f + 1500f * k * k, 1.6f);
            bp.Run(n);
            var air = bp.Band * (0.25f + 0.9f * k);
            var rumble = lp.Run(n) * 3f * (0.3f + k);
            var crackle = rng.NextDouble() < 0.0015 + 0.01 * k ? Noise(rng) * 1.2f : 0f;
            var env = Mathf.Min(t / 0.2f, 1f) * (t > len ? Mathf.Exp(-(t - len) / 0.05f) : 1f);
            return (air + rumble + crackle + Thump(t, 30f, 1.2f) * 0.3f) * env;
        };
    }

    /// <summary>Embrasement du jet : coup de bélier sourd, souffle qui s'ouvre d'un coup.</summary>
    private static Func<float, float> Flare(Random rng)
    {
        var lp = new Svf();
        return t =>
        {
            var n = Noise(rng);
            lp.Set(300f + 3000f * Mathf.Exp(-t / 0.25f), 0.8f);
            return lp.Run(n) * 1.6f * Env(t, 0.03f, 1.4f) + Thump(t, 42f, 0.35f) * 1.2f;
        };
    }

    /// <summary>Pas colossal : choc infrasonore, grondement qui roule, gravats qui crépitent.</summary>
    private static Func<float, float> TitanStep(Random rng)
    {
        var lp = new Svf();
        var rumble = new Svf();
        rumble.Set(90f, 0.6f);
        return t =>
        {
            var n = Noise(rng);
            lp.Set(80f + 1200f * Mathf.Exp(-t / 0.04f), 0.7f);
            var hit = lp.Run(n) * 1.8f * Env(t, 0.002f, 0.5f);
            var roll = rumble.Run(n) * 3.2f * Env(t, 0.05f, 2.4f);
            var debris = t > 0.12f && rng.NextDouble() < 0.004 * Math.Exp(-(t - 0.12f) / 0.6f) ? Noise(rng) * 0.9f : 0f;
            return Thump(t, 27f, 0.55f) * 1.7f + Thump(t, 52f, 0.18f) * 0.8f + hit + roll + debris;
        };
    }

    /// <summary>Queue qui fend l'air : souffle passe-bande qui balaie le grave puis le médium.</summary>
    private static Func<float, float> TailWhoosh(Random rng)
    {
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(120f, 0.7f);
        return t =>
        {
            var k = Mathf.Clamp(t / 1.5f, 0f, 1f);
            var n = Noise(rng);
            var sweep = Mathf.Sin(k * Mathf.Pi);
            bp.Set(180f + 700f * sweep, 1.3f);
            bp.Run(n);
            return (bp.Band * 1.4f + lp.Run(n) * 2f) * sweep * sweep;
        };
    }

    /// <summary>Plaque qui éclate : craquement d'os sec, fissures en chapelet, chute sourde.</summary>
    private static Func<float, float> PlateCrack(Random rng)
    {
        var hp = new Svf();
        hp.Set(2400f, 0.8f);
        var cracks = new float[7];
        for (var i = 0; i < cracks.Length; i++) cracks[i] = (float)(rng.NextDouble() * 0.45);
        return t =>
        {
            var n = Noise(rng);
            hp.Run(n);
            var crack = n * Mathf.Exp(-t / 0.008f) * 1.4f;
            foreach (var c in cracks)
                if (t > c) crack += hp.Band * Mathf.Exp(-(t - c) / 0.012f) * 0.9f;
            return crack + Thump(t - 0.05f > 0 ? t - 0.05f : 0f, 45f, 0.3f) * 1.2f + Thump(t, 120f, 0.06f) * 0.6f;
        };
    }

    /// <summary>
    /// Jet de feu (boucle) : grondement de flamme (bruit grave turbulent), souffle médium, crépitements ; modulations à
    /// nombre entier de cycles sur la boucle (aucune couture).
    /// </summary>
    private static Func<int, float> FireLoop(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var table = NoiseTable(rng, n);
        var lp = new Svf();
        lp.Set(160f, 0.7f);
        var mid = new Svf();
        mid.Set(700f, 0.9f);
        var hi = new Svf();
        hi.Set(3200f, 0.8f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = table[j];
            var turb = 0.7f + 0.18f * Mathf.Sin(Mathf.Tau * t * 3f / seconds * 4f) + 0.12f * Mathf.Sin(Mathf.Tau * t * 7f / seconds + 1.3f);
            var roar = lp.Run(x) * 3.2f * turb;
            mid.Run(x);
            hi.Run(x);
            var crack = Mathf.Abs(table[(j * 7 + 13) % n]) > 0.9985f ? table[(j * 3) % n] * 3f : 0f;
            return roar + mid.Band * 1.1f * turb + hi.Band * 0.25f + crack;
        };
    }
}
