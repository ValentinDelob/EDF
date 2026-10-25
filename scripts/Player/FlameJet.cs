using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Jet des lance-flammes d'EDF 5 (Burner, Flamethrower, Flame Cannon, Magma Cannon) : 60 flammèches par seconde qui
/// s'élargissent et ralentissent. Chaque flammèche brûle chaque ennemi qu'elle enveloppe toutes les 0,1 s pour 6 fois
/// les dégâts de l'arme (les dégâts d'EDF 5 sont donnés par image, à 60 images par seconde). Rendu : un MultiMesh.
/// </summary>
public partial class FlameJet : Node3D
{
    private struct Lick
    {
        public Vector3 Pos, Vel;
        public float Life, MaxLife, Damage, Size, Tick;
        public bool Local;
    }

    private readonly List<Lick> _licks = new();
    private World _world;
    private MultiMesh _mm;
    private float _carry;
    private const int Capacity = 1500;

    public static FlameJet For(World w)
    {
        if (w.GetNodeOrNull<FlameJet>("FlameJet") is { } f) return f;
        f = new FlameJet { Name = "FlameJet", _world = w };
        w.AddChild(f);
        return f;
    }

    public override void _Ready()
    {
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) } },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 32, Height = 32,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
            VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, InstanceCount = Capacity, VisibleInstanceCount = 0,
            Mesh = new QuadMesh { Size = Vector2.One, Material = mat },
        };
        AddChild(new MultiMeshInstance3D { Multimesh = _mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Émet les flammèches de <paramref name="dt"/> secondes de tir.</summary>
    public void Emit(Edf5Spec s, float damage, Vector3 from, Vector3 dir, float dt, bool local)
    {
        var cannon = s.Family == "flamecannon";
        var speed = cannon ? 58f : s.Name == "Burner" ? 28f : 36f;
        var life = cannon ? 0.95f : s.Name == "Burner" ? 0.42f : 0.6f;
        _carry += dt * 60f;
        var rng = _world.Rng;
        while (_carry >= 1f && _licks.Count < Capacity)
        {
            _carry -= 1f;
            var d = Edf5Fire.Spread(dir, Mathf.DegToRad(cannon ? 2.5f : 5f), rng);
            _licks.Add(new Lick { Pos = from, Vel = d * speed * (0.85f + 0.3f * rng.Randf()), Life = life, MaxLife = life, Damage = damage, Size = cannon ? 0.7f : 0.45f, Local = local });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var enemies = _world.EnemiesById.Values;
        for (var i = _licks.Count - 1; i >= 0; i--)
        {
            var l = _licks[i];
            l.Life -= dt;
            if (l.Life <= 0) { _licks.RemoveAt(i); continue; }
            l.Vel = l.Vel * (1f - 1.6f * dt) + Vector3.Up * 3f * dt;
            l.Pos += l.Vel * dt;
            var k = 1f - l.Life / l.MaxLife;
            var r = l.Size * (1f + 3.5f * k);
            if (l.Local)
            {
                l.Tick -= dt;
                if (l.Tick <= 0)
                {
                    l.Tick = 0.1f;
                    foreach (var e in enemies)
                    {
                        if (e.State == EnemyState.Dead) continue;
                        if (e.Center.DistanceTo(l.Pos) > r + e.Def.Size * 1.3f) continue;
                        var dmg = l.Damage * 6f;
                        Net.I.SendDamageEnemy(e.Id, dmg, l.Pos, false);
                        _world.LocalHitFx(e, l.Pos, false, dmg, false);
                        if (_world.LocalPlayer is { } p) { p.HitMarker = 1f; p.EarnCredits(dmg); }
                    }
                }
            }
            _licks[i] = l;
        }
    }

    public override void _Process(double delta)
    {
        var n = _licks.Count;
        _mm.VisibleInstanceCount = n;
        for (var i = 0; i < n; i++)
        {
            var l = _licks[i];
            var k = 1f - l.Life / l.MaxLife;
            var r = l.Size * (1f + 3.5f * k);
            _mm.SetInstanceTransform(i, new Transform3D(Basis.FromScale(Vector3.One * r * 2f), l.Pos));
            // Bleu-blanc à la sortie, jaune, orange puis fumée sombre.
            var c = k < 0.15f ? new Color(0.7f, 0.8f, 1f).Lerp(new Color(1f, 0.85f, 0.4f), k / 0.15f)
                : k < 0.6f ? new Color(1f, 0.85f, 0.4f).Lerp(new Color(1f, 0.38f, 0.08f), (k - 0.15f) / 0.45f)
                : new Color(1f, 0.38f, 0.08f).Lerp(new Color(0.15f, 0.12f, 0.1f), (k - 0.6f) / 0.4f);
            c.A = (1f - k) * 0.85f;
            _mm.SetInstanceColor(i, c);
        }
    }
}
