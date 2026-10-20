using VuloPods.Core.Platform;

namespace VuloPods.Tests;

public class BluetoothAudioConnectorTests
{
    [Fact]
    public async Task FindAppleAudioDevices_DoesNotThrow()
    {
        // Läuft auf einem Threadpool-Thread (MTA), wie in der App. Muss auch ohne
        // Bluetooth-Radio bzw. ohne gekoppelte Geräte eine (ggf. leere) Liste liefern.
        var devices = await Task.Run(BluetoothAudioConnector.FindAppleAudioDevices);

        Assert.NotNull(devices);
        Assert.All(devices, d =>
        {
            Assert.False(string.IsNullOrEmpty(d.Name));
            Assert.False(string.IsNullOrEmpty(d.ContainerId));
            Assert.NotEmpty(d.FilterIds);
        });
    }
}
