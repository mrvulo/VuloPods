using VuloPods.Core.Models;

namespace VuloPods.Core.Bluetooth;

public sealed class AirPodsStateStore : IDisposable
{
    public const short MinRssi = -75;
    public const short TakeoverMarginDb = 10;
    public const int TakeoverPackets = 5;
    public static readonly TimeSpan LostTimeout = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly ITimer _timeoutTimer;
    private ushort _challengerModelId;
    private int _challengerCount;

    public AirPodsState? Current { get; private set; }

    public event Action<AirPodsState>? DeviceAppeared;
    public event Action<AirPodsState>? StateChanged;
    public event Action<AirPodsState>? LidOpened;
    public event Action<AirPodsState, AirPodsAdvertisement>? EarStateChanged;
    public event Action? DeviceLost;

    public AirPodsStateStore(TimeProvider time)
    {
        _time = time;
        _timeoutTimer = time.CreateTimer(_ => CheckTimeout(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Push(AdvertisementReceived received)
    {
        AirPodsState? appeared = null;
        AirPodsState? changed = null;
        AirPodsState? lidOpened = null;
        (AirPodsState Next, AirPodsAdvertisement Previous)? ear = null;

        lock (_gate)
        {
            var adv = received.Advertisement;
            var next = new AirPodsState(adv, received.Rssi, received.Timestamp);
            var current = Current;

            if (current is null)
            {
                if (received.Rssi < MinRssi) return;
                Current = next;
                appeared = next;
            }
            else if (current.Advertisement.ModelId == adv.ModelId)
            {
                Current = next;
                var prev = current.Advertisement;
                if (adv != prev)
                {
                    changed = next;
                    var lidJustOpened = adv.LidOpen && !prev.LidOpen;
                    var counterChangedWhileOpen = adv.LidOpen && adv.LidOpenCounter != prev.LidOpenCounter;
                    if (lidJustOpened || counterChangedWhileOpen) lidOpened = next;
                    if (adv.LeftInEar != prev.LeftInEar || adv.RightInEar != prev.RightInEar) ear = (next, prev);
                }
            }
            else
            {
                if (received.Rssi >= MinRssi && received.Rssi >= current.Rssi + TakeoverMarginDb)
                {
                    if (_challengerModelId != adv.ModelId)
                    {
                        _challengerModelId = adv.ModelId;
                        _challengerCount = 0;
                    }
                    _challengerCount++;
                    if (_challengerCount >= TakeoverPackets)
                    {
                        _challengerCount = 0;
                        Current = next;
                        appeared = next;
                    }
                }
                else
                {
                    _challengerCount = 0;
                }
            }
        }

        if (appeared is not null) DeviceAppeared?.Invoke(appeared);
        if (changed is not null) StateChanged?.Invoke(changed);
        if (lidOpened is not null) LidOpened?.Invoke(lidOpened);
        if (ear is not null) EarStateChanged?.Invoke(ear.Value.Next, ear.Value.Previous);
    }

    private void CheckTimeout()
    {
        var lost = false;
        lock (_gate)
        {
            if (Current is not null && _time.GetUtcNow() - Current.LastSeen >= LostTimeout)
            {
                Current = null;
                _challengerCount = 0;
                lost = true;
            }
        }
        if (lost) DeviceLost?.Invoke();
    }

    public void Dispose() => _timeoutTimer.Dispose();
}
