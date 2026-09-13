using System.Windows;
using System.Windows.Threading;
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
        var scanner = _scanner = new AirPodsScanner(time) { LogRawPackets = e.Args.Contains("--dump") };
        var tray = _tray = new TrayService();
        var popup = _popup = new PopupWindow();

        tray.ShowRequested += () => popup.ShowFor(store.Current?.Advertisement);
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
            Log.Info($"Gerät: {AirPodsModels.DisplayName(state.Advertisement.Model)} (0x{state.Advertisement.ModelId:X4}), {state.Rssi} dBm");
        };
        store.StateChanged += _ => OnUi(() =>
        {
            RefreshTooltip();
            popup.RefreshIfVisible(store.Current?.Advertisement);
        });
        store.LidOpened += state => OnUi(() => popup.ShowFor(state.Advertisement));
        store.EarStateChanged += (state, _) =>
            autoPause.OnEarState(state.Advertisement.LeftInEar, state.Advertisement.RightInEar);
        store.DeviceLost += () => OnUi(() =>
        {
            RefreshTooltip();
            Log.Info("Gerät verloren");
        });

        scanner.Start();
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
