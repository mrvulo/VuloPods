using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace VuloPods.App;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public event Action? ShowRequested;
    public event Action? SettingsRequested;
    public event Action? ExportDiagnosticsRequested;
    public event Action? ExitRequested;

    /// <summary>Das Kontextmenü; spätere Erweiterungen fügen Einträge nach „Anzeigen“ (Index 0) ein.</summary>
    public ContextMenu Menu { get; }

    public TrayService()
    {
        Menu = BuildMenu();

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray.ico")),
            ToolTipText = "VuloPods – kein Gerät",
            ContextMenu = Menu,
            LeftClickCommand = new DelegateCommand(() => ShowRequested?.Invoke()),
            DoubleClickCommand = new DelegateCommand(() => SettingsRequested?.Invoke()),
        };
        _icon.ForceCreate();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var show = new MenuItem { Header = "Anzeigen" };
        show.Click += (_, _) => ShowRequested?.Invoke();
        var settings = new MenuItem { Header = "Einstellungen…" };
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        var export = new MenuItem { Header = "Diagnose exportieren…" };
        export.Click += (_, _) => ExportDiagnosticsRequested?.Invoke();
        var exit = new MenuItem { Header = "Beenden" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(show);
        menu.Items.Add(new Separator());
        menu.Items.Add(settings);
        menu.Items.Add(export);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        return menu;
    }

    public void SetTooltip(string text) => _icon.ToolTipText = text;

    public void Dispose()
    {
        _icon.Dispose();
    }
}
