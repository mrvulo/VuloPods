using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VuloPods.Core.Logging;
using VuloPods.Core.Platform;
using VuloPods.Core.Settings;

namespace VuloPods.App;

public partial class SettingsWindow : Window
{
    private const string GitHubUrl = "https://github.com/mrvulo/VuloPods";

    private static readonly int[] Thresholds = [10, 15, 20, 25, 30, 35, 40, 45, 50];
    private static readonly string[] TrayIconLabels = ["Logo", "Akku in Prozent"];
    private static readonly string[] ThemeLabels = ["Wie Windows", "Hell", "Dunkel"];

    private readonly SettingsStore _settings;
    private bool _loading = true;

    public SettingsWindow(SettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();

        ThresholdBox.ItemsSource = Thresholds.Select(t => $"{t} %").ToArray();
        TrayIconBox.ItemsSource = TrayIconLabels;
        ThemeBox.ItemsSource = ThemeLabels;
        VersionText.Text = $"Version {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)}";

        var s = settings.Current;
        AutostartBox.IsChecked = AutostartManager.IsEnabled();
        PopupOnLidOpenBox.IsChecked = s.PopupOnLidOpen;
        PopupSecondsSlider.Value = s.PopupSeconds;
        PopupSecondsText.Text = $"{s.PopupSeconds} s";
        AutoPauseBox.IsChecked = s.AutoPause;
        LowBatteryBox.IsChecked = s.LowBatteryWarning;
        ThresholdBox.SelectedIndex = Math.Max(0, Array.IndexOf(Thresholds, s.LowBatteryThreshold));
        TrayIconBox.SelectedIndex = (int)s.TrayIcon;
        ThemeBox.SelectedIndex = (int)s.Theme;
        _loading = false;
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AutostartManager.SetEnabled(AutostartBox.IsChecked == true);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e) => SaveFromControls();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SaveFromControls();

    private void OnPopupSecondsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PopupSecondsText is null) return; // Slider feuert während InitializeComponent.
        PopupSecondsText.Text = $"{(int)e.NewValue} s";
        SaveFromControls();
    }

    private void SaveFromControls()
    {
        if (_loading) return;
        try
        {
            _settings.Save(_settings.Current with
            {
                PopupOnLidOpen = PopupOnLidOpenBox.IsChecked == true,
                PopupSeconds = (int)PopupSecondsSlider.Value,
                AutoPause = AutoPauseBox.IsChecked == true,
                LowBatteryWarning = LowBatteryBox.IsChecked == true,
                LowBatteryThreshold = Thresholds[Math.Max(0, ThresholdBox.SelectedIndex)],
                TrayIcon = (TrayIconStyle)Math.Max(0, TrayIconBox.SelectedIndex),
                Theme = (ThemeMode)Math.Max(0, ThemeBox.SelectedIndex),
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Einstellungen konnten nicht gespeichert werden", ex);
        }
    }

    private void OnGitHubClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("GitHub-Link konnte nicht geöffnet werden", ex);
        }
    }
}
