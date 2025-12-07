using System.Diagnostics;
using Windows.Media.Control;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides control over the system's global media playback.
/// Uses Windows.Media.Control APIs to pause/play the current media session.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps the <see cref="GlobalSystemMediaTransportControlsSessionManager"/>
/// which provides access to the currently playing media session across all apps.
/// </para>
/// <para>
/// Use this service for features like automatic pause when AirPods are removed from ears.
/// </para>
/// </remarks>
public sealed class GlobalMediaController : IGlobalMediaController
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private bool _disposed;

    /// <summary>
    /// Gets whether the media controller is initialized and ready to use.
    /// </summary>
    public bool IsInitialized => _sessionManager != null;

    /// <summary>
    /// Gets whether there is currently a media session available.
    /// </summary>
    public bool HasActiveSession => _currentSession != null;

    /// <summary>
    /// Initializes the media controller asynchronously.
    /// Must be called before using Pause/Play methods.
    /// </summary>
    /// <returns>True if initialization was successful; otherwise, false.</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed) return false;

        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            
            if (_sessionManager == null)
            {
                Debug.WriteLine("[GlobalMediaController] Failed to get session manager");
                return false;
            }

            // Subscribe to session changes
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
            
            // Get the current session
            UpdateCurrentSession();
            
            Debug.WriteLine("[GlobalMediaController] Initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMediaController] Initialization error: {ex.Message}");
            return false;
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        if (_disposed) return;
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        if (_sessionManager == null) return;

        try
        {
            _currentSession = _sessionManager.GetCurrentSession();
            
            if (_currentSession != null)
            {
                Debug.WriteLine($"[GlobalMediaController] Current session: {_currentSession.SourceAppUserModelId}");
            }
            else
            {
                Debug.WriteLine("[GlobalMediaController] No active media session");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMediaController] Error getting current session: {ex.Message}");
            _currentSession = null;
        }
    }

    /// <summary>
    /// Pauses the current media playback.
    /// </summary>
    /// <returns>True if pause was successful; otherwise, false.</returns>
    public async Task<bool> PauseAsync()
    {
        if (_disposed) return false;

        try
        {
            UpdateCurrentSession();
            
            if (_currentSession == null)
            {
                Debug.WriteLine("[GlobalMediaController] No session to pause");
                return false;
            }

            var result = await _currentSession.TryPauseAsync();
            Debug.WriteLine($"[GlobalMediaController] Pause result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMediaController] Pause error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Resumes/plays the current media playback.
    /// </summary>
    /// <returns>True if play was successful; otherwise, false.</returns>
    public async Task<bool> PlayAsync()
    {
        if (_disposed) return false;

        try
        {
            UpdateCurrentSession();
            
            if (_currentSession == null)
            {
                Debug.WriteLine("[GlobalMediaController] No session to play");
                return false;
            }

            var result = await _currentSession.TryPlayAsync();
            Debug.WriteLine($"[GlobalMediaController] Play result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMediaController] Play error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the current playback status.
    /// </summary>
    /// <returns>The current playback status, or null if unavailable.</returns>
    public GlobalSystemMediaTransportControlsSessionPlaybackStatus? GetPlaybackStatus()
    {
        if (_disposed) return null;

        try
        {
            UpdateCurrentSession();
            
            if (_currentSession == null)
                return null;

            var playbackInfo = _currentSession.GetPlaybackInfo();
            return playbackInfo?.PlaybackStatus;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMediaController] Error getting playback status: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets whether media is currently playing.
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            var status = GetPlaybackStatus();
            return status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
    }

    /// <summary>
    /// Gets whether media is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            var status = GetPlaybackStatus();
            return status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_sessionManager != null)
        {
            _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _sessionManager = null;
        }

        _currentSession = null;
    }
}
