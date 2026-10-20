using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using VuloPods.Core.Platform;

namespace VuloPods.App;

public sealed class TrayService : IDisposable
{
    private static readonly Uri LogoUri = new("pack://application:,,,/Assets/tray.ico");

    private readonly TaskbarIcon _icon;
    private System.Drawing.Icon? _logo;
    private System.Drawing.Icon? _custom;
    private MenuItem? _showItem;
    private readonly List<Control> _connectItems = new();

    public event Action? ShowRequested;
    public event Action? SettingsRequested;
    public event Action? ExportDiagnosticsRequested;
    public event Action? ExitRequested;

    /// <summary>Wird ausgelöst, wenn das Kontextmenü geöffnet wird (UI-Thread).</summary>
    public event Action? MenuOpening;

    /// <summary>Das Kontextmenü; spätere Erweiterungen fügen Einträge nach „Anzeigen“ (Index 0) ein.</summary>
    public ContextMenu Menu { get; }

    public TrayService()
    {
        Menu = BuildMenu();
        Menu.Opened += (_, _) => MenuOpening?.Invoke();

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(LogoUri),
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
        var show = _showItem = new MenuItem { Header = "Anzeigen" };
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

    /// <summary>Zeigt während der Gerätesuche einen deaktivierten Platzhalter.</summary>
    public void ShowSearching() => ReplaceConnectItems(new Control[] { Disabled("Suche Geräte…") });

    /// <summary>Ersetzt den Verbinden/Trennen-Block direkt nach „Anzeigen“; nur auf dem UI-Thread aufrufen.</summary>
    public void SetConnectItems(IReadOnlyList<PairedAudioDevice> devices, bool bluetoothOff, Action<PairedAudioDevice> onToggle)
    {
        Control[] items;
        if (bluetoothOff)
        {
            items = new Control[] { Disabled("Bluetooth ist ausgeschaltet") };
        }
        else if (devices.Count == 0)
        {
            items = new Control[] { Disabled("Keine gekoppelten AirPods") };
        }
        else if (devices.Count == 1)
        {
            var d = devices[0];
            var item = new MenuItem { Header = d.IsConnected ? $"{d.Name} trennen" : $"{d.Name} verbinden" };
            item.Click += (_, _) => onToggle(d);
            items = new Control[] { item };
        }
        else
        {
            var sub = new MenuItem { Header = "Verbinden / Trennen" };
            foreach (var d in devices)
            {
                var device = d;
                var item = new MenuItem { Header = device.Name, IsChecked = device.IsConnected };
                item.Click += (_, _) => onToggle(device);
                sub.Items.Add(item);
            }

            items = new Control[] { sub };
        }

        ReplaceConnectItems(items);
    }

    private static MenuItem Disabled(string header) => new() { Header = header, IsEnabled = false };

    private void ReplaceConnectItems(IEnumerable<Control> items)
    {
        foreach (var old in _connectItems) Menu.Items.Remove(old);
        _connectItems.Clear();

        var index = _showItem is null ? 0 : Menu.Items.IndexOf(_showItem) + 1;
        foreach (var item in items)
        {
            Menu.Items.Insert(index++, item);
            _connectItems.Add(item);
        }
    }

    public void SetTooltip(string text) => _icon.ToolTipText = text;

    /// <summary>Setzt ein eigenes Icon; null stellt das Logo wieder her. Das vorherige eigene Icon wird freigegeben.</summary>
    public void SetIcon(System.Drawing.Icon? icon)
    {
        var previous = _custom;
        _custom = icon;
        _icon.Icon = icon ?? (_logo ??= LoadLogo());
        previous?.Dispose();
    }

    private static System.Drawing.Icon LoadLogo()
    {
        var size = TrayIconRenderer.GetSmallIconSize();
        var stream = Application.GetResourceStream(LogoUri)!.Stream;
        using (stream) return new System.Drawing.Icon(stream, size, size);
    }

    public void Dispose()
    {
        _icon.Dispose();
        _custom?.Dispose();
        _logo?.Dispose();
    }
}
