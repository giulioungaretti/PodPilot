namespace DeviceCommunication.Services;

/// <summary>
/// Interface for low-level Bluetooth audio device connectors.
/// Implementations provide different strategies for establishing audio connections.
/// </summary>
public interface IBluetoothConnector
{
    /// <summary>
    /// Attempts to connect to a Bluetooth audio device using its MAC address.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address as a ulong.</param>
    /// <returns>True if connection was successful; otherwise, false.</returns>
    Task<bool> ConnectAudioDeviceAsync(ulong address);

    /// <summary>
    /// Attempts to disconnect from a Bluetooth audio device using its MAC address.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address as a ulong.</param>
    /// <returns>True if disconnection was successful; otherwise, false.</returns>
    Task<bool> DisconnectAudioDeviceAsync(ulong address);
}
