namespace VuloPods.Core.Models;

public enum AirPodsModel
{
    Unknown,
    AirPods1,
    AirPods2,
    AirPods3,
    AirPods4,
    AirPods4Anc,
    AirPodsPro,
    AirPodsPro2,
    AirPodsPro3,
    AirPodsMax,
}

public static class AirPodsModels
{
    // Modell-IDs aus Byte 3-4 des Proximity-Pairing-Pakets (Big Endian).
    // Quellen: OpenPods PodsStatus.java, librepods. Unverifizierte IDs werden
    // beim Aufzeichnen eigener Pakete bestätigt (siehe README).
    public static AirPodsModel FromId(ushort modelId) => modelId switch
    {
        0x0220 => AirPodsModel.AirPods1,
        0x0F20 => AirPodsModel.AirPods2,
        0x1320 => AirPodsModel.AirPods3,
        0x1920 => AirPodsModel.AirPods4,
        0x1B20 => AirPodsModel.AirPods4Anc,
        0x0E20 => AirPodsModel.AirPodsPro,
        0x1420 => AirPodsModel.AirPodsPro2,
        0x2420 => AirPodsModel.AirPodsPro2,
        0x2720 => AirPodsModel.AirPodsPro3,
        0x0A20 => AirPodsModel.AirPodsMax,
        0x1F20 => AirPodsModel.AirPodsMax,
        _ => AirPodsModel.Unknown,
    };

    public static string DisplayName(AirPodsModel model) => model switch
    {
        AirPodsModel.AirPods1 => "AirPods (1. Gen)",
        AirPodsModel.AirPods2 => "AirPods (2. Gen)",
        AirPodsModel.AirPods3 => "AirPods (3. Gen)",
        AirPodsModel.AirPods4 => "AirPods 4",
        AirPodsModel.AirPods4Anc => "AirPods 4 (ANC)",
        AirPodsModel.AirPodsPro => "AirPods Pro",
        AirPodsModel.AirPodsPro2 => "AirPods Pro (2. Gen)",
        AirPodsModel.AirPodsPro3 => "AirPods Pro (3. Gen)",
        AirPodsModel.AirPodsMax => "AirPods Max",
        _ => "AirPods (unbekannt)",
    };
}
