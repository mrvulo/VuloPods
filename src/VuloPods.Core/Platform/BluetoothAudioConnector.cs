using global::System.Runtime.InteropServices;
using VuloPods.Core.Logging;
using VuloPods.Core.Platform.Interop;

namespace VuloPods.Core.Platform;

/// <summary>
/// Ein gekoppeltes Bluetooth-Audiogerät (Apple/Beats), gruppiert nach Container-ID.
/// </summary>
/// <param name="Name">Anzeigename der Geräteschnittstelle (z. B. „AirPods“).</param>
/// <param name="ContainerId">PKEY_Device_ContainerId des Geräts als GUID-String.</param>
/// <param name="IsConnected">true, wenn mindestens ein Audio-Endpunkt des Geräts aktiv ist.</param>
/// <param name="FilterIds">Eindeutige IDs der Bluetooth-KS-Filter (<c>{2}.\\?\bth…</c>), an die One-Shot-Properties gehen.</param>
public sealed record PairedAudioDevice(string Name, string ContainerId, bool IsConnected, IReadOnlyList<string> FilterIds);

/// <summary>
/// Verbindet und trennt gekoppelte Apple/Beats-Bluetooth-Audiogeräte treiberfrei und ohne
/// Adminrechte über die One-Shot-Properties von <c>KSPROPSETID_BtAudio</c> auf dem KS-Filter –
/// derselbe Weg, den das Windows-Soundpanel benutzt.
/// </summary>
/// <remarks>
/// Alle Methoden sind synchron, blockieren mehrere hundert Millisekunden und müssen auf einem
/// MTA-Threadpool-Thread laufen (z. B. per <c>Task.Run</c>). <b>Niemals auf dem UI-Thread
/// aufrufen.</b> Die Methoden werfen nie; Fehler werden geloggt und als leere Liste bzw.
/// <c>false</c> zurückgegeben.
/// </remarks>
public static class BluetoothAudioConnector
{
    private const string BluetoothFilterPrefix = @"{2}.\\?\bth";

    /// <summary>
    /// Zählt alle gekoppelten Bluetooth-Audiogeräte auf, deren Name „AirPods“ oder „Beats“ enthält.
    /// </summary>
    /// <remarks>Nicht auf dem UI-Thread aufrufen; siehe Klassenbeschreibung.</remarks>
    /// <returns>Gefundene Geräte, bei Fehlern eine leere Liste.</returns>
    public static IReadOnlyList<PairedAudioDevice> FindAppleAudioDevices()
    {
        try
        {
            var devices = EnumerateDevices();
            foreach (var device in devices)
            {
                Log.Info($"Bluetooth-Audiogerät gefunden: \"{device.Name}\" verbunden={device.IsConnected} Filter={device.FilterIds.Count}");
            }

            return devices;
        }
        catch (Exception ex)
        {
            Log.Error("Bluetooth-Audiogeräte konnten nicht aufgezählt werden", ex);
            return Array.Empty<PairedAudioDevice>();
        }
    }

    /// <summary>
    /// Sendet <c>KSPROPERTY_ONESHOT_RECONNECT</c> an alle Filter des Geräts.
    /// </summary>
    /// <remarks>
    /// Nicht auf dem UI-Thread aufrufen. <c>true</c> bedeutet nur, dass Windows den Auftrag
    /// angenommen hat; der tatsächliche Zustand muss später per
    /// <see cref="FindAppleAudioDevices"/> neu geprüft werden.
    /// </remarks>
    public static bool Connect(PairedAudioDevice device) =>
        SendOneShot(device, CoreAudioConstants.KsPropertyOneShotReconnect, "Verbinden");

    /// <summary>
    /// Sendet <c>KSPROPERTY_ONESHOT_DISCONNECT</c> an alle Filter des Geräts.
    /// </summary>
    /// <remarks>
    /// Nicht auf dem UI-Thread aufrufen. <c>true</c> bedeutet nur, dass Windows den Auftrag
    /// angenommen hat; der tatsächliche Zustand muss später per
    /// <see cref="FindAppleAudioDevices"/> neu geprüft werden.
    /// </remarks>
    public static bool Disconnect(PairedAudioDevice device) =>
        SendOneShot(device, CoreAudioConstants.KsPropertyOneShotDisconnect, "Trennen");

    private sealed class DeviceGroup
    {
        public required string Name { get; init; }
        public bool IsConnected { get; set; }
        public List<string> FilterIds { get; } = new();
    }

    private static IReadOnlyList<PairedAudioDevice> EnumerateDevices()
    {
        var groups = new Dictionary<string, DeviceGroup>(StringComparer.OrdinalIgnoreCase);
        var filterComparer = StringComparer.OrdinalIgnoreCase;

        object? enumeratorObject = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumeratorObject = new MMDeviceEnumeratorComObject();
            var enumerator = (IMMDeviceEnumerator)enumeratorObject;

            var hr = enumerator.EnumAudioEndpoints(
                CoreAudioConstants.EDataFlowAll,
                CoreAudioConstants.DeviceStateActive | CoreAudioConstants.DeviceStateUnplugged,
                out collection);
            if (hr != CoreAudioConstants.SOk || collection is null)
            {
                Log.Error($"EnumAudioEndpoints fehlgeschlagen: 0x{hr:X8}");
                return Array.Empty<PairedAudioDevice>();
            }

            hr = collection.GetCount(out var count);
            if (hr != CoreAudioConstants.SOk)
            {
                Log.Error($"IMMDeviceCollection.GetCount fehlgeschlagen: 0x{hr:X8}");
                return Array.Empty<PairedAudioDevice>();
            }

            for (uint i = 0; i < count; i++)
            {
                IMMDevice? endpoint = null;
                try
                {
                    hr = collection.Item(i, out endpoint);
                    if (hr != CoreAudioConstants.SOk || endpoint is null) continue;

                    ProcessEndpoint(endpoint, groups, filterComparer);
                }
                catch (Exception ex)
                {
                    Log.Error($"Audio-Endpunkt {i} konnte nicht ausgewertet werden", ex);
                }
                finally
                {
                    Release(endpoint);
                }
            }
        }
        finally
        {
            Release(collection);
            Release(enumeratorObject);
        }

        var result = new List<PairedAudioDevice>();
        foreach (var (containerId, group) in groups)
        {
            if (group.FilterIds.Count == 0) continue;
            if (!IsAppleOrBeats(group.Name)) continue;

            result.Add(new PairedAudioDevice(group.Name, containerId, group.IsConnected, group.FilterIds.AsReadOnly()));
        }

        return result;
    }

    private static void ProcessEndpoint(IMMDevice endpoint, Dictionary<string, DeviceGroup> groups, StringComparer filterComparer)
    {
        var hr = endpoint.GetState(out var state);
        var isActive = hr == CoreAudioConstants.SOk && (state & CoreAudioConstants.DeviceStateActive) != 0;

        var (containerId, friendlyName) = ReadEndpointProperties(endpoint);
        var filterIds = CollectBluetoothFilterIds(endpoint);

        Log.Debug($"Audio-Endpunkt \"{friendlyName}\" Zustand=0x{state:X} Container={containerId} BT-Filter={filterIds.Count}");

        if (filterIds.Count == 0 || containerId is null) return;

        if (!groups.TryGetValue(containerId, out var group))
        {
            group = new DeviceGroup { Name = friendlyName ?? string.Empty };
            groups[containerId] = group;
        }

        group.IsConnected |= isActive;
        foreach (var filterId in filterIds)
        {
            if (!group.FilterIds.Contains(filterId, filterComparer))
            {
                group.FilterIds.Add(filterId);
            }
        }
    }

    private static List<string> CollectBluetoothFilterIds(IMMDevice endpoint)
    {
        var filterIds = new List<string>();
        object? topologyObject = null;
        try
        {
            var iid = CoreAudioConstants.IidIDeviceTopology;
            var hr = endpoint.Activate(ref iid, CoreAudioConstants.ClsCtxAll, IntPtr.Zero, out topologyObject);
            if (hr != CoreAudioConstants.SOk || topologyObject is not IDeviceTopology topology) return filterIds;

            hr = topology.GetConnectorCount(out var connectorCount);
            if (hr != CoreAudioConstants.SOk) return filterIds;

            for (uint c = 0; c < connectorCount; c++)
            {
                IConnector? connector = null;
                try
                {
                    hr = topology.GetConnector(c, out connector);
                    if (hr != CoreAudioConstants.SOk || connector is null) continue;

                    hr = connector.GetDeviceIdConnectedTo(out var deviceId);
                    if (hr != CoreAudioConstants.SOk || string.IsNullOrEmpty(deviceId))
                    {
                        // E_NOTFOUND (0x80070490): Connector hängt an keinem Gerät – normal für
                        // getrennte Endpunkte, einfach überspringen.
                        if (hr != CoreAudioConstants.ENotFound)
                        {
                            Log.Debug($"GetDeviceIdConnectedTo Connector {c}: HRESULT 0x{hr:X8}");
                        }
                        continue;
                    }

                    if (deviceId.StartsWith(BluetoothFilterPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        filterIds.Add(deviceId);
                    }
                }
                finally
                {
                    Release(connector);
                }
            }
        }
        finally
        {
            Release(topologyObject);
        }

        return filterIds;
    }

    private static (string? ContainerId, string? FriendlyName) ReadEndpointProperties(IMMDevice endpoint)
    {
        IPropertyStore? store = null;
        try
        {
            var hr = endpoint.OpenPropertyStore(CoreAudioConstants.StgmRead, out store);
            if (hr != CoreAudioConstants.SOk || store is null) return (null, null);

            string? containerId = null;
            string? friendlyName = null;

            var containerKey = CoreAudioConstants.PkeyDeviceContainerId;
            hr = store.GetValue(ref containerKey, out var containerValue);
            try
            {
                if (hr == CoreAudioConstants.SOk)
                {
                    containerId = containerValue.GetClsid()?.ToString("B");
                }
            }
            finally
            {
                containerValue.Clear();
            }

            var nameKey = CoreAudioConstants.PkeyDeviceInterfaceFriendlyName;
            hr = store.GetValue(ref nameKey, out var nameValue);
            try
            {
                if (hr == CoreAudioConstants.SOk)
                {
                    friendlyName = nameValue.GetLpwstr();
                }
            }
            finally
            {
                nameValue.Clear();
            }

            return (containerId, friendlyName);
        }
        finally
        {
            Release(store);
        }
    }

    private static bool SendOneShot(PairedAudioDevice device, uint propertyId, string action)
    {
        try
        {
            var filterIds = device.FilterIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (filterIds.Count == 0)
            {
                Log.Error($"{action} \"{device.Name}\": keine Filter-IDs vorhanden");
                return false;
            }

            var anySucceeded = false;
            object? enumeratorObject = null;
            try
            {
                enumeratorObject = new MMDeviceEnumeratorComObject();
                var enumerator = (IMMDeviceEnumerator)enumeratorObject;

                foreach (var filterId in filterIds)
                {
                    var hr = SendOneShotToFilter(enumerator, filterId, propertyId);
                    Log.Info($"{action} \"{device.Name}\" Filter {filterId}: HRESULT 0x{hr:X8}");
                    anySucceeded |= hr == CoreAudioConstants.SOk;
                }
            }
            finally
            {
                Release(enumeratorObject);
            }

            return anySucceeded;
        }
        catch (Exception ex)
        {
            Log.Error($"{action} \"{device.Name}\" fehlgeschlagen", ex);
            return false;
        }
    }

    private static int SendOneShotToFilter(IMMDeviceEnumerator enumerator, string filterId, uint propertyId)
    {
        IMMDevice? filter = null;
        object? controlObject = null;
        try
        {
            var hr = enumerator.GetDevice(filterId, out filter);
            if (hr != CoreAudioConstants.SOk || filter is null)
            {
                return hr == CoreAudioConstants.SOk ? unchecked((int)0x80004005) : hr; // E_FAIL bei null
            }

            var iid = CoreAudioConstants.IidIKsControl;
            hr = filter.Activate(ref iid, CoreAudioConstants.ClsCtxAll, IntPtr.Zero, out controlObject);
            if (hr != CoreAudioConstants.SOk || controlObject is not IKsControl control)
            {
                return hr == CoreAudioConstants.SOk ? unchecked((int)0x80004002) : hr; // E_NOINTERFACE bei falschem Typ
            }

            var property = new KsProperty
            {
                Set = CoreAudioConstants.KsPropSetIdBtAudio,
                Id = propertyId,
                Flags = CoreAudioConstants.KsPropertyTypeGet,
            };

            return control.KsProperty(ref property, KsProperty.Size, IntPtr.Zero, 0, out _);
        }
        finally
        {
            Release(controlObject);
            Release(filter);
        }
    }

    private static bool IsAppleOrBeats(string name) =>
        name.Contains("AirPods", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Beats", StringComparison.OrdinalIgnoreCase);

    private static void Release(object? comObject)
    {
        if (comObject is null) return;
        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch (Exception ex)
        {
            Log.Error("COM-Objekt konnte nicht freigegeben werden", ex);
        }
    }
}
