namespace DeviceCommunication.Models;

/// <summary>
/// Represents information about a paired Bluetooth device.
/// </summary>
public sealed record PairedDeviceInfo
{
    /// <summary>
    /// The Windows device ID (used for connection operations).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The friendly name of the device.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The Bluetooth MAC address of the device.
    /// </summary>
    public required ulong Address { get; init; }

    /// <summary>
    /// Whether the device is currently connected.
    /// </summary>
    public required bool IsConnected { get; init; }
}
