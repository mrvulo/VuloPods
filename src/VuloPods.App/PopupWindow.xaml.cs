using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VuloPods.App.Interop;
using VuloPods.Core.Logging;
using VuloPods.Core.Models;

namespace VuloPods.App;

public partial class PopupWindow : Window
{
    public static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(5);
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(250));

    private readonly PopupViewModel _viewModel = new();
    private readonly DispatcherTimer _autoHide;
    private MouseHook? _mouseHook;

    public PopupWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _autoHide = new DispatcherTimer { Interval = AutoHideDelay };
        _autoHide.Tick += (_, _) => FadeOut();
    }

    public void ShowFor(AirPodsAdvertisement? adv)
    {
        _viewModel.Update(adv);
        PositionBottomRight();
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Show();
        EnsureMouseHook();
        _autoHide.Stop();
        _autoHide.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Kein Fokusklau und kein Alt-Tab-Eintrag.
        WindowStyles.MakeNoActivateToolWindow(new WindowInteropHelper(this).Handle);
    }

    private void EnsureMouseHook()
    {
        if (_mouseHook is not null) return;
        try
        {
            _mouseHook = new MouseHook();
            _mouseHook.ButtonDown += OnGlobalButtonDown;
        }
        catch (Exception ex)
        {
            Log.Error("Maus-Hook konnte nicht installiert werden", ex);
        }
    }

    private void OnGlobalButtonDown(int x, int y)
    {
        // Läuft im Hook-Callback: nur schnell prüfen, das Verstecken asynchron auf den Dispatcher legen.
        if (!IsVisible) return;
        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        var inside = x >= topLeft.X && x <= bottomRight.X && y >= topLeft.Y && y <= bottomRight.Y;
        if (!inside) Dispatcher.BeginInvoke(HideNow);
    }

    public void RefreshIfVisible(AirPodsAdvertisement? adv)
    {
        if (IsVisible) _viewModel.Update(adv);
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 12;
        Top = area.Bottom - Height - 12;
    }

    private void FadeOut()
    {
        _autoHide.Stop();
        var fade = new DoubleAnimation(1, 0, FadeDuration);
        fade.Completed += (_, _) => HideNow();
        BeginAnimation(OpacityProperty, fade);
    }

    private void HideNow()
    {
        _autoHide.Stop();
        _mouseHook?.Dispose();
        _mouseHook = null;
        BeginAnimation(OpacityProperty, null);
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Das Fenster wird nur versteckt, nie geschlossen; die App beendet sich über das Tray-Menü.
        e.Cancel = true;
        HideNow();
    }
}
