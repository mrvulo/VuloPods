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
using VuloPods.Core.Platform;
using VuloPods.Core.Settings;

namespace VuloPods.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AirPodsScanner? _scanner;
    private AirPodsStateStore? _store;
    private AutoPausePolicy? _autoPause;
    private TrayService? _tray;
    private PopupWindow? _popup;
    private SettingsStore? _settings;
    private SettingsWindow? _settingsWindow;
    private AppSettings? _appliedSettings;
    private LowBatteryPolicy _lowBattery = new();
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

        var settings = _settings = new SettingsStore(SettingsStore.DefaultPath);
        settings.Load();

        ApplyTheme();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Lokale Variablen statt Felder in den Lambdas, damit die Nullable-Analyse keine Warnungen erzeugt.
        var time = TimeProvider.System;
        var store = _store = new AirPodsStateStore(time);
        var autoPause = _autoPause = new AutoPausePolicy(new MediaController(), time);
        var scanner = _scanner = new AirPodsScanner(time) { LogRawPackets = e.Args.Contains("--dump") };
        var tray = _tray = new TrayService();
        var popup = _popup = new PopupWindow();

        ApplySettings(settings.Current);
        settings.Changed += s => RunOnUi(() => ApplySettings(s));

        tray.ShowRequested += () => popup.ShowFor(store.Current?.Advertisement);
        tray.SettingsRequested += ShowSettings;
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
            if (settings.Current.AutoPause)
                autoPause.OnEarState(state.Advertisement.LeftInEar, state.Advertisement.RightInEar);
            OnUi(RefreshTooltip);
            EvaluateLowBattery(state);
            Log.Info($"Gerät: {AirPodsModels.DisplayName(state.Advertisement.Model)} (0x{state.Advertisement.ModelId:X4}), {state.Rssi} dBm");
        };
        store.StateChanged += state =>
        {
            EvaluateLowBattery(state);
            OnUi(() =>
            {
                RefreshTooltip();
                popup.RefreshIfVisible(store.Current?.Advertisement);
            });
        };
        store.LidOpened += state =>
        {
            if (settings.Current.PopupOnLidOpen) OnUi(() => popup.ShowFor(state.Advertisement));
        };
        store.EarStateChanged += (state, _) =>
        {
            if (settings.Current.AutoPause)
                autoPause.OnEarState(state.Advertisement.LeftInEar, state.Advertisement.RightInEar);
        };
        store.DeviceLost += () =>
        {
            autoPause.Reset();
            OnUi(() =>
            {
                _lowBattery.Reset();
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

    private void EvaluateLowBattery(AirPodsState state)
    {
        // Die Policy ist nicht thread-sicher und wird bei Schwellenänderung ersetzt; Events kommen vom
        // Scanner-Thread, daher alles auf dem UI-Thread. Bei ausgeschalteter Warnung wird nicht bewertet,
        // damit nach dem Wiedereinschalten ein bereits niedriger Stand noch gemeldet wird.
        OnUi(() =>
        {
            if (_settings?.Current.LowBatteryWarning != true) return;
            foreach (var alert in _lowBattery.Evaluate(state.Advertisement))
            {
                if (!FullscreenDetector.IsFullscreenForeground()) ToastNotifier.ShowLowBattery(alert);
            }
        });
    }

    /// <summary>Wendet die Einstellungen an; nur auf dem UI-Thread aufrufen.</summary>
    private void ApplySettings(AppSettings s)
    {
        var previous = _appliedSettings;
        _appliedSettings = s;

        if (_popup is { } popup) popup.AutoHideDelay = TimeSpan.FromSeconds(s.PopupSeconds);
        if (previous is null || previous.LowBatteryThreshold != s.LowBatteryThreshold)
            _lowBattery = new LowBatteryPolicy(s.LowBatteryThreshold);
        if (previous is { AutoPause: true } && !s.AutoPause) _autoPause?.Reset();
        if (previous is not null && previous.Theme != s.Theme) ApplyTheme();
    }

    private void ShowSettings()
    {
        if (_settings is not { } settings) return;
        if (_settingsWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        var window = new SettingsWindow(settings);
        window.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = window;
        window.Show();
        window.Activate();
    }

    private void ApplyTheme()
    {
        var light = _settings?.Current.Theme switch
        {
            ThemeMode.Light => true,
            ThemeMode.Dark => false,
            _ => WindowsTheme.IsLight(),
        };
        var uri = new Uri(light ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative);
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
    }

    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        OnUi(() =>
        {
            if (_settings?.Current.Theme == ThemeMode.System) ApplyTheme();
        });
    }

    private void RefreshTooltip() =>
        _tray?.SetTooltip(StatusText.Tooltip(_store?.Current?.Advertisement, _scannerStatus));

    private void OnUi(Action action) => Dispatcher.BeginInvoke(action);

    private void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.BeginInvoke(action);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unbehandelte UI-Ausnahme", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _scanner?.Dispose();
        _store?.Dispose();
        _autoPause?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Log.Info("VuloPods beendet");
        base.OnExit(e);
    }
}
