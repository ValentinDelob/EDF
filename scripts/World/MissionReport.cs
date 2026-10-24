using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Bilan de fin de mission : conversion du butin en progression permanente (armure, armes).</summary>
public class MissionReport
{
    public bool Success;
    public float Elapsed;
    public int Kills;
    public float ArmorGained;
    public int[] Crates = new int[Rempart.Crates.Count];
    public int AutoCollected;
    /// <summary>Armes trouvées : nouvelle, possédée améliorée (avant / après) ou possédée sans amélioration.</summary>
    public readonly List<LootResult> Weapons = new();
    public string MissionName;
    public List<(string name, string cls, int kills, bool downed, bool me)> Squad = new();
    public Difficulty Difficulty;

    /// <summary>Tests (--end-after) : armes ajoutées au butin tiré, pour montrer une arme nouvelle et une arme améliorée.</summary>
    public static List<WeaponInstance> TestDrops;

    public static MissionReport Build(World w, bool success)
    {
        var r = new MissionReport
        {
            Success = success,
            Elapsed = w.Elapsed,
            Kills = w.TeamKills,
            MissionName = w.Mission.Name,
            Difficulty = w.Difficulty,
        };
        for (var i = 0; i < r.Crates.Length; i++) r.Crates[i] = w.CratesCollected[i];
        // Collecte automatique des caisses restantes en Facile et Normal.
        if (success && w.Difficulty <= Difficulty.Normal)
            foreach (var c in w.CratesById.Values)
            {
                r.Crates[(int)c.Type]++;
                r.AutoCollected++;
            }

        var profile = Profile.I;
        var cls = profile.Data.SelectedClass;
        var armor = r.Crates[(int)CrateType.Armor] * Difficulties.ArmorPerCrate[(int)w.Difficulty];
        if (!success) armor *= 0.5f;
        r.ArmorGained = armor;
        profile.AddArmor(cls, armor);

        // Armes : tirées pour la classe jouée, conservées même en cas d'échec.
        var progress = w.Mission.Index / (float)Mathf.Max(1, Missions.All.Length - 1);
        var pity = profile.Data.PityCounter;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var drops = new List<WeaponInstance>();
        for (var i = 0; i < r.Crates[(int)CrateType.Weapon]; i++)
            drops.Add(Loot.RollWeapon(cls, w.Difficulty, progress, rng, ref pity));
        if (TestDrops != null) drops.AddRange(TestDrops);
        TestDrops = null;
        // Les plus étoilées en dernier pour la révélation.
        drops.Sort((a, b) => (a.TotalStars * 1000 + a.Level).CompareTo(b.TotalStars * 1000 + b.Level));
        foreach (var d in drops) r.Weapons.Add(profile.Grant(d));
        profile.Data.PityCounter = pity;

        r.Squad = w.ScoreRows();
        profile.Data.TotalKills += r.Kills;
        profile.Data.MissionsPlayed++;
        if (success) profile.MarkCleared(w.Mission.Index, w.Difficulty);
        profile.Save();
        return r;
    }
}
