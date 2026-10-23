using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sons synthétisés au démarrage (aucun fichier audio externe) et lecture 2D/3D par pool de lecteurs.
/// </summary>
public partial class Sfx : Node
{
    public static Sfx I { get; private set; }

    private const int Rate = 22050;
    private readonly Dictionary<string, AudioStreamWav> _sounds = new();
    private readonly List<AudioStreamPlayer3D> _pool3D = new();
    private readonly List<AudioStreamPlayer> _pool2D = new();
    private int _next3D, _next2D;
    private readonly Dictionary<string, ulong> _lastPlayed = new();

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        var rng = new Random(1234);
        _sounds["rifle"] = Make(0.22f, t => Shot(rng, t, 0.035f, 1800f, 0.7f) + Thump(t, 120f, 0.04f) * 0.6f);
        _sounds["smg"] = Make(0.16f, t => Shot(rng, t, 0.025f, 2400f, 0.55f));
        _sounds["shotgun"] = Make(0.45f, t => Shot(rng, t, 0.08f, 1200f, 0.95f) + Thump(t, 80f, 0.09f));
        _sounds["sniper"] = Make(0.9f, t => Shot(rng, t, 0.12f, 1500f, 1f) + Thump(t, 60f, 0.15f) + Echo(rng, t, 0.35f, 0.25f));
        _sounds["minigun"] = Make(0.12f, t => Shot(rng, t, 0.02f, 2200f, 0.5f) + Thump(t, 150f, 0.02f) * 0.4f);
        _sounds["rocket"] = Make(0.7f, t => Noise(rng) * Env(t, 0.01f, 0.6f) * 0.5f + Thump(t, 90f, 0.1f) * 0.6f);
        _sounds["launcher"] = Make(0.35f, t => Thump(t, 140f, 0.05f) * 0.8f + Noise(rng) * Env(t, 0.005f, 0.12f) * 0.4f);
        _sounds["explosion"] = Make(2.2f, t => Boom(rng, t));
        _sounds["explosion_small"] = Make(1.0f, t => Boom(rng, t * 1.8f) * 0.8f);
        _sounds["laser"] = Make(0.25f, t => (Mathf.Sin(t * 2900f + Mathf.Sin(t * 60f) * 8f) * 0.35f + Noise(rng) * 0.08f) * Env(t, 0.005f, 0.22f));
        _sounds["zap"] = Make(0.35f, t => (Saw(t * (900f - t * 1800f)) * 0.4f + Noise(rng) * 0.35f) * Env(t, 0.002f, 0.3f));
        _sounds["plasma"] = Make(0.6f, t => (Mathf.Sin(t * (500f - 600f * t)) * 0.5f + Noise(rng) * 0.25f) * Env(t, 0.01f, 0.55f));
        _sounds["hammer"] = Make(0.6f, t => Thump(t, 55f, 0.25f) + Noise(rng) * Env(t, 0.002f, 0.12f) * 0.6f);
        _sounds["reload"] = Make(0.3f, t => Click(t, 0.0f) + Click(t, 0.18f) * 1.2f);
        _sounds["empty"] = Make(0.08f, t => Click(t, 0.0f) * 0.7f);
        _sounds["pickup"] = Make(0.25f, t => Mathf.Sin(t * Mathf.Tau * (660f + 900f * t)) * 0.35f * Env(t, 0.005f, 0.22f));
        _sounds["pickup_weapon"] = Make(0.4f, t => (Mathf.Sin(t * Mathf.Tau * 523f) * (t < 0.12f ? 1 : 0) + Mathf.Sin(t * Mathf.Tau * 784f) * (t >= 0.12f ? 1 : 0)) * 0.3f * Env(t, 0.005f, 0.38f));
        _sounds["ui_click"] = Make(0.06f, t => Mathf.Sin(t * Mathf.Tau * 1200f) * 0.25f * Env(t, 0.001f, 0.05f));
        _sounds["ui_hover"] = Make(0.04f, t => Mathf.Sin(t * Mathf.Tau * 1800f) * 0.08f * Env(t, 0.001f, 0.03f));
        _sounds["hit"] = Make(0.1f, t => (Noise(rng) * 0.4f + Mathf.Sin(t * Mathf.Tau * 300f) * 0.3f) * Env(t, 0.001f, 0.08f));
        _sounds["hitmarker"] = Make(0.05f, t => Mathf.Sin(t * Mathf.Tau * 2400f) * 0.2f * Env(t, 0.001f, 0.04f));
        _sounds["screech"] = Make(0.7f, t => (Saw(t * (700f + 200f * Mathf.Sin(t * 30f))) * 0.25f + Noise(rng) * 0.12f) * Env(t, 0.03f, 0.6f));
        _sounds["death_bug"] = Make(0.5f, t => (Noise(rng) * 0.4f * Env(t, 0.002f, 0.2f) + Saw(t * (300f - 250f * t)) * 0.25f * Env(t, 0.01f, 0.45f)));
        _sounds["spit"] = Make(0.35f, t => Noise(rng) * Env(t, 0.01f, 0.3f) * 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 200f)));
        _sounds["buzz"] = Make(0.5f, t => Saw(t * 180f) * 0.18f * Env(t, 0.05f, 0.45f));
        _sounds["hurt"] = Make(0.25f, t => (Thump(t, 200f, 0.08f) + Noise(rng) * 0.2f) * Env(t, 0.002f, 0.2f));
        _sounds["whistle"] = Make(2.0f, t => Mathf.Sin(t * Mathf.Tau * (1600f - 500f * t)) * 0.2f * Env(t, 0.3f, 1.9f));
        _sounds["jet"] = Make(0.5f, t => Noise(rng) * 0.25f * Env(t, 0.05f, 0.45f));
        _sounds["dash"] = Make(0.4f, t => Noise(rng) * 0.5f * Env(t, 0.005f, 0.35f) + Thump(t, 70f, 0.1f) * 0.4f);
        _sounds["collapse"] = Make(3f, t => Boom(rng, t * 0.6f) * 0.8f + Noise(rng) * 0.15f * Env(t, 0.2f, 2.8f));
        _sounds["heal"] = Make(0.6f, t => Mathf.Sin(t * Mathf.Tau * (440f + 220f * t)) * 0.2f * Env(t, 0.05f, 0.55f));
        _sounds["radio"] = Make(0.15f, t => Noise(rng) * 0.15f * Env(t, 0.005f, 0.12f) + Mathf.Sin(t * Mathf.Tau * 1000f) * 0.1f * Env(t, 0.005f, 0.1f));
        _sounds["wave"] = Make(1.6f, t => (Saw(t * 110f) + Saw(t * 111.5f)) * 0.15f * Env(t, 0.2f, 1.4f));
        _sounds["victory"] = Make(1.6f, t => Chord(t, new[] { 523f, 659f, 784f }) * Env(t, 0.02f, 1.5f));
        _sounds["defeat"] = Make(1.8f, t => Chord(t, new[] { 220f, 261f, 311f }) * Env(t, 0.02f, 1.7f));

        for (var i = 0; i < 48; i++)
        {
            var p = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 220f, UnitSize = 12f, AttenuationFilterCutoffHz = 9000f, MaxPolyphony = 1 };
            AddChild(p);
            _pool3D.Add(p);
        }
        for (var i = 0; i < 16; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Sfx" };
            AddChild(p);
            _pool2D.Add(p);
        }
    }

    /// <summary>Joue un son dans l'espace 3D.</summary>
    public static void At(string name, Vector3 pos, float volumeDb = 0f, float pitch = 1f, float minIntervalMs = 25f)
    {
        if (I == null || !I._sounds.TryGetValue(name, out var s)) return;
        if (!I.Throttle(name, minIntervalMs)) return;
        var p = I._pool3D[I._next3D];
        I._next3D = (I._next3D + 1) % I._pool3D.Count;
        p.Stream = s;
        p.GlobalPosition = pos;
        p.VolumeDb = volumeDb;
        p.PitchScale = pitch * (float)GD.RandRange(0.94, 1.06);
        p.Play();
    }

    /// <summary>Joue un son non spatialisé (interface, armes du joueur local).</summary>
    public static void Ui(string name, float volumeDb = 0f, float pitch = 1f, float minIntervalMs = 0f)
    {
        if (I == null || !I._sounds.TryGetValue(name, out var s)) return;
        if (minIntervalMs > 0 && !I.Throttle(name + "_2d", minIntervalMs)) return;
        var p = I._pool2D[I._next2D];
        I._next2D = (I._next2D + 1) % I._pool2D.Count;
        p.Stream = s;
        p.VolumeDb = volumeDb;
        p.PitchScale = pitch * (float)GD.RandRange(0.97, 1.03);
        p.Play();
    }

    private bool Throttle(string key, float ms)
    {
        var now = Time.GetTicksMsec();
        if (_lastPlayed.TryGetValue(key, out var last) && now - last < ms) return false;
        _lastPlayed[key] = now;
        return true;
    }

    // --- Synthèse ---

    private static AudioStreamWav Make(float seconds, Func<float, float> gen)
    {
        var n = (int)(seconds * Rate);
        var data = new byte[n * 2];
        float lp = 0;
        for (var i = 0; i < n; i++)
        {
            var t = i / (float)Rate;
            var v = gen(t);
            lp += (v - lp) * 0.6f; // léger passe-bas pour adoucir le bruit numérique
            var s = (short)Mathf.Clamp(lp * 32767f * 0.9f, -32768f, 32767f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    private static float Noise(Random rng) => (float)(rng.NextDouble() * 2 - 1);
    private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));

    private static float Env(float t, float attack, float release)
    {
        if (t < attack) return t / attack;
        var r = (t - attack) / release;
        return r >= 1 ? 0 : Mathf.Pow(1 - r, 2.2f);
    }

    private static float Thump(float t, float freq, float decay) =>
        Mathf.Sin(t * Mathf.Tau * freq * (1f - 0.5f * Mathf.Min(t / (decay * 3), 1))) * Mathf.Exp(-t / decay);

    private static float Shot(Random rng, float t, float decay, float color, float gain)
    {
        var crack = Noise(rng) * Mathf.Exp(-t / decay);
        var body = Mathf.Sin(t * Mathf.Tau * color * 0.1f) * Mathf.Exp(-t / (decay * 1.5f)) * 0.4f;
        return (crack + body) * gain;
    }

    private static float Echo(Random rng, float t, float delay, float gain) =>
        t > delay ? Noise(rng) * Mathf.Exp(-(t - delay) / 0.15f) * gain : 0f;

    private static float Boom(Random rng, float t)
    {
        var low = Mathf.Sin(t * Mathf.Tau * (55f - 20f * Mathf.Min(t, 1))) * Mathf.Exp(-t / 0.45f);
        var noise = Noise(rng) * Mathf.Exp(-t / 0.3f) * 0.7f;
        var rumble = Noise(rng) * Mathf.Exp(-t / 1.1f) * 0.25f;
        var a = t < 0.004f ? t / 0.004f : 1f;
        return (low * 0.9f + noise + rumble) * a;
    }

    private static float Click(float t, float at)
    {
        var d = t - at;
        if (d < 0) return 0;
        return Mathf.Sin(d * Mathf.Tau * 2200f) * Mathf.Exp(-d / 0.008f) * 0.5f;
    }

    private static float Chord(float t, float[] freqs)
    {
        float v = 0;
        foreach (var f in freqs) v += Mathf.Sin(t * Mathf.Tau * f) + 0.3f * Mathf.Sin(t * Mathf.Tau * f * 2);
        return v * 0.12f;
    }
}
