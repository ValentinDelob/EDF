using Godot;

namespace Rempart;

/// <summary>
/// Son du jetpack (Aéroporté) : allumage, flamme continue en boucle dont le volume et la hauteur suivent la poussée,
/// puis extinction. Attaché au soldat (joueur local ou coéquipier) ; lu en 3D.
/// </summary>
public partial class JetpackAudio : Node3D
{
    private AudioStreamPlayer3D _loop;
    private float _level, _airTime;
    private bool _on;

    public override void _Ready()
    {
        _loop = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 160f, UnitSize = 10f, VolumeDb = -80f };
        AddChild(_loop);
    }

    /// <summary>À appeler à chaque image : <paramref name="thrust"/> vrai tant que le jetpack pousse, <paramref name="climb"/> vitesse verticale.</summary>
    public void Tick(bool thrust, float climb, float dt)
    {
        if (_loop == null) return;
        if (thrust && !_on)
        {
            _on = true;
            _airTime = 0;
            Sfx.At("jet_start", GlobalPosition, -4f, 1f, 120);
            if (_loop.Stream == null) _loop.Stream = Sfx.Loop("jet_loop");
            if (_loop.Stream != null && !_loop.Playing) _loop.Play((float)GD.RandRange(0, 3.0));
        }
        else if (!thrust && _on)
        {
            _on = false;
            if (_airTime > 0.2f) Sfx.At("jet_stop", GlobalPosition, -5f, 1f, 120);
        }
        if (_on) _airTime += dt;
        // Montée rapide du souffle à l'allumage, retombée plus douce à la coupure.
        _level = Mathf.MoveToward(_level, _on ? 1f : 0f, dt * (_on ? 5f : 7f));
        if (_level <= 0.001f)
        {
            if (_loop.Playing) _loop.Stop();
            return;
        }
        if (!_loop.Playing && _loop.Stream != null) _loop.Play();
        _loop.VolumeDb = Mathf.LinearToDb(_level) - 3f;
        // La flamme monte légèrement dans les aigus quand on grimpe vite.
        _loop.PitchScale = 0.92f + 0.08f * _level + Mathf.Clamp(climb / 10f, 0f, 1f) * 0.06f;
    }

    /// <summary>Rafale de poussée (dash, piqué).</summary>
    public void Boost(float volumeDb = -2f, float pitch = 1f) => Sfx.At("jet_boost", GlobalPosition, volumeDb, pitch, 80);
}
