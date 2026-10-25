using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
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
            "Balise prioritaire", "Frappes d'artillerie, tourelles et champ de soin. Gagne des crédits d'appel : barrage d'artillerie ou véhicule (char, hélicoptère).",
            new Color(0.55f, 0.48f, 0.34f), new Color(0.75f, 0.55f, 0.15f), 3),
        new(SoldierClass.Heavy, "Grenadier blindé", "Ligne de front", 400, 4.2f, 18f,
            "Ancrage", "Exosquelette : -50 % de dégâts et aucun recul pendant 8 s. Poussées propulsées. Deux jeux de deux armes.",
            new Color(0.3f, 0.32f, 0.35f), new Color(0.75f, 0.2f, 0.12f), 4),
    };

    public static ClassDef Get(SoldierClass c) => All[(int)c];

    /// <summary>
    /// Le Grenadier blindé porte deux jeux de deux armes (emplacements 1-2 = jeu 1, 3-4 = jeu 2) ;
    /// les autres classes n'ont qu'un jeu.
    /// </summary>
    public static bool HasWeaponSets(SoldierClass c) => c == SoldierClass.Heavy;

    /// <summary>Nombre d'armes par jeu (Grenadier blindé).</summary>
    public const int SetSize = 2;
}

/// <summary>
/// Appels de véhicule de l'Opérateur d'appui : les crédits d'appel se gagnent en infligeant des dégâts et repartent de zéro
/// à chaque mission. L'entier du type circule sur le réseau (RPC VehicleCallRequest / VehicleCalled).
/// </summary>
public static class CallIns
{
    public const int Tank = 0, Heli = 1, Count = 2;
    public static readonly string[] Names = { "Char", "Hélico" };
    public static readonly float[] Costs = { 500f, 700f };
    /// <summary>Plafond des crédits d'appel.</summary>
    public const float Cap = 1000f;
    /// <summary>Coût du barrage d'artillerie (capacité de classe).</summary>
    public const float BarrageCost = 300f;
    /// <summary>Durée d'appui sur la touche d'appel pour commander le véhicule (un appui bref change de véhicule).</summary>
    public const float HoldTime = 0.6f;
    /// <summary>Portée maximale du point d'appel (distance horizontale au soldat).</summary>
    public const float Range = 80f;
}

public enum WeaponKind { Hitscan, Projectile, Beam, Melee, Airstrike, Turret, HealField }

/// <summary>Caractéristiques d'une arme qui portent des étoiles (0 = minimum, <see cref="WeaponInstance.MaxStars"/> = maximum).</summary>
/// <remarks>Blast, LockTime, LockRange : armes d'EDF 5 seulement (explosion, temps et portée de verrouillage).</remarks>
public enum WeaponStat { Damage = 0, FireRate = 1, Magazine = 2, Reload = 3, Accuracy = 4, Range = 5, Blast = 6, LockTime = 7, LockRange = 8 }

/// <summary>Modèle d'arme : valeurs de base au niveau 0, sans étoile.</summary>
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
    float MinLevel = 0,
    Edf5Spec Spec = null); // arme d'EDF 5 : valeurs, étoiles et tir viennent de Spec (voir Edf5Weapons)

public static class Weapons
{
    private static readonly WeaponTemplate[] Legacy =
    {
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

    /// <summary>Toutes les armes : Fusilier = armes du Ranger d'EDF 5 ; autres classes : armes propres au jeu (en attendant EDF 5).</summary>
    public static readonly WeaponTemplate[] All = Edf5Weapons.Ranger.Select(Edf5Weapons.Template).Concat(Legacy).ToArray();

    private static readonly Dictionary<string, WeaponTemplate> ById = All.ToDictionary(w => w.Id);

    public static WeaponTemplate Get(string id) => id != null && ById.TryGetValue(id, out var t) ? t : All[0];

    public static bool Exists(string id) => id != null && ById.ContainsKey(id);

    /// <summary>Types d'arme (catégories) d'une classe, dans l'ordre des données : une colonne par type dans l'arsenal.</summary>
    public static List<string> CategoriesFor(SoldierClass c) => ForClass(c).Select(t => t.Category).Distinct().ToList();

    /// <summary>Intitulé de colonne (pluriel) d'un type d'arme.</summary>
    public static string CategoryTitle(string category) => category switch
    {
        "Fusil d'assaut" => "Fusils d'assaut",
        "Fusil à pompe" => "Fusils à pompe",
        "Fusil de précision" => "Précision",
        "Lance-roquettes" => "Lance-roquettes",
        "Lance-grenades" => "Lance-grenades",
        "Laser à rayon" => "Lasers",
        "Lance électrique" => "Électriques",
        "Canon à plasma" => "Plasma",
        "Fusil à particules" => "Particules",
        "Arme de défense" => "Défense",
        "Frappe d'artillerie" => "Artillerie",
        "Tourelle automatique" => "Tourelles",
        "Champ de soin" => "Soins",
        "Bombardement en ligne" => "Bombardements",
        "Mitrailleuse rotative" => "Mitrailleuses",
        "Canon de siège" => "Canons de siège",
        "Marteau à énergie" => "Marteaux",
        "Lance-missiles" => "Lance-missiles",
        "Grenades" => "Grenades",
        "Armes spéciales" => "Spéciales",
        _ => category,
    };

    /// <summary>Courte description d'usage selon la nature de l'arme (panneau de l'arsenal).</summary>
    public static string Usage(WeaponTemplate t) => t.Kind switch
    {
        WeaponKind.Hitscan when t.Pellets > 1 => $"Tire une gerbe de {t.Pellets} projectiles, redoutable à courte portée.",
        WeaponKind.Hitscan when t.Zoom > 1 => "Tir de précision à longue portée, visée zoomée.",
        WeaponKind.Hitscan => t.Automatic ? "Tir automatique à impact immédiat." : "Tir coup par coup à impact immédiat.",
        WeaponKind.Projectile when t.BlastRadius > 0 => $"Projectiles explosifs (rayon {t.BlastRadius:0} m).",
        WeaponKind.Projectile => "Projectiles à trajectoire.",
        WeaponKind.Beam => "Rayon continu alimenté par le réacteur.",
        WeaponKind.Melee => "Arme de corps à corps à onde de choc.",
        WeaponKind.Airstrike => "Balise : appelle une frappe sur la zone visée.",
        WeaponKind.Turret => "Déploie une tourelle qui tire seule sur les ennemis.",
        WeaponKind.HealField => "Déploie un champ qui soigne les alliés.",
        _ => "",
    };

    public static IEnumerable<WeaponTemplate> ForClass(SoldierClass c) => All.Where(w => w.Class == c);

    public static readonly Dictionary<SoldierClass, string[]> StarterLoadout = new()
    {
        [SoldierClass.Rifleman] = new[] { "pa11", "grantm31" }, // départ du Ranger d'EDF 5
        [SoldierClass.Airborne] = new[] { "lz1", "pl7" },
        [SoldierClass.Support] = new[] { "pdw5", "art1", "med3" },
        [SoldierClass.Heavy] = new[] { "gt40", "cs90", "mh2", "ms6" }, // jeu 1 : mitrailleuse + canon ; jeu 2 : marteau + missiles
    };
}

/// <summary>
/// Arme possédée par le joueur : modèle + niveau + étoiles par caractéristique (aucune rareté).
/// Chaque caractéristique a de 0 (minimum : valeur du modèle au niveau de l'arme) à <see cref="MaxStars"/> étoiles ;
/// chaque étoile ajoute un bonus chiffré, toujours favorable.
/// </summary>
public class WeaponInstance
{
    /// <summary>Nombre maximal d'étoiles par caractéristique (réglage unique).</summary>
    public const int MaxStars = 5;
    public static readonly int StatCount = Enum.GetValues<WeaponStat>().Length;

    public string Uid { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string TemplateId { get; set; } = "fa21";
    public int Level { get; set; }
    /// <summary>Étoiles par caractéristique, indexées par <see cref="WeaponStat"/> (0..MaxStars).</summary>
    public int[] Stars { get; set; } = new int[StatCount];
    public bool IsNew { get; set; }

    /// <summary>Anciennes sauvegardes (version 1) : rareté 0..4, lue uniquement pour la migration.</summary>
    [JsonPropertyName("Rarity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyRarity { get; set; }

    public static readonly string[] StatNames = { "Dégâts", "Cadence", "Chargeur", "Rechargement", "Précision", "Portée", "Explosion", "Verrouillage", "Portée de verrouillage" };

    // Bonus par étoile (relatifs à la valeur sans étoile au niveau de l'arme).
    public const float DamagePerStar = 0.06f;
    public const float FireRatePerStar = 0.04f;
    public const float MagazinePerStar = 0.08f;
    public const float ReloadPerStar = 0.07f;
    public const float AccuracyPerStar = 0.10f;
    public const float RangePerStar = 0.05f;

    [JsonIgnore] public WeaponTemplate T => Weapons.Get(TemplateId);

    /// <summary>Copie indépendante (instantané avant / après d'une amélioration).</summary>
    public WeaponInstance Clone() => new()
    {
        Uid = Uid, TemplateId = TemplateId, Level = Level, IsNew = IsNew,
        Stars = (int[])(Stars ?? new int[StatCount]).Clone(),
    };

    /// <summary>
    /// Fusionne un autre exemplaire du même modèle dans celui-ci : niveau le plus haut et, pour chaque caractéristique,
    /// le plus grand nombre d'étoiles. Renvoie vrai si quelque chose a été amélioré.
    /// </summary>
    public bool MergeFrom(WeaponInstance o)
    {
        var improved = false;
        if (o.Level > Level) { Level = o.Level; improved = true; }
        if (Stars == null || Stars.Length != StatCount)
        {
            var s = new int[StatCount];
            if (Stars != null) Array.Copy(Stars, s, Math.Min(s.Length, Stars.Length));
            Stars = s;
        }
        foreach (var st in Enum.GetValues<WeaponStat>())
            if (o.StarsOf(st) > StarsOf(st)) { Stars[(int)st] = o.StarsOf(st); improved = true; }
        return improved;
    }

    /// <summary>Étoiles d'une caractéristique, bornées (une sauvegarde ou un MaxStars modifié restent valides).</summary>
    public int StarsOf(WeaponStat s)
    {
        var i = (int)s;
        return Stars != null && i < Stars.Length ? Math.Clamp(Stars[i], 0, MaxStarsOf(s)) : 0;
    }

    /// <summary>Étoiles maximales d'une caractéristique : propres à chaque arme d'EDF 5 (0 = valeur fixe), sinon <see cref="MaxStars"/>.</summary>
    public static int MaxStarsOf(WeaponTemplate t, WeaponStat s)
    {
        if (t.Spec is not { } e) return HasStat(t, s) ? MaxStars : 0;
        return s switch
        {
            WeaponStat.Damage => e.Damage.Stars,
            WeaponStat.FireRate => e.Rof.Stars,
            WeaponStat.Magazine => e.Ammo.Stars,
            WeaponStat.Reload => e.Reload.Stars,
            WeaponStat.Accuracy => e.Accuracy.Stars,
            WeaponStat.Range => Math.Max(e.Range.Stars, e.Search.Stars),
            WeaponStat.Blast => e.Blast.Stars,
            WeaponStat.LockTime => e.LockTime.Stars,
            WeaponStat.LockRange => e.LockRange.Stars,
            _ => 0,
        };
    }

    public int MaxStarsOf(WeaponStat s) => MaxStarsOf(T, s);

    /// <summary>La caractéristique a-t-elle un sens pour ce modèle (sinon ni étoile ni ligne affichée) ?</summary>
    public static bool HasStat(WeaponTemplate t, WeaponStat s) => t.Spec is { } e ? s switch
    {
        WeaponStat.Damage => e.Damage.Present,
        WeaponStat.FireRate => e.Rof.Present,
        WeaponStat.Magazine => e.Ammo.Present,
        WeaponStat.Reload => e.Reload.Present,
        WeaponStat.Accuracy => e.Accuracy.Grade >= 0,
        WeaponStat.Range => e.Range.Present || e.Search.Present,
        WeaponStat.Blast => e.Blast.Present,
        WeaponStat.LockTime => e.LockTime.Present,
        WeaponStat.LockRange => e.LockRange.Present,
        _ => false,
    } : s switch
    {
        WeaponStat.Damage => true,
        WeaponStat.FireRate => t.Kind is WeaponKind.Hitscan or WeaponKind.Projectile,
        WeaponStat.Magazine => t.Kind is WeaponKind.Hitscan or WeaponKind.Projectile && t.Magazine > 1,
        WeaponStat.Reload => true,
        WeaponStat.Accuracy => t.Kind is WeaponKind.Hitscan or WeaponKind.Projectile && t.Spread > 0,
        WeaponStat.Range => t.Kind is WeaponKind.Hitscan or WeaponKind.Projectile or WeaponKind.Beam or WeaponKind.Turret,
        _ => false,
    };

    public bool HasStat(WeaponStat s) => HasStat(T, s);

    private float LevelMul => 1f + 0.05f * Level;
    private Edf5Spec E5 => T.Spec;

    // Valeurs avec un nombre d'étoiles donné. Armes d'EDF 5 : formule des étoiles du jeu (Edf5Stat.At) ;
    // autres armes : valeur du modèle au niveau de l'arme + bonus par étoile.
    private float DamageAt(int s) => E5 is { } e ? e.Damage.At(s, Edf5Weapons.RaDamage, false) : T.Damage * LevelMul * (1f + DamagePerStar * s);
    private float FireRateAt(int s) => E5 is { } e ? (e.Rof.Present ? e.Rof.At(s, Edf5Weapons.RaRof, false) : 1f)
        : T.FireRate * (1f + 0.004f * Level) * (1f + FireRatePerStar * s);
    private int MagazineAt(int s) => E5 is { } e ? (e.Ammo.Present ? Math.Max(1, (int)MathF.Round(e.Ammo.At(s, Edf5Weapons.RaAmmo, false))) : 1)
        : T.Magazine <= 1 ? Math.Max(1, T.Magazine) : T.Magazine + Math.Max(s, (int)MathF.Round(T.Magazine * MagazinePerStar * s));
    private float ReloadAt(int s) => E5 is { } e ? (e.Reload.Present ? e.Reload.At(s, Edf5Weapons.RaReload, true) : 0.5f)
        : T.Reload / (1f + 0.003f * Level) / (1f + ReloadPerStar * s);
    private float SpreadAt(int s) => E5 is { } e ? (e.Accuracy.Grade < 0 ? 0f
            : Edf5Spec.GradeSpread(e.Accuracy.Grade + Edf5Weapons.GradePerStar * Math.Max(0, e.Accuracy.Stars - s)))
        : T.Spread / (1f + AccuracyPerStar * s);
    private float RangeAt(int s) => E5 is { } e ? (e.Search.Present ? e.Search.At(s, Edf5Weapons.RaRange, false) : e.Range.Present ? e.Range.At(s, Edf5Weapons.RaRange, false) : 300f)
        : T.Range * (1f + 0.002f * Level) * (1f + RangePerStar * s);
    private float BlastAt(int s) => E5 is { } e ? (e.Blast.Present ? e.Blast.At(s, Edf5Weapons.RaBlast, false) : 0f) : T.BlastRadius * (1f + 0.003f * Level);
    private float LockTimeAt(int s) => E5 is { LockTime.Present: true } e ? e.LockTime.At(s, Edf5Weapons.RaLockTime, true) : 0f;
    private float LockRangeAt(int s) => E5 is { LockRange.Present: true } e ? e.LockRange.At(s, Edf5Weapons.RaLockRange, false) : 0f;

    /// <summary>Rang de précision d'EDF 5 avec les étoiles actuelles (« A- »…), null pour les autres armes.</summary>
    [JsonIgnore] public string Grade => E5 is { } e && e.Accuracy.Grade >= 0
        ? Edf5Spec.Grades[Math.Clamp((int)MathF.Round(e.Accuracy.Grade + Edf5Weapons.GradePerStar * Math.Max(0, e.Accuracy.Stars - StarsOf(WeaponStat.Accuracy))), 0, Edf5Spec.Grades.Length - 1)]
        : null;
    /// <summary>Vitesse des projectiles (m/s), liée aux étoiles de portée comme dans EDF 5.</summary>
    [JsonIgnore] public float ShotSpeed => E5 is { Speed.Present: true } e
        ? e.Speed.At(StarsOf(WeaponStat.Range), Edf5Weapons.RaRange, false) : T.ProjectileSpeed;
    [JsonIgnore] public float LockTime => LockTimeAt(StarsOf(WeaponStat.LockTime));
    [JsonIgnore] public float LockRange => LockRangeAt(StarsOf(WeaponStat.LockRange));
    /// <summary>Cibles verrouillables : liées au chargeur (missiles d'EDF 5).</summary>
    [JsonIgnore] public int LockTargets => E5 is { } e && e.LockTargets > 0
        ? Math.Max(1, e.Ammo.Stars > 0 && e.Ammo.Max > 1 ? (int)MathF.Round(e.LockTargets * Magazine / e.Ammo.Max) : e.LockTargets) : 0;

    [JsonIgnore] public float Damage => DamageAt(StarsOf(WeaponStat.Damage));
    [JsonIgnore] public float FireRate => FireRateAt(StarsOf(WeaponStat.FireRate));
    [JsonIgnore] public int Magazine => MagazineAt(StarsOf(WeaponStat.Magazine));
    [JsonIgnore] public float Reload => ReloadAt(StarsOf(WeaponStat.Reload));
    [JsonIgnore] public float Spread => SpreadAt(StarsOf(WeaponStat.Accuracy));
    [JsonIgnore] public float Range => RangeAt(StarsOf(WeaponStat.Range));
    [JsonIgnore] public float BlastRadius => BlastAt(StarsOf(WeaponStat.Blast));

    /// <summary>Indice de précision affiché (100 = tir parfaitement droit).</summary>
    public static float AccuracyScore(float spread) => 100f / (1f + spread * 0.5f);

    [JsonIgnore] public float Dps
    {
        get
        {
            var perShot = Damage * T.Pellets;
            if (T.Kind == WeaponKind.Beam) return Damage;
            var cycle = Magazine / Math.Max(FireRate, 0.01f) + Reload;
            return perShot * Magazine / cycle;
        }
    }

    [JsonIgnore] public string DisplayName => $"{T.Name}  Nv.{Level}";

    /// <summary>Somme des étoiles des caractéristiques utiles (colonne ☆ de l'arsenal).</summary>
    /// <remarks>Armes d'EDF 5 : comme dans le jeu, la plus haute étoile parmi les caractéristiques (« PA-11 ☆7 »).</remarks>
    [JsonIgnore] public int TotalStars => E5 != null
        ? Enum.GetValues<WeaponStat>().Where(HasStat).Select(StarsOf).DefaultIfEmpty(0).Max()
        : Enum.GetValues<WeaponStat>().Where(HasStat).Sum(StarsOf);

    /// <summary>Somme maximale possible pour ce modèle (EDF 5 : étoile maximale la plus haute).</summary>
    [JsonIgnore] public int MaxTotalStars => E5 != null
        ? Enum.GetValues<WeaponStat>().Select(MaxStarsOf).DefaultIfEmpty(0).Max()
        : Enum.GetValues<WeaponStat>().Count(HasStat) * MaxStars;

    /// <summary>Toutes les caractéristiques au maximum (« ★PA-11 » dans EDF 5).</summary>
    [JsonIgnore] public bool Maxed => Enum.GetValues<WeaponStat>().Where(HasStat).All(st => StarsOf(st) >= MaxStarsOf(st));

    private static string Fr(float v, string fmt) => v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Petit bonus : une décimale sous 10 (évite un « +0 » pour une étoile réellement gagnée).</summary>
    private static string Small(float v) => Fr(v, v < 10 ? "0.#" : "0");

    /// <summary>Texte affiché d'une caractéristique : valeur actuelle et bonus apporté par ses étoiles (« (+10) »), toujours favorable.</summary>
    public (string value, string bonus) Describe(WeaponStat s)
    {
        var n = StarsOf(s);
        switch (s)
        {
            case WeaponStat.Damage:
            {
                var pel = T.Pellets > 1 && T.Kind is WeaponKind.Hitscan or WeaponKind.Projectile ? $" ×{T.Pellets}" : "";
                return ($"{Fr(Damage, "0")}{pel}", $"+{Small(Damage - DamageAt(0))}");
            }
            case WeaponStat.FireRate:
                return ($"{Fr(FireRate * 60f, "0")}/min", $"+{Fr((FireRate - FireRateAt(0)) * 60f, "0")}/min");
            case WeaponStat.Magazine:
                return ($"{Magazine}", $"+{Magazine - MagazineAt(0)}");
            case WeaponStat.Reload:
            {
                var fmt = Reload >= 10 ? "0" : "0.0";
                return ($"{Fr(Reload, fmt)} s", $"−{Fr(ReloadAt(0) - Reload, fmt)} s");
            }
            case WeaponStat.Accuracy when Grade != null:
                return (Grade, "");
            case WeaponStat.Accuracy:
                return ($"{Fr(AccuracyScore(Spread), "0")}", $"+{Small(AccuracyScore(Spread) - AccuracyScore(SpreadAt(0)))}");
            case WeaponStat.Blast:
                return ($"{Fr(BlastRadius, "0.0")} m", $"+{Fr(BlastRadius - BlastAt(0), "0.0")} m");
            case WeaponStat.LockTime:
                return ($"{Fr(LockTime, LockTime < 1 ? "0.00" : "0.0")} s", $"−{Fr(LockTimeAt(0) - LockTime, "0.00")} s");
            case WeaponStat.LockRange:
                return ($"{Fr(LockRange, "0")} m / {LockTargets}", $"+{Fr(LockRange - LockRangeAt(0), "0")} m");
            case WeaponStat.Range:
                return ($"{Fr(Range, "0")} m", $"+{Fr(Range - RangeAt(0), "0")} m");
        }
        return ("", "");
    }

    /// <summary>
    /// Migration d'une arme d'ancienne sauvegarde (rareté + 5 étoiles de 1 à 5, 3 = moyenne) vers les étoiles 0..MaxStars
    /// par caractéristique. La rareté devient des étoiles supplémentaires (0, 0, +1, +1, +2) ; la portée, absente,
    /// reprend la moyenne des anciennes étoiles.
    /// </summary>
    public void MigrateLegacy()
    {
        var old = Stars ?? Array.Empty<int>();
        var rarity = Math.Clamp(LegacyRarity ?? 0, 0, 4);
        var bonus = rarity switch { 0 or 1 => 0, 2 or 3 => 1, _ => 2 };
        int Conv(int s) => (int)MathF.Round(Math.Clamp(s - 2 + bonus, 0, 5) * MaxStars / 5f);
        var avg = old.Length > 0 ? (int)MathF.Round((float)old.Average()) : 3;
        var stars = new int[StatCount];
        for (var i = 0; i < StatCount; i++)
            stars[i] = i < 5 ? Conv(i < old.Length ? old[i] : 3) : Conv(avg);
        for (var i = 0; i < StatCount; i++)
            if (!HasStat(T, (WeaponStat)i)) stars[i] = 0;
        Stars = stars;
        LegacyRarity = null;
    }
}

public enum EnemyType { BlackAnt, RedAnt, AcidAnt, Wasp, Scavenger, Nest, Queen, Spider, Drone, Fortress, Centipede, Walker, Tripod, DropShip, Titan,
    AlienTrooper, AlienGuard, AlienFlyer, Portal, Mothership }

public record EnemyDef(
    EnemyType Type,
    string Name,
    float Health,
    float Speed,
    float Damage,
    float AttackRange,
    float AttackCooldown,
    float BaseSize,
    float DropChance,
    bool Flying = false,
    bool Ranged = false,
    bool Static = false,
    bool Boss = false,
    float Giant = 1f)
{
    /// <summary>
    /// Échelle du corps dans le monde (1 = proportions d'origine du modèle) : BaseSize (rapport entre types) × Giant (gigantisme façon EDF).
    /// Tout ce qui dépend du corps la suit : modèle, formes de collision, sondes d'escalade, séparation, centre, sorties de projectiles.
    /// </summary>
    public float Size => BaseSize * Giant;
}

public static class Enemies
{
    // Gigantisme (Giant) calé sur EDF (capture de référence : le soldat arrive sous la tête de la fourmi, les pattes passent au-dessus de lui) :
    // fourmi noire ≈ 11 m de long et 4,3 m de haut, rouge ≈ 13 m, charognard ≈ 8 m, araignée ≈ 12 m d'envergure, guêpe ≈ 6 m, drone ≈ 5 m,
    // mille-pattes ≈ 25 m, reine ≈ 34 m, nid ≈ 34 m (mesures : --showcase, journal « [taille] »). Portées de mêlée agrandies d'autant
    // (la tête est loin du centre) ; vitesses à peine relevées (grandes foulées) ; points de vie inchangés.
    public static readonly EnemyDef[] All =
    {
        new(EnemyType.BlackAnt, "Fourmi noire", 60, 10f, 6, 5.4f, 1.25f, 1f, 0.28f, Giant: 2f),
        new(EnemyType.RedAnt, "Fourmi rouge", 150, 8.8f, 9, 6.6f, 1.3f, 1.15f, 0.45f, Giant: 1.95f),
        new(EnemyType.AcidAnt, "Fourmi acide", 50, 8.2f, 6, 32f, 2.4f, 1f, 0.32f, Ranged: true, Giant: 2f),
        new(EnemyType.Wasp, "Guêpe", 40, 14f, 4, 28f, 1.8f, 0.8f, 0.3f, Flying: true, Ranged: true, Giant: 1.5f),
        new(EnemyType.Scavenger, "Charognard", 90, 13f, 5, 5f, 1.2f, 0.85f, 0.5f, Giant: 1.65f),
        new(EnemyType.Nest, "Nid", 3000, 0, 0, 0, 0, 1f, 1f, Static: true, Giant: 1.5f),
        new(EnemyType.Queen, "Reine", 40000, 5.5f, 40, 12f, 1.8f, 3.8f, 1f, Ranged: true, Boss: true, Giant: 1.35f),
        new(EnemyType.Spider, "Araignée", 70, 12f, 7, 26f, 2.0f, 1f, 0.3f, Ranged: true, Giant: 2f),
        new(EnemyType.Drone, "Drone d'attaque", 80, 15f, 5, 34f, 1.3f, 1f, 0.3f, Flying: true, Ranged: true, Giant: 1.25f),
        // Forteresse marcheuse : Damage = un tir laser (missiles, piétinement et canon en multiples). Déjà colossale : pas de gigantisme.
        new(EnemyType.Fortress, "Forteresse marcheuse", 70000, 2.5f, 5, 170f, 4f, 12f, 1f, Ranged: true, Boss: true),
        // Mille-pattes : morsure au contact et courte giclée d'acide à mi-distance (Damage pour les deux).
        new(EnemyType.Centipede, "Mille-pattes", 360, 8f, 10, 9f, 1.5f, 1.25f, 0.6f, Giant: 2.3f),
        // Machines (modèles construits en mètres, sans gigantisme ; BaseSize règle séparation, écrasement et souffle) :
        // Marcheur ≈ 14 m (Damage = un obus de canon), Tripode ≈ 21 m (Damage = un tic de 0,1 s du rayon),
        // vaisseau de largage ≈ 44 m (Damage = un tir laser de tourelle ; vulnérable seulement par la soute ouverte).
        new(EnemyType.Walker, "Marcheur", 2500, 4.2f, 45, 110f, 5.5f, 3.5f, 0.25f, Ranged: true),
        new(EnemyType.Tripod, "Tripode", 4000, 3.6f, 5, 125f, 10f, 5f, 0.25f, Ranged: true),
        new(EnemyType.DropShip, "Vaisseau de largage", 6000, 12f, 5, 90f, 2.6f, 6f, 1f, Flying: true, Ranged: true),
        // Titan reptilien (spécification §10.2) : kaiju d'environ 60 m, modèle construit en mètres (pas de gigantisme).
        // Damage = un tic de 0,1 s du souffle de feu (piétinement, coup de queue et sol en feu en multiples) ; BaseSize règle
        // l'écrasement du mobilier (force 6) et l'éparpillement du butin. Plaques d'armure à briser : voir Enemy.Titan.cs.
        new(EnemyType.Titan, "Titan reptilien", 120000, 3.2f, 8, 95f, 11f, 10f, 1f, Ranged: true, Boss: true),
        // Aliens (humanoïdes de 4 à 6 m construits en mètres, sans gigantisme ; voir Enemy.Aliens.cs) : fantassin
        // (Damage = un trait d'énergie, rafales de quatre), garde (Damage = coup de bouclier ; son blaster en fait la moitié),
        // volant (Damage = un trait laser) ; portail : structure qui fait passer des groupes d'aliens et de machines.
        new(EnemyType.AlienTrooper, "Fantassin alien", 400, 5.2f, 6, 75f, 3.4f, 1.3f, 0.25f, Ranged: true),
        new(EnemyType.AlienGuard, "Garde alien", 1200, 4.4f, 30, 6.5f, 2.4f, 1.5f, 0.25f),
        new(EnemyType.AlienFlyer, "Volant alien", 300, 13f, 5, 65f, 2.8f, 1.2f, 0.25f, Flying: true, Ranged: true),
        new(EnemyType.Portal, "Portail", 5000, 0, 0, 0, 0, 4f, 1f, Static: true),
        // Vaisseau-mère (boss final, spécification §10.2) : coque de plusieurs centaines de mètres en vol stationnaire au-dessus
        // de la capitale, construite en mètres (pas de gigantisme). Damage = un tic de 0,1 s du canon principal au centre de
        // la colonne (tourelles et retombées en fractions) ; vulnérable seulement par le canon ouvert (voir Enemy.Mothership.cs).
        new(EnemyType.Mothership, "Vaisseau-mère", 500000, 2.4f, 14, 420f, 2f, 10f, 1f, Flying: true, Ranged: true, Boss: true),
    };

    public static EnemyDef Get(EnemyType t) => All[(int)t];
}

public enum Difficulty { Easy = 0, Normal = 1, Hard = 2, Hardest = 3, Inferno = 4 }

public static class Difficulties
{
    public static readonly string[] Names = { "Facile", "Normal", "Difficile", "Chaos", "Inferno" };
    public static readonly float[] EnemyHealth = { 0.6f, 1f, 2f, 4f, 8f };
    public static readonly float[] EnemyDamage = { 0.5f, 1f, 2.5f, 5f, 10f };
    public static readonly int[] LevelMin = { 0, 5, 20, 40, 60 };
    public static readonly int[] LevelMax = { 25, 40, 60, 80, 100 };
    public static readonly float[] ArmorPerCrate = { 1, 1, 2, 3, 4 };
    /// <summary>Plafond d'armure en multijoueur (0 = aucun).</summary>
    public static readonly float[] OnlineArmorCap = { 0, 1500, 3000, 6000, 0 };
}

/// <summary>
/// Types de caisse. L'entier circule sur le réseau (RPC CrateSpawn / CratePicked) et indexe les compteurs :
/// la valeur 3 (anciennes caisses de crédits, supprimées) reste réservée et n'est plus jamais tirée.
/// </summary>
public enum CrateType { Weapon = 0, Armor = 1, Health = 2, Reserved3 = 3 }

public static class Crates
{
    /// <summary>Taille des compteurs de caisses (valeur réservée 3 comprise).</summary>
    public const int Count = 4;
    public static readonly string[] Names = { "Arme", "Armure", "Soin", "" };
    public static readonly Color[] Colors =
    {
        new(0.85f, 0.15f, 0.12f), new(0.2f, 0.75f, 0.25f), new(1f, 0.45f, 0.75f), new(0.5f, 0.5f, 0.5f),
    };
    // Palette d'Okabe et Ito : distincte pour les principales formes de daltonisme.
    private static readonly Color[] ColorblindColors =
    {
        new("e69f00"), new("56b4e9"), new("cc79a7"), new(0.5f, 0.5f, 0.5f),
    };

    /// <summary>Couleur d'une caisse selon l'option d'accessibilité.</summary>
    public static Color Tint(CrateType t) => (Settings.I?.ColorblindCrates == true ? ColorblindColors : Colors)[(int)t];

    public static CrateType Roll(RandomNumberGenerator rng)
    {
        var r = rng.Randf();
        if (r < 0.39f) return CrateType.Weapon;
        if (r < 0.78f) return CrateType.Armor;
        return CrateType.Health;
    }
}
