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
        _ => ("rifle", -3f, 1f),
    };

    public static void Fire(Edf5Spec s, Vector3 at, bool local)
    {
        var (snd, db, pitch) = For(s);
        if (local) Sfx.Ui(snd, db, pitch, 20);
        else Sfx.At(snd == "rifle" ? "rifle_far" : snd, at, db + 1f, pitch);
    }
}
