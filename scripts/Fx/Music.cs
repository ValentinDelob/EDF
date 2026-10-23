using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Rempart;

/// <summary>
/// Musique procédurale : pistes bouclées synthétisées en tâche de fond au démarrage,
/// fondu enchaîné entre deux lecteurs sur le bus "Music" et choix automatique selon l'état du jeu.
/// </summary>
public partial class Music : Node
{
    public enum Track { None, Menu, Calm, Combat, Boss }

    public static Music I { get; private set; }

    public const int Rate = 22050;

    /// <summary>Si faux, la piste n'est plus choisie automatiquement (seul Play() décide).</summary>
    public bool Auto = true;

    private static readonly Track[] Order = { Track.Menu, Track.Calm, Track.Combat, Track.Boss };

    // Gain relatif de chaque piste (les pistes sont normalisées au même crête).
    private static readonly Dictionary<Track, float> Gain = new()
    {
        [Track.Menu] = 0.85f, [Track.Calm] = 0.6f, [Track.Combat] = 0.9f, [Track.Boss] = 0.95f,
    };

    private readonly Dictionary<Track, AudioStreamWav> _streams = new();
    private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[2];
    private readonly Track[] _onPlayer = new Track[2];
    private readonly float[] _level = new float[2];
    private readonly float[] _target = new float[2];
    private int _cur;
    private Track _wanted = Track.None, _playing = Track.None;
    private float _fade = 1.5f;
    private float _duck = 1f, _duckLevel = 1f;
    private float _calmTimer, _endTimer, _holdTimer;
    private bool _intense;
    private bool _dump;
    private long _renderMs, _mainMs;

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        for (var i = 0; i < 2; i++)
        {
            _players[i] = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80f };
            AddChild(_players[i]);
        }
        _dump = OS.GetCmdlineUserArgs().Contains("--dump-audio");
        // Rendu hors du thread principal : seules la création des AudioStreamWav et le lancement s'y font.
        Task.Run(() =>
        {
            foreach (var t in Order)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var s = Compose(t);
                    var pcm = s.Finish(stereo: true);
                    var ms = sw.ElapsedMilliseconds;
                    Callable.From(() => OnRendered(t, pcm, s.N, ms)).CallDeferred();
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Music] échec du rendu {t} : {e}");
                }
            }
        });
    }

    private void OnRendered(Track t, byte[] pcm, int frames, long renderMs)
    {
        if (!IsInstanceValid(this)) return;
        var sw = Stopwatch.StartNew();
        _streams[t] = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = true, Data = pcm,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = frames,
        };
        _renderMs += renderMs;
        _mainMs += sw.ElapsedMilliseconds;
        if (_dump) GD.Print($"[Music] {t} prête : rendu {renderMs} ms, {sw.ElapsedMilliseconds} ms sur le thread principal, t={Time.GetTicksMsec()} ms");
        if (t == _wanted) Apply();
        if (_streams.Count == Order.Length && _dump) DumpAll();
    }

    /// <summary>Demande une piste (fondu enchaîné). None = silence.</summary>
    public void Play(Track t, float fade = 1.5f)
    {
        if (t == _wanted) return;
        _wanted = t;
        _fade = Mathf.Max(fade, 0.05f);
        Apply();
    }

    public Track Current => _playing;

    private void Apply()
    {
        if (_wanted == _playing) return;
        if (_wanted != Track.None && !_streams.ContainsKey(_wanted)) return; // pas encore rendue
        if (_dump) GD.Print($"[Music] {_playing} -> {_wanted} (t={Time.GetTicksMsec()} ms)");
        var old = _cur;
        var nxt = 1 - _cur;
        _target[old] = 0f;
        if (_wanted == Track.None)
        {
            _playing = Track.None;
            return;
        }
        if (_onPlayer[nxt] == _wanted && _players[nxt].Playing)
        {
            // Retour vers la piste en cours d'extinction : on inverse simplement le fondu.
            _target[nxt] = 1f;
        }
        else
        {
            var s = _streams[_wanted];
            var from = 0f;
            // Les pistes de mission partagent tempo et métrique : on reprend au même temps de mesure.
            if (OnGrid(_playing) && OnGrid(_wanted) && _players[old].Playing)
                from = (float)(_players[old].GetPlaybackPosition() % s.GetLength());
            var p = _players[nxt];
            p.Stream = s;
            p.VolumeDb = -80f;
            p.Play(from);
            _level[nxt] = 0f;
            _target[nxt] = 1f;
            _onPlayer[nxt] = _wanted;
        }
        _cur = nxt;
        _playing = _wanted;
    }

    private static bool OnGrid(Track t) => t is Track.Calm or Track.Combat or Track.Boss;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (Auto) Decide(dt);
        _duckLevel = Mathf.MoveToward(_duckLevel, _duck, dt * 2f);
        for (var i = 0; i < 2; i++)
        {
            var p = _players[i];
            _level[i] = Mathf.MoveToward(_level[i], _target[i], dt / _fade);
            if (!p.Playing) continue;
            if (_level[i] <= 0f && _target[i] <= 0f)
            {
                p.Stop();
                _onPlayer[i] = Track.None;
                continue;
            }
            // Courbe à puissance constante : pas de creux pendant le fondu enchaîné.
            var g = Mathf.Sin(_level[i] * Mathf.Pi * 0.5f) * Gain.GetValueOrDefault(_onPlayer[i], 1f) * _duckLevel;
            p.VolumeDb = Mathf.LinearToDb(Mathf.Max(g, 0.0001f));
        }
    }

    /// <summary>Choix automatique : menus, calme, combat, intensité maximale (boss / nids / grosse vague).</summary>
    private void Decide(float dt)
    {
        _holdTimer -= dt;
        var w = App.I?.CurrentWorld;
        if (w == null || !IsInstanceValid(w) || !w.IsInsideTree())
        {
            _duck = 1f;
            _endTimer = 0f;
            _intense = false;
            Play(Track.Menu, 2f);
            return;
        }
        _duck = w.Paused ? 0.45f : 1f;
        if (w.Ended)
        {
            // Silence pour laisser passer la fanfare de fin, puis thème du menu sur l'écran de résultats.
            _endTimer += dt;
            if (_endTimer < 4.5f) Play(Track.None, 0.6f);
            else Play(Track.Menu, 3f);
            return;
        }
        _endTimer = 0f;
        if (w.EnemiesRemaining >= 40) _intense = true;
        else if (w.EnemiesRemaining < 28) _intense = false;

        Track want;
        if (w.BossHealth >= 0f || _intense) want = Track.Boss;
        else if (w.EnemiesRemaining > 0) want = Track.Combat;
        else
        {
            // Calme seulement après quelques secondes sans ennemi (évite le va-et-vient entre vagues).
            _calmTimer += dt;
            want = _playing is Track.Combat or Track.Boss && _calmTimer < 6f ? _playing : Track.Calm;
        }
        if (want != Track.Calm) _calmTimer = 0f;
        if (want == _wanted || _holdTimer > 0f) return;
        Play(want, want == Track.Boss ? 1.2f : 2f);
        _holdTimer = 3f;
    }

    // ------------------------------------------------------------------ Débogage

    private void DumpAll()
    {
        GD.Print($"[Music] rendu {Order.Length} pistes : {_renderMs} ms (thread de fond), {_mainMs} ms sur le thread principal");
        DirAccess.MakeDirRecursiveAbsolute("user://audio_dump");
        foreach (var (t, s) in _streams) DumpWav("music_" + t.ToString().ToLowerInvariant(), s);
        if (Sfx.I != null) GD.Print($"[Music] synthèse des bruitages (thread de fond) : {Sfx.I.BuildMs} ms");
        if (Sfx.I != null) foreach (var (n, s) in Sfx.I.AllSounds()) DumpWav("sfx_" + n, s);
        GD.Print("[Music] export : " + ProjectSettings.GlobalizePath("user://audio_dump"));
    }

    private static void DumpWav(string name, AudioStreamWav s)
    {
        var d = s.Data;
        int peak = 0, clip = 0;
        double sum = 0;
        var n = d.Length / 2;
        for (var i = 0; i < n; i++)
        {
            var v = Math.Abs((int)(short)(d[i * 2] | (d[i * 2 + 1] << 8)));
            if (v > peak) peak = v;
            if (v >= 32700) clip++;
            sum += (double)v * v;
        }
        var rms = Math.Sqrt(sum / Math.Max(n, 1)) / 32768.0;
        GD.Print($"[audio] {name,-22} {s.GetLength(),6:0.00} s  {d.Length / 1024,6} Kio  crête {peak / 32768.0:0.000}  rms {rms:0.000}  écrêtés {clip}");
        if (s.LoopMode == AudioStreamWav.LoopModeEnum.Forward)
        {
            // Analyse de boucle : saut à la jointure comparé aux sauts habituels, profil RMS, part de graves (< 250 Hz).
            var ch = s.Stereo ? 2 : 1;
            var frames = n / ch;
            float S(int f, int c) => (short)(d[(f * ch + c) * 2] | (d[(f * ch + c) * 2 + 1] << 8)) / 32768f;
            var jumps = new List<float>(frames);
            for (var f = 1; f < frames; f++) jumps.Add(Math.Abs(S(f, 0) - S(f - 1, 0)));
            jumps.Sort();
            var seam = Math.Abs(S(0, 0) - S(frames - 1, 0));
            // Référence : même mesure au milieu de la boucle (mêmes frappes), pour distinguer une attaque d'un clic.
            var mid = 0f;
            for (var f = frames / 2 - 2; f < frames / 2 + 3; f++) mid = Math.Max(mid, Math.Abs(S(f, 0) - S(f - 1, 0)));
            GD.Print($"        saut au milieu de boucle (même temps fort) : {mid:0.0000}");
            var prof = new List<string>();
            for (var b = 0; b + Rate * 2 <= frames; b += Rate * 2)
            {
                double e = 0;
                for (var f = b; f < b + Rate * 2; f++) e += S(f, 0) * S(f, 0);
                prof.Add($"{Math.Sqrt(e / (Rate * 2)):0.00}");
            }
            double lowE = 0, allE = 0;
            float lp = 0;
            for (var f = 0; f < frames; f++)
            {
                var x = S(f, 0);
                lp += (x - lp) * 0.069f; // ~250 Hz
                lowE += lp * lp;
                allE += x * x;
            }
            GD.Print($"        jointure {seam:0.0000} (p99.9 {jumps[(int)(jumps.Count * 0.999)]:0.0000}, max {jumps[^1]:0.0000})  graves {lowE / allE * 100:0}%  rms/2s {string.Join(" ", prof)}");
        }
        s.SaveToWav($"user://audio_dump/{name}.wav");
    }

    // ------------------------------------------------------------------ Composition

    private static Score Compose(Track t) => t switch
    {
        Track.Menu => MenuTheme(),
        Track.Calm => CalmTheme(),
        Track.Combat => CombatTheme(),
        _ => BossTheme(),
    };

    // Accords (notes MIDI)
    private static readonly int[] Em = { 52, 55, 59, 64 }, Cmaj = { 48, 55, 60, 64 }, Dmaj = { 50, 57, 62, 66 };
    private static readonly int[] Bmaj = { 47, 54, 59, 63 }, Am = { 45, 52, 57, 60 }, Fmaj = { 53, 57, 60, 65 };

    /// <summary>Menu : ré mineur, 72 BPM, 12 mesures (40 s). Bourdon, nappes de cordes, cor, taikos épars.</summary>
    private static Score MenuTheme()
    {
        var s = new Score(72f, 12, 11);
        s.Drone(26, 0.38f, 260f, 2);
        s.Drone(38, 0.2f, 500f, 3);
        int[][] chords = { new[] { 50, 57, 62, 65 }, new[] { 46, 53, 58, 62 }, new[] { 50, 55, 58, 62 },
                           new[] { 50, 57, 62, 65 }, new[] { 46, 53, 58, 65 }, new[] { 45, 52, 57, 61 } };
        int[] roots = { 38, 34, 31, 38, 34, 33 };
        for (var c = 0; c < 6; c++)
        {
            var t = s.At(c * 2);
            foreach (var m in chords[c]) s.Note(Patches.Pad, t, s.At(2) - 0.3f, m, 0.13f, (m % 5 - 2) * 0.2f);
            s.Note(Patches.Cello, t, s.At(2) - 0.2f, roots[c], 0.22f);
            s.Taiko(t, 0.9f);
            s.Taiko(s.At(c * 2 + 1, 2.5f), 0.35f);
            if (c >= 3)
            {
                // Battement de cœur qui monte vers la fin de boucle.
                for (var b = 0; b < 2; b++)
                    for (var k = 0; k < 4; k += 2)
                    {
                        s.Tom(s.At(c * 2 + b, k), 70f, 0.35f);
                        s.Tom(s.At(c * 2 + b, k + 0.4f), 62f, 0.22f);
                    }
            }
        }
        for (var b = 4; b < 12; b++)
            for (var k = 0; k < 8; k++) s.Hat(s.At(b, k * 0.5f), k % 2 == 0 ? 0.06f : 0.035f);
        // Thème au cor
        (float bar, float beat, float len, int m)[] horn =
        {
            (2, 0, 2, 62), (2, 2, 1, 65), (2, 3, 1, 64), (3, 0, 3.5f, 62),
            (6, 0, 2, 69), (6, 2, 1, 67), (6, 3, 1, 65), (7, 0, 2, 64), (7, 2, 1.5f, 62),
            (10, 0, 2, 64), (10, 2, 2, 61), (11, 0, 3.5f, 57),
        };
        foreach (var h in horn) s.Note(Patches.Horn, s.At(h.bar, h.beat), h.len * s.Beat, h.m, 0.42f, -0.15f);
        s.Bell(s.At(1, 2), 81, 0.18f, 0.4f);
        s.Bell(s.At(5, 2), 77, 0.18f, -0.4f);
        s.Bell(s.At(9, 2), 74, 0.18f, 0.3f);
        s.Riser(s.At(10, 2), 6 * s.Beat, 0.25f);
        s.Reverb(0.55f, 0.88f);
        return s;
    }

    /// <summary>Calme en mission : mi mineur, grille de 128 BPM (demi-temps), 16 mesures (30 s).</summary>
    private static Score CalmTheme()
    {
        var s = new Score(128f, 16, 22);
        s.Drone(28, 0.32f, 220f, 1);
        s.Drone(40, 0.18f, 420f, 2);
        int[][] chords = { Em, Cmaj, Am, Bmaj };
        int[] roots = { 40, 36, 33, 35 };
        for (var c = 0; c < 4; c++)
        {
            var t = s.At(c * 4);
            foreach (var m in chords[c]) s.Note(Patches.Pad, t, s.At(4) - 0.4f, m, 0.12f, (m % 5 - 2) * 0.2f);
            s.Taiko(t, 0.55f);
            for (var b = 0; b < 4; b++)
            {
                for (var k = 0; k < 8; k++)
                    s.Note(Patches.Pulse, s.At(c * 4 + b, k * 0.5f), 0.18f, roots[c], k % 2 == 0 ? 0.22f : 0.13f);
                s.Kick(s.At(c * 4 + b), 0.35f, 0.25f);
                for (var k = 0; k < 4; k++) s.Hat(s.At(c * 4 + b, k), 0.04f, 0.03f, -0.3f);
            }
        }
        s.Bell(s.At(2, 0), 76, 0.14f, 0.3f);
        s.Bell(s.At(2, 1.5f), 71, 0.10f, 0.3f);
        s.Bell(s.At(6, 0), 72, 0.14f, -0.3f);
        s.Bell(s.At(10, 0), 69, 0.14f, 0.3f);
        s.Bell(s.At(14, 0), 75, 0.14f, -0.3f);
        s.Bell(s.At(14, 1.5f), 71, 0.10f, -0.3f);
        s.Reverb(0.5f, 0.87f);
        return s;
    }

    /// <summary>Combat : mi mineur, 128 BPM, 20 mesures (37,5 s). Caisse claire militaire, basse au galop, cuivres.</summary>
    private static Score CombatTheme()
    {
        var s = new Score(128f, 20, 33);
        int[][] chordA = { Em, Em, Cmaj, Cmaj, Dmaj, Dmaj, Bmaj, Bmaj };
        int[][] chordB = { Am, Am, Cmaj, Dmaj, Em, Em, Bmaj, Bmaj };
        int[] gallop = { 0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15 };
        for (var bar = 0; bar < 20; bar++)
        {
            var chord = bar < 8 ? chordA[bar] : bar < 16 ? chordB[bar - 8] : Em;
            var root = chord[0] - 12;
            float St(int step) => s.At(bar, step * 0.25f);

            if (bar < 16)
            {
                // Batterie
                foreach (var k in new[] { 0, 6, 8, 14 }) s.Kick(St(k), 0.8f);
                s.Snare(St(4), 0.75f);
                s.Snare(St(12), 0.8f);
                s.Snare(St(13), 0.18f, 0.2f);
                s.Snare(St(15), 0.22f, -0.2f);
                if (bar % 4 == 3) s.Roll(St(12), s.Beat, 8, 0.25f, 0.6f);
                for (var k = 0; k < 16; k += 2) s.Hat(St(k), k % 4 == 2 ? 0.2f : 0.12f);
                if (bar % 8 == 7)
                {
                    float[] toms = { 190f, 160f, 130f, 100f };
                    for (var k = 8; k < 16; k++) s.Tom(St(k), toms[(k - 8) / 2], 0.55f, (k - 11.5f) * 0.12f);
                }
                if (bar % 4 == 0) s.Taiko(St(0), 0.5f);
                // Basse au galop
                foreach (var k in gallop)
                {
                    var m = k == 8 ? root + 12 : k == 14 ? root + 7 : root;
                    s.Note(Patches.Bass, St(k), k % 4 == 0 ? 0.2f : 0.1f, m, k % 4 == 0 ? 0.5f : 0.38f);
                }
                // Cordes en spiccato
                int[] arp = { 0, 2, 3, 2 };
                for (var k = 0; k < 16; k++)
                    s.Note(Patches.Spiccato, St(k), 0.09f, chord[arp[k % 4]] + 12, bar < 8 ? 0.10f : 0.07f, k % 2 == 0 ? -0.4f : 0.4f);
            }
            else
            {
                // Pont : taikos, basse pédale, roulement qui relance la boucle.
                foreach (var k in new[] { 0, 3, 6, 8, 10, 12 }) s.Tom(St(k), k % 6 == 0 ? 95f : 125f, 0.7f, k % 4 == 0 ? -0.3f : 0.3f);
                s.Taiko(St(0), 0.9f);
                s.Kick(St(0), 1f);
                s.Kick(St(8), 0.9f);
                for (var k = 0; k < 4; k++) s.Note(Patches.Bass, St(k * 4), 0.4f, 28, 0.55f);
                if (bar == 19) s.Roll(St(0), 4 * s.Beat, 8, 0.15f, 0.85f);
                if (bar == 18) s.Riser(St(0), 8 * s.Beat, 0.3f);
            }

            // Cuivres
            if (bar < 8 || bar >= 16)
            {
                var stabs = bar % 2 == 0
                    ? new[] { (0, 3), (6, 2), (10, 2) }
                    : new[] { (0, 2), (3, 2), (8, 8) };
                var c = bar >= 16 ? new[] { Em, Cmaj, Dmaj, Bmaj }[bar - 16] : chord;
                foreach (var (st, len) in stabs)
                    foreach (var m in c) s.Note(len >= 8 ? Patches.Brass : Patches.Stab, St(st), len * 0.25f * s.Beat, m, 0.19f, (m % 3 - 1) * 0.3f);
            }
            else
            {
                foreach (var m in chord) s.Note(Patches.Pad, s.At(bar), s.At(1) - 0.05f, m, 0.07f, (m % 3 - 1) * 0.4f);
                if (bar % 2 == 0) foreach (var m in chord) s.Note(Patches.Stab, St(0), 0.25f * s.Beat * 2, m - 12, 0.12f);
            }
            if (bar % 8 == 0) s.Crash(St(0), 0.5f);
        }
        // Mélodie héroïque (section B)
        (float bar, float beat, float len, int m)[] lead =
        {
            (8, 0, 2, 76), (8, 2, 1, 72), (8, 3, 1, 71), (9, 0, 4, 69),
            (10, 0, 1.5f, 67), (10, 1.5f, 0.5f, 69), (10, 2, 1, 71), (10, 3, 1, 72), (11, 0, 3, 74), (11, 3, 0.5f, 72), (11, 3.5f, 0.5f, 71),
            (12, 0, 2, 71), (12, 2, 2, 76), (13, 0, 4, 76),
            (14, 0, 2, 78), (14, 2, 2, 75), (15, 0, 4, 71),
        };
        foreach (var l in lead)
        {
            s.Note(Patches.Brass, s.At(l.bar, l.beat), l.len * s.Beat - 0.03f, l.m, 0.38f, -0.1f);
            s.Note(Patches.Brass, s.At(l.bar, l.beat), l.len * s.Beat - 0.03f, l.m - 12, 0.2f, 0.15f);
        }
        s.Reverb(0.3f, 0.84f);
        return s;
    }

    /// <summary>Boss : mi phrygien, 128 BPM, 16 mesures (30 s). Doubles grosses caisses, chœur, « braaam ».</summary>
    private static Score BossTheme()
    {
        var s = new Score(128f, 16, 44);
        int[][] chords = { Em, Em, Fmaj, Em, Em, Em, Dmaj, Fmaj };
        for (var bar = 0; bar < 16; bar++)
        {
            var chord = chords[bar % 8];
            var root = chord[0] - 12;
            float St(int step) => s.At(bar, step * 0.25f);

            for (var k = 0; k < 16; k += 4) s.Kick(St(k), 0.8f, 0.22f);
            s.Kick(St(14), 0.6f, 0.18f);
            s.Kick(St(15), 0.7f, 0.18f);
            if (bar % 4 == 3) for (var k = 8; k < 14; k++) s.Kick(St(k), 0.6f, 0.15f);
            s.Snare(St(4), 0.9f);
            s.Snare(St(12), 0.9f);
            s.Snare(St(11), 0.3f, 0.2f);
            for (var k = 0; k < 16; k++) s.Hat(St(k), k % 2 == 0 ? 0.13f : 0.07f, 0.025f);
            if (bar % 2 == 1)
            {
                float[] toms = { 180f, 150f, 120f, 95f };
                for (var k = 8; k < 16; k++) s.Tom(St(k), toms[(k - 8) / 2], 0.5f, (k - 11.5f) * 0.12f);
            }
            if (bar % 4 == 0)
            {
                s.Taiko(St(0), 1f);
                s.Crash(St(0), bar % 8 == 0 ? 0.6f : 0.35f);
                // « Braaam » grave sur deux temps
                foreach (var m in new[] { 28, 40, 47 }) s.Note(Patches.Braaam, St(0), 2 * s.Beat, m, 0.3f);
            }
            // Basse ostinato en doubles croches
            for (var k = 0; k < 16; k++)
            {
                var m = k % 8 == 6 ? root + 1 : k % 8 == 7 ? root + 7 : root;
                s.Note(Patches.Bass, St(k), 0.1f, m, k % 4 == 0 ? 0.5f : 0.35f);
            }
            // Chœur tenu
            foreach (var m in chord) s.Note(Patches.Choir, s.At(bar), s.At(1) - 0.05f, m + 12, 0.07f, (m % 3 - 1) * 0.5f);
            // Cuivres en 3-3-2
            foreach (var (st, len) in new[] { (0, 2), (3, 2), (6, 2), (10, 4) })
                foreach (var m in chord) s.Note(Patches.Stab, St(st), len * 0.25f * s.Beat, m, 0.17f, (m % 3 - 1) * 0.3f);
        }
        (float bar, float beat, float len, int m)[] lead =
        {
            (8, 0, 2, 71), (8, 2, 2, 72), (9, 0, 4, 71), (10, 0, 2, 72), (10, 2, 1, 74), (10, 3, 1, 72), (11, 0, 4, 71),
            (12, 0, 2, 76), (12, 2, 2, 77), (13, 0, 3, 76), (13, 3, 1, 74), (14, 0, 2, 72), (14, 2, 2, 71), (15, 0, 2, 71), (15, 2, 2, 70),
        };
        foreach (var l in lead)
        {
            s.Note(Patches.Brass, s.At(l.bar, l.beat), l.len * s.Beat - 0.03f, l.m, 0.34f, 0.1f);
            s.Note(Patches.Brass, s.At(l.bar, l.beat), l.len * s.Beat - 0.03f, l.m - 12, 0.2f, -0.1f);
        }
        s.Reverb(0.3f, 0.85f);
        return s;
    }

    /// <summary>Fanfare de fin de mission, rendue en mono pour Sfx ("victory" / "defeat").</summary>
    public static float[] RenderStinger(bool victory)
    {
        var s = new Score(4f, victory ? 5 : 6);
        if (victory)
        {
            // Appel en triolet puis accord de mi majeur, roulement de timbales.
            foreach (var t in new[] { 0f, 0.11f, 0.22f }) s.Note(Patches.Stab, t, 0.08f, 71, 0.4f);
            foreach (var m in new[] { 40, 52, 56, 59, 64, 68 }) s.Note(Patches.Brass, 0.36f, 1.9f, m, m < 50 ? 0.3f : 0.22f);
            s.Note(Patches.Brass, 0.36f, 1.9f, 76, 0.15f);
            for (var k = 0; k < 18; k++) s.Tom(0.36f + k * 0.07f, 85f, 0.2f + 0.02f * k);
            s.Taiko(0.36f, 1f);
            s.Taiko(2.3f, 0.7f);
            s.Crash(0.36f, 0.5f);
        }
        else
        {
            // Descente sombre : do majeur, do mineur, puis mi mineur grave.
            foreach (var m in new[] { 48, 52, 55 }) s.Note(Patches.Brass, 0f, 0.55f, m, 0.25f);
            foreach (var m in new[] { 48, 51, 55 }) s.Note(Patches.Brass, 0.6f, 0.55f, m, 0.25f);
            foreach (var m in new[] { 28, 40, 47, 52, 55 }) s.Note(Patches.Braaam, 1.2f, 1.8f, m, 0.28f);
            s.Taiko(0f, 0.6f);
            s.Taiko(0.6f, 0.6f);
            s.Taiko(1.2f, 1f);
        }
        s.Reverb(0.4f, 0.86f);
        return s.FinishFloat();
    }
}

/// <summary>Réglages d'un instrument soustractif (scies + filtre résonant + enveloppes).</summary>
internal struct Patch
{
    public int Unison;
    public float Detune; // centièmes de demi-ton entre voix extrêmes
    public float Attack, Decay, Sustain, Release;
    public float Cut, CutEnv, CutDecay, Q;
    public float Square, Sub, Vibrato, Rev, Width;
}

internal static class Patches
{
    public static readonly Patch Brass = new() { Unison = 3, Detune = 14, Attack = 0.05f, Decay = 0.3f, Sustain = 0.75f, Release = 0.25f, Cut = 280, CutEnv = 2400, CutDecay = 0.8f, Q = 0.9f, Vibrato = 0.004f, Rev = 0.35f, Width = 0.5f };
    public static readonly Patch Stab = new() { Unison = 3, Detune = 16, Attack = 0.008f, Decay = 0.16f, Sustain = 0.35f, Release = 0.1f, Cut = 350, CutEnv = 3200, CutDecay = 0.12f, Q = 1.0f, Rev = 0.3f, Width = 0.6f };
    public static readonly Patch Horn = new() { Unison = 2, Detune = 6, Attack = 0.25f, Decay = 0.5f, Sustain = 0.8f, Release = 0.8f, Cut = 220, CutEnv = 900, CutDecay = 2.5f, Q = 0.8f, Square = 0.3f, Vibrato = 0.003f, Rev = 0.6f, Width = 0.3f };
    public static readonly Patch Pad = new() { Unison = 4, Detune = 16, Attack = 1.4f, Decay = 1f, Sustain = 0.9f, Release = 2.2f, Cut = 800, CutEnv = 500, CutDecay = 4f, Q = 0.7f, Rev = 0.6f, Width = 1f };
    public static readonly Patch Choir = new() { Unison = 4, Detune = 12, Attack = 0.35f, Decay = 0.6f, Sustain = 0.85f, Release = 0.9f, Cut = 1100, CutEnv = 300, CutDecay = 2f, Q = 1.6f, Square = 0.5f, Vibrato = 0.006f, Rev = 0.6f, Width = 1f };
    public static readonly Patch Cello = new() { Unison = 2, Detune = 8, Attack = 0.6f, Decay = 1f, Sustain = 0.85f, Release = 1.2f, Cut = 300, CutEnv = 300, CutDecay = 3f, Q = 0.8f, Sub = 0.4f, Vibrato = 0.004f, Rev = 0.4f, Width = 0.2f };
    public static readonly Patch Bass = new() { Unison = 2, Detune = 6, Attack = 0.004f, Decay = 0.12f, Sustain = 0.5f, Release = 0.05f, Cut = 160, CutEnv = 1000, CutDecay = 0.07f, Q = 1.1f, Square = 0.3f, Sub = 0.4f, Rev = 0.04f, Width = 0.1f };
    public static readonly Patch Pulse = new() { Unison = 2, Detune = 6, Attack = 0.01f, Decay = 0.15f, Sustain = 0.3f, Release = 0.1f, Cut = 140, CutEnv = 400, CutDecay = 0.1f, Q = 0.9f, Sub = 0.5f, Rev = 0.15f, Width = 0.2f };
    public static readonly Patch Spiccato = new() { Unison = 3, Detune = 10, Attack = 0.006f, Decay = 0.08f, Sustain = 0f, Release = 0.05f, Cut = 1100, CutEnv = 1600, CutDecay = 0.05f, Q = 0.8f, Rev = 0.35f, Width = 0.8f };
    public static readonly Patch Braaam = new() { Unison = 4, Detune = 18, Attack = 0.08f, Decay = 0.8f, Sustain = 0.7f, Release = 0.9f, Cut = 110, CutEnv = 1700, CutDecay = 0.9f, Q = 1.3f, Square = 0.5f, Sub = 0.5f, Rev = 0.5f, Width = 0.7f };
}

/// <summary>Filtre d'état variable (topologie TPT, stable à toute fréquence).</summary>
internal struct Svf
{
    private float _ic1, _ic2, _a1, _a2, _a3;
    public float Band;

    public void Set(float fc, float q)
    {
        fc = Math.Clamp(fc, 20f, Music.Rate * 0.45f);
        var g = MathF.Tan(MathF.PI * fc / Music.Rate);
        var k = 1f / q;
        _a1 = 1f / (1f + g * (g + k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    /// <summary>Renvoie la sortie passe-bas ; la passe-bande est dans Band.</summary>
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

/// <summary>Partition rendue dans des tampons stéréo circulaires (les queues de notes reviennent au début : boucle sans couture).</summary>
internal sealed class Score
{
    private const int Rate = Music.Rate;
    private const float Tau = MathF.PI * 2f;
    public readonly int N;
    public readonly float Beat;
    public float Sec => N / (float)Rate;
    private readonly float[] _l, _r, _send;
    private readonly bool _loop;
    private uint _seed;

    public Score(float bpm, int bars, int seed)
    {
        Beat = 60f / bpm;
        N = (int)MathF.Round(bars * 4 * Beat * Rate);
        _l = new float[N];
        _r = new float[N];
        _send = new float[N];
        _loop = true;
        _seed = (uint)(seed * 2654435761u + 1);
    }

    /// <summary>Partition non bouclée (fanfares).</summary>
    public Score(float seconds, int seed) : this(120f, 1, seed)
    {
        N = (int)(seconds * Rate);
        _l = new float[N];
        _r = new float[N];
        _send = new float[N];
        _loop = false;
    }

    public float At(float bar, float beat = 0f) => (bar * 4f + beat) * Beat;

    private int Idx(float t) => (int)(t * Rate);

    private float Noise()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (int)_seed * (1f / int.MaxValue);
    }

    private void Put(int i, float l, float r, float send)
    {
        if (_loop) i %= N;
        else if (i >= N) return;
        _l[i] += l;
        _r[i] += r;
        _send[i] += send;
    }

    private static void PanGains(float pan, out float gl, out float gr)
    {
        var a = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI * 0.25f;
        gl = MathF.Cos(a) * 1.4142f;
        gr = MathF.Sin(a) * 1.4142f;
    }

    public static float Mtof(float m) => 440f * MathF.Pow(2f, (m - 69f) / 12f);

    private static float Blep(float p, float dt)
    {
        if (p < dt) { p /= dt; return p + p - p * p - 1f; }
        if (p > 1f - dt) { p = (p - 1f) / dt; return p * p + p + p + 1f; }
        return 0f;
    }

    /// <summary>Dent de scie à bande limitée (PolyBLEP).</summary>
    private static float Saw(float p, float dt) => 2f * p - 1f - Blep(p, dt);

    // --- Percussions ---

    public void Kick(float t0, float g, float decay = 0.3f)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * decay * 4f);
        var ph = 0f;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            ph += (44f + 120f * MathF.Exp(-t / 0.025f)) / Rate;
            var v = MathF.Sin(ph * Tau) * MathF.Exp(-t / decay) + Noise() * MathF.Exp(-t / 0.0015f) * 0.4f;
            v = MathF.Tanh(v * 1.5f) * g;
            Put(i0 + k, v, v, v * 0.04f);
        }
    }

    public void Snare(float t0, float g, float pan = 0f)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * 0.35f);
        PanGains(pan, out var gl, out var gr);
        var prev = 0f;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            var n = Noise();
            var hp = n - prev;
            prev = n;
            var v = (hp * 0.5f + n * 0.3f) * MathF.Exp(-t / 0.1f)
                + (MathF.Sin(Tau * 190f * t) * 0.7f + MathF.Sin(Tau * 340f * t) * 0.3f) * MathF.Exp(-t / 0.04f);
            v *= g;
            Put(i0 + k, v * gl, v * gr, v * 0.25f);
        }
    }

    /// <summary>Roulement de caisse claire en crescendo (subdiv coups par temps).</summary>
    public void Roll(float t0, float dur, int subdiv, float g0, float g1)
    {
        var n = (int)MathF.Round(dur / Beat * subdiv);
        for (var i = 0; i < n; i++)
            Snare(t0 + i * Beat / subdiv, g0 + (g1 - g0) * i / Math.Max(n - 1, 1), (i % 2 == 0 ? -0.15f : 0.15f));
    }

    public void Tom(float t0, float f, float g, float pan = 0f)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * 0.9f);
        PanGains(pan, out var gl, out var gr);
        var ph = 0f;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            ph += f * (1f + 0.5f * MathF.Exp(-t / 0.03f)) / Rate;
            var v = (MathF.Sin(ph * Tau) * MathF.Exp(-t / 0.3f) + Noise() * MathF.Exp(-t / 0.01f) * 0.3f) * g;
            Put(i0 + k, v * gl, v * gr, v * 0.3f);
        }
    }

    public void Taiko(float t0, float g)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * 2.2f);
        var ph = 0f;
        var lp = 0f;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            ph += (36f + 70f * MathF.Exp(-t / 0.04f)) / Rate;
            lp += (Noise() - lp) * 0.15f;
            var v = MathF.Sin(ph * Tau) * MathF.Exp(-t / 0.6f) + lp * MathF.Exp(-t / 0.06f) * 1.5f;
            v = MathF.Tanh(v * 1.3f) * g;
            Put(i0 + k, v, v, v * 0.45f);
        }
    }

    public void Hat(float t0, float g, float decay = 0.035f, float pan = 0.25f)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * decay * 6f);
        PanGains(pan, out var gl, out var gr);
        float p1 = 0, p2 = 0;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            var n = Noise();
            var h = (n - 2f * p1 + p2) * 0.25f;
            p2 = p1;
            p1 = n;
            var v = h * MathF.Exp(-t / decay) * g;
            Put(i0 + k, v * gl, v * gr, v * 0.1f);
        }
    }

    public void Crash(float t0, float g)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * 3f);
        float pl = 0, pr = 0;
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            var nl = Noise();
            var nr = Noise();
            var metal = MathF.Sign(MathF.Sin(Tau * 540f * t)) + MathF.Sign(MathF.Sin(Tau * 807f * t)) + MathF.Sign(MathF.Sin(Tau * 1131f * t));
            var env = MathF.Min(t / 0.002f, 1f) * MathF.Exp(-t / 0.9f) * g;
            var l = ((nl - pl) * 0.45f + metal * 0.04f) * env;
            var r = ((nr - pr) * 0.45f + metal * 0.04f) * env;
            pl = nl;
            pr = nr;
            Put(i0 + k, l, r, (l + r) * 0.15f);
        }
    }

    /// <summary>Souffle filtré montant (transition vers le début de boucle).</summary>
    public void Riser(float t0, float dur, float g)
    {
        var i0 = Idx(t0);
        var len = (int)(Rate * dur);
        var fl = new Svf();
        var fr = new Svf();
        for (var k = 0; k < len; k++)
        {
            var x = k / (float)len;
            if (k % 16 == 0)
            {
                fl.Set(300f + 3800f * x * x, 2f);
                fr.Set(320f + 3800f * x * x, 2f);
            }
            fl.Run(Noise());
            fr.Run(Noise());
            var env = x * x * g;
            Put(i0 + k, fl.Band * env, fr.Band * env, (fl.Band + fr.Band) * env * 0.2f);
        }
    }

    // --- Instruments mélodiques ---

    public void Note(in Patch p, float t0, float dur, float midi, float g, float pan = 0f)
    {
        var f0 = Mtof(midi);
        var i0 = Idx(t0);
        var len = (int)((dur + p.Release) * Rate);
        var u = Math.Max(p.Unison, 1);
        var ph = new float[u];
        var det = new float[u];
        var ul = new float[u];
        var ur = new float[u];
        for (var v = 0; v < u; v++)
        {
            var x = u == 1 ? 0f : v / (float)(u - 1) * 2f - 1f;
            det[v] = MathF.Pow(2f, x * p.Detune * 0.5f / 1200f);
            ph[v] = Noise() * 0.5f + 0.5f;
            PanGains(Math.Clamp(pan + x * p.Width, -1f, 1f), out ul[v], out ur[v]);
        }
        PanGains(pan, out var gl, out var gr);
        var norm = g / MathF.Sqrt(u);
        var fl = new Svf();
        var fr = new Svf();
        var subPh = 0f;
        var attack = Math.Max(p.Attack, 0.001f);
        var decay = Math.Max(p.Decay, 0.001f);
        var release = Math.Max(p.Release, 0.001f);
        var sustainAt = EnvAds(dur, attack, decay, p.Sustain);
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            float amp;
            if (t < dur) amp = EnvAds(t, attack, decay, p.Sustain);
            else
            {
                var r = 1f - (t - dur) / release;
                amp = r <= 0f ? 0f : sustainAt * r * r;
            }
            var vib = 1f + p.Vibrato * MathF.Sin(Tau * 5.5f * t) * MathF.Min(t / 0.4f, 1f);
            float sl = 0, sr = 0;
            for (var v = 0; v < u; v++)
            {
                var dt = f0 * det[v] * vib / Rate;
                var q = ph[v] + dt;
                if (q >= 1f) q -= 1f;
                ph[v] = q;
                var s = Saw(q, dt);
                if (p.Square > 0f)
                {
                    var q2 = q + 0.5f;
                    if (q2 >= 1f) q2 -= 1f;
                    s = s * (1f - p.Square) + (s - Saw(q2, dt)) * 0.5f * p.Square;
                }
                sl += s * ul[v];
                sr += s * ur[v];
            }
            if ((k & 15) == 0)
            {
                var cut = p.Cut + p.CutEnv * amp * MathF.Exp(-t / Math.Max(p.CutDecay, 0.001f));
                fl.Set(cut, p.Q);
                fr.Set(cut, p.Q);
            }
            var yl = fl.Run(sl);
            var yr = fr.Run(sr);
            if (p.Sub > 0f)
            {
                subPh += f0 / Rate;
                if (subPh >= 1f) subPh -= 1f;
                var sub = MathF.Sin(subPh * Tau) * p.Sub;
                yl += sub * gl;
                yr += sub * gr;
            }
            var a = amp * norm;
            Put(i0 + k, yl * a, yr * a, (yl + yr) * 0.5f * a * p.Rev);
        }
    }

    private static float EnvAds(float t, float a, float d, float s)
    {
        if (t < a) return t / a;
        var x = (t - a) / d;
        return x >= 1f ? s : 1f - (1f - s) * x;
    }

    /// <summary>Cloche / piano lointain : partiels inharmoniques à décroissance différenciée.</summary>
    public void Bell(float t0, float midi, float g, float pan = 0f)
    {
        var f = Mtof(midi);
        var i0 = Idx(t0);
        var len = (int)(Rate * 3.5f);
        PanGains(pan, out var gl, out var gr);
        float[] ratio = { 1f, 2f, 3.01f, 4.16f, 5.43f };
        float[] amp = { 1f, 0.45f, 0.22f, 0.12f, 0.06f };
        float[] dec = { 2.4f, 1.4f, 0.8f, 0.45f, 0.3f };
        for (var k = 0; k < len; k++)
        {
            var t = k / (float)Rate;
            var v = 0f;
            for (var h = 0; h < ratio.Length; h++)
                if (f * ratio[h] < Rate * 0.45f) v += MathF.Sin(Tau * f * ratio[h] * t) * amp[h] * MathF.Exp(-t / dec[h]);
            v *= MathF.Min(t / 0.002f, 1f) * g;
            Put(i0 + k, v * gl, v * gr, v * 0.6f);
        }
    }

    /// <summary>Bourdon sur toute la boucle : fréquences quantifiées pour un nombre entier de cycles (aucun clic au bouclage).</summary>
    public void Drone(float midi, float g, float cut, int lfoCycles)
    {
        var cycles = new[] { MathF.Round(Mtof(midi) * Sec), MathF.Round(Mtof(midi) * 1.004f * Sec), MathF.Round(Mtof(midi) * 0.997f * Sec) };
        var fl = new Svf();
        var fr = new Svf();
        for (var pass = 0; pass < 2; pass++)
        {
            for (var k = 0; k < N; k++)
            {
                // Phase exacte en arithmétique entière : (cycles * k mod N) / N
                float P(int c) => (float)((long)cycles[c] * k % N) / N;
                var dt0 = cycles[0] / N;
                var a = Saw(P(0), dt0);
                var b = Saw(P(1), cycles[1] / N);
                var c2 = Saw(P(2), cycles[2] / N);
                var sub = MathF.Sin(P(0) * Tau);
                if ((k & 15) == 0)
                {
                    var lfo = MathF.Sin(Tau * lfoCycles * k / (float)N);
                    fl.Set(cut * (1f + 0.45f * lfo), 1.2f);
                    fr.Set(cut * (1f - 0.45f * lfo), 1.2f);
                }
                var yl = fl.Run(a * 0.6f + b * 0.4f);
                var yr = fr.Run(a * 0.6f + c2 * 0.4f);
                if (pass == 0) continue;
                var l = (yl * 0.5f + sub * 0.5f) * g;
                var r = (yr * 0.5f + sub * 0.5f) * g;
                Put(k, l, r, (l + r) * 0.1f);
            }
        }
    }

    // --- Mixage ---

    /// <summary>Réverbération de Schroeder (peignes amortis + passe-tout), bouclée : deux passes, la première chauffe les tampons.</summary>
    public void Reverb(float wet, float room)
    {
        int[] combs = { 557, 593, 641, 677, 709, 743 };
        int[] allp = { 278, 211 };
        var cl = combs.Select(n => new float[n]).ToArray();
        var cr = combs.Select(n => new float[n + 13]).ToArray();
        var al = allp.Select(n => new float[n]).ToArray();
        var ar = allp.Select(n => new float[n + 7]).ToArray();
        var fl = new float[combs.Length];
        var fr = new float[combs.Length];
        var ic = new int[combs.Length * 2];
        var ia = new int[allp.Length * 2];
        const float damp = 0.35f;
        var passes = _loop ? 2 : 1;
        for (var pass = 0; pass < passes; pass++)
        {
            for (var i = 0; i < N; i++)
            {
                var x = _send[i] * 0.15f;
                float ol = 0, or = 0;
                for (var c = 0; c < combs.Length; c++)
                {
                    ol += Comb(cl[c], ref ic[c * 2], ref fl[c], x, room, damp);
                    or += Comb(cr[c], ref ic[c * 2 + 1], ref fr[c], x, room, damp);
                }
                for (var a = 0; a < allp.Length; a++)
                {
                    ol = AllPass(al[a], ref ia[a * 2], ol);
                    or = AllPass(ar[a], ref ia[a * 2 + 1], or);
                }
                if (pass < passes - 1) continue;
                _l[i] += ol * wet;
                _r[i] += or * wet;
            }
        }
    }

    private static float Comb(float[] buf, ref int idx, ref float filt, float x, float fb, float damp)
    {
        var y = buf[idx];
        filt = y * (1f - damp) + filt * damp;
        buf[idx] = x + filt * fb;
        if (++idx >= buf.Length) idx = 0;
        return y;
    }

    private static float AllPass(float[] buf, ref int idx, float x)
    {
        var b = buf[idx];
        buf[idx] = x + b * 0.5f;
        if (++idx >= buf.Length) idx = 0;
        return b - x;
    }

    /// <summary>Normalise, sature doucement et convertit en PCM 16 bits (entrelacé si stéréo).</summary>
    public byte[] Finish(bool stereo)
    {
        Master();
        var ch = stereo ? 2 : 1;
        var data = new byte[N * 2 * ch];
        for (var i = 0; i < N; i++)
        {
            if (stereo)
            {
                Write(data, i * 2, _l[i]);
                Write(data, i * 2 + 1, _r[i]);
            }
            else Write(data, i, (_l[i] + _r[i]) * 0.5f);
        }
        return data;
    }

    public float[] FinishFloat()
    {
        Master();
        var m = new float[N];
        for (var i = 0; i < N; i++) m[i] = (_l[i] + _r[i]) * 0.5f;
        return m;
    }

    private void Master()
    {
        var peak = 1e-6f;
        for (var i = 0; i < N; i++) peak = MathF.Max(peak, MathF.Max(MathF.Abs(_l[i]), MathF.Abs(_r[i])));
        const float drive = 1.6f;
        var k = drive / peak;
        var norm = 0.89f / MathF.Tanh(drive);
        for (var i = 0; i < N; i++)
        {
            _l[i] = MathF.Tanh(_l[i] * k) * norm;
            _r[i] = MathF.Tanh(_r[i] * k) * norm;
        }
    }

    private static void Write(byte[] d, int i, float v)
    {
        var s = (short)Math.Clamp((int)(v * 32767f), -32768, 32767);
        d[i * 2] = (byte)(s & 0xff);
        d[i * 2 + 1] = (byte)((s >> 8) & 0xff);
    }
}
