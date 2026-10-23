using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

public enum SoldierClass { Rifleman = 0, Airborne = 1, Support = 2, Heavy = 3 }

public record ClassDef(
    SoldierClass Id,
    string Name,
    string Role,
    float BaseArmor,
    float WalkSpeed,
    float SprintSpeed,
    string AbilityName,
    string AbilityDescription,
    Color Primary,
    Color Secondary,
    int WeaponSlots);

public static class Classes
{
    public static readonly ClassDef[] All =
    {
        new(SoldierClass.Rifleman, "Fusilier", "Fantassin polyvalent", 250, 6.0f, 9.5f,
            "Fusée de ralliement", "Les alliés proches infligent +30 % de dégâts pendant 15 s. Roulade d'esquive.",
            new Color(0.36f, 0.42f, 0.30f), new Color(0.18f, 0.2f, 0.17f), 2),
        new(SoldierClass.Airborne, "Commando aéroporté", "Assaut aérien", 150, 7.0f, 14f,
            "Piqué", "Vol au réacteur dorsal. Le piqué inflige des dégâts de zone à l'impact.",
            new Color(0.75f, 0.77f, 0.8f), new Color(0.2f, 0.35f, 0.6f), 2),
        new(SoldierClass.Support, "Opérateur d'appui", "Soutien et frappes", 200, 5.5f, 8f,
            "Balise prioritaire", "Frappes d'artillerie, tourelles et champ de soin. Gagne des crédits d'appel.",
            new Color(0.55f, 0.48f, 0.34f), new Color(0.75f, 0.55f, 0.15f), 3),
        new(SoldierClass.Heavy, "Grenadier blindé", "Ligne de front", 400, 4.2f, 18f,
            "Ancrage", "Exosquelette : -50 % de dégâts et aucun recul pendant 8 s. Poussées propulsées.",
            new Color(0.3f, 0.32f, 0.35f), new Color(0.75f, 0.2f, 0.12f), 2),
    };

    public static ClassDef Get(SoldierClass c) => All[(int)c];
}

public enum WeaponKind { Hitscan, Projectile, Beam, Melee, Airstrike, Turret, HealField }

public enum Rarity { Standard = 0, Reinforced = 1, Elite = 2, Prototype = 3, Legendary = 4 }

public static class Rarities
{
    public static readonly string[] Names = { "Standard", "Renforcée", "Élite", "Prototype", "Légendaire" };
    public static readonly float[] Multiplier = { 1.0f, 1.08f, 1.16f, 1.25f, 1.35f };
    public static readonly float[] DropWeight = { 55f, 28f, 12f, 4.5f, 0.5f };
    public static readonly Color[] Colors =
    {
        new("c9ced3"), new("5bd16e"), new("3fa2ff"), new("b067ff"), new("ffc233"),
    };
}

/// <summary>Modèle d'arme : valeurs de base au niveau 0, qualité 3 étoiles, rareté Standard.</summary>
public record WeaponTemplate(
    string Id,
    string Name,
    SoldierClass Class,
    WeaponKind Kind,
    string Category,
    float Damage,
    float FireRate,
    int Magazine,
    float Reload,
    float Range,
    float Spread,       // degrés
    int Pellets = 1,
    float BlastRadius = 0,
    float ProjectileSpeed = 0,
    float EnergyCost = 0,
    float Zoom = 1f,
    bool Automatic = true,
    float MinLevel = 0);

public static class Weapons
{
    public static readonly WeaponTemplate[] All =
    {
        // Fusilier
        new("fa21", "FA-21 Vigie", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil d'assaut", 9, 10, 40, 1.6f, 260, 1.4f),
        new("fa30", "FA-30 Sentinelle", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil d'assaut", 14, 7.5f, 30, 1.8f, 300, 0.9f, MinLevel: 10),
        new("ca12", "CA-12 Brèche", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil à pompe", 11, 1.4f, 8, 2.2f, 70, 7f, Pellets: 10, Automatic: false),
        new("fp88", "FP-88 Faucheur", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil de précision", 120, 0.9f, 5, 2.4f, 900, 0.05f, Zoom: 4f, Automatic: false, MinLevel: 5),
        new("lr4", "LR-4 Tonnerre", SoldierClass.Rifleman, WeaponKind.Projectile, "Lance-roquettes", 140, 0.7f, 1, 2.2f, 400, 0.3f, BlastRadius: 7, ProjectileSpeed: 90, Automatic: false),
        new("gl6", "GL-6 Averse", SoldierClass.Rifleman, WeaponKind.Projectile, "Lance-grenades", 70, 2.5f, 6, 2.8f, 160, 1.5f, BlastRadius: 5, ProjectileSpeed: 45, MinLevel: 15),
        // Commando aéroporté
        new("lz1", "LZ-1 Lumen", SoldierClass.Airborne, WeaponKind.Beam, "Laser à rayon", 70, 1, 100, 1.4f, 220, 0, EnergyCost: 30),
        new("ae3", "AE-3 Foudre", SoldierClass.Airborne, WeaponKind.Hitscan, "Lance électrique", 26, 2.5f, 1, 0.8f, 45, 6f, Pellets: 8, EnergyCost: 18, Automatic: false),
        new("pl7", "PL-7 Nova", SoldierClass.Airborne, WeaponKind.Projectile, "Canon à plasma", 160, 1, 1, 1.8f, 300, 0.2f, BlastRadius: 8, ProjectileSpeed: 70, EnergyCost: 35, Automatic: false),
        new("pr9", "PR-9 Aiguille", SoldierClass.Airborne, WeaponKind.Hitscan, "Fusil à particules", 180, 1, 1, 1.6f, 800, 0, Zoom: 3f, EnergyCost: 28, Automatic: false, MinLevel: 10),
        // Opérateur d'appui
        new("pdw5", "PDW-5 Écho", SoldierClass.Support, WeaponKind.Hitscan, "Arme de défense", 7, 11, 45, 1.4f, 180, 2f),
        new("art1", "Balise ARTY-1", SoldierClass.Support, WeaponKind.Airstrike, "Frappe d'artillerie", 220, 1, 1, 14f, 300, 0, BlastRadius: 10, Pellets: 6, Automatic: false),
        new("trt2", "Tourelle TR-2", SoldierClass.Support, WeaponKind.Turret, "Tourelle automatique", 8, 9, 1, 25f, 80, 2f, Automatic: false),
        new("med3", "Champ médical M-3", SoldierClass.Support, WeaponKind.HealField, "Champ de soin", 12, 1, 1, 30f, 10, 0, BlastRadius: 9, Automatic: false),
        new("art5", "Balise SALVE-5", SoldierClass.Support, WeaponKind.Airstrike, "Bombardement en ligne", 160, 1, 1, 20f, 300, 0, BlastRadius: 8, Pellets: 12, Automatic: false, MinLevel: 15),
        // Grenadier blindé
        new("gt40", "GT-40 Ouragan", SoldierClass.Heavy, WeaponKind.Hitscan, "Mitrailleuse rotative", 10, 16, 200, 3.5f, 220, 2.6f),
        new("cs90", "CS-90 Bélier", SoldierClass.Heavy, WeaponKind.Projectile, "Canon de siège", 260, 0.5f, 1, 2.5f, 450, 0.2f, BlastRadius: 9, ProjectileSpeed: 110, Automatic: false),
        new("mh2", "MH-2 Enclume", SoldierClass.Heavy, WeaponKind.Melee, "Marteau à énergie", 220, 1.1f, 1, 0.6f, 6, 0, BlastRadius: 4, Automatic: false),
        new("ms6", "MS-6 Essaim", SoldierClass.Heavy, WeaponKind.Projectile, "Lance-missiles", 60, 6, 6, 3f, 350, 4f, BlastRadius: 4, ProjectileSpeed: 60, MinLevel: 12),
        // Armes de haut niveau (difficultés élevées)
        new("fa40", "FA-40 Garde", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil d'assaut", 20, 9, 45, 1.7f, 320, 0.8f, MinLevel: 30),
        new("ca20", "CA-20 Tempête", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil à pompe", 9, 4.5f, 20, 2.6f, 60, 8f, Pellets: 9, MinLevel: 25),
        new("fp120", "FP-120 Titan", SoldierClass.Rifleman, WeaponKind.Hitscan, "Fusil de précision", 420, 0.5f, 3, 3f, 1000, 0.02f, Zoom: 6f, Automatic: false, MinLevel: 40),
        new("lr9", "LR-9 Hydre", SoldierClass.Rifleman, WeaponKind.Projectile, "Lance-roquettes", 95, 0.6f, 1, 2.6f, 380, 3f, Pellets: 3, BlastRadius: 6, ProjectileSpeed: 80, Automatic: false, MinLevel: 35),
        new("lz5", "LZ-5 Faisceau", SoldierClass.Airborne, WeaponKind.Beam, "Laser à rayon", 150, 1, 100, 1.6f, 260, 0, EnergyCost: 38, MinLevel: 30),
        new("ae9", "AE-9 Arc", SoldierClass.Airborne, WeaponKind.Hitscan, "Lance électrique", 48, 2.2f, 1, 0.9f, 55, 7f, Pellets: 10, EnergyCost: 22, Automatic: false, MinLevel: 25),
        new("pl12", "PL-12 Supernova", SoldierClass.Airborne, WeaponKind.Projectile, "Canon à plasma", 420, 1, 1, 2.6f, 320, 0.2f, BlastRadius: 13, ProjectileSpeed: 65, EnergyCost: 55, Automatic: false, MinLevel: 45),
        new("pdw9", "PDW-9 Rafale", SoldierClass.Support, WeaponKind.Hitscan, "Arme de défense", 13, 13, 60, 1.5f, 200, 1.6f, MinLevel: 25),
        new("trt5", "Tourelle TR-5 Lourde", SoldierClass.Support, WeaponKind.Turret, "Tourelle automatique", 22, 9, 1, 30f, 100, 2f, Automatic: false, MinLevel: 30),
        new("art8", "Balise SALVE-8", SoldierClass.Support, WeaponKind.Airstrike, "Bombardement en ligne", 330, 1, 1, 22f, 300, 0, BlastRadius: 10, Pellets: 16, Automatic: false, MinLevel: 40),
        new("gt60", "GT-60 Typhon", SoldierClass.Heavy, WeaponKind.Hitscan, "Mitrailleuse rotative", 18, 20, 300, 4f, 240, 2.2f, MinLevel: 30),
        new("cs150", "CS-150 Colosse", SoldierClass.Heavy, WeaponKind.Projectile, "Canon de siège", 700, 0.35f, 1, 3.2f, 500, 0.15f, BlastRadius: 14, ProjectileSpeed: 120, Automatic: false, MinLevel: 45),
        new("mh6", "MH-6 Marteau-pilon", SoldierClass.Heavy, WeaponKind.Melee, "Marteau à énergie", 520, 0.9f, 1, 0.7f, 7, 0, BlastRadius: 5.5f, Automatic: false, MinLevel: 35),
    };

    public static WeaponTemplate Get(string id) => All.FirstOrDefault(w => w.Id == id) ?? All[0];

    public static IEnumerable<WeaponTemplate> ForClass(SoldierClass c) => All.Where(w => w.Class == c);

    public static readonly Dictionary<SoldierClass, string[]> StarterLoadout = new()
    {
        [SoldierClass.Rifleman] = new[] { "fa21", "lr4" },
        [SoldierClass.Airborne] = new[] { "lz1", "pl7" },
        [SoldierClass.Support] = new[] { "pdw5", "art1", "med3" },
        [SoldierClass.Heavy] = new[] { "gt40", "cs90" },
    };
}

/// <summary>Arme possédée par le joueur : modèle + niveau + rareté + étoiles par statistique.</summary>
public class WeaponInstance
{
    public string Uid { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string TemplateId { get; set; } = "fa21";
    public int Level { get; set; }
    public Rarity Rarity { get; set; }
    /// <summary>Étoiles (1..5) : dégâts, cadence, chargeur, rechargement, précision.</summary>
    public int[] Stars { get; set; } = { 3, 3, 3, 3, 3 };
    public bool IsNew { get; set; }

    public static readonly string[] StatNames = { "Dégâts", "Cadence", "Chargeur", "Rechargement", "Précision" };

    public WeaponTemplate T => Weapons.Get(TemplateId);

    private float Q(int i) => 0.8f + 0.1f * Mathf.Clamp(Stars[i], 1, 5);
    private float LevelMul => 1f + 0.05f * Level;
    private float RarityMul => Rarities.Multiplier[(int)Rarity];

    public float Damage => T.Damage * LevelMul * RarityMul * Q(0);
    public float FireRate => T.FireRate * (1f + 0.25f * (Q(1) - 1.1f)) * (1f + 0.004f * Level);
    public int Magazine => Math.Max(1, (int)MathF.Round(T.Magazine * (T.Magazine > 1 ? Q(2) / 1.1f : 1f)));
    public float Reload => T.Reload / (Q(3) / 1.1f) / (1f + 0.003f * Level);
    public float Spread => T.Spread / (Q(4) / 1.1f);
    public float Range => T.Range * (1f + 0.002f * Level);
    public float BlastRadius => T.BlastRadius * (1f + 0.003f * Level);

    public float Dps
    {
        get
        {
            var perShot = Damage * T.Pellets;
            if (T.Kind == WeaponKind.Beam) return Damage;
            var cycle = Magazine / Math.Max(FireRate, 0.01f) + Reload;
            return perShot * Magazine / cycle;
        }
    }

    public string DisplayName => $"{T.Name}  Nv.{Level}";

    public int TotalStars => Stars.Sum();
}

public enum EnemyType { BlackAnt, RedAnt, AcidAnt, Wasp, Scavenger, Nest, Queen, Spider, Drone }

public record EnemyDef(
    EnemyType Type,
    string Name,
    float Health,
    float Speed,
    float Damage,
    float AttackRange,
    float AttackCooldown,
    float Size,
    float DropChance,
    bool Flying = false,
    bool Ranged = false,
    bool Static = false,
    bool Boss = false);

public static class Enemies
{
    public static readonly EnemyDef[] All =
    {
        new(EnemyType.BlackAnt, "Fourmi noire", 60, 9f, 6, 3.2f, 1.25f, 1f, 0.28f),
        new(EnemyType.RedAnt, "Fourmi rouge", 150, 8f, 9, 3.4f, 1.3f, 1.15f, 0.45f),
        new(EnemyType.AcidAnt, "Fourmi acide", 50, 7.5f, 6, 32f, 2.4f, 1f, 0.32f, Ranged: true),
        new(EnemyType.Wasp, "Guêpe", 40, 13f, 4, 28f, 1.8f, 0.8f, 0.3f, Flying: true, Ranged: true),
        new(EnemyType.Scavenger, "Charognard", 90, 12f, 5, 3f, 1.2f, 0.85f, 0.5f),
        new(EnemyType.Nest, "Nid", 3000, 0, 0, 0, 0, 1f, 1f, Static: true),
        new(EnemyType.Queen, "Reine", 40000, 5f, 40, 9f, 1.8f, 3.8f, 1f, Ranged: true, Boss: true),
        new(EnemyType.Spider, "Araignée", 70, 11f, 7, 26f, 2.0f, 1f, 0.3f, Ranged: true),
        new(EnemyType.Drone, "Drone d'attaque", 80, 15f, 5, 34f, 1.3f, 1f, 0.3f, Flying: true, Ranged: true),
    };

    public static EnemyDef Get(EnemyType t) => All[(int)t];
}

public enum Difficulty { Easy = 0, Normal = 1, Hard = 2, Hardest = 3, Inferno = 4 }

public static class Difficulties
{
    public static readonly string[] Names = { "Facile", "Normal", "Difficile", "Très difficile", "Inferno" };
    public static readonly float[] EnemyHealth = { 0.6f, 1f, 2f, 4f, 8f };
    public static readonly float[] EnemyDamage = { 0.5f, 1f, 2.5f, 5f, 10f };
    public static readonly int[] LevelMin = { 0, 5, 20, 40, 60 };
    public static readonly int[] LevelMax = { 25, 40, 60, 80, 100 };
    public static readonly float[] ArmorPerCrate = { 1, 1, 2, 3, 4 };
    public static readonly float[] XpMul = { 0.75f, 1f, 1.5f, 2f, 3f };
}

public enum CrateType { Weapon = 0, Armor = 1, Health = 2, Credits = 3 }

public static class Crates
{
    public static readonly string[] Names = { "Arme", "Armure", "Soin", "Crédits" };
    public static readonly Color[] Colors =
    {
        new(0.85f, 0.15f, 0.12f), new(0.2f, 0.75f, 0.25f), new(1f, 0.45f, 0.75f), new(1f, 0.8f, 0.15f),
    };

    public static CrateType Roll(RandomNumberGenerator rng)
    {
        var r = rng.Randf();
        if (r < 0.36f) return CrateType.Weapon;
        if (r < 0.72f) return CrateType.Armor;
        if (r < 0.92f) return CrateType.Health;
        return CrateType.Credits;
    }
}
