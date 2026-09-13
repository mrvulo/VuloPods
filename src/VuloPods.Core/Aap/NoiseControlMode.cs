namespace VuloPods.Core.Aap;

/// <summary>Werte entsprechen dem AAP-Befehl 09 00 0D &lt;mode&gt;.</summary>
public enum NoiseControlMode
{
    Off = 1,
    NoiseCancellation = 2,
    Transparency = 3,
    Adaptive = 4,
}
