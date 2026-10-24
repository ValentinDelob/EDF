using Godot;

namespace Rempart;

/// <summary>Caisse de butin au sol.</summary>
public partial class Crate : Node3D
{
    public int Id;
    public CrateType Type;
    public bool Taken;
    private float _t;
    private Node3D _body;
    private Vector3 _vel;
    private bool _landed;

    public static Crate Create(int id, CrateType type, Vector3 pos)
    {
        var c = new Crate { Id = id, Type = type, Name = $"Crate{id}" };
        c._body = new Node3D();
        c.AddChild(c._body);
        var color = Crates.Tint(type);
        var shellMat = Mats.Painted(color.Darkened(0.25f), 0.45f, 0.3f);
        var size = type == CrateType.Weapon ? new Vector3(1.1f, 0.6f, 0.6f) : new Vector3(0.65f, 0.65f, 0.65f);
        // Forme propre à chaque type (lisible sans les couleurs) : arme allongée, armure cubique, soin cylindrique.
        Mesh shell = type == CrateType.Health
            ? new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.6f, RadialSegments = 16, Material = shellMat }
            : new BoxMesh { Size = size, Material = shellMat };
        c._body.AddChild(new MeshInstance3D { Mesh = shell });
        // Bandeau lumineux pour repérer la caisse de loin.
        c._body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(size.X + 0.04f, 0.08f, size.Z + 0.04f), Material = Mats.Glow(color, 2.5f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // Halo vertical
        c._body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.25f, Height = 3f, Material = BeamMat(color) },
            Position = new Vector3(0, 1.6f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        c.Position = pos + Vector3.Up * 1.0f;
        var rng = new RandomNumberGenerator();
        c._vel = new Vector3(rng.RandfRange(-3, 3), rng.RandfRange(5, 8), rng.RandfRange(-3, 3));
        return c;
    }

    private static StandardMaterial3D BeamMat(Color c) => new()
    {
        AlbedoColor = new Color(c, 0.25f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>Glisse vers le joueur (effet d'aimant local).</summary>
    public void Attract(Vector3 target, float dt)
    {
        if (!_landed) return;
        var to = target + Vector3.Up * 0.45f - Position;
        to.Y = 0;
        var d = to.Length();
        if (d < 0.1f) return;
        var np = Position + to / d * Mathf.Min(d, (14f - d) * dt);
        // Glisse en suivant le relief (la caisse est toujours posée sur le terrain) ; sur une carte plate, hauteur inchangée.
        if (World.Current?.Terrain != null) np.Y = World.GroundAt(np.X, np.Z) + 0.45f;
        Position = np;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        if (!_landed)
        {
            _vel += Vector3.Down * 20f * dt;
            var p = Position + _vel * dt;
            // Sol de la carte (relief de la vallée) ; 0 sur les cartes plates.
            var floor = World.GroundAt(p.X, p.Z) + 0.45f;
            if (p.Y <= floor) { p.Y = floor; _landed = true; }
            Position = p;
        }
        _body.Rotation = new Vector3(0, _t * 1.4f, 0);
        _body.Position = new Vector3(0, _landed ? Mathf.Sin(_t * 3f) * 0.08f : 0, 0);
    }
}
