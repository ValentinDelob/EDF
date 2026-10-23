using Godot;

namespace Rempart;

/// <summary>Captures d'écran (F12) et captures automatiques pour le suivi du développement.</summary>
public static class Screenshot
{
    public static string Take(Viewport vp, string prefix)
    {
        var img = vp.GetTexture().GetImage();
        var dir = OS.GetCmdlineUserArgs().Length > 0 ? CmdArg("shots", "user://screenshots") : "user://screenshots";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var stamp = Time.GetDatetimeStringFromSystem().Replace(":", "-");
        var path = $"{dir}/{prefix}_{stamp}_{Time.GetTicksMsec() % 1000}.png";
        img.SavePng(path);
        GD.Print("Capture : " + ProjectSettings.GlobalizePath(path));
        return path;
    }

    /// <summary>Lit un argument « --nom=valeur » passé après « -- » sur la ligne de commande.</summary>
    public static string CmdArg(string name, string fallback = null)
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a == "--" + name) return "true";
            if (a.StartsWith("--" + name + "=")) return a.Substring(name.Length + 3);
        }
        return fallback;
    }
}
