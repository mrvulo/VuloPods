using Microsoft.Win32;
using VuloPods.Core.Logging;

namespace VuloPods.Core.System;

/// <summary>Autostart über HKCU\...\Run. Fehler werden geloggt, nie geworfen.</summary>
public static class AutostartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VuloPods";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            Log.Error("Autostart-Status konnte nicht gelesen werden", ex);
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                {
                    Log.Error("Autostart: Prozesspfad unbekannt");
                    return;
                }
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Autostart konnte nicht geändert werden", ex);
        }
    }
}