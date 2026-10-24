using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Outil de développement « --forestprobe » (vallée forestière) :
/// <list type="bullet">
/// <item>compare <see cref="World.GroundHeight(float, float)"/> à des rayons contre la vraie collision du terrain (journal
/// « [relief] écart max ») et vérifie la hauteur des véhicules, des escouades et des départs ;</item>
/// <item>des capsules de la taille d'un soldat suivent avec la vraie physique la piste du camp de bûcherons (montée de
/// ~35 m), le pont, le gué nord et le gué sud (« [probe] … ok ») ;</item>
/// <item>journal « [vallée] » des insectes : hauteur au-dessus du sol (aucun ne doit passer sous le relief).</item>
/// </list>
/// Exemple : <c>--autostart=22 --forestprobe --god --quit-after=60</c> (en <c>--headless</c>).
/// </summary>
public static partial class ForestBuilder
{
    private static void AttachProbe(Ctx c)
    {
        if (Screenshot.CmdArg("forestprobe") == null) return;
        var walkers = new List<BizWalker>
        {
            new() { Label = "piste du camp de bûcherons", GoalY = PadHeight(CampYard), Path = PathOf(c, LoggingCtl, 0f) },
            new() { Label = "pont routier", GoalY = TrackEnd(c, BridgeW + BridgeDir * 10f), Path = new List<Vector3> { OnGround(c, BridgeE.X - BridgeDir.X * 14f, BridgeE.Y - BridgeDir.Y * 14f), OnGround(c, BridgeE.X, BridgeE.Y), OnGround(c, BridgeW.X, BridgeW.Y), OnGround(c, BridgeW.X + BridgeDir.X * 10f, BridgeW.Y + BridgeDir.Y * 10f) } },
            new() { Label = "gué nord", GoalY = TrackEnd(c, new Vector2(-81, -134.5f)), Path = new List<Vector3> { OnGround(c, -113, -133.5f), OnGround(c, -97, -134), OnGround(c, -81, -134.5f) } },
            new() { Label = "gué sud", GoalY = TrackEnd(c, new Vector2(-84, 98.5f)), Path = new List<Vector3> { OnGround(c, -54, 97), OnGround(c, -68, 97.5f), OnGround(c, -84, 98.5f) } },
        };
        foreach (var w in walkers)
        {
            w.GlobalPositionOnReady = w.Path[0] + Vector3.Up * 1.2f;
            c.Root.AddChild(w);
        }
        c.Root.AddChild(new ForestProbe());
    }

    private static float TrackEnd(Ctx c, Vector2 p) => c.Hf.Height(p.X, p.Y);

    private static List<Vector3> PathOf(Ctx c, IReadOnlyList<Vector2> ctl, float lift)
    {
        var l = new List<Vector3>();
        foreach (var p in Smooth(ctl, 6f)) l.Add(OnGround(c, p.X, p.Y, lift));
        return l;
    }
}

/// <summary>Contrôles du relief (voir <see cref="ForestBuilder"/> : « --forestprobe »).</summary>
public partial class ForestProbe : Node
{
    private float _t;
    private bool _checked;
    private float _logT;

    public override void _PhysicsProcess(double delta)
    {
        _t += (float)delta;
        var w = World.Current;
        if (w?.Terrain == null) return;
        if (!_checked && _t > 1.5f)
        {
            _checked = true;
            GD.Print($"[relief] moteur physique : {ProjectSettings.GetSetting("physics/3d/physics_engine")}");
            var space = w.GetWorld3D().DirectSpaceState;
            var rng = new RandomNumberGenerator { Seed = 99 };
            var worst = 0f;
            var worstAt = Vector3.Zero;
            var n = 0;
            for (var i = 0; i < 600; i++)
            {
                var x = rng.RandfRange(-218f, 218f);
                var z = rng.RandfRange(-218f, 156f);
                var q = PhysicsRayQueryParameters3D.Create(new Vector3(x, 200f, z), new Vector3(x, -50f, z), 1);
                var hit = space.IntersectRay(q);
                if (hit.Count == 0) continue;
                if (hit["collider"].AsGodotObject() is not StaticBody3D sb || sb.Name != "Ground") continue;
                if (hit["shape"].AsInt32() != 0) continue; // seulement la forme du terrain (la première du corps)
                var y = ((Vector3)hit["position"]).Y;
                var e = Mathf.Abs(y - w.Terrain.Terrain(x, z));
                n++;
                if (e > worst) { worst = e; worstAt = new Vector3(x, y, z); }
            }
            GD.Print($"[relief] {n} rayons contre le terrain : écart max {worst:0.000} m en {worstAt.Snapped(Vector3.One * 0.1f)}");
            foreach (var v in w.Vehicles.Values)
                GD.Print($"[relief] véhicule {v.Id} : {v.GlobalPosition.Y - w.GroundHeight(v.GlobalPosition):0.00} m au-dessus du sol, pente {Mathf.RadToDeg(Mathf.Acos(w.GroundNormal(v.GlobalPosition.X, v.GlobalPosition.Z).Y)):0.0}°");
            if (w.LocalPlayer != null) GD.Print($"[relief] soldat : {w.LocalPlayer.GlobalPosition.Y - w.GroundHeight(w.LocalPlayer.GlobalPosition):0.00} m au-dessus du sol");
            foreach (var a in w.Allies.ById.Values)
                GD.Print($"[relief] allié {a.Id} : {a.GlobalPosition.Y - w.GroundHeight(a.GlobalPosition):0.00} m");
            if (w.Objective != null)
                foreach (var g in w.Objective.Guards)
                    GD.Print($"[relief] {g.Label} : {g.GlobalPosition.Y - w.GroundHeight(g.GlobalPosition):0.00} m au-dessus du sol");
        }
        _logT -= (float)delta;
        if (_t > 4f && _logT <= 0f)
        {
            _logT = 4f;
            int under = 0, onGround = 0, high = 0, total = 0;
            var minRel = float.MaxValue;
            foreach (var e in w.EnemiesById.Values)
            {
                if (e.State == EnemyState.Dead || e.Def.Flying) continue;
                var rel = e.GlobalPosition.Y - w.TerrainHeight(e.GlobalPosition.X, e.GlobalPosition.Z);
                minRel = Mathf.Min(minRel, rel);
                total++;
                if (rel < -0.6f) under++;
                else if (rel < 1.5f) onGround++;
                else high++;
            }
            if (total > 0) GD.Print($"[vallée] t={_t:0} insectes au sol {onGround}, en hauteur {high}, sous le relief {under} (écart min {minRel:0.00} m) sur {total}");
            // Pont : insectes sur le tablier ou dessous (contrôle des blocages).
            var bc = new Vector3(ForestBuilder.RiverX(ForestBuilder.BridgeZ), 0, ForestBuilder.BridgeZ);
            foreach (var e in w.EnemiesById.Values)
            {
                var p = e.GlobalPosition;
                if (e.State == EnemyState.Dead || new Vector2(p.X - bc.X, p.Z - bc.Z).Length() > 26f) continue;
                GD.Print($"[pont] {e.Def.Type} {p.Snapped(Vector3.One * 0.1f)} sol {w.GroundHeight(p):0.0} terrain {w.TerrainHeight(p.X, p.Z):0.0} normale {e.Basis.Y.Snapped(Vector3.One * 0.1f)} bloqué {w.Flow.IsBlocked(p)}");
            }
        }
    }
}
