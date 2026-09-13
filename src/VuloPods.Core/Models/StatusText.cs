using VuloPods.Core.Bluetooth;

namespace VuloPods.Core.Models;

public static class StatusText
{
    public static string Tooltip(AirPodsAdvertisement? adv, ScannerStatus status)
    {
        if (status == ScannerStatus.BluetoothUnavailable) return "VuloPods – Bluetooth nicht verfügbar";
        if (adv is null) return "VuloPods – kein Gerät";
        return $"{AirPodsModels.DisplayName(adv.Model)}  L {Percent(adv.LeftBattery)}  R {Percent(adv.RightBattery)}  Case {Percent(adv.CaseBattery)}";
    }

    public static string Percent(int? value) => value is null ? "–" : $"{value}%";
}
