using System;
using System.Collections.Generic;
using System.Linq;

namespace Rempart;

/// <summary>Nature de tir d'une arme d'EDF 5 (voir Player.Edf5.cs).</summary>
public enum Edf5Kind
{
    Bullet, Rocket, GuidedRocket, Missile, HandGrenade, GrenadeLauncher, Napalm,
    Acid, Flame, Firecracker, BoundGun, Bomb, Impulse, Reverser, ReverseShooter,
}

/// <summary>
/// Caractéristique à étoiles : valeur au maximum d'étoiles <see cref="Stars"/> (celle des tables d'EDF 5) ;
/// <see cref="Stars"/> = 0 : valeur fixe. <see cref="Max"/> = 0 et <see cref="Stars"/> = 0 : absente.
/// </summary>
public readonly record struct Edf5Stat(float Max, int Stars)
{
    public bool Present => Max != 0 || Stars != 0;

    /// <summary>
    /// Valeur à <paramref name="s"/> étoiles, formule d'EDF 5 (wiki atwiki EDF5, « 装備の仕様 ») :
    /// v(★) = v5 × ((1 − Ra) + Ra × (★/5)^Mu) si une valeur haute est meilleure, v5 × ((1 + Ra) − Ra × (★/5)^Mu) sinon ;
    /// v5 est déduite de la valeur au maximum d'étoiles.
    /// </summary>
    public float At(int s, float ra, bool lowerBetter, float mu = 0.5f)
    {
        if (Stars <= 0) return Max;
        return Max / G(Stars, ra, lowerBetter, mu) * G(Math.Clamp(s, 0, Stars), ra, lowerBetter, mu);
    }

    private static float G(int n, float ra, bool lowerBetter, float mu)
    {
        var k = MathF.Pow(n / 5f, mu);
        return lowerBetter ? 1f + ra - ra * k : 1f - ra + ra * k;
    }
}

/// <summary>Arme d'EDF 5 : valeurs des tables officielles, famille de modèle 3D, nature de tir.</summary>
public sealed class Edf5Spec(string id, string name, string category, Edf5Kind kind, string family, int level)
{
    public readonly string Id = id, Name = name, Category = category, Family = family;
    public readonly Edf5Kind Kind = kind;
    public readonly int Level = level;
    public int Dlc;
    public Edf5Stat Ammo, Rof, Damage, Reload, Range, Speed, Blast, LockRange, LockTime, Search;
    /// <summary>Précision : rang (0 = S++ … 14 = F) au maximum d'étoiles.</summary>
    public (int Grade, int Stars) Accuracy = (-1, 0);
    /// <summary>Dégâts en fin de portée / dégâts à bout portant (1 = pas d'atténuation).</summary>
    public float FarDamage = 1f;
    public int Pellets = 1, Burst = 1, LockTargets;
    public bool Pierce, LaserSight;
    public float Zoom = 1f;
    /// <summary>Guidage des missiles : rang 0 (S++) … 14 ; -1 sans objet.</summary>
    public int Homing = -1;
    /// <summary>Retard d'explosion des grenades (s) ; 0 = à l'impact.</summary>
    public float Fuse;

    public static readonly string[] Grades = { "S++", "S+", "S", "A+", "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D", "E", "F" };

    /// <summary>Demi-angle de dispersion (degrés) d'un rang de précision.</summary>
    public static float GradeSpread(float grade) => grade <= 0 ? 0f : 0.1f * MathF.Pow(1.47f, grade - 1f);
}

public static partial class Edf5Weapons
{
    /// <summary>
    /// Modèle d'arme du jeu pour une arme d'EDF 5 : les champs communs (lus par le HUD, l'IA des bots…) prennent
    /// les valeurs à ★5 ; les valeurs réelles selon les étoiles viennent de <see cref="WeaponInstance"/>.
    /// </summary>
    public static WeaponTemplate Template(Edf5Spec s)
    {
        var kind = s.Kind switch
        {
            Edf5Kind.Bullet or Edf5Kind.BoundGun or Edf5Kind.Acid => WeaponKind.Hitscan,
            Edf5Kind.Flame or Edf5Kind.Reverser => WeaponKind.Beam,
            _ => WeaponKind.Projectile,
        };
        var rof = s.Rof.Present ? s.Rof.At(5, RaRof, false) : 1f;
        var cls = SoldierClass.Rifleman;
        return new WeaponTemplate(s.Id, s.Name, cls, kind, s.Category,
            s.Damage.At(5, RaDamage, false), rof, Math.Max(1, (int)MathF.Round(s.Ammo.Present ? s.Ammo.At(5, RaAmmo, false) : 1)),
            s.Reload.Present ? s.Reload.At(5, RaReload, true) : 0.5f, s.Range.Present ? s.Range.At(5, RaRange, false) : 300f,
            s.Accuracy.Grade >= 0 ? Edf5Spec.GradeSpread(s.Accuracy.Grade) : 0f, s.Pellets, s.Blast.Present ? s.Blast.At(5, RaBlast, false) : 0f,
            s.Speed.Present ? s.Speed.At(5, RaRange, false) : 0f, 0f, s.Zoom,
            // Armes automatiques : cadence d'au moins 4 coups/s (fusils d'assaut, lance-flammes…).
            rof >= 4f && s.Burst == 1 && s.Kind is Edf5Kind.Bullet or Edf5Kind.Flame or Edf5Kind.Acid or Edf5Kind.BoundGun or Edf5Kind.Reverser,
            s.Level, s);
    }

    // Coefficients Ra de la formule des étoiles (Mu = 0,5) : dégâts et munitions connus (0,5) ; les autres sont estimés.
    public const float RaDamage = 0.5f, RaAmmo = 0.5f, RaRof = 0.25f, RaReload = 0.3f, RaRange = 0.3f, RaBlast = 0.3f, RaLockTime = 0.5f, RaLockRange = 0.3f;

    /// <summary>Rangs de précision perdus par étoile manquante.</summary>
    public const float GradePerStar = 0.5f;

    /// <summary>Toutes les armes d'EDF 5 reprises dans le jeu, par identifiant.</summary>
    /// <remarks>Construit à la première demande : l'ordre d'initialisation des champs statiques entre les fichiers partiels n'est pas garanti.</remarks>
    public static Dictionary<string, Edf5Spec> ById => _byId ??= Ranger.ToDictionary(s => s.Id);
    private static Dictionary<string, Edf5Spec> _byId;
}
