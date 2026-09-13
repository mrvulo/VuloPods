using Microsoft.Extensions.Time.Testing;
using VuloPods.Core.Bluetooth;
using VuloPods.Core.Models;

namespace VuloPods.Tests;

public class AirPodsStateStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero));

    private static AirPodsAdvertisement Adv(ushort model = 0x1420, int left = 80, int right = 80, bool lidOpen = false, int lidCounter = 0, bool leftInEar = false, bool rightInEar = false) =>
        new(model, AirPodsModels.FromId(model), left, right, 50, false, false, false, leftInEar, rightInEar, !leftInEar && !rightInEar, lidOpen, lidCounter);

    private AdvertisementReceived Rx(AirPodsAdvertisement adv, short rssi = -50) => new(adv, rssi, _time.GetUtcNow());

    [Fact]
    public void FirstPacket_BecomesCurrent_AndRaisesDeviceAppeared()
    {
        using var store = new AirPodsStateStore(_time);
        AirPodsState? appeared = null;
        store.DeviceAppeared += s => appeared = s;

        store.Push(Rx(Adv()));

        Assert.NotNull(store.Current);
        Assert.NotNull(appeared);
        Assert.Equal(0x1420, appeared.Advertisement.ModelId);
    }

    [Fact]
    public void PacketBelowThreshold_IsIgnored_WhenNoDevice()
    {
        using var store = new AirPodsStateStore(_time);
        store.Push(Rx(Adv(), rssi: -80));
        Assert.Null(store.Current);
    }

    [Fact]
    public void WeakPacketFromCurrentDevice_StillUpdates()
    {
        using var store = new AirPodsStateStore(_time);
        store.Push(Rx(Adv(left: 80)));
        store.Push(Rx(Adv(left: 70), rssi: -90));
        Assert.Equal(70, store.Current!.Advertisement.LeftBattery);
    }

    [Fact]
    public void StateChanged_OnlyWhenAdvertisementDiffers()
    {
        using var store = new AirPodsStateStore(_time);
        var changes = 0;
        store.StateChanged += _ => changes++;

        store.Push(Rx(Adv(left: 80)));
        store.Push(Rx(Adv(left: 80)));
        store.Push(Rx(Adv(left: 70)));

        Assert.Equal(1, changes);
    }

    [Fact]
    public void LidOpened_FiresOnce_PerOpening()
    {
        using var store = new AirPodsStateStore(_time);
        var opened = 0;
        store.LidOpened += _ => opened++;

        store.Push(Rx(Adv(lidOpen: false, lidCounter: 2)));
        store.Push(Rx(Adv(lidOpen: true, lidCounter: 3)));
        store.Push(Rx(Adv(lidOpen: true, lidCounter: 3)));
        store.Push(Rx(Adv(lidOpen: false, lidCounter: 4)));
        store.Push(Rx(Adv(lidOpen: true, lidCounter: 5)));

        Assert.Equal(2, opened);
    }

    [Fact]
    public void LidOpened_FiresAgain_WhenCounterChangesWhileOpen()
    {
        using var store = new AirPodsStateStore(_time);
        var opened = 0;
        store.LidOpened += _ => opened++;

        store.Push(Rx(Adv(lidOpen: true, lidCounter: 1)));
        store.Push(Rx(Adv(lidOpen: true, lidCounter: 3)));

        Assert.Equal(1, opened);
    }

    [Fact]
    public void EarStateChanged_PassesPreviousAdvertisement()
    {
        using var store = new AirPodsStateStore(_time);
        AirPodsAdvertisement? previous = null;
        AirPodsState? next = null;
        store.EarStateChanged += (s, p) => { next = s; previous = p; };

        store.Push(Rx(Adv(leftInEar: true, rightInEar: true)));
        store.Push(Rx(Adv(leftInEar: false, rightInEar: true)));

        Assert.NotNull(previous);
        Assert.True(previous.LeftInEar);
        Assert.False(next!.Advertisement.LeftInEar);
    }

    [Fact]
    public void OtherModel_TakesOver_AfterFiveStrongerPackets()
    {
        using var store = new AirPodsStateStore(_time);
        var appeared = new List<ushort>();
        store.DeviceAppeared += s => appeared.Add(s.Advertisement.ModelId);

        store.Push(Rx(Adv(model: 0x1420), rssi: -60));
        for (var i = 0; i < 4; i++) store.Push(Rx(Adv(model: 0x0E20), rssi: -45));
        Assert.Equal(0x1420, store.Current!.Advertisement.ModelId);

        store.Push(Rx(Adv(model: 0x0E20), rssi: -45));
        Assert.Equal(0x0E20, store.Current!.Advertisement.ModelId);
        Assert.Equal(new ushort[] { 0x1420, 0x0E20 }, appeared);
    }

    [Fact]
    public void OtherModel_CounterResets_OnWeakerPacket()
    {
        using var store = new AirPodsStateStore(_time);
        store.Push(Rx(Adv(model: 0x1420), rssi: -60));
        for (var i = 0; i < 4; i++) store.Push(Rx(Adv(model: 0x0E20), rssi: -45));
        store.Push(Rx(Adv(model: 0x0E20), rssi: -55)); // nur 5 dB stärker → zählt nicht, setzt zurück
        for (var i = 0; i < 4; i++) store.Push(Rx(Adv(model: 0x0E20), rssi: -45));

        Assert.Equal(0x1420, store.Current!.Advertisement.ModelId);
    }

    [Fact]
    public void OtherModel_NotStronger_IsIgnored()
    {
        using var store = new AirPodsStateStore(_time);
        store.Push(Rx(Adv(model: 0x1420), rssi: -60));
        for (var i = 0; i < 10; i++) store.Push(Rx(Adv(model: 0x0E20), rssi: -60));
        Assert.Equal(0x1420, store.Current!.Advertisement.ModelId);
    }

    [Fact]
    public void DeviceLost_After30SecondsWithoutPackets()
    {
        using var store = new AirPodsStateStore(_time);
        var lost = 0;
        store.DeviceLost += () => lost++;

        store.Push(Rx(Adv()));
        _time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, lost);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(1, lost);
        Assert.Null(store.Current);
    }

    [Fact]
    public void DeviceNotLost_WhilePacketsKeepComing()
    {
        using var store = new AirPodsStateStore(_time);
        var lost = 0;
        store.DeviceLost += () => lost++;

        for (var i = 0; i < 6; i++)
        {
            store.Push(Rx(Adv()));
            _time.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(0, lost);
        Assert.NotNull(store.Current);
    }
}
