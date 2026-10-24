using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rempart;

public class ProfileData
{
    /// <summary>
    /// 1 : rareté + 5 étoiles (1..5) ; 2 : étoiles par caractéristique (0..MaxStars), sans rareté ;
    /// 3 : un seul exemplaire par modèle d'arme, plus de crédits ni d'expérience de classe
    /// (les anciens champs « Credits » et « ClassXp » sont simplement ignorés à la lecture).
    /// </summary>
    public const int CurrentVersion = 3;
    public int Version { get; set; } = CurrentVersion;
    public SoldierClass SelectedClass { get; set; } = SoldierClass.Rifleman;
    public Dictionary<string, float> ArmorBonus { get; set; } = new();
    /// <summary>Armes possédées : au plus un exemplaire par modèle.</summary>
    public List<WeaponInstance> Arsenal { get; set; } = new();
    public Dictionary<string, List<string>> Equipped { get; set; } = new();
    /// <summary>Clé "mission:difficulté" → nombre de victoires.</summary>
    public Dictionary<string, int> Cleared { get; set; } = new();
    public int PityCounter { get; set; }
    public int TotalKills { get; set; }
    /// <summary>Encyclopédie : éliminations par type d'ennemi (nom de l'énumération) ; présent = rencontré.</summary>
    public Dictionary<string, int> Bestiary { get; set; } = new();
    public int MissionsPlayed { get; set; }
}

/// <summary>
/// Résultat de l'attribution d'une arme trouvée : nouvelle arme, arme possédée améliorée (niveau et/ou étoiles)
/// ou arme possédée sans amélioration. <see cref="Before"/> et <see cref="After"/> sont des instantanés.
/// </summary>
public class LootResult
{
    /// <summary>Exemplaire de l'arsenal (nouveau ou amélioré).</summary>
    public WeaponInstance Weapon;
    /// <summary>Arme tirée au butin.</summary>
    public WeaponInstance Dropped;
    /// <summary>État avant (null pour une arme nouvelle) et après l'attribution.</summary>
    public WeaponInstance Before, After;
    public bool IsNew => Before == null;
    public bool Improved => Before != null && (After.Level > Before.Level || ImprovedStats.Any());

    /// <summary>Caractéristiques dont le nombre d'étoiles a augmenté.</summary>
    public IEnumerable<WeaponStat> ImprovedStats => Before == null
        ? Enumerable.Empty<WeaponStat>()
        : Enum.GetValues<WeaponStat>().Where(s => After.HasStat(s) && After.StarsOf(s) > Before.StarsOf(s));
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

    /// <summary>Armure (PV max) : armure de base de la classe + armure ramassée dans les caisses.</summary>
    public float Armor(SoldierClass c) =>
        Classes.Get(c).BaseArmor + (Data.ArmorBonus.TryGetValue(c.ToString(), out var b) ? b : 0);

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

    /// <summary>
    /// Attribue une arme trouvée. L'arsenal garde un seul exemplaire par modèle : une arme nouvelle est ajoutée ;
    /// une arme déjà possédée améliore l'exemplaire existant (niveau le plus haut, et pour chaque caractéristique
    /// le plus grand nombre d'étoiles). Le résultat porte l'état avant / après pour le bilan.
    /// </summary>
    public LootResult Grant(WeaponInstance w)
    {
        var owned = Data.Arsenal.FirstOrDefault(x => x.TemplateId == w.TemplateId);
        if (owned == null)
        {
            w.IsNew = true;
            Data.Arsenal.Add(w);
            return new LootResult { Weapon = w, Dropped = w.Clone(), After = w.Clone() };
        }
        var before = owned.Clone();
        owned.MergeFrom(w);
        return new LootResult { Weapon = owned, Dropped = w, Before = before, After = owned.Clone() };
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
        // Les lancements de test repartent d'un profil neuf (résultats reproductibles), sauf --keep-profile.
        if (IsTestRun && !System.Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--keep-profile") && FileAccess.FileExists(FilePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FilePath));
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
        Migrate();
        EnsureStarterWeapons();
    }

    /// <summary>Mise à niveau d'un profil ancien : la rareté et les étoiles 1..5 deviennent des étoiles 0..MaxStars par caractéristique.</summary>
    private void Migrate()
    {
        Data.Arsenal.RemoveAll(w => w == null);
        if (Data.Version < 2)
            foreach (var w in Data.Arsenal) w.MigrateLegacy();
        foreach (var w in Data.Arsenal)
        {
            w.LegacyRarity = null;
            if (w.Stars == null || w.Stars.Length != WeaponInstance.StatCount)
            {
                var s = new int[WeaponInstance.StatCount];
                if (w.Stars != null) Array.Copy(w.Stars, s, Math.Min(s.Length, w.Stars.Length));
                w.Stars = s;
            }
            for (var i = 0; i < w.Stars.Length; i++) w.Stars[i] = Math.Clamp(w.Stars[i], 0, WeaponInstance.MaxStars);
        }
        MergeDuplicates();
        Data.Version = ProfileData.CurrentVersion;
    }

    /// <summary>
    /// Un seul exemplaire par modèle : les doublons d'un ancien profil sont fusionnés dans le premier exemplaire
    /// (niveau max, étoiles max par caractéristique) et les références d'équipement sont redirigées vers lui.
    /// </summary>
    public void MergeDuplicates()
    {
        var keep = new Dictionary<string, WeaponInstance>();
        var remap = new Dictionary<string, string>();
        foreach (var w in Data.Arsenal.ToList())
        {
            if (!keep.TryGetValue(w.TemplateId, out var k))
            {
                keep[w.TemplateId] = w;
                continue;
            }
            k.MergeFrom(w);
            k.IsNew |= w.IsNew;
            remap[w.Uid] = k.Uid;
            Data.Arsenal.Remove(w);
        }
        if (remap.Count == 0) return;
        foreach (var key in Data.Equipped.Keys.ToList())
            Data.Equipped[key] = Data.Equipped[key].Select(u => remap.TryGetValue(u, out var n) ? n : u).Distinct().ToList();
        GD.Print($"Profil : {remap.Count} doublon(s) d'arme fusionné(s).");
    }

    private void EnsureStarterWeapons()
    {
        foreach (var (cls, ids) in Weapons.StarterLoadout)
        {
            foreach (var id in ids)
                if (!Data.Arsenal.Any(w => w.TemplateId == id))
                    Data.Arsenal.Add(new WeaponInstance { TemplateId = id, Level = 0 });
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

        var w = new WeaponInstance { TemplateId = t.Id, Level = level, Stars = RollStars(t, d, level, rng) };

        // Garantie : sans caractéristique au maximum depuis 30 armes, la suivante en reçoit une.
        pity++;
        var stats = Enum.GetValues<WeaponStat>().Where(s => WeaponInstance.HasStat(t, s)).ToList();
        if (pity >= 30 && !stats.Any(s => w.StarsOf(s) >= WeaponInstance.MaxStars))
            w.Stars[(int)stats[rng.RandiRange(0, stats.Count - 1)]] = WeaponInstance.MaxStars;
        if (stats.Any(s => w.StarsOf(s) >= WeaponInstance.MaxStars)) pity = 0;
        return w;
    }

    /// <summary>
    /// Chance qu'une étoile donnée soit obtenue : grandit avec la difficulté et le niveau de l'arme
    /// (≈ 0,15 en Facile au niveau 0, ≈ 0,7 en Inferno au niveau 100).
    /// </summary>
    public static float StarChance(Difficulty d, int level) =>
        Mathf.Clamp(0.15f + 0.09f * (int)d + 0.0019f * level, 0.05f, 0.9f);

    /// <summary>Tire les étoiles de chaque caractéristique utile : MaxStars essais indépendants (loi binomiale).</summary>
    public static int[] RollStars(WeaponTemplate t, Difficulty d, int level, RandomNumberGenerator rng)
    {
        var p = StarChance(d, level);
        var stars = new int[WeaponInstance.StatCount];
        foreach (var s in Enum.GetValues<WeaponStat>())
        {
            if (!WeaponInstance.HasStat(t, s)) continue;
            var n = 0;
            for (var k = 0; k < WeaponInstance.MaxStars; k++)
                if (rng.Randf() < p) n++;
            stars[(int)s] = n;
        }
        return stars;
    }
}
