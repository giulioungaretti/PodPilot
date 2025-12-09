namespace PodPilot.Core.Services;

/// <summary>
/// Result of a connection attempt.
/// </summary>
public enum ConnectionResult
{
    /// <summary>Connection established successfully.</summary>
    Connected,
    /// <summary>Device is not paired with Windows.</summary>
    NeedsPairing,
    /// <summary>Device was not found.</summary>
    DeviceNotFound,
    /// <summary>Connection failed due to an error.</summary>
    Failed
}

/// <summary>
/// Provides Bluetooth device connection management.
/// </summary>
public interface IBluetoothConnectionService : IDisposable
{
    /// <summary>
    /// Attempts to connect to a Bluetooth device using its Windows device ID.
    /// </summary>
    /// <param name="deviceId">The Windows device ID (from paired device enumeration).</param>
    /// <returns>The connection result.</returns>
    Task<ConnectionResult> ConnectByDeviceIdAsync(string deviceId);

    /// <summary>
    /// Disconnects from a device by Windows device ID.
    /// </summary>
    /// <param name="deviceId">The Windows device ID.</param>
    /// <returns>True if disconnection was successful; otherwise, false.</returns>
    Task<bool> DisconnectByDeviceIdAsync(string deviceId);

    /// <summary>
    /// Gets the connection status of a device by Windows device ID.
    /// </summary>
    /// <param name="deviceId">The Windows device ID.</param>
    /// <returns>True if connected; otherwise, false.</returns>
    bool IsConnectedByDeviceId(string deviceId);
}
