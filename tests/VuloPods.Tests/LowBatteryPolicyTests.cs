using VuloPods.Core.Battery;
using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class LowBatteryPolicyTests
{
    private static AirPodsAdvertisement Adv(
        int? left = 80, int? right = 80, int? caseBattery = 80,
        bool leftCharging = false, bool rightCharging = false, bool caseCharging = false) =>
        new(0x1320, AirPodsModel.AirPods3, left, right, caseBattery,
            leftCharging, rightCharging, caseCharging,
            false, false, false, false, 0);

    [Fact]
    public void FirstEvaluationBelowThreshold_Alerts()
    {
        var policy = new LowBatteryPolicy();

        var alerts = policy.Evaluate(Adv(left: 20));

        var alert = Assert.Single(alerts);
        Assert.Equal(new LowBatteryAlert(BatteryPart.Left, 20), alert);
    }

    [Fact]
    public void SameValueAgain_DoesNotAlertTwice()
    {
        var policy = new LowBatteryPolicy();
        policy.Evaluate(Adv(right: 10));

        Assert.Empty(policy.Evaluate(Adv(right: 10)));
        Assert.Empty(policy.Evaluate(Adv(right: 10)));
    }

    [Fact]
    public void Charging_DoesNotAlert()
    {
        var policy = new LowBatteryPolicy();

        Assert.Empty(policy.Evaluate(Adv(left: 10, leftCharging: true)));
    }

    [Fact]
    public void AboveThresholdThenBelow_AlertsAgain()
    {
        var policy = new LowBatteryPolicy();
        Assert.Single(policy.Evaluate(Adv(caseBattery: 10)));
        Assert.Empty(policy.Evaluate(Adv(caseBattery: 10)));
        Assert.Empty(policy.Evaluate(Adv(caseBattery: 50)));

        var alerts = policy.Evaluate(Adv(caseBattery: 20));

        Assert.Equal(new LowBatteryAlert(BatteryPart.Case, 20), Assert.Single(alerts));
    }

    [Fact]
    public void NullValue_DoesNotAlertAndDoesNotCountAsAbove()
    {
        var policy = new LowBatteryPolicy();
        Assert.Single(policy.Evaluate(Adv(left: 10)));

        Assert.Empty(policy.Evaluate(Adv(left: null)));
        // null darf die Hysterese nicht zurücksetzen.
        Assert.Empty(policy.Evaluate(Adv(left: 10)));
    }

    [Fact]
    public void Reset_AlertsAgainWhenStillBelow()
    {
        var policy = new LowBatteryPolicy();
        Assert.Single(policy.Evaluate(Adv(right: 10)));
        policy.Reset();

        var alerts = policy.Evaluate(Adv(right: 10));

        Assert.Equal(new LowBatteryAlert(BatteryPart.Right, 10), Assert.Single(alerts));
    }

    [Fact]
    public void MultipleParts_AlertInOneCall()
    {
        var policy = new LowBatteryPolicy();

        var alerts = policy.Evaluate(Adv(left: 10, caseBattery: 20));

        Assert.Equal(2, alerts.Count);
        Assert.Contains(new LowBatteryAlert(BatteryPart.Left, 10), alerts);
        Assert.Contains(new LowBatteryAlert(BatteryPart.Case, 20), alerts);
    }

    [Fact]
    public void ChargingPart_DoesNotBlockLaterAlertAfterUnplugging()
    {
        var policy = new LowBatteryPolicy();
        Assert.Empty(policy.Evaluate(Adv(left: 10, leftCharging: true)));

        Assert.Single(policy.Evaluate(Adv(left: 10)));
    }
}