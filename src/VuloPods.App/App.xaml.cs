using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VuloPods.App.Interop;
using VuloPods.App.Notifications;
using VuloPods.Core.Battery;
using VuloPods.Core.Bluetooth;
using VuloPods.Core.Logging;
using VuloPods.Core.Media;
using VuloPods.Core.Models;

namespace VuloPods.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AirPodsScanner? _scanner;
    private AirPodsStateStore? _store;
    private AutoPausePolicy? _autoPause;
    private TrayService? _tray;
    private PopupWindow? _popup;
    private ScannerStatus _scannerStatus = ScannerStatus.Scanning;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\VuloPods.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Log.Initialize(Log.DefaultDirectory);
        Log.Info("VuloPods gestartet");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unbehandelte Ausnahme", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unbeobachtete Task-Ausnahme", args.Exception);
            args.SetObserved();
        };

        // Lokale Variablen statt Felder in den Lambdas, damit die Nullable-Analyse keine Warnungen erzeugt.
        var time = TimeProvider.System;
        var store = _store = new AirPodsStateStore(time);
        var autoPause = _autoPause = new AutoPausePolicy(new MediaController(), time);
        var lowBattery = new LowBatteryPolicy();
        var scanner = _scanner = new AirPodsScanner(time) { LogRawPackets = e.Args.Contains("--dump") };
        var tray = _tray = new TrayService();
        var popup = _popup = new PopupWindow();

        tray.ShowRequested += () => popup.ShowFor(store.Current?.Advertisement);
        tray.ExportDiagnosticsRequested += () => _ = ExportDiagnosticsAsync();
        tray.ExitRequested += () => Shutdown();

        scanner.Received += store.Push;
        scanner.StatusChanged += status => OnUi(() =>
        {
            _scannerStatus = status;
            RefreshTooltip();
        });

        store.DeviceAppeared += state =>
        {
            autoPause.OnEarState(state.Advertisement.LeftInEar, state.Advertisement.RightInEar);
            OnUi(RefreshTooltip);
            EvaluateLowBattery(lowBattery, state);
            Log.Info($"Gerät: {AirPodsModels.DisplayName(state.Advertisement.Model)} (0x{state.Advertisement.ModelId:X4}), {state.Rssi} dBm");
        };
        store.StateChanged += state =>
        {
            EvaluateLowBattery(lowBattery, state);
            OnUi(() =>
            {
                RefreshTooltip();
                popup.RefreshIfVisible(store.Current?.Advertisement);
            });
        };
        store.LidOpened += state => OnUi(() => popup.ShowFor(state.Advertisement));
        store.EarStateChanged += (state, _) =>
            autoPause.OnEarState(state.Advertisement.LeftInEar, state.Advertisement.RightInEar);
        store.DeviceLost += () =>
        {
            autoPause.Reset();
            OnUi(() =>
            {
                lowBattery.Reset();
                RefreshTooltip();
                Log.Info("Gerät verloren");
            });
        };

        scanner.Start();
    }

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            var zipPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"VuloPods-Diagnose-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            var adapterName = await GetBluetoothAdapterNameAsync();
            var systemInfo = string.Join(Environment.NewLine,
                $"OS: {Environment.OSVersion.VersionString}",
                $"App: {typeof(App).Assembly.GetName().Version}",
                $".NET: {Environment.Version}",
                $"Bluetooth-Adapter: {adapterName}");
            var packets = _scanner?.RecentRawPackets ?? Array.Empty<string>();

            await Task.Run(() => DiagnosticsExporter.Export(Log.DefaultDirectory, packets, systemInfo, zipPath));
            Log.Info($"Diagnose exportiert: {zipPath}");
            Process.Start("explorer.exe", $"/select,\"{zipPath}\"");
        }
        catch (Exception ex)
        {
            Log.Error("Diagnose-Export fehlgeschlagen", ex);
        }
    }

    private static async Task<string> GetBluetoothAdapterNameAsync()
    {
        try
        {
            var adapter = await global::Windows.Devices.Bluetooth.BluetoothAdapter.GetDefaultAsync();
            if (adapter is null) return "unbekannt";
            var info = await global::Windows.Devices.Enumeration.DeviceInformation.CreateFromIdAsync(adapter.DeviceId);
            return string.IsNullOrWhiteSpace(info?.Name) ? "unbekannt" : info.Name;
        }
        catch (Exception ex)
        {
            Log.Error("Bluetooth-Adapter-Name nicht ermittelbar", ex);
            return "unbekannt";
        }
    }

    private void EvaluateLowBattery(LowBatteryPolicy policy, AirPodsState state)
    {
        // Die Policy ist nicht thread-sicher; Events kommen vom Scanner-Thread, daher alles auf dem UI-Thread.
        OnUi(() =>
        {
            foreach (var alert in policy.Evaluate(state.Advertisement))
            {
                if (!FullscreenDetector.IsFullscreenForeground()) ToastNotifier.ShowLowBattery(alert);
            }
        });
    }

    private void RefreshTooltip() =>
        _tray?.SetTooltip(StatusText.Tooltip(_store?.Current?.Advertisement, _scannerStatus));

    private void OnUi(Action action) => Dispatcher.BeginInvoke(action);

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unbehandelte UI-Ausnahme", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scanner?.Dispose();
        _store?.Dispose();
        _autoPause?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Log.Info("VuloPods beendet");
        base.OnExit(e);
    }
}
