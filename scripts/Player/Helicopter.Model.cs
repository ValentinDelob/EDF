using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Modèle de l'hélicoptère (modélisé dans Blender par tools/blender/helicopter.py, exporté en assets/models/helicopter.glb) :
/// matières du jeu par nom de matériau, pièces animées retrouvées par nom (rotors, tourelle de menton, canon rotatif,
/// paniers de roquettes et leurs bouches).
/// </summary>
public partial class Helicopter
{
    private const string ModelPath = "res://assets/models/helicopter.glb";

    /// <summary>Canon de la tourelle de menton (site) ; la tourelle <see cref="_gun"/> ne porte que le lacet.</summary>
    private Node3D _cannon;

    private void BuildModel()
    {
        var model = GD.Load<PackedScene>(ModelPath).Instantiate<Node3D>();
        model.Name = "Model";
        // Le corps pivote autour du centre de gravité (1,6 m) : le modèle, posé au sol, est décalé d'autant.
        model.Position = new Vector3(0, -1.6f, 0);
        _body.AddChild(model);

        var mats = new Dictionary<string, Material>
        {
            ["Paint"] = Mats.CarPaint(new Color(0.3f, 0.33f, 0.26f)),
            ["PaintDark"] = Mats.CarPaint(new Color(0.21f, 0.23f, 0.185f)),
            ["Dark"] = Mats.Metal(new Color(0.07f, 0.07f, 0.07f), 0.5f),
            ["Rubber"] = Mats.Painted(new Color(0.035f, 0.035f, 0.035f), 0.95f),
            ["Glass"] = Mats.Painted(new Color(0.04f, 0.06f, 0.07f), 0.06f, 0.6f),
            ["Blade"] = Mats.Painted(new Color(0.09f, 0.095f, 0.088f), 0.65f, 0.2f),
            ["Lamp"] = Mats.Glow(new Color(1f, 0.95f, 0.85f), 1.2f),
            ["LampRed"] = Mats.Glow(new Color(1f, 0.12f, 0.08f), 2.5f),
            ["LampGreen"] = Mats.Glow(new Color(0.15f, 1f, 0.3f), 2.5f),
        };
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                if (mats.TryGetValue(mi.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? "", out var m)) mi.SetSurfaceOverrideMaterial(s, m);
        }

        Node3D Find(string name) => model.FindChild(name, true, false) as Node3D;
        _rotor = Find("Rotor");
        _tailRotor = Find("TailRotor");
        _gun = Find("Gun");
        _cannon = Find("Cannon");
        _gunBarrels = Find("Barrels");
        _gunMuzzle = Find("GunMuzzle");
        // Paniers : [0] = côté +X (gauche de l'appareil, qui regarde vers +Z), [1] = côté −X.
        var pl = Find("Pod_L");
        var pr = Find("Pod_R");
        var (plus, minus) = pl.Position.X > 0 ? (pl, pr) : (pr, pl);
        _pods[0] = plus;
        _pods[1] = minus;
        _podMuzzles[0] = plus.FindChild("PodMuzzle_*", false, false) as Node3D;
        _podMuzzles[1] = minus.FindChild("PodMuzzle_*", false, false) as Node3D;
        // Projecteur de recherche sous le nez (nuit, souterrain), braqué vers l'avant et le bas.
        if (_gun?.GetParent() is Node3D gp) AddHeadlight(gp, _gun.Position + new Vector3(0, -0.4f, 0.6f), 32f, 75f, 20f, 7f);
    }
}
