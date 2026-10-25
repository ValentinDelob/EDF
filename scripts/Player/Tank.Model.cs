using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Modèle du char (modélisé dans Blender par tools/blender/tank.py, exporté en assets/models/tank.glb) :
/// matières du jeu par nom de matériau, chenilles dont les maillons défilent (shader) et roues qui tournent,
/// chaque côté à sa vitesse (virages en différentiel).
/// </summary>
public partial class Tank
{
    private const string ModelPath = "res://assets/models/tank.glb";

    private readonly List<(Node3D node, float radius, int side)> _rollers = new();
    private ShaderMaterial _trackL, _trackR;
    private float _beltL, _beltR, _lastYaw;
    private Node3D _tube;
    private float _tubeRestZ;

    private static Shader _trackShader;

    /// <summary>
    /// Bande de chenille : UV.y = abscisse le long de la bande (m), UV.x = position autour de la section (face extérieure : 0,5 à 0,75).
    /// Patins à crampons (pas de 18 cm), axes et guides visibles sur la face intérieure, défilement par « scroll ».
    /// </summary>
    private const string TrackCode = @"
shader_type spatial;
uniform float scroll = 0.0;
uniform vec3 steel : source_color = vec3(0.3, 0.29, 0.27);
uniform vec3 pad : source_color = vec3(0.075, 0.072, 0.068);
// Relief des crampons : UV2 = normale sortante de la bande (y, z du repère du modèle) sur les arêtes extérieures, 0 ailleurs.
float grouser_at(float v, float sc) { float f = fract((v - sc) / 0.18); return smoothstep(0.25, 0.33, f) * smoothstep(0.65, 0.57, f); }
void vertex() {
	VERTEX += vec3(0.0, UV2.x, UV2.y) * grouser_at(UV.y, scroll) * 0.045;
}
void fragment() {
	float f = fract((UV.y - scroll) / 0.18);
	float u = UV.x;
	bool outer = u > 0.5 && u < 0.75;
	bool inner = u < 0.25;
	bool side = !outer && !inner;
	float face = outer ? (u - 0.5) * 4.0 : inner ? u * 4.0 : (u < 0.5 ? (u - 0.25) * 4.0 : (u - 0.75) * 4.0);
	// Jour entre deux maillons, crampon transversal (face extérieure), guide central (face intérieure),
	// connecteurs d'extrémité (flancs) : blocs d'acier séparés par un jour sombre.
	float gap = smoothstep(0.0, 0.07, f) * smoothstep(1.0, 0.93, f);
	float grouser = outer ? grouser_at(UV.y, scroll) : 0.0;
	float guide = inner ? smoothstep(0.42, 0.45, face) * smoothstep(0.58, 0.55, face) * step(0.2, f) * step(f, 0.7) : 0.0;
	float conn = side ? smoothstep(0.12, 0.18, f) * smoothstep(0.88, 0.82, f) * smoothstep(0.1, 0.3, face) * smoothstep(0.95, 0.75, face) : 0.0;
	float pins = outer ? (step(face, 0.08) + step(0.92, face)) : 0.0;
	vec3 col = mix(pad, steel, clamp(grouser + guide + conn + pins * 0.7, 0.0, 1.0));
	col *= mix(0.3, 1.0, gap);
	col += vec3(0.06) * (grouser * smoothstep(0.5, 0.56, f) + conn * 0.5);
	ALBEDO = col;
	ROUGHNESS = mix(0.85, 0.4, clamp(grouser + guide + conn, 0.0, 1.0));
	METALLIC = 0.65 * clamp(grouser + conn + pins, 0.0, 1.0);
}";

    private void BuildModel()
    {
        var scene = GD.Load<PackedScene>(ModelPath);
        var model = scene.Instantiate<Node3D>();
        model.Name = "Model";
        _tilt.AddChild(model);
        _hull = model;

        _trackShader ??= new Shader { Code = TrackCode };
        var mats = new Dictionary<string, Material>
        {
            ["Paint"] = Mats.CarPaint(new Color(0.33f, 0.36f, 0.39f)),
            ["Accent"] = Mats.CarPaint(new Color(0.5f, 0.48f, 0.26f)),
            ["Dark"] = Mats.Metal(new Color(0.07f, 0.07f, 0.07f), 0.55f),
            ["Rubber"] = Mats.Painted(new Color(0.035f, 0.035f, 0.035f), 0.95f),
            ["Glass"] = Mats.Painted(new Color(0.03f, 0.05f, 0.06f), 0.08f, 0.5f),
            ["Lamp"] = Mats.Glow(new Color(1f, 0.92f, 0.75f), 0.8f),
        };
        _trackL = new ShaderMaterial { Shader = _trackShader };
        _trackR = new ShaderMaterial { Shader = _trackShader };

        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            var mesh = mi.Mesh;
            for (var s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                var name = mesh.SurfaceGetMaterial(s)?.ResourceName ?? "";
                if (name == "Track") mi.SetSurfaceOverrideMaterial(s, mi.Name.ToString().EndsWith("_L") ? _trackL : _trackR);
                else if (mats.TryGetValue(name, out var m)) mi.SetSurfaceOverrideMaterial(s, m);
            }
            var nm = mi.Name.ToString();
            if (nm.StartsWith("RoadWheel") || nm.StartsWith("Sprocket") || nm.StartsWith("Idler") || nm.StartsWith("Roller"))
            {
                var aabb = mesh.GetAabb();
                var radius = Mathf.Max(aabb.Size.Y, aabb.Size.Z) * 0.5f;
                _rollers.Add((mi, radius, nm.Contains("_L") ? -1 : 1));
            }
        }
        _turret = model.FindChild("Turret", true, false) as Node3D;
        _barrel = model.FindChild("Gun", true, false) as Node3D;
        _tube = model.FindChild("Tube", true, false) as Node3D;
        _muzzle = model.FindChild("Muzzle", true, false) as Node3D;
        _tubeRestZ = _tube.Position.Z;
        BuildSuspension(model);
    }

    /// <summary>
    /// Chenilles et roues : vitesse de chaque bande = vitesse du char ± rotation × demi-voie (le côté extérieur au virage
    /// va plus vite) ; en rotation sur place, les deux bandes tournent en sens contraire.
    /// </summary>
    private float AnimateTracks(float dt)
    {
        if (dt <= 0f) return 0f;
        var yaw = Rotation.Y;
        var yawRate = Mathf.Wrap(yaw - _lastYaw, -Mathf.Pi, Mathf.Pi) / dt;
        _lastYaw = yaw;
        if (Wrecked) return 0f;
        const float halfTrack = 1.43f;
        var vL = _speed + yawRate * halfTrack;
        var vR = _speed - yawRate * halfTrack;
        _beltL += vL * dt;
        _beltR += vR * dt;
        _trackL.SetShaderParameter("scroll", _beltL);
        _trackR.SetShaderParameter("scroll", _beltR);
        foreach (var (node, r, side) in _rollers)
        {
            var v = side < 0 ? vL : vR;
            node.Rotate(Vector3.Right, v / Mathf.Max(r, 0.05f) * dt);
        }
        return yawRate;
    }

    /// <summary>Recul du fût au départ du coup (le masque reste en place).</summary>
    private void Recoil()
    {
        var tw = CreateTween();
        tw.TweenProperty(_tube, "position:z", _tubeRestZ - 0.45f, 0.05f);
        tw.TweenProperty(_tube, "position:z", _tubeRestZ, 0.4f).SetTrans(Tween.TransitionType.Sine);
    }
}
