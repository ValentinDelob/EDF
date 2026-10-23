using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Réglages utilisateur (touches, souris, graphismes, audio) persistés dans user://settings.cfg.
/// </summary>
public partial class Settings : Node
{
    public static Settings I { get; private set; }

    private const string FilePath = "user://settings.cfg";

    public record ActionDef(string Id, string Label, string DefaultBinding);

    /// <summary>Actions remappables, dans l'ordre d'affichage du menu d'options.</summary>
    public static readonly ActionDef[] Actions =
    {
        new("move_forward", "Avancer", "key:W"),
        new("move_back", "Reculer", "key:S"),
        new("move_left", "Gauche", "key:A"),
        new("move_right", "Droite", "key:D"),
        new("jump", "Sauter / voler", "key:Space"),
        new("sprint", "Courir / propulsion", "key:Shift"),
        new("dodge", "Roulade / esquive", "key:Ctrl"),
        new("fire", "Tirer", "mouse:1"),
        new("aim", "Viser", "mouse:2"),
        new("reload", "Recharger", "key:R"),
        new("weapon_1", "Arme 1", "key:1"),
        new("weapon_2", "Arme 2", "key:2"),
        new("weapon_3", "Arme 3", "key:3"),
        new("weapon_swap", "Arme suivante", "key:Q"),
        new("ability", "Capacité de classe", "key:F"),
        new("grenade", "Équipement", "key:G"),
        new("interact", "Interagir / relever", "key:E"),
        new("scoreboard", "Tableau des scores", "key:Tab"),
        new("ping", "Marquer une position", "mouse:3"),
    };

    // Souris
    public float MouseSensitivity = 1.0f;
    public float AimSensitivity = 0.6f;
    public bool InvertY;

    // Graphismes
    public int Quality = 2; // 0 bas, 1 moyen, 2 haut, 3 ultra
    public bool Fullscreen;
    public bool VSync = true;
    public float Fov = 75f;
    public float RenderScale = 1f;
    public int MaxFps = 0;

    // Audio
    public float MasterVolume = 0.8f;
    public float SfxVolume = 0.9f;
    public float MusicVolume = 0.5f;

    // Interface
    public float HudScale = 1f;
    public bool DamageNumbers = true;

    // Joueur
    public string PlayerName = "Soldat";
    public string LastIp = "127.0.0.1";

    private readonly Dictionary<string, string> _bindings = new();

    public event Action Changed;

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        foreach (var a in Actions) _bindings[a.Id] = a.DefaultBinding;
        Load();
        ApplyInput();
    }

    public override void _Ready()
    {
        EnsureBuses();
        ApplyVideo();
        ApplyAudio();
    }

    public string GetBinding(string action) => _bindings.TryGetValue(action, out var b) ? b : "";

    public void SetBinding(string action, InputEvent ev)
    {
        var s = Serialize(ev);
        if (s == null) return;
        // Une touche ne peut servir qu'à une seule action : on libère l'ancienne.
        foreach (var a in Actions)
            if (a.Id != action && _bindings[a.Id] == s) _bindings[a.Id] = "";
        _bindings[action] = s;
        ApplyInput();
        Save();
    }

    public void ResetBindings()
    {
        foreach (var a in Actions) _bindings[a.Id] = a.DefaultBinding;
        ApplyInput();
        Save();
    }

    public static string Describe(string binding)
    {
        if (string.IsNullOrEmpty(binding)) return "—";
        var parts = binding.Split(':');
        if (parts[0] == "mouse")
            return parts[1] switch
            {
                "1" => "Clic gauche",
                "2" => "Clic droit",
                "3" => "Clic molette",
                "4" => "Molette haut",
                "5" => "Molette bas",
                "8" => "Souris 4",
                "9" => "Souris 5",
                _ => "Souris " + parts[1],
            };
        var label = parts[1];
        // Les touches sont liées par position physique : on affiche la lettre de la disposition réelle (ZQSD en AZERTY).
        var phys = OS.FindKeycodeFromString(parts[1]);
        if (phys != Key.None)
        {
            var local = DisplayServer.KeyboardGetLabelFromPhysical(phys);
            if (local != Key.None) label = OS.GetKeycodeString(local);
        }
        return label switch
        {
            "Space" => "Espace",
            "Shift" => "Maj",
            "Ctrl" => "Ctrl",
            "Tab" => "Tab",
            _ => label,
        };
    }

    private static string Serialize(InputEvent ev)
    {
        switch (ev)
        {
            case InputEventKey k:
                var code = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                if (code == Key.Escape) return null;
                return "key:" + OS.GetKeycodeString(code);
            case InputEventMouseButton m:
                return "mouse:" + (int)m.ButtonIndex;
        }
        return null;
    }

    private static InputEvent Deserialize(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var parts = s.Split(':', 2);
        if (parts.Length < 2) return null;
        if (parts[0] == "mouse" && int.TryParse(parts[1], out var btn))
            return new InputEventMouseButton { ButtonIndex = (MouseButton)btn };
        if (parts[0] == "key")
        {
            var code = OS.FindKeycodeFromString(parts[1]);
            if (code == Key.None) return null;
            return new InputEventKey { PhysicalKeycode = code };
        }
        return null;
    }

    /// <summary>Liaisons manette (disposition Xbox), non remappables pour l'instant.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, InputEvent[]> PadBindings = new()
    {
        ["move_forward"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1 } },
        ["move_back"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1 } },
        ["move_left"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1 } },
        ["move_right"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1 } },
        ["jump"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.A } },
        ["sprint"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.LeftStick } },
        ["dodge"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.B } },
        ["fire"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.TriggerRight, AxisValue = 1 } },
        ["aim"] = new InputEvent[] { new InputEventJoypadMotion { Axis = JoyAxis.TriggerLeft, AxisValue = 1 } },
        ["reload"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.X } },
        ["weapon_swap"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.Y } },
        ["ability"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder } },
        ["grenade"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder } },
        ["interact"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp } },
        ["scoreboard"] = new InputEvent[] { new InputEventJoypadButton { ButtonIndex = JoyButton.Back } },
    };

    private void ApplyInput()
    {
        foreach (var a in Actions)
        {
            if (!InputMap.HasAction(a.Id)) InputMap.AddAction(a.Id, 0.2f);
            InputMap.ActionEraseEvents(a.Id);
            var ev = Deserialize(_bindings[a.Id]);
            if (ev != null) InputMap.ActionAddEvent(a.Id, ev);
            if (PadBindings.TryGetValue(a.Id, out var pads))
                foreach (var pe in pads) InputMap.ActionAddEvent(a.Id, pe);
        }
        foreach (var (name, axis, value) in new[] { ("look_left", JoyAxis.RightX, -1f), ("look_right", JoyAxis.RightX, 1f), ("look_up", JoyAxis.RightY, -1f), ("look_down", JoyAxis.RightY, 1f) })
        {
            if (!InputMap.HasAction(name)) InputMap.AddAction(name, 0.15f);
            InputMap.ActionEraseEvents(name);
            InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
        }
        if (!InputMap.HasAction("pause"))
        {
            InputMap.AddAction("pause");
            InputMap.ActionAddEvent("pause", new InputEventKey { PhysicalKeycode = Key.Escape });
            InputMap.ActionAddEvent("pause", new InputEventJoypadButton { ButtonIndex = JoyButton.Start });
        }
        if (!InputMap.HasAction("debug_screenshot"))
        {
            InputMap.AddAction("debug_screenshot");
            InputMap.ActionAddEvent("debug_screenshot", new InputEventKey { PhysicalKeycode = Key.F12 });
        }
    }

    public void ApplyVideo()
    {
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = MaxFps;
        var vp = GetViewport();
        if (vp != null)
        {
            vp.Scaling3DScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
            vp.Scaling3DMode = RenderScale < 0.99f ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Bilinear;
            vp.Msaa3D = Quality >= 3 ? Viewport.Msaa.Msaa4X : (Quality >= 2 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled);
            vp.ScreenSpaceAA = Quality >= 1 ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        }
        Changed?.Invoke();
    }

    private static void EnsureBuses()
    {
        if (AudioServer.GetBusIndex("Sfx") < 0)
        {
            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, "Sfx");
            AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master");
        }
        if (AudioServer.GetBusIndex("Music") < 0)
        {
            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, "Music");
            AudioServer.SetBusSend(AudioServer.BusCount - 1, "Master");
        }
    }

    public void ApplyAudio()
    {
        SetBus("Master", MasterVolume);
        SetBus("Sfx", SfxVolume);
        SetBus("Music", MusicVolume);
    }

    private static void SetBus(string name, float v)
    {
        var idx = AudioServer.GetBusIndex(name);
        if (idx < 0) return;
        AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Max(v, 0.0001f)));
        AudioServer.SetBusMute(idx, v <= 0.001f);
    }

    public void NotifyChanged() => Changed?.Invoke();

    public void Save()
    {
        var cfg = new ConfigFile();
        foreach (var a in Actions) cfg.SetValue("bindings", a.Id, _bindings[a.Id]);
        cfg.SetValue("mouse", "sensitivity", MouseSensitivity);
        cfg.SetValue("mouse", "aim_sensitivity", AimSensitivity);
        cfg.SetValue("mouse", "invert_y", InvertY);
        cfg.SetValue("video", "quality", Quality);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("video", "vsync", VSync);
        cfg.SetValue("video", "fov", Fov);
        cfg.SetValue("video", "render_scale", RenderScale);
        cfg.SetValue("video", "max_fps", MaxFps);
        cfg.SetValue("audio", "master", MasterVolume);
        cfg.SetValue("audio", "sfx", SfxVolume);
        cfg.SetValue("audio", "music", MusicVolume);
        cfg.SetValue("ui", "hud_scale", HudScale);
        cfg.SetValue("ui", "damage_numbers", DamageNumbers);
        cfg.SetValue("player", "name", PlayerName);
        cfg.SetValue("player", "last_ip", LastIp);
        cfg.Save(FilePath);
    }

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) != Error.Ok) return;
        foreach (var a in Actions)
            _bindings[a.Id] = (string)cfg.GetValue("bindings", a.Id, a.DefaultBinding);
        MouseSensitivity = (float)cfg.GetValue("mouse", "sensitivity", MouseSensitivity);
        AimSensitivity = (float)cfg.GetValue("mouse", "aim_sensitivity", AimSensitivity);
        InvertY = (bool)cfg.GetValue("mouse", "invert_y", InvertY);
        Quality = (int)cfg.GetValue("video", "quality", Quality);
        Fullscreen = (bool)cfg.GetValue("video", "fullscreen", Fullscreen);
        VSync = (bool)cfg.GetValue("video", "vsync", VSync);
        Fov = (float)cfg.GetValue("video", "fov", Fov);
        RenderScale = (float)cfg.GetValue("video", "render_scale", RenderScale);
        MaxFps = (int)cfg.GetValue("video", "max_fps", MaxFps);
        MasterVolume = (float)cfg.GetValue("audio", "master", MasterVolume);
        SfxVolume = (float)cfg.GetValue("audio", "sfx", SfxVolume);
        MusicVolume = (float)cfg.GetValue("audio", "music", MusicVolume);
        HudScale = (float)cfg.GetValue("ui", "hud_scale", HudScale);
        DamageNumbers = (bool)cfg.GetValue("ui", "damage_numbers", DamageNumbers);
        PlayerName = (string)cfg.GetValue("player", "name", PlayerName);
        LastIp = (string)cfg.GetValue("player", "last_ip", LastIp);
    }
}
