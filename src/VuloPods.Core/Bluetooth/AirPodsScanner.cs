using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using VuloPods.Core.Logging;
using VuloPods.Core.Models;

namespace VuloPods.Core.Bluetooth;

public enum ScannerStatus
{
    Scanning,
    BluetoothUnavailable,
}

/// <summary>Empfängt BLE-Werbepakete, filtert Apple-Herstellerdaten und liefert geparste Pakete.</summary>
public sealed class AirPodsScanner : IDisposable
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly ITimer _retryTimer;
    private readonly HashSet<ushort> _loggedUnknownModels = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private bool _wantRunning;

    /// <summary>Wenn true, wird jedes geparste Paket als Hex ins Log geschrieben (zum Aufzeichnen von Testfällen).</summary>
    public bool LogRawPackets { get; set; }

    public event Action<AdvertisementReceived>? Received;
    public event Action<ScannerStatus>? StatusChanged;

    public AirPodsScanner(TimeProvider time)
    {
        _retryTimer = time.CreateTimer(_ => StartWatcher(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        _wantRunning = true;
        StartWatcher();
    }

    public void Stop()
    {
        _wantRunning = false;
        _retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        lock (_gate)
        {
            DisposeWatcher();
        }
    }

    private void StartWatcher()
    {
        if (!_wantRunning) return;
        lock (_gate)
        {
            DisposeWatcher();
            try
            {
                var watcher = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Active,
                };
                watcher.Received += OnReceived;
                watcher.Stopped += OnStopped;
                watcher.Start();
                _watcher = watcher;
                Log.Info("BLE-Scanner gestartet");
            }
            catch (Exception ex)
            {
                Log.Error("BLE-Scanner konnte nicht starten", ex);
                ScheduleRetry();
                return;
            }
        }
        StatusChanged?.Invoke(ScannerStatus.Scanning);
    }

    private void DisposeWatcher()
    {
        if (_watcher is null) return;
        _watcher.Received -= OnReceived;
        _watcher.Stopped -= OnStopped;
        try { _watcher.Stop(); } catch (Exception ex) { Log.Error("BLE-Scanner Stop fehlgeschlagen", ex); }
        _watcher = null;
    }

    private void ScheduleRetry()
    {
        StatusChanged?.Invoke(ScannerStatus.BluetoothUnavailable);
        _retryTimer.Change(RetryInterval, Timeout.InfiniteTimeSpan);
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (!_wantRunning) return;
        Log.Error($"BLE-Scanner gestoppt: {args.Error}");
        ScheduleRetry();
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        try
        {
            foreach (var section in args.Advertisement.ManufacturerData)
            {
                if (section.CompanyId != ProximityPairingParser.AppleCompanyId) continue;

                var bytes = new byte[section.Data.Length];
                using (var reader = DataReader.FromBuffer(section.Data))
                {
                    reader.ReadBytes(bytes);
                }

                if (LogRawPackets && bytes.Length > 0 && bytes[0] == 0x07)
                    Log.Debug($"RAW {args.RawSignalStrengthInDBm} dBm {Convert.ToHexString(bytes)}");

                var adv = ProximityPairingParser.Parse(bytes);
                if (adv is null) continue;

                if (LogRawPackets)
                    Log.Debug($"RX {args.RawSignalStrengthInDBm} dBm {Convert.ToHexString(bytes)}");

                if (adv.Model == AirPodsModel.Unknown)
                {
                    var isNew = false;
                    lock (_loggedUnknownModels)
                    {
                        isNew = _loggedUnknownModels.Add(adv.ModelId);
                    }
                    if (isNew)
                        Log.Info($"Unbekannte Modell-ID 0x{adv.ModelId:X4}, Paket {Convert.ToHexString(bytes)}");
                }

                Received?.Invoke(new AdvertisementReceived(adv, args.RawSignalStrengthInDBm, args.Timestamp));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Fehler beim Verarbeiten eines Werbepakets", ex);
        }
    }

    public void Dispose()
    {
        Stop();
        _retryTimer.Dispose();
    }
}
