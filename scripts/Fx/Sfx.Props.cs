using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Bruitages de destruction du décor : verre brisé, mobilier qui éclate, poteau métallique qui s'abat,
/// grondement d'un immeuble qui cède.
/// </summary>
public partial class Sfx
{
    private static void BuildProps(Dictionary<string, byte[]> snd)
    {
        var rng = new Random(9876);
        snd["glass"] = Make(1.1f, Glass(rng));
        snd["smash"] = Make(0.9f, Smash(rng));
        snd["pole_fall"] = Make(1.6f, PoleFall(rng));
        snd["rumble"] = Make(4f, Rumble(rng));
    }

    /// <summary>Verre qui éclate : impact sec puis pluie de tintements aigus qui s'espacent.</summary>
    private static Func<float, float> Glass(Random rng)
    {
        var hp = new Svf();
        var tinkles = new List<(float at, float hz, float amp)>();
        for (var i = 0; i < 40; i++)
        {
            var at = (float)Math.Pow(rng.NextDouble(), 1.8) * 0.9f;
            tinkles.Add((at, 2600f + (float)rng.NextDouble() * 5200f, 0.05f + (float)rng.NextDouble() * 0.12f * (1f - at)));
        }
        return t =>
        {
            var x = Noise(rng);
            hp.Set(4200f, 0.7f);
            hp.Run(x);
            var crack = (x - hp.Run(x) * 0.6f) * Env(t, 0.001f, 0.09f) * 0.7f;
            var ring = 0f;
            foreach (var (at, hz, amp) in tinkles)
            {
                var d = t - at;
                if (d < 0 || d > 0.12f) continue;
                ring += Mathf.Sin(Mathf.Tau * hz * d) * amp * Mathf.Exp(-d / 0.025f);
            }
            return crack + ring;
        };
    }

    /// <summary>Bois et tôle qui cèdent : craquement, coup sourd, cliquetis des morceaux.</summary>
    private static Func<float, float> Smash(Random rng)
    {
        var lp = new Svf();
        var rattle = 0f;
        return t =>
        {
            var x = Noise(rng);
            lp.Set(500f + 2600f * Mathf.Exp(-t / 0.05f), 0.9f);
            var crunch = lp.Run(x) * 1.2f * Env(t, 0.002f, 0.25f);
            if (rng.NextDouble() < 0.004 * Mathf.Exp(-t / 0.3f)) rattle = 1f;
            rattle *= 0.9f;
            return Thump(t, 85f, 0.08f) * 0.7f + crunch + x * rattle * 0.35f;
        };
    }

    /// <summary>Poteau qui s'abat : grincement métallique bref puis tintement grave et lourd à l'impact.</summary>
    private static Func<float, float> PoleFall(Random rng)
    {
        float[] hz = { 210f, 487f, 911f, 1430f };
        float[] dec = { 0.6f, 0.4f, 0.25f, 0.15f };
        return t =>
        {
            var x = Noise(rng);
            var creak = Mathf.Sin(Mathf.Tau * (180f + 60f * Mathf.Sin(t * 30f)) * t) * 0.18f * Env(t, 0.02f, 0.35f);
            var d = t - 0.45f;
            var clang = 0f;
            if (d > 0)
            {
                for (var i = 0; i < hz.Length; i++) clang += Mathf.Sin(Mathf.Tau * hz[i] * d) * Mathf.Exp(-d / dec[i]) * (0.3f - i * 0.05f);
                clang += Thump(d, 60f, 0.12f) * 0.8f + x * Mathf.Exp(-d / 0.02f) * 0.5f;
            }
            return creak + clang;
        };
    }

    /// <summary>Grondement d'un immeuble qui cède : basses roulantes, craquements de béton épars.</summary>
    private static Func<float, float> Rumble(Random rng)
    {
        var lp = new Svf();
        var crack = 0f;
        return t =>
        {
            var x = Noise(rng);
            lp.Set(90f + 40f * Mathf.Sin(t * 3f), 0.8f);
            var low = lp.Run(x) * 3f;
            if (rng.NextDouble() < 0.0012) crack = 1f;
            crack *= 0.985f;
            var env = Mathf.Min(t / 0.6f, 1f) * Mathf.Clamp((4f - t) / 1.2f, 0f, 1f);
            return (low + x * crack * 0.4f) * env;
        };
    }
}
