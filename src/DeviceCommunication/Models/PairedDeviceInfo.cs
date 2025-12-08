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
    /// The Apple Product ID (e.g., 0x2002 for AirPods Pro).
    /// Used for matching BLE advertisements to paired devices.
    /// </summary>
    public required ushort ProductId { get; init; }

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
    public bool IsConnected { get; init; }
}

/// <summary>
/// Event args for paired device state changes.
/// </summary>
public sealed record PairedDeviceChangedEventArgs
{
    /// <summary>
    /// The device that changed.
    /// </summary>
    public required PairedDeviceInfo Device { get; init; }
    
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public required PairedDeviceChangeType ChangeType { get; init; }
}

/// <summary>
/// The type of paired device change.
/// </summary>
public enum PairedDeviceChangeType
{
    /// <summary>A new paired device was discovered.</summary>
    Added,
    
    /// <summary>A paired device was updated (e.g., connection state changed).</summary>
    Updated,
    
    /// <summary>A paired device was removed (unpaired).</summary>
    Removed
}
