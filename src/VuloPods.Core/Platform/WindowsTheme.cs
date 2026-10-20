using Microsoft.Win32;
using VuloPods.Core.Logging;

namespace VuloPods.Core.Platform;

/// <summary>Liest das Windows-App-Theme (hell/dunkel).</summary>
public static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>true = helles App-Theme. Fehlender Wert oder Fehler → false (dunkel).</summary>
    public static bool IsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex)
        {
            Log.Error("Windows-Theme konnte nicht gelesen werden", ex);
            return false;
        }
    }
}