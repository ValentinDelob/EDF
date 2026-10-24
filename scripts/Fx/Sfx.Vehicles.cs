using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sons de véhicules : boucles sans couture (rotor, turbine, minigun) et bruitages ponctuels
/// (départ de roquette, alarme du cockpit, choc métallique).
/// </summary>
public partial class Sfx
{
    // Rotor : 4 pales à 4,5 tr/s (270 tr/min, régime d'un hélicoptère d'attaque) → passage de pale à 18 Hz.
    private const float BladePass = 18f;

    private static void BuildVehicles(Dictionary<string, byte[]> snd, Dictionary<string, byte[]> loops)
    {
        var rng = new Random(4321);
        loops["heli_rotor"] = MakeLoop(2f, RotorGen(rng, 2f), 0.9f);
        loops["heli_turbine"] = MakeLoop(1f, TurbineGen(rng, 1f), 0.8f);
        loops["heli_minigun"] = MakeLoop(0.5f, MinigunGen(rng, 0.5f), 0.9f);
        loops["heli_minigun_motor"] = MakeLoop(1f, MotorGen(rng, 1f), 0.7f);
        snd["heli_rocket"] = Make(1.6f, RocketLaunch(rng));
        snd["heli_warn"] = Make(0.42f, Warn);
        snd["vehicle_crash"] = Make(1.5f, Crash(rng));
    }

    /// <summary>
    /// Boucle sans couture : le générateur reçoit un indice d'échantillon et doit être périodique sur la durée
    /// (fréquences entières de cycles par boucle, bruit tiré d'une table de même longueur). On rend deux périodes
    /// et on garde la seconde : les filtres récursifs sont alors en régime établi et la fin rejoint le début.
    /// Normalisée à <paramref name="peak"/> (les niveaux relatifs se règlent au mixage).
    /// </summary>
    private static byte[] MakeLoop(float seconds, Func<int, float> gen, float peak)
    {
        var n = (int)(seconds * Rate);
        var buf = new float[n];
        float lp = 0;
        for (var i = 0; i < 2 * n; i++)
        {
            var v = gen(i);
            lp += (v - lp) * 0.6f;
            if (i >= n) buf[i - n] = lp;
        }
        var max = 0f;
        foreach (var v in buf) max = Math.Max(max, Math.Abs(v));
        var k = max > 0 ? peak / max : 1f;
        for (var i = 0; i < n; i++) buf[i] *= k;
        return FromSamples(buf, 0.99f);
    }

    /// <summary>Bruit blanc périodique (table de la longueur de la boucle).</summary>
    private static float[] NoiseTable(Random rng, int n)
    {
        var t = new float[n];
        for (var i = 0; i < n; i++) t[i] = (float)(rng.NextDouble() * 2 - 1);
        return t;
    }

    /// <summary>
    /// Rotor principal : « whop-whop » = claquement de pale (bruit passe-bande médium à attaque brève) + coup sourd grave
    /// à chaque passage de pale (18 Hz), amplitudes légèrement inégales d'une pale à l'autre (motif par tour),
    /// souffle du rotor (bruit grave modulé) et bourdonnement du rotor de queue (~81 Hz).
    /// </summary>
    private static Func<int, float> RotorGen(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        var slapBp = new Svf();
        slapBp.Set(520f, 0.9f);
        var slapHi = new Svf();
        slapHi.Set(1400f, 1.2f);
        var wash = new Svf();
        wash.Set(260f, 0.6f);
        var tail = new Svf();
        tail.Set(650f, 1.4f);
        float[] blade = { 1f, 0.84f, 0.95f, 0.78f };
        const float tailHz = 81f;
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = noise[j];
            var beat = t * BladePass;
            var k = (int)beat;
            var tau = (beat - k) / BladePass;                         // temps depuis le dernier passage de pale
            var amp = blade[k & 3];
            var env = tau < 0.004f ? tau / 0.004f : Mathf.Exp(-(tau - 0.004f) / 0.02f);
            slapBp.Run(x);
            slapHi.Run(x);
            var slap = (slapBp.Band * 2.6f + slapHi.Band * 0.6f) * env * amp;
            var thump = Mathf.Sin(Mathf.Tau * 64f * tau) * Mathf.Exp(-tau / 0.03f) * amp;
            var washV = wash.Run(x) * 1.6f * (0.75f + 0.25f * env);
            var tph = t * tailHz;
            var saw = 2f * (tph - Mathf.Floor(tph + 0.5f));
            tail.Run(saw + x * 0.3f);
            return slap * 1.0f + thump * 0.75f + washV * 0.55f + tail.Band * 0.12f;
        };
    }

    /// <summary>
    /// Turbine : sifflement du compresseur (raies entières qui battent légèrement, harmoniques), souffle aigu
    /// et grondement de tuyère. Le régime se règle par la hauteur de lecture.
    /// </summary>
    private static Func<int, float> TurbineGen(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        var hiss = new Svf();
        hiss.Set(3800f, 0.8f);
        var roar = new Svf();
        roar.Set(140f, 0.7f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = noise[j];
            var whine = Mathf.Sin(Mathf.Tau * 1210f * t) * 0.32f + Mathf.Sin(Mathf.Tau * 1213f * t) * 0.22f
                      + Mathf.Sin(Mathf.Tau * 2420f * t) * 0.14f + Mathf.Sin(Mathf.Tau * 3631f * t) * 0.06f
                      + Mathf.Sin(Mathf.Tau * 605f * t) * 0.08f;
            hiss.Run(x);
            var r = roar.Run(x);
            return whine * (0.9f + 0.1f * Mathf.Sin(Mathf.Tau * 7f * t)) + hiss.Band * 0.5f + r * 0.9f;
        };
    }

    /// <summary>
    /// Minigun M134 à ~3000 coups/min : 50 détonations/s qui se chevauchent (claquement, corps médium, coup sourd),
    /// petites variations d'un coup à l'autre, traînée grave continue → « brrrrt ».
    /// </summary>
    private static Func<int, float> MinigunGen(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        const float rps = 50f;
        var rounds = (int)(seconds * rps);
        var roundAmp = new float[rounds];
        for (var r = 0; r < rounds; r++) roundAmp[r] = 0.82f + 0.18f * (float)rng.NextDouble();
        var body = new Svf();
        body.Set(1900f, 1.1f);
        var low = new Svf();
        low.Set(220f, 0.7f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = noise[j];
            var b = t * rps;
            var k = (int)b;
            var tau = (b - k) / rps;
            var a = roundAmp[k % rounds];
            body.Run(x);
            var crack = x * Mathf.Exp(-tau / 0.0012f) * 0.7f;
            var bodyV = body.Band * 2.2f * Mathf.Exp(-tau / 0.006f);
            var thump = Mathf.Sin(Mathf.Tau * 150f * tau) * Mathf.Exp(-tau / 0.007f) * 0.8f;
            var tailV = low.Run(x) * 1.1f;
            return (crack + bodyV + thump) * a + tailV * 0.6f;
        };
    }

    /// <summary>Moteur électrique d'entraînement des canons du minigun (gémissement), hauteur = rotation.</summary>
    private static Func<int, float> MotorGen(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        var bp = new Svf();
        bp.Set(2200f, 1.5f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var tone = Mathf.Sin(Mathf.Tau * 330f * t) * 0.5f + Mathf.Sin(Mathf.Tau * 660f * t) * 0.3f + Mathf.Sin(Mathf.Tau * 990f * t) * 0.15f;
            var rattle = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Mathf.Tau * 50f * t), 8f);
            bp.Run(noise[j]);
            return tone + bp.Band * 0.6f * (0.4f + 0.6f * rattle);
        };
    }

    /// <summary>Départ de roquette : allumage, rugissement du moteur-fusée qui s'éloigne (balayage vers le grave), crépitement.</summary>
    private static Func<float, float> RocketLaunch(Random rng)
    {
        var bp = new Svf();
        var lp = new Svf();
        var crackle = 0f;
        return t =>
        {
            var x = Noise(rng);
            bp.Set(600f + 2400f * Mathf.Exp(-t / 0.35f), 1.1f);
            bp.Run(x);
            lp.Set(300f + 900f * Mathf.Exp(-t / 0.5f), 0.7f);
            var body = lp.Run(x);
            var roar = (bp.Band * 1.5f + body * 0.9f) * Env(t, 0.012f, 1.45f);
            if (rng.NextDouble() < 0.004 * Mathf.Exp(-t / 0.5f)) crackle = 1f;
            crackle *= 0.9f;
            var pop = x * Mathf.Exp(-t / 0.006f) * 0.9f + Thump(t, 85f, 0.07f) * 0.8f;
            return roar * 0.85f + pop + x * crackle * 0.4f;
        };
    }

    /// <summary>Alarme du cockpit (« master caution ») : deux bips 1046 / 784 Hz, timbre légèrement carré.</summary>
    private static float Warn(float t)
    {
        float Tone(float hz, float a, float b)
        {
            if (t < a || t > b) return 0f;
            var e = Mathf.Min(Mathf.Min((t - a) / 0.006f, (b - t) / 0.006f), 1f);
            var ph = Mathf.Tau * hz * t;
            return (Mathf.Sin(ph) + Mathf.Sin(ph * 3f) * 0.25f + Mathf.Sin(ph * 5f) * 0.08f) * e;
        }
        return (Tone(1046f, 0f, 0.17f) + Tone(784f, 0.21f, 0.38f)) * 0.42f;
    }

    /// <summary>Choc de véhicule : coup sourd, froissement de tôle (bruit filtré), partiels métalliques inharmoniques, débris.</summary>
    private static Func<float, float> Crash(Random rng)
    {
        var lp = new Svf();
        float[] hz = { 430f, 1033f, 1587f, 2210f, 3120f };
        float[] amp = { 0.32f, 0.28f, 0.22f, 0.16f, 0.1f };
        float[] dec = { 0.5f, 0.36f, 0.27f, 0.2f, 0.12f };
        var rattle = 0f;
        return t =>
        {
            var x = Noise(rng);
            lp.Set(300f + 3500f * Mathf.Exp(-t / 0.06f), 0.8f);
            var crunch = lp.Run(x) * 1.4f * Env(t, 0.002f, 0.35f);
            var ring = 0f;
            for (var i = 0; i < hz.Length; i++) ring += Mathf.Sin(Mathf.Tau * hz[i] * t) * amp[i] * Mathf.Exp(-t / dec[i]);
            if (rng.NextDouble() < 0.0018 * Mathf.Exp(-t / 0.5f)) rattle = 1f;
            rattle *= 0.92f;
            return Thump(t, 58f, 0.14f) * 1.0f + crunch + ring * (t < 0.002f ? t / 0.002f : 1f) + x * rattle * 0.45f
                   + x * Mathf.Exp(-t / 0.004f) * 0.8f;
        };
    }
}
