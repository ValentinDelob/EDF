using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rempart;

public class ProfileData
{
    public int Version { get; set; } = 1;
    public SoldierClass SelectedClass { get; set; } = SoldierClass.Rifleman;
    public Dictionary<string, float> ArmorBonus { get; set; } = new();
    public List<WeaponInstance> Arsenal { get; set; } = new();
    public Dictionary<string, List<string>> Equipped { get; set; } = new();
    public int Credits { get; set; }
    /// <summary>Clé "mission:difficulté" → nombre de victoires.</summary>
    public Dictionary<string, int> Cleared { get; set; } = new();
    public int PityCounter { get; set; }
    public int TotalKills { get; set; }
    /// <summary>Expérience cumulée par classe.</summary>
    public Dictionary<string, int> ClassXp { get; set; } = new();
    public int MissionsPlayed { get; set; }
}

/// <summary>Profil joueur sauvegardé localement (user://profile.json).</summary>
public partial class Profile : Node
{
    public static Profile I { get; private set; }
    public ProfileData Data { get; private set; } = new();

    /// <summary>Les lancements de test automatisés utilisent un profil séparé pour ne pas polluer celui du joueur.</summary>
    private static string FilePath => IsTestRun ? "user://profile_test.json" : "user://profile.json";

    private static bool IsTestRun
    {
        get
        {
            foreach (var a in OS.GetCmdlineUserArgs())
                if (a.StartsWith("--autostart") || a.StartsWith("--autohost") || a.StartsWith("--autojoin") || a.StartsWith("--menu-shots")) return true;
            return false;
        }
    }
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public override void _EnterTree()
    {
        I = this;
        Load();
    }

    public float Armor(SoldierClass c) =>
        Classes.Get(c).BaseArmor + (Data.ArmorBonus.TryGetValue(c.ToString(), out var b) ? b : 0) + (Level(c) - 1) * 3f;

    public const int MaxLevel = 60;

    /// <summary>Expérience nécessaire pour passer du niveau n au niveau n+1.</summary>
    public static int XpForLevel(int n) => (int)(300 * Mathf.Pow(n, 1.3f));

    public int Xp(SoldierClass c) => Data.ClassXp.TryGetValue(c.ToString(), out var x) ? x : 0;

    public int Level(SoldierClass c)
    {
        var xp = Xp(c);
        var lvl = 1;
        while (lvl < MaxLevel && xp >= XpForLevel(lvl)) { xp -= XpForLevel(lvl); lvl++; }
        return lvl;
    }

    /// <summary>Progression (0..1) dans le niveau courant.</summary>
    public float LevelProgress(SoldierClass c)
    {
        var xp = Xp(c);
        var lvl = 1;
        while (lvl < MaxLevel && xp >= XpForLevel(lvl)) { xp -= XpForLevel(lvl); lvl++; }
        return lvl >= MaxLevel ? 1f : xp / (float)XpForLevel(lvl);
    }

    /// <summary>Bonus de dégâts de classe (+1 % par niveau au-delà du premier).</summary>
    public float DamageBonus(SoldierClass c) => 1f + (Level(c) - 1) * 0.01f;

    public void AddXp(SoldierClass c, int amount) => Data.ClassXp[c.ToString()] = Xp(c) + amount;

    public void AddArmor(SoldierClass c, float amount)
    {
        Data.ArmorBonus.TryGetValue(c.ToString(), out var b);
        Data.ArmorBonus[c.ToString()] = b + amount;
    }

    public List<WeaponInstance> Equipped(SoldierClass c)
    {
        var slots = Classes.Get(c).WeaponSlots;
        if (!Data.Equipped.TryGetValue(c.ToString(), out var uids)) uids = new List<string>();
        var list = uids.Select(u => Data.Arsenal.FirstOrDefault(w => w.Uid == u)).Where(w => w != null && w.T.Class == c).ToList();
        // Complète avec l'équipement de départ si besoin.
        foreach (var w in Data.Arsenal.Where(w => w.T.Class == c))
        {
            if (list.Count >= slots) break;
            if (!list.Contains(w)) list.Add(w);
        }
        return list.Take(slots).ToList();
    }

    public void Equip(SoldierClass c, int slot, WeaponInstance w)
    {
        var list = Equipped(c).Select(x => x.Uid).ToList();
        var existing = list.IndexOf(w.Uid);
        if (existing >= 0 && slot < list.Count) (list[existing], list[slot]) = (list[slot], list[existing]);
        else if (slot < list.Count) list[slot] = w.Uid;
        else list.Add(w.Uid);
        Data.Equipped[c.ToString()] = list;
        Save();
    }

    /// <summary>Ajoute une arme ; si le modèle est déjà possédé à un niveau égal ou supérieur, améliore une étoile.</summary>
    public (WeaponInstance weapon, bool upgraded, int statIndex) Grant(WeaponInstance w, RandomNumberGenerator rng)
    {
        var dup = Data.Arsenal.FirstOrDefault(x => x.TemplateId == w.TemplateId && x.Level >= w.Level && x.Rarity >= w.Rarity);
        if (dup != null)
        {
            var candidates = Enumerable.Range(0, 5).Where(i => dup.Stars[i] < 5).ToList();
            if (candidates.Count > 0)
            {
                var stat = candidates[rng.RandiRange(0, candidates.Count - 1)];
                dup.Stars[stat]++;
                return (dup, true, stat);
            }
            Data.Credits += 200 + w.Level * 10;
            return (dup, true, -1);
        }
        w.IsNew = true;
        Data.Arsenal.Add(w);
        return (w, false, -1);
    }

    public bool IsCleared(int mission, Difficulty d) => Data.Cleared.TryGetValue($"{mission}:{(int)d}", out var n) && n > 0;

    public void MarkCleared(int mission, Difficulty d)
    {
        var key = $"{mission}:{(int)d}";
        Data.Cleared.TryGetValue(key, out var n);
        Data.Cleared[key] = n + 1;
    }

    public void Save()
    {
        try
        {
            // Écriture atomique : fichier temporaire unique puis renommage (deux instances locales ne corrompent plus le profil).
            var tmp = $"{FilePath}.{OS.GetProcessId()}.tmp";
            using (var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
                f?.StoreString(JsonSerializer.Serialize(Data, JsonOpts));
            DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(FilePath));
        }
        catch (Exception e)
        {
            GD.PushError("Sauvegarde impossible : " + e.Message);
        }
    }

    private void Load()
    {
        if (FileAccess.FileExists(FilePath))
        {
            try
            {
                using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
                Data = JsonSerializer.Deserialize<ProfileData>(f.GetAsText()) ?? new ProfileData();
            }
            catch (Exception e)
            {
                GD.PushWarning("Profil illisible, réinitialisation : " + e.Message);
                Data = new ProfileData();
            }
        }
        EnsureStarterWeapons();
    }

    private void EnsureStarterWeapons()
    {
        foreach (var (cls, ids) in Weapons.StarterLoadout)
        {
            foreach (var id in ids)
                if (!Data.Arsenal.Any(w => w.TemplateId == id))
                    Data.Arsenal.Add(new WeaponInstance { TemplateId = id, Level = 0, Rarity = Rarity.Standard });
            if (!Data.Equipped.ContainsKey(cls.ToString()))
                Data.Equipped[cls.ToString()] = ids.Select(id => Data.Arsenal.First(w => w.TemplateId == id).Uid).ToList();
        }
    }

    public void ResetProfile()
    {
        Data = new ProfileData();
        EnsureStarterWeapons();
        Save();
    }
}

public static class Loot
{
    /// <summary>Tire une arme pour une classe selon la difficulté et l'avancement (0..1).</summary>
    public static WeaponInstance RollWeapon(SoldierClass c, Difficulty d, float progress, RandomNumberGenerator rng, ref int pity)
    {
        var min = Difficulties.LevelMin[(int)d];
        var max = Difficulties.LevelMax[(int)d];
        var center = Mathf.Lerp(min, max, Mathf.Clamp(progress, 0, 1));
        var level = Mathf.Clamp((int)MathF.Round(center + rng.Randfn(0, (max - min) * 0.18f)), min, max);
        var pool = Weapons.ForClass(c).Where(t => t.MinLevel <= level).ToList();
        var t = pool[rng.RandiRange(0, pool.Count - 1)];

        var total = Rarities.DropWeight.Sum();
        var r = rng.Randf() * total;
        var rarity = Rarity.Standard;
        for (var i = 0; i < Rarities.DropWeight.Length; i++)
        {
            r -= Rarities.DropWeight[i];
            if (r <= 0) { rarity = (Rarity)i; break; }
        }
        pity++;
        if (pity >= 40 && rarity < Rarity.Elite) rarity = Rarity.Elite;
        if (rarity >= Rarity.Elite) pity = 0;

        var stars = new int[5];
        for (var i = 0; i < 5; i++) stars[i] = Mathf.Clamp(rng.RandiRange(1, 3) + (rarity >= Rarity.Prototype ? 1 : 0), 1, 5);
        return new WeaponInstance { TemplateId = t.Id, Level = level, Rarity = rarity, Stars = stars };
    }
}
