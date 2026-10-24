using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Effets du souffle de feu du titan (purement visuels, chez chaque pair) : jet en deux couches (cœur incandescent,
/// gaine turbulente) étiré de la gueule au point d'impact, lumière d'impact, boules de feu et fumée le long du jet par
/// les émetteurs partagés d'<see cref="ExplosionFx"/> (aucun nœud par flamme), et foyers au sol laissés par le balayage
/// (flammes, fumée noire, traces de brûlure, quelques lumières la nuit). Un nœud par titan, hors du modèle (TopLevel).
/// </summary>
public partial class TitanFire : Node3D
{
    private const string JetCode = @"
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float speed = 2.6;
uniform float hot = 1.0;
uniform float gain = 2.2;
instance uniform float intensity = 1.0;
instance uniform float len_m = 60.0;

void fragment() {
	float t = UV.y;
	vec2 q = vec2(UV.x * 2.0, t * len_m * 0.04 - TIME * speed);
	float n = texture(noise_tex, q).r * 0.62 + texture(noise_tex, q * vec2(2.0, 1.6) + vec2(0.37, -TIME * speed * 0.7)).g * 0.38;
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float edge = smoothstep(0.0, 0.7, facing);
	float start = smoothstep(0.0, 0.035, t);
	float tail = 1.0 - smoothstep(0.8, 1.0, t);
	float heat = clamp(n * 1.2 + hot * (1.0 - t) * 0.55 - t * 0.3, 0.0, 1.0);
	vec3 c = mix(vec3(0.45, 0.04, 0.0), vec3(1.0, 0.36, 0.04), smoothstep(0.12, 0.55, heat));
	c = mix(c, vec3(1.0, 0.62, 0.2), smoothstep(0.68, 1.0, heat));
	float a = edge * start * tail * smoothstep(0.12, 0.62, n + 0.3 * (1.0 - t)) * intensity;
	ALBEDO = c * gain;
	ALPHA = clamp(a, 0.0, 1.0);
}";

    /// <summary>Gaine du jet : flammes opaques déchiquetées (mélange), plus sombres et enfumées vers le bout.</summary>
    private const string FlameCode = @"
shader_type spatial;
render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float speed = 2.2;
instance uniform float intensity = 1.0;
instance uniform float len_m = 60.0;

void fragment() {
	float t = UV.y;
	vec2 q = vec2(UV.x * 3.0, t * len_m * 0.035 - TIME * speed);
	float n = texture(noise_tex, q).r * 0.6 + texture(noise_tex, q * vec2(1.7, 2.2) + vec2(0.31, -TIME * speed * 0.8)).g * 0.4;
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float body = smoothstep(0.05, 0.8, facing);
	float start = smoothstep(0.0, 0.05, t);
	float tail = 1.0 - smoothstep(0.86, 1.0, t);
	float thr = mix(0.3, 0.56, t);
	float flame = smoothstep(thr, thr + 0.2, n * (0.45 + 0.75 * body) + 0.15 * (1.0 - t));
	float heat = clamp(n * 0.95 + (1.0 - t) * 0.25 - 0.15 + body * 0.1, 0.0, 1.0);
	vec3 c = mix(vec3(0.16, 0.02, 0.0), vec3(0.85, 0.17, 0.01), smoothstep(0.15, 0.55, heat));
	c = mix(c, vec3(1.0, 0.46, 0.06), smoothstep(0.65, 0.95, heat));
	// Fumée noire sur les bords, vers le bout du jet.
	float soot = smoothstep(0.45, 1.0, t) * (1.0 - body);
	c = mix(c, vec3(0.06, 0.05, 0.045), soot * 0.85);
	ALBEDO = c * mix(0.95, 0.8, soot);
	ALPHA = clamp(flame * start * tail * intensity * 0.88, 0.0, 1.0);
}";

    private static Shader _shader, _flameShader;
    private static ArrayMesh _jet;
    private static ImageTexture _noise;
    private static ShaderMaterial _outerMat, _coreMat;

    private MeshInstance3D _outer, _core;
    private OmniLight3D _impactLight;
    private readonly OmniLight3D[] _fireLights = new OmniLight3D[3];
    private World _world;
    private float _puffT, _clock;
    private bool _on;

    /// <summary>Foyer au sol : position, fin de vie, prochain jet de flammes.</summary>
    private sealed class Ground { public Vector3 P; public float End, Next, Smoke, Size; }
    private readonly List<Ground> _ground = new();

    /// <summary>Nombre de foyers actifs (diagnostic).</summary>
    public int GroundCount => _ground.Count;

    /// <summary>Titan propriétaire : une fois retiré, le jet s'éteint et le nœud disparaît après le dernier foyer.</summary>
    private Node _owner;

    public static TitanFire Create(World world, Node owner)
    {
        var f = new TitanFire { Name = "TitanFire", TopLevel = true, _world = world, _owner = owner };
        f.Build();
        return f;
    }

    private void Build()
    {
        Resources();
        _outer = new MeshInstance3D { Mesh = _jet, MaterialOverride = _outerMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        _core = new MeshInstance3D { Mesh = _jet, MaterialOverride = _coreMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(_outer);
        AddChild(_core);
        _impactLight = new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.15f), OmniRange = 30f, LightEnergy = 0f, Visible = false, ShadowEnabled = false };
        AddChild(_impactLight);
        for (var i = 0; i < _fireLights.Length; i++)
        {
            _fireLights[i] = new OmniLight3D { LightColor = new Color(1f, 0.48f, 0.15f), OmniRange = 14f, LightEnergy = 0f, Visible = false, ShadowEnabled = false };
            AddChild(_fireLights[i]);
        }
    }

    private static void Resources()
    {
        if (_jet != null) return;
        // Bruit périodique sur deux canaux (volutes larges, détail fin).
        const int n = 128;
        var a = new FastNoiseLite { Seed = 91, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.03f, FractalOctaves = 4 };
        var b = new FastNoiseLite { Seed = 92, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.07f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 3 };
        var ia = a.GetSeamlessImage(n, n, false, false, 0.1f, true);
        var ib = b.GetSeamlessImage(n, n, false, false, 0.1f, true);
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
            img.SetPixel(x, y, new Color(ia.GetPixel(x, y).R, ib.GetPixel(x, y).R, 0f, 1f));
        img.GenerateMipmaps();
        _noise = ImageTexture.CreateFromImage(img);
        _shader = new Shader { Code = JetCode };
        _flameShader = new Shader { Code = FlameCode };
        _outerMat = new ShaderMaterial { Shader = _flameShader, RenderPriority = 12 };
        _outerMat.SetShaderParameter("noise_tex", _noise);
        _outerMat.SetShaderParameter("speed", 2.0f);
        _coreMat = new ShaderMaterial { Shader = _shader, RenderPriority = 13 };
        _coreMat.SetShaderParameter("noise_tex", _noise);
        _coreMat.SetShaderParameter("speed", 3.4f);
        _coreMat.SetShaderParameter("hot", 0.55f);
        _coreMat.SetShaderParameter("gain", 0.8f);
        // Jet : tube évasé le long de -Z (longueur 1, rayon 1 au bout), UV : tour × longueur.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int rings = 28, radial = 18;
        for (var i = 0; i <= rings; i++)
        {
            var t = i / (float)rings;
            var r = 0.12f + 0.88f * Mathf.Pow(t, 0.62f) + 0.12f * t * t;
            for (var j = 0; j <= radial; j++)
            {
                var th = Mathf.Tau * j / radial;
                var nrm = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
                st.SetNormal(nrm);
                st.SetUV(new Vector2(j / (float)radial, t));
                st.AddVertex(new Vector3(nrm.X * r, nrm.Y * r, -t));
            }
        }
        for (var i = 0; i < rings; i++)
        for (var j = 0; j < radial; j++)
        {
            var a0 = i * (radial + 1) + j;
            var b0 = a0 + radial + 1;
            st.AddIndex(a0); st.AddIndex(b0); st.AddIndex(a0 + 1);
            st.AddIndex(a0 + 1); st.AddIndex(b0); st.AddIndex(b0 + 1);
        }
        _jet = st.Commit();
    }

    /// <summary>Jet visible de <paramref name="from"/> à <paramref name="to"/> ; <paramref name="k"/> = montée (0..1).</summary>
    public void Show(Vector3 from, Vector3 to, float k, Vector3 impactNormal, bool hitSomething)
    {
        var d = to - from;
        var len = d.Length();
        if (len < 1f) { HideJet(); return; }
        _on = true;
        var basis = Basis.LookingAt(d / len, Mathf.Abs(d.Y / len) > 0.95f ? Vector3.Right : Vector3.Up);
        var w = Mathf.Clamp(len * 0.1f, 3f, 9f) * k;
        _outer.GlobalTransform = new Transform3D(basis * Basis.FromScale(new Vector3(w, w, len * 1.04f)), from);
        _core.GlobalTransform = new Transform3D(basis * Basis.FromScale(new Vector3(w * 0.32f, w * 0.32f, len * 0.9f)), from);
        _outer.SetInstanceShaderParameter("len_m", len);
        _core.SetInstanceShaderParameter("len_m", len);
        _outer.SetInstanceShaderParameter("intensity", k);
        _core.SetInstanceShaderParameter("intensity", k);
        _outer.Visible = _core.Visible = true;
        _impactLight.Visible = true;
        _impactLight.GlobalPosition = to + impactNormal * 2.5f;
        _impactLight.LightEnergy = (6f + 2.5f * Mathf.Sin(_clock * 37f)) * k * (Settings.I.ReduceFlashes ? 0.35f : 1f);

        // Volume du jet : boules de feu le long du trajet, gerbe et fumée noire à l'impact (émetteurs partagés).
        _puffT -= (float)GetProcessDeltaTime();
        if (_puffT > 0f) return;
        _puffT = 0.035f;
        var fx = ExplosionFx.For(_world);
        var far = fx.CameraDistance(to) > 220f;
        var dir = d / len;
        var side = dir.Cross(Vector3.Up).Normalized();
        if (side == Vector3.Zero) side = Vector3.Right;
        for (var i = 0; i < (far ? 2 : 6); i++)
        {
            var t = Mathf.Pow(GD.Randf(), 0.7f);
            var p = from + d * t + side * (float)GD.RandRange(-1, 1) * w * 0.3f * t;
            fx.FirePuff(p, dir * (14f + 10f * GD.Randf()) + Vector3.Up * 3f, 2.5f + 6f * t, 0.55f + 0.5f * GD.Randf(), new Color(1f, 0.5f, 0.16f));
            if (t > 0.5f && GD.Randf() < 0.3f) fx.SmokePuff(p + Vector3.Up * 3f, dir * 6f + Vector3.Up * 4f, 5f + 4f * t, 3f + 2f * GD.Randf(), new Color(0.08f, 0.07f, 0.06f));
        }
        if (hitSomething)
        {
            var up = (impactNormal + Vector3.Up * 0.5f).Normalized();
            fx.FirePuff(to + up * 1.5f, up * 7f + side * (float)GD.RandRange(-6, 6), 4.5f + 2f * GD.Randf(), 0.9f + 0.5f * GD.Randf(), new Color(1f, 0.5f, 0.18f));
            if (!far && GD.Randf() < 0.55f) fx.Spark(to + up, (up + new Vector3((float)GD.RandRange(-1, 1), 0.6f, (float)GD.RandRange(-1, 1))).Normalized() * (float)GD.RandRange(8, 18), 0.18f, 0.9f, new Color(1f, 0.6f, 0.25f));
            if (GD.Randf() < 0.35f) fx.SmokePuff(to + up * 4f, Vector3.Up * 4f, 6f + 3f * GD.Randf(), 4f + 2f * GD.Randf(), new Color(0.07f, 0.065f, 0.06f));
        }
    }

    public void HideJet()
    {
        if (!_on) return;
        _on = false;
        _outer.Visible = _core.Visible = false;
        _impactLight.Visible = false;
    }

    /// <summary>Le sol prend feu à cet endroit (foyer de <paramref name="life"/> s), trace de brûlure.</summary>
    public void AddGround(Vector3 p, float life)
    {
        foreach (var g in _ground)
            if (g.P.DistanceSquaredTo(p) < 9f) { g.End = Mathf.Max(g.End, _clock + life); return; }
        if (_ground.Count >= 28) _ground.RemoveAt(0);
        _ground.Add(new Ground { P = p, End = _clock + life, Next = 0f, Size = (float)GD.RandRange(2.6, 4f) });
        ExplosionFx.For(_world).ScorchMark(p, (float)GD.RandRange(7, 10));
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _clock += dt;
        var orphan = _owner == null || !IsInstanceValid(_owner) || !_owner.IsInsideTree();
        if (orphan) HideJet();
        if (_ground.Count == 0)
        {
            if (orphan) { QueueFree(); return; }
            foreach (var l in _fireLights) l.Visible = false;
            return;
        }
        var fx = ExplosionFx.For(_world);
        var cam = GetViewport()?.GetCamera3D();
        var camPos = cam?.GlobalPosition ?? Vector3.Zero;
        for (var i = _ground.Count - 1; i >= 0; i--)
        {
            var g = _ground[i];
            if (_clock >= g.End) { _ground.RemoveAt(i); continue; }
            var left = g.End - _clock;
            var fade = Mathf.Clamp(left / 3f, 0.25f, 1f);
            var dist = camPos.DistanceTo(g.P);
            g.Next -= dt;
            if (g.Next <= 0f)
            {
                // Plus loin, moins de flammes (mais toujours visibles).
                g.Next = dist < 90f ? 0.11f : dist < 200f ? 0.22f : 0.45f;
                var o = new Vector3((float)GD.RandRange(-1.5, 1.5), 0f, (float)GD.RandRange(-1.5, 1.5)) * g.Size * 0.5f;
                fx.FirePuff(g.P + o + Vector3.Up * 0.6f, Vector3.Up * (2.5f + 2f * GD.Randf()), g.Size * fade, 0.8f + 0.5f * GD.Randf(), new Color(1f, 0.5f, 0.16f));
            }
            g.Smoke -= dt;
            if (g.Smoke <= 0f)
            {
                g.Smoke = dist < 120f ? 0.7f : 1.4f;
                fx.SmokePuff(g.P + Vector3.Up * 3f, Vector3.Up * 3.5f, 4f + 2f * GD.Randf(), 5f + 2f * GD.Randf(), new Color(0.06f, 0.055f, 0.05f));
            }
        }
        // Lumières sur les trois foyers les plus récents (feux du soir et de nuit).
        for (var i = 0; i < _fireLights.Length; i++)
        {
            var idx = _ground.Count - 1 - i * 3;
            var l = _fireLights[i];
            if (idx < 0) { l.Visible = false; continue; }
            var g = _ground[idx];
            l.Visible = true;
            l.GlobalPosition = g.P + Vector3.Up * 2f;
            l.LightEnergy = (2.2f + 0.8f * Mathf.Sin(_clock * 13f + i * 2f)) * Mathf.Clamp((g.End - _clock) / 3f, 0f, 1f);
        }
    }

    /// <summary>Préchauffage (déploiement) : jets et lumière visibles en miniature sous le nœud donné (shaders compilés).</summary>
    public static void Prewarm(Node3D parent)
    {
        Resources();
        var a = new MeshInstance3D { Mesh = _jet, MaterialOverride = _outerMat, Position = new Vector3(0, 30, 10), Scale = new Vector3(4, 4, 30) };
        var b = new MeshInstance3D { Mesh = _jet, MaterialOverride = _coreMat, Position = new Vector3(0, 30, 10), Scale = new Vector3(2, 2, 28) };
        parent.AddChild(a);
        parent.AddChild(b);
    }
}
