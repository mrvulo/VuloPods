namespace VuloPods.Core.Aap;

public sealed class UnavailableAapTransport : IAapTransport
{
    public bool IsAvailable => false;

    public Task SetNoiseControlAsync(NoiseControlMode mode, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Der Geräuschmodus benötigt den VuloPods-Treiber (Teilprojekt 2).");
}
