using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Bilan de fin de mission : conversion du butin en progression permanente.</summary>
public class MissionReport
{
    public bool Success;
    public float Elapsed;
    public int Kills;
    public float ArmorGained;
    public int CreditsGained;
    public int[] Crates = new int[4];
    public int AutoCollected;
    public readonly List<(WeaponInstance weapon, bool upgraded, int stat)> Weapons = new();
    public string MissionName;
    public int XpGained;
    public List<(string name, string cls, int kills, bool downed, bool me)> Squad = new();
    public int LevelBefore, LevelAfter;
    public Difficulty Difficulty;

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
        for (var i = 0; i < 4; i++) r.Crates[i] = w.CratesCollected[i];
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

        if (success)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            var credits = r.Crates[(int)CrateType.Credits] * rng.RandiRange(80, 250) + 300 * ((int)w.Difficulty + 1);
            r.CreditsGained = credits;
            profile.Data.Credits += credits;
        }

        // Armes : tirées pour la classe jouée, conservées même en cas d'échec.
        var progress = w.Mission.Index / (float)Mathf.Max(1, Missions.All.Length - 1);
        var pity = profile.Data.PityCounter;
        var rng2 = new RandomNumberGenerator();
        rng2.Randomize();
        for (var i = 0; i < r.Crates[(int)CrateType.Weapon]; i++)
        {
            var wpn = Loot.RollWeapon(cls, w.Difficulty, progress, rng2, ref pity);
            r.Weapons.Add(profile.Grant(wpn, rng2));
        }
        profile.Data.PityCounter = pity;
        // Expérience de classe : éliminations de l'équipe + bonus de victoire, selon la difficulté.
        r.Squad = w.ScoreRows();
        r.LevelBefore = profile.Level(cls);
        r.XpGained = (int)((r.Kills * 8 + (success ? 600 + 250 * w.Mission.Index : 150)) * Difficulties.XpMul[(int)w.Difficulty]);
        profile.AddXp(cls, r.XpGained);
        r.LevelAfter = profile.Level(cls);
        profile.Data.TotalKills += r.Kills;
        profile.Data.MissionsPlayed++;
        if (success) profile.MarkCleared(w.Mission.Index, w.Difficulty);
        profile.Save();
        // Les plus rares en dernier pour la révélation.
        r.Weapons.Sort((a, b) => a.weapon.Rarity.CompareTo(b.weapon.Rarity));
        return r;
    }
}
