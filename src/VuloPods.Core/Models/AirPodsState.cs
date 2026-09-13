namespace VuloPods.Core.Models;

/// <summary>Ein empfangenes und geparstes Paket samt Signalstärke.</summary>
public sealed record AdvertisementReceived(AirPodsAdvertisement Advertisement, short Rssi, DateTimeOffset Timestamp);

/// <summary>Der zuletzt bekannte Zustand des aktiven Geräts.</summary>
public sealed record AirPodsState(AirPodsAdvertisement Advertisement, short Rssi, DateTimeOffset LastSeen);
