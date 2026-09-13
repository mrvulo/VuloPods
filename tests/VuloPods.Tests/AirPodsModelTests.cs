using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class AirPodsModelTests
{
    [Theory]
    [InlineData(0x0220, AirPodsModel.AirPods1)]
    [InlineData(0x0F20, AirPodsModel.AirPods2)]
    [InlineData(0x1320, AirPodsModel.AirPods3)]
    [InlineData(0x1920, AirPodsModel.AirPods4)]
    [InlineData(0x1B20, AirPodsModel.AirPods4Anc)]
    [InlineData(0x0E20, AirPodsModel.AirPodsPro)]
    [InlineData(0x1420, AirPodsModel.AirPodsPro2)]
    [InlineData(0x2420, AirPodsModel.AirPodsPro2)]
    [InlineData(0x2720, AirPodsModel.AirPodsPro3)]
    [InlineData(0x0A20, AirPodsModel.AirPodsMax)]
    [InlineData(0x1F20, AirPodsModel.AirPodsMax)]
    [InlineData(0x9999, AirPodsModel.Unknown)]
    public void FromId_MapsKnownIds(int id, AirPodsModel expected)
    {
        Assert.Equal(expected, AirPodsModels.FromId((ushort)id));
    }

    [Theory]
    [InlineData(AirPodsModel.AirPodsPro2, "AirPods Pro (2. Gen)")]
    [InlineData(AirPodsModel.AirPods4Anc, "AirPods 4 (ANC)")]
    [InlineData(AirPodsModel.Unknown, "AirPods (unbekannt)")]
    public void DisplayName_IsGerman(AirPodsModel model, string expected)
    {
        Assert.Equal(expected, AirPodsModels.DisplayName(model));
    }
}
