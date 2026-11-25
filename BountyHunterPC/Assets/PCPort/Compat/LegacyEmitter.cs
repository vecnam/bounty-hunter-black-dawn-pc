using UnityEngine;

// Replaces the legacy ParticleEmitter (removed in Unity 2018.3). The game only turns emitters on and off
// (`particleEmitter.emit` and `.enabled`); here that becomes Play/Stop of the ParticleSystem on the same object.
// PcPortParticleConverter turns the 18 legacy emitters of the prefabs into ParticleSystems; without one, this is a no-op.
public sealed class LegacyEmitter
{
    private readonly ParticleSystem ps;

    public LegacyEmitter(ParticleSystem ps)
    {
        this.ps = ps;
    }

    public bool emit
    {
        get { return ps != null && ps.isEmitting; }
        set
        {
            if (ps == null) return;
            if (value) ps.Play();
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // like the legacy one: stops emitting, live particles finish
        }
    }

    // In the legacy system, disabling the component stopped the emission the same way.
    public bool enabled
    {
        get { return emit; }
        set { emit = value; }
    }
}

public static class LegacyEmitterExtensions
{
    public static LegacyEmitter GetLegacyEmitter(this Component c)
    {
        return new LegacyEmitter(c != null ? c.GetComponent<ParticleSystem>() : null);
    }

    public static LegacyEmitter GetLegacyEmitter(this GameObject g)
    {
        return new LegacyEmitter(g != null ? g.GetComponent<ParticleSystem>() : null);
    }
}
