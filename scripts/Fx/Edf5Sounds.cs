using Godot;

namespace Rempart;

/// <summary>
/// Sons de tir des armes d'EDF 5 par famille : son de base, volume et hauteur propres à chaque famille
/// (un PA-11 ne sonne pas comme un Raven ni un KFF). Les sons du joueur local sont non spatialisés, les autres situés.
/// </summary>
public static class Edf5Sounds
{
    private static (string snd, float db, float pitch) For(Edf5Spec s) => s.Family switch
    {
        "pa11" => ("rifle", -3f, 1f),
        "minion" => ("smg", -4f, 0.85f),
        "slugger" => ("rifle", -2f, 0.82f),
        "gm" => ("rifle", -1f, 0.92f),
        "raven" => ("smg", -5f, 1.12f),
        "stork" => ("rifle", -3f, 0.95f),
        "orchid" => ("rifle", -2f, 1.06f),
        "slade" => ("rifle", -1f, 0.88f),
        "blazer" => ("laser", -3f, 0.8f),
        "slaughter" or "sgn" or "herring" or "breecher" or "monsoon" or "fracture" => ("shotgun", -1f, s.Family == "breecher" ? 0.85f : s.Family == "monsoon" ? 1.08f : 1f),
        "slugshot" or "bustershot" => ("shotgun", 0f, 0.8f),
        "kff" or "np" or "eagle" or "fang" => ("sniper", 1f, s.Family == "np" ? 1.12f : 1f),
        "lysander" or "dunkel" or "flounders" or "ptarmigan" or "pylon" or "penetrator" => ("sniper", 2f, s.Family == "lysander" ? 0.88f : 0.95f),
        "grant" or "goliath" or "hornet" or "leopard" or "ibis" => ("rocket", -1f, s.Family == "goliath" ? 0.8f : 1f),
        "cascade" or "volcano" => ("rocket", -4f, 1.15f),
        "emerald" or "mlra" or "fork" or "falcon" => ("rocket", -5f, 1.25f),
        "tortoise" or "prominence" => ("rocket", 0f, 0.75f),
        "um" or "brute" or "crumble" or "stampede" or "splash" or "sticky" or "napalm" or "geyser" => ("launcher", -2f, 1f),
        "mg" or "dng" or "boundgren" or "cbomb" or "impulse" => ("dash", -12f, 1.4f),
        "acid" => ("spit", -4f, 0.9f),
        "flamer" or "flamecannon" => ("jet_loop", -10f, 0.6f),
        "firecracker" => ("launcher", -3f, 1.3f),
        "boundgun" => ("smg", -4f, 1.25f),
        "reverser" or "revshooter" => ("heal", -8f, 1.2f),
        // Wing Diver (en attendant les sons propres) : arcs, lances, éclairs, rayons, impulsions, plasma.
        "rapier" or "phalanx" => ("zap", -8f, 1.2f),
        "powerlance" or "dragoon" or "monsterlance" => ("laser", -2f, 0.8f),
        "sparkvine" or "sparkwhip" or "lightningbow" or "boltgun" or "crossbow" or "boltshooter" or "raijin" => ("zap", -4f, 0.9f),
        "magblaster" or "destroyer" or "spine" or "reflectron" or "closedlaser" or "monster" => ("laser", -8f, 1.1f),
        "pulsemg" or "stardust" or "pulsebuster" => ("plasma", -6f, 1.4f),
        "plasmacannon" or "plasmafall" or "bigcannon" or "heavycannon" or "burstcannon" or "grit" or "splasher" => ("plasma", -1f, 0.85f),
        "mirage" or "geist" or "ghostchaser" or "fenrir" or "psiblade" => ("laser", -6f, 1.3f),
        "diffuser" or "gleipnir" or "thundercluster" or "heavensgate" or "psicluster" or "finaldays" or "blinkball" => ("dash", -8f, 1.3f),
        // Fencer (repli si le son propre manque) : mêlée, canons rotatifs, canons lourds, mortiers, missiles.
        "hammer" or "vulcan" or "blade" or "forceax" or "roller" or "haken" or "blasthole" or "twinspear" or "flashing" or "jackhammer"
            => ("hammer", -2f, 0.9f),
        "gatling" or "fgs" or "disruptor" => ("minigun", -3f, 0.9f),
        "galleon" or "handcannon" or "gallic" or "powerdyne" or "spinedriver" => ("sniper", 1f, 0.75f),
        "dexter" or "cannonshot" => ("shotgun", 0f, 0.8f),
        "flamerevolver" => ("jet_loop", -10f, 0.6f),
        "lightmortar" or "heavymortar" or "dispersal" or "crusher" => ("launcher", 0f, 0.7f),
        "armhound" or "bloodstorm" or "haytal" or "leviathan" or "arcane" or "fgimpact" or "phoenix" => ("rocket", -2f, 1f),
        _ => ("rifle", -3f, 1f),
    };

    /// <summary>Gain de lecture des sons enregistrés de la famille (celui du type de son sur lequel ils sont calés).</summary>
    public static float GainDb(Edf5Spec s) => For(s).snd switch
    {
        "rifle" or "rifle_far" or "smg" => 4f,
        "hammer" => 3f,
        "minigun" => 1f,
        "shotgun" or "sniper" => 6f,
        "rocket" => 4.5f,
        "launcher" or "dash" or "spit" or "jet_loop" => 3f,
        _ => 0f,
    };

    /// <summary>
    /// Son de tir : enregistrement propre à la famille (« e5_famille ») s'il existe, sinon son de base transposé.
    /// Jets continus (lance-flammes, Reverser) : un son toutes les 0,3 s au plus.
    /// </summary>
    public static void Fire(Edf5Spec s, Vector3 at, bool local)
    {
        var interval = s.Kind is Edf5Kind.Flame or Edf5Kind.Reverser ? 300f : 20f;
        var own = "e5_" + s.Family;
        if (Sfx.Has(own))
        {
            var p = 1f + (GD.Randf() - 0.5f) * 0.04f;
            if (local) Sfx.Ui(own, 0f, p, interval);
            else Sfx.At(own, at, -2f, p, interval);
            return;
        }
        var (snd, db, pitch) = For(s);
        if (local) Sfx.Ui(snd, db, pitch, interval);
        else Sfx.At(snd == "rifle" ? "rifle_far" : snd, at, db + 1f, pitch, interval);
    }
}
