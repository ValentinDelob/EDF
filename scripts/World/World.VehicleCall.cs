using Godot;

namespace Rempart;

/// <summary>
/// Appel de véhicule (Opérateur d'appui, spec §5.4) : l'hôte valide la demande, cherche un point de pose dégagé près
/// du point visé et diffuse la livraison ; chaque pair joue le largage (<see cref="VehicleDrop"/>). Un véhicule appelé
/// se comporte ensuite exactement comme un véhicule garé (tout le monde peut monter à bord, réarmement au point de pose).
/// Un seul véhicule appelé actif par Opérateur : un nouvel appel rapatrie l'ancien s'il est libre (refusé s'il est occupé).
/// </summary>
public partial class World
{
    /// <summary>Identifiants des véhicules appelés (hôte) : à partir de 100, loin des véhicules garés (1, 2).</summary>
    private int _nextCalledVehicleId = 100;

    /// <summary>Hôte : traite une demande d'appel. Faux si elle est refusée (véhicule précédent occupé, aucun point dégagé).</summary>
    public bool HostVehicleCall(long owner, int type, Vector3 at)
    {
        if (Ended || type < 0 || type >= CallIns.Count || !AirSupport) return false;
        Vector3 ownerPos;
        if (owner == Net.I.MyId && LocalPlayer != null) ownerPos = LocalPlayer.GlobalPosition;
        else if (Remotes.TryGetValue(owner, out var r)) ownerPos = r.GlobalPosition;
        else return false;

        var replaced = 0;
        foreach (var v in Vehicles.Values)
        {
            if (v.CalledBy != owner || v.Wrecked) continue;
            if (v.Driver != 0 || v.Delivering) return false;
            replaced = v.Id;
        }
        if (!FindDropSpot(at, ownerPos, type, out var pos)) return false;
        var dir = pos - ownerPos;
        dir.Y = 0;
        var yaw = dir.LengthSquared() > 0.01f ? Mathf.Atan2(dir.X, dir.Z) : 0f;
        var id = _nextCalledVehicleId++;
        GD.Print($"[appel] {CallIns.Names[type]} n°{id} pour le joueur {owner} : pose {pos.Snapped(Vector3.One * 0.1f)}, demandé {at.Snapped(Vector3.One)}" + (replaced != 0 ? $", remplace n°{replaced}" : ""));
        Net.I.BroadcastVehicleCalled(id, type, pos, yaw, owner, replaced);
        return true;
    }

    /// <summary>
    /// Point de pose : le point visé ramené à <see cref="CallIns.Range"/> m du soldat (et à 9 m au moins), puis des couronnes
    /// autour de lui et enfin autour du soldat. Il faut le sol nu (pas un toit ni un immeuble), une colonne d'air libre au-dessus
    /// et une emprise dégagée (mobilier, épaves, véhicules) à la taille du véhicule.
    /// </summary>
    private bool FindDropSpot(Vector3 at, Vector3 ownerPos, int type, out Vector3 pos)
    {
        pos = default;
        var d = at - ownerPos;
        d.Y = 0;
        var len = d.Length();
        var dir = len > 0.1f ? d / len : Vector3.Forward;
        var target = ownerPos + dir * Mathf.Clamp(len, 9f, CallIns.Range);
        var space = GetWorld3D().DirectSpaceState;
        var box = new BoxShape3D { Size = type == CallIns.Heli ? new Vector3(10f, 3f, 11f) : new Vector3(5f, 2.6f, 8f) };
        var query = new PhysicsShapeQueryParameters3D { Shape = box, CollisionMask = 1 };
        bool Try(Vector3 c, out Vector3 p)
        {
            p = default;
            if (Mathf.Abs(c.X) > CityBuilder.Half - 12f || Mathf.Abs(c.Z) > CityBuilder.Half - 12f || Flow.IsBlocked(c)) return false;
            if (new Vector2(c.X - ownerPos.X, c.Z - ownerPos.Z).Length() < 7f) return false; // jamais sur la tête du demandeur
            var ray = PhysicsRayQueryParameters3D.Create(new Vector3(c.X, 160f, c.Z), new Vector3(c.X, -20f, c.Z), 1);
            var hit = space.IntersectRay(ray);
            if (hit.Count == 0 || hit["collider"].AsGodotObject() is Building) return false;
            var ground = ((Vector3)hit["position"]).Y;
            if (ground > 3f || ground < -2f) return false;
            query.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(dir.X, dir.Z)), new Vector3(c.X, ground + 0.9f + box.Size.Y / 2f, c.Z));
            if (space.IntersectShape(query, 1).Count > 0) return false;
            foreach (var v in Vehicles.Values)
                if (new Vector2(v.GlobalPosition.X - c.X, v.GlobalPosition.Z - c.Z).Length() < 9f) return false;
            p = new Vector3(c.X, ground + 0.3f, c.Z);
            return true;
        }
        if (Try(target, out pos)) return true;
        foreach (var center in new[] { target, ownerPos })
            for (var ring = 1; ring <= 4; ring++)
                for (var k = 0; k < 10; k++)
                {
                    var a = k * Mathf.Tau / 10f + ring * 0.37f;
                    if (Try(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring * 6.5f, out pos)) return true;
                }
        return false;
    }

    /// <summary>Tous les pairs : un véhicule appelé arrive (largage), l'éventuel véhicule remplacé est rapatrié.</summary>
    public void OnVehicleCalled(int id, int type, Vector3 pos, float yaw, long owner, int replaced)
    {
        if (Vehicles.ContainsKey(id)) return;
        if (replaced != 0 && Vehicles.Remove(replaced, out var old))
        {
            // Rapatrié : enlevé dans un nuage de fumée (personne à bord, garanti par l'hôte).
            if (old.IsLocalDriver) LocalPlayer?.ExitVehicle();
            Effects.Smoke(this, old.GlobalPosition + Vector3.Up, 3f, 3f);
            old.QueueFree();
        }
        Vehicle v = type == CallIns.Heli ? Helicopter.Create(this, id, pos, yaw) : Tank.Create(this, id, pos, yaw);
        v.CalledBy = owner;
        v.Delivering = true;
        AddVehicle(v);
        var drop = new VehicleDrop();
        AddChild(drop);
        drop.Begin(this, v, pos, yaw, type);
        if (owner == Net.I.MyId) LocalPlayer?.OnVehicleCallConfirmed(id, type);
        if (VehicleCallLog) GD.Print($"[appel] largage n°{id} ({CallIns.Names[type]}) vu par {Net.I.MyId}");
    }

    /// <summary>L'hôte a refusé l'appel (véhicule précédent occupé ou aucun point dégagé).</summary>
    public void OnVehicleCallDenied()
    {
        Sfx.Ui("empty");
        GD.Print("[appel] refusé par l'hôte");
    }

    /// <summary>Test --call-vehicle : journal de la livraison chez chaque pair.</summary>
    public static bool VehicleCallLog;
}

/// <summary>
/// Largage d'un véhicule appelé, joué localement par chaque pair (durées identiques) : fumigène rouge au point de pose,
/// avion cargo qui passe au-dessus, chute libre puis trois voilures kaki, freinage par rétrofusées au ras du sol,
/// poussière et choc à l'atterrissage, voilures qui s'affaissent et dérivent. Environ 7 s au total.
/// </summary>
public partial class VehicleDrop : Node3D
{
    private const float PlaneLead = 1.6f;   // l'avion arrive à la verticale du point de pose
    private const float DropHeight = 55f;
    private const float Drift = 12f;        // le véhicule glisse dans l'axe de l'avion pendant la descente
    private const float PlaneSpeed = 85f;

    private World _world;
    private Vehicle _v;
    private Vector3 _land, _axis;
    private float _yaw, _t, _h, _vy;
    private bool _released, _landed, _retro;
    private Node3D _plane, _rig, _flare;
    private float _smokeT, _rigOpen;
    private int _type;

    public void Begin(World w, Vehicle v, Vector3 land, float yaw, int type)
    {
        _world = w;
        _v = v;
        _land = land;
        _yaw = yaw;
        _type = type;
        // Axe de passage de l'avion : perpendiculaire au cap du véhicule (il « sort » de la soute de côté).
        _axis = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        _h = DropHeight;
        v.Visible = false;
        v.GlobalPosition = land + Vector3.Up * DropHeight;

        // Fusée éclairante rouge au point de pose.
        _flare = new Node3D();
        AddChild(_flare);
        _flare.GlobalPosition = land + Vector3.Up * 0.1f;
        _flare.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 0.4f, Material = Mats.Glow(new Color(1f, 0.15f, 0.08f), 5f) }, Position = new Vector3(0, 0.2f, 0) });
        _flare.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.2f, 0.1f), LightEnergy = 2.5f, OmniRange = 9f, Position = new Vector3(0, 0.8f, 0), ShadowEnabled = false });
        Effects.Ring(w, land, type == CallIns.Heli ? 7f : 5.5f, new Color(1f, 0.3f, 0.15f), 6.5f);
        Sfx.At("radio", land, 0f);

        _plane = BuildPlane();
        AddChild(_plane);
        PlacePlane();
        Sfx.At("ship_engine", land + Vector3.Up * 70f, 4f, 0.55f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_world == null) return;
        _t += dt;
        PlacePlane();

        // Fumigène tant que le véhicule n'est pas posé.
        _smokeT -= dt;
        if (_smokeT <= 0 && !_landed)
        {
            _smokeT = 0.3f;
            Effects.SignalSmoke(_world, _land + Vector3.Up * 0.4f, new Color(0.85f, 0.12f, 0.08f), 2.4f, 4f);
        }

        if (!IsInstanceValid(_v) || _v.Wrecked)
        {
            Finish();
            return;
        }
        if (!_landed && _t >= PlaneLead) Descend(dt);
        else if (_landed) Collapse(dt);

        if (_t > PlaneLead + 14f) QueueFree();
    }

    private void Descend(float dt)
    {
        if (!_released)
        {
            _released = true;
            _v.Visible = true;
            _rig = BuildRig(_type);
            AddChild(_rig);
            _rig.Scale = Vector3.One * 0.05f;
            Sfx.At("hatch", _land + Vector3.Up * DropHeight, 4f, 0.8f);
        }
        var since = _t - PlaneLead;
        // Chute libre 0,7 s, ouverture des voilures, descente à 11 m/s, rétrofusées sous 6 m (3 m/s au contact).
        if (since < 0.7f) _vy = Mathf.MoveToward(_vy, 30f, 18f * dt);
        else if (_h > 6f) _vy = Mathf.Lerp(_vy, 11f, 1f - Mathf.Exp(-dt * 3f));
        else
        {
            if (!_retro)
            {
                _retro = true;
                Sfx.At("rocket", _v.GlobalPosition, 2f, 0.55f);
            }
            _vy = Mathf.Lerp(_vy, 3f, 1f - Mathf.Exp(-dt * 6f));
            Effects.Smoke(_world, _v.GlobalPosition + Vector3.Up * 0.3f, 1.6f, 1.2f);
        }
        if (since >= 0.7f) _rigOpen = Mathf.MoveToward(_rigOpen, 1f, dt * 1.8f);
        _h = Mathf.Max(0, _h - _vy * dt);
        var k = _h / DropHeight;
        var sway = new Vector3(Mathf.Sin(_t * 1.3f) * 0.05f * k, _yaw, Mathf.Cos(_t * 1.1f) * 0.05f * k);
        _v.GlobalPosition = _land + _axis * (Drift * k) + Vector3.Up * _h;
        _v.Rotation = sway;
        if (_rig != null)
        {
            var e = _rigOpen * _rigOpen * (3f - 2f * _rigOpen);
            _rig.Scale = Vector3.One * Mathf.Max(0.05f, e);
            _rig.GlobalPosition = _v.GlobalPosition + Vector3.Up * Top;
            _rig.Rotation = new Vector3(sway.X * 0.6f, _yaw, sway.Z * 0.6f);
        }
        if (_h <= 0f) Land();
    }

    private float Top => _type == CallIns.Heli ? 3.4f : 2.6f;

    private void Land()
    {
        _landed = true;
        _v.Rotation = new Vector3(0, _yaw, 0);
        _v.OnDelivered(_land);
        Effects.Dust(_world, _land, new Vector3(9f, 2f, 9f));
        Sfx.At("explosion_small", _land, 2f, 0.55f);
        Sfx.At("vehicle_crash", _land, -2f, 0.7f);
        _world.ShakeCamera(_land, 0.35f, 40f);
        if (IsInstanceValid(_flare)) _flare.QueueFree();
        if (World.VehicleCallLog) GD.Print($"[appel] véhicule n°{_v.Id} posé à {_land.Snapped(Vector3.One * 0.1f)} après {_t:0.0} s");
        _collapseT = 0;
    }

    private float _collapseT;

    /// <summary>Voilures larguées : elles s'affaissent, dérivent avec le vent et disparaissent.</summary>
    private void Collapse(float dt)
    {
        if (_rig == null || !IsInstanceValid(_rig)) return;
        _collapseT += dt;
        var c = Mathf.Clamp(_collapseT / 2.6f, 0, 1);
        _rig.GlobalPosition += (_axis * 2.2f + Vector3.Down * (1.5f + 3f * c)) * dt;
        _rig.Scale = new Vector3(1f + 0.25f * c, Mathf.Max(0.05f, 1f - 0.9f * c), 1f + 0.25f * c) * (c > 0.8f ? Mathf.Max(0.01f, (1f - c) * 5f) : 1f);
        if (c >= 1f)
        {
            _rig.QueueFree();
            _rig = null;
        }
    }

    private void Finish()
    {
        if (_rig != null && IsInstanceValid(_rig)) _rig.QueueFree();
        if (IsInstanceValid(_flare)) _flare.QueueFree();
        if (IsInstanceValid(_v) && _v.Delivering) _v.OnDelivered(_land);
        QueueFree();
    }

    /// <summary>Avion cargo : passe à ~60 m au-dessus du point de pose à l'instant du largage, puis s'éloigne.</summary>
    private void PlacePlane()
    {
        if (_plane == null) return;
        var along = (_t - PlaneLead) * PlaneSpeed;
        _plane.GlobalPosition = _land + Vector3.Up * (DropHeight + 7f) - _axis * along;
        _plane.Rotation = new Vector3(0, Mathf.Atan2(-_axis.X, -_axis.Z), 0);
        _plane.Visible = Mathf.Abs(along) < 900f;
        if (_t > PlaneLead + 10f)
        {
            _plane.QueueFree();
            _plane = null;
        }
    }

    private static Node3D BuildPlane()
    {
        var root = new Node3D();
        var paint = Mats.Painted(new Color(0.26f, 0.28f, 0.25f), 0.7f, 0.2f);
        var dark = Mats.Metal(new Color(0.1f, 0.1f, 0.1f), 0.6f);
        void Box(Vector3 size, Vector3 at, Material m, Vector3 rot = default) =>
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = m }, Position = at, RotationDegrees = rot, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        // Fuselage (axe +Z = avant), ailes hautes, quatre moteurs, empennage en T, rampe arrière ouverte.
        root.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 2.3f, Height = 30f, RadialSegments = 12, Rings = 4, Material = paint }, RotationDegrees = new Vector3(90, 0, 0) });
        Box(new Vector3(40f, 0.5f, 5f), new Vector3(0, 1.9f, 1.5f), paint);
        foreach (var x in new[] { -13f, -6.5f, 6.5f, 13f })
        {
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.9f, Height = 3.6f, Material = dark }, Position = new Vector3(x, 1.2f, 3.2f), RotationDegrees = new Vector3(90, 0, 0) });
            Box(new Vector3(0.15f, 4.2f, 0.25f), new Vector3(x, 1.2f, 5.1f), dark);
        }
        Box(new Vector3(0.5f, 6f, 4f), new Vector3(0, 4.5f, -13.5f), paint, new Vector3(-12, 0, 0));
        Box(new Vector3(14f, 0.35f, 3.2f), new Vector3(0, 7.2f, -14.5f), paint);
        Box(new Vector3(3.4f, 0.25f, 5f), new Vector3(0, -2.2f, -15.5f), dark, new Vector3(-22, 0, 0));
        // Feux de position (rouge à gauche, vert à droite).
        root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, Material = Mats.Glow(new Color(1f, 0.1f, 0.1f), 6f) }, Position = new Vector3(-20f, 1.9f, 1.5f) });
        root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, Material = Mats.Glow(new Color(0.1f, 1f, 0.2f), 6f) }, Position = new Vector3(20f, 1.9f, 1.5f) });
        return root;
    }

    /// <summary>Voilures (trois pour le char, deux pour l'hélicoptère) et suspentes jusqu'au point d'accroche du véhicule.</summary>
    private static Node3D BuildRig(int type)
    {
        var rig = new Node3D();
        var cloth = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.32f, 0.22f), Roughness = 0.95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var cloth2 = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.23f, 0.17f), Roughness = 0.95f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var cord = Mats.Painted(new Color(0.15f, 0.15f, 0.13f), 0.9f);
        var offsets = type == CallIns.Heli
            ? new[] { new Vector3(-3.6f, 0, 0), new Vector3(3.6f, 0, 0) }
            : new[] { new Vector3(-5f, 0, 1.2f), new Vector3(5f, 0, 1.2f), new Vector3(0, 0.8f, -3.8f) };
        const float lift = 13f, radius = 4.6f;
        foreach (var o in offsets)
        {
            var top = o + Vector3.Up * lift;
            var canopy = new Node3D { Position = top };
            rig.AddChild(canopy);
            // Coupole : hémisphère aplati, fuseaux alternés clair / foncé.
            for (var g = 0; g < 2; g++)
                canopy.AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = radius, Height = radius, IsHemisphere = true, RadialSegments = 16, Rings = 5, Material = g == 0 ? cloth : cloth2 },
                    Scale = new Vector3(g == 0 ? 1f : 0.985f, 0.55f, g == 0 ? 1f : 0.985f),
                    RotationDegrees = new Vector3(0, g * 11.25f, 0),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                });
            canopy.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.6f, Height = 0.3f, Material = cloth2 }, Position = new Vector3(0, radius * 0.55f, 0) });
            // Suspentes : du bord de la coupole au point d'accroche.
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.Tau / 6f;
                var rim = top + new Vector3(Mathf.Cos(a) * radius * 0.95f, 0, Mathf.Sin(a) * radius * 0.95f);
                var anchor = new Vector3(o.X * 0.15f, 0, o.Z * 0.15f);
                rig.AddChild(Line(anchor, rim, cord));
            }
        }
        return rig;
    }

    private static MeshInstance3D Line(Vector3 a, Vector3 b, Material m)
    {
        var d = b - a;
        var len = d.Length();
        var y = d / len;
        var x = Mathf.Abs(y.Dot(Vector3.Right)) > 0.9f ? Vector3.Forward.Cross(y).Normalized() : Vector3.Right.Cross(y).Normalized();
        var z = x.Cross(y);
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = len, RadialSegments = 4, Rings = 1, Material = m },
            Transform = new Transform3D(new Basis(x, y, z), (a + b) / 2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }
}
