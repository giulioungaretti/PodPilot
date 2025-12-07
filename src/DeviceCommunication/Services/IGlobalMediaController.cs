namespace DeviceCommunication.Services;

/// <summary>
/// Provides control over the system's global media playback.
/// </summary>
public interface IGlobalMediaController : IDisposable
{
    /// <summary>
    /// Gets whether the media controller is initialized and ready to use.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets whether there is currently a media session available.
    /// </summary>
    bool HasActiveSession { get; }

    /// <summary>
    /// Gets whether media is currently playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Gets whether media is currently paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Initializes the media controller asynchronously.
    /// Must be called before using Pause/Play methods.
    /// </summary>
    /// <returns>True if initialization was successful; otherwise, false.</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Pauses the current media playback.
    /// </summary>
    /// <returns>True if pause was successful; otherwise, false.</returns>
    Task<bool> PauseAsync();

    /// <summary>
    /// Resumes/plays the current media playback.
    /// </summary>
    /// <returns>True if play was successful; otherwise, false.</returns>
    Task<bool> PlayAsync();
}
