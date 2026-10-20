using System.Globalization;

namespace VuloPods.Core.Models;

public enum BadgeLevel { Normal, Low, Charging }

/// <summary>Text (Zahl ohne Prozentzeichen) und Farbstufe für das Tray-Icon.</summary>
public sealed record TrayBadge(string Text, BadgeLevel Level);

public static class TrayBadges
{
    /// <summary>
    /// Wert = Minimum der bekannten Pod-Werte, sonst der Case-Wert; alles unbekannt oder adv null → null.
    /// Charging, wenn ein ausgewählter Teil lädt (bei Pods: einer der Pods mit Minimalwert), sonst Low bei Wert &lt;= Schwelle.
    /// </summary>
    public static TrayBadge? Compute(AirPodsAdvertisement? adv, int lowThreshold)
    {
        if (adv is null) return null;

        int value;
        bool charging;
        if (adv.LeftBattery is null && adv.RightBattery is null)
        {
            if (adv.CaseBattery is not { } caseValue) return null;
            value = caseValue;
            charging = adv.CaseCharging;
        }
        else
        {
            value = Math.Min(adv.LeftBattery ?? int.MaxValue, adv.RightBattery ?? int.MaxValue);
            charging = (adv.LeftBattery == value && adv.LeftCharging)
                       || (adv.RightBattery == value && adv.RightCharging);
        }

        var level = charging ? BadgeLevel.Charging
            : value <= lowThreshold ? BadgeLevel.Low
            : BadgeLevel.Normal;
        return new TrayBadge(value.ToString(CultureInfo.InvariantCulture), level);
    }
}
