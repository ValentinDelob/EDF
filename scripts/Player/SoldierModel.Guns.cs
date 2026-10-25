using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Armes d'EDF 5 tenues par le soldat : modèle Blender de la famille (assets/models/weapons/&lt;famille&gt;.glb),
/// matières du jeu par nom de matériau, pièces optionnelles de la variante (laser, lunette…), repères de prise lus dans
/// le modèle (Grip, Support ou SupportV pour une poignée verticale, Butt, Mag, Muzzle).
/// </summary>
public partial class SoldierModel
{
    private static readonly Dictionary<string, PackedScene> GunScenes = new();
    /// <summary>Arme d'EDF 5 affichée (null : arme procédurale <see cref="Style"/>).</summary>
    public string GunId { get; private set; }

    /// <summary>Affiche l'arme tenue : modèle de la famille pour une arme d'EDF 5, sinon style procédural.</summary>
    public void SetWeapon(WeaponTemplate t)
    {
        if (t?.Spec is { } s && LoadGun(t, s)) return;
        if (GunId != null) { GunId = null; Style = (WeaponStyle)(-1); }
        SetWeaponStyle(t == null ? WeaponStyle.Rifle : StyleFor(t));
    }

    private static PackedScene GunScene(string family)
    {
        if (GunScenes.TryGetValue(family, out var p)) return p;
        var path = $"res://assets/models/weapons/{family}.glb";
        p = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        GunScenes[family] = p;
        return p;
    }

    private bool LoadGun(WeaponTemplate t, Edf5Spec s)
    {
        if (GunId == s.Id) return true;
        var scene = GunScene(s.Family);
        if (scene == null || _gun == null) return false;
        foreach (var c in _gun.GetChildren()) c.QueueFree();
        var model = scene.Instantiate<Node3D>();
        _gun.AddChild(model);
        var (paint, accent) = GunColors(s);
        var mats = new Dictionary<string, Material>
        {
            ["Metal"] = Mats.GunSteel(),
            ["Polymer"] = Mats.GunPolymer(new Color(0.045f, 0.045f, 0.043f)),
            ["Paint"] = Mats.GunPaint(paint),
            ["Accent"] = Mats.GunPaint(accent),
            ["Alloy"] = Mats.Metal(new Color(0.42f, 0.43f, 0.45f), 0.35f),
            ["Rubber"] = Mats.GunPolymer(new Color(0.02f, 0.02f, 0.02f)),
            ["Glass"] = Mats.GunLens(),
            ["Lens"] = Mats.GunLens(),
            ["Glow"] = Mats.Glow(new Color(1f, 0.15f, 0.08f), 3f),
            ["Energy"] = Mats.Glow(new Color(0.35f, 0.75f, 1f), 3f),
        };
        var tris = 0;
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                if (mats.TryGetValue(mi.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "", out var m)) mi.SetSurfaceOverrideMaterial(i, m);
                if (mi.Mesh is ArrayMesh am) tris += am.SurfaceGetArrayIndexLen(i) / 3;
            }
            // Pièces optionnelles de la variante : laser (« LS »), lunette (zoom), sinon masquées.
            if (mi.Name.ToString().StartsWith("opt_"))
            {
                var opt = mi.Name.ToString()[4..];
                mi.Visible = opt switch
                {
                    "laser" => s.LaserSight,
                    "scope" => s.Zoom > 1f,
                    _ => true,
                };
            }
        }
        Vector3 Pt(string name, Vector3 fallback) => model.FindChild(name, true, false) is Node3D p ? p.Position : fallback;
        var vertical = model.FindChild("SupportV", true, false) != null;
        var hold = HoldFor(s);
        _rig = new Rig
        {
            Hold = hold,
            Grip = Pt("Grip", new Vector3(0, -0.1f, -0.07f)),
            Support = vertical ? Pt("SupportV", Vector3.Zero) : Pt("Support", new Vector3(0, 0, 0.25f)),
            Butt = Pt("Butt", new Vector3(0, -0.02f, -0.36f)),
            Mag = Pt("Mag", new Vector3(0, -0.12f, 0.08f)),
            GripB = PistolGripR,
            SupB = hold == Hold.Launcher || vertical ? VerticalGripL : HandguardL,
        };
        Muzzle = new Node3D { Position = Pt("Muzzle", new Vector3(0, 0, 0.6f)) };
        _gun.AddChild(Muzzle);
        _gunTris = tris;
        GunId = s.Id;
        Style = (WeaponStyle)(-2);
        return true;
    }

    /// <summary>Tenue selon la nature de l'arme.</summary>
    private static Hold HoldFor(Edf5Spec s) => s.Kind switch
    {
        Edf5Kind.Rocket or Edf5Kind.GuidedRocket or Edf5Kind.Missile => Hold.Launcher,
        Edf5Kind.HandGrenade or Edf5Kind.Bomb or Edf5Kind.Impulse => Hold.OneHand,
        _ => Hold.Shoulder,
    };

    /// <summary>Couleurs de la famille (peinture, accent), comme les rendus de contrôle de tools/blender/weapons.</summary>
    private static (Color paint, Color accent) GunColors(Edf5Spec s) => s.Family switch
    {
        "pa11" => (new Color(0.05f, 0.05f, 0.048f), new Color(0.3f, 0.27f, 0.2f)),
        "raven" => (new Color(0.2f, 0.17f, 0.12f), new Color(0.05f, 0.05f, 0.05f)),
        "minion" => (new Color(0.16f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.08f)),
        "slugger" => (new Color(0.06f, 0.06f, 0.06f), new Color(0.25f, 0.14f, 0.07f)),
        "orchid" => (new Color(0.55f, 0.57f, 0.6f), new Color(0.12f, 0.2f, 0.32f)),
        "gm" => (new Color(0.12f, 0.14f, 0.1f), new Color(0.05f, 0.05f, 0.05f)),
        "gm64" => (new Color(0.28f, 0.16f, 0.08f), new Color(0.05f, 0.05f, 0.05f)),
        "stork" => (new Color(0.13f, 0.15f, 0.17f), new Color(0.07f, 0.08f, 0.09f)),
        "stork4" => (new Color(0.33f, 0.28f, 0.19f), new Color(0.12f, 0.12f, 0.11f)),
        "slade" => (new Color(0.2f, 0.21f, 0.23f), new Color(0.6f, 0.25f, 0.05f)),
        "blazer" => (new Color(0.75f, 0.76f, 0.78f), new Color(0.1f, 0.12f, 0.16f)),
        _ => (new Color(0.16f, 0.17f, 0.12f), new Color(0.32f, 0.27f, 0.18f)),
    };
}
