using Microsoft.Toolkit.Uwp.Notifications;
using VuloPods.Core.Battery;
using VuloPods.Core.Logging;

namespace VuloPods.App.Notifications;

public static class ToastNotifier
{
    public static void ShowLowBattery(LowBatteryAlert alert)
    {
        try
        {
            var line = alert.Part switch
            {
                BatteryPart.Left => $"Linker AirPod: {alert.Percent} %",
                BatteryPart.Right => $"Rechter AirPod: {alert.Percent} %",
                _ => $"Ladecase: {alert.Percent} %",
            };
            new ToastContentBuilder().AddText("VuloPods").AddText(line).Show();
        }
        catch (Exception ex)
        {
            Log.Error("Toast konnte nicht angezeigt werden", ex);
        }
    }
}
