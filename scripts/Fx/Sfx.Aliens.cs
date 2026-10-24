using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Sons des aliens : tir de fusil à énergie (claquement électrique qui chute, souffle ionisé), blaster lourd du garde,
/// laser du volant, téléportation (montée de souffle, scintillement, détonation sèche), rugissement de charge, coup de
/// bouclier, impacts sur le bouclier, cris de mort (variantes), poussée du portail, implosion et bourdonnement du portail.
/// </summary>
public partial class Sfx
{
    private static void BuildAliens(Dictionary<string, byte[]> snd, Dictionary<string, byte[][]> vars, Dictionary<string, byte[]> loops)
    {
        var rng = new Random(4242);
        snd["alien_rifle"] = Make(0.55f, EnergyShot(rng, 2400f, 620f, 0.16f, 110f, 0.9f));
        snd["alien_heavy"] = Make(1.1f, EnergyShot(rng, 1300f, 260f, 0.32f, 60f, 1.2f));
        snd["alien_laser"] = Make(0.4f, EnergyShot(rng, 4200f, 1500f, 0.09f, 180f, 0.6f));
        snd["alien_warp"] = Make(1.3f, Warp(rng));
        snd["alien_roar"] = Make(1.5f, Roar(rng, 1.4f, 62f));
        snd["alien_bash"] = Make(1.0f, Bash(rng));
        snd["shield_hit"] = Make(0.35f, ShieldHit(rng));
        snd["portal_surge"] = Make(2.2f, Surge(rng));
        snd["portal_collapse"] = Make(3.5f, Collapse(rng));
        vars["alien_death"] = Enumerable.Range(0, 3).Select(i => Make(0.9f + 0.15f * i, DeathCry(rng, i))).ToArray();
        snd["alien_death"] = vars["alien_death"][0];
        loops["portal_hum"] = MakeLoop(2f, PortalHum(rng, 2f), 0.8f);
    }

    /// <summary>
    /// Trait d'énergie : claquement d'arc (bruit très bref), sifflement dont la hauteur chute de f0 à f1, battement
    /// ionisé (deux scies désaccordées), coup sourd et queue de souffle.
    /// </summary>
    private static Func<float, float> EnergyShot(Random rng, float f0, float f1, float len, float thumpHz, float gain)
    {
        var bp = new Svf();
        var lp = new Svf();
        var ph = 0f;
        var ph2 = 0f;
        return t =>
        {
            var x = Noise(rng);
            var k = Mathf.Clamp(t / len, 0f, 1f);
            var f = Mathf.Lerp(f0, f1, 1f - (1f - k) * (1f - k));
            ph += f / Rate;
            ph2 += f * 0.503f / Rate;
            var zap = (Mathf.Sin(ph * Mathf.Tau) * 0.45f + Saw(ph2) * 0.3f) * Env(t, 0.002f, len);
            bp.Set(f * 1.3f, 1.4f);
            bp.Run(x);
            lp.Set(300f + 2500f * Mathf.Exp(-t / 0.05f), 0.7f);
            var hiss = lp.Run(x) * 0.7f * Env(t, 0.01f, len * 2.2f);
            return (x * Mathf.Exp(-t / 0.003f) * 0.6f + zap + bp.Band * 0.6f * Env(t, 0.003f, len * 0.8f) + Thump(t, thumpHz, 0.06f) * 0.7f + hiss) * gain;
        };
    }

    /// <summary>Téléportation : souffle qui monte et scintille, puis détonation sèche et coup grave.</summary>
    private static Func<float, float> Warp(Random rng)
    {
        var bp = new Svf();
        var ph = 0f;
        return t =>
        {
            var x = Noise(rng);
            var rise = Mathf.Clamp(t / 0.7f, 0f, 1f);
            bp.Set(300f + 3200f * rise * rise, 3f);
            bp.Run(x);
            ph += (400f + 1800f * rise) / Rate;
            var shimmer = Mathf.Sin(ph * Mathf.Tau) * Mathf.Sin(t * 90f) * 0.25f * rise;
            var build = (bp.Band * 0.9f + shimmer) * (t < 0.7f ? rise : Mathf.Exp(-(t - 0.7f) / 0.03f));
            var pop = t > 0.7f ? x * Mathf.Exp(-(t - 0.7f) / 0.02f) * 0.9f + Thump(t - 0.7f, 70f, 0.15f) * 1.2f : 0f;
            return build + pop;
        };
    }

    /// <summary>Rugissement guttural : source glottale grave à vibrato irrégulier, deux formants, souffle râpeux.</summary>
    private static Func<float, float> Roar(Random rng, float len, float pitch)
    {
        var f1 = new Svf();
        var f2 = new Svf();
        var rasp = new Svf();
        rasp.Set(1800f, 0.8f);
        var ph = 0f;
        return t =>
        {
            var x = Noise(rng);
            var p = pitch * (1f + 0.25f * Mathf.Sin(t * 3.1f) + 0.06f * Mathf.Sin(t * 23f)) * (t < 0.2f ? 0.8f + t : 1f);
            ph += p / Rate;
            var src = Saw(ph) + x * 0.35f;
            f1.Set(420f + 120f * Mathf.Sin(t * 2f), 5f);
            f2.Set(980f, 6f);
            f1.Run(src);
            f2.Run(src);
            rasp.Run(x);
            var amp = Env(t, 0.08f, len - 0.08f) * (0.8f + 0.2f * Mathf.Sin(t * 41f));
            return (f1.Band * 1.4f + f2.Band * 0.7f + rasp.Band * 0.4f + Mathf.Sin(ph * Mathf.Tau * 0.5f) * 0.3f) * amp;
        };
    }

    /// <summary>Coup de bouclier : choc de métal épais (partiels inharmoniques), coup sourd, raclement.</summary>
    private static Func<float, float> Bash(Random rng)
    {
        float[] hz = { 210f, 377f, 590f, 1130f };
        var lp = new Svf();
        return t =>
        {
            var x = Noise(rng);
            var ring = 0f;
            for (var i = 0; i < hz.Length; i++) ring += Mathf.Sin(Mathf.Tau * hz[i] * t) * Mathf.Exp(-t / (0.25f - i * 0.05f)) * (0.35f - i * 0.06f);
            lp.Set(200f + 2500f * Mathf.Exp(-t / 0.04f), 0.8f);
            return x * Mathf.Exp(-t / 0.006f) * 0.7f + Thump(t, 55f, 0.18f) * 1.3f + ring + lp.Run(x) * Env(t, 0.005f, 0.5f);
        };
    }

    /// <summary>Balle sur le bouclier : tintement d'énergie (grésillement aigu et note brève).</summary>
    private static Func<float, float> ShieldHit(Random rng)
    {
        var bp = new Svf();
        bp.Set(3200f, 2f);
        return t =>
        {
            var x = Noise(rng);
            bp.Run(x);
            return (bp.Band * 1.2f + Mathf.Sin(Mathf.Tau * 1450f * t) * 0.35f + Mathf.Sin(Mathf.Tau * 2210f * t) * 0.2f) * Env(t, 0.001f, 0.3f) + x * Mathf.Exp(-t / 0.004f) * 0.5f;
        };
    }

    /// <summary>Poussée du portail (passage d'un groupe) : grondement qui enfle, souffle, battement grave.</summary>
    private static Func<float, float> Surge(Random rng)
    {
        var lp = new Svf();
        var bp = new Svf();
        return t =>
        {
            var x = Noise(rng);
            lp.Set(120f + 200f * Mathf.Sin(Mathf.Min(t / 1.2f, 1f) * Mathf.Pi), 0.8f);
            bp.Set(700f + 500f * Mathf.Sin(t * 2.5f), 2f);
            bp.Run(x);
            var swell = Mathf.Sin(Mathf.Min(t / 2.1f, 1f) * Mathf.Pi);
            return (lp.Run(x) * 2.2f + bp.Band * 0.5f + Mathf.Sin(Mathf.Tau * 41f * t) * 0.5f * Mathf.Sin(t * 9f)) * swell;
        };
    }

    /// <summary>Effondrement du portail : aspiration (souffle qui monte), silence bref, implosion grave et grondement.</summary>
    private static Func<float, float> Collapse(Random rng)
    {
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(160f, 0.7f);
        return t =>
        {
            var x = Noise(rng);
            var k = Mathf.Clamp(t / 0.9f, 0f, 1f);
            bp.Set(200f + 3000f * k * k, 2f);
            bp.Run(x);
            var suck = t < 0.9f ? bp.Band * k * 1.1f : 0f;
            var boom = t > 1f ? Thump(t - 1f, 38f, 0.5f) * 1.6f + x * Mathf.Exp(-(t - 1f) / 0.03f) * 0.8f + lp.Run(x) * 2.5f * Env(t - 1f, 0.02f, 2.3f) : 0f;
            return suck + boom;
        };
    }

    /// <summary>Cri de mort : glissando descendant rauque (deux formants), gargouillis final.</summary>
    private static Func<float, float> DeathCry(Random rng, int variant)
    {
        var f1 = new Svf();
        var f2 = new Svf();
        var gurgle = new Svf();
        var ph = 0f;
        var len = 0.85f + 0.15f * variant;
        var p0 = 210f - variant * 35f;
        return t =>
        {
            var x = Noise(rng);
            var k = Mathf.Clamp(t / len, 0f, 1f);
            ph += p0 * (1f - 0.55f * k) * (1f + 0.05f * Mathf.Sin(t * 37f)) / Rate;
            var src = Saw(ph) + x * 0.4f;
            f1.Set(650f - 250f * k + variant * 60f, 4f);
            f2.Set(1500f - 400f * k, 5f);
            f1.Run(src);
            f2.Run(src);
            gurgle.Set(300f + 150f * Mathf.Sin(t * 60f), 3f);
            gurgle.Run(x);
            var g = k > 0.6f ? gurgle.Band * (k - 0.6f) * 2.5f : 0f;
            return (f1.Band * 1.2f + f2.Band * 0.6f + g) * Env(t, 0.03f, len);
        };
    }

    /// <summary>Bourdonnement du portail : grave pulsé (raies entières sur la boucle), souffle tournoyant, grésillement.</summary>
    private static Func<int, float> PortalHum(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        var bp = new Svf();
        var lp = new Svf();
        lp.Set(140f, 0.7f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = noise[j];
            // Fréquences entières de cycles par boucle (2 s) : 36 Hz, 54 Hz, 73,5 Hz ; balayage du souffle à 1 Hz.
            var hum = Mathf.Sin(Mathf.Tau * 36f * t) * 0.5f + Mathf.Sin(Mathf.Tau * 54f * t) * 0.3f + Mathf.Sin(Mathf.Tau * 73.5f * t) * 0.2f;
            bp.Set(500f + 350f * Mathf.Sin(Mathf.Tau * 1f * t), 2.5f);
            bp.Run(x);
            var crackle = x * x * x * 0.25f * (0.5f + 0.5f * Mathf.Sin(Mathf.Tau * 4f * t));
            return hum * (0.8f + 0.2f * Mathf.Sin(Mathf.Tau * 2f * t)) + bp.Band * 0.6f + lp.Run(x) * 1.2f + crackle;
        };
    }
}
