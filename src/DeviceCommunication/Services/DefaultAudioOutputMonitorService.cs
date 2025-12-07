using System.Diagnostics;
using Windows.Media.Devices;

namespace DeviceCommunication.Services;

/// <summary>
/// Monitors changes to the default audio output device using Windows event-based detection.
/// This replaces polling-based approaches for better performance and responsiveness.
/// </summary>
public sealed class DefaultAudioOutputMonitorService : IDefaultAudioOutputMonitorService
{
    private bool _isStarted;
    private bool _disposed;
    private string? _currentDefaultDeviceId;

    /// <inheritdoc />
    public event EventHandler<DefaultAudioOutputChangedEventArgs>? DefaultAudioOutputChanged;

    /// <inheritdoc />
    public string? CurrentDefaultDeviceId => _currentDefaultDeviceId;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (_isStarted)
        {
            return;
        }

        // Get the initial default device ID
        try
        {
            _currentDefaultDeviceId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            LogDebug($"Initial default audio output: {_currentDefaultDeviceId ?? "none"}");
        }
        catch (Exception ex)
        {
            LogDebug($"Error getting initial default audio device: {ex.Message}");
            _currentDefaultDeviceId = null;
        }

        // Subscribe to the Windows event
        MediaDevice.DefaultAudioRenderDeviceChanged += OnDefaultAudioRenderDeviceChanged;
        _isStarted = true;
        
        LogDebug("Started monitoring default audio output changes");
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        MediaDevice.DefaultAudioRenderDeviceChanged -= OnDefaultAudioRenderDeviceChanged;
        _isStarted = false;
        
        LogDebug("Stopped monitoring default audio output changes");
    }

    private void OnDefaultAudioRenderDeviceChanged(object? sender, DefaultAudioRenderDeviceChangedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        // Only process changes for the Default role (not Communications role)
        if (args.Role != AudioDeviceRole.Default)
        {
            return;
        }

        var newDeviceId = args.Id;
        var previousDeviceId = _currentDefaultDeviceId;
        
        // Only fire event if the device actually changed
        if (string.Equals(newDeviceId, previousDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentDefaultDeviceId = newDeviceId;
        
        LogDebug($"Default audio output changed: {previousDeviceId ?? "none"} -> {newDeviceId ?? "none"}");

        RaiseDefaultAudioOutputChanged(newDeviceId);
    }

    private void RaiseDefaultAudioOutputChanged(string? newDeviceId)
    {
        if (_disposed)
        {
            return;
        }

        var handler = DefaultAudioOutputChanged;
        if (handler == null)
        {
            return;
        }

        var eventArgs = new DefaultAudioOutputChangedEventArgs(newDeviceId);
        
        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<DefaultAudioOutputChangedEventArgs>)subscriber).Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                LogDebug($"Exception in DefaultAudioOutputChanged handler: {ex.Message}");
            }
        }
    }

    [Conditional("DEBUG")]
    private static void LogDebug(string message)
    {
        Debug.WriteLine($"[DefaultAudioOutputMonitorService] {message}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        DefaultAudioOutputChanged = null;
        _disposed = true;
    }
}
