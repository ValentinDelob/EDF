using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Dégâts progressifs et effondrement des immeubles.
/// Paliers (hôte, diffusés avec le point d'impact) : 1 fissures et éclats de façade, 2 trou béant et incendie à
/// l'étage touché, 3 second trou, feu plus fort et fumée du toit. À 0 PV : effondrement en plusieurs temps
/// (grondement et façades qui se vident, enfoncement en s'inclinant avec poussière au pied et éclats, nuage de
/// poussière qui roule dans les rues), tas de gravats solide (convexe, praticable) qui monte à la place.
/// </summary>
public partial class Building
{
    /// <summary>Palier atteint (hôte) et palier affiché (tous les pairs).</summary>
    public int HostStage, Stage;

    private static int _fires, _fireLights;
    private const int MaxFires = 12;
    private static Texture2D _holeTex, _crackTex;
    private List<Node> _stageFx;

    /// <summary>Palier de dégâts pour une fraction de PV restante.</summary>
    public int StageFor(float frac) => frac <= 0.15f ? 3 : frac <= 0.4f ? 2 : frac <= 0.7f ? 1 : 0;

    /// <summary>Affiche les paliers manquants jusqu'à <paramref name="stage"/> (tous les pairs).</summary>
    public void ShowStage(World w, int stage, Vector3 at)
    {
        if (Destroyed) return;
        while (Stage < stage)
        {
            Stage++;
            StageFx(w, Stage, at);
        }
    }

    private float H(int a, int salt) => RuinKit.Hash(Id * 13 + a, Stage * 7 + salt, 91);

    /// <summary>Point de façade (monde) le plus proche de l'impact, normale sortante et direction le long de la façade.</summary>
    private (Vector3 p, Vector3 n, Vector3 along) FacadePoint(Vector3 at, int salt)
    {
        var hx = Size.X / 2;
        var hz = Size.Z / 2;
        var local = at == Vector3.Zero
            ? new Vector3((H(1, salt) * 2 - 1) * hx * 1.5f, Size.Y * (0.3f + 0.5f * H(2, salt)), (H(3, salt) * 2 - 1) * hz * 1.5f)
            : at - GlobalPosition;
        var dx = Mathf.Abs(Mathf.Abs(local.X) - hx);
        var dz = Mathf.Abs(Mathf.Abs(local.Z) - hz);
        Vector3 n;
        if (dx < dz) { n = new Vector3(Mathf.Sign(local.X == 0 ? 1 : local.X), 0, 0); local.X = n.X * hx; local.Z = Mathf.Clamp(local.Z, -hz + 1.2f, hz - 1.2f); }
        else { n = new Vector3(0, 0, Mathf.Sign(local.Z == 0 ? 1 : local.Z)); local.Z = n.Z * hz; local.X = Mathf.Clamp(local.X, -hx + 1.2f, hx - 1.2f); }
        // Un impact au sol abîme le rez-de-chaussée / premier étage.
        local.Y = Mathf.Clamp(local.Y < 1.5f ? 2.5f + H(4, salt) * 2f : local.Y, 1.6f, Mathf.Max(1.7f, Size.Y - 1.4f));
        return (GlobalPosition + local, n, new Vector3(n.Z, 0, -n.X));
    }

    private Color ChunkColor(float k) => Tint(Id) * new Color(0.5f, 0.49f, 0.47f) * k;

    private void StageFx(World w, int stage, Vector3 at)
    {
        if (Standing) { StandingFx(w, stage, at); return; }
        var (p, n, along) = FacadePoint(at, stage);
        if (stage == 3)
        {
            // Second foyer : plus haut, décalé le long de la façade.
            p += along * ((H(5, 3) - 0.5f) * Mathf.Min(Size.X, Size.Z) * 0.6f);
            p.Y = Mathf.Clamp(p.Y + 3.2f, GlobalPosition.Y + 1.6f, GlobalPosition.Y + Mathf.Max(1.7f, Size.Y - 1.4f));
        }
        var fx = ExplosionFx.For(w);
        var dist = fx.CameraDistance(p);
        _stageFx ??= new List<Node>();

        // Fissures (palier 1) ou trou béant (paliers 2, 3) : plaque transparente plaquée contre la façade.
        var hole = stage >= 2;
        var s = hole ? 7f + H(6, 0) * 2.5f : 7.5f + H(6, 0) * 3f;
        var scar = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(s, s * 0.85f) },
            MaterialOverride = hole ? HoleMat : CrackMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = 320f,
        };
        AddChild(scar);
        scar.GlobalTransform = new Transform3D(new Basis(along, Vector3.Up, n), p + n * (0.06f + stage * 0.01f));
        _stageFx.Add(scar);
        if (dist > 300f) return;

        // Éclats de façade : blocs physiques près de la caméra, particules au-delà.
        var pool = DebrisPool.For(w);
        var rng = new RandomNumberGenerator { Seed = (ulong)(Id * 31 + stage) };
        float R(float a, float b) => rng.RandfRange(a, b);
        var nBlocks = dist < 75f ? 4 + stage * 2 : 0;
        for (var i = 0; i < nBlocks; i++)
        {
            var size = new Vector3(R(0.35f, 1.1f), R(0.25f, 0.7f), R(0.3f, 0.9f));
            var pos = p + n * 0.6f + along * R(-1.5f, 1.5f) + Vector3.Up * R(-1f, 1f);
            var vel = n * R(2f, 6f) + Vector3.Up * R(0f, 2.5f) + along * R(-2f, 2f);
            pool.Spawn(new Transform3D(new Basis(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)).Normalized() + Vector3.Up * 0.01f, R(0, 3f)), pos), size, vel,
                new Vector3(R(-4, 4), R(-4, 4), R(-4, 4)), ChunkColor(R(0.75f, 1.1f)), R(9f, 13f));
        }
        for (var i = 0; i < 12 + stage * 6; i++)
            fx.Chunk(p + n * 0.4f + along * R(-1.5f, 1.5f), n * R(1f, 6f) + Vector3.Up * R(-1f, 3f) + along * R(-2f, 2f), R(0.05f, 0.25f), R(3f, 5.5f), ChunkColor(R(0.6f, 1.1f)));
        // Poussière : bouffée à l'impact, rideau qui glisse le long de la façade, nappe au pied.
        var dust = new Color(0.34f, 0.32f, 0.29f, 0.55f);
        for (var i = 0; i < 6; i++)
            fx.DustPuff(p + n * 1.2f + along * R(-1.5f, 1.5f), n * R(2f, 5f) + new Vector3(R(-1, 1), R(-0.5f, 1f), R(-1, 1)), R(1.4f, 2.3f), R(2.2f, 3.5f), dust * R(0.9f, 1.1f));
        for (var y = p.Y - 2f; y > GlobalPosition.Y + 0.5f; y -= 3f)
            fx.DustPuff(new Vector3(p.X, y, p.Z) + n * 0.9f + along * R(-1f, 1f), n * R(0.3f, 1f) + Vector3.Down * R(0.5f, 1.5f), R(0.9f, 1.5f), R(1.8f, 2.8f), dust * 0.75f);
        var foot = new Vector3(p.X, GlobalPosition.Y, p.Z) + n * 2f;
        for (var i = 0; i < 4; i++)
            fx.DustPuff(foot + along * R(-2f, 2f), (n + along * R(-0.6f, 0.6f)) * R(2f, 4f), R(1.5f, 2.4f), R(2.5f, 3.5f), dust);
        // Vitres qui éclatent et tombent.
        for (var i = 0; i < 24; i++)
            fx.Spark(p + n * 0.3f + along * R(-2.5f, 2.5f) + Vector3.Up * R(-1.5f, 1.5f), n * R(1f, 4f) + Vector3.Up * R(-1f, 2f) + along * R(-1f, 1f), R(0.02f, 0.04f), R(0.8f, 1.6f),
                new Color(0.75f, 0.85f, 0.9f, 0.45f));
        Sfx.At("smash", p, 4f, R(0.4f, 0.5f), 60);
        Sfx.At("glass", p, 0f, R(0.85f, 1f), 60);

        // Incendie à l'étage touché (nombre limité par mission), fumée du toit au dernier palier.
        if (hole && _fires < MaxFires)
        {
            var night = w.Mission.Time == TimeOfDay.Night;
            var light = night && _fireLights < 3;
            if (light) _fireLights++;
            var fire = RuinFire.Create(Vector3.Zero, stage == 2 ? 0.9f : 1.2f, light, night, Id + stage);
            fire.SetMeta("light", light);
            AddChild(fire);
            fire.GlobalPosition = p + n * 1.1f - Vector3.Up * 1.3f; // au bord bas du trou : la cavité reste visible au-dessus
            _stageFx.Add(fire);
            _fires++;
        }
        if (stage == 3)
        {
            var smoke = RuinFire.SmokeColumn(new Vector3((H(7, 1) - 0.5f) * Size.X * 0.5f, Size.Y + 0.5f, (H(8, 1) - 0.5f) * Size.Z * 0.5f), 2.4f, true);
            AddChild(smoke);
            _stageFx.Add(smoke);
        }
    }

    /// <summary>Appelé sur tous les pairs quand l'hôte annonce la destruction.</summary>
    public void Collapse(World world)
    {
        if (Destroyed) return;
        Destroyed = true;
        CollisionLayer = 0;
        foreach (var c in GetChildren())
            if (c is CollisionShape3D cs) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        world.FxRoot.AddChild(new BuildingCollapse(this, world));
    }

    /// <summary>Libère les incendies des paliers (fin d'effondrement).</summary>
    internal void ClearStageFx()
    {
        if (_stageFx == null) return;
        foreach (var n in _stageFx)
        {
            if (n is RuinFire f)
            {
                _fires--;
                if (f.HasMeta("light") && f.GetMeta("light").AsBool()) _fireLights--;
            }
            if (IsInstanceValid(n)) n.QueueFree();
        }
        _stageFx = null;
    }

    /// <summary>Remise à zéro des compteurs partagés (nouvelle mission).</summary>
    internal static void ResetFires() { _fires = 0; _fireLights = 0; }

    /// <summary>Textures de dégâts générées au chargement (pas d'à-coup au premier impact).</summary>
    internal static Material[] ScarMaterials => new Material[] { HoleMat, CrackMat };

    internal static void WarmUp()
    {
        _ = HoleMat;
        _ = CrackMat;
        if (Screenshot.CmdArg("dump-scars") is { } dir) { HoleTex.GetImage().SavePng($"{dir}/hole.png"); CrackTex.GetImage().SavePng($"{dir}/crack.png"); }
    }

    internal Color RubbleTint => Tint(Id) * new Color(0.47f, 0.455f, 0.43f);

    // ------------------------------------------------------------------ Textures de dégâts (générées une fois)

    private static Texture2D HoleTex => _holeTex ??= MakeDamageTex(true);
    private static Texture2D CrackTex => _crackTex ??= MakeDamageTex(false);
    private static StandardMaterial3D _holeMat, _crackMat;
    private static StandardMaterial3D HoleMat => _holeMat ??= ScarMat(HoleTex);
    private static StandardMaterial3D CrackMat => _crackMat ??= ScarMat(CrackTex);

    private static StandardMaterial3D ScarMat(Texture2D tex) => new()
    {
        AlbedoTexture = tex,
        // Tramage (pipeline opaque) : les plaques alpha mélangées disparaissaient derrière le calque de dégâts des façades.
        Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash,
        AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverage,
        Roughness = 1f,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>
    /// Trou d'obus (cavité sombre, bord de béton éclaté, auréole de suie) ou fissures (réseau de fissures, enduit
    /// tombé par plaques), avec transparence ; bord du carré entièrement transparent.
    /// </summary>
    private static Texture2D MakeDamageTex(bool hole)
    {
        const int n = 256;
        var edge = new FastNoiseLite { Seed = hole ? 3 : 8, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.02f, FractalOctaves = 4 };
        var grain = new FastNoiseLite { Seed = 7, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.15f, FractalOctaves = 2 };
        // Fissures : lignes brisées qui rayonnent depuis le centre et se ramifient, plus fines vers l'extérieur.
        var crackMask = new float[n * n];
        var rng = new RandomNumberGenerator { Seed = hole ? 12u : 21u };
        void Crack(Vector2 p, float ang, float width, int segs, int depth)
        {
            for (var s = 0; s < segs; s++)
            {
                var len = rng.RandfRange(5f, 11f);
                ang += rng.RandfRange(-0.55f, 0.55f);
                var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
                for (var t = 0f; t <= 1f; t += 0.5f / len)
                {
                    var c = p.Lerp(q, t);
                    for (var oy = -2; oy <= 2; oy++)
                    for (var ox = -2; ox <= 2; ox++)
                    {
                        int px = (int)c.X + ox, py = (int)c.Y + oy;
                        if (px < 0 || py < 0 || px >= n || py >= n) continue;
                        var d = new Vector2(px + 0.5f, py + 0.5f).DistanceTo(c);
                        var v = Mathf.Clamp(width - d + 0.5f, 0f, 1f);
                        crackMask[py * n + px] = Mathf.Max(crackMask[py * n + px], v);
                    }
                }
                p = q;
                width = Mathf.Max(0.45f, width * 0.88f);
                if (depth < 2 && rng.Randf() < 0.22f) Crack(p, ang + rng.RandfRange(0.5f, 1.1f) * (rng.Randf() < 0.5f ? -1 : 1), width * 0.7f, segs - s - 1, depth + 1);
            }
        }
        var main = hole ? 9 : 7;
        for (var i = 0; i < main; i++)
        {
            var a0 = (i + rng.RandfRange(-0.3f, 0.3f)) / main * Mathf.Tau;
            var start = new Vector2(n / 2f, n / 2f) + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (hole ? n * 0.17f : rng.RandfRange(0f, n * 0.08f));
            Crack(start, a0, hole ? 2.6f : 2.4f, rng.RandiRange(7, 12), 0);
        }
        var data = new byte[n * n * 4];
        for (var y = 0; y < n; y++)
        for (var x = 0; x < n; x++)
        {
            var dx = (x + 0.5f) / n * 2f - 1f;
            var dy = (y + 0.5f) / n * 2f - 1f;
            var r = Mathf.Sqrt(dx * dx + dy * dy);
            var rn = r + edge.GetNoise2D(x, y) * 0.2f;
            var g = 0.5f + 0.5f * grain.GetNoise2D(x, y);
            var crackLine = crackMask[y * n + x];
            Color c;
            float a;
            if (hole)
            {
                if (rn < 0.3f)
                {
                    // Cavité : plancher et gravats sombres, plus clair vers le bord.
                    var k = Mathf.SmoothStep(0.05f, 0.3f, rn);
                    c = new Color(0.035f, 0.032f, 0.03f).Lerp(new Color(0.14f, 0.13f, 0.12f), k * (0.6f + 0.4f * g));
                    a = 1f;
                }
                else if (rn < 0.47f)
                {
                    // Béton éclaté autour du trou.
                    var v = 0.38f + 0.2f * g;
                    c = new Color(v, v * 0.97f, v * 0.92f);
                    a = 1f;
                }
                else
                {
                    // Auréole de suie et fissures rayonnantes.
                    var soot = (1f - Mathf.SmoothStep(0.47f, 0.95f, rn)) * 0.75f;
                    var cr = crackLine * (1f - Mathf.SmoothStep(0.55f, 0.98f, r));
                    a = Mathf.Max(soot, cr * 0.9f);
                    c = new Color(0.05f, 0.045f, 0.04f);
                }
            }
            else
            {
                var fade = 1f - Mathf.SmoothStep(0.55f, 0.97f, r);
                // Enduit tombé par plaques (béton plus clair dessous), fissures sombres, voile de poussière.
                var spall = Mathf.SmoothStep(0.36f, 0.5f, edge.GetNoise2D(x * 1.6f + 500, y * 1.6f) + 0.3f - r * 0.7f);
                var cr = crackLine * fade;
                if (cr > 0.25f) { c = new Color(0.04f, 0.035f, 0.03f); a = cr; }
                else if (spall > 0.1f) { var v = 0.42f + 0.18f * g; c = new Color(v, v * 0.97f, v * 0.93f); a = spall * fade; }
                else { c = new Color(0.12f, 0.11f, 0.1f); a = 0.25f * fade * g; }
            }
            if (r > 0.98f) a = 0f;
            var i = (y * n + x) * 4;
            data[i] = (byte)(c.R * 255f);
            data[i + 1] = (byte)(c.G * 255f);
            data[i + 2] = (byte)(c.B * 255f);
            data[i + 3] = (byte)(Mathf.Clamp(a, 0f, 1f) * 255f);
        }
        var img = Image.CreateFromData(n, n, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}

/// <summary>
/// Effondrement d'un immeuble (nœud temporaire, tous les pairs) : grondement, enfoncement incliné, poussière et
/// éclats, nuage qui roule dans les rues, tas de gravats solide.
/// </summary>
public partial class BuildingCollapse : Node
{
    private const float RumbleT = 0.8f, FrontT = 3.6f;
    private static int _active;

    private readonly Building _b;
    private readonly World _w;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<(Node3D n, Vector3 p)> _sinking = new();
    private Vector3 _base, _tiltAxis;
    private float _t, _emitT, _pieceT, _depth, _sinkDur, _tiltMax, _rubbleH, _q, _frontT = -1f;
    private int _pieces;
    private bool _sinkStarted, _done;
    private StaticBody3D _rubble;
    private ExplosionFx _fx;

    public BuildingCollapse() { }

    public BuildingCollapse(Building b, World w)
    {
        _b = b;
        _w = w;
        Name = $"Collapse{b.Id}";
        _rng.Seed = (ulong)(b.Id * 977 + 1);
    }

    private float R(float a, float b) => _rng.RandfRange(a, b);

    public override void _Ready()
    {
        if (_b == null) { QueueFree(); return; }
        _active++;
        _fx = ExplosionFx.For(_w);
        var s = _b.Size;
        _base = _b.Position;
        foreach (var c in _b.GetChildren())
            if (c is Node3D n) _sinking.Add((n, n.Position));
        _depth = s.Y + 2.5f;
        _sinkDur = Mathf.Clamp(2.4f + s.Y / 16f, 2.8f, 5.5f);
        var a = RuinKit.Hash(_b.Id, 1, 44) * Mathf.Tau;
        _tiltAxis = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        _tiltMax = Mathf.DegToRad(2.5f + RuinKit.Hash(_b.Id, 2, 44) * 5f) * Mathf.Clamp(s.Y / 20f, 0.4f, 1f);
        var dist = _fx.CameraDistance(_base + Vector3.Up * s.Y * 0.5f);
        _q = Mathf.Clamp(1f - (dist - 90f) / 320f, 0.3f, 1f) / Mathf.Max(1f, _active * 0.75f);
        BuildRubble();
        Sfx.At("rumble", _base + Vector3.Up * s.Y * 0.3f, 6f, 1f, 0);
        _w.ShakeCamera(_base, 0.35f, 90f);
        // Les vitres et l'enduit des étages bas cèdent d'abord.
        Burst(0.35f, 10);
    }

    public override void _ExitTree() => _active = Mathf.Max(0, _active - 1);

    private int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * _q));

    /// <summary>Tas de gravats (plusieurs buttes qui se chevauchent), sous le sol jusqu'à la fin de l'enfoncement.</summary>
    private void BuildRubble()
    {
        var s = _b.Size;
        var small = Mathf.Min(s.X, s.Z) < 4f; // pile de conteneurs, cabanon
        _rubbleH = small ? Mathf.Clamp(s.Y * 0.2f, 0.6f, 1.6f) : Mathf.Clamp(s.Y * 0.15f + 1.4f, 2f, 7f);
        var kit = new MeshKit();
        _rubble = new StaticBody3D { Name = $"Rubble{_b.Id}", CollisionLayer = 0, CollisionMask = 0 };
        var tint = _b.RubbleTint;
        var piles = small ? 1 : 4;
        for (var i = 0; i < piles; i++)
        {
            var off = piles == 1 ? Vector3.Zero : new Vector3((i % 2 - 0.5f) * s.X * 0.4f, 0, (i / 2 - 0.5f) * s.Z * 0.4f);
            var ps = piles == 1
                ? new Vector3(s.X * 1.05f, _rubbleH, s.Z * 1.3f)
                : new Vector3(s.X * (0.62f + 0.12f * RuinKit.Hash(_b.Id, i, 51)), _rubbleH * (0.75f + 0.35f * RuinKit.Hash(_b.Id, i, 52)), s.Z * (0.62f + 0.12f * RuinKit.Hash(_b.Id, i, 53)));
            var pts = Props.RubblePile(kit, off, i * 0.9f + _b.Id, ps, _b.Id * 5 + i, tint, !small && RuinKit.Hash(_b.Id, 3, 54) < 0.4f);
            _rubble.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = pts } });
        }
        _rubble.AddChild(new MeshInstance3D { Mesh = kit.Commit() });
        _b.GetParent().AddChild(_rubble);
        _rubble.Position = _base + Vector3.Down * (_rubbleH + 0.6f);
        _rubble.Visible = false;
    }

    /// <summary>Façades qui se vident : bouffées de poussière et de verre aux fenêtres, sur une bande de hauteur.</summary>
    private void Burst(float maxFrac, int n)
    {
        var s = _b.Size;
        var dust = new Color(0.36f, 0.34f, 0.31f, 0.55f);
        for (var i = 0; i < N(n); i++)
        {
            var side = _rng.RandiRange(0, 3);
            var nrm = side switch { 0 => Vector3.Right, 1 => Vector3.Left, 2 => Vector3.Back, _ => Vector3.Forward };
            var along = new Vector3(nrm.Z, 0, -nrm.X);
            var half = side < 2 ? s.Z / 2 : s.X / 2;
            var p = _b.GlobalPosition + nrm * ((side < 2 ? s.X : s.Z) / 2 + 0.4f) + along * R(-half, half) + Vector3.Up * R(1f, s.Y * maxFrac + 1f);
            _fx.DustPuff(p, nrm * R(2f, 5f) + Vector3.Up * R(-0.5f, 1f), R(2f, 3.5f), R(3f, 5f), dust);
            for (var k = 0; k < 4; k++)
                _fx.Spark(p, nrm * R(1f, 4f) + new Vector3(R(-1, 1), R(-1, 2), R(-1, 1)), R(0.02f, 0.04f), R(0.8f, 1.5f), new Color(0.75f, 0.85f, 0.9f, 0.45f));
        }
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        var dt = (float)delta;
        _t += dt;
        var s = _b.Size;
        var hx = s.X / 2;
        var hz = s.Z / 2;
        if (_t < RumbleT)
        {
            var k = _t / RumbleT;
            _b.Position = _base + new Vector3(R(-1, 1), 0, R(-1, 1)) * 0.07f * k;
            _emitT -= dt;
            if (_emitT <= 0) { _emitT = 0.15f; Burst(0.6f, 3); }
            return;
        }
        var st = _t - RumbleT;
        if (!_sinkStarted)
        {
            _sinkStarted = true;
            Sfx.At("collapse", _base + Vector3.Up * s.Y * 0.3f, 6f, 1f, 0);
            _w.ShakeCamera(_base, 0.9f, 90f);
        }
        if (st <= _sinkDur)
        {
            var u = st / _sinkDur;
            var y = -_depth * Mathf.Pow(u, 1.7f);
            var jitter = new Vector3(R(-1, 1), 0, R(-1, 1)) * 0.06f * (1f - u);
            _b.Position = _base + Vector3.Up * y + jitter;
            _b.Basis = new Basis(_tiltAxis, _tiltMax * u);
            var top = _base.Y + s.Y + y;
            _emitT -= dt;
            if (_emitT <= 0)
            {
                _emitT = 0.08f;
                EmitSinking(hx, hz, top, u);
            }
            // Gros blocs projetés depuis les arêtes du toit (corps rigides, près de la caméra seulement).
            _pieceT -= dt;
            if (_pieceT <= 0 && top > _base.Y + 1f && _pieces < 18 && _fx.CameraDistance(_base) < 110f)
            {
                _pieceT = 0.22f;
                _pieces++;
                var side = _rng.RandiRange(0, 3);
                var nrm = side switch { 0 => Vector3.Right, 1 => Vector3.Left, 2 => Vector3.Back, _ => Vector3.Forward };
                var along = new Vector3(nrm.Z, 0, -nrm.X);
                var half = side < 2 ? hz : hx;
                var p = _base + nrm * ((side < 2 ? hx : hz) + 0.5f) + along * R(-half, half) + Vector3.Up * (top - _base.Y - R(0.5f, 2f));
                var size = new Vector3(R(0.6f, 1.6f), R(0.4f, 1f), R(0.5f, 1.4f));
                DebrisPool.For(_w).Spawn(new Transform3D(new Basis(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)).Normalized() + Vector3.Up * 0.01f, R(0, 3)), p), size,
                    nrm * R(2f, 6f) + Vector3.Up * R(0f, 2f), new Vector3(R(-3, 3), R(-3, 3), R(-3, 3)), _b.RubbleTint * R(0.8f, 1.15f), R(10f, 14f));
            }
            // Le tas monte dans la poussière pendant la seconde moitié.
            if (u > 0.5f)
            {
                _rubble.Visible = true;
                var k = Mathf.SmoothStep(0.5f, 1f, u);
                _rubble.Position = _base + Vector3.Down * ((_rubbleH + 0.6f) * (1f - k));
            }
            return;
        }
        if (_frontT < 0)
        {
            // Fin de l'enfoncement : l'immeuble disparaît, les gravats deviennent solides, le nuage part.
            _frontT = 0;
            _b.Visible = false;
            _b.Position = _base;
            _b.Basis = Basis.Identity;
            _b.ClearStageFx();
            foreach (var (n, p) in _sinking) if (IsInstanceValid(n)) n.Position = p;
            _rubble.Position = _base;
            _rubble.CollisionLayer = 1;
            _w.ShakeCamera(_base, 0.5f, 70f);
            // Colonne de poussière au centre.
            for (var i = 0; i < N(14); i++)
                _fx.DustPuff(_base + new Vector3(R(-hx, hx) * 0.7f, R(1f, 4f), R(-hz, hz) * 0.7f), new Vector3(R(-1, 1), R(2f, 6f), R(-1, 1)), R(6f, 10f), R(9f, 14f),
                    new Color(0.37f, 0.35f, 0.32f, 0.5f));
        }
        _frontT += dt;
        _emitT -= dt;
        if (_emitT <= 0 && _frontT < FrontT)
        {
            _emitT = 0.12f;
            EmitFront(hx, hz);
        }
        if (_frontT >= FrontT)
        {
            _done = true;
            // Les gravats couvent un moment.
            var smoke = RuinFire.SmokeColumn(Vector3.Up * (_rubbleH * 0.6f), Mathf.Clamp(Mathf.Max(s.X, s.Z) / 14f, 0.8f, 2.4f), true);
            _rubble.AddChild(smoke);
            var timer = _w.GetTree().CreateTimer(80f, false);
            timer.Timeout += () => { if (IsInstanceValid(smoke)) smoke.QueueFree(); };
            QueueFree();
        }
    }

    /// <summary>Pendant l'enfoncement : poussière chassée au pied (tout le périmètre), éclats qui tombent du sommet, fumée claire au-dessus.</summary>
    private void EmitSinking(float hx, float hz, float top, float u)
    {
        var dust = new Color(0.36f, 0.34f, 0.31f, 0.55f);
        var per = 2f * (hx + hz);
        var n = N(Mathf.Clamp(per / 9f, 4f, 12f));
        for (var i = 0; i < n; i++)
        {
            var (p, nrm) = PerimeterPoint(hx, hz, 0.8f);
            _fx.DustPuff(p + Vector3.Up * R(0.5f, 2.5f), nrm * R(5f, 11f) + Vector3.Up * R(0.5f, 2.5f), R(3.5f, 6f), R(7f, 11f), dust * R(0.9f, 1.1f));
        }
        if (top > _base.Y + 0.5f)
        {
            for (var i = 0; i < N(6); i++)
            {
                var (p, nrm) = PerimeterPoint(hx, hz, 0.3f);
                p.Y = top - R(0f, 1.5f);
                _fx.Chunk(p, nrm * R(1f, 5f) + Vector3.Up * R(-1f, 2f), R(0.15f, 0.5f), R(3f, 5f), _b.RubbleTint * R(0.7f, 1.1f));
            }
            for (var i = 0; i < N(2); i++)
                _fx.SmokePuff(_base + new Vector3(R(-hx, hx), top - _base.Y + 1f, R(-hz, hz)), new Vector3(R(-1, 1), R(2f, 5f), R(-1, 1)), R(4f, 7f), R(8f, 12f),
                    new Color(0.4f, 0.38f, 0.35f, 0.45f));
        }
    }

    /// <summary>Nuage qui roule : anneau de poussière au ras du sol dont le rayon croît (le front avance dans les rues).</summary>
    private void EmitFront(float hx, float hz)
    {
        var r0 = Mathf.Max(hx, hz);
        var r = r0 + 4f + _frontT * 9f;
        var n = N(Mathf.Clamp(r / 3.5f, 8f, 22f));
        var fade = 1f - _frontT / FrontT * 0.5f;
        for (var i = 0; i < n; i++)
        {
            var a = (i + R(-0.4f, 0.4f)) / n * Mathf.Tau;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var p = new Vector3(_base.X, _base.Y, _base.Z) + d * r * R(0.85f, 1.05f) + Vector3.Up * R(0.8f, 2.5f);
            _fx.DustPuff(p, d * R(2.5f, 4.5f) + Vector3.Up * R(0.1f, 0.8f), R(5f, 8.5f) * fade, R(9f, 14f), new Color(0.37f, 0.35f, 0.32f, 0.42f * fade + 0.1f));
        }
    }

    private (Vector3 p, Vector3 n) PerimeterPoint(float hx, float hz, float out_)
    {
        var side = _rng.RandiRange(0, 3);
        var nrm = side switch { 0 => Vector3.Right, 1 => Vector3.Left, 2 => Vector3.Back, _ => Vector3.Forward };
        var along = new Vector3(nrm.Z, 0, -nrm.X);
        var half = side < 2 ? hz : hx;
        return (_base + nrm * ((side < 2 ? hx : hz) + out_) + along * R(-half, half), nrm);
    }
}
