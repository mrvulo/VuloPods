namespace VuloPods.Core.Aap;

/// <summary>Verbindung zum Apple Accessory Protocol. In v1 nicht verfügbar; Implementierung folgt mit dem Treiber (Teilprojekt 2).</summary>
public interface IAapTransport
{
    bool IsAvailable { get; }
    Task SetNoiseControlAsync(NoiseControlMode mode, CancellationToken cancellationToken = default);
}
