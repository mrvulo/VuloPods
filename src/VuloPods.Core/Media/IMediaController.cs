namespace VuloPods.Core.Media;

public interface IMediaController
{
    /// <summary>Pausiert die aktive Mediensitzung, falls sie gerade spielt. True, wenn tatsächlich pausiert wurde.</summary>
    Task<bool> PauseIfPlayingAsync();

    /// <summary>Setzt die aktive Mediensitzung fort.</summary>
    Task ResumeAsync();
}
