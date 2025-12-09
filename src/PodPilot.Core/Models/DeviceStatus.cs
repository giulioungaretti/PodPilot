namespace PodPilot.Core.Models;

/// <summary>
/// Represents the current status of the device from the user's perspective.
/// This is the single source of truth for what action is available.
/// </summary>
public enum DeviceStatus
{
    /// <summary>Device is not paired in Windows Bluetooth settings.</summary>
    Unpaired,
    
    /// <summary>Device is paired but not currently connected.</summary>
    Disconnected,
    
    /// <summary>Device is connected but audio is not routing to it.</summary>
    Connected,
    
    /// <summary>Device is connected AND is the default audio output.</summary>
    AudioActive
}
