using System.Diagnostics;
using System.Text;
using Godot;

namespace Rempart;

/// <summary>
/// Profilage du jeu (option de test <c>--perf</c>) : sections chronométrées dans les boucles chaudes et, toutes les 3 s,
/// une ligne <c>[perf]</c> dans le journal (temps moyens par image en ms). Sans l'option, <see cref="On"/> vaut false et
/// chaque mesure se réduit à un test de booléen.
///
/// Lecture : l'image (<c>Frame</c>) se découpe en <c>Scripts</c> (tous les _process) → <c>Flush</c> (appels différés, dont
/// les _draw de l'interface, puis notifications de transformation et suppressions) → <c>Flush2</c> (minuteurs, Tween) →
/// <c>Draw</c> (serveur de rendu sur le fil principal, attente du GPU comprise) → <c>PhysGap</c> (pas de physique et entrées).
/// <c>rendu cpu/gpu</c> = temps mesurés par le serveur de rendu pour la fenêtre ; <c>appels</c>, <c>objets</c>, <c>prim</c> =
/// passe visible, <c>ombres</c> = passes d'ombres (appels / objets). Les sections imbriquées (escalade, rayons, animation,
/// envoi des os, séparation dans l'IA des ennemis) sont comptées aussi dans leur parent. Une seconde ligne détaille les _draw
/// de l'interface (temps écoulé depuis le précédent, appels différés intercalés compris) et le reste de Flush.
/// </summary>
public partial class Perf : Node
{
    public enum S
    {
        /// <summary>Boucle d'IA des ennemis (hôte), tout compris.</summary>
        EnemyHost,
        /// <summary>Interpolation des ennemis (client), tout compris.</summary>
        EnemyClient,
        /// <summary>Escalade : pas sur la surface (sondes et déplacement).</summary>
        Climb,
        /// <summary>Rayons des sondes d'escalade seuls.</summary>
        Ray,
        /// <summary>Animation des modèles (pattes, IK, formes de collision).</summary>
        Anim,
        /// <summary>Envoi des os des insectes cuits au serveur de rendu (dans Anim).</summary>
        Skin,
        /// <summary>Séparation entre ennemis (grille de hachage).</summary>
        Separation,
        /// <summary>Reconstruction de la grille de hachage.</summary>
        Hash,
        /// <summary>Champ de flux (recalcul toutes les 0,5 s).</summary>
        Flow,
        /// <summary>Contrôle d'écrasement du mobilier (Destructibles.HostTick).</summary>
        Decor,
        /// <summary>Fondu des insectes traversés par la caméra.</summary>
        CamFade,
        /// <summary>Instantanés réseau (hôte : envoi ; client : réception).</summary>
        Snapshot,
        /// <summary>Alliés PNJ.</summary>
        Allies,
        /// <summary>Directeur de mission.</summary>
        Director,
        /// <summary>World._Process entier.</summary>
        World,
        /// <summary>Tous les _process des scripts (du premier nœud traité au dernier).</summary>
        Scripts,
        /// <summary>Image entière (d'un début de _process au suivant).</summary>
        Frame,
        /// <summary>Fin des _process → minuteurs de l'arbre : appels différés, transformations différées, suppressions.</summary>
        Flush,
        /// <summary>Minuteurs → début du rendu : animations Tween, transformations, rappels, serveurs (navigation, audio).</summary>
        Flush2,
        /// <summary>Rendu sur le fil principal (RenderingServer.draw : instances modifiées, culling, listes, envoi au GPU, attente).</summary>
        Draw,
        /// <summary>Fin du rendu → début des _process suivants : pas de physique (scripts _physics_process compris), entrées.</summary>
        PhysGap,
        /// <summary>Scripts _physics_process (du premier au dernier nœud), cumulés sur les pas de l'image.</summary>
        PhysScripts,
        Count,
    }

    public static bool On;
    private static readonly long[] Acc = new long[(int)S.Count];
    private static readonly long[] Start = new long[(int)S.Count];
    private static readonly int[] Calls = new int[(int)S.Count];

    public static void Begin(S s)
    {
        if (On) Start[(int)s] = Stopwatch.GetTimestamp();
    }

    public static void End(S s)
    {
        if (!On) return;
        Acc[(int)s] += Stopwatch.GetTimestamp() - Start[(int)s];
        Calls[(int)s]++;
    }

    /// <summary>Compteurs par image : insectes animés à chaque image / à cadence réduite, rayons d'escalade, ennemis par distance à la caméra.</summary>
    public static readonly int[] Counters = new int[6];
    public static readonly string[] CounterNames = { "anim", "animLod", "rayons", "<30m", "<60m", "<150m" };

    private double _t;
    private ulong _frames;
    private double _rcpu, _rgpu;
    private long _draw, _objs, _prims, _shDraw, _shObjs;
    private int _gc0, _gc2;
    private double _gcPause;
    private long _gcAlloc;

    /// <summary>Lance le profilage (une seule fois, nœud racine de la scène).</summary>
    public static void Install(Node root)
    {
        if (On) return;
        On = true;
        var p = new Perf { Name = "Perf", ProcessMode = ProcessModeEnum.Always, ProcessPriority = int.MaxValue };
        root.AddChild(p);
        root.AddChild(new PerfHead
        {
            Name = "PerfHead", ProcessMode = ProcessModeEnum.Always, ProcessPriority = int.MinValue, ProcessPhysicsPriority = int.MinValue,
        });
        // Rendu sur le fil principal (modèle « Safe ») : les signaux du serveur de rendu bornent la section Draw.
        RenderingServer.FramePreDraw += () => { End(S.Flush2); Begin(S.Draw); };
        // Un minuteur de durée nulle relancé à chaque image marque le passage aux minuteurs de l'arbre (fin de Flush).
        var tree = root.GetTree();
        void Tick()
        {
            End(S.Flush);
            var tail = Stopwatch.GetTimestamp() - _drawMark;
            DrawAcc["reste"] = (DrawAcc.TryGetValue("reste", out var v) ? v : 0) + tail;
            Begin(S.Flush2);
            tree.CreateTimer(0, true, false, false).Timeout += Tick;
        }
        tree.CreateTimer(0, true, false, false).Timeout += Tick;
        RenderingServer.FramePostDraw += () => { End(S.Draw); Begin(S.PhysGap); };
    }

    /// <summary>Premier nœud traité à chaque image : début de la section « scripts » et de l'image.</summary>
    private partial class PerfHead : Node
    {
        private long _last;

        public override void _Process(double delta)
        {
            var now = Stopwatch.GetTimestamp();
            if (_last != 0) { Acc[(int)S.Frame] += now - _last; Calls[(int)S.Frame]++; }
            _last = now;
            End(S.PhysGap);
            Start[(int)S.Scripts] = now;
        }

        public override void _PhysicsProcess(double delta) => Begin(S.PhysScripts);
    }

    public override void _Ready()
    {
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        ProcessPhysicsPriority = int.MaxValue;
    }

    public override void _PhysicsProcess(double delta) => End(S.PhysScripts);

    // Coût des _draw de l'interface (mesuré entre deux signaux « draw » successifs, dans la file des appels différés).
    private static readonly System.Collections.Generic.Dictionary<string, long> DrawAcc = new();
    private static long _drawMark;
    private readonly System.Collections.Generic.HashSet<ulong> _hooked = new();

    private void HookDraws(Node n)
    {
        if (n is CanvasItem ci && _hooked.Add(ci.GetInstanceId()))
        {
            var key = $"{n.GetType().Name}";
            ci.Draw += () =>
            {
                var now = Stopwatch.GetTimestamp();
                DrawAcc[key] = (DrawAcc.TryGetValue(key, out var v) ? v : 0) + now - _drawMark;
                _drawMark = now;
            };
        }
        foreach (var c in n.GetChildren()) HookDraws(c);
    }

    public override void _Process(double delta)
    {
        End(S.Scripts);
        Begin(S.Flush);
        _drawMark = Stopwatch.GetTimestamp();
        var vp = GetViewport();
        var rid = vp.GetViewportRid();
        _frames++;
        _t += delta;
        _rcpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid) + RenderingServer.GetFrameSetupTimeCpu();
        _rgpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
        _draw += vp.GetRenderInfo(Viewport.RenderInfoType.Visible, Viewport.RenderInfo.DrawCallsInFrame);
        _objs += vp.GetRenderInfo(Viewport.RenderInfoType.Visible, Viewport.RenderInfo.ObjectsInFrame);
        _prims += vp.GetRenderInfo(Viewport.RenderInfoType.Visible, Viewport.RenderInfo.PrimitivesInFrame);
        _shDraw += vp.GetRenderInfo(Viewport.RenderInfoType.Shadow, Viewport.RenderInfo.DrawCallsInFrame);
        _shObjs += vp.GetRenderInfo(Viewport.RenderInfoType.Shadow, Viewport.RenderInfo.ObjectsInFrame);
        // Répartition des ennemis par distance à la caméra (une image sur 10).
        if (_frames % 10 == 0 && World.Current is { } cw && IsInstanceValid(cw) && vp.GetCamera3D() is { } cam)
        {
            var cp = cam.GlobalPosition;
            foreach (var e in cw.EnemiesById.Values)
            {
                var dd = e.GlobalPosition.DistanceTo(cp);
                if (dd < 30) Counters[3] += 10;
                else if (dd < 60) Counters[4] += 10;
                else if (dd < 150) Counters[5] += 10;
            }
        }
        if (_t < 3.0) return;

        var f = (double)_frames;
        var ms = 1000.0 / Stopwatch.Frequency / f;
        var sb = new StringBuilder();
        var w = World.Current;
        sb.Append($"[perf] {f / _t:0} i/s ({_t * 1000 / f:0.0} ms) ennemis {(w != null && IsInstanceValid(w) ? w.EnemiesById.Count : 0)}");
        // Synchronisation verticale ou plafond actifs : les temps d'image sont arrondis (60 / 30 i/s).
        if (DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled || Engine.MaxFps > 0)
            sb.Append($" [vsync {DisplayServer.WindowGetVsyncMode()} plafond {Engine.MaxFps}]");
        sb.Append($" | rendu cpu {_rcpu / f:0.00} gpu {_rgpu / f:0.00}");
        sb.Append($" | appels {_draw / f:0} objets {_objs / f:0} prim {_prims / f / 1000:0}k ombres {_shDraw / f:0}/{_shObjs / f:0}");
        sb.Append($" | physique objets actifs {Performance.GetMonitor(Performance.Monitor.Physics3DActiveObjects):0} paires {Performance.GetMonitor(Performance.Monitor.Physics3DCollisionPairs):0} nœuds {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}");
        sb.Append(" |");
        for (var i = 0; i < (int)S.Count; i++)
        {
            if (Calls[i] == 0) continue;
            sb.Append($" {(S)i} {Acc[i] * ms:0.00}");
        }
        for (var i = 0; i < Counters.Length; i++)
            if (CounterNames[i] != "") sb.Append($" {CounterNames[i]} {Counters[i] / f:0}");
        // Ramasse-miettes .NET : collectes (génération 0 / 2), pause cumulée par image, octets alloués par image.
        var pause = System.GC.GetTotalPauseDuration().TotalMilliseconds;
        var alloc = System.GC.GetTotalAllocatedBytes();
        sb.Append($" | GC {System.GC.CollectionCount(0) - _gc0}/{System.GC.CollectionCount(2) - _gc2} pause {(pause - _gcPause) / f:0.00} alloc {(alloc - _gcAlloc) / f / 1024:0}k");
        _gc0 = System.GC.CollectionCount(0);
        _gc2 = System.GC.CollectionCount(2);
        _gcPause = pause;
        _gcAlloc = alloc;
        GD.Print(sb.ToString());
        if (DrawAcc.Count > 0)
        {
            var d = new StringBuilder("[perf] interface _draw :");
            foreach (var (k, v) in DrawAcc)
                if (v * ms >= 0.05) d.Append($" {k} {v * ms:0.00}");
            GD.Print(d.ToString());
            DrawAcc.Clear();
        }
        if (w != null && IsInstanceValid(w) && w.Hud != null) HookDraws(w.Hud);
        System.Array.Clear(Acc);
        System.Array.Clear(Calls);
        System.Array.Clear(Counters);
        _t = 0;
        _frames = 0;
        _rcpu = _rgpu = 0;
        _draw = _objs = _prims = _shDraw = _shObjs = 0;
    }
}
