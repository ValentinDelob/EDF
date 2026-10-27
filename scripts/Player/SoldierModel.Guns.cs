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
        // Équipements (cœur plasma, soutien du Fencer) : rien en main ; bouclier : porté au bras gauche (SetShield).
        if (t?.Spec?.Kind is Edf5Kind.Core or Edf5Kind.Gear) return;
        if (t?.Spec?.Kind == Edf5Kind.Shield) { SetShield(t.Spec); return; }
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
        var scene = GunScene(ModelName(s));
        if (scene == null || _gun == null) return false;
        foreach (var c in _gun.GetChildren()) c.QueueFree();
        var model = scene.Instantiate<Node3D>();
        _gun.AddChild(model);
        var tris = ApplyGunMats(model, s);
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

    /// <summary>Matières du jeu par nom de matériau et pièces optionnelles de la variante ; renvoie le nombre de triangles.</summary>
    private static int ApplyGunMats(Node3D model, Edf5Spec s)
    {
        var (paint, accent) = GunColors(s);
        return ApplyMats(model, paint, accent, s, Mats.GunMask($"res://assets/models/weapons/{ModelName(s)}.glb"));
    }

    /// <summary>Matières du jeu d'un modèle Blender (armes, objets de l'Air Raider) ; options de variante si <paramref name="s"/>.</summary>
    /// <remarks><paramref name="mask"/> : texture de détail cuite du modèle (usure, creux) ; sans elle, matières unies.</remarks>
    public static int ApplyMats(Node3D model, Color paint, Color accent, Edf5Spec s = null, Texture2D mask = null)
    {
        var mats = new Dictionary<string, Material>
        {
            ["Metal"] = Mats.GunSurface(Mats.GunKind.Metal, new Color(0.075f, 0.076f, 0.082f), mask),
            ["Polymer"] = Mats.GunSurface(Mats.GunKind.Polymer, new Color(0.05f, 0.05f, 0.048f), mask),
            ["Paint"] = Mats.GunSurface(Mats.GunKind.Paint, paint, mask),
            ["Accent"] = Mats.GunSurface(Mats.GunKind.Paint, accent, mask),
            ["Alloy"] = Mats.GunSurface(Mats.GunKind.Alloy, new Color(0.42f, 0.43f, 0.45f), mask),
            ["Rubber"] = Mats.GunSurface(Mats.GunKind.Rubber, new Color(0.025f, 0.025f, 0.025f), mask),
            ["Marking"] = Mats.GunSurface(Mats.GunKind.Marking, paint.Luminance > 0.45f ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.85f, 0.8f, 0.62f), mask),
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
            if (s != null && mi.Name.ToString().StartsWith("opt_"))
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
        return tris;
    }

    // ───────── Bouclier du Fencer (bras gauche) ─────────

    private Node3D _shield;
    /// <summary>Bouclier porté (null : aucun).</summary>
    public string ShieldId { get; private set; }
    /// <summary>Garde levée (bouton tenu, bouclier en main) : le bouclier passe devant le torse.</summary>
    public bool ShieldUp;
    private float _shieldBlend;

    /// <summary>Bouclier du jeu actif, tenu de la main gauche devant le flanc (null : retiré).</summary>
    public void SetShield(Edf5Spec s)
    {
        if (s?.Id == ShieldId) return;
        _shield?.QueueFree();
        _shield = null;
        ShieldId = null;
        if (s == null || _torso == null || GunScene(s.Family) is not { } scene) return;
        _shield = new Node3D();
        _torso.AddChild(_shield);
        var model = scene.Instantiate<Node3D>();
        _shield.AddChild(model);
        ApplyGunMats(model, s);
        ShieldId = s.Id;
    }

    /// <summary>Pose du bouclier (repère du torse) et main gauche sur sa poignée ; faux sans bouclier.</summary>
    private bool PoseShield(float dt, Transform3D frame, Transform3D torsoModel, float pitch, Vector3 pole)
    {
        if (_shield == null) return false;
        _shieldBlend = Mathf.MoveToward(_shieldBlend, ShieldUp ? 1f : 0f, dt * 6f);
        var b = Mathf.SmoothStep(0, 1, _shieldBlend);
        var p0 = new Vector3(0, 0.47f, 0);
        // Repos : sur le flanc gauche, légèrement tourné ; garde : devant la poitrine, face à la visée.
        var rest = new Transform3D(new Basis(Vector3.Up, 0.45f), p0 + V(0.34f, -0.12f, 0.2f));
        var aimR = new Basis(Vector3.Right, -pitch * 0.6f);
        var guard = new Transform3D(aimR, p0 + aimR * V(0.12f, -0.02f, 0.42f));
        var sx = rest.InterpolateWith(guard, b);
        var t = torsoModel.AffineInverse() * frame * sx;
        _shield.Transform = t;
        var gripB = t.Basis * VerticalGripL;
        SolveArm(_armL, _foreL, _handL, t.Origin - gripB * GripC, gripB, pole, 0, default, default);
        return true;
    }

    /// <summary>
    /// Modèle tenu : celui de la famille ; Air Raider : objets partagés (fumigène des frappes, désignateur laser, caisses de
    /// déploiement, pistolets de soutien, lance-bombes collantes par grande famille).
    /// </summary>
    public static string ModelName(Edf5Spec s) => s.Class != SoldierClass.Support ? s.Family : s.Kind switch
    {
        Edf5Kind.Artillery or Edf5Kind.Bomber => "smokecan",
        Edf5Kind.Gunship or Edf5Kind.AirMissile or Edf5Kind.Satellite => "designator",
        Edf5Kind.Guide => s.Family == "guidekit" ? "designator" : "assistgun",
        Edf5Kind.Limpet => s.Family is "limpetshot" or "busterbomb" ? "splendor" : s.Family,
        Edf5Kind.Sentry => "sentrycase",
        Edf5Kind.Robot => "robotcase",
        Edf5Kind.Post => s.Family is "lifevendor" or "powerpost" or "guardpost" ? "postcase" : "assistgun",
        Edf5Kind.Bunker or Edf5Kind.Decoy => "deployer",
        _ => s.Family == "suppress" ? "suppressgun" : s.Family,
    };

    /// <summary>Tenue selon la nature de l'arme.</summary>
    private static Hold HoldFor(Edf5Spec s) => s.Kind switch
    {
        // Air Raider : fumigène, désignateur et caisses à une main.
        _ when s.Class == SoldierClass.Support && ModelName(s) is "smokecan" or "designator" or "sentrycase" or "robotcase" or "postcase" or "deployer"
            => Hold.OneHand,
        // Fencer : armes de mêlée à une main, canons à la hanche, lance-missiles à l'épaule.
        Edf5Kind.Strike or Edf5Kind.Pierce => Hold.OneHand,
        Edf5Kind.Bullet or Edf5Kind.Flame or Edf5Kind.Mortar when s.Class == SoldierClass.Heavy => Hold.Hip,
        Edf5Kind.Rocket or Edf5Kind.GuidedRocket or Edf5Kind.Missile => Hold.Launcher,
        Edf5Kind.HandGrenade or Edf5Kind.Bomb or Edf5Kind.Impulse => Hold.OneHand,
        _ => Hold.Shoulder,
    };

    private static Dictionary<string, (Color, Color)> _palettes;

    /// <summary>
    /// Palettes des modèles (assets/models/weapons/palettes/&lt;module&gt;.json, écrites par l'export Blender) : la couleur du
    /// jeu est toujours celle du modèle. Clé : nom du modèle (famille, ou objet partagé de l'Air Raider).
    /// </summary>
    private static Dictionary<string, (Color, Color)> Palettes()
    {
        if (_palettes != null) return _palettes;
        _palettes = new Dictionary<string, (Color, Color)>();
        const string dir = "res://assets/models/weapons/palettes";
        if (!DirAccess.DirExistsAbsolute(dir)) return _palettes;
        foreach (var f in DirAccess.GetFilesAt(dir))
        {
            if (!f.EndsWith(".json")) continue;
            var json = Json.ParseString(FileAccess.GetFileAsString($"{dir}/{f}"));
            if (json.VariantType != Variant.Type.Dictionary) continue;
            foreach (var (k, v) in json.AsGodotDictionary())
            {
                var a = v.AsGodotArray();
                Color C(Variant c) { var x = c.AsGodotArray(); return new Color((float)x[0], (float)x[1], (float)x[2]); }
                _palettes[k.AsString()] = (C(a[0]), C(a[1]));
            }
        }
        return _palettes;
    }

    /// <summary>Couleurs de la famille (peinture, accent) : palette du modèle exporté, sinon valeurs ci-dessous.</summary>
    private static (Color paint, Color accent) GunColors(Edf5Spec s) =>
        Palettes().TryGetValue(ModelName(s), out var pc) ? pc : GunColorsFallback(s);

    private static (Color paint, Color accent) GunColorsFallback(Edf5Spec s) => s.Family switch
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
        // Wing Diver : coque claire, pièces sombres (quelques familles teintées, comme tools/blender/weapons/wingdiver.py).
        "dragoon" or "fenrir" => (new Color(0.55f, 0.57f, 0.6f), new Color(0.12f, 0.13f, 0.15f)),
        "monster" or "monsterlance" => (new Color(0.55f, 0.57f, 0.6f), new Color(0.5f, 0.12f, 0.08f)),
        "raijin" => (new Color(0.12f, 0.13f, 0.15f), new Color(0.6f, 0.5f, 0.15f)),
        "heavycannon" => (new Color(0.55f, 0.57f, 0.6f), new Color(0.5f, 0.25f, 0.05f)),
        "finaldays" => (new Color(0.12f, 0.13f, 0.15f), new Color(0.55f, 0.1f, 0.1f)),
        "vulcan" or "crusher" => (new Color(0.2f, 0.22f, 0.25f), new Color(0.6f, 0.33f, 0.08f)),
        "leviathan" => (new Color(0.32f, 0.3f, 0.22f), new Color(0.07f, 0.075f, 0.08f)),
        "ionmirror" => (new Color(0.5f, 0.52f, 0.56f), new Color(0.07f, 0.075f, 0.08f)),
        "reflector" => (new Color(0.55f, 0.57f, 0.6f), new Color(0.6f, 0.33f, 0.08f)),
        "tower" => (new Color(0.24f, 0.26f, 0.2f), new Color(0.07f, 0.075f, 0.08f)),
        "flamerevolver" => (new Color(0.2f, 0.22f, 0.25f), new Color(0.45f, 0.12f, 0.06f)),
        "disruptor" => (new Color(0.14f, 0.15f, 0.17f), new Color(0.6f, 0.33f, 0.08f)),
        // Air Raider : sable et pièces sombres ; fumigène et désignateur kaki ; matériel de soutien blanc.
        _ when s.Class == SoldierClass.Support && ModelName(s) is "smokecan" or "designator" => (new Color(0.26f, 0.29f, 0.2f), new Color(0.1f, 0.1f, 0.1f)),
        _ when s.Class == SoldierClass.Support && ModelName(s) is "assistgun" or "postcase" => (new Color(0.8f, 0.8f, 0.78f), new Color(0.1f, 0.1f, 0.1f)),
        _ when s.Class == SoldierClass.Support => (new Color(0.55f, 0.48f, 0.34f), new Color(0.1f, 0.1f, 0.1f)),
        // Fencer : acier bleuté, gaines sombres (comme tools/blender/weapons/fencer.py).
        _ when s.Class == SoldierClass.Heavy => (new Color(0.2f, 0.22f, 0.25f), new Color(0.07f, 0.075f, 0.08f)),
        _ when s.Class == SoldierClass.Airborne => (new Color(0.78f, 0.8f, 0.83f), new Color(0.12f, 0.13f, 0.15f)),
        _ => (new Color(0.16f, 0.17f, 0.12f), new Color(0.32f, 0.27f, 0.18f)),
    };
}
