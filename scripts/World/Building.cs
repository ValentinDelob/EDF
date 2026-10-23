using Godot;

namespace Rempart;

/// <summary>Immeuble destructible : s'effondre (enfoncement + poussière + débris) à 0 PV.</summary>
public partial class Building : StaticBody3D
{
    public int Id;
    public float Health;
    public float MaxHealth;
    public Vector3 Size;
    public bool Destroyed;
    public MeshInstance3D Visual;
    public Material FacadeMat;

    public static Building Create(int id, Vector3 center, Vector3 size, Material facade, Material roof)
    {
        var b = new Building
        {
            Id = id,
            Size = size,
            Name = $"Building{id}",
            CollisionLayer = 1,
            CollisionMask = 0,
            FacadeMat = facade,
        };
        b.Position = center;
        var floors = Mathf.Max(1, size.Y / 3.2f);
        b.MaxHealth = b.Health = 350 + floors * 140 + size.X * size.Z * 0.6f;

        b.Visual = new MeshInstance3D { Mesh = BuildMesh(size, facade, roof) };
        b.AddChild(b.Visual);
        b.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });

        // Petits détails de toit (climatiseurs, cage d'escalier).
        var rng = new RandomNumberGenerator { Seed = (ulong)(id * 7919 + 3) };
        var n = rng.RandiRange(1, 3);
        for (var i = 0; i < n; i++)
        {
            var s = new Vector3(rng.RandfRange(1.5f, 4f), rng.RandfRange(1f, 3f), rng.RandfRange(1.5f, 4f));
            var p = new Vector3(rng.RandfRange(-size.X / 2 + 3, size.X / 2 - 3), size.Y + s.Y / 2, rng.RandfRange(-size.Z / 2 + 3, size.Z / 2 - 3));
            b.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = s, Material = Mats.Concrete(new Color(0.5f, 0.5f, 0.5f), 5) }, Position = p });
        }
        return b;
    }

    /// <summary>Boîte dont les UV des façades sont en mètres (1 U = 7 m, 1 V = 3,2 m = un étage).</summary>
    private static ArrayMesh BuildMesh(Vector3 s, Material facade, Material roof)
    {
        var st = new SurfaceTool();
        st.Begin(Godot.Mesh.PrimitiveType.Triangles);
        st.SetMaterial(facade);
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float w)
        {
            var u = w / 7f;
            var v = h / 3.2f;
            st.SetNormal(n);
            // Sens horaire vu de l'extérieur (convention de Godot pour les faces avant).
            st.SetUV(new Vector2(0, v)); st.AddVertex(a);
            st.SetUV(new Vector2(u, 0)); st.AddVertex(c);
            st.SetUV(new Vector2(u, v)); st.AddVertex(b);
            st.SetUV(new Vector2(0, v)); st.AddVertex(a);
            st.SetUV(new Vector2(0, 0)); st.AddVertex(d);
            st.SetUV(new Vector2(u, 0)); st.AddVertex(c);
        }
        // Godot : faces avant dans le sens horaire vu de l'extérieur.
        Quad(new(-hx, 0, hz), new(hx, 0, hz), new(hx, h, hz), new(-hx, h, hz), Vector3.Back, s.X);
        Quad(new(hx, 0, -hz), new(-hx, 0, -hz), new(-hx, h, -hz), new(hx, h, -hz), Vector3.Forward, s.X);
        Quad(new(hx, 0, hz), new(hx, 0, -hz), new(hx, h, -hz), new(hx, h, hz), Vector3.Right, s.Z);
        Quad(new(-hx, 0, -hz), new(-hx, 0, hz), new(-hx, h, hz), new(-hx, h, -hz), Vector3.Left, s.Z);
        st.Index();
        st.GenerateTangents();
        var mesh = st.Commit();

        var top = new SurfaceTool();
        top.Begin(Godot.Mesh.PrimitiveType.Triangles);
        top.SetMaterial(roof);
        top.SetNormal(Vector3.Up);
        Vector3[] v4 = { new(-hx, h, -hz), new(hx, h, -hz), new(hx, h, hz), new(-hx, h, hz) };
        int[] idx = { 0, 1, 2, 0, 2, 3 };
        foreach (var i in idx)
        {
            top.SetUV(new Vector2(v4[i].X, v4[i].Z) * 0.2f);
            top.AddVertex(v4[i]);
        }
        top.GenerateTangents();
        top.Commit(mesh);
        return mesh;
    }

    /// <summary>Appelé sur tous les pairs quand l'hôte annonce la destruction.</summary>
    public void Collapse(World world)
    {
        if (Destroyed) return;
        Destroyed = true;
        CollisionLayer = 0;
        foreach (var c in GetChildren())
            if (c is CollisionShape3D cs) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        var pos = GlobalPosition;
        Sfx.At("collapse", pos + Vector3.Up * Size.Y * 0.3f, 6f, 1f, 0);
        Effects.Dust(world, pos, Size);
        world.ShakeCamera(pos, 0.9f, 80f);

        var tw = CreateTween();
        tw.SetParallel();
        foreach (var c in GetChildren())
            if (c is Node3D n)
                tw.TweenProperty(n, "position:y", n.Position.Y - Size.Y - 2f, 3.2f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(this, "rotation:z", (float)GD.RandRange(-0.06, 0.06), 3.2f);
        tw.Chain().TweenCallback(Callable.From(() =>
        {
            foreach (var c in GetChildren())
                if (c is MeshInstance3D m) m.Visible = false;
            Rotation = Vector3.Zero;
            // Tas de gravats.
            var rubble = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = Mathf.Min(Size.X, Size.Z) * 0.25f,
                    BottomRadius = Mathf.Max(Size.X, Size.Z) * 0.55f,
                    Height = Mathf.Min(3.5f, Size.Y * 0.15f),
                    RadialSegments = 9,
                    Rings = 2,
                    Material = Mats.Concrete(new Color(0.45f, 0.43f, 0.4f), 9),
                },
                Position = new Vector3(0, Mathf.Min(3.5f, Size.Y * 0.15f) / 2 - 0.3f, 0),
            };
            AddChild(rubble);
        }));
        Effects.Debris(world, pos, Size, FacadeMat);
    }
}
