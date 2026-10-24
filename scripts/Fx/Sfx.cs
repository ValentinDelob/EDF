using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Rempart;

/// <summary>
/// Sons synthétisés au démarrage, remplacés par des enregistrements pour les armes et explosions (<see cref="Recorded"/>),
/// et lecture 2D/3D par pool de lecteurs.
/// </summary>
public partial class Sfx : Node
{
    public static Sfx I { get; private set; }

    private const int Rate = 22050;
    private readonly Dictionary<string, AudioStreamWav> _sounds = new();
    // Variantes tirées au hasard à chaque lecture (cris, morts d'insectes...)
    private readonly Dictionary<string, AudioStreamWav[]> _variants = new();
    // Boucles sans couture (moteurs de véhicules), lues par des lecteurs propres à chaque véhicule.
    private readonly Dictionary<string, AudioStreamWav> _loops = new();
    private readonly List<AudioStreamPlayer3D> _pool3D = new();
    private readonly List<AudioStreamPlayer> _pool2D = new();
    private int _next3D, _next2D;
    private readonly Dictionary<string, ulong> _lastPlayed = new();

    /// <summary>Durée de synthèse au démarrage (ms), affichée par --dump-audio.</summary>
    internal long BuildMs { get; private set; }

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
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
        // Synthèse en tâche de fond : le thread principal ne fait que créer les flux à la fin.
        // Un son demandé avant (premières centaines de ms) est simplement ignoré.
        Task.Run(() =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var snd = new Dictionary<string, byte[]>();
                var vars = new Dictionary<string, byte[][]>();
                var loops = new Dictionary<string, byte[]>();
                Build(snd, vars);
                BuildVehicles(snd, loops);
                BuildProps(snd);
                var ms = sw.ElapsedMilliseconds;
                Callable.From(() => OnBuilt(snd, vars, loops, ms)).CallDeferred();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Sfx] échec de la synthèse : {e}");
            }
        });
    }

    private void OnBuilt(Dictionary<string, byte[]> snd, Dictionary<string, byte[][]> vars, Dictionary<string, byte[]> loops, long ms)
    {
        if (!IsInstanceValid(this)) return;
        foreach (var (n, d) in snd) _sounds[n] = Wav(d);
        foreach (var (n, d) in loops)
        {
            var w = Wav(d);
            w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            w.LoopBegin = 0;
            w.LoopEnd = d.Length / 2;
            _loops[n] = w;
        }
        foreach (var (n, v) in vars)
        {
            _variants[n] = v.Select(Wav).ToArray();
            _sounds[n] = _variants[n][0];
        }
        LoadRecorded();
        BuildMs = ms;
    }

    private static AudioStreamWav Wav(byte[] data) =>
        new() { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };

    private static void Build(Dictionary<string, byte[]> snd, Dictionary<string, byte[][]> vars)
    {
        var rng = new Random(1234);
        // Armes : transitoire + corps filtré + coup sourd + queue de réverbération urbaine
        snd["rifle"] = Make(0.5f, Gun(rng, 1700f, 0.035f, 120f, 0.04f, 0.22f, 0.35f, 0.75f));
        snd["smg"] = Make(0.35f, Gun(rng, 2300f, 0.022f, 150f, 0.025f, 0.15f, 0.25f, 0.6f));
        snd["shotgun"] = Make(0.8f, Gun(rng, 1100f, 0.08f, 75f, 0.09f, 0.35f, 0.5f, 0.95f));
        snd["sniper"] = Make(1.4f, Gun(rng, 1400f, 0.1f, 60f, 0.14f, 0.5f, 0.55f, 1f, slap: true));
        snd["minigun"] = Make(0.22f, Gun(rng, 2100f, 0.02f, 150f, 0.02f, 0.1f, 0.2f, 0.5f));
        snd["rocket"] = Make(0.9f, Rocket(rng));
        snd["launcher"] = Make(0.35f, t => Thump(t, 140f, 0.05f) * 0.8f + Noise(rng) * Env(t, 0.005f, 0.12f) * 0.4f);
        snd["explosion"] = Make(3.2f, Explosion(rng, 1f));
        snd["explosion_small"] = Make(1.6f, Explosion(rng, 1.8f));
        snd["laser"] = Make(0.25f, t => (Mathf.Sin(t * 2900f + Mathf.Sin(t * 60f) * 8f) * 0.35f + Noise(rng) * 0.08f) * Env(t, 0.005f, 0.22f));
        snd["zap"] = Make(0.35f, t => (Saw(t * (900f - t * 1800f)) * 0.4f + Noise(rng) * 0.35f) * Env(t, 0.002f, 0.3f));
        snd["plasma"] = Make(0.6f, t => (Mathf.Sin(t * (500f - 600f * t)) * 0.5f + Noise(rng) * 0.25f) * Env(t, 0.01f, 0.55f));
        snd["hammer"] = Make(0.6f, t => Thump(t, 55f, 0.25f) + Noise(rng) * Env(t, 0.002f, 0.12f) * 0.6f);
        snd["reload"] = Make(0.3f, t => Click(t, 0.0f) + Click(t, 0.18f) * 1.2f);
        snd["empty"] = Make(0.08f, t => Click(t, 0.0f) * 0.7f);
        snd["pickup"] = Make(0.25f, t => Mathf.Sin(t * Mathf.Tau * (660f + 900f * t)) * 0.35f * Env(t, 0.005f, 0.22f));
        snd["pickup_weapon"] = Make(0.4f, t => (Mathf.Sin(t * Mathf.Tau * 523f) * (t < 0.12f ? 1 : 0) + Mathf.Sin(t * Mathf.Tau * 784f) * (t >= 0.12f ? 1 : 0)) * 0.3f * Env(t, 0.005f, 0.38f));
        snd["ui_click"] = Make(0.06f, t => Mathf.Sin(t * Mathf.Tau * 1200f) * 0.25f * Env(t, 0.001f, 0.05f));
        snd["ui_hover"] = Make(0.04f, t => Mathf.Sin(t * Mathf.Tau * 1800f) * 0.08f * Env(t, 0.001f, 0.03f));
        snd["hit"] = Make(0.1f, t => (Noise(rng) * 0.4f + Mathf.Sin(t * Mathf.Tau * 300f) * 0.3f) * Env(t, 0.001f, 0.08f));
        snd["hitmarker"] = Make(0.05f, t => Mathf.Sin(t * Mathf.Tau * 2400f) * 0.2f * Env(t, 0.001f, 0.04f));
        vars["screech"] = Enumerable.Range(0, 5).Select(i => Make(0.5f + 0.08f * i, Screech(rng, i))).ToArray();
        snd["screech"] = vars["screech"][0];
        vars["death_bug"] = Enumerable.Range(0, 3).Select(i => Make(0.55f + 0.1f * i, DeathBug(rng, i))).ToArray();
        snd["death_bug"] = vars["death_bug"][0];
        snd["spit"] = Make(0.35f, t => Noise(rng) * Env(t, 0.01f, 0.3f) * 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 200f)));
        snd["buzz"] = Make(0.5f, t => Saw(t * 180f) * 0.18f * Env(t, 0.05f, 0.45f));
        snd["hurt"] = Make(0.25f, t => (Thump(t, 200f, 0.08f) + Noise(rng) * 0.2f) * Env(t, 0.002f, 0.2f));
        // Saisie de la fourmi rouge : claquement de mandibules + craquement d'armure, râle d'effort de la victime.
        vars["crunch"] = Enumerable.Range(0, 3).Select(i => Make(0.32f, Crunch(rng, i))).ToArray();
        snd["crunch"] = vars["crunch"][0];
        vars["grunt"] = Enumerable.Range(0, 3).Select(i => Make(0.3f + 0.04f * i, Grunt(rng, i))).ToArray();
        snd["grunt"] = vars["grunt"][0];
        snd["whistle"] = Make(2.0f, t => Mathf.Sin(t * Mathf.Tau * (1600f - 500f * t)) * 0.2f * Env(t, 0.3f, 1.9f));
        snd["dash"] = Make(0.4f, t => Noise(rng) * 0.5f * Env(t, 0.005f, 0.35f) + Thump(t, 70f, 0.1f) * 0.4f);
        snd["collapse"] = Make(3.5f, Explosion(rng, 0.6f, 0.15f));
        snd["heal"] = Make(0.6f, t => Mathf.Sin(t * Mathf.Tau * (440f + 220f * t)) * 0.2f * Env(t, 0.05f, 0.55f));
        snd["radio"] = Make(0.15f, t => Noise(rng) * 0.15f * Env(t, 0.005f, 0.12f) + Mathf.Sin(t * Mathf.Tau * 1000f) * 0.1f * Env(t, 0.005f, 0.1f));
        snd["wave"] = Make(1.6f, t => (Saw(t * 110f) + Saw(t * 111.5f)) * 0.15f * Env(t, 0.2f, 1.4f));
        // Fanfares de fin : rendues par le synthétiseur de Music (cuivres, timbales, réverbération)
        snd["victory"] = FromSamples(Music.RenderStinger(true), 0.8f);
        snd["defeat"] = FromSamples(Music.RenderStinger(false), 0.8f);
    }

    /// <summary>Joue un son dans l'espace 3D.</summary>
    public static void At(string name, Vector3 pos, float volumeDb = 0f, float pitch = 1f, float minIntervalMs = 25f)
    {
        var s = I?.Pick(name);
        if (s == null || !I.Throttle(name, minIntervalMs)) return;
        var p = I._pool3D[I._next3D];
        I._next3D = (I._next3D + 1) % I._pool3D.Count;
        p.Stream = s;
        p.GlobalPosition = pos;
        p.VolumeDb = volumeDb + I.Gain(name);
        p.PitchScale = pitch * (float)GD.RandRange(0.94, 1.06);
        p.Play();
    }

    /// <summary>Joue un son non spatialisé (interface, armes du joueur local).</summary>
    public static void Ui(string name, float volumeDb = 0f, float pitch = 1f, float minIntervalMs = 0f)
    {
        var s = I?.Pick(name);
        if (s == null) return;
        if (minIntervalMs > 0 && !I.Throttle(name + "_2d", minIntervalMs)) return;
        var p = I._pool2D[I._next2D];
        I._next2D = (I._next2D + 1) % I._pool2D.Count;
        p.Stream = s;
        p.VolumeDb = volumeDb + I.Gain(name);
        p.PitchScale = pitch * (float)GD.RandRange(0.97, 1.03);
        p.Play();
    }

    /// <summary>Boucle sans couture (null tant que la synthèse n'est pas terminée).</summary>
    public static AudioStreamWav Loop(string name) => I != null && I._loops.TryGetValue(name, out var s) ? s : null;

    private AudioStreamWav Pick(string name)
    {
        if (_variants.TryGetValue(name, out var v)) return v[(int)(GD.Randi() % (uint)v.Length)];
        return _sounds.GetValueOrDefault(name);
    }

    /// <summary>Tous les sons (variantes comprises), pour l'export de débogage --dump-audio.</summary>
    internal IEnumerable<(string, AudioStreamWav)> AllSounds()
    {
        foreach (var (n, s) in _loops) yield return ("loop_" + n, s);
        foreach (var (n, s) in _sounds) if (!_variants.ContainsKey(n)) yield return (n, s);
        foreach (var (n, v) in _variants) for (var i = 0; i < v.Length; i++) yield return ($"{n}_{i}", v[i]);
    }

    private bool Throttle(string key, float ms)
    {
        var now = Time.GetTicksMsec();
        if (_lastPlayed.TryGetValue(key, out var last) && now - last < ms) return false;
        _lastPlayed[key] = now;
        return true;
    }

    // --- Synthèse ---

    private static byte[] Make(float seconds, Func<float, float> gen)
    {
        var n = (int)(seconds * Rate);
        var buf = new float[n];
        float lp = 0;
        for (var i = 0; i < n; i++)
        {
            var v = gen(i / (float)Rate);
            lp += (v - lp) * 0.6f; // léger passe-bas pour adoucir le bruit numérique
            buf[i] = lp * 0.9f;
        }
        // Fondu de sortie très court : pas de clic si le générateur ne finit pas à zéro
        var fade = Math.Min(n, Rate / 200);
        for (var i = 0; i < fade; i++) buf[n - 1 - i] *= i / (float)fade;
        return FromSamples(buf, 0.97f, soft: true);
    }

    /// <summary>
    /// Convertit en PCM 16 bits. Sous la limite : inchangé. Au-delà : limiteur doux (tanh) qui écrase
    /// les transitoires sans baisser le corps du son, ou simple gain si soft est faux.
    /// </summary>
    private static byte[] FromSamples(float[] buf, float limit, bool soft = false)
    {
        var peak = 0f;
        foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
        var over = peak > limit;
        var k = over ? limit / peak : 1f;
        var tp = MathF.Tanh(peak / limit);
        var data = new byte[buf.Length * 2];
        for (var i = 0; i < buf.Length; i++)
        {
            var x = !over ? buf[i] : soft ? limit * MathF.Tanh(buf[i] / limit) / tp : buf[i] * k;
            var s = (short)Mathf.Clamp(x * 32767f, -32768f, 32767f);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return data;
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

    private static float Click(float t, float at)
    {
        var d = t - at;
        if (d < 0) return 0;
        return Mathf.Sin(d * Mathf.Tau * 2200f) * Mathf.Exp(-d / 0.008f) * 0.5f;
    }

    /// <summary>Coup de feu en couches : claquement bref, corps passe-bande, coup sourd grave, queue filtrée (+ échos de façades).</summary>
    private static Func<float, float> Gun(Random rng, float bodyHz, float bodyDecay, float thumpHz, float thumpDecay, float tailDecay, float tailGain, float gain, bool slap = false)
    {
        var bp = new Svf();
        var lp = new Svf();
        var echo = new Svf();
        echo.Set(900f, 0.8f);
        return t =>
        {
            var n = Noise(rng);
            var crack = n * Mathf.Exp(-t / 0.0025f);
            bp.Set(bodyHz * (1f + 0.8f * Mathf.Exp(-t / 0.01f)), 1.3f);
            bp.Run(n);
            var body = bp.Band * 2.4f * Mathf.Exp(-t / bodyDecay);
            var thump = Thump(t, thumpHz, thumpDecay) * 0.8f;
            lp.Set(150f + 1800f * Mathf.Exp(-t / (tailDecay * 0.4f)), 0.7f);
            var tail = lp.Run(n) * tailGain * (1f - Mathf.Exp(-t / 0.015f)) * Mathf.Exp(-t / tailDecay);
            var v = crack * 0.55f + body + thump + tail;
            if (slap)
            {
                // Réflexions sur les immeubles : deux claquements étouffés retardés
                var e = Burst(rng, t, 0.19f, 0.05f) * 0.5f + Burst(rng, t, 0.43f, 0.08f) * 0.3f;
                v += echo.Run(e) * 1.5f;
            }
            return v * gain;
        };
    }

    private static float Burst(Random rng, float t, float at, float decay) =>
        t < at ? 0f : Noise(rng) * Mathf.Exp(-(t - at) / decay);

    private static Func<float, float> Rocket(Random rng)
    {
        var bp = new Svf();
        return t =>
        {
            var n = Noise(rng);
            bp.Set(700f + 1600f * Mathf.Exp(-t / 0.3f), 1.5f);
            bp.Run(n);
            var whoosh = (bp.Band * 1.6f + n * 0.15f) * Env(t, 0.015f, 0.85f);
            return whoosh * 0.7f + Thump(t, 90f, 0.1f) * 0.6f + n * Mathf.Exp(-t / 0.004f) * 0.4f;
        };
    }

    /// <summary>Explosion : claquement, médium filtré qui s'assombrit, balayage grave, grondement brun et débris.</summary>
    private static Func<float, float> Explosion(Random rng, float speed, float crackGain = 0.9f)
    {
        var lp = new Svf();
        var brown = 0f;
        var ph = 0f;
        var debris = 0f;
        return t0 =>
        {
            var t = t0 * speed;
            var n = Noise(rng);
            var a = t < 0.003f ? t / 0.003f : 1f;
            var crack = n * Mathf.Exp(-t / 0.012f) * crackGain;
            lp.Set(120f + 2600f * Mathf.Exp(-t / 0.25f), 0.8f);
            var mid = lp.Run(n) * 1.5f * Mathf.Exp(-t / 0.6f);
            ph += (28f + 55f * Mathf.Exp(-t / 0.15f)) / Rate;
            var low = Mathf.Sin(ph * Mathf.Tau) * Mathf.Exp(-t / 0.7f);
            brown = (brown + n * 0.02f) * 0.997f;
            var rumble = brown * 2.2f * (1f - Mathf.Exp(-t / 0.08f)) * Mathf.Exp(-t / 1.3f);
            if (rng.NextDouble() < 0.0012 * Mathf.Exp(-t / 0.9f)) debris = 1f;
            debris *= 0.93f;
            return (crack + mid + low * 0.9f + rumble + n * debris * 0.35f) * a;
        };
    }

    /// <summary>Cri de fourmi : scie modulée dans un formant, avec cliquetis de mandibules selon la variante.</summary>
    private static Func<float, float> Screech(Random rng, int variant)
    {
        var f = new Svf();
        var ph = 0f;
        var baseHz = 480f + 110f * variant + (float)rng.NextDouble() * 60f;
        var mod = 16f + 7f * variant;
        var chitter = variant % 2 == 1 ? 38f + 8f * variant : 0f;
        var len = 0.5f + 0.08f * variant;
        return t =>
        {
            var x = t / len;
            var hz = baseHz * (1f + 0.25f * Mathf.Sin(t * Mathf.Tau * mod) + 0.35f * (variant >= 3 ? x : -x * 0.5f));
            ph += hz / Rate;
            var saw = 2f * (ph - Mathf.Floor(ph + 0.5f));
            f.Set(1600f + 900f * Mathf.Sin(t * Mathf.Tau * (3f + variant)), 3f);
            f.Run(saw * 0.6f + Noise(rng) * 0.35f);
            var gate = chitter > 0 ? Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * Mathf.Tau * chitter), 6f) : 1f;
            return f.Band * 0.9f * gate * Env(t, 0.02f, len - 0.04f);
        };
    }

    /// <summary>Mort d'insecte : craquement de carapace puis râle descendant.</summary>
    private static Func<float, float> DeathBug(Random rng, int variant)
    {
        var bp = new Svf();
        var ph = 0f;
        var start = 340f - 50f * variant;
        return t =>
        {
            var n = Noise(rng);
            bp.Set(2400f * Mathf.Exp(-t / 0.08f) + 300f, 1.2f);
            bp.Run(n);
            var crunch = bp.Band * 1.4f * Env(t, 0.002f, 0.18f) * (0.6f + 0.4f * Mathf.Sin(t * Mathf.Tau * 90f));
            ph += start * (1f - 0.8f * Mathf.Min(t / 0.5f, 1f)) / Rate;
            var squeal = 2f * (ph - Mathf.Floor(ph + 0.5f)) * 0.22f * Env(t, 0.01f, 0.45f + 0.1f * variant);
            return crunch + squeal + Thump(t, 70f, 0.06f) * 0.3f;
        };
    }

    /// <summary>
    /// Morsure de mandibules : double claquement sec de chitine (les deux lames qui se referment), tintement bref,
    /// crépitement de plaques d'armure qui cèdent, coup sourd du corps serré.
    /// </summary>
    private static Func<float, float> Crunch(Random rng, int variant)
    {
        var clackF = new Svf();
        var crackF = new Svf();
        var crackle = 0f;
        var hz = 2600f + 450f * variant;
        var gap = 0.012f + 0.004f * variant;
        return t =>
        {
            var n = Noise(rng);
            clackF.Set(hz, 2.5f);
            clackF.Run(n);
            var clack = clackF.Band * 3.2f * (Mathf.Exp(-t / 0.004f) + (t > gap ? 0.75f * Mathf.Exp(-(t - gap) / 0.005f) : 0f));
            var ping = Mathf.Sin(t * Mathf.Tau * (1700f + 160f * variant)) * Mathf.Exp(-t / 0.018f) * 0.3f;
            if (t > 0.01f && rng.NextDouble() < 0.014 * Mathf.Exp(-t / 0.08f)) crackle = 0.6f + (float)rng.NextDouble() * 0.7f;
            crackle *= 0.93f;
            crackF.Set(1300f + 700f * Mathf.Sin(t * 37f + variant), 1.3f);
            crackF.Run(n * crackle);
            return clack + ping + crackF.Band * 2.4f + Thump(t, 75f + 10f * variant, 0.05f) * 0.6f;
        };
    }

    /// <summary>Râle d'effort (voix grave étouffée) : onde en dents de scie dans deux formants « eu », souffle.</summary>
    private static Func<float, float> Grunt(Random rng, int variant)
    {
        var f1 = new Svf();
        var f2 = new Svf();
        var ph = 0f;
        var len = 0.3f + 0.04f * variant;
        var pitch = 125f - 12f * variant;
        return t =>
        {
            ph += pitch * (1f - 0.25f * t / len) * (1f + 0.04f * Mathf.Sin(t * 70f)) / Rate;
            var src = 2f * (ph - Mathf.Floor(ph + 0.5f)) + Noise(rng) * 0.25f;
            f1.Set(560f + 60f * variant, 4f);
            f2.Set(1050f + 80f * variant, 5f);
            f1.Run(src);
            f2.Run(src);
            return (f1.Band * 0.9f + f2.Band * 0.5f) * Env(t, 0.02f, len - 0.03f) * 0.9f;
        };
    }

    /// <summary>Filtre d'état variable (TPT) pour les générateurs à mémoire.</summary>
    private sealed class Svf
    {
        private float _ic1, _ic2, _a1, _a2, _a3;
        public float Band;

        public void Set(float fc, float q)
        {
            var g = Mathf.Tan(Mathf.Pi * Mathf.Clamp(fc, 20f, Rate * 0.45f) / Rate);
            var k = 1f / q;
            _a1 = 1f / (1f + g * (g + k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public float Run(float x)
        {
            var v3 = x - _ic2;
            var v1 = _a1 * _ic1 + _a2 * v3;
            var v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2f * v1 - _ic1;
            _ic2 = 2f * v2 - _ic2;
            Band = v1;
            return v2;
        }
    }
}
