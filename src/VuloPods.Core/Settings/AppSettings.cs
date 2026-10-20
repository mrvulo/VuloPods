namespace VuloPods.Core.Settings;

public enum TrayIconStyle { Logo, Percent }

public enum ThemeMode { System, Light, Dark }

/// <summary>Persistierte Benutzereinstellungen. Fehlende Felder in der JSON-Datei behalten ihren Default.</summary>
public sealed record AppSettings
{
    public bool PopupOnLidOpen { get; init; } = true;
    public int PopupSeconds { get; init; } = 5;          // 3..15
    public bool AutoPause { get; init; } = true;
    public bool LowBatteryWarning { get; init; } = true;
    public int LowBatteryThreshold { get; init; } = 20;  // 10..50, Schritt 5
    public TrayIconStyle TrayIcon { get; init; } = TrayIconStyle.Percent;
    public ThemeMode Theme { get; init; } = ThemeMode.System;

    /// <summary>Klemmt PopupSeconds auf 3..15 und die Schwelle auf 10..50 (gerundet auf 5er-Schritte).</summary>
    public AppSettings Normalized() => this with
    {
        PopupSeconds = Math.Clamp(PopupSeconds, 3, 15),
        LowBatteryThreshold = (int)(Math.Round(Math.Clamp(LowBatteryThreshold, 10, 50) / 5.0,
            MidpointRounding.AwayFromZero) * 5),
    };
}
