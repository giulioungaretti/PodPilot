namespace DeviceCommunication.Services;

/// <summary>
/// Event args containing information about a default audio output change.
/// </summary>
/// <param name="NewDeviceId">The device ID of the new default audio output device, or null if no device is set.</param>
public sealed record DefaultAudioOutputChangedEventArgs(string? NewDeviceId);

/// <summary>
/// Service that monitors changes to the default audio output device.
/// Uses Windows event-based detection instead of polling.
/// </summary>
public interface IDefaultAudioOutputMonitorService : IDisposable
{
    /// <summary>
    /// Occurs when the default audio output device changes.
    /// </summary>
    event EventHandler<DefaultAudioOutputChangedEventArgs>? DefaultAudioOutputChanged;

    /// <summary>
    /// Gets the current default audio output device ID, or null if no device is set.
    /// </summary>
    string? CurrentDefaultDeviceId { get; }

    /// <summary>
    /// Starts monitoring for default audio output changes.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring for default audio output changes.
    /// </summary>
    void Stop();
}
