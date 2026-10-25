using System;
using System.Collections.Generic;
using System.Linq;

namespace Rempart;

/// <summary>Nature de tir d'une arme d'EDF 5 (voir Player.Edf5.cs).</summary>
public enum Edf5Kind
{
    Bullet, Rocket, GuidedRocket, Missile, HandGrenade, GrenadeLauncher, Napalm,
    Acid, Flame, Firecracker, BoundGun, Bomb, Impulse, Reverser, ReverseShooter,
    // Wing Diver : arcs de plasma (Rapier, Phalanx), lances (rayon instantané), éclairs qui ricochent, rayons continus
    // (blasters), impulsions explosives, lasers de longue portée, obus de plasma, lances guidées, sphères d'énergie, cœur plasma.
    Arc, Lance, Spark, Beam, Pulse, Laser, PlasmaShot, Homing, Orb, Core,
    // Fencer : frappe chargée (marteaux, lames, rouleaux, haken), perforation (lances, marteaux-piqueurs), boucliers,
    // mortiers (obus en cloche), équipement de soutien (propulseurs, renforts de bouclier, stabilisateurs, exosquelettes).
    Strike, Pierce, Shield, Mortar, Gear,
}

/// <summary>
/// Attaque d'une arme de mêlée du Fencer : nom d'EDF 5 (« Clash Close Range », « Slash Midium », « Attack 2 »…), seuil de charge
/// (unités de charge, atteintes à la vitesse de charge de l'arme), puissance relative aux dégâts de l'arme, nombre de coups
/// (lames dispersées, lances jumelles), portée (m).
/// </summary>
public readonly record struct Edf5Strike(string Attack, float Charge, float Power, int Count, float Range);

/// <summary>
/// Alimentation des armes de la Wing Diver (atwiki EDF5, « エネルギー供給方式 ») : Ammo = munitions classiques (Ranger) ;
/// Reload = chargeur, recharge payée en énergie (même arme rangée) ; Charge = bouton tenu pour charger (énergie), tir au
/// relâchement, puissance selon la charge ; ChargeBurst = le chargeur se remplit en tenant le bouton puis se vide en rafale ;
/// Direct = un tir puis recharge immédiate payée en énergie (armes guidées, sphères).
/// </summary>
public enum Edf5Supply { Ammo, Reload, Charge, ChargeBurst, Direct }

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
    /// <summary>Classe qui porte l'arme (fixée par <see cref="Edf5Weapons.Template"/>).</summary>
    public SoldierClass Class;
    public Edf5Supply Supply = Edf5Supply.Ammo;
    /// <summary>Wing Diver : énergie consommée par une recharge (ou une charge complète).</summary>
    public Edf5Stat Energy;
    // Cœurs plasma : énergie maximale (étoiles), recharge permanente, recharge d'urgence (mêmes étoiles), coûts du vol et de
    // la poussée, gain de vitesse de poussée (avant, côté, arrière), vitesse de charge des armes, vitesse de vol.
    public Edf5Stat MaxEnergy, EmergencyCharge;
    public float EnergyCharge, FlightCost, BoostCost, WeaponCharge, FlightSpeed;
    public (float Forward, float Side, float Back) BoostSpeed;
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

    // Fencer. Strikes : niveaux de charge (frappe) ou attaques enchaînées (lances) ; Swing : vitesse de charge ou d'attaque ;
    // SpinUp : mise en rotation des canons automatiques (s) ; Guard : part des dégâts parée (bouclier, ou pendant une frappe) ;
    // bouclier : durabilité, angle de garde (degrés), rayon et coût (part de la durabilité) du renvoi, vitesse de marche
    // et de rotation portée / en garde.
    public Edf5Strike[] Strikes;
    public Edf5Stat Swing, Durability, SpinUp;
    public float Guard, GuardAngle, ReflectRadius, ReflectCost;
    public (float Speed, float Turn) Carry = (1f, 1f), Stance = (1f, 1f);
    /// <summary>Missiles qui ne se verrouillent qu'avec le kit de guidage d'un Air Raider : tir droit sinon.</summary>
    public bool GuideOnly;
    // Équipement de soutien du Fencer (valeurs absentes = neutres) : poussées et sauts propulsés consécutifs, intervalle des
    // poussées, vitesse horizontale des sauts ; consommation, durabilité, angle et réduction des dégâts des boucliers, réduction
    // du recul ; impact du poids sur la vitesse et la rotation, vitesse de marche.
    public int Dashes, Boosts;
    public float DashInterval = 1f, BoostGain = 1f, ShieldUse = 1f, ShieldDurability = 1f, ShieldAngle, ShieldCut, KnockbackCut,
        RecoilCut, WeightSpeed = 1f, WeightTurn = 1f, WalkSpeed = 1f;

    public static readonly string[] Grades = { "S++", "S+", "S", "A+", "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D", "E", "F", "G" };

    /// <summary>Demi-angle de dispersion (degrés) d'un rang de précision.</summary>
    public static float GradeSpread(float grade) => grade <= 0 ? 0f : 0.1f * MathF.Pow(1.47f, grade - 1f);
}

public static partial class Edf5Weapons
{
    /// <summary>
    /// Modèle d'arme du jeu pour une arme d'EDF 5 : les champs communs (lus par le HUD, l'IA des bots…) prennent
    /// les valeurs à ★5 ; les valeurs réelles selon les étoiles viennent de <see cref="WeaponInstance"/>.
    /// </summary>
    public static WeaponTemplate Template(Edf5Spec s) => Template(s, SoldierClass.Rifleman);

    public static WeaponTemplate Template(Edf5Spec s, SoldierClass cls)
    {
        var kind = s.Kind switch
        {
            Edf5Kind.Bullet or Edf5Kind.BoundGun or Edf5Kind.Acid or Edf5Kind.Arc or Edf5Kind.Lance or Edf5Kind.Spark
                or Edf5Kind.Beam or Edf5Kind.Laser => WeaponKind.Hitscan,
            Edf5Kind.Flame or Edf5Kind.Reverser => WeaponKind.Beam,
            Edf5Kind.Strike or Edf5Kind.Pierce or Edf5Kind.Shield => WeaponKind.Melee,
            _ => WeaponKind.Projectile,
        };
        var rof = s.Rof.Present ? s.Rof.At(5, RaRof, false) : 1f;
        s.Class = cls;
        return new WeaponTemplate(s.Id, s.Name, cls, kind, s.Category,
            s.Damage.At(5, RaDamage, false), rof, Math.Max(1, (int)MathF.Round(s.Ammo.Present ? s.Ammo.At(5, RaAmmo, false) : 1)),
            s.Reload.Present ? s.Reload.At(5, RaReload, true) : 0.5f, s.Range.Present ? s.Range.At(5, RaRange, false) : 300f,
            s.Accuracy.Grade >= 0 ? Edf5Spec.GradeSpread(s.Accuracy.Grade) : 0f, s.Pellets, s.Blast.Present ? s.Blast.At(5, RaBlast, false) : 0f,
            s.Speed.Present ? s.Speed.At(5, RaRange, false) : 0f, 0f, s.Zoom,
            // Armes automatiques : cadence d'au moins 4 coups/s (fusils d'assaut, lance-flammes, rapières, blasters…) ;
            // Wing Diver à alimentation directe : le tir continue tant que le bouton est tenu.
            s.Supply == Edf5Supply.Direct || s.SpinUp.Present || s.Kind == Edf5Kind.Pierce && rof >= 2f || rof >= 4f && s.Burst == 1
                && (s.Kind is Edf5Kind.Bullet or Edf5Kind.Flame or Edf5Kind.Acid or Edf5Kind.BoundGun or Edf5Kind.Reverser || s.Supply == Edf5Supply.Reload),
            s.Level, s);
    }

    // Coefficients Ra de la formule des étoiles (Mu = 0,5) : dégâts et munitions connus (0,5) ; les autres sont estimés.
    public const float RaDamage = 0.5f, RaAmmo = 0.5f, RaRof = 0.25f, RaReload = 0.3f, RaRange = 0.3f, RaBlast = 0.3f, RaLockTime = 0.5f, RaLockRange = 0.3f,
        RaEnergy = 0.3f, RaCapacity = 0.3f, RaSwing = 0.3f, RaSpinUp = 0.5f;

    /// <summary>Rangs de précision perdus par étoile manquante.</summary>
    public const float GradePerStar = 0.5f;

    /// <summary>Toutes les armes d'EDF 5 reprises dans le jeu, par identifiant.</summary>
    /// <remarks>Construit à la première demande : l'ordre d'initialisation des champs statiques entre les fichiers partiels n'est pas garanti.</remarks>
    public static Dictionary<string, Edf5Spec> ById => _byId ??= Ranger.Concat(WingDiver).Concat(Fencer).ToDictionary(s => s.Id);
    private static Dictionary<string, Edf5Spec> _byId;
}
