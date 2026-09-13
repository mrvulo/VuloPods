using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VuloPods.Core.Models;

namespace VuloPods.App;

public partial class PopupWindow : Window
{
    public static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(5);
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(250));

    private readonly PopupViewModel _viewModel = new();
    private readonly DispatcherTimer _autoHide;

    public PopupWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _autoHide = new DispatcherTimer { Interval = AutoHideDelay };
        _autoHide.Tick += (_, _) => FadeOut();
        Deactivated += (_, _) => HideNow();
    }

    public void ShowFor(AirPodsAdvertisement? adv)
    {
        _viewModel.Update(adv);
        PositionBottomRight();
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Show();
        Activate();
        _autoHide.Stop();
        _autoHide.Start();
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
