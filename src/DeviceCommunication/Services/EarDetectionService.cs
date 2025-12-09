using DeviceCommunication.Models;
using Microsoft.Extensions.Logging;
using PodPilot.Core.Models;
using PodPilot.Core.Services;

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
    private readonly ILogger<EarDetectionService> _logger;
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
            
            _logger.LogDebug("Ear detection {Status}", _isEnabled ? "enabled" : "disabled");
        }
    }

    /// <summary>
    /// Creates a new instance of the ear detection service.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mediaController">The media controller for pause/play operations.</param>
    /// <param name="stateService">The state service to get ear detection updates.</param>
    public EarDetectionService(
        ILogger<EarDetectionService> logger,
        IGlobalMediaController mediaController,
        IAirPodsStateService stateService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogDebug("Initialized successfully");
        }
        else
        {
            _logger.LogWarning("Failed to initialize media controller");
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
            _logger.LogError(ex, "Error processing ear detection");
        }
    }

    /// <summary>
    /// Handles the case when AirPods are removed from ears.
    /// Pauses media and remembers that we did it.
    /// </summary>
    private async Task HandleRemovedFromEarAsync()
    {
        _logger.LogDebug("AirPods removed from ear");
        
        // Only pause if something is playing
        if (_mediaController.IsPlaying)
        {
            _logger.LogDebug("Pausing media...");
            var success = await _mediaController.PauseAsync();
            
            if (success)
            {
                // Remember that WE paused it
                _wePausedIt = true;
                _logger.LogDebug("Media paused (we will resume when put back)");
            }
        }
        else
        {
            _logger.LogDebug("Nothing playing, no action needed");
        }
    }

    /// <summary>
    /// Handles the case when AirPods are put back in ears.
    /// Only resumes if WE were the ones who paused it.
    /// </summary>
    private async Task HandlePutInEarAsync()
    {
        _logger.LogDebug("AirPods put in ear");
        
        // Only resume if WE paused it (not if user manually paused)
        if (_wePausedIt)
        {
            _logger.LogDebug("Resuming media (we paused it earlier)...");
            var success = await _mediaController.PlayAsync();
            
            if (success)
            {
                // Clear the flag - we've resumed
                _wePausedIt = false;
                _logger.LogDebug("Media resumed");
            }
        }
        else
        {
            _logger.LogDebug("Not resuming - we didn't pause it");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateService.StateChanged -= OnStateChanged;
    }
}
