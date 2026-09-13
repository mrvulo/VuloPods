using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace VuloPods.App;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        var menu = new ContextMenu();
        var show = new MenuItem { Header = "Anzeigen" };
        show.Click += (_, _) => ShowRequested?.Invoke();
        var exit = new MenuItem { Header = "Beenden" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(show);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/tray.ico")),
            ToolTipText = "VuloPods – kein Gerät",
            ContextMenu = menu,
            LeftClickCommand = new DelegateCommand(() => ShowRequested?.Invoke()),
        };
        _icon.ForceCreate();
    }

    public void SetTooltip(string text) => _icon.ToolTipText = text;

    public void Dispose() => _icon.Dispose();
}
