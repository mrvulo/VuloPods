using System.Windows;
using VuloPods.Core.Models;

namespace VuloPods.App;

public partial class PopupWindow : Window
{
    public PopupWindow() => InitializeComponent();
    public void ShowFor(AirPodsAdvertisement? adv) { }
    public void RefreshIfVisible(AirPodsAdvertisement? adv) { }
}
