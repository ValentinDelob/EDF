using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sons des machines extraterrestres : pas lourd (impact sourd, tôle qui résonne, sifflement hydraulique), charge et tir
/// du canon du Marcheur, charge et rugissement du rayon du Tripode, portes de soute, grondement des moteurs du vaisseau.
/// </summary>
public partial class Sfx
{
    private static void BuildMachines(Dictionary<string, byte[]> snd, Dictionary<string, byte[]> loops)
    {
        var rng = new Random(777);
        snd["mech_step"] = Make(1.4f, MechStep(rng));
        snd["cannon_charge"] = Make(1.4f, ChargeWhine(rng, 1.3f, 180f, 900f));
        snd["mech_cannon"] = Make(2.2f, MechCannon(rng));
        snd["beam_charge"] = Make(1.9f, ChargeWhine(rng, 1.8f, 90f, 520f));
        snd["beam_fire"] = Make(2.8f, BeamRoar(rng, 2.6f));
        snd["hatch"] = Make(2.6f, Hatch(rng));
        loops["ship_engine"] = MakeLoop(2f, ShipEngineGen(rng, 2f), 0.85f);
    }

    /// <summary>Pas d'une machine : coup sourd très grave, choc de tôle (partiels inharmoniques), sifflement hydraulique.</summary>
    private static Func<float, float> MechStep(Random rng)
    {
        float[] hz = { 142f, 311f, 497f, 803f };
        float[] dec = { 0.35f, 0.22f, 0.15f, 0.09f };
        var lp = new Svf();
        var hiss = new Svf();
        hiss.Set(3200f, 0.9f);
        return t =>
        {
            var x = Noise(rng);
            lp.Set(120f + 900f * Mathf.Exp(-t / 0.05f), 0.7f);
            var body = lp.Run(x) * 1.6f * Env(t, 0.003f, 0.6f);
            var ring = 0f;
            for (var i = 0; i < hz.Length; i++) ring += Mathf.Sin(Mathf.Tau * hz[i] * t) * Mathf.Exp(-t / dec[i]) * (0.3f - i * 0.05f);
            hiss.Run(x);
            var h = t > 0.25f ? hiss.Band * 0.35f * Env(t - 0.25f, 0.05f, 0.7f) : 0f;
            return Thump(t, 38f, 0.3f) * 1.3f + Thump(t, 70f, 0.12f) * 0.6f + body + ring + h;
        };
    }

    /// <summary>Charge d'énergie : gémissement qui monte de f0 à f1 en <paramref name="len"/> s, battement et grésillement croissants.</summary>
    private static Func<float, float> ChargeWhine(Random rng, float len, float f0, float f1)
    {
        var ph = 0f;
        var ph2 = 0f;
        var bp = new Svf();
        bp.Set(1800f, 1.2f);
        return t =>
        {
            var k = Mathf.Clamp(t / len, 0f, 1f);
            var f = Mathf.Lerp(f0, f1, k * k);
            ph += f / Rate;
            ph2 += f * 1.505f / Rate;
            bp.Run(Noise(rng));
            var tone = Mathf.Sin(ph * Mathf.Tau) * 0.45f + Saw(ph2) * 0.18f * k;
            var crackle = bp.Band * (0.15f + 0.6f * k) * (0.6f + 0.4f * Mathf.Sin(t * 70f));
            var env = Mathf.Min(t / 0.15f, 1f) * (t > len ? Mathf.Exp(-(t - len) / 0.04f) : 1f);
            return (tone * (0.3f + 0.7f * k) + crackle) * env;
        };
    }

    /// <summary>Canon lourd : détonation (claquement + souffle), coup sourd profond, queue grondante qui roule dans les rues.</summary>
    private static Func<float, float> MechCannon(Random rng)
    {
        var lp = new Svf();
        var tail = new Svf();
        tail.Set(180f, 0.6f);
        return t =>
        {
            var x = Noise(rng);
            lp.Set(400f + 5000f * Mathf.Exp(-t / 0.03f), 0.8f);
            var blast = lp.Run(x) * 1.8f * Env(t, 0.001f, 0.35f);
            var rumble = tail.Run(x) * 2.2f * Env(t, 0.02f, 2.0f);
            return x * Mathf.Exp(-t / 0.004f) * 0.9f + Thump(t, 48f, 0.35f) * 1.4f + Thump(t, 95f, 0.08f) * 0.7f + blast + rumble;
        };
    }

    /// <summary>Rayon de destruction : rugissement électrique (scies désaccordées), friture d'arcs, grondement, fin en chute.</summary>
    private static Func<float, float> BeamRoar(Random rng, float len)
    {
        var bp = new Svf();
        bp.Set(2400f, 0.9f);
        var low = new Svf();
        low.Set(90f, 0.7f);
        var arc = 0f;
        return t =>
        {
            var x = Noise(rng);
            bp.Run(x);
            var drop = t > len ? Mathf.Exp(-(t - len) / 0.08f) : 1f;
            var f = 92f * (t > len ? 1f - (t - len) * 2f : 1f);
            var saw = Saw(t * f) * 0.35f + Saw(t * f * 1.01f) * 0.3f + Saw(t * f * 2.02f) * 0.15f;
            if (rng.NextDouble() < 0.0025) arc = 1f;
            arc *= 0.995f;
            var mod = 0.75f + 0.25f * Mathf.Sin(t * 37f) * Mathf.Sin(t * 5.3f);
            return (saw * mod + bp.Band * (0.4f + arc * 0.9f) + low.Run(x) * 1.6f) * Mathf.Min(t / 0.04f, 1f) * drop;
        };
    }

    /// <summary>Portes de soute : déverrouillage (clac), sifflement de vérins, roulement de tôle, butée finale.</summary>
    private static Func<float, float> Hatch(Random rng)
    {
        var hiss = new Svf();
        hiss.Set(2600f, 1f);
        var groan = new Svf();
        return t =>
        {
            var x = Noise(rng);
            hiss.Run(x);
            groan.Set(160f + 60f * Mathf.Sin(t * 3f), 3f);
            var g = groan.Run(x) * 1.2f * Env(t, 0.3f, 2.2f);
            var clack = x * Mathf.Exp(-t / 0.01f) * 0.9f + Thump(t, 90f, 0.08f) * 0.6f;
            var stop = t > 2.1f ? Thump(t - 2.1f, 60f, 0.2f) * 1.1f + x * Mathf.Exp(-(t - 2.1f) / 0.02f) * 0.5f : 0f;
            return clack + hiss.Band * 0.5f * Env(t, 0.05f, 1.6f) + g + stop;
        };
    }

    /// <summary>Moteurs du vaisseau : grondement grave pulsé (raies entières sur la boucle), souffle des tuyères, sifflement aigu.</summary>
    private static Func<int, float> ShipEngineGen(Random rng, float seconds)
    {
        var n = (int)(seconds * Rate);
        var noise = NoiseTable(rng, n);
        var roar = new Svf();
        roar.Set(110f, 0.7f);
        var hiss = new Svf();
        hiss.Set(1700f, 0.8f);
        return i =>
        {
            var j = i % n;
            var t = j / (float)Rate;
            var x = noise[j];
            // Fréquences entières de cycles par boucle (2 s) : 31 Hz, 46,5 Hz, 62 Hz ; pulsation à 3 Hz.
            var hum = Mathf.Sin(Mathf.Tau * 31f * t) * 0.5f + Mathf.Sin(Mathf.Tau * 46.5f * t) * 0.3f + Mathf.Sin(Mathf.Tau * 62f * t) * 0.25f;
            var pulse = 0.75f + 0.25f * Mathf.Sin(Mathf.Tau * 3f * t);
            hiss.Run(x);
            var whine = Mathf.Sin(Mathf.Tau * 880f * t) * 0.04f + Mathf.Sin(Mathf.Tau * 1321.5f * t) * 0.025f;
            return hum * pulse + roar.Run(x) * 1.4f + hiss.Band * 0.35f + whine;
        };
    }
}
