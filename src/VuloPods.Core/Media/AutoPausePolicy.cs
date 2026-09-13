using VuloPods.Core.Logging;

namespace VuloPods.Core.Media;

public sealed class AutoPausePolicy : IDisposable
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private enum PendingAction { None, Pause, Resume }

    private readonly object _gate = new();
    private readonly IMediaController _media;
    private readonly ITimer _timer;
    private int? _wornCount;
    private PendingAction _pending;
    private bool _wePaused;

    public AutoPausePolicy(IMediaController media, TimeProvider time)
    {
        _media = media;
        _timer = time.CreateTimer(_ => Fire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void OnEarState(bool leftInEar, bool rightInEar)
    {
        var worn = (leftInEar ? 1 : 0) + (rightInEar ? 1 : 0);
        lock (_gate)
        {
            if (_wornCount is null)
            {
                _wornCount = worn;
                return;
            }
            if (worn == _wornCount) return;

            _pending = worn < _wornCount ? PendingAction.Pause : PendingAction.Resume;
            _wornCount = worn;
            _timer.Change(Debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private async void Fire()
    {
        PendingAction action;
        lock (_gate)
        {
            action = _pending;
            _pending = PendingAction.None;
        }

        try
        {
            switch (action)
            {
                case PendingAction.Pause:
                    if (await _media.PauseIfPlayingAsync()) _wePaused = true;
                    break;
                case PendingAction.Resume:
                    if (_wePaused)
                    {
                        _wePaused = false;
                        await _media.ResumeAsync();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Auto-Pause fehlgeschlagen", ex);
        }
    }

    public void Dispose() => _timer.Dispose();
}
