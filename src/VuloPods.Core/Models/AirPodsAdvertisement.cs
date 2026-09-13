namespace VuloPods.Core.Models;

/// <summary>Ein geparstes Proximity-Pairing-Paket. Akkuwerte in Prozent (10er-Schritte) oder null = unbekannt.</summary>
public sealed record AirPodsAdvertisement(
    ushort ModelId,
    AirPodsModel Model,
    int? LeftBattery,
    int? RightBattery,
    int? CaseBattery,
    bool LeftCharging,
    bool RightCharging,
    bool CaseCharging,
    bool LeftInEar,
    bool RightInEar,
    bool BothInCase,
    bool LidOpen,
    int LidOpenCounter);
