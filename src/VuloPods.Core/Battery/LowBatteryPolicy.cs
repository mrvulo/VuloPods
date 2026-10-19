using VuloPods.Core.Models;

namespace VuloPods.Core.Battery;

public enum BatteryPart { Left, Right, Case }

public sealed record LowBatteryAlert(BatteryPart Part, int Percent);

/// <summary>
/// Entscheidet, wann eine Akku-Warnung ausgelöst wird (reine Logik). Pro Teil wird nur gewarnt,
/// wenn der Wert bekannt, &lt;= Schwelle und nicht ladend ist und das Teil zuvor über der Schwelle
/// lag oder noch nie bewertet wurde. Danach ist es bis zur nächsten Messung über der Schwelle stumm.
/// </summary>
public sealed class LowBatteryPolicy
{
    public const int Threshold = 20;

    private readonly bool[] _alerted = new bool[3];

    public IReadOnlyList<LowBatteryAlert> Evaluate(AirPodsAdvertisement adv)
    {
        var alerts = new List<LowBatteryAlert>();
        Check(BatteryPart.Left, adv.LeftBattery, adv.LeftCharging, alerts);
        Check(BatteryPart.Right, adv.RightBattery, adv.RightCharging, alerts);
        Check(BatteryPart.Case, adv.CaseBattery, adv.CaseCharging, alerts);
        return alerts;
    }

    public void Reset() => Array.Clear(_alerted);

    private void Check(BatteryPart part, int? value, bool charging, List<LowBatteryAlert> alerts)
    {
        if (value is not { } percent) return; // unbekannt: weder Warnung noch "über Schwelle"

        var index = (int)part;
        if (percent > Threshold)
        {
            _alerted[index] = false;
            return;
        }

        if (charging || _alerted[index]) return;

        _alerted[index] = true;
        alerts.Add(new LowBatteryAlert(part, percent));
    }
}