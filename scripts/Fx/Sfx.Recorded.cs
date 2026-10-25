using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Sons enregistrés (armes, explosions) qui remplacent les sons synthétisés du même nom.
/// Fichiers dans <c>res://assets/sfx/nom_i.wav</c>, préparés depuis des banques CC0 (voir <c>assets/sfx/CREDITS.md</c>).
/// Le gain compense la forte dynamique des enregistrements (crête plafonnée, attaque non écrasée) face aux anciens sons.
/// </summary>
public partial class Sfx
{
    private static readonly (string name, int count, float gainDb)[] Recorded =
    {
        ("rifle", 4, 4f),
        ("rifle_far", 4, 4f),
        ("smg", 3, 3f),
        ("shotgun", 4, 6f),
        ("sniper", 4, 6f),
        ("minigun", 4, 1f),
        ("reload", 1, 1.5f),
        ("empty", 1, 0f),
        ("rocket", 2, 4.5f),
        ("launcher", 2, 3f),
        ("explosion", 2, 4f),
        ("explosion_small", 4, 2f),
        ("collapse", 1, 3f),
        ("laser", 5, 0f),
        ("zap", 4, 0f),
        ("plasma", 3, 0f),
        ("hammer", 4, 3f),
        ("jet_start", 1, 0f),
        ("jet_stop", 1, 0f),
        ("jet_boost", 1, 0f),
    };

    /// <summary>Boucles enregistrées (lues par <see cref="Loop"/>).</summary>
    private static readonly string[] RecordedLoops = { "jet_loop" };

    private readonly Dictionary<string, float> _gain = new();

    /// <summary>Charge les enregistrements (thread principal) ; un fichier absent laisse le son synthétisé en place.</summary>
    private void LoadRecorded()
    {
        foreach (var (name, count, gain) in Recorded)
        {
            var list = new List<AudioStreamWav>();
            for (var i = 0; i < count; i++)
            {
                var path = $"res://assets/sfx/{name}_{i}.wav";
                if (!ResourceLoader.Exists(path) || GD.Load<AudioStreamWav>(path) is not { } file) continue;
                // Copie en mémoire (sans chemin) : la ressource importée est rendue aussitôt et n'est pas signalée
                // comme « encore utilisée » si un lecteur la tient encore à la fermeture.
                list.Add(new AudioStreamWav { Format = file.Format, MixRate = file.MixRate, Stereo = file.Stereo, Data = file.Data });
                file.Dispose();
            }
            if (list.Count == 0) continue;
            _variants[name] = list.ToArray();
            _sounds[name] = list[0];
            _gain[name] = gain;
        }
        LoadRecordedLoops();
        LoadEdf5();
    }

    /// <summary>
    /// Sons des familles d'armes d'EDF 5 (<c>res://assets/sfx/edf5/famille_i.wav</c>, tools/edf5/make_weapon_sfx.py), nommés
    /// « e5_famille », au gain du type de son sur lequel ils sont calés.
    /// </summary>
    private void LoadEdf5()
    {
        foreach (var s in Edf5Weapons.Ranger.Concat(Edf5Weapons.WingDiver).Where(x => x.Kind != Edf5Kind.Core))
        {
            var name = "e5_" + s.Family;
            if (_variants.ContainsKey(name)) continue;
            var list = new List<AudioStreamWav>();
            for (var i = 0; i < 4; i++)
            {
                var path = $"res://assets/sfx/edf5/{s.Family}_{i}.wav";
                if (!ResourceLoader.Exists(path) || GD.Load<AudioStreamWav>(path) is not { } file) continue;
                list.Add(new AudioStreamWav { Format = file.Format, MixRate = file.MixRate, Stereo = file.Stereo, Data = file.Data });
                file.Dispose();
            }
            if (list.Count == 0) continue;
            _variants[name] = list.ToArray();
            _sounds[name] = list[0];
            _gain[name] = Edf5Sounds.GainDb(s);
        }
    }

    /// <summary>Un son (synthétisé ou enregistré) de ce nom existe-t-il ?</summary>
    public static bool Has(string name) => I != null && I._sounds.ContainsKey(name);

    private void LoadRecordedLoops()
    {
        foreach (var name in RecordedLoops)
        {
            var path = $"res://assets/sfx/{name}_0.wav";
            if (!ResourceLoader.Exists(path) || GD.Load<AudioStreamWav>(path) is not { } file) continue;
            var data = file.Data;
            _loops[name] = new AudioStreamWav
            {
                Format = file.Format, MixRate = file.MixRate, Stereo = file.Stereo, Data = data,
                LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = data.Length / 2,
            };
            file.Dispose();
        }
    }

    private float Gain(string name) => _gain.TryGetValue(name, out var g) ? g : 0f;
}
