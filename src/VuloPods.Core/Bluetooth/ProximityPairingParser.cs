using VuloPods.Core.Models;

namespace VuloPods.Core.Bluetooth;

public static class ProximityPairingParser
{
    public const ushort AppleCompanyId = 0x004C;
    private const byte ProximityPairingType = 0x07;
    private const byte PairedPrefix = 0x01;
    private const int MinLength = 11;

    /// <param name="data">Herstellerdaten ohne die zwei Bytes Company-ID; data[0] muss 0x07 sein.</param>
    public static AirPodsAdvertisement? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinLength || data[0] != ProximityPairingType)
            return null;

        if (data[2] != PairedPrefix)
            return null; // 0x00 = Kopplungsmodus / Kurzform mit anderer Struktur

        var modelId = (ushort)((data[3] << 8) | data[4]);
        var status = data[5];
        var battery = data[6];
        var chargingAndCase = data[7];
        var lid = data[8];

        var currentIsLeft = (status & 0x20) != 0;
        var currentInEar = (status & 0x02) != 0;
        var otherInEar = (status & 0x08) != 0;
        var bothInCase = (status & 0x04) != 0;

        var currentBattery = ToPercent(battery & 0x0F);
        var otherBattery = ToPercent(battery >> 4);
        var caseBattery = ToPercent(chargingAndCase & 0x0F);

        var currentCharging = (chargingAndCase & 0x10) != 0;
        var otherCharging = (chargingAndCase & 0x20) != 0;
        var caseCharging = (chargingAndCase & 0x40) != 0;

        var lidOpen = (lid & 0x08) == 0;
        var lidCounter = lid & 0x07;

        return new AirPodsAdvertisement(
            ModelId: modelId,
            Model: AirPodsModels.FromId(modelId),
            LeftBattery: currentIsLeft ? currentBattery : otherBattery,
            RightBattery: currentIsLeft ? otherBattery : currentBattery,
            CaseBattery: caseBattery,
            LeftCharging: currentIsLeft ? currentCharging : otherCharging,
            RightCharging: currentIsLeft ? otherCharging : currentCharging,
            CaseCharging: caseCharging,
            LeftInEar: currentIsLeft ? currentInEar : otherInEar,
            RightInEar: currentIsLeft ? otherInEar : currentInEar,
            BothInCase: bothInCase,
            LidOpen: lidOpen,
            LidOpenCounter: lidCounter);
    }

    private static int? ToPercent(int nibble) => nibble switch
    {
        0xF => null,
        >= 0xA => 100,
        _ => nibble * 10,
    };
}
