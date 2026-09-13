using VuloPods.Core.Aap;

namespace VuloPods.Tests;

public class AapTransportTests
{
    [Fact]
    public async Task Unavailable_ReportsNotAvailable_AndThrows()
    {
        IAapTransport transport = new UnavailableAapTransport();
        Assert.False(transport.IsAvailable);
        await Assert.ThrowsAsync<NotSupportedException>(() => transport.SetNoiseControlAsync(NoiseControlMode.Transparency));
    }

    [Fact]
    public void ModeValues_MatchAapWireValues()
    {
        Assert.Equal(1, (int)NoiseControlMode.Off);
        Assert.Equal(2, (int)NoiseControlMode.NoiseCancellation);
        Assert.Equal(3, (int)NoiseControlMode.Transparency);
        Assert.Equal(4, (int)NoiseControlMode.Adaptive);
    }
}
