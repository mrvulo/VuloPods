using VuloPods.Core.Bluetooth;
using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class StatusTextTests
{
    [Fact]
    public void Tooltip_WithDevice_ShowsModelAndBatteries()
    {
        var adv = new AirPodsAdvertisement(0x1420, AirPodsModel.AirPodsPro2, 80, 90, 60, false, false, false, true, true, false, false, 0);
        Assert.Equal("AirPods Pro (2. Gen)  L 80%  R 90%  Case 60%", StatusText.Tooltip(adv, ScannerStatus.Scanning));
    }

    [Fact]
    public void Tooltip_UnknownBattery_ShowsDash()
    {
        var adv = new AirPodsAdvertisement(0x1420, AirPodsModel.AirPodsPro2, 80, null, null, false, false, false, true, true, false, false, 0);
        Assert.Equal("AirPods Pro (2. Gen)  L 80%  R –  Case –", StatusText.Tooltip(adv, ScannerStatus.Scanning));
    }

    [Fact]
    public void Tooltip_NoDevice()
    {
        Assert.Equal("VuloPods – kein Gerät", StatusText.Tooltip(null, ScannerStatus.Scanning));
    }

    [Fact]
    public void Tooltip_BluetoothUnavailable_WinsOverDevice()
    {
        var adv = new AirPodsAdvertisement(0x1420, AirPodsModel.AirPodsPro2, 80, 90, 60, false, false, false, true, true, false, false, 0);
        Assert.Equal("VuloPods – Bluetooth nicht verfügbar", StatusText.Tooltip(adv, ScannerStatus.BluetoothUnavailable));
    }
}
