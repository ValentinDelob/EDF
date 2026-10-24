using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Progression des opérations (dérivée du profil, sans donnée supplémentaire) : les missions se débloquent
/// les unes après les autres — la mission N+1 l'est dès que la mission N a été réussie, quelle que soit la difficulté.
/// </summary>
public static class MissionProgress
{
    public static bool Cleared(int mission)
    {
        for (var d = 0; d < Difficulties.Names.Length; d++)
            if (Profile.I.IsCleared(mission, (Difficulty)d)) return true;
        return false;
    }

    public static bool Unlocked(int mission) => mission <= 0 || Cleared(mission - 1);

    public static int ClearedCount => Missions.All.Count(m => Cleared(m.Index));

    /// <summary>Première mission débloquée et pas encore réussie (sinon la dernière).</summary>
    public static MissionDef Next =>
        Missions.All.FirstOrDefault(m => Unlocked(m.Index) && !Cleared(m.Index)) ?? Missions.All[^1];

    public static string Label(MissionDef m) => $"{m.Index + 1:00}. {m.Name}";

    public static string Zone(MissionDef m) => m.Map switch { MapId.Port => "port", MapId.Suburb => "banlieue", MapId.Business => "affaires", MapId.Industrial => "industrielle", MapId.Desert => "désert", MapId.Forest => "vallée", MapId.Underground => "souterraine", MapId.Capital => "capitale", _ => "ville" };

    public static string Goal(MissionDef m) => m.Goal switch
    {
        MissionGoal.Exterminate => "extermination",
        MissionGoal.DestroyNests => "destruction de nids",
        MissionGoal.Defense => "défense",
        MissionGoal.Escort => "escorte de convoi",
        MissionGoal.Survival => "survie",
        _ => "élimination du boss",
    };

    public static string Time(MissionDef m) => m.Map == MapId.Underground ? "sous terre, obscurité" : m.Time switch { TimeOfDay.Day => "jour", TimeOfDay.Dusk => "crépuscule", _ => "nuit" };

    /// <summary>Vignette de mission (capture en jeu, assets/missions/mission_N.jpg), ou null.</summary>
    public static Texture2D Thumbnail(int mission)
    {
        var path = $"res://assets/missions/mission_{mission}.jpg";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }
}

/// <summary>Écran « DÉPLOIEMENT » (maquette E-Mission) : voile sombre, mission · difficulté, barre animée.</summary>
public partial class DeployOverlay : Control
{
    private Control _bar;
    private Label _sub;
    private float _t;

    public DeployOverlay(string top, string sub, bool backdrop)
    {
        Theme = UiE.Theme;
        UiKit.FillRect(this);
        MouseFilter = MouseFilterEnum.Stop;
        if (backdrop) AddChild(new MenuBackdrop());
        AddChild(new ColorRect { Color = new Color(2 / 255f, 12 / 255f, 6 / 255f, 0.88f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        var stage = new EStage();
        AddChild(stage);
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Position = new Vector2(0, 0), Size = UiE.Design, MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 14);
        stage.AddChild(v);
        var l = UiE.Label(top, 18, UiE.Mint);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(l);
        var title = UiE.Title("DÉPLOIEMENT", 64, UiE.Text, UiE.Spaced(UiE.CondXBold, 6));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);
        var track = new Panel { CustomMinimumSize = new Vector2(420, 8), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        track.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0, 0, 0, 0.6f), UiE.Edge, 1, 0, 0, 0));
        v.AddChild(track);
        _bar = new Control { Size = new Vector2(168, 8), MouseFilter = MouseFilterEnum.Ignore };
        _bar.Draw += () => UiE.HGradient(_bar, new[] { Vector2.Zero, new Vector2(168, 0), new Vector2(168, 8), new Vector2(0, 8) }, 0, 168, new[] { (0f, UiE.Edge), (168f, UiE.Mint) });
        track.AddChild(_bar);
        _sub = UiE.Label(sub ?? "", 16, UiE.TextDim);
        _sub.HorizontalAlignment = HorizontalAlignment.Center;
        _sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sub.CustomMinimumSize = new Vector2(860, 0);
        _sub.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        v.AddChild(_sub);
    }

    public string Sub { set => _sub.Text = value ?? ""; }

    public override void _Process(double delta)
    {
        // Barre de 40 % qui traverse la piste (1,6 s, ease-in-out).
        _t += (float)delta;
        var u = _t / 1.6f % 1f;
        u = u * u * (3 - 2 * u);
        _bar.Position = new Vector2(Mathf.Lerp(-168f, 420f + 168f * 0.6f, u), 0);
    }
}
