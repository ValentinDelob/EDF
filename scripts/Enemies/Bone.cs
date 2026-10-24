using Godot;

namespace Rempart;

/// <summary>
/// Nœud articulé des modèles d'ennemis (hanche, fémur, tête, aile...). Tant qu'il est « vivant » (<see cref="Live"/>),
/// c'est un Node3D ordinaire (forteresse, machines, modèle pas encore cuit). Une fois l'insecte cuit en maillages à os
/// (<see cref="BugModel"/>, BugModel.Skin.cs), l'armature sort de la scène et ses poses ne vivent plus que côté C# :
/// l'animation écrit ici sans aucun appel au moteur, et la synchronisation lit <see cref="Transform"/> sans aller-retour.
/// Les propriétés masquent celles de Node3D : elles ne s'appliquent que par une référence de type <see cref="Bone"/>.
/// </summary>
public partial class Bone : Node3D
{
    /// <summary>Vrai : les propriétés passent par le Node3D du moteur ; faux : pose tenue en C# seulement.</summary>
    public bool Live = true;

    private Vector3 _pos, _rot, _scale = Vector3.One;
    private Transform3D _xf = Transform3D.Identity;
    private bool _dirty;

    public new Vector3 Position
    {
        get => Live ? base.Position : _pos;
        set
        {
            if (Live) base.Position = value;
            else { _pos = value; _dirty = true; }
        }
    }

    /// <summary>Angles d'Euler (radians, ordre YXZ comme Node3D).</summary>
    public new Vector3 Rotation
    {
        get => Live ? base.Rotation : _rot;
        set
        {
            if (Live) base.Rotation = value;
            else { _rot = value; _dirty = true; }
        }
    }

    public new Vector3 RotationDegrees
    {
        get => Live ? base.RotationDegrees : new Vector3(Mathf.RadToDeg(_rot.X), Mathf.RadToDeg(_rot.Y), Mathf.RadToDeg(_rot.Z));
        set
        {
            if (Live) base.RotationDegrees = value;
            else { _rot = new Vector3(Mathf.DegToRad(value.X), Mathf.DegToRad(value.Y), Mathf.DegToRad(value.Z)); _dirty = true; }
        }
    }

    public new Vector3 Scale
    {
        get => Live ? base.Scale : _scale;
        set
        {
            if (Live) base.Scale = value;
            else { _scale = value; _dirty = true; }
        }
    }

    /// <summary>Repère local (même composition que Node3D : rotation YXZ puis échelle, puis translation).</summary>
    public new Transform3D Transform
    {
        get
        {
            if (Live) return base.Transform;
            if (_dirty)
            {
                _xf = new Transform3D(EulerScale(_rot, _scale), _pos);
                _dirty = false;
            }
            return _xf;
        }
    }

    /// <summary>Ry · Rx · Rz · S (ordre YXZ de Node3D), développé : Basis.FromEuler compose trois matrices.</summary>
    private static Basis EulerScale(Vector3 r, Vector3 s)
    {
        // Mathf.SinCos est ~25 fois plus lent que Sin et Cos séparés.
        float sx = Mathf.Sin(r.X), cx = Mathf.Cos(r.X);
        float sy = Mathf.Sin(r.Y), cy = Mathf.Cos(r.Y);
        float sz = Mathf.Sin(r.Z), cz = Mathf.Cos(r.Z);
        // Lignes de Ry·Rx·Rz·S (Basis range ses lignes dans Row0..2).
        Basis b;
        b.Row0 = new Vector3((cy * cz + sy * sx * sz) * s.X, (-cy * sz + sy * sx * cz) * s.Y, sy * cx * s.Z);
        b.Row1 = new Vector3(cx * sz * s.X, cx * cz * s.Y, -sx * s.Z);
        b.Row2 = new Vector3((-sy * cz + cy * sx * sz) * s.X, (sy * sz + cy * sx * cz) * s.Y, cy * cx * s.Z);
        return b;
    }

    /// <summary>Passe la pose côté C# (le nœud moteur garde sa dernière pose et n'est plus mis à jour).</summary>
    public void Freeze()
    {
        if (!Live) return;
        _pos = base.Position;
        _rot = base.Rotation;
        _scale = base.Scale;
        _dirty = true;
        Live = false;
    }
}
