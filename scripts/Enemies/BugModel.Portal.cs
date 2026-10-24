using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Portail extraterrestre (spécification §10.1, ≈ 18 m de haut) : socle hexagonal en deux gradins, anneau vertical de
/// dix-huit voussoirs d'armure hérissés d'épines, deux piliers obliques, liserés et glyphes violets ; membrane en
/// vortex (shader) qui tourne et pulse, lueur violette. Plans du portail face à ±Z (repère du modèle).
/// Pièces non cuites (structure fixe, peu nombreuse, avec une lumière).
/// </summary>
public partial class BugModel
{
    /// <summary>Centre de l'anneau (hauteur) et rayon intérieur de la membrane.</summary>
    public const float PortalRingY = 9.2f, PortalRingR = 6.6f;

    private MeshInstance3D _pMembrane;
    private OmniLight3D _pLight;
    private ShaderMaterial _pMat;
    private float _pSurge;

    private const string PortalCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, shadows_disabled;

uniform vec4 col : source_color = vec4(0.72, 0.42, 1.0, 1.0);
uniform float surge = 0.0;
uniform float collapse = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float rr = r / max(1.0 - collapse, 0.001);
	if (rr > 1.0) discard;
	// Tourbillon : le plan tourne d'autant plus vite qu'on approche du centre (pas de couture d'angle).
	float ang = 2.8 / (rr + 0.22) - TIME * 1.4;
	vec2 q = mat2(vec2(cos(ang), sin(ang)), vec2(-sin(ang), cos(ang))) * p;
	float n = noise(q * 2.6 + vec2(TIME * 0.15, 0.0)) * 0.5 + noise(q * 6.3 - vec2(0.0, TIME * 0.3)) * 0.32 + noise(q * 14.0 + TIME * 0.5) * 0.18;
	float bands = smoothstep(0.45, 0.85, n);
	float edge = smoothstep(0.8, 1.0, rr);
	float eye = 1.0 - smoothstep(0.0, 0.22, rr);
	float pit = smoothstep(0.05, 0.5, rr);
	vec3 dark = vec3(0.012, 0.0, 0.025);
	vec3 c = mix(dark, col.rgb * 0.75, bands * (0.15 + 0.85 * rr) * pit) + col.rgb * edge * (0.9 + surge);
	c += col.rgb * eye * (0.25 + 0.15 * sin(TIME * 5.0)) * (1.0 + surge * 3.0);
	ALBEDO = c * 1.7;
	ALPHA = clamp(0.86 + 0.14 * edge + surge * 0.1, 0.0, 1.0) * (1.0 - smoothstep(0.985, 1.0, rr));
}
";

    private void BuildPortal()
    {
        var stone = new Color(0.12f, 0.115f, 0.11f);
        var stone2 = new Color(0.2f, 0.19f, 0.175f);
        var root = new Node3D { Name = "Portal" };
        AddChild(root);
        Part(root, "frame", m =>
        {
            m.Use(AlienArmor());
            // Socle : deux gradins hexagonaux, rampe d'accès de chaque côté.
            Cyl(m, new Vector3(0, -0.4f, 0), new Vector3(0, 0.9f, 0), 8.6f, 6, stone, 0.3f, 8.2f);
            Cyl(m, new Vector3(0, 0.85f, 0), new Vector3(0, 1.55f, 0), 6.4f, 6, stone2, 0.2f, 6.1f);
            for (var s = -1; s <= 1; s += 2)
                Plate(m, new Transform3D(new Basis(Vector3.Right, s * 0.16f), new Vector3(0, 0.55f, s * 7.6f)), new Vector3(3.2f, 0.25f, 2.2f), stone2, 0.15f, 8f);
            // Anneau : voussoirs à joints sombres, alternance de deux teintes.
            const int n = 18;
            for (var k = 0; k < n; k++)
            {
                var a = k * Mathf.Tau / n + Mathf.Pi / n;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0);
                var c = new Vector3(0, PortalRingY, 0) + radial * (PortalRingR + 0.8f);
                var b = new Basis(radial.Cross(tangent), radial, tangent);
                Plate(m, new Transform3D(b, c), new Vector3(1.05f, 0.85f, 1.08f), k % 2 == 0 ? stone : stone2, 0.18f, 6f, 0.92f);
                // Nervure frontale de chaque voussoir.
                Plate(m, new Transform3D(b, c + radial * 0.15f + Vector3.Back * 1.0f), new Vector3(0.1f, 0.4f, 0.8f), stone2, 0.05f, 6f);
                Plate(m, new Transform3D(b, c + radial * 0.15f + Vector3.Forward * 1.0f), new Vector3(0.1f, 0.4f, 0.8f), stone2, 0.05f, 6f);
            }
            // Piliers obliques, contreforts.
            for (var s = -1; s <= 1; s += 2)
            {
                var foot = new Vector3(s * 6.6f, 1.4f, 0);
                var top = new Vector3(s * 7.3f, 6.8f, 0);
                var dir = (top - foot).Normalized();
                var x = Vector3.Back.Cross(dir).Normalized();
                Plate(m, new Transform3D(new Basis(x, Vector3.Back, dir), (foot + top) / 2), new Vector3(0.95f, 1.25f, (top - foot).Length() / 2 + 0.4f), stone, 0.2f, 6f, 0.8f);
                Plate(m, new Transform3D(new Basis(Vector3.Up, 0), new Vector3(s * 8.6f, 1.6f, 0)), new Vector3(0.8f, 1.2f, 1.6f), stone2, 0.2f, 6f, 0.7f);
            }
            m.Use(MachMech());
            // Épines et crochets qui hérissent l'anneau (grim), câbles du socle vers les piliers.
            for (var k = 0; k < 9; k++)
            {
                var a = k * Mathf.Tau / 9 + 0.2f;
                if (Mathf.Sin(a) < -0.55f) continue;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var b0 = new Vector3(0, PortalRingY, 0) + radial * (PortalRingR + 1.5f);
                m.Hook(new[] { b0, b0 + radial * 1.6f + Vector3.Back * 0.3f, b0 + radial * 2.8f + Vector3.Back * 0.9f }, 0.32f, 6, ATrim, Vector3.Back);
            }
            for (var s = -1; s <= 1; s += 2)
            for (var z = -1; z <= 1; z += 2)
                Cable(m, new Vector3(s * 3.5f, 1.6f, z * 1.2f), new Vector3(s * 6.6f, 4.5f, z * 0.8f), 0.16f, 0.7f, ATrim);
            m.Use(AlienGlow());
            // Liserés de l'anneau (deux faces), glyphes sur le socle.
            foreach (var z in new[] { -1.13f, 1.13f })
                m.Torus(new Vector3(0, PortalRingY, z), new Basis(Vector3.Right, Mathf.Pi / 2), PortalRingR + 0.05f, 0.09f, 48, 4, Colors.White);
            for (var k = 0; k < 6; k++)
            {
                var a = k * Mathf.Tau / 6 + Mathf.Pi / 6;
                var c = new Vector3(Mathf.Cos(a) * 7.0f, 0.93f, Mathf.Sin(a) * 7.0f);
                m.Box(new Transform3D(new Basis(Vector3.Up, -a), c), new Vector3(0.12f, 0.03f, 0.9f), Colors.White);
            }
        });
        _pMat = new ShaderMaterial { Shader = new Shader { Code = PortalCode } };
        _pMat.SetShaderParameter("col", AlienEnergy);
        _pMembrane = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(PortalRingR * 2f, PortalRingR * 2f) },
            MaterialOverride = _pMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, PortalRingY, 0),
        };
        root.AddChild(_pMembrane);
        _pLight = new OmniLight3D { LightColor = AlienEnergy, LightEnergy = 3.5f, OmniRange = 34f, Position = new Vector3(0, PortalRingY, 0), ShadowEnabled = false };
        root.AddChild(_pLight);
    }

    /// <summary>
    /// Préchauffage pendant l'écran de déploiement si la mission compte des aliens (ou des portails, qui en font passer de
    /// tous les types) : maillages cuits une fois, un exemplaire minuscule de chaque devant la caméra et un trait d'énergie,
    /// le temps de compiler les shaders (pas d'à-coup à la première apparition).
    /// </summary>
    public static void PrewarmAliens(Camera3D cam, MissionDef mission)
    {
        if (cam == null) return;
        var types = mission.Phases.SelectMany(p => p.Waves).SelectMany(w => w.Groups).Select(g => g.type)
            .Where(t => t is EnemyType.AlienTrooper or EnemyType.AlienGuard or EnemyType.AlienFlyer).Distinct().ToList();
        if (mission.NestType == EnemyType.Portal)
            types = new System.Collections.Generic.List<EnemyType> { EnemyType.AlienTrooper, EnemyType.AlienGuard, EnemyType.AlienFlyer, EnemyType.Portal };
        if (types.Count == 0) return;
        var root = new Node3D { Name = "AlienPrewarm", Position = new Vector3(-0.3f, 0, -4f), Scale = Vector3.One * 0.004f };
        cam.AddChild(root);
        foreach (var t in types) root.AddChild(Create(t));
        root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f }, MaterialOverride = Mats.Glow(AlienEnergy, 6f) });
        cam.GetTree().CreateTimer(1.5f).Timeout += () => { if (IsInstanceValid(root)) root.QueueFree(); };
    }

    /// <summary>Le portail laisse passer un groupe : la membrane s'emballe et la lueur enfle.</summary>
    public void PortalSurge() => _pSurge = 1f;

    public void AnimatePortal(float dt)
    {
        _t += dt;
        if (_dead || _pMat == null) return;
        _pSurge = Mathf.Max(0, _pSurge - dt * 0.7f);
        _pMat.SetShaderParameter("surge", _pSurge);
        if (_pLight != null) _pLight.LightEnergy = 3.2f + Mathf.Sin(_t * 5.3f) * 0.4f + Mathf.Sin(_t * 13.1f) * 0.25f + _pSurge * 6f;
    }

    /// <summary>Mort du portail : la membrane se résorbe en son centre, la lueur s'éteint.</summary>
    public void PortalDeath()
    {
        _dead = true;
        if (_pMat == null) return;
        var tw = CreateTween();
        tw.TweenMethod(Callable.From<float>(k =>
        {
            _pMat.SetShaderParameter("collapse", k);
            _pMat.SetShaderParameter("surge", 2f * (1f - k));
            if (IsInstanceValid(_pLight)) _pLight.LightEnergy = 12f * (1f - k);
        }), 0f, 1f, 0.9f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenCallback(Callable.From(() => { _pMembrane.Visible = false; _pLight.Visible = false; }));
    }
}
