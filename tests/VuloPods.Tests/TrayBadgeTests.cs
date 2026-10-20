using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class TrayBadgeTests
{
    private static AirPodsAdvertisement Adv(
        int? left = null, int? right = null, int? caseBattery = null,
        bool leftCharging = false, bool rightCharging = false, bool caseCharging = false) =>
        new(0x1320, AirPodsModel.AirPods3, left, right, caseBattery,
            leftCharging, rightCharging, caseCharging,
            false, false, false, false, 0);

    [Fact]
    public void UsesMinimumOfLeftAndRight()
    {
        var badge = TrayBadges.Compute(Adv(left: 80, right: 60), 20);

        Assert.Equal(new TrayBadge("60", BadgeLevel.Normal), badge);
    }

    [Fact]
    public void OnlyCaseKnown_UsesCase()
    {
        var badge = TrayBadges.Compute(Adv(caseBattery: 40), 20);

        Assert.Equal(new TrayBadge("40", BadgeLevel.Normal), badge);
    }

    [Fact]
    public void EverythingUnknown_ReturnsNull()
    {
        Assert.Null(TrayBadges.Compute(Adv(), 20));
    }

    [Fact]
    public void NullAdvertisement_ReturnsNull()
    {
        Assert.Null(TrayBadges.Compute(null, 20));
    }

    [Fact]
    public void ValueEqualToThreshold_IsLow()
    {
        var badge = TrayBadges.Compute(Adv(left: 20, right: 50), 20);

        Assert.Equal(new TrayBadge("20", BadgeLevel.Low), badge);
    }

    [Fact]
    public void Charging_TakesPrecedenceOverLow()
    {
        var badge = TrayBadges.Compute(Adv(left: 10, right: 50, leftCharging: true), 20);

        Assert.Equal(new TrayBadge("10", BadgeLevel.Charging), badge);
    }

    [Fact]
    public void FullBattery_KeepsThreeDigits()
    {
        var badge = TrayBadges.Compute(Adv(left: 100, right: 100), 20);

        Assert.Equal(new TrayBadge("100", BadgeLevel.Normal), badge);
    }

    [Fact]
    public void OneMinimumPodCharging_IsCharging()
    {
        var badge = TrayBadges.Compute(Adv(left: 50, right: 50, leftCharging: true), 20);

        Assert.Equal(new TrayBadge("50", BadgeLevel.Charging), badge);
    }

    [Fact]
    public void ChargingNonMinimumPod_DoesNotCount()
    {
        var badge = TrayBadges.Compute(Adv(left: 80, right: 60, leftCharging: true), 20);

        Assert.Equal(new TrayBadge("60", BadgeLevel.Normal), badge);
    }

    [Fact]
    public void CaseChargingIsIgnoredWhenPodsKnown()
    {
        var badge = TrayBadges.Compute(Adv(left: 60, right: 70, caseCharging: true), 20);

        Assert.Equal(new TrayBadge("60", BadgeLevel.Normal), badge);
    }
}
