using System.Diagnostics;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Service that automatically pauses/resumes media based on AirPods ear detection.
/// </summary>
/// <remarks>
/// <para>
/// When both AirPods are removed from ears, this service pauses the current media.
/// When at least one AirPod is put back in ear, it resumes playback - but only if
/// this service was the one that paused it (not if the user manually paused).
/// </para>
/// <para>
/// This follows the pattern: "Only resume if **we** paused it - don't resume if user manually paused."
/// </para>
/// </remarks>
public sealed class EarDetectionService : IDisposable
{
    private readonly IGlobalMediaController _mediaController;
    private readonly IAirPodsStateService _stateService;
    
    /// <summary>
    /// Tracks whether WE paused the media (vs user pausing manually).
    /// Only resume if this is true - prevents annoying auto-resume when user wants silence.
    /// </summary>
    private bool _wePausedIt;
    
    /// <summary>
    /// The last known in-ear state to detect transitions.
    /// </summary>
    private bool _wasInEar;
    
    /// <summary>
    /// Whether ear detection is enabled.
    /// </summary>
    private bool _isEnabled;
    
    private bool _disposed;

    /// <summary>
    /// Gets or sets whether ear detection auto-pause/resume is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            
            if (!_isEnabled)
            {
                // Reset state when disabled
                _wePausedIt = false;
            }
            
            Debug.WriteLine($"[EarDetectionService] Ear detection {(_isEnabled ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Creates a new instance of the ear detection service.
    /// </summary>
    /// <param name="mediaController">The media controller for pause/play operations.</param>
    /// <param name="stateService">The state service to get ear detection updates.</param>
    public EarDetectionService(IGlobalMediaController mediaController, IAirPodsStateService stateService)
    {
        _mediaController = mediaController ?? throw new ArgumentNullException(nameof(mediaController));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
        
        // Subscribe to state changes
        _stateService.StateChanged += OnStateChanged;
        
        // Default to enabled
        _isEnabled = true;
    }

    /// <summary>
    /// Initializes the service. Must be called before ear detection will work.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_disposed) return;
        
        var initialized = await _mediaController.InitializeAsync();
        
        if (initialized)
        {
            Debug.WriteLine("[EarDetectionService] Initialized successfully");
        }
        else
        {
            Debug.WriteLine("[EarDetectionService] Failed to initialize media controller");
        }
    }

    private async void OnStateChanged(object? sender, AirPodsStateChangedEventArgs args)
    {
        if (_disposed || !_isEnabled) return;

        // Only care about BLE data updates (which have ear detection info)
        if (args.Reason != AirPodsStateChangeReason.BleDataUpdated)
            return;

        try
        {
            var state = args.State;
            
            // Consider "in ear" if at least one pod is in ear
            var isCurrentlyInEar = state.IsInEar;
            
            // Detect transition: was in ear, now not in ear
            if (_wasInEar && !isCurrentlyInEar)
            {
                await HandleRemovedFromEarAsync();
            }
            // Detect transition: was not in ear, now in ear
            else if (!_wasInEar && isCurrentlyInEar)
            {
                await HandlePutInEarAsync();
            }
            
            // Update last known state
            _wasInEar = isCurrentlyInEar;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EarDetectionService] Error processing ear detection: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the case when AirPods are removed from ears.
    /// Pauses media and remembers that we did it.
    /// </summary>
    private async Task HandleRemovedFromEarAsync()
    {
        Debug.WriteLine("[EarDetectionService] AirPods removed from ear");
        
        // Only pause if something is playing
        if (_mediaController.IsPlaying)
        {
            Debug.WriteLine("[EarDetectionService] Pausing media...");
            var success = await _mediaController.PauseAsync();
            
            if (success)
            {
                // Remember that WE paused it
                _wePausedIt = true;
                Debug.WriteLine("[EarDetectionService] Media paused (we will resume when put back)");
            }
        }
        else
        {
            Debug.WriteLine("[EarDetectionService] Nothing playing, no action needed");
        }
    }

    /// <summary>
    /// Handles the case when AirPods are put back in ears.
    /// Only resumes if WE were the ones who paused it.
    /// </summary>
    private async Task HandlePutInEarAsync()
    {
        Debug.WriteLine("[EarDetectionService] AirPods put in ear");
        
        // Only resume if WE paused it (not if user manually paused)
        if (_wePausedIt)
        {
            Debug.WriteLine("[EarDetectionService] Resuming media (we paused it earlier)...");
            var success = await _mediaController.PlayAsync();
            
            if (success)
            {
                // Clear the flag - we've resumed
                _wePausedIt = false;
                Debug.WriteLine("[EarDetectionService] Media resumed");
            }
        }
        else
        {
            Debug.WriteLine("[EarDetectionService] Not resuming - we didn't pause it");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateService.StateChanged -= OnStateChanged;
    }
}
