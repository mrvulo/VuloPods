using Windows.Media.Control;

namespace VuloPods.Core.Media;

/// <summary>Steuert die aktuelle Windows-Mediensitzung (Spotify, Browser, Groove ...).</summary>
public sealed class MediaController : IMediaController
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    private async Task<GlobalSystemMediaTransportControlsSession?> GetCurrentSessionAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return _manager.GetCurrentSession();
    }

    public async Task<bool> PauseIfPlayingAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is null) return false;
        var status = session.GetPlaybackInfo().PlaybackStatus;
        if (status != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) return false;
        return await session.TryPauseAsync();
    }

    public async Task ResumeAsync()
    {
        var session = await GetCurrentSessionAsync();
        if (session is null) return;
        await session.TryPlayAsync();
    }
}
