using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace Rempart;

/// <summary>
/// Caméra de contrôle visuel de la ville (outil de développement) :
/// « --citycam=x,y,z,lacet,tangage;x,y,z,lacet,tangage » enchaîne des points de vue fixes
/// toutes les « --citycam-period » secondes (2 s par défaut), à partir de « --citycam-start » (3 s).
/// Une capture est prise à la fin de chaque point de vue, puis le jeu se ferme.
/// </summary>
public partial class CityCam : Camera3D
{
    private readonly List<(Vector3 pos, Vector3 rot)> _views = new();
    private float _t;
    private float _period = 2f;
    private float _start = 3f;
    private int _shot = -1;

    public static void Attach(Node3D root)
    {
        var arg = Screenshot.CmdArg("citycam");
        if (string.IsNullOrEmpty(arg) || arg == "true") return;
        var cam = new CityCam { Name = "CityCam", Fov = 65, Far = 3000f, Near = 0.1f };
        foreach (var v in arg.Split(';'))
        {
            var p = v.Split(',');
            if (p.Length < 5) continue;
            float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
            cam._views.Add((new Vector3(F(0), F(1), F(2)), new Vector3(F(4), F(3), 0)));
        }
        if (cam._views.Count == 0) return;
        if (float.TryParse(Screenshot.CmdArg("citycam-period") ?? "2", NumberStyles.Float, CultureInfo.InvariantCulture, out var per)) cam._period = per;
        if (float.TryParse(Screenshot.CmdArg("citycam-start") ?? "3", NumberStyles.Float, CultureInfo.InvariantCulture, out var st)) cam._start = st;
        root.AddChild(cam);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t < _start) return;
        var k = (_t - _start) / _period;
        var i = (int)k;
        if (i >= _views.Count)
        {
            GetTree().Quit();
            return;
        }
        Position = _views[i].pos;
        // « --citycam-ground » : hauteurs relatives au sol (cartes à relief).
        if (Screenshot.CmdArg("citycam-ground") != null && World.Current != null)
            Position = World.Current.OnGround(Position, Position.Y);
        RotationDegrees = _views[i].rot;
        if (!Current) MakeCurrent();
        // Capture une fois le point de vue stabilisé (ombres, LOD).
        if (k - i > 0.85f && _shot < i)
        {
            _shot = i;
            Screenshot.Take(GetViewport(), $"citycam{i}");
            GD.Print($"citycam{i} : {Engine.GetFramesPerSecond()} i/s, {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} appels de rendu, {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objets, {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} primitives");
        }
    }
}
