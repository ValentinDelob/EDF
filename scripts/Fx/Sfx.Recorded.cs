using System.Collections.Generic;
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
    };

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
    }

    private float Gain(string name) => _gain.TryGetValue(name, out var g) ? g : 0f;
}
